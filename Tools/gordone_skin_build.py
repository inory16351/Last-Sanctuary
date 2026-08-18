# -*- coding: utf-8 -*-
"""고르도네(원거리 일반 중립 1006) 모션 시트 → 프레임 분해 (2026-08-19).

원본: ``<볼트>/리소스/asset/Gordone_asset.png`` (1536x1024, RGB — 알파 없음).

★★⚠ <b>거리 키잉만으로는 개체에 구멍이 뚫린다</b> (2026-08-19 실사고)
=====================================================================
처음엔 배경이 균일하니(26,28,29) 거리 키잉으로 끝난다고 봤다. 초록 배경에 얹어 보니
<b>돔 몸통이 벌집처럼 뚫려</b> 있었다. 실측하면 이유가 분명하다 —
<b>돔 내부 픽셀의 25%가 배경과의 거리 14 이하</b>다(껍질 사이 그늘이 배경만큼 어둡다).

★ 그래서 아니사킬과 <b>같은 해법</b>을 쓴다: <b>조각 테두리에서 흘려 채워</b>
  (`scipy.ndimage.label`) <b>테두리와 이어진</b> 배경만 투명하게 한다. 개체 안쪽의 어두운
  픽셀은 테두리와 이어지지 않으므로 <b>지워지지 않는다</b>.
  그 뒤 <b>구멍 메우기</b>(`binary_fill_holes`)로 남은 점구멍까지 닫는다 —
  실루엣 안쪽은 무슨 색이든 개체다.

★ 아니사킬 시트와 다른 점 둘
============================
① <b>행마다 프레임 수가 다르다</b>: 대기 8 · 이동 7 · 공격 6 · 사망 9.
   그래서 격자를 한 번 만들어 쓸 수 없고 <b>행마다 개수를 알려주고</b> 찾는다.
② 안내 글자가 <b>초록 글씨</b>다("공격 모션 (Attack / Ranged Poison)"). 개체는 보라·회색이라
   <b>초록이 우세한 픽셀</b>이 곧 글자다 — 그 한 줄로 지운다.
   ⚠ 글자가 각 행의 <b>맨 위</b>에 걸쳐 있어서, 안 지우면 1번 프레임에 글자가 찍히고
     프레임 경계까지 밀린다(실제로 "Walk)" · "Ranged Poison)" 이 찍혀 나왔다).

★★ <b>좌/우 라벨이 실제 방향과 뒤집혀 있다</b> (아니사킬과 같은 문제)
===================================================================
투사체(보라 독구슬)의 위치를 프레임마다 재서 확인했다:

    시트 라벨 좌(Left)  줄 → 독구슬이 <b>오른쪽</b>으로 날아간다 → 실제로는 <b>오른쪽</b>을 본다
    시트 라벨 우(Right) 줄 → 독구슬이 <b>왼쪽</b>으로  날아간다 → 실제로는 <b>왼쪽</b>을 본다

⚠ 몸통은 좌우가 거의 같다(둥근 돔이라 방향이 안 보인다) — <b>방향을 알려주는 것은
  투사체뿐</b>이다. 그래서 라벨을 무시하고 투사체 방향대로 넣는다.

★ 투사체 프레임은 <b>뽑지 않는다</b>
===================================
이 시트에는 <b>투사체 전용 행이 없다</b>(종양귀 시트에는 있었다 — `neutral_skin_build.py`).
독구슬이 공격 프레임 안에서 몸통과 같이 그려져 있어서, 억지로 떼어내면 프레임마다 크기·위치가
달라 <b>날아가는 궤적이 튄다</b>.

`projectileFrames` 를 비우면 `CombatProjectileFx` 가 <b>폴백 탄환</b>을 쓴다(실행 로그의
*"[Fx] 폴백 침 탄환 9프레임 로드"*). 독을 뱉는 몹에 침 탄환이라 결도 맞는다.
전용 투사체 원화가 오면 그 행만 추가하면 된다.

⚠ <b>사망 행은 안 넣는다</b> — `CharacterSkinSO` 에 사망 모션 칸이 없다(미결 266번).

사용법:  python Tools/gordone_skin_build.py
다음:    python Tools/gen_gordone_skin.py  →  python Tools/measure_skin_tiles.py
"""

import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT
from carcinos_skin_build import (bands, merge_to_count, split_to_count,
                                 write_png, ensure_folder_meta)

SRC = os.path.join(VAULT, "리소스", "asset", "Gordone_asset.png")
DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Gordonae", "Char")

#: 왼쪽 라벨 글자("대기 모션 (Idle)" · "좌 (Left)")를 버리는 x 경계. 실측: 첫 프레임은 150 부터.
LABEL_X_LIMIT = 150

#: 배경 후보 — 배경색과의 채널 최대차가 이 값 이내. 이 중 <b>테두리와 이어진</b> 덩어리만
#: 실제 배경으로 확정한다(맨 위 ★★⚠). ⚠ 이 값을 올려도 개체에 구멍이 안 뚫린다 —
#: 흘려 채우기가 막아 주기 때문이다. 그게 거리 키잉과의 결정적 차이다.
BG_TOL = 14

#: 잘라낸 조각 사방에 이만큼 여백을 붙여 흘린다 — 개체가 조각 테두리에 닿아 있으면
#: 흘릴 시작점이 없어 배경이 하나도 안 지워진다.
PAD = 3

#: 가장자리 한 겹을 반투명으로 — 계단을 눕힌다.
EDGE_SOFT = 1

#: 초록 안내 글자 판정 (맨 위 ★ ②). 실측 대표색 (142,220,180).
LABEL_GREEN_MARGIN = 25
LABEL_GREEN_MIN = 90

#: 글자 주변 안티에일리어싱까지 덮으려고 이만큼 부풀린다.
LABEL_GROW = 2

#: 프레임을 <b>가르는</b> 기준 — 알파 기준보다 높다(연무·잔광에 프레임이 안 붙게).
SEG_THRESHOLD = 30

#: 한 열에 이만큼 이상 찍혀야 「내용 있는 열」로 본다. ⚠ 1 로 두면 <b>독구슬 궤적</b>이
#: 프레임 사이를 메워 공격 행이 통째로 하나가 된다(실측).
COL_INK = 6

# ── 시트 배치 (실측 · 배경거리 30 기준 가로 밴드) ────────────────────────────
#   ★ 이름의 Right/Left 는 <b>실제 방향</b>이다 — 시트 라벨과 반대다(맨 위 ★★).
#   how = "auto" 는 빈 열을 찾아 가른다. "grid" 는 <b>내용 범위를 등분</b>한다.
#
#   ⚠⚠ <b>공격 행만 "grid" 다.</b> 독구슬이 남긴 <b>보라 연무가 프레임 사이를 메워</b>
#     빈 열이 안 생긴다. 임계값을 6가지 x 잉크량 7가지 = 42조합으로 훑어도 6덩어리가 되는
#     조합이 <b>한 행에 하나뿐</b>이었고 그것마저 간격이 [217,207,202,176,18] 로 깨졌다.
#     ★ 그리고 <b>5·6번 프레임에는 몸통이 아예 없다</b>(연무만 날아간다) — 그래서
#       "몸통을 찾는" 방법도 못 쓴다.
#     이 시트는 프레임 간격이 고르므로(대기 8칸 실측 간격 156~187) 등분이 안전하다.
ROWS = [
    ("Idle",         "Right", (13, 150),   8, "auto"),   # 시트 라벨 좌(Left)
    ("Idle",         "Left",  (153, 284),  8, "auto"),   # 시트 라벨 우(Right)
    ("Walk",         "Right", (301, 426),  7, "auto"),
    ("Walk",         "Left",  (439, 552),  7, "auto"),
    ("RangedAttack", "Right", (576, 703),  6, "grid"),
    ("RangedAttack", "Left",  (719, 832),  6, "grid"),
    ("Death",        None,    (858, 1004), 9, "auto"),
]

#: 실제로 뽑을 모션. 사망은 담을 칸이 없어 뺀다(맨 위 ⚠).
WANTED = {"Idle", "Walk", "RangedAttack"}


def label_mask(arr):
    """초록 안내 글자 (맨 위 ★ ②). 개체는 보라·회색이라 초록이 우세할 수 없다."""
    from scipy import ndimage

    r, g, b = arr[..., 0].astype(np.int16), arr[..., 1].astype(np.int16), arr[..., 2].astype(np.int16)
    m = (g > r + LABEL_GREEN_MARGIN) & (g > b + 15) & (g > LABEL_GREEN_MIN)
    if LABEL_GROW > 0:
        m = ndimage.binary_dilation(m, iterations=LABEL_GROW)
    return m


def to_rgba(rgb_block, bgcand):
    """
    <b>테두리에서 이어진 배경만</b> 투명하게 한다 (맨 위 ★★⚠).

    ⚠ 예전 방식(배경색과의 거리를 알파로 쓰기)은 <b>개체 안쪽에 구멍을 뚫는다</b> —
      이 개체는 껍질 사이 그늘이 배경만큼 어둡다(내부 픽셀의 25%가 거리 14 이하).
    """
    from scipy import ndimage

    h, w = bgcand.shape
    padded = np.zeros((h + PAD * 2, w + PAD * 2), dtype=bool)
    padded[PAD:PAD + h, PAD:PAD + w] = bgcand
    padded[:PAD, :] = True
    padded[-PAD:, :] = True
    padded[:, :PAD] = True
    padded[:, -PAD:] = True

    lbl, n = ndimage.label(padded)
    if n == 0:
        return np.dstack([rgb_block, np.full((h, w), 255, np.uint8)])

    outside = np.unique(np.concatenate([lbl[0, :], lbl[-1, :], lbl[:, 0], lbl[:, -1]]))
    outside = outside[outside > 0]
    background = np.isin(lbl, outside)[PAD:PAD + h, PAD:PAD + w]

    # ★ 남은 점구멍까지 닫는다 — 실루엣 안쪽은 무슨 색이든 개체다.
    opaque = ndimage.binary_fill_holes(~background)
    background = ~opaque

    alpha = np.where(background, 0, 255).astype(np.uint8)
    if EDGE_SOFT > 0:
        grown = ndimage.binary_dilation(background, iterations=EDGE_SOFT)
        alpha[np.logical_and(grown, ~background)] = 128
    return np.dstack([rgb_block, alpha])


def grid_frames(seg, y0, y1, count):
    """내용이 있는 x 범위를 <paramref>count</paramref> 등분한다 (위 ⚠⚠ 참조)."""
    col = seg[y0:y1 + 1].sum(axis=0) >= 1
    col[:LABEL_X_LIMIT] = False
    hit = np.where(col)[0]
    if len(hit) == 0:
        raise SystemExit("⚠ y %d~%d 가 비어 있습니다" % (y0, y1))

    lo, hi = int(hit.min()), int(hit.max())
    step = (hi - lo + 1) / float(count)
    return [(int(round(lo + step * i)), int(round(lo + step * (i + 1))) - 1)
            for i in range(count)]


def detect_frames(seg, y0, y1, count):
    """
    한 행에서 프레임 <paramref>count</paramref> 개의 x 범위를 찾는다.

    덩어리가 <b>남으면</b> 좁은 틈부터 합치고(잔광 조각), <b>모자라면</b> 가장 넓은 덩어리를
    균등 분할한다(독구슬 궤적으로 붙은 경우) — 카르시노스 빌더와 같은 두 함수를 쓴다.
    <b>몇 장이어야 하는지 알고 있으므로</b> 임계값을 맞히려 하지 않는다.
    """
    col = seg[y0:y1 + 1].sum(axis=0) >= COL_INK
    col[:LABEL_X_LIMIT] = False

    raw = bands(col, min_len=4)
    if not raw:
        raise SystemExit("⚠ y %d~%d 에서 프레임을 못 찾았습니다" % (y0, y1))

    if len(raw) > count:
        raw = merge_to_count(raw, count)
    elif len(raw) < count:
        raw = split_to_count(raw, count)
    return raw


def main():
    if not os.path.isfile(SRC):
        print("⚠ 원본이 없습니다:", SRC)
        return 1

    im = Image.open(SRC).convert("RGB")
    arr = np.asarray(im).astype(np.uint8)
    bg = arr[0, 0].astype(np.int16)
    dist = np.abs(arr.astype(np.int16) - bg).max(axis=2)

    labels = label_mask(arr)
    mask = (dist > BG_TOL) & ~labels          # 그림의 실제 경계(상자를 재는 데 쓴다)
    seg = (dist > SEG_THRESHOLD) & ~labels    # 프레임을 가르는 데만 쓴다
    bgcand = (dist <= BG_TOL) | labels        # 글자는 배경으로 취급한다

    print("원본 %dx%d · 배경 %s · 초록 글자 %d픽셀 제외"
          % (im.size[0], im.size[1], tuple(int(v) for v in bg), int(labels.sum())))

    made = 0
    for motion, side, (y0, y1), count, how in ROWS:
        if motion not in WANTED:
            continue

        spans = (grid_frames(seg, y0, y1, count) if how == "grid"
                 else detect_frames(seg, y0, y1, count))

        boxes = []
        for x0, x1 in spans:
            sub = mask[y0:y1 + 1, x0:x1 + 1]
            ys = np.where(sub.any(axis=1))[0]
            xs = np.where(sub.any(axis=0))[0]
            if len(ys) == 0:
                raise SystemExit("⚠ %s %s: x %d~%d 가 비어 있습니다" % (motion, side, x0, x1))
            boxes.append((x0 + xs.min(), x0 + xs.max(), y0 + ys.min(), y0 + ys.max()))

        # 캔버스는 이 행 전체가 안 잘리는 최소 크기 — 프레임마다 따로 잡으면
        # 피벗(하단 중앙) 기준이 흔들려 재생 중에 개체가 튄다.
        cw = max(b[1] - b[0] + 1 for b in boxes)
        ch = max(b[3] - b[2] + 1 for b in boxes)

        folder = os.path.join(DST_ROOT, motion)
        for i, (bx0, bx1, by0, by1) in enumerate(boxes):
            rgba = to_rgba(arr[by0:by1 + 1, bx0:bx1 + 1],
                           bgcand[by0:by1 + 1, bx0:bx1 + 1])

            canvas = np.zeros((ch, cw, 4), dtype=np.uint8)
            bw, bh = bx1 - bx0 + 1, by1 - by0 + 1
            canvas[ch - bh:ch, (cw - bw) // 2:(cw - bw) // 2 + bw] = rgba

            name = ("Char_%s_%02d" % (motion, i) if side is None
                    else "Char_%s_%s_%02d" % (motion, side, i))
            write_png(Image.fromarray(canvas, "RGBA"), folder, name)
            made += 1

        ensure_folder_meta(folder)
        print("  %-12s %-5s %d x %d · %d장 (%s)"
              % (motion, side or "-", cw, ch, len(boxes), how))

    ensure_folder_meta(DST_ROOT)
    print("\n%d장 → Art/Char_Asset/Char_Asset_Gordonae/Char/" % made)
    print("다음: python Tools/gen_gordone_skin.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
