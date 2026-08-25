# -*- coding: utf-8 -*-
"""볼트의 유물 아이콘 시트를 <b>칸으로 잘라</b> 프로젝트에 넣는다 (2026-08-25 신설).

유저 지시: *"유물 아이콘 미배정된 것들 볼트에서 이미지 확인해보고 짤라서 추가"*

시트는 넉 장(`리소스/sprites/Lelic_icon_01~04.png`, 각 1254x1254)이고 <b>격자 크기가
서로 다르다</b> — 01~03 은 6x6, 04 는 8x7 이다. 그래서 칸 수를 박아 넣지 않고
<b>어두운 칸 사이 여백(gutter)을 찾아</b> 격자를 잡는다.

★ 왜 자동 검출인가 — 「6으로 나눈다」로 박으면 시트 04 가 <b>어긋난 채로</b> 잘리고,
  잘린 그림은 «조금 이상한데?» 로만 보여서 원인을 찾기 어렵다. 여백은 실제로 거의
  순수한 검정이라 찾기 쉽다.

★ 배경은 <b>투명으로 만들지 않는다</b> — 이 아이콘들은 액자(테두리)가 그려진 «판» 이고,
  유물 칸도 사각형이다. 굳이 배경을 파내면 액자가 깨진다.

★★ <b>«쓰는 칸» 만 Resources 에 넣는다</b> (2026-08-26).

예전에는 164칸을 <b>전부</b> `Resources/RelicIcons/` 에 썼다. 그런데 그 폴더는
<b>런타임에 통째로 딸려 들어가는 자리</b>라, 쓰지도 않는 98칸(5.4MB)이 빌드에 실린다.
그래서 손으로 지웠던 모양인데 — <b>손으로 한 일은 다시 돌리면 되돌아온다</b>.
(`.gitignore` 의 «유물 아이콘 «팔레트»» 항목이 그 흔적이다.)

이제 이 스크립트가 <b>스스로 가른다</b>:
<code>
  쓰는 칸  → Assets/_Project/Resources/RelicIcons/     (커밋한다)
  나머지   → Assets/_Project/Art/RelicIconPalette/     (.gitignore 됨 · 고를 때만 본다)
</code>
「쓰는 칸」의 정본은 :data:`assign_relic_icons.ICONS` 하나다 — 목록을 두 벌로 만들지 않는다.

⚠ <b>배정에서 빠진 그림은 지운다</b>(`.meta` 까지). 안 지우면 «아이콘을 바꿨는데
  옛 그림이 폴더에 남아» 다음 사람이 어느 쪽이 산 것인지 못 가린다.

사용법:  python Tools/slice_relic_icons.py
결과:    Assets/_Project/Resources/RelicIcons/sheet<시트>_<행><열>.png
⚠ 그 뒤 Unity 에서 Assets/Refresh. 어느 유물이 어느 칸을 쓰는지는
  `Tools/assign_relic_icons.py` 가 정한다.
"""
import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT
from assign_relic_icons import ICONS

try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

SRC_DIR = os.path.join(VAULT, '리소스', 'sprites')
DST = os.path.join(PROJECT, 'Assets', '_Project', 'Resources', 'RelicIcons')

#: 쓰지 않는 칸이 가는 곳. `.gitignore` 됨 — «다음에 유물을 더할 때 골라 쓸 후보» 다.
PALETTE_DIR = os.path.join(PROJECT, 'Assets', '_Project', 'Art', 'RelicIconPalette')

#: 지금 실제로 쓰는 칸. 정본은 `assign_relic_icons.py` 하나다.
USED = set(ICONS.values())

SHEETS = ['Lelic_icon_01.png', 'Lelic_icon_02.png',
          'Lelic_icon_03.png', 'Lelic_icon_04.png']

# 여백으로 볼 밝기 문턱 (0~255). 칸 사이는 거의 순수한 검정이다.
GUTTER_MAX = 26

# 여백 줄이 이 비율 이상 어두워야 «여백» 으로 본다
GUTTER_RATIO = 0.97

# 이만큼보다 좁은 칸은 «여백을 잘못 읽은 것» 으로 보고 버린다 (픽셀)
MIN_CELL = 60


def runs_of_content(dark_mask):
    """어둡지 않은(= 내용이 있는) 구간의 (시작, 끝) 목록."""
    out = []
    start = None
    for i, is_gutter in enumerate(dark_mask):
        if not is_gutter and start is None:
            start = i
        elif is_gutter and start is not None:
            out.append((start, i))
            start = None
    if start is not None:
        out.append((start, len(dark_mask)))
    return [(a, b) for a, b in out if b - a >= MIN_CELL]


def slice_sheet(path, sheet_no):
    im = Image.open(path).convert('RGB')
    a = np.asarray(im).astype(np.int16)
    lum = a.max(axis=2)                      # 가장 밝은 채널 — 어두운 여백만 걸러낸다

    col_gutter = (lum <= GUTTER_MAX).mean(axis=0) >= GUTTER_RATIO
    row_gutter = (lum <= GUTTER_MAX).mean(axis=1) >= GUTTER_RATIO

    cols = runs_of_content(col_gutter)
    rows = runs_of_content(row_gutter)

    print('  %s  %dx%d → 격자 %d행 x %d열 = %d칸'
          % (os.path.basename(path), im.size[0], im.size[1],
             len(rows), len(cols), len(rows) * len(cols)))

    made = free = 0
    for ri, (y0, y1) in enumerate(rows, 1):
        for ci, (x0, x1) in enumerate(cols, 1):
            key = 'sheet%d_%02d%02d' % (sheet_no, ri, ci)
            cell = im.crop((x0, y0, x1, y1))
            if key in USED:
                cell.save(os.path.join(DST, key + '.png'))
                made += 1
            else:
                cell.save(os.path.join(PALETTE_DIR, key + '.png'))
                free += 1
    return made, free


def prune():
    """
    배정에서 빠진 그림을 <b>Resources 에서 치운다</b>(`.meta` 까지).

    ⚠ 지우기 전에 <b>이번에 쓰는 칸이 다 만들어졌는지</b> :func:`main` 이 먼저 확인한다 —
      순서가 바뀌면 «다 지웠는데 새것이 안 만들어진» 상태가 될 수 있다.
    """
    gone = []
    for fn in sorted(os.listdir(DST)):
        if not fn.endswith('.png'):
            continue
        if fn[:-4] in USED:
            continue
        os.remove(os.path.join(DST, fn))
        meta = os.path.join(DST, fn + '.meta')
        if os.path.exists(meta):
            os.remove(meta)
        gone.append(fn[:-4])
    return gone


def main():
    os.makedirs(DST, exist_ok=True)
    os.makedirs(PALETTE_DIR, exist_ok=True)
    used = free = 0
    for i, name in enumerate(SHEETS, 1):
        p = os.path.join(SRC_DIR, name)
        if not os.path.exists(p):
            print('  ⚠ 없음: %s' % name)
            continue
        u, f = slice_sheet(p, i)
        used += u
        free += f

    missing = sorted(k for k in USED
                     if not os.path.exists(os.path.join(DST, k + '.png')))
    if missing:
        raise SystemExit('⚠ 배정표가 가리키는 칸이 시트에 없습니다 — 이름을 확인할 것:\n  '
                         + '\n  '.join(missing))

    gone = prune()

    print('쓰는 칸 %d개 → %s' % (used, os.path.relpath(DST, PROJECT)))
    print('후보 칸 %d개 → %s  (.gitignore 됨)' % (free, os.path.relpath(PALETTE_DIR, PROJECT)))
    if gone:
        print('치운 옛 그림 %d개: %s' % (len(gone), ' · '.join(gone)))
    print('⚠ Unity 에서 Assets/Refresh 를 실행할 것.')


if __name__ == '__main__':
    main()
