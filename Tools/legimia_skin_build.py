# -*- coding: utf-8 -*-
"""레기미아(웨이브 30 보스 120006) 모션 시트 → 프레임 분해 (2026-08-21).

유저 지시: *"현재 레기미아의 인게임 이미지가 다른 이미지로 되어있어 이거 볼트 폴더에서
레기미아 에셋 확인하고 인게임에 적용시켜줘"*.

★★ <b>왜 다른 그림이 나오고 있었나</b> (실측 진단 · 이 스크립트를 쓴 이유)
=========================================================================
씬에는 ``Monster_Legimia_Template`` 이 이미 있고 그 `CharacterAnimator.skinResourceFolder`
는 ``MonsterSkins/Legimia`` 로 배선돼 있었다. 그런데 <b>그 폴더가 아예 없었다</b> —
스킨을 한 번도 구운 적이 없다. 게다가 `MonsterSpawner.bossSlots` 의 레기미아 칸이
<b>베일의 `MonsterUnit` 을 가리키고</b> 있었다(둘 다 `fileID: 818891188`). 그래서
30웨이브에 <b>베일이 그대로</b> 나왔다 — "다른 이미지" 의 정체가 이것이다.
→ 스킨은 이 스크립트가 굽고, 슬롯 배선은 씬 쪽에서 고친다.

원본 — **한 장**
================
``<볼트>/리소스/sprites/Char_Asset_Legimia.png`` (1536x1024)

베일과 달리 판본이 하나뿐이고 <b>구획이 아주 깨끗하다</b> — 아홉 줄이 가로 띠로 갈리고
줄마다 프레임이 빈 열로 떨어진다. 그래서 베일에서 필요했던 «붙은 덩어리 가르기» 도,
«몸통 칸만 골라내기»(`CELL_KEEP`) 도 필요가 없다.

★★ 그 대신 <b>프레임 번호가 함정이다</b>
----------------------------------------
번호(1~6)가 그림 <b>바로 옆·위</b>에 찍혀 있어 세 가지 방법이 다 실패한다(실측):

| 방법 | 왜 실패하나 |
|---|---|
| 부스러기로 버리기 | 번호 여섯 중 <b>셋만</b> 떨어져 있다. 대기 줄의 2·3·4 는 그림 덩어리에 <b>붙어</b> 들어간다(폭 155→169). |
| 빈 열로 가르기 | 이동 줄의 번호 「3」(x 553~560)은 <b>2번 프레임의 x 범위 안</b>이다(2번 덩어리 354~561). 어느 칸에 넣어도 틀린다. |
| 라벨 중간점으로 가르기 | 사망 줄 3번 프레임(580~768)이 4번 라벨의 중간점(≈699)을 <b>넘는다</b> — 3번의 오른쪽 절반이 4번 칸으로 간다. |

→ **번호를 먼저 지운다**(:data:`LABELS`). 번호는 회색 글자라 `load_sheet` 의 ``gray``
  마스크로 정확히 잡히고, 잡은 덩어리의 <b>세로 범위까지 재서</b> 그 상자만 지운다.
  ⚠ 지운 자리에 그림이 없는지 확인했다 — 상자 54개 전부 «잉크 40~131px, 그중 채도 있는
    픽셀 6~47px» 로 <b>숫자 한 글자분과 그 안티에일리어싱</b> 뿐이다. 그림이 섞여 있으면
    잉크가 수백 px 이 된다.
  ★ 지운 뒤에는 모든 줄이 <b>정확히 6칸</b>(이펙트 줄은 5~6칸)으로 떨어진다 — 아래 실행 결과.

무엇이 어디로 가나 (배선은 `Editor/CharacterSkinBuilder.cs` 가 폴더 이름으로 한다)
==================================================================================
| 시트 줄 | 폴더 | 스킨 칸 |
|---|---|---|
| 대기 모션 | ``Char/Idle`` | ``idleRight`` · ``idleLeft`` |
| 이동 모션 | ``Char/Move`` | ``walkRight`` · ``walkLeft`` |
| 근거리 공격 | ``Char/MeleeAttack`` | ``attackRight`` · ``attackLeft`` |
| 사망 모션 | ``Char/Death`` | ``deathRight`` · ``deathLeft`` |
| 스킬1 본체 시전 + 본체 스킬 효과 | ``Char/Skill1`` | ``skill1Right`` · ``skill1Left`` |
| 스킬1 종양 오브젝트 | ``Char/Skill1Projectile`` | ``skill1Projectile`` |
| 스킬1 종양 폭발 이펙트 | ``Char/Skill1Fx`` | ``skill1Fx`` |
| 스킬2 회복 이펙트 | ``Char/Skill2Fx`` | ``skill2Fx`` |

★ <b>스킬1 은 두 줄을 이어 12장</b>이다. 시트가 「본체 시전 모션」과 「본체 스킬 효과」를
  갈라 그렸지만 <b>같은 동작의 앞뒤</b>다 — 가슴의 문양이 1→6 으로 차오르고(시전),
  이어서 6장 동안 터질 듯이 빛난다(효과). 두 줄을 각각 6장으로 두면 어느 쪽도 동작이
  안 되고, 이으면 시전시간(`BossSkill_130011.castSeconds` = 1.5초)에 12장 ÷ 10fps =
  1.2초로 맞는다. ⚠ 두 줄의 프레임 폭이 같아야 이을 수 있는데 실측이 그렇다
  (153~176 vs 154~171 · 캔버스는 :func:`compose` 가 한 번에 잡는다).

⚠ <b>``Skill2`` 본체 모션은 시트에 없다.</b> 「강제 보급」(130012)에는 회복 이펙트만
  그려져 있다. 없는 그림을 지어내지 않는다 — 비워두면 `BossSkillCaster` 가 평타 모션으로
  시전한다(베일의 ``skill1Fx`` 를 비워둔 것과 같은 판단).

⚠ <b>``RangedAttack`` 도 없다</b> — 표의 `attackType` 이 근접(0)이라 원거리 줄이 애초에
  그려지지 않았다.

방향 — **이동·근접만 왼쪽**
===========================
| 줄 | 보는 쪽 | 근거 |
|---|---|---|
| 이동 | <b>왼쪽</b> | 얼굴이 왼쪽, 촉수가 오른쪽으로 끌린다 |
| 근거리 공격 | <b>왼쪽</b> | 4번 프레임의 <b>참격 궤적이 왼쪽</b>에 그려져 있다 |
| 대기 · 사망 · 스킬1 | 정면 대칭 | 촉수가 좌우로 펼쳐져 있다 — 원본을 오른쪽으로 둔다 |
그래서 :data:`SOURCE_FACES_LEFT` 두 줄만 «원본 = 왼쪽» 이고 나머지는 «원본 = 오른쪽» 이다
(`UnitCombat.spriteFacesRight` 가 참이므로 스킨의 Right 칸이 오른쪽을 봐야 한다).

★★ <b>해상도를 3배로 굽는다</b> (베일과 같은 이유 · `bale_skin_build.py` 의 ★★)
==============================================================================
레기미아는 표에서 콜라이더가 <b>15 x 10 타일</b>(베일과 같은 최대치)이고 원화 대기는
<b>155 x 115 px</b> 이다 → 화면에서 15 x 64 ÷ 155 ≈ <b>6.2배</b>로 늘어난다. Point
필터로 6배면 6px 짜리 계단이 그대로 보인다. 그래서 ① 프레임을 Lanczos 로 3배로 키워 굽고
② ppu 도 3배(64 → 192)로 올리고 ③ 필터를 Bilinear 로 둔다. <b>게임 안의 크기는 안 바뀐다</b>
— 크기는 ``contentSizeTiles`` = 픽셀 ÷ ppu 로 정해지므로 둘을 같이 올리면 상쇄된다.

사용법:  python Tools/legimia_skin_build.py
다음:    유니티 메뉴 **LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기**
        그 다음 `python Tools/measure_skin_tiles.py`
"""

import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT

from skin_sheet import (  # noqa: F401
    PPU, SKIN_SPEC_NAME, write_skin_spec,
    load_sheet, cells_by_clusters, cells_by_gaps, boxes_for, crop_rgba,
    body_anchor, base_anchor, compose, write_png, ensure_folder_meta,
    shadow_in_box, sharpen_rgba, resample_rgba, clear_frames, runs,
    enclosed_background, reflood_background,
    FILTER_BILINEAR,
)

SRC = os.path.join(VAULT, "리소스", "sprites", "Char_Asset_Legimia.png")

DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Legimia", "Char")

#: 선명도 보정 — 베일과 같은 값(그쪽 :data:`SHARPEN_AMOUNT` 주석에 네 단계 비교가 있다).
#: 레기미아도 같은 대역이다: 원화가 손그림이고 콜라이더 15x10 으로 6배 넘게 확대된다.
#: ⚠ threshold 6 이 핵심 — 평탄한 검은 몸통은 건드리지 않고 <b>경계만</b> 조인다.
#: ★ 갇힌 배경 판정 (`drop_pockets`). 기본값(300 / 40)보다 <b>둘 다 느슨하게</b> 잡았다 —
#:   레기미아는 촉수 사이 웅덩이가 작은 것도 많고(100px 대) 테두리가 <b>검은 촉수</b>라
#:   광도가 매우 낮다(실측: 하위 5% 가 10~30). 몸의 흰 문양은 <b>붉은 살</b>(광도 60~110)에
#:   둘러싸여 있어 55 로 자르면 갈린다.
POCKET_MIN_AREA = 80
POCKET_RING_LUM = 55

SHARPEN_AMOUNT = 0.40
SHARPEN_RADIUS = 1.2
SHARPEN_THRESHOLD = 6

SUPERSAMPLE = 3
BAKE_PPU = PPU * SUPERSAMPLE

SKIN_SPEC = {
    "skinAssetName": "Skin_Legimia",
    # ⚠ 웨이브 보스는 종마다 폴더 하나다 — 한 폴더에 몰아넣으면
    #   `CharacterAnimator.PickRandomSkin` 이 다른 몬스터에게 이 외형을 줄 수 있다.
    "outputFolder": "Assets/_Project/Resources/MonsterSkins/Legimia",
    "displayName": "레기미아",
    "framesPerSecond": "10",
    "attackFramesPerSecond": "12",
    # 종양 오브젝트(투사체 칸)는 «땅에 자라나는 종양» 이라 탄환보다 크다.
    # 몸집이 15타일이므로 2.5타일이면 발밑에 놓인 덩어리로 보인다.
    "projectileWidthTiles": "2.5",
}

# ──────────────────────────────────────────────────────────────────────────
# 프레임 번호 — **실측**. (라벨 띠 y0, y1, 번호 여섯의 x)
#
# ★ x 는 «그 번호를 물고 있는 열» 하나만 적는다 — 덩어리의 좌우 끝과 세로 범위는
#   :func:`erase_labels` 가 회색 마스크로 <b>다시 재서</b> 잡는다. 그래야 글꼴이
#   조금 달라져도 상자가 어긋나지 않는다.
# ⚠ 원화가 바뀌면 이 값을 다시 재야 한다 — `python Tools/sheet_probe.py Char_Asset_Legimia.png`
#   로 띠를 찾고 `--labels y0 y1` 로 번호를 센다.
# ──────────────────────────────────────────────────────────────────────────
LABELS = [
    (  9,  28, [149, 357, 575,  789,  986, 1203]),   # 대기
    (132, 150, [148, 355, 553,  767,  980, 1202]),   # 이동
    (234, 252, [150, 356, 571,  775,  987, 1202]),   # 근거리 공격
    (346, 376, [152, 360, 581,  804, 1012, 1203]),   # 사망
    (468, 487, [189, 394, 588,  818, 1037, 1232]),   # 스킬1 시전
    (596, 620, [191, 394, 588,  815, 1036, 1232]),   # 스킬1 효과
    (712, 730, [184, 379, 587,  812, 1023, 1226]),   # 종양 오브젝트
    (801, 823, [201, 398, 597,  819, 1040, 1229]),   # 종양 폭발
    (910, 930, [217, 405, 587,  803, 1023, 1203]),   # 회복 이펙트
]

# ──────────────────────────────────────────────────────────────────────────
# 시트 좌표 — **실측**. 장수는 적지 않는다(세는 쪽이 정본).
#   (폴더, y0, y1, x0, x1, kind)
#
# ★ ``x0`` 는 <b>제목 딱지 다음</b>이다(딱지가 x 14~193 까지 줄마다 다르게 뻗는다).
# ★ ``y1`` 은 구획선 잔재를 피해 잡았다 — 이동 줄의 y225·근접 줄의 y336 에 연회색
#   구획선이 1줄 남아 있어(`erase_box_borders` 가 못 지운 부분) 그대로 두면 폭 64px
#   짜리 가짜 칸이 생긴다(실측).
# ──────────────────────────────────────────────────────────────────────────
BODY_ROWS = [
    ("Idle",        (  6, 122, 140, 1450)),
    ("Move",        (133, 224, 140, 1450)),
    ("MeleeAttack", (235, 335, 140, 1490)),
    ("Death",       (347, 446, 140, 1450)),
]

#: 스킬1 — <b>두 줄을 이어 한 묶음</b>으로 굽는다 (맨 위 ★).
SKILL1_ROWS = [
    (469, 582, 180, 1450),   # 본체 시전 모션 6장
    (594, 698, 180, 1450),   # 본체 스킬 효과 6장
]

#: 이펙트 줄 — (폴더, y0, y1, x0, x1)
#:
#: ★★ 여기는 :func:`cells_by_clusters` 를 <b>쓰면 안 된다.</b> 그 함수는 «중앙값의 40%
#:   미만인 덩어리를 부스러기로 버리는데», 이 세 줄은 <b>그림이 커지는 연출</b>이라
#:   1번 프레임이 정말로 작다(종양 45px vs 5번 113px). 실제로 1번이 버려졌다.
#:   또 폭발 줄에는 <b>흩날리는 점</b>이 프레임마다 붙어 있어 «덩어리 = 칸» 이 아니다.
#: → 빈 열의 <b>가운데</b>로 가른다(:func:`cells_by_gaps`). 그러면 점들이 자기 칸에 남는다.
#:   ``min_len=12`` 는 6~15 구간의 가운데다(실측: 5 이하면 폭발이 7칸으로 갈리고
#:   16 이상이면 1번 칸이 사라진다).
FX_ROWS = [
    ("Skill1Projectile", (713, 781, 160, 1300)),   # 종양 오브젝트 5장 (6번 칸은 비어 있다)
    ("Skill1Fx",         (798, 886, 190, 1420)),   # 종양 폭발 6장
    ("Skill2Fx",         (907, 1004, 200, 1300)),  # 회복 이펙트 5장 (6번 칸은 비어 있다)
]

FX_GAP_MIN = 12

#: ★★ <b>원본이 왼쪽을 보고 있는 모션</b> — 맨 위 「방향」 절 참조.
SOURCE_FACES_LEFT = {"Move", "MeleeAttack"}

#: 좌우 방향이 없는 묶음 — 파일 이름에 Right/Left 를 안 붙인다(이펙트·투사체).
NO_DIRECTION = {"Skill1Projectile", "Skill1Fx", "Skill2Fx"}


def erase_labels(sheet):
    """
    프레임 번호를 <b>그림 마스크에서 지운다</b> (맨 위 ★★).

    번호가 있는 열을 씨앗으로 회색 덩어리의 좌우 끝을 찾고, 그 안에서 <b>세로 범위까지</b>
    재서 상자를 만든다. 상자 밖으로는 한 픽셀도 나가지 않으므로 그림을 먹을 길이 없다
    (1px 여유만 준다 — 안티에일리어싱이 상자 밖으로 새 나가기 때문).
    """
    gray, mask = sheet["gray"], sheet["mask"]
    n = wiped = 0
    for ly0, ly1, xs in LABELS:
        band = gray[ly0:ly1 + 1, :].any(axis=0)
        for x in xs:
            if not band[x]:
                raise SystemExit("⚠ 라벨 좌표가 어긋났습니다 (y %d~%d · x %d) — "
                                 "원화가 바뀌었으면 LABELS 를 다시 재세요." % (ly0, ly1, x))
            a = b = x
            while a > 0 and band[a - 1]:
                a -= 1
            while b < len(band) - 1 and band[b + 1]:
                b += 1
            rows_ = runs(gray[ly0:ly1 + 1, a:b + 1].any(axis=1), 1)
            y0, y1 = ly0 + rows_[0][0], ly0 + rows_[-1][1]
            wiped += int(mask[y0:y1 + 1, a:b + 1].sum())
            mask[max(0, y0 - 1):y1 + 2, max(0, a - 1):b + 2] = False
            n += 1
    print("  프레임 번호 %d개 지움 (%d px)" % (n, wiped))


def drop_shadow(sheet, boxes):
    """몸통 줄의 <b>발밑 그림자</b>를 지운다 — 이펙트 줄은 그 자체가 연출이라 안 건드린다."""
    shadow = np.zeros(sheet["mask"].shape, dtype=bool)
    for b in boxes:
        shadow |= shadow_in_box(sheet, b)
    sheet["mask"] &= ~shadow
    # ★ 그림자가 막고 있던 바깥 배경을 배경으로 편입한다 — 촉수 사이의 웅덩이가
    #   그림자로 바깥과 끊겨 있으면 «흰 천» 으로 남는다(바리올라와 같은 경우).
    return reflood_background(sheet, shadow)


def drop_pockets(sheet, boxes, label):
    """
    ★★ <b>갇힌 배경</b>을 지운다 (2026-08-21 · 유저 리포트: *"레기미아 인게임 스프라이트가
    누끼가 제대로 안따지고 흰색이 자꾸 보여"*).

    <b>왜 남았나</b> — 이 시트는 알파가 없는 <b>흰 배경 RGB</b>다. `background_mask` 는
    배경을 «시트 테두리에서 흘려 닿는 곳» 으로 정의하는데, 레기미아는 <b>촉수가 여러 개</b>
    라서 촉수와 촉수 사이의 흰 구역이 <b>바깥과 완전히 끊긴다</b>. 끊긴 배경은 배경으로
    안 잡히므로 <b>불투명한 흰 판때기</b>로 구워졌다(실측: 프레임마다 흰 화소가 불투명
    영역의 <b>3~5%</b> · 화면에서 어깨·다리 사이에 흰 천이 붙은 것처럼 보인다).

    ★ 119-6절이 시카리아·아루·카이론에서 겪은 <b>같은 사고</b>이고 도구도 그때 만들었다 —
      `enclosed_background` 가 «갇힌 덩어리 중 <b>테두리가 먹선</b>인 것» 만 되돌린다.
      그 조건이 있어서 <b>몸에 그려진 흰 하이라이트</b>(가슴의 흰 문양·눈)는 살아남는다.

    ⚠ <b>칸 단위로 돈다</b> — 시트 전체에서 갇힌 덩어리를 세면 수천 개가 나와 몇 분이 걸린다.
    ⚠ <b>지운 화소 수를 반드시 본다</b> — 프레임당 수백~수천이 정상이고, 수만이면 배경
      판정이 무너진 것이다(그때는 `ring_lum` 을 낮춘다).
    """
    total = 0
    for b in boxes:
        pocket = enclosed_background(sheet, b[2], b[3], b[0], b[1],
                                     min_area=POCKET_MIN_AREA,
                                     ring_lum=POCKET_RING_LUM)
        n = int(pocket.sum())
        if n:
            sheet["mask"] &= ~pocket
            sheet["bg_mask"] |= pocket
            total += n
    if total:
        print("      갇힌 배경 %6d px 지움 (%s)" % (total, label))
    return total


def bake(sheet, boxes):
    fr = [sharpen_rgba(crop_rgba(sheet, b), SHARPEN_AMOUNT, SHARPEN_RADIUS,
                       SHARPEN_THRESHOLD) for b in boxes]
    if SUPERSAMPLE != 1:
        fr = [resample_rgba(f, float(SUPERSAMPLE)) for f in fr]
    return fr


def write_group(images, name):
    folder = os.path.join(DST_ROOT, name)
    gone = clear_frames(folder)
    if gone:
        print("      ㈛ 예전 프레임 %d개 지움 (%s)" % (gone, name))
    kw = {"ppu": BAKE_PPU, "filter_mode": FILTER_BILINEAR}
    n = 0
    for i, img in enumerate(images):
        if name in NO_DIRECTION:
            write_png(img, folder, "Char_%s_%02d" % (name, i), **kw)
            n += 1
        else:
            flipped = img.transpose(Image.FLIP_LEFT_RIGHT)
            right, left = ((flipped, img) if name in SOURCE_FACES_LEFT
                           else (img, flipped))
            write_png(right, folder, "Char_%s_Right_%02d" % (name, i), **kw)
            write_png(left, folder, "Char_%s_Left_%02d" % (name, i), **kw)
            n += 2
    ensure_folder_meta(folder)
    return n


def body_boxes(sheet, y0, y1, x0, x1, name):
    """몸통 줄 한 줄의 그림 상자들 — 칸을 세고, 그림자를 지우고, 다시 잰다."""
    cells = cells_by_clusters(sheet["mask"], y0, y1, x0, x1)
    if not cells:
        raise SystemExit("⚠ %s: 칸을 하나도 못 찾았습니다 (y%d~%d)" % (name, y0, y1))
    rough = [b for b in boxes_for(sheet["mask"], cells, y0, y1) if b is not None]
    gained = drop_shadow(sheet, rough)
    if gained:
        print("      그림자가 막던 배경 %5d px 편입 (%s)" % (gained, name))
    # ★★ 촉수 사이에 갇힌 흰 배경을 지운다 (아래 drop_pockets 의 ★★).
    drop_pockets(sheet, rough, name)
    boxes = [b for b in boxes_for(sheet["mask"], cells, y0, y1) if b is not None]
    return cells, boxes


def build(sheet):
    made = 0

    for name, (y0, y1, x0, x1) in BODY_ROWS:
        cells, boxes = body_boxes(sheet, y0, y1, x0, x1, name)
        frames = bake(sheet, boxes)
        images, w, h = compose(frames, [body_anchor(f) for f in frames])
        made += write_group(images, name)
        print("  %-17s %3d x %3d · %2d장  %s  폭 %s"
              % (name, w, h, len(images),
                 "←" if name in SOURCE_FACES_LEFT else "→",
                 [e - s + 1 for s, e in cells]))

    # ★ 스킬1 — 두 줄을 이어 12장 (맨 위 ★). 캔버스는 12장을 한 번에 잡는다.
    s1_boxes, s1_widths = [], []
    for y0, y1, x0, x1 in SKILL1_ROWS:
        cells, boxes = body_boxes(sheet, y0, y1, x0, x1, "Skill1")
        s1_boxes += boxes
        s1_widths += [e - s + 1 for s, e in cells]
    frames = bake(sheet, s1_boxes)
    images, w, h = compose(frames, [body_anchor(f) for f in frames])
    made += write_group(images, "Skill1")
    print("  %-17s %3d x %3d · %2d장  →  폭 %s" % ("Skill1", w, h, len(images), s1_widths))

    for name, (y0, y1, x0, x1) in FX_ROWS:
        cells = cells_by_gaps(sheet["mask"], y0, y1, x0, x1, min_len=FX_GAP_MIN)
        if not cells:
            raise SystemExit("⚠ %s: 칸을 하나도 못 찾았습니다 (y%d~%d)" % (name, y0, y1))
        boxes = [b for b in boxes_for(sheet["mask"], cells, y0, y1) if b is not None]
        drop_pockets(sheet, boxes, name)
        boxes = [b for b in boxes_for(sheet["mask"], cells, y0, y1) if b is not None]
        frames = bake(sheet, boxes)
        images, w, h = compose(frames, [base_anchor(f) for f in frames])
        made += write_group(images, name)
        print("  %-17s %3d x %3d · %2d장     칸 %s"
              % (name, w, h, len(images), [(s, e) for s, e in cells]))

    return made


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print("[레기미아 모션 시트 분해]")

    sheet = load_sheet(SRC, box_borders=True)
    erase_labels(sheet)

    n = build(sheet)
    spec = write_skin_spec(DST_ROOT, SKIN_SPEC, "Tools/legimia_skin_build.py")
    ensure_folder_meta(DST_ROOT)
    ensure_folder_meta(os.path.dirname(DST_ROOT))
    print("  스킨 설정 %s (%d줄)" % (SKIN_SPEC_NAME, spec))
    print("  → 프레임 %d장" % n)
    print("  다음: 유니티 메뉴 LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기")


if __name__ == "__main__":
    main()
