namespace LastSanctuary.Combat
{
    /// <summary>
    /// 패시브 스킬 33종의 <b>분기용 식별자</b>. 캐릭터 테이블 <c>Skill.skill_type</c> 문자열과 1:1 이다.
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

        // ── 시그리드 9006 (2026-08-20) ───────────────────────────────
        /// <summary>
        /// 가학증 — 때린 적이 value01 초 안에 죽으면 <b>value02% 확률로</b>
        /// 지름 value03 안의 아군을 «시그리드 현재 체력의 value04%» 만큼 회복시키고
        /// 시그리드는 그만큼 잃는다. 발동할 때마다 <see cref="JoyOfPain"/> 도 켠다.
        /// </summary>
        Sadism,

        /// <summary>
        /// 고통의 기쁨 — 「가학증」이 발동할 때마다 공격속도 +value01%, value02초.
        /// 중첩되지 않고 <b>지속시간만 초기화</b>된다.
        /// </summary>
        JoyOfPain,

        /// <summary>
        /// 통제할 수 없는 쾌락 — 현재 체력이 최대 체력의 value01% 아래로 떨어지면
        /// value02초 동안 <b>모든 피해를 무시</b>한다. 회복은 되고, 체력 변화로 안 풀린다.
        /// 쿨타임이 있다(표의 cool_time).
        /// </summary>
        UncontrollablePleasure,

        // ── 시카리아 9007 (2026-08-20) ───────────────────────────────
        /// <summary>
        /// 고조된 감각 — 사거리 +value01 타일. 공격 유형이 <b>원거리</b>면 value02 만큼 더.
        /// 상시 효과다(쿨타임 없음).
        /// </summary>
        HeightenedSenses,

        /// <summary>
        /// 한발에 두마리 — 원거리 평타가 사거리 안의 적 <b>value01 마리</b>를 동시에 때린다.
        /// (value01 은 «총 몇 마리» 다 — 추가 수가 아니다. 정의문: "모든 적을 동시에 2마리")
        /// </summary>
        TwoOnOneLeg,

        /// <summary>
        /// 애로우 레인 — 현재 대상을 중심으로 반경 value01 칸 안의 적에게
        /// <b>원거리 공격력의 value02%</b> 피해. 쿨타임.
        /// </summary>
        ArrowRain,

        // ── 아루 9008 (2026-08-20) ───────────────────────────────────
        /// <summary>
        /// 도움의 손길 — 반경 value01 안에서 침식이 value02 이상이거나 후퇴 중인 동료를
        /// <b>즉시 아루 곁으로 옮긴다</b>. 상시(쿨타임 0)라 이 파일에서 자체 간격을 둔다.
        /// </summary>
        AHelpingHand,

        /// <summary>
        /// 구원 — 「도움의 손길」로 옮겨진 아군을 <b>즉시 체력 재생 가능 상태</b>로 만든다.
        /// 단독으로는 아무 일도 하지 않는다 — 위 스킬의 <b>부가 효과</b>다.
        /// </summary>
        Salvation,

        /// <summary>
        /// 강림 — 아루의 공격 유형이 쓰는 능력치의 value01% 를 <b>모든 능력치로 갖는 골렘</b>을
        /// 소환한다. 크기는 value02(가로) x value03(세로) 타일.
        /// ⚠ 쿨타임은 <b>골렘이 죽은 시점부터</b> 돈다(정의문).
        /// </summary>
        Dawn,

        // ── 카이론 9009 (2026-08-20) ─────────────────────────────────
        /// <summary>
        /// 타락한 육체 — value01 초 동안 <b>최대 체력의 value02% 짜리 보호막</b>. 쿨타임.
        /// </summary>
        FallenBody,

        /// <summary>
        /// 천상의 방패 — value01 초 정신집중 → 지름 value02 안의 적을 value03 초 <b>도발</b> →
        /// 도발이 끝나면 그 범위에 <b>근거리 공격력의 value04%</b> 피해. 쿨타임.
        /// </summary>
        CelestialShield,

        /// <summary>
        /// 천벌 — value01 초 정신집중 → <b>가로 value02 x 세로 value03</b> 직사각형 안의 적에게
        /// 근거리 공격력의 value04% 피해 + value05 초 동안 <b>방어력 value06% 감소</b>. 쿨타임.
        /// </summary>
        DivineWrath,

        // ── 아르세니아 9010 (2026-08-20) ─────────────────────────────
        /// <summary>
        /// 불안정성 — <b>근거리 유형을 고를 수 없다</b>. 대신 회복·마법에도 명중률·크리티컬이
        /// 걸린다(<see cref="Units.CharacterUnit.FullAccuracyAllowed"/>). 상시.
        /// </summary>
        Instability,

        /// <summary>
        /// 성스러운 축복 — 가장 전방의 아군 자리에 반지름 value01 의 공간을 value02 초 만든다.
        /// 그 안의 적은 <b>초당</b> value03% 피해, 아군은 <b>받는 회복</b>이 value03% 늘어난다.
        /// </summary>
        SacredBlessing,

        /// <summary>
        /// 완성되지 못한 고귀함 — 반경 value01 안의 적이 value02 이상이면 반경 value03 에
        /// value04% 피해. 쓰고 나면 value05 초 <b>행동 불능</b>(정신 이상 해제로 못 푼다).
        /// </summary>
        UnfinishedNobility,

        // ── 불칸 9011 (2026-08-20) ───────────────────────────────────
        /// <summary>
        /// 타오르는 분노 — 공격이 value01% 확률로 <b>화상</b>. 초당 공격력 value02% 를
        /// value03 초 동안. 상시.
        /// </summary>
        BlazingAnger,

        /// <summary>
        /// 현자의 지혜 — 마법 +value01 · 공격속도 +value02 <b>영구</b>. 상한을 초월한다.
        /// </summary>
        TheWisdomOfASage,

        /// <summary>
        /// 화염 세례 — 공격 중인 대상 자리에 거대 화염구. 반경 value01 에 value02% 피해. 쿨타임.
        /// </summary>
        FlameBlast,

        // ──────────────────────────────────────────────────────────────
        // 신규 3인 (2026-08-21) — 엘리시아 9012 · 세라피엘 9013 · 시안 9014
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 강인한 정신 (80034 · 엘리시아) — 체력이 최대의 <c>value01</c>% 아래로 떨어지면
        /// <c>value02</c>초에 <b>걸쳐</b> 최대 체력의 <c>value03</c>% 를 회복한다. 쿨타임.
        ///
        /// ⚠ 「급속 재생」(보스 2008)·「강제 보급」(보스 130012)과 <b>같은 «문턱 + 자기 회복»</b>
        ///   이지만 이쪽은 <b>캐릭터</b>다. 문턱은 «이하» 로 읽는다 — 한 프레임에 정확히 그 값을
        ///   지나갈 보장이 없어서 «정확히» 로 읽으면 영영 안 터진다(보스 쪽에서 같은 판단).
        /// </summary>
        StrongMind,

        /// <summary>
        /// 군단의 방패 (80035 · 엘리시아) — <b>공격받을 때마다</b> 자기 최대 체력의
        /// <c>value01</c>% 를 <b>때린 적에게</b> 그대로 준다. 상시 · 쿨타임 없음.
        ///
        /// ★★ <b>이 프로젝트의 첫 «반사» 스킬</b>이다. 지금까지 캐릭터 패시브의 계기는
        ///   «내가 때릴 때» 또는 «상시» 였고 <b>«맞을 때» 가 없었다</b>.
        ///   → <see cref="DamageableUnit.OnAnyDamaged"/> 를 구독한다.
        /// ⚠ <b>반사의 반사를 끊어야 한다</b> — 방패를 든 둘이 서로 때리면 무한히 오간다.
        ///   «반사 중» 깃발로 재진입을 막는다.
        /// ⚠ 피해는 <b>최대 체력 비례</b>라 방어력을 거치지 않는다(「타오르는 숨결」의 추가
        ///   피해와 같은 성질 — 근거가 공격력이 아니라 «내 체력» 이다).
        /// </summary>
        LegionsShield,

        /// <summary>
        /// 네 날개의 가호 (80036 · 엘리시아) — 자기 중심 반지름 <c>value01</c>:
        /// 적에게 <b>마법</b> x <c>value02</c>% 피해 + <c>value03</c>초 기절,
        /// <b>같은 범위의 아군</b>은 자기 최대 체력의 <c>value04</c>% 만큼 회복. 쿨타임.
        ///
        /// ★ 기절은 <see cref="UnitCombat.ApplyBind"/> 다 — 이 프로젝트는 «기절» 을 구속과
        ///   같은 상태로 다룬다(아니사킬 「거대한 위협 포효」·폴리르 「포화」와 같은 판단).
        /// </summary>
        BlessingOfFourWings,

        /// <summary>
        /// 회피 기동 (80037 · 세라피엘) — 반지름 <c>value01</c> 안에 적이 들어오면 즉시
        /// <b>그 적의 반대쪽으로</b> <c>value02</c> 타일 도약하고 <c>value03</c>회 원거리 공격. 쿨타임.
        ///
        /// ⚠ <b>순간이동은 «벽에 낀 유닛» 사고를 만든다</b>(116절) — 도착 자리를
        ///   <c>MapGenerator.TryFindPlaceableNear</c> 로 <b>빈 칸에 스냅</b>한다
        ///   (「도움의 손길」이 동료를 내려놓을 때와 같은 규칙 · 119-11절).
        /// </summary>
        EvasiveManeuver,

        /// <summary>
        /// 명사수 (80038 · 세라피엘) — <b>크리티컬 확률 +<c>value01</c> 영구</b>. 상한을 초월한다.
        ///
        /// ⚠⚠ <b>표에 밸류가 없었다</b>(125-4절이 예고한 그 구멍) — 정의문이 «20의 크리티컬
        ///   확률» 이라고 <b>문장에 숫자를 박아</b> 두었고 <c>value_01~06</c> 은 모두 0 이었다.
        ///   그대로 두면 <b>+0</b> 이 걸린다. → 표에 <c>value_01 = 20</c> 을 넣고 정의문을
        ///   <c>{value_01}</c> 로 바꿨다. 이 표의 규약은 «수치는 밸류 칸, 문장은 자리표시» 다.
        /// ★ 「현자의 지혜」(80032)와 <b>같은 통로</b>다 — <c>CharacterUnit.AddFlatStatBonus</c>
        ///   는 능력치 상한을 거치지 않으므로 «상한을 초월할 수 있다» 가 그대로 성립한다.
        /// </summary>
        Sharpshooter,

        /// <summary>
        /// 종말의 선언 (80039 · 세라피엘) — <b>전방 <c>value01</c>(가로) x <c>value02</c>(세로)</b>
        /// 직사각형에 <b>초당</b> 원거리 x <c>value03</c>% 피해를 <c>value04</c>초 동안. 쿨타임.
        ///
        /// ★★ <b>«초당 · 몇 초 동안» 인 첫 캐릭터 스킬</b>이다. 상자를 <b>시전한 자리에 고정</b>
        ///   하고 그 시간 동안 매초 다시 판정한다 — 베일 「담배 연기」와 같은 구조다.
        ///   ⚠ 상자를 세라피엘에게 붙여 따라다니게 하면 «전방 집중 포격» 이 아니라
        ///     «몸에 붙은 장판» 이 된다.
        /// </summary>
        DeclarationOfTheEnd,

        /// <summary>
        /// 영혼 흡수 (80040 · 시안) — 반지름 <c>value01</c> 안에서 적이 <b>죽을 때마다</b>
        /// 영혼 1을 얻는다. 모인 수는 스킬 칸에 표시된다. 상시.
        ///
        /// ★★ <b>이 프로젝트의 첫 «자원을 모으는» 패시브</b>다. 지금까지는 상시 보정 ·
        ///   쿨타임 발동 · 조건부 발동 셋뿐이었고 <b>누적 상태</b>가 없었다.
        ///   → <see cref="DamageableUnit.OnAnyDied"/> 를 구독해 거리로 걸러 센다.
        /// ⚠ <b>부활하면 0 부터다</b> — 「한계 돌파」로 이미 받은 영구 보정은 남지만, 영혼 자체는
        ///   판의 자원이 아니라 <b>그 유닛의 상태</b>다.
        /// </summary>
        SoulAbsorption,

        /// <summary>
        /// 사신의 낫 (80041 · 시안) — <b>근거리 평타마다</b> <c>value01</c>% 확률로 반지름
        /// <c>value02</c> 범위를 후려친다. 피해 = 근거리 x <c>value03</c>% <b>+ 모은 영혼 수</b>.
        ///
        /// ★ 계기는 <see cref="UnitCombat.OnAttackPerformed"/> 다 — «평타를 실제로 냈을 때» 라
        ///   쿨타임이 아니라 <b>확률</b>로 도는 것이 정의문과 맞는다.
        /// ★ <b>영혼이 피해에 더해진다</b> — «(근거리 공격력 x % + 획득한 영혼)» 이라고 정의문이
        ///   괄호까지 써서 못박았다. 즉 영혼은 <b>고정 추가 피해</b>이고 배율이 아니다.
        /// </summary>
        ReapersScythe,

        /// <summary>
        /// 한계 돌파 (80042 · 시안) — 영혼 <c>value01</c>개마다 근거리 공격력이 <b>영구히</b>
        /// <c>value02</c> 오른다. 상한을 초월한다.
        ///
        /// ★ 「명사수」와 같은 통로(<c>CharacterUnit.AddFlatStatBonus</c>)를 쓴다 —
        ///   다른 점은 <b>계기가 누적 영혼</b>이라는 것뿐이다.
        /// ⚠ <b>«지금 받아야 할 총량 − 이미 준 총량»</b> 을 더하는 식으로 짠다. 그래야 영혼이
        ///   늘 때마다 이중으로 걸리지 않고, 줄어도(부활) 정확히 되돌아간다.
        /// </summary>
        BreakingThroughLimits,
    }

    public static class PassiveSkillTypes
    {
        /// <summary>
        /// 테이블 문자열 → enum. 못 알아보면 <see cref="PassiveSkillType.None"/>.
        /// 공백·대소문자에 관대하게 만든다(표는 사람이 손으로 적는 칸이다).
        /// </summary>
        /// <summary>
        /// 표 문자열을 <b>비교용으로 다듬는다</b> — 앞뒤 공백 제거 · 소문자 ·
        /// <b>따옴표 제거</b>(곧은 것과 둥근 것 둘 다) · 공백·하이픈을 <c>_</c> 로.
        ///
        /// ⚠⚠ <b>둥근 따옴표는 실제로 표에 들어온다</b>(2026-08-21 · <c>The_Legion’s_Shield</c>).
        ///   엑셀이 «자동 고침» 으로 곧은 따옴표를 둥근 것으로 바꾸기 때문이고, 그 글자는
        ///   C# enum 이름에 쓸 수 없다. 여기서 지우면 <b>표를 손대지 않아도</b> 맞고 나중에
        ///   표가 곧은 따옴표로 바뀌어도 <b>같은 값</b>으로 떨어진다.
        /// </summary>
        static string Normalize(string raw)
        {
            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (char c in raw.Trim().ToLowerInvariant())
            {
                if (c == '\'' || c == '’' || c == 'ʼ') continue;   // 따옴표는 버린다
                sb.Append(c == ' ' || c == '-' ? '_' : c);
            }
            return sb.ToString();
        }

        public static PassiveSkillType Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return PassiveSkillType.None;

            switch (Normalize(raw))
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

                // ── 시그리드 9006 (2026-08-20) ──
                case "sadism":                  return PassiveSkillType.Sadism;
                case "joy_of_pain":             return PassiveSkillType.JoyOfPain;
                case "uncontrollable_pleasure": return PassiveSkillType.UncontrollablePleasure;

                // ── 신규 캐릭터 3인 (2026-08-20) ──
                case "heightened_senses":       return PassiveSkillType.HeightenedSenses;
                case "two_on_one_leg":          return PassiveSkillType.TwoOnOneLeg;
                case "arrow_rain":              return PassiveSkillType.ArrowRain;
                case "a_helping_hand":          return PassiveSkillType.AHelpingHand;
                case "salvation":               return PassiveSkillType.Salvation;
                case "dawn":                    return PassiveSkillType.Dawn;
                case "fallen_body":             return PassiveSkillType.FallenBody;
                // ⚠ 표의 `Skill` 시트는 소문자 s(`Celestial_shield`), `Skill_Type` 시트는
                //   대문자 S 로 적혀 있었다(2026-08-20 에 소문자로 통일). 어느 쪽이 와도 받는다 —
                //   여기서 이미 ToLowerInvariant 를 거치므로 자동으로 그렇게 된다.
                case "celestial_shield":        return PassiveSkillType.CelestialShield;
                case "divine_wrath":            return PassiveSkillType.DivineWrath;

                // ── 아르세니아 9010 · 불칸 9011 (2026-08-20) ──
                case "instability":             return PassiveSkillType.Instability;
                case "sacred_blessing":         return PassiveSkillType.SacredBlessing;
                case "unfinished_nobility":     return PassiveSkillType.UnfinishedNobility;
                case "blazing_anger":           return PassiveSkillType.BlazingAnger;
                case "the_wisdom_of_a_sage":    return PassiveSkillType.TheWisdomOfASage;
                case "flame_blast":             return PassiveSkillType.FlameBlast;

                // ── 신규 3인 (2026-08-21) ──
                //   ⚠ 표의 enum 두 개에 <b>둥근 따옴표(U+2019)</b>가 들어 있다
                //     (`The_Legion’s_Shield` · `The_Reaper’s_Scythe`) — 엑셀 자동 고침 때문이다.
                //     C# enum 이름에 못 쓰는 글자라 <see cref="Normalize"/> 가 <b>따옴표를 지우고</b>
                //     비교한다. 그래서 표를 안 고쳐도 맞고, 곧은 따옴표로 바뀌어도 맞는다.
                case "strong_mind":             return PassiveSkillType.StrongMind;
                case "the_legions_shield":      return PassiveSkillType.LegionsShield;
                case "legions_shield":          return PassiveSkillType.LegionsShield;
                case "blessing_of_four_wings":  return PassiveSkillType.BlessingOfFourWings;
                case "evasive_maneuver":        return PassiveSkillType.EvasiveManeuver;
                case "sharpshooter":            return PassiveSkillType.Sharpshooter;
                case "declaration_of_the_end":  return PassiveSkillType.DeclarationOfTheEnd;
                case "soul_absorption":         return PassiveSkillType.SoulAbsorption;
                case "the_reapers_scythe":      return PassiveSkillType.ReapersScythe;
                case "reapers_scythe":          return PassiveSkillType.ReapersScythe;
                case "breaking_through_limits": return PassiveSkillType.BreakingThroughLimits;
                default:                return PassiveSkillType.None;
            }
        }
    }
}
