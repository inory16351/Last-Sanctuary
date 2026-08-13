using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 보스 스킬 한 줄 = 에셋 하나. 원본은 <c>웨이브 몬스터 테이블.xlsx</c> 의 <c>Skill</c> 시트이고
    /// <c>Tools/sync_tables_to_assets.py</c> 가 그대로 옮긴다 — <b>값을 손으로 적지 않는다.</b>
    ///
    /// <b>왜 <c>Resources</c> 폴더인가</b> — MCP 로는 씬에 오브젝트 참조를 써넣을 수 없어서
    /// (진행상황 8절 4번), 이 프로젝트는 스킨·BGM·정신 이상을 전부 <c>Resources.LoadAll</c> 로
    /// 배선해 왔다(25-5·27-1·29-3절). 같은 방식이라 <b>스킬을 추가할 때 씬을 건드릴 필요가 없다</b> —
    /// 표에 줄을 하나 더 쓰고 파이프라인을 돌리면 끝이다.
    ///
    /// <b>값의 뜻</b> (스트링 테이블 <c>skill_type_desc_*</c> 그대로):
    /// "가장 가까운/먼 적을 향해 단탈리온이 존재하는 칸을 포함하여 {value_01}(가로) x
    /// {value_02}(세로) 범위의 적을 단탈리온의 근거리 공격력{value_03}%로 공격한다"
    /// </summary>
    [CreateAssetMenu(fileName = "BossSkill_", menuName = "Last Sanctuary/Boss Skill")]
    public class BossSkillSO : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("표의 skill_id. 몬스터 정의(MonsterDefinitionSO.bossSkillIds)가 이 번호로 참조한다")]
        public int skillId;

        [Tooltip("스트링 키 (예: skill_name_130001)")]
        public string nameKey = "";

        [Tooltip("⚠ nameKey 폴백용. 문구는 스트링 키 테이블에서 고칠 것")]
        public string displayName = "";

        [Tooltip("표의 skill_type 칸 문자열. BossSkillTypes.Parse 가 enum 으로 옮긴다")]
        public string skillType = "";

        [Tooltip("스트링 키 (예: skill_explain_130001)")]
        public string explainKey = "";

        [Header("수치 (표 그대로)")]
        [Tooltip("value_01 — 범위 <b>가로</b>(타일). 보스 자기 칸을 포함해 조준 방향으로 뻗는 길이다")]
        public float value01;

        [Tooltip("value_02 — 범위 <b>세로</b>(타일). 조준 방향과 직각인 두께다")]
        public float value02;

        [Tooltip("value_03 — 피해량(근거리 공격력의 %). 150 이면 평타의 1.5배")]
        public float value03;

        [Tooltip("cool_time — 재사용 대기시간(초)")]
        public float coolTime = 10f;

        /// <summary>분기용 식별자. 문자열 비교는 여기서 한 번만 한다.</summary>
        public BossSkillType Type => BossSkillTypes.Parse(skillType);

        /// <summary>화면·로그에 쓸 이름 — 스트링 테이블이 먼저, 없으면 리터럴.</summary>
        public string DisplayName => Data.StringTable.Get(nameKey, string.IsNullOrEmpty(displayName)
            ? name : displayName);

        /// <summary>설명 문구(툴팁·로그용).</summary>
        public string Explain => Data.StringTable.Get(explainKey, string.Empty);

        /// <summary>범위 가로(타일) — 조준 방향으로 뻗는 길이. 최소 1칸은 보장한다.</summary>
        public float LengthTiles => Mathf.Max(1f, value01);

        /// <summary>범위 세로(타일) — 조준 방향과 직각인 두께.</summary>
        public float WidthTiles => Mathf.Max(1f, value02);

        /// <summary>피해 배율(%). 표가 비어 있으면 평타(100%)로 떨어진다.</summary>
        public int DamagePercent => value03 > 0f ? Mathf.RoundToInt(value03) : 100;

        /// <summary>이 에셋이 쓸 만한지 — 종류를 못 알아보면 시전하지 않는다.</summary>
        public bool IsUsable => Type != BossSkillType.None && coolTime > 0f;
    }
}
