using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Map;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 중립 몬스터의 배회. <see cref="UnitCombat"/> 자체에는 "타겟 없을 때 스스로 돌아다니는"
    /// 기능이 없다(귀환 지점에 도착하면 Idle 로 멈추고 스스로는 움직이지 않는다) — 이 컴포넌트가
    /// <see cref="CharacterBehavior"/> 와 같은 패턴(주기적으로 새 귀환 지점을 찍어준다)으로
    /// 그 역할을 대신한다.
    ///
    /// 배회 범위는 "등장 가능 범위"와 같다 — 자기 종의 최소 등장 거리(<see cref="MinDistanceFromNexus"/>)
    /// 부터, 한 단계 위 종이 등장하기 시작하는 거리까지의 고리(annulus) 구간이다
    /// (유저 요청 예시: 역겨운 덩어리 1은 15~100 타일 구간에서 등장 가능 → 그 구간에서 배회).
    /// <see cref="NeutralMonsterSpawner"/> 가 스폰 직후 <see cref="Init"/> 으로 그 구간을 넘겨준다.
    ///
    /// 선공(aggressive) 개체는 <see cref="UnitCombat"/> 이 이미 타겟을 쫓는 동안은 이 컴포넌트가
    /// 손대지 않는다(교전 우선) — 타겟이 없을 때만 새 배회 지점을 고른다.
    /// </summary>
    [RequireComponent(typeof(UnitCombat))]
    public class NeutralMonsterWander : MonoBehaviour
    {
        [Tooltip("다음 배회 지점으로 옮기기까지의 대기 시간 범위(초)")]
        [SerializeField] Vector2 repositionDelay = new Vector2(4f, 10f);

        [Tooltip("목표에 이 거리 안으로 들어오면 도착으로 친다(타일)")]
        [Min(0.2f)] [SerializeField] float arriveDistance = 1f;

        [Tooltip("최상위 종(위 단계가 없어 상한이 무한대)일 때, 최소거리에 더해 얼마나 더 " +
                 "바깥까지 배회 범위로 잡을지(타일). 무한대를 그대로 쓰면 거리 샘플링이 " +
                 "Infinity×0=NaN 이 돼 유닛 좌표가 깨진다 — 반드시 유한한 값으로 바꿔줘야 한다")]
        [Min(1f)] [SerializeField] float unboundedWanderRange = 60f;

        // 벽에 걸리면 몇 번까지 다시 굴려볼지.
        const int Attempts = 8;

        UnitCombat _combat;
        MapGenerator _map;
        DamageableUnit _nexus;
        System.Random _rng;

        float _minRadius;
        float _maxRadius = float.PositiveInfinity;
        Vector3 _destination;
        float _repickTime;
        bool _initialized;

        void Awake()
        {
            _combat = GetComponent<UnitCombat>();

            // 스포너가 AddComponent 직후 같은 프레임에서 곧바로 Init() 을 부른다 —
            // Start() 는 다음 프레임에야 돌기 때문에 그때까지 기다리면 _rng/_map 이
            // null 인 채로 PickDestination() 이 불려 NRE 가 난다. Awake 에서 미리 채워둔다.
            _map = FindAnyObjectByType<MapGenerator>();
            _rng = new System.Random(GetInstanceID());
            _destination = transform.position;
        }

        /// <summary>스포너가 스폰 직후 호출한다 — 이 개체가 등장할 수 있는 거리 구간(체비셰프)을 넘겨준다.</summary>
        public void Init(float minRadius, float maxRadius)
        {
            _minRadius = Mathf.Max(0f, minRadius);

            // 상한이 무한대(최상위 종)면 유한한 값으로 바꿔둔다 — Mathf.Lerp(min, Infinity, t) 는
            // t 가 0이어도 Infinity×0=NaN 이 나와 좌표가 깨진다(실제로 이 버그로 유닛이
            // NaN 위치로 날아가는 문제가 있었다).
            float boundedMax = float.IsPositiveInfinity(maxRadius)
                ? _minRadius + unboundedWanderRange
                : maxRadius;
            _maxRadius = Mathf.Max(_minRadius + 1f, boundedMax);

            _initialized = true;
            PickDestination();
        }

        void Update()
        {
            if (!_initialized) return;

            // 교전 중(선공 개체가 캐릭터를 쫓는 중)이면 손대지 않는다 — 전투가 항상 우선이다.
            if (_combat.Target != null && _combat.Target.IsAlive) return;

            bool arrived = Vector2.Distance(transform.position, _destination) <= arriveDistance;
            if (arrived || _combat.DestinationUnreachable || Time.time >= _repickTime)
                PickDestination();
        }

        void PickDestination()
        {
            Vector3 nexus = NexusPosition();

            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                double angle = _rng.NextDouble() * System.Math.PI * 2.0;
                float radius = Mathf.Lerp(_minRadius, _maxRadius, (float)_rng.NextDouble());

                Vector3 candidate = nexus + new Vector3(
                    Mathf.Cos((float)angle) * radius,
                    Mathf.Sin((float)angle) * radius, 0f);

                if (!IsWalkable(candidate)) continue;
                if (ChebyshevDistance(candidate, nexus) < _minRadius) continue;

                _destination = candidate;
                break;
            }

            _repickTime = Time.time + Mathf.Lerp(repositionDelay.x, repositionDelay.y, (float)_rng.NextDouble());
            _combat.SetHome(_destination);
        }

        bool IsWalkable(Vector3 worldPos) =>
            _map == null || _map.IsCellPlaceable(_map.WorldToCell(worldPos));

        static float ChebyshevDistance(Vector3 a, Vector3 b) =>
            Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        Vector3 NexusPosition()
        {
            if (_nexus == null || !_nexus.IsAlive)
                _nexus = UnitRegistry.FindFirst(Faction.Angel, UnitKind.Nexus);

            return _nexus != null ? _nexus.transform.position : Vector3.zero;
        }
    }
}
