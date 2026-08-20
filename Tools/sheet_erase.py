# -*- coding: utf-8 -*-
"""제목 딱지의 **정확한 상자**를 재서 `erase=[...]` 로 찍어 준다 (2026-08-20 신설).

`sheet_labels.py` 는 딱지를 <b>찾아</b> 주지만 <b>끝을 정확히 못 짚는다</b> — 가로 닫기가
딱지 아래 그림까지 몇 px 끌어와 상자가 길어진다(불칸 메테오 딱지: 543~575 라고 나왔지만
실제는 <b>543~567</b> 이고 568부터는 메테오 그림이었다).

3px 만 길게 잡아도 그 아래 프레임의 <b>머리가 잘린다</b>. 그래서 «찾기» 와 «재기» 를 갈랐다.

<b>재는 방법</b> — 딱지는 «거의 모든 열이 어두운» 줄이 위아래로 쭉 이어진 것이다.
그래서 딱지 x 폭에서 <b>줄마다 어두운 비율</b>을 재고, 그 비율이 :data:`FILL_MIN` 아래로
떨어지는 첫 줄에서 끊는다. 그림은 이 비율이 훨씬 낮다(메테오는 0.40 → 0.26 으로 떨어졌다).

⚠ x 폭은 사람이 준다 — «어디부터 어디까지가 딱지인가» 는 글자를 읽어야 아는 것이고,
  이 도구는 그 폭 안에서 <b>위아래 끝만</b> 재 준다.

사용법 (한 줄에 «이름 y_대략0 y_대략1 x0 x1»):
    py -3 Tools/sheet_erase.py <시트> <딱지목록파일>
    py -3 Tools/sheet_erase.py <시트> --box 메테오 538 580 1090 1260
"""

import argparse
import io
import os
import sys

from sheet_labels import dark_mask, close_x

#: 이 비율 이상 어두우면 «딱지 줄» 로 본다.
FILL_MIN = 0.62


def measure(dark, y0, y1, x0, x1):
    """대략 범위 안에서 <b>가장 긴 «딱지 줄» 뭉치</b>를 찾아 정확한 y 를 돌려준다."""
    best = None
    run0 = None
    for y in range(y0, y1 + 2):
        wide = y <= y1 and dark[y, x0:x1 + 1].mean() >= FILL_MIN
        if wide and run0 is None:
            run0 = y
        elif not wide and run0 is not None:
            if best is None or (y - run0) > (best[1] - best[0] + 1):
                best = (run0, y - 1)
            run0 = None
    return best


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("sheet")
    ap.add_argument("boxes", nargs="?", help="딱지 목록 파일")
    ap.add_argument("--box", nargs=5, metavar=("NAME", "Y0", "Y1", "X0", "X1"))
    args = ap.parse_args()

    path = args.sheet
    if not os.path.isabs(path):
        from vault_path import VAULT
        cand = os.path.join(VAULT, "리소스", "sprites", path)
        if os.path.isfile(cand):
            path = cand

    rows = []
    if args.box:
        n, a, b, c, d_ = args.box
        rows.append((n, int(a), int(b), int(c), int(d_)))
    if args.boxes:
        for line in io.open(args.boxes, encoding="utf-8"):
            line = line.split("#", 1)[0].strip()
            if not line:
                continue
            p = line.split()
            rows.append((p[0], int(p[1]), int(p[2]), int(p[3]), int(p[4])))
    if not rows:
        raise SystemExit("딱지를 하나도 주지 않았습니다.")

    dark = close_x(dark_mask(path))
    out = []
    print("[%s · 딱지 %d개 정밀 측정]" % (os.path.basename(path), len(rows)))
    for name, ay0, ay1, x0, x1 in rows:
        got = measure(dark, ay0, ay1, x0, x1)
        if got is None:
            print("  ⚠ %-18s 딱지 줄을 못 찾았습니다 (대략 y%d~%d x%d~%d)"
                  % (name, ay0, ay1, x0, x1))
            continue
        y0, y1 = got
        print("  %-18s y %4d~%-4d  (대략 %d~%d 에서 찾음 · 높이 %d)"
              % (name, y0, y1, ay0, ay1, y1 - y0 + 1))
        out.append((name, y0, y1, x0, x1))

    print("\n    erase=(")
    for name, y0, y1, x0, x1 in out:
        print("        (%4d, %4d, %4d, %4d),   # %s" % (y0, y1, x0, x1, name))
    print("    ),")


if __name__ == "__main__":
    main()
