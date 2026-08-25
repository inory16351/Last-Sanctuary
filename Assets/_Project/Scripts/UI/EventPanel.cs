using System.Collections;
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
    /// ├─ Bg                    배경 그림 (2026-08-25 신설 · 표의 event_bg 로 갈아 끼운다)
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
        [Header("등장 연출 (2026-08-24 유저 지시 — 페이드 인 · 떠오르기)")]
        [Tooltip("창이 밝아지는 시간(초). 0 이면 연출 없이 바로 뜬다")]
        [Min(0f)] [SerializeField] float appearSeconds = 0.28f;

        [Tooltip("창이 <b>아래에서</b> 제자리로 올라오는 높이(px). 0 이면 제자리에서 밝아지기만 한다")]
        [Min(0f)] [SerializeField] float appearRisePixels = 36f;

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

        // ══════════════════════════════════════════════════════════════
        //  ★★★ 배경 그림 (2026-08-25 신설 — 유저: *"이미지 넣어서 줄테니까 연동해"*)
        // ══════════════════════════════════════════════════════════════
        // 표의 <c>event_bg</c> 가 <b>파일 이름</b>이다(`bg_fog` · `bg_aftermath` …).
        // 그림을 <c>Resources/EventBg/</c> 에 <b>키와 똑같은 이름</b>으로 넣으면 저절로 붙는다.
        //
        // ⚠ <b>스프라이트 참조는 인스펙터에 못 넣는다</b>(MCP 로는 오브젝트 참조를 못 쓴다 ·
        //   진행상황 8절 4번). 그래서 <b>코드가 이름으로 읽어</b> 꽂는다 —
        //   발굴 표식(<c>RelicDigService</c>)·로비 그림이 쓰는 것과 같은 방식이다.
        // ★ <b>그림이 없으면 조용히 배경 없이</b> 뜬다. 그림 하나 때문에 사건이 막히면 안 된다.

        [Header("배경 그림")]
        [Tooltip("Resources 아래 폴더. 표의 event_bg 가 여기서 <b>파일 이름</b>이 된다")]
        [SerializeField] string bgFolder = "EventBg";

        [Tooltip("배경을 그릴 Image 의 하이라키 이름. 없으면 배경 없이 뜬다")]
        [SerializeField] string bgPath = "Bg";

        [Tooltip("배경에 곱하는 색. ★ <b>알파를 낮춰 어둡게</b> 깔아야 위에 얹힌 글이 읽힌다 — " +
                 "그림이 아무리 좋아도 글을 못 읽으면 못 쓴다")]
        [SerializeField] Color bgTint = new Color(1f, 1f, 1f, 0.55f);

        Image _bg;

        /// <summary>한 번 읽은 그림은 들고 있는다 — 사건마다 <c>Resources.Load</c> 를 다시 돌지 않게.</summary>
        readonly System.Collections.Generic.Dictionary<string, Sprite> _bgCache =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        TMP_Text _title, _body, _choice0Label, _choice1Label, _closeLabel;
        Button _choice0, _choice1, _close;

        EventService _service;
        bool _bound;

        /// <summary>등장 연출용. 없으면 <see cref="EnsureIntro"/> 가 붙인다.</summary>
        CanvasGroup _group;
        RectTransform _rect;

        /// <summary>
        /// 창이 <b>도착해야 할 자리</b>. 씬에 잡아둔 값이다 —
        /// 연출이 여기서 출발해 여기로 돌아오므로 <b>한 번만</b> 기억해야 한다.
        /// ⚠ 매번 «지금 자리» 를 읽으면 연출이 끊겼을 때 창이 아래에 눌러앉는다.
        /// </summary>
        Vector2 _home;
        bool _homeKnown;

        /// <summary>지금 도는 등장 연출. 새 단계가 오면 갈아탄다(둘이 겹치면 알파가 싸운다).</summary>
        Coroutine _intro;

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

            // ★★ 배경은 <b>맨 뒤로</b> 보낸다. 씬에서 자식 순서가 어떻든 여기서 한 번
            //   못박으므로, 나중에 칸을 더해도 배경이 글을 덮는 사고가 나지 않는다.
            //   (UI 는 «형제 순서 = 그리는 순서» 다 — 먼저 그린 것이 아래 깔린다.)
            _bg = string.IsNullOrWhiteSpace(bgPath)
                ? null
                : transform.Find(bgPath)?.GetComponent<Image>();
            if (_bg != null)
            {
                _bg.transform.SetAsFirstSibling();
                _bg.raycastTarget = false;   // 배경이 선택지 클릭을 먹지 않게
            }

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

        /// <summary>
        /// ★★★ 표의 <c>event_bg</c> 로 배경 그림을 갈아 끼운다 (2026-08-25).
        ///
        /// <code>
        ///   event_bg = "bg_fog"  →  Resources/EventBg/bg_fog  →  Bg 이미지에 꽂는다
        ///   그림이 없다          →  Bg 를 <b>끈다</b>(창은 그대로 뜬다)
        /// </code>
        ///
        /// ⚠ <b>없는 그림을 «없다» 고 한 번만 알린다.</b> 사건이 뜰 때마다 경고를 쏟으면
        ///   콘솔이 도배돼 진짜 문제를 못 본다 — 캐시에 <c>null</c> 을 넣어 두 번째부터는 조용하다.
        /// ⚠ <b>textureType 이 Sprite(8)여야 읽힌다.</b> Default 로 들어오면
        ///   <see cref="Resources.Load{T}"/> 가 <b>조용히 null</b> 을 돌려준다(84-8절 ②의 함정) —
        ///   그림이 있는데 안 나오면 그 파일의 임포트 설정부터 볼 것.
        /// </summary>
        void ApplyBackground(EventDefinitionSO def)
        {
            if (_bg == null) return;

            Sprite sprite = LoadBg(def != null ? def.eventBg : null);

            if (sprite == null)
            {
                if (_bg.gameObject.activeSelf) _bg.gameObject.SetActive(false);
                return;
            }

            if (!_bg.gameObject.activeSelf) _bg.gameObject.SetActive(true);
            _bg.sprite = sprite;
            _bg.color = bgTint;
        }

        /// <summary>이름으로 배경 그림을 읽는다. 못 찾으면 <c>null</c>(캐시에도 그렇게 남긴다).</summary>
        Sprite LoadBg(string key)
        {
            key = (key ?? "").Trim();
            if (key.Length == 0) return null;

            if (_bgCache.TryGetValue(key, out Sprite cached)) return cached;

            Sprite found = Resources.Load<Sprite>($"{bgFolder}/{key}");
            _bgCache[key] = found;

            if (found == null)
                Debug.Log($"[사건] 배경 그림이 없습니다: Resources/{bgFolder}/{key} — " +
                          "배경 없이 띄웁니다.", this);

            return found;
        }

        // ------------------------------------------------------------------

        void HandleChanged(EventDefinitionSO def, EventChoice choice)
        {
            if (def == null)
            {
                gameObject.SetActive(false);
                return;
            }

            // ★★ 2026-08-24 — <b>«처음 열릴 때» 만</b> 등장 연출을 태운다(유저 지시:
            //   *"이벤트 등장 시 ui 등장에 페이드 인 / 떠오르기 효과 추가"*).
            //   ⚠ 본문 → 결과 단계는 <b>같은 창의 내용이 바뀌는 것</b>이라 여기서 또 태우면
            //     선택지를 누를 때마다 창이 아래로 툭 떨어졌다 올라온다.
            bool opening = !gameObject.activeSelf;

            // ★ 다른 창과 <b>배타</b>다 — 전술·성장 창처럼 겹쳐 뜨면 클릭이 섞인다(UI-23).
            HudExclusive.OpenOnly(this);
            gameObject.SetActive(true);
            Refresh(def, choice);

            if (opening) PlayAppear();
        }

        // ------------------------------------------------------------------
        // 등장 연출 — 페이드 인 + 떠오르기 (2026-08-24)
        // ------------------------------------------------------------------

        /// <summary>
        /// <see cref="CanvasGroup"/> 과 «제자리» 를 보장한다.
        ///
        /// ★ <b>코드가 붙인다</b> — 씬(MCP 로 만든 <c>HUD_Event</c>)에 컴포넌트가 없어도
        ///   동작해야 한다. 로비의 <c>EnsureGroup</c> 과 같은 이유·같은 방식이다.
        /// </summary>
        void EnsureIntro()
        {
            if (_group == null && !TryGetComponent(out _group))
                _group = gameObject.AddComponent<CanvasGroup>();

            _rect ??= transform as RectTransform;

            if (!_homeKnown && _rect != null)
            {
                _home = _rect.anchoredPosition;
                _homeKnown = true;
            }
        }

        void PlayAppear()
        {
            EnsureIntro();
            if (_group == null) return;

            if (_intro != null) StopCoroutine(_intro);
            _intro = StartCoroutine(Appear());
        }

        /// <summary>
        /// 아래에서 제자리로 올라오며 밝아진다. 끝에서 감속한다(<c>1 - (1-t)²</c>) —
        /// 등속으로 멈추면 툭 서는 느낌이 난다(로비 <c>RiseIn</c> 과 같은 곡선).
        ///
        /// ⚠ <see cref="Time.unscaledDeltaTime"/> 을 쓴다 — 일시정지(<c>timeScale = 0</c>)
        ///   중에 창이 뜨면 <c>deltaTime</c> 이 0 이라 연출이 <b>영영 멈춘다</b>.
        /// </summary>
        IEnumerator Appear()
        {
            if (appearSeconds <= 0f)
            {
                Settle();
                yield break;
            }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / appearSeconds;
                float e = Mathf.Clamp01(t);
                float eased = 1f - (1f - e) * (1f - e);

                _group.alpha = eased;
                if (_rect != null)
                    _rect.anchoredPosition =
                        new Vector2(_home.x, _home.y - appearRisePixels * (1f - eased));

                yield return null;
            }

            Settle();
        }

        /// <summary>연출의 마지막 프레임 — 창을 제자리에 완전히 세운다.</summary>
        void Settle()
        {
            _intro = null;
            if (_group != null) _group.alpha = 1f;
            if (_rect != null && _homeKnown) _rect.anchoredPosition = _home;
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
                _title.text = string.IsNullOrWhiteSpace(def.DisplayName) ? fallbackTitle : def.DisplayName;

            ApplyBackground(def);

            bool resultStage = choice != null;

            // ── 본문 ──
            if (_body != null)
            {
                string text = resultStage
                    ? Join(choice.ResultScript, choice.ResultEffect)
                    : def.Script;
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

            label.text = string.IsNullOrWhiteSpace(choice.ChoiceText) ? fallback : choice.ChoiceText;
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

            // ⚠ 연출 중에 닫히면 알파가 0.3, 자리가 아래인 채로 굳는다 — 다음에
            //   연출 없이 열리는 경로(appearSeconds = 0)가 그 상태를 그대로 물려받는다.
            //   닫을 때 항상 제자리로 세워 둔다.
            if (_intro != null) { StopCoroutine(_intro); _intro = null; }
            Settle();

            gameObject.SetActive(false);

            _service ??= EventService.Instance;
            _service?.CloseCurrent("창 닫기");
        }
    }
}
