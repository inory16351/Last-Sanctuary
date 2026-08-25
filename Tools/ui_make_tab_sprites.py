# -*- coding: utf-8 -*-
"""<b>분류 탭 전용 «밝은» 버튼 그림</b>을 만든다 (2026-08-26 신설).

유저 지시: *"도움말 ui 위 쪽 메뉴 이미지들 밝은 색 이미지로 변경 가시성이 너무 안 좋음"*

★★ <b>왜 새로 만드나</b>
-----------------------
도움말 창 위쪽의 분류 탭(기본·전투·성장·지휘·위험·운영)은 «창 안의 보통 버튼»
(``Btn_Panel_*``)을 쓰고 있었다. 그 그림의 평시 색은 팔레트의 <b>가장 어두운 쪽</b>
(``#12141C`` 계열)이라 <b>창 판때기와 거의 같은 밝기</b>다 — 눌러야 할 것이
배경과 구별되지 않는다.

볼트의 버튼 시트는 여섯 벌(``BUTTON_01~06``)뿐이고 <b>전부 이미 쓰고 있다</b>.
그래서 «더 밝은 원화» 를 가져올 데가 없다. 대신 <b>이미 있는 그림을 팔레트 안에서
한 단 올린다</b> — 색을 새로 만드는 것이 아니라 :data:`STEP` 이 정한 <b>같은 16색
팔레트의 다음 칸</b>으로 옮기는 것이라, 픽셀 아트의 색 수가 늘지 않는다
(``ui_sprite_cut.py`` 의 ④ «16색 팔레트로 스냅» 과 같은 규율).

<code>
  Btn_Tab_Normal ← Btn_Panel_Normal 을 <b>두 단</b> 올린 것   (평시에도 밝다)
  Btn_Tab_Hover  ← Btn_Panel_Hover  를 <b>한 단</b> 올린 것   (평시보다 더 밝다)
  Btn_Tab_On     ← Btn_Panel_On     그대로                    (청록 = 고른 것)
  Btn_Tab_Off    ← Btn_Panel_Off    그대로                    (잠김은 어두운 것이 맞다)
</code>

⚠ <b>청록(On)·주황·붉은 계열은 건드리지 않는다</b> — 그 색들은 «상태» 를 뜻한다.
  밝히면 «고른 것» 과 «안 고른 것» 의 차이가 오히려 줄어든다.
⚠ 알파는 그대로 둔다(0 또는 255). ``ui_sprite_cut.py`` 의 ⑤ 와 같은 이유.

★ ``.meta`` 는 <b>원본 것을 베껴</b> 쓴다 — 9-슬라이스 경계(22/0/22/0)·필터·압축이
  형제 버튼과 <b>한 치도 달라선 안 된다</b>. guid 만 경로에서 결정적으로 새로 만든다
  (``gen_relic_assets.py`` 와 같은 규칙 — 다시 돌려도 같은 guid 라 참조가 안 끊긴다).

사용법:  python Tools/ui_make_tab_sprites.py
다음:    유니티 Assets/Refresh → 메뉴 ``LastSanctuary/UI/배선``
"""
import hashlib
import io
import os
import re
import sys

import numpy as np
from PIL import Image

from vault_path import PROJECT

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

BTN_DIR = os.path.join(PROJECT, "Assets", "_Project", "Resources", "UI", "Buttons")

#: ★ «한 단 올린다» 의 정의. 왼쪽이 지금 색, 오른쪽이 한 단 밝은 색.
#:   전부 ``ui_sprite_cut.py`` 의 :data:`PALETTE` 안에 있는 값이다 — 새 색을 만들지 않는다.
#:
#: ⚠ <b>가장 어두운 ``#0D0F14`` 는 표에 없다</b> — 그것은 버튼의 <b>바깥 윤곽선</b>이다.
#:   같이 밝히면 창 판때기와의 경계가 흐려져 «밝게 했더니 오히려 뭉개진다» 가 된다.
#:   밝히는 것은 <b>속</b>이고, 윤곽은 검은 채로 두어야 밝은 속이 도드라진다.
STEP = {
    (0x12, 0x14, 0x1C): (0x21, 0x2B, 0x38),
    (0x1A, 0x1C, 0x21): (0x21, 0x2B, 0x38),
    (0x1A, 0x1F, 0x29): (0x2E, 0x42, 0x52),
    (0x21, 0x2B, 0x38): (0x3D, 0x54, 0x68),
    (0x2E, 0x42, 0x52): (0x5A, 0x71, 0x86),
    (0x3D, 0x54, 0x68): (0x94, 0xA3, 0xB3),
    (0x5A, 0x71, 0x86): (0x94, 0xA3, 0xB3),
}

#: (만들 이름, 베낄 원본, 몇 단 올릴지)
#:
#: ★ <b>평시도 한 단, 올림도 한 단</b>이다 — 둘 다 올려야 «평시가 밝아졌는데 마우스를
#:   올리면 어두워지는» 뒤집힘이 안 생긴다. 실측 색: 평시 속 ``#3D5468`` ·
#:   올림 속 ``#5A7186``. 창 판때기(``#12141C`` 계열)와 <b>두 단 이상</b> 벌어지므로
#:   «어디가 눌리는 곳인지» 가 한눈에 보이고, 글자(거의 흰색)도 여전히 읽힌다.
#: ⚠ <b>두 단</b>은 안 된다 — 속이 ``#94A3B3`` (은빛)이 되어 흰 글자가 묻힌다(실측).
JOBS = [
    ("Btn_Tab_Normal", "Btn_Panel_Normal", 1),
    ("Btn_Tab_Hover",  "Btn_Panel_Hover",  1),
    ("Btn_Tab_On",     "Btn_Panel_On",     0),
    ("Btn_Tab_Off",    "Btn_Panel_Off",    0),
]


def guid_for(key):
    """경로에서 결정적으로 만든다 — 다시 돌려도 같은 guid 라 참조가 안 끊긴다."""
    return hashlib.md5(("LastSanctuary/" + key).encode("utf-8")).hexdigest()


def lift(rgba, times):
    """팔레트 안에서 :data:`STEP` 만큼 밝기를 올린다. 표에 없는 색은 <b>그대로 둔다</b>."""
    out = rgba.copy()
    for _ in range(times):
        nxt = out.copy()
        for src, dst in STEP.items():
            m = ((out[:, :, 0] == src[0]) &
                 (out[:, :, 1] == src[1]) &
                 (out[:, :, 2] == src[2]) &
                 (out[:, :, 3] > 0))
            nxt[m, 0], nxt[m, 1], nxt[m, 2] = dst
        out = nxt
    return out


def main():
    made = 0
    for name, src, times in JOBS:
        src_png = os.path.join(BTN_DIR, src + ".png")
        if not os.path.exists(src_png):
            raise SystemExit("⚠ 원본이 없습니다: %s" % src_png)

        a = np.asarray(Image.open(src_png).convert("RGBA")).astype(np.uint8)
        out = lift(a, times)
        dst_png = os.path.join(BTN_DIR, name + ".png")
        Image.fromarray(out, "RGBA").save(dst_png)

        # ── .meta — 원본을 베끼고 guid 둘만 갈아끼운다 ──
        rel = os.path.relpath(dst_png, PROJECT).replace("\\", "/")
        meta = io.open(src_png + ".meta", encoding="utf-8").read()
        meta = re.sub(r"^guid: [0-9a-f]{32}$", "guid: " + guid_for(rel), meta, count=1, flags=re.M)
        meta = re.sub(r"spriteID: [0-9a-f]{32}",
                      "spriteID: " + guid_for(rel + "#sprite"), meta, count=1)
        io.open(dst_png + ".meta", "w", encoding="utf-8", newline="\n").write(meta)

        n_lift = int((out[:, :, :3] != a[:, :, :3]).any(axis=2).sum())
        print("  %-16s ← %-18s %d단 · 바뀐 픽셀 %d" % (name, src, times, n_lift))
        made += 1

    print("탭 그림 %d장 → %s" % (made, os.path.relpath(BTN_DIR, PROJECT)))
    print("⚠ 유니티에서 Assets/Refresh 뒤 메뉴 LastSanctuary/UI/배선 을 돌릴 것.")


if __name__ == "__main__":
    main()
