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
    /// ⚠ <b>고르는 창을 따로 두지 않는다</b> — 언어가 <b>둘뿐</b>이라 목록을 띄우면
    ///   누르는 횟수만 늘어난다. 배속 버튼과 같은 «누르면 다음» 이다.
    /// </summary>
    public static class LanguageSetting
    {
        /// <summary>언어 선택을 남기는 <see cref="PlayerPrefs"/> 열쇠.</summary>
        public const string PrefsKey = "ls_language";

        /// <summary>지난번에 고른 언어를 되살린다. 남긴 것이 없으면 한국어.</summary>
        public static void Restore()
        {
            int saved = PlayerPrefs.GetInt(PrefsKey, (int)GameLanguage.Korean);
            StringTable.Language = saved == (int)GameLanguage.English
                ? GameLanguage.English
                : GameLanguage.Korean;
        }

        /// <summary>한국어 ↔ English 를 오간다. 바뀐 언어를 돌려준다.</summary>
        public static GameLanguage Toggle()
        {
            GameLanguage next = StringTable.Language == GameLanguage.Korean
                ? GameLanguage.English
                : GameLanguage.Korean;

            StringTable.Language = next;
            PlayerPrefs.SetInt(PrefsKey, (int)next);
            PlayerPrefs.Save();
            return next;
        }

        /// <summary>
        /// 화면에 보일 언어 이름.
        /// ⚠ <b>제 나라 말로 적는다</b>(한국어 / English) — «Korean» 이라고 쓰면 한국어를
        ///   못 읽는 사람이 지금 무엇이 켜져 있는지 알 수 없다. 언어 이름은 번역하지 않는 것이 관례다.
        /// </summary>
        public static string NameOf(GameLanguage lang) =>
            lang == GameLanguage.English ? "English" : "한국어";

        /// <summary>지금 켜져 있는 언어의 이름.</summary>
        public static string CurrentName => NameOf(StringTable.Language);
    }
}
