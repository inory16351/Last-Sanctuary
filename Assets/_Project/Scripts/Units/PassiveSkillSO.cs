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

        [Tooltip("스킬 이름 (한글)")]
        public string skillName = "";

        [Tooltip("Skill_Type 시트의 enum 값. 나중에 효과를 구현할 때 이 문자열로 분기한다")]
        public string skillType = "";

        [Header("수치 — 효과 정의문의 {value_01~03} 에 채워진다")]
        public float value01;
        public float value02;
        public float value03;

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
                        Debug.LogWarning($"[Passive] 아이콘 'Resources/SkillIcons/{iconName}' 을 찾지 못했습니다. ({skillName})", this);
                }
                return _icon;
            }
        }

        /// <summary>정의문의 {value_01~03} 자리표시를 실제 수치로 치환한 문장.</summary>
        public string EffectText()
        {
            if (string.IsNullOrEmpty(effectTemplate)) return "";
            return effectTemplate
                .Replace("{value_01}", Num(value01))
                .Replace("{value_02}", Num(value02))
                .Replace("{value_03}", Num(value03));
        }

        /// <summary>소수점이 없으면 정수로 보여준다 — "3.0배" 대신 "3배".</summary>
        static string Num(float v) =>
            Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.##");

        public bool IsUsable => skillId != 0 && !string.IsNullOrWhiteSpace(skillName);
    }
}
