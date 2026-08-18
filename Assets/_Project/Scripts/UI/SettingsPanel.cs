using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LastSanctuary.Save;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 환경 설정 창 (2026-08-18 신설 — 유저 지시 <i>"허드 액션에 게임 종료 버튼을 환경 설정으로
    /// 바꾸고 환경설정에 저장하기 / 저장하고 로비로 돌아가기 / 음량 조절"</i>).
    ///
    /// 다른 창(<see cref="SquadPanel"/>·<see cref="TacticalOrderPanel"/>·<see cref="SubjugationPanel"/>)과
    /// <b>같은 API 모양</b>(<c>Instance</c>/<c>IsOpen</c>/<c>Toggle</c>/<c>SetOpen</c>/<c>Close</c>)을 쓰고,
    /// 배타는 <see cref="HudExclusive.OpenOnly"/> 한 줄에 맡긴다.
    ///
    /// <b>게임 종료 버튼이 여기 없는 이유</b> — 유저 확정으로 종료는 <b>로비 화면</b>이 맡는다.
    /// 게임 중에는 "저장하고 로비로" 가 그 자리를 대신한다.
    ///
    /// ⚠ <b>비활성으로 시작하므로 <c>Awake</c> 가 안 돈다</b> — 이 프로젝트에서 네 번 재발한
    /// 함정이다(59-6·88-1절). <see cref="Instance"/> 는 비활성 포함 조회로 채우고,
    /// 배선(<see cref="EnsureBound"/>)은 <b>열리는 순간</b>에도 한 번 더 확인한다.
    /// </summary>
    public class SettingsPanel : MonoBehaviour, IExclusiveHudPanel
    {
        static SettingsPanel _instance;

        /// <summary>비활성이어도 찾아온다 — 창은 평소 꺼져 있다.</summary>
        public static SettingsPanel Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<SettingsPanel>(FindObjectsInactive.Include);
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("하이라키 이름 (비어 있으면 이 이름으로 찾는다)")]
        [Tooltip("MCP 로는 씬 오브젝트 참조를 인스펙터에 넣을 수 없어서(진행상황 8절 4번) " +
                 "이름으로 찾는다. 다른 HUD 패널이 전부 쓰는 방식과 같다")]
        [SerializeField] string saveButtonPath = "Body/SaveButton";
        [SerializeField] string lobbyButtonPath = "Body/LobbyButton";
        [SerializeField] string closeButtonPath = "Header/CloseButton";
        [SerializeField] string statusPath = "Body/Status";

        [Header("문구")]
        [SerializeField] string savedFormat = "저장했습니다 ({0})";
        [SerializeField] string saveFailed = "저장하지 못했습니다.";

        [Tooltip("로비 씬 이름. 빌드 세팅에 들어 있어야 한다")]
        [SerializeField] string lobbySceneName = "Lobby";

        Button _saveButton;
        Button _lobbyButton;
        Button _closeButton;
        TMP_Text _status;

        bool _bound;

        void Awake()
        {
            Instance = this;
            EnsureBound();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// 자식들을 이름으로 찾아 배선한다. <b>여러 번 불려도 안전</b>해야 한다 —
        /// <c>Awake</c> 가 안 도는 경로가 있어서 <see cref="SetOpen"/> 도 이걸 부른다.
        /// </summary>
        void EnsureBound()
        {
            if (_bound) return;
            _bound = true;

            // 폰트는 씬에 이미 배선돼 있다 — 유저 지시 2026-08-18: <i>"폰트는 네오 둥근모
            // 베이크 해서 써라"</i>. 에디터 메뉴 <b>LastSanctuary/폰트/네오 둥근모 TMP 에셋 굽고
            // 씬에 적용</b> 이 씬의 TMP 전부에 정본 에셋을 붙인다(NeoDunggeunmoFontBaker).
            // ⚠ <b>이 창에 글자를 새로 추가하면 그 메뉴를 다시 실행할 것</b> — 안 하면 새 글자만
            //    TMP 기본 폰트(Liberation Sans)로 남아 한글이 안 보인다.
            _saveButton = FindComponent<Button>(saveButtonPath);
            _lobbyButton = FindComponent<Button>(lobbyButtonPath);
            _closeButton = FindComponent<Button>(closeButtonPath);
            _status = FindComponent<TMP_Text>(statusPath);

            if (_saveButton != null)
            {
                _saveButton.onClick.RemoveAllListeners();
                _saveButton.onClick.AddListener(HandleSave);
            }
            if (_lobbyButton != null)
            {
                _lobbyButton.onClick.RemoveAllListeners();
                _lobbyButton.onClick.AddListener(HandleSaveAndLobby);
            }
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveAllListeners();
                _closeButton.onClick.AddListener(Close);
            }

            // 음량은 <see cref="VolumeSlider"/> 가 통째로 맡는다 — 이 창은 음량에 대해
            // 아무것도 모른다(로비 창과 <b>같은 컴포넌트</b>를 쓰기 위해서다).
            SetStatus(string.Empty);
        }

        T FindComponent<T>(string path) where T : Component
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            Transform node = transform.Find(path);
            return node != null ? node.GetComponent<T>() : null;
        }

        // ------------------------------------------------------------------
        // 열고 닫기 — 다른 창과 같은 API
        // ------------------------------------------------------------------

        public bool IsOpen => gameObject.activeSelf;

        public void Toggle() => SetOpen(!IsOpen);

        public void Close() => gameObject.SetActive(false);

        public void SetOpen(bool open)
        {
            EnsureBound();
            gameObject.SetActive(open);
            if (!open) return;

            HudExclusive.OpenOnly(this);     // 다른 창·지정 모드를 전부 끈다

            // ⚠ 음량 표시는 여기서 안 만진다 — <see cref="VolumeSlider"/> 가 자기 OnEnable 에서
            //   지금 값을 다시 읽는다(로비에서 바꾼 값이 그대로 반영된다).
            SetStatus(string.Empty);
        }

        // ------------------------------------------------------------------
        // 동작
        // ------------------------------------------------------------------

        void HandleSave()
        {
            if (!TrySave()) return;
            SetStatus(string.Format(savedFormat, System.DateTime.Now.ToString("HH:mm:ss")));
        }

        /// <summary>
        /// 저장하고 로비로 돌아간다. <b>저장에 실패하면 씬을 넘기지 않는다</b> —
        /// 실패한 채로 나가면 그때까지의 진행이 통째로 사라진다.
        /// </summary>
        void HandleSaveAndLobby()
        {
            if (!TrySave())
            {
                SetStatus(saveFailed);
                return;
            }

            // ⚠ 게임 중에 배속·일시정지로 timeScale 이 0 이나 8 일 수 있다. 씬을 넘겨도
            //   timeScale 은 유지되므로(GameSpeedPanel 의 그 함정) 반드시 되돌려 놓는다 —
            //   안 그러면 로비의 페이드 인 연출이 멈추거나 8배속으로 지나간다.
            Time.timeScale = 1f;

            SceneManager.LoadScene(lobbySceneName);
        }

        bool TrySave()
        {
            Save.GameSnapshot snapshot = Save.GameSnapshot.Instance;
            if (snapshot == null)
            {
                Debug.LogWarning("[환경설정] GameSnapshot 을 찾지 못해 저장하지 못했습니다.", this);
                SetStatus(saveFailed);
                return false;
            }

            bool ok = snapshot.SaveNow("환경 설정에서 저장");
            if (!ok) SetStatus(saveFailed);
            return ok;
        }

        void SetStatus(string text)
        {
            if (_status == null) return;
            _status.text = text ?? string.Empty;
        }
    }
}
