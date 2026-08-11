using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 부대 지정 창. 부대를 만들고 지우고, 부대에 캐릭터를 배정한다.
    ///
    /// <b>배정 방식</b>(유저 확정 2026-08-11): 부대 슬롯을 누른 뒤 <b>로스터에서 캐릭터를 클릭</b>하면
    /// 그 캐릭터가 선택된 부대에 들어가고 슬롯에 일러스트가 나타난다. 같은 캐릭터를 다시 누르면 빠진다.
    ///
    /// <b>두 가지 용도</b>로 열린다:
    /// <list type="number">
    /// <item>액션 버튼("부대 지정") — 편성 모드. 부대를 만들고 인원을 넣는다.</item>
    /// <item>맵의 집결지 클릭 — 그 집결지에 <b>어느 부대를 보낼지</b> 고르는 모드
    ///       (<see cref="OpenForRallyPoint"/>). 이때는 부대 슬롯을 누르면 배정 대상이 아니라
    ///       <b>그 집결지의 담당 부대</b>가 정해진다.</item>
    /// </list>
    ///
    /// 다른 패널(<c>TacticalOrderPanel</c>·<c>CharacterGrowthPanel</c>)과 같은 API 모양을 쓴다.
    /// </summary>
    public class SquadPanel : MonoBehaviour
    {
        public static SquadPanel Instance { get; private set; }

        [Header("갱신")]
        [Min(0f)] [SerializeField] float refreshInterval = 0.15f;

        [Header("문구")]
        [SerializeField] string titleAssign = "부대 지정";
        [SerializeField] string titleRally = "집결지 #{0} — 담당 부대 선택";
        [SerializeField] string hintAssign = "부대를 고른 뒤 로스터에서 캐릭터를 클릭하면 배정됩니다. 다시 누르면 해제.";
        [SerializeField] string hintRally = "이 집결지를 맡을 부대를 고르세요. '전체'를 고르면 부대 지정이 풀립니다.";
        [SerializeField] string hintNoSquad = "부대가 없습니다. '부대 추가'로 만드세요.";
        [SerializeField] string emptySlotText = "-";
        [SerializeField] string memberFormat = "{0}명";

        [Header("색")]
        [SerializeField] Color squadNormal = new Color(0.11f, 0.13f, 0.17f, 0.92f);
        [SerializeField] Color squadSelected = new Color(0.16f, 0.42f, 0.38f, 0.98f);
        [SerializeField] Color squadRallyOwner = new Color(0.42f, 0.34f, 0.14f, 0.98f);
        [SerializeField] Color labelActive = new Color(0.90f, 0.93f, 0.96f, 1f);
        [SerializeField] Color labelDim = new Color(0.50f, 0.55f, 0.62f, 1f);

        /// <summary>부대 카드 하나 — 이름 · 인원 수 · 부대원 초상화 줄.</summary>
        class Card
        {
            public GameObject Root;
            public Image Background;
            public TMP_Text Name;
            public TMP_Text Count;
            public Button Button;
            public Button RemoveButton;
            public readonly List<Image> Portraits = new List<Image>();
        }

        readonly List<Card> _cards = new List<Card>();

        Transform _grid;
        RectTransform _cardTemplate;
        TMP_Text _titleText, _hintText;
        Button _addButton;

        SquadService _squads;

        /// <summary>지금 배정 대상으로 고른 부대. 0 이면 고른 것 없음.</summary>
        public int SelectedSquadId { get; private set; }

        /// <summary>집결지 담당 부대를 고르는 모드일 때 그 집결지 id. 0 이면 일반 편성 모드.</summary>
        int _rallyPointId;

        float _nextRefresh;
        int _shownSignature = int.MinValue;

        void Awake()
        {
            Instance = this;
            BuildBindings();
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            _squads = SquadService.Instance;
            if (_squads != null) _squads.OnSquadsChanged += HandleSquadsChanged;
            Rebuild();
        }

        void OnDisable()
        {
            if (_squads != null) _squads.OnSquadsChanged -= HandleSquadsChanged;
        }

        void Update()
        {
            if (_squads == null)
            {
                _squads = SquadService.Instance;
                if (_squads != null) { _squads.OnSquadsChanged += HandleSquadsChanged; Rebuild(); }
            }

            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;
            RefreshCards();
        }

        void HandleSquadsChanged() => Rebuild();

        // ------------------------------------------------------------------
        // 열고 닫기
        // ------------------------------------------------------------------

        public bool IsOpen => gameObject.activeSelf;

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (open)
            {
                // 같은 자리에 겹치는 창들은 서로 닫는다 (전술·성장 창과 같은 규칙).
                TacticalOrderPanel.Instance?.Close();
                CharacterGrowthPanel.Instance?.Close();
                _rallyPointId = 0;          // 액션 버튼으로 연 것 = 편성 모드
            }

            gameObject.SetActive(open);
            if (open) Rebuild();
            else SelectedSquadId = 0;      // 닫으면 배정 대상 해제 — 다음에 열 때 남아있지 않게
        }

        public void Close() => SetOpen(false);

        /// <summary>맵의 집결지를 눌렀을 때 — 그 집결지의 담당 부대를 고르는 모드로 연다.</summary>
        public void OpenForRallyPoint(int rallyPointId)
        {
            TacticalOrderPanel.Instance?.Close();
            CharacterGrowthPanel.Instance?.Close();

            gameObject.SetActive(true);
            _rallyPointId = rallyPointId;
            SelectedSquadId = 0;
            Rebuild();
        }

        // ------------------------------------------------------------------
        // 로스터 연동 — 캐릭터 클릭이 배정으로 이어지는 지점
        // ------------------------------------------------------------------

        /// <summary>
        /// 지금 로스터 클릭을 <b>부대 배정</b>으로 가로채야 하는 상태인가.
        /// 편성 모드에서 부대를 하나 고른 경우에만 true —
        /// 집결지 모드에서는 캐릭터를 만지지 않는다.
        /// </summary>
        public bool IsAssigning => IsOpen && _rallyPointId == 0 && SelectedSquadId != 0;

        /// <summary>
        /// 로스터에서 캐릭터를 눌렀을 때 <see cref="CharacterRosterPanel"/> 이 부른다.
        /// 배정을 처리했으면 true — 그 경우 로스터는 <b>선택을 바꾸지 않는다</b>
        /// (배정하려고 누른 건데 선택까지 바뀌면 다른 창의 표시가 따라 움직여 혼란스럽다).
        /// </summary>
        public bool TryAssign(CharacterUnit unit)
        {
            if (!IsAssigning || unit == null) return false;

            if (_squads == null) _squads = SquadService.Instance;
            if (_squads == null) return false;

            _squads.Assign(unit, SelectedSquadId);
            return true;
        }

        // ------------------------------------------------------------------
        // 버튼
        // ------------------------------------------------------------------

        void HandleAdd()
        {
            if (_squads == null) _squads = SquadService.Instance;
            var squad = _squads?.CreateSquad();
            if (squad != null) SelectedSquadId = squad.Id;
            Rebuild();
        }

        void HandleCardClicked(int squadId)
        {
            if (_rallyPointId != 0)
            {
                // 집결지 모드 — 같은 부대를 다시 누르면 '전체'(부대 미지정)로 되돌린다.
                int current = CurrentRallySquadId();
                RallyPointService.Instance?.AssignSquad(_rallyPointId,
                                                        current == squadId ? 0 : squadId);
                _shownSignature = int.MinValue;
                RefreshCards();
                return;
            }

            // 편성 모드 — 배정 대상 토글
            SelectedSquadId = SelectedSquadId == squadId ? 0 : squadId;
            _shownSignature = int.MinValue;
            RefreshCards();
        }

        void HandleRemove(int squadId)
        {
            if (_squads == null) _squads = SquadService.Instance;
            _squads?.RemoveSquad(squadId);
            if (SelectedSquadId == squadId) SelectedSquadId = 0;
            Rebuild();
        }

        int CurrentRallySquadId()
        {
            var service = RallyPointService.Instance;
            if (service == null || _rallyPointId == 0) return 0;

            var points = service.Points;
            for (int i = 0; i < points.Count; i++)
                if (points[i].Id == _rallyPointId) return points[i].SquadId;
            return 0;
        }

        // ------------------------------------------------------------------
        // 표시
        // ------------------------------------------------------------------

        /// <summary>부대 개수가 바뀌면 카드를 다시 만든다 (모체 복제 — §10 H-2).</summary>
        void Rebuild()
        {
            if (_grid == null || _cardTemplate == null) return;
            if (_squads == null) _squads = SquadService.Instance;

            int want = _squads != null ? _squads.Squads.Count : 0;

            while (_cards.Count < want)
            {
                RectTransform clone = Instantiate(_cardTemplate, _grid);
                clone.gameObject.SetActive(true);
                clone.name = $"SquadCard_{_cards.Count:00}";
                _cards.Add(BindCard(clone, _cards.Count));
            }

            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i].Root != null) _cards[i].Root.SetActive(i < want);

            _shownSignature = int.MinValue;   // 다음 RefreshCards 가 무조건 다시 그리게
            RefreshCards();
        }

        Card BindCard(RectTransform root, int index)
        {
            var card = new Card
            {
                Root = root.gameObject,
                Background = root.GetComponent<Image>(),
                Name = FindText(root, "Name"),
                Count = FindText(root, "Count"),
                Button = root.GetComponent<Button>(),
                RemoveButton = root.Find("RemoveButton")?.GetComponent<Button>(),
            };

            Transform portraits = root.Find("Portraits");
            if (portraits != null)
                for (int i = 0; i < portraits.childCount; i++)
                {
                    var img = portraits.GetChild(i).GetComponent<Image>();
                    if (img != null) card.Portraits.Add(img);
                }

            // 클로저가 인덱스를 잡도록 지역 변수에 복사 — 반복 변수를 그대로 캡처하면
            // 모든 버튼이 마지막 값을 쓴다(고전적 실수).
            int slot = index;
            if (card.Button != null)
            {
                card.Button.onClick.RemoveAllListeners();
                card.Button.onClick.AddListener(() => HandleCardClicked(SquadIdAt(slot)));
            }
            if (card.RemoveButton != null)
            {
                card.RemoveButton.onClick.RemoveAllListeners();
                card.RemoveButton.onClick.AddListener(() => HandleRemove(SquadIdAt(slot)));
            }
            return card;
        }

        int SquadIdAt(int index)
        {
            if (_squads == null) return 0;
            var list = _squads.Squads;
            return index >= 0 && index < list.Count ? list[index].Id : 0;
        }

        void RefreshCards()
        {
            if (_squads == null) _squads = SquadService.Instance;

            bool rallyMode = _rallyPointId != 0;
            int rallyOwner = CurrentRallySquadId();

            if (_titleText != null)
                _titleText.text = rallyMode ? string.Format(titleRally, _rallyPointId) : titleAssign;

            int squadCount = _squads != null ? _squads.Squads.Count : 0;
            if (_hintText != null)
                _hintText.text = squadCount == 0 ? hintNoSquad : (rallyMode ? hintRally : hintAssign);

            if (_addButton != null)
                _addButton.interactable = _squads != null && _squads.CanCreate && !rallyMode;

            // 표시 내용이 그대로면 건드리지 않는다 — 매 프레임 텍스처를 갈아끼우지 않게.
            int signature = ComputeSignature(rallyMode, rallyOwner);
            if (signature == _shownSignature) return;
            _shownSignature = signature;

            for (int i = 0; i < _cards.Count; i++)
            {
                Card card = _cards[i];
                if (card == null || card.Root == null || !card.Root.activeSelf) continue;

                var squad = _squads.Squads[i];

                if (card.Name != null)
                {
                    card.Name.text = squad.Name;
                    card.Name.color = labelActive;
                }
                if (card.Count != null)
                    card.Count.text = string.Format(memberFormat, squad.AliveCount);

                if (card.Background != null)
                {
                    bool highlighted = rallyMode ? squad.Id == rallyOwner : squad.Id == SelectedSquadId;
                    card.Background.color = !highlighted ? squadNormal
                                          : (rallyMode ? squadRallyOwner : squadSelected);
                }

                // 부대원 초상화 — 캐릭터 정의의 일러스트를 그대로 쓴다.
                for (int p = 0; p < card.Portraits.Count; p++)
                {
                    Image slot = card.Portraits[p];
                    if (slot == null) continue;

                    CharacterUnit member = p < squad.Members.Count ? squad.Members[p] : null;
                    Sprite art = member != null && member.Definition != null ? member.Definition.Illust : null;

                    slot.sprite = art;
                    slot.color = art != null ? Color.white : new Color(1f, 1f, 1f, 0.06f);
                }

                // 집결지 모드에서는 부대를 지우지 못하게 한다 — 그 창의 목적이 아니다.
                if (card.RemoveButton != null) card.RemoveButton.gameObject.SetActive(!rallyMode);
            }
        }

        /// <summary>표시에 영향을 주는 값들을 한 정수로 접는다 — 바뀔 때만 다시 그리려는 것.</summary>
        int ComputeSignature(bool rallyMode, int rallyOwner)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (rallyMode ? 1 : 0);
                h = h * 31 + rallyOwner;
                h = h * 31 + SelectedSquadId;
                if (_squads != null)
                {
                    var list = _squads.Squads;
                    h = h * 31 + list.Count;
                    for (int i = 0; i < list.Count; i++)
                    {
                        h = h * 31 + list[i].Id;
                        h = h * 31 + list[i].Members.Count;
                        for (int m = 0; m < list[i].Members.Count; m++)
                            h = h * 31 + (list[i].Members[m] != null ? list[i].Members[m].GetInstanceID() : 0);
                    }
                }
                return h;
            }
        }

        // ------------------------------------------------------------------
        // 하이라키 연결 — 경로로 찾는다 (MCP 로는 인스펙터 참조를 못 넣는다)
        // ------------------------------------------------------------------

        void BuildBindings()
        {
            _titleText = FindText(transform, "Header/Title");
            _hintText = FindText(transform, "Header/Subtitle");
            _grid = transform.Find("Body/Grid");
            _cardTemplate = transform.Find("Body/Grid/SquadCard_Template") as RectTransform;

            if (_cardTemplate != null) _cardTemplate.gameObject.SetActive(false);
            else Debug.LogWarning("[Squad] 'Body/Grid/SquadCard_Template' 을 찾지 못했습니다.", this);

            _addButton = transform.Find("Header/AddButton")?.GetComponent<Button>();
            if (_addButton != null) _addButton.onClick.AddListener(HandleAdd);

            HookClose("Header/CloseButton");
            HookClose("Footer/CloseButton");
        }

        void HookClose(string path)
        {
            var button = transform.Find(path)?.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(Close);
        }

        static TMP_Text FindText(Transform root, string path)
        {
            Transform node = root.Find(path);
            return node != null ? node.GetComponent<TMP_Text>() : null;
        }
    }
}
