using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Events;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★ <b>이벤트 창 (임시 UI)</b> — 유저 지시 2026-08-21: *"이벤트 테이블 적용 일단 임시
    /// ui 로 구현 / 빈 텍스트는 임시 텍스트로 채우기 (폰트 네오둥근모 사용)"*.
    ///
    /// <b>하이라키</b>: <c>UI_Root/HUD_Event</c> — MCP 로 직접 만든 오브젝트다(§10 H-1).
    /// <code>
    /// HUD_Event                (이 컴포넌트 · 평소에는 비활성)
    /// ├─ Title                 이벤트 이름
    /// ├─ Body                  지금 줄의 대사
    /// ├─ Choice0 &gt; Label     선택지 1 (수락 / 첫 보상)
    /// ├─ Choice1 &gt; Label     선택지 2 (취소 / 둘째 보상)
    /// └─ CloseButton &gt; Label 닫기
    /// </code>
    ///
    /// <b>왜 «임시» 인가</b> — 목업(HUD 목업 html)에 이벤트 패널이 없다. 배경 이미지
    /// (<c>event_bg</c>)·연출·타이핑 효과가 정해지지 않았으므로 <b>글자와 버튼만</b> 있는
    /// 창으로 둔다. 표의 흐름(대사 → 선택지 → 결과 → 종료)은 <b>전부 실제로 돈다</b> —
    /// 나중에 꾸미는 것은 이 스크립트를 안 고치고 씬 오브젝트만 바꾸면 된다.
    ///
    /// ⚠ 버튼 개수를 <b>둘로 고정</b>했다 — 표의 <c>Dialogue</c> 시트가
    ///   <c>next_dialogue_id_01</c>·<c>next_dialogue_id_02</c> 두 칸뿐이다. 셋째가 생기면
    ///   표에 칸이 먼저 생겨야 한다(없는 칸을 코드가 지어내지 않는다).
    /// </summary>
    public class EventPanel : MonoBehaviour, IExclusiveHudPanel
    {
        [Header("임시 문구 — 표에 값이 없을 때만 쓴다")]
        [Tooltip("이벤트 이름이 비어 있을 때")]
        [SerializeField] string fallbackTitle = "이름 없는 사건";

        [Tooltip("대사가 비어 있을 때 — 표의 빈 칸을 화면에서 알아볼 수 있게 한다")]
        [SerializeField] string fallbackBody = "(대사 준비 중)";

        [Tooltip("표의 choice_text 가 비어 있을 때 첫째 버튼에 넣을 문구")]
        [SerializeField] string choice0Label = "수락";

        [Tooltip("표의 choice_text 가 비어 있을 때 둘째 버튼에 넣을 문구")]
        [SerializeField] string choice1Label = "거절";

        [Tooltip("결과창에서 창을 닫는 버튼의 라벨")]
        [SerializeField] string finishLabel = "확인";

        TMP_Text _title, _body, _choice0Label, _choice1Label, _closeLabel;
        Button _choice0, _choice1, _close;

        EventService _service;
        bool _bound;

        /// <summary>
        /// 지금 <see cref="Present"/> 로 열리는 중인가. <see cref="Awake"/> 가 창을 다시
        /// 닫아버리는 것을 막는 데만 쓴다 (아래 ★★ 참조).
        /// </summary>
        bool _presenting;

        // ------------------------------------------------------------------

        /// <summary>
        /// ★★ <b>2026-08-21 — 이 창은 «스스로 구독» 할 수 없다.</b>
        ///
        /// <b>왜 이벤트가 떠도 창이 안 보였나</b> (유저 리포트: *"이벤트 지금 적용 되어도
        /// 시각적으로 확인이 불가"*) — 씬의 <c>HUD_Event</c> 는 <b>비활성</b>으로 저장돼 있다.
        /// 유니티는 비활성 오브젝트의 <c>Awake</c>·<c>OnEnable</c>·<c>Update</c> 를
        /// <b>한 번도 부르지 않는다</b>. 그래서 예전 구조(매 프레임 <c>Hook()</c> 으로
        /// <c>OnEventChanged</c> 를 구독)는 <b>영원히 실행되지 않았다</b> — 확률·표·대사·보상은
        /// 전부 정상으로 돌고 있었고(콘솔에 «비공개 주사위 … → 발생» 이 찍혔다),
        /// <b>화면에 나오는 통로 하나만</b> 죽어 있었다.
        ///
        /// → <b>서비스가 밀어 넣는다</b>(<see cref="EventService"/> 가
        ///   <see cref="Present"/> 를 부른다). 비활성 오브젝트도 <b>참조로는</b> 부를 수 있으므로
        ///   이 방향이면 «창이 꺼져 있어도 열 수 있다» 가 성립한다.
        ///
        /// ⚠ <b>Awake 에서 창을 닫지 않는다</b> — 처음 활성화되는 순간이 곧 «열릴 때» 이므로,
        ///   그때 <c>SetActive(false)</c> 를 하면 열자마자 닫힌다. 씬 값이 이미 비활성이라
        ///   닫아 둘 필요도 없고, 누가 켜 둔 채 저장했을 때만 <see cref="Start"/> 가 정리한다.
        /// </summary>
        void Awake() => Bind();

        void Start()
        {
            // 씬에 켜진 채로 저장된 경우만 닫는다 — 열려 있는 중이면 건드리지 않는다.
            if (!_presenting) gameObject.SetActive(false);
        }

        /// <summary>
        /// ★ 서비스가 부르는 <b>유일한 입구</b>. <paramref name="def"/> 가 null 이면 닫는다.
        /// 비활성 상태에서 불려도 동작한다 — 그것이 이 함수의 존재 이유다(위 ★★).
        /// </summary>
        public void Present(EventDefinitionSO def, EventChoice choice)
        {
            _presenting = def != null;
            Bind();                     // Awake 보다 먼저 불릴 수 있다 — 여러 번 불려도 안전하다
            HandleChanged(def, choice);
        }

        void Bind()
        {
            if (_bound) return;         // onClick 을 두 번 붙이면 선택지가 두 번 눌린다
            _bound = true;

            _title = Find("Title");
            _body = Find("Body");

            _choice0 = FindButton("Choice0");
            _choice1 = FindButton("Choice1");
            _close = FindButton("CloseButton");

            _choice0Label = Find("Choice0/Label");
            _choice1Label = Find("Choice1/Label");
            _closeLabel = Find("CloseButton/Label");

            if (_choice0 != null) _choice0.onClick.AddListener(() => Answer(0));
            if (_choice1 != null) _choice1.onClick.AddListener(() => Answer(1));
            if (_close != null) _close.onClick.AddListener(Close);
        }

        TMP_Text Find(string path) => transform.Find(path)?.GetComponent<TMP_Text>();
        Button FindButton(string path) => transform.Find(path)?.GetComponent<Button>();

        // ------------------------------------------------------------------

        void HandleChanged(EventDefinitionSO def, EventChoice choice)
        {
            if (def == null)
            {
                gameObject.SetActive(false);
                return;
            }

            // ★ 다른 창과 <b>배타</b>다 — 전술·성장 창처럼 겹쳐 뜨면 클릭이 섞인다(UI-23).
            HudExclusive.OpenOnly(this);
            gameObject.SetActive(true);
            Refresh(def, choice);
        }

        /// <summary>
        /// ★★ <b>한 창이 두 모습을 낸다</b> (Ver013).
        ///
        /// <code>
        ///   choice == null  →  «본문 단계» : event_script + 선택지 버튼 N개
        ///   choice != null  →  «결과 단계» : result_script + result_effect + 확인 버튼
        /// </code>
        ///
        /// ★ 창을 두 개 만들지 않은 이유 — 씬 오브젝트가 하나뿐이고(<c>HUD_Event</c>),
        ///   두 모습이 <b>같은 자리에 같은 크기로</b> 뜨는 것이 맞다. 표의 흐름도
        ///   «본문 → 고르기 → 결과 → 닫기» 로 한 줄이다.
        /// </summary>
        void Refresh(EventDefinitionSO def, EventChoice choice)
        {
            if (_title != null)
                _title.text = string.IsNullOrWhiteSpace(def.eventName) ? fallbackTitle : def.eventName;

            bool resultStage = choice != null;

            // ── 본문 ──
            if (_body != null)
            {
                string text = resultStage
                    ? Join(choice.resultScript, choice.resultEffect)
                    : def.eventScript;
                _body.text = string.IsNullOrWhiteSpace(text) ? fallbackBody : text;
            }

            // ── 선택지 버튼 : 결과 단계에서는 <b>둘 다 감춘다</b> ──
            var choices = def.OrderedChoices();
            SetChoice(_choice0, _choice0Label, resultStage ? null : At(choices, 0), choice0Label);
            SetChoice(_choice1, _choice1Label, resultStage ? null : At(choices, 1), choice1Label);

            // ★ 결과 단계에서만 «확인» — 본문 단계에서 닫기를 누르면 선택 없이 이벤트가
            //   날아가므로, 그때는 라벨을 «닫기» 로 두어 «고르지 않고 물러난다» 를 분명히 한다.
            if (_closeLabel != null) _closeLabel.text = resultStage ? finishLabel : "닫기";
        }

        static EventChoice At(System.Collections.Generic.List<EventChoice> list, int i) =>
            list != null && i < list.Count ? list[i] : null;

        /// <summary>버튼 한 칸을 표의 선택지에 맞춘다. <paramref name="choice"/> 가 null 이면 감춘다.</summary>
        void SetChoice(Button button, TMP_Text label, EventChoice choice, string fallback)
        {
            if (button != null) button.gameObject.SetActive(choice != null);
            if (label == null || choice == null) return;

            label.text = string.IsNullOrWhiteSpace(choice.choiceText) ? fallback : choice.choiceText;
        }

        /// <summary>결과 대사와 효과 요약을 한 칸에 넣는다 — 빈 칸은 건너뛴다.</summary>
        static string Join(string script, string effect)
        {
            bool a = !string.IsNullOrWhiteSpace(script);
            bool b = !string.IsNullOrWhiteSpace(effect);
            if (a && b) return script.Trim() + "\n\n" + effect.Trim();
            return a ? script.Trim() : (b ? effect.Trim() : "");
        }

        void Answer(int choice)
        {
            _service ??= EventService.Instance;
            _service?.Choose(choice);
        }

        // IExclusiveHudPanel — 다른 창이 열릴 때 이 창이 닫히는 통로.
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>
        /// ★★ <b>창을 닫으면 이벤트가 끝난다</b> (Ver013 · 표 «화면 흐름» 5번:
        /// *"결과창을 닫으면 이벤트가 종료됩니다"*).
        ///
        /// ⚠ <b>지속 효과는 사라지지 않는다</b> — 효과마다 «몇 초» 가 표에 적혀 있고
        ///   (<c>reward_duration</c>) 그 시간은 이벤트가 끝난 뒤에도 계속 흐른다
        ///   (<see cref="LastSanctuary.Events.EventService.CloseCurrent"/> 의 ⚠⚠).
        ///
        /// ★ <b>서비스에 알려야 한다</b> — 예전에는 창만 껐다. 그러면 서비스의
        ///   <c>Current</c> 가 남아 «이미 진행 중» 으로 보이고, <b>다음 이벤트가 영영 안 뜬다</b>.
        /// ⚠ <c>HudExclusive</c> 가 다른 창을 열면서 이것을 부를 수도 있다 — 그때도
        ///   이벤트를 끝내는 것이 맞다(창이 사라졌는데 이벤트가 살아 있으면 답할 방법이 없다).
        /// </summary>
        public void Close()
        {
            _presenting = false;
            gameObject.SetActive(false);

            _service ??= EventService.Instance;
            _service?.CloseCurrent("창 닫기");
        }
    }
}
