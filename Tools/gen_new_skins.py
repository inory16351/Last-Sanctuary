# -*- coding: utf-8 -*-
"""표에 새로 들어온 유닛의 스킨 에셋을 만든다 — 피올로 9004 · 단탈리온 120001 (2026-08-12).

**테이블이 정본**이라는 유저 확정에 따라 스킨 이름을 표의 `ingame_asset`
(`Char_Asset_Piolo` / `Char_Asset_Dantalian`)에서 그대로 따온다.

원화는 `Tools/import_char_asset.py` 가 먼저 Unity 로 들여와야 한다(프레임 PNG + .meta).

⚠ **`gen_skin_assets.py` 를 고치지 않고 새 파일로 뺐다** — 그 스크립트는 프레이야 스킨을
  통째로 다시 쓰고 기존 스킨 5개에 필드를 덧붙이는 일을 한다. 손으로 다듬은 프레임이
  있어서(30절의 분비형 암세포) 새 유닛을 추가할 때마다 그걸 같이 돌리는 것은 위험하다.
  이 파일은 **새 스킨 2개만** 쓴다.

⚠ MCP 에는 SO 에셋을 만드는 도구도, 스프라이트 배열 참조를 넣는 도구도 없다(8절 1·4번).
  스프라이트 목록은 guid 참조라 반드시 파일로 써야 한다 — 씬 오브젝트는 MCP 로 만든다.
⚠ .asset YAML 에 빈 줄을 넣으면 Unity 파서가 그 뒤 필드를 전부 무시한다(8절 3번).
"""
import os
import sys
import hashlib

# 콘솔이 cp949 라 한글·기호 출력에서 죽는다 — 출력만 UTF-8 로 바꾼다(파일 내용과 무관).
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset")
RES = os.path.join(PROJECT, "Assets", "_Project", "Resources")

SCRIPT_GUID_CHARACTER_SKIN = "a517e511b352f46488ffa35edf32295d"   # CharacterSkinSO.cs

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


def guid_of(asset_path):
    """.meta 에서 guid 를 읽는다. 없으면 죽는다 — 조용히 빈 참조를 넣으면 안 된다."""
    with open(asset_path + ".meta", encoding="utf-8") as f:
        for line in f:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    raise RuntimeError("guid 를 찾지 못했습니다: " + asset_path)


def frame_guids(char_folder, motion, side):
    """Art/Char_Asset/<char>/Char/<motion>/ 의 방향별 프레임 guid를 번호순으로."""
    if not motion:
        return []
    folder = os.path.join(ART, char_folder, "Char", motion)
    if not os.path.isdir(folder):
        return []
    key = "_%s_" % side if side else None
    names = sorted(n[:-4] for n in os.listdir(folder)
                   if n.endswith(".png") and (key in n if key else True))
    return [guid_of(os.path.join(folder, n + ".png")) for n in names]


def sprite_list(key, guids):
    """빈 목록은 `key: []` 로 — 빈 줄을 만들지 않는다."""
    if not guids:
        return "  %s: []\n" % key
    body = "  %s:\n" % key
    for g in guids:
        body += "  - {fileID: 21300000, guid: %s, type: 3}\n" % g
    return body


def write_skin(char, name, display, fps, atk_fps, motions, extra="", folder="Skins"):
    """
    ⚠ <paramref name="folder"/> 는 <b>Resources 아래의 어느 폴더에 쓸지</b>다.
      `CharacterAnimator` 는 폴더로 후보를 가른다 — 캐릭터는 `Skins`, 몬스터는 `MonsterSkins`.
      **몬스터 스킨을 `Skins` 에 쓰면 캐릭터가 그 외형으로 뽑힌다**(무작위 추첨이다).
      실제로 이 스크립트가 단탈리온을 `Skins` 에 쓰고 있었고, 누군가 손으로 옮겨서
      드러나지 않고 있었다 — 다시 돌리면 그 자리에 유령 사본이 되살아난다(2026-08-13 수정).
    """
    rel = "Resources/%s/%s.asset" % (folder, name)
    body = HEADER.format(script_guid=SCRIPT_GUID_CHARACTER_SKIN, name=name)
    body += "  displayName: %s\n" % display
    body += "  framesPerSecond: %s\n" % fps
    body += "  attackFramesPerSecond: %s\n" % atk_fps
    for key, (motion, side) in motions.items():
        body += sprite_list(key, frame_guids(char, motion, side))
    body += extra

    out_dir = os.path.join(RES, *folder.split("/"))
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, name + ".asset")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(body)

    # .meta 는 **없을 때만** 만든다 — 이미 있는 에셋의 guid 를 갈아치우면 그걸 참조하던
    # 곳이 전부 끊긴다(U-D2).
    if not os.path.exists(path + ".meta"):
        with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write(ASSET_META.format(
                guid=hashlib.md5(("LastSanctuary/" + rel).encode()).hexdigest()))

    counts = {k: len(frame_guids(char, m, s)) for k, (m, s) in motions.items() if m}
    print("  %s (%s): 생성 %s" % (name, folder, counts))


def write_piolo():
    """
    피올로 9004. ★ <b>회복 전용 모션을 가진 첫 캐릭터</b>다 — 38-7절이 "전용 원화가 있는
    캐릭터는 아직 없다" 로 남겨둔 칸이 이제 채워진다(healRight/healLeft).
    피올로는 회복력 11 로 4명 중 최고이고 패시브 3종이 전부 지원형이라(부식·정신 안정·
    정화의 손길) 실제로 치유 유형으로 쓰일 캐릭터다.

    투사체는 전용 원화가 없어 비운다 — CombatProjectileFx 의 폴백 탄환이 쓰인다.
    """
    write_skin(
        "Char_Asset_Piolo", "Skin_Piolo", "피올로", 10, 14,
        {
            "idleRight":   ("Idle", "Right"),
            "idleLeft":    ("Idle", "Left"),
            "walkRight":   ("Walk", "Right"),
            "walkLeft":    ("Walk", "Left"),
            "attackRight": ("MeleeAttack", "Right"),
            "attackLeft":  ("MeleeAttack", "Left"),
            "rangedRight": ("RangedAttack", "Right"),
            "rangedLeft":  ("RangedAttack", "Left"),
            "healRight":   ("Heal", "Right"),
            "healLeft":    ("Heal", "Left"),
            "projectileFrames": (None, None),
            "muzzleFlashFrames": (None, None),
            "impactFrames": (None, None),
        },
        extra="  projectileScale: 0.55\n  impactScale: {x: 1, y: 1}\n")


def write_dantalian():
    """
    최종보스 단탈리온 120001. 원화의 `Move` 는 `Walk` 로 들여왔다(이 프로젝트의 모션 이름).

    ★ **보스 스킬 2종의 원화를 배선한다 (2026-08-13)** — 예전에는
      "CharacterSkinSO 에 스킬 모션 칸이 없고 보스 스킬 자체가 미구현(미결 111번)"이라
      임포트만 해두고 놀리고 있었다. 이제 칸이 생겼고(`skill1*`/`skill2*`) 발동시키는
      코드(`BossSkillCaster`)도 있다.

        슬롯 0 = 표의 boss_skill_1 = 130001 타락한 무덤 → SpecialShockwave + ShockwaveFx
        슬롯 1 = 표의 boss_skill_2 = 130002 공허의 광선 → SpecialBeam       + BeamFx

      ⚠ **슬롯 순서가 표의 순서와 같아야 한다** — `MonsterDefinitionSO.bossSkillIds` 의
        인덱스가 곧 이 슬롯 번호다. 표에서 두 스킬의 순서를 바꾸면 여기도 같이 바꿔야
        모션이 안 어긋난다.
      ⚠ 지면 연출(`skill*Fx`)은 Fx 폴더에서 **파일 이름으로** 고른다 — `impactFrames` 가
        폴더 전체를 쓰고 있어서 그대로 두면 빔과 충격파가 섞인다.

    ⚠ Fx 3프레임은 착탄 연출로도 붙어 있다 — 근거리 보스라 탄환은 없지만 맞는 자리에
      뭔가 보이는 편이 낫다(실제로는 근거리라 CombatProjectileFx 가 그리지 않는다).
    """
    write_skin(
        "Char_Asset_Dantalian", "Skin_Dantalian", "단탈리온", 8, 10,
        {
            "idleRight":   ("Idle", "Right"),
            "idleLeft":    ("Idle", "Left"),
            "walkRight":   ("Walk", "Right"),
            "walkLeft":    ("Walk", "Left"),
            "attackRight": ("MeleeAttack", "Right"),
            "attackLeft":  ("MeleeAttack", "Left"),
            "rangedRight": (None, None),
            "rangedLeft":  (None, None),
            "healRight":   (None, None),
            "healLeft":    (None, None),
            "skill1Right": ("SpecialShockwave", "Right"),
            "skill1Left":  ("SpecialShockwave", "Left"),
            "skill2Right": ("SpecialBeam", "Right"),
            "skill2Left":  ("SpecialBeam", "Left"),
            "projectileFrames": (None, None),
            "muzzleFlashFrames": (None, None),
            "impactFrames": ("Fx", ""),
        },
        extra=(sprite_list("skill1Fx", frame_guids("Char_Asset_Dantalian", "Fx", "ShockwaveFx")) +
               sprite_list("skill2Fx", frame_guids("Char_Asset_Dantalian", "Fx", "BeamFx")) +
               "  projectileScale: 0.55\n  impactScale: {x: 1, y: 0.75}\n"),
        folder="MonsterSkins/Dantalian")


if __name__ == "__main__":
    print("새 스킨 생성:")
    write_piolo()
    write_dantalian()
    # ⚠ 이 스크립트는 스킨 에셋을 **통째로 다시 쓴다** — 실측 크기(contentSizeTiles 등)가
    #   같이 날아간다. 반드시 이어서 measure 를 돌려야 유닛 크기가 정상으로 돌아온다
    #   (64절에서 파이프라인이 크기 값을 날려 하드코딩으로 때웠던 바로 그 사고다).
    print("\n완료 — 이어서 `python Tools/measure_skin_tiles.py` 를 돌리고, "
          "Unity 에서 Assets/Refresh 를 실행할 것.")
