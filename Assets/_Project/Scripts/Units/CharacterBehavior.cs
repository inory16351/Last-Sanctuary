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
        Scout,   // 정찰 — 대기시간 동안 미탐사 지역으로 나가 전장을 밝힌다
        Guard,   // 방어 — 웨이브에 대비해 넥서스 주변을 돈다
        Rally,   // 집결 — 웨이브 소환 이후, 넥서스 대신 지정된 집결지 구역을 지킨다
    }

    /// <summary>
    /// 캐릭터의 자율 이동. 전투 자체는 <see cref="UnitCombat"/> 가 맡고,
    /// 이 컴포넌트는 "타겟이 없을 때 어디로 갈지"만 정한다.
    ///
    /// <see cref="UnitCombat"/> 는 타겟이 없으면 귀환 지점으로 걸어가므로,
    /// 귀환 지점을 옮기는 것이 곧 이동 명령이 된다. 덕분에 이동 코드가 두 벌로
    /// 갈라지지 않는다.
    ///
    ///   대기시간(Preparation) → 정찰: 아직 안 밝혀진 칸을 찾아 나간다
    ///   그 외(방어 시점)       → 집결지가 있으면 그 구역을 경계, 없으면 넥서스 주변을 경계
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

        [Header("중립 몬스터 사냥 (정찰 중)")]
        [Tooltip("정찰(Scout) 중 이 거리 안에 중립 몬스터가 있으면 정찰을 멈추고 사냥한다(타일). " +
                 "웨이브 몬스터와 달리 넥서스로 오지 않으므로 캐릭터가 직접 찾아가야 마주친다")]
        [Min(0f)] [SerializeField] float huntDetectRange = 10f;

        [Header("디버그")]
        [SerializeField] bool drawGizmos = true;

        UnitCombat _combat;
        DamageableUnit _self;
        CharacterUnit _character;
        FogOfWarService _fog;
        WaveManager _waveManager;
        MapGenerator _map;
        DamageableUnit _nexus;

        // 순찰 지점을 뽑을 때 벽에 걸리면 몇 번까지 다시 굴려볼지.
        const int GuardSpotAttempts = 8;

        CharacterDuty _duty = CharacterDuty.Guard;
        Vector3 _destination;
        float _repickTime;
        System.Random _rng;

        public CharacterDuty Duty => _duty;
        public Vector3 Destination => _destination;

        void Awake()
        {
            _combat = GetComponent<UnitCombat>();
            _self = GetComponent<DamageableUnit>();
            _character = GetComponent<CharacterUnit>();
            _destination = transform.position;
        }

        void Start()
        {
            _fog = FindAnyObjectByType<FogOfWarService>();
            _waveManager = FindAnyObjectByType<WaveManager>();
            _map = FindAnyObjectByType<MapGenerator>();

            // 캐릭터마다 다른 난수열을 써야 정찰 목표가 겹치지 않는다.
            _rng = new System.Random(GetInstanceID());

            _duty = CurrentDuty();
            PickDestination();
        }

        void Update()
        {
            if (_self == null || !_self.IsAlive) return;

            // 교전 중에는 UnitCombat 에 맡기고 목적지를 건드리지 않는다
            // (사냥 중인 중립 몬스터도 이 시점엔 이미 Target 으로 잡혀 있다).
            if (_combat.Target != null && _combat.Target.IsAlive) return;

            CharacterDuty baseline = CurrentDuty();   // Scout(대기시간) 또는 Guard(그 외)

            // 정찰 중에만 먼저 조우한 중립 몬스터를 사냥하러 간다 — 웨이브 몬스터는
            // 넥서스로 전진해오지만 중립 몬스터는 서식지에 머물러 있으므로, 캐릭터가
            // 직접 찾아가야만 마주친다(기획 요청: "탐색 중 조우 시 사냥, 에너지 획득").
            if (baseline == CharacterDuty.Scout && TryFindHuntPrey(out DamageableUnit prey))
            {
                _combat.SetHuntTarget(prey);
                return;
            }

            // 집결지는 "방어" 를 대신한다 — 정찰(대기시간) 중에는 반영하지 않는다.
            // baseline 이 Guard 로 바뀌는 시점이 곧 웨이브 소환 직후이므로(클래스 doc 참조),
            // 별도 이벤트 구독 없이 이 검사만으로 "소환 직후부터 반영" 이 정확히 성립한다.
            Vector3 rallyCenter = default;
            bool hasRally = baseline == CharacterDuty.Guard &&
                            UI.RallyPointService.TryGetRallyPoint(_character, out rallyCenter);
            CharacterDuty duty = hasRally ? CharacterDuty.Rally : baseline;

            if (duty != _duty)
            {
                _duty = duty;
                if (duty == CharacterDuty.Rally) PickRallySpot(rallyCenter);
                else PickDestination();
                return;
            }

            bool arrived = Vector2.Distance(transform.position, _destination) <= arriveDistance;

            // 길이 막혔거나(DestinationUnreachable) 도착했거나 재추첨 시간이 됐으면 다시 고른다.
            // 예전엔 집결지에 "도착"하면 그 뒤로 아무 재추첨이 없어 제자리에 멈춰 서 있었다
            // (UnitCombat 은 목표에 닿으면 Idle 로 멈추고 스스로 돌아다니지 않는다) — 방어와
            // 똑같이 구역 안에서 계속 순찰 지점을 다시 고르게 해서 고쳤다.
            if (arrived || _combat.DestinationUnreachable || Time.time >= _repickTime)
            {
                if (duty == CharacterDuty.Rally) PickRallySpot(rallyCenter);
                else PickDestination();
            }
        }

        // ------------------------------------------------------------------

        CharacterDuty CurrentDuty() =>
            _waveManager != null && _waveManager.Phase == WavePhase.Preparation
                ? CharacterDuty.Scout
                : CharacterDuty.Guard;

        void PickDestination()
        {
            bool picked = _duty == CharacterDuty.Scout ? PickScoutSpot() : PickGuardSpot();

            // 정찰할 곳이 없으면(맵을 다 밝혔거나 안개가 꺼져 있으면) 방어로 넘어간다.
            if (!picked && _duty == CharacterDuty.Scout)
            {
                _duty = CharacterDuty.Guard;
                PickGuardSpot();
            }
        }

        bool PickScoutSpot()
        {
            if (_fog == null || !_fog.IsReady) return false;
            if (!_fog.TryFindUnexploredTarget(transform.position, scoutMinDistance,
                                              scoutSearchRadius, _rng, out Vector3 target))
                return false;

            _destination = target;
            _repickTime = Time.time + scoutTimeout;
            _combat.SetHome(_destination, scoutLeash);
            return true;
        }

        bool PickGuardSpot() => PickSpotAround(NexusPosition(), guardRadius, guardLeash, square: false);

        /// <summary>
        /// 집결지 구역 안에서 순찰 지점을 고른다. 방어(<see cref="PickGuardSpot"/>)와 로직은
        /// 같고 표본 추출 방식만 다르다 — 방어는 넥서스 중심의 원, 집결은 "n×n 구역"이라는
        /// 요청을 그대로 반영해 정사각 영역 안에서 균등하게 뽑는다.
        /// </summary>
        bool PickRallySpot(Vector3 center) =>
            PickSpotAround(center, RallyAreaSize() * 0.5f, rallyLeash, square: true);

        /// <summary>
        /// 집결지 구역 크기 — <see cref="UI.RallyPointService.RallyAreaSize"/> 를 그대로 읽는다.
        /// 미니맵·월드 오버레이에 보이는 범위 표시와 실제 순찰 범위가 항상 같아야 하므로,
        /// 서비스가 정본이고 여기서는 값을 복제해두지 않는다. 서비스가 없을 때만 폴백을 쓴다.
        /// </summary>
        float RallyAreaSize() =>
            UI.RallyPointService.Instance != null
                ? UI.RallyPointService.Instance.RallyAreaSize
                : rallyAreaSizeFallback;

        /// <summary>
        /// 지정 중심 주변에서 순찰 지점을 하나 고른다. 벽·구조물에 걸린 칸은 버리고 다시
        /// 굴린다 — 검사 없이 그대로 쓰면 순찰 지점이 벽 안에 박혀 캐릭터가 도달할 수 없는
        /// 곳을 향해 벽에 붙어 멈춘 채 다음 재추첨까지 기다리게 된다.
        ///
        /// 도착 후에도 이 메서드가 주기적으로 다시 불려야 한다 — <see cref="UnitCombat"/> 은
        /// 목적지에 닿으면 Idle 로 멈추고 스스로는 돌아다니지 않으므로, 여기서 새 지점을
        /// 계속 골라줘야 "경계 순찰"처럼 보인다(가만히 서 있는 것과의 차이).
        /// </summary>
        bool PickSpotAround(Vector3 center, float halfExtent, float extraLeash, bool square)
        {
            for (int attempt = 0; attempt < GuardSpotAttempts; attempt++)
            {
                Vector3 candidate;
                if (square)
                {
                    // "n×n 구역" 요청을 그대로 반영 — 정사각 안에서 균등하게 뽑는다.
                    float dx = Mathf.Lerp(-halfExtent, halfExtent, (float)_rng.NextDouble());
                    float dy = Mathf.Lerp(-halfExtent, halfExtent, (float)_rng.NextDouble());
                    candidate = center + new Vector3(dx, dy, 0f);
                }
                else
                {
                    float minR = Mathf.Min(2f, halfExtent * 0.5f);
                    double angle = _rng.NextDouble() * System.Math.PI * 2.0;
                    float radius = Mathf.Lerp(minR, halfExtent, (float)_rng.NextDouble());

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

        /// <summary>주변에서 사냥할 만한(살아있는) 중립 몬스터를 찾는다. 가장 가까운 것을 고른다.</summary>
        bool TryFindHuntPrey(out DamageableUnit prey)
        {
            prey = null;
            if (huntDetectRange <= 0f) return false;

            float bestSqr = huntDetectRange * huntDetectRange;
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive || u.Faction != Faction.Neutral) continue;

                float sqr = (u.transform.position - transform.position).sqrMagnitude;
                if (sqr > bestSqr) continue;

                bestSqr = sqr;
                prey = u;
            }
            return prey != null;
        }

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
                CharacterDuty.Scout => new Color(0.4f, 1f, 0.6f, 0.9f),
                CharacterDuty.Rally => new Color(1f, 0.95f, 0.5f, 0.9f),
                _                   => new Color(0.4f, 0.7f, 1f, 0.9f),
            };
            Gizmos.DrawLine(transform.position, _destination);
            Gizmos.DrawWireCube(_destination, Vector3.one * 0.8f);
        }
    }
}
