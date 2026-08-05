"""
Wall_Outer 타일을 20x20 -> 20x40(2칸 높이)로 새로 그려 예전 16x32 벽 수준의 입체감을 낸다.

원리 (예전 프로젝트가 실제로 썼던 방식 — 진행상황.md 1절 "Individual 모드 + TopLeft 정렬"):
스프라이트를 셀보다 2배 높게 만들고 피벗을 조정하면, 지붕은 자기 칸을 덮고 정면 절반이
**아래(남쪽, 카메라 쪽)로 늘어져 다음 칸 위에 겹쳐** 그려진다. 그 정면이 벽의 높이가 된다.

한 타일 구성 (위->아래):
  rows  0-19  지붕(roof)  — 깨끗한 재질. 노출된 북/서/동 변에는 얇은 어두운 리브
  row     20  립(lip)     — 지붕과 정면이 만나는 밝은 하이라이트 (유저가 말한 "아우터 라인")
  rows 21-37  정면(face)  — 같은 재질을 세로로 반복, 위(밝음)->아래(어두움) 그라데이션
  rows 38-39  접지 그림자  — 거의 검정

⚠️ 재질은 Wall_Inner 에서 가져온다 (Wall_Outer 자신의 픽셀을 쓰지 않는다)
   Wall_Outer 원본의 방향별 밴드에는 세로 빗금(말뚝) 패턴이 들어 있다. 예:
       row14  ':    : :  : :  :    '
       row15  ':....:.:..:.:..:....'   <- 이 빗금
   이걸 재질로 쓰면 지붕과 정면에 빗금이 두 번 반복돼 나와 이질적으로 보인다(유저 지적).
   Wall_Inner_Fill 은 빗금 없는 유기적 텍스처라 재질로 쓰기에 적합하고, 내부 채움 칸과
   재질이 같아져 벽 덩어리가 하나의 표면으로 읽히는 이점도 있다.

⚠️ 피벗 계산 (틀리면 벽이 반 칸 밀린다)
   Tilemap 의 tileAnchor 가 (0.5, 0.5) 이므로 스프라이트 피벗이 칸 중심에 놓인다.
   칸 (x,y) 는 월드 [x,x+1]x[y,y+1]. 스프라이트 20x40 @PPU20 = 1x2 유닛.
   지붕이 자기 칸, 정면이 남쪽으로 늘어지게 하려면 스프라이트가 [x,x+1]x[y-1,y+1] 를 덮어야 한다.
     bottom-left = (x, y-1),  피벗 = bottom-left + (px*1, py*2) = (x+0.5, y+0.5)
     -> px = 0.5,  2*py = 1.5  ->  py = 0.75
   따라서 alignment 9(Custom) + pivot (0.5, 0.75). TopLeft(0,1) 로 두면 안 된다.

Wall_Inner(내부 채움)는 20x20 그대로 둔다 — 사방이 벽인 칸은 정면이 안 보인다.
"""
from PIL import Image
import os, re, sys

TILE = 20
FACE_H = TILE                 # 정면 높이 = 지붕과 같은 20px (총 40px)
COLS, ROWS = 8, 4
DIRECTIONS = ["North", "South", "West", "East",
              "NW_Corner", "NE_Corner", "SW_Corner", "SE_Corner"]
EXPOSED = {
    "North": {"N"}, "South": {"S"}, "West": {"W"}, "East": {"E"},
    "NW_Corner": {"N", "W"}, "NE_Corner": {"N", "E"},
    "SW_Corner": {"S", "W"}, "SE_Corner": {"S", "E"},
}

TOP_TARGET = 86.0             # 지붕 목표 밝기 (바닥 평균은 61 -> 충분히 구분된다)

# 정면 그라데이션. 빛은 좌상단에서 온다고 가정.
LIP_GAIN = 1.55               # 립 밝기 (지붕 대비)
LIP_WARM = (1.04, 0.99, 0.97)
FACE_TOP = 0.76               # 립 바로 아래
FACE_BOT = 0.40               # 접지 직전
SHADOW_MUL = 0.18
SHADOW_ROWS = 2

# 노출된 변의 얇은 리브 / 측면 어둡게
RIB_N = (2, 0.64)             # (두께 px, 배율)
RIB_W = (2, 0.80)
RIB_E = (2, 0.68)
FACE_SIDE_W = 0.88
FACE_SIDE_E = 0.80


def clamp(v):
    return 0 if v < 0 else (255 if v > 255 else int(v))


def lum(c):
    return (c[0] * 299 + c[1] * 587 + c[2] * 114) / 1000.0


def material(inner_img, col, row_from_bottom):
    """
    Wall_Inner 의 한 변형을 잘라 밝기를 TOP_TARGET 으로 정규화한 '깨끗한 재질'을 만든다.
    빗금이 없는 유기적 텍스처라 지붕/정면 양쪽에 그대로 쓸 수 있다.
    """
    ox = col * TILE
    oy = (ROWS - 1 - row_from_bottom) * TILE       # PIL 은 top-left 원점
    tile = inner_img.crop((ox, oy, ox + TILE, oy + TILE)).convert("RGBA")

    sp = tile.load()
    vals = [lum(sp[x, y]) for y in range(TILE) for x in range(TILE) if sp[x, y][3] > 40]
    mean = sum(vals) / len(vals) if vals else TOP_TARGET
    norm = TOP_TARGET / mean if mean > 1.0 else 1.0

    out = Image.new("RGBA", (TILE, TILE), (0, 0, 0, 0))
    op = out.load()
    for y in range(TILE):
        for x in range(TILE):
            c = sp[x, y]
            if c[3] == 0:
                continue
            op[x, y] = (clamp(c[0] * norm), clamp(c[1] * norm), clamp(c[2] * norm), c[3])
    return out


def build_tile(mat, direction):
    exposed = EXPOSED[direction]
    out = Image.new("RGBA", (TILE, TILE + FACE_H), (0, 0, 0, 0))
    mp = mat.load()
    op = out.load()

    # 립 색 — 재질 평균 기준으로 만든다.
    tot = [0.0, 0.0, 0.0]
    n = 0
    for y in range(TILE):
        for x in range(TILE):
            c = mp[x, y]
            if c[3] > 40:
                tot[0] += c[0]; tot[1] += c[1]; tot[2] += c[2]; n += 1
    lip_rgb = [(tot[i] / max(1, n)) * LIP_GAIN * LIP_WARM[i] for i in range(3)]

    # ① 지붕 — 깨끗한 재질 + 노출 변의 얇은 리브
    for y in range(TILE):
        for x in range(TILE):
            c = mp[x, y]
            if c[3] == 0:
                continue
            m = 1.0
            if "N" in exposed and y < RIB_N[0]:
                m *= RIB_N[1]
            if "W" in exposed and x < RIB_W[0]:
                m *= RIB_W[1]
            if "E" in exposed and x >= TILE - RIB_E[0]:
                m *= RIB_E[1]
            op[x, y] = (clamp(c[0] * m), clamp(c[1] * m), clamp(c[2] * m), c[3])

    # ② 립 — 지붕과 정면의 경계선
    for x in range(TILE):
        c = mp[x, TILE - 1]
        if c[3] == 0:
            continue
        m = 1.0
        if "W" in exposed and x < RIB_W[0]:
            m *= 0.85
        if "E" in exposed and x >= TILE - RIB_E[0]:
            m *= 0.80
        op[x, TILE] = (clamp(lip_rgb[0] * m), clamp(lip_rgb[1] * m),
                       clamp(lip_rgb[2] * m), c[3])

    # ③ 정면 + 접지 그림자
    for fy in range(1, FACE_H):          # 립이 fy=0 을 차지
        if fy >= FACE_H - SHADOW_ROWS:
            mul = SHADOW_MUL
        else:
            span = max(1, FACE_H - SHADOW_ROWS - 2)
            t = min(1.0, max(0.0, (fy - 1) / span))
            mul = FACE_TOP + (FACE_BOT - FACE_TOP) * t

        src_y = (fy - 1) % TILE          # 재질을 세로로 반복
        for x in range(TILE):
            c = mp[x, src_y]
            if c[3] == 0:
                continue
            m = mul
            if "W" in exposed and x < RIB_W[0]:
                m *= FACE_SIDE_W
            if "E" in exposed and x >= TILE - RIB_E[0]:
                m *= FACE_SIDE_E
            op[x, TILE + fy] = (clamp(c[0] * m), clamp(c[1] * m), clamp(c[2] * m), c[3])

    return out


def process(inner_src, outer_dst):
    inner = Image.open(inner_src).convert("RGBA")
    dst = Image.new("RGBA", (COLS * TILE, ROWS * (TILE + FACE_H)), (0, 0, 0, 0))

    for cx in range(COLS):
        direction = DIRECTIONS[cx]
        for ry in range(ROWS):
            # 방향/변형마다 다른 Inner 변형을 재질로 써서 단조로움을 피한다.
            mat = material(inner, col=cx, row_from_bottom=ry)
            tile = build_tile(mat, direction)
            dx = cx * TILE
            dy = (ROWS - 1 - ry) * (TILE + FACE_H)
            dst.paste(tile, (dx, dy))

    dst.save(outer_dst)
    print(f"  {os.path.basename(outer_dst)}: {COLS*ROWS} tiles -> {dst.size} "
          f"(cell {TILE}x{TILE+FACE_H}), material from Wall_Inner (no hatch)")


def rewrite_meta(meta_path, new_tile_h):
    """
    스프라이트 rect(height/y) 와 pivot 을 갱신한다.

    · internalID/spriteID/name 은 절대 건드리지 않는다 -> 이미 만들어진 32개 Tile
      에셋의 m_Sprite 참조가 유지된다(재생성 불필요).
    · 행 인덱스를 meta 에 적힌 '현재' height 로 나눠 구하므로 여러 번 실행해도 안전하다.
    · pivot 은 alignment 9(Custom) + (0.5, 0.75). 파일 상단 주석의 계산 참조.
    """
    text = open(meta_path, encoding="utf-8").read()

    pattern = re.compile(
        r'(name: (?P<name>\S+)\n'
        r'      rect:\n'
        r'        serializedVersion: 2\n'
        r'        x: (?P<x>\d+)\n'
        r'        y: (?P<y>\d+)\n'
        r'        width: (?P<width>\d+)\n'
        r'        height: (?P<height>\d+)\n'
        r'      alignment: \d+\n'
        r'      pivot: \{x: [-\d.]+, y: [-\d.]+\})'
    )

    count = 0

    def repl(m):
        nonlocal count
        cur_h = int(m.group("height"))
        row = int(m.group("y")) // max(1, cur_h)      # 현재 height 기준 -> 멱등
        count += 1
        return (f'name: {m.group("name")}\n'
                f'      rect:\n'
                f'        serializedVersion: 2\n'
                f'        x: {m.group("x")}\n'
                f'        y: {row * new_tile_h}\n'
                f'        width: {m.group("width")}\n'
                f'        height: {new_tile_h}\n'
                f'      alignment: 9\n'
                f'      pivot: {{x: 0.5, y: 0.75}}')

    open(meta_path, "w", encoding="utf-8").write(pattern.sub(repl, text))
    print(f"  {os.path.basename(meta_path)}: {count} sprites -> "
          f"height {new_tile_h}, alignment 9, pivot (0.5, 0.75)")
    return count


if __name__ == "__main__":
    src_dir = sys.argv[1] if len(sys.argv) > 1 else "_ArtBackup/OrganicTilemap_20260804"
    dst_dir = sys.argv[2] if len(sys.argv) > 2 else "Assets/_Project/Art/OrganicTilemap/OrganicTilemap"

    inner_src = os.path.join(src_dir, "Wall_Inner_20px.png")
    outer_dst = os.path.join(dst_dir, "Wall_Outer_20px.png")

    process(inner_src, outer_dst)
    n = rewrite_meta(outer_dst + ".meta", TILE + FACE_H)
    if n != COLS * ROWS:
        print(f"  !! WARNING: expected {COLS*ROWS} sprites, updated {n}")
    print("done")
