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
    /// ★ <b>중립 몬스터는 예외 없이 전부 비선공</b>이다 (유저 확정 2026-08-15) —
    /// 먼저 맞기 전에는 절대 공격하지 않는다. 맞으면 혼자 반격하고,
    /// <see cref="NeutralMonsterDefinitionSO.packRetaliate"/> 가 켜져 있으면
    /// <b>같은 무리 전체</b>가 그 공격자에게 덤빈다.
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
        protected override int RegenStat => stats.regen;

        /// <summary>
        /// 지금 쓰는 공격 능력치 종류 — 공격 유형에 따라 갈린다
        /// (<see cref="MonsterUnit.AttackStatType"/> · <see cref="CharacterUnit.AttackStatType"/> 와 같은 규칙).
        /// 표의 <c>atk_type</c> 이 <c>ranged</c> 인 종(1002)이 실제로 원거리 공격력을 쓴다.
        /// </summary>
        public StatType AttackStatType => AttackStatTypeOf(AttackTypeOf());

        public override int AttackStat => stats[AttackStatType];

        /// <summary>명중률 → 적중 확률(%). <b>원거리일 때만</b> 능력치를 본다(유저 확정 2026-08-15).</summary>
        public override float HitChancePercent =>
            Balance != null && RangedStatsApplyNow
                ? Balance.HitChancePercent(stats.accuracy)
                : 100f;

        /// <summary>크리티컬 → 치명타 확률(%). <b>원거리일 때만.</b></summary>
        public override float CriticalChancePercent =>
            Balance != null && RangedStatsApplyNow
                ? Balance.CriticalChancePercent(stats.critical)
                : 0f;

        public override Faction Faction => Faction.Neutral;
        public override UnitKind Kind => UnitKind.Monster;

        /// <summary>
        /// 몸집 반경(타일) — <b>근거리 유닛이 어디까지 다가가야 때릴 수 있는지</b>.
        /// <see cref="MonsterUnit.BodyRadiusTiles"/> 와 <b>같은 규칙</b>이다:
        /// 그림에 다시 맞춘 콜라이더의 가로·세로 중 <b>작은 쪽</b>의 절반.
        ///
        /// <b>왜 중립에도 필요해졌나</b> — 중립은 원래 전부 작은 정적 스프라이트라
        /// <see cref="UnitCombat"/> 의 기본값(0.4)으로 충분했다. 그런데 에픽
        /// (카르시노스 1004)이 <b>4.4 x 5.1 타일</b>짜리 몸집으로 들어오면서, 근접 캐릭터가
        /// 몸 한가운데까지 파고들려 하는 문제가 생겼다.
        ///
        /// 스킨(<see cref="CharacterAnimator"/>)이 없는 종은 0 을 돌려주고, 그러면
        /// <c>UnitCombat</c> 이 예전 기본값을 그대로 쓴다 — 1001~1003 은 동작이 안 바뀐다.
        /// </summary>
        public float BodyRadiusTiles
        {
            get
            {
                if (_animator == null) _animator = GetComponent<CharacterAnimator>();
                if (_animator == null) return 0f;

                Vector2 box = _animator.ColliderSizeTiles;
                return box.x > 0.01f && box.y > 0.01f
                    ? Mathf.Min(box.x, box.y) * 0.5f
                    : 0f;
            }
        }

        CharacterAnimator _animator;

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
            return $"{definition.DisplayName} [중립·비선공{(definition.packRetaliate ? "·무리반격" : "")}" +
                   $"{(definition.epic ? "·에픽" : "")}] " +
                   $"{stats} → HP {MaxHp} · 타격 {Balance.Attack(AttackStat)} · " +
                   $"피해감소 {Balance.DefenseReductionPercent(stats.defense)}% · " +
                   $"에너지 {definition.minEnergy}~{definition.maxEnergy}";
        }
    }
}
