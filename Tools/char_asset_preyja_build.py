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

SRC = r"C:\Project\라스트 생츄어리\리소스\asset\char_asset\Char_Asset_Preyja\Char"
PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DST = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset", "Char_Asset_Preyja", "Char")
DST_REL = "Assets/_Project/Art/Char_Asset/Char_Asset_Preyja/Char"

# 엘린 107px · 비기오르 111px @ PPU50 → 2.14 ~ 2.22 월드 유닛. 그 중간을 목표로 잡는다.
TARGET_BODY_UNITS = 2.18

ALPHA_SOLID = 128     # '몸'으로 볼 불투명도 — 흐린 잔광·그림자를 앵커 계산에서 뺀다
ALPHA_ANY = 32        # 프레임 경계(bbox)를 잡을 때 쓰는 문턱

# 원본 폴더명 → Unity 폴더명. 엘린·비기오르가 Walk 를 쓰므로 Move 를 Walk 로 맞춘다.
MOTION_RENAME = {"Move": "Walk"}

# 발 기준으로 정렬할 캐릭터 모션 (투사체는 발이 없다 — 따로 처리)
BODY_MOTIONS = ["Idle", "Walk", "MeleeAttack", "RangedAttack"]
FX_MOTIONS = ["Projectile", "ProjectileBurst"]


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
    """모션별·방향별 프레임을 모으고 Idle 은 복원해서 돌려준다."""
    result = {}    # (unity_motion, side) -> [Image]
    for motion in ["Idle", "MeleeAttack", "Move", "RangedAttack"]:
        for side in ["Left", "Right"]:
            raw = frames_of(motion, side)
            if motion == "Idle":
                row = stitch_row(raw)
                frames = split_characters(row, expected=3)
                print(f"  Idle {side}: 원본 {len(raw)}컷(깨짐) → 복원 {len(frames)}프레임")
            else:
                frames = raw
                print(f"  {motion} {side}: {len(frames)}프레임")
            result[(MOTION_RENAME.get(motion, motion), side)] = frames
    return result


def normalize_body(frames_by_key):
    """
    모든 캐릭터 프레임을 하나의 균일 캔버스에 발 기준으로 앉힌다.
    반환: {(motion, side): [Image]} · 캔버스 크기 · 아이들 몸 높이
    """
    metrics = {}
    for key, frames in frames_by_key.items():
        for i, f in enumerate(frames):
            t = trim(f)
            ax, ybot, ytop = body_anchor(t)
            metrics[(key, i)] = (t, ax, ybot, ytop)

    left_ext = max(ax for _, ax, _, _ in metrics.values())
    right_ext = max(t.width - ax for t, ax, _, _ in metrics.values())
    height = max(ybot + 1 for _, _, ybot, _ in metrics.values())

    half = max(left_ext, right_ext)          # 좌우 대칭 캔버스 — 피벗이 정확히 가운데여야 한다
    canvas = (half * 2, height)

    out = {}
    for key, frames in frames_by_key.items():
        made = []
        for i in range(len(frames)):
            t, ax, ybot, _ = metrics[(key, i)]
            img = Image.new("RGBA", canvas, (0, 0, 0, 0))
            img.alpha_composite(t, (half - ax, canvas[1] - 1 - ybot))
            made.append(img)
        out[key] = made

    idle_h = max(ybot - ytop + 1 for (k, _), (_, _, ybot, ytop) in metrics.items() if k[0] == "Idle")
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
