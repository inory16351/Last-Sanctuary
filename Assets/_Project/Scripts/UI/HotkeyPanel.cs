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

        // ★★ 치수 — <b>줄 수가 기능 수를 따라간다</b>. 2026-08-25 에 배속·일시정지·화면
        //    되돌리기 여섯이 들어와 9줄 → <b>15줄</b>이 되었고, 예전 값(38+4, 560px)으로는
        //    마지막 줄이 아래 버튼과 <b>겹쳤다</b>. 줄을 조금 낮추고 판을 키웠다.
        //    <code>
        //      필요한 높이 = 96(제목·안내) + 줄수 × (rowHeight + rowGap) − rowGap
        //                    + 20(여백) + 56(아래 버튼)
        //      15줄 · 34+3 → 96 + 552 + 76 = 724   → 판 740 (여유 16)
        //    </code>
        //    ⚠ 기능을 더 넣으면 <b>이 계산을 다시 할 것</b>. 넘치면 조용히 겹친다.
        [Header("치수 (1920x1080 기준)")]
        [SerializeField] Vector2 panelSize = new Vector2(470f, 740f);
        [SerializeField] float rowHeight = 34f;
        [SerializeField] float rowGap = 3f;
        [SerializeField] float keyColumnWidth = 130f;

        public static HotkeyPanel Instance { get; private set; }

        /// <summary>지금 «다음 키» 를 기다리는가. <see cref="HudHotkeys"/> 가 이걸 보고 비켜준다.</summary>
        public bool IsCapturing => _capturing.HasValue;

        public bool IsOpen => _body != null && _body.activeSelf;

        GameObject _body;
        readonly Dictionary<HotkeyAction, TMP_Text> _keyLabels =
            new Dictionary<HotkeyAction, TMP_Text>();

        HotkeyAction? _capturing;
        bool _built;

        void Awake()
        {
            Instance = this;
            Build();

            // ⚠ <b>여기서 gameObject.SetActive(false) 를 하지 않는다</b> — 루트를 끄면
            //   Awake 가 다시 안 돌아 배선이 사라진다(UnitPortraitPanel 이 겪은 함정).
            //   루트는 항상 활성으로 두고 <see cref="_body"/> 만 켜고 끈다
            //   (VictoryPanel·DefeatPanel 과 같은 방식).
            if (_body != null) _body.SetActive(false);

            HotkeyService.OnChanged += Redraw;
        }

        void OnDestroy()
        {
            HotkeyService.OnChanged -= Redraw;
            if (Instance == this) Instance = null;
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
                if (stolen.HasValue)
                    HudLog.Add($"「{HotkeyService.Label(stolen.Value)}」의 단축키가 해제되었습니다 " +
                               $"— {HotkeyService.KeyLabel(chosen)} 를 「{HotkeyService.Label(action)}」이 가져갔습니다",
                               HudLogKind.Warn);
                else
                    HudLog.Add($"「{HotkeyService.Label(action)}」 단축키 → " +
                               $"{HotkeyService.KeyLabel(chosen)}", HudLogKind.Good);

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
            panelBg.color = HudTheme.PanelBg;

            // 제목
            RectTransform title = NewRect("Title", panel);
            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = new Vector2(1f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.sizeDelta = new Vector2(-40f, 40f);
            title.anchoredPosition = new Vector2(0f, -14f);
            Label(title, titleText, 24f, TextAlignmentOptions.Left, HudTheme.TextMain);

            // 안내
            RectTransform hint = NewRect("Hint", panel);
            hint.anchorMin = new Vector2(0f, 1f);
            hint.anchorMax = new Vector2(1f, 1f);
            hint.pivot = new Vector2(0.5f, 1f);
            hint.sizeDelta = new Vector2(-40f, 34f);
            hint.anchoredPosition = new Vector2(0f, -54f);
            TMP_Text hintLabel = Label(hint, hintText, 14f,
                                       TextAlignmentOptions.TopLeft, HudTheme.TextDim);
            hintLabel.textWrappingMode = TextWrappingModes.Normal;

            // 줄 — 기능마다 하나
            float y = -96f;
            for (int i = 0; i < HotkeyService.All.Length; i++)
            {
                HotkeyAction action = HotkeyService.All[i];
                BuildRow(panel, action, y);
                y -= rowHeight + rowGap;
            }

            // 아래 버튼 둘
            BuildButton(panel, "ResetButton", resetLabel,
                        new Vector2(20f, 18f), new Vector2(0f, 0f), new Vector2(150f, 38f),
                        () =>
                        {
                            HotkeyService.ResetAll();
                            HudLog.Add("단축키를 기본값으로 되돌렸습니다", HudLogKind.Good);
                        });

            BuildButton(panel, "CloseButton", closeLabel,
                        new Vector2(-20f, 18f), new Vector2(1f, 0f), new Vector2(110f, 38f),
                        Close);
        }

        void BuildRow(RectTransform panel, HotkeyAction action, float y)
        {
            RectTransform row = NewRect("Row_" + action, panel);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(-40f, rowHeight);
            row.anchoredPosition = new Vector2(0f, y);

            var bg = row.gameObject.AddComponent<Image>();
            bg.color = HudTheme.RowBg;
            bg.raycastTarget = false;

            // 기능 이름 — 왼쪽
            RectTransform name = NewRect("Name", row);
            name.anchorMin = new Vector2(0f, 0f);
            name.anchorMax = new Vector2(1f, 1f);
            name.offsetMin = new Vector2(12f, 0f);
            name.offsetMax = new Vector2(-(keyColumnWidth + 8f), 0f);
            Label(name, HotkeyService.Label(action), 16f,
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

        void BuildButton(RectTransform panel, string name, string text,
                         Vector2 offset, Vector2 anchor, Vector2 size,
                         UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rect = NewRect(name, panel);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, 0f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;

            var bg = rect.gameObject.AddComponent<Image>();
            bg.color = HudTheme.RowBg;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(onClick);

            RectTransform labelRect = NewRect("Label", rect);
            Stretch(labelRect);
            Label(labelRect, text, 16f, TextAlignmentOptions.Center, HudTheme.TextMain);
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
    }
}
