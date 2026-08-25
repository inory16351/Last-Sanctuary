using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Units
{
    /// <summary>몬스터 등급. 웨이브 구성과 공격 우선순위가 달라진다.</summary>
    public enum MonsterTier
    {
        Normal = 0,     // 일반

        /// <summary>
        /// ⚠ <b>폐기 (2026-08-18)</b> — 유저 지시로 중간보스(혈인·공허의 속삭임)를 없앴다.
        /// 값을 지우지 <b>않는</b> 것은 <see cref="MainBoss"/> 가 2 로 직렬화돼 있기 때문이다 —
        /// 여기서 1 을 빼면 기존 에셋의 보스가 전부 <b>중간보스로 내려앉는다</b>.
        /// 새로 쓰지 말 것.
        /// </summary>
        MidBoss = 1,

        MainBoss = 2,   // 보스 (5웨이브마다 · 5·15 단탈리온 / 10·20 말파스)
    }

    /// <summary>
    /// 몬스터 한 종류의 데이터 테이블. 이 에셋만 만들면 스포너가 알아서 생성한다.
    /// 종류별로 에셋을 하나씩 두고, 외형 템플릿도 여기서 지정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/Units/Monster Definition", fileName = "Monster_")]
    public class MonsterDefinitionSO : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("★ 표의 <b>monster_id</b> (예: 120002 말파스). 2026-08-18 신설.\n\n" +
                 "<b>왜 필요했나</b> — 웨이브 표가 `boss_monster_id` 로 \"이 웨이브엔 이 보스\" 를 " +
                 "지정하게 되면서(중간보스 삭제·보스 2종), 스포너가 <b>id 로 정의를 찾아야</b> " +
                 "한다. 예전에는 어디에도 id 가 없어서 `nameKey`(monster_name_120002) 문자열을 " +
                 "파싱하는 수밖에 없었다 — 표의 값을 그대로 들고 있는 쪽이 정확하다.\n" +
                 "Tools/sync_tables_to_assets.py 가 표에서 그대로 옮긴다 — 손으로 적지 말 것")]
        public int monsterId;

        [Tooltip("스트링 키 (스트링 키 테이블.xlsx). 예: monster_name_100001\n" +
                 "비워두면 아래 displayName 리터럴을 쓴다(하위 호환)")]
        public string nameKey = "";

        [Tooltip("⚠ 스트링 테이블 도입 이후로는 nameKey 폴백용이다. " +
                 "문구는 스트링 키 테이블에서 고칠 것 — 표시에는 DisplayName 을 쓴다")]
        public string displayName = "암세포";

        public MonsterTier tier = MonsterTier.Normal;

        /// <summary>화면에 보여줄 이름 — 스트링 테이블이 먼저, 없으면 리터럴.</summary>
        public string DisplayName => Data.StringTable.Get(nameKey, displayName);

        [Tooltip("보스 <b>칭호</b>의 스트링 키 (예: boss_title_120001 → \"끝없는 형상의 군주\").\n" +
                 "표의 boss_title 칸을 Tools/sync_tables_to_assets.py 가 그대로 옮긴다 — 손으로 " +
                 "적지 말 것. 잡몹은 비어 있고, 비어 있으면 체력바에 칭호 줄이 아예 안 뜬다.\n" +
                 "유저 지시 2026-08-13: \"보스 몬스터는 소환되면 체력바에 타이틀을 붙여서 표기\"")]
        public string titleKey = "";

        /// <summary>
        /// 보스 칭호. 스트링 테이블에서 읽고, 키가 없거나 문구가 비어 있으면 <b>빈 문자열</b>이다
        /// (체력바가 이 값이 비었는지로 칭호 줄을 켜고 끈다).
        /// </summary>
        public string Title =>
            string.IsNullOrWhiteSpace(titleKey) ? string.Empty
                                                : Data.StringTable.Get(titleKey, string.Empty);

        [Header("외형 템플릿")]
        [Tooltip("복제할 원본. 종류마다 다른 템플릿을 지정한다")]
        public MonsterUnit template;

        // ==================================================================
        // 능력치 — ★★ <b>몬스터에는 상한이 없다</b> (유저 지시 2026-08-18:
        //   <i>"몬스터들은 스탯 상한값 없는 걸로 해줘 특히 보스들 체력 배율로 넣지말고"</i>)
        //
        // 예전에는 캐릭터와 같은 <b>1~100</b> 척도를 강제했다(<c>BalanceConfigSO.statMax</c>).
        // 그 상한은 <b>캐릭터 강화</b>가 무한히 오르지 않게 하려는 규칙인데, 몬스터에까지
        // 걸리면서 두 가지 사고를 냈다:
        //
        //   ① <b>보스 체력을 능력치로 표현할 수 없었다.</b> 그래서 54-3절이
        //      <c>hp_percent</c>(체력 배율) 칸을 따로 만들어 <b>상한을 우회</b>했다 —
        //      "보스가 3초에 죽던 문제" 를 배율로 덮은 것이다. 같은 체력이 두 칸
        //      (<c>hp</c> · <c>hp_percent</c>)에 나뉘어 있어 표를 읽어도 실제 체력을 모른다.
        //   ② <b>후반 웨이브에서 능력치가 조용히 잘렸다.</b> 웨이브 배율(<c>statPercent</c>)이
        //      1440%(18웨이브)를 넘으면 잡몹 체력 7 → 100 에서 멈춘다. 표가 설계한 곡선이
        //      <b>말없이 평평해지고 있었다.</b>
        //
        // ★ 이제 <c>[Min]</c> 이다 — 표에 174 를 적으면 174 로 동작한다.
        //   <b>캐릭터 쪽 상한(<c>statMax</c>)은 그대로다</b> — 강화가 무한히 오르면 안 된다는
        //   규칙은 유효하고, 영웅 각성만 그 위를 뚫는다(92-3절).
        // ==================================================================

        [Header("능력치 (상한 없음 — 위 주석 참조)")]
        [Min(1)] public int hpStat = 7;

        [Tooltip("근거리 공격력 (melee_atk). attackType 이 Melee 일 때 쓰인다")]
        [Min(0)] public int attackStat = 5;

        [Min(1)] public int defenseStat = 2;
        [Min(0)] public int regenStat = 0;

        // ------------------------------------------------------------------
        // 나머지 공격 계열 + 명중·치명 (2026-08-15 신설)
        //
        // <b>왜 이제야 생겼나</b> — 표(<c>웨이브 몬스터 테이블.xlsx</c> · <c>first_Stat</c>)에는
        // 처음부터 <c>ranged_atk</c> · <c>accuracy</c> · <c>critical</c> 칸이 있었는데
        // 담을 곳이 없어 파싱이 <c>max(melee, ranged)</c> 로 <b>두 칸을 하나로 접고</b>
        // 명중·치명은 <b>통째로 버리고</b> 있었다. 그래서 표에 <c>critical: 8</c> 이라고
        // 적힌 최종보스가 실제로는 치명타를 한 번도 내지 않았다.
        //
        // ★ 명중·치명은 <b>원거리 공격 유형에만</b> 적용된다(유저 확정 2026-08-15) —
        //   판정은 <see cref="MonsterUnit.HitChancePercent"/> 쪽에 있다.
        // ------------------------------------------------------------------

        [Tooltip("원거리 공격력 (ranged_atk). attackType 이 Ranged 일 때 쓰인다")]
        [Min(0)] public int rangedAttackStat = 0;

        [Tooltip("마법 공격력 (magic). attackType 이 Magic 일 때 쓰인다")]
        [Min(0)] public int magicStat = 0;

        [Tooltip("회복력 (cure). attackType 이 Heal 일 때 쓰인다 — 지금 몬스터에는 회복형이 없다")]
        [Min(0)] public int cureStat = 0;

        [Tooltip("명중률 (accuracy). ⚠ <b>원거리 공격 유형에만</b> 적용된다.\n" +
                 "적중% = 80 + 명중률 (상한 100) 이므로 20 이상이면 사실상 항상 명중")]
        [Min(0)] public int accuracyStat = 50;

        [Tooltip("크리티컬 확률 (critical). ⚠ <b>원거리 공격 유형에만</b> 적용된다.\n" +
                 "치명% = 크리티컬 × 1")]
        [Min(0)] public int criticalStat = 0;

        [Tooltip("저항력 (resistance). 침식 배율에 쓰인다 — 몬스터는 침식을 받지 않아 지금은 표시용")]
        [Min(0)] public int resistanceStat = 50;

        [Header("체력 보정 (⚠ 더 이상 쓰지 않는다 — 100 으로 둘 것)")]
        [Tooltip("★ 2026-08-18 부터 <b>보스도 체력을 능력치로 적는다</b> " +
                 "(유저 지시: \"보스들 체력 배율로 넣지말고\"). " +
                 "이 칸은 능력치 상한 100 을 우회하려고 54-3절이 만든 것이다. " +
                 "상한이 없어진 지금은 같은 체력을 hp 칸 하나로 적을 수 있고, " +
                 "그래야 표만 봐도 실제 체력을 안다. " +
                 "필드를 지우지는 않는다(U-D3) — 옛 에셋이 남아 있어도 조용히 곱해지도록. " +
                 "새로 적을 일은 없다")]
        [Min(1)] public int hpPercent = 100;

        [Header("전투 파라미터 (타일)")]
        [Tooltip("공격 방식. Ranged/Magic 이면 UnitCombat 이 사거리를 attackRange 로 맞추고, " +
                 "CombatProjectileFx 가 탄환 연출을 그려준다(피해는 예전과 똑같이 히트 스캔으로 즉시).\n" +
                 "몬스터는 Configure() 로만 값을 받으므로, 여기 적어두면 템플릿을 손대지 않아도 반영된다")]
        public TacticalAttackType attackType = TacticalAttackType.Melee;

        [Min(0.5f)] public float detectRange = 7f;
        [Min(0.2f)] public float attackRange = 1.2f;
        [Min(0.05f)] public float attacksPerSecond = 0.8f;
        [Min(0.1f)] public float moveSpeedTiles = 2.2f;

        [Header("외형 크기")]
        [Tooltip("한 변이 몇 타일인지. 보스는 크게(대형 그리드) 잡는다.\n" +
                 "⚠ 이 값은 정사각 전용이고 <b>스프라이트 스케일로만</b> 쓰였다 — 아래 " +
                 "bodyWidthTiles/bodyHeightTiles/spriteScale 이 그 역할을 나눠 받는다")]
        [Min(1)] public int footprintTiles = 1;

        // ------------------------------------------------------------------
        // 몸집 — 발판(가로·세로 따로) 과 스프라이트 스케일을 분리한다 (2026-08-12)
        //
        // <b>왜 나눴나</b>(유저 확정: "보스 크기는 가로 2 세로 3", 선택지 1번 =
        // "발판만 2x3, 스프라이트는 비율 유지") — <see cref="footprintTiles"/> 하나로는
        // ① 정사각만 되고 ② 그 값이 곧 스케일이라 <b>발판을 키우면 그림이 늘어난다.</b>
        // 원화가 가로로 넓은 단탈리온(보이는 크기 2.67 x 1.87 타일)을 2x3 에 맞추면
        // x 0.75 / y 1.60 의 비균등 스케일이 되어 그림이 세로로 찌그러진다.
        // 그래서 <b>게임이 쓰는 크기</b>와 <b>보이는 크기</b>를 따로 둔다.
        // ------------------------------------------------------------------

        [Header("몸집 (발판 / 스프라이트 분리)")]
        [Tooltip("발판 가로 칸 수. 0 이면 footprintTiles 를 정사각으로 쓴다.\n" +
                 "근거리 유닛이 이 몸집 바깥에서 때릴 수 있어야 하므로 " +
                 "UnitCombat 의 사거리 판정(TargetRadius)이 이 값을 읽는다")]
        [Min(0)] public int bodyWidthTiles;

        [Tooltip("발판 세로 칸 수. 0 이면 footprintTiles 를 정사각으로 쓴다")]
        [Min(0)] public int bodyHeightTiles;

        [Tooltip("⚠ <b>구식(픽셀 기준) — 쓰지 말 것.</b> 원화가 몇 픽셀인지 보고 손으로 고른 배율이라 " +
                 "원화가 바뀌면 게임 안 크기가 흔들린다. 실제로 이 값(0.75) 때문에 보스가 " +
                 "잡몹보다 작아졌다. 아래 renderHeightTiles 가 0 일 때만 폴백으로 쓰인다")]
        [Min(0f)] public float spriteScale;

        // ------------------------------------------------------------------
        // 크기 기준은 <b>타일</b>이다 (유저 확정 2026-08-13)
        //
        // "몇 배로 그릴지"가 아니라 <b>"몇 타일로 보일지"</b>만 적는다. 배율은
        // <see cref="LastSanctuary.Combat.CharacterAnimator"/> 가
        //     배율 = renderHeightTiles ÷ 스킨 실측 세로(타일)
        // 로 계산한다. 실측값은 원화의 알파 경계를 잰 것이라 PPU·캔버스 여백과 무관하고,
        // <b>균등 배율이라 원화 비율이 절대 안 깨진다.</b>
        //
        // 발판(근접 유닛이 어디서 때리는지)도 이 크기를 따라간다 — 유저 확정.
        // 아래 bodyWidthTiles/bodyHeightTiles 는 <b>스킨이 없는 유닛의 폴백</b>으로만 남는다.
        // ------------------------------------------------------------------

        [Tooltip("⚠ 세로 전용 폴백(타일). 아래 콜라이더 상자가 비어 있을 때만 쓰인다.\n" +
                 "0 이면 구식 spriteScale 로 떨어진다")]
        [Min(0f)] public float renderHeightTiles;

        /// <summary>목표 세로 크기(타일). 0 이면 구식 배율 경로로 떨어진다.</summary>
        public float RenderHeightTiles => renderHeightTiles;

        // ------------------------------------------------------------------
        // 콜라이더 상자 — <b>표에 적는 값은 이것 하나뿐이다</b> (유저 확정 2026-08-13)
        //
        // 표에 콜라이더 크기를 대충 적으면(2.5 x 1.9 처럼 한 자리 소수),
        //   ① 그 상자 <b>안에 들어가는 최대 배율</b>을 비율 유지로 계산해
        //   ② 그림을 그리고
        //   ③ <b>콜라이더를 그 그림 크기로 다시 맞춘다.</b>
        // 계산·재설정은 <see cref="LastSanctuary.Combat.CharacterAnimator"/> 가 한다.
        //
        // ⚠ 여기 적은 값이 곧 최종 판정 크기는 아니다 — 비율 때문에 한 축은 조금 작아진다.
        //   최종 판정 크기는 <c>CharacterAnimator.ColliderSizeTiles</c> 이고,
        //   <see cref="MonsterUnit.BodyRadiusTiles"/> 가 그걸 읽는다.
        // ------------------------------------------------------------------

        [Header("콜라이더 (타일) — 표의 collider_width/height_tiles")]
        [Tooltip("콜라이더 가로(타일). 세로와 함께 0 보다 커야 이 경로가 쓰인다.\n" +
                 "그림은 이 상자 안에서 비율을 유지한 최대 크기로 그려지고, " +
                 "콜라이더는 그 그림 크기로 다시 맞춰진다")]
        [Min(0f)] public float colliderWidthTiles;

        [Tooltip("콜라이더 세로(타일)")]
        [Min(0f)] public float colliderHeightTiles;

        /// <summary>콜라이더 상자를 쓸 수 있는지 (가로·세로 둘 다 적혀 있는지).</summary>
        public bool HasColliderBox => colliderWidthTiles > 0f && colliderHeightTiles > 0f;

        // ------------------------------------------------------------------
        // 보스 스킬 (2026-08-13)
        //
        // 표(`웨이브 몬스터 테이블.xlsx` / `wave_top_boss`)의 boss_skill_1~3 칸을 그대로 옮긴다.
        // <b>id 만 들고 있는 이유</b> — ScriptableObject 끼리 참조를 걸면 파이프라인이 guid 를
        // 알아야 하고, 스킬 에셋을 다시 만들 때마다 이 에셋도 같이 고쳐야 한다. 번호로 두면
        // 표가 정본이고 `BossSkillCaster` 가 Resources 에서 찾아 붙인다(스킨·정신 이상과 같은 방식).
        // ------------------------------------------------------------------

        [Header("일러스트 — 표의 illust (2026-08-18 신설)")]
        [Tooltip("일러스트 이름 (표 `wave_top_boss.illust`). Resources/Illust 아래를 찾는다.\n\n" +
                 "<b>왜 이제야 생겼나</b> — 클릭 초상화(UnitPortraitPanel)는 2026-08-15 에 " +
                 "<b>중립 몬스터에만</b> 붙었다(86-4·5절). 웨이브 보스는 표에 `illust` 칸이 " +
                 "있는데도 <b>읽는 코드가 없어</b> 눌러도 아무것도 안 떴다. 중립 쪽 " +
                 "(NeutralMonsterDefinitionSO.Illust)과 <b>같은 규칙·같은 폴더</b>다.\n" +
                 "Tools/sync_tables_to_assets.py 가 표에서 그대로 옮긴다 — 손으로 적지 말 것")]
        public string illustName = "";

        Sprite _illust;
        bool _illustLoaded;

        /// <summary>
        /// 초상화 일러스트. <c>Resources/Illust/</c> 에서 이름으로 읽어 캐시한다.
        ///
        /// ⚠ 못 찾으면 경고를 한 번 남긴다. 조용히 null 이 되면 "표에 적었는데 왜 안 뜨지"가
        /// 된다 — 히스톤 초상화가 정확히 그래서 인게임 모션으로 폴백됐다(84-8절 ②,
        /// 원인은 .meta 의 <c>textureType</c> 이 Sprite 가 아니었던 것).
        /// </summary>
        public Sprite Illust
        {
            get
            {
                if (_illustLoaded) return _illust;
                _illustLoaded = true;

                string n = illustName != null ? illustName.Trim() : "";
                if (n.Length == 0) return null;

                _illust = Resources.Load<Sprite>("Illust/" + n);
                if (_illust == null)
                    Debug.LogWarning($"[몬스터] 일러스트 'Resources/Illust/{n}' 을 찾지 못했습니다. " +
                                     $"({DisplayName}) — 파일 이름과 .meta 의 textureType(8=Sprite) 을 " +
                                     "확인해주세요.", this);
                return _illust;
            }
        }

        [Header("보스 스킬 — 표의 boss_skill_1~3")]
        [Tooltip("이 몬스터가 쓰는 보스 스킬의 id. Resources/BossSkills 의 BossSkillSO 를 " +
                 "이 번호로 찾는다. 순서가 곧 슬롯 번호이고, 스킨의 시전 모션 슬롯과 같다.\n" +
                 "빈 칸(0)은 무시한다 — 표의 boss_skill_3 이 0 이다")]
        public int[] bossSkillIds;

        /// <summary>보스 스킬을 하나라도 가졌는지.</summary>
        public bool HasBossSkills
        {
            get
            {
                if (bossSkillIds == null) return false;
                for (int i = 0; i < bossSkillIds.Length; i++)
                    if (bossSkillIds[i] > 0) return true;
                return false;
            }
        }

        /// <summary>발판 가로(칸). 안 정했으면 <see cref="footprintTiles"/> 정사각.</summary>
        public int BodyWidth => bodyWidthTiles > 0 ? bodyWidthTiles : Mathf.Max(1, footprintTiles);

        /// <summary>발판 세로(칸). 안 정했으면 <see cref="footprintTiles"/> 정사각.</summary>
        public int BodyHeight => bodyHeightTiles > 0 ? bodyHeightTiles : Mathf.Max(1, footprintTiles);

        /// <summary>스프라이트에 곱할 균등 스케일. 안 정했으면 예전 동작 그대로.</summary>
        public float EffectiveSpriteScale =>
            spriteScale > 0f ? spriteScale : Mathf.Max(1, footprintTiles);

        /// <summary>
        /// 공격 우선순위 (웨이브 기획서 p13).
        ///   일반 / 중간보스 : 타워 → 캐릭터 → 성역
        ///   메인보스        : 캐릭터 → 타워 → 성역
        /// </summary>
        public UnitKind[] TargetPriority => tier == MonsterTier.MainBoss
            ? new[] { UnitKind.Character, UnitKind.Tower, UnitKind.Nexus }
            : new[] { UnitKind.Tower, UnitKind.Character, UnitKind.Nexus };

        /// <summary>
        /// 웨이브 배율을 반영한 능력치를 만든다. 배율은 퍼센트(정수)로 받고
        /// 결과도 정수 능력치이므로, 치환된 체력·공격력도 정수로 떨어진다.
        ///
        /// ★★ <b>상한이 없다</b> (2026-08-18) — 예전에는 <c>statMax</c>(100)를 받아
        /// 모든 칸을 잘랐다. 그래서 <b>후반 웨이브에서 표가 설계한 곡선이 말없이 평평해졌다</b>:
        /// 잡몹 체력 7 은 18웨이브(배율 1440%)부터, 최종보스 공격력 8 은 20웨이브(1850%)에서
        /// 상한에 닿아 <b>그 위로는 배율을 올려도 아무 일도 일어나지 않았다.</b>
        /// 아래 낮은 쪽 자르기(1 · 0)만 남긴다 — 그건 "0 이면 아예 안 때린다" 를 막는 것이라
        /// 성격이 다르다.
        /// </summary>
        public StatBlock BuildStats(int hpPercentScale = 100, int attackPercentScale = 100,
                                   BalanceConfigSO balance = null)
        {
            // ★★ 상한 (2026-08-19) — <b>공격 계열에만</b> 걸린다. 근거는
            //   <see cref="BalanceConfigSO.monsterAttackStatMax"/> 위의 긴 주석이다:
            //   몬스터 능력치는 웨이브 배율로 무한히 오르는데 캐릭터는 statMax 100 에서
            //   멈추므로, 공격력이 계속 오르면 언젠가 어떤 캐릭터도 두 대를 못 버틴다.
            //   체력은 상한 없이 계속 오른다 — 후반 난이도는 <b>체력과 마리 수</b>로 만든다
            //   (유저 지시: "체력배율로만 플러스를 주고").
            //
            // ⚠ <paramref name="balance"/> 가 null 이면 <b>상한이 없다</b> — 96절과 똑같이
            //   동작한다. 웨이브 표를 안 쓰는 옛 경로·테스트에서 조용히 값이 달라지지 않게
            //   기본값을 「예전 동작」에 맞춘 것이다.
            int hpMax     = balance != null ? balance.monsterHpStatMax : 0;
            int attackMax = balance != null
                ? balance.AttackStatMaxFor(tier == MonsterTier.MainBoss)
                : 0;

            // ★ 웨이브 배율은 <b>지금 쓰는 공격 계열에만</b> 걸어도 되지만, 어느 칸을 쓸지는
            //   런타임의 <c>UnitCombat.AttackType</c> 이 정하므로 여기서는 알 수 없다 —
            //   <b>네 칸 모두에 같은 배율</b>을 건다. 쓰지 않는 칸은 0 이라 아무 영향이 없고,
            //   나중에 유형을 바꿔도 배율이 빠지지 않는다.
            //
            // ⚠ 자르는 순서: <b>배율 → 상한 → 낮은 쪽 바닥</b>. 바닥(1 · 0)을 마지막에 두는
            //   이유는 상한을 0 으로 적어버린 경우에도 "0 이면 아예 안 때린다" 를 막기 위해서다.
            int Scaled(int raw, int lowFloor) =>
                Mathf.Max(lowFloor,
                          BalanceConfigSO.CapStat(
                              BalanceConfigSO.ScaleByPercent(raw, attackPercentScale), attackMax));

            return new StatBlock
            {
                hp           = Mathf.Max(1, BalanceConfigSO.CapStat(
                                   BalanceConfigSO.ScaleByPercent(hpStat, hpPercentScale), hpMax)),
                attack       = Scaled(attackStat, 1),
                rangedAttack = Scaled(rangedAttackStat, 0),
                magic        = Scaled(magicStat, 0),
                cure         = Scaled(cureStat, 0),
                // ⚠ 아래 다섯 칸은 <b>웨이브 배율을 받지 않는다</b>(예전부터 그랬다) —
                //   표 값을 그대로 쓰므로 상한을 뗀 것만으로는 아무것도 안 바뀐다.
                defense      = Mathf.Max(1, defenseStat),
                regen        = Mathf.Max(0, regenStat),
                accuracy     = Mathf.Max(0, accuracyStat),
                critical     = Mathf.Max(0, criticalStat),
                resistance   = Mathf.Max(0, resistanceStat),
            };
        }
    }
}
