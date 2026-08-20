# -*- coding: utf-8 -*-
"""**칸마다** 그림이 밴드 위·아래에 닿는지 검사한다 (2026-08-21 신설).

`char_sheet.warn_if_clipped` 는 <b>줄 전체</b>를 한 번에 보므로 «어느 프레임이» 잘렸는지
알려주지 않고, 딱지·판 모서리가 걸리면 매번 경고가 떠서 <b>진짜 잘림이 묻힌다</b>
(아르세니아는 20줄 중 19줄에 경고가 떴다 — 그래서 «머리가 잘린 한 장» 을 못 봤다).

이 도구는 :func:`char_sheet.cells_of` 와 <b>같은 칸 가르기</b>를 쓴 뒤 칸마다
① 밴드 위끝에 닿는가 ② 아래끝에 닿는가 ③ 그 칸 바로 위·아래에 잉크가 이어지는가 를 본다.
③ 이 참일 때만 <b>정말 잘린 것</b>이다 — 닿기만 하고 밖이 비어 있으면 딱 맞게 잡은 것이다.

사용법:
    py -3 Tools/sheet_clip.py arsenia_skin_build
    py -3 Tools/sheet_clip.py arsenia_skin_build --only Walk Idle
"""

import argparse
import importlib
import sys

import numpy as np

from char_sheet import cells_of
from skin_sheet import load_sheet

#: 밴드 밖 몇 px 를 «이어지는가» 판정에 볼 것인가.
MARGIN = 5


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("module")
    ap.add_argument("--only", nargs="*", default=None)
    ap.add_argument("--min-ink", type=int, default=3,
                    help="밴드 밖 잉크가 이 값을 넘으면 «잘렸다» 로 본다")
    args = ap.parse_args()

    spec = importlib.import_module(args.module).SPEC
    sheets = {k: load_sheet(v, box_borders=True) for k, v in spec.sources.items()}
    for y0, y1, x0, x1 in spec.erase:
        sheets["01"]["mask"][y0:y1 + 1, x0:x1 + 1] = False

    print("[%s · 칸마다 잘림 검사]" % spec.title)
    bad = 0
    for row in spec.rows:
        if args.only and not any(k in row.name for k in args.only):
            continue
        sheet = sheets[row.src]
        m = sheet["mask"]
        h = m.shape[0]
        try:
            cells = cells_of(sheet, row)
        except SystemExit as e:
            print("  %-22s ⚠ 칸 가르기 실패: %s" % (row.name, e))
            continue

        for i, (cx0, cx1) in enumerate(cells):
            sub = m[row.y0:row.y1 + 1, cx0:cx1 + 1]
            if not sub.any():
                continue
            up = int(m[max(0, row.y0 - MARGIN):row.y0, cx0:cx1 + 1].sum())
            dn = int(m[row.y1 + 1:min(h, row.y1 + 1 + MARGIN), cx0:cx1 + 1].sum())
            touch_up = bool(sub[0].any())
            touch_dn = bool(sub[-1].any())

            notes = []
            if touch_up and up > args.min_ink:
                notes.append("위로 %d px 이어진다 (머리가 잘린다)" % up)
            if touch_dn and dn > args.min_ink:
                notes.append("아래로 %d px 이어진다 (발이 잘린다)" % dn)
            if notes:
                bad += 1
                print("  %-18s %d번 칸 x%4d~%-4d  ← %s"
                      % (row.name, i, cx0, cx1, " · ".join(notes)))

    print("  잘린 칸 %d개" % bad)


if __name__ == "__main__":
    main()
