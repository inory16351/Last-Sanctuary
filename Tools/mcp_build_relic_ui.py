# -*- coding: utf-8 -*-
"""유물 시스템의 <b>씬 오브젝트</b>를 MCP 로 만든다 (2026-08-23 신설).

유저 지시: *"모든 객체 생성 및 수정은 템플릿/슬롯 복제 제외 mcp 사용해서 직접 생성 및 수정,
불가피한 경우 아니면 하드코딩 하지마"*.

★ 왜 «스크립트로 MCP 를 부르는가»
----------------------------------
만들 오브젝트가 <b>40개가 넘는다</b>(유물 창 하나에 목록·상세·버튼이 스물 몇 개다).
한 번에 하나씩 손으로 부르면 ① 중간에 끊기면 어디까지 했는지 모르고 ② <b>다시 만들 수
없다</b>(씬이 깨졌을 때 되돌릴 방법이 없다). 그래서 <b>순서를 파일로</b> 적는다 —
이 프로젝트가 표·에셋에 대해 이미 쓰는 방식(`gen_*_assets.py`)과 같은 이유다.

⚠ <b>멱등하다</b> — `update_gameobject` 는 경로가 없으면 만들고 있으면 고친다.
  `update_component` 도 컴포넌트가 없으면 붙인다. 그래서 몇 번을 돌려도 결과가 같다.

⚠ <b>씬 저장은 마지막에 한 번</b>(`save_scene`) — 38MB 씬이라 저장이 비싸다.

무엇을 만드나
-------------
| 경로 | 무엇 |
|---|---|
| ``UI_Root/DigOverlay`` | 발굴 느낌표를 그리는 캔버스(+ 레이캐스터). 표식 원본 하나 |
| ``UI_Root/HUD_Relics`` | 유물 관리 창 — 왼쪽 목록 · 오른쪽 상세 |
| ``UI_Root/HUD_Actions/Buttons/RelicButton`` | 액션 버튼 「유물 관리」 |
| ``UI_Root/HUD_Growth/Stats/RelicSlot`` | 성장 창의 유물 장착 칸 |
| ``GameSystems`` | ``RelicInventory`` · ``RelicDigService`` 컴포넌트 |

사용법:  py -3 Tools/mcp_build_relic_ui.py
⚠ 유니티 에디터가 켜져 있어야 한다.
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

#: HUD 색 — 다른 창과 같은 값을 쓴다(HudTheme 의 대역).
PANEL_BG = {"r": 0.07, "g": 0.08, "b": 0.10, "a": 0.96}
BOX_BG = {"r": 0.10, "g": 0.12, "b": 0.15, "a": 0.92}
ROW_BG = {"r": 0.11, "g": 0.13, "b": 0.17, "a": 0.92}
BTN_BG = {"r": 0.16, "g": 0.42, "b": 0.38, "a": 0.98}
MARK_BG = {"r": 0.98, "g": 0.85, "b": 0.35, "a": 0.95}
TEXT = {"r": 0.88, "g": 0.91, "b": 0.95, "a": 1.0}
DIM = {"r": 0.62, "g": 0.68, "b": 0.75, "a": 1.0}

_requests = []


def call(method, params):
    _requests.append({"method": method, "params": params})


def go(path, active=True, layer=5):
    """오브젝트 하나 — 없으면 만들고 있으면 켜기/끄기만 고친다."""
    call("update_gameobject", {"objectPath": path,
                               "gameObjectData": {"activeSelf": active, "layer": layer}})


def comp(path, name, data=None):
    call("update_component", {"objectPath": path, "componentName": name,
                              "componentData": data or {}})


def rect(path, anchor_min, anchor_max, offset_min, offset_max, pivot=(0.5, 0.5)):
    comp(path, "RectTransform", {
        "anchorMin": {"x": anchor_min[0], "y": anchor_min[1]},
        "anchorMax": {"x": anchor_max[0], "y": anchor_max[1]},
        "offsetMin": {"x": offset_min[0], "y": offset_min[1]},
        "offsetMax": {"x": offset_max[0], "y": offset_max[1]},
        "pivot": {"x": pivot[0], "y": pivot[1]},
    })


def image(path, color, raycast=True):
    comp(path, "Image", {"color": color, "raycastTarget": raycast})


def text(path, value, size=18, color=None, align="Left", wrap=True):
    """⚠ <b>정렬은 «이름» 으로 준다</b> — MCP 브리지가 enum 을 <b>인덱스</b>로 해석해서
    TMP 의 실제 값(513 = Left)을 넘기면 «Enum index out of range» 로 죽는다(실측).
    쓰는 값: ``Left`` · ``Center`` · ``Right`` · ``TopLeft`` …"""
    comp(path, "TextMeshProUGUI", {
        "m_text": value,
        "m_fontSize": size,
        "m_fontColor": color or TEXT,
        "m_textAlignment": align,
        # ⚠⚠ <b>TMP 의 «있을 것 같은» 칸을 넣지 말 것</b> — 이 버전에는
        #   `m_enableWordWrapping`(→ `m_TextWrappingMode`) 도 `m_raycastTarget` 도 없다.
        #   없는 칸을 <b>하나라도</b> 넣으면 브리지가 요청 <b>전체</b>를 거절해서
        #   글자·크기·색까지 통째로 안 들어간다(실측: 그래서 16건이 조용히 비어 있었다).
        #   줄바꿈·레이캐스트는 기본값으로 둔다 — 이 창들에는 문제가 없다.
    })


def label(path, value, size=18, color=None, align="Center", wrap=True,
          amin=(0, 0), amax=(1, 1), omin=(6, 0), omax=(-6, 0)):
    go(path)
    rect(path, amin, amax, omin, omax)
    text(path, value, size, color, align, wrap)


# ══════════════════════════════════════════════════════════════════════════
# ① 발굴 오버레이 — 느낌표 표식을 그리는 캔버스
#
# ★ <b>Canvas + GraphicRaycaster 를 둘 다</b> 붙인다. 표식은 «누를 수 있어야» 하고
#   (유저 지시 3번), 레이캐스터가 없으면 클릭이 안 들어간다.
#   ⚠ 집결지·건설 오버레이와 같은 층에 두되 <b>그 위</b>에 그린다(sortingOrder 5) —
#     표식이 범위 사각형에 가려지면 못 누른다.
# ══════════════════════════════════════════════════════════════════════════
def build_dig_overlay():
    p = "UI_Root/DigOverlay"
    go(p)
    rect(p, (0, 0), (1, 1), (0, 0), (0, 0))
    comp(p, "Canvas", {"overrideSorting": True, "sortingOrder": 5})
    comp(p, "GraphicRaycaster", {})

    t = p + "/DigMarkerTemplate"
    go(t)
    rect(t, (0.5, 0.5), (0.5, 0.5), (-17, -17), (17, 17))
    image(t, MARK_BG)
    comp(t, "Button", {})
    label(t + "/Label", "!", 22, {"r": 0.10, "g": 0.09, "b": 0.06, "a": 1.0}, "Center", False)
    # ⚠ 원본은 <b>꺼둔다</b> — 복제해서 쓰는 모체다(건설 오버레이와 같은 규칙).
    go(t, active=False)


# ══════════════════════════════════════════════════════════════════════════
# ② 유물 관리 창
#
# 배치 — 왼쪽 목록(폭 300) · 오른쪽 상세(나머지). 토벌 창과 같은 모양이다.
# ══════════════════════════════════════════════════════════════════════════
def build_relic_panel():
    p = "UI_Root/HUD_Relics"
    go(p)
    rect(p, (0.5, 0.5), (0.5, 0.5), (-380, -240), (380, 240))
    image(p, PANEL_BG)
    comp(p, "RelicPanel", {})
    comp(p, "UiWindowDrag", {})

    # ── 머리 ──
    label(p + "/Header", "유물 관리", 22, TEXT, "Left",
          (0, 1), (1, 1), (18, -46), (-56, -10))

    c = p + "/CloseButton"
    go(c)
    rect(c, (1, 1), (1, 1), (-44, -44), (-12, -12))
    image(c, {"r": 0.30, "g": 0.13, "b": 0.15, "a": 0.95})
    comp(c, "Button", {})
    label(c + "/Label", "X", 18, TEXT, "Center", False)

    label(p + "/Hint", "유물을 고르면 설명이 나옵니다.", 14, DIM, "Left",
          (0, 0), (1, 0), (18, 10), (-18, 34))

    # ── 목록 (왼쪽) ──
    box = p + "/List"
    go(box)
    rect(box, (0, 0), (0, 1), (14, 40), (314, -52))
    image(box, BOX_BG, raycast=False)

    items = box + "/Items"
    go(items)
    rect(items, (0, 1), (1, 1), (6, -6), (-6, -6), pivot=(0.5, 1))
    comp(items, "VerticalLayoutGroup", {"spacing": 4, "childForceExpandHeight": False,
                                        "childControlHeight": False, "childControlWidth": True,
                                        "childForceExpandWidth": True})
    comp(items, "ContentSizeFitter", {"verticalFit": 2})

    row = box + "/RowTemplate"
    go(row)
    rect(row, (0, 1), (1, 1), (6, -50), (-6, -6))
    image(row, ROW_BG)
    comp(row, "Button", {})
    comp(row, "LayoutElement", {"minHeight": 42, "preferredHeight": 42})

    ico = row + "/Icon"
    go(ico)
    rect(ico, (0, 0.5), (0, 0.5), (6, -17), (40, 17))
    image(ico, {"r": 1, "g": 1, "b": 1, "a": 1}, raycast=False)

    label(row + "/Name", "유물", 16, TEXT, "Left", (0, 0), (1, 1), (48, 0), (-46, 0))
    label(row + "/Count", "", 14, DIM, "Right", (1, 0), (1, 1), (-44, 0), (-8, 0))
    go(row, active=False)     # ⚠ 원본은 꺼둔다

    # ── 상세 (오른쪽) ──
    d = p + "/Detail"
    go(d)
    rect(d, (0, 0), (1, 1), (324, 40), (-14, -52))
    image(d, BOX_BG, raycast=False)

    di = d + "/Icon"
    go(di)
    rect(di, (0, 1), (0, 1), (14, -78), (78, -14))
    image(di, {"r": 1, "g": 1, "b": 1, "a": 1}, raycast=False)

    label(d + "/Name", "-", 22, TEXT, "Left", (0, 1), (1, 1), (90, -46), (-14, -14))
    label(d + "/Grade", "", 15, DIM, "Left", (0, 1), (1, 1), (90, -76), (-14, -48))
    label(d + "/Effect", "", 16, TEXT, "TopLeft", (0, 1), (1, 1), (14, -170), (-14, -92))
    label(d + "/Flavor", "", 14, DIM, "TopLeft", (0, 1), (1, 1), (14, -250), (-14, -174))
    label(d + "/Source", "", 13, DIM, "Left", (0, 0), (1, 0), (14, 58), (-14, 84))
    label(d + "/Wearer", "", 13, DIM, "Left", (0, 0), (1, 0), (14, 34), (-14, 58))

    e = d + "/EquipButton"
    go(e)
    rect(e, (0, 0), (0, 0), (14, 8), (150, 44))
    image(e, BTN_BG)
    comp(e, "Button", {})
    label(e + "/Label", "장착", 16, TEXT, "Center", False)

    # ⚠ 창은 <b>꺼둔 상태</b>로 저장한다 — 다른 HUD 창과 같은 규칙(ActionPanel 이 연다).
    go(p, active=False)


# ══════════════════════════════════════════════════════════════════════════
# ③ 액션 버튼 「유물 관리」
#
# ⚠ 형제 버튼들과 <b>같은 구성</b>이어야 한다(Image + Button + LayoutElement + Label) —
#   VerticalLayoutGroup 이 높이를 LayoutElement 로 잡는다.
# ══════════════════════════════════════════════════════════════════════════
def build_action_button():
    b = "UI_Root/HUD_Actions/Buttons/RelicButton"
    go(b)
    rect(b, (0, 1), (1, 1), (0, -40), (0, 0))
    image(b, {"r": 0.13, "g": 0.17, "b": 0.22, "a": 0.95})
    comp(b, "Button", {})
    comp(b, "LayoutElement", {"minHeight": 40, "preferredHeight": 40})
    label(b + "/Label", "유물 관리", 16, TEXT, "Center", False)


# ══════════════════════════════════════════════════════════════════════════
# ④ 성장 창의 유물 칸 (유저 지시 10번)
# ══════════════════════════════════════════════════════════════════════════
def build_growth_slot():
    s = "UI_Root/HUD_Growth/Stats/RelicSlot"
    go(s)
    rect(s, (0, 0), (1, 0), (12, 8), (-12, 66))
    image(s, BOX_BG)
    comp(s, "Button", {})

    label(s + "/Head", "유물", 14, DIM, "Left", (0, 1), (0, 1), (10, -26), (60, -6))

    ico = s + "/Icon"
    go(ico)
    rect(ico, (0, 0), (0, 0), (10, 6), (48, 44))
    image(ico, {"r": 1, "g": 1, "b": 1, "a": 1}, raycast=False)

    label(s + "/Name", "없음", 16, DIM, "Left", (0, 0), (1, 1), (56, 4), (-12, -28))


# ══════════════════════════════════════════════════════════════════════════
# ⑤ 서비스 컴포넌트
# ══════════════════════════════════════════════════════════════════════════
def build_services():
    comp("GameSystems", "RelicInventory", {})
    comp("GameSystems", "RelicDigService", {})


def run():
    build_dig_overlay()
    build_relic_panel()
    build_action_button()
    build_growth_slot()
    build_services()
    call("save_scene", {})

    tmp = os.path.join(PROJECT, "Temp", "mcp_relic_ui.json")
    os.makedirs(os.path.dirname(tmp), exist_ok=True)
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(_requests, f, ensure_ascii=False, indent=1)

    print("[유물 UI · MCP] 요청 %d건" % len(_requests))
    out = subprocess.run(["node", CLI, "--batch", tmp],
                         capture_output=True, text=True, encoding="utf-8")
    bad = 0
    for line in (out.stdout or "").splitlines():
        if '"error"' in line or '"success": false' in line:
            bad += 1
            print("  ⚠ " + line.strip())
    if out.returncode != 0:
        print(out.stdout[-3000:])
        print(out.stderr[-2000:])
    print("  → 실패 %d건 (0 이어야 합니다)" % bad)
    print("  요청 목록: %s" % os.path.relpath(tmp, PROJECT))


if __name__ == "__main__":
    run()
