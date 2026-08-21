using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LastSanctuary.Events;
using LastSanctuary.Save;
using LastSanctuary.Units;

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
    /// ══════════════════════════════════════════════════════════════════
    ///  <b>게임 재시작</b> (2026-08-21 3차 — 유저 지시: <i>"'저장하고 로비로 돌아가기' 아래에
    ///  게임 재시작 버튼 하나 추가로 만들어서 그 버튼 누르면 게임이 처음으로 초기화"</i>)
    /// ══════════════════════════════════════════════════════════════════
    /// 버튼 실물은 씬에 있다 — <c>HUD_Settings/Body/RestartButton</c>
    /// (<c>Tools/scene_add_restart_button.py</c> 가 옆 버튼을 복제해 만들었다. 유니티가
    /// 꺼진 세션이라 MCP 를 못 써서 씬 YAML 에 직접 넣었다 — 그 파일의 ★★ 참조).
    ///
    /// <b>"처음으로" 를 무엇으로 읽었나</b> — 로비의 «새로하기»와 <b>같은 상태</b>다:
    /// 저장 파일을 지우고 · 판 전역 기록(등장 인물·중립 사냥 수·지속 보정)을 비우고 ·
    /// 게임 씬을 다시 연다. 로비를 거치지 않을 뿐 도착 지점이 같아야 «두 개가 다르다» 가 없다.
    ///
    /// ⚠ <b>두 번 눌러야 실행된다</b>(<see cref="restartConfirmSeconds"/>). 되돌릴 수 없고
    ///   저장까지 지우는 동작이 «저장하기» 바로 아래에 있어서, 한 번의 오조작으로 판이
    ///   통째로 날아가면 안 된다. 첫 번째 누름은 <c>Status</c> 칸에 경고만 띄운다.
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
        [SerializeField] string restartButtonPath = "Body/RestartButton";
        [SerializeField] string quitButtonPath = "Body/QuitButton";
        [SerializeField] string closeButtonPath = "Header/CloseButton";
        [SerializeField] string statusPath = "Body/Status";

        [Header("문구")]
        [SerializeField] string savedFormat = "저장했습니다 ({0})";
        [SerializeField] string saveFailed = "저장하지 못했습니다.";

        [Header("게임 재시작")]
        [Tooltip("한 번 눌렀을 때 뜨는 경고. 이 상태에서 한 번 더 눌러야 실제로 재시작한다")]
        [SerializeField] string restartConfirm =
            "정말 처음부터 다시 시작할까요? 한 번 더 누르면 실행됩니다 (저장이 지워집니다)";

        [Tooltip("경고가 살아 있는 시간(초). 지나면 처음 상태로 돌아간다 — " +
                 "창을 켜 둔 채 잊고 있다가 누르는 일을 막는다")]
        [Min(1f)] [SerializeField] float restartConfirmSeconds = 5f;

        [Header("저장 없이 나가기")]
        [Tooltip("한 번 눌렀을 때 뜨는 경고. 이 상태에서 한 번 더 눌러야 실제로 나간다. " +
                 "★ 저장 «파일» 은 지우지 않는다 — 마지막 저장 이후의 진행만 버린다")]
        [SerializeField] string quitConfirm =
            "저장하지 않고 로비로 나갈까요? 한 번 더 누르면 실행됩니다 (마지막 저장 이후 진행이 사라집니다)";

        [Tooltip("로비 씬 이름. 빌드 세팅에 들어 있어야 한다")]
        [SerializeField] string lobbySceneName = "Lobby";

        Button _saveButton;
        Button _lobbyButton;
        Button _restartButton;
        Button _quitButton;
        Button _closeButton;
        TMP_Text _status;

        bool _bound;

        /// <summary>재시작 확인이 살아 있는 시각(<see cref="Time.unscaledTime"/> 기준).
        /// 0 이면 «아직 한 번도 안 눌렀다». ⚠ 일시정지 중에도 흘러야 하므로 unscaled 다.</summary>
        float _restartArmedUntil;

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
            _restartButton = FindComponent<Button>(restartButtonPath);
            _quitButton = FindComponent<Button>(quitButtonPath);
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
            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveAllListeners();
                _restartButton.onClick.AddListener(HandleRestart);
            }
            else
            {
                // 씬에 버튼이 없으면 <b>조용히 넘어가지 않는다</b> — 이 창의 다른 배선과 달리
                // 이건 이번에 새로 만든 오브젝트라, 없다면 씬이 옛 버전이라는 뜻이다.
                Debug.LogWarning($"[환경설정] '{restartButtonPath}' 을 찾지 못했습니다 — " +
                                 "Tools/scene_add_restart_button.py 를 돌렸는지 확인해주세요.", this);
            }

            if (_quitButton != null)
            {
                _quitButton.onClick.RemoveAllListeners();
                _quitButton.onClick.AddListener(HandleQuitWithoutSaving);
            }
            else
            {
                // 재시작 버튼과 같은 이유로 조용히 넘기지 않는다 — 이번에 새로 만든 오브젝트다.
                Debug.LogWarning($"[환경설정] '{quitButtonPath}' 을 찾지 못했습니다 — " +
                                 "씬에 「저장 없이 나가기」 버튼이 있는지 확인해주세요.", this);
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
            _restartArmedUntil = 0f;     // 창을 다시 열면 «한 번 눌린» 상태가 남지 않는다
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

        /// <summary>
        /// 게임 재시작 — <b>두 번 눌러야</b> 실행된다(위 ⚠).
        ///
        /// ★ 첫 누름은 <c>Status</c> 칸에 경고를 띄우고 <see cref="restartConfirmSeconds"/>
        ///   동안만 «걸려 있다». 그 안에 다시 누르면 실행, 지나면 처음으로 돌아간다.
        /// </summary>
        void HandleRestart()
        {
            if (Time.unscaledTime > _restartArmedUntil)
            {
                _restartArmedUntil = Time.unscaledTime + restartConfirmSeconds;
                SetStatus(restartConfirm);
                return;
            }

            _restartArmedUntil = 0f;
            RestartRun();
        }

        /// <summary>
        /// <b>판을 처음 상태로 되돌린다</b> — 로비의 «새로하기»와 같은 도착 지점이다.
        ///
        /// 순서에 뜻이 있다:
        ///   ① <b>지속 보정을 먼저 걷는다</b> — <see cref="EventRewardService"/> 는 유닛에
        ///      건 보정을 «되돌릴 대상» 으로 들고 있다. 씬을 넘긴 뒤에 부르면 그 유닛들이
        ///      이미 파괴돼 있어 조용히 건너뛴다(다음 판으로 새지는 않지만, 살아 있을 때
        ///      거두는 편이 «누가 무엇을 되돌렸는가» 가 분명하다).
        ///   ② <b>판 전역 기록을 비운다</b> — 등장 인물(재등장 금지)과 중립 사냥 수는
        ///      <c>static</c> 이라 <b>씬을 다시 열어도 살아남는다</b>. 두 클래스 모두
        ///      «새 판을 시작할 때 비운다» 는 <c>ResetRun()</c> 을 이미 갖고 있는데
        ///      <b>아무도 부르지 않고 있었다</b> — 여기가 그 자리다.
        ///      (도메인 리로드가 도는 에디터 플레이 진입 때만 우연히 비워지고 있었다.)
        ///   ③ <b>저장을 지운다</b> — 안 지우면 첫 자동 저장 전에 게임을 껐다 켤 때
        ///      <b>버린 판으로 되돌아간다</b>(<see cref="LobbyPanel"/> 의 «새로하기»와 같은 이유).
        ///   ④ 씬을 다시 연다.
        ///
        /// ⚠ <b>지금 씬을 다시 연다</b> — 이름을 적어 두지 않는다. 이 창은 게임 씬에만 있고,
        ///   씬 이름을 두 곳(여기·로비)에 적으면 한쪽만 고쳐질 수 있다.
        /// ⚠ 배속·일시정지로 <c>timeScale</c> 이 0 이나 8 일 수 있다. 씬을 넘겨도 유지되므로
        ///   반드시 되돌린다(<see cref="HandleSaveAndLobby"/> 와 같은 함정).
        /// </summary>
        /// ★★ <b>2026-08-21 — 이 순서가 <see cref="Save.RunResetService"/> 로 옮겨졌다.</b>
        ///   위에 적힌 ①~④ 가 그대로 그 클래스에 있다. 옮긴 이유는 «새 판을 시작하는»
        ///   경로가 <b>넷</b>인데 이 셋(패배·승리·로비)이 ② 를 빠뜨리고 있었기 때문이다 —
        ///   그것이 «캐릭터가 죽으면 생성이 안 되는» 버그였다(그 클래스의 맨 위 주석).
        void RestartRun() => Save.RunResetService.BeginNewRun();

        /// <summary>
        /// ★★ <b>저장 없이 나가기</b> (2026-08-21 · 유저 지시: *"재시작 밑에 저장 없이 나가기
        /// 버튼 추가하고 기능 구현"*).
        ///
        /// <b>「저장하고 로비로 돌아가기」와 무엇이 다른가</b> — <b>저장을 하지 않는다</b>.
        /// 그것뿐이다. 마지막 자동 저장(또는 손으로 누른 저장) 이후의 진행은 <b>버려진다</b>.
        ///
        /// ⚠⚠ <b>저장 파일을 «지우지» 는 않는다.</b> «나가기» 는 «이 판을 버린다» 가 아니라
        ///   «지금 상태를 기록하지 않고 나간다» 다. 지워 버리면 유저가 <b>예전 저장까지</b>
        ///   잃는다 — 그것은 「게임 재시작」이 하는 일이고(그쪽은 경고를 두 번 받는다),
        ///   이 버튼의 뜻이 아니다. 로비에서 「이어하기」를 누르면 <b>마지막 저장</b>으로
        ///   돌아간다.
        ///
        /// ⚠ <b>두 번 눌러야 실행된다</b> — 되돌릴 수 없고(진행이 사라진다) 「저장하고 로비로」
        ///   바로 아래에 붙는 버튼이라, 한 번의 오조작으로 진행이 날아가면 안 된다.
        ///   「게임 재시작」과 <b>같은 확인 통로</b>를 쓴다(<see cref="_restartArmedUntil"/> 는
        ///   버튼마다 따로 두지 않고 «지금 무엇을 확인받는 중인가» 로 함께 관리한다).
        ///
        /// ⚠ <c>timeScale</c> 을 되돌린다 — 배속·일시정지 상태로 나가면 로비가 멈추거나
        ///   8배속으로 지나간다(<see cref="HandleSaveAndLobby"/> 와 같은 함정).
        /// ★ 이벤트의 지속 보정은 <b>여기서 걷는다</b> — 유닛이 살아 있을 때 거두는 편이
        ///   «누가 무엇을 되돌렸는가» 가 분명하다(<see cref="Save.RunResetService"/> 의 ①).
        ///   다만 판 전역 기록(등장 인물)은 <b>비우지 않는다</b> — 저장을 살려 두었으므로
        ///   이어하기로 돌아올 수 있고, 그때 «같은 인물이 두 번» 이 되면 안 된다.
        /// </summary>
        void HandleQuitWithoutSaving()
        {
            if (Time.unscaledTime > _restartArmedUntil)
            {
                _restartArmedUntil = Time.unscaledTime + restartConfirmSeconds;
                SetStatus(quitConfirm);
                return;
            }

            _restartArmedUntil = 0f;

            // 이벤트 지속 보정만 걷는다 (판 전역 기록은 그대로 — 위 ★).
            if (EventService.Instance != null) EventService.Instance.ClearRun();
            else EventRewardService.ClearAll();

            SaveService.PendingLoad = null;     // ⚠ 남아 있으면 로비가 그것을 이어하기로 읽는다
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
