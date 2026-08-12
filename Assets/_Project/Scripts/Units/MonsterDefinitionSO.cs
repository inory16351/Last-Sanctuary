using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Units
{
    /// <summary>몬스터 등급. 웨이브 구성과 공격 우선순위가 달라진다.</summary>
    public enum MonsterTier
    {
        Normal = 0,     // 일반
        MidBoss = 1,    // 중간 보스 (5웨이브)
        MainBoss = 2,   // 메인 보스 (10웨이브)
    }

    /// <summary>
    /// 몬스터 한 종류의 데이터 테이블. 이 에셋만 만들면 스포너가 알아서 생성한다.
    /// 종류별로 에셋을 하나씩 두고, 외형 템플릿도 여기서 지정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/Units/Monster Definition", fileName = "Monster_")]
    public class MonsterDefinitionSO : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("스트링 키 (스트링 키 테이블.xlsx). 예: monster_name_100001\n" +
                 "비워두면 아래 displayName 리터럴을 쓴다(하위 호환)")]
        public string nameKey = "";

        [Tooltip("⚠ 스트링 테이블 도입 이후로는 nameKey 폴백용이다. " +
                 "문구는 스트링 키 테이블에서 고칠 것 — 표시에는 DisplayName 을 쓴다")]
        public string displayName = "암세포";

        public MonsterTier tier = MonsterTier.Normal;

        /// <summary>화면에 보여줄 이름 — 스트링 테이블이 먼저, 없으면 리터럴.</summary>
        public string DisplayName => Data.StringTable.Get(nameKey, displayName);

        [Header("외형 템플릿")]
        [Tooltip("복제할 원본. 종류마다 다른 템플릿을 지정한다")]
        public MonsterUnit template;

        [Header("능력치 (1 ~ 100)")]
        [Range(1, 100)] public int hpStat = 7;
        [Range(1, 100)] public int attackStat = 5;
        [Range(1, 100)] public int defenseStat = 2;
        [Range(0, 100)] public int regenStat = 0;

        [Header("체력 보정")]
        [Tooltip("보스처럼 체력 규모를 따로 키울 때 사용. 퍼센트(정수) — 100 이면 보정 없음")]
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

        [Tooltip("스프라이트에 곱하는 <b>균등</b> 스케일. 0 이면 예전 동작" +
                 "(footprintTiles 를 그대로 스케일로 쓴다).\n" +
                 "★ 비율이 유지되도록 <b>한 값만</b> 쓴다 — 가로/세로를 따로 주면 원화가 찌그러진다.\n" +
                 "단탈리온: 보이는 가로 2.67타일 → 발판 가로 2타일에 맞추려면 0.75")]
        [Min(0f)] public float spriteScale;

        /// <summary>발판 가로(칸). 안 정했으면 <see cref="footprintTiles"/> 정사각.</summary>
        public int BodyWidth => bodyWidthTiles > 0 ? bodyWidthTiles : Mathf.Max(1, footprintTiles);

        /// <summary>발판 세로(칸). 안 정했으면 <see cref="footprintTiles"/> 정사각.</summary>
        public int BodyHeight => bodyHeightTiles > 0 ? bodyHeightTiles : Mathf.Max(1, footprintTiles);

        /// <summary>스프라이트에 곱할 균등 스케일. 안 정했으면 예전 동작 그대로.</summary>
        public float EffectiveSpriteScale =>
            spriteScale > 0f ? spriteScale : Mathf.Max(1, footprintTiles);

        /// <summary>
        /// 공격 우선순위 (웨이브 기획서 p13).
        ///   일반 / 중간보스 : 타워 → 캐릭터 → 넥서스
        ///   메인보스        : 캐릭터 → 타워 → 넥서스
        /// </summary>
        public UnitKind[] TargetPriority => tier == MonsterTier.MainBoss
            ? new[] { UnitKind.Character, UnitKind.Tower, UnitKind.Nexus }
            : new[] { UnitKind.Tower, UnitKind.Character, UnitKind.Nexus };

        /// <summary>
        /// 웨이브 배율을 반영한 능력치를 만든다. 배율은 퍼센트(정수)로 받고
        /// 결과도 정수 능력치이므로, 치환된 체력·공격력도 정수로 떨어진다.
        /// </summary>
        public StatBlock BuildStats(int hpPercentScale = 100, int attackPercentScale = 100,
                                    int statMax = 100)
        {
            return new StatBlock
            {
                hp      = Mathf.Clamp(BalanceConfigSO.ScaleByPercent(hpStat, hpPercentScale),
                                      1, statMax),
                attack  = Mathf.Clamp(BalanceConfigSO.ScaleByPercent(attackStat, attackPercentScale),
                                      1, statMax),
                defense = Mathf.Clamp(defenseStat, 1, statMax),
                regen   = Mathf.Clamp(regenStat, 0, statMax),
            };
        }
    }
}
