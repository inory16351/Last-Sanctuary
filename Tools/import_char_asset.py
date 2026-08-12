# -*- coding: utf-8 -*-
"""볼트의 원화 폴더를 Unity `Art/Char_Asset/` 으로 임포트한다 (프레임 PNG + .meta).

**테이블이 정본이다**(유저 확정 2026-08-12) — 그래서 이 스크립트의 인자는
`캐릭터 테이블` / `웨이브 몬스터 테이블` 의 **`ingame_asset` 값**이고, 그 이름 그대로
Unity 폴더를 만든다. 볼트 쪽 폴더 이름이 표와 다르면(예: `Char_Asset_kim`) **표 이름으로
바꿔 들여온다** — 표가 위이므로 게임 쪽이 표를 따라간다.

⚠ **프레임 파일 이름도 표 이름으로 바꾼다.** 원화의 `Char_kim_Idle_Left_00.png` 를
`Char_Idle_Left_00.png` 로 정규화한다 — `Tools/gen_skin_assets.py` 의 `frame_guids()` 가
`_Left_` / `_Right_` 만 보고 번호순으로 정렬하므로 접두사는 자유롭지만, 기존 임포트본
(프레이야 = `Char_Idle_Left_00.png`)과 규칙을 맞춰 두면 스킨 생성 코드가 캐릭터마다
갈라지지 않는다.

⚠ .meta 의 guid 는 **경로에서 결정적으로** 만든다(다시 돌려도 같은 guid → 참조가 살아있다).
  이 프로젝트의 다른 생성 스크립트와 같은 규칙이다.

⚠ 임포트 설정은 **기존 캐릭터 프레임과 동일**하게 맞춘다:
  `textureType 8`(Sprite) · `spriteMode 1`(Single) · `alignment 7` + pivot (0.5, 0)(발밑 기준)
  · `filterMode 0`(Point) · `alphaIsTransparency 1`.
  피벗이 발밑이어야 다른 유닛과 접지선이 어긋나지 않는다(진행상황 25-1·25-4절 —
  Angel 이 공격할 때 발이 20px 떠 있던 그 버그의 원인이 이 설정이었다).
"""
import os
import sys
import shutil
import hashlib
import re

VAULT_ASSET = r'C:\Project\Last-Sanctuary-Vault\리소스\asset'
PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, 'Assets', '_Project', 'Art', 'Char_Asset')

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

# 기존 캐릭터 프레임(Char_Asset_Preyja)의 설정을 그대로 옮긴 것.
PNG_META = """fileFormatVersion: 2
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
  spriteMeshType: 0
  alignment: 7
  spritePivot: {{x: 0.5, y: 0}}
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


def guid_for(key):
    return hashlib.md5(('LastSanctuary/' + key).encode('utf-8')).hexdigest()


def ensure_folder(path, rel):
    os.makedirs(path, exist_ok=True)
    meta = path + '.meta'
    if not os.path.exists(meta):
        with open(meta, 'w', encoding='utf-8', newline='\n') as f:
            f.write(FOLDER_META.format(guid=guid_for(rel)))


def normalize(name, motion):
    """`Char_kim_Idle_Left_00.png` / `Boss_Idle_Left_00.png` → `Char_<Motion>_<Side>_NN.png`."""
    m = re.search(r'_(Left|Right)_(\d+)\.png$', name)
    if m:
        return 'Char_%s_%s_%s.png' % (motion, m.group(1), m.group(2))
    # ⚠ 방향이 없는 프레임(Fx 등)은 <b>번호만 살리면 이름이 겹친다</b> —
    #   단탈리온의 `Boss_BeamFx_00` 과 `Boss_ShockwaveFx_00` 이 둘 다 `Char_Fx_00` 이 되어
    #   한 장이 조용히 덮어써졌다(실제로 3장 중 2장만 들어왔다). 그래서 첫 토큰만 떼고
    #   나머지를 그대로 살려 유일성을 보장한다.
    stem = name[:-4]
    rest = stem.split('_', 1)[1] if '_' in stem else stem
    return 'Char_%s_%s.png' % (motion, rest)


def import_asset(src_root, table_name, ppu, motion_map):
    """
    src_root      볼트의 원화 폴더 (…/Char)
    table_name    표의 ingame_asset 값 — Unity 폴더 이름이 된다
    motion_map    {원화 폴더명: Unity 모션명}. 값이 None 이면 건너뛴다.
    """
    dest_char = os.path.join(ART, table_name, 'Char')
    ensure_folder(os.path.join(ART, table_name), 'Art/Char_Asset/%s' % table_name)
    ensure_folder(dest_char, 'Art/Char_Asset/%s/Char' % table_name)

    total = 0
    for src_motion, dst_motion in motion_map.items():
        if dst_motion is None:
            continue
        src = os.path.join(src_root, src_motion)
        if not os.path.isdir(src):
            print('  ! 원화 폴더 없음:', src)
            continue

        dst = os.path.join(dest_char, dst_motion)
        ensure_folder(dst, 'Art/Char_Asset/%s/Char/%s' % (table_name, dst_motion))

        n = 0
        for f in sorted(os.listdir(src)):
            if not f.endswith('.png'):
                continue
            out_name = normalize(f, dst_motion)
            shutil.copyfile(os.path.join(src, f), os.path.join(dst, out_name))
            rel = 'Art/Char_Asset/%s/Char/%s/%s' % (table_name, dst_motion, out_name)
            with open(os.path.join(dst, out_name + '.meta'), 'w',
                      encoding='utf-8', newline='\n') as mf:
                mf.write(PNG_META.format(guid=guid_for(rel), ppu=ppu))
            n += 1
        print('  %s/%s: %d프레임' % (table_name, dst_motion, n))
        total += n
    return total


# ---------------------------------------------------------------------------
# 표에 적힌 두 에셋 (`ingame_asset` 컬럼)
# ---------------------------------------------------------------------------

def piolo():
    """
    캐릭터 9004 피올로. 표의 `ingame_asset` 칸이 비어 있었고(54-2절), 볼트에는
    쓰이지 않은 캐릭터 원화 `Char_Asset_kim` 이 남아 있었다.

    ★ 그 원화가 피올로인 근거 — **회복(Heal) 모션을 가진 유일한 캐릭터 원화**다.
      피올로는 회복력 11(4명 중 최고)이고 패시브 3종이 전부 지원형
      (부식·정신 안정·정화의 손길)이다. 다른 셋(Elin/Bigior/Preyja)은 이미 배정돼 있다.
    """
    print('[피올로 9004]')
    return import_asset(
        os.path.join(VAULT_ASSET, 'char_asset', 'Char_Asset_kim', 'Char'),
        'Char_Asset_Piolo',
        ppu=60,                     # 기존 캐릭터(프레이야)와 같은 스케일
        motion_map={
            'Idle': 'Idle',
            'Walk': 'Walk',
            'MeleeAttack': 'MeleeAttack',
            'RangedAttack': 'RangedAttack',
            'MagicAttack': 'MagicAttack',
            'Heal': 'Heal',         # ★ 전용 회복 모션 — 기존 캐릭터엔 하나도 없다(38-7절)
        })


def dantalian():
    """
    최종보스 120001 단탈리온. 표의 `ingame_asset` = `Char_Asset_Dantalian` 이고
    볼트에 원화가 그대로 있다(지금까지 게임에 안 들어와 있었다).

    ★ `SpecialBeam` / `SpecialShockwave` 는 보스 스킬 2종의 원화다 —
      공허의 광선(130002) · 타락한 무덤(130001). 스킬 자체는 아직 미구현이지만
      (미결 111번) 프레임은 같이 들여둔다: 나중에 구현할 때 아트를 다시 찾지 않아도 된다.
    """
    print('[단탈리온 120001]')
    return import_asset(
        os.path.join(VAULT_ASSET, 'monster_cancer_asset', 'Char_Asset_Dantalian', 'Char'),
        'Char_Asset_Dantalian',
        ppu=60,
        motion_map={
            'Idle': 'Idle',
            'Move': 'Walk',                 # 이 프로젝트의 모션 이름은 Walk 다
            'MeleeAttack': 'MeleeAttack',
            'SpecialBeam': 'SpecialBeam',
            'SpecialShockwave': 'SpecialShockwave',
            'Fx': 'Fx',
        })


if __name__ == '__main__':
    which = sys.argv[1] if len(sys.argv) > 1 else 'all'
    n = 0
    if which in ('all', 'piolo'):
        n += piolo()
    if which in ('all', 'dantalian'):
        n += dantalian()
    print('\n총 %d프레임 임포트 — Unity 에서 Assets/Refresh 를 실행할 것.' % n)
