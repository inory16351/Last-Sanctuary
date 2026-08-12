using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 정신 이상 한 종류의 데이터. <c>데이터 테이블/정신 이상 테이블.xlsx</c> 의
    /// <c>mental_error</c> 시트 한 줄이 이 에셋 하나에 대응한다.
    ///
    /// <b>왜 Resources 에 두는가</b> — 이 에셋은 씬 오브젝트의 인스펙터가 아니라
    /// <see cref="ErosionService"/> 가 <c>Resources.LoadAll</c> 로 폴더째 읽는다. MCP 로는 씬에
    /// 오브젝트 참조를 써넣을 수 없어서(진행상황 8절 4번), 이 프로젝트는 스킨·BGM·투사체를
    /// 전부 이 방식으로 배선해 왔다(진행상황 25-5·27-1·27-3절). 정신 이상도 같은 방식을 따르므로
    /// <b>새 종류를 추가할 때 씬을 건드릴 필요가 전혀 없다</b> — 에셋을 폴더에 넣으면 끝이다.
    ///
    /// <b>발동 확률의 의미</b> — 테이블의 11종 확률 합이 정확히 1.00 이다(0.1125×8 + 0.04×2 + 0.02).
    /// 즉 이 값은 "매 초 이 확률로 발동" 이 아니라 <b>침식이 상한에 닿았을 때 어느 종류가
    /// 뽑히는지를 정하는 가중치</b>다. <see cref="ErosionService.RollDefinition"/> 가 그렇게 쓴다.
    /// </summary>
    [CreateAssetMenu(fileName = "MentalError", menuName = "Last Sanctuary/정신 이상 정의")]
    public class MentalErrorDefinitionSO : ScriptableObject
    {
        [Header("식별 (테이블 mental_error_id / mental_error_type)")]
        [Tooltip("테이블의 mental_error_id — 40001~40011")]
        public int mentalErrorId;

        public MentalErrorType type = MentalErrorType.None;

        [Tooltip("스트링 키 (스트링 키 테이블.xlsx). 예: mental_error_name_40001\n" +
                 "비워두면 아래 koreanName 리터럴을 쓴다(하위 호환)")]
        public string nameKey = "";

        [Tooltip("테이블의 '한글 설명'(Korean_explain). 로그와 로스터 표기에 쓰인다 — " +
                 "\"OO이/가 «이 값» 상태에 빠집니다.\"\n" +
                 "⚠ 스트링 테이블 도입 이후로는 nameKey 폴백용이다")]
        public string koreanName = "";

        [Header("적용값 (의미는 종류마다 다르다 — 아래 주석 참조)")]
        [Tooltip("테이블 적용값_01.\n" +
                 "진정·우울: 효과 반경(타일) / 각성: 능력치 상승 % / 고조: 무료 강화 횟수\n" +
                 "자해: 최대 체력의 감소 % / 피학: 최대 체력의 초당 감소 % / 역겨움: 효과 반경(타일)")]
        public float value01;

        [Tooltip("테이블 적용값_02.\n" +
                 "진정: 낮추는 침식량 / 우울: 올리는 침식량 / 역겨움: 아군 최대 체력의 초당 감소 %\n" +
                 "그 외에는 쓰지 않는다(0)")]
        public float value02;

        [Header("지속 / 발동")]
        [Tooltip("지속 시간(초). 0 이면 즉발 — 효과를 한 번 적용하고 바로 끝난다(진정·우울·고조·자해)")]
        [Min(0f)] public float durationSeconds;

        [Tooltip("발동 직후 이 캐릭터의 침식 수치가 이 값으로 내려간다(테이블 after_erosion). " +
                 "상한 100 에서 이 값으로 떨어지므로, 값이 높은 종류일수록 다음 발동이 빨리 온다")]
        [Min(0)] public int afterErosion = 75;

        [Tooltip("추첨 가중치(테이블 activation_probability). 11종 합이 1.00 이 되도록 설계돼 있다")]
        [Min(0f)] public float activationProbability = 0.1f;

        /// <summary>지속 효과 없이 한 번만 적용되는 종류인지.</summary>
        public bool IsInstant => durationSeconds <= 0f;

        /// <summary>
        /// 표시용 이름. <b>스트링 테이블이 먼저</b>고, 키가 없으면 테이블 한글 설명,
        /// 그것도 비어 있으면 열거값 이름으로 떨어진다.
        /// </summary>
        public string DisplayName
        {
            get
            {
                string fallback = string.IsNullOrEmpty(koreanName) ? type.ToString() : koreanName;
                return Data.StringTable.Get(nameKey, fallback);
            }
        }

        public override string ToString() =>
            $"{mentalErrorId} {DisplayName}({type}) · 값 {value01}/{value02} · " +
            $"지속 {durationSeconds:0.#}초 · 발동후 침식 {afterErosion} · 가중치 {activationProbability:0.####}";
    }
}
