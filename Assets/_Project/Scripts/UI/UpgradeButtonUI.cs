using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Resource;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 강화 버튼. 캐릭터를 선택하면 활성화되고, 누르면 에너지를 소비해 능력치를 성장시킨다.
    ///
    /// 규칙 판단은 전부 <see cref="CharacterUpgradeService"/> 에 있고 이 컴포넌트는
    /// "언제 눌릴 수 있는지"와 "라벨에 무엇을 쓸지"만 다룬다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UpgradeButtonUI : MonoBehaviour
    {
        [Header("연결 (비워두면 자동으로 찾는다)")]
        [Tooltip("버튼 라벨. 비워두면 자식에서 TMP 텍스트를 찾아 쓴다")]
        [SerializeField] TMP_Text label;

        [Header("활성화 조건")]
        [Tooltip("에너지가 부족하면 선택했더라도 버튼을 비활성으로 둔다. " +
                 "끄면 선택만으로 활성화되고, 눌러도 부족하면 아무 일도 일어나지 않는다")]
        [SerializeField] bool requireAffordable = true;

        [Tooltip("켜면 선택된 캐릭터가 없을 때 버튼을 아예 숨긴다(기본은 회색으로 비활성)")]
        [SerializeField] bool hideWhenNoSelection = false;

        [Header("라벨")]
        [Tooltip("라벨에 강화 비용을 표시한다. {0} 자리에 비용이 들어간다")]
        [SerializeField] bool showCostOnLabel = true;
        [SerializeField] string costFormat = "강화 {0}";

        [Tooltip("선택된 캐릭터가 없을 때 라벨에 쓸 문구")]
        [SerializeField] string noSelectionLabel = "강화";

        Button _button;
        UnitSelector _selector;
        CharacterUpgradeService _upgrades;
        ResourceManager _resources;

        // 마지막으로 라벨/활성상태에 반영한 값. 바뀔 때만 갱신한다.
        CharacterUnit _shownUnit;
        int _shownCost = -1;
        bool _shownInteractable;

        void Awake()
        {
            _button = GetComponent<Button>();
            if (label == null) label = GetComponentInChildren<TMP_Text>();
        }

        void Start()
        {
            _selector = UnitSelector.Instance;
            _upgrades = CharacterUpgradeService.Instance;
            _resources = ResourceManager.Instance;

            if (_selector == null)
                Debug.LogWarning("[UpgradeButton] UnitSelector 를 찾지 못했습니다.", this);
            if (_upgrades == null)
                Debug.LogWarning("[UpgradeButton] CharacterUpgradeService 를 찾지 못했습니다.", this);

            _button.onClick.AddListener(HandleClick);
            RefreshNow();
        }

        void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClick);
        }

        /// <summary>
        /// 선택 상태와 보유 에너지 둘 다 수시로 변하므로 매 프레임 확인하되,
        /// 실제로 바뀐 것이 있을 때만 버튼/라벨을 건드린다.
        /// </summary>
        void Update()
        {
            CharacterUnit unit = _selector != null ? _selector.Selected : null;
            int cost = unit != null && _upgrades != null ? _upgrades.CostFor(unit) : -1;
            bool interactable = unit != null &&
                                (!requireAffordable || (_upgrades != null && _upgrades.CanUpgrade(unit)));

            if (ReferenceEquals(unit, _shownUnit) && cost == _shownCost &&
                interactable == _shownInteractable)
                return;

            _shownUnit = unit;
            _shownCost = cost;
            _shownInteractable = interactable;
            Apply(unit, cost, interactable);
        }

        void RefreshNow()
        {
            _shownCost = -2;    // 다음 Update 가 무조건 갱신하도록
            Update();
        }

        void Apply(CharacterUnit unit, int cost, bool interactable)
        {
            if (hideWhenNoSelection && _button.gameObject.activeSelf != (unit != null))
                _button.gameObject.SetActive(unit != null);

            _button.interactable = interactable;

            if (label == null) return;
            label.text = unit != null && showCostOnLabel
                ? string.Format(costFormat, cost)
                : noSelectionLabel;
        }

        void HandleClick()
        {
            if (_selector == null || _upgrades == null) return;

            CharacterUnit unit = _selector.Selected;
            if (unit == null) return;

            _upgrades.TryUpgrade(unit);
            RefreshNow();   // 비용이 올라갔으니 라벨을 즉시 갱신한다
        }
    }
}
