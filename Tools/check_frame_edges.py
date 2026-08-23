# -*- coding: utf-8 -*-
"""구워진 프레임 PNG 를 <b>결과물 쪽에서</b> 검사한다 — 「짤림」 자동 점검 (2026-08-22 신설).

★ 왜 이 도구가 필요한가
-----------------------
:func:`skin_sheet.audit_boxes` 는 <b>시트 안</b>에서 «상자가 밴드 경계에서 그림을 자르는가» 를
본다. 그것은 원인을 짚어 주지만, <b>줄과 줄이 서로 닿아 있는 시트</b>에서는 «더 넓힐 데가
없어서» 남는 경고도 함께 나온다 — 즉 <b>고칠 수 있는 것과 없는 것이 섞인다</b>.

유저가 보는 것은 결과물이다: *"위 아래도 조금씩 짤리는 이미지들이 발견된다"*.
그래서 <b>구워진 PNG 를 직접</b> 본다. 판정은 «그림의 <b>테두리 한 줄</b>이 얼마나 불투명한가»
하나다:

    잘린 단면    — 테두리 한 줄이 <b>거의 꽉 찬 불투명</b>(단면이 그대로 보인다)
    자연스러운 끝 — 테두리 한 줄은 <b>안티에일리어싱</b>이라 몇 픽셀뿐이고 알파도 낮다

⚠ <b>왼·오른쪽은 세로 기준이 다르다</b> — 캔버스는 :func:`skin_sheet.compose` 가 몸통 중심
  기준으로 좌우 같은 폭을 잡으므로, 그림의 좌우 끝은 대개 «한 점» 이다. 그래서 가로 검사는
  비율 문턱을 더 높게 둔다.

사용법:
    py -3 Tools/check_frame_edges.py                     ← 전부
    py -3 Tools/check_frame_edges.py Seraphiel Elysia    ← 이름으로 걸러서
    py -3 Tools/check_frame_edges.py --min-ratio 0.25
"""

import argparse
import os
import sys

import numpy as np
from PIL import Image

from vault_path import PROJECT
from skin_sheet import components

ART_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset")

#: 「불투명」으로 볼 알파. 이 아래는 안티에일리어싱으로 본다.
SOLID_ALPHA = 200

#: 테두리 한 줄에서 불투명 픽셀이 이 비율을 넘으면 <b>잘린 단면</b>으로 본다.
ROW_RATIO = 0.30
COL_RATIO = 0.45

#: 이 픽셀 수 미만이면 폭이 좁아 비율이 튀므로 세지 않는다.
MIN_SPAN = 12


def edge_report(path):
    """(위, 아래, 왼, 오른) 테두리의 «불투명 비율». 그림이 없으면 None."""
    a = np.asarray(Image.open(path).convert("RGBA"))
    alpha = a[:, :, 3]
    solid = alpha >= SOLID_ALPHA
    any_ink = alpha > 0
    if not any_ink.any():
        return None
    ys = np.where(any_ink.any(axis=1))[0]
    xs = np.where(any_ink.any(axis=0))[0]
    y0, y1 = int(ys.min()), int(ys.max())
    x0, x1 = int(xs.min()), int(xs.max())
    w = x1 - x0 + 1
    h = y1 - y0 + 1
    top = solid[y0, x0:x1 + 1].sum() / float(w) if w >= MIN_SPAN else 0.0
    bot = solid[y1, x0:x1 + 1].sum() / float(w) if w >= MIN_SPAN else 0.0
    lft = solid[y0:y1 + 1, x0].sum() / float(h) if h >= MIN_SPAN else 0.0
    rgt = solid[y0:y1 + 1, x1].sum() / float(h) if h >= MIN_SPAN else 0.0
    return top, bot, lft, rgt, w, h


#: 몸통 크기가 대기와 이 비율 넘게 다르면 알린다.
SIZE_TOLERANCE = 0.15

#: 몸통으로 볼 «가장 큰 덩어리» 만 본다 — 앞으로 뻗은 무기·궤적은 대개 따로 떨어져 있다.
BODY_SKIP = ("fx", "projectile", "impact", "muzzle", "travel", "burst", "beam",
             "orb", "blast", "wave", "sigil", "parts", "smoke", "ring", "unused")


def body_size(path):
    """이 프레임의 <b>몸통 덩어리</b> (가로, 세로) px. 없으면 None."""
    with Image.open(path) as im:
        a = np.asarray(im.convert("RGBA"))
    solid = a[:, :, 3] > 8
    if not solid.any():
        return None
    main = max(components(solid), key=lambda c: int(c.sum()))
    ys = np.where(main.any(axis=1))[0]
    xs = np.where(main.any(axis=0))[0]
    return int(xs[-1] - xs[0] + 1), int(ys[-1] - ys[0] + 1)


def size_report(char_root, name):
    """
    ★★ <b>모션마다 «몸» 이 같은 크기로 그려졌는가</b> (2026-08-22 신설).

    <b>왜 이 검사가 필요한가</b> (유저 지시: *"캐릭터가 커졌다 작아졌다 도 안하게 확실하게
    분석해서 … 비율이랑"*) — 게임 안 배율은 <b>스킨 하나에 한 값</b>이고 그 값은
    <b>대기 원화</b>로 정해진다(`measure_skin_tiles.py` · `CharacterAnimator.ResolveScale`).
    그러니 어떤 모션의 원화가 대기보다 크게 그려져 있으면 <b>그 모션에서만 캐릭터가 커진다</b>.

    ⚠ 자세로도 달라진다 — 달리는 그림은 웅크려서 20~30% 짧게 나오는 것이 <b>연출</b>이다.
      그래서 <b>죽이지 않고 알리기만</b> 한다. 어느 쪽인지는 사람이 겹쳐 보고 정한다.
    """
    sizes = {}
    for dirpath, _dirs, files in os.walk(char_root):
        group = os.path.basename(dirpath)
        if group.lower().startswith("unused") or any(k in group.lower() for k in BODY_SKIP):
            continue
        pngs = sorted(f for f in files if f.lower().endswith(".png"))
        if not pngs:
            continue
        got = [body_size(os.path.join(dirpath, f)) for f in pngs]
        got = [g for g in got if g]
        if got:
            sizes[group] = (float(np.median([g[0] for g in got])),
                            float(np.median([g[1] for g in got])))
    ref = sizes.get("Idle")
    if not ref or ref[1] <= 0:
        return []
    out = []
    for g, (w, h) in sorted(sizes.items()):
        d = h / ref[1] - 1.0
        if abs(d) > SIZE_TOLERANCE:
            out.append("    %-20s 몸통 %3.0f x %-3.0f  대기와 %+.0f%%" % (g, w, h, d * 100))
    return out


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("only", nargs="*", help="이름에 이 문자열이 든 캐릭터만")
    ap.add_argument("--row-ratio", type=float, default=ROW_RATIO)
    ap.add_argument("--col-ratio", type=float, default=COL_RATIO)
    ap.add_argument("--quiet", action="store_true", help="문제 있는 묶음만 찍는다")
    ap.add_argument("--size", action="store_true",
                    help="테두리 대신 <b>모션끼리 몸 크기</b>가 같은지 본다")
    args = ap.parse_args()

    if not os.path.isdir(ART_ROOT):
        raise SystemExit("⚠ 없는 폴더: " + ART_ROOT)

    if args.size:
        print("[몸 크기 균일성]  «가장 큰 덩어리» 의 세로를 대기와 견준다 · 문턱 %.0f%%"
              % (SIZE_TOLERANCE * 100))
        n = 0
        for char in sorted(os.listdir(ART_ROOT)):
            root = os.path.join(ART_ROOT, char)
            if not os.path.isdir(root):
                continue
            if args.only and not any(k.lower() in char.lower() for k in args.only):
                continue
            lines = size_report(root, char)
            if lines:
                n += len(lines)
                print("  %s" % char.replace("Char_Asset_", ""))
                for t in lines:
                    print(t)
        print("  → 대기와 %.0f%% 넘게 다른 묶음 %d개 (자세일 수 있다 — 사람이 판단)"
              % (SIZE_TOLERANCE * 100, n))
        return

    print("[프레임 테두리 검사]  불투명 알파 >= %d · 문턱 위아래 %.2f / 좌우 %.2f"
          % (SOLID_ALPHA, args.row_ratio, args.col_ratio))
    total_bad = 0
    for char in sorted(os.listdir(ART_ROOT)):
        root = os.path.join(ART_ROOT, char)
        if not os.path.isdir(root):
            continue
        if args.only and not any(k.lower() in char.lower() for k in args.only):
            continue
        lines = []
        for dirpath, _dirs, files in os.walk(root):
            pngs = sorted(f for f in files if f.lower().endswith(".png"))
            if not pngs:
                continue
            group = os.path.relpath(dirpath, root).replace("\\", "/")
            # ★★ <b>몸통 줄의 «아래» 는 검사하지 않는다</b> — 피벗이 발밑(0.5, 0)이라
            #   서 있는 캐릭터는 <b>바닥 한 줄이 꽉 찬 것이 정상</b>이다(발바닥·그림자).
            #   그것까지 세면 진짜 잘림이 묻힌다(실측: 76개 중 50개 남짓이 그것이었다).
            #   공중에 뜨는 이펙트만 «아래» 를 본다.
            #   ⚠ ``unused`` 는 여기서 빼야 한다 — «안 쓰는 <b>몸통</b> 원화» 도 있고
            #     (시그리드 `Unused_Skill1`) 그건 서 있는 그림이라 바닥이 꽉 찬 게 정상이다.
            floating = any(k in os.path.basename(dirpath).lower()
                           for k in BODY_SKIP if k != "unused")
            bad = []
            for f in pngs:
                r = edge_report(os.path.join(dirpath, f))
                if r is None:
                    continue
                top, bot, lft, rgt, w, h = r
                tags = []
                if top >= args.row_ratio:
                    tags.append("위 %.0f%%" % (top * 100))
                if floating and bot >= args.row_ratio:
                    tags.append("아래 %.0f%%" % (bot * 100))
                if lft >= args.col_ratio:
                    tags.append("왼 %.0f%%" % (lft * 100))
                if rgt >= args.col_ratio:
                    tags.append("오른 %.0f%%" % (rgt * 100))
                if tags:
                    bad.append("%s(%s)" % (os.path.splitext(f)[0].split("_")[-1],
                                           "·".join(tags)))
            if bad:
                lines.append("    %-22s %2d/%-2d  %s"
                             % (group, len(bad), len(pngs), ", ".join(bad[:8])
                                + (" …" if len(bad) > 8 else "")))
        if lines:
            total_bad += len(lines)
            print("  %s" % char.replace("Char_Asset_", ""))
            for t in lines:
                print(t)
        elif not args.quiet:
            print("  %s — 깨끗함" % char.replace("Char_Asset_", ""))
    print("  → 문제가 있는 묶음 %d개" % total_bad)


if __name__ == "__main__":
    main()
