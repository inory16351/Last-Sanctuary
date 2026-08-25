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

        [Tooltip("웨이브 테이블에 <b>boss_monster_id 가 없거나</b>(0) 그 id 가 아래 Boss Slots 에 " +
                 "없을 때 쓰는 <b>기본 보스</b>. 웨이브 표를 안 쓰는 예전 씬도 이 슬롯으로 돈다")]
        [SerializeField] MonsterSpawnEntry bossSlot;

        [Tooltip("★ <b>보스 명단</b> (2026-08-18 신설). 웨이브 표의 `boss_monster_id` 와 " +
                 "각 항목 정의 에셋의 `monsterId` 를 맞춰 <b>그 웨이브에 정해진 보스</b>를 고른다.\n\n" +
                 "<b>왜 바뀌었나</b> — 유저 지시로 <b>중간보스를 없애고</b> 5웨이브 단탈리온 / " +
                 "10웨이브 말파스로 바꾸면서 보스가 여러 종류가 됐다. 예전 구조(bossSlot 하나 + " +
                 "midBossSlots 가중치 추첨)로는 <b>어느 보스인지</b>를 표현할 수 없었다 — " +
                 "그리고 지시는 추첨이 아니라 <b>웨이브마다 정해진 보스</b>다.\n\n" +
                 "각 항목의 count 는 <b>쓰지 않는다</b>(마리 수는 웨이브 표의 boss_mon_num 이 정한다).")]
        [SerializeField] MonsterSpawnEntry[] bossSlots = System.Array.Empty<MonsterSpawnEntry>();

        [Header("맵 참조")]
        [SerializeField] MapGenerator mapGenerator;

        [Header("스폰 규칙")]
        [Tooltip("소환 주기를 자동으로 계산할 수 없을 때(웨이브 타이머 정보가 없을 때) 쓰는 " +
                 "고정 간격(초). 평소에는 아래 '소환 주기 자동 계산' 이 이 값을 대신한다")]
        [Min(0f)] [SerializeField] float spawnInterval = 0.5f;

        [Tooltip("맵 가장자리에서 안쪽으로 몇 칸 지점에 소환 포탈을 놓을지")]
        [Min(0)] [SerializeField] int edgeInset = 2;

        [Header("소환 포탈 (맵 가장자리의 랜덤 지점)")]
        [Tooltip("한 웨이브에 열리는 포탈 개수의 최솟값")]
        [Range(1, 4)] [SerializeField] int minPortalsPerWave = 1;

        [Tooltip("한 웨이브에 열리는 포탈 개수의 최댓값. 포탈이 몇 개든 그 웨이브의 " +
                 "총 개체수는 표대로 같다 — 나뉘어 들어올 뿐이다")]
        [Range(1, 4)] [SerializeField] int maxPortalsPerWave = 4;

        [Tooltip("포탈 하나가 차지하는 정사각 구역의 한 변(타일). 이 구역 안에서 몬스터가 나온다")]
        [Range(1, 9)] [SerializeField] int portalAreaTiles = 3;

        [Header("소환 주기 자동 계산")]
        [Tooltip("켜면 '마지막으로 소환된 몬스터가 웨이브 타이머 종료 직전에 성역에 닿도록' " +
                 "이동속도·포탈 거리로 간격을 역산한다. 끄면 위의 고정 spawnInterval 을 쓴다")]
        [SerializeField] bool autoSpawnInterval = true;

        [Tooltip("마지막 몬스터가 웨이브 타이머 종료보다 이만큼 먼저 도착하게 여유를 둔다(초). " +
                 "0 이면 도착과 동시에 타이머가 끝나 매 웨이브가 광폭화로 넘어간다 — " +
                 "마지막 무리를 정리할 시간을 남기려고 기본값을 둔다")]
        [Min(0f)] [SerializeField] float arrivalMarginSeconds = 8f;

        [Tooltip("벽을 피해 돌아가는 만큼 실제 이동거리가 직선거리보다 길다. 그 보정 배수")]
        [Range(1f, 2f)] [SerializeField] float pathDetourFactor = 1.15f;

        [Tooltip("자동 계산된 간격의 하한/상한(초). 물량이 아주 많거나 적을 때 극단값을 막는다")]
        [SerializeField] Vector2 spawnIntervalClamp = new Vector2(0.35f, 15f);

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

        /// <summary>증원 무리가 나올 포탈을 돌아가며 정하기 위한 순번.</summary>
        int _reinforceBatchIndex;

        /// <summary>이번 웨이브에 열린 포탈들. 초기 소환과 증원이 같은 포탈을 쓴다.</summary>
        readonly List<Vector3Int> _portals = new List<Vector3Int>();

        /// <summary>WaveManager 가 알려준 이번 웨이브의 전투 타이머 길이(초). 0 이면 모름.</summary>
        float _battleDuration;

        /// <summary>이번 웨이브에 열린 포탈 중심 셀. 미니맵 등에서 표시하고 싶으면 이걸 읽으면 된다.</summary>
        public IReadOnlyList<Vector3Int> CurrentPortals => _portals;

        public IReadOnlyList<MonsterUnit> Alive => _alive;
        public int AliveCount { get { Prune(); return _alive.Count; } }

        /// <summary>스폰 루틴이 아직 마리를 다 내보내지 않았으면 true. "전멸 판정"은 이게 꺼진 뒤에만 유효하다.</summary>
        public bool IsSpawning => _spawning;

        /// <summary>전투 중 증원이 더 오기로 예정돼 있으면 true. "전멸 판정"은 이게 꺼진 뒤에만 유효하다 —
        /// 안 그러면 증원 사이의 잠깐 조용한 틈에 웨이브가 조기 종료돼버린다.</summary>
        public bool HasPendingReinforcements => _hasPendingReinforcements;

        /// <summary>
        /// 그 웨이브에 보스가 나오는지 — <b>표를 보고</b> 판단한다.
        /// 배경음악(<c>BgmService</c>)·보스 체력바(<c>BossHealthPanel</c>)가 "보스가 실제로
        /// 스폰되기 전"에도 알아야 해서 살아있는 유닛이 아니라 웨이브 표를 근거로 쓴다.
        /// </summary>
        public bool IsBossWave(int wave) =>
            waveDefinitions != null &&
            waveDefinitions.TryGetWave(wave, out WaveMonsterComposition c) && c.bossCount > 0;

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

        /// <summary>
        /// 웨이브 번호와 <b>그 웨이브의 전투 타이머 길이</b>를 함께 받아 소환한다.
        /// 타이머 길이를 알아야 "마지막 몬스터가 타이머 종료 직전에 성역에 닿는" 소환 주기를
        /// 역산할 수 있다(<see cref="ResolveSpawnInterval"/>).
        /// </summary>
        public void SpawnWave(int wave, float battleDuration)
        {
            _battleDuration = Mathf.Max(0f, battleDuration);
            SpawnWave(wave);
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

            // 증원도 이번 웨이브에 이미 열려 있는 포탈에서 나온다 — 도중에 새 방향이
            // 생기면 플레이어가 대비해둔 전열이 의미를 잃는다.
            //
            // ★ 한 무리는 <b>한 포탈에서</b> 통째로 나온다(2026-08-13). 예전에는 PortalAt(i)
            //   로 마리마다 포탈을 돌려서, 증원 4마리가 네 방향에 한 마리씩 흩어졌다 —
            //   초기 소환에서 고친 "각개 격파" 문제와 정확히 같은 것이다. 어느 포탈에서
            //   나올지는 무리마다 돌아가며 정한다(_reinforceBatchIndex).
            Vector3Int portal = PortalAt(_reinforceBatchIndex++);
            int spread = GroupSpread(queue.Count);

            // ⚠⚠ <b>공격 배율은 체력 배율과 다른 열이다</b> (2026-08-24 고침).
            //   136-2절(S1)이 체력/공격 배율을 갈랐을 때 <see cref="SpawnRoutine"/> 만 고쳐서,
            //   <b>증원만</b> 공격 배율에 체력 배율을 쓰고 있었다 — 30웨이브에서 증원 한 마리의
            //   타격이 초기 소환분의 3.7배(3721% vs 1000%)였다. 폴백 규칙은 그쪽과 같다
            //   (열이 0 이면 statPercent — 그 열이 없던 옛 에셋이 예전처럼 동작한다).
            int atkScale = comp.attackPercent > 0 ? comp.attackPercent : comp.statPercent;

            for (int i = 0; i < queue.Count; i++)
                SpawnOne(queue[i].def, queue[i].template, portal,
                         comp.statPercent, atkScale, rng, spread);

            // 지금 광폭화 중이었다면(이론상 드묾 — 증원은 Battle 구간에서만 돌지만, 안전하게)
            // 방금 온 증원도 곧바로 같은 배율을 받는다.
            if (_currentEnragePercent != 100) SetEnragePercent(_currentEnragePercent);

            Debug.Log($"[MonsterSpawner] 웨이브 {wave} 증원 · {queue.Count}마리 (근거리{meleeN}/원거리{rangedN})", this);
        }

        /// <summary>
        /// ★★ <b>이벤트 보상 <c>summon_enemy</c> 가 쓰는 통로</b> (2026-08-24 신설) —
        /// 지금 웨이브의 <b>일반 몬스터</b>를 <paramref name="count"/> 마리 추가로 소환한다.
        ///
        /// 표(RewardType 시트): *"웨이브 포탈 위치에 현재 웨이브의 일반 몬스터를
        /// {value_01}마리 추가 소환합니다"*.
        ///
        /// ★ <b>증원과 완전히 같은 짜임</b>이다 — 근거리/원거리 절반씩 · 이미 열려 있는
        ///   포탈 하나에서 통째로 · 그 웨이브의 체력/공격 배율. 소환 규칙을 두 벌로 적으면
        ///   웨이브표 열이 하나 늘 때 한쪽을 반드시 빠뜨린다(준수사항 §10 H-3).
        /// ⚠ <b>보스는 섞이지 않는다</b> — «일반 몬스터» 라고 표가 못박았고, 보스는
        ///   웨이브표가 정하는 것이라 이벤트가 늘릴 것이 아니다.
        /// </summary>
        /// <returns>실제로 소환한 마리 수(0 이면 아무 일도 안 했다).</returns>
        public int SpawnExtraNormals(int count)
        {
            if (count <= 0 || balance == null) return 0;

            if (_root == null)
            {
                _root = new GameObject("Monsters").transform;
                _root.SetParent(transform, false);
            }

            // 배율은 «지금 웨이브» 의 것이다. 웨이브 정의가 없으면(무한 모드 밖 등) 아무것도 안 한다 —
            // 배율을 짐작해서 넣으면 이벤트 하나가 난이도 곡선을 조용히 흔든다.
            if (waveDefinitions == null ||
                !waveDefinitions.TryGetWave(waveNumber, out WaveMonsterComposition comp))
            {
                Debug.LogWarning($"[MonsterSpawner] 웨이브 {waveNumber} 의 정의가 없어 " +
                                 "이벤트 추가 소환을 건너뜁니다.", this);
                return 0;
            }

            int meleeN = count / 2;
            int rangedN = count - meleeN;

            var queue = new List<(MonsterDefinitionSO def, MonsterUnit template)>();
            AppendToQueue(queue, meleeSlot, meleeN);
            AppendToQueue(queue, rangedSlot, rangedN);
            if (queue.Count == 0) return 0;

            var rng = new System.Random(seed + waveNumber * 104729 + 977 + _alive.Count);
            Vector3Int portal = PortalAt(_reinforceBatchIndex++);
            int spread = GroupSpread(queue.Count);
            int atkScale = comp.attackPercent > 0 ? comp.attackPercent : comp.statPercent;

            for (int i = 0; i < queue.Count; i++)
                SpawnOne(queue[i].def, queue[i].template, portal,
                         comp.statPercent, atkScale, rng, spread);

            // 광폭화 중이면 방금 온 것들도 곧바로 같은 배율을 받는다(증원과 같은 이유).
            if (_currentEnragePercent != 100) SetEnragePercent(_currentEnragePercent);

            Debug.Log($"[MonsterSpawner] 이벤트 추가 소환 · {queue.Count}마리 " +
                      $"(근거리{meleeN}/원거리{rangedN}) · 웨이브 {waveNumber} 배율", this);
            return queue.Count;
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
            BuildWavePortals(waveNumber, rng);

            int hpScale, atkScale;
            var queue = new List<(MonsterDefinitionSO def, MonsterUnit template)>();
            int groupSize = 1;

            // 웨이브 테이블에 이 웨이브 행이 있으면 그 구성(마리 수·능력치 배율)을 그대로 쓴다.
            // 표가 없거나 행이 없으면(예전 씬 그대로) 기존 선형 공식 + 고정 spawnTable 로 돌아간다 —
            // 이 분기가 하나도 안 바뀌므로 웨이브 테이블을 아직 안 연결한 다른 상황을 깨지 않는다.
            if (waveDefinitions != null && waveDefinitions.TryGetWave(waveNumber, out var wave))
            {
                // ★★ 2026-08-24 — <b>체력과 공격의 배율을 따로 읽는다</b>(S1).
                //   예전에는 이 줄이 <c>hpScale = atkScale = wave.statPercent</c> 였다.
                //   공격 계열에는 상한이 있고(<c>monsterAttackStatMax</c>) 체력에는 없으므로,
                //   한 값을 둘에 넣으면 후반에 <b>공격만 상한에 붙어 얼어붙는다</b>
                //   (진행상황 135-1절 진단 ⑤⑥ — 잡몹 13웨이브 · 보스 20웨이브).
                //   ⚠ <c>attackPercent</c> 가 0 이면 예전과 똑같이 동작한다 — 그 열이 없던
                //     옛 에셋을 조용히 깨뜨리지 않기 위한 폴백이다.
                hpScale  = wave.statPercent;
                atkScale = wave.attackPercent > 0 ? wave.attackPercent : wave.statPercent;
                AppendToQueue(queue, meleeSlot, wave.meleeCount);
                AppendToQueue(queue, rangedSlot, wave.rangedCount);
                AppendBosses(queue, wave.bossCount, wave.bossMonsterId);
                groupSize = Mathf.Max(1, wave.spawnGroupSize);
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

            // ★ <b>무리 단위</b>로 내보낸다 (유저 지시 2026-08-13 — WaveMonsterComposition.
            //   spawnGroupSize 주석 참조). 주기는 <b>무리 개수</b>로 역산해야 마지막 무리가
            //   예전의 마지막 한 마리와 같은 시각에 도착한다 — 마리 수로 계산하면 무리 하나에
            //   한 번씩만 기다리므로 전체 소환이 groupSize 배 빨리 끝나버린다.
            int groupCount = Mathf.CeilToInt(queue.Count / (float)groupSize);
            float interval = ResolveSpawnInterval(groupCount);

            Debug.Log($"[MonsterSpawner] 웨이브 {waveNumber} 시작 · {queue.Count}마리 · " +
                      $"체력 {hpScale}% 공격 {atkScale}% · 포탈 {_portals.Count}개 · " +
                      $"무리 {groupSize}마리씩 {groupCount}무리 · " +
                      $"소환 주기 {interval:0.##}초 (총 {interval * Mathf.Max(0, groupCount - 1):0.#}초에 걸쳐 등장)",
                      this);

            // 포탈이 여러 개면 <b>무리 단위로</b> 돌아가며 배정한다. 예전에는 한 마리씩
            // 돌렸는데, 그러면 무리를 만들어도 그 안의 개체가 다시 사방으로 흩어져
            // "떼로 밀려온다"가 성립하지 않는다.
            for (int g = 0; g < groupCount; g++)
            {
                Vector3Int portal = PortalAt(g);
                int from = g * groupSize;
                int to = Mathf.Min(queue.Count, from + groupSize);

                for (int i = from; i < to; i++)
                    SpawnOne(queue[i].def, queue[i].template, portal, hpScale, atkScale, rng,
                             GroupSpread(to - from));

                if (interval > 0f) yield return new WaitForSeconds(interval);
            }

            _spawning = false;
        }

        /// <summary>
        /// 이번 웨이브의 소환 주기(초).
        ///
        /// <b>목표</b>(유저 요청): 마지막으로 소환된 몬스터가 <b>웨이브 타이머가 끝날 무렵</b>
        /// 성역에 닿는다. 그래야 전투 시간 내내 몬스터가 끊이지 않고 흘러 들어온다.
        ///
        /// <b>계산</b> — 웨이브 타이머는 "첫 전투가 벌어진 순간"부터 돈다(진행상황 11절).
        /// 첫 몬스터는 <c>t=0</c> 에 나와 <c>가장 가까운 포탈의 이동시간</c> 뒤에 닿고,
        /// 마지막 몬스터는 <c>(N-1)×간격</c> 에 나와 <c>가장 먼 포탈의 이동시간</c> 뒤에 닿는다.
        /// 둘이 만나야 하므로:
        /// <code>
        /// (N-1)×간격 + 이동_최대 = 이동_최소 + 전투시간 - 여유
        /// 간격 = (전투시간 - 여유 - (이동_최대 - 이동_최소)) / (N-1)
        /// </code>
        /// <paramref name="count"/> 가 1 이하이거나 전투시간을 모르면 고정값으로 돌아간다.
        /// </summary>
        float ResolveSpawnInterval(int count)
        {
            if (!autoSpawnInterval || count <= 1 || _battleDuration <= 0f) return spawnInterval;

            float speed = ReferenceMoveSpeed();
            if (speed <= 0f) return spawnInterval;

            float travelMin = float.PositiveInfinity;
            float travelMax = 0f;
            Vector3 nexus = CellCenter(mapGenerator != null ? mapGenerator.CenterCell : Vector3Int.zero);

            for (int i = 0; i < _portals.Count; i++)
            {
                float d = Vector2.Distance(CellCenter(_portals[i]), nexus) * pathDetourFactor;
                float t = d / speed;
                travelMin = Mathf.Min(travelMin, t);
                travelMax = Mathf.Max(travelMax, t);
            }
            if (float.IsPositiveInfinity(travelMin)) { travelMin = 0f; travelMax = 0f; }

            float window = _battleDuration - arrivalMarginSeconds - (travelMax - travelMin);
            float interval = window / (count - 1);

            return Mathf.Clamp(interval,
                               Mathf.Max(0f, spawnIntervalClamp.x),
                               Mathf.Max(spawnIntervalClamp.x, spawnIntervalClamp.y));
        }

        /// <summary>
        /// 소환 주기 계산의 기준이 되는 이동속도(타일/초) — 이번 웨이브 <b>본대</b>(근거리·원거리)의
        /// 느린 쪽을 쓴다. 보스는 웨이브당 한 마리뿐이고 훨씬 느려서(1.4) 기준으로 삼으면
        /// 전체 소환이 과하게 촘촘해진다.
        /// </summary>
        float ReferenceMoveSpeed()
        {
            float speed = 0f;
            if (meleeSlot.definition != null) speed = meleeSlot.definition.moveSpeedTiles;
            if (rangedSlot.definition != null)
                speed = speed > 0f ? Mathf.Min(speed, rangedSlot.definition.moveSpeedTiles)
                                   : rangedSlot.definition.moveSpeedTiles;

            if (speed <= 0f && spawnTable != null)
                foreach (MonsterSpawnEntry e in spawnTable)
                    if (e.definition != null)
                        speed = speed > 0f ? Mathf.Min(speed, e.definition.moveSpeedTiles)
                                           : e.definition.moveSpeedTiles;

            return speed;
        }

        /// <summary>정의+템플릿이 채워진 슬롯을 count 마리만큼 스폰 대기열에 넣는다.</summary>
        static void AppendToQueue(List<(MonsterDefinitionSO def, MonsterUnit template)> queue,
                                  MonsterSpawnEntry entry, int count)
        {
            if (entry.definition == null || count <= 0) return;
            MonsterUnit tpl = entry.template != null ? entry.template : entry.definition.template;
            for (int i = 0; i < count; i++) queue.Add((entry.definition, tpl));
        }
        /// <summary>
        /// 그 웨이브의 <b>보스</b>를 <paramref name="count"/> 마리 대기열에 넣는다.
        ///
        /// ★ <b>어느 보스인지는 표가 정한다</b> (2026-08-18) — <paramref name="bossMonsterId"/> 는
        /// 웨이브 표 <c>웨이브테이블.xlsx / Sheet2 / boss_monster_id</c> 그대로이고,
        /// <see cref="bossSlots"/> 에서 <c>definition.monsterId</c> 가 같은 항목을 찾아 쓴다.
        ///
        /// <b>왜 추첨이 아닌가</b> — 없어진 중간보스는 <c>spawn_percent</c> 로 가중치 추첨을
        /// 했지만, 유저 지시는 「5웨이브 단탈리온 · 10웨이브 말파스」로 <b>웨이브마다 정해진
        /// 보스</b>다. 추첨 구조를 물려받으면 표를 봐도 무엇이 나올지 알 수 없다.
        ///
        /// 못 찾으면 <see cref="bossSlot"/>(기본 보스)로 떨어진다 — 표에 id 를 아직 안 적은
        /// 웨이브나 웨이브 표를 안 쓰는 예전 씬에서 <b>보스가 통째로 사라지는 것</b>이
        /// "잘못된 보스가 나온다" 보다 알아채기 어렵기 때문이다(경고 로그를 남긴다).
        /// </summary>
        void AppendBosses(List<(MonsterDefinitionSO def, MonsterUnit template)> queue,
                          int count, int bossMonsterId)
        {
            if (count <= 0) return;

            MonsterSpawnEntry slot = ResolveBossSlot(bossMonsterId);
            if (slot.definition == null)
            {
                Debug.LogWarning($"[MonsterSpawner] 웨이브 {waveNumber} 에 보스 {count}마리가 " +
                                 $"예정돼 있는데(id {bossMonsterId}) Boss Slots · Boss Slot 어디에도 " +
                                 "정의가 없습니다.", this);
                return;
            }

            MonsterUnit tpl = ResolveBossTemplate(slot);
            for (int i = 0; i < count; i++) queue.Add((slot.definition, tpl));
        }

        /// <summary>
        /// 표의 보스 id → <see cref="bossSlots"/> 항목. 못 찾으면 <see cref="bossSlot"/>.
        /// </summary>
        MonsterSpawnEntry ResolveBossSlot(int bossMonsterId)
        {
            if (bossMonsterId > 0 && bossSlots != null)
            {
                for (int i = 0; i < bossSlots.Length; i++)
                {
                    MonsterDefinitionSO def = bossSlots[i].definition;
                    if (def != null && def.monsterId == bossMonsterId) return bossSlots[i];
                }

                if (bossSlot.definition == null || bossSlot.definition.monsterId != bossMonsterId)
                    Debug.LogWarning($"[MonsterSpawner] 웨이브 {waveNumber} 의 보스 id " +
                                     $"{bossMonsterId} 가 Boss Slots 에 없습니다 — 기본 보스로 " +
                                     "대신 내보냅니다.", this);
            }

            return bossSlot;
        }

        /// <summary>
        /// 보스가 쓸 외형 템플릿. 슬롯 → 정의 → <b>기본 보스 슬롯</b> → 같은 공격 타입의
        /// 잡몹 슬롯 순으로 찾는다.
        ///
        /// <b>왜 폴백이 필요한가</b> — 템플릿은 <b>씬 오브젝트 참조</b>라 ① 정의 에셋
        /// (ScriptableObject)에 넣을 수 없고(5절) ② MCP 로 씬의 배열 항목에 넣을 수도 없다
        /// (8절 4번). 폴백이 없으면 새 보스의 전용 템플릿을 손으로 연결할 때까지
        /// "템플릿이 없습니다" 만 뜨고 아무것도 안 나온다.
        ///
        /// 전용 템플릿이 연결되면 이 폴백은 자동으로 안 쓰인다.
        /// </summary>
        MonsterUnit ResolveBossTemplate(MonsterSpawnEntry entry)
        {
            if (entry.template != null) return entry.template;
            if (entry.definition != null && entry.definition.template != null)
                return entry.definition.template;

            if (bossSlot.template != null) return bossSlot.template;
            if (bossSlot.definition != null && bossSlot.definition.template != null)
                return bossSlot.definition.template;

            bool ranged = entry.definition != null &&
                          entry.definition.attackType != TacticalAttackType.Melee;
            MonsterSpawnEntry fallback = ranged ? rangedSlot : meleeSlot;
            return fallback.template != null ? fallback.template : fallback.definition?.template;
        }

        /// <summary>
        /// 무리 <paramref name="count"/> 마리가 흩어질 반경(타일).
        ///
        /// 포탈 구역(<see cref="portalAreaTiles"/>, 기본 3x3 = 9칸)은 원래 <b>한 마리씩</b>
        /// 나오던 시절에 정한 크기다. 무리가 7마리면 9칸에 몰려 한 덩어리로 겹쳐 나오고,
        /// 그 직후 밀어내기(separation)가 한꺼번에 걸려 사방으로 튄다. 무리 크기에 맞춰
        /// 반경을 넓혀 처음부터 벌어진 채로 나오게 한다.
        ///
        /// <c>ceil(√개수)</c> 는 "n 마리가 정사각으로 늘어설 한 변"이다 — 7마리면 3,
        /// 즉 반경 3(7x7칸)이라 겹칠 여지가 충분히 생긴다. 포탈 구역이 더 크게 설정돼
        /// 있으면 그쪽을 존중한다.
        /// </summary>
        int GroupSpread(int count) =>
            Mathf.Max(Mathf.Max(0, portalAreaTiles / 2),
                      Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, count))));

        void SpawnOne(MonsterDefinitionSO def, MonsterUnit template, Vector3Int portalCell,
                      int hpScale, int atkScale, System.Random rng, int spreadTiles = -1)
        {
            if (template == null)
            {
                Debug.LogError($"[MonsterSpawner] {def.name} 에 연결된 템플릿이 없습니다. " +
                               "스폰 테이블의 Template 칸을 채워주세요.", this);
                return;
            }

            // 포탈 구역(portalAreaTiles 정사각) 안의 아무 칸에서 나온다.
            // 구역은 통째로 비어 있는 것이 이미 확인됐지만(IsPortalAreaClear), 포탈을
            // 못 찾아 폴백으로 잡힌 자리일 수도 있어 배치 판정은 그대로 한 번 더 한다.
            Vector3Int cell = portalCell;
            if (mapGenerator != null)
            {
                int half = spreadTiles >= 0 ? spreadTiles : Mathf.Max(0, portalAreaTiles / 2);
                var jitter = new Vector3Int(portalCell.x + rng.Next(-half, half + 1),
                                            portalCell.y + rng.Next(-half, half + 1), 0);
                if (!mapGenerator.TryFindPlaceableNear(jitter, 8, null, out cell))
                    cell = portalCell;
            }

            MonsterUnit unit = Instantiate(template, CellCenter(cell),
                                           Quaternion.identity, _root);
            // 이름에 <b>일련번호를 붙이지 않는다</b>(유저 지시 2026-08-13) — 캐릭터
            // (CharacterUnit.ApplyDefinition: gameObject.name = def.DisplayName)와 같은 규칙이다.
            // 예전에는 "_1", "_2" 를 붙여 하이라키에서 구별했는데, 로그가 그 이름을 그대로 찍어
            // "지옥 송곳니_7 처치" 처럼 나왔다. 구별이 필요하면 하이라키의 순서·instanceId 로
            // 충분하고, 화면에 나가는 이름은 MonsterUnit.DisplayName 하나로 통일한다.
            unit.name = def.DisplayName;
            unit.gameObject.SetActive(true);

            // ★ 체력은 상한 없이 오르고 <b>공격 계열만</b> 상한에 걸린다
            //   (2026-08-19 · BalanceConfigSO.monsterAttackStatMax 위 주석).
            //   96-1절이 뗀 것은 <c>statMax</c>(캐릭터 강화 상한)이고, 이건 몬스터 전용
            //   상한이라 다른 값이다 — 같은 실수를 되풀이하는 것이 아니다.
            ConfigureSpawnedMonster(unit, def, def.BuildStats(hpScale, atkScale, balance));
        }

        /// <summary>
        /// 복제된 몬스터에 능력치·크기·AI 를 주입하고 살아있는 목록에 넣는다.
        ///
        /// <b>왜 갈라 뒀나</b> — 저장 복원(<see cref="RestoreMonster"/>)이 <b>똑같은 준비</b>를 해야
        /// 하는데, 다른 점은 "어디서 나오는가"(포탈 추첨 ↔ 저장된 좌표) 하나뿐이다.
        /// 같은 준비를 두 벌 적으면 표 컬럼이 하나 늘 때마다 한쪽을 반드시 빠뜨린다
        /// (준수사항 §10 H-3 — 같은 기능을 두 벌 만들지 않는다).
        /// </summary>
        void ConfigureSpawnedMonster(MonsterUnit unit, MonsterDefinitionSO def, StatBlock stats)
        {
            unit.Initialize(def, stats, balance);

            // 크기 보정 — <b>표의 콜라이더 상자(타일)</b>를 넘긴다(유저 확정 2026-08-13).
            // 애니메이터가 그 상자 안에 비율을 유지한 최대 크기로 그림을 맞추고, 콜라이더를
            // 다시 그 그림 크기로 맞춘다. 그래서 <b>같은 템플릿·같은 스킨을 쓰는 중간보스도
            // 이 한 줄로 커진다</b>(전용 템플릿이 없는 보스는 폴백을 쓴다 — ResolveBossTemplate).
            var anim = unit.GetComponent<Combat.CharacterAnimator>();
            if (anim != null && def.HasColliderBox)
            {
                anim.SetColliderBoxTiles(def.colliderWidthTiles, def.colliderHeightTiles);
            }
            else if (anim != null && def.RenderHeightTiles > 0f)
            {
                anim.SetRenderHeightTiles(def.RenderHeightTiles);   // 세로 전용 폴백
            }
            else
            {
                // 폴백 — 스킨(애니메이터)이 없는 유닛은 예전처럼 배율로만 키운다.
                float scale = def.EffectiveSpriteScale;
                if (!Mathf.Approximately(scale, 1f))
                    unit.transform.localScale = Vector3.one * scale;
            }

            // 정의 테이블의 전투 파라미터를 AI 에 주입
            var ai = unit.GetComponent<UnitCombat>();
            if (ai != null)
            {
                ai.Configure(def.detectRange, def.attackRange, def.moveSpeedTiles,
                             def.attacksPerSecond, advance: true, priority: def.TargetPriority,
                             type: def.attackType);
                ai.SetHome(unit.transform.position);
            }

            _alive.Add(unit);
        }

        // ==================================================================
        // 저장 복원 (2026-08-18 신설 — 98절)
        // ==================================================================

        /// <summary>
        /// 저장된 몬스터 한 마리를 <b>그 자리에 그 배율로</b> 되살린다.
        /// 정의는 <b>에셋 이름</b>으로 찾는다 — <c>MonsterDefinitionSO</c> 에는 id 칸이 없다.
        /// </summary>
        /// <returns>되살린 몬스터. 정의나 템플릿을 못 찾으면 null.</returns>
        public MonsterUnit RestoreMonster(string definitionName, Vector3 worldPos, StatBlock stats)
        {
            if (string.IsNullOrEmpty(definitionName)) return null;

            if (!TryFindSlot(definitionName, out MonsterDefinitionSO def, out MonsterUnit template))
            {
                Debug.LogWarning($"[MonsterSpawner] 저장된 몬스터 '{definitionName}' 의 정의를 " +
                                 "스폰 슬롯에서 찾지 못했습니다 — 이 마리는 복원하지 않습니다.", this);
                return null;
            }

            if (_root == null)
            {
                _root = new GameObject("Monsters").transform;
                _root.SetParent(transform, false);
            }

            MonsterUnit unit = Instantiate(template, worldPos, Quaternion.identity, _root);
            unit.name = def.DisplayName;
            unit.gameObject.SetActive(true);

            ConfigureSpawnedMonster(unit, def, stats);
            return unit;
        }

        /// <summary>에셋 이름으로 스폰 슬롯(정의 + 템플릿)을 찾는다. 슬롯 5종을 전부 뒤진다.</summary>
        bool TryFindSlot(string definitionName, out MonsterDefinitionSO def, out MonsterUnit template)
        {
            if (Match(meleeSlot, definitionName, out def, out template)) return true;
            if (Match(rangedSlot, definitionName, out def, out template)) return true;
            if (Match(bossSlot, definitionName, out def, out template)) return true;

            if (bossSlots != null)
                foreach (MonsterSpawnEntry e in bossSlots)
                    if (Match(e, definitionName, out def, out template)) return true;

            if (spawnTable != null)
                foreach (MonsterSpawnEntry e in spawnTable)
                    if (Match(e, definitionName, out def, out template)) return true;

            def = null;
            template = null;
            return false;
        }

        static bool Match(MonsterSpawnEntry entry, string definitionName,
                          out MonsterDefinitionSO def, out MonsterUnit template)
        {
            def = null;
            template = null;

            if (entry.definition == null || entry.definition.name != definitionName) return false;

            def = entry.definition;
            template = entry.template != null ? entry.template : entry.definition.template;
            return template != null;
        }

        /// <summary>
        /// 이번 웨이브의 소환 포탈을 새로 뽑는다 (유저 요청, 27절).
        ///
        /// 예전에는 네 변의 <b>정중앙</b> 네 곳이 고정 게이트였고 매 웨이브 그 넷을 모두 썼다.
        /// 이제는:
        ///   · 개수 = <see cref="minPortalsPerWave"/>~<see cref="maxPortalsPerWave"/> 랜덤(1~4)
        ///   · 위치 = 맵 가장자리의 <b>완전한 랜덤 지점</b>
        ///   · 크기 = <see cref="portalAreaTiles"/> 한 변의 정사각 구역(기본 3x3) — 그 안에서 나온다
        ///
        /// <b>변은 겹치지 않게 고른다.</b> 포탈을 나누는 목적이 "몬스터의 진군 방향을 갈라
        /// 전술적 재미를 준다"는 것이라, 같은 변에 두 개가 몰리면 그 의미가 없어진다.
        /// 그래서 네 변을 섞어 앞에서부터 필요한 개수만 쓴다(최대 4 = 한 변에 하나씩).
        ///
        /// <b>총 개체수는 포탈 수와 무관하다</b> — 웨이브 표의 마리 수를 포탈에 나눠 배분할 뿐이다.
        /// </summary>
        void BuildWavePortals(int wave, System.Random rng)
        {
            _portals.Clear();

            if (mapGenerator == null || mapGenerator.Config == null)
            {
                _portals.Add(Vector3Int.zero);
                return;
            }

            int want = Mathf.Clamp(rng.Next(Mathf.Min(minPortalsPerWave, maxPortalsPerWave),
                                            Mathf.Max(minPortalsPerWave, maxPortalsPerWave) + 1), 1, 4);

            // 네 변(0=하 1=상 2=좌 3=우)을 섞어 앞에서부터 want 개만 쓴다.
            var edges = new List<int> { 0, 1, 2, 3 };
            for (int i = edges.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (edges[i], edges[j]) = (edges[j], edges[i]);
            }

            for (int i = 0; i < want; i++)
                if (TryPickPortalOnEdge(edges[i], rng, out Vector3Int portal))
                    _portals.Add(portal);

            // 네 변 전부 실패하는 지형은 사실상 없지만, 그래도 하나는 있어야 소환이 된다.
            if (_portals.Count == 0) _portals.Add(mapGenerator.CenterCell);
        }

        /// <summary>
        /// 한 변 위의 랜덤 지점에서 <see cref="portalAreaTiles"/> 정사각 구역이 통째로 비어 있는
        /// 자리를 찾는다. 가장자리는 벽 밀도가 높아 한 번에 성공하지 못하는 경우가 흔하므로,
        /// 변을 따라 여러 번 다시 굴려보고 그래도 없으면 안쪽으로 조금씩 들어가며 찾는다.
        /// </summary>
        bool TryPickPortalOnEdge(int edge, System.Random rng, out Vector3Int portal)
        {
            portal = default;

            Vector2Int size = mapGenerator.Config.MapSize;
            Vector2Int org = mapGenerator.Config.Origin;
            int half = Mathf.Max(0, portalAreaTiles / 2);

            // 구역 전체가 맵 안에 들어와야 하므로 가장자리에서 최소 half 만큼은 띄운다.
            int baseInset = Mathf.Clamp(Mathf.Max(edgeInset, half + 1),
                                        half + 1, Mathf.Min(size.x, size.y) / 2 - 1);

            const int AlongAttempts = 24;    // 변을 따라 다시 굴려보는 횟수
            const int DepthSteps = 12;       // 안쪽으로 들어가며 시도하는 단계 수

            for (int depth = 0; depth < DepthSteps; depth++)
            {
                int inset = baseInset + depth * Mathf.Max(1, portalAreaTiles);

                for (int a = 0; a < AlongAttempts; a++)
                {
                    // 변을 따르는 좌표는 코너를 피해 [half+1, size-half-2] 안에서 완전 랜덤.
                    int alongMin = half + 1;
                    int alongMax = (edge <= 1 ? size.x : size.y) - half - 2;
                    if (alongMax <= alongMin) return false;
                    int along = rng.Next(alongMin, alongMax + 1);

                    Vector3Int c = edge switch
                    {
                        0 => new Vector3Int(org.x + along, org.y + inset, 0),                  // 하
                        1 => new Vector3Int(org.x + along, org.y + size.y - 1 - inset, 0),     // 상
                        2 => new Vector3Int(org.x + inset, org.y + along, 0),                  // 좌
                        _ => new Vector3Int(org.x + size.x - 1 - inset, org.y + along, 0),     // 우
                    };

                    if (!IsPortalAreaClear(c)) continue;

                    portal = c;
                    return true;
                }
            }
            return false;
        }

        /// <summary>포탈 구역이 통째로 배치 가능한지(맵 안 + 벽·구조물 아님).</summary>
        bool IsPortalAreaClear(Vector3Int center)
        {
            int half = Mathf.Max(0, portalAreaTiles / 2);
            for (int dy = -half; dy <= half; dy++)
                for (int dx = -half; dx <= half; dx++)
                    if (!mapGenerator.IsCellPlaceable(new Vector3Int(center.x + dx, center.y + dy, 0)))
                        return false;
            return true;
        }

        /// <summary>순번 <paramref name="index"/> 번째 몬스터가 나올 포탈 (포탈들을 돌아가며 씀).</summary>
        Vector3Int PortalAt(int index) =>
            _portals.Count > 0 ? _portals[index % _portals.Count] : Vector3Int.zero;

        Vector3 CellCenter(Vector3Int cell) =>
            mapGenerator != null
                ? mapGenerator.CellCenterWorld(cell)
                : new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

        void Prune() => _alive.RemoveAll(m => m == null || !m.IsAlive);

        /// <summary>이번 웨이브에 실제로 열린 포탈 구역을 그린다 (플레이 중에만 값이 있다).</summary>
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            float side = Mathf.Max(1, portalAreaTiles);
            for (int i = 0; i < _portals.Count; i++)
                Gizmos.DrawWireCube(CellCenter(_portals[i]), new Vector3(side, side, 0.1f));
        }
    }
}
