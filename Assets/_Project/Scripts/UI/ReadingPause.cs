using UnityEngine;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★★ <b>«읽는 동안 멈춘다» 를 한 곳에</b> (2026-08-24 신설 · 유저 지시:
    /// *"도움말 뜨면 게임 일시정지 되야함"*).
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  ★★ <c>timeScale = 0</c> 을 <b>직접 쓰지 않는다</b>
    /// ══════════════════════════════════════════════════════════════════
    /// <see cref="GameSpeedPanel"/> 주석이 못박아 둔 문장이 있다 —
    /// *"<c>timeScale = 0</c> 의 주인이 둘이 되는 것이 이 기능의 유일한 위험이다."*
    /// 지금 주인은 둘이다(일시정지 버튼/P · 패배·승리 화면). 창이 스스로 0 을 쓰면
    /// <b>셋째 주인</b>이 되고 「닫았는데 안 흐른다」 또는 「결과 화면 위에서 다시 흐른다」가 난다.
    /// 그래서 <b>이미 있는 손잡이</b>(<see cref="GameSpeedPanel.SetPaused"/>)만 쓴다.
    ///
    /// <b>규칙은 하나다 — 내가 멈춘 것만 내가 푼다.</b>
    /// <code>
    ///   Acquire() : _mine = !speed.IsPaused;  if (_mine) speed.SetPaused(true,  silent: true);
    ///   Release() : if (_mine) { _mine = false; speed.SetPaused(false, silent: true); }
    /// </code>
    /// 이러면 셋이 공짜로 해결된다:
    ///   ㉠ 유저가 P 로 멈춰 둔 채 창이 뜨면 닫아도 <b>계속 멈춰 있다</b>
    ///   ㉡ 패배·승리 화면에서는 <c>SetPaused</c> 의 «남이 멈춰둔 것은 건드리지 않는다» 가드에
    ///      걸려 아무 일도 안 한다
    ///   ㉢ 일시정지 버튼의 모양도 같이 바뀌어 «왜 멈췄는지» 가 화면에 설명된다
    ///
    /// ★ <b>왜 클래스인가</b> — 조언 카드와 도움말 창 <b>둘</b>이 같은 일을 한다. 각자 열 줄씩
    ///   적어 두면 한쪽만 고쳐지는 날이 온다(<see cref="HudExclusive"/> 가 생긴 것과 같은 이유).
    ///   창이 하나 더 늘어도 필드 하나 + 두 줄이면 붙는다.
    ///
    /// ⚠ <b>어두운 막이나 배타 처리는 이 클래스가 하지 않는다</b> — 그것은 창의 몫이다.
    ///   여기서 다루는 것은 <b>시간</b> 하나뿐이다.
    /// </summary>
    public class ReadingPause
    {
        GameSpeedPanel _speed;

        /// <summary>지금 멈춰 있는 것이 <b>내가 멈춘 것</b>인가 — 소유권 증표.</summary>
        bool _mine;

        /// <summary>내가 멈춰둔 상태인가. 창이 인스펙터·로그에 쓸 수 있다.</summary>
        public bool IsMine => _mine;

        /// <summary>지금 «읽는 중» 이라 멈춰 둔 판이 하나라도 있는가 (아래 ★★★).</summary>
        static int _held;

        /// <summary>
        /// ★★★ <b>도움말이 «자기가 멈춘 것» 을 «유저가 배속을 만졌다» 로 세지 않게 한다</b>
        /// (2026-08-24 · 유저 리포트로 찾은 버그).
        ///
        /// <b>무엇이 있었나</b> — <see cref="Help.HelpService"/> 는 «배속을 처음 만졌을 때»
        /// 조언을 띄우려고 <c>GameSpeedPanel.IsPaused</c> 를 본다. 그런데 조언 카드가 뜨면서
        /// <b>스스로 그 값을 true 로 만든다.</b> 그래서 카드가 뜨는 순간 「배속과 일시정지」
        /// 조언이 대기줄에 들어가고, 카드를 닫으면 곧바로 그것이 튀어나왔다 —
        /// <b>도움말이 도움말을 불러낸다.</b>
        ///
        /// ★ 그래서 «읽는 중인가» 를 <b>여기서</b> 알려 준다. 판단하는 쪽(<c>HelpService</c>)이
        ///   <c>GameSpeedPanel</c> 의 속을 들여다보며 «이 멈춤이 누구 것인가» 를 따지게 만들면
        ///   그 지식이 두 곳으로 갈린다. 멈춤의 주인은 이 클래스다.
        /// ⚠ <b>세는 것은 «실제로 내가 멈춘 것» 뿐이다</b> — 유저가 이미 P 로 멈춰 둔 상태에서
        ///   카드가 떠도 <see cref="Acquire"/> 는 아무것도 하지 않으므로 세지 않는다.
        ///   그때의 멈춤은 <b>정말 유저가 한 것</b>이라 조언이 떠야 맞다.
        /// </summary>
        public static bool AnyHeld => _held > 0;

        /// <summary>도메인 리로드가 꺼져 있어도 판마다 0에서 시작하게 한다(이 프로젝트의 static 규칙).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _held = 0;

        /// <summary>게임을 멈춘다. 이미 남이 멈춰둔 상태면 <b>아무것도 하지 않는다</b>.</summary>
        public void Acquire()
        {
            if (_mine) return;

            EnsurePanel();
            if (_speed == null) return;

            // ⚠ «지금 멈춰 있지 않다» 일 때만 내 것이 된다. 유저가 P 로 멈춰 둔 상태라면
            //   _mine 은 false 로 남고, 그래서 닫을 때도 풀지 않는다(위 ㉠).
            if (_speed.IsPaused) return;

            _mine = true;
            _held++;                       // ★ «읽는 중» 표시 — 위 ★★★
            _speed.SetPaused(true, silent: true);
        }

        /// <summary><b>내가 멈춘 것이면</b> 다시 흐르게 한다. 아니면 아무것도 하지 않는다.</summary>
        public void Release()
        {
            if (!_mine) return;
            _mine = false;
            _held = Mathf.Max(0, _held - 1);   // ⚠ 0 아래로 내려가지 않게 — 두 번 풀려도 안전하게

            EnsurePanel();
            _speed?.SetPaused(false, silent: true);
        }

        /// <summary>
        /// ⚠ 비활성 포함으로 찾는다 — 배속 패널은 평소 켜져 있지만, 씬을 옮기거나 HUD 를
        ///   꺼둔 채 시작하는 경로가 있어서 기본 인자로는 못 잡을 수 있다.
        /// </summary>
        void EnsurePanel()
        {
            if (_speed != null) return;
            _speed = Object.FindAnyObjectByType<GameSpeedPanel>(FindObjectsInactive.Include);
        }
    }
}
