# -*- coding: utf-8 -*-
"""`Skin_Nexus.asset` (NexusSkinSO) 을 만든다 (2026-08-18).

`nexus_skin_build.py` 가 써놓은 프레임을 읽어 YAML 로 엮는다. 스프라이트 참조는 guid 로
넣고, guid 는 프레임 .meta 에서 읽는다 — `gen_kasinoma_skin.py` 와 같은 방식이다.

무엇이 어디로 가나
-----------------
  · `idleHigh`  ← 체력 50% 이상   6장
  · `idleMid`   ← 체력 10~50%     6장
  · `idleLow`   ← 체력 10% 이하   6장
  · `destroy`   ← 파괴            8장

⚠ `framesPerSecond: 7` — 원화 시트의 지시(*"Unity에서 프레임 속도 6~8 FPS 권장"*).
   실제 재생 속도는 `NexusAnimator` 가 <b>체력에 따라 다시 늘린다</b>(빈사일수록 느리게).

⚠ .asset YAML 에 <b>빈 줄을 넣으면</b> Unity 파서가 그 뒤 필드를 전부 무시한다(8절 3번).

사용법:  python Tools/gen_nexus_skin.py
"""

import hashlib
import os
import sys

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset", "Char_Asset_Nexus")

REL_DIR = "Assets/_Project/Resources/BuildingSkins/Nexus"
REL_OUT = REL_DIR + "/Skin_Nexus.asset"
OUT = os.path.join(PROJECT, *REL_OUT.split("/"))

#: NexusSkinSO.cs 의 guid — .meta 에서 읽는다(하드코딩하면 스크립트를 옮길 때 끊긴다).
SCRIPT_META = os.path.join(PROJECT, "Assets", "_Project", "Scripts", "Units",
                           "NexusSkinSO.cs.meta")


def guid_of_meta(path):
    with open(path, encoding="utf-8") as f:
        for line in f:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    raise RuntimeError("guid 를 찾지 못했습니다: " + path)


def guid_of(path):
    return guid_of_meta(path + ".meta")


def frames(motion):
    folder = os.path.join(ART, motion)
    if not os.path.isdir(folder):
        return []
    names = sorted(n for n in os.listdir(folder)
                   if n.startswith("Nexus_%s_" % motion) and n.endswith(".png"))
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
    high = frames("IdleHigh")
    mid = frames("IdleMid")
    low = frames("IdleLow")
    destroy = frames("Destroy")

    if not high:
        raise SystemExit("⚠ 프레임이 없습니다 — 먼저 Tools/nexus_skin_build.py 를 돌리세요")

    script_guid = guid_of_meta(SCRIPT_META)

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
        f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
        "  m_Name: Skin_Nexus\n"
        "  m_EditorClassIdentifier:\n"
        "  displayName: 중앙 건물\n"
        "  framesPerSecond: 7\n"
    )
    body += sprite_list("idleHigh", high)
    body += sprite_list("idleMid", mid)
    body += sprite_list("idleLow", low)
    body += sprite_list("destroy", destroy)

    # 폴더 두 겹(BuildingSkins · BuildingSkins/Nexus)의 meta 를 모두 보장한다.
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    ensure_folder_meta(os.path.dirname(os.path.dirname(OUT)),
                       "Assets/_Project/Resources/BuildingSkins")
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

    print("  체력 50%% 이상  %d프레임" % len(high))
    print("  체력 10~50%%    %d프레임" % len(mid))
    print("  체력 10%% 이하  %d프레임" % len(low))
    print("  파괴           %d프레임" % len(destroy))
    print("→", REL_OUT)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
