using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Units;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 패시브 스킬의 <b>진행 주체</b>. 씬의 <c>GameSystems</c> 에 붙는다
    /// (<see cref="ErosionService"/> · <c>CharacterUpgradeService</c> 와 같은 자리·같은 패턴).
    ///
    /// 하는 일 셋:
    ///   ① 살아있는 캐릭터 전원의 <see cref="CharacterPassives.Tick"/> 을 밀어준다
    ///      — 캐릭터가 프레임 중간에 생성되는 이 프로젝트에서 <c>Update</c> 를 유닛에 두면
    ///        <c>Start</c> 순서 사고가 난다(29-2절이 침식에서 같은 이유로 이 구조를 썼다).
    ///   ② 정적 전투 이벤트를 <b>한 곳에서만</b> 구독해 해당 캐릭터에게 중계한다
    ///      — 유닛마다 구독하면 죽을 때 해제를 빠뜨려 누수가 난다.
    ///   ③ <b>유닛에 붙지 않는 상태</b>(부식 타이머 · '정화' 표식)를 들고 있다.
    ///      몬스터에 컴포넌트를 붙이면 스폰마다 비용이 들고, 몬스터 스크립트는 PROTO 소유다.
    ///
    /// ⚠️ 정적 이벤트를 쓰므로 도메인 리로드가 꺼져 있어도 구독이 남지 않도록
    /// <see cref="ResetStatics"/> 로 초기화한다(<see cref="DamageableUnit"/> 와 같은 방식).
    /// </summary>
    public class PassiveSkillService : MonoBehaviour
    {
        public static PassiveSkillService Instance { get; private set; }

        [Header("반경 (타일)")]
        [Tooltip("'희생'(엘린)이 다친 동료를 찾는 반경. 정의문에 '엘린의 주변' 이라고만 적혀 있어 " +
                 "표에 수치가 없다 — 값을 코드에 박지 않고 여기서 조정한다")]
        [Min(1f)] [SerializeField] float assistRadius = 6f;

        [Header("판정 시간 (초)")]
        [Tooltip("'포식'·'희열'(프레이야)의 \"직접 공격한 적이 사망\" 판정 유효시간. " +
                 "정의문이 '2초 내' 라고 못박고 있으므로 기본값 2 를 바꾸지 말 것 — " +
                 "표의 value_01(희열)에도 같은 2 가 적혀 있다")]
        [Min(0.1f)] [SerializeField] float killCreditSeconds = 2f;

        [Header("로그")]
        [SerializeField] bool logTriggers = false;

        /// <summary>'희생' 이 동료를 찾는 반경. 서비스가 없으면 기본값으로 떨어진다.</summary>
        public static float AssistRadius => Instance != null ? Instance.assistRadius : 6f;

        // ── 누가 누구를 언제 때렸나 — '포식'·'희열'의 처치 판정용 ──────────────
        //   피해자별로 "마지막으로 때린 캐릭터와 시각" 하나만 들고 있으면 충분하다.
        //   DamageableUnit.LastAttacker 를 쓸 수 없는 이유: 그 값은 <b>몬스터가 때린 것</b>까지
        //   섞여 들어오고, 죽는 순간 이미 다른 값으로 덮여 있을 수 있다.
        static readonly Dictionary<DamageableUnit, (CharacterPassives by, float at)> _lastHitBy =
            new Dictionary<DamageableUnit, (CharacterPassives, float)>();

        // ── 부식: 대상 → (걸어둔 양, 만료 시각) ──────────────────────────────
        static readonly Dictionary<DamageableUnit, (int amount, float until)> _corrosion =
            new Dictionary<DamageableUnit, (int, float)>();

        // ── 정화의 손길: 표식이 붙은 적 ───────────────────────────────────────
        static readonly HashSet<DamageableUnit> _purified = new HashSet<DamageableUnit>();

        readonly List<CharacterPassives> _scratch = new List<CharacterPassives>();
        static readonly List<DamageableUnit> _expired = new List<DamageableUnit>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            _lastHitBy.Clear();
            _corrosion.Clear();
            _purified.Clear();
        }

        void Awake() => Instance = this;

        void OnEnable()
        {
            DamageableUnit.OnAnyAttack += HandleAttack;
            DamageableUnit.OnAnyDied += HandleDied;
        }

        void OnDisable()
        {
            DamageableUnit.OnAnyAttack -= HandleAttack;
            DamageableUnit.OnAnyDied -= HandleDied;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // 캐릭터 전원 진행 — UnitRegistry 를 훑는다(FindObjectsByType 금지, U-D10).
            var all = UnitRegistry.All;
            _scratch.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is CharacterUnit c) || !c.IsAlive) continue;
                CharacterPassives p = CharacterPassives.EnsureOn(c);
                if (p != null) _scratch.Add(p);
            }
            for (int i = 0; i < _scratch.Count; i++) _scratch[i].Tick(dt);

            ExpireCorrosion();
        }

        // ------------------------------------------------------------------
        // 전투 이벤트 중계
        // ------------------------------------------------------------------

        /// <summary>
        /// 공격이 성사되는 순간. <b>피해 계산 전</b>에 발생하므로(<see cref="DamageableUnit.TakeDamageFrom"/>)
        /// 여기서 공격력 보정을 넣으면 바로 그 공격에 반영된다 —
        /// '유혈 낭자'·'광란'의 자해와 '부식'·'정화' 표식이 이 시점을 쓴다.
        /// </summary>
        void HandleAttack(DamageableUnit attacker, DamageableUnit target)
        {
            if (attacker == null || target == null) return;

            // ── '정화' 표식이 붙은 적을 때렸다 → 때린 쪽이 회복하고 표식이 사라진다 ──
            //    표식을 남긴 사람(피올로)이 아니라 <b>때린 캐릭터</b>가 회복한다(정의문 그대로).
            if (_purified.Count > 0 && _purified.Contains(target) &&
                attacker is CharacterUnit healer && attacker.Faction != target.Faction)
            {
                _purified.Remove(target);
                PassiveSkillSO so = FindPurifySource();
                if (so != null)
                {
                    int heal = Mathf.RoundToInt(healer.MaxHp * so.value01 * 0.01f);
                    if (heal > 0 && healer.AcceptsExternalHeal) healer.Heal(heal);
                    if (logTriggers) Debug.Log($"[패시브] 정화 — {healer.DisplayName} +{heal}", healer);
                }
            }

            if (!(attacker is CharacterUnit ch)) return;

            // 처치 판정용 장부 — 캐릭터가 때린 것만 기록한다.
            if (attacker.Faction != target.Faction)
            {
                CharacterPassives p = CharacterPassives.EnsureOn(ch);
                if (p != null) _lastHitBy[target] = (p, Time.time);
            }

            CharacterPassives passives = CharacterPassives.EnsureOn(ch);
            passives?.OnAttacking(target);
        }

        /// <summary>
        /// 어떤 유닛이 죽었다. 최근에 그 유닛을 때린 캐릭터가 있으면 처치 보상을 준다
        /// ('포식' 회복 · '희열' 중첩). 죽은 유닛에 얹혀 있던 상태도 여기서 정리한다.
        /// </summary>
        void HandleDied(DamageableUnit dead)
        {
            if (dead == null) return;

            if (_lastHitBy.TryGetValue(dead, out var credit))
            {
                _lastHitBy.Remove(dead);
                if (credit.by != null && credit.by.Unit != null && credit.by.Unit.IsAlive &&
                    Time.time - credit.at <= killCreditSeconds)
                    credit.by.OnRecentTargetKilled();
            }

            // 죽은 유닛에 걸려 있던 보정은 되돌릴 필요가 없다(사라지므로) — 장부만 비운다.
            _corrosion.Remove(dead);
            _purified.Remove(dead);
        }

        /// <summary>
        /// 지금 살아있는 캐릭터 중 '정화의 손길'을 발동해 둔 사람의 스킬 데이터.
        /// 회복량(<c>value01</c>)이 그 스킬 값이므로 표식을 소모할 때 필요하다.
        /// 여러 명이 켜 있으면 첫 번째를 쓴다 — 지금 이 스킬은 피올로 전용이다.
        /// </summary>
        static PassiveSkillSO FindPurifySource()
        {
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is CharacterUnit c) || !c.IsAlive) continue;
                var p = c.GetComponent<CharacterPassives>();
                if (p == null || !p.PurifyActive) continue;
                PassiveSkillSO so = p.Find(PassiveSkillType.PurifyingTouch);
                if (so != null) return so;
            }
            return null;
        }

        // ------------------------------------------------------------------
        // 부식 / 정화 — 유닛에 컴포넌트를 붙이지 않고 서비스가 장부로 관리한다
        // ------------------------------------------------------------------

        /// <summary>
        /// 부식 — 대상의 방어력을 <paramref name="amount"/> 만큼 깎고 시간이 지나면 되돌린다.
        /// <b>중첩 불가</b>(정의문): 이미 걸려 있으면 지속시간만 새로 잡는다. 값이 달라졌으면
        /// 차액만 조정한다 — 그래야 스킬 값을 바꿔도 방어력이 새지 않는다.
        /// </summary>
        public static void ApplyCorrosion(DamageableUnit target, int amount, float seconds)
        {
            if (target == null || amount == 0) return;
            float until = Time.time + Mathf.Max(0.1f, seconds);

            if (_corrosion.TryGetValue(target, out var now))
            {
                if (now.amount != amount)
                {
                    target.AddDefenseModifier(-(amount - now.amount));
                    _corrosion[target] = (amount, until);
                }
                else
                {
                    _corrosion[target] = (now.amount, until);   // 지속시간만 갱신
                }
                return;
            }

            target.AddDefenseModifier(-amount);
            _corrosion[target] = (amount, until);
        }

        void ExpireCorrosion()
        {
            if (_corrosion.Count == 0) return;

            _expired.Clear();
            foreach (var kv in _corrosion)
            {
                if (kv.Key != null && kv.Key.IsAlive && Time.time < kv.Value.until) continue;
                _expired.Add(kv.Key);
            }
            for (int i = 0; i < _expired.Count; i++)
            {
                DamageableUnit u = _expired[i];
                if (u != null && u.IsAlive) u.AddDefenseModifier(_corrosion[u].amount);
                _corrosion.Remove(u);
            }
        }

        /// <summary>'정화' 표식을 붙인다. 중첩되지 않는다(HashSet).</summary>
        public static void MarkPurified(DamageableUnit target)
        {
            if (target != null && target.IsAlive) _purified.Add(target);
        }

        /// <summary>그 적에게 '정화' 가 붙어 있는지 (UI·연출에서 쓸 수 있게 공개).</summary>
        public static bool IsPurified(DamageableUnit target) =>
            target != null && _purified.Contains(target);
    }
}
