using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.UI;
using LastSanctuary.Units;

namespace LastSanctuary.Combat
{
    /// <summary>영웅 각성의 세 단계. 로스터·성장 창이 표시에 쓴다.</summary>
    public enum HeroState
    {
        /// <summary>아직 처치 수가 모자라다.</summary>
        None = 0,

        /// <summary>처치 수를 채웠다 — 이제 <b>처치할 때마다</b> 각성을 굴린다.</summary>
        Ready = 1,

        /// <summary>각성했다. 능력치가 상한을 넘어 올라가 있다.</summary>
        Awakened = 2,
    }

    /// <summary>
    /// <b>처치 기록</b>과 <b>영웅 각성</b>의 규칙·수치를 들고 있는 서비스.
    /// 씬의 <c>GameSystems</c> 에 붙는다(<see cref="ErosionService"/> ·
    /// <see cref="PassiveSkillService"/> · <c>CharacterUpgradeService</c> 와 같은 자리·같은 패턴).
    /// 캐릭터별 상태는 <see cref="CharacterKills"/> 가 들고 있다.
    ///
    /// ══════════════════════════════════════════════════════════════════════
    /// <b>① 처치 인정 — "마지막에 때린 사람" 이 아니다</b> (유저 지시 2026-08-18)
    ///
    /// <i>"해당 캐릭터의 공격에 맞은 적이 2초 이내에 사망할 경우(지속 갱신)"</i> —
    /// 그러므로 죽기 전 <see cref="killCreditSeconds"/> 초 안에 때린 <b>모든 캐릭터</b>가
    /// 각각 1 킬을 받는다. 셋이 같이 잡으면 셋 다 1 킬이다(나눠 갖지 않는다).
    ///
    /// ⚠ <b>그래서 <see cref="PassiveSkillService"/> 의 장부를 재사용할 수 없었다.</b>
    /// 그쪽(「포식」·「희열」)은 피해자마다 <b>마지막에 때린 한 명</b>만 기억한다 —
    /// 같은 2초라도 <b>세는 방식이 다르다</b>. 한 장부를 억지로 공유하면 둘 중 하나는
    /// 반드시 틀린 값을 받는다.
    ///
    /// ⚠ <see cref="DamageableUnit.LastAttacker"/> 도 쓸 수 없다 — 그 값에는
    /// <b>몬스터가 몬스터를 때린 것</b>까지 섞이고, 죽는 순간 이미 덮여 있을 수 있다.
    ///
    /// ⚠ 판정은 <see cref="DamageableUnit.OnAnyDamaged"/>(피해가 <b>실제로 들어간</b> 순간)로 한다.
    /// <c>OnAnyAttack</c> 은 <b>빗나가도</b> 발생하므로 "맞은 적" 이라는 조건과 맞지 않는다.
    ///
    /// ══════════════════════════════════════════════════════════════════════
    /// <b>② 영웅 각성</b>
    ///
    ///   처치 <see cref="awakenKillThreshold"/> 이상          → <b>각성 가능</b> 상태
    ///   + Lv <see cref="awakenMinLevel"/> 이상 + 처치 성공   → <see cref="awakenChancePercent"/> % 로 각성
    ///
    /// 굴림은 <b>처치할 때마다</b> 한 번씩이다. 그래서 "언제 각성할지"는 알 수 없지만
    /// "많이 싸운 캐릭터가 먼저 된다" 가 보장된다.
    ///
    /// <b>효과 — 능력치 상한을 뚫는다.</b> 강화(<c>CharacterUpgradeService.Grow</c>)는
    /// <see cref="BalanceConfigSO.statMax"/> 에서 잘리지만, 각성은
    /// <see cref="CharacterUnit.AddFlatStatBonus"/> 를 쓴다 — 그 경로는 <b>상한을 적용한 뒤에</b>
    /// 더하도록 이미 만들어져 있다(패시브 「광란」·「희열」이 쓰는 그 통로다).
    /// 각성 능력치를 위해 새 개념을 만들지 않은 이유가 이것이다.
    ///
    /// ⚠ <b>저항력은 오르지 않는다</b>(<see cref="StatBlock.IsGrowable"/>) — 캐릭터 고유
    /// 고정값이라 강화로도 안 오른다. 각성만 예외로 두면 침식 밸런스가 통째로 흔들린다.
    /// </summary>
    public class HeroAwakeningService : MonoBehaviour
    {
        public static HeroAwakeningService Instance { get; private set; }

        // ──────────────────────────────────────────────────────────────────
        // 수치 — 전부 에딧 모드에서 조정한다 (유저 지시 2026-08-18)
        // ──────────────────────────────────────────────────────────────────

        [Header("처치 인정")]
        [Tooltip("내 공격에 맞은 적이 이 시간 안에 죽으면 내 처치로 인정한다. " +
                 "때릴 때마다 다시 세므로(지속 갱신) 계속 때리는 동안에는 끊기지 않는다")]
        [Min(0.1f)] [SerializeField] float killCreditSeconds = 2f;

        [Tooltip("처치 기록과 영웅 각성을 켠다. 끄면 이 서비스가 아무것도 하지 않는다 — " +
                 "밸런싱 중 영웅 각성만 빼고 테스트할 때 쓴다")]
        [SerializeField] bool trackKills = true;

        [Header("영웅 각성 — 조건")]
        [Tooltip("이 처치 수를 넘기면 '각성 가능' 상태가 된다")]
        [Min(1)] [SerializeField] int awakenKillThreshold = 50;

        [Tooltip("각성하려면 이 레벨(강화 횟수) 이상이어야 한다. 조건을 못 채우면 굴리지도 않는다")]
        [Min(0)] [SerializeField] int awakenMinLevel = 10;

        [Tooltip("조건을 모두 채운 상태에서 <b>처치 1회마다</b> 각성을 굴리는 확률(%). " +
                 "5 면 평균 20킬쯤 더 싸우면 각성한다")]
        [Range(0f, 100f)] [SerializeField] float awakenChancePercent = 5f;

        [Tooltip("한 캐릭터가 각성할 수 있는 최대 횟수. 1 이면 평생 한 번이다")]
        [Min(1)] [SerializeField] int maxAwakenings = 1;

        [Tooltip("두 번째 이후 각성에 추가로 필요한 처치 수. " +
                 "최대 횟수가 1 이면 쓰이지 않는다")]
        [Min(0)] [SerializeField] int killThresholdPerAwakening = 50;

        [Header("영웅 각성 — 효과 (능력치 상한을 넘는다)")]
        [Tooltip("각성 1회에 성장 가능한 능력치 전부가 이만큼 오른다. " +
                 "★ 이 상승분은 능력치 상한(BalanceConfig.statMax)을 넘어설 수 있다")]
        [Min(0)] [SerializeField] int awakenStatBonus = 8;

        [Tooltip("성장 유형(탱커·근거리…)에 묶인 능력치가 추가로 받는 값. " +
                 "4 면 묶인 능력치는 8+4=12 만큼 오른다")]
        [Min(0)] [SerializeField] int awakenFocusBonus = 4;

        [Header("로그")]
        [Tooltip("처치가 인정될 때마다 콘솔에 남긴다 (많이 찍히므로 평소에는 끈다)")]
        [SerializeField] bool logKills = false;

        [Tooltip("각성했을 때 HUD 로그와 콘솔에 남긴다")]
        [SerializeField] bool logAwakening = true;

        // ──────────────────────────────────────────────────────────────────
        // 장부 — 피해자마다 "누가 언제 때렸나" 목록
        //
        // 목록인 이유는 위 ① 참조 — 2초 안에 때린 <b>전원</b>이 처치를 받기 때문이다.
        // 같은 캐릭터가 여러 번 때리면 <b>시각만 갱신</b>한다(중복 등록하지 않는다).
        // ──────────────────────────────────────────────────────────────────

        struct Hit
        {
            public CharacterKills By;
            public float At;
        }

        static readonly Dictionary<DamageableUnit, List<Hit>> _hits =
            new Dictionary<DamageableUnit, List<Hit>>();

        /// <summary>비워 두고 재사용하는 목록 풀 — 난전에서 매 타격마다 할당하지 않으려는 것.</summary>
        static readonly Stack<List<Hit>> _pool = new Stack<List<Hit>>();

        static readonly List<DamageableUnit> _sweepScratch = new List<DamageableUnit>();

        /// <summary>장부 청소 주기(초). 죽지 않고 사라진 적(파괴·디스폰)의 항목을 걷어낸다.</summary>
        const float SweepIntervalSeconds = 5f;

        float _nextSweepAt;

        /// <summary>각성이 일어났다 (각성한 캐릭터, 몇 번째 각성인지). UI 연출용.</summary>
        public static event System.Action<CharacterUnit, int> OnAwakened;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            _hits.Clear();
            _pool.Clear();
            OnAwakened = null;
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            DamageableUnit.OnAnyDamaged += HandleDamaged;
            DamageableUnit.OnAnyDied += HandleDied;
        }

        void OnDisable()
        {
            DamageableUnit.OnAnyDamaged -= HandleDamaged;
            DamageableUnit.OnAnyDied -= HandleDied;
        }

        void Update()
        {
            if (Time.unscaledTime < _nextSweepAt) return;
            _nextSweepAt = Time.unscaledTime + SweepIntervalSeconds;
            Sweep();
        }

        // ==================================================================
        // 조회 — UI 가 쓴다
        // ==================================================================

        /// <summary>이 캐릭터의 처치 수. 기록이 없으면 0.</summary>
        public static int KillsOf(CharacterUnit unit)
        {
            CharacterKills k = CharacterKills.Of(unit);
            return k != null ? k.Kills : 0;
        }

        /// <summary>
        /// 이 캐릭터의 영웅 상태. 서비스가 씬에 없으면 각성 판단을 할 수 없으므로
        /// 이미 각성했는지만 보고 답한다.
        /// </summary>
        public static HeroState StateOf(CharacterUnit unit)
        {
            CharacterKills k = CharacterKills.Of(unit);
            if (k == null) return HeroState.None;
            if (k.IsHero) return HeroState.Awakened;

            HeroAwakeningService s = Instance;
            if (s == null) return HeroState.None;
            return k.Kills >= s.KillsNeededFor(k) ? HeroState.Ready : HeroState.None;
        }

        /// <summary>다음 각성까지 필요한 처치 수. 이미 최대 각성이면 0.</summary>
        public static int KillsRemainingFor(CharacterUnit unit)
        {
            CharacterKills k = CharacterKills.Of(unit);
            HeroAwakeningService s = Instance;
            if (k == null || s == null) return 0;
            if (k.Awakenings >= s.maxAwakenings) return 0;
            return Mathf.Max(0, s.KillsNeededFor(k) - k.Kills);
        }

        /// <summary>이 기록이 다음 각성을 굴리려면 몇 킬이 필요한가.</summary>
        int KillsNeededFor(CharacterKills k) =>
            awakenKillThreshold + killThresholdPerAwakening * Mathf.Max(0, k.Awakenings);

        // ==================================================================
        // 장부
        // ==================================================================

        /// <summary>
        /// 피해가 <b>실제로 들어간</b> 순간. 때린 쪽이 캐릭터이고 맞은 쪽이 아군이 아니면 기록한다.
        ///
        /// ⚠ <paramref name="attacker"/> 는 null 일 수 있다(패시브 지속 피해 등) —
        /// 주인 없는 피해는 누구의 처치도 아니다.
        /// </summary>
        void HandleDamaged(DamageableUnit attacker, DamageableUnit victim, int amount, bool critical)
        {
            if (!trackKills || attacker == null || victim == null) return;
            if (victim.Faction == Faction.Angel) return;      // 아군 오사(정신 이상 '혼란')는 세지 않는다
            var character = attacker as CharacterUnit;
            if (character == null) return;

            CharacterKills record = CharacterKills.EnsureOn(character);
            if (record == null) return;

            if (!_hits.TryGetValue(victim, out List<Hit> list))
            {
                list = _pool.Count > 0 ? _pool.Pop() : new List<Hit>(4);
                list.Clear();
                _hits[victim] = list;
            }

            float now = Time.time;

            // 같은 캐릭터가 이미 있으면 <b>시각만 갱신</b>한다 — 유저 지시의 "지속 갱신" 이 이것이다.
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].By != record) continue;
                list[i] = new Hit { By = record, At = now };
                return;
            }

            list.Add(new Hit { By = record, At = now });
        }

        /// <summary>
        /// 누군가 죽었다. 죽은 것이 적이면 <see cref="killCreditSeconds"/> 안에 때린 캐릭터
        /// 전원에게 처치를 인정한다.
        ///
        /// ⚠ 죽은 것이 <b>캐릭터</b>여도 장부는 치워야 한다 — 그 캐릭터가 때리던 적의 목록에는
        /// 여전히 남아 있지만, 그건 <see cref="Sweep"/> 과 아래 유효성 검사가 걸러낸다.
        /// </summary>
        void HandleDied(DamageableUnit unit)
        {
            if (unit == null) return;
            if (!_hits.TryGetValue(unit, out List<Hit> list)) return;

            _hits.Remove(unit);

            if (trackKills && unit.Faction != Faction.Angel)
            {
                float now = Time.time;
                for (int i = 0; i < list.Count; i++)
                {
                    if (now - list[i].At > killCreditSeconds) continue;   // 너무 오래됐다
                    if (list[i].By == null) continue;                     // 그 사이 사라진 캐릭터
                    AwardKill(list[i].By, unit);
                }
            }

            list.Clear();
            _pool.Push(list);
        }

        /// <summary>
        /// 죽지 않고 사라진 적(파괴·디스폰)의 항목과 다 식은 항목을 걷어낸다.
        /// 안 하면 <see cref="_hits"/> 가 한 판 내내 자라기만 한다.
        /// </summary>
        void Sweep()
        {
            float now = Time.time;
            _sweepScratch.Clear();

            foreach (KeyValuePair<DamageableUnit, List<Hit>> pair in _hits)
            {
                if (pair.Key == null) { _sweepScratch.Add(pair.Key); continue; }

                List<Hit> list = pair.Value;
                for (int i = list.Count - 1; i >= 0; i--)
                    if (list[i].By == null || now - list[i].At > killCreditSeconds)
                        list.RemoveAt(i);

                if (list.Count == 0) _sweepScratch.Add(pair.Key);
            }

            for (int i = 0; i < _sweepScratch.Count; i++)
            {
                if (_hits.TryGetValue(_sweepScratch[i], out List<Hit> list))
                {
                    list.Clear();
                    _pool.Push(list);
                }
                _hits.Remove(_sweepScratch[i]);
            }

            _sweepScratch.Clear();
        }

        // ==================================================================
        // 처치 · 각성
        // ==================================================================

        void AwardKill(CharacterKills record, DamageableUnit victim)
        {
            record.AddKill();

            if (logKills && record.Unit != null)
                Debug.Log($"[처치] {record.Unit.DisplayName} — {victim.DisplayName} " +
                          $"(누적 {record.Kills})", record);

            TryAwaken(record);
        }

        /// <summary>
        /// 각성을 굴린다. 조건을 하나라도 못 채우면 굴리지 않는다 —
        /// <b>순서가 곧 문서</b>다(횟수 → 처치 수 → 레벨 → 확률).
        /// </summary>
        void TryAwaken(CharacterKills record)
        {
            if (record.Awakenings >= maxAwakenings) return;
            if (record.Kills < KillsNeededFor(record)) return;

            CharacterUnit unit = record.Unit;
            if (unit == null) return;
            if (unit.UpgradeCount < awakenMinLevel) return;

            if (Random.value * 100f >= awakenChancePercent) return;

            Awaken(record, unit);
        }

        /// <summary>
        /// 실제 각성. 성장 가능한 능력치 전부를 <b>상한을 넘겨</b> 올린다.
        ///
        /// ★ <see cref="CharacterUnit.AddFlatStatBonus"/> 를 쓰는 것이 핵심이다 —
        /// 능력치 값 자체(<see cref="CharacterUnit.Stats"/>)를 올리면
        /// <see cref="BalanceConfigSO.statMax"/> 에서 잘리고 강화 계산과도 섞인다.
        /// 고정 보정 칸은 <b>상한 적용 뒤에</b> 더해지도록 이미 만들어져 있다.
        /// </summary>
        void Awaken(CharacterKills record, CharacterUnit unit)
        {
            record.RegisterAwakening();

            for (int i = 0; i < (int)StatType.COUNT; i++)
            {
                var t = (StatType)i;
                if (!StatBlock.IsGrowable(t)) continue;      // 저항력은 고유 고정값이다

                int bonus = awakenStatBonus;
                if (StatGrowthFocusTable.IsFavored(unit.GrowthFocus, t)) bonus += awakenFocusBonus;
                if (bonus == 0) continue;

                unit.AddFlatStatBonus(t, bonus);

                // ★ 성장 창이 그릴 <b>표시용 사본</b>에도 같은 양을 적는다.
                //   화면은 EffectiveStat(임시 보정까지 얹힌 값)이 아니라 이 사본을 쓴다 —
                //   그래야 숫자가 흔들리지 않는다(CharacterKills.RecordAwakenBonus 주석 참조).
                record.RecordAwakenBonus(t, bonus);
            }

            if (logAwakening)
            {
                string suffix = maxAwakenings > 1 ? $" ({record.Awakenings}단계)" : string.Empty;
                HudLog.Add($"{unit.DisplayName} 영웅 각성!{suffix}", HudLogKind.Good);
                Debug.Log($"[영웅] {unit.DisplayName} 각성 {record.Awakenings}회 · " +
                          $"처치 {record.Kills} · Lv.{unit.UpgradeCount} · " +
                          $"능력치 +{awakenStatBonus}(성장 유형 +{awakenStatBonus + awakenFocusBonus})", unit);
            }

            OnAwakened?.Invoke(unit, record.Awakenings);
        }
    }
}
