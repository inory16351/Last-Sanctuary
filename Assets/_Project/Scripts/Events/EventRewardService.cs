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
        /// <summary>
        /// 지금 걸려 있는 보정 하나. 끝날 때 <b>정확히 이 값</b>을 뺀다.
        ///
        /// ★★ <b>Ver013 — <see cref="expiresAt"/> 이 생겼다</b> (2026-08-21). 옛 표는
        /// 지속시간이 «이벤트가 끝날 때까지» 라는 <b>상대값</b>이라 <see cref="ClearAll"/>
        /// 한 번으로 전부 걷었다. Ver013 은 <b>효과마다 초</b>를 적으므로
        /// (<c>reward_duration_01/02</c>) 보정이 <b>각자 다른 시각에</b> 풀린다.
        /// </summary>
        class Applied
        {
            public CharacterUnit unit;
            public StatType stat;
            public int delta;

            /// <summary>이 보정이 풀리는 시각(<c>Time.time</c>). <b>0 이면 시간으로는 안 풀린다</b>.</summary>
            public float expiresAt;
        }

        static readonly List<Applied> _live = new List<Applied>();

        // ==================================================================
        // ★★ <b>능력치가 아닌 지속 효과</b> — 2026-08-24 신설
        //
        // <b>왜 필요했나</b> — 유저 지시 *"이벤트의 효과를 자원생성보다 더욱 다양화 하여
        // 배치"*. 표(RewardType 시트)에는 처음부터 51종이 있었는데 <b>코드가 아는 것은
        // 31종</b>뿐이었고(능력치 20 + 체력 2 + 침식 4 + 처치기록 + 에너지 2 + 성역 2),
        // 나머지 20종은 <see cref="Apply"/> 의 <c>default</c> 로 빠져 <b>경고만 찍고
        // 아무 일도 하지 않았다</b> — 선택지 161칸 가운데 31칸이 «누르면 아무 일도
        // 안 일어나는 칸» 이었다. 그래서 표를 아무리 다양하게 짜도 플레이어가 실제로
        // 체감하는 것은 «에너지와 능력치» 뿐이었다. 표를 고치기 <b>전에</b> 이것을 먼저
        // 메꾼다 — 그러지 않으면 «다양화» 가 화면에 나타나지 않는다.
        //
        // <b>어떻게</b> — 보호막·시야·사거리·회복 증폭처럼 <see cref="StatType"/> 이 아닌
        // 효과는 «되돌리는 방법» 이 저마다 다르다. 그래서 <b>되돌리는 일 자체</b>를
        // 델리게이트로 들고 있는 목록을 하나 둔다. 「걸었으면 반드시 되돌린다」는
        // <see cref="CharacterPassives"/> 의 규칙은 그대로다.
        //
        // ⚠ <b>스스로 시간이 지나면 풀리는 효과는 여기 넣지 않는다</b> —
        //   보호막(GrantShield) · 화상(ApplyBurn) · 구속(ApplyBind) · 허약(ApplyWeaken) ·
        //   부식(ApplyCorrosion) · 이벤트 속도 보정은 <b>자기 만료 시각</b>을 갖고 있다.
        //   같은 것을 두 곳에서 되돌리면 값이 두 번 빠진다.
        // ==================================================================

        /// <summary>시간이 되면 <see cref="revert"/> 를 부르는 예약 하나.</summary>
        class Timed
        {
            public float expiresAt;
            public System.Action revert;
        }

        static readonly List<Timed> _timed = new List<Timed>();

        /// <summary>
        /// <paramref name="revert"/> 를 <paramref name="seconds"/> 초 뒤에 부르도록 예약한다.
        /// 0 이하면 <b>예약하지 않는다</b> — 즉시 효과이거나 «되돌리지 않는» 효과다.
        /// </summary>
        static void ScheduleRevert(float seconds, System.Action revert)
        {
            if (revert == null || seconds <= 0f) return;
            _timed.Add(new Timed { expiresAt = Time.time + seconds, revert = revert });
        }

        /// <summary>지금 살아 있는 캐릭터 전부(소환수 제외 여부는 인자로).</summary>
        static List<CharacterUnit> Characters(bool includeSummoned)
        {
            var list = new List<CharacterUnit>();
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] is CharacterUnit c && c.IsAlive && (includeSummoned || !c.IsSummoned))
                    list.Add(c);
            return list;
        }

        /// <summary>
        /// 지금 전장에 살아 있는 <b>웨이브 몬스터</b> 전부.
        ///
        /// ⚠ <b>중립은 넣지 않는다</b> — 표가 전부 «웨이브 몬스터 전체» 라고 적고 있다.
        ///   중립까지 걸면 «성역 밖의 서식지에 있는 에픽» 이 이벤트 한 번에 묶이거나
        ///   불타는데, 그것은 사냥의 난이도를 이벤트가 결정한다는 뜻이 된다.
        /// </summary>
        static List<MonsterUnit> WaveMonsters()
        {
            var list = new List<MonsterUnit>();
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] is MonsterUnit m && m.IsAlive) list.Add(m);
            return list;
        }

        /// <summary>
        /// ★★ <b>지속시간이 다 된 보정을 되돌린다</b> (Ver013 · <see cref="EventService"/> 가
        /// 매 프레임 부른다).
        ///
        /// <b>왜 이 함수가 생겼나</b> — Info 시트: *"지속시간을 «이벤트가 끝날 때까지» 같은
        /// 상대값으로 두지 않고 초로 못박은 이유 — 이제 이벤트가 웨이브 «종료 시» 에 뜨기
        /// 때문에 «이벤트 종료» 라는 기준점이 사라졌습니다"*. 즉 <b>창을 닫아도 효과는 남고</b>,
        /// 각자의 초가 지나면 하나씩 풀린다.
        ///
        /// ⚠ <b>뒤에서부터</b> 지운다 — 앞에서 지우면 인덱스가 밀려 하나를 건너뛴다.
        /// </summary>
        public static void Tick()
        {
            float now = Time.time;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Applied a = _live[i];
                if (a.expiresAt <= 0f || now < a.expiresAt) continue;

                if (a.unit != null) a.unit.AddFlatStatBonus(a.stat, -a.delta);
                _live.RemoveAt(i);
            }

            // ★ 능력치가 아닌 지속 효과(시야·사거리·회복 증폭 …) — 위 ★★ 참조.
            for (int i = _timed.Count - 1; i >= 0; i--)
            {
                if (now < _timed[i].expiresAt) continue;
                _timed[i].revert?.Invoke();
                _timed.RemoveAt(i);
            }
        }

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
        public static string Apply(string rewardType, int value) => Apply(rewardType, value, 0);

        /// <summary>
        /// 보상 하나를 <paramref name="durationSeconds"/> 초 동안 건다 (Ver013).
        /// 0 이면 «시간으로는 안 풀린다» — 즉시 효과이거나 <see cref="ClearAll"/> 만 걷는다.
        /// </summary>
        public static string Apply(string rewardType, int value, int durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(rewardType)) return "";

            // ── 능력치 계열 : 지속 보정 ──
            if (StatRewards.TryGetValue(rewardType, out var spec))
                return ApplyStat(spec.stat, spec.sign * Mathf.Abs(value), durationSeconds);

            switch (rewardType)
            {
                // ── 체력 % (즉시) ──
                case "char_hp_percent_heal":
                case "char_hp_percent_loss":
                {
                    bool heal = rewardType == "char_hp_percent_heal";
                    int touched = 0;
                    var all = UnitRegistry.All;
                    for (int i = 0; i < all.Count; i++)
                    {
                        if (!(all[i] is CharacterUnit c) || !c.IsAlive) continue;
                        int amount = Mathf.Max(1, Mathf.RoundToInt(c.MaxHp * Mathf.Abs(value) * 0.01f));
                        if (heal) c.Heal(amount);
                        else
                        {
                            // ⚠ 표: *"이 피해로는 사망하지 않습니다(체력 1 에서 멈춤)"*.
                            //   ApplyDamage 를 그대로 쓰면 죽으므로 <b>체력 1 을 남기고</b> 깎는다.
                            int safe = Mathf.Min(amount, Mathf.Max(0, c.CurrentHp - 1));
                            if (safe > 0) c.ApplyDamage(safe);
                        }
                        touched++;
                    }
                    if (touched == 0) return "";
                    // ★ 회복과 손실은 «부호» 하나만 다른 같은 문장이라 <b>키를 하나로 묶는다</b> —
                    //   부호를 인자로 넘긴다(두 키로 두면 번역이 갈라질 자리가 공짜로 하나 늘어난다).
                    return string.Format(
                        UI.HudTheme.T("ui_reward_hp_percent", "체력 {0}% ({1}명)"),
                        heal ? $"+{value}" : $"−{value}", touched);
                }

                // ── 침식 (즉시 · 절대값) ──
                case "char_erosion_up":
                case "char_erosion_down":
                case "char_all_erosion_up":
                case "char_all_erosion_down":
                {
                    bool up = rewardType.EndsWith("_up");
                    bool everyone = rewardType.StartsWith("char_all_");
                    float delta = up ? Mathf.Abs(value) : -Mathf.Abs(value);

                    var targets = new List<CharacterUnit>();
                    var all = UnitRegistry.All;
                    for (int i = 0; i < all.Count; i++)
                        if (all[i] is CharacterUnit c && c.IsAlive && !c.IsSummoned) targets.Add(c);
                    if (targets.Count == 0) return "";

                    if (!everyone)
                    {
                        // 표: *"랜덤 캐릭터 1명"*
                        CharacterUnit one = targets[Random.Range(0, targets.Count)];
                        targets.Clear();
                        targets.Add(one);
                    }

                    int hit = 0;
                    for (int i = 0; i < targets.Count; i++)
                    {
                        var er = CharacterErosion.Of(targets[i]);
                        if (er == null) continue;
                        er.AddErosion(delta);
                        hit++;
                    }
                    if (hit == 0) return "";
                    // ★ 위 «체력» 과 같은 판단 — 오르내림은 부호만 다르므로 키 하나.
                    return string.Format(
                        UI.HudTheme.T("ui_reward_erosion", "침식 {0} ({1}명)"),
                        up ? $"+{Mathf.Abs(value)}" : $"−{Mathf.Abs(value)}", hit);
                }

                // ── 처치 기록 부여 (즉시) ──
                case "char_kill_grant":
                {
                    var pool = new List<CharacterUnit>();
                    var all = UnitRegistry.All;
                    for (int i = 0; i < all.Count; i++)
                        if (all[i] is CharacterUnit c && c.IsAlive && !c.IsSummoned) pool.Add(c);
                    if (pool.Count == 0) return "";

                    CharacterUnit pick = pool[Random.Range(0, pool.Count)];
                    var kills = CharacterKills.EnsureOn(pick);
                    if (kills == null) return "";
                    int n = Mathf.Max(1, Mathf.Abs(value));
                    for (int i = 0; i < n; i++) kills.AddKill();
                    return string.Format(
                        UI.HudTheme.T("ui_reward_kill_grant", "{0} 처치 기록 +{1}"), pick.DisplayName, n);
                }

                case "energy_gain":
                    ResourceManager.Instance?.AddEnergy(Mathf.Abs(value));
                    // ★ 아래 energy_loss 와 <b>같은 키</b> — «에너지 {부호}{수} » 한 문장이다.
                    return string.Format(
                        UI.HudTheme.T("ui_reward_energy", "에너지 {0}"), $"+{Mathf.Abs(value)}");

                case "energy_loss":
                {
                    // ⚠ <b>모자라면 있는 만큼만</b> 뺀다 — TrySpend 는 부족하면 «아무것도»
                    //   하지 않으므로, 그대로 쓰면 가난할 때 벌칙이 사라진다.
                    var rm = ResourceManager.Instance;
                    if (rm == null) return "";
                    int take = Mathf.Min(Mathf.Abs(value), rm.Energy);
                    if (take > 0) rm.TrySpend(take);
                    // ★ energy_gain 과 같은 키를 쓴다(위 참조).
                    return string.Format(
                        UI.HudTheme.T("ui_reward_energy", "에너지 {0}"), $"−{take}");
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
                        return string.Format(
                            UI.HudTheme.T("ui_reward_nexus_heal", "성역 회복 +{0}"), amount);
                    }
                    nexus.ApplyDamage(amount);
                    // ⚠ «회복» 과 «손상» 은 부호가 아니라 <b>낱말</b>이 다르므로 묶지 않는다.
                    return string.Format(
                        UI.HudTheme.T("ui_reward_nexus_damage", "성역 손상 −{0}"), amount);
                }

                // ══════════════════════════════════════════════════════════
                //  아래부터 2026-08-24 신설 — 위 ★★ 「능력치가 아닌 지속 효과」 참조
                // ══════════════════════════════════════════════════════════

                // ── 보호막 (자기 만료 · 되돌릴 것 없음) ──
                case "char_shield_gain":
                {
                    var chars = Characters(includeSummoned: true);
                    int touched = 0;
                    for (int i = 0; i < chars.Count; i++)
                    {
                        int amount = Mathf.Max(1, Mathf.RoundToInt(chars[i].MaxHp * Mathf.Abs(value) * 0.01f));
                        // ⚠ 지속시간이 0 이면 표가 «초» 를 안 적은 것이다 — 그때는 한 웨이브
                        //   분량(120초)으로 둔다. 0 을 그대로 넘기면 GrantShield 가 아무 일도 안 한다.
                        chars[i].GrantShield(amount, durationSeconds > 0 ? durationSeconds : 120f);
                        touched++;
                    }
                    if (touched == 0) return "";
                    return string.Format(
                        UI.HudTheme.T("ui_reward_shield", "보호막 최대 체력의 {0}% ({1}명)"),
                        Mathf.Abs(value), touched);
                }

                // ── 받는 회복량 증폭 (되돌려야 한다) ──
                case "char_heal_receive_up":
                {
                    var chars = Characters(includeSummoned: true);
                    int percent = Mathf.Abs(value);
                    int touched = 0;
                    for (int i = 0; i < chars.Count; i++)
                    {
                        CharacterUnit c = chars[i];
                        c.AddHealReceivedPercent(percent);
                        ScheduleRevert(durationSeconds, () =>
                        {
                            if (c != null) c.AddHealReceivedPercent(-percent);
                        });
                        touched++;
                    }
                    if (touched == 0) return "";
                    // ⚠ «{n}초» 꼬리를 <b>따로 이어 붙이지 않는다</b> — 영어는 그 자리가
                    //   «for 30s» 라 낱말이 붙어야 한다. 그래서 지속시간이 있고 없고를
                    //   <b>문장 두 벌</b>로 나눈다(아래 사거리·시야·능력치도 같은 방식).
                    return durationSeconds > 0
                        ? string.Format(
                            UI.HudTheme.T("ui_reward_heal_received_timed", "받는 회복량 +{0}% {1}초 ({2}명)"),
                            percent, durationSeconds, touched)
                        : string.Format(
                            UI.HudTheme.T("ui_reward_heal_received", "받는 회복량 +{0}% ({1}명)"),
                            percent, touched);
                }

                // ── 정신 이상 해제 (즉시) ──
                case "char_mental_cure":
                {
                    // 표: *"정신 이상이 발동 중인 캐릭터 {value_01}명"*. 침식 수치는 안 건드린다.
                    var chars = Characters(includeSummoned: false);
                    int want = Mathf.Max(1, Mathf.Abs(value));
                    int cured = 0;
                    for (int i = 0; i < chars.Count && cured < want; i++)
                    {
                        var er = CharacterErosion.Of(chars[i]);
                        if (er == null || !er.HasActive) continue;
                        er.ClearActiveExternally();
                        cured++;
                    }
                    // ⚠ 아무도 정신 이상이 아니면 <b>아무 일도 안 일어난다</b> — 그것이 표의 뜻이다.
                    //   억지로 침식을 깎아 «비슷한 것» 을 주지 않는다.
                    return cured == 0 ? "" : string.Format(
                        UI.HudTheme.T("ui_reward_mental_cure", "정신 이상 해제 ({0}명)"), cured);
                }

                // ── 공격 사거리 (되돌려야 한다) ──
                case "char_range_durat_up":
                case "char_range_durat_down":
                {
                    // ★ 사거리는 능력치가 아니라 <b>공격 유형이 정하는 값</b>이라
                    //   (UnitCombat.EffectiveAttackRange) StatType 통로가 없다. 그래서 지금
                    //   사거리의 퍼센트만큼을 «보너스 타일» 로 환산해 더하고, 끝날 때 뺀다.
                    int sign = rewardType.EndsWith("_up") ? 1 : -1;
                    float percent = sign * Mathf.Abs(value);
                    var chars = Characters(includeSummoned: true);
                    int touched = 0;
                    for (int i = 0; i < chars.Count; i++)
                    {
                        var combat = chars[i].GetComponent<UnitCombat>();
                        if (combat == null) continue;
                        float delta = combat.EffectiveAttackRange * percent * 0.01f;
                        if (Mathf.Abs(delta) < 0.01f) continue;
                        combat.AddAttackRangeBonus(delta);
                        ScheduleRevert(durationSeconds, () =>
                        {
                            if (combat != null) combat.AddAttackRangeBonus(-delta);
                        });
                        touched++;
                    }
                    if (touched == 0) return "";
                    return durationSeconds > 0
                        ? string.Format(
                            UI.HudTheme.T("ui_reward_range_timed", "공격 사거리 {0}% {1}초 ({2}명)"),
                            Signed(percent), durationSeconds, touched)
                        : string.Format(
                            UI.HudTheme.T("ui_reward_range", "공격 사거리 {0}% ({1}명)"),
                            Signed(percent), touched);
                }

                // ── 시야 (되돌려야 한다) ──
                case "char_vision_durat_up":
                case "char_vision_durat_down":
                {
                    int sign = rewardType.EndsWith("_up") ? 1 : -1;
                    float percent = sign * Mathf.Abs(value);
                    var chars = Characters(includeSummoned: true);
                    int touched = 0;
                    for (int i = 0; i < chars.Count; i++)
                    {
                        var vision = chars[i].GetComponent<Fog.VisionSource>();
                        if (vision == null) continue;
                        float before = vision.VisionTiles;
                        float after = Mathf.Max(1f, before * (1f + percent * 0.01f));
                        if (Mathf.Abs(after - before) < 0.01f) continue;
                        vision.SetVision(after);
                        // ⚠ <b>«원래 값으로» 되돌린다</b>(차액을 다시 빼지 않는다) —
                        //   시야는 곱셈으로 걸었으므로 차액을 빼면 소수점이 어긋난다.
                        ScheduleRevert(durationSeconds, () =>
                        {
                            if (vision != null) vision.SetVision(before);
                        });
                        touched++;
                    }
                    if (touched == 0) return "";
                    return durationSeconds > 0
                        ? string.Format(
                            UI.HudTheme.T("ui_reward_vision_timed", "시야 {0}% {1}초 ({2}명)"),
                            Signed(percent), durationSeconds, touched)
                        : string.Format(
                            UI.HudTheme.T("ui_reward_vision", "시야 {0}% ({1}명)"),
                            Signed(percent), touched);
                }

                // ── 영구 성장 (즉시 · 되돌리지 않는다) ──
                case "char_upgrade":
                {
                    // 표: *"저항력을 제외한 능력치 중 한 가지를 랜덤 추첨해, 랜덤 캐릭터 1명의
                    //      그 능력치를 {value_01} 만큼 영구히 올립니다"*.
                    // ⚠ <b>_live 에 넣지 않는다</b> — 넣으면 웨이브가 끝날 때 ClearAll 이
                    //   «영구히» 를 걷어간다. 저항력을 빼는 이유는 그 칸이 캐릭터 고유 고정값이라
                    //   강화로도 오르지 않기 때문이다(HeroAwakeningService 주석과 같은 규칙).
                    var pool = Characters(includeSummoned: false);
                    if (pool.Count == 0) return "";

                    CharacterUnit pick = pool[Random.Range(0, pool.Count)];
                    StatType stat;
                    do { stat = (StatType)Random.Range(0, (int)StatType.COUNT); }
                    while (stat == StatType.Resistance);

                    int amount = Mathf.Max(1, Mathf.Abs(value));
                    pick.AddFlatStatBonus(stat, amount);
                    // ⚠ {0}=이름 · {1}=능력치 이름 · {2}=수치. 셋 다 지우지 말 것.
                    return string.Format(
                        UI.HudTheme.T("ui_reward_permanent_stat", "{0} {1} +{2} (영구)"),
                        pick.DisplayName, StatBlock.DisplayName(stat), amount);
                }

                // ── 유물 획득 (즉시) ──
                //
                // ★★ <b>value_01 은 «수치» 가 아니라 «유물 ID» 다</b> (2026-08-24 · 유저 지시:
                //   *"등급별로 3개씩 이벤트 보상에 유물획득도 넣어"* ·
                //   *"이벤트 내용이랑 관련있는 유물이었으면 좋겠음 예상치못한 획득의 재미"*).
                //
                //   다른 보상 타입은 전부 «얼마나» 를 value_01 에 담는데 이것만 «무엇을» 을 담는다.
                //   그래도 <b>등급을 적어 굴리는 방식</b>을 쓰지 않은 이유가 있다 — 유저가
                //   «이벤트 내용과 관련 있는 유물» 을 원했다. 등급으로 굴리면 「곪은 자리」에서
                //   「젖은 활시위」가 나오는 일이 생기고, 그러면 <b>사건과 유물이 서로 남이 된다</b>.
                //   그래서 표가 <b>어느 유물인지 지목</b>한다.
                //
                // ⚠ 없는 ID 를 조용히 넘기지 않는다 — 표의 오타가 «아무 일도 안 일어나는 선택지»
                //   로 남으면 찾을 방법이 없다(이 클래스 맨 위의 ⚠⚠ 와 같은 판단).
                case "relic_gain":
                {
                    var inv = Relics.RelicInventory.Instance;
                    if (inv == null)
                    {
                        Debug.LogWarning("[이벤트] relic_gain — RelicInventory 가 없습니다 " +
                                         "(GameSystems 에 붙어 있는지 확인하세요).");
                        return "";
                    }

                    var relic = Relics.RelicRegistry.ById(value);
                    if (relic == null)
                    {
                        Debug.LogWarning($"[이벤트] relic_gain — 유물 ID {value} 를 찾지 못했습니다. " +
                                         "표의 reward_value_01 이 유물 테이블의 relic_id 와 맞는지 " +
                                         "확인하세요(py -3 Tools/gen_relic_assets.py 를 돌렸는지도).");
                        return "";
                    }

                    inv.Grant(relic);
                    // ⚠ 예전에는 «이름 획득 » 과 «(등급)» 을 <b>이어 붙였다</b> — 영어는 어순이
                    //   달라(Obtained X) 조각으로 두면 번역이 불가능하다. 한 형식으로 합친다.
                    return string.Format(
                        UI.HudTheme.T("ui_reward_relic_gain", "{0} 획득 ({1})"),
                        relic.DisplayName, Relics.RelicDefinitionSO.NameOf(relic.grade));
                }

                // ── 합류 / 사망 (즉시 · 확률) ──
                case "char_join":
                {
                    if (!Roll(value)) return "";
                    var create = UI.CharacterCreationService.Instance;
                    // ★ 「이벤트」는 <b>화면에 나간다</b> — CreateFree 가 «{이름} 합류 ({사유})»
                    //   로그를 찍는다. 아래 화상·구속의 표시 이름과 <b>같은 키</b>를 쓴다.
                    CharacterUnit joined = create != null
                        ? create.CreateFree(UI.HudTheme.T("ui_reward_source_event", "이벤트"))
                        : null;
                    return joined == null ? "" : string.Format(
                        UI.HudTheme.T("ui_reward_char_join", "{0} 합류"), joined.DisplayName);
                }

                case "char_die":
                {
                    if (!Roll(value)) return "";
                    // ⚠ 소환수는 제외한다 — «천사 1명» 이 사라져야 하는 자리에서 골렘이
                    //   죽으면 표의 문장(«부활 대상이 아닙니다»)과 뜻이 어긋난다.
                    var pool = Characters(includeSummoned: false);
                    if (pool.Count == 0) return "";
                    CharacterUnit victim = pool[Random.Range(0, pool.Count)];
                    string name = victim.DisplayName;
                    victim.ApplyDamage(victim.CurrentHp);
                    return string.Format(
                        UI.HudTheme.T("ui_reward_char_die", "{0} 사망"), name);
                }

                // ── 적 — 지속 상태 (전부 자기 만료 · 되돌릴 것 없음) ──
                case "enemy_atk_spd_durat_up":
                case "enemy_atk_spd_durat_down":
                case "enemy_move_spd_durat_up":
                case "enemy_move_spd_durat_down":
                {
                    bool atk = rewardType.StartsWith("enemy_atk_spd");
                    int sign = rewardType.EndsWith("_up") ? 1 : -1;
                    float percent = sign * Mathf.Abs(value);
                    float seconds = durationSeconds > 0 ? durationSeconds : 120f;

                    var mobs = WaveMonsters();
                    int touched = 0;
                    for (int i = 0; i < mobs.Count; i++)
                    {
                        var combat = mobs[i].GetComponent<UnitCombat>();
                        if (combat == null) continue;
                        if (atk) combat.ApplyEventAttackSpeedPercent(percent, seconds);
                        else combat.ApplyEventMoveSpeedPercent(percent, seconds);
                        touched++;
                    }
                    if (touched == 0) return "";
                    // ⚠ 예전에는 «적 공격 속도» 라는 <b>낱말만</b> 골라 문장에 끼워 넣었다 —
                    //   그러면 번역할 것이 낱말 조각이 되어 어순을 못 맞춘다. <b>문장을 통째로</b> 고른다.
                    // ★ seconds 는 여기서 항상 0보다 크다(위 120f 기본값) — 지속시간 없는 벌이 필요 없다.
                    string fmt = atk
                        ? UI.HudTheme.T("ui_reward_enemy_atk_spd", "적 공격 속도 {0}% {1}초 ({2}마리)")
                        : UI.HudTheme.T("ui_reward_enemy_move_spd", "적 이동 속도 {0}% {1}초 ({2}마리)");
                    return string.Format(fmt, Signed(percent), (int)seconds, touched);
                }

                case "enemy_atk_durat_down":
                {
                    // 「허약」과 <b>같은 통로</b>다(표가 그렇게 적고 있다).
                    var mobs = WaveMonsters();
                    float seconds = durationSeconds > 0 ? durationSeconds : 120f;
                    int touched = 0;
                    for (int i = 0; i < mobs.Count; i++)
                    {
                        var combat = mobs[i].GetComponent<UnitCombat>();
                        if (combat == null) continue;
                        combat.ApplyWeaken(Mathf.Abs(value), seconds);
                        touched++;
                    }
                    return touched == 0 ? "" : string.Format(
                        UI.HudTheme.T("ui_reward_enemy_atk_down", "적 공격력 −{0}% {1}초 ({2}마리)"),
                        Mathf.Abs(value), (int)seconds, touched);
                }

                case "enemy_def_durat_down":
                {
                    // 「부식」 장부를 쓴다 — 만료·중첩 규칙을 두 벌로 만들지 않는다.
                    //   ⚠ 표의 값은 <b>퍼센트</b>이고 부식은 <b>방어력 절대값</b>을 깎으므로
                    //     지금 방어력의 그 퍼센트만큼으로 환산한다.
                    var mobs = WaveMonsters();
                    float seconds = durationSeconds > 0 ? durationSeconds : 120f;
                    int touched = 0;
                    for (int i = 0; i < mobs.Count; i++)
                    {
                        int amount = Mathf.Max(1,
                            Mathf.RoundToInt(mobs[i].Stats.defense * Mathf.Abs(value) * 0.01f));
                        PassiveSkillService.ApplyCorrosion(mobs[i], amount, seconds);
                        touched++;
                    }
                    return touched == 0 ? "" : string.Format(
                        UI.HudTheme.T("ui_reward_enemy_def_down", "적 방어력 −{0}% {1}초 ({2}마리)"),
                        Mathf.Abs(value), (int)seconds, touched);
                }

                case "enemy_hp_percent_loss":
                {
                    var mobs = WaveMonsters();
                    int touched = 0;
                    for (int i = 0; i < mobs.Count; i++)
                    {
                        int amount = Mathf.Max(1,
                            Mathf.RoundToInt(mobs[i].MaxHp * Mathf.Abs(value) * 0.01f));
                        mobs[i].ApplyDamage(amount);
                        touched++;
                    }
                    return touched == 0 ? "" : string.Format(
                        UI.HudTheme.T("ui_reward_enemy_hp_loss", "적 체력 −{0}% ({1}마리)"),
                        Mathf.Abs(value), touched);
                }

                case "enemy_burn":
                {
                    var mobs = WaveMonsters();
                    float seconds = durationSeconds > 0 ? durationSeconds : 120f;
                    int touched = 0;
                    for (int i = 0; i < mobs.Count; i++)
                    {
                        int perSecond = Mathf.Max(1,
                            Mathf.RoundToInt(mobs[i].MaxHp * Mathf.Abs(value) * 0.01f));
                        var combat = mobs[i].GetComponent<UnitCombat>();
                        if (combat == null) continue;
                        // ★ 이 이름은 화상 표시 이름(UnitCombat.BurnLabel)으로 남는다 — 위 char_join 과 같은 키.
                        combat.ApplyBurn(perSecond, seconds, UI.HudTheme.T("ui_reward_source_event", "이벤트"));
                        touched++;
                    }
                    return touched == 0 ? "" : string.Format(
                        UI.HudTheme.T("ui_reward_enemy_burn", "적 화상 초당 {0}% {1}초 ({2}마리)"),
                        Mathf.Abs(value), (int)seconds, touched);
                }

                case "enemy_bind":
                {
                    var mobs = WaveMonsters();
                    // ★ 구속은 <b>시간만</b> 쓴다 — 표의 value_01 은 이 보상에서 뜻이 없다.
                    float seconds = durationSeconds > 0 ? durationSeconds : Mathf.Abs(value);
                    if (seconds <= 0f) return "";
                    int touched = 0;
                    for (int i = 0; i < mobs.Count; i++)
                    {
                        var combat = mobs[i].GetComponent<UnitCombat>();
                        if (combat == null) continue;
                        // ★ 이 이름은 초상화의 구속 표시(UnitCombat.BoundLabel)로 뜬다 — 같은 키.
                        combat.ApplyBind(seconds, UI.HudTheme.T("ui_reward_source_event", "이벤트"));
                        touched++;
                    }
                    // ⚠ 옛 «{seconds:0}» 서식은 인자 쪽으로 옮겼다 — 형식 문자열에 숫자 서식을
                    //   남기면 번역가가 «:0» 을 지워도 컴파일은 통과하고 화면만 어긋난다.
                    return touched == 0 ? "" : string.Format(
                        UI.HudTheme.T("ui_reward_enemy_bind", "적 구속 {0}초 ({1}마리)"),
                        seconds.ToString("0"), touched);
                }

                case "summon_enemy":
                {
                    var spawner = Object.FindFirstObjectByType<MonsterSpawner>();
                    if (spawner == null) return "";
                    int n = spawner.SpawnExtraNormals(Mathf.Max(1, Mathf.Abs(value)));
                    return n == 0 ? "" : string.Format(
                        UI.HudTheme.T("ui_reward_summon_enemy", "적 {0}마리 추가 소환"), n);
                }

                default:
                    // ⚠⚠ 맨 위 ⚠⚠ — 지어내지 않고 알린다.
                    Debug.LogWarning($"[이벤트] 보상 '{rewardType}'({value}) 은 아직 구현되지 않았습니다 — " +
                                     "RewardType 시트에는 있으나 코드가 없습니다.");
                    return "";
            }
        }

        // ⚠ 옛 <c>Span(seconds)</c>(«{n}초» 꼬리를 만들어 문장 뒤에 이어 붙이던 것)는 <b>지웠다</b> —
        //   그 꼬리가 영어에서는 «for 30s» 라 낱말이 필요하고, 조각으로 두면 번역할 수가 없다.
        //   지금은 지속시간이 «있는 문장/없는 문장» 두 벌을 각각 스트링 키로 둔다.

        /// <summary>부호를 붙인 정수 문구(+8 / −8). 로그가 표의 result_effect 와 같은 모양이 되게.</summary>
        static string Signed(float percent) =>
            percent > 0 ? $"+{Mathf.RoundToInt(percent)}" : $"−{Mathf.RoundToInt(-percent)}";

        /// <summary>
        /// 확률 판정 — 표의 값이 <b>퍼센트</b>인 보상(<c>char_join</c>·<c>char_die</c>)이 쓴다.
        /// 100 이상이면 반드시 발동하고, 0 이하면 발동하지 않는다.
        /// </summary>
        static bool Roll(int percent) =>
            percent >= 100 || (percent > 0 && Random.Range(0, 100) < percent);

        static string ApplyStat(StatType stat, int percent, int durationSeconds)
        {
            if (percent == 0) return "";

            // ★ 지속시간이 0 이면 <b>시간으로는 안 풀린다</b> — ClearAll 만 걷는다.
            //   («판을 갈아엎을 때» 는 여전히 한꺼번에 되돌려야 한다.)
            float expiresAt = durationSeconds > 0 ? Time.time + durationSeconds : 0f;

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
                _live.Add(new Applied { unit = c, stat = stat, delta = delta, expiresAt = expiresAt });
                touched++;
            }

            if (touched == 0) return "";
            string name = StatBlock.DisplayName(stat);
            // ★ 오름/내림은 <b>부호만</b> 다르므로 인자로 넘겨 키를 하나로 묶는다
            //   (음수는 int 서식이 이미 «-» 를 붙여 준다 — 옛 문장과 한 글자도 다르지 않다).
            string amount = percent > 0 ? $"+{percent}" : percent.ToString();
            return durationSeconds > 0
                ? string.Format(
                    UI.HudTheme.T("ui_reward_stat_timed", "{0} {1}% {2}초 ({3}명)"),
                    name, amount, durationSeconds, touched)
                : string.Format(
                    UI.HudTheme.T("ui_reward_stat", "{0} {1}% ({2}명)"),
                    name, amount, touched);
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

            for (int i = 0; i < _timed.Count; i++) _timed[i].revert?.Invoke();
            _timed.Clear();
        }

        /// <summary>지금 걸려 있는 보정 개수 — 디버그·로그용.</summary>
        public static int LiveCount => _live.Count + _timed.Count;
    }
}
