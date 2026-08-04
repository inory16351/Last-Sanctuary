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

        [Header("회피")]
        [Tooltip("서로 겹치지 않게 밀어내는 반경(타일). 0 이면 겹침 허용")]
        [Min(0f)] [SerializeField] float separationRadius = 0.55f;
        [Min(0f)] [SerializeField] float separationStrength = 1.4f;

        [Header("디버그")]
        [SerializeField] bool drawGizmos = true;

        DamageableUnit _self;
        FlowFieldService _flowField;
        MapGenerator _mapGenerator;
        FogOfWarService _fog;
        Vector3 _homePosition;
        DamageableUnit _target;
        float _nextAttackTime;
        float _nextRetargetTime;
        CombatState _state = CombatState.Idle;

        // 타겟 재탐색 간격. 매 프레임 전체를 훑지 않도록 분산시킨다.
        const float RetargetInterval = 0.2f;

        public CombatState State => _state;
        public DamageableUnit Target => _target;

        void Awake()
        {
            _self = GetComponent<DamageableUnit>();
            _homePosition = transform.position;
        }

        void Start()
        {
            _flowField = FindAnyObjectByType<FlowFieldService>();
            _mapGenerator = FindAnyObjectByType<MapGenerator>();
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

        /// <summary>
        /// 귀환 지점을 지정한다. 전진하지 않는 유닛(캐릭터·포탑)은 타겟이 없으면
        /// 이 지점으로 걸어가므로, 여기를 옮기는 것이 곧 "이동 명령"이 된다.
        /// </summary>
        public void SetHome(Vector3 worldPosition) => _homePosition = worldPosition;

        /// <summary>귀환 지점과 목줄 길이를 함께 지정한다.</summary>
        public void SetHome(Vector3 worldPosition, float leash)
        {
            _homePosition = worldPosition;
            if (leash >= 0f) leashRange = leash;
        }

        public Vector3 Home => _homePosition;

        /// <summary>귀환 지점에 도착했는지.</summary>
        public bool IsAtHome(float tolerance = 0.5f) =>
            Vector2.Distance(transform.position, _homePosition) <= tolerance;

        // ------------------------------------------------------------------

        void Update()
        {
            if (_self == null || !_self.IsAlive)
            {
                _state = CombatState.Dead;
                return;
            }

            float dt = Time.deltaTime;

            AcquireTargetIfNeeded();
            DecideState();
            Act(dt);
        }

        void AcquireTargetIfNeeded()
        {
            bool targetInvalid = _target == null || !_target.IsAlive;

            // 유효한 타겟이 있어도 주기적으로 다시 훑어 더 우선순위 높은 적을 잡는다.
            if (!targetInvalid && Time.time < _nextRetargetTime) return;
            _nextRetargetTime = Time.time + RetargetInterval;

            System.Func<DamageableUnit, bool> visibilityFilter = null;
            if (respectFogOfWar && _fog != null && _fog.IsReady)
                visibilityFilter = enemy => _fog.IsVisibleWorld(enemy.transform.position);

            DamageableUnit found = UnitRegistry.FindTarget(
                transform.position, _self.Faction, detectRange, targetPriority, visibilityFilter);

            // 목줄 밖의 적은 쫓지 않는다 (전진하지 않는 유닛에만 적용)
            if (found != null && !advanceToObjective && leashRange > 0f)
            {
                float d = Vector2.Distance(found.transform.position, _homePosition);
                if (d > leashRange) found = null;
            }

            _target = found;
        }

        void DecideState()
        {
            if (_target != null && _target.IsAlive)
            {
                float dist = Vector2.Distance(transform.position, _target.transform.position);
                _state = dist <= attackRange + TargetRadius(_target)
                    ? CombatState.Attack
                    : CombatState.Chase;
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
                    Vector3 dest = _target != null && _target.IsAlive
                        ? _target.transform.position
                        : _homePosition;
                    MoveTowards(dest, dt);
                    break;

                case CombatState.Advance:
                    AdvanceToObjective(dt);
                    break;
            }
        }

        // ------------------------------------------------------------------

        void TryAttack()
        {
            if (Time.time < _nextAttackTime) return;

            float aps = attacksPerSecond > 0f
                ? attacksPerSecond
                : (_self.Balance != null ? _self.Balance.attacksPerSecond : 1f);
            if (aps <= 0f) return;

            _nextAttackTime = Time.time + 1f / aps;

            // 공격한 쪽도 전투 상태로 기록해야 재생 대기 시간이 갱신된다.
            _self.MarkCombatAction();
            _target.TakeDamageFrom(_self);
        }

        void AdvanceToObjective(float dt)
        {
            // 플로우 필드가 있으면 벽을 피해 넥서스로 향한다.
            if (_flowField != null && _flowField.TryGetDirection(transform.position, out Vector2 dir))
            {
                Step(dir, dt);
                return;
            }

            // 없으면 넥서스를 직선으로 향한다 (플로우 필드 미구성 시 폴백)
            DamageableUnit nexus = UnitRegistry.FindFirst(_self.Faction.Opposite(), UnitKind.Nexus);
            if (nexus != null) MoveTowards(nexus.transform.position, dt);
        }

        void MoveTowards(Vector3 destination, float dt)
        {
            Vector2 delta = destination - transform.position;
            if (delta.sqrMagnitude < 0.0004f) return;
            Step(delta.normalized, dt);
        }

        void Step(Vector2 direction, float dt)
        {
            float speed = moveSpeedTiles > 0f
                ? moveSpeedTiles
                : (_self.Balance != null ? _self.Balance.moveSpeedTilesPerSecond : 3f);

            Vector2 move = direction + Separation() * separationStrength;
            if (move.sqrMagnitude > 0.0001f) move.Normalize();

            MoveWithCollision((Vector3)(move * speed * dt));
        }

        /// <summary>
        /// 유닛에 Collider2D 가 없어 물리 충돌이 걸리지 않으므로, 벽 타일맵을
        /// 직접 검사해 막힌 칸으로는 못 들어가게 한다(Advance 상태는 플로우 필드가
        /// 이미 벽을 피하지만, Chase·귀환은 직선 이동이라 이 검사가 없으면 벽을 뚫고 지나간다).
        /// 막히면 축 하나씩 미끄러뜨려 벽을 따라 이동하게 한다.
        /// </summary>
        void MoveWithCollision(Vector3 delta)
        {
            if (delta.sqrMagnitude < 1e-8f) return;

            Vector3 pos = transform.position;
            if (TryMoveTo(pos + delta)) return;
            if (delta.x != 0f && TryMoveTo(pos + new Vector3(delta.x, 0f, 0f))) return;
            if (delta.y != 0f) TryMoveTo(pos + new Vector3(0f, delta.y, 0f));
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
            Gizmos.DrawWireSphere(transform.position, detectRange);

            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, attackRange);

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
        }
    }
}
