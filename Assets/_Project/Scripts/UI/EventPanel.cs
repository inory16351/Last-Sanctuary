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

        // ══════════════════════════════════════════════════════════════
        //  ★★★ <b>선택지를 고르기 전에는 아무것도 못 한다</b> (2026-08-31 신설)
        // ══════════════════════════════════════════════════════════════
        // 유저 리포트: *"이벤트 등장 시 이벤트 선택지 선택 전엔 다른 ui로 넘어가지거나 게임이
        //   진행되면 안되는데 <b>그냥 이벤트 창을 끄고 넘어갈 수 있는 현상</b> 발생.
        //   이벤트 등장 시 <b>반드시 이벤트 선택지부터 선택</b>하도록 수정"*
        //
        // <b>무엇이 뚫려 있었나 — 구멍이 셋이었다</b>:
        // <code>
        //   ① 본문 단계에도 「닫기」 버튼이 <b>보였다</b>      → 누르면 선택 없이 사건이 끝났다
        //   ② Esc · 단축키 · 다른 창 버튼이 <b>다 살아 있었다</b> → HudExclusive 가 이 창을 닫았다
        //   ③ 게임이 <b>계속 흘렀다</b>                        → 읽는 동안 웨이브가 밀려왔다
        // </code>
        //
        // ★★ <b>고치는 방법을 «막을 곳마다» 두지 않았다.</b> 창을 열 수 있는 곳은
        //   허드 액션 버튼 · 단축키 · 도움말 안내 · 발굴 표식 … 으로 계속 늘어난다. 하나씩
        //   막으면 <b>새로 생기는 입구를 반드시 빠뜨린다</b>(HudExclusive 가 N² 를 피하려고
        //   만들어진 것과 같은 이유). 그래서 두 가지 <b>포괄적인</b> 장치만 쓴다:
        //
        //   ⑴ <b>화면 전체를 덮는 투명 판</b>(<see cref="EnsureBlocker"/>) — 마우스로 누를 수
        //      있는 것이 <b>이 창밖에 없다</b>. 허드 버튼·미니맵·맵 클릭·유닛 선택이 한 번에 막힌다.
        //   ⑵ <b>시간을 멈춘다</b>(<see cref="ApplyModalLock"/>) — «게임이 진행되면 안 된다» 가
        //      곧 <c>timeScale = 0</c> 이다. 이 프로젝트에는 이미 그 손잡이가 하나뿐이다.
        //
        //   키보드 단축키만 판으로 막을 수 없어서 <c>HudHotkeys</c> 한 곳에 문을 달았다.
        //
        // ⚠ <b>잠기는 것은 «본문 단계» 뿐이다</b> — 결과창은 답을 이미 받았으므로 예전처럼
        //   닫아도 되고 다른 창으로 넘어가도 된다(<c>EventService.AwaitingChoice</c>).

        [Header("선택 강제 (2026-08-31)")]
        [Tooltip("선택지를 고르기 전에는 <b>화면 전체를 덮는 투명 판</b>으로 다른 클릭을 막는다. " +
                 "끄면 예전처럼 다른 UI 를 누를 수 있다")]
        [SerializeField] bool blockClicksUntilChosen = true;

        [Tooltip("선택지를 고르기 전에는 <b>게임 시간을 멈춘다</b>(timeScale = 0). " +
                 "⚠ 남이 멈춰둔 상태(패배·승리 화면)는 건드리지 않는다 — 아래 ApplyModalLock 참조")]
        [SerializeField] bool pauseUntilChosen = true;

        [Tooltip("덮는 판의 색. 알파를 0 으로 두면 «보이지 않게» 막고, 조금 올리면 뒤가 어두워져 " +
                 "«지금은 이 창만 만질 수 있다» 가 눈에 보인다")]
        [SerializeField] Color blockerColor = new Color(0f, 0f, 0f, 0.45f);

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

        [Tooltip("배경을 그릴 Image 의 하이라키 경로. 없으면 배경 없이 뜬다.\n" +
                 "★ 부모(BgMask)가 RectMask2D 로 넘치는 부분을 잘라낸다 — 아래 ★★★ 참조")]
        [SerializeField] string bgPath = "BgMask/Bg";

        [Tooltip("배경에 곱하는 색. ★ <b>알파를 낮춰 어둡게</b> 깔아야 위에 얹힌 글이 읽힌다 — " +
                 "그림이 아무리 좋아도 글을 못 읽으면 못 쓴다")]
        [SerializeField] Color bgTint = new Color(1f, 1f, 1f, 0.55f);

        /// <summary>
        /// ★★★ <b>«늘리지 않고 채워 자른다»(aspect-fill)</b> — 어느 칸을 남길지 (2026-08-25).
        ///
        /// <b>왜 이렇게 하는가</b> — 창은 가로가 긴데(560x340 · 1.65) 생성 AI 가 뽑아 준 그림은
        /// 비율이 제각각이다. 실제로 시트 A 는 1.51, 시트 B 는 <b>1.20</b> 이었다.
        /// 창에 <b>늘려서</b> 채우면 시트 B 는 <b>가로로 37% 늘어나</b> 수정 기둥이 뭉툭해지고
        /// 아치가 넓적한 타원이 된다.
        ///
        /// → 비율을 지킨 채 <b>창을 덮고 넘치는 부분만 잘라낸다</b>. 왜곡이 <b>0</b> 이 된다.
        /// ★★ <b>이것이 이번 그림만의 해결이 아니다</b> — 앞으로 어떤 비율로 그림이 와도
        ///   깨지지 않는다. 시트 B 만 16:9 로 다시 뽑으면 «이번 것» 만 해결된다.
        ///
        /// ⚠ 대신 <b>위아래가 잘린다</b>(시트 B 는 세로의 27%). 무엇이 잘리느냐가 그림마다
        ///   다르므로 — 수정 기둥은 <b>위</b>가 중요하고 바닥 무늬는 <b>아래</b>가 중요하다 —
        ///   <b>남길 자리를 그림마다 정할 수 있게</b> 열어 둔다.
        /// </summary>
        [System.Serializable]
        public class BgFocus
        {
            [Tooltip("표의 event_bg 키. 예: bg_nexus")]
            public string key = "";

            [Tooltip("세로로 어디를 남길지. 0 = 위쪽을 남긴다 · 0.5 = 가운데 · 1 = 아래쪽")]
            [Range(0f, 1f)] public float focusY = 0.5f;
        }

        [Tooltip("잘라낼 때 세로로 어디를 남길지 (0 위 · 0.5 가운데 · 1 아래). " +
                 "아래 목록에 없는 그림은 이 값을 쓴다")]
        [Range(0f, 1f)] [SerializeField] float bgFocusY = 0.5f;

        [Tooltip("그림마다 다르게 잘라야 할 때만 적는다. 비어 있으면 위의 기본값을 쓴다")]
        [SerializeField] BgFocus[] bgFocus = new BgFocus[0];

        Image _bg;

        /// <summary>배경을 잘라내는 틀(<c>RectMask2D</c>). <see cref="_bg"/> 의 부모다.</summary>
        RectTransform _bgMask;

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

        /// <summary>화면 전체를 덮는 투명 판. <see cref="EnsureBlocker"/> 가 코드로 만든다.</summary>
        RectTransform _blocker;

        /// <summary>
        /// ★★ <b>내가 멈춰둔 상태인가 — 소유권 증표다</b>(<c>GameSpeedPanel._paused</c> 와 같은 판단).
        /// 패배·승리 화면도 <c>timeScale = 0</c> 을 쓰므로 «지금 0 이다» 만으로는 누가 멈춴
        /// 것인지 알 수 없다. 이 칸이 true 일 때만 이 창이 0 을 풀 수 있다.
        /// </summary>
        bool _ownsPause;

        /// <summary>멈추기 <b>직전</b>의 배속. 풀 때 그 값으로 되돌린다(1 로 덮으면 배속이 날아간다).</summary>
        float _resumeTimeScale = 1f;

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
        void Awake()
        {
            Bind();
            LocalizeLabels();
            // ★★★ 2026-08-27 — 언어가 바뀌면 문구를 다시 받아 온다(다음 사건 창부터 따라온다).
            //   ★ 떠 있는 창을 다시 칠하지는 않는다 — 이 창은 <see cref="Present"/> 로만
            //     그려지고 그 인자(사건·선택지)를 들고 있지 않아, 다시 칠하려면 상태를
            //     한 벌 더 들어야 한다. 사건 창은 뜬 채로 오래 두는 창이 아니다.
            Data.StringTable.OnLanguageChanged -= LocalizeLabels;
            Data.StringTable.OnLanguageChanged += LocalizeLabels;
        }

        void OnDestroy()
        {
            // ⚠ 정적 이벤트라 끊지 않으면 죽은 오브젝트가 구독에 남는다(SettingsPanel 의 그 ⚠).
            Data.StringTable.OnLanguageChanged -= LocalizeLabels;

            // ⚠⚠ <b>멈춰둔 시간을 반드시 되돌린다.</b> 씬을 다시 열거나(게임 재시작·로비 복귀)
            //   플레이 모드를 나갈 때 이 창이 파괴되는데, 그때 timeScale 이 0 으로 남으면
            //   <b>다음 판이 멈춘 채로 시작한다</b> — GameSpeedPanel.OnDisable 이 이미 밟은 함정이다.
            ReleasePause();
        }

        /// <summary>
        /// 창이 <b>어떤 경로로든</b> 꺼지면 잠금을 푼다 — <see cref="Close"/> 를 거치지 않는
        /// 경로(<c>SetActive(false)</c> 를 직접 부르는 코드 · 씬 언로드)까지 이 한 곳에서 받는다.
        /// ★ «푸는 곳을 하나로» 두는 것이 timeScale 을 다루는 유일한 안전한 방법이다.
        /// </summary>
        void OnDisable() => ReleasePause();

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
            _bgMask = _bg != null ? _bg.transform.parent as RectTransform : null;

            if (_bg != null)
            {
                // ★★ <b>맨 뒤로 보내는 것은 «틀» 이다</b> — 이미지가 아니라.
                //   이미지는 틀 안에서 넘치도록 커져 있으므로, 이미지를 옮기면 틀 밖으로 나간다.
                if (_bgMask != null) _bgMask.SetAsFirstSibling();
                else _bg.transform.SetAsFirstSibling();

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

            FitCover(sprite, FocusOf(def != null ? def.eventBg : null));
        }

        /// <summary>
        /// ★★★ <b>창을 «덮도록» 크기를 잡는다</b> — 늘리지 않고, 넘치는 쪽만 틀이 잘라낸다.
        ///
        /// <code>
        ///   그림이 창보다 <b>납작하면</b> → 가로를 창에 맞추고 <b>세로가 넘친다</b>(위아래가 잘린다)
        ///   그림이 창보다 <b>길쭉하면</b> → 세로를 창에 맞추고 <b>가로가 넘친다</b>(좌우가 잘린다)
        /// </code>
        ///
        /// ⚠ <b>「늘려 채우기」와 결과가 정반대다.</b> 늘리면 모양이 일그러지고, 이쪽은
        ///   모양이 그대로인 대신 <b>바깥이 없어진다</b>. 배경에는 뒤쪽이 맞다 —
        ///   글 뒤에 깔리는 그림에서 «가장자리 조금» 보다 «형태가 뭉개짐» 이 훨씬 눈에 띈다.
        ///
        /// ★ <paramref name="focusY"/> 로 <b>세로 어디를 남길지</b> 고른다
        ///   (0 위 · 0.5 가운데 · 1 아래). 수정 기둥처럼 <b>위가 중요한</b> 그림은 0 쪽으로.
        /// ⚠ 틀 크기를 매번 다시 읽는다 — 창은 <see cref="UiWindowDrag"/> 로 끌어 옮길 수 있고,
        ///   레이아웃이 도는 시점이 정해져 있지 않다.
        /// </summary>
        void FitCover(Sprite sprite, float focusY)
        {
            if (_bg == null || sprite == null) return;

            var rt = _bg.rectTransform;

            // 틀이 없으면 예전처럼 «늘려 채우기» 로 둔다(배선이 덜 된 씬에서도 뜨게).
            if (_bgMask == null) return;

            Rect frame = _bgMask.rect;
            if (frame.width <= 1f || frame.height <= 1f) return;

            Rect sr = sprite.rect;
            if (sr.width <= 0f || sr.height <= 0f) return;

            float want = sr.width / sr.height;          // 그림 비율
            float have = frame.width / frame.height;    // 창 비율

            float w, h;
            if (want > have) { h = frame.height; w = h * want; }   // 길쭉하다 → 좌우가 넘친다
            else             { w = frame.width;  h = w / want; }   // 납작하다 → 위아래가 넘친다

            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);

            // 남길 자리 — 넘치는 만큼만 밀 수 있다. focusY 0 이면 위쪽이 남는다.
            float overflowY = Mathf.Max(0f, h - frame.height);
            rt.anchoredPosition = new Vector2(0f, overflowY * (focusY - 0.5f));
        }

        /// <summary>이 그림을 자를 때 세로로 어디를 남길지. 목록에 없으면 기본값.</summary>
        float FocusOf(string key)
        {
            key = (key ?? "").Trim();
            if (key.Length > 0 && bgFocus != null)
            {
                for (int i = 0; i < bgFocus.Length; i++)
                    if (bgFocus[i] != null && bgFocus[i].key == key)
                        return bgFocus[i].focusY;
            }
            return bgFocusY;
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

            // ★★★ 본문 단계면 잠근다 · 결과 단계면 푼다 (2026-08-31 · 위 ★★★ 참조).
            //   <b>Refresh 뒤에</b> 부른다 — 판을 올릴 때 창의 내용이 이미 그려져 있어야
            //   «막혔는데 뭘 골라야 하는지 안 보이는» 한 프레임이 생기지 않는다.
            //   ⚠⚠ <b>고를 것이 없으면 잠그지 않는다</b> — 조건이 <c>EventService.AwaitingChoice</c>
            //     와 <b>글자 그대로 같아야 한다</b>. 갈리면 «판은 올라갔는데 Close 는 허용» 같은
            //     엇갈린 상태가 생긴다(그쪽의 ⚠⚠ 참조 — 탈출구 없는 정지).
            ApplyModalLock(choice == null && HasAnyChoice(def));

            if (opening) PlayAppear();
        }

        // ------------------------------------------------------------------
        //  선택 강제 — 덮는 판 · 시간 멈춤
        // ------------------------------------------------------------------

        /// <summary>
        /// 잠금을 켜거나 끈다. <paramref name="on"/> 은 «아직 안 골랐다» 다.
        ///
        /// ★★ <b>시간을 멈추는 규칙은 <c>GameSpeedPanel</c> 과 똑같이 «소유권» 으로 다룬다</b> —
        ///   이 프로젝트에서 <c>timeScale = 0</c> 을 쓰는 주인이 이미 셋이다
        ///   (일시정지 버튼 · 패배 화면 · 승리 화면). 넷째가 끼어들면서 남이 멈춰둔 것을
        ///   풀어버리면 <b>끝난 게임이 다시 흐른다</b>.
        ///   · 이미 0 이면 → <b>내가 멈춘 것이 아니다.</b> 손대지 않는다(잠글 필요도 없다).
        ///   · 내가 멈췄으면 → 풀 때 <b>멈추기 직전의 배속</b>으로 되돌린다(1 로 덮으면 x4 가 날아간다).
        ///
        /// ⚠ <c>fixedDeltaTime</c> 은 건드리지 않는다 — 0 을 곱하면 유니티가 예외를 던지고,
        ///   <c>timeScale</c> 이 0 이면 <c>FixedUpdate</c> 자체가 안 돈다(GameSpeedPanel 의 그 ⚠).
        /// </summary>
        void ApplyModalLock(bool on)
        {
            // ── 덮는 판 ──
            if (blockClicksUntilChosen)
            {
                EnsureBlocker();
                if (_blocker != null && _blocker.gameObject.activeSelf != on)
                    _blocker.gameObject.SetActive(on);
            }
            else if (_blocker != null && _blocker.gameObject.activeSelf)
            {
                _blocker.gameObject.SetActive(false);
            }

            // ── 시간 ──
            if (!on) { ReleasePause(); return; }
            if (!pauseUntilChosen || _ownsPause) return;
            if (Time.timeScale <= 0f) return;      // 남이 멈춰둔 상태 — 관여하지 않는다

            _resumeTimeScale = Time.timeScale;
            _ownsPause = true;
            Time.timeScale = 0f;
        }

        /// <summary>내가 멈춰둔 시간을 되돌린다. 내 것이 아니면 아무것도 하지 않는다.</summary>
        void ReleasePause()
        {
            if (!_ownsPause) return;
            _ownsPause = false;

            // ⚠ <b>지금도 0 일 때만</b> 되돌린다 — 그 사이에 누군가(패배 화면) 시간을 다시
            //   흐르게 했다면 소유권을 잃은 것이므로 그쪽 값을 덮어쓰지 않는다.
            if (Time.timeScale <= 0f)
                Time.timeScale = _resumeTimeScale > 0f ? _resumeTimeScale : 1f;
        }

        /// <summary>
        /// 화면 전체를 덮는 <b>투명 판</b>을 보장한다.
        ///
        /// ★★ <b>씬에 실물로 있다</b>(<c>HUD_Event/ModalBlocker</c> · MCP 로 만들었다 · §10 H-1) —
        ///   유저 지시 *"템플릿 슬롯 복제 하는 경우를 제외하고는 하드 코딩을 하지말고 mcp 연결해서
        ///   직접 생성 및 수정"* 그대로다. 그래서 <b>먼저 찾고</b>, 없을 때만 만든다.
        /// ⚠ 만드는 갈래를 남겨 두는 이유 — 씬이 아직 갱신되지 않은 상태(다른 브랜치·옛 씬)에서도
        ///   <b>선택 강제가 조용히 풀리면 안 된다</b>. 이 창의 <c>CanvasGroup</c> 이
        ///   같은 이유로 같은 모양을 하고 있다(<see cref="EnsureIntro"/> 의 ★).
        ///
        /// ★★ <b>왜 «이 창의 자식» 인가</b> — UI 의 그리는 순서는 <b>형제 순서</b>이고,
        ///   <see cref="HudExclusive.OpenOnly"/> 가 이 창을 <c>UI_Root</c> 의 <b>맨 뒤</b>로
        ///   올린다(=맨 위에 그린다). 그래서 이 창의 자식은 <b>다른 모든 HUD 위</b>에 온다.
        ///   판을 <c>UI_Root</c> 직속으로 만들면 «판과 창 중 누가 위인가» 를 따로 관리해야 하고,
        ///   창이 맨 앞으로 올라갈 때마다 판을 그 바로 아래로 다시 끼워 넣어야 한다.
        ///
        /// ★ <b>자식 중에서는 맨 앞</b>(<c>SetAsFirstSibling</c>)에 둔다 — 창의 배경·글씨·버튼보다
        ///   <b>먼저</b> 그려져야 <b>이 창의 선택지는 눌린다</b>. 판이 뒤에 있으면 창 자신도 막힌다.
        ///
        /// ⚠ 크기를 «부모에 스트레치 + 넉넉한 음수 오프셋» 으로 잡는다. 창은 760x420 인데
        ///   화면은 그보다 크므로, 부모에 딱 맞추면 <b>창 바깥이 안 막힌다</b>.
        ///   해상도를 읽어 맞추는 대신 <b>충분히 큰 값</b>을 쓴다 — 창을 드래그로 옮겨도
        ///   (<c>UiWindowDrag</c>) 화면을 계속 덮는다.
        /// </summary>
        void EnsureBlocker()
        {
            if (_blocker != null) return;

            // ★ 씬에 있는 것을 먼저 쓴다 (MCP 로 만든 HUD_Event/ModalBlocker).
            _blocker = transform.Find(BlockerName) as RectTransform;
            if (_blocker != null)
            {
                // 씬 값이 정본이다 — 크기·색을 코드가 다시 칠하지 않는다. 순서만 못박는다:
                // 이 창의 배경·글씨·버튼보다 <b>먼저</b> 그려져야 선택지가 눌린다(위 ★).
                _blocker.SetAsFirstSibling();

                Image scene = _blocker.GetComponent<Image>();
                if (scene != null) scene.raycastTarget = true;   // 이것만은 반드시 켜져 있어야 한다

                _blocker.gameObject.SetActive(false);
                return;
            }

            var go = new GameObject(BlockerName, typeof(RectTransform), typeof(Image));
            go.layer = gameObject.layer;

            _blocker = (RectTransform)go.transform;
            _blocker.SetParent(transform, false);

            _blocker.anchorMin = Vector2.zero;
            _blocker.anchorMax = Vector2.one;
            _blocker.pivot = new Vector2(0.5f, 0.5f);
            _blocker.offsetMin = new Vector2(-BlockerPadding, -BlockerPadding);
            _blocker.offsetMax = new Vector2(BlockerPadding, BlockerPadding);

            var image = go.GetComponent<Image>();
            image.color = blockerColor;
            image.raycastTarget = true;          // ★ 이것이 «막는다» 의 전부다

            _blocker.SetAsFirstSibling();
            go.SetActive(false);
        }

        /// <summary>
        /// 덮는 판이 창 밖으로 뻗어 나가는 여유(px). 창(760x420)과 이 값의 두 배를 더한 크기가
        /// <b>어떤 해상도보다 커야</b> 한다 — 4K(3840x2160)를 기준 해상도로 환산해도 남는다.
        /// ⚠ 이 값을 «화면 크기» 로 읽어 계산하지 않는 이유: 창은 드래그로 옮길 수 있어서
        ///   «지금 화면» 에 맞춰도 옮기는 순간 한쪽이 뚫린다. 넉넉히 크면 그 걱정이 없다.
        /// </summary>
        const float BlockerPadding = 4000f;

        /// <summary>덮는 판의 하이라키 이름. 씬(<c>HUD_Event/ModalBlocker</c>)과 짝을 맞춘다.</summary>
        const string BlockerName = "ModalBlocker";

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

            // ★★★ 2026-08-31 — <b>본문 단계에는 닫는 버튼이 없다</b> (유저 지시:
            //   *"반드시 이벤트 선택지부터 선택하도록"*).
            //
            //   ⚠⚠ 예전에는 여기서 라벨만 «닫기» 로 바꿔 <b>버튼을 남겨 두었다</b> —
            //     주석은 «고르지 않고 물러난다 를 분명히 한다» 였지만, 그 «물러남» 이 곧
            //     유저가 리포트한 버그다. 사건은 <b>고르는 것이 규칙</b>이므로 «안 고르는 길» 이
            //     화면에 있으면 안 된다.
            //   ★ <b>라벨을 지우는 것으로는 부족하다</b> — 버튼 자체를 감춰야 한다. 남겨 두면
            //     빈 버튼이 <see cref="Close"/> 를 계속 부를 수 있다(그쪽도 막았지만, 화면에
            //     «눌러도 아무 일 없는 버튼» 이 남는 것은 그 자체로 고장으로 읽힌다).
            //   ⚠⚠ <b>고를 것이 없는 사건은 예외다</b> — 표에 선택지 0개짜리 줄이 생기면
            //     버튼도 없고 닫기도 없어 <b>답할 방법이 없는 창</b>이 남는다. 그때는 닫기를
            //     남겨 두는 것이 유일한 탈출구다(<c>EventService.AwaitingChoice</c> 의 ⚠⚠).
            bool canClose = resultStage || !HasAnyChoice(def);
            if (_close != null) _close.gameObject.SetActive(canClose);
            if (_closeLabel != null)
                _closeLabel.text = resultStage ? finishLabel : HudTheme.T("ui_btn_close", "닫기");
        }

        /// <summary>
        /// 이 사건에 <b>고를 수 있는 것이 하나라도 있는가</b>. 잠금과 닫기 버튼이 <b>같은 값</b>을
        /// 봐야 하므로 한 줄로 뽑았다 — <c>EventService.AwaitingChoice</c> 와 같은 조건이다.
        /// </summary>
        static bool HasAnyChoice(EventDefinitionSO def) => def != null && def.choices.Count > 0;

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
        /// ★★★ <b>2026-08-31 — 선택지를 안 골랐으면 닫히지 않는다.</b>
        ///   위 ⚠ 는 «<c>HudExclusive</c> 가 다른 창을 열면서 이것을 부를 수도 있다 — 그때도
        ///   이벤트를 끝내는 것이 맞다» 고 적어 두었는데, 유저 지시(*"반드시 이벤트 선택지부터
        ///   선택"*)로 <b>그 판단이 뒤집혔다</b>. 이제 답하기 전에는 «창이 사라지는» 일 자체가
        ///   없어야 하므로, 어디서 불려도 <b>거절</b>한다.
        ///
        ///   ★ <b>거절을 조용히 한다</b> — 이 함수는 배타 조정자·Esc·씬 정리가 부르는
        ///     <b>내부 통로</b>다. 여기서 로그를 남기면 창 하나 열 때마다 로그가 한 줄씩 쌓인다.
        ///     유저에게 «못 닫는다» 를 알리는 것은 <b>버튼을 감추는 쪽</b>이 한다(<see cref="Refresh"/>).
        ///   ⚠ 부르는 쪽이 «닫았는지» 를 알아야 하면 <see cref="IsOpen"/> 을 다시 보면 된다 —
        ///     <c>HudExclusive.CloseOpenPanel</c> 이 그렇게 한다.
        /// </summary>
        public void Close()
        {
            // ⚠ 결과창은 예전처럼 닫힌다 — 잠기는 것은 <b>본문 단계</b>뿐이다.
            if (Events.EventService.IsAwaitingChoice) return;

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
    
        /// <summary>
        /// ★ 이 창의 문구를 <b>스트링 표</b>에서 가져온다 (2026-08-26 · 178-5절).
        /// 인스펙터 값은 <b>폴백</b>이다 — 표에 키가 없으면 화면은 지금과 같다.
        /// </summary>
        void LocalizeLabels()
        {
            fallbackTitle = HudTheme.T("ui_event_title_fallback", fallbackTitle);
            fallbackBody = HudTheme.T("ui_event_body_fallback", fallbackBody);
            choice0Label = HudTheme.T("ui_event_accept", choice0Label);
            choice1Label = HudTheme.T("ui_event_decline", choice1Label);
            finishLabel = HudTheme.T("ui_btn_confirm", finishLabel);
        }
}
}
