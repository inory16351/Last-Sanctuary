# -*- coding: utf-8 -*-
"""밴드마다 **칸 가르기 세 방법을 나란히** 보여준다 (2026-08-20 신설).

`char_sheet.Row.cells` 는 방법이 다섯 가지고(`labels`·`gaps`·`bounds`·`clusters`·`span`)
<b>시트마다 듣는 것이 다르다</b>. 어느 것을 쓸지 고르는 일을 지금까지는 «틀린 방법으로
한 번 돌려 보고 죽는 메시지를 읽어» 정했다 — 시트가 넷이고 줄이 스무 개씩이면 그 왕복이
너무 길다.

그래서 <b>한 번에 다 보여준다</b>. 세 방법이 <b>같은 수</b>를 내면 그 수를 믿어도 되고,
<b>어긋나면</b> 그 줄만 눈으로 본다(`sheet_band.py`). 이것이 이 도구의 전부다.

⚠ 여전히 <b>좌표를 지어내지 않는다</b> — 밴드는 사람이 준다.

사용법 (밴드 파일은 «이름 y0 y1 x0 x1» 한 줄씩 · `#` 은 주석):
    py -3 Tools/sheet_cells.py <시트> <밴드파일>
    py -3 Tools/sheet_cells.py <시트> --band Idle 40 126 398 1025
"""

import argparse
import io
import os
import sys

import numpy as np

from skin_sheet import load_sheet, cells_by_gaps, cells_by_clusters, cells_by_span


def report(sheet, name, y0, y1, x0, x1):
    mask = sheet["mask"]
    band = mask[y0:y1 + 1, x0:x1 + 1]
    col = band.any(axis=0)
    xs = np.where(col)[0]
    if not len(xs):
        print("  %-16s 잉크 없음 — 밴드가 틀렸다" % name)
        return
    ix0, ix1 = int(xs[0]) + x0, int(xs[-1]) + x0

    try:
        g = cells_by_gaps(mask, y0, y1, x0, x1)
    except Exception as e:                       # noqa: BLE001 — 진단 도구다
        g = "오류(%s)" % e
    try:
        c = cells_by_clusters(mask, y0, y1, x0, x1)
    except Exception as e:                       # noqa: BLE001
        c = "오류(%s)" % e

    ng = len(g) if isinstance(g, list) else g
    nc = len(c) if isinstance(c, list) else c
    agree = "" if ng != nc else "   ← 두 방법이 일치"
    print("  %-16s 잉크 x%4d~%-4d   gaps %-4s clusters %-4s%s"
          % (name, ix0, ix1, ng, nc, agree))
    if isinstance(c, list) and len(c) <= 14:
        print("      clusters: %s" % ", ".join("%d~%d" % (a, b) for a, b in c))
    if isinstance(g, list) and len(g) <= 14 and ng != nc:
        print("      gaps    : %s" % ", ".join("%d~%d" % (a, b) for a, b in g))
    # span 은 «장수를 안다» 는 전제라 개수를 못 세어 준다 — 대신 균등 폭을 알려 준다.
    for n in sorted({ng, nc} - {0}):
        if isinstance(n, int) and n > 0:
            print("      span(%d): 칸 폭 %.1f px" % (n, (ix1 - ix0 + 1) / float(n)))


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("sheet")
    ap.add_argument("bands", nargs="?", help="밴드 목록 파일")
    ap.add_argument("--band", nargs=5, metavar=("NAME", "Y0", "Y1", "X0", "X1"))
    args = ap.parse_args()

    path = args.sheet
    if not os.path.isabs(path):
        from vault_path import VAULT
        cand = os.path.join(VAULT, "리소스", "sprites", path)
        if os.path.isfile(cand):
            path = cand

    rows = []
    if args.band:
        n, y0, y1, x0, x1 = args.band
        rows.append((n, int(y0), int(y1), int(x0), int(x1)))
    if args.bands:
        for line in io.open(args.bands, encoding="utf-8"):
            line = line.split("#", 1)[0].strip()
            if not line:
                continue
            p = line.split()
            rows.append((p[0], int(p[1]), int(p[2]), int(p[3]), int(p[4])))
    if not rows:
        raise SystemExit("밴드를 하나도 주지 않았습니다.")

    sheet = load_sheet(path, box_borders=True)
    print("[%s · 밴드 %d개]" % (os.path.basename(path), len(rows)))
    for r in rows:
        report(sheet, *r)


if __name__ == "__main__":
    main()
