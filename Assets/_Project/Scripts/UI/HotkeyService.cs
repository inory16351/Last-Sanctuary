using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastSanctuary.UI
{
    /// <summary>
    /// HUD 에서 단축키를 붙일 수 있는 <b>기능</b> 목록.
    ///
    /// ⚠⚠ <b>번호를 재사용하지 말 것.</b> 저장 키가 <c>hotkey_&lt;번호&gt;</c> 라서, 가운데를
    /// 지우고 뒤를 당기면 <b>이미 저장된 사람의 단축키가 엉뚱한 기능으로 옮겨간다</b>
    /// (예: 「토벌 지시」에 걸어둔 키가 「건물 건설」의 자리로 읽힌다). 그래서 값을
    /// <b>명시</b>하고, 뺀 번호는 <b>빈 채로 남긴다</b>.
    /// </summary>
    public enum HotkeyAction
    {
        Settings = 0,
        Help = 1,
        CreateCharacter = 2,
        Squad = 3,
        Tactics = 4,
        Growth = 5,

        // 6 = 「건물 건설」 — 2026-08-25 유저 지시로 <b>뺐다</b>
        //   (*"건물 건설은 빼라 · 단축키 건축건설은 빼라"*).
        //   ★ 건설은 «다음 맵 클릭을 먹는 모드» 라 다른 기능과 성격이 다르다 — 켜 두면
        //     Esc 가 «취소» 로 소비되고(HudHotkeys 의 ①) 단축키와 맞물려 헷갈린다.
        //   ⚠ 번호 6 은 <b>비워 둔다</b>. 위 ⚠⚠ 참조.

        Subjugate = 7,
        Relics = 8,
        Hotkeys = 9,

        // ★★ 2026-08-25 — 유저 지적: *"그 단축키 지금 배속 설정이랑 스페이스도 있지 않냐?"*
        //   맞았다. 아래 여섯은 <b>장부 밖에서 각자 키를 읽고 있었다</b> —
        //   <c>GameSpeedPanel.Update</c>(P · 1234)와 <c>CameraRigController.HandleRecenter</c>(Space).
        //   그래서 «단축키 설정» 창이 <b>게임의 단축키를 다 보여주지 못했다</b>. 창이 거짓말을
        //   하는 셈이라 여기로 옮겼다 (*"모든 배정된 핫키 단축키 설정에 넣어"*).
        Pause = 10,
        Speed1 = 11,
        Speed2 = 12,
        Speed3 = 13,
        Speed4 = 14,
        Recenter = 15,
    }

    /// <summary>
    /// ★★ <b>단축키 장부</b> (2026-08-25 신설 — 유저 지시: *"단축키 메뉴 허드 액션 도움말 밑에
    /// 단축키 설정 메뉴 넣고"*).
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  왜 서비스가 필요한가
    /// ══════════════════════════════════════════════════════════════════
    /// 예전에는 <see cref="HudHotkeys"/> 안에 <c>escapeKey</c>·<c>f1Key</c> 가 <b>코드에 박혀</b>
    /// 있었다. 그래서 «단축키 설정» 창을 만들 수가 없었다 — 바꿀 <b>값이 없었다</b>.
    /// 이제 «기능 → 키» 를 이 장부가 들고, 창은 이 장부를 고치고,
    /// <see cref="HudHotkeys"/> 는 이 장부를 읽어 누른 키를 기능으로 옮긴다.
    /// 셋이 각각 <b>기억 / 표시 / 행동</b> 하나씩만 맡는다(SubjugationPanel 과 같은 구성).
    ///
    /// ★ <b>PlayerPrefs 에 저장한다</b> — 판(세이브)이 아니라 <b>사람</b>에 딸린 값이다.
    ///   새 판을 시작해도 단축키는 그대로여야 한다. 음량(<see cref="Save.SaveService.Volume"/>)이
    ///   같은 이유로 같은 자리에 있다.
    ///
    /// ⚠ <b>기본값을 바꾸면 이미 저장된 사람의 값은 안 바뀐다</b>(그게 맞다 — 사람이 고른 값이
    ///   우선이다). 전부 되돌리려면 창의 «기본값으로» 를 쓴다(<see cref="ResetAll"/>).
    ///
    /// ⚠⚠ <b>기능을 새로 더할 때</b>: ① 위 enum 에 더하고 ② <see cref="Defaults"/> 에 기본 키를
    ///   적고 ③ <see cref="Label"/> 에 이름을 적고 ④ <see cref="HudHotkeys"/> 에서 그 기능을
    ///   실제로 수행하는 가지를 더한다. <b>넷 다 해야</b> 창에 뜨고 실제로 눌린다.
    /// </summary>
    public static class HotkeyService
    {
        const string Prefix = "hotkey_";

        /// <summary>기본 배치. Esc·F1 은 2026-08-21 부터 쓰던 것을 그대로 둔다.</summary>
        static readonly Dictionary<HotkeyAction, Key> Defaults = new Dictionary<HotkeyAction, Key>
        {
            { HotkeyAction.Settings,        Key.Escape },
            { HotkeyAction.Help,            Key.F1 },
            { HotkeyAction.CreateCharacter, Key.Q },
            { HotkeyAction.Squad,           Key.W },
            { HotkeyAction.Tactics,         Key.E },
            { HotkeyAction.Growth,          Key.R },
            { HotkeyAction.Subjugate,       Key.T },
            { HotkeyAction.Relics,          Key.G },
            { HotkeyAction.Hotkeys,         Key.F2 },

            { HotkeyAction.Pause,           Key.P },
            { HotkeyAction.Speed1,          Key.Digit1 },
            { HotkeyAction.Speed2,          Key.Digit2 },
            { HotkeyAction.Speed3,          Key.Digit3 },
            { HotkeyAction.Speed4,          Key.Digit4 },
            { HotkeyAction.Recenter,        Key.Space },
        };

        /// <summary>창에 보여줄 이름. ⚠ HUD 버튼의 글자와 같게 둔다 — 다르면 못 찾는다.</summary>
        public static string Label(HotkeyAction action) => action switch
        {
            HotkeyAction.Settings        => "환경 설정",
            HotkeyAction.Help            => "도움말",
            HotkeyAction.CreateCharacter => "캐릭터 생성",
            HotkeyAction.Squad           => "부대 설정",
            HotkeyAction.Tactics         => "전술 지침",
            HotkeyAction.Growth          => "캐릭터 성장",
            HotkeyAction.Subjugate       => "토벌 지시",
            HotkeyAction.Relics          => "유물 관리",
            HotkeyAction.Hotkeys         => "단축키 설정",
            HotkeyAction.Pause           => "일시정지",
            HotkeyAction.Speed1          => "배속 1단계",
            HotkeyAction.Speed2          => "배속 2단계",
            HotkeyAction.Speed3          => "배속 3단계",
            HotkeyAction.Speed4          => "배속 4단계",
            HotkeyAction.Recenter        => "성역으로 화면 되돌리기",
            _                            => action.ToString(),
        };

        /// <summary>기능 목록 — 창이 이 순서로 줄을 그린다(enum 선언 순서).</summary>
        public static readonly HotkeyAction[] All =
            (HotkeyAction[])Enum.GetValues(typeof(HotkeyAction));

        /// <summary>키가 바뀌었다 — 창이 다시 그리도록.</summary>
        public static event Action OnChanged;

        public static Key Default(HotkeyAction action) =>
            Defaults.TryGetValue(action, out Key k) ? k : Key.None;

        /// <summary>이 기능에 걸린 키. 없으면 <see cref="Key.None"/>(= 단축키 없음).</summary>
        public static Key Get(HotkeyAction action)
        {
            string s = PlayerPrefs.GetString(Prefix + (int)action, null);
            if (string.IsNullOrEmpty(s)) return Default(action);
            return Enum.TryParse(s, out Key k) ? k : Default(action);
        }

        /// <summary>
        /// 키를 바꾼다.
        ///
        /// ★★ <b>다른 기능이 그 키를 쓰고 있으면 그쪽을 비운다</b> — 한 키에 두 기능이 걸리면
        /// 누를 때마다 둘 다 열려 «창이 겹쳐 뜬다» 가 된다. 조용히 덮어쓰지 않고
        /// <b>빼앗은 기능을 돌려준다</b>(호출부가 «○○의 단축키가 해제되었습니다» 를 말한다).
        /// </summary>
        /// <returns>이 키를 빼앗긴 기능. 없으면 null.</returns>
        public static HotkeyAction? Set(HotkeyAction action, Key key)
        {
            HotkeyAction? stolen = null;

            if (key != Key.None)
            {
                for (int i = 0; i < All.Length; i++)
                {
                    if (All[i] == action) continue;
                    if (Get(All[i]) != key) continue;
                    PlayerPrefs.SetString(Prefix + (int)All[i], Key.None.ToString());
                    stolen = All[i];
                    break;
                }
            }

            PlayerPrefs.SetString(Prefix + (int)action, key.ToString());
            PlayerPrefs.Save();
            OnChanged?.Invoke();
            return stolen;
        }

        /// <summary>전부 기본값으로.</summary>
        public static void ResetAll()
        {
            for (int i = 0; i < All.Length; i++)
                PlayerPrefs.DeleteKey(Prefix + (int)All[i]);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        /// <summary>
        /// 화면에 보여줄 키 이름. <see cref="Key.None"/> 은 «없음» 으로 적는다 —
        /// 빈 칸이면 «못 불러온 것» 처럼 보인다.
        /// </summary>
        public static string KeyLabel(Key key) => key == Key.None ? "없음" : key.ToString();
    }
}
