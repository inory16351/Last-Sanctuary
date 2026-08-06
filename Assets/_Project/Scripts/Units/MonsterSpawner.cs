using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Map;
using LastSanctuary.Wave;

namespace LastSanctuary.Units
{
    /// <summary>스폰 그룹 한 줄 — "어떤 몬스터를 몇 마리, 어떤 템플릿으로".</summary>
    [System.Serializable]
    public struct MonsterSpawnEntry
    {
        [Tooltip("능력치와 전투 파라미터가 담긴 데이터 테이블")]
        public MonsterDefinitionSO definition;

        [Tooltip("복제할 외형 템플릿. 비워두면 정의 에셋의 template 을 사용한다.\n" +
                 "ScriptableObject 는 씬 오브젝트를 참조할 수 없으므로, 템플릿이 " +
                 "하이라키에 있는 동안은 여기에 연결한다. 프리팹으로 만들면 정의 쪽에 넣어도 된다")]
        public MonsterUnit template;

        [Min(0)] public int count;
    }

    /// <summary>
    /// 몬스터 생성. 정의 테이블만 채우면 나머지는 자동으로 처리한다.
    ///
    /// 언제 소환할지는 <see cref="LastSanctuary.Wave.WaveManager"/> 가 정하고,
    /// 이 클래스는 "한 무리를 어떻게 스폰할지"만 담당한다.
    /// 몬스터는 맵 가장자리 스폰 게이트에서 0.5초 간격으로 순차 등장한다(웨이브 기획서 p10).
    ///
    /// <b>웨이브별 구성(2026-08-05 반영)</b>: <see cref="waveDefinitions"/> 가 지정되어 있고
    /// 그 표에 해당 웨이브 행이 있으면, 고정된 <see cref="spawnTable"/> 대신 그 행의 근거리/
    /// 원거리/보스 마리 수와 능력치 배율(<c>wave.statPercent</c>)을 쓴다 — `테이블/웨이브테이블.xlsx`
    /// 를 그대로 반영한 것으로, 예전의 "웨이브가 올라도 구성은 고정, 배율만 선형으로 커진다"
    /// (진행상황 6절)를 대체한다. 표가 없으면(예전 씬) <see cref="hpPercentPerWave"/>/
    /// <see cref="attackPercentPerWave"/> 선형 공식 + <see cref="spawnTable"/> 로 그대로 동작한다.
    /// </summary>
    public class MonsterSpawner : MonoBehaviour
    {
        [Header("데이터")]
        [SerializeField] BalanceConfigSO balance;

        [Tooltip("이 목록에 정의와 마리 수를 넣으면 알아서 생성한다. " +
                 "waveDefinitions 가 지정되고 그 웨이브 행을 찾으면 이 표의 count 는 무시된다")]
        [SerializeField] MonsterSpawnEntry[] spawnTable;

        [Header("웨이브 테이블 (테이블/웨이브테이블.xlsx 반영)")]
        [Tooltip("지정하면 웨이브 번호로 표를 조회해 근거리/원거리/보스 마리 수와 능력치 배율을 " +
                 "가져온다 — 비워두면 아래 hpPercentPerWave/attackPercentPerWave 선형 공식으로 " +
                 "되돌아간다(예전 동작 그대로 유지)")]
        [SerializeField] WaveDefinitionSO waveDefinitions;

        [Tooltip("웨이브 테이블 사용 시 근거리 담당 (정의+템플릿만 쓰고 count 는 표를 따른다)")]
        [SerializeField] MonsterSpawnEntry meleeSlot;

        [Tooltip("웨이브 테이블 사용 시 원거리 담당")]
        [SerializeField] MonsterSpawnEntry rangedSlot;

        [Tooltip("웨이브 테이블 사용 시 보스 담당")]
        [SerializeField] MonsterSpawnEntry bossSlot;

        [Header("맵 참조")]
        [SerializeField] MapGenerator mapGenerator;

        [Header("스폰 규칙")]
        [Tooltip("몬스터 간 등장 간격(초). 기획서 기준 0.5초")]
        [Min(0f)] [SerializeField] float spawnInterval = 0.5f;

        [Tooltip("맵 가장자리에서 안쪽으로 몇 칸 지점에 스폰할지")]
        [Min(0)] [SerializeField] int edgeInset = 2;

        [Header("웨이브 배율 (퍼센트 · 정수)")]
        [Tooltip("WaveManager 가 소환할 때마다 덮어쓴다. 스포너 단독 테스트 시의 기본값")]
        [Min(1)] [SerializeField] int waveNumber = 1;

        [Tooltip("웨이브가 1 오를 때마다 체력 능력치에 더해지는 퍼센트")]
        [Min(0)] [SerializeField] int hpPercentPerWave = 12;

        [Tooltip("웨이브가 1 오를 때마다 공격 능력치에 더해지는 퍼센트")]
        [Min(0)] [SerializeField] int attackPercentPerWave = 10;

        [Header("실행")]
        [Tooltip("WaveManager 가 대기시간 타이머로 소환을 지시하므로 평소에는 꺼둔다. " +
                 "스포너만 따로 테스트할 때만 켠다")]
        [SerializeField] bool spawnOnStart = false;
        [SerializeField] int seed = 777;

        Transform _root;
        readonly List<MonsterUnit> _alive = new List<MonsterUnit>();
        bool _spawning;

        Coroutine _reinforceCoroutine;
        bool _hasPendingReinforcements;
        int _currentEnragePercent = 100;

        public IReadOnlyList<MonsterUnit> Alive => _alive;
        public int AliveCount { get { Prune(); return _alive.Count; } }

        /// <summary>스폰 루틴이 아직 마리를 다 내보내지 않았으면 true. "전멸 판정"은 이게 꺼진 뒤에만 유효하다.</summary>
        public bool IsSpawning => _spawning;

        /// <summary>전투 중 증원이 더 오기로 예정돼 있으면 true. "전멸 판정"은 이게 꺼진 뒤에만 유효하다 —
        /// 안 그러면 증원 사이의 잠깐 조용한 틈에 웨이브가 조기 종료돼버린다.</summary>
        public bool HasPendingReinforcements => _hasPendingReinforcements;

        /// <summary>웨이브가 광폭화 상태일 때 남은 몬스터 전체에 능력치 배율(%)을 적용한다.</summary>
        public void SetEnragePercent(int percent)
        {
            _currentEnragePercent = percent;
            Prune();
            for (int i = 0; i < _alive.Count; i++)
                _alive[i].SetEnragePercent(percent);
        }

        void Start()
        {
            if (spawnOnStart) SpawnWave();
        }

        // ------------------------------------------------------------------

        /// <summary>웨이브 번호를 지정해 한 무리를 생성한다. WaveManager 가 호출한다.</summary>
        public void SpawnWave(int wave)
        {
            waveNumber = Mathf.Max(1, wave);
            SpawnWave();
        }

        /// <summary>현재 스폰 테이블대로 한 무리를 생성한다.</summary>
        public void SpawnWave()
        {
            if (balance == null)
            {
                Debug.LogError("[MonsterSpawner] Balance Config 가 연결되지 않았습니다.", this);
                return;
            }
            if (spawnTable == null || spawnTable.Length == 0)
            {
                Debug.LogWarning("[MonsterSpawner] 스폰 테이블이 비어 있습니다.", this);
                return;
            }

            if (_root == null)
            {
                _root = new GameObject("Monsters").transform;
                _root.SetParent(transform, false);
            }

            StopReinforcements();
            _currentEnragePercent = 100;

            StopAllCoroutines();
            _spawning = true;
            StartCoroutine(SpawnRoutine());
        }

        /// <summary>
        /// 전투 중 웨이브 테이블의 reinforceCount/reinforceIntervalSeconds 만큼 몬스터를 계속 흘려보낸다.
        /// WaveManager 가 Battle 시작 시 호출한다 — "광폭화가 거의 안 걸린다"(웨이브를 너무 쉽게,
        /// 빨리 정리해버린다)는 피드백으로 추가했다. 웨이브 타이머(<paramref name="battleDuration"/>)가
        /// 끝나기 전까지만 흘려보내고, 그 이후(광폭화)에는 더 늘리지 않는다 — 광폭화는 이미 능력치
        /// 배율로 압박하므로 마리 수까지 계속 늘리면 과하다.
        /// </summary>
        public void BeginReinforcements(int wave, float battleDuration)
        {
            StopReinforcements();
            if (waveDefinitions == null || !waveDefinitions.TryGetWave(wave, out WaveMonsterComposition comp))
                return;
            if (comp.reinforceCount <= 0 || comp.reinforceIntervalSeconds <= 0f) return;

            _reinforceCoroutine = StartCoroutine(ReinforcementRoutine(wave, comp, battleDuration));
        }

        /// <summary>예정된 증원을 모두 취소한다. 웨이브가 끝나거나(조기 클리어·광폭화 클리어) 패배하면 부른다.</summary>
        public void StopReinforcements()
        {
            if (_reinforceCoroutine != null) StopCoroutine(_reinforceCoroutine);
            _reinforceCoroutine = null;
            _hasPendingReinforcements = false;
        }

        IEnumerator ReinforcementRoutine(int wave, WaveMonsterComposition comp, float battleDuration)
        {
            _hasPendingReinforcements = true;
            var rng = new System.Random(seed + wave * 104729 + 13);
            float elapsed = 0f;

            // 다음 간격이 웨이브 타이머 안에 들어갈 때까지만 돈다 — 그래야 타이머가 끝나는
            // 순간에 맞춰 "더 올 증원 없음"이 되고, 그 직전에 이미 다 잡았다면 조기 클리어도
            // (증원 사이의 마지막 빈 틈에서) 그대로 성립한다.
            while (elapsed + comp.reinforceIntervalSeconds <= battleDuration)
            {
                yield return new WaitForSeconds(comp.reinforceIntervalSeconds);
                elapsed += comp.reinforceIntervalSeconds;
                SpawnReinforcementBatch(wave, comp, rng);
            }

            _hasPendingReinforcements = false;
            _reinforceCoroutine = null;
        }

        /// <summary>증원 한 무리 — 근거리/원거리를 절반씩(보스 없음) 기존 웨이브 배율 그대로 스폰한다.</summary>
        void SpawnReinforcementBatch(int wave, WaveMonsterComposition comp, System.Random rng)
        {
            if (balance == null) return;

            int meleeN = comp.reinforceCount / 2;
            int rangedN = comp.reinforceCount - meleeN;

            var queue = new List<(MonsterDefinitionSO def, MonsterUnit template)>();
            AppendToQueue(queue, meleeSlot, meleeN);
            AppendToQueue(queue, rangedSlot, rangedN);
            if (queue.Count == 0) return;

            List<Vector3Int> gates = BuildSpawnGates();
            for (int i = 0; i < queue.Count; i++)
            {
                Vector3Int gate = gates.Count > 0 ? gates[rng.Next(gates.Count)] : Vector3Int.zero;
                SpawnOne(queue[i].def, queue[i].template, gate, comp.statPercent, comp.statPercent, rng);
            }

            // 지금 광폭화 중이었다면(이론상 드묾 — 증원은 Battle 구간에서만 돌지만, 안전하게)
            // 방금 온 증원도 곧바로 같은 배율을 받는다.
            if (_currentEnragePercent != 100) SetEnragePercent(_currentEnragePercent);

            Debug.Log($"[MonsterSpawner] 웨이브 {wave} 증원 · {queue.Count}마리 (근거리{meleeN}/원거리{rangedN})", this);
        }

        public void ClearAll()
        {
            StopReinforcements();
            StopAllCoroutines();
            _spawning = false;
            for (int i = 0; i < _alive.Count; i++)
                if (_alive[i] != null) Destroy(_alive[i].gameObject);
            _alive.Clear();
        }

        // ------------------------------------------------------------------

        IEnumerator SpawnRoutine()
        {
            var rng = new System.Random(seed + waveNumber * 7919);
            List<Vector3Int> gates = BuildSpawnGates();

            int hpScale, atkScale;
            var queue = new List<(MonsterDefinitionSO def, MonsterUnit template)>();

            // 웨이브 테이블에 이 웨이브 행이 있으면 그 구성(마리 수·능력치 배율)을 그대로 쓴다.
            // 표가 없거나 행이 없으면(예전 씬 그대로) 기존 선형 공식 + 고정 spawnTable 로 돌아간다 —
            // 이 분기가 하나도 안 바뀌므로 웨이브 테이블을 아직 안 연결한 다른 상황을 깨지 않는다.
            if (waveDefinitions != null && waveDefinitions.TryGetWave(waveNumber, out var wave))
            {
                hpScale = atkScale = wave.statPercent;
                AppendToQueue(queue, meleeSlot, wave.meleeCount);
                AppendToQueue(queue, rangedSlot, wave.rangedCount);
                AppendToQueue(queue, bossSlot, wave.bossCount);
            }
            else
            {
                hpScale = 100 + hpPercentPerWave * (waveNumber - 1);
                atkScale = 100 + attackPercentPerWave * (waveNumber - 1);

                foreach (MonsterSpawnEntry e in spawnTable)
                    AppendToQueue(queue, e, e.count);
            }

            // 종류를 섞어서 한 방향에 같은 종류만 몰리지 않게 한다.
            for (int i = queue.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (queue[i], queue[j]) = (queue[j], queue[i]);
            }

            Debug.Log($"[MonsterSpawner] 웨이브 {waveNumber} 시작 · {queue.Count}마리 · " +
                      $"체력 {hpScale}% 공격 {atkScale}% · 게이트 {gates.Count}개", this);

            for (int i = 0; i < queue.Count; i++)
            {
                Vector3Int gate = gates.Count > 0
                    ? gates[rng.Next(gates.Count)]
                    : Vector3Int.zero;

                SpawnOne(queue[i].def, queue[i].template, gate, hpScale, atkScale, rng);

                if (spawnInterval > 0f) yield return new WaitForSeconds(spawnInterval);
            }

            _spawning = false;
        }

        /// <summary>정의+템플릿이 채워진 슬롯을 count 마리만큼 스폰 대기열에 넣는다.</summary>
        static void AppendToQueue(List<(MonsterDefinitionSO def, MonsterUnit template)> queue,
                                  MonsterSpawnEntry entry, int count)
        {
            if (entry.definition == null || count <= 0) return;
            MonsterUnit tpl = entry.template != null ? entry.template : entry.definition.template;
            for (int i = 0; i < count; i++) queue.Add((entry.definition, tpl));
        }

        void SpawnOne(MonsterDefinitionSO def, MonsterUnit template, Vector3Int gateCell,
                      int hpScale, int atkScale, System.Random rng)
        {
            if (template == null)
            {
                Debug.LogError($"[MonsterSpawner] {def.name} 에 연결된 템플릿이 없습니다. " +
                               "스폰 테이블의 Template 칸을 채워주세요.", this);
                return;
            }

            // 게이트 주변에서 빈 칸을 찾아 겹치지 않게 배치
            Vector3Int cell = gateCell;
            if (mapGenerator != null)
            {
                var jitter = new Vector3Int(gateCell.x + rng.Next(-2, 3),
                                            gateCell.y + rng.Next(-2, 3), 0);
                if (!mapGenerator.TryFindPlaceableNear(jitter, 8, null, out cell))
                    cell = gateCell;
            }

            MonsterUnit unit = Instantiate(template, CellCenter(cell),
                                           Quaternion.identity, _root);
            unit.name = $"{def.displayName}_{_alive.Count + 1}";
            unit.gameObject.SetActive(true);

            StatBlock scaled = def.BuildStats(hpScale, atkScale, balance.statMax);
            unit.Initialize(def, scaled, balance);

            // 크기 보정 — 보스는 대형 그리드
            if (def.footprintTiles > 1)
                unit.transform.localScale = Vector3.one * def.footprintTiles;

            // 정의 테이블의 전투 파라미터를 AI 에 주입
            var ai = unit.GetComponent<UnitCombat>();
            if (ai != null)
            {
                ai.Configure(def.detectRange, def.attackRange, def.moveSpeedTiles,
                             def.attacksPerSecond, advance: true, priority: def.TargetPriority);
                ai.SetHome(unit.transform.position);
            }

            _alive.Add(unit);
        }

        /// <summary>상하좌우 네 변의 중앙 부근을 스폰 게이트로 삼는다(맵 생성기가 뚫어둔 통로).</summary>
        List<Vector3Int> BuildSpawnGates()
        {
            var gates = new List<Vector3Int>();
            if (mapGenerator == null || mapGenerator.Config == null)
            {
                gates.Add(Vector3Int.zero);
                return gates;
            }

            Vector2Int size = mapGenerator.Config.MapSize;
            Vector2Int org = mapGenerator.Config.Origin;
            int mx = org.x + size.x / 2;
            int my = org.y + size.y / 2;
            int inset = Mathf.Clamp(edgeInset, 0, Mathf.Min(size.x, size.y) / 2 - 1);

            gates.Add(new Vector3Int(mx, org.y + inset, 0));                 // 하
            gates.Add(new Vector3Int(mx, org.y + size.y - 1 - inset, 0));    // 상
            gates.Add(new Vector3Int(org.x + inset, my, 0));                 // 좌
            gates.Add(new Vector3Int(org.x + size.x - 1 - inset, my, 0));    // 우
            return gates;
        }

        Vector3 CellCenter(Vector3Int cell) =>
            mapGenerator != null
                ? mapGenerator.CellCenterWorld(cell)
                : new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

        void Prune() => _alive.RemoveAll(m => m == null || !m.IsAlive);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            foreach (Vector3Int g in BuildSpawnGates())
                Gizmos.DrawWireSphere(CellCenter(g), 1.5f);
        }
    }
}
