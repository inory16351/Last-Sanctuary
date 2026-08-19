# -*- coding: utf-8 -*-
"""베일(웨이브 최종보스 120005) 모션 시트 → 프레임 분해 (2026-08-20).

유저 지시: *"베일도 만들고"* (2026-08-20)

원본 — **두 장이고 ``_02`` 가 정본이다**
----------------------------------------
``<볼트>/리소스/sprites/Bale_asset_02.png``  (1536x1024) — ★ **정본**
``<볼트>/리소스/sprites/Bale_asset_01.png``  (1536x1024) — 같은 구성의 **저프레임 판본**

두 장이 <b>구성은 완전히 같고 장수만 다르다</b>. ``_02`` 가 모든 줄에서 약 두 배다:

| 줄 | ``_01`` | ``_02`` | 판정 |
|---|---|---|---|
| 대기 | 8 | **16** | ``_02`` |
| 이동 | 10 | **16** | ``_02`` |
| 근거리 공격 | 9 | **18** | ``_02`` |
| 원거리 공격 | 10 | **20** | ``_02`` |
| 담뱃대 휘두르기 패턴 | 12 | **24** | ``_02`` |
| 담배연기 패턴 | 10 | **24** | ``_02`` |
| 원거리 투사체 | 8 | **16** | ``_02`` |
| 반원형 범위 이펙트 | 1 | 1 | 같음 |

⚠⚠ <b>시트 헤더의 프레임 수를 믿으면 안 된다.</b> 「■ 대기 (16프레임)」이라고 적혀 있지만
  실제로 그려진 것은 <b>14장</b>이다(빈 열로 갈라보면 정확히 14덩어리 · 간격 94.6px).
  라벨 번호도 <b>건너뛰거나 중복된다</b>(대기 줄에 12·15 가 없고, 원거리 줄에 14 가 두 번).
  그래서 장수는 <b>라벨 덩어리 개수</b>로 센다 — 그 값이 실제 덩어리 수와 일치한다(실측).

★ <b>장수가 시전 시간과 맞아떨어지는 쪽</b>을 골랐다 — 표(`웨이브 몬스터 테이블` / `Skill`)의
  `cast_time` 이 담뱃대 강타 **2초** · 담배연기 **3초** 다. 공격 모션은 초당 14장이므로
  24장 = 1.7초로 2초에 가깝다. ``_01`` 의 12·10장은 0.86·0.71초라 <b>시전이 끝나기 전에
  모션이 끝난다</b>. ``_01`` 이 픽셀은 더 큰데(프레임이 두 배 크다) 게임 안 크기는
  `contentSizeTiles` 로 정규화되므로 화면 크기는 어느 쪽을 써도 같다.

칸을 어떻게 가르나 — ★★ **그림 폭을 균등 분할**한다
----------------------------------------------------
이 시트는 앞의 세 방법이 **전부 안 통한다**(실측):

  · ``cells_by_gaps`` — 프레임이 서로 붙어 빈 열이 없다. 24칸이 **4~11칸**으로 붙는다.
  · ``cells_by_labels`` — 두 자리 라벨이 붙거나 헤더 글자와 섞여 개수가 모자란다
    (16칸 줄에서 **13~14개**만 잡힌다).
  · ``cells_by_pitch`` — 맨 앞·뒤 라벨로 간격을 구하는 방법. 여기서는 **맨 앞 라벨이
    「1」이 아니었다**(원거리 줄에서 「1」을 놓쳐 249 = 「2」를 시작으로 잡았다) —
    간격이 한 칸씩 밀려 프레임마다 옆 칸이 조금씩 잘려 들어왔다. 실제로 그렇게 나왔다.

→ 그래서 :func:`skin_sheet.cells_by_span` 을 쓴다: **그림이 놓인 전체 폭 ÷ 장수**.
  라벨을 아예 안 보므로 위 함정이 통째로 사라지고, 실측으로 라벨에서 구한 간격과
  거의 같다(대기 82.4 ↔ 82.3 · 스킬1 62.1 ↔ 62.3 · 스킬2 61.4 ↔ 61.0).
  시트가 «(16프레임)» 처럼 장수를 적어 놨으므로 개수는 확실하다.

⚠ 라벨은 **검산 전용**으로만 읽는다(:func:`report_frame_counts`) — 개수가 맞으면 좋고
  모자라도 그냥 알려준다. 라벨 줄의 x 시작이 캐릭터 줄만 **140** 인 이유는 헤더
  (`■ 대기 (16프레임)`)가 번호와 같은 줄에 있어서다.

★★ 시트에 **검은 액자**가 있다
------------------------------
구획 상자는 연회색인데 시트 전체를 감싼 테두리는 **검정**이다. 그래서 배경 흘려 채우기의
씨앗이 전부 액자 위에 떨어져 **시트 전부가 그림으로 잡혔다**(1,572,864px = 전 화소).
:func:`skin_sheet.erase_box_borders` 에 어두운 액자 판정을, :func:`background_mask` 에
안쪽 씨앗(``SEED_INSET``)을 넣어 해결했다 — 그쪽 주석에 근거가 있다.

무엇이 어디로 가나 (배선은 `Editor/CharacterSkinBuilder.cs` 가 한다)
--------------------------------------------------------------------
| 폴더 | 장수 | 스킨 칸 |
|---|---|---|
| ``Char/Idle`` | 16 | ``idleRight`` · ``idleLeft`` |
| ``Char/Move`` | 16 | ``walkRight`` · ``walkLeft`` |
| ``Char/MeleeAttack`` | 18 | ``attackRight`` · ``attackLeft`` |
| ``Char/RangedAttack`` | 20 | ``rangedRight`` · ``rangedLeft`` |
| ``Char/Skill1`` | 24 | ``skill1Right`` · ``skill1Left`` — 담뱃대 강타(130009) |
| ``Char/Skill2`` | 24 | ``skill2Right`` · ``skill2Left`` — 담배연기(130010) |
| ``Char/Projectile`` | 16 | ``projectileFrames`` — 원거리 평타 탄환 |
| ``Char/Skill2Fx`` | 1 | ``skill2Fx`` — 반원형 범위 이펙트 |

⚠ **``skill1Fx`` 는 없다.** 담뱃대 강타는 시트에 <b>범위 연출이 따로 없고</b> 모션 안에
  휘두르는 궤적이 그려져 있다(7~12번 칸). 비워두면 `BossSkillCaster` 가 범위 표시를
  생략한다 — 없는 그림을 지어내는 것보다 낫다.

⚠ 원거리 평타 줄의 **14번 칸은 탄환만** 그려져 있다(몸통이 없다). 그 칸도 그대로 뽑는다 —
  20장이 한 동작이고, 중간에 캐릭터가 사라지는 것이 원화의 의도다(발사 순간의 연출).

방향 — **전부 오른쪽**
----------------------
담뱃대가 오른쪽으로 뻗고 연기도 오른쪽으로 퍼진다. 왼쪽은 미러다.

사용법:  python Tools/bale_skin_build.py
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
    load_sheet, label_blobs, cells_by_span, boxes_for, boxes_dominant, crop_rgba,
    body_anchor, base_anchor, compose, write_png, ensure_folder_meta,
    shadow_in_box,
)

SRC = os.path.join(VAULT, "리소스", "sprites", "Bale_asset_02.png")
SRC_LOW = os.path.join(VAULT, "리소스", "sprites", "Bale_asset_01.png")

DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Bale", "Char")

SKIN_SPEC = {
    "skinAssetName": "Skin_Bale",
    # ⚠ 웨이브 보스는 종마다 폴더 하나다 — 한 폴더에 몰아넣으면
    #   `CharacterAnimator.PickRandomSkin` 이 다른 몬스터에게 이 외형을 줄 수 있다.
    "outputFolder": "Assets/_Project/Resources/MonsterSkins/Bale",
    "displayName": "베일",
    "framesPerSecond": "10",
    "attackFramesPerSecond": "14",
    # 담뱃대에서 뿜는 검은 구체. 몸집이 15타일이라 1.4 면 주먹만 하게 보인다.
    "projectileWidthTiles": "1.4",
}

#: 라벨 줄의 x 시작 — 캐릭터 모션 줄은 헤더가 같은 줄에 있다(맨 위 ⚠).
LABEL_X_BODY = 140
LABEL_X_PATTERN = 10

# ──────────────────────────────────────────────────────────────────────────
# 시트 좌표 — **실측** (Bale_asset_02.png)
#   name · 라벨 y0,y1 · 그림 y0,y1 · x0,x1 · 장수 · kind
# ──────────────────────────────────────────────────────────────────────────
# ⚠⚠ 라벨 줄의 x 와 **그림 줄의 x 는 다르다.** 캐릭터 모션 줄은 헤더
#    (`■ 대기 (16프레임)`)가 번호와 같은 줄에 있어 라벨은 140 부터 봐야 하는데,
#    그림에까지 그 값을 쓰면 **1번 프레임의 왼쪽이 잘려** 칸 간격이 통째로 어긋난다
#    (실측: 대기 줄의 진짜 잉크는 x114 에서 시작한다 — 140 으로 자르면 26px 손실).
ROWS = [
    #  name           라벨 y0,y1   그림 y0,y1   라벨x0          그림x0  x1    장수  kind
    ("Idle",          49,  61,  72, 151, LABEL_X_BODY,    10, 1523, 16, "body"),
    ("Move",         170, 185, 197, 270, LABEL_X_BODY,    10, 1523, 16, "body"),
    ("MeleeAttack",  292, 306, 314, 388, LABEL_X_BODY,    10, 1523, 18, "body"),
    ("RangedAttack", 409, 426, 431, 505, LABEL_X_BODY,    10, 1523, 20, "body"),
    ("Skill1",       576, 587, 590, 690, LABEL_X_PATTERN, 10, 1523, 24, "body"),
    ("Skill2",       727, 737, 742, 816, LABEL_X_PATTERN, 10, 1523, 24, "body"),
    # ⚠ 투사체는 오른쪽에 반원형 이펙트가 같이 있어 x 를 755 로 막는다.
    ("Projectile",   881, 897, 900, 1000, LABEL_X_PATTERN, 10, 755, 16, "fx"),
]

#: 반원형 범위 이펙트 — 라벨이 「(1프레임)」 하나뿐이라 칸을 가를 것이 없다.
#:   (name, y0, y1, x0, x1)
SINGLE_FX = [("Skill2Fx", 884, 1000, 760, 1523)]

#: 좌우 방향이 없는 묶음 — 파일 이름에 Right/Left 를 안 붙인다.
NO_DIRECTION = {"Projectile", "Skill2Fx"}


def report_frame_counts(sheet):
    """장수가 시트 표기와 맞는지 매번 다시 확인해 출력한다."""
    print("  [칸 검산] 라벨 개수는 붙어서 모자랄 수 있다 — 간격은 맨 앞·뒤만 쓴다")
    for name, l0, l1, y0, y1, lx0, x0, x1, cnt, kind in ROWS:
        bl = label_blobs(sheet["gray"], lx0, x1, l0, l1, gap=10, max_w=30)
        if len(bl) < 2:
            raise SystemExit("⚠ %s: 라벨을 두 개도 못 찾았습니다 (y%d~%d · x%d~%d)"
                             % (name, l0, l1, lx0, x1))
        print("    %-13s 라벨 %2d개 (x %4d..%4d) → 칸 %2d개"
              % (name, len(bl), bl[0][0], bl[-1][1], cnt))


def build(sheet):
    made = 0
    for name, l0, l1, y0, y1, lx0, x0, x1, cnt, kind in ROWS:
        # ★★ 장수는 **라벨 덩어리 개수**다 — 시트 헤더의 «(16프레임)» 을 믿으면 안 된다
        #    (맨 위 ⚠⚠). 그리고 칸은 «그림이 놓인 폭 ÷ 장수» 로 가른다.
        cnt = len(label_blobs(sheet["gray"], lx0, x1, l0, l1, gap=10, max_w=30))
        cells = cells_by_span(sheet["mask"], y0, y1, x0, x1, cnt)
        if len(cells) != cnt:
            raise SystemExit("⚠ %s: 칸이 %d개로 나왔습니다 (%d 기대)"
                             % (name, len(cells), cnt))

        if kind == "body":
            rough = [b for b in boxes_dominant(sheet["mask"], cells, y0, y1,
                                               min_ink_ratio=0.35) if b is not None]
            shadow = np.zeros(sheet["mask"].shape, dtype=bool)
            for b in rough:
                shadow |= shadow_in_box(sheet, b)
            sheet["mask"] &= ~shadow

        # ★★ 칸 안의 «가장 잉크가 많은 덩어리»만 — 옆 칸 조각을 버린다(그쪽 주석).
        # ⚠ 옆 칸에서 삐져 들어온 **망토 조각**이 꽤 커서(본체의 12~25%) 기본값으로는
        #   안 걸러진다 — 0.35 로 올렸다. 연기·담뱃대처럼 몸통과 떨어져 있어도 그 프레임의
        #   일부인 것은 훨씬 크므로 함께 남는다(실측으로 확인).
        boxes = boxes_dominant(sheet["mask"], cells, y0, y1, min_ink_ratio=0.35)
        missing = [i for i, b in enumerate(boxes) if b is None]
        if missing:
            raise SystemExit("⚠ %s: 빈 칸이 있습니다 %s — 좌표를 다시 재세요."
                             % (name, missing))

        frames = [crop_rgba(sheet, b) for b in boxes]
        anchor = body_anchor if kind == "body" else base_anchor
        images, w, h = compose(frames, [anchor(f) for f in frames])

        made += write_group(images, name)
        print("  %-13s %3d x %3d · %2d장" % (name, w, h, len(images)))

    for name, y0, y1, x0, x1 in SINGLE_FX:
        box = boxes_for(sheet["mask"], [(x0, x1)], y0, y1)[0]
        if box is None:
            raise SystemExit("⚠ %s: 그림을 못 찾았습니다" % name)
        rgba = crop_rgba(sheet, box)
        images, w, h = compose([rgba], [base_anchor(rgba)])
        made += write_group(images, name)
        print("  %-13s %3d x %3d ·  1장" % (name, w, h))

    return made


def write_group(images, name):
    folder = os.path.join(DST_ROOT, name)
    n = 0
    for i, img in enumerate(images):
        if name in NO_DIRECTION:
            write_png(img, folder, "Char_%s_%02d" % (name, i))
            n += 1
        else:
            write_png(img, folder, "Char_%s_Right_%02d" % (name, i))
            write_png(img.transpose(Image.FLIP_LEFT_RIGHT), folder,
                      "Char_%s_Left_%02d" % (name, i))
            n += 2
    ensure_folder_meta(folder)
    return n


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print("[베일 모션 시트 분해]")
    if os.path.isfile(SRC_LOW):
        print("  (저프레임 판본 %s 은 읽지 않는다 — 맨 위 표)" % os.path.basename(SRC_LOW))

    # ★★ 검은 액자를 배경 판정보다 먼저 지운다 (맨 위 ★★).
    sheet = load_sheet(SRC, box_borders=True)
    report_frame_counts(sheet)

    n = build(sheet)
    spec = write_skin_spec(DST_ROOT, SKIN_SPEC, "Tools/bale_skin_build.py")
    ensure_folder_meta(DST_ROOT)
    ensure_folder_meta(os.path.dirname(DST_ROOT))
    print("  스킨 설정 %s (%d줄)" % (SKIN_SPEC_NAME, spec))
    print("  → 프레임 %d장" % n)
    print("  다음: 유니티 메뉴 LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기")


if __name__ == "__main__":
    main()
