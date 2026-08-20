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

            // 모순된 조합(전방 + 동료와 함께 후퇴)을 여기서 한 번 걸러 낸다 —
            // 인스펙터에서 직접 고친 경우까지 이 경로를 지난다.
            order.Normalize();

            _combat?.ApplyTactics(order.attackType, order.targetPriority, order.attackReaction);
            _behavior?.ApplyTactics(order.position, order.expeditionType, order.roamRange,
                                    order.waveReaction, order.retreatHpPercent, order.retreatAction);

            OnAnyOrderChanged?.Invoke(this);
        }

        /// <summary>다른 지침을 통째로 덮어쓰고 즉시 반영한다.</summary>
        public void SetOrder(TacticalOrder newOrder)
        {
            order.CopyFrom(newOrder);
            ForceAllLocks();
            Apply();
        }

        // ------------------------------------------------------------------
        // 역할 잠금 — 「선봉장」(히스톤 80013) (2026-08-15)
        //
        // 정의문이 <b>"포지션은 전방 / 공격 유형은 근거리로 <u>고정</u>된다"</b> 이므로,
        // 에셋의 <c>attackPreset·positionPreset</c> 으로 <b>시작값</b>만 맞추는 것으로는 부족하다
        // (그건 태어날 때 한 번 정해줄 뿐, 유저가 전술 창에서 바로 바꿀 수 있다).
        //
        // 잠금은 <b>두 겹</b>이다 — UI 가 버튼을 잠그고, 여기서도 값을 거부한다.
        // <see cref="SetRetreatAction"/> 이 '전방 + 동료와 함께 후퇴'를 막는 것과 같은 이유이자
        // 같은 모양이다: "UI 가 버튼을 잠그지만, 여기서도 막아 다른 경로로 새어 들어오지 않게 한다".
        //
        // ⚠ 잠금은 <b>공격 유형·포지션 두 칸에만</b> 걸린다. 나머지 지침(교전 대상·탐험 유형·
        //   웨이브 반응·후퇴 기준)은 정의문이 언급하지 않으므로 그대로 유저가 고른다.
        // ------------------------------------------------------------------

        /// <summary>공격 유형·포지션이 패시브로 잠겨 있는가. <see cref="CharacterPassives"/> 가 켠다.</summary>
        public bool RoleLocked { get; private set; }

        /// <summary>
        /// 역할 잠금을 걸거나 푼다. 거는 순간 <b>잠긴 값으로 즉시 스냅</b>한다 —
        /// 스킬이 늦게 해금돼도(강화로 슬롯이 열리는 경우) 그 시점부터 정의문대로 맞춰진다.
        /// </summary>
        public void SetRoleLock(bool locked)
        {
            if (RoleLocked == locked) return;
            RoleLocked = locked;
            if (!locked) return;

            if (ForceLockedRole()) Apply();
        }

        // ------------------------------------------------------------------
        // 후퇴 기준 잠금 — 「가학증」(시그리드 80016) (2026-08-20)
        //
        // 정의문 마지막 문장: <b>"시그리드의 후퇴기준이 {Value_05}%로 고정됩니다."</b>
        //
        // ★ <b>왜 그 스킬에 이 문장이 붙었나</b> — 시그리드의 셋은 «체력이 바닥일 때 강해지는»
        //   구성이다. 「통제할 수 없는 쾌락」이 최대 체력 10% 아래에서 무적을 주므로,
        //   유저가 후퇴 기준을 30% 로 두면 <b>그 무적이 한 번도 안 켜진다</b> — 그 전에
        //   물러나 버리기 때문이다. 그래서 스킬이 후퇴 기준을 5% 로 못박는다.
        //
        // 짜임은 위 <see cref="RoleLocked"/>(선봉장)와 <b>완전히 같다</b> — 잠금은 두 겹이고
        // (UI 가 버튼·슬라이더를 끄고, 여기서도 값을 거부한다) 거는 순간 값을 스냅한다.
        // ------------------------------------------------------------------

        /// <summary>후퇴 기준이 패시브로 잠겨 있는가. <see cref="CharacterPassives"/> 가 켠다.</summary>
        public bool RetreatHpLocked { get; private set; }

        /// <summary>잠겨 있을 때 강제되는 후퇴 기준(%). 잠기지 않았으면 의미 없다.</summary>
        public int LockedRetreatHpPercent { get; private set; }

        /// <summary>
        /// 후퇴 기준을 <paramref name="percent"/> 로 잠근다. 거는 순간 <b>그 값으로 스냅</b>한다 —
        /// 스킬이 강화로 늦게 해금돼도 그 시점부터 정의문대로 맞춰진다.
        /// </summary>
        public void SetRetreatHpLock(int percent)
        {
            percent = Mathf.Clamp(percent, 0, 100);
            if (RetreatHpLocked && LockedRetreatHpPercent == percent) return;

            RetreatHpLocked = true;
            LockedRetreatHpPercent = percent;
            if (order.retreatHpPercent == percent) return;

            order.retreatHpPercent = percent;
            Apply();
        }

        /// <summary>후퇴 기준 잠금을 푼다. 값은 <b>그대로 둔다</b> — 되돌릴 «원래 값»이 없다.</summary>
        public void ClearRetreatHpLock()
        {
            RetreatHpLocked = false;
        }

        /// <summary>잠긴 값(전방·근거리)으로 맞춘다. 실제로 바뀐 게 있으면 true.</summary>
        bool ForceLockedRole()
        {
            bool changed = false;
            if (order.attackType != TacticalAttackType.Melee)
            {
                order.attackType = TacticalAttackType.Melee;
                changed = true;
            }
            if (order.position != TacticalPosition.Front)
            {
                order.position = TacticalPosition.Front;
                changed = true;
            }
            return changed;
        }

        // ── UI 가 항목 하나씩 바꿀 때 쓰는 편의 메서드 ────────────────────────
        // 값이 안 바뀌었으면 Apply 를 건너뛴다 — 같은 버튼을 다시 눌러도 AI 가
        // 목적지를 다시 뽑느라 캐릭터가 흠칫하지 않게 하려는 것.

        // ------------------------------------------------------------------
        // ★★ 근거리 금지 (2026-08-20 — 아르세니아 「불안정성」 80028)
        //
        // 정의문 첫 문장: <i>"아르세니아는 <b>근거리 공격 유형을 선택할 수 없습니다</b>"</i>.
        // 원화에도 근거리 공격 줄이 <b>아예 없다</b> — 그림이 없는 유형을 고를 수 있으면
        // 공격 모션이 원거리로 폴백해 «칼도 없이 근접해서 때리는» 그림이 된다.
        //
        // <b>왜 「역할 잠금」(RoleLocked) 을 쓰지 않나</b> — 그쪽은 <b>한 값으로 못박는</b>
        // 것이고(히스톤 = 근거리·전방 고정), 이쪽은 <b>한 값만 막는</b> 것이다.
        // 아르세니아는 원거리·마법·회복 중 자유롭게 고를 수 있어야 한다.
        // ------------------------------------------------------------------

        /// <summary>이 캐릭터가 고를 수 없는 공격 유형(없으면 <c>null</c>).</summary>
        public TacticalAttackType? BannedAttackType { get; private set; }

        /// <summary>근거리 금지를 걸거나 푼다. 걸 때 이미 근거리면 <b>즉시 옮긴다</b>.</summary>
        public void SetAttackTypeBan(TacticalAttackType? banned)
        {
            BannedAttackType = banned;
            if (banned == null || order.attackType != banned.Value) return;

            // ⚠ 금지된 값에 이미 서 있으면 어디로 옮길지 정해야 한다 —
            //   능력치 역산(CharacterRole)이 고르는 값을 그대로 쓴다. 그것이
            //   «이 캐릭터에게 가장 맞는 유형» 이라는 이 프로젝트의 기준이다(82-8절).
            var unit = GetComponent<Units.CharacterUnit>();
            TacticalAttackType next = unit != null
                ? Units.CharacterRole.ResolveAttackExcluding(unit.Stats, banned.Value)
                : TacticalAttackType.Ranged;
            order.attackType = next;
            Apply();
        }

        public void SetAttackType(TacticalAttackType v)
        {
            // ★ 금지된 유형은 받지 않는다 (위 ★★). UI 도 버튼을 잠그지만 여기서도 막는다 —
            //   «막는 곳을 한 군데만 두면 다른 경로로 새어 들어온다» 는 이 파일의 규칙이다.
            if (BannedAttackType.HasValue && v == BannedAttackType.Value) return;

            if (RoleLocked) return;               // 「선봉장」 — 근거리 고정
            if (order.attackType == v) return;
            order.attackType = v; Apply();
        }

        public void SetPosition(TacticalPosition v)
        {
            if (RoleLocked) return;               // 「선봉장」 — 전방 고정
            if (order.position == v) return;
            order.position = v;
            // 전방으로 바꾸면 '동료와 함께 후퇴'가 자동으로 '공격 유지'로 돌아간다(Normalize).
            Apply();
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

        public void SetExpeditionType(TacticalExpeditionType v)
        {
            if (order.expeditionType == v) return;
            order.expeditionType = v; Apply();
        }

        public void SetWaveReaction(TacticalWaveReaction v)
        {
            if (order.waveReaction == v) return;
            order.waveReaction = v; Apply();
        }

        /// <summary>
        /// 탐험 배회 범위. <b>협동 탐험이 켜진 부대에 속해 있으면 부대원 전원에게 같이 적용된다</b>
        /// (유저 확정 2026-08-14: "같은 부대에 설정되어 협동 탐험이 켜진 상태라면 한 명만 눌러도
        /// 같은 부대 소속 캐릭터의 설정이 동시에 변경").
        ///
        /// <b>왜 UI 가 아니라 여기서 전파하나</b> — 이 프로젝트는 지침을 바꾸는 경로가 여럿이다
        /// (전술 창 · <see cref="SetOrder"/> · 인스펙터 직접 수정). UI 에 두면
        /// "창으로는 같이 바뀌는데 다른 경로로는 혼자 바뀌는" 구멍이 생긴다 —
        /// <see cref="TacticalOrder.Normalize"/> 를 한 곳에 모아둔 것과 같은 이유다.
        ///
        /// ⚠ <b>전파는 한 단계뿐이다</b> — 부대원에게는 <see cref="ApplyRoamRangeLocal"/>(전파 없는
        /// 버전)를 부른다. 서로가 서로에게 전파하면 무한 재귀가 된다.
        ///
        /// ★ 협동 탐험이 <b>꺼져</b> 있으면 예전처럼 누른 캐릭터만 바뀐다 —
        /// 판정은 <see cref="SquadService.Squad.CoopExpedition"/> 한 곳이다
        /// (49-5절이 "스위치는 LeaderFor 한 곳"이라고 적어둔 것과 같은 원칙).
        /// </summary>
        public void SetRoamRange(TacticalRoamRange v)
        {
            ApplyRoamRangeLocal(v);

            SquadService squads = SquadService.Instance;
            if (squads == null) return;

            var unit = GetComponent<CharacterUnit>();
            SquadService.Squad squad = unit != null ? squads.SquadOf(unit) : null;
            if (squad == null || !squad.CoopExpedition) return;

            for (int i = 0; i < squad.Members.Count; i++)
            {
                CharacterUnit member = squad.Members[i];
                if (member == null || !member.IsAlive || member == unit) continue;

                var tactics = member.GetComponent<CharacterTactics>();
                if (tactics != null) tactics.ApplyRoamRangeLocal(v);
            }
        }

        /// <summary>배회 범위를 <b>이 캐릭터에게만</b> 적용한다 (부대 전파 없음).</summary>
        void ApplyRoamRangeLocal(TacticalRoamRange v)
        {
            if (order.roamRange == v) return;
            order.roamRange = v; Apply();
        }

        /// <summary>
        /// 후퇴 시 행동. <b>전방 포지션에서는 '동료와 함께 후퇴'를 받지 않는다</b> —
        /// UI 가 버튼을 잠그지만, 여기서도 막아 다른 경로로 새어 들어오지 않게 한다.
        /// </summary>
        public void SetRetreatAction(TacticalRetreatAction v)
        {
            if (v == TacticalRetreatAction.FallBackWithAlly && !order.CanFallBackWithAlly) return;
            if (order.retreatAction == v) return;
            order.retreatAction = v; Apply();
        }

        public void SetRetreatHpPercent(int percent)
        {
            if (RetreatHpLocked) return;          // 「가학증」 — 후퇴 기준 고정
            percent = Mathf.Clamp(percent, 0, 100);
            if (order.retreatHpPercent == percent) return;
            order.retreatHpPercent = percent; Apply();
        }

        /// <summary>
        /// UI 의 "초기화" — 코드 기본값으로 되돌린다.
        ///
        /// ⚠ <b>잠긴 칸은 되돌리지 않는다</b>(2026-08-20에 고쳤다). 예전에는 여기서
        /// <see cref="ForceLockedRole"/> 을 안 불러서, 「선봉장」이 걸린 히스톤도
        /// <b>초기화 버튼 한 번으로 중위·원거리가 됐다</b> — 정의문의 「고정」이 깨진다.
        /// 「가학증」의 후퇴 기준도 같은 구멍을 탔을 것이므로 둘을 함께 다시 건다.
        /// </summary>
        public void ResetToDefault()
        {
            order.ResetToDefault();
            ForceAllLocks();
            Apply();
        }

        /// <summary>
        /// 패시브로 잠긴 칸을 전부 다시 강제한다 — 지침을 <b>통째로 갈아끼우는</b> 경로
        /// (<see cref="SetOrder"/> · <see cref="ResetToDefault"/>)에서 부른다.
        /// 항목별 Set* 은 각자 자기 잠금을 보므로 여기 올 일이 없다.
        /// </summary>
        void ForceAllLocks()
        {
            if (RoleLocked) ForceLockedRole();
            if (RetreatHpLocked) order.retreatHpPercent = LockedRetreatHpPercent;
        }
    }
}
