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

        [Tooltip("선택지 1 라벨 — 표에 선택지 문구 칸이 없어 여기서 정한다")]
        [SerializeField] string choice0Label = "수락";

        [Tooltip("선택지 2 라벨")]
        [SerializeField] string choice1Label = "거절";

        [Tooltip("선택지가 없는 줄에서 «다음으로» 넘기는 버튼의 라벨")]
        [SerializeField] string continueLabel = "계속";

        [Tooltip("마지막 줄에서 창을 닫는 버튼의 라벨")]
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
        public void Present(EventDefinitionSO def, EventLine line)
        {
            _presenting = def != null && line != null;
            Bind();                     // Awake 보다 먼저 불릴 수 있다 — 여러 번 불려도 안전하다
            HandleChanged(def, line);
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

        void HandleChanged(EventDefinitionSO def, EventLine line)
        {
            if (def == null || line == null)
            {
                gameObject.SetActive(false);
                return;
            }

            // ★ 다른 창과 <b>배타</b>다 — 전술·성장 창처럼 겹쳐 뜨면 클릭이 섞인다(UI-23).
            HudExclusive.OpenOnly(this);
            gameObject.SetActive(true);
            Refresh(def, line);
        }

        void Refresh(EventDefinitionSO def, EventLine line)
        {
            if (_title != null)
                _title.text = string.IsNullOrWhiteSpace(def.eventName) ? fallbackTitle : def.eventName;

            if (_body != null)
                _body.text = string.IsNullOrWhiteSpace(line.dialogue) ? fallbackBody : line.dialogue;

            bool twoWay = line.IsChoice && line.nextDialogueId02 != 0;
            bool goesOn = line.nextDialogueId01 != 0 || line.nextDialogueId02 != 0;

            // 선택지 두 칸 — 표가 «choice_proceed» 라고 적은 줄에서만 둘이 된다.
            if (_choice0 != null) _choice0.gameObject.SetActive(goesOn);
            if (_choice1 != null) _choice1.gameObject.SetActive(twoWay);

            if (_choice0Label != null) _choice0Label.text = twoWay ? choice0Label : continueLabel;
            if (_choice1Label != null) _choice1Label.text = choice1Label;

            // ★ 마지막 줄에서는 <b>닫기만</b> 남는다 — 결과 문장을 읽을 시간을 준다.
            if (_closeLabel != null) _closeLabel.text = goesOn ? "닫기" : finishLabel;
        }

        void Answer(int choice)
        {
            _service ??= EventService.Instance;
            _service?.Advance(choice);
        }

        // IExclusiveHudPanel — 다른 창이 열릴 때 이 창이 닫히는 통로.
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>
        /// 창만 닫는다 — <b>이벤트를 끝내지 않는다.</b> 표의 지속 효과는
        /// «웨이브가 끝날 때까지» 이므로 창을 닫는 것으로 없어지면 안 된다.
        /// </summary>
        public void Close()
        {
            _presenting = false;
            gameObject.SetActive(false);
        }
    }
}
