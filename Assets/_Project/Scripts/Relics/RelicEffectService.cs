using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Units;

namespace LastSanctuary.Relics
{
    /// <summary>
    /// 장착한 유물의 <b>효과를 실제로 거는 곳</b> — 표 <c>EffectType</c> 시트와 1:1 이다.
    ///
    /// ★★ <b>두 갈래로 나뉜다.</b>
    /// <code>
    ///   ① 능력치 계열  — 장착하는 «순간» 고정 수치를 더하고, 벗을 때 같은 수치를 뺀다
    ///   ② 반응 계열    — 값을 «걸어두지» 않고, 사건이 일어날 때 조회해서 그때 처리한다
    /// </code>
    ///
    /// <b>왜 ①이 «순간 계산» 인가</b> — 이벤트 보상(<c>EventRewardService.ApplyStat</c>)이
    /// 이미 그렇게 한다. «지금 능력치의 몇 %» 를 <b>고정 수치</b>로 환산해
    /// <see cref="CharacterUnit.AddFlatStatBonus"/> 로 더한다. 그래야
    /// <b>뺄 때 정확히 같은 값을 뺄 수 있다</b> — 비율을 그때그때 다시 계산하면
    /// 그 사이에 강화가 끼어들었을 때 뺀 값이 더한 값과 달라져 능력치가 <b>영구히 어긋난다</b>.
    /// (표 Info 시트의 «장착한 뒤 강화로 능력치가 올라도 유물 보너스는 그대로입니다» 가 이것이다.)
    ///
    /// <b>왜 ②는 안 걸어두나</b> — 흡혈·반사·처치 보상은 «상태» 가 아니라 «사건» 이다.
    /// 걸어둘 곳이 없다. <see cref="DamageableUnit"/> 이 이미 정적 이벤트를 열어 두었으므로
    /// (<c>OnAnyDamaged</c> · <c>OnAnyDied</c>) 거기에 한 번만 붙는다.
    ///
    /// ⚠ <b>정적 이벤트는 반드시 떼어야 한다</b> — 이 프로젝트는 도메인 리로드를 끄고 쓰므로
    ///   (<c>DamageableUnit.ResetStatics</c> 가 그래서 있다) 붙인 채로 두면 다음 판에
    ///   <b>두 번 걸린다</b>. <see cref="Hook"/>/<see cref="Unhook"/> 가 한 벌로 관리한다.
    /// </summary>
    public static class RelicEffectService
    {
        // ------------------------------------------------------------------
        // ① 능력치 계열 — 걸어둔 것을 그대로 기억해 두었다가 같은 값을 뺀다
        // ------------------------------------------------------------------

        struct Applied
        {
            public CharacterUnit unit;
            public StatType stat;
            public int delta;
        }

        static readonly List<Applied> _live = new List<Applied>();

        /// <summary>지금 유물을 낀 캐릭터들 — 반응 계열이 «누가 무엇을 꼈나» 를 물을 때 쓴다.</summary>
        static readonly Dictionary<CharacterUnit, RelicDefinitionSO> _worn =
            new Dictionary<CharacterUnit, RelicDefinitionSO>();

        /// <summary>이 판에서 <see cref="RelicEffectType.ReviveOnce"/> 를 이미 쓴 캐릭터.</summary>
        static readonly HashSet<CharacterUnit> _revivedOnce = new HashSet<CharacterUnit>();

        static bool _hooked;

        /// <summary>표 <c>EffectType</c> → 능력치. 여기 없으면 «반응 계열» 이다.</summary>
        static readonly Dictionary<RelicEffectType, StatType> StatEffects =
            new Dictionary<RelicEffectType, StatType>
            {
                { RelicEffectType.HpUp,            StatType.Hp },
                { RelicEffectType.MeleeAttackUp,   StatType.Attack },
                { RelicEffectType.RangedAttackUp,  StatType.RangedAttack },
                { RelicEffectType.MagicUp,         StatType.Magic },
                { RelicEffectType.DefenseUp,       StatType.Defense },
                { RelicEffectType.ResistanceUp,    StatType.Resistance },
                { RelicEffectType.RegenUp,         StatType.Regen },
                { RelicEffectType.CureUp,          StatType.Cure },
                { RelicEffectType.AccuracyUp,      StatType.Accuracy },
                { RelicEffectType.CriticalUp,      StatType.Critical },
                { RelicEffectType.AttackSpeedUp,   StatType.AttackSpeed },
                { RelicEffectType.MoveSpeedUp,     StatType.MoveSpeed },
            };

        // ==================================================================
        // 장착 · 해제
        // ==================================================================

        public static void OnEquipped(CharacterUnit unit, RelicDefinitionSO relic)
        {
            if (unit == null || relic == null) return;
            Hook();

            _worn[unit] = relic;

            if (StatEffects.TryGetValue(relic.effectType, out StatType stat))
                AddStat(unit, stat, relic.value01);

            // 시야는 능력치가 아니라 <see cref="VisionSource"/> 의 칸이다 — 따로 민다.
            if (relic.effectType == RelicEffectType.VisionUp)
                ScaleVision(unit, 1f + Mathf.Abs(relic.value01) * 0.01f);

            // ★ 「두꺼워진 가피」는 <b>지금 체력에 따라</b> 붙었다 떨어진다 — 붙일지 여기서 한 번 본다.
            if (relic.effectType == RelicEffectType.LowHpDefenseUp)
                UpdateLowHpBonus(unit, relic);
        }

        public static void OnUnequipped(CharacterUnit unit, RelicDefinitionSO relic)
        {
            if (unit == null) return;

            RemoveAllFor(unit);
            _worn.Remove(unit);

            if (relic != null && relic.effectType == RelicEffectType.VisionUp)
                ScaleVision(unit, 1f / Mathf.Max(0.01f, 1f + Mathf.Abs(relic.value01) * 0.01f));
        }

        /// <summary>판을 갈아엎을 때 — 걸어둔 것을 전부 되돌린다.</summary>
        public static void ClearAll()
        {
            for (int i = 0; i < _live.Count; i++)
            {
                Applied a = _live[i];
                if (a.unit != null) a.unit.AddFlatStatBonus(a.stat, -a.delta);
            }
            _live.Clear();
            _worn.Clear();
            _revivedOnce.Clear();
        }

        /// <summary>이 캐릭터가 낀 유물. 없으면 null. 다른 시스템(발굴 속도 등)이 묻는다.</summary>
        public static RelicDefinitionSO WornBy(CharacterUnit unit) =>
            unit != null && _worn.TryGetValue(unit, out var r) ? r : null;

        /// <summary>
        /// 이 캐릭터의 <b>발굴 속도 배율</b>. <see cref="RelicEffectType.DigSpeed"/> 를
        /// 낀 캐릭터만 1 보다 크다. <see cref="RelicDigService"/> 가 매 프레임 묻는다.
        /// </summary>
        public static float DigSpeedMultiplier(CharacterUnit unit)
        {
            RelicDefinitionSO r = WornBy(unit);
            return r != null && r.effectType == RelicEffectType.DigSpeed
                ? 1f + Mathf.Abs(r.value01) * 0.01f
                : 1f;
        }

        /// <summary>
        /// 이 캐릭터의 <b>침식 상승 배율</b>. <see cref="RelicEffectType.ErosionSlow"/> 를
        /// 낀 캐릭터만 1 보다 작다. <see cref="CharacterErosion.Tick"/> 이 곱한다.
        /// </summary>
        public static float ErosionGainMultiplier(CharacterUnit unit)
        {
            RelicDefinitionSO r = WornBy(unit);
            if (r == null || r.effectType != RelicEffectType.ErosionSlow) return 1f;
            return Mathf.Clamp01(1f - Mathf.Abs(r.value01) * 0.01f);
        }

        // ==================================================================
        // ① 능력치 — 더하고 빼기
        // ==================================================================

        static void AddStat(CharacterUnit unit, StatType stat, int percent)
        {
            if (percent == 0) return;

            int now = unit.EffectiveStat(stat);
            int delta = Mathf.RoundToInt(now * percent * 0.01f);
            // ⚠ 반올림이 0 이면 <b>최소 1</b> 로 민다 — 능력치가 낮은 캐릭터에게만 효과가
            //   사라지는 것은 «장착하면 오른다» 는 표의 문장과 어긋난다(이벤트 보상과 같은 규칙).
            if (delta == 0) delta = percent > 0 ? 1 : -1;

            unit.AddFlatStatBonus(stat, delta);
            _live.Add(new Applied { unit = unit, stat = stat, delta = delta });
        }

        static void RemoveAllFor(CharacterUnit unit)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i].unit != unit) continue;
                if (unit != null) unit.AddFlatStatBonus(_live[i].stat, -_live[i].delta);
                _live.RemoveAt(i);
            }
        }

        static void ScaleVision(CharacterUnit unit, float factor)
        {
            var vision = unit.GetComponent<Fog.VisionSource>();
            if (vision != null) vision.SetVision(vision.VisionTiles * factor);
        }

        /// <summary>
        /// 「두꺼워진 가피」 — 체력 문턱을 넘나들 때 방어력 보너스를 붙였다 뗀다.
        /// <see cref="Tick"/> 이 주기적으로 부른다.
        /// </summary>
        static void UpdateLowHpBonus(CharacterUnit unit, RelicDefinitionSO relic)
        {
            if (unit == null || relic == null || unit.MaxHp <= 0) return;

            bool wantOn = unit.IsAlive &&
                          unit.CurrentHp * 100 <= unit.MaxHp * Mathf.Max(1, relic.value02);
            bool isOn = false;
            for (int i = 0; i < _live.Count; i++)
                if (_live[i].unit == unit && _live[i].stat == StatType.Defense) { isOn = true; break; }

            if (wantOn == isOn) return;
            if (wantOn) AddStat(unit, StatType.Defense, relic.value01);
            else RemoveAllFor(unit);
        }

        /// <summary>
        /// 문턱형 효과를 다시 재는 주기 호출. <see cref="RelicDigService"/> 가
        /// «이미 매 프레임 도는 서비스» 라 거기에 얹는다 — 이 하나 때문에
        /// <c>MonoBehaviour</c> 를 새로 두지 않는다.
        /// </summary>
        public static void Tick()
        {
            if (_worn.Count == 0) return;

            // ⚠ 순회 중에 _live 가 바뀌므로 <b>복사해서</b> 돈다.
            _tickScratch.Clear();
            foreach (var kv in _worn)
                if (kv.Value != null && kv.Value.effectType == RelicEffectType.LowHpDefenseUp)
                    _tickScratch.Add(kv);
            for (int i = 0; i < _tickScratch.Count; i++)
                UpdateLowHpBonus(_tickScratch[i].Key, _tickScratch[i].Value);
        }

        static readonly List<KeyValuePair<CharacterUnit, RelicDefinitionSO>> _tickScratch =
            new List<KeyValuePair<CharacterUnit, RelicDefinitionSO>>();

        // ==================================================================
        // ② 반응 계열 — 사건에 붙는다
        // ==================================================================

        static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            DamageableUnit.OnAnyDamaged += HandleDamaged;
            DamageableUnit.OnAnyDied += HandleDied;
        }

        /// <summary>판을 갈아엎을 때 함께 부른다 — 맨 위 ⚠ 참조.</summary>
        public static void Unhook()
        {
            if (!_hooked) return;
            _hooked = false;
            DamageableUnit.OnAnyDamaged -= HandleDamaged;
            DamageableUnit.OnAnyDied -= HandleDied;
        }

        /// <summary>반사가 반사를 부르는 것을 막는 표시(맨 위 EffectType 주석).</summary>
        static bool _inThorns;

        static void HandleDamaged(DamageableUnit attacker, DamageableUnit victim,
                                  int amount, bool critical)
        {
            if (amount <= 0) return;

            // ── 흡혈 : 때린 쪽이 유물을 꼈나 ──
            if (attacker is CharacterUnit striker && striker.IsAlive)
            {
                RelicDefinitionSO r = WornBy(striker);
                if (r != null && r.effectType == RelicEffectType.Lifesteal)
                {
                    int heal = Mathf.Max(1, Mathf.RoundToInt(amount * r.value01 * 0.01f));
                    striker.Heal(heal);
                }
            }

            // ── 반사 : 맞은 쪽이 유물을 꼈나 ──
            if (_inThorns) return;
            if (!(victim is CharacterUnit hurt) || !hurt.IsAlive) return;

            RelicDefinitionSO t = WornBy(hurt);
            if (t == null || t.effectType != RelicEffectType.Thorns) return;
            if (attacker == null || !attacker.IsAlive) return;

            // ★ <b>근접만</b> 되돌린다(표의 문장). 때린 쪽의 공격 유형으로 판단한다.
            // ⚠ 공격 유형은 <see cref="UnitCombat"/> 이 들고 있다 — <see cref="DamageableUnit"/> 의
            //   <c>AttackTypeOf()</c> 는 protected 라 밖에서 못 본다.
            var attackerCombat = attacker.GetComponent<UnitCombat>();
            if (attackerCombat == null || attackerCombat.AttackType != TacticalAttackType.Melee) return;

            int back = Mathf.Max(1, Mathf.RoundToInt(amount * t.value01 * 0.01f));
            _inThorns = true;
            attacker.ApplyDamage(back);
            _inThorns = false;
        }

        static void HandleDied(DamageableUnit dead)
        {
            if (dead == null) return;

            // ── 부활 : 죽은 쪽이 유물을 꼈나 ──
            if (dead is CharacterUnit fallen)
            {
                RelicDefinitionSO own = WornBy(fallen);
                if (own != null && own.effectType == RelicEffectType.ReviveOnce &&
                    !_revivedOnce.Contains(fallen))
                {
                    _revivedOnce.Add(fallen);
                    int hp = Mathf.Max(1, Mathf.RoundToInt(fallen.MaxHp * own.value01 * 0.01f));
                    fallen.ReviveWithHp(hp);
                    UI.HudLog.Add($"{fallen.DisplayName} — 「{own.DisplayName}」 이(가) 다시 일으켰습니다.",
                                  UI.HudLogKind.Good);
                    return;
                }
            }

            // ── 처치 보상 : <b>누가 죽였는지</b> 를 이 이벤트는 알려주지 않는다.
            //    그래서 «가장 최근에 이 유닛을 때린 캐릭터» 를 쓰지 않고,
            //    <see cref="HandleKillCredit"/> 를 통해 밖에서 알려주는 길만 둔다.
        }

        /// <summary>
        /// 처치 보상 — <b>누가 죽였는지 아는 쪽</b>이 알려준다
        /// (<see cref="RelicDropService"/> 가 이미 그 정보를 들고 있다).
        ///
        /// ⚠ <c>OnAnyDied</c> 는 «누가 죽였는가» 를 싣지 않는다. 그래서 죽인 쪽을 아는
        ///   한 곳에서만 부르게 하고, 여기서 추측하지 않는다.
        /// </summary>
        public static void HandleKillCredit(CharacterUnit killer)
        {
            if (killer == null || !killer.IsAlive) return;
            RelicDefinitionSO r = WornBy(killer);
            if (r == null) return;

            switch (r.effectType)
            {
                case RelicEffectType.KillEnergy:
                    Resource.ResourceManager.Instance?.AddEnergy(Mathf.Max(0, r.value01));
                    break;
                case RelicEffectType.KillHeal:
                    killer.Heal(Mathf.Max(1, Mathf.RoundToInt(killer.MaxHp * r.value01 * 0.01f)));
                    break;
            }
        }
    }
}
