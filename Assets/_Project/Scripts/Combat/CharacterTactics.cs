using UnityEngine;
using LastSanctuary.Units;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 캐릭터 한 명이 들고 다니는 전술 지침. 전술 지침 UI 는 이 컴포넌트만 고치고,
    /// 실제 AI 반영(누구를 어떻게 치고 어디에 서는지)은 여기서 <see cref="UnitCombat"/> 와
    /// <see cref="CharacterBehavior"/> 로 밀어 넣는다.
    ///
    /// <b>왜 서비스가 아니라 유닛에 붙였나</b> — 이 프로젝트는 캐릭터를 씬의
    /// <c>Character_Template</c> 복제로 만든다(진행상황 5절). 지침을 이 컴포넌트에 두면
    /// <b>템플릿의 인스펙터 값이 곧 모든 신규 캐릭터의 기본 지침</b>이 되고, 캐릭터가
    /// 죽거나 새로 생겨도 딕셔너리를 따로 관리할 필요가 없다. 중앙 서비스(딕셔너리)로
    /// 잡으면 파괴된 캐릭터의 항목이 남거나 새 캐릭터가 기본값을 못 받는 문제가 생긴다.
    ///
    /// <b>UI 와의 연결</b>: 패널은 <see cref="UnitSelector"/> 가 고른 캐릭터의 이 컴포넌트를
    /// 찾아 값을 읽고 쓴다. 선택 순서(패널 먼저/로스터 먼저)와 무관하게 항상 "지금 선택된
    /// 캐릭터"를 대상으로 하므로, 두 UI 를 어느 순서로 눌러도 같게 동작한다.
    /// </summary>
    [RequireComponent(typeof(UnitCombat))]
    public class CharacterTactics : MonoBehaviour
    {
        [Header("전술 지침 (템플릿 값 = 신규 캐릭터의 기본 지침)")]
        [SerializeField] TacticalOrder order = new TacticalOrder();

        UnitCombat _combat;
        CharacterBehavior _behavior;

        /// <summary>지금 이 캐릭터의 지침. <b>직접 고친 뒤에는 반드시 <see cref="Apply"/> 를 부를 것.</b></summary>
        public TacticalOrder Order => order;

        /// <summary>지침이 바뀌어 AI 에 반영된 직후 발생. UI 가 표시를 갱신하는 데 쓴다.</summary>
        public static event System.Action<CharacterTactics> OnAnyOrderChanged;

        void Awake()
        {
            _combat = GetComponent<UnitCombat>();
            _behavior = GetComponent<CharacterBehavior>();
        }

        // UnitCombat/CharacterBehavior 의 Start 에서 컴포넌트 참조가 준비되므로
        // 반영은 Start 에서 한 번 한다 (Awake 에 하면 순서에 따라 덮어써질 수 있다).
        void Start() => Apply();

        /// <summary>지금 <see cref="Order"/> 값을 AI 두 컴포넌트에 그대로 밀어 넣는다.</summary>
        public void Apply()
        {
            if (_combat == null) _combat = GetComponent<UnitCombat>();
            if (_behavior == null) _behavior = GetComponent<CharacterBehavior>();

            _combat?.ApplyTactics(order.attackType, order.targetPriority, order.attackReaction);
            _behavior?.ApplyTactics(order.position, order.nonCombat, order.waveReaction,
                                    order.retreatHpPercent);

            OnAnyOrderChanged?.Invoke(this);
        }

        /// <summary>다른 지침을 통째로 덮어쓰고 즉시 반영한다.</summary>
        public void SetOrder(TacticalOrder newOrder)
        {
            order.CopyFrom(newOrder);
            Apply();
        }

        // ── UI 가 항목 하나씩 바꿀 때 쓰는 편의 메서드 ────────────────────────
        // 값이 안 바뀌었으면 Apply 를 건너뛴다 — 같은 버튼을 다시 눌러도 AI 가
        // 목적지를 다시 뽑느라 캐릭터가 흠칫하지 않게 하려는 것.

        public void SetAttackType(TacticalAttackType v)
        {
            if (order.attackType == v) return;
            order.attackType = v; Apply();
        }

        public void SetPosition(TacticalPosition v)
        {
            if (order.position == v) return;
            order.position = v; Apply();
        }

        public void SetTargetPriority(TacticalTargetPriority v)
        {
            if (order.targetPriority == v) return;
            order.targetPriority = v; Apply();
        }

        public void SetAttackReaction(TacticalAttackReaction v)
        {
            if (order.attackReaction == v) return;
            order.attackReaction = v; Apply();
        }

        public void SetNonCombat(TacticalNonCombat v)
        {
            if (order.nonCombat == v) return;
            order.nonCombat = v; Apply();
        }

        public void SetWaveReaction(TacticalWaveReaction v)
        {
            if (order.waveReaction == v) return;
            order.waveReaction = v; Apply();
        }

        public void SetRetreatHpPercent(int percent)
        {
            percent = Mathf.Clamp(percent, 0, 100);
            if (order.retreatHpPercent == percent) return;
            order.retreatHpPercent = percent; Apply();
        }

        /// <summary>UI 의 "초기화" — 코드 기본값으로 되돌린다.</summary>
        public void ResetToDefault()
        {
            order.ResetToDefault();
            Apply();
        }
    }
}
