# -*- coding: utf-8 -*-
"""HUD 씬 손질 — 액션 패널 · 창 드래그 (2026-08-24).

유저 지시:
  · *"지금 허드 액션이 하드 코딩 되어 있음 건설 버튼 비활성화 유물관리 넣고
     허드 액션 크기 맞춰 mcp 로 직접"*
  · *"ui 들 스크롤 해서 창 옮길 수 있게 해줘 이벤트나 유물관리 ui도 다"*

무엇을 하나
-----------
① ``UI_Root/HUD_Actions/Buttons/BuildButton`` 을 <b>끈다</b>.
   건설은 이미 걷어낸 기능이다(UI-50 이 *"기존에 삭제된 건설처럼"* 이라고 적었다) —
   버튼만 남아 «눌러도 아무 일이 없는 칸» 이 되어 있었다.
   ⚠ <b>지우지 않는다</b> — 씬 오브젝트 삭제는 되돌리기 어렵고, `BuildButtonUI` 와
     `BuildService` 는 그대로 있으므로 되살릴 때 이 한 줄만 바꾸면 된다.

② ``HUD_Actions`` 높이를 <b>남은 버튼 수에 맞춘다</b>.

       보이는 버튼 7개 x 40 + 사이 6칸 x 8 = 328  →  안쪽 여백 20 을 더해 348

   ★ 값은 여기(씬)와 <see cref="ActionPanel"/> <b>두 곳에서</b> 정해진다 —
     실행 중에는 `ActionPanel` 이 <b>켜져 있는 자식을 세어</b> 다시 맞춘다(하드코딩 제거).
     여기서 굽는 것은 «에디터에서 봤을 때도 맞아 보이게» 하기 위한 것이다.

③ 창 셋에 <b>끌어 옮기기</b>(`UiWindowDrag`)를 붙인다 — ``HUD_Event`` ·
   ``HUD_SkillDetail`` · ``HUD_Portrait``. 나머지 여섯(설정·전술·성장·유물·토벌·부대)은
   2026-08-18 에 이미 붙어 있다.
   ⚠ ``HUD_Portrait`` 는 배경 Image 의 ``raycastTarget`` 이 <b>꺼져 있었다</b> —
     그대로는 포인터를 못 받아 드래그가 시작되지 않는다. 같이 켠다.

⚠ 멱등하다 — `update_gameobject`/`update_component` 가 «없으면 만들고 있으면 고친다».
⚠ 씬 저장은 마지막 한 번.

사용법:  python Tools/mcp_hud_20260824.py   (유니티 에디터가 켜져 있어야 한다)
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

#: 액션 버튼 한 칸의 높이·간격 — 씬의 LayoutElement / VerticalLayoutGroup 실측값.
BUTTON_H = 40
BUTTON_GAP = 8
PANEL_PAD = 20        # Buttons 컨테이너가 sizeDelta -20 으로 안쪽에 들어가 있다
VISIBLE_BUTTONS = 7   # 건설을 끈 뒤 남는 수

_requests = []


def call(method, params):
    _requests.append({"method": method, "params": params})


def go(path, active=True, layer=5):
    call("update_gameobject", {"objectPath": path,
                               "gameObjectData": {"activeSelf": active, "layer": layer}})


def comp(path, name, data=None):
    call("update_component", {"objectPath": path, "componentName": name,
                              "componentData": data or {}})


# ══════════════════════════════════════════════════════════════════════════
def build_actions():
    go("UI_Root/HUD_Actions/Buttons/BuildButton", active=False)

    h = VISIBLE_BUTTONS * BUTTON_H + (VISIBLE_BUTTONS - 1) * BUTTON_GAP + PANEL_PAD
    comp("UI_Root/HUD_Actions", "RectTransform", {"sizeDelta": {"x": 260, "y": h}})
    print("  HUD_Actions 높이 = %d (버튼 %d개)" % (h, VISIBLE_BUTTONS))


def build_drag():
    for path in ("UI_Root/HUD_Event", "UI_Root/HUD_SkillDetail", "UI_Root/HUD_Portrait"):
        comp(path, "UiWindowDrag", {})

    # 초상화 카드만 배경이 포인터를 안 받고 있었다 — 켜야 드래그가 시작된다.
    comp("UI_Root/HUD_Portrait", "Image", {"raycastTarget": True})


def run():
    build_actions()
    build_drag()
    call("save_scene", {})

    tmp = os.path.join(PROJECT, "Temp", "mcp_hud_20260824.json")
    os.makedirs(os.path.dirname(tmp), exist_ok=True)
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(_requests, f, ensure_ascii=False, indent=1)

    print("[HUD · MCP] 요청 %d건" % len(_requests))
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
    return 1 if (bad or out.returncode != 0) else 0


if __name__ == "__main__":
    sys.exit(run())
