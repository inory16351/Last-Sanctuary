using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Buildings;

namespace LastSanctuary.UI
{
    /// <summary>
    /// "건물 건설" 버튼 — <b>캐릭터 성장 버튼 바로 아래</b>에 있다(유저 요청).
    ///
    /// 누르면 <see cref="BuildService"/> 의 자리 지정 모드를 켜고 끈다. 조작 방식은
    /// "집결지 설정"(<see cref="RallyPointService"/> · <see cref="ActionPanel"/>)과 같다 —
    /// 켜면 마우스를 따라 2x2 미리보기가 뜨고, 맵을 클릭하면 그 자리가 건설 예정지가 된다.
    ///
    /// 실제로 짓는 것은 캐릭터다(<c>CharacterBehavior.TryBuild</c>) — 이 버튼은
    /// "어디에 지을지"만 받는다. 규칙 판단은 전부 서비스에 있고 여기서는
    /// "언제 눌릴 수 있는지"와 "라벨에 뭘 쓸지"만 다룬다
    /// (<see cref="UpgradeButtonUI"/> · <see cref="ActionPanel"/> 과 같은 구조).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class BuildButtonUI : MonoBehaviour
    {
        [Header("연결 (비워두면 자동으로 찾는다)")]
        [SerializeField] TMP_Text label;

        [Header("문구")]
        [Tooltip("{0} 자리에 다음 건설 비용이 들어간다")]
        [SerializeField] string idleFormat = "건물 건설 {0}";
        [SerializeField] string pickingText = "자리 지정 중 (Esc 취소)";
        [SerializeField] string atLimitText = "건설 상한";

        [Header("색")]
        [SerializeField] Color buttonNormal = new Color(0.13f, 0.17f, 0.22f, 0.95f);
        [SerializeField] Color buttonOn = new Color(0.16f, 0.42f, 0.38f, 0.98f);
        [SerializeField] Color buttonOff = new Color(0.10f, 0.11f, 0.13f, 0.85f);

        Button _button;
        Image _background;
        BuildService _service;

        // 마지막으로 화면에 반영한 값. 바뀔 때만 갱신한다 (TMP 는 대입할 때마다 메시를 다시 굽는다).
        int _shownCost = int.MinValue;
        bool _shownPicking;
        bool _shownCanPlace;

        void Awake()
        {
            _button = GetComponent<Button>();
            _background = GetComponent<Image>();
            if (label == null) label = GetComponentInChildren<TMP_Text>();
        }

        void Start()
        {
            // ★★ 기능이 꺼져 있으면 <b>버튼 자체를 숨긴다</b> (2026-08-20 · 유저 지시
            //   *"타워랑 건설 기능 삭제"* → «플레이에서만 제거»).
            //
            //   ⚠ «비활성(interactable = false)» 으로 두면 <b>회색 버튼이 남는다</b> —
            //     유저에게는 «고장난 기능» 으로 보인다. 지시는 «없애» 였으므로 감춘다.
            //   ★ 판단은 <see cref="BuildService.FeatureEnabled"/> 로 한다 —
            //     «서비스가 아직 안 깨어났다»(Instance == null) 와 구별해야 한다.
            //     구별하지 않으면 초기화 순서에 따라 버튼이 깜빡인다.
            if (!BuildService.FeatureEnabled)
            {
                gameObject.SetActive(false);
                return;
            }

            _service = BuildService.Instance;
            if (_service == null)
                Debug.LogWarning("[BuildButton] BuildService 를 찾지 못했습니다. " +
                                 "GameSystems 오브젝트에 붙어 있는지 확인하세요.", this);

            _button.onClick.AddListener(HandleClick);
            Refresh(force: true);
        }

        void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClick);
        }

        void Update() => Refresh(force: false);

        void HandleClick()
        {
            if (_service == null) _service = BuildService.Instance;
            _service?.TogglePicking();
            Refresh(force: true);
        }

        void Refresh(bool force)
        {
            if (_service == null) _service = BuildService.Instance;

            if (_service == null)
            {
                if (!force) return;
                _button.interactable = false;
                if (_background != null) _background.color = buttonOff;
                return;
            }

            bool picking = _service.IsPicking;
            bool canPlace = _service.CanPlace;
            int cost = _service.CurrentCost;

            if (!force && picking == _shownPicking && canPlace == _shownCanPlace && cost == _shownCost)
                return;

            _shownPicking = picking;
            _shownCanPlace = canPlace;
            _shownCost = cost;

            // 지정 모드 중에는 (취소하려면 눌러야 하므로) 항상 누를 수 있어야 한다.
            _button.interactable = picking || canPlace;
            if (_background != null)
                _background.color = picking ? buttonOn : (canPlace ? buttonNormal : buttonOff);

            if (label != null)
                label.text = picking ? pickingText
                           : _service.AtLimit ? atLimitText
                           : string.Format(idleFormat, cost);
        }
    }
}
