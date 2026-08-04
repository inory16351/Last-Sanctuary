using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 중앙 건물. 파괴되면 즉시 패배(핵심시스템 기획서 p9).
    /// 공격은 하지 않는다 — 공격하는 건물은 포탑이 따로 담당한다.
    /// </summary>
    public class Nexus : DamageableUnit
    {
        [Header("데이터")]
        [SerializeField] NexusDefinitionSO definition;

        /// <summary>넥서스가 파괴되었을 때. 패배 처리가 여기에 붙는다.</summary>
        public event System.Action<Nexus> OnNexusDestroyed;

        public NexusDefinitionSO Definition => definition;

        public override int MaxHp =>
            definition != null && Balance != null ? definition.MaxHp(Balance) : 0;

        public override int DefenseStat => definition != null ? definition.defenseStat : 0;

        /// <summary>넥서스는 공격하지 않는다.</summary>
        public override int AttackStat => 0;

        protected override int RegenStat => definition != null ? definition.regenStat : 0;

        public override Faction Faction => Faction.Angel;
        public override UnitKind Kind => UnitKind.Nexus;

        /// <summary>스포너가 복제 직후 호출한다.</summary>
        public void Initialize(NexusDefinitionSO def, BalanceConfigSO balance)
        {
            definition = def;
            SetupHealth(balance);
        }

        protected override void OnDeath()
        {
            Debug.Log("[Nexus] 중앙 건물이 파괴되었습니다 → 패배", this);
            OnNexusDestroyed?.Invoke(this);
        }

        public string DebugSummary()
        {
            if (definition == null || Balance == null) return "(데이터 없음)";
            return $"{definition.footprintTiles}x{definition.footprintTiles}타일 · " +
                   $"체력 {MaxHp} (능력치 {definition.hpStat}) · " +
                   $"피해감소 {definition.DefenseReductionPercent(Balance)}% " +
                   $"(능력치 {definition.defenseStat}) · " +
                   $"재생 {definition.RegenPerTick(Balance)}/{Balance.regenTickSeconds:0.#}초 " +
                   $"(능력치 {definition.regenStat}, 전투 후 {Balance.outOfCombatRegenDelay:0.#}초 대기)";
        }
    }
}
