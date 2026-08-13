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
    /// 배회 범위는 "등장 가능 범위"와 <b>정확히 같다</b> — 표의 <c>spawn_range_min</c> ~
    /// <c>spawn_range_max</c> 로 정해지는 <b>넥서스 중심의 360도 원형 고리</b>(유클리드 거리)다.
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

        // 벽에 걸리거나 맵 밖이면 몇 번까지 다시 굴려볼지.
        // ⚠ 고리가 맵 경계에 가까운 종은 각도 추첨이 자주 맵 밖으로 나가므로, 8번으로는
        //   매번 실패해 <b>제자리에 굳는다</b>(실패하면 목적지를 안 바꾸고 시각만 미룬다).
        //   스포너(96회)만큼은 아니어도 넉넉히 잡아둔다.
        const int Attempts = 48;

        UnitCombat _combat;
        MapGenerator _map;
        DamageableUnit _nexus;
        System.Random _rng;

        float _minRadius;
        float _maxRadius = float.PositiveInfinity;
        Vector3 _destination;
        float _repickTime;
        bool _initialized;

        void Awake() => EnsureReady();

        /// <summary>
        /// 참조·난수를 준비한다. <b>Awake·Init·Update 세 곳에서 모두 부른다</b>(여러 번 불려도
        /// 안전하다) — 이 컴포넌트는 스포너가 <c>AddComponent</c> 로 붙이고 곧바로
        /// <see cref="Init"/> 을 부르기 때문에, 초기화를 어느 한 콜백에만 두면 그 콜백이 아직
        /// 안 돈 상태에서 <see cref="PickDestination"/> 이 불려 <c>_rng</c> NRE 가 난다.
        ///
        /// 24-6절이 "Start → Awake 이동" 으로 고쳤다고 기록했지만 25-6절에서 재발이 확인됐고,
        /// 28-4절에서도 여전히 매 프레임 NRE 가 쏟아지고 있었다. <b>어느 콜백이 먼저 도는지
        /// 추론하는 대신 세 곳에서 다 부르는 것</b>이 이 함정의 확실한 대책이다 — 런타임에
        /// 동적으로 붙는 컴포넌트에는 이 패턴을 기본으로 쓸 것.
        /// </summary>
        void EnsureReady()
        {
            if (_rng != null) return;

            _combat = GetComponent<UnitCombat>();
            _map = FindAnyObjectByType<MapGenerator>();
            _rng = new System.Random(GetInstanceID());
            _destination = transform.position;
        }

        /// <summary>스포너가 스폰 직후 호출한다 — 이 개체가 등장할 수 있는 거리 구간(유클리드)을 넘겨준다.</summary>
        public void Init(float minRadius, float maxRadius)
        {
            EnsureReady();
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
            EnsureReady();

            // 교전 중(선공 개체가 캐릭터를 쫓는 중)이면 손대지 않는다 — 전투가 항상 우선이다.
            if (_combat.Target != null && _combat.Target.IsAlive) return;

            bool arrived = Vector2.Distance(transform.position, _destination) <= arriveDistance;
            if (arrived || _combat.DestinationUnreachable || Time.time >= _repickTime)
                PickDestination();
        }

        /// <summary>
        /// 다음 배회 지점을 고른다.
        ///
        /// ★ <b>넥서스 중심의 원형 고리(360도) 위에서 뽑는다</b>(2026-08-13 개정) —
        /// <c>NeutralMonsterSpawner.SampleRingCell</c> 과 <b>완전히 같은 방식</b>이라
        /// 배회 범위와 스폰 범위가 정확히 일치한다(유저 지시: "소환 가능한 범위 내에서만 배회").
        ///
        /// 반지름을 <c>√(lerp(min², max²))</c> 로 뽑는 이유도 스포너와 같다 — 그냥 <c>Lerp</c> 로
        /// 뽑으면 넓이가 좁은 <b>안쪽에 몰린다</b>(71-3절의 "중앙으로 모인다" 와 같은 종류의 편향).
        /// </summary>
        void PickDestination()
        {
            Vector3 nexus = NexusPosition();

            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                float t = (float)_rng.NextDouble();
                float r = Mathf.Sqrt(Mathf.Lerp(_minRadius * _minRadius, _maxRadius * _maxRadius, t));
                float angle = (float)(_rng.NextDouble() * System.Math.PI * 2.0);

                Vector3 candidate = nexus + new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
                if (!IsWalkable(candidate)) continue;

                // 벽을 피해 고르는 것이 아니라 "고른 자리가 벽인지"만 보므로, 거리 재검사는
                // 사실 통과가 보장된다 — 그래도 남겨둔다(넥서스가 (0,0)이 아닌 경우 대비).
                float d = Vector2.Distance(candidate, nexus);
                if (d < _minRadius || d > _maxRadius) continue;

                _destination = candidate;
                break;
            }

            _repickTime = Time.time + Mathf.Lerp(repositionDelay.x, repositionDelay.y, (float)_rng.NextDouble());
            _combat.SetHome(_destination);
        }

        bool IsWalkable(Vector3 worldPos) =>
            _map == null || _map.IsCellPlaceable(_map.WorldToCell(worldPos));

        Vector3 NexusPosition()
        {
            if (_nexus == null || !_nexus.IsAlive)
                _nexus = UnitRegistry.FindFirst(Faction.Angel, UnitKind.Nexus);

            return _nexus != null ? _nexus.transform.position : Vector3.zero;
        }
    }
}
