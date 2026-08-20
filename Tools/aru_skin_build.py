# -*- coding: utf-8 -*-
"""아루(캐릭터 9008) 모션 시트 → 프레임 분해 (2026-08-20).

원본 — **세 장이 왔는데 쓸 것은 두 장이다**
-------------------------------------------
``<볼트>/리소스/sprites/Aru_asset_01.png``  (1536x1024)  ← 본체 전부
``<볼트>/리소스/sprites/Aru_asset_02.png``  **``_01`` 과 바이트까지 같은 파일**(md5 동일)
``<볼트>/리소스/sprites/Aru_asset_03.png``  (1536x1024)  ← 「회복 받은 아군 이펙트」 큰 판
``<볼트>/리소스/sprites/Aru_dawn_asset.png``            ← 골렘. **다른 유닛이라 다른 스크립트**

⚠ ``_02`` 를 «두 번째 판본» 으로 착각하면 안 된다 — md5 를 재 보면 ``_01`` 과 같다.
  시그리드처럼 «줄마다 좋은 쪽을 고르는» 작업이 여기서는 아예 필요 없다.

★★ 이 시트의 함정 — **한 줄 안에 모션과 이펙트가 섞여 있다**
-------------------------------------------------------------
칸 수를 세어 그대로 쓰면 **캐릭터가 사라지고 마법진이 대신 재생된다.** 실측:

| 줄 | 칸 | 실제 내용 |
|---|---:|---|
| 원거리 공격 | 8 | 1~6 **캐릭터** · 7~8 <b>날아가는 투사체</b> |
| 마법 공격 | 8 | 1~7 **캐릭터** · 8 <b>착탄 폭발</b> |
| 스킬 1 (마법진 소환) | 8 | 1~4 **캐릭터** · 5~8 <b>바닥 마법진</b> |
| 스킬 2 (신성 강림) | 8 | 1~6 **캐릭터** · 7~8 <b>바닥 마법진</b> |

그래서 줄마다 :data:`ROWS` 에 ``take`` 를 적어 **앞쪽 몇 칸만** 굽는다. 뒤쪽 칸을 버려도
손해가 없다 — 오른쪽 단에 **같은 이펙트의 깨끗한 8장**이 따로 있기 때문이다.

★★ 두 번째 함정 — **오른쪽 단은 제목이 그림 밴드 안으로 들어온다**
------------------------------------------------------------------
왼쪽 단은 「제목 → 밑줄 → 그림 → 번호」가 y 로 깨끗이 갈리는데, 오른쪽 단의 이펙트는
**기둥이 제목 줄보다 위로 솟는다**(스킬 2 이펙트의 가장 높은 기둥은 제목보다 14px 위다).
밴드를 제목 아래로 내리면 그 기둥이 잘리고, 제목 위로 올리면 **제목 글자가 프레임에 박힌다.**

→ 밴드는 제목 위까지 넓게 잡고, **제목 글자만 사각형으로 지운다**(:data:`ERASE`).
  가능한 이유는 제목이 **왼쪽에 붙어 있고**(x 855~1170) 그 y 에서 이펙트는
  **오른쪽 끝 두 프레임**(x 1270~)에만 있기 때문이다 — 실측으로 확인했다.

★ 세 번째 함정 — **근거리 공격 줄은 라벨로도 빈 열로도 못 가른다**
------------------------------------------------------------------
낫의 궤적이 옆 칸까지 뻗어 ① 빈 열이 **한 곳뿐**이고 ② 라벨이 **7개**다
(원화에 6번이 없다 — 그림 자체가 7장이다). 라벨 중점으로 가르면 5번 프레임의 큰 소용돌이
한가운데(x≈550)가 경계로 잡혀 **궤적이 반토막 난다.**

→ 히스톤(84-1절)과 같은 결론: **경계를 손으로 재서 박는다**(:data:`BOUNDS`).
  잉크가 가장 얇은 열을 골랐다 — 실측값(그 열의 잉크 픽셀 수):

      x= 99 (0) · 210 (7) · 320 (2) · 435 (9) · 597 (0) · 703 (0)

  ⚠ 원화를 다시 받으면 이 표도 다시 재야 한다.

★ 네 번째 함정 — **이동은 좌/우 두 줄이 따로 그려져 있다**
-----------------------------------------------------------
제목이 「이동 (Move / Walk) - 좌 / 우」이고 **두 줄**이 있다(대기는 같은 제목인데 한 줄뿐이라
미러로 만든다). 엘린이 좌/우 줄의 y 를 뒤바꿔 적어 «가는 방향과 보는 방향이 반대»가 된 적이
있으므로(113-6절), :func:`report_tilt` 로 «머리가 발보다 어느 쪽에 있는가»를 재서 정했다 —
달릴 때는 머리가 앞선다.

⚠ 위쪽 줄의 프레임 번호에는 **작은 화살표가 같이 찍혀 있다**(「1 ↑」). 라벨 덩어리가 15개로
  잡혀 :func:`cells_by_labels` 가 무너진다 — 그 줄만 **빈 열**로 가른다(아래 ``cells``).

무엇이 어디로 가나
------------------
| 폴더 | 장수 | 스킨 칸 |
|---|---:|---|
| ``Char/Idle`` | 8 | ``idleRight``·``idleLeft`` (미러) |
| ``Char/Walk`` | 8+8 | ``walkRight``·``walkLeft`` — **원화 두 줄** |
| ``Char/MeleeAttack`` | 7 | ``attackRight``·``attackLeft`` |
| ``Char/RangedAttack`` | 6 | ``rangedRight``·``rangedLeft`` |
| ``Char/MagicAttack`` | 7 | ``magicRight``·``magicLeft`` |
| ``Char/Heal`` | 8 | ``healRight``·``healLeft`` |
| ``Char/Projectile`` | 8 | ``projectileFrames`` |
| ``Char/HealFx`` | 8 | ``healFxFrames`` — ``_03`` 에서 (아래 ★) |
| ``Char/Skill1`` | 4 | ``skill1Right``·``skill1Left`` — 「도움의 손길」(80022) |
| ``Char/Skill1Fx`` | 8 | ``skill1Fx`` |
| ``Char/Skill2`` | 6 | ``skill2Right``·``skill2Left`` — 「강림」(80024) |
| ``Char/Skill2Fx`` | 8 | ``skill2Fx`` |

★ **회복 이펙트는 ``_03`` 을 쓴다.** ``_01`` 오른쪽 단에도 같은 줄이 있지만 그쪽은
  **회색 마네킹**(회복을 받는 아군을 나타낸 가이드) 위에 그려져 있다 — 그대로 구우면
  아군 발밑에 <b>회색 인형이 하나 더</b> 뜬다. ``_03`` 은 같은 이펙트의 마네킹 없는 판이다.

★ ``_01`` 오른쪽 단의 「회복 이펙트 (Healer Effect)」 줄은 **안 쓴다** — 아루 본인이
  같이 그려져 있어 이펙트로 쓸 수 없고, 회복 모션은 왼쪽 단에 이미 있다.

사용법:  py -3 Tools/aru_skin_build.py
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
    load_sheet, label_count, cells_by_labels, cells_by_gaps,
    boxes_for, boxes_dominant, crop_rgba, body_anchor, base_anchor, compose,
    write_png, ensure_folder_meta, shadow_in_box, enclosed_background,
    resample_rgba, head_pixels,
)

ART = os.path.join(VAULT, "리소스", "sprites")
SRC01 = os.path.join(ART, "Aru_asset_01.png")
SRC03 = os.path.join(ART, "Aru_asset_03.png")

DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Aru", "Char")

SKIN_SPEC = {
    "skinAssetName": "Skin_Aru",
    "displayName": "아루",
    "framesPerSecond": "10",
    "attackFramesPerSecond": "14",
    # 날아가는 마법 구슬 원화가 실재한다.
    "groundImpactOnly": "0",
    "projectileWidthTiles": "0.9",
}

#: ★ 근거리 공격 줄의 **손으로 잰** 칸 경계 (맨 위 ★ 세 번째). 7칸 = 8개 값.
MELEE_BOUNDS = [22, 99, 210, 320, 435, 597, 703, 814]

#: ★★ 제목 글자를 지울 사각형 ``(y0, y1, x0, x1)`` — 오른쪽 단 전용 (맨 위 ★★ 두 번째).
ERASE = [
    (625, 640, 845, 1180),   # 「스킬 1 이펙트 …」  실측 x 855~1160
    (773, 792, 845, 1215),   # 「스킬 2 이펙트 …」  실측 x 855~1200
]

# ──────────────────────────────────────────────────────────────────────────
#   name     : 폴더 이름 (= 빌더 Slots 표의 키)
#   src      : "01" / "03"
#   kind     : "body" / "fx"
#   y0,y1    : 그림 밴드
#   x0,x1    : 단 경계
#   cells    : ("labels", ly0, ly1) | ("gaps",)
#   expect   : 칸이 몇 개로 잡혀야 하는가 — 어긋나면 죽는다
#   take     : 그중 실제로 구울 칸 수(앞에서부터). None 이면 전부 (맨 위 ★★ 첫 번째)
# ──────────────────────────────────────────────────────────────────────────
ROWS = [
    # ── 왼쪽 단 · 몸통 ──────────────────────────────────────────────────
    ("Idle",         "01", "body",  42, 118,  10, 830, ("labels", 123, 132), 8, None),
    ("WalkA",        "01", "body", 171, 241,  10, 830, ("gaps",),            8, None),
    ("WalkB",        "01", "body", 254, 326,  10, 830, ("labels", 330, 338), 8, None),
    ("MeleeAttack",  "01", "body", 369, 449,  10, 830, ("bounds",),          7, None),
    ("RangedAttack", "01", "body", 499, 564,  10, 830, ("labels", 568, 576), 8, 6),
    ("MagicAttack",  "01", "body", 607, 677,  10, 830, ("labels", 679, 688), 8, 7),
    ("Heal",         "01", "body", 719, 783,  10, 830, ("labels", 785, 793), 8, None),
    ("Skill1",       "01", "body", 819, 885,  10, 830, ("labels", 888, 895), 8, 4),
    ("Skill2",       "01", "body", 917, 999,  10, 830, ("labels", 1001, 1010), 8, 6),

    # ── 오른쪽 단 · 연출 ────────────────────────────────────────────────
    ("Projectile",   "01", "fx",    80, 145, 845, 1525, ("labels", 159, 168), 8, None),
    ("Skill1Fx",     "01", "fx",   615, 742, 845, 1525, ("labels", 744, 753), 8, None),
    ("Skill2Fx",     "01", "fx",   765, 932, 845, 1525, ("labels", 934, 943), 8, None),

    # ── 별지 ────────────────────────────────────────────────────────────
    ("HealFx",       "03", "fx",   112, 750,  10, 1525, ("labels", 771, 788), 8, None),
]

#: 좌우 방향이 없는 한 벌짜리 묶음.
NO_DIRECTION = {"Projectile", "Skill1Fx", "Skill2Fx", "HealFx"}

#: ★ 「이동」은 원화가 두 줄이다 — 미러하지 않고 **줄을 그대로 쓴다**.
#:   어느 줄이 어느 쪽인지는 :func:`report_tilt` 로 정했다(맨 위 ★ 네 번째).
WALK_ROWS = {"WalkA": None, "WalkB": None}     # main() 에서 채운다

#: 원본이 바라보는 쪽(미러로 반대쪽을 만든다). 실측으로 전부 오른쪽이었다 —
#: 낫이 오른쪽으로 휘고(근거리), 마법 구슬이 오른쪽으로 날아간다(원거리·마법).
DEFAULT_ORIGINAL_SIDE = "Right"
ORIGINAL_SIDE = {}

SCALE_REFERENCE = "Idle"
SCALE_MIN, SCALE_MAX = 0.70, 1.80

#: ★★ 옆 칸 조각을 버리고 «칸 한가운데를 물고 있는 덩어리»만 남길 줄
#:   (:func:`skin_sheet.boxes_dominant` · 베일 시트에서 만들어진 함수).
#:
#:   <b>왜 필요했나</b> — 이 시트는 한 줄에 모션과 이펙트가 섞여 있어서(맨 위 ★★ 첫 번째),
#:   버리는 칸의 <b>투사체가 앞 칸 안까지 들어와 있다.</b> 그대로 두면 그 조각이 상자를
#:   넓혀 <b>캔버스가 40px 이상 부푼다</b>(실측: 원거리 127 → 89 · 마법 120 → 87).
#:
#:   ⚠ <b>근거리는 뺀다</b> — 낫의 궤적이 몸에서 <b>떨어져</b> 그려진 프레임이 있어
#:     «가운데 덩어리» 규칙이 그 궤적을 남의 것으로 보고 버린다. 그 줄은 경계를 손으로
#:     재 두었으므로(:data:`MELEE_BOUNDS`) 조각이 애초에 안 들어온다.
DOMINANT_SKIP = {"MeleeAttack"}

#: 본체에 «이어 붙일» 최대 간격(칸 폭에 대한 비율). 기본값 0.12 로는 원거리 3번의
#: <b>파란 물보라 꼬리</b>가 4번 칸까지 흘러 들어온 것을 못 끊었다 — 실측으로 0.06
#: 에서 끊겼다. 더 내리면 이번엔 <b>본체에서 떨어져 그려진 랜턴 빛</b>이 잘린다.
DOMINANT_JOIN = 0.06

#: 이펙트 줄의 **목표 높이(px)**. 몸통과 달리 이펙트는 «대기 대비 머리 크기» 로 잴 수 없다.
#:
#: ★ ``HealFx`` 만 있는 이유 — 그 줄만 <b>다른 시트(``_03``)</b>에서 오고, 그 시트는 같은
#:   이펙트를 <b>639px 높이</b>로 크게 그려 뒀다. 그대로 구우면 PPU 64 기준 <b>10타일</b>
#:   짜리 그림이 아군 발밑에 깔린다(엘린의 회복 이펙트는 109px · 대기의 1.2배다).
#:   ``CombatProjectileFx.PlayHeal`` 은 ``impactWidthTiles`` 가 없으면 <b>원본 크기 그대로</b>
#:   그리므로, 크기를 여기서 정해 두는 것이 가장 확실하다.
FX_TARGET_HEIGHT = {"HealFx": 120}


def cells_for(sheet, spec, x0, x1, y0, y1):
    """줄마다 다른 칸 가르기 (맨 위 ★ 세 번째·네 번째 — 한 방법으로 통일할 수 없다)."""
    kind = spec[0]
    if kind == "labels":
        return cells_by_labels(sheet["gray"], x0, x1, spec[1], spec[2])
    if kind == "gaps":
        return cells_by_gaps(sheet["mask"], y0, y1, x0, x1)
    if kind == "bounds":
        return [(MELEE_BOUNDS[i], MELEE_BOUNDS[i + 1] - 1)
                for i in range(len(MELEE_BOUNDS) - 1)]
    raise SystemExit("알 수 없는 칸 가르기: %r" % (spec,))


def report_tilt(name, frames):
    """머리와 발의 가로 차이. 양수면 머리가 오른쪽(= 오른쪽으로 간다)."""
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
    print("    %-14s 머리−발 %+6.1f px  (양수 = 오른쪽)" % (name, v))
    return v


def measure_head_scale(collected):
    """모션마다 대기 대비 배율 (시그리드·시카리아와 같은 방법 — 머리 «면적»으로 잰다)."""
    areas = {}
    for name, frames in collected.items():
        vals = [head_pixels(f) for f in frames]
        vals = [v for v in vals if v > 60]
        if vals:
            # ★ 평균이 아니라 **중앙값**이다. 회복 줄의 마지막 두 장은 아루가 초록 안개로
            #   흩어져 머리가 거의 안 잡히는데(원화가 그렇다), 평균을 쓰면 그 두 장이
            #   줄 전체를 x1.22 로 부풀린다 — 모션이 바뀌는 순간 몸이 커진다.
            areas[name] = float(np.median(vals))

    ref = areas.get(SCALE_REFERENCE)
    if not ref:
        print("  ⚠ 기준 모션(%s)의 머리를 못 재 크기 정규화를 건너뜁니다" % SCALE_REFERENCE)
        return {}

    factors = {}
    print("  [크기 정규화] 머리 면적 → 대기 기준 배율")
    for name, area in sorted(areas.items()):
        f = (ref / area) ** 0.5
        factors[name] = f
        flag = "" if SCALE_MIN <= f <= SCALE_MAX else "  ← ⚠ 범위 밖"
        print("    %-14s 머리 %6.1f px  →  x%.3f%s" % (name, area, f, flag))

    bad = {k: v for k, v in factors.items() if not (SCALE_MIN <= v <= SCALE_MAX)}
    if bad:
        raise SystemExit("⚠ 크기 배율이 안전 범위(%.2f~%.2f)를 벗어났습니다: %s"
                         % (SCALE_MIN, SCALE_MAX, bad))
    return factors


def cut_all(sheets):
    """먼저 **자르기만** 한다 — 배율은 대기를 다 재고 나서야 알 수 있다."""
    cut = []
    collected = {}

    for name, src, kind, y0, y1, x0, x1, spec, expect, take in ROWS:
        sheet = sheets[src]
        cells = cells_for(sheet, spec, x0, x1, y0, y1)
        if len(cells) != expect:
            raise SystemExit(
                "⚠ %s(%s): 칸이 %d개인데 %d개를 기대했습니다 "
                "(그림 y%d~%d · x%d~%d · %s). 시트가 바뀌었으면 좌표를 다시 재세요."
                % (name, src, len(cells), expect, y0, y1, x0, x1, spec[0]))

        if take:
            cells = cells[:take]

        # ★ 몸통 줄만 발밑 그림자를 지운다 — 이펙트는 그 자체가 지면 연출이다.
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
                _pockets = enclosed_background(sheet, y0, y1, _cx0, _cx1)
                if _pockets.any():
                    sheet["mask"] &= ~_pockets

        if kind == "body":
            rough = [b for b in boxes_for(sheet["mask"], cells, y0, y1) if b is not None]
            shadow = np.zeros(sheet["mask"].shape, dtype=bool)
            for b in rough:
                shadow |= shadow_in_box(sheet, b)
            sheet["mask"] &= ~shadow

        if kind == "body" and name not in DOMINANT_SKIP:
            raw = boxes_dominant(sheet["mask"], cells, y0, y1,
                                 min_ink_ratio=DOMINANT_JOIN)
        else:
            raw = boxes_for(sheet["mask"], cells, y0, y1)
        boxes = [b for b in raw if b is not None]
        frames = [crop_rgba(sheet, b) for b in boxes]
        if len(frames) != len(cells):
            raise SystemExit("⚠ %s: 빈 칸이 %d개 있습니다 — 밴드를 확인하세요."
                             % (name, len(cells) - len(frames)))

        cut.append([name, kind, frames])
        if kind == "body" and name not in ("Skill1", "Skill2"):
            # 스킬 자세는 팔을 뻗어 머리 판정이 흔들린다 — 배율 기준에서 뺀다.
            collected[name] = frames
    return cut, collected


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print("[아루 모션 시트 분해]")

    sheets = {"01": load_sheet(SRC01, box_borders=True),
              "03": load_sheet(SRC03, box_borders=True)}

    # ★★ 오른쪽 단 제목 글자를 지운다 — 밴드로는 못 가른다(맨 위 ★★ 두 번째).
    for y0, y1, x0, x1 in ERASE:
        n = int(sheets["01"]["mask"][y0:y1 + 1, x0:x1 + 1].sum())
        sheets["01"]["mask"][y0:y1 + 1, x0:x1 + 1] = False
        print("  제목 지움 y%d~%d x%d~%d · %d px" % (y0, y1, x0, x1, n))

    cut, collected = cut_all(sheets)

    print("  [방향 실측]")
    tilt = {}
    for name, kind, frames in cut:
        if kind == "body":
            tilt[name] = report_tilt(name, frames)

    # ★ 이동 두 줄 중 «머리가 더 오른쪽» 인 쪽이 오른쪽으로 가는 줄이다.
    if tilt.get("WalkA", 0) >= tilt.get("WalkB", 0):
        WALK_ROWS["WalkA"], WALK_ROWS["WalkB"] = "Right", "Left"
    else:
        WALK_ROWS["WalkA"], WALK_ROWS["WalkB"] = "Left", "Right"
    print("    → 이동: WalkA=%s · WalkB=%s" % (WALK_ROWS["WalkA"], WALK_ROWS["WalkB"]))

    factors = measure_head_scale(collected)

    made = 0
    for name, kind, frames in cut:
        factor = factors.get(name, 1.0)
        if name in FX_TARGET_HEIGHT:
            tall = max(f.shape[0] for f in frames)
            factor = FX_TARGET_HEIGHT[name] / float(tall)
            print("    %-14s 원본 높이 %dpx → 목표 %dpx (x%.3f)"
                  % (name, tall, FX_TARGET_HEIGHT[name], factor))
        if abs(factor - 1.0) > 0.002:
            frames = [resample_rgba(f, factor) for f in frames]

        anchor = body_anchor if kind == "body" else base_anchor
        images, w, h = compose(frames, [anchor(f) for f in frames])

        if name in WALK_ROWS:
            # 두 줄이 각각 한 방향 — 미러하지 않는다. 폴더는 하나(``Walk``)로 합친다.
            side = WALK_ROWS[name]
            folder = os.path.join(DST_ROOT, "Walk")
            for i, img in enumerate(images):
                write_png(img, folder, "Char_Walk_%s_%02d" % (side, i))
                made += 1
            note = "원화 그대로 (%s)" % side
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
        print("  %-14s %3d x %3d · %2d장 · %s%s" % (name, w, h, len(images), note, scale_note))

    spec = write_skin_spec(DST_ROOT, SKIN_SPEC, "Tools/aru_skin_build.py")
    ensure_folder_meta(DST_ROOT)
    print("  스킨 설정 %s (%d줄)" % (SKIN_SPEC_NAME, spec))
    print("  → 프레임 %d장" % made)
    print("  다음: 유니티 메뉴 LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기")


if __name__ == "__main__":
    main()
