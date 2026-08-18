# -*- coding: utf-8 -*-
"""아니사킬(에픽 중립 보스 1005) 서식지 타일 — 바닥 · 가장자리 · 데코 (2026-08-19).

유저 지시: *"청크 에셋도 볼트에 넣었으니까 확인해보고 타일 에셋 만들어서 카르시노스 처럼
서식지 청크 생성해 이미지 색 배열 바꿔서 어느 정돈 차이나 보이게 만들고 대신 너무 이질적으로
만들진 마 아까 중앙 건물 청크 만들때 실수 했던 것 처럼"*

원본 — ★ 이번엔 <b>진짜 타일 시트</b>다
=======================================
    <볼트>/리소스/chunk/chunk_anisikill.png   80x80   → 20px 격자 4x4 = <b>바닥 16종</b>
    <볼트>/리소스/chunk/Deco_anisikill.png   160x80   → 20px 격자 8x4 = <b>데코 32종</b>

성역(`gen_sanctuary_tiles.py`)의 원본은 라벨·여백이 붙은 <b>참조 시트</b>라 칸을 다시 굽는
단계가 필요했다. 이건 격자에 딱 맞게 그려진 시트라 <b>그대로 자르면 된다</b> —
안쪽으로 파고들거나 어두운 테두리를 깎는 단계가 필요 없다(실측: 격자선이 없다).

★★ 색은 <b>거의 그대로 둔다</b> — 유저가 지목한 「아까 그 실수」
==============================================================
성역 1차(104-2절)에서 색조를 <b>보라-청(265°)</b> 으로 옮겼다가 *"이거는 색 너무 다르자나"* 로
되돌렸다(106절). 이번 지시는 그 실수를 이름으로 짚었다 — <b>"어느 정돈 차이나"</b> 와
<b>"너무 이질적으로 만들진 마"</b> 가 같이 붙어 있다.

실측해 보니 <b>원본이 이미 알맞은 자리에 있다</b>:

    맵 바닥            H 359°  S 0.49  V 0.37    ← 탁한 벽돌빛
    아니사킬 chunk 원본  H 342°  S 0.54  V 0.28    ← 어두운 장미빛
    카르시노스 서식지    H 309°  S 0.59  V 0.22    ← 어두운 자홍
    성역(넥서스)        H   3°  S 0.70  V 0.24    ← 짙은 핏빛

맵과 <b>17°</b> 떨어져 있고 카르시노스와 <b>33°</b> 떨어져 있다. 네 구역이 색조 축에서
309 → 342 → 359 → 3 으로 <b>골고루 벌어져</b> 있으므로, <b>색조는 손대지 않는다.</b>
색조를 옮기면 어느 한쪽과 붙어 버린다.

★ 그래서 「차이」는 <b>다른 두 축</b>에서 만든다 (106-3절이 찾은 방법과 같다):
    ① <b>맵보다 어둡고 진하게</b> — V 0.37 → 0.26 · S 0.49 → 0.62.
       ⚠ 이 방향이 안전하다: 88-5절에서 유닛이 묻힌 원인은 서식지가 <b>밝아서</b>였다.
    ② <b>호박색 불씨</b> — 원본 바닥의 밝은 점만 골라 조금 더 밝히고 <b>주황</b> 쪽으로
       기울인다. 아니사킬의 <b>주황 아귀</b>가 그 색이라, 바닥이 "그 개체의 자리" 로 읽힌다.
       ★ 이것이 지시의 「배열 바꿔서」에 해당하는 부분이다 — 무늬의 <b>성격</b>을 바꾼다.

    두 축 모두 <b>원본의 색조를 유지</b>하므로 이질감이 생기지 않는다.

★ 가장자리 16종은 <b>바닥에서 파생시킨다</b> (원화가 없다)
=========================================================
유저가 준 것은 바닥·데코 두 장이고 <b>가장자리 시트는 없다</b>. 그 칸이 비면
`NeutralHabitat.Paint` 가 테두리에도 바닥 타일을 깔아 서식지가 <b>맵에 뚝 끊긴다</b>.

카르시노스처럼 <b>바깥으로 잦아드는 테두리</b>를 만들려고, <b>바닥 타일에 방향별 알파 램프</b>를
씌워 16종(4방향 x 바닥 4종)을 굽는다. ⚠ <b>새로 그린 그림이 아니다</b> — 유저의 바닥 타일에
알파만 얹은 것이다. 가장자리 원화가 오면 이 단계를 지우고 그쪽을 자르면 된다.

사용법:  python Tools/gen_anisakil_habitat_tiles.py
다음:    python Tools/sync_tables_to_assets.py  (표의 habitat_tile_asset 이 이미 AnisakilHabitat 다)
"""

import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT
# ★ 자르기·메타·YAML 은 성역 타일 생성기의 것을 <b>그대로 쓴다</b> — 같은 규약
#   (20px · PPU 20 · 결정적 guid · Tile 에셋 형식)이라 복제하면 두 벌이 갈라진다.
from gen_sanctuary_tiles import (TILE, write_tile, ensure_folder_meta,
                                 _rgb_to_hsv, _hsv_to_rgb)

SRC_GROUND = os.path.join(VAULT, "리소스", "chunk", "chunk_anisikill.png")
SRC_PROPS = os.path.join(VAULT, "리소스", "chunk", "Deco_anisikill.png")

ART_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "OrganicTilemap")
TILE_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Resources", "HabitatTiles")

#: 묶음 이름 — 표(`habitat_design.habitat_tile_asset`)에 적힌 값과 <b>같아야 한다</b>.
#: 게임은 `Resources/HabitatTiles/<이름>` · `<이름>Edge` · `<이름>Props` 를 통째로 읽는다.
SET_NAME = "AnisakilHabitat"

# ── 색 보정 — 맨 위 ★★ 참조. <b>색조는 건드리지 않는다.</b> ──────────────────

#: 채도 배율 + 하한. 맵 바닥(0.49)보다 진해야 「탁한 흙」이 아니라 「살」로 읽힌다.
SATURATION = 1.15
SATURATION_FLOOR = 0.42

#: 밝기 — (V − PIVOT) x CONTRAST + PIVOT + LIFT. LIFT 가 <b>음수</b>인 것이 핵심이다
#: (맵보다 어둡게). 106-3절이 성역에서 쓴 것과 같은 방향이고, 폭은 그보다 작다.
VALUE_PIVOT = 0.30
VALUE_CONTRAST = 1.12
VALUE_LIFT = -0.03

#: 호박색 불씨 — 이 밝기를 넘는 픽셀만 조금 밝히고 <b>주황</b> 쪽으로 기울인다.
#: ⚠ 성역(3차)은 문턱 0.52 · 이득 0.46 이었다. 여기는 <b>절반 이하</b>다 —
#:   "어느 정돈" 이라는 지시에 맞춰 눈에 걸리는 정도까지만 올린다.
HIGHLIGHT_FROM = 0.46
HIGHLIGHT_GAIN = 0.18
HIGHLIGHT_HUE_OPEN = 0.075          # +27° → 342° 에서 시작해 주황(≈9°)까지
HIGHLIGHT_DESAT = 0.10

#: ★★ <b>타일마다 평균 밝기를 맞춘다</b> (0 = 원본 그대로, 1 = 전부 같은 밝기).
#:
#: ⚠ 원본 16종의 평균 밝기가 <b>30 ~ 62</b> 로 두 배 넘게 벌어져 있다(실측). 런타임이
#:   칸마다 무작위로 고르므로 그대로 깔면 <b>밝고 어두운 타일이 번갈아 놓인 격자무늬</b>가
#:   보인다 — 서식지를 깔아 보고 눈으로 확인했다. 유기적인 얼룩이 아니라 <b>버그처럼</b> 보인다.
#:
#: ★ 타일 <b>안쪽</b> 무늬는 그대로 두고 <b>평균만</b> 전체 평균 쪽으로 당긴다.
#:   0.75 면 30~62 가 45~53 으로 좁아진다 — 얼룩은 남고 격자는 사라진다.
#: ⚠ 데코·가장자리에는 쓰지 않는다: 데코는 <b>모양이 다른 물건들</b>이라 밝기를 맞추면
#:   대비가 죽고, 가장자리는 바닥에서 파생하므로 이미 맞춰져 있다.
GROUND_LEVEL_MATCH = 0.75

#: 데코 시트의 <b>판 배경</b>. 네 귀퉁이가 전부 이 색이다(실측).
#: 이 색과의 채널 최대차가 이 값 이내면 투명하게 만든다.
PROP_BG_TOL = 22

#: 가장자리 타일 — (이름, 바깥 방향). 방향은 알파가 0 으로 잦아드는 쪽이다.
EDGE_DIRS = [("N", (0, -1)), ("S", (0, 1)), ("W", (-1, 0)), ("E", (1, 0))]

#: 가장자리 한 장에서 <b>완전 불투명으로 남는 안쪽 비율</b>. 나머지가 램프다.
EDGE_SOLID_RATIO = 0.35

#: 가장자리 바깥 끝의 알파(0~1). 0 이면 딱 잘리고, 조금 남기면 스며든다.
EDGE_OUTER_ALPHA = 0.06


def restyle(rgb):
    """맨 위 ★ 의 두 축(어둡고 진하게 · 호박색 불씨)만 적용한다. <b>색조는 그대로.</b>"""
    h, s, v = _rgb_to_hsv(rgb)

    s = np.clip(np.maximum(s * SATURATION, SATURATION_FLOOR), 0.0, 1.0)
    v = np.clip((v - VALUE_PIVOT) * VALUE_CONTRAST + VALUE_PIVOT + VALUE_LIFT, 0.0, 1.0)

    over = np.clip((v - HIGHLIGHT_FROM) / max(1e-6, 1.0 - HIGHLIGHT_FROM), 0.0, 1.0)
    v = np.clip(v + over * HIGHLIGHT_GAIN, 0.0, 1.0)
    h = (h + over * HIGHLIGHT_HUE_OPEN) % 1.0
    s = np.clip(s - over * HIGHLIGHT_DESAT, 0.0, 1.0)

    return _hsv_to_rgb(h, s, v)


def slice_sheet(path):
    """20px 격자를 그대로 잘라 (행, 열) 순서로 돌려준다."""
    im = Image.open(path).convert("RGB")
    arr = np.asarray(im).astype(np.uint8)
    rows, cols = arr.shape[0] // TILE, arr.shape[1] // TILE
    out = []
    for r in range(rows):
        for c in range(cols):
            out.append(arr[r * TILE:(r + 1) * TILE, c * TILE:(c + 1) * TILE])
    return out, rows, cols


def level_match(tile, target_mean):
    """
    타일 평균 밝기를 <see cref="GROUND_LEVEL_MATCH"/> 만큼 전체 평균 쪽으로 당긴다
    (위 ★★ 참조). <b>곱셈</b>으로 맞춘다 — 더하기로 맞추면 어두운 타일의 그림자가 들려
    안개처럼 뿌옇게 된다.
    """
    a = tile.astype(np.float32)
    mine = a.mean()
    if mine <= 1e-3:
        return tile
    want = mine + (target_mean - mine) * GROUND_LEVEL_MATCH
    return np.clip(a * (want / mine), 0, 255).astype(np.uint8)


def opaque(tile, target_mean):
    """바닥 — 완전 불투명. 아래 지형을 덮는다."""
    rgb = restyle(level_match(tile, target_mean))
    return np.dstack([rgb, np.full(rgb.shape[:2], 255, np.uint8)])


def keyed(tile, bg):
    """데코 — 판 배경을 투명하게. 배경이 사방을 둘러싸고 있어 단순 키잉으로 충분하다."""
    dist = np.abs(tile.astype(np.int16) - bg).max(axis=2)
    alpha = np.where(dist <= PROP_BG_TOL, 0, 255).astype(np.uint8)
    return np.dstack([restyle(tile), alpha])


def edge_from_ground(tile, direction):
    """
    바닥 타일 + <b>방향별 알파 램프</b> = 가장자리 타일 (맨 위 ★ 가장자리 참조).

    안쪽 <see cref="EDGE_SOLID_RATIO"/> 는 불투명하게 두고, 바깥으로 갈수록 알파를 내린다.
    ⚠ 색은 안 건드린다 — 어둡게까지 하면 서식지 둘레에 <b>검은 테두리</b>가 생긴다
      (성역 1차에서 겪은 것과 같은 함정 · 102-8절).
    """
    dx, dy = direction
    t = np.linspace(0.0, 1.0, TILE, dtype=np.float32)      # 0 = 안쪽, 1 = 바깥쪽

    if dx != 0:
        ramp = t if dx > 0 else t[::-1]
        ramp = np.tile(ramp, (TILE, 1))
    else:
        ramp = t if dy > 0 else t[::-1]
        ramp = np.tile(ramp[:, None], (1, TILE))

    # 안쪽 구간은 1.0 로 눌러 두고 나머지에서만 잦아든다.
    solid = EDGE_SOLID_RATIO
    fade = np.clip((ramp - solid) / max(1e-6, 1.0 - solid), 0.0, 1.0)
    alpha = (1.0 - fade * (1.0 - EDGE_OUTER_ALPHA)) * 255.0

    return np.dstack([restyle(tile), alpha.astype(np.uint8)])


def main():
    for p in (SRC_GROUND, SRC_PROPS):
        if not os.path.isfile(p):
            print("⚠ 원본이 없습니다:", p)
            return 1

    ensure_folder_meta(TILE_ROOT)

    ground, gr, gc = slice_sheet(SRC_GROUND)
    props, pr, pc = slice_sheet(SRC_PROPS)
    print("바닥 시트 %dx%d = %d종 · 데코 시트 %dx%d = %d종"
          % (gr, gc, len(ground), pr, pc, len(props)))

    # ── ① 바닥 ────────────────────────────────────────────────────────
    art = os.path.join(ART_ROOT, SET_NAME)
    tiles = os.path.join(TILE_ROOT, SET_NAME)
    target = float(np.mean([t.astype(np.float32).mean() for t in ground]))
    print("바닥 평균 밝기 %.1f 로 정렬 (원본 %.0f~%.0f)"
          % (target, min(t.mean() for t in ground), max(t.mean() for t in ground)))
    for i, t in enumerate(ground):
        write_tile(opaque(t, target), art, tiles, "%s_%02d" % (SET_NAME, i))
    ensure_folder_meta(art)
    ensure_folder_meta(tiles)

    # ── ② 가장자리 (바닥에서 파생) ────────────────────────────────────
    art_e = os.path.join(ART_ROOT, SET_NAME + "Edge")
    tiles_e = os.path.join(TILE_ROOT, SET_NAME + "Edge")
    n_edge = 0
    for name, d in EDGE_DIRS:
        for i in range(4):                                  # 바닥 4종만 쓴다
            write_tile(edge_from_ground(level_match(ground[i], target), d), art_e, tiles_e,
                       "%sEdge_%s%02d" % (SET_NAME, name, i))
            n_edge += 1
    ensure_folder_meta(art_e)
    ensure_folder_meta(tiles_e)

    # ── ③ 데코 ────────────────────────────────────────────────────────
    bg = np.asarray(Image.open(SRC_PROPS).convert("RGB"))[0, 0].astype(np.int16)
    art_p = os.path.join(ART_ROOT, SET_NAME + "Props")
    tiles_p = os.path.join(TILE_ROOT, SET_NAME + "Props")
    for i, t in enumerate(props):
        write_tile(keyed(t, bg), art_p, tiles_p, "%sProps_%02d" % (SET_NAME, i))
    ensure_folder_meta(art_p)
    ensure_folder_meta(tiles_p)

    print("  바닥      %2d종  → Resources/HabitatTiles/%s" % (len(ground), SET_NAME))
    print("  가장자리  %2d종  → Resources/HabitatTiles/%sEdge  (바닥에서 파생)"
          % (n_edge, SET_NAME))
    print("  데코      %2d종  → Resources/HabitatTiles/%sProps  (판 배경 %s 키잉)"
          % (len(props), SET_NAME, tuple(int(v) for v in bg)))
    print("\nUnity 에서 Assets/Refresh 를 실행할 것.")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
