# -*- coding: utf-8 -*-
"""
프레이야(9003) 인게임 에셋을 Unity 로 들여오는 스크립트 (2026-08-11).

원본은 `리소스/asset/char_asset/Char_Asset_Preyja/` 이고 **건드리지 않는다** —
읽어서 `Assets/_Project/Art/Char_Asset/Char_Asset_Preyja/` 로 쓴다. 몇 번 돌려도
같은 결과가 나온다(guid 를 경로 md5 로 만든다 — gen_character_assets.py 와 같은 방식).

원본에서 발견한 문제 두 가지를 여기서 고친다.

1. ★ **Idle 프레임이 깨져 있었다.** 시트를 자를 때 한 행을 4등분해버려서
   - 한 컷에 캐릭터가 1.5명씩 들어가고(경계가 캐릭터를 관통),
   - 마지막 컷에는 시트의 행 라벨(`왼쪽` / `오른쪽` 글자)과 **옆 블록의 다른 포즈**까지 들어갔다.
   실제 Idle 은 **3프레임**이다. 원본 시트가 없으므로 4장을 이어붙여 행을 복원한 뒤
   세로 픽셀 밀도의 골(valley)에서 다시 잘라 캐릭터 3장만 남긴다.

2. **프레임마다 캔버스 크기가 제각각이고(120~197px) 발 위치가 안 맞았다.**
   그대로 넣으면 스프라이트를 갈아끼울 때마다 캐릭터가 화면에서 덜덜 떨린다.
   모든 프레임을 **발 기준으로 정렬한 균일 캔버스**에 다시 앉힌다
   (엘린 189x114 · 비기오르 139x131 처럼 모션 전체가 한 크기인 관례를 따른다).

3. **비율(화면 크기)이 안 맞았다.** 프레이야 원화가 엘린·비기오르보다 크게 그려져 있어서
   PPU 50 으로 넣으면 20% 넘게 커 보인다. **픽셀을 리샘플링하지 않고 PPU 만 조정**해
   맞춘다(확대·축소하면 픽셀아트가 뭉개진다). PPU 는 스프라이트마다 다를 수 있다.

4. ★ **원본 `Move` 를 쓰지 않고 Idle 에서 걷기를 만들어낸다** (유저 지시 2026-08-11:
   "이동 모습일때 너무 어색하니까 idle 상태 스프라이트 재해석 해서 붙여가지고 다시 만들어줘.
   다른 캐릭터들 스킨이랑 일관성을 가지도록").

   원본 `Move` 8장은 **웅크려 창을 앞으로 뻗은 돌진 포즈**라 직립 Idle 과 실루엣이 완전히 다르다.
   엘린·비기오르는 **Walk 가 Idle 과 같은 직립 실루엣이고 다리만 움직인다**(둘 다 프레임 사이
   위쪽 경계가 1~3px 만 흔들리고 발 위치는 고정). 프레이야만 걸을 때 다른 사람이 되는 셈이라
   서 있다가 움직이는 순간 튀어 보였다.

   그래서 Idle 3프레임을 **재해석해 6프레임 걷기 주기**를 합성한다 — 자세한 규칙은
   `synth_walk_from_idle` 주석 참조. 원본 `Move` 원화는 손대지 않고 남아 있으므로
   나중에 "돌진/질주" 같은 별도 모션으로 쓸 수 있다.

남는 한 가지 — RangedAttack 은 오른쪽이 4프레임, **왼쪽이 3프레임**이다. 원본 시트가
없어 잃어버린 것인지 애초에 3장인지 확정할 수 없다(좌우 원화가 서로의 거울상이 아니므로
오른쪽 프레임을 뒤집어 끼우면 날개 그림이 튄다). 그대로 3프레임으로 둔다 —
`CharacterSkinSO` 가 방향별로 길이를 따로 재므로 재생에는 문제가 없다.
"""

import os
import hashlib
import shutil

import numpy as np
from PIL import Image

SRC = r"C:\Project\Last-Sanctuary-Vault\리소스\asset\char_asset\Char_Asset_Preyja\Char"
PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DST = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset", "Char_Asset_Preyja", "Char")
DST_REL = "Assets/_Project/Art/Char_Asset/Char_Asset_Preyja/Char"

# 엘린 107px · 비기오르 111px @ PPU50 → 2.14 ~ 2.22 월드 유닛. 그 중간을 목표로 잡는다.
TARGET_BODY_UNITS = 2.18

ALPHA_SOLID = 128     # '몸'으로 볼 불투명도 — 흐린 잔광·그림자를 앵커 계산에서 뺀다
ALPHA_ANY = 32        # 프레임 경계(bbox)를 잡을 때 쓰는 문턱

# 원본에서 그대로 가져오는 캐릭터 모션. `Move` 는 일부러 빠져 있다 —
# 걷기는 Idle 에서 합성한다(파일 상단 4번).
SOURCE_BODY_MOTIONS = ["Idle", "MeleeAttack", "RangedAttack"]
FX_MOTIONS = ["Projectile", "ProjectileBurst"]

# ---- 걷기 합성 파라미터 (전부 여기 모아둔다 — 어색하면 이 숫자만 만진다) ----

WALK_FRAMES = 6                 # 두 걸음 한 주기. 비기오르 6 · 엘린 5 와 같은 급

# 어느 Idle 프레임을 밑그림으로 쓸지. ⚠ **핑퐁으로 돌린다** — [0,1,2,0,1,2] 로 두면
# 자락 흔들림이 0 이 되는 두 접지 프레임(0·3)이 밑그림까지 같아져 **완전히 같은 그림**이
# 두 장 나온다(실제로 그렇게 나왔다). 밑그림을 어긋나게 두면 여섯 장이 전부 달라진다.
WALK_POSE_ORDER = [0, 1, 2, 1, 0, 2]

WALK_LEAN_PX = 3                # 진행 방향으로 상체를 기울이는 양(머리 기준). 발은 0
WALK_BOB_PX = 2                 # 한 걸음마다 몸이 뜨는 높이
WALK_SWING_PX = 3               # 아랫도리(다리·로브)가 앞뒤로 흔들리는 폭
WALK_SWING_BAND = 0.38          # 아랫도리로 볼 높이 비율 (발끝에서 이 비율까지)

# ⚠ 좌우 흔들림(sway)은 넣지 않는다 — **몸 전체를 같은 양만큼 밀면 아무 일도 안 일어난다.**
#   정규화가 발 기준점을 캔버스 가운데에 맞추므로 균일 이동은 그 단계에서 정확히 상쇄된다.
#   체중 이동을 표현하려면 위아래가 서로 다르게 움직여야 하고, 그 역할은 위의 swing 이 한다.


# ======================================================================
# .meta 생성
# ======================================================================

def guid_for(rel_path):
    """gen_character_assets.py 와 같은 규칙 — 경로 md5. 다시 돌려도 guid 가 안 바뀐다."""
    return hashlib.md5(("LastSanctuary/" + rel_path).encode("utf-8")).hexdigest()


def png_meta(guid, ppu, pivot_bottom):
    """
    엘린 프레임의 .meta 를 그대로 본뜬 것. 중요한 값 네 개만 다르다.
      textureType: 8   Sprite  — Default(0) 로 두면 Resources.Load<Sprite> 가 null 이다(33-7절)
      filterMode: 0    Point   — 픽셀아트라 보간하면 흐려진다
      alignment 7 / pivot (0.5, 0)  발밑 피벗 (캐릭터) · alignment 0 / (0.5,0.5) 중심 (투사체)
      spritePixelsToUnits            화면 크기를 맞추는 값
    """
    alignment = 7 if pivot_bottom else 0
    pivot_y = 0 if pivot_bottom else 0.5
    platforms = ""
    for target, compress in (("DefaultTexturePlatform", 0), ("Standalone", 1),
                             ("Android", 1), ("WebGL", 1)):
        platforms += (
            "  - serializedVersion: 4\n"
            f"    buildTarget: {target}\n"
            "    maxTextureSize: 2048\n"
            "    resizeAlgorithm: 0\n"
            "    textureFormat: -1\n"
            f"    textureCompression: {compress}\n"
            "    compressionQuality: 50\n"
            "    crunchedCompression: 0\n"
            "    allowsAlphaSplitting: 0\n"
            "    overridden: 0\n"
            "    ignorePlatformSupport: 0\n"
            "    androidETC2FallbackOverride: 0\n"
            "    forceMaximumCompressionQuality_BC6H_BC7: 0\n")
    return f"""fileFormatVersion: 2
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
  alignment: {alignment}
  spritePivot: {{x: 0.5, y: {pivot_y}}}
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
{platforms}  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: {guid[:16]}0000000000000000
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


FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


# ======================================================================
# 이미지 유틸
# ======================================================================

def load(path):
    return Image.open(path).convert("RGBA")


def frames_of(motion, side):
    """원본 폴더에서 한 방향의 프레임을 번호 순으로 읽는다."""
    folder = os.path.join(SRC, motion)
    names = sorted(f for f in os.listdir(folder)
                   if f.endswith(".png") and (f"_{side}_" in f if side else True))
    return [load(os.path.join(folder, n)) for n in names]


def stitch_row(images):
    """
    잘린 컷들을 원래 시트 행으로 되돌린다.

    컷은 겹치지 않는 인접 슬라이스였다(총 폭 = 컷 폭의 합). 아래를 맞춰 이어붙이면
    행이 그대로 복원된다 — 실제로 이렇게 붙였을 때 캐릭터 3명 + 라벨 + 옆 블록 한 컷이
    끊김 없이 나왔다.
    """
    h = max(i.height for i in images)
    w = sum(i.width for i in images)
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    x = 0
    for im in images:
        out.alpha_composite(im, (x, h - im.height))   # 바닥 정렬
        x += im.width
    return out


def column_density(img, alpha=ALPHA_SOLID):
    return (np.array(img)[:, :, 3] > alpha).sum(axis=0)


def split_characters(row, expected):
    """
    복원한 행에서 캐릭터 `expected` 명을 잘라낸다.

    ⚠ 완전히 빈 열로는 못 나눈다 — 오른쪽 행에는 바닥 그림자가 행 전체에 2px 로
    깔려 있어 밀도가 0 이 되는 열이 없다. 그래서 **불투명 픽셀(alpha>128) 밀도의 골**을
    찾는다. 캐릭터 몸통은 100 이 넘고 사이 골은 10 아래로 떨어져 구분이 확실하다.
    """
    col = column_density(row)
    solid = col > 12                      # 몸통이 있는 열
    spans = []
    start = None
    for x, s in enumerate(solid):
        if s and start is None:
            start = x
        elif not s and start is not None:
            spans.append((start, x))
            start = None
    if start is not None:
        spans.append((start, len(solid)))

    # 너무 좁은 덩어리(라벨 글자·구분선 조각)는 캐릭터가 아니다
    spans = [s for s in spans if s[1] - s[0] >= 40]
    if len(spans) < expected:
        raise RuntimeError(f"캐릭터 {expected}명을 찾지 못했습니다: {spans}")

    # 앞쪽 `expected` 개만 쓴다 — 뒤에 붙은 라벨·옆 블록은 버린다
    spans = spans[:expected]

    out = []
    for i, (x0, x1) in enumerate(spans):
        # 이웃과의 경계는 두 덩어리 사이 골의 가운데로 잡아 잘린 픽셀이 안 남게 한다
        left = 0 if i == 0 else (spans[i - 1][1] + x0) // 2
        right = row.width if i == len(spans) - 1 else (x1 + spans[i + 1][0]) // 2
        if i == len(spans) - 1:
            right = min(row.width, x1 + 6)      # 마지막은 라벨 쪽으로 넘어가지 않게 붙여 자른다
        out.append(row.crop((left, 0, right, row.height)))
    return out


def body_anchor(img):
    """
    발 기준점 (x, y). y 는 실루엣 맨 아래, x 는 **아래쪽 20% 띠의 불투명 픽셀 x 중심**.

    왜 bbox 중심이 아닌가 — 창을 앞으로 뻗으면 bbox 가 그쪽으로 늘어나서, bbox 중심으로
    맞추면 창이 움직일 때마다 몸이 반대로 밀린다. 발은 프레임마다 거의 안 움직이므로
    발을 기준으로 맞춰야 재생 중에 몸이 안 떨린다.
    """
    a = np.array(img)[:, :, 3]
    solid = a > ALPHA_SOLID
    rows = np.nonzero(solid.sum(axis=1) >= 2)[0]
    if len(rows) == 0:
        rows = np.nonzero((a > ALPHA_ANY).sum(axis=1) >= 1)[0]
        solid = a > ALPHA_ANY
    y_bottom = int(rows.max())
    y_top = int(rows.min())
    band = max(1, int((y_bottom - y_top + 1) * 0.20))
    sub = solid[y_bottom - band + 1: y_bottom + 1, :]
    xs = np.nonzero(sub.any(axis=0))[0]
    if len(xs) == 0:
        xs = np.nonzero(solid.any(axis=0))[0]
    x_anchor = int(round(float(xs.mean())))
    return x_anchor, y_bottom, y_top


def row_shift(img, shifts):
    """
    행마다 정수 픽셀로 좌우로 밀어 새 이미지를 만든다 (기울이기 · 흔들기의 공통 수단).

    <b>정수 픽셀만 민다</b> — 실수 좌표로 회전·전단하면 픽셀아트가 보간되어 뭉개진다.
    행 단위 정수 이동이면 원본 픽셀이 그대로 살아있다.
    """
    w, h = img.size
    lo, hi = min(shifts), max(shifts)
    out = Image.new("RGBA", (w + (hi - lo), h), (0, 0, 0, 0))
    for y in range(h):
        out.paste(img.crop((0, y, w, y + 1)), (shifts[y] - lo, y))
    return out


def synth_walk_from_idle(idle_frames, facing_right):
    """
    ★ Idle 프레임을 재해석해 걷기 한 주기를 만든다 (유저 지시 — 파일 상단 4번).

    <b>왜 그리지 않고 변형하는가</b> — 다리를 새로 그릴 수단이 없다. 대신 **로브 자락과
    상체를 움직여** 걷는 것처럼 읽히게 한다. 이 캐릭터는 긴 로브를 입고 있어서
    다리 대신 자락이 흔들리는 편이 오히려 자연스럽다.

    한 프레임에 세 가지를 겹친다. 전부 <b>발끝을 고정</b>하고 위로 갈수록 커지므로
    발이 미끄러지지 않는다(엘린·비기오르 걷기도 아래 경계가 고정이고 위만 흔들린다).

    1. **기울임(lean)** — 진행 방향으로 상체를 기울인다. 머리에서 최대, 발에서 0.
       걷기와 서기를 가르는 가장 강한 신호다. 주기 내내 유지된다.
    2. **자락 흔들림(swing)** — 아랫도리(발끝~38% 높이)만 앞뒤로 흔든다.
       발끝과 허리에서 0, 그 중간에서 최대라 <b>자락이 앞뒤로 나부끼는</b> 모양이 된다.
       한 주기에 앞·뒤 한 번씩(사인) — 이것이 두 걸음이다.
    3. **바운스(bob)와 흔들(sway)** — 한 걸음마다 몸이 살짝 뜨고 좌우로 체중이 옮겨간다.

    ⚠️ 밑그림은 Idle 3장을 돌려쓴다. 한 장만 쓰면 로브 주름·날개가 완전히 굳어서
       변형만 도는 것이 눈에 보인다.
    """
    import math

    sign = 1 if facing_right else -1
    out = []
    for i in range(WALK_FRAMES):
        src = trim(idle_frames[WALK_POSE_ORDER[i % len(WALK_POSE_ORDER)] % len(idle_frames)])
        w, h = src.size
        phase = 2 * math.pi * i / WALK_FRAMES

        swing = sign * WALK_SWING_PX * math.sin(phase)
        # |sin| 이라 한 주기에 두 번 솟는다 = 걸음마다 한 번
        bob = int(round(WALK_BOB_PX * abs(math.sin(phase))))

        shifts = []
        for y in range(h):
            t = (h - 1 - y) / max(1, h - 1)          # 0=발끝, 1=머리끝
            lean = sign * WALK_LEAN_PX * t
            # 아랫도리에서만 살아나는 봉우리 — 발끝(0)과 밴드 끝에서 0, 가운데서 1
            band = math.sin(math.pi * t / WALK_SWING_BAND) if t < WALK_SWING_BAND else 0.0
            shifts.append(int(round(lean + swing * band)))

        # bob 은 이미지에 여백으로 넣지 않는다 — 정규화가 실루엣 아래를 캔버스 바닥에
        # 맞추므로 여백을 넣어봐야 그대로 상쇄된다. 배치 단계에 넘길 '들어올림' 값으로 둔다.
        out.append((row_shift(src, shifts), bob))
    return out


def trim(img):
    """투명 여백을 잘라낸다 (알파 문턱 기준)."""
    a = np.array(img)[:, :, 3]
    ys, xs = np.nonzero(a > ALPHA_ANY)
    if len(xs) == 0:
        return img
    return img.crop((int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1))


# ======================================================================
# 본체
# ======================================================================

def collect_body_frames():
    """
    모션별·방향별 프레임을 모은다. 값은 `(이미지, 들어올림px)` 목록이다 —
    들어올림은 걷기 바운스에만 쓰이고 나머지는 0 이다.

    Idle 은 깨진 원본을 복원하고, Walk 는 그 Idle 에서 합성한다.
    """
    result = {}
    for motion in SOURCE_BODY_MOTIONS:
        for side in ["Left", "Right"]:
            raw = frames_of(motion, side)
            if motion == "Idle":
                frames = split_characters(stitch_row(raw), expected=3)
                print(f"  Idle {side}: 원본 {len(raw)}컷(깨짐) → 복원 {len(frames)}프레임")
            else:
                frames = raw
                print(f"  {motion} {side}: {len(frames)}프레임")
            result[(motion, side)] = [(f, 0) for f in frames]

    # ★ 걷기는 원본 Move 가 아니라 Idle 에서 만든다 (파일 상단 4번)
    for side in ["Left", "Right"]:
        idle = [f for f, _ in result[("Idle", side)]]
        result[("Walk", side)] = synth_walk_from_idle(idle, facing_right=(side == "Right"))
        print(f"  Walk {side}: Idle {len(idle)}프레임 → 합성 {WALK_FRAMES}프레임 "
              f"(원본 Move 는 쓰지 않는다)")
    return result


def normalize_body(frames_by_key):
    """
    모든 캐릭터 프레임을 하나의 균일 캔버스에 발 기준으로 앉힌다.
    반환: {(motion, side): [Image]} · 캔버스 크기 · 대기 자세 몸 높이
    """
    metrics = {}
    for key, frames in frames_by_key.items():
        for i, (f, lift) in enumerate(frames):
            t = trim(f)
            ax, ybot, ytop = body_anchor(t)
            metrics[(key, i)] = (t, ax, ybot, ytop, lift)

    left_ext = max(ax for _, ax, _, _, _ in metrics.values())
    right_ext = max(t.width - ax for t, ax, _, _, _ in metrics.values())
    # 들어올린 프레임은 그만큼 위가 더 필요하다
    height = max(ybot + 1 + lift for _, _, ybot, _, lift in metrics.values())

    half = max(left_ext, right_ext)          # 좌우 대칭 캔버스 — 피벗이 정확히 가운데여야 한다
    canvas = (half * 2, height)

    out = {}
    for key, frames in frames_by_key.items():
        made = []
        for i in range(len(frames)):
            t, ax, ybot, _, lift = metrics[(key, i)]
            img = Image.new("RGBA", canvas, (0, 0, 0, 0))
            img.alpha_composite(t, (half - ax, canvas[1] - 1 - ybot - lift))
            made.append(img)
        out[key] = made

    idle_h = max(ybot - ytop + 1
                 for (k, _), (_, _, ybot, ytop, _) in metrics.items() if k[0] == "Idle")
    return out, canvas, idle_h


def normalize_fx(motion):
    """
    투사체·착탄 프레임 — 발이 없으므로 **중심 정렬**한다.
    피벗이 중심이어야 `CombatProjectileFx.AimAt` 의 회전이 탄환 가운데를 축으로 돈다
    (기존 Fx 스프라이트도 alignment 0 / pivot (0.5,0.5) 다).
    """
    folder = os.path.join(SRC, motion)
    names = sorted(f for f in os.listdir(folder) if f.endswith(".png"))
    if motion == "Projectile":
        # 오른쪽(+X) 프레임만 쓴다 — 왼쪽 프레임은 필요 없다.
        # 탄환은 진행 방향으로 회전시켜 그리므로(AimAt) 방향별 원화가 있으면 두 번 돌아간다.
        names = [n for n in names if "_Right_" in n]
    trimmed = [trim(load(os.path.join(folder, n))) for n in names]
    w = max(t.width for t in trimmed)
    h = max(t.height for t in trimmed)
    if w % 2:
        w += 1
    if h % 2:
        h += 1
    out = []
    for t in trimmed:
        img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        img.alpha_composite(t, ((w - t.width) // 2, (h - t.height) // 2))
        out.append(img)
    print(f"  {motion}: {len(out)}프레임 → {w}x{h} (중심 정렬)")
    return out


def write_frames(motion, side, frames, ppu, pivot_bottom):
    """PNG + .meta 를 쓰고 (상대경로, guid) 목록을 돌려준다."""
    folder = os.path.join(DST, motion)
    os.makedirs(folder, exist_ok=True)
    meta = folder + ".meta"
    if not os.path.exists(meta):
        with open(meta, "w", encoding="utf-8", newline="\n") as f:
            f.write(FOLDER_META.format(guid=guid_for(f"{DST_REL}/{motion}")))

    made = []
    for i, img in enumerate(frames):
        stem = f"Char_{motion}_{side}_{i:02d}" if side else f"Char_{motion}_{i:02d}"
        rel = f"{DST_REL}/{motion}/{stem}.png"
        path = os.path.join(folder, stem + ".png")
        img.save(path)
        g = guid_for(rel)
        with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write(png_meta(g, ppu, pivot_bottom))
        made.append((rel, g))
    return made


def ensure_parent_folder_metas():
    """Char_Asset_Preyja · Char_Asset_Preyja/Char 폴더 .meta"""
    for rel, path in ((f"{DST_REL}", DST),
                      (f"{DST_REL}".rsplit("/", 1)[0], os.path.dirname(DST))):
        os.makedirs(path, exist_ok=True)
        meta = path + ".meta"
        if not os.path.exists(meta):
            with open(meta, "w", encoding="utf-8", newline="\n") as f:
                f.write(FOLDER_META.format(guid=guid_for(rel)))


def main():
    if os.path.isdir(DST):
        shutil.rmtree(DST)          # 멱등하게 — 남은 프레임이 섞이지 않게 지우고 다시 쓴다
    ensure_parent_folder_metas()

    print("원본 프레임 수집:")
    body = collect_body_frames()
    frames, canvas, idle_h = normalize_body(body)

    ppu = int(round(idle_h / TARGET_BODY_UNITS))
    print(f"\n캔버스 {canvas[0]}x{canvas[1]} · 대기 자세 몸 높이 {idle_h}px")
    print(f"→ PPU {ppu} (엘린 107px@50 = 2.14유닛 / 프레이야 {idle_h}px@{ppu} = "
          f"{idle_h / ppu:.2f}유닛)")

    total = 0
    for (motion, side), imgs in sorted(frames.items()):
        total += len(write_frames(motion, side, imgs, ppu, pivot_bottom=True))

    for motion in FX_MOTIONS:
        imgs = normalize_fx(motion)
        total += len(write_frames(motion, "", imgs, ppu=50, pivot_bottom=False))

    # guid 목록 파일을 따로 남기지 않는다 — guid 가 경로 md5 라
    # 스킨 에셋 생성기(gen_skin_assets.py)가 같은 규칙으로 스스로 계산한다.
    # Assets/ 안에 중간 산출물을 두면 Unity 가 그것까지 임포트해 .meta 를 요구한다.
    print(f"\n프레임 {total}장 기록 완료 → {DST_REL}")


if __name__ == "__main__":
    main()
