namespace LastSanctuary.Combat
{
    /// <summary>
    /// <b>보스 스킬을 가진 유닛</b>이 구현하는 한 겹 (2026-08-15).
    ///
    /// ★ <b>왜 생겼나</b> — <see cref="BossSkillCaster"/> 는
    /// <c>MonsterDefinitionSO.bossSkillIds</c> 를 <b>직접</b> 읽고 있었다. 그래서 그 컴포넌트는
    /// 웨이브 몬스터에만 붙일 수 있었는데, 카르시노스(에픽 <b>중립</b> 1004)가 스킬을 갖게
    /// 되면서 같은 코드가 필요해졌다.
    ///
    /// 방법은 셋이었다:
    /// <code>
    ///   ① 캐스터 안에 `is MonsterUnit / is NeutralMonsterUnit` 갈래를 넣는다
    ///   ② 중립용 캐스터를 하나 더 만든다
    ///   ③ "스킬 번호를 알려줄 수 있다" 는 것만 인터페이스로 뽑는다   ← 이걸 골랐다
    /// </code>
    /// ①은 종이 늘 때마다 캐스터를 고쳐야 하고(이 저장소가 <c>DisplayName</c> 에서 정확히
    /// 그 방식으로 중립을 두 번 빠뜨렸다), ②는 조준·범위·연출 코드가 두 벌이 된다.
    /// ③은 <b>캐스터가 유닛 종류를 아예 모르게</b> 만든다 — 앞으로 스킬을 가진 종이
    /// 늘어도 그 종이 이 인터페이스만 구현하면 된다.
    /// </summary>
    public interface IBossSkillOwner
    {
        /// <summary>
        /// 이 유닛이 쓸 스킬 id 목록. 표의 <c>boss_skill_*</c> / <c>mon_skill_*</c> 순서 그대로다 —
        /// <b>그 순서가 곧 슬롯 번호</b>이고, 스킨의 <c>skill1*</c>/<c>skill2*</c> 모션과 짝이 된다.
        /// 0 은 빈 칸이라 건너뛴다.
        /// </summary>
        int[] BossSkillIds { get; }

        /// <summary>
        /// 정의가 들어와서 <see cref="BossSkillIds"/> 를 읽어도 되는 상태인가.
        ///
        /// ⚠ 유닛은 <c>Instantiate</c> 된 <b>뒤에</b> <c>Initialize</c> 로 정의를 받는다.
        /// 그 전에 읽으면 빈 목록을 캐시해버려 <b>스킬이 영영 안 나간다</b> —
        /// 이 저장소가 같은 순서 함정을 세 번 겪었다(24-6·27-9·29-2절).
        /// </summary>
        bool SkillsReady { get; }
    }
}
