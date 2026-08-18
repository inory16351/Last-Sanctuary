# -*- coding: utf-8 -*-
"""성역(넥서스 주변) 타일셋 — 참조 시트에서 실제 타일을 뽑아낸다 (2026-08-18).

유저 지시: *"넥서스 주변도 중립 몬스터 청크 처럼 일정 범위의 청크 넣어서 생성(불규칙한
저그 점막처럼) · 청크 에셋은 볼트 리소스에 있어 · 청크 색이나 배열은 너가 알아서 적절하게
수정해줘 좀 확실하게 다른 공간이랑 분리되어서 보이게"*

원본
----
``<볼트>/리소스/asset/Tower_Asset/Nexus_tile.png`` (1503x1047).
유저가 `Docs/타일셋_참조/` 의 <b>참조 시트 형식 그대로</b> 이미지 생성 모델에 시켜 받은
「성역(중앙 건물 영역) 바닥 타일 세트 — SanctuaryGround」다.

★★ 이건 <b>타일 시트가 아니라 「참조 시트」다</b>
-----------------------------------------------
겉보기에 "160x80px · 20px 타일 8열 x 4행" 이라고 적혀 있지만, 실제 파일은
<b>라벨과 여백이 붙은 설명용 판</b>이다(내가 만들어 준 참조 시트가 그 형식이라
모델이 그 형식으로 그려 줬다). 그래서 <b>칸을 잘라내 20x20 으로 다시 굽는 단계</b>가 필요하다.

시트 배치 (실측):

  ① 바닥 20종   y 156..269 · y 319..432   x 24.. (10칸씩 두 줄)
  ② 데코 12종   y 538..631               (12칸 한 줄)
  ③ 경계 32종   y 738..818 · y 880..960   (16칸씩 두 줄)

세 묶음이 카르시노스 서식지의 <b>바닥 / 데코 / 가장자리</b> 와 정확히 대응한다
(`NeutralHabitat.Paint` 가 그 셋을 받는다). 그래서 <b>같은 폴더 규약</b>으로 굽는다:

  Resources/HabitatTiles/Sanctuary        ← 바닥 20
  Resources/HabitatTiles/SanctuaryEdge    ← 경계 32
  Resources/HabitatTiles/SanctuaryProps   ← 데코 12

★ 세 묶음의 <b>알파 처리가 서로 다르다</b>
-----------------------------------------
  바닥 : 완전 불투명. 아래 지형을 <b>덮는다</b>.
  데코 : 어두운 판 배경을 <b>투명하게</b> — 바닥 위에 겹쳐 그린다.
  경계 : 원화가 <b>한쪽으로 어두워지며 사라지는</b> 그림이다. 그 어두워짐을
         <b>알파</b>로 바꿔야 바깥 지형으로 스며든다 — 어둡게 둔 채로 깔면
         성역 둘레에 <b>검은 테두리</b>가 생긴다.

⚠ 칸마다 <b>안쪽으로 3px 파고들어</b> 자른다. 참조 시트가 칸마다 옅은 테두리 선을 그려서,
  그대로 자르면 게임에서 <b>타일마다 격자가 보인다</b>.

사용법:  python Tools/gen_sanctuary_tiles.py
"""

import hashlib
import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT

SRC = os.path.join(VAULT, "리소스", "asset", "Tower_Asset", "Nexus_tile.png")

ART_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "OrganicTilemap")
TILE_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Resources", "HabitatTiles")

#: 굽는 타일 한 변(px). 기존 바닥 타일과 같아야 한 칸에 딱 맞는다.
TILE = 20
PPU = 20

#: 칸 테두리 선을 피해 안쪽으로 파고들 픽셀 수 (위 ⚠).
INSET = 3

#: 참조 시트의 판 배경색 — 네 귀퉁이 실측.
PAGE_BG = (25, 25, 33)

#: 데코의 배경을 지울 때 쓰는 채널 차 허용치.
DECO_TOL = 26

# ── 시트 배치 (실측) ──────────────────────────────────────────────────────
#   (묶음, y0, y1, x 시작, x 끝, 칸 수)
BANDS = [
    ("Ground", 156, 269,   24, 1484, 10),
    ("Ground", 319, 432,   24, 1484, 10),
    ("Props",  538, 631,   24, 1470, 12),
    ("Edge",   738, 818,   24, 1484, 16),
    ("Edge",   880, 960,   24, 1484, 16),
]

# ---------------------------------------------------------------------------
# ★ 색 보정 — "확실하게 다른 공간이랑 분리되어서 보이게" (유저 지시)
#
# 원화 그대로면 맵 바닥(어두운 적자색 `#2B161E`~`#9A4744`)과 <b>같은 계열</b>이라
# 멀리서 보면 그냥 조금 밝은 바닥으로 읽힌다. 성역은 <b>한눈에 다른 구역</b>이어야 한다.
#
# 무엇을 얼마나 바꿨나:
#   · 채도 +35%  — 자홍/보랏빛 쪽으로 확실히 기운다(맵은 붉은 갈색이다)
#   · 명도 +12%  — 주변보다 밝아 「빛나는 구역」으로 읽힌다
#   ⚠ 더 올리면 그 위에 선 유닛이 묻힌다 — 카르시노스 서식지에서 실제로 겪은 실수다
#     (88-5절: 형광 분홍 빗금으로 유닛이 안 보였다).
# ---------------------------------------------------------------------------
SATURATION = 1.35
BRIGHTNESS = 1.12

#: 경계 타일을 알파로 바꿀 때의 기준. 판 배경과의 거리가 LO 이하면 완전 투명,
#: HI 이상이면 완전 불투명. 그 사이는 선형이다.
EDGE_ALPHA_LO = 18
EDGE_ALPHA_HI = 72

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
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
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

TILE_ASSET = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 13312, guid: 0000000000000000e000000000000000, type: 0}}
  m_Name: {name}
  m_EditorClassIdentifier: UnityEngine.dll::UnityEngine.Tilemaps.Tile
  m_Sprite: {{fileID: 21300000, guid: {sprite_guid}, type: 3}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_Transform:
    e00: 1
    e01: 0
    e02: 0
    e03: 0
    e10: 0
    e11: 1
    e12: 0
    e13: 0
    e20: 0
    e21: 0
    e22: 1
    e23: 0
    e30: 0
    e31: 0
    e32: 0
    e33: 1
  m_InstancedGameObject: {{fileID: 0}}
  m_Flags: 1
  m_ColliderType: 0
"""

ASSET_META = ("fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n"
              "  externalObjects: {{}}\n  mainObjectFileID: 11400000\n"
              "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

FOLDER_META = ("fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\n"
               "DefaultImporter:\n  externalObjects: {{}}\n  userData: \n"
               "  assetBundleName: \n  assetBundleVariant: \n")


def rel_of(path):
    return os.path.relpath(path, os.path.join(PROJECT, "Assets")).replace("\\", "/")


def guid_for(rel):
    return hashlib.md5(("LastSanctuary/" + rel).encode("utf-8")).hexdigest()


def ensure_folder_meta(path):
    mp = path.rstrip("\\/") + ".meta"
    if not os.path.exists(mp):
        os.makedirs(path, exist_ok=True)
        with open(mp, "w", encoding="utf-8", newline="\n") as f:
            f.write(FOLDER_META.format(guid=guid_for(rel_of(path))))


def write_tile(rgba, art_dir, tile_dir, name):
    os.makedirs(art_dir, exist_ok=True)
    os.makedirs(tile_dir, exist_ok=True)

    png = os.path.join(art_dir, name + ".png")
    Image.fromarray(rgba, "RGBA").save(png)

    g = guid_for(rel_of(png))
    with open(png + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(META.format(guid=g, ppu=PPU, sprite_id=g[:32]))

    asset = os.path.join(tile_dir, name + ".asset")
    with open(asset, "w", encoding="utf-8", newline="\n") as f:
        f.write(TILE_ASSET.format(name=name, sprite_guid=g))
    with open(asset + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(ASSET_META.format(guid=guid_for(rel_of(asset))))


# ---------------------------------------------------------------------------

def punch(rgb):
    """채도·명도를 올려 <b>주변 지형과 확실히 갈라지게</b> 한다 (위 SATURATION 주석)."""
    a = rgb.astype(np.float32)
    grey = a.mean(axis=2, keepdims=True)
    a = grey + (a - grey) * SATURATION       # 채도
    a *= BRIGHTNESS                          # 명도
    return np.clip(a, 0, 255).astype(np.uint8)


def cut(img, band, i):
    """밴드의 i 번째 칸을 잘라 <b>TILE x TILE</b> 로 굽는다 (테두리 INSET 만큼 제외)."""
    _, y0, y1, x0, x1, n = band
    w = (x1 - x0) / float(n)
    left = int(round(x0 + w * i)) + INSET
    right = int(round(x0 + w * (i + 1))) - INSET
    box = (left, y0 + INSET, right, y1 - INSET + 1)
    return img.crop(box).resize((TILE, TILE), Image.LANCZOS)


def opaque(cell):
    """바닥 — 완전 불투명."""
    rgb = punch(np.asarray(cell.convert("RGB")))
    return np.dstack([rgb, np.full(rgb.shape[:2], 255, np.uint8)])


def keyed(cell, tol):
    """데코 — 판 배경색에 가까운 곳을 투명하게. 배경이 사방을 둘러싸고 있어 단순 키잉으로 충분하다."""
    rgb = np.asarray(cell.convert("RGB")).astype(np.int16)
    dist = np.abs(rgb - np.array(PAGE_BG, np.int16)).max(axis=2)
    alpha = np.where(dist <= tol, 0, 255).astype(np.uint8)
    return np.dstack([punch(rgb.astype(np.uint8)), alpha])


def faded(cell):
    """
    경계 — <b>판 배경으로 어두워지는 만큼</b>을 알파로 바꾼다.

    이 그림들은 한쪽이 성역 바닥이고 반대쪽이 판 배경으로 사그라든다. 그 사그라듦을
    그대로 색으로 두면 성역 둘레에 <b>검은 테두리</b>가 생긴다 — 알파로 옮겨야
    바깥 지형이 비쳐 보이며 스며든다.
    """
    rgb = np.asarray(cell.convert("RGB")).astype(np.int16)
    dist = np.abs(rgb - np.array(PAGE_BG, np.int16)).max(axis=2).astype(np.float32)
    t = (dist - EDGE_ALPHA_LO) / float(EDGE_ALPHA_HI - EDGE_ALPHA_LO)
    alpha = np.clip(t, 0.0, 1.0) * 255.0
    return np.dstack([punch(rgb.astype(np.uint8)), alpha.astype(np.uint8)])


def main():
    if not os.path.isfile(SRC):
        print("⚠ 원본 시트가 없습니다:", SRC)
        return 1

    img = Image.open(SRC).convert("RGB")

    ensure_folder_meta(TILE_ROOT)
    counts = {"Ground": 0, "Edge": 0, "Props": 0}

    for band in BANDS:
        kind, _, _, _, _, n = band
        art = os.path.join(ART_ROOT, "Sanctuary" + ("" if kind == "Ground" else kind))
        tile_dir = os.path.join(TILE_ROOT, "Sanctuary" + ("" if kind == "Ground" else kind))

        for i in range(n):
            cell = cut(img, band, i)
            if kind == "Ground":
                rgba = opaque(cell)
            elif kind == "Props":
                rgba = keyed(cell, DECO_TOL)
            else:
                rgba = faded(cell)

            name = "Sanctuary%s_%02d" % ("" if kind == "Ground" else kind, counts[kind])
            write_tile(rgba, art, tile_dir, name)
            counts[kind] += 1

        ensure_folder_meta(art)
        ensure_folder_meta(tile_dir)

    print("  바닥      %2d종  → Resources/HabitatTiles/Sanctuary" % counts["Ground"])
    print("  가장자리  %2d종  → Resources/HabitatTiles/SanctuaryEdge" % counts["Edge"])
    print("  데코      %2d종  → Resources/HabitatTiles/SanctuaryProps" % counts["Props"])
    print("\n원화 → Art/OrganicTilemap/Sanctuary*")
    print("Unity 에서 Assets/Refresh 를 실행할 것.")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
