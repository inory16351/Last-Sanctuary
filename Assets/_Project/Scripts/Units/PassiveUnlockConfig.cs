using UnityEngine;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 패시브 스킬이 <b>몇 번째 강화에 열리는지</b>를 정하는 설정. 씬의 <c>GameSystems</c> 에 붙어 있고
    /// <b>인스펙터에서 바로 고칠 수 있다</b>(유저 확정 2026-08-11 — 값을 코드나 캐릭터 에셋에
    /// 박아두지 말 것).
    ///
    /// <b>왜 캐릭터 에셋이 아니라 씬에 두는가</b> — 해금 시점은 캐릭터마다 다른 값이 아니라
    /// <b>게임 전체의 성장 곡선</b>이다. 캐릭터 에셋마다 넣어두면 캐릭터를 추가할 때마다 같은 숫자를
    /// 다시 적어야 하고, 밸런스를 조정할 때 에셋을 전부 열어야 한다. 여기 한 곳만 고치면 전원에게 적용된다.
    ///
    /// 슬롯 0(첫 번째 패시브)은 <b>생성 시 즉시 해금</b>이다(캐릭터 가이드 p6) — 그래서 이 표에는
    /// 슬롯 1·2의 조건만 둔다.
    /// </summary>
    public class PassiveUnlockConfig : MonoBehaviour
    {
        public static PassiveUnlockConfig Instance { get; private set; }

        [Header("해금 조건 — 강화 횟수")]
        [Tooltip("두 번째 패시브가 열리는 강화 횟수")]
        [Min(0)] [SerializeField] int unlockUpgradesSkill2 = 5;

        [Tooltip("세 번째 패시브가 열리는 강화 횟수")]
        [Min(0)] [SerializeField] int unlockUpgradesSkill3 = 10;

        /// <summary>
        /// 씬에 이 컴포넌트가 없을 때 쓰는 값. 인스펙터 기본값과 같게 유지할 것 —
        /// 두 값이 갈리면 "씬에 붙였을 때와 안 붙였을 때 게임이 다르게 동작"해서 추적이 어려워진다.
        /// </summary>
        const int FallbackSkill2 = 5;
        const int FallbackSkill3 = 10;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 이 슬롯이 열리는 데 필요한 강화 횟수. 슬롯 0 은 항상 0(즉시 해금).
        /// 정의되지 않은 슬롯은 <see cref="int.MaxValue"/> — 영원히 안 열린다.
        /// </summary>
        public int RequiredUpgradesFor(int slot) => slot switch
        {
            0 => 0,
            1 => unlockUpgradesSkill2,
            2 => unlockUpgradesSkill3,
            _ => int.MaxValue,
        };

        /// <summary>
        /// 씬에 설정이 없어도 게임이 멈추지 않게 하는 정적 진입점.
        /// <c>CharacterUnit</c> · <c>CharacterGrowthPanel</c> 둘 다 이 경로만 쓴다.
        /// </summary>
        public static int RequiredUpgrades(int slot)
        {
            if (Instance != null) return Instance.RequiredUpgradesFor(slot);

            return slot switch
            {
                0 => 0,
                1 => FallbackSkill2,
                2 => FallbackSkill3,
                _ => int.MaxValue,
            };
        }

        /// <summary>강화 횟수 기준으로 이 슬롯이 해금됐는가.</summary>
        public static bool IsUnlocked(int slot, int upgradeCount) =>
            upgradeCount >= RequiredUpgrades(slot);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;
    }
}
