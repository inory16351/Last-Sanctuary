using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// <b>토벌 지시 창</b> — 탐험 중 발견한 <b>에픽 중립 몬스터</b>를 부대별로 골라 잡으러 보낸다
    /// (2026-08-15, 유저 지시: <i>"각 부대에 탐험 시 발견한 에픽 몬스터를 선택해 토벌할 수 있는
    /// ui 추가"</i> · 위치는 <i>"새 HUD 창으로 분리"</i> 로 확정).
    ///
    /// ★ <b>조작 흐름 — 두 번 클릭</b>
    /// <code>
    ///   ① 왼쪽 「부대」 줄에서 부대를 고른다
    ///   ② 오른쪽 「발견한 에픽」 줄에서 대상을 고른다  → 그 부대에 토벌 명령
    ///   (같은 대상을 다시 누르면 명령 해제)
    /// </code>
    /// 부대 설정 창이 "부대를 고른 뒤 로스터를 클릭" 인 것과 <b>같은 감각</b>이라
    /// 새로 배울 것이 없다.
    ///
    /// ★ <b>이 창은 상태를 갖지 않는다.</b> 명령의 정본은
    /// <see cref="EpicSubjugationService"/> 이고, 여기서는 그 값을 그리고 버튼을 눌러
    /// 그 서비스를 부를 뿐이다. 이동·교전은 <c>CharacterBehavior.TickSubjugation</c> 이 한다 —
    /// 세 층이 각각 <b>기억 / 표시 / 행동</b> 하나씩만 맡는다.
    ///
    /// ⚠ 목록이 비어 있는 것은 <b>정상</b>이다 — 에픽은 맵 바깥 고리(성역에서 반지름
    ///   100~160타일)에 살아서, 부대를 그쪽까지 탐험 보내기 전에는 발견되지 않는다.
    ///
    /// 다른 창(<c>SquadPanel</c>·<c>TacticalOrderPanel</c>·<c>CharacterGrowthPanel</c>)과
    /// 같은 API 모양(<c>Instance</c>/<c>IsOpen</c>/<c>Toggle</c>/<c>SetOpen</c>/<c>Close</c>)을 쓰고,
    /// 배타는 <see cref="HudExclusive.OpenOnly"/> 한 줄로 맡긴다.
    /// </summary>
    public class SubjugationPanel : MonoBehaviour, IExclusiveHudPanel
    {
        /// <summary>
        /// 이 창도 <b>비활성으로 시작</b>하므로 <c>Awake</c> 가 안 돈다 —
        /// <c>SkillDetailPanel</c> 이 겪은 함정 그대로다(49-6·36-4절). 비활성까지 포함해 찾는다.
        /// </summary>
        public static SubjugationPanel Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = FindAnyObjectByType<SubjugationPanel>(FindObjectsInactive.Include);
                if (_instance != null) _instance.EnsureBound();
                return _instance;
            }
            private set => _instance = value;
        }

        static SubjugationPanel _instance;

        [Header("갱신")]
        [Min(0f)] [SerializeField] float refreshInterval = 0.25f;

        [Header("문구")]
        [SerializeField] string hintPickSquad = "토벌을 맡길 부대를 고르세요.";
        [SerializeField] string hintPickTarget = "잡을 대상을 고르세요. 같은 대상을 다시 누르면 명령이 해제됩니다.";
        [SerializeField] string hintNoSquad = "부대가 없습니다. '부대 설정'에서 먼저 만드세요.";
        [SerializeField] string hintNoTarget = "아직 발견한 에픽 몬스터가 없습니다. 부대를 외곽까지 탐험 보내세요.";
        [SerializeField] string memberFormat = "{0}명";
        [SerializeField] string orderNone = "명령 없음";
        [SerializeField] string orderBusy = "토벌 중";
        [SerializeField] string hpFormat = "{0} / {1}";

        [Header("적정 레벨 (2026-08-25)")]
        [Tooltip("{0} = 표 recommend_level. 대상 줄 <b>오른쪽 끝</b>에 붙는다")]
        [SerializeField] string levelFormat = "적정 Lv.{0}";

        [Tooltip("부대 평균 레벨이 적정 레벨 이상일 때의 색 (갈 만하다)")]
        [SerializeField] Color levelReady = new Color(0.45f, 0.95f, 0.62f, 1f);

        [Tooltip("모자랄 때의 색 (아직 위험하다)")]
        [SerializeField] Color levelShort = new Color(0.95f, 0.46f, 0.42f, 1f);

        [Tooltip("고른 부대가 없을 때의 색 (견줄 대상이 없으니 그냥 숫자만 보여 준다)")]
        [SerializeField] Color levelIdle = new Color(0.78f, 0.80f, 0.86f, 1f);

        [Header("정원 (2026-08-25)")]
        [Tooltip("{0} = 지금 붙은 부대 수 · {1} = 정원. 대상 줄 오른쪽에 함께 뜬다")]
        [SerializeField] string squadCountFormat = "부대 {0}/{1}";

        [Tooltip("정원이 찬 대상을 눌렀을 때의 안내. {0} = 정원")]
        [SerializeField] string hintTargetFull = "이 대상에는 이미 {0}개 부대가 가 있습니다.";

        [Tooltip("안내 문구가 이 시간(초)만큼 남아 있다가 원래 안내로 돌아간다")]
        [Min(0.5f)] [SerializeField] float noticeSeconds = 2.5f;

        [Header("색")]
        [SerializeField] Color rowNormal = new Color(0.11f, 0.13f, 0.17f, 0.92f);
        [SerializeField] Color rowSelected = new Color(0.16f, 0.42f, 0.38f, 0.98f);
        [SerializeField] Color rowOrdered = new Color(0.42f, 0.18f, 0.22f, 0.96f);

        /// <summary>부대 줄 하나.</summary>
        class SquadRow
        {
            public GameObject Root;
            public Image Background;
            public Button Button;
            public TMP_Text Name;
            public TMP_Text Order;

            /// <summary>★ 오른쪽 끝 — 지금 토벌 보낸 몬스터 이름 (2026-08-25).</summary>
            public TMP_Text Target;
        }

        /// <summary>대상 줄 하나.</summary>
        class TargetRow
        {
            public GameObject Root;
            public Image Background;
            public Button Button;
            public Image Art;
            public TMP_Text Name;
            public TMP_Text Hp;

            /// <summary>★ 오른쪽 끝 — 적정 레벨과 «부대 n/2» (2026-08-25).</summary>
            public TMP_Text Level;
        }

        readonly List<SquadRow> _squadRows = new List<SquadRow>();
        readonly List<TargetRow> _targetRows = new List<TargetRow>();

        RectTransform _squadTemplate, _targetTemplate;
        Transform _squadList, _targetList;
        TMP_Text _hint;

        SquadService _squads;
        EpicSubjugationService _service;

        int _selectedSquadId;
        float _nextRefresh;
        bool _bound;

        /// <summary>임시 안내(정원 초과 등)가 살아 있는 시각. <c>unscaledTime</c> 기준.</summary>
        float _noticeUntil;
        string _notice = "";

        void Awake()
        {
            Instance = this;
            EnsureBound();

            // ⚠⚠ <b>여기서 gameObject.SetActive(false) 를 부르면 안 된다</b> —
            //   이 창은 비활성으로 저장돼 있어 Awake 가 <b>처음 열릴 때</b> 돈다.
            //   그 자리에서 자기를 끄면 창이 영영 안 뜬다.
            //   자세한 이유는 <see cref="UnitPortraitPanel"/> 의 Awake 주석 참조.
            //   "시작 시 닫힘"은 씬에 그렇게 저장해서 지키고,
            //   <c>ActionPanel.Start</c> 가 한 번 더 닫아준다.
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;
            Rebuild();
        }

        // ------------------------------------------------------------------
        // 열고 닫기 — 다른 창과 같은 API
        // ------------------------------------------------------------------

        public bool IsOpen => gameObject.activeSelf;

        public void Toggle() => SetOpen(!IsOpen);

        public void Close() => gameObject.SetActive(false);

        public void SetOpen(bool open)
        {
            EnsureBound();
            gameObject.SetActive(open);
            if (!open) return;

            HudExclusive.OpenOnly(this);     // 다른 창·지정 모드를 전부 끈다
            _nextRefresh = 0f;
            Rebuild();
        }

        // ------------------------------------------------------------------
        // 표시
        // ------------------------------------------------------------------

        void Rebuild()
        {
            if (_squads == null) _squads = SquadService.Instance;
            if (_service == null) _service = EpicSubjugationService.Instance;

            RebuildSquads();
            RebuildTargets();
            RefreshHint();
        }

        void RebuildSquads()
        {
            if (_squadList == null || _squadTemplate == null) return;

            IReadOnlyList<SquadService.Squad> squads =
                _squads != null ? _squads.Squads : new List<SquadService.Squad>();

            while (_squadRows.Count < squads.Count)
            {
                RectTransform clone = Instantiate(_squadTemplate, _squadList);
                clone.name = $"SquadRow_{_squadRows.Count:00}";

                var row = new SquadRow
                {
                    Root = clone.gameObject,
                    Background = clone.GetComponent<Image>(),
                    Button = clone.GetComponent<Button>(),
                    Name = FindText(clone, "Name"),
                    Order = FindText(clone, "Order"),
                    Target = FindText(clone, "Target"),
                };

                // 클로저가 인덱스를 잡도록 지역 변수에 복사 — 반복 변수를 그대로 캡처하면
                // 모든 버튼이 마지막 값을 쓴다(SquadPanel 과 같은 주의).
                int slot = _squadRows.Count;
                Hook(row.Button, () => HandleSquadClicked(slot));

                _squadRows.Add(row);
                clone.gameObject.SetActive(true);
            }

            for (int i = 0; i < _squadRows.Count; i++)
            {
                SquadRow row = _squadRows[i];
                bool used = i < squads.Count;
                if (row.Root != null) row.Root.SetActive(used);
                if (!used) continue;

                SquadService.Squad squad = squads[i];
                if (row.Name != null)
                    row.Name.text = $"{squad.Name}  <size=80%>{string.Format(memberFormat, squad.AliveCount)}</size>";

                NeutralMonsterUnit order = _service != null ? _service.OrderOf(squad.Id) : null;

                // ★ 2026-08-25 — 몬스터 <b>이름</b>은 오른쪽 끝(Target)으로 옮겼다.
                //   왼쪽 둘째 줄은 «지금 무엇을 하는 중인가» 만 말한다 — 같은 글자를
                //   한 줄에 두 번 적으면 어느 쪽이 정본인지 알 수 없어진다.
                if (row.Order != null)
                    row.Order.text = order != null ? orderBusy : orderNone;

                if (row.Target != null)
                    row.Target.text = order != null
                        ? $"<color=#{ColorUtility.ToHtmlStringRGB(HudTheme.TextErosion)}>{order.DisplayName}</color>"
                        : "";

                if (row.Background != null)
                    row.Background.color = squad.Id == _selectedSquadId ? rowSelected
                                         : order != null ? rowOrdered
                                         : rowNormal;
            }
        }

        void RebuildTargets()
        {
            if (_targetList == null || _targetTemplate == null) return;

            IReadOnlyList<NeutralMonsterUnit> targets =
                _service != null ? _service.Discovered : new List<NeutralMonsterUnit>();

            while (_targetRows.Count < targets.Count)
            {
                RectTransform clone = Instantiate(_targetTemplate, _targetList);
                clone.name = $"TargetRow_{_targetRows.Count:00}";

                var row = new TargetRow
                {
                    Root = clone.gameObject,
                    Background = clone.GetComponent<Image>(),
                    Button = clone.GetComponent<Button>(),
                    Art = clone.Find("Art")?.GetComponent<Image>(),
                    Name = FindText(clone, "Name"),
                    Hp = FindText(clone, "Hp"),
                    Level = FindText(clone, "Level"),
                };

                int slot = _targetRows.Count;
                Hook(row.Button, () => HandleTargetClicked(slot));

                _targetRows.Add(row);
                clone.gameObject.SetActive(true);
            }

            NeutralMonsterUnit ordered = _service != null ? _service.OrderOf(_selectedSquadId) : null;

            for (int i = 0; i < _targetRows.Count; i++)
            {
                TargetRow row = _targetRows[i];
                bool used = i < targets.Count;
                if (row.Root != null) row.Root.SetActive(used);
                if (!used) continue;

                NeutralMonsterUnit t = targets[i];
                if (row.Name != null)
                {
                    string title = t.Title;
                    row.Name.text = string.IsNullOrWhiteSpace(title)
                        ? t.DisplayName
                        : $"{t.DisplayName}  <size=76%><color=#{ColorUtility.ToHtmlStringRGB(HudTheme.TextErosion)}>{title}</color></size>";
                }

                if (row.Hp != null)
                    row.Hp.text = string.Format(hpFormat, Mathf.Max(0, t.CurrentHp), Mathf.Max(1, t.MaxHp));

                if (row.Art != null)
                {
                    Sprite art = t.Portrait;
                    row.Art.sprite = art;
                    row.Art.preserveAspect = true;
                    row.Art.color = art != null ? Color.white : new Color(1f, 1f, 1f, 0f);
                }

                if (row.Level != null) FillLevel(row.Level, t);

                if (row.Background != null)
                    row.Background.color = ReferenceEquals(t, ordered) ? rowOrdered : rowNormal;
            }
        }

        /// <summary>
        /// ★★★ <b>적정 레벨과 정원</b>을 대상 줄 오른쪽 끝에 적는다 (2026-08-25 · 유저 지시:
        /// *"각 중립 에픽 몬스터당 레벨 기준을 … 에픽 몬스터 오른쪽 끝에다가 표시해"*).
        ///
        /// <code>
        ///   적정 Lv.25      ← 표 recommend_level (0 이면 줄 자체를 비운다)
        ///   부대 1/2        ← 지금 몇 부대가 가 있는가 · 정원
        /// </code>
        ///
        /// ★ <b>색으로 «갈 만한가» 를 말한다</b> — 유저가 정한 기준이 «부대 하나(4명)의 레벨» 이라
        ///   고른 부대의 <b>평균 레벨</b>과 견준다. 부대를 아직 안 골랐으면 견줄 것이 없으므로
        ///   회색으로 숫자만 보여 준다(거짓 안심·거짓 경고를 만들지 않는다).
        /// ⚠ 레벨은 <c>UpgradeCount</c> 다 — 이 게임이 화면 곳곳에서 «Lv.N» 으로 부르는 그 값이다
        ///   (<c>CharacterRosterPanel</c>·<c>UnitPortraitPanel</c> 과 같은 기준).
        /// </summary>
        void FillLevel(TMP_Text label, NeutralMonsterUnit t)
        {
            int need = t != null ? t.RecommendLevel : 0;
            int squads = _service != null ? _service.SquadCountOn(t) : 0;
            int cap = _service != null ? _service.MaxSquadsPerTarget : 1;

            if (need <= 0)
            {
                label.text = squads > 0 ? string.Format(squadCountFormat, squads, cap) : "";
                label.color = levelIdle;
                return;
            }

            float avg = AverageLevelOfSelectedSquad();
            label.color = avg < 0f ? levelIdle : (avg + 0.0001f >= need ? levelReady : levelShort);
            label.text = string.Format(levelFormat, need) +
                         $"{NEWLINE}<size=78%>{string.Format(squadCountFormat, squads, cap)}</size>";
        }

        /// <summary>
        /// 고른 부대의 <b>평균 레벨</b>. 부대를 안 골랐거나 살아 있는 사람이 없으면 −1.
        /// ⚠ 죽은 사람은 세지 않는다 — «4명 기준» 은 실제로 갈 수 있는 사람의 이야기다.
        /// </summary>
        float AverageLevelOfSelectedSquad()
        {
            if (_squads == null || _selectedSquadId <= 0) return -1f;

            SquadService.Squad squad = _squads.Find(_selectedSquadId);
            if (squad == null) return -1f;

            int sum = 0, n = 0;
            for (int i = 0; i < squad.Members.Count; i++)
            {
                CharacterUnit m = squad.Members[i];
                if (m == null || !m.IsAlive) continue;
                sum += m.UpgradeCount;
                n++;
            }
            return n > 0 ? sum / (float)n : -1f;
        }

        void RefreshHint()
        {
            if (_hint == null) return;

            // ★ 임시 안내(정원 초과)가 살아 있으면 그것을 보여 준다 — 누른 결과가
            //   화면 어딘가에 남아야 «눌렸는지» 를 안다(2026-08-25).
            if (Time.unscaledTime < _noticeUntil && !string.IsNullOrEmpty(_notice))
            {
                _hint.text = _notice;
                return;
            }

            bool hasSquad = _squads != null && _squads.Squads.Count > 0;
            bool hasTarget = _service != null && _service.Discovered.Count > 0;

            _hint.text = !hasSquad ? hintNoSquad
                       : !hasTarget ? hintNoTarget
                       : _selectedSquadId <= 0 ? hintPickSquad
                       : hintPickTarget;
        }

        // ------------------------------------------------------------------
        // 입력
        // ------------------------------------------------------------------

        void HandleSquadClicked(int index)
        {
            if (_squads == null) return;
            if (index < 0 || index >= _squads.Squads.Count) return;

            int id = _squads.Squads[index].Id;
            _selectedSquadId = _selectedSquadId == id ? 0 : id;   // 다시 누르면 선택 해제
            _nextRefresh = 0f;
        }

        void HandleTargetClicked(int index)
        {
            if (_service == null) return;
            if (index < 0 || index >= _service.Discovered.Count) return;
            if (_selectedSquadId <= 0) return;                    // 부대를 먼저 골라야 한다

            NeutralMonsterUnit t = _service.Discovered[index];
            NeutralMonsterUnit now = _service.OrderOf(_selectedSquadId);
            bool release = ReferenceEquals(now, t);            // 같은 대상을 다시 누르면 해제

            // ★ 정원이 찼으면 «왜 안 되는지» 를 말한다 (2026-08-25).
            if (!release && !_service.CanOrder(_selectedSquadId, t))
            {
                _notice = string.Format(hintTargetFull, _service.MaxSquadsPerTarget);
                _noticeUntil = Time.unscaledTime + noticeSeconds;
                _nextRefresh = 0f;
                return;
            }

            _service.SetOrder(_selectedSquadId, release ? null : t);
            _nextRefresh = 0f;
        }

        // ------------------------------------------------------------------

        void EnsureBound()
        {
            if (_bound) return;
            _bound = true;

            _squadList = transform.Find("Squads/List");
            _targetList = transform.Find("Targets/List");
            _hint = FindText(transform, "Hint");

            _squadTemplate = transform.Find("Squads/RowTemplate") as RectTransform;
            _targetTemplate = transform.Find("Targets/RowTemplate") as RectTransform;

            if (_squadTemplate == null || _targetTemplate == null)
                Debug.LogWarning("[토벌] Squads/RowTemplate · Targets/RowTemplate 을 찾지 못했습니다.", this);

            // ★ 닫기 버튼 (유저 지시 2026-08-18: "토벌 지시 ui도 x로 끌 수 있는 버튼 추가").
            //   이 창만 닫는 방법이 없어서, 한 번 열면 다른 창을 열어 밀어내는 수밖에 없었다
            //   (HudExclusive 가 배타로 닫아주는 것에 기대고 있었다).
            //   ⚠ 없어도 조용히 넘어간다 — 씬이 아직 갱신되지 않은 상태에서 에러를 내지 않는다.
            var close = transform.Find("CloseButton")?.GetComponent<Button>();
            if (close != null)
            {
                close.onClick.RemoveAllListeners();
                close.onClick.AddListener(Close);
            }
        }

        /// <summary>TMP 리치텍스트 안에 넣을 줄바꿈 — 소스에 실제 개행을 쓰지 않기 위한 상수.</summary>
        const string NEWLINE = "\n";

        static TMP_Text FindText(Transform parent, string path)
        {
            Transform t = parent.Find(path);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        static void Hook(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}
