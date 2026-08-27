using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Relics;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★★ <b>발굴 확인 창</b> (2026-08-24 신설 · 유저 지시).
    ///
    /// <i>"유물 자동 발굴 되게 하지말고 유물이 발견된 칸에 별도의 ui로 버튼을 생성하고 …
    /// 해당 칸을 누를 경우 발굴 ui가 나와서 발굴하기를 누르면 가장 가까운 캐릭터가 가서
    /// 발굴하게 해줘. 이벤트 ui처럼 «위험이 도사리고 있을지도 모릅니다....» yes: 가까이 가서
    /// 살펴본다. no: 방심은 금물이다. 그냥 두자."</i>
    ///
    /// <b>왜 창이 생겼나</b> — Ver01 은 표식을 누르면 <b>곧바로</b> 발굴 지시가 나갔다.
    /// 그래서 «잘못 눌렀다» 를 되돌릴 수 없었고, 무엇보다 <b>발굴이 도박이라는 것</b>이
    /// 화면 어디에도 없었다(파면 다칠 수도 있다 — 표 <c>DigOutcome</c> 의 <c>dig_hurt</c>).
    ///
    /// <b>세 모습을 한 창이 낸다</b> — 이벤트 창(<see cref="EventPanel"/>)과 같은 방식이다.
    /// <code>
    ///   ① 발견   discover 대사 + 선택지 두 개(파러 간다 / 그냥 둔다)
    ///   ② 답변   accept · decline 대사 + 「확인」
    ///   ③ 결과   result 대사 + 발굴 결과(DigOutcome) + 「확인」
    ///   (보스 드랍도 ③ 과 같은 모습을 쓴다 — boss_drop 대사 + 얻은 유물)
    /// </code>
    /// 창을 셋 만들지 않은 이유도 이벤트 창과 같다 — 같은 자리에 같은 크기로 떠야 하고,
    /// 흐름이 «묻고 → 답하고 → 결과» 로 한 줄이다.
    ///
    /// ⚠ <b>이 창은 게임을 멈추지 않는다.</b> 발굴은 전투 중에도 일어나므로 시간을 세우면
    ///   «창 하나 때문에 웨이브가 멈춘다» 가 된다. 대신 <see cref="HudExclusive"/> 로
    ///   다른 창과 배타다.
    ///
    /// ⚠ <b>대사는 표가 정본이다</b>(<see cref="RelicDialogueTableSO"/>) — 여기서 문구를
    ///   지어내지 않는다. 표가 비면 <c>Fallback</c> 한 줄이 뜬다(그것이 «표가 없다» 는 신호다).
    /// </summary>
    public class RelicDigPanel : MonoBehaviour, IExclusiveHudPanel
    {
        static RelicDigPanel _instance;

        public static RelicDigPanel Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<RelicDigPanel>(FindObjectsInactive.Include);
                return _instance;
            }
        }

        [Header("등장 연출 (이벤트 창과 같은 값)")]
        [Min(0f)] [SerializeField] float appearSeconds = 0.28f;
        [Min(0f)] [SerializeField] float appearRisePixels = 36f;

        [Header("문구 — 표에 값이 없을 때만 쓴다")]
        [SerializeField] string titleDiscover = "발굴 가능한 자리";
        [SerializeField] string titleResult = "발굴 결과";
        [SerializeField] string titleBossDrop = "빼앗은 것";
        [SerializeField] string confirmLabel = "확인";
        [SerializeField] string fallbackAccept = "가까이 가서 살펴본다.";
        [SerializeField] string fallbackDecline = "방심은 금물이다. 그냥 두자.";

        TMP_Text _title, _body, _choice0Label, _choice1Label, _confirmLabel;
        Button _choice0, _choice1, _confirm, _close;
        Image _icon;

        CanvasGroup _group;
        RectTransform _rect;
        Vector2 _home;
        bool _homeKnown;
        Coroutine _intro;
        bool _bound;

        /// <summary>지금 묻고 있는 자리. 결과 단계·보스 드랍에서는 null 이다.</summary>
        DigSite _site;

        // ------------------------------------------------------------------

        void Awake()
        {
            LocalizeLabels();
            // ★★★ 2026-08-27 — 언어가 바뀌면 문구를 다시 받아 온다(다음에 뜨는 창부터 따라온다).
            Data.StringTable.OnLanguageChanged -= LocalizeLabels;
            Data.StringTable.OnLanguageChanged += LocalizeLabels;
            _instance = this;
            Bind();
            // ⚠ 여기서 자기를 끄지 않는다 — 이 창은 비활성으로 저장돼 있어 Awake 가
            //   «처음 열릴 때» 돈다. 그 자리에서 끄면 영영 안 뜬다(EventPanel 의 ⚠⚠).
        }

        void OnDestroy()
        {
            // ⚠ 정적 이벤트라 끊지 않으면 죽은 오브젝트가 구독에 남는다(SettingsPanel 의 그 ⚠).
            Data.StringTable.OnLanguageChanged -= LocalizeLabels;
            if (_instance == this) _instance = null;
        }

        void Bind()
        {
            if (_bound) return;
            _bound = true;

            _title = Find("Title");
            _body = Find("Body");
            _icon = transform.Find("Icon")?.GetComponent<Image>();

            _choice0 = FindButton("Choice0");
            _choice1 = FindButton("Choice1");
            _confirm = FindButton("ConfirmButton");
            _close = FindButton("CloseButton");

            _choice0Label = Find("Choice0/Label");
            _choice1Label = Find("Choice1/Label");
            _confirmLabel = Find("ConfirmButton/Label");

            if (_confirm != null) _confirm.onClick.AddListener(Close);
            if (_close != null) _close.onClick.AddListener(Close);

            // ★★ <b>글자가 칸을 넘지 않게</b> (2026-08-24 · 유저 지시:
            //   *"텍스트가 짤리지 않도록"*). 이 창의 본문은 <b>길이를 미리 알 수 없다</b> —
            //   발견 대사 한 벌(두 줄)일 때도 있고, 결과 대사 + 발굴 결과 문구가 빈 줄을
            //   사이에 두고 붙을 때도 있다(<see cref="Join"/>). 그래서 <b>가장 긴 경우에
            //   칸을 맞추는 대신</b> 글자 쪽이 줄어들게 한다.
            // ⚠ 선택지 버튼 문구는 <b>한 줄</b>이다 — 「방심은 금물이다. 그냥 두자.」처럼
            //   긴 문구도 줄바꿈 없이 글자가 줄어 들어가야 버튼 높이(44px)를 안 넘는다.
            HudTheme.FitText(_title, 15f, wrap: false);
            HudTheme.FitText(_body, 12f);
            HudTheme.FitText(_choice0Label, 11f, wrap: false);
            HudTheme.FitText(_choice1Label, 11f, wrap: false);
            HudTheme.FitText(_confirmLabel, 12f, wrap: false);
        }

        TMP_Text Find(string path) => transform.Find(path)?.GetComponent<TMP_Text>();
        Button FindButton(string path) => transform.Find(path)?.GetComponent<Button>();

        // ==================================================================
        // ① 발견 — 묻는다
        // ==================================================================

        /// <summary>
        /// 발굴 칸을 눌렀다. <paramref name="site"/> 의 <see cref="DigSite.DialogueGroup"/> 에서
        /// discover 대사와 선택지를 꺼내 보여준다.
        /// </summary>
        public void PresentSite(DigSite site, RelicDialogueTableSO table)
        {
            if (site == null) return;
            Bind();
            _site = site;

            int group = site.DialogueGroup;
            string body = table != null ? table.Roll(group, RelicDialogueSituation.Discover) : "";
            if (string.IsNullOrWhiteSpace(body))
                body = RelicDialogueTableSO.Fallback(RelicDialogueSituation.Discover);

            Show(titleDiscover, body, icon: null);

            // ── 선택지 두 개 ──
            var rows = table != null
                ? table.ChoicesOf(table.ChoiceGroupOf(group))
                : new System.Collections.Generic.List<RelicChoiceRow>();

            SetChoice(_choice0, _choice0Label, 0, rows, table);
            SetChoice(_choice1, _choice1Label, 1, rows, table);

            // 답을 고르기 전에는 「확인」이 없다 — 있으면 «답하지 않고 닫는» 길이 두 개가 된다.
            if (_confirm != null) _confirm.gameObject.SetActive(false);
        }

        void SetChoice(Button button, TMP_Text label, int index,
                       System.Collections.Generic.List<RelicChoiceRow> rows,
                       RelicDialogueTableSO table)
        {
            if (button == null) return;

            // ★ 표가 비어도 <b>둘은 뜬다</b> — 창이 답할 수 없는 상태로 남으면 안 된다.
            RelicChoiceKind kind = index == 0 ? RelicChoiceKind.Accept : RelicChoiceKind.Decline;
            string text = index == 0 ? fallbackAccept : fallbackDecline;

            if (rows != null && index < rows.Count && rows[index] != null)
            {
                kind = rows[index].kind != RelicChoiceKind.None ? rows[index].kind : kind;
                if (!string.IsNullOrWhiteSpace(rows[index].ChoiceText)) text = rows[index].ChoiceText;
            }

            button.gameObject.SetActive(true);
            if (label != null) label.text = text;

            RelicChoiceKind captured = kind;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Answer(captured, table));
        }

        // ==================================================================
        // ② 답변
        // ==================================================================

        void Answer(RelicChoiceKind kind, RelicDialogueTableSO table)
        {
            DigSite site = _site;
            if (site == null) { Close(); return; }

            var situation = kind == RelicChoiceKind.Accept
                ? RelicDialogueSituation.Accept
                : RelicDialogueSituation.Decline;

            string body = table != null ? table.Roll(site.DialogueGroup, situation) : "";
            if (string.IsNullOrWhiteSpace(body)) body = RelicDialogueTableSO.Fallback(situation);

            // ★ <b>실제 지시는 여기서 나간다</b> — 창을 열었다고 파러 가지 않는다.
            if (kind == RelicChoiceKind.Accept) RelicDigService.Instance?.Confirm(site);

            _site = null;
            ShowMessage(titleDiscover, body, null);
        }

        // ==================================================================
        // ③ 결과 · 보스 드랍
        // ==================================================================

        /// <summary>발굴이 끝났다 — result 대사 + 결과 문구.</summary>
        public void PresentResult(string flavor, string outcome, Sprite icon)
        {
            Bind();
            _site = null;
            ShowMessage(titleResult, Join(flavor, outcome), icon);
        }

        /// <summary>보스를 잡아 유물이 떨어졌다 — boss_drop 대사 + 얻은 유물.</summary>
        public void PresentBossDrop(string flavor, string gained, Sprite icon)
        {
            Bind();
            _site = null;
            ShowMessage(titleBossDrop, Join(flavor, gained), icon);
        }

        static string Join(string a, string b)
        {
            bool x = !string.IsNullOrWhiteSpace(a), y = !string.IsNullOrWhiteSpace(b);
            if (x && y) return a.Trim() + "\n\n" + b.Trim();
            return x ? a.Trim() : (y ? b.Trim() : "");
        }

        /// <summary>선택지 없이 «읽고 닫는» 모습.</summary>
        void ShowMessage(string title, string body, Sprite icon)
        {
            Show(title, body, icon);
            if (_choice0 != null) _choice0.gameObject.SetActive(false);
            if (_choice1 != null) _choice1.gameObject.SetActive(false);
            if (_confirm != null) _confirm.gameObject.SetActive(true);
            if (_confirmLabel != null) _confirmLabel.text = confirmLabel;
        }

        void Show(string title, string body, Sprite icon)
        {
            bool opening = !gameObject.activeSelf;

            HudExclusive.OpenOnly(this);
            gameObject.SetActive(true);

            if (_title != null) _title.text = title;
            if (_body != null) _body.text = body;
            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
                _icon.color = Color.white;
            }

            if (opening) PlayAppear();
        }

        // ==================================================================
        // 등장 연출 (이벤트 창과 같은 곡선)
        // ==================================================================

        void EnsureIntro()
        {
            if (_group == null && !TryGetComponent(out _group))
                _group = gameObject.AddComponent<CanvasGroup>();
            _rect ??= transform as RectTransform;
            if (!_homeKnown && _rect != null) { _home = _rect.anchoredPosition; _homeKnown = true; }
        }

        void PlayAppear()
        {
            EnsureIntro();
            if (_group == null) return;
            if (_intro != null) StopCoroutine(_intro);
            _intro = StartCoroutine(Appear());
        }

        IEnumerator Appear()
        {
            if (appearSeconds <= 0f) { Settle(); yield break; }

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

        void Settle()
        {
            _intro = null;
            if (_group != null) _group.alpha = 1f;
            if (_rect != null && _homeKnown) _rect.anchoredPosition = _home;
        }

        // ==================================================================
        // 열고 닫기
        // ==================================================================

        public bool IsOpen => gameObject.activeSelf;

        public void Close()
        {
            _site = null;
            if (_intro != null) { StopCoroutine(_intro); _intro = null; }
            Settle();
            gameObject.SetActive(false);
        }
    
        /// <summary>
        /// ★ 이 창의 문구를 <b>스트링 표</b>에서 가져온다 (2026-08-26 · 178-5절).
        /// 인스펙터 값은 <b>폴백</b>이다 — 표에 키가 없으면 화면은 지금과 같다.
        /// </summary>
        void LocalizeLabels()
        {
            titleDiscover = HudTheme.T("ui_dig_title", titleDiscover);
            titleResult = HudTheme.T("ui_dig_title_result", titleResult);
            titleBossDrop = HudTheme.T("ui_dig_title_boss", titleBossDrop);
            confirmLabel = HudTheme.T("ui_btn_confirm", confirmLabel);
            fallbackAccept = HudTheme.T("ui_dig_choice_accept", fallbackAccept);
            fallbackDecline = HudTheme.T("ui_dig_choice_decline", fallbackDecline);
        }
}
}
