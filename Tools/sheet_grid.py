# -*- coding: utf-8 -*-
"""모션 시트에 **좌표 격자**를 덧그려 준다 (2026-08-20 신설).

**왜 있나** — 원화가 교체되면 좌표표는 전부 무효다(120-11절). 그때 하는 일은
«시트를 보면서 구획의 y·x 를 읽는» 것인데, 미리보기만 보면 <b>몇 px 인지 알 수 없다</b>.
축소 배율을 손으로 곱하다 <b>실제로 밴드를 8줄·10줄씩 잘라 먹은 적이 있다</b>(베일 · 120절).

그래서 <b>격자를 그림 위에 직접 얹는다</b>. 100px 마다 굵은 선 + 숫자, 50px 마다 얇은 선.
그러면 «이 구획은 y 380 쯤부터 450 쯤까지» 를 <b>세지 않고 읽을 수 있다</b>.

★ 자동 탐지(`sheet_labels.py`)를 먼저 써 보고 <b>안 되면 이쪽으로 온다</b> — 불칸 시트에서
  딱지 자동 탐지는 오검출이 52개나 나왔다(갑옷·불꽃의 어두운 덩어리가 딱지처럼 보인다).
  <b>사람이 글자를 읽는 편이 확실한 시트가 있다.</b>

⚠ 이 도구는 그림을 **읽기 위한 것**이고 굽는 경로와 아무 상관이 없다 — 격자가 얹힌
  이미지가 프레임으로 나가는 일은 없다(출력 폴더가 다르다).

사용법:
    py -3 Tools/sheet_grid.py <시트파일> <출력폴더> [--rows 2] [--cols 1] [--scale 1.0]
"""

import argparse
import os
import sys

from PIL import Image, ImageDraw, ImageFont

MINOR, MAJOR = 50, 100


def load_font(size):
    for name in ("malgun.ttf", "arial.ttf", "consola.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def flatten(path):
    """알파를 흰 바탕에 얹는다 — 투명 부분이 검게 보이면 구획을 못 읽는다."""
    im = Image.open(path).convert("RGBA")
    bg = Image.new("RGBA", im.size, (255, 255, 255, 255))
    bg.alpha_composite(im)
    return bg.convert("RGB")


def draw_grid(im, ox, oy, scale, font):
    """(ox, oy) 는 이 조각의 <b>원본 좌표</b> 왼쪽 위 — 숫자는 원본 좌표로 적는다."""
    d = ImageDraw.Draw(im, "RGBA")
    W, H = im.size

    def gx(x):     # 원본 x → 조각 x
        return int((x - ox) * scale)

    def gy(y):
        return int((y - oy) * scale)

    x = (ox // MINOR) * MINOR
    while gx(x) <= W:
        if gx(x) >= 0:
            major = x % MAJOR == 0
            d.line([(gx(x), 0), (gx(x), H)],
                   fill=(255, 0, 0, 150) if major else (0, 128, 255, 70),
                   width=2 if major else 1)
            if major:
                d.text((gx(x) + 3, 2), str(x), fill=(200, 0, 0, 255), font=font)
        x += MINOR

    y = (oy // MINOR) * MINOR
    while gy(y) <= H:
        if gy(y) >= 0:
            major = y % MAJOR == 0
            d.line([(0, gy(y)), (W, gy(y))],
                   fill=(255, 0, 0, 150) if major else (0, 128, 255, 70),
                   width=2 if major else 1)
            if major:
                d.text((3, gy(y) + 2), str(y), fill=(200, 0, 0, 255), font=font)
        y += MINOR
    return im


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("sheet")
    ap.add_argument("out")
    ap.add_argument("--rows", type=int, default=2, help="위아래로 몇 조각")
    ap.add_argument("--cols", type=int, default=1, help="좌우로 몇 조각")
    ap.add_argument("--scale", type=float, default=1.0)
    args = ap.parse_args()

    path = args.sheet
    if not os.path.isabs(path):
        from vault_path import VAULT
        cand = os.path.join(VAULT, "리소스", "sprites", path)
        if os.path.isfile(cand):
            path = cand

    im = flatten(path)
    W, H = im.size
    os.makedirs(args.out, exist_ok=True)
    font = load_font(max(11, int(13 * args.scale)))
    base = os.path.splitext(os.path.basename(path))[0]

    ph, pw = H // args.rows, W // args.cols
    made = []
    for r in range(args.rows):
        for c in range(args.cols):
            y0, y1 = r * ph, (r + 1) * ph if r < args.rows - 1 else H
            x0, x1 = c * pw, (c + 1) * pw if c < args.cols - 1 else W
            part = im.crop((x0, y0, x1, y1))
            if args.scale != 1.0:
                part = part.resize((int(part.width * args.scale),
                                    int(part.height * args.scale)), Image.LANCZOS)
            part = draw_grid(part, x0, y0, args.scale, font)
            name = "%s_r%dc%d.png" % (base, r, c)
            part.save(os.path.join(args.out, name))
            made.append((name, x0, x1, y0, y1))

    print("%s  %dx%d → %d조각" % (base, W, H, len(made)))
    for name, x0, x1, y0, y1 in made:
        print("  %s   x %d~%d · y %d~%d" % (name, x0, x1 - 1, y0, y1 - 1))


if __name__ == "__main__":
    main()
