using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Relics;
using LastSanctuary.UI;
using LastSanctuary.Units;
using LastSanctuary.Wave;

namespace LastSanctuary.Help
{
    /// <summary>
    /// ★★★ <b>도움말 조언자</b> (2026-08-24 신설 · 유저 지시:
    /// *"듀토리얼 용 UI 캔버스 만들어서 도움말 방식으로 듀토리얼 구성해줘"* ·
    /// *"최초로 해당 기능을 눌렀을때 나타나게"*).
    ///
    /// <b>하는 일은 셋뿐이다</b>
    /// <code>
    ///   ① 계기를 듣는다        — 이미 있는 이벤트에 붙거나(대부분), 못 붙는 것만 주기적으로 본다
    ///   ② 처음인지 판단한다     — 이미 본 항목은 다시 띄우지 않는다(show_once)
    ///   ③ 카드에 넘긴다        — 실제로 보여주고 멈추는 일은 <see cref="HelpCardPanel"/> 이 한다
    /// </code>
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  ★★ 왜 «버튼» 이 아니라 «결과» 에 거는가
    /// ══════════════════════════════════════════════════════════════════
    /// 유저 지시는 «최초로 해당 기능을 눌렀을 때» 다. 그런데 이 게임에서 한 기능을 누르는
    /// 통로는 여럿이다 — 액션 버튼 · 창 안의 버튼 · 단축키 · 로스터의 우클릭. 버튼마다
    /// 세면 <b>반드시 하나를 빠뜨리고</b>, 통로가 늘 때 조용히 어긋난다.
    /// 그래서 <b>기능이 실제로 일어난 자리</b>(부대가 만들어졌다 · 강화가 됐다)를 듣는다 —
    /// 통로가 몇이든 한 번만 잡히고, <b>다른 파일을 고치지 않아도</b> 된다.
    /// (<see cref="HudExclusive"/> 가 «규칙을 한 곳에 모은다» 는 같은 판단으로 생겼다.)
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  ⚠ 이 파일이 <b>다른 시스템을 한 줄도 고치지 않는다</b>
    /// ══════════════════════════════════════════════════════════════════
    /// 계기 23개가 전부 <b>이미 있는 public 이벤트</b>거나 <b>이미 있는 public 상태</b>다.
    /// 도움말 때문에 전투·웨이브·유물 코드에 <c>HelpService.Fire(...)</c> 를 심으면,
    /// 그 시스템들이 도움말을 <b>알아야</b> 하게 된다. 튜토리얼은 <b>얹는 한 겹</b>이어야 한다
    /// (진행상황 UI-51-8 이 세운 방향 그대로다).
    ///
    /// ⚠ <b>기억은 PlayerPrefs 에 남는다</b> — 판을 다시 시작해도 이미 읽은 조언은 다시 뜨지
    ///   않는다(문명과 같다). 다시 보고 싶으면 <see cref="ForgetAll"/> — 환경 설정에서 부른다.
    /// </summary>
    public class HelpService : MonoBehaviour
    {
        /// <summary>이미 본 항목을 적어 두는 자리. 값은 <c>help_id</c> 를 <c>|</c> 로 이은 것이다.</summary>
        public const string PrefsKey = "LastSanctuary.help.seen.v1";

        static HelpService _instance;

        public static HelpService Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<HelpService>(FindObjectsInactive.Include);
                return _instance;
            }
        }

        [Header("동작")]
        [Tooltip("끄면 조언 카드가 아예 뜨지 않는다. 백과(F1)는 그대로 열린다")]
        [SerializeField] bool enableAdviceCards = true;

        [Tooltip("계기가 걸린 뒤 카드를 띄우기까지 기다리는 시간(초). " +
                 "0 이면 «누른 그 프레임» 에 카드가 덮쳐서 방금 누른 버튼이 무엇이었는지 놓친다")]
        [Min(0f)] [SerializeField] float showDelaySeconds = 0.45f;

        [Tooltip("이벤트로 못 듣는 계기(보스 등장 · 침식 50 · 자동 저장 · 배속 · 발굴 표식 · " +
                 "건설 모드)를 몇 초마다 확인하는가. ★ 아직 안 뜬 것만 본다 — 다 뜨면 검사가 멈춘다")]
        [Min(0.1f)] [SerializeField] float pollInterval = 0.5f;

        [Tooltip("★★ 판이 시작되고 이 시간(초) 안에 걸린 계기는 <b>버린다</b> — " +
                 "스포너가 캐릭터를 만들고 부대를 묶고 전술을 넣는 것이 «유저가 누른 것» 으로 " +
                 "잡히는 것을 막는다. 실측으로 필요해진 값이다(아래 ★★★ 참조)")]
        [Min(0f)] [SerializeField] float startupGraceSeconds = 1.5f;

        [Tooltip("무엇이 왜 떴는지/왜 안 떴는지 콘솔에 남긴다 (조언이 안 뜰 때 켜서 본다)")]
        [SerializeField] bool logTriggers;

        HelpTableSO _table;
        readonly HashSet<string> _seen = new HashSet<string>();
        readonly List<HelpEntry> _queue = new List<HelpEntry>();
        readonly List<HelpEntry> _scratch = new List<HelpEntry>();

        /// <summary>주기 검사가 아직 필요한 계기들. 뜨고 나면 <b>빠진다</b>.</summary>
        readonly List<HelpTrigger> _polled = new List<HelpTrigger>();

        float _nextPoll;
        float _showAt;
        bool _hooked;

        /// <summary>이 시각까지는 «유저가 누른 것» 계기를 믿지 않는다 — <see cref="IsStartupNoise"/>.</summary>
        float _graceUntil;

        WaveManager _wave;
        GameSpeedPanel _speed;

        /// <summary>
        /// 씬에 이 컴포넌트가 없어도 스스로 붙는다 — <see cref="HudHotkeys.EnsureOn"/> 과 같은 관례.
        /// ⚠ 인스펙터에서 켜고 끄려면 실물이 있어야 하므로 <c>GameSystems</c> 에 MCP 로도 붙여 뒀다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        static void EnsureOn()
        {
            if (FindAnyObjectByType<HelpService>(FindObjectsInactive.Include) != null) return;

            GameObject host = GameObject.Find("GameSystems");
            if (host == null) return;      // 게임 씬이 아니다(로비·오프닝) — 조언자가 필요 없다
            host.AddComponent<HelpService>();
        }

        void Awake()
        {
            _instance = this;
            _table = HelpTableSO.Load();
            LoadSeen();
            CollectPolled();
            _graceUntil = Time.unscaledTime + startupGraceSeconds;

            // ★ «방금 저장됐다» 를 알려면 <b>지금 상태</b>를 적어 둬야 한다 — SaveChanged() 의 ★★.
            _saveLabelAtStart = Save.SaveService.SavedAtLabel();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            Unhook();
        }

        void OnEnable() => Hook();
        void OnDisable() => Unhook();

        // ==================================================================
        // 계기 — 이미 있는 이벤트에 붙는다
        // ==================================================================

        /// <summary>
        /// ⚠ <b>인스턴스 이벤트는 그 인스턴스가 생긴 뒤에야 붙는다.</b> 서비스들은
        /// <c>Awake</c> 순서가 정해져 있지 않아서, 여기서 한 번 실패하면 영영 안 붙는다 —
        /// 그래서 <see cref="Update"/> 가 «아직 안 붙은 것» 을 계속 다시 시도한다
        /// (<c>_hooked</c> 는 <b>정적 이벤트</b>에만 쓰는 표시다).
        /// </summary>
        void Hook()
        {
            if (_hooked) return;
            _hooked = true;

            DamageableUnit.OnAnyDied += OnAnyDied;
            DamageableUnit.OnAnyMissed += OnAnyMissed;
            ErosionService.OnMentalErrorTriggered += OnMentalError;
            HeroAwakeningService.OnAwakened += OnAwakened;
            CharacterTactics.OnAnyOrderChanged += OnTacticsChanged;
        }

        void Unhook()
        {
            if (!_hooked) return;
            _hooked = false;

            DamageableUnit.OnAnyDied -= OnAnyDied;
            DamageableUnit.OnAnyMissed -= OnAnyMissed;
            ErosionService.OnMentalErrorTriggered -= OnMentalError;
            HeroAwakeningService.OnAwakened -= OnAwakened;
            CharacterTactics.OnAnyOrderChanged -= OnTacticsChanged;

            UnhookInstances();
        }

        // ── 인스턴스 이벤트 ────────────────────────────────────────────────

        WaveManager _hookedWave;
        Resource.ResourceManager _hookedEnergy;
        CharacterCreationService _hookedCreation;
        CharacterUpgradeService _hookedUpgrade;
        SquadService _hookedSquad;
        RallyPointService _hookedRally;
        RelicInventory _hookedRelic;
        Events.EventService _hookedEvent;
        EpicSubjugationService _hookedEpic;

        /// <summary>아직 못 붙은 인스턴스 이벤트를 붙인다. 매 프레임 불려도 싸다(참조 비교뿐).</summary>
        void HookInstances()
        {
            if (_hookedWave == null)
            {
                _wave = _wave != null ? _wave : FindAnyObjectByType<WaveManager>();
                if (_wave != null)
                {
                    _hookedWave = _wave;
                    _wave.OnPhaseChanged += OnPhaseChanged;
                    CatchUpWave();
                }
            }

            var energy = Resource.ResourceManager.Instance;
            if (energy != null && _hookedEnergy != energy)
            {
                _hookedEnergy = energy;
                energy.OnEnergyChanged += OnEnergyChanged;
            }

            var creation = CharacterCreationService.Instance;
            if (creation != null && _hookedCreation != creation)
            {
                _hookedCreation = creation;
                creation.OnCreated += OnCharacterCreated;
            }

            var upgrade = CharacterUpgradeService.Instance;
            if (upgrade != null && _hookedUpgrade != upgrade)
            {
                _hookedUpgrade = upgrade;
                upgrade.OnUpgraded += OnUpgraded;
            }

            var squad = SquadService.Instance;
            if (squad != null && _hookedSquad != squad)
            {
                _hookedSquad = squad;
                squad.OnSquadsChanged += OnSquadsChanged;
            }

            var rally = RallyPointService.Instance;
            if (rally != null && _hookedRally != rally)
            {
                _hookedRally = rally;
                rally.OnPointsChanged += OnRallyChanged;
            }

            var relic = RelicInventory.Instance;
            if (relic != null && _hookedRelic != relic)
            {
                _hookedRelic = relic;
                relic.OnChanged += OnRelicChanged;
            }

            var evt = Events.EventService.Instance;
            if (evt != null && _hookedEvent != evt)
            {
                _hookedEvent = evt;
                evt.OnEventChanged += OnEventChanged;
            }

            var epic = EpicSubjugationService.Instance;
            if (epic != null && _hookedEpic != epic)
            {
                _hookedEpic = epic;
                epic.OnChanged += OnEpicChanged;
            }
        }

        void UnhookInstances()
        {
            if (_hookedWave != null) _hookedWave.OnPhaseChanged -= OnPhaseChanged;
            if (_hookedEnergy != null) _hookedEnergy.OnEnergyChanged -= OnEnergyChanged;
            if (_hookedCreation != null) _hookedCreation.OnCreated -= OnCharacterCreated;
            if (_hookedUpgrade != null) _hookedUpgrade.OnUpgraded -= OnUpgraded;
            if (_hookedSquad != null) _hookedSquad.OnSquadsChanged -= OnSquadsChanged;
            if (_hookedRally != null) _hookedRally.OnPointsChanged -= OnRallyChanged;
            if (_hookedRelic != null) _hookedRelic.OnChanged -= OnRelicChanged;
            if (_hookedEvent != null) _hookedEvent.OnEventChanged -= OnEventChanged;
            if (_hookedEpic != null) _hookedEpic.OnChanged -= OnEpicChanged;

            _hookedWave = null; _hookedEnergy = null; _hookedCreation = null;
            _hookedUpgrade = null; _hookedSquad = null; _hookedRally = null;
            _hookedRelic = null; _hookedEvent = null; _hookedEpic = null;
        }

        // ── 손잡이들 ──────────────────────────────────────────────────────

        /// <summary>
        /// ★★★ <b>구독하기 전에 이미 지나간 단계를 따라잡는다</b> (2026-08-24 · 실측으로 찾은 구멍).
        ///
        /// <see cref="WaveManager.Start"/> 가 곧바로 <c>StartGame()</c> → <c>BeginPreparation()</c>
        /// 을 부르므로 <b>첫 정비 단계 이벤트는 «Start 단계»에서 이미 터진다</b>.
        /// 이 서비스는 <see cref="Update"/> 에서 구독하니 <b>그 한 번을 영영 놓친다</b> —
        /// 즉 가장 먼저 떠야 하는 두 장(「성역과 성역」·「웨이브와 정비 시간」)이
        /// <b>아무 소리 없이 안 뜬다</b>. 이벤트를 «듣는» 방식의 유일한 약점이 이것이다.
        ///
        /// ★ 그래서 붙는 즉시 <b>지금 단계를 한 번 평가</b>한다. 계기는 «처음 한 번» 이므로
        ///   따라잡아도 중복이 나지 않는다(<see cref="IsSeen"/> 가 막는다).
        /// ⚠ <b>웨이브 단계만</b> 따라잡는다 — 부대·집결지처럼 «유저가 누른 것» 은 따라잡으면
        ///   안 된다. 스포너가 시작할 때 부대를 자동으로 묶으므로(<c>UnitSpawner.
        ///   AutoSquadInitialCharacters</c>) 그것까지 따라잡으면 판이 시작하자마자
        ///   카드 석 장이 줄을 선다. 그 계기들은 <b>진짜로 유저가 만졌을 때</b>만 걸려야 한다.
        /// </summary>
        void CatchUpWave()
        {
            if (_wave == null) return;
            OnPhaseChanged(_wave.Phase);
            if (logTriggers) Debug.Log($"[도움말] 따라잡기 — 지금 웨이브 단계 {_wave.Phase}", this);
        }

        void OnPhaseChanged(WavePhase phase)
        {
            switch (phase)
            {
                case WavePhase.Preparation: Fire(HelpTrigger.NewRunFirstPreparation); break;
                case WavePhase.Battle: Fire(HelpTrigger.BattleStarted); break;
                case WavePhase.Enrage: Fire(HelpTrigger.EnrageStarted); break;
            }
        }

        /// <summary>
        /// ⚠ 인자 순서가 <b>(변화량, 변화 후 총량)</b> 이다 — 이름만 보고 «지금 총량» 이 먼저라고
        ///   짐작하면 «늘었을 때만» 조건이 조용히 뒤집힌다(<c>ResourceManager.OnEnergyChanged</c> 주석).
        /// </summary>
        void OnEnergyChanged(int delta, int total)
        {
            if (delta > 0) Fire(HelpTrigger.EnergyGained);
        }

        void OnCharacterCreated(CharacterUnit unit, int cost) => Fire(HelpTrigger.CanCreateCharacter);
        void OnUpgraded(CharacterUnit unit, int level) => Fire(HelpTrigger.CharacterUpgraded);
        void OnAwakened(CharacterUnit unit, int kills) => Fire(HelpTrigger.HeroAwakened);
        /// <summary>
        /// ★★★ <b>전술 지침 창이 열려 있을 때만</b> «유저가 바꿨다» 로 센다
        /// (2026-08-24 · 유저 리포트로 찾은 버그).
        ///
        /// <c>CharacterTactics.Apply</c> 는 <b>유저가 창에서 고를 때</b>도 돌고
        /// <b>캐릭터가 태어날 때</b>도 돈다. 시작 유예(1.5초)로 뒷것을 걸러 보려 했지만
        /// <b>새어 나왔다</b> — 스포너의 일이 1.5초 안에 끝난다는 보장이 없다(실측으로 넷은
        /// 걸러지고 <b>다섯째가 통과했다</b>). 시간으로 «누가 했는가» 를 가르는 것은 언제나 경합이다.
        ///
        /// → <b>창이 열려 있는가</b>로 가른다. 그 창은 유저가 직접 열어야 하는 것이므로
        ///   «열려 있다» 가 곧 «유저가 지금 이것을 만지고 있다» 다. 경합이 없다.
        /// </summary>
        void OnTacticsChanged(CharacterTactics t)
        {
            TacticalOrderPanel panel = TacticalOrderPanel.Instance;
            if (panel == null || !panel.IsOpen) return;
            Fire(HelpTrigger.TacticsChanged);
        }

        void OnMentalError(CharacterUnit unit, MentalErrorDefinitionSO def) =>
            Fire(HelpTrigger.MentalErrorTriggered);

        /// <summary>
        /// ★ 전술과 같은 이유로 <b>부대 설정 창이 열려 있을 때만</b> 센다 —
        /// <c>UnitSpawner.AutoSquadInitialCharacters</c> 가 판을 차릴 때 부대를 자동으로 묶는다.
        /// </summary>
        void OnSquadsChanged()
        {
            SquadPanel panel = SquadPanel.Instance;
            if (panel == null || !panel.IsOpen) return;

            SquadService s = SquadService.Instance;
            if (s != null && s.Squads.Count > 0) Fire(HelpTrigger.SquadCreated);
        }

        void OnRallyChanged()
        {
            RallyPointService r = RallyPointService.Instance;
            if (r != null && r.HasAnyRally) Fire(HelpTrigger.RallyPointCreated);
        }

        void OnRelicChanged()
        {
            RelicInventory inv = RelicInventory.Instance;
            if (inv != null && inv.OwnedKinds > 0) Fire(HelpTrigger.RelicObtained);
        }

        void OnEventChanged(Events.EventDefinitionSO def, Events.EventChoice choice)
        {
            if (def != null) Fire(HelpTrigger.EventStarted);
        }

        void OnEpicChanged()
        {
            EpicSubjugationService e = EpicSubjugationService.Instance;
            if (e != null && e.Discovered.Count > 0) Fire(HelpTrigger.EpicNeutralFound);
        }

        /// <summary>
        /// ★ 아군이 쓰러졌나 / 중립을 잡았나 — <b>한 이벤트로 둘</b>을 가른다.
        /// ⚠ 성역·포탑도 <c>Angel</c> 이라 <b>캐릭터인지</b> 봐야 한다
        ///   (성역이 부서지면 그 판은 끝이고, 「쓰러진 캐릭터」 조언은 그때 쓸 데가 없다).
        /// </summary>
        void OnAnyDied(DamageableUnit unit)
        {
            if (unit == null) return;
            if (unit is CharacterUnit) Fire(HelpTrigger.AllyDied);
            else if (unit.Faction == Faction.Neutral) Fire(HelpTrigger.NeutralKilled);
        }

        /// <summary>★ <b>우리 공격이 빗나갔을 때만</b> — 적이 흘린 것은 배울 것이 없다.</summary>
        void OnAnyMissed(DamageableUnit attacker, DamageableUnit target)
        {
            if (attacker != null && attacker.Faction == Faction.Angel)
                Fire(HelpTrigger.FirstMiss);
        }

        // ==================================================================
        // 주기 검사 — 이벤트가 없는 것만
        // ==================================================================

        /// <summary>표에 실제로 쓰인 계기 중 «이벤트로 못 듣는 것» 만 골라 둔다.</summary>
        void CollectPolled()
        {
            _polled.Clear();
            if (_table == null) return;

            foreach (HelpTrigger t in new[]
            {
                HelpTrigger.BossWaveSpawned, HelpTrigger.RelicDigMarkAppeared,
                HelpTrigger.ErosionReached,
                HelpTrigger.AutoSaved, HelpTrigger.GameSpeedChanged,
            })
            {
                _table.CollectByTrigger(t, _scratch);
                for (int i = 0; i < _scratch.Count; i++)
                    if (!IsSeen(_scratch[i])) { _polled.Add(t); break; }
            }
        }

        void Poll()
        {
            for (int i = _polled.Count - 1; i >= 0; i--)
            {
                HelpTrigger t = _polled[i];
                if (!Probe(t)) continue;

                Fire(t);
                _polled.RemoveAt(i);   // ★ 뜬 것은 다시 안 본다 — 검사가 스스로 줄어든다
            }
        }

        /// <summary>그 계기의 조건이 «지금» 성립하는가.</summary>
        bool Probe(HelpTrigger t) => t switch
        {
            HelpTrigger.BossWaveSpawned => AnyBossAlive(),
            HelpTrigger.RelicDigMarkAppeared => RelicDigService.Instance != null &&
                                                RelicDigService.Instance.RevealedCount > 0,
            HelpTrigger.ErosionReached => ErosionAtLeast(ArgOf(t, 50)),
            HelpTrigger.AutoSaved => SaveChanged(),
            HelpTrigger.GameSpeedChanged => SpeedTouched(),
            _ => false,
        };

        /// <summary>
        /// ★★ <b>«저장 파일이 있다» 가 아니라 «방금 저장됐다» 를 본다</b>
        /// (2026-08-24 · 유저 리포트로 찾은 버그).
        ///
        /// 예전에는 <c>SaveService.HasSave</c> 를 봤다. 그런데 그것은 <b>지난 판의 저장 파일이
        /// 남아 있다</b> 는 뜻이라, 판을 켜는 순간 「저장과 이어하기」 조언이 대기줄에 들어갔다.
        /// 이어하기를 한 번이라도 쓴 유저에게는 <b>언제나</b> 그랬다.
        ///
        /// → <see cref="Awake"/> 에서 <b>지금 상태를 적어 두고</b>, 그것이 <b>바뀔 때</b>만 참이다.
        /// ⚠ 저장이 없던 상태에서 첫 자동 저장이 나면 라벨이 «없음» → «시각» 으로 바뀌므로
        ///   그 경우도 잡힌다.
        /// </summary>
        bool SaveChanged() => Save.SaveService.SavedAtLabel() != _saveLabelAtStart;

        /// <summary>판을 시작할 때의 저장 라벨. 이것이 바뀌면 «방금 저장됐다» 다.</summary>
        string _saveLabelAtStart = "";

        static bool AnyBossAlive()
        {
            var all = Combat.UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] is MonsterUnit m && m.IsBoss && m.IsAlive) return true;
            return false;
        }

        static bool ErosionAtLeast(int threshold)
        {
            var all = Combat.UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is not CharacterUnit c) continue;
                CharacterErosion e = CharacterErosion.Of(c);
                if (e != null && e.Erosion >= threshold) return true;
            }
            return false;
        }

        /// <summary>
        /// ★★★ <b>도움말이 스스로 멈춘 것은 «유저가 배속을 만졌다» 가 아니다</b>
        /// (2026-08-24 · 유저 리포트로 찾은 버그).
        ///
        /// 조언 카드와 안내는 읽는 동안 게임을 멈춘다(<see cref="ReadingPause"/>). 그러면
        /// <c>GameSpeedPanel.IsPaused</c> 가 참이 되고, 이 검사가 그것을 «유저가 정지를 눌렀다» 로
        /// 읽어 <b>「배속과 일시정지」 조언을 대기줄에 넣었다.</b> 카드가 뜨는 것만으로 다음 카드가
        /// 예약되니, 카드를 닫는 순간 그것이 튀어나온다 — <b>도움말이 도움말을 불러냈다.</b>
        ///
        /// → <see cref="ReadingPause.AnyHeld"/> 로 «지금 읽는 중인가» 를 물어 그때는 <b>세지 않는다</b>.
        /// ★ 유저가 P 로 <b>먼저</b> 멈춰 둔 상태라면 <c>ReadingPause</c> 는 소유권을 갖지 않으므로
        ///   <c>AnyHeld</c> 가 거짓이다 — 그 멈춤은 정말 유저가 한 것이라 조언이 떠야 맞다.
        /// </summary>
        bool SpeedTouched()
        {
            if (ReadingPause.AnyHeld) return false;

            if (_speed == null) _speed = FindAnyObjectByType<GameSpeedPanel>(FindObjectsInactive.Include);
            if (_speed == null) return false;
            return _speed.IsPaused || !Mathf.Approximately(_speed.CurrentSpeed, 1f);
        }

        /// <summary>그 계기를 쓰는 항목의 <c>trigger_arg</c>. 없으면 <paramref name="fallback"/>.</summary>
        int ArgOf(HelpTrigger t, int fallback)
        {
            if (_table == null) return fallback;
            _table.CollectByTrigger(t, _scratch);
            for (int i = 0; i < _scratch.Count; i++)
                if (_scratch[i].triggerArg > 0) return _scratch[i].triggerArg;
            return fallback;
        }

        // ==================================================================
        // 띄우기
        // ==================================================================

        /// <summary>
        /// 그 계기에 걸린 «아직 안 본» 항목을 대기줄에 넣는다.
        /// ★ <b>외부에서 불러도 된다</b> — 새 기능이 이벤트를 안 갖고 있으면 그쪽에서
        ///   이 한 줄만 부르면 붙는다.
        /// </summary>
        public void Fire(HelpTrigger trigger)
        {
            if (!enableAdviceCards || _table == null || trigger == HelpTrigger.None) return;

            if (IsStartupNoise(trigger))
            {
                if (logTriggers)
                    Debug.Log($"[도움말] {trigger} — 판이 <b>차려지는 중</b>이라 버렸습니다 " +
                              "(유저가 누른 것이 아닙니다)", this);
                return;
            }

            _table.CollectByTrigger(trigger, _scratch);
            if (_scratch.Count == 0)
            {
                if (logTriggers) Debug.Log($"[도움말] {trigger} — 표에 걸린 항목이 없습니다", this);
                return;
            }

            int added = 0;
            for (int i = 0; i < _scratch.Count; i++)
            {
                HelpEntry e = _scratch[i];
                if (IsSeen(e) || _queue.Contains(e)) continue;
                _queue.Add(e);
                added++;
            }
            if (added == 0) return;

            // 급한 것(priority 1)이 대기줄 앞으로 온다 — 같은 순간에 둘이 걸릴 수 있다.
            _queue.Sort((a, b) => a.priority != b.priority
                                ? a.priority.CompareTo(b.priority)
                                : a.order.CompareTo(b.order));

            _showAt = Time.unscaledTime + showDelaySeconds;
            if (logTriggers) Debug.Log($"[도움말] {trigger} — {added}개 대기줄에 넣었습니다", this);
        }

        // ==================================================================
        // ★★★ 허드 액션 버튼의 «첫 클릭» 을 가로챈다 (2026-08-25 신설)
        // ==================================================================

        /// <summary>지금 가로챈 항목. 카드나 안내가 끝나면 <see cref="CompletePending"/> 가 마무리한다.</summary>
        HelpEntry _pending;

        /// <summary>가로챈 버튼이 <b>원래 하려던 일</b>. 창을 여는 항목에는 없다(아래 doc).</summary>
        System.Action _pendingAction;

        /// <summary>
        /// ★★★ <b>버튼을 누른 «그 순간» 을 가로채 도움말을 먼저 보여준다</b>
        /// (2026-08-25 · 유저 지시: *"허드 액션의 각 버튼을 <b>최초로 눌렀을때</b> 해당 기능에
        /// 대한 도움말이 등장하는 것으로 진행"* · *"자세히 보기를 눌렀을 때 <b>실제 해당 ui가
        /// 켜지고</b> 각 기능에 대한 설명 시작"*).
        ///
        /// <b>왜 <see cref="Fire"/> 로는 안 되는가</b> — <see cref="PumpQueue"/> 에는
        /// «다른 창이 열려 있으면 기다린다»(<see cref="HudExclusive.AnyOpen"/>) 가 있다.
        /// 버튼은 <b>창을 연다</b>. 그래서 그냥 <c>Fire</c> 하면 카드가 대기줄에 들어간 채
        /// <b>창이 닫힐 때까지 안 뜬다</b> — 정작 설명이 필요한 순간을 지나쳐 버린다.
        ///
        /// <code>
        ///   버튼 클릭 ──▶ 아직 안 읽었다 ──▶ 창을 <b>열지 않고</b> 카드를 띄운다
        ///                                     ├ 「알겠습니다」 ──▶ 원래 하려던 일을 한다
        ///                                     └ 「자세히 보기」 ──▶ 안내가 <b>그 창을 열고</b> 짚는다
        ///                                                          끝나면 <b>열린 채로</b> 남는다
        ///        이미 읽었다 ──▶ false — 부르는 쪽이 <b>평소대로</b> 한다
        /// </code>
        ///
        /// ★ <b>두 길의 끝이 같다</b> — 어느 쪽으로 가도 유저가 누른 버튼의 일이 이루어진다.
        ///   «도움말을 봤더니 버튼이 안 먹었다» 가 되면 튜토리얼이 방해물이 된다.
        /// ⚠ <b>창을 여는 항목에는 <paramref name="continueAction"/> 이 필요 없다</b> —
        ///   표의 <c>open_panel</c> 이 이미 어느 창인지 알고 있고,
        ///   <see cref="CompletePending"/> 가 그것으로 연다. 넘겨도 무시하지 않고 «둘 다»
        ///   하면 창이 두 번 토글돼 <b>도로 닫힌다</b>.
        /// </summary>
        /// <param name="trigger">이 버튼에 걸린 <see cref="HelpTrigger"/> (<c>Action…</c>).</param>
        /// <param name="continueAction">
        ///   창을 열지 <b>않는</b> 항목(캐릭터 생성)이 원래 하려던 일. 창을 여는 항목은 <c>null</c>.
        /// </param>
        /// <returns><c>true</c> = 내가 가로챘다. 부르는 쪽은 <b>그대로 돌아가야 한다</b>.</returns>
        public bool InterceptFirstUse(HelpTrigger trigger, System.Action continueAction = null)
        {
            if (!enableAdviceCards || _table == null || trigger == HelpTrigger.None) return false;

            // ⚠ 이미 카드나 안내가 떠 있으면 가로채지 않는다 — 겹쳐 띄우면 앞엣것이 묻힌다.
            HelpCardPanel card = HelpCardPanel.Instance;
            if (card == null || card.IsOpen) return false;
            if (HelpTourPanel.Instance != null && HelpTourPanel.Instance.IsOpen) return false;

            // 판이 끝난 뒤(패배·승리 화면)에는 튜토리얼이 끼어들지 않는다.
            if (_wave != null && _wave.IsFinished) return false;

            _table.CollectByTrigger(trigger, _scratch);
            HelpEntry entry = null;
            for (int i = 0; i < _scratch.Count; i++)
                if (!IsSeen(_scratch[i])) { entry = _scratch[i]; break; }

            if (entry == null) return false;   // 이미 읽었다 — 평소대로 동작한다

            // ⚠ 대기줄에 같은 것이 들어 있으면 빼낸다 — 안 그러면 카드를 닫는 순간 <b>또</b> 뜬다.
            _queue.Remove(entry);

            MarkSeen(entry);
            _pending = entry;
            _pendingAction = continueAction;

            if (logTriggers)
                Debug.Log($"[도움말] {trigger} — 버튼을 <b>가로채</b> 「{entry.helpId}」 를 띄웁니다", this);

            card.Show(entry);
            return true;
        }

        /// <summary>
        /// 가로챈 도움말이 끝났다 — <b>버튼이 원래 하려던 일을 이제 한다</b>.
        /// 카드의 「알겠습니다」와 안내의 마지막 단계가 <b>둘 다</b> 부른다.
        ///
        /// ⚠ <b>먼저 비우고 나서 실행한다</b> — 여는 창이 또 도움말을 부르면(그럴 일은 없지만)
        ///   무한히 되돌아온다.
        /// ★ 안내를 거쳐 왔으면 그 창은 <b>이미 열려 있다</b>. 그래도 다시 여는 것이 맞다 —
        ///   안내가 <b>자기가 연 창은 자기가 닫고</b> 나가기 때문이다(145-6 절의 소유권 규칙).
        ///   같은 프레임 안에서 닫혔다 열리므로 화면에는 <b>끊김이 보이지 않는다</b>.
        /// </summary>
        public void CompletePending()
        {
            HelpEntry entry = _pending;
            System.Action action = _pendingAction;
            if (entry == null && action == null) return;

            _pending = null;
            _pendingAction = null;

            if (entry != null && !string.IsNullOrWhiteSpace(entry.openPanelPath))
            {
                Transform w = ResolvePath(entry.openPanelPath);
                if (w != null && HudExclusive.TryOpen(w, true)) return;

                Debug.LogWarning($"[도움말] {entry.helpId} 의 창을 열지 못했습니다: " +
                                 $"{entry.openPanelPath} — 표의 open_panel 을 확인하세요.", this);
                return;
            }

            action?.Invoke();
        }

        /// <summary>가로챈 것이 있으면 <b>버린다</b> — 창을 열지도, 원래 일을 하지도 않는다.</summary>
        public void CancelPending()
        {
            _pending = null;
            _pendingAction = null;
        }

        /// <summary><c>UI_Root/HUD_Xxx</c> 꼴의 씬 경로를 찾는다. 못 찾으면 null.</summary>
        static Transform ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            string[] parts = path.Split('/');
            GameObject root = GameObject.Find(parts[0]);
            if (root == null) return null;

            Transform t = root.transform;
            for (int i = 1; i < parts.Length && t != null; i++)
                t = FindChildIncludingInactive(t, parts[i]);
            return t;
        }

        /// <summary>⚠ <c>Transform.Find</c> 는 <b>비활성 자식도</b> 찾지만 이름이 정확해야 한다.</summary>
        static Transform FindChildIncludingInactive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            return null;
        }

        /// <summary>
        /// ★★★ <b>판이 «차려지는» 첫 순간을 «유저가 누른 것» 으로 세지 않는다</b>
        /// (2026-08-24 · <b>플레이 실측으로 찾은 것</b>).
        ///
        /// <b>무엇이 있었나</b> — 처음 붙여 놓고 플레이해 보니 판이 시작되자마자
        /// <b>「전술 지침」 카드가 떴다</b>. 아무도 전술을 만지지 않았는데.
        /// 원인은 이 프로젝트의 시작 절차다 — <c>UnitSpawner.SpawnAll</c> 이 캐릭터를 만들고
        /// <c>AutoSquadInitialCharacters</c> 로 부대를 묶고, 캐릭터마다
        /// <c>CharacterTactics.Apply</c> 가 한 번 돌면서 <c>OnAnyOrderChanged</c> 를 쏜다.
        /// 즉 <b>«기능이 실제로 일어난 자리» 를 듣는 이 설계의 유일한 약점</b>이 «판이 스스로
        /// 자기를 차리는 첫 프레임» 이다.
        ///
        /// ★ 시작 절차를 고치지 않는다 — 그쪽은 도움말과 아무 상관이 없고, 거기에 «도움말은
        ///   빼고» 를 심으면 이 서비스가 다른 시스템을 알게 된다(이 클래스의 존재 이유가 그것을
        ///   피하는 것이다). 그래서 <b>받는 쪽에서 걸러 낸다</b>.
        ///
        /// ⚠ <see cref="HelpTrigger.NewRunFirstPreparation"/> 은 <b>예외다</b> — 그것은
        ///   «유저가 누른 것» 이 아니라 <b>판의 상태</b>이고, 정확히 t=0 에 일어나야 맞는다
        ///   (가장 먼저 떠야 하는 두 장이 거기 걸려 있다).
        /// </summary>
        bool IsStartupNoise(HelpTrigger trigger) =>
            Time.unscaledTime < _graceUntil && trigger != HelpTrigger.NewRunFirstPreparation;

        void Update()
        {
            HookInstances();

            if (Time.unscaledTime >= _nextPoll && _polled.Count > 0)
            {
                _nextPoll = Time.unscaledTime + pollInterval;
                Poll();
            }

            PumpQueue();
        }

        /// <summary>
        /// 대기줄의 맨 앞을 카드에 넘긴다. <b>한 번에 하나</b>다.
        ///
        /// ⚠ <b>기다려야 하는 자리 넷</b> — ① 카드가 이미 떠 있다
        ///   ② <b>「자세히 보기」 안내가 돌고 있다</b> ③ 다른 창이 열려 있다
        ///   (사건 창 위에 조언을 덮으면 사건 선택지가 안 보인다) ④ 판이 끝났다
        ///   (패배·승리 화면이 <c>timeScale</c> 의 주인인 자리다).
        ///
        /// ★★★ <b>②가 이번에 고친 것이다</b> (2026-08-24 · 유저 리포트: *"자세히 보기를 누르면
        ///   다음 기능 도움말 ui 기능이 떠서 정작 자세히 보기를 누른 기능에 대한 ui 뒤에 뜨고
        ///   다음 기능 설명에 대한 ui가 먼저 뜸. 이러면 정상적인 듀토 진행 불가"*).
        ///
        ///   <b>무엇이 있었나</b> — 「자세히 보기」는 카드를 <b>닫고</b> 안내를 띄운다.
        ///   그 순간 이 함수가 «카드가 안 떠 있다» 고 보고 <b>대기줄의 다음 장을 꺼내</b>
        ///   안내 위에 덮었다. 카드는 뜰 때 <c>SetAsLastSibling</c> 을 부르므로 <b>안내보다
        ///   앞</b>으로 올라온다 — 그래서 «누른 기능의 안내는 뒤에, 다음 기능 카드가 앞에» 가 됐다.
        ///
        ///   ⚠ <see cref="HudExclusive.AnyOpen"/> 으로는 못 막는다. 안내는 <b>배타 창이 아니다</b>
        ///     (창이 아니라 덮는 한 겹이다). 창을 여는 항목은 그 창이 배타라서 우연히 막혔고,
        ///     창을 열지 않는 항목(웨이브·에너지·배속)만 <b>새어 나왔다</b> — 그래서 «어떤 것은
        ///     되고 어떤 것은 안 되는» 모양으로 보였다.
        /// </summary>
        void PumpQueue()
        {
            if (_queue.Count == 0) return;
            if (Time.unscaledTime < _showAt) return;

            HelpCardPanel card = HelpCardPanel.Instance;
            if (card == null) return;
            if (card.IsOpen) return;

            // ── ② 안내가 돌고 있으면 <b>끝날 때까지 기다린다</b> ──
            HelpTourPanel tour = HelpTourPanel.Instance;
            if (tour != null && tour.IsOpen) return;

            if (_wave != null && _wave.IsFinished) { _queue.Clear(); return; }
            if (HudExclusive.AnyOpen()) return;

            HelpEntry next = _queue[0];
            _queue.RemoveAt(0);
            MarkSeen(next);
            card.Show(next);
        }

        // ==================================================================
        // 기억
        // ==================================================================

        public bool IsSeen(HelpEntry e) =>
            e != null && e.showOnce && !string.IsNullOrEmpty(e.helpId) && _seen.Contains(e.helpId);

        void MarkSeen(HelpEntry e)
        {
            if (e == null || string.IsNullOrEmpty(e.helpId)) return;
            if (!_seen.Add(e.helpId)) return;
            SaveSeen();
        }

        void LoadSeen()
        {
            _seen.Clear();
            string raw = PlayerPrefs.GetString(PrefsKey, "");
            if (raw.Length == 0) return;

            string[] parts = raw.Split('|');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) _seen.Add(parts[i]);
        }

        void SaveSeen()
        {
            PlayerPrefs.SetString(PrefsKey, string.Join("|", _seen));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// ★ <b>조언을 처음부터 다시 본다</b> — 환경 설정에서 부른다.
        /// 기획을 검수할 때 «그 카드가 어떻게 떴는지» 를 다시 봐야 할 일이 잦다.
        /// </summary>
        public void ForgetAll()
        {
            _seen.Clear();
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
            CollectPolled();
            HudLog.Add(HudTheme.T("log_help_reset", "도움말을 처음 상태로 되돌렸습니다"), HudLogKind.Info);
        }

        /// <summary>지금까지 본 조언 수 / 전체 항목 수 — 환경 설정에 보여줄 수 있다.</summary>
        public int SeenCount => _seen.Count;
        public int TotalCount => _table != null ? _table.entries.Count : 0;
    }
}
