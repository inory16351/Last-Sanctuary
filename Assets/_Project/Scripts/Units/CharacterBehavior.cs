using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Fog;
using LastSanctuary.Wave;

namespace LastSanctuary.Units
{
    /// <summary>캐릭터가 지금 무엇을 하려는지.</summary>
    public enum CharacterDuty
    {
        Scout,   // 정찰 — 대기시간 동안 미탐사 지역으로 나가 전장을 밝힌다
        Guard,   // 방어 — 웨이브에 대비해 넥서스 주변을 돈다
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
    ///   그 외                 → 방어: 넥서스 반경 안을 돌아다닌다
    ///
    /// 적을 발견하면 UnitCombat 이 알아서 교전하고, 이 컴포넌트는 교전이 끝날
    /// 때까지 목적지를 건드리지 않는다.
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

        [Header("공통")]
        [Tooltip("목표에 이 거리 안으로 들어오면 도착으로 친다(타일)")]
        [Min(0.2f)] [SerializeField] float arriveDistance = 1.2f;

        [Header("디버그")]
        [SerializeField] bool drawGizmos = true;

        UnitCombat _combat;
        DamageableUnit _self;
        FogOfWarService _fog;
        WaveManager _waveManager;
        DamageableUnit _nexus;

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
            _destination = transform.position;
        }

        void Start()
        {
            _fog = FindAnyObjectByType<FogOfWarService>();
            _waveManager = FindAnyObjectByType<WaveManager>();

            // 캐릭터마다 다른 난수열을 써야 정찰 목표가 겹치지 않는다.
            _rng = new System.Random(GetInstanceID());

            _duty = CurrentDuty();
            PickDestination();
        }

        void Update()
        {
            if (_self == null || !_self.IsAlive) return;

            // 교전 중에는 UnitCombat 에 맡기고 목적지를 건드리지 않는다.
            if (_combat.Target != null && _combat.Target.IsAlive) return;

            CharacterDuty duty = CurrentDuty();
            if (duty != _duty)
            {
                _duty = duty;
                PickDestination();
                return;
            }

            bool arrived = Vector2.Distance(transform.position, _destination) <= arriveDistance;
            if (arrived || Time.time >= _repickTime) PickDestination();
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

        bool PickGuardSpot()
        {
            Vector3 center = NexusPosition();

            // 넥서스 주변 원 안의 임의 지점. 넥서스에 딱 붙지 않도록 안쪽 반경을 둔다.
            double angle = _rng.NextDouble() * System.Math.PI * 2.0;
            float minR = Mathf.Min(2f, guardRadius * 0.5f);
            float radius = Mathf.Lerp(minR, guardRadius, (float)_rng.NextDouble());

            _destination = center + new Vector3(
                Mathf.Cos((float)angle) * radius,
                Mathf.Sin((float)angle) * radius,
                0f);

            _repickTime = Time.time + Mathf.Lerp(guardRepositionDelay.x, guardRepositionDelay.y,
                                                 (float)_rng.NextDouble());

            // 목줄은 넥서스 기준이어야 하므로, 순찰 지점까지의 거리를 더해준다.
            _combat.SetHome(_destination, guardLeash + guardRadius);
            return true;
        }

        Vector3 NexusPosition()
        {
            if (_nexus == null || !_nexus.IsAlive)
                _nexus = UnitRegistry.FindFirst(Faction.Angel, UnitKind.Nexus);

            return _nexus != null ? _nexus.transform.position : Vector3.zero;
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos || !Application.isPlaying) return;

            Gizmos.color = _duty == CharacterDuty.Scout
                ? new Color(0.4f, 1f, 0.6f, 0.9f)
                : new Color(0.4f, 0.7f, 1f, 0.9f);
            Gizmos.DrawLine(transform.position, _destination);
            Gizmos.DrawWireCube(_destination, Vector3.one * 0.8f);
        }
    }
}
