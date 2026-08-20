using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Resource;
using LastSanctuary.Units;

namespace LastSanctuary.Events
{
    /// <summary>
    /// 이벤트 보상(<c>RewardType</c> 시트)을 실제로 <b>건다</b>.
    ///
    /// ★★ <b>왜 «퍼센트» 를 «고정치» 로 바꿔 거는가</b> — 표는 전부
    /// *"{value_01}% 만큼 캐릭터의 이동 속도가 상승합니다"* 처럼 <b>퍼센트</b>로 적혀 있는데,
    /// 이 프로젝트의 능력치 보정 통로는 둘뿐이다:
    ///
    ///   · <see cref="CharacterUnit.AddStatPercentBonus"/> — <b>모든 능력치</b>에 한꺼번에
    ///   · <see cref="CharacterUnit.AddFlatStatBonus"/>    — <b>한 능력치</b>에 고정치
    ///
    /// 표는 «한 능력치에 퍼센트» 라 둘 중 어느 쪽도 그대로 맞지 않는다. 그래서
    /// <b>걸 때 한 번 환산</b>한다: <c>델타 = round(지금 능력치 × 퍼센트 ÷ 100)</c>.
    /// 그리고 <b>걸어둔 델타를 기억</b>했다가 끝날 때 정확히 같은 값을 뺀다 —
    /// 「걸었으면 반드시 되돌린다」는 <see cref="CharacterPassives"/> 의 규칙 그대로다.
    ///
    /// ⚠ 그래서 «효과가 걸린 중에 강화» 를 하면 그 퍼센트는 <b>강화 전 능력치 기준</b>으로
    ///   남는다. 표가 지속시간을 «웨이브가 끝날 때까지» 로 못박고 있어 그 사이 값이 다시
    ///   계산될 이유가 없고, 다시 계산하면 되돌릴 값이 어긋난다.
    ///
    /// ⚠⚠ <b>아직 구현하지 않은 보상은 조용히 넘어가지 않는다</b> — 로그를 남긴다.
    ///   지어내서 «비슷한 것» 을 걸면 기획이 표를 고쳐도 알 수 없게 된다.
    /// </summary>
    public static class EventRewardService
    {
        /// <summary>지금 걸려 있는 보정 하나. 끝날 때 <b>정확히 이 값</b>을 뺀다.</summary>
        class Applied
        {
            public CharacterUnit unit;
            public StatType stat;
            public int delta;
        }

        static readonly List<Applied> _live = new List<Applied>();

        /// <summary>표의 보상 타입 → 어느 능력치인가 · 올리는가 내리는가.</summary>
        static readonly Dictionary<string, (StatType stat, int sign)> StatRewards =
            new Dictionary<string, (StatType, int)>
        {
            { "char_move_spd_durat_up",             (StatType.MoveSpeed,    +1) },
            { "char_move_spd_durat_down",           (StatType.MoveSpeed,    -1) },
            { "char_atk_spd_durat_up",              (StatType.AttackSpeed,  +1) },
            { "char_atk_spd_durat_down",            (StatType.AttackSpeed,  -1) },
            { "char_melee_atk_durat_up",            (StatType.Attack,       +1) },
            { "char_melee_atk_durat_down",          (StatType.Attack,       -1) },
            { "char_long_distance_atk_durat_up",    (StatType.RangedAttack, +1) },
            { "char_long_distance_atk_durat_down",  (StatType.RangedAttack, -1) },
            { "char_magic_atk_durat_up",            (StatType.Magic,        +1) },
            { "char_magic_atk_durat_down",          (StatType.Magic,        -1) },
            { "char_hit_durat_up",                  (StatType.Accuracy,     +1) },
            { "char_hit_durat_down",                (StatType.Accuracy,     -1) },
            { "char_critical_durat_up",             (StatType.Critical,     +1) },
            { "char_critical_durat_down",           (StatType.Critical,     -1) },
            { "char_heal_durat_up",                 (StatType.Cure,         +1) },
            { "char_heal_durat_down",               (StatType.Cure,         -1) },
            { "char_def_durat_up",                  (StatType.Defense,      +1) },
            { "char_def_durat_down",                (StatType.Defense,      -1) },
            { "char_resist_durat_up",               (StatType.Resistance,   +1) },
            { "char_resist_durat_down",             (StatType.Resistance,   -1) },
        };

        /// <summary>
        /// 보상 하나를 건다. 돌려주는 문자열은 <b>전투 로그에 찍을 한 줄</b>이고,
        /// 빈 문자열이면 «아무 일도 하지 않았다» 는 뜻이다.
        /// </summary>
        public static string Apply(string rewardType, int value)
        {
            if (string.IsNullOrWhiteSpace(rewardType)) return "";

            // ── 능력치 계열 : 지속 보정 ──
            if (StatRewards.TryGetValue(rewardType, out var spec))
                return ApplyStat(spec.stat, spec.sign * Mathf.Abs(value));

            switch (rewardType)
            {
                case "energy_gain":
                    ResourceManager.Instance?.AddEnergy(Mathf.Abs(value));
                    return $"에너지 +{Mathf.Abs(value)}";

                case "energy_loss":
                {
                    // ⚠ <b>모자라면 있는 만큼만</b> 뺀다 — TrySpend 는 부족하면 «아무것도»
                    //   하지 않으므로, 그대로 쓰면 가난할 때 벌칙이 사라진다.
                    var rm = ResourceManager.Instance;
                    if (rm == null) return "";
                    int take = Mathf.Min(Mathf.Abs(value), rm.Energy);
                    if (take > 0) rm.TrySpend(take);
                    return $"에너지 −{take}";
                }

                case "nexus_percent_heal":
                case "nexus_percent_loss":
                {
                    var nexus = Object.FindFirstObjectByType<Nexus>();
                    if (nexus == null) return "";
                    int amount = Mathf.Max(1, Mathf.RoundToInt(nexus.MaxHp * Mathf.Abs(value) * 0.01f));
                    if (rewardType == "nexus_percent_heal")
                    {
                        nexus.Heal(amount);
                        return $"성역 회복 +{amount}";
                    }
                    nexus.ApplyDamage(amount);
                    return $"성역 손상 −{amount}";
                }

                default:
                    // ⚠⚠ 맨 위 ⚠⚠ — 지어내지 않고 알린다.
                    Debug.LogWarning($"[이벤트] 보상 '{rewardType}'({value}) 은 아직 구현되지 않았습니다 — " +
                                     "RewardType 시트에는 있으나 코드가 없습니다.");
                    return "";
            }
        }

        static string ApplyStat(StatType stat, int percent)
        {
            if (percent == 0) return "";

            int touched = 0;
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is CharacterUnit c) || !c.IsAlive) continue;

                int now = c.EffectiveStat(stat);
                int delta = Mathf.RoundToInt(now * percent * 0.01f);
                // ⚠ 반올림이 0 이면 <b>최소 1</b> 로 민다 — 능력치가 낮은 캐릭터에게만
                //   효과가 없어지는 것은 «전원에게 걸린다» 는 표의 문장과 어긋난다.
                if (delta == 0) delta = percent > 0 ? 1 : -1;

                c.AddFlatStatBonus(stat, delta);
                _live.Add(new Applied { unit = c, stat = stat, delta = delta });
                touched++;
            }

            if (touched == 0) return "";
            string name = StatBlock.DisplayName(stat);
            return percent > 0 ? $"{name} +{percent}% ({touched}명)" : $"{name} {percent}% ({touched}명)";
        }

        /// <summary>
        /// 걸어둔 <b>지속 보정 전부</b>를 되돌린다 — 웨이브가 끝날 때
        /// <see cref="EventService"/> 가 부른다(표의 «웨이브 이벤트가 종료될 때까지»).
        /// </summary>
        public static void ClearAll()
        {
            for (int i = 0; i < _live.Count; i++)
            {
                Applied a = _live[i];
                if (a.unit != null) a.unit.AddFlatStatBonus(a.stat, -a.delta);
            }
            _live.Clear();
        }

        /// <summary>지금 걸려 있는 보정 개수 — 디버그·로그용.</summary>
        public static int LiveCount => _live.Count;
    }
}
