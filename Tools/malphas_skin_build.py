# -*- coding: utf-8 -*-
"""말파스(최종보스 120002) 모션 시트 → 프레임 분해 (2026-08-18).

원본
----
``<볼트>/리소스/Malphas_asset.png`` (1536x1024, 배경 흰색 254,254,254).
카르시노스와 달리 **한 장에 설명·일러스트·모션·이펙트가 전부 들어 있는 기획 시트**다.
왼쪽/오른쪽 두 단으로 나뉘고(세로 구분선 x≈634), 단마다 제목 막대 + 프레임 번호 라벨 +
프레임 행이 반복된다.

★ 카르시노스와 결정적으로 다른 점 — **빈 열 탐지로 프레임을 가를 수 없다**
--------------------------------------------------------------------
저주광선 행의 14~16번은 **레이저가 프레임 세 칸을 가로질러** 그려져 있고, 구속탄 행도
초록 구체가 다음 칸 몸통까지 닿는다. 즉 프레임 사이에 빈 열이 아예 없다.

대신 이 시트에는 **프레임 번호 라벨(01, 02 …)이 프레임마다 하나씩** 찍혀 있고
간격이 일정하다. 그래서 **라벨 덩어리의 x 중심으로 경계를 잡는다** — 라벨은 서로
절대 붙지 않으므로 이 방법은 레이저가 어떻게 뻗든 영향을 받지 않는다.
(히스톤이 겹친 프레임 때문에 자동 분리를 네 번 실패하고 손으로 잰 것과 같은 문제인데,
이 시트는 라벨이라는 더 나은 단서가 있다 — 84-1절)

⚠ 라벨이 프레임 수와 안 맞는 행이 있다
--------------------------------------
시트가 손으로 만들어져서 **번호가 건너뛴다**(이동 행에 13 없음 · 근거리 행에 08·11 없음).
그래서 "몇 장이어야 한다"를 강요하지 않고 **찾은 라벨 수만큼** 뽑는다 —
게임에서는 프레임 수가 몇 장이든 그대로 순환 재생되므로 문제가 없다.

방향
----
말파스는 **정면을 보는 좌우 대칭** 개체다. 다만 투사체·레이저가 **오른쪽으로** 나가므로
원본을 ``Right`` 로 보고 ``Left`` 를 좌우 반전으로 만든다.
⚠ 카르시노스는 반대다(원본이 왼쪽) — 새 원화마다 확인할 것.

사용법:  python Tools/malphas_skin_build.py
다음:    python Tools/gen_malphas_skin.py
"""

import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT

SRC = os.path.join(VAULT, "리소스", "Malphas_asset.png")
DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Malphas", "Char")

#: 1픽셀당 유니티 단위. 게임 안 크기는 contentSizeTiles 로 정규화되므로(61·66절)
#: 이 값이 화면 크기를 정하지 않는다 — 다른 유닛과 같은 대역이면 된다.
PPU = 64

#: 배경과 이만큼 떨어지면 그림으로 본다(세 채널 차이의 <b>합</b>). 가장자리는 두 값 사이에서
#: 부드럽게 뺀다.
#:
#: ⚠ 카르시노스(24/60)보다 <b>훨씬 높다.</b> 그 값으로 뽑아 보니 촉수 둘레에 <b>흰 테두리</b>가
#:   남았다 — 원본이 안티에일리어싱된 흰 배경 위에 있어서 (230,230,230) 같은 가장자리 픽셀의
#:   거리가 72 라 24/60 기준으로는 <b>완전 불투명</b>이 된다. 60/180 으로 올리면 그 픽셀은
#:   알파 25 로 거의 사라지고, 진짜 밝은 부분(뼈 크림색 213,170,131 → 거리 248)은 그대로 남는다.
ALPHA_LO = 60
ALPHA_HI = 180

#: 라벨(작은 검은 숫자)을 찾을 때 쓰는 임계값. 알파용보다 높다 — 옅은 잔광을 라벨로
#: 오인하면 프레임 경계가 통째로 어긋난다.
LABEL_THRESHOLD = 90

#: 라벨 글자 높이(px). 라벨 행은 이 높이 안에서 전부 끝난다.
LABEL_H = 14

#: 라벨 덩어리를 가르는 최소 빈 열. 숫자 두 자리('1'과 '2') 사이는 2~3px 이고
#: 라벨끼리는 40px 이상 떨어져 있다 — 그 사이 값이면 어디든 된다.
LABEL_GAP = 12

#: 라벨 덩어리의 최대 폭(px). ⚠ <b>이게 없으면 안 된다</b> — 저주광선 행에서 레이저 착탄
#: 폭발이 라벨 줄까지 세로로 걸쳐서 <b>아홉 번째 라벨</b>로 잡혔고, 그 결과 프레임이 하나
#: 더 생겼다. 두 자리 숫자는 22px 안쪽이라 34 면 넉넉하다.
LABEL_MAX_W = 34

#: 몸통 모션에서 <b>이 비율보다 그림이 적은</b> 프레임은 버린다(그 모션의 최대 잉크량 대비).
#: 원거리 공격 08·16번, 저주광선 마지막 칸처럼 <b>투사체·빔만 남고 본체가 없는</b> 칸이
#: 실제로 있는데, 그대로 두면 재생 중에 보스가 한 프레임 사라진다.
#: 투사체·빔은 따로 뽑으므로(FX_STRIPS) 버려도 손해가 없다.
#:
#: ⚠ 높이 비율이 아니라 <b>잉크 픽셀 수</b>로 잰다 — 저주광선 마지막 칸은 빔 착탄이
#:   위아래로 퍼져서 <b>높이는 본체만큼 크다.</b> 높이로 재면 안 걸러진다(실제로 그랬다).
MIN_BODY_AREA_RATIO = 0.35

# ──────────────────────────────────────────────────────────────────────────
# 시트 배치표 — **실측값이다.** (세로 잉크 밴드 + 라벨 행 탐지로 재고 눈으로 확인함)
#
#   (모션, x0, x1, 라벨행 y, 프레임 y0, 프레임 y1, 라벨 상한)
#
# 같은 모션이 여러 줄이면 여기 여러 번 나온다 — 위에서 아래 순서가 곧 프레임 순서다.
# ⚠ x 범위는 <b>단 안쪽</b>으로 잡는다. 가운데 세로 구분선(x 634~635)이 들어오면
#   모든 행에서 그 선이 프레임 조각으로 잡힌다.
#
# <b>라벨 상한</b>(마지막 칸) — 0 이면 제한 없음. 저주광선 둘째 줄에서만 필요하다:
# 레이저 <b>착탄 폭발</b>이 라벨 줄 높이까지 걸쳐서 아홉 번째 라벨로 잡힌다. 폭·잉크량
# 어느 쪽으로도 안정적으로 못 거른다(폭발은 두 자리 숫자만큼 좁고 본체의 43%나 된다).
# 이럴 때는 "이 줄은 여덟 칸"이라는 <b>시트에 적힌 사실</b>을 그대로 쓰는 것이 맞다 —
# 임계값을 맞히려고 다른 줄까지 위태롭게 만들지 않는다.
# ──────────────────────────────────────────────────────────────────────────
BANDS = [
    # ── 오른쪽 단 ────────────────────────────────────────────────────
    ("Idle",         640, 1533,  36,  58, 146, 0),
    ("Move",         640, 1533, 188, 206, 287, 0),
    ("Move",         640, 1533, 300, 309, 391, 0),
    ("Skill1",       640, 1533, 423, 439, 505, 0),
    ("Skill1",       640, 1533, 504, 525, 590, 0),
    ("Skill2",       640, 1533, 622, 636, 698, 0),
    ("Skill2",       640, 1533, 697, 718, 779, 8),   # ← 레이저 착탄이 9번째 라벨로 잡힌다
    ("Hit",          640, 1533, 805, 821, 882, 0),
    # ── 왼쪽 단 ─────────────────────────────────────────────────────
    ("RangedAttack",   4,  630, 272, 289, 363, 0),
    ("RangedAttack",   4,  630, 377, 384, 458, 0),
    ("MeleeAttack",    4,  630, 489, 506, 578, 0),
    ("MeleeAttack",    4,  630, 587, 596, 665, 0),
    ("FxBindingOrb",   4,  630, 696, 718, 779, 0),
]

#: 파일 이름 접두사 — 다른 캐릭터와 같은 규약.
FILE_PREFIX = {
    "Idle": "Char_Idle",
    "Move": "Char_Move",
    "MeleeAttack": "Char_MeleeAttack",
    "RangedAttack": "Char_RangedAttack",
    "Skill1": "Char_Skill1",
    "Skill2": "Char_Skill2",
    "Hit": "Char_Hit",
}

#: 방향(좌/우) 두 벌을 만들지 않는 모션. 이펙트·투사체는 조준 각도만큼 통째로
#: 회전시켜 깔리므로(`CombatProjectileFx`) 방향별 원화를 넣으면 두 번 돌아간다.
NO_FACING = {"FxBindingOrb"}

# ──────────────────────────────────────────────────────────────────────────
# 맨 아래 「투사체 / 이펙트」 줄 — 네 묶음이 가로로 나란히 있다.
# 여기는 라벨이 아니라 <b>빈 열</b>로 갈라도 된다(각 조각이 완전히 떨어져 있다).
#   ⚠ 저주광선만 예외 — 레이저가 이어져 보이지만 실제로는 조각마다 끊겨 있다.
#     안 갈라지면 라벨 방식으로 떨어뜨린다(아래 count 를 근거로).
# ──────────────────────────────────────────────────────────────────────────
FX_LABEL_Y = 944
FX_ROW = (957, 1006)

#: (이름, x 범위, 기대 프레임 수, 통짜로 뽑을지)
#:
#: ★ <b>저주광선만 「통짜」다.</b> 이 묶음은 여덟 칸으로 <b>나눌 수 없다</b> — 빔이 왼쪽
#:   기점에서 자라나는 그림이라 뒤 프레임이 앞 프레임을 통째로 덮는다. 칸으로 자르면
#:   길이 53px 짜리 토막 여덟 개가 나와 <b>레이저처럼 보이지 않는다</b>(실제로 그렇게 나왔다).
#:   범위 연출(`CombatProjectileFx.PlayArea`)은 어차피 그림 <b>한 장</b>을 표의 가로 x 세로
#:   상자에 맞춰 늘려 까는 물건이라, 가장 긴 빔 한 장이 정확히 필요한 것이다
#:   (단탈리온의 skill2Fx 도 한 장이다).
FX_STRIPS = [
    ("Projectile", (12, 402), 8, False),      # 기본 원거리 투사체 (검은 구체)
    ("BindingOrb", (408, 766), 7, False),     # 구속탄 투사체 (초록 구체 · 시트에 05번이 없다)
    ("CurseBeam", (772, 1230), 1, True),      # 저주광선 (레이저) — 통짜 한 장
    ("Impact", (1234, 1533), 4, False),       # 레이저 임팩트
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
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
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
    physicsShape: []
    bones: []
    spriteID: {sprite_id}
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


def guid_for(key):
    """경로에서 결정적으로 뽑은 guid — 다시 돌려도 같은 값이라 참조가 안 끊긴다."""
    import hashlib
    return hashlib.md5(("LastSanctuary/" + key).encode("utf-8")).hexdigest()


def bands(flags, gap=1, min_len=1):
    """
    True 가 이어지는 구간 목록 [(시작, 끝)]. <paramref>gap</paramref> 픽셀 이하로 떨어진
    구간은 하나로 본다 — 숫자 두 자리를 한 라벨로 묶는 데 쓴다.
    """
    out, run, hole = [], None, 0
    for i, v in enumerate(flags):
        if v:
            run = i if run is None else run
            hole = 0
        elif run is not None:
            hole += 1
            if hole > gap:
                if i - hole - run >= min_len:
                    out.append((run, i - hole - 1))
                run, hole = None, 0
    if run is not None and len(flags) - run >= min_len:
        out.append((run, len(flags) - 1))
    return out


def label_centers(dark, x0, x1, label_y):
    """
    라벨 행에서 프레임 번호 덩어리의 x 중심 목록. **프레임 경계의 유일한 근거**다.
    """
    strip = dark[label_y:label_y + LABEL_H, x0:x1 + 1]
    hit = strip.any(axis=0)
    found = bands(hit, gap=LABEL_GAP, min_len=3)
    return [x0 + (a + b) // 2 for a, b in found if b - a + 1 <= LABEL_MAX_W]


def split_by_centers(centers, x0, x1):
    """
    라벨 중심 사이의 <b>중점</b>을 경계로 삼는다. 양 끝은 반 칸씩 더 준다.

    ⚠ 균등 분할이 아니다 — 마지막 줄처럼 프레임이 몇 장 없는 행에서도
      칸 폭이 앞줄과 같게 유지된다(균등 분할하면 3장짜리 줄에서 칸이 5배로 넓어진다).
    """
    if not centers:
        return []
    if len(centers) == 1:
        return [(x0, x1)]

    pitch = int(round((centers[-1] - centers[0]) / (len(centers) - 1)))
    out = []
    for i, c in enumerate(centers):
        left = x0 if i == 0 else (centers[i - 1] + c) // 2
        right = x1 if i == len(centers) - 1 else (c + centers[i + 1]) // 2 - 1
        # 마지막 칸이 단 끝까지 늘어나 옆 이펙트를 삼키지 않게 한 칸 폭으로 제한한다.
        right = min(right, c + pitch // 2)
        left = max(left, c - pitch // 2)
        out.append((left, right))
    return out


def to_rgba(rgb_block, bg):
    """배경(흰색)과의 거리로 알파를 만든다. 가장자리만 부드럽게."""
    dist = np.abs(rgb_block.astype(int) - bg).sum(axis=2)
    alpha = np.clip((dist - ALPHA_LO) * 255.0 / (ALPHA_HI - ALPHA_LO), 0, 255)
    return np.dstack([rgb_block, alpha.astype(np.uint8)]).astype(np.uint8)


def write_png(img, folder, name):
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name + ".png")
    img.save(path)

    rel = os.path.relpath(path, os.path.join(PROJECT, "Assets", "_Project")).replace("\\", "/")
    g = guid_for(rel)
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(META.format(guid=g, ppu=PPU, sprite_id=g[:32]))
    return path


def ensure_folder_meta(path):
    mp = path.rstrip("\\/") + ".meta"
    if os.path.exists(mp):
        return
    rel = os.path.relpath(path, os.path.join(PROJECT, "Assets", "_Project")).replace("\\", "/")
    with open(mp, "w", encoding="utf-8", newline="\n") as f:
        f.write(FOLDER_META.format(guid=guid_for(rel)))


def boxes_for(mask, cells, y0, y1):
    """각 칸 안의 그림 경계 상자. 그림이 없는 칸은 None."""
    out = []
    for cx0, cx1 in cells:
        sub = mask[y0:y1 + 1, cx0:cx1 + 1]
        if not sub.any():
            out.append(None)
            continue
        ys = np.where(sub.any(axis=1))[0]
        xs = np.where(sub.any(axis=0))[0]
        out.append((cx0 + xs.min(), cx0 + xs.max(), y0 + ys.min(), y0 + ys.max()))
    return out


def main():
    if not os.path.isfile(SRC):
        print("⚠ 원본이 없습니다:", SRC)
        return 1

    im = Image.open(SRC).convert("RGB")
    arr = np.asarray(im).astype(np.uint8)
    bg = arr[0, 0].astype(int)
    dist = np.abs(arr.astype(int) - bg).sum(axis=2)

    mask = dist > ALPHA_LO              # 그림 경계를 재는 데 쓴다
    dark = dist > LABEL_THRESHOLD       # 라벨 글자를 찾는 데만 쓴다

    print("원본 %dx%d · 배경 %s" % (im.size[0], im.size[1], tuple(bg)))

    # ── 1) 모션 행 ────────────────────────────────────────────────────
    #    같은 모션의 여러 줄을 <b>한 캔버스 크기</b>로 맞춰야 재생 중에 안 튄다 —
    #    그래서 먼저 전부 모아 상자를 재고, 그 다음에 쓴다.
    collected = {}
    for motion, x0, x1, ly, y0, y1, limit in BANDS:
        centers = label_centers(dark, x0, x1, ly)
        if limit and len(centers) > limit:
            centers = centers[:limit]      # 남는 것은 항상 오른쪽 끝의 연출이다
        cells = split_by_centers(centers, x0, x1)
        boxes = [b for b in boxes_for(mask, cells, y0, y1) if b is not None]
        collected.setdefault(motion, []).extend(boxes)
        print("  %-13s y %4d~%-4d · 라벨 %2d개 → 프레임 %2d장"
              % (motion, y0, y1, len(centers), len(boxes)))

    made = 0
    for motion, boxes in collected.items():
        if not boxes:
            print("  ⚠ %s: 프레임을 못 찾았습니다" % motion)
            continue

        # ★ 본체가 없는 칸을 버린다 (MIN_BODY_HEIGHT_RATIO 주석 참조).
        #   이펙트 묶음은 원래 본체가 없으므로 거르지 않는다.
        if motion not in NO_FACING:
            areas = [int(mask[b[2]:b[3] + 1, b[0]:b[1] + 1].sum()) for b in boxes]
            biggest = max(areas)
            kept = [b for b, a in zip(boxes, areas)
                    if a >= biggest * MIN_BODY_AREA_RATIO]
            if len(kept) != len(boxes):
                print("  %-13s 본체 없는 칸 %d장 버림 (투사체만 남은 프레임)"
                      % (motion, len(boxes) - len(kept)))
                boxes = kept

        w = max(b[1] - b[0] + 1 for b in boxes)
        h = max(b[3] - b[2] + 1 for b in boxes)

        folder = os.path.join(DST_ROOT, motion)
        prefix = FILE_PREFIX.get(motion, "Char_" + motion)

        for i, (bx0, bx1, by0, by1) in enumerate(boxes):
            rgba = to_rgba(arr[by0:by1 + 1, bx0:bx1 + 1], bg)

            # ★ 가로는 <b>그림 중심</b>, 세로는 <b>바닥</b>을 캔버스에 맞춘다.
            #   피벗이 (0.5, 0) = 발밑이라 바닥을 맞춰야 모션 전환에서 위아래로 안 튄다.
            canvas = np.zeros((h, w, 4), dtype=np.uint8)
            bw, bh = bx1 - bx0 + 1, by1 - by0 + 1
            ox, oy = (w - bw) // 2, h - bh
            canvas[oy:oy + bh, ox:ox + bw] = rgba

            right = Image.fromarray(canvas, "RGBA")        # 원본이 오른쪽으로 쏜다
            if motion in NO_FACING:
                write_png(right, folder, "%s_%02d" % (prefix, i))
                made += 1
            else:
                write_png(right, folder, "%s_Right_%02d" % (prefix, i))
                write_png(right.transpose(Image.FLIP_LEFT_RIGHT), folder,
                          "%s_Left_%02d" % (prefix, i))
                made += 2

        ensure_folder_meta(folder)
        print("  %-13s %3d x %3d · %2d장%s"
              % (motion, w, h, len(boxes), "" if motion in NO_FACING else " (+좌우 반전)"))

    # ── 2) 투사체 / 이펙트 줄 ────────────────────────────────────────
    made += build_fx(arr, mask, dark, bg)

    ensure_folder_meta(DST_ROOT)
    ensure_folder_meta(os.path.dirname(DST_ROOT))
    print("\n프레임 %d장 생성 → %s" % (made, DST_ROOT))
    print("다음: python Tools/gen_malphas_skin.py")
    return 0


def build_fx(arr, mask, dark, bg):
    """
    맨 아래 「투사체 / 이펙트」 줄. 묶음마다 <b>캔버스를 따로</b> 잡는다 —
    레이저(가로로 아주 긴 것)와 구체(작고 둥근 것)를 한 캔버스로 묶으면
    구체가 레이저 길이만큼의 투명 여백을 달고 다녀서 연출이 작아 보인다.
    """
    y0, y1 = FX_ROW
    folder = os.path.join(DST_ROOT, "Fx")
    made = 0

    for name, (x0, x1), count, whole in FX_STRIPS:
        if whole:
            cells = [(x0, x1)]
        else:
            cells = split_by_centers(label_centers(dark, x0, x1, FX_LABEL_Y), x0, x1)
        boxes = [b for b in boxes_for(mask, cells, y0, y1) if b is not None]

        if not boxes:
            print("  ⚠ Fx/%s: 프레임을 못 찾았습니다" % name)
            continue

        w = max(b[1] - b[0] + 1 for b in boxes)
        h = max(b[3] - b[2] + 1 for b in boxes)

        for i, (bx0, bx1, by0, by1) in enumerate(boxes):
            rgba = to_rgba(arr[by0:by1 + 1, bx0:bx1 + 1], bg)
            canvas = np.zeros((h, w, 4), dtype=np.uint8)
            bw, bh = bx1 - bx0 + 1, by1 - by0 + 1
            # 이펙트는 <b>가운데</b> 정렬 — 발밑 기준이 아니라 범위 한가운데 깔린다.
            canvas[(h - bh) // 2:(h - bh) // 2 + bh, (w - bw) // 2:(w - bw) // 2 + bw] = rgba
            write_png(Image.fromarray(canvas, "RGBA"), folder, "Char_Fx_%s_%02d" % (name, i))
            made += 1

        note = "" if len(boxes) == count else "  ⚠ 기대 %d장" % count
        print("  Fx/%-11s %3d x %3d · %2d장 (방향 없음)%s" % (name, w, h, len(boxes), note))

    ensure_folder_meta(folder)
    return made


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
