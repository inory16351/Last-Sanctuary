# -*- coding: utf-8 -*-
"""분해 스크립트의 :data:`SPEC` 을 그대로 읽어 **줄마다 칸 가르기를 진단**한다 (2026-08-20 신설).

**왜 필요했나** — 불칸 시트를 ``span``(폭 ÷ 장수) 으로 갈랐더니 <b>절반이 어긋났다</b>.
원인이 분명하다: ``span`` 은 «잉크가 놓인 폭» 을 장수로 나누는데, 줄 끝에 <b>몸통이
아닌 것</b>(손을 떠난 탄 · 마법진)이 붙어 있으면 그 폭까지 나눗셈에 들어가
<b>모든 칸이 조금씩 밀린다</b>. 밀린 칸은 «반쪽 몸통» 으로 구워져 나온다.

그리고 이 시트는 프레임 간격이 <b>고르지 않다</b>(이동 줄 실측: 94·93·92·99·106·106·109).
즉 ``span`` 이 애초에 맞을 수 없는 시트였다.

그래서 필요한 것은 <b>줄마다 실제 경계를 재서 그대로 `bounds` 로 박는</b> 일이고,
그때 <b>제목 딱지를 지운 뒤</b>에 재야 한다(딱지가 남아 있으면 그것이 첫 칸이 된다).
:data:`SPEC` 을 그대로 읽으므로 <b>굽는 것과 똑같은 상태</b>에서 잰다 — 이것이 이 도구의 요점이다.

★ 좌·우 줄은 <b>같은 격자</b>에 놓여 있다. 그래서 한쪽에서 깨끗하게 잡힌 경계를
  <b>다른 쪽에도 그대로 쓸 수 있다</b>(그쪽은 이펙트가 칸을 붙여 못 잡는 경우가 있다).
  이 도구가 찍어 주는 ``bounds=`` 를 두 줄에 함께 붙이면 된다.

사용법:
    py -3 Tools/sheet_rows.py vulcan_skin_build
    py -3 Tools/sheet_rows.py vulcan_skin_build --only Walk Melee
"""

import argparse
import importlib
import sys

import numpy as np

from skin_sheet import load_sheet, cells_by_gaps, cells_by_clusters


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("module", help="분해 스크립트 이름 (예: vulcan_skin_build)")
    ap.add_argument("--only", nargs="*", default=None, help="이름에 이 말이 든 줄만")
    args = ap.parse_args()

    spec = importlib.import_module(args.module).SPEC
    sheets = {k: load_sheet(v, box_borders=True) for k, v in spec.sources.items()}

    # ⚠ 굽는 것과 <b>똑같은 순서</b>로 딱지를 먼저 지운다(char_sheet.run 과 같다).
    for y0, y1, x0, x1 in spec.erase:
        sheets["01"]["mask"][y0:y1 + 1, x0:x1 + 1] = False

    print("[%s · 줄 %d개]" % (spec.title, len(spec.rows)))
    print("  줄 이름            기대  gaps clusters  잉크 x")
    for row in spec.rows:
        if args.only and not any(k.lower() in row.name.lower() for k in args.only):
            continue
        mask = sheets[row.src]["mask"]
        band = mask[row.y0:row.y1 + 1, row.x0:row.x1 + 1]
        xs = np.where(band.any(axis=0))[0]
        if not len(xs):
            print("  %-18s %4d   잉크 없음 — 밴드가 틀렸다" % (row.name, row.expect))
            continue
        ix0, ix1 = int(xs[0]) + row.x0, int(xs[-1]) + row.x0
        g = cells_by_gaps(mask, row.y0, row.y1, row.x0, row.x1)
        c = cells_by_clusters(mask, row.y0, row.y1, row.x0, row.x1)
        flag = "  ← clusters 가 기대와 같다" if len(c) == row.expect else ""
        if len(g) == row.expect and len(c) != row.expect:
            flag = "  ← gaps 가 기대와 같다"
        print("  %-18s %4d  %4d %5d      x%4d~%-4d%s"
              % (row.name, row.expect, len(g), len(c), ix0, ix1, flag))
        best = c if len(c) == row.expect else (g if len(g) == row.expect else None)
        if best is not None:
            # ★ 그대로 붙일 수 있는 형태로 찍는다. 경계는 «다음 칸의 시작» 이므로
            #   칸의 끝+1 이 아니라 <b>다음 칸의 x0</b> 를 쓴다(char_sheet 의 bounds 규칙).
            bounds = [best[0][0]] + [b[0] for b in best[1:]] + [best[-1][1] + 1]
            print("      bounds=%s" % (bounds,))
        else:
            print("      ⚠ 두 방법 다 어긋난다 — 다른 쪽 줄의 bounds 를 쓰거나 눈으로 재라")


if __name__ == "__main__":
    main()
