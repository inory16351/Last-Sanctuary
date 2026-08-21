# -*- coding: utf-8 -*-
"""폴리르(에픽 중립 보스 1104) 서식지 타일 — 바닥 32종 · 데코 32종 (2026-08-21).

유저 지시: *"플뢰르 중립 몬스터 추가"* 의 일부. 원본 두 장:

    <볼트>/리소스/sprites/Polyir_chunk.png   1586x992  「용 서식지 바닥 타일 세트」  (RGB · 캡션 있음)
    <볼트>/리소스/sprites/Polyir_deco.png    1536x1024 「용 서식지 데코 세트」    (RGBA · 배경 투명)

★★★ <b>2026-08-21 (2차) — 데코 시트가 통째로 교체됐다</b> (유저 지시: *"플뢰르 데코 파일
======================================================================
변경한거 적용(투명 배경)"*). 새 시트는 <b>배경이 이미 투명</b>하고 숫자·캡션·칸 테두리가
<b>아예 없다</b>. 그래서 이 파일이 데코를 위해 갖고 있던 세 겹의 장치 —
«칸 격자로 자르기» · «무채색 글자 골라내기»(옛 `is_grey`) · «테두리에서 흘려 채워 배경
지우기»(옛 `keyed`/`PROP_BG_TOL`) — 가 <b>전부 필요 없어졌고, 그대로 두면 해롭다</b>
(옛 격자는 1586x992 기준이라 새 시트에서는 엉뚱한 자리를 자른다).

→ 데코는 이제 <b>알파를 그대로 믿는다</b>: 알파 마스크의 <b>덩어리(연결 요소)</b>를 세어
  프롭 32개를 찾고, 각자의 상자만 떼어 20x20 으로 줄인다. «칸» 이라는 개념이 없으므로
  다음 시트에서 배치가 바뀌어도 이 코드는 안 고쳐도 된다. 자세한 것은
  :func:`prop_boxes` 와 :func:`shrink` 의 ★ 를 볼 것.

⚠ <b>바닥 시트(`Polyir_chunk.png`)는 안 바뀌었다</b> — 아래 격자 상수
  (:data:`COLS`~:data:`GROUND_SIDE`)는 <b>바닥 전용</b>이다. 캡션이 그림 위에 겹쳐 있어
  칸 안에서 «숫자와 캡션을 피한 정사각형» 만 떼어 쓴다.

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
# 격자 — **실측**. ⚠ <b>바닥 시트 전용</b>이다(데코 시트는 격자를 안 쓴다 — 맨 위 ★★★).
#   좌우 여백 18px · 머리글 아래 y60 부터 · 마지막 캡션 위 y940 까지.
# ──────────────────────────────────────────────────────────────────────────
COLS, ROWS = 8, 4          #: 바닥 시트의 칸 수. 데코도 32개지만 <b>좌표로 세지 않는다</b>
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

#: ★ 데코 — <b>알파가 이 값을 넘으면 «그림»</b>. 시트의 알파는 0 또는 200~254 라
#:   가운데 어디를 잡아도 같지만, 부드러운 가장자리 한두 겹을 덩어리에 포함시키려고
#:   낮게 잡는다(경계를 잘라내면 프롭이 «오려낸 스티커» 처럼 보인다).
PROP_ALPHA_MIN = 8

#: ★ 프롭으로 인정하는 <b>최소 화소 수</b>. 새 시트에는 알파 1~3px 짜리 <b>먼지</b>가
#:   여섯 군데 있다(원화 저장 과정에서 남은 것). 이 값 아래는 덩어리로 세지 않는다.
#:   ⚠ 실제 프롭은 가장 작은 것이 10,702px 이라 여유가 아주 크다.
PROP_MIN_PIXELS = 2000

#: 프롭 상자를 이만큼 넉넉히 잡는다(px) — 알파 경계가 상자에 딱 붙으면 잘려 보인다.
PROP_PAD = 6


def load(path, mode="RGB"):
    if not os.path.isfile(path):
        raise SystemExit("⚠ 원본이 없습니다: " + path)
    return Image.open(path).convert(mode)


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



def prop_boxes(alpha):
    """
    데코 시트에서 <b>프롭 하나하나의 마스크와 상자</b>를 찾아 <b>읽는 순서</b>로 돌려준다.

    ★★ <b>«칸» 을 세지 않는다</b> — 알파의 <b>덩어리</b>를 센다. 새 시트는 프롭이
      8x4 로 놓여 있지만 <b>칸 경계를 넘나든다</b>(가시가 옆 칸 위로 뻗는다). 균등 격자로
      자르면 그 가시가 잘리고, 옆 칸의 가시가 물려 들어온다 — 실제로 재 보면 32칸 중
      절반이 칸의 좌우 끝에 닿아 있다. 그래서 <b>그림이 스스로 알려주는 경계</b>를 쓴다.

    ★ 덩어리 찾기는 <b>가로 런(run) + 유니온 파인드</b> 다 — 화소 단위 BFS 는 150만
      화소에서 느리고, 이 프로젝트에는 `scipy.ndimage` 가 없다. 런은 몇 천 개뿐이다.
      8방향으로 잇는다(대각선으로만 붙은 가시 끝이 따로 떨어지지 않게).

    ⚠ 돌려주는 마스크는 <b>그 덩어리만</b>이다 — 상자 안에 이웃 프롭의 가시가 들어와도
      마스크가 지운다. 상자만 잘라 쓰면 «옆 것이 물려 들어오는» 옛 문제가 되살아난다.

    :return: ``[(x0, y0, x1, y1, mask), ...]`` — 위에서 아래, 왼쪽에서 오른쪽 순서.
    """
    h, w = alpha.shape
    solid = alpha >= PROP_ALPHA_MIN

    runs = []                       # (y, x0, x1)  — x1 은 <b>미포함</b>
    row_runs = []                   # 행마다 그 행의 런 인덱스 목록
    for y in range(h):
        idx = []
        xs = np.nonzero(solid[y])[0]
        if len(xs):
            cut = np.nonzero(np.diff(xs) > 1)[0]
            starts = np.concatenate([[0], cut + 1])
            ends = np.concatenate([cut, [len(xs) - 1]])
            for s, e in zip(starts, ends):
                idx.append(len(runs))
                runs.append((y, int(xs[s]), int(xs[e]) + 1))
        row_runs.append(idx)

    parent = list(range(len(runs)))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[max(ra, rb)] = min(ra, rb)

    for y in range(1, h):
        for i in row_runs[y]:
            _, ax0, ax1 = runs[i]
            for j in row_runs[y - 1]:
                _, bx0, bx1 = runs[j]
                if bx0 - 1 < ax1 and ax0 - 1 < bx1:      # 8방향 — 대각선도 잇는다
                    union(i, j)

    groups = {}
    for i, (y, x0, x1) in enumerate(runs):
        g = groups.setdefault(find(i), [0, w, h, 0, 0])   # [화소수, x0, y0, x1, y1]
        g[0] += x1 - x0
        g[1] = min(g[1], x0)
        g[2] = min(g[2], y)
        g[3] = max(g[3], x1)
        g[4] = max(g[4], y + 1)

    keep = [(root, g) for root, g in groups.items() if g[0] >= PROP_MIN_PIXELS]

    # 읽는 순서 — 세로로 겹치는 것끼리 한 «줄» 로 묶고, 줄 안에서는 왼쪽부터.
    keep.sort(key=lambda kg: kg[1][2])
    rows_out, current, floor = [], [], -1
    for root, g in keep:
        if not current or g[2] < floor:
            current.append((root, g))
            floor = max(floor, g[2] + (g[4] - g[2]) // 2)
        else:
            rows_out.append(current)
            current = [(root, g)]
            floor = g[2] + (g[4] - g[2]) // 2
    if current:
        rows_out.append(current)

    ordered = []
    for band in rows_out:
        band.sort(key=lambda kg: kg[1][1])
        ordered.extend(band)

    # 덩어리별 마스크 — 런을 그대로 칠한다(전체 라벨 배열을 만들 필요가 없다).
    by_root = {}
    for i, (y, x0, x1) in enumerate(runs):
        by_root.setdefault(find(i), []).append((y, x0, x1))

    out = []
    for root, g in ordered:
        mask = np.zeros((h, w), bool)
        for y, x0, x1 in by_root[root]:
            mask[y, x0:x1] = True
        out.append((g[1], g[2], g[3], g[4], mask))
    return out


def square(box, w, h):
    """상자를 <b>정사각형</b>으로 넓힌다 — 타일이 20x20 이라 직사각을 그대로 줄이면 눌린다."""
    x0, y0, x1, y1 = box
    x0, y0, x1, y1 = x0 - PROP_PAD, y0 - PROP_PAD, x1 + PROP_PAD, y1 + PROP_PAD
    side = max(x1 - x0, y1 - y0)
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    return (cx - side // 2, cy - side // 2, cx - side // 2 + side, cy - side // 2 + side)


def opaque(im, box):
    """바닥 — 완전 불투명. 아래 지형을 덮는다."""
    small = im.crop(box).resize((TILE, TILE), Image.LANCZOS)
    rgb = np.asarray(small).astype(np.uint8)
    return np.dstack([rgb, np.full(rgb.shape[:2], 255, np.uint8)])


def shrink(rgba, box, mask):
    """
    데코 한 개를 20x20 으로 줄인다. <b>알파는 시트가 준 것을 그대로 쓴다</b>.

    ★ <b>알파를 곱해 두고 줄인다</b>(premultiply) — 그냥 줄이면 투명한 화소의 RGB(대개
      검정)가 가장자리에 섞여 <b>검은 테두리</b>가 생긴다. 113-2절이 캐릭터 프레임에서
      겪은 것과 같은 함정이다.
    ⚠ 상자를 시트 밖으로 넘기지 않는다 — 넘친 만큼은 투명으로 채운다(빈 곳이지 오류가 아니다).
    """
    h, w = mask.shape
    x0, y0, x1, y1 = box
    side = x1 - x0

    cut = np.zeros((side, side, 4), np.float32)
    sx0, sy0 = max(0, x0), max(0, y0)
    sx1, sy1 = min(w, x1), min(h, y1)
    if sx1 > sx0 and sy1 > sy0:
        piece = rgba[sy0:sy1, sx0:sx1].astype(np.float32)
        piece = piece * mask[sy0:sy1, sx0:sx1, None]      # 이웃 프롭을 지운다(위 ⚠)
        cut[sy0 - y0:sy1 - y0, sx0 - x0:sx1 - x0] = piece

    a = cut[:, :, 3:4] / 255.0
    pre = np.concatenate([cut[:, :, :3] * a, cut[:, :, 3:4]], axis=2)
    small = np.asarray(Image.fromarray(pre.astype(np.uint8), "RGBA")
                       .resize((TILE, TILE), Image.LANCZOS)).astype(np.float32)

    sa = np.clip(small[:, :, 3:4] / 255.0, 1e-4, None)
    rgb = np.clip(small[:, :, :3] / sa, 0, 255)
    return np.dstack([rgb.astype(np.uint8), small[:, :, 3].astype(np.uint8)])


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

    # ── ② 데코 — 알파 덩어리를 세어 나온 개수만큼 ────────────────────────
    #    ⚠ <b>32 를 못 박지 않는다</b>. 시트가 알려주는 대로 굽고, 예상과 다르면 소리를 낸다 —
    #      조용히 32개를 만들면 «어느 프롭이 어느 타일인가» 가 어긋난 채로 넘어간다.
    props = load(SRC_PROPS, "RGBA")
    rgba = np.asarray(props)
    found = prop_boxes(rgba[:, :, 3])

    art_p = os.path.join(ART_ROOT, SET_NAME + "Props")
    tiles_p = os.path.join(TILE_ROOT, SET_NAME + "Props")
    for m, (x0, y0, x1, y1, mask) in enumerate(found):
        write_tile(shrink(rgba, square((x0, y0, x1, y1), rgba.shape[1], rgba.shape[0]), mask),
                   art_p, tiles_p, "%sProps_%02d" % (SET_NAME, m))
    ensure_folder_meta(art_p)
    ensure_folder_meta(tiles_p)
    print("  데코  %2d종 → %sProps  (알파 덩어리 · %dx%d 시트)"
          % (len(found), SET_NAME, rgba.shape[1], rgba.shape[0]))

    expect = COLS * ROWS
    if len(found) != expect:
        print("  ⚠⚠ 데코가 %d개다 — 시트는 %d개(8x4)여야 한다. PROP_MIN_PIXELS 를 확인할 것."
              % (len(found), expect))

    print()
    print("  ⚠ 가장자리(Edge) 원화는 이 시트에 없다 — 경계에도 바닥을 깐다(맨 위 ★).")
    print("  다음: py -3 Tools/table_update_20260821_polyir_habitat.py")
    print("        py -3 Tools/sync_tables_to_assets.py · 유니티 Assets/Refresh")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
