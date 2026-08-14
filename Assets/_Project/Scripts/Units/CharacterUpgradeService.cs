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

        // ──────────────────────────────────────────────────────────────────
        // 성장량 — 유저 지시 2026-08-14로 전면 교체됐다.
        //
        //   이전: growthMin(1) ~ growthMax(5) 균등 랜덤. 1·2·3·4·5 가 전부 20%.
        //   지금: 아래 growthWeights 6칸의 <b>가중 추첨</b>.
        //         · 일반 능력치       → growthBaseMin + 굴림(0~5)      = <b>0~5</b>
        //         · 성장 유형에 묶인  → 거기에 growthFocusBonus(+1)    = <b>1~6</b>
        //         양 끝(0·5 / 1·6)은 확률이 낮고 가운데(2·3 / 3·4)가 높다.
        //
        // ⚠ growthMin/growthMax 필드를 <b>이름째로 없앴다</b> — 그래야 씬에 직렬화돼 있던
        //   옛 값(1/5, 진행상황 29-5절)이 버려지고 새 구조가 들어온다(49-1절의 그 원리).
        // ──────────────────────────────────────────────────────────────────

        [Header("성장량 — 강화 1회에 각 능력치가 오르는 폭의 확률 분포")]
        [Tooltip("굴림 결과 0·1·2·3·4·5 각각의 가중치. 합이 100 일 필요는 없다(비율로만 쓴다).\n" +
                 "★ 여기가 '각 수치마다 상승 확률'을 조절하는 자리다 — 양 끝을 낮추고 " +
                 "가운데를 높이면 평균 근처가 자주 나온다.\n" +
                 "일반 능력치는 이 굴림이 그대로 0~5, 성장 유형에 묶인 능력치는 +1 되어 1~6 이 된다")]
        [SerializeField] int[] growthWeights = { 8, 17, 25, 25, 17, 8 };

        [Tooltip("일반(성장 유형에 안 묶인) 능력치의 최솟값. 0 이면 '안 오를 수도 있다'")]
        [Min(0)] [SerializeField] int growthBaseMin = 0;

        [Tooltip("성장 유형에 묶인 능력치가 추가로 받는 값. 1 이면 0~5 가 1~6 이 된다")]
        [Min(0)] [SerializeField] int growthFocusBonus = 1;

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
            StatBlock grown = Grow(before, unit.Balance, unit.GrowthFocus);
            unit.ApplyUpgrade(grown);

            if (logUpgrades)
                Debug.Log($"[Upgrade] {unit.name} 강화 {unit.UpgradeCount}회 · 비용 {cost} · " +
                          $"성장 유형 {StatGrowthFocusTable.Label(unit.GrowthFocus)} · " +
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
                StatBlock grown = Grow(before, unit.Balance, unit.GrowthFocus);
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
        /// 성장 가능한 능력치마다 각각 독립적인 랜덤값을 더한다. 능력치 상한(기본 100)에서 잘린다.
        ///
        /// <b>저항력은 오르지 않는다</b>(<see cref="StatBlock.IsGrowable"/>) —
        /// 캐릭터 고유의 고정 능력치이기 때문이다(캐릭터 가이드 p5).
        /// 그래서 <see cref="StatBlock.Roll"/> 을 그대로 쓰지 않고 능력치마다 따로 굴린다:
        /// Roll 은 저항력을 50 으로 고정해 넣기 때문에 여기서 쓰면 저항력이 매번 50 이 더해진다.
        ///
        /// <b>성장 유형</b>(<paramref name="focus"/>)에 묶인 능력치는
        /// <see cref="growthFocusBonus"/> 만큼 더 오른다 — 같은 확률 분포를 한 칸 밀어 쓰는 것이라
        /// "잘 오른다"가 확실하면서도 최댓값만 나오는 식으로 망가지지 않는다.
        /// </summary>
        StatBlock Grow(StatBlock current, BalanceConfigSO balance, StatGrowthFocus focus)
        {
            int statMax = balance != null ? balance.statMax : 100;

            StatBlock result = current;
            for (int i = 0; i < (int)StatType.COUNT; i++)
            {
                var t = (StatType)i;
                if (!StatBlock.IsGrowable(t)) continue;   // 저항력은 고정

                int growth = growthBaseMin + RollGrowthStep();
                if (StatGrowthFocusTable.IsFavored(focus, t)) growth += growthFocusBonus;

                result[t] = Mathf.Min(statMax, current[t] + growth);
            }
            return result;
        }

        /// <summary>
        /// <see cref="growthWeights"/> 를 가중치로 삼아 0 ~ (칸 수 −1) 중 하나를 뽑는다.
        ///
        /// 표가 비었거나 합이 0 이면 <b>균등 추첨</b>으로 떨어진다 — 인스펙터에서 값을 다 지워도
        /// 강화가 멈추지 않게 하려는 안전장치다(이 프로젝트가 여러 번 겪은 "설정이 비면 기능이
        /// 조용히 죽는다" 를 막는다).
        /// </summary>
        int RollGrowthStep()
        {
            int count = growthWeights != null ? growthWeights.Length : 0;
            if (count == 0) return 0;

            int total = 0;
            for (int i = 0; i < count; i++) total += Mathf.Max(0, growthWeights[i]);
            if (total <= 0) return _rng.Next(count);

            int pick = _rng.Next(total);
            for (int i = 0; i < count; i++)
            {
                pick -= Mathf.Max(0, growthWeights[i]);
                if (pick < 0) return i;
            }
            return count - 1;
        }
    }
}
