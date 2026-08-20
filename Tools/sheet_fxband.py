# -*- coding: utf-8 -*-
"""이펙트 줄의 **밴드 위·아래끝을 실측**해 «잘리지 않는 값» 을 제안한다 (2026-08-21 신설).

★★ **왜 필요했나** — 유저 리포트: *"이펙트가 안 잘리게 해 윗부분 살짝 잘렸자나"*.
아르세니아 이펙트 <b>열한 줄이 전부</b> 위가 잘려 있었다(심한 것은 33줄). 원인은 하나다:
이펙트를 감싼 <b>둥근 판 테두리</b>를 피하려고 밴드를 안쪽으로 넉넉히 물러 적어 둔 것이다.
테두리는 이미 :data:`Spec.erase` 로 지우므로 물러설 필요가 없었다.

그렇다고 «잉크가 이어지는 끝» 까지 그냥 늘리면 <b>두 가지에 걸린다</b>:

1. **제목 딱지 판** — 이펙트 바로 위에 붙어 있어 프레임에 <b>글자가 박힌다</b>
   (실제로 그렇게 구워졌다). 딱지는 <b>가로로 150~250px 이어지는 한 덩어리</b>인데
   이펙트는 둥근 덩어리라 40~120px 이다 → <b>줄마다 최장 연속 런</b>으로 갈린다.
2. **이웃 이펙트 줄** — 위·아래 줄과 붙어 있으면 그쪽 그림을 물어 온다.
   그래서 <b>다른 줄이 선언한 밴드</b>를 만나면 멈춘다.

즉 이 도구가 내는 값은 «딱지와 이웃 사이에서 이펙트가 실제로 차지하는 범위» 다.
⚠ <b>제안</b>일 뿐이다 — 굽고 나서 눈으로 확인할 것(`Tools/sheet_clip.py` 와 같은 규칙).

사용법:
    py -3 Tools/sheet_fxband.py arsenia_skin_build
    py -3 Tools/sheet_fxband.py chiron_skin_build --label-run 150
"""

import argparse
import importlib
import sys

import numpy as np

from skin_sheet import load_sheet


def longest_run(row_mask):
    """이 줄에서 가장 긴 «연속 잉크» 의 길이(px)."""
    best = cur = 0
    for v in row_mask:
        cur = cur + 1 if v else 0
        if cur > best:
            best = cur
    return best


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("module")
    ap.add_argument("--label-run", type=int, default=150,
                    help="이 길이 이상 이어지면 «제목 딱지 판» 으로 본다")
    args = ap.parse_args()

    spec = importlib.import_module(args.module).SPEC
    sheets = {k: load_sheet(v, box_borders=True) for k, v in spec.sources.items()}
    for y0, y1, x0, x1 in spec.erase:
        sheets["01"]["mask"][y0:y1 + 1, x0:x1 + 1] = False

    print("[%s · 이펙트 밴드 실측]" % spec.title)
    for row in spec.rows:
        if row.kind != "fx":
            continue
        m = sheets[row.src]["mask"]
        sub = m[:, row.x0:row.x1 + 1]
        prof = sub.sum(axis=1)

        # 이웃 줄이 선언한 밴드 — 같은 시트의 다른 줄. 여기 닿으면 멈춘다.
        blocked = np.zeros(len(prof), dtype=bool)
        for other in spec.rows:
            if other is row or other.src != row.src:
                continue
            if other.y0 == row.y0 and other.y1 == row.y1:
                continue          # 같은 그림을 두 칸에 넣는 줄(Impact/ImpactMagic)
            blocked[other.y0:other.y1 + 1] = True

        def is_label(y):
            return longest_run(sub[y]) >= args.label_run

        y0 = row.y0
        while y0 - 1 >= 0 and prof[y0 - 1] > 0 and not blocked[y0 - 1] and not is_label(y0 - 1):
            y0 -= 1
        y1 = row.y1
        while y1 + 1 < len(prof) and prof[y1 + 1] > 0 and not blocked[y1 + 1] and not is_label(y1 + 1):
            y1 += 1

        why = []
        if y0 != row.y0:
            why.append("위 %+d" % (row.y0 - y0))
        if y1 != row.y1:
            why.append("아래 %+d" % (y1 - row.y1))
        tag = "  ← 제안 %d, %d  (%s)" % (y0, y1, " · ".join(why)) if why else "  (그대로)"
        print("  %-24s 현재 %4d~%-4d%s" % (row.name, row.y0, row.y1, tag))


if __name__ == "__main__":
    main()
