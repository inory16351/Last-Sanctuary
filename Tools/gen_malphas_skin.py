# -*- coding: utf-8 -*-
"""`Skin_Malphas.asset` (CharacterSkinSO) 을 만든다 (2026-08-18).

`malphas_skin_build.py` 가 써놓은 프레임을 읽어 YAML 로 엮는다. 스프라이트 참조는 guid 로
넣고, guid 는 프레임 .meta 에서 읽는다 — `gen_carcinos_skin.py` 와 완전히 같은 방식이다.

말파스는 <b>웨이브 최종보스 120002</b> 라 `Resources/MonsterSkins/Malphas` 에 넣는다
(종마다 폴더 하나 — `CharacterAnimator.PickRandomSkin` 이 폴더 안에서 무작위로 고르므로
한 폴더에 몰아넣으면 지옥 송곳니가 말파스 외형으로 나온다).

★ 원화가 <b>단탈리온보다 많이 들어 있다</b>
------------------------------------------
단탈리온 스킨은 원거리·투사체 칸이 비어 있지만(근접 보스라 필요가 없었다), 말파스는
표에서 `atk_type = Ranged` 이고 시트에 **원거리 공격 모션 + 검은 구체 투사체 + 착탄**이
전부 그려져 있다. 그래서:

  · `rangedRight/Left`     ← 원거리 공격(검은 구체 발사) 모션
  · `attackRight/Left`     ← 근거리 공격 모션 (붙었을 때 · 폴백)
  · `projectileFrames`     ← 기본 원거리 투사체(검은 구체) 8장
  · `impactFrames`         ← 레이저 임팩트 4장
  · `skill1Right/Left`     ← 구속탄 발사 모션 (130003 Binding_orb)
  · `skill1Fx`             ← 구속탄 피격/범위 폭발 10장  ← 원형 범위 연출
  · `skill2Right/Left`     ← 저주광선 발사 모션 (130004 Curse_beam)
  · `skill2Fx`             ← 저주광선(레이저) 8장       ← 직선 범위 연출

★ 2026-08-19 — <b>저주광선이 8장이 됐다</b> (유저 지시: *"레이저를 하나의 이미지로 하지 말고
스프라이트 이미지 하나하나 잘라서 점진적으로 발사되는거 처럼"*). 예전에는 통짜 한 장이라
시전하는 순간 이미 다 뻗은 빔이 툭 나타났다. 이제 `malphas_skin_build.build_beam` 이
<b>총구 섬광 4장 + 자라나는 빔 4장</b>을 같은 캔버스로 구워주고, `CombatProjectileFx.PlayArea`
가 시전 시간 전체에 고르게 펼쳐 재생한다 — 여기서는 <b>폴더에 있는 장수를 그대로 읽으므로
이 파일은 안 바뀐다.</b>

⚠ <b>안 쓰는 원화</b> — 피격/사망 모션(Hit 11장)과 구속탄 투사체(초록 구체 7장)는
   뽑아만 두고 배선하지 않는다. `CharacterSkinSO` 에 사망 모션 칸이 없고, 투사체 칸도
   <b>한 벌뿐</b>이라 평타용 검은 구체와 스킬용 초록 구체를 동시에 넣을 수 없다.
   칸이 생기면 `Char/Hit` · `Char/Fx/Char_Fx_BindingOrb_*` 를 그대로 연결하면 된다.

⚠ .asset YAML 에 <b>빈 줄을 넣으면</b> Unity 파서가 그 뒤 필드를 전부 무시한다(8절 3번).

사용법:  python Tools/gen_malphas_skin.py
다음:    python Tools/measure_skin_tiles.py  (contentSizeTiles 실측)
"""

import hashlib
import os
import sys

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset",
                   "Char_Asset_Malphas", "Char")

REL_DIR = "Assets/_Project/Resources/MonsterSkins/Malphas"
REL_OUT = REL_DIR + "/Skin_Malphas.asset"
OUT = os.path.join(PROJECT, *REL_OUT.split("/"))

SCRIPT_GUID_CHARACTER_SKIN = "a517e511b352f46488ffa35edf32295d"   # CharacterSkinSO.cs

DISPLAY_NAME = "말파스"


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
    rng_r, rng_l = frames("RangedAttack", "Right"), frames("RangedAttack", "Left")
    sk1_r, sk1_l = frames("Skill1", "Right"), frames("Skill1", "Left")
    sk2_r, sk2_l = frames("Skill2", "Right"), frames("Skill2", "Left")

    fx1 = frames("FxBindingOrb")                 # 구속탄 범위 폭발 (원형)
    fx2 = fx_frames("CurseBeam")                 # 저주광선 (직선) — 통짜 한 장
    projectile = fx_frames("Projectile")         # 기본 원거리 투사체 (검은 구체)
    impact = fx_frames("Impact")                 # 레이저 임팩트

    # ★ 2026-08-18 — 구속탄 <b>탄환</b>(초록 구체). 예전에는 뽑아만 두고 배선할 칸이
    #   없어서 놀고 있었다(맨 위 주석 29줄). CharacterSkinSO.skill1Projectile 신설로 연결.
    #   ⚠ 위 fx1(FxBindingOrb) 은 <b>터질 때</b>의 범위 폭발이고 이건 <b>날아가는</b> 탄환이다.
    sk1_projectile = fx_frames("BindingOrb")

    if not (idle_r and walk_r and rng_r):
        raise SystemExit("⚠ 프레임이 없습니다 — 먼저 Tools/malphas_skin_build.py 를 돌리세요")

    # ⚠ 여기서 `%` 서식을 쓰면 안 된다 — 첫 줄이 "%YAML 1.1" 이라 파이썬이 그 `%Y` 를
    #   서식 지시자로 읽고 죽는다(실제로 그랬다). f-string 으로 짠다.
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
        "  m_Name: Skin_Malphas\n"
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
    body += sprite_list("rangedRight", rng_r)
    body += sprite_list("rangedLeft", rng_l)
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
    body += sprite_list("skill1Projectile", sk1_projectile)
    body += sprite_list("skill2Projectile", [])
    body += sprite_list("projectileFrames", projectile)
    body += sprite_list("muzzleFlashFrames", [])
    body += sprite_list("impactFrames", impact)

    body += (
        "  projectileScale: 0.55\n"
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
    print("  원거리 공격 %2d프레임 (좌우)" % len(rng_r))
    print("  스킬1 구속탄   %2d프레임 (좌우) · 범위 연출 %d프레임" % (len(sk1_r), len(fx1)))
    print("  스킬2 저주광선 %2d프레임 (좌우) · 범위 연출 %d프레임" % (len(sk2_r), len(fx2)))
    print("  투사체 %d · 착탄 %d" % (len(projectile), len(impact)))
    total = (idle_r + idle_l + walk_r + walk_l + melee_r + melee_l + rng_r + rng_l +
             sk1_r + sk1_l + sk2_r + sk2_l + fx1 + fx2 + projectile + impact)
    print("  스프라이트 참조 %d개" % len(total))
    print("→", REL_OUT)
    print("다음: python Tools/measure_skin_tiles.py  (contentSizeTiles 실측)")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
