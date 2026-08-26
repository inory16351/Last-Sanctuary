using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★★ <b>성장 창 아래의 «유물 칸 셋»</b> (2026-08-26 신설 · 유저 지시:
    /// *"캐릭터 성장에 유물 장착 칸 슬롯을 아래로 더 내려서 가로 단위로 세 칸으로 자른 다음
    /// 장착하고 있는 유물 하나하나 정보를 볼 수 있게 해줘 스킬 칸 처럼"* ·
    /// *"아무것도 안 장착하고 있을땐 빈 슬롯으로 넣어주고"*).
    ///
    /// <b>왜 코드가 짓는가</b> — 칸 수는 <see cref="Relics.RelicInventory.EquipSlots"/> 가 정한다
    /// (지금 셋, 늘어날 수 있다). 씬에 셋을 손으로 두면 넷이 되는 날 <b>씬과 코드가 갈린다</b> —
    /// <see cref="RelicIconStrip"/> 이 같은 이유로 아이콘을 복제하고, 도움말 본문 스크롤
    /// (<c>HelpPanel.EnsureBodyScroll</c>)도 같은 이유로 코드가 짓는다.
    /// 게다가 <b>MCP 로는 스프라이트 참조를 못 넣는다</b>(진행상황 8절 4번) — 칸 판때기와
    /// «빈 칸» 아이콘이 그림이라 어차피 코드가 꽂아야 한다.
    ///
    /// <b>무엇을 짓나</b> — 띠(<c>RelicBar</c>) 안에 <b>가로로 셋</b>을 자른다.
    /// <code>
    ///   RelicBar 1172 x 90
    ///   ┌ RelicCard_0 ─┐ ┌ RelicCard_1 ─┐ ┌ RelicCard_2 ─┐  [유물 관리 열기]
    ///   │ ⬛ 이름       │ │ ⬛ 이름       │ │ ⬛ 빈 칸      │   ← 오른쪽은 예약폭
    ///   │    효과 두 줄 │ │    효과 두 줄 │ │              │
    ///   └──────────────┘ └──────────────┘ └──────────────┘
    /// </code>
    ///
    /// ★ <b>씬의 <c>Icon</c>·<c>Name</c>·<c>Effect</c> 를 «원본» 으로 쓴다</b> — 폰트·크기·색을
    ///   씬이 정하게 두려는 것이다(로스터 행이 <c>RowTemplate</c> 을 복제하는 것과 같은 결).
    ///   원본은 <b>끄지 않고 첫 칸으로 옮긴다</b> — 꺼 두고 복제만 쓰면 씬에서 값을 고쳐도
    ///   화면에 안 보여 «고쳤는데 안 바뀐다» 가 된다.
    /// ⚠ <b>다시 불려도 안전하다</b>(<see cref="Build"/>) — 이미 지어 뒀으면 그대로 쓴다.
    ///   성장 창은 0.2초마다 갱신되므로 여기서 매번 새로 지으면 프레임이 죽는다.
    /// </summary>
    public class RelicCardStrip
    {
        /// <summary>칸 하나.</summary>
        public class Card
        {
            public GameObject Root;
            public Image Plate;
            public Button Button;
            public Image Icon;
            public TMP_Text Name;
            public TMP_Text Effect;

            /// <summary>지금 이 칸이 보여주는 유물. 0 이면 빈 칸.</summary>
            public int RelicId;
        }

        readonly List<Card> _cards = new List<Card>();

        /// <summary>지어 둔 칸들. 비어 있으면 아직 안 지었다.</summary>
        public IReadOnlyList<Card> Cards => _cards;

        /// <summary>칸 사이 간격(픽셀).</summary>
        const float Gap = 10f;

        /// <summary>판때기 그림 — 스킬 칸(<c>PassiveCard_*</c>)과 <b>같은 것</b>을 쓴다.</summary>
        const string PlateResource = "UI/Frames/Hud_Plate";

        /// <summary>빈 칸 아이콘.</summary>
        const string EmptyIconResource = "UI/Frames/Slot_Empty";

        static Sprite _plate, _emptyIcon;

        /// <summary>빈 칸에 쓰는 아이콘. 없으면 <c>null</c>(그때는 아이콘을 끈다).</summary>
        public static Sprite EmptyIcon =>
            _emptyIcon != null ? _emptyIcon : (_emptyIcon = Resources.Load<Sprite>(EmptyIconResource));

        /// <summary>
        /// 칸을 <paramref name="slots"/> 개 짓는다. 이미 지어 뒀으면 아무 일도 하지 않는다.
        /// </summary>
        /// <param name="bar">띠(<c>RelicBar</c>).</param>
        /// <param name="icon">씬의 아이콘 — 첫 칸으로 옮기고 나머지 칸은 이것을 복제한다.</param>
        /// <param name="name">씬의 이름 칸.</param>
        /// <param name="effect">씬의 효과 칸.</param>
        /// <param name="slots">칸 수(유물 장착 칸과 같아야 한다).</param>
        /// <param name="rightReserved">
        /// 띠 오른쪽에 <b>비워 둘 폭</b> — 「유물 관리 열기」 버튼 자리다. 이걸 안 빼면
        /// 마지막 칸이 버튼 밑으로 들어간다.
        /// </param>
        public void Build(RectTransform bar, Image icon, TMP_Text name, TMP_Text effect,
                          int slots, float rightReserved)
        {
            if (_cards.Count > 0) return;
            if (bar == null || slots <= 0) return;

            if (_plate == null) _plate = Resources.Load<Sprite>(PlateResource);

            float barW = bar.rect.width;
            float barH = bar.rect.height;
            float width = (barW - rightReserved - Gap * (slots - 1)) / slots;
            if (width < 40f) return;                 // 띠가 너무 좁다 — 짓지 않는다(빈 목록이 곧 «없음»)

            for (int i = 0; i < slots; i++)
            {
                var card = new Card();

                var go = new GameObject($"RelicCard_{i}", typeof(RectTransform));
                go.layer = bar.gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(bar, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(width, barH);
                rt.anchoredPosition = new Vector2(i * (width + Gap), 0f);

                card.Root = go;
                card.Plate = go.AddComponent<Image>();
                if (_plate != null)
                {
                    card.Plate.sprite = _plate;
                    card.Plate.type = Image.Type.Sliced;
                }
                card.Plate.color = new Color(1f, 1f, 1f, 0.95f);

                // ★ 누르면 그 유물을 <b>유물 관리 창에서</b> 편다 — 스킬 칸이 스킬 상세를
                //   띄우는 것과 같은 짜임이다. 배선은 부르는 쪽(성장 창)이 한다.
                card.Button = go.AddComponent<Button>();
                card.Button.targetGraphic = card.Plate;
                card.Button.transition = Selectable.Transition.ColorTint;

                card.Icon = Adopt(icon, rt, i == 0, "Icon");
                card.Name = Adopt(name, rt, i == 0, "Name");
                card.Effect = Adopt(effect, rt, i == 0, "Effect");

                // ⚠ <b>판때기 테두리(위 10 · 아래 8 · 좌 10 · 우 8)를 피한다</b>
                //   (2026-08-26 · 유저 리포트: *"유물관리 칸 뒤에 텍스트 가려짐"*).
                //   테두리에 <b>닿기만 해도</b> 글자가 그림에 먹힌 것으로 보이므로 4px 숨통을 더 준다.
                Place(card.Icon, new Vector2(14f, -14f), new Vector2(46f, 46f));
                Place(card.Name, new Vector2(70f, -14f), new Vector2(width - 84f, 24f));
                Place(card.Effect, new Vector2(70f, -40f), new Vector2(width - 84f, barH - 52f));

                // 이름은 한 줄, 효과는 두 줄까지 — 칸이 좁으니 글자가 줄어들어 들어간다.
                HudTheme.FitText(card.Name, 11f, wrap: false);
                HudTheme.FitText(card.Effect, 10f);
                if (card.Effect != null) card.Effect.color = HudTheme.TextDim;

                _cards.Add(card);
            }
        }

        /// <summary>
        /// 원본을 <b>첫 칸에는 옮기고</b>(<paramref name="move"/>) 나머지 칸에는 복제해 넣는다.
        /// 원본이 없으면 <c>null</c> — 부르는 쪽이 «그 줄은 없다» 로 다룬다.
        /// </summary>
        static T Adopt<T>(T source, RectTransform parent, bool move, string childName)
            where T : Component
        {
            if (source == null) return null;

            T target = move ? source : Object.Instantiate(source, parent);
            if (move) target.transform.SetParent(parent, false);
            target.gameObject.name = childName;
            target.gameObject.SetActive(true);
            return target;
        }

        /// <summary>칸 좌상단 기준으로 자리를 잡는다 — 앵커·피벗을 <b>항상 함께</b> 준다.</summary>
        static void Place(Component c, Vector2 topLeft, Vector2 size)
        {
            if (c == null) return;
            var rt = c.transform as RectTransform;
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = topLeft;
        }
    }
}
