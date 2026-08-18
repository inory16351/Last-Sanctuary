# -*- coding: utf-8 -*-
"""넥서스(중앙 건물) 모션 시트 → 프레임 분해 (2026-08-18).

유저 지시: *"넥서스(중앙 건물) 클릭 가능하게 만들고 일러스트 넣어서 ILLUST UI 에 적용하고,
스프라이트 이미지 찾아보고 스킨 만들어서 심장 뛰는 거 처럼 만들어줘 모션 끼워 맞춰서"*

원본
----
``<볼트>/리소스/asset/Tower_Asset/Nexus_Spr.png`` (1402x1122, 배경 <b>검정</b>).

시트에 <b>체력 구간별 대기 모션 3벌 + 파괴 모션 1벌</b>이 들어 있다:

  1행  체력 50% 이상   6프레임 — "심장 박동 강함"
  2행  체력 10~50%     6프레임 — "심장 박동 약화 · 균열 및 손상"
  3행  체력 10% 이하   6프레임 — "심장 박동 불규칙 · 심각한 손상"
  4행  파괴            8프레임 — "붕괴 및 파괴 · 완전 파괴 후 정지"

시트 하단의 지시: *"각 프레임은 64x64px (권장) / 2x2 타일 크기 기준 · 탐뷰 3/4 시점 픽셀
아트 · Unity에서 프레임 속도 6~8 FPS 권장"* — 프레임 속도는 `gen_nexus_skin.py` 가
`framesPerSecond: 7` 로 넣는다.

★★ 배경을 <b>밝기로 자르면 안 된다</b> — 그림이 배경보다 어둡다
------------------------------------------------------------
배경은 (13,13,13) 짙은 회색인데, 고딕 성당의 그늘은 <b>순수 검정 (0,0,0)</b> 이다.
즉 "어두우면 배경" 규칙을 쓰면 <b>건물 몸통이 통째로 지워지고</b> 붉은 심장과 첨탑만
공중에 뜬다 — 처음에 그렇게 나왔다.

그래서 <b>테두리에서 흘려 채운다</b>(flood fill):

  ① 배경색 (13,13,13) 과 채널 차가 ``BG_TOL`` 이내인 픽셀을 후보로 본다.
  ② 그중 <b>이미지 테두리와 이어진 덩어리만</b> 배경으로 확정한다.

이러면 배경과 같은 밝기의 그림(그늘)이 <b>안쪽에 갇혀 있으므로</b> 살아남는다.
⚠ 첨탑 사이의 완전히 둘러싸인 틈은 불투명하게 남는다 — 건물 실루엣의 일부로 읽히므로
  그대로 두는 편이 낫다(잘라내려 하면 그늘까지 다시 뚫린다).

★ 프레임을 어떻게 가르나
-----------------------
각 행 왼쪽 끝(x < 190)은 <b>설명 글상자</b>다 — 잘라내고 시작한다. 나머지는 <b>빈 열</b>로
갈린다.

⚠ <b>"한 픽셀이라도 있으면 그림" 으로 세면 안 된다.</b> 위 flood fill 을 통과한 잡티가
  프레임 사이에 드문드문 남아, 파괴 행이 8칸 → <b>3칸</b>으로 붙어 버렸다. 한 열에
  ``COL_INK`` 픽셀 넘게 있어야 그림으로 본다 — 그러면 네 행이 전부 정확히 갈린다.
  (한 픽셀짜리 잡티 기둥은 ``MIN_W`` 로 한 번 더 거른다.)

사용법:  python Tools/nexus_skin_build.py
다음:    python Tools/gen_nexus_skin.py
"""

import hashlib
import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT

SRC = os.path.join(VAULT, "리소스", "asset", "Tower_Asset", "Nexus_Spr.png")
DST_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                        "Char_Asset_Nexus")

#: 1픽셀당 유니티 단위. 넥서스는 3x3 타일(NexusDefinition.footprintTiles)이고 원화
#: 한 칸이 180px 안팎이라, 60 이면 대략 3타일로 그려진다 — 실제 크기는
#: `NexusAnimator.renderSizeTiles` 가 다시 맞추므로 이 값이 화면 크기를 정하지는 않는다.
PPU = 60

#: 배경색 — 시트 네 귀퉁이에서 잰 값. 잡티가 있어 아래 허용치와 같이 쓴다.
BG_COLOR = (13, 13, 13)

#: 배경으로 볼 채널 차 허용치. 잡티는 ±2 안쪽이고 <b>그림의 검정은 13</b>이라 6 이면 갈린다.
BG_TOL = 6

#: 테두리를 부드럽게 하려고 이만큼은 반투명으로 남긴다(0 이면 딱딱한 계단이 보인다).
EDGE_SOFT = 1

#: 왼쪽 설명 글상자의 오른쪽 끝. 여기부터 오른쪽만 프레임으로 본다.
LABEL_X = 190

#: 프레임을 가르는 최소 빈 열.
GAP = 8

#: 한 열에 이 픽셀 수보다 많아야 「그림이 있는 열」로 본다 (위 ⚠ 참조).
COL_INK = 10

#: 이보다 좁은 조각은 버린다 — 잡티 한 줄이 프레임으로 잡히는 것을 막는다.
MIN_W = 20

#: (이름, 행 y0, y1, 기대 프레임 수)
ROWS = [
    ("IdleHigh",  43,  252, 6),    # 체력 50% 이상 — 박동 강함
    ("IdleMid",  318,  523, 6),    # 체력 10~50%  — 박동 약화 · 균열
    ("IdleLow",  588,  795, 6),    # 체력 10% 이하 — 박동 불규칙 · 붕괴
    ("Destroy",  861, 1020, 8),    # 파괴
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
    return hashlib.md5(("LastSanctuary/" + rel).encode("utf-8")).hexdigest()


def to_rgba(rgb):
    """
    <b>테두리에서 이어진 배경색 덩어리만</b> 투명하게 한다 (맨 위 ★★ 참조).

    <c>scipy.ndimage.label</c> 로 연결 성분을 잡는다 — 손으로 BFS 를 돌리면 150만 픽셀에
    몇 초씩 걸린다.
    """
    from scipy import ndimage

    a = rgb.astype(np.int16)
    bg = np.array(BG_COLOR, dtype=np.int16)
    near = (np.abs(a - bg).max(axis=2) <= BG_TOL)

    lbl, n = ndimage.label(near)                 # 4-이웃 (기본값)
    if n == 0:
        return np.dstack([rgb.astype(np.uint8),
                          np.full(rgb.shape[:2], 255, np.uint8)])

    # 테두리에 닿은 덩어리 번호만 배경이다.
    edge = np.concatenate([lbl[0, :], lbl[-1, :], lbl[:, 0], lbl[:, -1]])
    outside = np.unique(edge[edge > 0])
    background = np.isin(lbl, outside)

    alpha = np.where(background, 0, 255).astype(np.uint8)

    # 가장자리 한 겹만 반투명으로 — 계단을 눕힌다.
    if EDGE_SOFT > 0:
        grown = ndimage.binary_dilation(background, iterations=EDGE_SOFT)
        alpha[np.logical_and(grown, ~background)] = 128

    return np.dstack([rgb.astype(np.uint8), alpha])


def trim(rgba):
    a = rgba[:, :, 3]
    ys, xs = np.nonzero(a > 8)
    if len(ys) == 0:
        return None
    return rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def write_png(img, folder, name):
    os.makedirs(folder, exist_ok=True)
    rel = os.path.relpath(folder, os.path.join(PROJECT, "Assets")).replace("\\", "/")
    path = os.path.join(folder, name + ".png")
    img.save(path)
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(META.format(guid=md5_guid(rel + "/" + name + ".png"), ppu=PPU))

    mp = folder.rstrip("\\/") + ".meta"
    if not os.path.exists(mp):
        with open(mp, "w", encoding="utf-8", newline="\n") as f:
            f.write(FOLDER_META.format(guid=md5_guid(rel)))


def main():
    if not os.path.exists(SRC):
        raise SystemExit("⚠ 원본이 없습니다: " + SRC)

    rgb = np.asarray(Image.open(SRC).convert("RGB"))
    rgba = to_rgba(rgb)
    w = rgb.shape[1]

    for name, y0, y1, expect in ROWS:
        band = rgba[y0:y1 + 1, :, :]
        ink = (band[:, :, 3] > 8).sum(axis=0)

        segs, inb, run, s = [], False, 0, 0
        for x in range(LABEL_X, w):
            if ink[x] > COL_INK:
                if not inb:
                    s, inb = x, True
                run = 0
            elif inb:
                run += 1
                if run >= GAP:
                    segs.append((s, x - run))
                    inb = False
        if inb:
            segs.append((s, w - 1))

        segs = [(a, b) for a, b in segs if b - a + 1 >= MIN_W]

        n = 0
        for a, b in segs:
            cell = trim(band[:, a:b + 1, :])
            if cell is None:
                continue
            write_png(Image.fromarray(cell, "RGBA"), os.path.join(DST_ROOT, name),
                      "Nexus_%s_%02d" % (name, n))
            n += 1

        flag = "" if n == expect else "  ⚠ 기대 %d" % expect
        print("  %-10s %2d프레임%s" % (name, n, flag))

    print()
    print("→", DST_ROOT)
    print("다음: python Tools/gen_nexus_skin.py")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    main()
