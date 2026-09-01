using UnityEngine;
using LastSanctuary.Units;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 캐릭터 한 명의 침식 수치와 현재 정신 이상 상태. 규칙·수치는 <see cref="ErosionService"/> 가
    /// 들고 있고 이 컴포넌트는 <b>상태만</b> 들고 있다(전술 지침이 <c>CharacterTactics</c>(상태) ↔
    /// <c>TacticalOrder</c>(값)로 갈려 있는 것과 같은 구조).
    ///
    /// <b>Update 가 없다</b> — 진행은 <see cref="ErosionService.Update"/> 가 <see cref="Tick"/> 을
    /// 불러 밀어준다. 캐릭터는 프레임 중간에 <c>Instantiate</c> 되어 <c>Start</c> 가 <c>Update</c>
    /// 보다 늦게 도는 함정이 있는데(진행상황 27-9·28-4절에서 두 번 겪었다), 진행 주체를 서비스로
    /// 옮기면 그 순서 문제가 아예 생기지 않는다.
    ///
    /// <b>지속 효과는 반드시 되돌린다</b> — 각성(능력치 %)·이기심(치유 차단)·공포/광분/혼란(행동
    /// 오버라이드)은 지속시간이 끝나거나 캐릭터가 죽을 때 <see cref="ClearActive"/> 에서 원상복구
    /// 한다. 한 곳에서만 되돌리도록 모아둔 이유는, 해제 경로가 여러 갈래(만료·사망·비활성)라
    /// 흩어놓으면 하나를 빠뜨려 효과가 영구히 남기 때문이다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterErosion : MonoBehaviour
    {
        [Header("현재 상태 (읽기 전용 — 규칙과 수치는 GameSystems/ErosionService 에 있다)")]
        [SerializeField] float erosion;

        CharacterUnit _character;
        DamageableUnit _self;
        UnitCombat _combat;
        CharacterBehavior _behavior;

        MentalErrorDefinitionSO _active;
        float _activeEndTime;

        /// <summary>웨이브 전투에서 벗어난 뒤 흐른 시간. 회복 대기(지연)를 재는 데 쓴다.</summary>
        float _outOfCombatTime;

        /// <summary>초당 소수 피해(피학·역겨움)를 정수로 만들 때까지 모아두는 나머지.</summary>
        float _selfDamageCarry;

        /// <summary>역겨움 — 다음 묶음 처리까지 모아둔 <b>시간</b>(초).</summary>
        float _allyDamageCarry;

        /// <summary>
        /// 역겨움 — <b>아군별로</b> 1 미만의 소수 피해를 모아두는 나머지.
        ///
        /// ⚠⚠ <b>이 칸이 없으면 역겨움이 아무 피해도 주지 않는다.</b> 초당 0.5% · 묶음 0.25초 ·
        /// 캐릭터 최대 체력 80~200 이면 한 묶음의 피해가 <c>0.1~0.25</c> 라, 예전처럼 매 묶음마다
        /// <c>RoundToInt</c> 하면 <b>언제나 0</b> 이 되어 40초 내내 체력이 1 도 안 깎였다 —
        /// 로스터에 「역겨움」이 뜨고 로그도 남는데 실제로는 아무 일도 안 일어난다.
        /// <see cref="ApplySelfDamage"/> 가 피학에 쓰는 것과 <b>같은 방법</b>(내림 + 나머지 보관)이고,
        /// 대상이 여러 명이라 나머지를 <b>대상마다</b> 따로 들고 있어야 한다
        /// (최대 체력이 아군마다 다르므로 하나로 묶으면 누구의 나머지인지 알 수 없다).
        /// </summary>
        readonly System.Collections.Generic.Dictionary<int, float> _allyDamageCarryById =
            new System.Collections.Generic.Dictionary<int, float>();

        /// <summary>혼란 — 다음에 아군 타겟을 다시 고를 시각.</summary>
        float _nextConfusionRetarget;

        /// <summary>각성으로 올려둔 능력치 %. 해제할 때 정확히 이만큼 되돌린다.</summary>
        int _appliedStatPercent;

        /// <summary>혼란이 아군을 다시 고르는 간격(초). 죽은 아군을 계속 때리지 않게 한다.</summary>
        const float ConfusionRetargetInterval = 0.75f;

        /// <summary>혼란·광분이 대상을 찾는 최대 거리(타일).</summary>
        const float ConfusionSearchRange = 12f;
        const float MadnessSearchRange = 200f;

        /// <summary>광분 중 목줄 — 전술 지침을 무시하고 전방까지 쫓아가야 하므로 넉넉히 준다(타일).</summary>
        const float MadnessLeash = 200f;

        /// <summary>광분 중 목표를 다시 고르는 간격(초).</summary>
        const float MadnessRetargetInterval = 1f;
        float _nextMadnessRetarget;

        // ------------------------------------------------------------------
        // 조회 (UI 가 읽는다)
        // ------------------------------------------------------------------

        /// <summary>현재 침식 수치(0 ~ 상한).</summary>
        public float Erosion => erosion;

        /// <summary>침식 게이지 비율(0~1).</summary>
        public float ErosionRatio
        {
            get
            {
                int max = ErosionService.Instance != null ? ErosionService.Instance.ErosionMax : 100;
                return max > 0 ? Mathf.Clamp01(erosion / max) : 0f;
            }
        }

        /// <summary>지금 발동 중인 정신 이상. 없으면 null.</summary>
        public MentalErrorDefinitionSO Active => _active;

        public bool HasActive => _active != null;

        public MentalErrorType ActiveType => _active != null ? _active.type : MentalErrorType.None;

        /// <summary>로스터·전술 창에 그대로 쓰는 표시 이름("혼란" 등). 없으면 빈 문자열.</summary>
        public string ActiveName => _active != null ? _active.DisplayName : string.Empty;

        /// <summary>
        /// 이 캐릭터의 침식 컴포넌트를 얻는다.
        ///
        /// <b>정상 경로는 템플릿 복제다</b> — 이 컴포넌트는 씬의 <c>Character_Template</c> 에
        /// MCP 로 직접 붙여두었고(유저 확정: "모든 객체 생성은 템플릿 복제를 제외하곤 MCP 로
        /// 직접 생성"), 캐릭터는 그 템플릿을 <c>Instantiate</c> 해서 만들어지므로 자동으로 물려받는다
        /// (진행상황 5절의 템플릿 복제 패턴).
        ///
        /// 여기서 <c>AddComponent</c> 까지 하는 것은 <b>안전망</b>이다 — 템플릿에서 이 컴포넌트가
        /// 빠지면(브랜치 재동기화로 씬이 되돌아가는 사고가 이 프로젝트에서 실제로 두 번 있었다,
        /// 진행상황 28-3·28-4절) 침식 시스템이 조용히 죽어버린다. 없으면 붙여서 계속 동작하게 한다.
        /// </summary>
        public static CharacterErosion EnsureOn(CharacterUnit unit)
        {
            if (unit == null) return null;
            if (unit.TryGetComponent(out CharacterErosion existing)) return existing;
            return unit.gameObject.AddComponent<CharacterErosion>();
        }

        /// <summary>붙어 있으면 돌려주고, 없으면 null — 표시용 조회에 쓴다(붙이지 않는다).</summary>
        public static CharacterErosion Of(CharacterUnit unit) =>
            unit != null && unit.TryGetComponent(out CharacterErosion e) ? e : null;

        // ------------------------------------------------------------------

        void Awake() => EnsureReady();

        /// <summary>
        /// 참조를 준비한다. <b>Awake 와 <see cref="Tick"/> 양쪽에서 부른다</b>(여러 번 불려도 안전) —
        /// 어느 콜백이 먼저 도는지 추론하지 않고 쓰기 직전에 확인하는 것이 이 프로젝트에서
        /// 반복 확인된 초기화 함정의 유일한 확실한 대책이다(진행상황 28-4절, <c>CharacterBehavior.EnsureReady</c>).
        /// </summary>
        void EnsureReady()
        {
            if (_self != null) return;

            _character = GetComponent<CharacterUnit>();
            _self = GetComponent<DamageableUnit>();
            _combat = GetComponent<UnitCombat>();
            _behavior = GetComponent<CharacterBehavior>();
        }

        void OnDisable()
        {
            // 캐릭터가 죽으면 오브젝트가 파괴된다 — 그 전에 지속 효과를 되돌려 둔다.
            // (오브젝트가 사라지면 어차피 무의미하지만, 아군에게 걸어둔 효과는 없고
            //  전부 자기 자신에게 건 것이라 여기서 정리하면 상태가 항상 일관된다.)
            ClearActive();
        }

        // ------------------------------------------------------------------
        // 진행 — ErosionService 가 매 프레임 부른다
        // ------------------------------------------------------------------

        public void Tick(ErosionService service, float dt, bool waveMonstersAlive = false)
        {
            EnsureReady();
            if (service == null || _self == null || !_self.IsAlive) return;

            // ★ 소환수는 침식하지 않는다 — 부르는 쪽(<see cref="ErosionService"/>)이 이미
            //   걸러내지만, 다른 경로가 생겼을 때도 규칙이 지켜지도록 여기서도 막는다.
            //   («일어나지 않는다» 는 정의문이므로 한 곳만 지키는 것으로 두지 않는다.)
            if (_character != null && _character.IsSummoned) return;

            TickActiveState(service, dt);

            if (!service.EnableErosion) return;

            // 저항력이 상승·회복 속도를 모두 바꾼다 (캐릭터 가이드 p10).
            // 기준점(기본 50)에서 배율 1.0 이고, 그보다 낮으면 빨리 쌓이고 늦게 빠진다 —
            // 두 배율은 정확히 대칭이다. 정의가 없는 캐릭터는 둘 다 1.0 이라 예전과 같이 동작한다.
            // 침식 수치 자체는 실수로 누적한다(정수로 깎으면 초당 0.25 회복이 0 이 된다).
            float gain = _character != null ? _character.ErosionGainMultiplier : 1f;

            // ★★ 유물 「서늘한 해열」(relic_erosion_slow) — <b>쌓이는 쪽만</b> 늦춘다
            //   (표 EffectType 시트: *"빠지는 속도는 건드리지 않습니다"*). 그래서 recover 에는
            //   곱하지 않는다. 저항력 배율과 <b>곱해서</b> 쌓는다 — 둘은 서로 다른 이유로
            //   같은 값을 바꾸므로 어느 한쪽이 다른 쪽을 덮으면 안 된다.
            gain *= Relics.RelicEffectService.ErosionGainMultiplier(_character);
            float recover = _character != null ? _character.ErosionRecoverMultiplier : 1f;

            if (IsInWaveCombat(service))
            {
                _outOfCombatTime = 0f;
                erosion += service.ErosionPerSecondInCombat * dt * gain;
            }
            else if (waveMonstersAlive && service.RearErosionPercent > 0)
            {
                // ★★ <b>후방 침식</b> (2026-09-01 · 유저 지시: *"후방에 있는 아군도 어느 정도는
                //   침식이 되는 로직을 만들거나"*). 조건과 근거는 <see cref="ErosionService"/> 의
                //   <c>rearErosionPercent</c> 위 주석에 있다.
                //
                //   ⚠ <b>회복 대기(_outOfCombatTime)를 여기서도 0 으로 되돌린다.</b> 안 그러면
                //     쌓으면서 동시에 «대기시간이 흐르다가» 웨이브가 끝나는 순간 곧바로
                //     회복이 시작돼, 후방은 쌓자마자 되돌려받는다 — 이 칸을 만든 뜻이 사라진다.
                //   ★ 저항력 배율(gain)은 <b>전투 침식과 똑같이</b> 먹인다. 후방이라고 저항이
                //     다르게 작동하면 «저항력» 이라는 능력치의 뜻이 두 벌이 된다.
                _outOfCombatTime = 0f;
                erosion += service.RearErosionPerSecond * dt * gain;
            }
            else
            {
                _outOfCombatTime += dt;
                if (_outOfCombatTime >= service.RecoverDelaySeconds)
                    erosion -= service.ErosionRecoverPerSecond * dt * recover;
            }

            erosion = Mathf.Clamp(erosion, 0f, service.ErosionMax);

            if (erosion >= service.ErosionMax && !(service.BlockWhileActive && HasActive))
                Trigger(service);
        }

        /// <summary>
        /// 웨이브 몬스터와 전투 중인지. <b>중립 몬스터는 제외</b>한다(유저 확정: 침식은
        /// "웨이브 몬스터와 전투 시" 쌓인다) — 중립 몬스터도 <see cref="UnitKind.Monster"/> 를
        /// 공유하므로 <see cref="Faction.Cancer"/> 까지 확인해야 갈린다. <c>WaveManager</c> 가
        /// 전투 개시 판정에서 같은 실수를 했다가 고친 적이 있다(진행상황 24-6절 3번).
        ///
        /// 내가 노리는 중이거나(공격) 최근에 웨이브 몬스터에게 맞았으면(피격) 둘 다 전투로 본다 —
        /// 원거리 캐릭터처럼 일방적으로 때리는 쪽도, 맞기만 하는 쪽도 침식이 쌓여야 한다.
        /// </summary>
        bool IsInWaveCombat(ErosionService service)
        {
            if (_combat != null)
            {
                DamageableUnit target = _combat.Target;
                if (target != null && target.IsAlive && target.Faction == Faction.Cancer) return true;
            }

            DamageableUnit attacker = _self.LastAttacker;   // 죽은 상대는 자동으로 비워진다
            return attacker != null && attacker.Faction == Faction.Cancer &&
                   Time.time - _self.LastAttackedTime <= service.WaveCombatMemorySeconds;
        }

        // ------------------------------------------------------------------
        // 발동 / 해제
        // ------------------------------------------------------------------

        void Trigger(ErosionService service)
        {
            // 패시브 보정을 반영한 추첨 — '강철의 의지'(좋은 효과 가중치 ×N) ·
            // '광란'(이기심·광분으로 고정)이 여기서 작동한다.
            MentalErrorDefinitionSO def = service.RollDefinition(_character);
            if (def == null)
            {
                // 정의가 없으면 상한에서 매 프레임 재시도하게 되므로 조금 떨어뜨려 둔다.
                erosion = service.ErosionMax * 0.75f;
                return;
            }

            ClearActive();          // 중첩 방지 — 이전 상태의 지속 효과를 먼저 되돌린다
            _active = def;

            // 즉발(지속시간 0)도 로스터·로그에서 잠깐 보이게 표시 시간을 준다. 효과 자체는
            // ApplyOnce 에서 이미 한 번에 끝나 있고, 이 시간은 순수 표시용이다.
            float display = def.IsInstant ? service.InstantStateDisplaySeconds : def.durationSeconds;
            _activeEndTime = Time.time + Mathf.Max(0.01f, display);

            _selfDamageCarry = 0f;
            _allyDamageCarry = 0f;
            _allyDamageCarryById.Clear();
            _nextConfusionRetarget = 0f;
            _nextMadnessRetarget = 0f;

            ApplyOnce(def);
            ApplyLasting(def);

            erosion = Mathf.Clamp(def.afterErosion, 0f, service.ErosionMax);
            _outOfCombatTime = 0f;

            service.ReportTriggered(_character, def);
        }

        /// <summary>발동 순간 한 번만 적용되는 효과 (즉발 종류 + 지속 종류의 초기 1회분).</summary>
        void ApplyOnce(MentalErrorDefinitionSO def)
        {
            switch (def.type)
            {
                case MentalErrorType.SettleDown:
                    // 주변 아군의 침식을 낮춘다(본인 제외).
                    AddErosionToAlliesInRadius(def.value01, -def.value02);
                    break;

                case MentalErrorType.Depression:
                    // 주변 아군의 침식을 올린다(본인 제외) — 연쇄 발동을 노린 효과다.
                    AddErosionToAlliesInRadius(def.value01, +def.value02);
                    break;

                case MentalErrorType.Upsurge:
                    ApplyUpsurge(def);
                    break;

                case MentalErrorType.SelfHarm:
                    // 최대 체력의 value01 % 를 즉시 잃는다.
                    ApplySelfDamage(_self.MaxHp * def.value01 / 100f, immediate: true);
                    break;
            }
        }

        /// <summary>지속시간 동안 유지되는 효과를 켠다. <see cref="ClearActive"/> 가 정확히 되돌린다.</summary>
        void ApplyLasting(MentalErrorDefinitionSO def)
        {
            switch (def.type)
            {
                case MentalErrorType.Confusion:
                    // 아군을 공격한다 — 진영 판정을 건너뛰는 기존 훅(SetHuntTarget)을 그대로 쓴다.
                    // 공격 유형은 근거리로 강제한다(유저 확정) — 치유형이면 아군을 오히려
                    // 회복시켜 버리고, 마법형이면 범위 수집이 적 진영만 보므로 아무 일도
                    // 일어나지 않는다. 자세한 이유는 UnitCombat.SetForcedAttackType 참조.
                    _combat?.SetForcedAttackType(TacticalAttackType.Melee);
                    _behavior?.SetMentalOverride(MentalOverride.AttackAllies);
                    break;

                case MentalErrorType.Arousal:
                    _appliedStatPercent = Mathf.RoundToInt(def.value01);
                    _character?.AddStatPercentBonus(_appliedStatPercent);
                    break;

                case MentalErrorType.Terrified:
                    // 전투를 거부하고 성역 방향으로 회피 — 기존 후퇴 로직을 그대로 재사용한다.
                    _behavior?.SetMentalOverride(MentalOverride.Flee);
                    break;

                case MentalErrorType.Madness:
                    _behavior?.SetMentalOverride(MentalOverride.Charge);
                    break;

                case MentalErrorType.Selfishness:
                    _character?.SetExternalHealBlocked(true);
                    break;
            }
        }

        /// <summary>지속시간이 남아 있는 동안 매 프레임 도는 효과 + 만료 처리.</summary>
        void TickActiveState(ErosionService service, float dt)
        {
            if (_active == null) return;

            if (Time.time >= _activeEndTime) { ClearActive(); return; }

            // 즉발 종류는 표시만 남아 있는 상태라 진행할 효과가 없다.
            if (_active.IsInstant) return;

            switch (_active.type)
            {
                case MentalErrorType.Confusion:
                    TickConfusion();
                    break;

                case MentalErrorType.Madness:
                    TickMadness();
                    break;

                case MentalErrorType.Masochism:
                    // 초당 최대 체력의 value01 %.
                    ApplySelfDamage(_self.MaxHp * _active.value01 / 100f * dt, immediate: false);
                    break;

                case MentalErrorType.Disgusting:
                    TickDisgusting(dt);
                    break;
            }
        }

        /// <summary>
        /// <b>밖에서</b> 정신 이상을 해제한다 — 패시브 '정신 안정'(피올로)이 쓴다.
        /// 내부 <see cref="ClearActive"/> 와 같은 경로를 지나므로 지속 효과가 정확히 되돌아간다
        /// (해제 로직을 두 벌로 만들면 하나를 고칠 때 다른 하나가 남는다).
        /// </summary>
        public void ClearActiveExternally() => ClearActive();

        /// <summary>지속 효과를 전부 되돌리고 상태를 비운다. 만료·사망·중첩 모두 이 한 곳을 지난다.</summary>
        void ClearActive()
        {
            if (_active == null) return;

            switch (_active.type)
            {
                case MentalErrorType.Confusion:
                    // ⚠ 잠금(SetForcedHuntTarget)을 푸는 쪽을 부른다 — 그냥 ClearHuntTarget 을
                    //   부르면 잠금 때문에 아무 일도 일어나지 않아 아군 공격이 영영 안 풀린다.
                    _combat?.ClearForcedHuntTarget();
                    _combat?.ClearForcedAttackType();   // 전술 지침의 원래 공격 유형으로 복귀
                    _behavior?.SetMentalOverride(MentalOverride.None);
                    break;

                case MentalErrorType.Arousal:
                    if (_appliedStatPercent != 0)
                    {
                        _character?.AddStatPercentBonus(-_appliedStatPercent);
                        _appliedStatPercent = 0;
                    }
                    break;

                case MentalErrorType.Terrified:
                case MentalErrorType.Madness:
                    _behavior?.SetMentalOverride(MentalOverride.None);
                    break;

                case MentalErrorType.Selfishness:
                    _character?.SetExternalHealBlocked(false);
                    break;
            }

            _allyDamageCarryById.Clear();   // 다음 발동에 남은 나머지가 새지 않게 한다
            _active = null;
        }

        // ------------------------------------------------------------------
        // 개별 효과
        // ------------------------------------------------------------------

        /// <summary>혼란 — 주변 아군 중 가장 가까운 하나를 강제 타겟으로 잡는다.</summary>
        void TickConfusion()
        {
            if (_combat == null) return;
            if (Time.time < _nextConfusionRetarget && _combat.IsHunting) return;
            _nextConfusionRetarget = Time.time + ConfusionRetargetInterval;

            DamageableUnit ally = UnitRegistry.FindNearestAlly(
                transform.position, _self.Faction, ConfusionSearchRange, _self);

            // 목줄 밖이면 SetHuntTarget 이 다음 프레임에 스스로 놓아버리므로, 목줄을 지금 위치로
            // 옮겨 붙잡아 둔다 — 혼란은 "제자리에서 옆의 아군을 친다"가 자연스럽다.
            if (ally != null)
            {
                _combat.SetHome(transform.position, ConfusionSearchRange);

                // ★ 잠그는 쪽으로 넣는다 — 전술 지침을 바꿔도 이 타겟이 지워지지 않게
                //   (유저 지시 2026-08-17). 자세한 이유는 UnitCombat.SetForcedHuntTarget 참조.
                _combat.SetForcedHuntTarget(ally);
            }
        }

        /// <summary>광분 — 전술 지침을 무시하고 가장 가까운 웨이브 몬스터를 향해 나아간다.</summary>
        void TickMadness()
        {
            if (_combat == null) return;
            if (Time.time < _nextMadnessRetarget) return;
            _nextMadnessRetarget = Time.time + MadnessRetargetInterval;

            DamageableUnit enemy = UnitRegistry.FindTarget(
                transform.position, _self.Faction, MadnessSearchRange,
                System.Array.Empty<UnitKind>(), null);
            if (enemy == null) return;

            // 목적지를 적 쪽으로 옮기고 목줄을 크게 준다 — UnitCombat 은 타겟이 없으면 귀환
            // 지점으로 걸어가므로, 이것만으로 "전방으로 달려나가 적을 쫓는다"가 성립한다
            // (CharacterBehavior 가 목적지를 옮겨 이동을 표현하는 것과 같은 방식, 진행상황 12절).
            _combat.SetHome(enemy.transform.position, MadnessLeash);
        }

        /// <summary>역겨움 — 주변 아군이 초당 자기 최대 체력의 value02 % 를 잃는다.</summary>
        void TickDisgusting(float dt)
        {
            // 대상마다 최대 체력이 달라 각자 비율로 계산해야 한다. 소수 누적은 하나의
            // carry 로 묶어 관리하되(피해가 1 미만이면 모아서 넣는다), 실제 피해는 각 아군의
            // 최대 체력 기준으로 나눠 준다.
            _allyDamageCarry += dt;
            const float TickInterval = 0.25f;   // 4회/초 — 매 프레임 정수 피해를 넣으면 반올림 손실이 크다
            if (_allyDamageCarry < TickInterval) return;

            float elapsed = _allyDamageCarry;
            _allyDamageCarry = 0f;

            UnitRegistry.CollectAlliesInRadius(transform.position, _active.value01,
                                              _self.Faction, _self, _allyScratch);

            for (int i = 0; i < _allyScratch.Count; i++)
            {
                DamageableUnit ally = _allyScratch[i];
                if (ally == null || !ally.IsAlive) continue;

                // ⚠ <b>반올림하지 않는다</b> — 한 묶음의 피해가 1 미만이라 반올림하면 언제나 0 이다
                //   (_allyDamageCarryById 주석 참조). 내림하고 나머지는 <b>그 아군 몫으로</b> 남긴다.
                int id = ally.GetInstanceID();
                _allyDamageCarryById.TryGetValue(id, out float carry);
                carry += ally.MaxHp * _active.value02 / 100f * elapsed;

                int whole = Mathf.FloorToInt(carry);
                _allyDamageCarryById[id] = carry - whole;
                if (whole > 0) ally.ApplyDamage(whole);
            }
        }

        /// <summary>자해·피학의 자기 피해. 초당 소수 피해는 1 이 될 때까지 모아서 넣는다.</summary>
        void ApplySelfDamage(float amount, bool immediate)
        {
            if (amount <= 0f) return;

            if (immediate)
            {
                int once = Mathf.RoundToInt(amount);
                if (once > 0) _self.ApplyDamage(once);
                return;
            }

            _selfDamageCarry += amount;
            int whole = Mathf.FloorToInt(_selfDamageCarry);
            if (whole <= 0) return;

            _selfDamageCarry -= whole;
            _self.ApplyDamage(whole);
        }

        /// <summary>고조 — 자원을 쓰지 않고 강화한다. 강화 횟수(=다음 비용)는 정상적으로 오른다.</summary>
        void ApplyUpsurge(MentalErrorDefinitionSO def)
        {
            int times = Mathf.Max(1, Mathf.RoundToInt(def.value01));
            CharacterUpgradeService.Instance?.GrowFree(_character, times);
        }

        /// <summary>진정·우울 — 반경 안 아군의 침식을 올리거나 내린다(본인 제외).</summary>
        void AddErosionToAlliesInRadius(float radiusTiles, float delta)
        {
            if (Mathf.Approximately(delta, 0f)) return;

            UnitRegistry.CollectAlliesInRadius(transform.position, radiusTiles,
                                              _self.Faction, _self, _allyScratch);

            for (int i = 0; i < _allyScratch.Count; i++)
            {
                var ally = _allyScratch[i] as CharacterUnit;
                if (ally == null || !ally.IsAlive) continue;

                CharacterErosion other = EnsureOn(ally);
                other?.AddErosion(delta);
            }
        }

        /// <summary>
        /// 저장에서 침식 수치를 되돌린다 (98절).
        ///
        /// ⚠ <b>정신 이상은 같이 되돌리지 않는다</b> — 지속 시간·해제 조건이 얽혀 있어
        /// 걸린 상태만 복원하면 <b>영영 안 풀리는 정신 이상</b>이 남는다. 침식 수치가 그대로라
        /// 불러온 직후 조건이 차면 <see cref="Tick"/> 이 정상 경로로 다시 걸어준다.
        /// </summary>
        public void RestoreErosion(float value)
        {
            int max = ErosionService.Instance != null ? ErosionService.Instance.ErosionMax : 100;
            erosion = Mathf.Clamp(value, 0f, max);
        }

        /// <summary>침식을 직접 올리거나 내린다 (진정·우울이 서로에게 쓴다).</summary>
        public void AddErosion(float delta)
        {
            int max = ErosionService.Instance != null ? ErosionService.Instance.ErosionMax : 100;
            erosion = Mathf.Clamp(erosion + delta, 0f, max);
        }

        /// <summary>아군 조회 임시 버퍼. 프레임마다 새 리스트를 만들지 않으려고 정적으로 둔다
        /// (<c>UnitCombat._splashScratch</c> 와 같은 이유).</summary>
        static readonly System.Collections.Generic.List<DamageableUnit> _allyScratch =
            new System.Collections.Generic.List<DamageableUnit>(16);
    }
}
