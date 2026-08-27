using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LastSanctuary.Help;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★★ <b>화면에서 짚어 주는 안내</b> — 빨간 테두리로 실제 UI 를 하나하나 가리킨다
    /// (2026-08-24 신설 · 유저 지시: *"자세히 보기에서 실제 ui로 연결하고 <b>빨간 테두리 선으로
    /// 하나하나 설명</b>해주는 기능 넣어주고"*).
    ///
    /// <b>왜 필요한가</b> — 「강화」가 무엇인지 글로 읽어도 <b>어디를 눌러야 하는지</b>는 모른다.
    /// 처음 하는 사람에게 필요한 것은 설명문이 아니라 <b>손가락</b>이다. 이 판이 그 손가락이다.
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  ★★★ <b>그 창을 «직접 띄워 놓고» 안을 짚는다</b> (2026-08-24 유저 지시)
    /// ══════════════════════════════════════════════════════════════════
    /// *"전술 지침을 누르면 전술 지침에 대한 간략한 설명을 해주는 ui가 나와야 하고 거기서
    /// 자세히 보기를 누르면 <b>실제 전술 지침 ui를 띄워놓고 각 영역에 대해</b> 빨간색 테두리로
    /// 설명해 주어야 함"*.
    ///
    /// <code>
    ///   ① 기능을 처음 쓰면        →  조언 카드가 «간략한 설명» 을 보여준다
    ///   ② 「자세히 보기」를 누르면 →  <b>그 창이 실제로 열리고</b> 안의 영역을 차례로 짚는다
    ///   ③ 안내가 끝나면          →  <b>내가 연 창이면</b> 닫는다(유저가 열어 뒀으면 그대로 둔다)
    /// </code>
    ///
    /// ★ 창을 여는 일은 <see cref="HudExclusive.TryOpen"/> 이 한다 — <c>SetActive</c> 로 켜면
    ///   각 창의 <c>SetOpen</c> 안에 있는 «목록 다시 그리기» 가 돌지 않아 <b>내용이 빈 창</b>이 뜬다.
    /// ★ 창이 없는 항목은 <b>늘 보이는 HUD 하나</b> 안에서만 짚는다(에너지 · 웨이브 · 배속).
    /// ⚠ <b>단계가 없는 항목은 「자세히 보기」 자체가 뜨지 않는다</b> — 「성역이 부서지면 패배」
    ///   처럼 <b>화면의 칸과 상관없는 규칙</b>은 짚을 데가 없다(유저 지시). 그 판단은
    ///   <see cref="HasTour"/> 한 곳에서 하고, 카드와 백과가 그것을 함께 쓴다.
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  ★★ 빨간 테두리를 <b>네 개의 막대</b>로 그린다
    /// ══════════════════════════════════════════════════════════════════
    /// 테두리 스프라이트(9-slice)를 쓰면 그림 파일이 필요하고, 그림이 없으면 <b>속이 꽉 찬
    /// 네모</b>가 되어 가리려는 것을 가려 버린다. 그래서 <b>얇은 막대 넷</b>(위·아래·왼·오른)을
    /// 대상 테두리에 맞춰 놓는다 — 그림이 필요 없고, 가운데가 <b>비어 있어 대상이 그대로 보인다</b>.
    ///
    /// ★ 테두리가 <b>천천히 밝아졌다 어두워진다</b> — 멈춘 화면에서 정지한 빨간 네모는
    ///   «UI 의 일부» 처럼 보인다. 깜빡이면 «지금 이것을 보라» 가 된다.
    ///   ⚠ <see cref="Time.unscaledDeltaTime"/> 을 쓴다 — 안내 중에는 게임이 멈춰 있다.
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  ★★ 말풍선은 <b>대상을 가리지 않는 쪽</b>으로 붙는다
    /// ══════════════════════════════════════════════════════════════════
    /// 대상 위에 여유가 있으면 위에, 없으면 아래에 놓는다. 좌우도 화면 안으로 밀어 넣는다.
    /// 가운데 고정으로 두면 «가리키는 것을 말풍선이 덮는» 일이 반드시 생긴다.
    ///
    /// ⚠ <b>어두운 막을 깔지 않는다.</b> 가리켜야 할 것이 화면의 UI 인데 화면을 어둡게 덮으면
    ///   가리키는 대상이 같이 어두워진다. 대신 <b>투명한 막</b>으로 클릭만 막는다 —
    ///   안내 중에 뒤의 버튼이 눌리면 화면이 바뀌어 짚던 자리가 사라진다.
    /// ⚠ <b>배타 창이 아니다</b>(<see cref="IExclusiveHudPanel"/>) — 창이 아니라 <b>덮는 한 겹</b>이다.
    /// </summary>
    public class HelpTourPanel : MonoBehaviour
    {
        static HelpTourPanel _instance;

        public static HelpTourPanel Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<HelpTourPanel>(FindObjectsInactive.Include);
                return _instance;
            }
        }

        [Header("테두리")]
        [Tooltip("빨간 테두리의 굵기(px)")]
        [Min(1f)] [SerializeField] float borderThickness = 4f;

        [Tooltip("대상 테두리에서 이만큼 <b>바깥쪽</b>으로 벌린다 — 딱 붙으면 대상의 테두리와 섞인다")]
        [Min(0f)] [SerializeField] float borderPadding = 4f;

        [Tooltip("깜빡임 한 주기(초). 0 이면 깜빡이지 않는다")]
        [Min(0f)] [SerializeField] float pulseSeconds = 1.1f;

        [Tooltip("깜빡일 때 가장 옅어지는 투명도")]
        [Range(0.1f, 1f)] [SerializeField] float pulseMinAlpha = 0.45f;

        [Header("말풍선")]
        [Tooltip("대상과 말풍선 사이의 간격(px)")]
        [Min(0f)] [SerializeField] float bubbleGap = 16f;

        [Tooltip("말풍선이 화면 가장자리에서 이만큼은 떨어져 있게 한다(px)")]
        [Min(0f)] [SerializeField] float screenMargin = 24f;

        [Header("문구")]
        [Tooltip("{0} = 지금 단계 · {1} = 전체 단계 수")]
        [SerializeField] string counterFormat = "{0} / {1}";

        [SerializeField] string nextLabel = "다음";
        [SerializeField] string prevLabel = "이전";
        [SerializeField] string lastLabel = "다 봤습니다";
        [SerializeField] string quitLabel = "그만 보기";

        [Tooltip("짚을 곳을 찾지 못했을 때 단계 글 뒤에 덧붙이는 한 줄. " +
                 "★ 조용히 넘기지 않는다 — 표의 경로가 틀린 것을 유저가 알아야 한다")]
        [SerializeField] string missingNote =
            // ⚠ 앞의 줄바꿈은 코드가 붙인다 — 스트링 표는 앞뒤 공백·줄바꿈을 다듬는다
            "<b>(이 칸은 지금 화면에 없습니다. 해당 창을 열면 보입니다.)</b>";

        readonly List<HelpStepRow> _steps = new List<HelpStepRow>();
        int _index;
        HelpEntry _entry;

        /// <summary>안내가 열어 둔 창. 없으면 null.</summary>
        Transform _window;

        /// <summary>
        /// <b>그 창을 내가 열었는가</b> — 소유권 증표다. 유저가 이미 열어 두었으면 false 이고,
        /// 그때는 안내가 끝나도 <b>닫지 않는다</b>(<see cref="ReadingPause"/> 와 같은 규칙).
        /// </summary>
        bool _windowOpenedByMe;

        /// <summary>
        /// ★ 창을 연 <b>다음 프레임</b>에 첫 단계를 그리게 하는 표시.
        /// ⚠ 창을 켠 그 프레임에는 <see cref="RectTransform"/> 의 레이아웃이 아직 돌지 않아
        ///   귀퉁이 좌표가 <b>엉뚱한 값</b>이다 — 그 값으로 테두리를 놓으면 화면 구석에 찍힌다.
        /// </summary>
        bool _waitLayout;

        RectTransform _self;
        RectTransform _frame, _bTop, _bBottom, _bLeft, _bRight;
        RectTransform _bubble;
        TMP_Text _title, _counter, _text, _nextText, _prevText, _quitText;
        Button _next, _prev, _quit;
        Image[] _bars;
        bool _bound;

        /// <summary>읽는 동안의 일시정지. 규칙은 <see cref="ReadingPause"/> 에 있다.</summary>
        readonly ReadingPause _pause = new ReadingPause();

        void Awake()
        {
            LocalizeLabels();
            // ★★★ 2026-08-27 — 언어가 바뀌면 문구를 다시 받아 오고, 떠 있는 단계도 다시 그린다.
            Data.StringTable.OnLanguageChanged -= HandleLanguageChanged;
            Data.StringTable.OnLanguageChanged += HandleLanguageChanged;
            _instance = this;
            EnsureBound();
            // ⚠⚠ 여기서 자기를 끄지 않는다 — 이 판은 비활성으로 저장돼 있어 Awake 가
            //   «처음 열릴 때» 돈다. 그 자리에서 끄면 영영 안 뜬다.
        }

        void OnDestroy()
        {
            // ⚠ 정적 이벤트라 끊지 않으면 죽은 오브젝트가 구독에 남는다(SettingsPanel 의 그 ⚠).
            Data.StringTable.OnLanguageChanged -= HandleLanguageChanged;
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// 언어가 바뀌면 버튼 문구를 다시 받아 오고, <b>떠 있는 단계</b>를 그 자리에서 다시 쓴다.
        /// ★ 단계 글이 이제 표에서 오므로(184절) «다음 단계부터» 를 기다릴 이유가 없다.
        /// ⚠ <see cref="ShowStep"/> 은 범위 밖이면 창을 닫는다 — 그래서 범위를 먼저 본다.
        /// </summary>
        void HandleLanguageChanged()
        {
            LocalizeLabels();
            if (IsOpen && _index >= 0 && _index < _steps.Count) ShowStep();
        }

        public bool IsOpen => gameObject.activeSelf;

        // ------------------------------------------------------------------

        /// <summary>
        /// ★ <b>이 항목에 짚어 줄 것이 있는가</b> — 「자세히 보기」 버튼을 띄울지 정하는 <b>한 자리</b>.
        ///
        /// 조언 카드와 백과가 <b>같은 판단</b>을 써야 한다. 두 곳에서 각자 따지면 한쪽에만
        /// 버튼이 남아 «눌러도 아무 일이 없는 버튼» 이 된다(이 프로젝트가 건설 버튼에서 겪은 일).
        /// </summary>
        public static bool HasTour(HelpEntry entry)
        {
            if (entry == null) return false;
            HelpTableSO table = HelpTableSO.Load();
            return table != null && table.HasSteps(entry.helpId);
        }

        /// <summary>
        /// 그 항목의 안내를 처음부터 보여준다. 항목에 창이 지정돼 있으면 <b>그 창을 먼저 연다</b>.
        /// <returns>단계가 하나라도 있어서 실제로 시작했으면 <c>true</c>.</returns>
        /// ★ <c>false</c> 면 부르는 쪽은 <b>아무것도 하지 않는다</b> — 애초에 그런 항목에는
        ///   「자세히 보기」 버튼이 뜨지 않는다(<see cref="HasTour"/>).
        /// </summary>
        public bool Begin(HelpEntry entry)
        {
            if (entry == null) return false;

            HelpTableSO table = HelpTableSO.Load();
            if (table == null) return false;

            table.CollectSteps(entry.helpId, _steps);
            if (_steps.Count == 0) return false;

            // ⚠ <b>이미 안내가 돌고 있으면 먼저 끝낸다.</b> 백과에서 「화면에서 짚어 보기」를
            //   연달아 누를 수 있는데, 그때 창을 덮어쓰면 <b>앞 창의 소유권 표시가 사라져</b>
            //   내가 열어 둔 창이 영영 안 닫힌다. 다음 창이 배타 처리로 앞 창을 닫아 주는 것에
            //   기대면 «배타가 아닌 창» 이 하나 생기는 날 조용히 새어 나간다.
            if (IsOpen) CloseWindow();

            // ★★ <b>조언 카드가 떠 있으면 닫는다</b> (2026-08-24 · 유저 리포트).
            //   카드는 뜰 때 <c>SetAsLastSibling</c> 을 부르므로 <b>안내보다 앞</b>에 있다 —
            //   안 닫으면 «짚어 주는 판이 카드 뒤에» 깔린다. 「자세히 보기」 경로는 이미
            //   카드를 닫고 오지만, <b>순서에 기대지 않는다</b>(검수 메뉴·다른 UI 가 부를 수 있다).
            //   ⚠ <see cref="HelpCardPanel.CloseSilently"/> 로 닫는다 — 그냥 <c>Close</c> 는
            //     «다 읽었다» 길이라 가로챈 버튼의 창을 <b>지금</b> 열어 버린다(2026-08-25).
            HelpCardPanel card = HelpCardPanel.Instance;
            if (card != null && card.IsOpen) card.CloseSilently();

            EnsureBound();
            _entry = entry;
            _index = 0;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _pause.Acquire();
            OpenWindow(entry);
            ShowStep();
            return true;
        }

        /// <summary>
        /// 안내를 끝낸다. <b>내가 연 창과 내가 멈춘 시간만</b> 되돌린다.
        ///
        /// ★★ 그러고 나서 <b>가로챈 버튼의 일을 마무리한다</b>
        /// (<see cref="HelpService.CompletePending"/> · 2026-08-25). 액션 버튼을 눌러
        /// 이 안내까지 온 유저는 <b>그 창을 쓰려던 것</b>이므로, 설명이 끝나면 창이
        /// <b>열린 채로</b> 남아야 한다. 소유권 규칙대로 위에서 한 번 닫고 여기서 다시 여는데,
        /// <b>같은 프레임</b>이라 화면에는 끊김이 보이지 않는다.
        /// </summary>
        public void Close()
        {
            CloseWindow();
            _pause.Release();
            gameObject.SetActive(false);
            _entry = null;
            _steps.Clear();

            HelpService.Instance?.CompletePending();
        }

        // ------------------------------------------------------------------
        // 창 열고 닫기 — 위 ★★★
        // ------------------------------------------------------------------

        void OpenWindow(HelpEntry entry)
        {
            _window = null;
            _windowOpenedByMe = false;
            _waitLayout = false;

            if (string.IsNullOrWhiteSpace(entry.openPanelPath)) return;

            Transform w = Resolve(entry.openPanelPath);
            if (w == null)
            {
                Debug.LogWarning($"[도움말] 열 창을 찾지 못했습니다: {entry.openPanelPath} " +
                                 "(표의 open_panel 을 확인하세요). 창 없이 안내만 보여줍니다.", this);
                return;
            }

            // ★ 이미 열려 있으면 <b>내 것이 아니다</b> — 끝나도 닫지 않는다.
            bool wasOpen = w.gameObject.activeSelf;

            if (!HudExclusive.TryOpen(w, true))
            {
                Debug.LogWarning($"[도움말] {entry.openPanelPath} 는 바깥에서 열 수 있는 창이 " +
                                 "아닙니다(HudExclusive.TryOpen 에 가지를 더하세요). " +
                                 "창 없이 안내만 보여줍니다.", this);
                return;
            }

            _window = w;
            _windowOpenedByMe = !wasOpen;

            // ⚠ 창을 켠 그 프레임에는 레이아웃이 아직 돌지 않았다 — 한 프레임 미룬다.
            _waitLayout = true;

            // ★ 창이 열리며 HudExclusive.OpenOnly 가 그 창을 맨 앞으로 올린다. 그래도 이 안내는
            //   <b>다른 캔버스</b>(Help_Root · sortingOrder 20)에 있어 계속 위에 보인다.
            transform.SetAsLastSibling();
        }

        void CloseWindow()
        {
            if (_window != null && _windowOpenedByMe) HudExclusive.TryOpen(_window, false);
            _window = null;
            _windowOpenedByMe = false;
            _waitLayout = false;
        }

        void Step(int delta)
        {
            int next = _index + delta;
            if (next < 0) return;
            if (next >= _steps.Count) { Close(); return; }

            _index = next;
            ShowStep();
        }

        // ------------------------------------------------------------------

        /// <summary>지금 단계가 짚고 있는 칸. 없으면 null(글만 보여주는 단계).</summary>
        RectTransform _target;

        void ShowStep()
        {
            if (_index < 0 || _index >= _steps.Count) { Close(); return; }
            HelpStepRow step = _steps[_index];

            SetText(_title, _entry != null ? _entry.Title : "");
            SetText(_counter, string.Format(counterFormat, _index + 1, _steps.Count));

            _target = Resolve(step.targetPath);
            bool visible = _target != null && _target.gameObject.activeInHierarchy;

            // ★ 단계 글도 <b>스트링 표</b>를 거친다 (2026-08-27 · 184절) — 표의 stepText 는
            //   이제 폴백이다(<see cref="HelpStepRow.Text"/>).
            string text = step.Text;
            SetText(_text, visible || string.IsNullOrEmpty(step.targetPath)
                         ? text
                         : text + "\n" + missingNote);

            // 버튼 문구 — 마지막 단계에서는 「다음」이 「다 봤습니다」로 바뀐다.
            bool last = _index == _steps.Count - 1;
            SetText(_nextText, last ? lastLabel : nextLabel);
            SetText(_prevText, prevLabel);
            SetText(_quitText, quitLabel);
            if (_prev != null) _prev.gameObject.SetActive(_index > 0);

            Reposition();
        }

        /// <summary>
        /// ★★ 테두리와 말풍선을 <b>지금 좌표로</b> 다시 잡는다.
        ///
        /// <b>왜 매 프레임 하는가</b> — 짚는 대상이 <b>가만히 있지 않는다</b>:
        ///   · 창을 켠 <b>첫 프레임</b>에는 레이아웃이 아직 안 돌아 좌표가 엉뚱하다
        ///   · 유저가 창을 <b>끌어 옮길</b> 수 있다(<see cref="UiWindowDrag"/>)
        ///   · 목록이 <b>다시 그려지며</b> 칸 높이가 바뀐다(로스터·유물 목록)
        /// 한 번만 잡아 두면 그 세 경우에 테두리가 <b>엉뚱한 데</b> 남는다. 비용은 막대 넷과
        /// 말풍선 하나의 좌표 계산뿐이라 무시할 만하다.
        /// </summary>
        void Reposition()
        {
            bool visible = _target != null && _target.gameObject.activeInHierarchy;
            if (_frame != null && _frame.gameObject.activeSelf != visible)
                _frame.gameObject.SetActive(visible);

            if (visible) PlaceFrame(_target);
            PlaceBubble(visible ? _target : null);
        }

        /// <summary>
        /// ★★ 대상의 네 귀퉁이를 <b>이 캔버스의 좌표</b>로 옮겨 막대 넷을 놓는다.
        ///
        /// ⚠ 두 판이 <b>다른 캔버스</b>에 있을 수 있다(대상은 <c>UI_Root</c>, 이 판은
        ///   <c>Help_Root</c>). 그래서 <c>anchoredPosition</c> 을 그대로 베끼면 어긋난다 —
        ///   <b>월드 좌표를 거쳐</b> 옮겨야 한다. 둘 다 Screen Space Overlay 라
        ///   월드 좌표가 곧 화면 좌표다.
        /// </summary>
        void PlaceFrame(RectTransform target)
        {
            if (_frame == null || _self == null || target == null) return;

            Vector3[] corners = _corners;
            target.GetWorldCorners(corners);

            // 0 = 좌하 · 1 = 좌상 · 2 = 우상 · 3 = 우하
            Vector2 min = _self.InverseTransformPoint(corners[0]);
            Vector2 max = _self.InverseTransformPoint(corners[2]);

            min -= Vector2.one * borderPadding;
            max += Vector2.one * borderPadding;

            float w = max.x - min.x;
            float h = max.y - min.y;
            float t = borderThickness;

            _frame.anchoredPosition = (min + max) * 0.5f;
            _frame.sizeDelta = new Vector2(w, h);

            // 막대 넷 — 프레임의 안쪽 테두리에 붙인다(프레임 자신은 아무것도 그리지 않는다).
            Place(_bTop, new Vector2(0f, (h - t) * 0.5f), new Vector2(w, t));
            Place(_bBottom, new Vector2(0f, -(h - t) * 0.5f), new Vector2(w, t));
            Place(_bLeft, new Vector2(-(w - t) * 0.5f, 0f), new Vector2(t, h - t * 2f));
            Place(_bRight, new Vector2((w - t) * 0.5f, 0f), new Vector2(t, h - t * 2f));
        }

        static readonly Vector3[] _corners = new Vector3[4];

        static void Place(RectTransform bar, Vector2 pos, Vector2 size)
        {
            if (bar == null) return;
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
            bar.pivot = new Vector2(0.5f, 0.5f);
            bar.anchoredPosition = pos;
            bar.sizeDelta = size;
        }

        /// <summary>
        /// 말풍선을 대상 <b>위</b>에 놓는다. 위에 자리가 없으면 <b>아래</b>로 내린다.
        /// 대상이 없으면 화면 가운데 아래쪽에 둔다.
        /// </summary>
        void PlaceBubble(RectTransform target)
        {
            if (_bubble == null || _self == null) return;

            Vector2 size = _bubble.sizeDelta;
            float halfW = _self.rect.width * 0.5f;
            float halfH = _self.rect.height * 0.5f;

            if (target == null)
            {
                _bubble.anchoredPosition = new Vector2(0f, -halfH * 0.35f);
                return;
            }

            target.GetWorldCorners(_corners);
            Vector2 min = _self.InverseTransformPoint(_corners[0]);
            Vector2 max = _self.InverseTransformPoint(_corners[2]);
            float cx = (min.x + max.x) * 0.5f;

            float above = max.y + bubbleGap + size.y * 0.5f;
            float below = min.y - bubbleGap - size.y * 0.5f;

            // 위쪽이 화면 안에 들어가면 위, 아니면 아래.
            float y = above + size.y * 0.5f <= halfH - screenMargin ? above : below;
            y = Mathf.Clamp(y, -halfH + size.y * 0.5f + screenMargin,
                                halfH - size.y * 0.5f - screenMargin);
            float x = Mathf.Clamp(cx, -halfW + size.x * 0.5f + screenMargin,
                                       halfW - size.x * 0.5f - screenMargin);

            _bubble.anchoredPosition = new Vector2(x, y);
        }

        void Update()
        {
            // ⚠ 창을 켠 다음 프레임에 <b>다시 찾는다</b> — 첫 프레임에는 레이아웃이 돌지 않았고,
            //   창이 켜지면서 비활성이던 자식이 활성으로 바뀌므로 «보이는가» 판정도 달라진다.
            if (_waitLayout)
            {
                _waitLayout = false;
                ShowStep();
            }

            Reposition();
            Pulse();

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame ||
                kb.spaceKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
                Step(1);
            else if (kb.leftArrowKey.wasPressedThisFrame)
                Step(-1);
        }

        /// <summary>
        /// 테두리를 천천히 깜빡인다.
        /// ⚠ <see cref="Time.unscaledTime"/> — 안내 중에는 게임이 멈춰 있어
        ///   <c>Time.time</c> 이 흐르지 않는다(깜빡임이 얼어붙는다).
        /// </summary>
        void Pulse()
        {
            if (_bars == null || pulseSeconds <= 0f) return;

            float t = Mathf.Repeat(Time.unscaledTime, pulseSeconds) / pulseSeconds;
            float a = Mathf.Lerp(pulseMinAlpha, 1f, 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI * 2f));

            for (int i = 0; i < _bars.Length; i++)
            {
                if (_bars[i] == null) continue;
                Color c = _bars[i].color;
                c.a = a;
                _bars[i].color = c;
            }
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// ★★ 씬 경로로 <see cref="RectTransform"/> 을 찾는다 — <b>비활성도 찾는다</b>.
        ///
        /// ⚠ <see cref="GameObject.Find"/> 는 <b>비활성 오브젝트를 못 찾는다</b>. 짚어야 할 곳이
        ///   닫힌 창 안에 있을 수 있으므로(그때는 «화면에 없습니다» 를 알려야 한다) 뿌리부터
        ///   손으로 걸어간다 — <see cref="Transform.Find"/> 는 비활성 자식을 찾는다.
        /// </summary>
        static RectTransform Resolve(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            string[] parts = path.Split('/');
            Transform node = null;

            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i].name == parts[0]) { node = roots[i].transform; break; }

            for (int i = 1; node != null && i < parts.Length; i++)
                node = node.Find(parts[i]);

            return node as RectTransform;
        }

        void EnsureBound()
        {
            if (_bound) return;
            _bound = true;

            _self = transform as RectTransform;

            _frame = transform.Find("Frame") as RectTransform;
            if (_frame != null)
            {
                _bTop = _frame.Find("Top") as RectTransform;
                _bBottom = _frame.Find("Bottom") as RectTransform;
                _bLeft = _frame.Find("Left") as RectTransform;
                _bRight = _frame.Find("Right") as RectTransform;
                _bars = new[]
                {
                    _bTop != null ? _bTop.GetComponent<Image>() : null,
                    _bBottom != null ? _bBottom.GetComponent<Image>() : null,
                    _bLeft != null ? _bLeft.GetComponent<Image>() : null,
                    _bRight != null ? _bRight.GetComponent<Image>() : null,
                };
            }
            else Debug.LogWarning("[도움말] Frame 을 찾지 못했습니다 — " +
                                  "py -3 Tools/mcp_build_help_ui.py 를 돌리세요.", this);

            _bubble = transform.Find("Bubble") as RectTransform;
            if (_bubble == null) return;

            _title = FindText(_bubble, "Title");
            _counter = FindText(_bubble, "Counter");
            _text = FindText(_bubble, "Text");

            _next = _bubble.Find("NextButton")?.GetComponent<Button>();
            _prev = _bubble.Find("PrevButton")?.GetComponent<Button>();
            _quit = _bubble.Find("QuitButton")?.GetComponent<Button>();
            _nextText = FindText(_bubble, "NextButton/Label");
            _prevText = FindText(_bubble, "PrevButton/Label");
            _quitText = FindText(_bubble, "QuitButton/Label");

            if (_next != null)
            {
                _next.onClick.RemoveAllListeners();
                _next.onClick.AddListener(() => Step(1));
            }
            if (_prev != null)
            {
                _prev.onClick.RemoveAllListeners();
                _prev.onClick.AddListener(() => Step(-1));
            }
            if (_quit != null)
            {
                _quit.onClick.RemoveAllListeners();
                _quit.onClick.AddListener(Close);
            }

            // ⚠ 넘침 방지는 코드가 한다 — TMP 의 줄바꿈·자동 크기 칸은 MCP 로 못 넘긴다.
            HudTheme.FitText(_title, 13f, wrap: false);
            HudTheme.FitText(_counter, 10f, wrap: false);
            HudTheme.FitText(_text, 12f);
            HudTheme.FitText(_nextText, 11f, wrap: false);
            HudTheme.FitText(_prevText, 11f, wrap: false);
            HudTheme.FitText(_quitText, 10f, wrap: false);
        }

        static void SetText(TMP_Text t, string value)
        {
            if (t != null) t.text = value ?? "";
        }

        static TMP_Text FindText(Transform parent, string path)
        {
            Transform t = parent.Find(path);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }
    
        /// <summary>
        /// ★ 이 창의 문구를 <b>스트링 표</b>에서 가져온다 (2026-08-26 · 178-5절).
        /// 인스펙터 값은 <b>폴백</b>이다 — 표에 키가 없으면 화면은 지금과 같다.
        /// </summary>
        void LocalizeLabels()
        {
            nextLabel = HudTheme.T("ui_tour_next", nextLabel);
            prevLabel = HudTheme.T("ui_tour_prev", prevLabel);
            lastLabel = HudTheme.T("ui_tour_last", lastLabel);
            quitLabel = HudTheme.T("ui_tour_quit", quitLabel);
            missingNote = HudTheme.T("ui_tour_missing_note", missingNote);
        }
}
}
