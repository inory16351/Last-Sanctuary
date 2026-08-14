namespace LastSanctuary.Combat
{
    /// <summary>
    /// <b>성장 유형</b> — 강화할 때 <b>어느 능력치 묶음이 더 잘 오르는지</b>
    /// (유저 확정 2026-08-14).
    ///
    /// <b>어떻게 작동하나</b> — 강화는 성장 가능한 능력치 12종(저항력 제외)을 각각 굴린다.
    /// 이 유형에 <b>묶인</b> 능력치는 굴림 결과에 <c>growthFocusBonus</c>(기본 +1)를 더해
    /// 범위가 <b>0~5 → 1~6</b> 으로 올라간다. 확률 분포 자체는 같은 표를 쓴다
    /// (<c>CharacterUpgradeService.growthWeights</c>) — 한 칸 밀어 쓸 뿐이다.
    /// 그래서 "이 유형은 이 능치가 잘 오른다"가 <b>확실한 상향</b>이면서도
    /// 다른 능력치가 아예 안 오르는 것은 아니다.
    ///
    /// <b>UI</b> — 캐릭터 성장 창의 성장 유형 버튼 5개가 이 값을 정하고,
    /// 고른 유형에 묶인 능력치 칸은 다른 색으로 강조된다
    /// (유저 지시: "확률이 높은 스탯은 다른 색으로 표시").
    /// </summary>
    public enum StatGrowthFocus
    {
        /// <summary>미선택 — 전부 같은 확률(0~5)로 오른다.</summary>
        None = 0,

        /// <summary>탱커 — 체력 · 방어력 · 체력 재생.</summary>
        Tank = 1,

        /// <summary>근거리 딜러 — 근거리 공격력 · 체력 · 공격 속도.</summary>
        MeleeDps = 2,

        /// <summary>원거리 딜러 — 원거리 공격력 · 크리티컬 확률 · 명중률 · 공격 속도.</summary>
        RangedDps = 3,

        /// <summary>마법 딜러 — 마법 · 공격 속도.</summary>
        MagicDps = 4,

        /// <summary>지원가 — 회복력 · 이동속도.</summary>
        Support = 5,
    }

    /// <summary>
    /// 성장 유형에 딸린 표 — 어떤 능력치가 묶여 있고 화면에 뭐라고 쓰는지.
    ///
    /// <b>왜 한 곳에 모았나</b> — 이 목록을 읽는 곳이 셋이다(강화 계산 · 성장 창의 강조 표시 ·
    /// 버튼 라벨). 세 곳에 각자 적어두면 표가 갈라진다 — 이 프로젝트가
    /// <see cref="TacticalOrder.Label(TacticalAttackType)"/> 를 한 곳에 모아둔 것과 같은 이유다.
    /// </summary>
    public static class StatGrowthFocusTable
    {
        /// <summary>고를 수 있는 유형(미선택 제외). UI 버튼 순서가 이 순서다.</summary>
        public static readonly StatGrowthFocus[] Selectable =
        {
            StatGrowthFocus.Tank,
            StatGrowthFocus.MeleeDps,
            StatGrowthFocus.RangedDps,
            StatGrowthFocus.MagicDps,
            StatGrowthFocus.Support,
        };

        static readonly StatType[] TankStats = { StatType.Hp, StatType.Defense, StatType.Regen };

        static readonly StatType[] MeleeStats = { StatType.Attack, StatType.Hp, StatType.AttackSpeed };

        static readonly StatType[] RangedStats =
        {
            StatType.RangedAttack, StatType.Critical, StatType.Accuracy, StatType.AttackSpeed,
        };

        static readonly StatType[] MagicStats = { StatType.Magic, StatType.AttackSpeed };

        static readonly StatType[] SupportStats = { StatType.Cure, StatType.MoveSpeed };

        static readonly StatType[] NoStats = { };

        /// <summary>이 유형에서 더 잘 오르는 능력치들.</summary>
        public static StatType[] FavoredStats(StatGrowthFocus focus) => focus switch
        {
            StatGrowthFocus.Tank      => TankStats,
            StatGrowthFocus.MeleeDps  => MeleeStats,
            StatGrowthFocus.RangedDps => RangedStats,
            StatGrowthFocus.MagicDps  => MagicStats,
            StatGrowthFocus.Support   => SupportStats,
            _                         => NoStats,
        };

        /// <summary>이 능력치가 그 유형에 묶여 있는가.</summary>
        public static bool IsFavored(StatGrowthFocus focus, StatType stat)
        {
            StatType[] list = FavoredStats(focus);
            for (int i = 0; i < list.Length; i++)
                if (list[i] == stat) return true;
            return false;
        }

        /// <summary>버튼·로그에 쓰는 이름.</summary>
        public static string Label(StatGrowthFocus focus) => focus switch
        {
            StatGrowthFocus.Tank      => "탱커",
            StatGrowthFocus.MeleeDps  => "근거리 딜러",
            StatGrowthFocus.RangedDps => "원거리 딜러",
            StatGrowthFocus.MagicDps  => "마법 딜러",
            StatGrowthFocus.Support   => "지원가",
            _                         => "미선택",
        };
    }
}
