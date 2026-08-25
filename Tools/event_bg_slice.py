# -*- coding: utf-8 -*-
"""시트 원화를 잘라 사건 배경으로 굽는다 (2026-08-25).

> 유저: *"BG_01 BG_02 짤라서 이벤트 배경에 써야지"*

원본 (볼트 ``리소스/sprites/``)
-------------------------------
  · ``BG_01.png`` 1536x1024 — 시트 A (3x3 · 9종)
  · ``BG_02.png`` 1672x941  — 시트 B (3x2 · 6종)

결과: ``Assets/_Project/Resources/EventBg/bg_*.png``
      표의 ``event_bg`` 키와 <b>파일 이름이 같아야</b> `EventPanel` 이 찾는다.

★★ <b>균등 분할로 가정하지 않는다</b>
────────────────────────────────
1536/3 은 딱 떨어지지만 <b>칸 사이의 여백(거터)과 바깥 테두리</b>가 있어서
«가로/3» 으로 자르면 <b>칸마다 검은 띠가 남거나 옆 그림이 딸려 온다</b>.
그래서 <b>어두운 띠를 찾아</b> 그 사이를 칸으로 삼는다 —
147-3 절이 느낌표를 «좌표를 박지 않고 세어서» 찾은 것과 같은 태도다.
  ★ 원화가 바뀌어도 다시 돌리면 따라간다.

⚠ <b>거터는 «세로로 끝까지» 어두운 열</b>이다. 그림 자체가 어두운 칸(검은 숲·석실)이 있으므로
  «어둡다» 만으로는 안 되고 <b>그 열 전체가 고르게 어두운가</b>를 본다.

사용법:  py -3 Tools/event_bg_slice.py          (검출 결과만 본다)
         py -3 Tools/event_bg_slice.py --write  (실제로 굽는다)
다음:    유니티에서 Assets/Refresh
         메뉴 LastSanctuary/사건/배경 그림 임포트 설정 고치기
         메뉴 LastSanctuary/사건/배경 그림 점검
"""

import io
import os
import sys

from PIL import Image

from vault_path import VAULT, PROJECT

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

WRITE = "--write" in sys.argv

SRC_DIR = os.path.join(VAULT, "리소스", "sprites")
OUT_DIR = os.path.join(PROJECT, "Assets", "_Project", "Resources", "EventBg")

#: 시트마다 (파일, 열, 행, 왼쪽위부터 읽는 순서의 키 이름들)
#: ⚠ 이름 순서는 <b>프롬프트에 적어 준 순서</b> 그대로다 — 그림을 눈으로 확인해 맞췄다.
SHEETS = [
    ("BG_01.png", 3, 3, [
        "bg_fog",       "bg_aftermath",     "bg_supply",
        "bg_heat",      "bg_ground",        "bg_mind",
        "bg_formation", "bg_wound",         "bg_mana",
    ]),
    ("BG_02.png", 3, 2, [
        "bg_nexus",          "bg_habitat_1101", "bg_habitat_1102",
        "bg_habitat_1103",   "bg_habitat_1104", "bg_default",
    ]),
]

#: ★★ <b>한 줄(열/행)의 «평균 밝기»</b> 가 이 값 아래면 거터다. 0~255.
#:
#: ⚠ 처음에는 «어두운 픽셀의 비율» 로 판정했다가 <b>틀렸다</b> — 그림 자체가 어두운 칸
#:   (빈 곳간 · 석실 · 검은 숲)이 거터로 오인돼 칸이 506·505·<b>409</b> 로 들쭉날쭉해졌다.
#: ★ 실제로 재어 보니 <b>거터는 평균 3~9, 어두운 그림은 14~24</b> 로 <b>깨끗이 갈린다</b>.
#:   비율이 아니라 <b>평균</b>이 가르는 자다(147-3 절이 «세는 규칙» 을 고친 것과 같다).
GUTTER_MEAN = 11

#: 이보다 좁은 덩이는 부스러기로 버린다(칸이 이보다 작을 리 없다).
MIN_CELL = 80

#: 칸 크기가 이보다 더 차이 나면 «고르지 않다» 고 알린다(px).
#: ★ 생성된 격자는 칸이 같아야 한다 — 다르면 검출이 틀린 것이다.
SIZE_TOLERANCE = 12


def bands(is_gutter, min_cell):
    """거터가 아닌 구간(=칸)의 (시작, 끝) 목록. 너무 좁은 것은 버린다."""
    out, start = [], None
    for i, g in enumerate(is_gutter):
        if not g and start is None:
            start = i
        elif g and start is not None:
            if i - start >= min_cell:
                out.append((start, i))
            start = None
    if start is not None and len(is_gutter) - start >= min_cell:
        out.append((start, len(is_gutter)))
    return out


def gutter_mask(grey, axis):
    """axis=0 → 열마다, axis=1 → 행마다 «평균이 GUTTER_MEAN 아래인가»."""
    w, h = grey.size
    px = grey.load()
    n = w if axis == 0 else h
    span = h if axis == 0 else w
    out = []
    for i in range(n):
        total = 0
        for j in range(span):
            total += px[i, j] if axis == 0 else px[j, i]
        out.append(total / span < GUTTER_MEAN)
    return out


def report_uniformity(label, spans):
    """★ 칸 크기가 고른지 본다 — 생성된 격자는 같아야 하고, 다르면 검출이 틀린 것이다."""
    sizes = [b - a for a, b in spans]
    spread = max(sizes) - min(sizes)
    mark = "✓" if spread <= SIZE_TOLERANCE else "⚠"
    print(f"   {mark} {label} 크기 {sizes} — 최대 차이 {spread}px")
    return spread <= SIZE_TOLERANCE


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    total = 0

    for fn, cols, rows, names in SHEETS:
        path = os.path.join(SRC_DIR, fn)
        if not os.path.isfile(path):
            print(f"  ⚠ 원본 없음: {path}")
            continue

        im = Image.open(path).convert("RGB")
        grey = im.convert("L")
        w, h = im.size
        print(f"\n■ {fn}  {w}x{h}  →  {cols}x{rows} = {cols * rows}칸")

        xs = bands(gutter_mask(grey, 0), MIN_CELL)
        ys = bands(gutter_mask(grey, 1), MIN_CELL)
        print(f"   가로 덩이 {len(xs)}개: " + " · ".join(f"{a}~{b}" for a, b in xs))
        print(f"   세로 덩이 {len(ys)}개: " + " · ".join(f"{a}~{b}" for a, b in ys))

        if len(xs) == cols and len(ys) == rows:
            report_uniformity("가로", xs)
            report_uniformity("세로", ys)

        if len(xs) != cols or len(ys) != rows:
            # ⚠ 조용히 «대충» 자르지 않는다 — 잘못 자른 그림이 게임에 실리는 것이
            #   못 자른 것보다 나쁘다.
            print(f"   ✗ 칸을 {cols}x{rows} 로 가르지 못했습니다. "
                  f"DARK({DARK})·DARK_RATIO({DARK_RATIO}) 를 조정하십시오.")
            continue

        for r, (y0, y1) in enumerate(ys):
            for c, (x0, x1) in enumerate(xs):
                idx = r * cols + c
                name = names[idx]
                cell = im.crop((x0, y0, x1, y1))
                out = os.path.join(OUT_DIR, name + ".png")
                print(f"   {name:18s} {cell.size[0]:4d}x{cell.size[1]:<4d} "
                      f"({x0},{y0})–({x1},{y1})")
                if WRITE:
                    cell.save(out)
                total += 1

    print(f"\n칸 {total}개" + ("" if WRITE else "   ⚠ 미리보기 — 쓰려면 --write"))
    if WRITE:
        print(f"→ {OUT_DIR}")
        print("다음: 유니티 Assets/Refresh → 「배경 그림 임포트 설정 고치기」 → 「배경 그림 점검」")


if __name__ == "__main__":
    main()
