using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 체력 · 피해 · 재생을 가진 모든 대상의 공통 베이스.
    /// 캐릭터 / 넥서스 / 포탑 / 몬스터가 모두 이걸 상속해서 피해 계산이 한 곳에서만 돌게 한다.
    ///
    /// 재생 규칙: 전투(공격했거나 피해를 입은 상황)에서 벗어난 뒤
    /// BalanceConfig 의 outOfCombatRegenDelay 초가 지나야 재생이 시작되고,
    /// 그 뒤로는 regenTickSeconds 마다 정수 회복량이 한 번에 들어간다.
    ///
    /// 체력은 정수다 — 최대 체력 · 피해량 · 회복량이 전부 정수라 소수점이 생길 여지가 없다.
    /// </summary>
    public abstract class DamageableUnit : MonoBehaviour
    {
        [Header("현재 상태 (읽기 전용)")]
        [SerializeField] protected int currentHp;

        [SerializeField] BalanceConfigSO balance;

        /// <summary>마지막으로 전투 행동이 있었던 시각. 음의 무한대 = 처음부터 비전투.</summary>
        float _lastCombatTime = float.NegativeInfinity;

        /// <summary>회복 틱 누적 시간.</summary>
        float _regenTimer;

        public event System.Action<int, int> OnHpChanged;       // (현재, 최대)
        public event System.Action<DamageableUnit> OnDied;
        public event System.Action<bool> OnCombatStateChanged;  // true = 전투 진입

        /// <summary>공격이 성사될 때마다 발생 (공격자, 대상). 웨이브 타이머의 전투 개시 판정에 쓴다.</summary>
        public static event System.Action<DamageableUnit, DamageableUnit> OnAnyAttack;

        /// <summary>어떤 유닛이든 죽으면 발생. 넥서스 파괴(패배) 판정에 쓴다.</summary>
        public static event System.Action<DamageableUnit> OnAnyDied;

        /// <summary>도메인 리로드를 꺼도 정적 구독이 남지 않게 초기화.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            OnAnyAttack = null;
            OnAnyDied = null;
            OnAnyMissed = null;
            OnAnyCritical = null;
        }

        bool _wasInCombat;

        // ------------------------------------------------------------------
        // 파생 클래스가 채우는 값
        // ------------------------------------------------------------------

        public abstract int MaxHp { get; }
        public abstract int DefenseStat { get; }
        public abstract int AttackStat { get; }
        protected abstract int RegenStat { get; }

        /// <summary>소속 진영. 타겟 판정의 기준.</summary>
        public abstract Faction Faction { get; }

        /// <summary>유닛 종류. 몬스터의 공격 우선순위 판정에 쓰인다.</summary>
        public abstract UnitKind Kind { get; }

        /// <summary>
        /// <b>외부에서 넣는 회복</b>(치유형 캐릭터 등)을 받을 수 있는지. 기본은 항상 true 이고,
        /// 정신 이상 "이기심"에 걸린 캐릭터만 false 가 된다(<c>CharacterUnit</c> 에서 재정의).
        ///
        /// <see cref="Heal"/> 자체를 막지 않는 이유 — 체력 재생(<see cref="TickRegen"/>)도 같은
        /// <see cref="Heal"/> 을 쓰는데, "이기심"은 <b>본인의 체력 재생은 계속된다</b>가 정의라서
        /// 회복 지점을 막으면 안 된다. 그래서 외부 회복을 넣는 쪽(<c>UnitCombat.PerformHeal</c>)이
        /// 이 값을 확인하고 스스로 물러나는 방식으로 갈랐다.
        /// </summary>
        public virtual bool AcceptsExternalHeal => true;

        /// <summary>
        /// 능력치에서 파생된 초당 공격 횟수. <b>0 이면 "이 유닛은 능력치로 정하지 않는다"</b>는 뜻이고
        /// <c>UnitCombat</c> 이 기존 경로(인스펙터 값 → 밸런스 폴백)를 그대로 쓴다.
        ///
        /// 몬스터·중립·포탑은 능력치 4종만 쓰므로 기본값 0 을 그대로 둔다 — 확장 전과 동작이 같다.
        /// 캐릭터만 <c>CharacterUnit</c> 에서 재정의해 공격속도 능력치를 반영한다.
        /// </summary>
        public virtual float StatAttacksPerSecond => 0f;

        /// <summary>능력치에서 파생된 초당 이동 타일 수. 0 이면 기존 경로를 쓴다.</summary>
        public virtual float StatMoveSpeedTiles => 0f;

        /// <summary>
        /// 공격이 빗나갈 수 있는 유닛인지 — 적중 확률(%). 100 이면 절대 안 빗나간다(기본값).
        /// 몬스터는 명중 능력치가 없으므로 100 을 유지해 확장 전과 동작이 같다.
        /// <b>실수다</b> — 확률은 0.5% 단위 조정이 필요할 수 있어 정수로 깎지 않는다.
        /// </summary>
        public virtual float HitChancePercent => 100f;

        /// <summary>치명타 확률(%). 0 이면 치명타가 없다(기본값). 실수.</summary>
        public virtual float CriticalChancePercent => 0f;

        // ------------------------------------------------------------------
        // 패시브 스킬용 피해 보정 훅 (2026-08-12)
        //
        // 왜 여기(베이스)에 두는가 — 보정을 받는 쪽이 캐릭터일 수도 몬스터일 수도 있다.
        // 예: '부식'(피올로)은 <b>몬스터</b>의 방어력을 깎고, '광란'(프레이야)은 <b>캐릭터</b>의
        // 공격력을 올린다. 파생 클래스마다 따로 두면 두 벌이 되고, 능력치 자체
        // (<c>DefenseStat</c>/<c>AttackStat</c>)를 건드리면 UI 표시값까지 흔들린다 —
        // <b>피해 계산에만</b> 얹히는 별도 칸으로 둔다.
        // ------------------------------------------------------------------

        /// <summary>
        /// 피해 계산에서 이 유닛의 방어력에 더해지는 값(음수 가능). 정신 이상·패시브가 쓴다.
        /// 능력치 자체는 바뀌지 않으므로 로스터·성장 창의 표시값은 그대로다.
        /// 여러 효과가 겹칠 수 있으므로 <b>더하고 빼서</b> 쓴다(덮어쓰지 않는다).
        /// </summary>
        public int DefenseModifier { get; private set; }

        /// <summary>방어력 보정을 더한다. 해제할 때 같은 값을 음수로 넣는다.</summary>
        public void AddDefenseModifier(int delta) => DefenseModifier += delta;

        /// <summary>
        /// <b>다음 한 번의 공격에만</b> 더해지는 공격력. '유혈 낭자'(엘린)처럼
        /// "때릴 때 체력을 깎고 그만큼을 공격력에 더한다" 는 효과가 쓴다.
        ///
        /// <see cref="OnAnyAttack"/> 이 피해 계산 <b>전에</b> 발생하므로, 그 이벤트를 받은 쪽이
        /// 이 값을 채워 넣으면 바로 그 공격에 반영된다. 쓰고 나면 스스로 비워진다 —
        /// 안 비우면 다음 공격까지 새어나간다.
        /// </summary>
        public int OneShotAttackBonus { get; set; }

        // ------------------------------------------------------------------

        public BalanceConfigSO Balance => balance;
        public int CurrentHp => currentHp;

        /// <summary>체력바 등 표시용 비율. 체력 자체는 정수다.</summary>
        public float HpRatio => MaxHp > 0 ? (float)currentHp / MaxHp : 0f;
        public bool IsAlive => currentHp > 0;

        /// <summary>전투 중인지. 마지막 전투 행동으로부터 대기 시간이 안 지났으면 true.</summary>
        public bool IsInCombat =>
            balance != null && Time.time - _lastCombatTime < balance.outOfCombatRegenDelay;

        /// <summary>재생 시작까지 남은 시간(초). 0 이면 재생 중.</summary>
        public float RegenCountdown => balance == null
            ? 0f
            : Mathf.Max(0f, balance.outOfCombatRegenDelay - (Time.time - _lastCombatTime));

        // ------------------------------------------------------------------

        protected void SetupHealth(BalanceConfigSO config, bool fillHp = true)
        {
            balance = config;
            if (fillHp) currentHp = MaxHp;
            else currentHp = Mathf.Min(currentHp, MaxHp);
            _lastCombatTime = float.NegativeInfinity;
            _regenTimer = 0f;
            RaiseHpChanged();
        }

        protected virtual void Start()
        {
            // 인스펙터에서 직접 배치했거나 템플릿 값이 남아 있는 경우 보정
            if (currentHp <= 0 && MaxHp > 0) currentHp = MaxHp;
        }

        protected virtual void OnEnable() => UnitRegistry.Register(this);
        protected virtual void OnDisable() => UnitRegistry.Unregister(this);

        protected virtual void Update()
        {
            TickRegen(Time.deltaTime);
            TickCombatState();
        }

        /// <summary>
        /// 회복은 "틱마다 정수" 로 들어간다. 초당 소수점 회복을 누적하면 체력이
        /// 정수로 떨어지지 않으므로, 간격(regenTickSeconds)마다 정수량을 한 번에 넣는다.
        /// </summary>
        void TickRegen(float dt)
        {
            if (!IsAlive || balance == null) return;

            if (IsInCombat || currentHp >= MaxHp)
            {
                _regenTimer = 0f;                         // 전투 중 / 만피면 틱을 다시 센다
                return;
            }

            int amount = balance.RegenPerTick(RegenStat);
            float interval = Mathf.Max(0.1f, balance.regenTickSeconds);
            if (amount <= 0) return;

            _regenTimer += dt;
            while (_regenTimer >= interval && currentHp < MaxHp)
            {
                _regenTimer -= interval;
                Heal(amount);
            }
        }

        void TickCombatState()
        {
            bool now = IsInCombat;
            if (now == _wasInCombat) return;
            _wasInCombat = now;
            OnCombatStateChanged?.Invoke(now);
        }

        // ------------------------------------------------------------------
        // 전투
        // ------------------------------------------------------------------

        /// <summary>
        /// 전투 행동이 있었음을 기록한다. 재생 대기 시간이 여기서부터 다시 센다.
        /// 공격을 <b>가한</b> 쪽도 반드시 호출해야 한다 (피해를 안 입어도 전투 상태).
        /// </summary>
        public void MarkCombatAction()
        {
            _lastCombatTime = Time.time;
        }

        /// <summary>
        /// 마지막으로 나를 때린 상대. 비선공 유닛의 <b>반격</b> 판정에 쓴다 —
        /// "비선공"은 <b>먼저</b> 공격하지 않는다는 뜻이지 맞고도 가만히 있는다는 뜻이 아니다
        /// (유저 정의). <see cref="UnitCombat"/> 가 이 값을 보고 반격 대상을 잡는다.
        ///
        /// 죽은 상대는 자동으로 비워진다 — 시체를 계속 때리려 드는 걸 막는다.
        /// </summary>
        public DamageableUnit LastAttacker
        {
            get
            {
                if (_lastAttacker != null && !_lastAttacker.IsAlive) _lastAttacker = null;
                return _lastAttacker;
            }
        }

        /// <summary>마지막으로 맞은 시각. 반격을 언제까지 유지할지 판단하는 데 쓴다.</summary>
        public float LastAttackedTime { get; private set; } = float.NegativeInfinity;

        DamageableUnit _lastAttacker;

        /// <summary>
        /// 공격자의 공격력 능력치를 받아 피해를 계산해 적용한다.
        ///
        /// 처리 순서는 「능력치 및 공식 정리.xlsx」의 '데미지 계산' 시트와 같다:
        /// <b>① 명중 판정 → ② 기본 피해 → ③ 치명타 판정 → ④ 적용</b>.
        /// ①③ 은 공격자가 명중·치명타 능력치를 가진 경우에만 실제로 작동한다 —
        /// 몬스터는 기본값(적중 100% · 치명 0%)이라 확장 전과 결과가 완전히 같다.
        ///
        /// <b>빗나가도 <see cref="OnAnyAttack"/> 은 발생시킨다</b> — 이 이벤트가 웨이브 전투 개시
        /// 판정(11절)과 투사체 연출(25-5절)의 트리거라서, 빗나갔다고 발생시키지 않으면
        /// "쏘는데 아무 일도 안 일어나는" 상태가 된다.
        /// </summary>
        public void TakeDamageFrom(DamageableUnit attacker) => TakeDamageFrom(attacker, 100);

        /// <summary>
        /// 공격력에 <paramref name="attackPercent"/> % 를 먹인 뒤 위 순서 그대로 처리한다 —
        /// <b>보스 스킬</b>처럼 "근거리 공격력의 150%" 같은 배수를 가진 공격이 쓴다
        /// (<see cref="BossSkillSO.value03"/>).
        ///
        /// <b>왜 오버로드인가</b> — 기존 <see cref="TakeDamageFrom(DamageableUnit)"/> 의
        /// 시그니처를 건드리면 PROTO 가 쓰는 공개 API 가 바뀐다(준수사항 U-D4). 인자를
        /// 추가하지 않고 오버로드로 얹으면 기존 호출부가 하나도 안 바뀐다.
        ///
        /// 보정은 <b>능력치 단계에서</b> 곱한다 — 최종 피해에 곱하면 방어력 감소를 두 번
        /// 거친 것과 값이 달라진다. 이 프로젝트의 배율은 전부 "능력치에 먼저 곱하고 반올림"
        /// 규칙이다(진행상황 4절).
        /// </summary>
        public void TakeDamageFrom(DamageableUnit attacker, int attackPercent)
        {
            if (!IsAlive || balance == null || attacker == null) return;

            _lastAttacker = attacker;
            LastAttackedTime = Time.time;

            OnAnyAttack?.Invoke(attacker, this);

            // ① 명중 판정 — 빗나가면 피해 0 이고 치명타 판정도 하지 않는다.
            // 확률이 실수라 0~100 실수 난수로 굴린다(정수 난수로 굴리면 81.5% 가 81% 로 깎인다).
            float hit = attacker.HitChancePercent;
            if (hit < 100f && Random.value * 100f >= hit)
            {
                OnAnyMissed?.Invoke(attacker, this);
                return;
            }

            // ② 기본 피해
            //    패시브 보정을 여기서만 얹는다 — 능력치 프로퍼티를 건드리지 않으므로
            //    UI 표시값과 성장 계산은 영향을 받지 않는다(위 훅 주석 참조).
            //    공격력 일회성 보정은 쓰는 즉시 비운다(다음 공격으로 새어나가지 않게).
            int attackStat = attacker.AttackStat + attacker.OneShotAttackBonus;
            attacker.OneShotAttackBonus = 0;
            if (attackPercent != 100)
                attackStat = BalanceConfigSO.ScaleByPercent(attackStat, attackPercent);
            int defenseStat = Mathf.Max(0, DefenseStat + DefenseModifier);
            int damage = balance.Damage(attackStat, defenseStat);

            // ③ 치명타 판정
            float crit = attacker.CriticalChancePercent;
            bool critical = crit > 0f && Random.value * 100f < crit;
            if (critical)
            {
                damage = balance.ApplyCriticalDamage(damage);
                OnAnyCritical?.Invoke(attacker, this);
            }

            // ④ 적용
            ApplyDamage(damage);
        }

        /// <summary>공격이 빗나갔다 (공격자, 대상). MISS 연출용 — 아직 구독자가 없다.</summary>
        public static event System.Action<DamageableUnit, DamageableUnit> OnAnyMissed;

        /// <summary>치명타가 터졌다 (공격자, 대상). 연출용 — 아직 구독자가 없다.</summary>
        public static event System.Action<DamageableUnit, DamageableUnit> OnAnyCritical;

        /// <summary>계산이 끝난 피해량(정수)을 직접 적용한다.</summary>
        public void ApplyDamage(int amount)
        {
            if (!IsAlive || amount <= 0) return;

            MarkCombatAction();       // 피해를 입은 것도 전투 상태
            currentHp -= amount;
            RaiseHpChanged();

            if (currentHp <= 0)
            {
                currentHp = 0;
                OnDeath();              // Destroy 는 프레임 끝에 처리되므로 아래 이벤트는 안전하다
                OnDied?.Invoke(this);
                OnAnyDied?.Invoke(this);
            }
        }

        /// <summary>회복(정수). 재생 대기 시간에는 영향을 주지 않는다(힐러의 회복 등).</summary>
        public void Heal(int amount)
        {
            if (!IsAlive || amount <= 0) return;
            currentHp = Mathf.Min(MaxHp, currentHp + amount);
            RaiseHpChanged();
        }

        /// <summary>
        /// <b>죽은 유닛을 되살린다</b> — 체력을 <paramref name="hp"/> 로 되돌린다.
        ///
        /// <see cref="Heal"/> 로는 할 수 없다: 그쪽은 <c>IsAlive</c> 가드가 있어서
        /// 체력 0 인 유닛에 아무 일도 하지 않는다(회복이 시체를 일으키면 안 되므로 옳은 가드다).
        /// 부활은 <b>그 가드를 의도적으로 넘는 유일한 경로</b>라 따로 뚫어 둔다.
        ///
        /// ⚠ 되살릴 수 있는 것은 <b>아직 파괴되지 않은 유닛</b>뿐이다.
        /// <see cref="OnDeath"/> 가 <c>Destroy</c> 를 건너뛴 경우에만 성립한다 —
        /// 지금은 「분노」(히스톤 80014) 하나가 그렇게 한다.
        ///
        /// 전투 상태를 다시 찍어(<see cref="MarkCombatAction"/>) 일어나자마자 체력이
        /// 재생되지 않게 한다 — 방금 전투 한복판에서 쓰러진 것이므로.
        /// </summary>
        public void ReviveWithHp(int hp)
        {
            if (IsAlive) return;                 // 살아있으면 부활이 아니다
            if (MaxHp <= 0) return;

            currentHp = Mathf.Clamp(hp, 1, MaxHp);
            MarkCombatAction();
            RaiseHpChanged();
            OnRevived?.Invoke(this);
        }

        /// <summary>되살아난 직후 발생. 로스터가 '사망' 표시를 거두는 데 쓴다.</summary>
        public event System.Action<DamageableUnit> OnRevived;

        // ==================================================================
        // 공격 유형 — 세 유닛(캐릭터·몬스터·중립)이 <b>같은 규칙</b>을 쓰게 여기 모았다
        // (2026-08-15)
        //
        // <b>왜 베이스로 내렸나</b> — 같은 판정을 세 클래스에 복붙하면 반드시 갈라진다.
        // 실제로 갈라져 있었다: 캐릭터만 공격 유형별로 공격력을 골랐고
        // (<c>CharacterUnit.AttackStatType</c>), 몬스터·중립은 <b>유형과 무관하게 근거리
        // 공격력 한 칸</b>만 읽고 있었다 — 표에 <c>ranged_atk</c> 칸이 따로 있는데도
        // 파싱이 그 값을 근거리 칸에 접어 넣어 겨우 맞춰 두는 상태였다.
        //
        // ★ <b>명중률·크리티컬은 원거리 공격 유형에만 적용된다</b>(유저 확정 2026-08-15).
        //   근거리·마법·회복은 항상 명중하고 치명타가 나지 않는다. 이 규칙도 캐릭터에만
        //   있던 것을 여기로 올려 <b>몬스터·중립에도 똑같이</b> 걸리게 한 것이다.
        // ==================================================================

        UnitCombat _attackTypeSource;
        bool _attackTypeSearched;

        /// <summary>
        /// 지금 이 유닛의 공격 유형. <see cref="UnitCombat"/> 이 같은 오브젝트에 있으면 그 값이고,
        /// 없으면(넥서스 등) 근거리로 본다. <b>한 번만 찾아 캐시</b>한다 — 피해 계산 경로에서
        /// 불리므로 매번 <c>GetComponent</c> 를 돌면 낭비다.
        /// </summary>
        protected TacticalAttackType AttackTypeOf()
        {
            if (!_attackTypeSearched)
            {
                _attackTypeSource = GetComponent<UnitCombat>();
                _attackTypeSearched = true;
            }
            return _attackTypeSource != null ? _attackTypeSource.AttackType : TacticalAttackType.Melee;
        }

        /// <summary>공격 유형에 맞는 <b>공격력 능력치 종류</b>. 표의 네 공격 계열과 1:1 이다.</summary>
        protected static StatType AttackStatTypeOf(TacticalAttackType type) => type switch
        {
            TacticalAttackType.Ranged => StatType.RangedAttack,
            TacticalAttackType.Magic  => StatType.Magic,
            TacticalAttackType.Heal   => StatType.Cure,
            _                         => StatType.Attack,
        };

        /// <summary>
        /// 지금 공격에 <b>명중률·크리티컬 능력치가 적용되는가</b> — 원거리일 때만 true.
        /// false 면 파생 클래스가 적중 100% / 치명 0% 를 돌려주어 판정이 통째로 생략된다.
        /// </summary>
        protected bool RangedStatsApplyNow => AttackTypeOf() == TacticalAttackType.Ranged;

        protected virtual void OnDeath() { }

        void RaiseHpChanged() => OnHpChanged?.Invoke(Mathf.Max(0, currentHp), MaxHp);
    }
}
