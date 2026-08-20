# -*- coding: utf-8 -*-
"""모션 시트의 **좌표를 실측**해 주는 개발용 도구 (2026-08-20 신설).

**왜 있나** — 새 캐릭터가 올 때마다 하는 일이 똑같다: 가로 띠(제목/그림/번호)를 찾고,
단이 몇 개인지 보고, 줄마다 라벨이 몇 개로 잡히는지 세고, 프레임 사이에 빈 열이 있는지
확인한다. 그걸 손으로 하면 매번 같은 일회용 스크립트를 다시 쓰게 된다.

⚠ 이 도구는 **좌표를 지어내지 않는다** — 재서 보여줄 뿐이다. 어느 밴드가 제목이고
  어느 것이 그림인지는 **사람이 시트를 보고** 정한다(제목 줄이 그림과 겹치는 시트가
  실재하기 때문 — 아루 오른쪽 단·베일 패턴 줄).

사용법:
    py -3 Tools/sheet_probe.py <시트파일> [--x0 0 --x1 1535] [--split 830]
    py -3 Tools/sheet_probe.py <시트파일> --labels y0 y1 [--x0 .. --x1 ..]
    py -3 Tools/sheet_probe.py <시트파일> --gaps y0 y1 [--x0 .. --x1 ..]
    py -3 Tools/sheet_probe.py <시트파일> --thin y0 y1 x_from x_to   (가장 얇은 열 찾기)
"""

import argparse
import os
import sys

import numpy as np

from skin_sheet import load_sheet, runs, label_blobs


def bands(sheet, x0, x1, tag):
    m = sheet["mask"][:, x0:x1 + 1]
    ink = m.sum(axis=1)
    print("=== %s (x %d~%d)" % (tag, x0, x1))
    for a, b in runs(ink > 2, 1):
        print("    y %4d~%-4d  h=%3d  최대 잉크 %d" % (a, b, b - a + 1, ink[a:b + 1].max()))


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("sheet")
    ap.add_argument("--x0", type=int, default=0)
    ap.add_argument("--x1", type=int, default=-1)
    ap.add_argument("--split", type=int, default=0, help="단이 둘이면 그 경계 x")
    ap.add_argument("--labels", type=int, nargs=2, metavar=("Y0", "Y1"))
    ap.add_argument("--gaps", type=int, nargs=2, metavar=("Y0", "Y1"))
    ap.add_argument("--thin", type=int, nargs=4, metavar=("Y0", "Y1", "XA", "XB"))
    ap.add_argument("--no-borders", action="store_true")
    args = ap.parse_args()

    path = args.sheet
    if not os.path.isabs(path):
        from vault_path import VAULT
        cand = os.path.join(VAULT, "리소스", "sprites", path)
        if os.path.isfile(cand):
            path = cand

    sheet = load_sheet(path, box_borders=not args.no_borders)
    W = sheet["mask"].shape[1]
    x1 = args.x1 if args.x1 >= 0 else W - 1

    if args.labels:
        y0, y1 = args.labels
        b = label_blobs(sheet["gray"], args.x0, x1, y0, y1)
        print("라벨 %d개" % len(b))
        print("  x:", [(int(s), int(e)) for s, e in b])
        print("  폭:", [int(e - s + 1) for s, e in b])
        return

    if args.gaps:
        y0, y1 = args.gaps
        band = sheet["mask"][y0:y1 + 1, args.x0:x1 + 1].any(axis=0)
        xs = np.where(band)[0]
        print("잉크 x %d~%d" % (xs.min() + args.x0, xs.max() + args.x0) if len(xs) else "잉크 없음")
        print("빈 열(>=4):", [(a + args.x0, b + args.x0) for a, b in runs(~band, 4)])
        return

    if args.thin:
        y0, y1, xa, xb = args.thin
        col = sheet["mask"][y0:y1 + 1, :].sum(axis=0)
        seg = col[xa:xb]
        order = np.argsort(seg)[:12]
        print("가장 얇은 열 12개 (x, 잉크):")
        for i in sorted(order):
            print("    x=%4d  %3d" % (xa + int(i), int(seg[i])))
        return

    if args.split:
        bands(sheet, args.x0, args.split - 1, "왼쪽 단")
        bands(sheet, args.split, x1, "오른쪽 단")
    else:
        bands(sheet, args.x0, x1, "전체")


if __name__ == "__main__":
    main()
