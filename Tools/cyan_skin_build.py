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
| 상자 (y · <b>2026-08-22 실측</b>) | 폴더 | 무엇 |
|---|---|---|
| 434~510 | ``Projectile`` | 원거리 공격 투사체 — 낫 → 창 → 별 <b>6장</b>(뒤 두 칸은 점뿐이라 뺐다) |
| 525~625 | ``ImpactMagic`` | 마법 투사체/이펙트 — 구체가 커지며 사슬·별이 붙는다 <b>7장</b> |
| 630~736 | ``MeleeTravelFx`` | 근거리 공격 이펙트 — 초승달 3장 + 착탄 1장 |
| 746~850 | ``Skill1Fx`` | 스킬1 이펙트(회전 베기) — 소용돌이 → 고리 4단계 |
| 874~994 | ``HealFx`` | 회복 이펙트 — 바닥 고리 + 빛 기둥 <b>5장</b>(마지막은 올라가는 날개) |
| x 1395~ | ``Unused_Parts`` | 「기타 이펙트」 — 날개·사슬·깃털 <b>부품</b>이라 프레임이 아니다 |

★★ <b>이펙트 구획은 «반투명 흰 판» 위에 그려져 있다</b> — 실측 RGB (245,245,247) ·
  <b>알파 181~205</b>. 그 판이 전부 «그림» 으로 잡혀 프레임마다 <b>흰 사각형</b>이 함께
  구워지고 있었다(유저 리포트 «시안 이펙트 짤리는 문제» 의 정체). 아래
  :data:`PANEL_REGION` · :data:`LABEL_BOXES` 와 `skin_sheet.erase_panels` 참조.

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
    load_sheet, cells_by_span, cells_by_feet, boxes_dominant, boxes_for, crop_rgba,
    body_anchor, base_anchor, compose, write_png, ensure_folder_meta,
    clear_frames, erase_title_pills, erase_panels, drop_stray_parts, plant_feet,
)

SRC = os.path.join(VAULT, "리소스", "sprites", "Cyan_asset.png")

DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Cyan", "Char")

#: ★★ <b>제목 딱지 자리</b> — :func:`skin_sheet.erase_title_pills` 가 놓친 <b>상자 안쪽
#: 작은 라벨</b> 넷을 손으로 지운다. (y0, y1, x0, x1) · 전부 <b>실측</b>이다.
#:
#: <b>왜 자동으로 안 잡히나</b> — 이 넷은 알약 폭이 108~115px 로 :data:`skin_sheet.PILL_MIN_RUN`
#: (120)보다 <b>좁다</b>. 문턱을 100 으로 낮추면 「투사체/이펙트」 큰 제목(y379~412)과
#: 「원거리 공격 투사체」(y415~449)가 <b>세로로 이어져 한 덩어리(71px)</b> 가 되고,
#: :data:`skin_sheet.PILL_MAX_H`(46)에 걸려 <b>둘 다</b> 버려진다. 그래서 이 넷만 좌표로 적는다.
LABEL_BOXES = [
    (420, 443,  806,  920),   # 원거리 공격 투사체
    (631, 654,  806,  919),   # 근거리 공격 이펙트
    (869, 893,  806,  887),   # 회복 이펙트
    (421, 445, 1396, 1474),   # 기타 이펙트
]

#: ★ 반투명 판때기를 지울 구역 — 「분리된 이미지」 구획만. (y0, y1, x0, x1)
PANEL_REGION = (395, 1010, 795, 1535)

#: 몸통 줄 — (폴더, 좌y0, 좌y1, 우y0, 우y1, x0, x1, 장수).
#:
#: ★★★ <b>2026-08-22 — 전부 다시 쟀다.</b> 옛 값은 «딱지를 피하려고 밴드를 안쪽으로
#:   좁힌» 것이라 <b>그림도 같이 잘렸다</b>. 딱지를 지우게 된 뒤로는 좁힐 이유가 없다.
#:   경계는 세로 잉크 프로파일의 <b>골</b>로 잡았다(실측):
#:
#:     왼쪽 단 밴드  31~206 · 244~326 · 336~414 · 441~531 · 537~639 · 658~827 · 829~1011
#:     그 안의 골    120(대기 좌/우) · 741(원거리 좌/우) · 928(마법 좌/우)
#:     오른쪽 단     0~190(골 114) · 220~380(골 296)
#:
#: | 줄 | 옛 밴드 | <b>실측</b> | 잃고 있던 것 |
#: |---|---|---|---|
#: | 마법 좌 | 848~930 | <b>829~927</b> | <b>위 19px</b> — 낫과 후광 |
#: | 근거리 우 | 536~625 | <b>537~639</b> | <b>아래 14px</b> — 발과 망토 |
#: | 원거리 우 | 742~820 | <b>742~827</b> | 아래 7px |
#: | 회복 좌 | 40~105 | <b>0~113</b> | <b>위 40px</b> — 머리 위 회복 반짝임 |
#: | 회복 우 | 115~180 | <b>115~190</b> | 아래 10px |
#: | 스킬1 우 | 300~375 | <b>297~380</b> | 위 3 · 아래 5 |
PAIR_ROWS = [
    # ★★ <b>좌 8장 · 우 7장</b> — 원화가 그렇게 그려져 있다(2026-08-22 실측).
    #   발밑 덩어리로 세면 왼쪽 8개(x73·144·201·293·388·475·577·670), 오른쪽 7개
    #   (x45·151·255·359·466·565·665 · 폭이 52~56px 로 <b>전부 같다</b> = 합쳐진 것이 없다).
    #   ⚠ 옛 코드는 양쪽 다 8로 두고 «폭 ÷ 8» 로 갈랐다 — 오른쪽이 한 칸씩 밀려
    #     <b>낫이 반쪽으로 잘려</b> 옆 프레임에 붙었다(테두리 검사 좌우 63~84%).
    ("Idle",          31,  119,  120,  206,   40,  790, (8, 7)),
    ("Move",         244,  326,  336,  414,   40,  790, 9),
    ("MeleeAttack",  441,  531,  537,  639,   40,  790, 7),
    ("RangedAttack", 658,  740,  742,  827,   40,  790, 5),
    ("MagicAttack",  829,  927,  929, 1011,   40,  790, 8),
    # 오른쪽 단 — 회복과 스킬.
    ("Heal",           0,  113,  115,  190,  795, 1535, 5),
    ("Skill1",       220,  295,  297,  380,  795, 1535, 6),
]

#: 이펙트 상자 — (폴더, y0, y1, x0, x1, 칸 경계). 방향이 없다.
#:
#: ★★★ <b>2026-08-22 — 여기가 «시안 이펙트 짤림» 의 본체였다.</b> 두 가지가 겹쳐 있었다.
#:
#: <b>① 「분리된 이미지」 구획이 «반투명 흰 판» 위에 그려져 있다.</b> 실측 RGB (245,245,247) ·
#:   <b>알파 181~205</b>. `ALPHA_INK_MIN` 이 8 이라 그 판이 <b>전부 «그림» 으로</b> 잡혀서,
#:   프레임마다 <b>불투명한 흰 사각형</b>이 함께 구워졌다 — 회복 이펙트는 거의 흰 판이고
#:   이펙트가 그 안에 묻혀 «짤린» 것처럼 보였다(구워진 PNG 로 확인).
#:   → :func:`skin_sheet.erase_panels` + :func:`skin_sheet.sweep_panel_residue` 로 지운다.
#:
#: <b>② 밴드가 라벨 아래로 좁혀져 있었다.</b> 판을 지우고 나면 그럴 이유가 없다.
#:   실측 밴드(판·라벨을 지운 뒤):
#:
#:     투사체 434~510 · 마법 525~625 · 근거리 630~736 · 스킬1 746~850 · 회복 874~994
#:
#: ★ 칸은 <b>«폭 ÷ 장수» 를 쓰지 않는다</b> — 부품 간격이 고르지 않다(투사체 상자 실측
#:   간격 29·9·8·5·12·12·13). :func:`skin_sheet.cells_by_clusters` 로 덩어리를 잡고
#:   장수에 맞춰 합친 결과를 <b>경계로 박았다</b>. 장수는 판을 지운 그림을 눈으로 세었다.
FX_ROWS = [
    # 낫 → 창 → 별. ⚠ 상자에는 여덟 칸이 있는데 <b>뒤 두 칸은 점만 남은 잔광</b>이다.
    #   투사체 프레임은 <b>날아가는 동안 되풀이</b>되므로 그대로 쓰면 탄환이 «비행 중에
    #   사라졌다 나타난다». 그래서 형체가 있는 <b>앞 여섯 칸</b>만 쓴다.
    ("Projectile",     434,  510,  820, 1280,
     [827, 903, 989, 1069, 1157, 1220, 1275]),
    # 구체가 커지며 사슬·별이 붙는다. 일곱 칸.
    ("ImpactMagic",    525,  625,  830, 1370,
     [830, 872, 927, 987, 1055, 1157, 1257, 1367]),
    # 초승달 셋 + 착탄 하나.
    ("MeleeTravelFx",  630,  736,  811, 1350,
     [811, 920, 1083, 1226, 1345]),
    # 소용돌이 → 고리 네 단계.
    ("Skill1Fx",       746,  850,  818, 1375,
     [818, 951, 1093, 1245, 1372]),
    # 바닥 고리 + 빛 기둥 다섯 단계(마지막은 올라가는 날개).
    # ⚠ 경계 넷은 <b>눈으로 잡았다</b> — 1·2번은 고리 글로우가 붙어 덩어리로는 하나이고
    #   (실측 x817~945), 4·5번은 덩어리 경계(1312)가 <b>날개를 가로질러</b> 있었다.
    ("HealFx",         874,  994,  808, 1360,
     [808, 876, 962, 1106, 1246, 1360]),
    # 「기타 이펙트」 — 날개·사슬·깃털 부품. 한 장으로 남긴다(프레임이 아니다).
    ("Unused_Parts",   450, 1000, 1395, 1535, None),
]

SKIN_SPEC = {
    "skinAssetName": "Skin_Cyan",
    "outputFolder": "Assets/_Project/Resources/Skins",
    "displayName": "시안",
    "framesPerSecond": "10",
    "attackFramesPerSecond": "12",
    # 날아가는 낫 → 별. 낫 한 자루가 캐릭터 몸통만 하다.
    "projectileWidthTiles": "1.2",
    # ★★ <b>이펙트 크기를 «타일» 로 못박는다</b> (2026-08-22 · 유저 지시 *"확실하게
    #   이펙트가 표현되도록"*). 비워 두면(<b>0</b>) 배율이 1 이 되어 <b>구워진 픽셀 크기가
    #   그대로 화면 크기</b>가 된다 — 그러면 원화를 다시 자를 때마다(이번처럼) 연출이
    #   커졌다 작아졌다 한다. 이 프로젝트의 규칙은 «몇 타일로 그릴지만 적고 배율은 코드가
    #   계산한다»(`CombatProjectileFx.ScaleForWidthTiles`)이고, 그 규칙을 지금 적용한다.
    # ★ 값의 근거 — 몸통이 1.4~1.8 타일이다(`contentSizeTiles`). 참격은 몸보다 조금 커야
    #   «베었다» 로 읽히고, 착탄은 몸 정도면 «맞았다» 로 읽힌다. 카이론·아루의
    #   `impactWidthTiles`(1.4~1.6)와 같은 대역이다.
    "meleeTravelWidthTiles": "2.0",
    "impactWidthTiles": "1.5",
}


def pick_cells(mask, y0, y1, x0, x1, count, tag):
    """
    ★★ <b>칸은 «발밑» 으로 먼저 갈라 본다</b> (2026-08-22 · 유저 리포트: *"모션 동작
    하나하나를 잘 구분해서 배치하도록 해"*).

    이 시트는 <b>프레임 간격이 고르지 않다</b>. 대기 왼쪽 줄 실측: 진짜 빈 열이
    x137·265·361·546·644 <b>다섯 군데</b>뿐인데 프레임은 여덟이고, 몸통 중심 간격이
    62~110px 로 흔들린다. 그래서 «폭 ÷ 장수»(:func:`skin_sheet.cells_by_span`)는
    <b>최대 45px 까지 어긋난다</b> — 어긋난 칸이 낫을 반쪽으로 자르고, 그 반쪽이
    옆 프레임에 남아 «전 동작이 끼어드는» 것으로 보인다.

    ★ 낫은 <b>공중</b>에 있고 발은 <b>땅</b>에 따로 놓여 있으므로 발밑으로 가르면 맞는다
      (:func:`skin_sheet.cells_by_feet`). 실측: 대기 왼쪽이 139·198·270·362·452·549·648 로
      <b>진짜 빈 열 다섯 군데와 모두 일치</b>하고 빈 열이 없는 두 자리도 몸통 사이에 든다.
    ⚠ 다만 <b>모든 줄에 듣지는 않는다</b> — 망토가 땅까지 닿아 발이 이어지는 줄이 있다
      (근거리 오른쪽은 2칸으로 나온다). 그때는 장수가 안 맞으므로 <b>그것을 신호로</b>
      «폭 ÷ 장수» 로 되돌린다. 지어내지 않고 <b>둘 중 맞는 쪽을 고른다</b>.
    """
    try:
        feet = [tuple(c) for c in cells_by_feet(mask, y0, y1, x0, x1, count=count)]
    except SystemExit:
        feet = []
    if len(feet) == count:
        return feet, "발밑"
    return [tuple(c) for c in cells_by_span(mask, y0, y1, x0, x1, count)], "폭÷장수"


def build_pair(sheet, name, ly0, ly1, ry0, ry1, x0, x1, count):
    """``count`` 는 정수이거나 <b>(좌, 우)</b> 두 값이다 — 원화의 장수가 다를 수 있다."""
    """
    좌·우 두 줄을 <b>한 캔버스</b>에 얹고 Left/Right 로 굽는다 (맨 위 ⚠⚠).

    ★ 순서가 중요하다 — 좌 먼저, 우 나중에 담고 <b>compose 한 뒤</b> 반으로 가른다.
      그래야 두 방향이 같은 폭·높이·피벗을 갖는다.
    """
    frames, sides, modes = [], [], []
    stray = [0]
    counts = count if isinstance(count, (tuple, list)) else (count, count)
    for (y0, y1, side), want in zip(((ly0, ly1, "Left"), (ry0, ry1, "Right")), counts):
        cells, how = pick_cells(sheet["mask"], y0, y1, x0, x1, want, "%s %s" % (name, side))
        modes.append("%s %s" % (side[0], how))
        boxes = [b for b in boxes_dominant(sheet["mask"], cells, y0, y1, min_ink_ratio=0.06, name=name)
                 if b is not None]
        if len(boxes) != want:
            print("  [!] %s %s: 칸 %d개 기대했는데 %d개" % (name, side, want, len(boxes)))
        for b in boxes:
            # ★★ <b>옆 프레임의 낫·망토를 뗀다</b> (2026-08-22 · 유저 리포트: *"이동 모션
            #   사이 사이에 전 동작 모션과 함께 짤려 들어가서 어색해지는 부분들"*).
            #   이 시트는 프레임이 겹쳐 그려져 <b>앞 프레임의 낫이 다음 칸까지</b> 들어온다.
            #   칸 경계로는 그것을 «반쪽으로 잘라» 남기게 되므로(테두리 검사에서 좌우
            #   47~84% 가 단면으로 나왔다) <b>덩어리째 떼어낸다</b>.
            f, gone = drop_stray_parts(crop_rgba(sheet, b))
            if gone:
                stray[0] += gone
            frames.append(f)
            sides.append(side)

    if not frames:
        raise SystemExit("[!] %s: 그림을 못 찾았습니다" % name)

    # ★★ <b>발을 피벗에 맞춘다</b> — 좌·우를 <b>따로</b> 민다(그림이 서로 다르므로).
    #   묶음 안의 다리 놀림은 그대로 두고 «모션이 바뀔 때 옆으로 미끄러지는 것» 만 없앤다
    #   (:func:`skin_sheet.plant_feet` 의 ★★). 실측: 시안은 대기 −13.5px · 회복 +5.5px 라
    #   대기에서 회복으로 바뀌면 <b>19px 미끄러졌다</b>.
    anchors = [body_anchor(f) for f in frames]
    shifts = []
    for want_side in ("Left", "Right"):
        idx = [i for i, sd in enumerate(sides) if sd == want_side]
        if not idx:
            continue
        sub, sh = plant_feet([frames[i] for i in idx], [anchors[i] for i in idx])
        for k, i in enumerate(idx):
            anchors[i] = sub[k]
        shifts.append("%s%+.0f" % (want_side[0], sh))
    images, w, h = compose(frames, anchors)

    folder = os.path.join(DST_ROOT, name)
    gone = clear_frames(folder)
    if gone:
        print("      (i) 예전 프레임 %d개 지움 (%s)" % (gone, name))
    idx = {"Left": 0, "Right": 0}
    for img, side in zip(images, sides):
        write_png(img, folder, "Char_%s_%s_%02d" % (name, side, idx[side]))
        idx[side] += 1
    ensure_folder_meta(folder)
    print("  %-14s %3d x %3d · 좌 %d장 / 우 %d장  (시트의 두 줄을 그대로 · 미러 없음)%s"
          % (name, w, h, idx["Left"], idx["Right"],
             "  칸 %s · 피벗 %s%s" % ("/".join(modes), "/".join(shifts),
                             " · 옆 조각 %d px 떼어냄" % stray[0] if stray[0] else "")))
    return len(images)


def build_fx(sheet, name, y0, y1, x0, x1, bounds):
    """이펙트 상자 하나. ``bounds`` 가 ``None`` 이면 상자 전체를 한 장으로 굽는다."""
    if bounds is None:
        cells = [(x0, x1)]
    else:
        cells = [(bounds[i], bounds[i + 1] - 1) for i in range(len(bounds) - 1)]
    boxes = [b for b in boxes_for(sheet["mask"], cells, y0, y1, name=name) if b is not None]
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

    # ★ 순서가 중요하다: <b>딱지 → 판때기 → 라벨</b>. 판때기 판정은 «가장 많은 색» 을
    #   재므로 딱지(검은 알약)가 먼저 빠져야 흔들리지 않는다.
    erase_title_pills(sheet)
    erase_panels(sheet, passes=6, region=PANEL_REGION, alpha_max=245, sweep_alpha=235)
    for y0, y1, x0, x1 in LABEL_BOXES:
        n = int(sheet["mask"][y0:y1 + 1, x0:x1 + 1].sum())
        sheet["mask"][y0:y1 + 1, x0:x1 + 1] = False
        print("  상자 안 라벨 지움 y%d~%d x%d~%d · %d px" % (y0, y1, x0, x1, n))

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
