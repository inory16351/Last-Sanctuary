using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Units
{
    /// <summary>
    /// <b>부대별 에픽 몬스터 토벌 명령</b>을 들고 있는 장부 (2026-08-15, 유저 지시).
    ///
    /// <i>"각 부대에 탐험 시 발견한 에픽 몬스터를 선택해 토벌할 수 있는 ui 추가"</i>
    ///
    /// ★ <b>이 서비스는 아무도 움직이지 않는다.</b> "어느 부대가 누구를 잡으러 가는가" 만
    /// 기억하고, 실제 이동·교전은 <see cref="CharacterBehavior.TickSubjugation"/> 이
    /// 매 프레임 이 장부를 읽어서 한다. 집결지(<c>RallyPointService</c>)가 좌표만 들고
    /// 이동은 <c>CharacterBehavior</c> 가 하는 것과 <b>같은 구조</b>다 — 이동 로직이 두 벌로
    /// 갈리지 않게 하려는 것(UI-1 절이 집결지에서 내린 결론과 같다).
    ///
    /// ★ <b>"탐험 시 발견한"의 실체</b> — 안개가 걷힌 자리에 있는 에픽만 목록에 올린다
    /// (<see cref="Discovered"/>). 한 번 본 개체는 <b>안개가 다시 덮여도 목록에 남는다</b> —
    /// 이 게임의 안개는 한 번 밝히면 지형이 기억되는 방식이고(12절), "발견했다"는 사실도
    /// 같은 성질이어야 유저가 목록에서 대상을 잃지 않는다.
    ///
    /// ★★ <b>발견은 「개체」가 아니라 「종」으로 기억한다</b> (2026-08-20, 유저 리포트).
    /// -----------------------------------------------------------------------
    /// <i>"한 번 잡고 재생성 됐을 때 전장의 안개는 밝혀진 상황이지만 … 시야가 없으면
    /// 토벌 지시에 에픽 몬스터 UI가 뜨지않아 기능이 작동하지 않아"</i>
    ///
    /// 원인은 <see cref="_discovered"/> 가 <b>유닛 인스턴스</b>를 들고 있었다는 것이다:
    /// <code>
    ///   ① 에픽을 잡는다        → PruneDead 가 목록에서 지운다
    ///   ② 재생성(respawnSeconds 600) → <b>완전히 새 GameObject</b> 다
    ///   ③ 그 자리에 캐릭터가 없다   → 안개 판정이 false → <b>다시는 목록에 안 올라온다</b>
    /// </code>
    /// 안개는 이미 걷혀 있는데도 «시야» 가 없어서 못 찾는 상태가 영구히 이어졌다.
    ///
    /// 그래서 <b>종 번호</b>(<see cref="NeutralMonsterDefinitionSO.monId"/>)를
    /// <see cref="_knownSpecies"/> 에 남긴다. 한 번 본 종은 재생성돼도 <b>보자마자가 아니라
    /// 태어나자마자</b> 목록에 오른다. 「서식지는 고정이고(99-9절) 그 자리를 이미 안다」는
    /// 것이 이 게임의 전제이므로, 같은 종이 같은 자리에 다시 나오는 것을 플레이어가
    /// «모른다» 고 볼 이유가 없다.
    ///
    /// ⚠ <b>종 기억은 세이브에 남는다</b> — 안 남기면 불러오기 한 번에 «처음 보는 종» 으로
    ///   되돌아가 같은 버그가 재현된다(개체 번호만 저장하던 것이 정확히 그랬다).
    ///
    /// ★★★ <b>2026-08-21 — 안개 판정을 «가 본 자리» 로 고쳤다</b> (유저 리포트:
    /// *"폴리르만 안뜨고 있어"*).
    /// -----------------------------------------------------------------------
    /// 위 ★★ 의 «종 기억» 은 증상을 <b>가리고 있었을 뿐</b>이고, 진짜 원인은 판정이
    /// <c>IsVisibleWorld</c>(«<b>지금</b> 보이는가»)였다는 것이다 — 바로 위 ★ 이 적어 둔
    /// «안개가 <b>걷힌</b> 자리» 와 다른 질문이다.
    ///
    /// 옛 종(카르시노스·아니사킬·바리올라)은 맵을 밝히던 시절에 시야에 들어와 기억에
    /// 남았으므로 그 갈래를 타고 잘 떴다. <b>나중에 추가된 폴리르</b>는 기억이 없고,
    /// 태어나는 자리는 이미 안개가 걷혀 아무도 보고 있지 않다 → 영구히 목록 밖.
    ///
    /// → <see cref="Fog.FogOfWarService.IsExploredWorld"/> 로 바꿨다. 이제 <b>종을 가리지
    ///   않고</b> 규칙이 성립하므로, 에픽이 또 늘어도 같은 구멍이 생기지 않는다.
    ///   («종 기억» 은 여전히 «안 가 본 곳에서 다시 태어난» 개체를 잡아 주므로 남긴다.)
    ///
    /// ⚠ <b>명령은 부대 단위</b>다. 부대에 속하지 않은 캐릭터는 토벌 명령을 받지 않는다 —
    /// 유저 지시가 "각 부대에" 이고, 개인 단위로 열면 지시 대상이 두 갈래가 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class EpicSubjugationService : MonoBehaviour
    {
        public static EpicSubjugationService Instance { get; private set; }

        [Header("판정")]
        [Tooltip("이 거리(타일) 안까지 다가가면 <b>사냥 타겟</b>으로 넘긴다. 그 밖에서는 " +
                 "목적지만 잡고 걸어간다.\n" +
                 "⚠ 처음부터 사냥 타겟으로 주면 UnitCombat 의 추격 한계(huntPursuitTiles)에 " +
                 "걸려 <b>출발하자마자 포기</b>한다 — 그 한계는 '우연히 마주친 사냥감을 " +
                 "얼마나 쫓을지'라서 명시적인 토벌 명령과는 뜻이 다르다")]
        [Min(1f)] [SerializeField] float engageRangeTiles = 10f;

        [Tooltip("발견 목록을 다시 훑는 주기(초). 매 프레임 볼 필요가 없는 값이다")]
        [Min(0.05f)] [SerializeField] float discoveryInterval = 0.5f;

        [Tooltip("★ 한 마리에게 동시에 보낼 수 있는 부대 수 (2026-08-25 · 유저 지시:\n" +
                 "«한 몬스터 토벌은 최대 두개의 부대까지 설정 가능하게»).\n" +
                 "1 로 두면 예전 동작(한 마리에 한 부대)이다")]
        [Min(1)] [SerializeField] int maxSquadsPerTarget = 2;

        [Header("디버그")]
        [SerializeField] bool logOrders = true;

        /// <summary>부대 id → 토벌 대상.</summary>
        readonly Dictionary<int, NeutralMonsterUnit> _orders = new Dictionary<int, NeutralMonsterUnit>();

        /// <summary>한 번이라도 시야에 들어온 에픽. 안개가 다시 덮여도 남는다(클래스 주석 참조).</summary>
        readonly List<NeutralMonsterUnit> _discovered = new List<NeutralMonsterUnit>();

        /// <summary>
        /// 한 번이라도 발견한 <b>종</b>의 번호. 개체가 죽어도 <b>지우지 않는다</b> —
        /// 이게 «재생성돼도 토벌 목록에 남는다» 를 만드는 유일한 상태다(클래스 주석 ★★).
        /// </summary>
        readonly HashSet<int> _knownSpecies = new HashSet<int>();

        Fog.FogOfWarService _fog;
        float _nextScan;

        /// <summary>지금까지 발견한, <b>살아있는</b> 에픽 몬스터 목록. UI 가 이걸 그린다.</summary>
        public IReadOnlyList<NeutralMonsterUnit> Discovered => _discovered;

        /// <summary>명령·발견 목록이 바뀔 때마다 발생 — UI 가 다시 그린다.</summary>
        public event System.Action OnChanged;

        /// <summary>이 거리 안이면 사냥 타겟으로 넘긴다(타일).</summary>
        public float EngageRangeTiles => engageRangeTiles;

        /// <summary>한 마리에게 동시에 보낼 수 있는 부대 수 (2026-08-25).</summary>
        public int MaxSquadsPerTarget => Mathf.Max(1, maxSquadsPerTarget);

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Time.time < _nextScan) return;
            _nextScan = Time.time + discoveryInterval;

            bool changed = PruneDead();
            changed |= ScanForNewlySeen();
            if (changed) OnChanged?.Invoke();
        }

        // ------------------------------------------------------------------
        // 발견
        // ------------------------------------------------------------------

        bool ScanForNewlySeen()
        {
            if (_fog == null) _fog = FindAnyObjectByType<Fog.FogOfWarService>();

            bool changed = false;
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is not NeutralMonsterUnit n) continue;
                if (!n.IsAlive) continue;
                if (n.Definition == null || !n.Definition.epic) continue;
                if (_discovered.Contains(n)) continue;

                // ★★ <b>이미 아는 종이면 시야를 보지 않는다</b>(클래스 주석 ★★) —
                //    잡아서 사라졌다가 다시 태어난 개체가 이 갈래로 돌아온다.
                int speciesId = n.Definition.monId;
                bool known = _knownSpecies.Contains(speciesId);

                // ★★ <b>«가 본 자리» 로 판정한다</b> (2026-08-21 · 유저 리포트:
                //    *"다른 중립 에픽 몬스터들은 … 안개가 밝혀져서 토벌 지시 목록에 뜨는데
                //    폴리르만 안뜨고 있어"*).
                //
                //    ⚠⚠ 예전에는 <c>IsVisibleWorld</c>(«<b>지금</b> 누군가의 시야 안인가»)를
                //      봤다. 그래서 이 클래스가 스스로 적어 둔 규칙(«안개가 <b>걷힌</b> 자리에
                //      있는 에픽만 목록에 올린다»)과 <b>어긋나 있었다</b>.
                //
                //    <b>그런데 왜 폴리르만 문제였나</b> — 위의 «아는 종» 갈래가 그 어긋남을
                //    가려 주고 있었다. 카르시노스·아니사킬·바리올라는 <b>맵을 밝히던 시절에
                //    한 번 눈에 들어와</b> _knownSpecies 에 들어갔고, 그 뒤로는 시야 검사를
                //    건너뛴다. 폴리르는 <b>나중에 추가된 종</b>이라 그 기억이 없고, 태어나는
                //    자리(성역에서 100~160칸)는 이미 안개가 걷혀 «아무도 보고 있지 않는»
                //    곳이다 → 영원히 이 줄에서 걸렸다.
                //
                //    ★ <c>IsExploredWorld</c> 로 바꾸면 <b>종을 가리지 않고</b> 맞는다 —
                //      나중에 에픽이 또 늘어도 같은 구멍이 다시 생기지 않는다.
                //    ★ <see cref="_knownSpecies"/> 는 <b>남긴다</b> — «아직 안 가 본 곳에서
                //      다시 태어난» 개체를 여전히 잡아 주고, 저장 호환도 지킨다.
                //
                // 안개 서비스가 없으면(테스트 씬) 전부 보이는 것으로 친다.
                if (!known && _fog != null && !AnyPartExplored(n)) continue;

                _discovered.Add(n);
                _knownSpecies.Add(speciesId);
                changed = true;

                // ⚠ <b>처음 보는 종만</b> 알린다 — 재생성마다 «발견!» 이 뜨면 600초마다
                //   같은 줄이 로그를 채운다(재발견은 새 정보가 아니다).
                if (known) continue;

                if (logOrders)
                    Debug.Log($"[토벌] 에픽 몬스터 발견 — {n.DisplayName}" +
                              (string.IsNullOrWhiteSpace(n.Title) ? "" : $" ({n.Title})"), n);
                UI.HudLog.Add($"에픽 몬스터 발견 — {n.DisplayName}", UI.HudLogKind.Warn);
            }
            return changed;
        }

        /// <summary>
        /// ★★ <b>몸의 일부만 밝혀져 있어도 발견으로 본다</b> (2026-08-24 · 유저 지시:
        /// *"에픽 몬스터의 신체의 일부만 보여도 토벌 가능하게 해"*).
        ///
        /// <b>무엇이 문제였나</b> — 판정이 <c>IsExploredWorld(transform.position)</c>,
        /// 즉 <b>중심점 한 점</b>이었다. 에픽은 <b>7.5 x 11 타일</b>(표 <c>collider_*_tiles</c>)이라
        /// 몸이 절반 넘게 밝혀진 자리에 서 있어도 <b>그 한 점이 안개면</b> 목록에 오르지 않는다.
        /// ★ 이것은 94-1절에서 «거리는 몸집을 더해 재는데 시야는 중심점 한 점만 봤다» 로
        ///   고쳤던 것과 <b>같은 성질의 실수</b>다 — 큰 몸집을 점으로 다루면 반드시 어긋난다.
        ///
        /// <b>무엇을 「신체」로 보는가</b> — <see cref="SpriteRenderer.bounds"/>(월드 AABB)다.
        /// ★ 콜라이더 크기(<c>ColliderSizeTiles</c>)가 아니라 <b>실제로 그려진 것</b>을 쓴다 —
        ///   유저가 화면에서 보는 것이 그것이고, 피벗이 발밑인지 가운데인지(116-5절)를
        ///   <b>따지지 않아도 맞는다</b>. 그림이 없으면 콜라이더, 그것도 없으면 중심점으로 내려온다.
        ///
        /// 훑는 방법은 <b>가로·세로 각 5칸의 격자 + 중심</b>이다. 한 점이라도 밝혀져 있으면 발견이다.
        /// ⚠ 촘촘히 훑을 필요가 없다 — 안개 칸이 타일 단위이고 에픽 몸집이 7타일이 넘으므로
        ///   5x5 면 <b>칸을 건너뛰지 않는다</b>. 이 함수는 <c>rescanInterval</c> 주기로만 돈다.
        /// </summary>
        bool AnyPartExplored(NeutralMonsterUnit n)
        {
            if (_fog == null) return true;

            Vector3 c = n.transform.position;
            if (_fog.IsExploredWorld(c)) return true;      // 대개 여기서 끝난다

            // ── 「신체」의 월드 범위 ────────────────────────────────────
            Vector2 half;
            var sr = n.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                Bounds b = sr.bounds;
                c = b.center;                               // ★ 그림의 가운데 (피벗이 아니다)
                half = new Vector2(b.extents.x, b.extents.y);
            }
            else
            {
                float r = n.BodyRadiusTiles;
                if (r <= 0.01f) return false;               // 크기를 모르면 중심점 판정 그대로
                half = new Vector2(r, r);
            }

            if (half.x < 0.01f || half.y < 0.01f) return false;

            const int Steps = 5;                            // 5 x 5 격자
            for (int iy = 0; iy < Steps; iy++)
            {
                float ty = Steps == 1 ? 0f : (iy / (float)(Steps - 1)) * 2f - 1f;   // -1 ~ +1
                for (int ix = 0; ix < Steps; ix++)
                {
                    float tx = Steps == 1 ? 0f : (ix / (float)(Steps - 1)) * 2f - 1f;
                    var p = new Vector3(c.x + tx * half.x, c.y + ty * half.y, c.z);
                    if (_fog.IsExploredWorld(p)) return true;
                }
            }
            return false;
        }

        /// <summary>죽었거나 사라진 대상을 목록과 명령에서 지운다.</summary>
        bool PruneDead()
        {
            bool changed = false;

            for (int i = _discovered.Count - 1; i >= 0; i--)
                if (_discovered[i] == null || !_discovered[i].IsAlive)
                {
                    _discovered.RemoveAt(i);
                    changed = true;
                }

            _finished.Clear();
            foreach (var kv in _orders)
                if (kv.Value == null || !kv.Value.IsAlive) _finished.Add(kv.Key);

            for (int i = 0; i < _finished.Count; i++)
            {
                if (logOrders) Debug.Log($"[토벌] 부대 {_finished[i]} 의 토벌 대상이 사라져 명령을 해제합니다.", this);
                _orders.Remove(_finished[i]);
                changed = true;
            }
            return changed;
        }

        readonly List<int> _finished = new List<int>();

        // ------------------------------------------------------------------
        // 명령
        // ------------------------------------------------------------------

        /// <summary>
        /// ★★ <b>지금 이 대상에게 붙어 있는 부대 수</b> (2026-08-25 · 유저 지시:
        /// *"한 몬스터 토벌은 최대 두개의 부대까지 설정 가능하게 기능 구현해"*).
        ///
        /// ★ <b>세는 쪽이 «장부» 인 이 클래스인 이유</b> — 명령의 정본이 <see cref="_orders"/>
        ///   하나뿐이라 여기서 세면 항상 맞는다. UI 가 자기 화면을 세면 «화면에 안 그려진 부대»
        ///   (스크롤 밖·갱신 전)를 빠뜨린다.
        /// </summary>
        public int SquadCountOn(NeutralMonsterUnit target)
        {
            if (target == null) return 0;

            int n = 0;
            foreach (var kv in _orders)
                if (ReferenceEquals(kv.Value, target)) n++;
            return n;
        }

        /// <summary>
        /// 이 부대가 저 대상에게 <b>지금 지시를 낼 수 있는가</b>.
        /// 이미 그 대상을 맡고 있으면 참(다시 눌러 해제하는 길을 막지 않는다).
        /// </summary>
        public bool CanOrder(int squadId, NeutralMonsterUnit target)
        {
            if (target == null) return true;                       // 해제는 언제나 된다
            if (ReferenceEquals(OrderOf(squadId), target)) return true;
            return SquadCountOn(target) < MaxSquadsPerTarget;
        }

        /// <summary>이 부대의 토벌 대상. 없으면 null.</summary>
        public NeutralMonsterUnit OrderOf(int squadId)
        {
            if (!_orders.TryGetValue(squadId, out NeutralMonsterUnit t)) return null;
            return t != null && t.IsAlive ? t : null;
        }

        /// <summary>
        /// 이 캐릭터가 지금 토벌해야 할 대상. 부대가 없거나 명령이 없으면 null —
        /// <see cref="CharacterBehavior"/> 가 매 프레임 이걸 물어본다.
        /// </summary>
        public NeutralMonsterUnit TargetFor(CharacterUnit unit)
        {
            if (unit == null) return null;

            SquadService squads = SquadService.Instance;
            if (squads == null) return null;

            int squadId = squads.SquadIdOf(unit);
            return squadId <= 0 ? null : OrderOf(squadId);
        }

        /// <summary>
        /// 부대에 토벌 명령을 내린다. <paramref name="target"/> 이 null 이면 <b>명령 해제</b>다.
        /// 같은 대상을 다시 지시하면 아무 일도 하지 않는다.
        ///
        /// ★★ <b>한 대상에 <see cref="MaxSquadsPerTarget"/> 부대까지</b> (2026-08-25).
        ///   넘치면 <b>거절하고 <c>false</c></b> 를 돌려준다 — 조용히 덮어쓰면 유저는
        ///   «눌렀는데 아무 일도 안 일어난다» 로 읽는다. 부르는 쪽(창)이 이유를 말한다.
        /// </summary>
        /// <returns>명령이 실제로 바뀌었으면 <c>true</c>.</returns>
        public bool SetOrder(int squadId, NeutralMonsterUnit target)
        {
            if (squadId <= 0) return false;

            NeutralMonsterUnit now = OrderOf(squadId);
            if (ReferenceEquals(now, target)) return false;

            SquadService squads = SquadService.Instance;
            string squadName = squads?.Find(squadId)?.Name ?? $"부대 {squadId}";

            // ★ 정원 검사 — 해제(target == null)는 통과시킨다.
            if (target != null && !CanOrder(squadId, target))
            {
                UI.HudLog.Add($"{target.DisplayName} 에는 이미 {MaxSquadsPerTarget}개 부대가 " +
                              "가 있습니다", UI.HudLogKind.Warn);
                if (logOrders)
                    Debug.Log($"[토벌] {squadName} → {target.DisplayName} 거절 " +
                              $"(정원 {MaxSquadsPerTarget})", this);
                return false;
            }

            if (target == null || !target.IsAlive)
            {
                _orders.Remove(squadId);
                UI.HudLog.Add($"{squadName} 토벌 명령 해제");
                if (logOrders) Debug.Log($"[토벌] {squadName} 명령 해제", this);
            }
            else
            {
                _orders[squadId] = target;
                UI.HudLog.Add($"{squadName} → {target.DisplayName} 토벌", UI.HudLogKind.Warn);
                if (logOrders) Debug.Log($"[토벌] {squadName} → {target.DisplayName}", this);
            }

            // 명령이 바뀌면 <b>물고 있던 사냥감을 즉시 놓는다</b> — 안 그러면 이전 대상과
            // 싸우던 부대원이 그 교전이 끝날 때까지 새 명령을 못 본다
            // (CharacterBehavior 는 교전 중에 목적지를 안 건드린다).
            ReleaseHunts(squadId);

            OnChanged?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------
        // 저장 복원 (2026-08-18 신설)
        //
        // ★★ <b>왜 저장해야 하는가 — 발견은 "지금 보이는가"로 판정되기 때문이다.</b>
        //   <see cref="ScanForNewlySeen"/> 는 <c>IsVisibleWorld</c>(지금 시야에 들어와 있는가)로
        //   판정하는데, 안개의 <b>밝힌 칸</b>은 저장·복원되지만 <b>지금 보이는 칸</b>은 유닛
        //   위치로 매 프레임 다시 계산되는 값이라 복원 직후엔 아무것도 안 보인다. 그래서
        //   저장하지 않으면 <b>불러온 순간 발견 목록이 비고</b>(근처에 캐릭터가 없으면 다시 가서
        //   봐야 한다), 그 목록에 걸어둔 <b>부대 토벌 지시도 같이 사라진다.</b>
        //
        //   클래스 주석의 원칙("한 번 본 개체는 안개가 다시 덮여도 목록에 남는다")이 저장을
        //   건너뛰면 <b>게임을 껐다 켜는 순간에만 깨지고 있었다.</b>
        //
        // ⚠ <b>개체는 <see cref="NeutralMonsterUnit.SpawnId"/> 로 가리킨다</b> — 표의 monId 는
        //   종을 가리키지 개체를 가리키지 않아서, 같은 종이 여럿이면 누구를 발견했는지 알 수 없다.
        //   번호를 다시 매기는 일은 스포너가 한다(<c>NeutralMonsterSpawner.RestoreNeutral</c>).
        // ------------------------------------------------------------------

        /// <summary>발견한 개체들의 <see cref="NeutralMonsterUnit.SpawnId"/> 를 담는다.</summary>
        /// <summary>
        /// 발견한 <b>종</b> 번호를 내보낸다 (2026-08-20). 개체 번호(<see cref="ExportDiscovered"/>)와
        /// <b>따로</b> 저장해야 한다 — 개체는 죽으면 사라지지만 «그 종을 안다» 는 사실은 남는다.
        /// </summary>
        public void ExportKnownSpecies(List<int> ids)
        {
            if (ids == null) return;
            foreach (int id in _knownSpecies) ids.Add(id);
        }

        /// <summary>발견한 종 번호를 되돌린다. <b>개체 복원보다 먼저</b> 불러도 상관없다.</summary>
        public void RestoreKnownSpecies(IReadOnlyList<int> ids)
        {
            _knownSpecies.Clear();
            if (ids == null) return;
            for (int i = 0; i < ids.Count; i++)
                if (ids[i] > 0) _knownSpecies.Add(ids[i]);
        }

        public void ExportDiscovered(List<int> spawnIds)
        {
            if (spawnIds == null) return;

            for (int i = 0; i < _discovered.Count; i++)
            {
                NeutralMonsterUnit unit = _discovered[i];
                if (unit == null || !unit.IsAlive || unit.SpawnId <= 0) continue;
                spawnIds.Add(unit.SpawnId);
            }
        }

        /// <summary>부대별 토벌 지시를 <b>두 목록에 짝지어</b> 담는다 (부대 id · 대상 개체 번호).</summary>
        public void ExportOrders(List<int> squadIds, List<int> targetSpawnIds)
        {
            if (squadIds == null || targetSpawnIds == null) return;

            foreach (var kv in _orders)
            {
                NeutralMonsterUnit target = kv.Value;
                if (target == null || !target.IsAlive || target.SpawnId <= 0) continue;

                squadIds.Add(kv.Key);
                targetSpawnIds.Add(target.SpawnId);
            }
        }

        /// <summary>
        /// 저장된 발견 목록과 토벌 지시를 되돌린다. 목록에 없는 개체(복원에 실패한 마리)는
        /// 조용히 건너뛴다.
        ///
        /// ⚠ <see cref="SetOrder"/> 를 쓰지 않는다 — 그쪽은 로그를 남기고 사냥 타겟을 놓는
        /// <b>지시가 바뀌었을 때의 처리</b>다. 복원은 "그때 그 상태였다"를 재현하는 것이라
        /// 화면에 아무 말도 하지 않아야 한다(다른 서비스의 <c>Restore*</c> 와 같은 규칙).
        /// </summary>
        public void RestoreState(IReadOnlyList<NeutralMonsterUnit> discovered,
                                 IReadOnlyList<int> orderSquadIds,
                                 IReadOnlyList<NeutralMonsterUnit> orderTargets)
        {
            _discovered.Clear();
            _orders.Clear();

            // ⚠ <b>_knownSpecies 는 여기서 비우지 않는다</b> — 그건 RestoreKnownSpecies 가
            //   따로 채운다. 여기서 지우면 옛 세이브(종 목록이 없는 판)를 불러올 때
            //   «처음 보는 종» 으로 되돌아가 버그가 재현된다.
            if (discovered != null)
                for (int i = 0; i < discovered.Count; i++)
                {
                    NeutralMonsterUnit unit = discovered[i];
                    if (unit == null || !unit.IsAlive || _discovered.Contains(unit)) continue;
                    _discovered.Add(unit);

                    // 개체로 복원된 것은 그 종도 아는 것으로 본다 — 옛 세이브(종 목록이
                    // 없던 판)를 불러와도 그 자리에서 종 기억이 살아난다.
                    if (unit.Definition != null) _knownSpecies.Add(unit.Definition.monId);
                }

            if (orderSquadIds != null && orderTargets != null)
            {
                int pairs = Mathf.Min(orderSquadIds.Count, orderTargets.Count);
                for (int i = 0; i < pairs; i++)
                {
                    NeutralMonsterUnit target = orderTargets[i];
                    if (orderSquadIds[i] <= 0 || target == null || !target.IsAlive) continue;
                    _orders[orderSquadIds[i]] = target;
                }
            }

            OnChanged?.Invoke();
        }

        /// <summary>이 부대원 전원의 사냥 타겟을 놓는다 (명령이 바뀔 때).</summary>
        void ReleaseHunts(int squadId)
        {
            SquadService squads = SquadService.Instance;
            SquadService.Squad squad = squads != null ? squads.Find(squadId) : null;
            if (squad == null) return;

            for (int i = 0; i < squad.Members.Count; i++)
            {
                CharacterUnit m = squad.Members[i];
                if (m == null || !m.IsAlive) continue;
                m.GetComponent<UnitCombat>()?.ClearHuntTarget();
            }
        }
    }
}
