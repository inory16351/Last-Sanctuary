using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Save;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 음량 조절 한 줄 — <b>드래그되는 진짜 슬라이더</b> (유저 지시 2026-08-18:
    /// <i>"드래그 슬라이더 코딩으로 만들고"</i>).
    ///
    /// ★★ <b>유니티 <see cref="Slider"/> 는 MCP 로 완성할 수 없다</b> — <c>fillRect</c> ·
    /// <c>handleRect</c> · <c>targetGraphic</c> 이 전부 <b>씬 오브젝트 참조</b>인데 MCP 로는
    /// 인스펙터에 참조를 넣을 수 없다(진행상황 8절 4번). 그래서 <b>구조는 MCP 로 만들고
    /// 참조는 코드가 이름으로 찾아 꽂는다</b> — 이 프로젝트의 모든 HUD 가 쓰는 그 방식이다.
    /// 처음엔 그 제약 때문에 −/+ 버튼으로 만들었는데, 유저가 드래그를 원해 이렇게 바꿨다.
    ///
    /// <b>왜 별도 컴포넌트인가</b> — 게임 중(<see cref="SettingsPanel"/>)과 로비
    /// (<see cref="LobbySettingsWindow"/>) 두 곳이 <b>똑같은 음량 UI</b>를 쓴다. 창마다 슬라이더
    /// 배선 코드를 두 벌 적으면 한쪽만 고치는 날이 온다(준수사항 §10 H-3).
    /// 창들은 이제 음량에 대해 아무것도 모른다 — 이 컴포넌트를 계층에 두기만 하면 된다.
    ///
    /// <b>기대하는 자식 구조</b> (유니티 기본 Slider 와 같은 이름):
    /// <code>
    /// Volume                (이 컴포넌트)
    ///   Label               "음량"
    ///   Slider              (Slider 컴포넌트)
    ///     Background
    ///     Fill Area / Fill  → fillRect
    ///     Handle Slide Area / Handle → handleRect · targetGraphic
    ///   Value               "100%"
    /// </code>
    /// </summary>
    public class VolumeSlider : MonoBehaviour
    {
        [Header("하이라키 이름")]
        [SerializeField] string sliderPath = "Slider";
        [SerializeField] string fillPath = "Slider/Fill Area/Fill";
        [SerializeField] string handlePath = "Slider/Handle Slide Area/Handle";
        [SerializeField] string valuePath = "Value";

        Slider _slider;
        TMP_Text _value;
        bool _bound;

        void Awake() => EnsureBound();

        /// <summary>창이 꺼진 채로 만들어질 수 있으므로 켜질 때 값을 다시 읽는다.</summary>
        void OnEnable()
        {
            EnsureBound();
            if (_slider != null) _slider.SetValueWithoutNotify(SaveService.Volume);
            Paint(SaveService.Volume);
        }

        void EnsureBound()
        {
            if (_bound) return;
            _bound = true;

            _slider = Find<Slider>(sliderPath);
            _value = Find<TMP_Text>(valuePath);

            if (_slider == null)
            {
                Debug.LogWarning($"[음량] '{sliderPath}' 에서 Slider 를 찾지 못했습니다.", this);
                return;
            }

            WireSliderParts();

            _slider.minValue = 0f;
            _slider.maxValue = 1f;
            _slider.wholeNumbers = false;
            _slider.direction = Slider.Direction.LeftToRight;

            _slider.SetValueWithoutNotify(SaveService.Volume);
            _slider.onValueChanged.RemoveAllListeners();
            _slider.onValueChanged.AddListener(HandleChanged);

            Paint(SaveService.Volume);
        }

        /// <summary>
        /// 슬라이더의 <b>오브젝트 참조 세 칸</b>을 이름으로 찾아 꽂는다 — MCP 가 못 하는 그 일이다.
        ///
        /// ⚠ <c>fillRect</c>/<c>handleRect</c> 를 대입하면 유니티가 그 <see cref="RectTransform"/> 의
        /// 앵커를 <b>스스로 몰기 시작한다</b>(driven). 그래서 씬에서 잡아둔 앵커 값이 실행 중에
        /// 바뀌어 보이는 것이 정상이다 — 손으로 다시 맞추려 들면 안 된다.
        ///
        /// ⚠ 이미 꽂혀 있으면 건드리지 않는다 — 나중에 프리팹으로 승격해 인스펙터에 제대로
        /// 배선되면 그쪽이 정본이어야 한다.
        /// </summary>
        void WireSliderParts()
        {
            if (_slider.fillRect == null)
            {
                RectTransform fill = Find<RectTransform>(fillPath);
                if (fill != null) _slider.fillRect = fill;
            }

            if (_slider.handleRect == null)
            {
                RectTransform handle = Find<RectTransform>(handlePath);
                if (handle != null) _slider.handleRect = handle;
            }

            // 손잡이가 눌린 상태를 보여줄 그래픽. 없으면 클릭해도 색이 안 변할 뿐 동작은 한다.
            if (_slider.targetGraphic == null && _slider.handleRect != null)
            {
                var graphic = _slider.handleRect.GetComponent<Graphic>();
                if (graphic != null) _slider.targetGraphic = graphic;
            }
        }

        T Find<T>(string path) where T : Component
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            Transform node = transform.Find(path);
            return node != null ? node.GetComponent<T>() : null;
        }

        // ------------------------------------------------------------------

        void HandleChanged(float value)
        {
            SaveService.Volume = value;    // PlayerPrefs 저장 + AudioListener 반영까지 한 줄
            Paint(value);
        }

        void Paint(float value)
        {
            if (_value != null) _value.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }
    }
}
