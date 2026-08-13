using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Map;
using LastSanctuary.Fog;

namespace LastSanctuary.Combat
{
    /// <summary>자동 전투 상태. 기획서 p8 의 대기·이동·전투·사망에 대응.</summary>
    public enum CombatState
    {
        Idle,      // 대기 — 타겟 없음, 목표로 전진하지도 않음
        Advance,   // 이동 — 목표(넥서스)를 향해 전진
        Chase,     // 이동 — 특정 타겟 추격
        Attack,    // 전투 — 사거리 안, 쿨다운마다 공격
        Dead,      // 사망
    }

    /// <summary>
    /// 자동 전투 AI. DamageableUnit 과 같은 오브젝트에 붙는다.
    /// 파라미터를 전부 직렬화 필드로 노출해서 템플릿(근거리/원거리/보스/캐릭터)마다
    /// 값만 다르게 두면 되도록 했다. 스크립트는 하나만 유지한다.
    /// </summary>
    [RequireComponent(typeof(DamageableUnit))]
    public class UnitCombat : MonoBehaviour
    {
        [Header("사거리 (타일)")]
        [Tooltip("이 범위 안의 적을 타겟으로 인식한다")]
        [Min(0.5f)] [SerializeField] float detectRange = 7f;

        [Tooltip("이 범위 안이면 공격한다")]
        [Min(0.2f)] [SerializeField] float attackRange = 1.2f;

        [Tooltip("절대 움직이지 않는 유닛(포탑 등). 이동속도 0 은 '설정 안 함'으로 처리되어 " +
                 "밸런스 기본 속도로 폴백하므로, 고정 구조물은 반드시 이 값을 켜야 한다")]
        [SerializeField] bool immobile = false;

        [Header("이동")]
        [Tooltip("초당 이동 타일 수. 0 이하면 BalanceConfig 의 기본값을 쓴다")]
        [SerializeField] float moveSpeedTiles = 0f;

        [Tooltip("타겟이 없을 때 목표(넥서스)를 향해 전진한다. 몬스터는 켜고 캐릭터는 끈다")]
        [SerializeField] bool advanceToObjective = false;

        [Tooltip("전진하지 않는 유닛이 초기 위치에서 벗어날 수 있는 최대 거리(타일). " +
                 "이 밖의 적은 쫓지 않고 제자리로 돌아온다")]
        [Min(0f)] [SerializeField] float leashRange = 8f;

        [Header("공격")]
        [Tooltip("초당 공격 횟수. 0 이하면 BalanceConfig 의 기본값을 쓴다")]
        [SerializeField] float attacksPerSecond = 0f;

        [Tooltip("공격 우선순위. 앞에 있는 종류를 먼저 노린다 (웨이브 기획서 p13).\n" +
                 "비워두면 종류를 가리지 않고 가장 가까운 적을 노린다 — 캐릭터/포탑처럼 " +
                 "적이 몬스터 한 종류뿐인 유닛은 비워두는 쪽이 안전하다")]
        [SerializeField] UnitKind[] targetPriority = System.Array.Empty<UnitKind>();

        [Tooltip("켜면 안개에 가려진(시야 밖) 적은 타겟으로 인식하지 않는다.\n" +
                 "캐릭터·포탑처럼 플레이어 시야에 종속된 유닛에 켠다. " +
                 "몬스터는 꺼서 항상 지도 전체가 밝혀진 것처럼 공격한다")]
        [SerializeField] bool respectFogOfWar = false;

        [Tooltip("끄면 스스로 적을 찾아 먼저 공격하지 않는다 — 비선공 유닛(중립 몬스터 등)에 쓴다. " +
                 "맞았을 때의 반격은 아래 canRetaliate 가 따로 결정한다")]
        [SerializeField] bool canAcquireTargets = true;

        [Tooltip("비선공(canAcquireTargets 꺼짐) 유닛이 <b>맞으면 때린 상대에게 반격</b>할지. " +
                 "유저 정의: '비선공'은 먼저 공격하지 않는다는 뜻이지 맞고도 가만히 있는다는 뜻이 아니다. " +
                 "공격력이 0이면 반격해봐야 의미가 없으므로 자동으로 반격하지 않는다")]
        [SerializeField] bool canRetaliate = true;

        [Tooltip("마지막으로 맞은 뒤 이 시간(초) 동안 반격 대상을 유지한다. 지나면 원래대로 돌아간다")]
        [Min(0.5f)] [SerializeField] float retaliateMemorySeconds = 8f;

        [Tooltip("반격 대상을 쫓아갈 수 있는 최대 거리(타일). 이 밖으로 도망가면 포기한다")]
        [Min(0.5f)] [SerializeField] float retaliateChaseRange = 8f;

        [Header("동료 구원 (교전 고정을 푸는 유일한 조건)")]
        [Tooltip("사거리 밖에 있는 적이라도 동료를 때리고 있으면 사거리 안까지 이동해 공격한다. " +
                 "끄면 교전이 시작된 자리에서만 싸운다")]
        [SerializeField] bool answerAllyCalls = true;

        [Tooltip("동료가 이 시간(초) 안에 맞았으면 '지금 맞고 있다'로 본다. " +
                 "너무 길면 이미 끝난 싸움을 보고 달려간다")]
        [Min(0.2f)] [SerializeField] float allyCallMemorySeconds = 2f;

        [Tooltip("이 거리(타일) 안의 동료가 맞고 있을 때만 도우러 간다")]
        [Min(0.5f)] [SerializeField] float allyCallRange = 12f;

        [Header("사냥 (탐험 유형 '사냥' 이 물린 중립 몬스터)")]
        [Tooltip("사냥감을 물면 <b>죽을 때까지</b> 쫓는다 — 이 값은 그 예외인 '추격 포기 한계'다.\n" +
                 "사냥을 시작한 지점에서 사냥감이 이 거리(타일) 밖으로 도망가면 포기하고 원래 " +
                 "임무로 돌아간다. 0 이면 한계 없음(맵 끝까지 쫓는다).\n" +
                 "⚠️ 일반 목줄(leashRange)은 사냥에 적용하지 않는다 — 목줄의 기준점이 " +
                 "'지금 걸어가는 탐험 목적지' 라서, 적용하면 사냥감을 물린 즉시 놓아버린다")]
        [Min(0f)] [SerializeField] float huntPursuitTiles = 24f;

        [Header("교전 개시 위치")]
        [Tooltip("원거리·마법이 교전을 시작하기 전 최대 사거리까지 물러나 자리를 잡을 때, " +
                 "시작 지점에서 이 거리(타일)까지만 물러난다. 넘으면 그 자리에서 교전을 고정한다 — " +
                 "적이 더 빠르면 한계가 없을 때 전투 지역을 통째로 벗어난다")]
        [Min(0f)] [SerializeField] float openingRepositionMaxTiles = 4f;

        // ------------------------------------------------------------------
        // 전술 지침 (캐릭터 전용). CharacterTactics 가 UI 값으로 덮어쓴다.
        // 몬스터는 이 값을 아무도 안 건드리므로 기본값(근거리·가장 가까운 적·추격) 그대로 돌아간다.
        // 아래 숫자는 전부 인스펙터에서 조정하는 값이다 — Character_Template 에서 고치면
        // 새로 생성되는 캐릭터 전원에게 적용된다(진행상황 5절).
        // ------------------------------------------------------------------

        [Header("전술 — 공격 유형")]
        [Tooltip("근거리: 기존 공격 그대로 / 원거리: 히트 스캔 / 마법: 정사각 범위 / 치유: 아군 회복")]
        [SerializeField] TacticalAttackType attackType = TacticalAttackType.Melee;

        [Tooltip("원거리 히트 스캔 사거리(타일). 투사체 없이 즉시 적중한다")]
        [Min(0.5f)] [SerializeField] float rangedRangeTiles = 5f;

        [Tooltip("마법 최소 사거리(타일). 이보다 가까운 적은 노리지 않고 거리를 벌린다")]
        [Min(0f)] [SerializeField] float magicMinRangeTiles = 2f;

        [Tooltip("마법 최대 사거리(타일)")]
        [Min(0.5f)] [SerializeField] float magicMaxRangeTiles = 6f;

        [Tooltip("자기 주변 이 반경(타일) 안에 있는 적은 마법으로 때릴 수 없다 " +
                 "(범위 공격이 자기 발밑에서 터지지 않게 하는 안전 반경)")]
        [Min(0f)] [SerializeField] float magicSafeRadiusTiles = 1f;

        [Tooltip("마법 착탄 범위의 한 변(타일). 2 면 2x2 정사각")]
        [Min(0.5f)] [SerializeField] float magicAreaTiles = 2f;

        [Tooltip("치유 사거리(타일)")]
        [Min(0.5f)] [SerializeField] float healRangeTiles = 3f;

        [Tooltip("치유량 = 공격력 × 이 퍼센트 ÷ 100. 100 이면 '공격력 수치만큼' 회복시킨다")]
        [Min(0)] [SerializeField] int healPercentOfAttack = 100;

        [Tooltip("원거리·마법은 벽 너머의 적을 못 때리게 한다. 끄면 벽을 관통한다")]
        [SerializeField] bool requireLineOfSight = true;

        [Header("회피")]
        [Tooltip("서로 겹치지 않게 밀어내는 반경(타일). 0 이면 겹침 허용")]
        [Min(0f)] [SerializeField] float separationRadius = 0.55f;
        [Min(0f)] [SerializeField] float separationStrength = 1.4f;

        [Header("바라보는 방향")]
        [Tooltip("켜면 좌우로 이동할 때 스프라이트를 뒤집어 진행 방향을 보게 한다. " +
                 "코어 키퍼·스타듀 밸리 방식 — 위아래로 이동할 때는 뒤집지 않고 " +
                 "마지막 좌우 방향을 유지한다(스프라이트가 항상 화면에 수평으로 선다)")]
        [SerializeField] bool flipSpriteToFaceMovement = false;

        [Tooltip("스프라이트 원본이 오른쪽을 보고 있으면 켠다. 왼쪽을 보고 있으면 끈다. " +
                 "정면을 보는 아트라면 어느 쪽이든 좌우가 뒤집혀 보일 뿐이니 " +
                 "보기 좋은 쪽으로 고르면 된다")]
        [SerializeField] bool spriteFacesRight = true;

        [Tooltip("이 값보다 좌우 이동 성분이 작으면 방향을 바꾸지 않는다. " +
                 "거의 수직으로 움직일 때 좌우로 덜덜 떨리는 것을 막는다")]
        [Min(0f)] [SerializeField] float flipDeadzone = 0.15f;

        [Header("길찾기")]
        [Tooltip("켜면 목적지까지 A* 경로를 계산해 벽을 돌아간다. 끄면 직선 이동 + " +
                 "벽 슬라이딩만 하므로 오목한 지형에서 멈춘다")]
        [SerializeField] bool usePathfinding = true;

        [Tooltip("이 시간 동안 거의 못 움직이면 막힌 것으로 보고 경로를 다시 계산한다(초)")]
        [Min(0.1f)] [SerializeField] float stuckCheckInterval = 0.4f;

        [Header("디버그")]
        [SerializeField] bool drawGizmos = true;

        DamageableUnit _self;
        FlowFieldService _flowField;
        MapGenerator _mapGenerator;
        GridPathfinder _pathfinder;
        FogOfWarService _fog;
        Vector3 _homePosition;
        DamageableUnit _target;
        float _nextAttackTime;
        float _nextRetargetTime;
        CombatState _state = CombatState.Idle;

        // 정찰 중 중립 몬스터 사냥 등, 평소 진영 판정(Faction.Opposite)을 거치지 않고
        // "이 상대를 지금 공격하라"고 외부에서 지정하는 타겟. 설정돼 있으면 일반 탐색보다
        // 우선한다 — CharacterBehavior 가 이 훅으로 사냥을 건다.
        DamageableUnit _huntOverrideTarget;

        // 사냥을 시작한 자리. 추격 포기 한계(huntPursuitTiles)의 기준점이다 — 목줄의 기준점
        // (_homePosition = 지금 걸어가는 탐험 목적지)을 쓰면 사냥이 시작조차 못 한다.
        Vector3 _huntOrigin;

        // ── 전술 지침 런타임 상태 ────────────────────────────────────────────
        // 직렬화하지 않는다 — 지침의 정본은 CharacterTactics 이고, 여기 값은 그것이
        // Start 와 변경 시마다 밀어 넣는 사본이다(두 곳에 저장하면 어긋난다).

        /// <summary>전술 타겟 규칙(거리·강함·체력)을 쓸지. <see cref="ApplyTactics"/> 가 켠다 = 캐릭터.</summary>
        bool _tacticalTargeting;

        TacticalTargetPriority _targetMode = TacticalTargetPriority.Nearest;
        TacticalAttackReaction _reaction = TacticalAttackReaction.Chase;

        /// <summary>후퇴 중 — 타겟을 잡지도 공격하지도 않는다. <c>CharacterBehavior</c> 가 켠다.</summary>
        bool _combatSuppressed;

        /// <summary>공격 유형이 강제된 상태인지 (정신 이상 "혼란" 의 근거리 고정).</summary>
        bool _attackTypeForced;

        /// <summary>강제 전환 전의 공격 유형. 해제 시 이 값으로 되돌린다.</summary>
        TacticalAttackType _attackTypeBeforeForce;

        /// <summary>마법 최소 사거리 안으로 적이 들어와 거리를 벌리는 중.</summary>
        bool _backOff;

        /// <summary>"사거리에 들어올 때까지 대기" 반응으로 타겟을 쫓지 않고 자리를 지키는 중.</summary>
        bool _holdingGround;

        /// <summary>
        /// ★ <b>교전 고정</b> — 지금 타겟이 <b>한 번이라도 사거리 안에 들어온 뒤</b> true.
        ///
        /// <b>왜 필요했나 (유저 리포트 2026-08-11)</b> — 중위·후방은 매 프레임
        /// <see cref="_standoffTiles"/>(최대 사거리)를 유지하려 하는데, 적이 다가오는 만큼
        /// 계속 물러나므로 <b>전투 지역을 완전히 벗어날 때까지 뒷걸음질</b>을 쳤다.
        /// 교전이 시작된 뒤에는 <b>그 자리에서 쏘는 것</b>이 맞다 — 이 값이 켜지면
        /// <see cref="DecideState"/> 가 유지 거리 때문에 물러나는 분기를 건너뛴다.
        ///
        /// ⚠️ <b>못 때리는 거리(<see cref="MinAttackDistance"/>) 때문에 벌리는 것은 막지 않는다</b> —
        /// 그건 취향이 아니라 "가만히 있으면 아무도 못 때린다"는 물리적 제약이다.
        /// 사거리 밖으로 나간 적을 쫓는 것도 그대로다. 막는 것은 <b>물러나는 이동 하나뿐</b>이다.
        /// </summary>
        bool _engaged;

        /// <summary>교전 고정이 걸린 상대. 타겟이 바뀌면 고정을 푼다.</summary>
        DamageableUnit _engagedWith;

        /// <summary>교전 전 <b>개시 위치를 잡는 중</b>(원거리·마법이 최대 사거리까지 물러나는 구간).</summary>
        bool _repositioning;

        /// <summary>개시 위치 잡기를 시작한 지점. 여기서 얼마나 멀어졌는지로 한계를 잰다.</summary>
        Vector3 _openingAnchor;

        /// <summary>
        /// 사거리 밖이지만 <b>동료를 때리고 있는</b> 적을 잡으러 가는 중.
        /// 이 동안에는 교전 고정도 "대기" 반응도 무시하고 사거리 안까지 이동한다
        /// (유저 지시: "최대 사거리 밖에 적이 있는데 그 적이 동료 캐릭터를 공격하면
        /// 사거리 내의 거리로 이동해서 공격").
        /// </summary>
        bool _answeringAllyCall;

        /// <summary>
        /// ★ <b>후퇴 사격(카이팅)</b> — 넥서스 쪽으로 물러나면서 사거리 안의 적을 쏜다.
        /// <c>CharacterBehavior</c> 가 <b>체력 기준 후퇴</b>(본인 또는 전방 아군)에서만 켠다.
        ///
        /// 이 상태에서만 <b>이동과 공격이 동시에</b> 일어난다. 평소에는 상태 기계가
        /// Attack 이거나 Chase 이거나 둘 중 하나다.
        ///
        /// ⚠️ 이때도 <b>쫓아가지는 않는다</b> — 이동 목적지는 언제나 후퇴 지점(<see cref="_homePosition"/>)이고
        /// 사거리 안에 들어온 적만 쏜다. 예전에 후퇴 중 전투를 통째로 껐던 이유가
        /// "물러나는 길에 마주친 적을 다시 쫓아가느라 영영 못 빠져나온다" 였는데,
        /// 이동 목적지를 후퇴 지점으로 고정하면 그 문제가 원천적으로 안 생긴다.
        /// </summary>
        bool _retreatFiring;

        /// <summary>
        /// 중립 진영에만 적용되는 적대 억제 — 전술 지침의 탐험 유형 '탐색'.
        /// 자세한 내용은 <see cref="SetNeutralHostilitySuppressed"/>.
        /// </summary>
        bool _neutralHostilitySuppressed;

        /// <summary>
        /// 교전 중 타겟과 유지하려는 거리(타일). 0 이하면 예전처럼 타겟에 그대로 붙는다.
        /// <c>CharacterBehavior</c> 가 전술 포지션(전방/중위/후방)에 따라 매 프레임 밀어 넣는다 —
        /// 지침의 정본은 그쪽이므로 여기서는 직렬화하지 않는다.
        /// </summary>
        float _standoffTiles;

        /// <summary>
        /// 유지 거리 판정의 여유(타일). 0 이면 밀림(separation)에 밀릴 때마다 전진/후퇴가
        /// 뒤집혀 제자리에서 덜덜 떤다.
        /// </summary>
        const float StandoffTolerance = 0.6f;

        /// <summary>마법 범위 공격 대상 임시 버퍼. 프레임마다 새 리스트를 만들지 않으려고 정적으로 둔다.</summary>
        static readonly List<DamageableUnit> _splashScratch = new List<DamageableUnit>(16);

        // 벽 슬라이딩 시 좌/우 중 어느 쪽을 먼저 시도할지. 유닛마다 고정해서
        // 같은 장애물 앞에서 여러 유닛이 서로 다른 방향으로 흩어지게 한다.
        int _slideSign;

        // 스프라이트 뒤집기용. 마지막으로 인식한 좌우 방향을 유지한다.
        SpriteRenderer _sprite;
        int _facingSign = 1;

        // 경로 추종 상태
        readonly List<Vector3> _path = new List<Vector3>();
        int _pathIndex;
        Vector3 _pathGoal;
        bool _hasPathGoal;
        float _nextRepathTime;
        int _failedRepaths;

        // 막힘 감지
        Vector3 _lastStuckSamplePos;
        float _stuckTimer;
        bool _destinationUnreachable;

        // 갇힘 탈출 후보 재사용 버퍼 — 매 프레임 새로 할당하지 않는다.
        readonly HashSet<Vector3Int> _embedRejectScratch = new HashSet<Vector3Int>();

        // 타겟 재탐색 간격. 매 프레임 전체를 훑지 않도록 분산시킨다.
        const float RetargetInterval = 0.2f;

        // 경로를 다시 계산하기까지의 최소 간격. 매 프레임 A* 를 돌리지 않게 한다.
        const float RepathCooldown = 0.5f;

        // 웨이포인트에 이 거리 안으로 들어오면 다음 웨이포인트로 넘어간다.
        const float WaypointArriveDistance = 0.35f;

        // 목적지가 이만큼 움직이면 기존 경로를 버린다.
        const float GoalMoveTolerance = 1.5f;

        // 막힘 판정 — 이 거리보다 덜 움직였으면 못 움직인 것으로 본다.
        const float StuckMoveEpsilon = 0.05f;

        // 이만큼 연속으로 경로 계산에 실패하면 목적지 자체를 못 가는 곳으로 판단한다.
        const int UnreachableAfterFailures = 3;

        // 갇힘 탈출 시 "가장 가까운 빈 칸"을 몇 번까지 다시 찾아볼지 — 첫 후보가 벽 너머라 직선이
        // 막혀 있으면 그 칸을 제외하고 다음으로 가까운 칸을 본다.
        const int EmbedEscapeAttempts = 6;
        const int EmbedEscapeSearchRadius = 8;

        // 갇힘 탈출 직선 판정의 표본 간격(타일). 한 칸보다 촘촘해야 칸을 건너뛰지 않는다.
        const float EmbedEscapeLosStepTiles = 0.25f;

        public CombatState State => _state;
        public DamageableUnit Target => _target;

        /// <summary>
        /// 지금 목적지로 가는 길을 못 찾고 있는지. 행동 레이어(<c>CharacterBehavior</c>)가
        /// 이 값을 보고 타임아웃을 기다리지 않고 즉시 다른 목적지를 고르게 한다.
        /// </summary>
        public bool DestinationUnreachable => _destinationUnreachable;

        void Awake()
        {
            _self = GetComponent<DamageableUnit>();
            _sprite = GetComponent<SpriteRenderer>();
            _homePosition = transform.position;
            _slideSign = (GetInstanceID() & 1) == 0 ? 1 : -1;
            _lastStuckSamplePos = transform.position;
        }

        void Start()
        {
            _flowField = FindAnyObjectByType<FlowFieldService>();
            _mapGenerator = FindAnyObjectByType<MapGenerator>();
            if (usePathfinding) _pathfinder = FindAnyObjectByType<GridPathfinder>();
            if (respectFogOfWar) _fog = FindAnyObjectByType<FogOfWarService>();

            // 유닛마다 재탐색 시점을 흩어 프레임 부하를 고르게 한다.
            _nextRetargetTime = Time.time + Random.Range(0f, RetargetInterval);
        }

        /// <summary>
        /// 스포너가 정의값으로 파라미터를 덮어쓸 때 사용.
        ///
        /// <paramref name="type"/> 를 주면 공격 유형까지 정의 테이블에서 받는다.
        /// 유형별 사거리 필드(<see cref="rangedRangeTiles"/> 등)도 <paramref name="attack"/> 로
        /// 같이 맞춘다 — 안 그러면 <see cref="EffectiveAttackRange"/> 가 유형별 필드를 보므로
        /// 정의 테이블의 <c>attackRange</c> 가 조용히 무시된다.
        /// (캐릭터는 이 경로를 쓰지 않는다 — 전술 지침이 <see cref="ApplyTactics"/> 로 넣는다.)
        /// </summary>
        public void Configure(float detect, float attack, float speed, float aps,
                              bool advance, UnitKind[] priority, float leash = -1f,
                              TacticalAttackType? type = null)
        {
            detectRange = detect;
            attackRange = attack;
            moveSpeedTiles = speed;
            attacksPerSecond = aps;
            advanceToObjective = advance;
            if (priority != null && priority.Length > 0) targetPriority = priority;
            if (leash >= 0f) leashRange = leash;

            if (type.HasValue)
            {
                attackType = type.Value;
                switch (attackType)
                {
                    case TacticalAttackType.Ranged: rangedRangeTiles = attack; break;
                    case TacticalAttackType.Magic:  magicMaxRangeTiles = attack; break;
                    case TacticalAttackType.Heal:   healRangeTiles = attack; break;
                }
            }
        }

        /// <summary>무해한 유닛(비선공 중립 몬스터 등)에 써서 적을 인식/공격하지 못하게 한다.</summary>
        public void SetCanAcquireTargets(bool value) => canAcquireTargets = value;

        /// <summary>
        /// 맞았을 때 반격할지. <b>스폰할 때 데이터가 템플릿 값을 덮어쓰기 위해</b> 둔다 —
        /// 중립 몬스터의 선공/비선공은 표(<c>atk_take</c>) 한 칸이 정본인데, 템플릿 인스펙터에
        /// 이 값이 따로 켜져 있으면 표와 다르게 동작한다(유저 리포트 2026-08-13
        /// "비선공 선공 체크가 여러개 되어 있던데"). <see cref="SetCanAcquireTargets"/> 와 짝이다.
        /// </summary>
        public void SetCanRetaliate(bool value) => canRetaliate = value;

        /// <summary>
        /// 절대 움직이지 않는 유닛(포탑 등)으로 만든다.
        ///
        /// <b>이동속도 0 으로는 부족하다</b> — <see cref="CurrentSpeed"/> 는 0 을 "설정 안 함"
        /// 으로 보고 <see cref="BalanceConfigSO.moveSpeedTilesPerSecond"/> 로 폴백하기 때문에,
        /// 켜지 않으면 포탑이 사거리 밖의 적을 쫓아 걸어다닌다.
        /// </summary>
        public void SetImmobile(bool value) => immobile = value;

        /// <summary>범위 공격(마법 유형)의 판정 크기를 테이블 값으로 맞춘다 (포탑의 Splash).</summary>
        public void ConfigureSplash(float areaTiles, float minRange = 0f, float safeRadius = 0f)
        {
            magicAreaTiles = Mathf.Max(0.5f, areaTiles);
            magicMinRangeTiles = Mathf.Max(0f, minRange);
            magicSafeRadiusTiles = Mathf.Max(0f, safeRadius);
        }

        /// <summary>
        /// 귀환 지점을 지정한다. 전진하지 않는 유닛(캐릭터·포탑)은 타겟이 없으면
        /// 이 지점으로 걸어가므로, 여기를 옮기는 것이 곧 "이동 명령"이 된다.
        /// </summary>
        public void SetHome(Vector3 worldPosition)
        {
            _homePosition = worldPosition;
            ResetPathState();
        }

        /// <summary>귀환 지점과 목줄 길이를 함께 지정한다.</summary>
        public void SetHome(Vector3 worldPosition, float leash)
        {
            _homePosition = worldPosition;
            if (leash >= 0f) leashRange = leash;
            ResetPathState();
        }

        /// <summary>
        /// 새 이동 명령을 받았으니 경로와 막힘 판정을 초기화한다.
        /// 여기서 <see cref="_destinationUnreachable"/> 를 반드시 내려야 한다 —
        /// 새 목적지가 직전 목적지와 가까우면(순찰 지점이 우연히 겹치는 경우)
        /// 목표 변경 감지에 걸리지 않아 깃발이 계속 서 있고, 그러면 행동 레이어가
        /// 매 프레임 목적지를 다시 고르며 제자리를 맴돈다.
        /// </summary>
        void ResetPathState()
        {
            _path.Clear();
            _hasPathGoal = false;
            _failedRepaths = 0;
            _destinationUnreachable = false;
            _nextRepathTime = 0f;
        }

        public Vector3 Home => _homePosition;

        /// <summary>
        /// 평소 진영 판정을 건너뛰고 이 상대를 사냥하도록 강제한다 (탐험 중 중립 몬스터 조우 등).
        /// 다음 <see cref="AcquireTargetIfNeeded"/> 에서 즉시 <see cref="Target"/> 으로 잡힌다.
        ///
        /// <b>★ 한 번 물면 죽을 때까지 놓지 않는다</b>(유저 확정 2026-08-12: "우선 타깃으로 설정한
        /// 몬스터는 잡고 나서 합류하는 로직"). 그래서 이 시점의 <b>자기 위치</b>를 사냥 시작점으로
        /// 기억해 두고, 포기 판정을 그 지점 기준 <see cref="huntPursuitTiles"/> 하나로만 한다 —
        /// 자세한 이유는 <see cref="AcquireTargetIfNeeded"/> 의 사냥 블록 주석 참조.
        /// </summary>
        public void SetHuntTarget(DamageableUnit target)
        {
            // 중립 적대가 억제된 상태(탐험 유형 '탐색')에서는 사냥감 자체를 받지 않는다 —
            // 억제를 한 곳에서만 검사하려면 들어오는 입구도 같이 막아야 한다.
            if (_neutralHostilitySuppressed && target != null && target.Faction == Faction.Neutral) return;

            // 사냥감이 바뀌는 순간에만 시작점을 다시 찍는다 — 매 프레임 갱신하면 추격 한계가
            // 계속 밀려나서 "한계 없음" 과 같아진다(같은 사냥감을 매 프레임 다시 넣는 호출부가 있다).
            if (!ReferenceEquals(_huntOverrideTarget, target)) _huntOrigin = transform.position;
            _huntOverrideTarget = target;
        }

        /// <summary>사냥을 포기한다. 이미 그 상대를 쫓고 있었다면 타겟도 함께 비운다.</summary>
        public void ClearHuntTarget()
        {
            if (_huntOverrideTarget != null && _target == _huntOverrideTarget) _target = null;
            _huntOverrideTarget = null;
        }

        /// <summary>지금 사냥 타겟을 쫓는 중인지 (일반 진영 전투와 구분해서 보고 싶을 때 사용).</summary>
        public bool IsHunting => _huntOverrideTarget != null && _huntOverrideTarget.IsAlive;

        /// <summary>
        /// 공격(또는 치유)을 실제로 한 번 수행한 순간. 공격 모션을 재생할 타이밍을
        /// <see cref="CharacterAnimator"/> 가 이걸로 받는다 — 애니메이터가 매 프레임 상태를
        /// 추측하는 대신 "지금 때렸다"는 사실 하나만 알면 되게 하려는 것.
        /// </summary>
        public event System.Action OnAttackPerformed;

        /// <summary>귀환 지점에 도착했는지.</summary>
        public bool IsAtHome(float tolerance = 0.5f) =>
            Vector2.Distance(transform.position, _homePosition) <= tolerance;

        // ------------------------------------------------------------------
        // 전술 지침 반영 (CharacterTactics 전용 진입점)
        // ------------------------------------------------------------------

        /// <summary>
        /// 전술 지침을 반영한다. <see cref="Configure"/>(스포너가 몬스터 정의값을 넣는 경로)와
        /// 겹치지 않게 별도 메서드로 뒀다 — 캐릭터는 <c>Configure</c> 를 부르지 않으므로
        /// 두 경로가 서로의 값을 덮어쓸 일이 없다.
        /// </summary>
        public void ApplyTactics(TacticalAttackType type, TacticalTargetPriority mode,
                                 TacticalAttackReaction reaction)
        {
            // 강제 유형(정신 이상 "혼란" 등)이 걸려 있는 동안에는 지침 값을 바로 반영하지 않고
            // 보관만 한다 — 안 그러면 상태가 풀릴 때 되돌릴 원본을 잃어버린다.
            if (_attackTypeForced) _attackTypeBeforeForce = type;
            else attackType = type;

            _targetMode = mode;
            _reaction = reaction;
            _tacticalTargeting = true;

            // 공격 유형이 바뀌면 사거리 자체가 달라지므로, 들고 있던 타겟은 버리고 다시 고른다.
            _target = null;
            _backOff = false;
            _nextRetargetTime = 0f;
        }

        /// <summary>
        /// 공격 유형을 일시적으로 강제한다 — 정신 이상 "혼란"이 <b>근거리 공격</b>으로 바꾸는 데 쓴다
        /// (유저 확정: "혼란의 공격 로직은 캐릭터가 자동으로 근거리 공격으로 바뀌도록").
        ///
        /// <b>왜 강제가 필요한가</b> — 혼란은 아군을 때리는 상태인데, 전술 공격 유형이 그대로면
        /// 치유형 캐릭터는 아군을 <b>회복</b>시켜 버리고(치유 유형의 정의가 "아군 회복"이다),
        /// 마법형은 <see cref="PerformMagicSplash"/> 가 <c>Opposite</c> 진영만 모으므로 아무에게도
        /// 피해를 주지 못한다. 근거리로 고정하면 <see cref="TryAttack"/> 의 기본 경로
        /// (<c>_target.TakeDamageFrom</c>)를 타서 대상이 아군이든 적이든 그대로 때린다.
        ///
        /// 원래 유형은 <see cref="ClearForcedAttackType"/> 에서 되돌린다. 강제 중에 전술 지침이
        /// 바뀌면 <see cref="ApplyTactics"/> 가 새 값을 보관해 두었다가 해제 시점에 그 값으로 돌아간다.
        /// </summary>
        public void SetForcedAttackType(TacticalAttackType type)
        {
            if (!_attackTypeForced)
            {
                _attackTypeBeforeForce = attackType;
                _attackTypeForced = true;
            }

            if (attackType == type) return;

            attackType = type;
            _target = null;
            _backOff = false;
            _nextRetargetTime = 0f;
        }

        /// <summary>강제 공격 유형을 풀고 원래(전술 지침) 유형으로 되돌린다.</summary>
        public void ClearForcedAttackType()
        {
            if (!_attackTypeForced) return;

            _attackTypeForced = false;
            attackType = _attackTypeBeforeForce;
            _target = null;
            _backOff = false;
            _nextRetargetTime = 0f;
        }

        /// <summary>후퇴 중처럼 "지금은 싸우지 않는다"를 켜고 끈다 (<c>CharacterBehavior</c> 가 호출).</summary>
        public void SetCombatSuppressed(bool value)
        {
            if (_combatSuppressed == value) return;
            _combatSuppressed = value;
            if (value) { _target = null; _huntOverrideTarget = null; _backOff = false; }
        }

        /// <summary>
        /// ★ <b>후퇴 사격(카이팅)</b>을 켜고 끈다. <c>CharacterBehavior</c> 가
        /// <b>체력 기준 후퇴</b>(본인 또는 전방 아군)에서만 켠다 — 유저 지시 2026-08-11:
        /// "후퇴하면서 공격하는 건 전방의 캐릭터나 본인 스스로의 체력이 후퇴 기준에 다다라서
        /// 넥서스 방향으로 후퇴할 때 발동되는 걸로".
        ///
        /// <b>공포(정신 이상)와 다르다</b> — 공포는 <see cref="SetCombatSuppressed"/> 로
        /// 전투 자체를 끈다(패닉이라 반격하지 않는다). 체력 후퇴는 물러나면서 쏜다.
        ///
        /// 켜는 쪽이 <b>이동 목적지(<see cref="SetHome"/>)를 후퇴 지점으로 잡아 두어야 한다</b> —
        /// 이 상태의 이동은 언제나 그 지점을 향한다.
        /// </summary>
        public void SetRetreatFiring(bool value)
        {
            if (_retreatFiring == value) return;
            _retreatFiring = value;

            // 후퇴에 들어가거나 빠져나올 때는 교전 고정을 푼다 — 상황이 완전히 바뀌었으므로
            // 다음 교전은 처음부터 다시 판단해야 한다.
            _engaged = false;
            _engagedWith = null;
            _repositioning = false;
            _backOff = false;
            if (value) _huntOverrideTarget = null;   // 물러나면서 사냥을 이어갈 수는 없다
        }

        /// <summary>지금 후퇴하면서 쏘는 중인지 (디버그·표시용).</summary>
        public bool IsRetreatFiring => _retreatFiring;

        /// <summary>
        /// <b>중립 몬스터에 대한 적대 행동만</b> 끈다 — 전술 지침의 탐험 유형 <b>'탐색'</b> 이
        /// 켜는 스위치다(유저 확정 2026-08-12: "선공 몹 마주칠 시 공격당해도 반격 안 하고 도망 감").
        ///
        /// <b>왜 <see cref="SetCombatSuppressed"/> 를 쓰지 않는가</b> — 그쪽은 전투를 통째로 끈다.
        /// 탐험 중이라도 <b>웨이브 몬스터(Cancer)와는 싸워야 하므로</b>, 억제 대상을
        /// 중립 진영으로만 좁힌 스위치가 따로 필요하다. 막는 경로는 세 개다:
        /// 반격(<see cref="FindRetaliationTarget"/>) · 동료 구원(<see cref="AttackerOf"/>) ·
        /// 사냥 강제(<see cref="SetHuntTarget"/>).
        ///
        /// 실제로 <b>도망가는 이동</b>은 <c>CharacterBehavior</c> 가 맡는다 — 여기서는
        /// "때리지 않는다"까지만 보장한다.
        /// </summary>
        public void SetNeutralHostilitySuppressed(bool value)
        {
            if (_neutralHostilitySuppressed == value) return;
            _neutralHostilitySuppressed = value;
            if (!value) return;

            _huntOverrideTarget = null;
            if (_target != null && _target.Faction == Faction.Neutral)
            {
                _target = null;
                _engaged = false;
                _engagedWith = null;
            }
        }

        /// <summary>지금 중립 몬스터를 건드리지 않는 상태인지 (탐험 유형 '탐색').</summary>
        public bool IsNeutralHostilitySuppressed => _neutralHostilitySuppressed;

        public TacticalAttackType AttackType => attackType;

        /// <summary>
        /// 교전 중 타겟과 유지할 거리를 지정한다(타일). 0 이하면 해제 — 예전처럼 타겟에 붙는다.
        ///
        /// <b>왜 필요한가</b> — <see cref="CombatState.Chase"/> 는 타겟 위치로 곧장 걸어가므로,
        /// 원거리·마법 캐릭터도 결국 "처음 사거리에 들어온 자리"에 서게 된다. 전열(전방/중위/
        /// 후방)을 유지하려면 <b>얼마나 떨어져 설지</b>를 밖에서 정해줄 수 있어야 한다.
        /// 자기 사거리 밖에 세우면 영영 못 때리므로 <see cref="EffectiveAttackRange"/> 로 자른다.
        /// </summary>
        public void SetStandoff(float tiles) =>
            _standoffTiles = tiles <= 0f ? 0f : Mathf.Min(tiles, EffectiveAttackRange);

        /// <summary>지금 유지하려는 교전 거리(타일). 0 이면 지정 없음.</summary>
        public float Standoff => _standoffTiles;

        /// <summary>지금 공격 유형에서 실제로 때릴 수 있는 거리(타일).</summary>
        public float EffectiveAttackRange => Mathf.Max(0.2f, _attackRangeBonus + attackType switch
        {
            TacticalAttackType.Ranged => rangedRangeTiles,
            TacticalAttackType.Magic  => magicMaxRangeTiles,
            TacticalAttackType.Heal   => healRangeTiles,
            _                         => attackRange,
        });

        /// <summary>
        /// 패시브가 얹는 사거리 보너스(타일). 직렬화하지 않는다 — 임시 상태이고 정본은
        /// <c>CharacterPassives</c> 가 들고 있다.
        ///
        /// <b>왜 유형별 필드를 직접 안 고치는가</b> — 사거리는 공격 유형에 따라 읽는 필드가
        /// 다르다(위 switch). 유형이 바뀌면 어느 필드에 보너스를 넣었는지 추적해야 하고,
        /// 전술 지침으로 유형이 바뀔 때 값이 새거나 두 번 걸린다. <b>합산 지점 한 곳</b>에
        /// 얹으면 유형이 바뀌어도 항상 정확하다.
        /// </summary>
        float _attackRangeBonus;

        /// <summary>사거리 보너스를 더한다. 해제할 때 같은 값을 음수로 넣는다.</summary>
        public void AddAttackRangeBonus(float delta) => _attackRangeBonus += delta;

        /// <summary>
        /// 실제 인식 거리. 사거리가 인식 거리보다 길면(원거리 5 / 마법 6 vs 인식 7 이하) 때릴 수
        /// 있는 적을 못 보는 모순이 생기므로 둘 중 큰 값을 쓴다.
        /// </summary>
        public float EffectiveDetectRange => Mathf.Max(detectRange, EffectiveAttackRange);

        /// <summary>
        /// 이 거리보다 <b>가까우면 때릴 수 없다</b>(타일). 마법의 "자기 주변은 못 친다" 규칙이
        /// 유일한 근원이고, 나머지 유형은 0 이다.
        ///
        /// <b>왜 프로퍼티로 뽑았나</b> — 예전에는 <see cref="DecideState"/> 가 안전 반경만,
        /// <see cref="BuildTargetFilter"/> 는 안전 반경과 최소 사거리 둘 다를 보고 있어서
        /// <b>두 판정이 서로 달랐다.</b> 그 틈(안전 반경 ~ 최소 사거리 사이)에 적이 들어오면
        /// 상태는 Attack 인데 실제로는 아무도 못 때려 <b>제자리에서 공격 모션만 나왔다</b>
        /// (유저 리포트). 한 곳에서 계산해 세 판정(타겟 선정·상태 결정·실제 타격)이 항상
        /// 같은 선을 쓰게 했다.
        /// </summary>
        public float MinAttackDistance => attackType == TacticalAttackType.Magic
            ? Mathf.Max(magicSafeRadiusTiles, magicMinRangeTiles)
            : 0f;

        /// <summary>
        /// 마법 착탄 범위의 한 변(타일). 마법 유형이 아니면 0.
        ///
        /// <b>왜 공개하나</b> — 착탄 연출(<see cref="CombatProjectileFx"/>)이 <b>실제 피해 범위와
        /// 같은 크기</b>로 그려지게 하려고 열었다. 예전에는 연출 크기가 원화 픽셀에 맞춰 손으로
        /// 고른 배율이라 "보이는 범위"와 "맞는 범위"가 서로 달랐다.
        /// </summary>
        public float MagicAreaTiles =>
            attackType == TacticalAttackType.Magic ? magicAreaTiles : 0f;

        // ------------------------------------------------------------------

        void Update()
        {
            if (_self == null || !_self.IsAlive)
            {
                _state = CombatState.Dead;
                return;
            }

            float dt = Time.deltaTime;

            // 벽 안에 갇혀 있으면 이동 판정이 전부 실패해 영구히 멈춘다. 먼저 빼낸다.
            if (EscapeIfEmbedded(dt)) return;

            AcquireTargetIfNeeded();
            DecideState();
            Act(dt);
            TrackStuck(dt);
        }

        /// <summary>
        /// 유닛이 막힌 칸 안에 있으면 가장 가까운 빈 칸으로 밀어낸다.
        /// 스폰 위치가 나중에 막히는 경우(넥서스가 <c>Start</c> 에서 자기 발판 칸을
        /// 등록하면 그 위에 있던 캐릭터가 갇힌다, 또는 유닛이 서 있는 칸에 포탑을 지은 경우 —
        /// <see cref="LastSanctuary.Buildings.BuildService"/> 의 배치 판정은 그 칸에 유닛이
        /// 있는지는 보지 않는다)가 실제로 있었고, 이 상태가 되면 <see cref="TryMoveTo"/> 의
        /// 모든 후보가 같은 막힌 칸이라 전부 실패해 캐릭터가 그 자리에서 완전히 얼어붙는다.
        ///
        /// <b>버그 수정(유저 리포트: "후퇴할 때 벽을 뚫는다")</b> — 아래에서 목표 지점까지
        /// <c>Vector3.MoveTowards</c> 로 직선 이동하되 충돌 판정을 끈다. 이건 의도적이다:
        /// 지금 서 있는 칸 자체가 막힌 칸이라, 한 걸음이라도 <see cref="IsBlocked"/> 를 거치면
        /// (그 걸음이 아직 같은 칸 안이라) 매번 막혀서 영원히 못 빠져나온다. 문제는
        /// <see cref="MapGenerator.TryFindPlaceableNear"/> 가 "가장 가까운 빈 칸"을 순수 거리로만
        /// 찾는다는 것 — 그 칸이 벽 반대편이면 직선 탈출 경로가 벽·다른 포탑을 그대로 관통한다
        /// (특히 후퇴 지점이 넥서스 주변 좁은 반경이라 포탑이 몰려 있어 실제로 자주 걸렸다).
        /// 그래서 후보를 거리순으로 받아보되 <see cref="HasEscapeLineOfSight"/> 로
        /// "직선 경로가 실제로 뚫려 있는지"까지 확인하고, 막혀 있으면 그 칸을 제외하고 다음으로
        /// 가까운 칸을 다시 찾는다 — 탈출 자체(막힌 칸에서 첫걸음 떼기)는 그대로 충돌 무시를
        /// 유지하면서, 그 직선이 다른 벽을 관통하지는 않게 보장한다.
        ///
        /// <b>★ 재수정(유저 리포트 2026-08-12: "건설 완료 시 캐릭터가 건물에 끼어서 못 움직인다")</b> —
        /// 위 직선 확인을 <see cref="GridPathfinder.HasLineOfSight"/> 로 하고 있었는데, 그 판정은
        /// <b>출발 지점(i=0)부터</b> 검사한다. 지금 서 있는 칸은 정의상 막힌 칸이므로
        /// <b>어느 후보를 넣어도 첫 표본에서 곧바로 false</b>가 되고, 후보를 6번 다 걸러낸 뒤
        /// <c>false</c> 를 돌려줘 탈출 자체가 취소됐다 → <see cref="TryMoveTo"/> 의 모든 후보가
        /// 같은 막힌 칸이라 전부 실패 → <b>캐릭터가 영구히 얼어붙는다.</b> 포탑을 다 지은 그 자리에
        /// 건설자가 서 있는 경우가 바로 이 상황이라 매번 재현됐다.
        /// 그래서 <b>출발 지점에 붙어 있는 막힌 구간(=지금 갇혀 있는 구조물)은 건너뛰고</b>
        /// 그 뒤에 또 벽이 나오는지만 보는 전용 판정(<see cref="HasEscapeLineOfSight"/>)으로 바꿨다.
        /// </summary>
        /// <returns>탈출 중이면 true — 이 프레임의 다른 이동은 건너뛴다.</returns>
        bool EscapeIfEmbedded(float dt)
        {
            if (_mapGenerator == null) return false;

            // 고정 구조물은 자기 발판을 스스로 "막힌 칸"으로 등록하므로 항상 갇힌 것처럼
            // 보인다 — 빼내려 들면 지어놓은 자리에서 기어나온다.
            if (immobile) return false;

            Vector3Int cell = _mapGenerator.WorldToCell(transform.position);
            if (!_mapGenerator.IsCellBlocked(cell)) return false;

            if (!TryFindEscapeCell(cell, out Vector3Int free)) return false;

            // 충돌 판정을 무시하고 빈 칸 쪽으로 곧장 이동한다(이미 막힌 칸이므로
            // 판정을 거치면 어디로도 못 간다) — 다만 위에서 그 직선이 실제로 뚫려 있는지는
            // 이미 확인했다.
            Vector3 goal = _mapGenerator.CellCenterWorld(free);
            float speed = CurrentSpeed();
            transform.position = Vector3.MoveTowards(transform.position, goal, speed * dt);
            FaceMovement(goal - transform.position);
            return true;
        }

        /// <summary>
        /// <see cref="EscapeIfEmbedded"/> 가 쓸 탈출 목표 칸. 거리순으로 후보를 받아보다가,
        /// 지금 위치에서 그 칸까지 직선이 실제로 뚫려 있는(<see cref="HasEscapeLineOfSight"/>)
        /// 첫 번째 칸을 쓴다 — 벽 너머의 "가장 가까운 빈 칸"을 그대로 뚫고 가지 않기 위함이다.
        ///
        /// ⚠️ <b>어느 후보도 통과하지 못하면 "가장 가까운 빈 칸"을 그대로 쓴다</b>(마지막 폴백).
        /// 여기서 <c>false</c> 를 돌려주면 <see cref="EscapeIfEmbedded"/> 가 아무것도 하지 않고,
        /// 그 결과 유닛이 <b>영구히 얼어붙는다</b> — 벽을 잠깐 스치는 것보다 훨씬 나쁜 결과다.
        /// (직선 확인은 "가능하면 안 뚫는다" 는 개선이고, 탈출 자체는 포기해선 안 된다.)
        /// </summary>
        bool TryFindEscapeCell(Vector3Int fromCell, out Vector3Int free)
        {
            _embedRejectScratch.Clear();
            bool haveFallback = false;
            Vector3Int fallback = default;

            for (int attempt = 0; attempt < EmbedEscapeAttempts; attempt++)
            {
                if (!_mapGenerator.TryFindPlaceableNear(fromCell, EmbedEscapeSearchRadius,
                                                        _embedRejectScratch.Contains, out free))
                    break;   // 반경 안에 (제외한 것 말고는) 더 이상 후보가 없다

                if (!haveFallback) { fallback = free; haveFallback = true; }   // 가장 가까운 빈 칸

                if (HasEscapeLineOfSight(transform.position, _mapGenerator.CellCenterWorld(free)))
                    return true;

                _embedRejectScratch.Add(free);
            }

            free = fallback;
            return haveFallback;
        }

        /// <summary>
        /// <b>막힌 칸에 갇힌 상태에서의</b> 직선 통행 판정.
        /// <see cref="GridPathfinder.HasLineOfSight"/> 와 같은 방식으로 선을 훑지만,
        /// <b>출발 지점에 붙어 있는 막힌 구간은 통과로 본다</b> — 그게 지금 갇혀 있는 구조물
        /// (발밑에 세워진 포탑·넥서스 발판) 자신이기 때문이다. 그 구간을 벗어난 뒤에 다시
        /// 막힌 칸이 나오면 그때는 "벽 너머" 이므로 false.
        ///
        /// 이 구분이 없으면 첫 표본(=자기 칸)에서 무조건 막혀서 <b>탈출이 아예 시작되지 않는다</b>
        /// — 그게 "건설 완료 시 캐릭터가 건물에 끼어서 못 움직인다" 버그의 원인이었다.
        /// <see cref="GridPathfinder"/> 쪽을 고치지 않은 이유는 그 판정이 <b>정상 이동·경로 평활화</b>
        /// 에서도 쓰이기 때문이다 — 거기서는 출발 칸이 막혀 있으면 실제로 막힌 것이 맞다.
        /// </summary>
        bool HasEscapeLineOfSight(Vector3 fromWorld, Vector3 toWorld)
        {
            if (_mapGenerator == null) return false;

            Vector2 d = toWorld - fromWorld;
            float dist = d.magnitude;
            if (dist < 1e-4f) return _mapGenerator.IsCellPlaceable(_mapGenerator.WorldToCell(fromWorld));

            int steps = Mathf.Max(1, Mathf.CeilToInt(dist / EmbedEscapeLosStepTiles));
            bool leftStart = false;

            for (int i = 0; i <= steps; i++)
            {
                Vector3 p = fromWorld + (Vector3)(d * (i / (float)steps));
                bool placeable = _mapGenerator.IsCellPlaceable(_mapGenerator.WorldToCell(p));

                if (placeable) { leftStart = true; continue; }

                // 아직 출발 구조물 안이면 통과. 한 번 나온 뒤라면 그건 진짜 벽이다.
                if (leftStart) return false;
            }

            // 목표 칸 자체는 반드시 빈 칸이어야 한다(빈 칸을 찾아온 것이므로 보통 참).
            return leftStart;
        }

        /// <summary>
        /// 이동하려는데 실제로 못 움직이고 있으면 경로를 다시 계산한다.
        /// 몇 번 연속 실패하면 목적지를 못 가는 곳으로 표시해, 행동 레이어가
        /// 타임아웃(정찰 15초)을 기다리지 않고 바로 다른 곳을 고르게 한다.
        /// </summary>
        void TrackStuck(float dt)
        {
            bool tryingToMove = _state == CombatState.Chase || _state == CombatState.Advance;
            if (!tryingToMove)
            {
                _stuckTimer = 0f;
                _lastStuckSamplePos = transform.position;
                return;
            }

            _stuckTimer += dt;
            if (_stuckTimer < stuckCheckInterval) return;

            float moved = Vector2.Distance(transform.position, _lastStuckSamplePos);
            _stuckTimer = 0f;
            _lastStuckSamplePos = transform.position;

            if (moved > StuckMoveEpsilon)
            {
                // 잘 움직이고 있다 — 실패 카운터를 되돌린다.
                _failedRepaths = 0;
                _destinationUnreachable = false;
                return;
            }

            // 못 움직였다 → 경로를 버리고 다음 프레임에 다시 계산하게 한다.
            _path.Clear();
            _nextRepathTime = 0f;
            if (++_failedRepaths >= UnreachableAfterFailures) _destinationUnreachable = true;
        }

        void AcquireTargetIfNeeded()
        {
            if (_combatSuppressed) { _target = null; return; }

            // 비선공 유닛 — 스스로 적을 찾지는 않지만, 맞았으면 때린 상대에게 반격한다.
            if (!canAcquireTargets) { _target = FindRetaliationTarget(); return; }

            // 치유 유형은 적을 아예 노리지 않는다 — "공격 대신 회복"이 이 유형의 정의다.
            if (attackType == TacticalAttackType.Heal) { AcquireHealTarget(); return; }

            // ★ 사냥은 <b>잡을 때까지</b> 고정한다 (유저 확정 2026-08-12).
            //
            // ⚠️ 예전에는 여기서 <c>leashRange</c>(귀환 지점 기준 목줄)로 사냥감을 놓았다.
            //    그게 유저가 리포트한 "사냥으로 설정하면 그냥 때리다가 바로 돌아가 버린다" 의
            //    정체다 — 목줄의 기준점 <c>_homePosition</c> 은 <b>지금 걸어가고 있는 탐험
            //    목적지</b>(CharacterBehavior 가 14~60타일 밖으로 잡는다)이고, 사냥감은
            //    <b>자기 주변</b> huntDetectRange(10타일) 에서 고른다. 두 기준이 달라서
            //    거의 항상 "목줄 밖" 으로 판정됐고, 사냥감을 물린 다음 프레임에 바로 놓고
            //    (중립은 진영 타겟팅에 안 걸리므로) 순찰 목적지로 되돌아갔다. 호출부가 매 프레임
            //    같은 사냥감을 다시 넣으니 붙었다 놓았다를 반복하며 "한두 대 때리고 돌아간다"가 됐다.
            //
            // 그래서 포기 조건을 <b>사냥 시작점 기준 추격 한계</b> 하나로 바꿨다 — 기준점이
            // 사냥을 시작한 자리이므로 "이 근처에서 시작한 싸움은 끝까지 한다" 가 성립하고,
            // 사냥감이 도망쳐 맵을 가로지르는 경우만 걸러진다. 사냥감이 죽으면 여기서 스스로
            // 비워지고, 그 순간 <c>CharacterBehavior</c> 가 평소 임무 판단으로 떨어져
            // 집결지·부대로 <b>합류</b>한다(진행상황 46-1절의 그 경로 그대로다 — 추가 코드 없음).
            if (_huntOverrideTarget != null)
            {
                if (!_huntOverrideTarget.IsAlive)
                {
                    _huntOverrideTarget = null;   // 사냥감이 죽었다 — 이제 평소 임무로 돌아간다
                }
                else if (huntPursuitTiles > 0f &&
                         Vector2.Distance(_huntOverrideTarget.transform.position, _huntOrigin) > huntPursuitTiles)
                {
                    _huntOverrideTarget = null;   // 사냥 시작점에서 너무 멀리 도망갔다 — 포기한다
                }
                else
                {
                    _target = _huntOverrideTarget;
                    return;
                }
            }

            bool targetInvalid = _target == null || !_target.IsAlive;

            // 유효한 타겟이 있어도 주기적으로 다시 훑어 더 우선순위 높은 적을 잡는다.
            if (!targetInvalid && Time.time < _nextRetargetTime) return;
            _nextRetargetTime = Time.time + RetargetInterval;

            System.Func<DamageableUnit, bool> filter = BuildTargetFilter();

            DamageableUnit found = _tacticalTargeting
                ? UnitRegistry.FindTargetBy(transform.position, _self.Faction, EffectiveDetectRange,
                                            _targetMode, filter)
                : UnitRegistry.FindTarget(transform.position, _self.Faction, detectRange,
                                          targetPriority, filter);

            // 목줄 밖의 적은 쫓지 않는다 (전진하지 않는 유닛에만 적용)
            if (found != null && !advanceToObjective && leashRange > 0f)
            {
                float d = Vector2.Distance(found.transform.position, _homePosition);
                if (d > leashRange) found = null;
            }

            // 아무도 못 찾았으면 "나를 때린 상대" 를 본다 — <b>선공 유닛도 맞으면 반격한다</b>
            // (유저 정의: "중립 몬스터는 언제든 공격받으면 반격해야 한다").
            // 이게 없으면 인식 범위(detectRange)나 목줄 밖에서 맞을 때 가만히 서서 맞기만 한다 —
            // 원거리 캐릭터가 사거리 밖에서 쏘는 상황이 정확히 그렇다. 반격 경로가
            // 비선공(canAcquireTargets=false) 유닛에만 붙어 있어서 생긴 구멍이었다.
            // 무한정 쫓지 않는 것은 FindRetaliationTarget 이 이미 보장한다
            // (공격력 0 · 8초 경과 · retaliateChaseRange 밖이면 놓는다).
            if (found == null) found = FindRetaliationTarget();

            // ★ 동료를 때리는 적 — 내 사거리 밖이라도 이쪽으로 간다 (유저 지시 2026-08-11).
            //
            // 지금 때릴 수 있는 적이 있으면 그대로 둔다 — 눈앞의 적을 두고 딴 데로 달려가면
            // 오히려 대열이 무너진다. <b>때릴 수 있는 적이 없을 때만</b> 동료를 돕는다.
            if (answerAllyCalls && !_retreatFiring && (found == null || !InAttackRange(found)))
            {
                DamageableUnit caller = FindAllyAttacker();
                if (caller != null) found = caller;
            }

            // 후퇴 사격 중에는 <b>때릴 수 있는 적만</b> 잡는다 — 쫓아가지 않는 것이 이 상태의 정의다.
            if (_retreatFiring && found != null && !InAttackRange(found)) found = null;

            _target = found;
        }

        /// <summary>타겟이 지금 당장 때릴 수 있는 거리인지 (못 때리는 최소 거리도 함께 본다).</summary>
        bool InAttackRange(DamageableUnit unit)
        {
            if (unit == null) return false;
            float d = Vector2.Distance(transform.position, unit.transform.position);
            return d <= EffectiveAttackRange + TargetRadius(unit) && d >= MinAttackDistance;
        }

        /// <summary>
        /// <b>지금 동료를 때리고 있는 적</b> 중 가장 가까운 하나. 없으면 null.
        ///
        /// <b>왜 새로 기록하지 않는가</b> — <see cref="DamageableUnit.LastAttacker"/> /
        /// <see cref="DamageableUnit.LastAttackedTime"/> 이 이미 "누가 언제 나를 때렸나"를
        /// 들고 있다(반격 로직이 쓰는 것과 같은 값). 동료 쪽에서 그걸 읽으면 되므로
        /// 이벤트 구독이나 별도 장부가 필요 없다.
        /// </summary>
        DamageableUnit FindAllyAttacker()
        {
            if (_self == null || allyCallRange <= 0f) return null;

            Vector3 myPos = transform.position;
            float limitSqr = allyCallRange * allyCallRange;
            DamageableUnit best = null;
            float bestSqr = float.MaxValue;

            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit ally = all[i];
                if (ally == null || !ally.IsAlive || ReferenceEquals(ally, _self)) continue;
                if (ally.Faction != _self.Faction) continue;
                if (((Vector2)(ally.transform.position - myPos)).sqrMagnitude > limitSqr) continue;

                DamageableUnit attacker = AttackerOf(ally);
                if (attacker == null) continue;

                float sqr = ((Vector2)(attacker.transform.position - myPos)).sqrMagnitude;
                // 인식 범위 밖까지 달려가지는 않는다 — 목줄과 같은 취지의 안전장치.
                if (sqr > EffectiveDetectRange * EffectiveDetectRange) continue;
                if (sqr >= bestSqr) continue;

                best = attacker;
                bestSqr = sqr;
            }
            return best;
        }

        /// <summary>이 적이 지금 내 동료를 때리고 있는지.</summary>
        bool IsAllyAttacker(DamageableUnit enemy)
        {
            if (!answerAllyCalls || enemy == null || _self == null) return false;

            Vector3 myPos = transform.position;
            float limitSqr = allyCallRange * allyCallRange;

            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit ally = all[i];
                if (ally == null || !ally.IsAlive || ReferenceEquals(ally, _self)) continue;
                if (ally.Faction != _self.Faction) continue;
                if (((Vector2)(ally.transform.position - myPos)).sqrMagnitude > limitSqr) continue;
                if (ReferenceEquals(AttackerOf(ally), enemy)) return true;
            }
            return false;
        }

        /// <summary>그 유닛을 <b>방금</b> 때린 적. 시간이 지났거나 죽었으면 null.</summary>
        DamageableUnit AttackerOf(DamageableUnit unit)
        {
            if (Time.time - unit.LastAttackedTime > allyCallMemorySeconds) return null;
            DamageableUnit attacker = unit.LastAttacker;
            if (attacker == null || !attacker.IsAlive) return null;
            if (attacker.Faction == _self.Faction) return null;   // 혼란으로 아군이 때린 경우

            // 탐험 유형 '탐색' — 중립 몬스터에게는 동료 구원도 나서지 않는다.
            // 여기서 막지 않으면 "반격은 안 하는데 동료를 돕겠다며 그 중립을 때리러 가는" 모순이 생긴다.
            if (_neutralHostilitySuppressed && attacker.Faction == Faction.Neutral) return null;
            return attacker;
        }

        /// <summary>
        /// 비선공 유닛의 반격 대상. "비선공"은 <b>먼저</b> 공격하지 않는다는 뜻이지 맞고도
        /// 가만히 있는다는 뜻이 아니다(유저 정의) — 맞았으면 때린 상대를 되받아친다.
        ///
        /// 반격을 그만두는 조건 셋: 공격력이 0(때려봐야 의미 없음) · 마지막으로 맞은 지
        /// <see cref="retaliateMemorySeconds"/> 초 경과 · 상대가 <see cref="retaliateChaseRange"/>
        /// 밖으로 벗어남. 셋 중 하나라도 걸리면 타겟을 놓고 원래의 무해한 상태로 돌아간다
        /// (그래야 배회하던 중립 몬스터가 캐릭터를 맵 끝까지 쫓아가지 않는다).
        /// </summary>
        DamageableUnit FindRetaliationTarget()
        {
            if (!canRetaliate || _self == null || _self.AttackStat <= 0) return null;
            if (Time.time - _self.LastAttackedTime > retaliateMemorySeconds) return null;

            DamageableUnit attacker = _self.LastAttacker;
            if (attacker == null || !attacker.IsAlive) return null;

            // 탐험 유형 '탐색' — 중립(선공 몹)에게 맞아도 반격하지 않는다(유저 확정 2026-08-12).
            // 그 자리를 벗어나는 것은 CharacterBehavior 의 도망 로직이 맡는다.
            if (_neutralHostilitySuppressed && attacker.Faction == Faction.Neutral) return null;

            float chase = Mathf.Max(retaliateChaseRange, EffectiveAttackRange);
            if (Vector2.Distance(attacker.transform.position, transform.position) > chase) return null;

            return attacker;
        }

        /// <summary>
        /// 타겟 후보를 거르는 조건을 한 벌로 묶는다. 안개(기존 규칙)에 더해, 전술 공격 유형이
        /// 요구하는 조건(원거리·마법의 시야선, 마법의 최소 사거리)을 여기서 같이 본다 —
        /// "때릴 수 없는 적을 타겟으로 잡고 계속 걸어가는" 상태를 애초에 안 만들기 위함이다.
        /// </summary>
        System.Func<DamageableUnit, bool> BuildTargetFilter()
        {
            bool useFog = respectFogOfWar && _fog != null && _fog.IsReady;
            bool needLos = requireLineOfSight && _pathfinder != null &&
                           (attackType == TacticalAttackType.Ranged || attackType == TacticalAttackType.Magic);
            bool needMinRange = MinAttackDistance > 0f;

            if (!useFog && !needLos && !needMinRange) return null;

            return enemy =>
            {
                if (useFog && !_fog.IsVisibleWorld(enemy.transform.position)) return false;

                if (needMinRange)
                {
                    float d = Vector2.Distance(enemy.transform.position, transform.position);
                    // 못 때리는 거리 안에 있으면 아예 후보에서 뺀다. 다만 "지금 붙어 있는 적"까지
                    // 완전히 무시하면 마법사가 자기를 때리는 적을 못 보고 가만히 서 있게 되므로,
                    // 그 상황은 DecideState 의 거리 벌리기(_backOff)가 대신 처리한다.
                    // 기준선은 MinAttackDistance 한 곳에서만 계산한다 — 예전에는 이 필터와
                    // DecideState 가 서로 다른 선을 써서 그 사이 구간에 "공격 모션만 나오는" 틈이 있었다.
                    if (d < MinAttackDistance) return false;
                }

                if (needLos && !_pathfinder.HasLineOfSight(transform.position, enemy.transform.position))
                    return false;

                return true;
            };
        }

        /// <summary>
        /// 치유 유형의 "타겟" — 사거리 안에서 가장 많이 다친 아군. 없으면 타겟 없음(귀환/순찰).
        /// 적 타겟과 같은 <see cref="_target"/> 슬롯을 쓰기 때문에, 이동·상태 판정
        /// (<see cref="DecideState"/>/<see cref="Act"/>)을 그대로 재사용할 수 있다 —
        /// 실제 "때리기"만 <see cref="TryAttack"/> 에서 회복으로 갈린다.
        /// </summary>
        void AcquireHealTarget()
        {
            bool targetInvalid = _target == null || !_target.IsAlive || _target.HpRatio >= 1f;
            if (!targetInvalid && Time.time < _nextRetargetTime) return;
            _nextRetargetTime = Time.time + RetargetInterval;

            DamageableUnit found = UnitRegistry.FindWoundedAlly(
                transform.position, _self.Faction, EffectiveDetectRange, _self);

            // 아군이라도 목줄 밖까지 쫓아가면 대열이 흐트러진다 — 적 타겟과 같은 규칙을 적용한다.
            if (found != null && !advanceToObjective && leashRange > 0f &&
                Vector2.Distance(found.transform.position, _homePosition) > leashRange)
                found = null;

            _target = found;
        }

        void DecideState()
        {
            _backOff = false;
            _holdingGround = false;

            // 타겟이 바뀌거나 죽으면 교전 고정을 푼다 — 새 상대와는 처음부터 다시 판단한다.
            if (_target == null || !_target.IsAlive || !ReferenceEquals(_target, _engagedWith))
            {
                _engaged = false;
                _engagedWith = null;
                _repositioning = false;
            }

            // 지금 타겟이 "동료를 때리고 있어서 잡으러 가는" 상대인지 매 프레임 다시 본다.
            // ⚠️ 타겟을 고를 때 한 번 계산해 두면, 재탐색 간격(0.2초) 동안이나 억제·사냥 경로로
            //    빠질 때 옛 값이 남아 <b>엉뚱한 순간에 자리를 뜬다.</b> 판정의 정본을 한 곳에 둔다.
            _answeringAllyCall = answerAllyCalls && !_retreatFiring &&
                                 _target != null && _target.IsAlive &&
                                 !InAttackRange(_target) && IsAllyAttacker(_target);

            // ★ 후퇴 사격 — 이동은 언제나 후퇴 지점으로. 공격은 Act 가 따로 얹는다.
            if (_retreatFiring)
            {
                _state = CombatState.Chase;
                return;
            }

            if (_target != null && _target.IsAlive)
            {
                float dist = Vector2.Distance(transform.position, _target.transform.position);

                // 때릴 수 없을 만큼 붙었으면(마법의 안전 반경·최소 사거리) 거리를 벌린다.
                // 가만히 있으면 상태만 Attack 이고 실제로는 아무도 못 때리는 상태가 된다.
                // ⚠️ 교전 고정 중에도 이건 살아 있다 — 취향이 아니라 물리적 제약이다.
                if (dist < MinAttackDistance)
                {
                    _backOff = true;
                    _state = CombatState.Chase;
                    return;
                }

                // 전열 유지 — 지정된 교전 거리보다 가까우면 그만큼 물러난다(후방·중위 포지션).
                // 여유(StandoffTolerance)를 빼야 밀림에 밀릴 때마다 전진/후퇴가 뒤집히지 않는다.
                //
                // ★ <b>교전이 시작된 뒤에는 물러나지 않는다</b>(<see cref="_engaged"/>).
                //    적이 다가오는 만큼 계속 뒤로 빠지면 전투 지역을 통째로 벗어나 버린다
                //    (유저 리포트 2026-08-11). 유지 거리는 <b>붙기 전에 자리를 잡는 용도</b>로만 쓴다 —
                //    원거리·마법은 이 구간에서 최대 사거리까지 물러나 자리를 잡고 시작한다.
                if (!_engaged && _standoffTiles > 0f && dist < _standoffTiles - StandoffTolerance)
                {
                    // 자리를 잡기 시작한 지점을 기억한다 — 여기서 얼마나 멀어졌는지가 한계의 기준.
                    if (!_repositioning)
                    {
                        _repositioning = true;
                        _openingAnchor = transform.position;
                    }

                    // ⚠️ <b>한계가 없으면 결국 전투 지역을 벗어난다</b> — 적이 나보다 빠르면
                    //    거리가 영영 안 벌어져서 계속 물러나기만 한다. 개시 위치를 잡는 이동은
                    //    시작 지점에서 이만큼까지만 허용하고, 넘으면 그 자리에서 교전을 고정한다.
                    if (Vector2.Distance(transform.position, _openingAnchor) <= openingRepositionMaxTiles)
                    {
                        _backOff = true;
                        _state = CombatState.Chase;
                        return;
                    }

                    _engaged = true;
                    _engagedWith = _target;
                }

                if (dist <= EffectiveAttackRange + TargetRadius(_target))
                {
                    // 사거리 안에 들어온 그 순간이 교전 시작이다 — 이후로는 자리를 지킨다.
                    _engaged = true;
                    _engagedWith = _target;
                    _state = CombatState.Attack;
                    return;
                }

                // "적이 사거리 내에 들어올 때까지 대기" — 쫓아가지 않고 자기 자리를 지킨다.
                // 몬스터(advanceToObjective)에는 적용하지 않는다.
                // ⚠️ 동료를 때리는 적을 잡으러 가는 중이면 대기하지 않는다 — 그 상황만이
                //    자리를 뜨는 유일한 사유다(유저 지시).
                // ⚠️ <b>사냥감도 예외다</b>(유저 확정 2026-08-12) — 사냥은 "찾아가서 잡는" 행동이라
                //    대기와 뜻이 정면으로 어긋난다. 여기서 막으면 사냥감이 사거리를 벗어나는 순간
                //    <b>자기 자리로 되돌아가</b>(아래 Chase→_homePosition) "때리다가 돌아간다"가 된다.
                bool huntingThisTarget = _huntOverrideTarget != null &&
                                         ReferenceEquals(_target, _huntOverrideTarget);
                if (_reaction == TacticalAttackReaction.HoldGround && !advanceToObjective &&
                    !_answeringAllyCall && !huntingThisTarget)
                {
                    _holdingGround = true;
                    _state = Vector2.Distance(transform.position, _homePosition) > 0.3f
                        ? CombatState.Chase       // 자리로 돌아가는 것도 이동으로 취급
                        : CombatState.Idle;
                    return;
                }

                _state = CombatState.Chase;
                return;
            }

            if (advanceToObjective) { _state = CombatState.Advance; return; }

            // 귀환이 필요한지
            _state = Vector2.Distance(transform.position, _homePosition) > 0.3f
                ? CombatState.Chase       // 제자리로 돌아가는 것도 이동으로 취급
                : CombatState.Idle;
        }

        void Act(float dt)
        {
            // ★ 후퇴 사격(카이팅) — 이 상태에서만 이동과 공격이 <b>동시에</b> 일어난다.
            // 이동 목적지는 언제나 후퇴 지점이라 적을 쫓아가는 일이 없고,
            // 사거리 안에 들어온 적만 쏜다. 바라보는 방향은 CharacterAnimator 가
            // "이동 중엔 진행 방향 · 때리는 순간엔 타겟 방향" 으로 갈라 준다.
            if (_retreatFiring)
            {
                if (!immobile) MoveToDestination(_homePosition, dt);
                if (InAttackRange(_target)) TryAttack();
                return;
            }

            switch (_state)
            {
                case CombatState.Attack:
                    TryAttack();
                    break;

                // 고정 구조물(포탑)은 상태 계산만 하고 이동은 하지 않는다.
                case CombatState.Chase:
                    if (!immobile) MoveToDestination(ChaseDestination(), dt);
                    break;

                case CombatState.Advance:
                    if (!immobile) AdvanceToObjective(dt);
                    break;
            }
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// <see cref="CombatState.Chase"/> 일 때 실제로 향할 지점.
        /// 세 갈래다 — 평소엔 타겟 쪽, 마법이 너무 붙었으면 <b>반대</b> 쪽, 대기 반응이거나
        /// 타겟이 없으면 자기 자리로.
        /// </summary>
        Vector3 ChaseDestination()
        {
            bool hasTarget = _target != null && _target.IsAlive;
            if (!hasTarget || _holdingGround) return _homePosition;

            float want = DesiredEngageDistance();
            if (want <= 0f) return _target.transform.position;

            // 목적지를 <b>타겟 기준</b>으로 잡는다 — 예전에는 "내 위치에서 뒤로 N" 이라
            // 매 프레임 목표가 같이 밀려나 끝없이 물러났다. 타겟에서 N 만큼 떨어진 점은
            // 고정점이라 그 자리에 정확히 수렴한다.
            Vector2 away = (Vector2)(transform.position - _target.transform.position);
            if (away.sqrMagnitude < 0.0001f) away = Vector2.right;   // 완전히 겹친 경우
            return _target.transform.position + (Vector3)(away.normalized * want);
        }

        /// <summary>
        /// 지금 타겟과 두고 싶은 거리(타일). 0 이면 그대로 붙는다.
        /// 못 때리는 최소 거리(<see cref="MinAttackDistance"/>)와 전열 유지 거리
        /// (<see cref="SetStandoff"/>) 중 큰 쪽을 쓰되, <b>자기 사거리를 넘지 않게</b> 자른다 —
        /// 사거리 밖에 서면 영영 못 때린다.
        /// </summary>
        float DesiredEngageDistance()
        {
            float min = MinAttackDistance;
            float want = Mathf.Max(_standoffTiles, min > 0f ? min + StandoffTolerance : 0f);
            return want <= 0f ? 0f : Mathf.Min(want, EffectiveAttackRange);
        }

        void TryAttack()
        {
            if (_combatSuppressed) return;
            if (Time.time < _nextAttackTime) return;

            // 때릴 수 없는 거리면 <b>모션도 내지 않는다</b>. DecideState 가 이미 걸러주지만,
            // 한 프레임 사이에 상대가 파고들 수 있어 여기서도 확인한다 — 이게 없으면
            // "제자리에서 공격 모션만 나오고 아무 일도 안 일어나는" 상태가 눈에 보인다.
            if (_target != null && MinAttackDistance > 0f &&
                Vector2.Distance(transform.position, _target.transform.position) < MinAttackDistance)
                return;

            // 능력치(공격 속도)가 있으면 그게 최우선이다 — 캐릭터만 해당하고,
            // 몬스터·포탑은 0 을 돌려주므로 기존 경로(인스펙터 값 → 밸런스 폴백)를 그대로 탄다.
            float statAps = _self.StatAttacksPerSecond;
            float aps = statAps > 0f
                ? statAps
                : (attacksPerSecond > 0f
                    ? attacksPerSecond
                    : (_self.Balance != null ? _self.Balance.attacksPerSecond : 1f));
            if (aps <= 0f) return;

            _nextAttackTime = Time.time + 1f / aps;

            // 공격한 쪽도 전투 상태로 기록해야 재생 대기 시간이 갱신된다.
            _self.MarkCombatAction();

            switch (attackType)
            {
                case TacticalAttackType.Heal:
                    PerformHeal();
                    break;

                case TacticalAttackType.Magic:
                    PerformMagicSplash();
                    break;

                // 근거리와 원거리는 "즉시 단일 타격"으로 동일하다 — 다른 건 사거리뿐이다
                // (원거리 = 히트 스캔, 투사체를 날리지 않는다).
                default:
                    _target.TakeDamageFrom(_self);
                    break;
            }

            OnAttackPerformed?.Invoke();
        }

        /// <summary>
        /// 마법 — 타겟 지점을 중심으로 한 정사각 범위 안의 적을 전부 때린다.
        /// 자기 주변 <see cref="magicSafeRadiusTiles"/> 안에 있는 적은 범위에 걸려도 제외한다
        /// (유저 규칙: "1의 범위 안에 있는 적은 공격 불가").
        ///
        /// ⚠️ <b>타겟 자신은 따로 확인해서 때린다.</b>
        /// <see cref="UnitRegistry.CollectEnemiesInBox"/> 는 <see cref="FactionExtensions.Opposite"/>
        /// 진영만 모으는데, <see cref="SetHuntTarget"/> 으로 잡은 중립 몬스터는 그 진영이 아니다
        /// (Angel 의 Opposite 는 Cancer 다). 그래서 <b>마법 캐릭터가 중립 몬스터를 사냥하면
        /// 범위에 아무도 안 잡혀 피해가 0 이었고, 공격 모션만 무한히 반복됐다</b>(유저 리포트).
        /// 29-3절이 정신 이상 "혼란"에서 같은 이유로 근거리를 강제한 것과 같은 종류의 함정이다.
        /// </summary>
        void PerformMagicSplash()
        {
            Vector3 center = _target.transform.position;
            UnitRegistry.CollectEnemiesInBox(center, magicAreaTiles * 0.5f, _self.Faction, _splashScratch);

            float safeSqr = magicSafeRadiusTiles * magicSafeRadiusTiles;
            Vector3 myPos = transform.position;
            bool hitTarget = false;

            for (int i = 0; i < _splashScratch.Count; i++)
            {
                DamageableUnit u = _splashScratch[i];
                if (u == null || !u.IsAlive) continue;
                if (((Vector2)(u.transform.position - myPos)).sqrMagnitude < safeSqr) continue;

                if (ReferenceEquals(u, _target)) hitTarget = true;
                u.TakeDamageFrom(_self);
            }

            if (!hitTarget && _target != null && _target.IsAlive &&
                ((Vector2)(_target.transform.position - myPos)).sqrMagnitude >= safeSqr)
                _target.TakeDamageFrom(_self);
        }

        /// <summary>
        /// 치유 — 공격력 수치(×퍼센트)만큼 아군을 회복시킨다.
        /// 정신 이상 "이기심"에 걸린 대상은 치유를 거부한다(<see cref="DamageableUnit.AcceptsExternalHeal"/>) —
        /// 그 상태의 정의가 "치유 불가(본인의 체력 재생 제외)" 라서, 재생 경로(<c>TickRegen</c>)는
        /// 그대로 두고 <b>외부에서 넣는 회복</b>만 여기서 막는다.
        /// </summary>
        void PerformHeal()
        {
            if (_self.Balance == null) return;
            if (!_target.AcceptsExternalHeal) return;

            int amount = _self.Balance.Attack(_self.AttackStat) * healPercentOfAttack / 100;
            if (amount <= 0) return;

            _target.Heal(amount);
        }

        void AdvanceToObjective(float dt)
        {
            // 플로우 필드가 있으면 벽을 피해 넥서스로 향한다.
            if (_flowField != null && _flowField.TryGetDirection(transform.position, out Vector2 dir))
            {
                Step(dir, dt);
                return;
            }

            // 플로우 필드가 없거나 이 칸에 방향이 없으면(고립된 구석 등) 길찾기로 간다.
            DamageableUnit nexus = UnitRegistry.FindFirst(_self.Faction.Opposite(), UnitKind.Nexus);
            if (nexus != null) MoveToDestination(nexus.transform.position, dt);
        }

        /// <summary>
        /// 목적지까지 이동한다. 직선으로 갈 수 있으면 그대로 가고(대부분의 경우),
        /// 벽이 끼면 <see cref="GridPathfinder"/> 로 경로를 얻어 웨이포인트를 따라간다.
        ///
        /// 예전에는 직선 이동 + 축 분리 슬라이딩만 했는데, 그 방식은 벽이 오목하거나
        /// 목적지가 벽 뒤에 있으면 원리적으로 빠져나올 수 없어 캐릭터가 벽에 붙어
        /// 멈춘 채 행동 타임아웃(정찰 15초)을 기다리는 일이 잦았다.
        /// </summary>
        void MoveToDestination(Vector3 destination, float dt)
        {
            if (_pathfinder == null) { MoveTowards(destination, dt); return; }

            // 목적지가 크게 바뀌었으면 기존 경로를 버린다.
            if (!_hasPathGoal ||
                (destination - _pathGoal).sqrMagnitude > GoalMoveTolerance * GoalMoveTolerance)
            {
                _path.Clear();
                _pathGoal = destination;
                _hasPathGoal = true;
                _failedRepaths = 0;
                _destinationUnreachable = false;
            }

            // 직선으로 통하면 경로가 필요 없다. 추격 중 이 경우가 대부분이라
            // A* 호출 자체를 대체로 건너뛰게 된다.
            if (_pathfinder.HasLineOfSight(transform.position, destination))
            {
                _path.Clear();
                MoveTowards(destination, dt);
                return;
            }

            if (_path.Count == 0 && Time.time >= _nextRepathTime)
            {
                _nextRepathTime = Time.time + RepathCooldown;
                if (_pathfinder.TryFindPath(transform.position, destination, _path))
                {
                    _pathIndex = 0;
                }
                else
                {
                    _path.Clear();
                    if (++_failedRepaths >= UnreachableAfterFailures) _destinationUnreachable = true;
                }
            }

            if (_path.Count == 0)
            {
                // 경로를 못 얻었다 — 그래도 목적지 쪽으로 밀어보며 슬라이딩에 맡긴다.
                MoveTowards(destination, dt);
                return;
            }

            // 이미 지나친 웨이포인트를 건너뛴다.
            while (_pathIndex < _path.Count &&
                   Vector2.Distance(transform.position, _path[_pathIndex]) <= WaypointArriveDistance)
                _pathIndex++;

            if (_pathIndex >= _path.Count)
            {
                _path.Clear();
                MoveTowards(destination, dt);
                return;
            }

            MoveTowards(_path[_pathIndex], dt);
        }

        void MoveTowards(Vector3 destination, float dt)
        {
            Vector2 delta = destination - transform.position;
            if (delta.sqrMagnitude < 0.0004f) return;
            Step(delta.normalized, dt);
        }

        void Step(Vector2 direction, float dt)
        {
            Vector2 move = direction + Separation() * separationStrength;
            if (move.sqrMagnitude > 0.0001f) move.Normalize();

            FaceMovement(move);
            MoveWithCollision((Vector3)(move * CurrentSpeed() * dt));
        }

        float CurrentSpeed()
        {
            // 능력치(이동속도)가 있으면 그게 최우선. 몬스터·포탑은 0 이라 기존 경로를 그대로 탄다.
            float statSpeed = _self != null ? _self.StatMoveSpeedTiles : 0f;
            if (statSpeed > 0f) return statSpeed;

            return moveSpeedTiles > 0f
                ? moveSpeedTiles
                : (_self != null && _self.Balance != null ? _self.Balance.moveSpeedTilesPerSecond : 3f);
        }

        /// <summary>
        /// 스프라이트를 좌우로만 뒤집어 진행 방향을 보게 한다 (코어 키퍼·스타듀 밸리 방식).
        /// 트랜스폼을 회전시키면 위로 갈 때 스프라이트가 눕거나 뒤집혀 보이므로,
        /// 세로 이동은 방향에 반영하지 않고 마지막 좌우 방향을 그대로 유지한다.
        /// </summary>
        void FaceMovement(Vector2 move)
        {
            if (!flipSpriteToFaceMovement || _sprite == null) return;
            if (Mathf.Abs(move.x) < flipDeadzone) return;   // 거의 수직 이동 → 유지

            _facingSign = move.x > 0f ? 1 : -1;
            _sprite.flipX = spriteFacesRight ? _facingSign < 0 : _facingSign > 0;
        }

        /// <summary>
        /// 유닛에 Collider2D 가 없어 물리 충돌이 걸리지 않으므로, 벽 타일맵을
        /// 직접 검사해 막힌 칸으로는 못 들어가게 한다.
        ///
        /// 경로 자체는 <see cref="MoveToDestination"/> 의 A* 가 잡아주므로, 여기서는
        /// 벽을 스치는 정도만 처리한다 — 정면으로 막히면 축을 하나씩 분리해 미끄러뜨리고,
        /// 그래도 안 되면 진행 방향에 수직으로 살짝 흘린다. (예전에는 여기서
        /// 150도까지 꺾어보며 우회를 시도했는데, 한 프레임 이동량만큼만 움직일 수
        /// 있어 효과는 거의 없고 뒤로 밀려 덜덜 떠는 부작용만 있었다.)
        /// </summary>
        void MoveWithCollision(Vector3 delta)
        {
            if (delta.sqrMagnitude < 1e-8f) return;

            Vector3 pos = transform.position;
            if (TryMoveTo(pos + delta)) return;
            if (delta.x != 0f && TryMoveTo(pos + new Vector3(delta.x, 0f, 0f))) return;
            if (delta.y != 0f && TryMoveTo(pos + new Vector3(0f, delta.y, 0f))) return;

            // 벽을 정면으로 마주봤다 — 벽면을 따라 옆으로 흘린다.
            Vector2 dir = ((Vector2)delta).normalized;
            float dist = delta.magnitude;
            var perpendicular = new Vector2(-dir.y, dir.x) * _slideSign;
            if (TryMoveTo(pos + (Vector3)(perpendicular * dist))) return;
            TryMoveTo(pos + (Vector3)(-perpendicular * dist));
        }

        bool TryMoveTo(Vector3 candidate)
        {
            if (IsBlocked(candidate)) return false;
            transform.position = candidate;
            return true;
        }

        bool IsBlocked(Vector3 worldPos) =>
            _mapGenerator != null && _mapGenerator.IsCellBlocked(_mapGenerator.WorldToCell(worldPos));

        /// <summary>주변 유닛에서 밀어내는 힘. 겹쳐서 한 덩어리로 뭉치는 걸 막는다.</summary>
        Vector2 Separation()
        {
            if (separationRadius <= 0f) return Vector2.zero;

            Vector2 push = Vector2.zero;
            var all = UnitRegistry.All;
            Vector3 myPos = transform.position;

            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit other = all[i];
                if (other == null || other == _self || !other.IsAlive) continue;
                if (other.Kind == UnitKind.Nexus || other.Kind == UnitKind.Tower) continue;

                Vector2 d = myPos - other.transform.position;
                float sqr = d.sqrMagnitude;
                if (sqr > separationRadius * separationRadius || sqr < 0.0001f) continue;

                // 가까울수록 강하게 밀어낸다
                push += d.normalized * (1f - Mathf.Sqrt(sqr) / separationRadius);
            }
            return push;
        }

        /// <summary>큰 유닛(넥서스 등)은 중심까지 갈 수 없으므로 반경을 더해준다.</summary>
        static float TargetRadius(DamageableUnit target)
        {
            if (target.Kind == UnitKind.Nexus)
            {
                // ⚠ 예전에는 <c>transform.localScale.x</c> 를 읽었다 — 그건 <b>스프라이트를 몇 배로
                //   그리는지</b>(픽셀 기준 배율)라서, 아트를 다시 임포트해 PPU 가 바뀌면 근접
                //   유닛이 넥서스에 파고들거나 허공을 때리게 된다. 점유 칸 수(타일)가 정본이다.
                if (target is Units.Nexus nexus && nexus.Definition != null)
                    return Mathf.Max(0.5f, nexus.Definition.footprintTiles * 0.5f);

                return Mathf.Max(0.5f, target.transform.localScale.x * 0.5f);
            }

            // 몸집이 큰 몬스터(보스·중간보스) — 발판 크기만큼 반경을 준다.
            // 없으면(일반 몬스터는 발판 1칸) 아래 기본값으로 떨어진다.
            // ⚠ 이게 없으면 근거리 유닛이 <b>보스 중심까지</b> 들어가려 하고, 발판이 커질수록
            //   "몸 안으로 파고드는" 모습이 된다. 반대로 너무 크게 주면 허공을 때린다 —
            //   그래서 MonsterUnit 은 가로·세로 중 <b>작은 쪽</b>의 절반을 돌려준다.
            if (target is Units.MonsterUnit monster)
            {
                float body = monster.BodyRadiusTiles;
                if (body > 0.4f) return body;
            }

            return 0.4f;
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, EffectiveDetectRange);

            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, EffectiveAttackRange);

            // 못 때리는 최소 거리 — 이 안의 적은 공격이 안 나간다(마법의 안전 반경·최소 사거리).
            if (MinAttackDistance > 0f)
            {
                Gizmos.color = new Color(0.8f, 0.4f, 1f, 0.7f);
                Gizmos.DrawWireSphere(transform.position, MinAttackDistance);
            }

            // 전열 유지 거리 — 타겟에게서 이만큼 떨어져 싸우려 한다(후방·중위 포지션).
            // 지금 그보다 붙어 있어 물러나는 중이면 밝게 표시한다.
            if (Application.isPlaying && _target != null && _standoffTiles > 0f)
            {
                Gizmos.color = _backOff ? new Color(1f, 0.9f, 0.3f, 0.95f)
                                        : new Color(0.4f, 1f, 0.8f, 0.5f);
                Gizmos.DrawWireSphere(_target.transform.position, _standoffTiles);
            }

            if (!advanceToObjective && leashRange > 0f)
            {
                Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
                Gizmos.DrawWireSphere(Application.isPlaying ? _homePosition : transform.position,
                                      leashRange);
            }

            if (Application.isPlaying && _target != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, _target.transform.position);
            }

            // 계산된 우회 경로. 벽에 걸릴 때 어디로 돌아가려는지 눈으로 확인할 수 있다.
            if (Application.isPlaying && _path.Count > 0)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
                Vector3 prev = transform.position;
                for (int i = _pathIndex; i < _path.Count; i++)
                {
                    Gizmos.DrawLine(prev, _path[i]);
                    Gizmos.DrawWireCube(_path[i], Vector3.one * 0.25f);
                    prev = _path[i];
                }
            }
        }
    }
}
