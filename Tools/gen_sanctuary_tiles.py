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

#: ★ 그 뒤에 <b>어두운 테두리 줄만 골라</b> 더 깎는다 (trim_dark_border 참조).
#: 바깥 줄이 내부 평균의 이 비율보다 어두우면 격자선으로 보고 버린다.
BORDER_DARK_RATIO = 0.88

#: 한 방향에서 깎을 수 있는 최대 줄 수 — 그림 자체를 파먹지 않게 하는 안전장치.
BORDER_TRIM_MAX = 10

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
# ★★★ 색 재설계 3차 — <b>넥서스 건물 색에 맞춘다</b> (유저 지시 2026-08-18 3차)
#
# 2차에서 색조를 보라-청(265°)으로 옮겼다. 맵과는 확실히 갈렸지만 유저가 되돌려준 말이
# <i>"이거는 색 너무 다르자나 건물 색 확인해보고 해당 색과 컨셉이랑 맞춰서 다시 해봐
# 저렇게 푸르게 하면 안돼 건물 색이랑 맞춰야지"</i> 다. <b>맞는 지적이다</b> — 성역은
# 넥서스가 뻗어 나온 조직이므로, 넥서스와 <b>다른 색</b>이면 그게 성역이라는 뜻이 사라진다.
#
# ★ <b>넥서스 원화를 실측했다</b> (IdleHigh 6프레임 · 불투명 픽셀 142,819개):
#
#     밝기 구간              비중    rgb            H      S     V
#     가장 어두움(석재 그늘)  65.4%  ( 24,  9,  8)   3.1°  0.69  0.10
#     어두움                 19.8%  (102, 36, 34)   1.2°  0.66  0.40
#     중간                   10.5%  (161, 72, 68)   2.8°  0.58  0.63
#     밝음                    3.6%  (210,129,117)   7.5°  0.44  0.82
#     가장 밝음(발광)          0.7%  (241,202,188)  16.0°  0.22  0.95
#     ─────────────────────────────────────────────────────────────
#     전체                          ( 62, 26, 24)   2.8°  0.61  0.24
#
# 읽어낸 컨셉은 셋이다:
#   ① 색조는 <b>3° 부근의 크림슨</b>이고, <b>밝아질수록 오렌지-핑크로 열린다</b>(3° → 16°).
#   ② <b>어둡고 채도가 높다</b> — 전체 V 0.24 · S 0.61. 「검붉은 살덩이」다.
#   ③ 밝은 픽셀이 <b>1%도 안 되는데</b> 그 소수가 <b>심장의 발광</b>을 만든다.
#
# ★★ <b>그러면 맵 바닥과는 무엇으로 갈리나 — 색조가 아니라 「깊이」다</b>
# ------------------------------------------------------------------------
#     맵 바닥 : H 359°  S 0.49  V 0.37   ← 채도 낮고 중간 밝기 = <b>탁한 벽돌빛 갈색</b>
#     성역    : H   3°  S 0.72  V 0.28   ← 채도 높고 어두움 = <b>짙은 핏빛 살</b>
#
# 색조는 <b>4°밖에 안 떨어져 있고 그게 의도다</b>(건물과 같은 계열). 대신
#   · 채도를 <b>맵보다 크게</b>(0.49 → 0.72) 올려 「갈색」이 아니라 「피」로 읽히게 하고
#   · 밝기를 <b>맵보다 어둡게</b>(0.37 → 0.28) 내려 바닥이 아니라 <b>구덩이</b>처럼 보이게 하고
#   · 밝은 무늬만 <b>강하게 발광</b>시켜(V 0.85) 넥서스의 심장 빛이 바닥에 번진 것처럼 만든다.
# 2차가 "채도·명도만으로는 안 된다" 고 적었던 것은 <b>그때 올린 폭이 작았기 때문</b>이다
# (채도 +0.10 · 명도 +0.13 은 맵 자체의 밝기 편차 V 0.28~0.43 안에 묻힌다).
# 이번에는 <b>맵과 반대 방향</b>으로(더 어둡게) 벌리므로 그 편차와 겹치지 않는다.
#
# ⚠ 어둡게 만드는 것은 유닛 가시성에도 <b>더 안전하다</b> — 88-5절에서 유닛이 묻힌 이유는
#   서식지가 <b>밝아서</b>였다.
# ---------------------------------------------------------------------------

#: 원화의 기준 색조(실측 ≈335°). 이 값을 기준으로 색조를 옮긴다.
SRC_HUE = 335.0 / 360.0

#: ★ 목표 색조 — <b>넥서스 석재·살의 색조 3°</b>(실측). 건물과 같은 계열로 맞춘 값이다.
DST_HUE = 3.0 / 360.0

#: 원화가 가진 색조 편차를 이 배율로 <b>압축</b>해 목표 색조 주변에 모은다.
HUE_COMPRESS = 0.30

#: ★ 밝은 픽셀을 <b>오렌지-핑크로 여는</b> 폭(색조 회전량). 넥서스 원화가 밝아질수록
#: 3° → 16° 로 열리는 것을 그대로 따른다 — 이게 「불이 붙은 살」의 느낌을 만든다.
HUE_OPEN_AT_BRIGHT = 14.0 / 360.0

#: ★ 채도 — 배율 + <b>하한</b>. 하한 0.55 는 넥서스 어두운 구간의 S 0.66~0.69 를 겨눈 값이다.
#: 맵 바닥(0.49)보다 확실히 높아야 「갈색」이 아니라 「피」로 읽힌다.
SATURATION = 1.45
SATURATION_FLOOR = 0.55

#: ★ 밝기 — (V − PIVOT) × CONTRAST + PIVOT + LIFT.
#: LIFT 가 <b>음수</b>인 것이 이번 재설계의 핵심이다: 맵 바닥(0.37)보다 <b>어둡게</b> 내려
#: 성역이 「밝은 바닥」이 아니라 <b>파인 구덩이</b>로 보이게 한다.
VALUE_PIVOT = 0.42
VALUE_CONTRAST = 1.45
VALUE_LIFT = -0.18

#: ★ 발광 — 이 밝기를 넘는 픽셀만 강하게 밝힌다. 넥서스에서 밝은 픽셀은 4% 남짓뿐인데
#: 그 소수가 심장의 빛을 만든다. 문턱을 높게 두고 이득을 크게 주는 것이 그 구조다.
HIGHLIGHT_FROM = 0.52
HIGHLIGHT_GAIN = 0.46
HIGHLIGHT_DESAT = 0.26              # 빛나는 곳은 하얗게 뜬다 (넥서스 최고광 S 0.22)

#: ★ 가장자리 <b>테두리 발광</b> — 알파가 반쯤 열린 띠(성역의 경계선)를 밝힌다.
#: 어두운 성역에서는 이게 <b>유일한 경계 신호</b>라 2차(0.34)보다 훨씬 세게 준다 —
#: 색조가 맵과 4°밖에 안 떨어져 있어(건물에 맞춘 결과) 경계를 색으로 알릴 수 없다.
EDGE_RIM_FROM = 0.20                # 알파가 이 구간부터
EDGE_RIM_TO = 0.85                  #   ~ 이 구간까지가 테두리다
EDGE_RIM_GAIN = 0.55                # 그 띠의 밝기를 이만큼 올린다

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

def _rgb_to_hsv(rgb):
    """(H, S, V) 각각 0~1 실수 배열. <b>색조를 옮기려면 RGB 로는 안 되고 HSV 가 필요하다.</b>"""
    a = rgb.astype(np.float32) / 255.0
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx = a.max(axis=2)
    mn = a.min(axis=2)
    d = mx - mn
    safe = np.where(d > 1e-6, d, 1.0)

    hr = ((g - b) / safe) % 6.0
    hg = (b - r) / safe + 2.0
    hb = (r - g) / safe + 4.0
    h = np.where(mx == r, hr, np.where(mx == g, hg, hb)) / 6.0
    h = np.where(d > 1e-6, h % 1.0, 0.0)

    s = np.where(mx > 1e-6, d / np.where(mx > 1e-6, mx, 1.0), 0.0)
    return h, s, mx


def _hsv_to_rgb(h, s, v):
    """HSV(0~1) → uint8 RGB."""
    h6 = (h % 1.0) * 6.0
    i = np.floor(h6).astype(np.int32) % 6
    f = h6 - np.floor(h6)
    p = v * (1.0 - s)
    q = v * (1.0 - s * f)
    t = v * (1.0 - s * (1.0 - f))

    conds = [i == 0, i == 1, i == 2, i == 3, i == 4, i == 5]
    r = np.select(conds, [v, q, p, p, t, v])
    g = np.select(conds, [t, v, v, q, p, p])
    b = np.select(conds, [p, p, t, v, v, q])
    return np.clip(np.dstack([r, g, b]) * 255.0, 0, 255).astype(np.uint8)


def restyle(rgb):
    """
    ★★★ <b>성역을 「넥서스가 뻗어 나온 조직」으로 만드는 한 단계</b> (위 색 재설계 3차 주석).

    ① 색조를 <b>넥서스의 크림슨(3°)</b> 으로 옮기고 편차를 그 주변으로 압축한다
    ② 채도를 <b>맵보다 높게</b> 올린다 (탁한 갈색이 아니라 짙은 핏빛으로)
    ③ 밝기를 <b>맵보다 어둡게</b> 내리고 대비를 키운다 (바닥이 아니라 파인 구덩이로)
    ④ 밝은 무늬만 골라 <b>강하게 발광</b>시키고, 넥서스가 그러듯 <b>오렌지-핑크로 연다</b>

    ⚠ 색조는 <b>절대값이 아니라 기준색에서의 차이</b>로 옮긴다. 절대 색조를 그대로 쓰면
      원화의 자홍 픽셀과 붉은 픽셀이 <b>서로 다른 방향</b>으로 튀어 무늬가 깨진다.

    ⚠ ④의 <b>색조 열기</b>가 없으면 발광이 「하얗게 바랜 자리」로 보인다. 넥서스 원화는
      밝아질수록 3° → 16° 로 열리는데, 그 회전이 곧 <b>불이 붙은 살</b>의 인상이다.
    """
    h, s, v = _rgb_to_hsv(rgb)

    # ① 색조 — 넥서스의 크림슨으로.
    delta = (h - SRC_HUE + 0.5) % 1.0 - 0.5          # −0.5~0.5 로 감싼 차이
    h = (DST_HUE + delta * HUE_COMPRESS) % 1.0

    # ② 채도 — 맵 바닥(0.49)보다 확실히 높게.
    s = np.clip(np.maximum(s * SATURATION, SATURATION_FLOOR), 0.0, 1.0)

    # ③ 밝기 — 대비를 키우면서 <b>전체를 내린다</b>(VALUE_LIFT 가 음수다).
    v = np.clip((v - VALUE_PIVOT) * VALUE_CONTRAST + VALUE_PIVOT + VALUE_LIFT, 0.0, 1.0)

    # ④ 발광 — 문턱 위쪽만, 넘은 만큼에 비례해서. 색조를 오렌지-핑크로 열고 하얗게 띄운다.
    over = np.clip((v - HIGHLIGHT_FROM) / max(1e-6, 1.0 - HIGHLIGHT_FROM), 0.0, 1.0)
    v = np.clip(v + over * HIGHLIGHT_GAIN, 0.0, 1.0)
    h = (h + over * HUE_OPEN_AT_BRIGHT) % 1.0
    s = np.clip(s - over * HIGHLIGHT_DESAT, 0.0, 1.0)

    return _hsv_to_rgb(h, s, v)


def rim_glow(rgb, alpha01):
    """
    ★ 가장자리 타일의 <b>테두리를 밝힌다</b> (위 EDGE_RIM_* 주석).

    알파가 <b>반쯤 열린 띠</b>가 곧 성역의 경계선이다 — 거기만 밝히면 경계가
    <b>선</b>으로 읽힌다. 완전 불투명한 안쪽(성역 바닥)과 완전 투명한 바깥은 안 건드린다.
    """
    band = np.clip((alpha01 - EDGE_RIM_FROM) / max(1e-6, EDGE_RIM_TO - EDGE_RIM_FROM), 0.0, 1.0)
    band = band * (1.0 - band) * 4.0                 # 띠 가운데가 가장 밝은 종 모양

    h, s, v = _rgb_to_hsv(rgb)
    v = np.clip(v + band * EDGE_RIM_GAIN, 0.0, 1.0)
    s = np.clip(s - band * 0.12, 0.0, 1.0)
    return _hsv_to_rgb(h, s, v)


def trim_dark_border(cell):
    """
    ⚠⚠ <b>고정 INSET 만으로는 격자선이 안 지워진다</b> (2026-08-18 2차에 발견).

    굽고 나서 실측해 보니 <b>왼쪽 한 칸만</b> 어두웠다 (열 평균 59 vs 내부 82) —
    참조 시트가 칸 <b>왼쪽</b>에 구분선을 그려서, 칸 폭을 균등 분할로 추정하는 이 코드의
    반올림 오차가 그쪽으로만 쏠린다. 게임에서는 그 한 열이 <b>타일마다 세로 격자선</b>으로
    보인다(성역 미리보기에서 눈으로 확인했다).

    ★ INSET 을 3 → 7 로 키우면 가려지지만, 그건 <b>모든 방향에서</b> 그림을 버리는 방법이고
      시트가 갱신되면 또 안 맞는다. 대신 <b>어두운 테두리를 찾아서만</b> 깎는다 —
      바깥 줄의 밝기가 내부 평균의 <see cref="BORDER_DARK_RATIO"/> 배보다 어두우면 한 줄 버리고
      다시 본다. 네 방향 각각 최대 <see cref="BORDER_TRIM_MAX"/> 줄까지만 깎아
      그림 자체를 파먹지 않게 막는다.
    """
    a = np.asarray(cell.convert("RGB")).astype(np.float32).mean(axis=2)
    top, bottom, left, right = 0, a.shape[0], 0, a.shape[1]

    for _ in range(BORDER_TRIM_MAX * 4):
        inner = a[top + 2:bottom - 2, left + 2:right - 2]
        if inner.size == 0: break
        ref = inner.mean() * BORDER_DARK_RATIO

        # 가장 어두운 바깥 줄 하나를 고른다 — 한 번에 한 줄만 깎아야 멈출 자리를 지나치지 않는다.
        edges = [
            ("left",   a[top:bottom, left].mean(),      left - 0 < BORDER_TRIM_MAX),
            ("right",  a[top:bottom, right - 1].mean(), a.shape[1] - right < BORDER_TRIM_MAX),
            ("top",    a[top, left:right].mean(),       top < BORDER_TRIM_MAX),
            ("bottom", a[bottom - 1, left:right].mean(), a.shape[0] - bottom < BORDER_TRIM_MAX),
        ]
        worst = min((e for e in edges if e[2]), key=lambda e: e[1], default=None)
        if worst is None or worst[1] >= ref: break

        if worst[0] == "left":     left += 1
        elif worst[0] == "right":  right -= 1
        elif worst[0] == "top":    top += 1
        else:                      bottom -= 1

    return cell.crop((left, top, right, bottom))


def cut(img, band, i):
    """밴드의 i 번째 칸을 잘라 <b>TILE x TILE</b> 로 굽는다 (테두리 INSET + 어두운 줄 제외)."""
    _, y0, y1, x0, x1, n = band
    w = (x1 - x0) / float(n)
    left = int(round(x0 + w * i)) + INSET
    right = int(round(x0 + w * (i + 1))) - INSET
    box = (left, y0 + INSET, right, y1 - INSET + 1)
    return trim_dark_border(img.crop(box)).resize((TILE, TILE), Image.LANCZOS)


def opaque(cell):
    """바닥 — 완전 불투명."""
    rgb = restyle(np.asarray(cell.convert("RGB")))
    return np.dstack([rgb, np.full(rgb.shape[:2], 255, np.uint8)])


def keyed(cell, tol):
    """데코 — 판 배경색에 가까운 곳을 투명하게. 배경이 사방을 둘러싸고 있어 단순 키잉으로 충분하다."""
    rgb = np.asarray(cell.convert("RGB")).astype(np.int16)
    dist = np.abs(rgb - np.array(PAGE_BG, np.int16)).max(axis=2)
    alpha = np.where(dist <= tol, 0, 255).astype(np.uint8)
    return np.dstack([restyle(rgb.astype(np.uint8)), alpha])


def faded(cell):
    """
    경계 — <b>판 배경으로 어두워지는 만큼</b>을 알파로 바꾼다.

    이 그림들은 한쪽이 성역 바닥이고 반대쪽이 판 배경으로 사그라든다. 그 사그라듦을
    그대로 색으로 두면 성역 둘레에 <b>검은 테두리</b>가 생긴다 — 알파로 옮겨야
    바깥 지형이 비쳐 보이며 스며든다.
    """
    rgb = np.asarray(cell.convert("RGB")).astype(np.int16)
    dist = np.abs(rgb - np.array(PAGE_BG, np.int16)).max(axis=2).astype(np.float32)
    t = np.clip((dist - EDGE_ALPHA_LO) / float(EDGE_ALPHA_HI - EDGE_ALPHA_LO), 0.0, 1.0)

    # 색을 먼저 옮기고(restyle) 그 위에 <b>테두리 발광</b>을 얹는다 — 순서가 중요하다.
    # 발광을 먼저 얹으면 색조 압축이 그 밝기를 도로 평탄하게 만든다.
    styled = rim_glow(restyle(rgb.astype(np.uint8)), t)
    return np.dstack([styled, (t * 255.0).astype(np.uint8)])


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
