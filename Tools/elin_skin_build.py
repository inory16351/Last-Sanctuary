# -*- coding: utf-8 -*-
"""엘린(캐릭터 9001) 모션 시트 → 프레임 분해 (2026-08-19).

원본 — **두 장이다**
--------------------
``<볼트>/리소스/asset/char_asset/Elin_asset_02.png``  (1536x1024) — ★ **정본**
``<볼트>/리소스/asset/char_asset/Elin_asset_01.png``  (1536x1024) — 같은 시트의 **먼저 판본**

두 장은 같은 기획 시트의 두 판본이다. 라린길(115절)처럼 **줄 단위로 좋은 쪽**을 쓰려고
전부 재어봤는데, 이 시트는 **모든 줄에서 ``_02`` 가 이기거나 같다** — 그래서 정본이 하나다:

| 줄 | ``_01`` | ``_02`` | 판정 |
|---|---|---|---|
| 대기 | 7칸 | **8칸** | ``_02`` (한 장 더 많다) |
| 이동 | **한 줄** 8칸 | **두 줄** 8+8칸 (좌/우 전용 원화) | ``_02`` — 미러가 아니라 **방향별 원화**다 |
| 근거리 | 7칸 | **8칸** | ``_02`` |
| 원거리 · 회복 · 마법 | 7~9칸(경계 붙음) | **8칸** | ``_02`` |
| 이펙트 3종 | 6·6·7칸 | 6·6·7칸 | 같음 → ``_02`` (한 파일로 끝낸다) |
| 프레임 번호 | **없음** | **있음** | ``_02`` — 칸 수를 라벨로 검산할 수 있다 |

⚠ 그래서 ``_01`` 은 **읽지 않는다.** 지우지도 않는다(원본 보관) — 이 표가 그 판단의 근거다.

★ 크기 정규화를 **하지 않는다** — 이 시트는 한 배율로 그려졌다
--------------------------------------------------------------
말파스·라린길 파이프라인은 모션마다 median 세로를 재서 대기 줄에 맞췄다(113-2절).
**여기서 그러면 안 된다.** 원거리·회복·마법은 엘린이 **무릎을 꿇는** 동작이라 세로가
원래 짧다(69~76px vs 대기 82~85px). 세로로 맞추면 **꿇은 자세가 서 있는 키까지 늘어난다.**

한 배율로 그려졌다는 근거는 **머리 폭**이다 — 일곱 줄 전부 **32~35px** 로 같다
(실측, 아래 :func:`report_scale` 가 매번 다시 출력한다). 그래서 factor 는 1.0 고정이다.

★★ 배경 판정을 **거리**가 아니라 **이어짐**으로 한다 — 이 시트만의 함정
----------------------------------------------------------------------
말파스·라린길 파이프라인은 «흰색에서 얼마나 멀면 그림» 으로 알파를 만들었다.
**엘린 시트에는 그 방법을 쓸 수 없다.** 엘린의 두건·눈가리개·수도복 하이라이트가
**252~255**, 즉 **배경(254)과 같은 색**이다. 거리로 재면 그 부분이 **통째로 투명**해져
얼굴 반쪽과 두건이 사라진다(실제로 처음 그렇게 나왔다).

그래서 배경을 **시트 테두리와 이어진 덩어리**로만 정의한다(:func:`background_mask`) —
``dist <= BG_TOL`` 인 픽셀만 타고 흘러간다. 두건 속 흰색은 검은 외곽선에 둘러싸여
바깥과 이어지지 않으므로 **불투명하게 남는다.** 안쪽은 알파 255 로 두고, 배경에 닿는
**한 겹**만 거리로 부드럽게 깎는다(:func:`alpha_for`) — 기존 캐릭터 스프라이트도
부분 알파가 프레임당 100~1500px 뿐인 «거의 하드 알파» 다(실측).

★★ 발밑 그림자는 **프레임 아래쪽 띠 안에서만** 지운다
------------------------------------------------------
몸통 프레임 아래에 회색 타원이 깔려 있다(대기 1번 발밑 한 곳에만 약 320px). 이건
배경과 이어져 있어서 위의 이어짐 판정으로는 **안 걸린다**(색이 회색 148~215 라
``BG_TOL`` 을 넘는다).

⚠⚠ **카시노마·라린길이 쓴 «채도 낮고 밝으면 그림자» 흐름을 그대로 쓰면 안 된다.**
엘린의 **은발**이 채도 5~10 · 광도 200~235 라 그림자와 **구분이 안 되고**, 머리카락은
배경에 직접 닿아 있다 — 흘려 채우기가 머리카락을 타고 두건까지 들어가 프레임당
수천 px 을 먹는다(첫 시도에서 시트 전체 38,917px 이 지워졌다).

→ 그래서 **기하로 가둔다**: 프레임 경계 상자의 **아래 ``SHADOW_BAND_PX`` 줄 안에서만**
   배경에서 흘려 채운다. 그 띠 밖(머리·두건)에는 흐름이 닿을 수 없다. 광도 상한
   (``SHADOW_LUM_MAX``)으로 수도복의 흰 밑단도 함께 지킨다.
⚠ 이펙트 줄(오른쪽 단)에는 발밑 그림자가 없다 — 그 자체가 지면 연출이다. 그림자 지우기를
  **몸통 줄에만** 돌린다(:func:`build_fx` 는 부르지 않는다).

칸을 어떻게 가르나 — **이 시트는 빈 열이 다 있다**
--------------------------------------------------
말파스·라린길은 라벨로 칸 수를 세고 경계를 빈 열로 밀었다(113-1절). 엘린 시트는
**열 줄 전부 (프레임 수 + 1)개의 빈 열**이 그대로 있어서(실측) 빈 열의 **가운데**로
자르면 끝난다. 라벨은 **검산에만** 쓴다 — 라벨 개수와 칸 개수가 다르면 바로 죽는다.
(라벨을 안 보고 지나가면 시트가 바뀐 걸 모르고 조용히 틀린 개수를 뽑는다.)

방향 — **줄마다 다르다**
------------------------
  · 이동 → **좌/우 전용 원화 두 줄**. 첫 줄이 왼쪽(머리·베일이 왼쪽으로 흐른다),
    둘째 줄이 오른쪽. 시트 제목이 "이동 (Move / Walk) - 좌 / 우" 로 그렇게 적혀 있다.
  · 근거리 「사슬 휘두르기」 → **오른쪽** (사슬이 오른쪽으로 뻗고 5번 칸의 원호도 오른쪽)
  · 대기 · 원거리 · 회복 · 마법 → 정면(꿇거나 선 정면 자세) — 원본을 오른쪽으로 두고 미러

⚠ **원거리·마법 줄은 앞 5칸만 몸통이다.** 6~8칸은 그 줄 안에 같이 그려 넣은
**쇠사슬 솟구침 3단계 축약본**이고, 오른쪽 단에 **6단계 정식본**이 따로 있다.
축약본도 뽑아는 둔다(``Fx/ChainRiseShort*``) — 배선하지는 않는다. 아래 표 참조.

무엇이 어디로 가나 (배선은 `Editor/CharacterSkinBuilder.cs` 가 한다)
--------------------------------------------------------
| 폴더 | 장수 | 스킨 칸 |
|---|---|---|
| ``Char/Idle`` | 8 | ``idleRight`` · ``idleLeft`` |
| ``Char/Walk`` | 8+8 | ``walkRight`` · ``walkLeft`` (**미러가 아니다**) |
| ``Char/MeleeAttack`` | 8 | ``attackRight`` · ``attackLeft`` |
| ``Char/RangedAttack`` | 5 | ``rangedRight`` · ``rangedLeft`` |
| ``Char/MagicAttack`` | 5 | ``magicRight`` · ``magicLeft`` (**칸 신설** — 아래) |
| ``Char/Heal`` | 8 | ``healRight`` · ``healLeft`` |
| ``Char/Impact`` | 6 | ``impactFrames`` (원거리 적중 — 땅에서 사슬이 솟는다) |
| ``Char/ImpactMagic`` | 6 | ``magicImpactFrames`` (**칸 신설**) |
| ``Char/HealFx`` | 7 | ``healFxFrames`` (**칸 신설**) |
| ``Char/Unused_ChainRiseShort`` | 3 | ⚠ 미배선 — 원거리 줄 안의 축약본. 정식본(6장)이 있다 |
| ``Char/Unused_ChainRiseShortMagic`` | 3 | ⚠ 미배선 — 마법 줄 안의 축약본 |

★ 폴더 이름이 **곧 스킨 칸 이름**이다 — 유니티 빌더가 그 대응 표 하나만 들고 있다.
  ``Unused_`` 로 시작하면 건너뛴다(경고도 안 낸다).

★ **스킨에 칸 셋을 새로 만든 이유** — 시트가 「마법」과 「원거리」를 **다른 동작으로**
그렸고(같은 기도 자세가 아니다: 5번 칸이 65x62 vs 55x74, 평균 화소차 26~66),
적중 이펙트도 **갈색 사슬 / 보라 사슬**로 갈라 그렸다. 예전 스킨에는
``SkinAttackMotion`` 이 셋뿐이라(마법이 원거리 모션을 같이 썼다) 이 원화를 받을 칸이
없었다. 자세한 근거는 ``CharacterSkinSO`` 의 해당 필드 주석에 적었다.

★ **투사체를 뽑지 않는다** — 유저 지시(2026-08-19): *"엘린의 마법/원거리 공격은 투사체
없이 적중대상 땅바닥에서 사슬이 올라오는 걸로"*. 시트에도 날아가는 탄환 그림이 아예 없다.
스킨의 ``projectileFrames`` 를 비우는 것만으로는 부족하다(비면 진영 기본 탄환으로
떨어진다) — ``groundImpactOnly`` 를 켜서 **이동 연출 자체를 건너뛴다**.

발밑 그림자는 지우고 **바닥 정렬**한다
--------------------------------------
피벗이 (0.5, 0) = 발밑이라 프레임마다 캔버스 **아래쪽**을 맞춘다. 가로는 그림 전체가
아니라 **몸통 중심**(:func:`body_anchor`)으로 맞춘다 — 사슬이 한쪽으로 길게 뻗은 칸
(근거리 2·3·8번)에서 전체 중심을 쓰면 몸이 반대쪽으로 밀려 **옆으로 미끄러져 보인다**
(113-4절과 같은 사고).

이펙트는 **밑동**을 기준으로 가로 정렬한다 — 사슬이 위로 갈수록 기울어서 전체 중심을
쓰면 땅에 박힌 구멍이 대상 발밑에서 어긋난다.

사용법:  python Tools/elin_skin_build.py
다음:    유니티 메뉴 **LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기**
        (= `Editor/CharacterSkinBuilder.cs` · MCP `execute_menu_item` 으로도 부른다)
        그 다음 `python Tools/measure_skin_tiles.py` (contentSizeTiles 실측)

⚠ 예전처럼 `gen_*_skin.py` 로 .asset YAML 을 엮지 **않는다** — 유저 지시(2026-08-19)
  *"하드코딩 하지 말고 스킨 에셋 만들어서 mcp 로 직접 넣어줘"*. 이 스크립트는
  **프레임 PNG 와 `_skin_spec.txt` 까지만** 만들고, 배선은 유니티가 한다.
"""

import hashlib
import os
import shutil
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT

# ★ 시트 분해의 공통 규칙은 전부 여기 있다 — 이 파일에는 «이 시트의 실측 좌표와
#   판단» 만 남긴다. 배경·그림자·갇힌 배경 판정의 근거는 그쪽 주석에 있다.
from skin_sheet import (  # noqa: F401  (상수도 그대로 쓴다)
    PPU, BG_TOL, ALPHA_HI, RING_MIN_ALPHA,
    POCKET_MIN_AREA, POCKET_INK_RING_LUM,
    LABEL_LUM, LABEL_SAT, LABEL_GAP, LABEL_MAX_W, CELL_GAP_MIN,
    BODY_STREAK_RATIO, FX_BASE_RATIO,
    SHADOW_BAND_PX, SHADOW_SAT_MAX, SHADOW_LUM_MIN, SHADOW_LUM_MAX,
    SHADOW_SIDE_MARGIN,
    SKIN_SPEC_NAME, write_skin_spec,
    guid_for, modal_background, grow, flood, background_mask,
    enclosed_background, shadow_in_box, load_sheet, runs, label_count,
    cells_by_gaps, boxes_for, alpha_for, crop_rgba, column_thickness,
    body_anchor, base_anchor, compose, write_png, ensure_folder_meta,
)


SKIN_SPEC = {
    "skinAssetName": "Skin_Elin",
    "displayName": "엘린",
    "framesPerSecond": "10",
    "attackFramesPerSecond": "14",
    # ★★ 투사체를 쓰지 않는다 — 유저 지시(맨 위 ★). 대상 발밑에 착탄만 깐다.
    "groundImpactOnly": "1",
    # 사슬이 솟는 그림을 가로 몇 타일로 그릴지. 마법 공격은 실제 피해 범위를 쓰므로
    # (UnitCombat.MagicAreaTiles) 이 값은 원거리 유형일 때만 쓰인다.
    "impactWidthTiles": "1.6",
    # 바닥에 눕히지 않는다 — 위로 솟는 그림이 곧 의도다(십자가·사슬 둘 다 세로 연출).
    "impactFlattenY": "1",
}

#: ★ 정본 한 장 (맨 위 표).
SRC = os.path.join(VAULT, "리소스", "asset", "char_asset", "Elin_asset_02.png")

#: 읽지 않는다 — 맨 위 표의 근거를 매번 다시 확인해주려고 경로만 들고 있다.
SRC_FIRST_DRAFT = os.path.join(VAULT, "리소스", "asset", "char_asset", "Elin_asset_01.png")

DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Elin", "Char")

#: 옛 엘린 원화(날개 달린 「Angel」 프로토타입)를 지우고 시작한다 — 완전히 다른 인물이라
#: 남겨두면 어느 쪽이 정본인지 알 수 없다. `Anim/` 과 `Char_Angel_Controller.controller`
#: 도 같이 지운다: 그 컨트롤러의 guid 를 참조하는 파일이 프로젝트에 **하나도 없고**
#: (grep 확인), 다른 캐릭터 폴더는 전부 `Char/` 하나뿐이다.
STALE_DIRS = ["Anim"]
STALE_FILES = ["Char_Angel_Controller.controller", "Char_Angel_Controller.controller.meta",
               "Anim.meta"]

#: 1픽셀당 유니티 단위. 게임 안 크기는 contentSizeTiles 로 정규화되므로(61·66절)
#: 이 값이 화면 크기를 정하지 않는다.
# ──────────────────────────────────────────────────────────────────────────
# 시트 좌표 (Elin_asset_02.png · 실측)
#
#   name        : 폴더 이름 (= 파일 접두사 `Char_<name>_...`)
#   side        : "mirror" 원본 한 벌 + 좌우 미러 / "left" · "right" 그 방향 전용
#   y0,y1       : 그림 줄
#   ly0,ly1     : 프레임 번호 줄 (칸 수 검산)
#   take        : 앞에서 몇 칸만 쓸지 (None = 전부)
# ──────────────────────────────────────────────────────────────────────────
BODY_ROWS = [
    ("Idle",         "mirror",  52, 139, 143, 153, None),
    ("Walk",         "left",   204, 288, 294, 303, None),
    ("Walk",         "right",  313, 395, 401, 410, None),
    ("MeleeAttack",  "mirror", 456, 547, 551, 560, None),
    # ⚠ 6~8칸은 사슬 솟구침 축약본이다 — 아래 EXTRA_FX 가 따로 가져간다.
    ("RangedAttack", "mirror", 603, 691, 696, 705, 5),
    ("Heal",         "mirror", 752, 828, 834, 843, None),
    ("MagicAttack",  "mirror", 885, 974, 979, 989, 5),
]

BODY_X = (0, 900)

#: 오른쪽 단 — 지면 이펙트 세 줄. (name, y0, y1, ly0, ly1, 기대 장수)
FX_ROWS = [
    # (폴더 = 스킨 칸 이름, y0, y1, 라벨 y0, y1, 기대 장수)
    #   HealFx      ← 「회복 이펙트」 초록 십자가
    #   Impact      ← 「원거리 적중 이펙트」 갈색 쇠사슬 솟구침
    #   ImpactMagic ← 「마법 적중 이펙트」 보라 쇠사슬 솟구침
    ("HealFx",      181, 290, 299, 309, 7),
    ("Impact",      449, 588, 594, 603, 6),
    ("ImpactMagic", 674, 817, 825, 834, 6),
]

FX_X = (915, 1535)

#: 몸통 줄 안에 섞여 있는 축약 이펙트 — (name, 몸통줄 이름, 몇 번째 칸부터)
#: ⚠ ``Unused_`` 로 시작하는 폴더는 스킨 빌더가 **건너뛴다**(칸 이름이 아니므로).
EXTRA_FX = [
    ("Unused_ChainRiseShort",      "RangedAttack", 5),
    ("Unused_ChainRiseShortMagic", "MagicAttack",  5),
]

def report_scale(sheet, rows):
    """
    ★ 크기 정규화를 **안 하는** 근거를 매번 다시 출력한다 (맨 위 ★).

    머리 폭이 줄마다 같으면 한 배율로 그려진 시트다. 여기서 값이 흔들리기 시작하면
    시트가 바뀐 것이므로 그때 정규화를 다시 생각해야 한다.
    """
    print("  [크기 검산] 줄마다 머리 폭(px) — 같으면 한 배율이라 정규화 불필요")
    for name, _side, y0, y1, _l0, _l1, take in rows:
        cells = cells_by_gaps(sheet["mask"], y0, y1, *BODY_X)
        if take:
            cells = cells[:take]
        widths = []
        for box in boxes_for(sheet["mask"], cells, y0, y1):
            if box is None:
                continue
            bx0, bx1, by0, _by1 = box
            top = sheet["mask"][by0:by0 + 12, bx0:bx1 + 1]
            xs = np.where(top.any(axis=0))[0]
            widths.append(int(xs.max() - xs.min() + 1) if len(xs) else 0)
        print("    %-13s %s" % (name, widths))


def build_bodies(sheet):
    """몸통 모션 — 줄마다 캔버스를 따로 잡는다."""
    made = 0
    kept_cells = {}
    shadow_total = 0
    pocket_total = 0

    for name, side, y0, y1, ly0, ly1, take in BODY_ROWS:
        cells = cells_by_gaps(sheet["mask"], y0, y1, *BODY_X)
        labels = label_count(sheet["gray"], BODY_X[0], BODY_X[1], ly0, ly1)
        if labels != len(cells):
            raise SystemExit(
                "⚠ %s: 프레임 번호 %d개인데 칸이 %d개로 갈렸습니다 (y %d~%d). "
                "시트가 바뀌었을 수 있습니다 — 좌표를 다시 재세요."
                % (name, labels, len(cells), y0, y1))

        kept_cells[name] = (cells, y0, y1)
        use = cells[:take] if take else cells

        # ★★ 갇힌 배경(근거리 5번 원호 안) 되돌리기 — 먼저 한다. 그림자 띠 위치를 잡는
        #    경계 상자가 이 흰 덩어리 때문에 커져 있으면 띠가 엉뚱한 데 걸린다.
        for cx0, cx1 in use:
            pockets = enclosed_background(sheet, y0, y1, cx0, cx1)
            if pockets.any():
                sheet["mask"] &= ~pockets
                pocket_total += int(pockets.sum())

        # ★★ 발밑 그림자 — 상자를 한 번 잡아 띠 위치를 알아내고, 지운 뒤 상자를 다시 잡는다
        #    (그림자가 상자의 아래·좌우를 늘려놓기 때문에 순서가 중요하다).
        rough = [b for b in boxes_for(sheet["mask"], use, y0, y1) if b is not None]
        shadow = np.zeros(sheet["mask"].shape, dtype=bool)
        for b in rough:
            shadow |= shadow_in_box(sheet, b)
        sheet["mask"] &= ~shadow
        shadow_total += int(shadow.sum())

        boxes = [b for b in boxes_for(sheet["mask"], use, y0, y1) if b is not None]
        frames = [crop_rgba(sheet, b) for b in boxes]
        anchors = [body_anchor(f) for f in frames]
        images, w, h = compose(frames, anchors)

        folder = os.path.join(DST_ROOT, name)
        for i, img in enumerate(images):
            flipped = img.transpose(Image.FLIP_LEFT_RIGHT)
            if side == "mirror":
                write_png(img, folder, "Char_%s_Right_%02d" % (name, i))
                write_png(flipped, folder, "Char_%s_Left_%02d" % (name, i))
                made += 2
            else:
                write_png(img, folder, "Char_%s_%s_%02d"
                          % (name, "Right" if side == "right" else "Left", i))
                made += 1
        ensure_folder_meta(folder)

        note = {"mirror": "정면/오른쪽 원본 + 미러", "left": "왼쪽 전용 원화",
                "right": "오른쪽 전용 원화"}[side]
        extra = "" if not take else "  (뒤 %d칸은 이펙트라 제외)" % (len(cells) - take)
        print("  %-13s %3d x %3d · %2d장 · 라벨 %d개 · %s%s"
              % (name, w, h, len(images), labels, note, extra))

    print("  갇힌 배경(원호 안 흰 덩어리)으로 되돌린 픽셀 %d개" % pocket_total)
    print("  발밑 그림자로 지운 픽셀 %d개 (몸통 줄만 · 띠 %dpx 안에서)"
          % (shadow_total, SHADOW_BAND_PX))
    return made, kept_cells


def build_fx(sheet, kept_cells):
    """지면 이펙트 — 밑동 기준 가로 정렬 · 바닥 정렬. ⚠ 그림자 지우기를 돌리지 않는다."""
    made = 0

    groups = []
    for name, y0, y1, ly0, ly1, expect in FX_ROWS:
        cells = cells_by_gaps(sheet["mask"], y0, y1, *FX_X)
        labels = label_count(sheet["gray"], FX_X[0], FX_X[1], ly0, ly1)
        if len(cells) != expect or labels != expect:
            raise SystemExit(
                "⚠ Fx/%s: 칸 %d개 · 번호 %d개인데 %d장을 기대했습니다 (y %d~%d)."
                % (name, len(cells), labels, expect, y0, y1))
        groups.append((name, cells, y0, y1, "번호 %d개" % labels))

    # 몸통 줄 안에 섞인 축약본 (⚠ 미배선 — 맨 위 표)
    for name, row, start in EXTRA_FX:
        cells, y0, y1 = kept_cells[row]
        groups.append((name, cells[start:], y0, y1, "%s 줄 %d칸부터" % (row, start + 1)))

    for name, cells, y0, y1, note in groups:
        boxes = [b for b in boxes_for(sheet["mask"], cells, y0, y1) if b is not None]
        if not boxes:
            raise SystemExit("⚠ Fx/%s: 그림을 못 찾았습니다 (y %d~%d)" % (name, y0, y1))

        frames = [crop_rgba(sheet, b) for b in boxes]
        anchors = [base_anchor(f) for f in frames]
        images, w, h = compose(frames, anchors)

        # ★ 폴더 하나가 스킨 칸 하나다 — 유니티 쪽 빌더가 폴더 이름으로 배선한다
        #   (`Editor/CharacterSkinBuilder.cs`). 몸통 모션과 같은 자리에 나란히 둔다.
        folder = os.path.join(DST_ROOT, name)
        for i, img in enumerate(images):
            write_png(img, folder, "Char_%s_%02d" % (name, i))
            made += 1
        ensure_folder_meta(folder)
        print("  %-28s %3d x %3d · %2d장  (%s)" % (name, w, h, len(images), note))

    return made


def clear_stale():
    """옛 「Angel」 원화·애니메이션을 지운다 (맨 위 STALE_* 주석)."""
    root = os.path.dirname(DST_ROOT)
    removed = 0
    if os.path.isdir(DST_ROOT):
        for entry in sorted(os.listdir(DST_ROOT)):
            p = os.path.join(DST_ROOT, entry)
            if os.path.isdir(p):
                removed += sum(1 for _ in os.scandir(p))
                shutil.rmtree(p)
            elif entry.endswith(".meta"):
                os.remove(p)
                removed += 1
    for d in STALE_DIRS:
        p = os.path.join(root, d)
        if os.path.isdir(p):
            removed += sum(1 for _ in os.scandir(p))
            shutil.rmtree(p)
    for f in STALE_FILES:
        p = os.path.join(root, f)
        if os.path.isfile(p):
            os.remove(p)
            removed += 1
    print("  옛 엘린(Angel 프로토타입) 파일 %d개 삭제" % removed)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print("[엘린 모션 시트 분해]")
    if os.path.isfile(SRC_FIRST_DRAFT):
        print("  (먼저 판본 %s 은 읽지 않는다 — 모든 줄에서 정본이 이긴다. 맨 위 표)"
              % os.path.basename(SRC_FIRST_DRAFT))

    sheet = load_sheet(SRC)
    clear_stale()
    report_scale(sheet, BODY_ROWS)

    n1, kept = build_bodies(sheet)
    n2 = build_fx(sheet, kept)

    n = write_skin_spec(DST_ROOT, SKIN_SPEC, "Tools/elin_skin_build.py")
    print("  스킨 설정 %s (%d줄) — 유니티 빌더가 읽는다" % (SKIN_SPEC_NAME, n))
    ensure_folder_meta(DST_ROOT)
    print("  → 프레임 %d장 (몸통 %d · 이펙트 %d)" % (n1 + n2, n1, n2))
    print("  다음: 유니티 메뉴 LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기")


if __name__ == "__main__":
    main()
