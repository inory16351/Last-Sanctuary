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

            /// <summary>
            /// ★★ <b>문턱형·누적형이 붙인 것인가</b> (2026-08-24).
            ///
            /// 「두꺼워진 가피」는 체력 문턱을 넘나들 때 <b>자기 보너스만</b> 떼야 한다.
            /// 예전에는 <c>RemoveAllFor(unit)</c> 로 통째로 뗐는데, 효과 슬롯이 둘이 된
            /// 뒤로는 그러면 <b>같은 유물의 상시 보너스까지 사라진다</b>.
            /// </summary>
            public bool conditional;

            /// <summary>
            /// ★★★ <b>어느 유물이 붙였는가</b> (2026-08-26 · 칸이 셋이 되면서 필요해졌다).
            ///
            /// <b>왜</b> — 칸이 하나였을 때는 벗을 때 <c>RemoveAllFor(unit)</c> 로 그 캐릭터의
            /// 보정을 통째로 떼면 됐다. 칸이 셋이 되면 그 한 줄이 <b>같이 끼고 있던 나머지 둘의
            /// 보너스까지 지운다</b> — «유물 하나를 벗었더니 능력치가 세 개만큼 빠지는» 고장이다.
            /// 그래서 붙인 주인을 적어 두고 <b>그 유물 것만</b> 뗀다.
            /// </summary>
            public RelicDefinitionSO owner;
        }

        static readonly List<Applied> _live = new List<Applied>();

        /// <summary>
        /// 지금 유물을 낀 캐릭터들 — 반응 계열이 «누가 무엇을 꼈나» 를 물을 때 쓴다.
        /// ★★★ 2026-08-26 — 칸이 셋이 되어 <b>값이 목록</b>이 됐다. 빈 목록은 두지 않는다
        ///   (다 벗으면 항목째 지운다) — 그래야 <see cref="Tick"/>·웨이브 순회가 헛돌지 않는다.
        /// </summary>
        static readonly Dictionary<CharacterUnit, List<RelicDefinitionSO>> _worn =
            new Dictionary<CharacterUnit, List<RelicDefinitionSO>>();

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

            if (!_worn.TryGetValue(unit, out List<RelicDefinitionSO> list))
            {
                list = new List<RelicDefinitionSO>(3);
                _worn[unit] = list;
            }
            if (!list.Contains(relic)) list.Add(relic);

            // ★★ 2026-08-24 — <b>효과 슬롯 둘</b>을 다 돈다(표 Ver02).
            foreach (var (type, v1, _) in relic.Effects())
            {
                // ★★ 표 Ver02 — 능력치는 <b>정수 그대로</b> 더한다(%가 아니다).
                if (StatEffects.TryGetValue(type, out StatType stat))
                    AddStat(unit, stat, v1, relic);

                // 시야는 능력치가 아니라 <see cref="VisionSource"/> 의 칸이다 — 따로 민다.
                else if (type == RelicEffectType.VisionUp)
                    ScaleVision(unit, 1f + Mathf.Abs(v1) * 0.01f);
            }

            // ★ 「두꺼워진 가피」는 <b>지금 체력에 따라</b> 붙었다 떨어진다 — 붙일지 여기서 한 번 본다.
            UpdateLowHpBonus(unit, relic);
        }

        public static void OnUnequipped(CharacterUnit unit, RelicDefinitionSO relic)
        {
            if (unit == null) return;

            // ★★★ <b>그 유물이 붙인 것만</b> 뗀다 — Applied.owner 의 주석 참조.
            RemoveOwnedBy(unit, relic);

            if (_worn.TryGetValue(unit, out List<RelicDefinitionSO> list))
            {
                list.Remove(relic);
                if (list.Count == 0) _worn.Remove(unit);
            }

            if (relic != null)
                foreach (var (type, v1, _) in relic.Effects())
                    if (type == RelicEffectType.VisionUp)
                        ScaleVision(unit, 1f / Mathf.Max(0.01f, 1f + Mathf.Abs(v1) * 0.01f));

            // ⚠ 「심장에 박힌 가시」의 누적 장부는 <b>그 유물을 벗을 때만</b> 지운다 —
            //   다른 칸을 벗었다고 상한이 초기화되면 벗었다 끼우기로 무한 성장이 된다.
            if (relic != null && HasEffect(relic, RelicEffectType.KillGrowth))
                _killGrowth.Remove(unit);
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
            _killGrowth.Clear();
        }

        /// <summary>
        /// 이 캐릭터가 낀 유물의 <b>첫 칸</b>. 없으면 null.
        /// ⚠ 칸이 셋이 된 뒤로 이것은 «대표 하나» 다 — 효과를 물을 때는 반드시
        ///   <see cref="ValueOf"/>·<see cref="TryValueOf"/> 를 쓸 것(그쪽이 세 칸을 다 본다).
        /// </summary>
        public static RelicDefinitionSO WornBy(CharacterUnit unit) =>
            unit != null && _worn.TryGetValue(unit, out var list) && list.Count > 0 ? list[0] : null;

        /// <summary>이 캐릭터가 낀 유물 전부. 없으면 null (빈 목록을 만들지 않는다).</summary>
        public static List<RelicDefinitionSO> WornList(CharacterUnit unit) =>
            unit != null && _worn.TryGetValue(unit, out var list) ? list : null;

        /// <summary>그 유물이 이 효과를 가지고 있는가.</summary>
        static bool HasEffect(RelicDefinitionSO relic, RelicEffectType want)
        {
            if (relic == null) return false;
            foreach (var (type, _, _) in relic.Effects())
                if (type == want) return true;
            return false;
        }

        /// <summary>
        /// 이 캐릭터의 <b>발굴 속도 배율</b>. <see cref="RelicEffectType.DigSpeed"/> 를
        /// 낀 캐릭터만 1 보다 크다. <see cref="RelicDigService"/> 가 매 프레임 묻는다.
        /// </summary>
        public static float DigSpeedMultiplier(CharacterUnit unit) =>
            1f + Mathf.Abs(ValueOf(unit, RelicEffectType.DigSpeed)) * 0.01f;

        /// <summary>
        /// 이 캐릭터가 낀 유물의 <b>두 슬롯</b>에서 <paramref name="want"/> 를 찾아 v1 을 돌려준다.
        /// 없으면 0. — 슬롯이 둘이 된 뒤로 «첫 칸만 보는» 코드가 조용히 틀리기 때문에 한 곳으로 모았다.
        /// </summary>
        public static int ValueOf(CharacterUnit unit, RelicEffectType want)
        {
            // ★★★ 2026-08-26 — <b>세 칸을 다 돌고 «더한다»</b>.
            //   유저 확정: *"수치 그대로 — 3칸은 그대로 3배"*. 흡혈 5% 짜리 둘을 끼면 10% 다.
            //   ⚠ 예전에는 «첫 칸의 첫 일치» 하나만 돌려줬다 — 칸이 늘어난 채로 그대로 두면
            //     두 번째·세 번째 칸의 효과가 <b>조용히 사라진다</b>.
            List<RelicDefinitionSO> list = WornList(unit);
            if (list == null) return 0;

            int sum = 0;
            for (int i = 0; i < list.Count; i++)
            {
                RelicDefinitionSO r = list[i];
                if (r == null) continue;
                foreach (var (type, v1, _) in r.Effects())
                    if (type == want) sum += v1;
            }
            return sum;
        }

        /// <summary>위와 같되 <b>보조 수치</b>(v2)까지 함께. 없으면 <c>false</c>.</summary>
        public static bool TryValueOf(CharacterUnit unit, RelicEffectType want, out int v1, out int v2)
        {
            v1 = v2 = 0;

            // ★ 짝(v1,v2)은 <b>더하지 않는다</b> — v2 는 «문턱»·«초»·«상한» 이라 더하면 뜻이 깨진다
            //   (체력 30% 아래 + 체력 50% 아래 = 체력 80% 아래가 아니다).
            //   그래서 <b>v1 이 가장 큰 하나</b>를 고른다 — «가장 센 것이 이긴다» 가
            //   이 게임의 다른 중첩 규칙(패시브 보정)과 같은 결이다.
            List<RelicDefinitionSO> list = WornList(unit);
            if (list == null) return false;

            bool found = false;
            for (int i = 0; i < list.Count; i++)
            {
                RelicDefinitionSO r = list[i];
                if (r == null) continue;
                foreach (var (type, a, b) in r.Effects())
                {
                    if (type != want) continue;
                    if (found && a <= v1) continue;
                    v1 = a; v2 = b; found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// 이 캐릭터의 <b>침식 상승 배율</b>. <see cref="RelicEffectType.ErosionSlow"/> 를
        /// 낀 캐릭터만 1 보다 작다. <see cref="CharacterErosion.Tick"/> 이 곱한다.
        /// </summary>
        public static float ErosionGainMultiplier(CharacterUnit unit)
        {
            return Mathf.Clamp01(1f - Mathf.Abs(ValueOf(unit, RelicEffectType.ErosionSlow)) * 0.01f);
        }

        // ==================================================================
        // ① 능력치 — 더하고 빼기
        // ==================================================================

        /// <summary>
        /// ★★ <b>2026-08-24 (표 Ver02) — 표의 값을 «그대로» 더한다.</b>
        ///
        /// 예전에는 «지금 능력치의 v1%» 를 환산했다. 이 게임의 능력치는 한 자리 수라
        /// (체력 2~12 · 근거리 1~10) 그 방식은 ① <b>부익부</b>(높은 캐릭터가 더 받는다)이고
        /// ② 낮은 캐릭터는 반올림에서 +1 로 뭉개졌다. 정수로 주면 누구에게 붙여도 같다.
        ///
        /// ★ <see cref="CharacterUnit.AddFlatStatBonus"/> 는 <b>능력치 상한(100)을 넘긴다</b> —
        ///   표 <c>EffectType</c> 시트가 «상한을 초월합니다» 라고 못박은 그 통로다.
        /// </summary>
        static void AddStat(CharacterUnit unit, StatType stat, int amount,
                            RelicDefinitionSO owner, bool conditional = false)
        {
            if (amount == 0 || unit == null) return;

            unit.AddFlatStatBonus(stat, amount);
            _live.Add(new Applied
            {
                unit = unit, stat = stat, delta = amount,
                conditional = conditional, owner = owner,
            });
        }

        /// <summary>
        /// ★★★ <b>그 유물이 붙인 보정만</b> 되돌린다 (2026-08-26 · 칸이 셋).
        /// <paramref name="relic"/> 이 null 이면 <b>주인 없는 것</b>(옛 경로)만 뗀다 —
        /// 아무거나 떼면 다른 칸의 보너스가 함께 사라진다.
        /// </summary>
        static void RemoveOwnedBy(CharacterUnit unit, RelicDefinitionSO relic)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i].unit != unit) continue;
                if (_live[i].owner != relic) continue;
                if (unit != null) unit.AddFlatStatBonus(_live[i].stat, -_live[i].delta);
                _live.RemoveAt(i);
            }
        }

        /// <summary>
        /// 이 캐릭터에게 걸어둔 보정을 되돌린다.
        /// <paramref name="onlyConditional"/> 이 참이면 <b>문턱형이 붙인 것만</b> 뗀다 —
        /// 그러지 않으면 「두꺼워진 가피」가 문턱을 넘을 때마다 <b>같은 유물의 상시 보너스까지</b>
        /// 지운다(효과 슬롯이 둘이 된 뒤로 실제로 그렇게 된다).
        /// </summary>
        static void RemoveAllFor(CharacterUnit unit, bool onlyConditional = false,
                                 RelicDefinitionSO owner = null)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i].unit != unit) continue;
                if (onlyConditional && !_live[i].conditional) continue;
                // ★ 주인을 지정했으면 그 유물 것만 — 칸이 셋이라 문턱형도 유물마다 따로 붙는다.
                if (owner != null && _live[i].owner != owner) continue;
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

            int bonus = 0, threshold = 0;
            foreach (var (type, v1, v2) in relic.Effects())
                if (type == RelicEffectType.LowHpDefenseUp) { bonus = v1; threshold = v2; }
            if (bonus == 0) return;

            bool wantOn = unit.IsAlive &&
                          unit.CurrentHp * 100 <= unit.MaxHp * Mathf.Max(1, threshold);

            // ★ <b>이 유물이</b> 붙여 둔 문턱형이 있는지만 본다 — 칸이 셋이면 같은 캐릭터에게
            //   문턱형이 둘 붙어 있을 수 있고, 그때 «누가 켜져 있나» 를 섞으면 한쪽이 영영 안 꺼진다.
            bool isOn = false;
            for (int i = 0; i < _live.Count; i++)
                if (_live[i].unit == unit && _live[i].conditional && _live[i].owner == relic)
                { isOn = true; break; }

            if (wantOn == isOn) return;
            if (wantOn) AddStat(unit, StatType.Defense, bonus, relic, conditional: true);
            else RemoveAllFor(unit, onlyConditional: true, owner: relic);
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
            {
                List<RelicDefinitionSO> list = kv.Value;
                for (int i = 0; i < list.Count; i++)
                    if (HasEffect(list[i], RelicEffectType.LowHpDefenseUp))
                        _tickScratch.Add(new KeyValuePair<CharacterUnit, RelicDefinitionSO>(kv.Key, list[i]));
            }
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
                int steal = ValueOf(striker, RelicEffectType.Lifesteal);

                // ★ 「최후의 발버둥」 — 체력이 문턱 아래일 때만 켜지는 흡혈(2026-08-24).
                if (TryValueOf(striker, RelicEffectType.LowHpLifesteal, out int lowPct, out int gate) &&
                    striker.MaxHp > 0 && striker.CurrentHp * 100 <= striker.MaxHp * Mathf.Max(1, gate))
                    steal += lowPct;

                if (steal > 0) striker.Heal(Mathf.Max(1, Mathf.RoundToInt(amount * steal * 0.01f)));
            }

            // ── 반사 : 맞은 쪽이 유물을 꼈나 ──
            if (_inThorns) return;
            if (!(victim is CharacterUnit hurt) || !hurt.IsAlive) return;

            int thorns = ValueOf(hurt, RelicEffectType.Thorns);
            if (thorns <= 0) return;
            if (attacker == null || !attacker.IsAlive) return;

            // ★ <b>근접만</b> 되돌린다(표의 문장). 때린 쪽의 공격 유형으로 판단한다.
            // ⚠ 공격 유형은 <see cref="UnitCombat"/> 이 들고 있다 — <see cref="DamageableUnit"/> 의
            //   <c>AttackTypeOf()</c> 는 protected 라 밖에서 못 본다.
            var attackerCombat = attacker.GetComponent<UnitCombat>();
            if (attackerCombat == null || attackerCombat.AttackType != TacticalAttackType.Melee) return;

            int back = Mathf.Max(1, Mathf.RoundToInt(amount * thorns * 0.01f));
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
                // ★ 칸이 셋이므로 «첫 칸» 이 아니라 <b>부활을 가진 칸</b>을 찾는다 —
                //   안 그러면 2·3번 칸의 부활 유물이 이름만 엉뚱하게 찍힌다.
                RelicDefinitionSO own = RelicWith(fallen, RelicEffectType.ReviveOnce);
                int revivePct = ValueOf(fallen, RelicEffectType.ReviveOnce);
                if (own != null && revivePct > 0 && !_revivedOnce.Contains(fallen))
                {
                    _revivedOnce.Add(fallen);
                    int hp = Mathf.Max(1, Mathf.RoundToInt(fallen.MaxHp * revivePct * 0.01f));
                    fallen.ReviveWithHp(hp);
                    // ⚠ 이 파일에는 using LastSanctuary.UI 가 없다 — 상대 참조 그대로 쓴다.
                    UI.HudLog.Add(string.Format(
                                      UI.HudTheme.T("log_relic_revived",
                                                    "{0} — 「{1}」 이(가) 다시 일으켰습니다."),
                                      fallen.DisplayName, own.DisplayName),
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
            List<RelicDefinitionSO> worn = WornList(killer);
            if (worn == null) return;

            // ★★★ 2026-08-26 — <b>낀 유물 셋을</b> 다 돌고, 유물마다 <b>효과 슬롯 둘</b>을 다 본다.
            //   예전에는 «낀 유물 하나 × 효과 둘» 이었다.
            for (int w = 0; w < worn.Count; w++)
            {
            RelicDefinitionSO r = worn[w];
            if (r == null) continue;

            foreach (var (type, v1, v2) in r.Effects())
            {
                switch (type)
                {
                    case RelicEffectType.KillEnergy:
                    {
                        // ★★ 2026-08-25 — <b>확률 칸(v2)이 생겼다</b> (유저 지시: *"자원 획득량이
                        //   너무 사기니까 좀 조정해"* → *"%가 더 사기일듯 걍 자원 획득량을 좀
                        //   줄이거나 «확률»을 넣어"*).
                        //
                        //   ★ <b>%(비율) 로 안 바꿨다</b> — 이 게임의 처치 보상은 웨이브 잡몹 10 ·
                        //     중립 200~1200 으로 <b>대역이 60배 넘게 벌어진다</b>. 비율로 주면
                        //     중립을 사냥할수록 값이 같이 불어나 «기하급수» 문제가 유물로 옮겨올
                        //     뿐이다(NeutralGrowthService 의 ★★★). 절대값 + 확률이면
                        //     <b>어떤 적을 잡아도 기댓값이 같다</b>.
                        //   ⚠ <b>v2 가 0 이면 항상 터진다</b> — 확률 칸이 없던 시절의 표와 호환된다.
                        int chance = Mathf.Clamp(v2, 0, 100);
                        if (chance > 0 && Random.Range(0, 100) >= chance) break;

                        Resource.ResourceManager.Instance?.AddEnergy(Mathf.Max(0, v1));
                        break;
                    }

                    case RelicEffectType.KillHeal:
                        killer.Heal(Mathf.Max(1, Mathf.RoundToInt(killer.MaxHp * v1 * 0.01f)));
                        break;

                    case RelicEffectType.KillErosionDown:
                    {
                        var er = CharacterErosion.Of(killer);
                        if (er != null) er.AddErosion(-Mathf.Abs(v1));
                        break;
                    }

                    case RelicEffectType.KillGrowth:
                    {
                        // ⚠ <b>상한이 있다</b>(v2 회) — 없으면 40웨이브 동안 무한히 쌓인다.
                        _killGrowth.TryGetValue(killer, out int done);
                        if (done >= Mathf.Max(1, v2)) break;
                        _killGrowth[killer] = done + 1;
                        AddStat(killer, StatType.Attack, v1, r);
                        break;
                    }
                }
            }
            }
        }

        /// <summary>
        /// 이 캐릭터가 낀 유물 중 <paramref name="want"/> 효과를 가진 <b>첫 하나</b>.
        /// 「무엇이 그랬는지」를 화면에 적어야 할 때 쓴다(부활 로그 등).
        /// </summary>
        static RelicDefinitionSO RelicWith(CharacterUnit unit, RelicEffectType want)
        {
            List<RelicDefinitionSO> list = WornList(unit);
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++)
                if (HasEffect(list[i], want)) return list[i];
            return null;
        }

        /// <summary>「심장에 박힌 가시」가 이 캐릭터에게 이미 몇 번 쌓였나 (상한 검사용).</summary>
        static readonly Dictionary<CharacterUnit, int> _killGrowth =
            new Dictionary<CharacterUnit, int>();

        // ==================================================================
        // ③ 웨이브 계열 — «판의 흐름» 에 붙는다 (2026-08-24 신설)
        //
        // ⚠ <see cref="Wave.WaveManager"/> 의 이벤트는 <b>정적이 아니라 인스턴스</b> 것이라
        //   <see cref="Hook"/> 에서 붙일 수 없다(그 시점에 매니저가 없을 수 있다).
        //   그래서 <b>매니저가 스스로 부르지 않고</b>, 이미 매 프레임 도는
        //   <see cref="RelicDigService"/> 가 웨이브 번호가 바뀌는 것을 보고 알려준다 —
        //   「두꺼워진 가피」를 <see cref="Tick"/> 에 얹은 것과 같은 이유다.
        // ==================================================================

        /// <summary>웨이브의 몬스터가 <b>소환됐다</b> — 보호막 계열이 여기서 붙는다.</summary>
        public static void OnWaveSpawned()
        {
            foreach (var kv in _worn)
            {
                CharacterUnit unit = kv.Key;
                if (unit == null || !unit.IsAlive) continue;
                if (!TryValueOf(unit, RelicEffectType.WaveShield, out int pct, out int seconds)) continue;

                int amount = Mathf.Max(1, Mathf.RoundToInt(unit.MaxHp * pct * 0.01f));
                unit.GrantShield(amount, Mathf.Max(1, seconds));
            }
        }

        /// <summary>웨이브가 <b>끝났다</b> — 에너지·회복 계열이 여기서 들어온다.</summary>
        public static void OnWaveEnded()
        {
            foreach (var kv in _worn)
            {
                CharacterUnit unit = kv.Key;
                if (unit == null || !unit.IsAlive) continue;

                List<RelicDefinitionSO> list = kv.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    RelicDefinitionSO r = list[i];
                    if (r == null) continue;

                    foreach (var (type, v1, _) in r.Effects())
                    {
                        if (type == RelicEffectType.WaveEnergy)
                            Resource.ResourceManager.Instance?.AddEnergy(Mathf.Max(0, v1));
                        else if (type == RelicEffectType.WaveHeal)
                            unit.Heal(Mathf.Max(1, Mathf.RoundToInt(unit.MaxHp * v1 * 0.01f)));
                    }
                }
            }
        }
    }
}
