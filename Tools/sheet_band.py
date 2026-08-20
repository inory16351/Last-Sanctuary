# -*- coding: utf-8 -*-
"""밴드 하나를 **격자와 함께 잘라** 보여준다 (2026-08-20 신설).

`sheet_grid.py` 는 시트 <b>전체</b>를 조각내 주고, 이 도구는 <b>한 줄만</b> 크게 잘라 준다.
왜 둘 다 필요한가 — 구획을 찾는 일과 «그 구획이 몇 칸인가» 를 세는 일은 필요한 배율이 다르다.
프레임이 서로 <b>닿아 있으면</b> 빈 열이 안 생겨 `--gaps` 가 칸을 놓치는데
(불칸 대기 줄: 눈으로 7장인데 빈 열로는 6칸), 그때 <b>눈으로 세는 것</b> 말고는 방법이 없다.

★ 잘라낸 그림에 <b>원본 x 좌표</b>를 25px 마다 적는다 — 그래야 «세 번째 칸은 624부터» 를
  바로 `bounds` 로 옮겨 적을 수 있다.

사용법:
    py -3 Tools/sheet_band.py <시트> <출력png> --band y0 y1 x0 x1 [--scale 2]
"""

import argparse
import os
import sys

from PIL import Image, ImageDraw

from sheet_grid import flatten, load_font


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("sheet")
    ap.add_argument("out")
    ap.add_argument("--band", type=int, nargs=4, required=True,
                    metavar=("Y0", "Y1", "X0", "X1"))
    ap.add_argument("--scale", type=float, default=2.0)
    ap.add_argument("--step", type=int, default=25, help="x 눈금 간격(원본 px)")
    args = ap.parse_args()

    path = args.sheet
    if not os.path.isabs(path):
        from vault_path import VAULT
        cand = os.path.join(VAULT, "리소스", "sprites", path)
        if os.path.isfile(cand):
            path = cand

    y0, y1, x0, x1 = args.band
    im = flatten(path).crop((x0, y0, x1 + 1, y1 + 1))
    s = args.scale
    im = im.resize((int(im.width * s), int(im.height * s)), Image.LANCZOS)

    # 눈금을 그릴 자리를 위에 덧붙인다 — 그림 위에 겹치면 프레임 경계가 안 보인다.
    pad = 18
    canvas = Image.new("RGB", (im.width, im.height + pad), (255, 255, 255))
    canvas.paste(im, (0, pad))
    d = ImageDraw.Draw(canvas)
    font = load_font(11)
    x = (x0 // args.step) * args.step
    while x <= x1:
        px = int((x - x0) * s)
        if 0 <= px < canvas.width:
            major = (x % (args.step * 4) == 0)
            d.line([(px, pad), (px, canvas.height)],
                   fill=(255, 0, 0) if major else (120, 190, 255), width=1)
            if major:
                d.text((px + 2, 2), str(x), fill=(200, 0, 0), font=font)
        x += args.step

    canvas.save(args.out)
    print("%s  y%d~%d x%d~%d → %s (%dx%d · x%.1f)"
          % (os.path.basename(path), y0, y1, x0, x1, args.out,
             canvas.width, canvas.height, s))


if __name__ == "__main__":
    main()
