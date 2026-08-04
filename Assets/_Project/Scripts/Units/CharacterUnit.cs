using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 캐릭터(백혈구) 한 명. 이번 단계는 생성과 능력치 보관까지만 담당한다.
    /// 이동 / 타겟팅 / 자동전투 FSM 은 다음 단계에서 붙는다.
    ///
    /// 템플릿(하이라키의 Character_Template)을 복제해 생성하므로,
    /// 애니메이터·콜라이더·이펙트를 템플릿에 붙이면 생성되는 모든 캐릭터가 물려받는다.
    /// </summary>
    public class CharacterUnit : DamageableUnit
    {
        [Header("능력치 (생성 시 1~10 랜덤)")]
        [SerializeField] StatBlock stats;

        [Header("성장 기록")]
        [Tooltip("업그레이드 횟수. 새 캐릭터 생성 시 이 범위를 참조한다(기획서 p9)")]
        [SerializeField] int upgradeCount;

        public StatBlock Stats => stats;
        public int UpgradeCount => upgradeCount;

        public override int MaxHp => Balance != null ? Balance.MaxHp(stats.hp) : 0;
        public override int DefenseStat => stats.defense;
        public override int AttackStat => stats.attack;
        protected override int RegenStat => stats.regen;

        public override Faction Faction => Faction.Angel;
        public override UnitKind Kind => UnitKind.Character;

        /// <summary>스포너가 복제 직후 호출해 능력치를 주입한다.</summary>
        public void Initialize(StatBlock rolled, BalanceConfigSO balance, int upgrades = 0)
        {
            stats = rolled;
            upgradeCount = upgrades;
            SetupHealth(balance);   // 최대 체력이 stats 에 의존하므로 stats 대입 후에 호출
        }

        /// <summary>능력치를 직접 덮어쓴다 (성장 시스템에서 사용).</summary>
        public void ApplyStats(StatBlock newStats, bool keepCurrentHpRatio = true)
        {
            float ratio = keepCurrentHpRatio ? HpRatio : 1f;
            stats = newStats;
            SetupHealth(Balance, fillHp: false);

            // 비율 유지 — 반올림해서 체력이 정수로 유지되게 한다
            int target = Mathf.Clamp(Mathf.RoundToInt(MaxHp * ratio), 1, MaxHp);
            Heal(target - CurrentHp);
        }

        protected override void OnDeath()
        {
            Debug.Log($"[Character] {name} 사망", this);

            // 남겨두면 시체가 넥서스 주변에 쌓여 "전투가 멈춘 것처럼" 보인다.
            // 사망 연출이 필요해지면 여기서 애니메이션 후 파괴로 바꾼다.
            Destroy(gameObject);
        }

        /// <summary>디버깅용 요약.</summary>
        public string DebugSummary()
        {
            if (Balance == null) return stats.ToString();
            return $"{stats}  →  HP {MaxHp} · 타격 {Balance.Attack(stats.attack)} · " +
                   $"피해감소 {Balance.DefenseReductionPercent(stats.defense)}% · " +
                   $"재생 {Balance.RegenPerTick(stats.regen)}/{Balance.regenTickSeconds:0.#}초 " +
                   $"(전투 후 {Balance.outOfCombatRegenDelay:0.#}초 대기)";
        }
    }
}
