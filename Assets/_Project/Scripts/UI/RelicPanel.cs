using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Relics;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★★ <b>유물 관리</b> 창 (2026-08-23 신설 · 유저 지시 9번:
    /// *"허드 액션에 유물관리 신설 후 획득 유물 볼 수 있는 칸 만들어야 함(누르면 설명 나오게)"*).
    ///
    /// <b>구조는 토벌 지시 창(<see cref="SubjugationPanel"/>)과 같다</b> — 왼쪽에 목록,
    /// 오른쪽에 상세. 같은 API 모양(<c>Instance</c>/<c>IsOpen</c>/<c>Toggle</c>/<c>SetOpen</c>/
    /// <c>Close</c>)을 쓰고 <see cref="HudExclusive"/> 로 배타 처리한다 —
    /// 창이 하나 늘 때마다 조작감이 갈리지 않게 하려는 것이다.
    ///
    /// <b>무엇을 보여주나</b>
    /// <code>
    ///   목록 : 보유한 유물을 등급 순으로. 칸마다 아이콘 · 이름 · 개수 · «누가 끼고 있나»
    ///   상세 : 고른 유물의 이름 · 등급 · 효과 설명 · 서사 · 출처
    ///   장착 : 지금 로스터에서 <b>선택된 캐릭터</b>에게 끼운다(선택이 없으면 안내만)
    /// </code>
    ///
    /// ★ <b>장착 대상은 «선택된 캐릭터» 다</b> — 성장 창(<see cref="CharacterGrowthPanel"/>)이
    ///   이미 그 규칙을 쓴다(창이 캐릭터를 고르지 않고 <see cref="UnitSelector"/> 를 따라간다).
    ///   유물 창만 다른 방식이면 «어디서 캐릭터를 고르는가» 가 두 벌이 된다.
    ///
    /// ⚠ 씬 배선은 <b>이름으로</b> 찾는다(<see cref="EnsureBound"/>) — 이 프로젝트는 MCP 로
    ///   씬을 만들고 인스펙터 참조를 넣지 못하는 경우가 있어서, 다른 창들도 같은 방식이다.
    /// </summary>
    public class RelicPanel : MonoBehaviour, IExclusiveHudPanel
    {
        static RelicPanel _instance;

        public static RelicPanel Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<RelicPanel>(FindObjectsInactive.Include);
                return _instance;
            }
        }

        [Header("갱신")]
        [Min(0f)] [SerializeField] float refreshInterval = 0.25f;

        [Header("문구")]
        [SerializeField] string hintEmpty = "아직 얻은 유물이 없습니다. 발굴하거나 사냥해 보세요.";
        [SerializeField] string hintPick = "유물을 고르면 설명이 나옵니다.";
        [SerializeField] string hintNoCharacter = "장착하려면 로스터에서 캐릭터를 먼저 고르세요.";
        [SerializeField] string equipLabel = "장착";
        [SerializeField] string unequipLabel = "해제";
        [SerializeField] string countFormat = "x{0}";
        [SerializeField] string wearerFormat = "{0} 착용 중";

        [Header("색")]
        [SerializeField] Color rowNormal = new Color(0.11f, 0.13f, 0.17f, 0.92f);
        [SerializeField] Color rowSelected = new Color(0.16f, 0.42f, 0.38f, 0.98f);

        /// <summary>목록 한 칸.</summary>
        class Row
        {
            public GameObject Root;
            public Image Background;
            public Button Button;
            public Image Icon;
            public TMP_Text Name;
            public TMP_Text Count;
            public int RelicId;
        }

        readonly List<Row> _rows = new List<Row>();

        RectTransform _rowTemplate;
        Transform _list;
        TMP_Text _hint, _detailName, _detailGrade, _detailEffect, _detailFlavor, _detailSource, _detailWearer;
        Image _detailIcon;
        Button _equipButton;
        TMP_Text _equipLabelText;

        int _selectedRelicId;
        float _nextRefresh;
        bool _bound;

        void Awake()
        {
            _instance = this;
            EnsureBound();
            // ⚠⚠ 여기서 자기를 끄지 않는다 — 이 창은 비활성으로 저장돼 있어 Awake 가
            //   «처음 열릴 때» 돈다. 그 자리에서 끄면 영영 안 뜬다(SubjugationPanel 의 ⚠⚠).
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void OnEnable()
        {
            if (RelicInventory.Instance != null)
                RelicInventory.Instance.OnChanged += MarkDirty;
        }

        void OnDisable()
        {
            if (RelicInventory.Instance != null)
                RelicInventory.Instance.OnChanged -= MarkDirty;
        }

        void MarkDirty() => _nextRefresh = 0f;

        void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;
            Rebuild();
        }

        // ------------------------------------------------------------------
        // 열고 닫기 — 다른 창과 같은 API
        // ------------------------------------------------------------------

        public bool IsOpen => gameObject.activeSelf;

        public void Toggle() => SetOpen(!IsOpen);

        public void Close() => gameObject.SetActive(false);

        public void SetOpen(bool open)
        {
            EnsureBound();
            gameObject.SetActive(open);
            if (!open) return;

            HudExclusive.OpenOnly(this);
            _nextRefresh = 0f;
            Rebuild();
        }

        // ------------------------------------------------------------------
        // 그리기
        // ------------------------------------------------------------------

        void Rebuild()
        {
            EnsureBound();
            RelicInventory inv = RelicInventory.Instance;
            if (inv == null || _list == null || _rowTemplate == null) return;

            // ── 목록 : 등급 높은 것 먼저, 그다음 ID 순 ──
            _sorted.Clear();
            foreach (var kv in inv.Owned)
            {
                RelicDefinitionSO r = RelicRegistry.ById(kv.Key);
                if (r != null) _sorted.Add(r);
            }
            _sorted.Sort((a, b) =>
                a.grade != b.grade ? b.grade.CompareTo(a.grade) : a.relicId.CompareTo(b.relicId));

            while (_rows.Count < _sorted.Count) _rows.Add(MakeRow());

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                bool used = i < _sorted.Count;
                if (row.Root.activeSelf != used) row.Root.SetActive(used);
                if (!used) continue;

                RelicDefinitionSO r = _sorted[i];
                row.RelicId = r.relicId;
                if (row.Icon != null)
                {
                    row.Icon.sprite = r.icon;
                    row.Icon.enabled = r.icon != null;
                    row.Icon.color = Color.white;
                }
                if (row.Name != null)
                {
                    row.Name.text = r.DisplayName;
                    row.Name.color = r.GradeColor;
                }
                if (row.Count != null)
                {
                    int n = inv.OwnedCount(r.relicId);
                    row.Count.text = n > 1 ? string.Format(countFormat, n) : "";
                }
                if (row.Background != null)
                    row.Background.color = r.relicId == _selectedRelicId ? rowSelected : rowNormal;

                int captured = r.relicId;
                row.Button.onClick.RemoveAllListeners();
                row.Button.onClick.AddListener(() => Select(captured));
            }

            if (_sorted.Count == 0) _selectedRelicId = 0;
            ShowDetail();
        }

        readonly List<RelicDefinitionSO> _sorted = new List<RelicDefinitionSO>();

        void Select(int relicId)
        {
            _selectedRelicId = relicId;
            _nextRefresh = 0f;
            Rebuild();
        }

        void ShowDetail()
        {
            RelicDefinitionSO r = RelicRegistry.ById(_selectedRelicId);
            RelicInventory inv = RelicInventory.Instance;
            CharacterUnit target = SelectedCharacter();

            if (_detailIcon != null)
            {
                _detailIcon.sprite = r != null ? r.icon : null;
                _detailIcon.enabled = r != null && r.icon != null;
            }
            SetText(_detailName, r != null ? r.DisplayName : "-",
                    r != null ? r.GradeColor : Color.white);
            SetText(_detailGrade, r != null ? RelicDefinitionSO.NameOf(r.grade) : "",
                    r != null ? r.GradeColor : Color.white);
            SetText(_detailEffect, r != null ? r.relicDesc : "", Color.white);
            SetText(_detailFlavor, r != null ? r.relicFlavor : "", Color.white);
            SetText(_detailSource, r != null ? SourceTextOf(r) : "", Color.white);

            // «누가 끼고 있나»
            string wearer = "";
            if (r != null && inv != null)
            {
                int key = inv.WearerOf(r.relicId);
                if (key > 0) wearer = string.Format(wearerFormat, NameOfCharacter(key));
            }
            SetText(_detailWearer, wearer, Color.white);

            if (_hint != null)
                _hint.text = _sorted.Count == 0 ? hintEmpty
                           : r == null ? hintPick
                           : target == null ? hintNoCharacter
                           : hintPick;

            // ── 장착 버튼 ──
            if (_equipButton == null) return;

            bool alreadyOn = r != null && inv != null && target != null &&
                             inv.EquippedOn(target) == r;
            bool canEquip = r != null && inv != null && target != null &&
                            !target.IsSummoned && (alreadyOn || inv.FreeCount(r.relicId) > 0);

            _equipButton.interactable = canEquip;
            if (_equipLabelText != null)
                _equipLabelText.text = alreadyOn ? unequipLabel : equipLabel;

            _equipButton.onClick.RemoveAllListeners();
            RelicDefinitionSO captured = r;
            bool off = alreadyOn;
            _equipButton.onClick.AddListener(() =>
            {
                CharacterUnit unit = SelectedCharacter();
                if (unit == null || RelicInventory.Instance == null) return;
                if (off) RelicInventory.Instance.Unequip(unit);
                else if (!RelicInventory.Instance.TryEquip(unit, captured, out string why))
                    HudLog.Add(why, HudLogKind.Warn);
                _nextRefresh = 0f;
            });
        }

        static string SourceTextOf(RelicDefinitionSO r) => r.source switch
        {
            RelicSource.Boss => "보스 처치로만 얻습니다.",
            RelicSource.DigOnly => "발굴로만 얻습니다.",
            _ => "발굴하거나 일반 몬스터를 사냥해 얻습니다.",
        };

        /// <summary>정의 ID 로 이름을 찾는다 — 죽어서 인스턴스가 없어도 이름은 보여야 한다.</summary>
        static string NameOfCharacter(int definitionId)
        {
            var all = Combat.UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] is CharacterUnit c && RelicInventory.KeyOf(c) == definitionId)
                    return c.DisplayName;
            return "다른 캐릭터";
        }

        static CharacterUnit SelectedCharacter()
        {
            UnitSelector sel = UnitSelector.Instance;
            return sel != null ? sel.Selected as CharacterUnit : null;
        }

        static void SetText(TMP_Text t, string value, Color color)
        {
            if (t == null) return;
            t.text = value ?? "";
            t.color = color;
        }

        Row MakeRow()
        {
            RectTransform clone = Instantiate(_rowTemplate, _list);
            clone.gameObject.SetActive(true);
            clone.name = $"RelicRow_{_rows.Count + 1}";
            return new Row
            {
                Root = clone.gameObject,
                Background = clone.GetComponent<Image>(),
                Button = clone.GetComponent<Button>(),
                Icon = clone.Find("Icon")?.GetComponent<Image>(),
                Name = clone.Find("Name")?.GetComponent<TMP_Text>(),
                Count = clone.Find("Count")?.GetComponent<TMP_Text>(),
            };
        }

        void EnsureBound()
        {
            if (_bound) return;
            _bound = true;

            _list = transform.Find("List/Items");
            _rowTemplate = transform.Find("List/RowTemplate") as RectTransform;
            if (_rowTemplate != null) _rowTemplate.gameObject.SetActive(false);
            else Debug.LogWarning("[유물] List/RowTemplate 을 찾지 못했습니다.", this);

            _hint = FindText(transform, "Hint");
            _detailIcon = transform.Find("Detail/Icon")?.GetComponent<Image>();
            _detailName = FindText(transform, "Detail/Name");
            _detailGrade = FindText(transform, "Detail/Grade");
            _detailEffect = FindText(transform, "Detail/Effect");
            _detailFlavor = FindText(transform, "Detail/Flavor");
            _detailSource = FindText(transform, "Detail/Source");
            _detailWearer = FindText(transform, "Detail/Wearer");

            _equipButton = transform.Find("Detail/EquipButton")?.GetComponent<Button>();
            _equipLabelText = FindText(transform, "Detail/EquipButton/Label");

            var close = transform.Find("CloseButton")?.GetComponent<Button>();
            if (close != null)
            {
                close.onClick.RemoveAllListeners();
                close.onClick.AddListener(Close);
            }
        }

        static TMP_Text FindText(Transform parent, string path)
        {
            Transform t = parent.Find(path);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }
    }
}
