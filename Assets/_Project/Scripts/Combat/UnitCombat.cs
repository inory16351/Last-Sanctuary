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

        // ── 전술 지침 런타임 상태 ────────────────────────────────────────────
        // 직렬화하지 않는다 — 지침의 정본은 CharacterTactics 이고, 여기 값은 그것이
        // Start 와 변경 시마다 밀어 넣는 사본이다(두 곳에 저장하면 어긋난다).

        /// <summary>전술 타겟 규칙(거리·강함·체력)을 쓸지. <see cref="ApplyTactics"/> 가 켠다 = 캐릭터.</summary>
        bool _tacticalTargeting;

        TacticalTargetPriority _targetMode = TacticalTargetPriority.Nearest;
        TacticalAttackReaction _reaction = TacticalAttackReaction.Chase;

        /// <summary>후퇴 중 — 타겟을 잡지도 공격하지도 않는다. <c>CharacterBehavior</c> 가 켠다.</summary>
        bool _combatSuppressed;

        /// <summary>마법 최소 사거리 안으로 적이 들어와 거리를 벌리는 중.</summary>
        bool _backOff;

        /// <summary>"사거리에 들어올 때까지 대기" 반응으로 타겟을 쫓지 않고 자리를 지키는 중.</summary>
        bool _holdingGround;

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

        /// <summary>스포너가 정의값으로 파라미터를 덮어쓸 때 사용.</summary>
        public void Configure(float detect, float attack, float speed, float aps,
                              bool advance, UnitKind[] priority, float leash = -1f)
        {
            detectRange = detect;
            attackRange = attack;
            moveSpeedTiles = speed;
            attacksPerSecond = aps;
            advanceToObjective = advance;
            if (priority != null && priority.Length > 0) targetPriority = priority;
            if (leash >= 0f) leashRange = leash;
        }

        /// <summary>무해한 유닛(비선공 중립 몬스터 등)에 써서 적을 인식/공격하지 못하게 한다.</summary>
        public void SetCanAcquireTargets(bool value) => canAcquireTargets = value;

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
        /// 평소 진영 판정을 건너뛰고 이 상대를 사냥하도록 강제한다 (정찰 중 중립 몬스터 조우 등).
        /// 다음 <see cref="AcquireTargetIfNeeded"/> 에서 즉시 <see cref="Target"/> 으로 잡힌다.
        /// 대상이 <see cref="leashRange"/>(귀환 지점 기준) 밖으로 벗어나면 자동으로 포기한다.
        /// </summary>
        public void SetHuntTarget(DamageableUnit target) => _huntOverrideTarget = target;

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
            attackType = type;
            _targetMode = mode;
            _reaction = reaction;
            _tacticalTargeting = true;

            // 공격 유형이 바뀌면 사거리 자체가 달라지므로, 들고 있던 타겟은 버리고 다시 고른다.
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

        public TacticalAttackType AttackType => attackType;

        /// <summary>지금 공격 유형에서 실제로 때릴 수 있는 거리(타일).</summary>
        public float EffectiveAttackRange => attackType switch
        {
            TacticalAttackType.Ranged => rangedRangeTiles,
            TacticalAttackType.Magic  => magicMaxRangeTiles,
            TacticalAttackType.Heal   => healRangeTiles,
            _                         => attackRange,
        };

        /// <summary>
        /// 실제 인식 거리. 사거리가 인식 거리보다 길면(원거리 5 / 마법 6 vs 인식 7 이하) 때릴 수
        /// 있는 적을 못 보는 모순이 생기므로 둘 중 큰 값을 쓴다.
        /// </summary>
        public float EffectiveDetectRange => Mathf.Max(detectRange, EffectiveAttackRange);

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
        /// 등록하면 그 위에 있던 캐릭터가 갇힌다)가 실제로 있었고, 이 상태가 되면
        /// <see cref="TryMoveTo"/> 의 모든 후보가 같은 막힌 칸이라 전부 실패해
        /// 캐릭터가 그 자리에서 완전히 얼어붙는다.
        /// </summary>
        /// <returns>탈출 중이면 true — 이 프레임의 다른 이동은 건너뛴다.</returns>
        bool EscapeIfEmbedded(float dt)
        {
            if (_mapGenerator == null) return false;

            Vector3Int cell = _mapGenerator.WorldToCell(transform.position);
            if (!_mapGenerator.IsCellBlocked(cell)) return false;

            if (!_mapGenerator.TryFindPlaceableNear(cell, 8, null, out Vector3Int free))
                return false;

            // 충돌 판정을 무시하고 빈 칸 쪽으로 곧장 이동한다(이미 막힌 칸이므로
            // 판정을 거치면 어디로도 못 간다).
            Vector3 goal = _mapGenerator.CellCenterWorld(free);
            float speed = CurrentSpeed();
            transform.position = Vector3.MoveTowards(transform.position, goal, speed * dt);
            FaceMovement(goal - transform.position);
            return true;
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

            if (_huntOverrideTarget != null)
            {
                if (!_huntOverrideTarget.IsAlive)
                {
                    _huntOverrideTarget = null;   // 사냥감이 죽었다 — 다음부터 일반 탐색으로 돌아간다
                }
                else if (leashRange > 0f &&
                         Vector2.Distance(_huntOverrideTarget.transform.position, _homePosition) > leashRange)
                {
                    _huntOverrideTarget = null;   // 서식지 밖으로 너무 멀어졌다 — 사냥을 포기한다
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

            _target = found;
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
            bool needMinRange = attackType == TacticalAttackType.Magic && magicMinRangeTiles > 0f;

            if (!useFog && !needLos && !needMinRange) return null;

            return enemy =>
            {
                if (useFog && !_fog.IsVisibleWorld(enemy.transform.position)) return false;

                if (needMinRange)
                {
                    float d = Vector2.Distance(enemy.transform.position, transform.position);
                    // 안전 반경 안에 있으면 아예 후보에서 뺀다. 다만 "지금 붙어 있는 적"까지
                    // 완전히 무시하면 마법사가 자기를 때리는 적을 못 보고 가만히 서 있게 되므로,
                    // 그 상황은 DecideState 의 거리 벌리기(_backOff)가 대신 처리한다.
                    if (d < Mathf.Max(magicSafeRadiusTiles, magicMinRangeTiles)) return false;
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

            if (_target != null && _target.IsAlive)
            {
                float dist = Vector2.Distance(transform.position, _target.transform.position);

                // 마법: 안전 반경 안까지 붙은 적은 때릴 수 없다(유저 규칙: "1의 범위 안에 있는
                // 적은 공격 불가"). 가만히 있으면 영영 못 치므로 거리를 벌린다.
                if (attackType == TacticalAttackType.Magic && dist < magicSafeRadiusTiles)
                {
                    _backOff = true;
                    _state = CombatState.Chase;
                    return;
                }

                if (dist <= EffectiveAttackRange + TargetRadius(_target))
                {
                    _state = CombatState.Attack;
                    return;
                }

                // "적이 사거리 내에 들어올 때까지 대기" — 쫓아가지 않고 자기 자리를 지킨다.
                // 몬스터(advanceToObjective)에는 적용하지 않는다.
                if (_reaction == TacticalAttackReaction.HoldGround && !advanceToObjective)
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
            switch (_state)
            {
                case CombatState.Attack:
                    TryAttack();
                    break;

                case CombatState.Chase:
                    MoveToDestination(ChaseDestination(), dt);
                    break;

                case CombatState.Advance:
                    AdvanceToObjective(dt);
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

            if (_backOff && hasTarget)
            {
                Vector2 away = (Vector2)(transform.position - _target.transform.position);
                if (away.sqrMagnitude < 0.0001f) away = Vector2.right;   // 완전히 겹친 경우
                float back = Mathf.Max(magicMinRangeTiles, magicSafeRadiusTiles + 1f);
                return transform.position + (Vector3)(away.normalized * back);
            }

            return hasTarget && !_holdingGround ? _target.transform.position : _homePosition;
        }

        void TryAttack()
        {
            if (_combatSuppressed) return;
            if (Time.time < _nextAttackTime) return;

            float aps = attacksPerSecond > 0f
                ? attacksPerSecond
                : (_self.Balance != null ? _self.Balance.attacksPerSecond : 1f);
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
        /// </summary>
        void PerformMagicSplash()
        {
            Vector3 center = _target.transform.position;
            UnitRegistry.CollectEnemiesInBox(center, magicAreaTiles * 0.5f, _self.Faction, _splashScratch);

            float safeSqr = magicSafeRadiusTiles * magicSafeRadiusTiles;
            Vector3 myPos = transform.position;

            for (int i = 0; i < _splashScratch.Count; i++)
            {
                DamageableUnit u = _splashScratch[i];
                if (u == null || !u.IsAlive) continue;
                if (((Vector2)(u.transform.position - myPos)).sqrMagnitude < safeSqr) continue;
                u.TakeDamageFrom(_self);
            }
        }

        /// <summary>치유 — 공격력 수치(×퍼센트)만큼 아군을 회복시킨다.</summary>
        void PerformHeal()
        {
            if (_self.Balance == null) return;

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

        float CurrentSpeed() =>
            moveSpeedTiles > 0f
                ? moveSpeedTiles
                : (_self != null && _self.Balance != null ? _self.Balance.moveSpeedTilesPerSecond : 3f);

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
                var nexusScale = target.transform.localScale.x;
                return Mathf.Max(0.5f, nexusScale * 0.5f);
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

            // 마법의 안전 반경 — 이 안의 적은 못 때린다(거리를 벌린다).
            if (attackType == TacticalAttackType.Magic && magicSafeRadiusTiles > 0f)
            {
                Gizmos.color = new Color(0.8f, 0.4f, 1f, 0.7f);
                Gizmos.DrawWireSphere(transform.position, magicSafeRadiusTiles);
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
