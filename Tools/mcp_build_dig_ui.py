# -*- coding: utf-8 -*-
"""발굴 확인 창(`UI_Root/HUD_Dig`)을 MCP 로 만든다 (2026-08-24).

유저 지시: *"유물이 발견된 칸에 별도의 ui로 버튼을 생성하고 해당 버튼은 캐릭터/몬스터 등
개체 클릭보다 상위로 두고 해당 칸을 누를 경우 발굴 ui가 나와서 발굴하기를 누르면 가장 가까운
캐릭터가 가서 발굴하게 해줘"* · *"mcp 사용해서 직접 생성해"* ·
*"발굴 가능 칸이 발견되면 생성되는 발굴 ui 버튼은 느낌표로 처리"*.

무엇을 만드나
-------------
``UI_Root/HUD_Dig`` 하나다. 표식(느낌표)은 <b>이미 있다</b> — UI-50 이 만든
``UI_Root/DigOverlay/DigMarkerTemplate`` 이고, 유저 지시대로 <b>느낌표 그대로 둔다</b>.

★★ <b>«개체 클릭보다 상위» 는 이미 성립한다</b> — 확인해 보니 두 겹으로 되어 있었다:
  ① ``DigOverlay`` 는 <b>자기 Canvas + GraphicRaycaster</b> 를 갖고 ``sortingOrder = 5`` 다
     (``UI_Root`` 는 0 · 건설/집결지 오버레이는 −1). 그래서 표식이 다른 UI 위에 그려진다.
  ② ``UnitSelector`` 는 <b>포인터가 UI 위면 월드 클릭을 버린다</b>
     (`EventSystem.current.IsPointerOverGameObject()`). 그래서 유닛이 표식 아래 서 있어도
     표식이 먼저 눌린다.
  → 이번에 <b>새로 할 일은 없다</b>. 대신 아래 ★ 를 조심해야 한다.

★ <b>창에도 자기 Canvas 를 준다</b>(``sortingOrder = 6``). ``HUD_Dig`` 를 그냥
  ``UI_Root``(0) 아래 두면 <b>표식(5)이 창 위에 그려진다</b> — 창을 열어 놓고도 느낌표가
  글자를 뚫고 보인다. 창은 표식보다 위여야 한다.
  ⚠ Canvas 를 겹치면 <b>GraphicRaycaster 도 같이</b> 붙여야 한다 — 없으면 그 아래 버튼이
    클릭을 못 받는다(DigOverlay 가 같은 이유로 둘을 함께 붙였다).

⚠ 멱등하다 — `update_gameobject`/`update_component` 가 «없으면 만들고 있으면 고친다».
⚠ 돌린 뒤 <b>폰트 메뉴를 반드시 실행할 것</b> — MCP 로는 폰트 «참조» 를 못 넣어서
  새 글자가 TMP 기본 폰트로 태어난다(UI-52-1 에서 겪은 그 함정).

사용법:  python Tools/mcp_build_dig_ui.py   (유니티 에디터가 켜져 있어야 한다)
다음:    유니티 메뉴 LastSanctuary/폰트/네오 둥근모 TMP 에셋 굽고 씬에 적용
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

PANEL_BG = {"r": 0.07, "g": 0.08, "b": 0.10, "a": 0.97}
BOX_BG = {"r": 0.10, "g": 0.12, "b": 0.15, "a": 0.92}
BTN_BG = {"r": 0.16, "g": 0.42, "b": 0.38, "a": 0.98}
BTN_OFF = {"r": 0.13, "g": 0.17, "b": 0.22, "a": 0.95}
CLOSE_BG = {"r": 0.30, "g": 0.13, "b": 0.15, "a": 0.95}
TEXT = {"r": 0.88, "g": 0.91, "b": 0.95, "a": 1.0}
DIM = {"r": 0.62, "g": 0.68, "b": 0.75, "a": 1.0}

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


# ⚠⚠ <b>`wrap` 같은 «쓰지 않는 인자» 를 두지 말 것</b> — UI-52-1 에서 그 칸 하나 때문에
#   좌표 네 개가 한 칸씩 밀려 유물 창 글자가 통째로 화면 밖으로 나갔다.
def text(path, value, size=16, color=None, align="Left"):
    """⚠ 정렬은 <b>이름</b>으로 준다 — 브리지가 enum 을 인덱스로 해석한다(UI-50)."""
    comp(path, "TextMeshProUGUI", {
        "m_text": value,
        "m_fontSize": size,
        "m_fontColor": color or TEXT,
        "m_textAlignment": align,
        # ⚠ TMP 에 없는 칸(m_enableWordWrapping·m_raycastTarget)을 하나라도 넣으면
        #   브리지가 요청 «전체» 를 거절해 글자·색까지 조용히 안 들어간다(UI-50 실측).
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
# 발굴 확인 창
#
# 560 x 340 · 화면 한가운데. 이벤트 창(HUD_Event)과 <b>같은 결</b>로 잡았다 —
# 두 창이 «묻고 답한다» 는 같은 일을 하므로 조작감이 갈리면 안 된다.
# ══════════════════════════════════════════════════════════════════════════
def build_panel():
    p = "UI_Root/HUD_Dig"
    go(p)
    rect(p, (0.5, 0.5), (0.5, 0.5), (-280, -170), (280, 170))
    image(p, PANEL_BG)

    # ★ 표식(sortingOrder 5)보다 위. 레이캐스터를 함께 붙인다(맨 위 ★).
    comp(p, "Canvas", {"overrideSorting": True, "sortingOrder": 6})
    comp(p, "GraphicRaycaster", {})
    comp(p, "RelicDigPanel", {})
    comp(p, "UiWindowDrag", {})

    label(p + "/Title", "발굴 가능한 자리", 22, TEXT, "Left",
          (0, 1), (1, 1), (18, -46), (-56, -12))

    c = p + "/CloseButton"
    go(c)
    rect(c, (1, 1), (1, 1), (-44, -44), (-12, -12))
    image(c, CLOSE_BG)
    comp(c, "Button", {})
    label(c + "/Label", "X", 18, TEXT, "Center", (0, 0), (1, 1), (0, 0), (0, 0))

    # 아이콘 — 결과·보스 드랍에서만 켜진다(코드가 끈다).
    ico = p + "/Icon"
    go(ico)
    rect(ico, (0, 1), (0, 1), (18, -124), (82, -60))
    image(ico, {"r": 1, "g": 1, "b": 1, "a": 1}, raycast=False)

    # ★ 본문 칸을 바닥까지 내렸다 (2026-08-24 · *"텍스트가 짤리지 않도록"*) —
    #   창은 340 높이고 선택지 버튼 위쪽이 −224 인데 본문은 −200 에서 끊겨 있었다.
    #   결과 단계에서는 «result 대사 + 빈 줄 + 발굴 결과» 가 붙어 가장 길어진다.
    # ⚠ 넘침 방지 자체는 코드가 한다(`RelicDigPanel.Bind` → `HudTheme.FitText`).
    label(p + "/Body", "", 16, TEXT, "TopLeft",
          (0, 1), (1, 1), (96, -216), (-18, -56))

    # 선택지 둘 — 발견 단계에서만. 가로로 길게(문장이 길다).
    button(p + "/Choice0", BTN_BG, (0, 0), (1, 0), (18, 72), (-18, 116),
           "가까이 가서 살펴본다.")
    button(p + "/Choice1", BTN_OFF, (0, 0), (1, 0), (18, 20), (-18, 64),
           "방심은 금물이다. 그냥 두자.")

    # 확인 — 답변·결과 단계에서만. 가운데 짧게(«읽었다» 만 하는 버튼이다).
    button(p + "/ConfirmButton", BTN_BG, (0.5, 0), (0.5, 0), (-80, 20), (80, 64), "확인")

    # ⚠ 창은 <b>꺼둔 상태</b>로 저장한다 — 다른 HUD 창과 같은 규칙.
    go(p, active=False)


def run():
    build_panel()
    call("save_scene", {})

    tmp = os.path.join(PROJECT, "Temp", "mcp_dig_ui.json")
    os.makedirs(os.path.dirname(tmp), exist_ok=True)
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(_requests, f, ensure_ascii=False, indent=1)

    print("[발굴 창 · MCP] 요청 %d건" % len(_requests))
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
    print("  실패 %d건" % bad)
    print("  다음: 유니티 메뉴 LastSanctuary/폰트/네오 둥근모 TMP 에셋 굽고 씬에 적용")
    return 1 if (bad or out.returncode != 0) else 0


if __name__ == "__main__":
    sys.exit(run())
