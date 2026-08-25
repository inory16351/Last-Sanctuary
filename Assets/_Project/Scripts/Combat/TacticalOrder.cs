using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>캐릭터의 공격 유형. 전술 지침 UI 의 "캐릭터 공격 유형" 4개에 대응한다.</summary>
    public enum TacticalAttackType
    {
        /// <summary>근거리 — 기존 공격 형태를 그대로 유지한다(붙어서 단일 타격).</summary>
        Melee,

        /// <summary>원거리 — 지정 사거리의 히트 스캔 단일 타격(투사체 없이 즉시 적중).</summary>
        Ranged,

        /// <summary>마법 — 최소~최대 사거리 사이의 대상에 정사각 범위 피해. 자기 주변은 못 친다.</summary>
        Magic,

        /// <summary>회복 — 적을 공격하지 않고, 사거리 안의 다친 아군을 공격력 수치만큼 회복시킨다.</summary>
        Heal,
    }

    /// <summary>
    /// 캐릭터가 유지하려는 전열 위치. <b>집결지(없으면 성역 방어 반경)를 기준으로</b>
    /// 성역에서 얼마나 먼 쪽에 서는지를 뜻한다 (유저 정의).
    /// </summary>
    public enum TacticalPosition
    {
        /// <summary>전방 — 구역 안에서 성역으로부터 가장 먼 쪽.</summary>
        Front,

        /// <summary>중위 — 구역 중앙.</summary>
        Mid,

        /// <summary>후방 — 구역 안에서 성역에 가장 가까운 쪽.</summary>
        Back,
    }

    /// <summary>공격 우선 대상 선정 방식.</summary>
    public enum TacticalTargetPriority
    {
        Nearest,     // 가장 가까운 적
        Strongest,   // 가장 강력한 적 (공격력 능력치 기준, 동률이면 가까운 쪽)
        Farthest,    // 가장 먼 적
        Weakest,     // 가장 체력이 적은 적 (현재 체력 기준)
    }

    /// <summary>적을 발견했을 때의 반응.</summary>
    public enum TacticalAttackReaction
    {
        /// <summary>시야(인식 사거리) 안의 적을 쫓아가서 공격한다.</summary>
        Chase,

        /// <summary>적이 사거리 안에 들어올 때까지 자기 위치를 지킨다.</summary>
        HoldGround,
    }

    /// <summary>
    /// <b>후퇴 시 행동</b> — 전방 아군이 물러날 때 나는 어떻게 할지 (유저 지시 2026-08-11).
    ///
    /// 자기 체력이 후퇴 기준 아래로 내려가면 <b>어느 쪽을 골랐든 물러난다</b> —
    /// 이 지침이 정하는 것은 <b>"남이 물러날 때"</b> 뿐이다.
    /// </summary>
    public enum TacticalRetreatAction
    {
        /// <summary>공격 유지 — 앞이 빠져도 자기 자리에서 계속 싸운다.</summary>
        KeepFighting,

        /// <summary>
        /// 동료와 함께 후퇴 — 전방이 물러나면 <b>최대 사거리를 유지하며</b> 같이 물러난다.
        /// 성역까지 도망가는 것이 아니라 적에게서 자기 사거리만큼 떨어진 자리를 계속 잡는다.
        /// </summary>
        FallBackWithAlly,
    }

    /// <summary>
    /// <b>탐험 유형</b> — 전장을 돌아다닐 때 <b>중립 몬스터를 어떻게 대할지</b>
    /// (유저 확정 2026-08-12).
    ///
    /// <b>★ 용어 주의 — "탐험"이 상위 개념이고 "탐색"은 그 안의 한 유형이다</b>(유저 확정):
    /// <code>
    ///   탐험(Expedition) = 맵을 돌아다니는 활동 전체
    ///     ├─ 사냥(Hunt)     중립을 먼저 공격한다
    ///     └─ 탐색(Explore)  안 때리고 맞아도 반격 없이 도망간다
    /// </code>
    /// 두 값 모두 <b>안개를 밝히며 돌아다니는 것은 같다</b> — 다른 것은 중립 몬스터를
    /// 만났을 때뿐이다. 그래서 두 유형을 아우르는 이름이 "탐험"이어야 한다
    /// ("탐색 유형"이라고 부르면 하위 유형 하나와 이름이 겹쳐 헷갈린다).
    /// 코드에서도 상위 개념은 <c>Expedition</c>, 하위 유형은 <c>Explore</c> 로 갈라 쓴다.
    ///
    /// ⚠️ <b>'정찰(Patrol)'이 2026-08-15 에 없어졌다</b>(유저 지시: <i>"정찰 삭제(선공 중립
    /// 몬스터가 없어졌으므로)"</i>). 정찰의 정의는 <b>"먼저 안 때리고, 선공 몹에게 맞으면
    /// 반격한다"</b> 였는데, 같은 날 <b>중립 몬스터가 예외 없이 전부 비선공</b>으로 확정되면서
    /// (<see cref="LastSanctuary.Units.NeutralMonsterDefinitionSO"/> 의 무리 주석 참조)
    /// <b>먼저 맞을 일 자체가 없어졌다</b> — 사냥과 구분되는 지점이 사라져 실질적으로
    /// 탐색과 같은 값이 되었다. 고르는 항목이 셋인데 둘이 같은 행동을 하면 유저가
    /// "무슨 차이지"만 남는다.
    ///
    /// ⚠️ <b>'건물 건설'도 이 목록에 없다.</b> 건설은 지침으로 고르는 것이 아니라
    /// <b>예정지에서 가장 가까운 캐릭터가 맡는 공용 작업</b>이다(유저 확정 2026-08-12) —
    /// 배정은 <c>Buildings.BuildService.AssignedSiteFor</c> 한 곳에서만 정한다.
    /// </summary>
    public enum TacticalExpeditionType
    {
        /// <summary>사냥 — 돌아다니다 중립 몬스터를 마주치면 <b>즉시 공격해 사냥</b>한다.</summary>
        Hunt,

        /// <summary>탐색 — 중립 몬스터를 아예 건드리지 않고 <b>안개만 밝힌다</b>.
        /// 맞아도 <b>반격하지 않고 그 자리를 벗어난다</b>.</summary>
        Explore,
    }

    /// <summary>
    /// <b>탐험 배회 범위</b> — 탐험(<see cref="TacticalExpeditionType"/> 세 유형 전부)을 할 때
    /// <b>성역을 중심으로 얼마나 멀리까지 나갈 수 있는지</b> (유저 지시 2026-08-14).
    ///
    /// <b>왜 필요했나</b> — 탐험 AI 는 "아직 안 밝혀진 칸"을 최우선으로 고르는데, 그 후보를
    /// 캐릭터 위치 기준 최대 <c>scoutSearchRadius</c>(60타일)까지 훑는다. 그래서
    /// <b>게임 초반부터 맵 저 끝까지 걸어 나가</b> 강한 중립 몬스터와 마주치는 일이 잦았고,
    /// 반대로 <b>맵을 다 밝히고 나면</b> 고를 후보가 없어 방어로 떨어져 아무것도 안 했다.
    /// 이 지침은 그 두 문제를 같은 값 하나로 잡는다 — 범위 안이면 안개를 밝히고,
    /// 다 밝혔으면 그 범위 안에서 계속 돌아다닌다(<c>CharacterBehavior.PickExpeditionSpot</c>).
    ///
    /// ★ <b>값은 지름이다</b>(유저 확정 2026-08-14) — 중립 몬스터 등장 범위(73-5절)와 같은
    /// 규칙이다. 실제 판정에 쓰는 반지름은 코드가 절반으로 나눠 쓴다.
    /// 지름 자체는 <c>CharacterBehavior</c> 인스펙터
    /// (<c>roamDiameterNear</c> · <c>roamDiameterMid</c>)에 있다 —
    /// <b>화면에는 타일 값을 노출하지 않는다</b>(유저 지시: "명확한 타일 값을 유저에게
    /// 전달하지 말고"). UI 는 <see cref="TacticalOrder.Label(TacticalRoamRange)"/> 의
    /// 분위기 있는 이름만 보여준다.
    ///
    /// ⚠ <b>협동 탐험이 켜진 부대에서는 한 명만 눌러도 부대원 전원이 같이 바뀐다</b>
    /// (유저 확정) — 전파는 <c>CharacterTactics.SetRoamRange</c> 한 곳에서만 한다.
    /// </summary>
    public enum TacticalRoamRange
    {
        /// <summary>근방 — 성역 바로 주변만 돈다. 초반 안전 운용. <b>기본값</b>.</summary>
        Near,

        /// <summary>외곽 — 성역 바깥 지대까지 나간다.</summary>
        Mid,

        /// <summary>전역 — 제한 없이 맵 끝까지 나간다(이 지침이 생기기 전의 동작).</summary>
        Far,
    }

    /// <summary>웨이브가 시작될 때의 반응.</summary>
    public enum TacticalWaveReaction
    {
        /// <summary>
        /// 탐험 우선 — 웨이브가 와도 <b>탐험과 건설을 계속</b>한다.
        /// 여기서 말하는 탐험은 <see cref="TacticalExpeditionType"/> 세 유형 전부다 —
        /// 사냥이든 정찰이든 탐색이든 자기 유형대로 계속 돌아다닌다.
        ///
        /// ⚠️ <b>집결지를 무시한다</b> — 같은 부대에 집결지가 잡혀 있어도 가지 않는다
        /// (유저 확정 2026-08-12). 방어는 다른 캐릭터에게 맡기고 맵을 계속 밝히는 역할.
        /// </summary>
        KeepExploring,

        /// <summary>즉시 방어 — 웨이브 감지 즉시 하던 일을 놓고 <b>집결지</b>(없으면 성역)로 이동한다.</summary>
        DefendNow,
    }

    /// <summary>
    /// 캐릭터 한 명의 전술 지침 한 벌. 전술 지침 UI(<c>TacticalOrderPanel</c>)가 이 값을 고치고,
    /// <see cref="CharacterTactics"/> 가 <see cref="UnitCombat"/> · <c>CharacterBehavior</c> 에 반영한다.
    ///
    /// <b>왜 클래스인가</b> — 캐릭터마다 한 벌씩 들고 다녀야 하고(구조체면 복사본을 고치게 된다),
    /// 인스펙터에서 <see cref="CharacterTactics"/> 안에 접혀 보이도록 <c>[System.Serializable]</c> 로 둔다.
    /// 템플릿(<c>Character_Template</c>)에 붙여 두면 새로 생성되는 캐릭터가 그 기본값을 물려받는다
    /// (진행상황 5절의 템플릿 복제 패턴).
    /// </summary>
    [System.Serializable]
    public class TacticalOrder
    {
        [Tooltip("캐릭터 공격 유형")]
        public TacticalAttackType attackType = TacticalAttackType.Melee;

        [Tooltip("캐릭터가 기본적으로 유지하려는 위치 (집결지 기준, 성역에서 먼 쪽이 전방)")]
        public TacticalPosition position = TacticalPosition.Mid;

        [Tooltip("공격 우선 대상")]
        public TacticalTargetPriority targetPriority = TacticalTargetPriority.Nearest;

        [Tooltip("적을 발견했을 때의 반응")]
        public TacticalAttackReaction attackReaction = TacticalAttackReaction.Chase;

        [Tooltip("탐험 유형 — 돌아다니며 중립 몬스터를 어떻게 대할지 (사냥 / 정찰 / 탐색)")]
        public TacticalExpeditionType expeditionType = TacticalExpeditionType.Hunt;

        [Tooltip("탐험 배회 범위 — 성역 중심으로 얼마나 멀리까지 나갈지 (근방 / 외곽 / 전역).\n" +
                 "실제 지름(타일)은 CharacterBehavior 인스펙터의 roamDiameterNear/Mid 에 있다")]
        public TacticalRoamRange roamRange = TacticalRoamRange.Near;

        [Tooltip("웨이브가 시작될 때의 반응")]
        public TacticalWaveReaction waveReaction = TacticalWaveReaction.DefendNow;

        [Tooltip("체력이 이 % 이하로 떨어지면 후퇴를 시도한다. 0 이면 후퇴하지 않는다")]
        [Range(0, 100)] public int retreatHpPercent = 35;

        [Tooltip("전방 아군이 물러날 때 나는 어떻게 할지. " +
                 "공격 위치가 '전방'이면 따라 물러날 대상이 없으므로 '공격 유지'로 고정된다")]
        public TacticalRetreatAction retreatAction = TacticalRetreatAction.KeepFighting;

        /// <summary>다른 지침의 값을 그대로 가져온다 (UI 가 편집 대상 캐릭터를 바꿀 때).</summary>
        public void CopyFrom(TacticalOrder other)
        {
            if (other == null) return;
            attackType = other.attackType;
            position = other.position;
            targetPriority = other.targetPriority;
            attackReaction = other.attackReaction;
            expeditionType = other.expeditionType;
            roamRange = other.roamRange;
            waveReaction = other.waveReaction;
            retreatHpPercent = other.retreatHpPercent;
            retreatAction = other.retreatAction;
            Normalize();
        }

        /// <summary>
        /// 서로 모순되는 조합을 바로잡는다. <b>지금은 한 가지</b> —
        /// <b>공격 위치가 전방이면 '동료와 함께 후퇴'를 고를 수 없다</b>(유저 확정 2026-08-11).
        /// 전방보다 앞에 선 아군이 없으니 따라 물러날 대상 자체가 없다.
        ///
        /// 값을 넣는 모든 경로(UI · CopyFrom · 인스펙터 직접 수정)가 이 한 곳을 지나게 해서
        /// "UI 에서는 막았는데 다른 경로로는 들어가는" 구멍을 없앤다.
        /// </summary>
        public void Normalize()
        {
            if (position == TacticalPosition.Front)
                retreatAction = TacticalRetreatAction.KeepFighting;

            // ⚠ 2026-08-15 에 <c>Patrol</c> 이 없어지면서 <b>enum 번호가 하나씩 당겨졌다</b>
            //   (예전: Hunt 0 · Patrol 1 · Explore 2 → 지금: Hunt 0 · Explore 1).
            //   씬에 저장된 값은 0 하나뿐이라 실제 영향은 없지만, 인스펙터에서 손으로
            //   2 를 넣어둔 사본이 있으면 <b>어느 쪽도 아닌 값</b>이 된다.
            //   모르는 값은 전부 탐색으로 본다 — 없어진 정찰이 사실상 탐색과 같은 행동이
            //   됐기 때문이고(맨 위 enum 주석), 사냥으로 떨어뜨리면 지침을 안 준
            //   캐릭터가 갑자기 중립을 때리러 간다.
            if (expeditionType != TacticalExpeditionType.Hunt &&
                expeditionType != TacticalExpeditionType.Explore)
                expeditionType = TacticalExpeditionType.Explore;
        }

        /// <summary>이 지침에서 '동료와 함께 후퇴'를 고를 수 있는지 (UI 가 버튼을 잠글 때 쓴다).</summary>
        public bool CanFallBackWithAlly => position != TacticalPosition.Front;

        /// <summary>UI 의 "초기화" 버튼 — 코드 기본값으로 되돌린다.</summary>
        public void ResetToDefault() => CopyFrom(new TacticalOrder());

        /// <summary>UI 하단 "현재 지침 요약" 한 문장.</summary>
        public string Summarize()
        {
            string retreat = retreatHpPercent > 0 ? $"체력 {retreatHpPercent}% 이하에서 후퇴" : "후퇴하지 않음";
            return $"{Label(position)} 에서 {Label(attackType)} 공격으로 {Label(targetPriority)}을(를) 노리고, " +
                   $"{Label(attackReaction)}. 탐험 유형은 {Label(expeditionType)}({Label(roamRange)}), " +
                   $"웨이브에는 {Label(waveReaction)}. {retreat}, 앞이 빠지면 {Label(retreatAction)}.";
        }

        // ── 표시용 라벨 (UI 와 요약문이 같은 문구를 쓰도록 여기 한 곳에 모아둔다) ──────

        public static string Label(TacticalAttackType v) => v switch
        {
            TacticalAttackType.Ranged => "원거리",
            TacticalAttackType.Magic  => "마법",
            TacticalAttackType.Heal   => "치유",
            _                         => "근거리",
        };

        public static string Label(TacticalRetreatAction v) => v switch
        {
            TacticalRetreatAction.FallBackWithAlly => "동료와 함께 후퇴",
            _                                      => "공격 유지",
        };

        public static string Label(TacticalPosition v) => v switch
        {
            TacticalPosition.Front => "전방",
            TacticalPosition.Back  => "후방",
            _                      => "중위",
        };

        public static string Label(TacticalTargetPriority v) => v switch
        {
            TacticalTargetPriority.Strongest => "가장 강력한 적",
            TacticalTargetPriority.Farthest  => "가장 먼 적",
            TacticalTargetPriority.Weakest   => "가장 체력이 적은 적",
            _                                => "가장 가까운 적",
        };

        public static string Label(TacticalAttackReaction v) =>
            v == TacticalAttackReaction.HoldGround ? "사거리에 들어올 때까지 대기" : "시야 내의 적을 쫓아가 공격";

        public static string Label(TacticalExpeditionType v) => v switch
        {
            TacticalExpeditionType.Explore => "탐색",
            _                              => "사냥",
        };

        /// <summary>
        /// 탐험 유형 하나를 고르면 무슨 일이 벌어지는지 자세히 설명하는 여러 문장짜리 문구
        /// (유저 지시 2026-08-13: "탐색과 정찰에 대한 기능을 디테일하게 설명칸에 기입"). UI
        /// (<c>TacticalOrderPanel</c>) 가 탐험 유형 버튼을 누를 때마다 그 유형의 설명을
        /// 이 문구로 갈아 끼운다 — 이 세 값은 서로 완전히 다른 행동이라, 버튼 색만으로는
        /// "맞으면 반격하는지" · "도망가는지" 같은 차이가 안 보인다.
        /// </summary>
        public static string Description(TacticalExpeditionType v) => v switch
        {
            TacticalExpeditionType.Explore =>
                "탐색 — 탐험 중 중립 몬스터를 절대 공격하지 않고 안개만 밝히며 돌아다닙니다. " +
                "중립 몬스터에게 공격당해도 반격 없이 그 자리를 벗어나 도망칩니다(웨이브 " +
                "몬스터에게 맞을 때는 평소처럼 맞서 싸웁니다). 전투를 최대한 피해 안전하게 " +
                "시야를 넓히고 싶을 때 적합합니다.",

            _ =>
                "사냥 — 탐험 중 중립 몬스터를 발견하면 먼저 다가가 공격해 사냥합니다. 처치하면 " +
                "에너지를 얻지만, 사냥에 나서는 동안 부대에서 떨어지거나 강한 개체와 마주쳐 " +
                "위험해질 수 있습니다.",
        };

        /// <summary>
        /// 탐험 배회 범위의 짧은 이름. <b>타일 수치를 절대 넣지 않는다</b>(유저 지시
        /// 2026-08-14: "명확한 타일 값을 유저에게 전달하지 말고") — 분위기로만 거리를 전한다.
        /// 버튼 라벨 칸이 71px(폰트 16 = 두 글자)이라 두 글자로 맞췄고, 자세한 뜻은
        /// 버튼 아래 힌트칸(<c>Col3/RoamHint</c>)이 <see cref="Description"/> 로 보여준다.
        /// </summary>
        public static string Label(TacticalRoamRange v) => v switch
        {
            TacticalRoamRange.Mid => "외곽",
            TacticalRoamRange.Far => "전역",
            _                     => "근방",
        };

        /// <summary>
        /// 탐험 배회 범위를 고르면 무슨 일이 벌어지는지 한두 줄로 설명하는 문구.
        /// 전술 지침 창의 <c>Col3/RoamHint</c> 가 버튼을 누를 때마다 이 문구로 갈아 끼운다.
        /// </summary>
        public static string Description(TacticalRoamRange v) => v switch
        {
            TacticalRoamRange.Mid =>
                "성역 바깥 지대까지 나가 탐험합니다. 밝힐 곳이 더 많지만 그만큼 위험합니다.",

            TacticalRoamRange.Far =>
                "제한 없이 맵 끝까지 나갑니다. 미지의 존재와 마주칠 각오가 필요합니다.",

            _ =>
                "성역 가까운 곳만 돕니다. 위험은 적지만 넓은 지역을 밝히지 못합니다.",
        };

        public static string Label(TacticalWaveReaction v) =>
            v == TacticalWaveReaction.KeepExploring ? "탐험 우선" : "즉시 방어";
    }
}
