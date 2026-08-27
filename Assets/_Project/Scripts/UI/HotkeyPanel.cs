using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★ <b>단축키 설정 창</b> (2026-08-25 신설 — 유저 지시: *"단축키 메뉴 허드 액션 도움말
    /// 밑에 단축키 설정 메뉴 넣고"*).
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  ★ <b>줄을 코드로 짓는다</b> — 이 창만 그렇다
    /// ══════════════════════════════════════════════════════════════════
    /// 다른 HUD 창은 씬에 배선되어 있다. 이 창은 <b>줄 수가 기능 수를 따라간다</b>
    /// (<see cref="HotkeyService.All"/>) — 기능이 하나 늘 때마다 씬에 줄을 하나 더 만들고
    /// 위치를 다시 잡아야 한다면 <b>반드시 어긋난다</b>. 그리고 MCP 로는 오브젝트를 만들 수는
    /// 있어도 <b>형제 순서를 정할 수 없어</b> 줄 순서가 뒤섞인다.
    ///
    /// 그래서 <b>루트 하나만</b> 씬에 두고(<c>UI_Root/HUD_Hotkeys</c>) 안쪽은 여기서 짓는다.
    /// <see cref="OpeningDirector"/>·<see cref="EndingDirector"/> 가 같은 판단을 한 자리다.
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  키를 «받는» 방식
    /// ══════════════════════════════════════════════════════════════════
    /// 줄의 키 버튼을 누르면 <see cref="_capturing"/> 이 켜지고, 그 다음 <b>아무 키나</b>
    /// 누르면 그 키가 들어간다.
    /// <code>
    ///   Esc  → 취소 (바꾸지 않고 빠져나온다)
    ///   Del  → 단축키 <b>없음</b> 으로 (Key.None)
    ///   그 외 → 그 키로
    /// </code>
    /// ⚠⚠ <b>받는 동안 <see cref="HudHotkeys"/> 는 아무 키도 먹지 않는다</b> — 그쪽이
    ///   <see cref="IsCapturing"/> 을 보고 비켜준다. 안 비키면 «환경 설정을 B 로» 바꾸려고
    ///   B 를 누르는 순간 건설 지정 모드가 같이 켜진다.
    ///
    /// ⚠ <b>일시정지 중에도 동작해야 한다</b> — 이 창은 배타 창이라 다른 창처럼 게임을 멈춘
    ///   상태에서 열릴 수 있다. 시간을 재지 않으므로 <c>timeScale</c> 과 무관하다.
    /// </summary>
    public class HotkeyPanel : MonoBehaviour, IExclusiveHudPanel
    {
        [Header("문구")]
        [SerializeField] string titleText = "단축키 설정";
        [SerializeField] string hintText = "키 칸을 누르고 원하는 키를 누르세요.  Esc 취소 · Del 해제";
        [SerializeField] string capturingText = "키를 누르세요…";
        [SerializeField] string resetLabel = "기본값으로";
        [SerializeField] string closeLabel = "닫기";

        // ★★ 치수 — <b>구르는 칸을 넣은 뒤로는 줄 수와 무관하다</b> (2026-08-25).
        //    예전에는 «줄 수 × 줄 높이» 로 판 높이를 손으로 맞췄고, 기능이 늘 때마다 다시
        //    계산해야 했다(9줄 → 15줄에서 실제로 한 번 겹쳤다). 이제 넘치면 굴러간다.
        [Header("치수 (1920x1080 기준)")]
        [SerializeField] Vector2 panelSize = new Vector2(470f, 520f);
        [SerializeField] float rowHeight = 34f;
        [SerializeField] float rowGap = 3f;

        [Tooltip("스크롤 막대 굵기(px)")]
        [Min(6f)] [SerializeField] float scrollbarWidth = 10f;

        [SerializeField] float keyColumnWidth = 130f;

        public static HotkeyPanel Instance { get; private set; }

        /// <summary>지금 «다음 키» 를 기다리는가. <see cref="HudHotkeys"/> 가 이걸 보고 비켜준다.</summary>
        public bool IsCapturing => _capturing.HasValue;

        public bool IsOpen => _body != null && _body.activeSelf;

        GameObject _body;
        readonly Dictionary<HotkeyAction, TMP_Text> _keyLabels =
            new Dictionary<HotkeyAction, TMP_Text>();

        // ★★ 2026-08-27 — <b>언어가 바뀌면 다시 써야 하는 글자 칸들</b> (유저 리포트:
        //   *"영어로 변경되지 않는 UI들이 있어(ex 로그, 단축키 설정 등)"*).
        //   이 창은 <see cref="Build"/> 에서 <b>한 번</b> 지어지고 그 뒤로는 <see cref="Redraw"/>
        //   가 «키 칸» 만 다시 썼다. 제목·안내·버튼 둘·<b>기능 이름 열다섯</b>은 지을 때
        //   쓴 글자가 그대로 남아 있어, 언어를 바꿔도 창을 다시 열어도 한국어였다
        //   (창을 <b>다시 짓지 않기</b> 때문이다 — <c>_built</c> 가 막는다).
        readonly Dictionary<HotkeyAction, TMP_Text> _nameLabels =
            new Dictionary<HotkeyAction, TMP_Text>();

        TMP_Text _titleLabel;
        TMP_Text _hintLabel;
        TMP_Text _resetLabelText;
        TMP_Text _closeLabelText;

        HotkeyAction? _capturing;
        bool _built;

        void Awake()
        {
            LocalizeLabels();
            Instance = this;
            Build();

            // ⚠ <b>여기서 gameObject.SetActive(false) 를 하지 않는다</b> — 루트를 끄면
            //   Awake 가 다시 안 돌아 배선이 사라진다(UnitPortraitPanel 이 겪은 함정).
            //   루트는 항상 활성으로 두고 <see cref="_body"/> 만 켜고 끈다
            //   (VictoryPanel·DefeatPanel 과 같은 방식).
            if (_body != null) _body.SetActive(false);

            HotkeyService.OnChanged += Redraw;

            // ★★★ 2026-08-27 — 언어가 바뀌면 <see cref="Relabel"/> 로 다시 쓴다.
            Data.StringTable.OnLanguageChanged -= HandleLanguageChanged;
            Data.StringTable.OnLanguageChanged += HandleLanguageChanged;
        }

        void OnDestroy()
        {
            HotkeyService.OnChanged -= Redraw;
            // ⚠ 정적 이벤트라 끊지 않으면 죽은 오브젝트가 구독에 남는다(SettingsPanel 의 그 ⚠).
            Data.StringTable.OnLanguageChanged -= HandleLanguageChanged;
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// ★★★ <b>언어가 바뀌면 이 창의 글자를 전부 다시 쓴다</b> (2026-08-27).
        ///
        /// ⚠ <b>다시 «짓지» 않는다</b> — <see cref="Build"/> 는 <see cref="_built"/> 가 막고,
        ///   다시 지으면 지금 구르고 있는 위치와 «키 잡는 중» 상태가 날아간다. 글자만 갈아 끼운다.
        /// </summary>
        void HandleLanguageChanged()
        {
            LocalizeLabels();
            Relabel();
            Redraw();          // 키 칸의 «없음» 도 표를 거치므로 함께 다시 쓴다
        }

        /// <summary>지어 둔 글자 칸에 <b>지금 언어</b>의 문구를 다시 적는다.</summary>
        void Relabel()
        {
            if (_titleLabel != null) _titleLabel.text = titleText;
            if (_hintLabel != null) _hintLabel.text = hintText;
            if (_resetLabelText != null) _resetLabelText.text = resetLabel;
            if (_closeLabelText != null) _closeLabelText.text = closeLabel;

            foreach (var kv in _nameLabels)
                if (kv.Value != null) kv.Value.text = HotkeyService.Label(kv.Key);
        }

        void Update()
        {
            if (!_capturing.HasValue) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // 눌린 키를 하나 찾는다. anyKey 는 «눌렸는가» 만 알려 주므로 목록을 훑는다.
            foreach (KeyControl control in kb.allKeys)
            {
                if (!control.wasPressedThisFrame) continue;

                HotkeyAction action = _capturing.Value;
                _capturing = null;

                if (control.keyCode == Key.Escape)      // 취소
                {
                    Redraw();
                    return;
                }

                Key chosen = control.keyCode == Key.Delete ? Key.None : control.keyCode;
                HotkeyAction? stolen = HotkeyService.Set(action, chosen);

                // ★ 빼앗은 기능을 <b>말해 준다</b> — 조용히 지우면 «저쪽 단축키가 왜 사라졌지» 가 된다.
                // ⚠ <b>조각을 이어 붙이지 않는다</b> — 자리표 셋짜리 «형식 하나»를 표에서
                //   가져온다. 영어는 어순이 달라(「A 의 키를 B 가 가져갔다」 ↔ 「B took … from A」)
                //   조각으로 나누면 옮길 방법이 없다(173-6절·179-2절의 그 규칙).
                if (stolen.HasValue)
                    HudLog.Add(string.Format(
                                   HudTheme.T("log_hotkey_stolen",
                                              "「{0}」의 단축키가 해제되었습니다 — {1} 를 「{2}」이 가져갔습니다"),
                                   HotkeyService.Label(stolen.Value),
                                   HotkeyService.KeyLabel(chosen),
                                   HotkeyService.Label(action)),
                               HudLogKind.Warn);
                else
                    HudLog.Add(string.Format(
                                   HudTheme.T("log_hotkey_assigned", "「{0}」 단축키 → {1}"),
                                   HotkeyService.Label(action),
                                   HotkeyService.KeyLabel(chosen)),
                               HudLogKind.Good);

                Redraw();
                return;
            }
        }

        // ------------------------------------------------------------------

        public void Toggle() => SetOpen(!IsOpen);

        /// <summary>
        /// ⚠ <see cref="HudExclusive.OpenOnly"/> 는 <b>다른 창을 닫고 이 창을 맨 앞으로 올릴</b>
        /// 뿐이고 <b>열어 주지는 않는다</b> — 여는 것은 각 창이 스스로 한다(그쪽 doc:
        /// *"창의 SetOpen(true) 맨 앞에서 부르면 된다"*). 그래서 순서가 «먼저 정리, 그다음 열기» 다.
        /// </summary>
        public void SetOpen(bool open)
        {
            if (!open) { Close(); return; }

            HudExclusive.OpenOnly(this);
            Open();
        }

        /// <summary>배타 조정자가 부른다. ⚠ 닫을 때 «키 받는 중» 도 반드시 끈다.</summary>
        public void Close()
        {
            _capturing = null;
            if (_body != null && _body.activeSelf) _body.SetActive(false);
        }

        /// <summary>배타 조정자가 «이 창만 열어라» 로 부르는 경로.</summary>
        public void Open()
        {
            Build();
            Redraw();
            if (_body != null && !_body.activeSelf) _body.SetActive(true);
        }

        // ------------------------------------------------------------------

        void Redraw()
        {
            foreach (var kv in _keyLabels)
            {
                if (kv.Value == null) continue;
                bool capturing = _capturing.HasValue && _capturing.Value == kv.Key;
                kv.Value.text = capturing
                    ? capturingText
                    : HotkeyService.KeyLabel(HotkeyService.Get(kv.Key));
                kv.Value.color = capturing ? HudTheme.TextAccent : HudTheme.TextMain;
            }
        }

        // ── 짓기 ──────────────────────────────────────────────────────────

        void Build()
        {
            if (_built) return;
            _built = true;

            RectTransform root = GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            Stretch(root);

            // Body — 화면 전체를 덮는 반투명 막 + 가운데 판. 막이 뒤쪽 클릭을 가로막는다.
            RectTransform body = NewRect("Body", root);
            Stretch(body);
            _body = body.gameObject;
            var scrim = body.gameObject.AddComponent<Image>();
            scrim.color = new Color(0f, 0f, 0f, 0.55f);
            scrim.raycastTarget = true;

            RectTransform panel = NewRect("Panel", body);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = panelSize;
            var panelBg = panel.gameObject.AddComponent<Image>();
            // ★★ <b>다른 창과 같은 그림을 쓴다</b> (2026-08-25).
            //   씬에 배선된 창들은 에디터의 «LastSanctuary/UI/배선» 이 <c>Win_Frame</c> 을
            //   깔아 준다. 이 창은 <b>런타임에 지어지므로</b> 그 적용기가 닿지 못한다 —
            //   그래서 여기서 직접 읽는다. 못 찾으면 단색으로 떨어진다(로비가 아닌 씬 등).
            Sprite frame = Resources.Load<Sprite>("UI/Frames/Win_Frame");
            if (frame != null)
            {
                panelBg.sprite = frame;
                panelBg.type = Image.Type.Sliced;
                panelBg.color = Color.white;
            }
            else panelBg.color = HudTheme.PanelBg;

            // 제목
            RectTransform title = NewRect("Title", panel);
            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = new Vector2(1f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.sizeDelta = new Vector2(-40f, 40f);
            title.anchoredPosition = new Vector2(0f, -14f);
            _titleLabel = Label(title, titleText, 24f, TextAlignmentOptions.Left, HudTheme.TextMain);

            // 안내
            RectTransform hint = NewRect("Hint", panel);
            hint.anchorMin = new Vector2(0f, 1f);
            hint.anchorMax = new Vector2(1f, 1f);
            hint.pivot = new Vector2(0.5f, 1f);
            hint.sizeDelta = new Vector2(-40f, 34f);
            hint.anchoredPosition = new Vector2(0f, -54f);
            _hintLabel = Label(hint, hintText, 14f,
                               TextAlignmentOptions.TopLeft, HudTheme.TextDim);
            _hintLabel.textWrappingMode = TextWrappingModes.Normal;

            // ★★★ <b>줄은 «구르는 칸» 안에 넣는다</b> (2026-08-25 · 유저 지시:
            //   *"단축키 설정에 스크롤 바 넣어주고"*).
            //
            // <b>왜</b> — 줄 수가 기능 수를 따라간다. 오늘 하루에 9줄 → 15줄이 되었고,
            // 그때마다 판 높이를 손으로 다시 계산했다(그리고 한 번 겹쳤다). 구르게 하면
            // <b>줄이 늘어도 판이 그대로</b>다 — 계산이 필요 없어진다.
            //
            // ★ <c>ScrollRect</c> 를 손으로 짓는다: 액자(Viewport + RectMask2D)와 그 안의
            //   내용 칸(Content). 내용 칸의 높이를 줄 수로 정하면 스크롤 범위가 저절로 잡힌다.
            // ⚠ 막대(Scrollbar)는 <b>액자 밖</b>에 둔다 — 안에 넣으면 마스크에 잘린다.
            RectTransform viewport = NewRect("Viewport", panel);
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(20f, 66f);          // 아래 버튼 자리를 비운다
            viewport.offsetMax = new Vector2(-20f - scrollbarWidth - 6f, -92f);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            int rows = HotkeyService.All.Length;
            float contentHeight = rows * (rowHeight + rowGap) - rowGap;
            content.sizeDelta = new Vector2(0f, contentHeight);

            float y = 0f;
            for (int i = 0; i < rows; i++)
            {
                BuildRow(content, HotkeyService.All[i], y);
                y -= rowHeight + rowGap;
            }

            // 막대 — 액자 오른쪽
            RectTransform barRect = NewRect("Scrollbar", panel);
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.offsetMin = new Vector2(-20f - scrollbarWidth, 66f);
            barRect.offsetMax = new Vector2(-20f, -92f);

            var barBg = barRect.gameObject.AddComponent<Image>();
            barBg.color = HudTheme.PanelBgSoft;

            RectTransform handleRect = NewRect("Handle", barRect);
            Stretch(handleRect);
            var handleImage = handleRect.gameObject.AddComponent<Image>();
            handleImage.color = HudTheme.RowBgOn;

            var bar = barRect.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handleRect;
            bar.targetGraphic = handleImage;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            // ⚠ <b>액자가 클릭을 받아야 굴러간다</b> — 투명해도 <c>Image</c> 가 있어야
            //   휠·드래그가 이 칸에 닿는다. 알파 0 으로 두어 보이지는 않게 한다.
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;

            // 아래 버튼 둘
            _resetLabelText = BuildButton(panel, "ResetButton", resetLabel,
                        new Vector2(20f, 18f), new Vector2(0f, 0f), new Vector2(150f, 38f),
                        () =>
                        {
                            HotkeyService.ResetAll();
                            HudLog.Add(HudTheme.T("log_hotkey_reset_all",
                                                  "단축키를 기본값으로 되돌렸습니다"),
                                       HudLogKind.Good);
                        });

            _closeLabelText = BuildButton(panel, "CloseButton", closeLabel,
                        new Vector2(-20f, 18f), new Vector2(1f, 0f), new Vector2(110f, 38f),
                        Close);
        }

        /// <summary>줄 하나. <paramref name="parent"/> 는 구르는 칸(<c>Content</c>)이다.</summary>
        void BuildRow(RectTransform parent, HotkeyAction action, float y)
        {
            RectTransform row = NewRect("Row_" + action, parent);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            // ⚠ 폭 보정을 0 으로 둔다 — 예전에는 판에 직접 붙어서 −40 이 필요했지만
            //   지금은 <c>Content</c> 안이고 그 칸이 이미 여백을 갖고 있다.
            row.sizeDelta = new Vector2(0f, rowHeight);
            row.anchoredPosition = new Vector2(0f, y);

            // ⚠ 줄(행)에는 <b>그림을 깔지 않는다</b> — 적용기가 목록의 «행» 을 일부러 건너뛰는
            //   것과 같은 이유다(UiSkinApplier 의 ⚠): 행은 색으로 상태를 보여주는데 그림을
            //   깔면 그 색이 그림에 곱해져 안 보인다.
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = HudTheme.RowBg;
            bg.raycastTarget = false;

            // 기능 이름 — 왼쪽
            RectTransform name = NewRect("Name", row);
            name.anchorMin = new Vector2(0f, 0f);
            name.anchorMax = new Vector2(1f, 1f);
            name.offsetMin = new Vector2(12f, 0f);
            name.offsetMax = new Vector2(-(keyColumnWidth + 8f), 0f);
            _nameLabels[action] = Label(name, HotkeyService.Label(action), 15f,
                                        TextAlignmentOptions.Left, HudTheme.TextMain);

            // 키 칸 — 오른쪽. <b>이게 버튼이다</b>
            RectTransform keyRect = NewRect("Key", row);
            keyRect.anchorMin = new Vector2(1f, 0.5f);
            keyRect.anchorMax = new Vector2(1f, 0.5f);
            keyRect.pivot = new Vector2(1f, 0.5f);
            keyRect.sizeDelta = new Vector2(keyColumnWidth, rowHeight - 8f);
            keyRect.anchoredPosition = new Vector2(-6f, 0f);

            var keyBg = keyRect.gameObject.AddComponent<Image>();
            keyBg.color = HudTheme.PanelBgSoft;
            keyBg.raycastTarget = true;

            var button = keyRect.gameObject.AddComponent<Button>();
            button.targetGraphic = keyBg;
            HotkeyAction captured = action;
            button.onClick.AddListener(() =>
            {
                _capturing = captured;
                Redraw();
            });

            RectTransform keyLabelRect = NewRect("Label", keyRect);
            Stretch(keyLabelRect);
            TMP_Text keyLabel = Label(keyLabelRect,
                                      HotkeyService.KeyLabel(HotkeyService.Get(action)),
                                      15f, TextAlignmentOptions.Center, HudTheme.TextMain);
            _keyLabels[action] = keyLabel;
        }

        /// <summary>버튼 하나를 짓고 <b>그 글자 칸을 돌려준다</b> — 언어가 바뀌면 다시 써야 한다.</summary>
        TMP_Text BuildButton(RectTransform panel, string name, string text,
                             Vector2 offset, Vector2 anchor, Vector2 size,
                             UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rect = NewRect(name, panel);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, 0f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;

            var bg = rect.gameObject.AddComponent<Image>();
            Sprite face = Resources.Load<Sprite>("UI/Buttons/Btn_Action_Normal");
            if (face != null)
            {
                bg.sprite = face;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            else bg.color = HudTheme.RowBg;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(onClick);

            RectTransform labelRect = NewRect("Label", rect);
            Stretch(labelRect);
            return Label(labelRect, text, 16f, TextAlignmentOptions.Center, HudTheme.TextMain);
        }

        static TMP_Text Label(RectTransform rect, string text, float size,
                              TextAlignmentOptions align, Color color)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = HudTheme.Font;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    
        /// <summary>
        /// ★ 이 창의 문구를 <b>스트링 표</b>에서 가져온다 (2026-08-26 · 178-5절).
        /// 인스펙터 값은 <b>폴백</b>이다 — 표에 키가 없으면 화면은 지금과 같다.
        /// </summary>
        void LocalizeLabels()
        {
            titleText = HudTheme.T("ui_settings_hotkeys", titleText);
            hintText = HudTheme.T("ui_hotkey_hint", hintText);
            capturingText = HudTheme.T("ui_hotkey_capturing", capturingText);
            resetLabel = HudTheme.T("ui_hotkey_reset", resetLabel);
            closeLabel = HudTheme.T("ui_btn_close", closeLabel);
        }
}
}
