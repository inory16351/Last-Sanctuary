# -*- coding: utf-8 -*-
"""세라피엘(9013 · 「침묵의 총성」) 모션 시트 → 프레임 분해 (2026-08-21).

원본 — **세 장 중 `_02`** 를 쓴다
==================================
``<볼트>/리소스/sprites/asset_Seraphiel_01.png`` (1536x1024 · RGBA)
``<볼트>/리소스/sprites/asset_Seraphiel_02.png`` (1536x1024 · RGBA)  ← ★ 정본
``<볼트>/리소스/sprites/asset_Seraphiel_03.png`` (1536x1024 · **RGB**)

⚠⚠ <b>``_03`` 은 쓸 수 없다</b> — 알파가 <b>체커보드 무늬로 구워져</b> 있다(모드가 RGB 다).
  투명이 «회색 두 색이 번갈아 칠해진 격자» 로 <b>화소에 박혀</b> 있어서, 배경 판정이
  두 색 중 <b>하나만</b> 배경으로 잡고 나머지를 그림으로 본다 — 프레임마다 격자가 남는다.
  그림은 셋 중 가장 크고 「확대 컷」 줄도 하나 더 있는데, <b>배경을 되살릴 방법이 없다</b>.
  ★ ``_02`` 는 RGBA 이고 ``_01`` 보다 크게 그려져 있다 → ``_02`` 가 정본.

★★ <b>두 단으로 나뉜 시트다</b>
------------------------------
위 두 줄(대기·이동)은 <b>가로 전체</b>를 쓰고, 그 아래 세 줄은 <b>왼쪽/오른쪽 단</b>에
서로 다른 모션이 나란히 있다:

    근거리 공격(왼) | 원거리 공격(오른)
    마법(왼)        | 회복(오른)
    스킬1 블링크(왼) | 스킬2 탄막(오른)

★ <b>y 는 «딱지가 없는 x 창» 으로 갈랐다</b> — 제목 딱지(검은 알약)가 줄마다 다른 x 에
  있어서, 딱지가 닿지 않는 창으로 세로 밴드를 재고 격자 이미지로 확인했다(실측):

    x 720~1530 (대기·이동 딱지 밖)  →  대기 39~126 · 이동 168~266
    x 460~1000 (왼쪽 단 딱지 밖)    →  275~398 · 430~557 · 559~675

⚠ <b>왼쪽 위 «2x 확대 / 1x» 미리보기는 프레임이 아니다</b>(x 0~400 · y 0~270).
  대기·이동 줄의 ``x0`` 를 430 으로 둬서 통째로 잘라낸다 — 넣으면 <b>거인 한 장</b>이
  대기 모션에 섞인다.

무엇이 어디로 가나
==================
| 시트 줄 | 폴더 | 장수 |
|---|---|---|
| 대기 모션 (Idle) | ``Idle`` | 8 |
| 이동 모션 (Move / Walk) | ``Move`` | 9 — <b>원본이 왼쪽</b> |
| 근거리 공격 (Melee / Gun-Kata) | ``MeleeAttack`` | 7 |
| 원거리 공격 (Ranged Attack) | ``RangedAttack`` | 6 |
| 마법 모션 (Magic Attack) | ``MagicAttack`` | 6 |
| 회복 모션 (Heal Skill) | ``Heal`` | 6 |
| 스킬 1 (Blink / Teleport Back) | ``Skill1`` | 7 — 「회피 기동」(80037) · <b>원본이 왼쪽</b> |
| 스킬 2 (Bullet Storm / Gatling) | ``Skill2`` | 6 — 「종말의 선언」(80039) |

⚠ 「명사수」(80038)는 <b>모션이 없다</b> — 크리티컬 확률이 영구히 오르는 상시 패시브라
  시트에도 그런 줄이 없다. 없는 것을 지어내지 않는다.

이펙트 — 아래 두 띠의 <b>상자들</b>
-----------------------------------
★★ <b>이 상자들은 «부품 팔레트» 다</b> — 한 상자 안에 <b>세로로 2~3 줄</b>이 쌓여 있고
  그 줄들은 «단계» 가 아니라 <b>변형</b>이다(총알 상자는 같은 배열이 3줄 = 탄종 3가지).
  실측: 총알 상자는 ``y720~739`` · ``761~783`` · ``800~819`` 세 줄에 각각 7덩어리다.
  → <b>맨 위 줄만</b> 프레임으로 굽는다. 아래 줄까지 한 칸에 넣으면 두 장이 겹쳐 구워진다.

| 상자 | 폴더 | 장수 |
|---|---|---|
| 총알 / 투사체 (맨 위 줄) | ``Projectile`` | 7 |
| 총구 이펙트 (맨 위 줄) | ``MuzzleFlash`` | 4 |
| 근거리 이펙트 (맨 위 줄) | ``MeleeTravelFx`` | 3 |
| 마법 이펙트 (맨 위 줄) | ``ImpactMagic`` | 4 |
| 회복 이펙트 | ``HealFx`` | 4 |
| 스킬1 이펙트 (Teleport) | ``Skill1Fx`` | 4 |
| 스킬2 이펙트 (Bullet Storm) | ``Skill2Fx`` | 3 |

사용법:  python Tools/seraphiel_skin_build.py
다음:    유니티 메뉴 **LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기**
        그 다음 `python Tools/measure_skin_tiles.py`
"""

import os
import sys

from PIL import Image

from vault_path import VAULT, PROJECT

from skin_sheet import (  # noqa: F401
    PPU, SKIN_SPEC_NAME, write_skin_spec,
    load_sheet, cells_by_span, boxes_dominant, boxes_for, crop_rgba,
    body_anchor, base_anchor, compose, write_png, ensure_folder_meta,
    clear_frames,
)

SRC = os.path.join(VAULT, "리소스", "sprites", "asset_Seraphiel_02.png")

DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Seraphiel", "Char")

#: 몸통 줄 — (폴더, y0, y1, x0, x1, 장수). ★ 전부 <b>실측</b>이다(맨 위 ★).
#: ⚠ 대기·이동의 x0 이 430 인 것은 «2x 확대» 미리보기를 잘라내기 위한 것이다.
BODY_ROWS = [
    ("Idle",          39, 126,  430, 1535, 8),
    # ⚠⚠ <b>이동 줄의 x0 은 480 이다</b>(대기는 430) — 왼쪽 위 «2x 확대» 미리보기의
    #   <b>오른쪽 날개가 y168~266 까지 내려와 x472 까지 뻗는다</b>(실측: 이 밴드의 열 덩어리가
    #   `343~575` 로 미리보기 날개와 1번 프레임 몸통을 <b>한 덩어리</b>로 물고 있다).
    #   430 으로 두면 그 날개가 «1번 프레임의 날개» 로 구워진다 — 실제로 그렇게 나왔다
    #   (유저 리포트의 «잘린 날개가 오른쪽에 붙었다» 가 이것이다).
    ("Move",         168, 266,  480, 1535, 9),
    # ★★ y0 은 <b>딱지 아래</b>다 — 딱지가 세 줄 다 <b>밴드 맨 위에</b> 있다(실측:
    #   근거리/원거리 y275~303 · 마법/회복 y413~439 · 스킬1/2 y559~587).
    #   ⚠ 처음에 275·430·559 로 잡았더니 <b>딱지 글자가 프레임에 통째로</b> 구워졌다.
    ("MeleeAttack",  306, 398,    5, 1005, 7),
    ("RangedAttack", 306, 398, 1020, 1535, 6),
    ("MagicAttack",  442, 557,    5, 1005, 6),
    ("Heal",         442, 557, 1020, 1535, 6),
    ("Skill1",       590, 675,    5, 1000, 7),
    ("Skill2",       590, 675, 1000, 1535, 6),
]

#: 이펙트 상자 — (폴더, y0, y1, x0, x1, 장수).
#: ⚠ 위 띠는 <b>맨 위 줄만</b> 쓴다(맨 위 ★★) — y 를 720~745 로 좁게 잡은 것이 그것이다.
FX_ROWS = [
    # ⚠ 위 띠의 딱지는 y681~708 이다 → y0 = 712.
    ("Projectile",    712,  750,   10,  292, 7),
    ("MuzzleFlash",   712,  760,  295,  566, 4),
    ("MeleeTravelFx", 712,  760,  568,  806, 3),
    ("ImpactMagic",   712,  760,  870, 1180, 4),
    # 아래 띠 — 딱지가 y828~860 이다.
    # ★★ 이 세 상자는 <b>세로로 두 줄</b>인데 <b>사이에 빈 줄이 없다</b>(실측: 회복·스킬1 은
    #   y860~1010 이 한 덩어리다). 그래서 사람이 <b>아래 줄만</b> y 로 잘라냈다 —
    #   위·아래를 한 칸에 넣으면 <b>한 프레임에 두 장이 겹쳐</b> 구워진다(실제로 그랬다).
    ("HealFx",        935, 1012,   10,  378, 4),
    ("Skill1Fx",      935, 1015,  466,  768, 3),
    # 스킬2 만 두 줄 사이에 빈 줄이 있다(y910~913) → 아래 줄이 연기→불→십자 진행이다.
    ("Skill2Fx",      913, 1011, 1140, 1332, 3),
]

#: ★★ <b>칸을 안쪽으로 좁히는 폭</b>(px) — 0 이면 좁히지 않는다.
#:
#: ⚠⚠ <b>왜 필요한가</b> (2026-08-21 · 유저 리포트: *"왼쪽 날개부분이 잘려서 나오는데 그
#:   잘린 부분이 오른쪽에 붙었어"*) — 이동 줄은 <b>모션 블러로 프레임이 이어져</b> 있다.
#:   실측: 이동 줄 ``x420~575`` 가 <b>열 하나도 비지 않은 한 덩어리</b>다. 즉 앞 프레임의
#:   날개가 흐릿한 꼬리로 다음 프레임까지 <b>붙어</b> 있어서, 열을 보고 가르는 어떤 방법도
#:   (`boxes_dominant` 의 «떨어진 덩어리 버리기» 포함) <b>이 둘을 못 나눈다</b>.
#:
#: → 그래서 칸을 <b>양쪽에서 조금씩 깎는다</b>. 몸통은 칸 가운데에 있고(폭 70~90px in 117px)
#:   블러 꼬리는 <b>칸 가장자리</b>에 있으므로, 가장자리를 버리면 남의 날개만 떨어진다.
#: ⚠ 너무 크게 깎으면 <b>자기 날개</b>가 잘린다 — 몸통 폭과 칸 폭의 차이의 절반이 상한이다.
#: 좌우 방향이 없는 묶음.
NO_DIRECTION = {"Projectile", "MuzzleFlash", "MeleeTravelFx", "ImpactMagic",
                "HealFx", "Skill1Fx", "Skill2Fx"}

#: ★★ <b>원본이 왼쪽을 보고 있는 줄</b> — 뒤집어서 Right 로 굽는다.
#:
#: ⚠⚠ <b>2026-08-21 수정</b> — 처음에 «원본이 전부 오른쪽» 으로 잡았는데 <b>틀렸다</b>.
#:   유저 리포트: *"왼쪽을 보고있는데 오른쪽으로 이동중"*. 다시 재보니 줄마다 다르다(실측):
#:
#:     <b>이동</b>    가면이 <b>왼쪽</b> · 총구가 왼쪽 아래 · <b>날개가 오른쪽으로 끌린다</b>  → 왼쪽
#:     <b>스킬1</b>   가면이 <b>왼쪽</b> · 총이 왼쪽 아래 · 잔상이 오른쪽                      → 왼쪽
#:     원거리·마법·스킬2  총구가 <b>오른쪽</b>으로 뻗고 <b>총구 화염도 오른쪽</b>            → 오른쪽
#:     근접(건카타)  회전 동작이라 프레임마다 갈리는데 3번의 총구가 오른쪽 위다              → 오른쪽
#:     대기·회복     정면 대칭(총을 아래로 모으고 있다)                                      → 그대로
#:
#: ★ <b>모든 «Right» 칸이 오른쪽을 보게 맞춘다</b> — 한 줄이라도 어긋나면 걷다가 공격할 때
#:   <b>캐릭터가 좌우로 뒤집힌다</b>(`CharacterAnimator` 는 `FacingRight` 하나로 두 칸을 고른다).
SOURCE_FACES_LEFT = {"Move", "Skill1"}

#: 칸을 안쪽으로 좁히는 폭 (위 ★★). 적어두지 않은 줄은 0 이다.
#: ★ 미리보기를 x0 으로 걷어낸 뒤에는 <b>조금만</b> 깎으면 된다 — 14 는 자기 날개까지
#:   잘라먹었다(실측: 칸 폭 55~61px 로 몸통이 클리핑됐다).
CELL_INSET = {"Move": 4}


def inset_cells(cells, px):
    """각 칸을 양쪽에서 ``px`` 만큼 깎는다. 칸이 뒤집히지 않게 최소 폭을 남긴다."""
    if px <= 0:
        return cells
    out = []
    for a, b in cells:
        keep = max(8, (b - a + 1) - px * 2)
        mid = (a + b) // 2
        out.append((mid - keep // 2, mid + keep // 2))
    return out

SKIN_SPEC = {
    "skinAssetName": "Skin_Seraphiel",
    "outputFolder": "Assets/_Project/Resources/Skins",
    "displayName": "세라피엘",
    "framesPerSecond": "10",
    # 갯틀링을 쏘는 인물이라 공격 프레임을 빠르게 돌린다.
    "attackFramesPerSecond": "16",
    # 총알 — 화면에서 «탄환» 으로 읽히는 최소 크기.
    "projectileWidthTiles": "0.6",
}


def write_group(images, name):
    folder = os.path.join(DST_ROOT, name)
    gone = clear_frames(folder)
    if gone:
        print("      (i) 예전 프레임 %d개 지움 (%s)" % (gone, name))
    n = 0
    for i, img in enumerate(images):
        if name in NO_DIRECTION or name.startswith("Unused_"):
            write_png(img, folder, "Char_%s_%02d" % (name, i))
            n += 1
        else:
            flipped = img.transpose(Image.FLIP_LEFT_RIGHT)
            right, left = ((flipped, img) if name in SOURCE_FACES_LEFT
                           else (img, flipped))
            write_png(right, folder, "Char_%s_Right_%02d" % (name, i))
            write_png(left, folder, "Char_%s_Left_%02d" % (name, i))
            n += 2
    ensure_folder_meta(folder)
    return n


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print("[세라피엘 모션 시트 분해]")
    sheet = load_sheet(SRC)
    made = 0

    for name, y0, y1, x0, x1, count in BODY_ROWS:
        cells = cells_by_span(sheet["mask"], y0, y1, x0, x1, count)
        cells = inset_cells(cells, CELL_INSET.get(name, 0))
        # ★★ 2026-08-21 — <b>0.06 → 0.02</b>. 유저 리포트: *"왼쪽 날개부분이 잘려서 나오는데
        #   그 잘린 부분이 오른쪽에 붙었어"*. 이 시트는 <b>프레임이 겹쳐</b> 그려져 있어서
        #   앞 프레임의 날개 끝이 다음 칸으로 삐져 들어온다. `boxes_dominant` 의 이 값은
        #   «본체에서 이만큼 떨어진 덩어리까지 이어 붙인다» 는 뜻이라, 0.06(≈7px)이면
        #   <b>남의 날개까지 붙여</b> 굽는다(실측: 이동 1·7·9번에 날개 조각이 남았다).
        #   0.02 는 하한 4px 로 떨어져 <b>붙어 있는 자기 날개만</b> 남는다.
        boxes = [b for b in boxes_dominant(sheet["mask"], cells, y0, y1, min_ink_ratio=0.02)
                 if b is not None]
        if len(boxes) != count:
            print("  [!] %s: 칸 %d개 기대했는데 %d개" % (name, count, len(boxes)))
        frames = [crop_rgba(sheet, b) for b in boxes]
        images, w, h = compose(frames, [body_anchor(f) for f in frames])
        made += write_group(images, name)
        print("  %-14s %3d x %3d · %2d장  %s  폭 %s"
              % (name, w, h, len(images),
                 "<-" if name in SOURCE_FACES_LEFT else "->",
                 [b[1] - b[0] + 1 for b in boxes]))

    for name, y0, y1, x0, x1, count in FX_ROWS:
        cells = cells_by_span(sheet["mask"], y0, y1, x0, x1, count)
        boxes = [b for b in boxes_for(sheet["mask"], cells, y0, y1) if b is not None]
        if not boxes:
            print("  [!] %s: 그림을 못 찾았습니다 (y%d~%d)" % (name, y0, y1))
            continue
        frames = [crop_rgba(sheet, b) for b in boxes]
        images, w, h = compose(frames, [base_anchor(f) for f in frames])
        made += write_group(images, name)
        print("  %-14s %3d x %3d · %2d장" % (name, w, h, len(images)))

    spec = write_skin_spec(DST_ROOT, SKIN_SPEC, "Tools/seraphiel_skin_build.py")
    ensure_folder_meta(DST_ROOT)
    ensure_folder_meta(os.path.dirname(DST_ROOT))
    print("  스킨 설정 %s (%d줄)" % (SKIN_SPEC_NAME, spec))
    print("  -> 프레임 %d장" % made)
    print("  다음: 유니티 메뉴 LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기")


if __name__ == "__main__":
    main()
