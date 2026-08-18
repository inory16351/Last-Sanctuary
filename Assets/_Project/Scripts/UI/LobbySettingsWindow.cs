using UnityEngine;
using UnityEngine.UI;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 로비의 환경 설정 창. 게임 중의 <see cref="SettingsPanel"/> 과 <b>같은 값</b>(음량)을 다루지만
    /// <b>저장하기 · 로비로 돌아가기가 없다</b> — 로비에는 저장할 판이 없고 이미 로비다.
    ///
    /// <b>왜 <see cref="SettingsPanel"/> 을 재사용하지 않나</b> — 그쪽은 <see cref="HudExclusive"/>
    /// (게임 HUD 창끼리의 배타)와 <c>GameSnapshot</c>(게임 상태)에 묶여 있는데 로비에는 둘 다 없다.
    ///
    /// <b>음량은 두 창이 같은 컴포넌트를 쓴다</b> — <see cref="VolumeSlider"/> 가 슬라이더 배선과
    /// 값 저장을 통째로 맡으므로, 이 창에는 음량 코드가 <b>한 줄도 없다</b>.
    /// 그래서 이 클래스에 남은 일은 <b>열고 닫기</b>뿐이다.
    ///
    /// 폰트 저작권 문구는 <b>양쪽에 다 넣는다</b>(유저 지시) — 어느 쪽 환경 설정을 열어도 보여야 한다.
    /// </summary>
    public class LobbySettingsWindow : MonoBehaviour
    {
        [Header("하이라키 이름")]
        [SerializeField] string closeButtonPath = "Header/CloseButton";

        Button _closeButton;
        bool _bound;

        void Awake() => EnsureBound();

        void EnsureBound()
        {
            if (_bound) return;
            _bound = true;

            _closeButton = FindComponent<Button>(closeButtonPath);
            if (_closeButton == null) return;

            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(Close);
        }

        T FindComponent<T>(string path) where T : Component
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            Transform node = transform.Find(path);
            return node != null ? node.GetComponent<T>() : null;
        }

        // ------------------------------------------------------------------

        public bool IsOpen => gameObject.activeSelf;

        public void Toggle() => SetOpen(!IsOpen);

        public void Close() => gameObject.SetActive(false);

        public void SetOpen(bool open)
        {
            EnsureBound();
            gameObject.SetActive(open);
            // ⚠ 음량 표시는 여기서 안 만진다 — VolumeSlider 가 자기 OnEnable 에서 다시 읽는다.
        }
    }
}
