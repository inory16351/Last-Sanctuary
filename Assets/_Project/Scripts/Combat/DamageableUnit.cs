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

        /// <summary>공격자의 공격력 능력치를 받아 피해를 계산해 적용한다.</summary>
        public void TakeDamageFrom(DamageableUnit attacker)
        {
            if (!IsAlive || balance == null || attacker == null) return;

            OnAnyAttack?.Invoke(attacker, this);
            ApplyDamage(balance.Damage(attacker.AttackStat, DefenseStat));
        }

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

        protected virtual void OnDeath() { }

        void RaiseHpChanged() => OnHpChanged?.Invoke(Mathf.Max(0, currentHp), MaxHp);
    }
}
