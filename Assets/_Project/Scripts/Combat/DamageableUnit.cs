using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 체력 · 피해 · 재생을 가진 모든 대상의 공통 베이스.
    /// 캐릭터 / 성역 / 포탑 / 몬스터가 모두 이걸 상속해서 피해 계산이 한 곳에서만 돌게 한다.
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

        /// <summary>어떤 유닛이든 죽으면 발생. 성역 파괴(패배) 판정에 쓴다.</summary>
        public static event System.Action<DamageableUnit> OnAnyDied;

        /// <summary>도메인 리로드를 꺼도 정적 구독이 남지 않게 초기화.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            OnAnyAttack = null;
            OnAnyDied = null;
            OnAnyMissed = null;
            OnAnyCritical = null;
            OnAnyDamaged = null;
            OnAnyHealed = null;
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
        /// <b>화면·로그에 나가는 이름.</b> 기본은 하이라키 이름이고, 표를 가진 종류
        /// (캐릭터·웨이브 몬스터·중립 몬스터)가 <b>표의 이름</b>으로 재정의한다.
        ///
        /// ★ <b>왜 여기로 올렸나</b> (2026-08-15) — 같은 뜻의 "이 유닛의 표시 이름"을 고르는
        /// 코드가 <c>BattleLogPanel.NameOf</c> · <c>CharacterPassives.DisplayNameOf</c> 두 곳에
        /// <b>따로</b> 있었고, 둘 다 <c>is CharacterUnit</c> / <c>is MonsterUnit</c> 만 보고
        /// <b>중립 몬스터를 빠뜨려</b> 하이라키 이름(일련번호가 붙던 이름)으로 떨어졌다.
        /// 종류가 하나 늘 때마다 두 곳을 같이 고쳐야 하는 구조여서 반드시 한쪽을 빠뜨린다 —
        /// 판정을 <b>유닛 자신</b>에게 물어보는 것으로 바꿔 갈래 자체를 없앴다.
        /// </summary>
        public virtual string DisplayName => name;

        /// <summary>
        /// <b>칭호</b> — 이름 아래에 붙는 수식구(예: 카르시노스 "검은 숲의 종양").
        /// 없으면 빈 문자열이다.
        ///
        /// 표에 <c>*_title</c> 칸이 있는 종(웨이브 보스·에픽 중립)만 값이 있다.
        /// 보스 체력바와 초상화가 <b>같은 값</b>을 쓰게 하려고 여기로 올렸다 —
        /// 예전에는 <c>MonsterUnit</c> 만 갖고 있어서, 중립 에픽의 칭호를 띄우려면
        /// UI 쪽에 <c>is</c> 갈래를 또 하나 만들어야 했다.
        /// </summary>
        public virtual string Title => string.Empty;

        /// <summary>
        /// <b>초상화</b> — 클릭했을 때 띄우는 일러스트. 없으면 null.
        ///
        /// 표의 <c>illust</c> / <c>mon_illust</c> 칸이 가리키는
        /// <c>Resources/Illust/</c> 의 그림이다. 원화가 없는 종(웨이브 몬스터·성역·포탑)은
        /// null 을 돌려주고, 초상화 UI 는 그림 없이 이름·칭호만 보여준다.
        /// </summary>
        public virtual Sprite Portrait => null;

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
        /// ★ <b>전투 중 받는 회복 감소</b>가 이 유닛에 걸리는가 (2026-09-01 · 유저 지시).
        ///
        /// 기본 <b>false</b> — 몬스터·중립·성역·포탑은 예전 그대로 회복한다.
        /// <see cref="LastSanctuary.Units.CharacterUnit"/> 만 <c>true</c> 로 연다.
        ///
        /// <b>왜 «캐릭터인가» 를 여기서 묻지 않나</b> — <c>UnitCombat.PerformHeal</c> 의
        /// <c>FullAccuracyAllowed</c> 와 같은 원칙이다. 조건을 <b>유닛에게 묻는</b> 형태로
        /// 두면, 나중에 같은 규칙을 갖는 유닛이 늘어도 이 파일은 그대로다.
        ///
        /// ⚠ <b>보스를 일부러 뺐다.</b> 보스의 자가 회복은 스킬 표가 «최대 체력 N%» 로
        ///   못박은 값이라, 여기서 반토막을 내면 표에 적힌 숫자와 화면이 달라진다.
        ///   문제가 된 것은 플레이어 쪽 지속력이므로 그쪽만 건드린다.
        /// </summary>
        protected virtual bool UsesInCombatHealPenalty => false;

        /// <summary>
        /// 지금 «전투 중» 이라 받는 회복이 깎이는 상태인가.
        ///
        /// ⚠ <see cref="IsInCombat"/> 와 <b>다른 시간 창</b>을 쓴다 — 그쪽은
        ///   <c>outOfCombatRegenDelay</c>(5초)로 «완전히 물러났나» 를 묻고,
        ///   이쪽은 <c>healInCombatSeconds</c>(3초)로 «지금 맞고 있나» 를 묻는다.
        ///   두 규칙이 같은 칸을 쓰면 재생 대기시간을 만질 때 회복 페널티가 딸려 움직인다.
        /// </summary>
        public bool IsInHealPenaltyCombat =>
            balance != null && Time.time - _lastCombatTime < balance.healInCombatSeconds;

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

        /// <summary>
        /// ★★★ <b>다음 한 번의 공격에만</b> 더해지는 <b>최종 피해</b>(방어력·치명타 계산이
        /// <b>끝난 뒤</b> 더한다). 2026-08-31 신설 — 히스톤 「복수자」(80015)가 쓴다.
        ///
        /// <b><see cref="OneShotAttackBonus"/> 와 무엇이 다른가</b> — 저쪽은 <b>공격력 능력치</b>에
        /// 얹혀 «공격력 − 방어력» 공식을 <b>같이 통과</b>한다. 이 값은 공식을 통과하지 않는다:
        /// <code>
        ///   OneShotAttackBonus : 피해 = 공식(공격력 + N, 방어력)          ← 방어력이 깎아낸다
        ///   OneShotFlatDamage  : 피해 = 공식(공격력, 방어력) + N          ← 방어력이 못 깎는다
        /// </code>
        /// 「복수자」의 정의문이 <i>"근거리 공격 데미지 + 히스톤이 보유한 분노의 데미지
        /// (<b>공식 계산 후 합연산</b>)"</i> 이라고 <b>계산 순서를 못박고</b> 있어서 칸을 따로 뒀다 —
        /// 공격력 자리에 넣으면 방어력 높은 적에게 분노가 통째로 먹히고, 그것은 정의문과 다르다.
        ///
        /// ⚠ <b>치명타가 곱하지 않는다</b> — 정의문의 «공식 계산 후» 에는 치명타 배수도 포함된다고
        ///   읽었다(치명타는 <c>balance.ApplyCriticalDamage</c> 로 공식 안에 있다). 분노에 치명타가
        ///   곱하면 분노 100 · 치명타에서 한 방이 두 배로 튀어 «가끔 죽지 않는 적이 죽는» 편차가 된다.
        /// ⚠ 쓰고 나면 스스로 비워진다 — 안 비우면 다음 공격까지 새어나간다.
        /// </summary>
        public int OneShotFlatDamage { get; set; }

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

                // ⚠ 재생은 숫자를 띄우지 않는다 — 비전투 중 계속 도는 배경 동작이라
                //   띄우면 평시 화면이 초록 숫자로 뒤덮인다(OnAnyHealed 주석 참조).
                HealSilently(amount);
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
        /// ★ <b>재생 대기를 즉시 푼다</b> (2026-08-20 — 아루 「구원」 80023).
        ///
        /// 정의문: <i>"'구원의 손길'로 이송 되어진 아군은 즉시 체력 재생 가능 상태가 됩니다."</i>
        /// 재생 조건은 «마지막 전투 행동으로부터 <see cref="BalanceConfigSO.outOfCombatRegenDelay"/>
        /// 초» 하나뿐이므로(<see cref="IsInCombat"/>), 그 시각을 <b>충분히 과거로</b> 밀면
        /// 그 순간 재생이 가능해진다. <see cref="MarkCombatAction"/> 의 정반대 짝이다.
        ///
        /// ⚠ 체력을 <b>주지는 않는다</b> — 정의문이 "재생 가능 상태" 라고만 했다.
        ///   회복량은 평소 재생 규칙이 그대로 정한다.
        /// </summary>
        public void MakeRegenReady()
        {
            float delay = balance != null ? balance.outOfCombatRegenDelay : 0f;
            _lastCombatTime = Time.time - delay - 1f;
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
                // ⚠ 빗나가도 <b>일회성 최종 피해는 비운다</b> — 안 비우면 그 값이 다음 공격까지
                //   살아남아 <b>두 번 더해진다</b>(호출부가 매 공격마다 += 로 채운다).
                attacker.OneShotFlatDamage = 0;
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

            // ★ 공식 «뒤» 의 합연산 — 히스톤 「복수자」의 분노 피해가 여기로 들어온다
            //   (<see cref="OneShotFlatDamage"/> 의 ⚠ 두 개 참조). ③ 치명타보다 <b>아래</b>에
            //   두어야 «공식 계산 후» 가 되고, 쓰는 즉시 비워야 다음 공격으로 새지 않는다.
            int flat = attacker.OneShotFlatDamage;
            attacker.OneShotFlatDamage = 0;
            if (flat > 0) damage += flat;

            // ④ 적용 — 치명타 여부를 아래 ApplyDamage 가 이벤트에 실어 보낸다.
            _pendingCritical = critical;
            ApplyDamage(damage);
        }

        /// <summary>공격이 빗나갔다 (공격자, 대상). MISS 연출용 — 아직 구독자가 없다.</summary>
        public static event System.Action<DamageableUnit, DamageableUnit> OnAnyMissed;

        /// <summary>치명타가 터졌다 (공격자, 대상). 연출용 — 아직 구독자가 없다.</summary>
        public static event System.Action<DamageableUnit, DamageableUnit> OnAnyCritical;

        /// <summary>
        /// <b>피해가 실제로 들어갔다</b> (공격자, 대상, 피해량, 치명타 여부) — 2026-08-16 신설.
        /// 화면에 <b>데미지 숫자</b>를 띄우는 <see cref="DamageNumberFx"/> 가 이걸 듣는다.
        ///
        /// ★ 기존 <see cref="OnAnyAttack"/> 과 다른 점 — 저쪽은 <b>계산 전</b>에 발생하고
        /// 빗나가도 발생한다(웨이브 전투 개시 감지용이라 그래야 한다). 이 이벤트는
        /// <b>깎인 체력이 확정된 뒤</b>에 그 값과 함께 발생한다.
        ///
        /// ⚠ 공격자는 <b>null 일 수 있다</b> — 패시브의 지속 피해처럼 때린 주체 없이
        /// <see cref="ApplyDamage"/> 를 직접 부르는 경로가 있다. 숫자를 띄우는 데는
        /// <b>맞은 쪽</b>만 있으면 되므로 대상은 언제나 유효하다.
        /// </summary>
        public static event System.Action<DamageableUnit, DamageableUnit, int, bool> OnAnyDamaged;

        /// <summary>
        /// <b>회복이 실제로 들어갔다</b> (회복받은 유닛, 실제로 찬 체력) — 2026-08-17 신설.
        /// <see cref="DamageNumberFx"/> 가 초록 숫자를 띄우는 데 쓴다
        /// (유저 지시: <i>"데미지 표기 처럼 힐 들어가는 숫자도 구현(초록색)으로"</i>).
        ///
        /// ★ <b>요청한 양이 아니라 실제로 찬 양</b>을 싣는다 — 체력이 거의 가득한 대상에게
        /// 100 을 회복시키면 실제로는 3 만 차는데, 화면에 100 이 뜨면 거짓말이 된다.
        ///
        /// ⚠ <b>체력 재생(<see cref="TickRegen"/>)은 이 이벤트를 쏘지 않는다.</b>
        /// 재생은 비전투 중 <c>regenTickSeconds</c> 마다 조용히 도는 배경 동작이라,
        /// 숫자를 띄우면 <b>아무 일도 없는 평시에 화면이 초록 숫자로 뒤덮인다.</b>
        /// 구분은 <see cref="_silentHeal"/> 한 칸으로 한다 — <c>Heal(int)</c> 의 시그니처를
        /// 바꾸지 않으려는 것이다(<see cref="_pendingCritical"/> 과 같은 이유·같은 방식).
        /// </summary>
        public static event System.Action<DamageableUnit, int> OnAnyHealed;

        /// <summary>
        /// 지금 적용되는 피해가 치명타인가. <see cref="TakeDamageFrom"/> 이 찍고
        /// <see cref="ApplyDamage"/> 가 이벤트에 실어 보낸 뒤 지운다 —
        /// <c>ApplyDamage(int)</c> 의 시그니처를 바꾸지 않으려는 것이다(PROTO 가 쓰는 공개 API).
        /// </summary>
        bool _pendingCritical;

        /// <summary>
        /// 지금 들어가는 회복을 <b>화면에 띄우지 않는다</b>. 체력 재생·최대 체력 변동에 따른
        /// 보정처럼 "플레이어가 한 일이 아닌" 회복에만 켠다. <see cref="OnAnyHealed"/> 참조.
        /// </summary>
        bool _silentHeal;

        // ------------------------------------------------------------------
        // ★★ 「통제할 수 없는 쾌락」(시그리드 80018) 의 무적 (2026-08-20)
        //
        // 정의문: <i>"시그리드의 현재 체력이 최대체력의 {v1}% 보다 낮아지면 {v2}초 동안
        // 시그리드가 어떠한 데미지도 받지 않습니다. <b>회복은 가능합니다.</b> 해당 효과는
        // <b>체력의 변화로 해제되지 않습니다.</b>"</i>
        //
        // ★ <b>왜 <see cref="ApplyDamage"/> 에 두는가</b> — 피해가 체력을 깎는 자리는 여기
        //   <b>하나</b>다. <see cref="TakeDamageFrom"/> 도 · 지속 피해도 · 보스 스킬도 결국
        //   이 함수를 부른다. 위쪽 경로마다 막으면 새로 생긴 피해 경로가 조용히 무적을 뚫는다.
        //
        // ★ 「회복은 가능」 은 저절로 성립한다 — <see cref="Heal"/> 은 이 함수를 안 거친다.
        // ★ 「체력의 변화로 해제되지 않는다」 도 저절로 성립한다 — <b>시각 하나</b>로만
        //   표현되는 상태라 체력을 보지 않는다(UnitCombat 의 「허약」·「구속」과 같은 규칙).
        // ------------------------------------------------------------------

        /// <summary>무적이 끝나는 시각. 0 이면 안 걸렸다.</summary>
        float _invulnerableUntil;

        /// <summary>지금 어떤 피해도 받지 않는지.</summary>
        public bool IsInvulnerable => Time.time < _invulnerableUntil;

        /// <summary>
        /// <paramref name="seconds"/> 초 동안 <b>모든 피해를 무시</b>한다. 회복은 그대로 된다.
        /// 이미 걸려 있으면 <b>더 긴 쪽</b>으로 둔다 — 짧은 것이 긴 것을 덮어 깎으면
        /// "무적이 도중에 풀린다" 는 사고가 난다.
        /// </summary>
        public void GrantInvulnerability(float seconds)
        {
            if (seconds <= 0f) return;
            _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + seconds);
        }

        /// <summary>
        /// <b>자기 스킬의 대가</b>로 체력을 깎는다 — 「가학증」처럼 «내가 내는 값» 이다.
        ///
        /// ★ <see cref="ApplyDamage"/> 와 <b>두 가지가 다르다</b>:
        /// <list type="number">
        /// <item><b>무적을 무시한다.</b> 무적은 «남이 주는 피해» 를 막는 것이고, 대가는
        ///       내가 내는 것이다. 무적 중에 대가만 사라지면 스킬이 공짜가 된다.</item>
        /// <item><b>이걸로는 죽지 않는다</b> — 체력 1 에서 멈춘다. 「유혈 낭자」·「광란」이
        ///       이미 같은 규칙을 손으로 지키고 있었다("정의문에 사망 처리가 없다").
        ///       규칙을 한 곳에 두어 다음 스킬이 그 clamp 를 빠뜨리지 않게 한다.</item>
        /// </list>
        /// </summary>
        public void LoseHpToSelfCost(int amount)
        {
            if (!IsAlive || amount <= 0) return;
            int safe = Mathf.Min(amount, Mathf.Max(0, currentHp - 1));
            if (safe <= 0) return;
            ApplyDamageCore(safe);
        }

        // ------------------------------------------------------------------
        // ★★ 보호막 (2026-08-20 — 카이론 「타락한 육체」 80025)
        //
        // <b>무적과 무엇이 다른가</b> — 무적은 «시간» 이 다할 때까지 <b>전부</b> 막고,
        // 보호막은 «양» 이 다할 때까지 막는다. 정의문이 «최대 체력의 20% 짜리 보호막» 이라고
        // <b>양</b>으로 적어 두었으므로 시간만으로는 표현할 수 없다.
        //
        // <b>왜 여기(체력이 깎이는 자리)에 두는가</b> — 무적과 완전히 같은 이유다:
        // 피해 경로가 여럿(평타·지속 피해·보스 스킬)인데 <see cref="ApplyDamageCore"/> 는
        // 하나다. 위쪽에서 막으면 새 경로가 조용히 보호막을 뚫는다.
        //
        // ⚠ <b>대가(<see cref="LoseHpToSelfCost"/>)는 막지 않는다</b> — 그건 «내가 내는 값» 이라
        //   무적도 안 막는다. 보호막이 자기 스킬 비용까지 대신 내면 스킬이 공짜가 된다.
        // ⚠ 시간이 다하면 <b>남은 양은 사라진다</b>(정의문 "value01 초 동안").
        // ------------------------------------------------------------------

        int _shield;
        float _shieldUntil;

        /// <summary>지금 남아 있는 보호막의 양. 시간이 지났으면 0.</summary>
        public int Shield => Time.time < _shieldUntil ? _shield : 0;

        /// <summary>보호막이 걸려 있는지 (UI·로그가 쓴다).</summary>
        public bool HasShield => Shield > 0;

        /// <summary>
        /// <paramref name="amount"/> 만큼의 보호막을 <paramref name="seconds"/> 초 동안 건다.
        /// 이미 걸려 있으면 <b>더 큰 쪽·더 긴 쪽</b>으로 둔다 — 무적과 같은 규칙이고 이유도 같다
        /// (약한 것이 강한 것을 덮어 깎으면 "보호막이 도중에 얇아진다" 는 사고가 난다).
        /// </summary>
        public void GrantShield(int amount, float seconds)
        {
            if (amount <= 0 || seconds <= 0f) return;
            _shield = Mathf.Max(Shield, amount);
            _shieldUntil = Mathf.Max(_shieldUntil, Time.time + seconds);
        }

        /// <summary>보호막을 즉시 없앤다.</summary>
        public void ClearShield()
        {
            _shield = 0;
            _shieldUntil = 0f;
        }

        /// <summary>
        /// 보호막으로 <paramref name="amount"/> 를 흡수하고 <b>남은 피해</b>를 돌려준다.
        /// 보호막이 없으면 그대로 돌려준다.
        /// </summary>
        int AbsorbWithShield(int amount)
        {
            int shield = Shield;
            if (shield <= 0) return amount;

            int absorbed = Mathf.Min(shield, amount);
            _shield = shield - absorbed;
            if (_shield <= 0) _shieldUntil = 0f;
            return amount - absorbed;
        }

        /// <summary>계산이 끝난 피해량(정수)을 직접 적용한다.</summary>
        public void ApplyDamage(int amount)
        {
            if (!IsAlive || amount <= 0) return;

            // ★★ 무적 — 체력을 건드리지 않고 그대로 돌아간다 (위 주석).
            //    ⚠ 숫자도 띄우지 않는다: "0" 이 뜨면 빗나간 것과 구분이 안 된다.
            if (IsInvulnerable)
            {
                _pendingCritical = false;
                return;
            }

            // ★ 보호막이 먼저 먹는다. 전부 막았으면 무적과 같게 <b>조용히</b> 돌아간다.
            amount = AbsorbWithShield(amount);
            if (amount <= 0)
            {
                _pendingCritical = false;
                return;
            }

            ApplyDamageCore(amount);
        }

        /// <summary>
        /// ★ <b>체력이 실제로 깎이는 자리 — 여기 하나뿐이다.</b>
        /// <see cref="ApplyDamage"/>(무적 판정을 거친다)와
        /// <see cref="LoseHpToSelfCost"/>(무적을 무시한다)가 둘 다 이걸 부른다.
        /// 사망 처리·연출 이벤트를 두 벌로 만들지 않으려고 갈라 뒀다.
        /// </summary>
        void ApplyDamageCore(int amount)
        {

            MarkCombatAction();       // 피해를 입은 것도 전투 상태
            currentHp -= amount;
            RaiseHpChanged();

            // 데미지 숫자 연출(2026-08-16). 여기에 두는 이유 — 지속 피해처럼
            // TakeDamageFrom 을 거치지 않고 이 함수를 직접 부르는 경로도 숫자가 뜬다.
            OnAnyDamaged?.Invoke(_lastAttacker, this, amount, _pendingCritical);
            _pendingCritical = false;

            if (currentHp <= 0)
            {
                currentHp = 0;
                OnDeath();              // Destroy 는 프레임 끝에 처리되므로 아래 이벤트는 안전하다
                OnDied?.Invoke(this);
                OnAnyDied?.Invoke(this);
            }
        }

        /// <summary>회복(정수). 재생 대기 시간에는 영향을 주지 않는다(힐러의 회복 등).</summary>
        // ------------------------------------------------------------------
        // ★★ 받는 회복 증폭 (2026-08-20 — 아르세니아 「성스러운 축복」 80029)
        //
        // 정의문: <i>"해당 공간에 있는 … 캐릭터는 자신이 <b>받는 회복 효과</b>가
        // {value_03} 만큼 증폭됩니다"</i>. «주는 쪽» 이 아니라 <b>«받는 쪽»</b> 이 기준이라
        // 회복을 <b>넣는 자리</b>(여기)에서 곱하는 것이 맞다 — 회복 경로가 여럿이기 때문이다
        // (평타 치유 · 「희생」 · 「복수자」 · 체력 재생).
        //
        // ⚠ <b>여러 공간이 겹치면 더한다</b>(곱하지 않는다). 곱하면 두 겹만으로 44% 가 되어
        //   표의 «20%» 라는 값이 뜻을 잃는다.
        // ------------------------------------------------------------------

        int _healReceivedPercent;

        /// <summary>지금 받는 회복이 몇 % 늘어나는지. 0 이면 평소와 같다.</summary>
        public int HealReceivedPercent => _healReceivedPercent;

        /// <summary>증폭을 더한다. 뺄 때는 같은 값을 음수로 넣는다(겹침을 지우지 않기 위해).</summary>
        public void AddHealReceivedPercent(int deltaPercent)
        {
            _healReceivedPercent = Mathf.Max(-100, _healReceivedPercent + deltaPercent);
        }

        /// <summary>증폭을 반영한 실제 회복량.</summary>
        int AmplifiedHeal(int amount) =>
            _healReceivedPercent == 0
                ? amount
                : Mathf.Max(0, Mathf.RoundToInt(amount * (100 + _healReceivedPercent) / 100f));

        public void Heal(int amount) => Heal(amount, false);

        /// <summary>
        /// ★ <b>치명타 회복</b>을 받는 갈래 (2026-08-20 — 아르세니아 「불안정성」 80028).
        ///
        /// 유저 확정: *"아르세니아의 회복이 크리티컬로 터질때 150%로 회복됨"*.
        /// <paramref name="critical"/> 은 <b>연출에만</b> 쓴다 — 배율은 부르는 쪽
        /// (<see cref="UnitCombat"/> 의 <c>PerformHeal</c>)이 이미 곱해서 넘긴다.
        ///
        /// <b>왜 곱셈을 여기서 안 하나</b> — 회복 경로가 여럿이다(평타 치유 · 「희생」 ·
        /// 「복수자」 · 체력 재생). 치명타는 <b>«평타 치유» 하나에만</b> 걸리는 것이
        /// 정의문이고, 여기서 곱하면 다른 경로에도 새어 들어간다.
        ///
        /// ⚠ 「받는 회복 증폭」과의 순서 — 치명타는 <b>주는 쪽</b>, 증폭은 <b>받는 쪽</b>이다.
        ///   그래서 치명타를 먼저(부르는 쪽) 곱하고 증폭을 나중에(여기) 곱한다.
        ///   순서를 뒤집으면 결과는 같지만, «누구의 규칙인가» 가 코드에서 사라진다.
        /// </summary>
        public void Heal(int amount, bool critical)
        {
            if (!IsAlive || amount <= 0) return;

            // ★★ 받는 회복 증폭을 <b>여기서</b> 먹인다 (위 AmplifiedHeal 주석).
            amount = AmplifiedHeal(amount);
            if (amount <= 0) return;

            // ★★★ <b>전투 중 받는 회복 감소</b> (2026-09-01 · 유저 지시).
            //
            //   증폭(성역 등) <b>다음에</b> 곱한다 — 증폭은 «이 캐릭터가 회복을 잘 받는다»,
            //   이쪽은 «지금 맞고 있어서 잘 안 받는다» 라 서로 다른 층이다. 순서를 뒤집어도
            //   값은 같지만, 이 순서라야 «성역 안에서도 전투 중이면 깎인다» 가 코드로 읽힌다.
            //
            //   ⚠ <c>_silentHeal</c> 은 <b>거른다</b>. 그 갈래로 오는 것은 체력 재생과
            //     레벨업 체력 보정인데, 재생은 애초에 전투 중에 돌지 않고
            //     레벨업 보정은 «최대 체력의 N% 로 맞춘다» 는 대입이라
            //     배율을 먹이면 목표 체력에 못 미쳐 값이 깨진다.
            if (!_silentHeal && UsesInCombatHealPenalty && IsInHealPenaltyCombat && balance != null)
            {
                amount = Mathf.RoundToInt(amount * balance.InCombatHealMultiplier);
                if (amount <= 0) return;
            }

            // ★ 실제로 찬 양을 재서 이벤트에 싣는다 — 요청량이 아니다(OnAnyHealed 주석 참조).
            int before = currentHp;
            currentHp = Mathf.Min(MaxHp, currentHp + amount);
            int applied = currentHp - before;

            RaiseHpChanged();

            if (applied <= 0 || _silentHeal) return;

            // ⚠ 시그니처를 안 바꾸려고 «지금 회복이 치명타인가» 를 칸 하나로 넘긴다 —
            //   <see cref="_pendingCritical"/> 과 <b>같은 방식</b>이다(그쪽 주석 참조).
            //   이벤트는 동기로 발생하므로 구독자가 읽는 시점에 이 값이 유효하다.
            _pendingHealCritical = critical;
            OnAnyHealed?.Invoke(this, applied);
            _pendingHealCritical = false;
        }

        bool _pendingHealCritical;

        /// <summary>
        /// 지금 <see cref="OnAnyHealed"/> 로 나가는 회복이 <b>치명타</b>인가.
        /// 구독자가 <b>그 이벤트 처리 중에만</b> 읽는 값이다(그 밖에서는 언제나 false).
        /// </summary>
        public bool PendingHealCritical => _pendingHealCritical;

        /// <summary>
        /// ★ <b>「빗나감」을 밖에서 알린다</b> (2026-08-20).
        ///
        /// <see cref="OnAnyMissed"/> 는 static 이벤트라 <b>이 클래스 밖에서는 못 쏜다</b>.
        /// 그런데 회복의 명중 판정은 <see cref="UnitCombat"/> 의 <c>PerformHeal</c> 에 있다 —
        /// 회복이 피해 파이프라인(<see cref="TakeDamageFrom"/>)을 지나지 않기 때문이다.
        /// 그래서 <b>쏘는 창구</b>만 열어 둔다. 판정을 여기로 옮기지 않는 이유는
        /// 그쪽이 «회복» 이고 이 함수가 «피해» 이기 때문이다.
        /// </summary>
        public static void RaiseMissed(DamageableUnit attacker, DamageableUnit victim)
        {
            if (victim == null) return;
            OnAnyMissed?.Invoke(attacker, victim);
        }

        /// <summary>
        /// <b>숫자를 띄우지 않는 회복.</b> 체력 재생·최대 체력 변동 보정처럼 플레이어의 행동이
        /// 아닌 회복에 쓴다. 회복 자체는 <see cref="Heal"/> 과 완전히 같다 —
        /// 회복 로직을 두 벌로 만들면 한쪽만 고치는 사고가 난다.
        /// </summary>
        public void HealSilently(int amount)
        {
            _silentHeal = true;
            Heal(amount);
            _silentHeal = false;
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
        /// 없으면(성역 등) 근거리로 본다. <b>한 번만 찾아 캐시</b>한다 — 피해 계산 경로에서
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
