namespace LastSanctuary.Combat
{
    /// <summary>진영. 테마상 천사(백혈구) 대 암세포.</summary>
    public enum Faction
    {
        Angel = 0,   // 플레이어 — 캐릭터, 넥서스, 포탑
        Cancer = 1,  // 적 — 몬스터 (웨이브로 넥서스를 침공)
        Neutral = 2, // 중립 몬스터 — 웨이브와 무관하게 맵에 서식, 캐릭터가 사냥해 에너지 획득
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
        /// <summary>
        /// 전투 AI가 "누구를 적으로 볼지" 판단하는 기준.
        /// Angel↔Cancer 는 기존과 동일한 대칭 관계를 유지한다.
        /// Neutral(선공형 중립 몬스터)은 Angel(캐릭터)만 적으로 본다 — 캐릭터 쪽에서
        /// 중립 몬스터를 자동으로 적대시하지는 않는다(그러면 Opposite 가 대칭이 깨진다).
        /// 캐릭터가 중립 몬스터를 사냥하는 것은 <c>UnitCombat.SetHuntTarget</c> 을 통한
        /// 별도 오버라이드 경로다 — <see cref="LastSanctuary.Units.CharacterBehavior"/> 참조.
        /// </summary>
        public static Faction Opposite(this Faction f) => f switch
        {
            Faction.Angel => Faction.Cancer,
            Faction.Cancer => Faction.Angel,
            Faction.Neutral => Faction.Angel,
            _ => Faction.Angel,
        };
    }
}
