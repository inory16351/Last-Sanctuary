# -*- coding: utf-8 -*-
"""`Skin_Kasinoma.asset` (CharacterSkinSO) 을 만든다 (2026-08-18).

`kasinoma_skin_build.py` 가 써놓은 프레임을 읽어 YAML 로 엮는다. 스프라이트 참조는 guid 로
넣고, guid 는 프레임 .meta 에서 읽는다 — `gen_malphas_skin.py` 와 완전히 같은 방식이다.

카시노마는 <b>웨이브 보스 120003</b> 라 `Resources/MonsterSkins/Kasinoma` 에 넣는다
(종마다 폴더 하나 — `CharacterAnimator.PickRandomSkin` 이 폴더 안에서 무작위로 고르므로
한 폴더에 몰아넣으면 다른 몬스터가 카시노마 외형으로 나온다).

무엇이 어디로 가나
-----------------
  · `idleRight/Left`       ← 대기 8장
  · `walkRight/Left`       ← 이동 7장
  · `attackRight/Left`     ← 근거리 공격(두 쌍의 팔 휘두르기) 9장
  · `skill1Right/Left`     ← 돌진 6장 + <b>착지 후 경직 4장</b> (130005 Lure_blood)
  · `skill1Fx`             ← 대시 위치 표시(타겟 마커) 6장   ← 돌진 <b>도착 지점</b> 연출
  · `skill1Projectile`     ← 대시 돌진 잔상 4장              ← 출발→도착 사이를 날아간다
  · `skill2Right/Left`     ← 6연타 6장 + <b>종료 후 경직 4장</b> (130006 Death_song)
  · `skill2Fx`             ← 6연타 휘두르기 이펙트 6장       ← 직선 범위 연출

⚠ <b>안 쓰는 원화</b> — 방향 전환(Turn 4장)은 뽑아만 두고 배선하지 않는다.
   `CharacterSkinSO` 에 「방향 전환」 칸이 없고, 이 프로젝트의 유닛은 좌우 반전으로
   방향을 바꾼다(69-6절). 칸이 생기면 `Char/Turn` 을 그대로 연결하면 된다.

⚠ <b>원거리 칸을 비운다</b> — 카시노마는 표에서 `atk_type = Melee` 다. 비워두면
   `CharacterAnimator` 가 근접 모션으로 떨어진다.

⚠ .asset YAML 에 <b>빈 줄을 넣으면</b> Unity 파서가 그 뒤 필드를 전부 무시한다(8절 3번).

사용법:  python Tools/gen_kasinoma_skin.py
다음:    python Tools/measure_skin_tiles.py  (contentSizeTiles 실측)
"""

import hashlib
import os
import sys

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                   "Char_Asset_Kasinoma", "Char")

REL_DIR = "Assets/_Project/Resources/MonsterSkins/Kasinoma"
REL_OUT = REL_DIR + "/Skin_Kasinoma.asset"
OUT = os.path.join(PROJECT, *REL_OUT.split("/"))

SCRIPT_GUID_CHARACTER_SKIN = "a517e511b352f46488ffa35edf32295d"   # CharacterSkinSO.cs

DISPLAY_NAME = "카시노마"


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
    prefix = "Char_%s_%s_" % (motion, side) if side else "Char_%s_" % motion
    names = sorted(n for n in os.listdir(folder)
                   if n.startswith(prefix) and n.endswith(".png"))
    return [guid_of(os.path.join(folder, n)) for n in names]


def fx_frames(name):
    """이펙트는 방향이 없다 — 조준 각도만큼 통째로 회전시켜 깔린다."""
    folder = os.path.join(ART, "Fx")
    if not os.path.isdir(folder):
        return []
    prefix = "Char_Fx_%s_" % name
    names = sorted(n for n in os.listdir(folder)
                   if n.startswith(prefix) and n.endswith(".png"))
    return [guid_of(os.path.join(folder, n)) for n in names]


def sprite_list(key, guids):
    if not guids:
        return "  %s: []\n" % key
    body = "  %s:\n" % key
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
    walk_r, walk_l = frames("Move", "Right"), frames("Move", "Left")
    melee_r, melee_l = frames("MeleeAttack", "Right"), frames("MeleeAttack", "Left")
    sk1_r, sk1_l = frames("Skill1", "Right"), frames("Skill1", "Left")
    sk2_r, sk2_l = frames("Skill2", "Right"), frames("Skill2", "Left")

    fx1 = fx_frames("DashMark")        # 돌진 도착 지점 표시 (원형 마커)
    fx2 = fx_frames("SixSlash")        # 6연타 휘두르기 (직선 범위)
    trail = fx_frames("DashTrail")     # 돌진 잔상 — 출발→도착 사이를 날아간다

    if not (idle_r and walk_r and melee_r):
        raise SystemExit("⚠ 프레임이 없습니다 — 먼저 Tools/kasinoma_skin_build.py 를 돌리세요")

    # ⚠ 여기서 `%` 서식을 쓰면 안 된다 — 첫 줄이 "%YAML 1.1" 이라 파이썬이 그 `%Y` 를
    #   서식 지시자로 읽고 죽는다(말파스에서 실제로 그랬다). f-string 으로 짠다.
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
        "  m_Name: Skin_Kasinoma\n"
        "  m_EditorClassIdentifier:\n"
        f"  displayName: {DISPLAY_NAME}\n"
        "  framesPerSecond: 10\n"
        "  attackFramesPerSecond: 14\n"
    )
    body += sprite_list("idleRight", idle_r)
    body += sprite_list("idleLeft", idle_l)
    body += sprite_list("walkRight", walk_r)
    body += sprite_list("walkLeft", walk_l)
    body += sprite_list("attackRight", melee_r)
    body += sprite_list("attackLeft", melee_l)
    body += sprite_list("rangedRight", [])
    body += sprite_list("rangedLeft", [])
    body += sprite_list("healRight", [])
    body += sprite_list("healLeft", [])
    body += sprite_list("reviveRight", [])
    body += sprite_list("reviveLeft", [])
    body += sprite_list("reviveFx", [])
    body += sprite_list("skill1Right", sk1_r)
    body += sprite_list("skill1Left", sk1_l)
    body += sprite_list("skill2Right", sk2_r)
    body += sprite_list("skill2Left", sk2_l)
    body += sprite_list("skill1Fx", fx1)
    body += sprite_list("skill2Fx", fx2)
    body += sprite_list("skill1Projectile", trail)
    body += sprite_list("skill2Projectile", [])
    body += sprite_list("projectileFrames", [])
    body += sprite_list("muzzleFlashFrames", [])
    body += sprite_list("impactFrames", [])

    body += (
        "  projectileScale: 1\n"
        "  impactScale: {x: 1, y: 1}\n"
        # 아래 네 줄은 Tools/measure_skin_tiles.py 가 알파 경계로 실측해 덮어쓴다.
        "  contentSizeTiles: {x: 0, y: 0}\n"
        "  projectileSizeTiles: {x: 0, y: 0}\n"
        "  impactSizeTiles: {x: 0, y: 0}\n"
        "  projectileWidthTiles: 0\n"
        "  impactWidthTiles: 0\n"
        "  impactFlattenY: 1\n"
    )

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    ensure_folder_meta(os.path.dirname(OUT), REL_DIR)

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(body)
    with open(OUT + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write("fileFormatVersion: 2\n"
                "guid: %s\n" % md5_guid(REL_OUT) +
                "NativeFormatImporter:\n"
                "  externalObjects: {}\n"
                "  mainObjectFileID: 11400000\n"
                "  userData:\n"
                "  assetBundleName:\n"
                "  assetBundleVariant:\n")

    print("  대기        %2d프레임 (좌우)" % len(idle_r))
    print("  이동        %2d프레임 (좌우)" % len(walk_r))
    print("  근접 공격   %2d프레임 (좌우)" % len(melee_r))
    print("  스킬1 혈취  %2d프레임 (좌우) · 마커 %d · 잔상 %d"
          % (len(sk1_r), len(fx1), len(trail)))
    print("  스킬2 노래  %2d프레임 (좌우) · 범위 연출 %d" % (len(sk2_r), len(fx2)))
    total = (idle_r + idle_l + walk_r + walk_l + melee_r + melee_l +
             sk1_r + sk1_l + sk2_r + sk2_l + fx1 + fx2 + trail)
    print("  스프라이트 참조 %d개" % len(total))
    print("→", REL_OUT)
    print("다음: python Tools/measure_skin_tiles.py  (contentSizeTiles 실측)")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
