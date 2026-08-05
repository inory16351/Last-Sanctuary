using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.CameraControl;
using LastSanctuary.Combat;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 좌측 상단 캐릭터 로스터. 캐릭터마다 한 줄씩 상태(이름·HP·능력치·현재 행동)를 보여주고,
    /// 줄을 누르면 그 캐릭터가 선택되며, 줄 안의 버튼으로 바로 강화할 수 있다.
    ///
    /// <b>행은 모체 하나를 복제해서 만든다</b>(<see cref="rowTemplate"/>) — 오브젝트는 하이라키에
    /// 만들되 개수가 런타임에 정해지는 반복 요소만 스크립트가 복제한다는 규칙
    /// (Docs/UI 브렌치 준수사항.md §10 H-2). 유닛 템플릿 복제 패턴(진행상황 5절)과 같은 모양이다.
    ///
    /// 갱신은 두 갈래다:
    ///   - <b>HP 바</b>: <see cref="DamageableUnit.OnHpChanged"/> 를 행마다 구독해 <b>즉시</b> 반영한다.
    ///     맞는 순간 바로 줄어들어야 하는 값이라 폴링 지연을 두지 않는다.
    ///   - <b>그 외(이름·능력치·행동·선택 표시·강화 가능 여부)</b>: <see cref="refreshInterval"/> 마다
    ///     한 번씩 — 매 프레임 전수 조회를 피하기 위한 것이다(준수사항 U-D10).
    /// 재구성(행 개수 맞추기)은 살아있는 캐릭터 집합이 바뀌었을 때만(생성·사망) 일어난다.
    /// </summary>
    public class CharacterRosterPanel : MonoBehaviour
    {
        [Header("하이라키 연결 (비워두면 자식에서 이름으로 찾는다)")]
        [Tooltip("행이 쌓이는 컨테이너 (VerticalLayoutGroup). 비우면 자식 \"List\"")]
        [SerializeField] RectTransform listRoot;

        [Tooltip("복제할 행의 원본. 비우면 자식 \"RowTemplate\". 비활성으로 둘 것")]
        [SerializeField] RectTransform rowTemplate;

        [Header("갱신")]
        [Tooltip("HP·행동·비용을 다시 읽는 주기(초). 0 이면 매 프레임")]
        [Min(0f)] [SerializeField] float refreshInterval = 0.2f;

        [Header("색")]
        [SerializeField] Color rowNormal = new Color(0.10f, 0.12f, 0.16f, 0.70f);
        [SerializeField] Color rowSelected = new Color(0.13f, 0.28f, 0.26f, 0.90f);

        [Tooltip("HP 가 이 비율 아래로 내려가면 막대가 붉게 바뀐다")]
        [Range(0f, 1f)] [SerializeField] float lowHpRatio = 0.35f;

        [Header("카메라")]
        [Tooltip("행을 누르면 카메라를 그 캐릭터 위치로 옮긴다")]
        [SerializeField] bool focusCameraOnSelect = true;

        [Tooltip("켜면 감쇠 없이 즉시 이동(SnapTo), 끄면 부드럽게 이동(FocusOn)")]
        [SerializeField] bool snapCamera = false;

        /// <summary>복제된 행 하나가 참조하는 조각들. 매번 GetComponent 하지 않으려고 캐시한다.</summary>
        class Row
        {
            public GameObject Root;
            public Image Background;
            public Button SelectButton;
            public TMP_Text Name;
            public TMP_Text Duty;
            public TMP_Text Stats;
            public Image HpFill;
            public Button UpgradeButton;
            public TMP_Text UpgradeLabel;
            public CharacterUnit Unit;

            /// <summary>지금 <see cref="HpHandler"/> 를 구독하고 있는 대상. 행이 재활용되어
            /// 다른 캐릭터로 바뀔 때 이전 구독을 정확히 끊기 위해 <see cref="Unit"/> 과 따로 든다.</summary>
            public DamageableUnit SubscribedUnit;

            /// <summary>이 행에 고정된 HP 변경 핸들러. 구독/해제에 매번 같은 델리게이트가 필요하다.</summary>
            public System.Action<int, int> HpHandler;
        }

        readonly List<Row> _rows = new List<Row>();
        readonly List<CharacterUnit> _characters = new List<CharacterUnit>();
        readonly List<CharacterUnit> _scratch = new List<CharacterUnit>();

        UnitSelector _selector;
        CharacterUpgradeService _upgrades;
        CameraRigController _cameraRig;
        float _nextRefresh;

        void Start()
        {
            _selector = UnitSelector.Instance;
            _upgrades = CharacterUpgradeService.Instance;
            _cameraRig = FindAnyObjectByType<CameraRigController>();

            // MCP 로는 씬 오브젝트 참조를 인스펙터에 넣을 수 없어서(진행상황 8절 4번),
            // 비어 있으면 이름으로 찾는다. 인스펙터에서 직접 넣으면 그 값이 우선이다.
            // "List" 는 스크롤(ScrollView/Viewport) 안으로 옮겨졌으므로 경로가 한 단계 깊다.
            if (listRoot == null) listRoot = transform.Find("ScrollView/Viewport/List") as RectTransform;
            if (rowTemplate == null) rowTemplate = transform.Find("RowTemplate") as RectTransform;

            if (rowTemplate == null || listRoot == null)
            {
                Debug.LogError("[Roster] listRoot / rowTemplate 이 연결되지 않았습니다. " +
                               "HUD_Roster 의 ScrollView/Viewport/List 와 RowTemplate 을 인스펙터에 넣어주세요.", this);
                enabled = false;
                return;
            }

            rowTemplate.gameObject.SetActive(false);
            BindScrollRect();
            Rebuild();
        }

        /// <summary>
        /// ScrollRect·Scrollbar 의 object-참조 필드(content/viewport/handleRect 등)는
        /// MCP 로 넣을 수 없다(진행상황 8절 4번) — 그래서 이름으로 찾아 코드에서 직접 연결한다.
        /// 인스펙터에서 이미 연결돼 있으면 건드리지 않는다(사람이 직접 맞춘 값이 우선).
        /// </summary>
        void BindScrollRect()
        {
            var scrollRect = transform.Find("ScrollView")?.GetComponent<ScrollRect>();
            if (scrollRect == null) return;

            if (scrollRect.content == null) scrollRect.content = listRoot;
            if (scrollRect.viewport == null)
                scrollRect.viewport = transform.Find("ScrollView/Viewport") as RectTransform;

            if (scrollRect.verticalScrollbar == null)
            {
                var scrollbar = transform.Find("Scrollbar")?.GetComponent<Scrollbar>();
                if (scrollbar != null)
                {
                    scrollRect.verticalScrollbar = scrollbar;

                    if (scrollbar.handleRect == null)
                        scrollbar.handleRect = transform.Find("Scrollbar/Handle") as RectTransform;
                    if (scrollbar.targetGraphic == null)
                        scrollbar.targetGraphic = transform.Find("Scrollbar/Handle")?.GetComponent<Image>();
                }
            }
        }

        void OnDestroy()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                if (row.SubscribedUnit != null) row.SubscribedUnit.OnHpChanged -= row.HpHandler;
            }
        }

        void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;

            if (_selector == null) _selector = UnitSelector.Instance;
            if (_upgrades == null) _upgrades = CharacterUpgradeService.Instance;

            if (CharacterSetChanged()) Rebuild();
            RefreshValues();
        }

        // ------------------------------------------------------------------

        /// <summary>살아있는 캐릭터 목록을 모은다. 죽으면 오브젝트가 파괴되므로 레지스트리가 정본이다.</summary>
        void CollectCharacters(List<CharacterUnit> into)
        {
            into.Clear();
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] is CharacterUnit c && c.IsAlive) into.Add(c);
        }

        bool CharacterSetChanged()
        {
            CollectCharacters(_scratch);
            if (_scratch.Count != _characters.Count) return true;

            for (int i = 0; i < _scratch.Count; i++)
                if (!ReferenceEquals(_scratch[i], _characters[i])) return true;

            return false;
        }

        /// <summary>행 개수를 캐릭터 수에 맞추고 각 행에 캐릭터를 물린다.</summary>
        void Rebuild()
        {
            CollectCharacters(_characters);

            // 모자라면 모체를 복제해서 채운다. 남으면 끄기만 하고 파괴하지 않는다
            // (캐릭터가 죽었다 다시 생기는 일이 잦아서, 매번 Destroy 하면 GC 만 늘어난다).
            while (_rows.Count < _characters.Count)
                _rows.Add(CreateRow(_rows.Count));

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                bool used = i < _characters.Count;

                if (row.Root.activeSelf != used) row.Root.SetActive(used);

                CharacterUnit newUnit = used ? _characters[i] : null;
                if (ReferenceEquals(row.Unit, newUnit)) continue;

                // 행이 다른 캐릭터로 바뀌는 순간(재활용 포함) 구독을 정확히 갈아탄다 —
                // 이전 대상의 이벤트를 계속 듣고 있으면 남의 HP 로 이 행이 갱신되는
                // 사고가 난다.
                if (row.SubscribedUnit != null) row.SubscribedUnit.OnHpChanged -= row.HpHandler;

                row.Unit = newUnit;
                row.SubscribedUnit = newUnit;

                if (newUnit != null)
                {
                    newUnit.OnHpChanged += row.HpHandler;
                    ApplyHp(row, newUnit.CurrentHp, newUnit.MaxHp);   // 재구성 즉시 최신 HP로
                }
            }
        }

        Row CreateRow(int index)
        {
            RectTransform clone = Instantiate(rowTemplate, listRoot);
            clone.name = $"Row_{index + 1}";
            clone.gameObject.SetActive(true);

            var row = new Row
            {
                Root = clone.gameObject,
                Background = clone.GetComponent<Image>(),
                SelectButton = clone.GetComponent<Button>(),
                Name = FindText(clone, "Name"),
                Duty = FindText(clone, "Duty"),
                Stats = FindText(clone, "Stats"),
            };

            Transform hpBack = clone.Find("HpBack");
            if (hpBack != null)
            {
                Transform fill = hpBack.Find("HpFill");
                if (fill != null) row.HpFill = fill.GetComponent<Image>();
            }

            Transform upgrade = clone.Find("RowUpgrade");
            if (upgrade != null)
            {
                row.UpgradeButton = upgrade.GetComponent<Button>();
                row.UpgradeLabel = FindText(upgrade, "Label");
            }

            // 람다가 row 를 잡아두므로 행이 다른 캐릭터로 바뀌어도 항상 지금 물린 캐릭터를 쓴다.
            if (row.SelectButton != null)
                row.SelectButton.onClick.AddListener(() => SelectRow(row));

            if (row.UpgradeButton != null)
                row.UpgradeButton.onClick.AddListener(() => UpgradeRow(row));

            // HP 핸들러도 같은 이유로 row 를 닫아 두고 한 번만 만든다 — Rebuild() 가
            // 구독/해제할 때마다 새 델리게이트를 만들면 구독 해제(-=)가 다른 인스턴스를
            // 지우려다 실패한다(C# 이벤트는 참조가 같아야 -= 가 먹는다).
            row.HpHandler = (current, max) => ApplyHp(row, current, max);

            return row;
        }

        /// <summary>HP 바 채움·색만 즉시 반영한다. <see cref="DamageableUnit.OnHpChanged"/> 구독 콜백.</summary>
        void ApplyHp(Row row, int current, int max)
        {
            if (row.HpFill == null) return;

            float ratio = max > 0 ? (float)current / max : 0f;
            row.HpFill.fillAmount = ratio;
            row.HpFill.color = ratio <= lowHpRatio ? HudTheme.BarHpLow : HudTheme.BarHp;
        }

        static TMP_Text FindText(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        void SelectRow(Row row)
        {
            if (row.Unit == null || !row.Unit.IsAlive) return;
            if (_selector == null) _selector = UnitSelector.Instance;
            _selector?.Select(row.Unit);
            FocusCameraOn(row.Unit);
            RefreshValues();
        }

        /// <summary>
        /// 카메라를 그 캐릭터로 옮긴다. 카메라는 <c>CameraAnchor</c> 오브젝트가 움직이고
        /// 시네머신이 따라오는 구조라(진행상황 1·7절), 여기서도 카메라가 아니라 리그를 부른다.
        /// 맵 경계 밖으로는 <c>CameraRigController</c> 가 알아서 잘라준다.
        /// </summary>
        void FocusCameraOn(CharacterUnit unit)
        {
            if (!focusCameraOnSelect || unit == null) return;
            if (_cameraRig == null) _cameraRig = FindAnyObjectByType<CameraRigController>();
            if (_cameraRig == null) return;

            if (snapCamera) _cameraRig.SnapTo(unit.transform.position);
            else _cameraRig.FocusOn(unit.transform.position);
        }

        void UpgradeRow(Row row)
        {
            if (row.Unit == null || !row.Unit.IsAlive) return;
            if (_upgrades == null) _upgrades = CharacterUpgradeService.Instance;
            if (_upgrades == null) return;

            // 강화 대상이 곧 선택 대상이 되도록 맞춰준다 — 우측 강화 버튼과 헷갈리지 않게.
            _selector?.Select(row.Unit);

            int cost = _upgrades.CostFor(row.Unit);
            if (_upgrades.TryUpgrade(row.Unit))
                HudLog.Add($"{row.Unit.name} 강화 (−{cost})", HudLogKind.Good);
            else
                HudLog.Add($"강화 실패 — 에너지 {cost} 필요", HudLogKind.Warn);

            RefreshValues();
        }

        // ------------------------------------------------------------------

        void RefreshValues()
        {
            CharacterUnit selected = _selector != null ? _selector.Selected : null;

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                if (!row.Root.activeSelf || row.Unit == null) continue;

                CharacterUnit unit = row.Unit;

                if (row.Name != null) row.Name.text = unit.name;

                if (row.Stats != null)
                {
                    StatBlock s = unit.Stats;
                    row.Stats.text = $"체{s.hp} 공{s.attack} 방{s.defense} 회{s.regen}" +
                                     (unit.UpgradeCount > 0 ? $"  +{unit.UpgradeCount}" : string.Empty);
                }

                // HP 바는 여기서 건드리지 않는다 — OnHpChanged 구독(ApplyHp)이 즉시 반영한다.
                // 폴링과 이벤트가 같은 값을 이중으로 쓰면 순서에 따라 잠깐 어긋나 보일 수 있다.

                if (row.Duty != null) row.Duty.text = DutyTextOf(unit);

                if (row.Background != null)
                    row.Background.color = ReferenceEquals(unit, selected) ? rowSelected : rowNormal;

                if (row.UpgradeButton != null)
                {
                    bool can = _upgrades != null && _upgrades.CanUpgrade(unit);
                    row.UpgradeButton.interactable = can;
                    if (row.UpgradeLabel != null)
                        row.UpgradeLabel.text = _upgrades != null
                            ? $"강화 {_upgrades.CostFor(unit)}"
                            : "강화";
                }
            }
        }

        /// <summary>"지금 뭐 하는 중인지" 한 단어. 전투가 자율 이동보다 우선이라 먼저 검사한다.</summary>
        static string DutyTextOf(CharacterUnit unit)
        {
            var combat = unit.GetComponent<UnitCombat>();
            if (combat != null && combat.Target != null && combat.Target.IsAlive)
                return combat.IsHunting ? "사냥" : "교전";

            var behavior = unit.GetComponent<CharacterBehavior>();
            if (behavior == null) return "-";

            return behavior.Duty switch
            {
                CharacterDuty.Scout => "정찰",
                CharacterDuty.Rally => "집결",
                _                   => "방어",
            };
        }
    }
}
