namespace LastSanctuary.Combat
{
    /// <summary>진영. 테마상 천사(백혈구) 대 암세포.</summary>
    public enum Faction
    {
        Angel = 0,   // 플레이어 — 캐릭터, 넥서스, 포탑
        Cancer = 1,  // 적 — 몬스터
    }

    /// <summary>
    /// 유닛 종류. 몬스터의 공격 우선순위 판정에 쓴다(웨이브 기획서 p13).
    ///   일반 / 중간보스 : 타워 → 캐릭터 → 넥서스
    ///   메인보스        : 캐릭터 → 타워 → 넥서스
    /// </summary>
    public enum UnitKind
    {
        Character = 0,
        Tower = 1,
        Nexus = 2,
        Monster = 3,
    }

    public static class FactionExtensions
    {
        public static Faction Opposite(this Faction f) =>
            f == Faction.Angel ? Faction.Cancer : Faction.Angel;
    }
}
