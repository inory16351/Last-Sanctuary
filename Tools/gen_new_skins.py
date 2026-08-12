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
import hashlib

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


def write_skin(char, name, display, fps, atk_fps, motions, extra=""):
    rel = "Resources/Skins/%s.asset" % name
    body = HEADER.format(script_guid=SCRIPT_GUID_CHARACTER_SKIN, name=name)
    body += "  displayName: %s\n" % display
    body += "  framesPerSecond: %s\n" % fps
    body += "  attackFramesPerSecond: %s\n" % atk_fps
    for key, (motion, side) in motions.items():
        body += sprite_list(key, frame_guids(char, motion, side))
    body += extra

    path = os.path.join(RES, "Skins", name + ".asset")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(body)
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(ASSET_META.format(
            guid=hashlib.md5(("LastSanctuary/" + rel).encode()).hexdigest()))

    counts = {k: len(frame_guids(char, m, s)) for k, (m, s) in motions.items() if m}
    print("  %s: 생성 %s" % (name, counts))


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

    ⚠ SpecialBeam/SpecialShockwave(보스 스킬 2종의 원화)는 <b>스킨에 배선하지 않는다</b> —
      CharacterSkinSO 에 스킬 모션 칸이 없고 보스 스킬 자체가 미구현이다(미결 111번).
      프레임은 Art 에 임포트만 해뒀으니 구현할 때 아트를 다시 찾을 필요는 없다.
    ⚠ Fx 3프레임은 착탄 연출로 붙였다 — 근거리 보스라 탄환은 없지만 맞는 자리에
      뭔가 보이는 편이 낫다.
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
            "projectileFrames": (None, None),
            "muzzleFlashFrames": (None, None),
            "impactFrames": ("Fx", ""),
        },
        extra="  projectileScale: 0.55\n  impactScale: {x: 1, y: 0.75}\n")


if __name__ == "__main__":
    print("새 스킨 생성:")
    write_piolo()
    write_dantalian()
    print("\n완료 — Unity 에서 Assets/Refresh 를 실행할 것.")
