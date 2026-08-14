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

        [Tooltip("성장 유형 — 강화 시 어느 능력치 묶음이 더 잘 오를지. " +
                 "캐릭터 성장 창의 성장 유형 버튼이 정한다(유저 확정 2026-08-14).\n" +
                 "캐릭터마다 따로 기억되므로 다른 캐릭터를 봤다가 돌아와도 유지된다")]
        [SerializeField] StatGrowthFocus growthFocus = StatGrowthFocus.None;

        [Header("정체 (캐릭터 테이블)")]
        [Tooltip("이 캐릭터가 누구인지. 이름 · 일러스트 · 패시브 3종이 여기서 온다. " +
                 "비어 있으면 이름 없는 무작위 능력치 캐릭터로 취급한다(확장 전 동작)")]
        [SerializeField] CharacterDefinitionSO definition;

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

        /// <summary>
        /// 지금 정해진 성장 유형. <see cref="CharacterUpgradeService"/> 가 강화할 때 읽는다.
        /// 미선택(<see cref="StatGrowthFocus.None"/>)이면 모든 능력치가 같은 확률로 오른다.
        /// </summary>
        public StatGrowthFocus GrowthFocus => growthFocus;

        /// <summary>성장 유형을 정한다. 캐릭터 성장 창의 유형 버튼이 부르는 유일한 경로다.</summary>
        public void SetGrowthFocus(StatGrowthFocus focus) => growthFocus = focus;

        /// <summary>이 캐릭터가 누구인지. 정의 없이 생성된 캐릭터는 null.</summary>
        public CharacterDefinitionSO Definition => definition;

        /// <summary>표시 이름. 정의가 있으면 테이블의 한글 이름, 없으면 오브젝트 이름.</summary>
        public string DisplayName =>
            definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.DisplayName
                : name;

        /// <summary>
        /// 슬롯의 패시브가 지금 해금돼 있는가. 정의가 없으면 항상 false.
        /// 해금 기준은 <see cref="upgradeCount"/> — 캐릭터 가이드 p6.
        /// </summary>
        public bool IsPassiveUnlocked(int slot) =>
            definition != null && definition.IsPassiveUnlocked(slot, upgradeCount);

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
            int flat = FlatStatBonus(type);

            if (_statPercentBonus == 0)
                return flat == 0 ? raw : Mathf.Max(1, raw + flat);

            int scaled = Mathf.RoundToInt(raw * (100 + _statPercentBonus) / 100f);
            int cap = Balance != null ? Balance.statMax : 100;
            scaled = Mathf.Clamp(scaled, 1, cap);

            // ★ 고정 보정은 <b>상한을 적용한 뒤에</b> 더한다 — 패시브 정의문이 명시적으로
            //   "이 수치는 최대 능력치 표기 값을 초월할 수 있다" 라고 못박고 있다
            //   ('로 아이아스' 방어력 +8 · '광란' 공격력 +10 · '희열' 공속·이속).
            return Mathf.Max(1, scaled + flat);
        }

        // ------------------------------------------------------------------
        // 고정(flat) 능력치 보정 — 패시브 스킬용 (2026-08-12)
        //
        // <see cref="AddStatPercentBonus"/>(퍼센트)와 갈라 둔 이유: 퍼센트 보정은 상한에 걸리고
        // 능력치 전체에 걸리는데, 패시브는 <b>특정 능력치 하나</b>에 <b>상한을 넘겨</b> 더한다.
        // 같은 칸을 공유하면 정신 이상 '각성'(전체 +10%)과 서로를 지운다.
        // ------------------------------------------------------------------

        /// <summary>스탯 종류별 고정 보정. 값이 0 인 항목은 넣지 않는다(대부분 비어 있다).</summary>
        System.Collections.Generic.Dictionary<StatType, int> _flatBonus;

        int FlatStatBonus(StatType type) =>
            _flatBonus != null && _flatBonus.TryGetValue(type, out int v) ? v : 0;

        /// <summary>
        /// 능력치 하나에 고정값을 더한다. 해제할 때 같은 값을 음수로 넣는다 —
        /// 그래야 여러 패시브가 겹쳐도 서로의 값을 지우지 않는다.
        ///
        /// 체력에 걸면 최대 체력이 즉시 바뀌므로 <b>현재 체력 비율을 유지</b>한다
        /// (<see cref="AddStatPercentBonus"/> 와 같은 이유).
        /// </summary>
        public void AddFlatStatBonus(StatType type, int delta)
        {
            if (delta == 0) return;

            _flatBonus ??= new System.Collections.Generic.Dictionary<StatType, int>();
            _flatBonus.TryGetValue(type, out int now);
            int next = now + delta;
            if (next == 0) _flatBonus.Remove(type);
            else _flatBonus[type] = next;

            if (type != StatType.Hp) return;

            float ratio = HpRatio;
            SetupHealth(Balance, fillHp: false);
            int target = Mathf.Clamp(Mathf.RoundToInt(MaxHp * ratio), 1, MaxHp);
            if (target > CurrentHp) Heal(target - CurrentHp);
            else if (target < CurrentHp) ApplyDamage(CurrentHp - target);
        }

        public override int MaxHp => Balance != null ? Balance.MaxHp(EffectiveStat(StatType.Hp)) : 0;
        public override int DefenseStat => EffectiveStat(StatType.Defense);
        protected override int RegenStat => EffectiveStat(StatType.Regen);

        /// <summary>
        /// 지금 쓰는 공격 능력치. <b>전술 지침의 공격 유형에 따라 달라진다</b> —
        /// 근거리면 근거리 공격력, 원거리면 원거리 공격력, 마법이면 마법, 회복이면 회복력.
        ///
        /// 이 설계 때문에 캐릭터 테이블의 네 공격 계열 능력치가 모두 의미를 갖는다:
        /// 엘린은 마법(8)으로 쓸 때, 프레이야는 근거리(8)로 쓸 때 가장 강하다.
        /// 유형별 능력치가 0 이면 그 유형으로는 최소 피해만 들어간다.
        /// </summary>
        public StatType AttackStatType => AttackTypeOf() switch
        {
            TacticalAttackType.Ranged => StatType.RangedAttack,
            TacticalAttackType.Magic  => StatType.Magic,
            TacticalAttackType.Heal   => StatType.Cure,
            _                         => StatType.Attack,
        };

        public override int AttackStat => EffectiveStat(AttackStatType);

        /// <summary>공격 속도 능력치 → 초당 공격 횟수.</summary>
        public override float StatAttacksPerSecond =>
            Balance != null ? Balance.AttacksPerSecondOf(EffectiveStat(StatType.AttackSpeed)) : 0f;

        /// <summary>이동속도 능력치 → 초당 이동 타일 수.</summary>
        public override float StatMoveSpeedTiles =>
            Balance != null ? Balance.MoveSpeedTilesOf(EffectiveStat(StatType.MoveSpeed)) : 0f;

        /// <summary>명중률 능력치 → 적중 확률(%). 실수 — 0.5% 단위 조정이 가능하다.</summary>
        public override float HitChancePercent =>
            Balance != null ? Balance.HitChancePercent(EffectiveStat(StatType.Accuracy)) : 100f;

        /// <summary>크리티컬 확률 능력치 → 치명타 확률(%). 실수.</summary>
        public override float CriticalChancePercent =>
            Balance != null ? Balance.CriticalChancePercent(EffectiveStat(StatType.Critical)) : 0f;

        /// <summary>저항력 → 침식 상승 배율. 기준점(50)에서 1.0.</summary>
        public float ErosionGainMultiplier =>
            Balance != null ? Balance.ErosionGainMultiplier(stats.resistance) : 1f;

        /// <summary>저항력 → 침식 회복 배율. 상승 배율과 대칭.</summary>
        public float ErosionRecoverMultiplier =>
            Balance != null ? Balance.ErosionRecoverMultiplier(stats.resistance) : 1f;

        UnitCombat _combat;

        /// <summary>
        /// 지금 공격 유형. <see cref="UnitCombat"/> 이 같은 오브젝트에 있으므로 캐시해서 읽는다.
        /// 아직 붙기 전(생성 직후 한 프레임)이면 근거리로 본다.
        /// </summary>
        TacticalAttackType AttackTypeOf()
        {
            if (_combat == null) _combat = GetComponent<UnitCombat>();
            return _combat != null ? _combat.AttackType : TacticalAttackType.Melee;
        }

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

        /// <summary>
        /// 스포너가 복제 직후 호출해 능력치를 주입한다.
        ///
        /// <b>여기서 역할(공격 유형 · 전열 위치 · 성장 유형)까지 정한다</b>(유저 지시 2026-08-14) —
        /// 예전에는 템플릿(<c>Character_Template</c>)의 지침을 그대로 물려받아 <b>누구든 근거리 ·
        /// 중위</b>로 태어났다. 능력치 기반 판단이므로 정의가 없는 무작위 캐릭터
        /// (<see cref="StatBlock.Roll"/> 폴백)도 자기 능력치에 맞는 역할을 받는다 —
        /// 그래서 <see cref="InitializeFrom"/> 이 아니라 <b>두 경로가 공유하는 이 메서드</b>에 뒀다.
        /// </summary>
        public void Initialize(StatBlock rolled, BalanceConfigSO balance, int upgrades = 0)
        {
            stats = rolled;
            upgradeCount = upgrades;
            SetupHealth(balance);   // 최대 체력이 stats 에 의존하므로 stats 대입 후에 호출

            CharacterRole.Apply(this, definition);
        }

        /// <summary>
        /// 캐릭터 테이블의 정의로 생성한다. 능력치는 랜덤 롤이 아니라 정의된 고정값을 그대로 쓴다.
        /// 오브젝트 이름도 인물 이름으로 바꿔 하이라키·로그에서 누구인지 바로 보이게 한다.
        /// </summary>
        public void InitializeFrom(CharacterDefinitionSO def, BalanceConfigSO balance, int upgrades = 0)
        {
            if (def == null) return;

            definition = def;
            Initialize(def.stats, balance, upgrades);

            if (!string.IsNullOrWhiteSpace(def.DisplayName))
                gameObject.name = def.DisplayName;

            ApplyDefinitionSkin(def);
        }

        /// <summary>
        /// 정의에 지정된 외형을 입힌다. <see cref="CharacterAnimator"/> 는 오브젝트가 활성화되는
        /// 순간 <b>무작위로</b> 스킨을 고르므로(스포너가 복제 직후 활성화한다), 그 뒤에 덮어써야 한다.
        ///
        /// 스킨 이름이 비어 있거나 에셋을 못 찾으면 <b>아무것도 하지 않는다</b> —
        /// 그러면 무작위로 고른 스킨이 그대로 남아 최소한 화면에는 뭔가 보인다.
        /// </summary>
        void ApplyDefinitionSkin(CharacterDefinitionSO def)
        {
            if (string.IsNullOrWhiteSpace(def.skinAssetName)) return;

            var animator = GetComponent<CharacterAnimator>();
            if (animator == null) return;

            var skin = Resources.Load<CharacterSkinSO>("Skins/" + def.skinAssetName.Trim());
            if (skin == null)
            {
                Debug.LogWarning($"[Character] 외형 'Resources/Skins/{def.skinAssetName}' 을 찾지 못했습니다. " +
                                 $"({def.DisplayName}) — 무작위로 고른 외형을 그대로 씁니다.", this);
                return;
            }
            animator.SetSkin(skin);
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
            return $"{DisplayName}: {stats}  →  HP {MaxHp} · 타격 {Balance.Attack(AttackStat)} · " +
                   $"피해감소 {Balance.DefenseReductionPercent(stats.defense)}% · " +
                   $"재생 {Balance.RegenPerTick(stats.regen)}/{Balance.regenTickSeconds:0.#}초 · " +
                   $"적중 {HitChancePercent:0.#}% · 치명 {CriticalChancePercent:0.#}% · " +
                   $"공속 {StatAttacksPerSecond:0.##}/초 · 이속 {StatMoveSpeedTiles:0.##}타일/초 · " +
                   $"침식 상승 {ErosionGainMultiplier:0.##}배 / 회복 {ErosionRecoverMultiplier:0.##}배";
        }
    }
}
