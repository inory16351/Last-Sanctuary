# -*- coding: utf-8 -*-
"""구워진 프레임의 <b>피벗을 발에 맞춘다</b> — 결과물 쪽 보정 (2026-08-22 신설).

★ 왜 이 도구가 필요한가
-----------------------
유저 지시: *"캐릭터가 커졌다 작아졌다 도 안하게 확실하게 분석해서 피벗 맞추고 비율이랑"*.

스프라이트 피벗은 <b>캔버스 하단 가운데</b>(0.5, 0)로 고정되어 있다
(:data:`skin_sheet.META`). 그러니 «모션이 바뀔 때 캐릭터가 옆으로 미끄러지지 않는다» 는
<b>모션마다 발이 캔버스 가운데에 있다</b> 와 같은 말이다.

분해 스크립트가 있는 캐릭터는 :func:`skin_sheet.plant_feet` 가 구울 때 맞춘다. 그런데
<b>분해 스크립트가 없는 옛 팩</b>이 여덟 벌 있다(단타리안·히스톤·피올로·종양거미·카시노마·
비기오르·헬팽·영혼궁수) — 원화가 낱장 PNG 로 들어와 캔버스가 제각각이다. 실측:

    단타리안  42.2 px    히스톤  23.5 px    피올로  23.2 px    종양거미 20.5 px
    카시노마  15.0 px    비기오르 9.5 px    헬팽    8.8 px     영혼궁수  6.0 px

그만큼 <b>모션이 바뀔 때 옆으로 뛴다</b>. 원화가 없어 다시 구울 수 없으므로
<b>구워진 PNG 를 다시 얹는다</b>.

무엇을 하나
-----------
묶음(폴더)마다 · <b>방향마다</b>(Right/Left/무방향) 따로:

1. 프레임마다 :func:`skin_sheet.foot_center` — «몸통 덩어리의 아래쪽 띠» 가로 중심
2. 그 값들의 <b>중앙값</b>이 캔버스 가운데에 오도록 <b>같은 양</b>을 민다
   (프레임마다 맞추면 걷는 다리 놀림이 지워진다 — 그건 더 이상하다)
3. 캔버스 폭은 :func:`skin_sheet.compose` 와 같은 규칙으로 «피벗 좌우 같은 폭» 으로 다시 잡는다
4. 세로는 <b>바닥에 붙인다</b> — 피벗이 하단이므로 그래야 위아래로 안 튄다

⚠ ``.meta`` 는 <b>건드리지 않는다</b>. 이 프로젝트의 프레임 메타에는 rect 가 없고
  (``spriteMode: 1`` · ``alignment: 9`` · ``spritePivot {0.5, 0}``) 크기에 딸린 값이 없다 —
  픽셀만 다시 써도 guid·PPU·필터가 그대로 유지된다.
⚠ 이펙트 묶음은 <b>건너뛴다</b> — 그쪽 기준은 발이 아니라 «밑동»(:func:`skin_sheet.base_anchor`)
  이고, 대상 발밑에 오도록 맞춰져 있다.
★ <b>멱등</b>이다 — 한 번 맞추면 다음 실행에서 밀 양이 0 이라 아무것도 안 바뀐다.

사용법:
    py -3 Tools/align_skin_pivots.py                 ← 전부 (바꾸기 전에 보고만)
    py -3 Tools/align_skin_pivots.py --apply
    py -3 Tools/align_skin_pivots.py Histon --apply
"""

import argparse
import os
import re
import sys

import numpy as np
from PIL import Image

from vault_path import PROJECT
from skin_sheet import foot_center

ART_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset")

#: 이펙트 묶음 — 발이 기준이 아니므로 건너뛴다. 이름에 이 조각이 들면 이펙트로 본다.
FX_HINTS = ("fx", "projectile", "impact", "muzzle", "travel", "burst", "beam",
            "orb", "blast", "wave", "sigil", "parts", "smoke", "ring")

#: 이만큼(px) 미만이면 밀지 않는다 — 반올림 잡음까지 따라다닐 이유가 없다.
MIN_SHIFT = 2.0


def is_fx(group):
    g = group.lower()
    return any(h in g for h in FX_HINTS)


def side_of(name):
    """파일 이름에서 방향. ``_Left_`` / ``_Right_`` 가 없으면 ``""``."""
    m = re.search(r"_(Left|Right)_\d+", name)
    return m.group(1) if m else ""


def load(path):
    """⚠ <b>파일을 반드시 닫는다</b> — PIL 은 게으르게 읽어 파일 핸들을 붙잡고 있고,
    윈도에서는 열린 파일에 덮어쓰면 ``Errno 22`` 로 죽는다(실제로 그랬다)."""
    with Image.open(path) as im:
        return np.asarray(im.convert("RGBA")).copy()


def relay(frames):
    """묶음을 <b>같은 캔버스</b>에 다시 얹는다 — 가로는 발 중심, 세로는 바닥.

    ⚠⚠ <b>밀 양은 «여백을 걷어낸 뒤» 다시 재야 한다</b> (2026-08-22 실사고).
      처음에는 원본 캔버스에서 잰 값을 그대로 썼는데, 옛 팩은 <b>캔버스 여백이 좌우로
      제각각</b>이라 그 값이 «여백을 걷어낸 좌표» 에서는 다른 값이다. 그래서 한 번 맞춘
      뒤 다시 재면 <b>더 어긋나 있었다</b>(영혼궁수 이동 +6.5 → −9.0). 멱등이 깨진 것이다.
      → 여기서 다시 잰다. 그러면 한 번 돌린 뒤에는 밀 양이 0 이 되어 멱등이 성립한다.
    """
    tight = []
    for a in frames:
        solid = a[:, :, 3] > 0
        if not solid.any():
            tight.append(a)
            continue
        ys = np.where(solid.any(axis=1))[0]
        xs = np.where(solid.any(axis=0))[0]
        tight.append(a[ys.min():ys.max() + 1, xs.min():xs.max() + 1])

    got = []
    for t in tight:
        c = foot_center(t)
        if c is not None:
            got.append(c - t.shape[1] / 2.0)
    shift = float(np.median(got)) if got else 0.0

    anchors = [t.shape[1] / 2.0 + shift for t in tight]
    pad = max(max(anchors), max(t.shape[1] - a for t, a in zip(tight, anchors)))
    w = int(np.ceil(pad * 2))
    h = max(t.shape[0] for t in tight)
    out = []
    for t, a in zip(tight, anchors):
        canvas = np.zeros((h, w, 4), dtype=np.uint8)
        bh, bw = t.shape[0], t.shape[1]
        ox = max(0, min(w - bw, int(round(w / 2.0 - a))))
        canvas[h - bh:h, ox:ox + bw] = t
        out.append(canvas)
    return out


def process_group(folder, apply):
    """한 묶음. (바뀐 장수, 방향별 밀린 양) 을 돌려준다."""
    files = sorted(f for f in os.listdir(folder) if f.lower().endswith(".png"))
    if not files:
        return 0, {}

    by_side = {}
    for f in files:
        by_side.setdefault(side_of(f), []).append(f)

    changed, shifts = 0, {}
    for side, names in sorted(by_side.items()):
        frames = [load(os.path.join(folder, n)) for n in names]
        offs = []
        for a in frames:
            c = foot_center(a)
            if c is not None:
                offs.append(c - a.shape[1] / 2.0)
        if not offs:
            continue
        shift = float(np.median(offs))
        shifts[side or "-"] = shift
        if abs(shift) < MIN_SHIFT:
            continue
        if not apply:
            changed += len(names)
            continue
        for n, img in zip(names, relay(frames)):
            Image.fromarray(img, "RGBA").save(os.path.join(folder, n))
            changed += 1
    return changed, shifts


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("only", nargs="*", help="이름에 이 문자열이 든 캐릭터만")
    ap.add_argument("--apply", action="store_true", help="실제로 다시 쓴다(기본은 보고만)")
    ap.add_argument("--min-shift", type=float, default=MIN_SHIFT)
    args = ap.parse_args()

    globals()["MIN_SHIFT"] = args.min_shift

    print("[피벗 발 맞춤]  %s · 최소 %.1fpx 부터 민다"
          % ("적용" if args.apply else "보고만 (바꾸려면 --apply)", MIN_SHIFT))
    total = 0
    for char in sorted(os.listdir(ART_ROOT)):
        root = os.path.join(ART_ROOT, char)
        if not os.path.isdir(root):
            continue
        if args.only and not any(k.lower() in char.lower() for k in args.only):
            continue
        lines = []
        for dirpath, _dirs, files in os.walk(root):
            group = os.path.basename(dirpath)
            if not any(f.lower().endswith(".png") for f in files):
                continue
            if is_fx(group) or group.lower().startswith("unused"):
                continue
            n, shifts = process_group(dirpath, args.apply)
            if n:
                lines.append("    %-20s %2d장  %s"
                             % (group, n,
                                " ".join("%s%+.1f" % (k, v) for k, v in sorted(shifts.items()))))
        if lines:
            total += sum(int(t.split()[1].rstrip("장")) for t in lines)
            print("  %s" % char.replace("Char_Asset_", ""))
            for t in lines:
                print(t)
    print("  → %s %d장" % ("다시 씀" if args.apply else "고칠 것", total))
    if not args.apply and total:
        print("  ⚠ 아직 아무것도 안 바꿨다 — `--apply` 를 붙여 다시 실행할 것")


if __name__ == "__main__":
    main()
