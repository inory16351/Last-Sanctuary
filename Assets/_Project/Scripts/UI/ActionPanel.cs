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

        [Header("문구")]
        [SerializeField] string createFormat = "캐릭터 생성 {0}";
        [SerializeField] string createAtLimit = "인원 상한";
        [SerializeField] string rallyIdle = "집결지 설정";
        [SerializeField] string rallyPicking = "맵을 클릭 (Esc 취소)";
        [SerializeField] string rallyClear = "집결지 해제 (우클릭)";

        [Header("색")]
        [SerializeField] Color buttonNormal = new Color(0.13f, 0.17f, 0.22f, 0.95f);
        [SerializeField] Color buttonOn = new Color(0.16f, 0.42f, 0.38f, 0.98f);
        [SerializeField] Color buttonOff = new Color(0.10f, 0.11f, 0.13f, 0.85f);

        CharacterCreationService _creation;
        RallyPointService _rally;

        // 마지막으로 화면에 반영한 값. 바뀔 때만 갱신한다.
        int _shownCost = int.MinValue;
        bool _shownCanCreate;
        bool _shownPicking;
        bool _shownHasRally;

        void Start()
        {
            _creation = CharacterCreationService.Instance;
            _rally = RallyPointService.Instance;

            // MCP 로는 씬 오브젝트 참조를 인스펙터에 넣을 수 없어서(진행상황 8절 4번),
            // 비어 있으면 이름으로 찾는다.
            Resolve("Buttons/CreateButton", ref createButton, ref createBackground, ref createLabel);
            Resolve("Buttons/RallyButton", ref rallyButton, ref rallyBackground, ref rallyLabel);

            if (createButton != null) createButton.onClick.AddListener(HandleCreate);
            if (rallyButton != null) rallyButton.onClick.AddListener(HandleRally);

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

        void HandleRally()
        {
            if (_rally == null) _rally = RallyPointService.Instance;
            _rally?.TogglePicking();
            Refresh(force: true);
        }

        void Refresh(bool force)
        {
            RefreshCreate(force);
            RefreshRally(force);
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
            if (rallyLabel != null)
                rallyLabel.text = picking ? rallyPicking : (hasRally ? rallyClear : rallyIdle);
        }
    }
}
