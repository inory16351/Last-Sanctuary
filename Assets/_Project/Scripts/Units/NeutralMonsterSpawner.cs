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
    /// ★★ <b>첫 등장만은 미룰 수 있다</b> (2026-08-24) — 표의 <c>first_spawn_delay</c> 가
    /// 0 보다 큰 종은 «게임 시작 + 그 초» 가 되어야 처음 나타난다(에픽 넷이 300초다).
    /// 자세한 이유는 아래 <see cref="_awaitingFirstSpawn"/> 의 ★★ 주석에 있다.
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

        // ==================================================================
        //  ★★ 에픽 서식지가 <b>판마다 같은 자리</b>에 생기던 문제 (2026-08-21)
        //
        //  유저 지시: *"청크가 고정되어있는거 테이블 값 확인해서 200~320 타일 범위 내에
        //  랜덤으로 서식지 생성해줘"*.
        //
        //  ⚠⚠ <b>범위는 처음부터 맞았다.</b> 표(`neutrality_mon`)의 에픽 4종은 전부
        //    `spawn_range_min 200` · `spawn_range_max 320` 이고, 추첨도 그 고리 안에서
        //    넓이 기준 균일하게 돌고 있었다(<see cref="SampleRingCell"/>).
        //
        //  <b>진짜 원인은 시드였다</b> — 이 스포너만 <c>new System.Random(seed)</c> 로
        //  <b>고정 시드</b>를 쓰고 있었다. 이 프로젝트의 다른 난수 서비스는 전부
        //  <c>randomizeSeed</c> 를 갖고 있고 기본값이 <b>켜짐</b>이다
        //  (<c>UnitSpawner</c>·<c>ErosionService</c>·<c>CharacterUpgradeService</c>·
        //   <c>MapGenerator</c>). 여기만 빠져 있어서 <b>판을 새로 시작해도 에픽이
        //  늘 같은 자리에 나왔다</b> — 유저가 «고정» 이라고 본 그것이다.
        // ==================================================================

        [Tooltip("★ 켜면 판마다 <b>다른 자리</b>에 나온다(기본). 끄면 아래 시드로 " +
                 "<b>항상 같은 배치</b>가 되어 재현 테스트에 쓸 수 있다.\n" +
                 "⚠ 이 프로젝트의 다른 난수 서비스와 같은 이름·같은 기본값이다")]
        [SerializeField] bool randomizeSeed = true;

        [Tooltip("<b>randomizeSeed 를 껐을 때만</b> 쓰는 고정 시드. " +
                 "같은 시드 = 항상 같은 배치라 버그 재현에 편하다")]
        [SerializeField] int seed = 20260805;

        [Header("에픽 서식지 — 서로 겹치지 않게")]
        [Tooltip("★ 에픽끼리 <b>서식지 중심</b>이 이만큼은 떨어지게 한다(타일). " +
                 "유저 지시: *\"중립 에픽 몬스터 당 거리를 둬서 둘이 겹치지 않게\"*.\n\n" +
                 "서식지 반경이 14 라면 두 원이 <b>닿지 않으려면 28</b>이 필요하고, " +
                 "«겹치지 않는다» 가 눈에 보이려면 여유가 더 있어야 한다.\n" +
                 "0 이면 검사하지 않는다(예전 동작)")]
        [Min(0f)] [SerializeField] float epicHabitatMinSeparationTiles = 70f;

        [Tooltip("떨어뜨릴 자리를 못 찾았을 때 <b>그래도 소환할지</b>. " +
                 "켜면 경고를 남기고 가장 멀리 떨어진 후보에 놓는다(에픽이 안 나오는 것보다 낫다). " +
                 "끄면 소환을 포기한다")]
        [SerializeField] bool spawnEpicEvenIfCrowded = true;

        Transform _root;
        System.Random _rng;
        readonly Dictionary<NeutralMonsterDefinitionSO, List<NeutralMonsterUnit>> _alive =
            new Dictionary<NeutralMonsterDefinitionSO, List<NeutralMonsterUnit>>();

        // ==================================================================
        // ★★ <b>첫 등장을 미루는 종</b> (2026-08-24 · 유저 지시
        //   *"에픽 보스 몬스터의 생성 시간을 게임 시작 이후 300초 뒤로 수정"*)
        //
        // 예전에는 <see cref="Start"/> 가 <b>전 종을 상한까지 한꺼번에</b> 채웠다
        // (`RestockAll(fillToCapImmediately: true)`). 그래서 에픽 넷이 <b>0초에</b>
        // 서식지까지 완성된 채로 서 있었고, 밸런스 기획서가 정한 «카르시노스 첫 조우 =
        // Lv10 1부대» 라는 시점을 게임이 통제할 수 없었다.
        //
        // ★ <b>표가 정본</b>이다 — 종마다 다른 지연을 줄 수 있게
        //   `first_spawn_delay`(NeutralMonsterDefinitionSO.firstSpawnDelaySeconds) 를 읽는다.
        //   0 인 종(잡몹 중립)은 <b>예전과 완전히 같이</b> 시작과 함께 나온다.
        //
        // 이 집합에 든 종은 «아직 한 번도 안 나온» 종이다. 지연이 끝나 처음 채울 때는
        // <b>상한까지 한꺼번에</b> 채운다(최초 서식이므로 나눠 채울 이유가 없다 — Start 와 같다).
        //
        // ⚠ <b>불러온 판</b>: 세이브에 이미 살아 있던 개체는 <see cref="RestoreNeutral"/> 로
        //   돌아오고, 그러면 상한이 차서 이 예약은 아무 일도 하지 않는다. 다만 «에픽이 아직
        //   안 나온 시점의 세이브» 를 불러오면 지연이 <b>불러온 시점부터</b> 다시 300초다 —
        //   판 시작 시각을 저장하지 않기 때문이다(restockInterval 도 원래 그렇게 동작한다).
        // ==================================================================
        readonly HashSet<NeutralMonsterDefinitionSO> _awaitingFirstSpawn =
            new HashSet<NeutralMonsterDefinitionSO>();

        /// <summary>
        /// 다음에 매길 <see cref="NeutralMonsterUnit.SpawnId"/>. 0 은 "아직 안 매김"의 뜻으로
        /// 남겨두고 <b>1부터</b> 매긴다 — 복원 때 <c>save.spawnId == 0</c> 이면 구버전 세이브
        /// (99-9절 직후, 아직 이 번호가 없던 시절)로 알아보기 위한 여지다.
        /// </summary>
        int _nextSpawnId = 1;

        void Start()
        {
            // ★ 판마다 다른 배치 — 위 ★★ 참조. 예전에는 고정 시드라 늘 같은 자리였다.
            _rng = new System.Random(randomizeSeed
                ? Random.Range(int.MinValue, int.MaxValue)
                : seed);
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

        /// <summary>
        /// 씬에서 이 종의 템플릿을 <b>이름으로</b> 찾는다. 없으면 null.
        ///
        /// ★ 찾는 이름이 두 개다 (유저 지시 2026-08-15: <i>"중립 몬스터 템플릿 또한 몬스터
        /// 이름에 맞게 수정"</i>):
        /// <code>
        ///   ① &lt;종 이름&gt;_Template        예: TumorSpider_Template   ← 지금 쓰는 이름
        ///   ② &lt;정의 에셋 이름&gt;_Template  예: NeutralMonster_1_Template  ← 예전 이름
        /// </code>
        /// <b>종 이름은 표의 <c>mon_skin</c></b> 에서 온다
        /// (<see cref="NeutralMonsterDefinitionSO.SpeciesName"/>) — 스킨 폴더
        /// <c>MonsterSkins/TumorSpider</c> 와 <b>같은 이름</b>이라 하이라키·에셋 폴더·표가
        /// 한 이름으로 묶인다. 예전에는 정의 에셋 이름(<c>NeutralMonster_1</c>)만 봤는데,
        /// 그 이름은 <b>몇 번째 종인지</b>만 알려줄 뿐 무엇인지 알려주지 않았다.
        ///
        /// ⚠ <b>②를 남겨두는 이유</b> — 정의 에셋 이름은 파이썬 파이프라인
        /// (<c>sync_tables_to_assets.py</c> 의 <c>NEUTRAL_ASSET_BY_ID</c>)이 쓰는 정본이라
        /// 그대로 두었다. 표에 <c>mon_skin</c> 을 아직 안 적은 종은 종 이름이 비므로
        /// ②로만 찾히고, 그래야 옛 씬도 그대로 돈다.
        /// </summary>
        NeutralMonsterUnit FindTemplateFor(NeutralMonsterDefinitionSO def)
        {
            string species = def.SpeciesName;
            string[] wanted = species.Length > 0
                ? new[] { species + "_Template", def.name + "_Template" }
                : new[] { def.name + "_Template" };

            Transform root = templateRoot != null ? templateRoot : FindTemplateRoot();
            if (root != null)
            {
                for (int i = 0; i < wanted.Length; i++)
                {
                    Transform t = root.Find(wanted[i]);   // ⚠ 비활성도 찾는다(GameObject.Find 와 다름)
                    if (t != null) return t.GetComponent<NeutralMonsterUnit>();
                }
            }

            // 부모를 못 찾았을 때의 마지막 수단 — 씬 전체에서 이름으로 훑는다.
            foreach (GameObject go in gameObject.scene.GetRootGameObjects())
                for (int i = 0; i < wanted.Length; i++)
                {
                    Transform found = FindDeep(go.transform, wanted[i]);
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
                    // ★★ 첫 등장을 미루는 종 — 위 ★★ 참조. 표의 first_spawn_delay 가 정본이다.
                    float delay = e.definition.firstSpawnDelaySeconds;
                    if (delay > 0f)
                    {
                        _nextRestockTime[e.definition] = Time.time + delay;
                        _awaitingFirstSpawn.Add(e.definition);
                        Debug.Log($"[NeutralMonsterSpawner] {e.definition.name} 은 게임 시작 " +
                                  $"{delay:0}초 뒤에 처음 나타납니다(표 first_spawn_delay).", this);
                        continue;
                    }

                    _nextRestockTime[e.definition] = Time.time + RestockSecondsFor(e.definition);
                }

                List<NeutralMonsterUnit> list = PruneAndGet(e.definition);
                int need = CapFor(e) - list.Count;
                if (need <= 0)
                {
                    // 상한이 이미 찼다면(세이브 복원 등) 예약은 소용이 없으므로 지운다.
                    _awaitingFirstSpawn.Remove(e.definition);
                    continue;
                }

                // ★ 지연이 끝나 <b>처음</b> 채우는 차례라면 Start 와 똑같이 상한까지 한꺼번에 채운다.
                bool firstFill = fillToCapImmediately || _awaitingFirstSpawn.Remove(e.definition);

                int spawnNow = firstFill ? need : Mathf.Min(need, maxSpawnPerRestock);
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

            // 서식지 씨앗은 <b>여기서 뽑는다</b> — 스포너의 난수는 게임을 켤 때 새로 시작하므로
            // 같은 자리에 소환돼도 모양이 달라진다(유저 지시 2026-08-15). 저장 복원은 이 값을
            // 그대로 되돌려 주므로 <b>불러온 판에서는 모양이 안 바뀐다</b>.
            ConfigureSpawnedNeutral(unit, entry, pos, cell, minDist, maxDist, _rng.Next(), spawnId: 0);
        }

        /// <summary>
        /// 복제된 중립 몬스터에 능력치·AI·외형·무리·배회·서식지를 주입하고 살아있는 목록에 넣는다.
        ///
        /// <b>왜 갈라 뒀나</b> — 저장 복원(<see cref="RestoreNeutral"/>)이 <b>똑같은 준비</b>를
        /// 해야 하는데 다른 점은 "어디서 나오고 어떤 씨앗을 쓰는가" 뿐이다. 같은 준비를 두 벌
        /// 적으면 표 컬럼이 하나 늘 때마다 한쪽을 반드시 빠뜨린다(준수사항 §10 H-3).
        /// <see cref="MonsterSpawner.ConfigureSpawnedMonster"/> 가 같은 이유로 같은 모양이다.
        /// </summary>
        /// <param name="spawnId">
        /// 0 이면 새 번호를 매긴다. 복원은 <b>저장된 번호를 그대로</b> 넘긴다 — 토벌 발견 목록과
        /// 부대 토벌 지시가 그 번호로 개체를 다시 찾는다(<see cref="NeutralMonsterUnit.SpawnId"/>).
        /// </param>
        void ConfigureSpawnedNeutral(NeutralMonsterUnit unit, NeutralSpawnEntry entry,
                                     Vector3 pos, Vector3Int cell,
                                     float minDist, float maxDist, int habitatSeed, int spawnId)
        {
            // ⚠ 복원한 번호가 이미 매긴 번호보다 크면 다음 번호를 그 위로 밀어야 한다 —
            //   안 그러면 복원 뒤에 새로 소환된 개체가 <b>복원된 개체와 같은 번호</b>를 받아
            //   토벌 목록이 엉뚱한 마리를 가리킨다.
            if (spawnId > 0) _nextSpawnId = Mathf.Max(_nextSpawnId, spawnId + 1);
            else spawnId = _nextSpawnId++;

            unit.AssignSpawnId(spawnId);

            // 이름에 <b>일련번호를 붙이지 않는다</b> (유저 지시 2026-08-15: "동일 개체면 다
            // 해당 개체 이름으로 나오게"). 웨이브 몬스터가 2026-08-13 에 이미 같은 규칙으로
            // 바뀌었는데(<see cref="MonsterSpawner"/> 의 <c>unit.name = def.DisplayName</c>)
            // <b>중립만 빠져 있었다</b> — 그래서 로그에 "종양 거미_4821 처치" 처럼 나왔다.
            // 하이라키에서 개체를 구별하는 데는 순서·instanceId 로 충분하고, 화면에 나가는
            // 이름은 표의 <c>mon_name</c>(스트링 키) 하나로 통일한다.
            unit.name = entry.definition.DisplayName;
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
            {
                wander.InitHabitat(pos,
                                   entry.definition.habitatRadiusTiles,
                                   entry.definition.habitatChaseTiles,
                                   entry.definition.habitatIdleSlackTiles);
                PaintHabitat(unit, entry.definition, cell, habitatSeed);

                // ★ 스킬 — 표(mon_skill_1·2)에 값이 있는 종만 캐스터를 붙인다
                //   (유저 지시 2026-08-15: "에픽 몬스터 스킬 구현(카르시노스)").
                //   컴포넌트는 <b>웨이브 보스와 같은 것</b>이다 — 조준·범위·연출 코드를
                //   두 벌로 만들지 않으려고 BossSkillCaster 를 종에 무관하게 고쳤다
                //   (Combat.IBossSkillOwner 주석 참조).
                if (entry.definition.HasSkills &&
                    unit.GetComponent<Combat.BossSkillCaster>() == null)
                    unit.gameObject.AddComponent<Combat.BossSkillCaster>();
            }
            else
            {
                wander.Init(minDist, ResolveOuterRadius(minDist, maxDist),
                            entry.definition.leashRangeTiles);
            }

            PruneAndGet(entry.definition).Add(unit);
        }

        // ==================================================================
        // 저장 복원 (2026-08-18 신설 — 99절, 유저 지시
        //   <i>"중립 몬스터의 소환된 숫자와 서식지 위치는 유지하는 로직으로 만들어줘"</i>)
        //
        // ★ <b>서식지는 칸을 저장하지 않는다.</b> 모양이 (중심 칸 · 반지름 · 씨앗) 셋으로
        //   완전히 결정되므로(<see cref="NeutralHabitat"/>), 그 셋만 되돌리면 수천 칸이
        //   같은 모양으로 다시 그려진다. 반지름은 표에 있으니 실제로 저장할 것은 <b>둘</b>이다.
        //
        // ★ <b>개체를 하나씩 저장하므로 "소환된 숫자"가 저절로 유지된다.</b> 마리 수를 따로
        //   세어 저장하고 복원 때 그만큼 새로 뽑는 방법도 있지만, 그러면 <b>있던 자리가 아니라
        //   아무 데나</b> 다시 태어난다 — 유저가 같이 요구한 "서식지 위치 유지"와 어긋난다.
        // ==================================================================

        /// <summary>살아있는 중립 전부. 저장할 때 훑는다.</summary>
        public IEnumerable<NeutralMonsterUnit> AliveAll()
        {
            foreach (var pair in _alive)
            {
                List<NeutralMonsterUnit> list = pair.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    NeutralMonsterUnit unit = list[i];
                    if (unit != null && unit.IsAlive) yield return unit;
                }
            }
        }

        /// <summary>
        /// 지금 있는 중립을 전부 없앤다 (복원 직전에 판을 비우는 용도).
        ///
        /// ★★ <b>파괴하기 전에 서식지를 반드시 <see cref="NeutralHabitat.Restore"/> 로
        /// 즉시 되돌려야 한다.</b> 안 그러면 <b>새로 그린 서식지가 7.5초에 걸쳐 지워진다.</b>
        ///
        /// 이유 — <see cref="NeutralHabitat.OnDestroy"/> 는 <b>페이드아웃 연출</b>을 시작한다
        /// (96-3절, "저그 점막이 걷히듯"). 그 연출은 몬스터가 죽는 즉시 파괴되는 탓에
        /// <see cref="HabitatFadeOut"/> 이라는 <b>독립 오브젝트</b>가 이어받아
        /// <c>fadeSpreadSeconds(6) + fadeCellSeconds(1.5)</c> = <b>7.5초 동안</b> 칸을 하나씩
        /// 훑으며 <b>원래(서식지 이전) 타일로 되돌린다.</b>
        ///
        /// 그런데 복원은 <b>같은 중심 · 같은 씨앗</b>으로 새 서식지를 <b>곧바로</b> 그린다 —
        /// 즉 <b>정확히 같은 칸</b>이다. 그래서 그 7.5초 동안 연출이 <b>방금 그린 서식지를
        /// 뒤에서 지워 나간다.</b> 불러오면 서식지가 보였다가 스르르 사라지고 에픽이 맨땅에 선다.
        ///
        /// <see cref="NeutralHabitat.Restore"/> 는 <b>동기적으로</b> 되돌리고 칸 목록을 <b>비운다</b>.
        /// 목록이 비면 뒤이어 도는 <c>OnDestroy</c> 의 <see cref="HabitatFadeOut.Begin"/> 이
        /// "되돌릴 칸이 없다"로 즉시 빠져나가므로(그 함수 첫 줄의 <c>anything</c> 검사),
        /// <b>나중에 끼어들 기록자가 아예 안 생긴다.</b>
        ///
        /// ⚠ 이것이 <b>미결 230번</b>("페이드 중에 같은 자리에 새 서식지가 그려지면 겹친다")이
        /// 실제로 터지는 경로다 — 그 절은 카르시노스 재생성이 600초라 안 부딪힌다고 봤지만,
        /// <b>저장 복원은 0초 만에 같은 자리에 다시 그린다.</b>
        /// </summary>
        public void ClearAllForRestore()
        {
            foreach (var pair in _alive)
            {
                List<NeutralMonsterUnit> list = pair.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    NeutralMonsterUnit unit = list[i];
                    if (unit == null) continue;

                    var habitat = unit.GetComponent<NeutralHabitat>();
                    if (habitat != null) habitat.Restore();

                    Destroy(unit.gameObject);
                }
                list.Clear();
            }
        }

        /// <summary>
        /// 저장된 중립 한 마리를 <b>그 자리에 그 서식지로</b> 되살린다.
        /// 정의는 표의 <c>monId</c> 로 찾는다 — 웨이브 몬스터와 달리 중립 정의에는 id 칸이 있다.
        /// </summary>
        /// <returns>되살린 개체. 정의나 템플릿을 못 찾으면 null.</returns>
        public NeutralMonsterUnit RestoreNeutral(int monId, Vector3 worldPos, Vector3 homePos,
                                                 bool hasHabitat, Vector3Int habitatCell,
                                                 int habitatSeed, int spawnId)
        {
            if (!TryFindEntry(monId, out NeutralSpawnEntry entry))
            {
                Debug.LogWarning($"[NeutralMonsterSpawner] 저장된 중립 id {monId} 의 정의를 " +
                                 "스폰 표에서 찾지 못했습니다 — 이 마리는 복원하지 않습니다.", this);
                return null;
            }

            NeutralMonsterUnit template =
                entry.template != null ? entry.template : entry.definition.template;
            if (template == null) return null;

            if (_root == null)
            {
                _root = new GameObject("NeutralMonsters").transform;
                _root.SetParent(transform, false);
            }

            NeutralMonsterUnit unit = Instantiate(template, worldPos, Quaternion.identity, _root);

            // ★ 서식지 중심은 <b>지금 서 있는 자리가 아니라 저장된 중심</b>이다 — 에픽은 맞으면
            //   서식지 밖까지 쫓아 나가므로, 저장된 순간 자리를 중심으로 삼으면 서식지가
            //   불러올 때마다 조금씩 밀려난다.
            Vector3Int cell = hasHabitat
                ? habitatCell
                : (mapGenerator != null ? mapGenerator.WorldToCell(worldPos) : Vector3Int.zero);

            ConfigureSpawnedNeutral(unit, entry, homePos, cell,
                                    entry.definition.MinDistanceFromNexus,
                                    entry.definition.MaxDistanceFromNexus,
                                    habitatSeed, spawnId);

            // ⚠ ConfigureSpawnedNeutral 은 <b>배회 기준점</b>을 잡으려고 homePos 를 받았다.
            //   실제 서 있어야 할 자리는 저장된 위치이므로 마지막에 옮긴다.
            unit.transform.position = worldPos;

            return unit;
        }

        /// <summary>표의 <c>monId</c> 로 스폰 표의 줄을 찾는다.</summary>
        bool TryFindEntry(int monId, out NeutralSpawnEntry entry)
        {
            if (spawnTable != null)
            {
                foreach (NeutralSpawnEntry e in spawnTable)
                {
                    if (e.definition == null || e.definition.monId != monId) continue;
                    entry = e;
                    return true;
                }
            }

            entry = default;
            return false;
        }

        // ------------------------------------------------------------------
        // 재생성 대기 시각 (2026-08-18 신설 — 유저 지시 "타이머 넣어서")
        //
        // ★ <b>왜 이것까지 저장해야 하는가</b> — <see cref="_nextRestockTime"/> 은
        //   <c>Time.time</c>(씬이 열린 뒤 흐른 시간) 기준이라, 씬을 새로 부르면 <b>0 부터
        //   다시 시작한다</b>. 그러면 저장 시점에 죽어 있던 카르시노스는 <b>죽은 지 얼마나
        //   됐는지를 잊고</b> 불러온 순간부터 다시 재생성 주기(600초)를 꽉 기다린다 —
        //   재생성 직전에 저장했다가 불러오면 기다림이 통째로 되돌려지는 셈이다.
        //
        // ⚠ 남은 <b>초</b>로 담는다(절대 시각이 아니다). 씬마다 <c>Time.time</c> 의 원점이
        //   다르므로 시각을 그대로 적으면 아무 뜻이 없는 숫자가 된다.
        // ------------------------------------------------------------------

        /// <summary>종별 남은 재생성 대기 시간(초)을 <b>두 목록에 짝지어</b> 담는다.</summary>
        public void ExportRestockDelays(List<int> monIds, List<float> secondsRemaining)
        {
            if (monIds == null || secondsRemaining == null || spawnTable == null) return;

            foreach (NeutralSpawnEntry e in spawnTable)
            {
                if (e.definition == null) continue;
                if (!_nextRestockTime.TryGetValue(e.definition, out float at)) continue;

                monIds.Add(e.definition.monId);
                secondsRemaining.Add(Mathf.Max(0f, at - Time.time));
            }
        }

        /// <summary>
        /// 저장된 남은 대기 시간을 되돌린다. <see cref="Start"/> 가 이미 자기 주기를 잡아둔
        /// 뒤에 불려도 되도록 <b>덮어쓴다</b>.
        ///
        /// ⚠ 남은 시간을 그 종의 주기로 <b>자른다</b> — 표의 <c>respawn_seconds</c> 를 줄이는
        /// 개정이 있으면 옛 세이브의 값이 새 주기보다 길 수 있고, 그러면 표를 고쳤는데도
        /// 옛 세이브에서만 한참 안 나오는 상태가 된다.
        /// </summary>
        public void ImportRestockDelays(IReadOnlyList<int> monIds, IReadOnlyList<float> secondsRemaining)
        {
            if (monIds == null || secondsRemaining == null || spawnTable == null) return;

            int pairs = Mathf.Min(monIds.Count, secondsRemaining.Count);
            for (int i = 0; i < pairs; i++)
            {
                if (!TryFindEntry(monIds[i], out NeutralSpawnEntry entry)) continue;

                float wait = Mathf.Clamp(secondsRemaining[i], 0f, RestockSecondsFor(entry.definition));
                _nextRestockTime[entry.definition] = Time.time + wait;
            }
        }

        // ------------------------------------------------------------------
        // 서식지 바닥 그리기 (유저 지시 2026-08-15)
        //
        // *"매 게임 시작 카르시노스가 소환 될때마다 새로운 서식지 타일 에셋들이 섞여서
        //   서식지 디자인이 매 게임마다 조금씩 달라지도록"*
        //
        // 모양을 만드는 일은 전부 <see cref="NeutralHabitat"/> 에 있다 — 여기서는
        // <b>씨앗을 주는 것</b>만 한다. 씨앗을 스포너의 난수에서 뽑는 것이 핵심이다:
        // 그 난수는 게임을 켤 때 새로 시작하므로 같은 자리에 소환돼도 모양이 달라진다.
        //
        // ⚠ 타일 묶음은 <b>표(habitat_design 시트)</b>가 정한다. 표에 안 적힌 종은
        //   폴더가 비어 조용히 아무것도 안 그린다 — 예전 동작 그대로다.
        // ------------------------------------------------------------------

        [Header("서식지")]
        [Tooltip("에픽 개체를 소환할 때 서식지가 몇 칸으로 그려졌는지 콘솔에 남긴다")]
        [SerializeField] bool logHabitat = true;

        /// <summary>종별 서식지 타일 묶음 캐시. 개체마다 Resources 를 다시 읽지 않으려는 것.</summary>
        readonly Dictionary<string, UnityEngine.Tilemaps.TileBase[]> _habitatTiles =
            new Dictionary<string, UnityEngine.Tilemaps.TileBase[]>();

        void PaintHabitat(NeutralMonsterUnit unit, NeutralMonsterDefinitionSO def, Vector3Int cell,
                          int seed)
        {
            if (mapGenerator == null) return;

            // ★★ <b>에픽인데 타일 이름이 비어 있으면 알린다</b> (2026-08-19, 유저 리포트:
            //   *"아니사킬의 서식지가 생성되지 않는다"*).
            //
            //   ⚠ <b>이 버그는 콘솔에 흔적을 하나도 안 남겼다.</b> 표(`habitat_design`)에
            //   아니사킬 줄이 없어서 `habitatTileAsset` 이 빈 칸이었는데, 그러면
            //   <see cref="NeutralMonsterDefinitionSO.HabitatTileResourcePath"/> 가 <c>""</c> 를
            //   돌려주고 <see cref="LoadHabitatTiles"/> 는 <b>첫 줄에서</b> null 로 빠진다 —
            //   경고를 찍는 코드는 그 아래에 있어서 <b>도달하지 못한다.</b> 그래서 아래
            //   <c>ground == null</c> 가 조용히 return 하고, <b>눈으로만</b> 발견된다.
            //
            //   빈 칸 자체는 <b>일반 종에게는 정상</b>이다(서식지가 없는 종이 대부분이다).
            //   그래서 "빈 칸이면 경고" 가 아니라 <b>에픽인데 빈 칸이면 경고</b>다 —
            //   에픽의 정의가 "서식지를 갖는 보스형" 이므로 그때만 모순이 된다.
            if (def.epic && def.HabitatTileResourcePath.Length == 0)
                Debug.LogWarning(
                    $"[NeutralMonsterSpawner] {def.DisplayName}({def.monId}) 는 에픽인데 " +
                    "서식지 타일 이름이 비어 있어 서식지를 그리지 않습니다. 표 " +
                    "(`임시용 중립 몬스터.xlsx` habitat_design 시트)에 이 id 의 " +
                    "habitat_tile_asset 을 적고 sync_tables_to_assets.py 를 돌려주세요.", unit);

            // 바닥이 없으면 서식지 자체를 안 그린다. 가장자리·데코는 없어도 된다
            // (없으면 각각 바닥 타일로 대체 / 데코 생략 — NeutralHabitat.Paint 참조).
            var ground = LoadHabitatTiles(def.HabitatTileResourcePath, def, unit, required: true);
            if (ground == null || ground.Length == 0) return;

            var edge = LoadHabitatTiles(def.HabitatEdgeResourcePath, def, unit, required: false);
            var props = LoadHabitatTiles(def.HabitatPropResourcePath, def, unit, required: false);

            var habitat = unit.gameObject.GetComponent<NeutralHabitat>();
            if (habitat == null) habitat = unit.gameObject.AddComponent<NeutralHabitat>();

            // ⚠ 씨앗을 <b>여기서 뽑지 않는다</b> — 부르는 쪽이 준다. 저장 복원이 같은 씨앗을
            //   넘겨 같은 모양을 다시 그려야 하기 때문이다(NeutralHabitat._seed 주석).
            habitat.Paint(mapGenerator, ground, edge, props, cell,
                          def.habitatRadiusTiles, seed);

            if (logHabitat)
                Debug.Log($"[NeutralMonsterSpawner] {def.DisplayName} 서식지 " +
                          $"{habitat.PaintedCells}칸 · 데코 {habitat.PropCells}개 " +
                          $"(반지름 {def.habitatRadiusTiles}타일 · 바닥 {ground.Length}종 · " +
                          $"가장자리 {(edge != null ? edge.Length : 0)}종 · " +
                          $"데코 {(props != null ? props.Length : 0)}종)", unit);
        }

        /// <summary>서식지 타일 묶음 하나를 읽어 캐시한다. 없으면 null.</summary>
        UnityEngine.Tilemaps.TileBase[] LoadHabitatTiles(
            string path, NeutralMonsterDefinitionSO def, NeutralMonsterUnit unit, bool required)
        {
            if (string.IsNullOrEmpty(path)) return null;

            if (!_habitatTiles.TryGetValue(path, out var tiles))
            {
                tiles = Resources.LoadAll<UnityEngine.Tilemaps.TileBase>(path);
                _habitatTiles[path] = tiles;

                if (required && (tiles == null || tiles.Length == 0))
                    Debug.LogWarning(
                        $"[NeutralMonsterSpawner] {def.name} 의 서식지 타일 'Resources/{path}' 이 " +
                        "비어 있습니다. 표의 habitat_tile_asset 값과 실제 폴더 이름을 확인해주세요.", unit);
            }
            return tiles;
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
        /// 넥서스(셀 (0,0)) 중심 <b>정사각 고리</b>(변 min ~ 변 max 타일) 안에서 배치 가능한 칸을
        /// 무작위로 찾는다.
        ///
        /// ★★ <b>2026-08-16 — 원형에서 정사각형으로 바꿨다</b> (유저 확정).
        /// <i>"맵 모서리에 몬스터 안 생기는 문제는 몬스터 생성 가능 / 배회 범위를 사각형으로
        /// 생성하게 하는 로직으로 해결하자. 변이 15인 정사각형에서부터 변이 99인 정사각형까지
        /// — 이러면 맵 끝까지 꽉차게 생성 가능하니까"</i>
        ///
        /// <b>왜 필요했나</b> — 표의 상한이 <b>변 320</b>(=반지름 160)이고 판정이 유클리드
        /// 원이었는데, 정사각 맵의 <b>모서리는 226타일</b>이다. 그래서 반지름 160~226 사이의
        /// 네 모서리 <b>22,021칸(맵의 21.5%)</b>이 규칙상 절대 후보가 되지 못했다(86-9절).
        /// 판정을 <b>체비셰프 거리</b>(max(|x|,|y|))로 바꾸면 "변 N 인 정사각형"이 그대로
        /// 표현되고, 변 320 이 곧 맵 전체라 <b>모서리까지 꽉 찬다.</b>
        ///
        /// ⚠ <b>표 값의 뜻은 안 바뀐다.</b> 여전히 "지름"이 아니라 <b>한 변의 길이</b>로 읽으면
        /// 되고(15 → 넥서스에서 ±7.5), 숫자를 하나도 고칠 필요가 없다 —
        /// 원형일 때 지름을 반으로 나누던 자리에서 이제 <b>변을 반으로</b> 나눈다.
        ///
        /// ⚠ <b>고른 자리를 그대로 검사한다</b> — <see cref="MapGenerator.TryFindPlaceableNear"/> 가
        ///   벽을 피해 옆 칸으로 옮겨줄 수 있으므로, 옮겨진 뒤의 거리를 다시 재서 고리를 벗어났으면
        ///   버린다(옮기는 폭이 <see cref="placementFallbackRadius"/> 뿐이라 대부분 통과한다).
        /// </summary>
        bool TryFindSpawnCell(NeutralMonsterDefinitionSO def, float minDist, float maxDist, out Vector3Int result)
        {
            float outer = ResolveOuterRadius(minDist, maxDist);

            // ★ 에픽은 <b>서로 떨어져야</b> 한다 — 아래 ★★ 참조. 일반 중립은 검사하지 않는다
            //   (수십 마리가 고리를 채우는 종이라 서로 떨어뜨릴 개념이 없다).
            bool separate = def.epic && epicHabitatMinSeparationTiles > 0f;

            // 떨어뜨리기에 실패했을 때 쓸 «그래도 가장 나은» 후보 — 이웃과 가장 멀리 떨어진 칸.
            Vector3Int best = Vector3Int.zero;
            float bestGap = -1f;

            // 고리가 맵 모서리 쪽에만 걸치는 경우 추첨이 자주 헛돌기 때문에 넉넉히 잡는다.
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

                if (!mapGenerator.TryFindPlaceableNear(candidate, placementFallbackRadius, null,
                                                       out Vector3Int placeable) ||
                    RadiusFromNexus(placeable) < minDist ||
                    RadiusFromNexus(placeable) > outer)
                    continue;

                if (!separate)
                {
                    result = placeable;
                    return true;
                }

                // 이미 서 있는 에픽들과 얼마나 떨어졌는가.
                float gap = NearestEpicHabitatDistance(placeable, def);
                if (gap >= epicHabitatMinSeparationTiles)
                {
                    result = placeable;
                    return true;
                }
                if (gap > bestGap)
                {
                    bestGap = gap;
                    best = placeable;
                }
            }

            // ⚠ 여기까지 왔으면 «떨어진 자리» 를 못 찾았다. 조용히 실패하지 않는다.
            if (separate && bestGap >= 0f)
            {
                if (spawnEpicEvenIfCrowded)
                {
                    Debug.LogWarning(
                        $"[중립] {def.DisplayName} — 다른 에픽과 " +
                        $"{epicHabitatMinSeparationTiles:0}타일 떨어진 자리를 못 찾았습니다. " +
                        $"가장 먼 후보({bestGap:0.#}타일)에 놓습니다. " +
                        "간격을 줄이거나 등장 범위를 넓혀주세요.", this);
                    result = best;
                    return true;
                }

                Debug.LogWarning(
                    $"[중립] {def.DisplayName} — 다른 에픽과 떨어진 자리를 못 찾아 소환을 " +
                    $"건너뜁니다(가장 먼 후보 {bestGap:0.#}타일 < {epicHabitatMinSeparationTiles:0}). " +
                    "spawnEpicEvenIfCrowded 를 켜면 그래도 소환합니다.", this);
            }

            result = Vector3Int.zero;
            return false;
        }

        /// <summary>
        /// ★★ <b>지금 살아 있는 다른 에픽의 서식지 중심까지의 최단 거리</b>(타일).
        /// 비교할 에픽이 하나도 없으면 <see cref="float.PositiveInfinity"/>.
        ///
        /// 유저 지시: *"중립 에픽 몬스터 당 거리를 둬서 둘이 겹치지 않게 로직 구성해줘"*.
        ///
        /// ★ <b>따로 목록을 들지 않는다</b> — «살아 있는 개체» 에서 매번 읽는다.
        ///   별도 리스트를 두면 죽음·재생성·저장 복원마다 <b>지우는 것을 잊는 자리</b>가
        ///   셋 생기고, 하나만 빠뜨려도 «있지도 않은 에픽 때문에 자리를 못 찾는» 상태가 된다.
        ///   에픽은 종당 1마리(표 <c>max_alive</c>)라 훑을 것이 네 마리뿐이다.
        ///
        /// ★ 기준점은 <b>서식지 중심</b>이지 «지금 서 있는 자리» 가 아니다 — 에픽은 맞으면
        ///   서식지 밖까지 쫓아 나가므로(<c>habitatChaseTiles</c>) 현재 위치로 재면
        ///   교전 중에 값이 출렁인다. 저장 코드가 같은 이유로 같은 판단을 했다
        ///   (<c>GameSnapshot</c> 의 «서식지 중심이 정본이다»).
        ///
        /// <param name="self">지금 놓으려는 종. <b>자기 자신은 빼지 않는다</b> — 같은 종이
        ///   둘 이상 나올 수 있는 표라면(max_alive 2) 그 둘도 떨어져야 맞다.</param>
        /// </summary>
        float NearestEpicHabitatDistance(Vector3Int cell, NeutralMonsterDefinitionSO self)
        {
            Vector3 world = mapGenerator != null
                ? mapGenerator.CellCenterWorld(cell)
                : new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

            float nearest = float.PositiveInfinity;

            foreach (KeyValuePair<NeutralMonsterDefinitionSO, List<NeutralMonsterUnit>> pair in _alive)
            {
                if (pair.Key == null || !pair.Key.epic) continue;

                List<NeutralMonsterUnit> list = pair.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    NeutralMonsterUnit unit = list[i];
                    if (unit == null || !unit.IsAlive) continue;

                    var wander = unit.GetComponent<NeutralMonsterWander>();
                    Vector3 center = wander != null && wander.IsHabitatMode
                        ? wander.HabitatCenter
                        : unit.transform.position;

                    float d = Vector2.Distance(world, center);
                    if (d < nearest) nearest = d;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 이 종이 실제로 쓸 <b>바깥 반변</b>(정사각형 한 변의 절반, 타일).
        /// 표의 상한이 맵 밖이면 맵 크기로 자른다. 상한이 무한대(표에 0)면 맵 끝까지 쓴다.
        /// </summary>
        float ResolveOuterRadius(float minDist, float maxDist)
        {
            float reach = MapMaxRadius();
            float outer = float.IsPositiveInfinity(maxDist) ? reach : Mathf.Min(maxDist, reach);
            return Mathf.Max(minDist + 1f, outer);
        }

        /// <summary>
        /// 넥서스(맵 중심)에서 맵 안의 한 점까지 나올 수 있는 <b>최대 체비셰프 거리</b>(타일) —
        /// 즉 <b>맵 반쪽 크기</b>다. 320×320 이면 158타일(경계벽 2칸을 뺐다).
        ///
        /// ★ 예전에는 여기에 √2 를 곱해 226 을 돌려줬다(유클리드 모서리까지의 거리). 판정이
        /// 정사각형으로 바뀌면서 <b>모서리와 축 방향이 같은 거리</b>가 됐으므로 곱할 것이 없다.
        /// </summary>
        float MapMaxRadius()
        {
            if (mapGenerator == null || mapGenerator.Config == null) return placementSearchRadius;

            float halfX = mapGenerator.Config.MapSize.x * 0.5f - 2f;
            float halfY = mapGenerator.Config.MapSize.y * 0.5f - 2f;
            return Mathf.Min(halfX, halfY);
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
        /// <summary>
        /// <b>정사각 고리</b>에서 칸 하나를 고르게 뽑는다 (2026-08-16 — 유저 확정으로 원형에서 교체).
        ///
        /// 방법: 먼저 <b>어느 정사각 테두리</b>인지를 고르고(반변 r), 그 테두리 위의 한 점을 고른다.
        /// <code>
        ///   ① r = sqrt(lerp(min², max², t))   ← 넓이 균등. 안쪽으로 쏠리지 않게 하는 부분이다
        ///   ② 그 테두리(한 변 2r)의 둘레 위 한 점을 균등하게 고른다
        /// </code>
        ///
        /// ★ ①의 <b>제곱근</b>이 핵심이다. r 을 그냥 균등하게 뽑으면 <b>안쪽 테두리에 몰린다</b> —
        /// 정사각 테두리의 길이가 r 에 비례해 자라므로 바깥쪽일수록 칸이 많은데 뽑을 확률은
        /// 같기 때문이다. 원형일 때 쓰던 것과 같은 보정이고, 71절이 "중앙으로 모인다"로
        /// 잡았던 문제와 같은 종류다.
        ///
        /// ②는 둘레를 네 변으로 갈라 그중 하나를 고르고 그 변 위에서 균등하게 뽑는다.
        /// (각도로 뽑으면 모서리 쪽이 성기게 나온다 — 각도와 둘레가 비례하지 않는다.)
        /// </summary>
        static Vector3Int SampleRingCell(System.Random rng, float minDist, float maxDist)
        {
            float t = (float)rng.NextDouble();
            float r = Mathf.Sqrt(Mathf.Lerp(minDist * minDist, maxDist * maxDist, t));

            // 테두리 위의 위치 — 네 변 중 하나를 고르고 그 변에서 균등하게.
            int side = rng.Next(4);
            float u = (float)(rng.NextDouble() * 2.0 - 1.0) * r;   // -r ~ +r

            float x, y;
            switch (side)
            {
                case 0: x = u;  y = r;  break;      // 위
                case 1: x = u;  y = -r; break;      // 아래
                case 2: x = r;  y = u;  break;      // 오른쪽
                default: x = -r; y = u; break;      // 왼쪽
            }

            return new Vector3Int(Mathf.RoundToInt(x), Mathf.RoundToInt(y), 0);
        }

        /// <summary>
        /// 넥서스(셀 (0,0))로부터의 <b>체비셰프 거리</b>(타일) — max(|x|, |y|).
        ///
        /// 이 값이 곧 "그 칸이 올라앉은 정사각형의 반변" 이다. 즉 <b>변이 N 인 정사각형 안</b>
        /// = <c>RadiusFromNexus(cell) &lt;= N/2</c> 로 정확히 표현된다.
        /// </summary>
        static float RadiusFromNexus(Vector3Int cell) =>
            Mathf.Max(Mathf.Abs(cell.x), Mathf.Abs(cell.y));

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
