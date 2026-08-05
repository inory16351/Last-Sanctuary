using UnityEngine;
using LastSanctuary.Resource;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 캐릭터 생성 규칙 — 비용 계산과 소비만 담당하고, 실제 생성은
    /// <see cref="UnitSpawner.SpawnOneCharacter"/> 에 맡긴다.
    ///
    /// 규칙(비용)과 입력(버튼)을 갈라두는 건 이 프로젝트가 이미 쓰는 구조다 —
    /// <c>CharacterUpgradeService</c> ↔ <c>UpgradeButtonUI</c> 와 같은 모양으로 맞췄다.
    /// 덕분에 버튼을 다시 만들어도 비용 규칙은 그대로 남는다.
    ///
    ///   비용 = baseCost + costIncreasePerCharacter × (지금까지 생성된 캐릭터 수 − 시작 인원)
    ///
    /// 시작 인원(UnitSpawner 가 게임 시작에 만드는 3명)은 비용 계산에서 빼서,
    /// 첫 추가 생성이 항상 baseCost 부터 시작하게 했다.
    /// </summary>
    public class CharacterCreationService : MonoBehaviour
    {
        [Header("비용")]
        [Tooltip("첫 번째 추가 캐릭터를 만드는 데 드는 에너지")]
        [Min(0)] [SerializeField] int baseCost = 30;

        [Tooltip("한 명 늘어날 때마다 다음 생성 비용에 더해지는 양")]
        [Min(0)] [SerializeField] int costIncreasePerCharacter = 15;

        [Header("제한")]
        [Tooltip("이 인원을 넘겨서는 만들 수 없다. 0 이면 제한 없음")]
        [Min(0)] [SerializeField] int maxCharacters = 12;

        [Header("디버그")]
        [SerializeField] bool logCreation = true;

        UnitSpawner _spawner;
        int _startingCount = -1;   // 시작 인원. 첫 조회 때 확정한다

        public static CharacterCreationService Instance { get; private set; }

        /// <summary>생성이 성사될 때마다 (생성된 캐릭터, 소비한 비용).</summary>
        public event System.Action<CharacterUnit, int> OnCreated;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            _spawner = FindAnyObjectByType<UnitSpawner>();
            if (_spawner == null)
                Debug.LogWarning("[Create] UnitSpawner 를 찾지 못했습니다. 캐릭터 생성 버튼이 동작하지 않습니다.", this);
        }

        // ------------------------------------------------------------------

        /// <summary>지금 살아있는 캐릭터 수. 죽으면 오브젝트가 파괴되므로 레지스트리를 세는 게 정확하다.</summary>
        public int AliveCount
        {
            get
            {
                var all = LastSanctuary.Combat.UnitRegistry.All;
                int n = 0;
                for (int i = 0; i < all.Count; i++)
                    if (all[i] is CharacterUnit c && c.IsAlive) n++;
                return n;
            }
        }

        /// <summary>지금까지 만들어진 캐릭터 수(죽은 것 포함). 비용은 이 값 기준으로 오른다.</summary>
        public int CreatedCount => _spawner != null ? _spawner.SpawnedCharacters.Count : 0;

        /// <summary>다음 캐릭터를 만드는 데 드는 에너지.</summary>
        public int CurrentCost
        {
            get
            {
                if (_spawner == null) return baseCost;

                // 시작 인원은 게임이 캐릭터를 다 만든 뒤(첫 프레임 이후)에야 확정된다.
                if (_startingCount < 0 && _spawner.SpawnedCharacters.Count > 0)
                    _startingCount = _spawner.SpawnedCharacters.Count;

                int start = Mathf.Max(0, _startingCount);
                int extra = Mathf.Max(0, _spawner.SpawnedCharacters.Count - start);
                return baseCost + costIncreasePerCharacter * extra;
            }
        }

        /// <summary>인원 상한에 걸렸는지.</summary>
        public bool AtLimit => maxCharacters > 0 && AliveCount >= maxCharacters;

        /// <summary>지금 만들 수 있는지 (스포너가 있고, 상한 미만이고, 에너지가 충분한지).</summary>
        public bool CanCreate
        {
            get
            {
                if (_spawner == null || AtLimit) return false;
                ResourceManager resources = ResourceManager.Instance;
                return resources != null && resources.CanAfford(CurrentCost);
            }
        }

        /// <summary>
        /// 에너지를 소비하고 캐릭터를 한 명 만든다. 부족하거나 상한이면 아무것도 하지 않는다.
        /// <b>비용을 먼저 차감하고 성공했을 때만 생성한다</b> — 순서를 반대로 하면
        /// 에너지가 모자랄 때 캐릭터만 생기는 구멍이 난다.
        /// </summary>
        public CharacterUnit TryCreate()
        {
            if (_spawner == null)
            {
                Debug.LogWarning("[Create] UnitSpawner 가 없어 생성할 수 없습니다.", this);
                return null;
            }

            if (AtLimit)
            {
                HudLog.Add($"인원 상한 {maxCharacters} 명에 도달했습니다", HudLogKind.Warn);
                return null;
            }

            ResourceManager resources = ResourceManager.Instance;
            if (resources == null)
            {
                Debug.LogWarning("[Create] ResourceManager 를 찾을 수 없어 생성할 수 없습니다.", this);
                return null;
            }

            int cost = CurrentCost;
            if (!resources.TrySpend(cost))
            {
                if (logCreation)
                    Debug.Log($"[Create] 에너지 부족 — 생성에 {cost} 필요, 보유 {resources.Energy}");
                HudLog.Add($"에너지 부족 — 생성에 {cost} 필요", HudLogKind.Warn);
                return null;
            }

            CharacterUnit unit = _spawner.SpawnOneCharacter();
            if (unit == null)
            {
                // 생성이 실패했으면 깎은 에너지를 돌려준다.
                resources.AddEnergy(cost);
                Debug.LogWarning("[Create] 생성에 실패해 에너지를 돌려주었습니다.", this);
                return null;
            }

            if (logCreation) Debug.Log($"[Create] {unit.name} 생성 · 비용 {cost} · 다음 비용 {CurrentCost}", unit);
            HudLog.Add($"{unit.name} 생성 (−{cost})", HudLogKind.Good);

            OnCreated?.Invoke(unit, cost);
            return unit;
        }

        void OnValidate()
        {
            if (maxCharacters > 0 && maxCharacters < 1) maxCharacters = 1;
        }
    }
}
