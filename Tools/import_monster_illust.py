# -*- coding: utf-8 -*-
"""몬스터 일러스트를 `Resources/Illust/` 로 임포트한다 (2026-08-15).

유저 지시: *"클릭하면 콘솔 로그 아래에 클릭한 객체의 일러스트가 연동되는 ui"* —
그 UI(`UnitPortraitPanel`)가 읽을 그림을 게임 쪽에 넣는 일이다.

원본 (볼트 `리소스/illust/`)
----------------------------
  · ``neutrality nomal monster/Tumor_spider_illust.png``  → 1001 종양 거미
  · ``neutrality nomal monster/Tumorling_illust.png``     → 1002 종양귀
  · ``neutrality nomal monster/Tumor mole_illust.png``    → 1003 종양 두더지
  · ``epic_boss/Carcinos_ilust.png``                      → 1004 카르시노스

⚠ 마지막 파일은 <b>원본 이름에 오타가 있다</b>(`ilust`). 표에는 `Carcinos_illust` 라고
  적혀 있으므로 <b>임포트하면서 이름을 바로잡는다</b> — 원본은 건드리지 않는다.
  (원본 이름을 고치면 볼트 git 이력에서 파일이 갈라지고, 표를 고치면 오타가 게임에 남는다.)

★ 캐릭터 초상화와 달리 <b>얼굴로 자르지 않는다</b>
--------------------------------------------------
`crop_illust_faces.py` 는 캐릭터 원화를 얼굴 중심으로 잘라 초상화 칸(210x284, 세로형)에
맞춘다. 몬스터는 <b>전신의 실루엣 자체가 정보</b>고(거미의 다리·두더지의 등가시), 얼굴이
어디인지도 종마다 다르다. 그래서 <b>비율을 유지한 채 통째로</b> 넣고, UI 쪽에서
`PreserveAspect` 로 칸에 맞춘다.

★ 다만 <b>크기는 줄인다</b> — 원본은 장당 2.5~3.6MB(≈1400x1120)다. 네 장이면 12MB 가
  `Resources/` 에 들어가고, `Resources` 는 <b>쓰든 안 쓰든 빌드에 통째로 들어간다</b>.
  긴 변 ``MAX_EDGE`` px 로 줄인다 — 초상화 칸이 그보다 작아 화면에서 차이가 없다.

⚠ <b>`textureType` 이 8(Sprite) 이어야 한다.</b> 0(Default) 이면
  `Resources.Load<Sprite>` 가 조용히 null 을 돌려주고 UI 는 폴백으로 넘어간다 —
  히스톤 초상화가 인게임 모션으로 뜨던 사고가 정확히 이것이었다(84-8절 ②).

⚠ 원본에서 읽어 Resources 로 쓰므로 몇 번을 돌려도 결과가 같다(멱등).

사용법:  py -3 Tools/import_monster_illust.py
"""

import hashlib
import os
import sys

from PIL import Image

from vault_path import VAULT, PROJECT

SRC_ROOT = os.path.join(VAULT, "리소스", "illust")
DST = os.path.join(PROJECT, "Assets", "_Project", "Resources", "Illust")

#: 긴 변을 이 픽셀로 줄인다. 초상화 칸은 이보다 훨씬 작다.
MAX_EDGE = 768

#: (원본 상대경로, 출력 이름 — 표의 `mon_illust` 값과 <b>정확히 같아야 한다</b>)
ILLUSTS = [
    (os.path.join("neutrality nomal monster", "Tumor_spider_illust.png"), "TumorSpider_illust"),
    (os.path.join("neutrality nomal monster", "Tumorling_illust.png"),    "Tumorling_illust"),
    (os.path.join("neutrality nomal monster", "Tumor mole_illust.png"),   "TumorMole_illust"),
    (os.path.join("epic_boss", "Carcinos_ilust.png"),                     "Carcinos_illust"),
    # ★ 웨이브 보스 3종 (2026-08-18) — 표 `wave_top_boss.illust` 와 이름이 같아야 한다.
    #   중립과 <b>같은 폴더·같은 규칙</b>이다(MonsterDefinitionSO.Illust 신설).
    (os.path.join("monster_cancer", "Dantalian_illust.png"),               "Dantalian_illust"),
    (os.path.join("monster_cancer", "Malphas_illust.png"),                 "Malphas_illust"),
    (os.path.join("monster_cancer", "Kasinoma_illust.png"),                "Kasinoma_illust"),
    # ★ 넥서스 (2026-08-18) — 몬스터는 아니지만 <b>같은 UI·같은 폴더</b>를 쓴다
    #   (NexusDefinitionSO.illustName). 이 스크립트가 「클릭 초상화용 그림 임포트」 담당이다.
    (os.path.join("Nexus", "Nexus_illust.png"),                            "Nexus_illust"),
    # ★ 중립 2종 (2026-08-19).
    #
    # ⚠⚠ <b>두 파일 다 이름이 표와 달랐다</b> — 여기서 <b>표 쪽 이름으로 바로잡는다</b>
    #   (출력 이름이 표의 `mon_illust` 와 같아야 게임이 찾는다):
    #     Anasakil_illust.webp → <b>Anisakil_illust</b>   (원화 시트 제목이 ANISAKIL 이다)
    #     Gordone_illust.png   → Gordone_illust           (표를 Gordonae → Gordone 로 고쳤다)
    #
    # ⚠ 아니사킬만 <b>.webp</b> 다(나머지는 전부 png). Pillow 가 읽어 png 로 저장하므로
    #   따로 변환할 것은 없다 — 다만 <b>원본 확장자를 그대로 적어야</b> 파일을 찾는다.
    # ⚠ 2026-08-19 — 원본이 `neutrality nomal monster/Anasakil_illust.webp` 에서
    #   `epic_boss/Anisakil_illust.png` 로 옮겨졌다(다른 에픽들과 같은 폴더로 정리된 듯).
    #   옛 경로는 이제 파일이 없다 — 새 경로로 갈아끼운다.
    (os.path.join("epic_boss", "Anisakil_illust.png"),                     "Anisakil_illust"),
    (os.path.join("neutrality nomal monster", "Gordone_illust.png"),        "Gordonae_illust"),
    # ★ 웨이브 보스 라린길(120004) · 중립 에픽 바리올라(1103) — 2026-08-19.
    #   둘 다 원본 이름이 이미 표(wave_top_boss.illust · neutrality_mon.mon_illust)와
    #   같아서 바로잡을 것이 없다.
    (os.path.join("monster_cancer", "Laryngeal_illust.png"),                "Laryngeal_illust"),
    (os.path.join("epic_boss", "Variola_illust.png"),                       "Variola_illust"),
]

#: 초상화용 임포트 설정. `Resources/Illust/illust_*.png.meta` 와 같은 값이다
#: (filterMode 1 = Bilinear · alignment 0 = Center · textureType 8 = Sprite).
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
    filterMode: 1
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
  spritePixelsToUnits: 100
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


def guid_for(rel):
    return hashlib.md5(("LastSanctuary/" + rel).encode("utf-8")).hexdigest()


def main():
    os.makedirs(DST, exist_ok=True)
    made = 0

    for rel_src, out_name in ILLUSTS:
        src = os.path.join(SRC_ROOT, rel_src)
        if not os.path.isfile(src):
            print("  ⚠ 원본 없음:", src)
            continue

        im = Image.open(src).convert("RGBA")
        w, h = im.size
        if max(w, h) > MAX_EDGE:
            s = MAX_EDGE / float(max(w, h))
            im = im.resize((max(1, int(w * s)), max(1, int(h * s))), Image.LANCZOS)

        out = os.path.join(DST, out_name + ".png")
        im.save(out)

        rel = os.path.relpath(out, os.path.join(PROJECT, "Assets", "_Project")).replace("\\", "/")
        g = guid_for(rel)
        with open(out + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write(META.format(guid=g, sprite_id=g[:32]))

        made += 1
        print(f"  {out_name:<20} {w}x{h} → {im.size[0]}x{im.size[1]}"
              f"  ({os.path.getsize(out) / 1024:.0f}KB)")

    print(f"\n일러스트 {made}장 → Resources/Illust")
    print("Unity 에서 Assets/Refresh 를 실행할 것.")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
