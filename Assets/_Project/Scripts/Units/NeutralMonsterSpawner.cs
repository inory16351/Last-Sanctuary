using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Map;

namespace LastSanctuary.Units
{
    /// <summary>스폰 그룹 한 줄 — "어떤 중립 몬스터를, 어떤 템플릿으로, 몇 마리까지 유지할지".</summary>
    [System.Serializable]
    public struct NeutralSpawnEntry
    {
        [Tooltip("능력치·등장범위·에너지 보상이 담긴 데이터 테이블")]
        public NeutralMonsterDefinitionSO definition;

        [Tooltip("복제할 외형 템플릿. 비워두면 정의 에셋의 template 을 사용한다")]
        public NeutralMonsterUnit template;

        [Tooltip("이 종류가 맵에 동시에 존재할 수 있는 최대 개체 수. " +
                 "먼 곳(등장범위가 큰)일수록 강하므로 낮게 잡는 편이 밸런스에 맞다")]
        [Min(0)] public int maxAlive;
    }

    /// <summary>
    /// 중립 몬스터 생성. 웨이브 몬스터(<see cref="MonsterSpawner"/>)와 달리 웨이브 타이머와
    /// 무관하게 게임 시작부터 맵에 서식하고, 사냥당한 만큼 주기적으로 다시 채워진다.
    ///
    /// <b>생성 주기</b>: 원본 기획 테이블(`임시용 중립 몬스터.xlsx`)에는 스폰 주기 항목이
    /// 없어서 이번에 임의로 설계했다 — 게임 시작 시 정의별 상한까지 즉시 채우고, 이후
    /// <see cref="restockInterval"/> 마다 부족분을 조금씩(<see cref="maxSpawnPerRestock"/>)
    /// 보충한다. 한 번에 상한까지 다 채우면 리스폰이 몰려 티가 나므로 나눠서 채운다.
    /// 2026-08-05, 진행상황.md 22절 참조.
    ///
    /// <b>등장 범위</b>: 넥서스(항상 셀 (0,0))로부터 <see cref="NeutralMonsterDefinitionSO.MinDistanceFromNexus"/>
    /// (등장범위 n 의 절반, 체비셰프 거리 = n×n 정사각 구역의 경계) 이상 떨어진 칸에서만 스폰한다.
    /// "부터 나타날 수 있다"는 하한 조건이라 상한은 없다 — 더 멀리서도 계속 나타난다.
    /// </summary>
    public class NeutralMonsterSpawner : MonoBehaviour
    {
        [Header("데이터")]
        [SerializeField] BalanceConfigSO balance;

        [Tooltip("이 목록에 정의와 개체수 상한을 넣으면 알아서 유지한다")]
        [SerializeField] NeutralSpawnEntry[] spawnTable;

        [Header("맵 참조")]
        [SerializeField] MapGenerator mapGenerator;

        [Header("생성 주기 (임의 설계 — 위 클래스 설명 참조)")]
        [Tooltip("개체수를 확인해 상한보다 부족하면 채우는 간격(초).\n" +
                 "★ 종마다 다른 주기를 주고 싶으면 표(`임시용 중립 몬스터.xlsx` 의 " +
                 "`respawn_seconds`)를 채울 것 — 그 값이 있으면 이 값보다 우선한다")]
        [Min(1f)] [SerializeField] float restockInterval = 20f;

        [Tooltip("한 번 확인할 때 종류당 최대 몇 마리까지 채울지")]
        [Min(1)] [SerializeField] int maxSpawnPerRestock = 2;

        [Header("배치")]
        [Tooltip("등장 최소거리보다 얼마나 더 바깥까지 후보로 넓혀볼지(타일)")]
        [Min(1)] [SerializeField] int placementSearchRadius = 40;

        [Tooltip("배치 가능한 칸을 찾을 때 후보 셀 주변으로 몇 칸까지 대체 위치를 찾아볼지")]
        [Min(0)] [SerializeField] int placementFallbackRadius = 4;

        [SerializeField] int seed = 20260805;

        Transform _root;
        System.Random _rng;
        readonly Dictionary<NeutralMonsterDefinitionSO, List<NeutralMonsterUnit>> _alive =
            new Dictionary<NeutralMonsterDefinitionSO, List<NeutralMonsterUnit>>();

        /// <summary>종별 등장 거리 상한(체비셰프) — 한 단계 위 종의 등장 거리 하한. 최상위 종은 무한대.</summary>
        readonly Dictionary<NeutralMonsterDefinitionSO, float> _maxDistanceByDef =
            new Dictionary<NeutralMonsterDefinitionSO, float>();

        void Start()
        {
            _rng = new System.Random(seed);
            _root = new GameObject("NeutralMonsters").transform;
            _root.SetParent(transform, false);

            if (spawnTable == null) return;
            foreach (NeutralSpawnEntry e in spawnTable)
                if (e.definition != null) _alive[e.definition] = new List<NeutralMonsterUnit>();

            BuildMaxDistanceTable();

            // 시작할 때는 상한까지 한 번에 채운다 — 리스폰이 아니라 최초 서식이므로 몰려도 티가 안 난다.
            RestockAll(fillToCapImmediately: true);
            StartCoroutine(RestockLoop());
        }

        /// <summary>
        /// 각 종이 등장 가능한 거리의 상한을 구한다 — "부터 나타날 수 있다"(하한)만 있던 것을,
        /// 한 단계 위 종의 하한을 이 종의 상한으로 삼아 종마다 자기 구간(고리)을 갖게 한다
        /// (유저 요청: "역겨운 덩어리 1은 15~100 타일 구간에서만 등장"). 최상위 종은 위가 없으니
        /// 무한대로 둔다 — 예전처럼 "더 멀리서도 계속 나타난다".
        /// </summary>
        void BuildMaxDistanceTable()
        {
            _maxDistanceByDef.Clear();
            if (spawnTable == null) return;

            foreach (NeutralSpawnEntry mine in spawnTable)
            {
                if (mine.definition == null) continue;
                float min = mine.definition.MinDistanceFromNexus;
                float upper = float.PositiveInfinity;

                foreach (NeutralSpawnEntry other in spawnTable)
                {
                    if (other.definition == null || other.definition == mine.definition) continue;
                    float otherMin = other.definition.MinDistanceFromNexus;
                    if (otherMin > min && otherMin < upper) upper = otherMin;
                }

                _maxDistanceByDef[mine.definition] = upper;
            }
        }

        float MaxDistanceFor(NeutralMonsterDefinitionSO def) =>
            _maxDistanceByDef.TryGetValue(def, out float v) ? v : float.PositiveInfinity;

        /// <summary>종별 다음 보충 시각. 표의 <c>respawn_seconds</c> 를 종마다 따로 돌리기 위한 것.</summary>
        readonly Dictionary<NeutralMonsterDefinitionSO, float> _nextRestockTime =
            new Dictionary<NeutralMonsterDefinitionSO, float>();

        /// <summary>
        /// 이 종의 개체수 상한. <b>표가 정본</b>이고(정의의 <c>maxAlive</c>), 0 이면 씬 스포너의
        /// 슬롯 값으로 떨어진다 — 표에 값을 넣기 전의 씬을 깨지 않기 위한 폴백이다.
        /// </summary>
        static int CapFor(NeutralSpawnEntry e) =>
            e.definition != null && e.definition.maxAlive > 0 ? e.definition.maxAlive : e.maxAlive;

        /// <summary>이 종의 보충 간격(초). 표가 정본, 0 이면 스포너의 공통값.</summary>
        float RestockSecondsFor(NeutralMonsterDefinitionSO def) =>
            def != null && def.respawnSeconds > 0f ? def.respawnSeconds : restockInterval;

        /// <summary>
        /// ★ <b>종마다 다른 주기</b>로 돈다 (유저 지시 2026-08-13 "스폰 주기도 조절").
        ///
        /// 예전에는 <see cref="restockInterval"/> 하나로 전 종을 한꺼번에 채웠다. 그러면
        /// "가까운 종은 천천히, 먼 종은 빨리 다시 차오르게" 같은 조정이 불가능해서,
        /// 중앙에 사냥감이 계속 넘치고 멀리 나갈 이유가 없어진다 — 유저가 지적한 그 문제다.
        /// 루프는 1초마다 돌면서 <b>각 종의 자기 시각</b>이 됐는지만 본다(코루틴을 종마다
        /// 따로 띄우지 않는 이유: 개체 정리·상한 계산을 한 곳에 모아두는 편이 읽기 쉽다).
        /// </summary>
        IEnumerator RestockLoop()
        {
            var tick = new WaitForSeconds(1f);
            while (true)
            {
                yield return tick;
                RestockAll(fillToCapImmediately: false);
            }
        }

        void RestockAll(bool fillToCapImmediately)
        {
            if (balance == null || spawnTable == null) return;

            foreach (NeutralSpawnEntry e in spawnTable)
            {
                if (e.definition == null) continue;

                if (!fillToCapImmediately)
                {
                    // 아직 이 종의 차례가 아니면 건너뛴다.
                    if (_nextRestockTime.TryGetValue(e.definition, out float at) && Time.time < at) continue;
                    _nextRestockTime[e.definition] = Time.time + RestockSecondsFor(e.definition);
                }
                else
                {
                    _nextRestockTime[e.definition] = Time.time + RestockSecondsFor(e.definition);
                }

                List<NeutralMonsterUnit> list = PruneAndGet(e.definition);
                int need = CapFor(e) - list.Count;
                if (need <= 0) continue;

                int spawnNow = fillToCapImmediately ? need : Mathf.Min(need, maxSpawnPerRestock);
                for (int i = 0; i < spawnNow; i++) SpawnOne(e);
            }
        }

        List<NeutralMonsterUnit> PruneAndGet(NeutralMonsterDefinitionSO def)
        {
            if (!_alive.TryGetValue(def, out List<NeutralMonsterUnit> list))
            {
                list = new List<NeutralMonsterUnit>();
                _alive[def] = list;
            }
            list.RemoveAll(m => m == null || !m.IsAlive);
            return list;
        }

        void SpawnOne(NeutralSpawnEntry entry)
        {
            NeutralMonsterUnit template = entry.template != null ? entry.template : entry.definition.template;
            if (template == null)
            {
                Debug.LogError($"[NeutralMonsterSpawner] {entry.definition.name} 에 연결된 템플릿이 없습니다. " +
                               "스폰 테이블의 Template 칸을 채워주세요.", this);
                return;
            }

            float minDist = entry.definition.MinDistanceFromNexus;
            float maxDist = MaxDistanceFor(entry.definition);
            if (!TryFindSpawnCell(entry.definition, minDist, maxDist, out Vector3Int cell)) return;

            Vector3 pos = mapGenerator != null
                ? mapGenerator.CellCenterWorld(cell)
                : new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

            NeutralMonsterUnit unit = Instantiate(template, pos, Quaternion.identity, _root);
            unit.name = $"{entry.definition.DisplayName}_{_rng.Next(1000, 9999)}";
            unit.gameObject.SetActive(true);
            unit.Initialize(entry.definition, balance);

            var ai = unit.GetComponent<UnitCombat>();
            if (ai != null)
            {
                // ★ <b>적대 판정은 표의 atk_take(=aggressive) 한 칸이 전부다</b> (유저 확정 2026-08-13).
                //   선공   → 적이 보이면 먼저 다가가 때린다        (canAcquireTargets = true)
                //   비선공 → 맞기 전까지 공격하지 않고, 맞으면 반격 (canAcquireTargets = false · canRetaliate = true)
                //
                // ⚠ <b>두 값을 여기서 반드시 덮어쓴다.</b> 예전에는 canAcquireTargets 만 넣고
                //   canRetaliate 는 템플릿 인스펙터 값을 그대로 뒀다. 그래서 표는 비선공인데
                //   템플릿이 선공으로 켜져 있으면 서로 어긋났고, 인스펙터에 '선공 체크'가
                //   여러 개로 보였다(유저 리포트). 이제 <b>표가 언제나 이긴다</b>.
                //
                // 타겟 우선순위도 항상 캐릭터로 준다 — 비선공이라도 <b>맞으면</b> 반격해야 하고,
                // 그때 우선순위가 비어 있으면 때린 상대를 못 고른다.
                //
                // 공격 방식은 <b>근거리 고정</b>이다(유저 지시: "일단 중립몹들 공격 방식은 근거리로").
                ai.Configure(entry.definition.detectRange, entry.definition.attackRange,
                             entry.definition.moveSpeedTiles, entry.definition.attacksPerSecond,
                             advance: false,
                             priority: new[] { UnitKind.Character },
                             leash: entry.definition.leashRangeTiles,
                             type: TacticalAttackType.Melee);
                ai.SetCanAcquireTargets(entry.definition.aggressive);
                ai.SetCanRetaliate(true);
                ai.SetHome(unit.transform.position);
            }

            // 배회 — <b>자기가 소환될 수 있는 구간과 정확히 같은 고리</b> 안에서만 돌아다닌다
            // (유저 지시 2026-08-13: "중립 몬스터가 소환 가능한 범위 내에서만 배회하게 해줘").
            // ⚠ 여기서 넘기는 바깥 반지름은 <b>스폰에 쓴 것과 같은 값</b>이어야 한다 — 예전에는
            //   무한대를 넘기고 배회 쪽에서 임의의 60타일로 잘라, 스폰 범위와 배회 범위가
            //   서로 달랐다(최상위 종이 스폰 가능 구역 밖으로 걸어나갔다).
            var wander = unit.gameObject.GetComponent<NeutralMonsterWander>();
            if (wander == null) wander = unit.gameObject.AddComponent<NeutralMonsterWander>();
            wander.Init(minDist, ResolveOuterRadius(minDist, maxDist));

            PruneAndGet(entry.definition).Add(unit);
        }

        /// <summary>
        /// 넥서스(셀 (0,0)) 기준 정의된 최소거리 이상, 그리고 <paramref name="maxDist"/> 이하(유한하면)인
        /// 배치 가능한 칸을 무작위로 찾는다. 상한이 무한대(최상위 종)면 예전처럼
        /// "더 멀리서도 계속 나타난다".
        /// </summary>
        bool TryFindSpawnCell(NeutralMonsterDefinitionSO def, float minDist, float maxDist, out Vector3Int result)
        {
            float outer = ResolveOuterRadius(minDist, maxDist);

            const int Attempts = 32;
            for (int i = 0; i < Attempts; i++)
            {
                Vector3Int candidate = SampleRingCell(_rng, minDist, outer);

                if (mapGenerator == null)
                {
                    result = candidate;
                    return true;
                }
                if (!mapGenerator.IsCellInsideMap(candidate)) continue;

                if (mapGenerator.TryFindPlaceableNear(candidate, placementFallbackRadius, null,
                                                       out Vector3Int placeable) &&
                    ChebyshevDistance(placeable) >= minDist &&
                    ChebyshevDistance(placeable) <= outer)
                {
                    result = placeable;
                    return true;
                }
            }

            result = Vector3Int.zero;
            return false;
        }

        /// <summary>이 종이 실제로 쓸 바깥 반지름(타일, 체비셰프). 무한대는 맵 크기로 자른다.</summary>
        float ResolveOuterRadius(float minDist, float maxDist)
        {
            int mapHalf = mapGenerator != null && mapGenerator.Config != null
                ? Mathf.Max(mapGenerator.Config.MapSize.x, mapGenerator.Config.MapSize.y) / 2 - 2
                : Mathf.CeilToInt(minDist) + placementSearchRadius;

            float outer = float.IsPositiveInfinity(maxDist)
                ? Mathf.Min(minDist + placementSearchRadius, mapHalf)
                : Mathf.Min(maxDist, mapHalf);

            return Mathf.Max(minDist + 1f, outer);
        }

        /// <summary>
        /// ★ <b>체비셰프 고리(사각 링) 안에서 균일하게 한 칸을 고른다</b> (2026-08-13 개정).
        ///
        /// <b>왜 고쳤나</b> — 예전에는 <b>각도 + 유클리드 반지름</b>으로 뽑고 그 결과를
        /// <b>체비셰프</b> 거리로 검사했다. 두 거리는 대각선에서 최대 √2 배 차이가 나므로,
        /// 유클리드 반지름 <c>r</c> 로 뽑은 점의 체비셰프 거리는 평균적으로 <c>r</c> 보다
        /// <b>작다</b>. 그 결과 개체가 고리 안쪽(=넥서스 쪽)으로 쏠렸고, 바깥 경계 근처는
        /// 거의 비었다 — 유저가 말한 <b>"계속 중앙으로 중립몹이 모인다"</b> 의 실체다.
        ///
        /// 이제 <b>체비셰프 거리 자체를 균일하게</b> 뽑고, 그 거리의 정사각 테두리 위에서
        /// 한 점을 고른다. 결과의 체비셰프 거리는 <b>정확히</b> 뽑은 값이라 고리를 벗어나지도,
        /// 안쪽으로 쏠리지도 않는다.
        /// </summary>
        static Vector3Int SampleRingCell(System.Random rng, float minDist, float maxDist)
        {
            float d = Mathf.Lerp(minDist, maxDist, (float)rng.NextDouble());
            int r = Mathf.Max(1, Mathf.RoundToInt(d));
            int along = rng.Next(-r, r + 1);

            return rng.Next(4) switch
            {
                0 => new Vector3Int(along, r, 0),      // 위 변
                1 => new Vector3Int(along, -r, 0),     // 아래 변
                2 => new Vector3Int(r, along, 0),      // 오른 변
                _ => new Vector3Int(-r, along, 0),     // 왼 변
            };
        }

        static float ChebyshevDistance(Vector3Int cell) => Mathf.Max(Mathf.Abs(cell.x), Mathf.Abs(cell.y));

        void OnDrawGizmosSelected()
        {
            if (spawnTable == null) return;
            Gizmos.color = new Color(0.6f, 1f, 0.3f, 0.5f);
            foreach (NeutralSpawnEntry e in spawnTable)
            {
                if (e.definition == null) continue;
                float half = e.definition.MinDistanceFromNexus;
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(half * 2f, half * 2f, 0f));
            }
        }
    }
}
