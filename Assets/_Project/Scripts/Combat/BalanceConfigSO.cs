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

        // ==================================================================
        // ★★ 몬스터 능력치 상한 (2026-08-19 · 40웨이브 확장과 함께 신설)
        //
        // <b>왜 다시 생겼나</b> — 96절이 <c>statMax</c>(100)를 몬스터에서 <b>떼어냈다</b>.
        // 그 상한은 캐릭터 강화용인데 몬스터에까지 걸려 후반 웨이브에서 표가 설계한 곡선이
        // 말없이 평평해졌기 때문이고, 그 판단은 지금도 유효하다.
        //
        // 그런데 상한이 아예 없어지자 유저 피드백대로 <b>후반에 캐릭터가 그냥 녹았다</b>:
        // <i>"몬스터 스탯 상한을 풀었는데 그러니까 후반에 그냥 캐릭터가 녹아버려서 기존처럼
        // 체력배율로만 플러스를 주고 스탯 상한은 어느 정도 조절해야 할듯"</i> (2026-08-19).
        //
        // 원인은 <b>비대칭</b>이다 — 몬스터 능력치는 웨이브 배율로 무한히 오르는데
        // <b>캐릭터는 <see cref="statMax"/> 100 에서 멈춘다</b>(영웅 각성만 그 위를 뚫는다).
        // 방어력이 비율 감소라 무적이 되지 않으므로, 공격력이 계속 오르면 언젠가는
        // 어떤 캐릭터도 두 대를 못 버틴다. 40웨이브까지 늘리면 그 지점이 확실히 온다.
        //
        // ★ 그래서 <b>「체력만 계속 오르고 공격력은 어느 선에서 멈춘다」</b>로 갈랐다
        //   (유저 지시의 "체력배율로만 플러스를 주고" 그대로).
        //   · 체력   → 상한 없음(기본 0). 후반 난이도는 <b>체력 × 마리 수</b>로 만든다.
        //   · 공격 계열 → 상한 있음. 여기서 멈추면 캐릭터가 「버틸 수 있는 상태」로 남는다.
        //   · 방어·재생·명중·치명·저항 → <b>애초에 웨이브 배율을 안 받는다</b>(96-1절 ⚠).
        //     표 값을 그대로 쓰므로 상한을 둘 이유가 없다 — 그래서 칸도 만들지 않았다.
        //
        // 기준으로 삼은 계산 (이 에셋의 계수 · 체력 40+10×스탯 · 타격력 2+2×스탯 ·
        // 피해 배율 50/(50+방어)):
        //
        //   방어 40 / 체력 60 인 캐릭터(체력 640)가 …
        //     보스 공격 120 → 한 대 135  → 4.7대 버팀 (보스 초당 0.77회 = 약 6초)
        //     잡몹 공격  60 → 한 대  68  → 9.4대 버팀
        //   전부 찍은 캐릭터(방어·체력 100 → 체력 1040)가 …
        //     보스 → 한 대 81 → 12.8대 · 잡몹 → 한 대 41 → 25대
        //
        // ⚠ <b>0 은 「무제한」이다</b> — 잘라내지 않는다. 상한을 없애고 싶으면 0 으로 두면
        //   96절 상태로 정확히 돌아간다.
        // ==================================================================

        [Header("★ 몬스터 능력치 상한 (0 = 무제한) — 캐릭터의 statMax 와 별개다")]
        [Tooltip("몬스터 <b>체력</b> 능력치 상한. 기본 <b>0(무제한)</b> — 유저 지시대로 " +
                 "후반 난이도는 체력과 마리 수로 만든다.\n" +
                 "⚠ 여기에 값을 넣으면 후반 웨이브에서 체력 곡선이 평평해진다. " +
                 "웨이브가 너무 길어질 때 쓰는 비상 손잡이로 두고, 평소엔 0 이 맞다.\n" +
                 "참고: 40웨이브(배율 3000%) 기준 잡몹 체력 스탯 210 · 보스 5220")]
        [Min(0)] public int monsterHpStatMax = 0;

        [Tooltip("<b>일반 몬스터</b>의 공격 계열(근거리·원거리·마법·회복) 능력치 상한. " +
                 "기본 <b>60</b>.\n" +
                 "잡몹 원본 공격력이 4~5 라 배율 <b>1200~1500%</b>(대략 13~15웨이브)에서 " +
                 "이 값에 닿고, 그 뒤로는 <b>마리 수</b>가 난이도를 만든다.\n" +
                 "★ 이 칸이 「후반에 캐릭터가 녹는」 문제의 주 손잡이다 — 올리면 " +
                 "잡몹 한 대가 더 아프다")]
        [Min(0)] public int monsterAttackStatMax = 40;

        [Tooltip("<b>보스</b>(MonsterTier.MainBoss)의 공격 계열 능력치 상한. 기본 <b>120</b> — " +
                 "일반의 두 배다.\n" +
                 "<b>왜 따로 두나</b> — 한 값으로 묶으면 후반에 보스와 잡몹의 한 대가 " +
                 "같아져서 «보스가 세다» 는 느낌이 사라진다. 보스 원본 공격력 7~10 이라 " +
                 "배율 1200~1700%(대략 13~18웨이브)에서 닿는다")]
        [Min(0)] public int bossAttackStatMax = 150;

        // ══════════════════════════════════════════════════════════════
        // ★ <b>중립 몬스터 사냥 성장 수치는 여기 없다</b> (2026-08-21).
        //
        //   유저 지시가 *"에딧모드에서 변경 가능하게"* 였으므로 <b>씬 컴포넌트</b>에 뒀다:
        //       Hierarchy ▸ GameSystems ▸ Inspector ▸ <b>Neutral Growth Service</b>
        //   (<see cref="LastSanctuary.Units.NeutralGrowthService"/>)
        //
        //   ⚠ 처음에 이 파일에 칸을 만들었다가 옮겼다 — <b>두 곳에 두지 않는다</b>.
        //     다만 <b>능력치 상한</b>은 여기 남는다(바로 위 세 칸) — 그것은 웨이브 몬스터와
        //     <b>공유</b>하는 밸런스이고, 유저 확정도 *"체력 말고는 상한값 웨이브 몬스터와
        //     동일하게"* 였다.
        // ══════════════════════════════════════════════════════════════


        // ══════════════════════════════════════════════════════════════
        // ★★ 2026-08-20 — 값을 다시 잡았다 (유저 지시)
        //
        //   *"보스는 조금 더 스탯 상한 풀어주는 쪽으로 하고 지금처럼 거의 캐릭터가
        //   녹을정도론 하지말자"*
        //
        //       잡몹  60 → <b>40</b>      보스  120 → <b>150</b>
        //
        //   <b>근거 — 한 대가 캐릭터 최대 체력의 몇 %인가</b>(실측 · 이 파일의 공식 그대로).
        //   기준은 «중간쯤 큰 캐릭터»: 체력 스탯 30(= 최대 체력 340) · 방어력 20.
        //
        //       공격 상한   한 대     최대 체력 대비   몇 대에 죽나
        //          40        59 피해      17%            6 대
        //          60        87 피해      26%            4 대     ← 예전 잡몹 값
        //         120       173 피해      51%            2 대     ← 예전 보스 값
        //         150       216 피해      64%            2 대
        //
        //   ★ <b>잡몹을 60 → 40 으로 내린 이유</b> — 잡몹은 «여러 마리가 동시에» 때린다.
        //     넷이 붙으면 4대 = 한 턴이라 사실상 손쓸 틈이 없다(그것이 «녹는다» 의 정체다).
        //     40 이면 같은 상황에서 여섯 대를 버티므로 후퇴·회복이 실제로 작동한다.
        //     ⚠ <b>후반 난이도가 약해지는 것이 아니다</b> — 체력은 여전히 상한이 없고
        //       마리 수도 그대로다. 「한 대의 크기」만 줄인 것이다.
        //
        //   ★ <b>보스를 120 → 150 으로 올린 이유</b> — 보스는 <b>한 마리</b>다. 크게 때려도
        //     «피할 수 있는 위협» 으로 남고, 잡몹과 한 대가 같아지면 «보스가 세다» 는
        //     느낌이 사라진다(이 상한을 애초에 갈라 둔 이유가 그것이다).
        //
        //   ⚠ 두 값은 <b>배율이 상한에 닿는 웨이브</b>도 같이 옮긴다:
        //       잡몹 원본 공격력 4~5 → 배율 800~1000%(대략 9~11웨이브)에서 40 에 닿는다
        //       보스 원본 공격력 7~10 → 배율 1500~2100%(대략 16~21웨이브)에서 150 에 닿는다
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 능력치에 상한을 씌운다. <paramref name="max"/> 가 <b>0 이면 무제한</b>이라
        /// 값을 그대로 돌려준다 — 96절(상한 철폐) 상태로 되돌릴 수 있는 길을 남긴 것이다.
        /// </summary>
        public static int CapStat(int value, int max) => max > 0 ? Mathf.Min(value, max) : value;

        /// <summary>이 등급의 공격 계열 상한. 보스만 따로 높다.</summary>
        public int AttackStatMaxFor(bool isBoss) => isBoss ? bossAttackStatMax : monsterAttackStatMax;

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

        // ══════════════════════════════════════════════════════════════
        // ★★ 2026-08-20 — 명중·치명 공식을 표에 맞춰 다시 잡았다 (유저 지시:
        //    *"명중률이랑 크리티컬 적용 공식 바꼈으니까 그것도 확인 … 그리고 적용"*)
        //
        //    「능력치 및 공식 정리.xlsx」의 <b>「공식」 시트</b>가 이렇게 바뀌어 있었다:
        //
        //        적중 확률(%)   <b>40 + (명중률 × 0.6)</b>   (100 을 넘으면 100)
        //        치명타 확률(%) 크리티컬 × 0.8              <b>"그냥 상한 없이"</b>
        //
        //    ⚠ <b>「계수」 시트는 아직 옛 값(85 · 0.3 · 상한 60)이 적혀 있다.</b>
        //      두 시트가 어긋나 있고, 손으로 쓴 「공식」 시트가 <b>나중에 고쳐진 쪽</b>이라
        //      그쪽을 정본으로 봤다. 다음에 표를 손볼 때 「계수」 시트도 같이 맞출 것.
        //
        //    <b>무엇이 달라지나</b> — 명중률이 «거의 안 빗나감」에서 «능력치가 실제로 중요한 값»
        //    이 된다:
        //        명중 0    85% → <b>40%</b>
        //        명중 10   88% → <b>46%</b>
        //        명중 50  100% → <b>70%</b>
        //        명중 100 100% → <b>100%</b>   ← 여기서 처음 100 에 닿는다
        //
        //    ★ 치명 상한은 60 → <b>100</b> 으로 뒀다. 표의 "상한 없이" 를 그대로 옮기면
        //      크리 125 에서 100% 를 넘는데, <b>확률에 100% 위가 없다</b> — 필드를
        //      없애는 대신 «닿을 수는 있지만 넘지는 않는» 100 이 그 뜻에 가장 가깝다.
        //      (Range(1,100) 이라 애초에 그 위를 넣을 수도 없다.)
        // ══════════════════════════════════════════════════════════════

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
