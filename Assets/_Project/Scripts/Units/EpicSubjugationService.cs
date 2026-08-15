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

        [Header("디버그")]
        [SerializeField] bool logOrders = true;

        /// <summary>부대 id → 토벌 대상.</summary>
        readonly Dictionary<int, NeutralMonsterUnit> _orders = new Dictionary<int, NeutralMonsterUnit>();

        /// <summary>한 번이라도 시야에 들어온 에픽. 안개가 다시 덮여도 남는다(클래스 주석 참조).</summary>
        readonly List<NeutralMonsterUnit> _discovered = new List<NeutralMonsterUnit>();

        Fog.FogOfWarService _fog;
        float _nextScan;

        /// <summary>지금까지 발견한, <b>살아있는</b> 에픽 몬스터 목록. UI 가 이걸 그린다.</summary>
        public IReadOnlyList<NeutralMonsterUnit> Discovered => _discovered;

        /// <summary>명령·발견 목록이 바뀔 때마다 발생 — UI 가 다시 그린다.</summary>
        public event System.Action OnChanged;

        /// <summary>이 거리 안이면 사냥 타겟으로 넘긴다(타일).</summary>
        public float EngageRangeTiles => engageRangeTiles;

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

                // 안개 서비스가 없으면(테스트 씬) 전부 보이는 것으로 친다.
                if (_fog != null && !_fog.IsVisibleWorld(n.transform.position)) continue;

                _discovered.Add(n);
                changed = true;

                if (logOrders)
                    Debug.Log($"[토벌] 에픽 몬스터 발견 — {n.DisplayName}" +
                              (string.IsNullOrWhiteSpace(n.Title) ? "" : $" ({n.Title})"), n);
                UI.HudLog.Add($"에픽 몬스터 발견 — {n.DisplayName}", UI.HudLogKind.Warn);
            }
            return changed;
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
        /// </summary>
        public void SetOrder(int squadId, NeutralMonsterUnit target)
        {
            if (squadId <= 0) return;

            NeutralMonsterUnit now = OrderOf(squadId);
            if (ReferenceEquals(now, target)) return;

            SquadService squads = SquadService.Instance;
            string squadName = squads?.Find(squadId)?.Name ?? $"부대 {squadId}";

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
