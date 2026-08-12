using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 중립 몬스터 한 마리. 웨이브 진영(Cancer)과 무관하게 <see cref="Faction.Neutral"/> 로
    /// 등록되어, 몬스터들의 자동 전투 AI(Opposite 기준)에도 캐릭터의 평소 전투 AI에도
    /// 잡히지 않는다 — 캐릭터가 정찰 중 <see cref="CharacterBehavior"/> 가 별도로 찾아내
    /// <see cref="UnitCombat.SetHuntTarget"/> 으로 사냥을 걸어야만 교전한다.
    ///
    /// 선공형(<see cref="NeutralMonsterDefinitionSO.aggressive"/>)은 <see cref="UnitCombat"/> 도
    /// 붙어 있어 스스로 캐릭터를 발견하면 먼저 공격한다(Faction.Neutral.Opposite() == Angel).
    /// </summary>
    public class NeutralMonsterUnit : DamageableUnit
    {
        [Header("데이터")]
        [SerializeField] NeutralMonsterDefinitionSO definition;

        [Header("능력치 (웨이브 배율 없음)")]
        [SerializeField] StatBlock stats;

        public NeutralMonsterDefinitionSO Definition => definition;
        public StatBlock Stats => stats;

        public override int MaxHp => Balance != null ? Balance.MaxHp(stats.hp) : 0;
        public override int DefenseStat => stats.defense;
        public override int AttackStat => stats.attack;
        protected override int RegenStat => stats.regen;

        public override Faction Faction => Faction.Neutral;
        public override UnitKind Kind => UnitKind.Monster;

        /// <summary>처치 시 지급할 에너지를 이 범위에서 무작위로 뽑는다 (정의 테이블 min/max_energy).</summary>
        public int RollEnergyReward() =>
            definition != null ? Random.Range(definition.minEnergy, definition.maxEnergy + 1) : 0;

        /// <summary>스포너가 복제 직후 호출한다.</summary>
        public void Initialize(NeutralMonsterDefinitionSO def, BalanceConfigSO balance)
        {
            definition = def;
            stats = def != null ? def.BuildStats() : new StatBlock { hp = 1 };
            SetupHealth(balance);
        }

        protected override void OnDeath()
        {
            // 에너지 지급은 여기서 직접 하지 않는다 — ResourceManager 가
            // DamageableUnit.OnAnyDied 를 구독해 처리한다(웨이브 몬스터와 같은 패턴).
            Destroy(gameObject);
        }

        public string DebugSummary()
        {
            if (Balance == null || definition == null) return stats.ToString();
            return $"{definition.DisplayName} [중립{(definition.aggressive ? "·선공" : "")}] " +
                   $"{stats} → HP {MaxHp} · 타격 {Balance.Attack(stats.attack)} · " +
                   $"피해감소 {Balance.DefenseReductionPercent(stats.defense)}% · " +
                   $"에너지 {definition.minEnergy}~{definition.maxEnergy}";
        }
    }
}
