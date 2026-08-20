# -*- coding: utf-8 -*-
"""줄마다 **프레임 경계 후보**를 «빈 열 + 국소 최소» 로 찾아 ``bounds=`` 로 찍는다 (2026-08-21 신설).

★★ **왜 또 도구를 만들었나** — `sheet_rows.py` 는 «``gaps`` / ``clusters`` 중 어느 쪽이
기대 장수와 같은가» 를 알려 준다. 그런데 <b>개수가 맞아도 경계가 틀릴 수 있다.</b>
아르세니아 「스킬 3」 이 정확히 그랬다:

  · 눈으로 세면 <b>6장</b>인데 :data:`Row.expect` 에 <b>7</b> 이 적혀 있었고,
  · ``clusters`` 도 <b>7</b> 을 내놓아 개수 검사를 <b>통과</b>했다
    (3번 프레임의 후광이 끊겨 두 덩어리로 잡힌 것이다),
  · 그래서 «반쪽 몸통 + 날개만» 두 장이 조용히 구워졌다.

즉 개수 검사는 <b>경계의 정당성을 보증하지 않는다.</b> 이 도구는 그 대신
<b>열마다 잉크 두께</b>를 보고 «프레임이 갈리는 자리» 를 직접 찾는다:

1. **빈 열**(잉크 0) 구간 — 프레임이 떨어져 있으면 여기서 갈린다.
2. **국소 최소** — 후광·장판이 닿아 빈 열이 안 생기는 줄에서도, 두 몸통 사이는
   <b>반드시 얇아진다</b>. 창을 옮기며 «주변보다 낮고 최대의 일정 비율 아래» 인 골을 찾는다.

⚠ 이 도구는 <b>후보</b>를 낸다. 장수가 맞지 않으면 골 판정 문턱(``--floor``)을 올리거나
  내려 다시 보고, <b>마지막 판단은 눈으로 한다</b>(`sheet_band.py` 로 같은 줄을 확대해 대조).

사용법:
    py -3 Tools/sheet_split.py arsenia_skin_build
    py -3 Tools/sheet_split.py arsenia_skin_build --only Skill3 Walk --floor 0.5
"""

import argparse
import importlib
import sys

import numpy as np

from skin_sheet import load_sheet


def gaps_and_valleys(col, x0, floor_ratio, window):
    """빈 열 구간과 국소 최소의 x 좌표 목록."""
    cuts = []

    # ① 빈 열 — 구간의 가운데를 경계로 삼는다.
    i = 0
    while i < len(col):
        if col[i] == 0:
            j = i
            while j < len(col) and col[j] == 0:
                j += 1
            if i > 0 and j < len(col):          # 줄 양 끝의 여백은 경계가 아니다
                cuts.append((x0 + (i + j) // 2, 0, "빈 열 %d px" % (j - i)))
            i = j
        else:
            i += 1

    # ② 국소 최소 — 후광이 이어진 줄용. 평활 후 «창 안에서 최소이고 문턱 아래» 인 자리.
    if len(col) > 2 * window:
        sm = np.convolve(col.astype(float), np.ones(7) / 7.0, mode="same")
        floor = col.max() * floor_ratio
        found = []
        for i in range(window, len(col) - window):
            w = sm[i - window:i + window + 1]
            if sm[i] == w.min() and sm[i] <= floor:
                found.append(i)
        for i in found:
            x = x0 + i
            if any(abs(x - c[0]) <= window for c in cuts):
                continue
            cuts.append((x, int(col[i]), "골 %d px" % int(col[i])))

    return sorted(cuts)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("module")
    ap.add_argument("--only", nargs="*", default=None)
    ap.add_argument("--floor", type=float, default=0.55,
                    help="국소 최소로 인정할 상한 (줄 최대 잉크의 비율)")
    ap.add_argument("--window", type=int, default=14, help="국소 최소를 볼 창 반폭(px)")
    args = ap.parse_args()

    spec = importlib.import_module(args.module).SPEC
    sheets = {k: load_sheet(v, box_borders=True) for k, v in spec.sources.items()}
    for y0, y1, x0, x1 in spec.erase:
        sheets["01"]["mask"][y0:y1 + 1, x0:x1 + 1] = False

    print("[%s · 프레임 경계 후보]" % spec.title)
    for row in spec.rows:
        if args.only and not any(k in row.name for k in args.only):
            continue
        m = sheets[row.src]["mask"]
        col = m[row.y0:row.y1 + 1, row.x0:row.x1 + 1].sum(axis=0)
        if not col.any():
            print("  %-22s ⚠ 밴드가 비어 있다" % row.name)
            continue

        ink = np.where(col > 0)[0]
        lo, hi = row.x0 + int(ink[0]), row.x0 + int(ink[-1])
        cuts = [c for c in gaps_and_valleys(col, row.x0, args.floor, args.window)
                if lo < c[0] < hi]
        n = len(cuts) + 1
        mark = "" if n == row.expect else "  ← ⚠ 기대 %d 장과 다르다" % row.expect
        print("  %-22s 잉크 x%4d~%-4d · 경계 %d개 → %d장%s"
              % (row.name, lo, hi, len(cuts), n, mark))
        print("      bounds=%s" % ([lo] + [c[0] for c in cuts] + [hi + 1],))
        if cuts:
            print("      근거: %s" % " · ".join("%d(%s)" % (c[0], c[2]) for c in cuts))


if __name__ == "__main__":
    main()
