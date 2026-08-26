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

        // ------------------------------------------------------------------
        // ★★ 소환된 유닛인가 (2026-08-20 — 아루의 「강림」 골렘)
        //
        // 골렘은 <b>캐릭터로 만들어졌지만 캐릭터로 세면 안 된다.</b> 정의문이 그렇게 갈라 놓았다:
        //   · <b>로스터에는 뜬다</b>            → 로스터는 이 값을 안 본다
        //   · <b>침식이 일어나지 않는다</b>     → CharacterErosion 을 붙이지 않는다
        //   · <b>전술을 수정할 수 없다</b>      → CharacterTactics 를 잠근다
        //   · <b>후퇴하지 않는다</b>            → 후퇴 기준을 0 으로 잠근다
        //
        // 그리고 여기 플래그가 필요한 이유는 <b>«전멸» 판정</b> 하나 때문이다 —
        // 골렘만 살아남았는데 «아직 캐릭터가 있다» 로 보면 패배 화면이 영원히 안 뜬다
        // (WaveManager 의 전멸 판정 · 결과 화면의 생존자 수).
        // ------------------------------------------------------------------

        /// <summary>스킬로 소환된 유닛인지. 「전멸」 판정과 결과 화면의 생존자 수에서 제외된다.</summary>
        public bool IsSummoned { get; private set; }

        /// <summary>소환 표식을 세운다. <b>되돌리지 않는다</b> — 소환수는 평생 소환수다.</summary>
        public void MarkSummoned() => IsSummoned = true;

        /// <summary>
        /// 지금 정해진 성장 유형. <see cref="CharacterUpgradeService"/> 가 강화할 때 읽는다.
        /// 미선택(<see cref="StatGrowthFocus.None"/>)이면 모든 능력치가 같은 확률로 오른다.
        /// </summary>
        public StatGrowthFocus GrowthFocus => growthFocus;

        /// <summary>성장 유형을 정한다. 캐릭터 성장 창의 유형 버튼이 부르는 유일한 경로다.</summary>
        public void SetGrowthFocus(StatGrowthFocus focus) => growthFocus = focus;

        /// <summary>이 캐릭터가 누구인지. 정의 없이 생성된 캐릭터는 null.</summary>
        public CharacterDefinitionSO Definition => definition;

        /// <summary>초상화 (캐릭터 테이블 <c>illust</c>). 클릭했을 때 <see cref="UI.UnitPortraitPanel"/> 이 띄운다.</summary>
        public override Sprite Portrait => definition != null ? definition.Illust : null;

        /// <summary>
        /// 칭호 (캐릭터 테이블 <c>character_title</c>, 2026-08-19 신설).
        /// 정의가 없거나 표에 칭호가 안 적혀 있으면 <b>빈 문자열</b>이고, 상세 카드는 그 줄을
        /// 비워 둔다 — 유저 확정: "칭호 해금이 되지 않았을 때는 칭호칸 비워놔".
        /// </summary>
        public override string Title => definition != null ? definition.Title : string.Empty;

        /// <summary>
        /// ★★ <b>두 번째 등장부터 배정되는 «다른 이름» 의 스트링 키</b>
        /// (2026-08-26 · <see cref="CharacterAltNames"/>). 비어 있으면 정의의 이름을 쓴다.
        /// ⚠ 이름 <b>문자열</b>이 아니라 <b>키</b>를 들고 있다 — 언어를 바꾸면 대체 이름도
        ///   그 언어로 나와야 한다.
        /// </summary>
        [SerializeField] string altNameKey;

        /// <summary>배정된 대체 이름 키(없으면 빈 문자열). 세이브가 읽고 쓴다.</summary>
        public string AltNameKey => altNameKey;

        /// <summary>
        /// 세이브 복원용 — 저장돼 있던 대체 이름 키를 그대로 되돌린다.
        /// <b>새로 배정하지 않는다</b>(<see cref="CharacterAltNames.MarkRestored"/> 참조).
        /// </summary>
        public void RestoreAltNameKey(string key)
        {
            altNameKey = key;
            ApplyObjectName();
        }

        /// <summary>
        /// 표시 이름. <b>대체 이름이 배정돼 있으면 그것</b>, 아니면 정의의 이름,
        /// 정의도 없으면 오브젝트 이름.
        ///
        /// ★ 화면·로그·엔딩 명단(<c>RunRecord.Describe</c>)이 전부 이 하나를 보므로
        ///   여기만 덮으면 «다른 인물» 로 보인다.
        /// </summary>
        public override string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(altNameKey))
                {
                    string alt = Data.StringTable.Get(altNameKey, null);
                    // 표에서 키가 사라졌으면 키 문자열이 그대로 돌아온다 — 그때는 원래 이름으로.
                    if (!string.IsNullOrWhiteSpace(alt) && alt != altNameKey) return alt;
                }

                return definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName)
                    ? definition.DisplayName
                    : name;
            }
        }

        /// <summary>하이라키 이름을 지금 표시 이름으로 맞춘다(로그·인스펙터에서 누구인지 보이게).</summary>
        void ApplyObjectName()
        {
            string shown = DisplayName;
            if (!string.IsNullOrWhiteSpace(shown)) gameObject.name = shown;
        }

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
            // 최대 체력이 바뀌어 비율을 맞추는 보정이다 — 회복이 아니므로 숫자를 띄우지 않는다.
            if (target > CurrentHp) HealSilently(target - CurrentHp);
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

        // ------------------------------------------------------------------
        // ★ 명중률·크리티컬은 <b>원거리 공격 유형에만</b> 적용된다 (유저 확정 2026-08-15)
        //
        // 회복·근거리·마법으로 싸우는 캐릭터는 <b>항상 명중</b>하고 <b>치명타가 나지 않는다</b>.
        // 회복은 <see cref="UnitCombat"/> 의 <c>PerformHeal</c> 이 <c>TakeDamageFrom</c> 을
        // 아예 안 타므로 원래부터 제외였고, 이번에 근거리·마법이 같은 규칙으로 합류한 것이다.
        //
        // 근거 — 성장 유형 표(82-5절)가 명중률·크리티컬을 <b>원거리 딜러</b> 묶음에만 넣어 두었다.
        // 두 능력치가 모든 유형에 걸리면 그 묶음이 의미를 잃는다(공격력 계열이 유형별로 갈리는
        // <see cref="AttackStatType"/> 과 같은 취지다).
        //
        // ⚠ 판정 자체는 <see cref="DamageableUnit.TakeDamageFrom"/> 한 곳에 있고 그쪽은
        // 공격 유형을 모른다 — <b>확률을 0/100 으로 돌려주는 것</b>으로 유형을 반영한다.
        // 파이프라인을 건드리지 않으므로 몬스터·포탑·보스의 동작은 한 줄도 안 바뀐다.
        // ------------------------------------------------------------------

        /// <summary>
        /// 명중률 능력치 → 적중 확률(%). <b>원거리일 때만</b> 능력치를 본다.
        /// 그 외 유형은 100%(= 절대 안 빗나감)이라 <c>TakeDamageFrom</c> 의 명중 판정이 통째로 생략된다.
        /// </summary>
        public override float HitChancePercent =>
            Balance != null && (AttackTypeOf() == TacticalAttackType.Ranged || FullAccuracyAllowed)
                ? Balance.HitChancePercent(EffectiveStat(StatType.Accuracy))
                : 100f;

        // ------------------------------------------------------------------
        // ★★ 「모든 유형에 명중·크리」 예외 (2026-08-20 — 아르세니아 「불안정성」 80028)
        //
        // 정의문: <i>"아르세니아는 <b>회복과 마법 공격에도</b> 명중률과 크리티컬 확률의
        // 영향을 받습니다"</i>. 위 ★ 규칙(원거리 전용)의 <b>두 번째 예외</b>다 —
        // 첫 번째는 히스톤 「선봉장」의 근거리 크리티컬(<see cref="MeleeCriticalAllowed"/>).
        //
        // <b>왜 「선봉장」 칸을 같이 쓰지 않나</b> — 그쪽은 <b>근거리만</b> 열고 <b>크리티컬만</b>
        // 연다. 이쪽은 <b>모든 유형</b>에 <b>명중까지</b> 연다. 한 칸으로 합치면 두 스킬이
        // 서로의 범위를 넓혀 버린다(히스톤이 갑자기 회복에도 크리가 뜬다).
        // ------------------------------------------------------------------

        int _fullAccuracyGrants;

        /// <summary>지금 «모든 유형에 명중·크리» 예외가 걸려 있는가.</summary>
        public bool FullAccuracyAllowed => _fullAccuracyGrants > 0;

        /// <summary>예외를 더한다. 해제할 때 같은 값을 음수로 넣는다.</summary>
        public void AddFullAccuracyGrant(int delta)
        {
            if (delta == 0) return;
            _fullAccuracyGrants = Mathf.Max(0, _fullAccuracyGrants + delta);
        }

        /// <summary>
        /// 크리티컬 확률 능력치 → 치명타 확률(%). <b>원거리일 때만</b>, 그리고
        /// <see cref="MeleeCriticalAllowed"/> 가 열린 근거리일 때만 능력치를 본다.
        /// </summary>
        public override float CriticalChancePercent =>
            Balance != null && CriticalAppliesToCurrentAttack()
                ? Balance.CriticalChancePercent(EffectiveStat(StatType.Critical))
                : 0f;

        /// <summary>지금 공격 유형이 치명타를 낼 수 있는가.</summary>
        bool CriticalAppliesToCurrentAttack()
        {
            TacticalAttackType type = AttackTypeOf();
            if (type == TacticalAttackType.Ranged) return true;

            // 예외 ② — 「불안정성」(아르세니아 80028)은 <b>모든 유형</b>을 연다.
            if (FullAccuracyAllowed) return true;

            // 예외 — 「선봉장」(히스톤 80013)이 근거리 크리티컬을 열어준다.
            return type == TacticalAttackType.Melee && MeleeCriticalAllowed;
        }

        /// <summary>
        /// 근거리 크리티컬 예외가 지금 걸려 있는가 — 「선봉장」 같은 패시브가 연다.
        /// </summary>
        public bool MeleeCriticalAllowed => _meleeCriticalGrants > 0;

        /// <summary>
        /// 근거리 크리티컬 예외를 건 패시브 수. <b>bool 이 아니라 개수</b>인 이유는
        /// <see cref="AddFlatStatBonus"/> 와 같다 — 두 패시브가 같은 예외를 걸었을 때
        /// 한쪽이 풀리면서 다른 쪽 것까지 지우면 안 된다.
        /// </summary>
        int _meleeCriticalGrants;

        /// <summary>
        /// 근거리 크리티컬 예외를 걸거나(+1) 푼다(−1). 해제할 때 <b>건 값을 음수로</b> 넣는다.
        /// </summary>
        public void AddMeleeCriticalGrant(int delta)
        {
            if (delta == 0) return;
            _meleeCriticalGrants = Mathf.Max(0, _meleeCriticalGrants + delta);
        }

        /// <summary>저항력 → 침식 상승 배율. 기준점(50)에서 1.0.</summary>
        public float ErosionGainMultiplier =>
            Balance != null ? Balance.ErosionGainMultiplier(stats.resistance) : 1f;

        /// <summary>저항력 → 침식 회복 배율. 상승 배율과 대칭.</summary>
        public float ErosionRecoverMultiplier =>
            Balance != null ? Balance.ErosionRecoverMultiplier(stats.resistance) : 1f;

        // ⚠ 공격 유형 조회(<c>AttackTypeOf</c>)는 <see cref="DamageableUnit"/> 로 옮겼다
        //   (2026-08-15) — 몬스터·중립도 같은 규칙을 쓰게 되면서, 세 클래스에 같은 코드가
        //   세 벌 있을 이유가 없어졌다. 여기 있던 사본은 베이스 것을 가려(CS0108) 지웠다.

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
            // 최대 체력이 바뀌어 비율을 맞추는 보정이다 — 회복이 아니므로 숫자를 띄우지 않는다.
            if (target > CurrentHp) HealSilently(target - CurrentHp);
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

            // ★★ 2026-08-26 — <b>이번 판에 두 번째로 등장하는 인물이면 다른 이름을 받는다</b>
            //   (유저 지시: *"같은 캐릭터가 두번째로 등장할때는 랜덤한 다른 이름을 가지고
            //   태어나게 해 다른 인물처럼 보이도록"*).
            //   ⚠ 세이브 복원은 이 문을 지나지만 <c>RestoreAltNameKey</c> 가 <b>뒤에</b> 덮으므로
            //     복원된 이름이 이긴다(그쪽은 세지 않는다 — CharacterAltNames.MarkRestored).
            altNameKey = CharacterAltNames.RegisterAppearance(def.characterId);

            ApplyObjectName();
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
            // 위와 같은 이유 — 능력치 교체에 따른 비율 보정이라 숫자를 띄우지 않는다.
            HealSilently(target - CurrentHp);
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
            // ★ 「분노」(히스톤 80014) — 분노가 가득 차 있으면 <b>파괴하지 않는다</b>.
            //   경직 동안 체력 0 인 채로 누워 있다가 <see cref="Combat.CharacterPassives"/> 가
            //   되살린다. 이 갈림길이 없으면 아래 Destroy 가 먼저 돌아 부활할 몸이 사라진다.
            //
            //   ⚠ 파괴를 건너뛰므로 <b>이 캐릭터는 UnitRegistry 에 계속 남는다</b>. 체력이 0 이라
            //   <b>"지금 싸울 수 있나"를 묻는 곳</b>(타겟 탐색·아우라·회복 대상·UnitCombat/
            //   CharacterBehavior 의 Update 가드)은 전부 IsAlive 로 알아서 걸러진다.
            //
            //   ⚠⚠ 반대로 <b>"이 캐릭터가 없어졌나"를 묻는 곳은 IsAlive 만 보면 틀린다</b> —
            //   그쪽은 <see cref="IsRevivePending"/> 을 같이 봐야 한다. 지금 그렇게 고친 곳:
            //     · <c>WaveManager.CountAliveCharacters</c>   — 안 고치면 마지막 생존자가
            //       쓰러진 프레임에 <b>패배가 확정되어 부활이 오기 전에 게임이 끝난다</b>
            //     · <c>SquadService.HandleAnyDied</c>          — 안 고치면 부대에서 영구 제명된다
            //     · <c>CharacterCreationService.AliveCount</c> — 안 고치면 인원 상한을 넘겨 생성된다
            //     · <c>RelicInventory.HandleAnyDied</c>       — 안 고치면 <b>부활할 캐릭터의
            //       유물이 사라진다</b>(2026-08-26 · 사망 시 유물 소멸 규칙이 생겼다)
            //   ★ <b>죽음을 세는 코드를 새로 만들 때 이 목록에 넣을지 반드시 판단할 것.</b>
            //
            //   ⚠ 이 갈림길을 지나도 <c>OnDied</c>/<c>OnAnyDied</c> 는 <b>그대로 발생한다</b>
            //   (<see cref="DamageableUnit.ApplyDamage"/>) — 쓰러진 것 자체는 사실이기 때문이다.
            //   되돌릴 것이 있는 구독자는 <see cref="DamageableUnit.OnRevived"/> 로 되감는다
            //   (<c>CharacterRosterPanel</c> 의 사망 표시가 그 예다).
            Combat.CharacterPassives passives = Passives;
            if (passives != null && passives.TryBeginRevive())
            {
                Debug.Log($"[Character] {name} 쓰러짐 — 부활 대기", this);
                return;
            }

            Debug.Log($"[Character] {name} 사망", this);

            // ★★ <b>사망 모션이 있으면 그것을 보여주고 나서 치운다</b> (2026-08-20).
            //
            //   유저 지시: *"사망 모션 넣지 않기(스프라이트에 있더라도 <b>단, 골렘은 예외</b>)"*.
            //   그래서 판정을 «누구인가» 가 아니라 <b>«원화에 사망 줄이 있는가»</b> 로 뒀다 —
            //   분해 스크립트가 캐릭터의 사망 줄을 굽지 않으므로(골렘만 굽는다) 그 규칙이
            //   <b>자동으로</b> 성립한다. 새 캐릭터가 늘어도 여기를 고칠 일이 없고,
            //   기획이 «이 캐릭터도 사망 모션» 이라고 하면 <b>원화를 굽는 것만으로</b> 켜진다.
            //
            //   ⚠ 모션이 도는 동안 <b>싸우지 못하게 막는다</b>. 체력이 0 이라 대부분의 코드는
            //     `IsAlive` 로 알아서 걸리지만, 이동·표적 유지처럼 «지금 상태» 를 들고 있는
            //     쪽은 한 프레임 더 움직일 수 있다. 소환 구간이 쓰는 통로를 그대로 쓴다
            //     (<see cref="Combat.SummonDelay"/> — 이름과 달리 «잠시 굳히기» 가 전부다).
            //   ⚠ 파괴를 늦추므로 이 몸은 잠깐 <c>UnitRegistry</c> 에 남는다. 위 「분노」
            //     주석과 <b>같은 성질</b>이고, 그쪽처럼 «없어졌나» 를 묻는 곳이 문제가 되지는
            //     않는다 — 이 경로는 <b>반드시</b> 파괴로 끝나기 때문이다(부활과 다르다).
            var anim = GetComponent<Combat.CharacterAnimator>();
            float deathClip = anim != null ? anim.PlayDeathMotion() : 0f;
            if (deathClip > 0f)
            {
                var freeze = gameObject.GetComponent<Combat.SummonDelay>();
                if (freeze == null) freeze = gameObject.AddComponent<Combat.SummonDelay>();
                freeze.Begin(deathClip);
                Destroy(gameObject, deathClip);
                return;
            }

            // 남겨두면 시체가 성역 주변에 쌓여 "전투가 멈춘 것처럼" 보인다.
            Destroy(gameObject);
        }

        /// <summary>
        /// ★ <b>쓰러졌지만 곧 되살아난다</b> — 「분노」(히스톤 80014)의 경직 구간.
        /// 체력은 0(<c>IsAlive == false</c>)이지만 <b>파괴되지 않았고 반드시 돌아온다.</b>
        ///
        /// <b>왜 공개하나</b> — 죽음을 세는 코드가 두 종류인데 성격이 다르기 때문이다:
        ///   ① <b>"지금 싸울 수 있나"</b>를 묻는 곳(타겟 탐색·아우라·회복 대상)은
        ///      <c>IsAlive</c> 만 보면 맞다 — 3초 동안은 정말로 못 싸운다.
        ///   ② <b>"이 캐릭터가 없어졌나"</b>를 묻는 곳(패배 판정·부대 편성·인원 상한)은
        ///      <c>IsAlive</c> 만 보면 <b>틀린다</b> — 없어진 게 아니라 잠깐 누워 있는 것이다.
        ///      이런 곳이 이 값을 같이 봐야 한다.
        ///
        /// ②를 놓치면 실제로 사고가 난다: 마지막 생존자가 부활 대기에 들어간 순간
        /// 패배가 확정되어 <b>3초 뒤 부활이 오기 전에 게임이 끝난다.</b>
        /// </summary>
        public bool IsRevivePending => Passives != null && Passives.IsReviving;

        Combat.CharacterPassives _passives;

        /// <summary>
        /// 이 캐릭터의 패시브 컴포넌트. 사망 경로에서 부르므로 <b>캐시</b>한다 —
        /// 죽는 순간에 <c>GetComponent</c> 를 도는 건 낭비이고, 이 값은 생애 동안 안 바뀐다.
        /// </summary>
        Combat.CharacterPassives Passives
        {
            get
            {
                if (_passives == null) _passives = GetComponent<Combat.CharacterPassives>();
                return _passives;
            }
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
