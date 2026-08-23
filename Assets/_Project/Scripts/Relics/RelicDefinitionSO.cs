using UnityEngine;

namespace LastSanctuary.Relics
{
    /// <summary>
    /// 유물 등급 — 표 <c>Grade</c> 시트의 <c>grade</c> 3종.
    ///
    /// ★ 등급이 하는 일은 <b>셋</b>이다: ① 얻을 확률 ② 효과의 세기 ③ 화면에서의 색.
    ///   ②는 «같은 효과 타입에 더 큰 수치» 로 표현한다 — 등급마다 <b>다른</b> 효과를 두면
    ///   «레어인데 내 캐릭터엔 쓸모없다» 가 잦아진다(표 Info 시트의 «세기 대역» 항목).
    ///
    /// ⚠ 표에 없는 이름이 오면 <see cref="None"/> 이고 그 유물은 <b>어느 풀에도 안 들어간다</b> —
    ///   조용히 «일반» 으로 떨어지면 기획이 오타를 냈을 때 알 수 없다
    ///   (<c>EventTrigger</c> 가 같은 이유로 같은 규칙을 쓴다).
    /// </summary>
    public enum RelicGrade
    {
        None = 0,
        Common = 1,     // common — 일반
        Rare = 2,       // rare   — 레어
        Epic = 3,       // epic   — 에픽
    }

    /// <summary>
    /// 유물이 <b>어디서 나오는가</b> — 표 <c>Relic</c> 시트의 <c>source</c>.
    /// </summary>
    public enum RelicSource
    {
        None = 0,

        /// <summary><c>dig_monster</c> — 발굴에서도 나오고 일반 몬스터 처치로도 나온다.</summary>
        DigAndMonster = 1,

        /// <summary><c>dig</c> — <b>발굴에서만</b> 나온다(발굴 전용 에픽 둘).</summary>
        DigOnly = 2,

        /// <summary>
        /// <c>boss</c> — <b>그 보스를 잡아야만</b> 나온다.
        /// <see cref="RelicDefinitionSO.sourceId"/> 가 그 보스의 몬스터 ID 다
        /// (웨이브 보스 120001~120006 · 에픽 중립 1101~1104).
        /// </summary>
        Boss = 3,
    }

    /// <summary>
    /// 유물 효과 — 표 <c>EffectType</c> 시트의 enum.
    ///
    /// ★★ <b>규약은 이벤트 보상 타입(<c>RewardType</c> 시트)과 같다</b> — «타입 + 수치» 다
    ///   (유저 지시: *"유물 효과는 기존 스킬이나 이벤트 리워드 타입 처럼 타입 밸류를 쓸 것"*).
    ///   다른 점은 하나뿐이다:
    /// <code>
    ///   이벤트 보상 : 전원에게 · 몇 초 동안        → duration 칸이 있다
    ///   유물        : 장착한 한 명에게 · 장착한 동안 → duration 칸이 없다
    /// </code>
    ///
    /// ⚠ <b>여기 값을 더하면 <see cref="RelicEffectService"/> 에도 반드시 가지가 필요하다.</b>
    ///   표에만 있고 코드에 없는 타입은 <see cref="None"/> 으로 읽히고, 그 유물은
    ///   «장착은 되는데 아무 일도 안 일어나는» 것이 된다. 그래서 등록기가 그런 유물을
    ///   <b>경고로 알린다</b>(<see cref="RelicRegistry"/>).
    /// </summary>
    public enum RelicEffectType
    {
        None = 0,

        // ── 능력치 (장착한 동안 계속) ─────────────────────────────────
        // ★ value_01 = %. 장착 순간의 «지금 능력치» 에 대한 %를 <b>고정 수치</b>로 환산해
        //   더한다(이벤트 보상 ApplyStat 과 같은 방식) — 그래서 뗐다 끼면 값이 다시 계산된다.
        HpUp = 1,
        MeleeAttackUp = 2,
        RangedAttackUp = 3,
        MagicUp = 4,
        DefenseUp = 5,
        ResistanceUp = 6,
        RegenUp = 7,
        CureUp = 8,
        AccuracyUp = 9,
        CriticalUp = 10,
        AttackSpeedUp = 11,
        MoveSpeedUp = 12,

        // ── 조건부·반응 ──────────────────────────────────────────────
        /// <summary>입힌 피해의 v1% 만큼 자신을 회복.</summary>
        Lifesteal = 20,

        /// <summary>받은 <b>근접</b> 피해의 v1% 를 때린 적에게 반사.</summary>
        Thorns = 21,

        /// <summary>처치할 때마다 에너지 +v1 (절대값).</summary>
        KillEnergy = 22,

        /// <summary>처치할 때마다 최대 체력의 v1% 회복.</summary>
        KillHeal = 23,

        /// <summary>체력이 v2% 이하일 때 방어력 +v1%.</summary>
        LowHpDefenseUp = 24,

        /// <summary>사망 시 <b>한 판에 한 번</b> 최대 체력 v1% 로 부활.</summary>
        ReviveOnce = 25,

        // ── 성역 운영 ────────────────────────────────────────────────
        /// <summary>침식이 <b>쌓이는</b> 속도가 v1% 느려진다(빠지는 속도는 그대로).</summary>
        ErosionSlow = 30,

        /// <summary>시야 +v1%.</summary>
        VisionUp = 31,

        /// <summary>이 캐릭터의 발굴 속도 +v1%.</summary>
        DigSpeed = 32,
    }

    /// <summary>
    /// 유물 하나 — 표 <c>Relic</c> 시트의 <b>한 행</b>.
    ///
    /// <b>어디서 오나</b> — <c>Tools/gen_relic_assets.py</c> 가
    /// <c><볼트>/데이터 테이블/Last_Sanctuary_유물테이블_Ver01.xlsx</c> 를 읽어
    /// <c>Assets/_Project/Resources/Relics/</c> 에 이 에셋들을 쓴다.
    /// ⚠ <b>손으로 고치지 말 것</b> — 다시 돌리면 덮어쓴다. 값을 바꾸려면 표를 고친다.
    ///
    /// ⚠ 칸 이름은 <b>표의 영문 헤더를 그대로</b> 옮겼다 — 표와 코드를 나란히 놓고 대조할
    ///   수 있어야 한다(이 프로젝트가 스킬 <c>value_01</c> 을 그렇게 쓰고 있다).
    ///
    /// ★ <b>문자열이 원문 그대로다</b>(스트링 키가 아니다) — 유저 지시:
    ///   *"스트링 키는 내가 검토하게 옮기게 일단은 해당 테이블에 스트링으로 정리"*.
    ///   나중에 키로 바꿀 때 손댈 곳은 <c>gen_relic_table.py</c> · <c>gen_relic_assets.py</c>
    ///   두 곳이고, 이 클래스는 <see cref="DisplayName"/> 만 고치면 된다.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/유물 정의", fileName = "Relic_")]
    public class RelicDefinitionSO : ScriptableObject
    {
        [Header("표 원본 (Relic 시트)")]
        [Tooltip("relic_id — 일반 700001~ · 레어 710001~ · 에픽 720001~")]
        public int relicId;

        [Tooltip("relic_name — 한국어 원문(스트링 키 아님)")]
        public string relicName;

        public RelicGrade grade = RelicGrade.Common;

        [Tooltip("effect_type — 표 EffectType 시트의 enum")]
        public RelicEffectType effectType = RelicEffectType.None;

        [Tooltip("value_01 — 효과의 주 수치. 대개 %, KillEnergy 만 절대값")]
        public int value01;

        [Tooltip("value_02 — «두 번째 조건» 이 필요한 타입만 쓴다(지금은 체력 문턱 하나)")]
        public int value02;

        [TextArea(2, 4)]
        [Tooltip("relic_desc — 효과 설명(유저에게 그대로 보여준다)")]
        public string relicDesc;

        [TextArea(2, 4)]
        [Tooltip("relic_flavor — 서사 한 줄")]
        public string relicFlavor;

        [Header("출처")]
        public RelicSource source = RelicSource.DigAndMonster;

        [Tooltip("source_id — Boss 일 때 그 보스의 몬스터 ID. 그 외에는 0")]
        public int sourceId;

        [Tooltip("drop_weight — 같은 등급·같은 출처 풀 안에서의 뽑기 가중치")]
        [Min(0)] public int dropWeight = 10;

        [Header("아이콘")]
        [Tooltip("icon — 파일 이름 키. 원화가 오면 같은 이름으로 덮으면 된다")]
        public string iconKey;

        [Tooltip("Resources/RelicIcons 에서 찾아 넣은 스프라이트")]
        public Sprite icon;

        /// <summary>화면에 쓸 이름. 비어 있으면 에셋 이름으로 떨어진다.</summary>
        public string DisplayName =>
            string.IsNullOrWhiteSpace(relicName) ? name : relicName;

        /// <summary>이 유물이 <b>보스 전용</b>인가 — 발굴·일반 몹 풀에서 빼는 기준.</summary>
        public bool IsBossOnly => source == RelicSource.Boss;

        /// <summary>등급 색. UI 가 이름·테두리에 쓴다(표 <c>Grade</c> 시트의 값과 같다).</summary>
        public Color GradeColor => ColorOf(grade);

        /// <summary>표 <c>Grade</c> 시트의 <c>grade_color</c> 와 <b>같은 값</b>이어야 한다.</summary>
        public static Color ColorOf(RelicGrade g) => g switch
        {
            RelicGrade.Rare => new Color(0.435f, 0.765f, 0.910f),   // 6FC3E8
            RelicGrade.Epic => new Color(0.847f, 0.608f, 1.000f),   // D89BFF
            _               => new Color(0.722f, 0.769f, 0.812f),   // B8C4CF
        };

        /// <summary>표 <c>Grade</c> 시트의 <c>grade_name</c>.</summary>
        public static string NameOf(RelicGrade g) => g switch
        {
            RelicGrade.Rare => "레어",
            RelicGrade.Epic => "에픽",
            RelicGrade.Common => "일반",
            _ => "?",
        };
    }
}
