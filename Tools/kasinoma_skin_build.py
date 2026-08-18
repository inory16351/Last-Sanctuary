# -*- coding: utf-8 -*-
"""카시노마(웨이브 보스 120003) 모션 시트 → 프레임 분해 (2026-08-18).

원본
----
``<볼트>/리소스/Kasinoma_asset_01.png`` (1536x1024).
말파스 시트와 같은 「한 장짜리 기획 시트」인데 **배경이 균일한 흰색이 아니다**
(모서리가 204,202,204 · JPEG 잡티가 섞여 있다). 그래서 배경 판정을 **밝기 + 채도**로 한다.

★ 프레임을 어떻게 가르나 — <b>라벨로 구간을 정하고, 그 사이 빈 열에서 자른다</b>
--------------------------------------------------------------------------
말파스는 라벨 중심의 **중점**에서 잘랐다. 카시노마는 그렇게 하면 **안 된다** —
라벨이 프레임 한가운데가 아니라 <b>왼쪽 위</b>에 찍혀 있고 그 어긋남이 오른쪽으로
갈수록 커진다(근거리 행에서 30px 이상). 중점에서 자르면 앞 프레임의 오른쪽이 잘린다.

그래서 두 단계로 한다:

  ① **라벨**(회색조 숫자)로 "여기부터 여기까지 프레임 하나" 를 <b>센다</b>.
  ② 이웃한 두 라벨 사이에서 **잉크가 가장 없는 열**을 찾아 거기서 <b>자른다</b>.

⚠ **라벨을 회색조로만 찾는다** — 이 시트의 그림은 전부 붉은 계열(채도 높음)이라
  `채도 < 42` 한 줄로 그림을 통째로 걸러낼 수 있다. 이게 없으면 붉은 칼자국이
  라벨로 잡혀 근거리 행에 <b>가짜 프레임</b>이 생긴다(실제로 그랬다).

⚠ **라벨 수 = 프레임 수가 아니다.** 시트가 손으로 만들어져 번호가 건너뛴다
  (이동 03 없음 · 근거리 03 없음 · 스킬1 05 없음). 말파스와 같은 원칙 —
  **찾은 라벨 수만큼** 뽑는다. 게임에서는 몇 장이든 그대로 순환 재생된다.

방향
----
카시노마는 원본이 **왼쪽을 본다**(이동·돌진·팔 휘두르기가 전부 왼쪽으로 간다).
그래서 원본을 ``Left`` 로 보고 ``Right`` 를 좌우 반전으로 만든다.
⚠ 말파스는 반대다(원본이 오른쪽) — 새 원화마다 확인할 것.

사용법:  python Tools/kasinoma_skin_build.py
다음:    python Tools/gen_kasinoma_skin.py
"""

import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT

SRC = os.path.join(VAULT, "리소스", "Kasinoma_asset_01.png")
DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Kasinoma", "Char")

#: 1픽셀당 유니티 단위. 게임 안 크기는 contentSizeTiles 로 정규화되므로(61·66절)
#: 이 값이 화면 크기를 정하지 않는다 — 다른 유닛과 같은 대역이면 된다.
PPU = 64

#: 배경 판정. ⚠ 말파스(흰 배경)와 <b>다르다</b> — 이 시트는 배경이 밝은 회색이고
#: 그림이 전부 붉은 계열이라 「밝고 무채색이면 배경」이 가장 잘 맞는다.
#: 가장자리는 두 값 사이에서 부드럽게 뺀다.
BG_LUM_HI = 196      # 이보다 밝고 무채색이면 확실한 배경
BG_LUM_LO = 150      # 이보다 어두우면 확실한 그림
BG_SAT = 40          # 채도가 이보다 높으면 밝아도 그림(붉은 잔광)

#: 라벨(작은 회색 숫자) 판정. 그림은 채도가 높아 빠진다.
LABEL_LUM = 110
LABEL_SAT = 42

#: 라벨 덩어리를 가르는 <b>기본</b> 최소 빈 열. 두 자리 숫자 안쪽 간격은 3px,
#: 라벨끼리는 100px 이상 떨어져 있다.
#: ⚠ 스킬2 줄만 다르다 — 거기는 라벨이 "01  1타" 처럼 <b>두 덩어리</b>라 이 값으로는
#:   한 프레임이 둘로 갈린다(실제로 12프레임이 나왔다). 그 줄만 밴드 표에서 40 으로 올린다.
LABEL_GAP = 14

#: 라벨 덩어리의 <b>기본</b> 최대 폭(px).
#: ⚠ <b>이게 좁아야 한다.</b> 진짜 라벨(두 자리 숫자)은 10~13px 인데, 넉넉히 44 로 두면
#:   근거리 행의 <b>붉은 칼자국</b>(35px)과 「경직」줄 끝의 <b>발톱</b>(25px)이 라벨로 잡혀
#:   가짜 프레임이 생긴다(실제로 11프레임이 나왔다). 16 이면 숫자만 남는다.
LABEL_MAX_W = 16

#: 몸통 모션에서 <b>이 비율보다 그림이 적은</b> 프레임은 버린다(그 모션의 최대 잉크량 대비).
#: 돌진(Skill1) 04~06 은 본체가 잔상으로 흩어져 잉크가 적지만 <b>그것이 그 프레임의 그림</b>
#: 이라 버리면 안 된다 — 그래서 말파스(0.35)보다 훨씬 낮게 둔다.
MIN_BODY_AREA_RATIO = 0.04

# ──────────────────────────────────────────────────────────────────────────
# 시트 배치표 — **실측값이다** (잉크 밴드 + 라벨 탐지로 재고 눈으로 확인함).
#
#   (모션, 라벨 y0, 라벨 y1, 프레임 y0, 프레임 y1, x0, x1, 라벨 상한)
#
# 같은 모션이 여러 줄이면 여기 여러 번 나온다 — 위에서 아래 순서가 곧 프레임 순서다.
# ★ 「경직」줄을 <b>같은 모션 뒤에 이어 붙인다</b> — 돌진/연타가 끝나고 숨을 고르는
#   그림이라 따로 재생할 자리가 없다. 이어 붙이면 스킬 모션 한 바퀴가
#   「기술 → 경직」으로 자연스럽게 끝난다.
#
# ──────────────────────────────────────────────────────────────────────────
#   (모션, 라벨 y0, 라벨 y1, 프레임 y0, 프레임 y1, x0, x1, 라벨 간격, 라벨 최대폭)
BANDS = [
    ("Idle",        50,  58,   60,  222,   10, 1090, 14, 16),
    ("Move",       276, 285,  288,  416,   10,  940, 14, 16),
    ("Turn",       276, 285,  288,  416,  960, 1510, 14, 16),
    ("MeleeAttack",459, 470,  471,  576,   10, 1512, 14, 16),
    ("Skill1",     623, 634,  635,  720,   10, 1005, 14, 16),
    ("Skill1",     623, 634,  635,  720, 1040, 1512, 14, 16),   # 착지 후 경직
    ("Skill2",     767, 780,  781,  865,   10, 1005, 40, 48),   # ⚠ "01  1타" 두 덩어리
    ("Skill2",     767, 780,  781,  865, 1040, 1512, 14, 16),   # 종료 후 경직
]

#: 파일 이름 접두사 — 다른 캐릭터와 같은 규약.
FILE_PREFIX = {
    "Idle": "Char_Idle",
    "Move": "Char_Move",
    "Turn": "Char_Turn",
    "MeleeAttack": "Char_MeleeAttack",
    "Skill1": "Char_Skill1",
    "Skill2": "Char_Skill2",
}

#: 방향(좌/우) 두 벌을 만들지 않는 모션.
NO_FACING = set()

# ──────────────────────────────────────────────────────────────────────────
# 맨 아래 「이펙트」 줄 — 세 묶음이 가로로 나란히 있다.
# 여기는 라벨이 아니라 <b>빈 열</b>로 갈라도 된다(조각이 완전히 떨어져 있다).
#   ⚠ 「대시 위치 표시」의 마지막 칸만 예외 — 흩뿌려진 핏방울이라 세 조각으로 갈라진다.
#     그래서 <b>조각 사이 간격이 이 값보다 좁으면 같은 프레임</b>으로 붙인다.
# ──────────────────────────────────────────────────────────────────────────
#: ⚠ <b>932 부터다.</b> 918~926 이 프레임 번호 줄이라 915 로 잡으면 뽑은 그림마다
#:   위쪽에 작은 「01」 「02」가 <b>같이 딸려 나온다</b>(실제로 그랬다).
FX_ROW = (932, 1012)
FX_MERGE_GAP = 6

#: (이름, x 범위, 기대 프레임 수, <b>잉크 문턱</b>)
#:
#: ⚠ <b>잔상만 문턱이 높다.</b> 돌진 잔상 네 장은 <b>연기가 서로 닿아 있어</b> 문턱 0 으로
#:   세면 앞의 세 장이 <b>한 덩어리</b>로 붙는다(실제로 2프레임이 나왔다). "한 열에 잉크가
#:   3픽셀 넘게 있어야 그림" 으로 올리면 옅은 연결부가 끊기고 네 장이 정확히 갈린다.
#:   ⚠ 더 올리면(16 이상) 잔상 자체가 조각나므로 3 이 맞다.
FX_STRIPS = [
    ("DashMark",  (10, 490), 6, 0),    # 대시 위치 표시 (타겟 위치 마커) → skill1Fx
    ("DashTrail", (500, 950), 4, 3),   # 대시 돌진 잔상 (몬스터 기준)    → skill1Projectile
    ("SixSlash",  (975, 1520), 6, 0),  # 6연타 휘두르기                  → skill2Fx
]

META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 9
  spritePivot: {{x: 0.5, y: 0}}
  spritePixelsToUnits: {ppu}
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META = ("fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\n"
               "DefaultImporter:\n  externalObjects: {{}}\n  userData: \n"
               "  assetBundleName: \n  assetBundleVariant: \n")


def md5_guid(rel):
    import hashlib
    return hashlib.md5(("LastSanctuary/" + rel).encode("utf-8")).hexdigest()


# ---------------------------------------------------------------------------
# 알파 뽑기
# ---------------------------------------------------------------------------

def to_rgba(rgb):
    """밝고 무채색인 곳을 투명하게. 가장자리는 두 임계값 사이에서 부드럽게 뺀다."""
    a = rgb.astype(np.float32)
    lum = a.mean(axis=2)
    sat = a.max(axis=2) - a.min(axis=2)

    # 배경다움 0(그림) ~ 1(배경): 밝을수록 배경, 단 채도가 높으면 그림으로 끌어내린다.
    t = (lum - BG_LUM_LO) / float(BG_LUM_HI - BG_LUM_LO)
    t = np.clip(t, 0.0, 1.0)
    t = np.where(sat > BG_SAT, 0.0, t)

    alpha = np.clip((1.0 - t) * 255.0, 0, 255).astype(np.uint8)
    out = np.dstack([rgb.astype(np.uint8), alpha])
    return out


def trim(rgba):
    """알파 경계로 여백을 자른다. 완전히 비었으면 None."""
    a = rgba[:, :, 3]
    ys, xs = np.nonzero(a > 8)
    if len(ys) == 0:
        return None, 0
    return rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1], int((a > 8).sum())


# ---------------------------------------------------------------------------
# 라벨 · 경계 찾기
# ---------------------------------------------------------------------------

def label_centers(dark, ly0, ly1, x0, x1, gap=LABEL_GAP, max_w=LABEL_MAX_W):
    sub = dark[ly0:ly1 + 1, x0:x1]
    cs = sub.sum(axis=0)
    segs, inb, run, s = [], False, 0, 0
    for x, v in enumerate(cs):
        if v > 0:
            if not inb:
                s, inb = x, True
            run = 0
        elif inb:
            run += 1
            if run >= gap:
                segs.append((s, x - run))
                inb = False
    if inb:
        segs.append((s, x1 - x0 - 1))

    return [x0 + (a + b) // 2 for a, b in segs if b - a + 1 <= max_w]


def cut_points(ink_cols, centers, x0, x1):
    """이웃한 두 라벨 사이에서 <b>잉크가 가장 없는 열</b>을 찾아 자른다."""
    cuts = [x0]
    for i in range(len(centers) - 1):
        lo, hi = centers[i] + 6, centers[i + 1] + 6
        lo, hi = max(lo, x0 + 1), min(hi, x1 - 1)
        if hi <= lo:
            cuts.append((centers[i] + centers[i + 1]) // 2)
            continue

        window = ink_cols[lo:hi]
        best = int(np.argmin(window))
        # 0 인 구간이 여러 열이면 그 <b>한가운데</b>를 고른다 — 한쪽 끝에서 자르면
        # 그림이 여백 없이 딱 붙어 다음 프레임의 첫 픽셀을 물고 갈 수 있다.
        zeros = np.nonzero(window == window[best])[0]
        best = int(zeros[len(zeros) // 2])
        cuts.append(lo + best)
    cuts.append(x1)
    return cuts


# ---------------------------------------------------------------------------

def write_png(img, folder, name):
    os.makedirs(folder, exist_ok=True)
    rel = os.path.relpath(folder, os.path.join(PROJECT, "Assets")).replace("\\", "/")
    path = os.path.join(folder, name + ".png")
    img.save(path)
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(META.format(guid=md5_guid(rel + "/" + name + ".png"), ppu=PPU))

    # 폴더 meta 도 같이 (없으면 유니티가 만들지만 guid 가 매번 달라진다)
    mp = folder.rstrip("\\/") + ".meta"
    if not os.path.exists(mp):
        with open(mp, "w", encoding="utf-8", newline="\n") as f:
            f.write(FOLDER_META.format(guid=md5_guid(rel)))


def main():
    if not os.path.exists(SRC):
        raise SystemExit("⚠ 원본이 없습니다: " + SRC)

    rgb = np.asarray(Image.open(SRC).convert("RGB"))
    rgba_full = to_rgba(rgb)

    lum = rgb.astype(np.int16).mean(axis=2)
    sat = rgb.astype(np.int16).max(axis=2) - rgb.astype(np.int16).min(axis=2)
    dark = (lum < LABEL_LUM) & (sat < LABEL_SAT)

    counts = {}
    for motion, ly0, ly1, y0, y1, x0, x1, gap, max_w in BANDS:
        centers = label_centers(dark, ly0, ly1, x0, x1, gap, max_w)
        if not centers:
            print("  ⚠ %s (%d..%d) 라벨을 못 찾았습니다 — 건너뜁니다" % (motion, x0, x1))
            continue

        band = rgba_full[y0:y1 + 1, :, :]
        ink_cols = (band[:, :, 3] > 8).sum(axis=0)
        cuts = cut_points(ink_cols, centers, x0, x1)

        frames = []
        for i in range(len(cuts) - 1):
            cell, area = trim(band[:, cuts[i]:cuts[i + 1], :])
            if cell is not None:
                frames.append((cell, area))

        if not frames:
            continue
        peak = max(a for _, a in frames)
        frames = [c for c, a in frames if a >= peak * MIN_BODY_AREA_RATIO]

        idx = counts.get(motion, 0)
        prefix = FILE_PREFIX[motion]
        for cell in frames:
            img = Image.fromarray(cell, "RGBA")
            folder = os.path.join(DST_ROOT, motion)
            if motion in NO_FACING:
                write_png(img, folder, "%s_%02d" % (prefix, idx))
            else:
                # ★ 원본이 <b>왼쪽</b>을 본다 — 오른쪽은 좌우 반전이다.
                write_png(img, folder, "%s_Left_%02d" % (prefix, idx))
                write_png(img.transpose(Image.FLIP_LEFT_RIGHT), folder,
                          "%s_Right_%02d" % (prefix, idx))
            idx += 1
        counts[motion] = idx
        print("  %-12s %s..%s  %2d프레임" % (motion, x0, x1, len(frames)))

    # ── 이펙트 줄 ────────────────────────────────────────────────────
    fy0, fy1 = FX_ROW
    band = rgba_full[fy0:fy1 + 1, :, :]
    ink = (band[:, :, 3] > 8).sum(axis=0)
    for name, (x0, x1), expect, thr in FX_STRIPS:
        segs, inb, run, s = [], False, 0, 0
        for x in range(x0, x1):
            if ink[x] > thr:
                if not inb:
                    s, inb = x, True
                run = 0
            elif inb:
                run += 1
                if run >= FX_MERGE_GAP:
                    segs.append((s, x - run))
                    inb = False
        if inb:
            segs.append((s, x1 - 1))

        folder = os.path.join(DST_ROOT, "Fx")
        n = 0
        for a, b in segs:
            cell, area = trim(band[:, a:b + 1, :])
            if cell is None:
                continue
            write_png(Image.fromarray(cell, "RGBA"), folder, "Char_Fx_%s_%02d" % (name, n))
            n += 1
        flag = "" if n == expect else "  ⚠ 기대 %d" % expect
        print("  Fx %-10s %2d프레임%s" % (name, n, flag))

    print()
    print("→", DST_ROOT)
    print("다음: python Tools/gen_kasinoma_skin.py")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    main()
