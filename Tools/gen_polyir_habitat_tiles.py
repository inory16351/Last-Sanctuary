# -*- coding: utf-8 -*-
"""폴리르(에픽 중립 보스 1104) 서식지 타일 — 바닥 32종 · 데코 32종 (2026-08-21).

유저 지시: *"플뢰르 중립 몬스터 추가"* 의 일부. 원본 두 장:

    <볼트>/리소스/sprites/Polyir_chunk.png   1586x992  「용 서식지 바닥 타일 세트」
    <볼트>/리소스/sprites/Polyir_deco.png    1586x992  「용 서식지 데코 세트」

★★ 이 시트는 <b>지금까지 중 가장 다루기 쉽다</b> — 두 장 다 <b>같은 격자</b>다
======================================================================
시트가 스스로 규격을 적어 두었다: *"160x80px · 20px 타일 8열 x 4행 = 32칸"*.
즉 <b>8열 x 4행 = 32칸</b>이고 두 장이 완전히 같은 배치다(바리올라 시트는 구획마다
행·열이 달라 네 벌의 좌표가 필요했다 — 121절).

★ <b>칸마다 «20» 이라는 숫자와 캡션이 붙어 있다</b> — 그림 위에 겹쳐 그려져 있어서
  그대로 20x20 으로 줄이면 타일 왼쪽 위에 <b>흰 얼룩</b>이 남는다. 그래서 칸 안에서
  <b>숫자와 캡션을 피한 정사각형</b>만 잘라 쓴다(:data:`INNER`). 유기적인 무늬라
  중심이 조금 옮겨져도 눈에 보이지 않는다.

★ <b>가장자리(Edge) 원화가 없다</b> — 이 두 장에는 바닥과 데코뿐이다(바리올라 시트에는
  벽·전이 구획이 있었다). <see cref="NeutralHabitat"/> 는 Edge 를 <b>선택</b>으로 받으므로
  (`required: false`) 없으면 <b>경계에도 바닥 타일을 깐다</b> — 조용히 잘 돌아간다.
  ⚠ 지어내지 않는다: 원화가 오면 그때 `PolyirHabitatEdge` 를 만들면 된다.

★ 색은 <b>손대지 않는다</b>(110절의 교훈 *"너무 이질적으로 만들진 마"*). 이 시트는
  검붉은 용암 계열이라 이미 맵과 붙어 있다.

사용법:  py -3 Tools/gen_polyir_habitat_tiles.py
다음:    py -3 Tools/table_update_20260821_polyir_habitat.py   (표에 habitat_design 줄)
         py -3 Tools/sync_tables_to_assets.py
         유니티 Assets/Refresh
"""

import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT

# ★ 자르기·메타·YAML 은 성역 타일 생성기의 것을 그대로 쓴다 — 같은 규약
#   (20px · PPU 20 · 결정적 guid · Tile 에셋 형식)이라 복제하면 두 벌이 갈라진다.
from gen_sanctuary_tiles import TILE, write_tile, ensure_folder_meta

SRC_GROUND = os.path.join(VAULT, "리소스", "sprites", "Polyir_chunk.png")
SRC_PROPS = os.path.join(VAULT, "리소스", "sprites", "Polyir_deco.png")

ART_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "OrganicTilemap")
TILE_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Resources", "HabitatTiles")

SET_NAME = "PolyirHabitat"

# ──────────────────────────────────────────────────────────────────────────
# 격자 — **실측**. 두 시트가 같다.
#   좌우 여백 18px · 머리글 아래 y60 부터 · 마지막 캡션 위 y940 까지.
# ──────────────────────────────────────────────────────────────────────────
COLS, ROWS = 8, 4
X0, X1 = 18, 1568
Y0, Y1 = 60, 940

#: 칸(193.75 x 220) 안에서 <b>그림만 있는 영역</b> — (x0, y0, x1, y1).
#:
#: ★★ 2026-08-21 — <b>실측으로 다시 잡았다</b>. 처음에는 (27, 18) 에서 140x140 을 떼었는데
#:   칸의 구조가 이랬다(2배 확대해 눈으로 재고 화소로 검산):
#:     y 27~50    «20» 숫자 (흰 글자 — 그림 위에 겹쳐 있다)
#:     y 52~198   <b>그림</b>
#:     y 200~     캡션 «#00 데코 · w8»
#:   즉 옛 상자는 <b>위쪽 빈 자리를 넣고 프롭의 아래쪽을 잘라</b> 냈다 — 데코가
#:   «잘린 조각» 으로 보이던 이유다(유저 리포트: *"투명해지지 않도록 다시해"*).
CONTENT = (11, 58, 183, 196)

#: 바닥은 <b>고정 정사각형</b>으로 뗀다 — 이어 붙는 무늬라 어디를 떼도 같다.
#:   숫자·캡션만 피하면 된다(위 CONTENT 안에서 가운데 정사각).
GROUND_SIDE = 140

#: 데코의 그림 판정 — 칸 배경색과 이만큼 넘게 다르면 «그림» 으로 본다(RGB 채널 최대 차이).
#:   상자를 찾는 데만 쓴다(투명 판정은 아래 :func:`keyed` 의 흘려 채우기가 한다).
PROP_ART_DIFF = 20

#: 그림 상자를 이만큼 넉넉히 잡는다(px) — 알파 경계가 상자에 딱 붙으면 잘려 보인다.
PROP_PAD = 6

#: ★★ <b>«회색 글자·테두리» 를 그림에서 뺀다</b> (2026-08-21).
#:
#:   칸 안에는 그림 말고도 <b>흰 숫자(«20»)와 칸 테두리 선</b>이 있다. 둘 다 배경과
#:   충분히 달라서 «그림» 판정에 걸리고, 그러면 ① 상자가 그쪽으로 늘어나고
#:   ② 구운 타일 왼쪽 위에 <b>밝은 회색 조각</b>이 남는다(실제로 남았다).
#:
#:   갈라내는 기준은 <b>채도</b>다 — 이 시트의 데코는 전부 <b>붉거나 노란</b> 유기물이고
#:   (채도 30~120), 글자·테두리는 <b>무채색</b>(채도 0~10)이다. 밝기까지 함께 보면
#:   어두운 배경 노이즈와도 안 헷갈린다.
GREY_SAT_MAX = 14
GREY_LUM_MIN = 90

#: 데코의 배경 판정 — 칸 배경색과 이만큼 가까우면 «배경» 으로 본다(RGB 채널 최대 차이).
#:
#: ★★ 2026-08-21 — <b>방식을 바꿨다</b> (유저 리포트: *"투명해지지 않도록 다시해"*).
#:
#:   처음에는 바리올라와 같은 «밝기·채도 문턱» 으로 <b>그림을 골라냈다</b>
#:   (`밝기 > 배경+16 또는 채도 > 28` 인 픽셀만 남긴다). 그런데 이 시트의 데코는
#:   <b>어두운 검붉은 결정</b>이라 그림의 상당 부분이 그 문턱을 못 넘는다 —
#:   결과가 «가운데가 뻥 뚫린 반투명 얼룩» 이었다.
#:
#:   → <b>거꾸로 한다</b>: «배경과 닮았고 <b>칸 테두리에서 이어져 들어오는</b> 픽셀만»
#:     지운다(흘려 채우기). 그림 안쪽의 어두운 픽셀은 테두리에서 닿을 수 없으므로
#:     <b>그대로 남는다</b>. 이 프로젝트가 캐릭터 원화에서 쓰는 «갇힌 배경»
#:     (`skin_sheet.enclosed_background`)과 <b>같은 발상</b>이고, 방향만 반대다.
#:
#: ⚠ 문턱을 넉넉히 잡아도 안전하다 — 테두리에서 이어지지 않으면 안 지운다.
PROP_BG_TOL = 26


def load(path):
    if not os.path.isfile(path):
        raise SystemExit("⚠ 원본이 없습니다: " + path)
    return Image.open(path).convert("RGB")


def cell_origin(col, row):
    """칸 왼쪽 위 좌표."""
    px = (X1 - X0) / float(COLS)
    py = (Y1 - Y0) / float(ROWS)
    return int(round(X0 + px * col)), int(round(Y0 + py * row))


def ground_box(col, row):
    """바닥 — 칸 가운데의 고정 정사각형(숫자·캡션을 피한다)."""
    ox, oy = cell_origin(col, row)
    cx0, cy0, cx1, cy1 = CONTENT
    mx = (cx0 + cx1) // 2
    my = (cy0 + cy1) // 2
    half = GROUND_SIDE // 2
    return (ox + mx - half, oy + my - half, ox + mx + half, oy + my + half)


def prop_box(im, col, row):
    """
    데코 — 칸 안에서 <b>그림이 실제로 차지한 상자</b>를 찾아 정사각형으로 넓힌다.

    ★ 정사각형으로 만드는 이유 — 타일은 20x20 정사각이다. 직사각 상자를 그대로 줄이면
      프롭이 <b>납작하거나 홀쭉하게</b> 눌린다.
    ⚠ 칸 밖으로 나가지 않게 자른다 — 넘치면 옆 칸의 프롭이 물려 들어온다.
    """
    ox, oy = cell_origin(col, row)
    cx0, cy0, cx1, cy1 = CONTENT
    region = im.crop((ox + cx0, oy + cy0, ox + cx1, oy + cy1))

    a = np.asarray(region).astype(np.int16)
    k = 5
    corner = np.concatenate([a[:k, :k].reshape(-1, 3), a[:k, -k:].reshape(-1, 3),
                             a[-k:, :k].reshape(-1, 3), a[-k:, -k:].reshape(-1, 3)])
    bg = np.median(corner, axis=0)
    art = np.max(np.abs(a - bg), axis=2) > PROP_ART_DIFF
    art &= ~is_grey(a)              # 숫자·테두리 조각은 그림이 아니다(위 ★★)

    ys, xs = np.nonzero(art)
    if len(xs) == 0:                      # 빈 칸 — 칸 전체를 쓴다(있을 수 없지만 방어)
        return (ox + cx0, oy + cy0, ox + cx1, oy + cy1)

    x0, x1 = int(xs.min()) - PROP_PAD, int(xs.max()) + PROP_PAD
    y0, y1 = int(ys.min()) - PROP_PAD, int(ys.max()) + PROP_PAD

    # 정사각형으로 — 긴 변을 기준으로 짧은 변을 양쪽으로 늘린다.
    side = max(x1 - x0, y1 - y0)
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    x0, x1 = cx - side // 2, cx + side // 2
    y0, y1 = cy - side // 2, cy + side // 2

    # 칸(CONTENT) 밖으로 나가면 안으로 밀어 넣는다.
    w = cx1 - cx0
    h = cy1 - cy0
    x0 = max(0, min(x0, w - 1))
    y0 = max(0, min(y0, h - 1))
    x1 = max(x0 + 1, min(x1, w))
    y1 = max(y0 + 1, min(y1, h))

    return (ox + cx0 + x0, oy + cy0 + y0, ox + cx0 + x1, oy + cy0 + y1)


def is_grey(rgb):
    """숫자·테두리처럼 <b>밝고 무채색</b>인 화소 (위 :data:`GREY_SAT_MAX` 의 ★★)."""
    lum = rgb.mean(axis=2)
    sat = rgb.max(axis=2) - rgb.min(axis=2)
    return (sat <= GREY_SAT_MAX) & (lum >= GREY_LUM_MIN)


def opaque(im, box):
    """바닥 — 완전 불투명. 아래 지형을 덮는다."""
    small = im.crop(box).resize((TILE, TILE), Image.LANCZOS)
    rgb = np.asarray(small).astype(np.uint8)
    return np.dstack([rgb, np.full(rgb.shape[:2], 255, np.uint8)])


def keyed(im, box):
    """
    데코 — <b>테두리에서 이어져 들어오는 배경만</b> 투명하게 (위 :data:`PROP_BG_TOL` 의 ★★).

    ⚠ 알파는 20x20 으로 <b>줄이기 전에</b> 만든다 — 줄인 뒤에 만들면 가장자리에서
      배경색이 섞여 테두리가 생긴다(113-2절과 같은 이유).
    ★ 기준 배경색은 <b>이 칸의 네 귀퉁이 중앙값</b>이다 — 시트 전체로 재면 칸마다
      조금씩 다른 배경을 못 따라간다.
    """
    crop = im.crop(box)
    rgb = np.asarray(crop).astype(np.int16)
    h, w, _ = rgb.shape

    k = 6
    corner = np.concatenate([rgb[:k, :k].reshape(-1, 3), rgb[:k, -k:].reshape(-1, 3),
                             rgb[-k:, :k].reshape(-1, 3), rgb[-k:, -k:].reshape(-1, 3)])
    bg = np.median(corner, axis=0)

    near = np.max(np.abs(rgb - bg), axis=2) <= PROP_BG_TOL     # 배경색과 닮은 픽셀
    near |= is_grey(rgb)          # 숫자·테두리 조각도 «배경» 으로 지운다(위 ★★)

    # ── 테두리에서 흘려 채운다(4방향 BFS) — 닿는 것만 «바깥 배경» 이다 ──
    out = np.zeros((h, w), bool)
    stack = []
    for x in range(w):
        for y in (0, h - 1):
            if near[y, x]:
                stack.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if near[y, x]:
                stack.append((y, x))
    while stack:
        y, x = stack.pop()
        if out[y, x] or not near[y, x]:
            continue
        out[y, x] = True
        if y > 0:
            stack.append((y - 1, x))
        if y + 1 < h:
            stack.append((y + 1, x))
        if x > 0:
            stack.append((y, x - 1))
        if x + 1 < w:
            stack.append((y, x + 1))

    rgba = np.dstack([np.asarray(crop).astype(np.uint8),
                      np.where(out, 0, 255).astype(np.uint8)])
    return np.asarray(Image.fromarray(rgba, "RGBA").resize((TILE, TILE), Image.LANCZOS))


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print("[폴리르 서식지 타일]")

    ensure_folder_meta(TILE_ROOT)

    # ── ① 바닥 32종 ───────────────────────────────────────────────────
    ground = load(SRC_GROUND)
    art_g = os.path.join(ART_ROOT, SET_NAME)
    tiles_g = os.path.join(TILE_ROOT, SET_NAME)
    n = 0
    for row in range(ROWS):
        for col in range(COLS):
            write_tile(opaque(ground, ground_box(col, row)),
                       art_g, tiles_g, "%s_%02d" % (SET_NAME, n))
            n += 1
    ensure_folder_meta(art_g)
    ensure_folder_meta(tiles_g)
    print("  바닥  %2d종 → %s" % (n, SET_NAME))

    # ── ② 데코 32종 ───────────────────────────────────────────────────
    props = load(SRC_PROPS)
    art_p = os.path.join(ART_ROOT, SET_NAME + "Props")
    tiles_p = os.path.join(TILE_ROOT, SET_NAME + "Props")
    m = 0
    for row in range(ROWS):
        for col in range(COLS):
            write_tile(keyed(props, prop_box(props, col, row)),
                       art_p, tiles_p, "%sProps_%02d" % (SET_NAME, m))
            m += 1
    ensure_folder_meta(art_p)
    ensure_folder_meta(tiles_p)
    print("  데코  %2d종 → %sProps" % (m, SET_NAME))

    print()
    print("  ⚠ 가장자리(Edge) 원화는 이 시트에 없다 — 경계에도 바닥을 깐다(맨 위 ★).")
    print("  다음: py -3 Tools/table_update_20260821_polyir_habitat.py")
    print("        py -3 Tools/sync_tables_to_assets.py · 유니티 Assets/Refresh")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
