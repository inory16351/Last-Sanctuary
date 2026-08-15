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

        [Tooltip("★ 한 번에 움직이는 거리(타일). <b>재추첨 주기 안에 걸어서 닿을 수 있는 거리</b>여야 한다.\n" +
                 "중립 몬스터 이동 속도가 2.2~2.5타일/초, 재추첨이 4~10초이므로 8~25타일을 걸을 수 있다.\n\n" +
                 "⚠ 이 값이 없던 시절에는 <b>고리 전체에서 아무 점이나</b> 목표로 잡았는데, 그러면 " +
                 "닿기 전에 다음 목표가 뽑혀 <b>영영 도착하지 못하고 고리의 무게중심(=넥서스)으로 " +
                 "흘러간다</b>. 그것이 '중앙으로 모인다'의 진짜 원인이었다(진행상황 73-11절)")]
        [Min(1f)] [SerializeField] float wanderStepTiles = 12f;

        [Tooltip("최상위 종(위 단계가 없어 상한이 무한대)일 때, 최소거리에 더해 얼마나 더 " +
                 "바깥까지 배회 범위로 잡을지(타일). 무한대를 그대로 쓰면 거리 샘플링이 " +
                 "Infinity×0=NaN 이 돼 유닛 좌표가 깨진다 — 반드시 유한한 값으로 바꿔줘야 한다")]
        [Min(1f)] [SerializeField] float unboundedWanderRange = 60f;

        [Tooltip("★ 스포너가 정의의 `leashRangeTiles` 로 덮어쓴다 — 인스펙터 값은 폴백이다.\n\n" +
                 "<b>배회 가능 범위(고리)에서 이 거리까지는 적을 쫓아간다</b>(타일). " +
                 "그보다 멀어지면 <b>추격을 포기하고 고리로 복귀</b>한다(유저 확정 2026-08-13).\n" +
                 "복귀 중에는 이동 목적지가 고리로 고정되고 <b>사거리 안에 들어온 적만</b> 때린다 — " +
                 "쫓아가지는 않는다(`UnitCombat.SetRetreatFiring`).")]
        [Min(0f)] [SerializeField] float pursuitTiles = 6f;

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

        /// <summary>추격 한계를 넘어 고리로 돌아가는 중 (<see cref="UpdateReturnState"/>).</summary>
        bool _returning;

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

        /// <summary>
        /// 스포너가 스폰 직후 호출한다 — 이 개체가 등장할 수 있는 거리 구간(유클리드)과
        /// <b>고리 밖으로 쫓아갈 수 있는 거리</b>(표의 <c>leashRangeTiles</c>)를 넘겨준다.
        /// </summary>
        public void Init(float minRadius, float maxRadius, float pursuitRangeTiles)
        {
            EnsureReady();
            pursuitTiles = Mathf.Max(0f, pursuitRangeTiles);
            _minRadius = Mathf.Max(0f, minRadius);

            // 상한이 무한대(최상위 종)면 유한한 값으로 바꿔둔다 — Mathf.Lerp(min, Infinity, t) 는
            // t 가 0이어도 Infinity×0=NaN 이 나와 좌표가 깨진다(실제로 이 버그로 유닛이
            // NaN 위치로 날아가는 문제가 있었다).
            float boundedMax = float.IsPositiveInfinity(maxRadius)
                ? _minRadius + unboundedWanderRange
                : maxRadius;
            _maxRadius = Mathf.Max(_minRadius + 1f, boundedMax);

            _habitatMode = false;
            _initialized = true;
            PickDestination();
        }

        // ==================================================================
        // ★ 서식지 모드 (에픽 중립 몬스터) — 롤 정글 캠프 (유저 지시 2026-08-15)
        //
        // *"에픽 타입 중립 몬스터는 일정 타일 원형 범위의 서식지를 가지고 해당 서식지의
        //   중앙에서 대기 하다가 공격 받으면 서식지 범위 내 일정 타일 범위 밖까지 캐릭터들 추격"*
        //
        // <b>일반 모드와 무엇이 다른가 — 딱 두 가지다.</b>
        //   ① 기준 중심이 <b>넥서스 → 자기 스폰 지점</b>으로 바뀐다.
        //   ② 배회하지 않고 <b>중앙에서 대기</b>한다.
        // 나머지(고리 밖 판정 · 복귀 · 후퇴 사격)는 <b>완전히 같은 코드</b>를 쓴다 —
        // 서식지도 결국 "중심 + 반지름" 이라 고리(min=0, max=반지름)로 표현되기 때문이다.
        // 추격 한계도 같은 <see cref="pursuitTiles"/> 이고, 값만 표가 아니라 정의 SO 에서 온다.
        //
        // ⚠ 중심을 <b>스폰 지점으로 고정</b>하는 것이 핵심이다. 넥서스 기준으로 두면 맵
        //   반대편의 에픽 둘이 같은 고리를 공유해 "자기 자리" 라는 개념이 생기지 않는다.
        // ==================================================================

        /// <summary>서식지 모드인가. 켜지면 중심이 넥서스가 아니라 <see cref="_habitatCenter"/> 다.</summary>
        bool _habitatMode;
        Vector3 _habitatCenter;

        /// <summary>서식지 중앙에서 이 거리 안이면 "제자리" 로 보고 더 움직이지 않는다.</summary>
        float _habitatIdleSlack = 1f;

        /// <summary>
        /// 에픽 개체를 <b>자기 서식지</b>에 묶는다. 스포너가 스폰 직후 호출한다.
        /// </summary>
        /// <param name="center">서식지 중심 — 이 개체가 태어난 자리.</param>
        /// <param name="radiusTiles">서식지 반지름(타일).</param>
        /// <param name="chaseTiles">서식지 <b>경계에서</b> 이만큼 더 나갈 때까지만 쫓는다.</param>
        /// <param name="idleSlackTiles">중앙에서 이 거리 안이면 제자리로 본다.</param>
        public void InitHabitat(Vector3 center, float radiusTiles, float chaseTiles,
                                float idleSlackTiles)
        {
            EnsureReady();

            _habitatMode = true;
            _habitatCenter = center;
            _habitatIdleSlack = Mathf.Max(0f, idleSlackTiles);

            pursuitTiles = Mathf.Max(0f, chaseTiles);
            _minRadius = 0f;
            _maxRadius = Mathf.Max(1f, radiusTiles);

            _initialized = true;
            _destination = center;
            _combat.SetHome(center);
        }

        /// <summary>
        /// 고리·복귀 판정의 <b>중심</b>. 서식지 모드면 스폰 지점, 아니면 넥서스.
        /// 두 모드가 같은 판정 코드를 공유할 수 있게 하는 한 지점이다.
        /// </summary>
        Vector3 RingCenter() => _habitatMode ? _habitatCenter : NexusPosition();

        void Update()
        {
            if (!_initialized) return;
            EnsureReady();

            UpdateReturnState();

            // 복귀 중에는 교전보다 복귀가 우선이다 — 목적지를 계속 고리 쪽으로 다시 잡아준다.
            // (이동·공격 처리는 UnitCombat 의 후퇴 사격이 맡는다 — 쫓아가지 않고 사거리 안만 때린다)
            if (_returning)
            {
                bool back = Vector2.Distance(transform.position, _destination) <= arriveDistance;
                if (back || _combat.DestinationUnreachable || Time.time >= _repickTime)
                    PickReturnDestination();
                return;
            }

            // 교전 중(선공 개체가 캐릭터를 쫓는 중)이면 손대지 않는다 — 전투가 항상 우선이다.
            if (_combat.Target != null && _combat.Target.IsAlive) return;

            bool arrived = Vector2.Distance(transform.position, _destination) <= arriveDistance;
            if (arrived || _combat.DestinationUnreachable || Time.time >= _repickTime)
                PickDestination();
        }

        /// <summary>
        /// ★ <b>추격 한계 — 배회 가능 범위(고리) 기준</b> (유저 확정 2026-08-13:
        /// *"추적 범위까진 쫓아가고, 배회 가능 범위에서 추적 타일 거리까지 멀어지고 나면
        /// 추격 포기하고 다시 배회 가능 범위로 복귀"*).
        ///
        /// <b>왜 목줄(<c>leashRange</c>)만으로는 안 되나</b> — <c>UnitCombat</c> 의 목줄은
        /// <b>귀환 지점 기준</b>이고, 반격 경로(<c>FindRetaliationTarget</c>)는 <b>목줄을 아예 안 본다.</b>
        /// 게다가 그 판정 기준(<c>retaliateChaseRange</c>)이 <b>자기 위치</b>라, 맞으면서 끌려가면
        /// 판정 범위도 같이 따라와 <b>얼마든지 멀리 끌려간다</b>(래칫). 그래서 한계를
        /// <b>움직이지 않는 기준(넥서스 중심 고리)</b> 으로 다시 잡는다.
        ///
        /// 복귀는 <c>SetRetreatFiring</c> 으로 한다 — 전투를 통째로 끄는
        /// <c>SetCombatSuppressed</c> 와 달리 <b>이동 목적지만 고리로 고정</b>하고
        /// <b>사거리 안에 들어온 적은 그대로 때린다.</b> "추격만 포기" 라는 지시에 정확히 맞고,
        /// 돌아가는 동안 무저항으로 맞기만 하는 상태도 안 된다.
        /// </summary>
        void UpdateReturnState()
        {
            float outside = DistanceOutsideRing();

            if (_returning)
            {
                // 고리 안으로 완전히 돌아왔을 때만 푼다 — 경계에서 켜졌다 꺼졌다 하지 않게.
                if (outside > 0f) return;
                _returning = false;
                _combat.SetRetreatFiring(false);
                _repickTime = 0f;
                return;
            }

            if (outside <= pursuitTiles) return;

            _returning = true;
            _combat.SetRetreatFiring(true);
            PickReturnDestination();
        }

        /// <summary>
        /// 지금 위치가 고리(<see cref="_minRadius"/>~<see cref="_maxRadius"/>) 밖으로 얼마나
        /// 벗어났는지(타일). 안이면 0.
        ///
        /// ★ <b>일반 배회는 정사각 고리</b>다(2026-08-16, 유저 확정) — 스폰 판정과 <b>같은
        /// 거리 함수</b>를 써야 "소환 가능한 범위 안에서만 배회한다"(73-5절 유저 지시)가
        /// 성립한다. 둘이 어긋나면 개체가 스폰 범위 밖으로 걸어 나간다.
        /// <b>서식지 모드(에픽)는 원형 그대로</b>다 — 자기 자리를 중심으로 한 원이 정의다.
        /// </summary>
        float DistanceOutsideRing()
        {
            Vector2 v = (Vector2)(transform.position - RingCenter());
            float d = _habitatMode
                ? v.magnitude                                        // 서식지 = 원
                : Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y));         // 배회 고리 = 정사각형

            if (d < _minRadius) return _minRadius - d;
            if (d > _maxRadius) return d - _maxRadius;
            return 0f;
        }

        /// <summary>
        /// 복귀 목적지 — 지금 각도 그대로 <b>가장 가까운 고리 경계</b>.
        ///
        /// ★ 서식지 모드는 경계가 아니라 <b>중앙</b>으로 돌아간다 — 정의문이 "서식지의
        /// 중앙에서 대기" 이므로, 경계에 멈춰 서면 다음에 또 그 자리에서 시작하게 된다.
        /// </summary>
        void PickReturnDestination()
        {
            if (_habitatMode)
            {
                _destination = _habitatCenter;
                _repickTime = Time.time +
                              Mathf.Lerp(repositionDelay.x, repositionDelay.y, (float)_rng.NextDouble());
                _combat.SetHome(_destination);
                return;
            }

            Vector3 nexus = RingCenter();
            _destination = ClampToRing(transform.position, nexus);
            _repickTime = Time.time + Mathf.Lerp(repositionDelay.x, repositionDelay.y, (float)_rng.NextDouble());
            _combat.SetHome(_destination);
        }

        /// <summary>
        /// 다음 배회 지점을 고른다 — <b>지금 자리에서 한 걸음</b>(<see cref="wanderStepTiles"/>)이고,
        /// 그 걸음이 고리를 벗어나면 <b>반지름만 고리 안으로 접어 넣는다</b>(각도는 유지).
        ///
        /// ★★ <b>왜 "고리 안의 아무 점"이 아니라 "한 걸음"인가</b> (2026-08-13 재수정, 유저 리포트
        /// "또 중앙으로 모여들고 있음") — 예전에는 목표를 <b>고리 전체에서 균일하게</b> 뽑았다.
        /// 분포 자체는 옳았지만 <b>제약이 목표에만 걸리고 위치에는 안 걸린다</b>는 것이 함정이다:
        ///
        /// <code>
        /// 이동 속도 2.2~2.5타일/초 · 재추첨 4~10초 → 한 주기에 8~25타일밖에 못 간다
        /// 그런데 1003 의 고리는 반지름 100~160 — 반대편 목표까지는 200타일이 넘는다
        /// → <b>도착하기 전에 늘 새 목표가 뽑힌다.</b> 매번 "고리 위 무작위 점" 방향으로
        ///   조금씩 걷는 것은 곧 <b>고리의 무게중심(=넥서스)으로 가는 랜덤워크</b>다
        /// </code>
        ///
        /// 실제로 시뮬레이션하면 고리 100~160 짜리 개체가 <b>넥서스에서 18~70타일</b>까지
        /// 흘러들어온다. 71-3절이 고친 것은 <b>표본 분포</b>였고, 이건 그 위에 남아 있던
        /// <b>다른</b> 원인이다 — 같은 증상이라 같은 버그로 보였다.
        ///
        /// 이제 목표가 항상 <b>한 주기 안에 닿을 거리</b>이므로 개체는 실제로 도착하고,
        /// 도착 지점은 언제나 고리 안이다 → <b>위치 자체가 고리에 갇힌다.</b>
        /// 교전에 끌려나가 고리 밖으로 밀려나도, 다음 추첨에서 반지름이 고리로 접히므로
        /// <b>스스로 걸어서 돌아온다.</b>
        /// </summary>
        void PickDestination()
        {
            Vector3 nexus = RingCenter();

            // ★ 서식지 모드는 <b>배회하지 않는다</b> — "서식지의 중앙에서 대기" 가 정의문이다.
            //   이미 중앙 근처면 목적지를 그대로 두어 제자리에 서 있게 하고, 밀려났으면
            //   중앙으로 돌아간다. (여유를 두는 이유: 정확히 한 점을 목적지로 주면
            //   도착 판정이 걸리지 않아 미세하게 계속 꼼지락거린다)
            if (_habitatMode)
            {
                bool atCenter = Vector2.Distance(transform.position, _habitatCenter) <= _habitatIdleSlack;
                _destination = atCenter ? transform.position : _habitatCenter;

                _repickTime = Time.time +
                              Mathf.Lerp(repositionDelay.x, repositionDelay.y, (float)_rng.NextDouble());
                _combat.SetHome(_habitatCenter);
                return;
            }

            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                float angle = (float)(_rng.NextDouble() * System.Math.PI * 2.0);
                float step = Mathf.Lerp(wanderStepTiles * 0.4f, wanderStepTiles, (float)_rng.NextDouble());

                Vector3 candidate = transform.position +
                                    new Vector3(Mathf.Cos(angle) * step, Mathf.Sin(angle) * step, 0f);
                candidate = ClampToRing(candidate, nexus);

                if (!IsWalkable(candidate)) continue;

                _destination = candidate;
                break;
            }

            _repickTime = Time.time + Mathf.Lerp(repositionDelay.x, repositionDelay.y, (float)_rng.NextDouble());
            _combat.SetHome(_destination);
        }

        /// <summary>
        /// 그 지점을 <b>넥서스 기준 고리 안</b>으로 접어 넣는다 — 방향은 그대로 두고
        /// <b>거리만</b> [min, max] 로 자른다.
        ///
        /// 개체가 고리 밖(교전에 끌려나갔거나 스폰 직후)에 있으면 이 함수가 돌려주는 지점은
        /// <b>고리 경계</b>가 되므로, 그쪽으로 걸어가는 것이 곧 <b>복귀</b>가 된다.
        ///
        /// ★ <b>정사각 고리</b>(2026-08-16) — 자르는 축이 원형과 다르다.
        /// 원형은 반지름 하나를 잘랐지만, 정사각형은 <b>max(|x|,|y|) 가 [min,max] 에 들어오도록
        /// x·y 를 같은 비율로</b> 줄이거나 늘린다. 그러면 방향(넥서스에서 본 각도)이 유지된 채
        /// 정사각 테두리 위로 정확히 옮겨진다.
        /// <b>서식지 모드(에픽)는 원형</b>이라 예전 계산을 그대로 쓴다.
        /// </summary>
        Vector3 ClampToRing(Vector3 world, Vector3 nexus)
        {
            Vector2 v = (Vector2)(world - nexus);

            float d = _habitatMode
                ? v.magnitude
                : Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y));

            // 정확히 중심 위면 방향을 정할 수 없다 — 임의의 방향으로 밀어낸다.
            if (d < 0.0001f) { v = Vector2.up; d = 1f; }

            float clamped = Mathf.Clamp(d, _minRadius, _maxRadius);
            if (Mathf.Approximately(clamped, d)) return world;

            // 두 좌표를 같은 비율로 — 원형이든 정사각형이든 이 한 줄로 경계에 놓인다.
            return nexus + (Vector3)(v * (clamped / d));
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
