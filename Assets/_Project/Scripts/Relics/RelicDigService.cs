using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Combat;
using LastSanctuary.Fog;
using LastSanctuary.Map;
using LastSanctuary.UI;
using LastSanctuary.Units;

namespace LastSanctuary.Relics
{
    /// <summary>발굴 가능 칸 하나. <see cref="LastSanctuary.Buildings.BuildSite"/> 와 같은 모양이다.</summary>
    public class DigSite
    {
        public Vector3Int Cell;
        public Vector3 Center;

        /// <summary>한 번이라도 캐릭터 시야에 들어왔는가 — <b>기억</b>이다(표 Info 시트 «보이기»).</summary>
        public bool Revealed;

        /// <summary>유저가 «파라» 고 눌렀는가. 누르기 전에는 캐릭터가 가지 않는다.</summary>
        public bool Ordered;

        /// <summary>지금까지 쌓인 발굴 시간(초).</summary>
        public float Progress;

        /// <summary>이 자리를 맡은 캐릭터. <b>한 자리에 한 명</b>(건설과 같은 규칙).</summary>
        public CharacterBehavior Digger;

        /// <summary>
        /// ★★ 이 자리가 쓸 <b>대사 묶음</b> (2026-08-24 · 표 Ver02 <c>Dialogue</c> 시트).
        ///
        /// 자리를 만들 때 한 번 뽑아 <b>여기에 기억해 둔다</b> — 창을 다시 열 때마다 말투가
        /// 바뀌면 «다른 자리를 보고 있나» 싶어진다. 발견의 말투와 결과의 말투는 이어져야 한다.
        /// </summary>
        public int DialogueGroup;

        public float Ratio(float required) => required > 0f ? Mathf.Clamp01(Progress / required) : 0f;
    }

    /// <summary>
    /// ★★★ <b>유물 발굴</b> (2026-08-23 신설 · 유저 지시).
    ///
    /// <code>
    ///   ② 맵이 생성(게임 재시작)될 때마다 맵 전체에 랜덤한 칸이 총 N개 발굴 가능 칸으로 선정
    ///   ③ 그 칸이 캐릭터의 시야에 보이면 느낌표가 뜨고 클릭 가능해진다.
    ///      클릭하면 «기존에 삭제된 건설처럼» 가장 가까운 캐릭터가 15초에 걸쳐 발굴하고,
    ///      일정 확률에 따라 에너지·체력 회복/감소·유물 획득 등이 일어난다
    /// </code>
    ///
    /// <b>왜 건설과 «같은 구조» 인가</b> — 유저 지시가 그렇게 못박았고("기존에 삭제된 건설처럼"),
    /// 실제로 필요한 것이 같다: «자리 목록 · 한 자리에 한 명 · 걸어가서 시간을 채운다 ·
    /// 진행도를 화면에 겹쳐 그린다». <see cref="LastSanctuary.Buildings.BuildService"/> 가
    /// 이미 그 네 가지를 다 갖고 있으므로 <b>그 구조를 그대로 옮겼다</b> — 두 기능이 서로 다른
    /// 조작감을 갖지 않게 하려는 것이다(그 클래스가 집결지에서 구조를 가져온 것과 같은 이유).
    ///
    /// ★ <b>다른 점 하나 — 클릭을 «월드» 가 아니라 «UI» 로 받는다.</b>
    ///   건설은 «배치 모드» 로 들어가 맵을 클릭하지만, 발굴은 모드가 없고 <b>칸에 뜬 느낌표</b>를
    ///   직접 누른다(지시 3번). 월드 클릭으로 받으면 <see cref="UnitSelector"/>·집결지와
    ///   <b>같은 클릭을 두고 다툰다</b>. 느낌표를 <b>UI 버튼</b>으로 두면 그쪽들이 이미
    ///   «포인터가 UI 위면 무시» 하므로 다툼이 아예 생기지 않는다.
    ///
    /// ★ <b>자리는 <see cref="Start"/> 에서 고른다 — 그것이 곧 «맵이 생성될 때마다» 다.</b>
    ///   이 프로젝트의 맵은 <see cref="MapGenerator.Awake"/> 가 <b>판마다 새 씨앗으로</b>
    ///   다시 만든다(그쪽 <c>ResolveStartupSeed</c>). 유니티는 «모든 Awake 가 모든 Start 보다
    ///   먼저» 를 보장하므로, <see cref="Start"/> 에 오면 <see cref="MapGenerator.MapSize"/> ·
    ///   걸을 수 있는 칸이 이미 확정돼 있다 — 안개·흐름장·스포너가 전부 같은 이유로
    ///   <c>Start</c> 에서 맵을 읽는다.
    /// ⚠ <b>이어하기는 새로 고르지 않는다</b> — 저장된 자리를 <see cref="Restore"/> 가 넣는다.
    ///   안 그러면 이어할 때마다 자리가 바뀌어 «가던 캐릭터가 허공을 판다».
    /// </summary>
    public class RelicDigService : MonoBehaviour
    {
        public static RelicDigService Instance { get; private set; }

        // ------------------------------------------------------------------
        // 인스펙터 — 유저 지시: *"발굴 가능 칸 갯수는 너가 정하고 에딧에서 수정 가능하게"*
        // ------------------------------------------------------------------

        [Header("발굴 가능 칸")]
        [Tooltip("맵 전체에 뿌릴 발굴 가능 칸 수.\n" +
                 "★ 기본 24 — 맵이 넓어 한 판에 다 밝히기 어렵고, 이 정도면 «탐험하다 가끔 만난다» " +
                 "가 된다. 너무 많으면 발굴이 주 수입원이 되어 웨이브를 미루는 것이 최적 전략이 된다")]
        // ★★ 110 → 45 (2026-08-25 · 유저 리포트 *"발굴 가능 칸이 너무 많고"*).
        //   110 은 «맵 전체로 넓혔으니 밀도를 지키자» 로 올린 값인데(2026-08-25 오전),
        //   칸 하나가 곧 발굴 <b>한 번</b>이라 그것이 한 판의 발굴 횟수 상한이 된다.
        //   에너지 성장(ScaledEnergy)과 함께 줄여 «한 판 약 990 에너지» 로 맞췄다.
        // ⚠ 이 값은 <b>씬에도 있다</b> — 씬 값이 이긴다. 둘을 같이 고쳐야 한다.
        [Min(0)] [SerializeField] int digSiteCount = 45;

        [Tooltip("성역에서 이만큼(타일) 떨어진 곳에만 둔다 — 시작하자마자 다 캐지 않게")]
        [Min(0f)] [SerializeField] float minDistanceFromNexus = 14f;

        [Tooltip("성역에서 이 거리(타일) <b>안쪽</b>에만 자리를 둔다.\n" +
                 "★ <b>0 = 맵 전체</b>(직사각형 320x320 끝까지) — 유저 확정 2026-08-25:\n" +
                 "«중앙 건물로부터 14타일부터 맵 끝까지(직사각형 범위 320까지)로 확장».\n" +
                 "⚠ 넓힌 만큼 <b>칸 수도 같이 올려야</b> 한다 — 안 그러면 밀도가 묽어져\n" +
                 "성역 근처에서 하나도 안 보인다(digSiteCount 참조)")]
        [Min(0f)] [SerializeField] float maxDistanceFromNexus = 0f;

        [Tooltip("발굴 칸끼리 이만큼(타일)은 떨어뜨린다 — 한곳에 몰리면 «한 번 가서 다 캔다» 가 된다")]
        // ★ 10 → 12 (2026-08-25) — 칸 수를 45로 줄이면서 간격을 넓혔다. 좁게 두면 줄어든
        //   칸이 <b>몇 군데에 뭉쳐</b> 「한 번 가서 다 캔다」가 되고, 넓히면 같은 45개가
        //   맵에 골라 퍼져 «찾아 다니는» 맛이 남는다.
        [Min(0f)] [SerializeField] float minSpacing = 12f;

        [Tooltip("자리를 고를 때 시도할 최대 횟수. 이만큼 굴려도 자리가 안 나오면 포기한다")]
        [Min(1)] [SerializeField] int placementAttempts = 4000;

        [Header("발굴")]
        [Tooltip("플로우 필드(걸어갈 수 있는 곳 지도)가 구워지기를 이만큼(초) 기다린 뒤 " +
                 "자리를 고른다. 그 안에 준비가 안 되면 도달 검사 없이 고르고 경고한다")]
        [Min(0.1f)] [SerializeField] float pickWaitSeconds = 5f;

        [Tooltip("한 칸을 파는 데 걸리는 시간(초). 유저 지시: 15초")]
        [Min(1f)] [SerializeField] float digSeconds = 15f;

        [Header("발굴 에너지 성장 (2026-08-25 — ScaledEnergy 의 긴 설명 참조)")]
        [Tooltip("웨이브 하나가 지날 때마다 발굴 에너지 배율이 이만큼 오른다.\n" +
                 "0.10 = 웨이브마다 +10%. 0 이면 성장 없음(표 값 그대로)")]
        [Min(0f)] [SerializeField] float energyGrowthPerWave = 0.10f;

        [Tooltip("배율 상한. 2.5 면 표 값의 2.5배가 최대다 " +
                 "(growthPerWave 0.10 이면 16웨이브에 상한에 닿는다)")]
        [Min(1f)] [SerializeField] float energyGrowthCap = 2.5f;

        [Tooltip("캐릭터가 이 거리(타일) 안에 들어와야 발굴이 진행된다")]
        [Min(0.5f)] [SerializeField] float digWorkRange = 1.6f;

        [Header("오버레이 (모체 하나를 복제해서 쓴다 — 비활성으로 둘 것)")]
        [Tooltip("느낌표 표식의 원본. UI_Root/DigOverlay 아래. 비어 있으면 이름으로 찾는다")]
        [SerializeField] RectTransform markerTemplate;

        [SerializeField] RectTransform overlayParent;

        [SerializeField] Color idleColor = new Color(0.98f, 0.85f, 0.35f, 0.95f);
        [SerializeField] Color orderedColor = new Color(0.45f, 0.95f, 0.78f, 0.95f);

        [Tooltip("표식의 화면 크기(픽셀). 카메라 줌과 무관하게 일정하게 둔다 — 작아지면 못 누른다")]
        [Min(8f)] [SerializeField] float markerPixels = 34f;

        [Header("표식 그림 (2026-08-25)")]
        [Tooltip("느낌표 원화의 Resources 경로. 비우거나 못 찾으면 <b>글자 느낌표</b>로 돌아간다.\n" +
                 "굽는 스크립트: Tools/dig_marker_build.py")]
        [SerializeField] string markerSpriteResource = "DigMarker/dig_marker";

        [Tooltip("평소 표식의 색. ★ 원화를 <b>그려진 대로</b> 보여주려면 흰색이어야 한다 — " +
                 "다른 색을 주면 곱연산으로 물든다")]
        [SerializeField] Color markerSpriteIdle = Color.white;

        [Tooltip("파는 중일 때의 색. 진행도가 오르면 평소 색으로 돌아온다 — " +
                 "«지금 누가 파고 있다» 를 색이 옅어지는 것으로 알린다")]
        [SerializeField] Color markerSpriteDigging = new Color(0.52f, 0.56f, 0.62f, 0.95f);

        [Header("통통 튀기 (2026-08-25 · 주의를 끌기 위해)")]
        [Tooltip("튀어오르는 높이(픽셀). 0 이면 튀지 않는다")]
        [Min(0f)] [SerializeField] float bounceHeight = 9f;

        [Tooltip("한 번 튀고 다음에 튀기까지의 주기(초)")]
        [Min(0.05f)] [SerializeField] float bouncePeriod = 0.95f;

        [Tooltip("주기 중 <b>공중에 있는</b> 비율. 1 보다 작아야 바닥에서 잠깐 쉰다 — " +
                 "그 «쉼» 이 있어야 통통 튀는 것으로 보인다. 1 이면 계속 흐물거린다")]
        [Range(0.15f, 1f)] [SerializeField] float bounceAirRatio = 0.55f;

        [Tooltip("표식마다 튀는 때를 이만큼 어긋나게 한다(초). 0 이면 전부 <b>한꺼번에</b> 튄다")]
        [Min(0f)] [SerializeField] float bouncePhaseStep = 0.19f;

        /// <summary>느낌표 원화. 못 찾으면 null 이고, 그때는 글자 느낌표를 그대로 쓴다.</summary>
        Sprite _markerSprite;

        [Header("디버그")]
        [SerializeField] bool logChanges = true;

        readonly List<DigSite> _sites = new List<DigSite>();
        readonly List<RectTransform> _markers = new List<RectTransform>();
        readonly List<CharacterBehavior> _freeWorkers = new List<CharacterBehavior>();

        int _assignFrame = -1;
        Camera _camera;
        MapGenerator _map;
        FogOfWarService _fog;
        RelicDigTableSO _table;

        /// <summary>지금 남아 있는 발굴 칸. <see cref="CharacterBehavior"/> 가 읽어간다.</summary>
        public IReadOnlyList<DigSite> Sites => _sites;

        /// <summary>한 칸을 파는 데 필요한 시간(초). UI 가 진행도를 그릴 때도 쓴다.</summary>
        public float DigSeconds => Mathf.Max(1f, digSeconds);

        /// <summary>지금 화면에 보이는(= 발견한) 칸 수. 액션 버튼의 뱃지가 읽는다.</summary>
        public int RevealedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _sites.Count; i++) if (_sites[i].Revealed) n++;
                return n;
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            // ★ 드랍 판정은 상태가 없는 정적 클래스다 — <b>이 서비스의 생애에 맞춰</b>
            //   붙였다 뗀다(그 클래스의 ⚠: 안 떼면 다음 판에 두 배로 걸린다).
            RelicDropService.Hook();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // ⚠ 정적 이벤트를 붙여 둔 것이 있으므로 판이 끝날 때 반드시 뗀다(그 클래스들의 ⚠).
            RelicDropService.Unhook();
            RelicEffectService.Unhook();
        }

        /// <summary>발굴 대사표(표 Ver02). 없으면 창이 <c>Fallback</c> 한 줄로 뜬다.</summary>
        RelicDialogueTableSO _dialogue;

        /// <summary>대사 묶음 번호들 — 자리를 만들 때 여기서 하나 뽑는다.</summary>
        readonly List<int> _dialogueGroups = new List<int>();

        /// <summary>마지막으로 본 웨이브 번호. 바뀌는 순간이 «웨이브가 넘어갔다» 다.</summary>
        int _seenWave = -1;

        Wave.WaveManager _wave;

        void Start()
        {
            _camera = Camera.main;
            _map = FindAnyObjectByType<MapGenerator>();
            _fog = FindAnyObjectByType<FogOfWarService>();
            _table = RelicDigTableSO.Load();
            _dialogue = Resources.Load<RelicDialogueTableSO>("Relics/RelicDialogueTable");
            if (_dialogue != null) _dialogueGroups.AddRange(_dialogue.GroupIds());
            else Debug.LogWarning("[유물] Resources/Relics/RelicDialogueTable 이 없습니다 — "
                                  + "발굴 창이 기본 문구로 뜹니다. gen_relic_assets.py 를 돌려주세요.", this);

            ResolveOverlay();

            // ★ 이어하기가 예약돼 있으면 자리를 새로 고르지 않는다 — 저장된 자리를
            //   <see cref="Restore"/> 가 넣어 준다(그쪽이 늦게 도착해도 되도록 비워 둔다).
            if (Save.SaveService.PendingLoad != null) return;

            // ★★ <b>여기서 곧바로 고르지 않는다</b> (2026-08-25).
            //   자리 고르기가 «걸어갈 수 있는가» 를 보게 되면서 <see cref="FlowFieldService"/> 가
            //   필요해졌는데, 그쪽도 <c>Start</c> 에서 굽는다. 실측(콘솔 시각)으로
            //   <b>이 서비스가 먼저</b> 돌았다 — 유니티는 오브젝트 사이의 Start 순서를
            //   보장하지 않는다. 그래서 «준비되면 그때» 고른다(아래 <see cref="TryPickWhenReady"/>).
            //   ⚠ 실행 순서를 스크립트 설정으로 못박는 방법도 있지만, 그건 <b>보이지 않는
            //     의존</b>이라 다음 사람이 이 코드만 읽고는 알 수 없다.
            _pickPending = true;
            _pickDeadline = Time.unscaledTime + pickWaitSeconds;
        }

        void ResolveOverlay()
        {
            GameObject canvas = GameObject.Find("UI_Root");
            Transform overlay = canvas != null ? canvas.transform.Find("DigOverlay") : null;

            if (markerTemplate == null && overlay != null)
                markerTemplate = overlay.Find("DigMarkerTemplate") as RectTransform;
            if (overlayParent == null) overlayParent = overlay as RectTransform;
            if (overlayParent == null && markerTemplate != null)
                overlayParent = markerTemplate.parent as RectTransform;

            if (markerTemplate != null) markerTemplate.gameObject.SetActive(false);
            else Debug.LogWarning("[유물] DigOverlay/DigMarkerTemplate 을 찾지 못했습니다 — " +
                                  "발굴 칸이 화면에 안 보입니다.", this);

            SinkOverlayBehindWindows();
            LoadMarkerSprite();
        }

        /// <summary>
        /// ★★ <b>느낌표를 모든 창 «뒤» 로 내린다</b> (2026-08-25 · 유저 리포트:
        /// *"발굴 느낌표가 타 ui 를 켰을때 가려지지 않고 위에 나타남"*).
        ///
        /// <b>원인</b> — 한 <see cref="Canvas"/> 안에서 그리는 순서는 <b>형제 순서</b>다
        /// (나중 형제가 앞). <c>DigOverlay</c> 는 <c>UI_Root</c> 의 자식 23개 중
        /// <b>21번째</b>였다 — 로스터·미니맵은 물론 <c>HUD_Portrait</c>·<c>HUD_Squad</c>·
        /// <c>HUD_Event</c>·<c>HUD_Settings</c>·<c>HUD_Defeat</c>·<c>HUD_Victory</c> 까지
        /// <b>거의 전부보다 앞</b>이었다. 그래서 창을 열어도 느낌표가 그 위에 떠 있었다.
        ///
        /// <b>왜 코드에서 고치나</b> — 씬에서 형제 순서를 바꿔도 되지만,
        /// ① <b>MCP 에는 형제 순서를 바꾸는 도구가 없다</b>(reparent 는 순서를 못 정한다),
        /// ② 사람이 하이라키에서 <b>드래그 한 번</b>으로 다시 깨뜨릴 수 있고 그때 아무 경고도
        ///    나지 않는다. 한 줄로 <b>매번 확정</b>하는 편이 무너지지 않는다.
        ///    (<c>UiFillBar.Prepare</c> 가 스프라이트에 대해 하는 것과 같은 «안전망» 이다.)
        ///
        /// ★ <b>맨 앞이 아니라 맨 «뒤» 로 보낸다</b> — 창 하나하나보다 뒤로 보내려면
        ///   «어느 창보다 뒤인가» 를 관리해야 하고, 창이 하나 늘 때마다 그 표가 틀린다.
        ///   맨 뒤로 보내면 <b>앞으로 생길 창까지</b> 저절로 느낌표를 가린다.
        /// ⚠ 그래서 느낌표는 상시 HUD(로스터·미니맵·로그) <b>뒤에도</b> 깔린다. 그게 맞다 —
        ///   표식이 로스터 판 위로 삐져나오는 것도 같은 종류의 사고였다.
        /// ⚠ 클릭은 그리는 순서의 <b>역순</b>이라, 뒤로 내려도 위에 아무것도 없으면 그대로 눌린다.
        ///   창을 열면 그 창이 클릭을 먹는데 — 그것이 이 수정이 원하는 바다.
        ///
        /// ══════════════════════════════════════════════════════════════════
        ///  ★★★ 2026-08-26 — <b>위의 «형제 순서» 수정은 아무 일도 하지 않았다</b>
        /// ══════════════════════════════════════════════════════════════════
        /// 유저 리포트가 <b>그대로 다시</b> 들어왔다: *"느낌표가 있어도 다른 ui 가 뜨면 그 ui
        /// 위에 계속 느낌표가 뜸"*.
        ///
        /// <b>진짜 원인</b> — <c>DigOverlay</c> 는 <b>자기 <see cref="Canvas"/></b> 를 갖고 있고
        /// 그 캔버스가 <c>overrideSorting = true · sortingOrder = 5</c> 다(134-5절이 «표식이
        /// 개체 클릭보다 위» 를 만들려고 붙인 것이다). 그런데 <b><c>overrideSorting</c> 이 켜진
        /// 캔버스는 형제 순서를 통째로 무시한다</b> — <c>sortingOrder</c> 만 본다. 창들은 전부
        /// 자기 캔버스가 없어 <c>UI_Root</c>(0) 에 얹혀 있으므로 <b>5 는 언제나 그 위</b>였다.
        /// <c>SetAsFirstSibling()</c> 은 «같은 캔버스 안» 에서만 뜻이 있는 도구라 헛돌았다.
        ///
        /// ★ <b>그래서 이제 «순서» 가 아니라 «층» 을 맞춘다</b> — <c>sortingOrder</c> 를
        ///   <see cref="OverlaySortingOrder"/>(−1) 로 <b>내린다</b>. 씬에서도 −1 로 고쳐 뒀고,
        ///   이 줄은 사람이 인스펙터에서 되돌렸을 때를 위한 <b>안전망</b>이다(위 «왜 코드에서
        ///   고치나» 의 이유가 그대로 산다).
        /// ★ <b>−1 은 건설·집결지 오버레이와 같은 층이다</b>(둘 다 −1). 같은 층 안에서는 다시
        ///   형제 순서가 정하고 <c>DigOverlay</c> 가 그 둘보다 <b>뒤 형제</b>라 위에 그려진다 —
        ///   느낌표가 집결지 범위 원에 묻히지 않는다. 그래서 <c>SetAsFirstSibling()</c> 은
        ///   <b>부른다면 오히려 해가 된다</b>(맨 앞으로 가면 그 둘 밑으로 들어간다). 뺐다.
        /// ⚠ <b><c>HUD_Dig</c>(발굴 창)는 6 이라 여전히 위</b>다 — 그건 맞다. 표식을 눌러서 뜬
        ///   창이 그 표식에 가리면 안 된다.
        /// ⚠ 클릭 우선순위도 함께 내려간다(<c>GraphicRaycaster</c> 가 <c>sortingOrder</c> 를
        ///   우선순위로 쓴다). «유닛보다 표식이 먼저 눌린다» 는 <b>깨지지 않는다</b> —
        ///   그것을 지탱하는 것은 이 숫자가 아니라 <c>UnitSelector</c> 의
        ///   <c>IsPointerOverGameObject()</c> 다(134-5절이 «두 겹» 이라 적어 둔 그 둘째 겹).
        /// </summary>
        void SinkOverlayBehindWindows()
        {
            if (overlayParent == null) return;

            // ★ 캔버스가 있으면 «층» 이 정본이다 — 형제 순서는 그때 무시된다.
            if (overlayParent.TryGetComponent(out Canvas canvas))
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = OverlaySortingOrder;
                return;
            }

            // 캔버스가 없는 씬(테스트 등)에서는 예전대로 형제 순서로 내린다.
            overlayParent.SetAsFirstSibling();
        }

        /// <summary>
        /// 느낌표 층. <b>창(0)보다 아래 · 건설·집결지 오버레이와 같은 −1</b>.
        /// ⚠ 씬의 <c>DigOverlay ▸ Canvas ▸ Sorting Order</c> 와 <b>같은 값</b>이어야 한다.
        /// </summary>
        const int OverlaySortingOrder = -1;

        /// <summary>
        /// ★★ <b>느낌표를 글자에서 원화로 바꾼다</b> (2026-08-25 · 유저 지시:
        /// *"느낌표 스프라이트 볼트에 넣어놨으니까 <b>텍스트 대신 주황색 느낌표 짤라서 써</b>
        /// 발굴칸에"*).
        ///
        /// ★ 원화를 <b>못 찾으면 글자 느낌표를 그대로 둔다.</b> 표식이 아예 안 보이는 것보다
        ///   «옛 모양으로 보이는» 편이 낫고, 무엇을 굽지 않았는지 경고로 알린다.
        /// ⚠ <b>원본(템플릿)의 글자를 끈다</b> — 복제는 템플릿을 그대로 베끼므로 여기서 한 번
        ///   끄면 앞으로 만들어지는 표식 전부에 적용된다. 표식마다 끄면 프레임마다 일이 생긴다.
        /// ⚠ 스프라이트 «참조» 는 MCP 로 넣을 수 없다(진행상황 8절 4번) — 그래서 씬이 아니라
        ///   <b>코드가 Resources 에서 읽어</b> 꽂는다(<c>HudTheme.Font</c> 와 같은 방식).
        /// </summary>
        void LoadMarkerSprite()
        {
            if (string.IsNullOrWhiteSpace(markerSpriteResource)) return;

            _markerSprite = Resources.Load<Sprite>(markerSpriteResource);
            if (_markerSprite == null)
            {
                Debug.LogWarning($"[유물] 느낌표 원화를 찾지 못했습니다: Resources/{markerSpriteResource} " +
                                 "— py -3 Tools/dig_marker_build.py 를 돌리세요. " +
                                 "그때까지는 글자 느낌표로 보입니다.", this);
                return;
            }

            if (markerTemplate == null) return;

            Transform label = markerTemplate.Find("Label");
            if (label != null && label.gameObject.activeSelf) label.gameObject.SetActive(false);

            // ★ 원화의 가로세로가 1:1 이 아니다(느낌표는 세로로 길다). preserveAspect 를 켜지
            //   않으면 <b>납작하게 눌린다</b>. 칸은 정사각으로 두어 누르는 넓이를 지킨다.
            if (markerTemplate.TryGetComponent(out Image templateImage))
            {
                templateImage.sprite = _markerSprite;
                templateImage.preserveAspect = true;
                templateImage.color = markerSpriteIdle;
            }
        }

        // ==================================================================
        // 자리 고르기
        // ==================================================================

        /// <summary>
        /// 맵 전체에서 발굴 가능 칸을 <see cref="digSiteCount"/> 개 고른다.
        ///
        /// 조건 <b>다섯</b>(표 Info 시트 «발굴 칸 «자리 고르기»»):
        /// <code>
        ///   ① 걸을 수 있고 벽·구조물이 없는 칸       (IsCellPlaceable)
        ///   ② 성역에서 minDistanceFromNexus 밖
        ///   ③ 성역에서 maxDistanceFromNexus 안       ★ 0 이면 <b>맵 전체</b>(직사각형 끝까지)
        ///   ④ 다른 발굴 칸과 minSpacing 이상 떨어짐
        ///   ⑤ <b>실제로 걸어갈 수 있는 칸</b>         (FlowFieldService.IsCellReachable)
        /// </code>
        ///
        /// ★★★ <b>2026-08-25 — 범위를 맵 전체로 넓혔다</b> (유저 확정:
        /// *"발굴 가능 칸 뜨는 범위 중앙 건물로부터 14타일부터 맵 끝까지(직사각형 범위 320까지)로
        /// 확장하고 맵내에 뜰 수 있는 발굴 가능 칸 확장된 구역 고려해서 재설정"*).
        ///
        /// ⚠⚠ <b>넓히면 칸 수도 같이 올려야 한다.</b> 넓이가 2.7배가 되는데 칸 수를 그대로 두면
        ///   밀도가 그만큼 묽어져 <b>성역 근처에서 하나도 안 보인다</b> — «넓혔더니 오히려
        ///   못 찾는» 것이 된다. 그래서 <b>40 → 110</b> 으로 같이 올렸다(씬 값).
        /// ★ ⑤가 이때 생겼다 — 맵 전체로 넓히면 벽에 둘러싸인 «못 가는 주머니» 가 사정권에
        ///   들어온다. 거기 놓인 표식은 <b>지시해도 아무도 도착하지 않아</b> 영영 남는다.
        ///
        /// ⚠ 조건을 만족하는 자리가 모자라면 <b>있는 만큼만</b> 둔다 — 무한 루프를 돌지 않는다.
        /// ⚠ 칸은 <b>파면 사라진다</b>. 다시 생기지 않으므로 이 숫자가 곧 한 판의 발굴 횟수 상한이다.
        /// </summary>
        public void PickSites()
        {
            _sites.Clear();
            if (digSiteCount <= 0) return;                 // 끄려고 0 을 넣은 것이다 — 조용히 넘어간다

            // ⚠⚠ <b>여기서 조용히 돌아가면 «기능이 없는 것» 이 된다</b> (2026-08-25).
            //   실제로 그랬다 — MapGenerator.MapSize 가 (0,0) 이라 아래 검사에서 빠져나갔고,
            //   그 return 이 로그보다 앞이라 <b>콘솔에 한 줄도 남지 않았다</b>.
            //   유저에게는 «발굴 기능이 구현이 안 된 것» 으로 보였다.
            //   → 못 하면 <b>반드시 이유를 말한다</b>. 실패는 조용해서는 안 된다.
            if (_map == null)
            {
                Debug.LogWarning("[유물] MapGenerator 를 찾지 못해 발굴 칸을 두지 못했습니다 — " +
                                 "발굴이 통째로 동작하지 않습니다.", this);
                return;
            }

            Vector3 nexus = NexusPosition();
            float nexusSqr = minDistanceFromNexus * minDistanceFromNexus;
            float spaceSqr = minSpacing * minSpacing;

            // ★★ 바깥 한계 — 0 이면 맵 전체(예전 동작).
            float farSqr = maxDistanceFromNexus > 0f
                ? maxDistanceFromNexus * maxDistanceFromNexus : float.PositiveInfinity;

            Vector2Int size = _map.MapSize;
            Vector2Int origin = _map.Origin;
            if (size.x <= 0 || size.y <= 0)
            {
                Debug.LogWarning($"[유물] 맵 크기가 {size} 라 발굴 칸을 두지 못했습니다 — " +
                                 "MapGenerator 의 config 가 비어 있는지 확인하세요.", this);
                return;
            }

            // ★★★ <b>걸어갈 수 있는 칸만</b> (2026-08-25). 자리 고르기를 맵 전체로 넓히면
            //   벽에 둘러싸인 «못 가는 주머니» 에 칸이 놓일 수 있다. <c>IsCellPlaceable</c> 는
            //   «벽이 아닌가» 만 보지 «거기까지 갈 수 있는가» 는 보지 않는다.
            //   못 가는 자리에 표식이 뜨면 <b>지시해도 아무도 도착하지 않는</b> 칸이 되어
            //   영영 남는다 — 유저에게는 «발굴이 또 고장났다» 로 보인다.
            //   ⚠ 플로우 필드가 아직 안 구워졌으면 이 검사를 건너뛴다(아래 TryPickWhenReady 가
            //     준비될 때까지 기다리므로 대개 준비돼 있다).
            bool useReach = _flow != null && _flow.IsReady;
            int rejectedUnreachable = 0;

            int tries = 0;
            while (_sites.Count < digSiteCount && tries < placementAttempts)
            {
                tries++;
                var local = new Vector2Int(Random.Range(0, size.x), Random.Range(0, size.y));
                Vector3Int cell = _map.LocalToCell(local);
                if (!_map.IsCellPlaceable(cell)) continue;
                if (useReach && !_flow.IsCellReachable(cell)) { rejectedUnreachable++; continue; }

                Vector3 world = _map.CellCenterWorld(cell);
                float fromNexus = ((Vector2)(world - nexus)).sqrMagnitude;
                if (fromNexus < nexusSqr) continue;
                if (fromNexus > farSqr) continue;        // ★ 너무 먼 곳은 아무도 못 간다

                bool tooClose = false;
                for (int i = 0; i < _sites.Count; i++)
                    if (((Vector2)(world - _sites[i].Center)).sqrMagnitude < spaceSqr) { tooClose = true; break; }
                if (tooClose) continue;

                _sites.Add(new DigSite { Cell = cell, Center = world,
                                         DialogueGroup = RollDialogueGroup() });
            }

            if (logChanges)
                Debug.Log($"[유물] 발굴 가능 칸 {_sites.Count}개 배치 (목표 {digSiteCount} · 시도 {tries}" +
                          (useReach ? $" · 못 가는 자리 {rejectedUnreachable}건 버림" : " · 도달 검사 없음") +
                          $" · 성역에서 {minDistanceFromNexus:0.#}~" +
                          (maxDistanceFromNexus > 0f ? $"{maxDistanceFromNexus:0.#}타일" : "맵 끝까지") + ")", this);
            if (_sites.Count < digSiteCount)
                Debug.LogWarning($"[유물] 조건에 맞는 자리가 모자라 {_sites.Count}개만 두었습니다 — " +
                                 "minSpacing / minDistanceFromNexus 를 줄여보세요.", this);
        }

        Vector3 NexusPosition()
        {
            var nexus = FindAnyObjectByType<Nexus>();
            return nexus != null ? nexus.transform.position : Vector3.zero;
        }

        // ==================================================================
        // 매 프레임
        // ==================================================================

        /// <summary>대사 묶음 하나를 고른다. 표가 없으면 0(창이 Fallback 문구로 뜬다).</summary>
        int RollDialogueGroup() =>
            _dialogueGroups.Count > 0 ? _dialogueGroups[Random.Range(0, _dialogueGroups.Count)] : 0;

        /// <summary>아직 자리를 안 골랐다 — 플로우 필드가 준비되기를 기다리는 중.</summary>
        bool _pickPending;

        /// <summary>이 시각까지도 준비가 안 되면 <b>그냥 고른다</b>(플로우 필드가 없는 씬).</summary>
        float _pickDeadline;

        FlowFieldService _flow;

        /// <summary>
        /// ★ 플로우 필드가 준비되면 자리를 고른다. 기다려도 안 되면(그런 씬이면)
        /// <see cref="pickWaitSeconds"/> 뒤에 <b>도달 검사 없이</b> 고른다 —
        /// 기능이 통째로 죽는 것보다 낫다.
        /// </summary>
        void TryPickWhenReady()
        {
            if (_flow == null) _flow = FindAnyObjectByType<FlowFieldService>();

            bool ready = _flow != null && _flow.IsReady;
            if (!ready && Time.unscaledTime < _pickDeadline) return;

            _pickPending = false;
            if (!ready)
                Debug.LogWarning("[유물] 플로우 필드가 준비되지 않아 <b>도달 검사 없이</b> " +
                                 "발굴 칸을 골랐습니다 — 못 가는 자리가 섞일 수 있습니다.", this);
            PickSites();
        }

        void Update()
        {
            if (_pickPending) TryPickWhenReady();

            UpdateReveal();
            UpdateMarkers();
            WatchWave();

            // ★ 문턱형 유물(「두꺼워진 가피」)을 여기서 함께 재운다 — 그 하나 때문에
            //   MonoBehaviour 를 새로 두지 않는다(RelicEffectService.Tick 의 주석).
            RelicEffectService.Tick();
        }

        /// <summary>
        /// 시야에 들어온 칸을 <b>발견</b>으로 표시한다.
        ///
        /// ★ <b>안개가 걷힌 것만으로는 안 된다</b> — 유저 지시가 «캐릭터의 시야에 보일 경우» 다.
        ///   그래서 <see cref="FogOfWarService.IsVisible"/>(지금 누군가의 시야 안인가)를 본다.
        ///   ⚠ 한 번 본 칸은 <b>계속 보인다</b>(기억) — 시야를 벗어날 때마다 느낌표가
        ///   사라지면 «분명 봤는데 없어졌다» 가 된다.
        /// </summary>
        /// <summary>
        /// ★★ <b>웨이브 계열 유물의 통로</b> (2026-08-24 · 표 Ver02 의
        /// <c>relic_wave_shield</c> · <c>relic_wave_energy</c> · <c>relic_wave_heal</c>).
        ///
        /// <b>왜 여기서 보나</b> — <see cref="Wave.WaveManager"/> 의 이벤트는 <b>정적이 아니라
        /// 인스턴스</b> 것이라 <see cref="RelicEffectService"/>(정적 클래스)가 붙을 수 없다.
        /// 매니저가 유물을 알아야 하게 만드는 것도 방향이 거꾸로다(유물은 나중에 생긴 것이다).
        /// 그래서 <b>이미 매 프레임 도는 이 서비스</b>가 웨이브 번호가 바뀌는 것을 보고
        /// 알려준다 — 「두꺼워진 가피」를 이 <c>Update</c> 에 얹은 것과 <b>같은 이유</b>다.
        ///
        /// ⚠ 첫 프레임(<c>_seenWave &lt; 0</c>)에는 «바뀌었다» 로 보지 않는다 —
        ///   판을 켜자마자 보호막이 공짜로 걸리면 안 된다.
        /// </summary>
        void WatchWave()
        {
            // ⚠ WaveManager 에는 static Instance 가 없다 — 한 번 찾아서 들고 있는다.
            if (_wave == null)
            {
                _wave = FindAnyObjectByType<Wave.WaveManager>();
                if (_wave == null) return;
            }

            int now = _wave.WaveNumber;
            if (now == _seenWave) return;

            bool first = _seenWave < 0;
            _seenWave = now;
            if (first) return;

            RelicEffectService.OnWaveEnded();     // 지난 웨이브가 끝났고
            RelicEffectService.OnWaveSpawned();   // 새 웨이브가 시작된다
        }

        /// <summary>
        /// ★★★ <b>«가 본 자리» 면 찾은 것으로 본다</b> (2026-08-25 · 유저 지시:
        /// *"발굴 다시 가능하게 만들고 느낌표 넣고 발굴 가능 칸에 바운스효과와 함께"*).
        ///
        /// ══════════════════════════════════════════════════════════════
        ///  왜 바꿨나 — <b>«지금 보이는가» 로는 사실상 아무도 못 찾는다</b>
        /// ══════════════════════════════════════════════════════════════
        /// 예전 판정은 <c>IsVisible</c>(«<b>지금</b> 누군가의 시야 칸인가»)였다. 그런데
        /// 발굴 칸은 <b>24개</b>가 320×320(102,400칸) 위에 흩어져 있고 서로 10칸 이상
        /// 떨어져 있다. 캐릭터가 <b>그 위를 지나가는 순간</b> 시야에 들어와야만 찾아지는데,
        /// 탐험은 안개를 걷으며 <b>지나가 버리는</b> 것이라 그 순간을 놓치면 끝이다 —
        /// 안개는 이미 걷혔고, 다시는 «지금 보이는» 상태가 되지 않는다.
        ///
        /// ★ <b>이 프로젝트는 같은 결론을 이미 한 번 냈다.</b>
        ///   <see cref="Units.EpicSubjugationService"/> 가 «폴리르만 안 뜬다» 를 쫓다가
        ///   <c>IsVisibleWorld</c> → <c>IsExploredWorld</c> 로 바꾼 그 판단과 <b>같은 것</b>이다.
        ///   이 게임의 안개는 «한 번 밝히면 지형이 기억되는» 방식이고, «찾았다» 도 같은
        ///   성질이어야 앞뒤가 맞는다.
        ///
        /// ⚠ <b>«한꺼번에 다 뜨는» 것이 아니다</b> — 밝힌 자리에 있는 칸만 뜬다. 맵을
        ///   넓혀 갈수록 표식이 하나씩 늘고, 그것이 «탐험의 보상» 으로 읽힌다.
        /// ⚠ 로그는 <b>여전히 한 칸에 한 번</b>이다(<c>Revealed</c> 로 막는다).
        /// </summary>
        void UpdateReveal()
        {
            if (_fog == null || !_fog.IsReady) return;
            for (int i = 0; i < _sites.Count; i++)
            {
                DigSite s = _sites[i];
                if (s.Revealed) continue;
                if (!_fog.IsExplored(s.Cell)) continue;

                s.Revealed = true;
                HudLog.Add("발굴할 수 있는 자리를 찾았습니다", HudLogKind.Good);
            }
        }

        // ==================================================================
        // 표식 (UI 버튼)
        // ==================================================================

        void UpdateMarkers()
        {
            if (overlayParent == null || markerTemplate == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            int need = 0;
            for (int i = 0; i < _sites.Count; i++) if (_sites[i].Revealed) need++;

            while (_markers.Count < need)
            {
                RectTransform clone = Instantiate(markerTemplate, overlayParent);
                clone.name = $"DigMarker_{_markers.Count + 1}";
                _markers.Add(clone);
            }

            int slot = 0;
            for (int i = 0; i < _sites.Count; i++)
            {
                DigSite s = _sites[i];
                if (!s.Revealed) continue;

                RectTransform item = _markers[slot++];
                Vector3 screen = _camera.WorldToScreenPoint(s.Center);
                if (screen.z < 0f)
                {
                    if (item.gameObject.activeSelf) item.gameObject.SetActive(false);
                    continue;
                }

                if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    overlayParent, screen, null, out Vector2 local);
                // ★ 파는 중에는 튀지 않는다 — 이미 사람이 가고 있으니 <b>주의를 끌 일이 끝났다</b>.
                item.anchoredPosition = local + new Vector2(0f, s.Ordered ? 0f : BounceOffset(slot - 1));
                item.sizeDelta = new Vector2(markerPixels, markerPixels);

                var img = item.GetComponent<Image>();
                if (img != null)
                {
                    if (_markerSprite != null)
                    {
                        // ⚠ 복제가 템플릿보다 먼저 만들어졌을 수 있다(저장된 씬의 값) —
                        //   그래서 여기서도 한 번 확인한다. 같으면 아무 일도 하지 않는다.
                        if (img.sprite != _markerSprite)
                        {
                            img.sprite = _markerSprite;
                            img.preserveAspect = true;
                        }
                        img.color = s.Ordered
                            ? Color.Lerp(markerSpriteDigging, markerSpriteIdle, s.Ratio(DigSeconds))
                            : markerSpriteIdle;
                    }
                    else
                    {
                        // 원화가 없을 때의 옛 모양 — 파는 중이면 진행도를 색으로 보여준다.
                        img.color = s.Ordered
                            ? Color.Lerp(orderedColor, Color.white, s.Ratio(DigSeconds))
                            : idleColor;
                    }
                }

                // ⚠ 버튼은 <b>매 프레임 다시 배선한다</b> — 표식은 자리마다 재사용되므로
                //   («다섯 번째 표식» 이 프레임마다 다른 자리를 가리킬 수 있다) 지난 프레임의
                //   람다가 남아 있으면 <b>엉뚱한 자리를 판다</b>.
                var button = item.GetComponent<Button>();
                if (button != null)
                {
                    DigSite captured = s;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => Open(captured));
                    button.interactable = !captured.Ordered;
                }
            }

            for (int i = slot; i < _markers.Count; i++)
                if (_markers[i].gameObject.activeSelf) _markers[i].gameObject.SetActive(false);
        }

        /// <summary>
        /// ★★ <b>통통 튀는 높이</b> (2026-08-25 · 유저 지시: *"통통 튀게 해줘 주의를 끌 수 있도록"*).
        ///
        /// <b>왜 사인 곡선이 아닌가</b> — <c>Sin</c> 은 위아래로 <b>고르게 흐물거린다</b>.
        /// 통통 튀는 것으로 보이려면 <b>바닥에서 쉬는 시간</b>이 있어야 한다. 실제로 튀는 공이
        /// 그렇다 — 잠깐 솟았다가 내려와 <b>머문다</b>. 그래서 «공중에 있는 동안» 만
        /// 포물선(<c>4x(1-x)</c>)을 그리고 나머지 시간은 <b>0</b>으로 둔다.
        ///
        /// <code>
        ///   |    ▁▄█▄▁          ▁▄█▄▁          ▁▄█▄▁
        ///   |____      __________     __________     ____   ← 이 «쉼» 이 통통의 정체다
        ///        공중         바닥
        /// </code>
        ///
        /// ★ 표식마다 <b>때를 어긋나게</b> 한다(<see cref="bouncePhaseStep"/>) — 여럿이
        ///   한꺼번에 튀면 기계처럼 보이고, 어긋나면 살아 있는 것처럼 보인다.
        /// ⚠ <see cref="Time.unscaledTime"/> 을 쓴다 — 조언 카드나 일시정지로 시간이 멈춰도
        ///   표식은 계속 튀어야 «누를 수 있는 것» 으로 읽힌다(멈춘 동안에도 누를 수 있다).
        /// </summary>
        float BounceOffset(int index)
        {
            if (bounceHeight <= 0f || bouncePeriod <= 0f) return 0f;

            float t = Mathf.Repeat(Time.unscaledTime + index * bouncePhaseStep, bouncePeriod)
                    / bouncePeriod;
            if (t >= bounceAirRatio) return 0f;          // 바닥에서 쉬는 구간

            float x = t / bounceAirRatio;                 // 공중에 있는 동안을 0~1 로
            return 4f * x * (1f - x) * bounceHeight;      // 포물선 — 올랐다 내린다
        }

        /// <summary>
        /// ★★ <b>느낌표를 눌렀다 — 곧바로 파지 않고 «묻는다»</b> (2026-08-24 · 유저 지시:
        /// <i>"유물 자동 발굴 되게 하지말고 … 해당 칸을 누를 경우 발굴 ui가 나와서
        /// 발굴하기를 누르면 가장 가까운 캐릭터가 가서 발굴하게"</i>).
        ///
        /// 창이 없으면(씬을 아직 안 만들었으면) <b>예전처럼 곧바로 지시한다</b> —
        /// UI 하나 때문에 기능이 통째로 죽으면 안 된다.
        /// </summary>
        public void Open(DigSite site)
        {
            if (site == null || site.Ordered) return;

            var panel = UI.RelicDigPanel.Instance;
            if (panel == null) { Confirm(site); return; }

            panel.PresentSite(site, _dialogue);
        }

        /// <summary>창에서 «파러 간다» 를 골랐다 — 이제야 지시가 나간다.</summary>
        public void Confirm(DigSite site)
        {
            if (site == null || site.Ordered) return;
            site.Ordered = true;
            HudLog.Add("발굴을 지시했습니다 — 가장 가까운 캐릭터가 갑니다", HudLogKind.Good);
        }

        // ==================================================================
        // 배정 · 진행 (건설과 같은 규칙)
        // ==================================================================

        /// <summary>
        /// 이 캐릭터가 맡은 발굴 자리. 없으면 null.
        /// <see cref="LastSanctuary.Buildings.BuildService.AssignedSiteFor"/> 와 같은 구조다 —
        /// 배정은 <b>프레임당 한 번</b> 전체를 보고 정한다(캐릭터마다 스스로 고르면 한 자리에 몰린다).
        /// </summary>
        public DigSite AssignedSiteFor(CharacterBehavior worker)
        {
            if (worker == null || _sites.Count == 0) return null;

            if (_assignFrame != Time.frameCount)
            {
                _assignFrame = Time.frameCount;
                AssignDiggers();
            }

            for (int i = 0; i < _sites.Count; i++)
                if (_sites[i].Digger == worker) return _sites[i];
            return null;
        }

        void AssignDiggers()
        {
            _freeWorkers.Clear();
            var units = UnitRegistry.All;
            for (int i = 0; i < units.Count; i++)
            {
                DamageableUnit u = units[i];
                if (u == null || !u.IsAlive || u.Kind != UnitKind.Character) continue;

                // ★★ <b>소환수(아루의 골렘)는 발굴하지 않는다</b> (2026-08-25 · 유저 지시:
                //   *"골렘은 발굴 가능 대상에서 빼줘"*).
                //
                //   골렘은 <c>UnitKind.Character</c> 라서 위 검사를 <b>통과했다</b> — 그래서
                //   가장 가까운 «캐릭터» 로 뽑혀 삽질을 하러 갔다. 골렘은 전술을 바꿀 수도,
                //   후퇴할 수도, 침식이 차지도 않는 «싸우기만 하는» 유닛이다(「강림」 정의문).
                //   심부름을 시킬 대상이 아니고, 아루가 죽으면 <b>같이 사라져</b> 파던 자리가
                //   담당자 없이 남는다.
                //   ⚠ 같은 판단이 이미 세 곳에 있다 — 승리 화면의 인원 수 · 침식 칸 ·
                //     엔딩 명단. <see cref="CharacterUnit.IsSummoned"/> 의 긴 주석 참조.
                if (u is CharacterUnit c && c.IsSummoned) continue;

                var worker = u.GetComponent<CharacterBehavior>();
                if (worker != null && worker.CanTakeBuildOrder) _freeWorkers.Add(worker);
            }

            // 유지되는 배정은 후보에서 뺀다(한 명이 두 자리를 맡지 않게).
            for (int i = 0; i < _sites.Count; i++)
            {
                DigSite s = _sites[i];
                if (s.Digger == null) continue;
                if (!s.Ordered || !_freeWorkers.Remove(s.Digger)) s.Digger = null;
            }

            // 남은 자리 × 남은 후보 중 <b>가장 가까운 짝</b>부터 (유저 지시: «가장 가까운 캐릭터»).
            while (_freeWorkers.Count > 0)
            {
                DigSite bestSite = null;
                CharacterBehavior bestWorker = null;
                float bestSqr = float.PositiveInfinity;

                for (int i = 0; i < _sites.Count; i++)
                {
                    DigSite s = _sites[i];
                    if (!s.Ordered || s.Digger != null) continue;

                    for (int w = 0; w < _freeWorkers.Count; w++)
                    {
                        CharacterBehavior worker = _freeWorkers[w];
                        float sqr = ((Vector2)(s.Center - worker.transform.position)).sqrMagnitude;
                        if (sqr >= bestSqr) continue;
                        bestSite = s;
                        bestWorker = worker;
                        bestSqr = sqr;
                    }
                }

                if (bestWorker == null) break;
                bestSite.Digger = bestWorker;
                _freeWorkers.Remove(bestWorker);
            }
        }

        /// <summary>캐릭터가 이 거리 안에 들어와야 삽질이 진행된다.</summary>
        public float WorkRange => digWorkRange;

        /// <summary>
        /// ★ <b>미니맵도 같은 리듬으로 튄다</b> (2026-08-25 · 유저 지시:
        /// *"발굴 가능 칸이 발견되면 느낌표를 미니맵에도 추가해줘"*).
        ///
        /// ⚠ 튀는 계산을 미니맵에 <b>복사하지 않는다</b> — 두 벌이 되면 인스펙터에서 주기를
        ///   바꿨을 때 한쪽만 따라간다. 계산의 정본은 여기 하나다.
        /// <paramref name="scale"/> 로 크기만 줄여 쓴다(미니맵은 좁아서 9px 이 과하다).
        /// </summary>
        public float BounceFor(int index, float scale = 1f) => BounceOffset(index) * scale;

        /// <summary>미니맵 표식이 쓸 원화. 못 구웠으면 null(그때는 점으로 그린다).</summary>
        public Sprite MarkerSprite => _markerSprite;

        /// <summary>
        /// 현장에서 <paramref name="seconds"/> 만큼 판다. 한 자리에 한 명이므로 사람이 늘어도
        /// 빨라지지 않는다 — 대신 <see cref="RelicEffectType.DigSpeed"/> 유물이 빠르게 한다.
        /// </summary>
        public void Contribute(DigSite site, float seconds, CharacterUnit digger)
        {
            if (site == null || seconds <= 0f || !_sites.Contains(site)) return;

            site.Progress += seconds * RelicEffectService.DigSpeedMultiplier(digger);
            if (site.Progress < DigSeconds) return;

            Complete(site, digger);
        }

        // ==================================================================
        // 결과
        // ==================================================================

        void Complete(DigSite site, CharacterUnit digger)
        {
            _sites.Remove(site);

            DigOutcomeRow row = _table != null ? _table.Roll() : null;
            if (row == null)
            {
                HudLog.Add("발굴을 마쳤지만 아무것도 나오지 않았습니다");
                return;
            }

            row = PromoteIfExhausted(row);

            string what = ApplyOutcome(row, digger);
            string who = digger != null ? digger.DisplayName : "누군가";
            HudLog.Add(string.IsNullOrEmpty(what)
                           ? $"{who} — 발굴: {row.Script}"
                           : $"{who} — 발굴: {what}",
                       what.StartsWith("−") || row.outcomeType == "dig_hurt" ||
                       row.outcomeType == "dig_erosion_up"
                           ? HudLogKind.Warn
                           : HudLogKind.Good);

            // ★ 결과 창 — 같은 대사 묶음의 result 줄 + 표의 결과 문구(2026-08-24).
            var panel = UI.RelicDigPanel.Instance;
            if (panel != null)
            {
                string flavor = _dialogue != null
                    ? _dialogue.Roll(site.DialogueGroup, RelicDialogueSituation.Result) : "";
                if (string.IsNullOrWhiteSpace(flavor))
                    flavor = RelicDialogueTableSO.Fallback(RelicDialogueSituation.Result);

                string line = row.Script;
                if (!string.IsNullOrEmpty(what)) line += "\n" + what;
                panel.PresentResult(flavor, line, _lastGrantedIcon);
            }
            _lastGrantedIcon = null;

            if (logChanges) Debug.Log($"[유물] 발굴 완료 {site.Cell} → {row.outcomeType}", this);
        }

        /// <summary>승급을 한 판에 <b>한 번만</b> 알리기 위한 표시.</summary>
        bool _promotionAnnounced;

        /// <summary>
        /// ★★★ <b>일반을 다 모았으면 그 자리에서 에픽을 굴린다</b> (2026-08-26 · 유저 지시:
        /// *"일반 등급 다 뽑으면 에픽 등급 굴리는걸로"*).
        ///
        /// ══════════════════════════════════════════════════════════════════
        ///  왜 — <b>발굴 결과의 14%가 통째로 죽어 있었다</b>
        /// ══════════════════════════════════════════════════════════════════
        /// 160절이 중복 획득을 막으면서 «다 모으면 그 등급이 마른다» 가 생겼다. 그런데
        /// <b>일반은 한 판에 반드시 마른다</b> — 웨이브 몬스터만 30웨이브에 1,792마리이고
        /// 처치 드랍이 1.2%라 <b>기대 21.5회</b>, 여기에 발굴 6.3 · 중립 사냥 2~5 를 더하면
        /// 30회를 넘는데 일반 풀은 <b>24종</b>뿐이다(대략 26~27웨이브에 마른다).
        /// 그 뒤로는 <c>dig_relic_common</c>(14%)이 «이미 다 모았습니다» 한 줄로 끝났다.
        ///
        /// ★ <b>«다 모았다» 를 벌이 아니라 보상으로 바꾼다.</b> 마른 자리를 에픽(발굴 전용)이
        ///   물려받으므로 일반 24종을 다 모은 판일수록 발굴이 <b>더</b> 값어치 있어진다.
        /// ★ <b>유물 종수를 늘리지 않아도 되는 이유가 이것이다</b> — 풀을 넓히면 개별 유물이
        ///   나올 확률만 옅어진다(특정 레어가 한 판에 나올 확률은 이미 27%다).
        ///
        /// ⚠ <b>레어를 건너뛴다.</b> 레어는 한 판에 5~6개밖에 안 들어와 마르지 않으므로
        ///   «마른 등급을 물려받는» 자리가 아니다. 일반 → <b>에픽</b> 이 유저가 정한 규칙이다.
        /// ⚠ <b>«발굴 전용» 에픽 풀에서만 굴린다</b> — 공용 풀(<c>_commonPool</c>)에는 에픽이
        ///   아예 없다(에픽은 보스 전용 · 발굴 전용 · 사건 전용으로만 나뉜다). 그래서 이
        ///   승급은 <b>발굴에서만</b> 일어나고, 처치 드랍은 이 문을 지나지 않는다 —
        ///   처치로 «발굴 전용» 이 나오면 그 이름이 곧 거짓이 된다.
        /// ⚠ <b>에픽마저 말랐으면 승급하지 않는다</b> — 그때는 원래대로 «일반 유물은 이미 다
        ///   모았습니다» 가 뜬다(<see cref="GrantRelic"/>). 그것도 사실이다.
        /// ★ <b>문자열이 아니라 «행» 을 갈아끼운다</b> — 결과 창은 <c>row.Script</c> 를 함께
        ///   보여주므로, 행을 안 갈면 에픽을 주면서 일반의 대사가 남는다.
        /// </summary>
        DigOutcomeRow PromoteIfExhausted(DigOutcomeRow row)
        {
            if (row == null || row.outcomeType != "dig_relic_common") return row;
            if (RelicRegistry.HasRemaining(RelicGrade.Common)) return row;
            if (!RelicRegistry.HasRemaining(RelicGrade.Epic, true)) return row;

            DigOutcomeRow epic = _table != null ? _table.Find("dig_relic_epic") : null;
            if (epic == null) return row;

            // ★ 한 번만 알린다 — 매 발굴마다 뜨면 로그가 묻힌다(등록기의 경고와 같은 규칙).
            //   조용히 바꾸면 «에픽이 왜 이렇게 자주 나오지» 가 되어 규칙을 못 읽는다.
            if (!_promotionAnnounced)
            {
                _promotionAnnounced = true;
                HudLog.Add("일반 유물을 모두 모았습니다 — 이제 그 자리에서 에픽을 찾습니다",
                           HudLogKind.Good);
            }

            if (logChanges) Debug.Log("[유물] 발굴 승급 — dig_relic_common → dig_relic_epic", this);
            return epic;
        }

        /// <summary>
        /// ★★★ <b>발굴 에너지를 «판이 익은 정도» 에 비례해 키운다</b> (2026-08-25).
        ///
        /// ══════════════════════════════════════════════════════════════════
        ///  왜 — <b>발굴이 손해 없이 너무 좋았다</b>
        /// ══════════════════════════════════════════════════════════════════
        /// 유저 리포트: *"발굴 가능 칸이 너무 많고, 밸류가 너무 쎄서 발굴이 너무 좋음.
        /// 에너지가 한번에 무지막지하게 많이 제공되니까 별도의 손해 없이. …
        /// <b>타이머를 설정해서 점진적으로 보상량을 올리는 방법을 고려해봐.</b>"*
        ///
        /// 예전 수치로 계산해 보면 —
        /// <code>
        ///   기대 에너지/발굴 = 0.29×120 + 0.06×300 = 52.8
        ///   칸 110개        → 한 판에 <b>5,808</b>
        ///   캐릭터 생성 비용 = 150 + 100n  (몬스터 한 마리 = 10)
        /// </code>
        /// 발굴만으로 캐릭터 열 명을 뽑는다. 게다가 한 번에 300이 들어오는데
        /// 몬스터 한 마리가 10이니 <b>서른 마리를 한 삽에</b> 캐는 셈이었다.
        ///
        /// ══════════════════════════════════════════════════════════════════
        ///  ★ 시계를 «시간» 이 아니라 <b>«웨이브»</b> 로 잡았다
        /// ══════════════════════════════════════════════════════════════════
        /// 유저가 말한 «타이머» 를 <b>벽시계 초</b>로 읽으면 두 군데서 깨진다:
        /// <code>
        ///   ① 저장·복원 — 흐른 시간은 저장하지 않는다. 불러오면 <b>0으로 되돌아가</b>
        ///      20웨이브에서 이어했는데 보상이 1웨이브 값이 된다. 웨이브 번호는 저장한다.
        ///   ② 배속 — 이 게임은 배속이 있다(HUD_Speed). 8배속으로 돌리면 벽시계는 같은데
        ///      <b>실제 진행은 여덟 배</b>라 «시간당 보상» 이 진행도와 어긋난다.
        /// </code>
        /// 그리고 웨이브는 <b>난이도의 척도</b>이기도 하다 — «판이 어려워질수록 보상이 커진다» 가
        /// «오래 켜 두면 커진다» 보다 이 게임이 원하는 규칙이다.
        /// ⚠ 그래서 «점진적으로 올린다» 는 지시는 지켰고, 그 <b>기준만</b> 웨이브로 바꿨다.
        ///
        /// ══════════════════════════════════════════════════════════════════
        ///  곡선
        /// ══════════════════════════════════════════════════════════════════
        /// <code>
        ///   배율 = 1 + growthPerWave × (웨이브 − 1)      (최대 growthCap 까지)
        ///
        ///   기본값 0.10 · 상한 2.5 · 표 값 40/100 이면 —
        ///     1웨이브   ×1.00 →  40 / 100
        ///    10웨이브   ×1.90 →  76 / 190
        ///    16웨이브~  ×2.50 → 100 / 250   (상한)
        ///
        ///   기대 에너지/발굴 = 0.24×기본 + 0.03×큰것   (아래 표의 새 가중치)
        ///     1웨이브 12.6 · 20웨이브 31.5 · 판 평균 ≈ 22
        ///   칸 45개 → 한 판에 <b>약 990</b> (예전의 6분의 1)
        /// </code>
        /// 초반 발굴은 «덤» 이고 후반 발굴은 «값어치» 가 된다 — 유저가 요구한 모양이다.
        ///
        /// ⚠ <b>에너지에만 건다.</b> 체력·침식 결과는 «최대치의 %» 라 배율을 곱하면
        ///   뜻이 이상해지고(120% 회복), 그것들은 애초에 문제가 아니었다.
        /// ⚠ <see cref="WaveManager"/> 를 못 찾으면 배율 1 이다 — 테스트 씬에서 조용히 돈다.
        /// </summary>
        int ScaledEnergy(int baseValue)
        {
            if (baseValue <= 0) return baseValue;

            if (_wave == null) _wave = FindAnyObjectByType<Wave.WaveManager>();
            int wave = _wave != null ? _wave.WaveNumber : 1;

            float mult = 1f + energyGrowthPerWave * Mathf.Max(0, wave - 1);
            mult = Mathf.Clamp(mult, 1f, Mathf.Max(1f, energyGrowthCap));

            return Mathf.Max(1, Mathf.RoundToInt(baseValue * mult));
        }

        /// <summary>
        /// 발굴 결과 한 줄을 적용한다 — 표 <c>DigOutcome</c> 시트의 <c>outcome_type</c> 과 1:1.
        ///
        /// ⚠ <b>여기 없는 타입은 «아무 일도 안 일어난다»</b>. 표에 새 결과를 더하면
        ///   반드시 가지도 함께 더할 것(등록기가 유물 효과에 대해 하는 경고와 같은 규칙).
        /// </summary>
        string ApplyOutcome(DigOutcomeRow row, CharacterUnit digger)
        {
            switch (row.outcomeType)
            {
                case "dig_energy":
                case "dig_energy_big":
                {
                    // ★★ 에너지만 «판이 익을수록» 커진다 (2026-08-25 · ScaledEnergy 의 설명).
                    int amount = ScaledEnergy(row.value01);
                    Resource.ResourceManager.Instance?.AddEnergy(amount);
                    return $"에너지 +{amount}";
                }

                case "dig_heal":
                {
                    if (digger == null || !digger.IsAlive) return "";
                    int amount = Mathf.Max(1, Mathf.RoundToInt(digger.MaxHp * row.value01 * 0.01f));
                    digger.Heal(amount);
                    return $"체력 +{row.value01}%";
                }

                case "dig_hurt":
                {
                    if (digger == null || !digger.IsAlive) return "";
                    int amount = Mathf.Max(1, Mathf.RoundToInt(digger.MaxHp * row.value01 * 0.01f));
                    // ⚠ 표: *"이 피해로 죽지 않습니다"* — 체력 1 을 남긴다(이벤트 보상과 같은 규칙).
                    int safe = Mathf.Min(amount, Mathf.Max(0, digger.CurrentHp - 1));
                    if (safe > 0) digger.ApplyDamage(safe);
                    return $"체력 −{row.value01}%";
                }

                case "dig_erosion_up":
                case "dig_erosion_down":
                {
                    var er = digger != null ? CharacterErosion.Of(digger) : null;
                    if (er == null) return "";
                    bool up = row.outcomeType == "dig_erosion_up";
                    er.AddErosion(up ? row.value01 : -row.value01);
                    return up ? $"침식 +{row.value01}" : $"침식 −{row.value01}";
                }

                case "dig_nothing":
                    return "";

                case "dig_relic_common": return GrantRelic(RelicGrade.Common, false);
                case "dig_relic_rare":   return GrantRelic(RelicGrade.Rare, false);
                case "dig_relic_epic":   return GrantRelic(RelicGrade.Epic, true);
            }
            return "";
        }

        /// <summary>방금 발굴로 얻은 유물의 아이콘 — 결과 창이 그린다. 없으면 null.</summary>
        Sprite _lastGrantedIcon;

        /// <summary>
        /// ★★ <b>이미 가진 유물은 나오지 않는다</b> (2026-08-25 · 유저 지시:
        /// *"유물 중복 획득 안되게 수정해줘"*). 거르는 일은 <see cref="RelicRegistry.RollGrade"/>
        /// 가 하고, 여기서는 <b>다 가졌을 때</b> 를 말로 알린다 — 조용히 «아무것도 안 나옴» 이면
        /// 유저가 «발굴이 또 고장났나» 로 읽는다(위 PickSites 의 ⚠⚠ 와 같은 교훈).
        /// </summary>
        string GrantRelic(RelicGrade grade, bool digOnly)
        {
            RelicDefinitionSO relic = RelicRegistry.RollGrade(grade, digOnly);
            if (relic == null)
                return $"{RelicDefinitionSO.NameOf(grade)} 유물은 이미 다 모았습니다";

            RelicInventory.Instance?.Grant(relic);
            _lastGrantedIcon = relic.icon;
            return $"유물 「{relic.DisplayName}」 ({RelicDefinitionSO.NameOf(grade)})";
        }

        // ==================================================================
        // 세이브
        // ==================================================================

        /// <summary>세이브용 — (칸 x, 칸 y, 발견 여부, 진행도 ×100) 로 접는다.</summary>
        public List<Vector4> Capture()
        {
            var list = new List<Vector4>(_sites.Count);
            for (int i = 0; i < _sites.Count; i++)
            {
                DigSite s = _sites[i];
                list.Add(new Vector4(s.Cell.x, s.Cell.y,
                                     (s.Revealed ? 1 : 0) + (s.Ordered ? 2 : 0),
                                     s.Progress));
            }
            return list;
        }

        /// <summary>이어하기 — 저장된 자리를 되살린다. 없으면 새로 고른다.</summary>
        public void Restore(List<Vector4> saved)
        {
            if (saved == null || saved.Count == 0)
            {
                if (_sites.Count == 0) PickSites();
                return;
            }

            _sites.Clear();
            if (_map == null) _map = FindAnyObjectByType<MapGenerator>();
            for (int i = 0; i < saved.Count; i++)
            {
                Vector4 v = saved[i];
                var cell = new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), 0);
                int flags = Mathf.RoundToInt(v.z);
                _sites.Add(new DigSite
                {
                    Cell = cell,
                    Center = _map != null ? _map.CellCenterWorld(cell) : Vector3.zero,
                    Revealed = (flags & 1) != 0,
                    Ordered = (flags & 2) != 0,
                    Progress = v.w,
                    // ⚠ 대사 묶음은 <b>저장하지 않는다</b> — 세이브 형식(Vector4)에 칸이 없고,
                    //   말투가 이어하기 뒤에 달라지는 것은 «틀린» 것이 아니다.
                    //   자리·진행도처럼 «맞아야 하는» 값과 구분한다.
                    DialogueGroup = RollDialogueGroup(),
                });
            }
            if (logChanges) Debug.Log($"[유물] 발굴 칸 {_sites.Count}개 복원", this);
        }
    }
}
