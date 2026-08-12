# -*- coding: utf-8 -*-
"""
스킨 에셋(.asset) 생성·갱신 스크립트 (2026-08-11).

하는 일 두 가지.

1. **`Skin_Preyja.asset` 을 새로 만든다** — `char_asset_preyja_build.py` 가 써놓은
   프레임을 읽어 CharacterSkinSO YAML 로 엮는다. 스프라이트 참조는 guid 로 넣는데,
   guid 는 프레임 .meta 에서 읽는다.

2. **기존 스킨 5개에 새 필드를 덧붙인다** — 회복 모션(healRight/healLeft)과
   투사체(projectileFrames · muzzleFlashFrames · impactFrames · projectileScale ·
   impactScale). 각 유닛이 지금까지
   `CombatProjectileFx` 안의 분기로 받고 있던 탄환을 **자기 스킨으로 옮기는 것**이
   목적이다(유저 지시: "객체마다 투사체 스킨 따로 관리").

   ⚠ 기존 스킨의 프레임 목록은 **손대지 않는다.** 분비형 암세포처럼 손으로 다듬은
   프레임(침 줄기를 지운 것 — 30절)이 있어서 전체를 다시 생성하면 그 작업이 날아간다.
   그래서 키를 새로 추가하거나 이미 있으면 갈아끼우는 방식으로만 고친다.

⚠ .asset YAML 에 **빈 줄을 넣으면 Unity 파서가 그 뒤 필드를 전부 무시한다**(진행상황 8절 3번).
⚠ MCP 에는 SO 에셋을 만드는 도구도, 스프라이트 참조를 넣는 도구도 없다(8절 1·4번).
   그래서 이 종류의 에셋만 스크립트로 쓴다 — 씬 오브젝트·컴포넌트는 MCP 로 만든다.
"""

import os
import re
import hashlib

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset")
RES = os.path.join(PROJECT, "Assets", "_Project", "Resources")

SCRIPT_GUID_CHARACTER_SKIN = "a517e511b352f46488ffa35edf32295d"   # CharacterSkinSO.cs

# Resources/Fx 의 폴백 탄환 — 각 스킨에 "자기 탄환"으로 박아준다.
FX = os.path.join(RES, "Fx")


def guid_of(asset_path):
    """.meta 에서 guid 를 읽는다. 파일이 없으면 바로 죽는다 — 조용히 빈 참조를 넣으면 안 된다."""
    meta = asset_path + ".meta"
    with open(meta, encoding="utf-8") as f:
        for line in f:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    raise RuntimeError(f"guid 를 찾지 못했습니다: {meta}")


def fx_guids(*names):
    return [guid_of(os.path.join(FX, n + ".png")) for n in names]


def spit_guids():
    names = sorted(n[:-4] for n in os.listdir(FX)
                   if n.startswith("Projectile_Spit_") and n.endswith(".png"))
    return [guid_of(os.path.join(FX, n + ".png")) for n in names]


def frame_guids(char_folder, motion, side):
    """Art/Char_Asset/<char>/Char/<motion>/ 에서 방향별 프레임 guid 를 번호 순으로."""
    folder = os.path.join(ART, char_folder, "Char", motion)
    if not os.path.isdir(folder):
        return []
    names = sorted(n[:-4] for n in os.listdir(folder)
                   if n.endswith(".png") and (f"_{side}_" in n if side else True))
    return [guid_of(os.path.join(folder, n + ".png")) for n in names]


def sprite_list(key, guids):
    """`key:` 다음에 스프라이트 참조를 줄줄이. 빈 목록은 `key: []` 로 — 빈 줄을 만들지 않는다."""
    if not guids:
        return f"  {key}: []\n"
    body = f"  {key}:\n"
    for g in guids:
        body += "  - {fileID: 21300000, guid: %s, type: 3}\n" % g
    return body


# ======================================================================
# 1. Skin_Preyja 새로 만들기
# ======================================================================

HEADER = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier:
"""

ASSET_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def write_preyja():
    char = "Char_Asset_Preyja"
    name = "Skin_Preyja"
    rel = f"Resources/Skins/{name}.asset"

    body = HEADER.format(script_guid=SCRIPT_GUID_CHARACTER_SKIN, name=name)
    body += "  displayName: 프레이야\n"
    body += "  framesPerSecond: 10\n"
    body += "  attackFramesPerSecond: 14\n"
    body += sprite_list("idleRight", frame_guids(char, "Idle", "Right"))
    body += sprite_list("idleLeft", frame_guids(char, "Idle", "Left"))
    body += sprite_list("walkRight", frame_guids(char, "Walk", "Right"))
    body += sprite_list("walkLeft", frame_guids(char, "Walk", "Left"))
    body += sprite_list("attackRight", frame_guids(char, "MeleeAttack", "Right"))
    body += sprite_list("attackLeft", frame_guids(char, "MeleeAttack", "Left"))
    body += sprite_list("rangedRight", frame_guids(char, "RangedAttack", "Right"))
    body += sprite_list("rangedLeft", frame_guids(char, "RangedAttack", "Left"))
    # 회복 전용 원화가 없다 → 비워둔다. CharacterSkinSO.Attack 이 원거리 → 근접으로 대체한다.
    body += sprite_list("healRight", [])
    body += sprite_list("healLeft", [])
    # ★ 프레이야는 원본 아트팩에 전용 투사체가 들어있다 (Projectile 4장 + ProjectileBurst 5장).
    body += sprite_list("projectileFrames", frame_guids(char, "Projectile", ""))
    # 발사 섬광은 없다 — ProjectileBurst 는 손끝 섬광이 아니라 **맞은 자리**의 연출이다
    # (창이 꽂히고 사방으로 터진다 → 마법이면 피해 범위 표시. 유저 지적 2026-08-11).
    body += sprite_list("muzzleFlashFrames", [])
    body += sprite_list("impactFrames", frame_guids(char, "ProjectileBurst", ""))
    body += "  projectileScale: 0.45\n"
    # 마법 범위(magicAreaTiles 2 = 2x2타일)를 덮도록 x 를 맞추고, 3/4 탑뷰라 y 를 눌러 눕힌다.
    # 원화가 측면 시점(위로 솟는 폭발)이라 이건 임시 보정이다 — 탑뷰로 다시 그리는 것이 정답.
    body += "  impactScale: {x: 1.05, y: 0.7}\n"

    path = os.path.join(RES, "Skins", name + ".asset")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(body)
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(ASSET_META.format(guid=hashlib.md5(("LastSanctuary/" + rel).encode()).hexdigest()))
    n_proj = len(frame_guids(char, "Projectile", ""))
    n_burst = len(frame_guids(char, "ProjectileBurst", ""))
    print(f"  {name}: 생성 (투사체 {n_proj}프레임 · 착탄 {n_burst}프레임)")


# ======================================================================
# 2. 기존 스킨에 새 필드 덧붙이기
# ======================================================================

def upsert_keys(path, new_fields, drop=None):
    """
    `new_fields` 를 파일 끝에 추가한다. 이미 있는 키는 지우고 다시 쓴다(멱등).

    ⚠ 스프라이트 목록 키는 `key:` 다음 줄부터 `  - {fileID...}` 가 이어지므로
      키를 지울 때 딸린 항목 줄까지 함께 지워야 한다.
    """
    with open(path, encoding="utf-8") as f:
        lines = f.read().splitlines()

    keys = [k for k, _ in new_fields] + list(drop or ())
    out = []
    i = 0
    while i < len(lines):
        line = lines[i]
        m = re.match(r"^  ([A-Za-z0-9_]+):", line)
        if m and m.group(1) in keys:
            i += 1
            while i < len(lines) and lines[i].startswith("  - "):
                i += 1
            continue
        out.append(line)
        i += 1

    body = "\n".join(out).rstrip("\n") + "\n"
    for _, text in new_fields:
        body += text
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(body)
    assert "\n\n" not in body, f"빈 줄이 생겼습니다(8절 3번): {path}"


def patch_existing():
    bolt, flash = fx_guids("Projectile_Bolt", "Projectile_Flash")
    bolt_t, flash_t = fx_guids("Projectile_Bolt_Tower", "Projectile_Flash_Tower")
    spits = spit_guids()

    # (에셋 경로, 탄환, 발사 섬광, 착탄 효과, 탄환 배율)
    #
    # 착탄 효과는 지금 프레이야만 갖고 있다 — 나머지는 원화가 없어 비어 있다.
    # 만들어지면 여기에 넣기만 하면 되고 연출 코드는 안 고친다.
    plan = [
        # 엘린·비기오르 — 지금까지 쓰던 천사 탄환을 각자의 스킨으로 옮긴다.
        ("Skins/Skin_Elin.asset", [bolt], [flash], [], 0.55),
        ("Skins/Skin_Bigior.asset", [bolt], [flash], [], 0.55),
        # 근거리 몬스터는 탄환을 쏘지 않는다 — 그래도 필드는 만들어 둔다(비어 있음이 명시적이게).
        ("MonsterSkins/Melee/Skin_HellFang.asset", [], [], [], 0.55),
        # 분비형 암세포의 침 9프레임. 마지막 두 장이 흩어지는 그림이라 착탄 효과가 따로 필요 없다.
        ("MonsterSkins/Ranged/Skin_SoulArcher.asset", spits, [], [], 0.35),
        # 포탑 레이저 (원화에서 오려낸 것 — 27-11절).
        ("BuildingSkins/Skin_Tower.asset", [bolt_t], [flash_t], [], 0.85),
    ]

    for rel, proj, muzzle, impact, scale in plan:
        path = os.path.join(RES, rel)
        tower = rel.startswith("BuildingSkins")
        fields = []
        if not tower:
            # 회복 모션은 캐릭터 스킨에만 있다 (건물은 회복 전술이 없다).
            fields.append(("healRight", sprite_list("healRight", [])))
            fields.append(("healLeft", sprite_list("healLeft", [])))
        fields.append(("projectileFrames", sprite_list("projectileFrames", proj)))
        fields.append(("muzzleFlashFrames", sprite_list("muzzleFlashFrames", muzzle)))
        fields.append(("impactFrames", sprite_list("impactFrames", impact)))
        fields.append(("projectileScale", f"  projectileScale: {scale}\n"))
        fields.append(("impactScale", "  impactScale: {x: 1, y: 1}\n"))
        # `projectileBurst` 는 섬광과 착탄을 한 필드로 쓰던 시절의 이름이다 — 남아 있으면 지운다.
        upsert_keys(path, fields, drop=["projectileBurst"])
        print(f"  {os.path.basename(rel)}: 탄환 {len(proj)} · 발사섬광 {len(muzzle)} · 착탄 {len(impact)}"
              f"{'' if tower else ' · 회복 비어있음(공격 모션으로 대체)'}")


def main():
    print("Skin_Preyja 생성:")
    write_preyja()
    print("기존 스킨에 투사체·회복 필드 추가:")
    patch_existing()


if __name__ == "__main__":
    main()
