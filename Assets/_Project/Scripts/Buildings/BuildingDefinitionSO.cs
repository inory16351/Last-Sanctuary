using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Buildings
{
    /// <summary>건물 종류. 데이터 시트 <c>Construction.Const_type</c> 그대로.</summary>
    public enum BuildingKind
    {
        /// <summary>중앙 건물 — 게임 시작 시 자동 생성, 추가 건설 불가, 공격 불가.</summary>
        Core,

        /// <summary>포탑 — 자원을 소모해 건설한다. 공격 가능.</summary>
        Turret,
    }

    /// <summary>
    /// 건물 한 종류의 데이터 테이블.
    /// 원본은 <c>데이터 테이블/Last_Sanctuary_건물데이터시트_Ver05.xlsx</c> 이고,
    /// 시트가 <c>Construction</c> / <c>Cost</c> / <c>ATK</c> / <c>Hp</c> 로 정규화되어 있는 것을
    /// 여기서는 <b>한 에셋에 모아</b> 담는다 — ID 조인은 스프레드시트의 편의를 위한 것이고,
    /// 게임에서는 건물 하나가 자기 값을 전부 들고 있는 편이 훨씬 다루기 쉽다.
    ///
    /// <b>단위 주의</b> — 시트에서 <c>HP</c> 는 <b>절대 체력값</b>이고(포탑 100, 중앙건물 500),
    /// <c>DEF</c> · <c>ATK</c> 는 <b>1~100 능력치</b>다(<see cref="BalanceConfigSO"/> 로 치환된다).
    /// 시트의 DEF 탭 공식이 <c>BalanceConfigSO</c> 와 같은 식이라는 것을 확인했다.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/Buildings/Building Definition", fileName = "Building_")]
    public class BuildingDefinitionSO : ScriptableObject
    {
        [Header("식별 (Construction 시트)")]
        [Tooltip("Const_id — 10001 중앙건물 / 10002 포탑")]
        public int constId = 10002;

        [Tooltip("스트링 키 (스트링 키 테이블.xlsx). 예: const_name_10002\n" +
                 "비워두면 아래 displayName 리터럴을 쓴다(하위 호환)")]
        public string nameKey = "";

        [Tooltip("Const_name. ⚠ 스트링 테이블 도입 이후로는 nameKey 폴백용이다 — " +
                 "표시에는 DisplayName 을 쓴다")]
        public string displayName = "포탑";

        /// <summary>화면에 보여줄 이름 — 스트링 테이블이 먼저, 없으면 리터럴.</summary>
        public string DisplayName => Data.StringTable.Get(nameKey, displayName);

        [Tooltip("Const_type. 코드에서는 이름이 아니라 이 값으로 분기한다(시트 규칙)")]
        public BuildingKind kind = BuildingKind.Turret;

        [Tooltip("Max_count. 0 = 개수 제한 없음")]
        [Min(0)] public int maxCount = 0;

        [Header("체력 · 방어 (Construction 시트)")]
        [Tooltip("HP — 능력치가 아니라 절대 체력값이다")]
        [Min(1)] public int hp = 100;

        [Tooltip("DEF — 1~100 방어 능력치")]
        [Range(0, 100)] public int defenseStat = 2;

        [Tooltip("Heal_on_wave. 켜면 웨이브 시작 시 최대 체력으로 회복")]
        public bool healOnWave = false;

        [Header("공격 (ATK 시트 — 공격하지 않는 건물은 attackStat 0)")]
        [Tooltip("ATK — 1~100 공격 능력치")]
        [Range(0, 100)] public int attackStat = 10;

        [Tooltip("Atk_speed — 초당 공격 횟수")]
        [Min(0f)] public float attacksPerSecond = 1f;

        [Tooltip("Range — 사거리(타일)")]
        [Min(0f)] public float attackRange = 5f;

        [Tooltip("Target_type = Splash 이면 켠다. 범위 피해를 준다")]
        public bool splash = true;

        [Tooltip("Splash 일 때 범위의 한 변(타일)")]
        [Min(0.5f)] public float splashAreaTiles = 2f;

        [Header("건설 (Cost 시트 + 새로 추가한 Build_time 컬럼)")]
        [Tooltip("Const_cost 1회차 값")]
        [Min(0)] public int costBase = 50;

        [Tooltip("회차당 증가량")]
        [Min(0)] public int costStep = 20;

        [Tooltip("비용 증가가 멈추는 회차. 이후로는 그 회차의 비용이 유지된다")]
        [Min(1)] public int costStepEndCount = 10;

        [Tooltip("Build_time — 건설에 걸리는 시간(초). 캐릭터가 현장에 머문 시간의 합이 " +
                 "이 값에 도달하면 완성된다. 여러 명이 붙으면 그만큼 빨라진다")]
        [Min(0f)] public float buildSeconds = 15f;

        [Header("배치")]
        [Tooltip("한 변이 몇 타일인지. 포탑은 2 (2x2)")]
        [Min(1)] public int footprintTiles = 2;

        [Tooltip("Resources 아래의 스프라이트 경로. 스프라이트는 오브젝트 참조라 MCP 로 " +
                 "씬에 넣을 수 없어서(진행상황 8절 4번) 경로로 읽는다")]
        public string spriteResourcePath = "Buildings/Turret";

        [Header("시야")]
        [Tooltip("이 건물이 밝히는 시야의 한 변(타일). 0 이면 시야를 주지 않는다")]
        [Min(0f)] public float visionTiles = 8f;

        /// <summary>
        /// <paramref name="alreadyBuilt"/> 개를 이미 지은 상태에서 <b>다음</b> 하나의 비용.
        /// 시트 공식 그대로: <c>기본 + 증가량 × (MIN(n, 종료회차) − 1)</c>, n = alreadyBuilt+1.
        /// </summary>
        public int CostFor(int alreadyBuilt)
        {
            int n = Mathf.Max(1, alreadyBuilt + 1);
            return costBase + costStep * (Mathf.Min(n, Mathf.Max(1, costStepEndCount)) - 1);
        }

        /// <summary>개수 제한에 걸렸는지. <see cref="maxCount"/> 0 은 무제한.</summary>
        public bool AtLimit(int alreadyBuilt) => maxCount > 0 && alreadyBuilt >= maxCount;

        /// <summary>공격 유형 — 시트의 <c>Target_type</c> 을 전투 시스템의 유형으로 옮긴 것.</summary>
        public TacticalAttackType AttackType =>
            attackStat <= 0 ? TacticalAttackType.Melee
                            : (splash ? TacticalAttackType.Magic : TacticalAttackType.Ranged);
    }
}
