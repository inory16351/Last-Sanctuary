using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 부대(Squad) — 캐릭터 여러 명을 한 묶음으로 다루는 편성. 씬의 <c>GameSystems</c> 에 붙어 있고
    /// 부대 개수 상한 등은 <b>인스펙터에서 고친다</b>(진행상황 35절과 같은 원칙 — 값을 코드에 박지 않는다).
    ///
    /// <b>부대가 하는 일은 "함께 이동"뿐이다</b>(유저 확정 2026-08-11):
    /// 같은 부대원은 <b>탐색·사냥할 때 함께 움직인다.</b> 전투 방식·전열·타겟팅 같은 전술 지침은
    /// 여전히 캐릭터마다 따로다(<see cref="CharacterTactics"/>).
    ///
    /// <b>★ 개인 비전투 우선순위를 덮지 않는다</b>(유저 확정):
    /// 1번과 2번이 같은 부대인데 1번이 "건물 건설"이면 → 1번은 혼자 건설하러 가고 2번은 사냥·탐색을
    /// 계속한다. 1번은 <b>건설이 끝나면 자동으로 합류</b>한다. 그래서 부대 이동은 "지금 실제로
    /// 탐색·사냥 중인 부대원"끼리만 적용된다 — 판정은 <see cref="IsMovementEligible"/> 한 곳에 있다.
    ///
    /// <b>왜 중앙 서비스인가</b> — <see cref="CharacterTactics"/> 는 "캐릭터마다 한 벌"이라 컴포넌트가
    /// 맞았지만, 부대는 <b>여러 캐릭터에 걸친 집합</b>이라 한 캐릭터에 담을 수 없다.
    /// 대신 그 문서가 경고한 문제(죽은 캐릭터 항목이 남는 것)를 <see cref="HandleAnyDied"/> 로 직접 막는다.
    /// </summary>
    public class SquadService : MonoBehaviour
    {
        public static SquadService Instance { get; private set; }

        [Header("편성 규칙")]
        [Tooltip("만들 수 있는 부대 최대 개수")]
        [Min(1)] [SerializeField] int maxSquads = 6;

        [Tooltip("부대 하나에 넣을 수 있는 최대 인원. 0 이면 제한 없음")]
        [Min(0)] [SerializeField] int maxMembersPerSquad = 0;

        [Tooltip("새 부대 이름 형식. {0} 에 번호가 들어간다")]
        [SerializeField] string squadNameFormat = "{0}부대";

        [Tooltip("유저가 직접 지을 수 있는 부대 이름의 최대 글자 수. 깃발 위 이름표가 " +
                 "너무 길어지지 않게 하는 값이라 화면을 보고 조정할 것")]
        [Min(1)] [SerializeField] int maxNameLength = 10;

        [Header("디버그")]
        [SerializeField] bool logChanges = true;

        /// <summary>부대 하나.</summary>
        public class Squad
        {
            public int Id;
            public string Name;
            public readonly List<CharacterUnit> Members = new List<CharacterUnit>();

            /// <summary>살아있는 인원만 센다.</summary>
            public int AliveCount
            {
                get
                {
                    int n = 0;
                    for (int i = 0; i < Members.Count; i++)
                        if (Members[i] != null && Members[i].IsAlive) n++;
                    return n;
                }
            }
        }

        readonly List<Squad> _squads = new List<Squad>();
        int _nextId = 1;

        /// <summary>편성이 바뀌었다 (부대 추가·삭제·인원 변경). UI 가 표시를 다시 그린다.</summary>
        public event System.Action OnSquadsChanged;

        public IReadOnlyList<Squad> Squads => _squads;
        public int MaxSquads => maxSquads;
        public bool CanCreate => _squads.Count < maxSquads;

        void Awake() => Instance = this;

        void OnEnable() => DamageableUnit.OnAnyDied += HandleAnyDied;
        void OnDisable() => DamageableUnit.OnAnyDied -= HandleAnyDied;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------
        // 편성
        // ------------------------------------------------------------------

        /// <summary>새 부대를 만든다. 상한에 걸리면 null.</summary>
        public Squad CreateSquad()
        {
            if (!CanCreate) return null;

            var squad = new Squad { Id = _nextId++, Name = string.Format(squadNameFormat, _squads.Count + 1) };
            _squads.Add(squad);

            if (logChanges) Debug.Log($"[Squad] {squad.Name} 생성 (총 {_squads.Count}개)", this);
            OnSquadsChanged?.Invoke();
            return squad;
        }

        /// <summary>부대를 없앤다. 소속돼 있던 캐릭터는 무소속이 된다.</summary>
        public void RemoveSquad(int squadId)
        {
            int index = _squads.FindIndex(s => s.Id == squadId);
            if (index < 0) return;

            string name = _squads[index].Name;
            _squads.RemoveAt(index);

            if (logChanges) Debug.Log($"[Squad] {name} 삭제 (총 {_squads.Count}개)", this);
            OnSquadsChanged?.Invoke();
        }

        public Squad Find(int squadId) => _squads.Find(s => s.Id == squadId);

        /// <summary>
        /// 부대 이름을 유저가 지은 것으로 바꾼다(유저 확정 2026-08-12 — 깃발 위에 이 이름이 뜬다).
        /// <b>빈 이름은 거부</b>한다 — 깃발에 아무 글자도 없으면 어느 부대 것인지 알 수 없다.
        /// 길이는 <c>maxNameLength</c> 로 자른다(인스펙터).
        /// </summary>
        public bool Rename(int squadId, string name)
        {
            Squad squad = Find(squadId);
            if (squad == null) return false;

            string trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0) return false;
            if (trimmed.Length > maxNameLength) trimmed = trimmed.Substring(0, maxNameLength);
            if (trimmed == squad.Name) return false;

            squad.Name = trimmed;
            if (logChanges) Debug.Log($"[Squad] 부대 #{squadId} 이름 → {trimmed}", this);
            OnSquadsChanged?.Invoke();
            return true;
        }

        /// <summary>이 캐릭터가 속한 부대. 무소속이면 null.</summary>
        public Squad SquadOf(CharacterUnit unit)
        {
            if (unit == null) return null;
            for (int i = 0; i < _squads.Count; i++)
                if (_squads[i].Members.Contains(unit)) return _squads[i];
            return null;
        }

        public int SquadIdOf(CharacterUnit unit)
        {
            Squad s = SquadOf(unit);
            return s != null ? s.Id : 0;
        }

        /// <summary>
        /// 캐릭터를 부대에 넣는다. <b>이미 다른 부대에 있으면 자동으로 빠져나온다</b> —
        /// 한 캐릭터가 두 부대에 동시에 속하면 "함께 이동"의 기준이 모순되기 때문이다.
        /// 같은 부대를 다시 지정하면 <b>배정을 해제</b>한다(토글) — 로스터를 다시 눌러 뺄 수 있게.
        /// </summary>
        public bool Assign(CharacterUnit unit, int squadId)
        {
            if (unit == null) return false;

            Squad target = Find(squadId);
            if (target == null) return false;

            Squad current = SquadOf(unit);
            if (current == target)
            {
                current.Members.Remove(unit);
                if (logChanges) Debug.Log($"[Squad] {unit.DisplayName} → {target.Name} 배정 해제", this);
                OnSquadsChanged?.Invoke();
                return true;
            }

            if (maxMembersPerSquad > 0 && target.Members.Count >= maxMembersPerSquad)
            {
                if (logChanges) Debug.Log($"[Squad] {target.Name} 인원 상한({maxMembersPerSquad}) — {unit.DisplayName} 배정 실패", this);
                return false;
            }

            current?.Members.Remove(unit);
            target.Members.Add(unit);

            if (logChanges) Debug.Log($"[Squad] {unit.DisplayName} → {target.Name} 배정", this);
            OnSquadsChanged?.Invoke();
            return true;
        }

        /// <summary>어느 부대에도 속하지 않게 한다.</summary>
        public void Unassign(CharacterUnit unit)
        {
            Squad current = SquadOf(unit);
            if (current == null) return;

            current.Members.Remove(unit);
            if (logChanges) Debug.Log($"[Squad] {unit.DisplayName} 부대 해제", this);
            OnSquadsChanged?.Invoke();
        }

        // ------------------------------------------------------------------
        // 함께 이동 — 누가 기준인가
        // ------------------------------------------------------------------

        /// <summary>
        /// 지금 <b>부대 이동에 참여할 수 있는</b> 상태인가.
        ///
        /// 건설하러 간 캐릭터는 빠진다(유저 확정) — <c>CharacterBehavior.Update</c> 가
        /// <c>TryBuild()</c> 에서 먼저 빠져나가므로 <see cref="CharacterDuty.Build"/> 가
        /// 곧 "지금 건설 중"이라는 뜻이고, 지을 곳이 없어지면 duty 가 저절로 Scout/Guard 로
        /// 돌아오면서 <b>자동으로 합류</b>한다. 별도의 "합류" 처리가 필요 없는 이유다.
        ///
        /// 후퇴·정신 이상 중인 캐릭터도 뺀다 — 그 상태들은 이동을 스스로 통제해야 한다.
        /// </summary>
        public static bool IsMovementEligible(CharacterBehavior behavior)
        {
            if (behavior == null) return false;

            var unit = behavior.GetComponent<CharacterUnit>();
            if (unit == null || !unit.IsAlive) return false;

            if (behavior.Duty == CharacterDuty.Build) return false;   // 건설 중 — 혼자 간다
            if (behavior.IsRetreating) return false;
            if (behavior.Mental != MentalOverride.None) return false;

            return true;
        }

        /// <summary>
        /// 이 캐릭터가 따라야 할 <b>부대 기준원</b>. 자기 자신이 기준이면 null 을 돌려준다
        /// (= 스스로 목적지를 고른다).
        ///
        /// 기준은 <b>부대원 중 이동 가능한 첫 번째</b>다. 순서는 배정 순서 그대로라
        /// 매 프레임 흔들리지 않는다 — 거리나 체력으로 고르면 기준이 계속 바뀌어
        /// 부대가 서로를 쫓아다니며 진동한다.
        /// </summary>
        public CharacterBehavior LeaderFor(CharacterBehavior member)
        {
            if (member == null) return null;

            var unit = member.GetComponent<CharacterUnit>();
            if (unit == null) return null;

            Squad squad = SquadOf(unit);
            if (squad == null || squad.Members.Count < 2) return null;

            for (int i = 0; i < squad.Members.Count; i++)
            {
                CharacterUnit m = squad.Members[i];
                if (m == null || !m.IsAlive) continue;

                var behavior = m.GetComponent<CharacterBehavior>();
                if (!IsMovementEligible(behavior)) continue;

                // 기준이 나 자신이면 내가 앞장선다
                return behavior == member ? null : behavior;
            }
            return null;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// 죽은 캐릭터를 편성에서 지운다. <see cref="CharacterTactics"/> 문서가 경고한
        /// "중앙 딕셔너리에 죽은 항목이 남는 문제"를 여기서 직접 막는다.
        /// </summary>
        void HandleAnyDied(DamageableUnit dead)
        {
            if (dead is not CharacterUnit character) return;

            bool removed = false;
            for (int i = 0; i < _squads.Count; i++)
                if (_squads[i].Members.Remove(character)) removed = true;

            if (removed) OnSquadsChanged?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;
    }
}
