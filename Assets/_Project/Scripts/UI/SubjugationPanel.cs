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
    /// ⚠ 목록이 비어 있는 것은 <b>정상</b>이다 — 에픽은 맵 바깥 고리(넥서스에서 반지름
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
        [SerializeField] string hpFormat = "{0} / {1}";

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
                if (row.Order != null)
                    row.Order.text = order != null ? order.DisplayName : orderNone;

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

                if (row.Background != null)
                    row.Background.color = ReferenceEquals(t, ordered) ? rowOrdered : rowNormal;
            }
        }

        void RefreshHint()
        {
            if (_hint == null) return;

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

            // 같은 대상을 다시 누르면 해제.
            _service.SetOrder(_selectedSquadId, ReferenceEquals(now, t) ? null : t);
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
        }

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
