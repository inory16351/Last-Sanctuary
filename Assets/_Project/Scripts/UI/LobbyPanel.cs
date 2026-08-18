using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LastSanctuary.Save;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 로비(타이틀) 화면 (2026-08-18 신설 — 유저 지시 <i>"로비 화면 만들어 … 환경설정 /
    /// 이어하기 / 새로하기 / 게임 종료 버튼 … 게임 시작하면 로비화면에서 시작"</i>).
    ///
    /// <b>왜 별도 씬인가</b> — "저장하고 로비로 돌아가기" 와 "새로하기" 가 둘 다 <b>게임 판을
    /// 완전히 새로 만드는</b> 동작이다. 게임 씬(<c>Proto_01</c>) 안에 덮개로 만들면 맵·유닛·
    /// 웨이브가 뒤에서 이미 돌고 있어서, 새로하기를 누를 때마다 그것들을 <b>손으로 되돌려야</b>
    /// 한다 — 되돌릴 것을 하나라도 빠뜨리면 이전 판의 잔재가 새 판에 섞인다.
    /// 씬을 새로 부르면 그 문제가 통째로 없어진다.
    ///
    /// <b>연출</b>(유저 지시) — 타이틀 이미지가 먼저 페이드 인 하고, 그 뒤 버튼들이
    /// <b>순차적으로</b> 페이드 인 한다. 시간은 <see cref="Time.unscaledDeltaTime"/> 으로 잰다 —
    /// 게임에서 일시정지(<c>timeScale = 0</c>)한 채 로비로 나오면 연출이 멈춰버린다.
    /// (<see cref="SettingsPanel"/> 이 씬을 넘기기 전에 되돌리지만, 그 한 곳에만 의존하지 않는다.)
    /// </summary>
    public class LobbyPanel : MonoBehaviour
    {
        [Header("하이라키 이름 (MCP 로는 참조를 못 넣어 이름으로 찾는다 — 진행상황 8절 4번)")]
        [SerializeField] string titlePath = "Title";
        [SerializeField] string continueButtonPath = "Menu/ContinueButton";
        [SerializeField] string newGameButtonPath = "Menu/NewGameButton";
        [SerializeField] string settingsButtonPath = "Menu/SettingsButton";
        [SerializeField] string quitButtonPath = "Menu/QuitButton";
        [SerializeField] string savedAtPath = "Menu/SavedAt";
        [SerializeField] string settingsWindowPath = "SettingsWindow";

        [Header("씬")]
        [Tooltip("게임 본편 씬 이름. 빌드 세팅에 들어 있어야 한다")]
        [SerializeField] string gameSceneName = "Proto_01";

        [Header("페이드 인")]
        [Tooltip("타이틀 이미지가 떠오르는 시간(초)")]
        [Min(0f)] [SerializeField] float titleFadeSeconds = 1.2f;

        [Tooltip("타이틀이 다 뜬 뒤 첫 버튼이 뜨기까지 기다리는 시간(초)")]
        [Min(0f)] [SerializeField] float menuDelaySeconds = 0.25f;

        [Tooltip("버튼 하나가 떠오르는 시간(초)")]
        [Min(0f)] [SerializeField] float buttonFadeSeconds = 0.45f;

        [Tooltip("버튼과 버튼 사이의 간격(초). 이 값 때문에 '순차적으로' 뜬다")]
        [Min(0f)] [SerializeField] float buttonStaggerSeconds = 0.18f;

        [Header("문구")]
        [SerializeField] string savedAtFormat = "마지막 저장: {0}";
        [SerializeField] string noSaveText = "저장된 게임이 없습니다";

        CanvasGroup _titleGroup;
        Button _continueButton;
        Button _newGameButton;
        Button _settingsButton;
        Button _quitButton;
        TMP_Text _savedAt;
        LobbySettingsWindow _settingsWindow;

        /// <summary>페이드 인 순서대로의 버튼 묶음. 각자 <see cref="CanvasGroup"/> 을 갖는다.</summary>
        readonly List<CanvasGroup> _menuGroups = new List<CanvasGroup>();

        void Start()
        {
            SaveService.ApplyVolume();     // 빌드를 새로 켰을 때 저장된 음량을 반영한다

            // 폰트는 씬에 이미 배선돼 있다 — 유저 지시 2026-08-18: <i>"폰트는 네오 둥근모
            // 베이크 해서 써라"</i>. 에디터 메뉴 <b>LastSanctuary/폰트/네오 둥근모 TMP 에셋 굽고
            // 씬에 적용</b> 이 이 씬의 TMP 전부에 정본 에셋을 붙인다.
            // ⚠ <b>로비에 글자를 새로 추가하면 그 메뉴를 다시 실행할 것.</b>
            Bind();
            StartCoroutine(PlayIntro());
        }

        // ------------------------------------------------------------------

        void Bind()
        {
            _titleGroup = EnsureGroup(titlePath);

            _continueButton = FindComponent<Button>(continueButtonPath);
            _newGameButton = FindComponent<Button>(newGameButtonPath);
            _settingsButton = FindComponent<Button>(settingsButtonPath);
            _quitButton = FindComponent<Button>(quitButtonPath);
            _savedAt = FindComponent<TMP_Text>(savedAtPath);

            _settingsWindow = FindComponent<LobbySettingsWindow>(settingsWindowPath);
            if (_settingsWindow != null) _settingsWindow.Close();

            if (_continueButton != null) _continueButton.onClick.AddListener(HandleContinue);
            if (_newGameButton != null) _newGameButton.onClick.AddListener(HandleNewGame);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(HandleSettings);
            if (_quitButton != null) _quitButton.onClick.AddListener(HandleQuit);

            RefreshSaveInfo();

            // 페이드 인 순서 — 유저가 제일 먼저 쓸 것부터.
            AddMenuGroup(continueButtonPath);
            AddMenuGroup(newGameButtonPath);
            AddMenuGroup(settingsButtonPath);
            AddMenuGroup(quitButtonPath);
        }

        void RefreshSaveInfo()
        {
            bool hasSave = SaveService.HasSave;

            // 저장이 없으면 "이어하기" 를 <b>끈다</b> — 눌러도 아무 일이 없는 버튼은 고장으로 보인다.
            if (_continueButton != null) _continueButton.interactable = hasSave;

            if (_savedAt == null) return;

            string savedAt = hasSave ? SaveService.SavedAtLabel() : string.Empty;
            _savedAt.text = string.IsNullOrEmpty(savedAt)
                ? noSaveText
                : string.Format(savedAtFormat, savedAt);
        }

        void AddMenuGroup(string path)
        {
            CanvasGroup group = EnsureGroup(path);
            if (group != null) _menuGroups.Add(group);
        }

        /// <summary>
        /// 페이드에 쓸 <see cref="CanvasGroup"/> 을 보장한다. 없으면 붙인다 —
        /// MCP 로 컴포넌트를 붙일 수는 있지만, 코드가 스스로 보장하면 씬이 되돌아가도 안 깨진다
        /// (<c>CharacterKills.EnsureOn</c> 이 같은 이유로 같은 방식을 쓴다).
        /// </summary>
        CanvasGroup EnsureGroup(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            Transform node = transform.Find(path);
            if (node == null) return null;

            if (!node.TryGetComponent(out CanvasGroup group))
                group = node.gameObject.AddComponent<CanvasGroup>();

            group.alpha = 0f;      // 연출이 시작되기 전에는 전부 숨어 있다
            return group;
        }

        T FindComponent<T>(string path) where T : Component
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            Transform node = transform.Find(path);
            return node != null ? node.GetComponent<T>() : null;
        }

        // ------------------------------------------------------------------
        // 연출 — 타이틀 → 버튼 순차
        // ------------------------------------------------------------------

        IEnumerator PlayIntro()
        {
            yield return Fade(_titleGroup, titleFadeSeconds);

            if (menuDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(menuDelaySeconds);

            for (int i = 0; i < _menuGroups.Count; i++)
            {
                // ★ 앞 버튼이 <b>다 뜨기를 기다리지 않는다</b> — 간격(stagger)만 두고 다음을 시작해
                //   물결처럼 이어지게 한다. 다 기다리면 버튼 4개에 2.5초가 걸려 답답하다.
                StartCoroutine(Fade(_menuGroups[i], buttonFadeSeconds));

                if (buttonStaggerSeconds > 0f && i < _menuGroups.Count - 1)
                    yield return new WaitForSecondsRealtime(buttonStaggerSeconds);
            }
        }

        /// <summary>
        /// 투명도를 0 → 1 로 올린다.
        /// ⚠ <see cref="Time.unscaledDeltaTime"/> 을 쓴다 — 게임에서 일시정지한 채 로비로
        /// 나오면 <c>deltaTime</c> 이 0 이라 연출이 <b>영영 멈춘다</b>.
        /// </summary>
        static IEnumerator Fade(CanvasGroup group, float seconds)
        {
            if (group == null) yield break;

            if (seconds <= 0f)
            {
                group.alpha = 1f;
                yield break;
            }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / seconds;
                group.alpha = Mathf.Clamp01(t);
                yield return null;
            }
            group.alpha = 1f;
        }

        // ------------------------------------------------------------------
        // 버튼
        // ------------------------------------------------------------------

        /// <summary>이어하기 — 저장을 읽어 들고 게임 씬으로 넘어간다.</summary>
        void HandleContinue()
        {
            SaveData data = SaveService.Load();
            if (data == null)
            {
                // 파일이 깨졌거나 형식이 다르다. 표시를 사실에 맞추고 넘어가지 않는다.
                RefreshSaveInfo();
                if (_savedAt != null) _savedAt.text = SaveService.LastMessage;
                return;
            }

            SaveService.PendingLoad = data;   // 게임 씬의 GameSnapshot 이 첫 프레임에 꺼내 쓴다
            LoadGame();
        }

        /// <summary>
        /// 새로하기 — <b>저장 파일을 지우고</b> 시작한다.
        ///
        /// 지우지 않으면 새 판에서 첫 자동 저장이 일어나기 전까지 옛 세이브가 남아,
        /// 그 사이에 게임을 껐다 켜면 <b>버린 판으로 되돌아간다</b>.
        /// </summary>
        void HandleNewGame()
        {
            SaveService.Delete();
            LoadGame();
        }

        void LoadGame()
        {
            // 게임에서 일시정지·배속을 걸어둔 채 나왔을 수 있다. 씬을 넘겨도 timeScale 은
            // 유지되므로(GameSpeedPanel 의 그 함정) 반드시 되돌려 놓는다.
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameSceneName);
        }

        void HandleSettings()
        {
            if (_settingsWindow == null) return;
            _settingsWindow.Toggle();
        }

        /// <summary>
        /// 게임 종료. 에디터에서는 <c>Application.Quit()</c> 이 아무 일도 하지 않으므로
        /// 플레이 모드를 끄는 것으로 대신한다.
        /// </summary>
        void HandleQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
