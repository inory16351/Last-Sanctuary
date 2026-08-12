using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 부대 설정 창. 부대를 만들고 지우고, 인원을 배정하고, <b>부대마다 집결지를 설정·해제</b>한다.
    ///
    /// <b>2026-08-12 개편</b>(유저 확정): 예전엔 액션 버튼이 "부대 지정 / 집결지 생성 / 집결지 해제"
    /// 세 개였고 집결지의 담당 부대는 <b>맵의 집결지를 클릭</b>해서 골랐다. 지금은 그 셋이
    /// <b>"부대 설정" 버튼 하나</b>로 합쳐졌고, 집결지는 <b>부대 카드 안의 두 버튼</b>으로 다룬다 —
    /// 즉 집결지는 처음부터 어느 부대의 것인지가 정해진 채로 만들어진다.
    ///
    /// <b>배정 방식</b>(유저 확정 2026-08-11): 부대 카드를 누른 뒤 <b>로스터에서 캐릭터를 클릭</b>하면
    /// 그 캐릭터가 선택된 부대에 들어가고 슬롯에 일러스트가 나타난다. 같은 캐릭터를 다시 누르면 빠진다.
    ///
    /// 다른 패널(<c>TacticalOrderPanel</c>·<c>CharacterGrowthPanel</c>)과 같은 API 모양을 쓴다.
    /// </summary>
    public class SquadPanel : MonoBehaviour
    {
        public static SquadPanel Instance { get; private set; }

        [Header("갱신")]
        [Min(0f)] [SerializeField] float refreshInterval = 0.15f;

        [Header("문구")]
        [SerializeField] string title = "부대 설정";
        [SerializeField] string hint = "부대를 고른 뒤 로스터에서 캐릭터를 클릭하면 배정됩니다(다시 누르면 해제). 부대 이름은 직접 고칠 수 있습니다.";
        [SerializeField] string hintNoSquad = "부대가 없습니다. '부대 추가'로 만드세요.";
        [SerializeField] string memberFormat = "{0}명";
        [SerializeField] string rallySetIdle = "집결지 설정";
        [SerializeField] string rallySetPicking = "맵을 클릭";
        [SerializeField] string rallySetMove = "집결지 이동";
        [SerializeField] string rallyClear = "집결지 해제";

        [Header("색")]
        [SerializeField] Color squadNormal = new Color(0.11f, 0.13f, 0.17f, 0.92f);
        [SerializeField] Color squadSelected = new Color(0.16f, 0.42f, 0.38f, 0.98f);
        [SerializeField] Color buttonNormal = new Color(0.13f, 0.17f, 0.22f, 0.95f);
        [SerializeField] Color buttonOn = new Color(0.16f, 0.42f, 0.38f, 0.98f);
        [SerializeField] Color buttonOff = new Color(0.10f, 0.11f, 0.13f, 0.85f);
        [SerializeField] Color labelActive = new Color(0.90f, 0.93f, 0.96f, 1f);

        /// <summary>부대 카드 하나 — 이름(편집 가능) · 인원 수 · 부대원 초상화 줄 · 집결지 버튼 2개.</summary>
        class Card
        {
            public GameObject Root;
            public Image Background;
            public TMP_InputField NameInput;
            public TMP_Text Count;
            public Button Button;
            public Button RemoveButton;
            public Button RallySetButton;
            public Image RallySetBackground;
            public TMP_Text RallySetLabel;
            public Button RallyClearButton;
            public Image RallyClearBackground;
            public TMP_Text RallyClearLabel;
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
            }
            else
            {
                // 창을 닫으면 집결지 지정 모드도 같이 끊는다 — 창에서 시작한 조작이라
                // 창이 없는데 "맵을 클릭하세요" 상태만 남으면 빠져나올 방법이 안 보인다.
                // (지정 모드로 들어가면서 창이 자동으로 닫히는 경우는 예외 — HandleRallySet 참조)
                if (!_pickingHandoff) RallyPointService.Instance?.CancelPicking();
                _pickingHandoff = false;
            }

            gameObject.SetActive(open);
            if (open) Rebuild();
            else SelectedSquadId = 0;      // 닫으면 배정 대상 해제 — 다음에 열 때 남아있지 않게
        }

        public void Close() => SetOpen(false);

        /// <summary>"집결지 설정"으로 창이 스스로 닫히는 중인지 — 그때는 지정 모드를 끊으면 안 된다.</summary>
        bool _pickingHandoff;

        // ------------------------------------------------------------------
        // 로스터 연동 — 캐릭터 클릭이 배정으로 이어지는 지점
        // ------------------------------------------------------------------

        /// <summary>지금 로스터 클릭을 <b>부대 배정</b>으로 가로채야 하는 상태인가.</summary>
        public bool IsAssigning => IsOpen && SelectedSquadId != 0;

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

        void HandleRename(int squadId, string value)
        {
            if (_squads == null) _squads = SquadService.Instance;
            if (_squads == null) return;

            // 실패(빈 이름 등)해도 조용히 넘어간다 — 다음 RefreshCards 가 원래 이름으로 되돌린다.
            _squads.Rename(squadId, value);
            _shownSignature = int.MinValue;
        }

        /// <summary>
        /// 이 부대의 집결지를 찍는다. <b>창을 닫는다</b> — 창이 화면의 큰 부분을 덮고 있어서
        /// 열어둔 채로는 맵을 클릭할 수 없다.
        /// </summary>
        void HandleRallySet(int squadId)
        {
            var rally = RallyPointService.Instance;
            if (rally == null) return;

            bool alreadyPicking = rally.IsPicking && rally.PickingSquadId == squadId;
            rally.TogglePickingForSquad(squadId);

            if (alreadyPicking) return;      // 껐으면 창은 그대로 둔다

            _pickingHandoff = true;          // 아래 Close 가 방금 켠 지정 모드를 끄지 않게
            Close();
        }

        void HandleRallyClear(int squadId)
        {
            RallyPointService.Instance?.RemoveForSquad(squadId);
            _shownSignature = int.MinValue;
            RefreshCards();
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
                clone.name = $"SquadCard_{_cards.Count:00}";

                // ⚠️ 배선(BindCard)을 활성화보다 먼저 한다 — TMP_InputField 는 OnEnable 에서
                // 캐럿을 만들면서 textComponent/textViewport 를 읽는다. 활성화부터 하면
                // 그 참조가 아직 null 이라 입력창이 죽는다.
                _cards.Add(BindCard(clone, _cards.Count));
                clone.gameObject.SetActive(true);
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
                Count = FindText(root, "Count"),
                Button = root.GetComponent<Button>(),
                RemoveButton = root.Find("RemoveButton")?.GetComponent<Button>(),
                RallySetButton = root.Find("RallySetButton")?.GetComponent<Button>(),
                RallySetBackground = root.Find("RallySetButton")?.GetComponent<Image>(),
                RallySetLabel = FindText(root, "RallySetButton/Label"),
                RallyClearButton = root.Find("RallyClearButton")?.GetComponent<Button>(),
                RallyClearBackground = root.Find("RallyClearButton")?.GetComponent<Image>(),
                RallyClearLabel = FindText(root, "RallyClearButton/Label"),
            };

            BindNameInput(root, card);

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
            Hook(card.Button, () => HandleCardClicked(SquadIdAt(slot)));
            Hook(card.RemoveButton, () => HandleRemove(SquadIdAt(slot)));
            Hook(card.RallySetButton, () => HandleRallySet(SquadIdAt(slot)));
            Hook(card.RallyClearButton, () => HandleRallyClear(SquadIdAt(slot)));

            if (card.NameInput != null)
            {
                card.NameInput.onEndEdit.RemoveAllListeners();
                card.NameInput.onEndEdit.AddListener(v => HandleRename(SquadIdAt(slot), v));
            }
            return card;
        }

        /// <summary>
        /// 부대 이름 입력창을 배선한다. <see cref="TMP_InputField"/> 는 표시용 텍스트와
        /// 클리핑 영역을 <b>참조로</b> 들고 있어야 하는데, MCP 로는 씬 오브젝트 참조를 넣을 수
        /// 없다(진행상황 8절 4번) — 그래서 하이라키(NameInput/TextArea/Name)만 MCP 로 만들고
        /// 참조는 여기서 이어준다.
        /// </summary>
        static void BindNameInput(RectTransform root, Card card)
        {
            Transform node = root.Find("NameInput");
            if (node == null) return;

            var input = node.GetComponent<TMP_InputField>();
            if (input == null) return;

            var viewport = node.Find("TextArea") as RectTransform;
            var text = viewport != null ? viewport.Find("Name")?.GetComponent<TMP_Text>() : null;

            if (viewport != null) input.textViewport = viewport;
            if (text != null) input.textComponent = text;

            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;
            card.NameInput = input;
        }

        static void Hook(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
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

            if (_titleText != null) _titleText.text = title;

            int squadCount = _squads != null ? _squads.Squads.Count : 0;
            if (_hintText != null) _hintText.text = squadCount == 0 ? hintNoSquad : hint;

            if (_addButton != null)
                _addButton.interactable = _squads != null && _squads.CanCreate;

            RallyPointService rally = RallyPointService.Instance;

            // 표시 내용이 그대로면 건드리지 않는다 — 매 프레임 텍스처를 갈아끼우지 않게.
            int signature = ComputeSignature(rally);
            if (signature == _shownSignature) return;
            _shownSignature = signature;

            for (int i = 0; i < _cards.Count; i++)
            {
                Card card = _cards[i];
                if (card == null || card.Root == null || !card.Root.activeSelf) continue;

                var squad = _squads.Squads[i];

                // 입력 중에는 건드리지 않는다 — 타이핑하는 글자를 매 갱신마다 덮어쓰게 된다.
                if (card.NameInput != null && !card.NameInput.isFocused)
                    card.NameInput.SetTextWithoutNotify(squad.Name);

                if (card.Count != null)
                {
                    card.Count.text = string.Format(memberFormat, squad.AliveCount);
                    card.Count.color = labelActive;
                }

                if (card.Background != null)
                    card.Background.color = squad.Id == SelectedSquadId ? squadSelected : squadNormal;

                RefreshRallyButtons(card, squad.Id, rally);

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
            }
        }

        void RefreshRallyButtons(Card card, int squadId, RallyPointService rally)
        {
            bool hasRally = rally != null && rally.HasRallyForSquad(squadId);
            bool picking = rally != null && rally.IsPicking && rally.PickingSquadId == squadId;

            if (card.RallySetButton != null)
            {
                card.RallySetButton.interactable = rally != null;
                if (card.RallySetBackground != null)
                    card.RallySetBackground.color = rally == null ? buttonOff
                                                  : (picking ? buttonOn : buttonNormal);
                if (card.RallySetLabel != null)
                    card.RallySetLabel.text = picking ? rallySetPicking
                                            : (hasRally ? rallySetMove : rallySetIdle);
            }

            if (card.RallyClearButton != null)
            {
                // 지울 것이 있을 때만 눌린다 (예전 액션 패널의 '집결지 해제' 와 같은 규칙).
                card.RallyClearButton.interactable = hasRally;
                if (card.RallyClearBackground != null)
                    card.RallyClearBackground.color = hasRally ? buttonNormal : buttonOff;
                if (card.RallyClearLabel != null) card.RallyClearLabel.text = rallyClear;
            }
        }

        /// <summary>표시에 영향을 주는 값들을 한 정수로 접는다 — 바뀔 때만 다시 그리려는 것.</summary>
        int ComputeSignature(RallyPointService rally)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + SelectedSquadId;
                h = h * 31 + (rally != null && rally.IsPicking ? rally.PickingSquadId + 1 : 0);
                if (_squads != null)
                {
                    var list = _squads.Squads;
                    h = h * 31 + list.Count;
                    for (int i = 0; i < list.Count; i++)
                    {
                        h = h * 31 + list[i].Id;
                        h = h * 31 + (list[i].Name != null ? list[i].Name.GetHashCode() : 0);
                        h = h * 31 + (rally != null && rally.HasRallyForSquad(list[i].Id) ? 1 : 0);
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
