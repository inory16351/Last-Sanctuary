using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 중립 몬스터 한 종류의 데이터 테이블. 웨이브 몬스터(<see cref="MonsterDefinitionSO"/>)와
    /// 달리 웨이브 배율을 받지 않는다 — 맵에 항상 서식하며 캐릭터가 정찰 중 사냥해
    /// 에너지를 얻는 대상이다.
    ///
    /// 원본 데이터: `데이터 테이블/임시용 중립 몬스터.xlsx` (neutrality_mon 시트).
    /// spawn_range · atk_take(선공 여부) 를 뺀 나머지 칸(에너지·능력치)은 비어 있어서
    /// 기존 웨이브 몬스터 스탯(근거리 7/5/2/0, 원거리 6/4/1/0)을 기준으로 밸런스를
    /// 맞춰 채웠다 — 2026-08-05, 진행상황.md 22절 참조.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/Units/Neutral Monster Definition", fileName = "NeutralMonster_")]
    public class NeutralMonsterDefinitionSO : ScriptableObject
    {
        [Header("식별 (테이블 mon_id/mon_name)")]
        public int monId = 1001;

        [Tooltip("스트링 키 (스트링 키 테이블.xlsx). 예: mon_name_1001\n" +
                 "비워두면 아래 displayName 리터럴을 쓴다(하위 호환)")]
        public string nameKey = "";

        [Tooltip("⚠ 스트링 테이블 도입 이후로는 nameKey 폴백용이다 — 표시에는 DisplayName 을 쓴다")]
        public string displayName = "역겨운 덩어리";

        /// <summary>화면에 보여줄 이름 — 스트링 테이블이 먼저, 없으면 리터럴.</summary>
        public string DisplayName => Data.StringTable.Get(nameKey, displayName);

        [Header("외형 템플릿")]
        [Tooltip("복제할 원본. 씬 오브젝트는 SO가 참조할 수 없으므로(Unity 제약), " +
                 "MonsterDefinitionSO와 같은 패턴으로 스포너 쪽 스폰 테이블에도 별도로 연결한다")]
        public NeutralMonsterUnit template;

        [Header("등장 범위 (테이블 spawn_range)")]
        [Tooltip("넥서스 기준 이 값 × 이 값(n×n) 타일 구역부터 나타날 수 있다. " +
                 "실제 판정은 절반(n/2 타일)을 넥서스로부터의 최소 체비셰프 거리로 사용한다. " +
                 "\"부터\"이므로 상한은 없다 — 더 멀리 나가도 계속 나타난다")]
        [Min(1)] public int spawnRangeTiles = 100;

        /// <summary>넥서스 중심에서 이 거리(타일, 체비셰프) 이상 떨어져야 스폰 후보가 된다.</summary>
        public float MinDistanceFromNexus => spawnRangeTiles * 0.5f;

        [Header("에너지 보상 (테이블 min/max_energy)")]
        [Tooltip("처치 시 획득하는 에너지의 최소값")]
        [Min(0)] public int minEnergy = 5;
        [Tooltip("처치 시 획득하는 에너지의 최대값 (포함)")]
        [Min(0)] public int maxEnergy = 10;

        [Header("개체 수 · 재생성 (테이블 max_alive / respawn_seconds)")]
        [Tooltip("이 종류가 맵에 동시에 존재할 수 있는 최대 개체 수. 표가 정본이다.\n" +
                 "0 이면 씬 스포너의 Spawn Table 에 적힌 값으로 떨어진다(예전 동작)")]
        [Min(0)] public int maxAlive;

        [Tooltip("부족분을 다시 채우는 간격(초). 종마다 다르게 둘 수 있다 — 가까운 종을 느리게, " +
                 "먼 종을 빠르게 두면 후반에 멀리 나갈 이유가 생긴다(유저 지시 2026-08-13).\n" +
                 "0 이면 씬 스포너의 Restock Interval 로 떨어진다")]
        [Min(0f)] public float respawnSeconds;

        [Header("능력치 1~100 (테이블 first_Stat 시트)")]
        [Tooltip("웨이브 몬스터와 같은 BalanceConfigSO 치환 공식을 그대로 쓴다.\n" +
                 "★ 2026-08-13 부터 `임시용 중립 몬스터.xlsx` 의 <b>first_Stat 시트</b>가 정본이다 — " +
                 "웨이브 몬스터 테이블과 같은 형식(웨이브 몬스터의 first_Stat 을 그대로 따랐다)")]
        [Range(0, 100)] public int attackStat = 0;
        [Range(1, 100)] public int hpStat = 3;
        [Range(0, 100)] public int defenseStat = 0;
        [Range(0, 100)] public int regenStat = 0;

        [Header("★ 선공 여부 (테이블 atk_take) — 이 한 칸이 전부다")]
        [Tooltip("★ <b>중립 몬스터의 적대 판정은 이 값 하나로 정해진다</b> (유저 확정 2026-08-13).\n\n" +
                 "  <b>켜짐(선공)</b>   — 적이 보이면 먼저 다가가서 때린다.\n" +
                 "  <b>꺼짐(비선공)</b> — <b>맞기 전까지는</b> 공격하지 않는다. 맞으면 반격한다.\n\n" +
                 "⚠ 예전에는 이 값 말고도 템플릿의 <c>UnitCombat.canAcquireTargets</c>·" +
                 "<c>canRetaliate</c> 가 인스펙터에 따로 노출돼 있어서 '선공 체크가 여러 개' 로 " +
                 "보였고, 세 값이 어긋나면 표와 다르게 동작했다(유저 리포트). 이제 스포너가 " +
                 "스폰할 때마다 이 값 하나로 <b>둘 다 덮어쓴다</b> — 템플릿에 뭐가 켜져 있든 " +
                 "표가 이긴다(NeutralMonsterSpawner.SpawnOne).")]
        public bool aggressive = false;

        [Header("전투 파라미터 (타일)")]
        [Tooltip("이 거리 안의 적을 인식한다. <b>선공일 때만</b> 스스로 찾아간다 — " +
                 "비선공은 맞았을 때의 반격 거리로만 쓰인다")]
        [Min(0.5f)] public float detectRange = 6f;

        [Tooltip("공격 사거리(타일). 중립 몬스터는 전부 <b>근거리</b>다(유저 지시 2026-08-13)")]
        [Min(0.2f)] public float attackRange = 1.2f;

        [Min(0.05f)] public float attacksPerSecond = 0.7f;
        [Min(0.1f)] public float moveSpeedTiles = 1.8f;

        [Tooltip("스폰 지점 기준 이 반경 밖의 적은 쫓지 않고 돌아온다(타일). 서식지에 묶어둔다")]
        [Min(1f)] public float leashRangeTiles = 6f;

        /// <summary>웨이브 배율 없이 그대로 쓰는 능력치 묶음.</summary>
        public StatBlock BuildStats() => new StatBlock
        {
            hp = Mathf.Max(1, hpStat),
            attack = Mathf.Max(0, attackStat),
            defense = Mathf.Max(0, defenseStat),
            regen = Mathf.Max(0, regenStat),
        };
    }
}
