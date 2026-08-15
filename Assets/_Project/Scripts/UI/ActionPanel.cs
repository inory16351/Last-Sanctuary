using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 우측 액션 버튼 묶음 — 캐릭터 생성 / 부대 설정 / 전술 지침.
    /// (캐릭터 강화 버튼은 <see cref="UpgradeButtonUI"/>, 건설은 <see cref="BuildButtonUI"/> 가
    ///  그대로 담당한다. 같은 기능을 두 벌 만들지 않는다 — 준수사항 §10 H-3.)
    ///
    /// <b>2026-08-12 통합</b>(유저 확정): 예전의 "부대 지정 / 집결지 생성 / 집결지 해제"
    /// 세 버튼을 <b>"부대 설정" 하나</b>로 합쳤다. 집결지는 이제 그 창의 부대 카드마다
    /// 붙은 두 버튼(설정·해제)으로 다룬다 — <see cref="SquadPanel"/> 참조.
    /// 그래서 이 컴포넌트에서 집결지 관련 필드·핸들러가 통째로 빠졌다.
    ///
    /// 규칙 판단은 서비스에 있고 이 컴포넌트는 "언제 눌릴 수 있는지"와 "라벨에 뭘 쓸지"만 다룬다.
    /// <c>UpgradeButtonUI</c> 와 같은 구조로 맞췄다.
    /// </summary>
    public class ActionPanel : MonoBehaviour
    {
        [Header("하이라키 연결")]
        [SerializeField] Button createButton;
        [SerializeField] TMP_Text createLabel;
        [SerializeField] Image createBackground;

        [SerializeField] Button tacticsButton;
        [SerializeField] TMP_Text tacticsLabel;
        [SerializeField] Image tacticsBackground;

        [SerializeField] Button squadButton;
        [SerializeField] TMP_Text squadLabel;
        [SerializeField] Image squadBackground;

        [SerializeField] Button subjugateButton;
        [SerializeField] TMP_Text subjugateLabel;
        [SerializeField] Image subjugateBackground;

        [Header("문구")]
        [SerializeField] string createFormat = "캐릭터 생성 {0}";
        [SerializeField] string createAtLimit = "인원 상한";
        [SerializeField] string tacticsIdle = "전술 지침";
        [SerializeField] string tacticsOpen = "전술 지침 닫기";
        [SerializeField] string squadIdle = "부대 설정";
        [SerializeField] string squadOpen = "부대 설정 닫기";
        [SerializeField] string squadPicking = "집결지 지정 중";
        [SerializeField] string subjugateOpen = "토벌 지시 닫기";
        [Tooltip("{0} = 발견한 에픽 몬스터 수. 0 이면 아래 subjugateNone 을 쓴다")]
        [SerializeField] string subjugateFound = "토벌 지시 ({0})";
        [SerializeField] string subjugateNone = "토벌 지시";

        [Header("색")]
        [SerializeField] Color buttonNormal = new Color(0.13f, 0.17f, 0.22f, 0.95f);
        [SerializeField] Color buttonOn = new Color(0.16f, 0.42f, 0.38f, 0.98f);
        [SerializeField] Color buttonOff = new Color(0.10f, 0.11f, 0.13f, 0.85f);

        CharacterCreationService _creation;

        /// <summary>전술 지침 창. <b>평소 비활성</b>이라 이름/타입 조회에 비활성 포함이 필요하다.</summary>
        TacticalOrderPanel _tacticsPanel;

        /// <summary>부대 설정 창. 전술 지침 창과 같은 이유로 비활성 포함 조회가 필요하다.</summary>
        SquadPanel _squadPanel;

        /// <summary>토벌 지시 창(2026-08-15 신설). 위 둘과 같은 이유로 비활성 포함 조회.</summary>
        SubjugationPanel _subjugationPanel;

        // 마지막으로 화면에 반영한 값. 바뀔 때만 갱신한다.
        int _shownCost = int.MinValue;
        bool _shownCanCreate;
        bool _shownTacticsOpen;
        int _shownSquadState = int.MinValue;
        int _shownSubjugateState = int.MinValue;

        void Start()
        {
            _creation = CharacterCreationService.Instance;

            // 전술 지침·부대 설정 창은 버튼을 누르기 전까지 비활성이므로 Instance(=Awake 에서 설정)가
            // 아직 없다. 비활성 오브젝트까지 포함해 직접 찾는다.
            _tacticsPanel = FindAnyObjectByType<TacticalOrderPanel>(FindObjectsInactive.Include);
            _squadPanel = FindAnyObjectByType<SquadPanel>(FindObjectsInactive.Include);
            _subjugationPanel = FindAnyObjectByType<SubjugationPanel>(FindObjectsInactive.Include);

            // MCP 로는 씬 오브젝트 참조를 인스펙터에 넣을 수 없어서(진행상황 8절 4번),
            // 비어 있으면 이름으로 찾는다.
            Resolve("Buttons/CreateButton", ref createButton, ref createBackground, ref createLabel);
            Resolve("Buttons/TacticsButton", ref tacticsButton, ref tacticsBackground, ref tacticsLabel);
            Resolve("Buttons/SquadButton", ref squadButton, ref squadBackground, ref squadLabel);
            Resolve("Buttons/SubjugateButton", ref subjugateButton, ref subjugateBackground, ref subjugateLabel);

            // 창들은 "닫힌 채로 시작"이 규칙이다. 창 스스로 Awake 에서 닫으면
            // <b>열리는 순간 닫히는</b> 버그가 되므로(UnitPortraitPanel.Awake 주석),
            // 항상 살아 있는 이쪽에서 한 번 확인해 닫는다.
            if (_subjugationPanel != null && _subjugationPanel.IsOpen) _subjugationPanel.Close();

            if (createButton != null) createButton.onClick.AddListener(HandleCreate);
            if (tacticsButton != null) tacticsButton.onClick.AddListener(HandleTactics);
            if (squadButton != null) squadButton.onClick.AddListener(HandleSquad);
            if (subjugateButton != null) subjugateButton.onClick.AddListener(HandleSubjugate);

            if (_creation == null)
                Debug.LogWarning("[Actions] CharacterCreationService 를 찾지 못했습니다. " +
                                 "GameSystems 오브젝트에 붙어 있는지 확인하세요.", this);

            Refresh(force: true);
        }

        void Update() => Refresh(force: false);

        // ------------------------------------------------------------------

        /// <summary>버튼 하나를 이루는 세 조각(Button·Image·Label)을 경로에서 한 번에 채운다.</summary>
        void Resolve(string path, ref Button button, ref Image background, ref TMP_Text label)
        {
            Transform node = transform.Find(path);
            if (node == null) return;

            if (button == null) button = node.GetComponent<Button>();
            if (background == null) background = node.GetComponent<Image>();
            if (label == null)
            {
                Transform labelNode = node.Find("Label");
                if (labelNode != null) label = labelNode.GetComponent<TMP_Text>();
            }
        }

        void HandleCreate()
        {
            if (_creation == null) _creation = CharacterCreationService.Instance;
            _creation?.TryCreate();
            Refresh(force: true);
        }

        /// <summary>
        /// 부대 설정 창을 연다/닫는다. <b>집결지 지정 중이면 먼저 그것부터 취소</b>한다 —
        /// 지정 모드는 이 창에서 시작한 조작이라(카드의 "집결지 설정"), 창을 다시 여는 것이
        /// 곧 "그만두고 돌아왔다"는 뜻이다.
        /// </summary>
        void HandleSquad()
        {
            if (_squadPanel == null)
                _squadPanel = FindAnyObjectByType<SquadPanel>(FindObjectsInactive.Include);

            var rally = RallyPointService.Instance;
            if (rally != null && rally.IsPicking) rally.CancelPicking();

            _squadPanel?.Toggle();
            Refresh(force: true);
        }

        /// <summary>
        /// 전술 지침 창을 연다/닫는다. 창은 캐릭터를 선택하지 않는다 — 선택은 로스터와
        /// 월드 클릭이 하고, 창은 그 선택을 따라간다(<see cref="TacticalOrderPanel"/> 클래스 doc).
        /// 그래서 선택된 캐릭터가 없어도 열 수 있다.
        /// </summary>
        void HandleTactics()
        {
            if (_tacticsPanel == null)
                _tacticsPanel = FindAnyObjectByType<TacticalOrderPanel>(FindObjectsInactive.Include);

            _tacticsPanel?.Toggle();
            Refresh(force: true);
        }

        /// <summary>
        /// 토벌 지시 창을 연다/닫는다 (2026-08-15 신설). 부대 설정과 같은 이유로
        /// <b>집결지 지정 중이면 먼저 취소</b>한다 — 맵 클릭을 기다리는 모드를 켜둔 채
        /// 다른 창을 열면 그다음 클릭이 어디로 갈지 알 수 없다.
        /// </summary>
        void HandleSubjugate()
        {
            if (_subjugationPanel == null)
                _subjugationPanel = FindAnyObjectByType<SubjugationPanel>(FindObjectsInactive.Include);

            var rally = RallyPointService.Instance;
            if (rally != null && rally.IsPicking) rally.CancelPicking();

            _subjugationPanel?.Toggle();
            Refresh(force: true);
        }

        void Refresh(bool force)
        {
            RefreshCreate(force);
            RefreshTactics(force);
            RefreshSquad(force);
            RefreshSubjugate(force);
        }

        /// <summary>
        /// 토벌 지시 버튼 — 열림 여부와 <b>발견한 에픽 수</b>를 같이 보여준다.
        /// 발견한 것이 없으면 숫자를 안 붙인다(0 이 붙어 있으면 고장난 것처럼 보인다).
        /// </summary>
        void RefreshSubjugate(bool force)
        {
            if (subjugateButton == null) return;

            bool open = _subjugationPanel != null && _subjugationPanel.IsOpen;
            var service = Units.EpicSubjugationService.Instance;
            int found = service != null ? service.Discovered.Count : 0;

            int state = (open ? 1 : 0) | (found << 1);
            if (!force && state == _shownSubjugateState) return;
            _shownSubjugateState = state;

            subjugateButton.interactable = _subjugationPanel != null;
            if (subjugateBackground != null)
                subjugateBackground.color = _subjugationPanel == null ? buttonOff
                                          : (open ? buttonOn : buttonNormal);
            if (subjugateLabel != null)
                subjugateLabel.text = open ? subjugateOpen
                                    : found > 0 ? string.Format(subjugateFound, found)
                                    : subjugateNone;
        }

        void RefreshSquad(bool force)
        {
            if (squadButton == null) return;

            bool open = _squadPanel != null && _squadPanel.IsOpen;
            var rally = RallyPointService.Instance;
            bool picking = rally != null && rally.IsPicking;

            // 열림/지정중 두 상태를 한 값으로 접어 바뀔 때만 갱신한다.
            int state = (open ? 1 : 0) | (picking ? 2 : 0);
            if (!force && state == _shownSquadState) return;
            _shownSquadState = state;

            squadButton.interactable = _squadPanel != null;
            if (squadBackground != null)
                squadBackground.color = _squadPanel == null ? buttonOff
                                      : (open || picking ? buttonOn : buttonNormal);
            if (squadLabel != null)
                squadLabel.text = picking ? squadPicking : (open ? squadOpen : squadIdle);
        }

        void RefreshTactics(bool force)
        {
            if (tacticsButton == null) return;

            bool open = _tacticsPanel != null && _tacticsPanel.IsOpen;
            if (!force && open == _shownTacticsOpen) return;
            _shownTacticsOpen = open;

            tacticsButton.interactable = _tacticsPanel != null;
            if (tacticsBackground != null)
                tacticsBackground.color = _tacticsPanel == null ? buttonOff
                                        : (open ? buttonOn : buttonNormal);
            if (tacticsLabel != null) tacticsLabel.text = open ? tacticsOpen : tacticsIdle;
        }

        void RefreshCreate(bool force)
        {
            if (createButton == null) return;
            if (_creation == null) _creation = CharacterCreationService.Instance;

            if (_creation == null)
            {
                if (!force) return;
                createButton.interactable = false;
                if (createBackground != null) createBackground.color = buttonOff;
                return;
            }

            bool atLimit = _creation.AtLimit;
            int cost = _creation.CurrentCost;
            bool can = _creation.CanCreate;

            if (!force && cost == _shownCost && can == _shownCanCreate) return;
            _shownCost = cost;
            _shownCanCreate = can;

            createButton.interactable = can;
            if (createBackground != null) createBackground.color = can ? buttonNormal : buttonOff;
            if (createLabel != null)
                createLabel.text = atLimit ? createAtLimit : string.Format(createFormat, cost);
        }
    }
}
