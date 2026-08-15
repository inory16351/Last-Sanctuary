namespace LastSanctuary.Combat
{
    /// <summary>
    /// 보스 스킬의 <b>분기용 식별자</b>. 웨이브 몬스터 테이블 <c>Skill.skill_type</c> 문자열과
    /// 1:1 이다 — <see cref="PassiveSkillType"/> 과 완전히 같은 구조·같은 이유다
    /// (문자열을 매 프레임 비교하지 않고, 오타가 컴파일에 안 걸리는 것을 막는다).
    ///
    /// 두 스킬의 <b>모양은 같다</b> — 보스 자기 칸에서 한 방향으로 뻗는 직사각형 범위에
    /// "근거리 공격력 × value03%" 를 넣는다. 다른 것은 <b>누구를 향해 뻗는지</b> 뿐이다:
    /// 타락한 무덤은 <b>가장 가까운</b> 적, 공허의 광선은 <b>가장 먼</b> 적.
    /// 그래서 <see cref="BossSkillCaster"/> 는 이 enum 을 조준 규칙에만 쓴다.
    /// </summary>
    public enum BossSkillType
    {
        /// <summary>알 수 없는 값 — 시전하지 않는다(표에 새 스킬이 추가된 경우).</summary>
        None = 0,

        /// <summary>
        /// 타락한 무덤 (130001) — <b>가장 가까운</b> 적을 향해 5 x 3 타일.
        /// 근접한 적을 쓸어내는 기술이라 붙어 있는 전열이 주 대상이다.
        /// </summary>
        FallenTomb,

        /// <summary>
        /// 공허의 광선 (130002) — <b>가장 먼</b> 적을 향해 15 x 3 타일.
        /// 뒤에서 안전하게 쏘던 원거리·치유 캐릭터를 노리는 기술이다.
        /// </summary>
        VoidLaser,

        // ──────────────────────────────────────────────────────────────
        // 카르시노스 (에픽 중립 보스 1004) — 2026-08-15
        //
        // ⚠ <b>값의 뜻이 단탈리온과 다르다.</b> 단탈리온의 두 스킬은 value_04 가 침식이지만,
        //   이 둘은 스트링 테이블의 정의문이 각자 다른 것을 말한다:
        //     할퀴기      value_04 = 방어력 <b>감소 %</b> · value_05 = 지속 <b>초</b>
        //     죽음의 포효 value_01 = <b>반지름</b> 타일 · value_02 = <b>넉백</b> 타일
        //   그래서 <see cref="BossSkillSO"/> 의 프로퍼티도 종류별로 갈라 읽는다 —
        //   칸 번호가 아니라 <b>뜻</b>으로 이름 붙인 프로퍼티를 쓸 것.
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 할퀴기 (2001) — <b>가장 가까운</b> 적을 향해 전방 5 x 3 타일.
        /// 맞은 적은 <b>방어력이 깎인다</b>(value_04 % · value_05 초).
        /// 단탈리온의 「타락한 무덤」과 조준·모양이 같고, 붙는 효과만 다르다.
        /// </summary>
        Scratch,

        /// <summary>
        /// 죽음의 포효 (2002) — 자기 중심 <b>원형</b>. 맞은 적을 <b>뒤로 밀어낸다</b>
        /// (value_02 타일). 붙어서 때리던 전열을 통째로 떼어내는 기술이다.
        /// </summary>
        RoarDeath,
    }

    public static class BossSkillTypes
    {
        /// <summary>
        /// 테이블 문자열 → enum. 못 알아보면 <see cref="BossSkillType.None"/>.
        /// 공백·대소문자에 관대하다 — 표는 사람이 손으로 적는 칸이고, 실제로 이 프로젝트의
        /// 표에 앞 공백이 섞여 들어온 적이 있다(<see cref="PassiveSkillTypes.Parse"/> 주석).
        /// </summary>
        public static BossSkillType Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return BossSkillType.None;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "fallen_tomb": return BossSkillType.FallenTomb;
                case "void_laser":  return BossSkillType.VoidLaser;
                case "scratch":     return BossSkillType.Scratch;
                case "roar_death":  return BossSkillType.RoarDeath;
                default:            return BossSkillType.None;
            }
        }
    }

    /// <summary>
    /// 스킬 범위의 <b>모양</b>. 표 <c>Skill.range_type</c> 칸이 정본이다(2026-08-13 신설).
    ///
    /// <b>왜 칸을 새로 뒀나</b> — 유저 지시가 "360도 범위 값으로 적용 가능하게 ... 원형으로"
    /// 였는데, 단탈리온의 두 스킬은 원화가 <b>가로로 긴 파동·광선</b>이라 그대로 원형으로 만들면
    /// 그림과 판정이 어긋난다. 그래서 <b>모양을 표에서 고르게</b> 하고, 기본값인
    /// <see cref="Line"/> 은 조준을 4방향에서 <b>자유각(360도)</b>으로 풀어 "대각선 적을 못
    /// 때린다"는 문제만 정확히 해소한다. 진짜 원형이 필요한 스킬은 표에서 <c>Circle</c> 로
    /// 바꾸면 코드 수정 없이 원형으로 돈다.
    /// </summary>
    public enum BossSkillShape
    {
        /// <summary>
        /// 조준 방향으로 뻗는 <b>직사각형</b>(기본). 각도 제한이 없다 —
        /// <see cref="UnitRegistry.CollectEnemiesInOrientedRect"/> 로 상자를 통째로 돌린다.
        /// </summary>
        Line = 0,

        /// <summary>
        /// 보스를 중심으로 한 <b>원형</b>. 반지름 = <c>value_01</c>(지름, 타일) ÷ 2.
        /// 방향이라는 개념이 없으므로 어느 각도의 적이든 거리만 맞으면 전부 맞는다.
        /// </summary>
        Circle = 1,
    }

    public static class BossSkillShapes
    {
        /// <summary>표 문자열 → enum. 비었거나 못 알아보면 <see cref="BossSkillShape.Line"/>.</summary>
        public static BossSkillShape Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return BossSkillShape.Line;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "circle":
                case "round":
                case "radial": return BossSkillShape.Circle;
                default:       return BossSkillShape.Line;
            }
        }
    }
}
