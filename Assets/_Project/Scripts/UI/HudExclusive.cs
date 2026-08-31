using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastSanctuary.UI
{
    /// <summary>
    /// <b>동시에 하나만 켜져 있어야 하는 HUD 창</b>임을 나타낸다.
    /// 지금은 전술 지침 · 부대 설정 · 캐릭터 성장 · 토벌 지시 넷이다 — 전부 화면 가운데를 덮는다.
    /// </summary>
    public interface IExclusiveHudPanel
    {
        bool IsOpen { get; }
        void Close();
    }

    /// <summary>
    /// HUD 창·모드의 <b>배타 조정자</b>. 창이 열릴 때 <see cref="OpenOnly"/> 를 한 번 부르면
    /// 나머지 창과 "맵 클릭을 기다리는 모드"(집결지 지정 · 건설 자리 지정)가 전부 닫힌다.
    ///
    /// <b>왜 만들었나</b> — 예전에는 창끼리 <b>서로를 직접 닫았다</b>:
    /// <code>
    /// TacticalOrderPanel.SetOpen(true)  → CharacterGrowthPanel.Close()          (부대 설정은 안 닫음)
    /// CharacterGrowthPanel.SetOpen(true)→ TacticalOrderPanel.Close()            (부대 설정은 안 닫음)
    /// SquadPanel.SetOpen(true)          → 전술·성장 둘 다 Close()
    /// </code>
    /// 세 창이 서로를 <b>각자</b> 닫다 보니 조합 3개 중 2개가 빠져 있었고, 그래서
    /// <b>부대 설정을 켠 채로 전술 지침·캐릭터 성장을 같이 열 수 있었다</b>
    /// (유저 리포트 2026-08-13). 창이 하나 늘 때마다 N² 로 늘어나는 구조라 언젠가 반드시
    /// 빠뜨린다 — <b>규칙을 한 곳에 모은다.</b>
    ///
    /// <b>맵 클릭 모드도 같이 끈다</b> — 집결지 지정·건설 자리 지정은 "다음 클릭을 먹는" 상태라
    /// 창이 그 위에 열리면 어느 쪽이 클릭을 가져갈지 알 수 없다. 창을 여는 것이 곧
    /// "그 조작은 그만두겠다"는 뜻이다(<c>ActionPanel.HandleSquad</c> 가 이미 같은 판단을 했다).
    ///
    /// ⚠ <b>비활성 오브젝트도 찾아야 한다.</b> 세 창은 평소 꺼져 있어서
    /// <c>FindObjectsByType</c> 기본 인자로는 잡히지 않는다 —
    /// <c>FindObjectsInactive.Include</c> 가 반드시 필요하다(59-6절의 그 함정과 같은 뿌리).
    /// 조회가 비싸므로 <b>씬마다 한 번만</b> 하고 캐시한다. 창은 씬에 고정이라 늘거나 줄지 않는다.
    ///
    /// ⚠⚠ <b>«씬마다» 가 핵심이다</b> — 예전에는 <b>프로세스마다</b> 한 번이었고, 그래서 씬을
    /// 다시 여는 순간(게임 재시작 · 로비 왕복 · 패배/승리 다시하기) 목록이 <b>파괴된 컴포넌트</b>로
    /// 가득 차 이 클래스가 통째로 무력해졌다. 자세한 것은 <see cref="Invalidate"/> 의 ⚠⚠.
    /// </summary>
    public static class HudExclusive
    {
        static readonly List<IExclusiveHudPanel> _panels = new List<IExclusiveHudPanel>();
        static bool _scanned;

        /// <summary>재진입 방지 — <c>Close()</c> 안에서 또 <see cref="OpenOnly"/> 가 불려도 안전하게.</summary>
        static bool _busy;

        /// <summary>플레이 모드를 다시 시작할 때 옛 씬의 컴포넌트가 남지 않게 (도메인 리로드 off 대비).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            _panels.Clear();
            _scanned = false;
            _busy = false;

            // ★★★ 씬을 다시 열 때마다 목록을 버린다 — 아래 <see cref="Invalidate"/> 의 ⚠⚠ 참조.
            //   ⚠ 이 <c>Reset</c> 은 «프로세스마다 한 번» 이지만, 도메인 리로드를 꺼 두면
            //     구독이 <b>지난 플레이에서 살아남는다</b>. -= 를 먼저 해서 두 번 걸리지 않게 한다
            //     (정적 메서드라 델리게이트가 같은 것으로 비교된다 — 람다로 쓰면 안 된다).
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Invalidate();

        /// <summary>
        /// ★★★ <b>스캔한 창 목록을 버린다</b> (2026-08-25 · 유저 리포트:
        /// *"게임 재시작을 누르거나 게임을 껐다 키면 UI 창들이 여러개 켜지고 esc 로도 종료가 되지 않아"*).
        ///
        /// ══════════════════════════════════════════════════════════════════
        ///  ⚠⚠ 무슨 일이 있었나 — <b>씬을 다시 열면 목록이 시체로 가득 찬다</b>
        /// ══════════════════════════════════════════════════════════════════
        /// <see cref="EnsureScanned"/> 는 «창은 씬에 고정이라 늘거나 줄지 않는다» 는 전제로
        /// <b>한 번만</b> 훑고 캐시한다. 그 전제는 <b>씬 하나 안에서만</b> 참이었다:
        ///
        /// <code>
        ///   로비 ▸ 게임 시작            → Proto_01 을 연다 → 여기서 스캔 (정상)
        ///   환경설정 ▸ 게임 재시작      → Proto_01 을 <b>다시</b> 연다 → 옛 창은 전부 파괴
        ///   환경설정 ▸ 로비 ▸ 이어하기  → 같은 일
        ///   패배/승리 ▸ 다시하기        → 같은 일
        /// </code>
        ///
        /// 목록에 남은 것은 <b>파괴된 컴포넌트</b>뿐이고, 세 함수가 전부 그것을 조용히 건너뛴다
        /// (<c>p is Object o &amp;&amp; o == null</c>). 그래서 <b>새 씬의 창은 목록에 없다</b>:
        ///
        /// * <see cref="OpenOnly"/> 가 <b>아무것도 닫지 못한다</b> → 전술 지침·캐릭터 성장이 <b>동시에</b> 열린다.
        /// * <see cref="CloseOpenPanel"/> 이 <c>false</c> 를 돌려준다 → Esc 가 «닫을 창이 없다» 로
        ///   판단하고 <b>환경 설정을 하나 더 연다</b> — 누를수록 나빠진다.
        ///
        /// ★ <c>RuntimeInitializeOnLoadMethod</c> 로는 못 막는다. 그것은 <b>프로세스마다 한 번</b>이라
        ///   씬을 다시 여는 것으로는 돌지 않는다 — <see cref="LastSanctuary.Save.RunResetService"/> 가
        ///   생긴 이유와 <b>똑같은 뿌리</b>다(그 클래스의 ⚠⚠).
        ///
        /// ★ <b>등록 방식(<c>OnEnable</c> 에서 Register)으로는 못 바꾼다</b> — 창은 평소 비활성이고
        ///   유니티는 비활성 오브젝트의 <c>Awake</c>·<c>OnEnable</c> 을 <b>아예 부르지 않는다</b>.
        ///   «한 번도 열린 적 없는 창» 이 스스로 등록할 방법이 없어서 훑는 것이다. 그래서 고칠 곳은
        ///   «훑는 방법» 이 아니라 <b>«언제 다시 훑는가»</b> 다.
        /// </summary>
        public static void Invalidate()
        {
            _panels.Clear();
            _scanned = false;
        }

        /// <summary>
        /// <paramref name="keep"/> 만 남기고 다른 배타 창과 맵 클릭 모드를 전부 닫는다.
        /// 창의 <c>SetOpen(true)</c> 맨 앞에서 부르면 된다.
        /// </summary>
        public static void OpenOnly(IExclusiveHudPanel keep)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                EnsureScanned();

                for (int i = 0; i < _panels.Count; i++)
                {
                    IExclusiveHudPanel p = _panels[i];
                    if (p == null || ReferenceEquals(p, keep)) continue;

                    // 파괴된 컴포넌트는 == null 로 잡히지 않는다(인터페이스 참조) — Unity 오브젝트로 확인한다.
                    if (p is Object o && o == null) continue;
                    if (!p.IsOpen) continue;

                    p.Close();
                }

                // ★ 남은 창을 <b>맨 앞</b>으로 올린다 (유저 지시 2026-08-18:
                //   <i>"일러스트 ui 가 다른 창 켰을때 해당 창들 가리지 못하게"</i>).
                //
                //   유니티 UI 의 그리는 순서는 <b>형제 순서</b>다. 유닛 클릭 초상화
                //   (<c>HUD_Portrait</c>)는 창들보다 뒤에 만들어져 형제 순서가 더 뒤라
                //   <b>항상 창 위에 그려지고 있었다</b>. 초상화 쪽에서 피하게 만들면
                //   앞으로 겹치는 UI 가 생길 때마다 같은 규칙을 또 짜야 한다 —
                //   <b>여는 쪽이 맨 앞으로 올라온다</b>는 규칙 하나로 끝낸다.
                //
                //   ⚠ 창을 새로 만들어도 자동으로 적용된다 — IExclusiveHudPanel 만 구현하면 된다.
                if (keep is MonoBehaviour mb && mb != null) mb.transform.SetAsLastSibling();

                CancelMapModes();
            }
            finally { _busy = false; }
        }

        /// <summary>
        /// ★ <b>지금 열려 있는 배타 창을 닫는다</b> (2026-08-21 · <see cref="HudHotkeys"/> 의 Esc).
        ///
        /// <returns>닫은 창이 있으면 <c>true</c>. 열린 창이 없었으면 <c>false</c>.</returns>
        ///
        /// ★ 목록을 <see cref="OpenOnly"/> 와 <b>공유</b>한다 — 창이 새로 생겨도
        ///   <see cref="IExclusiveHudPanel"/> 만 구현하면 Esc 가 저절로 닫는다.
        ///   («규칙을 한 곳에 모은다» 는 이 클래스의 존재 이유 그대로다.)
        /// ⚠ 배타 창은 «동시에 하나» 가 규칙이라 하나만 닫으면 끝이지만, 혹시 둘이 열려
        ///   있어도 <b>전부</b> 닫는다 — 한 번의 Esc 로 화면이 확실히 정리되는 편이 낫다.
        /// ★★★ <b>2026-08-31 — 창은 «닫기를 거절» 할 수 있다.</b>
        ///   <see cref="EventPanel"/> 은 선택지를 고르기 전에는 <c>Close()</c> 를 무시한다
        ///   (유저 지시: *"반드시 이벤트 선택지부터 선택"*). 그래서 «불렀으니 닫혔다» 고
        ///   가정하지 않는다 — <b>부른 뒤에 <c>IsOpen</c> 을 다시 본다</b>. 이 값이 틀리면
        ///   Esc 를 받는 쪽이 «닫을 창이 있었다» 고 판단해 <b>환경 설정을 안 열고</b>
        ///   아무 일도 안 하는 상태가 된다.
        /// </summary>
        public static bool CloseOpenPanel()
        {
            EnsureScanned();

            bool closed = false;
            for (int i = 0; i < _panels.Count; i++)
            {
                IExclusiveHudPanel p = _panels[i];
                if (p == null) continue;
                if (p is Object o && o == null) continue;
                if (!p.IsOpen) continue;

                p.Close();
                if (!p.IsOpen) closed = true;      // ★ 거절했으면 «닫았다» 로 세지 않는다
            }
            return closed;
        }

        /// <summary>
        /// ★ <b>지금 열려 있는 배타 창이 있는가</b> (2026-08-24 · <see cref="Help.HelpService"/>).
        ///
        /// <b>왜 필요한가</b> — 조언 카드는 «그 상황이 처음 왔을 때» 저절로 뜬다. 그런데 사건 창이나
        /// 성장 창이 열려 있는 위에 덮이면 <b>그 창의 선택지가 안 보인다</b>. 카드는 창이 닫힐 때까지
        /// 대기줄에서 기다려야 하고, 그 판단에 필요한 것이 이 한 줄이다.
        ///
        /// ★ <see cref="CloseOpenPanel"/> 과 목록을 <b>공유</b>한다 — 창이 새로 생겨도
        ///   <see cref="IExclusiveHudPanel"/> 만 구현하면 저절로 셈에 들어온다.
        /// </summary>
        public static bool AnyOpen()
        {
            EnsureScanned();

            for (int i = 0; i < _panels.Count; i++)
            {
                IExclusiveHudPanel p = _panels[i];
                if (p == null) continue;
                if (p is Object o && o == null) continue;
                if (p.IsOpen) return true;
            }
            return false;
        }

        /// <summary>
        /// ★★★ <b>창 하나를 이름으로 열거나 닫는다</b> (2026-08-24 · 도움말 안내가 쓴다).
        ///
        /// <b>왜 여기 있나</b> — 도움말의 「자세히 보기」는 <b>그 창을 실제로 띄워 놓고</b> 안을
        /// 짚어야 한다(유저 지시). 그런데 «창을 연다» 는 `gameObject.SetActive(true)` 가 아니다 —
        /// 각 창의 <c>SetOpen</c> 이 <see cref="OpenOnly"/> 로 다른 창을 닫고, 목록을 다시 그리고,
        /// 맨 앞으로 올라온다. 그 절차를 건너뛰면 <b>내용이 빈 창</b>이 뜬다.
        ///
        /// ★ <b>스위치를 <see cref="IExclusiveHudPanel"/> 에 올리지 않은 이유</b> —
        ///   <c>EventPanel</c>·<c>RelicDigPanel</c> 도 이 인터페이스를 구현하는데, 그 둘은
        ///   «사건이 일어나서» 뜨는 창이라 <b>바깥에서 열 수 있는 것이 아니다</b>.
        ///   인터페이스에 <c>SetOpen</c> 을 올리면 그 둘에 <b>가짜 구현</b>을 넣어야 한다.
        ///   그래서 «열 수 있는 창» 만 여기 <b>명시적으로</b> 적는다 — 컴파일러가 검사해 주고,
        ///   창이 하나 늘 때 <b>한 줄만</b> 더하면 된다.
        ///
        /// ⚠ 여기 없는 창을 넘기면 <c>false</c> 를 돌려준다 — <b>조용히 SetActive 로 때우지 않는다.</b>
        ///   내용이 빈 창이 뜨는 것보다 «못 열었다» 를 부르는 쪽이 아는 것이 낫다.
        /// </summary>
        /// <returns>열거나 닫는 데 성공했으면 <c>true</c>.</returns>
        public static bool TryOpen(Transform window, bool open)
        {
            if (window == null) return false;

            if (window.TryGetComponent(out TacticalOrderPanel tactics)) { tactics.SetOpen(open); return true; }
            if (window.TryGetComponent(out SquadPanel squad)) { squad.SetOpen(open); return true; }
            if (window.TryGetComponent(out CharacterGrowthPanel growth)) { growth.SetOpen(open); return true; }
            if (window.TryGetComponent(out RelicPanel relic)) { relic.SetOpen(open); return true; }
            if (window.TryGetComponent(out SubjugationPanel subjugate)) { subjugate.SetOpen(open); return true; }
            if (window.TryGetComponent(out SettingsPanel settings)) { settings.SetOpen(open); return true; }
            if (window.TryGetComponent(out HelpPanel help)) { help.SetOpen(open); return true; }

            return false;
        }

        /// <summary>맵 클릭을 기다리는 모드를 전부 끊는다 (집결지 지정 · 건설 자리 지정).</summary>
        public static void CancelMapModes()
        {
            RallyPointService rally = RallyPointService.Instance;
            if (rally != null && rally.IsPicking) rally.CancelPicking();

            Buildings.BuildService build = Buildings.BuildService.Instance;
            if (build != null && build.IsPicking) build.CancelPicking();
        }

        static void EnsureScanned()
        {
            if (_scanned && IsCacheAlive()) return;
            _scanned = true;
            _panels.Clear();

            // ⚠ 비활성 포함 — 세 창은 평소 꺼져 있다.
            var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,
                                                             FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] is IExclusiveHudPanel panel) _panels.Add(panel);
        }

        /// <summary>
        /// ★ <b>캐시가 아직 «이 씬의 것» 인가</b> (2026-08-25 · <see cref="Invalidate"/> 의 안전망).
        ///
        /// <see cref="SceneManager.sceneLoaded"/> 구독이 정본이고 이쪽은 <b>그물의 두 번째 겹</b>이다.
        /// 파괴된 컴포넌트가 <b>하나라도</b> 있으면 목록 전체가 옛 씬의 것이므로 다시 훑는다.
        ///
        /// ★ <b>빈 목록도 «못 믿는다» 로 본다</b> — 창이 하나도 없는 씬(로비·오프닝)에서
        ///   한 번 훑고 나면 그 빈 목록이 게임 씬까지 따라가 <b>같은 증상</b>을 낳는다.
        ///
        /// ⚠ 값이 아홉 개 남짓이라 매 호출 검사해도 싸다 — 이 함수를 프레임마다 부르는 곳은
        ///   <c>HelpService</c> 의 <see cref="AnyOpen"/> 하나뿐이고, 거기서도 참조 비교 아홉 번이다.
        /// </summary>
        static bool IsCacheAlive()
        {
            if (_panels.Count == 0) return false;

            for (int i = 0; i < _panels.Count; i++)
            {
                IExclusiveHudPanel p = _panels[i];
                if (p == null) return false;
                if (p is Object o && o == null) return false;   // 파괴된 컴포넌트 — 옛 씬의 것이다
            }
            return true;
        }
    }
}
