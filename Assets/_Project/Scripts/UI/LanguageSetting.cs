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

        /// <summary>
        /// ★★★ <b>판이 시작될 때 스스로 되살린다</b> (2026-08-26 · 유저 리포트:
        /// *"스트링 키 영어 번역이 반영이 안되고 있는데"*).
        ///
        /// <b>무엇이 빠져 있었나</b> — <see cref="Restore"/> 를 부르는 곳이
        /// <c>SettingsPanel.Awake</c> 와 <c>LobbySettingsWindow</c> <b>둘뿐</b>이었다. 그런데
        /// 설정 창은 씬에서 <b>닫힌 창(<c>active = 0</c>)</b> 이라 <c>Awake</c> 가 돌지 않는다.
        /// <c>StringTable.ResetStatics</c> 는 판마다 언어를 한국어로 되돌리므로,
        /// <b>PlayerPrefs 에 English 가 남아 있어도 게임은 늘 한국어로 시작</b>했다.
        /// 설정 창을 «열어야» 영어가 되고, 그 상태에서 언어 버튼을 누르면 한국어로
        /// 돌아가니 <b>«영어가 반영 안 된다»</b> 로 보였다.
        ///
        /// ★ <b>창이 아니라 여기서 부른다.</b> 언어는 «화면 하나의 상태» 가 아니라
        ///   <b>판 전체의 상태</b> 다. 어떤 창이 열려 있든 같아야 하므로, 되살리는 책임도
        ///   창이 아닌 <b>이 규칙 한 곳</b>에 둔다(<c>StringTable.ResetStatics</c> ·
        ///   <c>SquadService.ResetStatics</c> 와 같은 자리).
        /// ★ <b>순서가 맞는다</b> — <c>StringTable.ResetStatics</c> 는
        ///   <c>SubsystemRegistration</c>(더 이르다)에서 언어를 초기화하고, 이 훅은
        ///   <c>BeforeSceneLoad</c> 에서 되살린다. 즉 <b>첫 프레임이 그려지기 전</b>에
        ///   언어가 정해지므로 창마다 «다시 그리기» 가 필요 없다.
        /// ⚠ <c>SettingsPanel.Awake</c> 의 호출은 <b>남겨 둔다</b> — 같은 값을 다시 넣는
        ///   것이라 무해하고(<c>Language</c> 의 setter 가 같은 값이면 이벤트도 안 쏜다),
        ///   그 창은 그 김에 자기 라벨을 갱신한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RestoreOnBoot() => Restore();

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
