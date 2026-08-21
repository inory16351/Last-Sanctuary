# -*- coding: utf-8 -*-
"""시안(9014 · 「여명의 수확자」) 모션 시트 → 프레임 분해 (2026-08-21).

원본 — **한 장**
================
``<볼트>/리소스/sprites/Cyan_asset.png`` (1536x1024 · RGBA)

★★ <b>이 시트만 «좌/우» 가 따로 그려져 있다</b>
-----------------------------------------------
다른 열넷은 <b>오른쪽만</b> 그려져 있어 왼쪽은 코드가 뒤집어 만든다. 시안은 줄마다
``좌`` · ``우`` 딱지가 붙은 <b>두 줄</b>이고 그림이 실제로 다르다(낫을 든 손·망토가 겹치는
순서가 반대다). 그래서 <b>미러를 쓰지 않고 시트의 두 줄을 그대로 Left/Right 로</b> 굽는다.

⚠⚠ <b>두 줄을 «같은 캔버스» 에 얹어야 한다.</b> 좌·우를 따로
:func:`skin_sheet.compose` 하면 캔버스 크기와 피벗이 달라져서, 유닛이 <b>방향을 바꾸는
순간 옆으로 튀고 크기가 변한다</b>(이 프로젝트가 113-4절에서 잡은 그 사고와 같은 것).
→ :func:`build_pair` 가 좌·우 프레임을 <b>한 번에</b> compose 하고 나서 반으로 가른다.

★★ <b>y 는 «라벨이 없는 x 창» 으로 갈랐다</b>
---------------------------------------------
제목 딱지(검은 알약)가 <b>x 0~440 에만</b> 있고 프레임은 x 20 부터 시작해서 <b>딱지와
프레임의 x 가 겹친다</b>. 그래서 자동 밴드 탐지를 그냥 돌리면 딱지가 줄에 붙는다
(실측: ``x60~175`` 로 재면 「대기 딱지 + 대기 좌」 가 한 밴드 ``y3~117`` 로 잡힌다).
→ <b>딱지가 없는 창</b>(``x200~760``)으로 세로 밴드를 잡고, 격자 이미지로 확인했다.

무엇이 어디로 가나
==================
| 시트 줄 (좌 y / 우 y) | 폴더 | 장수 |
|---|---|---|
| 대기 31~118 / 122~205 | ``Idle`` | 8 |
| 이동 244~327 / 334~416 | ``Move`` | 9 |
| 근거리 공격 417~531 / 536~650 | ``MeleeAttack`` | 7 |
| 원거리 공격 657~740 / 742~820 | ``RangedAttack`` | 5 |
| 마법 848~930 / 933~1012 | ``MagicAttack`` | 8 |
| 회복 40~105 / 115~180 <b>(오른쪽 단)</b> | ``Heal`` | 5 |
| 스킬 「회전 베기」 226~296 / 300~375 <b>(오른쪽 단)</b> | ``Skill1`` | 6 |

이펙트 — 오른쪽 단의 <b>「분리된 이미지」</b> 상자들
---------------------------------------------------
| 상자 (y) | 폴더 | 무엇 |
|---|---|---|
| 440~505 | ``Projectile`` | 원거리 공격 투사체 — 낫 → 별 8단계 |
| 552~615 | ``ImpactMagic`` | 마법 투사체/이펙트 — 구체가 커지며 사슬·별이 붙는다 |
| 650~735 | ``MeleeTravelFx`` | 근거리 공격 이펙트 — 초승달 3장 + 착탄 1장 |
| 760~852 | ``Skill1Fx`` | 스킬1 이펙트(회전 베기) — 소용돌이 4단계 |
| 890~988 | ``HealFx`` | 회복 이펙트 — 바닥 고리 + 빛 기둥 6단계 |
| x 1390~ | ``Unused_Parts`` | 「기타 이펙트」 — 날개·사슬·깃털 <b>부품</b>이라 프레임이 아니다 |

⚠ ``Skill1`` 은 표의 <b>``skill_02`` (80041 「사신의 낫」)</b> 다 — 시안의 세 스킬 중
  <b>몸 동작이 있는 것이 이것 하나</b>이기 때문이다(80040 「영혼 흡수」는 상시 수집,
  80042 「한계 돌파」는 상시 성장이라 모션이 없다). 슬롯 번호는 <b>코드가 정한다</b>
  (`CharacterPassives` 가 스킬 종류별로 `PlaySkillMotion(n)` 을 고른다) — 표의 칸 순서가
  아니라 «어느 원화를 쓸지» 로 맞추는 것이 이 프로젝트의 방식이다(카이론 119-9절).

사용법:  python Tools/cyan_skin_build.py
다음:    유니티 메뉴 **LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기**
        그 다음 `python Tools/measure_skin_tiles.py`
"""

import os
import sys

from vault_path import VAULT, PROJECT

from skin_sheet import (  # noqa: F401
    PPU, SKIN_SPEC_NAME, write_skin_spec,
    load_sheet, cells_by_span, boxes_dominant, boxes_for, crop_rgba,
    body_anchor, base_anchor, compose, write_png, ensure_folder_meta,
    clear_frames,
)

SRC = os.path.join(VAULT, "리소스", "sprites", "Cyan_asset.png")

DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Cyan", "Char")

#: 몸통 줄 — (폴더, 좌y0, 좌y1, 우y0, 우y1, x0, x1, 장수).
#: ★ y 는 «딱지가 없는 창»(x200~760)의 세로 밴드 + 격자 이미지 확인으로 잡은 <b>실측</b>이다.
PAIR_ROWS = [
    ("Idle",          31,  118,  122,  205,   50,  765, 8),
    ("Move",         244,  327,  334,  416,   50,  765, 9),
    # 우 줄의 y1 을 625 로 끊었다 — 650 이면 아래의 「원거리 공격 모션」 <b>딱지 글자</b>가
    # 프레임에 섞여 들어간다(실제로 그렇게 구워졌다 · 딱지는 y 633~655).
    ("MeleeAttack",  417,  531,  536,  625,   50,  765, 7),
    ("RangedAttack", 657,  740,  742,  820,   50,  765, 5),
    ("MagicAttack",  848,  930,  933, 1012,   50,  765, 8),
    # 오른쪽 단 — 회복과 스킬은 x 800~1520 에 있다.
    ("Heal",          40,  105,  115,  180,  830, 1520, 5),
    ("Skill1",       226,  296,  300,  375,  830, 1520, 6),
]

#: 이펙트 상자 — (폴더, y0, y1, x0, x1, 장수). 방향이 없다.
FX_ROWS = [
    # ⚠ y0 은 <b>상자 안쪽 딱지 아래</b>다 — 딱지(예: 「원거리 공격 투사체」 y424~439)를
    #   넣으면 글자가 프레임에 섞인다(실제로 그렇게 구워졌다).
    ("Projectile",     445,  505,  810, 1185, 8),
    ("ImpactMagic",    555,  618,  810, 1385, 8),
    ("MeleeTravelFx",  656,  738,  810, 1180, 4),
    ("Skill1Fx",       768,  855,  810, 1180, 4),
    ("HealFx",         895,  990,  810, 1180, 6),
    # 「기타 이펙트」 — 날개·사슬·깃털 부품. 한 장으로 남긴다(프레임이 아니다).
    ("Unused_Parts",   520, 1005, 1390, 1536, 1),
]

SKIN_SPEC = {
    "skinAssetName": "Skin_Cyan",
    "outputFolder": "Assets/_Project/Resources/Skins",
    "displayName": "시안",
    "framesPerSecond": "10",
    "attackFramesPerSecond": "12",
    # 날아가는 낫 → 별. 낫 한 자루가 캐릭터 몸통만 하다.
    "projectileWidthTiles": "1.2",
}


def build_pair(sheet, name, ly0, ly1, ry0, ry1, x0, x1, count):
    """
    좌·우 두 줄을 <b>한 캔버스</b>에 얹고 Left/Right 로 굽는다 (맨 위 ⚠⚠).

    ★ 순서가 중요하다 — 좌 먼저, 우 나중에 담고 <b>compose 한 뒤</b> 반으로 가른다.
      그래야 두 방향이 같은 폭·높이·피벗을 갖는다.
    """
    frames, sides = [], []
    for y0, y1, side in ((ly0, ly1, "Left"), (ry0, ry1, "Right")):
        cells = cells_by_span(sheet["mask"], y0, y1, x0, x1, count)
        boxes = [b for b in boxes_dominant(sheet["mask"], cells, y0, y1, min_ink_ratio=0.06)
                 if b is not None]
        if len(boxes) != count:
            print("  [!] %s %s: 칸 %d개 기대했는데 %d개" % (name, side, count, len(boxes)))
        for b in boxes:
            frames.append(crop_rgba(sheet, b))
            sides.append(side)

    if not frames:
        raise SystemExit("[!] %s: 그림을 못 찾았습니다" % name)

    images, w, h = compose(frames, [body_anchor(f) for f in frames])

    folder = os.path.join(DST_ROOT, name)
    gone = clear_frames(folder)
    if gone:
        print("      (i) 예전 프레임 %d개 지움 (%s)" % (gone, name))
    idx = {"Left": 0, "Right": 0}
    for img, side in zip(images, sides):
        write_png(img, folder, "Char_%s_%s_%02d" % (name, side, idx[side]))
        idx[side] += 1
    ensure_folder_meta(folder)
    print("  %-14s %3d x %3d · 좌 %d장 / 우 %d장  (시트의 두 줄을 그대로 · 미러 없음)"
          % (name, w, h, idx["Left"], idx["Right"]))
    return len(images)


def build_fx(sheet, name, y0, y1, x0, x1, count):
    cells = cells_by_span(sheet["mask"], y0, y1, x0, x1, count)
    boxes = [b for b in boxes_for(sheet["mask"], cells, y0, y1) if b is not None]
    if not boxes:
        print("  [!] %s: 그림을 못 찾았습니다 (y%d~%d) — 건너뜁니다" % (name, y0, y1))
        return 0
    frames = [crop_rgba(sheet, b) for b in boxes]
    images, w, h = compose(frames, [base_anchor(f) for f in frames])

    folder = os.path.join(DST_ROOT, name)
    gone = clear_frames(folder)
    if gone:
        print("      (i) 예전 프레임 %d개 지움 (%s)" % (gone, name))
    for i, img in enumerate(images):
        write_png(img, folder, "Char_%s_%02d" % (name, i))
    ensure_folder_meta(folder)
    print("  %-14s %3d x %3d · %2d장" % (name, w, h, len(images)))
    return len(images)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print("[시안 모션 시트 분해]")
    # ★★ 상자 테두리를 <b>배경 판정보다 먼저</b> 지운다 — 이펙트가 «둥근 모서리
    #   흰 상자» 안에 들어 있어 그 테두리가 프레임에 딸려 온다.
    sheet = load_sheet(SRC, box_borders=True)

    made = 0
    for row in PAIR_ROWS:
        made += build_pair(sheet, *row)
    for row in FX_ROWS:
        made += build_fx(sheet, *row)

    spec = write_skin_spec(DST_ROOT, SKIN_SPEC, "Tools/cyan_skin_build.py")
    ensure_folder_meta(DST_ROOT)
    ensure_folder_meta(os.path.dirname(DST_ROOT))
    print("  스킨 설정 %s (%d줄)" % (SKIN_SPEC_NAME, spec))
    print("  -> 프레임 %d장" % made)
    print("  다음: 유니티 메뉴 LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기")


if __name__ == "__main__":
    main()
