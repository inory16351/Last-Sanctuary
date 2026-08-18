# -*- coding: utf-8 -*-
"""`Skin_Gordone.asset` (CharacterSkinSO) 을 만든다 (2026-08-19).

`gordone_skin_build.py` 가 써놓은 프레임을 읽어 YAML 로 엮는다. 스프라이트 참조는 guid 로
넣고 guid 는 프레임 .meta 에서 읽는다 — `gen_carcinos_skin.py` 와 완전히 같은 방식이다.

★ 출력 위치 — <b>종마다 폴더 하나</b>
`Resources/MonsterSkins/Gordonae/Skin_Gordonae.asset`.
`CharacterAnimator.PickRandomSkin` 이 폴더 안에서 <b>무작위로</b> 고르므로, 종을 섞어 두면
고르도네가 종양귀 외형으로 나올 수 있다(기존 몬스터와 같은 규약).

★ 고르도네는 <b>원거리 종</b>이다(표 `atk_type=ranged`) — 그래서
  <b>`rangedRight`/`rangedLeft` 를 채우고 `attackRight`/`attackLeft` 는 비운다.</b>
  ⚠ 근접 칸을 비워도 된다: `CharacterSkinSO.Attack` 은 <b>공격 유형에 맞는 칸</b>을 먼저 보고
    비어 있을 때만 폴백한다. 원거리 종에 근접 모션 원화가 없으므로 그쪽을 비우는 것이 맞다.

⚠ <b>스킬 칸도 비운다</b> — 표의 `mon_skill_1`·`mon_skill_2` 가 비어 있다(일반 몹).
⚠ <b>투사체 칸도 비운다</b> — 시트에 투사체 전용 행이 없다. 비우면
  `CombatProjectileFx` 가 폴백 탄환을 쓴다(`gordone_skin_build.py` 맨 위 참조).
⚠ .asset YAML 에 <b>빈 줄을 넣으면</b> Unity 파서가 그 뒤 필드를 전부 무시한다(8절 3번).

사용법:  python Tools/gen_anisakil_skin.py
다음:    python Tools/measure_skin_tiles.py   (contentSizeTiles 실측)
"""

import hashlib
import os
import sys

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                   "Char_Asset_Gordonae", "Char")

REL_OUT = "Assets/_Project/Resources/MonsterSkins/Gordonae/Skin_Gordonae.asset"
OUT = os.path.join(PROJECT, *REL_OUT.split("/"))

SCRIPT_GUID_CHARACTER_SKIN = "a517e511b352f46488ffa35edf32295d"   # CharacterSkinSO.cs

DISPLAY_NAME = "고르도네"


def guid_of(path):
    """.meta 에서 guid 를 읽는다. 없으면 바로 죽는다 — 조용히 빈 참조를 넣으면 안 된다."""
    with open(path + ".meta", encoding="utf-8") as f:
        for line in f:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    raise RuntimeError("guid 를 찾지 못했습니다: " + path + ".meta")


def frames(motion, side=None):
    folder = os.path.join(ART, motion)
    if not os.path.isdir(folder):
        return []
    prefix = f"Char_{motion}_{side}_" if side else f"Char_{motion}_"
    names = sorted(n for n in os.listdir(folder)
                   if n.startswith(prefix) and n.endswith(".png"))
    return [guid_of(os.path.join(folder, n)) for n in names]


def sprite_list(key, guids):
    if not guids:
        return f"  {key}: []\n"
    body = f"  {key}:\n"
    for g in guids:
        body += "  - {fileID: 21300000, guid: %s, type: 3}\n" % g
    return body


def md5_guid(rel):
    return hashlib.md5(("LastSanctuary/" + rel).encode("utf-8")).hexdigest()


FOLDER_META = ("fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\n"
               "DefaultImporter:\n  externalObjects: {{}}\n  userData: \n"
               "  assetBundleName: \n  assetBundleVariant: \n")


def ensure_folder_meta(path, rel):
    mp = path.rstrip("\\/") + ".meta"
    if not os.path.exists(mp):
        with open(mp, "w", encoding="utf-8", newline="\n") as f:
            f.write(FOLDER_META.format(guid=md5_guid(rel)))


def main():
    idle_r, idle_l = frames("Idle", "Right"), frames("Idle", "Left")
    walk_r, walk_l = frames("Walk", "Right"), frames("Walk", "Left")
    rng_r, rng_l = frames("RangedAttack", "Right"), frames("RangedAttack", "Left")

    if not (idle_r and walk_r and rng_r):
        raise SystemExit("⚠ 프레임이 없습니다 — 먼저 Tools/gordone_skin_build.py 를 돌리세요")

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
        "  m_Name: Skin_Gordonae\n"
        "  m_EditorClassIdentifier:\n"
        f"  displayName: {DISPLAY_NAME}\n"
        "  framesPerSecond: 10\n"
        "  attackFramesPerSecond: 14\n"
    )
    body += sprite_list("idleRight", idle_r)
    body += sprite_list("idleLeft", idle_l)
    body += sprite_list("walkRight", walk_r)
    body += sprite_list("walkLeft", walk_l)
    body += sprite_list("rangedRight", rng_r)
    body += sprite_list("rangedLeft", rng_l)

    for key in ("attackRight", "attackLeft", "healRight", "healLeft",
                "reviveRight", "reviveLeft", "reviveFx",
                "skill1Right", "skill1Left", "skill2Right", "skill2Left",
                "skill1Fx", "skill2Fx", "skill1Projectile", "skill2Projectile",
                "projectileFrames", "muzzleFlashFrames", "impactFrames"):
        body += sprite_list(key, [])

    body += (
        "  projectileScale: 1\n"
        "  impactScale: {x: 1, y: 1}\n"
        # contentSizeTiles 는 Tools/measure_skin_tiles.py 가 실측해 덮어쓴다.
        "  contentSizeTiles: {x: 0, y: 0}\n"
        "  projectileSizeTiles: {x: 0, y: 0}\n"
        "  impactSizeTiles: {x: 0, y: 0}\n"
        "  projectileWidthTiles: 0\n"
        "  impactWidthTiles: 0\n"
        "  impactFlattenY: 1\n"
    )

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    ensure_folder_meta(os.path.dirname(OUT),
                       "Assets/_Project/Resources/MonsterSkins/Gordonae")

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(body)
    with open(OUT + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write("fileFormatVersion: 2\n"
                f"guid: {md5_guid(REL_OUT)}\n"
                "NativeFormatImporter:\n"
                "  externalObjects: {}\n"
                "  mainObjectFileID: 11400000\n"
                "  userData:\n"
                "  assetBundleName:\n"
                "  assetBundleVariant:\n")

    print(f"  대기      {len(idle_r)}/{len(idle_l)}프레임 (우/좌)")
    print(f"  이동      {len(walk_r)}/{len(walk_l)}프레임")
    print(f"  원거리    {len(rng_r)}/{len(rng_l)}프레임")
    total = idle_r + idle_l + walk_r + walk_l + rng_r + rng_l
    print(f"  스프라이트 참조 {len(total)}개")
    print("→", REL_OUT)
    print("다음: python Tools/measure_skin_tiles.py")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
