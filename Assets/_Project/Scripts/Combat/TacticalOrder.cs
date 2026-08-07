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
    /// 캐릭터가 유지하려는 전열 위치. <b>집결지(없으면 넥서스 방어 반경)를 기준으로</b>
    /// 넥서스에서 얼마나 먼 쪽에 서는지를 뜻한다 (유저 정의).
    /// </summary>
    public enum TacticalPosition
    {
        /// <summary>전방 — 구역 안에서 넥서스로부터 가장 먼 쪽.</summary>
        Front,

        /// <summary>중위 — 구역 중앙.</summary>
        Mid,

        /// <summary>후방 — 구역 안에서 넥서스에 가장 가까운 쪽.</summary>
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

    /// <summary>웨이브가 없는 동안 무엇을 우선할지.</summary>
    public enum TacticalNonCombat
    {
        /// <summary>중립 몬스터 사냥 — 자원 확보 우선.</summary>
        Hunt,

        /// <summary>탐색 — 전장의 안개 해제 우선(사냥하지 않는다).</summary>
        Explore,

        /// <summary>건물 건설 — 플레이어가 찍어둔 건설 예정지를 <b>맵 어디에 있든</b> 맡아
        /// 짓는다(건설 전담). 지을 곳이 없으면 넥서스 주변을 지키며 대기한다.
        /// 다른 우선 행동을 고른 캐릭터도 가까운 예정지는 도와준다 —
        /// <c>CharacterBehavior.assistBuildRange</c> 참조.</summary>
        Build,
    }

    /// <summary>웨이브가 시작될 때의 반응.</summary>
    public enum TacticalWaveReaction
    {
        /// <summary>우선 행동 중시 — 진행 중인 사냥/탐색을 마친 뒤 합류한다.</summary>
        FinishCurrent,

        /// <summary>즉시 방어 — 웨이브 감지 즉시 하던 일을 놓고 집결지/넥서스로 복귀한다.</summary>
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

        [Tooltip("캐릭터가 기본적으로 유지하려는 위치 (집결지 기준, 넥서스에서 먼 쪽이 전방)")]
        public TacticalPosition position = TacticalPosition.Mid;

        [Tooltip("공격 우선 대상")]
        public TacticalTargetPriority targetPriority = TacticalTargetPriority.Nearest;

        [Tooltip("적을 발견했을 때의 반응")]
        public TacticalAttackReaction attackReaction = TacticalAttackReaction.Chase;

        [Tooltip("비전투(대기시간) 중 우선 행동")]
        public TacticalNonCombat nonCombat = TacticalNonCombat.Hunt;

        [Tooltip("웨이브가 시작될 때의 반응")]
        public TacticalWaveReaction waveReaction = TacticalWaveReaction.DefendNow;

        [Tooltip("체력이 이 % 이하로 떨어지면 후퇴를 시도한다. 0 이면 후퇴하지 않는다")]
        [Range(0, 100)] public int retreatHpPercent = 35;

        /// <summary>다른 지침의 값을 그대로 가져온다 (UI 가 편집 대상 캐릭터를 바꿀 때).</summary>
        public void CopyFrom(TacticalOrder other)
        {
            if (other == null) return;
            attackType = other.attackType;
            position = other.position;
            targetPriority = other.targetPriority;
            attackReaction = other.attackReaction;
            nonCombat = other.nonCombat;
            waveReaction = other.waveReaction;
            retreatHpPercent = other.retreatHpPercent;
        }

        /// <summary>UI 의 "초기화" 버튼 — 코드 기본값으로 되돌린다.</summary>
        public void ResetToDefault() => CopyFrom(new TacticalOrder());

        /// <summary>UI 하단 "현재 지침 요약" 한 문장.</summary>
        public string Summarize()
        {
            string retreat = retreatHpPercent > 0 ? $"체력 {retreatHpPercent}% 이하에서 후퇴" : "후퇴하지 않음";
            return $"{Label(position)} 에서 {Label(attackType)} 공격으로 {Label(targetPriority)}을(를) 노리고, " +
                   $"{Label(attackReaction)}. 비전투 시 {Label(nonCombat)}, 웨이브에는 {Label(waveReaction)}. {retreat}.";
        }

        // ── 표시용 라벨 (UI 와 요약문이 같은 문구를 쓰도록 여기 한 곳에 모아둔다) ──────

        public static string Label(TacticalAttackType v) => v switch
        {
            TacticalAttackType.Ranged => "원거리",
            TacticalAttackType.Magic  => "마법",
            TacticalAttackType.Heal   => "치유",
            _                         => "근거리",
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

        public static string Label(TacticalNonCombat v) => v switch
        {
            TacticalNonCombat.Explore => "탐색",
            TacticalNonCombat.Build   => "건물 건설",
            _                         => "중립 몬스터 사냥",
        };

        public static string Label(TacticalWaveReaction v) =>
            v == TacticalWaveReaction.FinishCurrent ? "우선 행동 중시" : "즉시 방어";
    }
}
