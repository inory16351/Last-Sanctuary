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
    public class CharacterPassives : MonoBehaviour
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

        CharacterUnit _unit;
        UnitCombat _combat;

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
        }

        void OnDisable()
        {
            // 죽거나 사라질 때 <b>남에게 걸어둔 보정을 반드시 되돌린다</b> —
            // 안 그러면 비기오르가 죽은 뒤에도 동료 방어력이 영구히 +8 로 남는다.
            ClearDefenseAura();
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

            float radius = Mathf.Sqrt(size.x * size.x * 0.25f + size.y * size.y);
            vision.SetVision(radius * 2f);
            return true;
        }

        // ------------------------------------------------------------------
        // 매 프레임 (서비스가 부른다)
        // ------------------------------------------------------------------

        public void Tick(float dt)
        {
            if (_unit == null || !_unit.IsAlive) return;
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
                                              $"{DisplayNameOf(target)} 회복"), UI.HudLogKind.Good);
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

                CharacterErosion ero = CharacterErosion.Of(ally);
                if (ero == null || !ero.HasActive) continue;
                if (MentalErrorTypes.IsGood(ero.ActiveType)) continue;   // 좋은 효과는 풀지 않는다

                if (!UI.RallyPointService.TryGetRallyPoint(ally, out Vector3 rally)) continue;
                if ((rally - myRally).sqrMagnitude > 0.01f) continue;     // 같은 집결지인지

                _calmDownReadyAt = Time.time + Mathf.Max(0f, so.coolTime);
                ero.ClearActiveExternally();
                ero.AddErosion(-so.value01);

                UI.HudLog.Add(UI.HudLog.SkillLine(_unit.DisplayName, so.DisplayName,
                                                  $"{ally.DisplayName} 회복"), UI.HudLogKind.Good);
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
        /// 로그에 쓸 대상 이름. 캐릭터·몬스터는 <b>표의 이름</b>(<c>DisplayName</c>)을 쓰고,
        /// 그것이 없는 유닛(넥서스·포탑)만 오브젝트 이름으로 떨어진다 —
        /// 복제본 뒤에 붙는 번호가 로그에 새어나오지 않게 하는 규칙이다(유저 지시 2026-08-13).
        /// </summary>
        static string DisplayNameOf(DamageableUnit u)
        {
            if (u == null) return string.Empty;
            if (u is CharacterUnit c) return c.DisplayName;
            if (u is MonsterUnit m) return m.DisplayName;
            return u.name;
        }

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

            // ── 부식: 맞은 적의 방어력 −value01, value02초. 중첩 불가 ──
            PassiveSkillSO corrosion = Find(PassiveSkillType.Corrosion);
            if (corrosion != null && target != null && target.Faction != _unit.Faction)
                PassiveSkillService.ApplyCorrosion(target,
                                                   Mathf.RoundToInt(corrosion.value01),
                                                   corrosion.value02);

            // ── 정화의 손길: 발동 중이면 때린 적에게 표식을 남긴다 ──
            if (PurifyActive && target != null && target.Faction != _unit.Faction)
                PassiveSkillService.MarkPurified(target);
        }

        /// <summary>이 캐릭터가 최근에 때린 적이 죽었다 (포식 · 희열).</summary>
        public void OnRecentTargetKilled()
        {
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
