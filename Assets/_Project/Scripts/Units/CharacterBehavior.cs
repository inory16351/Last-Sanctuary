using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Fog;
using LastSanctuary.Map;
using LastSanctuary.Wave;

namespace LastSanctuary.Units
{
    /// <summary>캐릭터가 지금 무엇을 하려는지.</summary>
    public enum CharacterDuty
    {
        /// <summary>탐험 — 미탐사 지역으로 나가 전장을 밝힌다.
        /// <b>사냥·정찰·탐색 세 유형을 모두 아우르는 상위 임무</b>다(유저 확정 용어) —
        /// 중립 몬스터를 어떻게 대할지는 전술 지침의 <b>탐험 유형</b>이 따로 정하고,
        /// 이 임무는 "돌아다닌다"까지만 뜻한다.</summary>
        Expedition,

        Guard,   // 방어 — 웨이브에 대비해 넥서스 주변을 돈다
        Rally,   // 집결 — 웨이브 소환 이후, 넥서스 대신 지정된 집결지 구역을 지킨다

        /// <summary>건설 — 플레이어가 찍어둔 건설 예정지로 가서 건물을 짓는 중.
        /// 웨이브 타임(전투·광폭화)에는 잡히지 않는다 — 그때는 싸우는 게 먼저다.</summary>
        Build,

        /// <summary>후퇴 — 전술 지침의 "후퇴 판단 기준" 이하로 체력이 떨어져 넥서스로 물러난 상태.
        /// 다른 모든 임무보다 우선하며, 회복될 때까지 싸우지 않는다.</summary>
        Retreat,

        /// <summary>도망 — 탐험 유형이 <b>'탐색'</b> 인 캐릭터가 선공 중립 몬스터에게 맞았을 때.
        /// 반격하지 않고 그 자리를 벗어난다(유저 확정 2026-08-12). 체력 후퇴와 달리
        /// <b>넥서스로 돌아가지 않고</b> 때린 상대의 반대 방향으로만 물러난다.</summary>
        Flee,

        /// <summary>확인 — <b>시야 밖</b>에서 날아온 공격의 출처를 보러 가는 중
        /// (유저 지시 2026-08-13). <b>전방 포지션 캐릭터만</b> 맡는다.
        /// 안 보이는 적은 때릴 수 없게 막은 대신 둔 반응이다 —
        /// <see cref="LastSanctuary.Combat.SightAlertService"/> 참조.</summary>
        Investigate,
    }

    /// <summary>
    /// 캐릭터의 자율 이동. 전투 자체는 <see cref="UnitCombat"/> 가 맡고,
    /// 이 컴포넌트는 "타겟이 없을 때 어디로 갈지"만 정한다.
    ///
    /// <see cref="UnitCombat"/> 는 타겟이 없으면 귀환 지점으로 걸어가므로,
    /// 귀환 지점을 옮기는 것이 곧 이동 명령이 된다. 덕분에 이동 코드가 두 벌로
    /// 갈라지지 않는다.
    ///
    ///   대기시간(Preparation) → 탐험: 아직 안 밝혀진 칸을 찾아 나간다
    ///   그 외(방어 시점)       → 집결지가 있으면 그 구역을 경계, 없으면 넥서스 주변을 경계
    ///   단 웨이브 반응이 '탐험 우선'이면 웨이브 중에도 탐험을 유지하고 집결지를 무시한다
    ///
    /// 적을 발견하면 UnitCombat 이 알아서 교전하고, 이 컴포넌트는 교전이 끝날
    /// 때까지 목적지를 건드리지 않는다.
    ///
    /// <b>집결지 반영 시점 — "웨이브 몬스터 소환 직후"</b>: 플레이어는 대기시간 중에도
    /// 미리 집결지를 찍어둘 수 있지만, 실제로 그쪽으로 움직이는 건 방어 임무로
    /// 넘어가는 순간부터다. <see cref="WaveManager"/> 는 대기시간이 끝나며 몬스터를
    /// 소환하는 순간 곧바로 Marching 으로 전환하므로(11절), "방어 임무 시작 = 소환 직후"가
    /// 그대로 성립한다 — 소환 이벤트를 따로 구독하지 않고 <see cref="CurrentDuty"/> 판정만으로
    /// 정확한 시점에 반영된다.
    /// </summary>
    [RequireComponent(typeof(UnitCombat))]
    public class CharacterBehavior : MonoBehaviour
    {
        [Header("정찰 (대기시간)")]
        [Tooltip("정찰 목표를 최소 이 거리 밖에서 고른다(타일). 너무 작으면 " +
                 "시야 경계를 한 칸씩 갉아먹으며 제자리에서 맴돈다")]
        [Min(2f)] [SerializeField] float scoutMinDistance = 14f;

        [Tooltip("미탐사 지점을 찾을 최대 거리(타일)")]
        [Min(4f)] [SerializeField] float scoutSearchRadius = 60f;

        [Header("탐험 배회 범위 (넥서스 중심 원의 지름, 타일)")]
        [Tooltip("전술 지침 '근방' 을 골랐을 때의 배회 가능 구간 — 넥서스 중심 원의 <b>지름</b>이다.\n" +
                 "★ 반지름이 아니다(유저 확정 2026-08-14). 중립 몬스터 등장 범위(73-5절)와 같은 규칙이라 " +
                 "코드가 절반으로 나눠 쓴다.\n" +
                 "이 값은 화면에 절대 노출하지 않는다 — UI 는 '근방/외곽/전역' 이름만 보여준다")]
        [Min(2f)] [SerializeField] float roamDiameterNear = 99f;

        [Tooltip("전술 지침 '외곽' 을 골랐을 때의 배회 가능 구간 지름(타일). '전역' 은 제한 없음이라 값이 없다")]
        [Min(2f)] [SerializeField] float roamDiameterMid = 199f;

        [Tooltip("배회 범위 안이 전부 밝혀졌을 때, 배회 목표를 <b>지금 자리에서 한 걸음</b> 떨어진 " +
                 "곳으로 고른다 — 그 걸음 폭(타일).\n" +
                 "★ 왜 '원 안의 아무 점'이 아닌가 — 73-11절이 중립 몬스터에서 겪은 함정이다. " +
                 "이동 속도보다 재추첨이 빠르면 목표에 닿기 전에 새 목표가 뽑혀 " +
                 "<b>원의 무게중심(=넥서스)으로 가는 랜덤워크</b>가 된다. " +
                 "한 걸음씩 옮기고 그 걸음을 원 안으로 접으면 위치 자체가 원에 갇힌다")]
        [Min(2f)] [SerializeField] float roamWanderStepTiles = 16f;

        [Tooltip("탐험 유형이 '사냥' 이고 배회 범위 안이 전부 밝혀졌을 때, 목표를 배회 범위의 " +
                 "바깥 몇 %(0~1) 지점에서 고를지. 1 에 가까울수록 경계선에 딱 붙는다 — " +
                 "몬스터는 대개 바깥쪽에 있으므로 외곽을 훑는 것이 사냥에 유리하다(유저 지시 2026-08-14)")]
        [Range(0.3f, 1f)] [SerializeField] float roamHuntOuterBand = 0.85f;

        [Tooltip("사냥감을 쫓을 때 배회 범위 <b>밖으로</b> 나갈 수 있는 여유(타일). " +
                 "경계에서 사냥감을 문 캐릭터가 한 발짝도 못 나가면 사냥이 성립하지 않으므로 " +
                 "이만큼은 따라 나간다. 넘으면 사냥을 포기하고 배회 범위로 돌아온다 — " +
                 "부대(협동 탐험)가 있으면 그 대열로 합류하고, 없으면 복귀만 한다 " +
                 "(유저 지시 2026-08-14, 중립 몬스터의 고리 복귀 73-12절과 같은 규칙).\n" +
                 "0 이면 경계 밖으로 한 발짝도 안 나간다. '전역'에는 경계가 없어 적용되지 않는다")]
        [Min(0f)] [SerializeField] float roamHuntOvershootTiles = 12f;

        [Header("시야 밖 피격 확인 (전방 캐릭터가 확인하러 간다)")]
        [Tooltip("켜면, 시야 밖의 적에게 맞았을 때 그 자리를 경보로 남기고 " +
                 "전방 포지션 캐릭터 중 가장 가까운 한 명이 확인하러 간다. " +
                 "끄면 안 보이는 적에게 맞아도 아무 반응이 없다")]
        [SerializeField] bool investigateUnseenAttacks = true;

        [Tooltip("확인하러 갈 최대 거리(타일). 이보다 먼 경보는 무시한다 — " +
                 "맵 끝까지 달려가면 전열이 비어버린다")]
        [Min(2f)] [SerializeField] float investigateRange = 30f;

        [Tooltip("경보를 유지하는 시간(초). 지나면 아무도 확인하지 않은 채 사라진다")]
        [Min(1f)] [SerializeField] float investigateTtlSeconds = 12f;

        [Tooltip("이 거리(타일) 안의 보고는 같은 경보로 합친다. " +
                 "0 에 가까우면 저격수 한 명에 경보가 수십 개 쌓인다")]
        [Min(0.5f)] [SerializeField] float investigateMergeTiles = 4f;

        [Tooltip("정찰 목표 주변에서 적을 쫓을 수 있는 거리(타일)")]
        [Min(1f)] [SerializeField] float scoutLeash = 6f;

        [Tooltip("목표에 못 가고 이 시간이 지나면 다른 곳을 고른다(초). 길이 막혔을 때의 탈출구")]
        [Min(1f)] [SerializeField] float scoutTimeout = 15f;

        [Header("방어 (대기시간 외)")]
        [Tooltip("넥서스에서 이 반경 안을 돌아다니며 지킨다(타일)")]
        [Min(1f)] [SerializeField] float guardRadius = 8f;

        [Tooltip("방어 중 적을 쫓을 수 있는 거리(타일)")]
        [Min(1f)] [SerializeField] float guardLeash = 7f;

        [Tooltip("다음 순찰 지점으로 옮기기까지의 대기 시간 범위(초)")]
        [SerializeField] Vector2 guardRepositionDelay = new Vector2(2.5f, 6f);

        [Header("집결 (플레이어 지정, 웨이브 소환 이후 반영)")]
        [Tooltip("집결지 구역 안에서 적을 쫓을 수 있는 거리(타일). 너무 크면 구역을 벗어나 흩어진다")]
        [Min(1f)] [SerializeField] float rallyLeash = 6f;

        [Tooltip("RallyPointService 를 못 찾았을 때만 쓰는 구역 크기(타일). 평소엔 " +
                 "RallyPointService.RallyAreaSize 를 그대로 읽는다 — 화면에 보이는 범위 표시와 " +
                 "실제 순찰 범위가 항상 같아야 해서 값을 두 곳에 따로 두지 않는다")]
        [Min(2f)] [SerializeField] float rallyAreaSizeFallback = 10f;

        [Header("공통")]
        [Tooltip("목표에 이 거리 안으로 들어오면 도착으로 친다(타일)")]
        [Min(0.2f)] [SerializeField] float arriveDistance = 1.2f;

        [Header("중립 몬스터 사냥 (대기시간 · 진군 중)")]
        [Tooltip("대기시간이나 진군 중 이 거리 안에 중립 몬스터가 있으면 하던 일을 멈추고 사냥한다(타일). " +
                 "웨이브 몬스터와 달리 넥서스로 오지 않으므로 캐릭터가 직접 찾아가야 마주친다. " +
                 "웨이브 타임(전투·광폭화)에는 웨이브 몬스터가 우선이라 사냥하지 않는다")]
        [Min(0f)] [SerializeField] float huntDetectRange = 10f;

        [Tooltip("★ 에픽 중립(카르시노스 등)도 <b>지나가다 알아서</b> 사냥할지. 기본은 끔. " +
                 "에픽은 「토벌 지시」 창으로 부대에 <b>명시적으로 시키는</b> 상대다(86-8절) — " +
                 "켜 두면 사냥 유형 캐릭터가 근처를 지나기만 해도 혼자 달려들어 그 창이 무의미해진다")]
        [SerializeField] bool huntEpicNeutrals = false;

        [Header("건설 (플레이어가 찍어둔 예정지)")]
        [Tooltip("이 거리 안까지 들어가면 건설 작업이 진행된다(타일)")]
        [Min(0.5f)] [SerializeField] float buildWorkRange = 1.8f;

        [Tooltip("건설 중 적을 쫓을 수 있는 거리(타일). 짧게 둬야 현장을 지킨다")]
        [Min(1f)] [SerializeField] float buildLeash = 5f;

        [Tooltip("건설 예정지를 맡을 수 있는 최대 거리(타일). 0 이면 제한 없음.\n" +
                 "건설은 전술 지침 항목이 아니라 '예정지에서 가장 가까운 캐릭터가 맡는 공용 작업' " +
                 "이므로(유저 확정 2026-08-12) 기본값은 0 = 무제한이다. " +
                 "맵 반대편까지 걸어가는 게 싫으면 여기에 거리를 넣으면 된다")]
        [Min(0f)] [SerializeField] float buildRange = 0f;

        [Header("치유 유형 — 근처에서 싸우는 동료 지원")]
        [Tooltip("공격 유형이 '치유'일 때, 이 거리 안에서 <b>다쳤거나 싸우고 있는 동료</b>를 찾아 " +
                 "그 옆으로 붙는다(타일). 0 이면 지원하러 나서지 않고 평소처럼 순찰만 한다.\n" +
                 "치유 유형은 적을 아예 노리지 않으므로(유저 확정 2026-08-13) 이 값이 사실상 " +
                 "'무엇을 하러 움직이는가'를 정한다")]
        [Min(0f)] [SerializeField] float healSupportRange = 16f;

        [Tooltip("지원 자리를 다시 계산하는 주기(초). 동료가 움직이므로 순찰보다 자주 갱신한다")]
        [Min(0.1f)] [SerializeField] float healSupportRepick = 0.5f;

        [Header("부대 — 협동 탐험 시 함께 이동")]
        [Tooltip("부대 기준원의 목적지에서 이만큼 떨어져 선다(타일). 0 이면 같은 지점으로 몰린다.\n" +
                 "캐릭터마다 고정된 방향으로 어긋나므로 대열이 흔들리지 않는다")]
        [Min(0f)] [SerializeField] float squadFollowSpacing = 2.5f;

        [Tooltip("협동 탐험 중 기준원에게서 이만큼 멀어지면 <b>하던 사냥을 놓고 대열로 복귀</b>한다(타일). " +
                 "0 이면 복귀 강제 없음(예전 동작).\n" +
                 "유저 확정 2026-08-13: \"협동 탐험을 켜면 어떤 탐험 유형이든 건설 목표가 있지 않는 " +
                 "이상 함께 탐험을 가야 한다\" — 세부 행동(중립을 때릴지)은 각자의 탐험 유형을 따르되, " +
                 "이동만은 부대를 따른다")]
        [Min(0f)] [SerializeField] float squadRegroupDistance = 12f;

        [Tooltip("부대를 따라가는 중 목적지를 다시 읽는 주기(초). 기준원이 새 목적지를 고른 것을 " +
                 "이만큼 뒤에 따라잡는다. 너무 짧으면 매 프레임 경로가 초기화된다")]
        [Min(0.2f)] [SerializeField] float squadFollowRepick = 1.5f;

        [Header("전방 포지션 — 적극 방어 (인터셉트)")]
        [Tooltip("전방 포지션 캐릭터가 '막으러 나가는' 판정 거리(타일). 구역 중심에서 이 거리 안에 " +
                 "웨이브 몬스터가 들어오면, 순찰을 멈추고 그 적과 구역 사이를 가로막는다. " +
                 "0 이면 전방도 그냥 구역 앞쪽을 순찰만 한다")]
        [Min(0f)] [SerializeField] float frontInterceptRange = 14f;

        [Tooltip("가로막을 때 구역 경계에서 적 쪽으로 더 나가는 거리(타일). 클수록 앞으로 나선다")]
        [Min(0f)] [SerializeField] float frontInterceptOvershoot = 1.5f;

        [Tooltip("가로막는 동안 목줄에 더해주는 여유(타일). 구역 밖으로 나서는 만큼 " +
                 "목줄도 늘려야 그 자리에서 실제로 교전할 수 있다")]
        [Min(0f)] [SerializeField] float frontInterceptLeashBonus = 4f;

        [Tooltip("가로막는 위치를 다시 계산하는 주기(초). 적이 움직이므로 순찰보다 자주 갱신한다")]
        [Min(0.1f)] [SerializeField] float frontInterceptRepick = 0.5f;

        [Header("중위·후방 포지션 — 사거리 지원")]
        [Tooltip("구역 안에서 교전이 벌어졌을 때 지원하러 나서는 판정 거리(타일). " +
                 "이 거리 안에 '지금 싸우고 있는' 적이 있으면 순찰을 멈추고 자기 최대 사거리 " +
                 "지점으로 이동해 아군을 지원한다. 0 이면 예전처럼 구역 안을 순찰만 한다")]
        [Min(0f)] [SerializeField] float supportRange = 14f;

        [Tooltip("지원 위치를 다시 계산하는 주기(초)")]
        [Min(0.1f)] [SerializeField] float supportRepick = 0.5f;

        [Tooltip("중위 포지션이 전방 아군보다 얼마나 뒤에 설지(타일). " +
                 "자기 최대 사거리를 넘지는 않는다 — 넘으면 자기가 못 때린다")]
        [Min(0f)] [SerializeField] float midBehindGap = 1.5f;

        [Header("후퇴 (전술 지침의 '후퇴 판단 기준')")]
        [Tooltip("전방 아군이 후퇴 중인지 다시 확인하는 간격(초). 매 프레임 전체 유닛을 훑지 않기 위한 값")]
        [Min(0.05f)] [SerializeField] float frontRetreatCheckInterval = 0.25f;

        [Tooltip("후퇴 시 물러나 대기할 지점 — 넥서스로부터의 반경(타일)")]
        [Min(0.5f)] [SerializeField] float retreatRadius = 3f;

        [Header("도망 (탐험 유형 '탐색' — 선공 몹에게 맞았을 때)")]
        [Tooltip("때린 상대의 반대 방향으로 한 번에 이만큼 물러난다(타일)")]
        [Min(1f)] [SerializeField] float fleeDistance = 12f;

        [Tooltip("마지막으로 맞은 뒤 이 시간 동안 도망을 유지한다(초). " +
                 "쫓아오는 동안은 계속 맞으므로 실제로는 '떼어낼 때까지' 도망친다")]
        [Min(0.5f)] [SerializeField] float fleeMemorySeconds = 4f;

        [Header("디버그")]
        [SerializeField] bool drawGizmos = true;

        UnitCombat _combat;
        DamageableUnit _self;
        CharacterUnit _character;
        FogOfWarService _fog;
        WaveManager _waveManager;
        MapGenerator _map;
        DamageableUnit _nexus;

        /// <summary>넥서스에서 만든 거리장. 탐험 목표가 <b>실제로 걸어갈 수 있는 곳인지</b> 거르는 데만 쓴다.</summary>
        FlowFieldService _flow;

        /// <summary>도달 가능 판정을 매번 새 델리게이트로 만들지 않도록 캐시한다(GC 압박 방지).</summary>
        System.Func<Vector3Int, bool> _reachableTest;

        // 순찰 지점을 뽑을 때 벽에 걸리면 몇 번까지 다시 굴려볼지.
        const int GuardSpotAttempts = 8;

        CharacterDuty _duty = CharacterDuty.Guard;
        Vector3 _destination;
        float _repickTime;

        /// <summary>지금 확인하러 가고 있는 경보. 없으면 null (<see cref="TickInvestigate"/>).</summary>
        SightAlertService.Alert _investigating;

        /// <summary>경보 목록 청소를 다시 돌릴 시각. 캐릭터마다 매 프레임 돌 필요가 없다.</summary>
        float _nextAlertPrune;

        /// <summary>경보 청소 간격(초). 공용 목록이라 누가 부르든 결과가 같다.</summary>
        const float AlertPruneInterval = 0.5f;
        System.Random _rng;

        // ── 전술 지침 (CharacterTactics 가 밀어 넣는다. 여기선 직렬화하지 않는다) ────
        TacticalPosition _position = TacticalPosition.Mid;
        TacticalRetreatAction _retreatAction = TacticalRetreatAction.KeepFighting;
        TacticalExpeditionType _expeditionType = TacticalExpeditionType.Hunt;
        TacticalRoamRange _roamRange = TacticalRoamRange.Near;
        TacticalWaveReaction _waveReaction = TacticalWaveReaction.DefendNow;
        int _retreatHpPercent;
        bool _retreating;

        /// <summary>탐험 유형 '탐색' 이 선공 몹에게 맞아 그 자리를 벗어나는 중.</summary>
        bool _fleeing;

        /// <summary>정신 이상이 임무 판단을 가로챈 상태. <see cref="CharacterErosion"/> 만 설정한다.</summary>
        MentalOverride _mental = MentalOverride.None;

        public CharacterDuty Duty => _duty;
        public Vector3 Destination => _destination;

        /// <summary>지금 후퇴 중인지 (로스터 표시·디버그용).</summary>
        public bool IsRetreating => _retreating;

        /// <summary>
        /// ★ <b>자기 체력이 후퇴 기준 아래로 떨어져서</b> 물러나는 중인지 —
        /// 남을 따라 물러나는 <b>동반 후퇴</b>와 구분한다.
        ///
        /// <b>왜 구분해야 하나 (2026-08-13 버그)</b> — 동반 후퇴 판정
        /// (<see cref="FrontAllyIsRetreating"/>)이 <c>IsRetreating</c> 을 그대로 봤기 때문에
        /// <b>따라 물러나는 사람도 "앞이 물러난다"의 근거가 됐다.</b> 그 결과 두 명이
        /// <b>서로를 따라</b> 물러나는 고리가 생겼다:
        /// <code>
        ///   중위 M 이 체력 때문에 후퇴 → 넥서스 바로 옆(retreatRadius 3)까지 물러난다
        ///   후방 B 가 M 을 따라 후퇴  → 동반 후퇴는 "적에게서 최대 사거리" 자리라
        ///                              M 보다 <b>넥서스에서 더 먼 곳</b>에 선다
        ///   M 의 체력이 회복됨        → 그런데 이제 B 가 "나보다 앞에서 후퇴 중인 아군"이라
        ///                              M 은 후퇴를 못 끝낸다 → B 도 M 때문에 못 끝낸다
        /// </code>
        /// 서로가 서로의 이유가 되어 <b>체력이 다 차도 영원히 후퇴</b>한다 —
        /// 유저 리포트("체력이 회복되어도 계속 후퇴")의 정체다.
        ///
        /// <b>고친 규칙</b>: 후퇴는 <b>체력이 떨어진 당사자에게서만 전파된다.</b>
        /// 동반 후퇴자는 남을 끌고 가지 않으므로 고리가 만들어질 수 없다.
        /// </summary>
        public bool IsRetreatingBySelfHp => _retreating && _retreatBySelfHp;

        /// <summary>이번 후퇴가 "내 체력 때문"인지. 동반 후퇴면 false. <see cref="IsRetreatingBySelfHp"/> 참조.</summary>
        bool _retreatBySelfHp;

        /// <summary>탐험 유형 '탐색' 이 선공 몹을 피해 도망치는 중인지 (로스터 표시·부대 이동 제외 판정).</summary>
        public bool IsFleeing => _fleeing;

        // 전방 아군 후퇴 판정 캐시 — frontRetreatCheckInterval 간격으로만 다시 계산한다.
        bool _frontRetreating;
        float _nextFrontRetreatCheck;

        /// <summary>따라 물러나기 시작한 전방 아군. 그가 후퇴를 끝낼 때까지 붙잡고 있는다.</summary>
        CharacterBehavior _followingRetreatOf;

        /// <summary>지금 정신 이상이 임무를 가로채고 있는지 (로스터 표시·디버그용).</summary>
        public MentalOverride Mental => _mental;

        /// <summary>
        /// 정신 이상의 행동 오버라이드를 켜고 끈다 (<see cref="CharacterErosion"/> 전용 진입점).
        ///
        /// <b>왜 여기서 후퇴 판단까지 끊는가</b> — 오버라이드가 걸리는 세 상태(혼란·공포·광분)는
        /// 전부 "전술 지침을 따르지 않는 상태"이고, 후퇴 기준(<c>retreatHpPercent</c>)도 전술
        /// 지침의 한 항목이다. 지침대로 후퇴해 버리면 <see cref="SetRetreating"/> 이
        /// <c>ClearHuntTarget</c>·<c>SetCombatSuppressed</c> 를 불러 혼란·광분의 효과를 그 자리에서
        /// 지워 버린다 — 그래서 오버라이드 중에는 후퇴 상태를 강제로 해제해 둔다.
        /// 공포는 그 자체가 회피 상태라 후퇴 로직(<see cref="TickRetreat"/>)을 직접 재사용한다.
        /// </summary>
        public void SetMentalOverride(MentalOverride mode)
        {
            if (_mental == mode) return;

            MentalOverride previous = _mental;
            _mental = mode;

            if (mode == MentalOverride.None)
            {
                // 정상 복귀 — 공포가 켜둔 전투 억제를 풀고, 다음 프레임에 원래 임무를 다시 고른다.
                if (previous == MentalOverride.Flee)
                {
                    _retreating = false;
                    _retreatBySelfHp = false;
                    _combat.SetCombatSuppressed(false);
                    _combat.SetRetreatFiring(false);
                }
                _duty = CurrentDuty();
                _repickTime = 0f;
                return;
            }

            // 오버라이드 진입 — 지침에 따른 후퇴는 여기서 끝낸다(위 주석 참조).
            // ⚠️ `_retreating` 을 직접 끄므로 `SetRetreating` 을 안 거친다 —
            //    후퇴 사격도 여기서 같이 꺼야 한다. 안 그러면 정신 이상 중에 계속 쏜다.
            _retreating = false;
            _retreatBySelfHp = false;
            _combat.SetRetreatFiring(false);

            if (mode == MentalOverride.Flee)
            {
                // 공포: 적을 인식하지 않고 넥서스 쪽으로 물러난다.
                // ★ 체력 후퇴와 달리 <b>쏘지 않는다</b> — 패닉 상태라는 것이 이 상태의 정의다.
                _combat.SetCombatSuppressed(true);
                _combat.ClearHuntTarget();
                _duty = CharacterDuty.Retreat;
                PickRetreatSpot();
            }
            else
            {
                // 혼란·광분: 싸우기는 한다 — 공포가 켜뒀을 수 있는 전투 억제만 확실히 풀어준다.
                _combat.SetCombatSuppressed(false);
                _repickTime = 0f;
            }
        }

        /// <summary>
        /// 전술 지침을 반영한다. <see cref="CharacterTactics"/> 만 호출한다 —
        /// 지침의 정본은 그쪽이고 여기는 사본이다.
        /// </summary>
        public void ApplyTactics(TacticalPosition position, TacticalExpeditionType scoutMode,
                                 TacticalRoamRange roamRange, TacticalWaveReaction waveReaction,
                                 int retreatHpPercent, TacticalRetreatAction retreatAction)
        {
            // 지침은 Awake·Start 순서와 무관하게 들어올 수 있다 — 아래에서 _combat 을 쓰므로
            // 쓰기 직전에 준비를 확인한다(EnsureReady 주석의 그 패턴).
            EnsureReady();

            bool positionChanged = _position != position;
            bool roamChanged = _roamRange != roamRange;

            _position = position;
            _expeditionType = scoutMode;
            _roamRange = roamRange;
            _waveReaction = waveReaction;
            _retreatHpPercent = Mathf.Clamp(retreatHpPercent, 0, 100);
            _retreatAction = retreatAction;

            // 탐험 유형 '탐색' 은 중립을 아예 안 건드린다 — 그 판정은 UnitCombat 이 들고 있어야
            // 반격·동료 구원·사냥 강제 세 경로를 한 번에 막을 수 있다. 지침이 들어오는
            // 이 지점에서만 밀어 넣으므로 매 프레임 확인할 필요가 없다.
            _combat?.SetNeutralHostilitySuppressed(scoutMode == TacticalExpeditionType.Explore);

            // 사냥에서 다른 유형으로 바꾸면 지금 물고 있던 사냥감도 그 자리에서 놓는다 —
            // 안 그러면 지침을 바꿔도 그 한 마리를 끝까지 쫓아가 반영이 안 된 것처럼 보인다.
            if (scoutMode != TacticalExpeditionType.Hunt) _combat?.ClearHuntTarget();

            // '공격 유지'로 돌아왔는데 남을 따라 물러나던 중이면 그 자리에서 끊는다 —
            // 안 그러면 지침을 바꿔도 이번 후퇴가 끝날 때까지 반영이 안 된 것처럼 보인다.
            if (_retreatAction != TacticalRetreatAction.FallBackWithAlly && _followingRetreatOf != null)
            {
                _followingRetreatOf = null;
                _frontRetreating = false;
                _nextFrontRetreatCheck = 0f;
            }

            // 포지션·배회 범위가 바뀌면 지금 서 있는 자리(또는 향하던 목적지)가 더 이상 맞지
            // 않으므로 즉시 다시 고른다. (다음 재추첨까지 최대 6초를 기다리면 UI 를 눌러도
            // 아무 반응이 없어 보인다 — 배회 범위를 좁혔는데 한참 더 걸어 나가면 특히 그렇다)
            if (positionChanged || roamChanged) _repickTime = 0f;
        }

        /// <summary>
        /// <b>씬 참조·난수는 여기서(Awake) 준비한다 — Start 가 아니다.</b>
        /// 캐릭터 생성 버튼(<c>CharacterCreationService</c>)으로 만든 캐릭터는 다른 오브젝트의
        /// Update() 도중(= 이번 프레임의 Start 단계가 이미 지나간 뒤) 곧바로 Instantiate 되는데,
        /// 이렇게 프레임 중간에 태어난 오브젝트는 Awake·OnEnable 은 그 즉시 돌지만 Start 는
        /// 다음 프레임으로 밀린다 — 그런데 Update 는 그 프레임에 바로 불릴 수 있다(유니티의
        /// 잘 알려진 함정). 그래서 Update 가 참조하는 <c>_rng</c>·<c>_map</c>·<c>_fog</c>·
        /// <c>_waveManager</c> 를 Start 에 두면 그 첫 프레임에 전부 null 로 쓰여 NRE 가 난다 —
        /// 24-6절의 <c>NeutralMonsterWander</c> 가 겪은 것과 완전히 같은 종류의 버그이고 고치는
        /// 방법도 같다(초기화를 Awake 로 옮긴다 — Awake 는 프레임 중간에 태어나도 그 즉시 돈다).
        /// </summary>
        void Awake() => EnsureReady();

        /// <summary>
        /// 참조·난수를 준비한다. <b>Awake·Start·Update 세 곳에서 모두 부른다</b>(여러 번 불려도
        /// 안전하다). 27-9절이 초기화를 Start 에서 Awake 로 옮겼는데도 28-4절에서 같은 NRE
        /// (<c>_rng</c> null)가 다시 확인됐다 — <b>어느 콜백이 먼저 도는지 추론하는 대신
        /// 쓰기 직전에 확인하는 것</b>이 이 함정의 확실한 대책이다. 런타임에 동적으로 생성·부착
        /// 되는 컴포넌트에는 이 패턴을 기본으로 쓸 것(<c>NeutralMonsterWander</c> 도 동일).
        /// </summary>
        void EnsureReady()
        {
            if (_rng != null) return;

            _combat = GetComponent<UnitCombat>();
            _self = GetComponent<DamageableUnit>();
            _character = GetComponent<CharacterUnit>();
            _destination = transform.position;

            _fog = FindAnyObjectByType<FogOfWarService>();
            _waveManager = FindAnyObjectByType<WaveManager>();
            _map = FindAnyObjectByType<MapGenerator>();
            _flow = FindAnyObjectByType<FlowFieldService>();
            _reachableTest = cell => _flow.IsCellReachable(cell);

            // 캐릭터마다 다른 난수열을 써야 정찰 목표가 겹치지 않는다.
            _rng = new System.Random(GetInstanceID());
        }

        void Start()
        {
            EnsureReady();
            _duty = CurrentDuty();
            PickDestination();
        }

        void Update()
        {
            EnsureReady();
            if (_self == null || !_self.IsAlive) return;

            // 정신 이상이 임무를 가로챈 상태가 후퇴보다도 먼저다 — 그 상태의 정의 자체가
            // "전술 지침(후퇴 기준 포함)을 따르지 않는다" 이기 때문이다(SetMentalOverride 참조).
            // 정신 이상·후퇴 중에는 전열 유지 거리를 풀어둔다 — 광분은 돌진이 정의고,
            // 후퇴·공포는 아예 싸우지 않는다. 남겨두면 그 상태의 이동과 서로 당긴다.
            if (_mental != MentalOverride.None)
            {
                _combat.SetStandoff(0f);
                TickMentalOverride();
                return;
            }

            // 후퇴 판단이 그다음이다 — 다른 어떤 임무보다 우선한다.
            UpdateRetreatState();
            if (_retreating) { _combat.SetStandoff(0f); TickRetreat(); return; }

            // 도망(탐험 유형 '탐색' 이 선공 몹에게 맞은 상태)은 후퇴 바로 다음이다 —
            // 반격하지 않는 상태이므로 아래의 교전·임무 판단을 아예 타지 않아야 한다.
            UpdateFleeState();
            if (_fleeing) { _combat.SetStandoff(0f); TickFlee(); return; }

            // ★ <b>토벌 명령</b> — 부대에 잡을 대상이 지정돼 있으면 그 대상이 아래의 모든
            //   자율 판단(웨이브 우선·협동 대열·배회 한계·사냥감 물색)보다 <b>앞선다</b>
            //   (유저 지시 2026-08-15: "각 부대에 ... 에픽 몬스터를 선택해 토벌할 수 있는 ui").
            //
            //   여기(맨 앞)에 두는 이유 — 아래 513~538행의 "사냥 포기" 검사 세 개가
            //   <b>배회 범위 밖이면 사냥감을 놓는다</b>. 에픽은 맵 바깥 고리(반지름 100~160)에
            //   사는데 부대의 배회 범위는 보통 그보다 훨씬 좁아서, 뒤에 두면 출발하자마자
            //   명령이 취소된다. 명시적인 지시는 자동 판단이 취소하면 안 된다.
            if (TickSubjugation()) return;

            // 웨이브 타임(전투·광폭화)이 시작되면 사냥 중이던 중립 몬스터보다 웨이브 몬스터를
            // 우선한다 — 사냥 타겟을 놓아 UnitCombat 의 일반 진영 타겟팅(가장 가까운 웨이브
            // 몬스터)이 대신 잡게 한다(유저 요청: "웨이브 타임에는 웨이브 몬스터 우선 처리").
            //
            // 전술 지침 "탐험 우선"(KeepExploring)을 고른 캐릭터는 예외다 — 웨이브가 와도
            // 탐험·건설을 계속하는 것이 그 선택지의 정의이므로 여기서 놓지 않는다.
            if (_combat.IsHunting && IsWaveTimePhase() &&
                _waveReaction == TacticalWaveReaction.DefendNow)
                _combat.ClearHuntTarget();

            // ★ 협동 탐험 — <b>대열에서 너무 벌어지면 물고 있던 사냥감을 놓는다</b>
            //   (유저 확정 2026-08-13: "협동 탐험을 켜면 ... 함께 탐험을 가야 한다").
            //
            //   왜 여기(교전 판정보다 앞)인가 — 사냥 중에는 아래의 "교전 중이면 목적지를
            //   건드리지 않는다"에서 <b>매 프레임 되돌아간다.</b> 그 뒤에 두면 이 검사가
            //   영영 실행되지 않아, 사냥 유형 부대원 혼자 24타일(<c>huntPursuitTiles</c>)까지
            //   쫓아가며 부대가 갈라진 채로 각자 놀게 된다.
            if (_combat.IsHunting && IsFarFromSquadLeader()) _combat.ClearHuntTarget();

            // ★ 사냥 추격이 <b>배회 범위에서 너무 멀어지면</b> 물고 있던 사냥감을 놓고 돌아온다
            //   (유저 지시 2026-08-14). 여기 두는 이유는 바로 위 협동 탐험 검사와 같다 —
            //   아래 "교전 중이면 목적지를 안 건드린다" 보다 앞이어야 매 프레임 실행된다.
            //
            //   놓고 나면 <b>따로 복귀 코드가 필요 없다</b>: ClearHuntTarget 이 타겟까지 비우므로
            //   이 프레임이 그대로 PickExpeditionSpot 까지 내려가고, 거기 맨 앞의 TryFollowSquad 가
            //   부대 대열로 데려간다(협동 탐험이 켜져 있을 때). 부대가 없으면 그 아래의
            //   안개 탐색·자유 배회가 배회 범위 <b>안</b>의 지점만 고르므로 자연히 복귀한다.
            if (_combat.IsHunting && IsBeyondRoamLimit())
            {
                _combat.ClearHuntTarget();
                _repickTime = 0f;   // 다음 재추첨(최대 6초)을 기다리지 않고 그 자리에서 복귀 목적지를 고른다
            }

            // ★ 시야 밖에서 맞았으면 그 자리를 경보로 남긴다 (유저 지시 2026-08-13).
            //   <b>교전 판정보다 앞에 둔다</b> — 다른 적과 싸우는 중에 저격당하는 경우가
            //   정확히 이 기능이 필요한 상황인데, 아래 교전 분기에서 return 하면 영영 안 돈다.
            ReportUnseenAttacker();

            // 교전 중에는 이동을 UnitCombat 에 맡기고 목적지를 건드리지 않는다
            // (사냥 중인 중립 몬스터도 이 시점엔 이미 Target 으로 잡혀 있다).
            // 다만 <b>얼마나 떨어져 싸울지</b>는 전술 포지션이 정하므로 그것만 밀어 넣는다 —
            // 사냥이든 웨이브 전투든 같은 규칙이다(유저 요청: "사냥 중에도 전투 위치 고수").
            if (_combat.Target != null && _combat.Target.IsAlive)
            {
                _combat.SetStandoff(StandoffFor(_combat.Target));
                return;
            }
            _combat.SetStandoff(0f);

            CharacterDuty baseline = CurrentDuty();   // Expedition(탐험) 또는 Guard(방어)

            // 집결지는 "방어" 를 대신한다 — 탐험 중에는 반영하지 않는다.
            // baseline 이 Guard 로 바뀌는 시점이 곧 웨이브 소환 직후이므로(클래스 doc 참조),
            // 별도 이벤트 구독 없이 이 검사만으로 "소환 직후부터 반영" 이 정확히 성립한다.
            //
            // ★ 그래서 <b>웨이브 반응 '탐험 우선'은 집결지를 자동으로 무시한다</b>
            //   (유저 확정 2026-08-12: "같은 부대로 설정되어 있더라도 탐험 우선으로 설정 시
            //   집결지 무시"). 그 지침은 CurrentDuty 가 항상 Scout 을 돌려주므로 이 조건에
            //   애초에 걸리지 않는다 — 집결지를 따로 예외 처리하는 코드가 필요 없다.
            Vector3 rallyCenter = default;
            bool hasRally = baseline == CharacterDuty.Guard &&
                            UI.RallyPointService.TryGetRallyPoint(_character, out rallyCenter);
            CharacterDuty duty = hasRally ? CharacterDuty.Rally : baseline;

            // 대기시간·진군 중에는 먼저 조우한 중립 몬스터를 사냥하러 간다 — 웨이브 몬스터는
            // 넥서스로 전진해오지만 중립 몬스터는 서식지에 머물러 있으므로, 캐릭터가
            // 직접 찾아가야만 마주친다(기획 요청: "탐색 중 조우 시 사냥, 에너지 획득"). 다만
            // 방어·집결 중(=진군)에는 지금 모여야 할 구역(집결지가 있으면 그 구역, 없으면 넥서스
            // 주변 방어 반경) 밖까지 쫓아가면 대열이 흐트러진다는 피드백으로, 그 구역 안에
            // 있는 사냥감만 본다 — 탐험 중에는 원래대로 구역 제한 없이 캐릭터 주변만 본다.
            // 건설이 사냥보다 앞이다 — 예정지는 플레이어가 직접 찍은 <b>명시적인 지시</b>고
            // 사냥·탐험은 할 일이 없을 때의 기본 행동이다. 웨이브 타임(전투·광폭화)에는
            // 원래 아예 시도하지 않았는데, 이제 <b>웨이브 반응이 '탐험 우선'이면 웨이브 중에도
            // 탐험·건설을 계속한다</b>(유저 확정 2026-08-12) — 그 판정이 <see cref="CanDoExpeditionWork"/> 다.
            bool expeditionWork = CanDoExpeditionWork();

            // ★ 확인하러 가기 — 시야 밖에서 날아온 공격의 출처를 <b>전방 캐릭터가</b> 보러 간다.
            //   건설·사냥·순찰보다 앞이다: 지금 우리가 <b>맞고 있는데 반격을 못 하는</b> 상황이라
            //   원인을 찾는 것이 먼저다. 교전 중이면 위에서 이미 return 했으므로 여기 오지 않는다.
            if (TickInvestigate()) return;

            if (expeditionWork && TryBuild()) return;

            // ★ 치유 유형은 <b>적을 노리지 않는다</b> — 대신 근처에서 싸우고 있는 동료 옆에
            //   붙어 지원한다(유저 확정 2026-08-13). 사냥·순찰보다 앞에 둔다: 이 유형에게
            //   "할 일"은 사냥감이 아니라 동료다.
            if (TryPickHealSupportSpot(duty, rallyCenter)) return;

            if (expeditionWork && TryFindHuntPrey(duty, rallyCenter, out DamageableUnit prey))
            {
                _combat.SetHuntTarget(prey);
                return;
            }

            if (duty != _duty)
            {
                _duty = duty;
                PickZoneSpot(duty, rallyCenter);
                return;
            }

            bool arrived = Vector2.Distance(transform.position, _destination) <= arriveDistance;

            // 길이 막혔거나(DestinationUnreachable) 도착했거나 재추첨 시간이 됐으면 다시 고른다.
            // 예전엔 집결지에 "도착"하면 그 뒤로 아무 재추첨이 없어 제자리에 멈춰 서 있었다
            // (UnitCombat 은 목표에 닿으면 Idle 로 멈추고 스스로 돌아다니지 않는다) — 방어와
            // 똑같이 구역 안에서 계속 순찰 지점을 다시 고르게 해서 고쳤다.
            if (arrived || _combat.DestinationUnreachable || Time.time >= _repickTime)
                PickZoneSpot(duty, rallyCenter);
        }

        // ------------------------------------------------------------------
        // 목적지 선택 — 전방 포지션의 "가로막기"가 순찰보다 우선한다
        // ------------------------------------------------------------------

        /// <summary>
        /// 지금 임무에 맞는 목적지를 고른다.
        ///
        /// <b>전방 포지션은 순찰보다 "가로막기"가 먼저다</b>(유저 요청: "전방 포지션이 적극적으로
        /// 방어해야 한다"). 구역 근처까지 다가온 웨이브 몬스터가 있으면, 구역 안을 어슬렁거리는
        /// 대신 <b>그 적과 구역 사이</b>로 나가 선다 — 뒤에 있는 중위·후방 캐릭터에게 적이
        /// 닿기 전에 먼저 걸리게 하려는 것이다. 적이 없으면 평소대로 구역 앞쪽(포지션 구간)을
        /// 순찰한다. <b>공격 유형(근거리·원거리·마법·치유)은 이 판정에 관여하지 않는다</b> —
        /// 전열을 정하는 건 포지션이고, 공격 방식은 그대로 유지된다.
        /// </summary>
        void PickZoneSpot(CharacterDuty duty, Vector3 rallyCenter)
        {
            if (duty != CharacterDuty.Expedition)
            {
                bool rally = duty == CharacterDuty.Rally;
                Vector3 center = rally ? rallyCenter : NexusPosition();
                float half = rally ? RallyAreaSize() * 0.5f : guardRadius;
                float leash = rally ? rallyLeash : guardLeash;

                // 전방은 막으러 나가고, 중위·후방은 사거리에서 지원하러 붙는다.
                // 둘 다 "구역 안을 어슬렁거리는" 순찰보다 앞선다.
                bool moved = _position == TacticalPosition.Front
                    ? TryPickInterceptSpot(center, half, leash)
                    : TryPickSupportSpot(center, half, leash);
                if (moved) return;
            }

            if (duty == CharacterDuty.Rally) PickRallySpot(rallyCenter);
            else PickDestination();
        }

        /// <summary>
        /// 구역으로 다가오는 웨이브 몬스터를 가로막는 자리를 잡는다.
        /// 자리는 <b>구역 중심에서 그 적 방향으로 구역 경계 + 여유만큼</b> 나간 지점 —
        /// 적이 구역에 들어오는 길목이다. 적이 움직이므로 <see cref="frontInterceptRepick"/>
        /// 주기로 계속 다시 잡는다.
        ///
        /// 대상은 <b>웨이브 몬스터(Cancer 진영)만</b>이다. 중립 몬스터까지 막으러 나가면
        /// 전방 캐릭터가 사냥감을 쫓아 구역을 비우게 된다(24-5절에서 고친 그 문제와 같다).
        /// </summary>
        bool TryPickInterceptSpot(Vector3 center, float halfExtent, float leash)
        {
            if (frontInterceptRange <= 0f) return false;

            DamageableUnit threat = null;
            float bestSqr = frontInterceptRange * frontInterceptRange;

            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive) continue;
                if (u.Faction != Faction.Cancer) continue;

                // "구역에 얼마나 가까운가"로 고른다 — 나에게 가까운 적이 아니라
                // 지키는 구역을 위협하는 적을 막아야 한다.
                float sqr = ((Vector2)(u.transform.position - center)).sqrMagnitude;
                if (sqr > bestSqr) continue;

                bestSqr = sqr;
                threat = u;
            }
            if (threat == null) return false;

            Vector2 toThreat = (Vector2)(threat.transform.position - center);
            Vector2 dir = toThreat.sqrMagnitude > 0.01f ? toThreat.normalized : Vector2.up;

            // 적이 이미 구역 안까지 들어왔으면 그 앞을 막는 게 의미가 없다 — 적 쪽으로 붙는다.
            float outward = Mathf.Min(halfExtent + frontInterceptOvershoot, toThreat.magnitude);
            Vector3 spot = center + (Vector3)(dir * outward);

            // 벽에 박힌 자리면 중심 쪽으로 조금씩 당겨보며 설 수 있는 곳을 찾는다.
            for (int step = 0; step < 4 && !IsWalkable(spot); step++)
                spot = center + (Vector3)(dir * (outward * (1f - 0.25f * (step + 1))));

            if (!IsWalkable(spot)) return false;

            _destination = spot;
            _repickTime = Time.time + frontInterceptRepick;
            _combat.SetHome(_destination, leash + halfExtent + frontInterceptLeashBonus);
            return true;
        }

        /// <summary>
        /// 중위·후방 포지션이 <b>교전이 벌어진 쪽으로 사거리만큼 붙어 지원</b>하는 자리를 잡는다
        /// (유저 요청: "후방·중위여도 집결지 내의 적이 공격받으면 최대 사거리에서 아군을 지원").
        ///
        /// 예전에는 중위·후방이 구역 안 자기 구간을 순찰하기만 해서, 전방이 앞에서 싸우는데도
        /// <b>인식 범위 밖에 서 있으면 아예 참전하지 않았다.</b> 이제는 싸움이 벌어진 곳으로
        /// 걸어가되 <see cref="StandoffFor"/> 가 정한 거리(후방=최대 사거리, 중위=전방 아군
        /// 뒤)에서 멈춘다 — 거기까지 가면 <see cref="UnitCombat"/> 이 알아서 적을 잡는다.
        ///
        /// <b>"공격 받으면"</b> 은 <see cref="DamageableUnit.IsInCombat"/> 로 본다(때렸든 맞았든
        /// 최근에 전투가 있었다는 뜻) — 그냥 지나가는 적까지 지원하러 나서면 중위·후방이
        /// 구역을 비운다.
        /// </summary>
        bool TryPickSupportSpot(Vector3 center, float halfExtent, float leash)
        {
            if (supportRange <= 0f) return false;

            DamageableUnit foe = null;
            float bestSqr = supportRange * supportRange;

            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive) continue;
                if (u.Faction != Faction.Cancer) continue;
                if (!u.IsInCombat) continue;                                   // 아직 아무 일도 없는 적은 제외
                if (!IsInsideZone(u.transform.position, center, halfExtent)) continue;

                // 지원은 "싸움이 난 곳"으로 가는 것이므로 나에게 가까운 쪽부터 본다
                // (가로막기와 달리 구역 중심 기준이 아니다 — 이미 구역 안의 적만 남았다).
                float sqr = ((Vector2)(u.transform.position - transform.position)).sqrMagnitude;
                if (sqr > bestSqr) continue;

                bestSqr = sqr;
                foe = u;
            }
            if (foe == null) return false;

            Vector3 spot = StandSpotAgainst(foe, center);
            if (!IsWalkable(spot)) return false;

            _destination = spot;
            _repickTime = Time.time + supportRepick;
            _combat.SetHome(_destination, leash + halfExtent);
            return true;
        }

        /// <summary>
        /// 그 적을 상대로 설 자리 — 적에게서 <see cref="StandoffFor"/> 만큼 떨어진 지점.
        ///
        /// 방향은 <b>지금 내가 서 있는 쪽</b>을 유지한다(적 주위를 옆으로 돌지 않게).
        /// 적과 겹쳐 있어 방향을 못 정하면 구역 중심 쪽으로 물러난다.
        /// 벽에 박히면 적 쪽으로 조금씩 당겨보며 설 수 있는 자리를 찾는다.
        /// </summary>
        Vector3 StandSpotAgainst(DamageableUnit foe, Vector3 zoneCenter)
        {
            float stand = Mathf.Max(0.5f, StandoffFor(foe));

            Vector2 away = (Vector2)(transform.position - foe.transform.position);
            if (away.sqrMagnitude < 0.01f) away = (Vector2)(zoneCenter - foe.transform.position);
            if (away.sqrMagnitude < 0.01f) away = Vector2.up;
            away = away.normalized;

            Vector3 spot = foe.transform.position + (Vector3)(away * stand);
            for (int step = 0; step < 4 && !IsWalkable(spot); step++)
                spot = foe.transform.position + (Vector3)(away * (stand * (1f - 0.25f * (step + 1))));

            return spot;
        }

        /// <summary>
        /// 전술 포지션에 따라 <b>그 적과 얼마나 떨어져 싸울지</b>(타일).
        /// 웨이브 전투든 중립 몬스터 사냥이든 같은 규칙을 쓴다 —
        /// 유저 요청: "사냥 중에도 아군과 함께 싸울 때는 전술 위치를 고수해야 한다".
        ///
        ///   전방 — 0. 적극적으로 붙는다(예전 동작 그대로).
        ///   중위 — <b>전방 아군보다 한 발 뒤</b>(<see cref="midBehindGap"/>). 앞에 아무도
        ///          없으면 후방과 같이 최대 사거리에서 지원한다.
        ///   후방 — 자기 최대 사거리.
        ///
        /// 어느 경우든 <see cref="UnitCombat.SetStandoff"/> 가 자기 사거리로 잘라주므로
        /// "사거리 밖에 서서 영영 못 때리는" 상태는 생기지 않는다. 근거리 유형은 최대
        /// 사거리가 곧 접촉 거리라 후방이어도 결국 붙어서 싸운다 — 의도한 동작이다.
        /// </summary>
        float StandoffFor(DamageableUnit foe)
        {
            float range = _combat.EffectiveAttackRange;

            // ★ 치유 유형의 "타겟"은 적이 아니라 <b>다친 동료</b>다(유저 확정 2026-08-13) —
            //   전열 규칙을 그대로 적용하면 후방일 때 <b>치유 사거리 끝</b>에 서게 되어,
            //   동료가 한 걸음만 움직여도 사거리를 벗어나 치유가 끊긴다. 여유를 두고 붙는다.
            if (_combat.AttackType == TacticalAttackType.Heal)
                return _position == TacticalPosition.Front ? 0f : range * 0.6f;

            switch (_position)
            {
                case TacticalPosition.Front:
                    return 0f;

                case TacticalPosition.Back:
                    return range;

                default:
                    float front = FrontAllyDistanceTo(foe);
                    return front >= 0f ? Mathf.Min(front + midBehindGap, range) : range;
            }
        }

        /// <summary>
        /// 그 적에게 <b>가장 가까이 붙어 있는 아군</b>까지의 거리(타일). 없으면 −1.
        /// 넥서스는 제외한다 — 움직이지 않는 건물을 "전방 아군"으로 삼으면 중위가 넥서스
        /// 뒤에 서 버린다. 포탑은 전열의 일부로 본다.
        /// </summary>
        float FrontAllyDistanceTo(DamageableUnit foe)
        {
            float best = -1f;
            float limitSqr = supportRange * supportRange;

            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive || ReferenceEquals(u, _self)) continue;
                if (u.Faction != Faction.Angel) continue;
                if (u.Kind != UnitKind.Character && u.Kind != UnitKind.Tower) continue;

                float sqr = ((Vector2)(u.transform.position - foe.transform.position)).sqrMagnitude;
                if (sqr > limitSqr) continue;
                if (best >= 0f && sqr >= best * best) continue;

                best = Mathf.Sqrt(sqr);
            }
            return best;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// 전술 지침을 반영한 "지금의 기본 임무" — <b>웨이브 반응</b> 한 축으로 정해진다
        /// (유저 확정 2026-08-12로 단순해졌다).
        ///
        ///   대기시간(Preparation)   → 언제나 <b>탐험</b>(Expedition). 탐험 유형이 무엇이든
        ///                             돌아다니는 것은 같고, 다른 것은 중립 몬스터를 만났을 때다.
        ///   웨이브 · <b>탐험 우선</b> → 계속 <b>탐험</b>. 집결지도 무시한다(호출부 주석 참조).
        ///   웨이브 · <b>즉시 방어</b> → <b>방어</b>(Guard). 집결지가 있으면 호출부가 Rally 로 바꾼다.
        ///
        /// ⚠️ 예전에는 "우선 행동 중시"가 <b>진군 구간까지만</b> 정찰을 유지하고 목적지에 닿으면
        /// 합류하는 반쪽 지침이었다. 이제는 <b>웨이브 내내 탐험을 유지</b>한다 — 방어는
        /// '즉시 방어'를 고른 캐릭터에게 맡기고 이쪽은 맵을 계속 밝히는 역할로 갈랐다.
        /// </summary>
        CharacterDuty CurrentDuty()
        {
            if (_waveManager == null) return CharacterDuty.Guard;

            if (_waveManager.Phase == WavePhase.Preparation) return CharacterDuty.Expedition;

            return _waveReaction == TacticalWaveReaction.KeepExploring
                ? CharacterDuty.Expedition
                : CharacterDuty.Guard;
        }

        /// <summary>
        /// 지금 <b>탐험·건설 같은 비전투 작업</b>을 해도 되는 구간인가.
        ///
        /// 평소에는 웨이브 타임(전투·광폭화)이 아닐 때만이다 — 그때는 싸우는 게 먼저다.
        /// 다만 웨이브 반응이 <b>'탐험 우선'</b>이면 웨이브 중에도 계속한다
        /// (유저 확정 2026-08-12: "탐험 우선 → 탐험 및 건설 우선 수행").
        /// </summary>
        bool CanDoExpeditionWork() =>
            !IsWaveTimePhase() || _waveReaction == TacticalWaveReaction.KeepExploring;

        // ------------------------------------------------------------------
        // 시야 밖 피격 확인 — 안 보이는 적을 못 때리게 막은 대신 둔 반응
        // ------------------------------------------------------------------

        /// <summary>
        /// ★ <b>안 보이는 적에게 맞았으면 그 자리를 경보로 남긴다</b> (유저 지시 2026-08-13).
        ///
        /// <b>왜 폴링인가</b> — <see cref="DamageableUnit.LastAttacker"/> /
        /// <see cref="DamageableUnit.LastAttackedTime"/> 이 이미 "누가 언제 나를 때렸나"를
        /// 들고 있다(반격·동료 구원이 쓰는 것과 같은 값). 새 이벤트를 구독하면 같은 사실을
        /// 두 곳에서 관리하게 되고, 정적 이벤트는 도메인 리로드 처리까지 딸려온다.
        ///
        /// 같은 자리 보고는 <see cref="SightAlertService.Report"/> 가 합쳐주므로
        /// 매 프레임 불러도 경보가 쌓이지 않는다.
        /// </summary>
        void ReportUnseenAttacker()
        {
            if (!investigateUnseenAttacks || _self == null) return;

            // 방금 맞은 것만 본다 — 오래된 기록으로 경보를 되살리지 않는다.
            if (Time.time - _self.LastAttackedTime > investigateTtlSeconds) return;

            DamageableUnit attacker = _self.LastAttacker;
            if (attacker == null || !attacker.IsAlive) return;

            // 보이는 적이면 평소대로 싸우면 된다 — 확인할 것이 없다.
            if (_combat.IsFogVisible(attacker.transform.position)) return;

            SightAlertService.Report(attacker.transform.position, investigateMergeTiles);
        }

        /// <summary>
        /// ★ <b>토벌 명령</b>을 수행한다 (2026-08-15). 명령이 있으면 true 를 돌려
        /// 이 프레임의 자율 판단을 통째로 건너뛴다.
        ///
        /// <b>두 단계로 나눈다</b> — 이게 이 함수의 전부다:
        /// <code>
        ///   멀 때 : 목적지만 대상 쪽으로 잡고 <b>걸어간다</b> (사냥 타겟은 아직 안 준다)
        ///   가까울 때: 사냥 타겟으로 넘긴다 → 그다음은 평소 전투(UnitCombat)가 맡는다
        /// </code>
        /// ⚠ <b>처음부터 사냥 타겟으로 주면 안 된다.</b> <c>UnitCombat</c> 의 사냥 추격 한계
        /// (<c>huntPursuitTiles</c>, 기준점은 물기 시작한 자리)는 "우연히 마주친 사냥감을
        /// 얼마나 쫓을지"를 정하는 값이라 보통 20타일 남짓이다. 에픽은 맵 바깥 고리에 사는데
        /// 출발점이 넥서스 근처라, 그대로 주면 <b>몇 걸음 만에 포기</b>한다.
        /// 명시적인 지시는 그 한계에 걸리면 안 되므로, 사거리 안에 들어갈 때까지는
        /// <b>이동만</b> 시키고 그 안에서만 사냥으로 넘긴다.
        ///
        /// ⚠ 교전이 붙으면(<c>_combat.Target</c>) 목적지를 건드리지 않는다 — 평소 전투와
        /// 같은 규칙이다. 다만 <b>전술 포지션에 따른 교전 거리</b>는 계속 밀어 넣는다.
        /// </summary>
        bool TickSubjugation()
        {
            EpicSubjugationService service = EpicSubjugationService.Instance;
            if (service == null) return false;

            NeutralMonsterUnit target = service.TargetFor(_character);
            if (target == null || !target.IsAlive) return false;

            // ★ <b>웨이브가 오면 전술 지침을 따른다</b> (유저 확정 2026-08-16, 미결 191번).
            //   웨이브 반응이 '즉시 방어'면 토벌을 <b>잠시 놓고</b> 아래의 평소 판단으로
            //   내려간다 — 그쪽이 집결지·넥서스로 데려간다. '탐험 우선'이면 계속 간다.
            //
            //   ⚠ <b>명령 자체는 해제하지 않는다.</b> 장부(EpicSubjugationService)는 그대로
            //     두므로 웨이브가 끝나면 하던 토벌을 이어서 한다 — 유저가 다시 지시할
            //     필요가 없다. "잠시 놓는다"와 "명령을 지운다"는 다른 일이다.
            if (IsWaveTimePhase() && _waveReaction == TacticalWaveReaction.DefendNow)
            {
                _combat.ClearHuntTarget();
                return false;
            }

            // 이미 붙어서 싸우는 중이면 이동은 UnitCombat 에 맡긴다.
            if (_combat.Target != null && _combat.Target.IsAlive)
            {
                _combat.SetStandoff(StandoffFor(_combat.Target));
                return true;
            }

            float distance = Vector2.Distance(transform.position, target.transform.position);
            if (distance <= service.EngageRangeTiles)
            {
                // 사거리 안 — 여기서부터는 평소 사냥과 같다.
                _combat.SetStandoff(0f);
                _combat.SetHuntTarget(target);
                return true;
            }

            // 아직 멀다 — 걸어간다. 목적지를 매 프레임 갱신해 대상이 움직여도 따라간다.
            //
            // ⚠ 귀환 지점(SetHome)도 대상 자리로 옮긴다. 안 그러면 목줄(leash)이 넥서스
            //   기준으로 남아 <b>걸어가는 도중에 되끌려온다</b> — 77-1절이 고친 그 래칫의
            //   반대 방향 증상이다. 목줄 길이는 교전 사거리로 넉넉히 준다.
            _combat.SetStandoff(0f);
            _combat.ClearHuntTarget();
            _duty = CharacterDuty.Expedition;
            _destination = target.transform.position;
            _combat.SetHome(_destination, service.EngageRangeTiles);
            return true;
        }

        /// <summary>
        /// ★ <b>전방 캐릭터가 경보 지점으로 가서 확인한다.</b> 맡았으면 true 를 돌려
        /// 아래의 건설·사냥·순찰 판단을 건너뛰게 한다.
        ///
        /// <b>왜 전방만인가</b> — 유저 지시가 "전방의 캐릭터가 그 곳으로 가서 확인"이다.
        /// 중위·후방이 자리를 뜨면 전열이 앞뒤로 갈라진다.
        /// ⚠️ 그래서 <b>전방 포지션 캐릭터가 한 명도 없으면 아무도 확인하지 않는다</b> —
        /// 경보는 수명이 지나 사라진다(의도한 동작, 미결로 기록).
        ///
        /// <b>여러 전방이 같이 달려가지 않게</b> 하려고, 경보에 담당자를 하나만 등록하고
        /// (<see cref="SightAlertService.TryClaim"/>) <b>그 경보에 가장 가까운 전방</b>만
        /// 집는다. 판정을 서비스가 아니라 여기서 하는 이유는 서비스가 누가 전방인지 모르기 때문이다.
        ///
        /// 확인이 끝나는 조건은 <b>도착</b>이다. 도착하면 자기 시야로 그 자리를 밝히므로,
        /// 적이 실제로 있었다면 그 순간 평소 전투가 이어받는다(교전 분기가 위에 있다).
        /// </summary>
        bool TickInvestigate()
        {
            if (!investigateUnseenAttacks) { ReleaseInvestigation(false); return false; }

            // 오래된 경보와 이미 눈으로 확인된 경보를 치운다 — 캐릭터마다 매 프레임 돌 필요가
            // 없으므로 간격을 둔다(어느 캐릭터가 부르든 결과는 같은 공용 목록이다).
            if (Time.time >= _nextAlertPrune)
            {
                _nextAlertPrune = Time.time + AlertPruneInterval;
                SightAlertService.Prune(investigateTtlSeconds, pos => _combat.IsFogVisible(pos));
            }

            // 확인을 맡을 자격이 없으면 들고 있던 담당도 내려놓는다.
            if (!CanInvestigate()) { ReleaseInvestigation(false); return false; }

            // 이미 맡은 경보가 있으면 그쪽으로 계속 간다.
            if (_investigating != null)
            {
                // Prune 이 지웠거나(누가 봤다) 남이 가로챘으면 놓는다.
                if (!SightAlertService.Contains(_investigating) ||
                    !SightAlertService.TryClaim(_investigating, this))
                {
                    ReleaseInvestigation(false);
                    return false;
                }

                if (Vector2.Distance(transform.position, _investigating.Position) <= arriveDistance ||
                    _combat.DestinationUnreachable)
                {
                    // 도착(또는 길이 막힘) — 확인 완료로 처리한다. 적이 있었다면 도착하면서
                    // 시야에 들어와 위쪽 교전 분기가 이미 이어받았을 것이다.
                    ReleaseInvestigation(true);
                    return false;
                }

                _duty = CharacterDuty.Investigate;
                if ((_destination - _investigating.Position).sqrMagnitude > 0.01f)
                {
                    _destination = _investigating.Position;
                    _combat.SetHome(_destination, investigateRange);
                }
                return true;
            }

            // 새로 맡을 경보 찾기 — 내가 그 경보에 가장 가까운 전방일 때만 집는다.
            var alert = SightAlertService.FindUnclaimedNearest(transform.position, investigateRange);
            if (alert == null) return false;
            if (!ReferenceEquals(PickInvestigator(alert.Position), this)) return false;
            if (!SightAlertService.TryClaim(alert, this)) return false;

            _investigating = alert;
            _duty = CharacterDuty.Investigate;
            _destination = alert.Position;
            _combat.SetHome(_destination, investigateRange);
            return true;
        }

        /// <summary>
        /// 죽거나 파괴될 때 맡고 있던 경보의 담당을 풀어 <b>다른 전방이 이어받게</b> 한다.
        /// (유니티의 <c>Object == null</c> 이 파괴를 잡아주므로 없어도 동작하지만,
        /// 담당이 바로 풀리는 편이 다음 캐릭터가 한 프레임이라도 빨리 출발한다.)
        /// </summary>
        void OnDisable() => ReleaseInvestigation(false);

        /// <summary>맡고 있던 경보를 내려놓는다. <paramref name="resolved"/> 면 경보 자체를 지운다.</summary>
        void ReleaseInvestigation(bool resolved)
        {
            if (_investigating == null) return;
            SightAlertService.Release(_investigating, this, resolved);
            _investigating = null;
            _repickTime = 0f;   // 다음 프레임에 원래 임무의 목적지를 다시 고른다
        }

        /// <summary>
        /// 지금 확인을 맡을 수 있는 상태인지 — 후퇴·도망·정신 이상 중이면 못 맡는다.
        /// <b>포지션 조건은 여기서 보지 않는다</b>(누가 갈지는 <see cref="PickInvestigator"/> 가 정한다).
        /// </summary>
        bool CanInvestigate() =>
            !_retreating && !_fleeing && _mental == MentalOverride.None;

        /// <summary>
        /// ★ <b>이 경보를 누가 확인하러 갈지</b> 정한다. 후보 전체를 한 번 훑어 한 명을 고르므로
        /// 캐릭터마다 같은 답이 나오고, 그래서 <b>여럿이 몰려가지 않는다.</b>
        ///
        /// 규칙 (유저 확정 2026-08-13):
        /// <list type="number">
        /// <item><b>전방 포지션 캐릭터가 있으면</b> 그중 경보에 <b>가장 가까운</b> 한 명.</item>
        /// <item><b>전방이 한 명도 없으면</b> "제일 앞에 있는 캐릭터" — 즉 <b>넥서스에서 가장 먼</b>
        ///       캐릭터가 대신 간다. 이 프로젝트가 전열을 정의하는 기준(넥서스로부터의 거리,
        ///       36절)을 그대로 쓴다.</item>
        /// </list>
        ///
        /// 처음에는 전방만 맡게 했는데, <b>전방을 아무도 지정하지 않으면 경보가 수명이 다할 때까지
        /// 방치</b>됐다(75-3절의 미결 148번) — 유저 지시로 폴백을 넣었다.
        /// </summary>
        CharacterBehavior PickInvestigator(Vector3 worldPos)
        {
            CharacterBehavior bestFront = null;
            float bestFrontSqr = float.MaxValue;

            CharacterBehavior frontmost = null;
            float frontmostSqr = -1f;
            Vector3 nexus = NexusPosition();

            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive) continue;
                if (u.Faction != _self.Faction || u.Kind != UnitKind.Character) continue;

                var who = u.GetComponent<CharacterBehavior>();
                if (who == null || !who.CanInvestigate()) continue;

                if (who._position == TacticalPosition.Front)
                {
                    float sqr = ((Vector2)(worldPos - u.transform.position)).sqrMagnitude;
                    if (sqr < bestFrontSqr) { bestFrontSqr = sqr; bestFront = who; }
                }

                // 폴백 후보 — 넥서스에서 가장 먼 캐릭터(= 제일 앞에 있는 캐릭터)
                float fromNexus = ((Vector2)(u.transform.position - nexus)).sqrMagnitude;
                if (fromNexus > frontmostSqr) { frontmostSqr = fromNexus; frontmost = who; }
            }

            return bestFront != null ? bestFront : frontmost;
        }

        // ------------------------------------------------------------------
        // 후퇴 — 전술 지침의 "후퇴 판단 기준"
        // ------------------------------------------------------------------

        /// <summary>
        /// 체력이 <b>기준 미만</b>이면 후퇴, <b>기준 이상</b>으로 회복되면 즉시 복귀한다
        /// (유저 확정 2026-08-13: "해당 체력 이상으로 회복되면 바로 복귀").
        ///
        /// ⚠ 예전에는 복귀선에 <c>retreatRecoverMargin</c>(+15%)을 얹은 히스테리시스가 있었다.
        ///   유저 지시로 없앴다 — 기준 하나만 쓴다. 들어가는 조건이 <b>미만</b>(&lt;),
        ///   나오는 조건이 <b>이상</b>(&gt;=)이라 두 구간이 겹치지 않으므로, 여유가 없어도
        ///   기준선에서 후퇴/복귀가 매 프레임 뒤집히지는 않는다.
        /// </summary>
        void UpdateRetreatState()
        {
            // ★ 전방 아군이 물러나면 같이 물러난다 (유저 지시 2026-08-11:
            //   "전방의 캐릭터나 본인 스스로의 체력이 후퇴 기준에 다다라서 ... 후퇴할 때").
            //   앞이 무너졌는데 뒤가 그 자리에 남으면 그대로 물려 죽는다.
            bool frontFallingBack = FrontAllyIsRetreating();

            // 후퇴 기준이 0 이면 "후퇴하지 않음"이라는 명시적 지침이다 —
            // 전방이 물러나도 따라가지 않는다. 지침을 넘어서까지 대신 판단하지 않는다.
            if (_retreatHpPercent <= 0) { SetRetreating(false, bySelfHp: false); return; }

            float percent = _self.HpRatio * 100f;

            // 기준 하나로 갈린다 — 미만이면 후퇴, 이상이면 복귀(유저 확정 2026-08-13).
            bool belowEnter = percent < _retreatHpPercent;
            bool aboveExit = !belowEnter;

            if (!_retreating)
            {
                if (belowEnter || frontFallingBack) SetRetreating(true, bySelfHp: belowEnter);
                return;
            }

            // ★ 이미 후퇴 중일 때 — <b>사유가 바뀌면 그것도 반영한다.</b>
            //   동반 후퇴로 들어온 뒤 체력까지 떨어지면 그때부터 '자기 체력 후퇴'로 승격하고,
            //   기준 이상으로 회복되면 (앞사람을 따라 계속 물러나더라도) 동반 후퇴로 내려간다.
            _retreatBySelfHp = belowEnter;

            // 체력이 기준 이상으로 회복됐고 따라갈 앞사람도 없으면 후퇴를 끝낸다.
            if (aboveExit && !frontFallingBack) SetRetreating(false, bySelfHp: false);
        }

        /// <summary>
        /// <b>나보다 앞에 선 아군 캐릭터가 지금 후퇴 중인지.</b>
        ///
        /// <b>"앞"의 기준은 넥서스로부터의 거리</b>다 — 넥서스에서 나보다 먼 쪽에 있으면 전방이다.
        /// 이 프로젝트가 전열(전방/중위/후방)을 정의하는 방식 그대로다(36절).
        /// ⚠️ <b>적과의 거리로 재면 안 된다</b> — 물러나는 도중 적이 사거리 밖으로 나가
        /// <c>_combat.Target</c> 이 null 이 되는 순간 판정이 뒤집혀서
        /// <b>후퇴/복귀를 반복하며 제자리에서 떤다.</b>
        ///
        /// 한 번 따라 물러나기 시작하면 <b>그 상대가 후퇴를 끝낼 때까지 계속 따른다</b>
        /// (<see cref="_followingRetreatOf"/>) — 중간에 서로의 앞뒤가 바뀌어도 흔들리지 않게.
        ///
        /// 포탑은 세지 않는다(후퇴하지 않는 구조물이다). 내가 전방이면 따라 물러날 대상이
        /// 없으므로 보지 않는다 — 전방은 자기 체력으로만 판단한다.
        ///
        /// ⚠️ 매 프레임 전체 유닛을 훑지 않도록 <see cref="frontRetreatCheckInterval"/> 간격으로만
        /// 다시 계산하고 사이에는 직전 결과를 쓴다.
        /// </summary>
        bool FrontAllyIsRetreating()
        {
            // ★ '공격 유지'를 고른 캐릭터는 앞이 빠져도 자기 자리에서 계속 싸운다(전술 지침).
            //   전방 포지션은 애초에 이 지침을 고를 수 없다(TacticalOrder.Normalize).
            if (_retreatAction != TacticalRetreatAction.FallBackWithAlly) return false;
            if (_position == TacticalPosition.Front || supportRange <= 0f) return false;
            if (Time.time < _nextFrontRetreatCheck) return _frontRetreating;
            _nextFrontRetreatCheck = Time.time + frontRetreatCheckInterval;

            float limitSqr = supportRange * supportRange;

            // 따라 물러나던 상대가 아직 후퇴 중이면 앞뒤를 다시 따지지 않고 계속 따른다.
            if (_followingRetreatOf != null)
            {
                bool stillFollowing =
                    _followingRetreatOf._self != null && _followingRetreatOf._self.IsAlive &&
                    _followingRetreatOf.IsRetreatingBySelfHp &&
                    ((Vector2)(_followingRetreatOf.transform.position - transform.position)).sqrMagnitude
                        <= limitSqr;
                if (stillFollowing) { _frontRetreating = true; return true; }
                _followingRetreatOf = null;
            }

            Vector3 nexus = NexusPosition();
            float myDistToNexus = Vector2.Distance(transform.position, nexus);
            _frontRetreating = false;

            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive || ReferenceEquals(u, _self)) continue;
                if (u.Faction != _self.Faction || u.Kind != UnitKind.Character) continue;
                if (((Vector2)(u.transform.position - transform.position)).sqrMagnitude > limitSqr) continue;

                // 넥서스에서 나보다 먼 쪽에 선 아군만 "전방"이다.
                if (Vector2.Distance(u.transform.position, nexus) <= myDistToNexus) continue;

                // ★ <b>체력 때문에 물러나는 사람만</b> 따라간다 — 동반 후퇴자를 근거로 삼으면
                //   서로를 따라 물러나는 고리가 생겨 영원히 안 끝난다
                //   (<see cref="IsRetreatingBySelfHp"/> 의 설명 참조).
                var behavior = u.GetComponent<CharacterBehavior>();
                if (behavior == null || !behavior.IsRetreatingBySelfHp) continue;

                _followingRetreatOf = behavior;
                _frontRetreating = true;
                break;
            }
            return _frontRetreating;
        }

        void SetRetreating(bool value, bool bySelfHp)
        {
            _retreatBySelfHp = value && bySelfHp;
            if (_retreating == value) return;
            _retreating = value;

            // ★ 예전에는 여기서 전투를 통째로 껐다(`SetCombatSuppressed`) — "물러나는 길에
            //    마주친 적을 다시 쫓아가느라 영영 못 빠져나온다"는 이유였다. 이제는
            //    <b>후퇴 사격</b>으로 바꾼다: 이동 목적지는 후퇴 지점으로 고정되고 사거리 안에
            //    들어온 적만 쏘므로, 쫓아가는 일이 애초에 없다(유저 지시 2026-08-11).
            //    공포(정신 이상)는 여전히 전투를 끈다 — 그쪽은 SetMentalOverride 가 직접 켠다.
            _combat.SetRetreatFiring(value);

            if (value)
            {
                _combat.ClearHuntTarget();
                _duty = CharacterDuty.Retreat;
                PickRetreatSpot();
            }
            else
            {
                _repickTime = 0f;   // 복귀 — 다음 프레임에 원래 임무의 목적지를 다시 고른다
            }
        }

        /// <summary>
        /// 정신 이상 오버라이드가 걸린 동안의 유지 처리.
        ///
        /// 혼란·광분은 <b>목적지·타겟을 <see cref="CharacterErosion"/> 이 직접 밀어넣는다</b>
        /// (아군 강제 타겟 / 적 방향으로 귀환 지점 이동) — 그래서 여기서는 아무것도 하지 않고
        /// 평소 임무 판단이 그 값을 덮어쓰지 않게 비켜 주기만 한다. 두 곳에서 목적지를 쓰면
        /// 서로 지워서 캐릭터가 제자리에서 흠칫거린다.
        /// </summary>
        void TickMentalOverride()
        {
            if (_mental == MentalOverride.Flee)
            {
                TickRetreat();     // 공포 = 회피. 도착·타임아웃마다 넥서스 주변을 다시 고른다
                return;
            }

            // 혼란·광분 — 표시용 임무만 갱신해 둔다(로스터는 정신 이상 이름을 우선 표시한다).
            _duty = _mental == MentalOverride.Charge ? CharacterDuty.Expedition : CharacterDuty.Guard;
        }

        /// <summary>후퇴 중 유지 — 넥서스 근처에 도착했으면 그 자리에 머문다.</summary>
        void TickRetreat()
        {
            _duty = CharacterDuty.Retreat;

            bool arrived = Vector2.Distance(transform.position, _destination) <= arriveDistance;
            if (arrived || _combat.DestinationUnreachable || Time.time >= _repickTime)
                PickRetreatSpot();
        }

        /// <summary>
        /// ★ <b>동반 후퇴 전용 후퇴 지점</b> — 넥서스까지 도망가는 것이 아니라
        /// <b>적에게서 자기 최대 사거리만큼 떨어진, 넥서스 쪽 자리</b>를 잡는다
        /// (유저 지시: "동료와 함께 후퇴를 선택하면 전방이 후퇴할때 최대 사거리를 유지하며 같이 후퇴").
        ///
        /// 적이 다가오면 이 점도 넥서스 쪽으로 밀려나므로 <b>거리를 유지한 채 계속 물러난다.</b>
        /// 적을 못 찾으면 평소 후퇴 지점(넥서스 주변)으로 넘긴다.
        ///
        /// ⚠️ 기준이 되는 적은 <c>UnitCombat.Target</c> 이 아니라 <b>가장 가까운 적</b>이다 —
        /// 후퇴 사격 중에는 사거리 밖 타겟을 놓아버리므로, 타겟을 기준으로 잡으면
        /// 물러나자마자 기준이 사라져 후퇴 지점이 넥서스로 튄다.
        /// </summary>
        bool TryPickStandoffRetreatSpot()
        {
            DamageableUnit foe = UnitRegistry.FindTarget(
                transform.position, _self.Faction, _combat.EffectiveDetectRange, null, null);
            if (foe == null) return false;

            Vector3 nexus = NexusPosition();
            Vector2 towardNexus = nexus - foe.transform.position;
            if (towardNexus.sqrMagnitude < 0.0001f) return false;

            float range = Mathf.Max(1f, _combat.EffectiveAttackRange);
            Vector3 spot = foe.transform.position + (Vector3)(towardNexus.normalized * range);
            if (!IsWalkable(spot)) return false;

            _destination = spot;
            _repickTime = Time.time + 0.5f;   // 적이 움직이므로 자주 다시 잡는다
            _combat.SetHome(_destination, retreatRadius + guardRadius);
            return true;
        }

        void PickRetreatSpot()
        {
            // 내 체력은 멀쩡한데 물러나는 중 = 전방을 따라가는 동반 후퇴다.
            if (_retreatAction == TacticalRetreatAction.FallBackWithAlly &&
                _self.HpRatio * 100f > _retreatHpPercent &&
                TryPickStandoffRetreatSpot())
                return;

            Vector3 nexus = NexusPosition();
            Vector3 candidate = nexus;

            for (int attempt = 0; attempt < GuardSpotAttempts; attempt++)
            {
                double angle = _rng.NextDouble() * System.Math.PI * 2.0;
                float radius = retreatRadius * (float)_rng.NextDouble();
                candidate = nexus + new Vector3(Mathf.Cos((float)angle) * radius,
                                                Mathf.Sin((float)angle) * radius, 0f);
                if (IsWalkable(candidate)) break;
            }

            _destination = candidate;
            _repickTime = Time.time + 3f;

            // 목줄을 넉넉히 줘야 넥서스까지 오는 길이 막히지 않는다(전투는 어차피 꺼져 있다).
            _combat.SetHome(_destination, retreatRadius + guardRadius);
        }

        // ------------------------------------------------------------------
        // 도망 — 탐험 유형 '탐색' 이 선공 몹에게 맞았을 때 (유저 확정 2026-08-12)
        // ------------------------------------------------------------------

        /// <summary>
        /// 도망 상태를 켜고 끈다. 조건은 <b>탐험 유형이 '탐색'</b> 이고
        /// <b>중립 몬스터에게 방금 맞았다</b> 는 것 둘뿐이다.
        ///
        /// <b>왜 웨이브 몬스터(Cancer)는 세지 않는가</b> — 이 지침이 정하는 것은 중립 몬스터를
        /// 어떻게 대할지이고, 웨이브 몬스터와는 탐험 중이라도 싸워야 한다. Cancer 에게 맞으면
        /// <c>LastAttacker</c> 가 그쪽으로 바뀌므로 도망이 저절로 풀리고 평소 전투로 돌아간다.
        /// </summary>
        void UpdateFleeState()
        {
            bool want = _expeditionType == TacticalExpeditionType.Explore && RecentNeutralAttacker() != null;
            if (want == _fleeing) return;

            _fleeing = want;
            if (want)
            {
                _duty = CharacterDuty.Flee;
                PickFleeSpot();
            }
            else
            {
                _repickTime = 0f;   // 복귀 — 다음 프레임에 원래 임무의 목적지를 다시 고른다
            }
        }

        /// <summary>
        /// <b>방금 나를 때린 중립 몬스터.</b> 없으면 null.
        /// <see cref="DamageableUnit.LastAttacker"/>/<see cref="DamageableUnit.LastAttackedTime"/> 을
        /// 그대로 읽는다 — 반격 로직이 쓰는 것과 같은 값이라 별도 장부가 필요 없다(42-2절과 같은 방식).
        /// </summary>
        DamageableUnit RecentNeutralAttacker()
        {
            if (_self == null) return null;
            if (Time.time - _self.LastAttackedTime > fleeMemorySeconds) return null;

            DamageableUnit attacker = _self.LastAttacker;
            return attacker != null && attacker.IsAlive && attacker.Faction == Faction.Neutral
                ? attacker
                : null;
        }

        /// <summary>도망 중 유지 — 도착하거나 길이 막히면 계속 더 멀리 잡는다.</summary>
        void TickFlee()
        {
            _duty = CharacterDuty.Flee;

            bool arrived = Vector2.Distance(transform.position, _destination) <= arriveDistance;
            if (arrived || _combat.DestinationUnreachable || Time.time >= _repickTime)
                PickFleeSpot();
        }

        /// <summary>
        /// 때린 상대의 <b>반대 방향</b>으로 <see cref="fleeDistance"/> 만큼 물러난 지점.
        /// 벽에 막히면 방향을 45°씩 돌려보고, 그래도 못 가면 제자리에 둔다
        /// (넥서스로 돌아가지는 않는다 — 그건 체력 후퇴의 동작이고, 이쪽은 "그 자리를 벗어난다"다).
        ///
        /// ⚠️ 목줄(<see cref="UnitCombat.SetHome"/> 두 번째 인자)을 <b>0 으로 준다</b> —
        /// 도망 중에 눈에 걸린 웨이브 몬스터를 쫓아가면 도망이 아니게 된다.
        /// </summary>
        void PickFleeSpot()
        {
            DamageableUnit foe = RecentNeutralAttacker();
            Vector3 from = foe != null ? foe.transform.position : _destination;

            Vector2 away = (Vector2)(transform.position - from);
            if (away.sqrMagnitude < 0.01f) away = Vector2.up;
            away = away.normalized;

            Vector3 spot = transform.position + (Vector3)(away * fleeDistance);
            for (int step = 0; step < 7 && !IsWalkable(spot); step++)
            {
                away = (Vector2)(Quaternion.Euler(0f, 0f, 45f) * (Vector3)away);
                spot = transform.position + (Vector3)(away * fleeDistance);
            }
            if (!IsWalkable(spot)) spot = transform.position;

            _destination = spot;
            _repickTime = Time.time + 1f;
            _combat.SetHome(_destination, 0f);
        }

        // ------------------------------------------------------------------
        // 건설 — "캐릭터가 알아서 판단해서" 짓는다 (유저 요청)
        // ------------------------------------------------------------------

        /// <summary>
        /// 건설 예정지를 맡을 수 있는 최대 거리(타일). <b>0 이면 제한 없음.</b>
        ///
        /// ⚠️ 예전에는 전술 지침에 "건물 건설"이 있어서 <b>전담 캐릭터</b>가 맵 어디든 맡고
        /// 나머지는 이 거리 안만 도왔다. 지금은 지침 항목이 사라졌으므로
        /// <b>모두가 같은 조건으로 후보</b>고, 배정 기준은 <b>거리 하나</b>다
        /// (유저 확정 2026-08-12: "건설은 그냥 제일 가까운 캐릭터가 우선 수행").
        /// </summary>
        public float BuildRange => buildRange;

        /// <summary>
        /// 지금 건설을 맡을 수 있는 상태인가 — <see cref="Buildings.BuildService"/> 가
        /// 건설자를 고를 때 후보 조건으로 쓴다. <see cref="Update"/> 가 <see cref="TryBuild"/>
        /// 까지 내려오는 조건(살아있고, 후퇴·도망 중이 아니고, 비전투 작업이 가능한 구간이고,
        /// 교전 중이 아니다)과 같아야 한다 — 어긋나면 일 못 하는 캐릭터에게 자리가 배정된 채 묶인다.
        /// </summary>
        public bool CanTakeBuildOrder =>
            _self != null && _self.IsAlive && !_retreating && !_fleeing && CanDoExpeditionWork() &&
            (_combat == null || _combat.Target == null || !_combat.Target.IsAlive);

        /// <summary>
        /// 맡은 건설 예정지가 있으면 그쪽으로 가고, 도착했으면 짓는다.
        ///
        /// <b>누가 가는지는 여기서 정하지 않는다</b>(유저 확정) —
        /// <see cref="Buildings.BuildService.AssignedSiteFor"/> 가 전체를 보고
        /// <b>예정지마다 지금 가장 적합한 캐릭터 한 명</b>을 붙인다(전담 우선 → 거리순).
        /// 캐릭터마다 스스로 고르면 같은 자리에 여럿이 몰리는데, 이제 <b>한 자리에는 한
        /// 명만</b> 붙는다.
        ///
        /// <b>언제 가는가</b> — 웨이브 타임(전투·광폭화)이 아닐 때만이다. 호출부에서 이미
        /// 걸러지므로 여기서는 다시 확인하지 않는다.
        /// </summary>
        bool TryBuild()
        {
            Buildings.BuildService svc = Buildings.BuildService.Instance;
            if (svc == null) return false;

            Buildings.BuildSite site = svc.AssignedSiteFor(this);
            if (site == null) return false;

            _duty = CharacterDuty.Build;

            // 목적지는 현장 한 곳으로 고정한다. UnitCombat 은 귀환 지점에 도착하면 멈추므로
            // 매 프레임 다시 찍을 필요가 없고, 다시 찍으면 경로가 계속 초기화된다.
            if ((_destination - site.Center).sqrMagnitude > 0.01f)
            {
                _destination = site.Center;
                _combat.SetHome(_destination, buildLeash);
            }

            if (Vector2.Distance(transform.position, site.Center) <= buildWorkRange)
                svc.Contribute(site, Time.deltaTime);

            return true;
        }

        /// <summary>웨이브 몬스터가 우선인 구간(전투·광폭화) — 이 동안은 중립 몬스터를 사냥하지 않는다.</summary>
        bool IsWaveTimePhase() =>
            _waveManager != null &&
            (_waveManager.Phase == WavePhase.Battle || _waveManager.Phase == WavePhase.Enrage);

        void PickDestination()
        {
            bool picked = _duty == CharacterDuty.Expedition ? PickExpeditionSpot() : PickGuardSpot();

            // 탐험할 곳이 아예 없으면(안개 서비스가 없거나 맵 자체가 준비 전이면) 방어로 넘어간다.
            //
            // ⚠ 예전에는 <b>맵을 다 밝히기만 해도</b> 여기로 떨어졌다 — 유저 리포트
            //   "맵이 다 밝혀지면 캐릭터가 아무 행동도 안 한다"의 정체다. 이제
            //   <see cref="PickExpeditionSpot"/> 이 밝힐 곳이 없어도 배회 범위 안에서
            //   자유 배회 지점을 돌려주므로 이 폴백은 진짜 예외 상황에만 걸린다.
            if (!picked && _duty == CharacterDuty.Expedition)
            {
                _duty = CharacterDuty.Guard;
                PickGuardSpot();
            }
        }

        /// <summary>
        /// 지금 지침의 <b>탐험 배회 반지름</b>(타일). 전술 지침은 <b>지름</b>으로 정의돼 있으므로
        /// (유저 확정 2026-08-14 — 중립 몬스터 등장 범위 73-5절과 같은 규칙) 여기서 절반으로 나눈다.
        /// '전역'은 제한 없음이라 무한대다.
        /// </summary>
        float RoamRadiusTiles => _roamRange switch
        {
            TacticalRoamRange.Near => roamDiameterNear * 0.5f,
            TacticalRoamRange.Mid  => roamDiameterMid * 0.5f,
            _                      => float.PositiveInfinity,
        };

        /// <summary>
        /// 사냥감을 쫓다 <b>배회 범위 밖으로 너무 나갔는지</b> — 넘으면 사냥을 포기하고 돌아온다
        /// (유저 지시 2026-08-14: "일정 타일 범위 내에서는 범위를 벗어나서 추적하지만 일정 거리
        /// 이상 배회 가능 거리에서 멀어지면 다시 배회 가능 거리로 돌아가 동료와 합류").
        ///
        /// ★ <b>기준점이 넥서스라서 래칫이 아니다</b> — 77-2절이 기록한 함정("자기 위치가
        /// 기준이면 걸어갈수록 판정 범위도 같이 따라와 얼마든지 끌려간다")을 여기서 다시 밟지
        /// 않으려면 기준점이 <b>움직이지 않아야</b> 한다. 재는 것은 "내가 넥서스에서 얼마나
        /// 떨어졌나" 하나이고, 그 한계선은 배회 범위 + 여유로 고정돼 있다.
        /// 73-12절이 중립 몬스터에게 적용한 "고리 기준 추격 한계"와 같은 규칙이다.
        ///
        /// 사냥 자체의 추격 한계(<c>UnitCombat.huntPursuitTiles</c> 24타일 · 사냥 시작점 기준)는
        /// 그대로 살아 있다 — 이건 <b>그 위에 얹는 배회 범위 쪽 한계</b>다. 둘 중 먼저 걸리는 쪽이 이긴다.
        /// </summary>
        bool IsBeyondRoamLimit()
        {
            float roam = RoamRadiusTiles;
            if (float.IsPositiveInfinity(roam)) return false;   // '전역' 은 경계 자체가 없다

            float limit = roam + roamHuntOvershootTiles;
            return ((Vector2)(transform.position - NexusPosition())).sqrMagnitude > limit * limit;
        }

        /// <summary>
        /// 탐험 목적지를 고른다. <b>우선순위는 유저가 확정했다</b>(2026-08-14):
        /// <code>
        ///   ① 부대 협동 탐험 — 기준원이 있으면 그를 따라간다 (유형과 무관)
        ///   ② 배회 범위 안에 아직 안 밝혀진 곳이 있으면 → 전장의 안개 밝히기부터
        ///      (물리적으로 갈 수 없는 곳은 후보에서 뺀다)
        ///   ③ 다 밝혀졌고 탐험 유형이 '사냥' 이면 → 배회 범위의 <b>외곽</b>을 훑는다
        ///   ④ 그 외 → 배회 범위 안 자유 배회
        /// </code>
        /// ②~④ 는 전부 <b>넥서스 중심 원</b>(<see cref="RoamRadiusTiles"/>) 안으로 묶인다 —
        /// 이 제한이 없던 예전에는 캐릭터가 초반부터 맵 끝까지 걸어 나가 강한 중립 몬스터와
        /// 마주쳤다(유저 리포트).
        /// </summary>
        bool PickExpeditionSpot()
        {
            // ① 같은 부대원과 함께 움직인다 — 기준원이 있으면 그가 정한 목적지를 따라간다.
            //
            // ★ <b>이 지점이 탐험 유형과 무관하다는 점이 중요하다</b>(유저 확정 2026-08-12):
            //   여기까지 오는 조건은 "지금 임무가 탐험"뿐이고 사냥/정찰/탐색을 구분하지 않는다.
            //   그래서 부대원끼리 유형이 서로 달라도 <b>이동은 같이 한다.</b>
            //
            // 건설하러 간 부대원은 여기 오지 않으므로(Update 의 TryBuild 에서 먼저 빠진다)
            // 자연히 제외되고, 건설이 끝나면 다시 이 경로를 타며 합류한다.
            //
            // ⚠ 배회 범위를 여기에는 걸지 않는다 — 협동 탐험은 부대가 <b>같이 움직인다</b>가
            //   목적이고, 기준원의 목적지는 이미 기준원 자신의 배회 범위로 묶여 있다.
            //   여기서 또 자르면 부대원만 뒤처져 "따로 논다"가 된다(73-4절의 그 문제).
            if (TryFollowSquad(scoutLeash)) return true;

            Vector3 nexus = NexusPosition();
            float roam = RoamRadiusTiles;

            // ② 배회 범위 안의 안개부터 밝힌다.
            //    도달 가능 판정은 거리장이 준비됐을 때만 건다 — 준비 전에 걸면 후보가 전부
            //    사라진다(FlowFieldService.IsCellReachable 주석 참조).
            if (_fog != null && _fog.IsReady &&
                _fog.TryFindUnexploredTarget(transform.position, scoutMinDistance,
                                             scoutSearchRadius, _rng, out Vector3 target,
                                             nexus, roam,
                                             _flow != null && _flow.IsReady ? _reachableTest : null))
            {
                _destination = target;
                _repickTime = Time.time + scoutTimeout;
                _combat.SetHome(_destination, scoutLeash);
                return true;
            }

            // ③·④ 밝힐 곳이 없다 — 배회 범위 안에서 계속 돌아다닌다.
            return PickRoamSpot(nexus, roam);
        }

        /// <summary>
        /// 배회 범위 안이 전부 밝혀졌을 때의 목적지. <b>사냥 유형이면 바깥쪽 띠에서</b>,
        /// 그 외에는 범위 전체에서 고른다(유저 확정 2026-08-14: "모두 밝혀져 있고 우선 행동이
        /// 사냥이라면 최대한 외곽 범위에서 몬스터를 탐색하는 것을 우선적으로. 사냥이 아니라면
        /// 그냥 자유 배회").
        ///
        /// <b>왜 외곽인가</b> — 중립 몬스터는 넥서스에서 떨어진 고리에 서식하므로(73-5·73-11절)
        /// 안쪽을 아무리 돌아도 사냥감이 없다. 배회 범위의 바깥 띠를 훑는 것이 곧
        /// "이 범위 안에서 만날 수 있는 가장 많은 몬스터"를 보는 길이다.
        /// (치유 유형은 애초에 사냥을 안 하므로 — <c>TryFindHuntPrey</c> 가 먼저 막는다 —
        ///  탐험 유형이 '사냥'이어도 외곽으로 내보내지 않는다.)
        ///
        /// ★ <b>목표는 "원 안의 아무 점"이 아니라 "지금 자리에서 한 걸음"이다</b> —
        /// 73-11절이 중립 몬스터에서 밟은 함정을 그대로 피한 것이다. 이동 속도(2~3타일/초)보다
        /// 재추첨(2.5~6초)이 빠르면 <b>목표에 닿기 전에 늘 새 목표가 뽑히므로</b>, 원 안에서
        /// 균등하게 뽑은 목표들의 평균 = 원의 무게중심(넥서스)으로 흘러가는 랜덤워크가 된다.
        /// 한 걸음씩 옮기고 그 걸음을 허용 반지름 구간으로 <b>접으면</b>(clamp) 위치 자체가
        /// 구간에 갇히고, 밖으로 끌려나가도 스스로 돌아온다.
        ///
        /// <b>'전역'(무한대) 처리</b> — 접을 원이 없으므로 걸음만 밟는다. "제한이 없다"는 뜻이지
        /// "매번 맵 반대편으로 간다"는 뜻이 아니다.
        /// </summary>
        bool PickRoamSpot(Vector3 nexus, float roamRadius)
        {
            bool unlimited = float.IsPositiveInfinity(roamRadius);

            bool hunting = _expeditionType == TacticalExpeditionType.Hunt &&
                           _combat.AttackType != TacticalAttackType.Heal;

            // 허용 반지름 구간 — 사냥이면 바깥 띠만, 아니면 원 전체.
            float minRadius = hunting && !unlimited ? roamRadius * roamHuntOuterBand : 0f;

            bool picked = false;
            for (int attempt = 0; attempt < GuardSpotAttempts; attempt++)
            {
                double angle = _rng.NextDouble() * System.Math.PI * 2.0;
                Vector3 candidate = transform.position + new Vector3(
                    Mathf.Cos((float)angle) * roamWanderStepTiles,
                    Mathf.Sin((float)angle) * roamWanderStepTiles,
                    0f);

                // 그 한 걸음을 허용 구간으로 접는다(ClampToRing — 73-11절과 같은 방식).
                if (!unlimited)
                {
                    Vector2 offset = candidate - nexus;
                    float d = offset.magnitude;
                    float clamped = Mathf.Clamp(d, minRadius, roamRadius);
                    if (!Mathf.Approximately(d, clamped))
                    {
                        // 넥서스 위에 정확히 겹쳐 있으면 방향이 없다 — 방금 뽑은 각도를 쓴다.
                        Vector2 dir = d > 0.01f
                            ? offset / d
                            : new Vector2(Mathf.Cos((float)angle), Mathf.Sin((float)angle));
                        candidate = nexus + (Vector3)(dir * clamped);
                    }
                }

                if (!IsWalkable(candidate)) continue;

                // 벽으로 둘러싸인 주머니는 걸어서 못 간다 — 안개 탐색과 같은 기준으로 거른다.
                if (_flow != null && _flow.IsReady && _map != null &&
                    !_flow.IsCellReachable(_map.WorldToCell(candidate))) continue;

                _destination = candidate;
                picked = true;
                break;
            }

            // 다 실패했으면(좁은 맵·벽 밀집) 지금 자리를 유지한다 — 방어로 떨어뜨리지 않는다.
            // 그러면 다음 재추첨에서 다시 시도하므로 "아무것도 안 하는" 상태로 굳지 않는다.
            if (!picked) _destination = transform.position;

            _repickTime = Time.time + Mathf.Lerp(guardRepositionDelay.x, guardRepositionDelay.y,
                                                 (float)_rng.NextDouble());
            _combat.SetHome(_destination, scoutLeash);
            return true;
        }

        /// <summary>
        /// 부대 기준원의 목적지를 따라간다. 기준원이 없으면 false —
        /// 무소속이거나, 내가 기준이거나, <b>그 부대의 협동 탐험이 꺼져 있을 때</b>다
        /// (판정은 <see cref="SquadService.LeaderFor"/> 한 곳).
        ///
        /// <b>같은 자리에 겹치지 않게 흩어 세운다</b> — 목적지를 그대로 쓰면 부대원이 한 점에
        /// 몰려 서로 밀어낸다. 캐릭터마다 고정된 각도(<see cref="GetInstanceID"/> 기반)로
        /// 조금씩 어긋난 자리를 잡아, 매 프레임 흔들리지 않으면서 대열처럼 보이게 한다.
        /// </summary>
        bool TryFollowSquad(float leash)
        {
            SquadService squads = SquadService.Instance;
            if (squads == null) return false;

            CharacterBehavior leader = squads.LeaderFor(this);
            if (leader == null) return false;

            Vector3 target = SquadAnchorOf(leader);

            if (squadFollowSpacing > 0f)
            {
                // 캐릭터마다 고정된 방향 — 랜덤을 쓰면 목적지를 다시 고를 때마다 자리가 바뀐다.
                float angle = (Mathf.Abs(GetInstanceID()) % 360) * Mathf.Deg2Rad;
                target += new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * squadFollowSpacing;
            }

            _destination = target;
            _repickTime = Time.time + squadFollowRepick;
            _combat.SetHome(_destination, leash + squadFollowSpacing);
            return true;
        }

        /// <summary>
        /// 부대원들이 모일 지점 — 기준원의 <b>목적지</b>가 원칙이지만, 기준원이 지금
        /// <b>이동 중이 아니면 기준원의 현재 위치</b>를 쓴다.
        ///
        /// <b>왜 필요한가 (미결 79번 · 유저 리포트 "협동 탐험을 켜도 따로 논다")</b> —
        /// 기준원이 교전·사냥에 붙잡히면 <see cref="Destination"/> 이 <b>마지막으로 고른
        /// 탐험 목적지에 그대로 멈춘다.</b> 그 지점은 14~60타일 앞이므로, 부대원들은
        /// 싸우는 기준원을 뒤에 두고 <b>혼자 저 앞으로 걸어가</b> 그 자리에 서 있게 된다 —
        /// 화면에서는 정확히 "따로 논다"로 보인다.
        /// 기준원이 멈춰 있을 때 위치를 기준으로 삼으면 부대가 그 주변으로 모인다.
        /// </summary>
        Vector3 SquadAnchorOf(CharacterBehavior leader)
        {
            bool leaderTravelling =
                leader._duty == CharacterDuty.Expedition &&
                (leader._combat == null || leader._combat.Target == null ||
                 !leader._combat.Target.IsAlive);

            return leaderTravelling ? leader.Destination : leader.transform.position;
        }

        /// <summary>
        /// 협동 탐험 중인데 기준원에게서 <see cref="squadRegroupDistance"/> 넘게 벌어졌는지.
        /// 협동 탐험이 꺼져 있거나 내가 기준원이면 언제나 false(<see cref="SquadService.LeaderFor"/>).
        /// </summary>
        bool IsFarFromSquadLeader()
        {
            if (squadRegroupDistance <= 0f) return false;

            SquadService squads = SquadService.Instance;
            CharacterBehavior leader = squads != null ? squads.LeaderFor(this) : null;
            if (leader == null) return false;

            return ((Vector2)(leader.transform.position - transform.position)).sqrMagnitude >
                   squadRegroupDistance * squadRegroupDistance;
        }

        // ------------------------------------------------------------------
        // 치유 유형 — 근처에서 싸우는 동료 옆으로 붙는다 (유저 확정 2026-08-13)
        // ------------------------------------------------------------------

        /// <summary>지금 지원하러 붙어 있는 동료. 같은 상대면 목적지를 다시 찍지 않는다.</summary>
        DamageableUnit _healAnchor;

        /// <summary>
        /// ★ <b>치유 유형의 이동</b> — 적이 아니라 <b>동료</b>를 기준으로 움직인다
        /// (유저 확정 2026-08-13: "회복 유형은 몬스터를 어차피 못 때리니 타겟 지정하지 말고
        /// 근처에서 전투를 하는 동료를 지원하게").
        ///
        /// 우선순위는 <b>다친 동료 → 지금 싸우고 있는 동료</b> 순이고, 같은 순위면 가까운 쪽이다.
        /// 자리는 그 동료의 <b>넥서스 쪽 뒤</b>로 치유 사거리의 60% 지점 — 앞에 서면 대신 맞는다.
        ///
        /// ⚠ <b>대상은 자신을 제외한 다른 캐릭터뿐이다</b>(유저 확정 2026-08-13:
        ///   "포탑이랑 넥서스는 회복 대상에서 빼"). 실제 치유 타겟팅도
        ///   같은 규칙이다(<c>UnitCombat.AcquireHealTarget</c>) — 예전에는 자기를 타겟으로 잡고
        ///   그 자리에 굳어 회복 모션만 반복했다.
        ///
        /// ⚠ <b>매 프레임 목적지를 다시 찍지 않는다</b> — 같은 동료를 계속 지원하는 동안에는
        ///   <see cref="healSupportRepick"/> 주기로만 갱신한다. 매 프레임 찍으면 경로가 계속
        ///   초기화돼 제자리에서 흠칫거린다(27-5절 · TryBuild 주석과 같은 이유).
        /// </summary>
        bool TryPickHealSupportSpot(CharacterDuty duty, Vector3 rallyCenter)
        {
            if (_combat.AttackType != TacticalAttackType.Heal || healSupportRange <= 0f) return false;

            // 구역 제한은 사냥과 같은 규칙이다 — 방어·집결 중에는 그 구역 안의 동료만 본다.
            Vector3 zoneCenter;
            float zoneHalfExtent;
            switch (duty)
            {
                case CharacterDuty.Rally:
                    zoneCenter = rallyCenter;
                    zoneHalfExtent = RallyAreaSize() * 0.5f;
                    break;
                case CharacterDuty.Guard:
                    zoneCenter = NexusPosition();
                    zoneHalfExtent = guardRadius;
                    break;
                default:
                    zoneCenter = transform.position;
                    zoneHalfExtent = float.PositiveInfinity;
                    break;
            }

            DamageableUnit best = null;
            int bestRank = int.MaxValue;      // 0 = 다쳤다(더 급하다), 1 = 싸우는 중
            float bestSqr = float.MaxValue;
            float limitSqr = healSupportRange * healSupportRange;

            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive || ReferenceEquals(u, _self)) continue;
                if (u.Faction != _self.Faction) continue;

                // ★ 치유 대상은 <b>자신을 제외한 다른 캐릭터</b>뿐이다(유저 확정 2026-08-13) —
                //   넥서스·포탑 옆에 가봐야 치유할 수 없으므로 지원 대상에서도 뺀다.
                if (u.Kind != UnitKind.Character) continue;

                int rank = u.HpRatio < 1f ? 0 : (u.IsInCombat ? 1 : int.MaxValue);
                if (rank == int.MaxValue) continue;              // 멀쩡하고 싸우지도 않는다

                float sqr = ((Vector2)(u.transform.position - transform.position)).sqrMagnitude;
                if (sqr > limitSqr) continue;
                if (!IsInsideZone(u.transform.position, zoneCenter, zoneHalfExtent)) continue;

                if (best != null && (rank > bestRank || (rank == bestRank && sqr >= bestSqr))) continue;

                best = u;
                bestRank = rank;
                bestSqr = sqr;
            }

            if (best == null) { _healAnchor = null; return false; }

            _duty = duty;   // 표시는 원래 임무 그대로 — 지원은 별도 임무가 아니다

            // 같은 동료를 계속 따라가는 중이면 주기가 될 때까지 목적지를 건드리지 않는다.
            if (ReferenceEquals(best, _healAnchor) && Time.time < _repickTime) return true;
            _healAnchor = best;

            float stand = Mathf.Max(0.5f, _combat.EffectiveAttackRange * 0.6f);

            Vector2 back = (Vector2)(NexusPosition() - best.transform.position);
            if (back.sqrMagnitude < 0.01f) back = (Vector2)(transform.position - best.transform.position);
            if (back.sqrMagnitude < 0.01f) back = Vector2.down;
            back = back.normalized;

            Vector3 spot = best.transform.position + (Vector3)(back * stand);
            for (int step = 0; step < 4 && !IsWalkable(spot); step++)
                spot = best.transform.position + (Vector3)(back * (stand * (1f - 0.25f * (step + 1))));
            if (!IsWalkable(spot)) spot = best.transform.position;

            _destination = spot;
            _repickTime = Time.time + healSupportRepick;

            // 목줄은 치유 사거리만큼 — 지원 대상 옆에 붙어 있기만 하면 된다.
            _combat.SetHome(_destination, Mathf.Max(1f, _combat.EffectiveAttackRange));
            return true;
        }

        bool PickGuardSpot() => PickSpotAround(NexusPosition(), guardRadius, guardLeash, directional: false);

        /// <summary>
        /// 집결지 구역 안에서 순찰 지점을 고른다. 방어(<see cref="PickGuardSpot"/>)와 로직은
        /// 같고 표본 추출 방식만 다르다 — 방어는 넥서스 중심의 원에서 방향 구분 없이 뽑고,
        /// 집결은 넥서스→집결지 축을 기준으로 전/중/후 포지션을 구분해 뽑는다(둘 다 원형 구역).
        /// </summary>
        bool PickRallySpot(Vector3 center) =>
            PickSpotAround(center, RallyAreaSize() * 0.5f, rallyLeash, directional: true);

        /// <summary>
        /// 집결지 구역 크기(지름, 타일) — <see cref="UI.RallyPointService.RallyAreaSize"/> 를 그대로 읽는다.
        /// 미니맵·월드 오버레이에 보이는 범위 표시와 실제 순찰 범위가 항상 같아야 하므로,
        /// 서비스가 정본이고 여기서는 값을 복제해두지 않는다. 서비스가 없을 때만 폴백을 쓴다.
        /// </summary>
        float RallyAreaSize() =>
            UI.RallyPointService.Instance != null
                ? UI.RallyPointService.Instance.RallyAreaSize
                : rallyAreaSizeFallback;

        /// <summary>
        /// 지정 중심 주변(반지름 <paramref name="halfExtent"/> 원형 구역)에서 순찰 지점을 하나
        /// 고른다. 벽·구조물에 걸린 칸은 버리고 다시 굴린다 — 검사 없이 그대로 쓰면 순찰
        /// 지점이 벽 안에 박혀 캐릭터가 도달할 수 없는 곳을 향해 벽에 붙어 멈춘 채 다음
        /// 재추첨까지 기다리게 된다.
        ///
        /// 도착 후에도 이 메서드가 주기적으로 다시 불려야 한다 — <see cref="UnitCombat"/> 은
        /// 목적지에 닿으면 Idle 로 멈추고 스스로는 돌아다니지 않으므로, 여기서 새 지점을
        /// 계속 골라줘야 "경계 순찰"처럼 보인다(가만히 서 있는 것과의 차이).
        /// </summary>
        bool PickSpotAround(Vector3 center, float halfExtent, float extraLeash, bool directional)
        {
            // 전방/중위/후방은 "넥서스에서 얼마나 먼가"로 정의된다(유저 규칙).
            // 집결지(directional)에서는 넥서스 → 집결지 중심 방향이 그 축이 되고,
            // 넥서스 방어(!directional)에서는 반지름 자체가 곧 넥서스로부터의 거리라 축이 필요 없다.
            Vector3 nexus = NexusPosition();
            Vector2 axis = (Vector2)(center - nexus);
            axis = axis.sqrMagnitude > 0.01f ? axis.normalized : Vector2.up;
            Vector2 perpendicular = new Vector2(-axis.y, axis.x);

            GetPositionBand(out float bandLow, out float bandHigh);

            for (int attempt = 0; attempt < GuardSpotAttempts; attempt++)
            {
                Vector3 candidate;
                if (directional)
                {
                    // 집결지는 원형 구역이다(유저 요청). 축 방향(along)은 포지션 구간에서
                    // 뽑되, 그 축과 수직인 폭(side)은 "반지름 halfExtent 원" 을 벗어나지
                    // 않도록 피타고라스로 상한을 건다 — 사각형을 뽑고 잘라내는 대신, 처음부터
                    // 원 안의 점만 나오도록 닫힌 형태로 계산한다(재시도 없이 항상 유효).
                    float along = Mathf.Lerp(bandLow, bandHigh, (float)_rng.NextDouble()) * halfExtent;
                    float maxSide = Mathf.Sqrt(Mathf.Max(0f, halfExtent * halfExtent - along * along));
                    float side = Mathf.Lerp(-1f, 1f, (float)_rng.NextDouble()) * maxSide;
                    candidate = center + (Vector3)(axis * along + perpendicular * side);
                }
                else
                {
                    // 방향 구분 없는 원형(넥서스 방어) — 구간 [-1,1] 을 반지름 비율 [0,1] 로 옮긴다.
                    // 넥서스 위에 정확히 겹치지 않도록 최소 8% 는 띄운다.
                    float t = Mathf.Lerp(bandLow, bandHigh, (float)_rng.NextDouble());
                    float radius = Mathf.Lerp(0.08f, 1f, (t + 1f) * 0.5f) * halfExtent;

                    double angle = _rng.NextDouble() * System.Math.PI * 2.0;
                    candidate = center + new Vector3(
                        Mathf.Cos((float)angle) * radius,
                        Mathf.Sin((float)angle) * radius,
                        0f);
                }

                if (!IsWalkable(candidate)) continue;

                _destination = candidate;
                break;
            }

            // 다 실패했으면 중심 근처의 갈 수 있는 칸으로 폴백한다.
            if (!IsWalkable(_destination) && _map != null &&
                _map.TryFindPlaceableNear(_map.WorldToCell(center), Mathf.CeilToInt(halfExtent),
                                          null, out Vector3Int fallback))
                _destination = _map.CellCenterWorld(fallback);

            _repickTime = Time.time + Mathf.Lerp(guardRepositionDelay.x, guardRepositionDelay.y,
                                                 (float)_rng.NextDouble());

            // 목줄은 중심 기준이어야 하므로, 순찰 지점까지의 거리를 더해준다.
            _combat.SetHome(_destination, extraLeash + halfExtent);
            return true;
        }

        /// <summary>
        /// 포지션(전방/중위/후방)이 구역 안에서 차지하는 구간. -1 = 넥서스에 가장 가까운 끝,
        /// +1 = 가장 먼 끝. 세 구간이 겹치지 않게 1/3 씩 나눈다.
        /// </summary>
        void GetPositionBand(out float low, out float high)
        {
            switch (_position)
            {
                case TacticalPosition.Front: low =  0.34f; high =  1.00f; break;
                case TacticalPosition.Back:  low = -1.00f; high = -0.34f; break;
                default:                     low = -0.33f; high =  0.33f; break;
            }
        }

        /// <summary>
        /// 주변에서 사냥할 만한(살아있는) 중립 몬스터를 찾는다. 가장 가까운 것을 고른다.
        ///
        /// 탐험(Expedition) 중에는 원래대로 구역 제한 없이 캐릭터 주변만 본다 — 어차피 안 밝혀진
        /// 지역으로 널리 돌아다니는 임무라 "구역"이라는 개념이 없다.
        /// 방어·집결(Guard/Rally) 중에는 사냥감이 지금 모여야 할 구역(집결지가 있으면 그
        /// 구역, 없으면 넥서스 방어 반경) 안에 있을 때만 쫓는다 — 안 그러면 구역 밖까지
        /// 쫓아가버려 대열이 흐트러진다(유저 피드백).
        ///
        /// <b>협동 탐험 중이면 부대 기준원 주변으로 한 번 더 좁힌다</b> — 아래 주석 참조.
        /// </summary>
        bool TryFindHuntPrey(CharacterDuty duty, Vector3 rallyCenter, out DamageableUnit prey)
        {
            prey = null;
            if (huntDetectRange <= 0f) return false;

            // 탐험 유형 — <b>'사냥'만</b> 중립 몬스터를 먼저 공격한다(유저 확정 2026-08-12).
            //   탐색(Explore) : 아예 건드리지 않는다. 맞아도 반격 대신 도망친다
            //                   (UnitCombat.SetNeutralHostilitySuppressed · UpdateFleeState).
            // ⚠ 2026-08-15 에 '정찰'이 없어졌다 — 중립이 전부 비선공이 되면서 탐색과
            //   같은 행동이 됐다(TacticalExpeditionType 주석 참조).
            if (_expeditionType != TacticalExpeditionType.Hunt) return false;

            // 치유 유형은 애초에 적을 때리지 않는다 — 사냥감을 잡아봐야 쫓아가기만 한다.
            // 이 유형의 이동은 TryPickHealSupportSpot 이 따로 맡는다(호출부에서 더 앞이다).
            if (_combat.AttackType == TacticalAttackType.Heal) return false;

            // 협동 탐험 중 대열에서 벌어져 있으면 새 사냥감을 물지 않는다 — 먼저 합류한다
            // (유저 확정 2026-08-13). 호출부는 이미 물고 있던 사냥감도 같이 놓는다.
            if (IsFarFromSquadLeader()) return false;

            // 배회 범위 밖으로 너무 나가 있으면 새 사냥감도 물지 않는다 — 먼저 돌아온다
            // (유저 지시 2026-08-14). 이게 없으면 경계에서 <b>놓자마자 다시 무는</b> 왕복이 생긴다:
            // 호출부가 한계를 넘은 사냥감을 놓아도, 바로 아래 후보 탐색이 경계 안쪽의 다른
            // 사냥감을 물어 그쪽으로 다시 끌려 나가기 때문이다. 위 협동 탐험 검사와 같은 형태.
            if (IsBeyondRoamLimit()) return false;

            // 부대원과 함께 사냥한다 — 기준원이 이미 노리는 사냥감이 있으면 같은 놈을 문다.
            // 각자 가장 가까운 놈을 고르면 부대가 사방으로 흩어진다(유저 요청: "같은 부대는
            // 함께 이동"). 사거리 제한은 두지 않는다 — 기준원을 따라가는 중이라 어차피 곧 붙는다.
            //
            // ★ 기준원은 <b>협동 탐험이 켜져 있을 때만</b> 잡힌다(SquadService.LeaderFor) —
            //   꺼두면 여기도 null 이라 각자 자기 사냥감을 고른다. 스위치가 한 곳뿐인 이유다.
            CharacterBehavior leader = null;
            SquadService squads = SquadService.Instance;
            if (squads != null)
            {
                leader = squads.LeaderFor(this);
                if (leader != null && leader._combat != null)
                {
                    DamageableUnit shared = leader._combat.Target;
                    if (shared != null && shared.IsAlive && shared.Faction == Faction.Neutral)
                    {
                        prey = shared;
                        return true;
                    }
                }
            }

            Vector3 zoneCenter;
            float zoneHalfExtent;
            switch (duty)
            {
                case CharacterDuty.Rally:
                    zoneCenter = rallyCenter;
                    zoneHalfExtent = RallyAreaSize() * 0.5f;
                    break;
                case CharacterDuty.Guard:
                    zoneCenter = NexusPosition();
                    zoneHalfExtent = guardRadius;
                    break;
                default:
                    // 탐험 — 구역은 <b>탐험 배회 범위</b>다(유저 지시 2026-08-14).
                    // 예전엔 제한이 없어서, 배회 범위를 좁혀놔도 그 경계 바로 밖의 사냥감을
                    // 물고 나가버렸다 — 목적지만 묶고 사냥감을 안 묶으면 제한이 새어나간다
                    // (73-11절이 중립 몬스터에서 겪은 "제약이 목표에만 걸려 있었다"와 같은 함정).
                    // '전역'이면 반지름이 무한대라 IsInsideZone 이 전부 통과시킨다 = 예전 동작.
                    zoneCenter = NexusPosition();
                    zoneHalfExtent = RoamRadiusTiles;
                    break;
            }

            // ★ 협동 탐험 중이면(기준원이 있으면) <b>기준원 주변의 사냥감만</b> 본다
            //   (유저 확정 2026-08-12: "최소한 이동만은 같이 해야 된다").
            //
            //   이게 없으면 이런 일이 생긴다 — 기준원은 탐색 유형이라 중립을 무시하고 계속
            //   나아가는데, 사냥 유형인 부대원은 자기 옆의 중립을 물고 뒤에 남는다. 그 뒤로
            //   기준원과의 거리가 벌어지면 부대가 갈라진 채로 각자 논다.
            //   기준원 기준으로 좁혀두면 <b>사냥은 하되 대열에서 이탈하지는 않는다</b> —
            //   유형이 서로 달라도 이동이 같이 가는 것이 이 한 줄로 보장된다.
            //   반경은 huntDetectRange 를 그대로 재사용한다(새 인스펙터 값을 늘리지 않는다).
            if (leader != null)
            {
                zoneCenter = leader.transform.position;
                zoneHalfExtent = huntDetectRange;
            }

            float bestSqr = huntDetectRange * huntDetectRange;
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive || u.Faction != Faction.Neutral) continue;

                // ★ 에픽은 <b>토벌 지시</b>로만 상대한다 (2026-08-18). 이게 없으면 사냥 유형
                //   캐릭터가 카르시노스 10타일 안을 지나가기만 해도 혼자 달려들어,
                //   86-8절이 만든 「토벌 지시」 창이 하는 일이 없어진다.
                //   ⚠ 토벌 명령을 받은 캐릭터는 이 함수를 <b>거치지 않는다</b> —
                //     TickSubjugation 이 훨씬 앞에서 직접 SetHuntTarget 을 한다.
                if (!huntEpicNeutrals && u is NeutralMonsterUnit ne &&
                    ne.Definition != null && ne.Definition.epic) continue;

                float sqr = (u.transform.position - transform.position).sqrMagnitude;
                if (sqr > bestSqr) continue;
                if (!IsInsideZone(u.transform.position, zoneCenter, zoneHalfExtent)) continue;

                bestSqr = sqr;
                prey = u;
            }
            return prey != null;
        }

        /// <summary>
        /// 구역은 전부 원형이다(방어·집결 둘 다) — 넥서스 방어는 원래부터 원, 집결지도
        /// 이번에 원형으로 바뀌었다(유저 요청). 그래서 판정은 중심까지 거리 하나면 된다.
        /// </summary>
        static bool IsInsideZone(Vector3 pos, Vector3 center, float halfExtent) =>
            float.IsPositiveInfinity(halfExtent) || Vector2.Distance(pos, center) <= halfExtent;

        /// <summary>그 월드 지점이 걸어갈 수 있는 칸인지 (맵 안 + 벽·구조물 아님).</summary>
        bool IsWalkable(Vector3 worldPos) =>
            _map == null || _map.IsCellPlaceable(_map.WorldToCell(worldPos));

        Vector3 NexusPosition()
        {
            if (_nexus == null || !_nexus.IsAlive)
                _nexus = UnitRegistry.FindFirst(Faction.Angel, UnitKind.Nexus);

            return _nexus != null ? _nexus.transform.position : Vector3.zero;
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos || !Application.isPlaying) return;

            Gizmos.color = _duty switch
            {
                CharacterDuty.Expedition => new Color(0.4f, 1f, 0.6f, 0.9f),
                CharacterDuty.Rally => new Color(1f, 0.95f, 0.5f, 0.9f),
                CharacterDuty.Build => new Color(1f, 0.6f, 0.25f, 0.9f),
                CharacterDuty.Flee  => new Color(0.8f, 0.5f, 1f, 0.9f),
                _                   => new Color(0.4f, 0.7f, 1f, 0.9f),
            };
            Gizmos.DrawLine(transform.position, _destination);
            Gizmos.DrawWireCube(_destination, Vector3.one * 0.8f);
        }
    }
}
