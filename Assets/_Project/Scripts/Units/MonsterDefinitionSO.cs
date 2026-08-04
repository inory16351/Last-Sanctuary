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
        public string displayName = "암세포";
        public MonsterTier tier = MonsterTier.Normal;

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
        [Min(0.5f)] public float detectRange = 7f;
        [Min(0.2f)] public float attackRange = 1.2f;
        [Min(0.05f)] public float attacksPerSecond = 0.8f;
        [Min(0.1f)] public float moveSpeedTiles = 2.2f;

        [Header("외형 크기")]
        [Tooltip("한 변이 몇 타일인지. 보스는 크게(대형 그리드) 잡는다")]
        [Min(1)] public int footprintTiles = 1;

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
