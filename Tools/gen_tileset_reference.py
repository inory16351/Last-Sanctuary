# -*- coding: utf-8 -*-
"""청크 타일셋 참조 시트 생성 (2026-08-18).

유저 지시: *"앞으로 청크 생성 할때 이미지 생성 모델한테 요청하려고 하는데 너가 지금 쓰고
있는 바닥 타일이랑 데코 세트 이미지로 만들어서 전달해줘"*

무엇을 만드나
-------------
지금 게임이 실제로 쓰고 있는 시트를 **이미지 생성 모델이 읽을 수 있는 크기·설명과 함께**
다시 그린다. 원본은 20px 타일이라 그대로 보내면 모델이 격자를 못 읽는다.

  · ``참조_01_바닥타일.png``   — OrganicTerrain (8x4 = 32칸, 바닥 20 + 갈라진 바닥 12)
  · ``참조_02_데코세트.png``   — OrganicProps  (8x4 = 32칸, 8계열 x 4변형)
  · ``참조_03_벽과경계.png``   — Wall_Outer / Wall_Inner / Transitions (문맥용)
  · ``참조_04_빈격자_템플릿.png`` — 새 시트를 그릴 때 쓸 빈 격자(치수만 표시)

★ 왜 스크립트인가 — 시트가 갱신되면 참조 이미지도 같이 갱신돼야 한다. 손으로 만든
  이미지는 그 순간 낡는다. **항상 Assets 의 현재 시트를 읽어 다시 그린다(멱등).**

사용법:  python Tools/gen_tileset_reference.py
결과:   <프로젝트>/Docs/타일셋_참조/  (+ 같은 폴더에 사양서 마크다운)
"""

import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(PROJECT, "Assets", "_Project", "Art", "OrganicTilemap", "OrganicTilemap")
OUT = os.path.join(PROJECT, "Docs", "타일셋_참조")

#: 원본 타일 한 변(px). 카탈로그의 tileSizePx 와 같아야 한다 — 아래에서 검증한다.
TILE = 20

#: 참조 이미지에서 타일 하나를 몇 배로 키울지. 이미지 생성 모델이 픽셀을 셀 수 있는 크기.
ZOOM = 10

MARGIN = 28          # 격자 바깥 여백
LABEL_H = 50         # 칸 아래 라벨 두 줄이 들어갈 높이 (두 줄이라 넉넉해야 한다)
TITLE_H = 96         # 상단 제목 영역

BG = (24, 20, 26)
GRID = (86, 74, 88)
TEXT = (238, 232, 238)
DIM = (168, 158, 170)
ACCENT = (232, 132, 120)


def font(size, bold=False):
    """한글이 들어가므로 맑은 고딕을 쓴다. 없으면 기본 폰트(영문만)로 떨어진다."""
    for name in (("malgunbd.ttf", "malgun.ttf") if bold else ("malgun.ttf",)):
        path = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts", name)
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


F_TITLE = font(34, True)
F_SUB = font(19)
F_CELL = font(17, True)
F_TINY = font(15)


def load_catalog():
    with open(os.path.join(SRC, "TileMapCatalog.json"), encoding="utf-8") as f:
        cat = json.load(f)
    if int(cat.get("tileSizePx", TILE)) != TILE:
        raise SystemExit("카탈로그의 tileSizePx 가 %s 라 이 스크립트의 TILE(%d) 과 다릅니다."
                         % (cat.get("tileSizePx"), TILE))
    return {s["sheet"]: s["tiles"] for s in cat["sheets"]}


def sheet_grid(sheet_name):
    """시트 PNG 를 열고 (이미지, 열, 행) 을 돌려준다. 행은 <b>위에서부터</b> 센다."""
    im = Image.open(os.path.join(SRC, sheet_name)).convert("RGBA")
    return im, im.width // TILE, im.height // TILE


def crop_cell(im, cx, cy, cell_h=TILE):
    """카탈로그의 cell=[x, y] 는 <b>위에서부터</b>의 행 번호다(실측 확인)."""
    return im.crop((cx * TILE, cy * cell_h, (cx + 1) * TILE, (cy + 1) * cell_h))


def draw_sheet(sheet_name, title, subtitle, labeler, out_name, cell_h=TILE):
    """
    시트 하나를 확대 + 격자 + 칸별 라벨로 그린다.

    labeler(cx, cy) -> (윗줄, 아랫줄) 문자열. 없으면 ('', '').
    """
    im, cols, rows = sheet_grid(sheet_name)
    rows = im.height // cell_h

    cw = TILE * ZOOM
    ch = cell_h * ZOOM
    W = MARGIN * 2 + cols * cw
    H = TITLE_H + MARGIN + rows * (ch + LABEL_H) + MARGIN

    canvas = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(canvas)

    d.text((MARGIN, 24), title, font=F_TITLE, fill=TEXT)
    d.text((MARGIN, 64), subtitle, font=F_SUB, fill=DIM)

    for ry in range(rows):
        for cx in range(cols):
            tile = crop_cell(im, cx, ry, cell_h).resize((cw, ch), Image.NEAREST)

            x = MARGIN + cx * cw
            y = TITLE_H + MARGIN + ry * (ch + LABEL_H)

            # 투명 타일이 있으므로 바닥을 깔고 알파 합성한다.
            canvas.paste(Image.new("RGB", (cw, ch), (14, 12, 16)), (x, y))
            canvas.paste(tile, (x, y), tile)
            d.rectangle([x, y, x + cw - 1, y + ch - 1], outline=GRID, width=2)

            top, bottom = labeler(cx, ry)
            if top:
                d.text((x + 6, y + ch + 6), top, font=F_CELL, fill=TEXT)
            if bottom:
                d.text((x + 6, y + ch + 27), bottom, font=F_TINY, fill=DIM)

    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, out_name)
    canvas.save(path)
    print("  %s  (%dx%d · %d칸)" % (out_name, W, H, cols * rows))
    return path


def by_cell(tiles, cell_h_rows=1):
    """카탈로그 타일 목록 → {(cx, cy): 타일정보}."""
    return {(t["cell"][0], t["cell"][1]): t for t in tiles}


def main():
    catalog = load_catalog()
    os.makedirs(OUT, exist_ok=True)
    print("[타일셋 참조 시트]")

    # ── 1. 바닥 ────────────────────────────────────────────────────────
    terrain = by_cell(catalog["OrganicTerrain_20px.png"])

    def label_terrain(cx, cy):
        t = terrain.get((cx, cy))
        if not t:
            return ("", "")
        kind = "갈라진 바닥" if t["category"] == "ground_cracked" else "바닥"
        return ("%s" % t["id"].replace("terrain_", "#"), "%s · w%d" % (kind, t["weight"]))

    draw_sheet("OrganicTerrain_20px.png",
               "① 바닥 타일 세트 — OrganicTerrain (현재 사용 중)",
               "160x80px · 20px 타일 8열 x 4행 = 32칸 · 바닥 20칸 + 갈라진 바닥 12칸 · "
               "w = 추첨 가중치(클수록 자주 깔린다)",
               label_terrain, "참조_01_바닥타일.png")

    # ── 2. 데코(프롭) ──────────────────────────────────────────────────
    props = by_cell(catalog["OrganicProps_20px.png"])

    KOR = {
        "bone": "뼈", "egg_sac": "알집", "fungus": "균사", "pit": "구덩이",
        "root": "뿌리", "spike": "가시", "tentacle": "촉수", "tubular_growth": "관형 증식",
    }

    # ⚠ 계열은 <b>열</b>이고 변형은 <b>행</b>이다 — 실제 시트를 그려 보고 확인했다.
    #   (한 열을 위에서 아래로 훑으면 같은 계열의 변형 4개가 나온다)
    def label_props(cx, cy):
        t = props.get((cx, cy))
        if not t:
            return ("", "")
        cat = t["category"]
        return (KOR.get(cat, cat), "%s · 변형 %d" % (cat, cy + 1))

    draw_sheet("OrganicProps_20px.png",
               "② 데코(프롭) 세트 — OrganicProps (현재 사용 중)",
               "160x80px · 20px 타일 8열 x 4행 = 32칸 · <열> 8계열 x <행> 4변형 · "
               "배경 투명 · 바닥 타일 위에 겹쳐 그린다(overlay_on_ground)",
               label_props, "참조_02_데코세트.png")

    # ── 3. 벽·경계 (문맥용) ───────────────────────────────────────────
    #    새 바닥/데코를 그릴 때 톤을 맞출 대상이라 같이 넣는다.
    outer = by_cell(catalog["OrganicTilemap.png"]) if False else {}
    wall_rules = {t["cell"][0] + t["cell"][1] * 8: t.get("rule", "")
                  for t in catalog["Wall_Outer_20px.png"]}

    def label_wall(cx, cy):
        rule = wall_rules.get(cx + cy * 8, "")
        rule = rule.replace("solid_collider; ", "").replace("exposed_", "")
        return (rule, "")

    # Wall_Outer 는 20x40(2칸 높이)이다 — 셀 높이를 그렇게 넘긴다.
    draw_sheet("Wall_Outer_20px.png",
               "③-A 벽(노출면) — Wall_Outer · ★ 한 칸이 20x40 (2타일 높이)",
               "160x160px · 20x40px 타일 8열 x 4행 = 32칸 · 피벗 (0.5, 0.75) · "
               "위 절반=윗면, 아래 절반=카메라를 향한 정면(아래 칸을 통째로 덮는다)",
               label_wall, "참조_03A_벽_노출면.png", cell_h=TILE * 2)

    draw_sheet("Wall_Inner_20px.png",
               "③-B 벽(내부 채움) — Wall_Inner · 20x20",
               "160x80px · 사방이 벽인 칸에만 깔린다(정면이 안 보이므로 1타일 높이)",
               lambda cx, cy: ("", ""), "참조_03B_벽_내부채움.png")

    trans = by_cell(catalog["OrganicTransitions_20px.png"])

    def label_trans(cx, cy):
        t = trans.get((cx, cy))
        if not t:
            return ("", "")
        fam = "피 웅덩이" if t["category"] == "blood_edge" else "균열"
        return (fam, t.get("rule", ""))

    draw_sheet("OrganicTransitions_20px.png",
               "③-C 경계 장식 — OrganicTransitions",
               "160x80px · 바닥이 벽과 닿는 칸에 얹는다 · 2계열(피 웅덩이/균열) x 8방향 x 2변형",
               label_trans, "참조_03C_경계장식.png")

    # ── 4. 빈 격자 템플릿 ─────────────────────────────────────────────
    draw_blank_template()

    write_spec()
    print("\n완료 — %s" % OUT)


def draw_blank_template():
    """새 시트를 그릴 때 지켜야 할 격자만 그린 빈 판. 모델에 '이 규격으로' 라고 줄 때 쓴다."""
    cols, rows = 8, 4
    cw = ch = TILE * ZOOM
    W = MARGIN * 2 + cols * cw
    H = TITLE_H + MARGIN + rows * (ch + LABEL_H) + MARGIN

    canvas = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(canvas)
    d.text((MARGIN, 24), "④ 새 시트 규격 — 이 격자를 그대로 지킬 것", font=F_TITLE, fill=TEXT)
    d.text((MARGIN, 64),
           "출력 크기 160x80px · 한 칸 20x20px · 8열 x 4행 = 32칸 · 칸 사이 여백 0 · "
           "격자선을 그리지 말 것(칸 경계는 픽셀 좌표로만 존재)",
           font=F_SUB, fill=DIM)

    for ry in range(rows):
        for cx in range(cols):
            x = MARGIN + cx * cw
            y = TITLE_H + MARGIN + ry * (ch + LABEL_H)
            d.rectangle([x, y, x + cw - 1, y + ch - 1], outline=GRID, width=2)
            d.text((x + 6, y + ch + 4), "칸 %d" % (ry * cols + cx), font=F_CELL, fill=DIM)
            d.text((x + 6, y + 6), "20", font=F_TINY, fill=(70, 62, 72))

    d.text((MARGIN, H - MARGIN + 2),
           "※ 원본 좌표: 왼쪽 위가 (0,0), 오른쪽으로 x, 아래로 y",
           font=F_TINY, fill=ACCENT)

    path = os.path.join(OUT, "참조_04_빈격자_템플릿.png")
    canvas.save(path)
    print("  참조_04_빈격자_템플릿.png  (%dx%d)" % (W, H))


SPEC = """# 청크 타일셋 — 이미지 생성 모델 전달용 사양서

> 이 파일과 같은 폴더의 `참조_*.png` 를 함께 첨부할 것.
> 생성 스크립트: `Tools/gen_tileset_reference.py` (시트가 바뀌면 다시 돌리면 된다)

## 0. 이 게임이 타일을 쓰는 방식

`MapGenerator` 가 맵을 **청크(20x20 타일)** 단위로 나누고, 청크마다 아래 풀에서
타일을 **가중 랜덤**으로 뽑아 깐다. 그래서 한 세트 안의 타일들은
**서로 아무 순서로 이어 붙여도 어색하지 않아야 한다** — 이것이 가장 중요한 제약이다.

타일맵은 3층이다:

| 층 | 시트 | 역할 |
|---|---|---|
| Ground | `OrganicTerrain_20px.png` | 바닥. 빈칸 없이 전부 깔린다 |
| Deco | `OrganicProps_20px.png` · `OrganicTransitions_20px.png` | 바닥 위 장식. **한 칸에 하나만** |
| Obstacle | `Wall_Outer_20px.png` · `Wall_Inner_20px.png` | 벽(이동 불가) |

## 1. 공통 규격 (절대 어기면 안 되는 것)

- **타일 한 칸 = 20 x 20 px** (벽 노출면만 20 x 40 — 3절 참조)
- **시트 = 8열 x 4행 = 32칸**, 즉 **160 x 80 px** (벽 노출면 시트는 160 x 160)
- 칸 사이 **여백·격자선 없음**. 칸 경계는 픽셀 좌표로만 존재한다
- **픽셀 아트**. 안티에일리어싱된 흐릿한 가장자리 금지, 픽셀당 1색
- 좌표계: **왼쪽 위가 (0,0)**, 오른쪽으로 x, 아래로 y
- Pixels Per Unit = 20 (타일 한 칸 = 게임 1타일 = 캐릭터 몸통 폭 정도)

## 2. 바닥 타일 세트 (`참조_01_바닥타일.png`)

- 32칸 = **일반 바닥 20칸 + 갈라진 바닥 12칸**
- **경계가 없어야 한다.** 어느 타일 옆에 어느 타일이 와도 이음매가 보이면 안 된다
  → 타일 가장자리에 테두리·비네트·방향성 있는 큰 무늬를 넣지 말 것
- 무늬는 **타일 안쪽에서 끝나는 작은 반점·결·얼룩** 수준으로
- 밝기 편차를 작게 유지할 것. 칸마다 밝기가 크게 다르면 맵이 **체커보드처럼 얼룩진다**
  (실제로 벽에서 그 문제가 났었다 — 평균 밝기 편차 32단계를 8단계 이하로 줄여 해결)
- 현재 톤: 어두운 적자색 살점 계열. 대표색 `#2B161E` `#5F2926` `#9A4744`

## 3. 데코(프롭) 세트 (`참조_02_데코세트.png`)

- 32칸 = **8계열(열) x 4변형(행)** — 열 순서: 뿌리 / 뼈 / 알집 / 관형 증식 / 가시 / 촉수 / 균사 / 구덩이
- **배경 완전 투명(알파 0).** 바닥 타일 위에 겹쳐 그린다
- 그림은 **칸 안에서 끝나야** 한다 — 옆 칸으로 삐져나가면 안 된다 (한 칸에 하나만 놓이므로
  이웃 칸과 이어지는 그림을 그릴 수 없다)
- 바닥보다 **명도 대비가 커야** 눈에 띈다. 현재는 자홍 계열 `#7D263C` `#A5465B`,
  뼈 계열만 크림색 `#D5AA83`
- 한 칸을 꽉 채우지 말 것 — 대략 **가운데 60~80%** 만 쓰고 여백을 남긴다

## 4. 벽 (`참조_03A/03B`) — 새로 그릴 때만 읽으면 되는 부분

- `Wall_Inner`(사방이 벽인 칸): **20x20**, 윗면만 보인다
- `Wall_Outer`(노출면 8방향 x 4변형): **20x40 (2타일 높이)** · 피벗 `(0.5, 0.75)`
  - 위 20px = 윗면, 아래 20px = **카메라를 향한 정면**
  - 정면은 **아래 칸을 통째로 덮는다** → 그 칸은 게임에서 이동 불가로 처리된다
  - 맨 아랫줄은 접지 그림자라 가장 어둡게, 윗면과 정면 경계에는 밝은 립 한 줄

## 5. 경계 장식 (`참조_03C`)

- 2계열(피 웅덩이 / 균열) x **8방향**(N/S/W/E + 네 모서리) x 2변형
- 바닥칸이 벽과 닿는 쪽에 얹는다 → **방향이 그림에 드러나야** 한다

## 6. 새 청크 세트를 요청할 때 쓸 프롬프트 뼈대

```
아래 첨부한 참조 시트와 **완전히 같은 규격**으로 새 바이옴 타일셋을 그려줘.

규격(반드시 지킬 것):
- 바닥 시트: 160x80px, 20x20px 타일 8열x4행 = 32칸, 여백/격자선 없음
- 데코 시트: 160x80px, 같은 격자, 배경 투명, 8계열x4변형
- 픽셀 아트, 안티에일리어싱 없음, 픽셀당 1색
- 바닥 타일은 서로 어떤 순서로 이어 붙여도 이음매가 안 보여야 함
  (타일 가장자리에 테두리·큰 방향성 무늬 금지)
- 바닥 32칸의 평균 밝기 편차를 8단계 이내로

바꿀 것: <바이옴 컨셉 — 예: "얼어붙은 신경조직, 청백색 계열">
유지할 것: 격자 규격 · 이음매 없음 · 대비 수준 · 데코의 여백 비율
```
"""


def write_spec():
    path = os.path.join(OUT, "타일셋_사양서.md")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(SPEC)
    print("  타일셋_사양서.md")


if __name__ == "__main__":
    main()
