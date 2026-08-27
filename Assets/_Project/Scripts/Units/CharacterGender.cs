namespace LastSanctuary.Units
{
    /// <summary>
    /// ★★ <b>인물의 성별</b> (2026-08-27 신설 · 유저 지시: *"캐릭터 시트에 남녀 표기 칼럼 하나
    /// 추가하고 일러스트 바탕으로 파악해서 남녀 기입 … 2번째 생성부터 캐릭터 이름 랜덤으로
    /// 들어가는 거 남녀 구분해서 남캐는 남자 이름 여캐는 여자이름으로"*).
    ///
    /// <b>지금 쓰이는 곳은 «다른 이름» 뽑기 한 곳뿐</b>이다
    /// (<see cref="CharacterAltNames.RegisterAppearance"/>) — 능력치·전투·UI 는 성별을 보지 않는다.
    /// 화면에 성별을 표기하는 자리도 아직 없다(그래서 스트링 키도 만들지 않았다).
    ///
    /// <b>정본은 표다</b> — <c>캐릭터 테이블.xlsx</c> 의 <c>Character</c> 시트 <c>gender</c> 칸
    /// (<c>male</c> / <c>female</c>)이고, <c>Tools/gen_character_assets.py</c> 가 이 enum 의
    /// 정수로 옮겨 적는다. 대체 이름 쪽 정본은 <c>대체 이름 테이블.xlsx</c> 의 같은 이름 칸이다.
    ///
    /// ⚠ <b><see cref="Unknown"/> 이 0 인 이유</b> — 표에 칸이 비었거나 옛 에셋이라 값이
    ///   직렬화돼 있지 않으면 C# 기본값 0 이 그대로 쓰인다(183-2절의 «지연 폭탄» 과 같은 자리).
    ///   그때 «남자» 로 읽히면 여성 인물이 조용히 남자 이름을 받는다. 0 은 «모른다» 여야 하고,
    ///   모르면 <b>성별을 안 가리고</b> 뽑는다(= 이 기능이 생기기 전과 같은 동작).
    /// </summary>
    public enum CharacterGender
    {
        /// <summary>표에 값이 없다 — 성별을 안 가리고 이름을 뽑는다(예전 동작).</summary>
        Unknown = 0,

        /// <summary>표의 <c>male</c>.</summary>
        Male = 1,

        /// <summary>표의 <c>female</c>.</summary>
        Female = 2,
    }

    /// <summary>표의 글자(<c>male</c>/<c>female</c>)와 <see cref="CharacterGender"/> 사이의 변환.</summary>
    public static class CharacterGenderText
    {
        /// <summary>
        /// 표에서 읽은 글자를 enum 으로. 모르는 글자는 <see cref="CharacterGender.Unknown"/> 이다
        /// — <b>일부러 예외를 던지지 않는다</b>. 데이터 한 칸의 오타로 게임이 안 뜨는 것보다,
        /// 그 인물만 예전처럼 아무 이름이나 받는 편이 낫다(생성기 쪽에서 오타를 잡는다).
        /// </summary>
        public static CharacterGender Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return CharacterGender.Unknown;
            switch (text.Trim().ToLowerInvariant())
            {
                case "m":
                case "male":
                case "남":
                case "남자": return CharacterGender.Male;
                case "f":
                case "female":
                case "여":
                case "여자": return CharacterGender.Female;
                default: return CharacterGender.Unknown;
            }
        }
    }
}
