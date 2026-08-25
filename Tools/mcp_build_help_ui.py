# -*- coding: utf-8 -*-
"""★★ <b>도움말(튜토리얼) 전용 UI 캔버스</b>를 MCP 로 만든다 (2026-08-24).

유저 지시: *"지금 듀토리얼 테이블에 들어가 있는 거 듀토리얼 용 UI 캔버스 만들어서 도움말
방식으로 듀토리얼 구성해줘"* · *"최초로 해당 기능을 눌렀을때 나타나게"* ·
*"MCP 이용 해서 직접 생성, 수정"*.

무엇을 만드나
-------------
``Help_Root`` — <b>새 루트 캔버스 하나</b>. 그 아래 둘이다.

  ``Help_Root/HUD_Help``      도움말 창(백과) 940x600 — 탭 · 목록 · 상세
  ``Help_Root/HUD_HelpCard``  조언 카드 — 화면을 덮는 어두운 막 + 가운데 카드 720x300

그리고 ``UI_Root/HUD_Actions/Buttons/HelpButton``(액션 버튼) ·
``GameSystems`` 에 ``HelpService`` 를 붙인다.

★★ <b>왜 «별도 캔버스» 인가</b> (유저 지시가 그렇게 못박았고, 실제로 필요하다)
-----------------------------------------------------------------------
이 씬의 그리는 순서는 <b>캔버스의 sortingOrder</b> 로 갈린다:

    UI_Root 0  ·  건설/집결지 오버레이 −1  ·  DigOverlay 5  ·  HUD_Dig 6

조언 카드는 <b>게임을 멈추고</b> 읽게 하는 판이라 <b>무엇보다도 위</b>여야 한다 —
느낌표 표식(5)이나 발굴 창(6)이 카드를 뚫고 나오면 «멈췄는데 뒤가 만져지는» 꼴이 된다.
그래서 ``Help_Root`` 를 <b>20</b> 으로 둔다.
⚠ Canvas 를 새로 두면 <b>GraphicRaycaster 도 같이</b> 붙여야 한다 — 없으면 그 아래 버튼이
  클릭을 못 받는다(``DigOverlay``·``HUD_Dig`` 가 같은 이유로 둘을 함께 붙였다).
⚠ ``CanvasScaler`` 도 붙인다 — ``UI_Root`` 와 <b>같은 값</b>(1920x1080 · MatchWidthOrHeight
  0.5)이어야 창 크기가 해상도에 따라 어긋나지 않는다.

★ 카드가 백과보다 <b>뒤에</b> 만들어진다 — 같은 캔버스 안에서는 <b>형제 순서</b>가
  그리는 순서다. 그래도 두 쪽 모두 열릴 때 ``SetAsLastSibling()`` 을 부르므로(코드)
  순서에 의존하지는 않는다.

⚠ 멱등하다 — `update_gameobject`/`update_component` 가 «없으면 만들고 있으면 고친다».
⚠ 돌린 뒤 <b>폰트 메뉴를 반드시 실행할 것</b> — MCP 로는 폰트 «참조» 를 못 넣어서
  새 글자가 TMP 기본 폰트로 태어난다(한글이 깨진다).

⚠⚠ <b>돌린 뒤에는 반드시 «배선» 을 다시 돌릴 것</b> (2026-08-26).
  이 스크립트의 :func:`image` 는 색을 <b>어두운 단색</b>으로 되돌린다. 배선이 깔아 둔
  그림은 남지만 그 어두운 색이 <b>그림에 곱해져</b> 새까매진다 — «그림을 넣었는데
  안 보인다» 의 정체(<c>HudTheme.PaintButton</c> 의 설명과 같은 함정).

사용법:  py -3 Tools/mcp_build_help_ui.py   (유니티 에디터가 켜져 있어야 한다)
다음:    ① 유니티 메뉴 LastSanctuary/폰트/네오 둥근모 TMP 에셋 굽고 씬에 적용
         ② 유니티 메뉴 LastSanctuary/UI/배선      ← 그림을 깔고 색을 흰색으로 되돌린다
         ③ 유니티 메뉴 LastSanctuary/UI/글자 여백  ← 남은 글자를 테두리 안쪽으로 민다
"""

import json
import os
import subprocess
import sys

from vault_path import PROJECT

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

CLI = os.path.join(PROJECT, "Tools", "mcp_unity_cli.js")

# ── 색 — HudTheme 과 같은 톤. 값을 여기서 새로 만들지 말 것 ──────────────
PANEL_BG = {"r": 0.07, "g": 0.08, "b": 0.10, "a": 0.97}
BOX_BG = {"r": 0.10, "g": 0.12, "b": 0.15, "a": 0.92}
ROW_BG = {"r": 0.11, "g": 0.13, "b": 0.17, "a": 0.92}
BTN_BG = {"r": 0.16, "g": 0.42, "b": 0.38, "a": 0.98}
BTN_OFF = {"r": 0.13, "g": 0.17, "b": 0.22, "a": 0.95}
CLOSE_BG = {"r": 0.30, "g": 0.13, "b": 0.15, "a": 0.95}
TEXT = {"r": 0.88, "g": 0.92, "b": 0.94, "a": 1.0}
DIM = {"r": 0.58, "g": 0.64, "b": 0.70, "a": 1.0}
ACCENT = {"r": 0.45, "g": 0.95, "b": 0.78, "a": 1.0}
HANDLE = {"r": 0.30, "g": 0.42, "b": 0.48, "a": 0.95}

#: ★ 짚어 주는 <b>빨간 테두리</b>. 이 게임의 어떤 UI 색과도 겹치지 않는다 —
#   체력바의 «위험한 빨강»(0.92,0.38,0.38)보다 <b>훨씬 진하고 선명하게</b> 잡아
#   «전투 정보» 가 아니라 «지금 이것을 보라» 로 읽히게 했다.
TOUR_RED = {"r": 1.0, "g": 0.16, "b": 0.16, "a": 1.0}

#: ★ 어두운 막. 너무 짙으면 «무슨 일이 있었는지» 가 안 보이고, 너무 옅으면 멈춘 줄 모른다.
SCRIM = {"r": 0.02, "g": 0.03, "b": 0.04, "a": 0.62}

#: 액션 버튼 칸 계산 — mcp_hud_20260824.py 와 <b>같은 식</b>이다(값을 두 벌로 만들지 않는다).
BUTTON_H = 40
BUTTON_GAP = 8

# ══════════════════════════════════════════════════════════════════════════
#  ★★★ <b>그림 테두리 안쪽에만 내용을 둔다</b> (2026-08-26 · 유저 지시:
#  *"도움말 ui에 ui 이미지들 적용하고 텍스트 위치 안 가리게 맞추기"*)
#
#  이 창은 <b>단색 사각형</b>이던 시절의 좌표를 그대로 쓰고 있었다. 그때는 테두리가
#  1~2px 이라 글자를 가장자리 12~18px 에 놓아도 됐다. 픽셀 UI(UI-61)가 들어오면서
#  <b>창 그림(<c>Win_Frame</c>)의 테두리가 23px</b>, <b>속판(<c>Hud_Plate</c>)이 10px</b>
#  가 되었고, 배선을 돌리는 순간 머리글·힌트·목록·상세가 <b>전부 테두리 밑으로</b>
#  들어간다.
#
#  ★ 여백을 <b>여기서 못박는다</b> — <see cref="UiTextInset"/> 는 «글자» 만 밀 수 있고
#    목록·상세 같은 <b>속판 자체</b>는 못 옮긴다. 판의 자리는 MCP 의 몫이다(§10 H-1).
#  ★ <b>창을 키워서</b> 여백을 만든다 — 안쪽 폭을 그대로 두면 본문 줄 수가 줄어
#    «안 가려졌는데 대신 넘친다» 가 된다. 940x600 → 980x648.
#  ⚠ 값은 <b>실측한 경계 + 숨통 4</b> 다: 창 23+4 = 27 · 속판 10+4 = 14.
#    그림을 다시 뽑아 테두리가 바뀌면 여기 두 상수만 고친다.
# ══════════════════════════════════════════════════════════════════════════
WIN_PAD = 27      # Win_Frame  경계 23/22/23/21 + 숨통 4
BOX_PAD = 14      # Hud_Plate  경계 10/8/8/10   + 숨통 4
PANEL_PAD = 20
VISIBLE_BUTTONS = 8      # 건설은 꺼져 있고, 이번에 「도움말」이 하나 늘어 7 → 8

_requests = []


def call(method, params):
    _requests.append({"method": method, "params": params})


def go(path, active=True, layer=5):
    call("update_gameobject", {"objectPath": path,
                               "gameObjectData": {"activeSelf": active, "layer": layer}})


def comp(path, name, data=None):
    call("update_component", {"objectPath": path, "componentName": name,
                              "componentData": data or {}})


def rect(path, amin, amax, omin, omax, pivot=(0.5, 0.5)):
    comp(path, "RectTransform", {
        "anchorMin": {"x": amin[0], "y": amin[1]},
        "anchorMax": {"x": amax[0], "y": amax[1]},
        "offsetMin": {"x": omin[0], "y": omin[1]},
        "offsetMax": {"x": omax[0], "y": omax[1]},
        "pivot": {"x": pivot[0], "y": pivot[1]},
    })


def image(path, color, raycast=True):
    comp(path, "Image", {"color": color, "raycastTarget": raycast})


# ⚠⚠ <b>«쓰지 않는 인자» 를 두지 말 것</b> — UI-52-1 에서 그 칸 하나 때문에 좌표 네 개가
#   한 칸씩 밀려 유물 창 글자가 통째로 화면 밖으로 나갔다. 아래 서명은 mcp_build_dig_ui.py 와
#   <b>글자까지 같다</b>.
def text(path, value, size=16, color=None, align="Left"):
    """⚠ 정렬은 <b>이름</b>으로 준다 — 브리지가 enum 을 인덱스로 해석한다."""
    comp(path, "TextMeshProUGUI", {
        "m_text": value,
        "m_fontSize": size,
        "m_fontColor": color or TEXT,
        "m_textAlignment": align,
        # ⚠ TMP 에 없는 칸(m_enableWordWrapping·m_raycastTarget)을 하나라도 넣으면
        #   브리지가 요청 «전체» 를 거절해 글자·색까지 조용히 안 들어간다.
    })


def label(path, value, size, color, align, amin, amax, omin, omax):
    go(path)
    rect(path, amin, amax, omin, omax)
    text(path, value, size, color, align)


def button(path, bg, amin, amax, omin, omax, caption, size=16):
    go(path)
    rect(path, amin, amax, omin, omax)
    image(path, bg)
    comp(path, "Button", {})
    label(path + "/Label", caption, size, TEXT, "Center", (0, 0), (1, 1), (6, 0), (-6, 0))


# ══════════════════════════════════════════════════════════════════════════
# ① 전용 캔버스
# ══════════════════════════════════════════════════════════════════════════
def build_root():
    r = "Help_Root"
    go(r)
    # ★ sortingOrder 20 — DigOverlay(5)·HUD_Dig(6) 보다 확실히 위(맨 위 ★★).
    comp(r, "Canvas", {"renderMode": "ScreenSpaceOverlay", "sortingOrder": 20,
                       "pixelPerfect": True})
    # ⚠ UI_Root 와 <b>같은 값</b>이어야 한다 — 다르면 같은 해상도에서 창 크기가 갈린다.
    comp(r, "CanvasScaler", {"m_UiScaleMode": "ScaleWithScreenSize",
                             "m_ReferenceResolution": {"x": 1920, "y": 1080},
                             "m_ScreenMatchMode": "MatchWidthOrHeight",
                             "m_MatchWidthOrHeight": 0.5,
                             "m_ReferencePixelsPerUnit": 100})
    comp(r, "GraphicRaycaster", {})


# ══════════════════════════════════════════════════════════════════════════
# ② 도움말 창(백과) 940 x 600
#
# 배치 — 머리 · 분류 탭 한 줄 · 왼쪽 목록(폭 306) · 오른쪽 상세(나머지).
# 유물 관리 창(HUD_Relics)과 같은 결이다. 다른 점은 <b>탭 한 줄</b>뿐이고,
# 그 줄이 있는 이유는 항목이 27개라 한 목록에 담으면 못 찾기 때문이다.
# ══════════════════════════════════════════════════════════════════════════
def build_panel():
    p = "Help_Root/HUD_Help"
    go(p)
    # ★ 980x648 — 예전 940x600 에 <b>테두리 몫(좌우 27씩 · 위아래 24씩)</b>을 더한 것.
    #   안쪽 쓸 수 있는 넓이는 예전과 같다.
    rect(p, (0.5, 0.5), (0.5, 0.5), (-490, -324), (490, 324))
    image(p, PANEL_BG)
    comp(p, "HelpPanel", {})
    comp(p, "UiWindowDrag", {})

    label(p + "/Header", "도움말", 22, TEXT, "Left",
          (0, 1), (1, 1), (28, -64), (-66, -28))

    # ⚠ 닫기는 <b>테두리 안쪽</b>으로 들인다 — 전에는 12px 라 액자 모서리 장식에
    #   반쯤 덮여 있었다. 32x32 는 그대로(Btn_Close 가 정사각이라 늘리면 뭉갠다).
    c = p + "/CloseButton"
    go(c)
    rect(c, (1, 1), (1, 1), (-62, -62), (-30, -30))
    image(c, CLOSE_BG)
    comp(c, "Button", {})
    label(c + "/Label", "X", 18, TEXT, "Center", (0, 0), (1, 1), (0, 0), (0, 0))

    label(p + "/Hint", "왼쪽에서 항목을 고르면 설명이 나옵니다.", 14, DIM, "Left",
          (0, 0), (1, 0), (28, 28), (-28, 50))

    # ── 분류 탭 ──
    #   ★ 개수는 <b>표</b>가 정한다 — 코드가 TabTemplate 을 복제한다(HelpPanel.MakeTab).
    #     씬에 여섯 개를 박아 두면 표에 분류가 하나 늘 때 조용히 안 보인다.
    #   ⚠ 머리글이 위로 16 내려왔으므로 탭도 <b>같이</b> 내린다 — 안 내리면 겹친다.
    tabs = p + "/Tabs"
    go(tabs)
    rect(tabs, (0, 1), (1, 1), (28, -104), (-28, -68))
    comp(tabs, "HorizontalLayoutGroup", {"spacing": 4,
                                         "childControlWidth": True,
                                         "childControlHeight": True,
                                         "childForceExpandWidth": True,
                                         "childForceExpandHeight": True})

    tt = tabs + "/TabTemplate"
    go(tt)
    rect(tt, (0, 0), (0, 1), (0, 0), (140, 0))
    image(tt, BTN_OFF)
    comp(tt, "Button", {})
    comp(tt, "LayoutElement", {"minWidth": 80, "preferredWidth": 150, "minHeight": 36})
    label(tt + "/Label", "분류", 15, TEXT, "Center", (0, 0), (1, 1), (4, 0), (-4, 0))
    go(tt, active=False)     # ⚠ 원본은 꺼둔다

    # ── 목록 (왼쪽) ──
    box = p + "/List"
    go(box)
    rect(box, (0, 0), (0, 1), (28, 58), (334, -112))
    image(box, BOX_BG, raycast=False)

    # ⚠ 속판(Hud_Plate)의 경계는 좌 10 · 아래 8 · 우 8 · 위 10 이다. 4를 더한 값이
    #   아래 좌표다 — 예전 4px 로는 목록 첫 줄과 마지막 줄이 판 테두리에 물렸다.
    sv = box + "/ScrollView"
    go(sv)
    rect(sv, (0, 0), (1, 1), (14, 12), (-28, -14))
    comp(sv, "ScrollRect", {"horizontal": False, "vertical": True,
                            "movementType": 2, "scrollSensitivity": 24})

    vp = sv + "/Viewport"
    go(vp)
    rect(vp, (0, 0), (1, 1), (0, 0), (0, 0))
    comp(vp, "RectMask2D", {})

    items = vp + "/Items"
    go(items)
    rect(items, (0, 1), (1, 1), (0, 0), (0, 0), pivot=(0.5, 1))
    comp(items, "VerticalLayoutGroup", {"spacing": 4, "childForceExpandHeight": False,
                                        "childControlHeight": False, "childControlWidth": True,
                                        "childForceExpandWidth": True})
    comp(items, "ContentSizeFitter", {"verticalFit": 2})

    bar = box + "/Scrollbar"
    go(bar)
    rect(bar, (1, 0), (1, 1), (-24, 12), (-12, -14))
    image(bar, {"r": 0.06, "g": 0.07, "b": 0.09, "a": 0.9}, raycast=True)
    comp(bar, "Scrollbar", {"direction": 2})     # 2 = BottomToTop
    h = bar + "/Handle"
    go(h)
    rect(h, (0, 0), (1, 1), (0, 0), (0, 0))
    image(h, HANDLE)

    row = box + "/RowTemplate"
    go(row)
    rect(row, (0, 1), (1, 1), (4, -42), (-4, -4))
    image(row, ROW_BG)
    comp(row, "Button", {})
    comp(row, "LayoutElement", {"minHeight": 38, "preferredHeight": 38})
    label(row + "/Dot", "◦", 14, ACCENT, "Center", (0, 0), (0, 1), (6, 0), (26, 0))
    label(row + "/Name", "항목", 15, TEXT, "Left", (0, 0), (1, 1), (28, 0), (-8, 0))
    go(row, active=False)     # ⚠ 원본은 꺼둔다

    # ── 상세 (오른쪽) 608 x 478 ──
    d = p + "/Detail"
    go(d)
    rect(d, (0, 0), (1, 1), (344, 58), (-28, -112))
    image(d, BOX_BG, raycast=False)

    label(d + "/Title", "-", 22, TEXT, "Left", (0, 1), (1, 1), (16, -52), (-16, -16))
    label(d + "/Category", "", 13, DIM, "Left", (0, 1), (1, 1), (16, -76), (-16, -54))
    # 요약 — 가장 긴 것이 67자 · 두 줄이다. 16pt · 74px 면 넉넉하다.
    label(d + "/Summary", "", 16, ACCENT, "TopLeft", (0, 1), (1, 1), (16, -154), (-16, -80))
    # ★ 본문 — 가장 긴 것이 <b>241자 · 5줄</b>이고 한 줄이 83자까지 간다(실측).
    #   576px 폭에 15pt 면 한 줄에 38자쯤 들어가므로 최악이 11줄쯤 = 231px. 252px 를 준다.
    # ⚠ «절대 안 넘친다» 는 보장은 <b>코드가</b> 한다(HelpPanel.EnsureBound → HudTheme.FitText).
    label(d + "/Body", "", 15, TEXT, "TopLeft", (0, 1), (1, 1), (16, -410), (-16, -158))

    # ★ 아래 두 버튼은 <b>가로로 나란히</b> 둔다 — 상세 틀이 608 폭이라 둘이 들어간다.
    #   「화면에서 짚어 보기」가 <b>왼쪽</b>이다(처음 하는 사람이 먼저 눌러야 하는 쪽).
    # ⚠ 오른쪽 끝이 592 다 — 속판 폭 608 에서 <b>16</b>을 남긴다(경계 8 + 숨통).
    button(d + "/TourButton", BTN_BG, (0, 0), (0, 0), (16, 16), (234, 54),
           "화면에서 짚어 보기", 14)
    button(d + "/SeeAlsoButton", BTN_OFF, (0, 0), (0, 0), (244, 16), (592, 54),
           "함께 볼 것", 14)

    # ⚠ 창은 <b>꺼둔 상태</b>로 저장한다 — 다른 HUD 창과 같은 규칙(ActionPanel·F1 이 연다).
    #   ★ 이 한 줄을 빠뜨리면 «게임을 켜자마자 도움말이 떠 있다» 가 된다. 실행 중에는
    #   ActionPanel.Start 가 한 번 닫아 주지만, 그것에 기대면 에디터에서 씬을 열 때마다
    #   창이 화면을 덮어 다른 UI 작업을 방해한다.
    go(p, active=False)


# ══════════════════════════════════════════════════════════════════════════
# ③ 조언 카드
#
# ⚠ 어두운 막의 raycastTarget 을 <b>켜 둔다</b> — 그것이 뒤의 전장·버튼 클릭을 막는
#   유일한 장치다. 끄면 «멈춘 화면» 뒤로 손이 닿는다.
# ══════════════════════════════════════════════════════════════════════════
def build_card():
    p = "Help_Root/HUD_HelpCard"
    go(p)
    rect(p, (0, 0), (1, 1), (0, 0), (0, 0))
    image(p, SCRIM, raycast=True)
    comp(p, "HelpCardPanel", {})

    # ★ 720x300 → <b>760x330</b> — 카드에도 창 그림(Win_Frame)이 깔리므로 테두리 몫을
    #   더한다(맨 위 WIN_PAD 참조). 안쪽에 쓸 수 있는 넓이는 예전과 같다.
    c = p + "/Card"
    go(c)
    rect(c, (0.5, 0.5), (0.5, 0.5), (-380, -165), (380, 165))
    image(c, PANEL_BG)

    label(c + "/Badge", "도움말", 14, ACCENT, "Left",
          (0, 1), (1, 1), (28, -52), (-28, -26))
    label(c + "/Title", "-", 26, TEXT, "Left",
          (0, 1), (1, 1), (28, -96), (-28, -56))
    # 요약 두 줄(가장 긴 줄 43자) → 704px 폭에 17pt 면 한 줄에 41자쯤. 최악 4줄 = 102px.
    label(c + "/Summary", "", 17, TEXT, "TopLeft",
          (0, 1), (1, 1), (28, -206), (-28, -104))

    button(c + "/MoreButton", BTN_OFF, (0, 0), (0, 0), (28, 28), (228, 72),
           "자세히 보기", 15)
    button(c + "/OkButton", BTN_BG, (1, 0), (1, 0), (-228, 28), (-28, 72),
           "알겠습니다", 15)

    # ⚠ 카드는 <b>꺼둔 상태</b>로 저장한다 — HelpService 가 띄운다.
    go(p, active=False)


# ══════════════════════════════════════════════════════════════════════════
# ④ 화면에서 짚어 주는 안내 (빨간 테두리)
#
# ★★ <b>어두운 막을 깔지 않는다.</b> 가리켜야 할 것이 화면의 UI 인데 화면을 어둡게 덮으면
#   가리키는 대상이 같이 어두워진다. 그래서 <b>거의 투명한 막</b>으로 <b>클릭만</b> 막는다 —
#   안내 중에 뒤의 버튼이 눌리면 화면이 바뀌어 짚던 자리가 사라진다.
#   ⚠ 알파를 0 으로 두면 유니티가 그 Image 의 레이캐스트를 <b>그대로 받는다</b>(투명해도 막힌다).
#     그래도 0.02 를 준 이유는 «막이 있다» 는 것을 아주 옅게 보여 주기 위해서다.
#
# ★★ 테두리는 <b>막대 넷</b>이다(위·아래·왼·오른). 9-slice 테두리 스프라이트가 없어서
#   Image 하나로 그리면 <b>속이 꽉 찬 네모</b>가 되어 짚으려는 것을 덮어 버린다.
#   막대 넷이면 그림 없이도 가운데가 비어 <b>대상이 그대로 보인다</b>.
#   ⚠ 좌표·크기는 <b>코드가</b> 매 단계 다시 잡는다(HelpTourPanel.PlaceFrame) —
#     여기서 주는 값은 «에디터에서 봤을 때 모양이 잡혀 있게» 하는 초기값일 뿐이다.
# ══════════════════════════════════════════════════════════════════════════
def build_tour():
    p = "Help_Root/HUD_HelpTour"
    go(p)
    rect(p, (0, 0), (1, 1), (0, 0), (0, 0))
    image(p, {"r": 0, "g": 0, "b": 0, "a": 0.02}, raycast=True)
    comp(p, "HelpTourPanel", {})

    # ── 빨간 테두리 (막대 넷) ──
    f = p + "/Frame"
    go(f)
    comp(f, "RectTransform", {
        "anchorMin": {"x": 0.5, "y": 0.5}, "anchorMax": {"x": 0.5, "y": 0.5},
        "pivot": {"x": 0.5, "y": 0.5},
        "anchoredPosition": {"x": 0, "y": 0},
        "sizeDelta": {"x": 200, "y": 60},
    })
    for name, pos, size in (
        ("Top",    (0, 28),   (200, 4)),
        ("Bottom", (0, -28),  (200, 4)),
        ("Left",   (-98, 0),  (4, 52)),
        ("Right",  (98, 0),   (4, 52)),
    ):
        b = f + "/" + name
        go(b)
        comp(b, "RectTransform", {
            "anchorMin": {"x": 0.5, "y": 0.5}, "anchorMax": {"x": 0.5, "y": 0.5},
            "pivot": {"x": 0.5, "y": 0.5},
            "anchoredPosition": {"x": pos[0], "y": pos[1]},
            "sizeDelta": {"x": size[0], "y": size[1]},
        })
        image(b, TOUR_RED, raycast=False)

    # ── 말풍선 ──
    #   ★ 자리는 <b>코드가</b> 매 단계 잡는다(대상을 가리지 않는 쪽으로) — 여기 값은 초기값.
    b = p + "/Bubble"
    go(b)
    comp(b, "RectTransform", {
        "anchorMin": {"x": 0.5, "y": 0.5}, "anchorMax": {"x": 0.5, "y": 0.5},
        "pivot": {"x": 0.5, "y": 0.5},
        "anchoredPosition": {"x": 0, "y": -240},
        # ★ 560x210 → <b>600x240</b> — 말풍선에도 창 그림이 깔린다(맨 위 WIN_PAD).
        #   ⚠ 크기를 여기서 바꿔도 된다 — HelpTourPanel.PlaceBubble 은 <b>자리</b>만 잡고
        #     크기는 <c>sizeDelta</c> 를 <b>읽어서</b> 쓴다.
        "sizeDelta": {"x": 600, "y": 240},
    })
    image(b, PANEL_BG)

    label(b + "/Title", "-", 18, TEXT, "Left", (0, 1), (1, 1), (28, -54), (-102, -26))
    label(b + "/Counter", "1 / 1", 13, ACCENT, "Right", (1, 1), (1, 1), (-94, -52), (-28, -28))
    # 단계 글 — 두세 문장. 544px 폭에 15pt 면 한 줄에 36자쯤이라 여섯 줄까지 들어간다.
    label(b + "/Text", "", 15, TEXT, "TopLeft", (0, 1), (1, 1), (28, -164), (-28, -60))

    button(b + "/PrevButton", BTN_OFF, (0, 0), (0, 0), (28, 28), (138, 68), "이전", 14)
    button(b + "/NextButton", BTN_BG, (1, 0), (1, 0), (-158, 28), (-28, 68), "다음", 14)
    button(b + "/QuitButton", BTN_OFF, (0.5, 0), (0.5, 0), (-70, 28), (70, 68), "그만 보기", 13)

    # ⚠ 안내는 <b>꺼둔 상태</b>로 저장한다 — 카드/백과가 띄운다.
    go(p, active=False)


# ══════════════════════════════════════════════════════════════════════════
# ⑤ 액션 버튼 「도움말」 + 칸 높이
#
# ⚠ 형제 버튼들과 <b>같은 구성</b>이어야 한다(Image + Button + LayoutElement + Label) —
#   VerticalLayoutGroup 이 높이를 LayoutElement 로 잡는다.
# ★ 높이는 실행 중에 ActionPanel 이 <b>켜져 있는 자식을 세어</b> 다시 맞춘다.
#   여기서 굽는 것은 «에디터에서 봤을 때도 맞아 보이게» 하기 위한 것이다.
# ══════════════════════════════════════════════════════════════════════════
def build_action_button():
    b = "UI_Root/HUD_Actions/Buttons/HelpButton"
    go(b)
    rect(b, (0, 1), (1, 1), (0, -40), (0, 0))
    image(b, BTN_OFF)
    comp(b, "Button", {})
    comp(b, "LayoutElement", {"minHeight": BUTTON_H, "preferredHeight": BUTTON_H})
    label(b + "/Label", "도움말 (F1)", 16, TEXT, "Center", (0, 0), (1, 1), (6, 0), (-6, 0))

    h = VISIBLE_BUTTONS * BUTTON_H + (VISIBLE_BUTTONS - 1) * BUTTON_GAP + PANEL_PAD
    comp("UI_Root/HUD_Actions", "RectTransform", {"sizeDelta": {"x": 260, "y": h}})
    print("  HUD_Actions 높이 = %d (보이는 버튼 %d개)" % (h, VISIBLE_BUTTONS))


# ══════════════════════════════════════════════════════════════════════════
# ⑥ 서비스
# ══════════════════════════════════════════════════════════════════════════
def build_services():
    # ★ 코드에도 EnsureOn 이 있지만(HelpService), <b>인스펙터에서 켜고 끄려면 실물이 있어야</b>
    #   한다 — HudHotkeys 를 씬에 직접 붙여 둔 것과 같은 이유다.
    comp("GameSystems", "HelpService", {})


def run():
    build_root()
    build_panel()
    build_card()
    build_tour()
    build_action_button()
    build_services()
    call("save_scene", {})

    tmp = os.path.join(PROJECT, "Temp", "mcp_help_ui.json")
    os.makedirs(os.path.dirname(tmp), exist_ok=True)
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(_requests, f, ensure_ascii=False, indent=1)

    print("[도움말 UI · MCP] 요청 %d건" % len(_requests))
    out = subprocess.run(["node", CLI, "--batch", tmp],
                         capture_output=True, text=True, encoding="utf-8")
    bad = 0
    for line in (out.stdout or "").splitlines():
        if '"error"' in line or '"success": false' in line:
            bad += 1
            print("  ⚠ " + line.strip())
    if out.returncode != 0:
        print((out.stdout or "")[-3000:])
        print((out.stderr or "")[-2000:])
    print("  → 실패 %d건 (0 이어야 합니다)" % bad)
    print("  요청 목록: %s" % os.path.relpath(tmp, PROJECT))
    print("  다음: 유니티 메뉴 LastSanctuary/폰트/네오 둥근모 TMP 에셋 굽고 씬에 적용")
    return 1 if (bad or out.returncode != 0) else 0


if __name__ == "__main__":
    sys.exit(run())
