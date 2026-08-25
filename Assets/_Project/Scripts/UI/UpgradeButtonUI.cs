using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastSanctuary.UI
{
    /// <summary>
    /// "캐릭터 성장" 버튼. <see cref="ActionPanel"/> 의 전술 지침 버튼과 완전히 같은 역할이다 —
    /// 캐릭터를 직접 강화하지 않고 <see cref="CharacterGrowthPanel"/> 을 열고 닫기만 한다
    /// (유저 확정: "전술 지침이랑 같은 로직으로"). 실제 강화(에너지 소비 + 능력치 성장)는
    /// 그 창의 "강화하기" 버튼이 맡는다 — 버튼 두 개가 같은 일을 하지 않게 하려는 것
    /// (준수사항 §10 H-3).
    ///
    /// 이전에는 이 버튼이 직접 <see cref="Units.CharacterUpgradeService.TryUpgrade"/> 를 불렀지만,
    /// 유저가 강화창을 요청하면서 그 역할이 창의 버튼으로 옮겨갔다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UpgradeButtonUI : MonoBehaviour
    {
        [Header("연결 (비워두면 자동으로 찾는다)")]
        [Tooltip("버튼 라벨. 비워두면 자식에서 TMP 텍스트를 찾아 쓴다")]
        [SerializeField] TMP_Text label;

        [Header("문구")]
        [SerializeField] string idleText = "캐릭터 성장";
        [SerializeField] string openText = "캐릭터 성장 닫기";

        [Header("색")]
        [SerializeField] Color buttonNormal = new Color(0.13f, 0.17f, 0.22f, 0.95f);
        [SerializeField] Color buttonOn = new Color(0.16f, 0.42f, 0.38f, 0.98f);
        [SerializeField] Color buttonOff = new Color(0.10f, 0.11f, 0.13f, 0.85f);

        Button _button;
        Image _background;
        CharacterGrowthPanel _panel;
        bool _shownOpen;

        void Awake()
        {
            _button = GetComponent<Button>();
            _background = GetComponent<Image>();
            if (label == null) label = GetComponentInChildren<TMP_Text>();
        }

        void Start()
        {
            // 강화창은 버튼을 누르기 전까지 비활성이므로 Instance(=Awake 에서 설정)가 아직 없을 수
            // 있다. 비활성 오브젝트까지 포함해 직접 찾는다(TacticalOrderPanel 과 같은 패턴).
            _panel = FindAnyObjectByType<CharacterGrowthPanel>(FindObjectsInactive.Include);
            if (_panel == null)
                Debug.LogWarning("[GrowthButton] CharacterGrowthPanel 을 찾지 못했습니다.", this);

            _button.onClick.AddListener(HandleClick);
            RefreshNow();
        }

        void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClick);
        }

        void Update()
        {
            if (_panel == null)
                _panel = FindAnyObjectByType<CharacterGrowthPanel>(FindObjectsInactive.Include);

            bool open = _panel != null && _panel.IsOpen;
            if (open == _shownOpen) return;
            Apply(open);
        }

        void RefreshNow()
        {
            _shownOpen = !( _panel != null && _panel.IsOpen );   // 다음 Update 가 무조건 갱신하도록
            Apply(_panel != null && _panel.IsOpen);
        }

        void Apply(bool open)
        {
            _shownOpen = open;
            _button.interactable = _panel != null;

            if (_background != null)
                _background.color = _panel == null ? buttonOff : (open ? buttonOn : buttonNormal);

            if (label != null) label.text = open ? openText : idleText;
        }

        void HandleClick()
        {
            // ★★ 이 버튼도 <b>허드 액션의 한 칸</b>이다 (2026-08-25 · 유저 지시: *"허드 액션의
            //   각 버튼을 최초로 눌렀을때 해당 기능에 대한 도움말이 등장"*). 파일만 갈라져
            //   있을 뿐 <see cref="ActionPanel"/> 의 버튼들과 같은 규칙을 따른다.
            //   ⚠ 창을 여는 일은 도움말 쪽이 표의 open_panel(<c>UI_Root/HUD_Growth</c>)로 한다.
            Help.HelpService help = Help.HelpService.Instance;
            if (help != null && help.InterceptFirstUse(Help.HelpTrigger.ActionUpgrade)) return;

            if (_panel == null)
                _panel = FindAnyObjectByType<CharacterGrowthPanel>(FindObjectsInactive.Include);

            _panel?.Toggle();
            RefreshNow();
        }
    }
}
