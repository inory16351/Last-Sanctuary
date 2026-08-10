using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Resource;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 캐릭터 강화 규칙. 비용 계산과 성장 적용만 담당하고, 클릭/버튼 같은 입력은
    /// UI 쪽(<c>UpgradeButtonUI</c>)이 맡는다.
    ///
    /// <b>비용은 캐릭터마다 따로 올라간다</b> — 별도 저장소를 두지 않고
    /// <see cref="CharacterUnit.UpgradeCount"/>(이미 캐릭터마다 직렬화되는 값)에서
    /// 계산한다. 그래서 캐릭터가 늘어나든 죽든 비용 장부를 따로 관리할 필요가 없고,
    /// 캐릭터를 새로 만들면 횟수 0 이라 자동으로 기본 비용부터 시작한다.
    ///
    ///   비용 = baseCost + costIncreasePerUpgrade × (그 캐릭터의 강화 횟수)
    /// </summary>
    public class CharacterUpgradeService : MonoBehaviour
    {
        [Header("비용 (캐릭터별로 따로 올라감)")]
        [Tooltip("한 번도 강화하지 않은 캐릭터의 강화 비용")]
        [Min(0)] [SerializeField] int baseCost = 20;

        [Tooltip("그 캐릭터를 한 번 강화할 때마다 다음 비용에 더해지는 양")]
        [Min(0)] [SerializeField] int costIncreasePerUpgrade = 10;

        [Header("성장량 (강화 1회당 각 능력치에 더해지는 랜덤 값)")]
        [Min(0)] [SerializeField] int growthMin = 1;
        [Min(0)] [SerializeField] int growthMax = 10;

        [Header("랜덤")]
        [Tooltip("같은 시드 = 항상 같은 성장 결과. 밸런싱 테스트에 필요")]
        [SerializeField] int seed = 20260804;

        [Tooltip("켜면 실행할 때마다 다른 성장값이 나온다")]
        [SerializeField] bool randomizeSeed = true;

        [Header("디버그")]
        [SerializeField] bool logUpgrades = true;

        System.Random _rng;

        public static CharacterUpgradeService Instance { get; private set; }

        /// <summary>강화가 성사될 때마다 발생 (대상, 소비한 비용).</summary>
        public event System.Action<CharacterUnit, int> OnUpgraded;

        void Awake()
        {
            Instance = this;
            _rng = new System.Random(
                randomizeSeed ? Random.Range(int.MinValue, int.MaxValue) : seed);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------

        /// <summary>이 캐릭터를 지금 강화하는 데 드는 비용.</summary>
        public int CostFor(CharacterUnit unit) =>
            unit == null ? 0 : baseCost + costIncreasePerUpgrade * unit.UpgradeCount;

        /// <summary>지금 강화할 수 있는지 (대상이 살아있고 에너지가 충분한지).</summary>
        public bool CanUpgrade(CharacterUnit unit)
        {
            if (unit == null || !unit.IsAlive) return false;
            ResourceManager resources = ResourceManager.Instance;
            return resources != null && resources.CanAfford(CostFor(unit));
        }

        /// <summary>
        /// 에너지를 소비하고 능력치를 성장시킨다. 에너지가 부족하면 아무것도 하지 않는다.
        /// </summary>
        public bool TryUpgrade(CharacterUnit unit)
        {
            if (unit == null || !unit.IsAlive) return false;

            ResourceManager resources = ResourceManager.Instance;
            if (resources == null)
            {
                Debug.LogWarning("[Upgrade] ResourceManager 를 찾을 수 없어 강화할 수 없습니다.", this);
                return false;
            }

            int cost = CostFor(unit);
            if (!resources.TrySpend(cost))
            {
                if (logUpgrades)
                    Debug.Log($"[Upgrade] 에너지 부족 — {unit.name} 강화에 {cost} 필요, " +
                              $"보유 {resources.Energy}");
                return false;
            }

            StatBlock before = unit.Stats;
            StatBlock grown = Grow(before, unit.Balance);
            unit.ApplyUpgrade(grown);

            if (logUpgrades)
                Debug.Log($"[Upgrade] {unit.name} 강화 {unit.UpgradeCount}회 · 비용 {cost} · " +
                          $"{before} → {grown} · 다음 비용 {CostFor(unit)}", unit);

            OnUpgraded?.Invoke(unit, cost);
            return true;
        }

        /// <summary>
        /// <b>자원을 쓰지 않고</b> 강화한다 — 정신 이상 "고조"(자원 소모 없이 강화됨)가 쓴다.
        ///
        /// 강화 횟수(<see cref="CharacterUnit.UpgradeCount"/>)는 정상적으로 오른다. 테이블의
        /// "자원 소모는 없으니 자원 소모 값은 동일하게 상승" 이 정확히 이 동작이다 — 이 프로젝트는
        /// 강화 비용을 별도 장부가 아니라 그 캐릭터의 강화 횟수에서 계산하므로(<see cref="CostFor"/>),
        /// <see cref="CharacterUnit.ApplyUpgrade"/> 를 부르는 것만으로 "공짜로 강화됐지만 다음
        /// 강화 비용은 그만큼 올라간다"가 그대로 성립한다.
        /// </summary>
        /// <returns>실제로 적용된 강화 횟수.</returns>
        public int GrowFree(CharacterUnit unit, int times)
        {
            if (unit == null || !unit.IsAlive || times <= 0) return 0;

            int applied = 0;
            for (int i = 0; i < times; i++)
            {
                StatBlock before = unit.Stats;
                StatBlock grown = Grow(before, unit.Balance);
                unit.ApplyUpgrade(grown);
                applied++;
            }

            if (logUpgrades)
                Debug.Log($"[Upgrade] {unit.name} 무료 강화 {applied}회(정신 이상 고조) · " +
                          $"누적 {unit.UpgradeCount}회 · 다음 비용 {CostFor(unit)}", unit);

            OnUpgraded?.Invoke(unit, 0);
            return applied;
        }

        /// <summary>
        /// 능력치 4종에 각각 독립적인 랜덤값을 더한다. 능력치 상한(기본 100)에서 잘린다.
        /// <see cref="StatBlock.Roll"/> 를 재사용해 "각 능력치에 균등 랜덤" 규칙이
        /// 캐릭터 생성과 성장에서 같은 코드로 유지되게 했다.
        /// </summary>
        StatBlock Grow(StatBlock current, BalanceConfigSO balance)
        {
            int min = Mathf.Min(growthMin, growthMax);
            int max = Mathf.Max(growthMin, growthMax);
            StatBlock growth = StatBlock.Roll(_rng, min, max);

            int statMax = balance != null ? balance.statMax : 100;

            StatBlock result = current;
            for (int i = 0; i < (int)StatType.COUNT; i++)
            {
                var t = (StatType)i;
                result[t] = Mathf.Min(statMax, current[t] + growth[t]);
            }
            return result;
        }

        void OnValidate()
        {
            growthMax = Mathf.Max(growthMin, growthMax);
        }
    }
}
