# -*- coding: utf-8 -*-
"""로비 환경 설정 창에 «사람에 딸린» 설정 버튼을 붙인다 (2026-08-26).

유저 지시
---------
  · *"로비화면 환경 설정에도 환경 설정 버튼들 같이 붙여주고"*

무엇을 하나
-----------
게임 중 환경 설정(``HUD_Settings``)의 버튼은 두 갈래다.

    판에 딸린 것   : 저장 · 로비로 · 다시 시작 · 저장 않고 나가기   → 로비에는 없다
    사람에 딸린 것 : 음량 · 언어 · 단축키 · 도움말 기억             → 로비에도 있어야 한다

뒤쪽 넷은 전부 ``PlayerPrefs`` 에 남는 값이라 <b>어느 씬에서 만져도 같다</b>.
로비 창에는 음량뿐이었으므로 <b>언어 · 단축키 · 도움말 다시 보기</b> 셋을 더한다.

    SettingsWindow 520x260 → 520x400
      Body/LanguageButton        (0,   0) 0 x 46   ← 온폭
      Body/HotkeyButton          (-2, -56) 반폭     ┐ 게임 쪽 창과 같은 «반폭 둘»
      Body/HelpResetButton       ( 2, -56) 반폭     ┘
      Body/Volume                (0, -112) 0 x 44   ← 있던 것을 내린다
      Body/Status                (0, -166) -14 x 26 ← 새로 만든다
      Body/Copyright             바닥 고정(그대로)

    UI_Root/HUD_Hotkeys  ← 새로 만든다(빈 루트 + HotkeyPanel)
      ★ 단축키 창은 <b>루트 하나만 씬에 두고 안쪽을 스스로 짓는다</b>
        (``HotkeyPanel.Build``). 그래서 로비에도 빈 루트 하나면 된다.
      ⚠ 루트는 <b>켜 둔 채로</b> 저장한다 — 끄면 ``Awake`` 가 안 돌아
        ``HotkeyPanel.Instance`` 가 영영 null 이다(그 클래스의 ⚠ 주석 그대로).
        창을 여닫는 것은 그 안의 ``Body`` 다.

⚠⚠ <b>돌린 뒤에 유니티 메뉴 셋을 실행할 것</b> (이 스크립트가 마지막에 같이 부른다)
      LastSanctuary/UI/배선                       ← 새 버튼에 그림을 꽂는다
      LastSanctuary/UI/글자 여백                  ← 글자가 장식에 안 닿게
      LastSanctuary/폰트/네오 둥근모 TMP 에셋 굽고 씬에 적용  ← 새 글자의 한글이 안 깨지게
    MCP 로는 스프라이트·폰트 «참조» 를 넣을 수 없다(진행상황 8절 4번).

⚠ 멱등하다 — ``update_gameobject``/``update_component`` 가 «없으면 만들고 있으면 고친다».
⚠ 씬을 갈아 끼우므로 <b>Proto_01 을 먼저 저장해 둘 것</b>. 이 스크립트는 마지막에
  Proto_01 로 되돌려 놓는다.

사용법:  python Tools/mcp_lobby_settings_20260826.py   (유니티 에디터가 켜져 있어야 한다)
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

LOBBY = "Assets/Scenes/Lobby.unity"
GAME = "Assets/Scenes/Proto_01.unity"

WIN = "UI_Root/Lobby/SettingsWindow"
BODY = WIN + "/Body"

TEXT = {"r": 0.88, "g": 0.92, "b": 0.94, "a": 1.0}
DIM = {"r": 0.58, "g": 0.64, "b": 0.70, "a": 1.0}

ROW_H = 46          # 버튼 한 칸 — 게임 쪽 환경 설정과 같은 값
ROW_GAP = 10

_requests = []


def call(method, params):
    _requests.append({"method": method, "params": params})


def go(path, active=True, layer=5):
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


def text(path, value, size=18, color=None, align="Left"):
    """⚠ 정렬은 «이름» 으로 준다 — 브리지가 enum 을 인덱스로 해석한다(mcp_build_relic_ui 의 ⚠).
    ⚠ TMP 의 «있을 것 같은» 칸을 넣지 말 것 — 하나라도 없는 칸이 섞이면 요청 전체가 거절된다."""
    comp(path, "TextMeshProUGUI", {
        "m_text": value,
        "m_fontSize": size,
        "m_fontColor": color or TEXT,
        "m_textAlignment": align,
    })


def button(path, label_text, top, left_frac=0.0, right_frac=1.0, pad=(0, 0)):
    """창 Body 안의 «가로 띠» 버튼 하나. ``top`` 은 Body 위에서 내려온 거리(양수).

    ★ 그림은 안 꽂는다 — ``LastSanctuary/UI/배선`` 이 렉트 비율을 보고 골라 준다.
      실행 중에도 ``HudTheme.EnsureButtonSkin`` 이 한 번 더 받쳐 준다.
    """
    go(path)
    rect(path,
         (left_frac, 1.0), (right_frac, 1.0),
         (pad[0], -(top + ROW_H)), (pad[1], -top),
         pivot=(0.5, 1.0))
    comp(path, "Image", {"raycastTarget": True})
    comp(path, "Button", {})

    lab = path + "/Label"
    go(lab)
    rect(lab, (0, 0), (1, 1), (8, 0), (-8, 0))
    text(lab, label_text, 18, TEXT, "Center")


# ══════════════════════════════════════════════════════════════════════════
def build():
    call("load_scene", {"scenePath": LOBBY})

    # ── 창을 키운다 — 버튼 두 줄 + 음량 + 상태 + 저작권이 들어가야 한다 ──
    comp(WIN, "RectTransform", {"sizeDelta": {"x": 520, "y": 400}})

    y = 0
    button(BODY + "/LanguageButton", "언어 : 한국어", y)

    y += ROW_H + ROW_GAP                       # 56
    button(BODY + "/HotkeyButton", "단축키 설정", y, 0.0, 0.5, pad=(0, -4))
    button(BODY + "/HelpResetButton", "도움말 다시 보기", y, 0.5, 1.0, pad=(4, 0))

    y += ROW_H + ROW_GAP                       # 112
    rect(BODY + "/Volume", (0, 1), (1, 1), (0, -(y + 44)), (0, -y), pivot=(0.5, 1.0))

    y += 44 + ROW_GAP                          # 166
    go(BODY + "/Status")
    rect(BODY + "/Status", (0, 1), (1, 1), (7, -(y + 26)), (-7, -y), pivot=(0.5, 1.0))
    text(BODY + "/Status", "", 15, DIM, "Center")

    # ── 단축키 창의 «빈 루트» ─────────────────────────────────────────
    # ⚠ 켜 둔 채로 저장한다 — 끄면 Awake 가 안 돌아 Instance 가 null 이다.
    go("UI_Root/HUD_Hotkeys", active=True)
    rect("UI_Root/HUD_Hotkeys", (0, 0), (1, 1), (0, 0), (0, 0))
    comp("UI_Root/HUD_Hotkeys", "HotkeyPanel", {})

    # ── 참조를 넣는 일은 에디터 메뉴가 한다 (MCP 는 못 넣는다) ────────
    call("execute_menu_item", {"menuPath": "LastSanctuary/UI/배선"})
    call("execute_menu_item", {"menuPath": "LastSanctuary/UI/글자 여백"})
    call("execute_menu_item",
         {"menuPath": "LastSanctuary/폰트/네오 둥근모 TMP 에셋 굽고 씬에 적용"})

    call("save_scene", {})

    # 작업 씬을 원래대로 돌려놓는다 — 에디터를 켜 둔 사람의 자리를 안 바꾼다.
    call("load_scene", {"scenePath": GAME})


def run():
    build()

    tmp = os.path.join(PROJECT, "Temp", "mcp_lobby_settings_20260826.json")
    os.makedirs(os.path.dirname(tmp), exist_ok=True)
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(_requests, f, ensure_ascii=False, indent=1)

    print("[로비 환경설정 · MCP] 요청 %d건" % len(_requests))
    out = subprocess.run(["node", CLI, "--batch", tmp],
                         capture_output=True, text=True, encoding="utf-8")
    bad = 0
    for line in (out.stdout or "").splitlines():
        if '"error"' in line or '"success": false' in line:
            bad += 1
            print("  ⚠ " + line.strip())
    if out.returncode != 0:
        print(out.stdout)
        print(out.stderr)
        sys.exit(out.returncode)
    print("  실패 %d건" % bad)


if __name__ == "__main__":
    run()
