# -*- coding: utf-8 -*-
"""바리올라(에픽 중립 보스 1103) 모션 시트 → 프레임 분해 (2026-08-20).

원본: ``<볼트>/리소스/sprites/Variola_asset.png`` (1536x1024, RGB — 알파가 없다).

유저 지시: *"바리올라 모션 스킨 — 서식지는 끝났고 Variola_asset.png만 남았습니다.
시트가 깨끗해 보여서 오래 안 걸립니다."*

★ 실제로 깨끗하다 — 그런데 함정이 셋 있었다
=============================================

★★ ① **라벨 번호가 건너뛴다** (장수를 헤더/라벨 번호로 세면 안 된다)
-------------------------------------------------------------------
이 작가의 시트는 헤더 숫자가 틀린 적이 있다(120절 — 베일 「대기 (16프레임)」인데 14장 ·
바리올라 서식지 「벽 16종」인데 12칸). 이 시트는 헤더에 장수를 안 적었지만 **프레임 번호가
중간을 건너뛴다**:

    이동        01 02 03 04 05 __ 07 08   → 「06」이 없다.  실제 **7장**
    근거리 공격 01 02 __ 04 … 10          → 「03」이 없다.  실제 **9장**
    스킬 2      01 __ 03 04 … 08          → 「02」가 없다.  실제 **7장**

그래서 **라벨 개수(= 그려진 칸 수)가 정본**이고 번호는 무시한다. 아래 실행 결과가
:func:`skin_sheet.cells_by_clusters` 로 센 값이고, 라벨 개수와 **전부 일치**한다(검산).

★★ ② **소의 뿔이 프레임 번호 줄까지 솟는다**
---------------------------------------------
라벨을 세면 스킬 2 줄에서 **7개가 아니라 9개**가 나왔다 — 뿔 끝이 라벨 줄 높이까지
올라와 폭 3~4px 짜리 가짜 덩어리를 둘 만든다. :data:`skin_sheet.LABEL_MIN_W` (8px)를
새로 만들어 막았다. 진짜 라벨은 두 자리라도 8px 이상이다(실측 9~12px).
⚠ 이건 :data:`skin_sheet.LABEL_MAX_W` 의 **반대 방향** 함정이다 — 위쪽은 「이펙트가
  라벨에 붙어 커진 덩어리」, 아래쪽은 「그림 일부가 라벨 줄에 들어온 부스러기」다.

★★ ③ **스킬 2 줄만 라벨이 그림과 같은 y 에 걸친다**
----------------------------------------------------
다른 줄은 라벨 줄과 그림 줄이 떨어져 있는데(대기 44~58 / 78~212 …), 스킬 2 는
독기 구름이 위로 퍼져서 **라벨(≈765~778)이 그림 띠(768~865) 안에 들어온다**.
베일은 같은 상황에서 y0 를 글자 아래로 내렸지만(120절) 그러면 **구름 윗부분이 잘린다**.
여기서는 :func:`erase_label_patches` 로 **라벨 글자 자리만** 지운다 —
가로 19px x 세로 12px 짜리 창 7개라 구름 손실이 거의 없다.

방향 — **원본이 왼쪽을 본다**
=============================
아니사킬·고르도네 시트가 좌/우 라벨을 뒤집어 놓은 적이 있어(118절) **라벨을 믿지 않고
그림으로 확인**했다. 이 시트에는 좌/우 라벨 자체가 없으므로 머리 위치를 직접 재야 한다:

    대기 8프레임 · 프레임 폭에 대한 상대 위치 (0 = 왼쪽 끝)
      위쪽 띠(뿔·머리) 무게중심   0.367  ← **왼쪽에 쏠려 있다**
      아래쪽 띠(다리·주둥이)      0.557

뿔과 머리가 왼쪽에 몰려 있고 꼬리(끝에 종양 방울)가 오른쪽이다. 눈으로도 확인했다.
→ **원본이 곧 ``Left``** 이고 ``Right`` 는 미러다. ⚠ 베일·라린길과 **반대**다.

넣는 모션 · 안 넣는 원화
========================
| 시트 구획 | 스킨 칸 | 왜 |
|---|---|---|
| 1. 대기 (8) | ``idle`` | |
| 2. 이동 (7) | ``walk`` | 폴더 이름은 몬스터 규칙인 ``Move`` 다 |
| 3. 근거리 공격 (9) | ``attack`` | 「소 뿔로 찌르기」 |
| 4. 스킬 1 (5) | ``skill1`` | 「소름 끼치는 흉터」(2005) — 침식 |
| 5. 스킬 2 (7) | ``skill2`` | 「치명적인 독기」(2006) — 최대체력 비례 |
| 대시 위치 표시 (6) | ``skill1Fx`` | ★ 스킬 1 은 **적 1명을 지정**하는 기술이라 「타겟 위치 마커」가 맞다 |
| 독기 확산 (6) | ``skill2Fx`` | 시트가 *"스킬 2 - 범위 시각화"* 라고 적어놨다 |

⚠ **안 넣는 원화 셋** — 받을 칸이 `CharacterSkinSO` 에 없다. ``Unused_`` 접두사로
  남겨둔다(:class:`CharacterSkinBuilder` 가 그 접두사를 경고 없이 건너뛴다):

  · ``Unused_Turn`` (4) — 「방향 전환」. 이 프로젝트는 좌우 프레임을 **따로 들고 갈아끼우는**
    방식이라(``flipX`` 를 안 쓴다) 전환 중간 동작을 재생할 자리가 없다.
  · ``Unused_Skill1Recover`` (4) · ``Unused_Skill2Recover`` (4) — 「스킬 종료 후 경직」.
    ★ **스킬 프레임 뒤에 붙이지 않았다** — 스킬 모션은 시전 시간 동안 <b>반복 재생</b>되므로
    (``CharacterAnimator.PlaySkillMotion``) 경직 자세가 중간에 계속 끼어들어 어색해진다.
    「경직」은 한 번만 재생되는 별도 칸이 있어야 뜻이 산다.
  · ``Unused_DashTrail`` (4) — 「대시 돌진 잔상」. 바리올라의 두 스킬은 **제자리 원형**이라
    (표 ``range_type = Circle``) 돌진하지 않는다. 잔상을 ``skill1Projectile`` 에 넣어도
    ``BossSkillCaster`` 가 그 칸을 구속탄·이끌리는 혈취에서만 쓰므로 **영영 안 나온다** —
    안 나오는 자리에 배선해 두면 「배선했다」는 기록만 남는다.

사용법:  python Tools/variola_skin_build.py
다음:    유니티 메뉴 ``LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기``
         →  python Tools/measure_skin_tiles.py
"""

import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from vault_path import VAULT, PROJECT                                   # noqa: E402
from skin_sheet import (                                                # noqa: E402,F401
    PPU, SKIN_SPEC_NAME, write_skin_spec,
    load_sheet, cells_by_clusters, cells_by_labels, label_blobs, boxes_for, crop_rgba,
    body_anchor, base_anchor, compose, plant_feet, drop_stray_parts, write_png, ensure_folder_meta,
    shadow_in_box, reflood_background,
)

SRC = os.path.join(VAULT, "리소스", "sprites", "Variola_asset.png")

DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Variola", "Char")

SKIN_SPEC = {
    "skinAssetName": "Skin_Variola",
    # ⚠ 중립 에픽도 종마다 폴더 하나다 — `NeutralMonsterDefinitionSO.SkinResourcePath` 가
    #   `MonsterSkins/<종>/Skin_<종>` 을 찾는다(표의 `mon_skin` = Variola).
    "outputFolder": "Assets/_Project/Resources/MonsterSkins/Variola",
    "displayName": "바리올라",
    "framesPerSecond": "10",
    "attackFramesPerSecond": "12",
}

# ──────────────────────────────────────────────────────────────────────────
# 시트 좌표 — **실측**. 장수는 적지 않는다(세는 쪽이 정본 · 맨 위 ★★①).
#
#   (폴더, y0, y1, x0, x1, kind, 라벨 y0, 라벨 y1, split)
#     kind      "body" 면 발밑 그림자를 지운다 / "fx" 면 그대로 둔다
#     라벨 y     검산용 — 라벨 개수와 칸 개수가 다르면 죽는다.
#                None 이면 검산을 건너뛴다(라벨이 없는 구획).
#     split     칸을 가르는 방법. ★ **줄마다 다르다**:
#                 "clusters" 그림 덩어리 그대로 (프레임이 떨어져 있는 줄 — 기본)
#                 "labels"   프레임 번호 중심의 중간점 (덩어리가 붙어버리는 줄)
# ──────────────────────────────────────────────────────────────────────────
ROWS = [
    ("Idle",                 78,  212,   10, 1105, "body",  44,   58, "clusters"),
    # ⚠ x1 = 1105 — 오른쪽의 **설명 패널**(이름·칭호·스킬 글씨, x≥1133)을 안 먹게 막는다.
    ("Move",                309,  414,   10,  950, "body", 282,  296, "clusters"),
    ("Unused_Turn",         309,  414,  955, 1530, "body", 282,  296, "clusters"),
    ("MeleeAttack",         480,  568,   10, 1530, "body", 462,  476, "clusters"),
    ("Skill1",              630,  719,   10,  950, "body", 615,  629, "clusters"),
    ("Unused_Skill1Recover",630,  719,  960, 1530, "body", 615,  629, "clusters"),
    # ★★ 스킬 2 두 구획만 라벨이 그림 띠 안에 들어온다 (맨 위 ★★③) — 아래
    #    LABEL_OVERLAP 이 그 자리를 지운다. y0 는 **내리지 않는다**(구름을 살린다).
    #
    # ★★ 그리고 **스킬 2 본체만 `labels` 로 가른다** — 독기 구름이 프레임끼리 이어져
    #    덩어리가 <b>7개가 아니라 1개</b>로 붙는다(실측: 폭 809px 하나). 「붙은 덩어리를
    #    등분」하는 `cells_by_clusters` 의 뒷정리도 안 통한다 — 그건 중앙값을 자로 쓰는데
    #    비교할 다른 덩어리가 없다. 반면 **라벨은 7개가 깨끗하게 잡힌다**(위 ★★②를
    #    막은 뒤로) 그래서 라벨 중간점이 유일하게 믿을 수 있는 경계다.
    #    ⚠ 「경직」쪽은 구름이 없어 그냥 떨어지므로 `clusters` 그대로 둔다.
    ("Skill2",              768,  865,   10,  990, "body", 758,  772, "labels"),
    ("Unused_Skill2Recover",768,  865,  995, 1530, "body", 758,  772, "clusters"),
    ("Skill1Fx",            931, 1002,   10,  512, "fx",   912,  927, "clusters"),
    ("Unused_DashTrail",    931, 1002,  515,  925, "fx",   912,  927, "clusters"),
    ("Skill2Fx",            931, 1002,  930, 1530, "fx",   912,  927, "clusters"),
]

#: 라벨이 그림 띠 안으로 들어오는 구획 — 그 폴더만 :func:`erase_label_patches` 를 돌린다.
#: 값은 «지울 세로 범위» 다(라벨 글자가 실제로 걸치는 y).
LABEL_OVERLAP = {
    "Skill2":               (768, 779),
    "Unused_Skill2Recover": (768, 771),
}

#: 좌우 방향이 없는 묶음 — 파일 이름에 Right/Left 를 안 붙인다.
#: ⚠ 이펙트는 **방향이 없어야** 한다. 「타겟 위치 마커」·「독기 구름」은 회전 대칭이라
#:   좌우를 만들면 같은 그림이 두 벌 생긴다.
NO_DIRECTION = {"Skill1Fx", "Skill2Fx", "Unused_DashTrail"}

#: ★ 원본이 **왼쪽**을 본다 (맨 위 「방향」) — 베일·라린길과 반대다.
ORIGINAL_SIDE = "Left"

#: 라벨로 인정할 **최소 폭**(px) — 맨 위 ★★②. 이 시트의 진짜 라벨은 9~12px 이고
#: 소의 뿔이 만드는 가짜 덩어리는 3~4px 다.
#: ⚠ `skin_sheet` 의 기본값은 **1(무동작)** 이다 — 엘린 시트의 라벨은 3~7px 라서
#:   8 을 전역 기본값으로 두면 그쪽이 전멸한다. 그래서 여기서 넘긴다.
LABEL_MIN_W = 8


def erase_label_patches(sheet, centers, y0, y1, half=10):
    """
    프레임 번호 글자 자리만 그림 마스크에서 지운다 (맨 위 ★★③).

    <b>왜 창을 좁게 잡나</b> — 넓게 지우면 그 자리의 그림(독기 구름)까지 사라진다.
    라벨은 폭 9~12px 이라 ``half=10`` 이면 넉넉히 덮으면서 옆 칸은 안 건드린다.

    <b>왜 y0 를 내리는 대신 이걸 쓰나</b> — y0 를 라벨 아래로 내리면 **그 줄 전체**에서
    위 11픽셀이 잘린다. 베일은 그걸 감수했지만(담뱃대 끝 몇 px), 여기서는 잘리는 것이
    <b>스킬의 정체인 독기 구름</b>이라 손실이 눈에 띈다.

    :returns: 지운 픽셀 수 (0 이면 라벨이 그림과 안 겹쳤다는 뜻 — 좌표를 다시 볼 것).
    """
    before = int(sheet["mask"].sum())
    for cx in centers:
        sheet["mask"][y0:y1 + 1, max(0, cx - half):cx + half + 1] = False
    return before - int(sheet["mask"].sum())


def write_group(images, name):
    folder = os.path.join(DST_ROOT, name)
    n = 0
    for i, img in enumerate(images):
        if name in NO_DIRECTION:
            write_png(img, folder, "Char_%s_%02d" % (name, i))
            n += 1
        else:
            other = "Right" if ORIGINAL_SIDE == "Left" else "Left"
            write_png(img, folder, "Char_%s_%s_%02d" % (name, ORIGINAL_SIDE, i))
            write_png(img.transpose(Image.FLIP_LEFT_RIGHT), folder,
                      "Char_%s_%s_%02d" % (name, other, i))
            n += 2
    ensure_folder_meta(folder)
    return n


def build(sheet):
    made = 0
    for name, y0, y1, x0, x1, kind, ly0, ly1, split in ROWS:
        # ── 라벨 개수 = 검산의 정본 (맨 위 ★★①) ────────────────────────
        labels = None
        if ly0 is not None:
            labels = len(label_blobs(sheet["gray"], x0, x1, ly0, ly1,
                                     min_w=LABEL_MIN_W))

        # ── 라벨이 그림 띠에 걸치는 구획만 그 자리를 지운다 (★★③) ──────
        erased = 0
        if name in LABEL_OVERLAP:
            centers = [(a + b) // 2 for a, b in
                       label_blobs(sheet["gray"], x0, x1, ly0, ly1, min_w=LABEL_MIN_W)]
            ey0, ey1 = LABEL_OVERLAP[name]
            erased = erase_label_patches(sheet, centers, ey0, ey1)
            if erased == 0:
                raise SystemExit(
                    "⚠ %s: 라벨 자리를 지웠는데 픽셀이 하나도 안 줄었습니다 — "
                    "라벨이 그림과 안 겹칩니다. LABEL_OVERLAP 좌표를 다시 재세요." % name)

        if split == "labels":
            cells = cells_by_labels(sheet["gray"], x0, x1, ly0, ly1, min_w=LABEL_MIN_W)
        else:
            cells = cells_by_clusters(sheet["mask"], y0, y1, x0, x1)
        if not cells:
            raise SystemExit("⚠ %s: 칸을 하나도 못 찾았습니다 (y%d~%d)" % (name, y0, y1))

        if labels is not None and labels != len(cells):
            raise SystemExit(
                "⚠ %s: 프레임 번호 %d개인데 칸이 %d개로 갈렸습니다 (y %d~%d · x %d~%d). "
                "시트가 바뀌었을 수 있습니다 — 좌표를 다시 재세요."
                % (name, labels, len(cells), y0, y1, x0, x1))

        # ★ 몸통 줄만 발밑 그림자를 지운다 — 이펙트는 그 자체가 연출이다.
        gained = 0
        if kind == "body":
            rough = [b for b in boxes_for(sheet["mask"], cells, y0, y1) if b is not None]
            shadow = np.zeros(sheet["mask"].shape, dtype=bool)
            for b in rough:
                shadow |= shadow_in_box(sheet, b)
            sheet["mask"] &= ~shadow

            # ★★ 그림자를 지운 <b>다음에</b> 배경을 다시 흘린다 — 이 개체는 <b>네발</b>이라
            #    발밑 그림자가 <b>다리 사이를 막아</b> 배 아래 배경이 바깥과 끊긴다.
            #    끊긴 배경은 배경으로 안 잡혀 <b>불투명한 흰 웅덩이</b>로 남는다
            #    (배 밑에 흰 천이 붙은 것처럼 보였다). 자세한 근거는
            #    :func:`skin_sheet.reflood_background`.
            #    ⚠ 지금까지 캐릭터·보스는 전부 <b>두 발</b>이라 이 함정이 없었다.
            gained = reflood_background(sheet, shadow)

        boxes = [b for b in boxes_for(sheet["mask"], cells, y0, y1, name=name) if b is not None]
        frames = [crop_rgba(sheet, b) for b in boxes]
        anchor = body_anchor if kind == "body" else base_anchor
        if kind == "body":
            # ★★ <b>옆 프레임에서 들어온 떠 있는 조각을 뗀다</b>
            #   (:func:`skin_sheet.drop_stray_parts` 의 ★★ · 2026-08-22).
            #   유저 리포트: *"이동 모션 사이 사이에 전 동작 모션과 함께 짤려 들어가서
            #   어색해지는 부분들"*.
            frames = [drop_stray_parts(f)[0] for f in frames]
        anchors = [anchor(f) for f in frames]
        if kind == "body":
            # ★★ <b>발을 피벗에 맞춘다</b> — 모션이 바뀔 때 옆으로 미끄러지지 않게
            #   (:func:`skin_sheet.plant_feet` 의 ★★). 묶음 안의 움직임은 그대로 둔다.
            anchors, _shift = plant_feet(frames, anchors)
        images, w, h = compose(frames, anchors)

        made += write_group(images, name)
        note = "" if not erased else "  (라벨 자리 %dpx 지움)" % erased
        if gained:
            note += "  (다리 사이 배경 %dpx 회수)" % gained
        print("  %-22s %3d x %3d · %2d장 · 라벨 %s · %-8s 폭 %s%s"
              % (name, w, h, len(images),
                 "-" if labels is None else str(labels), split,
                 [e - s + 1 for s, e in cells], note))

    return made


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print("[바리올라 모션 시트 분해]")

    # 구획 사각 테두리를 배경 판정보다 먼저 지운다(상자가 있는 시트에서는 필수).
    sheet = load_sheet(SRC, box_borders=True)

    n = build(sheet)
    spec = write_skin_spec(DST_ROOT, SKIN_SPEC, "Tools/variola_skin_build.py")
    ensure_folder_meta(DST_ROOT)
    ensure_folder_meta(os.path.dirname(DST_ROOT))
    print("  스킨 설정 %s (%d줄)" % (SKIN_SPEC_NAME, spec))
    print("  → 프레임 %d장" % n)
    print("  다음: 유니티 메뉴 LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기")


if __name__ == "__main__":
    main()
