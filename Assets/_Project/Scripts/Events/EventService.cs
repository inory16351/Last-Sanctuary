using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Wave;

namespace LastSanctuary.Events
{
    /// <summary>
    /// ★★ <b>이벤트 진행자</b> — 표(<c>이벤트테이블</c>)를 읽어 «언제 어떤 이벤트가 뜨는가» 를 정한다.
    ///
    /// <b>어디에 붙나</b> — 씬의 <c>GameSystems</c> 다. 그 오브젝트에 이미
    /// <c>ResourceManager</c>·<c>SquadService</c> 같은 «판 하나에 하나뿐인 서비스» 들이
    /// 모여 있어 같은 자리가 맞다.
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  자연 발생 확률 — <b>인스펙터에서 조정한다</b> (유저 지시 2026-08-21)
    /// ══════════════════════════════════════════════════════════════════
    /// 표(<c>EventType</c> 시트)의 <c>event_cond_value_01</c> 이 <b>80</b> 이다:
    /// *"80%의 웨이브 이벤트 확률 내에서 가중치 {value_02} 만큼 랜덤하게 해당 이벤트가 발생합니다"*.
    ///
    /// 즉 «두 단계» 다:
    ///   ① <b>발생하는가</b>  — 이 확률(<see cref="waveEventChancePercent"/>)로 한 번 굴린다
    ///   ② <b>무엇이 뜨는가</b> — 통과했으면 <c>event_value_02</c>(가중치)로 하나를 뽑는다
    ///
    /// ★ ①의 값을 <b>에셋이 아니라 이 컴포넌트</b>에 둔 이유 — 밸런싱 중에 가장 자주 만지는
    ///   값이고, 표에는 타입별로 하나씩(둘)만 있다. 42개 에셋에 흩어 두면 «한 번에 바꾸기» 가
    ///   안 되고, 표를 다시 생성하면 손으로 고친 값이 지워진다.
    ///   ⚠ 그래서 <b>표의 80 과 여기 기본값이 같아야 한다</b> — 갈리면 «표대로 안 나온다» 가 된다.
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  <b>지속시간</b> — 같은 인스펙터의 «이벤트 지속시간» 칸 (유저 지시 2026-08-21 3차)
    /// ══════════════════════════════════════════════════════════════════
    /// <c>useTableDuration</c> 을 켜면 표의 <c>event_value_01</c>(웨이브 120 · 비공개 180),
    /// 끄면 그 아래 두 칸이 <b>전부를 덮는다</b>. 세는 것은 <see cref="TickDuration"/> 이다.
    ///
    /// ⚠ 이 클래스는 <b>임시 UI</b>(<c>UI.EventPanel</c>)와 짝이다. 대사 흐름·선택지·보상까지
    ///   표대로 돌아가지만 <b>배경 이미지</b>(<c>event_bg</c>)와 <b>토벌 이벤트</b>
    ///   (ev_raid — «몬스터에 인접하면 발생») 는 아직 배선하지 않았다.
    /// </summary>
    public class EventService : MonoBehaviour
    {
        public static EventService Instance { get; private set; }

        // ──────────────────────────────────────────────────────────────
        //  인스펙터 — 사람이 정하는 값
        // ──────────────────────────────────────────────────────────────

        [Header("자연 발생 확률 (%) — 표 EventType.event_cond_value_01")]
        [Tooltip("웨이브 타이머가 시작될 때 웨이브 이벤트가 하나 뜰 확률. 표 기준 80. " +
                 "0 이면 웨이브 이벤트가 뜨지 않는다")]
        [Range(0, 100)] [SerializeField] int waveEventChancePercent = 80;

        [Tooltip("비공개 타이머가 돌 때 타이머 이벤트가 하나 뜰 확률. 표 기준 80")]
        [Range(0, 100)] [SerializeField] int privateEventChancePercent = 80;

        [Header("비공개 타이머")]
        [Tooltip("비공개 타이머의 주기(초). 표의 event_value_01 이 180 이라 그 값을 기본으로 둔다. " +
                 "이 타이머는 화면에 보이지 않는다 — 그래서 «비공개» 다")]
        [Min(5f)] [SerializeField] float privateTimerSeconds = 180f;

        [Tooltip("판이 시작되고 첫 비공개 타이머가 돌기까지의 시간(초)")]
        [Min(0f)] [SerializeField] float privateFirstDelaySeconds = 60f;

        // ──────────────────────────────────────────────────────────────
        //  ★★ 이벤트 <b>지속시간</b> — 여기서 조정한다 (유저 지시 2026-08-21 3차:
        //      *"이벤트 타이머 지속시간 에딧에서 수정가능하게 게임 시스템 하이라키에서"*)
        //
        //  <b>어디를 만지나</b> — Hierarchy ▸ GameSystems ▸ Inspector ▸ Event Service
        //  ▸ «이벤트 지속시간» 칸이다. 침식(<c>ErosionService</c>)·중립 사냥 성장
        //  (<c>NeutralGrowthService</c>)이 있는 <b>같은 자리</b>다.
        //
        //  ⚠⚠ <b>지속시간은 지금까지 «있지만 안 도는» 값이었다.</b> 표의
        //  <c>event_value_01</c>(웨이브 120 · 비공개 180)을
        //  <see cref="EventDefinitionSO.DurationSeconds"/> 가 읽어 두기는 했는데
        //  <b>아무도 쓰지 않았다</b> — 이벤트는 오직 «웨이브 단계가 바뀔 때»
        //  (<see cref="HandlePhase"/>)만 끝났다. 그래서 비공개 이벤트는 웨이브가 넘어갈
        //  때까지, 즉 <b>표가 적은 180초와 아무 상관 없이</b> 남아 있었다.
        //  → <see cref="TickDuration"/> 이 실제로 센다. 값을 만지면 <b>정말로 바뀐다.</b>
        // ──────────────────────────────────────────────────────────────

        [Header("이벤트 지속시간")]
        [Tooltip("켜면 표(event_value_01)에 적힌 길이를 쓴다 — 웨이브 120초 · 비공개 180초. " +
                 "끄면 아래 두 칸의 값으로 <b>전부 덮어쓴다</b> (밸런싱 중에 한 번에 바꾸고 싶을 때)")]
        [SerializeField] bool useTableDuration = true;

        [Tooltip("웨이브 이벤트가 유지되는 시간(초). useTableDuration 을 껐을 때만 쓴다. " +
                 "0 이면 «시간으로는 안 끝난다» — 웨이브가 넘어갈 때까지 남는다")]
        [Min(0f)] [SerializeField] float waveEventDurationSeconds = 120f;

        [Tooltip("비공개·토벌 이벤트가 유지되는 시간(초). useTableDuration 을 껐을 때만 쓴다. " +
                 "0 이면 «시간으로는 안 끝난다»")]
        [Min(0f)] [SerializeField] float privateEventDurationSeconds = 180f;

        [Tooltip("★ 지속시간을 <b>창이 열려 있는 동안에는 세지 않는다</b>. 유저가 선택지를 " +
                 "읽는 시간이 제한시간에서 깎이면 «읽다가 사라졌다» 가 된다. " +
                 "끄면 창이 떠 있어도 시간이 흐른다(제한시간 연출을 원할 때)")]
        [SerializeField] bool pauseWhilePanelOpen = true;

        [Header("스위치")]
        [Tooltip("끄면 이벤트가 하나도 뜨지 않는다. 다른 것을 검증할 때 쓴다")]
        [SerializeField] bool eventsEnabled = true;

        [Tooltip("★ 확률을 무시하고 반드시 발생시킨다 — 이벤트 UI·보상을 확인할 때 켠다")]
        [SerializeField] bool alwaysTrigger;

        [Tooltip("주사위를 굴린 결과를 콘솔에 남긴다 (확률을 조정할 때 켜면 편하다)")]
        [SerializeField] bool logRolls = true;

        [Tooltip("★ 같은 이벤트가 다시 후보가 되기까지 <b>건너뛸 추첨 횟수</b>. " +
                 "표(Info 시트) 기준 2 — «같은 이벤트는 발생 후 2회의 쿨타임을 갖습니다». " +
                 "0 이면 쿨타임 없음(바로 다시 뽑힐 수 있다)")]
        [Min(0)] [SerializeField] int eventCooldownRounds = 2;

        // ──────────────────────────────────────────────────────────────

        readonly List<EventDefinitionSO> _wave = new List<EventDefinitionSO>();
        readonly List<EventDefinitionSO> _private = new List<EventDefinitionSO>();
        readonly List<EventDefinitionSO> _habitat = new List<EventDefinitionSO>();

        WaveManager _waveManager;
        bool _hooked;
        float _privateNextAt;

        /// <summary>
        /// ★★ <b>직전 웨이브 단계</b> — <c>wave_end</c> 판정에 쓴다 (Ver013).
        ///
        /// 표가 못박은 정의: *"광폭화(Enrage)까지 모두 종료되어 <b>정비 시간으로 넘어가는
        /// 프레임</b>"*. 즉 «Preparation 으로 들어왔다» 만으로는 부족하다 — 판이 처음
        /// 시작될 때도 Preparation 이다. <b>어디서 왔는가</b> 를 봐야 한다.
        /// </summary>
        WavePhase _lastPhase = WavePhase.Idle;

        /// <summary>
        /// ★ <b>이벤트별 쿨타임</b> — Info 시트: *"같은 이벤트는 발생 후 2회의 쿨타임을
        /// 갖습니다(그 두 번은 후보에서 빠집니다)"*. 값은 «앞으로 몇 번 더 건너뛸지» 다.
        /// </summary>
        readonly Dictionary<int, int> _cooldown = new Dictionary<int, int>();

        /// <summary>이미 발동한 <c>habitat_contact</c> 이벤트 — 한 판에 서식지당 한 번뿐이다.</summary>
        readonly HashSet<int> _habitatFired = new HashSet<int>();

        /// <summary>지금 이벤트가 <b>시간으로</b> 끝나는 시각. 0 이면 «시간으로는 안 끝난다».</summary>
        float _endsAt;

        /// <summary>지금 이벤트에 걸린 지속시간(초). 0 이면 무제한. 표시용으로 열어 둔다.</summary>
        public float CurrentDurationSeconds { get; private set; }

        /// <summary>
        /// 지금 이벤트가 끝나기까지 남은 시간(초). 이벤트가 없거나 무제한이면 0.
        /// ★ 나중에 «남은 시간» 을 화면에 그릴 때 쓰는 통로다 — 지금은 아무도 안 본다.
        /// </summary>
        public float RemainingSeconds =>
            Current == null || _endsAt <= 0f ? 0f : Mathf.Max(0f, _endsAt - Time.time);

        /// <summary>지금 화면에 떠 있는 이벤트. null 이면 없다.</summary>
        public EventDefinitionSO Current { get; private set; }

        /// <summary>
        /// 유저가 고른 선택지. <b>null 이면 아직 본문 단계</b>(선택지 버튼을 보여주는 중)이고,
        /// 값이 있으면 <b>결과창 단계</b>다. 창이 이 하나로 두 모습을 낸다.
        /// </summary>
        public EventChoice CurrentChoice { get; private set; }

        /// <summary>이벤트가 열리거나(정의·선택) 닫힐 때(null) 알린다 — UI 가 구독한다.</summary>
        public event System.Action<EventDefinitionSO, EventChoice> OnEventChanged;

        /// <summary>이 판에서 다시 뽑지 않는 이벤트 (<c>repeatable = 0</c>).</summary>
        readonly HashSet<int> _finished = new HashSet<int>();

        // ------------------------------------------------------------------

        /// <summary>
        /// 씬에 이 컴포넌트가 없을 때의 <b>안전망</b>. 씬을 고치지 않고 기능을 얹는
        /// 이 프로젝트의 관례를 따른다(<c>CharacterPassives.EnsureOn</c> 과 같은 취지).
        /// ⚠ 다만 <b>인스펙터에서 확률을 만지려면 실물이 있어야</b> 하므로 씬의
        ///   <c>GameSystems</c> 에 MCP 로 <b>직접 붙여 두었다</b>.
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        static void EnsureOn()
        {
            if (Instance != null) return;
            if (Object.FindFirstObjectByType<EventService>() != null) return;

            GameObject host = GameObject.Find("GameSystems");
            if (host == null) host = new GameObject("GameSystems");
            host.AddComponent<EventService>();
        }

        void Awake()
        {
            Instance = this;
            LoadDefinitions();
        }

        void OnDestroy()
        {
            if (_waveManager != null) _waveManager.OnPhaseChanged -= HandlePhase;
            if (Instance == this) Instance = null;
        }

        void LoadDefinitions()
        {
            _wave.Clear();
            _private.Clear();
            _habitat.Clear();

            int skipped = 0;
            var all = Resources.LoadAll<EventDefinitionSO>("Events");
            for (int i = 0; i < all.Length; i++)
            {
                EventDefinitionSO def = all[i];
                if (def == null) continue;
                if (!def.IsUsable)
                {
                    // ⚠ 조용히 넘기지 않는다 — 표에 오타가 있으면 여기서만 보인다.
                    skipped++;
                    continue;
                }
                switch (def.Trigger)
                {
                    case EventTrigger.WaveEnd: _wave.Add(def); break;
                    case EventTrigger.PrivateTimer: _private.Add(def); break;
                    case EventTrigger.HabitatContact: _habitat.Add(def); break;
                }
            }
            Debug.Log($"[이벤트] 정의 로드 — 웨이브종료 {_wave.Count} · 비공개타이머 {_private.Count}" +
                      $" · 서식지접촉 {_habitat.Count}" + (skipped > 0 ? $" (⚠ 못 읽은 정의 {skipped}개)" : "") +
                      $" (웨이브 확률 {waveEventChancePercent}% · 비공개 {privateEventChancePercent}%)");
        }

        void Update()
        {
            HookWave();
            TickPrivateTimer();
            TickHabitatContact();
            TickDuration();

            // ★ Ver013 — 효과마다 다른 «남은 초» 를 센다. 창이 닫혀도 계속 돈다
            //   (표: 지속시간은 이벤트 종료와 무관한 절대 초다).
            EventRewardService.Tick();
        }

        /// <summary>
        /// 지속시간이 다 되면 이벤트를 끝낸다 (위 «이벤트 지속시간» 의 ★★).
        ///
        /// ★ <b>창이 열려 있는 동안에는 밀어 준다</b>(<see cref="pauseWhilePanelOpen"/>) —
        ///   시간을 세는 목적은 «지속 보정이 영원히 남는 것» 을 막는 것이지 유저를
        ///   재촉하는 것이 아니다. 창을 닫은 순간부터 남은 시간이 다시 흐른다.
        /// ⚠ <see cref="EndCurrent"/> 가 <see cref="EventRewardService.ClearAll"/> 을 부르므로
        ///   지속 보정도 여기서 같이 걷힌다 — 시간과 효과가 갈리지 않는다.
        /// </summary>
        void TickDuration()
        {
            if (Current == null || _endsAt <= 0f) return;

            if (pauseWhilePanelOpen && _panel != null && _panel.IsOpen)
            {
                _endsAt += Time.deltaTime;      // 읽는 동안은 안 깎인다
                return;
            }

            if (Time.time < _endsAt) return;
            CloseCurrent($"제한시간 {CurrentDurationSeconds:0}초 경과");
        }

        /// <summary>
        /// 이 이벤트에 걸릴 지속시간(초)을 정한다. 0 이면 «시간으로는 안 끝난다».
        /// 표를 쓸지 인스펙터 값을 쓸지는 <see cref="useTableDuration"/> 하나가 정한다 —
        /// 두 곳에서 반씩 정하면 «어느 쪽이 맞나» 를 매번 다시 물어야 한다.
        /// </summary>
        float DurationFor(EventDefinitionSO def)
        {
            // ★★ Ver013 — 표에서 event_value_01(창이 떠 있는 시간)이 <b>사라졌다</b>.
            //    지속시간은 이제 «효과마다»(reward_duration) 이고, 이 값은 «창을 얼마나
            //    띄워 둘지» 만 뜻한다. 그래서 표를 볼 것이 없고 인스펙터 값만 쓴다.
            //    ⚠ useTableDuration 을 켜 두면 «표 값이 없다» 는 뜻으로 0(무제한)이 된다 —
            //      즉 유저가 답할 때까지 창이 남는다. 그것이 Ver013 의 기본 동작이다.
            if (useTableDuration) return 0f;
            return def != null && def.Trigger == EventTrigger.WaveEnd
                ? waveEventDurationSeconds
                : privateEventDurationSeconds;
        }

        void HookWave()
        {
            if (_hooked) return;
            _waveManager = Object.FindFirstObjectByType<WaveManager>();
            if (_waveManager == null) return;
            _waveManager.OnPhaseChanged += HandlePhase;
            _hooked = true;
            _privateNextAt = Time.time + privateFirstDelaySeconds;
        }

        /// <summary>
        /// 웨이브 단계가 바뀌었다.
        ///
        /// ★ 발생 시점을 <b>전투 단계</b>로 잡았다 — 표의 조건이 <c>wave_start</c>
        ///   («웨이브 타이머 시작 시») 이고, 이 게임의 웨이브 타이머는 <b>첫 전투가 벌어질 때</b>
        ///   흐르기 시작한다(진군 중에는 멈춰 있다). 즉 «타이머 시작» = 전투 단계 진입이다.
        /// ★ 종료 조건 <c>wave_done</c> 은 <b>그 단계를 벗어나는 것</b>으로 본다 —
        ///   표의 «웨이브 타이머가 종료되면 이벤트를 종료 처리 합니다» 그대로다.
        /// </summary>
        void HandlePhase(WavePhase phase)
        {
            WavePhase from = _lastPhase;
            _lastPhase = phase;

            // ★★ Ver013 — <b>웨이브가 완전히 끝난 순간</b>에 발동한다.
            //    표(Condition 시트): *"웨이브가 완전히 끝난 순간(전투 → 광폭화까지 모두
            //    종료되어 정비 시간으로 넘어가는 프레임)"*.
            //
            //    ⚠⚠ Ver012 구현은 <b>전투 단계 진입</b>(Battle)에 발동했다 — 그때의 표가
            //      «웨이브 타이머 시작 시» 였기 때문이다. Ver013 이 그 조건을 뒤집었으므로
            //      여기가 바뀌는 자리다.
            //    ★ <b>어디서 왔는지</b> 를 본다 — 판이 처음 시작될 때도 Preparation 이라
            //      «들어왔다» 만 보면 0웨이브에 이벤트가 뜬다.
            if (phase == WavePhase.Preparation &&
                (from == WavePhase.Battle || from == WavePhase.Enrage))
            {
                TryRoll(_wave, waveEventChancePercent, "웨이브종료");
                return;
            }

            // 패배·승리에서는 창을 치운다 — 결과 화면 위에 이벤트가 겹치면 안 된다.
            if (phase == WavePhase.Defeat || phase == WavePhase.Victory)
                EndCurrent("판 종료");
        }

        /// <summary>
        /// ★★ <c>habitat_contact</c> — <b>서식지에 처음 닿는 순간</b> 100% 발동 (Ver013 신설).
        ///
        /// 표(Condition 시트): *"trigger_value 에 적힌 중립 몬스터의 서식지 타일에 캐릭터가
        /// <b>최초로</b> 인접한 순간 100% 발동합니다. 확률·가중치를 쓰지 않으며 한 판에
        /// 서식지당 한 번만 발동합니다"*.
        ///
        /// <b>어떻게 «인접» 을 재나</b> — 서식지는 (중심 칸 · 반지름) 으로 완전히 결정되므로
        /// (<see cref="Units.NeutralHabitat"/>), <b>중심에서 반지름 + 1타일</b> 안에 캐릭터가
        /// 있으면 닿은 것으로 본다. 칸 목록을 훑지 않는 이유는 서식지가 수천 칸이고
        /// 저장 코드도 같은 이유로 «칸을 담지 않는다» 를 택했기 때문이다.
        ///
        /// ⚠ <b>소환수는 세지 않는다</b> — «캐릭터가» 라는 표의 문장이고, 골렘이 먼저 닿아
        ///   이벤트가 뜨면 «내가 안 갔는데» 가 된다.
        /// ⚠ 이미 창이 떠 있으면 <b>미룬다</b>(발동 표시를 남기지 않는다) — 다음 프레임에
        ///   다시 본다. 여기서 <see cref="_habitatFired"/> 에 넣어버리면 «한 번뿐» 이라
        ///   그 이벤트를 영영 못 본다.
        /// </summary>
        void TickHabitatContact()
        {
            if (!eventsEnabled || _habitat.Count == 0 || Current != null) return;

            var all = Combat.UnitRegistry.All;

            for (int e = 0; e < _habitat.Count; e++)
            {
                EventDefinitionSO def = _habitat[e];
                if (_habitatFired.Contains(def.eventId)) continue;
                if (def.triggerValue == 0) continue;

                // ① 그 종류의 중립 몬스터 중 서식지를 그린 개체를 찾는다.
                for (int i = 0; i < all.Count; i++)
                {
                    if (!(all[i] is Units.NeutralMonsterUnit mon) || mon.Definition == null) continue;
                    if (mon.Definition.monId != def.triggerValue) continue;

                    var habitat = mon.GetComponent<Units.NeutralHabitat>();
                    if (habitat == null || !habitat.HasPainted) continue;

                    Vector3 center = habitat.transform.position;
                    float reach = mon.Definition.habitatRadiusTiles + 1f;
                    float reachSq = reach * reach;

                    // ② 캐릭터가 그 안에 들어와 있는가.
                    for (int c = 0; c < all.Count; c++)
                    {
                        if (!(all[c] is Units.CharacterUnit ch) || !ch.IsAlive || ch.IsSummoned) continue;
                        if ((ch.transform.position - center).sqrMagnitude > reachSq) continue;

                        _habitatFired.Add(def.eventId);
                        if (logRolls)
                            Debug.Log($"[이벤트] 서식지 접촉 — {mon.Definition.DisplayName}" +
                                      $"(id {def.triggerValue}) 에 {ch.DisplayName} 이(가) 닿았다 → {def.DisplayName}");
                        Begin(def);
                        return;                 // 한 프레임에 하나만
                    }
                }
            }
        }

        void TickPrivateTimer()
        {
            if (!_hooked || _privateNextAt <= 0f || Time.time < _privateNextAt) return;
            _privateNextAt = Time.time + privateTimerSeconds;
            TryRoll(_private, privateEventChancePercent, "비공개");
        }

        /// <summary>
        /// ① 확률로 굴리고 ② 통과하면 가중치로 하나 뽑아 ③ 화면에 띄운다.
        /// 이미 이벤트가 떠 있으면 <b>새로 뽑지 않는다</b> — 두 장이 겹치면 어느 것에
        /// 답한 것인지 알 수 없다(보스 스킬 동시발동 방지와 같은 판단).
        /// </summary>
        void TryRoll(List<EventDefinitionSO> pool, int chancePercent, string label)
        {
            if (!eventsEnabled || pool.Count == 0) return;
            if (Current != null)
            {
                if (logRolls) Debug.Log($"[이벤트] {label} — 이미 진행 중이라 건너뜀 ({Current.DisplayName})");
                return;
            }

            // ★ <b>«추첨 한 회» 는 여기다</b> — 쿨타임(표: «발생 후 2회») 을 이 자리에서 깎는다.
            //   Begin 에서 깎으면 서식지 접촉으로 창이 떠도 웨이브 쿨타임이 줄어든다
            //   («2회» 가 뜻하는 회차와 어긋난다).
            TickCooldowns();

            int roll = Random.Range(0, 100);
            bool pass = alwaysTrigger || roll < chancePercent;
            if (logRolls)
                Debug.Log($"[이벤트] {label} 주사위 {roll} < {chancePercent}% → " +
                          (pass ? "발생" : "없음") + (alwaysTrigger ? " (항상 발생 켜짐)" : ""));
            if (!pass) return;

            EventDefinitionSO pick = PickWeighted(pool);
            if (pick != null) Begin(pick);
        }

        /// <summary>
        /// 가중치(<c>weight</c>) 비율로 하나 뽑는다.
        ///
        /// <b>후보에서 빠지는 것 둘</b> (Info 시트):
        ///   · <see cref="_finished"/> — <c>repeatable = 0</c> 이라 한 판에 한 번뿐인 것
        ///   · <see cref="_cooldown"/> — *"같은 이벤트는 발생 후 2회의 쿨타임을 갖습니다"*
        /// </summary>
        EventDefinitionSO PickWeighted(List<EventDefinitionSO> pool)
        {
            int total = 0;
            for (int i = 0; i < pool.Count; i++)
                if (Eligible(pool[i])) total += pool[i].Weight;
            if (total <= 0) return null;

            int r = Random.Range(0, total);
            for (int i = 0; i < pool.Count; i++)
            {
                if (!Eligible(pool[i])) continue;
                r -= pool[i].Weight;
                if (r < 0) return pool[i];
            }
            return null;
        }

        /// <summary>지금 뽑힐 수 있는 이벤트인가 (위 <see cref="PickWeighted"/> 의 둘).</summary>
        bool Eligible(EventDefinitionSO def)
        {
            if (def == null || def.Weight <= 0) return false;
            if (_finished.Contains(def.eventId)) return false;
            return !_cooldown.TryGetValue(def.eventId, out int left) || left <= 0;
        }

        /// <summary>
        /// 이벤트 하나가 끝났으니 <b>쿨타임을 매긴다</b>.
        ///
        /// ★ 쿨타임은 «앞으로 몇 번의 추첨에서 빠질지» 다(<see cref="eventCooldownRounds"/>).
        ///   초로 재지 않는 이유 — 표가 «2회» 라고 <b>횟수</b>로 적었고, 웨이브 길이는
        ///   판마다 다르기 때문이다.
        /// ⚠ <c>repeatable = 0</c> 이면 쿨타임이 아니라 <b>영구 제외</b>다.
        /// </summary>
        void MarkUsed(EventDefinitionSO def)
        {
            if (def == null) return;

            if (!def.repeatable)
            {
                _finished.Add(def.eventId);
                return;
            }
            _cooldown[def.eventId] = Mathf.Max(0, eventCooldownRounds);
        }

        /// <summary>추첨을 한 번 돌렸으니 <b>모든 쿨타임을 하나씩 깎는다</b>.</summary>
        void TickCooldowns()
        {
            if (_cooldown.Count == 0) return;
            var keys = new List<int>(_cooldown.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                int left = _cooldown[keys[i]] - 1;
                if (left <= 0) _cooldown.Remove(keys[i]);
                else _cooldown[keys[i]] = left;
            }
        }

        // ------------------------------------------------------------------
        //  진행
        // ------------------------------------------------------------------

        /// <summary>
        /// 이벤트를 시작한다 — 배경 + 본문(<c>event_script</c>) + 선택지 버튼을 띄운다.
        ///
        /// ★ Ver013 부터 «첫 줄» 이라는 개념이 없다. 본문은 <b>한 묶음으로 한 번에</b> 나가고
        ///   분기는 선택지가 담당한다(Info 시트 «화면 흐름» 2~3번).
        /// </summary>
        public void Begin(EventDefinitionSO def)
        {
            if (def == null) return;

            Current = def;
            CurrentChoice = null;               // 아직 아무것도 안 골랐다 = 본문 단계
            CurrentDurationSeconds = DurationFor(def);
            _endsAt = CurrentDurationSeconds > 0f ? Time.time + CurrentDurationSeconds : 0f;

            MarkUsed(def);

            UI.HudLog.Add($"<b>[사건]</b> {def.DisplayName}", UI.HudLogKind.Good);
            OnEventChanged?.Invoke(Current, CurrentChoice);
            ShowPanel();
        }

        /// <summary>
        /// ★★ <b>선택지를 골랐다</b> — 보상을 걸고 <b>결과창</b>으로 넘어간다 (Ver013).
        ///
        /// <paramref name="index"/> 는 <c>choice_order</c> 순서의 번호다(0 = 첫째).
        ///
        /// <b>순서가 있다</b> (Info 시트 4번): ① 보상 적용 → ② 결과 대사·효과 요약 표시.
        /// 보상을 먼저 걸어야 결과창의 «효과 요약» 과 실제로 걸린 것이 어긋나지 않는다.
        ///
        /// ⚠ 이벤트는 <b>여기서 끝나지 않는다</b> — 유저가 결과창을 닫을 때 끝난다
        ///   (<see cref="CloseCurrent"/>). 표: *"결과창을 닫으면 이벤트가 종료됩니다"*.
        /// ⚠ 이미 결과창이면 <b>다시 고를 수 없다</b> — 두 번 누르면 보상이 두 번 걸린다.
        /// </summary>
        public void Choose(int index)
        {
            if (Current == null || CurrentChoice != null) return;

            EventChoice choice = Current.ChoiceAt(index);
            if (choice == null) return;

            ApplyReward(choice.rewardType01, choice.rewardValue01, choice.rewardDuration01);
            if (choice.HasSecondReward)
                ApplyReward(choice.rewardType02, choice.rewardValue02, choice.rewardDuration02);

            CurrentChoice = choice;
            OnEventChanged?.Invoke(Current, CurrentChoice);
            ShowPanel();
        }

        void ApplyReward(string type, int value, int duration)
        {
            if (string.IsNullOrWhiteSpace(type)) return;

            string log = EventRewardService.Apply(type, value, duration);
            if (!string.IsNullOrEmpty(log))
                UI.HudLog.Add($"[사건] {Current.DisplayName} — {log}", UI.HudLogKind.Good);
        }

        /// <summary>
        /// 결과창을 닫아 <b>이벤트를 끝낸다</b>.
        ///
        /// ⚠⚠ <b>지속 효과는 걷지 않는다</b> — Ver013 의 가장 중요한 변화다. 효과마다
        ///   «몇 초» 가 표에 적혀 있고(<c>reward_duration</c>), 그 시간은 <b>이벤트가 끝난
        ///   뒤에도 계속 흐른다</b>(Info 시트: 웨이브 이벤트의 240초는 «두 웨이브 분량»).
        ///   여기서 <c>ClearAll</c> 을 부르면 표가 적은 시간이 <b>전부 무의미해진다</b>.
        ///   되돌리는 것은 <see cref="EventRewardService.Tick"/> 이 초로 한다.
        /// </summary>
        public void CloseCurrent(string why)
        {
            if (Current == null) return;
            if (logRolls) Debug.Log($"[이벤트] {Current.DisplayName} 종료 — {why}");

            Current = null;
            CurrentChoice = null;
            _endsAt = 0f;
            CurrentDurationSeconds = 0f;
            OnEventChanged?.Invoke(null, null);
            ShowPanel();
        }

        /// <summary>
        /// 예전 이름 — «창을 치운다» 는 뜻으로 부르던 곳들을 위해 남긴다.
        /// ⚠ 이제 지속 효과를 걷지 않는다(위 <see cref="CloseCurrent"/> 의 ⚠⚠).
        /// </summary>
        public void EndCurrent(string why) => CloseCurrent(why);

        /// <summary>
        /// ★ <b>판을 갈아엎을 때</b>만 쓰는 통로 — 지속 효과까지 전부 되돌린다.
        /// 「게임 재시작」·로비 복귀가 부른다(<c>SettingsPanel.RestartRun</c>).
        /// </summary>
        public void ClearRun()
        {
            CloseCurrent("판 초기화");
            EventRewardService.ClearAll();
            _finished.Clear();
            _cooldown.Clear();
            _habitatFired.Clear();
            _lastPhase = WavePhase.Idle;
        }

        /// <summary>
        /// ★★ <b>화면에 띄운다</b> (2026-08-21 · 유저 리포트: *"이벤트 지금 적용 되어도
        /// 시각적으로 확인이 불가"*).
        ///
        /// <b>왜 이벤트(<see cref="OnEventChanged"/>)만으로는 안 됐나</b> — 이벤트 창
        /// (<c>UI_Root/HUD_Event</c>)은 씬에 <b>비활성</b>으로 저장돼 있다. 유니티는 비활성
        /// 오브젝트의 <c>Awake</c>·<c>OnEnable</c>·<c>Update</c> 를 <b>부르지 않으므로</b>,
        /// 창이 «스스로 구독» 하는 코드는 영원히 실행되지 않았다. 표·확률·보상은 전부 정상으로
        /// 돌고 있었고 <b>보여주는 통로 하나만</b> 죽어 있었다.
        ///
        /// → 여기서 <b>밀어 넣는다</b>. 비활성 오브젝트도 <b>참조로는</b> 부를 수 있다.
        ///   찾을 때 <see cref="FindObjectsInactive.Include"/> 를 반드시 켜야 한다 —
        ///   기본값(Exclude)으로는 꺼진 창을 <b>못 찾는다</b>(이 버그의 두 번째 함정).
        ///
        /// ★ <see cref="OnEventChanged"/> 는 <b>그대로 남긴다</b> — 다른 구독자(로그·연출)가
        ///   붙을 자리이고, 창을 직접 부르는 것과 성격이 다르다.
        /// ⚠ 창을 <b>캐시</b>한다(찾기는 비용이 있다). 파괴되면 다음 호출에 다시 찾는다.
        /// </summary>
        void ShowPanel()
        {
            if (_panel == null)
                _panel = Object.FindFirstObjectByType<UI.EventPanel>(FindObjectsInactive.Include);

            if (_panel == null)
            {
                if (logRolls)
                    Debug.LogWarning("[이벤트] 이벤트 창(UI_Root/HUD_Event)을 찾지 못했습니다 — " +
                                     "표는 정상으로 돌지만 화면에는 아무것도 안 뜹니다.", this);
                return;
            }

            _panel.Present(Current, CurrentChoice);
        }

        UI.EventPanel _panel;

        /// <summary>디버그용 — 확률을 무시하고 웨이브 이벤트 하나를 띄운다(인스펙터 우클릭 메뉴).</summary>
        [ContextMenu("웨이브 이벤트 하나 발생시키기")]
        void DebugFireWave()
        {
            EventDefinitionSO pick = PickWeighted(_wave);
            if (pick != null) Begin(pick);
        }
    }
}
