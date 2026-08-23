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

from skin_sheet import load_sheet, runs, label_blobs, erase_title_pills


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
    ap.add_argument("--cells", type=int, nargs=2, metavar=("Y0", "Y1"),
                    help="이 y 창에서 프레임 덩어리를 세고 그 덩어리의 «진짜 세로 범위» 까지 잰다")
    ap.add_argument("--look", type=int, default=60,
                    help="--cells 가 밴드 위·아래로 더 살펴볼 폭(px)")
    ap.add_argument("--gap", type=int, default=1, help="--cells 가 칸을 가를 최소 빈 열")
    ap.add_argument("--thin", type=int, nargs=4, metavar=("Y0", "Y1", "XA", "XB"))
    ap.add_argument("--no-borders", action="store_true")
    ap.add_argument("--pills", action="store_true",
                    help="재기 전에 제목 딱지를 지운다 (skin_sheet.erase_title_pills)")
    ap.add_argument("--pill-run", type=int, default=None,
                    help="--pills 의 «어두운 가로 런» 문턱(기본 120)")
    args = ap.parse_args()

    path = args.sheet
    if not os.path.isabs(path):
        from vault_path import VAULT
        cand = os.path.join(VAULT, "리소스", "sprites", path)
        if os.path.isfile(cand):
            path = cand

    sheet = load_sheet(path, box_borders=not args.no_borders)
    if args.pills:
        erase_title_pills(sheet, min_run=args.pill_run)
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

    if args.cells:
        # ★★ <b>«칸을 세는 창» 과 «잘렸는지 재는 창» 을 따로 둔다</b> (2026-08-22 신설).
        #
        # 이 프로젝트의 시트들은 제목 딱지가 <b>그림 줄과 x 가 겹친다</b>. 그래서
        # 밴드를 딱지 아래로 내려 잡는데(세라피엘 이동 = y168), 그러면 <b>딱지 밑변의
        # 안티에일리어싱 한 줄</b>이 첫 칸에 들어와 칸 가르기가 어긋난다.
        #
        # → <b>칸은 딱지가 확실히 없는 좁은 창</b>에서 세고(``--cells y0 y1``),
        #   그 칸의 <b>진짜 세로 범위</b>는 위·아래로 ``--look`` px 더 넓혀 잰다.
        #   두 값이 다르면 <b>밴드가 그림을 자르고 있다</b>는 뜻이다.
        y0, y1 = args.cells
        m = sheet["mask"]
        band = m[y0:y1 + 1, args.x0:x1 + 1].any(axis=0)
        cells = [(a + args.x0, b + args.x0) for a, b in runs(band, 1)]
        if args.gap > 1:
            # 빈 열이 ``--gap`` 보다 좁으면 한 칸으로 붙인다.
            merged = [list(cells[0])] if cells else []
            for a, b in cells[1:]:
                if a - merged[-1][1] - 1 < args.gap:
                    merged[-1][1] = b
                else:
                    merged.append([a, b])
            cells = [tuple(c) for c in merged]
        wy0, wy1 = max(0, y0 - args.look), min(m.shape[0] - 1, y1 + args.look)
        print("칸 %d개 (창 y%d~%d · 빈 열 %d 이상에서 가름)" % (len(cells), y0, y1, args.gap))
        print("  bounds = %r" % ([c[0] for c in cells] + [cells[-1][1] + 1] if cells else []))
        print("  칸별 — x범위 / 폭 / 이 x 에서 잉크가 실제로 놓인 y (창 y%d~%d)" % (wy0, wy1))
        for i, (a, b) in enumerate(cells):
            colink = m[wy0:wy1 + 1, a:b + 1].any(axis=1)
            segs = [(int(s0) + wy0, int(s1) + wy0) for s0, s1 in runs(colink, 1)]
            inside = [t for t in segs if not (t[1] < y0 or t[0] > y1)]
            cut = []
            if inside:
                if inside[0][0] < y0:
                    cut.append("위 %d" % (y0 - inside[0][0]))
                if inside[-1][1] > y1:
                    cut.append("아래 %d" % (inside[-1][1] - y1))
            print("    %2d  x %4d~%-4d w=%3d  y %s%s"
                  % (i, a, b, b - a + 1, inside,
                     "   ⚠ 밴드 밖으로 " + "·".join(cut) if cut else ""))
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
