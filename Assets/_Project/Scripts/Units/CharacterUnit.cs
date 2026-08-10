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

        /// <summary>
        /// 정신 이상 "각성" 처럼 <b>능력치 전체에 일시적으로 걸리는 보정</b>(%).
        /// 직렬화하지 않는다 — 임시 상태이고 정본은 <see cref="CharacterErosion"/> 이 들고 있다.
        /// 여러 효과가 겹칠 수 있으므로 값을 덮어쓰지 않고 더한다(<see cref="AddStatPercentBonus"/>).
        /// </summary>
        int _statPercentBonus;

        /// <summary>정신 이상 "이기심" — 외부 치유를 받지 못한다(자기 재생은 계속된다).</summary>
        bool _externalHealBlocked;

        public StatBlock Stats => stats;
        public int UpgradeCount => upgradeCount;

        /// <summary>지금 걸려 있는 능력치 보정(%). 0 이면 보정 없음.</summary>
        public int StatPercentBonus => _statPercentBonus;

        /// <summary>
        /// 보정이 반영된 실제 능력치. 몬스터의 <c>hpPercent</c> 와 같은 방식으로
        /// <b>치환 공식에 넣기 전 원시 능력치에 먼저 곱하고 반올림</b>한다(진행상황 4절) —
        /// 그래야 체력·타격 결과가 정수로 유지된다. 능력치 상한(<see cref="BalanceConfigSO.statMax"/>)도
        /// 그대로 적용한다.
        /// </summary>
        public int EffectiveStat(StatType type)
        {
            int raw = stats[type];
            if (_statPercentBonus == 0) return raw;

            int scaled = Mathf.RoundToInt(raw * (100 + _statPercentBonus) / 100f);
            int cap = Balance != null ? Balance.statMax : 100;
            return Mathf.Clamp(scaled, 1, cap);
        }

        public override int MaxHp => Balance != null ? Balance.MaxHp(EffectiveStat(StatType.Hp)) : 0;
        public override int DefenseStat => EffectiveStat(StatType.Defense);
        public override int AttackStat => EffectiveStat(StatType.Attack);
        protected override int RegenStat => EffectiveStat(StatType.Regen);

        /// <summary>이기심 상태에서는 외부 치유를 거부한다 — 자기 체력 재생은 이 경로를 거치지 않는다.</summary>
        public override bool AcceptsExternalHeal => !_externalHealBlocked;

        public override Faction Faction => Faction.Angel;
        public override UnitKind Kind => UnitKind.Character;

        /// <summary>
        /// 능력치 보정(%)을 더한다. 해제할 때는 같은 값을 음수로 넣는다 —
        /// 그래야 여러 효과가 겹쳐도 서로의 값을 지우지 않는다.
        ///
        /// 최대 체력이 즉시 바뀌므로 <b>현재 체력 비율을 유지</b>한다. 보정이 걸릴 때 공짜 회복이
        /// 되거나, 풀릴 때 최대 체력이 현재 체력보다 낮아져 값이 튀는 것을 막는다.
        /// </summary>
        public void AddStatPercentBonus(int deltaPercent)
        {
            if (deltaPercent == 0) return;

            float ratio = HpRatio;
            _statPercentBonus += deltaPercent;

            SetupHealth(Balance, fillHp: false);
            int target = Mathf.Clamp(Mathf.RoundToInt(MaxHp * ratio), 1, MaxHp);
            if (target > CurrentHp) Heal(target - CurrentHp);
            else if (target < CurrentHp) ApplyDamage(CurrentHp - target);
        }

        /// <summary>외부 치유 차단을 켜고 끈다 (정신 이상 "이기심").</summary>
        public void SetExternalHealBlocked(bool value) => _externalHealBlocked = value;

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

        /// <summary>
        /// 성장 1회를 적용한다. 능력치를 덮어쓰고 업그레이드 횟수를 1 올린다.
        ///
        /// 업그레이드 횟수가 곧 <b>그 캐릭터의 다음 강화 비용</b>을 결정하므로
        /// (<see cref="CharacterUpgradeService.CostFor"/>), 비용을 따로 저장하지 않고도
        /// 캐릭터마다 독립적으로 비용이 올라간다. 새로 만든 캐릭터는 횟수가 0이라
        /// 항상 기본 비용부터 시작한다.
        /// </summary>
        public void ApplyUpgrade(StatBlock newStats)
        {
            ApplyStats(newStats);
            upgradeCount++;
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
