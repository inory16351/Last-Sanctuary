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

        /// <summary>
        /// 전술 지침의 "공격 우선 대상"(<see cref="TacticalTargetPriority"/>)대로 적 하나를 고른다.
        ///
        /// <see cref="FindTarget"/> 을 고치지 않고 따로 둔 이유: 저쪽은 몬스터가 쓰는
        /// "종류 우선순위(<see cref="UnitKind"/> 배열)" 규칙이고(웨이브 기획서 p13), 이쪽은
        /// 캐릭터가 쓰는 "거리·강함·체력" 규칙이라 판정 축 자체가 다르다. 한 함수에 섞으면
        /// 몬스터 타겟팅까지 같이 흔들린다(진행상황 6절의 과거 버그와 같은 종류의 위험).
        /// </summary>
        public static DamageableUnit FindTargetBy(Vector3 from, Faction myFaction, float maxRangeTiles,
                                                  TacticalTargetPriority mode,
                                                  System.Func<DamageableUnit, bool> filter = null)
        {
            float maxSqr = maxRangeTiles * maxRangeTiles;
            Faction enemy = myFaction.Opposite();

            DamageableUnit best = null;
            float bestScore = 0f;
            float bestSqr = 0f;

            for (int i = 0; i < _units.Count; i++)
            {
                DamageableUnit u = _units[i];
                if (u == null || !u.IsAlive) continue;
                if (u.Faction != enemy) continue;
                if (filter != null && !filter(u)) continue;

                float sqr = (u.transform.position - from).sqrMagnitude;
                if (sqr > maxSqr) continue;

                // 점수는 "클수록 좋다"로 통일한다 — 거리 기준은 부호만 뒤집으면 되고,
                // 새 기준을 추가할 때도 비교 코드를 안 건드려도 된다.
                float score = mode switch
                {
                    TacticalTargetPriority.Strongest => u.AttackStat,
                    TacticalTargetPriority.Farthest  => sqr,
                    TacticalTargetPriority.Weakest   => -u.CurrentHp,
                    _                                => -sqr,   // Nearest
                };

                // 동률(같은 공격력·같은 체력)이면 가까운 쪽을 고른다 — 안 그러면 등록 순서에
                // 따라 멀리 있는 적을 붙잡고 계속 걸어가는 이상한 그림이 나온다.
                if (best != null && (score < bestScore || (score == bestScore && sqr >= bestSqr))) continue;

                best = u;
                bestScore = score;
                bestSqr = sqr;
            }
            return best;
        }

        /// <summary>
        /// 사거리 안에서 가장 많이 다친 아군을 찾는다 (치유 유형 캐릭터용).
        /// 체력 비율이 가장 낮은 대상을 고르고, 같으면 가까운 쪽을 고른다.
        /// <paramref name="exclude"/> 는 보통 자기 자신 — 자기만 계속 치유하는 걸 막는 데 쓴다.
        /// </summary>
        public static DamageableUnit FindWoundedAlly(Vector3 from, Faction faction, float maxRangeTiles,
                                                     DamageableUnit exclude = null,
                                                     bool includeSelfIfWounded = true)
        {
            float maxSqr = maxRangeTiles * maxRangeTiles;

            DamageableUnit best = null;
            float bestRatio = 1f;      // 만피는 후보가 아니다
            float bestSqr = 0f;

            for (int i = 0; i < _units.Count; i++)
            {
                DamageableUnit u = _units[i];
                if (u == null || !u.IsAlive) continue;
                if (u.Faction != faction) continue;
                if (!includeSelfIfWounded && ReferenceEquals(u, exclude)) continue;

                // 치유를 거부하는 대상(정신 이상 "이기심")은 후보에서 뺀다 — 안 그러면 치유형
                // 캐릭터가 "가장 많이 다친 아군"으로 그를 붙잡고 아무 효과 없이 서 있게 된다.
                if (!u.AcceptsExternalHeal) continue;

                float ratio = u.HpRatio;
                if (ratio >= 1f) continue;

                float sqr = (u.transform.position - from).sqrMagnitude;
                if (sqr > maxSqr) continue;

                if (best != null && (ratio > bestRatio || (ratio == bestRatio && sqr >= bestSqr))) continue;

                best = u;
                bestRatio = ratio;
                bestSqr = sqr;
            }
            return best;
        }

        /// <summary>
        /// 한 지점을 중심으로 한 <b>정사각</b> 범위 안의 적을 모은다 (마법 범위 공격용).
        /// 셀 격자가 아니라 월드 좌표 기준이라 사거리·범위 값을 소수로 둬도 그대로 동작한다.
        /// </summary>
        public static void CollectEnemiesInBox(Vector3 center, float halfExtentTiles, Faction myFaction,
                                               List<DamageableUnit> into)
        {
            into.Clear();
            Faction enemy = myFaction.Opposite();

            for (int i = 0; i < _units.Count; i++)
            {
                DamageableUnit u = _units[i];
                if (u == null || !u.IsAlive || u.Faction != enemy) continue;

                Vector3 d = u.transform.position - center;
                if (Mathf.Abs(d.x) <= halfExtentTiles && Mathf.Abs(d.y) <= halfExtentTiles) into.Add(u);
            }
        }

        /// <summary>
        /// 한 지점을 중심으로 한 <b>직사각형</b> 범위 안의 적을 모은다 (보스 스킬용).
        ///
        /// <see cref="CollectEnemiesInBox"/> 와 달리 가로·세로 반지름을 따로 받는다 —
        /// 보스 스킬은 "5 x 3", "15 x 3" 처럼 한 방향으로 긴 범위라 정사각으로는 표현할 수 없다.
        /// 상자 자체를 돌리지는 않는다: <see cref="BossSkillCaster"/> 가 조준 방향을 4방향으로
        /// 잘라 가로·세로를 바꿔 넣기 때문에 축 정렬 검사만으로 충분하다(맵도 타일 격자다).
        /// </summary>
        public static void CollectEnemiesInRect(Vector3 center, Vector2 halfSizeTiles, Faction myFaction,
                                                List<DamageableUnit> into)
        {
            into.Clear();
            Faction enemy = myFaction.Opposite();

            for (int i = 0; i < _units.Count; i++)
            {
                DamageableUnit u = _units[i];
                if (u == null || !u.IsAlive || u.Faction != enemy) continue;

                Vector3 d = u.transform.position - center;
                if (Mathf.Abs(d.x) <= halfSizeTiles.x && Mathf.Abs(d.y) <= halfSizeTiles.y) into.Add(u);
            }
        }

        /// <summary>
        /// 반경 안의 <b>같은 진영</b> 유닛 중 가장 가까운 하나 (정신 이상 "혼란" 의 아군 공격용).
        /// <see cref="FindTarget"/> 계열은 <see cref="FactionExtensions.Opposite"/> 로 적을 찾으므로
        /// 아군을 노리는 데는 쓸 수 없어 따로 둔다. <paramref name="exclude"/> 는 보통 자기 자신이다.
        /// </summary>
        public static DamageableUnit FindNearestAlly(Vector3 from, Faction faction, float maxRangeTiles,
                                                     DamageableUnit exclude = null)
        {
            float maxSqr = maxRangeTiles * maxRangeTiles;

            DamageableUnit best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < _units.Count; i++)
            {
                DamageableUnit u = _units[i];
                if (u == null || !u.IsAlive || u.Faction != faction) continue;
                if (ReferenceEquals(u, exclude)) continue;

                float sqr = (u.transform.position - from).sqrMagnitude;
                if (sqr > maxSqr || sqr >= bestSqr) continue;

                best = u;
                bestSqr = sqr;
            }
            return best;
        }

        /// <summary>
        /// 반경(원형) 안의 <b>같은 진영</b> 유닛을 모은다 — 정신 이상의 광역 효과
        /// (진정·우울의 침식 전이, 역겨움의 지속 피해)에 쓴다.
        /// <see cref="CollectEnemiesInBox"/> 와 달리 원형이고 대상이 아군이다.
        /// </summary>
        public static void CollectAlliesInRadius(Vector3 center, float radiusTiles, Faction faction,
                                                 DamageableUnit exclude, List<DamageableUnit> into)
        {
            into.Clear();
            float maxSqr = radiusTiles * radiusTiles;

            for (int i = 0; i < _units.Count; i++)
            {
                DamageableUnit u = _units[i];
                if (u == null || !u.IsAlive || u.Faction != faction) continue;
                if (ReferenceEquals(u, exclude)) continue;
                if ((u.transform.position - center).sqrMagnitude > maxSqr) continue;

                into.Add(u);
            }
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
