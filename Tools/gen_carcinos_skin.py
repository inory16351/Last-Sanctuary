# -*- coding: utf-8 -*-
"""`Skin_Carcinos.asset` (CharacterSkinSO) 을 만든다 (2026-08-15).

`carcinos_skin_build.py` 가 써놓은 프레임을 읽어 YAML 로 엮는다. 스프라이트 참조는 guid 로
넣고, guid 는 프레임 .meta 에서 읽는다 — `gen_histon_skin.py` 와 완전히 같은 방식이다.

★ 출력 위치가 <b>캐릭터 스킨과 다르다</b>
------------------------------------------
카르시노스는 캐릭터가 아니라 <b>중립 몬스터(1004 에픽 보스)</b> 다. 그래서
`Resources/Skins`(캐릭터) 가 아니라 <b>`Resources/MonsterSkins/Carcinos`</b> 에 넣는다.

<b>왜 종마다 하위 폴더인가</b> — `CharacterAnimator.PickRandomSkin` 은 자기
`skinResourceFolder` 안의 스킨 중 <b>무작위로</b> 하나를 고른다. 몬스터 스킨을 한 폴더에
몰아넣으면 지옥 송곳니가 카르시노스 외형으로 나올 수 있다. 그래서 기존 몬스터도
`MonsterSkins/HellFang` · `MonsterSkins/SoulArcher` · `MonsterSkins/Dantalian` 처럼
<b>종마다 폴더 하나</b>이고, 템플릿의 `skinResourceFolder` 가 그 폴더를 가리켜
후보가 언제나 한 개가 되게 해 둔다. 같은 규약을 따른다.

⚠ 이번에 넣는 모션은 <b>대기·이동·근접 공격 3종뿐</b>이다 (유저 지시 — 스킬이 아직
   표에 없다). 원거리·회복·스킬·투사체 칸은 비운다:
     · 원거리/회복이 비면 `CharacterSkinSO.Attack` 이 근접 모션으로 폴백한다
     · 스킬 칸이 비면 `CharacterAnimator.PlaySkillMotion` 이 평타로 폴백한다
   스킬을 넣을 때는 `carcinos_skin_build.py` 에 Skill1·Skill2 행을 켜고
   (원본 시트에 이미 들어 있다) 여기에 `skill1*`/`skill2*` 를 채우면 된다.
   ⚠ 스킬 <b>이펙트</b> 원화는 `Carcinos_asset_01.png` 의 맨 아래 분리된 행에 있다.

⚠ .asset YAML 에 <b>빈 줄을 넣으면</b> Unity 파서가 그 뒤 필드를 전부 무시한다(진행상황 8절 3번).

사용법:  python Tools/gen_carcinos_skin.py
"""

import os
import hashlib

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                   "Char_Asset_Carcinos", "Char")

REL_OUT = "Assets/_Project/Resources/MonsterSkins/Carcinos/Skin_Carcinos.asset"
OUT = os.path.join(PROJECT, *REL_OUT.split("/"))

SCRIPT_GUID_CHARACTER_SKIN = "a517e511b352f46488ffa35edf32295d"   # CharacterSkinSO.cs

DISPLAY_NAME = "카르시노스"


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


def fx_frames(name):
    """이펙트는 방향이 없다 — `Char/Fx/Char_Fx_<이름>_NN.png` 한 벌."""
    folder = os.path.join(ART, "Fx")
    if not os.path.isdir(folder):
        return []
    prefix = f"Char_Fx_{name}_"
    names = sorted(n for n in os.listdir(folder)
                   if n.startswith(prefix) and n.endswith(".png"))
    return [guid_of(os.path.join(folder, n)) for n in names]


def main():
    idle_r, idle_l = frames("Idle", "Right"), frames("Idle", "Left")
    walk_r, walk_l = frames("Walk", "Right"), frames("Walk", "Left")
    atk_r, atk_l = frames("MeleeAttack", "Right"), frames("MeleeAttack", "Left")

    # ★ 2026-08-15 — 스킬 두 벌이 들어왔다 (표의 2001 할퀴기 · 2002 죽음의 포효).
    #   슬롯 번호는 정의의 skillIds 순서와 같다: 슬롯 0 = 2001, 슬롯 1 = 2002.
    sk1_r, sk1_l = frames("Skill1", "Right"), frames("Skill1", "Left")
    sk2_r, sk2_l = frames("Skill2", "Right"), frames("Skill2", "Left")
    fx1, fx2 = fx_frames("Skill1"), fx_frames("Skill2")

    if not (idle_r and walk_r and atk_r):
        raise SystemExit("⚠ 프레임이 없습니다 — 먼저 Tools/carcinos_skin_build.py 를 돌리세요")

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
        "  m_Name: Skin_Carcinos\n"
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

    # 아직 원화를 안 넣은 칸 — 비어 있으면 각자 폴백이 걸린다(맨 위 주석 참조).
    for key in ("rangedRight", "rangedLeft", "healRight", "healLeft",
                "reviveRight", "reviveLeft", "reviveFx",
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
    ensure_folder_meta(os.path.dirname(OUT),
                       "Assets/_Project/Resources/MonsterSkins/Carcinos")

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

    print(f"  대기      {len(idle_r)}프레임 (좌우)")
    print(f"  이동      {len(walk_r)}프레임 (좌우)")
    print(f"  근접 공격 {len(atk_r)}프레임 (좌우)")
    print(f"  스킬1     {len(sk1_r)}프레임 (좌우) · 이펙트 {len(fx1)}프레임")
    print(f"  스킬2     {len(sk2_r)}프레임 (좌우) · 이펙트 {len(fx2)}프레임")
    total = (idle_r + idle_l + walk_r + walk_l + atk_r + atk_l +
             sk1_r + sk1_l + sk2_r + sk2_l + fx1 + fx2)
    print(f"  스프라이트 참조 {len(total)}개")
    print("→", REL_OUT)
    print("다음: python Tools/measure_skin_tiles.py  (contentSizeTiles 실측)")


if __name__ == "__main__":
    import sys
    sys.stdout.reconfigure(encoding="utf-8")
    main()
