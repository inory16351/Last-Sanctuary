# -*- coding: utf-8 -*-
"""`Skin_Anisakil.asset` (CharacterSkinSO) 을 만든다 (2026-08-19).

`anisakil_skin_build.py` 가 써놓은 프레임을 읽어 YAML 로 엮는다. 스프라이트 참조는 guid 로
넣고 guid 는 프레임 .meta 에서 읽는다 — `gen_carcinos_skin.py` 와 완전히 같은 방식이다.

★ 출력 위치 — <b>종마다 폴더 하나</b>
`Resources/MonsterSkins/Anisakil/Skin_Anisakil.asset`.
`CharacterAnimator.PickRandomSkin` 이 폴더 안에서 <b>무작위로</b> 고르므로, 종을 섞어 두면
아니사킬이 카르시노스 외형으로 나올 수 있다(기존 몬스터와 같은 규약).

⚠ 비우는 칸 — 아니사킬은 <b>근거리 종</b>이다(표 `atk_type=melee`):
    · 원거리/회복이 비면 `CharacterSkinSO.Attack` 이 근접 모션으로 폴백한다
    · 투사체가 비면 던지는 스킬이 없으므로 애초에 안 쓰인다
⚠ .asset YAML 에 <b>빈 줄을 넣으면</b> Unity 파서가 그 뒤 필드를 전부 무시한다(8절 3번).

사용법:  python Tools/gen_anisakil_skin.py
다음:    python Tools/measure_skin_tiles.py   (contentSizeTiles 실측)
"""

import hashlib
import os
import sys

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                   "Char_Asset_Anisakil", "Char")

REL_OUT = "Assets/_Project/Resources/MonsterSkins/Anisakil/Skin_Anisakil.asset"
OUT = os.path.join(PROJECT, *REL_OUT.split("/"))

SCRIPT_GUID_CHARACTER_SKIN = "a517e511b352f46488ffa35edf32295d"   # CharacterSkinSO.cs

DISPLAY_NAME = "아니사킬"


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
    atk_r, atk_l = frames("MeleeAttack", "Right"), frames("MeleeAttack", "Left")
    sk1_r, sk1_l = frames("Skill1", "Right"), frames("Skill1", "Left")
    sk2_r, sk2_l = frames("Skill2", "Right"), frames("Skill2", "Left")

    # 이펙트는 방향이 없다 — 연출은 조준 각도만큼 통째로 회전시켜 깔린다
    # (`CombatProjectileFx.PlayArea` 가 rotation 을 준다).
    fx1, fx2 = frames("Fx1"), frames("Fx2")

    if not (idle_r and walk_r and atk_r):
        raise SystemExit("⚠ 프레임이 없습니다 — 먼저 Tools/anisakil_skin_build.py 를 돌리세요")

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
        "  m_Name: Skin_Anisakil\n"
        "  m_EditorClassIdentifier:\n"
        f"  displayName: {DISPLAY_NAME}\n"
        "  framesPerSecond: 10\n"
        "  attackFramesPerSecond: 14\n"
    )
    body += sprite_list("idleRight", idle_r)
    body += sprite_list("idleLeft", idle_l)
    body += sprite_list("walkRight", walk_r)
    body += sprite_list("walkLeft", walk_l)
    body += sprite_list("attackRight", atk_r)
    body += sprite_list("attackLeft", atk_l)
    body += sprite_list("skill1Right", sk1_r)
    body += sprite_list("skill1Left", sk1_l)
    body += sprite_list("skill2Right", sk2_r)
    body += sprite_list("skill2Left", sk2_l)
    body += sprite_list("skill1Fx", fx1)
    body += sprite_list("skill2Fx", fx2)

    for key in ("rangedRight", "rangedLeft", "healRight", "healLeft",
                "reviveRight", "reviveLeft", "reviveFx",
                "skill1Projectile", "skill2Projectile",
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
                       "Assets/_Project/Resources/MonsterSkins/Anisakil")

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
    print(f"  근접 공격 {len(atk_r)}/{len(atk_l)}프레임")
    print(f"  스킬1     {len(sk1_r)}/{len(sk1_l)}프레임 · 이펙트 {len(fx1)}프레임")
    print(f"  스킬2     {len(sk2_r)}/{len(sk2_l)}프레임 · 이펙트 {len(fx2)}프레임")
    total = (idle_r + idle_l + walk_r + walk_l + atk_r + atk_l +
             sk1_r + sk1_l + sk2_r + sk2_l + fx1 + fx2)
    print(f"  스프라이트 참조 {len(total)}개")
    print("→", REL_OUT)
    print("다음: python Tools/measure_skin_tiles.py")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
