# -*- coding: utf-8 -*-
"""카르시노스(에픽 중립 보스 1004) 모션 시트 → 프레임 분해 (2026-08-15).

유저 지시: *"카르시노스 스킨 에셋 만들어 스킬은 아직 추가 안했으니 우선 대기 이동 근거리
공격만 넣어놓은 이미지 두장 적절하게 짤라서 넣어."*

원본
----
볼트에 시트가 **두 장** 있다. 둘 다 1536x1024 이고 5행 x 6열, 같은 모션 구성이다.

  · ``Carcinos_asset_01.png`` — 개체가 작게 그려져 있고, 맨 아래에 **이펙트 전용 행**이 따로 있다
  · ``Carcinos_asset_02.png`` — 개체가 **더 크게(고해상도로)** 그려져 있고 이펙트가 프레임에 통합돼 있다

**`asset_02` 를 스킨 원본으로 쓴다.** 이유는 개체당 픽셀 수가 더 많아서다 — 게임 안 크기는
`contentSizeTiles` 로 정규화되므로(61·66절) 원화가 클수록 선명하다.
`asset_01` 은 **버리지 않는다**: 분리된 이펙트 행(할퀴기 궤적 6장 · 포효 파동 4장)이
스킬을 구현할 때 그대로 필요하다. 그때 이 스크립트에 행을 추가하면 된다.

이번에 넣는 것: **대기(Idle) · 이동(Walk) · 근거리 공격(MeleeAttack) 3종뿐이다.**
스킬 1·2 행은 표에 스킬이 아직 없어서 건너뛴다(유저 지시).

★ 히스톤 시트와 결정적으로 다른 점 — **자를 때 고생할 일이 없다**
--------------------------------------------------------------
히스톤은 프레임이 x축에서 실제로 겹쳐 있어 자동 분리를 네 가지 방법으로 시도해 **네 번 다
실패**했고 결국 경계표를 손으로 쟀다(84-1절). 카르시노스는 실측해보니 **행마다 6개 덩어리가
완전히 떨어져 있다** — 그래서 연결 성분(빈 열) 탐지가 그대로 통한다. 손으로 잰 표가 없으므로
**원화를 다시 받아도 이 스크립트가 그대로 동작한다.**

★ 알파 — 배경이 **흰색**이다 (히스톤과 반대)
-------------------------------------------
히스톤은 순흑 배경에 검은 갑옷이라 밝기로 가를 수 없었고 실루엣 채우기까지 갔다(84-8절).
카르시노스는 **밝은 배경(253,253,253) 에 어두운 몸** 이라 배경과의 거리로 깨끗하게 갈린다.
뿔·두개골이 크림색(대략 200,190,170)이지만 배경과의 거리가 199 라 임계값 아래로 안 떨어진다.

방향
----
원본은 **왼쪽을 본다**(이동 행에서 확인). 그래서 ``Left`` 가 원본이고 ``Right`` 는 좌우 반전이다.
⚠ 히스톤·피올로·엘린은 반대다(원본이 오른쪽) — 캐릭터마다 다르므로 새 원화마다 확인할 것.

사용법:  python Tools/carcinos_skin_build.py
"""

import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT

SRC = os.path.join(VAULT, "리소스", "asset", "Carcinos_asset_02.png")
DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Carcinos", "Char")

# 1픽셀당 유니티 단위. 게임 안 크기는 contentSizeTiles 로 정규화되므로(61·66절)
# 이 값이 화면 크기를 정하지는 않는다 — 다른 유닛과 같은 대역이면 된다.
PPU = 64

# 배경과 이만큼 떨어지면 그림으로 본다. 안티에일리어싱 가장자리는 두 값 사이에서 부드럽게 뺀다.
ALPHA_LO = 24
ALPHA_HI = 60

# ★ <b>프레임을 가르는 임계값은 알파용보다 높다</b> — 이 둘을 같은 값으로 쓰면 안 된다.
#
#   알파용(24)으로 열을 훑으면 흐릿한 먼지·잔광까지 잡혀서 <b>이동 행 6프레임이 통째로
#   하나로 붙는다</b>(실제로 그렇게 나왔다). 프레임 사이 실측 간격이 42~54px 인데
#   그 틈이 옅은 픽셀로 메워지기 때문이다.
#
#   40 으로 올리면 세 행 모두 정확히 6덩어리로 갈라진다(실측: 대기 최소 간격 71 ·
#   이동 42 · 근접 20). 알파는 여전히 24 를 쓰므로 <b>그림이 깎이지는 않는다</b> —
#   가르는 기준과 지우는 기준을 분리한 것뿐이다.
SEG_THRESHOLD = 40

# 이보다 가까운 덩어리는 같은 프레임의 조각으로 본다.
# 근접 공격 행에 본체에서 2~3px 떨어진 이펙트 조각이 실제로 있고, 프레임 사이는 최소 20px 이다.
MERGE_GAP = 10

# 왼쪽 행 라벨(한글)이 있는 구역. 이 x 미만은 통째로 무시한다 —
# 실측 결과 프레임은 x 206 부터 시작하므로 190 이면 안전하다.
LABEL_X_LIMIT = 190

# 잘라낼 행. (게임에서 쓰는 모션 이름, 시트에서의 y 범위)
#   ⚠ y 범위는 이 스크립트가 스스로 찾는다 — 아래 detect_rows() 참조.
#      여기 순서가 곧 시트 위에서 아래 순서다.
WANTED_ROWS = ["Idle", "Walk", "MeleeAttack"]

# 시트의 실제 행 순서 (스킬 2행은 이번에 안 쓴다)
SHEET_ROW_ORDER = ["Idle", "Walk", "MeleeAttack", "Skill1", "Skill2"]

# 파일 이름에 쓰는 접두사 — 다른 캐릭터와 같은 규약이다.
FILE_PREFIX = {"Idle": "Char_Idle", "Walk": "Char_Walk", "MeleeAttack": "Char_MeleeAttack"}

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

FOLDER_META = "fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def guid_for(key):
    """경로에서 결정적으로 뽑은 guid — 다시 돌려도 같은 값이라 참조가 안 끊긴다."""
    import hashlib
    return hashlib.md5(("LastSanctuary/" + key).encode("utf-8")).hexdigest()


def bands(flags, min_len=1):
    """True 가 이어지는 구간 목록 [(시작, 끝)]."""
    out, run = [], None
    for i, v in enumerate(flags):
        if v:
            if run is None:
                run = i
        elif run is not None:
            out.append((run, i - 1))
            run = None
    if run is not None:
        out.append((run, len(flags) - 1))
    return [b for b in out if b[1] - b[0] + 1 >= min_len]


def detect_rows(mask):
    """
    내용이 있는 가로 밴드를 찾아 <b>아래로 갈수록</b> 순서대로 돌려준다.

    맨 위의 열 번호(1~6) 밴드는 높이가 20px 남짓이라 걸러진다 — 모션 행은 140px 이상이다.
    """
    found = bands(mask.sum(axis=1) > 0, min_len=60)
    if len(found) < len(SHEET_ROW_ORDER):
        raise SystemExit(f"⚠ 모션 행을 {len(SHEET_ROW_ORDER)}개 찾지 못했습니다: {found}")
    return dict(zip(SHEET_ROW_ORDER, found[:len(SHEET_ROW_ORDER)]))


def detect_frames(mask, y0, y1):
    """
    한 행 안에서 프레임 6개의 x 범위를 찾는다.

    ★ 카르시노스는 프레임끼리 <b>실제로 떨어져 있어서</b> 빈 열 탐지가 그대로 통한다
      (히스톤은 겹쳐 있어서 이 방법이 실패했다 — 84-1절 ③).

    다만 이펙트 조각이 본체에서 몇 픽셀 떨어져 나오는 경우가 있어(근거리 공격 행에서
    실제로 3~5px 짜리 조각이 관측된다), <b>가까운 덩어리는 하나로 합친다.</b>
    """
    sub = mask[y0:y1 + 1, :]
    colhit = sub.sum(axis=0) > 0
    colhit[:LABEL_X_LIMIT] = False          # 왼쪽 한글 라벨 제거

    raw = bands(colhit, min_len=1)
    if not raw:
        raise SystemExit("⚠ 프레임을 하나도 못 찾았습니다")

    # 붙어 있는 조각(이펙트 파편)만 합친다 — 위 MERGE_GAP 주석 참조.
    merged = [list(raw[0])]
    for s, e in raw[1:]:
        if s - merged[-1][1] <= MERGE_GAP:
            merged[-1][1] = e
        else:
            merged.append([s, e])

    if len(merged) != 6:
        raise SystemExit(f"⚠ 프레임이 6개가 아닙니다({len(merged)}개): {merged}")
    return [tuple(m) for m in merged]


def to_rgba(rgb_block, bg):
    """배경(밝은 색)과의 거리로 알파를 만든다. 가장자리만 부드럽게."""
    dist = np.abs(rgb_block.astype(int) - bg).sum(axis=2)
    alpha = np.clip((dist - ALPHA_LO) * 255.0 / (ALPHA_HI - ALPHA_LO), 0, 255)
    out = np.dstack([rgb_block, alpha.astype(np.uint8)])
    return out.astype(np.uint8)


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


def main():
    if not os.path.isfile(SRC):
        print("⚠ 원본이 없습니다:", SRC)
        return 1

    im = Image.open(SRC).convert("RGB")
    arr = np.asarray(im).astype(np.uint8)
    bg = arr[0, 0].astype(int)
    dist = np.abs(arr.astype(int) - bg).sum(axis=2)

    mask = dist > ALPHA_LO          # 그림의 실제 경계(잘라낼 상자를 재는 데 쓴다)
    seg = dist > SEG_THRESHOLD      # 프레임을 가르는 데만 쓴다 (위 주석 참조)

    rows = detect_rows(seg)
    print(f"원본 {im.size[0]}x{im.size[1]} · 배경 {tuple(bg)}")

    made = 0
    for motion in WANTED_ROWS:
        y0, y1 = rows[motion]
        frames = detect_frames(seg, y0, y1)

        # ── 캔버스 크기: 이 행의 모든 프레임이 안 잘리는 최소 크기 ────────────
        #    ⚠ 프레임마다 따로 자르면 모션 중에 캐릭터가 위아래·좌우로 튄다.
        boxes = []
        for x0, x1 in frames:
            sub = mask[y0:y1 + 1, x0:x1 + 1]
            ys = np.where(sub.any(axis=1))[0]
            xs = np.where(sub.any(axis=0))[0]
            boxes.append((x0 + xs.min(), x0 + xs.max(), y0 + ys.min(), y0 + ys.max()))

        w = max(b[1] - b[0] + 1 for b in boxes)
        h = max(b[3] - b[2] + 1 for b in boxes)

        folder = os.path.join(DST_ROOT, motion)
        for i, (bx0, bx1, by0, by1) in enumerate(boxes):
            block = arr[by0:by1 + 1, bx0:bx1 + 1]
            rgba = to_rgba(block, bg)

            # ★ 가로는 <b>그림 중심</b>을, 세로는 <b>바닥</b>을 캔버스에 맞춘다.
            #   피벗이 (0.5, 0) = 발밑이라 바닥을 맞춰야 모션이 바뀔 때 위아래로 안 튄다.
            canvas = np.zeros((h, w, 4), dtype=np.uint8)
            bw, bh = bx1 - bx0 + 1, by1 - by0 + 1
            ox = (w - bw) // 2
            oy = h - bh
            canvas[oy:oy + bh, ox:ox + bw] = rgba

            left = Image.fromarray(canvas, "RGBA")                      # 원본이 왼쪽을 본다
            right = left.transpose(Image.FLIP_LEFT_RIGHT)

            write_png(left, folder, f"{FILE_PREFIX[motion]}_Left_{i:02d}")
            write_png(right, folder, f"{FILE_PREFIX[motion]}_Right_{i:02d}")
            made += 2

        ensure_folder_meta(folder)
        print(f"  {motion}: {w} x {h} · 6장 (+ 좌우 반전 6) · 행 y {y0}~{y1}")

    ensure_folder_meta(DST_ROOT)
    ensure_folder_meta(os.path.dirname(DST_ROOT))
    print(f"\n프레임 {made}장 생성 → {DST_ROOT}")
    print("다음: python Tools/gen_carcinos_skin.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
