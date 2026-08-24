using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Resource;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 캐릭터 강화 규칙. 비용 계산과 성장 적용만 담당하고, 클릭/버튼 같은 입력은
    /// UI 쪽(<c>UpgradeButtonUI</c>)이 맡는다.
    ///
    /// <b>비용은 캐릭터마다 따로 올라간다</b> — 별도 저장소를 두지 않고
    /// <see cref="CharacterUnit.UpgradeCount"/>(이미 캐릭터마다 직렬화되는 값)에서
    /// 계산한다. 그래서 캐릭터가 늘어나든 죽든 비용 장부를 따로 관리할 필요가 없고,
    /// 캐릭터를 새로 만들면 횟수 0 이라 자동으로 기본 비용부터 시작한다.
    ///
    ///   비용 = baseCost + costIncreasePerUpgrade × (그 캐릭터의 강화 횟수)
    ///
    /// ★★ <b>Lv30 부터는 그 선형 곡선이 끝나고 등비로 꺾인다</b> (2026-08-24 · 유저 지시:
    /// *"30lv 이상부터 강화 밸류 엄청 올리기 … 30LV 이상 부터는 강화에 소모되는 자원
    /// 소모량을 급진적으로 올려야 할듯"*). <see cref="steepStartLevel"/> 절 참조.
    /// </summary>
    public class CharacterUpgradeService : MonoBehaviour
    {
        [Header("비용 (캐릭터별로 따로 올라감)")]
        [Tooltip("한 번도 강화하지 않은 캐릭터의 강화 비용")]
        [Min(0)] [SerializeField] int baseCost = 20;

        [Tooltip("그 캐릭터를 한 번 강화할 때마다 다음 비용에 더해지는 양")]
        [Min(0)] [SerializeField] int costIncreasePerUpgrade = 10;

        // ──────────────────────────────────────────────────────────────────
        // ★★ Lv30 이후의 «벽» (2026-08-24 신설)
        //
        // <b>왜 필요했나</b> — 밸런스 기획서는 후반부(21~30웨이브)를 «성장·생성에
        // 하드캡이 걸리는 구간» 으로 정의하고, 30웨이브 레기미아를 «Lv30 3부대와 싸워도
        // 근소하게 승리» 로 못박았다. 그런데 비용이 `40 + 10n` 선형이면 Lv30 을 넘긴 뒤에도
        // 다음 한 칸이 그 앞 칸보다 3% 밖에 안 비싸다 — 후반의 남는 에너지가 그대로
        // 레벨로 환산되어 <b>천장이 없다</b>. 136절이 잡은 «w30 = Lv30» 착지도 그 위쪽이
        // 열려 있으면 «목표» 가 아니라 «지나가는 점» 이 된다.
        //
        // <b>어떻게</b> — <see cref="steepStartLevel"/> 미만은 <b>예전 그대로</b>(선형)이고,
        // 그 이상은 «그 레벨의 선형 비용» 에서 시작해 레벨마다
        // <see cref="steepGrowthPerLevel"/> 배씩 곱한다. 즉 Lv30 에 <b>도달하는</b> 비용은
        // 한 톨도 안 변하고(= 136절의 경제 모델이 그대로 유효하다), Lv30 <b>을 넘어서는</b>
        // 값만 급격해진다. 유저 지시의 «30LV 이상 부터» 가 정확히 이 뜻이다.
        //
        //      n < 30 : 40 + 10n                    (Lv29 → 330 · 예전과 같다)
        //      n ≥ 30 : (40 + 10×30) × 1.35^(n−30)  (Lv30 340 · Lv35 1,520 · Lv40 6,830)
        //
        // ⚠ <b>반올림 자리를 못박는다</b>(<see cref="costRoundTo"/>) — 등비로 곱하면
        //   1,523 같은 값이 나오는데, 이 게임의 다른 비용은 전부 10 단위다(생성 200+150n ·
        //   강화 40+10n). 화면에 찍히는 숫자의 «자리» 가 갑자기 달라지면 값을 잘못 넣은 것처럼
        //   보인다. 그래서 10 단위로 맞춘다.
        //
        // ⚠ 저장·복원은 손댈 것이 없다 — 이 프로젝트는 비용을 <b>장부에 적지 않고</b>
        //   강화 횟수에서 매번 계산한다(위 클래스 주석). 곡선을 바꾸면 옛 세이브도 새 곡선을 쓴다.
        // ──────────────────────────────────────────────────────────────────

        [Tooltip("★ 이 강화 횟수(=레벨)부터 비용이 <b>선형에서 등비로</b> 꺾인다. " +
                 "0 이면 꺾이지 않는다(예전 동작).\n" +
                 "밸런스 기획서의 «후반부는 성장에 하드캡» 을 값으로 옮긴 자리다 — " +
                 "Lv30 까지 올라가는 비용은 이 값을 바꿔도 <b>변하지 않는다</b>")]
        [Min(0)] [SerializeField] int steepStartLevel = 30;

        [Tooltip("★ steepStartLevel 이후 <b>레벨 하나당 비용에 곱하는 배율</b>. " +
                 "1.35 면 다섯 레벨마다 약 4.5배다(Lv30 340 → Lv35 1,520 → Lv40 6,830).\n" +
                 "1 이하면 등비가 걸리지 않는다(선형과 같아진다)")]
        [Min(1f)] [SerializeField] float steepGrowthPerLevel = 1.35f;

        [Tooltip("등비 구간의 비용을 이 단위로 반올림한다. 이 게임의 다른 비용이 전부 " +
                 "10 단위여서 자리를 맞춘다. 0·1 이면 반올림하지 않는다")]
        [Min(0)] [SerializeField] int costRoundTo = 10;

        // ──────────────────────────────────────────────────────────────────
        // 성장량 — 유저 지시 2026-08-14로 전면 교체됐다.
        //
        //   이전: growthMin(1) ~ growthMax(5) 균등 랜덤. 1·2·3·4·5 가 전부 20%.
        //   지금: 아래 growthWeights 6칸의 <b>가중 추첨</b>.
        //         · 일반 능력치       → growthBaseMin + 굴림(0~5)      = <b>0~5</b>
        //         · 성장 유형에 묶인  → 거기에 growthFocusBonus(+1)    = <b>1~6</b>
        //         양 끝(0·5 / 1·6)은 확률이 낮고 가운데(2·3 / 3·4)가 높다.
        //
        // ⚠ growthMin/growthMax 필드를 <b>이름째로 없앴다</b> — 그래야 씬에 직렬화돼 있던
        //   옛 값(1/5, 진행상황 29-5절)이 버려지고 새 구조가 들어온다(49-1절의 그 원리).
        // ──────────────────────────────────────────────────────────────────

        [Header("성장량 — 강화 1회에 각 능력치가 오르는 폭의 확률 분포")]
        [Tooltip("굴림 결과 0·1·2·3·4·5 각각의 가중치. 합이 100 일 필요는 없다(비율로만 쓴다).\n" +
                 "★ 여기가 '각 수치마다 상승 확률'을 조절하는 자리다 — 양 끝을 낮추고 " +
                 "가운데를 높이면 평균 근처가 자주 나온다.\n" +
                 "일반 능력치는 이 굴림이 그대로 0~5, 성장 유형에 묶인 능력치는 +1 되어 1~6 이 된다")]
        [SerializeField] int[] growthWeights = { 8, 17, 25, 25, 17, 8 };

        [Tooltip("일반(성장 유형에 안 묶인) 능력치의 최솟값. 0 이면 '안 오를 수도 있다'")]
        [Min(0)] [SerializeField] int growthBaseMin = 0;

        [Tooltip("성장 유형에 묶인 능력치가 추가로 받는 값. 1 이면 0~5 가 1~6 이 된다")]
        [Min(0)] [SerializeField] int growthFocusBonus = 1;

        [Header("랜덤")]
        [Tooltip("같은 시드 = 항상 같은 성장 결과. 밸런싱 테스트에 필요")]
        [SerializeField] int seed = 20260804;

        [Tooltip("켜면 실행할 때마다 다른 성장값이 나온다")]
        [SerializeField] bool randomizeSeed = true;

        [Header("디버그")]
        [SerializeField] bool logUpgrades = true;

        System.Random _rng;

        public static CharacterUpgradeService Instance { get; private set; }

        /// <summary>강화가 성사될 때마다 발생 (대상, 소비한 비용).</summary>
        public event System.Action<CharacterUnit, int> OnUpgraded;

        void Awake()
        {
            Instance = this;
            _rng = new System.Random(
                randomizeSeed ? Random.Range(int.MinValue, int.MaxValue) : seed);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------

        /// <summary>이 캐릭터를 지금 강화하는 데 드는 비용.</summary>
        public int CostFor(CharacterUnit unit) =>
            unit == null ? 0 : CostForLevel(unit.UpgradeCount);

        /// <summary>
        /// 강화 횟수 <paramref name="level"/> 인 캐릭터의 다음 강화 비용.
        ///
        /// ★ <b>유닛 없이도 부를 수 있게</b> 갈라 뒀다 — 곡선을 표(`능력치 및 공식 정리.xlsx`)와
        /// 대조할 때 필요하고, 에디터 도구·시뮬레이션이 캐릭터를 만들지 않고 값을 확인할 수 있다.
        /// </summary>
        public int CostForLevel(int level)
        {
            if (level < 0) level = 0;

            int linear = baseCost + costIncreasePerUpgrade * level;

            // 선형 구간 — 예전과 <b>한 톨도 다르지 않다</b>.
            if (steepStartLevel <= 0 || level < steepStartLevel || steepGrowthPerLevel <= 1f)
                return linear;

            // 등비 구간 — 꺾이는 지점의 «선형 비용» 에서 출발한다. 그래서 Lv30 의 비용은
            // 예전과 같고(연속이다), 그 위로만 급해진다.
            int atStart = baseCost + costIncreasePerUpgrade * steepStartLevel;
            double cost = atStart * System.Math.Pow(steepGrowthPerLevel, level - steepStartLevel);

            if (costRoundTo > 1)
                cost = System.Math.Round(cost / costRoundTo) * costRoundTo;

            // ⚠ int 로 넘치지 않게 자른다 — 1.35^n 은 Lv90 쯤에서 int 를 넘는다.
            //   무한 모드에서 그 레벨에 닿을 일은 없지만, 넘치면 <b>음수 비용</b>이 되어
            //   «공짜로 무한 강화» 가 된다(가장 나쁜 방향의 고장이다).
            return cost >= int.MaxValue ? int.MaxValue : (int)cost;
        }

        /// <summary>
        /// 지금 강화할 수 있는지 (대상이 살아있고 <b>강화가 허용되며</b> 에너지가 충분한지).
        ///
        /// ★★ <b>소환수는 강화할 수 없다</b> (2026-08-20 · 유저 리포트
        ///   *"골렘 강화/전술 변경 가능한 버그"*). 「강림」(80024) 정의문의 마지막 문장이
        ///   <b>"골렘은 강화할 수 없습니다"</b> 다.
        ///
        ///   <b>왜 여기인가</b> — 골렘은 로스터에 <b>보여야 하는</b> 유닛이므로
        ///   (정의문: «로스터에는 표기 되지만») 성장 창에 들어오는 것 자체는 정상이다.
        ///   막을 곳은 <b>강화라는 행위</b> 하나이고, 그 판정이 지나는 문은 여기다 —
        ///   UI 의 버튼 잠금(<c>CharacterGrowthPanel</c>)도 이 함수를 읽으므로
        ///   <b>한 군데만 고치면 버튼과 실제 처리가 같이 닫힌다</b>.
        ///   ⚠ 골렘의 능력치는 소환 시점의 아루에서 <b>계산해서 만든 것</b>이라
        ///     (<see cref="Combat.AruGolem"/>) 여기서 올려도 다음 소환에 남지 않는다.
        ///     즉 «올려도 사라지는 강화» 에 에너지를 쓰게 되는 것이 진짜 손해였다.
        /// </summary>
        public bool CanUpgrade(CharacterUnit unit)
        {
            if (unit == null || !unit.IsAlive) return false;
            if (unit.IsSummoned) return false;          // 골렘 — 위 ★★
            ResourceManager resources = ResourceManager.Instance;
            return resources != null && resources.CanAfford(CostFor(unit));
        }

        /// <summary>
        /// 에너지를 소비하고 능력치를 성장시킨다. 에너지가 부족하면 아무것도 하지 않는다.
        /// </summary>
        public bool TryUpgrade(CharacterUnit unit)
        {
            if (unit == null || !unit.IsAlive) return false;
            // ⚠ <b>여기서도 막는다</b> — 이 프로젝트의 규칙이다("막는 곳을 한 군데만 두면
            //   다른 경로로 새어 들어온다" · CharacterTactics 의 잠금 주석과 같다).
            if (unit.IsSummoned) return false;

            ResourceManager resources = ResourceManager.Instance;
            if (resources == null)
            {
                Debug.LogWarning("[Upgrade] ResourceManager 를 찾을 수 없어 강화할 수 없습니다.", this);
                return false;
            }

            int cost = CostFor(unit);
            if (!resources.TrySpend(cost))
            {
                if (logUpgrades)
                    Debug.Log($"[Upgrade] 에너지 부족 — {unit.name} 강화에 {cost} 필요, " +
                              $"보유 {resources.Energy}");
                return false;
            }

            StatBlock before = unit.Stats;
            StatBlock grown = Grow(before, unit.Balance, unit.GrowthFocus);
            unit.ApplyUpgrade(grown);

            if (logUpgrades)
                Debug.Log($"[Upgrade] {unit.DisplayName} Lv.{unit.UpgradeCount} · 비용 {cost} · " +
                          $"성장 유형 {StatGrowthFocusTable.Label(unit.GrowthFocus)} · " +
                          $"{before} → {grown} · 다음 비용 {CostFor(unit)}", unit);

            OnUpgraded?.Invoke(unit, cost);
            return true;
        }

        /// <summary>
        /// <b>자원을 쓰지 않고</b> 강화한다 — 정신 이상 "고조"(자원 소모 없이 강화됨)가 쓴다.
        ///
        /// 강화 횟수(<see cref="CharacterUnit.UpgradeCount"/>)는 정상적으로 오른다. 테이블의
        /// "자원 소모는 없으니 자원 소모 값은 동일하게 상승" 이 정확히 이 동작이다 — 이 프로젝트는
        /// 강화 비용을 별도 장부가 아니라 그 캐릭터의 강화 횟수에서 계산하므로(<see cref="CostFor"/>),
        /// <see cref="CharacterUnit.ApplyUpgrade"/> 를 부르는 것만으로 "공짜로 강화됐지만 다음
        /// 강화 비용은 그만큼 올라간다"가 그대로 성립한다.
        /// </summary>
        /// <returns>실제로 적용된 강화 횟수.</returns>
        public int GrowFree(CharacterUnit unit, int times)
        {
            if (unit == null || !unit.IsAlive || times <= 0) return 0;

            int applied = 0;
            for (int i = 0; i < times; i++)
            {
                StatBlock before = unit.Stats;
                StatBlock grown = Grow(before, unit.Balance, unit.GrowthFocus);
                unit.ApplyUpgrade(grown);
                applied++;
            }

            if (logUpgrades)
                Debug.Log($"[Upgrade] {unit.DisplayName} 무료 강화 {applied}회(정신 이상 고조) · " +
                          $"Lv.{unit.UpgradeCount} · 다음 비용 {CostFor(unit)}", unit);

            OnUpgraded?.Invoke(unit, 0);
            return applied;
        }

        /// <summary>
        /// 성장 가능한 능력치마다 각각 독립적인 랜덤값을 더한다. 능력치 상한(기본 100)에서 잘린다.
        ///
        /// <b>저항력은 오르지 않는다</b>(<see cref="StatBlock.IsGrowable"/>) —
        /// 캐릭터 고유의 고정 능력치이기 때문이다(캐릭터 가이드 p5).
        /// 그래서 <see cref="StatBlock.Roll"/> 을 그대로 쓰지 않고 능력치마다 따로 굴린다:
        /// Roll 은 저항력을 50 으로 고정해 넣기 때문에 여기서 쓰면 저항력이 매번 50 이 더해진다.
        ///
        /// <b>성장 유형</b>(<paramref name="focus"/>)에 묶인 능력치는
        /// <see cref="growthFocusBonus"/> 만큼 더 오른다 — 같은 확률 분포를 한 칸 밀어 쓰는 것이라
        /// "잘 오른다"가 확실하면서도 최댓값만 나오는 식으로 망가지지 않는다.
        /// </summary>
        StatBlock Grow(StatBlock current, BalanceConfigSO balance, StatGrowthFocus focus)
        {
            int statMax = balance != null ? balance.statMax : 100;

            StatBlock result = current;
            for (int i = 0; i < (int)StatType.COUNT; i++)
            {
                var t = (StatType)i;
                if (!StatBlock.IsGrowable(t)) continue;   // 저항력은 고정

                int growth = growthBaseMin + RollGrowthStep();
                if (StatGrowthFocusTable.IsFavored(focus, t)) growth += growthFocusBonus;

                result[t] = Mathf.Min(statMax, current[t] + growth);
            }
            return result;
        }

        /// <summary>
        /// <see cref="growthWeights"/> 를 가중치로 삼아 0 ~ (칸 수 −1) 중 하나를 뽑는다.
        ///
        /// 표가 비었거나 합이 0 이면 <b>균등 추첨</b>으로 떨어진다 — 인스펙터에서 값을 다 지워도
        /// 강화가 멈추지 않게 하려는 안전장치다(이 프로젝트가 여러 번 겪은 "설정이 비면 기능이
        /// 조용히 죽는다" 를 막는다).
        /// </summary>
        int RollGrowthStep()
        {
            int count = growthWeights != null ? growthWeights.Length : 0;
            if (count == 0) return 0;

            int total = 0;
            for (int i = 0; i < count; i++) total += Mathf.Max(0, growthWeights[i]);
            if (total <= 0) return _rng.Next(count);

            int pick = _rng.Next(total);
            for (int i = 0; i < count; i++)
            {
                pick -= Mathf.Max(0, growthWeights[i]);
                if (pick < 0) return i;
            }
            return count - 1;
        }
    }
}
