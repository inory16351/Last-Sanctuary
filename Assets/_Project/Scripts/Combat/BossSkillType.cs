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
                default:            return BossSkillType.None;
            }
        }
    }
}
