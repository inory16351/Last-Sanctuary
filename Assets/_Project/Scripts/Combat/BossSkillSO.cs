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
    /// {value_02}(세로) 범위의 적을 단탈리온의 근거리 공격력{value_03}%로 공격한다.
    /// 맞은 적은 침식이 {value_04} 만큼 오른다."
    ///
    /// <b>value_04(침식)는 2026-08-13 유저 지시로 추가된 컬럼</b> — 기존 9칸 뒤에 그대로
    /// 붙였다(순서·형식을 바꾸지 않는다). 이전에는 "보스 공격에 맞으면 침식 +10"을
    /// <c>GameSystems/ErosionService</c> 의 전역 상수로 뒀었는데, **스킬마다 침식량이
    /// 다르다**(타락한 무덤 5 · 공허의 광선 10)는 요구가 나와서 시스템 값이 아니라
    /// <b>스킬 데이터 자체</b>로 옮겼다 — 시스템 로직을 늘리지 않고 표 한 칸으로 표현된다.
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

        [Tooltip("value_04 — 이 스킬에 맞은 캐릭터가 즉시 얻는 침식 수치. 표에 새로 추가된 " +
                 "컬럼(2026-08-13, 기존 9칸 뒤에 그대로 붙였다) — 유저 확정: 타락한 무덤 5 / " +
                 "공허의 광선 10.\n" +
                 "⚠ 이 값은 시스템(GameSystems/ErosionService)이 아니라 <b>스킬 데이터</b>가 들고 " +
                 "있다 — 보스 스킬마다 침식량이 다르므로 시스템 전역 상수로 두면 이 차이를 표현할 " +
                 "수 없다. 적용은 CharacterErosion.AddErosion 을 직접 부르는 " +
                 "BossSkillCaster.TryCast 가 한다")]
        public float value04;

        [Tooltip("value_05 — 표의 다섯 번째 값 칸. <b>스킬 종류마다 뜻이 다르다</b>:\n" +
                 "  · 할퀴기(2001)   — 방어력 감소가 지속되는 <b>초</b>\n" +
                 "  · 구속탄(130003) — 「허약」이 지속되는 <b>초</b>\n" +
                 "  · 나머지          — 안 쓴다\n" +
                 "⚠ 칸 번호로 읽지 말고 아래 뜻 있는 프로퍼티(DefenseDownSeconds 등)를 쓸 것")]
        public float value05;

        [Tooltip("value_06 — 표의 여섯 번째 값 칸 (2026-08-18 신설).\n" +
                 "  · 구속탄(130003) — 「구속」이 지속되는 <b>초</b>\n" +
                 "  · 나머지          — 안 쓴다")]
        public float value06;

        [Tooltip("★ mentalerror_damage — 이 스킬에 맞은 캐릭터가 즉시 얻는 <b>침식 수치</b>.\n\n" +
                 "⚠ <b>예전에는 value_04 를 이 뜻으로 읽었다</b>(2026-08-13 에 그렇게 붙였다). " +
                 "그 뒤 표에 value_04·05·06 이 <b>진짜 수치 칸</b>으로 채워지고 침식은 " +
                 "`mentalerror_damage` 라는 <b>자기 컬럼</b>을 갖게 되었는데, 파이프라인이 " +
                 "따라가지 못해 값이 3칸씩 밀려 있었다(2026-08-18 발견 — 그래서 " +
                 "단탈리온 두 스킬의 coolTime 이 0 이 되어 <b>한 번도 발동하지 않았다</b>).\n" +
                 "이제 이 칸이 침식의 유일한 출처다 — value_04 를 침식으로 읽지 말 것")]
        public float erosionValue;

        [Tooltip("cool_time — 재사용 대기시간(초)")]
        public float coolTime = 10f;

        [Header("연출 · 범위 모양 (2026-08-13 신설)")]
        [Tooltip("cast_time — <b>이 스킬</b>의 시전 모션·범위 연출이 화면에 머무는 시간(초).\n" +
                 "예전에는 BossSkillCaster 의 castSeconds 하나로 <b>모든 스킬이 같은 시간</b>이었다 " +
                 "— 유저 지시(2026-08-13) \"에딧 모드에서 Cast Seconds 를 각 스킬마다 설정할 수 " +
                 "있게\" 로 스킬 데이터로 내렸다. 공허의 광선처럼 길게 보여야 하는 기술은 이 값만 " +
                 "키우면 된다.\n" +
                 "⚠ 피해는 시전과 <b>동시에</b> 들어간다 — 이 값은 연출 길이일 뿐 판정 시점이 아니다.\n" +
                 "0 이면 BossSkillCaster 의 castSeconds(전역 기본값)로 떨어진다")]
        [Min(0f)] public float castSeconds;

        [Tooltip("range_type — 범위 모양. 'Line'(기본) = 조준 방향으로 뻗는 직사각형(각도 제한 없음), " +
                 "'Circle' = 보스 중심 원형(지름 = value_01).\n" +
                 "표에서 이 칸만 바꾸면 코드 수정 없이 모양이 바뀐다")]
        public string rangeType = "";

        /// <summary>분기용 식별자. 문자열 비교는 여기서 한 번만 한다.</summary>
        public BossSkillType Type => BossSkillTypes.Parse(skillType);

        /// <summary>범위 모양. 표의 <c>range_type</c> 칸이 비어 있으면 직사각형이다.</summary>
        public BossSkillShape Shape => BossSkillShapes.Parse(rangeType);

        /// <summary>
        /// 이 스킬의 연출 길이(초). 표에 값이 없으면 <paramref name="fallbackSeconds"/>
        /// (<see cref="BossSkillCaster"/> 인스펙터의 전역 기본값)로 떨어진다.
        /// </summary>
        public float CastSecondsOr(float fallbackSeconds) =>
            castSeconds > 0f ? castSeconds : fallbackSeconds;

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

        /// <summary>
        /// 이 스킬에 맞으면 오르는 침식 수치. 음수는 0 으로 자른다.
        ///
        /// ★ 2026-08-18 — <b>종류로 갈라 읽던 것을 없앴다.</b> 예전에는 "단탈리온의 두 스킬만
        /// <c>value_04</c> 가 침식" 이라 종류를 봐야 했는데, 표가 <c>mentalerror_damage</c>
        /// 라는 <b>전용 칸</b>을 갖게 되면서 그럴 이유가 사라졌다 — 모든 스킬이 같은 칸을 쓴다.
        /// </summary>
        public float ErosionValue => Mathf.Max(0f, erosionValue);

        // ──────────────────────────────────────────────────────────────────
        // 카르시노스 — 칸 번호가 아니라 <b>뜻</b>으로 읽는 프로퍼티 (2026-08-15)
        //
        // 스트링 테이블의 정의문이 근거다:
        //   할퀴기      "…맞은 적은 방어력이{value_04}% 만큼 감소하며 지속시간은 {value_05}초…"
        //   죽음의 포효 "…카르시노스 + {value_01} 반지름 타일 범위… 뒤로 {value_02} 타일 만큼 밀려나며…"
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// 맞은 적의 <b>방어력을 몇 % 깎는지</b>. 할퀴기 전용 — 다른 스킬은 0.
        /// </summary>
        public float DefenseDownPercent =>
            Type == BossSkillType.Scratch ? Mathf.Max(0f, value04) : 0f;

        /// <summary>방어력 감소가 지속되는 초. 할퀴기 전용.</summary>
        public float DefenseDownSeconds =>
            Type == BossSkillType.Scratch ? Mathf.Max(0f, value05) : 0f;

        /// <summary>
        /// 맞은 적을 <b>뒤로 몇 타일</b> 밀어내는지. 죽음의 포효 전용 — 다른 스킬은 0.
        /// </summary>
        public float KnockbackTiles =>
            Type == BossSkillType.RoarDeath ? Mathf.Max(0f, value02) : 0f;

        // ──────────────────────────────────────────────────────────────────
        // 말파스 — 구속탄 (2026-08-18)
        //
        // 스트링 테이블 `skill_type_desc_Binding_orb` 그대로:
        //   "…{value_01} 반지름 타일 범위의 투사체를 날리고 맞은 적을 기준으로
        //    {value_02} 반지름 타일 범위의 모든 적에게 {value_03}% 만큼 피해를 입히고
        //    공격속도를 {value_04}% 만큼 감소시키는 '허약' 상태로 만든다.
        //    허약 상태는 {value_05}초 만큼 지속되며, 허약 상태의 적이 해당 공격에 다시
        //    피격 시 즉시 허약 상태를 해제하고 {value_06}초 만큼 이동과 공격이 불가능한
        //    '구속' 상태로 만든다."
        // ──────────────────────────────────────────────────────────────────

        /// <summary>구속탄이 터지는 <b>반지름</b>(타일). 다른 스킬은 0.</summary>
        public float BlastRadiusTiles =>
            Type == BossSkillType.BindingOrb ? Mathf.Max(0.5f, value02) : 0f;

        /// <summary>「허약」이 깎는 <b>공격속도 %</b>. 구속탄 전용.</summary>
        public float WeakenAttackSpeedPercent =>
            Type == BossSkillType.BindingOrb ? Mathf.Max(0f, value04) : 0f;

        /// <summary>「허약」이 지속되는 초. 구속탄 전용.</summary>
        public float WeakenSeconds =>
            Type == BossSkillType.BindingOrb ? Mathf.Max(0f, value05) : 0f;

        /// <summary>「구속」이 지속되는 초 — <b>허약 상태에서 또 맞았을 때만</b>. 구속탄 전용.</summary>
        public float BindSeconds =>
            Type == BossSkillType.BindingOrb ? Mathf.Max(0f, value06) : 0f;

        /// <summary>
        /// <b>원형 범위의 반지름</b>(타일)이 <c>value_01</c> 로 적혀 있는 스킬인가.
        ///
        /// ⚠ 여기가 갈린다 — 기존 <see cref="BossSkillShape.Circle"/> 는 <c>value_01</c> 을
        /// <b>지름</b>으로 읽는다(단탈리온 기준). 그런데 카르시노스 「죽음의 포효」의 정의문은
        /// *"카르시노스 + {value_01} 반지름 타일 범위"* 라 <b>반지름</b>이다. 값을 그대로 쓰면
        /// 실제 범위가 <b>절반</b>이 되어 표와 화면이 어긋난다.
        /// </summary>
        public bool CircleValueIsRadius => Type == BossSkillType.RoarDeath;

        /// <summary>이 에셋이 쓸 만한지 — 종류를 못 알아보면 시전하지 않는다.</summary>
        public bool IsUsable => Type != BossSkillType.None && coolTime > 0f;
    }
}
