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
    public class NeutralMonsterUnit : DamageableUnit, IBossSkillOwner
    {
        /// <summary>표의 <c>mon_skill_1~2</c>. 순서가 곧 스킬 슬롯 번호다 (에픽만 값이 있다).</summary>
        public int[] BossSkillIds => definition != null ? definition.skillIds : null;

        /// <summary>정의가 들어왔는가 — 스포너가 <c>Initialize</c> 를 부른 뒤에 true.</summary>
        public bool SkillsReady => definition != null;

        [Header("데이터")]
        [SerializeField] NeutralMonsterDefinitionSO definition;

        [Header("능력치 (웨이브 배율 없음)")]
        [SerializeField] StatBlock stats;

        public NeutralMonsterDefinitionSO Definition => definition;
        public StatBlock Stats => stats;

        /// <summary>
        /// 이 개체를 가리키는 <b>런타임 고유 번호</b> (2026-08-18 신설 — 저장 복원용).
        ///
        /// ★ <b>왜 필요한가</b> — 표의 <c>monId</c> 는 <b>종(種)</b>을 가리키지 <b>개체</b>를
        /// 가리키지 않는다. 같은 종이 여러 마리 있을 수 있고(<c>maxAlive</c> &gt; 1),
        /// 발견 목록(<see cref="EpicSubjugationService.Discovered"/>)과 토벌 명령
        /// (<see cref="EpicSubjugationService.SetOrder"/>)은 <b>특정 개체 하나</b>를 가리켜야 한다.
        ///
        /// 저장할 때는 이 번호를 그대로 적고, 복원할 때는 <see cref="NeutralMonsterSpawner"/> 가
        /// <b>같은 번호를 그 개체에 다시 매긴다</b> — 그래야 "부대 2가 노리던 그 개체"를
        /// 저장 전과 똑같이 다시 가리킬 수 있다.
        /// </summary>
        public int SpawnId { get; private set; }

        /// <summary>스포너만 부른다 — 소환 직후(신규) 또는 복원 직후(저장된 값 재부여) 한 번뿐이다.</summary>
        public void AssignSpawnId(int id) => SpawnId = id;

        /// <summary>
        /// 화면·로그에 쓰는 이름 — <b>표의 <c>mon_name</c>(스트링 키)</b> 이 정본이다.
        ///
        /// <see cref="MonsterUnit.DisplayName"/> · <see cref="CharacterUnit.DisplayName"/> 과
        /// 같은 자리다. 중립만 이게 없어서 로그(<c>BattleLogPanel.NameOf</c>)가
        /// <c>unit.name</c>(하이라키 이름)으로 떨어졌고, 스포너가 붙이던 일련번호가
        /// 그대로 찍혔다(유저 지시 2026-08-15: "동일 개체면 다 해당 개체 이름으로").
        /// 스포너 쪽 번호는 없앴지만, <b>하이라키 이름이 어떻게 바뀌든 로그가 흔들리지 않게</b>
        /// 표시 이름을 여기서 명시적으로 준다.
        /// </summary>
        public override string DisplayName =>
            definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.DisplayName
                : name;

        /// <summary>칭호 (표 <c>mon_title</c>). 에픽만 값이 있다 — 예: 카르시노스 "검은 숲의 종양".</summary>
        public override string Title => definition != null ? definition.Title : string.Empty;

        /// <summary>초상화 (표 <c>mon_illust</c>). 클릭했을 때 <see cref="UI.UnitPortraitPanel"/> 이 띄운다.</summary>
        public override Sprite Portrait => definition != null ? definition.Illust : null;

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
                if (!_animatorResolved)
                {
                    _animator = GetComponent<CharacterAnimator>();
                    _animatorResolved = true;
                }
                if (_animator == null) return 0f;

                Vector2 box = _animator.ColliderSizeTiles;
                return box.x > 0.01f && box.y > 0.01f
                    ? Mathf.Min(box.x, box.y) * 0.5f
                    : 0f;
            }
        }

        CharacterAnimator _animator;

        /// <summary>
        /// <see cref="_animator"/> 를 이미 찾아봤는지. <b>없다는 결과도 캐시해야 한다</b> —
        /// 「null 이면 다시 찾는다」 는 형태는 <b>스킨이 없는 종</b>(1001~1003 은
        /// <see cref="CharacterAnimator"/> 자체가 없다)에서 <c>GetComponent</c> 를 매번 다시
        /// 부른다. 예전에는 이 속성을 타겟 하나당 한 번만 읽어서 티가 안 났지만,
        /// <c>UnitCombat.Separation</c> 이 <b>주변 유닛 전체</b>의 몸집을 매 프레임 읽게 된
        /// 뒤로는 유닛 수의 제곱만큼 <c>GetComponent</c> 가 돈다.
        /// </summary>
        bool _animatorResolved;

        /// <summary>
        /// ★★ <b>이 개체에 걸린 사냥 성장 배율</b> (2026-08-21 · <see cref="NeutralKillTally"/>).
        /// <b>소환 순간에 굳는다</b> — 이미 서 있는 개체가 갑자기 세지지 않는다.
        /// 능력치에 이미 반영돼 있다.
        /// ⚠ <b>보상 에너지는 이 값을 쓰지 않는다</b>(2026-08-25) — <see cref="EnergyMultiplier"/>
        ///   라는 <b>별개의 배율</b>을 쓴다. 아래 <see cref="RollEnergyReward"/> 참조.
        /// </summary>
        public float GrowthMultiplier { get; private set; } = 1f;

        /// <summary>
        /// ★ <b>적정 레벨</b> — 표 <c>recommend_level</c> 그대로. 0 이면 «없음» 이다
        /// (2026-08-25 · 토벌 지시 창이 읽는다).
        /// </summary>
        public int RecommendLevel => definition != null ? definition.recommendLevel : 0;

        /// <summary>
        /// ★★★ <b>이 개체의 «자원» 배율</b> — 능력치 배율과 <b>다른 값</b>이다 (2026-08-25).
        ///
        /// 예전에는 <see cref="GrowthMultiplier"/> 하나를 능력치와 보상에 <b>똑같이</b> 썼고,
        /// 능력치 배율에 상한이 없어서 한 판 자원 수입이 처치 수의 <b>제곱</b>으로 불어났다
        /// (<see cref="NeutralGrowthService"/> 의 ★★★). 이제 자원은 제 몫·제 상한을 쓴다.
        /// ⚠ 능력치와 마찬가지로 <b>소환 순간에 굳는다</b> — 방금 잡은 한 마리가 자기 보상을
        ///   올려주지 않는다.
        /// </summary>
        public float EnergyMultiplier { get; private set; } = 1f;

        /// <summary>
        /// 처치 시 지급할 에너지를 이 범위에서 무작위로 뽑는다 (정의 테이블 min/max_energy).
        ///
        /// ★★ 2026-08-21 — <b>사냥 성장 배율을 곱한다</b> (유저 지시: *"중립 몬스터 성장
        /// 배율에 자원값도 배율 적용 되어야 해"*). 강해진 개체가 같은 자원을 준다면
        /// «잡기만 어렵고 이득은 같다» 가 되어 사냥할 이유가 줄어든다.
        ///
        /// ★★★ 2026-08-25 — <b>그런데 «같은 배율» 은 너무 셌다</b> (유저 리포트: *"자원 성장이
        /// 너무 기하급수적"*). 이제 <see cref="EnergyMultiplier"/> 라는 <b>별개의 배율</b>을 쓴다 —
        /// 능력치는 그대로 두고 자원만 완만하게, 그리고 <b>상한이 있게</b> 오른다
        /// (<see cref="NeutralGrowthService"/> 의 ★★★).
        /// ⚠ 배율은 <b>이 개체가 태어난 시점</b>의 값이다 — 방금 잡은 한 마리가 자기 보상을
        ///   올려주지 않는다(그러면 «마지막 한 마리만 이득» 이 되어 계단이 흐려진다).
        /// ⚠ 끄는 손잡이: <see cref="BalanceConfigSO.neutralHuntGrowthScalesEnergy"/>.
        /// </summary>
        public int RollEnergyReward()
        {
            if (definition == null) return 0;

            int rolled = Random.Range(definition.minEnergy, definition.maxEnergy + 1);

            // ★★ 2026-08-25 — <b>능력치 배율이 아니라 «자원» 배율</b>을 쓴다.
            //   끄기·비율·상한은 전부 EnergyMultiplier 안에서 이미 판단됐다
            //   (GameSystems ▸ NeutralGrowthService 의 «자원 획득량 성장» 칸 셋).
            if (EnergyMultiplier <= 1f) return rolled;

            return Mathf.Max(rolled, Mathf.RoundToInt(rolled * EnergyMultiplier));
        }

        /// <summary>스포너가 복제 직후 호출한다.</summary>
        public void Initialize(NeutralMonsterDefinitionSO def, BalanceConfigSO balance)
        {
            definition = def;

            // ★ 사냥 성장 — 같은 종을 잡을수록 다음 개체가 강해진다(2026-08-21).
            //   세는 곳은 아래 OnDeath · 수치는 <b>하이라키</b> GameSystems ▸ NeutralGrowthService.
            //   ★★ 2026-08-24 — <b>종별 성장 배율</b>(표 growth_per_kill)을 넘긴다(S6).
            //     0 이면 예전처럼 씬의 전역 값으로 떨어진다.
            GrowthMultiplier = def != null
                ? NeutralKillTally.MultiplierFor(def.monId, def.growthPerKill)
                : 1f;

            // ★★ 자원 배율은 <b>따로</b> 굳힌다 — 능력치와 같은 값이 아니다(2026-08-25).
            EnergyMultiplier = def != null
                ? NeutralKillTally.EnergyMultiplierFor(def.monId, def.growthPerKill)
                : 1f;

            stats = def != null ? def.BuildStats(GrowthMultiplier, balance) : new StatBlock { hp = 1 };
            SetupHealth(balance);

            NeutralGrowthService cfg = NeutralGrowthService.Instance;
            if (cfg != null && cfg.LogGrowth && def != null)
                Debug.Log($"[중립성장] {def.DisplayName}(id {def.monId}) · 처치 " +
                          $"{NeutralKillTally.KillsOf(def.monId)}마리 · " +
                          $"{NeutralKillTally.StepsOf(def.monId)}단계 · 능력치 x{GrowthMultiplier:0.##} " +
                          $"· 자원 x{EnergyMultiplier:0.##} → 체력 {MaxHp}", this);
        }

        protected override void OnDeath()
        {
            // ★ 종별 처치 수를 센다 — 다음에 이 종이 소환될 때의 배율이 여기서 오른다.
            //   ⚠ 보상 에너지보다 <b>뒤</b>에 세도 상관없다: 보상은 이 개체가 <b>태어날 때</b>
            //     굳은 배율을 쓴다(위 RollEnergyReward). 즉 순서에 의존하지 않는다.
            if (definition != null) NeutralKillTally.Record(definition.monId);

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
