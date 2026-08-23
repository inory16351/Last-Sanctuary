# -*- coding: utf-8 -*-
"""시카리아(캐릭터 9007) 모션 시트 → 프레임 분해 (2026-08-20).

원본 — **한 장이다**
--------------------
``<볼트>/리소스/sprites/Char_Asset_Sicaria.png``  (1536x1024)

시그리드는 두 장이 서로 없는 줄을 갖고 있어 줄 단위로 골라야 했지만, 시카리아는
**한 장에 일곱 줄이 다 들어 있다.** 고를 것이 없다.

시트 구조 — 구획 박스 테두리가 있다(시그리드와 같다)
---------------------------------------------------
:func:`skin_sheet.load_sheet` 를 ``box_borders=True`` 로 부른다. 안 그러면 테두리가
잉크로 잡혀 칸 경계가 통째로 무너진다.

가로 띠는 다섯 벌이고 각 벌이 「제목 → 그림 → 프레임 번호」 세 줄로 되어 있다(실측):

| 제목 | 그림 | 번호 |
|---|---|---|
| 27~45 | **63~147** | 164~175 |
| 210~228 | **258~338** | 354~365 |
| 402~420 | **443~525** | 542~553 |
| 588~606 | **620~718** | 735~746 |
| 779~797 | **806~955** | 982~993 |

⚠ 제목 줄에는 「■ 대기 모션 (10프레임)」 같은 **한글 제목**이 들어 있다. 그림 밴드가
제목과 확실히 갈라져 있으므로 그대로 쓰면 되지만, **밴드를 넓히면 제목이 프레임에 섞인다**
(히스톤이 실제로 그랬다 — 84-1절).

★ 위 두 벌은 **한 줄에 구획이 둘**이다 — 실측한 빈 열에서 가른다:
    1벌  대기(x 22~860)      | 투사체(x 880~1515)     ← 빈 열 834~901
    2벌  이동(x 22~730)      | 원거리 공격(x 740~1515) ← 빈 열 720~740

★ **프레임 수가 제목과 다르다** — 제목을 믿으면 안 된다
-------------------------------------------------------
제목은 「10프레임」이라고 적혀 있는데 실제로 그려진 칸은 다르다. 원화가 정본이므로
**실측한 라벨 수**를 기대값으로 박는다(어긋나면 스크립트가 죽는다):

    대기 10 · 투사체 7 · 이동 **9** · 원거리 **9** · 근거리 12 · 스킬 시전 12 · 스킬 이펙트 **9**

번호가 건너뛴다(이동은 6번, 원거리는 9번, 이펙트는 9번이 없다) — 유저가 그 칸을
안 그린 것이고, 재생에는 지장이 없다(등간격으로 도는 낱장 목록일 뿐이다).

무엇이 어디로 가나
------------------
| 폴더 | 장수 | 스킨 칸 |
|---|---|---|
| ``Char/Idle`` | 10 | ``idleRight``·``idleLeft`` |
| ``Char/Walk`` | 9 | ``walkRight``·``walkLeft`` |
| ``Char/MeleeAttack`` | 12 | ``attackRight``·``attackLeft`` |
| ``Char/RangedAttack`` | 9 | ``rangedRight``·``rangedLeft`` |
| ``Char/Projectile`` | 7 | ``projectileFrames`` — 은화살 |
| ``Char/Skill1`` | 12 | ``skill1Right``·``skill1Left`` — 「애로우 레인」 시전 |
| ``Char/Skill1Fx`` | 9 | ``skill1Fx`` — 화살비가 떨어지는 자리 |

★ **스킬 칸을 실제로 쓴다** — 시그리드와 다른 점이다.
시그리드의 스킬 셋은 전부 패시브라 「시전」 시점이 없어 ``Unused_`` 로 남겼다. 시카리아의
「애로우 레인」(80021)은 **쿨타임 25초짜리 발동형**이라 시전 순간이 있다 —
:meth:`CharacterAnimator.PlaySkillMotion` 이 슬롯 0 을 재생하고
``skill1Fx`` 가 착탄 지점에 깔린다(보스가 쓰던 경로를 그대로 탄다).

★ 투사체 — ``impactFrames`` 는 비운다
--------------------------------------
은화살은 **날아가는 화살 그림**이다(시그리드의 별처럼 «커지다 터지는» 그림이 아니다).
착탄 원화가 따로 없으므로 ``impactFrames`` 를 비우고 ``groundImpactOnly`` 를 끈다 —
날아가는 그림이 실재하기 때문이다.

사용법:  py -3 Tools/sicaria_skin_build.py
다음:    유니티 메뉴 **LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기**
        그 다음 `py -3 Tools/measure_skin_tiles.py`
"""

import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT

from skin_sheet import (  # noqa: F401
    SKIN_SPEC_NAME, write_skin_spec,
    load_sheet, label_count, cells_by_labels,
    boxes_for, crop_rgba, body_anchor, base_anchor, compose, plant_feet, drop_stray_parts,
    write_png, ensure_folder_meta, shadow_in_box, enclosed_background,
    resample_rgba, head_pixels,
)

SRC = os.path.join(VAULT, "리소스", "sprites", "Char_Asset_Sicaria.png")

DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Sicaria", "Char")

#: 스킨 에셋의 «값» 칸 (원화만 봐서는 알 수 없는 것).
SKIN_SPEC = {
    "skinAssetName": "Skin_Sicaria",
    "displayName": "시카리아",
    "framesPerSecond": "10",
    "attackFramesPerSecond": "14",
    # 날아가는 화살 원화가 실재한다 — 지면 착탄만 그리는 폴백을 쓰지 않는다.
    "groundImpactOnly": "0",
    # 화살은 가늘고 길다. 한 타일 남짓으로 그린다.
    "projectileWidthTiles": "1.0",
}

# ──────────────────────────────────────────────────────────────────────────
#   name  : 폴더 이름 (= 스킨 칸 · 빌더의 Slots 표가 대응을 안다)
#   ly0,ly1 : 프레임 번호 줄 / y0,y1 : 그림 줄 / x0,x1 : 구획
#   expect: 기대 장수 — 다르면 바로 죽는다 (맨 위 ★)
#   kind  : "body" 몸통(그림자 지우기 + 몸통 중심 정렬) / "fx" 연출(밑동 정렬)
# ──────────────────────────────────────────────────────────────────────────
ROWS = [
    ("Idle",         164, 175,  63, 147,   22,  860, 10, "body"),
    ("Projectile",   164, 175,  63, 147,  880, 1515,  7, "fx"),
    ("Walk",         354, 365, 258, 338,   22,  730,  9, "body"),
    ("RangedAttack", 354, 365, 258, 338,  740, 1515,  9, "body"),
    ("MeleeAttack",  542, 553, 443, 525,   22, 1500, 12, "body"),
    ("Skill1",       735, 746, 620, 718,   22, 1520, 12, "body"),
    ("Skill1Fx",     982, 993, 806, 955,   22, 1490,  9, "fx"),
]

#: ★★ 「갇힌 배경」 **두 번째 통과** — (면적, 테두리 광도) (2026-08-21)
#:
#: 유저 지시: *"시카리아가 공격할 때 활 안에 흰색 이미지가 씌워져있어 이거 투명으로
#: 변경해줘 / 이동할 때 시카리아 다리 뒤에 흰색 이미지"*.
#:
#: **무엇이 어긋났나** — 아래 <c>enclosed_background</c> 를 <b>이미 부르고 있었는데</b>
#: 기본값 ``(300, 120)`` 이라 **면적에서 걸러졌다**. 실측(원본 시트 기준):
#:
#:     활 안쪽      39~197px · 테두리 광도 39~58   ← 배경이다. 지워야 한다
#:     다리 사이    53~250px · 테두리 광도 25~40   ← 배경이다. 지워야 한다
#:     후드 정수리  44~ 81px · 테두리 광도 104~156 ← **그림이다. 남아야 한다**
#:     날개-몸통 띠          · 테두리 광도  67~ 88 ← **그림이다. 남아야 한다**
#:
#: ⚠⚠ **면적만 낮추면 안 된다** — ``(40, 120)`` 으로 구워 보니 이동 9장 전부
#:   **후드 정수리에 구멍이 뚫렸다**(테두리 104~156 이 120 아래로 들어온다).
#:   그래서 **테두리를 60 으로 조인 별도 통과**를 더해 <b>합집합</b>을 쓴다.
#: ⚠ **기존 통과를 지우면 안 된다** — 근거리 6번(1220px·테두리 60.2)·8번(701px·테두리
#:   103.6)은 테두리가 60 보다 밝아 새 통과로는 안 잡힌다. 둘 다 필요하다.
#: ★ 값 60/60 은 지어낸 것이 아니라 이 프로젝트의 표준이다 —
#:   ``char_sheet.py`` 의 기본값 ``pocket=(60, 60)`` · ``aru_golem_skin_build.py`` 와 같다.
#:   시카리아는 «옛 7명» 의 개별 스크립트라 그 값이 안 내려와 있었다.
#: ⚠ 여유가 넓지 않다 — 가장 빡빡한 것(원거리 4번 아래쪽 테두리 58.4)과 남겨야 할 것
#:   (62.0)이 60 을 사이에 두고 ±1.6 이다. 시트 원화가 바뀌면 이 표를 다시 재야 한다.
POCKET_TIGHT = (40, 60)

#: 좌우 방향이 **없는** 한 벌짜리 묶음.
#: 투사체는 +X 를 향한 한 벌만 있으면 코드가 방향으로 돌리고, 스킬 이펙트는 바닥 그림이다.
NO_DIRECTION = {"Projectile", "Skill1Fx"}

#: 원본이 바라보는 쪽. ⚠ 줄마다 다를 수 있다(시그리드는 이동만 왼쪽이었다 — 113-6절 규칙).
#:   :func:`report_tilt` 가 «머리가 발보다 어느 쪽에 있는가»를 재서 찍어준다 —
#:   달릴 때는 머리가 앞선다. 실측 결과 시카리아는 **전부 오른쪽**이었다.
ORIGINAL_SIDE = {}
DEFAULT_ORIGINAL_SIDE = "Right"

#: 크기 정규화의 기준 모션 (``measure_skin_tiles.py`` 가 대기만 재기 때문에 대기다).
SCALE_REFERENCE = "Idle"
SCALE_MIN, SCALE_MAX = 0.70, 1.80


def measure_head_scale(collected):
    """모션마다 **대기 대비 몇 배로 늘려야 하는지** (시그리드와 같은 이유·같은 방법).

    게임의 크기 기준(``contentSizeTiles``)은 **대기 원화 하나만** 재고 그 배율이 모든
    모션에 곱해진다. 줄마다 그린 크기가 다르면 모션이 바뀔 때 몸이 커졌다 작아진다.
    「키」가 아니라 **머리 면적**으로 재는 이유는 :func:`skin_sheet.head_pixels` 참조
    (활·망토가 상자를 늘리고, 자세에 따라 키가 줄어드는 것은 연출이다).
    """
    areas = {}
    for name, frames in collected.items():
        vals = [head_pixels(f) for f in frames]
        vals = [v for v in vals if v > 60]
        if vals:
            areas[name] = float(np.mean(vals))

    ref = areas.get(SCALE_REFERENCE)
    if not ref:
        print("  ⚠ 기준 모션(%s)의 머리를 못 재 크기 정규화를 건너뜁니다" % SCALE_REFERENCE)
        return {}

    factors = {}
    print("  [크기 정규화] 머리 면적 → 대기(%s) 기준 배율" % SCALE_REFERENCE)
    for name, area in sorted(areas.items()):
        f = (ref / area) ** 0.5
        factors[name] = f
        flag = "" if SCALE_MIN <= f <= SCALE_MAX else "  ← ⚠ 범위 밖"
        print("    %-16s 머리 %6.1f px  →  x%.3f%s" % (name, area, f, flag))

    bad = {k: v for k, v in factors.items() if not (SCALE_MIN <= v <= SCALE_MAX)}
    if bad:
        raise SystemExit(
            "⚠ 크기 배율이 안전 범위(%.2f~%.2f)를 벗어났습니다: %s\n"
            "   머리 판정이 틀렸거나 시트가 바뀌었습니다." % (SCALE_MIN, SCALE_MAX, bad))
    return factors


def report_tilt(name, frames):
    """머리와 발의 **가로 차이**. 양수면 머리가 오른쪽(= 오른쪽을 본다).

    시그리드에서 «이동만 왼쪽을 본다»를 잡아낸 측정이다(113-6절이 세운 규칙). 여기서는
    **찍기만 한다** — 판단은 사람이 하고 결과를 :data:`ORIGINAL_SIDE` 에 적는다.
    """
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
    if vals:
        print("    %-16s 머리−발 %+6.1f px  (양수 = 오른쪽)" % (name, float(np.mean(vals))))


def build(sheet):
    """두 번 훑는다 — 배율의 기준(대기)을 다 재기 전에는 다른 줄의 배율을 모른다."""
    cut = []
    collected = {}

    for name, ly0, ly1, y0, y1, x0, x1, expect, kind in ROWS:
        cells = cells_by_labels(sheet["gray"], x0, x1, ly0, ly1)
        labels = label_count(sheet["gray"], x0, x1, ly0, ly1)
        if labels != expect or len(cells) != expect:
            raise SystemExit(
                "⚠ %s: 프레임 번호 %d개 · 칸 %d개인데 %d장을 기대했습니다 "
                "(라벨 y%d~%d · 그림 y%d~%d · x%d~%d). 시트가 바뀌었으면 좌표를 다시 재세요."
                % (name, labels, len(cells), expect, ly0, ly1, y0, y1, x0, x1))

        # ★★ **갇힌 배경**을 먼저 되돌린다 (엘린과 같은 순서 · skin_sheet 의 POCKET_* 주석).
        #   이어짐으로 배경을 정하므로 <b>닫힌 선 안에 갇힌 흰 구역</b>(치맛자락 안·활과 몸
        #   사이)은 배경으로 안 잡혀 «흰 판때기» 로 남는다. 이 덩어리가 남아 있으면 경계
        #   상자가 커져 아래 그림자 띠도 엉뚱한 자리에 걸린다 — 그래서 순서가 먼저다.
        if kind == "body":
            for _cx0, _cx1 in cells:
                # ★★ <b>두 통과의 합집합</b>을 쓴다 (2026-08-21 · 아래 POCKET_TIGHT 주석).
                _pockets = (
                    enclosed_background(sheet, y0, y1, _cx0, _cx1) |
                    enclosed_background(sheet, y0, y1, _cx0, _cx1,
                                        min_area=POCKET_TIGHT[0],
                                        ring_lum=POCKET_TIGHT[1]))
                if _pockets.any():
                    sheet["mask"] &= ~_pockets

        # ★ 몸통 줄만 발밑 그림자를 지운다 — 이펙트는 그 자체가 지면 연출이다.
        if kind == "body":
            rough = [b for b in boxes_for(sheet["mask"], cells, y0, y1) if b is not None]
            shadow = np.zeros(sheet["mask"].shape, dtype=bool)
            for b in rough:
                shadow |= shadow_in_box(sheet, b)
            sheet["mask"] &= ~shadow

        boxes = [b for b in boxes_for(sheet["mask"], cells, y0, y1, name=name) if b is not None]
        frames = [crop_rgba(sheet, b) for b in boxes]

        cut.append((name, kind, labels, frames))
        if kind == "body":
            collected[name] = frames

    print("  [방향 실측]")
    for name, kind, _labels, frames in cut:
        if kind == "body":
            report_tilt(name, frames)

    factors = measure_head_scale(collected)

    made = 0
    for name, kind, labels, frames in cut:
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

        folder = os.path.join(DST_ROOT, name)
        if name in NO_DIRECTION:
            for i, img in enumerate(images):
                write_png(img, folder, "Char_%s_%02d" % (name, i))
                made += 1
            note = "방향 없음"
        else:
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
        print("  %-14s %3d x %3d · %2d장 · 라벨 %d개 · %s%s"
              % (name, w, h, len(images), labels, note, scale_note))
    return made


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print("[시카리아 모션 시트 분해]")

    # ★★ 구획 사각 테두리를 배경 판정보다 먼저 지운다 — 안 그러면 상자 안쪽이 갇혀 잉크가 된다.
    sheet = load_sheet(SRC, box_borders=True)
    n = build(sheet)

    spec = write_skin_spec(DST_ROOT, SKIN_SPEC, "Tools/sicaria_skin_build.py")
    ensure_folder_meta(DST_ROOT)
    print("  스킨 설정 %s (%d줄)" % (SKIN_SPEC_NAME, spec))
    print("  → 프레임 %d장" % n)
    print("  다음: 유니티 메뉴 LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기")


if __name__ == "__main__":
    main()
