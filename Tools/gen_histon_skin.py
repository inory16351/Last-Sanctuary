# -*- coding: utf-8 -*-
"""`Skin_Histon.asset` (CharacterSkinSO) 을 만든다 (2026-08-14).

`histon_skin_build.py` 가 써놓은 프레임을 읽어 YAML 로 엮는다. 스프라이트 참조는 guid 로
넣는데, guid 는 프레임 .meta 에서 읽는다 — `gen_skin_assets.py` 와 완전히 같은 방식이다.

⚠ .asset YAML 에 <b>빈 줄을 넣으면</b> Unity 파서가 그 뒤 필드를 전부 무시한다(진행상황 8절 3번).
⚠ MCP 에는 SO 에셋을 만드는 도구도, 스프라이트 참조를 넣는 도구도 없다(8절 1·4번) —
   그래서 이 종류의 에셋만 스크립트로 쓴다. 씬 오브젝트·컴포넌트는 MCP 로 만든다.

히스톤은 <b>근접 전용</b>이다(표의 Vanguard 스킬이 "공격 유형은 근거리로 고정"). 그래서
원거리·투사체 칸은 비워 둔다 — `CharacterSkinSO.Attack` 이 없으면 근접 모션으로 폴백한다.

사용법:  python Tools/gen_histon_skin.py
"""

import os
import hashlib

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset", "Char_Asset_Histon", "Char")
OUT = os.path.join(PROJECT, "Assets", "_Project", "Resources", "Skins", "Skin_Histon.asset")

SCRIPT_GUID_CHARACTER_SKIN = "a517e511b352f46488ffa35edf32295d"   # CharacterSkinSO.cs


def guid_of(path):
    """.meta 에서 guid 를 읽는다. 없으면 바로 죽는다 — 조용히 빈 참조를 넣으면 안 된다."""
    with open(path + ".meta", encoding="utf-8") as f:
        for line in f:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    raise RuntimeError("guid 를 찾지 못했습니다: " + path + ".meta")


def frames(motion, side):
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


def asset_guid():
    return hashlib.md5(b"LastSanctuary/Assets/_Project/Resources/Skins/Skin_Histon.asset").hexdigest()


def main():
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
        "  m_Name: Skin_Histon\n"
        "  m_EditorClassIdentifier:\n"
        "  displayName: 히스톤\n"
        "  framesPerSecond: 10\n"
        "  attackFramesPerSecond: 14\n"
    )
    body += sprite_list("idleRight", frames("Idle", "Right"))
    body += sprite_list("idleLeft", frames("Idle", "Left"))
    body += sprite_list("walkRight", frames("Walk", "Right"))
    body += sprite_list("walkLeft", frames("Walk", "Left"))
    body += sprite_list("attackRight", frames("MeleeAttack", "Right"))
    body += sprite_list("attackLeft", frames("MeleeAttack", "Left"))
    # 원거리·회복은 비운다 — 근접 전용이라 Attack() 이 근접 모션으로 폴백한다.
    for key in ("rangedRight", "rangedLeft", "healRight", "healLeft"):
        body += sprite_list(key, [])
    body += sprite_list("reviveRight", frames("Revive", "Right"))
    body += sprite_list("reviveLeft", frames("Revive", "Left"))
    body += sprite_list("reviveFx", frames("ReviveFx", ""))
    for key in ("skill1Right", "skill1Left", "skill2Right", "skill2Left",
                "skill1Fx", "skill2Fx",
                "projectileFrames", "muzzleFlashFrames", "impactFrames"):
        body += sprite_list(key, [])
    body += (
        "  projectileScale: 0.55\n"
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
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(body)
    with open(OUT + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write("fileFormatVersion: 2\n"
                f"guid: {asset_guid()}\n"
                "NativeFormatImporter:\n"
                "  externalObjects: {}\n"
                "  mainObjectFileID: 11400000\n"
                "  userData:\n"
                "  assetBundleName:\n"
                "  assetBundleVariant:\n")

    for motion, side in (("Idle", "Right"), ("Walk", "Right"), ("MeleeAttack", "Right"),
                         ("Revive", "Right"), ("ReviveFx", "")):
        print(f"  {motion:12} {len(frames(motion, side))}프레임")
    print("→", os.path.relpath(OUT, PROJECT))


if __name__ == "__main__":
    main()
