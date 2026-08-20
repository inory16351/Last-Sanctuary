# -*- coding: utf-8 -*-
"""모션 시트의 **제목 딱지**를 찾아 준다 (2026-08-20 신설).

**왜 필요했나** — 2026-08-20 오후에 캐릭터 원화 네 장이 통째로 교체됐고
(아르세니아·카이론·불칸·아루) <b>레이아웃까지 바뀌었다</b>. 옛 좌표표는 전부 무효다.

`sheet_probe.py` 는 «가로 띠» 를 찾아 주지만 이 시트들에서는 <b>거의 못 쓴다</b> —
제목 딱지가 <b>검은 판때기</b>라서 잉크로 잡히고, 그러면 딱지와 그 아래 그림이 한 띠로
붙어 버린다(실제로 998px 짜리 띠 하나가 나왔다).

그런데 바로 그 성질을 <b>뒤집어 쓸 수 있다</b>: 제목 딱지는

* <b>아주 어둡고</b>(글자만 흰색)
* <b>가로로 길고</b> 높이가 일정하며
* <b>속이 꽉 차 있다</b>(그림은 속이 비거나 알록달록하다)

그래서 딱지를 먼저 찾으면 <b>시트가 스스로 구획을 알려 준다</b> — 딱지 하나가 곧
한 구획의 «머리» 다. 사람이 미리보기를 보며 세는 일이 줄고, 무엇보다 <b>구획을
빠뜨리지 않는다</b>(아르세니아 시트에서 한 띠에 구획이 셋씩 들어 있어 실제로 빠뜨렸다).

⚠ 이 도구도 **좌표를 지어내지 않는다** — 딱지의 상자와 «딱지 아래 첫 그림줄» 만
  재서 보여준다. 어느 딱지가 어느 모션인지는 <b>사람이 글자를 읽고</b> 정한다
  (`--crop` 으로 딱지 부분만 잘라 볼 수 있다).

사용법:
    py -3 Tools/sheet_labels.py <시트파일>
    py -3 Tools/sheet_labels.py <시트파일> --crop <출력폴더>   (딱지마다 잘라 저장)
"""

import argparse
import os
import sys

import numpy as np
from PIL import Image

from skin_sheet import load_sheet, runs

#: 딱지로 볼 최대 밝기. 글자는 흰색이라 평균이 아니라 <b>어두운 픽셀 비율</b>로 본다.
DARK_LUM = 90
#: 한 줄이 «딱지 줄» 로 인정될 최소 어두운 픽셀 수.
MIN_DARK_RUN = 40
#: 딱지 높이의 허용 범위(px). 이보다 얇으면 글자·그림자, 두꺼우면 그림이다.
LABEL_H = (14, 40)
#: 딱지 폭의 최소값(px).
LABEL_W_MIN = 60


def dark_mask(path):
    im = Image.open(path)
    im = im.convert("RGBA")
    a = np.asarray(im).astype(np.int32)
    rgb, alpha = a[:, :, :3], a[:, :, 3]
    lum = (rgb[:, :, 0] * 299 + rgb[:, :, 1] * 587 + rgb[:, :, 2] * 114) // 1000
    return (lum <= DARK_LUM) & (alpha > 8)


#: 가로 닫기 반지름(px). 딱지 안 흰 글자의 획 사이를 메울 만큼이면 된다.
CLOSE_X = 12


def close_x(mask, k=CLOSE_X):
    """
    ★★ <b>가로로 «닫기»</b> — 늘렸다가(dilate) 같은 양만큼 줄인다(erode).

    <b>왜 필요했나</b> — 딱지를 «60px 넘게 끊기지 않고 어두운 줄» 로 찾으려 했더니
    <b>또 하나도 못 찾았다</b>. 실측해 보니 이유가 분명했다: 딱지 <b>가운데 줄</b>은
    글자가 <b>흰색</b>이라 어두운 구간이 조각조각 끊긴다(y=21 에서 `(68,106)`,
    `(149,181)` 둘뿐이다). 끊기지 않은 줄은 딱지의 <b>위·아래 테두리</b> 두세 줄뿐이고,
    그것만으로는 높이 검사(14~40px)를 통과할 수 없다.

    닫기를 먼저 걸면 글자 틈이 메워져 <b>딱지가 통째로 한 구간</b>이 된다. 그림에는
    이만큼 촘촘한 어두운 덩어리가 가로로 이어지지 않으므로 <b>구별이 유지된다</b>.

    ⚠ 늘린 만큼 줄이므로 <b>상자 폭은 늘어나지 않는다</b> — 그래서 «닫기» 이고
      단순한 «늘리기» 가 아니다. 늘리기만 하면 딱지가 옆 그림과 붙는다.
    """
    n = mask.shape[1]
    grown = mask.copy()
    for s in range(1, k + 1):
        grown[:, s:] |= mask[:, :n - s]
        grown[:, :n - s] |= mask[:, s:]
    out = grown.copy()
    for s in range(1, k + 1):
        out[:, s:] &= grown[:, :n - s]
        out[:, :n - s] &= grown[:, s:]
        # 바깥은 «없음» 으로 본다 — 시트 가장자리에 딱지가 붙지 않으므로 안전하다.
        out[:, :s] = False
        out[:, n - s:] = False
    return out


def find_labels(dark, min_w=None):
    """
    어두운 판때기의 상자 목록.

    ⚠ **가로 띠로 먼저 가르면 안 된다** — 처음에 그렇게 썼다가 <b>딱지를 하나도 못
      찾았다</b>. 그림(갑옷·머리카락)에도 어두운 픽셀이 있어 «어두운 픽셀이 40개 넘는
      줄» 이 <b>거의 모든 줄</b>이고, 그러면 띠 하나가 시트 전체가 되어 높이 검사를
      통과하지 못한다.

    그래서 <b>줄마다 «길게 이어진 어두운 구간» 을 먼저 찾고</b>, 그 구간이 좌우로
    겹치는 줄끼리 위아래로 잇는다. 딱지는 «60px 넘게 <b>끊기지 않고</b> 어두운 줄»
    이 20~30줄 쌓인 것이고, 그림에는 그런 구간이 거의 없다(있어도 두세 줄이라
    높이 검사에서 걸러진다).
    """
    if min_w is None:
        min_w = LABEL_W_MIN
    H, W = dark.shape
    # ★ 글자 틈을 먼저 메운다 — 위 close_x 의 긴 주석 참조.
    solid = close_x(dark)
    open_boxes, done = [], []

    for y in range(H):
        segs = [(a, b) for a, b in runs(solid[y], 1) if b - a + 1 >= min_w]

        used = [False] * len(open_boxes)
        for a, b in segs:
            hit = -1
            for i, box in enumerate(open_boxes):
                if used[i]:
                    continue
                # 좌우로 겹치면 같은 딱지로 본다(딱지는 곧게 서 있다).
                if a <= box["x1"] and b >= box["x0"]:
                    hit = i
                    break
            if hit >= 0:
                box = open_boxes[hit]
                box["x0"], box["x1"] = min(box["x0"], a), max(box["x1"], b)
                box["y1"] = y
                used[hit] = True
            else:
                open_boxes.append({"y0": y, "y1": y, "x0": a, "x1": b})
                used.append(True)

        # 이 줄에서 이어지지 않은 상자는 끝난 것이다.
        still = []
        for i, box in enumerate(open_boxes):
            if i < len(used) and used[i]:
                still.append(box)
            else:
                done.append(box)
        open_boxes = still
    done.extend(open_boxes)

    out = []
    for box in done:
        h, w = box["y1"] - box["y0"] + 1, box["x1"] - box["x0"] + 1
        if not (LABEL_H[0] <= h <= LABEL_H[1]) or w < min_w:
            continue
        # 속이 꽉 찬 판때기인가 — 상자 안 어두운 비율. 그림은 이 값이 낮다.
        fill = solid[box["y0"]:box["y1"] + 1, box["x0"]:box["x1"] + 1].mean()
        if fill < 0.80:
            continue
        out.append((box["y0"], box["y1"], box["x0"], box["x1"], float(fill)))
    out.sort(key=lambda t: (t[0], t[2]))
    return out


#: 딱지 안쪽 색의 «고르기» 한계. 이 값보다 얼룩지면 딱지가 아니다.
FLAT_STD_MAX = 26.0


def flat_enough(path_rgb, box):
    """
    ★★ <b>딱지는 «한 색»이다</b> — 그것이 그림과 가르는 마지막 열쇠다 (2026-08-20).

    <b>왜 필요했나</b> — 높이·폭·«꽉 찬 정도» 만으로 걸렀더니 불칸 시트에서 <b>52개</b>가
    나왔다. 정답은 열다섯쯤이고, 나머지는 <b>갑옷의 검은 부분·불꽃 속 그림자</b> 였다.
    그것들도 어둡고 넓고 꽉 차 있어서 앞의 세 검사를 다 통과한다.

    갈라 주는 것은 <b>색이 고른가</b> 하나다. 딱지는 «거의 같은 검정» 을 칠한 판때기라
    표준편차가 아주 낮고, 갑옷·불꽃은 명암과 색이 흔들려 훨씬 높다. 흰 글자는
    <b>밝은 픽셀을 빼고</b> 재서 피한다(글자를 넣고 재면 딱지의 편차가 커져 버린다).
    """
    y0, y1, x0, x1 = box[:4]
    sub = path_rgb[y0:y1 + 1, x0:x1 + 1].astype(np.float32)
    lum = sub[:, :, 0] * 0.299 + sub[:, :, 1] * 0.587 + sub[:, :, 2] * 0.114
    dark = lum <= DARK_LUM
    if dark.sum() < 40:
        return False, 999.0
    std = float(sub[dark].std())
    return std <= FLAT_STD_MAX, std


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("sheet")
    ap.add_argument("--crop", help="딱지 그림을 이 폴더에 저장한다")
    ap.add_argument("--min-w", type=int, default=None, dest="min_w",
                    help="딱지 최소 폭. 「좌」·「우」 같은 작은 딱지까지 잡으려면 24 쯤")
    args = ap.parse_args()

    path = args.sheet
    if not os.path.isabs(path):
        from vault_path import VAULT
        cand = os.path.join(VAULT, "리소스", "sprites", path)
        if os.path.isfile(cand):
            path = cand

    dark = dark_mask(path)
    rgb = np.asarray(Image.open(path).convert("RGB"))
    sheet = load_sheet(path, box_borders=True)
    mask = sheet["mask"]
    H, W = mask.shape

    raw = find_labels(dark, args.min_w)
    labels, dropped = [], 0
    for box in raw:
        ok, std = flat_enough(rgb, box)
        if ok:
            labels.append(box + (std,))
        else:
            dropped += 1
    print("제목 딱지 %d개  (시트 %dx%d · 얼룩져서 버린 것 %d개)"
          % (len(labels), W, H, dropped))
    print("   #   딱지 y0~y1    x0~x1     채움  고르기  딱지 아래 첫 그림줄")
    for i, (y0, y1, x0, x1, fill, std) in enumerate(labels):
        # 딱지 바로 아래에서 그림이 다시 시작하는 y (같은 x 폭에서 보지 않는다 —
        # 딱지는 구획 왼쪽에 붙고 그림은 그 오른쪽까지 퍼지기 때문이다).
        below = ""
        if y1 + 1 < H:
            prof = mask[y1 + 1:min(H, y1 + 120), :].sum(axis=1)
            nz = np.where(prof > 2)[0]
            if len(nz):
                below = "y%d 부터" % (y1 + 1 + nz[0])
        print("  %2d   %4d~%-4d   %4d~%-4d  %.2f  %5.1f   %s"
              % (i, y0, y1, x0, x1, fill, std, below))

    # ★ 그대로 `char_sheet.Spec.erase` 에 붙일 수 있게 찍어 준다 — 손으로 옮겨 적다
    #   숫자를 틀리는 것이 이 작업에서 가장 흔한 사고다.
    print("\n  erase=[  # 제목 딱지 %d개" % len(labels))
    for y0, y1, x0, x1, _f, _s in labels:
        print("      (%d, %d, %d, %d)," % (y0, y1, x0, x1))
    print("  ],")

    if args.crop:
        os.makedirs(args.crop, exist_ok=True)
        im = Image.open(path).convert("RGBA")
        bg = Image.new("RGBA", im.size, (255, 255, 255, 255))
        bg.alpha_composite(im)
        im = bg.convert("RGB")
        for i, (y0, y1, x0, x1, _f, _s) in enumerate(labels):
            pad = 4
            c = im.crop((max(0, x0 - pad), max(0, y0 - pad),
                         min(W, x1 + pad), min(H, y1 + pad)))
            c = c.resize((c.width * 2, c.height * 2), Image.LANCZOS)
            c.save(os.path.join(args.crop, "label_%02d.png" % i))
        print("딱지 그림 %d장 → %s" % (len(labels), args.crop))


if __name__ == "__main__":
    main()
