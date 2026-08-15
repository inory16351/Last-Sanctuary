namespace LastSanctuary.Combat
{
    /// <summary>
    /// 패시브 스킬 15종의 <b>분기용 식별자</b>. 캐릭터 테이블 <c>Skill.skill_type</c> 문자열과 1:1 이다.
    ///
    /// <b>왜 enum 으로 한 번 더 옮기는가</b> — <c>PassiveSkillSO.skillType</c> 은 문자열이고
    /// (테이블의 enum 칸을 그대로 옮긴 것), 전투 로직에서 매 프레임 문자열을 비교하면
    /// ① 오타가 컴파일에 안 걸리고 ② switch 가 사전 조회로 떨어진다.
    /// 문자열 → enum 변환은 <see cref="Parse"/> 에서 <b>한 번만</b> 하고 캐시한다.
    ///
    /// ⚠️ <b>표의 값에 앞뒤 공백이 섞여 있다</b> — 실제로 <c>" Rho_aias"</c>(앞 공백)가 들어 있다.
    /// 그래서 <see cref="Parse"/> 는 반드시 <c>Trim()</c> 한다. 이걸 빼면 로 아이아스만
    /// 조용히 <see cref="None"/> 이 되어 <b>스킬 하나가 아무 일도 안 한다</b>.
    /// </summary>
    public enum PassiveSkillType
    {
        /// <summary>알 수 없는 값 — 효과를 적용하지 않는다(표에 새 스킬이 추가된 경우).</summary>
        None = 0,

        // ── 엘린 9001 ────────────────────────────────────────────────
        /// <summary>타고난 섬세함 — 시야 0, 대신 사거리 +value01.</summary>
        InnateDelicacy,
        /// <summary>희생 — 크게 다친 동료를 자기 체력으로 살린다. 쿨타임.</summary>
        Sacrifice,
        /// <summary>유혈 낭자 — 공격마다 현재 체력 value01% 를 깎아 그만큼 공격력에 더한다.</summary>
        BloodAttack,

        // ── 비기오르 9002 ────────────────────────────────────────────
        /// <summary>강철의 의지 — 정신 이상 추첨에서 좋은 효과 가중치 ×value01.</summary>
        WillOfIron,
        /// <summary>타오르는 날개 — 반경 value01 의 적에게 초당 자기 현재 체력 value02% 피해.</summary>
        BlazingWings,
        /// <summary>로 아이아스 — 반경 value01 의 동료 방어력 +value02 (상한 초월).</summary>
        RhoAias,

        // ── 프레이야 9003 ────────────────────────────────────────────
        /// <summary>포식 — 최근 때린 적이 죽으면 최대 체력 value01% 회복.</summary>
        Gluttony,
        /// <summary>희열 — 최근 때린 적이 죽으면 공속·이속 +value03, value02초, 중첩·갱신.</summary>
        Ecstasy,
        /// <summary>광란 — 체력 50% 미만에서 공격력 +value01, 공격 시 현재 체력 value02% 소모.</summary>
        Rampage,

        // ── 피올로 9004 ──────────────────────────────────────────────
        /// <summary>부식 — 맞은 적의 방어력 −value01, value02초, 중첩 불가.</summary>
        Corrosion,
        /// <summary>정신 안정 — 같은 집결지 동료의 나쁜 정신 이상을 해제하고 침식 −value01. 쿨타임.</summary>
        CalmDown,
        /// <summary>정화의 손길 — 발동 중 때린 적에게 '정화' 부여. 그 적을 때리면 회복.</summary>
        PurifyingTouch,

        // ── 히스톤 9005 ──────────────────────────────────────────────
        /// <summary>선봉장 — 포지션 전방·공격 유형 근거리로 고정. 근거리 크리티컬 예외.</summary>
        Vanguard,
        /// <summary>분노 — '분노' 수치(0~100). 100 에서 죽으면 경직 뒤 부활.</summary>
        RageOn,
        /// <summary>복수자 — 부활할 때 반경 안 적에게 피해 + 아군 회복.</summary>
        Reaver,
    }

    public static class PassiveSkillTypes
    {
        /// <summary>
        /// 테이블 문자열 → enum. 못 알아보면 <see cref="PassiveSkillType.None"/>.
        /// 공백·대소문자에 관대하게 만든다(표는 사람이 손으로 적는 칸이다).
        /// </summary>
        public static PassiveSkillType Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return PassiveSkillType.None;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "innate_delicacy": return PassiveSkillType.InnateDelicacy;
                case "sacrifice":       return PassiveSkillType.Sacrifice;
                case "blood_attack":    return PassiveSkillType.BloodAttack;
                case "will_of_iron":    return PassiveSkillType.WillOfIron;
                case "blazing_wings":   return PassiveSkillType.BlazingWings;
                case "rho_aias":        return PassiveSkillType.RhoAias;
                case "gluttony":        return PassiveSkillType.Gluttony;
                case "ecstasy":         return PassiveSkillType.Ecstasy;
                case "rampage":         return PassiveSkillType.Rampage;
                case "corrosion":       return PassiveSkillType.Corrosion;
                case "calm_down":       return PassiveSkillType.CalmDown;
                case "purifying_touch": return PassiveSkillType.PurifyingTouch;
                case "vanguard":        return PassiveSkillType.Vanguard;
                case "rage_on":         return PassiveSkillType.RageOn;
                case "reaver":          return PassiveSkillType.Reaver;
                default:                return PassiveSkillType.None;
            }
        }
    }
}
