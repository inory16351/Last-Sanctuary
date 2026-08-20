# -*- coding: utf-8 -*-
"""모션 시트 → 프레임 PNG 분해 **공통 부품** (2026-08-19).

★ 왜 생겼나
-----------
캐릭터·몬스터마다 분해 스크립트를 한 벌씩 만들다 보니(`malphas_skin_build.py` ·
`laryngeal_skin_build.py` · `kasinoma_skin_build.py` …) **같은 규칙이 여섯 벌**로 갈렸다.
엘린 시트에서 알아낸 세 가지(배경을 이어짐으로 판정 · 그림자를 아래 띠에서만 지움 ·
갇힌 배경 되돌리기)는 **모든 시트에 해당하는 규칙**인데, 한 파일에만 있으면 다음 캐릭터에서
같은 함정을 처음부터 다시 밟는다. 그래서 여기 한 벌만 둔다.

캐릭터별 스크립트에 남는 것은 **그 시트의 실측 좌표와 판단**뿐이다
(`elin_skin_build.py` · `sigrid_skin_build.py`).

세 가지 함정 — 자세한 근거는 각 함수 주석에
--------------------------------------------
1. :func:`background_mask` — 배경은 «흰색과의 거리» 가 아니라 **테두리와 이어진 덩어리**다.
   흰 두건·은빛 갑주가 배경과 같은 색(252~255)일 수 있고, 거리로 재면 그 부분이 사라진다.
2. :func:`shadow_in_box` — 발밑 그림자는 **프레임 아래쪽 띠 안에서만** 지운다.
   은발이 그림자와 채도·광도가 겹쳐서, 색만 보고 흘려 채우면 머리카락을 타고 번진다.
3. :func:`enclosed_background` — 닫힌 선(휘두르는 원호)에 **갇힌 배경**은 되돌린다.
   단 「면적이 크고 먹선에 둘러싸인」 것만 — 빛나는 이펙트의 밝은 속은 그림이다.

⚠ scipy 는 쓰지 않는다 — 없는 PC 가 있다(113-7절). numpy 전파로 흘려 채운다.
"""

import hashlib
import os

import numpy as np
from PIL import Image, ImageFilter

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

PPU = 64

# ──────────────────────────────────────────────────────────────────────────
# ★ 스킨 에셋의 «값» 칸 — :data:`SKIN_SPEC_NAME` 파일로 원화 폴더에 함께 써 둔다.
#
# <b>왜 파일로 내보내나</b> (유저 지시 2026-08-19: *"하드코딩 하지 말고 스킨 에셋 만들어서
# mcp 로 직접 넣어줘"*) — 스킨 에셋은 파이썬이 YAML 을 손으로 엮는 것이 아니라
# **유니티가 직접** 만든다(`Assets/_Project/Scripts/Editor/CharacterSkinBuilder.cs` ·
# MCP `execute_menu_item` 으로 실행). 그러면 guid 를 코드가 들고 있을 필요가 없다.
#
# 그런데 「재생 속도」·「투사체를 쓰지 않는다」 같은 값은 **원화만 봐서는 알 수 없다.**
# 캐릭터마다 C# 에 적으면 그게 하드코딩이므로, <b>원화 폴더 옆에 데이터로</b> 둔다 —
# 이 스크립트가 쓰고 유니티 빌더가 읽는다. 캐릭터가 늘어도 C# 은 안 바뀐다.
# ──────────────────────────────────────────────────────────────────────────
SKIN_SPEC_NAME = "_skin_spec.txt"

#: 캐릭터별 값은 각 분해 스크립트가 자기 ``SKIN_SPEC`` 딕셔너리로 들고 있고,
#: :func:`write_skin_spec` 가 이 이름의 파일로 내보낸다.

#: ★ **배경으로 흘려갈 수 있는 최대 거리**(세 채널 차이의 합). 이 시트의 배경은 순백이
#:   아니라 **254,254,254** 이고 ±2 노이즈가 있다(합 ≤ 6). 30 = 채널당 10 정도까지 허용.
#:   ⚠ 올리면 회복 이펙트의 연한 후광을 타고 흐름이 안으로 들어간다.
BG_TOL = 30

#: 알파가 이 값 미만이면 «투명 = 배경» 으로 본다 (:func:`load_sheet` 의 ★★).
#: 8 인 이유는 :func:`crop_rgba` 가 이미 «알파 8 이하는 없는 픽셀» 로 다루기 때문이다 —
#: 두 곳이 다른 기준을 쓰면 경계 한 줄이 서로 어긋난다.
ALPHA_INK_MIN = 8

#: 배경 흘려 채우기의 씨앗을 뿌릴 **안쪽 띠**(px). 시트를 감싼 테두리(검은 액자)가 있으면
#: 맨 가장자리에는 배경이 없다 — :func:`background_mask` 의 ⚠⚠ 참조.
SEED_INSET = 6

#: 배경에 닿는 한 겹을 부드럽게 깎을 때의 상한 거리 — 이만큼 멀면 완전 불투명.
ALPHA_HI = 180

#: 그 한 겹의 최소 알파. 0 이면 경계 픽셀이 사라져 그림에 구멍이 난다.
RING_MIN_ALPHA = 60

# ──────────────────────────────────────────────────────────────────────────
# ★★ 「갇힌 배경」 되돌리기 (:func:`enclosed_background`)
#
# 이어짐으로 배경을 정하면 **닫힌 선 안에 갇힌 배경**은 배경으로 안 잡힌다. 근거리
# 5번 칸(사슬을 크게 휘두르는 원호)이 정확히 그 모양이라, 원호 **안쪽 78x80 = 3,323px
# 이 흰 원판**으로 남았다(실제로 그렇게 나왔다).
#
# 그런데 두건·눈가리개·수도복의 흰색도 「갇힌 배경색」이다 — 그건 남아야 한다.
# 실측으로 둘은 **두 값으로 깨끗하게 갈린다**:
#
#   | 갇힌 덩어리 | 면적 | 테두리 광도(하위 5%) | 정체 |
#   |---|---|---|---|
#   | 근거리 5번 원호 안 | 3,323 | **8** | 배경 (사슬·먹선에 둘러싸임) |
#   | 근거리 5번 원호 위 | 469 | **53** | 배경 |
#   | 회복 이펙트 십자 속 | 583·641 | **167~187** | **그림** (빛의 밝은 속) |
#   | 두건·눈가리개·수도복 | 전부 ≤ 200 | — | **그림** |
#
# → 「면적이 크고 **먹선에 둘러싸인**」 것만 배경으로 되돌린다. 회복 이펙트의 밝은
#   속은 테두리가 밝아서 걸리지 않고(그 흰색이 채워진 채로 나오는 게 맞다 — 눈으로 확인),
#   캐릭터 흰색은 면적이 작아서 걸리지 않는다.
# ──────────────────────────────────────────────────────────────────────────
POCKET_MIN_AREA = 300
POCKET_INK_RING_LUM = 120

#: 라벨(작은 회색 숫자) 판정 — **회색조로만** 찾는다(102-3절·113-1절과 같은 이유).
LABEL_LUM = 150
LABEL_SAT = 40

#: 라벨 덩어리를 가르는 최소 빈 열. 두 자리 숫자 안쪽 간격보다 크고 라벨 간격보다 작다.
LABEL_GAP = 12

#: 라벨 덩어리의 최대 폭(px). 실측으로 진짜 라벨은 8~14px 다.
LABEL_MAX_W = 22

#: 라벨 덩어리의 **최소** 폭(px) — 기본값은 **1(무동작)**.
#:
#: ★★ 2026-08-20 신설. 바리올라 시트에서 **소의 뿔이 프레임 번호 줄 높이까지 솟아**
#: 폭 3~4px 짜리 가짜 덩어리를 둘 만들었다(스킬 2 줄: 진짜 라벨 7개 + 뿔 2개 = 9개).
#: `LABEL_MAX_W` 의 **반대 방향** 함정이다 — 위쪽은 «이펙트가 라벨에 붙어 커진 덩어리»,
#: 아래쪽은 «그림 일부가 라벨 줄에 들어온 부스러기» 다.
#:
#: ⚠⚠ **기본값을 8 로 두면 안 된다.** 라벨 글자 크기가 시트마다 다르다 — 실측:
#:       바리올라  9~12px  (두 자리 숫자 · 큰 글씨)
#:       엘린      3~ 7px  (한 자리 숫자 · 작은 글씨) ← 8 로 자르면 **전멸**한다
#:   실제로 8 을 기본값으로 넣었다가 엘린 분해가 «프레임 번호 0개» 로 죽었다.
#:   그래서 **필요한 스크립트가 `min_w=` 로 직접 넘긴다** — 시트마다 다른 값을
#:   전역 기본값으로 정할 근거가 없다.
LABEL_MIN_W = 1

#: 칸을 가르는 빈 열의 최소 두께(px). 근거리 줄의 가장 좁은 빈 열이 4px 다.
CELL_GAP_MIN = 3

#: 몸통 중심을 잴 때 「얇은 줄기」로 보고 버리는 두께 기준 — 그 열의 세로 두께가
#: 가장 두꺼운 열의 이 비율 미만이면 사슬로 본다.
BODY_STREAK_RATIO = 0.35

#: 이펙트의 **밑동**으로 볼 아래쪽 비율 — 이만큼만 보고 가로 중심을 잡는다.
FX_BASE_RATIO = 0.22

# ──────────────────────────────────────────────────────────────────────────
# 발밑 드롭 섀도 판정 (맨 위 ★★ 두 번째).
#
# ⚠ 이 세 값은 **함께** 그림자를 가둔다 — 하나만 넓혀도 은발로 흐름이 새어 나간다.
#   · 띠(band)   : 프레임 아래쪽 몇 줄만 볼지        ← 머리·두건에 흐름이 닿지 못하게
#   · 채도 상한  : 회색인 것만                        ← 금장식·홍조를 지키려고
#   · 광도 대역  : 반투명 회색이 놓이는 구간만        ← 수도복 흰 밑단(250+)을 지키려고
# ──────────────────────────────────────────────────────────────────────────
SHADOW_BAND_PX = 14
SHADOW_SAT_MAX = 12
SHADOW_LUM_MIN = 110
SHADOW_LUM_MAX = 235

#: 그림자 흐름이 상자 좌우로 조금 더 볼 여백(px) — 타원이 발보다 넓다.
SHADOW_SIDE_MARGIN = 6

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
    filterMode: {filter}
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
    return hashlib.md5(("LastSanctuary/" + key).encode("utf-8")).hexdigest()


# ---------------------------------------------------------------------------
# 시트 읽기 · 그림자 지우기
# ---------------------------------------------------------------------------

def modal_background(arr):
    """가장 많이 나오는 색 = 배경. 이 시트는 순백(255)이 아니라 254 다."""
    flat = arr.reshape(-1, 3)[::17]
    colors, counts = np.unique(flat, axis=0, return_counts=True)
    return colors[counts.argmax()].astype(int)


def grow(seen, ok):
    """네 방향 전파 한 걸음. scipy 는 쓰지 않는다 — 없는 PC 가 있다(113-7절)."""
    g = seen.copy()
    g[1:, :] |= seen[:-1, :]
    g[:-1, :] |= seen[1:, :]
    g[:, 1:] |= seen[:, :-1]
    g[:, :-1] |= seen[:, 1:]
    g &= ok
    return g


def flood(ok, seed):
    seen = seed & ok
    while True:
        nxt = grow(seen, ok)
        if np.array_equal(nxt, seen):
            return seen
        seen = nxt


def background_mask(dist):
    """
    ★★ 배경 = **시트 테두리와 이어진** 「배경색에 가까운」 덩어리 (맨 위 ★★ 첫 번째).

    거리만 보면 두건·눈가리개의 흰색(252~255)이 배경과 같은 색이라 통째로 사라진다.
    이어짐을 따지면 그 흰색은 검은 외곽선에 갇혀 **바깥과 이어지지 않아** 살아남는다.
    """
    ok = dist <= BG_TOL
    seed = np.zeros(ok.shape, dtype=bool)

    # ⚠⚠ 맨 가장자리에만 씨앗을 뿌리면 **테두리가 있는 시트에서 배경이 0px 이 된다.**
    #   베일 시트가 그랬다 — 시트 전체를 감싼 <b>검은 액자</b>가 있어서 맨 끝 줄에
    #   배경색 픽셀이 하나도 없고, 흘려 채우기가 시작조차 못 해 **시트 전부가 그림**으로
    #   잡혔다(1,572,864px = 전 화소). 그래서 <b>안쪽 띠에서도</b> 뿌린다.
    #   (라린길 스크립트가 같은 문제에 ``SEED_INSET`` 을 쓴 것과 같은 처리다.)
    for i in (0, SEED_INSET):
        if i * 2 >= min(ok.shape):
            continue
        seed[i, :] = True
        seed[-i - 1, :] = True
        seed[:, i] = True
        seed[:, -i - 1] = True

    return flood(ok, seed)


def enclosed_background(sheet, y0, y1, cx0, cx1, min_area=None, ring_lum=None):
    """
    ★★ 칸 하나 안의 **갇힌 배경** (맨 위 ``POCKET_*`` 주석의 표).

    ★ 2026-08-20 — :paramref:`min_area` · :paramref:`ring_lum` 를 열었다(기본값은 그대로).

      골렘(아루의 소환수)은 <b>두 다리 사이</b>가 101~275px 로 갇히는데 기본
      :data:`POCKET_MIN_AREA` (300) 에 못 미쳐 흰 판때기로 남았다. 면적만 낮추면
      갑옷 속 흰 하이라이트까지 뚫릴 수 있으므로 **테두리 어둡기를 같이 조인다** —
      실측으로 다리 사이의 테두리 광도(하위 5%)는 <b>14~23</b> 이고, 갑옷 하이라이트는
      금빛·회색(80 이상)에 둘러싸여 있어 60 으로 자르면 깨끗이 갈린다.

    칸 단위로 도는 이유는 **속도**다 — 시트 전체에서 갇힌 덩어리를 세면 2,751개가 나와
    한 덩어리씩 흘려 채우는 데 몇 분이 걸린다. 칸은 100x90 남짓이라 즉시 끝난다.
    (배경 마스크 자체는 전역에서 구한 것을 그대로 쓴다 — 칸마다 다시 구하면 캐릭터가
    칸 위·아래 경계에 닿는 줄에서 판정이 갈린다.)
    """
    min_area = POCKET_MIN_AREA if min_area is None else min_area
    ring_lum = POCKET_INK_RING_LUM if ring_lum is None else ring_lum

    sl = (slice(y0, y1 + 1), slice(cx0, cx1 + 1))
    pockets = (sheet["dist"][sl] <= BG_TOL) & ~sheet["bg_mask"][sl]
    lum = sheet["lum"][sl]
    ones = np.ones_like(pockets)

    out = np.zeros(sheet["mask"].shape, dtype=bool)
    rest = pockets.copy()
    while rest.any():
        ys, xs = np.where(rest)
        seed = np.zeros(pockets.shape, dtype=bool)
        seed[ys[0], xs[0]] = True
        comp = flood(pockets, seed)
        rest &= ~comp

        if int(comp.sum()) < min_area:
            continue
        ring = grow(grow(comp, ones), ones) & ~comp
        if not ring.any():
            continue
        if np.percentile(lum[ring], 5) < ring_lum:
            out[sl] |= comp
    return out


def reflood_background(sheet, removed):
    """
    ★★ 그림자를 지운 뒤 **새로 바깥과 이어진 배경**을 배경으로 편입한다 (2026-08-20 신설).

    <b>왜 필요한가 — 네발 짐승의 「배 아래 흰 웅덩이」</b>
    ------------------------------------------------------
    :func:`background_mask` 는 배경을 «시트 테두리에서 흘려 닿는 곳» 으로 정의한다.
    그런데 바리올라(소)는 **발밑 그림자가 다리 사이를 막아** 배 아래의 배경이 바깥과
    끊긴다. 끊긴 배경은 배경으로 안 잡히므로 **불투명한 흰 덩어리**로 남는다
    (실측: 대기 한 프레임에 수백 px · 화면에서 배 밑에 흰 천이 붙은 것처럼 보인다).

    그림자를 지우면 그 벽이 없어지지만, :data:`bg_mask` 는 :func:`load_sheet` 에서
    **한 번 구해 둔 값**이라 저절로 갱신되지 않는다. 그래서 지운 뒤 다시 흘려 준다.

    ★ <b>:func:`enclosed_background` 로는 안 된다</b> — 그쪽은 「갇힌 덩어리」를 찾지만
      <b>테두리가 먹선일 때만</b> 되돌린다(:data:`POCKET_INK_RING_LUM`). 배 아래 웅덩이의
      벽 절반은 **연회색 그림자**라 그 조건에 안 걸린다. 두 함수는 노리는 것이 다르다:
      저쪽은 «그림 안의 흰 원판»(엘린 원호), 이쪽은 «그림자가 막은 바깥 배경».

    ⚠ <b>배경색 픽셀만</b> 편입한다(:data:`BG_TOL` 안) — 밝은 하이라이트는 그 대역
      밖이라 안전하다. 그림에 진짜 순백이 있고 그것이 바깥과 이어져 있으면 사라지므로,
      돌려주는 픽셀 수를 **반드시 확인할 것**(프레임 하나에 수백 px 이 정상, 수만이면
      배경 판정이 무너진 것이다).

    :param removed: 방금 그림에서 뺀 픽셀(그림자). 흘려 채우기의 **씨앗이자 통로**다.
    :returns: 새로 배경이 된 픽셀 수 (``removed`` 자신은 세지 않는다).
    """
    ok = (sheet["dist"] <= BG_TOL) | removed
    region = flood(ok, sheet["bg_mask"] | removed)

    gained = region & ~sheet["bg_mask"] & ~removed
    sheet["bg_mask"] |= region
    sheet["mask"] &= ~region
    return int(gained.sum())


def shadow_in_box(sheet, box):
    """
    ★★ 프레임 하나의 **발밑 그림자** 픽셀 (맨 위 ★★ 두 번째).

    상자의 **아래 :data:`SHADOW_BAND_PX` 줄** 안에서만 배경에서 흘려 채운다.
    띠 밖으로 나갈 수 없으니 은발·두건까지 갈 길이 없다 — 첫 시도에서 시트 전체
    38,917px 을 먹었던 그 사고를 기하로 막는다.
    """
    bx0, bx1, by0, by1 = box
    h, w = sheet["dist"].shape
    zy0 = max(by0, by1 - SHADOW_BAND_PX + 1)
    zx0 = max(0, bx0 - SHADOW_SIDE_MARGIN)
    zx1 = min(w - 1, bx1 + SHADOW_SIDE_MARGIN)

    zone = np.zeros((h, w), dtype=bool)
    zone[zy0:by1 + 1, zx0:zx1 + 1] = True

    gray = ((sheet["sat"] <= SHADOW_SAT_MAX) &
            (sheet["lum"] >= SHADOW_LUM_MIN) &
            (sheet["lum"] <= SHADOW_LUM_MAX))
    ok = zone & (gray | sheet["bg_mask"])
    return flood(ok, zone & sheet["bg_mask"]) & ~sheet["bg_mask"]


#: 구획 사각 테두리 판정 (:func:`erase_box_borders`) — 연회색 균일선.
BORDER_SAT_MAX = 12
BORDER_LUM_MIN = 150
BORDER_LUM_MAX = 245

#: 「긴 직선」으로 인정할 최소 길이 — 그 줄/열 길이에 대한 비율.
BORDER_ROW_RATIO = 0.30
BORDER_COL_RATIO = 0.20

#: 검은 액자 판정 — 어두운 쪽은 그림과 헷갈리므로 훨씬 엄격하게 본다(위 ⚠⚠).
BORDER_DARK_LUM_MAX = 90
BORDER_DARK_RATIO = 0.90


def erase_box_borders(arr, bg):
    """
    ★★ 구획을 감싼 **연회색 사각 테두리**를 배경색으로 칠한다.

    <b>왜 배경 판정보다 먼저 해야 하나</b> — :func:`background_mask` 는 「시트 테두리와
    이어진 것」만 배경으로 본다. 구획 상자가 있으면 **상자 안쪽이 통째로 갇혀서**
    배경으로 안 잡힌다. 시그리드 시트에서 실제로 그렇게 됐다:
    잉크가 **1,111,161 px**(시트의 72%)로 잡히고 프레임 경계 상자가 구획 전체가 됐다.
    (엘린 시트에는 상자가 없어서 이 문제가 없었다.)

    ⚠ 색만 보면 **은발**(채도 5~10 · 광도 200~235)도 걸린다. 그래서 **긴 직선인 것만**
      지운다 — 그 줄/열에서 테두리색 픽셀이 폭·높이의 :data:`BORDER_ROW_RATIO` /
      :data:`BORDER_COL_RATIO` 를 넘는 경우다. 머리카락은 그렇게 길게 이어지지 않는다.

    ⚠ 지우는 것은 **그 줄/열의 테두리색 픽셀만**이다 — 줄 전체를 칠하면 상자를 지나가는
      그림(지팡이·불길)에 한 픽셀 두께의 구멍이 난다.
    """
    a = arr.astype(np.int16)
    sat = a.max(axis=2) - a.min(axis=2)
    lum = a.mean(axis=2)

    light = ((sat <= BORDER_SAT_MAX) &
             (lum >= BORDER_LUM_MIN) & (lum <= BORDER_LUM_MAX))

    # ⚠⚠ **검은 액자도 있다.** 베일 시트는 구획 상자가 연회색인데 <b>시트 전체를 감싼
    #   테두리는 검정</b>이다. 연회색만 지우면 그 액자가 남아 배경 흘려 채우기가 시작조차
    #   못 하고(씨앗이 전부 액자 위에 떨어진다) **시트 전부가 그림**으로 잡힌다.
    #
    # ⚠ 베일의 몸은 거의 검정이라 색만으로는 액자와 구분이 안 된다 — 그래서 어두운 쪽은
    #   **더 엄격한 비율**(:data:`BORDER_DARK_RATIO`)로 「거의 한 줄 전체가 그 색」일 때만
    #   지운다. 그림은 그렇게 한 줄을 가득 채우지 않는다.
    dark = (sat <= BORDER_SAT_MAX) & (lum <= BORDER_DARK_LUM_MAX)

    h, w = light.shape
    rows = set(np.where(light.sum(axis=1) > w * BORDER_ROW_RATIO)[0].tolist())
    cols = set(np.where(light.sum(axis=0) > h * BORDER_COL_RATIO)[0].tolist())
    dark_rows = set(np.where(dark.sum(axis=1) > w * BORDER_DARK_RATIO)[0].tolist())
    dark_cols = set(np.where(dark.sum(axis=0) > h * BORDER_DARK_RATIO)[0].tolist())

    erased = 0
    for y in sorted(rows | dark_rows):
        sel = (light if y in rows else dark)[y, :]
        erased += int(sel.sum())
        arr[y, sel] = bg
    for x in sorted(cols | dark_cols):
        sel = (light if x in cols else dark)[:, x]
        erased += int(sel.sum())
        arr[sel, x] = bg

    return arr, len(rows | dark_rows), len(cols | dark_cols), erased


def load_sheet(path, box_borders=False):
    """
    시트를 읽어 파생 마스크를 함께 돌려준다.

    <paramref name="box_borders"/> 를 켜면 구획 사각 테두리를 먼저 지운다
    (:func:`erase_box_borders` — 상자가 있는 시트에서는 **반드시** 켜야 한다).
    """
    if not os.path.isfile(path):
        raise SystemExit("⚠ 원본이 없습니다: " + path)

    src = Image.open(path)

    # ★★ 2026-08-20 — <b>알파가 있는 시트를 알파로 읽는다.</b>
    #
    #   유저가 아르세니아 시트를 <b>배경 없는 PNG</b>(RGBA)로 다시 내보냈다. 그때까지
    #   이 함수는 무조건 `convert("RGB")` 로 알파를 <b>버리고</b> 흰 배경을 흘려 채워
    #   찾았는데, 알파를 버리면 투명 픽셀의 RGB 가 그대로 남아 <b>시트 전체가 그림으로</b>
    #   잡힌다(실측: 모든 줄이 «밴드 밖에 그림이 2154px» 로 나왔다 — 배경이 통째로 잉크였다).
    #
    #   그림 자체가 «어디까지가 그림인지» 를 알파로 이미 알려주고 있으므로, 있으면
    #   <b>그쪽이 정본</b>이다 — 흘려 채우기보다 정확하고, 「갇힌 배경」 문제도 아예 없다
    #   (다리 사이가 투명하게 그려져 오기 때문).
    #
    #   ⚠ 알파가 없는 옛 시트는 <b>한 줄도 안 바뀐다</b> — 아래 `has_alpha` 가 False 라
    #     예전 경로를 그대로 탄다.
    rgba = np.asarray(src.convert("RGBA")).astype(np.uint8)
    alpha = rgba[:, :, 3]
    has_alpha = bool(alpha.min() < 250)

    if has_alpha:
        # 투명한 곳을 흰색으로 깔아 둔다 — 라벨·테두리 판정이 «흰 배경» 을 전제한다.
        arr = rgba[:, :, :3].copy()
        arr[alpha < ALPHA_INK_MIN] = 255
    else:
        arr = np.asarray(src.convert("RGB")).astype(np.uint8).copy()

    bg = modal_background(arr)

    if box_borders:
        arr, nr, nc, ne = erase_box_borders(arr, bg)
        print("  %s · 구획 테두리 제거: 가로 %d줄 · 세로 %d열 · %d px"
              % (os.path.basename(path), nr, nc, ne))

    a16 = arr.astype(np.int16)
    dist = np.abs(a16 - bg).sum(axis=2)

    if has_alpha:
        # ★ 알파가 곧 배경 판정이다. 다만 <b>구획 테두리·제목 글자는 불투명</b>하므로
        #   흰색 판정도 함께 건다 — 둘 중 하나라도 배경이면 배경이다.
        #
        # ⚠⚠ <b>그 흰색 판정을 «거리» 로 하면 안 된다</b> (2026-08-20 실사고).
        #   처음에 ``dist <= BG_TOL`` 로 썼는데, 그러면 <b>불투명한 흰 픽셀이 전부 배경</b>이
        #   된다. 카이론의 <b>황금 구체(보호막)</b> 는 가운데가 거의 흰색(RGB 254 · 알파 250)
        #   이라 <b>중심에 구멍이 뚫린 채</b> 구워졌다 — 실제로 그렇게 나왔다.
        #   흰 갑옷 하이라이트·흰 두건도 같은 위험에 있었다(11,082 px 이 그렇게 지워지고 있었다).
        #
        # ★ 고치는 방법은 <b>이미 이 파일 안에 있었다</b> — :func:`background_mask` 는
        #   «시트 <b>테두리와 이어진</b> 흰색만 배경» 으로 본다(그 함수의 ★★ 주석이
        #   두건 흰색을 지키려고 만든 것이라고 적어 두었다). 알파 시트에도 그쪽을 쓴다:
        #   그림에 <b>갇힌</b> 흰색은 바깥과 이어지지 않으므로 <b>살아남는다</b>.
        #   ⚠ 원래 의도(불투명한 제목 딱지·테두리 지우기)는 그대로 유지된다 —
        #     그것들은 시트 가장자리 쪽 흰 배경에 둘러싸여 있어 흘려 채우기가 닿는다.
        bg_mask = (alpha < ALPHA_INK_MIN) | background_mask(dist)
        print("  %s · 알파로 배경 판정 (투명 %d px)"
              % (os.path.basename(path), int((alpha < ALPHA_INK_MIN).sum())))
    else:
        bg_mask = background_mask(dist)
    gray = ((a16.max(axis=2) - a16.min(axis=2)) < LABEL_SAT) & (a16.mean(axis=2) < LABEL_LUM)

    print("  원본 %s · 배경 %s · 배경 %d px / 그림 %d px"
          % (os.path.basename(path), tuple(int(v) for v in bg),
             int(bg_mask.sum()), int((~bg_mask).sum())))

    return {
        "arr": arr, "bg": bg, "dist": dist, "gray": gray,
        "lum": a16.mean(axis=2), "sat": a16.max(axis=2) - a16.min(axis=2),
        "bg_mask": bg_mask,
        # ★★ 2026-08-20 — <b>원화가 가진 알파를 그대로 실어 보낸다</b>(없으면 None).
        #   :func:`crop_rgba` 가 이것을 곱해 굽는다 — 그 함수의 ⚠⚠ 주석 참조.
        "src_alpha": alpha if has_alpha else None,
        # ★ 「그림」 마스크. 그림자를 지울 때마다 여기서 뺀다.
        "mask": ~bg_mask,
    }


# ---------------------------------------------------------------------------
# 칸 가르기
# ---------------------------------------------------------------------------

def runs(flags, min_len=1):
    """True 가 이어지는 구간 목록."""
    out, i, n = [], 0, len(flags)
    while i < n:
        if flags[i]:
            j = i
            while j < n and flags[j]:
                j += 1
            if j - i >= min_len:
                out.append((i, j - 1))
            i = j
        else:
            i += 1
    return out


def label_blobs(gray, x0, x1, ly0, ly1, gap=None, max_w=None, min_w=None):
    """
    프레임 번호 줄의 라벨 덩어리 목록 ``[(x0, x1), …]``.

    ⚠ 라벨은 **회색조로만** 찾는다 — 채도가 있는 이펙트가 라벨 줄 높이까지 올라와 있으면
    「흰색과의 거리」로만 찾을 때 이펙트가 라벨에 붙어 폭 50px 짜리 가짜 덩어리가 된다
    (라린길 실측 · 102-3절). 그 필터는 :func:`load_sheet` 의 ``gray`` 가 이미 걸었다.

    ⚠ **폭이 양쪽으로 걸린다** — :data:`LABEL_MAX_W` 위(이펙트가 붙은 덩어리)와
    :data:`LABEL_MIN_W` 아래(그림 일부가 라벨 줄에 솟은 부스러기)를 둘 다 버린다.
    바리올라 시트에서 소의 뿔이 정확히 아래쪽 함정이었다(2026-08-20).
    """
    gap = LABEL_GAP if gap is None else gap
    max_w = LABEL_MAX_W if max_w is None else max_w
    min_w = LABEL_MIN_W if min_w is None else min_w

    lab = gray[ly0:ly1 + 1, x0:x1 + 1].any(axis=0)
    xs = np.where(lab)[0]
    if not len(xs):
        return []

    blobs = []
    start = prev = xs[0]
    for x in xs[1:]:
        if x - prev > gap:
            blobs.append((start, prev))
            start = x
        prev = x
    blobs.append((start, prev))
    return [(a + x0, b + x0) for a, b in blobs
            if min_w <= b - a + 1 <= max_w]


def label_count(gray, x0, x1, ly0, ly1, gap=None, max_w=None, min_w=None):
    """프레임 번호가 몇 개인지. **검산 전용**이다."""
    return len(label_blobs(gray, x0, x1, ly0, ly1, gap, max_w, min_w))


def boxes_dominant(mask, cells, y0, y1, min_ink_ratio=0.12):
    """
    ★★ 칸마다 **한가운데를 물고 있는 덩어리**의 경계 상자. 옆 칸 조각을 버린다.

    <b>왜 필요한가</b> — 칸 경계를 산술로 가르면(:func:`cells_by_span`) 간격이 완벽하지
    않은 시트에서 **프레임마다 옆 칸의 망토·담뱃대 끝이 조금씩 딸려 들어온다.** 베일
    시트가 그랬다: 모든 프레임에 이웃의 조각이 붙어 나왔다(실제로 눈으로 확인).
    :func:`boxes_for` 는 칸 안의 <b>모든</b> 잉크를 감싸므로 그 조각까지 포함한다.

    여기서는 칸 안을 <b>빈 열로 갈라</b> 덩어리를 나누고, **칸 한가운데를 물고 있는
    덩어리**를 기준으로 삼아 좌우로 «간격이 좁은 동안만» 이어 붙인다.

    ⚠ <b>크기로 고르면 안 된다.</b> 처음엔 「가장 잉크가 많은 덩어리」로 했는데, 옆 칸에서
      들어온 <b>망토 조각이 본체의 25%</b> 나 돼서 안 걸러졌다(실측). 프레임의 본체는
      언제나 자기 칸 <b>가운데</b>에 있고 조각은 <b>가장자리</b>에 있으므로, 자리로
      가르면 크기와 무관하게 갈린다.

    ⚠ <paramref name="min_ink_ratio"/> 는 이제 «이어 붙일 최대 간격»(칸 폭에 대한 비율)이다.
      본체에 딸린 담뱃대·연기·튄 피는 바로 옆에 있어 함께 남고, 옆 칸 조각은 멀어서 빠진다.
    """
    out = []
    for cx0, cx1 in cells:
        sub = mask[y0:y1 + 1, cx0:cx1 + 1]
        if not sub.any():
            out.append(None)
            continue

        band = sub.any(axis=0)
        groups = runs(band, 1)
        if not groups:
            out.append(None)
            continue

        # ★ 기준은 «가장 큰 것» 이 아니라 **칸 한가운데를 물고 있는 덩어리**다.
        #   프레임의 본체는 자기 칸 가운데에 있고, 옆 칸에서 삐져 들어온 조각은
        #   <b>칸 가장자리</b>에 있다. 크기로 고르면 조각이 꽤 클 때(본체의 25%) 못 거른다 —
        #   자리로 고르면 크기와 무관하게 갈린다.
        width = sub.shape[1]
        mid = width // 2
        anchor = next((k for k, (s, e) in enumerate(groups) if s <= mid <= e), None)
        if anchor is None:
            # 한가운데가 비었으면(연기만 있는 칸 등) 가장 가까운 덩어리를 기준으로 삼는다.
            anchor = min(range(len(groups)),
                         key=lambda k: min(abs(groups[k][0] - mid), abs(groups[k][1] - mid)))

        # 기준 덩어리에서 좌우로, **간격이 좁은 동안만** 이어 붙인다 — 본체에 딸린
        # 담뱃대·연기·튄 피는 바로 옆에 있고, 옆 칸 조각은 멀리 떨어져 있다.
        near = max(4, int(width * min_ink_ratio))
        lo = hi = anchor
        while lo > 0 and groups[lo][0] - groups[lo - 1][1] <= near:
            lo -= 1
        while hi < len(groups) - 1 and groups[hi + 1][0] - groups[hi][1] <= near:
            hi += 1
        keep = groups[lo:hi + 1]

        gx0 = cx0 + keep[0][0]
        gx1 = cx0 + keep[-1][1]
        col = mask[y0:y1 + 1, gx0:gx1 + 1]
        ys = np.where(col.any(axis=1))[0]
        out.append((gx0, gx1, y0 + int(ys.min()), y0 + int(ys.max())))
    return out


def cells_by_clusters(mask, y0, y1, x0, x1,
                      sliver_ratio=0.40, split_ratio=1.55, edge_margin=8, search=18):
    """
    ★★ 칸을 **그림 덩어리 그대로** 잡는다 — 프레임이 서로 떨어져 있는 시트용.

    :func:`cells_by_gaps` 와 다른 점은 **뒷정리 두 가지**다. 그것 때문에 빈 열이 있어도
    그냥 세기만 하면 개수가 틀린다:

    1. <b>부스러기를 버린다</b> — 폭이 중앙값의 :paramref:`sliver_ratio` 미만인 덩어리.
       원화 옆에 튄 점·잘린 획이 한두 개씩 있다(베일 이동 줄에 7px·5px 두 개).
    2. <b>붙은 덩어리를 가른다</b> — 폭이 중앙값의 :paramref:`split_ratio` 배를 넘으면
       «중앙값 몇 개분인지» 로 등분하되, 경계는 그 근처에서 <b>잉크가 가장 적은 열</b>로
       옮긴다. 그래야 팔·망토 한가운데를 자르지 않는다.

    ⚠ 이 방법은 **프레임 폭이 고르다는 전제**에 기댄다(중앙값을 자로 쓰므로). 베일
      ``_01`` 이 그렇다(91~146px). 폭이 제각각인 시트에서는 :func:`cells_by_span` 이 낫다.
    """
    sub = mask[y0:y1 + 1, x0:x1 + 1]
    band = sub.any(axis=0)
    groups = runs(band, 1)
    if not groups:
        return []

    widths = [e - s + 1 for s, e in groups]
    med = float(np.median(widths))
    keep = [g for g, wd in zip(groups, widths) if wd >= med * sliver_ratio]
    if not keep:
        return []
    med = float(np.median([e - s + 1 for s, e in keep]))

    cols = sub.sum(axis=0)
    out = []
    for s, e in keep:
        wd = e - s + 1
        n = int(round(wd / med)) if med > 0 else 1
        if n >= 2 and wd > med * split_ratio:
            bounds = [s]
            for k in range(1, n):
                mid = s + int(wd * k / n)
                lo = max(s + edge_margin, mid - search)
                hi = min(e - edge_margin, mid + search)
                bounds.append(lo + int(np.argmin(cols[lo:hi])) if hi > lo else mid)
            bounds.append(e + 1)
            for k in range(len(bounds) - 1):
                out.append((bounds[k] + x0, bounds[k + 1] - 1 + x0))
        else:
            out.append((s + x0, e + x0))
    return out


def cells_by_span(mask, y0, y1, x0, x1, count):
    """
    ★★ 칸 경계를 **그림이 차지한 폭을 균등 분할**해서 정한다 — 프레임 수를 아는 줄에 쓴다.

    <b>왜 이게 가장 튼튼한가</b> — 베일 시트에서 앞의 세 방법이 다 실패했다(실측):

      · ``cells_by_gaps``   프레임이 붙어 빈 열이 없다 (24칸 → 4~11칸)
      · ``cells_by_labels`` 두 자리 라벨이 붙어 개수가 모자란다 (16칸 → 13~14개)
      · ``cells_by_pitch``  <b>맨 앞 라벨이 「1」이 아닐 수 있다.</b> 원거리 줄에서 「1」을
        놓쳐 간격이 한 칸씩 밀렸고, 프레임마다 옆 칸이 조금씩 잘려 들어왔다.

    반면 «그림이 놓인 전체 폭 ÷ 장수» 는 라벨을 아예 안 본다. 실측으로 라벨에서 구한
    간격과 **거의 같다**: 대기 82.4 ↔ 82.3 · 스킬1 62.1 ↔ 62.3 · 스킬2 61.4 ↔ 61.0.

    ⚠ 전제는 «칸 간격이 일정하고, 맨 앞·맨 뒤 칸의 그림이 자기 칸을 채운다» 다.
      이펙트가 줄 밖으로 뻗는 시트에서는 :paramref:`x1` 로 범위를 좁혀 줄 것.
    ⚠ 개수를 반드시 넘겨야 한다 — 시트에 «(16프레임)» 처럼 적혀 있는 값이다.
    """
    if count < 1:
        return []
    band = mask[y0:y1 + 1, x0:x1 + 1].any(axis=0)
    xs = np.where(band)[0]
    if not len(xs):
        return []

    a = int(xs.min()) + x0
    b = int(xs.max()) + x0
    width = (b - a + 1) / float(count)
    return [(int(round(a + i * width)), int(round(a + (i + 1) * width)) - 1)
            for i in range(count)]


#: 발밑 띠의 높이 — 밴드 안 그림 높이의 이 비율만 본다.
FEET_FRAC = 0.18
#: 발밑에서 이 폭보다 좁은 조각은 부스러기로 본다(px).
FEET_MIN_W = 12


#: 「허리」로 볼 잉크 두께 — 몸통 열 평균의 이 비율 이하면 «여기가 끊긴 곳» 으로 본다.
TAIL_WAIST_RATIO = 0.18


def merge_to_count(segs, count):
    """
    ★★ 발 조각을 <b>목표 개수까지 «가장 좁은 틈부터» 합친다</b> (2026-08-20 신설).

    <b>왜 필요했나</b> — 발밑 판정은 불칸에서 완벽했지만 카이론에서 <b>무너졌다</b>(실측:
    이동 줄이 9장인데 <b>16조각</b>). 이유가 분명하다: 불칸은 <b>긴 로브</b>를 입어 두 발이
    한 덩어리로 붙는데, 카이론은 <b>맨다리</b>라서 <b>다리 하나가 조각 하나</b>가 된다.
    걷는 자세는 다리를 벌리므로 조각이 프레임마다 둘씩 나온다.

    ★ 고치는 방법은 «발이 몇 개인가» 를 세지 않고 <b>«프레임이 몇 장인가» 를 주는</b> 것이다.
      그러면 남은 일은 <b>어디를 합칠지</b>인데, 답이 하나뿐이다 — <b>가장 좁은 틈</b>.
      한 프레임 안의 두 발 사이(보폭)는 프레임과 프레임 사이보다 <b>반드시 좁다</b>
      (그렇지 않으면 프레임이 겹친다). 그래서 좁은 틈부터 차례로 합치면
      <b>다리끼리 먼저 붙고</b> 프레임 경계는 마지막까지 남는다.

    ⚠ <b>조각이 목표보다 적으면 아무것도 안 한다</b> — 쪼갤 방법이 없다. 그때는
      부르는 쪽의 ``expect`` 검사가 <b>죽어 준다</b>(조용히 틀린 프레임을 굽지 않는다).
    ⚠ 이 단계가 붙으면서 ``feet`` 의 ``expect`` 는 «검산» 에서 <b>«입력»</b> 이 된다 —
      ``span`` 과 같은 성질이다. 장수는 시트를 <b>눈으로 세어</b> 적을 것.
    """
    segs = list(segs)
    if count is None or count < 1 or len(segs) <= count:
        return segs
    while len(segs) > count:
        gaps = [segs[i + 1][0] - segs[i][1] for i in range(len(segs) - 1)]
        k = min(range(len(gaps)), key=lambda i: gaps[i])
        segs[k] = (segs[k][0], segs[k + 1][1])
        del segs[k + 1]
    return segs


def cells_by_feet(mask, y0, y1, x0, x1, frac=None, min_w=None, trim_tail=False,
                  count=None):
    """
    ★★ 칸 경계를 **«발밑»만 보고** 정한다 (2026-08-20 신설).

    <b>왜 이것이 필요했나</b> — 2026-08-20 에 교체된 새 시트들은 <b>이펙트가 몸통과 겹쳐
    그려져 있다</b>. 그러면 앞의 어느 방법도 못 가른다(불칸 실측):

      · ``gaps``     푸른 칼자국·마법진이 칸 사이를 메워 5칸이 <b>1~3칸</b>으로 붙는다
      · ``clusters`` 개수는 맞을 때가 있지만 <b>가장 얇은 곳</b>에서 끊으므로 경계가
        몸통 <b>안쪽</b>으로 들어온다(마법 4번 칸이 x399 에서 끊겨 몸이 잘렸다)
      · ``span``     줄 끝에 «손을 떠난 탄» 이 붙어 있으면 그 폭까지 나눗셈에 들어가
        <b>모든 칸이 밀린다</b>. 게다가 이 시트는 간격이 고르지도 않다(94·93·92·99·106·106·109)

    그런데 <b>몸통에는 이펙트가 갖지 못한 성질이 하나 있다</b> — 몸통은 <b>땅을 딛는다</b>.
    칼자국·마법진·불덩이는 공중에 뜬다. 그래서 밴드의 <b>아래쪽 %d%%</b> 만 들여다보면
    «발» 만 남고, 발은 프레임마다 <b>깨끗하게 떨어져 있다</b>.

    이 성질이 두 가지를 <b>공짜로</b> 해결한다:

    ① <b>손을 떠난 탄이 저절로 빠진다</b> — 불칸 원거리·스킬1 줄은 «몸통 4 + 탄 1» 인데
       발밑으로 세면 <b>정확히 4</b> 가 나온다(``take`` 를 안 써도 된다).
    ② 경계가 <b>몸과 몸 사이</b>에 놓인다 — 발 사이의 가운데를 경계로 삼으므로
       몸통 안쪽으로 파고들지 않는다.

    ⚠ **땅에 닿는 이펙트는 못 가른다** — 바닥에 깔리는 마법진·화염 고리는 발처럼 보인다.
      그 줄은 여전히 ``take``/``keep`` 이나 ``bounds`` 가 필요하다(불칸 마법 줄이 그렇다).
    ⚠ 발밑 띠는 <b>밴드 높이가 아니라 «실제 그림의 아래끝»</b> 에서 잡는다 — 밴드를
      넉넉히 잡아도 흔들리지 않게 하려는 것이다.
    """ % int(FEET_FRAC * 100)
    # ⚠ ``None`` 을 받아 기본값으로 되돌린다 — 호출부가 «앞 인자는 기본, 뒤 인자만 지정»
    #   을 하려면 (`("feet", None, None, True)`) 이 처리가 있어야 한다.
    frac = FEET_FRAC if frac is None else frac
    min_w = FEET_MIN_W if min_w is None else min_w

    band = mask[y0:y1 + 1, x0:x1 + 1]
    prof = band.sum(axis=1)
    nz = np.where(prof > 1)[0]
    if not len(nz):
        return []

    bottom = int(nz[-1])
    height = int(nz[-1]) - int(nz[0]) + 1
    top = max(0, bottom - max(4, int(height * frac)))
    feet = band[top:bottom + 1].any(axis=0)
    segs = [(a, b) for a, b in runs(feet, 1) if b - a + 1 >= min_w]
    if not segs:
        return []

    # ★★ 다리가 갈라져 조각이 늘어난 줄은 <b>목표 개수까지 합친다</b>(위 merge_to_count).
    segs = merge_to_count(segs, count)

    # ★★ 경계는 «발 사이에서 <b>가장 옅은 열</b>» 이다 — 히스톤에서 배운 것이다
    #   (`histon_skin_build.frame_bounds` 의 긴 주석). 가운데에서 자르면 <b>이펙트의
    #   가장 두꺼운 곳</b>을 지나가는 일이 생겨 궤적이 반토막 난다.
    #
    #   ⚠ 히스톤에서는 «가장 옅은 열» 자동 탐지가 <b>실패했다</b> — 격자 경계 ±N 을
    #     훑었더니 <b>궤적 한가운데</b>(프레임 안쪽)가 가장 옅게 나왔기 때문이다.
    #     여기서는 그 함정이 없다: 훑는 범위를 <b>«발과 발 사이»로 못박기</b> 때문에
    #     몸통 안쪽은 애초에 후보가 아니다(몸통은 발을 품고 있다).
    #   ★ 동률이면 <b>가운데에 가까운 쪽</b>을 고른다 — 한쪽 프레임에 치우친 경계는
    #     그 프레임의 날개를 깎는다.
    ink = band.sum(axis=0)
    cuts = [0]
    for (_a0, b0), (a1, _b1) in zip(segs, segs[1:]):
        lo, hi = b0 + 1, a1                      # [발 끝+1, 다음 발 시작)
        if hi <= lo:
            cuts.append(a1)
            continue
        window = ink[lo:hi]
        mid = (b0 + a1) / 2.0
        thin = window.min()
        cand = [lo + int(k) for k in np.where(window == thin)[0]]
        cuts.append(min(cand, key=lambda x: abs(x - mid)))

    # ★★ <b>마지막 칸의 오른쪽 끝</b> — :paramref:`trim_tail` (2026-08-20).
    #
    #   <b>왜 필요했나</b> — 발밑 판정은 «칸 수» 는 맞게 세지만(뜬 이펙트는 발이 없다)
    #   <b>마지막 칸의 오른쪽 끝은 여전히 x1</b> 이다. 그래서 「손을 떠난 탄」·「바닥 마법진」이
    #   마지막 몸통 칸에 <b>딸려 들어간다</b> — 구운 그림에서 «몸통 옆에 붙은 붉은 조각» 으로
    #   실제로 나왔다(유저 지시: *"캐릭터랑 이펙트를 확실하게 분리"*).
    #
    #   밴드 x1 을 손으로 줄이는 방법도 있지만 <b>줄마다·방향마다 값이 다르다</b>
    #   (불칸 스킬1: 좌 397 · 우 416). 시트가 넷이고 줄이 스무 개씩이라 손으로는 못 쫓는다.
    #
    #   그래서 «허리» 를 찾는다: 마지막 발 끝에서 오른쪽으로 훑어, 잉크가 <b>몸통 평균의
    #   18% 이하</b>로 떨어지는 <b>첫</b> 열에서 끊는다. 몸통은 두껍고, 몸통과 이펙트가
    #   붙어 있어도 그 사이는 얇다 — 그것이 «허리» 다.
    #   ⚠ 못 찾으면 <b>x1 그대로 둔다</b> — 억지로 끊으면 날개가 잘린다.
    tail = band.shape[1]
    if trim_tail and segs:
        body_cols = np.concatenate([ink[a:b + 1] for a, b in segs])
        waist = max(3.0, float(body_cols.mean()) * TAIL_WAIST_RATIO)
        for x in range(segs[-1][1] + 1, band.shape[1]):
            if ink[x] <= waist:
                tail = x
                break
    cuts.append(tail)
    return [(cuts[i] + x0, cuts[i + 1] - 1 + x0) for i in range(len(segs))]


def cells_by_pitch(gray, x0, x1, ly0, ly1, count, gap=None, max_w=None, min_w=None):
    """
    ★★ 칸 경계를 **일정한 간격**으로 정한다 — 프레임 수를 아는 줄에 쓴다.

    <b>언제 이걸 쓰나</b> — 베일 시트처럼 ① 프레임이 서로 붙어 빈 열이 없고
    (``cells_by_gaps`` 실패: 24칸이 4~11칸으로 붙는다) ② 라벨이 두 자리 숫자에서
    붙거나 잘려 개수가 모자란 줄이다(``cells_by_labels`` 실패: 16칸이 13개로 잡힌다).

    ★ 그런 시트도 **칸 간격은 일정**하다 — 라벨이 등간격으로 찍혀 있는 것이 그 증거다.
      그래서 <b>맨 앞과 맨 뒤 라벨</b>만 믿고 나머지는 산술로 채운다. 가운데 라벨이
      몇 개 붙어도 상관없다.

    ⚠ 그래서 **개수를 반드시 넘겨야 한다** — 시트에 «(16프레임)» 처럼 적혀 있는 값이다.
      개수를 틀리면 조용히 어긋난 칸이 나오므로, 부르는 쪽이 시트의 표기를 그대로 적을 것.
    """
    blobs = label_blobs(gray, x0, x1, ly0, ly1, gap, max_w, min_w)
    if len(blobs) < 2 or count < 1:
        return []

    first = (blobs[0][0] + blobs[0][1]) / 2.0
    last = (blobs[-1][0] + blobs[-1][1]) / 2.0
    if count == 1:
        return [(x0, x1)]

    pitch = (last - first) / (count - 1)
    centers = [first + i * pitch for i in range(count)]
    edges = ([x0] +
             [int(round((centers[i] + centers[i + 1]) / 2.0)) for i in range(count - 1)] +
             [x1])
    return [(edges[i], edges[i + 1]) for i in range(count)]


def cells_by_labels(gray, x0, x1, ly0, ly1, gap=None, max_w=None, min_w=None):
    """
    ★ 칸 경계를 **라벨 중심의 중간점**으로 정한다 (말파스·라린길 방식 · 113-1절).

    <b>언제 이걸 쓰나</b> — 프레임 사이에 빈 열이 없는 시트다. 시그리드 시트가 그렇다:
    지팡이가 옆 칸까지 뻗어 있어 ``cells_by_gaps`` 로는 8칸이 5칸으로 붙는다(실측).
    엘린 시트는 빈 열이 다 있어서 그쪽을 쓴다 — 빈 열이 있으면 그것이 더 정확하다.

    ⚠ 첫 칸의 왼쪽 끝과 마지막 칸의 오른쪽 끝은 **패널 경계**로 둔다. 라벨 간격의 절반을
      바깥으로 미는 방법도 있지만, 그러면 패널 밖(다른 단)까지 먹을 수 있다.
    """
    centers = [(a + b) // 2 for a, b in label_blobs(gray, x0, x1, ly0, ly1, gap, max_w, min_w)]
    if len(centers) < 2:
        return []
    edges = [x0] + [(centers[i] + centers[i + 1]) // 2 for i in range(len(centers) - 1)] + [x1]
    return [(edges[i], edges[i + 1]) for i in range(len(edges) - 1)]


def cells_by_gaps(mask, y0, y1, x0, x1, min_len=None):
    """
    칸 경계 = **빈 열의 가운데**. 이 시트는 (프레임 수 + 1)개의 빈 열이 다 있다(맨 위).

    ★ 2026-08-20 — :paramref:`min_len` 을 열었다(기본값 :data:`CELL_GAP_MIN` 그대로).

      아르세니아의 이펙트 줄에서 <b>3px 짜리 틈</b>이 폭발 하나를 둘로 갈랐다(실측:
      투사체 줄에 5칸을 기대했는데 6칸). 반짝이는 입자 사이의 우연한 틈이라 «칸 경계» 가
      아니다 — 그런 줄만 8 정도로 올려 <b>진짜 칸 사이의 틈만</b> 남긴다.
    """
    band = mask[y0:y1 + 1, x0:x1 + 1].any(axis=0)
    gaps = [(a + x0, b + x0) for a, b in runs(~band, CELL_GAP_MIN if min_len is None else min_len)]
    mids = [(a + b) // 2 for a, b in gaps]
    return [(mids[k], mids[k + 1]) for k in range(len(mids) - 1)]


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


# ---------------------------------------------------------------------------
# 프레임 굽기
# ---------------------------------------------------------------------------

def alpha_for(opaque, dist):
    """
    알파 — **안쪽은 255, 배경에 닿는 한 겹만** 거리로 깎는다 (맨 위 ★★ 첫 번째).

    거리 하나로 전부 정하면 배경색과 같은 흰 두건이 사라진다. 안쪽을 통째로 불투명하게
    두고 경계 한 겹만 부드럽게 하면 원화의 안티에일리어싱이 살아나면서 흰색도 남는다.
    기존 캐릭터 스프라이트도 부분 알파가 프레임당 100~1500px 뿐인 «거의 하드 알파» 다.
    """
    alpha = np.where(opaque, 255.0, 0.0)

    # 투명에 닿는 불투명 한 겹 = 투명 마스크를 한 걸음 키운 뒤 불투명과 겹치는 부분.
    edge = grow(~opaque, np.ones_like(opaque)) & opaque
    soft = np.clip((dist - BG_TOL) * 255.0 / (ALPHA_HI - BG_TOL),
                   RING_MIN_ALPHA, 255.0)
    alpha[edge] = soft[edge]
    return alpha.astype(np.uint8)


def crop_rgba(sheet, box):
    """상자를 RGBA 로 굽고 알파 경계까지 다시 죈다.

    ⚠⚠ **원화에 알파가 있으면 그 알파를 곱한다** (2026-08-20 실사고 · 아루 새 시트).

    :func:`alpha_for` 는 «배경에 닿는 한 겹만» 깎고 <b>안쪽은 통째로 255</b> 로 만든다.
    잉크/배경을 색으로 가려내던 옛 시트에서는 그것이 맞았다 — 배경은 흰색 한 가지였고
    그림 안에 «반투명» 이라는 개념이 없었다.

    그런데 <b>알파 PNG 는 그림 자체가 반투명 구역을 갖고 있다.</b> 새 세대 시트 넷은
    전부 <b>알파 8~63 인 픽셀이 15만 장 안팎</b>이다(아루 189k · 카이론 147k) — 인물을
    감싼 <b>부드러운 잔광·그림자</b>다. :data:`ALPHA_INK_MIN` 이 8 이므로 그것이 전부
    «잉크» 로 잡히고, ``alpha_for`` 가 안쪽을 255 로 채워 <b>불투명한 갈색 테두리</b>가
    된다. 실제로 그렇게 나왔다 — 아루 대기 프레임을 4배로 늘려 보면 인물 실루엣 바깥에
    <b>올리브색 판때기</b>가 한 겹 둘러 있다(카이론·아르세니아·불칸도 옅게 같은 증상).

    ★ 고치는 방법은 «원화가 이미 답을 갖고 있다» 는 것이다 — <b>화가가 그린 알파</b>가
      어디까지가 그림인지 말해 준다. 그래서 두 알파를 <b>곱한다</b>:

      * ``alpha_for``   — 배경 판정(제목 딱지·옆 칸 조각을 지운 결과)
      * ``src_alpha``   — 화가가 그린 부드러움

      ⚠ 상자를 다시 죄는 기준(``alpha > 0``)은 <b>바뀌지 않는다</b> — ``mask`` 가
        ``alpha >= 8`` 의 부분집합이므로 곱해도 0 이 되는 픽셀이 새로 생기지 않는다.
        즉 <b>프레임 크기·피벗은 한 픽셀도 안 움직인다</b>(재보고 확인했다).
      ⚠ 알파 없는 옛 시트(몬스터 대부분)는 ``src_alpha`` 가 ``None`` 이라
        <b>예전 경로를 그대로 탄다</b>.
    """
    bx0, bx1, by0, by1 = box
    sl = (slice(by0, by1 + 1), slice(bx0, bx1 + 1))
    rgb = sheet["arr"][sl]
    alpha = alpha_for(sheet["mask"][sl], sheet["dist"][sl]).astype(np.float32)
    src = sheet.get("src_alpha")
    if src is not None:
        alpha *= src[sl].astype(np.float32) / 255.0
    rgba = np.dstack([rgb, np.clip(alpha, 0, 255).astype(np.uint8)]).astype(np.uint8)

    solid = rgba[:, :, 3] > 0
    if not solid.any():
        return rgba
    ys = np.where(solid.any(axis=1))[0]
    xs = np.where(solid.any(axis=0))[0]
    return rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def resample_rgba(rgba, factor, resample=None):
    """
    RGBA 프레임 한 장을 <paramref name="factor"/> 배로 리샘플한다 (2026-08-20 신설).

    ★★ <b>알파를 먼저 곱한다(premultiply).</b> 그냥 RGBA 를 늘리면 <b>투명 픽셀의 색</b>이
    경계로 번진다 — 이 시트들의 배경은 흰색이라 캐릭터 둘레에 <b>흰 테두리</b>가 생기고,
    게임 배경이 어두워서 그게 그대로 눈에 보인다.
    ``말파스`` 는 같은 문제를 «RGB 를 먼저 늘리고 그 다음에 알파를 만든다» 로 피했는데
    (``malphas_skin_build.render_frame``), 그 방법은 <b>알파를 거리로 다시 계산</b>해야 해서
    :func:`background_mask` 가 살려낸 <b>흰 두건</b>을 잃는다. 곱셈 방식은 이미 만들어 둔
    알파를 그대로 늘리므로 그 위험이 없다.

    ⚠ 배율이 1 에 가까우면 <b>원본을 그대로 돌려준다</b> — 무의미한 리샘플로 도트가
      한 번 더 뭉개지지 않게.
    """
    if resample is None:
        resample = Image.LANCZOS
    if abs(factor - 1.0) <= 0.002:
        return rgba

    h, w = rgba.shape[0], rgba.shape[1]
    nw = max(1, int(round(w * factor)))
    nh = max(1, int(round(h * factor)))

    a = rgba[:, :, 3].astype(np.float32) / 255.0
    pm = rgba[:, :, :3].astype(np.float32) * a[:, :, None]      # 곱해 둔다

    pm_img = Image.fromarray(np.clip(pm, 0, 255).astype(np.uint8), "RGB")
    a_img = Image.fromarray(rgba[:, :, 3], "L")

    pm2 = np.asarray(pm_img.resize((nw, nh), resample)).astype(np.float32)
    a2 = np.asarray(a_img.resize((nw, nh), resample)).astype(np.float32)

    # 다시 나눈다. 알파 0 인 곳은 색이 뜻을 갖지 않으므로 0 으로 둔다.
    safe = np.maximum(a2, 1.0) / 255.0
    rgb2 = np.clip(pm2 / safe[:, :, None], 0, 255)
    rgb2[a2 <= 0] = 0

    out = np.dstack([rgb2.astype(np.uint8), a2.astype(np.uint8)])

    # 리샘플이 남긴 반투명 여백을 다시 죈다(crop_rgba 와 같은 마무리).
    solid = out[:, :, 3] > 0
    if not solid.any():
        return out
    ys = np.where(solid.any(axis=1))[0]
    xs = np.where(solid.any(axis=0))[0]
    return out[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def feather_edges(rgba, top=0, bottom=0, left=0, right=0):
    """
    프레임의 <b>지정한 변</b>에서 알파를 0 으로 <b>서서히 떨어뜨린다</b> (2026-08-21 신설).

    ★★ <b>왜 필요한가</b> — 유저 리포트: *"쉴드 이미지 또 잘렸네 이펙트 확실히 구분해서
    잘라"*. 카이론의 보호막 구체 여섯 장은 시트에서 <b>위·아래 줄이 맞닿아</b> 있다
    (실측: 한 열의 잉크가 y892~1024 로 <b>끊기지 않는다</b> — 구체 두 개가 딱 붙어 있다).
    즉 <b>어디를 잘라도 잘린다.</b> 가장 옅은 허리(y956~960)에서 갈라도 옅은 후광이
    <b>변에 28px 남고</b>, 그 자리가 «칼로 자른 직선» 으로 보인다.

    그래서 «어디서 자를까» 를 더 찾는 대신 <b>자른 자리를 부드럽게 만든다</b>.
    직선으로 끊긴 후광은 눈에 띄지만 <b>서서히 사라지는 후광은 원래 그런 것처럼 보인다.</b>

    ⚠ <b>알파만</b> 건드린다 — RGB 는 그대로다(:func:`sharpen_rgba` 와 같은 규칙).
    ⚠ 폭은 <b>그 변에서 몇 px 을</b> 이라는 뜻이고, 0 이면 그 변은 손대지 않는다.
    """
    if top <= 0 and bottom <= 0 and left <= 0 and right <= 0:
        return rgba
    out = rgba.copy()
    a = out[:, :, 3].astype(np.float32)
    h, w = a.shape

    def ramp(n):
        # 0 → 1 로 오르는 부드러운 곡선(끝이 0 이라 «완전히 사라진다»)
        return (np.arange(1, n + 1, dtype=np.float32) / (n + 1.0)) ** 1.5

    if top > 0:
        n = min(top, h)
        a[:n, :] *= ramp(n)[:, None]
    if bottom > 0:
        n = min(bottom, h)
        a[h - n:, :] *= ramp(n)[::-1][:, None]
    if left > 0:
        n = min(left, w)
        a[:, :n] *= ramp(n)[None, :]
    if right > 0:
        n = min(right, w)
        a[:, w - n:] *= ramp(n)[::-1][None, :]

    out[:, :, 3] = np.clip(a, 0, 255).astype(np.uint8)
    return out


def sharpen_rgba(rgba, amount=0.9, radius=1.0, threshold=2):
    """
    RGBA 프레임의 <b>RGB 만</b> 언샵 마스크로 조인다 (2026-08-20 신설).

    ★★ <b>왜 필요한가</b> (유저 지시: *"이미지가 너무 흐려서 베일이랑 베일 스킬 이미지
    선명하게 해줘"*)
    ------------------------------------------------------------------
    큰 보스는 원화 한 픽셀이 화면에서 <b>여러 픽셀</b>이 된다. 실측:
    <code>
        타일당 원화 픽셀   일반 중립 70~96 px
                           에픽 보스  13~23 px      ← 5~6배 부족
        베일은 콜라이더가 15x10 으로 <b>표에서 혼자 큰 값</b>이라(나머지 보스는 11x7.5)
        확대율이 x7.4 까지 올라간다.
    </code>
    거기다 베일 원화는 <b>부드러운 에어브러시 음영</b>으로 그려져 있어(다른 보스는 먹선이
    또렷하다) 확대하면 그 무름이 그대로 커진다. 다른 보스와 나란히 놓고 보면 확연히 흐리다.

    ★ 확대로 잃은 <b>해상도</b>는 되돌릴 수 없다 — 대신 <b>국부 대비</b>를 올려 경계를
      또렷하게 만든다. 그림을 새로 그리지 않고 할 수 있는 것 중 이게 가장 정직하다.

    ⚠ <b>알파는 건드리지 않는다.</b> 알파에 언샵을 걸면 :func:`alpha_for` 가 만든 부드러운
      경계 한 겹이 들쭉날쭉해져 <b>테두리에 점이 튄다</b>.
    ⚠ 투명한 곳의 RGB(흰 배경)가 경계로 번지지 않게 <b>알파를 곱해 두고</b> 필터를 걸었다가
      다시 나눈다 — :func:`resample_rgba` 와 같은 이유다.
    """
    if amount <= 0.0:
        return rgba

    a = rgba[:, :, 3].astype(np.float32) / 255.0
    pm = rgba[:, :, :3].astype(np.float32) * a[:, :, None]

    img = Image.fromarray(np.clip(pm, 0, 255).astype(np.uint8), "RGB")
    img = img.filter(ImageFilter.UnsharpMask(radius=radius,
                                             percent=int(round(amount * 100)),
                                             threshold=threshold))
    pm2 = np.asarray(img).astype(np.float32)

    safe = np.maximum(a, 1.0 / 255.0)
    rgb2 = np.clip(pm2 / safe[:, :, None], 0, 255)
    rgb2[a <= 0] = 0

    return np.dstack([rgb2.astype(np.uint8), rgba[:, :, 3]])


def head_pixels(rgba, thin_ratio=0.35, top_ratio=0.45,
                lum_min=140, sat_max=45, gray_max=35):
    """
    프레임에서 <b>머리(밝고 저채도인 덩어리 = 은발 + 흰 두건)</b> 픽셀 수 (2026-08-20 신설).

    ★★ <b>왜 「키」가 아니라 「머리」로 크기를 재는가</b> — 말파스는 행마다 그린 크기가
    다른 문제를 <b>세로 중앙값</b>으로 맞췄다(``SCALE_REFERENCE_MOTION``). 그런데 그 방법은
    <b>지팡이를 든 캐릭터와 기울어지는 모션</b>에서 어긋난다:

      · 지팡이가 곧게 서면(대기) 경계 상자가 위로 늘어나고, 비스듬하면(이동) 짧아진다.
      · <b>이동은 몸을 기울인다</b> — 키가 줄어드는 것이 <b>연출</b>이지 크기 오류가 아니다.

    실측(시그리드): 「키」로 재면 이동에 <b>1.154배</b>가 필요하다고 나오는데, 머리로 재면
    <b>1.045배</b>다. 눈으로 봐도 이동의 머리는 대기와 거의 같다 — 기울어져서 키만 줄었다.
    「키」를 믿고 늘리면 걸을 때 <b>10% 커진다</b>(고치려던 증상이 반대로 생긴다).

    머리는 <b>기울어도 크기가 안 바뀌는</b> 부위라 배율의 기준이 된다.

    ⚠ <b>얇은 줄기(지팡이·사슬)를 먼저 버린다</b> — 그 열이 상자를 위로 끌어올려
      「상단 45%」의 기준을 망친다. 판정은 :data:`EDGE_STREAK_RATIO` 와 같은 생각이다.
    """
    a = rgba[:, :, 3] > 100
    if not a.any():
        return 0

    thick = a.sum(axis=0)
    keep = thick >= thick.max() * thin_ratio
    body = a & keep[None, :]
    if not body.any():
        return 0

    ys = np.where(body.any(axis=1))[0]
    y0, y1 = ys[0], ys[-1]
    top = np.zeros_like(a)
    top[y0:y0 + max(1, int((y1 - y0 + 1) * top_ratio)), :] = True

    r = rgba[:, :, 0].astype(int)
    g = rgba[:, :, 1].astype(int)
    b = rgba[:, :, 2].astype(int)
    head = body & top & (r > lum_min) & (np.abs(r - b) < sat_max) & (np.abs(r - g) < gray_max)
    return int(head.sum())


def column_thickness(rgba):
    """열마다 «가장 위 픽셀 ~ 가장 아래 픽셀» 두께. 얇은 줄기(사슬)를 가려내는 재료."""
    solid = rgba[:, :, 3] > 0
    out = np.zeros(rgba.shape[1], dtype=int)
    for i in range(rgba.shape[1]):
        ys = np.where(solid[:, i])[0]
        out[i] = (ys.max() - ys.min() + 1) if len(ys) else 0
    return out


def body_anchor(rgba):
    """
    이 프레임에서 **몸통의 가로 중심**(px). 캔버스 정렬 기준이다.

    그림 전체 중심을 쓰면 사슬이 한쪽으로 뻗은 칸에서 몸통이 반대쪽으로 밀린다 —
    피벗이 캔버스 가로 한가운데(0.5)라 그만큼 엘린이 옆으로 미끄러져 보인다(113-4절).
    """
    th = column_thickness(rgba)
    if th.max() <= 0:
        return rgba.shape[1] / 2.0
    thick = np.where(th > th.max() * BODY_STREAK_RATIO)[0]
    if not len(thick):
        return rgba.shape[1] / 2.0
    return (thick.min() + thick.max() + 1) / 2.0


def body_extent(rgba):
    """
    이 프레임에서 <b>몸통만</b>의 (가로, 세로) 픽셀 크기. 앞으로 뻗은 이펙트는 뺀다.

    ★★ 2026-08-20 신설 — 유저 지시: *"캐릭터의 크기는 유지하고 앞 공간에 이펙트가 나올
    공간을 넣는 로직 … 이펙트의 크기도 실측해서 분리하고 딱 캐릭터의 크기는 유지되게"*.

    <b>왜 상자 크기로는 못 재나</b> — 근접 공격 프레임은 «캐릭터 + 앞으로 뻗는 궤적» 이라
    상자가 이펙트만큼 커진다. 그 값으로 «크기가 유지되는가» 를 판단하면 <b>전부 실패</b>로
    나온다(실제로 카이론 근거리 상자는 대기보다 1.4배 넓다 — 몸은 같은데도).

    그래서 :func:`body_anchor` 가 쓰는 <b>«두꺼운 열»</b> 판정을 그대로 쓴다: 세로로 두꺼운
    열만 몸통으로 보고, 그 구간의 가로 폭과 <b>그 구간 안에서의</b> 세로 높이를 잰다.
    궤적·연기는 얇게 퍼지므로 이 판정에서 빠진다.
    """
    th = column_thickness(rgba)
    if th.max() <= 0:
        return (0, 0)
    thick = np.where(th > th.max() * BODY_STREAK_RATIO)[0]
    if not len(thick):
        return (0, 0)
    x0, x1 = int(thick.min()), int(thick.max())
    sub = rgba[:, x0:x1 + 1, 3] > 8
    ys = np.where(sub.any(axis=1))[0]
    if not len(ys):
        return (x1 - x0 + 1, 0)
    return (x1 - x0 + 1, int(ys[-1] - ys[0] + 1))


def base_anchor(rgba):
    """이펙트의 **밑동** 가로 중심 — 땅에 박힌 구멍이 대상 발밑에 오게 한다."""
    solid = rgba[:, :, 3] > 0
    h = solid.shape[0]
    band = solid[int(h * (1.0 - FX_BASE_RATIO)):]
    xs = np.where(band.any(axis=0))[0]
    if not len(xs):
        return rgba.shape[1] / 2.0
    return (xs.min() + xs.max() + 1) / 2.0


def compose(frames, anchors):
    """
    한 묶음을 **같은 캔버스**에 얹는다 — 세로는 바닥(피벗 0.5, 0), 가로는 anchor.

    캔버스 가로를 anchor 좌우로 **같게** 잡는 것이 핵심이다. 안 그러면 피벗(가로 0.5)이
    몸통 중심에서 벗어나 프레임마다 조금씩 옆으로 튄다.
    """
    pad = max(max(anchors), max(f.shape[1] - a for f, a in zip(frames, anchors)))
    w = int(np.ceil(pad * 2))
    h = max(f.shape[0] for f in frames)
    out = []
    for rgba, anchor in zip(frames, anchors):
        canvas = np.zeros((h, w, 4), dtype=np.uint8)
        bh, bw = rgba.shape[0], rgba.shape[1]
        ox = int(round(w / 2.0 - anchor))
        ox = max(0, min(ox, w - bw))
        canvas[h - bh:h, ox:ox + bw] = rgba
        out.append(Image.fromarray(canvas, "RGBA"))
    return out, w, h


#: 기본 필터 — <b>Point(0)</b>. 이 프로젝트의 원화는 대부분 픽셀아트라 확대해도 또렷한 쪽이 맞다.
#:
#: ★ 2026-08-20 — <b>열어 뒀다</b>(기본값은 그대로). 베일처럼 «원화는 손그림인데 게임에서
#:   7배로 확대되는» 유닛은 Point 로 그리면 <b>7px 짜리 계단</b>이 그대로 보인다
#:   (유저 리포트: *"베일 너무 이미지 깨짐"*). 그런 유닛만 Bilinear(1) 로 굽는다.
FILTER_POINT = 0
FILTER_BILINEAR = 1


def write_png(img, folder, name, ppu=PPU, filter_mode=FILTER_POINT):
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name + ".png")
    img.save(path)
    rel = os.path.relpath(path, os.path.join(PROJECT, "Assets", "_Project")).replace("\\", "/")
    g = guid_for(rel)
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(META.format(guid=g, ppu=ppu, sprite_id=g[:32], filter=filter_mode))


def clear_frames(folder):
    """
    ★★ 굽기 전에 폴더의 <b>옛 프레임을 지운다</b> (2026-08-20 신설).

    <b>왜 필요했나</b> — 장수가 <b>줄어드는</b> 수정을 하면 옛 파일이 그대로 남는다.
    실제로 베일에서 그랬다: 담배연기 칸을 빼서 12장 → 7장이 됐는데 폴더에는
    ``Char_Skill2_Right_07`` ~ ``11`` 이 남아 있어서 <b>빼려던 연기가 계속 재생됐다.</b>
    유니티 빌더는 폴더의 스프라이트를 <b>이름순으로 전부</b> 담으므로 이 잔재를 못 거른다.

    ⚠ <b>``.png`` 와 그 ``.meta`` 만</b> 지운다 — 폴더 자체와 폴더 meta 는 남긴다
      (폴더 guid 가 바뀌면 유니티가 폴더를 새로 만든 것으로 본다).
    """
    if not os.path.isdir(folder):
        return 0
    n = 0
    for f in os.listdir(folder):
        if f.endswith(".png") or f.endswith(".png.meta"):
            os.remove(os.path.join(folder, f))
            n += 1
    return n


def ensure_folder_meta(path):
    mp = path.rstrip("\\/") + ".meta"
    if os.path.exists(mp):
        return
    rel = os.path.relpath(path, os.path.join(PROJECT, "Assets", "_Project")).replace("\\", "/")
    with open(mp, "w", encoding="utf-8", newline="\n") as f:
        f.write(FOLDER_META.format(guid=guid_for(rel)))


# ---------------------------------------------------------------------------
# 몸통 · 이펙트
# ---------------------------------------------------------------------------


def write_skin_spec(dst_root, spec, made_by):
    """
    ★ 스킨 에셋의 «값» 칸을 원화 폴더에 **데이터로** 남긴다.

    <b>왜 파일로 내보내나</b> (유저 지시 2026-08-19: *"하드코딩 하지 말고 스킨 에셋 만들어서
    mcp 로 직접 넣어줘"*) — 스킨 에셋은 파이썬이 YAML 을 손으로 엮는 것이 아니라
    **유니티가 직접** 만든다(``Assets/_Project/Scripts/Editor/CharacterSkinBuilder.cs`` ·
    MCP ``execute_menu_item``). 그러면 guid 를 코드가 들고 있을 필요가 없다.

    그런데 「재생 속도」·「투사체를 쓰지 않는다」 같은 값은 **원화만 봐서는 알 수 없다.**
    캐릭터마다 C# 에 적으면 그게 하드코딩이므로 **원화 폴더 옆에 데이터로** 둔다 —
    분해 스크립트가 쓰고 유니티 빌더가 읽는다. 캐릭터가 늘어도 C# 은 안 바뀐다.
    """
    path = os.path.join(dst_root, SKIN_SPEC_NAME)
    lines = ["# %s 가 만든 파일 — 손으로 고치지 말 것." % made_by,
             "# Editor/CharacterSkinBuilder.cs 가 읽어서 스킨 에셋에 적는다.",
             "# 원화만 봐서는 알 수 없는 값만 여기 있다(폴더 구성은 폴더 이름이 정본).",
             ""]
    lines += ["%s=%s" % (k, v) for k, v in spec.items()]
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    return len(spec)
