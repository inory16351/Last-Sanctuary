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

try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

SRC_DIR = os.path.join(VAULT, '리소스', 'sprites')
DST = os.path.join(PROJECT, 'Assets', '_Project', 'Resources', 'RelicIcons')

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

    made = 0
    for ri, (y0, y1) in enumerate(rows, 1):
        for ci, (x0, x1) in enumerate(cols, 1):
            cell = im.crop((x0, y0, x1, y1))
            out = os.path.join(DST, 'sheet%d_%02d%02d.png' % (sheet_no, ri, ci))
            cell.save(out)
            made += 1
    return made, len(rows), len(cols)


def main():
    os.makedirs(DST, exist_ok=True)
    total = 0
    for i, name in enumerate(SHEETS, 1):
        p = os.path.join(SRC_DIR, name)
        if not os.path.exists(p):
            print('  ⚠ 없음: %s' % name)
            continue
        n, r, c = slice_sheet(p, i)
        total += n
    print('총 %d칸을 %s 에 넣었다.' % (total, DST))
    print('⚠ Unity 에서 Assets/Refresh 를 실행할 것.')


if __name__ == '__main__':
    main()
