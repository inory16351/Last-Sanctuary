using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★ <b>HUD 단축키</b> (2026-08-21 신설 — 유저 지시: *"esc 눌렀을 때 환경설정 창 뜨게하기"*).
    ///
    /// <b>어디에 붙나</b> — 씬의 <c>GameSystems</c> 다. «판 하나에 하나뿐인 서비스» 가 모여 있는
    /// 자리이고, 무엇보다 <b>항상 활성</b>이다.
    ///
    /// ⚠⚠ <b>왜 <see cref="SettingsPanel"/> 안에 넣지 않았나</b> — 그 창은 평소 <b>비활성</b>이고,
    /// 유니티는 비활성 오브젝트의 <c>Update</c> 를 <b>부르지 않는다</b>. 즉 창 안에서 키를
    /// 읽으면 «닫혀 있을 때는 열 수 없다» 가 된다. 이 프로젝트가 네 번 겪은 함정이고
    /// (59-6·88-1절), 이벤트 창이 «안 보이던» 원인도 같은 것이었다.
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  Esc 의 <b>우선순위</b> — 이미 Esc 를 쓰는 곳이 있다
    /// ══════════════════════════════════════════════════════════════════
    /// <code>
    ///   ⓞ 조언 카드가 떠 있으면      → 카드가 먹는다 (여기서는 <b>양보한다</b> · 2026-08-24)
    ///   ① 맵 클릭을 기다리는 중이면  → 그 모드가 스스로 취소한다 (여기서는 <b>손대지 않는다</b>)
    ///   ② 열려 있는 창이 있으면      → 그 창을 닫는다
    ///   ③ 아무것도 없으면            → 환경 설정을 연다
    /// </code>
    ///
    /// <b>F1 — 도움말 창(백과)</b>. Esc 가 이미 환경 설정을 쓰고 있어서 도움말에는 빈 키가
    /// 필요했다(<see cref="HandleHelp"/>).
    ///
    /// ★ ①을 <b>건드리지 않는 것</b>이 중요하다. <c>BuildService.HandlePicking</c> 과
    ///   <c>RallyPointService</c> 가 <b>자기 Update 에서</b> Esc 를 읽어 지정 모드를 취소한다
    ///   (버튼 문구에도 «Esc 취소» 라고 적혀 있다). 여기서 같은 프레임에 창까지 열면
    ///   «취소하려고 눌렀는데 설정이 열린다» 가 된다. 그래서 지정 모드일 때는 <b>양보한다</b>.
    ///
    /// ★ ②는 <see cref="IExclusiveHudPanel"/> 을 그대로 쓴다 — 창이 새로 생겨도 이 규칙이
    ///   저절로 적용된다(<see cref="HudExclusive"/> 가 같은 이유로 만들어졌다).
    ///   ⚠ 환경 설정 창 자신도 배타 창이라, 열려 있을 때 Esc 를 누르면 ②에서 닫힌다 —
    ///     즉 <b>Esc 가 토글</b>처럼 동작한다. 의도한 것이다.
    ///
    /// ⚠ <b>일시정지 중에도 동작해야 한다</b> — <c>Update</c> 는 <c>timeScale</c> 이 0 이어도
    ///   돌지만, 시간을 재는 값은 <c>unscaledTime</c> 을 써야 한다. 여기서는 시간을 재지 않는다.
    /// </summary>
    public class HudHotkeys : MonoBehaviour
    {
        [Header("단축키")]
        [Tooltip("Esc 로 환경 설정을 열고 닫는다. 끄면 Esc 는 지정 모드 취소에만 쓰인다")]
        [SerializeField] bool escapeOpensSettings = true;

        [Tooltip("맵 클릭 지정 모드(집결지·건설) 중에는 Esc 를 양보한다 — 위 ★ 참조. " +
                 "끄면 지정 모드 중에도 창이 열린다(권장하지 않는다)")]
        [SerializeField] bool yieldToMapModes = true;

        [Tooltip("F1 로 도움말 창(백과)을 열고 닫는다 (2026-08-24). " +
                 "Esc 는 이미 환경 설정이 쓰고 있어서 도움말에는 빈 키가 필요했다")]
        [SerializeField] bool f1OpensHelp = true;

        [Tooltip("무엇 때문에 Esc 가 소비됐는지 콘솔에 남긴다 (단축키가 안 먹을 때 켜서 본다)")]
        [SerializeField] bool logKeys;

        /// <summary>
        /// 씬에 이 컴포넌트가 없어도 스스로 붙는다 — 이 프로젝트의 관례
        /// (<c>EventService.EnsureOn</c> 과 같은 취지).
        /// ⚠ 다만 <b>인스펙터에서 켜고 끄려면 실물이 있어야</b> 하므로 씬의 <c>GameSystems</c> 에
        ///   MCP 로 직접 붙여 두었다(§10 H-1).
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        static void EnsureOn()
        {
            if (Object.FindFirstObjectByType<HudHotkeys>(FindObjectsInactive.Include) != null) return;

            GameObject host = GameObject.Find("GameSystems");
            if (host == null) return;          // 게임 씬이 아니다(로비) — 단축키가 필요 없다
            host.AddComponent<HudHotkeys>();
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // ★★★ <b>키를 «장부» 에서 읽는다</b> (2026-08-25 · <see cref="HotkeyService"/>).
            //   예전에는 <c>kb.escapeKey</c>·<c>kb.f1Key</c> 가 여기 <b>박혀</b> 있었다 —
            //   그래서 «단축키 설정» 창을 만들 수가 없었다(바꿀 값이 없었다).
            //
            // ⚠ <b>단축키를 바꾸는 중이면 아무 키도 먹지 않는다</b> — 창이 «다음 키를
            //   기다리는» 상태에서 여기가 같은 키를 먹으면, 예를 들어 «환경 설정을 B 로»
            //   바꾸려고 B 를 누르는 순간 건설이 같이 열린다.
            if (HotkeyPanel.Instance != null && HotkeyPanel.Instance.IsCapturing) return;

            for (int i = 0; i < HotkeyService.All.Length; i++)
            {
                HotkeyAction action = HotkeyService.All[i];
                Key key = HotkeyService.Get(action);
                if (key == Key.None) continue;

                KeyControl control = kb[key];
                if (control == null || !control.wasPressedThisFrame) continue;

                Invoke(action);
            }
        }

        /// <summary>
        /// 기능 하나를 수행한다.
        ///
        /// ★ <b>Esc(환경 설정)만 특별하다</b> — 우선순위가 셋이나 있다(클래스 doc).
        ///   나머지는 «그 창을 토글» 이므로 <see cref="ToggleExclusive"/> 하나로 끝난다.
        /// ⚠ 창을 못 찾으면 <b>경고를 남긴다</b>. 조용히 넘기면 «단축키가 안 먹는다» 로만 보인다.
        /// </summary>
        void Invoke(HotkeyAction action)
        {
            switch (action)
            {
                case HotkeyAction.Settings: HandleEscape(); return;
                case HotkeyAction.Help:     HandleHelp();   return;

                case HotkeyAction.Hotkeys:
                    if (HotkeyPanel.Instance != null) HotkeyPanel.Instance.Toggle();
                    else Debug.LogWarning("[단축키] 단축키 설정 창을 찾지 못했습니다.", this);
                    return;

                // ★ 창이 아니라 <b>행동</b>이다 — 서비스를 직접 부른다.
                //   ⚠ 버튼을 흉내내려고 <c>ActionPanel</c> 을 거치지 않았다. 그 패널에는
                //     싱글턴이 없고, 있다 해도 «버튼을 누른 것처럼» 하는 우회는 버튼 배치가
                //     바뀔 때 조용히 깨진다. 서비스가 규칙(비용·상한)의 정본이다.
                case HotkeyAction.CreateCharacter:
                    Units.CharacterUnit made = CharacterCreationService.Instance?.TryCreate();
                    if (made == null && logKeys)
                        Debug.Log("[단축키] 캐릭터 생성 — 지금은 만들 수 없습니다", this);
                    return;

                case HotkeyAction.Squad:     ToggleExclusive(SquadPanel.Instance);           return;
                case HotkeyAction.Tactics:   ToggleExclusive(TacticalOrderPanel.Instance);   return;
                case HotkeyAction.Growth:    ToggleExclusive(CharacterGrowthPanel.Instance); return;
                case HotkeyAction.Subjugate: ToggleExclusive(SubjugationPanel.Instance);     return;
                case HotkeyAction.Relics:    ToggleExclusive(RelicPanel.Instance);           return;

                // ── 배속·일시정지 (2026-08-25 · GameSpeedPanel 에서 옮겨 왔다) ──
                //   ⚠ 그 패널의 인스펙터 게이트를 <b>존중한다</b> — 껐으면 안 먹는다.
                case HotkeyAction.Pause:
                {
                    GameSpeedPanel speed = Speed();
                    if (speed != null && speed.PauseShortcutEnabled) speed.TogglePause();
                    return;
                }

                case HotkeyAction.Speed1: SelectSpeed(0); return;
                case HotkeyAction.Speed2: SelectSpeed(1); return;
                case HotkeyAction.Speed3: SelectSpeed(2); return;
                case HotkeyAction.Speed4: SelectSpeed(3); return;

                // ── 화면 되돌리기 (CameraRigController 에서 옮겨 왔다) ──
                case HotkeyAction.Recenter:
                {
                    if (_camera == null)
                        _camera = FindAnyObjectByType<CameraControl.CameraRigController>();
                    if (_camera != null) _camera.Recenter();
                    else if (logKeys) Debug.Log("[단축키] 카메라를 찾지 못했습니다", this);
                    return;
                }
            }

            // ⚠ 여기까지 왔다 = enum 에 기능을 더하고 <b>가지를 안 더한 것</b>이다.
            //   조용히 넘기면 «창에는 있는데 눌러도 아무 일이 없다» 가 된다
            //   (HotkeyService 의 ⚠⚠ «넷 다 해야 한다»).
            Debug.LogWarning($"[단축키] 「{HotkeyService.Label(action)}」을 수행하는 " +
                             "가지가 HudHotkeys.Invoke 에 없습니다.", this);
        }

        /// <summary>
        /// 배타 창을 토글한다. <b>열 때는 <see cref="HudExclusive.OpenOnly"/> 규칙을 그 창이
        /// 스스로 따른다</b> — 여기서 다른 창을 닫지 않는다(그 규칙이 두 벌이 되면 어긋난다).
        /// </summary>
        // 캐시 — 클릭할 때만 도는 경로가 아니라 <b>키를 누를 때마다</b> 도므로 찾아 두고 쓴다.
        GameSpeedPanel _speed;
        CameraControl.CameraRigController _camera;

        GameSpeedPanel Speed()
        {
            if (_speed == null) _speed = FindAnyObjectByType<GameSpeedPanel>(FindObjectsInactive.Include);
            return _speed;
        }

        /// <summary>배속 단계를 고른다. ⚠ 패널의 인스펙터 게이트를 존중한다.</summary>
        void SelectSpeed(int index)
        {
            GameSpeedPanel speed = Speed();
            if (speed == null)
            {
                if (logKeys) Debug.Log("[단축키] 배속 패널을 찾지 못했습니다", this);
                return;
            }
            if (!speed.ShortcutsEnabled) return;
            speed.Select(index);
        }

        void ToggleExclusive(IExclusiveHudPanel panel)
        {
            if (panel == null)
            {
                if (logKeys) Debug.Log("[단축키] 그 창이 이 씬에 없습니다.", this);
                return;
            }

            if (panel.IsOpen) panel.Close();
            else HudExclusive.OpenOnly(panel);
        }

        /// <summary>
        /// ★ <b>F1 — 도움말 창(백과)</b> (2026-08-24 · 유저 지시로 도움말 튜토리얼을 붙이면서).
        ///
        /// ⚠ 이 창은 평소 <b>비활성</b>이라 창 안에서 키를 읽으면 «닫혀 있을 때는 열 수 없다» 가
        ///   된다 — 이 클래스가 존재하는 이유 그대로다(위 ⚠⚠).
        /// ★ 조언 카드가 떠 있을 때는 <b>양보한다</b> — 카드가 «자세히 보기» 로 같은 창을 열므로
        ///   여기서 또 열면 카드는 남고 창이 그 아래 열려 둘이 겹친다.
        /// </summary>
        void HandleHelp()
        {
            if (!f1OpensHelp) return;

            if (HelpCardPanel.Instance != null && HelpCardPanel.Instance.IsOpen)
            {
                if (logKeys) Debug.Log("[단축키] F1 — 조언 카드가 떠 있어 양보했습니다", this);
                return;
            }

            HelpPanel help = HelpPanel.Instance;
            if (help == null)
            {
                Debug.LogWarning("[단축키] 도움말 창(Help_Root/HUD_Help)을 찾지 못했습니다. " +
                                 "py -3 Tools/mcp_build_help_ui.py 를 돌리세요.", this);
                return;
            }

            help.Toggle();
            if (logKeys) Debug.Log($"[단축키] F1 — 도움말을 {(help.IsOpen ? "열었" : "닫았")}습니다", this);
        }

        void HandleEscape()
        {
            if (!escapeOpensSettings) return;

            // ── ⓞ 조언 카드가 떠 있으면 <b>카드가 먹는다</b> (2026-08-24) ──
            //   카드는 «읽는 동안 게임을 멈춘» 상태다. 여기서 창을 닫거나 환경 설정을 열면
            //   카드가 멈춰둔 것을 <b>다른 창이 물려받는</b> 꼴이 된다. 카드는 활성이라
            //   자기 Update 에서 Esc 를 읽는다(HelpCardPanel.Update).
            if (HelpCardPanel.Instance != null && HelpCardPanel.Instance.IsOpen)
            {
                if (logKeys) Debug.Log("[단축키] Esc — 조언 카드가 쓰는 중이라 양보했습니다", this);
                return;
            }

            // ── ① 맵 클릭 지정 모드에 양보한다 (위 ★) ──
            if (yieldToMapModes && IsPickingOnMap())
            {
                if (logKeys) Debug.Log("[단축키] Esc — 지정 모드가 쓰는 중이라 양보했습니다", this);
                return;
            }

            // ── ② 열려 있는 창을 닫는다 ──
            if (HudExclusive.CloseOpenPanel())
            {
                if (logKeys) Debug.Log("[단축키] Esc — 열려 있던 창을 닫았습니다", this);
                return;
            }

            // ── ③ 환경 설정을 연다 ──
            SettingsPanel settings = SettingsPanel.Instance;
            if (settings == null)
            {
                Debug.LogWarning("[단축키] 환경 설정 창(UI_Root/HUD_Settings)을 찾지 못했습니다.", this);
                return;
            }

            settings.SetOpen(true);
            if (logKeys) Debug.Log("[단축키] Esc — 환경 설정을 열었습니다", this);
        }

        /// <summary>지금 «다음 맵 클릭을 먹는» 모드가 켜져 있는가.</summary>
        static bool IsPickingOnMap()
        {
            RallyPointService rally = RallyPointService.Instance;
            if (rally != null && rally.IsPicking) return true;

            Buildings.BuildService build = Buildings.BuildService.Instance;
            return build != null && build.IsPicking;
        }
    }
}
