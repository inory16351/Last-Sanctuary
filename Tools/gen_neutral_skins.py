# -*- coding: utf-8 -*-
"""중립 몬스터 3종의 `CharacterSkinSO` 에셋을 만든다 (2026-08-15).

`neutral_skin_build.py` 가 써놓은 프레임을 읽어 YAML 로 엮는다. 스프라이트 참조는 guid 로
넣고, guid 는 프레임 경로에서 결정적으로 뽑는다(빌더와 <b>같은 규칙</b>) —
`gen_carcinos_skin.py` / `gen_histon_skin.py` 와 완전히 같은 방식이다.

★ 출력 위치 — <b>종마다 폴더 하나</b>
------------------------------------
`Resources/MonsterSkins/<종>/Skin_<종>.asset`.

`CharacterAnimator.PickRandomSkin` 은 자기 `skinResourceFolder` 안의 스킨 중 <b>무작위로</b>
하나를 고른다. 몬스터 스킨을 한 폴더에 몰아넣으면 종양 거미가 카르시노스 외형으로 나올 수
있다. 그래서 기존 몬스터(`MonsterSkins/HellFang` · `SoulArcher` · `Dantalian` · `Carcinos`)와
같은 규약을 따른다 — 후보가 언제나 한 개가 되게.

★ 표와의 연결
-------------
`임시용 중립 몬스터.xlsx` 의 `mon_skin` 칸에 `<종>_asset` 을 적으면
`NeutralMonsterDefinitionSO.SkinResourcePath` 가 `MonsterSkins/<종>/Skin_<종>` 을 만든다.
이 스크립트를 돌린 뒤 <b>표의 그 칸을 채워야</b> 게임에 붙는다 —
`table_update_20260815_neutral_skins.py` 가 그 일을 한다.

⚠ 원거리 종(종양귀)만 `rangedRight/Left` 와 `projectileFrames` 가 찬다. 근거리 두 종은
  그 칸이 비고, 비면 `CharacterSkinSO.Attack` 이 근접 모션으로 폴백한다.
⚠ .asset YAML 에 <b>빈 줄을 넣으면</b> Unity 파서가 그 뒤 필드를 전부 무시한다(진행상황 8절 3번).

사용법:  py -3 Tools/gen_neutral_skins.py
다음:    py -3 Tools/measure_skin_tiles.py   (contentSizeTiles 실측)
"""

import hashlib
import os
import sys

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset")

SCRIPT_GUID_CHARACTER_SKIN = "a517e511b352f46488ffa35edf32295d"   # CharacterSkinSO.cs

#: (종 이름, 화면에 쓸 이름). 이름은 스트링 키 테이블의 kr 과 같은 값이다 —
#: 이 칸은 로그·디버그용이라 게임 표시에는 쓰이지 않는다(표시는 표의 mon_name 이 정본).
SPECIES = [
    ("TumorSpider", "종양 거미"),
    ("Tumorling",   "종양귀"),
    ("TumorMole",   "종양 두더지"),
]

#: 스킨 필드 → (모션 폴더, 방향). 방향이 None 이면 방향 없는 한 벌.
MOTION_FIELDS = [
    ("idleRight",        "Idle",         "Right"),
    ("idleLeft",         "Idle",         "Left"),
    ("walkRight",        "Walk",         "Right"),
    ("walkLeft",         "Walk",         "Left"),
    ("attackRight",      "MeleeAttack",  "Right"),
    ("attackLeft",       "MeleeAttack",  "Left"),
    ("rangedRight",      "RangedAttack", "Right"),
    ("rangedLeft",       "RangedAttack", "Left"),
    ("projectileFrames", "Projectile",   None),
]

#: 원화가 없어 비워두는 칸 — 각자 폴백이 걸린다(맨 위 주석).
EMPTY_FIELDS = [
    "healRight", "healLeft",
    "reviveRight", "reviveLeft", "reviveFx",
    "skill1Right", "skill1Left", "skill2Right", "skill2Left",
    "skill1Fx", "skill2Fx",
    "muzzleFlashFrames", "impactFrames",
]

FOLDER_META = ("fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\n"
               "DefaultImporter:\n  externalObjects: {{}}\n  userData: \n"
               "  assetBundleName: \n  assetBundleVariant: \n")


def md5_guid(rel):
    return hashlib.md5(("LastSanctuary/" + rel).encode("utf-8")).hexdigest()


def guid_of_frame(path):
    """빌더가 .meta 에 쓴 것과 <b>같은 규칙</b>으로 계산한다(경로 → md5)."""
    rel = os.path.relpath(path, os.path.join(PROJECT, "Assets", "_Project")).replace("\\", "/")
    return md5_guid(rel)


def frames(species, motion, side):
    folder = os.path.join(ART_ROOT, "Char_Asset_" + species, "Char", motion)
    if not os.path.isdir(folder):
        return []
    prefix = f"Char_{motion}_{side}_" if side else f"Char_{motion}_"
    names = sorted(n for n in os.listdir(folder)
                   if n.startswith(prefix) and n.endswith(".png"))
    return [guid_of_frame(os.path.join(folder, n)) for n in names]


def sprite_list(key, guids):
    if not guids:
        return f"  {key}: []\n"
    body = f"  {key}:\n"
    for g in guids:
        body += "  - {fileID: 21300000, guid: %s, type: 3}\n" % g
    return body


def ensure_folder_meta(path, rel):
    mp = path.rstrip("\\/") + ".meta"
    if not os.path.exists(mp):
        with open(mp, "w", encoding="utf-8", newline="\n") as f:
            f.write(FOLDER_META.format(guid=md5_guid(rel)))


def build(species, display):
    got = {}
    for field, motion, side in MOTION_FIELDS:
        got[field] = frames(species, motion, side)

    if not got["idleRight"]:
        print(f"  ⚠ {species}: 프레임이 없습니다 — 먼저 Tools/neutral_skin_build.py 를 도세요")
        return False

    rel_out = f"Assets/_Project/Resources/MonsterSkins/{species}/Skin_{species}.asset"
    out = os.path.join(PROJECT, *rel_out.split("/"))

    body = (
        "%YAML 1.1\n"
        "%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!114 &11400000\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID_CHARACTER_SKIN}, type: 3}}\n"
        f"  m_Name: Skin_{species}\n"
        "  m_EditorClassIdentifier:\n"
        f"  displayName: {display}\n"
        "  framesPerSecond: 10\n"
        "  attackFramesPerSecond: 14\n"
    )
    for field, _, _ in MOTION_FIELDS:
        # 투사체는 아래 표시 크기 칸들과 붙여 두는 편이 읽기 좋지만, 필드 순서는
        # Unity 직렬화에 영향이 없다(이름으로 찾는다).
        body += sprite_list(field, got[field])
    for field in EMPTY_FIELDS:
        body += sprite_list(field, [])

    body += (
        "  projectileScale: 0.55\n"
        "  impactScale: {x: 1, y: 1}\n"
        # 아래 셋은 Tools/measure_skin_tiles.py 가 실측해 덮어쓴다.
        "  contentSizeTiles: {x: 0, y: 0}\n"
        "  projectileSizeTiles: {x: 0, y: 0}\n"
        "  impactSizeTiles: {x: 0, y: 0}\n"
        # 탄환을 가로 몇 타일로 그릴지. 종양귀 탄환은 꼬리가 길어 1.6 타일로 뒀다 —
        # 화면에서 보고 조정할 값이라 인스펙터에서 고치면 된다.
        f"  projectileWidthTiles: {1.6 if got['projectileFrames'] else 0}\n"
        "  impactWidthTiles: 0\n"
        "  impactFlattenY: 1\n"
    )

    os.makedirs(os.path.dirname(out), exist_ok=True)
    ensure_folder_meta(os.path.dirname(out),
                       f"Assets/_Project/Resources/MonsterSkins/{species}")

    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write(body)
    with open(out + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write("fileFormatVersion: 2\n"
                f"guid: {md5_guid(rel_out)}\n"
                "NativeFormatImporter:\n"
                "  externalObjects: {}\n"
                "  mainObjectFileID: 11400000\n"
                "  userData:\n"
                "  assetBundleName:\n"
                "  assetBundleVariant:\n")

    parts = [f"대기 {len(got['idleRight'])}", f"이동 {len(got['walkRight'])}"]
    if got["attackRight"]:
        parts.append(f"근접 {len(got['attackRight'])}")
    if got["rangedRight"]:
        parts.append(f"원거리 {len(got['rangedRight'])}")
    if got["projectileFrames"]:
        parts.append(f"투사체 {len(got['projectileFrames'])}")
    total = sum(len(v) for v in got.values())
    print(f"  {species:<12} {display:<8} {' · '.join(parts)}  (스프라이트 참조 {total}개)")
    print(f"    → {rel_out}")
    return True


def main():
    ensure_folder_meta(os.path.join(PROJECT, "Assets", "_Project", "Resources", "MonsterSkins"),
                       "Assets/_Project/Resources/MonsterSkins")
    ok = 0
    for species, display in SPECIES:
        if build(species, display):
            ok += 1
    print(f"\n스킨 {ok}개 생성")
    print("다음: py -3 Tools/measure_skin_tiles.py")
    return 0 if ok == len(SPECIES) else 1


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
