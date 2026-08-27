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
    public class TacticalOrderPanel : MonoBehaviour, IExclusiveHudPanel
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

            /// <summary>지금 고를 수 있는지. null 이면 항상 가능.</summary>
            public System.Func<bool> IsAvailable;
        }

        readonly List<Option> _options = new List<Option>();

        // 값이 계속 바뀌는 표시들
        TMP_Text _nameText, _levelText, _hpPercentText, _hintText, _summaryText, _retreatValueText;

        /// <summary>탐험 배회 범위 버튼 아래 한 줄 설명. 버튼을 누를 때마다 그 범위의 설명으로 바뀐다.</summary>
        TMP_Text _roamHintText;
        Image _hpFill, _hpGhost;

        /// <summary>후퇴 기준을 끌어서 고르는 슬라이더(0~100%). 배선은 <see cref="SliderWiring"/> 이 한다.</summary>
        Slider _retreatSlider;

        // 초상화 — 캐릭터 성장 창(CharacterGrowthPanel)과 완전히 같은 구조·같은 규칙이다.
        // 액자(Portrait)는 항상 보이고, 그 안의 Sprite 레이어에만 그림을 얹는다.
        [Header("일러스트 채우기 (2026-08-17)")]
        [Tooltip("그림이 세로로 잘릴 때 남길 위치 (0=아래 · 0.5=가운데 · 1=위). " +
                 "인물화는 위쪽을 남겨야 <b>얼굴</b>이 들어온다. PortraitFit 주석 참조")]
        [Range(0f, 1f)] [SerializeField] float portraitVerticalAnchor = 0.86f;

        Image _portraitSprite;
        GameObject _portraitHint;
        SpriteRenderer _unitSprite;

        /// <summary>깎인 구간을 잠깐 남겨두는 잔상 값. <see cref="HpGhostBar"/> 가 관리한다.</summary>
        readonly HpGhostBar _ghost = new HpGhostBar();

        /// <summary>침식 게이지 — 체력바가 보이는 곳엔 침식도 같이 보여야 한다(유저 확정).</summary>
        readonly ErosionGaugeView _erosion = new ErosionGaugeView();

        UnitSelector _selector;
        CharacterUnit _unit;
        CharacterTactics _tactics;
        float _nextRefresh;

        void Awake()
        {
            LocalizeLabels();
            // ★★★ 2026-08-27 — 언어가 바뀌면 다시 그린다(창 열넷에 한꺼번에 이은 것 중 하나).
            Data.StringTable.OnLanguageChanged -= HandleLanguageChanged;
            Data.StringTable.OnLanguageChanged += HandleLanguageChanged;
            Instance = this;
            BuildBindings();
        }

        void OnDestroy()
        {
            // ⚠ 정적 이벤트라 끊지 않으면 죽은 오브젝트가 구독에 남는다(SettingsPanel 의 그 ⚠).
            Data.StringTable.OnLanguageChanged -= HandleLanguageChanged;
            if (Instance == this) Instance = null;
            Unsubscribe();
        }

        /// <summary>
        /// 언어가 바뀌면 문구를 다시 받아 오고, <b>열려 있으면</b> 그 자리에서 다시 칠한다.
        /// ⚠ 닫혀 있을 때는 다시 칠하지 않는다 — 이 창은 열 때마다 <see cref="RefreshAll"/>
        ///   를 돌리므로 다음에 열리면 저절로 새 언어가 된다. 닫힌 창을 건드리면 꺼져 있는
        ///   자식을 훑다가 배선이 없는 상태를 밟는다.
        /// </summary>
        void HandleLanguageChanged()
        {
            LocalizeLabels();
            if (IsOpen) RefreshAll();
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
            // 같은 자리에 겹치는 다른 창과 맵 클릭 모드를 전부 닫는다.
            // ⚠ 예전에는 여기서 CharacterGrowthPanel 만 닫았다 — 부대 설정은 그대로 열려 있어서
            //   두 창이 겹쳐 보였다(유저 리포트 2026-08-13). 규칙을 HudExclusive 한 곳으로 모았다.
            if (open) HudExclusive.OpenOnly(this);

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
            _unitSprite = _unit != null ? _unit.GetComponent<SpriteRenderer>() : null;

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

            // ★★ <b>전면 잠금</b>(소환수) — 2026-08-21 · 유저 리포트:
            //   *"아루의 골렘이 … 전술 수정이 가능한 버그"*.
            //
            //   <see cref="CharacterTactics"/> 는 <b>이미</b> 잠긴 유닛의 값 변경을 전부
            //   거부하고 있었다(그 파일의 «골렘 — 전술 전면 잠금» 주석 11곳). 그런데 이 창은
            //   <b>역할 두 줄만</b> 잠금을 반영했다(``roleFree``) — 나머지 줄(교전 대상·탐험
            //   유형·웨이브 반응·배회 범위·후퇴 행동)은 버튼이 <b>멀쩡히 눌렸고</b> 색도
            //   평상시였다. 눌러도 값이 안 바뀌니 «되는 것 같은데 안 되는» 상태였다.
            //   → 여기서 한 번에 막는다. 옵션마다 조건을 또 적으면 새 줄이 늘 때 빠뜨린다.
            //
            // ⚠ <b>``on``(지금 선택된 값)은 그대로 강조한다</b> — 잠긴 줄도 «무엇으로
            //   고정됐는지» 는 보여야 한다(아래 ★ 주석과 같은 이유).
            bool locked = has && _tactics.TacticsLocked;

            for (int i = 0; i < _options.Count; i++)
            {
                Option option = _options[i];
                bool available = has && !locked &&
                                 (option.IsAvailable == null || option.IsAvailable());

                if (option.Button != null) option.Button.interactable = available;
                if (option.Background == null) continue;

                // ★ <b>선택 표시가 잠금보다 우선</b>이다 — 「선봉장」처럼 <b>줄 전체가 잠기는</b>
                //   경우, 잠김색을 먼저 칠하면 7칸이 똑같이 회색이 되어 <b>무엇으로 고정됐는지</b>
                //   화면에서 알 수 없다. 지금 값은 언제나 강조하고, 나머지만 잠김색으로 눌러
                //   "고를 수는 없지만 이걸로 정해져 있다"가 한눈에 보이게 한다.
                bool on = has && option.IsOn();
                Paint(option.Background, on ? ButtonState.On
                                        : available ? ButtonState.Normal : ButtonState.Off);
            }

            if (_hintText != null) _hintText.text = has ? selectionHint : noSelectionHint;

            // ★ 이 칸은 "탐험 유형" 버튼(사냥/정찰/탐색) 바로 아래에 있다 — 유저 지시
            //   2026-08-13: "탐색과 정찰에 대한 기능을 디테일하게 아래 설명칸에 기입, 예시:
            //   탐색 버튼을 눌렀을 때 아래 설명칸에 탐색에 대한 설명". 지금 선택된 탐험 유형이
            //   바뀔 때마다(버튼을 누를 때마다 RefreshAll 이 다시 불린다) 그 유형의 상세 설명으로
            //   갈아 끼운다. 예전에는 전체 지침을 한 문장으로 압축한 Order.Summarize() 를
            //   보여줬지만, 다른 항목은 버튼 색(선택 표시)만 봐도 뜻이 분명한 반면 탐험 유형
            //   세 가지는 "맞으면 반격하는지" · "도망가는지" 같은 차이가 버튼만으론 안 보여서
            //   이 칸이 필요했다 — 그래서 이 칸의 역할을 탐험 유형 설명으로 좁혔다.
            if (_summaryText != null)
                _summaryText.text = has ? TacticalOrder.Description(_tactics.Order.expeditionType) : "-";

            // 탐험 배회 범위 — 버튼 라벨은 "근방/외곽/전역" 두 글자뿐이라 뜻이 안 보인다.
            // 타일 값을 노출하지 말라는 지시(2026-08-14)가 있으므로 숫자 대신 이 한 줄로 설명한다.
            if (_roamHintText != null)
                _roamHintText.text = has ? TacticalOrder.Description(_tactics.Order.roamRange) : "-";

            RefreshRetreat();
            RefreshLiveValues();
        }

        void RefreshRetreat()
        {
            int percent = _tactics != null ? _tactics.Order.retreatHpPercent : 0;

            if (_retreatValueText != null)
                _retreatValueText.text = _tactics != null
                    ? (percent > 0 ? $"{percent}%"
                                   : HudTheme.T("ui_tactics_retreat_off", "사용 안 함"))
                    : "-";

            if (_retreatSlider == null) return;

            // ⚠ <b><see cref="Slider.SetValueWithoutNotify"/> 로 넣는다.</b> 그냥 <c>value</c> 에
            //   대입하면 <c>onValueChanged</c> 가 되불려 <b>표시가 지침을 다시 쓴다</b> —
            //   선택을 다른 캐릭터로 바꿀 때마다 이전 캐릭터의 값이 새 캐릭터에 덮어써진다.
            _retreatSlider.SetValueWithoutNotify(percent);

            // 선택이 없으면 끌 수 없게 한다 — 움직여도 아무 일이 없는 손잡이는 고장으로 보인다.
            // ★ 「가학증」(시그리드)으로 후퇴 기준이 고정된 캐릭터도 같은 이유로 끈다
            //   (2026-08-20). 값은 그대로 보이므로 «무엇으로 고정됐는지» 는 화면에 남는다.
            _retreatSlider.interactable = _tactics != null && !_tactics.RetreatHpLocked;
        }

        /// <summary>
        /// 초상화. <b>캐릭터 테이블의 일러스트</b>(<c>Resources/Illust/</c>)를 우선 쓰고,
        /// 정의가 없는 캐릭터만 인게임 스프라이트를 그대로 얹는다 —
        /// <c>CharacterGrowthPanel.RefreshPortrait</c> 와 완전히 같은 규칙이다.
        /// </summary>
        void RefreshPortrait()
        {
            Sprite sprite = null;

            if (_unit != null)
            {
                var def = _unit.Definition;
                if (def != null) sprite = def.Illust;
                if (sprite == null && _unitSprite != null) sprite = _unitSprite.sprite;
            }

            if (_portraitSprite != null)
            {
                _portraitSprite.sprite = sprite;
                _portraitSprite.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);

                // ★ 액자(Portrait, RectMask2D)를 꽉 채운다 (2026-08-17) —
                //   예전에는 preserveAspect 로 '맞춰 넣기' 만 해서, 인게임 스프라이트로
                //   폴백한 경우(정의가 없는 캐릭터)에 액자 대부분이 비었다.
                //   인물이므로 잘릴 때 위(얼굴)를 남긴다. PortraitFit 주석 참조.
                PortraitFit.Cover(_portraitSprite, portraitVerticalAnchor);
            }

            if (_portraitHint != null) _portraitHint.SetActive(sprite == null);
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
                _erosion.Refresh(null);
                RefreshPortrait();
                return;
            }

            if (_nameText != null) _nameText.text = _unit.DisplayName;
            RefreshPortrait();

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
            _erosion.Refresh(_unit);
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
            _portraitSprite = FindImage("Info/Portrait/Sprite");
            _portraitHint = transform.Find("Info/Portrait/Hint")?.gameObject;

            _summaryText = FindText("Col3/Summary/Text");
            _roamHintText = FindText("Col3/RoamHint");
            _retreatValueText = FindText("Col2/RetreatValue");
            _hpGhost = FindImage("Info/HpBack/HpGhost");
            _erosion.Bind(transform, "Info/ErosionBack");

            // 스프라이트가 비어 있으면 fillAmount 가 무시되어 막대 길이가 안 변한다 —
            // UiFillBar 문서 참조. ⚠ <b>슬라이더의 Fill 은 여기 넣지 않는다</b> — 그쪽은
            // fillAmount 가 아니라 <b>렉트 크기</b>로 줄어드는 것이라 Filled 로 바꾸면 안 된다.
            UiFillBar.Prepare(_hpFill, _hpGhost);

            BindRetreatSlider();

            // 공격 유형 / 포지션
            //
            // ★ <b>「선봉장」(히스톤 80013)이 걸린 캐릭터는 이 두 줄이 통째로 잠긴다</b> —
            //   정의문이 "포지션은 전방 / 공격 유형은 근거리로 <u>고정</u>된다" 이기 때문이다.
            //   잠금은 두 겹이다: 여기서 버튼을 끄고, <see cref="CharacterTactics.SetAttackType"/>
            //   쪽에서도 값을 거부한다('전방 + 동료와 함께 후퇴' 와 같은 방식).
            //   ⚠ 잠긴 항목도 <b>지금 선택된 값은 그대로 강조</b>된다 — 색 계산이 IsOn 을
            //     따로 보기 때문이라 "무엇으로 고정됐는지"가 화면에 남는다.
            System.Func<bool> roleFree = () => !_tactics.RoleLocked;

            AddOption("Col1/Type/Melee",  () => Set(t => t.SetAttackType(TacticalAttackType.Melee)),
                      () => _tactics.Order.attackType == TacticalAttackType.Melee, roleFree);
            AddOption("Col1/Type/Ranged", () => Set(t => t.SetAttackType(TacticalAttackType.Ranged)),
                      () => _tactics.Order.attackType == TacticalAttackType.Ranged, roleFree);
            AddOption("Col1/Type/Magic",  () => Set(t => t.SetAttackType(TacticalAttackType.Magic)),
                      () => _tactics.Order.attackType == TacticalAttackType.Magic, roleFree);
            AddOption("Col1/Type/Heal",   () => Set(t => t.SetAttackType(TacticalAttackType.Heal)),
                      () => _tactics.Order.attackType == TacticalAttackType.Heal, roleFree);

            AddOption("Col1/Pos/Front", () => Set(t => t.SetPosition(TacticalPosition.Front)),
                      () => _tactics.Order.position == TacticalPosition.Front, roleFree);
            AddOption("Col1/Pos/Mid",   () => Set(t => t.SetPosition(TacticalPosition.Mid)),
                      () => _tactics.Order.position == TacticalPosition.Mid, roleFree);
            AddOption("Col1/Pos/Back",  () => Set(t => t.SetPosition(TacticalPosition.Back)),
                      () => _tactics.Order.position == TacticalPosition.Back, roleFree);

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
            //
            // ★ <b>「가학증」(시그리드 80016)이 걸린 캐릭터는 이 두 버튼이 잠긴다</b> —
            //   정의문이 "후퇴기준이 {Value_05}%로 <u>고정</u>됩니다" 이기 때문이다.
            //   선봉장과 같은 두 겹 잠금이다: 여기서 버튼을 끄고,
            //   <see cref="CharacterTactics.SetRetreatHpPercent"/> 쪽에서도 값을 거부한다.
            System.Func<bool> retreatFree = () => !_tactics.RetreatHpLocked;

            AddOption("Col2/Retreat/Minus", () => Set(t => t.SetRetreatHpPercent(t.Order.retreatHpPercent - retreatStep)),
                      () => false, retreatFree);
            AddOption("Col2/Retreat/Plus",  () => Set(t => t.SetRetreatHpPercent(t.Order.retreatHpPercent + retreatStep)),
                      () => false, retreatFree);

            // 후퇴 시 행동 — '동료와 함께 후퇴'는 전방 포지션에서 고를 수 없다.
            // 잠금은 RefreshAll 이 매번 다시 판단한다(포지션을 바꾸면 그 자리에서 반영돼야 한다).
            AddOption("Col2/RetreatAction/Keep",
                      () => Set(t => t.SetRetreatAction(TacticalRetreatAction.KeepFighting)),
                      () => _tactics.Order.retreatAction == TacticalRetreatAction.KeepFighting);
            AddOption("Col2/RetreatAction/WithAlly",
                      () => Set(t => t.SetRetreatAction(TacticalRetreatAction.FallBackWithAlly)),
                      () => _tactics.Order.retreatAction == TacticalRetreatAction.FallBackWithAlly,
                      () => _tactics.Order.CanFallBackWithAlly);

            // 탐험 유형 — 2026-08-15 부터 <b>사냥 / 탐색 둘</b>이다.
            // ⚠ 씬의 `Col3/Non/Patrol` 버튼은 <b>비활성으로 껐다</b>(지우지 않았다) —
            //   슬롯을 지우면 하이라키의 세로 배치가 밀리고, 정찰이 되살아날 때 다시 만들어야 한다.
            //   여기서 배선을 안 하므로 눌러도 아무 일도 일어나지 않는다.
            AddOption("Col3/Non/Hunt",    () => Set(t => t.SetExpeditionType(TacticalExpeditionType.Hunt)),
                      () => _tactics.Order.expeditionType == TacticalExpeditionType.Hunt);
            AddOption("Col3/Non/Explore", () => Set(t => t.SetExpeditionType(TacticalExpeditionType.Explore)),
                      () => _tactics.Order.expeditionType == TacticalExpeditionType.Explore);

            // 탐험 배회 범위 — 성역 중심 원 안에서만 돌아다니게 하는 지침(유저 지시 2026-08-14).
            // ★ 협동 탐험이 켜진 부대면 <b>부대원 전원이 같이 바뀐다</b> — 그 전파는
            //   CharacterTactics.SetRoamRange 안에 있다(UI 는 아무것도 모른다).
            AddOption("Col3/Roam/Near", () => Set(t => t.SetRoamRange(TacticalRoamRange.Near)),
                      () => _tactics.Order.roamRange == TacticalRoamRange.Near);
            AddOption("Col3/Roam/Mid",  () => Set(t => t.SetRoamRange(TacticalRoamRange.Mid)),
                      () => _tactics.Order.roamRange == TacticalRoamRange.Mid);
            AddOption("Col3/Roam/Far",  () => Set(t => t.SetRoamRange(TacticalRoamRange.Far)),
                      () => _tactics.Order.roamRange == TacticalRoamRange.Far);

            // 웨이브 반응
            AddOption("Col3/Wave/Priority", () => Set(t => t.SetWaveReaction(TacticalWaveReaction.KeepExploring)),
                      () => _tactics.Order.waveReaction == TacticalWaveReaction.KeepExploring);
            AddOption("Col3/Wave/Defend",   () => Set(t => t.SetWaveReaction(TacticalWaveReaction.DefendNow)),
                      () => _tactics.Order.waveReaction == TacticalWaveReaction.DefendNow);

            // 초기화 / 닫기
            AddOption("Footer/ResetButton", () => Set(t => t.ResetToDefault()), () => false);

            HookClose("Header/CloseButton");
            HookClose("Footer/CloseButton");
        }

        /// <summary>
        /// 후퇴 기준 <b>드래그 슬라이더</b> (2026-08-18, 유저 지시:
        /// <i>"HP 후퇴기준에도 음량 조절 처럼 코딩으로 드래그바 넣어줘"</i>).
        ///
        /// ★ 음량(<see cref="VolumeSlider"/>)과 <b>똑같은 방식</b>이다 — 유니티 <see cref="Slider"/> 의
        /// <c>fillRect</c>/<c>handleRect</c>/<c>targetGraphic</c> 은 MCP 로 넣을 수 없는 오브젝트
        /// 참조라, <b>구조는 MCP 로 만들고 참조는 코드가 이름으로 꽂는다</b>
        /// (<see cref="SliderWiring"/>).
        ///
        /// ⚠ 예전에는 이 자리가 <see cref="UiDragBar"/>(참조가 필요 없는 최소 구현) + ± 버튼이었다.
        /// 막대에 <b>손잡이가 없어</b> 끌 수 있다는 것이 화면에 안 보이던 것이 교체 이유다.
        /// ± 버튼(<see cref="retreatStep"/>%)은 <b>그대로 남긴다</b> — 정확한 값을 맞출 때 쓴다.
        /// </summary>
        void BindRetreatSlider()
        {
            Transform node = transform.Find("Col2/RetreatSlider");
            _retreatSlider = node != null ? node.GetComponent<Slider>() : null;
            if (_retreatSlider == null)
            {
                Debug.LogWarning("[Tactics] 'Col2/RetreatSlider' 에서 Slider 를 찾지 못했습니다.", this);
                return;
            }

            SliderWiring.Wire(_retreatSlider,
                              node.Find("Fill Area/Fill") as RectTransform,
                              node.Find("Handle Slide Area/Handle") as RectTransform);

            _retreatSlider.minValue = 0f;
            _retreatSlider.maxValue = 100f;
            _retreatSlider.wholeNumbers = true;      // 후퇴 기준은 1% 단위다
            _retreatSlider.direction = Slider.Direction.LeftToRight;

            _retreatSlider.onValueChanged.RemoveAllListeners();
            _retreatSlider.onValueChanged.AddListener(value =>
                Set(t => t.SetRetreatHpPercent(Mathf.RoundToInt(value))));
        }

        /// <summary>지침을 바꾸는 공통 경로 — 선택이 없으면 아무 일도 하지 않는다.</summary>
        void Set(System.Action<CharacterTactics> change)
        {
            if (_tactics == null) return;
            change(_tactics);
            RefreshAll();
        }

        /// <param name="isAvailable">
        /// 지금 이 선택지를 고를 수 있는지. null 이면 항상 고를 수 있다.
        /// 고를 수 없는 선택지는 <b>숨기지 않고 잠근다</b> — 사라지면 "원래 없는 기능"으로 보이고,
        /// 왜 못 고르는지도 알 수 없다.
        /// </param>
        void AddOption(string path, System.Action onPick, System.Func<bool> isOn,
                       System.Func<bool> isAvailable = null)
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
                IsAvailable = isAvailable == null ? null : (System.Func<bool>)(() => _tactics != null && isAvailable()),
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

        /// <summary>
        /// 옵션 칸의 «고른 것 / 잠긴 것» 을 그림으로 바꾼다 (2026-08-25 · 버튼 그림 도입).
        /// 그림이 없으면 예전처럼 색을 칠한다.
        /// </summary>
        void Paint(Image img, ButtonState state) =>
            HudTheme.PaintButton(img, state,
                state == ButtonState.On ? optionSelected :
                state == ButtonState.Off ? optionDisabled : optionNormal);

    
        /// <summary>
        /// ★ 이 창의 문구를 <b>스트링 표</b>에서 가져온다 (2026-08-26 · 178-5절).
        /// 인스펙터 값은 <b>폴백</b>이다 — 표에 키가 없으면 화면은 지금과 같다.
        /// </summary>
        void LocalizeLabels()
        {
            noSelectionName = HudTheme.T("ui_sel_none_name", noSelectionName);
            noSelectionHint = HudTheme.T("ui_sel_none_hint", noSelectionHint);
            selectionHint = HudTheme.T("ui_sel_switch_hint", selectionHint);
        }
}
}
