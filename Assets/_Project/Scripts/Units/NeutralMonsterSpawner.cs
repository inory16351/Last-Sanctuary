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
        [Tooltip("개체수를 확인해 상한보다 부족하면 채우는 간격(초)")]
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

        void Start()
        {
            _rng = new System.Random(seed);
            _root = new GameObject("NeutralMonsters").transform;
            _root.SetParent(transform, false);

            if (spawnTable == null) return;
            foreach (NeutralSpawnEntry e in spawnTable)
                if (e.definition != null) _alive[e.definition] = new List<NeutralMonsterUnit>();

            // 시작할 때는 상한까지 한 번에 채운다 — 리스폰이 아니라 최초 서식이므로 몰려도 티가 안 난다.
            RestockAll(fillToCapImmediately: true);
            StartCoroutine(RestockLoop());
        }

        IEnumerator RestockLoop()
        {
            var wait = new WaitForSeconds(restockInterval);
            while (true)
            {
                yield return wait;
                RestockAll(fillToCapImmediately: false);
            }
        }

        void RestockAll(bool fillToCapImmediately)
        {
            if (balance == null || spawnTable == null) return;

            foreach (NeutralSpawnEntry e in spawnTable)
            {
                if (e.definition == null) continue;

                List<NeutralMonsterUnit> list = PruneAndGet(e.definition);
                int need = e.maxAlive - list.Count;
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

            if (!TryFindSpawnCell(entry.definition, out Vector3Int cell)) return;

            Vector3 pos = mapGenerator != null
                ? mapGenerator.CellCenterWorld(cell)
                : new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

            NeutralMonsterUnit unit = Instantiate(template, pos, Quaternion.identity, _root);
            unit.name = $"{entry.definition.displayName}_{_rng.Next(1000, 9999)}";
            unit.gameObject.SetActive(true);
            unit.Initialize(entry.definition, balance);

            var ai = unit.GetComponent<UnitCombat>();
            if (ai != null)
            {
                if (entry.definition.aggressive)
                {
                    // 선공형만 전투 AI를 켠다 — 적은 캐릭터뿐이고(넥서스로 전진하지 않음),
                    // 서식지 밖으로 멀리 쫓아가지 않도록 정의된 leash 를 그대로 쓴다.
                    ai.Configure(entry.definition.detectRange, entry.definition.attackRange,
                                 entry.definition.moveSpeedTiles, entry.definition.attacksPerSecond,
                                 advance: false, priority: new[] { UnitKind.Character },
                                 leash: entry.definition.leashRangeTiles);
                    ai.SetHome(unit.transform.position);
                }
                else
                {
                    // 비선공 개체는 전투 능력이 없는 사냥감이다 — AI 자체를 꺼서
                    // 어떤 상황에서도 스스로 공격하지 않게 한다.
                    ai.enabled = false;
                }
            }

            PruneAndGet(entry.definition).Add(unit);
        }

        /// <summary>
        /// 넥서스(셀 (0,0)) 기준 정의된 최소거리 이상 + 배치 가능한 칸을 무작위로 찾는다.
        /// "부터 나타날 수 있다"는 하한만 있으므로 상한은 맵 절반 크기로 자연히 제한된다.
        /// </summary>
        bool TryFindSpawnCell(NeutralMonsterDefinitionSO def, out Vector3Int result)
        {
            float minDist = def.MinDistanceFromNexus;
            int maxHalf = mapGenerator != null && mapGenerator.Config != null
                ? Mathf.Max(mapGenerator.Config.MapSize.x, mapGenerator.Config.MapSize.y) / 2
                : Mathf.CeilToInt(minDist) + placementSearchRadius;
            int outerRadius = Mathf.Clamp(Mathf.CeilToInt(minDist) + placementSearchRadius, 1, Mathf.Max(1, maxHalf));

            const int Attempts = 24;
            for (int i = 0; i < Attempts; i++)
            {
                double angle = _rng.NextDouble() * System.Math.PI * 2.0;
                float dist = Mathf.Lerp(minDist, outerRadius, (float)_rng.NextDouble());

                var candidate = new Vector3Int(
                    Mathf.RoundToInt(Mathf.Cos((float)angle) * dist),
                    Mathf.RoundToInt(Mathf.Sin((float)angle) * dist), 0);

                if (mapGenerator == null)
                {
                    result = candidate;
                    return true;
                }
                if (!mapGenerator.IsCellInsideMap(candidate)) continue;

                if (mapGenerator.TryFindPlaceableNear(candidate, placementFallbackRadius, null,
                                                       out Vector3Int placeable) &&
                    ChebyshevDistance(placeable) >= minDist)
                {
                    result = placeable;
                    return true;
                }
            }

            result = Vector3Int.zero;
            return false;
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
