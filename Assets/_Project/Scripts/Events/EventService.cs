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

        [Header("스위치")]
        [Tooltip("끄면 이벤트가 하나도 뜨지 않는다. 다른 것을 검증할 때 쓴다")]
        [SerializeField] bool eventsEnabled = true;

        [Tooltip("★ 확률을 무시하고 반드시 발생시킨다 — 이벤트 UI·보상을 확인할 때 켠다")]
        [SerializeField] bool alwaysTrigger;

        [Tooltip("주사위를 굴린 결과를 콘솔에 남긴다 (확률을 조정할 때 켜면 편하다)")]
        [SerializeField] bool logRolls = true;

        // ──────────────────────────────────────────────────────────────

        readonly List<EventDefinitionSO> _wave = new List<EventDefinitionSO>();
        readonly List<EventDefinitionSO> _private = new List<EventDefinitionSO>();
        readonly List<EventDefinitionSO> _raid = new List<EventDefinitionSO>();

        WaveManager _waveManager;
        bool _hooked;
        float _privateNextAt;

        /// <summary>지금 화면에 떠 있는 이벤트. null 이면 없다.</summary>
        public EventDefinitionSO Current { get; private set; }

        /// <summary>지금 보여주고 있는 대사 줄.</summary>
        public EventLine CurrentLine { get; private set; }

        /// <summary>이벤트가 열리거나(정의·줄) 닫힐 때(null) 알린다 — UI 가 구독한다.</summary>
        public event System.Action<EventDefinitionSO, EventLine> OnEventChanged;

        /// <summary>이 판에서 이미 끝난 이벤트(재수락 불가 <c>500002</c>). 다시 뽑지 않는다.</summary>
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
            _raid.Clear();

            var all = Resources.LoadAll<EventDefinitionSO>("Events");
            for (int i = 0; i < all.Length; i++)
            {
                EventDefinitionSO def = all[i];
                if (def == null || !def.IsUsable) continue;
                switch (def.Kind)
                {
                    case EventKind.Wave: _wave.Add(def); break;
                    case EventKind.Private: _private.Add(def); break;
                    case EventKind.Raid: _raid.Add(def); break;
                }
            }
            Debug.Log($"[이벤트] 정의 로드 — 웨이브 {_wave.Count} · 비공개 {_private.Count} · 토벌 {_raid.Count}" +
                      $" (웨이브 확률 {waveEventChancePercent}% · 비공개 {privateEventChancePercent}%)");
        }

        void Update()
        {
            HookWave();
            TickPrivateTimer();
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
            if (phase == WavePhase.Battle)
            {
                TryRoll(_wave, waveEventChancePercent, "웨이브");
                return;
            }

            if (phase == WavePhase.Preparation || phase == WavePhase.Marching ||
                phase == WavePhase.Defeat || phase == WavePhase.Victory)
                EndCurrent("웨이브 종료");
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
                if (logRolls) Debug.Log($"[이벤트] {label} — 이미 진행 중이라 건너뜀 ({Current.eventName})");
                return;
            }

            int roll = Random.Range(0, 100);
            bool pass = alwaysTrigger || roll < chancePercent;
            if (logRolls)
                Debug.Log($"[이벤트] {label} 주사위 {roll} < {chancePercent}% → " +
                          (pass ? "발생" : "없음") + (alwaysTrigger ? " (항상 발생 켜짐)" : ""));
            if (!pass) return;

            EventDefinitionSO pick = PickWeighted(pool);
            if (pick != null) Begin(pick);
        }

        /// <summary>가중치(<c>event_value_02</c>) 비율로 하나 뽑는다. 끝난 이벤트는 제외한다.</summary>
        EventDefinitionSO PickWeighted(List<EventDefinitionSO> pool)
        {
            int total = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (_finished.Contains(pool[i].eventId)) continue;
                total += pool[i].Weight;
            }
            if (total <= 0) return null;

            int r = Random.Range(0, total);
            for (int i = 0; i < pool.Count; i++)
            {
                if (_finished.Contains(pool[i].eventId)) continue;
                r -= pool[i].Weight;
                if (r < 0) return pool[i];
            }
            return null;
        }

        // ------------------------------------------------------------------
        //  진행
        // ------------------------------------------------------------------

        /// <summary>이벤트를 시작한다 — 첫 줄(<c>active</c>)을 띄운다.</summary>
        public void Begin(EventDefinitionSO def)
        {
            if (def == null) return;
            Current = def;
            CurrentLine = def.FirstLine();
            UI.HudLog.Add($"<b>[사건]</b> {def.eventName}", UI.HudLogKind.Good);
            OnEventChanged?.Invoke(Current, CurrentLine);
        }

        /// <summary>
        /// 지금 줄에서 <b>다음으로</b> 넘어간다.
        /// <paramref name="choice"/> 는 선택지 번호(0 = 첫째 · 1 = 둘째)다.
        /// 선택지가 없는 줄이면 <c>next_dialogue_id_01</c> 로 간다.
        /// </summary>
        public void Advance(int choice)
        {
            if (Current == null || CurrentLine == null) return;

            EventLine line = CurrentLine;

            // ★ 확률로 갈리는 줄은 <b>유저 입력을 안 본다</b> — 여기서 굴린다.
            if (line.IsRandom)
                choice = Random.Range(0, 100) < line.rewardProceedValue01 ? 0 : 1;

            bool second = choice == 1 && line.nextDialogueId02 != 0;
            string reward = second ? line.rewardValue03 : line.rewardValue01;
            int value = second ? line.rewardValue04 : line.rewardValue02;

            if (!string.IsNullOrWhiteSpace(reward))
            {
                string log = EventRewardService.Apply(reward, value);
                if (!string.IsNullOrEmpty(log))
                    UI.HudLog.Add($"[사건] {Current.eventName} — {log}", UI.HudLogKind.Good);
            }

            int nextId = second ? line.nextDialogueId02 : line.nextDialogueId01;
            EventLine next = nextId != 0 ? Current.Find(nextId) : null;

            if (next == null)
            {
                EndCurrent("대사 끝");
                return;
            }

            CurrentLine = next;
            OnEventChanged?.Invoke(Current, CurrentLine);

            // 재수락 불가(500002)로 끝나는 줄이면 이 판에서 다시 뽑지 않는다.
            if (next.Ends && next.endSwitch == 500002) _finished.Add(Current.eventId);
        }

        /// <summary>창을 닫고 지속 보정을 되돌린다.</summary>
        public void EndCurrent(string why)
        {
            if (Current == null) return;
            if (logRolls) Debug.Log($"[이벤트] {Current.eventName} 종료 — {why}");
            Current = null;
            CurrentLine = null;
            EventRewardService.ClearAll();
            OnEventChanged?.Invoke(null, null);
        }

        /// <summary>디버그용 — 확률을 무시하고 웨이브 이벤트 하나를 띄운다(인스펙터 우클릭 메뉴).</summary>
        [ContextMenu("웨이브 이벤트 하나 발생시키기")]
        void DebugFireWave()
        {
            EventDefinitionSO pick = PickWeighted(_wave);
            if (pick != null) Begin(pick);
        }
    }
}
