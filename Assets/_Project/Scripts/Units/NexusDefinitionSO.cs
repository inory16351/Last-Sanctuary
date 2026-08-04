using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 중앙 건물(넥서스) 능력치. 에디터에서 직접 편집한다.
    ///
    /// 캐릭터와 같은 1~100 척도와 같은 치환 공식을 쓰되, 건물이라 체력 규모가
    /// 훨씬 커야 하므로 체력에만 별도 배율을 둔다. 배율도 퍼센트(정수)라 결과는 정수다.
    /// (능력치 100 → 1040, × 250% → 2600)
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/Units/Nexus Definition", fileName = "NexusDefinition")]
    public class NexusDefinitionSO : ScriptableObject
    {
        [Header("능력치 (1 ~ 100)")]
        [Range(1, 100)] public int hpStat = 100;
        [Range(1, 100)] public int defenseStat = 10;
        [Range(1, 100)] public int regenStat = 5;

        [Header("건물 보정")]
        [Tooltip("건물은 캐릭터보다 체력이 커야 하므로 최대 체력에만 곱하는 배율(%). " +
                 "250 이면 2.5배")]
        [Min(1)] public int hpPercent = 250;

        [Header("외형")]
        [Tooltip("한 변이 몇 타일인지. 3 이면 3x3 타일을 차지한다")]
        [Min(1)] public int footprintTiles = 3;

        /// <summary>치환된 최대 체력(정수).</summary>
        public int MaxHp(BalanceConfigSO balance) =>
            balance == null ? 0 : BalanceConfigSO.ScaleByPercent(balance.MaxHp(hpStat), hpPercent);

        /// <summary>치환된 회복 틱당 회복량(정수).</summary>
        public int RegenPerTick(BalanceConfigSO balance) =>
            balance == null ? 0 : balance.RegenPerTick(regenStat);

        /// <summary>표시용 피해 감소율(%). 정수.</summary>
        public int DefenseReductionPercent(BalanceConfigSO balance) =>
            balance == null ? 0 : balance.DefenseReductionPercent(defenseStat);
    }
}
