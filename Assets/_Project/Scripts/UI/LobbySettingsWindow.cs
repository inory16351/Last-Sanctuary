using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 로비의 환경 설정 창. 게임 중의 <see cref="SettingsPanel"/> 과 <b>같은 값</b>을 다루지만
    /// <b>저장하기 · 로비로 돌아가기 · 다시 시작이 없다</b> — 로비에는 저장할 판이 없고 이미 로비다.
    ///
    /// <b>왜 <see cref="SettingsPanel"/> 을 재사용하지 않나</b> — 그쪽은 <see cref="HudExclusive"/>
    /// (게임 HUD 창끼리의 배타)와 <c>GameSnapshot</c>(게임 상태)에 묶여 있는데 로비에는 둘 다 없다.
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  ★★ <b>«사람에 딸린 값» 은 여기에도 있어야 한다</b> (2026-08-26 · 유저 지시:
    ///  *"로비화면 환경 설정에도 환경 설정 버튼들 같이 붙여주고"*)
    /// ══════════════════════════════════════════════════════════════════
    /// 게임 중 환경 설정의 버튼은 두 갈래다.
    /// <code>
    ///   판에 딸린 것 : 저장 · 로비로 · 다시 시작 · 저장 않고 나가기  → 로비에는 없다
    ///   사람에 딸린 것 : 음량 · 언어 · 단축키 · 도움말 기억          → 로비에도 있어야 한다
    /// </code>
    /// 뒤쪽 넷은 전부 <see cref="PlayerPrefs"/> 에 남는 값이라 <b>어느 씬에서 만져도 같다</b>.
    /// «환경 설정» 을 로비에서 열었는데 언어가 없으면, 판을 한 번 시작해야 언어를 바꿀 수 있다.
    ///
    /// ★ <b>규칙은 각자 자기 자리에 있다</b> — 음량은 <see cref="VolumeSlider"/>,
    ///   언어는 <see cref="LanguageSetting"/>, 단축키는 <c>HotkeyService</c>·<see cref="HotkeyPanel"/>.
    ///   그래서 이 창에는 «누르면 그쪽을 부른다» 만 있고 값을 다루는 코드가 없다.
    /// ⚠ <b>도움말만 예외다</b> — <see cref="Help.HelpService"/> 는 게임 씬에만 있다.
    ///   로비에서는 서비스 없이 <see cref="Help.HelpService.PrefsKey"/> 를 지운다(아래 ★).
    ///
    /// 폰트 저작권 문구는 <b>양쪽에 다 넣는다</b>(유저 지시) — 어느 쪽 환경 설정을 열어도 보여야 한다.
    /// </summary>
    public class LobbySettingsWindow : MonoBehaviour
    {
        [Header("하이라키 이름")]
        [SerializeField] string closeButtonPath = "Header/CloseButton";
        [SerializeField] string languageButtonPath = "Body/LanguageButton";
        [SerializeField] string hotkeyButtonPath = "Body/HotkeyButton";
        [SerializeField] string helpResetButtonPath = "Body/HelpResetButton";
        [SerializeField] string statusPath = "Body/Status";

        [Header("문구")]
        [Tooltip("언어 버튼에 찍는 글 — {0} 에 지금 언어 이름이 들어간다")]
        [SerializeField] string languageLabelFormat = "언어 : {0}";

        [SerializeField] string helpResetDone =
            "도움말을 처음 상태로 되돌렸습니다 — 판을 시작하면 설명이 다시 나옵니다.";

        [SerializeField] string hotkeyMissing = "단축키 설정 창을 찾지 못했습니다.";

        Button _closeButton;
        Button _languageButton;
        Button _hotkeyButton;
        Button _helpResetButton;
        TMP_Text _status;
        bool _bound;

        void Awake() => EnsureBound();

        void EnsureBound()
        {
            if (_bound) return;
            _bound = true;

            _closeButton = FindComponent<Button>(closeButtonPath);
            _languageButton = FindComponent<Button>(languageButtonPath);
            _hotkeyButton = FindComponent<Button>(hotkeyButtonPath);
            _helpResetButton = FindComponent<Button>(helpResetButtonPath);
            _status = FindComponent<TMP_Text>(statusPath);

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveAllListeners();
                _closeButton.onClick.AddListener(Close);
            }

            // ★ 그림은 코드가 꽂는다 — MCP 로는 씬 오브젝트에 Sprite 참조를 넣을 수 없다
            //   (진행상황 8절 4번). 게임 쪽 환경 설정의 언어 버튼과 <b>같은 처리</b>다.
            if (_languageButton != null)
            {
                HudTheme.EnsureButtonSkin(_languageButton);
                _languageButton.onClick.RemoveAllListeners();
                _languageButton.onClick.AddListener(HandleToggleLanguage);
                LanguageSetting.Restore();
                RefreshLanguageLabel();
            }

            if (_hotkeyButton != null)
            {
                HudTheme.EnsureButtonSkin(_hotkeyButton);
                _hotkeyButton.onClick.RemoveAllListeners();
                _hotkeyButton.onClick.AddListener(HandleHotkeys);
            }

            if (_helpResetButton != null)
            {
                HudTheme.EnsureButtonSkin(_helpResetButton);
                _helpResetButton.onClick.RemoveAllListeners();
                _helpResetButton.onClick.AddListener(HandleForgetHelp);
            }

            SetStatus(string.Empty);
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
            if (open)
            {
                RefreshLanguageLabel();
                SetStatus(string.Empty);
            }
            // ⚠ 음량 표시는 여기서 안 만진다 — VolumeSlider 가 자기 OnEnable 에서 다시 읽는다.
        }

        // ------------------------------------------------------------------
        //  언어 · 단축키 · 도움말
        // ------------------------------------------------------------------

        void HandleToggleLanguage()
        {
            Data.GameLanguage next = LanguageSetting.Toggle();
            RefreshLanguageLabel();
            SetStatus(string.Format(languageLabelFormat, LanguageSetting.NameOf(next)));
        }

        /// <summary>버튼에 지금 언어를 적는다.</summary>
        void RefreshLanguageLabel()
        {
            if (_languageButton == null) return;

            TMP_Text label = _languageButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = string.Format(languageLabelFormat, LanguageSetting.CurrentName);
        }

        /// <summary>
        /// 단축키 설정 창을 연다. 그 창은 <b>루트 하나만 씬에 두고 안쪽을 스스로 짓는다</b>
        /// (<see cref="HotkeyPanel"/> 의 ★) — 그래서 로비에도 빈 루트 하나만 있으면 된다.
        /// ⚠ 이 창은 닫는다 — 두 창이 겹치면 뒤쪽 창의 버튼이 앞 창의 막에 가린다.
        /// </summary>
        void HandleHotkeys()
        {
            HotkeyPanel panel = HotkeyPanel.Instance;
            if (panel == null)
            {
                SetStatus(hotkeyMissing);
                return;
            }

            Close();
            panel.SetOpen(true);
        }

        /// <summary>
        /// ★ <b>서비스 없이 지운다.</b> <see cref="Help.HelpService"/> 는 게임 씬에만 있으므로
        /// 로비에서는 <see cref="Help.HelpService.PrefsKey"/> 를 직접 지운다 — 그 서비스도
        /// <c>ForgetAll</c> 에서 결국 같은 열쇠를 지운다(열쇠가 <c>public const</c> 인 이유).
        /// ⚠ 두 번 확인하지 않는다 — 되돌릴 수 있는 조작이다(다시 읽으면 그만이다).
        /// </summary>
        void HandleForgetHelp()
        {
            Help.HelpService help = Help.HelpService.Instance;
            if (help != null) help.ForgetAll();
            else
            {
                PlayerPrefs.DeleteKey(Help.HelpService.PrefsKey);
                PlayerPrefs.Save();
            }

            SetStatus(helpResetDone);
        }

        void SetStatus(string text)
        {
            if (_status != null) _status.text = text;
        }
    }
}
