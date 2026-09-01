using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Data;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★★ <b>언어를 고르는 스크롤 목록</b> (2026-09-01 신설 · 유저 지시:
    /// *"… 스크롤바 언어설정에 넣어서 언어 추가해줘"*).
    ///
    /// <b>왜 목록인가</b> — 예전에는 «누르면 다음 언어» 였다. 언어가 <b>둘</b>일 때는
    /// 그것이 가장 적은 조작이었지만(<see cref="LanguageSetting"/> 의 옛 주석),
    /// <b>아홉</b>이 되면 정반대가 된다: 폴란드어를 고르려면 여덟 번 눌러야 하고
    /// 한 번 지나치면 <b>여덟 번을 더</b> 눌러야 한다.
    ///
    /// ★ <b>씬을 고치지 않는다</b> — 창을 통째로 코드가 만든다
    ///   (<see cref="NexusHealthBar"/> · <c>ShieldOverlayFx</c> 와 같은 규칙).
    ///   설정 창이 게임에도 로비에도 있고 <b>둘 다 씬이 다르므로</b>, 프리팹을 만들면
    ///   두 씬에 각각 붙여야 하고 한쪽만 갱신되는 사고가 난다.
    ///
    /// ★ <b>부모 캔버스를 타고 올라간다</b> — 어느 창이 열었든 그 창의 캔버스에 붙는다.
    ///   그래야 해상도 대응(<c>CanvasScaler</c>)과 정렬이 저절로 맞는다.
    ///
    /// ⚠ <b>언어 이름은 번역하지 않는다</b>(<see cref="LanguageSetting.NameOf"/>).
    ///   목록의 아홉 줄이 «지금 언어» 로 전부 번역되면 자기 언어를 찾을 수가 없다.
    ///
    /// ⚠ <b>바깥을 눌러 닫는 판</b>을 뒤에 깐다. 그것이 없으면 목록 밖을 눌렀을 때
    ///   뒤에 있는 설정 창의 버튼이 눌린다 — 사건 창이 밟았던 함정과 같은 종류다
    ///   (진행상황 198-2절).
    /// </summary>
    [DisallowMultipleComponent]
    public class LanguagePickerPopup : MonoBehaviour
    {
        // ── 크기 (px · 캔버스 기준) ─────────────────────────────────────
        const float PanelWidth = 320f;
        const float PanelHeight = 380f;
        const float RowHeight = 46f;
        const float RowGap = 4f;
        const float Pad = 12f;
        const float TitleHeight = 34f;
        const float ScrollbarWidth = 10f;

        /// <summary>지금 떠 있는 창. 두 개가 겹치지 않게 하나만 둔다.</summary>
        static LanguagePickerPopup _open;

        System.Action _onPicked;
        readonly List<(GameLanguage lang, Image bg, TMP_Text label)> _rows = new();

        /// <summary>
        /// 목록을 띄운다. 이미 떠 있으면 <b>닫는다</b> — 같은 버튼을 다시 누른 것이므로
        /// «토글» 이 자연스럽다(배속·미니맵 창과 같은 결).
        /// </summary>
        /// <param name="anchor">이 창을 띄운 버튼. 캔버스를 찾는 데만 쓴다.</param>
        /// <param name="onPicked">고른 뒤 부를 것 — 부른 쪽이 자기 라벨을 다시 그린다.</param>
        public static void Open(Component anchor, System.Action onPicked)
        {
            if (_open != null)
            {
                _open.Close();
                return;
            }
            if (anchor == null) return;

            Canvas canvas = anchor.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var go = new GameObject("LanguagePicker", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var popup = go.AddComponent<LanguagePickerPopup>();
            popup._onPicked = onPicked;
            popup.Build();
            _open = popup;
        }

        /// <summary>지금 떠 있으면 닫는다. 창이 사라질 때 부르는 쪽에서도 쓴다.</summary>
        public static void CloseIfOpen()
        {
            if (_open != null) _open.Close();
        }

        void Close()
        {
            if (_open == this) _open = null;
            if (this != null && gameObject != null) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_open == this) _open = null;
        }

        // ------------------------------------------------------------------
        // 만들기
        // ------------------------------------------------------------------

        void Build()
        {
            var self = (RectTransform)transform;
            Stretch(self);
            // 뒤에 있는 창보다 앞이어야 한다 — 형제 중 <b>맨 마지막</b>이 맨 앞이다.
            self.SetAsLastSibling();

            BuildBlocker(self);
            RectTransform panel = BuildPanel(self);
            BuildTitle(panel);
            BuildList(panel);
        }

        /// <summary>바깥을 눌러 닫는 투명 판. 뒤 창의 버튼이 눌리는 것을 막는다.</summary>
        void BuildBlocker(RectTransform parent)
        {
            var go = new GameObject("Blocker", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);

            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.45f);   // ⚠ 완전 투명이면 클릭을 안 먹는다

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Close);
        }

        RectTransform BuildPanel(RectTransform parent)
        {
            var go = new GameObject("Panel", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            var img = go.AddComponent<Image>();
            img.color = HudTheme.PanelBg;

            // ⚠ 판 자신도 클릭을 먹어야 한다 — 안 그러면 목록 사이 빈틈을 눌렀을 때
            //   뒤의 Blocker 가 받아 창이 닫힌다.
            go.AddComponent<Button>().transition = Selectable.Transition.None;
            return rt;
        }

        void BuildTitle(RectTransform panel)
        {
            var go = new GameObject("Title", typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(Pad, 0f);
            rt.offsetMax = new Vector2(-Pad, -Pad);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, TitleHeight);

            var text = go.AddComponent<TextMeshProUGUI>();
            ApplyFont(text);
            // ★ 제목은 번역한다 — 목록의 «언어 이름» 과 달리 이것은 보통의 UI 문구다.
            text.text = StringTable.Get("ui_settings_language", "언어 / Language");
            text.fontSize = 20f;
            text.color = HudTheme.TextMain;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }

        /// <summary>
        /// 스크롤 영역 — <c>ScrollRect</c> + <c>Viewport(Mask)</c> + <c>Content</c> + 세로 스크롤바.
        /// <c>BattleLogPanel</c> · <c>CharacterRosterPanel</c> 이 씬에서 쓰는 것과 같은 구성이고,
        /// 여기서는 그것을 코드로 세운다.
        /// </summary>
        void BuildList(RectTransform panel)
        {
            // ── ScrollRect ──
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform));
            scrollGo.transform.SetParent(panel, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(Pad, Pad);
            scrollRt.offsetMax = new Vector2(-Pad, -(Pad + TitleHeight + 6f));

            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            // ── Viewport (Mask) ──
            var viewGo = new GameObject("Viewport", typeof(RectTransform));
            viewGo.transform.SetParent(scrollRt, false);
            var viewRt = (RectTransform)viewGo.transform;
            viewRt.anchorMin = Vector2.zero;
            viewRt.anchorMax = Vector2.one;
            viewRt.offsetMin = Vector2.zero;
            viewRt.offsetMax = new Vector2(-(ScrollbarWidth + 4f), 0f);
            viewRt.pivot = new Vector2(0f, 1f);

            // ⚠ Mask 는 Graphic 이 있어야 자른다 — 투명한 Image 를 깐다.
            var viewImg = viewGo.AddComponent<Image>();
            viewImg.color = new Color(1f, 1f, 1f, 0.004f);
            viewGo.AddComponent<Mask>().showMaskGraphic = false;
            scroll.viewport = viewRt;

            // ── Content ──
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewRt, false);
            var contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            scroll.content = contentRt;

            var langs = LanguageSetting.All;
            float y = 0f;
            for (int i = 0; i < langs.Length; i++)
            {
                BuildRow(contentRt, langs[i], y);
                y -= RowHeight + RowGap;
            }
            contentRt.sizeDelta = new Vector2(0f, langs.Length * (RowHeight + RowGap) + RowGap);

            BuildScrollbar(scrollRt, scroll);
            RefreshRows();

            // ★ 지금 언어가 목록 밖에 있으면 보이도록 스크롤을 맞춘다 —
            //   아홉 줄 중 아래쪽 언어를 쓰고 있으면 창을 열자마자 «내 언어» 가 안 보인다.
            int at = System.Array.IndexOf(langs, StringTable.Language);
            if (at >= 0 && langs.Length > 1)
                scroll.verticalNormalizedPosition = 1f - (float)at / (langs.Length - 1);
        }

        void BuildScrollbar(RectTransform parent, ScrollRect scroll)
        {
            var go = new GameObject("Scrollbar", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(ScrollbarWidth, 0f);
            rt.anchoredPosition = Vector2.zero;

            var back = go.AddComponent<Image>();
            back.color = HudTheme.PanelBgSoft;

            var bar = go.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;

            var handleArea = new GameObject("SlidingArea", typeof(RectTransform));
            handleArea.transform.SetParent(rt, false);
            Stretch((RectTransform)handleArea.transform);

            var handleGo = new GameObject("Handle", typeof(RectTransform));
            handleGo.transform.SetParent(handleArea.transform, false);
            var handleRt = (RectTransform)handleGo.transform;
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.offsetMin = Vector2.zero;
            handleRt.offsetMax = Vector2.zero;

            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = HudTheme.ButtonHover;

            bar.handleRect = handleRt;
            bar.targetGraphic = handleImg;

            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        }

        void BuildRow(RectTransform content, GameLanguage lang, float y)
        {
            var go = new GameObject("Row_" + lang, typeof(RectTransform));
            go.transform.SetParent(content, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(0f, y - RowGap);
            rt.sizeDelta = new Vector2(0f, RowHeight);

            var bg = go.AddComponent<Image>();
            bg.color = HudTheme.RowBg;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            GameLanguage captured = lang;
            btn.onClick.AddListener(() => Pick(captured));

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rt, false);
            Stretch((RectTransform)labelGo.transform);

            var text = labelGo.AddComponent<TextMeshProUGUI>();
            ApplyFont(text);
            text.text = LanguageSetting.NameOf(lang);
            text.fontSize = 20f;
            text.color = HudTheme.TextMain;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            _rows.Add((lang, bg, text));
        }

        // ------------------------------------------------------------------

        void Pick(GameLanguage lang)
        {
            LanguageSetting.Select(lang);
            RefreshRows();
            _onPicked?.Invoke();
            Close();          // 고르면 닫는다 — 한 번에 하나만 고르는 목록이다
        }

        /// <summary>지금 켜진 언어를 강조한다.</summary>
        void RefreshRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                bool on = _rows[i].lang == StringTable.Language;
                if (_rows[i].bg != null)
                    _rows[i].bg.color = on ? HudTheme.RowBgOn : HudTheme.RowBg;
                if (_rows[i].label != null)
                    _rows[i].label.color = on ? HudTheme.TextAccent : HudTheme.TextMain;
            }
        }

        void Update()
        {
            // Esc 로 닫는다 — 사건 창과 달리 이 목록은 «갇히면 안 되는» 창이 아니다.
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        /// <summary>
        /// ★★★ <b>코드로 만든 TMP 칸에는 폰트를 «직접» 넣어야 한다</b>
        /// (2026-09-01 · 유저 리포트: *"언어 선택에서 한국어랑 일본어 네모로 표시되는거 고쳐줘"*).
        ///
        /// ══════════════════════════════════════════════════════════════
        /// <b>왜 하필 그 둘만 네모였나 — 증상이 원인을 정확히 가리킨다</b>
        /// ══════════════════════════════════════════════════════════════
        /// <c>AddComponent&lt;TextMeshProUGUI&gt;()</c> 는 폰트를 안 정해주면
        /// <see cref="TMP_Settings.defaultFontAsset"/>(=<c>LiberationSans SDF</c>)로 그린다.
        /// 그 폰트의 커버리지가 곧 <b>어느 줄이 살고 어느 줄이 죽는가</b>였다:
        ///
        /// <list type="bullet">
        /// <item>라틴 · 키릴 — 있다 → English · Español · Français · Deutsch ·
        ///       Português · Polski · <b>Русский 까지 멀쩡히 나왔다</b></item>
        /// <item>한글 · 가나 · 한자 — <b>0자</b> → 한국어 · 日本語 <b>두 줄만</b> □</item>
        /// </list>
        ///
        /// ⚠ <b>«한글이 안 나온다» 를 폰트 폴백 문제로 오해하기 쉽다.</b> 폴백은
        ///   <see cref="HudTheme.Font"/>(네오둥근모)에 걸려 있는데, 이 창은 <b>그 폰트를
        ///   아예 쓰고 있지 않았다.</b> 폴백을 아무리 고쳐도 안 고쳐지는 자리였다 —
        ///   실제로 일본어 폴백을 굽고 연결한 <b>뒤에도</b> 네모가 남아 있었다.
        ///
        /// ★ <b>씬에 있는 창은 왜 멀쩡했나</b> — 인스펙터에서 폰트를 이미 물려 놨기 때문이다.
        ///   <b>코드로 세우는 창만</b> 이 함정을 밟는다(<c>HudTheme.Font</c> 가 존재하는 이유).
        ///
        /// ⚠ 폰트를 못 찾아도 <b>죽이지 않는다</b> — <see cref="HudTheme.Font"/> 가 이미
        ///   에러 로그를 한 번 찍고 기본 폰트로 떨어진다. 여기서 또 찍으면 아홉 줄마다
        ///   같은 로그가 아홉 번 쌓인다.
        /// </summary>
        static void ApplyFont(TMP_Text text)
        {
            TMP_FontAsset font = HudTheme.Font;
            if (font != null) text.font = font;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
