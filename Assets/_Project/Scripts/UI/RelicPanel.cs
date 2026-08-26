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
        [SerializeField] string hintEmptyKey = "ui_relic_hint_empty";
        [SerializeField] string hintPick = "유물을 고르면 설명이 나옵니다.";
        [SerializeField] string hintPickKey = "ui_relic_hint_pick";
        [SerializeField] string hintNoCharacter = "장착하려면 로스터에서 캐릭터를 먼저 고르세요.";
        [SerializeField] string hintNoCharacterKey = "ui_relic_hint_no_character";

        // ★ 유물 칸 (2026-08-26 · 1칸 → 3칸)
        [Tooltip("{0} = 쓰고 있는 칸 · {1} = 전체 칸")]
        [SerializeField] string slotFormat = "(유물 칸 {0}/{1})";
        [SerializeField] string equipLabel = "장착";
        [SerializeField] string unequipLabel = "해제";
        [SerializeField] string countFormat = "x{0}";
        [SerializeField] string wearerFormat = "{0} 착용 중";

        // ★★ 목록 칸 오른쪽 끝의 «누가 끼고 있나» (2026-08-26 · 유저 지시:
        //   *"유물 목록 버튼 오른쪽 끝에 장착하고 있는 캐릭터 나오게"*).
        [Tooltip("목록 칸에 적을 착용자. {0} = 이름")]
        [SerializeField] string rowWearerFormat = "{0}";

        [Tooltip("둘 이상이 끼고 있을 때. {0} = 첫 이름 · {1} = 나머지 수")]
        [SerializeField] string rowWearerMoreFormat = "{0} 외 {1}";

        [Tooltip("고른 캐릭터가 아닌 <b>다른</b> 캐릭터에게서 벗길 때의 버튼 문구")]
        [SerializeField] string unequipOtherLabel = "해제";

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

            /// <summary>★ 칸 오른쪽 끝의 착용자 이름 (2026-08-26). 아무도 안 끼면 빈 글자.</summary>
            public TMP_Text Wearer;

            public int RelicId;
        }

        readonly List<Row> _rows = new List<Row>();

        RectTransform _rowTemplate;
        Transform _list;
        TMP_Text _hint, _detailName, _detailGrade, _detailEffect, _detailFlavor, _detailSource, _detailWearer;
        Image _detailIcon;
        Button _equipButton;
        TMP_Text _equipLabelText;

        // ★ 해제 버튼 (2026-08-26) — 씬에 없으면 null 이고 장착 버튼이 예전처럼 토글로 돈다.
        Button _unequipButton;
        TMP_Text _unequipLabelText;

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

        /// <summary>
        /// ★ <b>그 유물을 골라서 창을 편다</b> (2026-08-26 — 성장 창의 유물 칸 셋이 쓰는 입구).
        /// 스킬 칸이 스킬 상세를 띄우는 것과 같은 자리다.
        /// ⚠ 여는 것이 먼저다 — <see cref="Select"/> 가 <see cref="Rebuild"/> 를 부르고,
        ///   그 안에서 씬 배선(<see cref="EnsureBound"/>)이 돌아야 한다.
        /// </summary>
        public void FocusRelic(int relicId)
        {
            SetOpen(true);
            if (relicId > 0) Select(relicId);
        }

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
                    row.Count.text = n > 1
                        ? string.Format(Data.StringTable.Get("ui_relic_count", countFormat), n) : "";
                }
                if (row.Wearer != null)
                    row.Wearer.text = WearerTextOf(inv, r.relicId);
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

        /// <summary>착용자 조회용 임시 목록 — 매 칸마다 새로 만들지 않는다(칸이 45개까지 간다).</summary>
        readonly List<int> _wearerKeys = new List<int>();

        /// <summary>
        /// ★★ 목록 칸 오른쪽 끝에 적을 «누가 끼고 있나» (2026-08-26).
        ///
        /// ★ <b>「착용 중」 같은 꼬리말을 붙이지 않는다</b> — 칸의 오른쪽 끝은 88px 뿐이라
        ///   이름만으로도 긴데(「아르세니아」) 꼬리말을 붙이면 글자가 절반으로 줄어든다.
        ///   상세 칸은 넓으므로 거기서는 <see cref="wearerFormat"/>(「엘린 착용 중」)을 쓴다.
        /// ⚠⚠ <b>둘 이상은 «있을 수 없는 상태» 다</b>(2026-08-26 · 유저 지시 «중복 장착 금지»).
        ///   같은 유물은 장부에 <b>하나만</b> 있고(<see cref="RelicInventory.Grant"/>),
        ///   장착은 수량을 세어 막고(<see cref="RelicInventory.TryEquip"/>), 저장을 되살릴 때도
        ///   자른다(<see cref="RelicInventory.Restore"/>). 그래도 여기서 둘이 나오면 <b>장부가
        ///   깨진 것</b>이므로 «외 N» 으로 <b>감추지 않고 경고를 남긴다</b> — 조용히 예쁘게
        ///   보여주면 그 고장을 아무도 모른다.
        /// </summary>
        string WearerTextOf(RelicInventory inv, int relicId)
        {
            if (inv == null) return "";
            _wearerKeys.Clear();
            inv.CollectWearers(relicId, _wearerKeys);
            if (_wearerKeys.Count == 0) return "";

            string first = NameOfCharacter(_wearerKeys[0]);
            if (_wearerKeys.Count == 1)
                return string.Format(Data.StringTable.Get("ui_relic_row_wearer", rowWearerFormat), first);

            Debug.LogWarning($"[유물] {relicId} 를 {_wearerKeys.Count} 명이 끼고 있습니다 — " +
                             "한 유물은 한 명만 낄 수 있습니다(장부가 깨졌습니다).", this);
            return string.Format(Data.StringTable.Get("ui_relic_wearer_more", rowWearerMoreFormat),
                                 first, _wearerKeys.Count - 1);
        }

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
            SetText(_detailEffect, r != null ? r.Desc : "", Color.white);
            SetText(_detailFlavor, r != null ? r.Flavor : "", Color.white);
            SetText(_detailSource, r != null ? SourceTextOf(r) : "", Color.white);

            // «누가 끼고 있나»
            string wearer = "";
            if (r != null && inv != null)
            {
                int key = inv.WearerOf(r.relicId);
                if (key > 0)
                    wearer = string.Format(Data.StringTable.Get("ui_relic_wearer", wearerFormat),
                                           NameOfCharacter(key));
            }
            SetText(_detailWearer, wearer, Color.white);

            if (_hint != null)
            {
                // ★ 칸이 셋이 되면서 «왜 못 끼는지» 가 «수량» 말고 «칸» 일 수도 있게 됐다.
                //   그래서 고른 캐릭터의 칸 상태를 그대로 적는다 — 버튼이 회색인 이유가 보인다.
                string slotNote = inv != null && target != null
                    ? string.Format(Data.StringTable.Get("ui_relic_slot_format", slotFormat),
                                    inv.UsedSlots(target), inv.EquipSlots)
                    : string.Empty;

                string pick = Data.StringTable.Get(hintPickKey, hintPick);
                _hint.text =
                      _sorted.Count == 0 ? Data.StringTable.Get(hintEmptyKey, hintEmpty)
                    : r == null ? pick
                    : target == null ? Data.StringTable.Get(hintNoCharacterKey, hintNoCharacter)
                    : string.IsNullOrEmpty(slotNote) ? pick
                    : $"{pick}  {slotNote}";
            }

            // ── 장착 · 해제 두 버튼 ──
            //
            // ★★★ 2026-08-26 — <b>토글 하나를 버튼 둘로 갈랐다</b> (유저 지시:
            //   *"누가 장착하고 있든 바로 유물관리 칸에서 해제할 수 있게"*).
            //
            //   토글이었을 때는 «고른 캐릭터가 꼈나» 만 물어서, <b>다른 캐릭터가 낀 유물은
            //   이 창에서 벗길 길이 없었다</b> — 그 캐릭터를 로스터에서 먼저 골라야 했다.
            //   그렇다고 «남이 끼고 있으면 해제» 로 뜻을 바꾸면, 여분이 있는데도 장착을
            //   못 누르게 된다(같은 유물을 둘 이상 가질 수 있다). 그래서 <b>둘로 나눴다</b>:
            //     장착 = 고른 캐릭터에게 (빈 칸 + 여분이 있을 때)
            //     해제 = <b>누가 끼고 있든</b> (고른 캐릭터가 꼈으면 그 캐릭터부터)
            //
            // ⚠ 씬에 <c>Detail/UnequipButton</c> 이 없어도 죽지 않는다 — 옛 씬에서는
            //   장착 버튼만 예전처럼 «토글» 로 돈다(아래 <c>_unequipButton == null</c> 갈래).

            bool alreadyOn = r != null && inv != null && target != null &&
                             inv.IsEquippedOn(target, r);
            bool hasFreeSlot = inv != null && target != null &&
                               inv.UsedSlots(target) < inv.EquipSlots;
            bool canEquip = r != null && inv != null && target != null &&
                            !target.IsSummoned && hasFreeSlot &&
                            inv.FreeCount(r.relicId) > 0;

            // «누가 끼고 있나» — 고른 캐릭터를 먼저 본다(같은 유물을 둘이 낄 수 있다).
            CharacterUnit wearerUnit = alreadyOn ? target : null;
            if (wearerUnit == null && r != null && inv != null)
            {
                _wearerKeys.Clear();
                inv.CollectWearers(r.relicId, _wearerKeys);
                for (int i = 0; i < _wearerKeys.Count && wearerUnit == null; i++)
                    wearerUnit = CharacterOfKey(_wearerKeys[i]);
            }

            if (_unequipButton != null)
            {
                _unequipButton.interactable = wearerUnit != null;
                if (_unequipLabelText != null)
                    _unequipLabelText.text = Data.StringTable.Get("ui_relic_unequip",
                                                 alreadyOn ? unequipLabel : unequipOtherLabel);

                RelicDefinitionSO offRelic = r;
                CharacterUnit offUnit = wearerUnit;
                _unequipButton.onClick.RemoveAllListeners();
                _unequipButton.onClick.AddListener(() =>
                {
                    if (offUnit == null || offRelic == null || RelicInventory.Instance == null) return;
                    RelicInventory.Instance.Unequip(offUnit, offRelic);
                    _nextRefresh = 0f;
                });
            }

            if (_equipButton == null) return;

            // 해제 버튼이 없는 옛 씬에서는 예전처럼 토글로 둔다.
            bool toggleMode = _unequipButton == null;
            _equipButton.interactable = toggleMode ? (canEquip || alreadyOn) : canEquip;
            if (_equipLabelText != null)
                _equipLabelText.text = toggleMode && alreadyOn
                    ? Data.StringTable.Get("ui_relic_unequip", unequipLabel)
                    : Data.StringTable.Get("ui_relic_equip", equipLabel);

            _equipButton.onClick.RemoveAllListeners();
            RelicDefinitionSO captured = r;
            bool off = toggleMode && alreadyOn;
            _equipButton.onClick.AddListener(() =>
            {
                CharacterUnit unit = SelectedCharacter();
                if (unit == null || RelicInventory.Instance == null) return;
                if (off) RelicInventory.Instance.Unequip(unit, captured);
                else if (!RelicInventory.Instance.TryEquip(unit, captured, out string why))
                    HudLog.Add(why, HudLogKind.Warn);
                _nextRefresh = 0f;
            });
        }

        static string SourceTextOf(RelicDefinitionSO r) => r.source switch
        {
            RelicSource.Boss => Data.StringTable.Get("ui_relic_src_boss", "보스 처치로만 얻습니다."),
            RelicSource.DigOnly => Data.StringTable.Get("ui_relic_src_dig", "발굴로만 얻습니다."),
            RelicSource.Event => Data.StringTable.Get("ui_relic_src_event", "사건에서만 얻습니다."),
            _ => Data.StringTable.Get("ui_relic_src_common", "발굴하거나 일반 몬스터를 사냥해 얻습니다."),
        };

        /// <summary>정의 ID 로 이름을 찾는다 — 죽어서 인스턴스가 없어도 이름은 보여야 한다.</summary>
        static string NameOfCharacter(int definitionId)
        {
            CharacterUnit c = CharacterOfKey(definitionId);
            return c != null ? c.DisplayName : Data.StringTable.Get("ui_relic_other_wearer", "다른 캐릭터");
        }

        /// <summary>
        /// ★ 정의 ID 로 <b>살아 있는 인스턴스</b>를 찾는다 (2026-08-26).
        ///
        /// <see cref="RelicInventory.Unequip"/> 는 <b>캐릭터를 받는다</b> — 보정을 되돌리려면
        /// 그 인스턴스가 있어야 하기 때문이다(<c>RelicEffectService.OnUnequipped</c>).
        /// 그래서 «다른 캐릭터에게서 벗기기» 는 그 인스턴스를 찾아야 성립한다.
        /// ⚠ <b>못 찾으면 null</b>이다 — 판이 끝나 인스턴스가 사라진 경우다. 그때는 해제 버튼이
        ///   꺼진다(장부만 고치면 보정이 남아 «벗겼는데 능력치가 그대로» 가 된다).
        /// </summary>
        static CharacterUnit CharacterOfKey(int definitionId)
        {
            if (definitionId <= 0) return null;
            var all = Combat.UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] is CharacterUnit c && RelicInventory.KeyOf(c) == definitionId)
                    return c;
            return null;
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
            var row = new Row
            {
                Root = clone.gameObject,
                Background = clone.GetComponent<Image>(),
                Button = clone.GetComponent<Button>(),
                Icon = clone.Find("Icon")?.GetComponent<Image>(),
                Name = clone.Find("Name")?.GetComponent<TMP_Text>(),
                Count = clone.Find("Count")?.GetComponent<TMP_Text>(),
                Wearer = clone.Find("Wearer")?.GetComponent<TMP_Text>(),
            };

            // ★ 착용자는 <b>오른쪽 끝에 붙여</b> 오른쪽 정렬로 적는다 — 이름 길이가 제각각이라
            //   가운데 정렬로 두면 칸마다 시작점이 달라 «들쭉날쭉» 해 보인다.
            // ⚠ 정렬·색은 <b>코드가 정한다</b> — MCP 로 TMP 의 정렬 칸을 넣지 못한다(8절 4번).
            if (row.Wearer != null)
            {
                row.Wearer.alignment = TextAlignmentOptions.MidlineRight;
                row.Wearer.color = HudTheme.TextDim;
            }

            // ★ 이름 칸은 <b>한 줄</b>로 둔다(칸 높이가 42px 뿐이다) — 「각성한 수지상세포」
            //   처럼 긴 이름은 줄바꿈 대신 글자가 줄어들어 들어간다.
            HudTheme.FitText(row.Name, 11f, wrap: false);
            HudTheme.FitText(row.Count, 10f, wrap: false);
            HudTheme.FitText(row.Wearer, 9f, wrap: false);
            return row;
        }

        void EnsureBound()
        {
            if (_bound) return;
            _bound = true;

            // ★ 목록 칸은 «스크롤 뷰 안» 으로 옮겨졌다 (2026-08-24) — 유물이 많아지면
            //   칸이 창 밖으로 삐져나오던 것을 막기 위해서다. 옛 경로(List/Items)도 그대로
            //   찾는다 — 씬이 아직 옛 구조인 상태에서도 창이 조용히 비지 않게 하는 폴백이다.
            _list = transform.Find("List/ScrollView/Viewport/Items")
                 ?? transform.Find("List/Items");
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

            _unequipButton = transform.Find("Detail/UnequipButton")?.GetComponent<Button>();
            _unequipLabelText = FindText(transform, "Detail/UnequipButton/Label");

            FitDetailTexts();

            var close = transform.Find("CloseButton")?.GetComponent<Button>();
            if (close != null)
            {
                close.onClick.RemoveAllListeners();
                close.onClick.AddListener(Close);
            }

            BindScrollRect();
        }

        /// <summary>
        /// ★★ <b>상세 칸의 글자가 칸을 넘지 않게 한다</b> (2026-08-24 · 유저 지시:
        /// *"유물 ui안에 텍스트 배치할때 텍스트가 짤리지 않도록"*).
        ///
        /// 유물 45종의 설명은 길이가 제각각이다 — 「명중률이 2 증가합니다.」(11자)부터
        /// 「웨이브가 시작될 때 최대 체력의 15% 만큼 보호막을 20초 동안 두릅니다.」(38자)까지
        /// 세 배 넘게 차이가 난다. 서사(<c>relicFlavor</c>)는 더 길어 45자에 이른다.
        /// <b>가장 긴 것에 칸을 맞추면 짧은 것이 허전하고, 짧은 것에 맞추면 긴 것이 넘친다</b> —
        /// 그래서 칸은 넉넉히 두고 <b>글자 쪽이 줄어들게</b> 했다(<see cref="HudTheme.FitText"/>).
        ///
        /// ★ <b>최소 크기를 칸마다 다르게</b> 준다 — 이름·설명은 반드시 읽혀야 하므로 덜
        ///   줄이고(15·12), 서사·출처는 곁들이는 글이라 더 줄여도 된다(11·10).
        /// ⚠ <b>목록 칸의 이름은 <see cref="MakeRow"/> 에서</b> 따로 맞춘다 — 그 칸은
        ///   여기서 배선할 때 아직 복제되지 않았다.
        /// </summary>
        void FitDetailTexts()
        {
            HudTheme.FitText(_detailName, 15f);
            HudTheme.FitText(_detailGrade, 11f);
            HudTheme.FitText(_detailEffect, 12f);
            HudTheme.FitText(_detailFlavor, 11f);
            HudTheme.FitText(_detailSource, 10f);
            HudTheme.FitText(_detailWearer, 10f);
            HudTheme.FitText(_hint, 10f);
            // 버튼 문구는 <b>한 줄</b>이다 — 「장착」/「해제」 두 글자라 줄바꿈이 필요 없다.
            HudTheme.FitText(_equipLabelText, 12f, wrap: false);
            HudTheme.FitText(_unequipLabelText, 12f, wrap: false);
        }

        /// <summary>
        /// ★★ 목록을 <b>스크롤로 넘긴다</b> (2026-08-24 · 유저 지시:
        /// *"유물 획득 많이 하면 UI 아래로 창 삐져 나오는거 막고 스크롤바로 컨트롤하게"*).
        ///
        /// 유물이 45종이고 칸 하나가 44+4 px 이라 <b>여덟 칸</b>이면 목록 틀(388 px)을 넘는다.
        /// 그 위는 그동안 <b>잘리지도 않고</b> 창 밖으로 계속 그려지고 있었다 —
        /// <c>Items</c> 가 <c>ContentSizeFitter</c> 로 자기 키를 늘리기만 했기 때문이다.
        ///
        /// <b>구조는 로스터 창과 같다</b>(<see cref="CharacterRosterPanel"/>) —
        /// <c>List/ScrollView</c>(ScrollRect) → <c>Viewport</c>(RectMask2D) → <c>Items</c>(내용),
        /// 그 옆에 <c>List/Scrollbar</c>. 창이 하나 늘 때마다 조작감이 갈리지 않게 하려는 것이다.
        ///
        /// ⚠ <c>ScrollRect</c>·<c>Scrollbar</c> 의 <b>object-참조 필드</b>
        ///   (content / viewport / verticalScrollbar / handleRect / targetGraphic)는
        ///   MCP 로 넣을 수 없다(진행상황 8절 4번) — 그래서 이름으로 찾아 코드가 꽂는다.
        ///   인스펙터에서 이미 연결돼 있으면 <b>건드리지 않는다</b>(사람이 맞춘 값이 우선).
        /// </summary>
        void BindScrollRect()
        {
            var scroll = transform.Find("List/ScrollView")?.GetComponent<ScrollRect>();
            if (scroll == null) return;

            if (scroll.content == null) scroll.content = _list as RectTransform;
            if (scroll.viewport == null)
                scroll.viewport = transform.Find("List/ScrollView/Viewport") as RectTransform;

            if (scroll.verticalScrollbar == null)
            {
                var bar = transform.Find("List/Scrollbar")?.GetComponent<Scrollbar>();
                if (bar != null)
                {
                    scroll.verticalScrollbar = bar;

                    if (bar.handleRect == null)
                        bar.handleRect = transform.Find("List/Scrollbar/Handle") as RectTransform;
                    if (bar.targetGraphic == null)
                        bar.targetGraphic = transform.Find("List/Scrollbar/Handle")?.GetComponent<Image>();
                }
            }
        }

        static TMP_Text FindText(Transform parent, string path)
        {
            Transform t = parent.Find(path);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }
    }
}
