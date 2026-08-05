"""
Wall_Outer / Wall_Inner 시트에 3/4 뷰 입체 음영을 다시 입힌다.

원본 진단 (20x20 타일, 8열=방향 x 4행=변형)
  1) South 타일의 밝은 립(윗면-앞면 경계선)이 맨 아래 row19 에 있다 → 기하학적으로 반대.
     카메라를 향한 앞면인데 밑동이 빛나서 벽이 서 있는 느낌이 죽는다.
  2) 앞면이 5px(타일의 25%)뿐이라 수직 벽으로 읽히지 않는다.
  3) Wall_Inner 32개 변형의 평균 밝기 편차가 32단계(53~85)라, 칸마다 랜덤으로 깔리면
     벽 덩어리가 체커보드처럼 얼룩진다. 하나의 평면으로 안 보인다.

전략: "평탄화 후 재음영".
  ① 타일을 윗면 밝기 기준으로 평탄화한다(원본의 밴드 음영과 변형 간 밝기 편차를 제거).
     이때 픽셀 단위 텍스처는 배율만 곱하므로 그대로 남는다.
  ② 노출된 최근접 변 기준으로 역할(top/lip/face/shadow)을 다시 계산해 칠한다.
     최근접 변 판정이라 모서리 타일의 립이 자연스럽게 45도로 만난다.

앞면을 10px(50%)로 키우고 수직 그라데이션을 줘서 벽이 실제로 솟아 보이게 한다.
"""
from PIL import Image
import os, sys

# 원본(백업)에서 읽어 Assets 로 쓴다. 항상 원본을 입력으로 삼으므로
# 몇 번을 다시 돌려도 결과가 같다(멱등). 파라미터를 바꿔 재실행하기 좋다.
SRC_DIR = sys.argv[1] if len(sys.argv) > 1 else "_ArtBackup/OrganicTilemap_20260804"
DST_DIR = sys.argv[2] if len(sys.argv) > 2 else "Assets/_Project/Art/OrganicTilemap/OrganicTilemap"
TILE = 20
COLS, ROWS = 8, 4

DIRECTIONS = ["North", "South", "West", "East",
              "NW_Corner", "NE_Corner", "SW_Corner", "SE_Corner"]
EXPOSED = {
    "North": {"N"}, "South": {"S"}, "West": {"W"}, "East": {"E"},
    "NW_Corner": {"N", "W"}, "NE_Corner": {"N", "E"},
    "SW_Corner": {"S", "W"}, "SE_Corner": {"S", "E"},
}

# 변별 밴드 (그 변까지의 거리 d 기준. d=0 이 가장 바깥 픽셀)
#   face=(lo,hi) 측면, lip=윗면과 만나는 하이라이트 선, shadow=접지 그림자
# South 는 카메라를 향한 정면이라 두껍게 준다 — 여기서 입체감이 나온다.
#
# 립(밝은 하이라이트)은 남쪽에만 준다. 네 변에 다 주면 덩어리가 네온 테두리를 두른
# 평평한 판처럼 보인다 — 실제 3/4 조명에서는 카메라를 향한 남쪽 면만 밝은 경계를
# 만들고, 나머지 변은 그냥 윗면이 끝나는 어두운 리�만 보인다.
BAND = {
    "S": {"shadow": 0, "face": (1, 10), "lip": 11},
    "W": {"shadow": None, "face": (0, 2), "lip": None},
    "E": {"shadow": None, "face": (0, 2), "lip": None},
    "N": {"shadow": None, "face": (0, 1), "lip": None},
}
# 최근접 변이 같을 때의 우선순위 — 정면(S)을 가장 우선한다.
EDGE_ORDER = ["S", "W", "E", "N"]

TOP_TARGET = 86.0        # 모든 윗면을 이 밝기로 통일 (얼룩 제거). 바닥 평균은 61.
LIP_GAIN = 1.5           # 윗면 대비 립 밝기. 너무 높이면 형광 테두리처럼 보인다
LIP_WARM = (1.04, 0.99, 0.98)
MUL_SHADOW = 0.24        # 접지 그림자 — 벽이 바닥에 닿는 선

# 측면 밝기: 빛이 좌상단에서 온다고 가정. (바깥쪽, 안쪽) 로 그라데이션.
FACE_GRAD = {
    "S": (0.38, 0.72),   # 아래로 갈수록 어둡게 — 벽 높이가 느껴진다
    "W": (0.74, 0.88),   # 빛을 받는 쪽이라 약하게만
    "E": (0.56, 0.70),
    "N": (0.58, 0.66),   # 먼 쪽 — 윗면이 끝나는 어두운 리�만
}


def clamp(v):
    return 0 if v < 0 else (255 if v > 255 else int(v))


def lum(c):
    return (c[0] * 299 + c[1] * 587 + c[2] * 114) / 1000.0


# 원본 아트가 실제로 쓰고 있는 밴드 구조. 평탄화 단계에서 이걸 기준으로
# "어두운 측면"과 "밝은 립 선"을 각각 윗면 밝기로 되돌린다.
# 이 단계를 빼면 원본의 밝은 립 선이 영역 평균 안에서 비율만 유지된 채 그대로 남아,
# 결과물에 네온 테두리가 계속 보인다(실제로 그렇게 실패했다).
SRC_BAND = {
    "N": {"face": (0, 3), "lip": 4},
    "S": {"face": (1, 5), "lip": 0},    # row19 가 원본 립, rows14-18 이 측면
    "W": {"face": (0, 2), "lip": 3},
    "E": {"face": (0, 2), "lip": 3},
}


def nearest_edge(x, y, exposed):
    best = None
    for e in EDGE_ORDER:
        if e not in exposed:
            continue
        d = {"N": y, "S": TILE - 1 - y, "W": x, "E": TILE - 1 - x}[e]
        if best is None or d < best[1]:
            best = (e, d)
    return best


def src_key(x, y, exposed):
    """
    원본 아트 기준 영역 키. 평탄화 배율을 이 단위로 계산한다.

    밴드는 <b>줄 단위</b>로 나눈다(변까지의 거리 d 별로 따로). 밴드 전체를 한 영역으로
    묶어 평균으로 나누면 밴드 안의 밝기 기울기가 그대로 남아, 평탄화한 뒤에도
    밝은 줄이 생긴다 — North 밴드가 24→39 기울기라 평균으로 나누니 rows2-3 이
    91/98 로 튀어 상단에 밝은 테두리가 남았다(실제로 그렇게 실패했다).
    줄마다 정규화하면 그 줄이 정확히 목표 밝기로 떨어진다.
    """
    best = nearest_edge(x, y, exposed)
    if best is None:
        return "top"
    edge, d = best
    b = SRC_BAND[edge]
    lo, hi = b["face"]
    if d == b["lip"] or lo <= d <= hi:
        return f"srcline:{edge}:{d}"
    return "top"


def role_of(x, y, exposed):
    """새 기하 기준 픽셀 역할과 노출 변까지의 거리."""
    best = nearest_edge(x, y, exposed)
    if best is None:
        return ("top", None, 0)

    edge, d = best
    b = BAND[edge]
    if b["shadow"] is not None and d == b["shadow"]:
        return ("shadow", edge, d)
    lo, hi = b["face"]
    if lo <= d <= hi:
        return ("face", edge, d)
    if b["lip"] is not None and d == b["lip"]:
        return ("lip", edge, d)
    return ("top", edge, d)


def process_tile(im, ox, oy, exposed):
    px = im.load()

    # ① 원본 영역별 평균을 재서, 모든 영역을 윗면 목표 밝기로 되돌리는 배율을 만든다.
    #    (어두운 측면 밴드 + 밝은 립 선을 모두 중립화 → 균일한 평면이 된다)
    src_keys, region_lums = {}, {}
    for y in range(TILE):
        for x in range(TILE):
            k = src_key(x, y, exposed)
            src_keys[(x, y)] = k
            c = px[ox + x, oy + y]
            if c[3] > 40:
                region_lums.setdefault(k, []).append(lum(c))

    if "top" not in region_lums:
        return

    flatten = {}
    for key, vals in region_lums.items():
        m = sum(vals) / len(vals)
        flatten[key] = (TOP_TARGET / m) if m > 1.0 else 1.0

    # 립 색은 평탄화된 윗면색 기준으로 만든다.
    tr = tg = tb = 0.0
    n = 0
    tmul = flatten["top"]
    for y in range(TILE):
        for x in range(TILE):
            if src_keys[(x, y)] != "top":
                continue
            c = px[ox + x, oy + y]
            if c[3] <= 40:
                continue
            tr += c[0] * tmul; tg += c[1] * tmul; tb += c[2] * tmul; n += 1
    lip_rgb = None
    if n:
        lip_rgb = [(tr / n) * LIP_GAIN * LIP_WARM[0],
                   (tg / n) * LIP_GAIN * LIP_WARM[1],
                   (tb / n) * LIP_GAIN * LIP_WARM[2]]

    # ② 새 기하 기준으로 역할별 재음영
    for y in range(TILE):
        for x in range(TILE):
            c = px[ox + x, oy + y]
            if c[3] == 0:
                continue
            kind, edge, d = role_of(x, y, exposed)
            base = flatten.get(src_keys[(x, y)], 1.0)

            if kind == "top":
                m = base
            elif kind == "face":
                lo, hi = BAND[edge]["face"]
                t = (d - lo) / max(1, hi - lo)          # 0=바깥, 1=안쪽
                g0, g1 = FACE_GRAD[edge]
                m = base * (g0 + (g1 - g0) * t)
            elif kind == "shadow":
                m = base * MUL_SHADOW
            else:  # lip
                if lip_rgb is None:
                    m = base * LIP_GAIN
                else:
                    dev = (lum(c) * base - TOP_TARGET) * 0.3
                    px[ox + x, oy + y] = (clamp(lip_rgb[0] + dev),
                                          clamp(lip_rgb[1] + dev),
                                          clamp(lip_rgb[2] + dev), c[3])
                    continue
            px[ox + x, oy + y] = (clamp(c[0] * m), clamp(c[1] * m),
                                  clamp(c[2] * m), c[3])


def process_outer(src, dst):
    im = Image.open(src).convert("RGBA")
    for cx in range(COLS):
        exposed = EXPOSED[DIRECTIONS[cx]]
        for ry in range(ROWS):
            process_tile(im, cx * TILE, ry * TILE, exposed)
    im.save(dst)
    print(f"  {os.path.basename(dst)}: 방향별 {COLS*ROWS}개 타일 재음영 (앞면 10px)")


def process_inner(src, dst):
    """
    내부 채움은 측면이 없다. 32개 변형의 밝기를 같은 목표로 정규화해서
    벽 덩어리가 얼룩지지 않게 하고, 아주 약한 좌상단 라이팅만 남긴다.
    """
    im = Image.open(src).convert("RGBA")
    px = im.load()
    before, after = [], []
    for ty in range(ROWS):
        for tx in range(COLS):
            ox, oy = tx * TILE, ty * TILE
            vals = []
            for y in range(TILE):
                for x in range(TILE):
                    c = px[ox + x, oy + y]
                    if c[3] > 40:
                        vals.append(lum(c))
            if not vals:
                continue
            m = sum(vals) / len(vals)
            before.append(m)
            norm = TOP_TARGET / m if m > 1.0 else 1.0
            for y in range(TILE):
                for x in range(TILE):
                    c = px[ox + x, oy + y]
                    if c[3] == 0:
                        continue
                    t = (x + y) / (2.0 * (TILE - 1))
                    mul = norm * (1.04 - 0.08 * t)
                    px[ox + x, oy + y] = (clamp(c[0] * mul), clamp(c[1] * mul),
                                          clamp(c[2] * mul), c[3])
            vals2 = []
            for y in range(TILE):
                for x in range(TILE):
                    c = px[ox + x, oy + y]
                    if c[3] > 40:
                        vals2.append(lum(c))
            after.append(sum(vals2) / len(vals2))
    im.save(dst)
    print(f"  {os.path.basename(dst)}: 변형 {len(before)}개 밝기 정규화 "
          f"(편차 {max(before)-min(before):.0f} → {max(after)-min(after):.0f})")


if __name__ == "__main__":
    for name, fn in (("Wall_Outer_20px.png", process_outer),
                     ("Wall_Inner_20px.png", process_inner)):
        fn(os.path.join(SRC_DIR, name), os.path.join(DST_DIR, name))
    print(f"done: {SRC_DIR} -> {DST_DIR}")
