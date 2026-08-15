using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 몬스터 한 마리. 캐릭터·넥서스와 같은 DamageableUnit 을 상속해서
    /// 피해 계산이 한 공식으로 통일된다.
    /// </summary>
    public class MonsterUnit : DamageableUnit, IBossSkillOwner
    {
        /// <summary>표의 <c>boss_skill_1~3</c>. 순서가 곧 스킬 슬롯 번호다.</summary>
        public int[] BossSkillIds => definition != null ? definition.bossSkillIds : null;

        /// <summary>정의가 들어왔는가 — 스포너가 <c>Initialize</c> 를 부른 뒤에 true.</summary>
        public bool SkillsReady => definition != null;

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

        /// <summary>
        /// 화면·로그에 쓸 이름. <see cref="CharacterUnit.DisplayName"/> 과 <b>같은 규칙</b>이다 —
        /// 표의 이름이 먼저고, 없을 때만 오브젝트 이름으로 떨어진다.
        ///
        /// <b>왜 오브젝트 이름을 직접 쓰지 않나</b>(유저 지시 2026-08-13: "로그에 템플릿 복제
        /// 될 때마다 몬스터 뒤에 번호 붙는 거 없애줘 캐릭터랑 동일하게 그냥 이름으로 처리") —
        /// 스포너가 복제본을 구별하려고 이름 뒤에 일련번호를 붙이던 시절이 있었고, 로그가
        /// 그 이름을 그대로 찍어 "지옥 송곳니_7 처치"처럼 나왔다. 표시 이름을 여기 하나로
        /// 모아두면 하이라키 이름을 어떻게 짓든 로그가 흔들리지 않는다.
        /// </summary>
        public override string DisplayName =>
            definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.DisplayName
                : name;

        /// <summary>
        /// 보스 <b>칭호</b>(예: 단탈리온의 "끝없는 형상의 군주"). 없으면 빈 문자열.
        /// 보스 체력바가 이름 위에 띄운다(유저 지시 2026-08-13).
        /// </summary>
        public override string Title => definition != null ? definition.Title : string.Empty;

        /// <summary>
        /// 보스급(중간보스·최종보스)인지 — <see cref="MonsterTier.Normal"/> 이 아니면 보스다.
        /// 체력바·BGM 이 이미 같은 판정을 각자 하고 있어서 한 곳으로 모아둔다.
        /// </summary>
        public bool IsBoss => Tier != MonsterTier.Normal;

        /// <summary>
        /// 몸집 반경(타일) — <b>근거리 유닛이 어디까지 다가가야 때릴 수 있는지</b>.
        /// 이 프로젝트의 유닛에는 <c>Collider2D</c> 가 없으므로(준수사항 U-D9) <b>이 값이 곧
        /// 콜라이더</b>다. <c>UnitCombat.TargetRadius</c> 가 이걸 읽는다.
        ///
        /// <b>그림에 다시 맞춘 콜라이더를 쓴다</b>(유저 확정 2026-08-13) — 표에 적은 희망
        /// 크기가 아니라 <b>실제로 그려진 크기</b>(<c>CharacterAnimator.ColliderSizeTiles</c>)다.
        /// 표 값을 그대로 쓰면 비율 때문에 그림이 상자보다 작을 때 근접 유닛이 허공을 때린다.
        ///
        /// 가로·세로 중 <b>작은 쪽</b>의 절반을 쓴다: 큰 쪽을 쓰면 몸 옆구리에서 사거리 안으로
        /// 판정돼 공중에서 때리는 것처럼 보인다.
        /// 스킨이 없어 그림 크기를 알 수 없을 때만 정의의 발판 칸 수로 떨어지고,
        /// 둘 다 없으면 0 — <c>UnitCombat</c> 의 기존 기본값(0.4)이 그대로 쓰인다.
        /// </summary>
        public float BodyRadiusTiles
        {
            get
            {
                if (_animator == null) _animator = GetComponent<Combat.CharacterAnimator>();
                if (_animator != null)
                {
                    Vector2 box = _animator.ColliderSizeTiles;
                    if (box.x > 0.01f && box.y > 0.01f)
                        return Mathf.Min(box.x, box.y) * 0.5f;
                }

                return definition == null
                    ? 0f
                    : Mathf.Max(0f, Mathf.Min(definition.BodyWidth, definition.BodyHeight) * 0.5f);
            }
        }

        /// <summary>
        /// 그림에 다시 맞춘 콜라이더 크기(타일, 가로·세로). 디버그·UI 표시용 —
        /// 전투 판정은 위 <see cref="BodyRadiusTiles"/> 하나만 쓴다(반경 기준이라 한 값이면 된다).
        /// </summary>
        public Vector2 ColliderSizeTiles
        {
            get
            {
                if (_animator == null) _animator = GetComponent<Combat.CharacterAnimator>();
                return _animator != null ? _animator.ColliderSizeTiles : Vector2.zero;
            }
        }

        Combat.CharacterAnimator _animator;

        public override int MaxHp =>
            Balance != null
                ? BalanceConfigSO.ScaleByPercent(
                      BalanceConfigSO.ScaleByPercent(Balance.MaxHp(stats.hp), hpPercent), enragePercent)
                : 0;

        public override int DefenseStat => stats.defense;
        protected override int RegenStat => stats.regen;

        /// <summary>
        /// 지금 쓰는 공격 능력치 종류. <b>공격 유형에 따라 달라진다</b> —
        /// 캐릭터(<see cref="CharacterUnit.AttackStatType"/>)와 <b>같은 규칙</b>이다.
        ///
        /// ⚠ 예전에는 유형과 무관하게 <c>stats.attack</c>(근거리) 한 칸만 읽었고,
        /// 파싱 스크립트가 <c>max(melee_atk, ranged_atk)</c> 로 <b>표의 두 칸을 하나로 접어</b>
        /// 겨우 맞춰 두고 있었다. 이제 표의 네 공격 계열이 그대로 의미를 갖는다.
        /// </summary>
        public StatType AttackStatType => AttackStatTypeOf(AttackTypeOf());

        public override int AttackStat =>
            BalanceConfigSO.ScaleByPercent(stats[AttackStatType], enragePercent);

        /// <summary>
        /// 명중률 능력치 → 적중 확률(%). <b>원거리일 때만</b> 능력치를 본다
        /// (유저 확정 2026-08-15 — 캐릭터와 같은 규칙).
        /// 근거리·마법은 100% 라 명중 판정이 통째로 생략된다.
        /// </summary>
        public override float HitChancePercent =>
            Balance != null && RangedStatsApplyNow
                ? Balance.HitChancePercent(stats.accuracy)
                : 100f;

        /// <summary>크리티컬 확률 능력치 → 치명타 확률(%). <b>원거리일 때만.</b></summary>
        public override float CriticalChancePercent =>
            Balance != null && RangedStatsApplyNow
                ? Balance.CriticalChancePercent(stats.critical)
                : 0f;

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
