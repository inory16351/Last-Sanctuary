namespace LastSanctuary.Combat
{
    /// <summary>
    /// 정신 이상 11종. 데이터 테이블 <c>정신 이상 테이블.xlsx</c> 의 <c>mental_error_type</c> 시트와
    /// 1:1 대응한다 (시트의 열거값 이름을 C# 관례로만 바꿨다 — <c>Settle_down</c> → <see cref="SettleDown"/>,
    /// <c>Self_harm</c> → <see cref="SelfHarm"/>).
    ///
    /// 각 항목의 실제 수치(적용값·지속시간·발동확률·발동 후 침식)는 이 enum 이 아니라
    /// <see cref="MentalErrorDefinitionSO"/> 에셋이 들고 있다 — 밸런스를 코드 수정 없이
    /// 에디터에서 고칠 수 있게 하려는 것이고, 이 프로젝트가 몬스터·건물에 이미 쓰는 방식이다.
    /// </summary>
    public enum MentalErrorType
    {
        None = 0,

        /// <summary>혼란 — 아군을 공격한다.</summary>
        Confusion = 1,

        /// <summary>진정 — 주변 아군의 침식 수치를 낮춘다(본인 제외). 즉발.</summary>
        SettleDown = 2,

        /// <summary>각성 — 모든 능력치가 일정 % 상승한다.</summary>
        Arousal = 3,

        /// <summary>공포 — 전투를 거부하고 넥서스 방향으로 회피한다.</summary>
        Terrified = 4,

        /// <summary>우울 — 주변 아군의 침식 수치를 올린다(본인 제외). 즉발.</summary>
        Depression = 5,

        /// <summary>광분 — 전술 지침을 거부하고 전방의 적을 쫓아가며 공격한다.</summary>
        Madness = 6,

        /// <summary>고조 — 자원 소모 없이 강화된다(강화 횟수=비용은 정상적으로 오른다). 즉발.</summary>
        Upsurge = 7,

        /// <summary>자해 — 현재 체력이 최대 체력의 일정 % 만큼 즉시 감소한다. 즉발.</summary>
        SelfHarm = 8,

        /// <summary>피학 — 현재 체력이 초당 최대 체력의 일정 % 만큼 감소한다.</summary>
        Masochism = 9,

        /// <summary>이기심 — 치유를 받지 못한다(본인의 체력 재생은 계속된다).</summary>
        Selfishness = 10,

        /// <summary>역겨움 — 주변 아군의 체력이 초당 최대 체력의 일정 % 만큼 감소한다.</summary>
        Disgusting = 11,
    }

    public static class MentalErrorTypes
    {
        /// <summary>
        /// <b>유저에게 이득을 주는 효과인지.</b> 데이터 테이블은 이 구분을 컬럼으로 갖고 있지 않고
        /// 문서(진행상황 54-6절 표)에서 <c>(+)</c> 로만 표시한다 — 진정·각성·고조 셋이다.
        ///
        /// 코드에서 판정하는 이유: 패시브 '강철의 의지'(비기오르)의 정의문이
        /// <b>"좋은 효과가 발동할 확률이 N배 높다"</b> 이므로 이 구분이 없으면 그 스킬을 구현할 수 없다.
        /// 표에 컬럼을 새로 만들지 않은 것은, 이 분류가 <b>밸런스 값이 아니라 효과의 성질</b>이라
        /// 값이 바뀔 여지가 없기 때문이다(각성이 나쁜 효과가 되는 일은 없다).
        /// 새 종류를 추가하면 여기도 같이 고쳐야 한다.
        /// </summary>
        public static bool IsGood(MentalErrorType type) =>
            type == MentalErrorType.SettleDown ||
            type == MentalErrorType.Arousal ||
            type == MentalErrorType.Upsurge;
    }

    /// <summary>
    /// 정신 이상이 캐릭터의 <b>이동·임무 결정</b>을 어떻게 가로채는지.
    /// <c>CharacterBehavior</c> 가 이 값을 보고 평소 임무 판단(정찰·방어·집결·건설·사냥)을
    /// 건너뛴다 — 효과별 세부 동작은 <see cref="CharacterErosion"/> 이 담당한다.
    ///
    /// 행동 오버라이드가 걸린 동안에는 <b>전술 지침의 후퇴 기준도 무시</b>한다.
    /// 세 상태(혼란·공포·광분) 모두 "지침을 따르지 않는 상태"라는 것이 그 정의이기 때문이다.
    /// </summary>
    public enum MentalOverride
    {
        /// <summary>정상 — 평소 임무 판단을 그대로 한다.</summary>
        None = 0,

        /// <summary>혼란 — 이동은 제자리 유지, 타겟만 아군으로 강제된다.</summary>
        AttackAllies,

        /// <summary>공포 — 전투를 거부하고 넥서스 쪽으로 회피한다(후퇴 로직 재사용).</summary>
        Flee,

        /// <summary>광분 — 전방의 적을 향해 달려나간다.</summary>
        Charge,
    }

    /// <summary>
    /// 한글 조사(助詞) 자동 선택. 로그 문구가 <c>"OO이 / 가 XX 상태에 빠집니다."</c> 형식이라
    /// 이름의 마지막 글자에 받침이 있는지에 따라 "이" 와 "가" 를 골라야 한다.
    ///
    /// 캐릭터 이름이 숫자로 끝나는 경우(<c>백혈구_1</c> 등)가 흔해서 숫자도 읽는 소리 기준으로
    /// 처리한다 — 1(일)·3(삼)·6(육)·7(칠)·8(팔)·0(영)은 받침이 있고, 2(이)·4(사)·5(오)·9(구)는 없다.
    /// </summary>
    public static class KoreanParticle
    {
        /// <summary>이름 뒤에 붙일 주격 조사 — 받침이 있으면 "이", 없으면 "가".</summary>
        public static string IGa(string name) => HasFinalConsonant(name) ? "이" : "가";

        /// <summary>이름 + 주격 조사. 표시 문구를 만드는 쪽이 매번 붙이지 않도록 묶어둔다.</summary>
        public static string WithIGa(string name) => name + IGa(name);

        /// <summary>마지막 글자에 받침이 있는지. 판단할 수 없는 문자는 받침이 있다고 본다("이").</summary>
        static bool HasFinalConsonant(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;

            char last = name[name.Length - 1];

            // 완성형 한글 음절: (코드포인트 - 0xAC00) % 28 == 0 이면 종성이 없다.
            if (last >= '가' && last <= '힣')
                return (last - '가') % 28 != 0;

            if (last >= '0' && last <= '9')
            {
                // 일 삼 육 칠 팔 영 → 받침 있음 / 이 사 오 구 → 없음
                switch (last)
                {
                    case '2': case '4': case '5': case '9': return false;
                    default: return true;
                }
            }

            return true;
        }
    }
}
