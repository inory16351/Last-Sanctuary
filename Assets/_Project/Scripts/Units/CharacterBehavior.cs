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

        /// <summary>건설 — 플레이어가 찍어둔 건설 예정지로 가서 건물을 짓는 중.
        /// 웨이브 타임(전투·광폭화)에는 잡히지 않는다 — 그때는 싸우는 게 먼저다.</summary>
        Build,

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

        [Header("건설 (플레이어가 찍어둔 예정지)")]
        [Tooltip("이 거리 안까지 들어가면 건설 작업이 진행된다(타일)")]
        [Min(0.5f)] [SerializeField] float buildWorkRange = 1.8f;

        [Tooltip("건설 중 적을 쫓을 수 있는 거리(타일). 짧게 둬야 현장을 지킨다")]
        [Min(1f)] [SerializeField] float buildLeash = 5f;

        [Tooltip("전술 우선 행동이 '건물 건설'이 아닌 캐릭터가 그래도 도와주러 가는 거리(타일). " +
                 "0 이면 건설 전담(우선 행동 = 건물 건설) 캐릭터만 짓는다.\n" +
                 "'건물 건설'을 고른 캐릭터는 이 값과 무관하게 맵 어디의 예정지든 맡는다")]
        [Min(0f)] [SerializeField] float assistBuildRange = 22f;

        [Header("부대 — 함께 이동 (탐색 · 사냥)")]
        [Tooltip("부대 기준원의 목적지에서 이만큼 떨어져 선다(타일). 0 이면 같은 지점으로 몰린다.\n" +
                 "캐릭터마다 고정된 방향으로 어긋나므로 대열이 흔들리지 않는다")]
        [Min(0f)] [SerializeField] float squadFollowSpacing = 2.5f;

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

        /// <summary>정신 이상이 임무 판단을 가로챈 상태. <see cref="CharacterErosion"/> 만 설정한다.</summary>
        MentalOverride _mental = MentalOverride.None;

        public CharacterDuty Duty => _duty;
        public Vector3 Destination => _destination;

        /// <summary>지금 후퇴 중인지 (로스터 표시·디버그용).</summary>
        public bool IsRetreating => _retreating;

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
                    _combat.SetCombatSuppressed(false);
                }
                _duty = CurrentDuty();
                _repickTime = 0f;
                return;
            }

            // 오버라이드 진입 — 지침에 따른 후퇴는 여기서 끝낸다(위 주석 참조).
            _retreating = false;

            if (mode == MentalOverride.Flee)
            {
                // 공포: 적을 인식하지 않고 넥서스 쪽으로 물러난다 = 기존 후퇴와 완전히 같은 동작.
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

            // 웨이브 타임(전투·광폭화)이 시작되면 사냥 중이던 중립 몬스터보다 웨이브 몬스터를
            // 우선한다 — 사냥 타겟을 놓아 UnitCombat 의 일반 진영 타겟팅(가장 가까운 웨이브
            // 몬스터)이 대신 잡게 한다(유저 요청: "웨이브 타임에는 웨이브 몬스터 우선 처리").
            //
            // 전술 지침 "우선 행동 중시"(FinishCurrent)를 고른 캐릭터는 예외다 — 하던 사냥을
            // 마치고 합류하는 것이 그 선택지의 정의이므로 여기서 놓지 않는다.
            if (_combat.IsHunting && IsWaveTimePhase() &&
                _waveReaction == TacticalWaveReaction.DefendNow)
                _combat.ClearHuntTarget();

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
            // 건설이 사냥·정찰보다 앞이다 — 예정지는 플레이어가 직접 찍은 <b>명시적인 지시</b>고
            // 사냥·정찰은 할 일이 없을 때의 기본 행동이다. 웨이브 타임(전투·광폭화)에는
            // 아예 시도하지 않는다 — 그때는 싸우는 게 먼저다(유저 요청: "건설 타이밍은
            // 캐릭터가 알아서 판단").
            if (!IsWaveTimePhase() && TryBuild()) return;

            if (!IsWaveTimePhase() && TryFindHuntPrey(duty, rallyCenter, out DamageableUnit prey))
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
            if (duty != CharacterDuty.Scout)
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
        /// 전술 지침을 반영한 "지금의 기본 임무". 두 축이 섞인다:
        ///
        ///   <b>비전투 우선 행동</b> — 대기시간에 무엇을 할지.
        ///     사냥/탐색은 돌아다녀야 하므로 정찰(Scout), 건설은 예정지가 없을 때만 여기까지 와서
        ///     자리를 지킨다(Guard) — 실제 건설 판정은 <see cref="TryBuild"/> 가 더 앞에서 한다.
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

        /// <summary>
        /// 대기시간에 무엇을 할지. "건물 건설"은 실제 건설 판정이 <see cref="TryBuild"/> 에서
        /// 먼저 일어나므로, 여기까지 왔다는 건 <b>지을 예정지가 없다</b>는 뜻이다 —
        /// 그때는 사냥·탐색 우선 행동과 똑같이 돌아다닌다(유저 요청: "건설 끝나고 대기 시간엔
        /// 놀지 말고 사냥이나 정찰해야 한다"). 예정지가 생기면 다음 프레임 <see cref="TryBuild"/>
        /// 가 바로 그쪽으로 되돌린다.
        /// </summary>
        CharacterDuty PreparationDuty() => CharacterDuty.Scout;

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
            _duty = _mental == MentalOverride.Charge ? CharacterDuty.Scout : CharacterDuty.Guard;
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

        // ------------------------------------------------------------------
        // 건설 — "캐릭터가 알아서 판단해서" 짓는다 (유저 요청)
        // ------------------------------------------------------------------

        /// <summary>전술 우선 행동이 "건물 건설"인가 — 건설 전담 캐릭터다.</summary>
        public bool BuildDedicated => _nonCombat == TacticalNonCombat.Build;

        /// <summary>전담이 아닌 캐릭터가 그래도 도우러 가는 거리(타일). 0 이면 안 간다.</summary>
        public float AssistBuildRange => assistBuildRange;

        /// <summary>
        /// 지금 건설을 맡을 수 있는 상태인가 — <see cref="Buildings.BuildService"/> 가
        /// 건설자를 고를 때 후보 조건으로 쓴다. <see cref="Update"/> 가 <see cref="TryBuild"/>
        /// 까지 내려오는 조건(살아있고, 후퇴 중이 아니고, 웨이브 타임이 아니고, 교전 중이
        /// 아니다)과 같아야 한다 — 어긋나면 일 못 하는 캐릭터에게 자리가 배정된 채 묶인다.
        /// </summary>
        public bool CanTakeBuildOrder =>
            _self != null && _self.IsAlive && !_retreating && !IsWaveTimePhase() &&
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
            // 같은 부대원과 함께 움직인다 — 기준원이 있으면 그가 정한 목적지를 따라간다.
            // 건설하러 간 부대원은 여기 오지 않으므로(Update 의 TryBuild 에서 먼저 빠진다)
            // 자연히 제외되고, 건설이 끝나면 다시 이 경로를 타며 합류한다.
            if (TryFollowSquad(scoutLeash)) return true;

            if (_fog == null || !_fog.IsReady) return false;
            if (!_fog.TryFindUnexploredTarget(transform.position, scoutMinDistance,
                                              scoutSearchRadius, _rng, out Vector3 target))
                return false;

            _destination = target;
            _repickTime = Time.time + scoutTimeout;
            _combat.SetHome(_destination, scoutLeash);
            return true;
        }

        /// <summary>
        /// 부대 기준원의 목적지를 따라간다. 기준원이 없으면(무소속이거나 내가 기준이면) false.
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

            Vector3 target = leader.Destination;

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

            // 전술 지침 — "중립 몬스터 사냥"을 고른 캐릭터는 당연히 사냥한다.
            // 탐색(Explore)은 안개 해제가 목적이므로 사냥하지 않는다.
            // 건설(Build)은 여기 도달했다는 것 자체가 "지금 지을 예정지가 없다"는 뜻이다
            // (있었으면 Update 의 TryBuild 에서 이미 return 했다) — 노는 대신 사냥한다
            // (유저 요청: "건설 끝나고 대기 시간엔 사냥이나 정찰해야 한다").
            if (_nonCombat != TacticalNonCombat.Hunt && _nonCombat != TacticalNonCombat.Build)
                return false;

            // 치유 유형은 애초에 적을 때리지 않는다 — 사냥감을 잡아봐야 쫓아가기만 한다.
            if (_combat.AttackType == TacticalAttackType.Heal) return false;

            // 부대원과 함께 사냥한다 — 기준원이 이미 노리는 사냥감이 있으면 같은 놈을 문다.
            // 각자 가장 가까운 놈을 고르면 부대가 사방으로 흩어진다(유저 요청: "같은 부대는
            // 탐색·사냥 시 함께 이동"). 사거리 제한은 두지 않는다 — 기준원을 따라가는 중이라
            // 어차피 곧 붙는다.
            SquadService squads = SquadService.Instance;
            if (squads != null)
            {
                CharacterBehavior leader = squads.LeaderFor(this);
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
                default:   // Scout — 구역 제한 없음
                    zoneCenter = transform.position;
                    zoneHalfExtent = float.PositiveInfinity;
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
                CharacterDuty.Scout => new Color(0.4f, 1f, 0.6f, 0.9f),
                CharacterDuty.Rally => new Color(1f, 0.95f, 0.5f, 0.9f),
                CharacterDuty.Build => new Color(1f, 0.6f, 0.25f, 0.9f),
                _                   => new Color(0.4f, 0.7f, 1f, 0.9f),
            };
            Gizmos.DrawLine(transform.position, _destination);
            Gizmos.DrawWireCube(_destination, Vector3.one * 0.8f);
        }
    }
}
