# -*- coding: utf-8 -*-
"""아루의 소환수 「강림(Dawn)」 골렘 모션 시트 → 프레임 분해 (2026-08-20).

원본: ``<볼트>/리소스/sprites/Aru_dawn_asset.png`` (1536x1024)

**왜 아루와 다른 스크립트인가** — 골렘은 아루의 모션이 아니라 **별개의 유닛**이다.
표(``skill_type_desc_Dawn``)가 그렇게 규정한다: 자기 능력치로 싸우고, 로스터에 뜨고,
부대에 배정되고, 죽으면 그때부터 아루의 쿨타임이 돈다. 그래서 스킨 에셋도 따로 만든다
(``Skin_AruGolem``) — 한 스킨에 두 유닛의 모션을 담으면 재생 코드가 골라 쓸 방법이 없다.

시트 구조 — 실측 (제목 → 밑줄 → 그림 → 번호)
--------------------------------------------
| 줄 | 그림 y | 번호 y | x | 장수 |
|---|---|---|---|---:|
| 소환 (Summon) | 53~218 | 237~247 | 5~785 | 8 |
| 대기 (Idle) | 326~458 | 479~488 | 5~785 | **5** |
| 이동 (Move) | 564~676 | 697~706 | 5~785 | 8 |
| 근거리 공격 | 790~940 | 952~961 | **5~1530** | 12 |
| 사망 (Death) | 64~229 | 237~247 | 790~1530 | **7** |
| 근거리 이펙트 ① | 360~514 | 533~542 | 790~1530 | 4 |
| 근거리 이펙트 ② | 569~723 | 737~747 | 790~1530 | 4 |

★ **근거리 공격 줄만 시트 폭을 다 쓴다** — 왼쪽 단이 아니라 두 단에 걸쳐 12칸이다.
★ **대기는 5장, 사망은 7장**이다(사망은 원화에 6번이 없다). 제목의 프레임 수를 믿지 말 것 —
  :data:`ROWS` 의 ``expect`` 는 **라벨을 실측한 값**이고, 어긋나면 스크립트가 죽는다.
★ **근거리 이펙트는 두 줄이 한 벌**이다(1~4 위 · 5~8 아래). 한 폴더로 합쳐 굽는다.

★★ 근거리·사망은 **경계를 손으로 박았다** (히스톤 84-1절 · 아루와 같은 이유)
-----------------------------------------------------------------------------
황금 궤적과 부서지는 돌덩이가 옆 칸까지 뻗어 ① 빈 열이 거의 없고 ② 라벨 간격이
프레임마다 다르다(101~171px). 잉크가 가장 얇은 열을 실측해 박았다:

    근거리  106(0) · 218(0) · 319(0) · 417(0) · 594(0) · 704(10) · 856(**24**) ·
            1027(1) · 1158(0) · 1252(0) · 1370(0)
    사망    881(0) · 978(0) · 1077(0) · 1178(0) · 1282(0) · 1399(0)

⚠ 근거리의 856 은 **완전히 비지 않는다**(24px). 7~9번 프레임의 궤적이 서로 겹쳐 그려져
  있어 어디를 잘라도 무언가는 지나간다 — 800~940 을 전부 재 봐도 최소가 24 다.
  그중 가장 얇은 곳을 골랐다.
⚠ 원화를 다시 받으면 이 두 표를 **반드시 다시 재야 한다.**

무엇이 어디로 가나
------------------
| 폴더 | 장수 | 스킨 칸 |
|---|---:|---|
| ``Char/Idle`` | 5 | ``idleRight``·``idleLeft`` |
| ``Char/Walk`` | 8 | ``walkRight``·``walkLeft`` |
| ``Char/MeleeAttack`` | 12 | ``attackRight``·``attackLeft`` |
| ``Char/MeleeTravelFx`` | 8 | ``meleeTravelFrames`` — 휘두른 궤적 + 지면 충격 |
| ``Char/Summon`` | 8 | ``summonRight``·``summonLeft`` — ★ 신설 칸 |
| ``Char/Death`` | 7 | ``deathRight``·``deathLeft`` — ★ 신설 칸 |

사용법:  py -3 Tools/aru_golem_skin_build.py
다음:    유니티 메뉴 **LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기**
"""

import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT

from skin_sheet import (  # noqa: F401
    SKIN_SPEC_NAME, write_skin_spec,
    load_sheet, cells_by_labels,
    boxes_for, boxes_dominant, crop_rgba, body_anchor, base_anchor, compose, plant_feet, drop_stray_parts,
    write_png, ensure_folder_meta, shadow_in_box, enclosed_background,
    resample_rgba, head_pixels,
)

SRC = os.path.join(VAULT, "리소스", "sprites", "Aru_dawn_asset.png")

DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_AruGolem", "Char")

SKIN_SPEC = {
    "skinAssetName": "Skin_AruGolem",
    "displayName": "강림한 골렘",
    "framesPerSecond": "10",
    "attackFramesPerSecond": "14",
    # 투사체가 없다 — 순수 근접 유닛이다.
    "groundImpactOnly": "0",
    # 휘두른 궤적이 목표 쪽으로 뻗는다. 골렘 몸집(약 2타일)에 맞춰 조금 크게 둔다.
    "meleeTravelWidthTiles": "1.6",
}

#: 손으로 잰 칸 경계 (맨 위 ★★).
MELEE_BOUNDS = [26, 106, 218, 319, 417, 594, 704, 856, 1027, 1158, 1252, 1370, 1487]
DEATH_BOUNDS = [800, 881, 978, 1077, 1178, 1282, 1399, 1522]

# ──────────────────────────────────────────────────────────────────────────
#   name / kind / y0,y1 / x0,x1 / cells / expect
# ──────────────────────────────────────────────────────────────────────────
ROWS = [
    ("Summon",        "body",  53, 218,    5,  785, ("labels", 237, 247), 8),
    ("Idle",          "body", 326, 458,    5,  785, ("labels", 479, 488), 5),
    ("Walk",          "body", 564, 676,    5,  785, ("labels", 697, 706), 8),
    ("MeleeAttack",   "body", 790, 940,    5, 1530, ("bounds", "melee"), 12),
    ("Death",         "body",  64, 229,  790, 1530, ("bounds", "death"),  7),
    # 근거리 이펙트 — 두 줄이 한 벌. 폴더는 하나로 합친다(아래 ``FX_MERGE``).
    ("MeleeTravelFxA", "fx",  360, 514,  790, 1530, ("labels", 533, 542), 4),
    ("MeleeTravelFxB", "fx",  569, 723,  790, 1530, ("labels", 737, 747), 4),
]

#: 두 줄을 이어 한 폴더로 굽는다 — 이름 → (폴더, 시작 번호).
FX_MERGE = {"MeleeTravelFxA": ("MeleeTravelFx", 0),
            "MeleeTravelFxB": ("MeleeTravelFx", 4)}

NO_DIRECTION = {"MeleeTravelFxA", "MeleeTravelFxB"}

DEFAULT_ORIGINAL_SIDE = "Right"
ORIGINAL_SIDE = {}

SCALE_REFERENCE = "Idle"
SCALE_MIN, SCALE_MAX = 0.70, 1.80

#: 옆 칸 조각을 끊는 간격 비율 (아루와 같은 값 — 같은 이유).
DOMINANT_JOIN = 0.06
#: ⚠ 경계를 손으로 잰 줄은 «가운데 덩어리» 규칙을 쓰지 않는다 — 궤적이 몸에서 떨어져
#:   그려진 프레임이 있어 그 규칙이 궤적을 남의 것으로 보고 버린다.
DOMINANT_SKIP = {"MeleeAttack", "Death"}


def cells_for(sheet, spec, x0, x1):
    kind = spec[0]
    if kind == "labels":
        return cells_by_labels(sheet["gray"], x0, x1, spec[1], spec[2])
    if kind == "bounds":
        b = MELEE_BOUNDS if spec[1] == "melee" else DEATH_BOUNDS
        return [(b[i], b[i + 1] - 1) for i in range(len(b) - 1)]
    raise SystemExit("알 수 없는 칸 가르기: %r" % (spec,))


def report_tilt(name, frames):
    vals = []
    for f in frames:
        a = np.asarray(f)[:, :, 3] > 8
        ys = np.where(a.any(axis=1))[0]
        if len(ys) < 8:
            continue
        h = ys[-1] - ys[0] + 1
        head = a[ys[0]:ys[0] + max(1, h // 4)]
        foot = a[ys[-1] - max(1, h // 5):ys[-1] + 1]
        if not head.any() or not foot.any():
            continue
        hx = np.average(np.arange(a.shape[1]), weights=head.sum(axis=0))
        fx = np.average(np.arange(a.shape[1]), weights=foot.sum(axis=0))
        vals.append(hx - fx)
    v = float(np.mean(vals)) if vals else 0.0
    print("    %-16s 머리−발 %+6.1f px  (양수 = 오른쪽)" % (name, v))


def measure_scale(collected):
    """
    ★★ 골렘은 **키**로 잰다 — 캐릭터들과 다른 기준이다.

    시그리드·시카리아·아루는 «머리 면적»으로 쟀다. 지팡이·활·낫이 상자를 늘려 키를
    못 믿기 때문이다(:func:`skin_sheet.head_pixels`). 골렘은 사정이 반대다:

      · 들고 있는 것이 없어 <b>상자가 곧 몸</b>이다 — 키가 그대로 크기다.
      · 이동 원화가 <b>옆을 보고 웅크린</b> 자세라 머리가 정면보다 훨씬 작게 잡힌다.
        실측: 머리 면적 기준 <b>x1.42</b> · 키 기준 <b>x1.17</b>. 원화를 나란히 놓고 보면
        1.17 쪽이 맞다 — 1.42 로 굽으면 걸을 때 골렘이 대기보다 <b>커진다</b>.

    ⚠ 소환·사망은 아예 기준에서 뺀다(:func:`main`) — 돌무더기에서 자라나고 무너지는
      그림이라 어느 기준으로도 «같은 크기» 가 아니다. 원화 그대로 굽는 것이 맞다.
    """
    heights = {}
    for name, frames in collected.items():
        heights[name] = float(np.median([f.shape[0] for f in frames]))

    ref = heights.get(SCALE_REFERENCE)
    if not ref:
        print("  ⚠ 기준 모션(%s)을 못 재 크기 정규화를 건너뜁니다" % SCALE_REFERENCE)
        return {}

    factors = {}
    print("  [크기 정규화] 상자 높이 → 대기 기준 배율")
    for name, area in sorted(heights.items()):
        f = ref / area
        factors[name] = f
        flag = "" if SCALE_MIN <= f <= SCALE_MAX else "  ← ⚠ 범위 밖"
        print("    %-16s 높이 %6.1f px  →  x%.3f%s" % (name, area, f, flag))

    bad = {k: v for k, v in factors.items() if not (SCALE_MIN <= v <= SCALE_MAX)}
    if bad:
        raise SystemExit("⚠ 크기 배율이 안전 범위(%.2f~%.2f)를 벗어났습니다: %s"
                         % (SCALE_MIN, SCALE_MAX, bad))
    return factors


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print("[강림 골렘 모션 시트 분해]")
    sheet = load_sheet(SRC, box_borders=True)

    cut = []
    collected = {}
    for name, kind, y0, y1, x0, x1, spec, expect in ROWS:
        cells = cells_for(sheet, spec, x0, x1)
        if len(cells) != expect:
            raise SystemExit(
                "⚠ %s: 칸이 %d개인데 %d개를 기대했습니다 (그림 y%d~%d · x%d~%d · %s)."
                % (name, len(cells), expect, y0, y1, x0, x1, spec[0]))

        # ★★ **갇힌 배경**을 먼저 되돌린다 (엘린과 같은 순서 · skin_sheet 의 POCKET_* 주석).
        #
        #   이어짐으로 배경을 정하므로 <b>닫힌 선 안에 갇힌 흰 구역</b>은 배경으로 안 잡힌다.
        #   실제로 그렇게 나왔다 — 골렘의 <b>두 다리 사이</b>, 시카리아의 <b>치맛자락 안</b>이
        #   흰 덩어리로 남아 게임에서 «흰 판때기» 로 보인다.
        #
        #   ⚠ 순서가 중요하다: 이 흰 덩어리가 남아 있으면 경계 상자가 커져 있어서
        #     아래 그림자 띠가 엉뚱한 자리에 걸린다.
        if kind == "body":
            for _cx0, _cx1 in cells:
                _pockets = enclosed_background(sheet, y0, y1, _cx0, _cx1,
                                              min_area=60, ring_lum=60)
                if _pockets.any():
                    sheet["mask"] &= ~_pockets

        if kind == "body":
            rough = [b for b in boxes_for(sheet["mask"], cells, y0, y1) if b is not None]
            shadow = np.zeros(sheet["mask"].shape, dtype=bool)
            for b in rough:
                shadow |= shadow_in_box(sheet, b)
            sheet["mask"] &= ~shadow

        if kind == "body" and name not in DOMINANT_SKIP:
            raw = boxes_dominant(sheet["mask"], cells, y0, y1, min_ink_ratio=DOMINANT_JOIN, name=name)
        else:
            raw = boxes_for(sheet["mask"], cells, y0, y1)
        boxes = [b for b in raw if b is not None]
        if len(boxes) != len(cells):
            raise SystemExit("⚠ %s: 빈 칸이 %d개 있습니다." % (name, len(cells) - len(boxes)))

        frames = [crop_rgba(sheet, b) for b in boxes]
        cut.append((name, kind, frames))
        # ⚠ 소환·사망은 배율 기준에서 뺀다 — 돌무더기에서 «자라나는» 그림이라
        #   머리 판정이 프레임마다 크게 흔들린다.
        if kind == "body" and name not in ("Summon", "Death"):
            collected[name] = frames

    print("  [방향 실측]")
    for name, kind, frames in cut:
        if kind == "body":
            report_tilt(name, frames)

    factors = measure_scale(collected)

    made = 0
    for name, kind, frames in cut:
        factor = factors.get(name, 1.0)
        if abs(factor - 1.0) > 0.002:
            frames = [resample_rgba(f, factor) for f in frames]

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

        if name in FX_MERGE:
            folder_name, start = FX_MERGE[name]
            folder = os.path.join(DST_ROOT, folder_name)
            for i, img in enumerate(images):
                write_png(img, folder, "Char_%s_%02d" % (folder_name, start + i))
                made += 1
            note = "이어 붙임 (%d~)" % start
        elif name in NO_DIRECTION:
            folder = os.path.join(DST_ROOT, name)
            for i, img in enumerate(images):
                write_png(img, folder, "Char_%s_%02d" % (name, i))
                made += 1
            note = "방향 없음"
        else:
            folder = os.path.join(DST_ROOT, name)
            orig = ORIGINAL_SIDE.get(name, DEFAULT_ORIGINAL_SIDE)
            other = "Left" if orig == "Right" else "Right"
            for i, img in enumerate(images):
                write_png(img, folder, "Char_%s_%s_%02d" % (name, orig, i))
                write_png(img.transpose(Image.FLIP_LEFT_RIGHT), folder,
                          "Char_%s_%s_%02d" % (name, other, i))
                made += 2
            note = "%s 원본 + 미러" % ("오른쪽" if orig == "Right" else "★왼쪽")
        ensure_folder_meta(folder)

        scale_note = "" if abs(factor - 1.0) <= 0.002 else "  (크기 x%.3f)" % factor
        print("  %-16s %3d x %3d · %2d장 · %s%s" % (name, w, h, len(images), note, scale_note))

    spec = write_skin_spec(DST_ROOT, SKIN_SPEC, "Tools/aru_golem_skin_build.py")
    ensure_folder_meta(DST_ROOT)
    print("  스킨 설정 %s (%d줄)" % (SKIN_SPEC_NAME, spec))
    print("  → 프레임 %d장" % made)


if __name__ == "__main__":
    main()
