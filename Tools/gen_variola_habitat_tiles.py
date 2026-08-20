# -*- coding: utf-8 -*-
"""바리올라(에픽 중립 보스 1103) 서식지 타일 — 바닥 · 가장자리 · 데코 (2026-08-20).

유저 지시: *"바리올라 서식지 청크 이미지 넣었으니까 분석해서 타일맵 만들고 (스프라이트 폴더에
있음) 테이블에도 값 추가해줘"*

원본 — ★ 이번엔 **라벨이 붙은 참조 시트**다
============================================
    <볼트>/리소스/sprites/Variola_chunk.png   1443x1090

아니사킬은 격자에 딱 맞게 그려진 시트라 그냥 잘랐지만(110절), 이 장은 <b>구획 제목과
설명이 붙은 참조 시트</b>다 — 성역 시트(`gen_sanctuary_tiles.py`)와 같은 종류다.
시트가 스스로 적어 놓은 구성:

    ① 바닥 타일 세트 (20x20) — 16종      2행 x 8열
    ② 데코(프롭) 세트 (20x20) — 32종     3행 (격자가 아니다 · 아래 ★★)
    ③ 벽(내부/외곽) 타일 세트 — 16종     2행 x 6열   ← ⚠ 실제로는 12칸
    ④ 전이/경계 타일 세트 — 8종          2행 x 4열

⚠ <b>③ 의 「16종」은 시트가 틀렸다</b> — 실제로 그려진 것은 <b>12칸</b>이다(격자 실측).
  베일 시트에서도 헤더 프레임 수가 틀렸다 — 이 작가의 시트는 헤더 숫자를 믿으면 안 된다.

★★ 데코는 **격자가 아니다** — 덩어리로 센다
============================================
바닥·벽·경계는 균일한 격자라 좌표로 자르면 되는데, 데코 세 줄은 <b>폭이 제각각인 프롭이
띠 위에 늘어서 있다</b>. 칸 구분선도 없다(윗줄 배경 광도가 33~45 로 일정 · 실측).

균일 격자로 자르면 프롭이 반씩 잘리므로, <b>열마다 그림 픽셀이 몇 개인지</b> 세어
빈 열로 가른다. 시트 바탕(19)과 <b>띠 배경</b>(33~45)이 다르므로 기준은 <b>띠 배경</b>이다 —
시트 바탕 기준으로 재면 띠 전체가 한 덩어리가 된다(실제로 그랬다).

★ 그렇게 세면 <b>40개</b>가 나온다(헤더의 32 보다 많다). 눈으로 확인했고 전부 온전한
  프롭 하나씩이다 — 데코는 많을수록 좋으므로 그대로 쓴다.
⚠ 폭이 중앙값의 :data:`PROP_SPLIT_RATIO` 배를 넘는 덩어리는 <b>둘로 가른다</b> —
  1번 칸(초록 결정 + 뾰족탑)만 붙어 나왔다. 안 가르면 165px 짜리가 20x20 에 눌려
  혼자만 찌그러져 보인다.

★ 색은 **손대지 않는다**
========================
110절의 교훈(*"너무 이질적으로 만들진 마"*)대로 색조를 옮기지 않는다. 실측해 보니
원본이 이미 알맞은 자리에 있다:

    맵 바닥            H 359°  S 0.49  V 0.37
    아니사킬 서식지     H 342°  S 0.54  V 0.28
    카르시노스 서식지   H 309°  S 0.59  V 0.22
    **바리올라 서식지   H 290°  S 0.16  V 0.19**   ← 이 시트
    성역(넥서스)       H   3°  S 0.70  V 0.24

카르시노스와 <b>19°</b> 로 가장 가깝지만, 바리올라만 <b>형광 초록 균열</b>을 갖고 있어
화면에서 헷갈리지 않는다. 그리고 맵보다 이미 <b>훨씬 어둡다</b>(V 0.19 vs 0.37) —
88-5절에서 유닛이 묻힌 원인이 「서식지가 밝아서」였으므로 이 방향이 안전하다.
채도가 낮은 것(0.16)도 원화의 성격이다 — 올리면 초록 균열이 튄다.

★ 벽 12종은 **뽑아 두되 배선하지 않는다**
=========================================
`NeutralHabitat` 에는 바닥 · 가장자리 · 데코 세 칸뿐이고 <b>「벽」 칸이 없다</b>.
그림이 나빠서가 아니라 받을 자리가 없어서다 — 라린길의 미배선 원화와 같은 처리다
(`VariolaHabitatWall` 폴더에 남긴다). 칸이 생기면 그대로 이으면 된다.

사용법:  python Tools/gen_variola_habitat_tiles.py
다음:    py -3 Tools/table_update_20260820_variola_habitat.py   (표에 habitat_design 줄 추가)
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

SRC = os.path.join(VAULT, "리소스", "sprites", "Variola_chunk.png")

ART_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "OrganicTilemap")
TILE_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Resources", "HabitatTiles")

SET_NAME = "VariolaHabitat"

# ──────────────────────────────────────────────────────────────────────────
# 시트 좌표 — **실측** (밝은 행/열 밴드로 찾은 값)
#   구획마다 (행 y0,y1) 목록과 (열 x0, 칸 수) 를 적는다.
# ──────────────────────────────────────────────────────────────────────────

#: ① 바닥 — 2행 x 8열. 칸 폭 172 · 간격 175.6 (x 19 에서 시작해 1423 에서 끝난다).
GROUND_ROWS = [(50, 180), (185, 314)]
GROUND_X = (19, 1423)
GROUND_COLS = 8

#: ② 데코 — 격자가 아니다(맨 위 ★★). 줄만 적고 칸은 덩어리로 찾는다.
PROP_ROWS = [(372, 474), (478, 579), (583, 686)]

#: ③ 벽 — 2행 x 6열, 왼쪽 절반. ⚠ 배선하지 않는다(맨 위 ★).
WALL_ROWS = [(785, 913), (918, 1045)]
WALL_X = (19, 854)
WALL_COLS = 6

#: ④ 전이/경계 — 2행 x 4열, 오른쪽. 이것이 `Edge` 다.
EDGE_ROWS = WALL_ROWS
EDGE_X = (875, 1423)
EDGE_COLS = 4

#: 데코 판정 — 띠 배경보다 이만큼 밝거나, 채도가 이 값을 넘으면 그림.
PROP_LUM_OVER = 16
PROP_SAT_MIN = 28

#: 데코 덩어리 판정 — 열마다 그림 픽셀이 이만큼 이상이어야 «있다» 로 본다.
PROP_MIN_COL_PX = 3

#: 이만큼 가까운 덩어리는 하나로 잇고(같은 프롭의 흩어진 조각), 이보다 좁으면 버린다.
PROP_MERGE_GAP = 8
PROP_MIN_WIDTH = 20

#: 중앙값의 이 배를 넘는 덩어리는 **둘로 가른다** (맨 위 ⚠).
PROP_SPLIT_RATIO = 1.6


def load():
    if not os.path.isfile(SRC):
        raise SystemExit("⚠ 원본이 없습니다: " + SRC)
    im = Image.open(SRC).convert("RGB")
    a = np.asarray(im).astype(np.int16)
    return im, a, a.mean(axis=2), a.max(axis=2) - a.min(axis=2)


def grid_cells(x0, x1, cols):
    """균일 격자 — 구획 폭을 칸 수로 나눈다."""
    step = (x1 - x0 + 1) / float(cols)
    return [(int(round(x0 + i * step)), int(round(x0 + (i + 1) * step)) - 1)
            for i in range(cols)]


def square(im, box):
    """
    한 칸을 20x20 으로 굽는다.

    ⚠ 시트의 칸은 <b>정사각이 아니다</b>(바닥 172x131 · 벽 136x129 — 실측). 20x20 타일로
      쓰려면 어차피 정사각으로 맞춰야 하므로 <b>가로세로를 따로</b> 줄인다. 바위 질감이라
      약간 눌려도 티가 안 나고, 비율을 지키려고 잘라내면 무늬가 끊긴다.
    """
    return np.asarray(im.crop(box).resize((TILE, TILE), Image.LANCZOS)).astype(np.uint8)


def opaque(rgb):
    """바닥·경계·벽 — 완전 불투명. 아래 지형을 덮는다."""
    return np.dstack([rgb, np.full(rgb.shape[:2], 255, np.uint8)])


def keyed(im, box, bg_lum):
    """
    데코 — 띠 배경을 투명하게. 기준이 <b>시트 바탕이 아니라 띠 배경</b>인 것이 요점이다
    (맨 위 ★★). 알파는 20x20 으로 줄이기 <b>전에</b> 만든다 — 줄인 뒤에 만들면
    가장자리에서 배경색이 섞여 테두리가 생긴다(113-2절과 같은 이유).
    """
    crop = im.crop(box)
    a = np.asarray(crop).astype(np.int16)
    lum = a.mean(axis=2)
    sat = a.max(axis=2) - a.min(axis=2)
    art = (lum > bg_lum + PROP_LUM_OVER) | (sat > PROP_SAT_MIN)

    rgba = np.dstack([np.asarray(crop).astype(np.uint8),
                      np.where(art, 255, 0).astype(np.uint8)])
    return np.asarray(Image.fromarray(rgba, "RGBA").resize((TILE, TILE), Image.LANCZOS))


def find_props(lum, sat, y0, y1, width):
    """데코 한 줄의 프롭 경계 상자 (맨 위 ★★)."""
    bg = float(np.median(lum[y0:y0 + 7, 25:width - 20]))
    art = (lum[y0:y1 + 1] > bg + PROP_LUM_OVER) | (sat[y0:y1 + 1] > PROP_SAT_MIN)
    cols = art.sum(axis=0)
    on = cols >= PROP_MIN_COL_PX

    runs, i = [], 0
    while i < width:
        if on[i]:
            j = i
            while j < width and on[j]:
                j += 1
            runs.append([i, j - 1])
            i = j
        else:
            i += 1

    merged = []
    for r in runs:
        if merged and r[0] - merged[-1][1] <= PROP_MERGE_GAP:
            merged[-1][1] = r[1]
        else:
            merged.append(r)
    keep = [r for r in merged if r[1] - r[0] + 1 >= PROP_MIN_WIDTH]
    if not keep:
        return [], bg

    # ⚠ 너무 넓은 덩어리는 «잉크가 가장 적은 열» 에서 둘로 가른다 (맨 위 ⚠).
    med = float(np.median([r[1] - r[0] + 1 for r in keep]))
    split = []
    for s, e in keep:
        if e - s + 1 > med * PROP_SPLIT_RATIO:
            inner = cols[s + 12:e - 11]
            if len(inner):
                cut = s + 12 + int(np.argmin(inner))
                split.append((s, cut - 1))
                split.append((cut, e))
                continue
        split.append((s, e))

    out = []
    for s, e in split:
        sub = art[:, s:e + 1]
        ys = np.where(sub.any(axis=1))[0]
        if not len(ys):
            continue
        out.append((s, y0 + int(ys.min()), e + 1, y0 + int(ys.max()) + 1))
    return out, bg


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print("[바리올라 서식지 타일]")

    im, a, lum, sat = load()
    width = a.shape[1]
    ensure_folder_meta(TILE_ROOT)

    # ── ① 바닥 ────────────────────────────────────────────────────────
    art_g = os.path.join(ART_ROOT, SET_NAME)
    tiles_g = os.path.join(TILE_ROOT, SET_NAME)
    n = 0
    for y0, y1 in GROUND_ROWS:
        for x0, x1 in grid_cells(GROUND_X[0], GROUND_X[1], GROUND_COLS):
            write_tile(opaque(square(im, (x0, y0, x1 + 1, y1 + 1))),
                       art_g, tiles_g, "%s_%02d" % (SET_NAME, n))
            n += 1
    ensure_folder_meta(art_g)
    ensure_folder_meta(tiles_g)
    print("  바닥      %2d종 → %s" % (n, SET_NAME))

    # ── ④ 전이/경계 → Edge ────────────────────────────────────────────
    art_e = os.path.join(ART_ROOT, SET_NAME + "Edge")
    tiles_e = os.path.join(TILE_ROOT, SET_NAME + "Edge")
    ne = 0
    for y0, y1 in EDGE_ROWS:
        for x0, x1 in grid_cells(EDGE_X[0], EDGE_X[1], EDGE_COLS):
            write_tile(opaque(square(im, (x0, y0, x1 + 1, y1 + 1))),
                       art_e, tiles_e, "%sEdge_%02d" % (SET_NAME, ne))
            ne += 1
    ensure_folder_meta(art_e)
    ensure_folder_meta(tiles_e)
    print("  가장자리  %2d종 → %sEdge  ★ 이번엔 원화가 있다(파생 아님)" % (ne, SET_NAME))

    # ── ③ 벽 (⚠ 미배선) ──────────────────────────────────────────────
    art_w = os.path.join(ART_ROOT, SET_NAME + "Wall")
    tiles_w = os.path.join(TILE_ROOT, SET_NAME + "Wall")
    nw = 0
    for y0, y1 in WALL_ROWS:
        for x0, x1 in grid_cells(WALL_X[0], WALL_X[1], WALL_COLS):
            write_tile(opaque(square(im, (x0, y0, x1 + 1, y1 + 1))),
                       art_w, tiles_w, "%sWall_%02d" % (SET_NAME, nw))
            nw += 1
    ensure_folder_meta(art_w)
    ensure_folder_meta(tiles_w)
    print("  벽        %2d종 → %sWall  ⚠ 미배선(NeutralHabitat 에 「벽」 칸이 없다)"
          % (nw, SET_NAME))

    # ── ② 데코 ────────────────────────────────────────────────────────
    art_p = os.path.join(ART_ROOT, SET_NAME + "Props")
    tiles_p = os.path.join(TILE_ROOT, SET_NAME + "Props")
    np_ = 0
    for y0, y1 in PROP_ROWS:
        boxes, bg = find_props(lum, sat, y0, y1, width)
        for box in boxes:
            write_tile(keyed(im, box, bg), art_p, tiles_p, "%sProps_%02d" % (SET_NAME, np_))
            np_ += 1
        print("    데코 줄 y%d~%d — %2d개 (띠 배경 광도 %.0f)" % (y0, y1, len(boxes), bg))
    ensure_folder_meta(art_p)
    ensure_folder_meta(tiles_p)
    print("  데코      %2d종 → %sProps" % (np_, SET_NAME))

    print()
    print("  다음: py -3 Tools/table_update_20260820_variola_habitat.py")
    print("        py -3 Tools/sync_tables_to_assets.py · 유니티 Assets/Refresh")
    return 0


if __name__ == "__main__":
    sys.exit(main())
