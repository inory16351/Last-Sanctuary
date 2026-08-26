# -*- coding: utf-8 -*-
"""
집결지 깃발 원화 → 게임 스프라이트 (2026-08-26 신설)

  볼트/리소스/sprites/FLAG.png  →  Assets/_Project/Resources/UI/RallyFlag.png

``Tools/gen_rally_art.py`` 가 그리던 <b>임시 깃발을 대체</b>한다. 그 파일 머리말이 적어 둔
교체 규격을 그대로 지킨다: 1:2 비율 · PPU = 가로 픽셀 수 · 피벗은 <b>깃대 밑동</b>.

★ ``ui_sprite_cut.py`` 를 안 쓰는 이유 두 가지
  ① <b>16색 HUD 팔레트로 스냅하면 안 된다</b> — 깃발 천은 <b>회색조</b>로 왔고(코드가 부대
     색을 곱한다) 그 회색을 청록 계열 팔레트에 스냅하면 곱했을 때 색이 탁해진다.
  ② <b>내용 bbox 로 자르면 안 된다</b> — 깃발은 천이 오른쪽으로만 뻗어 있어 bbox 중심과
     깃대 중심이 다르다. 잘라내면 «깃대가 어디에 박히는가» 를 잃는다. <b>캔버스째</b> 줄인다.

★★ <b>피벗은 «가로 중앙» 이 아니라 «깃대 중심» 이다</b>
  원화의 깃대는 캔버스 가로 중앙에서 <b>왼쪽으로 치우쳐</b> 있다(실측). 피벗을 0.5 로 두면
  깃발을 꽂았을 때 <b>깃대 밑동이 집결지 칸에서 그만큼 벗어난다</b>. 그래서 이 스크립트가
  깃대 x 를 <b>실측해</b> 피벗으로 적는다(``alignment: 9`` = Custom).
  ⚠ 원화를 다시 뽑아 깃대 위치가 달라져도 이 스크립트를 돌리면 저절로 맞는다.

사용법:  python Tools/import_rally_flag.py
"""
from __future__ import annotations

import os
import re
import numpy as np
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = r"C:\Project\Last-Sanctuary-Vault\리소스\sprites\FLAG.png"
DST = os.path.join(PROJECT, "Assets", "_Project", "Resources", "UI", "RallyFlag.png")

#: gen_rally_art.py 가 못박은 규격. PPU 는 가로 픽셀 수와 같아야 한다(= 가로 1타일).
OUT_W, OUT_H = 32, 64

#: 알파를 0/255 로 가르는 문턱. 픽셀 아트에 반투명 가장자리는 없다.
ALPHA_CUT = 128


def pole_center_x(a: np.ndarray) -> float:
    """
    깃대의 가로 중심(0~1). <b>아래쪽 절반</b>만 본다 — 위쪽은 천이 붙어 있어 중심이 밀린다.

    ★ 깃대만 남는 구간에서 각 행의 불투명 구간 <b>중앙</b>을 모아 중앙값을 쓴다.
      평균이 아니라 중앙값인 것은, 밑동의 접지 그림자처럼 좌우로 퍼지는 행이 섞여도
      끌려가지 않게 하기 위해서다.
    """
    h, w, _ = a.shape
    solid = a[:, :, 3] > 10

    centers = []
    for y in range(int(h * 0.55), int(h * 0.92)):
        xs = np.where(solid[y])[0]
        if len(xs) == 0:
            continue
        centers.append((xs.min() + xs.max()) / 2.0)

    if not centers:
        return 0.5
    return float(np.median(centers)) / (w - 1)


def patch_meta(path: str, pivot_x: float) -> None:
    """
    기존 ``.meta`` 를 <b>고쳐 쓴다</b> — 새로 만들지 않는다.

    ⚠ guid 가 바뀌면 씬·프리팹의 참조가 통째로 끊긴다. 이 프로젝트가 반복해 피해 온 사고다
      (161-7 의 «생성기가 기존 .meta 의 guid 를 읽어서 다시 쓴다» 와 같은 이유).
    ⚠ ``alignment`` 7(Bottom Center) → <b>9(Custom)</b> 로 바꿔야 ``spritePivot`` 이 먹는다.
      9 로 두고 pivot 을 안 고치면 «가운데 아래» 가 아니라 «임의의 값» 이 되므로 둘은 한 쌍이다.
    """
    meta = path + ".meta"
    if not os.path.exists(meta):
        raise SystemExit("[깃발] .meta 가 없습니다: %s\n"
                         "  유니티를 한 번 띄워 임포트한 뒤 다시 돌리세요." % meta)

    with open(meta, "r", encoding="utf-8") as f:
        text = f.read()

    before = text
    text = re.sub(r"(?m)^(\s*)alignment: \d+$", r"\g<1>alignment: 9", text)
    text = re.sub(r"(?m)^(\s*)spritePivot: \{x: [\d.]+, y: [\d.]+\}$",
                  r"\g<1>spritePivot: {x: %.4f, y: 0}" % pivot_x, text)

    if text == before:
        print("  ⚠ .meta 에서 alignment/spritePivot 을 못 찾았습니다 — 손으로 확인하세요")
        return

    with open(meta, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    print("  .meta  alignment 9(Custom) · spritePivot x=%.4f" % pivot_x)


def main() -> None:
    if not os.path.exists(SRC):
        raise SystemExit("[깃발] 원화가 없습니다: %s" % SRC)

    im = Image.open(SRC).convert("RGBA")
    a = np.asarray(im).astype(np.float64)
    print("[집결지 깃발] 원화 %dx%d" % im.size)

    px = pole_center_x(a)
    print("  깃대 중심 x = %.1f px (캔버스의 %.1f%%) · 가로 중앙은 %.1f px"
          % (px * (im.width - 1), px * 100, (im.width - 1) / 2))

    # ★ BOX(면적 평균)로 줄인다 — NEAREST 는 1px 테두리를 통째로 날린다.
    small = im.resize((OUT_W, OUT_H), Image.BOX)

    # ★ 알파 이진화 — 부드러운 가장자리가 어두운 맵 위에서 지저분한 후광으로 남는다.
    b = np.asarray(small).astype(np.int32)
    solid = b[:, :, 3] >= ALPHA_CUT
    b[:, :, 3] = np.where(solid, 255, 0)
    b[~solid] = 0

    Image.fromarray(b.astype(np.uint8), "RGBA").save(DST)
    print("  → %s  %dx%d" % (os.path.relpath(DST, PROJECT), OUT_W, OUT_H))

    patch_meta(DST, px)
    print("  다음: 유니티에서 Assets/Refresh")


if __name__ == "__main__":
    main()
