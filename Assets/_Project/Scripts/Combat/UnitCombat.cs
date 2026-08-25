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
        Advance,   // 이동 — 목표(성역)를 향해 전진
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

        [Tooltip("타겟이 없을 때 목표(성역)를 향해 전진한다. 몬스터는 켜고 캐릭터는 끈다")]
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
        [Tooltip("서로 겹치지 않게 밀어내는 반경(타일). 0 이면 겹침 허용.\n\n" +
                 "★ 이 값은 <b>최소치</b>다 — 실제 반경은 이 값과 <b>두 유닛의 몸집 합</b> 중 " +
                 "큰 쪽이다(표의 collider_width/height_tiles → BodyRadiusTiles). 몸집이 큰 종은 " +
                 "이 값만으로는 그림이 거의 완전히 겹친다(2.6x1.9 짜리 고르도네가 0.55타일에서 " +
                 "멈추던 버그 — Separation 주석 참조)")]
        [Min(0f)] [SerializeField] float separationRadius = 0.55f;
        [Min(0f)] [SerializeField] float separationStrength = 1.4f;

        [Tooltip("밀림이 이동 방향을 이길 수 없게 하는 상한. 1 이 '가려던 방향과 같은 세기'다. " +
                 "⚠️ 상한이 없으면 주변 유닛 수만큼 힘이 더해져(5마리면 7배) 가려던 방향이 " +
                 "묻히고 몬스터 무리에 그대로 휩쓸려 끌려간다")]
        [Min(0.05f)] [SerializeField] float separationMaxInfluence = 0.7f;

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

        /// <summary>
        /// 정신 이상(「혼란」)이 사냥 타겟을 <b>잠근</b> 상태.
        /// 자세한 이유는 <see cref="SetForcedHuntTarget"/> 참조.
        /// </summary>
        bool _huntTargetForced;

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
        /// ★ <b>후퇴 사격(카이팅)</b> — 성역 쪽으로 물러나면서 사거리 안의 적을 쏜다.
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

        // 두 유닛이 이 거리 안이면 "정확히 겹쳤다" 로 보고 밀어낼 방향을 정해준다
        // (거리에서 방향을 뽑을 수 없는 구간 — Separation 주석 참조).
        const float CoincidentEpsilon = 0.01f;

        // 몸집 반경이 이 값을 넘는 유닛만 벽에서 몸만큼 떨어지려 한다(WallClearance).
        // 한 칸(0.5) 안에 들어가는 유닛은 중심점 판정만으로 이미 벽 밖에 있다 —
        // 캐릭터·잡몹의 이동을 한 프레임도 바꾸지 않기 위한 문턱이다.
        const float WallClearanceMinBody = 0.6f;

        // 제자리에서 겹침을 풀 때, 밀림이 이보다 약하면 아무것도 하지 않는다.
        // 없으면 사거리 밖 이웃의 미세한 힘에 반응해 제자리에서 떤다.
        const float UnstackDeadzone = 0.05f;

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

        /// <summary>
        /// ★★ <b>정신 이상이 잡은 사냥감</b> — 전술 지침이 지우지 못하게 <b>잠근다</b>
        /// (2026-08-17 신설, 유저 지시: <i>"정신 이상 상태에 걸린 캐릭터의 전술을 변경했을 때
        /// 정신 이상이 풀리거나 효과가 사라지면 안 된다"</i>).
        ///
        /// <b>왜 필요했나 — 실제로 지워지고 있었다.</b> 「혼란」은 아군을 때리는 상태를
        /// <see cref="SetHuntTarget"/> 하나로 구현한다(진영 판정을 건너뛰는 유일한 훅이라서).
        /// 그런데 그 값을 <b>전술 변경 경로 두 곳이 무조건 지우고 있었다</b>:
        /// <code>
        ///   CharacterBehavior.ApplyTactics   : 탐험 유형이 '사냥' 이 아니면 ClearHuntTarget()
        ///   UnitCombat.SetNeutralHostilitySuppressed : '탐색' 으로 바꾸면 _huntOverrideTarget = null
        /// </code>
        /// 즉 <b>혼란에 걸린 캐릭터의 전술 지침을 건드리면 그 순간 아군 공격이 멎었다.</b>
        /// (<c>TickConfusion</c> 이 0.75초 뒤 다시 잡아주긴 하지만, 그 사이가 눈에 보이고
        /// 무엇보다 "지침을 바꾸면 정신 이상이 풀린다"는 잘못된 인상을 준다.)
        ///
        /// 잠금 방식은 <see cref="SetForcedAttackType"/> 과 <b>일부러 똑같이</b> 맞췄다 —
        /// 같은 성질의 문제(정신 이상이 소유한 값을 지침이 덮어씀)를 두 가지 방식으로 풀면
        /// 다음 사람이 한쪽만 보고 나머지를 놓친다.
        /// </summary>
        public void SetForcedHuntTarget(DamageableUnit target)
        {
            _huntTargetForced = true;

            // ⚠ SetHuntTarget 을 거치지 않는다 — 그쪽의 '중립 억제' 검사에 걸릴 수 있는데,
            //   정신 이상은 그 억제(전술 지침의 한 항목)보다 우선이라는 것이 이 함수의 뜻이다.
            if (!ReferenceEquals(_huntOverrideTarget, target)) _huntOrigin = transform.position;
            _huntOverrideTarget = target;
        }

        /// <summary>
        /// 잠금을 풀고 사냥감도 놓는다. <c>CharacterErosion.ClearActive</c> 만 부른다 —
        /// 정신 이상 해제 경로가 그 한 곳으로 모여 있기 때문이다(만료·사망·중첩 전부).
        /// </summary>
        public void ClearForcedHuntTarget()
        {
            if (!_huntTargetForced) return;
            _huntTargetForced = false;
            ClearHuntTarget();
        }

        /// <summary>사냥을 포기한다. 이미 그 상대를 쫓고 있었다면 타겟도 함께 비운다.</summary>
        public void ClearHuntTarget()
        {
            // ★ 정신 이상이 잠가둔 사냥감은 지침·자동 판단이 놓을 수 없다
            //   (<see cref="SetForcedHuntTarget"/> 참조).
            if (_huntTargetForced) return;

            if (_huntOverrideTarget != null && _target == _huntOverrideTarget) _target = null;
            _huntOverrideTarget = null;
        }

        /// <summary>지금 사냥 타겟을 쫓는 중인지 (일반 진영 전투와 구분해서 보고 싶을 때 사용).</summary>
        public bool IsHunting => _huntOverrideTarget != null && _huntOverrideTarget.IsAlive;

        /// <summary>
        /// 지금 쫓고 있는 사냥감. 살아 있지 않으면 <b>null</b> — <see cref="IsHunting"/> 과
        /// 같은 판정을 쓴다(둘이 어긋나면 «사냥 중인데 사냥감이 없다» 가 생긴다).
        ///
        /// ★ 왜 필요한가 — «누가 이 사냥감을 물렸는지» 를 부르는 쪽이 구분해야 하는 일이 있다.
        ///   첫 사용자는 <see cref="AruGolem.Follow"/> 다: 주인이 표적을 놓았을 때 <b>주인에게서
        ///   물려받은 표적만</b> 놓고 골렘이 스스로 문 중립 사냥감은 남겨야 한다.
        /// </summary>
        public DamageableUnit HuntTarget => IsHunting ? _huntOverrideTarget : null;

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
            // ⚠ 사냥감은 ClearHuntTarget 을 거쳐 놓는다 — 정신 이상이 잠근 타겟을
            //   직접 null 로 밀면 그 잠금이 무의미해진다(SetForcedHuntTarget 참조).
            if (value) { _target = null; ClearHuntTarget(); _backOff = false; }
        }

        /// <summary>
        /// ★ <b>후퇴 사격(카이팅)</b>을 켜고 끈다. <c>CharacterBehavior</c> 가
        /// <b>체력 기준 후퇴</b>(본인 또는 전방 아군)에서만 켠다 — 유저 지시 2026-08-11:
        /// "후퇴하면서 공격하는 건 전방의 캐릭터나 본인 스스로의 체력이 후퇴 기준에 다다라서
        /// 성역 방향으로 후퇴할 때 발동되는 걸로".
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
            if (value) ClearHuntTarget();   // 물러나면서 사냥을 이어갈 수는 없다 (잠금은 존중한다)
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

            // ★★ 여기가 「혼란」을 지우던 자리였다 — 전술 지침의 탐험 유형을 '탐색' 으로
            //   바꾸는 것만으로 정신 이상이 잡아둔 아군 타겟이 날아갔다(2026-08-17 수정).
            ClearHuntTarget();
            if (_target != null && _target.Faction == Faction.Neutral)
            {
                _target = null;
                _engaged = false;
                _engagedWith = null;
            }
        }

        /// <summary>지금 중립 몬스터를 건드리지 않는 상태인지 (탐험 유형 '탐색').</summary>
        public bool IsNeutralHostilitySuppressed => _neutralHostilitySuppressed;

        // ------------------------------------------------------------------
        // 「허약」 · 「구속」 — 말파스 구속탄이 거는 상태 (2026-08-18)
        //
        // ★ <b>왜 여기(컴포넌트)에 두고 서비스 장부에 안 두는가</b> — 부식(방어력 감소)은
        //   <see cref="PassiveSkillService"/> 가 장부로 관리한다. 그건 <b>되돌려야 하는</b>
        //   보정이라(걸 때 빼고 풀 때 더한다) 새면 영구히 깎이기 때문에 한 곳에 모은 것이다.
        //   이 둘은 <b>시각(時刻) 하나</b>로 표현되는 상태라 되돌릴 것이 없다 — 만료 시각이
        //   지나면 저절로 꺼지고, 유닛이 죽으면 컴포넌트째 사라진다. 장부에 넣으면
        //   "죽은 유닛 정리" 코드만 늘어난다.
        // ------------------------------------------------------------------

        /// <summary>「허약」이 끝나는 시각. 0 이면 안 걸렸다.</summary>
        float _weakenUntil;

        /// <summary>「허약」 중 공격속도에 곱할 값(0~1).</summary>
        float _weakenAttackSpeedMul = 1f;

        /// <summary>「구속」이 끝나는 시각. 0 이면 안 걸렸다.</summary>
        float _boundUntil;

        /// <summary>
        /// 「구속」의 화면 표시 이름. 보스마다 다른 이름(예: 기절)을 쓸 수 있어서
        /// (<see cref="BossSkillSO.StatusName"/>, 2026-08-19) 상수가 아니라 인스턴스 값이다.
        /// 기본값 "구속" — 이름을 안 넘기는 옛 호출부·비어 있는 스킬은 그대로 이걸 쓴다.
        /// </summary>
        string _boundLabel = "구속";

        /// <summary>
        /// <b>「허약」</b> — 공격속도를 <paramref name="reducePercent"/> % 만큼 깎는다.
        /// 같은 것이 또 걸리면 <b>지속시간만 새로 잡는다</b>(중첩하지 않는다) — 감소율을
        /// 곱해 쌓으면 몇 발만 맞아도 공격이 사실상 멈춘다.
        /// </summary>
        public void ApplyWeaken(float reducePercent, float seconds)
        {
            if (seconds <= 0f) return;
            _weakenAttackSpeedMul = Mathf.Clamp(1f - reducePercent / 100f, 0.05f, 1f);
            _weakenUntil = Time.time + seconds;
        }

        /// <summary>지금 「허약」에 걸려 있는지 — 구속탄이 "또 맞았는지"를 이걸로 판정한다.</summary>
        public bool IsWeakened => Time.time < _weakenUntil;

        // ------------------------------------------------------------------
        // ★ 「고통의 기쁨」(시그리드 80017) — 공격속도 상승 (2026-08-20)
        //
        // 정의문: <i>"'가학증'이 발동할 때마다 시그리드의 공격속도가 {v1}% 만큼 {v2}초 동안
        // 증가합니다. 해당 효과는 중첩될 수 없으며 지속시간만을 초기화 합니다."</i>
        //
        // ★ <b>「허약」과 완전히 같은 짜임</b>이다 — 능력치를 건드리지 않고 <b>쓰는 자리에서
        //   곱한다</b>. 그래서 로스터·성장 창의 표시 공속은 그대로다(그쪽 규칙을 그대로 따랐다).
        //   중첩 금지 + 지속시간만 초기화도 「허약」과 같아서 코드 모양이 같다.
        // ------------------------------------------------------------------

        /// <summary>「고통의 기쁨」이 끝나는 시각. 0 이면 안 걸렸다.</summary>
        float _hasteUntil;

        /// <summary>「고통의 기쁨」 중 공격속도에 곱할 값(1 이상).</summary>
        float _hasteAttackSpeedMul = 1f;

        /// <summary>
        /// <b>「고통의 기쁨」</b> — 공격속도를 <paramref name="increasePercent"/> % 만큼 올린다.
        /// 또 발동하면 <b>지속시간만 새로 잡는다</b>(정의문: "중첩될 수 없으며 지속시간만을 초기화").
        /// </summary>
        public void ApplyHaste(float increasePercent, float seconds)
        {
            if (seconds <= 0f) return;
            _hasteAttackSpeedMul = Mathf.Max(1f, 1f + increasePercent / 100f);
            _hasteUntil = Time.time + seconds;
        }

        /// <summary>지금 「고통의 기쁨」이 켜져 있는지.</summary>
        public bool IsHastened => Time.time < _hasteUntil;

        // ------------------------------------------------------------------
        // ★★ 「이벤트 보정」 — 공격속도·이동속도를 <b>올리고 내리는 한 벌</b>
        //   (2026-08-24 · 이벤트 보상 `enemy_atk_spd_durat_up/down` ·
        //    `enemy_move_spd_durat_up/down` 을 걸 통로)
        //
        // <b>왜 새로 만들었나</b> — 이미 있는 통로는 방향이 <b>한쪽뿐</b>이다:
        //   · 「허약」 (<see cref="ApplyWeaken"/>)  — 공격속도를 <b>깎기만</b> 한다
        //   · 「고통의 기쁨」(<see cref="ApplyHaste"/>) — <c>Mathf.Max(1f, …)</c> 라 <b>올리기만</b> 한다
        //   · 이동속도에는 배율 통로가 <b>아예 없었다</b>
        // 이벤트 표는 넷을 모두 요구한다(적 공속 ±, 적 이속 ±). 스킬 통로에 억지로
        // 얹으면 «스킬이 걸렸다» 는 판정(<see cref="IsWeakened"/>·<see cref="IsHastened"/>)이
        // 이벤트 때문에 켜져 상태 표시·중첩 규칙이 어긋난다 — 그래서 <b>다른 원인은 다른 칸</b>이다.
        //
        // ★ 능력치를 건드리지 않고 <b>쓰는 자리에서 곱한다</b> — 「허약」과 같은 짜임이다.
        //   그래서 로스터·성장 창의 표시값은 그대로다.
        // ⚠ 캐릭터의 공속·이속 이벤트 보상은 이 통로를 <b>쓰지 않는다</b> —
        //   그쪽은 능력치가 있으므로 `EventRewardService` 가 StatType 으로 걸고,
        //   화면 표시도 같이 움직이는 편이 맞다. 이 통로는 <b>능력치가 없는 유닛</b>
        //   (몬스터·중립)을 위한 것이다.
        // ------------------------------------------------------------------

        float _eventAtkSpeedMul = 1f;
        float _eventAtkSpeedUntil;
        float _eventMoveSpeedMul = 1f;
        float _eventMoveSpeedUntil;

        /// <summary>
        /// 이벤트 보정 — 공격속도에 <paramref name="percent"/> % 를 더한 배율을 곱한다
        /// (+20 이면 1.2배 · −20 이면 0.8배). 또 걸면 <b>새 값으로 덮고 시간도 새로 잡는다</b>.
        /// </summary>
        public void ApplyEventAttackSpeedPercent(float percent, float seconds)
        {
            if (seconds <= 0f) return;
            _eventAtkSpeedMul = Mathf.Max(0.05f, 1f + percent / 100f);
            _eventAtkSpeedUntil = Time.time + seconds;
        }

        /// <summary>이벤트 보정 — 이동속도. <see cref="ApplyEventAttackSpeedPercent"/> 와 같은 규칙.</summary>
        public void ApplyEventMoveSpeedPercent(float percent, float seconds)
        {
            if (seconds <= 0f) return;
            _eventMoveSpeedMul = Mathf.Max(0.05f, 1f + percent / 100f);
            _eventMoveSpeedUntil = Time.time + seconds;
        }

        /// <summary>지금 곱해야 하는 이벤트 공격속도 배율(안 걸렸으면 1).</summary>
        float EventAttackSpeedMul => Time.time < _eventAtkSpeedUntil ? _eventAtkSpeedMul : 1f;

        /// <summary>지금 곱해야 하는 이벤트 이동속도 배율(안 걸렸으면 1).</summary>
        float EventMoveSpeedMul => Time.time < _eventMoveSpeedUntil ? _eventMoveSpeedMul : 1f;

        // ------------------------------------------------------------------
        // ★★ 「중독」 — 초당 최대 체력 비례 지속 피해 (2026-08-20 · 베일 「담배 연기」)
        //
        // 정의문(`skill_type_desc_Pipe_smoke`): <i>"…{value_04}초 만큼 중독상태가 됩니다.
        // 중독상태가 된 캐릭터는 <b>매 초 최대체력의 {value_05}%</b>의 피해를 입습니다."</i>
        //
        // ★ <b>왜 「허약」·「구속」과 같은 자리에 두는가</b> — 이것도 <b>시각(時刻) 하나로
        //   표현되는 상태</b>다(되돌릴 보정이 없다 · 유닛이 죽으면 컴포넌트째 사라진다).
        //   위 「허약」 주석의 판단 기준이 그대로 적용된다.
        //
        // ★★ 하지만 <b>하나 다르다 — 이건 스스로 피해를 낸다.</b> 그래서 남은 시간 말고
        //   <b>초 누적기</b>가 하나 더 필요하다. 프레임마다 조금씩 깎지 않고 <b>1초마다
        //   한 번</b> 넣는다 — 정의문이 «매 초» 이고, 프레임 분할로 넣으면
        //   ① 최대 체력이 작은 유닛에서 <b>반올림이 0 이 되어 아예 안 아프고</b>
        //   ② 전투 숫자(88절)가 초당 60개 뜬다.
        //
        // ⚠ <b>중첩되지 않는다</b> — 다시 걸리면 «더 아픈 쪽» 을 남기고 지속시간을 새로
        //   잡는다(「허약」의 «지속시간만 초기화» 와 같은 취지). 연기 안에 서 있으면
        //   매 프레임 다시 걸리는데, 중첩시키면 <b>몇 프레임 만에 즉사</b>한다.
        // ------------------------------------------------------------------

        /// <summary>「중독」이 끝나는 시각. 0 이면 안 걸렸다.</summary>
        float _poisonUntil;

        /// <summary>「중독」이 <b>매초</b> 깎는 최대 체력의 %.</summary>
        float _poisonPercentPerSecond;

        /// <summary>다음 「중독」 피해를 넣을 시각 — 1초 간격을 지키는 데 쓴다.</summary>
        float _poisonNextTickAt;

        /// <summary>「중독」의 화면 표시 이름. 「구속」의 <c>_boundLabel</c> 과 같은 이유로 인스턴스 값이다.</summary>
        string _poisonLabel = "중독";

        /// <summary>
        /// <b>「중독」</b> — <paramref name="seconds"/> 초 동안 매초 최대 체력의
        /// <paramref name="percentOfMaxHpPerSecond"/> % 를 깎는다.
        ///
        /// 이미 걸려 있으면 <b>더 아픈 쪽의 세기</b>를 남기고 지속시간을 새로 잡는다
        /// (중첩 금지 — 위 ⚠). 세기가 같으면 시간만 늘어난다.
        /// </summary>
        public void ApplyPoison(float percentOfMaxHpPerSecond, float seconds, string label = null)
        {
            if (seconds <= 0f || percentOfMaxHpPerSecond <= 0f) return;

            bool fresh = !IsPoisoned;
            _poisonPercentPerSecond = Mathf.Max(_poisonPercentPerSecond, percentOfMaxHpPerSecond);
            _poisonUntil = Mathf.Max(_poisonUntil, Time.time + seconds);
            if (!string.IsNullOrEmpty(label)) _poisonLabel = label;

            // 새로 걸릴 때만 시계를 다시 찍는다 — 매 프레임 갱신하면 «다음 1초» 가 계속
            // 뒤로 밀려 <b>연기 안에 서 있는 동안 한 대도 안 맞는다</b>.
            if (fresh) _poisonNextTickAt = Time.time + 1f;
        }

        /// <summary>지금 「중독」에 걸려 있는지.</summary>
        public bool IsPoisoned => Time.time < _poisonUntil;

        /// <summary>
        /// 지금 걸린 「중독」의 화면 표시 이름. <see cref="IsPoisoned"/> 가 false 여도
        /// 마지막 값을 들고 있으니 호출부는 반드시 그쪽을 먼저 볼 것
        /// (<see cref="BoundLabel"/> 과 같은 규칙).
        /// </summary>
        public string PoisonLabel => _poisonLabel;

        /// <summary>「중독」을 즉시 푼다.</summary>
        public void ClearPoison()
        {
            _poisonUntil = 0f;
            _poisonPercentPerSecond = 0f;
        }

        /// <summary>
        /// 「중독」의 <b>초당 피해</b>를 넣는다. <see cref="Update"/> 가 매 프레임 부르고,
        /// 1초가 지났을 때만 실제로 깎는다.
        ///
        /// ★ <b>구속보다 먼저 돈다</b>(<see cref="Update"/> 참조) — 구속된 채로 중독되면
        ///   «움직이지도 못하고 독도 안 퍼진다» 가 되는데, 두 상태는 서로 아무 관계가 없다.
        ///
        /// ⚠ 피해량은 <b>올림</b>이다 — 최대 체력이 작은 유닛에서 0 이 되면
        ///   «최대체력의 %» 라는 말 자체가 무너진다(「타오르는 숨결」과 같은 판단).
        /// ⚠ <see cref="DamageableUnit.ApplyDamage(int)"/> 를 쓴다 — 근거가 공격력이 아니라
        ///   <b>맞는 쪽의 체력</b>이라 방어력 계산에 넣을 자리가 없다.
        /// </summary>
        void TickPoison()
        {
            if (!IsPoisoned) return;
            if (Time.time < _poisonNextTickAt) return;

            _poisonNextTickAt = Time.time + 1f;

            int amount = Mathf.CeilToInt(_self.MaxHp * _poisonPercentPerSecond / 100f);
            if (amount > 0) _self.ApplyDamage(amount);
        }

        // ------------------------------------------------------------------
        // ★★ 화상 (2026-08-20 — 불칸 「타오르는 분노」 80031)
        //
        // <b>중독과 무엇이 다른가</b> — 기준이 다르다:
        //     중독  <b>맞는 쪽</b>의 최대 체력 %      (베일 「담배 연기」)
        //     화상  <b>때린 쪽</b>의 공격력 %          (불칸 「타오르는 분노」)
        //
        // 그래서 «퍼센트» 로는 표현할 수 없다 — 걸 때 <b>계산이 끝난 정수</b>를 받는다.
        // 두 개를 한 칸에 합치지 않은 이유도 같다: 합치면 어느 기준인지 잃어버리고,
        // 둘이 동시에 걸렸을 때 한쪽이 다른 쪽을 덮는다.
        //
        // ⚠ 걸린 값은 <b>더 큰 쪽</b>으로 둔다(중독과 같은 규칙) — 약한 화상이 강한 화상을
        //   덮어 깎으면 «불이 도중에 약해진다» 는 사고가 난다.
        // ------------------------------------------------------------------

        int _burnPerSecond;
        float _burnUntil;
        float _burnNextTickAt;
        string _burnLabel;

        /// <summary>지금 불타고 있는지.</summary>
        public bool IsBurning => Time.time < _burnUntil;

        /// <summary>화상의 화면 표시 이름(로스터·상세 카드가 쓴다).</summary>
        public string BurnLabel => _burnLabel;

        /// <summary>
        /// <paramref name="damagePerSecond"/> 만큼을 <paramref name="seconds"/> 초 동안
        /// <b>초당 한 번</b> 입힌다. 계산은 <b>거는 쪽</b>이 끝내서 넣는다(위 ★★).
        /// </summary>
        public void ApplyBurn(int damagePerSecond, float seconds, string label = null)
        {
            if (damagePerSecond <= 0 || seconds <= 0f) return;

            bool fresh = !IsBurning;
            _burnPerSecond = Mathf.Max(_burnPerSecond, damagePerSecond);
            _burnUntil = Mathf.Max(_burnUntil, Time.time + seconds);
            if (!string.IsNullOrEmpty(label)) _burnLabel = label;

            // 새로 붙을 때만 시계를 찍는다 — 매 프레임 갱신하면 «다음 1초» 가 계속 밀려
            // <b>불 속에 있는 동안 한 대도 안 맞는다</b>(중독에서 이미 겪은 함정).
            if (fresh) _burnNextTickAt = Time.time + 1f;
        }

        /// <summary>화상을 즉시 끈다.</summary>
        public void ClearBurn()
        {
            _burnUntil = 0f;
            _burnPerSecond = 0;
        }

        void TickBurn()
        {
            if (!IsBurning)
            {
                _burnPerSecond = 0;
                return;
            }
            if (Time.time < _burnNextTickAt) return;

            _burnNextTickAt = Time.time + 1f;
            if (_burnPerSecond > 0) _self.ApplyDamage(_burnPerSecond);
        }

        /// <summary>「허약」을 즉시 푼다 (구속으로 넘어갈 때).</summary>
        public void ClearWeaken()
        {
            _weakenUntil = 0f;
            _weakenAttackSpeedMul = 1f;
        }

        /// <summary>
        /// <b>「구속」</b> — <paramref name="seconds"/> 초 동안 <b>이동도 공격도 못 한다.</b>
        ///
        /// ⚠ <see cref="SetCombatSuppressed"/> 와 <b>다르다</b>: 그쪽은 "싸우지 않는다" 일 뿐
        /// 이동은 그대로다(후퇴가 그 위에서 돈다). 구속은 <see cref="Act"/> 자체를 건너뛴다.
        /// 그래서 후퇴·집결 같은 상위 지시가 켜져 있어도 그 자리에 묶인다.
        ///
        /// 이미 걸려 있으면 <b>더 긴 쪽</b>을 남긴다 — 짧은 것이 덮어써서 일찍 풀리면
        /// "구속이 안 걸렸다" 로 보인다.
        ///
        /// <paramref name="label"/> 은 상세 카드(<see cref="UI.UnitPortraitPanel"/>)가 보여줄
        /// 이름이다(2026-08-19, <see cref="BossSkillSO.StatusName"/>). 비우면(또는 안 넘기면)
        /// <b>"구속"</b>으로 떨어진다 — <b>매번 넣은 값으로 갈아 끼운다</b>(지속시간과 달리
        /// "더 긴 쪽"을 비교하지 않는다). 지금 화면에 뭐라고 뜰지는 <b>가장 최근에 건 스킬</b>이
        /// 정하는 것이 자연스럽다.
        /// </summary>
        public void ApplyBind(float seconds, string label = null)
        {
            if (seconds <= 0f) return;
            _boundUntil = Mathf.Max(_boundUntil, Time.time + seconds);
            _boundLabel = string.IsNullOrEmpty(label) ? "구속" : label;
            _target = null;
            _engaged = false;
            _engagedWith = null;
        }

        /// <summary>지금 「구속」되어 있는지 (연출·UI 에서 쓸 수 있게 공개).</summary>
        public bool IsBound => Time.time < _boundUntil;

        /// <summary>
        /// 지금 걸린 「구속」의 화면 표시 이름("구속"·"기절" 등). <see cref="IsBound"/> 가
        /// false 여도 마지막 값을 들고 있을 수 있으니, 호출부는 반드시 <see cref="IsBound"/>
        /// 를 먼저 확인할 것(<see cref="UI.UnitPortraitPanel"/> 참조).
        /// </summary>
        public string BoundLabel => _boundLabel;

        /// <summary>
        /// 구속을 <b>즉시</b> 푼다 — 피올로의 「정신 안정」이 쓴다.
        ///
        /// ★ 근거는 스킬 정의문이다 (`skill_type_desc_Huge_threat`, 2026-08-19):
        /// <i>"구속 상태는 부정적인 정신 이상 상태를 해제하는 효과로 해제 가능하다"</i>.
        /// 구속은 정신 이상이 <b>아니지만</b>(그쪽은 <c>CharacterErosion</c> 이 들고 있다)
        /// 표가 <b>같은 해제 수단</b>을 지정했으므로 그 하나에 같이 태운다.
        ///
        /// ⚠ 말파스 구속탄의 구속에도 <b>같이</b> 적용된다 — 같은 상태에 해제 규칙이
        ///   두 벌 있으면 하나를 고칠 때 다른 하나가 남는다.
        /// </summary>
        public void ClearBind() => _boundUntil = 0f;

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

            // ★ 「중독」(베일 「담배 연기」) — <b>이 함수의 맨 앞</b>이다(TickPoison 주석).
            //   ⚠ 아래 두 갈래는 <b>둘 다 return</b> 한다(벽 탈출 · 구속). 그 뒤에 두면
            //     벽에 끼거나 구속된 동안 <b>독이 멈춘다</b> — 중독은 «행동» 이 아니라
            //     «몸» 에 걸린 상태라 행동을 못 하는 것과 아무 관계가 없다.
            TickPoison();
            TickBurn();
            if (!_self.IsAlive) { _state = CombatState.Dead; return; }   // 독으로 죽었다

            // 벽 안에 갇혀 있으면 이동 판정이 전부 실패해 영구히 멈춘다. 먼저 빼낸다.
            if (EscapeIfEmbedded(dt)) return;

            // ★ 「구속」(말파스 구속탄) — 이동도 공격도 안 한다(ApplyBind 주석 참조).
            //   ⚠ <b>탈출 판정보다는 뒤</b>다: 구속된 채로 벽/구조물에 갇히면 구속이 풀려도
            //     빠져나올 방법이 없어 영구히 얼어붙는다.
            if (IsBound)
            {
                _state = CombatState.Idle;
                return;
            }

            AcquireTargetIfNeeded();
            DecideState();
            Act(dt);
            TrackStuck(dt);
        }

        /// <summary>
        /// 유닛이 막힌 칸 안에 있으면 가장 가까운 빈 칸으로 밀어낸다.
        /// 스폰 위치가 나중에 막히는 경우(성역이 <c>Start</c> 에서 자기 발판 칸을
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
        /// (특히 후퇴 지점이 성역 주변 좁은 반경이라 포탑이 몰려 있어 실제로 자주 걸렸다).
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
        /// (발밑에 세워진 포탑·성역 발판) 자신이기 때문이다. 그 구간을 벗어난 뒤에 다시
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

        // ------------------------------------------------------------------
        // ★★ 도발 (2026-08-20 — 카이론 「천상의 방패」 80026)
        //
        // <b>왜 사냥 타겟(<see cref="SetForcedHuntTarget"/>)을 재활용하지 않았나</b> —
        // 그 칸은 <b>정신 이상 「혼란」이 소유</b>하고 있고(위 그 함수의 긴 주석), 도발이
        // 그 위에 덮어쓰면 도발이 풀릴 때 혼란도 같이 풀린다. 서로 다른 원인이 같은 칸을
        // 쓰면 «누가 마지막에 썼는가» 로 동작이 갈린다 — 이 파일이 이미 한 번 겪은 사고다.
        //
        // 그래서 <b>자기 칸</b>을 따로 둔다. 판정은 타겟을 고르는 <b>맨 앞</b>에서 한 줄로
        // 끝난다: 도발 중이면 그 상대가 곧 타겟이다.
        //
        // ⚠ 도발한 쪽이 죽거나 시간이 다하면 <b>스스로 풀린다</b> — 별도의 해제 호출이
        //   필요 없게 «시각 + 대상» 두 값으로만 표현한다(「허약」·「구속」과 같은 규칙).
        // ------------------------------------------------------------------

        DamageableUnit _tauntBy;
        float _tauntUntil;

        /// <summary>지금 도발당해 특정 상대만 노리고 있는지.</summary>
        public bool IsTaunted =>
            Time.time < _tauntUntil && _tauntBy != null && _tauntBy.IsAlive;

        /// <summary>도발한 상대. 도발 중이 아니면 null.</summary>
        public DamageableUnit TauntedBy => IsTaunted ? _tauntBy : null;

        /// <summary>
        /// <paramref name="seconds"/> 초 동안 <paramref name="by"/> 만 노리게 한다.
        /// 이미 도발 중이면 <b>더 긴 쪽</b>으로 둔다(무적·보호막과 같은 규칙).
        /// </summary>
        public void ApplyTaunt(DamageableUnit by, float seconds)
        {
            if (by == null || seconds <= 0f) return;
            _tauntBy = by;
            _tauntUntil = Mathf.Max(_tauntUntil, Time.time + seconds);
            _target = by;
        }

        /// <summary>도발을 즉시 푼다.</summary>
        public void ClearTaunt()
        {
            _tauntBy = null;
            _tauntUntil = 0f;
        }

        // ------------------------------------------------------------------
        // ★★ 다중 사격 (2026-08-20 — 시카리아 「한발에 두마리」 80020)
        //
        // 정의문: <i>"원거리 공격은 사거리 안에 있는 모든 적을 동시에 {value_01} 마리를 공격"</i>.
        // 즉 <b>총 몇 마리</b>이고 추가 수가 아니다 — 값 2 는 «둘을 동시에» 다.
        //
        // ⚠ 마법의 범위 타격(<see cref="PerformMagicSplash"/>)과 <b>다르다</b>: 그쪽은
        //   «타겟 지점 주변의 정사각형» 이고 이쪽은 «내 사거리 안의 아무 적 N 명» 이다.
        //   그래서 코드를 합치지 않는다.
        // ------------------------------------------------------------------

        int _rangedMultiShot = 1;

        /// <summary>원거리 평타가 한 번에 때리는 적 수(총계). 1 이면 평소와 같다.</summary>
        public int RangedMultiShot => _rangedMultiShot;

        /// <summary>다중 사격 수를 정한다. 1 이하는 «평소» 로 되돌린다.</summary>
        public void SetRangedMultiShot(int targets) => _rangedMultiShot = Mathf.Max(1, targets);

        void AcquireTargetIfNeeded()
        {
            if (_combatSuppressed) { _target = null; return; }

            // ★ 도발이 <b>가장 앞</b>이다 — 전술·사냥·진영 판단 전부를 덮는다.
            //   그것이 «도발» 이라는 말의 뜻이다(위 ★★).
            if (IsTaunted)
            {
                _target = _tauntBy;
                return;
            }

            // ── 비선공 유닛 ──────────────────────────────────────────────────
            // 스스로 적을 찾지는 않는다. 다만 두 가지에는 반응한다:
            //   ① 자기가 맞았으면 때린 상대에게 반격한다.
            //   ② <b>같은 무리의 동료</b>가 맞았으면 그 공격자에게 덤빈다 —
            //      표의 <c>atk_take</c>(무리 반격 여부)가 켜진 경우만.
            //
            // ★ ②가 이번에 새로 뚫린 길이다 (유저 지시 2026-08-15).
            //   예전에는 여기서 반격 대상만 잡고 <b>즉시 return</b> 했기 때문에, 아래 971행의
            //   동료 구원(ally call) 경로를 통째로 건너뛰었다. 그 상태에서 "모든 중립은
            //   비선공" 으로 바꾸면 <b>무리 반격이 영원히 발동하지 않는다</b> — 무리를 만들어도
            //   서로를 도우러 갈 코드 경로가 없어진다.
            //
            //   그래서 조기 return 을 유지하되(비선공은 진영 기준 탐색을 하면 안 된다),
            //   <b>무리 호출만</b> 그 안에서 따로 확인한다. 판정 범위는 선공 유닛의 동료 구원과
            //   같은 <c>allyCallRange</c> 를 쓴다 — 두 벌로 갈리면 "무리는 12타일인데 구원은
            //   8타일" 같은 어긋남이 생긴다.
            if (!canAcquireTargets)
            {
                DamageableUnit hit = FindRetaliationTarget();

                if (hit == null && answerAllyCalls && !_retreatFiring)
                    hit = FindPackCallAttacker();

                _target = hit;
                return;
            }

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

            // ★ 이 타겟을 <b>귀환 지점에서 얼마나 멀리까지</b> 쫓아도 되는지.
            //   경로마다 다르므로 여기서 같이 들고 간다 — 아래 하나뿐인 목줄 관문이 쓴다.
            float leashAllowance = leashRange;

            // 아무도 못 찾았으면 "나를 때린 상대" 를 본다 — <b>선공 유닛도 맞으면 반격한다</b>
            // (유저 정의: "중립 몬스터는 언제든 공격받으면 반격해야 한다").
            // 이게 없으면 인식 범위(detectRange)나 목줄 밖에서 맞을 때 가만히 서서 맞기만 한다 —
            // 원거리 캐릭터가 사거리 밖에서 쏘는 상황이 정확히 그렇다.
            if (found == null)
            {
                found = FindRetaliationTarget();
                if (found != null) leashAllowance = Mathf.Max(leashRange, retaliateChaseRange);
            }

            // ★ 동료를 때리는 적 — 내 사거리 밖이라도 이쪽으로 간다 (유저 지시 2026-08-11).
            //
            // 지금 때릴 수 있는 적이 있으면 그대로 둔다 — 눈앞의 적을 두고 딴 데로 달려가면
            // 오히려 대열이 무너진다. <b>때릴 수 있는 적이 없을 때만</b> 동료를 돕는다.
            if (answerAllyCalls && !_retreatFiring && (found == null || !InAttackRange(found)))
            {
                DamageableUnit caller = FindAllyAttacker();
                if (caller != null)
                {
                    found = caller;
                    leashAllowance = Mathf.Max(leashRange, allyCallRange);
                }
            }

            // ★★ <b>하나뿐인 목줄 관문</b> — 기준점은 언제나 <b>귀환 지점</b>(움직이지 않는 점)이다.
            //
            // <b>왜 이렇게 바꿨나 (유저 리포트 2026-08-13: "캐릭터가 몬스터에게 끌려간다")</b> —
            // 예전에는 목줄이 <b>정상 탐색 경로에만</b> 걸려 있었고, 반격·동료 구원은
            // 목줄을 아예 안 보면서 <b>자기 위치 기준</b>으로만 거리를 봤다. 그래서
            // 걸어갈수록 판정 범위도 같이 따라와 <b>래칫처럼 얼마든지 끌려갔다</b> —
            // 73-12절이 중립 몬스터에서 똑같이 겪고 "판정 기준을 움직이지 않는 점으로
            // 옮겨야 한다"고 기록한 것과 같은 구조의 버그다.
            //
            // 이제 <b>모든 경로</b>가 이 한 곳을 지나고, 경로마다 허용 거리만 다르다:
            //   정상 탐색 = leashRange · 반격 = max(leashRange, retaliateChaseRange)
            //   동료 구원 = max(leashRange, allyCallRange)
            //
            // ⚠️ 전부 <b>max()</b> 다 — 각 경로의 값은 "최소 이만큼은 보장한다"는 뜻이고,
            //    <c>leashRange</c> 를 더 크게 올리면 그쪽이 이긴다. 씬의 캐릭터 템플릿 기준
            //    실효값은 정상 탐색 7 · 반격 8 · 동료 구원 12타일이다(leashRange 가 7).
            // (사냥·치유는 위에서 각자 규칙으로 이미 return 했다.)
            if (found != null && !advanceToObjective && leashAllowance > 0f &&
                Vector2.Distance(found.transform.position, _homePosition) > leashAllowance)
                found = null;

            // 후퇴 사격 중에는 <b>때릴 수 있는 적만</b> 잡는다 — 쫓아가지 않는 것이 이 상태의 정의다.
            if (_retreatFiring && found != null && !InAttackRange(found)) found = null;

            _target = found;
        }

        /// <summary>
        /// ★ 그 자리가 <b>지금 우리 진영의 시야 안</b>인지. 안개를 안 보는 유닛
        /// (<see cref="respectFogOfWar"/> 가 꺼진 몬스터)이나 안개 서비스가 없으면 항상 true 다 —
        /// 즉 <b>기존 동작이 그대로 유지</b>되고, 캐릭터만 이 규칙에 걸린다.
        ///
        /// 안개는 진영 공용 텍스처 하나라 <b>동료가 밝힌 곳도 "보이는" 것으로 잡힌다</b> —
        /// 엘린의 「타고난 섬세함」(공유 시야의 적에게 사거리 +2)이 그 위에서 성립한다.
        /// </summary>
        public bool IsFogVisible(Vector3 worldPos)
        {
            if (!respectFogOfWar) return true;
            if (_fog == null || !_fog.IsReady) return true;
            return _fog.IsVisibleWorld(worldPos);
        }

        /// <summary>
        /// ★★ <b>이 유닛이 지금 보이는가</b> — <see cref="IsFogVisible"/> 와 달리
        /// <b>몸집을 안다</b>(2026-08-18).
        ///
        /// <b>왜 필요했나 — 카르시노스 앞에서 캐릭터가 얼어붙었다.</b>
        /// 거리 판정은 몸집을 더해서 재는데(<see cref="TargetRadius"/>, 에픽은 <b>2.2타일</b>)
        /// 시야 판정은 <b>중심점 한 점</b>만 봤다. 그래서 4.4x5.1타일짜리 몸의 가장자리에 붙어
        /// 서면 <b>"사거리 안"인데 "안 보이는"</b> 상태가 성립하고, 그 조합은
        /// <see cref="DecideState"/> 를 Attack 에 묶어 <b>이동도 공격도 하지 않는</b> 정지가 된다.
        ///
        /// 고친 방식 — 중심이 안 보이면 <b>나에게 가장 가까운 몸 가장자리</b>를 한 번 더 본다.
        /// 몸의 일부라도 시야에 들어와 있으면 보이는 것으로 친다. 몸집이 없는 유닛
        /// (<see cref="TargetRadius"/> 기본 0.4)은 두 점이 사실상 같아 동작이 안 바뀐다.
        /// </summary>
        public bool IsUnitVisible(DamageableUnit unit)
        {
            if (unit == null) return false;

            Vector3 center = unit.transform.position;
            if (IsFogVisible(center)) return true;

            float radius = TargetRadius(unit);
            if (radius <= 0.45f) return false;          // 몸집이 없는 유닛 — 중심이 곧 전부다

            Vector2 toMe = (Vector2)(transform.position - center);
            if (toMe.sqrMagnitude < 0.0001f) return true;   // 겹쳐 있으면 당연히 보인다

            return IsFogVisible(center + (Vector3)(toMe.normalized * radius));
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

                // ★ 안 보이는 적을 도우러 달려가지는 않는다 — 가서도 때릴 수 없다
                //   (유저 지시 2026-08-13). 그 자리는 '확인할 지점'으로 따로 처리된다.
                if (!IsFogVisible(attacker.transform.position)) continue;

                float sqr = ((Vector2)(attacker.transform.position - myPos)).sqrMagnitude;
                // 인식 범위 밖까지 달려가지는 않는다 — 목줄과 같은 취지의 안전장치.
                if (sqr > EffectiveDetectRange * EffectiveDetectRange) continue;
                if (sqr >= bestSqr) continue;

                best = attacker;
                bestSqr = sqr;
            }
            return best;
        }

        /// <summary>
        /// ★ <b>무리 반격</b> — 같은 무리의 동료를 때리고 있는 적 중 가장 가까운 하나.
        /// (유저 지시 2026-08-15: *"같은 부대의 동료 몬스터가 공격 당할 시 반격"*)
        ///
        /// <see cref="FindAllyAttacker"/> 와 <b>거의 같지만 두 가지가 다르다</b>:
        ///   ① 동료의 조건이 "같은 진영" 이 아니라 <b>"같은 무리"</b>다
        ///      (<see cref="Units.NeutralPack.SamePack"/>). 진영만 보면 <b>맵 반대편의
        ///      남남인 중립까지</b> 서로를 동료로 여긴다 — 모든 중립이 같은
        ///      <c>Faction.Neutral</c> 이기 때문이다. 실제로 그렇게 동작하고 있었다.
        ///   ② <b>인식 범위(detectRange) 제한을 걸지 않는다.</b> 무리는 "봤다" 가 아니라
        ///      "같은 편이 비명을 질렀다" 로 움직이는 것이라, 무리 반경 안이면 달려간다.
        ///
        /// 무리에 속하지 않았거나 무리 반격이 꺼져 있으면 아무것도 하지 않는다.
        /// </summary>
        DamageableUnit FindPackCallAttacker()
        {
            if (_self == null || allyCallRange <= 0f) return null;

            var myPack = Units.NeutralPack.Of(_self);
            if (myPack == null || !myPack.AnswersPackCalls) return null;

            Vector3 myPos = transform.position;
            float limitSqr = allyCallRange * allyCallRange;
            DamageableUnit best = null;
            float bestSqr = float.MaxValue;

            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit ally = all[i];
                if (ally == null || !ally.IsAlive || ReferenceEquals(ally, _self)) continue;
                if (!Units.NeutralPack.SamePack(_self, ally)) continue;
                if (((Vector2)(ally.transform.position - myPos)).sqrMagnitude > limitSqr) continue;

                DamageableUnit attacker = AttackerOf(ally);
                if (attacker == null) continue;
                if (!IsFogVisible(attacker.transform.position)) continue;

                float sqr = ((Vector2)(attacker.transform.position - myPos)).sqrMagnitude;
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

            // ★★ 2026-08-21 — <b>같은 진영에게는 반격하지 않는다</b> (유저 리포트:
            //   *"혼란 상태에서 캐릭터 끼리 전투가 일어나면 혼란이 풀리더라도 몬스터가
            //   오기 전까진 계속해서 둘이서 전투를 하는 버그"*).
            //
            //   <b>왜 «혼란이 풀려도» 계속 싸웠나</b> — 「혼란」은 아군을 때리는 상태를
            //   <c>SetForcedHuntTarget</c> 으로 구현하고, 풀릴 때 그 잠금과 타겟을 정확히
            //   놓는다(<c>CharacterErosion.ClearActive</c> → <c>ClearForcedHuntTarget</c>).
            //   그래서 <b>혼란에 걸린 쪽은 제대로 멈춘다.</b> 문제는 <b>맞은 쪽</b>이었다 —
            //   여기서 «나를 때린 상대» 를 <b>진영을 보지 않고</b> 돌려줘서, 맞은 아군이
            //   때린 아군을 반격했다. 그 반격이 다시 상대의 <c>LastAttacker</c> 를 갱신하니
            //   <b>서로가 서로의 «방금 나를 때린 상대» 가 되어</b> 8초 기억
            //   (<see cref="retaliateMemorySeconds"/>)이 영원히 갱신됐다.
            //   진짜 적이 나타나면 그쪽이 먼저 잡히므로(반격은 <b>못 찾았을 때만</b> 본다)
            //   «몬스터가 오기 전까지» 라는 리포트가 정확히 그 구조를 가리킨다.
            //
            //   ★ 같은 판정이 <b>동료 구원 쪽에는 이미 있었다</b>(<see cref="AttackerOf"/> 의
            //     «혼란으로 아군이 때린 경우») — 두 경로 중 한 곳에만 있었던 것이다.
            //   ⚠ 「혼란」에 걸린 쪽의 공격은 이 함수를 지나지 않는다(강제 사냥 타겟이다) —
            //     즉 이 한 줄이 <b>혼란의 효과를 약화시키지 않는다.</b>
            if (attacker.Faction == _self.Faction) return null;

            // ★ 안 보이는 상대에게는 반격하지 않는다 (유저 지시 2026-08-13:
            //   "시야 밖에 있는 적은 공격 못하게").
            //
            //   ⚠️ 이 경로가 안개 판정의 <b>가장 큰 구멍</b>이었다 — 타겟 선정 필터
            //   (<see cref="BuildTargetFilter"/>)는 안개를 보는데 반격은 안 봐서,
            //   <b>사거리 밖에서 쏘는 안 보이는 적을 그대로 쫓아갔다.</b>
            //   대신 <c>CharacterBehavior</c> 가 그 자리를 "확인할 지점"으로 남긴다
            //   (<see cref="SightAlertService"/>).
            if (!IsFogVisible(attacker.transform.position)) return null;

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
        /// 치유 유형의 "타겟" — <b>내 주변에서 체력이 깎인 아군</b> 중 가장 많이 다친 하나.
        /// 없으면 타겟 없음(이동은 <see cref="Units.CharacterBehavior"/> 에 맡긴다).
        /// 적 타겟과 같은 <see cref="_target"/> 슬롯을 쓰기 때문에, 이동·상태 판정
        /// (<see cref="DecideState"/>/<see cref="Act"/>)을 그대로 재사용할 수 있다 —
        /// 실제 "때리기"만 <see cref="TryAttack"/> 에서 회복으로 갈린다.
        ///
        /// <b>★ 대상은 "자신을 제외한 다른 캐릭터" 뿐이다</b> (유저 확정 2026-08-13:
        /// "포탑이랑 성역은 회복 대상에서 빼 · 회복은 자신을 제외한 다른 캐릭터에게만 가능").
        /// 그래서 <c>kindFilter: UnitKind.Character</c> 를 넘긴다 — 성역·포탑은 후보가 아니다.
        /// ⚠ 예전에는 <c>FindWoundedAlly(..., exclude: _self)</c> 로 불렀지만 그 함수의
        ///   <c>includeSelfIfWounded</c> 기본값이 <b>true</b> 라 <c>exclude</c> 가 아무 일도 안 했다.
        ///   그래서 자기가 제일 많이 다쳤으면 <b>자기를 타겟으로 잡고</b>, 거리 0 이라 항상
        ///   사거리 안 → 그 자리에서 자기 회복만 반복했다. 호출부(<c>CharacterBehavior</c>)는
        ///   타겟이 있으면 목적지를 안 건드리므로 <b>영원히 제자리에 멈춰 회복 모션만</b> 나왔다 —
        ///   유저가 리포트한 그 그림이다. 자기 체력은 재생(<c>TickRegen</c>)이 맡는다.
        ///
        /// <b>★ 목줄로 거르지 않는다</b> — 목줄의 기준점 <c>_homePosition</c> 은 지금 걸어가는
        /// <b>탐험 목적지</b>(14~60타일 밖)라, 바로 옆의 다친 동료가 "목줄 밖"으로 걸러지는
        /// 일이 생긴다(56-2절의 사냥이 겪은 것과 같은 함정). 후보는 이미
        /// <see cref="EffectiveDetectRange"/> 로 <b>내 위치 기준</b> 잘려 있으므로 그것으로 충분하다.
        /// </summary>
        void AcquireHealTarget()
        {
            bool targetInvalid = _target == null || !_target.IsAlive || _target.HpRatio >= 1f;
            if (!targetInvalid && Time.time < _nextRetargetTime) return;
            _nextRetargetTime = Time.time + RetargetInterval;

            _target = UnitRegistry.FindWoundedAlly(
                transform.position, _self.Faction, EffectiveDetectRange, _self,
                includeSelfIfWounded: false, kindFilter: UnitKind.Character);
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

                // ★★ <b>사거리 안이어도 "지금 때릴 수 있어야" Attack 이다</b> (2026-08-18).
                //   Attack 상태는 <b>이동하지 않는다</b>. 그래서 때릴 수 없는 상대에게 Attack 으로
                //   굳으면 그 자리에서 영영 멈춘다 — 캐릭터 쪽은 더 확실히 멈춘다.
                //   <c>CharacterBehavior.Update</c> 가 "타겟이 있으면 목적지를 안 건드린다" 로
                //   일찍 빠져나가기 때문에 <b>목적지조차 새로 안 고른다.</b>
                //
                //   ⚠ 실제로 그 조합이 성립했다 — <b>카르시노스</b>(몸집 2.2타일)를 사냥 타겟으로
                //   문 마법 캐릭터는 사거리 8 + 2.2 = <b>10.2타일</b>에서 Attack 에 들어가는데
                //   시야는 3.5타일이라 <c>TryAttack</c> 이 안개에서 막혔다. 사냥 타겟 경로는
                //   <see cref="AcquireTargetIfNeeded"/> 에서 안개 필터를 건너뛰므로 타겟도 안 풀린다.
                //
                //   → 못 때리면 <b>Chase 로 떨어뜨려 다가가게</b> 한다. 가까워지면 시야에 들어와
                //     자연히 Attack 으로 올라간다. 치유는 예외다(아군을 시야로 막지 않는다).
                bool canHitNow = attackType == TacticalAttackType.Heal || IsUnitVisible(_target);

                if (dist <= EffectiveAttackRange + TargetRadius(_target) && canHitNow)
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

                // ★ 제자리에 선 상태에서도 <b>겹침만은 푼다</b> — 아래 주석 참조.
                case CombatState.Idle:
                    if (!immobile) UnstackWhileIdle(dt);
                    break;
            }
        }

        /// <summary>
        /// 제자리에 선 채로 다른 유닛과 겹쳐 있으면 조금씩 밀려난다.
        ///
        /// ★★ <b>왜 필요한가</b> (2026-08-19, 유저 리포트: *"두 몬스터가 겹쳐져서 움직이지
        /// 않는다"*) — 밀림(<see cref="Separation"/>)은 <see cref="Step"/> <b>안에만</b> 있어서
        /// <b>움직이는 동안에만</b> 작동했다. 그런데 <see cref="CombatState.Idle"/> 은 이름 그대로
        /// <b>아무 이동도 하지 않는</b> 상태다:
        /// <code>
        ///   두 마리가 각자 목적지에 도착 → 둘 다 Idle → 밀림이 아예 안 돈다
        ///   → <b>겹친 채로 영구히 굳는다</b> (배회가 다음 목적지를 뽑는 4~10초 동안 계속)
        ///   → 우연히 목적지가 서로 가까우면 그 상태가 계속 되풀이된다
        /// </code>
        ///
        /// ★ <b>귀환 지점을 같이 옮기는 것이 핵심이다.</b> 안 옮기면 밀려난 거리만큼
        /// <see cref="DecideState"/> 가 <see cref="CombatState.Chase"/> 로 되돌려 보내서
        /// (귀환 판정이 0.3타일이다) <b>밀리고 돌아오고를 무한히 반복</b>한다 — 제자리에서
        /// 덜덜 떠는 모습이 되고, 겹침은 그대로 남는다. Idle 은 "지금 자리가 내 자리" 라는
        /// 뜻이므로 자리를 겹침이 풀린 쪽으로 함께 옮기는 것이 맞다.
        /// 배회 레이어가 다음 추첨에서 귀환 지점을 다시 못 박으므로 값이 흘러가지도 않는다.
        ///
        /// ⚠ 이동은 반드시 <see cref="MoveWithCollision"/> 을 거친다 — 겹침을 푼다고 벽을
        ///   뚫으면 이번에 같이 고치는 "벽에 낀다" 를 우리가 만드는 셈이다.
        /// </summary>
        void UnstackWhileIdle(float dt)
        {
            Vector2 push = Separation();
            if (push.sqrMagnitude < UnstackDeadzone * UnstackDeadzone) return;

            Vector3 before = transform.position;
            MoveWithCollision((Vector3)(Vector2.ClampMagnitude(push, 1f) * CurrentSpeed() * dt));
            _homePosition += transform.position - before;
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

            // ★ <b>안 보이는 적은 때리지 못한다</b> (유저 지시 2026-08-13). 타겟을 고를 때
            //   이미 걸렀지만(<see cref="BuildTargetFilter"/>), 재탐색은 0.2초 간격이라
            //   그 사이에 적이 시야를 벗어날 수 있다 — <b>마지막 관문을 여기 둔다.</b>
            //
            //   ⚠️ 치유 유형은 예외다 — 이때 <c>_target</c> 은 <b>적이 아니라 아군</b>이고,
            //   아군을 시야 때문에 못 살리는 것은 이 규칙의 취지가 아니다.
            if (attackType != TacticalAttackType.Heal && _target != null &&
                !IsUnitVisible(_target))
                return;

            // 능력치(공격 속도)가 있으면 그게 최우선이다 — 캐릭터만 해당하고,
            // 몬스터·포탑은 0 을 돌려주므로 기존 경로(인스펙터 값 → 밸런스 폴백)를 그대로 탄다.
            float statAps = _self.StatAttacksPerSecond;
            float aps = statAps > 0f
                ? statAps
                : (attacksPerSecond > 0f
                    ? attacksPerSecond
                    : (_self.Balance != null ? _self.Balance.attacksPerSecond : 1f));

            // ★ 「허약」(말파스 구속탄) — 공격속도만 깎는다. 능력치 자체는 안 건드리므로
            //   로스터·성장 창의 표시값은 그대로다(DefenseModifier 와 같은 원칙).
            if (IsWeakened) aps *= _weakenAttackSpeedMul;

            // ★ 「고통의 기쁨」(시그리드) — 같은 자리에서 곱한다. 허약과 동시에 걸리면
            //   둘 다 곱해진다(하나가 다른 하나를 지우지 않는다) — 서로 다른 원인이므로.
            if (IsHastened) aps *= _hasteAttackSpeedMul;

            // ★ 이벤트 보정 — 위 ★★ 참조. 스킬과 <b>다른 칸</b>이라 같이 곱해진다.
            aps *= EventAttackSpeedMul;

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
                    // ★ 「한발에 두마리」 — 원거리일 때만, 값이 2 이상일 때만 갈라진다.
                    if (attackType == TacticalAttackType.Ranged && _rangedMultiShot > 1)
                        PerformRangedMultiShot();
                    else
                        _target.TakeDamageFrom(_self);
                    break;
            }

            OnAttackPerformed?.Invoke();
        }

        /// <summary>
        /// ★ 「한발에 두마리」(시카리아 80020) — <b>사거리 안</b>의 적을 최대
        /// <see cref="_rangedMultiShot"/> 명까지 동시에 때린다.
        ///
        /// <b>현재 타겟이 항상 첫 번째</b>다 — 그래야 «조준하던 적이 안 맞는» 일이 없다.
        /// 나머지는 가까운 순으로 채운다(정의문에 우선순위가 없으므로 가장 자연스러운 순서).
        ///
        /// ⚠ <b>사냥 중인 중립</b>도 맞아야 한다. <see cref="UnitRegistry.CollectEnemiesInBox"/>
        ///   는 <see cref="FactionExtensions.Opposite"/> 진영만 모으는데 중립은 그 진영이
        ///   아니다 — <see cref="PerformMagicSplash"/> 가 이미 밟은 함정이라 여기서도
        ///   <b>타겟을 따로 먼저 때린다</b>.
        /// </summary>
        void PerformRangedMultiShot()
        {
            _target.TakeDamageFrom(_self);          // ① 조준하던 적이 먼저다

            int remain = _rangedMultiShot - 1;
            if (remain <= 0) return;

            Vector3 myPos = transform.position;
            float range = EffectiveAttackRange;
            UnitRegistry.CollectEnemiesInBox(myPos, range, _self.Faction, _splashScratch);

            // 가까운 순으로 — 정의문에 우선순위가 없다(위 요약).
            _splashScratch.Sort((x, y) =>
                ((Vector2)(x.transform.position - myPos)).sqrMagnitude
                .CompareTo(((Vector2)(y.transform.position - myPos)).sqrMagnitude));

            float sqr = range * range;
            for (int i = 0; i < _splashScratch.Count && remain > 0; i++)
            {
                DamageableUnit u = _splashScratch[i];
                if (u == null || !u.IsAlive) continue;
                if (ReferenceEquals(u, _target)) continue;                 // ①에서 이미 맞았다
                // 상자로 모았으므로 <b>원형 사거리</b>로 한 번 더 거른다 —
                // 안 그러면 모서리의 적이 사거리 밖인데도 맞는다.
                if (((Vector2)(u.transform.position - myPos)).sqrMagnitude > sqr) continue;

                u.TakeDamageFrom(_self);
                remain--;
            }
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

            // ★★ <b>회복에도 명중·치명타</b> — 「불안정성」(아르세니아 80028) (2026-08-20)
            //
            //   정의문: <i>"아르세니아는 회복과 마법 공격에도 <b>명중률과 크리티컬 확률의
            //   영향을 받습니다</b>"</i>. 유저가 뜻을 확정해 줬다: *"아르세니아의 회복이
            //   크리티컬로 터질때 <b>150%로 회복</b>됨"*.
            //
            //   ⚠⚠ <b>회복은 피해 파이프라인을 안 지난다.</b> 명중·치명타 판정은 전부
            //     <see cref="DamageableUnit.TakeDamageFrom"/> 안에 있고, 회복은 이 함수가
            //     <see cref="DamageableUnit.Heal"/> 을 직접 부른다. 그래서
            //     `HitChancePercent`·`CriticalChancePercent` 를 열어 둔 것만으로는
            //     <b>회복에 아무 일도 일어나지 않았다</b> — 120절에 그 두 칸을 열어 두고도
            //     회복이 그대로였던 이유가 이것이다.
            //
            //   ★ <b>여는 조건을 유닛에게 묻는다</b>(`FullAccuracyAllowed`) — 여기서
            //     «아르세니아인가» 를 묻지 않는다. 같은 예외를 갖는 캐릭터가 늘어도
            //     이 코드는 그대로다(그 프로퍼티의 긴 주석과 같은 원칙).
            //   ★ 배율은 <b>표의 치명타 배율을 그대로</b> 쓴다(`ApplyCriticalDamage`) —
            //     회복만 다른 숫자를 쓰면 «크리티컬 1.5배» 라는 규칙이 두 벌이 된다.
            bool healCrit = false;
            if (_self is Units.CharacterUnit healer && healer.FullAccuracyAllowed)
            {
                // ── 명중 ── 빗나가면 회복이 <b>들어가지 않는다</b>. 연출도 「빗나감」이다.
                if (Random.value * 100f >= _self.HitChancePercent)
                {
                    DamageableUnit.RaiseMissed(_self, _target);
                    return;
                }

                // ── 치명타 ── 터지면 회복량이 배율만큼 커진다.
                if (Random.value * 100f < _self.CriticalChancePercent)
                {
                    amount = _self.Balance.ApplyCriticalDamage(amount);
                    healCrit = true;
                }
            }

            _target.Heal(amount, healCrit);

            // ★★ <b>회복 횟수를 센다</b> (2026-08-21 · 유저 지시: *"힐러는 회복 횟수를
            //   카운트해서 회복을 200번 사용하면 영웅 각성이 가능한 상태로"*).
            //
            //   여기가 «회복을 사용했다» 의 정의다 — 위의 명중 실패·회복량 0·이기심 거절은
            //   모두 이 줄에 <b>도달하지 못한다</b>. 그래서 «성공한 회복» 만 세어진다.
            //   ⚠ 규칙과 수치는 전부 <see cref="HeroAwakeningService"/> 에 있다 — 여기서는
            //     «일어났다» 만 알린다(처치가 OnAnyDamaged/OnAnyDied 로 가는 것과 같은 결).
            if (_self is Units.CharacterUnit healerUnit)
                HeroAwakeningService.Instance?.NotifyHeal(healerUnit);

            // ★ 회복 연출 (2026-08-19) — 회복이 <b>실제로 들어간 뒤</b>에만 깐다.
            //   거절(이기심)·회복량 0 에서 빠져나간 위 두 갈래보다 아래에 있는 것이 요점이다:
            //   시전만 하고 아무 일도 안 났는데 초록 십자가가 뜨면 회복된 것처럼 보인다.
            //   스킨에 healFxFrames 가 없으면 아무것도 안 한다(예전 동작 그대로).
            CombatProjectileFx.PlayHeal(_self, _target);
        }

        void AdvanceToObjective(float dt)
        {
            // 플로우 필드가 있으면 벽을 피해 성역으로 향한다.
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
            // ★ 밀림은 <b>가려던 방향을 이길 수 없다</b> (유저 리포트 2026-08-13:
            //   "캐릭터가 몬스터에게 끌려간다").
            //
            //   <see cref="Separation"/> 은 주변 유닛마다 힘을 <b>더하기</b> 때문에 크기 상한이
            //   없었다 — 몬스터 5마리가 붙으면 최대 5 × 1.4 = 7 이 되어 방향 벡터(길이 1)가
            //   완전히 묻힌다. 그러면 성역으로 진군하는 무리에 <b>그대로 휩쓸려 같이 흘러간다.</b>
            //   상한을 걸면 겹침 방지는 그대로 되면서 진행 방향이 항상 주도권을 갖는다.
            //   ★ <b>벽에서 밀리는 힘도 같은 상한 아래에 넣는다</b>(2026-08-19,
            //     <see cref="WallClearance"/>) — 두 힘을 따로 상한 걸면 합이 1 을 넘어
            //     위 규칙("밀림은 가려던 방향을 이길 수 없다")이 깨진다.
            float body = BodyRadiusOf(_self);
            Vector2 push = Vector2.ClampMagnitude(
                (Separation() + WallClearance(body)) * separationStrength,
                separationMaxInfluence);

            Vector2 move = direction + push;
            if (move.sqrMagnitude > 0.0001f) move.Normalize();

            FaceMovement(move);
            MoveWithCollision((Vector3)(move * CurrentSpeed() * dt));
        }

        float CurrentSpeed()
        {
            // 능력치(이동속도)가 있으면 그게 최우선. 몬스터·포탑은 0 이라 기존 경로를 그대로 탄다.
            // ★ 이벤트 보정은 <b>어느 경로에나</b> 곱한다 — 안 걸렸으면 1 이라 동작이 같다.
            float mul = EventMoveSpeedMul;

            float statSpeed = _self != null ? _self.StatMoveSpeedTiles : 0f;
            if (statSpeed > 0f) return statSpeed * mul;

            return mul * (moveSpeedTiles > 0f
                ? moveSpeedTiles
                : (_self != null && _self.Balance != null ? _self.Balance.moveSpeedTilesPerSecond : 3f));
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

        /// <summary>
        /// 주변 유닛에서 밀어내는 힘. 겹쳐서 한 덩어리로 뭉치는 걸 막는다.
        ///
        /// ★★ <b>반경은 두 유닛의 몸집에서 나온다</b> (2026-08-19, 유저 리포트: *"고르도네 두
        /// 마리가 겹쳐져서 움직이지 않는다"*). 예전에는 인스펙터의 <see cref="separationRadius"/>
        /// 하나(0.55타일)를 <b>몸집과 무관하게</b> 썼다. 그 값은 중립이 전부 작은 정적
        /// 스프라이트였던 시절의 값이라, 표에서 몸집을 받게 된 뒤로는 어긋난다:
        /// <code>
        ///   고르도네(1004) 콜라이더 2.6 x 1.9 → BodyRadiusTiles = min(2.6,1.9)/2 ≒ 0.95
        ///   그런데 밀림은 0.55 타일에서 멈춘다 → 두 마리가 <b>그림상 거의 완전히 겹친다</b>
        ///   에픽(11 x 7.5)은 차이가 더 크다 — 반경 3.7 짜리 둘이 0.55 에서 멈춘다
        /// </code>
        /// 그래서 <b>내 몸집 + 상대 몸집</b>(둘 중 몸집을 모르는 쪽은 0)과 인스펙터 값 중
        /// <b>큰 쪽</b>을 쓴다. 몸집 판정은 <see cref="TargetRadius"/>(근접 유닛이 어디까지
        /// 다가가는지)와 <b>같은 값</b>을 읽으므로, "붙어서 때리는 거리" 와 "밀어내는 거리" 가
        /// 서로 싸우지 않는다.
        ///
        /// ⚠ 힘의 <b>상한</b>은 그대로다(<see cref="separationMaxInfluence"/>) — 반경만 넓혔고
        ///   세기는 안 건드렸으므로 "밀림은 가려던 방향을 이길 수 없다" 는 규칙이 유지된다.
        /// </summary>
        Vector2 Separation()
        {
            // ⚠ <b>0 = 겹침 허용</b> 이라는 인스펙터의 뜻은 그대로 지킨다 — 몸집을 보게 됐다고
            //   해서 이 스위치를 무력화하면, 일부러 끈 유닛(씬의 Tower_Template)이 조용히
            //   다시 밀어내기 시작한다.
            if (separationRadius <= 0f) return Vector2.zero;

            Vector2 push = Vector2.zero;
            var all = UnitRegistry.All;
            Vector3 myPos = transform.position;
            float myBody = BodyRadiusOf(_self);

            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit other = all[i];
                if (other == null || other == _self || !other.IsAlive) continue;
                if (other.Kind == UnitKind.Nexus || other.Kind == UnitKind.Tower) continue;

                float want = Mathf.Max(separationRadius, myBody + BodyRadiusOf(other));
                if (want <= 0f) continue;

                Vector2 d = myPos - other.transform.position;
                float sqr = d.sqrMagnitude;
                if (sqr > want * want) continue;

                // ★ <b>정확히 겹친 경우</b> — 예전에는 여기서 <c>continue</c> 했다. 방향을 정할
                //   수 없다는 이유였지만, 그러면 <b>완전히 겹친 두 마리는 영영 안 떨어진다</b>
                //   (밀림이 0 이므로 서로를 밀어낼 근거가 사라진다). 겹침을 푸는 것이 이 함수의
                //   목적이므로, 방향을 못 정할 때는 <b>정해준다</b> — instanceId 비교로 갈라서
                //   두 유닛이 <b>반드시 서로 반대쪽</b>으로 밀리게 한다(같은 쪽으로 밀면 겹친
                //   채로 함께 이동한다).
                if (sqr < CoincidentEpsilon * CoincidentEpsilon)
                {
                    float sign = _self.GetInstanceID() > other.GetInstanceID() ? 1f : -1f;
                    push += new Vector2(sign, 0f);
                    continue;
                }

                // 가까울수록 강하게 밀어낸다
                push += d.normalized * (1f - Mathf.Sqrt(sqr) / want);
            }
            return push;
        }

        /// <summary>
        /// 밀림 계산에 쓰는 <b>몸집 반경</b>(타일). 몸집을 모르는 유닛(캐릭터·정적 스프라이트
        /// 중립)은 0 이다.
        ///
        /// <b><see cref="TargetRadius"/> 와 같은 값을 읽지만 기본값 0.4 를 씌우지 않는다.</b>
        /// 그쪽은 "근접 유닛이 어디까지 다가가야 때릴 수 있는가" 라서 <b>모르면 0.4</b> 가
        /// 안전한 답이지만, 여기서는 <b>모른다</b>와 <b>반경 0.4 다</b>를 구분해야 한다 —
        /// 모르는 쪽에 0.4 를 씌우면 캐릭터끼리의 밀림 반경이 인스펙터 값(0.55)에서
        /// 0.8 로 조용히 넓어져, 이번 수정과 무관한 캐릭터 진형이 같이 변한다.
        /// </summary>
        static float BodyRadiusOf(DamageableUnit unit)
        {
            if (unit is Units.MonsterUnit monster) return Mathf.Max(0f, monster.BodyRadiusTiles);
            if (unit is Units.NeutralMonsterUnit neutral) return Mathf.Max(0f, neutral.BodyRadiusTiles);
            return 0f;
        }

        /// <summary>
        /// 근처 <b>막힌 칸</b>에서 밀어내는 힘 — 몸집이 큰 유닛이 <b>벽 그림 안에 파묻히는</b>
        /// 것을 막는다 (2026-08-19, 유저 리포트: *"고르도네 두 마리가 벽에 낀다"*).
        ///
        /// <b>왜 필요한가</b> — 이동 충돌 판정(<see cref="IsBlocked"/>)은 <b>중심점 한 점</b>만
        /// 본다. 유닛에 <c>Collider2D</c> 가 없으니 그게 정본이긴 하지만, 표에서 몸집을 받게 된
        /// 뒤로는 그림과 판정이 갈라진다:
        /// <code>
        ///   고르도네 몸집 2.6 x 1.9 · 중심은 벽 바로 아래 칸 중심에 <b>합법적으로</b> 설 수 있다
        ///   벽은 2칸 높이로 그려져 있고(21·22절) 그 아래 한 칸까지 그림이 덮는다(IsWallSkirt)
        ///   → 중심이 규칙을 지켜도 <b>몸통 대부분이 벽 그림 뒤로 들어간다</b> = "벽에 끼었다"
        /// </code>
        ///
        /// ★★ <b>막는 것이 아니라 미는 것</b>이다. "몸집만큼 떨어진 칸만 갈 수 있다" 는 하드
        /// 판정으로 바꾸면 <b>몸집보다 좁은 통로가 통째로 막힌다</b> — 경로를 내는 A* 는
        /// 중심 기준(1칸)이라, 자기가 걸을 수 없는 길을 스스로 계획하는 유닛이 생기고
        /// 그건 지금 고치려는 것보다 <b>더 나쁜 끼임</b>이다. 밀림은
        /// <see cref="separationMaxInfluence"/> 상한 아래에 있어 <b>가려던 방향을 절대 못 이기며</b>,
        /// 통로 양쪽 벽에서 오는 힘은 서로 지워지므로 좁은 길도 그대로 지난다.
        ///
        /// ⚠ 몸집이 한 칸 안에 들어가는 유닛(<see cref="WallClearanceMinBody"/> 이하)은
        ///   <b>계산 자체를 건너뛴다</b> — 캐릭터·잡몹의 이동은 한 프레임도 달라지지 않는다.
        /// </summary>
        Vector2 WallClearance(float bodyRadius)
        {
            if (_mapGenerator == null || bodyRadius <= WallClearanceMinBody) return Vector2.zero;

            Vector3 pos = transform.position;
            Vector3Int center = _mapGenerator.WorldToCell(pos);
            int span = Mathf.CeilToInt(bodyRadius);

            Vector2 push = Vector2.zero;
            for (int dy = -span; dy <= span; dy++)
            {
                for (int dx = -span; dx <= span; dx++)
                {
                    if (dx == 0 && dy == 0) continue;   // 자기 칸은 EscapeIfEmbedded 의 몫이다

                    var cell = new Vector3Int(center.x + dx, center.y + dy, 0);
                    if (!_mapGenerator.IsCellBlocked(cell)) continue;

                    Vector3 cellCenter = _mapGenerator.CellCenterWorld(cell);

                    // ⚠ 거리는 <b>칸의 테두리</b>까지 잰다 — 칸 중심으로 재면 바로 옆칸 벽이
                    //   1.0타일로 잡혀서(실제 테두리는 0.5타일) 몸집이 1 미만인 유닛에게는
                    //   이 힘이 아예 안 걸린다. 미는 <b>방향</b>은 중심을 쓴다(테두리로 뽑으면
                    //   벽에 딱 붙었을 때 0 벡터가 나와 방향을 정할 수 없다).
                    float dist = DistanceToCell(pos, cellCenter);
                    if (dist >= bodyRadius) continue;

                    Vector2 away = (Vector2)(pos - cellCenter);
                    if (away.sqrMagnitude < 0.0001f) continue;

                    push += away.normalized * (1f - dist / bodyRadius);
                }
            }
            return push;
        }

        /// <summary>
        /// 한 점에서 <b>칸(1x1 정사각형)의 가장 가까운 지점</b>까지의 거리(타일).
        /// <see cref="Units.NeutralMonsterWander"/> 의 같은 이름 함수와 같은 계산이다 — 두 곳
        /// 모두 "몸통이 벽에 닿았는가" 를 묻고, 그 답은 칸 중심이 아니라 <b>테두리</b>로 갈린다
        /// (바로 옆칸 중심은 1.0타일이지만 그 칸의 이쪽 테두리는 0.5타일이다).
        /// </summary>
        static float DistanceToCell(Vector3 point, Vector3 cellCenter)
        {
            float dx = Mathf.Max(0f, Mathf.Abs(point.x - cellCenter.x) - 0.5f);
            float dy = Mathf.Max(0f, Mathf.Abs(point.y - cellCenter.y) - 0.5f);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>큰 유닛(성역 등)은 중심까지 갈 수 없으므로 반경을 더해준다.</summary>
        static float TargetRadius(DamageableUnit target)
        {
            if (target.Kind == UnitKind.Nexus)
            {
                // ⚠ 예전에는 <c>transform.localScale.x</c> 를 읽었다 — 그건 <b>스프라이트를 몇 배로
                //   그리는지</b>(픽셀 기준 배율)라서, 아트를 다시 임포트해 PPU 가 바뀌면 근접
                //   유닛이 성역에 파고들거나 허공을 때리게 된다. 점유 칸 수(타일)가 정본이다.
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

            // ★ 중립 몬스터도 같은 처리를 받는다 (2026-08-15).
            //   예전에는 중립이 전부 <b>작은 정적 스프라이트</b>라 기본값 0.4 로 충분했는데,
            //   에픽(카르시노스 1004)이 4.4 x 5.1 타일짜리 몸집으로 들어오면서
            //   위 주석이 경고한 그대로 <b>근접 캐릭터가 몸 한가운데까지 파고들려</b> 했다.
            if (target is Units.NeutralMonsterUnit neutral)
            {
                float body = neutral.BodyRadiusTiles;
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
