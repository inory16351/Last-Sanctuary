using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 몬스터 한 마리. 캐릭터·넥서스와 같은 DamageableUnit 을 상속해서
    /// 피해 계산이 한 공식으로 통일된다.
    /// </summary>
    public class MonsterUnit : DamageableUnit
    {
        [Header("데이터")]
        [SerializeField] MonsterDefinitionSO definition;

        [Header("능력치 (웨이브 배율 반영 후)")]
        [SerializeField] StatBlock stats;

        [Tooltip("체력 보정(%). 100 이면 보정 없음")]
        [SerializeField] int hpPercent = 100;

        public MonsterDefinitionSO Definition => definition;
        public StatBlock Stats => stats;
        public MonsterTier Tier => definition != null ? definition.tier : MonsterTier.Normal;

        public override int MaxHp =>
            Balance != null
                ? BalanceConfigSO.ScaleByPercent(Balance.MaxHp(stats.hp), hpPercent)
                : 0;

        public override int DefenseStat => stats.defense;
        public override int AttackStat => stats.attack;
        protected override int RegenStat => stats.regen;

        public override Faction Faction => Faction.Cancer;
        public override UnitKind Kind => UnitKind.Monster;

        /// <summary>스포너가 복제 직후 호출한다.</summary>
        public void Initialize(MonsterDefinitionSO def, StatBlock scaledStats, BalanceConfigSO balance)
        {
            definition = def;
            stats = scaledStats;
            hpPercent = def != null ? def.hpPercent : 100;
            SetupHealth(balance);
        }

        protected override void OnDeath()
        {
            // 자원 획득은 다음 단계(자원 매니저)에서 이 시점에 연결한다.
            Destroy(gameObject);
        }

        public string DebugSummary()
        {
            if (Balance == null) return stats.ToString();
            return $"{(definition != null ? definition.displayName : name)} [{Tier}] " +
                   $"{stats} → HP {MaxHp} · 타격 {Balance.Attack(stats.attack)} · " +
                   $"피해감소 {Balance.DefenseReductionPercent(stats.defense)}%";
        }
    }
}
