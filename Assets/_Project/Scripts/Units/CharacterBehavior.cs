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

        /// <summary>후퇴 — 전술 지침의 "후퇴 판단 기준" 이하로 체력이 떨어져 넥서스로 물러난 상태.
        /// 다른 모든 임무보다 우선하며, 회복될 때까지 싸우지 않는다.</summary>
        Retreat,
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

        [Header("중립 몬스터 사냥 (대기시간 · 진군 중)")]
        [Tooltip("대기시간이나 진군 중 이 거리 안에 중립 몬스터가 있으면 하던 일을 멈추고 사냥한다(타일). " +
                 "웨이브 몬스터와 달리 넥서스로 오지 않으므로 캐릭터가 직접 찾아가야 마주친다. " +
                 "웨이브 타임(전투·광폭화)에는 웨이브 몬스터가 우선이라 사냥하지 않는다")]
        [Min(0f)] [SerializeField] float huntDetectRange = 10f;

        [Header("후퇴 (전술 지침의 '후퇴 판단 기준')")]
        [Tooltip("후퇴 기준 + 이 여유(%)만큼 회복되면 다시 전투에 복귀한다. 여유가 0이면 " +
                 "기준선 근처에서 후퇴/복귀를 무한히 반복하며 덜덜 떤다")]
        [Range(0, 50)] [SerializeField] int retreatRecoverMargin = 15;

        [Tooltip("후퇴 시 물러나 대기할 지점 — 넥서스로부터의 반경(타일)")]
        [Min(0.5f)] [SerializeField] float retreatRadius = 3f;

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

        // ── 전술 지침 (CharacterTactics 가 밀어 넣는다. 여기선 직렬화하지 않는다) ────
        TacticalPosition _position = TacticalPosition.Mid;
        TacticalNonCombat _nonCombat = TacticalNonCombat.Hunt;
        TacticalWaveReaction _waveReaction = TacticalWaveReaction.DefendNow;
        int _retreatHpPercent;
        bool _retreating;

        public CharacterDuty Duty => _duty;
        public Vector3 Destination => _destination;

        /// <summary>지금 후퇴 중인지 (로스터 표시·디버그용).</summary>
        public bool IsRetreating => _retreating;

        /// <summary>
        /// 전술 지침을 반영한다. <see cref="CharacterTactics"/> 만 호출한다 —
        /// 지침의 정본은 그쪽이고 여기는 사본이다.
        /// </summary>
        public void ApplyTactics(TacticalPosition position, TacticalNonCombat nonCombat,
                                 TacticalWaveReaction waveReaction, int retreatHpPercent)
        {
            bool positionChanged = _position != position;

            _position = position;
            _nonCombat = nonCombat;
            _waveReaction = waveReaction;
            _retreatHpPercent = Mathf.Clamp(retreatHpPercent, 0, 100);

            // 포지션이 바뀌면 지금 서 있는 자리가 더 이상 맞지 않으므로 즉시 다시 고른다.
            // (다음 재추첨까지 최대 6초를 기다리면 UI 를 눌러도 아무 반응이 없어 보인다)
            if (positionChanged) _repickTime = 0f;
        }

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

            // 후퇴 판단이 가장 먼저다 — 다른 어떤 임무보다 우선한다.
            UpdateRetreatState();
            if (_retreating) { TickRetreat(); return; }

            // 웨이브 타임(전투·광폭화)이 시작되면 사냥 중이던 중립 몬스터보다 웨이브 몬스터를
            // 우선한다 — 사냥 타겟을 놓아 UnitCombat 의 일반 진영 타겟팅(가장 가까운 웨이브
            // 몬스터)이 대신 잡게 한다(유저 요청: "웨이브 타임에는 웨이브 몬스터 우선 처리").
            //
            // 전술 지침 "우선 행동 중시"(FinishCurrent)를 고른 캐릭터는 예외다 — 하던 사냥을
            // 마치고 합류하는 것이 그 선택지의 정의이므로 여기서 놓지 않는다.
            if (_combat.IsHunting && IsWaveTimePhase() &&
                _waveReaction == TacticalWaveReaction.DefendNow)
                _combat.ClearHuntTarget();

            // 교전 중에는 UnitCombat 에 맡기고 목적지를 건드리지 않는다
            // (사냥 중인 중립 몬스터도 이 시점엔 이미 Target 으로 잡혀 있다).
            if (_combat.Target != null && _combat.Target.IsAlive) return;

            CharacterDuty baseline = CurrentDuty();   // Scout(대기시간) 또는 Guard(그 외)

            // 집결지는 "방어" 를 대신한다 — 정찰(대기시간) 중에는 반영하지 않는다.
            // baseline 이 Guard 로 바뀌는 시점이 곧 웨이브 소환 직후이므로(클래스 doc 참조),
            // 별도 이벤트 구독 없이 이 검사만으로 "소환 직후부터 반영" 이 정확히 성립한다.
            Vector3 rallyCenter = default;
            bool hasRally = baseline == CharacterDuty.Guard &&
                            UI.RallyPointService.TryGetRallyPoint(_character, out rallyCenter);
            CharacterDuty duty = hasRally ? CharacterDuty.Rally : baseline;

            // 대기시간·진군 중에는 먼저 조우한 중립 몬스터를 사냥하러 간다 — 웨이브 몬스터는
            // 넥서스로 전진해오지만 중립 몬스터는 서식지에 머물러 있으므로, 캐릭터가
            // 직접 찾아가야만 마주친다(기획 요청: "탐색 중 조우 시 사냥, 에너지 획득"). 다만
            // 방어·집결 중(=진군)에는 지금 모여야 할 구역(집결지가 있으면 그 구역, 없으면 넥서스
            // 주변 방어 반경) 밖까지 쫓아가면 대열이 흐트러진다는 피드백으로, 그 구역 안에
            // 있는 사냥감만 본다 — 정찰 중에는 원래대로 구역 제한 없이 캐릭터 주변만 본다.
            // 웨이브 타임에는 위에서 이미 걸러지므로 여기서는 phase 만 보면 된다.
            if (!IsWaveTimePhase() && TryFindHuntPrey(duty, rallyCenter, out DamageableUnit prey))
            {
                _combat.SetHuntTarget(prey);
                return;
            }

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

        /// <summary>
        /// 전술 지침을 반영한 "지금의 기본 임무". 두 축이 섞인다:
        ///
        ///   <b>비전투 우선 행동</b> — 대기시간에 무엇을 할지.
        ///     사냥/탐색은 돌아다녀야 하므로 정찰(Scout), 건설은 자리를 지켜야 하므로 방어(Guard).
        ///     <i>건설 시스템이 아직 없어서</i> "건물 건설"은 실질적으로 "대기"로 동작한다.
        ///
        ///   <b>웨이브 반응</b> — 웨이브가 시작될 때 하던 일을 마칠지.
        ///     "즉시 방어"는 소환되는 순간 바로 Guard 로 넘어가고(기존 동작),
        ///     "우선 행동 중시"는 진군(Marching) 구간까지는 정찰을 유지했다가
        ///     목적지에 닿으면 합류한다. 전투(Battle)가 시작되면 어느 쪽이든 합류한다 —
        ///     그때까지 안 돌아오면 넥서스가 비어버린다.
        /// </summary>
        CharacterDuty CurrentDuty()
        {
            if (_waveManager == null) return CharacterDuty.Guard;

            if (_waveManager.Phase == WavePhase.Preparation) return PreparationDuty();

            if (_waveReaction == TacticalWaveReaction.FinishCurrent &&
                _waveManager.Phase == WavePhase.Marching &&
                _duty == CharacterDuty.Scout &&
                Vector2.Distance(transform.position, _destination) > arriveDistance)
                return CharacterDuty.Scout;

            return CharacterDuty.Guard;
        }

        /// <summary>대기시간에 무엇을 할지 — 비전투 우선 행동에 따라 갈린다.</summary>
        CharacterDuty PreparationDuty() =>
            _nonCombat == TacticalNonCombat.Build
                ? CharacterDuty.Guard      // 건설 미구현 → 자리를 지키며 대기
                : CharacterDuty.Scout;     // 사냥·탐색 둘 다 돌아다녀야 성립한다

        // ------------------------------------------------------------------
        // 후퇴 — 전술 지침의 "후퇴 판단 기준"
        // ------------------------------------------------------------------

        /// <summary>
        /// 체력이 기준 이하면 후퇴 상태로, 기준 + 여유 이상으로 회복되면 복귀로 전환한다.
        /// 여유(<see cref="retreatRecoverMargin"/>)를 두는 이유는 기준선 바로 위아래에서
        /// 후퇴/복귀가 매 프레임 뒤집히는 것을 막기 위함이다(히스테리시스).
        /// </summary>
        void UpdateRetreatState()
        {
            if (_retreatHpPercent <= 0) { SetRetreating(false); return; }

            float percent = _self.HpRatio * 100f;

            if (!_retreating && percent <= _retreatHpPercent) SetRetreating(true);
            else if (_retreating && percent >= Mathf.Min(100, _retreatHpPercent + retreatRecoverMargin))
                SetRetreating(false);
        }

        void SetRetreating(bool value)
        {
            if (_retreating == value) return;
            _retreating = value;

            // 후퇴 중에는 적을 아예 인식하지 않는다 — 안 그러면 물러나는 길에 마주친 적을
            // 다시 쫓아가느라 영영 못 빠져나온다.
            _combat.SetCombatSuppressed(value);

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

        /// <summary>후퇴 중 유지 — 넥서스 근처에 도착했으면 그 자리에 머문다.</summary>
        void TickRetreat()
        {
            _duty = CharacterDuty.Retreat;

            bool arrived = Vector2.Distance(transform.position, _destination) <= arriveDistance;
            if (arrived || _combat.DestinationUnreachable || Time.time >= _repickTime)
                PickRetreatSpot();
        }

        void PickRetreatSpot()
        {
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

        /// <summary>웨이브 몬스터가 우선인 구간(전투·광폭화) — 이 동안은 중립 몬스터를 사냥하지 않는다.</summary>
        bool IsWaveTimePhase() =>
            _waveManager != null &&
            (_waveManager.Phase == WavePhase.Battle || _waveManager.Phase == WavePhase.Enrage);

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
            // 전방/중위/후방은 "넥서스에서 얼마나 먼가"로 정의된다(유저 규칙).
            // 정사각 구역(집결지)에서는 넥서스 → 집결지 중심 방향이 그 축이 되고,
            // 원형 구역(넥서스 방어)에서는 반지름 자체가 곧 넥서스로부터의 거리라 축이 필요 없다.
            Vector3 nexus = NexusPosition();
            Vector2 axis = (Vector2)(center - nexus);
            axis = axis.sqrMagnitude > 0.01f ? axis.normalized : Vector2.up;
            Vector2 perpendicular = new Vector2(-axis.y, axis.x);

            GetPositionBand(out float bandLow, out float bandHigh);

            for (int attempt = 0; attempt < GuardSpotAttempts; attempt++)
            {
                Vector3 candidate;
                if (square)
                {
                    // "n×n 구역" 요청을 그대로 반영하되, 넥서스 축 방향으로는 포지션 구간
                    // 안에서만 뽑는다 — 전방이면 구역의 바깥쪽 1/3, 후방이면 넥서스 쪽 1/3.
                    float along = Mathf.Lerp(bandLow, bandHigh, (float)_rng.NextDouble()) * halfExtent;
                    float side = Mathf.Lerp(-1f, 1f, (float)_rng.NextDouble()) * halfExtent;
                    candidate = center + (Vector3)(axis * along + perpendicular * side);
                }
                else
                {
                    // 원형(넥서스 방어) — 구간 [-1,1] 을 반지름 비율 [0,1] 로 옮긴다.
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
        /// 정찰(Scout) 중에는 원래대로 구역 제한 없이 캐릭터 주변만 본다 — 어차피 안 밝혀진
        /// 지역으로 널리 돌아다니는 임무라 "구역"이라는 개념이 없다.
        /// 방어·집결(Guard/Rally) 중에는 사냥감이 지금 모여야 할 구역(집결지가 있으면 그
        /// 구역, 없으면 넥서스 방어 반경) 안에 있을 때만 쫓는다 — 안 그러면 구역 밖까지
        /// 쫓아가버려 대열이 흐트러진다(유저 피드백).
        /// </summary>
        bool TryFindHuntPrey(CharacterDuty duty, Vector3 rallyCenter, out DamageableUnit prey)
        {
            prey = null;
            if (huntDetectRange <= 0f) return false;

            // 전술 지침 — "중립 몬스터 사냥"을 고른 캐릭터만 사냥한다.
            // 탐색(Explore)은 안개 해제가, 건설(Build)은 자리 지키기가 목적이므로 사냥하지 않는다.
            if (_nonCombat != TacticalNonCombat.Hunt) return false;

            // 치유 유형은 애초에 적을 때리지 않는다 — 사냥감을 잡아봐야 쫓아가기만 한다.
            if (_combat.AttackType == TacticalAttackType.Heal) return false;

            Vector3 zoneCenter;
            float zoneHalfExtent;
            bool zoneSquare;
            switch (duty)
            {
                case CharacterDuty.Rally:
                    zoneCenter = rallyCenter;
                    zoneHalfExtent = RallyAreaSize() * 0.5f;
                    zoneSquare = true;
                    break;
                case CharacterDuty.Guard:
                    zoneCenter = NexusPosition();
                    zoneHalfExtent = guardRadius;
                    zoneSquare = false;
                    break;
                default:   // Scout — 구역 제한 없음
                    zoneCenter = transform.position;
                    zoneHalfExtent = float.PositiveInfinity;
                    zoneSquare = false;
                    break;
            }

            float bestSqr = huntDetectRange * huntDetectRange;
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive || u.Faction != Faction.Neutral) continue;

                float sqr = (u.transform.position - transform.position).sqrMagnitude;
                if (sqr > bestSqr) continue;
                if (!IsInsideZone(u.transform.position, zoneCenter, zoneHalfExtent, zoneSquare)) continue;

                bestSqr = sqr;
                prey = u;
            }
            return prey != null;
        }

        static bool IsInsideZone(Vector3 pos, Vector3 center, float halfExtent, bool square)
        {
            if (float.IsPositiveInfinity(halfExtent)) return true;

            if (square)
            {
                Vector3 d = pos - center;
                return Mathf.Abs(d.x) <= halfExtent && Mathf.Abs(d.y) <= halfExtent;
            }
            return Vector2.Distance(pos, center) <= halfExtent;
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
