using System.Collections.Generic;
using UnityEngine;

namespace LastSanctuary.UI
{
    /// <summary>
    /// <b>동시에 하나만 켜져 있어야 하는 HUD 창</b>임을 나타낸다.
    /// 지금은 전술 지침 · 부대 설정 · 캐릭터 성장 셋이다 — 셋 다 화면 같은 자리에 뜬다.
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
    /// 조회가 비싸므로 <b>한 번만</b> 하고 캐시한다. 창은 씬에 고정이라 늘거나 줄지 않는다.
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

                CancelMapModes();
            }
            finally { _busy = false; }
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
            if (_scanned) return;
            _scanned = true;
            _panels.Clear();

            // ⚠ 비활성 포함 — 세 창은 평소 꺼져 있다.
            var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,
                                                             FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] is IExclusiveHudPanel panel) _panels.Add(panel);
        }
    }
}
