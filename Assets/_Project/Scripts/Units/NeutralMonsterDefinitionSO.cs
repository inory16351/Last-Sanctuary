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

        // ==================================================================
        // 등장 범위 — ★★ 2026-08-16 부터 <b>정사각형</b>이다 (유저 확정)
        //
        // <i>"몬스터 생성 가능 / 배회 범위를 사각형으로 생성하게 하는 로직으로 해결하자.
        //    변이 15인 정사각형에서부터 변이 99인 정사각형까지 — 이러면 맵 끝까지 꽉차게
        //    생성 가능하니까"</i>
        //
        // <b>표 값의 뜻은 그대로다</b> — 여전히 "한 변의 길이"이고 숫자를 하나도 안 고쳤다.
        // 바뀐 것은 <b>거리를 재는 방법</b> 하나뿐이다:
        //     원형: sqrt(x² + y²)  ≤ 값/2      ← 2026-08-15 까지
        //     사각: max(|x|, |y|)  ≤ 값/2      ← 지금
        //
        // <b>왜 바꿨나</b> — 정사각 맵에서 원으로 뽑으면 <b>네 모서리에 아무것도 안 나온다.</b>
        // 상한이 변 320(=반변 160)인데 맵 모서리는 중심에서 유클리드로 226 이라, 그 사이
        // 22,021칸(맵의 <b>21.5%</b>)이 규칙상 후보가 될 수 없었다(진행상황 86-9절).
        // ==================================================================

        [Header("등장 범위 (테이블 spawn_range_min / spawn_range_max) — ★ 정사각형 한 변")]
        [Tooltip("★ <b>성역을 중심에 둔 정사각형 한 변의 길이(타일)</b> 하한.\n\n" +
                 "판정은 <b>체비셰프 거리</b> max(|x|,|y|) 다 — 실제 검사에는 이 값의 " +
                 "<b>절반</b>(반변)을 쓴다. 예: 15 → 성역에서 ±7.5타일 바깥부터.\n\n" +
                 "⚠ 2026-08-16 이전에는 같은 숫자를 <b>원의 지름</b>으로 읽었다 — 뜻이 " +
                 "'한 변'으로 바뀌었을 뿐 <b>표 값은 그대로</b>다.")]
        [Min(0f)] public float spawnRangeMinTiles = 15f;

        [Tooltip("등장 정사각형 <b>한 변</b>(타일) 상한. 이보다 멀리서는 나타나지도, 배회하지도 않는다.\n" +
                 "0 이면 제한 없음(맵 끝까지)으로 친다.\n" +
                 "★ 320×320 맵에서 <b>320 을 적으면 맵 전체</b>가 된다 — 모서리까지 꽉 찬다.")]
        [Min(0f)] public float spawnRangeMaxTiles = 100f;

        /// <summary>
        /// 성역 중심에서 이 거리(타일, <b>체비셰프 반변</b>) 이상 떨어져야 스폰 후보가 된다.
        /// 표의 값은 <b>한 변</b>이므로 절반으로 나눈다.
        /// </summary>
        public float MinDistanceFromNexus => Mathf.Max(0f, spawnRangeMinTiles) * 0.5f;

        /// <summary>
        /// 성역 중심에서 이 거리(타일, <b>체비셰프 반변</b>) 이하여야 스폰 후보가 된다.
        /// 표의 값은 <b>한 변</b>이므로 절반으로 나눈다. 0 이면 무한대.
        /// </summary>
        public float MaxDistanceFromNexus =>
            spawnRangeMaxTiles > 0f
                ? Mathf.Max(spawnRangeMaxTiles * 0.5f, MinDistanceFromNexus + 1f)
                : float.PositiveInfinity;

        /// <summary>
        /// ★★★ <b>적정 레벨</b> — 이 에픽을 감당하려면 <b>부대 하나(4명)</b> 가 몇 레벨이어야
        /// 하는가 (2026-08-25 · 유저 지시: *"토벌지시 UI에서 중립 에픽 몬스터의 적정 레벨을
        /// 표시할거야 … 권장 기준은 부대 하나 (4명)의 레벨이 적정 레벨을 달성했을 때"*).
        ///
        /// ★ <b>표에 두는 이유</b> — 능력치에서 «계산해서» 뽑을 수도 있지만, 그러면 밸런스를
        ///   만질 때마다 권장치가 <b>제멋대로 흔들린다</b>. 이것은 계산값이 아니라
        ///   <b>기획이 정한 문턱</b>이므로 표에 적힌 그대로 보여 준다(몬스터 크기를 상수에서
        ///   표 컬럼으로 옮긴 118-3절과 같은 판단).
        /// ⚠ 0 이면 <b>표시하지 않는다</b> — 잡몹 중립에는 이 개념이 없다.
        /// </summary>
        [Header("적정 레벨 (테이블 recommend_level · 2026-08-25)")]
        [Tooltip("이 에픽을 잡으려면 부대 하나(4명)가 몇 레벨이어야 하는가.\n" +
                 "0 이면 토벌 지시 창에 아무것도 안 뜬다(잡몹 중립).")]
        [Min(0)] public int recommendLevel;

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

        [Tooltip("★★ <b>이 종을 한 마리 잡을 때마다 다음 개체에 더해지는 배율</b> " +
                 "(표 `임시용 중립 몬스터.xlsx` 의 `growth_per_kill`, 2026-08-24 신설). " +
                 "<b>왜 종별로 두는가</b> — 밸런스 기획서가 에픽마다 <b>다른</b> 성장을 요구한다: " +
                 "«카르시노스는 재생성될 때마다 +1레벨 · 아니사킬 +1~2 · 바리올라 +2~3 · " +
                 "폴리르 +4~5». 그런데 NeutralGrowthService 는 씬에 하나뿐이고 " +
                 "(killsPerStep 10 · stepMultiplier 0.1) <b>종을 구분하지 않는다</b> — " +
                 "잡몹 중립과 에픽에 같은 배율이 걸린다. 게다가 에픽은 maxAlive 1 · " +
                 "respawnSeconds 600~800초라 한 판(9,000초) 동안 열 마리 남짓만 나오고, " +
                 "그 전체를 다 잡아도 «10마리당 +0.1» 로는 x1.1~1.5 에 그친다. " +
                 "★ 이 값은 <b>한 마리당</b>이다 — 0.01 이면 «10마리당 +0.1» 로 " +
                 "서비스의 기본값과 정확히 같다(잡몹 중립이 그 값이다). " +
                 "⚠ <b>0 이면 서비스의 전역 값을 쓴다</b> — 이 열이 없던 옛 에셋이 예전과 " +
                 "똑같이 동작한다")]
        [Min(0f)] public float growthPerKill;

        [Tooltip("★★ <b>게임을 시작한 뒤 이 종이 처음 나타나기까지 기다리는 시간(초)</b> " +
                 "(표 `임시용 중립 몬스터.xlsx` 의 `first_spawn_delay`, 2026-08-24 신설).\n\n" +
                 "<b>왜 필요한가</b> — 유저 지시 «에픽 보스 몬스터의 생성 시간을 게임 시작 이후 " +
                 "300초 뒤로». 예전에는 스포너가 <b>Start 에서 상한까지 한꺼번에</b> 채웠기 때문에 " +
                 "에픽 넷이 <b>0초에</b> 서식지까지 완성된 채로 서 있었다. 밸런스 기획서는 " +
                 "카르시노스 첫 조우를 «Lv10 1부대» 로 잡고 있는데, 판이 시작된 순간 이미 " +
                 "맵에 있으면 그 조우 시점을 게임이 통제할 수 없다.\n\n" +
                 "0 이면 <b>예전처럼 시작과 함께</b> 나온다(잡몹 중립이 그 값이다).\n" +
                 "⚠ 이 값은 <b>첫 등장에만</b> 걸린다 — 그 뒤의 재생성 간격은 respawnSeconds 다")]
        [Min(0f)] public float firstSpawnDelaySeconds;

        [Header("능력치 1~100 (테이블 first_Stat 시트)")]
        [Tooltip("웨이브 몬스터와 같은 BalanceConfigSO 치환 공식을 그대로 쓴다.\n" +
                 "★ 2026-08-13 부터 `임시용 중립 몬스터.xlsx` 의 <b>first_Stat 시트</b>가 정본이다 — " +
                 "웨이브 몬스터 테이블과 같은 형식(웨이브 몬스터의 first_Stat 을 그대로 따랐다)")]
        [Min(0)] public int attackStat = 0;
        [Min(1)] public int hpStat = 3;
        [Min(0)] public int defenseStat = 0;
        [Min(0)] public int regenStat = 0;

        [Tooltip("원거리 공격력 (ranged_atk). attackType 이 Ranged 일 때 쓰인다")]
        [Min(0)] public int rangedAttackStat = 0;

        [Tooltip("마법 공격력 (magic). attackType 이 Magic 일 때 쓰인다")]
        [Min(0)] public int magicStat = 0;

        [Tooltip("회복력 (cure). attackType 이 Heal 일 때 쓰인다")]
        [Min(0)] public int cureStat = 0;

        [Tooltip("명중률 (accuracy). ⚠ <b>원거리 공격 유형에만</b> 적용된다 (유저 확정 2026-08-15).\n" +
                 "적중% = 80 + 명중률 (상한 100)")]
        [Min(0)] public int accuracyStat = 50;

        [Tooltip("크리티컬 확률 (critical). ⚠ <b>원거리 공격 유형에만</b> 적용된다")]
        [Min(0)] public int criticalStat = 0;

        [Tooltip("저항력 (resistance). 표시용 — 중립은 침식을 받지 않는다")]
        [Min(0)] public int resistanceStat = 50;

        [Header("체력 배율 (표 hp_percent) — 에픽/보스형 전용 손잡이")]
        [Tooltip("★ 2026-08-21 신설 (유저 지시: 중립 몬스터에게도 체력 배율 추가 · 특히 보스 " +
                 "몬스터 칼럼 추가하고 테이블에도 추가). " +
                 "체력 = hp 능력치 x 이 값(%) 을 치환 공식에 넣는다. 100 이면 예전과 같다. " +
                 "<b>왜 필요한가</b> — 중립은 <b>웨이브 배율을 받지 않는다</b>(설계). " +
                 "그래서 에픽 보스의 체력을 키우려면 표의 hp 칸을 계속 키워야 하는데, " +
                 "그 칸은 <b>다른 종과 같은 척도</b>라 «40 vs 4000» 처럼 벌어지면 표를 읽기 " +
                 "어려워진다. 배율 칸을 따로 두면 «이 종은 기본 체력의 몇 배인가» 를 한눈에 본다. " +
                 "⚠ 웨이브 몬스터의 같은 이름 칸(MonsterDefinitionSO.hpPercent)은 " +
                 "<b>더 이상 쓰지 않는다</b> — 그쪽은 능력치 상한을 우회하려던 칸이었고 " +
                 "상한이 없어져 필요가 사라졌다. 이 칸은 <b>상한이 아니라 척도</b> 문제를 " +
                 "푸는 것이므로 성격이 다르다.")]
        [Min(1)] public int hpPercent = 100;

        // ==================================================================
        // ★ 무리 (테이블 group_making / group_member / atk_take) — 2026-08-15 재정의
        //
        // ⚠ <b>`atk_take` 의 의미를 바로잡았다.</b> 71절이 이 칸을 "선공 여부" 로 읽어
        // <c>aggressive</c> 에 넣고 있었는데, 표의 한글 헤더는 처음부터
        // <b>"동료 협공 여부"</b> 였다. 유저 확정(2026-08-15)으로 정리하면:
        //
        //   · <b>선공 여부는 표에 없다</b> — 종류로 정해진다.
        //     <b>중립 몬스터는 전부 비선공</b>, 웨이브 몬스터는 전부 선공.
        //   · <c>atk_take</c> = <b>무리 반격 여부</b> — 같은 무리의 동료가 맞으면 같이 덤빈다.
        //
        // 그래서 <c>aggressive</c> 필드를 없앴다. 남겨두면 "선공 체크가 여러 개" 문제가
        // 그대로 돌아온다(위 88~98행이 지적하던 바로 그 문제다).
        // ==================================================================

        [Header("★ 무리 (테이블 group_making · group_member · atk_take)")]
        [Tooltip("무리를 짓는가 (group_making).\n" +
                 "켜져 있으면 스폰될 때 <b>가까이 있는 같은 종</b>과 한 무리로 묶인다.\n" +
                 "묶는 거리와 최대 마리 수는 아래 group_member 와 스포너의 무리 반경이 정한다")]
        public bool groupMaking = false;

        [Tooltip("한 무리의 최대 마리 수 (group_member). 0 이면 무리를 만들지 않는다")]
        [Min(0)] public int groupMember = 0;

        [Tooltip("★ <b>무리 반격 여부</b> (atk_take — 표 한글 헤더 \"동료 협공 여부\").\n\n" +
                 "  <b>켜짐</b> — 같은 무리의 동료가 공격받으면 <b>무리 전체가</b> 그 공격자에게 덤빈다.\n" +
                 "  <b>꺼짐</b> — 맞은 개체만 혼자 반격한다.\n\n" +
                 "⚠ 이 값은 <b>선공 여부가 아니다</b>. 중립 몬스터는 예외 없이 전부 비선공이다 " +
                 "(유저 확정 2026-08-15) — 먼저 맞기 전에는 절대 공격하지 않는다.")]
        public bool packRetaliate = false;

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

        [Tooltip("공격 방식 (표 atk_type). ⚠ <b>명중률·크리티컬은 Ranged 일 때만</b> 적용된다.\n" +
                 "스폰할 때 스포너가 UnitCombat 에 넣어준다 — 템플릿을 손대지 않아도 반영된다")]
        public TacticalAttackType attackType = TacticalAttackType.Melee;

        // ==================================================================
        // 외형 — 표의 mon_title · collider_*_tiles · mon_illust · mon_skin (2026-08-15)
        //
        // 웨이브 몬스터(<see cref="MonsterDefinitionSO"/>)가 이미 갖고 있던 칸들을
        // <b>같은 이름·같은 뜻</b>으로 중립에도 들여왔다. 표 형식이 두 벌로 갈리면
        // 파싱도 두 벌이 된다.
        // ==================================================================

        [Header("외형")]
        [Tooltip("칭호의 스트링 키 (표 mon_title). 예: mon_title_1004.\n" +
                 "비어 있으면 칭호가 없다 — 보스 체력바가 이름만 띄운다")]
        public string titleKey = "";

        /// <summary>칭호. 없으면 빈 문자열 — <see cref="MonsterDefinitionSO.Title"/> 과 같은 규칙.</summary>
        public string Title =>
            string.IsNullOrWhiteSpace(titleKey)
                ? string.Empty
                : Data.StringTable.Get(titleKey, string.Empty);

        [Tooltip("콜라이더 가로(타일). 세로와 함께 0 보다 커야 이 경로가 쓰인다.\n" +
                 "그림을 이 상자 안에 비율 유지로 맞추고, 콜라이더를 다시 그 그림 크기로 맞춘다(61·66절)")]
        [Min(0f)] public float colliderWidthTiles;

        [Tooltip("콜라이더 세로(타일)")]
        [Min(0f)] public float colliderHeightTiles;

        /// <summary>표의 콜라이더 상자가 실제로 채워져 있는지.</summary>
        public bool HasColliderBox => colliderWidthTiles > 0f && colliderHeightTiles > 0f;

        [Tooltip("일러스트 이름 (표 mon_illust). Resources/Illust 아래를 찾는다.\n" +
                 "★ 2026-08-15 부터 실제로 쓰인다 — 유닛을 클릭하면 UnitPortraitPanel 이 띄운다")]
        public string illustName = "";

        Sprite _illust;
        bool _illustLoaded;

        /// <summary>
        /// 초상화 일러스트. <c>Resources/Illust/</c> 에서 이름으로 읽어 캐시한다 —
        /// <see cref="CharacterDefinitionSO.Illust"/> 와 <b>같은 규칙·같은 폴더</b>다.
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
                    Debug.LogWarning($"[중립] 일러스트 'Resources/Illust/{n}' 을 찾지 못했습니다. " +
                                     $"({DisplayName}) — 파일 이름과 .meta 의 textureType(8=Sprite) 을 " +
                                     "확인해주세요.", this);
                return _illust;
            }
        }

        [Tooltip("스킨 <b>종 이름</b> (표 mon_skin 에서 '_asset' 을 뗀 것).\n" +
                 "Resources/MonsterSkins/<종>/Skin_<종> 을 찾는다 — 예: Carcinos → " +
                 "Resources/MonsterSkins/Carcinos/Skin_Carcinos.\n" +
                 "비어 있으면 스킨을 붙이지 않는다(1001~1003 처럼 정적 스프라이트로 남는다)")]
        public string skinAssetName = "";

        /// <summary>
        /// 스킨 에셋의 Resources 경로. 없으면 빈 문자열.
        ///
        /// <b>왜 종 이름 하나로 경로를 만드나</b> — 몬스터 스킨은 종마다 폴더 하나라는
        /// 규약이 이미 있다(<c>MonsterSkins/HellFang</c> 등). 그 규약을 코드가 알고 있으면
        /// 표에는 종 이름만 적으면 된다.
        /// </summary>
        public string SkinResourcePath
        {
            get
            {
                string s = SpeciesName;
                return s.Length == 0 ? "" : $"MonsterSkins/{s}/Skin_{s}";
            }
        }

        /// <summary>
        /// <b>종 이름</b> — 표의 <c>mon_skin</c> 에서 꼬리표를 뗀 것(예: <c>TumorSpider</c>).
        /// 비어 있으면 빈 문자열.
        ///
        /// 이 한 이름이 <b>세 곳을 묶는다</b>:
        /// <code>
        ///   Resources/MonsterSkins/&lt;종&gt;/Skin_&lt;종&gt;   외형 에셋
        ///   Art/Char_Asset/Char_Asset_&lt;종&gt;/…          프레임 원본
        ///   씬의 &lt;종&gt;_Template                        복제할 템플릿
        /// </code>
        /// 그래서 표에 종 이름만 적으면 나머지가 따라온다
        /// (<see cref="NeutralMonsterSpawner.FindTemplateFor"/> 주석 참조).
        /// </summary>
        public string SpeciesName => skinAssetName != null ? skinAssetName.Trim() : "";

        // ==================================================================
        // ★ 서식지 (mon_type == Epic) — 롤 정글 캠프 방식 (유저 지시 2026-08-15)
        //
        // <b>일반(Normal)과 무엇이 다른가</b>
        //   · 일반 — <b>성역 중심</b>의 고리(spawn_range_min~max) 안을 계속 배회한다.
        //   · 에픽 — <b>자기가 태어난 자리</b>를 중심으로 한 원이 서식지다. 그 중앙에서
        //     <b>가만히 기다리다가</b>, 맞으면 서식지 밖 일정 거리까지만 쫓고 돌아온다.
        //
        // 기준점이 성역에서 <b>자기 스폰 지점</b>으로 바뀌는 것이 핵심이다. 성역 기준으로
        // 두면 맵 반대편의 에픽이 서로 같은 고리를 공유해 "자기 자리" 라는 개념이 생기지 않는다.
        //
        // ⚠ 값은 <b>에디터에서 조정한다</b>(유저 지시: "타일 계산 값들은 에딧에서 수정할 수
        //   있도록"). 표에는 넣지 않았다 — 연출·손맛에 해당하는 값이라 밸런싱 중 자주 바뀐다.
        // ==================================================================

        [Header("★ 서식지 (에픽 전용)")]
        [Tooltip("에픽 중립 몬스터인가 (표 mon_type == epic).\n" +
                 "켜지면 성역 고리 배회 대신 <b>자기 스폰 지점 중심의 서식지</b>를 쓴다")]
        public bool epic = false;

        [Tooltip("서식지 반지름(타일). 이 원의 <b>중앙에서 대기</b>한다.\n" +
                 "에픽이 아니면 쓰이지 않는다")]
        [Min(1f)] public float habitatRadiusTiles = 12f;

        [Tooltip("서식지 <b>경계에서</b> 이만큼 더 나갈 때까지만 쫓는다(타일).\n" +
                 "이 선을 넘으면 추격을 포기하고 서식지 중앙으로 돌아간다 — 롤 정글 캠프와 같다")]
        [Min(0f)] public float habitatChaseTiles = 6f;

        /// <summary>
        /// 서식지 중앙에서 이 거리 안이면 "제자리" 로 본다(타일).
        /// 0 이면 정확히 중앙에 붙어 서므로 살짝 여유를 준다.
        /// </summary>
        [Min(0f)] public float habitatIdleSlackTiles = 1f;

        [Tooltip("★ 이 종이 쓰는 스킬 id (표 mon_skill_1 · mon_skill_2).\n" +
                 "웨이브 보스의 boss_skill_1~3 과 <b>같은 규칙</b>이다 — 순서가 곧 슬롯 번호이고, " +
                 "스킨의 skill1*/skill2* 모션과 짝이 된다. 0 은 빈 칸이라 건너뛴다.\n" +
                 "에셋은 Resources/BossSkills 에서 그 번호로 찾는다 (BossSkillCaster)")]
        public int[] skillIds;

        /// <summary>스킬을 하나라도 가진 종인가 — 스포너가 BossSkillCaster 를 붙일지 정한다.</summary>
        public bool HasSkills
        {
            get
            {
                if (skillIds == null) return false;
                for (int i = 0; i < skillIds.Length; i++)
                    if (skillIds[i] > 0) return true;
                return false;
            }
        }

        [Tooltip("서식지 바닥에 깔 <b>타일 묶음 이름</b> (표 habitat_design 시트의 habitat_tile_asset).\n" +
                 "Resources/HabitatTiles/<이름>/ 폴더의 타일을 전부 후보로 쓴다 — 예: CarcinosHabitat.\n" +
                 "비어 있으면 서식지를 바닥에 그리지 않는다(예전 동작)")]
        public string habitatTileAsset = "";

        /// <summary>
        /// 서식지 <b>바닥</b> 타일 폴더의 Resources 경로. 없으면 빈 문자열.
        ///
        /// <b>왜 폴더째인가</b> — 타일이 32종이라 개별 참조를 표에 적을 수 없고, SO 는 씬 참조를
        /// 가질 수 없다(진행상황 8절 4번). 스킨과 <b>같은 규약</b>이다: 표에는 이름 하나만 적고
        /// 코드가 <c>Resources.LoadAll</c> 로 폴더를 통째로 읽는다.
        ///
        /// ★ 2026-08-16 부터 묶음이 <b>셋</b>이다 — 이름 하나에서 나머지 둘을 <b>접미사로</b>
        /// 만든다(스킨이 종 이름 하나로 폴더를 찾는 것과 같은 방식):
        /// <code>
        ///   CarcinosHabitat        바닥
        ///   CarcinosHabitatEdge    가장자리 한 칸
        ///   CarcinosHabitatProps   바닥 위 데코
        /// </code>
        /// </summary>
        public string HabitatTileResourcePath => HabitatPath("");

        /// <summary>서식지 <b>가장자리</b> 타일 폴더. 없으면 빈 문자열.</summary>
        public string HabitatEdgeResourcePath => HabitatPath("Edge");

        /// <summary>서식지 <b>데코</b> 타일 폴더. 없으면 빈 문자열.</summary>
        public string HabitatPropResourcePath => HabitatPath("Props");

        string HabitatPath(string suffix)
        {
            string s = habitatTileAsset != null ? habitatTileAsset.Trim() : "";
            return s.Length == 0 ? "" : "HabitatTiles/" + s + suffix;
        }

        /// <summary>
        /// 웨이브 배율 없이 쓰는 능력치 묶음 — <b>체력 배율</b>(<see cref="hpPercent"/>)과
        /// <b>사냥 성장 배율</b>만 반영한다.
        ///
        /// ★ 2026-08-15 부터 <b>12칸을 전부</b> 채운다 — 예전에는 hp/attack/defense/regen
        /// 네 칸만 채워서, 표에 <c>accuracy</c>·<c>critical</c> 이 적혀 있어도 게임에
        /// 반영되지 않았고 원거리 종(1002)은 <c>ranged_atk</c> 칸이 아예 없었다.
        ///
        /// ★★ 2026-08-21 — <paramref name="growth"/> (사냥 성장 · <see cref="NeutralKillTally"/> ·
        /// 수치는 <b>하이라키 GameSystems ▸ NeutralGrowthService</b>).
        /// <b>어디에 걸고 어디에 안 거는가</b>를 웨이브 몬스터와 <b>같은 규칙</b>으로 맞췄다
        /// (유저 지시: *"체력 말고는 상한값 웨이브 몬스터와 동일하게"*):
        ///
        /// <list type="bullet">
        /// <item><b>체력</b> — 배율을 걸고 <b>상한 없이</b> 오른다
        ///       (<see cref="BalanceConfigSO.monsterHpStatMax"/> 는 기본 0 = 무제한)</item>
        /// <item><b>공격 계열 4칸</b>(근거리·원거리·마법·회복) — 배율을 걸고
        ///       <b>웨이브 몬스터와 같은 상한</b>으로 자른다
        ///       (<see cref="BalanceConfigSO.AttackStatMaxFor"/> · 에픽은 보스 상한을 쓴다)</item>
        /// <item><b>그 밖의 다섯 칸</b>(방어·재생·명중·치명·저항) — <b>배율을 걸지 않는다.</b>
        ///       웨이브 배율도 이 칸들은 건드리지 않는다(<c>MonsterDefinitionSO.BuildStats</c>
        ///       의 ⚠ 주석) — 두 곳의 규칙이 갈리면 «어느 쪽이 맞나» 를 매번 다시 물어야 한다</item>
        /// </list>
        ///
        /// ⚠ <paramref name="balance"/> 가 null 이면 <b>상한이 없다</b> — 표를 안 쓰는 옛 경로·
        ///   테스트에서 값이 조용히 달라지지 않게 «예전 동작» 을 기본으로 둔 것이다
        ///   (웨이브 쪽 <c>BuildStats</c> 와 같은 판단).
        /// </summary>
        public StatBlock BuildStats(float growth = 1f, BalanceConfigSO balance = null)
        {
            float mul = growth > 0f ? growth : 1f;
            int hpMax = balance != null ? balance.monsterHpStatMax : 0;
            int atkMax = balance != null ? balance.AttackStatMaxFor(epic) : 0;

            int Grown(int raw, int lowFloor, int cap) =>
                Mathf.Max(lowFloor, BalanceConfigSO.CapStat(Mathf.RoundToInt(raw * mul), cap));

            // 체력만 두 배율을 겹쳐 받는다 — 표의 hp_percent 는 «이 종의 척도», 성장은 «판의 진행».
            int hpScaled = BalanceConfigSO.ScaleByPercent(Mathf.Max(1, hpStat), hpPercent);

            return new StatBlock
            {
                hp           = Grown(hpScaled, 1, hpMax),
                attack       = Grown(Mathf.Max(0, attackStat), 0, atkMax),
                rangedAttack = Grown(Mathf.Max(0, rangedAttackStat), 0, atkMax),
                magic        = Grown(Mathf.Max(0, magicStat), 0, atkMax),
                cure         = Grown(Mathf.Max(0, cureStat), 0, atkMax),
                defense      = Mathf.Max(0, defenseStat),
                regen        = Mathf.Max(0, regenStat),
                accuracy     = Mathf.Max(0, accuracyStat),
                critical     = Mathf.Max(0, criticalStat),
                resistance   = Mathf.Max(0, resistanceStat),
            };
        }
    }
}
