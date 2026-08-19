# -*- coding: utf-8 -*-
"""시그리드(캐릭터 9006) 모션 시트 → 프레임 분해 (2026-08-19).

원본 — **두 장이고, 이번엔 정말로 둘 다 필요하다**
--------------------------------------------------
``<볼트>/리소스/asset/char_asset/Sigrid_asset_01.png``  (1536x1024)
``<볼트>/리소스/asset/char_asset/Sigrid_asset_02.png``  (1536x1024)

엘린은 ``_02`` 가 모든 줄에서 이겨서 한 장만 읽었다. 시그리드는 **두 장이 서로 없는 줄을
갖고 있다** — 라린길(115절)처럼 **줄 단위로** 고른다:

| 줄 | ``_01`` | ``_02`` | 쓰는 쪽 | 왜 |
|---|---|---|---|---|
| 대기 | 8칸 | 8칸 | **02** | 라벨이 깨끗하게 8개로 잡힌다(01 은 그림이 라벨 줄을 침범) |
| 이동 | 10칸 | 8칸 | **02** | 01 이 두 장 많지만 라벨이 11개로 잡혀 칸을 못 가른다 |
| 근거리 공격 | 8칸 | 8칸 | **02** | 같은 이유 (01 라벨 12개 오검출) |
| **원거리 공격** | **7칸** | 없음 | **01** | ``_02`` 에 이 줄이 아예 없다 |
| **원거리 투사체** | **8칸** | 없음 | **01** | 없으면 시그리드가 기본 회색 화살을 쏜다 |
| 이동 이펙트 | 없음 | **8칸** | **02** | ``_01`` 에 없다 |
| 근거리 이펙트 | 없음 | **8칸** | **02** | ``_01`` 에 없다 |
| 스킬 1~3 모션 | 7칸 | 7칸 | **02** | 01 라벨 오검출 |
| 스킬 1~3 이펙트 | 8칸 | 7칸 | **02** | 01 이 한 장 많지만 **한 시트로 묶는 편**이 낫다(아래 ⚠) |

⚠ 스킬 이펙트만 ``_01``(8장)을 쓸 수도 있었다. 안 그런 이유는 **모션과 이펙트가 같은
판본이어야 프레임 수가 맞아떨어지기 때문**이다 — 모션 7장에 이펙트 8장을 얹으면 마지막
한 장이 모션이 끝난 뒤에 혼자 남는다. 라린길에서는 이펙트가 **스킬 상자에 따로 깔려**
모션과 무관했기 때문에 판본을 섞어도 됐다(115절). 여기서는 같이 재생된다.

★ 시그리드는 **투사체를 쓴다** — 엘린과 반대다
----------------------------------------------
``_01`` 맨 위에 「원거리 투사체 (Ranged Projectile)」 8장이 있다. **점 → 별 → 큰 폭발**로
자라는 그림이라, 분비형 암세포의 침처럼 **비행 시간에 고르게 펼치면 목표에 닿는 순간
저절로 터져 사라진다**(진행상황 29-9절과 같은 성질). 그래서:

  · ``projectileFrames`` ← 투사체 8장
  · ``impactFrames``     ← **비운다.** 투사체 마지막 장이 이미 폭발이라 겹치면 두 번 터진다.
  · ``groundImpactOnly`` ← **끈다.** 엘린과 달리 날아가는 그림이 실재한다.

표(``first_Stat``)의 시그리드도 ``ranged_atk 8`` 이 최고치라 **원거리 유형**으로 판정된다
(``CharacterRole.Resolve``) — 시트와 표가 같은 이야기를 한다.

시트 구조 — **박스 테두리로 줄이 나뉜다** (엘린 시트와 다른 점)
---------------------------------------------------------------
시그리드 시트는 각 구획에 **연회색 사각 테두리**가 그려져 있다(채도 ≤12 · 광도 150~245).
그래서 ① 잉크 밴드가 테두리로 이어져 한 덩어리가 되고 ② 세로 테두리가 칸 경계처럼 보인다.
:func:`skin_sheet.load_sheet` 가 테두리를 잉크에서 빼주지 않으므로, 아래 좌표는
**테두리를 빼고 실측한 값**이다(구획 경계는 ``_02`` 기준 y196·364·538·716,
세로는 y538~880 구간에서 x470·993).

⚠ **칸 가르기는 라벨 중심으로 한다**(:func:`skin_sheet.cells_by_labels`).
  시그리드의 지팡이가 옆 칸까지 뻗어서 빈 열이 없다 — ``cells_by_gaps`` 로는 8칸이
  5칸으로 붙는다(실측: 대기 0칸 · 근거리 5칸 · 스킬 5칸).

무엇이 어디로 가나 (배선은 `Editor/CharacterSkinBuilder.cs` 가 한다)
--------------------------------------------------------------------
| 폴더 | 장수 | 스킨 칸 |
|---|---|---|
| ``Char/Idle`` | 8 | ``idleRight`` · ``idleLeft`` |
| ``Char/Walk`` | 8 | ``walkRight`` · ``walkLeft`` |
| ``Char/MeleeAttack`` | 8 | ``attackRight`` · ``attackLeft`` |
| ``Char/RangedAttack`` | 7 | ``rangedRight`` · ``rangedLeft`` |
| ``Char/Projectile`` | 8 | ``projectileFrames`` |
| ``Char/Unused_MoveFx`` | 8 | ⚠ 미배선 — 「이동 이펙트」 칸이 없다 |
| ``Char/Unused_MeleeFx`` | 8 | ⚠ 미배선 — 「평타 이펙트」 칸이 없다(라린길과 같은 이유) |
| ``Char/Unused_Skill1`` ~ ``3`` | 7씩 | ⚠ 미배선 — 아래 ★ |
| ``Char/Unused_Skill1Fx`` ~ ``3Fx`` | 7씩 | ⚠ 미배선 — 아래 ★ |

★ **스킬 모션 여섯 줄을 배선하지 않는 이유** — 시그리드의 스킬 셋은 표에서
**전부 패시브**다(80016 가학증 · 80017 고통의 기쁨 · 80018 통제할 수 없는 쾌락).
패시브는 「시전」이 없어서 재생 시점이 없고, ``CharacterSkinSO`` 의 ``skill1/2`` 칸은
**보스 스킬 시전용**(``BossSkillCaster``)이라 캐릭터는 그 경로를 타지 않는다. 게다가 칸이
**둘뿐**인데 원화는 셋이다. 그림이 나빠서가 아니라 **받을 자리가 없어서** 남긴다 —
패시브에 연출을 붙이는 날 폴더 이름의 ``Unused_`` 만 떼면 된다.

⚠ 시트의 스킬 이름(섭취 / 고통 선사 / 사디즘)과 **표의 스킬 이름이 다르다.**
  유저 확정(2026-08-19): *"스킬 이름은 테이블 우선"* → 표(가학증 / 고통의 기쁨 /
  통제할 수 없는 쾌락)가 정본이다. 그래서 폴더 이름은 시트 제목이 아니라 **슬롯 번호**
  (``Skill1``·``Skill2``·``Skill3``)로 둔다 — 이름을 박아두면 표를 고칠 때 어긋난다.

크기 — **다른 캐릭터와 같게 나온다**
------------------------------------
유저 지시(2026-08-19): *"다른 캐릭터랑 크기 맞춰서 만들어"*. 크기는 이 스크립트가 정하지
않는다 — 씬의 ``Character_Template.renderHeightTiles = 2.15`` 가 스킨 실측값
(``contentSizeTiles``)으로 나눠 **전원을 2.15타일 키로** 그린다. 그래서 여기서는
:func:`report_scale` 로 «한 배율로 그려진 시트인지»만 확인하고 정규화는 하지 않는다
(엘린과 같은 판단 — 이유는 그쪽 주석).

사용법:  python Tools/sigrid_skin_build.py
다음:    유니티 메뉴 **LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기**
        그 다음 `python Tools/measure_skin_tiles.py` (contentSizeTiles 실측)
"""

import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT

from skin_sheet import (  # noqa: F401
    PPU, BG_TOL, ALPHA_HI, RING_MIN_ALPHA,
    SKIN_SPEC_NAME, write_skin_spec,
    guid_for, load_sheet, label_count, cells_by_labels, cells_by_gaps,
    boxes_for, crop_rgba, body_anchor, base_anchor, compose,
    write_png, ensure_folder_meta, shadow_in_box, enclosed_background,
)

ART = os.path.join(VAULT, "리소스", "asset", "char_asset")
SRC01 = os.path.join(ART, "Sigrid_asset_01.png")
SRC02 = os.path.join(ART, "Sigrid_asset_02.png")

DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Sigrid", "Char")

#: ★ 스킨 에셋의 «값» 칸 (원화만 봐서는 알 수 없는 것).
SKIN_SPEC = {
    "skinAssetName": "Skin_Sigrid",
    "displayName": "시그리드",
    "framesPerSecond": "10",
    "attackFramesPerSecond": "14",
    # ⚠ 엘린과 반대다 — 시그리드는 날아가는 투사체 원화가 실재한다(맨 위 ★).
    "groundImpactOnly": "0",
    # 투사체를 가로 몇 타일로 그릴지. 별 모양이 커지는 그림이라 한 타일 남짓이 맞다.
    "projectileWidthTiles": "1.1",
}

# ──────────────────────────────────────────────────────────────────────────
# 시트 좌표 — **실측** (맨 위 「시트 구조」 참조)
#
#   src   : "01" / "02"
#   name  : 폴더 이름 (= 스킨 칸 이름 · ``Unused_`` 는 빌더가 건너뛴다)
#   ly0,ly1 : 프레임 번호 줄
#   y0,y1 : 그림 줄
#   x0,x1 : 패널 (박스 테두리 안쪽)
#   expect: 기대 장수 — 다르면 바로 죽는다
#   kind  : "body" 몸통(그림자 지우기 + 몸통 중심 정렬) / "fx" 이펙트(밑동 정렬)
# ──────────────────────────────────────────────────────────────────────────
ROWS = [
    # ── 몸통 ────────────────────────────────────────────────────────────
    ("02", "Idle",         39,  48,  52, 192,   11,  882, 8, "body"),
    ("02", "Walk",        232, 241, 245, 362,   11,  882, 8, "body"),
    ("02", "MeleeAttack", 400, 409, 413, 536,   11,  882, 8, "body"),
    # ★ ``_02`` 에 없는 두 줄 — 여기만 ``_01`` 을 쓴다 (맨 위 표)
    ("01", "RangedAttack", 484, 495, 499, 569,  19,  900, 7, "body"),

    # ── 투사체 (방향 없는 한 벌 · +X 를 향한다) ───────────────────────
    ("01", "Projectile",   59,  73,  80, 190,  200, 1340, 8, "fx"),

    # ── 이펙트 (⚠ 받을 칸이 없어 미배선 — 맨 위 표) ───────────────────
    ("02", "Unused_MoveFx",  239, 248, 252, 362,  885, 1523, 8, "fx"),
    ("02", "Unused_MeleeFx", 406, 415, 419, 536,  885, 1523, 8, "fx"),

    # ── 스킬 모션 세 줄 (세로 3단) · ⚠ 미배선 ─────────────────────────
    ("02", "Unused_Skill1", 574, 583, 588, 714,   11,  469, 7, "body"),
    ("02", "Unused_Skill2", 574, 583, 588, 714,  472,  992, 7, "body"),
    ("02", "Unused_Skill3", 574, 583, 588, 714,  995, 1523, 7, "body"),

    # ── 스킬 이펙트 세 줄 · ⚠ 미배선 ───────────────────────────────────
    ("02", "Unused_Skill1Fx", 759, 768, 772, 878,   11,  469, 7, "fx"),
    ("02", "Unused_Skill2Fx", 759, 768, 772, 878,  472,  992, 7, "fx"),
    ("02", "Unused_Skill3Fx", 759, 768, 772, 878,  995, 1523, 7, "fx"),
]

#: 좌우 방향이 **없는** 한 벌짜리 묶음 — 파일 이름에 Right/Left 를 안 붙인다.
#: (투사체는 +X 를 향한 한 벌만 있으면 코드가 방향으로 돌린다 · 이펙트는 바닥 그림)
NO_DIRECTION = {"Projectile", "Unused_MoveFx", "Unused_MeleeFx",
                "Unused_Skill1Fx", "Unused_Skill2Fx", "Unused_Skill3Fx"}

#: ★ 시그리드는 **오른쪽을 보고** 그려졌다 — 지팡이가 오른쪽으로 뻗고(근거리 3·4·8번)
#:   이동에서도 몸이 오른쪽으로 기운다. 왼쪽은 미러다.
#:   ⚠ 대기·스킬은 정면 대칭이라 어느 쪽이어도 같다 — 규칙을 하나로 두려고 같이 미러한다.

def report_scale(sheets):
    """
    한 배율로 그려진 시트인지 — **머리 폭**으로 확인한다(엘린과 같은 검산).
    두 시트를 섞어 쓰므로 **두 판본 사이도** 같아야 한다.
    """
    print("  [크기 검산] 줄마다 머리 폭(px) — 같으면 한 배율이라 정규화 불필요")
    for src, name, ly0, ly1, y0, y1, x0, x1, expect, kind in ROWS:
        if kind != "body":
            continue
        sheet = sheets[src]
        cells = cells_by_labels(sheet["gray"], x0, x1, ly0, ly1)
        widths = []
        for box in boxes_for(sheet["mask"], cells, y0, y1):
            if box is None:
                continue
            bx0, bx1, by0, _by1 = box
            top = sheet["mask"][by0:by0 + 14, bx0:bx1 + 1]
            xs = np.where(top.any(axis=0))[0]
            widths.append(int(xs.max() - xs.min() + 1) if len(xs) else 0)
        print("    %-16s (%s) %s" % (name, src, widths))


def build(sheets):
    made = 0
    for src, name, ly0, ly1, y0, y1, x0, x1, expect, kind in ROWS:
        sheet = sheets[src]

        cells = cells_by_labels(sheet["gray"], x0, x1, ly0, ly1)
        labels = label_count(sheet["gray"], x0, x1, ly0, ly1)
        if labels != expect or len(cells) != expect:
            raise SystemExit(
                "⚠ %s(%s): 프레임 번호 %d개 · 칸 %d개인데 %d장을 기대했습니다 "
                "(라벨 y%d~%d · 그림 y%d~%d · x%d~%d). 시트가 바뀌었으면 좌표를 다시 재세요."
                % (name, src, labels, len(cells), expect, ly0, ly1, y0, y1, x0, x1))

        # ★ 몸통 줄만 발밑 그림자를 지운다 — 이펙트는 그 자체가 지면 연출이다.
        if kind == "body":
            rough = [b for b in boxes_for(sheet["mask"], cells, y0, y1) if b is not None]
            shadow = np.zeros(sheet["mask"].shape, dtype=bool)
            for b in rough:
                shadow |= shadow_in_box(sheet, b)
            sheet["mask"] &= ~shadow

        boxes = [b for b in boxes_for(sheet["mask"], cells, y0, y1) if b is not None]
        frames = [crop_rgba(sheet, b) for b in boxes]
        anchor = body_anchor if kind == "body" else base_anchor
        images, w, h = compose(frames, [anchor(f) for f in frames])

        folder = os.path.join(DST_ROOT, name)
        if name in NO_DIRECTION:
            for i, img in enumerate(images):
                write_png(img, folder, "Char_%s_%02d" % (name, i))
                made += 1
            note = "방향 없음"
        else:
            for i, img in enumerate(images):
                write_png(img, folder, "Char_%s_Right_%02d" % (name, i))
                write_png(img.transpose(Image.FLIP_LEFT_RIGHT), folder,
                          "Char_%s_Left_%02d" % (name, i))
                made += 2
            note = "오른쪽 원본 + 미러"
        ensure_folder_meta(folder)

        print("  %-18s (%s) %3d x %3d · %2d장 · 라벨 %d개 · %s"
              % (name, src, w, h, len(images), labels, note))
    return made


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print("[시그리드 모션 시트 분해]")

    # ★★ 구획 사각 테두리를 **배경 판정보다 먼저** 지운다 — 안 그러면 상자 안쪽이
    #    통째로 갇혀 잉크로 잡힌다(맨 위 「시트 구조」 ⚠).
    sheets = {}
    for tag, path in (("01", SRC01), ("02", SRC02)):
        sheets[tag] = load_sheet(path, box_borders=True)

    report_scale(sheets)
    n = build(sheets)

    spec = write_skin_spec(DST_ROOT, SKIN_SPEC, "Tools/sigrid_skin_build.py")
    ensure_folder_meta(DST_ROOT)
    print("  스킨 설정 %s (%d줄) — 유니티 빌더가 읽는다" % (SKIN_SPEC_NAME, spec))
    print("  → 프레임 %d장" % n)
    print("  다음: 유니티 메뉴 LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기")


if __name__ == "__main__":
    main()
