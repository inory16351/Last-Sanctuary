using System.Collections.Generic;
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

        /// <summary>
        /// ★★ <c>event</c> — <b>사건 보상으로만</b> 나온다 (2026-08-24 신설 · 유저 지시:
        /// *"이벤트 보상용 전용 에픽 유물 3개만 추가해 … 예상치못한 획득의 재미를 느낄 수 있게"*).
        ///
        /// <b>발굴·처치·보스 어디에서도 안 나온다.</b> 사건의 선택지가
        /// <c>relic_gain</c> 보상으로 <b>ID 를 지목해</b> 주는 것이 유일한 통로다
        /// (<c>EventRewardService</c>). <see cref="RelicDefinitionSO.sourceId"/> 에는
        /// 그 사건의 <c>event_id</c> 를 적어 «어느 사건이 주는가» 를 표에서 읽을 수 있게 했다.
        ///
        /// ⚠ <see cref="RelicRegistry"/> 의 뽑기 풀에 <b>들어가서는 안 된다</b> —
        ///   그쪽 <c>switch</c> 의 <c>default</c> 가 «일반 풀» 이라, 가지를 안 만들면
        ///   조용히 발굴·처치에서 튀어나온다(그래서 거기에 명시적으로 가지를 뒀다).
        /// </summary>
        Event = 4,
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
        // ★★ <b>2026-08-24 (표 Ver02) — value_01 이 «%» 가 아니라 «정수» 다.</b>
        //   표의 값을 <b>그대로</b> <c>AddFlatStatBonus</c> 에 넣는다. 그래서
        //   ① 누구에게 붙여도 크기가 같고 ② <b>능력치 상한(100)을 초월</b>한다
        //   (패시브 「로 아이아스」 방어 +8 · 「명사수」 크리 +20 이 쓰는 그 통로다).
        //
        //   왜 바꿨나 — 이 게임의 능력치는 <b>한 자리 수</b>다(체력 2~12 · 근거리 1~10).
        //   %로 주면 «높은 캐릭터가 더 많이 받는» 부익부가 되고, 낮은 캐릭터는 반올림에서
        //   +1 로 뭉개져 «장착해도 그대로» 가 된다.
        //
        //   ⚠ 밸런스 기준(표 Info) — <b>강화 1회 = 총 +2.5 포인트</b>가 여러 능력치에 흩어진다.
        //     그래서 유물은 일반 +1 · 레어 +2 · 에픽 +2를 두 칸이다(집중되므로 강화보다 세다).
        //     공격 1 포인트 = 피해 +2 · 체력 1 포인트 = HP +10 · 크리 1 포인트 = +1%p 다.
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

        // ── 2026-08-24 (표 Ver02) 신설 ────────────────────────────────
        /// <summary>처치할 때마다 침식 −v1.</summary>
        KillErosionDown = 40,

        /// <summary>
        /// 처치할 때마다 <b>근거리 공격력 +v1</b>, <b>최대 v2 회</b>까지 누적.
        /// ⚠ 누적은 이 판 동안만 — 벗으면 쌓인 만큼 그대로 빠진다.
        /// </summary>
        KillGrowth = 41,

        /// <summary>체력이 v2% 이하일 때만 흡혈 v1%.</summary>
        LowHpLifesteal = 42,

        /// <summary>웨이브가 <b>소환될 때</b> 최대 체력 v1% 보호막을 v2 초.</summary>
        WaveShield = 43,

        /// <summary>웨이브가 <b>끝날 때마다</b> 에너지 +v1.</summary>
        WaveEnergy = 44,

        /// <summary>웨이브가 <b>끝날 때마다</b> 최대 체력 v1% 회복.</summary>
        WaveHeal = 45,
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

        [Tooltip("value_02 — «두 번째 조건» 이 필요한 타입만 쓴다(체력 문턱·지속시간·누적 상한)")]
        public int value02;

        [Header("효과 슬롯 2 (표 Ver02 — «두 능력치가 함께 오르는 유물»)")]
        [Tooltip("effect_type_02 — 비어 있으면(None) 이 슬롯은 없는 것이다.\n" +
                 "규약은 이벤트 보상의 reward_type_02 와 같다")]
        public RelicEffectType effectType2 = RelicEffectType.None;

        [Tooltip("value_03 — 두 번째 효과의 주 수치")]
        public int value03;

        [Tooltip("value_04 — 두 번째 효과의 보조 수치")]
        public int value04;

        /// <summary>이 유물이 쓰는 효과 슬롯을 <b>순서대로</b> 돌려준다(빈 칸은 건너뛴다).</summary>
        public IEnumerable<(RelicEffectType type, int v1, int v2)> Effects()
        {
            if (effectType != RelicEffectType.None) yield return (effectType, value01, value02);
            if (effectType2 != RelicEffectType.None) yield return (effectType2, value03, value04);
        }

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

        // ══════════════════════════════════════════════════════════════
        //  ★★★ 스트링 키 (2026-08-25 신설 — 유저: *"이벤트랑 유물 테이블도
        //      스트링 키 테이블 연동"*)
        // ══════════════════════════════════════════════════════════════
        // 위의 <see cref="relicName"/>·<see cref="relicDesc"/>·<see cref="relicFlavor"/> 는
        // 이제 <b>폴백</b>이다. 정본은 스트링 키 테이블이고, 이 세 키가 그리로 가는 다리다.
        // (건물·캐릭터·몬스터가 이미 쓰는 것과 <b>완전히 같은 짜임</b>이다 —
        //  `CharacterDefinitionSO.DisplayName` 참고.)
        //
        // ⚠ <b>문구를 고칠 곳은 유물 표</b>다. 스트링 키 테이블에서 고치면 다음
        //   `gen_string_table.py` 에 되돌아온다(그쪽의 «기존 우선» 규칙 때문에 사실은
        //   남지만, 두 곳이 갈리면 어느 쪽이 정본인지 알 수 없게 된다).

        [Header("스트링 키")]
        [Tooltip("relic_name_<id> — 비어 있으면 relicName 을 그대로 쓴다")]
        public string nameKey = "";

        [Tooltip("relic_desc_<id> — 비어 있으면 relicDesc 를 그대로 쓴다")]
        public string descKey = "";

        [Tooltip("relic_flavor_<id> — 비어 있으면 relicFlavor 를 그대로 쓴다")]
        public string flavorKey = "";

        /// <summary>화면에 쓸 이름. 키 → 표의 원문 → 에셋 이름 순으로 떨어진다.</summary>
        public string DisplayName => Data.StringTable.Get(
            nameKey, string.IsNullOrWhiteSpace(relicName) ? name : relicName);

        /// <summary>효과 설명 — 화면에 그대로 나온다.</summary>
        public string Desc => Data.StringTable.Get(descKey, relicDesc);

        /// <summary>서사 한 줄.</summary>
        public string Flavor => Data.StringTable.Get(flavorKey, relicFlavor);

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

        /// <summary>
        /// 표 <c>Grade</c> 시트의 <c>grade_name</c>.
        ///
        /// ★ 2026-08-25 — <b>스트링 키를 먼저 본다</b>(<c>relic_grade_common</c> …).
        ///   등급 이름은 «일반/레어/에픽» 세 낱말뿐이라 표에 두지 않아도 굴러가지만,
        ///   <b>화면에 나오는 글은 예외 없이 스트링 테이블을 지나가야</b> 나중에 언어를
        ///   붙일 때 «여기만 안 바뀐다» 가 생기지 않는다.
        /// ⚠ 아래 한글은 <b>폴백</b>이다 — 표가 없어도 «?» 가 뜨지 않게 한다.
        /// </summary>
        public static string NameOf(RelicGrade g) => Data.StringTable.Get(KeyOf(g), g switch
        {
            RelicGrade.Rare => "레어",
            RelicGrade.Epic => "에픽",
            RelicGrade.Common => "일반",
            _ => "?",
        });

        /// <summary>표 <c>Grade</c> 시트의 <c>grade</c> enum 그대로 — 스트링 키의 꼬리다.</summary>
        static string KeyOf(RelicGrade g) => g switch
        {
            RelicGrade.Rare => "relic_grade_rare",
            RelicGrade.Epic => "relic_grade_epic",
            RelicGrade.Common => "relic_grade_common",
            _ => "",
        };
    }
}
