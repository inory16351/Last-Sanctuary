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
        [SerializeField] Color activeTextColor = new Color(0.05f, 0.08f, 0.10f, 1f);

        [Tooltip("선택되지 않은 버튼의 글자색")]
        [SerializeField] Color idleTextColor = new Color(0.78f, 0.86f, 0.92f, 1f);

        [Header("동작")]
        [Tooltip("키보드 1·2·3·4 로도 바꿀 수 있게 한다")]
        [SerializeField] bool keyboardShortcuts = true;

        [Tooltip("배속을 바꿀 때 HUD 로그에 남긴다")]
        [SerializeField] bool logChanges = true;

        readonly List<Button> _buttons = new List<Button>();
        readonly List<Image> _backgrounds = new List<Image>();
        readonly List<TMP_Text> _labels = new List<TMP_Text>();

        int _index;

        /// <summary>지금 걸려 있는 배속. 다른 시스템이 참고할 수 있다.</summary>
        public float CurrentSpeed => _index >= 0 && _index < speeds.Length ? speeds[_index] : 1f;

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

            Select(0, silent: true);
        }

        /// <summary>
        /// 플레이 모드를 나갈 때 배속을 되돌린다.
        /// ⚠ 에디터의 <c>timeScale</c> 은 플레이 모드를 나가도 유지되므로 반드시 필요하다.
        /// </summary>
        void OnDisable()
        {
            // 패배·승리 화면이 0 으로 멈춰둔 상태라면 그쪽 책임이므로 건드리지 않는다.
            if (Time.timeScale <= 0f) return;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _baseFixedDelta;
        }

        void Update()
        {
            if (!keyboardShortcuts) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;   // 구 Input Manager 혼용 금지(U-D7)
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame) Select(0);
            else if (kb.digit2Key.wasPressedThisFrame) Select(1);
            else if (kb.digit3Key.wasPressedThisFrame) Select(2);
            else if (kb.digit4Key.wasPressedThisFrame) Select(3);
        }

        /// <summary>배속 단계를 고른다. 범위 밖이면 조용히 무시한다(키보드 단축키가 부를 수 있다).</summary>
        public void Select(int index, bool silent = false)
        {
            if (index < 0 || index >= speeds.Length) return;

            _index = index;
            Apply();
            Paint();

            if (!silent && logChanges)
                HudLog.Add($"게임 속도 {ButtonName(speeds[index])}", HudLogKind.Info);
        }

        void Apply()
        {
            float speed = Mathf.Max(0.01f, speeds[_index]);

            // 패배·승리로 멈춰 있는 동안에는 시간을 다시 흐르게 만들지 않는다 —
            // 그쪽이 timeScale 의 주인이고, 되돌리는 것도 그쪽이다.
            if (Time.timeScale <= 0f) return;

            Time.timeScale = speed;
            // 물리 보폭도 같이 키운다 — 안 그러면 8배속에서 FixedUpdate 가 8배 돌아 프레임이 급락한다.
            Time.fixedDeltaTime = _baseFixedDelta * speed;
        }

        void Paint()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                bool on = i == _index;
                if (_backgrounds[i] != null) _backgrounds[i].color = on ? activeColor : idleColor;
                if (_labels[i] != null) _labels[i].color = on ? activeTextColor : idleTextColor;
            }
        }

        /// <summary>배속 값 → 자식 오브젝트 이름. 정수면 "x2", 소수면 "x1.5".</summary>
        static string ButtonName(float speed) =>
            Mathf.Approximately(speed, Mathf.Round(speed))
                ? $"x{Mathf.RoundToInt(speed)}"
                : $"x{speed:0.##}";
    }
}
