# -*- coding: utf-8 -*-
"""카르시노스 서식지 타일 — 바닥 · 가장자리 · 데코 (2026-08-15 신설 / 2026-08-16 확장).

유저 지시(2026-08-15): *"현재까지 나와 있는 리소스 이용해서 카르시노스 서식지 타일 에셋 만들어서 넣고"*
유저 지시(2026-08-16): *"서식지의 색만 바꾸지 말고 데코도 추가하거나 더 만들어서 더 색다르게 해줘"*

무엇을 만드나 — <b>세 묶음</b>
------------------------------
  ① ``HabitatTiles/CarcinosHabitat/``       바닥 32종   서식지 안쪽 바닥
  ② ``HabitatTiles/CarcinosHabitatEdge/``   가장자리 16종  서식지 <b>테두리 한 칸</b>
  ③ ``HabitatTiles/CarcinosHabitatProps/``  데코 28종   바닥 위에 <b>드문드문</b> 얹는다

원화는 ``Art/OrganicTilemap/CarcinosHabitat*/`` 에 낱장 PNG 로 나간다.

★ <b>기존 리소스에서 파생시킨다</b> — 새로 그리지 않는다
------------------------------------------------------
바닥·데코 모두 이미 쓰고 있는 타일셋의 <b>픽셀 결을 그대로 두고 색만</b> 갈아끼운다:

  바닥 ← ``OrganicTerrain_20px.png``   (20x20 x 32장)
  데코 ← ``OrganicProps_20px.png``     (20x20 x 32장 — 뼈·촉수·종양구·말뚝 등, 배경 투명)

명도 → 색 램프는 ``Carcinos_asset_02.png`` 몸통 픽셀의 명도 십분위 평균색에서 출발했다
(0~10% = (7,3,6) · 90~100% = (200,177,183)). ⚠ 그대로 쓰면 <b>바닥이 거의 검게</b> 나와
유닛이 안 보인다 — 색조는 유지하고 명도만 끌어올린 것이 아래 ``RAMP`` 다.

★ <b>2026-08-16 에 더한 것</b> — "색만 바꾸지 말라"에 대한 답
------------------------------------------------------------
  · <b>혈관 타일</b>  바닥 32종 중 8종에 <b>밝은 마젠타 혈관</b>이 갈라져 흐른다.
    난수 걷기로 그려서 타일마다 무늬가 다르고, 결절보다 훨씬 크게 눈에 띈다.
  · <b>가장자리 세트</b>  테두리 칸을 <b>어둡게 + 바깥쪽이 성글게</b> 만들어 서식지가
    맵에 <b>뚝 끊기지 않고 스며들게</b> 한다.
  · <b>데코 프롭</b>  기존 프롭을 카르시노스 색으로 옮긴 16종 + <b>새로 그린 12종</b>
    (종양 군집 · 촉수 · 포자). 새로 그린 것들은 원본 타일셋에 없던 형태라
    "색만 바꿨다"는 인상을 지우는 부분이다.

★ <b>낱장 PNG 로 만드는 이유</b> — 시트(spriteMode 2)로 만들면 스프라이트마다
  ``internalID`` 를 .meta 에 손으로 박아야 하고, 그 값이 Tile 에셋의 ``m_Sprite.fileID`` 와
  정확히 맞아야 한다. 낱장(spriteMode 1)이면 fileID 가 <b>항상 21300000</b> 이라
  참조가 단순해지고 다시 돌려도 안 깨진다.

★ 결과가 항상 같다(멱등) — 색·결절·혈관 위치를 <b>타일 번호로 시드한 난수</b>로 뽑는다.
  게임마다 달라지는 것은 타일 자체가 아니라 <b>어느 칸에 어느 타일을 까는지</b>이고,
  그건 런타임(`NeutralHabitat`)이 정한다.

사용법:  py -3 Tools/gen_carcinos_habitat_tiles.py
"""

import hashlib
import math
import os
import random
import sys

import numpy as np
from PIL import Image

from vault_path import PROJECT

TILESET = os.path.join(PROJECT, "Assets", "_Project", "Art", "OrganicTilemap", "OrganicTilemap")
SRC_TERRAIN = os.path.join(TILESET, "OrganicTerrain_20px.png")
SRC_PROPS = os.path.join(TILESET, "OrganicProps_20px.png")

ART_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "OrganicTilemap")
TILE_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Resources", "HabitatTiles")

TILE = 20          # 타일 한 변(px). 원본 시트가 20px 격자다
PPU = 20           # 기존 바닥 타일과 같아야 한 칸에 딱 맞는다

#: 명도(0~255) → 색. 카르시노스 원화의 색조를 유지하되 바닥으로 쓸 만큼 밝기를 올린 값.
RAMP = [
    (0,   22,  10,  24),
    (48,  40,  17,  40),
    (96,  64,  27,  58),
    (160, 100, 43,  84),
    (208, 148, 62,  116),
    (255, 196, 92,  152),
]

#: 가장자리 타일은 같은 램프를 <b>어둡게</b> 눌러 쓴다(맵 바닥 쪽으로 잦아들게).
EDGE_DIM = 0.62

#: ★ 데코 프롭은 같은 램프를 <b>밝게</b> 올려 쓴다.
#: 바닥이 워낙 어두워서(램프가 22~196) 프롭까지 같은 밝기로 칠하면 <b>바닥에 묻혀 안 보인다</b>
#: (실측 — 첫 생성물이 그랬다). 프롭은 바닥 <b>위에</b> 얹는 물건이라 대비가 있어야 한다.
PROP_GAIN = 1.45

#: 종양 결절 — (바깥색, 안쪽색). 원화의 발광 종양에서 뽑았다.
NODULE_RIM = (168, 44, 110)
NODULE_CORE = (238, 132, 194)

#: 혈관 — 결절보다 밝고 가늘다.
#: ⚠ 처음엔 (255,156,214)/(196,60,140) 로 뽑았는데 <b>바닥 전체가 형광 분홍 빗금</b>처럼
#:   보여 유닛이 묻혔다(깔아놓고 확인). 피부 <b>아래</b> 비치는 느낌이 되도록 한 단 낮췄다.
VEIN_CORE = (222, 104, 170)
VEIN_EDGE = (150, 46, 106)

NODULE_COUNT = (0, 4)
NODULE_RADIUS = (1, 2)

#: 바닥 32종 중 몇 종에 혈관을 그릴지 (뒤에서부터).
#: ⚠ 런타임이 32종에서 <b>균등하게</b> 뽑으므로 이 수가 곧 화면 비율이다 — 8 로 두면
#:   네 칸에 한 칸꼴로 혈관이 지나가 너무 시끄러웠다. 5 면 여섯 칸에 한 칸쯤이다.
VEIN_TILES = 5

#: 가장자리 타일 수 · 데코 타일 수
EDGE_TILES = 16
PROP_RECOLOR = 16      # 기존 프롭을 색만 바꾼 것

#: ★★ <b>0 이다</b> — 코드로 그린 데코 12종을 <b>뺐다</b> (2026-08-24 유저 지시:
#:   *"데코에 자꾸 스킬 이펙트가 박혀 있는데 데코 타일 뜯어보고 캐릭터 스킬 이펙트들
#:   섞여 있으면 삭제좀"*).
#:
#:   <see cref="draw_prop"/> 의 의도는 «종양 군집 · 촉수 · 포자» 였지만, 20px 에 밝은
#:   자홍으로 찍히면 화면에서는 <b>투사체 궤적 · 폭발 · 반짝임</b>으로 보인다 — 바닥에
#:   스킬 이펙트가 박힌 것처럼 읽힌다. 색만 바꾼 16종(원본 타일셋과 결이 이어지는 것)은
#:   그대로 두고 이 열두 장만 없앴다.
#:
#:   ⚠ 이 값을 12 로 되돌리면 <b>그 열두 장이 되살아난다</b>. 되살릴 일이 있으면
#:     «왜 이번엔 괜찮은지»(색을 낮췄는지 · 형태를 바꿨는지)를 먼저 적을 것.
#:   ★ <see cref="draw_prop"/> 함수 자체는 남겨 둔다 — 되돌릴 때 다시 쓰기 위해서다.
PROP_DRAWN = 0

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

ASSET_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META = ("fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\n"
               "DefaultImporter:\n  externalObjects: {{}}\n  userData: \n"
               "  assetBundleName: \n  assetBundleVariant: \n")


# ── 공용 ──────────────────────────────────────────────────────────────────

def guid_for(rel):
    return hashlib.md5(("LastSanctuary/" + rel).encode("utf-8")).hexdigest()


def rel_of(path):
    return os.path.relpath(path, os.path.join(PROJECT, "Assets", "_Project")).replace("\\", "/")


def ensure_folder_meta(path):
    mp = path.rstrip("\\/") + ".meta"
    if not os.path.exists(mp):
        with open(mp, "w", encoding="utf-8", newline="\n") as f:
            f.write(FOLDER_META.format(guid=guid_for(rel_of(path))))


def ramp_lut(dim=1.0):
    """명도 0~255 → RGB 룩업 테이블 (256, 3). ``dim`` 으로 전체를 어둡게 누를 수 있다."""
    lut = np.zeros((256, 3), dtype=np.float64)
    for i in range(len(RAMP) - 1):
        l0, r0, g0, b0 = RAMP[i]
        l1, r1, g1, b1 = RAMP[i + 1]
        for v in range(l0, l1 + 1):
            t = 0.0 if l1 == l0 else (v - l0) / float(l1 - l0)
            lut[v] = (r0 + (r1 - r0) * t, g0 + (g1 - g0) * t, b0 + (b1 - b0) * t)
    return (lut * dim).clip(0, 255).astype(np.uint8)


def slice_sheet(path):
    """시트를 20x20 낱장으로 자른다."""
    sheet = Image.open(path).convert("RGBA")
    sw, sh = sheet.size
    cols, rows = sw // TILE, sh // TILE
    return [np.asarray(sheet.crop((x * TILE, y * TILE, (x + 1) * TILE, (y + 1) * TILE))).astype(np.uint8)
            for y in range(rows) for x in range(cols)]


def recolor(tile, lut):
    """명도 → 램프. 알파는 그대로 둔다(프롭의 투명 배경이 유지된다)."""
    rgb = tile[:, :, :3].astype(np.float64)
    lum = (rgb @ np.array([0.299, 0.587, 0.114])).clip(0, 255).astype(np.uint8)
    return np.dstack([lut[lum], tile[:, :, 3]]).astype(np.uint8)


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


# ── 무늬 그리기 ───────────────────────────────────────────────────────────

def add_nodules(rgb, rng):
    """종양 결절 몇 개를 얹는다. 가장자리에는 안 놓는다(칸끼리 이어질 때 튀어 보인다)."""
    h, w, _ = rgb.shape
    for _ in range(rng.randint(*NODULE_COUNT)):
        r = rng.randint(*NODULE_RADIUS)
        cx, cy = rng.randint(r + 1, w - r - 2), rng.randint(r + 1, h - r - 2)
        for y in range(cy - r, cy + r + 1):
            for x in range(cx - r, cx + r + 1):
                d = (x - cx) ** 2 + (y - cy) ** 2
                if d > r * r:
                    continue
                rgb[y, x] = NODULE_CORE if d <= max(0, (r - 1) ** 2) else NODULE_RIM
    return rgb


def add_veins(rgb, rng):
    """
    <b>혈관</b> — 타일을 가로지르는 밝은 줄기 (2026-08-16 신설).

    난수 걷기로 그린다: 한 변에서 출발해 반대쪽으로 가되 매 걸음 방향을 조금씩 흔든다.
    ★ <b>타일 경계에서 시작하고 끝나게</b> 해서, 칸을 이어 깔았을 때 혈관이
    <b>다음 칸으로 이어져 보이게</b> 한다(같은 무늬가 아니어도 눈은 이어진 것으로 읽는다).
    """
    h, w, _ = rgb.shape
    # 한 타일에 한 줄기만. 두 줄기를 넣어봤더니 바닥이 시끄러워 유닛이 묻혔다.
    for _ in range(1):
        horizontal = rng.random() < 0.5
        pos = rng.uniform(3, (h if horizontal else w) - 4)
        drift = rng.uniform(-0.55, 0.55)

        steps = w if horizontal else h
        for i in range(steps):
            pos += drift + rng.uniform(-0.35, 0.35)
            drift = max(-0.7, min(0.7, drift + rng.uniform(-0.12, 0.12)))
            p = int(round(pos))
            if p < 1 or p > (h - 2 if horizontal else w - 2):
                break

            if horizontal:
                rgb[p, i] = VEIN_CORE
                rgb[p - 1, i] = VEIN_EDGE
                rgb[p + 1, i] = VEIN_EDGE
            else:
                rgb[i, p] = VEIN_CORE
                rgb[i, p - 1] = VEIN_EDGE
                rgb[i, p + 1] = VEIN_EDGE
    return rgb


def fade_outward(rgba, rng):
    """
    가장자리 타일 — <b>바깥쪽이 성글어지게</b> 알파를 깎는다.

    어느 쪽이 바깥인지는 런타임이 모르므로, <b>네 방향 각각</b>에 대한 변형을 만들지 않고
    <b>가장자리 전체</b>를 얼룩덜룩하게 비운다. 서식지 테두리를 따라 이 타일들이 깔리면
    경계가 톱니처럼 흩어져 <b>맵 바닥으로 잦아드는</b> 인상이 된다.
    """
    h, w, _ = rgba.shape
    alpha = rgba[:, :, 3].astype(np.float64)

    for y in range(h):
        for x in range(w):
            # 타일 중심에서 멀수록 비울 확률이 높다(체비셰프 거리 — 사각 타일에 맞는다).
            d = max(abs(x - (w - 1) / 2), abs(y - (h - 1) / 2)) / ((w - 1) / 2)
            if d < 0.45:
                continue
            if rng.random() < (d - 0.45) * 1.25:
                alpha[y, x] = 0

    rgba[:, :, 3] = alpha.astype(np.uint8)
    return rgba


def draw_prop(index, rng):
    """
    <b>새로 그린</b> 데코 프롭 한 장 (2026-08-16). 배경은 투명.

    원본 타일셋에 없던 형태 세 가지를 번갈아 만든다 — "색만 바꿨다"는 인상을 지우는 부분이다:
      0) <b>종양 군집</b>  크고 작은 구슬이 뭉쳐 있다
      1) <b>촉수</b>       바닥에서 솟아 휘는 가는 줄기
      2) <b>포자</b>       흩뿌려진 점 무리
    """
    img = np.zeros((TILE, TILE, 4), dtype=np.uint8)
    kind = index % 3

    def put(x, y, color, a=255):
        if 0 <= x < TILE and 0 <= y < TILE:
            img[y, x, :3] = color
            img[y, x, 3] = a

    def blob(cx, cy, r, core, rim):
        for y in range(cy - r, cy + r + 1):
            for x in range(cx - r, cx + r + 1):
                d = (x - cx) ** 2 + (y - cy) ** 2
                if d > r * r:
                    continue
                put(x, y, core if d <= max(0, (r - 1) ** 2) else rim)

    if kind == 0:                                   # 종양 군집
        for _ in range(rng.randint(3, 5)):
            blob(rng.randint(5, 14), rng.randint(5, 14), rng.randint(2, 4),
                 NODULE_CORE, NODULE_RIM)

    elif kind == 1:                                 # 촉수
        x = rng.randint(7, 12)
        y = TILE - 2
        drift = rng.uniform(-0.6, 0.6)
        for _ in range(rng.randint(9, 14)):
            put(int(x), y, VEIN_EDGE)
            put(int(x) + 1, y, VEIN_CORE)
            x += drift
            drift += rng.uniform(-0.3, 0.3)
            y -= 1
            if y < 2:
                break
        blob(int(x), max(2, y), 2, NODULE_CORE, NODULE_RIM)     # 끝에 종양 한 알

    else:                                           # 포자
        for _ in range(rng.randint(6, 12)):
            put(rng.randint(2, 17), rng.randint(2, 17),
                VEIN_CORE if rng.random() < 0.4 else NODULE_RIM)

    return img


# ── 묶음별 생성 ───────────────────────────────────────────────────────────

def build_ground(tiles, lut):
    art = os.path.join(ART_ROOT, "CarcinosHabitat")
    tile_dir = os.path.join(TILE_ROOT, "CarcinosHabitat")
    made = 0

    for idx, tile in enumerate(tiles):
        rgb = recolor(tile, lut)[:, :, :3].copy()
        rng = random.Random(1004 * 1000 + idx)

        rgb = add_nodules(rgb, rng)
        # 뒤쪽 몇 종에만 혈관을 넣는다 — 전부 넣으면 바닥이 시끄러워진다.
        if idx >= len(tiles) - VEIN_TILES:
            rgb = add_veins(rgb, rng)

        write_tile(np.dstack([rgb, tile[:, :, 3]]).astype(np.uint8),
                   art, tile_dir, f"CarcinosHabitat_20px_{idx:02d}")
        made += 1

    ensure_folder_meta(art)
    ensure_folder_meta(tile_dir)
    print(f"  바닥      {made}종 (혈관 {VEIN_TILES}종 포함)")
    return made


def build_edge(tiles, lut_dim):
    art = os.path.join(ART_ROOT, "CarcinosHabitatEdge")
    tile_dir = os.path.join(TILE_ROOT, "CarcinosHabitatEdge")
    made = 0

    for idx in range(EDGE_TILES):
        rng = random.Random(1004 * 2000 + idx)
        rgba = recolor(tiles[idx % len(tiles)], lut_dim).copy()
        rgba = fade_outward(rgba, rng)
        write_tile(rgba, art, tile_dir, f"CarcinosHabitatEdge_20px_{idx:02d}")
        made += 1

    ensure_folder_meta(art)
    ensure_folder_meta(tile_dir)
    print(f"  가장자리  {made}종 (어둡게 + 바깥이 성글게)")
    return made


def build_props(props, lut):
    art = os.path.join(ART_ROOT, "CarcinosHabitatProps")
    tile_dir = os.path.join(TILE_ROOT, "CarcinosHabitatProps")
    made = 0

    # ① 기존 프롭 색만 바꾼 것 — 원본 타일셋과 결이 이어진다.
    step = max(1, len(props) // PROP_RECOLOR)
    for i in range(PROP_RECOLOR):
        write_tile(recolor(props[(i * step) % len(props)], lut),
                   art, tile_dir, f"CarcinosHabitatProp_20px_{i:02d}")
        made += 1

    # ② 새로 그린 것 — 원본에 없던 형태.
    for i in range(PROP_DRAWN):
        write_tile(draw_prop(i, random.Random(1004 * 3000 + i)),
                   art, tile_dir, f"CarcinosHabitatProp_20px_{PROP_RECOLOR + i:02d}")
        made += 1

    ensure_folder_meta(art)
    ensure_folder_meta(tile_dir)
    print(f"  데코      {made}종 (색 변환 {PROP_RECOLOR} + 새로 그림 {PROP_DRAWN})")
    return made


def main():
    for p in (SRC_TERRAIN, SRC_PROPS):
        if not os.path.isfile(p):
            print("⚠ 원본 타일 시트가 없습니다:", p)
            return 1

    terrain = slice_sheet(SRC_TERRAIN)
    props = slice_sheet(SRC_PROPS)
    lut = ramp_lut()
    lut_dim = ramp_lut(EDGE_DIM)
    lut_prop = ramp_lut(PROP_GAIN)

    os.makedirs(TILE_ROOT, exist_ok=True)
    ensure_folder_meta(TILE_ROOT)

    total = build_ground(terrain, lut) + build_edge(terrain, lut_dim) + build_props(props, lut_prop)

    print(f"\n서식지 타일 {total}종 생성")
    print(f"  원화 → Art/OrganicTilemap/CarcinosHabitat*")
    print(f"  타일 → Resources/HabitatTiles/CarcinosHabitat*")
    print("Unity 에서 Assets/Refresh 를 실행할 것.")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
