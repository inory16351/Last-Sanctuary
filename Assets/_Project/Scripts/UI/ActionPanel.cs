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

        [Header("유물 관리 (2026-08-23)")]
        [SerializeField] Button relicButton;
        [SerializeField] TMP_Text relicLabel;
        [SerializeField] Image relicBackground;

        [Header("도움말 (2026-08-24)")]
        [SerializeField] Button helpButton;
        [SerializeField] TMP_Text helpLabel;
        [SerializeField] Image helpBackground;

        [SerializeField] Button settingsButton;
        [SerializeField] TMP_Text settingsLabel;
        [SerializeField] Image settingsBackground;

        [Header("문구")]
        [SerializeField] string createFormat = "캐릭터 생성 {0}";
        [SerializeField] string createAtLimit = "인원 상한";

        [Tooltip("★ 등장할 인물이 남지 않았을 때. 인원 상한과 <b>다른 상태</b>다 — " +
                 "정원은 비어 있는데 «표에 남은 인물» 이 없는 것이라, 같은 문구를 쓰면 " +
                 "유저가 «누굴 죽여야 하나» 로 잘못 읽는다")]
        [SerializeField] string createOutOfCandidates = "등장할 인물 없음";
        [SerializeField] string tacticsIdle = "전술 지침";
        [SerializeField] string tacticsOpen = "전술 지침 닫기";
        [SerializeField] string squadIdle = "부대 설정";
        [SerializeField] string squadOpen = "부대 설정 닫기";
        [SerializeField] string squadPicking = "집결지 지정 중";
        [SerializeField] string subjugateOpen = "토벌 지시 닫기";
        [Tooltip("{0} = 발견한 에픽 몬스터 수. 0 이면 아래 subjugateNone 을 쓴다")]
        [SerializeField] string subjugateFound = "토벌 지시 ({0})";
        [SerializeField] string subjugateNone = "토벌 지시";
        [Tooltip("{0} = 아직 안 판 «발견한» 발굴 칸 수. 0 이면 아래 relicIdle 을 쓴다")]
        [SerializeField] string relicFound = "유물 관리 (발굴 {0})";
        [SerializeField] string relicIdle = "유물 관리";
        [SerializeField] string relicOpen = "유물 관리 닫기";
        [SerializeField] string settingsIdle = "환경 설정";
        [SerializeField] string settingsOpen = "환경 설정 닫기";
        [Tooltip("{0} = 아직 안 읽은 도움말 수. 0 이면 아래 helpIdle 을 쓴다")]
        [SerializeField] string helpUnread = "도움말 (새 {0})";
        [SerializeField] string helpIdle = "도움말 (F1)";
        [SerializeField] string helpOpen = "도움말 닫기";

        [Header("칸 높이 (2026-08-24 — 하드코딩 제거)")]
        [Tooltip("켜면 <b>켜져 있는 버튼 수</b>에 맞춰 이 패널의 높이를 스스로 맞춘다.\n" +
                 "예전에는 씬에 박힌 값이라 버튼이 하나 늘 때마다 <b>칸 밖으로 넘쳤다</b> " +
                 "— 유물 관리를 더한 뒤 8개가 되어 96px 이 잘려 나갔다(2026-08-24 실측)")]
        [SerializeField] bool autoFitHeight = true;

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

        /// <summary>환경 설정 창(2026-08-18 신설). 위 셋과 같은 이유로 비활성 포함 조회.</summary>
        SettingsPanel _settingsPanel;

        /// <summary>유물 관리 창(2026-08-23 신설). 위 넷과 같은 이유로 비활성 포함 조회.</summary>
        RelicPanel _relicPanel;

        /// <summary>도움말 창(2026-08-24 신설). 위 다섯과 같은 이유로 비활성 포함 조회.</summary>
        HelpPanel _helpPanel;

        // 마지막으로 화면에 반영한 값. 바뀔 때만 갱신한다.
        int _shownCost = int.MinValue;
        bool _shownCanCreate;
        bool _shownOutOfCandidates;
        bool _shownTacticsOpen;
        int _shownSquadState = int.MinValue;
        int _shownSubjugateState = int.MinValue;
        int _shownSettingsState = int.MinValue;
        int _shownRelicState = int.MinValue;
        int _shownHelpState = int.MinValue;

        void Start()
        {
            _creation = CharacterCreationService.Instance;

            // 전술 지침·부대 설정 창은 버튼을 누르기 전까지 비활성이므로 Instance(=Awake 에서 설정)가
            // 아직 없다. 비활성 오브젝트까지 포함해 직접 찾는다.
            _tacticsPanel = FindAnyObjectByType<TacticalOrderPanel>(FindObjectsInactive.Include);
            _squadPanel = FindAnyObjectByType<SquadPanel>(FindObjectsInactive.Include);
            _subjugationPanel = FindAnyObjectByType<SubjugationPanel>(FindObjectsInactive.Include);
            _settingsPanel = FindAnyObjectByType<SettingsPanel>(FindObjectsInactive.Include);
            _relicPanel = FindAnyObjectByType<RelicPanel>(FindObjectsInactive.Include);
            _helpPanel = FindAnyObjectByType<HelpPanel>(FindObjectsInactive.Include);

            // MCP 로는 씬 오브젝트 참조를 인스펙터에 넣을 수 없어서(진행상황 8절 4번),
            // 비어 있으면 이름으로 찾는다.
            Resolve("Buttons/CreateButton", ref createButton, ref createBackground, ref createLabel);
            Resolve("Buttons/TacticsButton", ref tacticsButton, ref tacticsBackground, ref tacticsLabel);
            Resolve("Buttons/SquadButton", ref squadButton, ref squadBackground, ref squadLabel);
            Resolve("Buttons/SubjugateButton", ref subjugateButton, ref subjugateBackground, ref subjugateLabel);
            Resolve("Buttons/SettingsButton", ref settingsButton, ref settingsBackground, ref settingsLabel);
            Resolve("Buttons/RelicButton", ref relicButton, ref relicBackground, ref relicLabel);
            Resolve("Buttons/HelpButton", ref helpButton, ref helpBackground, ref helpLabel);

            // 창들은 "닫힌 채로 시작"이 규칙이다. 창 스스로 Awake 에서 닫으면
            // <b>열리는 순간 닫히는</b> 버그가 되므로(UnitPortraitPanel.Awake 주석),
            // 항상 살아 있는 이쪽에서 한 번 확인해 닫는다.
            // ★ 2026-08-25 — 전술 지침·부대 설정이 <b>이 목록에서 빠져 있었다</b>. 씬 값이 마침
            //   비활성이라 드러나지 않았을 뿐, «닫힌 채로 시작» 이 규칙이면 여섯 창이 다 여기 있어야
            //   한다(창을 켠 채 씬을 저장하는 사고는 실제로 일어난다 — EventPanel.Start 의 주석).
            if (_tacticsPanel != null && _tacticsPanel.IsOpen) _tacticsPanel.Close();
            if (_squadPanel != null && _squadPanel.IsOpen) _squadPanel.Close();
            if (_subjugationPanel != null && _subjugationPanel.IsOpen) _subjugationPanel.Close();
            if (_settingsPanel != null && _settingsPanel.IsOpen) _settingsPanel.Close();
            if (_relicPanel != null && _relicPanel.IsOpen) _relicPanel.Close();
            if (_helpPanel != null && _helpPanel.IsOpen) _helpPanel.Close();

            if (createButton != null) createButton.onClick.AddListener(HandleCreate);
            if (tacticsButton != null) tacticsButton.onClick.AddListener(HandleTactics);
            if (squadButton != null) squadButton.onClick.AddListener(HandleSquad);
            if (subjugateButton != null) subjugateButton.onClick.AddListener(HandleSubjugate);
            if (settingsButton != null) settingsButton.onClick.AddListener(HandleSettings);
            if (relicButton != null) relicButton.onClick.AddListener(HandleRelic);
            if (helpButton != null) helpButton.onClick.AddListener(HandleHelp);

            if (_creation == null)
                Debug.LogWarning("[Actions] CharacterCreationService 를 찾지 못했습니다. " +
                                 "GameSystems 오브젝트에 붙어 있는지 확인하세요.", this);

            FitHeight();
            Refresh(force: true);
        }

        void Update() => Refresh(force: false);

        /// <summary>
        /// ★★ <b>패널 높이를 버튼 수에서 계산한다</b> (2026-08-24 · 유저 지시:
        /// <i>"지금 허드 액션이 하드 코딩 되어 있음 … 허드 액션 크기 맞춰"</i>).
        ///
        /// 예전에는 씬의 <c>sizeDelta.y</c> 가 정본이었다. 버튼을 하나 더할 때 그 값을 같이
        /// 고치지 않으면 <b>조용히 칸 밖으로 넘친다</b> — 실제로 유물 관리 버튼이 들어오면서
        /// 8개가 되어 <b>96px 이 잘려 있었다</b>. 세는 것이 기억하는 것보다 안전하다.
        ///
        /// ★ 높이는 <see cref="LayoutElement.preferredHeight"/> 를 먼저 본다 —
        ///   <see cref="VerticalLayoutGroup"/> 가 실제로 쓰는 값이 그것이다(칸의 현재 높이는
        ///   레이아웃이 아직 돌기 전이면 엉뚱한 값일 수 있다).
        /// ⚠ <c>Buttons</c> 는 <b>늘어나는(stretch) 자식</b>이라 자기 높이가
        ///   <c>부모 높이 + sizeDelta.y</c> 다. 그래서 부모에게 필요한 높이는
        ///   <c>내용 높이 − sizeDelta.y</c> 다(sizeDelta.y 가 −20 이면 20 을 더하는 셈).
        /// </summary>
        void FitHeight()
        {
            if (!autoFitHeight) return;

            var self = transform as RectTransform;
            var box = transform.Find("Buttons") as RectTransform;
            if (self == null || box == null) return;

            // 세로로 늘어나 있지 않으면 이 계산이 성립하지 않는다 — 손대지 않는다.
            if (!Mathf.Approximately(box.anchorMin.y, 0f) ||
                !Mathf.Approximately(box.anchorMax.y, 1f)) return;

            var layout = box.GetComponent<VerticalLayoutGroup>();
            float spacing = layout != null ? layout.spacing : 0f;
            float padding = layout != null ? layout.padding.top + layout.padding.bottom : 0f;

            float content = padding;
            int shown = 0;
            for (int i = 0; i < box.childCount; i++)
            {
                var child = box.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf) continue;

                var element = child.GetComponent<LayoutElement>();
                float h = element != null && element.preferredHeight > 0f
                        ? element.preferredHeight
                        : child.rect.height;

                content += h;
                shown++;
            }
            if (shown == 0) return;
            content += spacing * (shown - 1);

            float want = content - box.sizeDelta.y;
            if (Mathf.Abs(want - self.sizeDelta.y) < 0.5f) return;

            self.sizeDelta = new Vector2(self.sizeDelta.x, want);
        }

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

        // ══════════════════════════════════════════════════════════════
        //  ★★★ 버튼을 <b>처음</b> 누르면 도움말이 먼저 뜬다 (2026-08-25)
        // ══════════════════════════════════════════════════════════════
        // 유저 지시: *"허드 액션의 각 버튼을 <b>최초로 눌렀을때</b> 해당 기능에 대한 도움말이
        // 등장하는 것으로 진행"* · *"자세히 보기를 눌렀을 때 <b>실제 해당 ui가 켜지고</b>
        // 각 기능에 대한 설명 시작"*.
        //
        // ★ 여기 늘어놓는 것은 <b>한 줄뿐</b>이다 — 창을 여는 일도, 다시 여는 일도
        //   <see cref="Help.HelpService.InterceptFirstUse"/> 쪽이 표의 <c>open_panel</c> 을
        //   보고 한다. 이 파일은 «어느 버튼이 어느 계기인가» 만 안다.
        // ⚠ <b>두 번째부터는 아무 일도 없다</b>(이미 읽은 항목이면 false 를 돌려준다).
        //   그래서 평소 조작이 느려지거나 막히지 않는다.

        /// <summary>도움말이 이 클릭을 가로챘으면 <c>true</c> — 부르는 쪽은 그대로 돌아간다.</summary>
        static bool Intercept(Help.HelpTrigger trigger, System.Action continueAction = null)
        {
            Help.HelpService service = Help.HelpService.Instance;
            return service != null && service.InterceptFirstUse(trigger, continueAction);
        }

        void HandleCreate()
        {
            // ⚠ 캐릭터 생성은 <b>창을 열지 않는</b> 유일한 액션이다 — 표의 open_panel 이 비어
            //   있으므로 «원래 하려던 일» 을 직접 넘긴다(그것이 없으면 도움말을 읽은 대가로
            //   «버튼이 한 번 안 먹는» 일이 된다).
            if (Intercept(Help.HelpTrigger.ActionCreate, CreateNow)) return;
            CreateNow();
        }

        void CreateNow()
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
            if (Intercept(Help.HelpTrigger.ActionSquad)) return;

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
            if (Intercept(Help.HelpTrigger.ActionTactics)) return;

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
            if (Intercept(Help.HelpTrigger.ActionSubjugate)) return;

            if (_subjugationPanel == null)
                _subjugationPanel = FindAnyObjectByType<SubjugationPanel>(FindObjectsInactive.Include);

            var rally = RallyPointService.Instance;
            if (rally != null && rally.IsPicking) rally.CancelPicking();

            _subjugationPanel?.Toggle();
            Refresh(force: true);
        }

        /// <summary>
        /// 환경 설정 창을 연다/닫는다. 부대·토벌과 같은 이유로 <b>집결지 지정 중이면 먼저 취소</b>한다.
        /// </summary>
        void HandleSettings()
        {
            if (Intercept(Help.HelpTrigger.ActionSettings)) return;

            if (_settingsPanel == null)
                _settingsPanel = FindAnyObjectByType<SettingsPanel>(FindObjectsInactive.Include);

            var rally = RallyPointService.Instance;
            if (rally != null && rally.IsPicking) rally.CancelPicking();

            _settingsPanel?.Toggle();
            Refresh(force: true);
        }

        void Refresh(bool force)
        {
            RefreshCreate(force);
            RefreshTactics(force);
            RefreshSquad(force);
            RefreshSubjugate(force);
            RefreshSettings(force);
            RefreshRelic(force);
            RefreshHelp(force);
        }

        /// <summary>
        /// ★ 도움말 버튼 — 열림 여부와 <b>아직 안 읽은 항목 수</b>를 보여준다 (2026-08-24).
        /// 토벌·유물 버튼이 «지금 할 일이 있는가» 를 숫자로 보여주는 것과 같은 규칙이다:
        /// 창을 열지 않아도 «새로 읽을 것이 있는가» 를 알 수 있어야 한다.
        /// </summary>
        void RefreshHelp(bool force)
        {
            if (helpButton == null) return;

            bool open = _helpPanel != null && _helpPanel.IsOpen;
            Help.HelpService service = Help.HelpService.Instance;
            int unread = service != null ? Mathf.Max(0, service.TotalCount - service.SeenCount) : 0;

            int state = (open ? 1 : 0) + unread * 2;
            if (!force && state == _shownHelpState) return;
            _shownHelpState = state;

            helpButton.interactable = _helpPanel != null;
            if (helpBackground != null)
                helpBackground.color = _helpPanel == null ? buttonOff
                                     : (open ? buttonOn : buttonNormal);
            if (helpLabel != null)
                helpLabel.text = open ? helpOpen
                               : unread > 0 ? string.Format(helpUnread, unread)
                               : helpIdle;
        }

        /// <summary>도움말 창을 연다/닫는다 — 다른 창들과 같은 구조. F1 도 같은 일을 한다.</summary>
        void HandleHelp()
        {
            if (_helpPanel == null)
                _helpPanel = FindAnyObjectByType<HelpPanel>(FindObjectsInactive.Include);
            _helpPanel?.Toggle();
            Refresh(force: true);
        }

        /// <summary>
        /// ★ 유물 관리 버튼 — 열림 여부와 <b>아직 안 판 발굴 칸 수</b>를 보여준다
        /// (2026-08-23). 토벌 버튼이 «발견한 에픽 수» 를 보여주는 것과 같은 규칙이다:
        /// 창을 열지 않아도 «지금 할 일이 있는가» 를 알 수 있어야 한다.
        /// </summary>
        void RefreshRelic(bool force)
        {
            if (relicButton == null) return;

            bool open = _relicPanel != null && _relicPanel.IsOpen;
            var dig = Relics.RelicDigService.Instance;
            int found = dig != null ? dig.RevealedCount : 0;

            int state = (open ? 1 : 0) + found * 2;
            if (!force && state == _shownRelicState) return;
            _shownRelicState = state;

            relicButton.interactable = _relicPanel != null;
            if (relicBackground != null)
                relicBackground.color = _relicPanel == null ? buttonOff
                                      : (open ? buttonOn : buttonNormal);
            if (relicLabel != null)
                relicLabel.text = open ? relicOpen
                                : found > 0 ? string.Format(relicFound, found)
                                : relicIdle;
        }

        /// <summary>유물 관리 창을 연다/닫는다 — 다른 창들과 같은 구조.</summary>
        void HandleRelic()
        {
            if (Intercept(Help.HelpTrigger.ActionRelic)) return;

            if (_relicPanel == null)
                _relicPanel = FindAnyObjectByType<RelicPanel>(FindObjectsInactive.Include);
            _helpPanel = FindAnyObjectByType<HelpPanel>(FindObjectsInactive.Include);
            _relicPanel?.Toggle();
            Refresh(force: true);
        }

        /// <summary>환경 설정 버튼 — 열림 여부만 보여준다(다른 창들과 같은 구조).</summary>
        void RefreshSettings(bool force)
        {
            if (settingsButton == null) return;

            bool open = _settingsPanel != null && _settingsPanel.IsOpen;
            int state = open ? 1 : 0;
            if (!force && state == _shownSettingsState) return;
            _shownSettingsState = state;

            settingsButton.interactable = _settingsPanel != null;
            if (settingsBackground != null)
                settingsBackground.color = _settingsPanel == null ? buttonOff
                                         : (open ? buttonOn : buttonNormal);
            if (settingsLabel != null)
                settingsLabel.text = open ? settingsOpen : settingsIdle;
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
            bool noCandidates = _creation.OutOfCandidates;

            if (!force && cost == _shownCost && can == _shownCanCreate &&
                noCandidates == _shownOutOfCandidates) return;
            _shownCost = cost;
            _shownCanCreate = can;
            _shownOutOfCandidates = noCandidates;

            createButton.interactable = can;
            if (createBackground != null) createBackground.color = can ? buttonNormal : buttonOff;
            if (createLabel != null)
                // ★★ <b>«더 나올 인물이 없다» 를 화면에 적는다</b> (2026-08-21).
                //   ⚠ 예전에는 이 상태에서도 «캐릭터 생성 170» 이 그대로 떠 있고 버튼만
                //     회색이 됐다. 그리고 <c>interactable = false</c> 라 클릭이 안 되므로
                //     <c>CharacterCreationService.TryCreate</c> 안의 설명 로그
                //     («더 등장할 인물이 없습니다»)에 <b>도달할 방법이 없었다</b> —
                //     즉 이 상태를 유저에게 알리는 통로가 <b>하나도</b> 없었다.
                //   ★ 판정은 서비스에 그대로 두고 여기서는 <b>읽기만</b> 한다
                //     (인원 상한 <c>createAtLimit</c> 과 같은 모양).
                createLabel.text = noCandidates ? createOutOfCandidates
                                 : atLimit ? createAtLimit
                                 : string.Format(createFormat, cost);
        }
    }
}
