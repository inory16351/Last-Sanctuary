using UnityEngine;
using LastSanctuary.Data;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★ <b>언어 선택 규칙 한 곳</b> (2026-08-26 신설 · 유저 지시:
    /// *"로비화면 환경 설정에도 환경 설정 버튼들 같이 붙여주고"*).
    ///
    /// <b>왜 생겼나</b> — 언어 토글은 게임 중 <see cref="SettingsPanel"/> 에만 있었다.
    /// 로비의 <see cref="LobbySettingsWindow"/> 에도 같은 버튼을 붙이면서
    /// «어느 열쇠에 저장하나 · 기본은 무엇인가 · 이름을 뭐라 적나» 가 <b>두 벌</b>이 된다.
    /// 두 벌이 되면 한쪽만 고쳤을 때 «로비에서 바꿨는데 게임에서 안 바뀐다» 가 된다 —
    /// <c>VolumeSlider</c> 가 음량을 한 곳에 모은 것과 같은 이유로 여기 모은다.
    ///
    /// ★ <b>값 자체는 <see cref="StringTable"/> 이 들고 있다.</b> 여기 있는 것은
    ///   «사람에 딸린 선택» 을 <see cref="PlayerPrefs"/> 에 남기고 되살리는 규칙뿐이다
    ///   (단축키가 <c>HotkeyService</c> 에 있는 것과 같은 자리).
    ///
    /// ══════════════════════════════════════════════════════════════════
    /// ★★★ 2026-09-01 — <b>둘에서 아홉으로</b> (유저 지시: *"스페인어, 프랑스어,
    ///   독일어, 일본어, 러시아어, 포르투갈어, 폴란드어 … 스크롤바 언어설정에 넣어서"*)
    ///
    ///   <b>«누르면 다음» 을 버렸다.</b> 예전 주석은 «언어가 둘뿐이라 목록을 띄우면
    ///   누르는 횟수만 늘어난다» 고 적어 뒀는데, 아홉이 되면 정반대가 된다 —
    ///   폴란드어를 고르려고 버튼을 여덟 번 눌러야 하고, 한 번 지나치면 <b>여덟 번을
    ///   더</b> 눌러야 한다. 그래서 <see cref="LanguagePickerPopup"/>(스크롤 목록)을 띄운다.
    ///
    ///   ⚠ <see cref="Toggle"/> 은 <b>남겨 뒀다</b> — 지우면 그것을 부르던 두 창이
    ///     컴파일에서 깨진다. 이제 «다음 언어» 라는 뜻이고, 목록을 못 띄우는 상황의
    ///     폴백으로도 쓴다.
    /// ══════════════════════════════════════════════════════════════════
    /// </summary>
    public static class LanguageSetting
    {
        /// <summary>언어 선택을 남기는 <see cref="PlayerPrefs"/> 열쇠.</summary>
        public const string PrefsKey = "ls_language";

        /// <summary>
        /// 고를 수 있는 언어. <b>목록의 순서가 곧 화면의 순서</b>다.
        ///
        /// ⚠ <see cref="GameLanguage"/> 의 값은 PlayerPrefs 에 정수로 남으므로
        ///   <b>enum 쪽은 재배치 금지</b>다. 이 배열은 «보여주는 순서» 일 뿐이라
        ///   자유롭게 바꿔도 저장된 선택이 깨지지 않는다.
        /// </summary>
        public static readonly GameLanguage[] All =
        {
            GameLanguage.Korean,
            GameLanguage.English,
            GameLanguage.Japanese,
            GameLanguage.Spanish,
            GameLanguage.Portuguese,
            GameLanguage.French,
            GameLanguage.German,
            GameLanguage.Russian,
            GameLanguage.Polish,
        };

        /// <summary>지난번에 고른 언어를 되살린다. 남긴 것이 없거나 값이 이상하면 한국어.</summary>
        public static void Restore()
        {
            int saved = PlayerPrefs.GetInt(PrefsKey, (int)GameLanguage.Korean);
            StringTable.Language = IsValid(saved) ? (GameLanguage)saved : GameLanguage.Korean;
        }

        /// <summary>
        /// 저장된 정수가 지금 빌드에 있는 언어인가.
        ///
        /// ⚠ <b>이 검사가 없으면 안 된다</b> — 언어를 빼거나 순서를 잘못 건드린 빌드에서
        ///   범위 밖 정수가 들어오면 <c>(GameLanguage)9</c> 같은 값이 되고,
        ///   <c>StringTable.Get</c> 이 없는 컬럼을 보다가 <b>모든 문구가 폴백</b>으로 떨어진다.
        /// </summary>
        static bool IsValid(int value) =>
            value >= 0 && value < (int)GameLanguage.COUNT;

        /// <summary>
        /// ★★★ <b>판이 시작될 때 스스로 되살린다</b> (2026-08-26 · 유저 리포트:
        /// *"스트링 키 영어 번역이 반영이 안되고 있는데"*).
        ///
        /// <b>무엇이 빠져 있었나</b> — <see cref="Restore"/> 를 부르는 곳이
        /// <c>SettingsPanel.Awake</c> 와 <c>LobbySettingsWindow</c> <b>둘뿐</b>이었다. 그런데
        /// 설정 창은 씬에서 <b>닫힌 창(<c>active = 0</c>)</b> 이라 <c>Awake</c> 가 돌지 않는다.
        /// <c>StringTable.ResetStatics</c> 는 판마다 언어를 한국어로 되돌리므로,
        /// <b>PlayerPrefs 에 English 가 남아 있어도 게임은 늘 한국어로 시작</b>했다.
        ///
        /// ★ <b>창이 아니라 여기서 부른다.</b> 언어는 «화면 하나의 상태» 가 아니라
        ///   <b>판 전체의 상태</b> 다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RestoreOnBoot() => Restore();

        /// <summary>
        /// ★ 언어를 <b>골라서</b> 정한다 (2026-09-01 · 목록에서 고르는 길).
        /// 같은 언어를 다시 골라도 안전하다 — <c>Language</c> 의 setter 가 걸러낸다.
        /// </summary>
        public static void Select(GameLanguage lang)
        {
            if (!IsValid((int)lang)) return;

            StringTable.Language = lang;
            PlayerPrefs.SetInt(PrefsKey, (int)lang);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// <b>다음 언어</b>로 넘어간다(<see cref="All"/> 순서를 돈다). 바뀐 언어를 돌려준다.
        ///
        /// ⚠ 예전에는 «한국어 ↔ English» 였다. 이름을 그대로 둔 것은 이것을 부르는
        ///   두 창을 깨뜨리지 않기 위해서다 — 지금은 목록(<see cref="LanguagePickerPopup"/>)이
        ///   기본 경로이고, 이것은 폴백이다.
        /// </summary>
        public static GameLanguage Toggle()
        {
            int at = System.Array.IndexOf(All, StringTable.Language);
            GameLanguage next = All[(at < 0 ? 0 : at + 1) % All.Length];
            Select(next);
            return next;
        }

        /// <summary>
        /// 화면에 보일 언어 이름.
        /// ⚠ <b>제 나라 말로 적는다</b>(한국어 / English / Español) — «Spanish» 라고 쓰면
        ///   그 말을 쓰는 사람이 목록에서 자기 언어를 <b>못 찾는다</b>.
        ///   언어 이름을 번역하지 않는 것은 어느 게임에서나 지키는 관례다.
        /// ⚠ <b>스트링 테이블을 안 거친다</b> — 거치면 «지금 언어» 로 번역되어 버려서
        ///   목록의 아홉 줄이 전부 한 언어로 보인다. 그러면 고를 수가 없다.
        /// </summary>
        public static string NameOf(GameLanguage lang) => lang switch
        {
            GameLanguage.English    => "English",
            GameLanguage.Spanish    => "Español",
            GameLanguage.French     => "Français",
            GameLanguage.German     => "Deutsch",
            GameLanguage.Japanese   => "日本語",
            GameLanguage.Russian    => "Русский",
            GameLanguage.Portuguese => "Português",
            GameLanguage.Polish     => "Polski",
            _                       => "한국어",
        };

        /// <summary>지금 켜져 있는 언어의 이름.</summary>
        public static string CurrentName => NameOf(StringTable.Language);
    }
}
