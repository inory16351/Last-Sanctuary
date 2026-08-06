using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Combat;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 전술 지침 창. 지금 선택된 캐릭터의 <see cref="CharacterTactics"/> 를 읽어 보여주고,
    /// 버튼을 누르면 그 자리에서 지침을 바꿔 AI 에 즉시 반영한다.
    ///
    /// <b>선택은 이 창이 하지 않는다 (유저 확정)</b> — 캐릭터를 고르는 곳은 오직
    /// <see cref="UnitSelector"/>(월드 클릭)와 로스터(<see cref="CharacterRosterPanel"/>) 두 곳이다.
    /// 이 창은 <see cref="UnitSelector.OnSelectionChanged"/> 를 구독해 <b>선택을 따라가기만</b> 한다.
    /// 그래서 누르는 순서가 어느 쪽이든 결과가 같다:
    ///   · 로스터에서 캐릭터를 고른 뒤 이 창을 열면 → 그 캐릭터의 지침이 이미 떠 있다
    ///   · 이 창을 먼저 열어두고 로스터에서 캐릭터를 고르면 → 창의 내용이 실시간으로 바뀐다
    ///
    /// <b>창이 로스터를 가리지 않는다</b> — 화면 왼쪽(로스터)과 오른쪽(에너지·액션·미니맵)을
    /// 비워둔 가운데 영역에 배치하고, <b>전체 화면을 덮는 반투명 배경(모달)을 두지 않는다.</b>
    /// 모달을 깔면 그 <c>Image</c> 가 레이캐스트를 먹어 로스터 클릭이 막히기 때문이다 —
    /// 이 창이 열려 있는 동안에도 로스터로 캐릭터를 계속 바꿀 수 있어야 한다는 요구가 우선이다.
    ///
    /// <b>저장/취소가 없다</b> — 목업에는 저장·취소 버튼이 있지만, "실시간으로 반영"이 이 창의
    /// 요구사항이라 누르는 즉시 적용한다. 대신 "초기화"(기본 지침으로 되돌리기)만 남겼다.
    ///
    /// 하이라키는 MCP 로 직접 만들고(준수사항 §10 H-1), 스크립트는 <b>경로로 찾아서</b> 연결한다
    /// — MCP 로는 인스펙터의 오브젝트 참조를 채울 수 없기 때문이다(진행상황 8절 4번).
    /// </summary>
    public class TacticalOrderPanel : MonoBehaviour
    {
        [Header("갱신")]
        [Tooltip("체력 % 등 값이 계속 변하는 표시를 다시 읽는 주기(초)")]
        [Min(0f)] [SerializeField] float refreshInterval = 0.15f;

        [Header("후퇴 기준 조절")]
        [Tooltip("+ / - 버튼 한 번에 움직이는 폭(%)")]
        [Range(1, 25)] [SerializeField] int retreatStep = 5;

        [Header("색")]
        [SerializeField] Color optionNormal = new Color(0.13f, 0.17f, 0.22f, 0.95f);
        [SerializeField] Color optionSelected = new Color(0.16f, 0.42f, 0.38f, 0.98f);
        [SerializeField] Color optionDisabled = new Color(0.10f, 0.11f, 0.13f, 0.85f);

        [Header("문구")]
        [SerializeField] string noSelectionName = "선택된 캐릭터 없음";
        [SerializeField] string noSelectionHint = "로스터에서 캐릭터를 선택하세요.";
        [SerializeField] string selectionHint = "로스터에서 다른 캐릭터를 고르면 즉시 전환됩니다.";

        /// <summary>다른 UI(액션 버튼)가 열고 닫을 수 있게 하나만 둔다.</summary>
        public static TacticalOrderPanel Instance { get; private set; }

        /// <summary>
        /// 옵션 버튼 하나. 배경색으로 선택 여부를 보여주므로 <c>Button</c> 과 <c>Image</c> 를 같이 든다.
        /// </summary>
        class Option
        {
            public Button Button;
            public Image Background;
            public System.Action OnPick;
            public System.Func<bool> IsOn;
        }

        readonly List<Option> _options = new List<Option>();

        // 값이 계속 바뀌는 표시들
        TMP_Text _nameText, _levelText, _hpPercentText, _hintText, _summaryText, _retreatValueText;
        Image _hpFill, _retreatBarFill, _hpGhost;

        /// <summary>깎인 구간을 잠깐 남겨두는 잔상 값. <see cref="HpGhostBar"/> 가 관리한다.</summary>
        readonly HpGhostBar _ghost = new HpGhostBar();

        UnitSelector _selector;
        CharacterUnit _unit;
        CharacterTactics _tactics;
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
            // 선택기가 나중에 살아나는 경우(스크립트 실행 순서)를 위해 계속 확인한다.
            if (_selector == null)
            {
                _selector = UnitSelector.Instance;
                if (_selector != null)
                {
                    _selector.OnSelectionChanged += HandleSelectionChanged;
                    RebindToSelection();
                }
            }

            // 선택된 캐릭터가 죽어 파괴됐으면 놓는다.
            if (_unit != null && !_unit.IsAlive) Bind(null);

            // 잔상은 갱신 주기와 무관하게 매 프레임 진행해야 부드럽다.
            if (_hpGhost != null && _ghost.Tick(_unit != null ? _unit.HpRatio : 0f,
                                                Time.unscaledDeltaTime))
                _hpGhost.fillAmount = _ghost.Value;

            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;

            RefreshLiveValues();
        }

        // ------------------------------------------------------------------
        // 열고 닫기 — HUD_Actions 의 "전술 지침" 버튼이 부른다
        // ------------------------------------------------------------------

        public bool IsOpen => gameObject.activeSelf;

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            // 캐릭터 성장 창과 동시에 열리면 화면 같은 자리에 겹친다(둘 다 HUD_Tactics 와 같은
            // 위치·크기) — 유저 확정: 이 창을 열면 캐릭터 성장 창은 자동으로 닫힌다.
            if (open) CharacterGrowthPanel.Instance?.Close();

            gameObject.SetActive(open);
            if (open) RebindToSelection();   // 열릴 때 지금 선택을 즉시 반영
        }

        public void Close() => SetOpen(false);

        // ------------------------------------------------------------------
        // 선택 연동
        // ------------------------------------------------------------------

        void Subscribe()
        {
            if (_selector == null) _selector = UnitSelector.Instance;
            if (_selector != null) _selector.OnSelectionChanged += HandleSelectionChanged;
            CharacterTactics.OnAnyOrderChanged += HandleOrderChanged;
        }

        void Unsubscribe()
        {
            if (_selector != null) _selector.OnSelectionChanged -= HandleSelectionChanged;
            CharacterTactics.OnAnyOrderChanged -= HandleOrderChanged;
        }

        void HandleSelectionChanged(CharacterUnit unit) => Bind(unit);

        /// <summary>다른 경로(인스펙터 직접 수정 등)로 지침이 바뀌어도 화면이 따라가게 한다.</summary>
        void HandleOrderChanged(CharacterTactics tactics)
        {
            if (tactics != null && ReferenceEquals(tactics, _tactics)) RefreshAll();
        }

        void RebindToSelection()
        {
            if (_selector == null) _selector = UnitSelector.Instance;
            Bind(_selector != null ? _selector.Selected : null);
        }

        void Bind(CharacterUnit unit)
        {
            _unit = unit != null && unit.IsAlive ? unit : null;
            _tactics = _unit != null ? _unit.GetComponent<CharacterTactics>() : null;

            // 다른 캐릭터로 바뀌었으면 잔상은 애니메이션 없이 바로 맞춘다 —
            // 안 그러면 새로 고른 캐릭터의 체력이 이전 캐릭터 값에서 줄어드는 것처럼 보인다.
            _ghost.Snap(_unit != null ? _unit.HpRatio : 0f);
            if (_hpGhost != null) _hpGhost.fillAmount = _ghost.Value;

            if (_unit != null && _tactics == null)
                Debug.LogWarning($"[Tactics] {_unit.name} 에 CharacterTactics 가 없습니다. " +
                                 "Character_Template 에 컴포넌트를 붙여야 지침을 바꿀 수 있습니다.", _unit);

            RefreshAll();
        }

        // ------------------------------------------------------------------
        // 표시 갱신
        // ------------------------------------------------------------------

        void RefreshAll()
        {
            bool has = _tactics != null;

            for (int i = 0; i < _options.Count; i++)
            {
                Option option = _options[i];
                if (option.Button != null) option.Button.interactable = has;
                if (option.Background == null) continue;

                option.Background.color = !has
                    ? optionDisabled
                    : (option.IsOn() ? optionSelected : optionNormal);
            }

            if (_hintText != null) _hintText.text = has ? selectionHint : noSelectionHint;
            if (_summaryText != null)
                _summaryText.text = has ? _tactics.Order.Summarize() : "-";

            RefreshRetreat();
            RefreshLiveValues();
        }

        void RefreshRetreat()
        {
            int percent = _tactics != null ? _tactics.Order.retreatHpPercent : 0;

            if (_retreatValueText != null)
                _retreatValueText.text = _tactics != null
                    ? (percent > 0 ? $"{percent}%" : "사용 안 함")
                    : "-";

            if (_retreatBarFill != null) _retreatBarFill.fillAmount = percent / 100f;
        }

        /// <summary>이름 · 강화 횟수(LV) · 현재 체력 % — 유저가 요청한 세 가지만 보여준다.</summary>
        void RefreshLiveValues()
        {
            if (_unit == null)
            {
                if (_nameText != null) _nameText.text = noSelectionName;
                if (_levelText != null) _levelText.text = "LV.-";
                if (_hpPercentText != null) _hpPercentText.text = "-";
                if (_hpFill != null) _hpFill.fillAmount = 0f;
                if (_hpGhost != null) { _ghost.Snap(0f); _hpGhost.fillAmount = 0f; }
                return;
            }

            if (_nameText != null) _nameText.text = _unit.name;

            // "강화 횟수(LV)" — CharacterUpgradeService 가 올리는 그 횟수를 그대로 쓴다.
            if (_levelText != null) _levelText.text = $"LV.{_unit.UpgradeCount}";

            float ratio = _unit.HpRatio;
            if (_hpPercentText != null) _hpPercentText.text = $"{Mathf.RoundToInt(ratio * 100f)}%";
            if (_hpFill != null)
            {
                // 본 막대는 즉시 반영한다 — 깎이는 걸 보여주는 건 잔상 막대의 몫이다.
                _hpFill.fillAmount = ratio;
                _hpFill.color = HpGaugeColor(ratio);
            }
            _ghost.SetActual(ratio);
        }

        /// <summary>로스터와 같은 3단 그라디언트(초록 → 노랑 → 빨강)를 쓴다.</summary>
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
            _levelText = FindText("Info/Level");
            _hpPercentText = FindText("Info/HpPercent");
            _hintText = FindText("Info/Hint");
            _hpFill = FindImage("Info/HpBack/HpFill");

            _summaryText = FindText("Col3/Summary/Text");
            _retreatValueText = FindText("Col2/RetreatValue");
            _retreatBarFill = FindImage("Col2/RetreatBar/Fill");
            _hpGhost = FindImage("Info/HpBack/HpGhost");

            // 후퇴 기준 막대 — 눌러서/끌어서 1% 단위로 고른다. 아래 ± 버튼(5% 단위)은 그대로 둔다:
            // 막대는 대충 잡을 때, 버튼은 정확히 맞출 때 쓰라는 것.
            var dragBar = transform.Find("Col2/RetreatBar")?.GetComponent<UiDragBar>();
            if (dragBar != null)
                dragBar.OnValueChanged += ratio =>
                    Set(t => t.SetRetreatHpPercent(Mathf.RoundToInt(ratio * 100f)));

            // 공격 유형
            AddOption("Col1/Type/Melee",  () => Set(t => t.SetAttackType(TacticalAttackType.Melee)),
                      () => _tactics.Order.attackType == TacticalAttackType.Melee);
            AddOption("Col1/Type/Ranged", () => Set(t => t.SetAttackType(TacticalAttackType.Ranged)),
                      () => _tactics.Order.attackType == TacticalAttackType.Ranged);
            AddOption("Col1/Type/Magic",  () => Set(t => t.SetAttackType(TacticalAttackType.Magic)),
                      () => _tactics.Order.attackType == TacticalAttackType.Magic);
            AddOption("Col1/Type/Heal",   () => Set(t => t.SetAttackType(TacticalAttackType.Heal)),
                      () => _tactics.Order.attackType == TacticalAttackType.Heal);

            // 포지션
            AddOption("Col1/Pos/Front", () => Set(t => t.SetPosition(TacticalPosition.Front)),
                      () => _tactics.Order.position == TacticalPosition.Front);
            AddOption("Col1/Pos/Mid",   () => Set(t => t.SetPosition(TacticalPosition.Mid)),
                      () => _tactics.Order.position == TacticalPosition.Mid);
            AddOption("Col1/Pos/Back",  () => Set(t => t.SetPosition(TacticalPosition.Back)),
                      () => _tactics.Order.position == TacticalPosition.Back);

            // 공격 반응
            AddOption("Col1/React/Chase", () => Set(t => t.SetAttackReaction(TacticalAttackReaction.Chase)),
                      () => _tactics.Order.attackReaction == TacticalAttackReaction.Chase);
            AddOption("Col1/React/Hold",  () => Set(t => t.SetAttackReaction(TacticalAttackReaction.HoldGround)),
                      () => _tactics.Order.attackReaction == TacticalAttackReaction.HoldGround);

            // 공격 우선 대상
            AddOption("Col2/Target/Nearest",   () => Set(t => t.SetTargetPriority(TacticalTargetPriority.Nearest)),
                      () => _tactics.Order.targetPriority == TacticalTargetPriority.Nearest);
            AddOption("Col2/Target/Strongest", () => Set(t => t.SetTargetPriority(TacticalTargetPriority.Strongest)),
                      () => _tactics.Order.targetPriority == TacticalTargetPriority.Strongest);
            AddOption("Col2/Target/Farthest",  () => Set(t => t.SetTargetPriority(TacticalTargetPriority.Farthest)),
                      () => _tactics.Order.targetPriority == TacticalTargetPriority.Farthest);
            AddOption("Col2/Target/Weakest",   () => Set(t => t.SetTargetPriority(TacticalTargetPriority.Weakest)),
                      () => _tactics.Order.targetPriority == TacticalTargetPriority.Weakest);

            // 후퇴 기준 — 슬라이더 대신 +/- 버튼. 오브젝트 참조(Slider 의 fillRect/handleRect)를
            // MCP 로 넣을 수 없어서, 참조가 필요 없는 구성으로 바꿨다.
            AddOption("Col2/Retreat/Minus", () => Set(t => t.SetRetreatHpPercent(t.Order.retreatHpPercent - retreatStep)),
                      () => false);
            AddOption("Col2/Retreat/Plus",  () => Set(t => t.SetRetreatHpPercent(t.Order.retreatHpPercent + retreatStep)),
                      () => false);

            // 비전투 우선 행동
            AddOption("Col3/Non/Hunt",    () => Set(t => t.SetNonCombat(TacticalNonCombat.Hunt)),
                      () => _tactics.Order.nonCombat == TacticalNonCombat.Hunt);
            AddOption("Col3/Non/Explore", () => Set(t => t.SetNonCombat(TacticalNonCombat.Explore)),
                      () => _tactics.Order.nonCombat == TacticalNonCombat.Explore);
            AddOption("Col3/Non/Build",   () => Set(t => t.SetNonCombat(TacticalNonCombat.Build)),
                      () => _tactics.Order.nonCombat == TacticalNonCombat.Build);

            // 웨이브 반응
            AddOption("Col3/Wave/Priority", () => Set(t => t.SetWaveReaction(TacticalWaveReaction.FinishCurrent)),
                      () => _tactics.Order.waveReaction == TacticalWaveReaction.FinishCurrent);
            AddOption("Col3/Wave/Defend",   () => Set(t => t.SetWaveReaction(TacticalWaveReaction.DefendNow)),
                      () => _tactics.Order.waveReaction == TacticalWaveReaction.DefendNow);

            // 초기화 / 닫기
            AddOption("Footer/ResetButton", () => Set(t => t.ResetToDefault()), () => false);

            HookClose("Header/CloseButton");
            HookClose("Footer/CloseButton");
        }

        /// <summary>지침을 바꾸는 공통 경로 — 선택이 없으면 아무 일도 하지 않는다.</summary>
        void Set(System.Action<CharacterTactics> change)
        {
            if (_tactics == null) return;
            change(_tactics);
            RefreshAll();
        }

        void AddOption(string path, System.Action onPick, System.Func<bool> isOn)
        {
            Transform node = transform.Find(path);
            if (node == null)
            {
                Debug.LogWarning($"[Tactics] 하이라키에서 '{path}' 를 찾지 못했습니다.", this);
                return;
            }

            var option = new Option
            {
                Button = node.GetComponent<Button>(),
                Background = node.GetComponent<Image>(),
                OnPick = onPick,
                // 선택 대상이 없을 때 IsOn 이 불리면 NullReference 가 나므로 여기서 한 번 막는다.
                IsOn = () => _tactics != null && isOn(),
            };

            if (option.Button != null) option.Button.onClick.AddListener(() => option.OnPick());
            _options.Add(option);
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
