using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Help;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★★ <b>도움말 창(백과)</b> — 문명의 «문명 백과» 자리 (2026-08-24 신설 · 유저 지시:
    /// *"듀토리얼 만들고 싶은데 문명 듀토리얼 처럼 도움말처럼 구성하고 싶음"*).
    ///
    /// <b>구조는 유물 관리 창과 같다</b>(<see cref="RelicPanel"/>) — 왼쪽에 목록, 오른쪽에 상세.
    /// 그 위에 <b>분류 탭</b> 한 줄이 더 붙는다(항목이 27개라 한 목록에 다 담으면 못 찾는다).
    /// 같은 API 모양(<c>Instance</c>/<c>IsOpen</c>/<c>Toggle</c>/<c>SetOpen</c>/<c>Close</c>)을 쓰고
    /// <see cref="HudExclusive"/> 로 배타 처리한다 — 창이 하나 늘 때마다 조작감이 갈리지 않게.
    ///
    /// <b>무엇을 보여주나</b>
    /// <code>
    ///   탭   : 표에 나온 순서대로의 분류 (기본 · 전투 · 성장 · 지휘 · 위험 · 운영)
    ///   목록 : 그 분류의 항목을 order 순으로. <b>읽은 것/안 읽은 것</b>을 점으로 가른다
    ///   상세 : 제목 · 요약 · 본문 · 「함께 볼 것」 버튼(see_also)
    /// </code>
    ///
    /// ★ <b>탭과 목록 칸은 원본을 복제해 만든다</b>(<c>TabTemplate</c> · <c>RowTemplate</c>) —
    ///   분류가 여섯이라 씬에 여섯 개를 박아 두면 표에서 분류를 하나 더할 때 <b>조용히
    ///   안 보인다</b>. 표가 정본이므로 개수도 표에서 와야 한다.
    ///
    /// ★ <b>열려 있는 동안 게임이 멈춘다</b> (유저 지시: *"도움말 뜨면 게임 일시정지 되야함"*).
    ///   멈추는 방법은 <see cref="ReadingPause"/> 에 있다 — <c>timeScale</c> 을 직접 쓰지 않고
    ///   <see cref="GameSpeedPanel"/> 을 통하며 <b>내가 멈춘 것만 내가 푼다</b>.
    ///   조언 카드(<see cref="HelpCardPanel"/>)도 같은 클래스를 쓴다.
    ///
    /// ⚠ <b>리치 텍스트를 끄지 말 것</b> — 본문에 <c>&lt;b&gt;</c> 태그가 들어 있다
    ///   (표 <c>읽기</c> 시트의 표시 규약). 끄면 태그가 글자로 보인다.
    /// ⚠ 씬 배선은 <b>이름으로</b> 찾는다 — MCP 로는 인스펙터 참조를 넣을 수 없다(8절 4번).
    /// </summary>
    public class HelpPanel : MonoBehaviour, IExclusiveHudPanel
    {
        static HelpPanel _instance;

        public static HelpPanel Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<HelpPanel>(FindObjectsInactive.Include);
                return _instance;
            }
        }

        [Header("문구")]
        [SerializeField] string hintPick = "왼쪽에서 항목을 고르면 설명이 나옵니다.";
        [Tooltip("{0} = 읽은 수 · {1} = 전체 수")]
        [SerializeField] string hintProgress = "읽은 조언 {0} / {1}";
        [Tooltip("{0} = 함께 볼 항목의 제목")]
        [SerializeField] string seeAlsoFormat = "함께 볼 것 — {0}";
        [Tooltip("빨간 테두리로 실제 화면을 짚어 주는 안내를 여는 버튼의 문구")]
        [SerializeField] string tourLabel = "화면에서 짚어 보기";

        [Tooltip("★ 본문 스크롤의 휠 감도. 본문이 길어 기본값(1)이면 한참 굴려야 한다")]
        [Min(1f)] [SerializeField] float bodyScrollSensitivity = 28f;

        [Header("읽음 표시")]
        [Tooltip("이미 조언으로 뜬 적이 있는 항목 앞에 붙는 글자")]
        [SerializeField] string dotSeen = "•";
        [Tooltip("아직 안 뜬 항목 앞에 붙는 글자")]
        [SerializeField] string dotUnseen = "◦";

        // ⚠ 아래 넷은 <b>그림이 없을 때 칠할 색</b>이다(<see cref="HudTheme.PaintButton"/> 의
        //   fallback). 탭에는 <c>Btn_Tab_*</c> 그림이 깔리므로 실제로는 안 쓰이고,
        //   목록 행은 그림을 안 깔기 때문에 <b>행 둘만</b> 실제로 화면에 나온다.
        [Header("색 (그림이 없을 때만)")]
        [SerializeField] Color rowNormal = new Color(0.11f, 0.13f, 0.17f, 0.92f);
        [SerializeField] Color rowSelected = new Color(0.16f, 0.42f, 0.38f, 0.98f);
        [SerializeField] Color tabNormal = new Color(0.13f, 0.17f, 0.22f, 0.95f);
        [SerializeField] Color tabSelected = new Color(0.16f, 0.42f, 0.38f, 0.98f);

        /// <summary>목록 한 칸.</summary>
        class Row
        {
            public GameObject Root;
            public Image Background;
            public Button Button;
            public TMP_Text Dot;
            public TMP_Text Name;
            public string HelpId;
        }

        /// <summary>분류 탭 하나.</summary>
        class Tab
        {
            public GameObject Root;
            public Image Background;
            public Button Button;
            public TMP_Text Label;
            public string Category;
        }

        readonly List<Row> _rows = new List<Row>();
        readonly List<Tab> _tabs = new List<Tab>();
        readonly List<HelpEntry> _shown = new List<HelpEntry>();

        HelpTableSO _table;

        RectTransform _rowTemplate, _tabTemplate;
        Transform _list, _tabBox;
        TMP_Text _hint, _detailTitle, _detailCategory, _detailSummary, _detailBody, _seeAlsoLabel;

        /// <summary>본문을 감싼 스크롤. <see cref="EnsureBodyScroll"/> 이 <b>코드로</b> 짓는다.</summary>
        ScrollRect _bodyScroll;

        /// <summary>
        /// ★★★ <b>본문에 스크롤을 단다</b> (2026-08-26 · 유저 지시: *"도움말 정신이상 설명
        /// 같은거 너무 길어서 폰트 깨지니까 그냥 스크롤 기능 넣어서 해줘"*).
        ///
        /// <b>왜 깨졌나</b> — 예전에는 <see cref="HudTheme.FitText"/> 로 «칸에 맞게 줄여서»
        /// 넣었다. 그 방법은 241자짜리 본문까지는 버텼지만, 「정신 이상 낱낱」처럼
        /// <b>스무 줄이 넘는</b> 글이 들어오자 11pt 하한까지 줄어들어 <b>글자가 뭉갰다</b>.
        /// 칸은 그대로인데 글이 길어졌으면 <b>줄일 것이 아니라 넘겨 보게</b> 해야 한다.
        ///
        /// <b>왜 코드가 짓나</b> — <see cref="ScrollRect"/> 의 <c>content</c>·<c>viewport</c> 는
        /// <b>오브젝트 참조</b>라 MCP 로 넣을 수 없다(8절 1번). 씬에는 <c>Detail/Body</c> 하나만
        /// 두고 여기서 감싼다 — <c>RelicIconStrip</c> 이 아이콘 하나를 셋으로 늘리는 것과 같은 방식이다.
        ///
        /// ★ <b>Body 를 «있던 자리 그대로» 감싼다</b> — 뷰포트가 Body 의 앵커·크기·형제 순서를
        ///   물려받으므로 화면에서 자리가 <b>1px도 안 움직인다</b>.
        /// ⚠ <b>자동 크기를 끈다</b> — 스크롤이 있으면 줄일 이유가 없고, 켜 두면 «줄이기» 와
        ///   «늘려서 스크롤» 이 서로 싸워 높이가 진동한다.
        /// ⚠ 경로 조회(<c>Detail/Body</c>)가 <b>끝난 뒤에</b> 불러야 한다 — 먼저 감싸면
        ///   그 경로가 <c>Detail/BodyScroll/Body</c> 로 바뀌어 못 찾는다.
        /// </summary>
        void EnsureBodyScroll()
        {
            if (_bodyScroll != null || _detailBody == null) return;

            RectTransform body = _detailBody.rectTransform;
            var parent = body.parent as RectTransform;
            if (parent == null) return;

            var go = new GameObject("BodyScroll",
                                    typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
            var view = (RectTransform)go.transform;
            view.SetParent(parent, false);
            view.anchorMin = body.anchorMin;
            view.anchorMax = body.anchorMax;
            view.pivot = body.pivot;
            view.sizeDelta = body.sizeDelta;
            view.anchoredPosition = body.anchoredPosition;
            view.SetSiblingIndex(body.GetSiblingIndex());   // 그리는 순서도 그대로

            body.SetParent(view, false);
            body.anchorMin = new Vector2(0f, 1f);
            body.anchorMax = new Vector2(1f, 1f);
            body.pivot = new Vector2(0.5f, 1f);
            body.anchoredPosition = Vector2.zero;
            body.sizeDelta = new Vector2(0f, body.sizeDelta.y);

            // 글 길이에 따라 높이가 자란다 — 그 높이가 곧 스크롤 범위다.
            var fitter = body.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _detailBody.enableAutoSizing = false;
            _detailBody.textWrappingMode = TextWrappingModes.Normal;
            _detailBody.overflowMode = TextOverflowModes.Overflow;

            _bodyScroll = go.GetComponent<ScrollRect>();
            _bodyScroll.content = body;
            _bodyScroll.viewport = view;
            _bodyScroll.horizontal = false;
            _bodyScroll.vertical = true;
            _bodyScroll.movementType = ScrollRect.MovementType.Clamped;
            _bodyScroll.scrollSensitivity = bodyScrollSensitivity;
        }
        Button _seeAlsoButton;

        /// <summary>「화면에서 짚어 보기」 — 표에 단계가 있는 항목에서만 켠다.</summary>
        Button _tourButton;
        TMP_Text _tourLabelText;

        string _category;
        string _selectedId;
        bool _bound;

        /// <summary>읽는 동안의 일시정지. 규칙은 <see cref="ReadingPause"/> 에 있다.</summary>
        readonly ReadingPause _pause = new ReadingPause();

        void Awake()
        {
            LocalizeLabels();
            // ★★★ 2026-08-27 — 언어가 바뀌면 다시 그린다.
            Data.StringTable.OnLanguageChanged -= HandleLanguageChanged;
            Data.StringTable.OnLanguageChanged += HandleLanguageChanged;
            _instance = this;
            EnsureBound();
            // ⚠⚠ 여기서 자기를 끄지 않는다 — 이 창은 비활성으로 저장돼 있어 Awake 가
            //   «처음 열릴 때» 돈다(RelicPanel 의 ⚠⚠ 와 같은 함정).
        }

        void OnDestroy()
        {
            // ⚠ 정적 이벤트라 끊지 않으면 죽은 오브젝트가 구독에 남는다(SettingsPanel 의 그 ⚠).
            Data.StringTable.OnLanguageChanged -= HandleLanguageChanged;
            if (_instance == this) _instance = null;
        }

        /// <summary>언어가 바뀌면 문구를 다시 받아 오고, 열려 있으면 목록째 다시 짓는다.</summary>
        void HandleLanguageChanged()
        {
            LocalizeLabels();
            if (IsOpen) Rebuild();
        }

        // ------------------------------------------------------------------
        // 열고 닫기 — 다른 창과 같은 API
        // ------------------------------------------------------------------

        public bool IsOpen => gameObject.activeSelf;

        public void Toggle() => SetOpen(!IsOpen);

        public void Close()
        {
            // ⚠ 멈춤을 <b>먼저</b> 푼다 — 창을 끄고 나면 이 컴포넌트의 Update 가 돌지 않아
            //   «닫혔는데 안 흐른다» 를 되돌릴 자리가 없다.
            _pause.Release();
            gameObject.SetActive(false);
        }

        public void SetOpen(bool open)
        {
            EnsureBound();

            if (!open) { Close(); return; }

            gameObject.SetActive(true);
            HudExclusive.OpenOnly(this);
            _pause.Acquire();
            Rebuild();
        }

        /// <summary>
        /// ★ <b>그 항목을 펼친 채로 연다</b> — 조언 카드의 「자세히 보기」가 부른다.
        /// 분류 탭까지 같이 옮겨 준다(그러지 않으면 «목록에 없는 항목» 이 상세에 떠 있게 된다).
        /// </summary>
        public void OpenAt(HelpEntry entry)
        {
            EnsureBound();
            if (entry != null)
            {
                _category = entry.category;
                _selectedId = entry.helpId;
            }
            SetOpen(true);
        }

        /// <summary><c>help_id</c> 로 여는 편의 함수 — 다른 UI 가 «이 부분 설명» 버튼을 달 때 쓴다.</summary>
        public void OpenAt(string helpId)
        {
            EnsureBound();
            OpenAt(_table != null ? _table.ById(helpId) : null);
        }

        // ------------------------------------------------------------------
        // 그리기
        // ------------------------------------------------------------------

        void Rebuild()
        {
            EnsureBound();
            if (_table == null || _list == null || _rowTemplate == null) return;

            RebuildTabs();

            _table.CollectByCategory(_category, _shown);

            // 고른 것이 이 분류에 없으면 맨 위로 — 상세가 «빈 칸» 이 되는 것을 막는다.
            if (_table.ById(_selectedId) == null || !_shown.Exists(e => e.helpId == _selectedId))
                _selectedId = _shown.Count > 0 ? _shown[0].helpId : null;

            while (_rows.Count < _shown.Count) _rows.Add(MakeRow());

            HelpService service = HelpService.Instance;

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                bool used = i < _shown.Count;
                if (row.Root.activeSelf != used) row.Root.SetActive(used);
                if (!used) continue;

                HelpEntry e = _shown[i];
                row.HelpId = e.helpId;

                if (row.Name != null) row.Name.text = e.Title;
                if (row.Dot != null)
                    row.Dot.text = service != null && service.IsSeen(e) ? dotSeen : dotUnseen;
                // ★ 목록 행은 <b>그림을 안 깐다</b>(배선의 ⚠ — 행은 선택 색으로 갈라야 한다).
                //   그래도 <see cref="HudTheme.PaintButton"/> 을 거치는 이유는 «그림이 없으면
                //   색을 칠한다» 가 그 함수 안에 있어서다 — 창마다 규칙이 갈리지 않는다.
                if (row.Background != null)
                    HudTheme.PaintButton(row.Background,
                                         e.helpId == _selectedId ? ButtonState.On : ButtonState.Normal,
                                         e.helpId == _selectedId ? rowSelected : rowNormal);

                string captured = e.helpId;
                row.Button.onClick.RemoveAllListeners();
                row.Button.onClick.AddListener(() => Select(captured));
            }

            ShowDetail();
        }

        void RebuildTabs()
        {
            if (_tabBox == null || _tabTemplate == null) return;

            List<string> categories = _table.Categories();
            if (categories.Count == 0) return;

            if (string.IsNullOrEmpty(_category) || !categories.Contains(_category))
                _category = categories[0];

            while (_tabs.Count < categories.Count) _tabs.Add(MakeTab());

            for (int i = 0; i < _tabs.Count; i++)
            {
                Tab tab = _tabs[i];
                bool used = i < categories.Count;
                if (tab.Root.activeSelf != used) tab.Root.SetActive(used);
                if (!used) continue;

                string name = categories[i];
                tab.Category = name;
                // ★ 탭에 <b>보여 주는 이름</b>은 표를 거친다 (2026-08-27 · 184절).
                //   ⚠ <see cref="Tab.Category"/> 는 <b>번역하지 않는다</b> — 그것은 항목을 묶는
                //     열쇠라(<see cref="HelpTableSO.CollectByCategory"/>) 번역하면 언어를 바꾼
                //     순간 «그 분류에 항목이 하나도 없다» 가 된다.
                if (tab.Label != null) tab.Label.text = CategoryLabel(name);

                // ★★ <b>탭은 «색» 이 아니라 «그림» 으로 갈린다</b> (2026-08-26 · 유저 지시:
                //   *"도움말 ui 위 쪽 메뉴 이미지들 밝은 색 이미지로 변경 가시성이 너무 안 좋음"*).
                //
                //   예전에는 여기서 <c>Background.color</c> 에 어두운 <c>tabNormal</c> 을
                //   직접 칠했다. 배선이 탭에 그림을 깔자 그 색이 그림에 <b>곱해져</b>
                //   새까매졌다 — <see cref="HudTheme.PaintButton"/> 의 설명에 있는
                //   «그림을 넣었는데 안 보인다» 와 정확히 같은 함정이다.
                // ★ 계열(<c>Btn_Tab_</c>)은 <b>씬에 붙은 그림</b>이 말해 준다 — 이 코드는
                //   어느 그림을 쓰는지 몰라도 된다.
                if (tab.Background != null)
                    HudTheme.PaintButton(tab.Background,
                                         name == _category ? ButtonState.On : ButtonState.Normal,
                                         name == _category ? tabSelected : tabNormal);

                string captured = name;
                tab.Button.onClick.RemoveAllListeners();
                tab.Button.onClick.AddListener(() => SelectCategory(captured));
            }
        }

        /// <summary>
        /// 분류 <b>식별자</b>를 화면에 쓸 글자로 바꿔 준다 (2026-08-27 · 184절).
        ///
        /// ★ <see cref="HelpTableSO.Categories"/> 가 돌려주는 것은 <b>식별자</b>라 그 자리에는
        ///   스트링 키가 없다. 그 분류의 항목 <b>하나</b>를 찾아 그것이 들고 있는 키를 쓴다 —
        ///   같은 분류의 줄은 전부 같은 키를 들고 있다(표가 그렇게 굽힌다).
        /// ⚠ 못 찾으면 식별자를 그대로 보여준다 — 지금까지의 화면 그대로다.
        /// </summary>
        string CategoryLabel(string category)
        {
            if (_table == null || string.IsNullOrEmpty(category)) return category;
            for (int i = 0; i < _table.entries.Count; i++)
            {
                HelpEntry e = _table.entries[i];
                if (e != null && e.category == category) return e.CategoryName;
            }
            return category;
        }

        void SelectCategory(string category)
        {
            if (_category == category) return;
            _category = category;
            _selectedId = null;      // 새 분류의 맨 위로 (Rebuild 가 정한다)
            Rebuild();
        }

        void Select(string helpId)
        {
            _selectedId = helpId;
            Rebuild();
        }

        void ShowDetail()
        {
            HelpEntry e = _table != null ? _table.ById(_selectedId) : null;

            SetText(_detailTitle, e != null ? e.Title : "-");
            SetText(_detailCategory, e != null ? e.CategoryName : "");
            SetText(_detailSummary, e != null ? e.Summary : "");
            SetText(_detailBody, e != null ? e.Body : "");

            // ★ 항목을 바꾸면 <b>맨 위부터</b> 보여준다 — 앞 항목을 내려 읽던 자리에
            //   그대로 서 있으면 «글이 중간부터 시작하는» 것으로 보인다.
            if (_bodyScroll != null) _bodyScroll.verticalNormalizedPosition = 1f;

            HelpService service = HelpService.Instance;
            if (_hint != null)
                _hint.text = e == null ? hintPick
                           : service != null
                             ? string.Format(hintProgress, service.SeenCount, service.TotalCount)
                             : hintPick;

            // ── 「화면에서 짚어 보기」 ──
            //
            // ★ 표에 단계가 있는 항목에서만 켠다 — 없는데 켜 두면 «눌러도 아무 일이 없는 버튼»
            //   이 된다(이 프로젝트가 건설 버튼에서 이미 겪은 일이다).
            if (_tourButton != null)
            {
                // ⚠⚠ <b>글자를 여기서 다시 쓴다</b> (2026-08-27 · 184절). 예전에는
                //   <see cref="EnsureBound"/> 에서 <b>한 번만</b> 썼는데, 그 함수는 <c>_bound</c>
                //   가 막아 <b>두 번 돌지 않는다</b> — 창을 한 번 연 뒤 언어를 바꾸면
                //   <see cref="LocalizeLabels"/> 가 <c>tourLabel</c> 을 영어로 바꿔도
                //   <b>화면의 칸은 한국어 그대로</b>였다. 이 창의 다른 칸들은
                //   <see cref="UiLocalizer"/> 지도가 맡는데 이 버튼만 지도에도 없어
                //   <b>영영 한국어</b>인 유일한 자리였다.
                if (_tourLabelText != null) _tourLabelText.text = tourLabel;

                // ★ 판단은 <see cref="HelpTourPanel.HasTour"/> <b>한 곳</b>에서 한다 —
                //   조언 카드의 「자세히 보기」도 같은 함수를 쓴다. 두 곳에서 각자 따지면
                //   한쪽에만 버튼이 남아 «눌러도 아무 일이 없는 버튼» 이 된다.
                bool hasSteps = HelpTourPanel.HasTour(e);
                if (_tourButton.gameObject.activeSelf != hasSteps)
                    _tourButton.gameObject.SetActive(hasSteps);

                if (hasSteps)
                {
                    HelpEntry tourEntry = e;
                    _tourButton.onClick.RemoveAllListeners();
                    _tourButton.onClick.AddListener(() =>
                    {
                        HelpTourPanel tour = HelpTourPanel.Instance;
                        if (tour == null) return;
                        // ⚠ 안내가 화면을 짚으므로 <b>이 창을 먼저 닫는다</b> —
                        //   창이 떠 있으면 그 아래의 UI 를 가려 짚어도 안 보인다.
                        Close();
                        tour.Begin(tourEntry);
                    });
                }
            }

            // ── 「함께 볼 것」 ──
            if (_seeAlsoButton == null) return;

            HelpEntry other = e != null && _table != null ? _table.ById(e.seeAlso) : null;
            bool has = other != null;

            if (_seeAlsoButton.gameObject.activeSelf != has)
                _seeAlsoButton.gameObject.SetActive(has);
            if (!has) return;

            if (_seeAlsoLabel != null) _seeAlsoLabel.text = string.Format(seeAlsoFormat, other.Title);

            string captured = other.helpId;
            _seeAlsoButton.onClick.RemoveAllListeners();
            _seeAlsoButton.onClick.AddListener(() =>
            {
                _category = _table.ById(captured)?.category ?? _category;
                Select(captured);
            });
        }

        // ------------------------------------------------------------------

        Row MakeRow()
        {
            RectTransform clone = Instantiate(_rowTemplate, _list);
            clone.gameObject.SetActive(true);
            clone.name = $"HelpRow_{_rows.Count + 1}";

            var row = new Row
            {
                Root = clone.gameObject,
                Background = clone.GetComponent<Image>(),
                Button = clone.GetComponent<Button>(),
                Dot = clone.Find("Dot")?.GetComponent<TMP_Text>(),
                Name = clone.Find("Name")?.GetComponent<TMP_Text>(),
            };

            // ★ 목록 칸은 <b>한 줄</b>이다(높이 38px) — 「에픽 중립과 토벌 지시」처럼 긴 제목은
            //   줄바꿈 대신 글자가 줄어들어 들어간다(유물 창 목록과 같은 규칙).
            HudTheme.FitText(row.Name, 11f, wrap: false);
            HudTheme.FitText(row.Dot, 10f, wrap: false);
            return row;
        }

        Tab MakeTab()
        {
            RectTransform clone = Instantiate(_tabTemplate, _tabBox);
            clone.gameObject.SetActive(true);
            clone.name = $"HelpTab_{_tabs.Count + 1}";

            var tab = new Tab
            {
                Root = clone.gameObject,
                Background = clone.GetComponent<Image>(),
                Button = clone.GetComponent<Button>(),
                Label = clone.Find("Label")?.GetComponent<TMP_Text>(),
            };
            HudTheme.FitText(tab.Label, 11f, wrap: false);
            return tab;
        }

        void EnsureBound()
        {
            if (_bound) return;
            _bound = true;

            _table = HelpTableSO.Load();

            _tabBox = transform.Find("Tabs");
            _tabTemplate = transform.Find("Tabs/TabTemplate") as RectTransform;
            if (_tabTemplate != null) _tabTemplate.gameObject.SetActive(false);
            else Debug.LogWarning("[도움말] Tabs/TabTemplate 을 찾지 못했습니다.", this);

            _list = transform.Find("List/ScrollView/Viewport/Items");
            _rowTemplate = transform.Find("List/RowTemplate") as RectTransform;
            if (_rowTemplate != null) _rowTemplate.gameObject.SetActive(false);
            else Debug.LogWarning("[도움말] List/RowTemplate 을 찾지 못했습니다.", this);

            _hint = FindText(transform, "Hint");
            _detailTitle = FindText(transform, "Detail/Title");
            _detailCategory = FindText(transform, "Detail/Category");
            _detailSummary = FindText(transform, "Detail/Summary");
            _detailBody = FindText(transform, "Detail/Body");

            _seeAlsoButton = transform.Find("Detail/SeeAlsoButton")?.GetComponent<Button>();
            _seeAlsoLabel = FindText(transform, "Detail/SeeAlsoButton/Label");

            _tourButton = transform.Find("Detail/TourButton")?.GetComponent<Button>();
            _tourLabelText = FindText(transform, "Detail/TourButton/Label");
            if (_tourLabelText != null) _tourLabelText.text = tourLabel;

            // ⚠ 넘침 방지는 코드가 한다 — TMP 의 줄바꿈·자동 크기 칸은 MCP 로 못 넘긴다
            //   (HudTheme.FitText 의 ⚠). 본문은 가장 긴 것이 241자 · 5줄이다.
            HudTheme.FitText(_detailTitle, 16f, wrap: false);
            HudTheme.FitText(_detailCategory, 10f, wrap: false);
            HudTheme.FitText(_detailSummary, 12f);
            // ⚠ 본문에는 <b>FitText 를 걸지 않는다</b> — 아래 EnsureBodyScroll 이 스크롤을
            //   달아 «줄이는» 대신 «넘겨 보게» 한다(그 함수의 ★★★).
            HudTheme.FitText(_hint, 10f);
            HudTheme.FitText(_seeAlsoLabel, 11f, wrap: false);
            HudTheme.FitText(_tourLabelText, 11f, wrap: false);

            EnsureBodyScroll();

            var close = transform.Find("CloseButton")?.GetComponent<Button>();
            if (close != null)
            {
                close.onClick.RemoveAllListeners();
                close.onClick.AddListener(Close);
            }

            BindScrollRect();
        }

        /// <summary>
        /// 목록을 스크롤로 넘긴다. <b>구조는 유물 창과 같다</b> —
        /// <c>List/ScrollView</c>(ScrollRect) → <c>Viewport</c>(RectMask2D) → <c>Items</c>,
        /// 그 옆에 <c>List/Scrollbar</c>.
        ///
        /// ⚠ <c>ScrollRect</c>·<c>Scrollbar</c> 의 <b>object-참조 필드</b>는 MCP 로 넣을 수 없다
        ///   (8절 4번) — 이름으로 찾아 코드가 꽂는다. 인스펙터에서 이미 연결돼 있으면
        ///   <b>건드리지 않는다</b>(사람이 맞춘 값이 우선).
        /// </summary>
        void BindScrollRect()
        {
            var scroll = transform.Find("List/ScrollView")?.GetComponent<ScrollRect>();
            if (scroll == null) return;

            if (scroll.content == null) scroll.content = _list as RectTransform;
            if (scroll.viewport == null)
                scroll.viewport = transform.Find("List/ScrollView/Viewport") as RectTransform;

            if (scroll.verticalScrollbar != null) return;

            var bar = transform.Find("List/Scrollbar")?.GetComponent<Scrollbar>();
            if (bar == null) return;

            scroll.verticalScrollbar = bar;
            if (bar.handleRect == null)
                bar.handleRect = transform.Find("List/Scrollbar/Handle") as RectTransform;
            if (bar.targetGraphic == null)
                bar.targetGraphic = transform.Find("List/Scrollbar/Handle")?.GetComponent<Image>();
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
            hintPick = HudTheme.T("ui_help_hint_pick", hintPick);
            hintProgress = HudTheme.T("ui_help_progress_format", hintProgress);
            seeAlsoFormat = HudTheme.T("ui_help_see_also_format", seeAlsoFormat);
            tourLabel = HudTheme.T("ui_help_tour", tourLabel);
        }
}
}
