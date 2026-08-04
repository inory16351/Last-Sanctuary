using System.Collections.Generic;
using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 살아있는 유닛 목록. 타겟 탐색이 매 프레임 FindObjectsByType 를 돌지 않도록
    /// 등록/해제 방식으로 관리한다.
    ///
    /// 지금은 선형 탐색이다. 유닛이 수백 개로 늘어나면 공간 분할 그리드로
    /// 교체할 지점이며, 호출부(FindTarget)는 그대로 두면 된다.
    /// </summary>
    public static class UnitRegistry
    {
        static readonly List<DamageableUnit> _units = new List<DamageableUnit>(256);

        public static IReadOnlyList<DamageableUnit> All => _units;

        /// <summary>플레이 모드를 다시 시작할 때 정적 목록이 남아있지 않게 초기화.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => _units.Clear();

        public static void Register(DamageableUnit u)
        {
            if (u != null && !_units.Contains(u)) _units.Add(u);
        }

        public static void Unregister(DamageableUnit u)
        {
            if (u != null) _units.Remove(u);
        }

        /// <summary>
        /// 우선순위 순서대로 훑어, 가장 앞선 종류 중에서 가장 가까운 적을 고른다.
        /// priority 가 비어 있으면 종류를 구분하지 않고 최근접을 고른다.
        /// filter 를 주면(예: 안개 시야 판정) 그 조건도 만족해야 후보가 된다.
        /// </summary>
        public static DamageableUnit FindTarget(Vector3 from, Faction myFaction,
                                                float maxRangeTiles, UnitKind[] priority,
                                                System.Func<DamageableUnit, bool> filter = null)
        {
            float maxSqr = maxRangeTiles * maxRangeTiles;
            Faction enemy = myFaction.Opposite();

            if (priority == null || priority.Length == 0)
                return NearestOfKinds(from, enemy, maxSqr, null, filter);

            for (int p = 0; p < priority.Length; p++)
            {
                var found = NearestOfKinds(from, enemy, maxSqr, priority[p], filter);
                if (found != null) return found;
            }
            return null;
        }

        static DamageableUnit NearestOfKinds(Vector3 from, Faction enemy, float maxSqr, UnitKind? kind,
                                             System.Func<DamageableUnit, bool> filter)
        {
            DamageableUnit best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < _units.Count; i++)
            {
                DamageableUnit u = _units[i];
                if (u == null || !u.IsAlive) continue;
                if (u.Faction != enemy) continue;
                if (kind.HasValue && u.Kind != kind.Value) continue;
                if (filter != null && !filter(u)) continue;

                float sqr = (u.transform.position - from).sqrMagnitude;
                if (sqr > maxSqr || sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = u;
            }
            return best;
        }

        /// <summary>특정 진영·종류의 유닛을 하나 찾는다 (넥서스 위치 조회 등).</summary>
        public static DamageableUnit FindFirst(Faction faction, UnitKind kind)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                DamageableUnit u = _units[i];
                if (u != null && u.IsAlive && u.Faction == faction && u.Kind == kind) return u;
            }
            return null;
        }

        public static int CountAlive(Faction faction)
        {
            int n = 0;
            for (int i = 0; i < _units.Count; i++)
                if (_units[i] != null && _units[i].IsAlive && _units[i].Faction == faction) n++;
            return n;
        }
    }
}
