# -*- coding: utf-8 -*-
"""새 스킬 아이콘 시트 두 장 → ``Resources/SkillIcons/`` 개별 아이콘 (2026-08-20).

유저 지시
---------
*"추가로 스킬 아이콘 더 만들어서 넣었으니까 검토 해보고 더 적절한 아이콘이라고 생각할 경우에
현재 적용된 스킬 아이콘들 포함해서 바꿔서 적용하고 잘라서 에셋에 맞는 이름으로 저장한 후
테이블에도 적어줘"*

원본
----
``<볼트>/리소스/sprites/skill_icon_01.png``  (1286x1223 · RGBA) — **6 x 5 = 30개**
``<볼트>/리소스/sprites/skill_icon_02.png``  (1300x1210 · RGB)  — **6 x 6 = 36개**

★★ 검토 결과 — **기존 24개와 새 66개는 «다른 세트» 다**
--------------------------------------------------------
기존 ``Resources/SkillIcons/`` 의 24개는 **테두리 없는 납작한 발광 그림**이고, 새 두 장은
**장식 테두리가 있는 픽셀아트 타일**이다. 같은 소재도 완전히 다르게 그려져 있다
(활 · 번개 · 고드름 · 모래시계 등이 양쪽에 다 있지만 스타일이 안 맞는다).

→ 그래서 **섞어 쓰지 않는다.** 한 화면(상세 카드 · 로스터)에 두 스타일이 같이 뜨면
  «아이콘이 빠진 것처럼» 보인다. 새 세트가 개수도 많고(66 vs 24) 소재도 넓으므로
  **모든 스킬을 새 세트로 옮긴다** — 유저 지시의 *"현재 적용된 스킬 아이콘들 포함해서 바꿔서"*
  가 그 뜻이다. 배정은 ``Tools/table_update_20260820_skill_icons.py`` 가 표에 적는다.

⚠ 기존 24개는 **지우지 않는다.** 표에서 참조가 사라지면 게임은 새 것만 읽지만,
  ① 되돌릴 여지를 남기고 ② 지우면 옛 세이브·옛 에셋이 참조하던 이름이 조용히 깨진다.
  (스킬 아이콘은 이름 문자열로 읽으므로 파일이 남아 있어도 비용이 거의 없다 —
  `Resources` 는 폴더째 빌드에 들어가지만 아이콘 24장은 무게가 미미하다.)

칸을 어떻게 가르나
------------------
아이콘 타일은 **완전한 검정/투명 간격**으로 떨어져 있다. 그래서 「불투명하고 완전 검정이
아닌」 픽셀의 행·열 밴드를 세면 격자가 그대로 나온다(실측: 5행 6열 / 6행 6열, 오차 없음).
칸마다 다시 경계를 죄지 **않는다** — 아이콘은 테두리까지가 그림이고, 죄면 타일마다
크기가 달라져 UI 에서 들쭉날쭉해진다.

⚠ 출력은 **192x192 정사각**으로 리샘플한다 — 기존 아이콘이 전부 192x192 · PPU 192 이고,
  UI 가 그 크기를 전제로 배치돼 있다. 원본 칸은 143~200px 로 조금씩 다르다.

이름 — **그림 내용으로 짓는다**
-------------------------------
:data:`NAMES_01` · :data:`NAMES_02` 가 **행 우선**으로 늘어놓은 이름표다. 스킬 이름이 아니라
**그림이 무엇인지**로 지었다 — 스킬 배정이 바뀌어도 파일 이름이 거짓말이 되지 않게.
(예: 「가학증」에 붙일 아이콘 이름이 ``icon_sadism`` 이면 그 스킬을 다른 그림으로
 바꾸는 순간 이름이 어긋난다.)

⚠ 기존 24개와 **이름이 겹치지 않게** 지었다 — 같은 소재라도 다른 그림이라
  (``icon_lightning`` ↔ ``icon_lightning_strike``) 덮어쓰면 옛 참조가 새 그림을 가리킨다.

사용법:  python Tools/skill_icon_build.py
다음:    py -3 Tools/table_update_20260820_skill_icons.py   (표에 배정)
        유니티 Assets/Refresh
"""

import hashlib
import os
import sys

import numpy as np
from PIL import Image

from vault_path import VAULT, PROJECT

SRC_DIR = os.path.join(VAULT, "리소스", "sprites")
DST = os.path.join(PROJECT, "Assets", "_Project", "Resources", "SkillIcons")

#: 출력 크기 · PPU — 기존 아이콘과 같아야 UI 배치가 안 흔들린다.
SIZE = 192
PPU = 192

#: 타일 판정 — 「불투명하고 완전 검정이 아닌」 픽셀.
TILE_ALPHA_MIN = 40
TILE_LUM_MIN = 30

#: 행·열 밴드로 인정할 최소 채움 비율 / 최소 두께(px).
BAND_FILL_RATIO = 0.25
BAND_MIN_PX = 20

# ──────────────────────────────────────────────────────────────────────────
# 이름표 — **행 우선**(왼→오, 위→아래). 그림 내용으로 지었다(맨 위 「이름」).
# ──────────────────────────────────────────────────────────────────────────
NAMES_01 = [
    # 1행 — 무기·원소
    "sword_flame_slash", "arrow_volley_blue", "bow_shot",
    "comet_fall", "lightning_strike", "ice_spikes",
    # 2행 — 조준·회복·보호
    "holy_target", "heal_plus", "group_heal",
    "guard_shield", "angel_ascend", "charge_slash",
    # 3행 — 화살·독·심연
    # ⚠ ``poison_skull`` 은 기존 아이콘 이름이다 — 겹치면 옛 그림을 덮어쓴다.
    "arrow_volley_gold", "snipe_mark", "plague_skull",
    "void_spikes", "light_pillar", "winged_sword",
    # 4행 — 대지·질주·도발·피
    "stone_golem", "sprint_dash", "taunt_guard",
    "blood_drop", "frozen_hourglass", "fire_burst",
    # 5행 — 부활·강화·심장·주문
    "revive_angel", "blood_spikes", "blade_buff",
    "heart_guard", "spell_tome", "demon_eye",
]

NAMES_02 = [
    # 1행 — 승천·어둠·구속
    "ascension", "dark_effigy", "chain_shackle",
    "meteor_circle", "void_spiral", "blood_moon",
    # 2행 — 사안·발톱·망령·독
    "evil_eye", "claw_marks", "ghost_wail",
    "thorn_vines", "plague_cloud", "undead_rise",
    # 3행 — 뼈·참격·사냥·깃털
    "bone_spikes", "red_slash", "hunt_mark",
    "feather_burst", "gold_tornado", "sand_hourglass",
    # 4행 — 시설 (⚠ 스킬이 아니라 건물·설비 소재다)
    "cannon_battery", "spike_trap", "medic_tent",
    "holy_banner", "anvil_forge", "crystal_keep",
    # 5행 — 문서·제단·보상 (⚠ 스킬이 아니다)
    "scroll_quill", "arcane_tome", "dark_shrine",
    "dark_gate", "treasure_chest", "coin_pouch",
    # 6행 — 관문·해골·공허·서리·사신·유성
    "teal_portal", "skull_pyre", "void_orb",
    "frost_wolf", "hooded_reaper", "meteor_shower",
]

SHEETS = [
    ("skill_icon_01.png", 6, 5, NAMES_01),
    ("skill_icon_02.png", 6, 6, NAMES_02),
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


def guid_for(key):
    """경로에서 결정적으로 뽑은 guid — 다시 돌려도 같은 값이라 참조가 안 끊긴다."""
    return hashlib.md5(("LastSanctuary/" + key).encode("utf-8")).hexdigest()


def bands(counts, limit):
    """채움이 <paramref>limit</paramref> 를 넘는 구간(두께 BAND_MIN_PX 이상) 목록."""
    out, i, n = [], 0, len(counts)
    while i < n:
        if counts[i] > limit:
            j = i
            while j < n and counts[j] > limit:
                j += 1
            if j - i > BAND_MIN_PX:
                out.append((i, j - 1))
            i = j
        else:
            i += 1
    return out


def write_icon(img, name):
    os.makedirs(DST, exist_ok=True)
    path = os.path.join(DST, "icon_%s.png" % name)
    img.save(path)

    rel = os.path.relpath(path, os.path.join(PROJECT, "Assets", "_Project")).replace("\\", "/")
    g = guid_for(rel)
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(META.format(guid=g, ppu=PPU, sprite_id=g[:32]))
    return path


def slice_sheet(fname, cols, rows, names):
    path = os.path.join(SRC_DIR, fname)
    if not os.path.isfile(path):
        raise SystemExit("⚠ 원본이 없습니다: " + path)

    im = Image.open(path).convert("RGBA")
    a = np.asarray(im).astype(np.int16)
    solid = (a[:, :, 3] > TILE_ALPHA_MIN) & (a[:, :, :3].max(axis=2) > TILE_LUM_MIN)
    h, w = solid.shape

    rb = bands(solid.sum(axis=1), w * BAND_FILL_RATIO)
    cb = bands(solid.sum(axis=0), h * BAND_FILL_RATIO)

    if len(rb) != rows or len(cb) != cols:
        raise SystemExit(
            "⚠ %s: 격자가 %d행 %d열로 잡혔습니다 (%d x %d 을 기대). 시트가 바뀌었으면 "
            "cols/rows 를 고치세요.\n   행: %s\n   열: %s"
            % (fname, len(rb), len(cb), rows, cols, rb, cb))

    if len(names) != rows * cols:
        raise SystemExit("⚠ %s: 이름표가 %d개인데 칸이 %d개입니다."
                         % (fname, len(names), rows * cols))

    made = 0
    for r, (y0, y1) in enumerate(rb):
        for c, (x0, x1) in enumerate(cb):
            tile = im.crop((x0, y0, x1 + 1, y1 + 1))
            # 정사각으로 맞춘다 — 칸이 조금씩 달라도 UI 에서는 같은 크기여야 한다.
            tile = tile.resize((SIZE, SIZE), Image.LANCZOS)
            write_icon(tile, names[r * cols + c])
            made += 1

    print("  %s  %d행 x %d열 = %d개  (칸 크기 %dx%d ~ %dx%d → %dx%d 로 리샘플)"
          % (fname, rows, cols, made,
             cb[0][1] - cb[0][0] + 1, rb[0][1] - rb[0][0] + 1,
             cb[-1][1] - cb[-1][0] + 1, rb[-1][1] - rb[-1][0] + 1, SIZE, SIZE))
    return made


def check_name_clashes():
    """
    ⚠ 기존 아이콘 이름과 겹치면 **옛 그림을 덮어쓴다** — 실제로 한 번 그랬다
    (``poison_skull``). 같은 소재라도 다른 그림이므로 겹쳐서는 안 된다.
    """
    if not os.path.isdir(DST):
        return
    have = {f[5:-4] for f in os.listdir(DST) if f.startswith("icon_") and f.endswith(".png")}
    new = set(NAMES_01) | set(NAMES_02)
    clash = sorted(have & new)
    # 이 스크립트가 만든 것끼리는 겹쳐도 된다(다시 돌리는 경우) — 문제는 «이 스크립트가
    # 만들지 않은» 기존 파일과 겹치는 것이다. 그걸 구분할 방법은 guid 규칙이다:
    # 우리가 만든 .meta 는 경로에서 계산한 guid 를 갖는다.
    real = []
    for name in clash:
        meta = os.path.join(DST, "icon_%s.png.meta" % name)
        if not os.path.isfile(meta):
            continue
        rel = os.path.relpath(os.path.join(DST, "icon_%s.png" % name),
                              os.path.join(PROJECT, "Assets", "_Project")).replace("\\", "/")
        want = guid_for(rel)
        with open(meta, encoding="utf-8") as f:
            head = f.read(400)
        if ("guid: " + want) not in head:
            real.append(name)
    if real:
        raise SystemExit("⚠ 기존 아이콘과 이름이 겹칩니다(덮어쓰면 옛 그림이 사라진다): "
                         + ", ".join(real))


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    print("[스킬 아이콘 시트 분해]")

    check_name_clashes()
    before = {f for f in os.listdir(DST) if f.endswith(".png")} if os.path.isdir(DST) else set()

    total = 0
    for fname, cols, rows, names in SHEETS:
        total += slice_sheet(fname, cols, rows, names)

    after = {f for f in os.listdir(DST) if f.endswith(".png")}
    print("  → 아이콘 %d개 저장 (%s)" % (total, os.path.relpath(DST, PROJECT)))
    print("  기존 %d개 유지 · 새로 늘어난 파일 %d개"
          % (len(before), len(after - before)))
    print("  다음: py -3 Tools/table_update_20260820_skill_icons.py")


if __name__ == "__main__":
    main()
