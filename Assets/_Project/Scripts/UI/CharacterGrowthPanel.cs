using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Combat;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 캐릭터 성장(강화) 창. <see cref="TacticalOrderPanel"/> 과 완전히 같은 로직으로 만들었다
    /// (유저 확정) — 이 창도 캐릭터를 선택하지 않고 <see cref="UnitSelector.OnSelectionChanged"/> 를
    /// 따라가기만 하고, 전체 화면 모달을 두지 않아 로스터(<see cref="CharacterRosterPanel"/>)를
    /// 계속 클릭할 수 있다. 자세한 설계 이유는 <see cref="TacticalOrderPanel"/> 클래스 문서 참조.
    ///
    /// <b>능력치 표시 규칙(유저 확정)</b>:
    ///   · 능력치 표(<see cref="Slots"/>)의 체력 칸도 다른 능력치와 똑같이 <see cref="StatBlock"/>
    ///     원시값(1~100, <see cref="BalanceConfigSO"/> 치환 공식을 거치기 <b>전</b>)을 보여준다 —
    ///     특별 취급하지 않는다.
    ///   · "지금 얼마나 위험한지"(현재/최대 %)는 표 위 <b>초상화 아래 체력바</b>(<see cref="_hpFill"/>·
    ///     <see cref="_hpPercentText"/>)가 따로 보여준다 — 전술 창의 Info 구성과 같다.
    ///   · 프로토타입은 <see cref="StatType"/> 4종만 구현돼 있다. 정식 기획(14~15종) 중 나머지는
    ///     이름을 지어내지 않고(참고 자료 원문이 인코딩 손상으로 복구 불가) <see cref="Slots"/> 에
    ///     <c>Type = null</c> 인 빈 칸으로 남겨 회색으로 비활성 표시한다 — 자리만 잡아두는 것.
    ///
    /// <b>증가치(+N) 표시 규칙(유저 확정)</b>: 강화 1회의 결과만 보여준다. 다음 강화를 누르면
    /// 이전 증가치는 지우고 이번 결과로 덮어쓴다(누적 아님) — <see cref="_delta"/> 를 매 강화마다
    /// <c>Clear()</c> 한 뒤 새로 채우는 것으로 구현했다.
    ///
    /// <b>초상화</b>: 선택된 캐릭터의 <see cref="SpriteRenderer"/> 가 지금 들고 있는 스프라이트를
    /// 그대로 <c>Info/Portrait</c> 의 <see cref="Image"/> 에 얹는다(<see cref="CharacterAnimator"/> 가
    /// 매 프레임 갈아끼우는 그 스프라이트라 애니메이션도 그대로 따라온다). 선택이 없으면 원래
    /// 플레이스홀더 문구("캐릭터 일러스트 (추후 연동)")로 되돌린다.
    /// </summary>
    public class CharacterGrowthPanel : MonoBehaviour
    {
        [Header("갱신")]
        [Tooltip("체력 % 등 값이 계속 변하는 표시를 다시 읽는 주기(초)")]
        [Min(0f)] [SerializeField] float refreshInterval = 0.15f;

        [Header("문구")]
        [SerializeField] string noSelectionName = "선택된 캐릭터 없음";
        [SerializeField] string noSelectionHint = "로스터에서 캐릭터를 선택하세요.";
        [SerializeField] string selectionHint = "로스터에서 다른 캐릭터를 고르면 즉시 전환됩니다.";
        [SerializeField] string enhanceFormat = "강화하기 ({0})";
        [SerializeField] string enhanceNoSelection = "강화하기";
        [SerializeField] string enhanceMaxed = "능력치 상한";
        [SerializeField] string noteAffordable = "선택한 캐릭터의 능력치 4종에 무작위 성장치를 더합니다.";
        [SerializeField] string noteUnaffordable = "에너지가 부족합니다.";
        [SerializeField] string noteNoSelection = "-";

        [Header("색")]
        [SerializeField] Color rowActive = new Color(0.13f, 0.17f, 0.22f, 0.95f);
        [SerializeField] Color rowDisabled = new Color(0.09f, 0.10f, 0.12f, 0.75f);
        [SerializeField] Color labelActive = new Color(0.88f, 0.92f, 0.94f, 1f);
        [SerializeField] Color labelDisabled = new Color(0.42f, 0.45f, 0.48f, 1f);
        [SerializeField] Color deltaColor = new Color(0.45f, 0.95f, 0.6f, 1f);
        [SerializeField] Color costColor = new Color(0.98f, 0.85f, 0.45f, 1f);
        [SerializeField] Color costUnaffordableColor = new Color(0.96f, 0.42f, 0.42f, 1f);

        /// <summary>다른 UI(액션 버튼)가 열고 닫을 수 있게 하나만 둔다.</summary>
        public static CharacterGrowthPanel Instance { get; private set; }

        /// <summary>능력치 한 칸. <see cref="Type"/> 이 null 이면 아직 구현되지 않은 능력치라 빈 칸으로 둔다.</summary>
        struct StatSlot
        {
            public StatType? Type;
            public string DisplayName;
        }

        /// <summary>
        /// 구현된 4종을 앞에, 나머지는 정식 기획 참고 자료(Character Enhance UI.html)의 abbr 코드만
        /// 자리 표시로 둔다 — 원문 한글 라벨은 그 파일이 인코딩 손상으로 복구 불가능해서 지어내지 않았다.
        /// </summary>
        static readonly StatSlot[] Slots =
        {
            new StatSlot { Type = StatType.Hp,      DisplayName = "체력" },
            new StatSlot { Type = StatType.Attack,  DisplayName = "공격력" },
            new StatSlot { Type = StatType.Defense, DisplayName = "방어력" },
            new StatSlot { Type = StatType.Regen,   DisplayName = "체력회복력" },
            new StatSlot { Type = null, DisplayName = "ASPD" },
            new StatSlot { Type = null, DisplayName = "RNG" },
            new StatSlot { Type = null, DisplayName = "CRT" },
            new StatSlot { Type = null, DisplayName = "CDM" },
            new StatSlot { Type = null, DisplayName = "RES" },
            new StatSlot { Type = null, DisplayName = "EVA" },
            new StatSlot { Type = null, DisplayName = "ACC" },
            new StatSlot { Type = null, DisplayName = "PEN" },
            new StatSlot { Type = null, DisplayName = "MSPD" },
            new StatSlot { Type = null, DisplayName = "SGT" },
            new StatSlot { Type = null, DisplayName = "GAIN" },
        };

        class Row
        {
            public Image Background;
            public TMP_Text Label;
            public TMP_Text Value;
            public TMP_Text Delta;
        }

        readonly Row[] _rows = new Row[Slots.Length];

        // 값이 계속 바뀌는 표시들
        TMP_Text _nameText, _countText, _hpPercentText, _hintText, _noteText, _enhanceLabel;
        Image _hpFill, _hpGhost;
        Button _enhanceButton;

        // 초상화 — 액자(Portrait)는 항상 보이고, 그 안의 Sprite 레이어에만 캐릭터 그림을 얹는다.
        // 액자 자체에 sprite 를 넣으면 선택이 없을 때 액자까지 사라져 패널이 비어 보인다.
        Image _portraitSprite;
        GameObject _portraitHint;
        SpriteRenderer _unitSprite;

        /// <summary>강화 비용 표시(유저 확정: "강화 시 소모되는 자원"을 명시).</summary>
        TMP_Text _costText;

        readonly HpGhostBar _ghost = new HpGhostBar();

        /// <summary>침식 게이지 — 체력바가 보이는 곳엔 침식도 같이 보여야 한다(유저 확정).</summary>
        readonly ErosionGaugeView _erosion = new ErosionGaugeView();

        /// <summary>이번 강화 1회로 오른 만큼만 들고 있다 — 다음 강화를 누르면 지우고 새로 채운다.</summary>
        readonly Dictionary<StatType, int> _delta = new Dictionary<StatType, int>();

        UnitSelector _selector;
        CharacterUpgradeService _upgrades;
        CharacterUnit _unit;
        float _nextRefresh;

        void Awake()
        {
            Instance = this;
            BuildBindings();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unsubscribe();
        }

        void OnEnable()
        {
            Subscribe();
            RebindToSelection();
        }

        void OnDisable() => Unsubscribe();

        void Update()
        {
            // 선택기·강화 서비스가 나중에 살아나는 경우(스크립트 실행 순서)를 위해 계속 확인한다.
            if (_selector == null)
            {
                _selector = UnitSelector.Instance;
                if (_selector != null)
                {
                    _selector.OnSelectionChanged += HandleSelectionChanged;
                    RebindToSelection();
                }
            }
            if (_upgrades == null) _upgrades = CharacterUpgradeService.Instance;

            if (_unit != null && !_unit.IsAlive) Bind(null);

            // 잔상은 갱신 주기와 무관하게 매 프레임 진행해야 부드럽다.
            if (_hpGhost != null && _ghost.Tick(_unit != null ? _unit.HpRatio : 0f,
                                                Time.unscaledDeltaTime))
                _hpGhost.fillAmount = _ghost.Value;

            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;

            RefreshAll();
        }

        // ------------------------------------------------------------------
        // 열고 닫기 — HUD_Actions 의 "캐릭터 성장" 버튼이 부른다
        // ------------------------------------------------------------------

        public bool IsOpen => gameObject.activeSelf;

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            // 전술 지침 창과 동시에 열리면 화면 같은 자리에 겹친다(둘 다 HUD_Tactics 와 같은
            // 위치·크기) — 유저 확정: 이 창을 열면 전술 지침 창은 자동으로 닫힌다.
            if (open) TacticalOrderPanel.Instance?.Close();

            gameObject.SetActive(open);
            if (open) RebindToSelection();
        }

        public void Close() => SetOpen(false);

        // ------------------------------------------------------------------
        // 선택 연동
        // ------------------------------------------------------------------

        void Subscribe()
        {
            if (_selector == null) _selector = UnitSelector.Instance;
            if (_selector != null) _selector.OnSelectionChanged += HandleSelectionChanged;
            if (_upgrades == null) _upgrades = CharacterUpgradeService.Instance;
        }

        void Unsubscribe()
        {
            if (_selector != null) _selector.OnSelectionChanged -= HandleSelectionChanged;
        }

        void HandleSelectionChanged(CharacterUnit unit) => Bind(unit);

        void RebindToSelection()
        {
            if (_selector == null) _selector = UnitSelector.Instance;
            Bind(_selector != null ? _selector.Selected : null);
        }

        void Bind(CharacterUnit unit)
        {
            _unit = unit != null && unit.IsAlive ? unit : null;
            _unitSprite = _unit != null ? _unit.GetComponent<SpriteRenderer>() : null;

            // 다른 캐릭터로 바뀌면 잔상은 애니메이션 없이 바로 맞추고, 지난 강화의 증가치 표시도
            // 지운다 — 안 그러면 새로 고른 캐릭터에 이전 캐릭터의 "+N" 이 그대로 남는다.
            _ghost.Snap(_unit != null ? _unit.HpRatio : 0f);
            if (_hpGhost != null) _hpGhost.fillAmount = _ghost.Value;
            _delta.Clear();

            RefreshAll();
        }

        // ------------------------------------------------------------------
        // 강화
        // ------------------------------------------------------------------

        void HandleEnhance()
        {
            if (_unit == null) return;
            if (_upgrades == null) _upgrades = CharacterUpgradeService.Instance;
            if (_upgrades == null) return;

            StatBlock before = _unit.Stats;
            if (!_upgrades.TryUpgrade(_unit)) { RefreshAll(); return; }
            StatBlock after = _unit.Stats;

            // 누적하지 않는다 — 이번 강화에서 오른 것만 남긴다(유저 확정).
            _delta.Clear();
            for (int i = 0; i < (int)StatType.COUNT; i++)
            {
                var t = (StatType)i;
                int d = after[t] - before[t];
                if (d > 0) _delta[t] = d;
            }

            RefreshAll();
        }

        // ------------------------------------------------------------------
        // 표시 갱신
        // ------------------------------------------------------------------

        void RefreshAll()
        {
            bool has = _unit != null;

            if (_nameText != null) _nameText.text = has ? _unit.name : noSelectionName;
            if (_countText != null) _countText.text = has ? $"강화 {_unit.UpgradeCount}회" : "-";
            if (_hintText != null) _hintText.text = has ? selectionHint : noSelectionHint;

            float ratio = has ? _unit.HpRatio : 0f;
            if (_hpPercentText != null) _hpPercentText.text = has ? $"{Mathf.RoundToInt(ratio * 100f)}%" : "-";
            if (_hpFill != null)
            {
                _hpFill.fillAmount = ratio;
                _hpFill.color = HpGaugeColor(ratio);
            }
            _ghost.SetActual(ratio);
            _erosion.Refresh(has ? _unit : null);

            RefreshPortrait(has);
            RefreshRows(has);
            RefreshFooter(has);
        }

        /// <summary>선택된 캐릭터가 지금 들고 있는 스프라이트를 그대로 얹는다(연출 없음, 매 갱신마다 동기화).</summary>
        void RefreshPortrait(bool has)
        {
            Sprite sprite = has && _unitSprite != null ? _unitSprite.sprite : null;

            if (_portraitSprite != null)
            {
                _portraitSprite.sprite = sprite;
                // 알파로만 켜고 끈다 — 액자(부모 Portrait)는 항상 보여야 한다.
                _portraitSprite.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            }

            if (_portraitHint != null) _portraitHint.SetActive(sprite == null);
        }

        void RefreshRows(bool has)
        {
            StatBlock stats = has ? _unit.Stats : default;

            for (int i = 0; i < Slots.Length; i++)
            {
                Row row = _rows[i];
                if (row == null) continue;

                StatSlot slot = Slots[i];
                bool implemented = slot.Type.HasValue;
                bool active = implemented && has;

                if (row.Label != null)
                    row.Label.color = implemented ? labelActive : labelDisabled;
                if (row.Background != null)
                    row.Background.color = active ? rowActive : rowDisabled;

                if (row.Value != null)
                    row.Value.text = active ? stats[slot.Type.Value].ToString() : "-";

                if (row.Delta != null)
                {
                    int d = active && _delta.TryGetValue(slot.Type.Value, out int found) ? found : 0;
                    row.Delta.text = d > 0 ? $"+{d}" : "";
                }
            }
        }

        void RefreshFooter(bool has)
        {
            if (_upgrades == null) _upgrades = CharacterUpgradeService.Instance;

            bool statCapped = has && _unit.Balance != null &&
                              AreStatsAtCap(_unit.Stats, _unit.Balance.statMax);
            bool canUpgrade = has && _upgrades != null && _upgrades.CanUpgrade(_unit) && !statCapped;
            int cost = has && _upgrades != null ? _upgrades.CostFor(_unit) : 0;

            if (_enhanceButton != null) _enhanceButton.interactable = canUpgrade;

            // 소모 자원은 버튼 라벨과 별도로 한 줄 명시한다(유저 확정) — 에너지가 부족하면
            // 붉게 표시해 "왜 버튼이 안 눌리는지"가 바로 보이게 한다.
            if (_costText != null)
            {
                _costText.text = has ? $"에너지 {cost}" : "-";
                _costText.color = !has || canUpgrade ? costColor : costUnaffordableColor;
            }

            if (_enhanceLabel != null)
            {
                if (!has) _enhanceLabel.text = enhanceNoSelection;
                else if (statCapped) _enhanceLabel.text = enhanceMaxed;
                else _enhanceLabel.text = string.Format(enhanceFormat,
                                                         _upgrades != null ? _upgrades.CostFor(_unit) : 0);
            }

            if (_noteText != null)
            {
                if (!has) _noteText.text = noteNoSelection;
                else if (statCapped) _noteText.text = enhanceMaxed;
                else _noteText.text = canUpgrade ? noteAffordable : noteUnaffordable;
            }
        }

        static bool AreStatsAtCap(StatBlock stats, int cap) =>
            stats.hp >= cap && stats.attack >= cap && stats.defense >= cap && stats.regen >= cap;

        /// <summary>로스터·전술 창과 같은 3단 그라디언트(초록 → 노랑 → 빨강).</summary>
        static Color HpGaugeColor(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            const float mid = 0.35f;
            return ratio >= mid
                ? Color.Lerp(HudTheme.BarHpMid, HudTheme.BarHp, (ratio - mid) / (1f - mid))
                : Color.Lerp(HudTheme.BarHpLow, HudTheme.BarHpMid, ratio / mid);
        }

        // ------------------------------------------------------------------
        // 하이라키 연결 — 경로로 찾는다 (MCP 로는 인스펙터 참조를 못 넣는다)
        // ------------------------------------------------------------------

        void BuildBindings()
        {
            _nameText = FindText("Info/Name");
            _countText = FindText("Info/Level");
            _hpPercentText = FindText("Info/HpPercent");
            _hintText = FindText("Info/Hint");
            _hpFill = FindImage("Info/HpBack/HpFill");
            _hpGhost = FindImage("Info/HpBack/HpGhost");
            _erosion.Bind(transform, "Info/ErosionBack");
            _portraitSprite = FindImage("Info/Portrait/Sprite");
            _portraitHint = transform.Find("Info/Portrait/Hint")?.gameObject;
            _costText = FindText("Info/CostValue");

            for (int i = 0; i < Slots.Length; i++)
            {
                string path = $"Stats/Grid/StatRow_{i:00}";
                Transform node = transform.Find(path);
                if (node == null)
                {
                    Debug.LogWarning($"[Growth] 하이라키에서 '{path}' 를 찾지 못했습니다.", this);
                    continue;
                }

                _rows[i] = new Row
                {
                    Background = node.GetComponent<Image>(),
                    Label = FindText($"{path}/Label"),
                    Value = FindText($"{path}/Value"),
                    Delta = FindText($"{path}/Delta"),
                };

                if (_rows[i].Label != null) _rows[i].Label.text = Slots[i].DisplayName;
            }

            // 강화 버튼·비용/안내 문구는 초상화 바로 아래(Info 컬럼)에 둔다(유저 확정) —
            // 원래 Footer 에 있었으나, "강화하기가 초상화 밑에 붙어서 현재 가격·강화 단계를
            // 보여줘야 한다"는 요청으로 Info 아래로 옮겼다. Info/Level 이 이미 "강화 N회"(현재
            // 단계)를 보여주고 있고, 이 버튼의 라벨이 "강화하기 (비용)" 을 보여준다.
            _enhanceButton = transform.Find("Info/EnhanceButton")?.GetComponent<Button>();
            _enhanceLabel = FindText("Info/EnhanceButton/Label");
            _noteText = FindText("Info/Note");

            if (_enhanceButton != null) _enhanceButton.onClick.AddListener(HandleEnhance);

            HookClose("Header/CloseButton");
            HookClose("Footer/CloseButton");
        }

        void HookClose(string path)
        {
            var button = transform.Find(path)?.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(Close);
        }

        TMP_Text FindText(string path)
        {
            Transform node = transform.Find(path);
            return node != null ? node.GetComponent<TMP_Text>() : null;
        }

        Image FindImage(string path)
        {
            Transform node = transform.Find(path);
            return node != null ? node.GetComponent<Image>() : null;
        }
    }
}
