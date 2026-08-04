using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 능력치 치환 공식과 전투 상수. 계수를 코드에 박지 않고 전부 여기에 두어
    /// 에디터에서 조정하면서 밸런싱할 수 있게 한다.
    ///
    /// <b>치환 결과는 전부 정수다.</b> 계수까지 모두 int 로 두고 정수 연산만 쓰므로
    /// 체력 · 타격 · 피해량 · 회복량에 소수점이 나오지 않는다.
    /// (사거리 · 이동속도 · 공격속도는 능력치 치환이 아니라 시간/거리 파라미터라 실수로 둔다)
    ///
    /// 공식 근거는 「프로토 타입 캐릭터 생성 규칙.md」 참조.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/Combat/Balance Config", fileName = "BalanceConfig")]
    public class BalanceConfigSO : ScriptableObject
    {
        [Header("능력치 척도")]
        [Tooltip("능력치의 최소값. 유저에게 보이는 값과 동일한 척도")]
        [Min(1)] public int statMin = 1;

        [Tooltip("능력치의 최대값")]
        [Min(1)] public int statMax = 100;

        [Header("생성 시 랜덤 범위")]
        [Tooltip("캐릭터 생성 시 각 능력치가 이 범위에서 균등 랜덤으로 결정된다")]
        [Min(1)] public int initialStatMin = 1;
        [Min(1)] public int initialStatMax = 10;

        [Header("체력 :  maxHp = base + 능력치 × perStat  (정수)")]
        [Tooltip("능력치 1 → 50, 10 → 140, 100 → 1040. 전부 10 단위로 떨어진다")]
        public int hpBase = 40;
        [Min(1)] public int hpPerStat = 10;

        [Header("공격력 :  attack = base + 능력치 × perStat  (정수)")]
        [Tooltip("능력치 1 → 4, 10 → 22, 100 → 202. 전부 짝수라 방어력 50(절반 감소)에서도 정수")]
        public int attackBase = 2;
        [Min(1)] public int attackPerStat = 2;

        [Header("방어력 :  받는 피해 = 공격력 × k / (k + 능력치 × perStat)")]
        [Tooltip("작을수록 방어력의 효율이 높아진다. 50 이면 능력치 50 에서 정확히 절반 감소")]
        [Min(1)] public int defenseK = 50;
        [Min(1)] public int defensePerStat = 1;

        [Header("체력 회복력 :  회복 틱마다 (능력치 × perStat) 만큼 정수 회복")]
        [Tooltip("회복 틱 간격(초). 10 이면 능력치 10 인 유닛이 10초마다 10 회복 = 초당 1")]
        [Min(0.1f)] public float regenTickSeconds = 10f;
        [Min(0)] public int regenPerStat = 1;

        [Header("피해 공통")]
        [Tooltip("방어력이 아무리 높아도 이 값은 관통한다. 무적 방지")]
        [Min(0)] public int minDamage = 1;

        [Header("프로토타입 고정 상수 (능력치로 분리되지 않은 값)")]
        [Tooltip("초당 공격 횟수. 정식 버전의 공격속도 능력치 자리")]
        [Min(0.05f)] public float attacksPerSecond = 1f;

        [Tooltip("초당 이동 타일 수")]
        [Min(0f)] public float moveSpeedTilesPerSecond = 3f;

        [Tooltip("근접 공격 사거리 (타일)")]
        [Min(0.1f)] public float meleeRangeTiles = 1.2f;

        [Tooltip("시야 반경 (타일). 2D Light 반경에 사용")]
        [Min(0.1f)] public float sightTiles = 8f;

        [Header("체력 재생 규칙")]
        [Tooltip("전투(공격했거나 피해를 입은 상황)에서 벗어난 뒤 " +
                 "재생이 시작되기까지 기다리는 시간(초).\n" +
                 "0 이면 전투 중에도 재생된다")]
        [Min(0f)] public float outOfCombatRegenDelay = 5f;

        // ------------------------------------------------------------------
        // 치환 공식 — 반환값은 모두 정수
        // ------------------------------------------------------------------

        /// <summary>능력치 → 최대 체력.</summary>
        public int MaxHp(int hpStat) => hpBase + hpStat * hpPerStat;

        /// <summary>능력치 → 타격당 공격력.</summary>
        public int Attack(int attackStat) => attackBase + attackStat * attackPerStat;

        /// <summary>능력치 → 회복 틱 1회당 회복량. 실제 회복 간격은 regenTickSeconds.</summary>
        public int RegenPerTick(int regenStat) => Mathf.Max(0, regenStat) * regenPerStat;

        /// <summary>표시용 — 초당 회복량(소수). 실제 회복은 틱 단위 정수로 들어간다.</summary>
        public float RegenPerSecond(int regenStat) =>
            regenTickSeconds > 0f ? RegenPerTick(regenStat) / regenTickSeconds : 0f;

        /// <summary>방어력 비율식의 분모. k + 능력치 × perStat.</summary>
        public int DefenseDenominator(int defenseStat) =>
            Mathf.Max(1, defenseK + Mathf.Max(0, defenseStat) * defensePerStat);

        /// <summary>
        /// 최종 피해량(정수). 감산이 아니라 비율 감소를 쓰기 때문에
        /// 방어력이 높아도 무적이 되지 않고, 웨이브 배율이 곱해져도 방어력이 계속 유효하다.
        /// 나눗셈은 반올림해서 결과가 항상 정수로 떨어진다.
        /// </summary>
        public int Damage(int attackerAttackStat, int defenderDefenseStat) =>
            Mathf.Max(minDamage,
                      DivRound(Attack(attackerAttackStat) * defenseK,
                               DefenseDenominator(defenderDefenseStat)));

        /// <summary>표시용 — 방어력의 피해 감소율(%). 정수.</summary>
        public int DefenseReductionPercent(int defenseStat) =>
            100 - DivRound(defenseK * 100, DefenseDenominator(defenseStat));

        // ------------------------------------------------------------------
        // 정수 유틸 — 배율도 퍼센트(정수)로만 다뤄서 소수점이 끼어들지 않게 한다
        // ------------------------------------------------------------------

        /// <summary>반올림 정수 나눗셈. 분모는 양수여야 한다.</summary>
        public static int DivRound(int numerator, int denominator) =>
            denominator <= 0 ? 0 : (numerator * 2 + denominator) / (denominator * 2);

        /// <summary>value 에 percent(%) 를 곱한 정수. 100 이면 그대로.</summary>
        public static int ScaleByPercent(int value, int percent) =>
            DivRound(value * Mathf.Max(0, percent), 100);

        void OnValidate()
        {
            statMax = Mathf.Max(statMin, statMax);
            initialStatMin = Mathf.Clamp(initialStatMin, statMin, statMax);
            initialStatMax = Mathf.Clamp(initialStatMax, initialStatMin, statMax);
            defenseK = Mathf.Max(1, defenseK);
            defensePerStat = Mathf.Max(1, defensePerStat);
            regenTickSeconds = Mathf.Max(0.1f, regenTickSeconds);
        }
    }
}
