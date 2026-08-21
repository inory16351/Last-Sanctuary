# -*- coding: utf-8 -*-
"""엘리시아(9012 · 「무너지지 않는 방패」) 모션 시트 → 프레임 분해 (2026-08-21).

유저 지시: *"볼트 폴더의 테이블이랑 에셋 이미지, 일러스트 이미지 확인해서 엘리시아랑
Seraphiel, Cyan 이렇게 총 3종 캐릭터 인게임에 구현해줘"*

원본 — **두 장 중 `_02`** 를 쓴다
==================================
``<볼트>/리소스/sprites/Elysia_asset_01.png`` (1536x1024 · RGBA)
``<볼트>/리소스/sprites/Elysia_asset_02.png`` (1536x1024 · RGBA)  ← ★ 정본

두 장은 <b>구획이 완전히 같고 그림 크기만 다르다</b>. ``_02`` 가 <b>더 크게</b> 그려져
있어(대기 프레임 폭 92~105px vs ``_01`` 의 78~88px) 픽셀이 더 많다. 게임에서 확대해
쓰므로 <b>큰 쪽이 정본</b>이다 — 베일이 «자를 수 있는 쪽» 을 고른 것과는 다른 기준이다
(이 시트는 둘 다 같은 방식으로 갈리므로 해상도로 고른다).

★★ <b>이 시트에는 라벨도 구획선도 없다</b>
------------------------------------------
프레임 번호·제목 딱지가 <b>하나도 없고</b> 줄을 가르는 선도 없다. 게다가 날개·검이
옆 칸까지 뻗어 <b>가로로도 세로로도 덩어리가 붙는다</b>(실측: 이동 줄은 6칸이 한 덩어리
``x15~817`` 로 잡히고, 시트 전체가 세로로 한 밴드 ``y0~1007`` 다). 그래서:

  · <b>y 는 사람이 격자를 보고 읽었다</b> — `sheet_grid.py` 로 100px 격자를 얹어 읽은 값이
    아래 :data:`BODY_ROWS` 다. 자동 밴드 탐지는 <b>시트 전체를 한 덩어리</b>로 잡는다.
  · <b>x 는 «폭 ÷ 장수»</b>(:func:`skin_sheet.cells_by_span`) 로 가른다. 라벨이 없으니
    라벨 기반 방법은 애초에 쓸 수 없고, 빈 열 기반도 붙어서 안 된다.
  · 그렇게 가르면 <b>칸마다 옆 칸의 날개 끝이 딸려 온다</b> — :func:`skin_sheet.boxes_dominant`
    가 «칸 한가운데를 물고 있는 덩어리» 만 남겨 그것을 막는다(베일에서 같은 문제를 잡은 함수).

★ <b>장수의 근거</b> — 대기 줄만 빈 열로 깨끗하게 갈리고 <b>정확히 6덩어리</b>다
  (폭 92~105px · 시작 x 34·151·273·392·518·633 → 간격 119.8px 로 고르다). 격자 이미지에서
  나머지 줄도 같은 6칸 자리에 놓여 있다. 마지막 줄(궁극기)만 <b>4장</b>이다.

무엇이 어디로 가나 (배선은 `Editor/CharacterSkinBuilder.cs` 가 폴더 이름으로 한다)
==================================================================================
| 시트 줄 (y) | 폴더 | 근거 |
|---|---|---|
| 1 (5~128) 정면 대기 | ``Idle`` | 방패를 든 정면 자세 |
| 2 (132~252) 걷기 | ``Move`` | 날개를 뒤로 뻗고 달린다 |
| 3 (255~372) 검 찌르기 <b>이펙트 없음</b> | ``Unused_MeleeNoFx`` | 4번 줄과 <b>같은 동작</b>인데 궤적이 없다 → 원화 보존만 |
| 4 (376~503) 검 찌르기 + <b>금색 궤적</b> | ``MeleeAttack`` | 궤적까지 그려진 <b>완성본</b>이라 이쪽을 쓴다 |
| 5 (508~625) 빛 창을 앞으로 | ``RangedAttack`` · ``MagicAttack`` | 오른쪽 단 1번(빛 창 투사체)과 <b>짝</b>이다 |
| 6 (628~750) 두 손을 모으고 발밑에 빛 고리 | ``Heal`` | 표의 회복력이 <b>12(최고)</b> 라 회복 역할이다 |
| 7 (760~940) 네 날개를 펼친다 · <b>4장</b> | ``Skill1`` | 「네 날개의 가호」(80036) 그 자체 |

⚠ 5번 줄을 <b>두 폴더에 같은 그림으로</b> 굽는다. 표의 능력치가 마법 6 · 원거리 2 라
  역할 역산(`CharacterRole`)이 어느 쪽을 고를지 그림만 봐서는 알 수 없고, 둘 다 «빛
  투사체를 앞으로 쏘는» 같은 동작이다 — 비워두면 그 칸이 무작위 폴백을 탄다.

이펙트 (오른쪽 단 · x 880~1536)
--------------------------------
| 줄 (y) | 폴더 | 무엇 |
|---|---|---|
| 5~120 | ``Projectile`` | 빛 창 → 별 4단계 |
| 125~210 | ``Impact`` | 착탄 구체 3단계 |
| 212~356 | ``Unused_Sigils`` | 방패 문양·나침반별·고리 — <b>배선하지 않는다</b>(대응하는 스킬 칸이 없다) |
| 358~505 | ``MeleeTravelFx`` | 초승달 궤적 3장 — 4번 줄의 궤적이 «날아가는» 판본 |
| 508~610 | ``HealFx`` | 빛 기둥 + 바닥 고리 3장 |
| 612~750 | ``Skill1Fx`` | 금색 고리 + 빛 기둥 (「가호」의 바닥 연출) |
| 790~1015 | ``Unused_Ultimate`` | 큰 나침반 문양·깃털·빛줄기 |

⚠ ``Skill1Fx`` 를 «금색 고리» 쪽으로 골랐다 — 큰 나침반 문양 줄(790~1015)은 깃털·빛줄기가
  <b>서로 붙어</b> 한 덩어리(실측 x940~1535)라 프레임으로 가르면 <b>깃털 토막</b>이 나온다.
  고리 쪽은 덩어리가 깨끗하게 떨어진다. 원화는 ``Unused_`` 로 남긴다.

방향 — ★★ <b>이동 줄만 왼쪽을 본다</b>
--------------------------------------
⚠⚠ <b>2026-08-21 수정</b> — 처음에 «원본이 전부 오른쪽» 으로 잡았는데 <b>틀렸다</b>.
  유저 리포트: *"왼쪽을 보고있는데 오른쪽으로 이동중"*. 다시 재보니 줄마다 다르다(실측):

| 줄 | 보는 쪽 | 근거 |
|---|---|---|
| <b>이동</b> | <b>왼쪽</b> | 얼굴·방패가 <b>왼쪽</b>이고 <b>날개가 오른쪽으로 끌린다</b> — 끌리는 쪽의 반대가 진행 방향이다 |
| 근접 | 오른쪽 | 검이 오른쪽 아래로 찌르고 <b>금색 궤적이 오른쪽</b>에 그려진다 |
| 원거리 | 오른쪽 | 빛 창이 <b>오른쪽</b>으로 뻗고 별 폭발도 오른쪽이다 |
| 대기 · 회복 · 스킬1 | 정면 대칭 | 방패를 정면으로 들고 있다 |

★ <b>모든 «Right» 칸이 오른쪽을 보게 맞춘다</b> — 한 줄이라도 어긋나면 걷다가 공격할 때
  <b>캐릭터가 좌우로 뒤집힌다</b>(`CharacterAnimator` 는 `FacingRight` 하나로 두 칸을 고른다).
  그래서 원본이 왼쪽인 줄만 :data:`SOURCE_FACES_LEFT` 에 넣어 <b>뒤집어서</b> Right 로 굽는다.

사용법:  python Tools/elysia_skin_build.py
다음:    유니티 메뉴 **LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기**
        그 다음 `python Tools/measure_skin_tiles.py`
"""

import os
import sys

from PIL import Image

from vault_path import VAULT, PROJECT

from skin_sheet import (  # noqa: F401
    PPU, SKIN_SPEC_NAME, write_skin_spec,
    load_sheet, cells_by_span, cells_by_gaps, boxes_dominant, boxes_for, crop_rgba,
    body_anchor, base_anchor, compose, write_png, ensure_folder_meta,
    clear_frames,
)

SRC = os.path.join(VAULT, "리소스", "sprites", "Elysia_asset_02.png")

DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Elysia", "Char")

#: 몸통 줄 — (폴더, y0, y1, 장수). ★ y 는 격자로 읽은 <b>실측</b>이다(맨 위 ★★).
BODY_ROWS = [
    ("Idle",               5, 128, 6),
    ("Move",             132, 252, 6),
    ("Unused_MeleeNoFx", 255, 372, 6),
    ("MeleeAttack",      376, 503, 6),
    ("RangedAttack",     508, 625, 6),
    ("Heal",             628, 750, 6),
    ("Skill1",           760, 940, 4),
]

#: 몸통 단의 x 범위 — 오른쪽 이펙트 단(880~)이 들어오지 않게 막는다.
BODY_X = (0, 860)

#: ★ 같은 그림을 <b>두 폴더</b>에 굽는다 (맨 위 ⚠).
ALSO_AS = {"RangedAttack": "MagicAttack"}

#: 이펙트 줄 — (폴더, y0, y1, x0, x1, 장수).
#:
#: ★★ <b>이 단은 «애니메이션» 이 아니라 «부품 팔레트» 다.</b> 한 줄에 <b>서로 다른 것</b>이
#:   나란히 있다 — 예를 들어 빛 기둥 줄에는 기둥 3단계 <b>다음에 깃털</b>이 붙어 있고,
#:   고리 줄에는 고리 하나 뒤에 <b>날개 네 장과 반짝임</b>이 있다. 그래서
#:   <b>«progressive 한 단계» 인 줄만 프레임으로 굽고</b> 나머지는 ``Unused_`` 로 남긴다.
#:   ⚠ 팔레트를 그대로 프레임으로 구우면 재생할 때 <b>깃털이 한 프레임 끼어든다</b>.
#:
#: ★ 칸은 «폭 ÷ 장수» 다(:func:`skin_sheet.cells_by_span`). 빈 열로는 못 가른다 —
#:   글로우가 서로 닿아 한 덩어리가 된다(실측: 문양 줄 전체가 ``x961~1529`` 한 덩어리).
#: ★ ``x1`` 로 <b>줄 뒤쪽의 다른 부품을 잘라낸다</b> — 그게 «장수 ÷ 폭» 을 맞히는 열쇠다
#:   (깃털까지 넣으면 3단계가 4~5칸으로 갈린다).
FX_ROWS = [
    # 빛 창 → 별. 4단계가 또렷하게 커진다.
    ("Projectile",        5,  120,  885, 1515, 4),
    # 착탄 구체 4단계(큰 구체 → 작은 마름모). x 를 1365 로 끊어 <b>반짝임 부품</b>을 뺀다.
    ("Impact",          125,  210,  960, 1365, 4),
    # 방패 문양·나침반별·고리별·별·타원 — 대응하는 스킬 칸이 없다.
    ("Unused_Sigils",   212,  356,  955, 1535, 5),
    # 초승달 궤적 3장 — 근접 4번 줄의 궤적이 «날아가는» 판본이다.
    ("MeleeTravelFx",   358,  505,  945, 1520, 3),
    # 빛 기둥 3단계. x 를 1370 으로 끊어 <b>깃털</b>을 뺀다.
    ("HealFx",          508,  610,  990, 1370, 3),
    # 「가호」의 바닥 고리 — <b>한 장</b>이다(뒤의 날개·반짝임은 아래 Unused_).
    ("Skill1Fx",        612,  750,  920, 1195, 1),
    ("Unused_Wings",    612,  750, 1195, 1535, 2),
    # 큰 나침반 문양·깃털·빛줄기 — 서로 붙어 한 덩어리(x940~1535)라 못 가른다.
    ("Unused_Ultimate", 790, 1015,  920, 1535, 1),
]

#: 좌우 방향이 없는 묶음 — 파일 이름에 Right/Left 를 안 붙인다.
NO_DIRECTION = {"Projectile", "Impact", "MeleeTravelFx", "HealFx", "Skill1Fx"}

#: ★★ <b>원본이 왼쪽을 보고 있는 줄</b> — 뒤집어서 Right 로 굽는다(맨 위 ⚠⚠).
SOURCE_FACES_LEFT = {"Move"}

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
CELL_INSET = {"Move": 12}


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
    "skinAssetName": "Skin_Elysia",
    "outputFolder": "Assets/_Project/Resources/Skins",
    "displayName": "엘리시아",
    "framesPerSecond": "10",
    "attackFramesPerSecond": "12",
    # 빛 창 투사체 — 검 한 자루 길이면 화면에서 «날아가는 창» 으로 읽힌다.
    "projectileWidthTiles": "1.0",
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


def build(sheet):
    made = 0

    for name, y0, y1, count in BODY_ROWS:
        cells = cells_by_span(sheet["mask"], y0, y1, BODY_X[0], BODY_X[1], count)
        cells = inset_cells(cells, CELL_INSET.get(name, 0))
        if not cells:
            raise SystemExit("[!] %s: 칸을 못 찾았습니다 (y%d~%d)" % (name, y0, y1))
        # 옆 칸의 날개 끝을 버린다 (맨 위 ★★ 세 번째 점).
        boxes = [b for b in boxes_dominant(sheet["mask"], cells, y0, y1, min_ink_ratio=0.02) if b is not None]
        frames = [crop_rgba(sheet, b) for b in boxes]
        images, w, h = compose(frames, [body_anchor(f) for f in frames])
        made += write_group(images, name)
        print("  %-18s %3d x %3d · %2d장  %s  폭 %s"
              % (name, w, h, len(images),
                 "<-" if name in SOURCE_FACES_LEFT else "->",
                 [b[1] - b[0] + 1 for b in boxes]))

        if name in ALSO_AS:
            alias = ALSO_AS[name]
            made += write_group(images, alias)
            print("  %-18s %3d x %3d · %2d장  (같은 그림 — 맨 위 ⚠)"
                  % (alias, w, h, len(images)))

    for name, y0, y1, x0, x1, count in FX_ROWS:
        cells = cells_by_span(sheet["mask"], y0, y1, x0, x1, count)
        if not cells:
            print("  [!] %s: 칸을 못 찾았습니다 (y%d~%d) — 건너뜁니다" % (name, y0, y1))
            continue
        boxes = [b for b in boxes_for(sheet["mask"], cells, y0, y1) if b is not None]
        frames = [crop_rgba(sheet, b) for b in boxes]
        images, w, h = compose(frames, [base_anchor(f) for f in frames])
        made += write_group(images, name)
        print("  %-18s %3d x %3d · %2d장  칸 %s"
              % (name, w, h, len(images), [(a, b) for a, b in cells]))

    return made


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print("[엘리시아 모션 시트 분해]")
    sheet = load_sheet(SRC)
    n = build(sheet)
    spec = write_skin_spec(DST_ROOT, SKIN_SPEC, "Tools/elysia_skin_build.py")
    ensure_folder_meta(DST_ROOT)
    ensure_folder_meta(os.path.dirname(DST_ROOT))
    print("  스킨 설정 %s (%d줄)" % (SKIN_SPEC_NAME, spec))
    print("  -> 프레임 %d장" % n)
    print("  다음: 유니티 메뉴 LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기")


if __name__ == "__main__":
    main()
