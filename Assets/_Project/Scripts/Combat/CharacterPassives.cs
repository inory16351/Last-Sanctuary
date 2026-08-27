using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Units;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 캐릭터 한 명의 <b>패시브 스킬 실제 효과</b>. 캐릭터 테이블 <c>Skill</c> 시트 12종을 전부 다룬다.
    ///
    /// <b>왜 이 컴포넌트가 새로 필요했나</b> — 33절이 패시브를 데이터로 들여왔지만
    /// <c>PassiveSkillSO</c> 주석이 스스로 적어둔 대로 <b>"표시용 데이터"</b>였다: 성장 창에
    /// 아이콘·이름·설명을 보여주는 것까지만 하고 <b>전투에는 아무 영향이 없었다</b>
    /// (미결 34번, 유저 리포트: "캐릭터들 스킬이 실제로는 전혀 적용되고 있지 않다").
    /// 이 컴포넌트가 그 마지막 칸을 채운다.
    ///
    /// <b>구조 — 상태는 유닛, 진행은 서비스</b>(<see cref="PassiveSkillService"/>).
    /// <see cref="CharacterErosion"/> 과 완전히 같은 방식이고 이유도 같다(29-2절):
    /// 캐릭터는 프레임 중간에 <c>Instantiate</c> 되므로 <c>Update</c> 를 이 컴포넌트가 직접 갖고
    /// 있으면 <c>Start</c> 보다 먼저 도는 순서 사고가 난다 — 이 프로젝트에서 이미 세 번 겪었다
    /// (24-6 · 27-9 · 28-4절). 진행 주체를 서비스로 올리면 그 문제가 아예 생기지 않는다.
    ///
    /// <b>해금과 연동된다</b> — 슬롯 0 은 생성 즉시, 슬롯 1·2 는 강화 횟수
    /// (<see cref="PassiveUnlockConfig"/>)로 열린다. 잠긴 스킬은 효과도 없다.
    /// 강화로 새로 열리면 <see cref="Refresh"/> 가 그 순간부터 적용한다.
    /// </summary>
    [RequireComponent(typeof(CharacterUnit))]
    public partial class CharacterPassives : MonoBehaviour
    {
        // ── 상시 효과가 "지금 걸어둔" 양. 해제할 때 정확히 같은 값을 빼려면 기억해야 한다 ──
        int _appliedDefenseAura;      // 로 아이아스가 <b>남에게</b> 걸어둔 것은 아래 _auraTargets
        int _appliedRampageAttack;    // 광란 — 공격력 고정 보정
        int _appliedRangeBonus;       // 타고난 섬세함 — 사거리 보너스

        bool _visionZeroed;           // 타고난 섬세함 — 시야를 최소치로 낮췄는지 (ApplyBlindVision)
        bool _rampageOn;

        // 희열 — 중첩 수와 만료 시각. 중첩마다 지속시간이 0 으로 초기화된다(정의문).
        int _ecstasyStacks;
        float _ecstasyEndTime;

        // 쿨타임 (희생 · 정신 안정 · 정화의 손길)
        float _sacrificeReadyAt;
        float _calmDownReadyAt;
        float _purifyReadyAt;

        /// <summary>
        /// <b>시도 순서</b> — 이 캐릭터가 가진 쿨타임 스킬(희생·정신 안정·정화의 손길) 중
        /// 해금된 것만, <see cref="PassiveSkillSO.coolTime"/> <b>내림차순</b>으로 담아둔다
        /// (유저 지시 2026-08-13: "쿨이 동시에 돌면 쿨타임이 더 긴 스킬부터 쓰도록" —
        /// <see cref="BossSkillCaster"/> 와 완전히 같은 규칙을 캐릭터에도 적용한다).
        ///
        /// 한 캐릭터가 이 셋 중 <b>둘 다</b> 가진 경우는 지금 피올로(정신 안정 180초 ·
        /// 정화의 손길 120초)뿐이지만, 다른 캐릭터가 나중에 겹치더라도 그대로 동작한다.
        ///
        /// <see cref="Refresh"/> 가 해금 목록이 바뀔 때만 다시 계산한다 — 매 프레임 정렬하지 않는다.
        /// </summary>
        readonly List<PassiveSkillType> _cooldownPriority = new List<PassiveSkillType>(3);

        // 정화의 손길 — 발동 중이면 만료 시각
        float _purifyEndTime;

        // 로 아이아스 — 지금 방어력을 걸어준 동료들 (빠져나가면 정확히 되돌린다)
        readonly Dictionary<DamageableUnit, int> _auraTargets = new Dictionary<DamageableUnit, int>();

        // 타오르는 날개 — 초당 피해를 프레임 단위로 쪼갠 누적분
        float _blazeCarry;

        // ── 히스톤 9005 ────────────────────────────────────────────────────
        // 선봉장 — 근거리 크리티컬 예외를 지금 걸어두었는지(0/1). 되돌릴 때 정확히 같은 값을 뺀다.
        int _appliedMeleeCritGrant;

        // 분노 — 0~100. <b>실수로 들고 있는다</b>: 초당 하락값이 0.5 라 정수로 깎으면
        // 매 프레임 0 이 되어 영영 안 줄어든다(미결 182번이 지적한 그 소수값이다).
        float _rage;

        // 분노 — 부활 쿨타임이 끝나는 시각 / 경직이 끝나 되살아나는 시각
        float _reviveReadyAt;
        float _reviveAt;
        bool _reviving;

        CharacterUnit _unit;
        UnitCombat _combat;
        CharacterTactics _tactics;
        CharacterAnimator _animator;

        // 해금된 스킬만 담는다. Refresh 가 다시 만든다.
        readonly List<(PassiveSkillType type, PassiveSkillSO so)> _active =
            new List<(PassiveSkillType, PassiveSkillSO)>();

        int _lastUpgradeCount = -1;

        public CharacterUnit Unit => _unit;

        /// <summary>
        /// 이 캐릭터에게 컴포넌트를 보장한다. <c>Character_Template</c> 에 MCP 로 붙여 두지만
        /// (그래야 복제되는 전원이 물려받는다), 템플릿에서 빠지는 사고가 실제로 두 번 있었으므로
        /// (28-3·28-4절 브랜치 재동기화) 코드 안전망을 남긴다 — <see cref="CharacterErosion.EnsureOn"/>
        /// 과 같은 이유·같은 모양이다.
        /// </summary>
        public static CharacterPassives EnsureOn(CharacterUnit unit)
        {
            if (unit == null) return null;
            var p = unit.GetComponent<CharacterPassives>();
            return p != null ? p : unit.gameObject.AddComponent<CharacterPassives>();
        }

        void Awake()
        {
            _unit = GetComponent<CharacterUnit>();
            _combat = GetComponent<UnitCombat>();
            _tactics = GetComponent<CharacterTactics>();
            _animator = GetComponent<CharacterAnimator>();
        }

        void OnDisable()
        {
            // 죽거나 사라질 때 <b>남에게 걸어둔 보정을 반드시 되돌린다</b> —
            // 안 그러면 비기오르가 죽은 뒤에도 동료 방어력이 영구히 +8 로 남는다.
            ClearDefenseAura();
            ClearNewcomerEffects();     // 신규 3인 (CharacterPassives.Newcomers.cs)
            ClearTrioEffects();         // 신규 3인 2차 (CharacterPassives.Trio.cs)
        }

        /// <summary>
        /// 해금된 스킬 목록을 다시 만든다. 강화 횟수가 바뀔 때만 실제로 일한다 —
        /// 매 프레임 정의를 다시 훑으면 낭비다.
        /// </summary>
        public void Refresh()
        {
            if (_unit == null) return;
            if (_unit.UpgradeCount == _lastUpgradeCount) return;
            _lastUpgradeCount = _unit.UpgradeCount;

            _active.Clear();
            CharacterDefinitionSO def = _unit.Definition;
            if (def == null) return;

            for (int slot = 0; slot < 3; slot++)
            {
                if (!_unit.IsPassiveUnlocked(slot)) continue;
                PassiveSkillSO so = def.PassiveAt(slot);
                if (so == null || !so.IsUsable) continue;

                PassiveSkillType type = PassiveSkillTypes.Parse(so.skillType);
                if (type == PassiveSkillType.None) continue;
                _active.Add((type, so));
            }

            ApplyAlwaysOn();
            RebuildCooldownPriority();
        }

        /// <summary>
        /// 쿨타임 스킬(희생·정신 안정·정화의 손길) 중 이 캐릭터가 가진 것만 골라
        /// 쿨타임 내림차순으로 정렬해둔다. <see cref="TickCooldownSkills"/> 가 이 순서로 시도한다.
        /// </summary>
        void RebuildCooldownPriority()
        {
            _cooldownPriority.Clear();
            foreach (PassiveSkillType type in CooldownSkillTypes)
                if (Has(type)) _cooldownPriority.Add(type);

            _cooldownPriority.Sort((a, b) =>
                Find(b).coolTime.CompareTo(Find(a).coolTime));
        }

        /// <summary>쿨타임으로 게이트되는 스킬 종류 전부. 새 쿨타임 스킬을 추가하면 이 배열에도 넣을 것.</summary>
        static readonly PassiveSkillType[] CooldownSkillTypes =
        {
            PassiveSkillType.Sacrifice, PassiveSkillType.CalmDown, PassiveSkillType.PurifyingTouch,

            // ── 신규 캐릭터 3인 (2026-08-20) — 구현은 CharacterPassives.Newcomers.cs ──
            PassiveSkillType.ArrowRain, PassiveSkillType.Dawn,
            PassiveSkillType.FallenBody, PassiveSkillType.CelestialShield,
            PassiveSkillType.DivineWrath,

            // ★ 「도움의 손길」은 <b>표가 30초를 준 뒤에</b> 여기 들어왔다 (2026-08-21) —
            //   그전에는 쿨타임이 0 이라 매 프레임 갈래(TickNewcomers)에 있었다.
            //   상세는 `CharacterPassives.Newcomers` 의 `_helpingHandReadyAt` 주석.
            PassiveSkillType.AHelpingHand,

            // ── 아르세니아 9010 · 불칸 9011 (2026-08-20) ──
            PassiveSkillType.SacredBlessing, PassiveSkillType.UnfinishedNobility,
            PassiveSkillType.FlameBlast,

            // ── 신규 3인 2차 (2026-08-21) — 구현은 CharacterPassives.Trio.cs ──
            //   ⚠ 여기 넣지 않으면 <b>쿨타임 표가 부르지 않아 영영 발동하지 않는다</b>
            //     (위 주석의 «새 쿨타임 스킬을 추가하면 이 배열에도 넣을 것» 이 그 뜻이다).
            //   ★ 「군단의 방패」·「명사수」·「영혼 흡수」·「사신의 낫」·「한계 돌파」는 <b>없다</b> —
            //     쿨타임이 아니라 상시·이벤트로 도는 것들이다(Trio.cs 의 ★★ 참조).
            PassiveSkillType.StrongMind, PassiveSkillType.BlessingOfFourWings,
            PassiveSkillType.EvasiveManeuver, PassiveSkillType.DeclarationOfTheEnd,
        };

        /// <summary>이 캐릭터가 그 패시브를 지금 쓸 수 있는지 (해금 + 표에 있는 종류).</summary>
        public bool Has(PassiveSkillType type) => Find(type) != null;

        /// <summary>그 패시브의 데이터. 없으면 null.</summary>
        public PassiveSkillSO Find(PassiveSkillType type)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].type == type) return _active[i].so;
            return null;
        }

        // ------------------------------------------------------------------
        // 상시 효과 — 목록이 바뀔 때 한 번만 걸고 끝낸다
        // ------------------------------------------------------------------

        /// <summary>
        /// 켜두면 계속 유지되는 효과. <b>다시 불려도 안전하게</b> 이전 값을 먼저 되돌린 뒤 새로 건다
        /// (강화로 스킬이 늘어나면 이 함수가 다시 불린다).
        /// </summary>
        void ApplyAlwaysOn()
        {
            // ── 타고난 섬세함: 시야 0, 사거리 +value01 ──
            PassiveSkillSO delicacy = Find(PassiveSkillType.InnateDelicacy);

            if (_appliedRangeBonus != 0)
            {
                _combat?.AddAttackRangeBonus(-_appliedRangeBonus);
                _appliedRangeBonus = 0;
            }
            if (delicacy != null)
            {
                _appliedRangeBonus = Mathf.RoundToInt(delicacy.value01);
                _combat?.AddAttackRangeBonus(_appliedRangeBonus);

                if (!_visionZeroed) _visionZeroed = ApplyBlindVision();
            }

            // ── 선봉장: 포지션 전방 · 공격 유형 근거리로 고정 + 근거리 크리티컬 예외 ──
            //
            // 두 효과 모두 <b>상시</b>다. 크리티컬 예외는 이 캐릭터에 "근거리도 치명타를
            // 낼 수 있다" 는 표를 하나 세워두는 것이고, 실제 판정은
            // <see cref="CharacterUnit.CriticalChancePercent"/> 가 한다 —
            // 명중률·크리티컬이 원래 <b>원거리 전용</b>이라(유저 확정 2026-08-15)
            // 이 예외가 없으면 히스톤은 영원히 치명타가 안 난다. 정의문이
            // <b>"예외적으로"</b> 라고 적은 것이 바로 그 규칙을 전제한 문장이다.
            int wantCritGrant = Find(PassiveSkillType.Vanguard) != null ? 1 : 0;
            if (wantCritGrant != _appliedMeleeCritGrant)
            {
                _unit.AddMeleeCriticalGrant(wantCritGrant - _appliedMeleeCritGrant);
                _appliedMeleeCritGrant = wantCritGrant;
            }
            _tactics?.SetRoleLock(wantCritGrant != 0);

            // ── 가학증: 후퇴 기준을 value05% 로 고정한다 (2026-08-20) ──
            //    정의문 마지막 문장이 그렇게 적혀 있다. 근거는
            //    <see cref="CharacterTactics.RetreatHpLocked"/> 위의 긴 주석 —
            //    「통제할 수 없는 쾌락」의 무적이 켜지기 전에 물러나 버리면 안 되기 때문이다.
            //
            // ⚠ 이 함수는 <b>해금 목록이 바뀔 때마다</b> 다시 돈다(ApplyAlwaysOn). 그래서
            //   강화로 슬롯이 늦게 열려도 그 순간부터 잠기고, 스킬이 없는 캐릭터에서는
            //   잠금을 <b>푼다</b> — 걸어둔 채 두면 다른 캐릭터가 이 컴포넌트를 물려받는
            //   구조는 아니지만, 「걸었으면 반드시 되돌린다」 는 이 파일의 규칙이다.
            //
            // ★★ 2026-08-20 — <b>2번 슬롯이 해금된 뒤부터</b> 잠근다 (유저 리포트:
            //    *"시그리드 두번째 스킬 열리기도 전에 5% 로 후퇴기준 고정되는 버그도 있음
            //    2번째 스킬이 해금되었을때부터 잠겨야돼"*).
            //
            //    <b>왜 버그였나</b> — 후퇴 기준을 잠그는 값(`value_05`)은 <b>1번 스킬</b>
            //    「가학증」에 적혀 있고 1번은 <b>생성 즉시</b> 열린다. 그래서 시그리드는
            //    태어나자마자 «체력 5% 까지 안 물러나는» 캐릭터가 됐다 — 그런데 그 무모함을
            //    받쳐 주는 것은 3번 「통제할 수 없는 쾌락」(저체력 무적)이고, 그 앞에
            //    2번 「고통의 기쁨」(공속 증가)이 먼저 열린다. <b>받쳐 주는 것이 하나도
            //    없는 구간</b>에서 후퇴만 막히니 그냥 죽는다.
            //
            //    <b>슬롯 번호로 판단한다</b> — 「가학증을 가졌는가」가 아니라
            //    <b>「2번 슬롯이 열렸는가」</b>를 본다. 그래야 다른 캐릭터가 같은 구조를
            //    쓰더라도 규칙이 그대로 성립한다(스킬 이름에 매달지 않는다).
            PassiveSkillSO sadism = Find(PassiveSkillType.Sadism);
            bool slot2Open = _unit != null && _unit.Definition != null &&
                             _unit.Definition.IsPassiveUnlocked(1, _unit.UpgradeCount);
            if (sadism != null && slot2Open)
                _tactics?.SetRetreatHpLock(Mathf.RoundToInt(sadism.value05));
            else
                _tactics?.ClearRetreatHpLock();

            // ── 신규 캐릭터 3인의 상시 효과 (CharacterPassives.Newcomers.cs) ──
            ApplyAlwaysOnNewcomers();
            ApplyAlwaysOnTrio();
        }

        /// <summary>
        /// ★ <b>「타고난 섬세함」의 시야 예외</b> (유저 지시 2026-08-13).
        ///
        /// 이 스킬의 정의문은 "시야 값이 0이 된다"지만, <b>0 을 그대로 넣으면 화면이 깨진다.</b>
        /// <see cref="Fog.VisionSource.SetVision"/> 이 <c>Mathf.Max(1f, …)</c> 로 잘라 시야가
        /// <b>1타일(반경 0.5)</b> 이 되고, <see cref="Fog.FogOfWarService.RevealCircle"/> 는
        /// 반경 0.5 에서 <b>정확히 한 칸</b>만 밝힌다. 그런데 캐릭터 그림은 발밑을 기준으로
        /// <b>2.6 x 2.15 타일</b>(엘린)이라 <b>몸의 대부분이 안개에 덮인다</b> —
        /// 걸어다니면 밝은 칸이 바뀌면서 <b>캐릭터가 보였다 안 보였다</b> 한다(유저 리포트).
        ///
        /// 그래서 <b>"시야 0" 을 「자기 그림만 딱 덮는 최소 시야」로 해석</b>한다.
        /// 반경은 발밑(피벗)에서 그림의 <b>위쪽 모서리</b>까지 —
        /// <c>√((가로/2)² + 세로²)</c> 다(피벗이 발밑이라 그림이 위로만 솟는다).
        /// 엘린 기준 반경 약 2.5타일 = <c>visionTiles</c> 약 5.0 이고,
        /// 캐릭터 템플릿 기본값 7 보다 여전히 작다 — <b>정찰 능력은 그대로 없다.</b>
        ///
        /// <b>왜 여기(스킬)에서 처리하나</b> — 이건 이 스킬 하나의 예외이고,
        /// <see cref="Fog.VisionSource"/> 의 최소값을 건드리면 다른 모든 유닛에 영향이 간다.
        ///
        /// <returns>실제로 적용했으면 true. 스킨이 아직 안 붙어 크기를 못 재면 false 를 돌려
        /// <b>다음 <see cref="Refresh"/> 에 다시 시도</b>하게 한다 — 캐릭터는 프레임 중간에
        /// 생성되므로 이 시점에 <see cref="CharacterAnimator"/> 가 준비 안 됐을 수 있다.</returns>
        /// </summary>
        bool ApplyBlindVision()
        {
            // ⚠️ 컴포넌트를 끄는 대신 시야를 줄인다 — 끄면 FogOfWarService 가 이 유닛을
            //    목록에서 빼는데, 되살리는 경로가 없다.
            var vision = GetComponent<Fog.VisionSource>();
            if (vision == null) return true;   // 시야원이 없으면 더 할 일이 없다

            var anim = GetComponent<CharacterAnimator>();
            Vector2 size = anim != null ? anim.RenderedSizeTiles : Vector2.zero;
            if (size.x <= 0.01f || size.y <= 0.01f) return false;   // 아직 스킨이 없다 — 다음 Refresh 에

            // ★ 그림 크기의 <b>사각형</b>으로 밝힌다 (유저 지시 2026-08-13:
            //   "시야가 딱 캐릭터의 이미지 만큼의 공간만 가져야 하는데 지금 시야가 너무 넓음").
            //
            //   처음에는 원형 반경을 √((가로/2)² + 세로²) 로 잡았는데, 원이 그림의 모서리까지
            //   닿아야 하므로 <b>밝히는 넓이가 그림의 3.5배</b>가 됐다
            //   (엘린: 그림 2.61x2.15 = 5.6타일² vs 반경 2.52 원 = 19.9타일²).
            //   사각형이면 딱 그림만 덮는다.
            //
            //   오프셋 y 에 높이의 절반을 넣는 이유: 캐릭터 피벗이 <b>발밑</b>이라 그림이
            //   위로만 솟는다. 0 으로 두면 상자의 아래 절반이 바닥 아래로 빠져 머리가 잘린다.
            vision.SetVisionBox(size, new Vector2(0f, size.y * 0.5f));
            return true;
        }

        // ------------------------------------------------------------------
        // 매 프레임 (서비스가 부른다)
        // ------------------------------------------------------------------

        public void Tick(float dt)
        {
            if (_unit == null) return;

            // ★ 쓰러져 부활을 기다리는 동안에도 <b>이 한 갈래만</b> 돈다 —
            //   경직 시간을 세는 주체가 여기이기 때문이다. 나머지 효과는 죽은 채로
            //   돌면 안 되므로(아우라·자해·쿨타임) 여기서 끊는다.
            if (!_unit.IsAlive)
            {
                if (_reviving) TickRevive();
                return;
            }

            Refresh();
            if (_active.Count == 0) return;

            // 「타고난 섬세함」의 시야 예외는 <b>스킨이 붙은 뒤에야</b> 그림 크기를 잴 수 있다.
            // 캐릭터는 프레임 중간에 Instantiate 되므로 Refresh 시점에 CharacterAnimator 가
            // 아직 준비 안 됐을 수 있고, Refresh 는 강화 횟수가 바뀔 때만 다시 도므로
            // 그 안에서만 재시도하면 <b>영영 안 걸린다</b> — 걸릴 때까지 여기서 다시 시도한다
            // (성공하면 _visionZeroed 가 켜져 다시는 들어오지 않는다).
            if (!_visionZeroed && Has(PassiveSkillType.InnateDelicacy))
                _visionZeroed = ApplyBlindVision();

            TickRampage();
            TickEcstasy();
            TickDefenseAura();
            TickBlazingWings(dt);
            TickRageDecay(dt);
            TickUncontrollablePleasure();
            TickNewcomers(dt);          // 신규 3인 (CharacterPassives.Newcomers.cs)
            TickTrio(dt);               // 신규 3인 2차 (CharacterPassives.Trio.cs)
            TickCooldownSkills();
        }

        /// <summary>
        /// 희생·정신 안정·정화의 손길 중 <b>쿨타임이 된 것을 우선순위(내림차순) 순서로 시도</b>해
        /// 최대 하나만 발동시킨다 — 동시 발동 방지 규칙(위 <see cref="_cooldownPriority"/> 주석).
        /// 앞 순위가 쿨타임이 안 됐거나 조건(다친 동료 없음 등)이 안 맞으면 다음 순위로 넘어간다 —
        /// 큰 스킬 하나가 계속 막혀 있다고 작은 스킬까지 영원히 못 나가게 하지 않기 위해서다.
        /// </summary>
        void TickCooldownSkills()
        {
            for (int i = 0; i < _cooldownPriority.Count; i++)
            {
                bool fired = _cooldownPriority[i] switch
                {
                    PassiveSkillType.Sacrifice => TrySacrifice(),
                    PassiveSkillType.CalmDown => TryCalmDown(),
                    PassiveSkillType.PurifyingTouch => TryPurifyingTouch(),

                    // ── 신규 캐릭터 3인 (2026-08-20) ──
                    PassiveSkillType.ArrowRain => TryArrowRain(),
                    PassiveSkillType.Dawn => TryDawn(),
                    PassiveSkillType.FallenBody => TryFallenBody(),
                    PassiveSkillType.CelestialShield => TryCelestialShield(),
                    PassiveSkillType.DivineWrath => TryDivineWrath(),
                    PassiveSkillType.AHelpingHand => TryHelpingHand(),
                    PassiveSkillType.SacredBlessing => TrySacredBlessing(),
                    PassiveSkillType.UnfinishedNobility => TryUnfinishedNobility(),
                    PassiveSkillType.FlameBlast => TryFlameBlast(),

                    // ── 신규 3인 2차 (2026-08-21) ──
                    PassiveSkillType.StrongMind => TryStrongMind(),
                    PassiveSkillType.BlessingOfFourWings => TryBlessingOfFourWings(),
                    PassiveSkillType.EvasiveManeuver => TryEvasiveManeuver(),
                    PassiveSkillType.DeclarationOfTheEnd => TryDeclarationOfTheEnd(),
                    _ => false,
                };
                if (fired) return;   // 한 프레임에 하나만 (BossSkillCaster.Update 와 같은 규칙)
            }
        }

        /// <summary>광란 — 체력 50% 미만에서 켜지고, 다시 50% 이상이 되면 꺼진다(정의문 그대로).</summary>
        void TickRampage()
        {
            PassiveSkillSO so = Find(PassiveSkillType.Rampage);
            if (so == null)
            {
                if (_rampageOn) SetRampage(false, 0);
                return;
            }

            bool want = _unit.HpRatio < 0.5f;
            if (want == _rampageOn) return;
            SetRampage(want, Mathf.RoundToInt(so.value01));
        }

        void SetRampage(bool on, int attackBonus)
        {
            if (_appliedRampageAttack != 0)
            {
                _unit.AddFlatStatBonus(_unit.AttackStatType, -_appliedRampageAttack);
                _appliedRampageAttack = 0;
            }
            _rampageOn = on;
            if (!on) return;

            _appliedRampageAttack = attackBonus;
            _unit.AddFlatStatBonus(_unit.AttackStatType, _appliedRampageAttack);
        }

        /// <summary>희열 — 중첩이 만료되면 한꺼번에 내려놓는다.</summary>
        void TickEcstasy()
        {
            if (_ecstasyStacks <= 0) return;
            if (Time.time < _ecstasyEndTime) return;

            PassiveSkillSO so = Find(PassiveSkillType.Ecstasy);
            int per = so != null ? Mathf.RoundToInt(so.value03) : 0;
            if (per != 0)
            {
                _unit.AddFlatStatBonus(StatType.AttackSpeed, -per * _ecstasyStacks);
                _unit.AddFlatStatBonus(StatType.MoveSpeed, -per * _ecstasyStacks);
            }
            _ecstasyStacks = 0;
        }

        /// <summary>
        /// 로 아이아스 — 반경 안의 동료에게 방어력을 걸고, 빠져나간 동료에게서는 거둔다.
        /// <b>걸어준 양을 유닛별로 기억</b>하는 이유: 스킬 값이 바뀌거나 대상이 죽어도
        /// 정확히 같은 값을 되돌려야 방어력이 새지 않는다.
        /// </summary>
        void TickDefenseAura()
        {
            PassiveSkillSO so = Find(PassiveSkillType.RhoAias);
            if (so == null)
            {
                ClearDefenseAura();
                return;
            }

            float radius = so.value01;
            int amount = Mathf.RoundToInt(so.value02);
            Vector3 myPos = transform.position;
            float sqr = radius * radius;

            _auraScratch.Clear();
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive) continue;
                if (u.Faction != _unit.Faction) continue;
                if (ReferenceEquals(u, _unit)) continue;   // 정의문: "동료들" — 본인 제외
                if (((Vector2)(u.transform.position - myPos)).sqrMagnitude > sqr) continue;
                _auraScratch.Add(u);
            }

            // 새로 들어온 대상에게 걸기 / 값이 바뀐 대상 보정
            for (int i = 0; i < _auraScratch.Count; i++)
            {
                DamageableUnit u = _auraScratch[i];
                _auraTargets.TryGetValue(u, out int now);
                if (now == amount) continue;
                u.AddDefenseModifier(amount - now);
                _auraTargets[u] = amount;
            }

            // 빠져나간(또는 죽은) 대상에게서 거두기
            _auraRemove.Clear();
            foreach (var kv in _auraTargets)
            {
                if (kv.Key != null && kv.Key.IsAlive && _auraScratch.Contains(kv.Key)) continue;
                _auraRemove.Add(kv.Key);
            }
            for (int i = 0; i < _auraRemove.Count; i++)
            {
                DamageableUnit u = _auraRemove[i];
                if (u != null) u.AddDefenseModifier(-_auraTargets[u]);
                _auraTargets.Remove(u);
            }
        }

        static readonly List<DamageableUnit> _auraScratch = new List<DamageableUnit>();
        static readonly List<DamageableUnit> _auraRemove = new List<DamageableUnit>();

        void ClearDefenseAura()
        {
            if (_auraTargets.Count == 0) return;
            foreach (var kv in _auraTargets)
                if (kv.Key != null) kv.Key.AddDefenseModifier(-kv.Value);
            _auraTargets.Clear();
        }

        /// <summary>
        /// 타오르는 날개 — 반경 안의 적에게 <b>초당</b> 자기 현재 체력의 value02% 피해.
        /// 정의문에 "데미지 계산 공식 적용" 이 명시돼 있으므로 방어력 감소를 거친다
        /// (<see cref="DamageableUnit.TakeDamageFrom"/> 이 아니라 계산된 값을 넣는 경로는
        /// 방어력을 안 거치므로 쓰지 않는다).
        ///
        /// 초당 값을 프레임으로 쪼개면 정수 피해가 0 이 되어 아무 일도 안 일어난다 —
        /// <see cref="_blazeCarry"/> 에 실수로 모아 1 이상이 될 때만 적용한다
        /// (체력 재생이 틱을 쓰는 것과 같은 이유, 4절).
        /// </summary>
        void TickBlazingWings(float dt)
        {
            PassiveSkillSO so = Find(PassiveSkillType.BlazingWings);
            if (so == null) { _blazeCarry = 0f; return; }

            float perSecond = _unit.CurrentHp * so.value02 * 0.01f;
            if (perSecond <= 0f) return;

            _blazeCarry += perSecond * dt;
            if (_blazeCarry < 1f) return;

            int amount = Mathf.FloorToInt(_blazeCarry);
            _blazeCarry -= amount;

            float radius = so.value01;
            float sqr = radius * radius;
            Vector3 myPos = transform.position;
            Faction enemy = _unit.Faction.Opposite();

            var all = UnitRegistry.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive || u.Faction != enemy) continue;
                if (((Vector2)(u.transform.position - myPos)).sqrMagnitude > sqr) continue;

                // 방어력 공식을 거친다 — 공격력 자리에 "초당 피해량" 을 넣는다.
                int def = Mathf.Max(0, u.DefenseStat + u.DefenseModifier);
                int dealt = _unit.Balance != null ? _unit.Balance.Damage(amount, def) : amount;
                u.ApplyDamage(dealt);
                _unit.MarkCombatAction();
            }
        }

        // ==================================================================
        // 히스톤 9005 — 분노(80014) · 복수자(80015)
        //
        // <b>왜 '분노'가 여기 있나</b> — 캐릭터 한 명에게만 붙는 별개 자원이라
        // <see cref="CharacterUnit"/> 의 능력치 칸에 넣을 수 없다(다른 넷에게는 의미가 없다).
        // 「희열」의 중첩 수·「정화의 손길」의 발동 시각과 같은 자리, 같은 취급이다.
        //
        // <b>부활은 33-6절과 충돌하지 않는다</b>: 그 규칙("죽으면 재등장할 수 없다")은
        // <b>새 캐릭터를 뽑을 때 그 id 를 다시 안 쓴다</b>는 등장 규칙이고, 여기 부활은
        // 죽은 자리에서 <b>같은 개체</b>가 일어나는 것이다. 히스톤은 파괴되지 않으므로
        // <c>CharacterDefinitionRegistry</c> 의 등장 장부에 아무 영향이 없다.
        // ==================================================================

        /// <summary>지금 분노 수치(0~100). 성장 창·로스터가 표시에 쓸 수 있다.</summary>
        public float Rage => _rage;

        /// <summary>이 캐릭터가 '분노'를 쌓는가 (= 「분노」 패시브가 해금돼 있는가).</summary>
        public bool HasRage => Has(PassiveSkillType.RageOn);

        /// <summary>지금 쓰러져 부활을 기다리는 중인가. 죽었지만 파괴되지 않은 상태다.</summary>
        public bool IsReviving => _reviving;

        /// <summary>부활이 <b>지금</b> 가능한가 — 분노가 가득하고 쿨타임도 끝났는가.</summary>
        public bool ReviveReady =>
            HasRage && _rage >= RageMax && Time.time >= _reviveReadyAt;

        /// <summary>분노 상한. 정의문이 "(0~100)" 이라고 못박은 값이라 표에 칸이 없다.</summary>
        public const float RageMax = 100f;

        /// <summary>
        /// 분노 하락 — <b>체력 재생이 도는 상태</b>에서 초당 value02 만큼 떨어진다.
        /// 진군 중에는 예외로 떨어지지 않는다(정의문).
        ///
        /// ⚠ "체력 재생 가능 상태" 를 <see cref="DamageableUnit.IsInCombat"/> 가 꺼진 것으로
        /// 읽었다. <see cref="DamageableUnit"/> 의 재생 관문은 <c>IsInCombat || 만피</c> 인데,
        /// <b>만피 조건까지 그대로 옮기면 체력이 꽉 찬 히스톤의 분노가 영원히 안 줄어든다</b> —
        /// 정의문의 의도(전투를 쉬면 분노가 식는다)와 반대가 된다. 전투 여부만 본다.
        /// </summary>
        void TickRageDecay(float dt)
        {
            PassiveSkillSO so = Find(PassiveSkillType.RageOn);
            if (so == null) { _rage = 0f; return; }
            if (_rage <= 0f) return;

            if (_unit.IsInCombat) return;                 // 재생이 안 도는 상태
            if (PassiveSkillService.WaveIsMarching) return;  // 진군 중 예외 (정의문)

            _rage = Mathf.Max(0f, _rage - Mathf.Max(0f, so.value02) * dt);
        }

        /// <summary>
        /// 사망 순간에 <see cref="CharacterUnit.OnDeath"/> 가 묻는다 —
        /// <b>true 를 돌려주면 그쪽이 <c>Destroy</c> 를 건너뛴다.</b>
        ///
        /// 여기서 쿨타임을 <b>미리</b> 걸고 분노를 <b>미리</b> 비우는 이유: 경직 중에 또 죽거나
        /// (이미 체력 0 이라 실제로는 안 일어나지만) 이 함수가 두 번 불려도 부활이 두 번
        /// 예약되지 않게 하려는 것이다. 조건 검사와 상태 변경 사이에 틈을 두지 않는다.
        /// </summary>
        public bool TryBeginRevive()
        {
            if (_reviving) return true;          // 이미 예약돼 있다 — 파괴만 막으면 된다

            PassiveSkillSO so = Find(PassiveSkillType.RageOn);
            if (so == null) return false;
            if (_rage < RageMax) return false;
            if (Time.time < _reviveReadyAt) return false;

            float stun = Mathf.Max(0f, so.value03);

            _reviving = true;
            _reviveAt = Time.time + stun;
            _reviveReadyAt = Time.time + Mathf.Max(0f, so.coolTime);
            _rage = 0f;                          // 부활에 쓰고 비운다

            // 경직 내내 쓰러진 모션을 돌린다 — 원화가 없으면 조용히 생략된다.
            _animator?.PlayReviveMotion(stun);

            // ★★★ 2026-08-25 — <b>「복수자」의 범위 연출을 여기서 같이 깐다</b> (유저 지시:
            //   *"히스톤 세번째 스킬 … <b>이펙트 삭제하고 두번째 스킬 부활 시에 해당 스킬
            //   이펙트 그냥 같이 넣어서 표현</b>"*).
            //
            //   <b>예전에는 두 박자였다</b> — ① 경직 내내 쓰러진 모션이 돌고 ② 경직이 <b>끝난 뒤</b>
            //   <see cref="PerformReaverBurst"/> 가 0.6초짜리 범위 연출을 <b>따로</b> 깔았다.
            //   그래서 「일어난다」와 「터진다」가 <b>이어진 한 장면으로 안 읽혔다</b>.
            //
            //   ★ 이제 연출은 <b>부활의 일부</b>다 — 쓰러진 모션과 <b>같은 구간</b>에 깔려
            //     경직 내내 발밑에 퍼져 있다가 일어설 때 함께 끝난다.
            //   ⚠ <b>피해·회복은 옮기지 않았다</b>(<see cref="PerformReaverBurst"/> 에 그대로 있다).
            //     유저가 바꾼 것은 «이펙트» 이고, 경직 중에 피해를 넣으면 <b>죽어 있는 동안
            //     싸우는</b> 것이 된다 — 그것은 연출 변경이 아니라 밸런스 변경이다.
            PlayReviveFx(stun);

            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName,
                                              string.Format(UI.HudTheme.T("log_detail_revive_in",
                                                                          "{0:0.#}초 뒤 부활"), stun)),
                          UI.HudLogKind.Good);
            return true;
        }

        /// <summary>
        /// 경직이 끝났는지 보고, 끝났으면 실제로 되살린다. 죽어 있는 동안만 돈다.
        ///
        /// ★★ <b>죽은 그 자리에서 일어난다 — 위치를 절대 옮기지 않는다</b>
        /// (유저 확정 2026-08-16: <i>"히스톤은 부활할때 사망한 자리에서 부활해야 함.
        /// 생성할때만 중앙건물에서 생성"</i>).
        ///
        /// 그래서 이 함수에는 <b>좌표를 건드리는 줄이 한 줄도 없다</b> — 부활은
        /// <b>체력을 되돌리는 것</b>뿐이고, 몸은 애초에 파괴되지 않아 그 자리에 그대로 있다
        /// (<see cref="LastSanctuary.Units.CharacterUnit.OnDeath"/> 가 <c>Destroy</c> 를 건너뛴다).
        ///
        /// ⚠ <b>성역 생성 경로(<c>UnitSpawner</c>)를 타면 안 된다.</b> 그쪽은
        /// <b>최초 생성 전용</b>이다 — 되살릴 때 그 경로를 쓰면 히스톤이 맵 반대편에서
        /// 갑자기 중앙건물로 순간이동한다. 부활에 "다시 만든다"는 개념을 넣지 말 것.
        ///
        /// (일어난 <b>뒤에</b> 스스로 집결지·전열로 걸어 돌아가는 것은 평소 이동이라
        ///  이 규칙과 무관하다.)
        /// </summary>
        void TickRevive()
        {
            if (Time.time < _reviveAt) return;
            _reviving = false;

            // ★ 체력을 되돌리는 것이 곧 부활이다 — <see cref="DamageableUnit.Heal"/> 은
            //   살아있는 유닛만 회복시키므로(IsAlive 가드) 쓸 수 없다. 전용 통로를 쓴다.
            //   ⚠ 여기에 위치 대입을 추가하지 말 것(위 주석).
            _unit.ReviveWithHp(_unit.MaxHp);

            UI.HudLog.Add(string.Format(UI.HudTheme.T("log_revived", "{0} 부활"), _unit.DisplayName),
                          UI.HudLogKind.Good);

            PerformReaverBurst();
        }

        /// <summary>
        /// 복수자 — 부활하는 순간 반경 value01 타일 <b>원형</b> 안에서
        /// 적에게 공격력의 value02% 피해, <b>아군 캐릭터</b>에게 최대체력의 value03% 회복.
        ///
        /// 적 피해는 <see cref="DamageableUnit.TakeDamageFrom"/> 로 넣는다 — 정의문이
        /// "공격력의 %" 라고 공격력을 기준으로 삼았으므로 방어력·치명타를 포함한
        /// 정상 데미지 파이프라인을 타는 게 맞다(보스 스킬이 쓰는 경로와 같다).
        /// 「타오르는 날개」가 <c>ApplyDamage</c> 를 쓰는 것과 갈리는 지점인데, 그쪽은
        /// 기준이 "자기 <b>체력</b>의 %" 라 공격력 자리에 넣을 값이 따로 있었다.
        ///
        /// 회복 대상은 <b>캐릭터만</b>이다(정의문 "아군 캐릭터들") — 성역·포탑은 제외한다.
        /// 73-13절이 치유 유형에 대해 확정한 규칙과 같다. 자기 자신은 이미 만피로
        /// 일어났으므로 넣어도 아무 일이 없지만, 정의문이 "아군"이라 했으니 제외하지 않는다.
        /// </summary>
        void PerformReaverBurst()
        {
            PassiveSkillSO so = Find(PassiveSkillType.Reaver);
            if (so == null) return;

            float radius = Mathf.Max(0f, so.value01);
            if (radius <= 0f) return;

            int damagePercent = Mathf.RoundToInt(so.value02);
            float healRatio = so.value03 * 0.01f;
            float sqr = radius * radius;
            Vector3 myPos = transform.position;

            // ⚠ <b>여기서 연출을 깔지 않는다</b> (2026-08-25 · 유저 지시로 옮겼다) —
            //   범위 연출은 <see cref="TryBeginRevive"/> 가 <b>부활 모션과 같은 구간</b>에 깐다.
            //   이 함수에는 이제 <b>피해와 회복만</b> 남는다.

            // 목록을 먼저 복사한다 — 피해로 유닛이 죽으면 UnitRegistry.All 이 그 자리에서
            // 바뀔 수 있다(OnDied → 파괴 → Unregister). 역순 순회만으로는 부족하다.
            _reaverScratch.Clear();
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive) continue;
                if (((Vector2)(u.transform.position - myPos)).sqrMagnitude > sqr) continue;
                _reaverScratch.Add(u);
            }

            for (int i = 0; i < _reaverScratch.Count; i++)
            {
                DamageableUnit u = _reaverScratch[i];
                if (u == null || !u.IsAlive) continue;

                if (u.Faction == _unit.Faction)
                {
                    if (u.Kind != UnitKind.Character) continue;      // 성역·포탑 제외
                    if (!u.AcceptsExternalHeal) continue;            // 이기심
                    int heal = Mathf.RoundToInt(u.MaxHp * healRatio);
                    if (heal > 0) u.Heal(heal);
                }
                else if (damagePercent > 0)
                {
                    u.TakeDamageFrom(_unit, damagePercent);
                }
            }
            _reaverScratch.Clear();

            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName,
                                              string.Format(UI.HudTheme.T("log_detail_radius",
                                                                          "반경 {0:0.#}타일"), radius)),
                          UI.HudLogKind.Good);
        }

        /// <summary>
        /// ★★ <b>부활 범위 연출</b> — 쓰러진 모션과 <b>같은 구간</b>에 발밑에 깐다
        /// (2026-08-25 · 유저 지시로 「복수자」에서 <b>부활</b>로 옮겼다. <see cref="TryBeginRevive"/> 참조).
        ///
        /// ★ <b>크기는 「복수자」의 반경을 따른다</b> — 연출이 부활 쪽으로 왔어도 «보이는 범위 =
        ///   맞는 범위» 규칙(61-5절)은 그대로다. 실제로 맞는 범위가 그 반경이기 때문이다.
        /// ⚠ <b>「복수자」가 없으면(=아직 안 열렸으면) 반경을 지어내지 않는다</b> — 그때는
        ///   터질 것이 없으므로 연출도 깔지 않는다. 안 그러면 «퍼졌는데 아무 일도 안 일어나는»
        ///   부활이 된다.
        /// ⚠ 원화(<c>reviveFx</c>)가 없는 스킨은 조용히 넘어간다 — 히스톤 외에는 다 비어 있다.
        /// </summary>
        void PlayReviveFx(float seconds)
        {
            if (seconds <= 0f) return;

            Sprite[] fx = _animator != null && _animator.Skin != null ? _animator.Skin.ReviveFx() : null;
            if (fx == null) return;

            PassiveSkillSO reaver = Find(PassiveSkillType.Reaver);
            float radius = reaver != null ? Mathf.Max(0f, reaver.value01) : 0f;
            if (radius <= 0f) return;

            CombatProjectileFx.PlayArea(fx, transform.position,
                                        new Vector2(radius * 2f, radius * 2f),
                                        0f, null, seconds);
        }

        /// <summary>복수자 범위 판정용 임시 목록. 유닛마다 갖지 않도록 정적으로 공유한다.</summary>
        static readonly List<DamageableUnit> _reaverScratch = new List<DamageableUnit>();

        /// <summary>
        /// 희생 — 주변에 최대 체력의 value01% 이상 잃은 동료가 있으면, 자기 체력을 value02%
        /// 깎아 그 동료를 최대 체력의 value02% 만큼 회복시킨다. 쿨타임.
        ///
        /// 반경이 정의문에 없어서(“엘린의 주변”) <see cref="PassiveSkillService.assistRadius"/> 를
        /// 쓴다 — 인스펙터 값이다(값을 코드에 박지 않는다는 이 프로젝트의 규칙, 35절).
        /// </summary>
        /// <summary>발동했으면 true — <see cref="TickCooldownSkills"/> 의 우선순위 시도가 이 값을 본다.</summary>
        bool TrySacrifice()
        {
            PassiveSkillSO so = Find(PassiveSkillType.Sacrifice);
            if (so == null || Time.time < _sacrificeReadyAt) return false;

            float needLostRatio = so.value01 * 0.01f;
            float ratio = so.value02 * 0.01f;
            if (ratio <= 0f) return false;

            // 자기 체력을 깎는 효과이므로 자기가 위험하면 하지 않는다 — 안 그러면 자살한다.
            int cost = Mathf.RoundToInt(_unit.MaxHp * ratio);
            if (cost <= 0 || _unit.CurrentHp <= cost) return false;

            DamageableUnit target = FindWoundedAlly(needLostRatio);
            if (target == null) return false;

            _sacrificeReadyAt = Time.time + Mathf.Max(0f, so.coolTime);
            _unit.ApplyDamage(cost);
            target.Heal(Mathf.RoundToInt(target.MaxHp * ratio));

            // 스킬 이름을 코드에 적지 않는다 — 표(so.DisplayName)에서 온다.
            // 형식은 보스 스킬과 같은 UI.HudLog.SkillLine 한 곳이 정한다.
            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName,
                                              string.Format(UI.HudTheme.T("log_detail_healed",
                                                                          "{0} 회복"),
                                                            DisplayNameOf(target))),
                          UI.HudLogKind.Good);
            return true;
        }

        DamageableUnit FindWoundedAlly(float needLostRatio)
        {
            float radius = PassiveSkillService.AssistRadius;
            float sqr = radius * radius;
            Vector3 myPos = transform.position;

            DamageableUnit worst = null;
            float worstRatio = 1f;

            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive || ReferenceEquals(u, _unit)) continue;
                if (u.Faction != _unit.Faction || u.Kind != UnitKind.Character) continue;
                if (!u.AcceptsExternalHeal) continue;      // 이기심 — 받지 못한다
                if (u.HpRatio > 1f - needLostRatio) continue;
                if (((Vector2)(u.transform.position - myPos)).sqrMagnitude > sqr) continue;
                if (u.HpRatio >= worstRatio) continue;

                worst = u;
                worstRatio = u.HpRatio;
            }
            return worst;
        }

        /// <summary>
        /// 정신 안정 — <b>같은 집결지</b>의 동료가 나쁜 정신 이상에 걸려 있으면 즉시 해제하고
        /// 침식을 value01 만큼 낮춘다. 쿨타임.
        ///
        /// "같은 집결지" 는 <see cref="UI.RallyPointService.TryGetRallyPoint"/> 로 판정한다 —
        /// 두 캐릭터의 집결지 좌표가 같으면 같은 집결지다(부대당 하나이므로 47-3절 기준으로
        /// 사실상 "같은 부대" 와 같다). 집결지가 없는 캐릭터끼리는 이 스킬이 작동하지 않는다.
        /// </summary>
        /// <summary>발동했으면 true — <see cref="TickCooldownSkills"/> 의 우선순위 시도가 이 값을 본다.</summary>
        bool TryCalmDown()
        {
            PassiveSkillSO so = Find(PassiveSkillType.CalmDown);
            if (so == null || Time.time < _calmDownReadyAt) return false;

            if (!UI.RallyPointService.TryGetRallyPoint(_unit, out Vector3 myRally)) return false;

            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is CharacterUnit ally) || !ally.IsAlive) continue;
                if (ReferenceEquals(ally, _unit)) continue;

                // ★ 2026-08-19 — <b>구속도 푸는 대상이다.</b> 근거는 아니사킬
                //   「거대한 위협 포효」의 정의문: <i>"구속 상태는 부정적인 정신 이상 상태를
                //   해제하는 효과로 해제 가능하다"</i>. 구속은 정신 이상이 아니라
                //   <c>UnitCombat</c> 의 상태지만, 표가 <b>이 스킬을 해제 수단으로 지정</b>했다.
                //   ⚠ 말파스 구속탄의 구속에도 같이 걸린다 — 같은 상태다.
                CharacterErosion ero = CharacterErosion.Of(ally);
                UnitCombat allyCombat = ally.GetComponent<UnitCombat>();

                bool badMental = ero != null && ero.HasActive
                                 && !MentalErrorTypes.IsGood(ero.ActiveType);
                bool bound = allyCombat != null && allyCombat.IsBound;
                if (!badMental && !bound) continue;

                if (!UI.RallyPointService.TryGetRallyPoint(ally, out Vector3 rally)) continue;
                if ((rally - myRally).sqrMagnitude > 0.01f) continue;     // 같은 집결지인지

                _calmDownReadyAt = Time.time + Mathf.Max(0f, so.coolTime);
                if (badMental) ero.ClearActiveExternally();
                if (bound) allyCombat.ClearBind();
                ero?.AddErosion(-so.value01);

                UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName,
                                                  string.Format(UI.HudTheme.T("log_detail_healed",
                                                                              "{0} 회복"),
                                                                ally.DisplayName)),
                              UI.HudLogKind.Good);
                return true;   // 쿨타임당 한 명
            }
            return false;
        }

        /// <summary>
        /// 정화의 손길 — 자기 체력이 최대의 value03% 이하가 되면 발동해 value02초 지속한다.
        /// 지속 중 때린 적에게 '정화' 표식이 붙고, 그 적을 때린 <b>아군</b>이 회복한다
        /// (표식 처리는 <see cref="PassiveSkillService"/> 의 공격 이벤트 쪽).
        /// </summary>
        /// <summary>발동했으면 true — <see cref="TickCooldownSkills"/> 의 우선순위 시도가 이 값을 본다.</summary>
        bool TryPurifyingTouch()
        {
            PassiveSkillSO so = Find(PassiveSkillType.PurifyingTouch);
            if (so == null) return false;

            if (PurifyActive) return false;
            if (Time.time < _purifyReadyAt) return false;
            if (_unit.HpRatio > so.value03 * 0.01f) return false;

            _purifyEndTime = Time.time + Mathf.Max(0.1f, so.value02);
            _purifyReadyAt = Time.time + Mathf.Max(0f, so.coolTime);
            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName),
                          UI.HudLogKind.Good);
            return true;
        }

        /// <summary>
        /// 로그에 쓸 대상 이름. 표를 가진 유닛(캐릭터·웨이브 몬스터·중립 몬스터)은
        /// <b>표의 이름</b>을 쓰고, 그것이 없는 유닛(성역·포탑)만 오브젝트 이름으로 떨어진다 —
        /// 복제본 뒤에 붙는 번호가 로그에 새어나오지 않게 하는 규칙이다(유저 지시 2026-08-13).
        ///
        /// ★ 2026-08-15 — 여기 있던 종류별 갈래를 지우고
        /// <see cref="DamageableUnit.DisplayName"/> 에게 물어본다. 갈래가
        /// <c>BattleLogPanel</c> 에도 한 벌 더 있었고 <b>둘 다 중립을 빠뜨렸다</b>.
        /// </summary>
        static string DisplayNameOf(DamageableUnit u) =>
            u != null ? u.DisplayName : string.Empty;

        /// <summary>정화의 손길이 지금 켜져 있는지.</summary>
        public bool PurifyActive => Time.time < _purifyEndTime;

        // ------------------------------------------------------------------
        // 이벤트에서 불리는 것들 (PassiveSkillService 가 중계한다)
        // ------------------------------------------------------------------

        /// <summary>
        /// 이 캐릭터가 <b>때리기 직전</b>. <see cref="DamageableUnit.OnAnyAttack"/> 이 피해 계산
        /// 전에 발생하므로, 여기서 공격력 일회성 보정을 넣으면 그 공격에 그대로 반영된다.
        /// </summary>
        public void OnAttacking(DamageableUnit target)
        {
            // ── 유혈 낭자: 현재 체력 value01% 를 깎고 그만큼 공격력에 더한다 ──
            PassiveSkillSO blood = Find(PassiveSkillType.BloodAttack);
            if (blood != null)
            {
                int cost = Mathf.RoundToInt(_unit.CurrentHp * blood.value01 * 0.01f);
                // 자해로 죽지는 않게 한다 — 정의문에 사망 처리가 없다.
                cost = Mathf.Min(cost, Mathf.Max(0, _unit.CurrentHp - 1));
                if (cost > 0)
                {
                    _unit.ApplyDamage(cost);
                    _unit.OneShotAttackBonus += cost;
                }
            }

            // ── 광란: 공격 시 현재 체력 value02% 소모 ──
            PassiveSkillSO rampage = Find(PassiveSkillType.Rampage);
            if (rampage != null && _rampageOn)
            {
                int cost = Mathf.RoundToInt(_unit.CurrentHp * rampage.value02 * 0.01f);
                cost = Mathf.Min(cost, Mathf.Max(0, _unit.CurrentHp - 1));
                if (cost > 0) _unit.ApplyDamage(cost);
            }

            // ── 타오르는 분노(불칸): 확률로 화상 (CharacterPassives.Newcomers.cs) ──
            OnAttackingNewcomers(target);

            // ── 부식: 맞은 적의 방어력 −value01, value02초. 중첩 불가 ──
            PassiveSkillSO corrosion = Find(PassiveSkillType.Corrosion);
            if (corrosion != null && target != null && target.Faction != _unit.Faction)
                PassiveSkillService.ApplyCorrosion(target,
                                                   Mathf.RoundToInt(corrosion.value01),
                                                   corrosion.value02);

            // ── 정화의 손길: 발동 중이면 때린 적에게 표식을 남긴다 ──
            if (PurifyActive && target != null && target.Faction != _unit.Faction)
                PassiveSkillService.MarkPurified(target);

            // ── 분노: 공격할 때마다 value01 만큼 쌓인다 (상한 100) ──
            //    정의문이 "공격 할때 마다" 라 <b>맞았는지는 보지 않는다</b> — 이 이벤트는
            //    명중 판정 <b>전에</b> 발생하므로(33-3절) 빗나간 공격도 분노를 준다.
            //    히스톤은 근거리라 어차피 항상 명중하지만, 규칙을 명시해 둔다.
            PassiveSkillSO rage = Find(PassiveSkillType.RageOn);
            if (rage != null)
                _rage = Mathf.Min(RageMax, _rage + Mathf.Max(0f, rage.value01));
        }

        // ------------------------------------------------------------------
        // ★★ 시그리드 9006 — 가학증 · 고통의 기쁨 · 통제할 수 없는 쾌락 (2026-08-20)
        //
        // 세 개가 <b>한 줄기</b>다: 적을 죽여 「가학증」이 터지면 그 자리에서
        // 「고통의 기쁨」이 켜지고, 그러다 체력이 바닥나면 「통제할 수 없는 쾌락」이 지켜준다.
        // 그래서 발동 지점도 한 곳(:func:`OnRecentTargetKilled`)에 모여 있다.
        //
        // ★ <b>왜 자기 체력을 「현재 체력」 기준으로 다루나</b> — 정의문이 그렇게 적혀 있다:
        //   *"아군의 현재 체력을 시그리드의 현재체력의 {v4}% 만큼 회복시키고 시그리드는
        //   현재 체력이 {v4}% 만큼 감소합니다."* 최대 체력이 아니라 <b>현재</b> 체력이라
        //   연달아 터지면 회복량이 점점 줄어든다(체력이 줄기 때문에) — 그게 설계다.
        // ------------------------------------------------------------------

        /// <summary>「통제할 수 없는 쾌락」이 다시 발동할 수 있는 시각.</summary>
        float _pleasureReadyAt;

        /// <summary>
        /// ★ <b>「통제할 수 없는 쾌락」</b> — 현재 체력이 최대의 value01% 아래로 내려가면
        /// value02 초 동안 무적. 쿨타임(coolTime)이 지나야 다시 걸린다.
        ///
        /// <b>왜 매 프레임 보나</b> — 조건이 「낮아지면」이라 <b>어떤 경로로 깎였는지와
        /// 무관</b>하다(평타 · 지속 피해 · 자기 스킬로 깎는 「가학증」까지). 피해 경로마다
        /// 훅을 걸면 새 경로가 생길 때 조용히 빠진다 — 상태를 보는 쪽이 안전하다.
        ///
        /// ⚠ 이미 무적인 동안에는 다시 걸지 않는다 — 그러면 체력이 낮은 채로 무한 무적이 된다.
        /// </summary>
        void TickUncontrollablePleasure()
        {
            PassiveSkillSO so = Find(PassiveSkillType.UncontrollablePleasure);
            if (so == null || !_unit.IsAlive) return;
            if (_unit.IsInvulnerable) return;
            if (Time.time < _pleasureReadyAt) return;

            int threshold = Mathf.RoundToInt(_unit.MaxHp * so.value01 * 0.01f);
            if (_unit.CurrentHp > threshold) return;

            float seconds = Mathf.Max(0.1f, so.value02);
            _unit.GrantInvulnerability(seconds);
            _pleasureReadyAt = Time.time + Mathf.Max(0f, so.coolTime);

            // 형식은 다른 패시브와 같은 UI.HudLog.SkillLine 한 곳이 정한다.
            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName,
                                              string.Format(UI.HudTheme.T("log_detail_invincible",
                                                                          "{0:0.#}초 무적"), seconds)),
                          UI.HudLogKind.Good);
        }

        /// <summary>
        /// ★ <b>「가학증」</b> — 때린 적이 value01 초 안에 죽었을 때 value02% 확률로 터진다.
        /// 터지면 지름 value03 안의 <b>아군</b>을 시그리드 현재 체력의 value04% 만큼 회복시키고
        /// 시그리드는 그만큼 잃는다. 그리고 「고통의 기쁨」을 켠다.
        ///
        /// <paramref name="sinceHit"/> 는 «때린 뒤 죽기까지 걸린 시간(초)» 이다.
        /// ⚠ 이 스킬만 <b>자기 value01</b> 로 창을 잰다 — 포식·희열은 서비스의 전역
        /// <c>killCreditSeconds</c>(2초)를 쓴다. 지금은 두 값이 같지만, 표를 고쳤을 때
        /// 스킬 정의문("{v1}초 안에")과 실제 동작이 어긋나지 않게 여기서 다시 본다.
        ///
        /// ⚠ 「지름」이다 — 반경이 아니다(정의문: "지름 {v3}범위"). 그래서 반으로 나눈다.
        ///   로 아이아스(value01 = 반경)와 다르니 헷갈리지 말 것.
        /// </summary>
        void TrySadism(float sinceHit)
        {
            PassiveSkillSO so = Find(PassiveSkillType.Sadism);
            if (so == null || !_unit.IsAlive) return;
            if (sinceHit > Mathf.Max(0.01f, so.value01)) return;
            if (Random.value * 100f >= so.value02) return;

            // 회복·소모량은 <b>지금</b> 체력 기준 (위 ★ 주석).
            int amount = Mathf.RoundToInt(_unit.CurrentHp * so.value04 * 0.01f);

            int healed = 0;
            if (amount > 0)
            {
                float radius = Mathf.Max(0f, so.value03) * 0.5f;   // ⚠ 지름 → 반경
                float sqr = radius * radius;
                Vector3 myPos = transform.position;

                var all = UnitRegistry.All;
                for (int i = 0; i < all.Count; i++)
                {
                    DamageableUnit u = all[i];
                    if (u == null || !u.IsAlive) continue;
                    if (u.Faction != _unit.Faction) continue;
                    if (ReferenceEquals(u, _unit)) continue;      // 자기는 깎이는 쪽이다
                    if (!u.AcceptsExternalHeal) continue;          // 「이기심」은 회복을 거부한다
                    if (((Vector2)(u.transform.position - myPos)).sqrMagnitude > sqr) continue;
                    u.Heal(amount);
                    healed++;
                }

                // ⚠ 자기 체력은 <b>ApplyDamage 로 깎지 않는다</b> — 그러면 「통제할 수 없는
                //   쾌락」의 무적이 이 소모까지 막아버려 스킬이 자기 대가를 안 치른다.
                //   대가는 무적과 무관해야 하므로 체력을 직접 줄인다.
                _unit.LoseHpToSelfCost(amount);
            }

            // ── 고통의 기쁨: 「가학증」이 발동할 때마다 (정의문) ──
            PassiveSkillSO joy = Find(PassiveSkillType.JoyOfPain);
            if (joy != null && _combat != null)
                _combat.ApplyHaste(joy.value01, joy.value02);

            UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName,
                                              string.Format(UI.HudTheme.T("log_detail_share_hp",
                                                                          "아군 {0}명 +{1} · 자신 −{1}"),
                                                            healed, amount)),
                          UI.HudLogKind.Good);
        }

        /// <summary>이 캐릭터가 최근에 때린 적이 죽었다 (포식 · 희열 · 가학증).</summary>
        public void OnRecentTargetKilled(float sinceHit = 0f)
        {
            TrySadism(sinceHit);

            PassiveSkillSO glut = Find(PassiveSkillType.Gluttony);
            if (glut != null)
            {
                int heal = Mathf.RoundToInt(_unit.MaxHp * glut.value01 * 0.01f);
                if (heal > 0) _unit.Heal(heal);
            }

            PassiveSkillSO ecs = Find(PassiveSkillType.Ecstasy);
            if (ecs != null)
            {
                int per = Mathf.RoundToInt(ecs.value03);
                if (per != 0)
                {
                    _ecstasyStacks++;
                    _unit.AddFlatStatBonus(StatType.AttackSpeed, per);
                    _unit.AddFlatStatBonus(StatType.MoveSpeed, per);
                }
                // 중첩될 때마다 지속시간이 0 으로 초기화된다 (정의문).
                _ecstasyEndTime = Time.time + Mathf.Max(0.1f, ecs.value02);
            }
        }

        /// <summary>
        /// 정신 이상 추첨 가중치를 이 캐릭터의 패시브로 보정한다.
        /// <see cref="ErosionService.RollDefinition(CharacterUnit)"/> 가 부른다.
        /// </summary>
        /// <returns>가중치 배수. 1 이면 보정 없음. 0 이면 이 종류는 뽑히지 않는다.</returns>
        public float MentalWeightMultiplier(MentalErrorDefinitionSO def)
        {
            if (def == null) return 1f;

            // ── 광란: 받을 수 있는 정신 이상이 이기심·광분으로 고정된다 (각 50%) ──
            //    발동 조건(체력 50% 미만)이 켜져 있을 때만 적용한다 — 정의문이 그 상태의 효과다.
            if (_rampageOn && Has(PassiveSkillType.Rampage))
                return def.type == MentalErrorType.Selfishness || def.type == MentalErrorType.Madness
                    ? 1f : 0f;

            // ── 강철의 의지: 좋은 효과 가중치 ×value01 ──
            //    "남은 확률을 부정적 효과에 균일하게 재분배" 는 가중치 추첨에서 자동으로 성립한다 —
            //    좋은 쪽 가중치만 키우면 나머지의 상대 비율이 그대로 유지되기 때문이다.
            PassiveSkillSO iron = Find(PassiveSkillType.WillOfIron);
            if (iron != null && MentalErrorTypes.IsGood(def.type))
                return Mathf.Max(0f, iron.value01);

            return 1f;
        }
    }
}
