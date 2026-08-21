using UnityEngine;

namespace LastSanctuary.Units
{
    /// <summary>
    /// ★★ <b>중립 몬스터 사냥 성장</b> — 같은 종을 잡을수록 <b>다음 개체</b>가 강해진다
    /// (2026-08-21 · 유저 지시: *"중립 몬스터 같은 개체를 일정 마리 이상 사냥할 경우 배율이
    /// 적용 되는 로직 만들어줘 우선은 10마리당 0.1 배율 추가로 만들고 … 이거 에딧에서
    /// 조정가능하게 만든다음 어디에 넣었는지 알려줘"* · *"중립 몬스터 성장 배율에 자원값도
    /// 배율 적용 되어야 해"*).
    ///
    /// <b>어디서 조정하나 — 하이라키 <c>GameSystems</c> 다.</b>
    /// <code>
    /// Hierarchy ▸ GameSystems  ▸ Inspector ▸ Neutral Growth Service
    /// </code>
    /// ⚠⚠ 처음에는 이 값들을 <see cref="Combat.BalanceConfigSO"/>(에셋)에 뒀는데,
    ///   유저가 원한 것은 <b>하이라키에서 바로 만지는 것</b>이었다(*"에딧모드에서 변경 가능하게
    ///   해달라고"*). 그래서 <b>씬 컴포넌트로 옮겼다</b> — 침식 수치가
    ///   <see cref="Combat.ErosionService"/> 로 <c>GameSystems</c> 에 있는 것과 같은 자리다.
    ///   <b>수치의 정본은 이제 이 컴포넌트 하나뿐</b>이다(에셋 쪽 칸은 지웠다 — 두 곳에 있으면
    ///   어느 쪽이 이기는지 매번 다시 물어야 한다).
    ///
    /// <b>왜 «다음 개체» 인가</b> — 배율은 <b>소환 순간에 굳는다</b>
    /// (<see cref="NeutralMonsterUnit.Initialize"/>). 이미 서 있는 개체가 갑자기 세지면
    /// 플레이어가 보고 있던 싸움의 규칙이 도중에 바뀌는 셈이다.
    ///
    /// <list type="table">
    /// <item><term>세는 곳</term><description><see cref="NeutralKillTally"/>(종별 처치 수)</description></item>
    /// <item><term>쓰는 곳</term><description><see cref="NeutralMonsterUnit.Initialize"/> ·
    ///   <see cref="NeutralMonsterDefinitionSO.BuildStats"/></description></item>
    /// </list>
    ///
    /// ★ <b>능력치 상한은 여기 없다</b> — 체력은 상한 없이 오르고 공격 계열은
    ///   <b>웨이브 몬스터와 같은 칸</b>(<c>BalanceConfigSO.monsterAttackStatMax</c> ·
    ///   <c>bossAttackStatMax</c>)으로 자른다(유저 확정: *"체력 말고는 상한값 웨이브 몬스터와
    ///   동일하게"*). 그 값은 <b>웨이브와 공유</b>하는 밸런스라 에셋에 있는 것이 맞다.
    /// </summary>
    public class NeutralGrowthService : MonoBehaviour
    {
        [Header("중립 몬스터 사냥 성장 (같은 종을 잡을수록 다음 개체가 강해진다)")]
        [Tooltip("끄면 처치 수를 세기만 하고 배율을 걸지 않는다(예전 동작).")]
        [SerializeField] bool growthEnabled = true;

        [Tooltip("몇 마리를 잡을 때마다 한 단계 오르는가. 기본 10마리.")]
        [Min(1)] [SerializeField] int killsPerStep = 10;

        [Tooltip("한 단계당 더해지는 배율. 기본 0.1 = 10마리마다 +10%.\n" +
                 "10마리 → x1.1 · 50마리 → x1.5 · 100마리 → x2.0")]
        [Min(0f)] [SerializeField] float stepMultiplier = 0.1f;

        [Tooltip("배율 상한(0 = 무제한). 예: 3 이면 x3 에서 멈춘다.\n" +
                 "⚠ 능력치 상한은 이것과 별개다 — 체력은 무제한, 공격 계열은 웨이브 몬스터와 " +
                 "같은 칸(BalanceConfig 의 monsterAttackStatMax·bossAttackStatMax)을 쓴다")]
        [Min(0f)] [SerializeField] float maxMultiplier = 0f;

        [Tooltip("처치 보상 에너지에도 같은 배율을 곱한다.\n" +
                 "강해진 개체는 그만큼 더 준다 — 안 그러면 «잡기만 어렵고 이득은 같다» 가 된다")]
        [SerializeField] bool scaleEnergyReward = true;

        [Header("디버그")]
        [Tooltip("개체가 소환될 때 «종 id · 처치 수 · 배율» 을 콘솔에 찍는다")]
        [SerializeField] bool logGrowth = false;

        public static NeutralGrowthService Instance { get; private set; }

        // ------------------------------------------------------------------

        public bool GrowthEnabled => growthEnabled;
        public int KillsPerStep => Mathf.Max(1, killsPerStep);
        public float StepMultiplier => Mathf.Max(0f, stepMultiplier);
        public float MaxMultiplier => Mathf.Max(0f, maxMultiplier);
        public bool ScaleEnergyReward => scaleEnergyReward;
        public bool LogGrowth => logGrowth;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 씬에 이 컴포넌트가 없을 때의 <b>안전망</b> — 붙여서 기본값으로 돌게 한다.
        /// <see cref="Events.EventService.EnsureOn"/> 과 같은 취지다(씬을 고치지 않고도
        /// 기능이 죽지 않게).
        /// ⚠ 다만 <b>인스펙터에서 만지려면 실물이 있어야</b> 하므로 씬의 <c>GameSystems</c> 에
        ///   MCP 로 <b>직접 붙여 두었다</b>.
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        static void EnsureOn()
        {
            if (Instance != null) return;
            if (FindFirstObjectByType<NeutralGrowthService>() != null) return;

            GameObject host = GameObject.Find("GameSystems");
            if (host == null) host = new GameObject("GameSystems");
            host.AddComponent<NeutralGrowthService>();
        }
    }
}
