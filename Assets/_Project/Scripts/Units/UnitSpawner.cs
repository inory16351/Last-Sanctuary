using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Map;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 최초 생성 담당 (핵심시스템 기획서 p13).
    ///   1. 맵 중앙에 중앙 건물(넥서스) 생성
    ///   2. 넥서스 근처에 캐릭터 생성
    ///
    /// 생성 방식: 하이라키(또는 프리팹)의 템플릿 오브젝트를 Instantiate 로 복제하고
    /// 능력치 테이블 값을 주입한다. 템플릿에 애니메이터·콜라이더·자식 오브젝트를 붙이면
    /// 생성되는 모든 유닛이 그대로 물려받으므로, 나중에 확장할 때 코드를 고칠 필요가 없다.
    ///
    /// Instantiate 는 씬 오브젝트와 프리팹 에셋을 모두 받으므로, 템플릿을 프리팹으로
    /// 승격시켜도 코드는 그대로 동작한다.
    /// </summary>
    public class UnitSpawner : MonoBehaviour
    {
        [Header("데이터")]
        [SerializeField] BalanceConfigSO balance;
        [SerializeField] NexusDefinitionSO nexusDefinition;

        [Header("템플릿 (하이라키 오브젝트 또는 프리팹)")]
        [Tooltip("복제할 캐릭터 원본. 비활성 상태로 두면 템플릿 자체는 게임에 참여하지 않는다")]
        [SerializeField] CharacterUnit characterTemplate;

        [Tooltip("복제할 넥서스 원본")]
        [SerializeField] Nexus nexusTemplate;

        [Header("맵 참조")]
        [Tooltip("배치 가능한 칸을 판정하는 데 사용. 없으면 원점 주변에 그냥 배치한다")]
        [SerializeField] MapGenerator mapGenerator;

        [Header("캐릭터 생성")]
        [Min(0)] [SerializeField] int characterCount = 3;

        [Tooltip("넥서스로부터 몇 칸 떨어진 곳부터 배치를 시도할지")]
        [Min(1)] [SerializeField] int spawnRingRadius = 3;

        [Tooltip("배치 가능한 칸을 찾을 최대 탐색 반경")]
        [Min(1)] [SerializeField] int maxSearchRadius = 12;

        [Header("랜덤")]
        [Tooltip("같은 시드 = 항상 같은 능력치. 버그 재현과 밸런싱 테스트에 필요")]
        [SerializeField] int seed = 20260803;

        [Tooltip("켜면 실행할 때마다 다른 능력치가 나온다")]
        [SerializeField] bool randomizeSeed = false;

        [Header("실행")]
        [SerializeField] bool spawnOnStart = true;

        // 생성 결과
        public Nexus SpawnedNexus { get; private set; }
        public List<CharacterUnit> SpawnedCharacters { get; } = new List<CharacterUnit>();

        /// <summary>
        /// 캐릭터가 하나 생성될 때마다 발생. 최초 3명과 <see cref="SpawnOneCharacter"/> 로
        /// 추가 생성된 캐릭터 모두 여기로 온다 — UI 가 목록을 갱신할 때 쓴다.
        /// </summary>
        public event System.Action<CharacterUnit> OnCharacterSpawned;

        Transform _unitsRoot;

        // 능력치 추첨용 난수열. 최초 생성과 추가 생성이 같은 난수열을 이어 써야
        // "같은 시드 = 같은 결과" 가 유지된다.
        System.Random _rng;

        // 이미 유닛이 놓인 칸(넥서스 발판 포함). 추가 생성 때도 겹치지 않게 하려면
        // 지역 변수가 아니라 스포너가 계속 들고 있어야 한다.
        readonly HashSet<Vector3Int> _usedCells = new HashSet<Vector3Int>();

        void Start()
        {
            if (spawnOnStart) SpawnAll();
        }

        // ------------------------------------------------------------------

        /// <summary>넥서스와 캐릭터를 모두 생성한다.</summary>
        public void SpawnAll()
        {
            if (!Validate()) return;

            Clear();
            _unitsRoot = new GameObject("Units").transform;
            _unitsRoot.SetParent(transform, false);

            Vector3Int centerCell = mapGenerator != null ? mapGenerator.CenterCell : Vector3Int.zero;

            SpawnNexus(centerCell);
            SpawnCharacters(centerCell);
        }

        /// <summary>생성된 유닛을 모두 제거한다. 템플릿은 건드리지 않는다.</summary>
        public void Clear()
        {
            SpawnedCharacters.Clear();
            SpawnedNexus = null;
            _usedCells.Clear();

            if (_unitsRoot == null) return;
            if (Application.isPlaying) Destroy(_unitsRoot.gameObject);
            else DestroyImmediate(_unitsRoot.gameObject);
            _unitsRoot = null;
        }

        bool Validate()
        {
            if (balance == null)
            {
                Debug.LogError("[UnitSpawner] Balance Config 가 연결되지 않았습니다.", this);
                return false;
            }
            if (nexusDefinition == null)
            {
                Debug.LogError("[UnitSpawner] Nexus Definition 이 연결되지 않았습니다.", this);
                return false;
            }
            if (characterTemplate == null)
            {
                Debug.LogError("[UnitSpawner] Character Template 이 연결되지 않았습니다. " +
                               "하이라키의 Character_Template 을 넣어주세요.", this);
                return false;
            }
            if (nexusTemplate == null)
            {
                Debug.LogError("[UnitSpawner] Nexus Template 이 연결되지 않았습니다.", this);
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------

        void SpawnNexus(Vector3Int centerCell)
        {
            Nexus nexus = Instantiate(nexusTemplate, CellCenter(centerCell),
                                      Quaternion.identity, _unitsRoot);
            nexus.name = "Nexus";
            nexus.gameObject.SetActive(true);       // 템플릿이 비활성이어도 복제본은 활성화
            nexus.Initialize(nexusDefinition, balance);

            SpawnedNexus = nexus;
            Debug.Log($"[UnitSpawner] 넥서스 생성 · {nexus.DebugSummary()}", nexus);
        }

        void SpawnCharacters(Vector3Int centerCell)
        {
            _rng = new System.Random(
                randomizeSeed ? Random.Range(int.MinValue, int.MaxValue) : seed);

            // 넥서스가 차지하는 칸은 제외
            int half = Mathf.Max(1, nexusDefinition.footprintTiles) / 2;
            for (int dy = -half; dy <= half; dy++)
                for (int dx = -half; dx <= half; dx++)
                    _usedCells.Add(new Vector3Int(centerCell.x + dx, centerCell.y + dy, 0));

            for (int i = 0; i < characterCount; i++)
                SpawnCharacterAt(centerCell, i, characterCount);
        }

        /// <summary>
        /// 캐릭터 한 명을 추가로 생성한다 — <b>UI(캐릭터 생성 패널)가 호출하는 유일한 접점.</b>
        ///
        /// 비용 소모·생성 가능 판정은 여기서 하지 않는다. 그건 게임 규칙이고, 이 프로젝트는
        /// 규칙과 입력을 분리하는 방식을 이미 쓰고 있다(<c>CharacterUpgradeService</c> ↔
        /// <c>UpgradeButtonUI</c>). 호출하는 쪽이 <c>ResourceManager.TrySpend</c> 로
        /// 비용을 먼저 처리하고, 성공했을 때만 이 메서드를 부른다.
        ///
        /// 능력치는 최초 3명과 같은 난수열을 이어 써서 "같은 시드 = 같은 결과"를 유지한다.
        /// </summary>
        /// <returns>생성된 캐릭터. 데이터/템플릿이 연결되지 않았으면 null.</returns>
        public CharacterUnit SpawnOneCharacter()
        {
            if (!Validate()) return null;

            // SpawnAll 없이 먼저 불릴 수도 있다(에디터에서 단독 테스트 등).
            if (_unitsRoot == null)
            {
                _unitsRoot = new GameObject("Units").transform;
                _unitsRoot.SetParent(transform, false);
            }
            if (_rng == null)
                _rng = new System.Random(
                    randomizeSeed ? Random.Range(int.MinValue, int.MaxValue) : seed);

            Vector3Int centerCell = mapGenerator != null ? mapGenerator.CenterCell : Vector3Int.zero;
            int index = SpawnedCharacters.Count;

            // 배치 각도를 계속 균등하게 나누려면 분모가 실제 인원 수와 함께 커져야 한다.
            return SpawnCharacterAt(centerCell, index, Mathf.Max(characterCount, index + 1));
        }

        /// <summary>캐릭터 한 명을 실제로 만든다. 최초 생성과 추가 생성이 이 경로를 공유한다.</summary>
        CharacterUnit SpawnCharacterAt(Vector3Int centerCell, int index, int ringDivisor)
        {
            Vector3Int cell = PickCharacterCell(centerCell, index, ringDivisor, _usedCells);
            _usedCells.Add(cell);

            StatBlock rolled = StatBlock.Roll(_rng, balance.initialStatMin, balance.initialStatMax);

            CharacterUnit unit = Instantiate(characterTemplate, CellCenter(cell),
                                             Quaternion.identity, _unitsRoot);
            unit.name = $"Character_{index + 1}";
            unit.gameObject.SetActive(true);
            unit.Initialize(rolled, balance);

            SpawnedCharacters.Add(unit);
            Debug.Log($"[UnitSpawner] {unit.name} @ cell{cell.x},{cell.y} · {unit.DebugSummary()}",
                      unit);

            OnCharacterSpawned?.Invoke(unit);
            return unit;
        }

        /// <summary>넥서스 주위를 균등하게 둘러싸는 위치를 고르고, 막혀 있으면 근처 빈 칸을 찾는다.</summary>
        Vector3Int PickCharacterCell(Vector3Int centerCell, int index, int ringDivisor,
                                     HashSet<Vector3Int> used)
        {
            float angle = (360f / Mathf.Max(1, ringDivisor)) * index * Mathf.Deg2Rad;
            var desired = new Vector3Int(
                centerCell.x + Mathf.RoundToInt(Mathf.Cos(angle) * spawnRingRadius),
                centerCell.y + Mathf.RoundToInt(Mathf.Sin(angle) * spawnRingRadius),
                0);

            if (mapGenerator == null) return desired;

            if (mapGenerator.TryFindPlaceableNear(desired, maxSearchRadius, used.Contains,
                                                  out Vector3Int found))
                return found;

            Debug.LogWarning($"[UnitSpawner] {index + 1}번째 캐릭터를 놓을 빈 칸을 " +
                             $"반경 {maxSearchRadius} 안에서 찾지 못했습니다.", this);
            return desired;
        }

        Vector3 CellCenter(Vector3Int cell) =>
            mapGenerator != null
                ? mapGenerator.CellCenterWorld(cell)
                : new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

        void OnDrawGizmosSelected()
        {
            Vector3Int center = mapGenerator != null ? mapGenerator.CenterCell : Vector3Int.zero;
            Vector3 c = CellCenter(center);

            Gizmos.color = Color.cyan;
            int size = nexusDefinition != null ? Mathf.Max(1, nexusDefinition.footprintTiles) : 3;
            Gizmos.DrawWireCube(c, new Vector3(size, size, 0f));

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(c, spawnRingRadius);
        }
    }
}
