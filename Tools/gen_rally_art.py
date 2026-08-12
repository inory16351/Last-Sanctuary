# -*- coding: utf-8 -*-
"""
집결지 표시용 임시 아트 2장을 만든다 (멱등 — 몇 번을 돌려도 결과가 같다).

  Assets/_Project/Resources/UI/RallyFlag.png          32x64  (PPU 32 → 가로 1 · 세로 2 타일)
  Assets/_Project/Resources/UI/RallyRangeOutline.png  256x256 (테두리만 있는 원)

깃발은 **유저가 나중에 직접 그린 PNG 로 교체할 임시 아트**다. 교체할 때 지켜야 할 규격:
  - 크기 비율 1:2 (가로 1타일 · 세로 2타일). PPU 는 `가로 픽셀 수` 와 같게 둘 것.
  - 피벗은 **하단 중앙**(alignment 7) — 깃대가 박히는 지점이 집결지 정중앙 칸이 된다.
  - 파일 경로/이름을 그대로 두면 코드 수정 없이 바로 반영된다
    (`RallyPointService` 가 `Resources.Load<Sprite>("UI/RallyFlag")` 로 읽는다).
콜라이더는 코드가 스프라이트 바운즈에서 매번 다시 계산하므로, 크기가 달라져도 자동으로 맞는다.
"""
from __future__ import annotations

import os
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "_Project", "Resources", "UI")

# 깃발 색 — 어두운 맵 위에서 눈에 띄되 UI 의 청록 강조색과 싸우지 않는 호박색
BANNER = (232, 194, 90, 255)
BANNER_DARK = (176, 138, 52, 255)
POLE = (214, 210, 198, 255)
POLE_DARK = (120, 116, 106, 255)
OUTLINE = (26, 22, 16, 255)
SHADOW = (0, 0, 0, 110)

FLAG_W, FLAG_H = 32, 64
SUPER = 4          # 4배로 그린 뒤 축소 = 계단현상 완화


def build_flag() -> Image.Image:
    """깃대(왼쪽) + 삼각 깃발(오른쪽) + 접지 그림자. 하단 중앙이 깃대 밑동이다."""
    w, h = FLAG_W * SUPER, FLAG_H * SUPER
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    cx = w // 2                     # 깃대 중심 = 캔버스 가로 중앙(피벗과 같은 x)
    pole_half = int(1.6 * SUPER)
    top = int(3 * SUPER)
    ground = h - int(3 * SUPER)

    # 접지 그림자 — 밑동이 바닥에 붙어 보이게
    d.ellipse([cx - 7 * SUPER, ground - int(2.5 * SUPER),
               cx + 7 * SUPER, ground + int(2.5 * SUPER)], fill=SHADOW)

    # 깃발(삼각) — 깃대 오른쪽으로 펄럭인다
    banner_top = top + int(1.5 * SUPER)
    banner_bot = top + int(17 * SUPER)
    tip_x = cx + int(15 * SUPER)
    d.polygon([(cx, banner_top), (tip_x, (banner_top + banner_bot) // 2), (cx, banner_bot)],
              fill=BANNER, outline=OUTLINE, width=SUPER)
    # 아래쪽 절반만 어둡게 — 천이 접힌 느낌
    d.polygon([(cx, (banner_top + banner_bot) // 2),
               (tip_x, (banner_top + banner_bot) // 2), (cx, banner_bot)], fill=BANNER_DARK)
    d.polygon([(cx, banner_top), (tip_x, (banner_top + banner_bot) // 2), (cx, banner_bot)],
              outline=OUTLINE, width=SUPER)

    # 깃대 — 깃발보다 나중에 그려 깃발 밑동을 덮는다
    d.rectangle([cx - pole_half - SUPER, top - SUPER, cx + pole_half + SUPER, ground], fill=OUTLINE)
    d.rectangle([cx - pole_half, top, cx + pole_half, ground - SUPER], fill=POLE)
    d.rectangle([cx, top, cx + pole_half, ground - SUPER], fill=POLE_DARK)

    # 깃대 꼭대기 구슬
    d.ellipse([cx - 3 * SUPER, top - int(4.5 * SUPER), cx + 3 * SUPER, top + int(1.5 * SUPER)],
              fill=BANNER, outline=OUTLINE, width=SUPER)

    return img.resize((FLAG_W, FLAG_H), Image.LANCZOS)


RANGE_SIZE = 256
RING_THICKNESS = 3.0        # 256px 기준 — UI 에서 늘려 그리므로 얇게 잡는다


def build_range_outline() -> Image.Image:
    """가운데가 비고 테두리만 있는 원. 색은 코드에서 tint 하므로 흰색으로 만든다."""
    s = RANGE_SIZE * SUPER
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    pad = int(RING_THICKNESS * SUPER)      # 테두리가 캔버스 밖으로 잘리지 않게
    d.ellipse([pad, pad, s - pad, s - pad], outline=(255, 255, 255, 255),
              width=int(RING_THICKNESS * SUPER))
    return img.resize((RANGE_SIZE, RANGE_SIZE), Image.LANCZOS)


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
  spriteMeshType: 0
  alignment: {alignment}
  spritePivot: {{x: {pivot_x}, y: {pivot_y}}}
  spritePixelsToUnits: {ppu}
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
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
    customData:{space}
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:{space}
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:{space}
  pSDRemoveMatte: 0
  userData:{space}
  assetBundleName:{space}
  assetBundleVariant:{space}
"""


def write_meta(png_path: str, guid: str, ppu: int, alignment: int,
               pivot: tuple[float, float], filter_mode: int) -> None:
    """
    .meta 를 **guid 고정**으로 직접 쓴다 — 파일을 다시 만들어도 guid 가 유지돼야
    씬/에셋의 참조가 안 끊긴다(진행상황 8절 1번과 같은 이유).
    이미 있으면 건드리지 않는다 — 유저가 임포트 설정을 손봤을 수 있다.
    """
    meta_path = png_path + ".meta"
    if os.path.exists(meta_path):
        print(f"  meta 유지 (이미 있음): {os.path.basename(meta_path)}")
        return
    text = META.format(guid=guid, ppu=ppu, alignment=alignment,
                       pivot_x=pivot[0], pivot_y=pivot[1], filter=filter_mode, space=" ")
    with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print(f"  meta 생성: {os.path.basename(meta_path)}")


def main() -> None:
    os.makedirs(OUT_DIR, exist_ok=True)

    flag_path = os.path.join(OUT_DIR, "RallyFlag.png")
    build_flag().save(flag_path)
    print(f"깃발: {flag_path} ({FLAG_W}x{FLAG_H})")
    # alignment 7 = Custom? 아니다 — 7 은 BottomCenter. 이 프로젝트의 캐릭터 스킨과 같은 값.
    write_meta(flag_path, "9c1ab2f0d4e7451aab7e0c1d2f3a4b50",
               ppu=FLAG_W, alignment=7, pivot=(0.5, 0), filter_mode=0)

    ring_path = os.path.join(OUT_DIR, "RallyRangeOutline.png")
    build_range_outline().save(ring_path)
    print(f"범위 테두리: {ring_path} ({RANGE_SIZE}x{RANGE_SIZE})")
    write_meta(ring_path, "9c1ab2f0d4e7451aab7e0c1d2f3a4b51",
               ppu=100, alignment=0, pivot=(0.5, 0.5), filter_mode=1)


if __name__ == "__main__":
    main()
