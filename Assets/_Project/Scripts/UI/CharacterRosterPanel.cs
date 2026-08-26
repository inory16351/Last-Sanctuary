using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.CameraControl;
using LastSanctuary.Combat;
using LastSanctuary.Units;
using LastSanctuary.Wave;

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
    /// <b>죽어도 행이 즉시 사라지지 않는다</b> — 유저 요청: 사망한 캐릭터는 회색으로 표시해
    /// "죽었음"을 명확히 보여주고, 실제로 목록에서 지우는 건 웨이브가 끝난 뒤(<see cref="WaveManager.OnWaveEnded"/>)
    /// 로 미룬다. 그래서 캐릭터 목록(<see cref="_characters"/>)은 사망으로 줄어들지 않고
    /// <see cref="HandleWaveEnded"/> 에서만 정리된다. `CharacterUnit.OnDeath()` 가 오브젝트를
    /// 파괴하므로(<c>Destroy(gameObject)</c>), 죽는 순간(<see cref="DamageableUnit.OnDied"/>)에
    /// 이름·능력치를 미리 스냅샷해두고, 그 뒤로는 살아있는 멤버(Stats·transform 등)를 다시 읽지 않는다.
    ///
    /// 갱신은 세 갈래다:
    ///   - <b>HP 바</b>: <see cref="DamageableUnit.OnHpChanged"/> 를 행마다 구독해 <b>즉시</b> 반영한다.
    ///   - <b>사망 처리</b>: <see cref="DamageableUnit.OnDied"/> 를 행마다 구독해 <b>즉시</b> 회색으로 바꾼다.
    ///   - <b>그 외(이름·능력치·행동·선택 표시·강화 가능 여부)</b>: <see cref="refreshInterval"/> 마다
    ///     한 번씩, 살아있는 행만 — 매 프레임 전수 조회를 피하기 위한 것이다(준수사항 U-D10).
    ///
    /// <b>정렬(유저 요청)</b>: 목록은 항상 <b>현재 체력 %가 낮은 캐릭터가 위, 높은 캐릭터가
    /// 아래</b>로 정렬된다 — 지금 신경 써야 할 캐릭터가 스크롤 없이 바로 보이게 하기 위함이다.
    /// 사망한 캐릭터는 체력 0%지만 "지금 신경 쓸 대상"이 아니라서 예외로 **항상 맨 아래**에
    /// 둔다(살아있는 캐릭터들보다 뒤). <see cref="Row"/> 객체와 캐릭터의 실제 매칭
    /// (구독·데이터)은 전혀 안 바뀐다 — <see cref="ReorderRows"/> 가 화면에 보이는 **순서만**
    /// (`SetSiblingIndex`) 매 갱신마다 다시 계산한다.
    ///
    /// <b>체력바 — 철권식 잔상(유저 요청, 2차)</b>: 처음엔 <c>fillAmount</c> 자체를 목표치까지
    /// 서서히 줄였는데(그게 "실제로 깎이는" 느낌일 거라 봤다), 그러면 <b>맞는 순간에는 아무
    /// 변화가 없고</b> 막대가 뒤늦게 스르륵 줄어들 뿐이라 오히려 안 보인다는 피드백을 받았다
    /// ("깎인 부분이 없어지는 게 시각적으로 보여야 한다"). 지금은 두 겹이다 —
    /// <see cref="Row.HpFill"/> 은 실제 체력을 <b>즉시</b> 반영하고, 그 <b>뒤</b>에 깔린
    /// <see cref="Row.HpGhost"/> 가 맞기 직전 값을 잠깐 붙들었다가 서서히 사라진다
    /// (계산은 <see cref="HpGhostBar"/>). 능력치 표시
    /// (근접해서 보는 상세 스탯)와 캐릭터별 강화 버튼은 이 카드에서 뺐다 — 능력치 텍스트는
    /// 체력바가 커진 자리를 대신 채우고(숫자 % 를 더 잘 보이게), 강화는 추후 별도 UI로
    /// 다시 만든다는 전제로 제거했다(전체 강화는 HUD_Actions 의 기존 버튼으로 여전히 가능).
    /// </summary>
    public class CharacterRosterPanel : MonoBehaviour
    {
        [Header("하이라키 연결 (비워두면 자식에서 이름으로 찾는다)")]
        [Tooltip("행이 쌓이는 컨테이너 (VerticalLayoutGroup). 비우면 \"ScrollView/Viewport/List\"")]
        [SerializeField] RectTransform listRoot;

        [Tooltip("복제할 행의 원본. 비우면 자식 \"RowTemplate\". 비활성으로 둘 것")]
        [SerializeField] RectTransform rowTemplate;

        [Header("갱신")]
        [Tooltip("HP·행동·비용을 다시 읽는 주기(초). 0 이면 매 프레임")]
        [Min(0f)] [SerializeField] float refreshInterval = 0.2f;

        [Header("부대 묶음 (2026-08-24)")]
        [Tooltip("켜면 목록을 <b>부대 단위로 모아</b> 정렬하고, 같은 부대에 같은 색 테두리를 두른다.\n" +
                 "끄면 예전처럼 생성순으로만 늘어선다")]
        [SerializeField] bool groupBySquad = true;

        [Tooltip("부대 테두리 두께(px). 너무 두꺼우면 행 사이가 붙어 보인다")]
        [SerializeField] Vector2 squadOutlineThickness = new Vector2(2.5f, 2.5f);

        [Header("색")]
        [SerializeField] Color rowNormal = new Color(0.10f, 0.12f, 0.16f, 0.70f);
        [SerializeField] Color rowSelected = new Color(0.13f, 0.28f, 0.26f, 0.90f);

        [Tooltip("체력 막대 색이 이 비율에서 중간(노랑)이 되고, 위/아래로 초록/빨강 쪽으로 부드럽게 바뀐다. " +
                 "칸이 좁아 막대 길이만으로는 조금씩 줄어드는 게 눈에 잘 안 띄어서, 색으로도 남은 %를 " +
                 "가늠할 수 있게 한다(유저 피드백: 체력바가 게이지로 안 줄어드는 것처럼 보인다)")]
        [Range(0.01f, 0.99f)] [SerializeField] float lowHpRatio = 0.35f;

        [Header("사망 표시 (웨이브가 끝날 때까지 행을 남겨둔다)")]
        [Tooltip("사망한 캐릭터의 행 배경색")]
        [SerializeField] Color rowDead = new Color(0.08f, 0.08f, 0.09f, 0.55f);

        [Tooltip("★ 얼굴이 세로로 잘릴 때 어디를 남길지 — 1 이면 맨 위, 0.5 면 가운데.\n" +
                 "행의 칸은 84px 정사각이라 1(맨 위)이다 — 내리면 머리·후드·후광이 잘린다")]
        [Range(0f, 1f)] [SerializeField] float portraitVerticalAnchor = 1f;

        [Tooltip("★★ 얼굴을 얼마나 크게 볼지 — 1 이면 «액자를 채우는 최소 배율»(가슴까지 들어온다). " +
                 "1.4 면 보이는 영역이 420 → 300px 로 좁아진다.\n" +
                 "⚠ 15장을 실측해 고른 값이다 — 1.5 를 넘기면 왕관·모자가 높은 캐릭터" +
                 "(불칸·아루 새벽)의 얼굴이 칸 아래로 밀려난다. 앵커는 1(맨 위) 고정")]
        [Range(1f, 3f)] [SerializeField] float portraitZoom = 1.4f;

        [Tooltip("★ 얼굴을 액자의 어디에 놓을지 — 0.35 면 위에서 35% 지점. " +
                 "작을수록 얼굴이 위로 올라가고 어깨가 더 보인다(0.5 면 얼굴이 칸 정중앙)")]
        [Range(0.15f, 0.6f)] [SerializeField] float portraitFacePlacement = 0.35f;

        [Tooltip("죽은 캐릭터의 얼굴을 얼마나 어둡게 할지 — 1 이면 그대로, 0 이면 검정")]
        [Range(0f, 1f)] [SerializeField] float deadPortraitDim = 0.35f;

        // ★★★ 유물 칸 셋 (2026-08-26)
        [Tooltip("★ 유물 아이콘을 늘어놓는 간격(픽셀). <b>음수면 왼쪽으로</b> 자란다 — " +
                 "행의 아이콘은 얼굴 오른쪽 아래에 붙어 있어서 오른쪽으로 자라면 본문 글자를 " +
                 "덮는다. 0 이면 «아이콘 폭 + 2px» 를 쓴다")]
        [SerializeField] float relicIconStepPixels = -18f;

        [Tooltip("★ 부대를 «행 전체 색» 으로 말한다(2026-08-26 유저 확정).\n" +
                 "끄면 예전처럼 «테두리 선 + 왼쪽 띠» 로 돌아간다 — 다만 행 카드 그림이 들어온 뒤로 " +
                 "그 선은 카드 장식에 묻혀 잘 안 보인다")]
        [SerializeField] bool squadUsesRowTint = true;

        [Tooltip("★ 카드 «안쪽 홈» 에 까는 부대 색 판의 진하기(알파).\n" +
                 "판은 글자·얼굴 «아래» 에 깔리므로 진해도 이름·수치가 안 물든다 — " +
                 "0.5 쯤이면 여섯 색이 한눈에 갈린다")]
        [Range(0f, 1f)] [SerializeField] float squadTintAlpha = 0.5f;

        [Tooltip("사망한 캐릭터의 체력바 색. 비어서(투명) 안 보이는 것보다 " +
                 "꽉 찬 회색 막대가 '사망'을 훨씬 눈에 띄게 알려준다")]
        [SerializeField] Color deadBarColor = new Color(0.42f, 0.42f, 0.45f, 0.9f);

        [Tooltip("사망한 캐릭터의 이름 글자색")]
        [SerializeField] Color deadTextColor = new Color(0.5f, 0.5f, 0.52f, 1f);

        [Header("체력바 잔상 (철권식 — 깎인 구간이 눈에 보이게)")]
        [Tooltip("맞은 직후 '방금 깎인 구간'을 잔상 막대로 그대로 붙들고 있는 시간(초)")]
        [Min(0f)] [SerializeField] float ghostHoldSeconds = 0.35f;

        [Tooltip("붙들기가 끝난 뒤 잔상이 줄어드는 속도(비율/초). 1.0 = 가득 찬 막대가 1초에 다 빈다")]
        [Min(0.05f)] [SerializeField] float ghostDrainSpeed = 0.7f;

        [Tooltip("잔상 막대 색. 본 막대보다 밝고 붉은 쪽이 '방금 깎였다'로 잘 읽힌다")]
        [SerializeField] Color ghostColor = new Color(1f, 0.85f, 0.85f, 0.95f);

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

            /// <summary>
            /// ★ <b>부대 아웃라인</b>(2026-08-24). 행 배경에 붙은 <see cref="Outline"/> 이
            /// 같은 부대끼리 <b>같은 색 테두리</b>를 두른다. 부대가 없으면 알파 0 이라
            /// 컴포넌트는 있어도 <b>보이지 않는다</b> — 붙였다 뗐다 하면 레이아웃이 흔들린다.
            /// </summary>
            public Outline SquadOutline;

            /// <summary>
            /// ★★ <b>부대 색 띠</b>(2026-08-26 · 유저가 원화를 뽑아 줬다 — `Roster_SquadTab`).
            ///
            /// 행 <b>왼쪽 가장자리</b>에 세우는 7px 짜리 세로 띠다. 원화가 <b>회색조</b>라
            /// 여기서 부대 색을 <b>곱한다</b>(`Bar_Fill` 과 같은 규약).
            /// ★ <see cref="SquadOutline"/> 과 <b>같은 색</b>을 쓴다 — 둘이 갈리면 «테두리는
            ///   파란데 띠는 주황» 이 된다. 색을 고르는 곳은 <see cref="ApplySquadOutline"/> 한 곳이다.
            /// ⚠ 부대가 없으면 <b>알파 0</b> 이다(끄지 않는다) — 껐다 켜면 레이아웃이 흔들린다.
            /// </summary>
            public Image SquadTab;

            /// <summary>
            /// ★★ <b>부대 색 층</b>(2026-08-26). 카드 <b>위에</b> 한 겹 얹는 반투명 판이다 —
            /// 곱셈으로는 어두운 카드를 밝게 할 수 없어서 생겼다(<see cref="ApplySquadOutline"/> 의 ★★★).
            /// ⚠ 부대가 없으면 <b>알파 0</b> 이다(끄지 않는다 — 껐다 켜면 레이아웃이 흔들린다).
            /// </summary>
            public Image SquadTint;

            /// <summary>
            /// ★★ <b>얼굴</b>(2026-08-26). <c>Portrait</c>(60×60 · <see cref="RectMask2D"/>) 안의
            /// <c>PortraitArt</c> 다 — <see cref="PortraitFit"/>.Cover 가 «액자를 꽉 채우게»
            /// 키우므로 <b>넘치는 만큼을 잘라 줄 부모</b>가 필요하다. 그 부모가 마스크다.
            /// ⚠ 액자 그림(<c>PortraitFrame</c>)은 <b>형제</b>이고 <b>뒤 형제</b>라 얼굴 위에 그려진다 —
            ///   자식으로 두면 얼굴이 액자 테두리를 덮는다.
            /// </summary>
            public Image PortraitArt;

            /// <summary>얼굴 액자 그림. 얼굴이 없으면 같이 숨긴다(빈 액자만 남으면 «고장» 으로 보인다).</summary>
            public Image PortraitFrame;

            /// <summary>
            /// ★★ <b>장착한 유물 아이콘</b>(2026-08-26 · 유저 지시:
            /// *"캐릭터 로스터에 캐릭터가 장착하고 있는 유물 아이콘도 연동해서 넣어줘"*).
            /// 얼굴 <b>오른쪽 아래에 겹쳐</b> 놓는다 — 유물은 «그 캐릭터의 것» 이라 얼굴에
            /// 붙는 것이 뜻에 맞고, 본문 폭을 한 픽셀도 안 먹는다.
            /// ⚠ 없으면 <b>알파 0</b> 이다(끄지 않는다 — 껐다 켜면 레이아웃이 흔들린다).
            /// </summary>
            public Image RelicIcon;

            /// <summary>
            /// ★★★ 유물 칸이 셋이 되면서(2026-08-26) 아이콘도 셋이다.
            /// 씬에는 <see cref="RelicIcon"/> <b>하나만</b> 있고 나머지는
            /// <see cref="RelicIconStrip"/> 이 복제한다 — 그쪽 클래스 주석의 «왜 복제인가» 참조.
            /// </summary>
            public RelicIconStrip Relics;

            /// <summary>행을 꾹 누르면 캐릭터 성장 창을 여는 판정(유저 확정 2026-08-12).
            /// 모체(<c>RowTemplate</c>)에 붙어 있어서 복제되는 모든 행이 물려받는다.</summary>
            public UiLongPress LongPress;

            public TMP_Text Name;
            public TMP_Text Duty;
            public Image HpFill;

            /// <summary>본 막대 <b>뒤</b>에 깔리는 잔상 막대. 방금 깎인 구간을 잠깐 남긴다.</summary>
            public Image HpGhost;

            /// <summary>
            /// ★★ 보호막 막대 (2026-08-20 — 유저 지시: *"체력바에 실드는 하얀색으로 표현"*).
            ///
            /// <b>본 막대 뒤·잔상 앞</b>에 깔린다. 채움은 <b>(체력 + 보호막) ÷ 최대체력</b> 이라
            /// 체력 막대가 그 위를 덮고, <b>덮이지 않고 남은 흰 구간</b>이 곧 보호막이다.
            /// 막대 하나를 더 그리는 대신 «겹쳐서 남는 부분» 으로 표현하는 이 방법이
            /// <b>Unity 의 filled Image 로 중간부터 칠할 수 없다</b>는 제약을 피하는 정석이다.
            ///
            /// ⚠ 이 오브젝트는 <b>씬에 없다</b> — 행을 만들 때 잔상 막대를 복제해서 만든다
            ///   (<see cref="EnsureShieldBar"/>). 행 모체를 손보지 않아도 모든 행에 생긴다.
            /// </summary>
            public Image HpShield;

            public TMP_Text HpPercentLabel;

            /// <summary>침식 게이지(막대 + 숫자). 하이라키에 없으면 조용히 비활성 상태로 남는다.</summary>
            public readonly ErosionGaugeView Erosion = new ErosionGaugeView();

            /// <summary>살아있는 동안만 유효. 죽은 뒤에는 멤버를 다시 읽지 않는다(파괴된 오브젝트라서).</summary>
            public CharacterUnit Unit;

            /// <summary>지금 <see cref="HpHandler"/>/<see cref="DiedHandler"/> 를 구독하고 있는 대상.
            /// 행이 재활용되어 다른 캐릭터로 바뀔 때 이전 구독을 정확히 끊기 위해 <see cref="Unit"/> 과 따로 든다.</summary>
            public DamageableUnit SubscribedUnit;

            /// <summary>이 행에 고정된 핸들러들. 구독/해제에 매번 같은 델리게이트가 필요하다.</summary>
            public System.Action<int, int> HpHandler;
            public System.Action<DamageableUnit> DiedHandler;

            /// <summary>부활 콜백(<see cref="DamageableUnit.OnRevived"/>) — 「분노」(히스톤)가 쓴다.</summary>
            public System.Action<DamageableUnit> RevivedHandler;

            /// <summary>사망 확정 여부. true 가 되면 폴링 갱신(RefreshValues)에서 건드리지 않는다.</summary>
            public bool IsDead;

            /// <summary>죽기 직전에 찍어둔 이름 — 죽은 뒤에는 이 값만 쓴다(Unit.name 을 다시 못 읽어서).</summary>
            public string CachedName;

            /// <summary>실제 최신 체력 비율. <see cref="ApplyHp"/>(이벤트 콜백)가 즉시 갱신하고,
            /// 본 막대(<see cref="HpFill"/>)도 그 자리에서 바로 이 값으로 바뀐다.</summary>
            public float HpRatioTarget;

            /// <summary>잔상 막대 계산기. 깎인 구간을 잠깐 남겼다가 서서히 지운다.</summary>
            public readonly HpGhostBar Ghost = new HpGhostBar();
        }

        readonly List<Row> _rows = new List<Row>();

        /// <summary>로스터에 한 번이라도 올라온 캐릭터 전부. 죽어도 여기서 안 빠진다 —
        /// <see cref="HandleWaveEnded"/> 에서만 정리한다.</summary>
        readonly List<CharacterUnit> _characters = new List<CharacterUnit>();

        readonly List<CharacterUnit> _aliveScratch = new List<CharacterUnit>();

        /// <summary>이번 웨이브에서 죽은 캐릭터 집합. 웨이브가 끝나면 이 집합 기준으로 정리하고 비운다.</summary>
        readonly HashSet<CharacterUnit> _dead = new HashSet<CharacterUnit>();

        UnitSelector _selector;
        CameraRigController _cameraRig;
        WaveManager _waveManager;

        /// <summary>비활성 상태로도 찾아둔 성장 창 (<see cref="GrowthPanel"/> 참조).</summary>
        CharacterGrowthPanel _growthPanel;

        float _nextRefresh;

        /// <summary>
        /// 제목 칸(<c>Title</c>). <b>인원/상한</b>을 여기에 붙인다 (2026-08-21 · 유저 지시:
        /// *"캐릭터그리드에 캐릭터 뽑은 개수에 비례하여 8/12 이런식으로 표기"*).
        ///
        /// ★ 씬의 원래 문구(«캐릭터»)를 <b>지우지 않고 뒤에 숫자만 붙인다</b> —
        ///   <see cref="_titleBase"/> 에 첫 프레임의 문구를 담아 두고 매 갱신에 «{문구} n/상한»
        ///   으로 다시 만든다. 문구를 코드에 적으면 씬에서 이름을 바꿀 수 없게 된다.
        /// </summary>
        TMP_Text _titleLabel;
        string _titleBase;

        /// <summary>씬에 박혀 있던 제목 문구 — 표에 키가 없을 때 되돌아갈 값.</summary>
        string _titleSceneText;

        /// <summary>제목 머리말의 스트링 키(«캐릭터» / «Characters»).</summary>
        const string TitleKey = "ui_roster_title";

        /// <summary>상한을 아는 쪽. 없으면 숫자만(«n») 쓴다 — 지어낸 상한을 그리지 않는다.</summary>
        CharacterCreationService _creation;

        void Start()
        {
            _selector = UnitSelector.Instance;
            _cameraRig = FindAnyObjectByType<CameraRigController>();
            _waveManager = FindAnyObjectByType<WaveManager>();

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

            // 제목 — 첫 문구를 기억해 두고 뒤에 «인원/상한» 만 붙인다(위 _titleLabel 주석).
            // ★★ 2026-08-26 — <b>씬의 문구는 «폴백» 으로만 쓴다.</b> 예전에는 씬의 «캐릭터» 를
            //   그대로 머리말로 삼았기 때문에, 언어를 영어로 바꿔도 <b>제목만 한국어로 남았다</b>
            //   (매 갱신에 이 머리말로 제목을 다시 지으므로 다른 곳에서 고쳐도 되돌아간다).
            //   표에 키가 없으면 씬 문구가 그대로 쓰이니 화면은 지금과 같다.
            _titleLabel = transform.Find("Title")?.GetComponent<TMP_Text>();
            _titleSceneText = _titleLabel != null ? _titleLabel.text : null;
            _titleBase = _titleSceneText != null
                ? Data.StringTable.Get(TitleKey, _titleSceneText)
                : null;

            // 언어가 바뀌면 머리말을 다시 읽는다 — 제목은 매 갱신에 다시 만들어지므로
            // 여기서 머리말만 갈아 두면 다음 갱신에 저절로 따라온다.
            Data.StringTable.OnLanguageChanged -= HandleLanguageChanged;
            Data.StringTable.OnLanguageChanged += HandleLanguageChanged;

            if (_waveManager != null) _waveManager.OnWaveEnded += HandleWaveEnded;

            AppendNewCharacters();
            RefreshTitle();
        }

        /// <summary>
        /// 제목을 «{문구} 인원/상한» 으로 다시 쓴다 (2026-08-21).
        ///
        /// ★ <b>세는 값은 «지금 자리를 차지한 인원»</b>(<see cref="CharacterCreationService.AliveCount"/>)
        ///   이다 — 상한을 막는 값과 <b>같은 값</b>이어야 «12/12 인데 왜 못 만드나» 가 안 생긴다.
        ///   그래서 로스터의 행 수(죽은 카드까지 남아 있다)를 쓰지 않는다.
        /// ⚠ 생성 서비스가 씬에 없으면 <b>분모를 그리지 않는다</b> — 상한을 지어내지 않는다.
        /// </summary>
        void RefreshTitle()
        {
            if (_titleLabel == null) return;
            if (_creation == null) _creation = CharacterCreationService.Instance;

            string head = string.IsNullOrEmpty(_titleBase) ? string.Empty : _titleBase + " ";
            if (_creation == null)
            {
                _titleLabel.text = head.TrimEnd();
                return;
            }

            int max = _creation.MaxCharacters;
            _titleLabel.text = max > 0
                ? $"{head}{_creation.AliveCount}/{max}"
                : $"{head}{_creation.AliveCount}";
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
            if (_waveManager != null) _waveManager.OnWaveEnded -= HandleWaveEnded;
            Data.StringTable.OnLanguageChanged -= HandleLanguageChanged;
            UnsubscribeAll();
        }

        /// <summary>
        /// 언어가 바뀌면 <b>제목 머리말만</b> 다시 읽는다. 행의 글자(이름·역할·상태)는
        /// 0.2초마다 도는 갱신이 표에서 다시 읽으므로 여기서 건드릴 것이 없다.
        /// </summary>
        void HandleLanguageChanged()
        {
            if (_titleSceneText != null)
                _titleBase = Data.StringTable.Get(TitleKey, _titleSceneText);
            RefreshTitle();
        }

        void UnsubscribeAll()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                if (row.SubscribedUnit == null) continue;
                row.SubscribedUnit.OnHpChanged -= row.HpHandler;
                row.SubscribedUnit.OnDied -= row.DiedHandler;
                row.SubscribedUnit.OnRevived -= row.RevivedHandler;
            }
        }

        void Update()
        {
            // 잔상은 폴링 주기(refreshInterval)와 무관하게 매 프레임 진행해야 부드럽다 —
            // 0.2초 단위로 뚝뚝 끊기면 연출로서 의미가 없다.
            AnimateGhostBars(Time.unscaledDeltaTime);

            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;

            if (_selector == null) _selector = UnitSelector.Instance;
            if (_waveManager == null)
            {
                _waveManager = FindAnyObjectByType<WaveManager>();
                if (_waveManager != null) _waveManager.OnWaveEnded += HandleWaveEnded;
            }

            PurgeVanishedCharacters();   // ★ 새로 붙이기 «전에» 사라진 것을 뺀다 (아래 ★★)
            AppendNewCharacters();
            RefreshValues();
            ReorderRows();
            RefreshTitle();
        }

        /// <summary>
        /// ★★ <b>«죽지 않았는데 사라진» 캐릭터의 행을 지운다</b> (2026-08-21 3차 · 유저 리포트:
        /// *"로비화면에서 저장하고 이어하기를 눌렀을 때 캐릭터 그리드에 아무런 상호작용이 되지
        /// 않는 캐릭터 UI 3개가 나온다"*).
        ///
        /// <b>왜 3개인가</b> — 게임 씬은 열리자마자 <b>시작 캐릭터 3명</b>을 자동으로 세운다
        /// (진행상황 80절). 이 패널의 <see cref="Start"/> 가 그 셋을 <see cref="_characters"/>
        /// 에 담고 행을 만든다. 그 <b>다음 프레임</b>에 <c>GameSnapshot.RestoreNextFrame</c> 이
        /// 돌면서 <c>UnitSpawner.DestroySpawnedCharactersForRestore()</c> 가 셋을
        /// <b>통째로 파괴</b>하고 저장된 인원을 새로 세운다.
        ///
        /// 그런데 이 목록은 <b>죽어야만</b> 줄어든다(<see cref="HandleWaveEnded"/>) — 파괴된
        /// 셋은 <see cref="DamageableUnit.OnDied"/> 를 <b>부르지 않고</b> 사라지므로
        /// <see cref="_dead"/> 에도 안 들어간다. 그래서 셋은 목록에 영원히 남고,
        /// <see cref="RefreshValues"/> 는 <c>row.Unit == null</c> 이라 <b>건너뛰기만</b> 한다 —
        /// 마지막으로 그려진 이름·체력바가 <b>그대로 굳은</b> 행 세 개가 남는다. 눌러도
        /// 아무 일이 없는 이유는 <see cref="Row.Unit"/> 이 파괴된 오브젝트라서다.
        ///
        /// → <b>파괴됐는데 <see cref="_dead"/> 에 없으면 «불러오기로 갈아엎힌 것»</b> 이다.
        ///   그 하나로 «죽어서 회색으로 남겨둔 카드»(웨이브가 끝날 때까지 남아야 한다)와
        ///   확실히 갈린다 — 죽은 캐릭터는 파괴되기 <b>전에</b> <see cref="_dead"/> 에 들어간다.
        ///
        /// ⚠ 불러오기 전용으로 만들지 않았다. «판을 갈아엎는» 경로가 또 생겨도(재시작 등)
        ///   이 한 곳이 알아서 정리한다.
        /// </summary>
        void PurgeVanishedCharacters()
        {
            int before = _characters.Count;
            _characters.RemoveAll(HasVanished);
            if (_characters.Count == before) return;

            ReassignAllRows();
        }

        /// <summary>파괴됐지만 <b>죽어서</b> 파괴된 것이 아닌가 (위 ★★).</summary>
        bool HasVanished(CharacterUnit unit) => unit == null && !_dead.Contains(unit);

        // ------------------------------------------------------------------
        // 캐릭터 목록 — 죽어도 안 줄어든다. 웨이브 종료 때만 정리한다.
        // ------------------------------------------------------------------

        /// <summary>지금 살아있는 캐릭터를 모은다 — "새로 생긴 캐릭터"를 찾는 용도로만 쓴다.</summary>
        void CollectAliveCharacters(List<CharacterUnit> into)
        {
            into.Clear();
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] is CharacterUnit c && c.IsAlive) into.Add(c);
        }

        /// <summary>아직 로스터에 없는 살아있는 캐릭터를 찾아 뒤에 추가하고 행을 물린다.</summary>
        void AppendNewCharacters()
        {
            CollectAliveCharacters(_aliveScratch);

            bool added = false;
            for (int i = 0; i < _aliveScratch.Count; i++)
            {
                CharacterUnit c = _aliveScratch[i];
                if (_characters.Contains(c)) continue;
                _characters.Add(c);
                added = true;
            }
            if (!added) return;

            while (_rows.Count < _characters.Count)
                _rows.Add(CreateRow(_rows.Count));

            for (int i = 0; i < _characters.Count; i++)
            {
                Row row = _rows[i];
                if (!row.Root.activeSelf) row.Root.SetActive(true);
                if (ReferenceEquals(row.Unit, _characters[i])) continue;   // 이미 이 캐릭터를 보여주는 중
                BindRowToUnit(row, _characters[i]);
            }
        }

        /// <summary>웨이브가 끝나면 죽은 캐릭터를 로스터에서 실제로 지운다(유저 요청).</summary>
        void HandleWaveEnded(int wave)
        {
            if (_dead.Count == 0) return;

            _characters.RemoveAll(c => _dead.Contains(c));
            _dead.Clear();

            ReassignAllRows();
        }

        /// <summary>죽은 캐릭터가 빠져 인덱스가 밀렸을 수 있으니 행 전체를 다시 배정한다.</summary>
        void ReassignAllRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                bool used = i < _characters.Count;

                if (!used)
                {
                    if (row.SubscribedUnit != null)
                    {
                        row.SubscribedUnit.OnHpChanged -= row.HpHandler;
                        row.SubscribedUnit.OnDied -= row.DiedHandler;
                        row.SubscribedUnit.OnRevived -= row.RevivedHandler;
                        row.SubscribedUnit = null;
                    }
                    row.Unit = null;
                    row.IsDead = false;
                    if (row.Root.activeSelf) row.Root.SetActive(false);
                    continue;
                }

                if (!row.Root.activeSelf) row.Root.SetActive(true);

                CharacterUnit newUnit = _characters[i];
                if (ReferenceEquals(row.Unit, newUnit)) continue;   // 죽지 않고 그대로 남은 자리

                if (row.SubscribedUnit != null)
                {
                    row.SubscribedUnit.OnHpChanged -= row.HpHandler;
                    row.SubscribedUnit.OnDied -= row.DiedHandler;
                    row.SubscribedUnit.OnRevived -= row.RevivedHandler;
                }
                BindRowToUnit(row, newUnit);
            }
        }

        /// <summary>행에 캐릭터를 물리고 구독을 건다. "살아있음" 상태로 시각을 되돌린다(재활용 대비).</summary>
        void BindRowToUnit(Row row, CharacterUnit unit)
        {
            row.Unit = unit;
            row.SubscribedUnit = unit;
            row.IsDead = false;

            unit.OnHpChanged += row.HpHandler;
            unit.OnDied += row.DiedHandler;
            unit.OnRevived += row.RevivedHandler;

            // 재구성/재활용 직후엔 잔상도 애니메이션 없이 바로 스냅한다 — 안 그러면 새로 물린
            // 캐릭터의 잔상이 이전 캐릭터 값에서부터 줄어드는 것처럼 보인다.
            row.HpRatioTarget = unit.MaxHp > 0 ? (float)unit.CurrentHp / unit.MaxHp : 0f;
            row.Ghost.HoldSeconds = ghostHoldSeconds;
            row.Ghost.DrainPerSecond = ghostDrainSpeed;
            row.Ghost.Snap(row.HpRatioTarget);
            ApplyDisplayedHp(row);

            ApplyAliveAppearance(row);
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
                LongPress = clone.GetComponent<UiLongPress>(),
                Name = FindText(clone, "Name"),
                Duty = FindText(clone, "Duty"),
                SquadTab = FindImage(clone, "SquadTab"),
                SquadTint = FindImage(clone, "SquadTint"),
                PortraitFrame = FindImage(clone, "PortraitFrame"),
                RelicIcon = FindImage(clone, "RelicIcon"),
            };

            // ★ 유물 칸 수는 <b>장부가 정본</b>이다(RelicInventory.EquipSlots) — 여기에 3 을
            //   박아 두면 인스펙터에서 칸을 늘렸을 때 로스터만 못 따라온다.
            if (row.RelicIcon != null)
            {
                row.Relics = new RelicIconStrip();
                var inv = Relics.RelicInventory.Instance;
                row.Relics.Build(row.RelicIcon, inv != null ? inv.EquipSlots : 3,
                                 relicIconStepPixels);
            }

            // ★★ <b>부대 색 판은 «맨 아래» 로 내린다</b> (2026-08-26 · 2차).
            //   유니티 UI 는 형제 순서대로 그리므로 <b>첫째 자식 = 가장 먼저 = 제일 뒤</b> 다.
            //   판이 마지막 자식이면 이름·체력바·얼굴 <b>위에</b> 깔려 글자가 물든다 —
            //   그래서 알파를 0.22 로 묶어 둘 수밖에 없었고, 그러니 색이 안 보였다.
            //   맨 아래로 내리면 <b>카드 위·내용 아래</b> 라 진하게 칠해도 안전하다.
            // ⚠ 위치는 씬에서 «카드 안쪽 홈» 에 맞춰 잡았다 — 여기서 칸을 건드리지 않는다.
            if (row.SquadTint != null)
            {
                row.SquadTint.raycastTarget = false;
                row.SquadTint.transform.SetAsFirstSibling();
            }

            Transform portrait = clone.Find("Portrait");
            if (portrait != null) row.PortraitArt = FindImage(portrait, "PortraitArt");

            // ★ 액자 그림도 코드가 꽂는다(SquadTab 과 같은 이유 — MCP 가 Sprite 를 못 넣는다).
            if (row.PortraitFrame != null && row.PortraitFrame.sprite == null)
            {
                Sprite frame = Resources.Load<Sprite>(PortraitFrameResource);
                if (frame != null)
                {
                    row.PortraitFrame.sprite = frame;
                    row.PortraitFrame.type = Image.Type.Sliced;
                }
            }

            // ★★ 부대 색 띠 — 그림은 <b>코드가 꽂는다</b>(2026-08-26).
            //   MCP 로는 씬 오브젝트에 Sprite 참조를 넣을 수 없다(8절 1번) — 모체에는
            //   빈 <see cref="Image"/> 만 두고 여기서 <c>Resources</c> 로 읽는다.
            //   <c>RallyFlag</c>·<c>CombatProjectileFx</c> 가 쓰는 그 방식이다.
            if (row.SquadTab != null && row.SquadTab.sprite == null)
            {
                Sprite tab = Resources.Load<Sprite>(SquadTabResource);
                if (tab != null) { row.SquadTab.sprite = tab; row.SquadTab.type = Image.Type.Sliced; }
                else if (!_squadTabWarned)
                {
                    _squadTabWarned = true;
                    Debug.LogWarning($"[로스터] {SquadTabResource} 를 찾지 못했습니다 — " +
                                     "부대 색 띠가 안 보입니다.", this);
                }
            }

            // ★ <b>이름이 칸을 넘지 않게</b> (2026-08-25 · 유저 지시: *"캐릭터 로스터에
            //   텍스트 짤리는거 수정"*).
            //
            //   잘리던 주된 원인은 <b>행의 배치</b>였고 그건 씬에서 고쳤다 — 이름 칸이
            //   «맡은 일» 칸과 6px 겹치고, 체력바가 이름 아래를 8px 파고들고 있었다.
            //   이건 그 위의 <b>안전망</b>이다: 이름은 «<c>이름</c> Lv.<c>N</c>»
            //   (<see cref="NameTextOf"/>) 이라 <b>이름이 길면 여전히 넘칠 수 있다</b>.
            //
            // ★ <b>잘라내지 않고 «작게» 맞춘다</b> — 뒤를 자르면 레벨이 사라져서
            //   «누가 더 컸는지» 를 못 본다(<see cref="HudTheme.FitText"/> 의 설계 이유 그대로).
            // ⚠ 한 줄짜리 띠라 <b>줄바꿈은 끈다</b>(wrap: false) — 켜면 두 번째 줄이
            //   체력바 위로 흘러내린다.
            HudTheme.FitText(row.Name, minSize: 12f, wrap: false);
            HudTheme.FitText(row.Duty, minSize: 11f, wrap: false);

            // ★ 아웃라인은 <b>코드가 붙인다</b> — 행 모체(RowTemplate)를 손대지 않아도
            //   모든 행에 생긴다(보호막 막대 EnsureShieldBar 와 같은 방식).
            if (row.Background != null)
            {
                row.SquadOutline = row.Background.GetComponent<Outline>();
                if (row.SquadOutline == null)
                    row.SquadOutline = row.Background.gameObject.AddComponent<Outline>();
                row.SquadOutline.effectDistance = squadOutlineThickness;
                row.SquadOutline.useGraphicAlpha = false;
                row.SquadOutline.effectColor = Color.clear;
            }

            Transform hpBack = clone.Find("HpBack");
            if (hpBack != null)
            {
                Transform fill = hpBack.Find("HpFill");
                if (fill != null) row.HpFill = fill.GetComponent<Image>();

                // 잔상은 본 막대 뒤에 그려져야 하므로 하이라키에서 HpFill 보다 앞 형제여야 한다.
                Transform ghost = hpBack.Find("HpGhost");
                if (ghost != null) row.HpGhost = ghost.GetComponent<Image>();

                Transform percentLabel = hpBack.Find("HpPercentLabel");
                if (percentLabel != null) row.HpPercentLabel = percentLabel.GetComponent<TMP_Text>();

                // 스프라이트가 비어 있으면 fillAmount 가 아예 무시되어 막대가 렉트 전체로
                // 칠해진다(색만 바뀌고 길이는 안 변한다) — UiFillBar 문서 참조.
                UiFillBar.Prepare(row.HpFill, row.HpGhost);
                EnsureShieldBar(row);
            }

            // 침식 게이지는 체력바와 형제로 둔다(HpBack 아래) — 체력이 보이는 곳엔 침식도
            // 같이 보여야 한다는 요구(유저 확정)를 행 단위로 만족시킨다.
            row.Erosion.Bind(clone, "ErosionBack");

            // 람다가 row 를 잡아두므로 행이 다른 캐릭터로 바뀌어도 항상 지금 물린 캐릭터를 쓴다.
            if (row.SelectButton != null)
                row.SelectButton.onClick.AddListener(() => SelectRow(row));

            // 꾹 누르면 성장 창 (유저 확정 2026-08-12) — 짧게 누르면 위의 onClick 이 그대로 돈다.
            if (row.LongPress != null)
                row.LongPress.OnLongPress += () => OpenGrowthFor(row);

            // 핸들러도 같은 이유로 row 를 닫아 두고 한 번만 만든다 — 구독/해제할 때마다 새
            // 델리게이트를 만들면 구독 해제(-=)가 다른 인스턴스를 지우려다 실패한다
            // (C# 이벤트는 참조가 같아야 -= 가 먹는다).
            row.HpHandler = (current, max) => ApplyHp(row, current, max);
            row.DiedHandler = unit => HandleUnitDied(row, unit);
            row.RevivedHandler = unit => HandleUnitRevived(row, unit);

            return row;
        }

        /// <summary>
        /// <see cref="DamageableUnit.OnHpChanged"/> 구독 콜백. <b>본 막대는 여기서 즉시 바뀐다.</b>
        ///
        /// 예전에는 <c>fillAmount</c> 를 목표치까지 서서히 줄였는데(그게 "실제로 깎이는" 느낌을
        /// 줄 거라 봤다), 그러면 <b>맞는 순간에는 아무 변화가 없고</b> 뒤늦게 스르륵 줄어들 뿐이라
        /// 오히려 안 보인다는 피드백을 받았다. 지금은 격투 게임식으로 갈랐다 — 본 막대는 즉시
        /// 떨어지고, "방금 깎인 구간"은 뒤에 깔린 잔상 막대(<see cref="Row.HpGhost"/>)가 잠깐
        /// 남겼다가 지운다.
        /// </summary>
        void ApplyHp(Row row, int current, int max)
        {
            if (row.IsDead) return;   // 사망 처리 후에는 건드리지 않는다

            row.HpRatioTarget = max > 0 ? (float)current / max : 0f;
            row.Ghost.SetActual(row.HpRatioTarget);
            ApplyDisplayedHp(row);
        }

        /// <summary>
        /// 잔상 막대만 매 프레임 진행시킨다(본 막대는 이미 즉시 반영돼 있다).
        /// 폴링 주기와 무관하게 <see cref="Update"/> 맨 앞에서 호출한다.
        /// </summary>
        void AnimateGhostBars(float dt)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                if (row.IsDead || row.HpGhost == null) continue;

                if (row.Ghost.Tick(row.HpRatioTarget, dt))
                    row.HpGhost.fillAmount = row.Ghost.Value;
            }
        }

        /// <summary>보호막 막대의 색. 유저 지시로 <b>흰색</b>이다.</summary>
        static readonly Color ShieldColor = new Color(1f, 1f, 1f, 0.92f);

        /// <summary>
        /// 보호막 막대를 <b>잔상 막대를 복제해서</b> 만든다 (없으면).
        ///
        /// <b>왜 씬에 안 만들고 복제하나</b> — 행은 모체 하나를 복제해 쓰는 구조라
        /// (<see cref="rowTemplate"/>) 모체에 오브젝트를 하나 넣으면 될 것 같지만, 그러면
        /// 씬 파일을 고쳐야 하고 <b>모체가 비활성</b>이라 MCP 로 찾기도 어렵다.
        /// 잔상 막대는 이미 «본 막대 뒤에 깔린 같은 모양» 이라 복제 원본으로 딱 맞는다 —
        /// 크기·앵커·스프라이트·fillMethod 가 전부 맞춰져 있다.
        ///
        /// 형제 순서: 잔상 → <b>보호막</b> → 본 막대. 뒤에서 앞으로 그려지므로 본 막대가
        /// 보호막을 덮고, 체력을 넘는 구간만 흰색으로 남는다.
        /// </summary>
        void EnsureShieldBar(Row row)
        {
            if (row.HpShield != null || row.HpGhost == null || row.HpFill == null) return;

            Image clone = Instantiate(row.HpGhost, row.HpGhost.transform.parent);
            clone.name = "HpShield";
            clone.color = ShieldColor;
            clone.raycastTarget = false;
            clone.fillAmount = 0f;

            // 잔상 바로 뒤(= 위)에 두고, 본 막대는 그보다 앞에 있어야 한다.
            clone.transform.SetSiblingIndex(row.HpGhost.transform.GetSiblingIndex() + 1);
            if (row.HpFill.transform.GetSiblingIndex() <= clone.transform.GetSiblingIndex())
                row.HpFill.transform.SetAsLastSibling();

            row.HpShield = clone;
        }

        /// <summary>지금 실제 체력 비율 그대로 막대 채움·색·숫자 %를 그린다.</summary>
        void ApplyDisplayedHp(Row row)
        {
            if (row.HpFill != null)
            {
                row.HpFill.fillAmount = row.HpRatioTarget;
                row.HpFill.color = HpGaugeColor(row.HpRatioTarget);
            }

            if (row.HpGhost != null)
            {
                row.HpGhost.fillAmount = row.Ghost.Value;
                row.HpGhost.color = ghostColor;
            }

            // ★ 보호막 — (체력 + 보호막) 까지 흰색으로 채운다. 본 막대가 그 위를 덮으므로
            //   화면에는 «체력 끝에서 보호막 끝까지» 만 하얗게 남는다(위 EnsureShieldBar).
            if (row.HpShield != null)
            {
                int shield = row.Unit != null ? row.Unit.Shield : 0;
                int max = row.Unit != null ? row.Unit.MaxHp : 0;
                row.HpShield.fillAmount = shield > 0 && max > 0
                    ? Mathf.Clamp01(row.HpRatioTarget + (float)shield / max)
                    : 0f;
            }

            // 막대 길이만으로는 몇 % 줄었는지 눈으로 정확히 재기 어렵다는 피드백 —
            // 현재 체력을 0~100% 정수로 환산해 막대 위에 숫자로도 그대로 보여준다.
            if (row.HpPercentLabel != null)
                row.HpPercentLabel.text = $"{Mathf.RoundToInt(row.HpRatioTarget * 100f)}%";
        }

        /// <summary>
        /// 체력 비율에 맞춰 초록 → 노랑 → 빨강으로 부드럽게 보간한다.
        /// 막대 자체는 항상 <see cref="ApplyHp"/> 에서 fillAmount 로 줄어들고 있었지만,
        /// 로스터 칸이 좁아 몇 % 줄어든 게 눈에 잘 안 띄어서 "그냥 맞을 때 빨갛게 반짝인다"로
        /// 보인다는 피드백이 있었다 — 색으로도 남은 %를 가늠할 수 있게 3단 그라디언트로 바꿨다.
        /// </summary>
        Color HpGaugeColor(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            float mid = lowHpRatio;
            return ratio >= mid
                ? Color.Lerp(HudTheme.BarHpMid, HudTheme.BarHp, (ratio - mid) / (1f - mid))
                : Color.Lerp(HudTheme.BarHpLow, HudTheme.BarHpMid, ratio / mid);
        }

        /// <summary>
        /// 사망 확정. <see cref="DamageableUnit.OnDied"/> 구독 콜백 — <c>CharacterUnit.OnDeath()</c>
        /// 가 <c>Destroy(gameObject)</c> 를 부른 바로 다음에 같은 프레임에서 호출된다. Unity 의
        /// Destroy 는 프레임 끝에 처리되므로(DamageableUnit.ApplyDamage 주석 참조) <b>지금은 아직</b>
        /// <c>row.Unit</c> 의 멤버를 안전하게 읽을 수 있는 마지막 순간이다 — 표시에 필요한 값을
        /// 전부 스냅샷해두고, 이후로는 다시 읽지 않는다.
        /// </summary>
        void HandleUnitDied(Row row, DamageableUnit deadUnit)
        {
            if (row.IsDead) return;
            row.IsDead = true;

            if (row.Unit != null)
            {
                _dead.Add(row.Unit);
                row.CachedName = NameTextOf(row.Unit);
            }

            ApplyDeadAppearance(row);

            // 선택 대상에서 확실히 빼둔다. UnitSelector 도 다음 프레임에 스스로 선택을
            // 놓지만(죽은 유닛은 IsAlive 가 false), 그 전에 행이라도 눌리지 않게 즉시 막는다.
            if (row.SelectButton != null) row.SelectButton.interactable = false;

            // 사망은 맨 아래로 내려가야 하는 순서 변경이라, 다음 폴링(최대 refreshInterval)
            // 까지 기다리지 않고 그 자리에서 바로 다시 정렬한다.
            ReorderRows();
        }

        /// <summary>
        /// ★ <b>부활</b> (<see cref="DamageableUnit.OnRevived"/> 구독 콜백) —
        /// 「분노」(히스톤 80014)가 쓰러진 캐릭터를 되살렸다.
        ///
        /// <see cref="HandleUnitDied"/> 를 <b>정확히 되감는다</b>: 회색 표시를 지우고,
        /// 웨이브 종료 시 지울 목록(<see cref="_dead"/>)에서 빼고, 행을 다시 누를 수 있게 한다.
        /// 이 되감기가 없으면 <b>멀쩡히 살아 움직이는 캐릭터가 로스터에서는 '사망'으로 남고</b>
        /// 웨이브가 끝날 때 목록에서 사라진다.
        ///
        /// 이 캐릭터는 파괴되지 않았으므로 <see cref="_characters"/> 에 그대로 들어 있다 —
        /// 목록을 다시 만들 필요가 없고, 행도 그대로 쓴다.
        /// </summary>
        void HandleUnitRevived(Row row, DamageableUnit unit)
        {
            if (!row.IsDead) return;
            row.IsDead = false;

            if (row.Unit != null) _dead.Remove(row.Unit);

            ApplyAliveAppearance(row);
            if (row.SelectButton != null) row.SelectButton.interactable = true;

            // 사망 표시가 막대를 1(꽉 참)로 못박아 두었으므로 실제 체력으로 되돌린다.
            // 잔상도 같이 스냅한다 — 안 그러면 회색 막대가 서서히 줄어드는 것처럼 보인다.
            if (unit != null)
            {
                row.HpRatioTarget = unit.MaxHp > 0 ? (float)unit.CurrentHp / unit.MaxHp : 0f;
                row.Ghost.Snap(row.HpRatioTarget);
                ApplyDisplayedHp(row);
            }

            ReorderRows();
        }

        /// <summary>죽은 캐릭터의 행을 회색으로 — "확실하게 죽었다"는 걸 알아볼 수 있게 한다.</summary>
        void ApplyDeadAppearance(Row row)
        {
            // ★★ 2026-08-26 — <b>색을 직접 칠하지 않고 <see cref="HudTheme.PaintButton"/> 을 지난다.</b>
            //   행에 그림(`Btn_Roster_*`)이 깔리면서, 어두운 색을 그대로 칠하면 그 색이
            //   그림에 <b>곱해져</b> 새까매진다 — 그 함수가 «그림을 넣었는데 안 보인다» 로
            //   적어 둔 바로 그 사고다. 그림이 없으면 예전처럼 이 색을 칠한다.
            HudTheme.PaintButton(row.Background, ButtonState.Off, rowDead);

            // 죽으면 부대에서 빠진다(SquadService 가 OnAnyDied 로 정리한다) — 테두리도 지운다.
            // ⚠ 부대 색 띠도 <b>같이</b> 지운다 — 안 지우면 사망 행에만 색 띠가 남아
            //   «죽었는데 아직 부대원» 으로 읽힌다.
            if (row.SquadOutline != null) row.SquadOutline.effectColor = Color.clear;
            if (row.SquadTab != null) row.SquadTab.color = Color.clear;
            if (row.SquadTint != null) row.SquadTint.color = Color.clear;

            // ★ 얼굴은 <b>지우지 않고 어둡게</b> 한다 — 누가 죽었는지는 얼굴로 알아보는 것이
            //   가장 빠르다. 액자는 그대로 두어 «칸이 비었다» 로 보이지 않게 한다.
            if (row.PortraitArt != null && row.PortraitArt.sprite != null)
                row.PortraitArt.color = new Color(deadPortraitDim, deadPortraitDim, deadPortraitDim, 1f);
            if (row.Name != null) { row.Name.text = row.CachedName; row.Name.color = deadTextColor; }
            if (row.Duty != null)
            {
                row.Duty.text = Data.StringTable.Get("ui_duty_dead", "사망");
                row.Duty.color = deadTextColor;
            }

            // 비어서(투명) 안 보이는 것보다, 꽉 찬 회색 막대가 "사망"을 훨씬 눈에 띄게 알려준다.
            // 죽음은 연출로 서서히 보여줄 상태가 아니라 즉시 확정이라 여기서 바로 맞춘다
            // (AnimateGhostBars 는 어차피 IsDead 행을 건너뛴다).
            row.HpRatioTarget = 1f;
            row.Ghost.Snap(1f);
            if (row.HpFill != null)
            {
                row.HpFill.fillAmount = 1f;
                row.HpFill.color = deadBarColor;
            }
            if (row.HpGhost != null) row.HpGhost.fillAmount = 0f;   // 잔상은 사망 표시에 방해만 된다
            if (row.HpPercentLabel != null) row.HpPercentLabel.text = string.Empty;

            // 죽은 캐릭터의 침식 수치는 의미가 없다 — 비워서 회색 행과 톤을 맞춘다.
            row.Erosion.Clear();
        }

        /// <summary>
        /// 영웅 각성한 캐릭터인가 — 이름을 금색으로 그릴지 정한다.
        /// ★ <see cref="UnitPortraitPanel"/> 과 <b>같은 판정</b>을 쓴다(각성 횟수 ≥ 1).
        /// ⚠ <c>Of</c> 를 쓴다(<c>EnsureOn</c> 이 아니다) — 표시하는 쪽이 컴포넌트를
        ///   <b>만들어서는</b> 안 된다. 없으면 «각성 안 함» 이 맞다.
        /// </summary>
        static bool IsHero(CharacterUnit unit)
        {
            if (unit == null) return false;
            CharacterKills kills = CharacterKills.Of(unit);
            return kills != null && kills.IsHero;
        }

        /// <summary>행이 재활용될 때 이전 사망 표시(회색)를 지우고 정상 색으로 되돌린다.</summary>
        void ApplyAliveAppearance(Row row)
        {
            HudTheme.PaintButton(row.Background, ButtonState.Normal, rowNormal);
            // ⚠ 각성 색은 <see cref="RefreshValues"/> 가 매 프레임 다시 칠한다 — 여기서는
            //   «재활용된 행의 회색을 지운다» 만 한다(그 함수의 ⚠ 참조).
            if (row.Name != null) row.Name.color = HudTheme.TextMain;
            if (row.Duty != null) row.Duty.color = HudTheme.TextDim;
            if (row.SelectButton != null) row.SelectButton.interactable = true;
        }

        /// <summary>
        /// 그 캐릭터의 정의(표에서 온 값). 얼굴 초점을 읽는 데 쓴다 — 2026-08-26.
        /// ⚠ 소환수·몬스터는 정의가 없거나 종류가 달라 <c>null</c> 이다.
        /// </summary>
        static Units.CharacterDefinitionSO DefinitionOf(CharacterUnit unit) =>
            unit != null ? unit.Definition : null;

        static TMP_Text FindText(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        /// <summary>모체에서 이 이름의 자식 <see cref="Image"/>. 없으면 null(그림 없이도 굴러간다).</summary>
        static Image FindImage(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        /// <summary>부대 색 띠 원화. <c>Tools/ui_sprite_cut.py</c> 가 <c>UI_10.png</c> 에서 자른다.</summary>
        const string SquadTabResource = "UI/Frames/Roster_SquadTab";

        /// <summary>얼굴 액자 원화. <c>UI_11.png</c> 에서 자른 것.</summary>
        const string PortraitFrameResource = "UI/Frames/Roster_PortraitSlot";

        /// <summary>
        /// ★★ 행의 <b>얼굴 · 액자 · 유물 아이콘</b>을 그 캐릭터에 맞춘다 (2026-08-26).
        ///
        /// ★ <b>얼굴은 «맞춰 넣기» 가 아니라 «꽉 채우기» 다</b> — 인물화는 세로가 길어
        ///   <c>preserveAspect</c> 로 넣으면 60×60 칸의 <b>폭 절반</b>만 쓰고 양옆이 빈다
        ///   (90-7절이 상세 카드에서 겪은 그것). <see cref="PortraitFit"/>.Cover 가 부모를
        ///   꽉 채우게 키우고, 부모의 <see cref="RectMask2D"/> 가 넘친 만큼 잘라낸다.
        /// ⚠ <b>세로로 잘릴 때 위쪽(얼굴)을 남긴다</b> — <c>verticalAnchor</c> 0.85.
        ///   상세 카드가 사람에게 쓰는 값과 같다.
        /// ⚠ 그림이 없으면 <b>얼굴·액자를 같이</b> 알파 0 으로 지운다 — 액자만 남으면
        ///   «그림이 깨졌다» 로 보인다.
        /// </summary>
        void ApplyPortrait(Row row, CharacterUnit unit)
        {
            Sprite art = unit != null ? unit.Portrait : null;

            if (row.PortraitArt != null)
            {
                if (!ReferenceEquals(row.PortraitArt.sprite, art))
                {
                    row.PortraitArt.sprite = art;
                    if (art != null)
                    {
                        // ★★★ <b>얼굴 좌표를 표에서 받아 자른다</b> (2026-08-26 · 유저 지시:
                        //   *"다시 측정해서 자연스럽게 … 얼굴이 보이는 상체 일러스트 부분만"*).
                        //
                        //   인물화 15장의 얼굴 중심이 세로 0.19~0.38 로 흩어져 있어서
                        //   «맨 위를 남긴다» 같은 한 규칙으로는 못 맞춘다
                        //   (<see cref="Units.CharacterDefinitionSO.faceY"/> 의 설명).
                        // ⚠ 표에 값이 없으면(0) <b>예전처럼 앵커로</b> 자른다 — 몬스터를 로스터에
                        //   띄우는 일은 없지만, 표를 아직 안 채운 캐릭터도 안 깨져야 한다.
                        Units.CharacterDefinitionSO def = DefinitionOf(unit);
                        float fx = def != null ? def.faceX : 0f;
                        float fy = def != null ? def.faceY : 0f;

                        PortraitFit.Cover(row.PortraitArt, portraitVerticalAnchor,
                                          zoom: portraitZoom,
                                          focusX: fx > 0f ? fx : -1f,
                                          focusY: fy > 0f ? fy : -1f,
                                          focusPlacement: portraitFacePlacement);
                    }
                }
                row.PortraitArt.color = art != null ? Color.white : Color.clear;
            }

            if (row.PortraitFrame != null)
                row.PortraitFrame.color = art != null ? Color.white : Color.clear;
        }

        /// <summary>
        /// ★★ 행의 <b>장착 유물 아이콘</b>. 안 꼈으면 알파 0 이다.
        ///
        /// ★ <b>등급 색으로 칠하지 않는다</b> — 아이콘은 원화라 색을 곱하면 탁해진다.
        ///   등급은 <b>테두리 색</b>으로 말하는 것이 이 프로젝트의 규약이지만, 26px 짜리
        ///   아이콘에 테두리를 두르면 그림이 안 보인다. 여기서는 «무엇을 꼈나» 만 보이면 된다
        ///   (등급까지 알고 싶으면 유물 창·상세 카드가 이미 색으로 말한다).
        /// ⚠ <see cref="Relics.RelicInventory"/> 가 없으면(로비·테스트) 조용히 지운다.
        /// </summary>
        void ApplyRelicIcon(Row row, CharacterUnit unit)
        {
            // ★★★ 2026-08-26 — 칸이 셋이 되어 «띠» 가 그린다. 칸이 하나였을 때의 규칙
            //   (등급 색으로 안 칠한다 · 빈 칸은 알파 0)은 그대로 띠 안에 있다.
            row.Relics?.Refresh(unit);
        }

        /// <summary>띠 원화가 없다는 경고는 <b>한 번만</b> 낸다 — 행마다 뜨면 로그가 묻힌다.</summary>
        bool _squadTabWarned;

        void SelectRow(Row row)
        {
            if (row.IsDead || row.Unit == null || !row.Unit.IsAlive) return;

            // 이번 누름이 이미 "꾹 누르기"로 처리됐으면 클릭은 무시한다 — 손을 뗄 때 클릭이
            // 뒤따라 오는데, 그걸 그대로 받으면 부대 배정 모드에서 성장 창을 열려다
            // 엉뚱하게 부대에 배정돼 버린다(UiLongPress.ConsumedThisPress 주석 참조).
            if (row.LongPress != null && row.LongPress.ConsumedThisPress) return;

            // 부대 지정 창에서 부대를 골라둔 상태라면, 이 클릭은 "선택"이 아니라 "배정"이다
            // (유저 확정 2026-08-11: 부대 슬롯을 누른 뒤 로스터의 캐릭터를 누르면 그 부대에 들어간다).
            // 배정으로 처리했으면 선택을 바꾸지 않는다 — 배정하려고 누른 건데 선택까지 따라 바뀌면
            // 다른 창(전술·성장)의 표시가 같이 움직여 혼란스럽다.
            if (SquadPanel.Instance != null && SquadPanel.Instance.TryAssign(row.Unit))
            {
                RefreshValues();
                return;
            }

            if (_selector == null) _selector = UnitSelector.Instance;
            _selector?.Select(row.Unit);
            FocusCameraOn(row.Unit);
            RefreshValues();
        }

        /// <summary>
        /// 행을 <b>꾹 눌렀을 때</b> — 그 캐릭터를 선택하고 <b>캐릭터 성장 창</b>을 바로 띄운다
        /// (유저 확정 2026-08-12: "캐릭터 로스터 각 캐릭터 버튼 꾹 누르면 해당 캐릭터 성장 창이
        /// 바로 나오게").
        ///
        /// ⚠️ <b>부대 배정보다 우선한다</b> — <see cref="SelectRow"/> 와 달리
        /// <see cref="SquadPanel.TryAssign"/> 을 거치지 않는다. 성장 창을 열려고 꾹 눌렀는데
        /// 부대에 배정되면 의도와 정반대다.
        ///
        /// ⚠️ 성장 창은 <b>선택된 캐릭터를 따라가는 창</b>이라(창이 스스로 캐릭터를 고르지 않는다)
        /// 열기 전에 선택을 먼저 옮겨야 한다.
        /// </summary>
        void OpenGrowthFor(Row row)
        {
            if (row.IsDead || row.Unit == null || !row.Unit.IsAlive) return;

            if (_selector == null) _selector = UnitSelector.Instance;
            _selector?.Select(row.Unit);
            FocusCameraOn(row.Unit);

            GrowthPanel()?.SetOpen(true);
            RefreshValues();
        }

        /// <summary>
        /// 캐릭터 성장 창. ⚠️ <b><see cref="CharacterGrowthPanel.Instance"/> 는 창이 한 번도
        /// 안 열렸으면 null 이다</b> — 비활성 오브젝트라 <c>Awake</c> 가 아직 안 돌았기 때문이다
        /// (진행상황 36-4절에서 <c>SquadPanel</c> 로 같은 문제를 겪었다).
        /// 그래서 비활성까지 포함해 찾아 캐시한다.
        /// </summary>
        CharacterGrowthPanel GrowthPanel()
        {
            if (CharacterGrowthPanel.Instance != null) return CharacterGrowthPanel.Instance;

            if (_growthPanel == null)
                _growthPanel = FindAnyObjectByType<CharacterGrowthPanel>(FindObjectsInactive.Include);

            return _growthPanel;
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

        // ------------------------------------------------------------------

        void RefreshValues()
        {
            CharacterUnit selected = _selector != null ? _selector.Selected : null;

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                if (!row.Root.activeSelf || row.Unit == null) continue;
                if (row.IsDead) continue;   // ApplyDeadAppearance 가 이미 확정한 값을 그대로 둔다

                CharacterUnit unit = row.Unit;

                if (row.Name != null)
                {
                    row.Name.text = NameTextOf(unit);

                    // ★★ <b>영웅 각성한 캐릭터는 이름이 금색이다</b> (2026-08-21 · 유저 지시:
                    //   *"왼쪽 위 캐릭터 그리드에도 영웅각성한 캐릭터 이름 황금색으로 변경"*).
                    //
                    //   ★ 판정과 색을 <b>상세 UI 와 공유</b>한다 — 판정은
                    //     <see cref="Combat.CharacterKills.IsHero"/>, 색은
                    //     <see cref="HudTheme.TextHero"/> 다. 두 창이 각자 정하면 한쪽만 바뀐다.
                    //   ⚠ <b>매 프레임 여기서 다시 칠한다</b> — 행은 재활용되므로
                    //     (<see cref="ApplyAliveAppearance"/> 가 TextMain 으로 되돌린다)
                    //     각성 여부를 <b>갱신 때마다</b> 다시 반영해야 한다.
                    //     각성은 판 중간에 일어나므로 «한 번 칠하고 끝» 이 성립하지 않는다.
                    row.Name.color = IsHero(unit) ? HudTheme.TextHero : HudTheme.TextMain;
                }

                // HP 바는 여기서 건드리지 않는다 — ApplyHp(즉시)+AnimateHpBars(매 프레임)가 반영한다.
                // 폴링과 애니메이션이 같은 값을 이중으로 쓰면 순서에 따라 잠깐 어긋나 보일 수 있다.

                // 현재 상태 — 구속(기절 등)이 정신 이상보다 먼저다(둘 다 걸릴 일은 거의 없지만
                // UnitPortraitPanel.StateTextOf 와 우선순위를 맞춘다), 정신 이상이 발동 중이면
                // 그 이름을 임무보다 먼저 보여준다(유저 확정: "로스터의 현재 상태에 정신 이상 상태
                // 표기" → 2026-08-19, 구속도 같은 자리에 같은 방식으로 표기). 색까지 바꿔서
                // "지금 정상이 아니다"가 한눈에 보이게 한다.
                if (row.Duty != null)
                {
                    UnitCombat combat = unit.GetComponent<UnitCombat>();
                    CharacterErosion erosion = CharacterErosion.Of(unit);
                    bool bound = combat != null && combat.IsBound;
                    // ★ 2026-08-20 — 「중독」(베일 「담배 연기」)도 같은 자리에 같은 방식으로.
                    //   우선순위는 UnitPortraitPanel.StateTextOf 와 <b>같게</b> 맞춘다:
                    //   구속 → 중독 → 정신 이상 → 임무. 두 창이 서로 다른 것을 보여주면
                    //   "어느 쪽이 맞지" 가 된다.
                    bool poisoned = !bound && combat != null && combat.IsPoisoned;
                    bool deranged = !bound && !poisoned && erosion != null && erosion.HasActive;

                    if (bound) row.Duty.text = combat.BoundLabel;
                    else if (poisoned) row.Duty.text = combat.PoisonLabel;
                    else if (deranged) row.Duty.text = erosion.ActiveName;
                    else row.Duty.text = DutyTextOf(unit);

                    row.Duty.color = bound || poisoned ? HudTheme.TextDanger
                                   : deranged ? HudTheme.TextErosion
                                   : HudTheme.TextDim;
                }

                row.Erosion.Refresh(unit);

                // ★ 고른 행은 «켜짐» 그림, 나머지는 «평소» 그림이다(2026-08-26).
                bool picked = ReferenceEquals(unit, selected);
                HudTheme.PaintButton(row.Background,
                                     picked ? ButtonState.On : ButtonState.Normal,
                                     picked ? rowSelected : rowNormal);

                ApplySquadOutline(row, unit);
                ApplyPortrait(row, unit);
                ApplyRelicIcon(row, unit);
            }
        }

        // ------------------------------------------------------------------
        // 부대 묶음 (2026-08-24 유저 지시)
        // ------------------------------------------------------------------

        /// <summary>
        /// 이 캐릭터가 속한 부대의 <b>순번</b>(0부터). 없으면 <see cref="int.MaxValue"/>.
        ///
        /// ★ 부대 <b>id</b> 가 아니라 <b>목록에서의 순번</b>을 쓴다 — id 는 지웠다 만들면
        ///   1,2,5 처럼 띄엄띄엄해져 색이 건너뛴다. 순번이면 «위에서 몇 번째 부대» 와
        ///   색이 언제나 같이 간다(부대 창의 카드 순서와도 맞는다).
        /// </summary>
        static int SquadOrderOf(CharacterUnit unit)
        {
            if (unit == null) return int.MaxValue;

            SquadService squads = SquadService.Instance;
            if (squads == null) return int.MaxValue;

            SquadService.Squad squad = squads.SquadOf(unit);
            if (squad == null) return int.MaxValue;

            var list = squads.Squads;
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], squad)) return i;

            return int.MaxValue;
        }

        /// <summary>
        /// 행 테두리를 그 캐릭터의 부대 색으로 칠한다. 부대가 없으면 <b>투명</b>이다 —
        /// «없음» 을 회색 테두리로 표현하면 그것도 하나의 부대처럼 보인다.
        /// </summary>
        void ApplySquadOutline(Row row, CharacterUnit unit)
        {
            // ⚠ 아웃라인이 없어도 <b>행 물들이기는 해야 한다</b> — 예전에는 여기서 통째로
            //   돌아섰다(그때는 아웃라인이 유일한 표시였다).
            if (row.SquadOutline == null && !squadUsesRowTint) return;

            int order = groupBySquad ? SquadOrderOf(unit) : int.MaxValue;
            bool has = order != int.MaxValue;
            Color squad = has ? HudTheme.SquadColor(order) : Color.white;

            if (squadUsesRowTint)
            {
                // ★★★ <b>행 «전체» 를 부대 색으로 물들인다</b> (2026-08-26 · 유저 지시:
                //   *"부대 지정선 이렇게 하지말고 아까처럼 전체 색 바뀌는걸로 해줘"* ·
                //   *"안 보임 이렇게 하면"*).
                //
                //   테두리(1~2px)와 왼쪽 띠(7px)는 <b>행 카드 그림이 들어온 뒤로 안 보였다</b> —
                //   카드에 이미 밝은 테두리 장식이 있어 그 위에 얇은 선을 하나 더 그으면
                //   장식에 묻힌다. 면(面)으로 말해야 한 눈에 갈린다.
                //
                // ⚠ <b>곱셈이다.</b> 카드 속색이 어두운 남색(#212B38)이라 부대 색을 그대로
                //   곱하면 너무 탁해진다 — <see cref="squadTintStrength"/> 만큼만 흰색에서
                //   부대 색 쪽으로 옮겨 «물든» 정도로 둔다.
                // ⚠ <see cref="HudTheme.PaintButton"/> 이 매번 색을 흰색으로 되돌리므로
                //   <b>반드시 그 뒤에</b> 불려야 한다(RefreshValues 의 호출 순서가 그렇다).
                // ★★★ 2026-08-26 — <b>곱셈이 아니라 «한 겹 얹기» 다</b> (유저: *"아까 너무 어둡던데"*).
                //
                //   처음에는 <c>Background.color</c> 에 부대 색을 <b>곱했다</b>. 그런데 곱셈은
                //   <b>원본보다 밝아질 수 없다</b> — 카드 속색이 <c>#212B38</c>(33,43,56) 이라
                //   1부대 청록을 곱해도 <c>#152838</c>(21,40,54) 로 <b>되레 어두워진다</b>.
                //   채도도 못 올린다. «반영은 되는데 안 보인다» 의 정체다.
                //
                // ★ 그래서 <b>반투명 층</b>(<see cref="Row.SquadTint"/>)을 카드 위에 한 겹 얹는다.
                //   알파 블렌딩이라 어두운 바탕에도 색이 <b>더해진다</b>.
                // ⚠ 알파는 낮게 둔다(<see cref="squadTintAlpha"/> 0.22) — 이 층은 글자 위에도
                //   깔리므로 진하면 이름·수치가 물든다. «물들었다» 가 보이는 최소치가 맞다.
                // ⚠ 배경은 <b>흰색으로 되돌린다</b> — 곱셈을 걷어내지 않으면 두 겹이 겹쳐
                //   여전히 어둡다(<see cref="HudTheme.PaintButton"/> 이 흰색으로 두는 그대로).
                if (row.SquadTint != null)
                {
                    // ★★★ 2026-08-26 (2차) — <b>층이 카드 그림을 물려받으면 안 된다</b>
                    //   (유저 보고: *"같은 부대일 때 같은 색으로 묶어두는 기능 없어짐"*).
                    //
                    //   앞선 판은 «실루엣을 맞추려고» <c>SquadTint.sprite = Background.sprite</c>
                    //   를 했다. 그런데 <see cref="Image"/> 는 <b>그림에 색을 곱해서</b> 그린다 —
                    //   카드 그림의 속살이 <c>#212B38</c> 로 어두우니 부대 색을 곱해도
                    //   <b>어두운 색</b>이 나오고, 그걸 알파로 얹으니 «살짝 어두워진 것» 밖에
                    //   안 됐다. 색이 안 보이니 <b>기능이 사라진 것처럼</b> 보였다.
                    //
                    // ★ 그래서 <b>그림 없는 흰 판</b>으로 되돌린다 — 흰색에 부대 색을 곱하면
                    //   부대 색 그대로다. 예전에 «네모가 삐져나와» 보였던 것은 판이 행
                    //   <b>전체</b>를 덮었기 때문인데, 이제 칸을 <b>카드 안쪽 홈에 맞춰</b>
                    //   씬에서 잡아 뒀다(위·아래 테두리와 아래 레일을 비켜 간다).
                    // ⚠ <b>판은 첫째 자식</b>이라 카드 위·글자 아래에 깔린다
                    //   (<see cref="CreateRow"/> 의 <c>SetAsFirstSibling</c>). 그래서 알파를
                    //   0.5 까지 올려도 이름·수치가 물들지 않는다.
                    if (row.SquadTint.sprite != null) row.SquadTint.sprite = null;

                    Color layer = has
                        ? new Color(squad.r, squad.g, squad.b, squadTintAlpha)
                        : Color.clear;
                    if (row.SquadTint.color != layer) row.SquadTint.color = layer;
                }

                // 면으로 말하기로 했으면 선은 <b>지운다</b> — 둘 다 켜면 시끄럽다.
                if (row.SquadOutline != null) row.SquadOutline.effectColor = Color.clear;
                if (row.SquadTab != null) row.SquadTab.color = Color.clear;
                return;
            }

            // ── 예전 방식(선) — <see cref="squadUsesRowTint"/> 를 끄면 이쪽으로 돌아온다 ──
            row.SquadOutline.effectColor = has ? squad : Color.clear;

            // ★ 띠도 <b>같은 색</b>이다 — 둘이 갈리면 «테두리는 파란데 띠는 주황» 이 된다.
            if (row.SquadTab != null) row.SquadTab.color = has ? squad : Color.clear;
        }

        /// <summary>
        /// 화면에 보이는 행 순서를 <b>생성순으로 고정</b>한다. <see cref="Row"/> 와 캐릭터의
        /// 매칭(구독·데이터)은 그대로 두고 <c>SetSiblingIndex</c> 로 <see cref="listRoot"/> 안의
        /// 표시 순서만 바꾼다 — <c>VerticalLayoutGroup</c> 이 형제 인덱스 순으로 배치하므로
        /// 이것만으로 목록이 다시 정렬된다.
        ///
        /// ★★ <b>2026-08-21 — 체력순 정렬을 없앴다</b> (유저 지시: *"생성순대로 캐릭터 그리드
        /// 위치 고정"*). 예전에는 «체력 % 낮은 순, 사망은 맨 아래» 였다 — 지금 신경 쓸 캐릭터를
        /// 위로 올린다는 뜻이었는데, <b>맞을 때마다 카드가 자리를 바꿔</b> 방금 누르려던 카드가
        /// 손 밑에서 사라졌다. «누구의 카드가 어디에 있는지» 를 외울 수 있는 편이 낫다.
        ///
        /// ★ 순서의 근거는 <see cref="_characters"/> <b>목록의 순번</b>이다 —
        ///   <see cref="AppendNewCharacters"/> 가 새 캐릭터를 <b>뒤에만</b> 붙이므로 그 자체가
        ///   생성순이고, 죽어도 빠지지 않는다(<see cref="HandleWaveEnded"/> 에서만 정리).
        ///   즉 <b>죽은 캐릭터도 자기 자리에 그대로 남는다</b> — «위치 고정» 의 뜻이 그것이다.
        /// ⚠ 목록에 없는 행(있을 수 없지만 방어)은 맨 뒤로 보낸다.
        /// </summary>
        void ReorderRows()
        {
            // ★★ 2026-08-24 — <b>부대가 먼저, 그 안에서 생성순</b>(유저 지시:
            //   *"캐릭터 로스터 배열 정렬을 같은 부대 기준으로"*).
            //   부대에 안 든 캐릭터는 <b>맨 아래로</b> 모인다(순번 int.MaxValue).
            //   ⚠ <c>OrderBy().ThenBy()</c> 는 <b>안정 정렬</b>이라 같은 부대 안의 순서는
            //     생성순 그대로다 — 카드가 손 밑에서 튀지 않는다(아래 ★★ 와 같은 이유).
            var active = (groupBySquad
                    ? _rows.Where(r => r.Root.activeSelf)
                           .OrderBy(r => SquadOrderOf(r.Unit))
                           .ThenBy(r => CreationIndexOf(r.Unit))
                    : _rows.Where(r => r.Root.activeSelf)
                           .OrderBy(r => CreationIndexOf(r.Unit)))
                .ToList();

            for (int i = 0; i < active.Count; i++)
                active[i].Root.transform.SetSiblingIndex(i);
        }

        /// <summary>생성 순번. 목록에 없으면 맨 뒤(<see cref="int.MaxValue"/>).</summary>
        int CreationIndexOf(CharacterUnit unit)
        {
            if (unit == null) return int.MaxValue;
            int i = _characters.IndexOf(unit);
            return i < 0 ? int.MaxValue : i;
        }

        /// <summary>
        /// 로스터 행의 이름 칸 — <b>이름 옆에 레벨</b>을 붙인다
        /// (유저 지시 2026-08-15: <i>"캐릭터의 강화 횟수를 lv로 바꾸고 캐릭터의 레벨을
        /// 로스터의 이름 옆에 표기"</i>).
        ///
        /// <b>왜 칸을 새로 안 만들었나</b> — 행 템플릿(<c>RowTemplate</c>)의 가로 폭은 이미
        /// 이름·상태·HP·침식으로 꽉 차 있다(48절 미결 64번: Info 컬럼 폭이 상한이다).
        /// 칸을 하나 더 끼우면 이름이 잘린다. 레벨은 두세 글자라 <b>이름 칸 안에</b>
        /// 작은 글씨로 얹는 편이 폭을 안 먹는다.
        ///
        /// ⚠ TMP 리치 텍스트를 쓴다 — 이 프로젝트의 TMP 는 리치 텍스트가 켜져 있다
        /// (전술 지침 창의 <c>LV.</c> 표기가 이미 같은 방식이다).
        ///
        /// ★ <b>레벨 = 강화 횟수</b>다. 값을 새로 만들지 않았다 —
        /// <see cref="CharacterUnit.UpgradeCount"/> 가 이미 그 뜻이고, 패시브 해금 조건도
        /// 그 값을 본다(35절). "강화 횟수"라는 <b>이름만</b> 화면에서 Lv 로 바꾼 것이다.
        /// </summary>
        static string NameTextOf(CharacterUnit unit)
        {
            if (unit == null) return string.Empty;
            string lv = ColorUtility.ToHtmlStringRGB(HudTheme.TextAccent);
            return $"{unit.DisplayName} <size=78%><color=#{lv}>Lv.{unit.UpgradeCount}</color></size>";
        }

        /// <summary>"지금 뭐 하는 중인지" 한 단어. 전투가 자율 이동보다 우선이라 먼저 검사한다.</summary>
        static string DutyTextOf(CharacterUnit unit)
        {
            var behavior = unit.GetComponent<CharacterBehavior>();

            // 후퇴·도망은 전투보다 먼저 본다 — 그 중에는 타겟을 잡지 않으므로 아래 교전 판정에
            // 걸리지 않지만, 순서를 명시해 두는 편이 의도가 분명하다.
            if (behavior != null && behavior.IsRetreating) return Data.StringTable.Get("ui_duty_retreat", "후퇴");
            if (behavior != null && behavior.IsFleeing) return Data.StringTable.Get("ui_duty_flee", "도망");

            var combat = unit.GetComponent<UnitCombat>();
            if (combat != null && combat.Target != null && combat.Target.IsAlive)
            {
                if (combat.AttackType == TacticalAttackType.Heal) return Data.StringTable.Get("ui_duty_heal", "치유");
                return combat.IsHunting ? Data.StringTable.Get("ui_duty_hunt", "사냥")
                                        : Data.StringTable.Get("ui_duty_fight", "교전");
            }

            if (behavior == null) return "-";

            return behavior.Duty switch
            {
                CharacterDuty.Expedition   => Data.StringTable.Get("ui_duty_expedition", "탐험"),
                CharacterDuty.Rally   => Data.StringTable.Get("ui_duty_rally", "집결"),
                CharacterDuty.Retreat => Data.StringTable.Get("ui_duty_retreat", "후퇴"),
                CharacterDuty.Flee    => Data.StringTable.Get("ui_duty_flee", "도망"),
                CharacterDuty.Build   => Data.StringTable.Get("ui_duty_build", "건설"),
                _                     => Data.StringTable.Get("ui_duty_guard", "방어"),
            };
        }
    }
}
