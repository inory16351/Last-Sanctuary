using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 우측 액션 버튼 묶음 — 캐릭터 생성 / 집결지 설정.
    /// (캐릭터 강화 버튼은 기존 <see cref="UpgradeButtonUI"/> 가 그대로 담당한다.
    ///  같은 기능을 두 벌 만들지 않는다 — 준수사항 §10 H-3.)
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

        [SerializeField] Button rallyButton;
        [SerializeField] TMP_Text rallyLabel;
        [SerializeField] Image rallyBackground;

        [SerializeField] Button rallyClearButton;
        [SerializeField] TMP_Text rallyClearLabel;
        [SerializeField] Image rallyClearBackground;

        [SerializeField] Button tacticsButton;
        [SerializeField] TMP_Text tacticsLabel;
        [SerializeField] Image tacticsBackground;

        [SerializeField] Button squadButton;
        [SerializeField] TMP_Text squadLabel;
        [SerializeField] Image squadBackground;

        [Header("문구")]
        [SerializeField] string createFormat = "캐릭터 생성 {0}";
        [SerializeField] string createAtLimit = "인원 상한";
        [SerializeField] string rallyIdle = "집결지 생성";
        [SerializeField] string rallyPicking = "맵을 클릭 (Esc 취소)";
        [SerializeField] string rallyClear = "집결지 해제";
        [SerializeField] string tacticsIdle = "전술 지침";
        [SerializeField] string tacticsOpen = "전술 지침 닫기";
        [SerializeField] string squadIdle = "부대 지정";
        [SerializeField] string squadOpen = "부대 지정 닫기";

        [Header("색")]
        [SerializeField] Color buttonNormal = new Color(0.13f, 0.17f, 0.22f, 0.95f);
        [SerializeField] Color buttonOn = new Color(0.16f, 0.42f, 0.38f, 0.98f);
        [SerializeField] Color buttonOff = new Color(0.10f, 0.11f, 0.13f, 0.85f);

        CharacterCreationService _creation;
        RallyPointService _rally;

        /// <summary>전술 지침 창. <b>평소 비활성</b>이라 이름/타입 조회에 비활성 포함이 필요하다.</summary>
        TacticalOrderPanel _tacticsPanel;

        /// <summary>부대 지정 창. 전술 지침 창과 같은 이유로 비활성 포함 조회가 필요하다.</summary>
        SquadPanel _squadPanel;

        // 마지막으로 화면에 반영한 값. 바뀔 때만 갱신한다.
        int _shownCost = int.MinValue;
        bool _shownCanCreate;
        bool _shownPicking;
        bool _shownHasRally;
        bool _shownTacticsOpen;
        bool _shownSquadOpen;

        void Start()
        {
            _creation = CharacterCreationService.Instance;
            _rally = RallyPointService.Instance;

            // 전술 지침 창은 버튼을 누르기 전까지 비활성이므로 Instance(=Awake 에서 설정)가
            // 아직 없다. 비활성 오브젝트까지 포함해 직접 찾는다.
            _tacticsPanel = FindAnyObjectByType<TacticalOrderPanel>(FindObjectsInactive.Include);
            _squadPanel = FindAnyObjectByType<SquadPanel>(FindObjectsInactive.Include);

            // MCP 로는 씬 오브젝트 참조를 인스펙터에 넣을 수 없어서(진행상황 8절 4번),
            // 비어 있으면 이름으로 찾는다.
            Resolve("Buttons/CreateButton", ref createButton, ref createBackground, ref createLabel);
            Resolve("Buttons/RallyButton", ref rallyButton, ref rallyBackground, ref rallyLabel);
            Resolve("Buttons/RallyClearButton", ref rallyClearButton, ref rallyClearBackground, ref rallyClearLabel);
            Resolve("Buttons/TacticsButton", ref tacticsButton, ref tacticsBackground, ref tacticsLabel);
            Resolve("Buttons/SquadButton", ref squadButton, ref squadBackground, ref squadLabel);

            if (createButton != null) createButton.onClick.AddListener(HandleCreate);
            if (rallyButton != null) rallyButton.onClick.AddListener(HandleRally);
            if (rallyClearButton != null) rallyClearButton.onClick.AddListener(HandleRallyClear);
            if (tacticsButton != null) tacticsButton.onClick.AddListener(HandleTactics);
            if (squadButton != null) squadButton.onClick.AddListener(HandleSquad);

            if (_creation == null)
                Debug.LogWarning("[Actions] CharacterCreationService 를 찾지 못했습니다. " +
                                 "GameSystems 오브젝트에 붙어 있는지 확인하세요.", this);
            if (_rally == null)
                Debug.LogWarning("[Actions] RallyPointService 를 찾지 못했습니다.", this);

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

        /// <summary>집결지 <b>생성</b> — 맵을 클릭해 새 집결지를 하나 찍는다(여러 개 만들 수 있다).</summary>
        void HandleRally()
        {
            if (_rally == null) _rally = RallyPointService.Instance;
            _rally?.TogglePicking();
            Refresh(force: true);
        }

        /// <summary>
        /// 집결지 <b>해제</b> — 생성과 별도 버튼으로 갈랐다(유저 확정 2026-08-11).
        /// 캐릭터를 고른 채 누르면 그 캐릭터 것만, 아무것도 안 고른 채 누르면 전부 지운다.
        /// </summary>
        void HandleRallyClear()
        {
            if (_rally == null) _rally = RallyPointService.Instance;
            _rally?.CancelPicking();
            _rally?.ClearForCurrentTarget();
            Refresh(force: true);
        }

        /// <summary>부대 지정 창을 연다/닫는다.</summary>
        void HandleSquad()
        {
            if (_squadPanel == null)
                _squadPanel = FindAnyObjectByType<SquadPanel>(FindObjectsInactive.Include);

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

        void Refresh(bool force)
        {
            RefreshCreate(force);
            RefreshRally(force);
            RefreshTactics(force);
            RefreshSquad(force);
        }

        void RefreshSquad(bool force)
        {
            if (squadButton == null) return;

            bool open = _squadPanel != null && _squadPanel.IsOpen;
            if (!force && open == _shownSquadOpen) return;
            _shownSquadOpen = open;

            squadButton.interactable = _squadPanel != null;
            if (squadBackground != null)
                squadBackground.color = _squadPanel == null ? buttonOff
                                      : (open ? buttonOn : buttonNormal);
            if (squadLabel != null) squadLabel.text = open ? squadOpen : squadIdle;
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

        void RefreshRally(bool force)
        {
            if (rallyButton == null) return;
            if (_rally == null) _rally = RallyPointService.Instance;

            if (_rally == null)
            {
                if (!force) return;
                rallyButton.interactable = false;
                if (rallyBackground != null) rallyBackground.color = buttonOff;
                return;
            }

            bool picking = _rally.IsPicking;
            bool hasRally = _rally.HasAnyRally;

            if (!force && picking == _shownPicking && hasRally == _shownHasRally) return;
            _shownPicking = picking;
            _shownHasRally = hasRally;

            rallyButton.interactable = true;
            if (rallyBackground != null) rallyBackground.color = picking ? buttonOn : buttonNormal;
            if (rallyLabel != null) rallyLabel.text = picking ? rallyPicking : rallyIdle;

            // 해제 버튼은 지울 것이 있을 때만 눌린다.
            if (rallyClearButton != null)
            {
                rallyClearButton.interactable = hasRally;
                if (rallyClearBackground != null)
                    rallyClearBackground.color = hasRally ? buttonNormal : buttonOff;
                if (rallyClearLabel != null) rallyClearLabel.text = rallyClear;
            }
        }
    }
}
