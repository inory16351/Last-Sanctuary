using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using LastSanctuary.Help;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★★ <b>조언 카드</b> — 문명의 조언자와 같은 자리 (2026-08-24 신설).
    ///
    /// 그 상황이 <b>처음</b> 왔을 때 <see cref="HelpService"/> 가 이 카드에 항목 하나를 넘긴다.
    /// 카드는 <b>게임을 멈추고</b> 요약 두어 줄을 보여주고, 「자세히 보기」로 백과
    /// (<see cref="HelpPanel"/>)의 그 항목을 연다.
    ///
    /// ★ <b>읽는 동안 게임이 멈춘다</b>(유저 확정사항 ① · 지시 *"도움말 뜨면 게임 일시정지
    ///   되야함"*). 멈추는 방법은 <see cref="ReadingPause"/> 에 있다 — <c>timeScale</c> 을
    ///   직접 쓰지 않고 <see cref="GameSpeedPanel"/> 을 통하며, <b>내가 멈춘 것만 내가 푼다</b>.
    ///   도움말 창(<see cref="HelpPanel"/>)도 <b>같은 클래스</b>를 쓴다.
    ///
    /// ⚠ <b>배타 창(<see cref="IExclusiveHudPanel"/>)이 아니다.</b> 카드는 «화면 가운데를
    ///   차지하는 창» 이 아니라 <b>위에 덮는 한 겹</b>이다. 배타 창으로 만들면 창을 하나 열
    ///   때마다 조언이 소리 없이 사라진다 — 처음 한 번밖에 안 뜨는 글이라 그러면 영영 못 읽는다.
    ///   대신 <see cref="HelpService"/> 가 «다른 창이 열려 있으면 기다린다» 로 겹침을 막는다.
    ///
    /// ⚠ <b>어두운 막의 <c>raycastTarget</c> 을 끄지 말 것</b> — 그것이 뒤의 전장·버튼 클릭을
    ///   막는 유일한 장치다. 끄면 멈춘 화면 뒤로 손이 닿는다.
    /// </summary>
    public class HelpCardPanel : MonoBehaviour
    {
        static HelpCardPanel _instance;

        public static HelpCardPanel Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<HelpCardPanel>(FindObjectsInactive.Include);
                return _instance;
            }
        }

        [Header("연출")]
        [Tooltip("카드가 올라오며 밝아지는 시간(초). 0 이면 연출 없이 즉시 뜬다")]
        [Min(0f)] [SerializeField] float appearSeconds = 0.24f;

        [Tooltip("연출이 시작될 때 카드가 아래로 내려가 있는 거리(px)")]
        [Min(0f)] [SerializeField] float appearRisePixels = 28f;

        [Header("문구")]
        [Tooltip("{0} = 분류 이름. 카드가 «무엇인지» 를 알려주는 머리표")]
        [SerializeField] string badgeFormat = "도움말 · {0}";

        [SerializeField] string moreLabel = "자세히 보기";
        [SerializeField] string okLabel = "알겠습니다";

        [Header("동작")]
        [Tooltip("Esc · Enter · Space 로도 닫는다. ⚠ 이 값을 끄면 버튼으로만 닫을 수 있다")]
        [SerializeField] bool closeWithKeyboard = true;

        HelpEntry _entry;

        TMP_Text _badge, _title, _summary, _moreLabelText, _okLabelText;
        Button _moreButton, _okButton;
        RectTransform _card;
        CanvasGroup _group;
        Vector2 _home;
        Coroutine _intro;
        bool _bound;

        /// <summary>읽는 동안의 일시정지. 규칙은 <see cref="ReadingPause"/> 에 있다.</summary>
        readonly ReadingPause _pause = new ReadingPause();

        void Awake()
        {
            _instance = this;
            EnsureBound();
            // ⚠⚠ 여기서 자기를 끄지 않는다 — 이 카드는 비활성으로 저장돼 있어 Awake 가
            //   «처음 열릴 때» 돈다. 그 자리에서 끄면 영영 안 뜬다(RelicPanel 의 ⚠⚠ 와 같다).
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>지금 카드가 떠 있는 항목. 없으면 null.</summary>
        public HelpEntry Current => IsOpen ? _entry : null;

        // ------------------------------------------------------------------

        /// <summary>항목 하나를 띄운다. <see cref="HelpService"/> 만 부른다.</summary>
        public void Show(HelpEntry entry)
        {
            if (entry == null) return;

            EnsureBound();
            _entry = entry;

            SetText(_badge, string.Format(badgeFormat, entry.category ?? ""));
            SetText(_title, entry.Title);
            SetText(_summary, entry.Summary);
            SetText(_moreLabelText, moreLabel);
            SetText(_okLabelText, okLabel);

            // ★★ <b>짚을 것이 없으면 「자세히 보기」를 숨긴다</b> (2026-08-24 · 유저 지시:
            //   *"단순히 성역이 파괴되면 게임이 종료된다는 간단한 규칙 같은거
            //   (다른 ui와 연결되지 않아도 되는 기능)은 그냥 자세히 보기 없어도 됨"*).
            //   ⚠ 숨기지 않으면 «눌러도 아무 일이 없는 버튼» 이 된다 — 이 프로젝트가 건설
            //     버튼에서 이미 겪은 일이다(그때는 알릴 통로가 하나도 없었다).
            //   ★ 판단은 <see cref="HelpTourPanel.HasTour"/> <b>한 곳</b>에서 한다 —
            //     백과의 「화면에서 짚어 보기」도 같은 함수를 쓴다.
            bool hasTour = HelpTourPanel.HasTour(entry);
            if (_moreButton != null && _moreButton.gameObject.activeSelf != hasTour)
                _moreButton.gameObject.SetActive(hasTour);

            // 「알겠습니다」는 짚을 것이 없으면 <b>가운데</b>로 온다 — 버튼 하나가 한쪽에
            // 치우쳐 있으면 «옆 버튼이 사라졌다» 로 보인다.
            if (_okButton != null)
                CenterOk(!hasTour);

            gameObject.SetActive(true);
            transform.SetAsLastSibling();   // 같은 캔버스 안에서 맨 앞으로

            _pause.Acquire();
            PlayAppear();
        }

        /// <summary>
        /// 카드를 닫고 <b>내가 멈춘 것이면</b> 다시 흐르게 한다.
        ///
        /// ★★ 「알겠습니다」와 키보드가 부르는 <b>«다 읽었다» 길</b>이다 — 그래서
        ///   가로챈 버튼이 원래 하려던 일을 여기서 마무리한다
        ///   (<see cref="HelpService.CompletePending"/> · 2026-08-25).
        ///   ⚠ 안내로 넘어갈 때는 이 길이 아니다 — <see cref="CloseSilently"/> 를 쓴다.
        ///     여기서 마무리해 버리면 창이 <b>안내보다 먼저</b> 열려 소유권이 어긋난다.
        /// </summary>
        public void Close()
        {
            CloseSilently();
            HelpService.Instance?.CompletePending();
        }

        /// <summary>
        /// 카드만 닫는다 — <b>가로챈 일은 그대로 둔다</b>. 「자세히 보기」로 안내에 넘길 때 쓴다.
        /// </summary>
        public void CloseSilently()
        {
            _pause.Release();
            Settle();                       // 연출 중에 닫혀도 다음에 제자리에서 시작하게
            gameObject.SetActive(false);
            _entry = null;
        }

        /// <summary>
        /// ★★ 「자세히 보기」 — <b>실제 화면을 짚어 준다</b> (2026-08-24 · 유저 지시:
        /// *"자세히 보기에서 실제 ui로 연결하고 빨간 테두리 선으로 하나하나 설명해주는 기능"*).
        ///
        /// <code>
        ///   표에 짚어 줄 단계가 있으면  →  빨간 테두리 안내(<see cref="HelpTourPanel"/>)
        ///   없으면                    →  백과의 그 항목(<see cref="HelpPanel"/>)
        /// </code>
        /// ★ <b>순서가 이 방향인 이유</b> — 처음 하는 사람에게 필요한 것은 설명문이 아니라
        ///   «어디를 눌러야 하는가» 다. 글은 그다음이다.
        /// ⚠ 짚을 곳이 없는 항목도 있다(명중·크리티컬처럼 화면의 칸이 아닌 규칙) —
        ///   그때 <see cref="HelpTourPanel.Begin"/> 이 <c>false</c> 를 돌려주므로
        ///   <b>글로 되돌아간다</b>. 아무 일도 안 일어나는 버튼이 되지 않게 하는 장치다.
        /// </summary>
        void OpenEncyclopedia()
        {
            HelpEntry e = _entry;
            CloseSilently();                // ⚠ 가로챈 일은 <b>안내가</b> 마무리한다

            HelpTourPanel tour = HelpTourPanel.Instance;
            if (tour != null && tour.Begin(e)) return;

            // ⚠ 여기 오는 것은 <b>있을 수 없는 일</b>이다 — 버튼은 짚을 것이 있을 때만 뜬다.
            //   그래도 조용히 아무 일도 안 하지 않고 백과를 열어 준다(글은 언제나 있다).
            Debug.LogWarning($"[도움말] {e?.helpId} 의 안내를 시작하지 못했습니다 — " +
                             "표의 HelpStep 을 확인하세요. 백과를 대신 엽니다.", this);

            // ⚠ 안내가 못 떴으니 <b>마무리해 줄 사람이 없다</b> — 가로챈 것을 버린다.
            //   여기서 창을 열어 버리면 방금 띄운 백과를 <b>제가 도로 닫는다</b>
            //   (배타 창끼리는 하나만 열린다). 항목은 이미 «읽음» 이라 다음 클릭부터는
            //   버튼이 평소대로 동작한다.
            HelpService.Instance?.CancelPending();

            HelpPanel panel = HelpPanel.Instance;
            if (panel != null) panel.OpenAt(e);
        }

        void Update()
        {
            if (!closeWithKeyboard) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // ⚠ Esc 는 HudHotkeys 도 읽는다 — 그쪽이 카드에 <b>양보</b>한다(HudHotkeys 의 ★).
            if (kb.escapeKey.wasPressedThisFrame ||
                kb.enterKey.wasPressedThisFrame ||
                kb.numpadEnterKey.wasPressedThisFrame ||
                kb.spaceKey.wasPressedThisFrame)
                Close();
        }

        // ------------------------------------------------------------------
        // 연출 — 사건 창(EventPanel)과 같은 곡선
        // ------------------------------------------------------------------

        void PlayAppear()
        {
            EnsureIntro();
            if (_group == null) return;

            if (_intro != null) StopCoroutine(_intro);
            _intro = StartCoroutine(Appear());
        }

        void EnsureIntro()
        {
            if (_card == null) return;
            // ★ CanvasGroup 은 <b>코드가 붙인다</b> — 씬에 없어도 동작해야 한다(사건 창과 같은 규칙).
            if (_group == null && !_card.TryGetComponent(out _group))
                _group = _card.gameObject.AddComponent<CanvasGroup>();
            if (_home == Vector2.zero) _home = _card.anchoredPosition;
        }

        /// <summary>
        /// 아래에서 제자리로 올라오며 밝아진다. 끝에서 감속한다(<c>1 - (1-t)²</c>).
        /// ⚠ <see cref="Time.unscaledDeltaTime"/> — 카드가 뜨는 순간 게임은 이미 멈춰 있다.
        ///   <c>deltaTime</c> 을 쓰면 연출이 <b>영영</b> 멈춘다.
        /// </summary>
        IEnumerator Appear()
        {
            if (appearSeconds <= 0f || _group == null)
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
                if (_card != null)
                    _card.anchoredPosition =
                        new Vector2(_home.x, _home.y - appearRisePixels * (1f - eased));

                yield return null;
            }
            Settle();
        }

        /// <summary>연출의 마지막 프레임 — 카드를 제자리에 완전히 세운다.</summary>
        void Settle()
        {
            _intro = null;
            if (_group != null) _group.alpha = 1f;
            if (_card != null && _home != Vector2.zero) _card.anchoredPosition = _home;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// 씬 배선을 <b>이름으로</b> 찾는다 — 이 프로젝트는 MCP 로 씬을 만들고 인스펙터 참조를
        /// 넣지 못한다(진행상황 8절 4번). 다른 창들도 전부 같은 방식이다.
        /// </summary>
        void EnsureBound()
        {
            if (_bound) return;
            _bound = true;

            _card = transform.Find("Card") as RectTransform;
            if (_card == null)
            {
                Debug.LogWarning("[도움말] Card 를 찾지 못했습니다 — " +
                                 "py -3 Tools/mcp_build_help_ui.py 를 돌리세요.", this);
                return;
            }

            _badge = FindText(_card, "Badge");
            _title = FindText(_card, "Title");
            _summary = FindText(_card, "Summary");

            _moreButton = _card.Find("MoreButton")?.GetComponent<Button>();
            _okButton = _card.Find("OkButton")?.GetComponent<Button>();
            _moreLabelText = FindText(_card, "MoreButton/Label");
            _okLabelText = FindText(_card, "OkButton/Label");

            if (_moreButton != null)
            {
                _moreButton.onClick.RemoveAllListeners();
                _moreButton.onClick.AddListener(OpenEncyclopedia);
            }
            if (_okButton != null)
            {
                _okButton.onClick.RemoveAllListeners();
                _okButton.onClick.AddListener(Close);
            }

            // ⚠ 넘침 방지는 <b>코드가</b> 한다 — TMP 의 줄바꿈·자동 크기 칸은 MCP 로 못 넘긴다
            //   (HudTheme.FitText 의 ⚠). 요약은 두 줄짜리라 넉넉하지만 43자 줄이 있어 필요하다.
            HudTheme.FitText(_badge, 11f, wrap: false);
            HudTheme.FitText(_title, 18f, wrap: false);
            HudTheme.FitText(_summary, 13f);
            HudTheme.FitText(_moreLabelText, 12f, wrap: false);
            HudTheme.FitText(_okLabelText, 12f, wrap: false);

            EnsureIntro();
        }

        /// <summary>
        /// 「알겠습니다」를 가운데로 옮기거나 제자리(오른쪽)로 되돌린다.
        ///
        /// ⚠ 씬의 값을 <b>기억해 두고</b> 되돌린다 — 여기서 좌표를 새로 지어내면 씬을
        ///   MCP 로 다시 구울 때 두 곳의 값이 갈린다(이 프로젝트가 반복해 밟은 함정이다).
        /// </summary>
        void CenterOk(bool center)
        {
            var rt = _okButton.transform as RectTransform;
            if (rt == null) return;

            if (!_okHomeSaved)
            {
                _okHomeSaved = true;
                _okAnchorMin = rt.anchorMin;
                _okAnchorMax = rt.anchorMax;
                _okOffsetMin = rt.offsetMin;
                _okOffsetMax = rt.offsetMax;
            }

            if (!center)
            {
                rt.anchorMin = _okAnchorMin;
                rt.anchorMax = _okAnchorMax;
                rt.offsetMin = _okOffsetMin;
                rt.offsetMax = _okOffsetMax;
                return;
            }

            float w = _okOffsetMax.x - _okOffsetMin.x;   // 제자리에서의 너비를 그대로 쓴다
            float h = _okOffsetMax.y - _okOffsetMin.y;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(-w * 0.5f, _okOffsetMin.y);
            rt.offsetMax = new Vector2(w * 0.5f, _okOffsetMin.y + h);
        }

        bool _okHomeSaved;
        Vector2 _okAnchorMin, _okAnchorMax, _okOffsetMin, _okOffsetMax;

        static void SetText(TMP_Text t, string value)
        {
            if (t != null) t.text = value ?? "";
        }

        static TMP_Text FindText(Transform parent, string path)
        {
            Transform t = parent.Find(path);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }
    }
}
