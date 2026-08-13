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
        ///
        /// ⚠ <paramref name="exclude"/> 는 <paramref name="includeSelfIfWounded"/> 가 <b>false 일 때만</b>
        ///   동작한다. 이 기본값(true) 때문에 <c>UnitCombat.AcquireHealTarget</c> 이 자기 자신을
        ///   치유 타겟으로 잡고 제자리에 굳는 버그가 있었다(진행상황 73-1절) — 새로 부를 때 주의할 것.
        /// </summary>
        /// <param name="kindFilter">
        /// 이 종류만 후보로 본다. null 이면 종류를 가리지 않는다.
        /// ★ 치유는 <b>자신을 제외한 다른 캐릭터에게만</b> 가능하다(유저 확정 2026-08-13) —
        /// 넥서스·포탑은 대상이 아니므로 호출부가 <see cref="UnitKind.Character"/> 를 넘긴다.
        /// </param>
        public static DamageableUnit FindWoundedAlly(Vector3 from, Faction faction, float maxRangeTiles,
                                                     DamageableUnit exclude = null,
                                                     bool includeSelfIfWounded = true,
                                                     UnitKind? kindFilter = null)
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
                if (kindFilter.HasValue && u.Kind != kindFilter.Value) continue;
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
        /// 상자 자체를 돌리지는 않는다 — <b>축 정렬</b> 검사다. 임의 각도로 돌아간 범위가
        /// 필요하면 <see cref="CollectEnemiesInOrientedRect"/> 를 쓸 것.
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
        /// <b>임의 각도로 돌아간 직사각형</b> 범위 안의 적을 모은다 (보스 스킬 — 2026-08-13 개정).
        ///
        /// <b>왜 필요했나</b> — 예전에는 조준을 상·하·좌·우 <b>4방향으로 잘라</b> 축 정렬 상자만
        /// 썼다. 그래서 <b>대각선에만 적이 있으면 아무도 못 맞히는</b> 상황이 실제로 나왔다
        /// (유저 리포트 2026-08-13: "4방향에 적이 없으면 대각선 방향 적을 못 때리니까 의도랑 안 맞음").
        /// 이제 상자를 <b>조준 방향 그대로</b> 돌려서 360도 어느 각도로든 나간다.
        ///
        /// <b>계산</b> — 상자를 돌리는 대신 <b>대상을 스킬 좌표계로 옮겨</b> 검사한다:
        /// <paramref name="forward"/> 방향 성분(앞뒤)과 그와 직각인 성분(좌우)을 내적으로 구하면
        /// 회전 행렬을 만들 필요 없이 축 정렬 검사와 똑같이 비교할 수 있다.
        ///
        /// <paramref name="halfSizeTiles"/> 는 <b>스킬 좌표계 기준</b>이다:
        /// x = <paramref name="forward"/> 로 뻗는 길이의 절반, y = 그와 직각인 두께의 절반.
        /// </summary>
        public static void CollectEnemiesInOrientedRect(Vector3 center, Vector2 halfSizeTiles,
                                                        Vector2 forward, Faction myFaction,
                                                        List<DamageableUnit> into)
        {
            into.Clear();
            Faction enemy = myFaction.Opposite();

            // 0 벡터가 들어오면 축 정렬(오른쪽)로 떨어진다 — 0 으로 나누지 않게.
            Vector2 f = forward.sqrMagnitude > 0.000001f ? forward.normalized : Vector2.right;
            var right = new Vector2(-f.y, f.x);      // f 를 +90도 돌린 것 = 두께 축

            for (int i = 0; i < _units.Count; i++)
            {
                DamageableUnit u = _units[i];
                if (u == null || !u.IsAlive || u.Faction != enemy) continue;

                Vector2 d = (Vector2)(u.transform.position - center);
                if (Mathf.Abs(Vector2.Dot(d, f)) > halfSizeTiles.x) continue;
                if (Mathf.Abs(Vector2.Dot(d, right)) > halfSizeTiles.y) continue;

                into.Add(u);
            }
        }

        /// <summary>
        /// <b>원형</b> 범위 안의 적을 모은다 (보스 스킬의 <c>Circle</c> 범위 타입).
        /// <see cref="CollectAlliesInRadius"/> 의 적 버전이다 — 방향이라는 개념 자체가 없으므로
        /// 조준이 어긋날 일이 없고, 그래서 "360도 어디에 있든 맞는다" 가 그대로 성립한다.
        /// </summary>
        public static void CollectEnemiesInRadius(Vector3 center, float radiusTiles, Faction myFaction,
                                                  List<DamageableUnit> into)
        {
            into.Clear();
            Faction enemy = myFaction.Opposite();
            float maxSqr = radiusTiles * radiusTiles;

            for (int i = 0; i < _units.Count; i++)
            {
                DamageableUnit u = _units[i];
                if (u == null || !u.IsAlive || u.Faction != enemy) continue;
                if (((Vector2)(u.transform.position - center)).sqrMagnitude > maxSqr) continue;

                into.Add(u);
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
