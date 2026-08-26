using TMPro;
using UnityEngine;
using LastSanctuary.Resource;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 상단 ENERGY 텍스트에 현재 보유 에너지를 표시한다.
    /// 형식은 <c>에너지 00</c> — 한 자리 수도 두 자리로 채워 자리수가 흔들리지 않게 한다.
    ///
    /// 값이 바뀔 때만 갱신한다(<see cref="ResourceManager.OnEnergyChanged"/> 구독).
    /// 매 프레임 문자열을 만들면 TMP 가 메시를 다시 생성해 낭비가 크다.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class EnergyLabel : MonoBehaviour
    {
        [Header("표시 형식")]
        [Tooltip("{0} 자리에 에너지 수치가 들어간다. 00 은 두 자리로 0 을 채우라는 뜻")]
        [SerializeField] string format = "에너지 {0:00}";

        TMP_Text _label;
        ResourceManager _resources;

        void Awake() => _label = GetComponent<TMP_Text>();

        void Start()
        {
            _resources = ResourceManager.Instance;
            if (_resources == null)
            {
                _label.text = string.Format(format, 0);
                Debug.LogWarning("[EnergyLabel] ResourceManager 를 찾지 못했습니다. " +
                                 "씬에 ResourceManager 오브젝트가 있는지 확인하세요.", this);
                return;
            }

            _resources.OnEnergyChanged += HandleEnergyChanged;
            Data.StringTable.OnLanguageChanged -= HandleLanguageChanged;
            Data.StringTable.OnLanguageChanged += HandleLanguageChanged;
            Refresh(_resources.Energy);
        }

        void OnDestroy()
        {
            if (_resources != null) _resources.OnEnergyChanged -= HandleEnergyChanged;
            Data.StringTable.OnLanguageChanged -= HandleLanguageChanged;
        }

        void HandleEnergyChanged(int delta, int total) => Refresh(total);

        /// <summary>언어가 바뀌면 지금 수치를 그 언어로 다시 찍는다.</summary>
        void HandleLanguageChanged() => Refresh(_resources != null ? _resources.Energy : 0);

        // ★ 형식 문구도 스트링 표가 정본이다 (2026-08-26). 인스펙터의 값은 <b>폴백</b>이라
        //   표에 키가 없으면 지금과 똑같이 나온다. {0:00} 같은 서식 지정자는 표에도 그대로 둔다.
        void Refresh(int total) =>
            _label.text = string.Format(Data.StringTable.Get(FormatKey, format), total);

        const string FormatKey = "ui_energy_format";
    }
}
