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

        /// <summary>광폭화 배율(%). 100 이면 보정 없음. 웨이브 타이머가 끝나도 처치가 안 되면 오른다.</summary>
        int enragePercent = 100;

        public MonsterDefinitionSO Definition => definition;
        public StatBlock Stats => stats;
        public MonsterTier Tier => definition != null ? definition.tier : MonsterTier.Normal;

        public override int MaxHp =>
            Balance != null
                ? BalanceConfigSO.ScaleByPercent(
                      BalanceConfigSO.ScaleByPercent(Balance.MaxHp(stats.hp), hpPercent), enragePercent)
                : 0;

        public override int DefenseStat => stats.defense;
        public override int AttackStat => BalanceConfigSO.ScaleByPercent(stats.attack, enragePercent);
        protected override int RegenStat => stats.regen;

        public override Faction Faction => Faction.Cancer;
        public override UnitKind Kind => UnitKind.Monster;

        /// <summary>스포너가 복제 직후 호출한다.</summary>
        public void Initialize(MonsterDefinitionSO def, StatBlock scaledStats, BalanceConfigSO balance)
        {
            definition = def;
            stats = scaledStats;
            hpPercent = def != null ? def.hpPercent : 100;
            enragePercent = 100;
            SetupHealth(balance);
        }

        /// <summary>WaveManager 가 광폭화 진행 중 매초 호출해 능력치 배율을 올린다.</summary>
        public void SetEnragePercent(int percent) => enragePercent = Mathf.Max(100, percent);

        protected override void OnDeath()
        {
            // 자원 획득은 여기서 직접 하지 않는다 — ResourceManager 가
            // DamageableUnit.OnAnyDied 를 구독해 처리한다.
            Destroy(gameObject);
        }

        public string DebugSummary()
        {
            if (Balance == null) return stats.ToString();
            return $"{(definition != null ? definition.DisplayName : name)} [{Tier}] " +
                   $"{stats} → HP {MaxHp} · 타격 {Balance.Attack(AttackStat)} · " +
                   $"피해감소 {Balance.DefenseReductionPercent(stats.defense)}%";
        }
    }
}
