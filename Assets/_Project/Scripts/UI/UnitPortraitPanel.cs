using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LastSanctuary.Combat;
using LastSanctuary.Units;
using LastSanctuary.Relics;

namespace LastSanctuary.UI
{
    /// <summary>
    /// <b>클릭한 유닛의 일러스트</b>를 로그 창 아래에 띄우는 패널 (2026-08-15, 유저 지시).
    ///
    /// <i>"캐릭터 뿐만 아니라 몬스터 들도 클릭 가능하게 만들고 클릭하면 콘솔 로그 아래에
    /// 클릭한 객체의 일러스트가 연동되는 ui 만들기"</i> ·
    /// <i>"해당 ui 는 다시 땅을 클릭하면 비활성화 되는 로직(게임 시작 시 비활성화)"</i>
    ///
    /// ★ <b>선택을 따라가기만 한다</b>
    /// ----------------------------
    /// 이 패널은 <b>아무것도 고르지 않는다.</b> <see cref="UnitSelector.OnUnitSelectionChanged"/>
    /// 를 구독해 값이 오면 켜고, null 이 오면 끈다 — 그게 곧 "땅을 클릭하면 비활성화" 다
    /// (빈 땅 클릭 → <c>UnitSelector.Clear()</c> → null). <b>땅 클릭을 따로 감지하지 않는다</b>:
    /// 클릭 판정이 두 곳에 생기면 드래그 임계값·UI 위 클릭 제외 같은 규칙을 두 벌 유지해야 한다.
    /// <c>TacticalOrderPanel</c> 이 선택을 따라가는 구조와 같다.
    ///
    /// ★ <b>게임 시작 시 비활성</b> — <see cref="Awake"/> 에서 스스로 꺼진다. 씬에 켜진 채로
    /// 저장돼 있어도 첫 프레임에 닫힌다(<c>SkillDetailPanel</c> 과 같은 방식).
    ///
    /// ⚠ <b>HudExclusive 에 넣지 않았다.</b> 그 배타 규칙은 "창을 열면 다른 창이 닫힌다" 인데,
    /// 이 패널은 <b>창이 아니라 표시</b>다 — 전술 지침을 보면서 몬스터를 클릭해 정보를 보는 것이
    /// 자연스럽고, 그때 지침 창이 닫히면 오히려 방해가 된다.
    ///
    /// ⚠ 일러스트가 없는 종(웨이브 몬스터·성역·포탑)은 <b>그림 없이 이름·칭호만</b> 보여준다.
    ///   표에 <c>illust</c> 칸이 생기면 그대로 뜬다 — 이 코드는 안 바뀐다.
    /// </summary>
    public class UnitPortraitPanel : MonoBehaviour
    {
        [Header("문구")]
        [Tooltip("일러스트가 없는 유닛의 그림 자리에 대신 띄울 한 줄")]
        [SerializeField] string noArtText = "일러스트 없음";

        [Tooltip("{0} = 강화 횟수. 캐릭터에게만 쓴다")]
        [SerializeField] string levelFormat = "Lv.{0}";

        [Tooltip("보스·몬스터 체력 줄. {0} = 백분율(정수)")]
        [SerializeField] string hpFormat = "HP: {0}%";

        [Tooltip("잠긴 스킬 줄. {0} = 스킬 이름 · {1} = 해금에 필요한 강화 횟수")]
        [SerializeField] string lockedSkillFormat = "{0} <size=78%>(Lv.{1})</size>";

        [Header("색")]
        [SerializeField] Color titleColor = new Color(0.84f, 0.64f, 1f, 1f);

        [Tooltip("평소 이름 색. ★ 영웅 각성한 캐릭터는 이 값을 무시하고 " +
                 "HudTheme.TextHero(금색)로 그린다 — 두 창이 같은 색을 쓰게 하려고 " +
                 "각성 색은 인스펙터에 두지 않았다")]
        [SerializeField] Color nameColor = new Color(0.88f, 0.92f, 0.94f, 1f);

        [Tooltip("잠긴 스킬의 <b>아이콘</b> 색 — 알파를 낮춰 '비활성' 으로 읽히게 한다")]
        [SerializeField] Color lockedIconColor = new Color(1f, 1f, 1f, 0.25f);

        // ★★★ 유물 칸 셋 (2026-08-26)
        [Tooltip("★ 유물 아이콘을 늘어놓는 간격(픽셀). 카드의 유물 줄은 <b>왼쪽 끝</b>에서 " +
                 "시작하므로 오른쪽으로 자란다(양수). 0 이면 «아이콘 폭 + 2px»")]
        [SerializeField] float relicIconStepPixels = 30f;

        [Tooltip("잠긴 스킬의 <b>글자</b> 색")]
        [SerializeField] Color lockedTextColor = new Color(0.45f, 0.49f, 0.55f, 1f);

        [Header("문구 · 색 — 분노 게이지 (히스톤 「분노」)")]
        [Tooltip("{0} = 지금 분노, {1} = 분노 상한(100). ★ 성장 창과 같은 형식으로 둔다 — " +
                 "두 창이 다른 문구를 쓰면 «어느 쪽이 맞지» 가 된다")]
        [SerializeField] string rageFormat = "분노 {0:0.#} / {1:0}";

        [Tooltip("평소 분노 숫자 색")]
        [SerializeField] Color rageTextColor = new Color(0.96f, 0.62f, 0.45f, 1f);

        [Tooltip("분노가 가득 찼을 때(= 부활 준비) 숫자 색")]
        [SerializeField] Color rageFullColor = new Color(1f, 0.42f, 0.36f, 1f);

        [Header("문구 · 색 — 영혼 수 (시안 「영혼 흡수」 · 2026-08-25)")]
        [Tooltip("{0} = 지금까지 거둔 영혼 수. ⚠ 상한이 없어 «/ 최대» 가 없다.\n" +
                 "★ 성장 창(CharacterGrowthPanel.soulFormat)과 <b>같은 형식</b>으로 둔다 — " +
                 "두 창이 다른 문구를 쓰면 «어느 쪽이 맞지» 가 된다")]
        [SerializeField] string soulFormat = "획득한 영혼 수: {0:0}개";

        [Tooltip("영혼 숫자 색 — 성장 창과 같은 값")]
        [SerializeField] Color soulTextColor = new Color(0.78f, 0.72f, 0.92f, 1f);

        [Header("체력바 색 (보스·몬스터)")]
        [SerializeField] Color barHigh = new Color(0.40f, 0.85f, 0.52f, 1f);
        [SerializeField] Color barLow = new Color(0.92f, 0.38f, 0.38f, 1f);

        [Header("갱신")]
        [Tooltip("상태·체력처럼 <b>계속 변하는 칸</b>만 다시 그리는 주기(초). " +
                 "이름·칭호·스킬 같은 정적인 칸은 선택이 바뀔 때만 그린다")]
        [Min(0.05f)] [SerializeField] float volatileRefreshInterval = 0.2f;

        [Header("일러스트 채우기 (2026-08-17)")]
        [Tooltip("★ <b>캐릭터</b> 그림이 세로로 잘릴 때 남길 위치 (0=아래 · 0.5=가운데 · 1=위).\n" +
                 "인물화는 위쪽을 남겨야 <b>얼굴</b>이 들어온다 — 가운데를 남기면 가슴이 남는다")]
        [Range(0f, 1f)] [SerializeField] float characterVerticalAnchor = 0.86f;

        [Tooltip("<b>몬스터</b> 그림이 세로로 잘릴 때 남길 위치. " +
                 "전신 실루엣 자체가 정보라 가운데가 맞다")]
        [Range(0f, 1f)] [SerializeField] float monsterVerticalAnchor = 0.5f;

        Image _art;
        TMP_Text _nameText;
        TMP_Text _titleText;
        TMP_Text _noArtLabel;

        // ── PPT 목업(2026-08-19)에서 새로 생긴 칸들 ───────────────────────────
        //   캐릭터 : 칭호 / 이름 · 레벨 · 상태 / 스킬 3줄
        //   보스   : 칭호 / 이름 · 상태 / 체력바 · HP %
        TMP_Text _levelText;
        TMP_Text _stateText;

        /// <summary>스킬 3줄(캐릭터 전용). 슬롯 번호 = 패시브 슬롯 번호다.</summary>
        Transform _skillsRoot;
        readonly Transform[] _skillSlots = new Transform[SkillSlots];
        readonly Image[] _skillIcons = new Image[SkillSlots];
        readonly TMP_Text[] _skillLabels = new TMP_Text[SkillSlots];

        // ── 분노 게이지 (히스톤 「분노」 해금 시에만 켜진다) — 2026-08-21 ──────
        //   하이라키: <c>Rage</c> ▸ <c>RageLabel</c>(글씨) + <c>RageBack</c> ▸ <c>RageFill</c>(바)
        //   ★ 유저 지시대로 <b>글씨 아래에 바</b>가 온다.
        Transform _rageRoot;
        Transform _rageBack;      // 막대 — 분노일 때만 켠다 (영혼은 글자만)
        TMP_Text _rageLabel;
        Image _rageFill;

        // ── 장착 유물 한 줄 (2026-08-24 · 유저 지시: *"캐릭터 선택하면 나오는 ui
        //   스킬 아래에 장착한 유물도 나오게 해줘"*) ─────────────────────────────
        //   하이라키: <c>Relic</c> ▸ <c>Icon</c> + <c>Label</c>
        //   ★ 성장 창의 유물 띠(<see cref="CharacterGrowthPanel"/>)와 <b>같은 것을 읽는다</b>
        //     (<c>RelicInventory.EquippedOn</c>) — 두 창이 어긋나지 않게.
        Transform _relicRoot;
        Image _relicIcon;
        TMP_Text _relicLabel;

        /// <summary>
        /// ★★★ 유물 칸 셋 (2026-08-26). 씬에는 아이콘 하나(<c>Relic/Icon</c>)만 있고
        /// 나머지 둘은 <see cref="RelicIconStrip"/> 이 복제한다.
        /// </summary>
        readonly RelicIconStrip _relicStrip = new RelicIconStrip();

        /// <summary>체력 묶음(보스·몬스터 전용).</summary>
        Transform _hpRoot;
        Image _hpFill;
        TMP_Text _hpText;

        /// <summary>패시브 슬롯 수 — <c>CharacterPassives.Refresh</c> 와 같은 3 이다.</summary>
        const int SkillSlots = 3;

        UnitSelector _selector;
        DamageableUnit _shown;
        bool _bound;

        /// <summary>다음에 <see cref="RefreshVolatile"/> 을 돌릴 시각.</summary>
        float _nextVolatileRefresh;

        /// <summary>
        /// 강화 서비스를 구독해 뒀는지. 서비스는 <c>GameSystems</c> 에 상주하지만
        /// <b>이 패널보다 늦게 살아날 수 있어</b> 한 번에 못 걸릴 때가 있다 —
        /// 그래서 <see cref="Subscribe"/> 를 여러 곳에서 다시 부르고 이 칸으로 중복을 막는다.
        /// </summary>
        bool _upgradeHooked;

        /// <summary>
        /// 비활성으로 시작하므로 <c>Awake</c> 가 안 돌 수 있다 — <c>SkillDetailPanel</c> 이 겪은
        /// 함정 그대로다(49-6절·36-4절). 비활성까지 포함해 찾고 배선을 보장한다.
        /// </summary>
        public static UnitPortraitPanel Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = FindAnyObjectByType<UnitPortraitPanel>(FindObjectsInactive.Include);
                if (_instance != null) _instance.EnsureBound();
                return _instance;
            }
            private set => _instance = value;
        }

        static UnitPortraitPanel _instance;

        void Awake()
        {
            Instance = this;
            EnsureBound();

            // ⚠⚠ <b>여기서 gameObject.SetActive(false) 를 부르면 안 된다.</b>
            //
            //   이 창은 씬에 <b>비활성으로 저장</b>돼 있다. 비활성 오브젝트의 Awake 는
            //   씬 로드 때 <b>안 돌고</b>, 나중에 <c>SetActive(true)</c> 로 켜지는 순간
            //   <b>그 호출 안에서 동기적으로</b> 돈다. 그래서 Awake 에서 자기를 끄면
            //   <b>열리는 바로 그 순간 스스로 닫혀</b> 창이 영영 안 뜬다
            //   (유저 리포트 2026-08-15: "지금 일러스트 연동 ui 작동안함").
            //
            //   "게임 시작 시 비활성"은 <b>씬에 그렇게 저장해서</b> 지킨다. 혹시 켜진 채로
            //   저장됐더라도 항상 살아 있는 <see cref="UnitSelector"/> 가
            //   <see cref="Bind"/> 에서 한 번 닫아준다.
            //
            //   ⚠ 같은 코드가 <c>SkillDetailPanel</c>·<c>SubjugationPanel</c> 에도 있었다 —
            //     셋 다 같은 이유로 고쳤다.
        }

        void OnEnable() => Subscribe();

        /// <summary>
        /// ⚠⚠ <b>여기서 구독을 끊으면 안 된다</b> (2026-08-16, 유저 리포트:
        /// <i>"땅을 클릭하면 영원히 다시 활성화가 안되는 게 아니라 ... 또 다른 캐릭터나
        /// 몬스터 클릭하면 일러스트 연동되서 나오게"</i>).
        ///
        /// 예전에는 여기서 <c>OnUnitSelectionChanged -= …</c> 를 했다. 그런데 이 패널은
        /// <b>닫힐 때 비활성이 된다</b>(빈 땅 클릭 → <see cref="Close"/>). 그 순간 구독이
        /// 끊기므로 <b>그다음 유닛 클릭이 이 패널에 영영 도달하지 못한다</b> — 한 번 닫으면
        /// 다시는 안 열렸다.
        ///
        /// 구독은 <b>델리게이트</b>라 컴포넌트가 꺼져 있어도 살아 있다. 그래서 이 패널은
        /// <b>꺼진 채로 선택 이벤트를 기다리다가</b> 값이 오면 스스로 켜진다 — 그것이
        /// 이 창의 동작 방식이다. 정리는 <see cref="OnDestroy"/> 에서만 한다.
        /// </summary>
        void OnDisable()
        {
            // 일부러 비워 둔다 — 위 주석 참조.
        }

        void OnDestroy()
        {
            if (_selector != null) _selector.OnUnitSelectionChanged -= HandleUnitSelected;

            // ⚠ <b>정적 이벤트는 반드시 떼야 한다</b> — 씬을 다시 열면 파괴된 이 패널이
            //   구독자로 남아 «없어진 오브젝트» 를 건드린다(GameSnapshot 이 같은 이유로
            //   OnDestroy 에서 떼는 것과 같다).
            DamageableUnit.OnAnyDied -= HandleAnyDied;
            Data.StringTable.OnLanguageChanged -= HandleLanguageChanged;

            Units.CharacterUpgradeService service = Units.CharacterUpgradeService.Instance;
            if (service != null) service.OnUpgraded -= HandleUpgraded;
            _upgradeHooked = false;

            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// ⚠ 이 패널은 <b>꺼진 채로 시작</b>하므로 <c>OnEnable</c> 만으로는 구독이 안 걸린다
        /// (꺼져 있으면 아예 안 불린다). 그래서 <b>선택 이벤트를 놓치지 않도록</b>
        /// 씬에 상주하는 중계자가 필요한데, 여기서는 <see cref="UnitSelector"/> 쪽에서
        /// 이 패널을 <b>깨워주는</b> 대신 — 아래 <see cref="Bind"/> 를 <c>UnitSelector</c> 가
        /// 시작할 때 한 번 부르게 한다.
        /// </summary>
        void Subscribe()
        {
            if (_selector != null || (_selector = UnitSelector.Instance) != null)
            {
                _selector.OnUnitSelectionChanged -= HandleUnitSelected;
                _selector.OnUnitSelectionChanged += HandleUnitSelected;
            }

            // ★★ 보고 있는 대상이 죽으면 닫는다 (2026-08-25 · HandleAnyDied 의 설명).
            //   ⚠ <b>정적 이벤트</b>라 두 번 걸면 두 번 불린다 — 먼저 떼고 다시 건다
            //     (위 선택 이벤트와 같은 방식).
            DamageableUnit.OnAnyDied -= HandleAnyDied;
            DamageableUnit.OnAnyDied += HandleAnyDied;

            // ★★★ <b>언어를 바꾸면 그 자리에서 다시 그린다</b> (2026-08-26 · 유저 리포트:
            //   *"프로필 UI가 열려있는 상태에서 언어 변경하면 바로 초상화 UI내에 있는 문자들이
            //     영어로 바뀌지 않고 다른 곳 클릭했다가 클릭해야 바뀜"*).
            //
            //   <b>왜 안 바뀌었나</b> — 이 창은 «고른 유닛이 바뀔 때» 만 글을 다시 쓴다
            //   (<see cref="Show"/>). 언어는 <see cref="Data.StringTable"/> 안에서만 바뀌므로
            //   창은 그 사실을 <b>모른 채</b> 옛 글을 들고 있었다. 그래서 다른 데를 눌러
            //   «선택 바뀜» 을 한 번 일으켜야 바뀌었다.
            //   → 미결 133번(«언어를 바꿔도 상시 HUD 가 안 바뀐다»)의 <b>첫 구독자</b>다.
            //
            // ⚠ 정적 이벤트라 두 번 걸면 두 번 불린다 — 먼저 떼고 다시 건다(위와 같은 방식).
            Data.StringTable.OnLanguageChanged -= HandleLanguageChanged;
            Data.StringTable.OnLanguageChanged += HandleLanguageChanged;

            HookUpgrades();
        }

        /// <summary>
        /// 언어가 바뀌었다 — <b>보고 있는 대상을 그대로 다시 그린다</b>.
        ///
        /// ★ <see cref="Show"/> 를 다시 부르는 것이 가장 안전하다 — 이름·칭호·역할·스킬 이름
        ///   같은 «한 번만 쓰는 글» 이 전부 그 안에서 정해지기 때문이다. 매 프레임 도는
        ///   <see cref="RefreshVolatile"/> 은 상태 글만 다시 쓴다.
        /// ⚠ 창이 닫혀 있으면(<c>_shown == null</c>) 아무 일도 하지 않는다 — 여기서 <c>Show</c> 를
        ///   부르면 <b>닫아 둔 창이 저절로 열린다</b>.
        /// </summary>
        void HandleLanguageChanged()
        {
            if (_shown == null || !gameObject.activeSelf) return;
            Show(_shown);
        }

        /// <summary>
        /// ★★★ <b>보고 있던 대상이 죽으면 창을 닫는다</b> (2026-08-25 · 유저 지시:
        /// *"초상화 ui 가 죽지 않은 대상을 잡고 있을때 해당 대상이 죽으면 비활성화 되고
        /// 다시 다른 살아있는 대상을 클릭했을 때 활성화 되게 해줘"*).
        ///
        /// <b>왜 필요한가</b> — 이 창은 «선택이 바뀔 때만» 다시 그린다(정적 카드라 폴링이 없다).
        /// 그래서 몬스터를 눌러 두고 그 몬스터가 죽으면 <b>시체의 정보가 그대로 남아</b>
        /// 체력 0%인 카드가 화면에 붙어 있었다. 선택은 여전히 그 유닛을 가리키고 있으므로
        /// 다른 유닛을 누를 때까지 사라지지 않는다.
        ///
        /// ★ <b>«다시 활성화» 는 따로 할 일이 없다.</b> 살아 있는 대상을 클릭하면
        /// <see cref="UnitSelector"/> 가 선택 변경을 알리고 <see cref="Show"/> 가 창을 켠다 —
        /// 원래 있던 경로다. 여기서는 <b>닫는 것만</b> 맡는다.
        ///
        /// ⚠ <b>부활 대기 중이면 닫지 않는다</b> — 히스톤 「분노」가 되살릴 캐릭터는
        ///   «죽었다» 이벤트가 나지만 곧 일어난다. 그때 창이 깜빡이면 오히려 고장으로 보인다
        ///   (<see cref="Save.GameSnapshot.HandleAnyDied"/> 가 저장을 건너뛰는 것과 같은 판단).
        /// </summary>
        void HandleAnyDied(DamageableUnit unit)
        {
            if (unit == null || !ReferenceEquals(unit, _shown)) return;
            if (unit is CharacterUnit c && c.IsRevivePending) return;

            Close();
        }

        /// <summary>
        /// ★ <b>강화하면 레벨이 즉시 이 카드에 반영되게</b> 한다 (유저 지시 2026-08-18:
        /// <i>"캐릭터 강화 시에 레벨 단계가 즉시 일러스트 ui 에 연동되게"</i>).
        ///
        /// <b>왜 필요했나</b> — 이름 줄은 예전부터 <c>Lv.N</c> 을 같이 적고 있었다
        /// (<see cref="NameLineOf"/>, 86-8절). 그런데 그 글자를 쓰는 곳이
        /// <see cref="Show"/> <b>하나뿐</b>이고 <see cref="Show"/> 는 <b>선택이 바뀔 때만</b>
        /// 불린다. 그래서 카드를 띄워 둔 채로 강화하면 <b>레벨이 옛 값 그대로 남았다</b> —
        /// 다른 캐릭터를 클릭했다 돌아와야 갱신됐다.
        ///
        /// ⚠ <b>매 프레임 다시 그리지 않는다.</b> 이 카드는 값이 거의 안 바뀌는 정적 카드라
        /// (로스터·성장 창처럼 주기 갱신이 없다) 폴링을 넣으면 그것만으로 상시 비용이 생긴다.
        /// <b>바뀌는 순간에만</b> 알림을 받는 편이 이 창의 성격에 맞는다.
        ///
        /// ⚠ 구독은 <b>델리게이트</b>라 이 패널이 꺼져 있어도 살아 있다 —
        /// <see cref="OnDisable"/> 주석의 그 성질을 여기서도 그대로 쓴다.
        /// 그래서 카드를 닫아 둔 동안 강화가 일어나도, 다시 열면 이미 맞는 값이 그려진다.
        /// </summary>
        void HookUpgrades()
        {
            if (_upgradeHooked) return;

            Units.CharacterUpgradeService service = Units.CharacterUpgradeService.Instance;
            if (service == null) return;          // 아직 안 살아났다 — 다음 기회에 다시 시도한다

            service.OnUpgraded -= HandleUpgraded;
            service.OnUpgraded += HandleUpgraded;
            _upgradeHooked = true;
        }

        /// <summary>
        /// 강화가 성사됐다. <b>지금 이 카드에 떠 있는 그 캐릭터</b>일 때만 다시 그린다 —
        /// 다른 캐릭터를 강화했다고 이 카드가 흔들릴 이유가 없다.
        ///
        /// ⚠ 정신 이상 「고조」의 무료 강화(<c>GrowFree</c>)도 같은 이벤트를 쏜다 —
        /// 그쪽도 레벨이 오르므로 <b>같이 반영되는 것이 맞다</b>.
        /// </summary>
        void HandleUpgraded(Units.CharacterUnit unit, int cost)
        {
            if (unit == null || !ReferenceEquals(unit, _shown)) return;
            Show(unit);
        }

        /// <summary>
        /// <see cref="UnitSelector"/> 가 <c>Start</c> 에서 한 번 부른다 — <b>꺼져 있는 패널이
        /// 스스로 구독할 수 없기 때문</b>이다. 여기서 건 구독은 패널이 꺼져 있어도 살아 있다
        /// (구독 주체는 컴포넌트가 아니라 델리게이트다).
        /// </summary>
        public void Bind(UnitSelector selector)
        {
            EnsureBound();

            // "게임 시작 시 비활성" — 씬에 켜진 채 저장됐더라도 여기서 한 번 닫는다.
            // (Awake 에서 닫으면 안 되는 이유는 그쪽 주석 참조.)
            if (gameObject.activeSelf) Close();

            _selector = selector;
            if (_selector != null)
            {
                _selector.OnUnitSelectionChanged -= HandleUnitSelected;
                _selector.OnUnitSelectionChanged += HandleUnitSelected;
            }

            // 강화 서비스도 같이 건다 — 이 시점(UnitSelector.Start)이면 GameSystems 는 이미 깨어 있다.
            HookUpgrades();
        }

        // ------------------------------------------------------------------

        void HandleUnitSelected(DamageableUnit unit)
        {
            if (unit == null) { Close(); return; }
            Show(unit);
        }

        /// <summary>지금 보여주고 있는 유닛. 없으면 null.</summary>
        public DamageableUnit Shown => _shown;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>패널을 닫는다 — 빈 땅 클릭이 이 경로로 들어온다.</summary>
        public void Close()
        {
            _shown = null;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>유닛 하나를 보여준다.</summary>
        public void Show(DamageableUnit unit)
        {
            if (unit == null) { Close(); return; }

            EnsureBound();
            HookUpgrades();      // 서비스가 늦게 살아난 경우를 대비 — 이미 걸려 있으면 즉시 반환한다
            _shown = unit;
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            Sprite art = unit.Portrait;
            if (_art != null)
            {
                _art.sprite = art;
                // 그림이 없을 때 흰 사각형이 남지 않도록 알파로 지운다
                // (Image 를 끄면 레이아웃이 흔들린다).
                _art.color = art != null ? Color.white : new Color(1f, 1f, 1f, 0f);

                // ★ 액자를 꽉 채운다 (2026-08-17) — preserveAspect 는 '맞춰 넣기' 라
                //   세로형 인물화가 가로 액자에서 폭의 46% 만 쓰고 나머지가 빈 공간이었다.
                //   자세한 계산은 PortraitFit 클래스 주석 참조.
                //
                //   ⚠ 사람과 몬스터는 남길 곳이 다르다 — 인물은 위(얼굴), 전신 몬스터는
                //     가운데(실루엣 전체). 세로로 잘릴 때만 차이가 난다.
                PortraitFit.Cover(_art,
                    unit is CharacterUnit ? characterVerticalAnchor : monsterVerticalAnchor);
            }
            if (_noArtLabel != null)
            {
                _noArtLabel.text = noArtText;
                _noArtLabel.gameObject.SetActive(art == null);
            }

            // 이름은 <b>이름만</b> 적는다 — 레벨은 옆 칸(Level)이 따로 맡는다(2026-08-19 목업).
            if (_nameText != null)
            {
                _nameText.text = unit.DisplayName;

                // ★★ <b>영웅 각성한 캐릭터는 이름이 금색이다</b> (2026-08-21 · 유저 지시).
                //   판정은 <see cref="Combat.CharacterKills.IsHero"/> 한 곳이다 — 각성 횟수가
                //   1 이상인가. 로스터도 <b>같은 함수</b>를 본다(규칙이 두 벌이 되지 않게).
                _nameText.color = IsHero(unit) ? HudTheme.TextHero : nameColor;
            }

            if (_titleText != null)
            {
                // ★ <b>칭호가 없으면 빈칸으로 둔다</b> (유저 확정 2026-08-19:
                //   "칭호 해금이 되지 않았을 때는 칭호칸 비워놔").
                //   예전에는 여기에 소속("아군"·"중립 몬스터")을 대신 적었는데, 그러면
                //   <b>칭호를 얻은 것처럼 보인다.</b> 소속은 아래 상태 칸이 대신 알려준다.
                string title = unit.Title;
                _titleText.text = string.IsNullOrWhiteSpace(title) ? string.Empty : title;
                _titleText.color = titleColor;
            }

            var character = unit as CharacterUnit;

            // 레벨은 캐릭터에게만 있다 — 몬스터 줄에서는 칸 자체를 비운다.
            if (_levelText != null)
                _levelText.text = character != null
                    ? string.Format(levelFormat, character.UpgradeCount)
                    : string.Empty;

            ShowSkills(character);
            ShowRelic(character);
            if (_hpRoot != null) _hpRoot.gameObject.SetActive(character == null);

            RefreshVolatile();
        }

        /// <summary>
        /// 영웅 각성한 캐릭터인가 — 이름을 금색으로 그릴지 정한다.
        ///
        /// ★ <see cref="CharacterKills.IsHero"/>(각성 횟수 ≥ 1)를 그대로 본다. 판정을
        ///   여기서 새로 짜지 않는다 — 로스터도 <b>같은 것</b>을 봐야 두 창이 안 어긋난다.
        /// ⚠ <see cref="CharacterKills.Of"/> 를 쓴다(<c>EnsureOn</c> 이 아니다) — 표시하는
        ///   쪽이 컴포넌트를 <b>만들어서는</b> 안 된다. 없으면 «각성 안 함» 이 맞다.
        /// </summary>
        static bool IsHero(DamageableUnit unit)
        {
            if (unit is not CharacterUnit character) return false;
            CharacterKills kills = CharacterKills.Of(character);
            return kills != null && kills.IsHero;
        }

        // ------------------------------------------------------------------
        //  ★★ 분노 게이지 (2026-08-21 · 유저 지시: *"히스톤 두번째 스킬 해방 시 분노가
        //      차는 분노바가 캐릭터 성장창에는 실시간으로 보이는데 캐릭터 상세 UI …
        //      에도 표기되면 좋겠어 분노 빨간색 바는 분노 글씨 아래에 넣어서"*)
        //
        //  ★ <b>성장 창과 같은 판정을 쓴다</b>(<c>CharacterGrowthPanel.ShowRageGauge</c>) —
        //    「분노」(<c>rage_on</c>) 스킬이 <b>해금된</b> 캐릭터에게만 보인다.
        //    ⚠ 인물 이름이나 슬롯 번호로 찾지 않는다 — 그 스킬이 어느 칸에 오는지는
        //      캐릭터 정의가 정한다(82-8절의 원칙). «스킬 종류» 로만 판정한다.
        //  ⚠ 캐릭터가 아니면(몬스터) 항상 끈다.
        // ------------------------------------------------------------------

        /// <summary>
        /// ★★★ <b>«스스로 쌓이는 수치» 칸</b> — 지금 둘이 이 한 칸을 나눠 쓴다
        /// (2026-08-25 · 유저 지시: *"시안 초상화 ui 의 첫번째 스킬에 획득한 영혼 표기"*).
        ///
        /// <code>
        ///   히스톤 「분노」    0~100 이라 «몇 분의 몇» 이 성립한다  →  <b>막대 + 글자</b>
        ///   시안   「영혼 흡수」 상한이 없다                        →  <b>글자만</b>
        /// </code>
        ///
        /// ★ <b>칸을 새로 만들지 않았다.</b> 성장 창이 같은 결론에 먼저 도달했다
        /// (<c>CharacterGrowthPanel.ShowSkillCounter</c>) — 한 캐릭터가 둘을 <b>동시에</b>
        /// 가지는 경우가 없으므로 칸 하나로 충분하고, 새 오브젝트를 만들면 씬에 배선할 것이
        /// 하나 더 늘고 <b>둘 중 어느 것도 없는 캐릭터</b>에서 빈 칸을 또 숨겨야 한다.
        /// ⚠ 그래서 하이라키 이름은 여전히 <c>Rage</c> 다 — 이름만 보고 «분노 전용» 으로 읽지 말 것.
        ///
        /// ★ <b>어느 «스킬 칸» 인지로 찾지 않는다</b> — 히스톤의 분노는 둘째 칸, 시안의 영혼은
        /// 첫째 칸이지만 그것은 <b>캐릭터 정의가 정하는</b> 것이다(82-8절의 원칙).
        /// «스킬 종류» 로만 판정한다.
        /// ⚠ 둘 다 <b>해금되어야</b> 보인다 — 잠긴 스킬의 수치가 새어나가면 안 된다.
        /// </summary>
        void RefreshSkillCounter()
        {
            if (_rageRoot == null) return;

            var character = _shown as CharacterUnit;
            bool rageOn = character != null && HasSkillUnlocked(character, PassiveSkillType.RageOn);
            bool soulOn = !rageOn && character != null
                          && HasSkillUnlocked(character, PassiveSkillType.SoulAbsorption);

            bool show = rageOn || soulOn;
            if (_rageRoot.gameObject.activeSelf != show) _rageRoot.gameObject.SetActive(show);
            if (!show) return;

            var passives = character.GetComponent<CharacterPassives>();

            // ── 막대는 <b>분노일 때만</b> 켠다 (영혼은 상한이 없어 «몇 분의 몇» 이 없다) ──
            if (_rageBack != null && _rageBack.gameObject.activeSelf != rageOn)
                _rageBack.gameObject.SetActive(rageOn);

            if (rageOn)
            {
                float rage = passives != null ? passives.Rage : 0f;
                float ratio = Mathf.Clamp01(rage / CharacterPassives.RageMax);

                if (_rageFill != null)
                {
                    UiFillBar.Prepare(_rageFill);
                    _rageFill.fillAmount = ratio;
                }
                if (_rageLabel != null)
                {
                    _rageLabel.text = string.Format(rageFormat, rage, CharacterPassives.RageMax);
                    _rageLabel.color = ratio >= 1f ? rageFullColor : rageTextColor;
                }
                return;
            }

            // ── 영혼 — 글자만 ──
            int souls = passives != null ? passives.SoulCount : 0;
            if (_rageLabel != null)
            {
                _rageLabel.text = string.Format(soulFormat, souls);
                _rageLabel.color = soulTextColor;
            }
        }

        /// <summary>
        /// 이 캐릭터가 <paramref name="want"/> 종류의 스킬을 <b>해금했는가</b>.
        /// 성장 창과 같은 조건이다 — 스킬 종류가 맞고 강화 횟수가 해금 기준을 넘었을 때.
        ///
        /// ★ 2026-08-25 — 「분노」 전용이던 것을 <b>종류를 받는 형태</b>로 넓혔다
        /// (시안 「영혼 흡수」가 같은 판정을 필요로 한다). 슬롯 번호는 캐릭터마다 다르므로
        /// <b>세 칸을 다 훑어</b> 그 종류를 찾는다.
        /// </summary>
        static bool HasSkillUnlocked(CharacterUnit character, PassiveSkillType want)
        {
            if (character == null || character.Definition == null) return false;

            for (int slot = 0; slot < SkillSlots; slot++)
            {
                PassiveSkillSO skill = character.Definition.PassiveAt(slot);
                if (skill == null) continue;
                if (PassiveSkillTypes.Parse(skill.skillType) != want) continue;
                return character.IsPassiveUnlocked(slot);
            }
            return false;
        }

        // ------------------------------------------------------------------
        // 스킬 3줄 — <b>확인용 표시</b>다 (유저 확정 2026-08-19)
        //
        // <i>"기능적으로 클릭했을 때 변동되는 UI가 아닌 말 그대로 어떤 스킬을 가지고 있는지
        // 레벨이 몇인지를 확인할 수 있는 확인용 UI"</i> — 그래서 이 줄에는 <b>Button 이 없고</b>
        // 눌러도 아무 일도 일어나지 않는다. 스킬 상세를 여는 창은 성장 창
        // (<see cref="CharacterGrowthPanel"/> → <see cref="SkillDetailPanel"/>) 쪽 몫이다.
        // ------------------------------------------------------------------

        /// <summary>
        /// 캐릭터의 패시브 3종을 그린다. <b>잠긴 슬롯도 지우지 않고 흐리게 보여준다</b> —
        /// "레벨 1일 때 스킬 2·3은 잠겨있으니까 비활성화된거처럼 표기" (유저 지시).
        /// 무엇을 갖게 될지 미리 보이는 것이 이 카드의 목적이다.
        ///
        /// 캐릭터가 아니면 묶음을 통째로 끈다 — 그 자리는 체력바가 쓴다.
        /// </summary>
        void ShowSkills(CharacterUnit character)
        {
            if (_skillsRoot != null) _skillsRoot.gameObject.SetActive(character != null);
            if (character == null) return;

            CharacterDefinitionSO def = character.Definition;

            for (int slot = 0; slot < SkillSlots; slot++)
            {
                Transform row = _skillSlots[slot];
                if (row == null) continue;

                PassiveSkillSO so = def != null ? def.PassiveAt(slot) : null;

                // 표에 그 슬롯이 아예 없는 캐릭터 — 빈 줄을 남기지 않고 통째로 숨긴다.
                if (so == null || !so.IsUsable) { row.gameObject.SetActive(false); continue; }
                row.gameObject.SetActive(true);

                bool unlocked = character.IsPassiveUnlocked(slot);

                if (_skillLabels[slot] != null)
                {
                    _skillLabels[slot].text = unlocked
                        ? so.DisplayName
                        : string.Format(lockedSkillFormat, so.DisplayName,
                                        PassiveUnlockConfig.RequiredUpgrades(slot));
                    _skillLabels[slot].color = unlocked ? HudTheme.TextMain : lockedTextColor;
                }

                if (_skillIcons[slot] != null)
                {
                    Sprite icon = so.Icon;
                    _skillIcons[slot].sprite = icon;
                    // 아이콘이 없는 스킬은 흰 사각형이 남지 않게 알파로 지운다(그림 자리와 같은 규칙).
                    _skillIcons[slot].color = icon == null
                        ? new Color(1f, 1f, 1f, 0f)
                        : (unlocked ? Color.white : lockedIconColor);
                }
            }
        }

        /// <summary>
        /// ★ <b>장착한 유물 한 줄</b> (2026-08-24 · 유저 지시:
        /// *"캐릭터 선택하면 나오는 ui 스킬 아래에 장착한 유물도 나오게 해줘"*).
        ///
        /// 스킬 세 줄 <b>아래</b>에 아이콘 + 이름을 그린다. 이름 색은 <b>등급 색</b>
        /// (<see cref="RelicDefinitionSO.GradeColor"/>)이라 «무슨 등급을 끼고 있나»가 한눈에 보인다 —
        /// 유물 관리 창의 목록과 <b>같은 규칙</b>이다.
        ///
        /// ★ <b>장착 열쇠는 캐릭터 정의 ID 다</b> — <see cref="RelicInventory.EquippedOn"/> 를
        ///   그대로 부른다. 여기서 «누가 무엇을 끼고 있나»를 따로 세지 않는다(장부가 둘이 되면
        ///   죽고 다시 난 캐릭터에서 두 창의 답이 갈린다).
        ///
        /// 아무것도 안 끼웠거나 캐릭터가 아니면 <b>줄을 통째로 숨긴다</b> — 빈 줄을 남기면
        /// 그 자리가 «비어 있다»가 아니라 «깨졌다»로 보인다(스킬 줄과 같은 판단).
        /// </summary>
        void ShowRelic(CharacterUnit character)
        {
            // ★★★ 2026-08-26 — <b>아이콘만</b> 보여준다 (유저 지시:
            //   *"유물 장착 인벤토리 3칸으로 변경 / 초상화UI(<b>아이콘만</b>)"*).
            //
            //   ⚠ <b>이름 칸을 지우지 않고 «끈다»</b>. 씬 오브젝트(<c>Relic/Label</c>)를
            //     없애면 옛 씬·옛 세이브와 어긋나고, 나중에 되돌리려면 좌표를 다시 실측해야
            //     한다. 값이 아니라 <b>표시 여부</b>로 다루는 것이 이 프로젝트의 관례다.
            //   ★ 이름이 사라져도 <b>등급을 잃지 않는다</b> — 유물 관리 창과 성장 창이
            //     여전히 등급 색으로 말한다. 이 카드에서는 «무엇을 몇 개 꼈나» 만 보이면 된다.
            if (_relicLabel != null && _relicLabel.gameObject.activeSelf)
                _relicLabel.gameObject.SetActive(false);

            if (_relicIcon != null && _relicStrip.Count == 0)
            {
                RelicInventory bootstrap = RelicInventory.Instance;
                _relicStrip.Build(_relicIcon, bootstrap != null ? bootstrap.EquipSlots : 3,
                                  relicIconStepPixels);
            }

            int worn = _relicStrip.Refresh(character);

            // 아무것도 안 끼웠거나 캐릭터가 아니면 <b>줄을 통째로 숨긴다</b> —
            // 빈 줄을 남기면 그 자리가 «비어 있다» 가 아니라 «깨졌다» 로 보인다.
            if (_relicRoot != null) _relicRoot.gameObject.SetActive(worn > 0);
        }

        // ------------------------------------------------------------------
        // 계속 변하는 칸 — 상태 · 체력
        //
        // ⚠ <b>카드 전체를 다시 그리지 않는다.</b> 이름·칭호·스킬은 선택이 바뀔 때만 바뀌는
        //   정적인 칸이라 <see cref="Show"/> 한 번으로 끝이고, 여기서는 <b>상태와 체력</b>만
        //   주기적으로 손본다(U-D10 — 미니맵처럼 갱신 주기를 둔다).
        // ------------------------------------------------------------------

        void Update()
        {
            if (_shown == null) return;
            if (Time.unscaledTime < _nextVolatileRefresh) return;
            _nextVolatileRefresh = Time.unscaledTime + volatileRefreshInterval;
            RefreshVolatile();
        }

        void RefreshVolatile()
        {
            if (_shown == null) return;

            // 죽어서 사라진 유닛을 계속 붙잡고 있지 않는다.
            if (!_shown.IsAlive) { Close(); return; }

            if (_stateText != null) _stateText.text = StateTextOf(_shown);

            // ★ 분노는 <b>체력 칸보다 먼저</b> 갱신한다 — 아래 체력 칸은 캐릭터일 때
            //   비활성이라 그 자리에서 return 하고, 그러면 분노가 영영 안 갱신된다.
            RefreshSkillCounter();

            // ★ 유물은 <b>창이 열려 있는 동안에도 바뀐다</b> — 유물 관리 창에서 끼우거나
            //   벗으면 이 카드가 즉시 따라와야 한다. 조회는 사전 한 번이라 주기 갱신으로 충분하다.
            ShowRelic(_shown as CharacterUnit);

            if (_hpRoot == null || !_hpRoot.gameObject.activeSelf) return;

            float ratio = Mathf.Clamp01(_shown.HpRatio);
            if (_hpFill != null)
            {
                UiFillBar.Prepare(_hpFill);      // 스프라이트가 비면 fillAmount 가 무시된다(UiFillBar 참조)
                _hpFill.fillAmount = ratio;
                _hpFill.color = Color.Lerp(barLow, barHigh, ratio);
            }
            // 소수점을 올린다 — 1% 도 안 남았는데 "HP: 0%" 로 보이면 이미 죽은 것처럼 읽힌다.
            if (_hpText != null)
                _hpText.text = string.Format(hpFormat, Mathf.CeilToInt(ratio * 100f));
        }

        /// <summary>
        /// 상태 칸. 목업의 "기절" 자리다.
        ///
        /// ★ <b>평상시엔 빈칸이다</b>(유저 확정 2026-08-19: "레벨 옆에 방어라고 뜨는거
        /// 없애줘 보스도 마찬가지로 이름 옆에 웨이브몬스터라고 뜨는 글씨 없애줘").
        ///
        /// 예전에는 여기에 <b>임무</b>(캐릭터: 탐험·방어…)나 <b>소속</b>(몬스터: 웨이브
        /// 몬스터·아군…)을 항상 채워 넣었는데, 둘 다 <b>거의 항상 같은 값</b>이라
        /// (캐릭터는 대부분 시간에 "방어", 몬스터는 항상 "웨이브 몬스터") 매번 같은 글자가
        /// 떠서 정보가 아니라 잡음이었다. <b>구속·정신 이상처럼 실제로 "지금 이상하다"고
        /// 알려줄 값이 있을 때만</b> 채우고, 없으면 빈칸으로 둔다 — 원래 상세 카드 4칸
        /// (칭호·이름·레벨·상태 / 칭호·이름·상태·체력) 요청에서 "상태"가 뜻한 것이
        /// 바로 이 <b>이상 상태</b>였다.
        ///
        /// ⚠ 이 값은 <b>어느 테이블 컬럼에서도 오지 않는다</b> — <c>CharacterBehavior.Duty</c>
        /// 는 씬의 행동 스크립트가 매 프레임 계산하는 런타임 상태이고, <c>Faction</c> 은
        /// 유닛 종류마다 코드가 고정해 두는 값이다. 그래서 이 변경에 맞춰 지울 표 컬럼이
        /// 없다(확인 완료).
        ///
        /// ★ 구속의 이름은 <b>스킬마다 다를 수 있다</b>(2026-08-19 신설,
        /// <see cref="BossSkillSO.StatusName"/>) — 아니사킬 「거대한 위협 포효」에 맞으면
        /// "기절", 말파스 「구속탄」에 맞으면 "구속" 이 뜬다. <see cref="UnitCombat.BoundLabel"/>
        /// 이 그 스킬이 마지막으로 건 이름을 들고 있으므로 여기서 하드코딩하지 않는다.
        /// 캐릭터가 실제로 맞는 순간 <see cref="UnitCombat.ApplyBind"/> 가 바로 갈아 끼우므로,
        /// 0.2초 주기 갱신(<see cref="volatileRefreshInterval"/>) 안에 화면에도 즉시 반영된다.
        /// </summary>
        static string StateTextOf(DamageableUnit unit)
        {
            if (unit == null) return string.Empty;

            var combat = unit.GetComponent<UnitCombat>();
            if (combat != null && combat.IsBound) return combat.BoundLabel;

            // ★ 2026-08-20 — 「중독」(베일 「담배 연기」). <b>구속 다음</b>이다:
            //   둘이 같이 걸릴 수 있고, 그때 «움직일 수 없다» 가 «독에 걸렸다» 보다
            //   플레이어가 먼저 알아야 하는 정보다(칸이 하나뿐이라 우선순위가 필요하다).
            if (combat != null && combat.IsPoisoned) return combat.PoisonLabel;

            if (unit is CharacterUnit character)
            {
                CharacterErosion erosion = CharacterErosion.Of(character);
                if (erosion != null && erosion.HasActive) return erosion.ActiveName;
            }

            return string.Empty;
        }

        // ------------------------------------------------------------------

        void EnsureBound()
        {
            if (_bound) return;
            _bound = true;

            // ★ 2026-08-17 구조 변경 — 액자(Art, RectMask2D) 안에 그림(Art/Sprite)이 들어간다.
            //   전술 지침·성장 창이 이미 쓰던 "Portrait/Sprite" 와 <b>같은 모양</b>으로 맞춘 것이다.
            //   꽉 채운 그림은 액자 밖으로 넘치므로 <b>가릴 층이 반드시 하나 더</b> 필요하다.
            //   예전 구조(그림이 곧 Art)로 되돌아간 씬에서도 죽지 않게 폴백을 둔다.
            _art = transform.Find("Art/Sprite") != null
                ? Find<Image>("Art/Sprite")
                : Find<Image>("Art");

            _nameText = Find<TMP_Text>("Name");
            _titleText = Find<TMP_Text>("Title");
            _noArtLabel = Find<TMP_Text>("NoArt");

            // ── 2026-08-19 목업에서 늘어난 칸들 ──────────────────────────────
            //   ⚠ <b>없어도 죽지 않는다.</b> 이 패널은 씬이 예전 상태로 되돌아가는 사고를
            //     두 번 겪은 프로젝트에 있으므로(28-3·28-4절), 새 칸은 전부 조용히 넘어가는
            //     선택 항목으로 둔다 — 옛 씬에서는 예전 카드 그대로 뜬다.
            _levelText = FindOptional<TMP_Text>("Level");
            _stateText = FindOptional<TMP_Text>("State");

            _skillsRoot = transform.Find("Skills");
            for (int slot = 0; slot < SkillSlots; slot++)
            {
                _skillSlots[slot] = transform.Find($"Skills/Slot{slot}");
                _skillIcons[slot] = FindOptional<Image>($"Skills/Slot{slot}/Icon");
                _skillLabels[slot] = FindOptional<TMP_Text>($"Skills/Slot{slot}/Label");
            }

            // ── 장착 유물 한 줄 — 2026-08-24 ────────────────────────────────
            //   ⚠ 위 ⚠ 와 같은 이유로 <b>없어도 죽지 않는다</b>.
            _relicRoot = transform.Find("Relic");
            _relicIcon = FindOptional<Image>("Relic/Icon");
            _relicLabel = FindOptional<TMP_Text>("Relic/Label");

            _hpRoot = transform.Find("Hp");
            _hpFill = FindOptional<Image>("Hp/HpBack/HpFill");
            _hpText = FindOptional<TMP_Text>("Hp/HpText");

            // ── 분노 게이지 (히스톤 「분노」 전용) — 2026-08-21 ─────────────────
            //   유저 지시: *"히스톤 두번째 스킬 해방 시 … 캐릭터 상세 UI에도 표기 …
            //   분노 빨간색 바는 분노 글씨 아래에 넣어서"*.
            //   ⚠ 위 ⚠ 와 같은 이유로 <b>없어도 죽지 않는다</b>.
            _rageRoot = transform.Find("Rage");
            _rageLabel = FindOptional<TMP_Text>("Rage/RageLabel");
            _rageBack = transform.Find("Rage/RageBack");
            _rageFill = FindOptional<Image>("Rage/RageBack/RageFill");
            UiFillBar.Prepare(_rageFill);
        }

        T Find<T>(string path) where T : Component
        {
            Transform t = transform.Find(path);
            if (t == null)
            {
                Debug.LogWarning($"[Portrait] 하이라키에서 '{path}' 를 찾지 못했습니다.", this);
                return null;
            }
            return t.GetComponent<T>();
        }

        /// <summary>있으면 쓰고 없으면 조용히 null — 새로 늘어난 칸에만 쓴다(위 주석 참조).</summary>
        T FindOptional<T>(string path) where T : Component
        {
            Transform t = transform.Find(path);
            return t != null ? t.GetComponent<T>() : null;
        }
    }
}
