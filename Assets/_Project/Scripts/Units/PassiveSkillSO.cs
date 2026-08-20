using UnityEngine;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 캐릭터 테이블(<c>캐릭터 테이블.xlsx</c>)의 <b>Skill</b> · <b>Skill_Type</b> 두 시트를 합친 데이터.
    ///
    /// 한 에셋이 스킬 한 줄이다:
    /// <list type="bullet">
    /// <item>Skill 시트 → id · 이름 · 타입 · value_01~03 · 쿨타임 · 아이콘 · 설명(플레이버)</item>
    /// <item>Skill_Type 시트 → 실제 효과 정의문(<see cref="effectTemplate"/>)</item>
    /// </list>
    ///
    /// 효과 정의문에는 <c>{value_01}</c> 같은 자리표시가 들어 있고, <see cref="EffectText"/> 가
    /// 실제 수치로 치환한다 — 테이블 문구를 그대로 옮겨 적으면 UI 에 수치가 채워져 나온다.
    ///
    /// ⚠ <b>이 에셋은 아직 "표시용 데이터"다.</b> 실제 전투 효과는 붙어 있지 않다 —
    /// 캐릭터 성장 창에서 아이콘·이름·설명·해금 여부를 보여주는 데까지만 쓰인다.
    /// 효과 구현은 <see cref="skillType"/> 문자열로 분기하는 별도 작업이다.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/Units/Passive Skill", fileName = "Skill_")]
    public class PassiveSkillSO : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("캐릭터 테이블 Skill 시트의 skill_id")]
        public int skillId;

        [Header("스트링 키 (스트링 키 테이블.xlsx)")]
        [Tooltip("스킬 이름 키. 예: skill_name_80001")]
        public string nameKey = "";

        [Tooltip("플레이버 문장 키. 예: skill_explain_80001")]
        public string flavorKey = "";

        [Tooltip("효과 정의문 키. 예: skill_type_desc_Innate_delicacy\n" +
                 "★ 이 문구는 {value_01} 같은 자리표를 담고 있고 EffectText() 가 수치로 바꾼다")]
        public string effectKey = "";

        [Header("문구 (⚠ 위 키를 못 찾았을 때의 폴백)")]
        [Tooltip("스킬 이름 (한글). 표시에는 DisplayName 을 쓸 것")]
        public string skillName = "";

        [Tooltip("Skill_Type 시트의 enum 값. 나중에 효과를 구현할 때 이 문자열로 분기한다.\n" +
                 "⚠ 이건 문구가 아니라 <b>분기용 식별자</b>다 — 스트링 키로 빼지 않는다")]
        public string skillType = "";

        [Header("수치 — 효과 정의문의 {value_01~04} 에 채워진다")]
        public float value01;
        public float value02;
        public float value03;

        [Tooltip("★ 2026-08-20 신설 — 표에 `value_04` 컬럼이 있는데 여기 받을 칸이 없어서 " +
                 "그동안 <b>버려지고 있었다</b>. 시그리드 「가학증」이 첫 사용자다 " +
                 "(아군 회복량 = 시그리드 현재 체력의 value04%).\n" +
                 "⚠ 이 칸이 없던 동안 `gen_character_assets.py` 가 컬럼을 <b>번호로</b> 읽어 " +
                 "쿨타임·아이콘이 한 칸씩 밀릴 상태였다 — 같이 고쳤다(그쪽 주석 참조)")]
        public float value04;

        [Tooltip("★ 2026-08-20 신설 (유저가 표에 `value_05` 컬럼을 추가). 첫 사용자는 " +
                 "시그리드 「가학증」의 마지막 문장이다: <b>「시그리드의 후퇴기준이 " +
                 "{Value_05}%로 고정됩니다」</b>.\n" +
                 "⚠ 컬럼을 <b>이름으로</b> 읽으므로(gen_character_assets.py) 칸이 하나 더 " +
                 "늘어도 뒤의 쿨타임·아이콘이 밀리지 않는다 — 그게 value_04 때 났던 사고다")]
        public float value05;

        [Tooltip("★ 2026-08-20 신설 (신규 캐릭터 3인). 첫 사용자는 카이론 「천벌」(80027)의 " +
                 "마지막 문장이다: <b>「피해를 당한 적은 {value_05}초 동안 방어력이 " +
                 "{value_06}% 감소합니다」</b>. 표에 이미 있던 컬럼이라 값만 받아 온다")]
        public float value06;

        [Tooltip("쿨타임(초). 0 이면 상시 발동")]
        public float coolTime;

        [Header("표시")]
        [Tooltip("Resources/SkillIcons/ 아래의 파일 이름 (확장자 없이). " +
                 "스프라이트 참조는 MCP 로 씬/에셋에 넣을 수 없어 경로 문자열로 다룬다 — 진행상황 8절 1번")]
        public string iconName = "";

        [Tooltip("Skill 시트의 skill_explain — 캐릭터를 설명하는 플레이버 문장. " +
                 "캐릭터 성장 창의 스킬 칸에 이걸 보여준다")]
        [TextArea(2, 4)] public string flavorText = "";

        [Tooltip("Skill_Type 시트의 정의문 — 실제로 무슨 일이 일어나는지. " +
                 "{value_01} 같은 자리표시를 그대로 두면 EffectText 가 수치로 바꿔준다. " +
                 "스킬 칸을 클릭했을 때 뜨는 상세 창에 보여준다")]
        [TextArea(3, 8)] public string effectTemplate = "";

        Sprite _icon;
        bool _iconLoaded;

        /// <summary>
        /// 아이콘 스프라이트. <c>Resources/SkillIcons/</c> 에서 이름으로 읽어 캐시한다.
        /// 못 찾으면 null 을 돌려주고, UI 는 그 경우 이름 텍스트로 대체한다.
        /// </summary>
        public Sprite Icon
        {
            get
            {
                if (_iconLoaded) return _icon;
                _iconLoaded = true;
                if (!string.IsNullOrWhiteSpace(iconName))
                {
                    _icon = Resources.Load<Sprite>("SkillIcons/" + iconName.Trim());
                    if (_icon == null)
                        Debug.LogWarning($"[Passive] 아이콘 'Resources/SkillIcons/{iconName}' 을 찾지 못했습니다. ({DisplayName})", this);
                }
                return _icon;
            }
        }

        /// <summary>화면에 보여줄 스킬 이름 — 스트링 테이블이 먼저, 없으면 리터럴.</summary>
        public string DisplayName => Data.StringTable.Get(nameKey, skillName);

        /// <summary>플레이버 문장 — 스트링 테이블이 먼저, 없으면 리터럴.</summary>
        public string FlavorText => Data.StringTable.Get(flavorKey, flavorText);

        /// <summary>
        /// 정의문의 {value_01~03} 자리표시를 실제 수치로 치환한 문장.
        /// <b>문구 원본은 스트링 테이블</b>이고(<see cref="effectKey"/>),
        /// 키가 없으면 <see cref="effectTemplate"/> 리터럴로 폴백한다 — 치환 규칙은 같다.
        /// </summary>
        public string EffectText()
        {
            string template = Data.StringTable.Get(effectKey, effectTemplate);
            if (string.IsNullOrEmpty(template)) return "";

            // ★★ 2026-08-20 — <b>여섯 개까지 · 대소문자를 가리지 않고</b> 치환한다.
            //
            //   ① 예전에는 01~03 만 바꿨다. 그런데 표의 정의문은 이미 <b>{value_06} 까지</b>
            //      쓰고 있다(카이론 「천벌」). 안 바꾸면 상세 카드에 «{value_04}% 의 데미지»
            //      처럼 <b>자리표시가 그대로</b> 뜬다.
            //   ② 표에는 <b>{Value_02}</b> 처럼 대문자로 적힌 자리표시가 실재한다
            //      (시카리아 「고조된 감각」·「애로우 레인」). 사람이 손으로 적는 칸이라
            //      대소문자를 강제할 수 없으므로 <b>읽는 쪽이 관대해야 한다</b> —
            //      `skillType` 을 Trim 하는 것과 같은 판단이다(PassiveSkillType 주석).
            float[] values = { value01, value02, value03, value04, value05, value06 };
            for (int i = 0; i < values.Length; i++)
                template = ReplaceCaseInsensitive(template, "{value_0" + (i + 1) + "}", Num(values[i]));
            return template;
        }

        /// <summary>대소문자를 가리지 않는 치환 (위 ②). 못 찾으면 원문 그대로.</summary>
        static string ReplaceCaseInsensitive(string text, string token, string value)
        {
            int at = text.IndexOf(token, System.StringComparison.OrdinalIgnoreCase);
            while (at >= 0)
            {
                text = text.Substring(0, at) + value + text.Substring(at + token.Length);
                at = text.IndexOf(token, at + value.Length, System.StringComparison.OrdinalIgnoreCase);
            }
            return text;
        }

        /// <summary>소수점이 없으면 정수로 보여준다 — "3.0배" 대신 "3배".</summary>
        static string Num(float v) =>
            Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.##");

        /// <summary>
        /// 쓸 수 있는 스킬인지. <b>이름이 어느 경로로든 잡히면</b> 쓸 수 있다 —
        /// 스트링 키만 있고 리터럴이 비어 있는 에셋(이제 생성되는 형태)도 통과해야 한다.
        /// </summary>
        public bool IsUsable =>
            skillId != 0 &&
            (!string.IsNullOrWhiteSpace(skillName) || !string.IsNullOrWhiteSpace(nameKey));
    }
}
