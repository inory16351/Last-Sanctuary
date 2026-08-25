using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 웨이브 타이머 옆의 <b>배속 버튼</b> (x1 · x2 · x4 · x8). 유저 지시 2026-08-13:
    /// "웨이브 타이머 옆에 배속 버튼 넣어서 x2 / x4 / x8 버튼 넣어줘 실제 배속도 가능하게".
    ///
    /// <b>어떻게 배속하나 — <see cref="Time.timeScale"/> 한 줄이다.</b>
    /// 이 프로젝트의 게임 로직(이동·공격 쿨다운·재생·침식·웨이브 타이머)은 전부
    /// <c>Time.deltaTime</c>/<c>Time.time</c> 기준이라 <c>timeScale</c> 을 올리면 그대로 빨라진다.
    /// 반대로 HUD·카메라는 이미 <c>Time.unscaledTime</c> 을 쓰고 있어서(<see cref="CharacterRosterPanel"/>·
    /// <c>CameraRigController</c>) 배속과 무관하게 똑같이 반응한다 — 29절의 패배 화면이
    /// <c>timeScale = 0</c> 으로 게임만 멈추는 것과 <b>완전히 같은 구조</b>다.
    ///
    /// <b>물리 보폭도 같이 올린다</b> — <see cref="Time.fixedDeltaTime"/> 을 그대로 두면 8배속에서
    /// 한 프레임에 FixedUpdate 가 8배 돌아 프레임이 급락한다. 배속만큼 키워 호출 횟수를 유지한다.
    ///
    /// ⚠ <b>패배·승리 화면과 충돌하지 않는다</b> — 그쪽은 <c>timeScale = 0</c> 으로 멈추고
    /// 스스로 1 로 되돌린다(<see cref="DefeatPanel"/>). 그래서 이 패널은 게임이 멈춰 있는 동안
    /// (<see cref="Time.timeScale"/> 이 0) 배속을 다시 걸지 않는다. 되돌아온 뒤 유저가 다시 누르면 된다.
    ///
    /// ⚠ <b>에디터의 <c>Time.timeScale</c> 은 플레이 모드를 나가도 유지된다.</b> 8배속으로 둔 채
    /// Stop 을 누르면 다음 플레이가 8배속으로 시작한다 — <see cref="OnDisable"/> 에서 되돌린다
    /// (<see cref="DefeatPanel"/> 이 같은 함정을 이미 겪었다).
    ///
    /// <b>버튼은 씬에 실물로 있다</b>(준수사항 §10 H-1, MCP 생성). 이 스크립트는 이름으로 찾아
    /// 배선하고 값만 칠한다 — MCP 로는 이벤트·오브젝트 참조를 인스펙터에 넣을 수 없기 때문이다
    /// (진행상황 8절 4번). 다른 HUD 패널이 전부 쓰는 방식과 같다.
    ///
    /// ─────────────────────────────────────────────────────────────────────
    /// <b>일시정지</b>(유저 지시 2026-08-18: <i>"배속 버튼 옆에 일시정지 버튼 넣기"</i>)
    ///
    /// 배속과 <b>같은 손잡이</b>(<see cref="Time.timeScale"/>)를 쓴다 — 0 이 곧 정지다.
    /// 그래서 별도 시스템을 만들지 않고 이 패널이 배속 단계 하나를 더 든 것처럼 다룬다.
    ///
    /// ★★ <b>timeScale = 0 의 주인이 둘이 되는 것이 이 기능의 유일한 위험이다.</b>
    /// 패배·승리 화면(<see cref="DefeatPanel"/> · <see cref="VictoryPanel"/>)도 0 으로 멈추는데,
    /// 그쪽이 멈춰둔 것을 이 패널이 "일시정지 해제" 로 풀어버리면 <b>끝난 게임이 다시 흐른다.</b>
    /// 그래서 <b>내가 멈춘 것인지</b>(<see cref="_paused"/>)를 반드시 들고 다니고,
    ///   · 내가 멈춘 게 아닌데 0 이면 → 일시정지 버튼 자체를 <b>먹지 않는다</b>
    ///   · 내가 멈춘 상태인데 밖에서 0 이 아니게 되면 → 소유권을 잃은 것으로 보고 스스로 푼다
    /// 두 방향을 모두 막는다. <see cref="Apply"/> 가 원래 갖고 있던
    /// "0 인 동안에는 배속을 다시 걸지 않는다" 가드와 <b>같은 판단의 확장</b>이다.
    ///
    /// ⚠ 정지 중에도 HUD·카메라는 살아 있다 — 둘 다 <c>Time.unscaledTime</c> 기준이라
    /// 원래부터 그렇게 동작한다(패배 화면에서 버튼을 누를 수 있는 것과 같은 이유).
    ///
    /// ⚠ <b>정지 중에는 <see cref="Time.fixedDeltaTime"/> 을 건드리지 않는다</b> — 배속처럼
    /// 곱하면 0 이 되어 유니티가 예외를 던진다. 어차피 <c>timeScale</c> 이 0 이면
    /// FixedUpdate 가 돌지 않으므로 원래 값 그대로 두면 된다.
    /// </summary>
    public class GameSpeedPanel : MonoBehaviour
    {
        [Header("배속 단계")]
        [Tooltip("버튼이 만들어질 순서대로의 배속. 씬의 자식 버튼 이름 'x1' 'x2' … 와 짝을 맞춘다.\n" +
                 "값을 늘리려면 여기와 씬 버튼을 같이 늘려야 한다")]
        [SerializeField] float[] speeds = { 1f, 2f, 4f, 8f };

        [Header("색")]
        [Tooltip("지금 선택된 배속 버튼의 배경색")]
        [SerializeField] Color activeColor = new Color(0.45f, 0.95f, 0.78f, 0.92f);

        [Tooltip("선택되지 않은 버튼의 배경색")]
        [SerializeField] Color idleColor = new Color(0.10f, 0.13f, 0.18f, 0.80f);

        [Tooltip("선택된 버튼의 글자색")]
        // ★★ <b>선택된 배속의 글자색</b> (2026-08-25 · 유저 지시: *"배속 선택했을때
        //   텍스트가 사라지게 하지 말고 다른 것들처럼 초록색으로 표기해줘"*).
        //
        // ⚠ 예전 값은 <b>거의 검정</b>(0.05, 0.08, 0.10)이었다 — 그때는 선택된 칩의
        //   배경이 <b>밝은 청록</b>이라 어두운 글자가 맞았다. 픽셀 UI 로 바꾸면서 «켜짐»
        //   그림이 <b>어두운 청록 판</b>이 되자 검은 글자가 그대로 묻혀 <b>사라진 것처럼</b>
        //   보였다. 이제 <see cref="HudTheme.TextAccent"/> 와 같은 청록을 쓴다 —
        //   각성 금색·부대 색처럼 «강조는 한 곳에서» 규칙 그대로다.
        [SerializeField] Color activeTextColor = new Color(0.45f, 0.95f, 0.78f, 1f);

        [Tooltip("선택되지 않은 버튼의 글자색")]
        [SerializeField] Color idleTextColor = new Color(0.78f, 0.86f, 0.92f, 1f);

        [Header("일시정지")]
        [Tooltip("일시정지 버튼의 자식 오브젝트 이름. 씬의 'Pause' 와 짝을 맞춘다")]
        [SerializeField] string pauseButtonName = "Pause";

        [Tooltip("게임이 흐르는 동안 일시정지 버튼에 적히는 글자 (누르면 멈춘다)")]
        [SerializeField] string pauseLabel = "정지";

        [Tooltip("멈춰 있는 동안 일시정지 버튼에 적히는 글자 (누르면 다시 흐른다)")]
        [SerializeField] string resumeLabel = "재개";

        [Header("동작")]
        [Tooltip("키보드 1·2·3·4 로도 바꿀 수 있게 한다")]
        [SerializeField] bool keyboardShortcuts = true;

        [Tooltip("키보드 P 로도 일시정지/재개할 수 있게 한다. " +
                 "스페이스는 카메라 되돌리기(CameraRigController), 숫자는 배속이 이미 쓰고 있다")]
        [SerializeField] bool pauseKeyboardShortcut = true;

        [Tooltip("배속을 바꿀 때 HUD 로그에 남긴다")]
        [SerializeField] bool logChanges = true;

        readonly List<Button> _buttons = new List<Button>();
        readonly List<Image> _backgrounds = new List<Image>();
        readonly List<TMP_Text> _labels = new List<TMP_Text>();

        Button _pauseButton;
        Image _pauseBackground;
        TMP_Text _pauseLabel;

        int _index;

        /// <summary>
        /// <b>내가 멈춰둔 상태인가.</b> 단순한 표시가 아니라 <b>소유권 증표</b>다 —
        /// 패배·승리 화면도 <c>timeScale = 0</c> 을 쓰기 때문에, "지금 0 이다" 만으로는
        /// 누가 멈춘 것인지 알 수 없다. 이 칸이 true 일 때만 이 패널이 0 을 풀 수 있다.
        /// </summary>
        bool _paused;

        /// <summary>지금 걸려 있는 배속. 다른 시스템이 참고할 수 있다.</summary>
        public float CurrentSpeed => _index >= 0 && _index < speeds.Length ? speeds[_index] : 1f;

        /// <summary>지금 이 패널이 게임을 멈춰둔 상태인가. 다른 시스템이 참고할 수 있다.</summary>
        public bool IsPaused => _paused;

        /// <summary>
        /// 배속을 걸지 않은 상태의 물리 보폭. <see cref="Time.fixedDeltaTime"/> 을 배속만큼
        /// 곱해 쓰므로 원래 값을 기억해둬야 한다(누적해서 곱하면 값이 폭주한다).
        /// </summary>
        float _baseFixedDelta;

        void Awake() => _baseFixedDelta = Time.fixedDeltaTime;

        void Start()
        {
            // MCP 로는 씬 오브젝트 참조를 인스펙터에 넣을 수 없어서(진행상황 8절 4번) 이름으로 찾는다.
            for (int i = 0; i < speeds.Length; i++)
            {
                Transform child = transform.Find(ButtonName(speeds[i]));
                if (child == null)
                {
                    Debug.LogError($"[배속] 자식 버튼 '{ButtonName(speeds[i])}' 을 찾지 못했습니다.", this);
                    enabled = false;
                    return;
                }

                var button = child.GetComponent<Button>();
                if (button == null)
                {
                    Debug.LogError($"[배속] '{child.name}' 에 Button 컴포넌트가 없습니다.", this);
                    enabled = false;
                    return;
                }

                int captured = i;                       // 클로저가 반복 변수를 잡지 않게 복사한다
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => Select(captured));

                _buttons.Add(button);
                _backgrounds.Add(child.GetComponent<Image>());
                _labels.Add(child.GetComponentInChildren<TMP_Text>());
            }

            BindPauseButton();
            Select(0, silent: true);
        }

        /// <summary>
        /// 일시정지 버튼을 찾아 배선한다. <b>없으면 조용히 넘어간다</b> — 배속 버튼과 달리
        /// 이 버튼이 빠져도 게임은 그대로 돌아가므로, 씬이 아직 갱신되지 않은 상태에서
        /// 콘솔을 에러로 채우지 않는다(키보드 P 는 그래도 동작한다).
        /// </summary>
        void BindPauseButton()
        {
            if (string.IsNullOrWhiteSpace(pauseButtonName)) return;

            Transform child = transform.Find(pauseButtonName);
            if (child == null) return;

            _pauseButton = child.GetComponent<Button>();
            _pauseBackground = child.GetComponent<Image>();
            _pauseLabel = child.GetComponentInChildren<TMP_Text>();

            if (_pauseButton == null) return;
            _pauseButton.onClick.RemoveAllListeners();
            _pauseButton.onClick.AddListener(TogglePause);
        }

        /// <summary>
        /// 플레이 모드를 나갈 때 배속을 되돌린다.
        /// ⚠ 에디터의 <c>timeScale</c> 은 플레이 모드를 나가도 유지되므로 반드시 필요하다.
        /// </summary>
        void OnDisable()
        {
            // 패배·승리 화면이 0 으로 멈춰둔 상태라면 그쪽 책임이므로 건드리지 않는다.
            // ★ 단, <b>내가 멈춘 것</b>(_paused)이면 여기서 반드시 풀어야 한다 —
            //   안 그러면 일시정지한 채로 Stop 을 누른 다음 플레이가 멈춘 채로 시작한다
            //   (이 클래스가 원래 배속에서 겪은 그 함정과 같은 것이다).
            if (Time.timeScale <= 0f && !_paused) return;

            _paused = false;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _baseFixedDelta;
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;   // 구 Input Manager 혼용 금지(U-D7)
            if (kb == null) return;

            // ⚠ 내가 멈춘 상태인데 밖에서 시간이 다시 흐르면(예: 패배 화면이 되돌려 놓았다)
            //   소유권을 잃은 것이므로 표시를 사실에 맞춘다 — 안 그러면 "정지 중"이라고
            //   적힌 채 게임이 흘러 버튼이 거짓말을 한다.
            if (_paused && Time.timeScale > 0f)
            {
                _paused = false;
                Paint();
            }

            // ★★★ <b>키를 여기서 읽지 않는다</b> (2026-08-25 · 유저 지적:
            //   *"그 단축키 지금 배속 설정이랑 스페이스도 있지 않냐?"*).
            //
            //   P·1·2·3·4 가 <b>이 Update 에 박혀</b> 있어서 «단축키 설정» 창이 그 키들을
            //   보여주지도, 바꾸지도 못했다. 이제 <see cref="HotkeyService"/> 가 들고
            //   <c>HudHotkeys</c> 가 읽어 <see cref="TogglePause"/>·<see cref="Select"/> 를 부른다.
            //   ⚠ 아래 <see cref="ShortcutsEnabled"/>·<see cref="PauseShortcutEnabled"/> 는
            //     그쪽이 «이 기능을 단축키로 쓸지» 를 물어보는 문이다 — 인스펙터 값의 뜻이 살아 있다.
        }

        // ==================================================================
        // 일시정지
        // ==================================================================

        /// <summary>일시정지 ↔ 재개. 버튼과 키보드 P 가 함께 부른다.</summary>
        public void TogglePause() => SetPaused(!_paused);

        /// <summary>배속 단축키를 쓸지 (인스펙터 값). <c>HudHotkeys</c> 가 물어본다.</summary>
        public bool ShortcutsEnabled => keyboardShortcuts;

        /// <summary>일시정지 단축키를 쓸지 (인스펙터 값). <c>HudHotkeys</c> 가 물어본다.</summary>
        public bool PauseShortcutEnabled => pauseKeyboardShortcut;

        /// <summary>
        /// 게임을 멈추거나 다시 흐르게 한다.
        ///
        /// ★ <b>남이 멈춰둔 것은 건드리지 않는다</b> — 패배·승리 화면이 <c>timeScale = 0</c> 으로
        /// 멈춰둔 동안(그때 <see cref="_paused"/> 는 false 다) 이 함수는 아무것도 하지 않는다.
        /// 그러지 않으면 결과 화면 위에서 이 버튼을 눌러 <b>끝난 게임을 다시 흐르게</b> 할 수 있다.
        /// </summary>
        public void SetPaused(bool value, bool silent = false)
        {
            if (_paused == value) return;
            if (Time.timeScale <= 0f && !_paused) return;   // 남이 멈춰둔 상태 — 관여하지 않는다

            _paused = value;

            // 재개일 때만 force — 위 가드를 통과했다는 것은 <b>0 의 주인이 나였다</b>는 뜻이다.
            Apply(force: !value);
            Paint();

            if (!silent && logChanges)
                HudLog.Add(value ? "일시정지" : $"재개 ({ButtonName(CurrentSpeed)})", HudLogKind.Info);
        }

        /// <summary>배속 단계를 고른다. 범위 밖이면 조용히 무시한다(키보드 단축키가 부를 수 있다).</summary>
        public void Select(int index, bool silent = false)
        {
            if (index < 0 || index >= speeds.Length) return;

            _index = index;

            // 멈춰 있을 때 배속을 고르면 <b>그 배속으로 재개</b>한다 — 정지 상태에서 배속만
            // 바꾸고 화면이 그대로 멈춰 있으면 "버튼이 안 먹는다" 로 보인다.
            bool resuming = _paused;
            if (resuming) _paused = false;

            Apply(force: resuming);
            Paint();

            if (!silent && logChanges)
                HudLog.Add($"게임 속도 {ButtonName(speeds[index])}", HudLogKind.Info);
        }

        /// <summary>
        /// 지금 상태를 <see cref="Time.timeScale"/> 에 반영한다.
        ///
        /// ★ <paramref name="force"/> — <b>내가 걸어둔 정지를 푸는 순간에만</b> true 다.
        /// 아래의 "0 이면 손대지 않는다" 가드는 <b>남이 멈춰둔 것</b>을 지키기 위한 것인데,
        /// 방금까지 0 의 주인이 나였다면 그 가드가 <b>내 재개까지 막아</b> 화면이 영영 멈춘다.
        /// 소유권 판정은 부르는 쪽(<see cref="SetPaused"/>·<see cref="Select"/>)이 이미 했으므로
        /// 여기서는 그 결과만 받는다.
        /// </summary>
        void Apply(bool force = false)
        {
            // ★ 내가 멈춘 상태라면 무조건 0 이다 — 아래 가드보다 먼저 본다.
            //   ⚠ fixedDeltaTime 은 건드리지 않는다: 0 을 곱하면 유니티가 예외를 던지고,
            //     timeScale 이 0 이면 FixedUpdate 자체가 안 돌아 원래 값을 둬도 문제가 없다.
            if (_paused)
            {
                Time.timeScale = 0f;
                return;
            }

            float speed = Mathf.Max(0.01f, speeds[_index]);

            // 패배·승리로 멈춰 있는 동안에는 시간을 다시 흐르게 만들지 않는다 —
            // 그쪽이 timeScale 의 주인이고, 되돌리는 것도 그쪽이다.
            if (!force && Time.timeScale <= 0f) return;

            Time.timeScale = speed;
            // 물리 보폭도 같이 키운다 — 안 그러면 8배속에서 FixedUpdate 가 8배 돌아 프레임이 급락한다.
            Time.fixedDeltaTime = _baseFixedDelta * speed;
        }

        void Paint()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                // 멈춰 있는 동안에는 어느 배속도 "지금 걸려 있다" 가 아니다 —
                // 재개하면 돌아갈 배속이라 선택 표시를 끄고 글자만 남긴다.
                bool on = !_paused && i == _index;
                Paint(_backgrounds[i], on ? ButtonState.On : ButtonState.Normal);
                if (_labels[i] != null) _labels[i].color = on ? activeTextColor : idleTextColor;
            }

            if (_pauseBackground != null)
                Paint(_pauseBackground, _paused ? ButtonState.On : ButtonState.Normal);
            if (_pauseLabel != null)
            {
                _pauseLabel.text = _paused ? resumeLabel : pauseLabel;
                _pauseLabel.color = _paused ? activeTextColor : idleTextColor;
            }
        }

        /// <summary>배속 값 → 자식 오브젝트 이름. 정수면 "x2", 소수면 "x1.5".</summary>
        static string ButtonName(float speed) =>
            Mathf.Approximately(speed, Mathf.Round(speed))
                ? $"x{Mathf.RoundToInt(speed)}"
                : $"x{speed:0.##}";

        /// <summary>
        /// 배속 칩의 «선택됨» 을 그림으로 바꾼다 (2026-08-25 · 버튼 그림 도입).
        /// 그림이 없으면 예전처럼 <see cref="activeColor"/>·<see cref="idleColor"/> 를 칠한다.
        /// </summary>
        void Paint(Image img, ButtonState state) =>
            HudTheme.PaintButton(img, state, state == ButtonState.On ? activeColor : idleColor);

    }
}
