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
    /// <b>등장 범위</b> (유저 확정 2026-08-13): 표(`임시용 중립 몬스터.xlsx`)의
    /// <c>spawn_range_min</c> / <c>spawn_range_max</c> 두 칸이 정본이고, 값은 <b>넥서스를 중심에 둔
    /// 원의 지름(타일)</b>이다("지름 15의 원에서부터 99의 원까지"). 판정은 <b>360도 원형(유클리드)</b> —
    /// 지름의 절반을 반지름으로 삼은 원형 고리 안에서만 나타난다
    /// (변환은 <see cref="NeutralMonsterDefinitionSO.MinDistanceFromNexus"/> 한 곳에서만 한다).
    ///
    /// ⚠ 예전에는 <c>spawn_range</c> 한 칸(n)을 받아 <b>n/2 를 체비셰프(정사각) 거리</b>로 쓰고
    ///   상한은 "한 단계 위 종의 하한"으로 <b>추론</b>했다. 이제 표에 상·하한이 명시돼 있으므로
    ///   그 추론(<c>BuildMaxDistanceTable</c>)을 없앴고, 정사각 링도 원형 고리로 바꿨다.
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

        void Start()
        {
            _rng = new System.Random(seed);
            _root = new GameObject("NeutralMonsters").transform;
            _root.SetParent(transform, false);

            AppendExtraDefinitions();

            if (spawnTable == null) return;
            foreach (NeutralSpawnEntry e in spawnTable)
                if (e.definition != null) _alive[e.definition] = new List<NeutralMonsterUnit>();

            WarnUnreachableRings();

            // 시작할 때는 상한까지 한 번에 채운다 — 리스폰이 아니라 최초 서식이므로 몰려도 티가 안 난다.
            RestockAll(fillToCapImmediately: true);
            StartCoroutine(RestockLoop());
        }

        /// <summary>
        /// 표에 적힌 고리가 <b>맵 안에 실제로 존재하는지</b> 한 번만 확인해 알려준다.
        ///
        /// 320×320 맵은 넥서스(중심)에서 축 방향으로 158타일, <b>모서리까지 약 223타일</b>이다.
        /// 표의 값은 <b>지름</b>이므로 그 절반이 반지름인데, 그 반지름이 223 을 넘으면
        /// 그 종은 <b>한 마리도 나오지 않는다</b> — 표만 보고는 알 수 없는 조건이라
        /// 콘솔에 남긴다(유저가 표를 조정할 근거).
        /// </summary>
        void WarnUnreachableRings()
        {
            float reach = MapMaxRadius();

            foreach (NeutralSpawnEntry e in spawnTable)
            {
                if (e.definition == null) continue;
                float min = e.definition.MinDistanceFromNexus;
                if (min < reach) continue;

                Debug.LogWarning(
                    $"[NeutralMonsterSpawner] {e.definition.name} 의 등장 최소 지름 " +
                    $"{e.definition.spawnRangeMinTiles:0}타일(= 반지름 {min:0})이 맵에서 닿을 수 있는 " +
                    $"최대 거리({reach:0}타일)보다 멀어 한 마리도 나올 수 없습니다. " +
                    "표(`임시용 중립 몬스터.xlsx` 의 spawn_range_min)를 낮춰주세요.", this);
            }
        }

        // ==================================================================
        // ★ 표에 새로 생긴 종을 <b>씬 배선 없이</b> 등록한다 (2026-08-15)
        //
        // <b>무엇이 문제였나</b> — <c>spawnTable</c> 은 <b>씬 오브젝트 참조</b>(template)를 가진
        // 구조체 배열이라 <b>MCP 로 채울 수 없다</b>(진행상황 8절 4번). 실제로 두 가지를
        // 시도해 둘 다 거절당했다:
        //     spawnTable      → "Expected object value for 'spawnTable'"
        //     에셋 참조 배열   → 같은 오류 (배열 자체가 안 된다)
        // 그래서 표에 종을 하나 추가할 때마다 유저가 인스펙터에서 손으로 슬롯을 만들어야 했다.
        //
        // <b>해법 — 캐릭터가 이미 쓰던 방식을 그대로 가져왔다.</b>
        // 캐릭터는 <c>Resources/Characters/</c> 를 통째로 읽어서 <b>씬 배선이 아예 필요 없다</b>
        // (84-6절: *"캐릭터는 Resources/Characters/ 를 자동으로 읽는 구조라 씬 배선이 필요 없다"*).
        // 중립 정의도 <c>Resources/NeutralMonsters/</c> 로 옮기고 같은 구조로 만들었다.
        //   · 정의 에셋 → <c>Resources.LoadAll</c> 로 전부 읽는다
        //   · 템플릿    → 정의 에셋 이름 + <c>_Template</c> 로 씬에서 찾는다
        //                 (<c>NeutralMonster_4</c> → <c>NeutralMonster_4_Template</c>)
        //
        // 결과: **표에 종을 추가하고 파싱만 돌리면 게임에 나온다.** 씬을 열 필요가 없다.
        //
        // ⚠ 템플릿은 <b>비활성</b>이라 <c>GameObject.Find</c> 로는 못 찾는다(UI-1절 함정 4).
        //   씬 루트를 직접 훑어 <c>Transform.Find</c> 로 내려간다.
        //
        // ⚠ 에셋을 옮길 때 <b>.meta 를 같이 옮겼다</b> — guid 가 유지되므로 씬의 기존
        //   <c>spawnTable</c> 슬롯 3개가 그대로 살아 있다(84절 폰트 이동과 같은 이유).
        // ==================================================================

        /// <summary>
        /// 중립 정의 에셋이 사는 Resources 하위 폴더.
        /// <c>CharacterDefinitionRegistry</c> 가 <c>Resources/Characters</c> 를 읽는 것과 같은 자리.
        /// </summary>
        public const string DefinitionResourceFolder = "NeutralMonsters";

        [Header("자동 등록")]
        [Tooltip("켜면 Resources/NeutralMonsters 의 정의를 전부 읽어, 위 spawnTable 에 없는 종을 " +
                 "<b>자동으로</b> 추가한다. 템플릿은 이름으로 찾는다.\n" +
                 "끄면 예전처럼 spawnTable 슬롯에 있는 종만 나온다")]
        [SerializeField] bool autoRegisterFromResources = true;

        [Tooltip("템플릿을 찾을 부모. 비워두면 씬에서 'Neutral_Templates' 를 이름으로 찾는다")]
        [SerializeField] Transform templateRoot;

        void AppendExtraDefinitions()
        {
            if (!autoRegisterFromResources) return;

            var loaded = Resources.LoadAll<NeutralMonsterDefinitionSO>(DefinitionResourceFolder);
            if (loaded == null || loaded.Length == 0)
            {
                Debug.LogWarning(
                    $"[NeutralMonsterSpawner] Resources/{DefinitionResourceFolder} 에 중립 정의가 " +
                    "하나도 없습니다. 에셋이 그 폴더에 있는지 확인해주세요.", this);
                return;
            }

            // ⚠ 등장 순서를 고정한다 — Resources.LoadAll 의 순서는 보장되지 않는다.
            //   난수 시드가 같아도 스폰 결과가 실행마다 달라지면 재현이 안 된다.
            System.Array.Sort(loaded, (a, b) => a.monId.CompareTo(b.monId));

            var list = new List<NeutralSpawnEntry>(spawnTable ?? new NeutralSpawnEntry[0]);

            foreach (NeutralMonsterDefinitionSO def in loaded)
            {
                if (def == null) continue;

                bool already = false;
                for (int i = 0; i < list.Count; i++)
                    if (list[i].definition == def) { already = true; break; }
                if (already) continue;

                NeutralMonsterUnit template = FindTemplateFor(def);
                if (template == null)
                {
                    Debug.LogWarning(
                        $"[NeutralMonsterSpawner] {def.name} 의 템플릿 '{def.name}_Template' 을 " +
                        "씬에서 찾지 못했습니다. Templates/Neutral_Templates 아래에 그 이름으로 " +
                        "만들어 주세요(다른 종 템플릿을 복제하면 됩니다).", this);
                    continue;
                }

                list.Add(new NeutralSpawnEntry
                {
                    definition = def,
                    template = template,
                    maxAlive = 0,          // 0 = 표(정의)의 maxAlive 를 쓴다 — CapFor 참조
                });
                Debug.Log($"[NeutralMonsterSpawner] {def.name} 자동 등록 (템플릿 {template.name})", this);
            }

            spawnTable = list.ToArray();
        }

        /// <summary>정의 에셋 이름 + <c>_Template</c> 로 씬에서 템플릿을 찾는다. 없으면 null.</summary>
        NeutralMonsterUnit FindTemplateFor(NeutralMonsterDefinitionSO def)
        {
            string wanted = def.name + "_Template";

            Transform root = templateRoot != null ? templateRoot : FindTemplateRoot();
            if (root != null)
            {
                Transform t = root.Find(wanted);          // ⚠ 비활성도 찾는다(GameObject.Find 와 다름)
                if (t != null) return t.GetComponent<NeutralMonsterUnit>();
            }

            // 부모를 못 찾았을 때의 마지막 수단 — 씬 전체에서 이름으로 훑는다.
            foreach (GameObject go in gameObject.scene.GetRootGameObjects())
            {
                Transform found = FindDeep(go.transform, wanted);
                if (found != null) return found.GetComponent<NeutralMonsterUnit>();
            }
            return null;
        }

        Transform FindTemplateRoot()
        {
            foreach (GameObject go in gameObject.scene.GetRootGameObjects())
            {
                Transform found = FindDeep(go.transform, "Neutral_Templates");
                if (found != null) return found;
            }
            return null;
        }

        static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeep(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

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
            float maxDist = entry.definition.MaxDistanceFromNexus;
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
                // ★ <b>중립 몬스터는 예외 없이 전부 비선공이다</b> (유저 확정 2026-08-15).
                //   맞기 전까지 공격하지 않고, 맞으면 반격한다
                //   (canAcquireTargets = false · canRetaliate = true).
                //
                // ⚠ 표의 <c>atk_take</c> 를 여기 쓰지 않는다 — 그 칸은 <b>선공 여부가 아니라
                //   무리 반격 여부</b>다(표의 한글 헤더가 처음부터 "동료 협공 여부"였다).
                //   71절이 이 칸을 선공으로 읽어 <c>aggressive</c> 에 넣고 있었고,
                //   그래서 1002·1003 이 선공으로 돌아다녔다. 아래 무리 배정이 정본이다.
                //
                // ⚠ <b>두 값을 여기서 반드시 덮어쓴다.</b> 템플릿 인스펙터에 뭐가 켜져 있든
                //   (실제로 _2_·_3_ 템플릿은 canAcquireTargets 가 1 이다) 코드가 이긴다.
                //
                // 타겟 우선순위도 항상 캐릭터로 준다 — 비선공이라도 <b>맞으면</b> 반격해야 하고,
                // 그때 우선순위가 비어 있으면 때린 상대를 못 고른다.
                //
                // ★ 공격 방식은 이제 <b>표의 atk_type</b> 을 따른다(유저 지시 2026-08-15).
                //   예전에는 근거리로 못박혀 있어서, 표에 <c>ranged</c> 라고 적힌 1002 도
                //   근거리로 붙어 싸웠다.
                ai.Configure(entry.definition.detectRange, entry.definition.attackRange,
                             entry.definition.moveSpeedTiles, entry.definition.attacksPerSecond,
                             advance: false,
                             priority: new[] { UnitKind.Character },
                             leash: entry.definition.leashRangeTiles,
                             type: entry.definition.attackType);
                ai.SetCanAcquireTargets(false);
                ai.SetCanRetaliate(true);
                ai.SetHome(unit.transform.position);
            }

            ApplyLook(unit, entry.definition);
            AssignPack(unit, entry.definition, pos);

            // 배회 — <b>자기가 소환될 수 있는 구간과 정확히 같은 고리</b> 안에서만 돌아다닌다
            // (유저 지시 2026-08-13: "중립 몬스터가 소환 가능한 범위 내에서만 배회하게 해줘").
            // ⚠ 여기서 넘기는 바깥 반지름은 <b>스폰에 쓴 것과 같은 값</b>이어야 한다 — 예전에는
            //   무한대를 넘기고 배회 쪽에서 임의의 60타일로 잘라, 스폰 범위와 배회 범위가
            //   서로 달랐다(최상위 종이 스폰 가능 구역 밖으로 걸어나갔다).
            //
            // ★ 세 번째 인자는 <b>고리 밖으로 쫓아갈 수 있는 거리</b>다(유저 확정 2026-08-13:
            //   "추적 범위까진 쫓아가고, 배회 가능 범위에서 추적 타일 거리까지 멀어지면
            //   추격 포기하고 복귀"). 표의 `leashRangeTiles` 를 그대로 쓴다 — 그 칸의 뜻이
            //   원래 "이 반경 밖의 적은 쫓지 않고 돌아온다" 라서 값이 두 개로 갈리지 않는다.
            var wander = unit.gameObject.GetComponent<NeutralMonsterWander>();
            if (wander == null) wander = unit.gameObject.AddComponent<NeutralMonsterWander>();

            // ★ 에픽은 <b>넥서스 고리가 아니라 자기 스폰 지점</b>이 기준이다 (유저 지시 2026-08-15).
            //   롤 정글 캠프처럼 자기 서식지 한가운데서 기다리다가, 맞으면 서식지 밖
            //   일정 거리까지만 쫓고 돌아온다.
            if (entry.definition.epic)
                wander.InitHabitat(pos,
                                   entry.definition.habitatRadiusTiles,
                                   entry.definition.habitatChaseTiles,
                                   entry.definition.habitatIdleSlackTiles);
            else
                wander.Init(minDist, ResolveOuterRadius(minDist, maxDist),
                            entry.definition.leashRangeTiles);

            PruneAndGet(entry.definition).Add(unit);
        }

        // ------------------------------------------------------------------
        // 외형 — 표의 mon_skin · collider_*_tiles (2026-08-15)
        //
        // 웨이브 스포너(<see cref="MonsterSpawner"/>)가 하는 것과 <b>같은 일, 같은 순서</b>다:
        //   ① 스킨을 붙이고 ② 콜라이더 상자를 넘겨 그림 크기를 맞춘다.
        //
        // ⚠ 순서가 중요하다 — 스킨이 없으면 <see cref="CharacterAnimator.SetColliderBoxTiles"/>
        //   가 잴 그림이 없다.
        //
        // 스킨을 안 적는 종(1001~1003)은 <see cref="CharacterAnimator"/> 자체가 템플릿에
        // 없으므로 이 함수가 조용히 아무것도 하지 않는다 — 예전 그대로 정적 스프라이트다.
        // ------------------------------------------------------------------

        void ApplyLook(NeutralMonsterUnit unit, NeutralMonsterDefinitionSO def)
        {
            if (def == null) return;

            var anim = unit.GetComponent<Combat.CharacterAnimator>();
            if (anim == null) return;

            // ① 스킨 — 표의 mon_skin 이 정본이다.
            //    ⚠ 템플릿의 skinResourceFolder 로도 같은 스킨이 잡히지만, 표에 적힌 쪽이 이긴다.
            //      두 곳이 어긋나면 "표를 고쳤는데 외형이 안 바뀐다" 가 된다.
            string path = def.SkinResourcePath;
            if (path.Length > 0)
            {
                var skin = Resources.Load<Combat.CharacterSkinSO>(path);
                if (skin != null) anim.SetSkin(skin);
                else
                    Debug.LogWarning(
                        $"[NeutralMonsterSpawner] {def.name} 의 스킨 'Resources/{path}' 을 " +
                        "찾지 못했습니다. 표의 mon_skin 값과 실제 폴더 이름을 확인해주세요.", unit);
            }

            // ② 크기 — 표의 콜라이더 상자(타일).
            if (def.HasColliderBox)
                anim.SetColliderBoxTiles(def.colliderWidthTiles, def.colliderHeightTiles);
        }

        // ------------------------------------------------------------------
        // 무리 (유저 지시 2026-08-15)
        //
        // *"일정 타일 범위 내에서 생성된 같은 개체의 몬스터는 동료로 인식하여 같은 부대로 묶인다"*
        //
        // 묶는 일을 <b>스폰 시점</b>에 하는 이유: 배회로 흩어진 뒤에 거리로 다시 묶으면
        // 무리가 프레임마다 바뀐다("지금 옆에 있으니 동료" → 걸어가면 남남). 태어난 자리로
        // 한 번 정하면 죽을 때까지 같은 무리다 — <b>정글 캠프</b>의 성질과도 맞는다.
        // ------------------------------------------------------------------

        [Header("무리")]
        [Tooltip("스폰 지점이 이 거리 안이고 <b>같은 종</b>이면 한 무리로 묶는다(타일).\n" +
                 "표에는 없는 값이다 — 손맛에 해당해 여기서 조정한다")]
        [Min(0f)] [SerializeField] float packMergeRadiusTiles = 10f;

        /// <summary>
        /// 갓 배치한 개체를 근처의 같은 종 무리에 넣거나, 없으면 새 무리를 연다.
        /// <c>group_making</c> 이 꺼져 있거나 <c>group_member</c> 가 0 이면 무리를 만들지 않는다
        /// (혼자 다니는 종 — 표의 1003·1004 가 그렇다).
        /// </summary>
        void AssignPack(NeutralMonsterUnit unit, NeutralMonsterDefinitionSO def, Vector3 pos)
        {
            var pack = unit.GetComponent<NeutralPack>();
            if (pack == null) pack = unit.gameObject.AddComponent<NeutralPack>();

            if (def == null || !def.groupMaking || def.groupMember <= 0)
            {
                pack.Assign(0, false);       // 무리 없음 — 맞으면 혼자 반격한다
                return;
            }

            int id = NeutralPack.FindNearbyPack(def, pos, packMergeRadiusTiles, def.groupMember);
            if (id == 0) id = NeutralPack.NewPackId();

            pack.Assign(id, def.packRetaliate);
        }

        /// <summary>
        /// 넥서스(셀 (0,0)) 중심 <b>원형 고리</b>(min ~ max 타일, 유클리드) 안에서 배치 가능한 칸을
        /// 무작위로 찾는다.
        ///
        /// ⚠ <b>고른 자리를 그대로 검사한다</b> — <see cref="MapGenerator.TryFindPlaceableNear"/> 가
        ///   벽을 피해 옆 칸으로 옮겨줄 수 있으므로, 옮겨진 뒤의 거리를 다시 재서 고리를 벗어났으면
        ///   버린다(옮기는 폭이 <see cref="placementFallbackRadius"/> 뿐이라 대부분 통과한다).
        /// </summary>
        bool TryFindSpawnCell(NeutralMonsterDefinitionSO def, float minDist, float maxDist, out Vector3Int result)
        {
            float outer = ResolveOuterRadius(minDist, maxDist);

            // 고리가 맵 모서리 쪽에만 걸치는 경우(하한이 맵 반지름보다 클 때) 각도 추첨이
            // 자주 헛돌기 때문에 시도 횟수를 넉넉히 잡는다.
            const int Attempts = 96;
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
                    RadiusFromNexus(placeable) >= minDist &&
                    RadiusFromNexus(placeable) <= outer)
                {
                    result = placeable;
                    return true;
                }
            }

            result = Vector3Int.zero;
            return false;
        }

        /// <summary>
        /// 이 종이 실제로 쓸 바깥 반지름(타일, 유클리드). 표의 상한이 맵 밖이면 맵 크기로 자른다.
        /// 상한이 무한대(표에 0)면 맵에서 닿을 수 있는 최대 거리까지 쓴다.
        /// </summary>
        float ResolveOuterRadius(float minDist, float maxDist)
        {
            float reach = MapMaxRadius();
            float outer = float.IsPositiveInfinity(maxDist) ? reach : Mathf.Min(maxDist, reach);
            return Mathf.Max(minDist + 1f, outer);
        }

        /// <summary>
        /// 넥서스(맵 중심)에서 맵 안의 한 점까지 나올 수 있는 <b>최대 유클리드 거리</b>(타일) —
        /// 정사각 맵의 모서리까지, 즉 반쪽 크기 × √2. 320×320 이면 약 226타일이다.
        /// 맵 참조가 없으면 최소거리 + 탐색 반경으로 폴백한다.
        /// </summary>
        float MapMaxRadius()
        {
            if (mapGenerator == null || mapGenerator.Config == null) return placementSearchRadius;

            float halfX = mapGenerator.Config.MapSize.x * 0.5f - 2f;
            float halfY = mapGenerator.Config.MapSize.y * 0.5f - 2f;
            return Mathf.Sqrt(halfX * halfX + halfY * halfY);
        }

        /// <summary>
        /// ★ <b>원형 고리(360도) 안에서 넓이 기준으로 균일하게 한 칸을 고른다</b>
        /// (유저 확정 2026-08-13: "넥서스 기준 타일 범위로 360도 원형").
        ///
        /// <b>왜 반지름을 √ 로 뽑는가</b> — 각도를 균일하게 뽑고 반지름을 그냥 <c>Lerp</c> 로
        /// 뽑으면 <b>안쪽이 좁고 바깥이 넓은데 같은 수를 뿌리게</b> 되어 개체가 넥서스 쪽으로
        /// 쏠린다(71-3절이 고쳤던 "중앙으로 모인다" 와 같은 종류의 편향이다).
        /// <c>r = √(lerp(min², max²))</c> 로 뽑으면 고리 넓이에 비례해 균일하게 퍼진다.
        ///
        /// ⚠ 예전 방식(체비셰프 정사각 링)에서 이걸로 바꾼 이유는 유저 지시 하나다 —
        ///   등장 범위가 <b>정사각 구역이 아니라 원형</b>이어야 한다.
        /// </summary>
        static Vector3Int SampleRingCell(System.Random rng, float minDist, float maxDist)
        {
            float t = (float)rng.NextDouble();
            float r = Mathf.Sqrt(Mathf.Lerp(minDist * minDist, maxDist * maxDist, t));
            float angle = (float)(rng.NextDouble() * System.Math.PI * 2.0);

            return new Vector3Int(Mathf.RoundToInt(Mathf.Cos(angle) * r),
                                  Mathf.RoundToInt(Mathf.Sin(angle) * r), 0);
        }

        /// <summary>넥서스(셀 (0,0))로부터의 유클리드 거리(타일).</summary>
        static float RadiusFromNexus(Vector3Int cell) => Mathf.Sqrt(cell.x * cell.x + cell.y * cell.y);

        void OnDrawGizmosSelected()
        {
            if (spawnTable == null) return;

            foreach (NeutralSpawnEntry e in spawnTable)
            {
                if (e.definition == null) continue;

                // 원형 고리 — 안쪽 원과 바깥쪽 원을 색을 나눠 그린다.
                Gizmos.color = new Color(0.6f, 1f, 0.3f, 0.6f);
                DrawCircle(e.definition.MinDistanceFromNexus);

                float max = e.definition.MaxDistanceFromNexus;
                if (float.IsPositiveInfinity(max)) continue;

                Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.4f);
                DrawCircle(max);
            }
        }

        static void DrawCircle(float radius)
        {
            const int Segments = 64;
            Vector3 prev = new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                Vector3 next = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
