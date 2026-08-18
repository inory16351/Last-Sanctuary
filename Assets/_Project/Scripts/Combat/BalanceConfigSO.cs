using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 능력치 치환 공식과 전투 상수. 계수를 코드에 박지 않고 전부 여기에 두어
    /// 에디터에서 조정하면서 밸런싱할 수 있게 한다.
    ///
    /// <b>계산은 실수로, 적용은 반올림 정수로</b> (유저 확정 2026-08-11 — 이전의 "정수 유지 법칙" 폐기).
    /// <list type="bullet">
    /// <item><b>계수는 소수점을 써도 된다</b> — 예: 공격 계수 1.8, 방어 계수 0.5.
    ///       밸런싱할 때 정수로만 움직이면 조정 폭이 너무 거칠었다.</item>
    /// <item><b>결과가 개수인 것은 반올림해서 정수로 적용한다</b> — 체력 · 피해량 · 회복량.
    ///       체력이 87.3 인 것은 의미가 없고 표시도 지저분하다.</item>
    /// <item><b>결과가 비율·속도인 것은 실수 그대로 쓴다</b> — 공격 속도(회/초) · 이동 속도(타일/초) ·
    ///       적중·치명 확률(%) · 침식 배율. 여기서 정수로 깎으면 오히려 조정이 불가능해진다
    ///       (공속 0.85 를 못 쓰게 된다).</item>
    /// </list>
    ///
    /// <b>왜 바꿨나</b> — 예전에는 계수까지 전부 <c>int</c> 로 두고 <c>DivRound</c> 같은
    /// 정수 나눗셈만 썼다. 소수점을 없애려는 목적이었지만, 대가가 컸다:
    /// 공격 속도를 0.1 단위로 조정하려고 <c>attackSpeedBaseTenths</c> 처럼 "10배 정수" 필드를
    /// 만들어야 했고(읽는 사람이 매번 10으로 나눠 생각해야 했다), 계수를 1 → 2 로만 움직일 수 있어
    /// 밸런스 조정이 두 배씩 튀었다. 이제 실수로 계산하고 <b>마지막에 한 번만</b> 반올림한다.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/Combat/Balance Config", fileName = "BalanceConfig")]
    public class BalanceConfigSO : ScriptableObject
    {
        [Header("능력치 척도 (능력치 자체는 정수)")]
        [Tooltip("능력치의 최소값. 유저에게 보이는 값과 동일한 척도")]
        [Min(1)] public int statMin = 1;

        [Tooltip("능력치의 최대값. ★ <b>캐릭터 전용</b>이다 (2026-08-18) — " +
                 "강화(CharacterUpgradeService.Grow)가 여기서 잘리고, 영웅 각성만 그 위를 뚫는다. " +
                 "⚠ <b>몬스터에는 걸리지 않는다.</b> 예전에는 MonsterDefinitionSO.BuildStats 가 " +
                 "이 값으로 몬스터 능력치까지 잘랐는데, 그 때문에 후반 웨이브에서 표가 설계한 " +
                 "곡선이 말없이 평평해지고 보스 체력을 배율(hp_percent)로 우회해야 했다")]
        [Min(1)] public int statMax = 100;

        [Header("생성 시 랜덤 범위")]
        [Tooltip("캐릭터 생성 시 각 능력치가 이 범위에서 균등 랜덤으로 결정된다. " +
                 "캐릭터 테이블에 정의된 인물은 이 롤을 쓰지 않고 고정값을 받는다")]
        [Min(1)] public int initialStatMin = 1;
        [Min(1)] public int initialStatMax = 10;

        [Header("체력  =  기본 + 체력 × 계수      → 반올림 정수")]
        [Tooltip("체력 능력치 0 일 때의 최대 체력")]
        public float hpBase = 40f;

        [Tooltip("체력 1당 최대 체력 증가량. 10 이면 체력 2 → 60, 체력 8 → 120")]
        [Min(0.01f)] public float hpPerStat = 10f;

        [Header("타격력  =  기본 + 공격력 × 계수      (내부는 실수, 표시는 반올림)")]
        [Tooltip("공격 능력치 0 일 때의 타격력")]
        public float attackBase = 2f;

        [Tooltip("공격력 1당 타격력 증가량")]
        [Min(0.01f)] public float attackPerStat = 2f;

        [Header("방어력  →  피해 배율 = K ÷ (K + 방어력 × 계수)")]
        [Tooltip("작을수록 방어력의 효율이 높아진다. 50 이면 방어력 50 에서 정확히 절반 감소")]
        [Min(1f)] public float defenseK = 50f;

        [Tooltip("방어력 1당 분모 증가량")]
        [Min(0.01f)] public float defensePerStat = 1f;

        [Header("피해 공통")]
        [Tooltip("방어력이 아무리 높아도 이 값은 관통한다. 무적 방지")]
        [Min(0)] public int minDamage = 1;

        [Header("체력 재생  =  회복력 × 계수  (틱마다)      → 반올림 정수")]
        [Tooltip("회복 틱 간격(초). 10 이면 회복력 6 인 유닛이 10초마다 6 회복")]
        [Min(0.1f)] public float regenTickSeconds = 10f;

        [Min(0f)] public float regenPerStat = 1f;

        [Tooltip("전투(공격했거나 피해를 입은 상황)에서 벗어난 뒤 재생이 시작되기까지 기다리는 시간(초). " +
                 "0 이면 전투 중에도 재생된다")]
        [Min(0f)] public float outOfCombatRegenDelay = 5f;

        [Header("적중 확률(%)  =  기본 + 명중률 × 계수      (실수 유지 — 확률)")]
        [Range(0f, 100f)] public float accuracyBasePercent = 80f;
        [Min(0f)] public float accuracyPerStat = 1f;
        [Range(1f, 100f)] public float accuracyMaxPercent = 100f;

        [Header("치명타 확률(%)  =  크리티컬 × 계수      (실수 유지 — 확률)")]
        [Min(0f)] public float criticalPerStat = 1f;
        [Range(1f, 100f)] public float criticalMaxPercent = 100f;

        [Tooltip("치명타 시 피해 배율. 1.5 = 1.5배. 캐릭터별 능력치가 아니라 전역 상수 — " +
                 "캐릭터 테이블에 치명피해 컬럼이 없기 때문")]
        [Min(1f)] public float criticalDamageMultiplier = 1.5f;

        [Header("초당 공격 횟수  =  기본 + (한계 − 기본) × 공속 ÷ (공속 + 반감점)   (실수 유지 — 속도)")]
        [Tooltip("공속 능력치 0 일 때 초당 공격 횟수")]
        [Min(0.05f)] public float attacksPerSecondBase = 0.6f;

        [Tooltip("이 능력치에서 기본과 한계의 정확히 중간이 된다. " +
                 "작을수록 초반에 빨리 오르고 뒤가 완만해진다")]
        [Min(0.01f)] public float attacksPerSecondHalfStat = 50f;

        [Tooltip("점근 한계 — 능력치를 무한히 올려도 이 값에 닿지 않는다. " +
                 "예전처럼 잘라내는 상한이 아니다")]
        [Min(0.05f)] public float attacksPerSecondMax = 3.6f;

        [Header("초당 이동 타일  =  기본 + (한계 − 기본) × 이속 ÷ (이속 + 반감점)   (실수 유지 — 속도)")]
        [Tooltip("이속 능력치 0 일 때 초당 이동 타일. 웨이브 몬스터는 2.2타일/초")]
        [Min(0.1f)] public float moveSpeedBase = 2.1f;

        [Tooltip("이 능력치에서 기본과 한계의 정확히 중간이 된다")]
        [Min(0.01f)] public float moveSpeedHalfStat = 50f;

        [Tooltip("점근 한계 — 닿지 않는다")]
        [Min(0.1f)] public float moveSpeedMax = 6f;

        [Header("저항력  →  침식 배율      (실수 유지 — 배율)")]
        [Tooltip("이 저항력이 배율 1.0(=변화 없음)의 기준점이다. " +
                 "기준보다 낮으면 침식이 빨리 쌓이고 늦게 빠진다 (유저 확정 2026-08-11)")]
        [Range(1f, 100f)] public float resistancePivot = 50f;

        [Tooltip("기준점에서 1 벗어날 때마다 배율이 이만큼 움직인다. " +
                 "0.01 이면 저항력 13 → 상승 1.37배 / 회복 0.63배")]
        [Min(0f)] public float resistancePerStat = 0.01f;

        [Header("프로토타입 고정 상수 (능력치로 분리되지 않은 값)")]
        [Tooltip("공속 능력치가 없는 유닛(몬스터·포탑)의 폴백 초당 공격 횟수")]
        [Min(0.05f)] public float attacksPerSecond = 1f;

        [Tooltip("이속 능력치가 없는 유닛의 폴백 초당 이동 타일")]
        [Min(0f)] public float moveSpeedTilesPerSecond = 3f;

        // ==================================================================
        // 치환 공식
        //
        // 규칙: 계산은 실수로 하고, 결과가 "개수"인 것만 마지막에 반올림한다.
        //       Mathf.RoundToInt 는 .5 를 짝수로 보내는 은행가 반올림이 아니라
        //       일반 반올림에 가깝게 동작한다(0.5 → 1, 1.5 → 2, 2.5 → 3 은 아니고 2).
        //       ⚠ .5 경계가 밸런스에 중요한 값은 계수를 조정해 경계를 피하는 편이 낫다.
        // ==================================================================

        /// <summary>능력치 → 최대 체력. <b>반올림 정수</b> (체력은 개수).</summary>
        public int MaxHp(int hpStat) => Mathf.Max(1, Mathf.RoundToInt(hpBase + hpStat * hpPerStat));

        /// <summary>
        /// 능력치 → 타격력(<b>실수</b>). 피해 계산의 중간값이라 여기서는 반올림하지 않는다 —
        /// 반올림을 두 번 하면(타격력에서 한 번, 피해량에서 한 번) 오차가 쌓인다.
        /// </summary>
        public float AttackPower(int attackStat) => attackBase + attackStat * attackPerStat;

        /// <summary>능력치 → 타격력(표시용 <b>반올림 정수</b>).</summary>
        public int Attack(int attackStat) => Mathf.RoundToInt(AttackPower(attackStat));

        /// <summary>
        /// 방어력 → <b>받는 피해 배율</b>(0~1 실수). K ÷ (K + 방어력 × 계수).
        /// 방어력 0 이면 1.0(그대로), 방어력 50 이면 0.5(절반), 방어력 100 이면 0.333.
        /// </summary>
        public float DamageMultiplier(int defenseStat) =>
            defenseK / (defenseK + Mathf.Max(0, defenseStat) * defensePerStat);

        /// <summary>
        /// 최종 피해량. <b>반올림 정수</b> (체력을 깎는 값이라 개수).
        /// 감산이 아니라 비율 감소를 쓰기 때문에 방어력이 높아도 무적이 되지 않고,
        /// 웨이브 배율이 곱해져도 방어력이 계속 유효하다.
        /// </summary>
        public int Damage(int attackerAttackStat, int defenderDefenseStat) =>
            Mathf.Max(minDamage,
                      Mathf.RoundToInt(AttackPower(attackerAttackStat) *
                                       DamageMultiplier(defenderDefenseStat)));

        /// <summary>표시용 — 방어력의 피해 감소율(%). 반올림 정수.</summary>
        public int DefenseReductionPercent(int defenseStat) =>
            Mathf.RoundToInt((1f - DamageMultiplier(defenseStat)) * 100f);

        /// <summary>능력치 → 회복 틱 1회당 회복량. <b>반올림 정수</b> (체력은 개수).</summary>
        public int RegenPerTick(int regenStat) =>
            Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, regenStat) * regenPerStat));

        /// <summary>표시용 — 초당 회복량(실수). 실제 회복은 틱 단위 정수로 들어간다.</summary>
        public float RegenPerSecond(int regenStat) =>
            regenTickSeconds > 0f ? RegenPerTick(regenStat) / regenTickSeconds : 0f;

        /// <summary>능력치 → 적중 확률(%). <b>실수</b> — 확률은 0.5% 단위 조정이 필요하다.</summary>
        public float HitChancePercent(int accuracyStat) =>
            Mathf.Clamp(accuracyBasePercent + Mathf.Max(0, accuracyStat) * accuracyPerStat,
                        0f, accuracyMaxPercent);

        /// <summary>능력치 → 치명타 확률(%). <b>실수</b>.</summary>
        public float CriticalChancePercent(int criticalStat) =>
            Mathf.Clamp(Mathf.Max(0, criticalStat) * criticalPerStat, 0f, criticalMaxPercent);

        /// <summary>치명타가 터졌을 때의 피해. <b>반올림 정수</b>.</summary>
        public int ApplyCriticalDamage(int damage) =>
            Mathf.RoundToInt(damage * criticalDamageMultiplier);

        /// <summary>능력치 → 초당 공격 횟수. <b>실수 유지</b> (0.85회/초 같은 값이 필요하다).</summary>
        public float AttacksPerSecondOf(int attackSpeedStat) =>
            SpeedCurve(attacksPerSecondBase, attacksPerSecondMax,
                       attacksPerSecondHalfStat, attackSpeedStat);

        /// <summary>능력치 → 초당 이동 타일 수. <b>실수 유지</b>.</summary>
        public float MoveSpeedTilesOf(int moveSpeedStat) =>
            SpeedCurve(moveSpeedBase, moveSpeedMax, moveSpeedHalfStat, moveSpeedStat);

        /// <summary>
        /// 공속·이속 공통 곡선. <c>기본 + (한계 − 기본) × 능력치 ÷ (능력치 + 반감점)</c>.
        ///
        /// <b>왜 하드 상한을 버렸나 (2026-08-11 개정, 유저 지시)</b> — 예전 식은
        /// <c>기본 + 능력치 × 계수</c> 를 상한에서 <c>Min</c> 으로 잘라냈다. 그래서
        /// <b>공속 능력치 40 · 이속 36 을 넘기면 능력치가 아무 일도 하지 않았다</b> —
        /// 능력치 상한이 100 인데 그 절반도 못 쓰고 죽는 값이었고, 강화를 13회쯤 하면
        /// 두 능력치에 투자하는 것이 완전히 헛수고가 됐다.
        ///
        /// 이 곡선은 <b>상한에 도달하지 않으면서 계속 증가</b>한다 — 능력치 100 이든 200 이든
        /// 올린 만큼 반영되고, 그러면서도 한계값을 넘지 못하므로 "능력치 100 에서 초당 10회"
        /// 같은 붕괴가 나지 않는다. 상한이 필요하다는 기존 판단과 100 초과도 반영돼야 한다는
        /// 요구를 동시에 만족시키는 형태가 이것뿐이다.
        ///
        /// 반감점은 "이 능력치에서 기본과 한계의 정확히 중간"이라는 뜻이다(50 → 능력치 50).
        /// 계수를 역산해 기존 캐릭터 값이 거의 안 바뀌게 잡았다
        /// (엘린 공속 0.78 → 0.77 / 이속 2.82 → 2.69).
        /// </summary>
        static float SpeedCurve(float baseValue, float limit, float halfStat, int stat)
        {
            float s = Mathf.Max(0, stat);
            float span = Mathf.Max(0f, limit - baseValue);
            return baseValue + span * s / (s + Mathf.Max(0.01f, halfStat));
        }

        /// <summary>
        /// 저항력 → 침식 <b>상승</b> 배율(실수). 기준점(50)에서 1.0.
        /// 기준보다 낮으면 1.0 을 넘어 빨리 쌓이고, 높으면 1.0 아래로 천천히 쌓인다.
        /// 저항 13 → 1.37배, 저항 100 → 0.5배.
        /// </summary>
        public float ErosionGainMultiplier(int resistanceStat) =>
            Mathf.Max(0.01f, 1f + (resistancePivot - Mathf.Max(0, resistanceStat)) * resistancePerStat);

        /// <summary>
        /// 저항력 → 침식 <b>회복</b> 배율(실수). 상승 배율과 정확히 대칭이다.
        /// 저항 13 → 0.63배(느리게), 저항 100 → 1.5배(빠르게).
        /// </summary>
        public float ErosionRecoverMultiplier(int resistanceStat) =>
            Mathf.Max(0.01f, 1f - (resistancePivot - Mathf.Max(0, resistanceStat)) * resistancePerStat);

        // ------------------------------------------------------------------
        // 유틸
        // ------------------------------------------------------------------

        /// <summary>
        /// value 에 percent(%) 를 곱한 <b>반올림 정수</b>. 100 이면 그대로.
        /// 몬스터의 체력 보정(<c>hpPercent</c>)·광폭화 배율·웨이브 배율이 이걸 쓴다 —
        /// 그 값들은 개수(체력·능력치)라 정수로 떨어져야 한다.
        /// </summary>
        public static int ScaleByPercent(int value, int percent) =>
            Mathf.RoundToInt(value * Mathf.Max(0, percent) / 100f);

        void OnValidate()
        {
            statMax = Mathf.Max(statMin, statMax);
            initialStatMin = Mathf.Clamp(initialStatMin, statMin, statMax);
            initialStatMax = Mathf.Clamp(initialStatMax, initialStatMin, statMax);
            defenseK = Mathf.Max(1f, defenseK);
            defensePerStat = Mathf.Max(0.01f, defensePerStat);
            regenTickSeconds = Mathf.Max(0.1f, regenTickSeconds);
            attacksPerSecondMax = Mathf.Max(attacksPerSecondBase, attacksPerSecondMax);
            moveSpeedMax = Mathf.Max(moveSpeedBase, moveSpeedMax);
            attacksPerSecondHalfStat = Mathf.Max(0.01f, attacksPerSecondHalfStat);
            moveSpeedHalfStat = Mathf.Max(0.01f, moveSpeedHalfStat);
        }
    }
}
