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
    public class CharacterGrowthPanel : MonoBehaviour, IExclusiveHudPanel
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
        [SerializeField] string noteAffordable = "능력치에 무작위 성장치를 더합니다 (저항력 제외).";
        [SerializeField] string noteUnaffordable = "에너지가 부족합니다.";
        [SerializeField] string noteNoSelection = "-";

        [Header("문구 — 패시브 스킬")]
        [Tooltip("미해금 스킬의 이름 자리. 내용을 감춘다")]
        [SerializeField] string lockedTitle = "???";
        [SerializeField] string lockedDesc = "???";
        [Tooltip("{0} = 해금에 필요한 강화 횟수")]
        [SerializeField] string lockedNoteFormat = "강화 {0}회에 해금";
        [SerializeField] string unlockedNote = "해금됨";
        [SerializeField] string passiveClickHint = "클릭 → 상세";
        [SerializeField] string passiveNoneText = "이 캐릭터에는 지정된 스킬이 없습니다.";
        [SerializeField] string passiveNoSelectionText = "캐릭터를 선택하세요.";

        [Header("색")]
        [SerializeField] Color rowActive = new Color(0.13f, 0.17f, 0.22f, 0.95f);
        [SerializeField] Color rowDisabled = new Color(0.09f, 0.10f, 0.12f, 0.75f);
        [SerializeField] Color labelActive = new Color(0.88f, 0.92f, 0.94f, 1f);
        [SerializeField] Color labelDisabled = new Color(0.42f, 0.45f, 0.48f, 1f);
        [SerializeField] Color deltaColor = new Color(0.45f, 0.95f, 0.6f, 1f);
        [SerializeField] Color costColor = new Color(0.98f, 0.85f, 0.45f, 1f);
        [SerializeField] Color costUnaffordableColor = new Color(0.96f, 0.42f, 0.42f, 1f);

        [Header("색 — 패시브 스킬")]
        [SerializeField] Color passiveUnlockedColor = new Color(0.13f, 0.15f, 0.20f, 0.95f);
        [SerializeField] Color passiveLockedColor = new Color(0.07f, 0.08f, 0.10f, 0.85f);
        [SerializeField] Color passiveEmptyColor = new Color(0.06f, 0.07f, 0.08f, 0.6f);
        [Tooltip("미해금 아이콘을 눌러 실루엣으로 만드는 색. 알파는 남기고 밝기만 죽인다")]
        [SerializeField] Color passiveSilhouetteColor = new Color(0.06f, 0.06f, 0.08f, 0.95f);
        [SerializeField] Color passiveDescColor = new Color(0.72f, 0.76f, 0.82f, 1f);
        [SerializeField] Color unlockedNoteColor = new Color(0.45f, 0.85f, 0.6f, 1f);

        /// <summary>다른 UI(액션 버튼)가 열고 닫을 수 있게 하나만 둔다.</summary>
        public static CharacterGrowthPanel Instance { get; private set; }

        /// <summary>능력치 한 칸. <see cref="Type"/> 이 null 이면 아직 구현되지 않은 능력치라 빈 칸으로 둔다.</summary>
        struct StatSlot
        {
            public StatType? Type;
            public string DisplayName;
        }

        /// <summary>
        /// 캐릭터 테이블(first_Stat 시트)의 12능력치. 화면은 3열 × 4행이고 이 배열 순서가 곧 배치 순서다
        /// (같은 계열끼리 한 줄에 오도록 묶었다: 공격 계열 → 방어/회복 계열 → 명중/치명 → 속도/저항).
        ///
        /// <b>시야 · 사거리는 넣지 않는다</b> — 모든 캐릭터가 같은 고정값을 쓰고 패시브 스킬로만
        /// 달라지므로 캐릭터마다 표기할 값이 없다(유저 확정 2026-08-11). 테이블에도 컬럼이 없다.
        ///
        /// 이제 <see cref="StatSlot.Type"/> 이 null 인 칸은 없다 — 15칸 중 11칸이 미구현
        /// 자리표시(ASPD/RNG/CRT…)였던 이전 구조를 12칸 전부 실제 능력치로 교체했다.
        /// null 처리는 코드에 그대로 남겨둔다: 나중에 능력치가 또 늘어날 때 다시 쓴다.
        /// </summary>
        static readonly StatSlot[] Slots =
        {
            new StatSlot { Type = StatType.Hp,           DisplayName = "체력" },
            new StatSlot { Type = StatType.Attack,       DisplayName = "근거리 공격력" },
            new StatSlot { Type = StatType.RangedAttack, DisplayName = "원거리 공격력" },

            new StatSlot { Type = StatType.Magic,        DisplayName = "마법" },
            new StatSlot { Type = StatType.Cure,         DisplayName = "회복력" },
            new StatSlot { Type = StatType.Defense,      DisplayName = "방어력" },

            new StatSlot { Type = StatType.Regen,        DisplayName = "체력 재생" },
            new StatSlot { Type = StatType.Accuracy,     DisplayName = "명중률" },
            new StatSlot { Type = StatType.Critical,     DisplayName = "크리티컬 확률" },

            new StatSlot { Type = StatType.AttackSpeed,  DisplayName = "공격 속도" },
            new StatSlot { Type = StatType.MoveSpeed,    DisplayName = "이동속도" },
            new StatSlot { Type = StatType.Resistance,   DisplayName = "저항력" },
        };

        /// <summary>패시브 스킬 칸 하나. 카드 전체가 버튼이고, 누르면 상세 창이 열린다.</summary>
        class PassiveCard
        {
            public GameObject Root;
            public Image Background;
            public Image Icon;
            public TMP_Text Name;
            public TMP_Text Lock;
            public TMP_Text Desc;
            public TMP_Text Hint;
            public Button Button;
        }

        const int PassiveSlotCount = 3;
        readonly PassiveCard[] _passives = new PassiveCard[PassiveSlotCount];

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
            // 같은 자리에 겹치는 다른 창과 맵 클릭 모드를 전부 닫는다.
            // ⚠ 예전에는 여기서 TacticalOrderPanel 만 닫았다 — 부대 설정은 그대로 열려 있었다
            //   (유저 리포트 2026-08-13). 규칙을 HudExclusive 한 곳으로 모았다.
            if (open) HudExclusive.OpenOnly(this);

            gameObject.SetActive(open);
            if (open) RebindToSelection();

            // 상세 창은 이 창 위에 뜨는 자식 같은 존재라, 부모가 닫히면 같이 닫혀야 한다 —
            // 안 그러면 성장 창을 닫아도 스킬 설명만 화면에 덩그러니 남는다.
            if (!open) SkillDetailPanel.Instance?.Close();
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

            if (_nameText != null) _nameText.text = has ? _unit.DisplayName : noSelectionName;
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
            RefreshPassives(has);
            RefreshFooter(has);
        }

        /// <summary>
        /// 초상화. <b>캐릭터 테이블의 일러스트</b>(<c>Resources/Illust/</c>)를 우선 쓰고,
        /// 정의가 없는 캐릭터(무작위 능력치 캐릭터)만 예전처럼 인게임 스프라이트를 그대로 얹는다.
        ///
        /// 예전에는 항상 <see cref="SpriteRenderer"/> 를 미러링해서 "살아있는 애니메이션"이
        /// 초상화에 나왔는데, 이제 정식 일러스트가 생겼으니 그게 우선이다.
        /// </summary>
        void RefreshPortrait(bool has)
        {
            Sprite sprite = null;

            if (has)
            {
                var def = _unit.Definition;
                if (def != null) sprite = def.Illust;
                if (sprite == null && _unitSprite != null) sprite = _unitSprite.sprite;
            }

            if (_portraitSprite != null)
            {
                _portraitSprite.sprite = sprite;
                // 알파로만 켜고 끈다 — 액자(부모 Portrait)는 항상 보여야 한다.
                _portraitSprite.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            }

            if (_portraitHint != null) _portraitHint.SetActive(sprite == null);
        }

        /// <summary>
        /// 패시브 스킬 3칸.
        ///
        /// <b>미해금은 내용을 절대 보여주지 않는다</b>(유저 확정) — 아이콘은 완전히 어둡게 눌러
        /// 실루엣만 남기고, 이름과 설명은 <c>???</c> 로 가린다. 대신 <b>언제 열리는지</b>
        /// (강화 N회)는 알려줘야 목표가 생기므로 그것만 표시한다.
        /// </summary>
        void RefreshPassives(bool has)
        {
            var def = has ? _unit.Definition : null;

            for (int slot = 0; slot < PassiveSlotCount; slot++)
            {
                PassiveCard card = _passives[slot];
                if (card == null) continue;

                PassiveSkillSO skill = def != null ? def.PassiveAt(slot) : null;
                bool unlocked = skill != null && has && _unit.IsPassiveUnlocked(slot);

                // 선택이 없거나 이 캐릭터에 스킬 자체가 없으면 빈 칸으로 둔다
                if (skill == null)
                {
                    if (card.Background != null) card.Background.color = passiveEmptyColor;
                    if (card.Icon != null) card.Icon.color = new Color(1f, 1f, 1f, 0f);
                    if (card.Name != null) { card.Name.text = "-"; card.Name.color = labelDisabled; }
                    if (card.Lock != null) card.Lock.text = "";
                    if (card.Desc != null) card.Desc.text = has ? passiveNoneText : passiveNoSelectionText;
                    if (card.Hint != null) card.Hint.text = "";
                    if (card.Button != null) card.Button.interactable = false;
                    continue;
                }

                if (card.Background != null)
                    card.Background.color = unlocked ? passiveUnlockedColor : passiveLockedColor;

                if (card.Icon != null)
                {
                    Sprite icon = skill.Icon;
                    card.Icon.sprite = icon;
                    // 실루엣 — 아이콘을 지우지 않고 검게 눌러야 "무언가 있다"는 게 보인다
                    card.Icon.color = icon == null
                        ? new Color(1f, 1f, 1f, 0f)
                        : (unlocked ? Color.white : passiveSilhouetteColor);
                }

                if (card.Name != null)
                {
                    card.Name.text = unlocked ? skill.DisplayName : lockedTitle;
                    card.Name.color = unlocked ? labelActive : labelDisabled;
                }

                if (card.Lock != null)
                {
                    card.Lock.text = unlocked
                        ? unlockedNote
                        : string.Format(lockedNoteFormat, def.UnlockUpgradesFor(slot));
                    card.Lock.color = unlocked ? unlockedNoteColor : labelDisabled;
                }

                if (card.Desc != null)
                {
                    card.Desc.text = unlocked ? skill.FlavorText : lockedDesc;
                    card.Desc.color = unlocked ? passiveDescColor : labelDisabled;
                }

                if (card.Hint != null) card.Hint.text = unlocked ? passiveClickHint : "";

                // 해금된 것만 상세 창을 열 수 있다 — 잠긴 스킬의 내용이 새어나가지 않게
                if (card.Button != null) card.Button.interactable = unlocked;
            }
        }

        /// <summary>패시브 칸을 눌렀을 때 — 상세 효과 창을 연다.</summary>
        void HandlePassiveClicked(int slot)
        {
            if (_unit == null) return;
            var def = _unit.Definition;
            if (def == null) return;

            PassiveSkillSO skill = def.PassiveAt(slot);
            if (skill == null) return;

            SkillDetailPanel.Instance?.Open(skill, _unit, slot, _unit.IsPassiveUnlocked(slot));
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

        /// <summary>
        /// 강화로 더 올릴 것이 남아 있는가. <b>성장하는 능력치만</b> 본다 —
        /// 저항력은 애초에 강화 대상이 아니라서(고정값) 여기에 넣으면
        /// 저항력이 낮은 캐릭터는 영원히 "상한 도달"이 안 뜬다.
        /// </summary>
        static bool AreStatsAtCap(StatBlock stats, int cap)
        {
            for (int i = 0; i < (int)StatType.COUNT; i++)
            {
                var t = (StatType)i;
                if (!StatBlock.IsGrowable(t)) continue;
                if (stats[t] < cap) return false;
            }
            return true;
        }

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

            // 스프라이트가 비어 있으면 fillAmount 가 무시되어 막대 길이가 안 변한다 —
            // UiFillBar 문서 참조.
            UiFillBar.Prepare(_hpFill, _hpGhost);
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

            // 패시브 카드 3장. 클로저가 슬롯 번호를 잡도록 지역 변수에 복사해서 넘긴다 —
            // 반복 변수를 그대로 캡처하면 세 버튼이 전부 마지막 값을 쓴다(고전적 실수).
            for (int i = 0; i < PassiveSlotCount; i++)
            {
                string path = $"Stats/PassiveGrid/PassiveCard_{i:00}";
                Transform node = transform.Find(path);
                if (node == null)
                {
                    Debug.LogWarning($"[Growth] 하이라키에서 '{path}' 를 찾지 못했습니다.", this);
                    continue;
                }

                var card = new PassiveCard
                {
                    Root = node.gameObject,
                    Background = node.GetComponent<Image>(),
                    Icon = FindImage($"{path}/Icon"),
                    Name = FindText($"{path}/Name"),
                    Lock = FindText($"{path}/Lock"),
                    Desc = FindText($"{path}/Desc"),
                    Hint = FindText($"{path}/Hint"),
                    Button = node.GetComponent<Button>(),
                };
                _passives[i] = card;

                if (card.Button != null)
                {
                    int slot = i;
                    card.Button.onClick.RemoveAllListeners();
                    card.Button.onClick.AddListener(() => HandlePassiveClicked(slot));
                }
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
