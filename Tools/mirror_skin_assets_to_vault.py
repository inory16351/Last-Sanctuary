# -*- coding: utf-8 -*-
"""잘라낸 스킨 프레임 폴더를 볼트 `리소스/` 에도 복사해 정리한다 (2026-08-15).

유저 지시: *"리소스에도 만든 에셋 폴더 들 복사해서 정리"*

무엇을 왜
---------
볼트 `리소스/asset/` 은 <b>진영별로</b> 정리돼 있다:

    리소스/asset/char_asset/            아군 캐릭터 (Char_Asset_Elin · Bigior · …)
    리소스/asset/monster_cancer_asset/  웨이브(암세포) 몬스터 (Dantalian · HellFang · SoulArcher)
    리소스/asset/Tower_Asset/           건물

<b>중립 몬스터 자리가 없었다.</b> 그래서 이번에 만든 네 종(종양 거미·종양귀·종양 두더지·
카르시노스)이 유니티 프로젝트 안에만 있고 볼트에는 <b>한 장짜리 모션 시트만</b> 있는 상태였다.
시트만 남으면 "몇 프레임으로 어떻게 잘랐는지"가 볼트에서 사라진다 — 원화 담당자가
볼트만 열었을 때 결과물을 볼 수 없다.

    리소스/asset/monster_neutral_asset/Char_Asset_<종>/Char/<모션>/…

⚠ <b>단방향이다</b> — 유니티 → 볼트로만 복사한다. 정본은 `Tools/*_skin_build.py` 가 만드는
  유니티 쪽이고, 볼트 사본은 <b>보기용</b>이다. 반대로 옮기면 어느 쪽이 정본인지 갈린다.
⚠ `.meta` 는 복사하지 않는다 — 유니티 밖에서는 뜻이 없고, 볼트에 두면 나중에 누가
  실수로 되돌려 복사했을 때 guid 가 꼬인다.
⚠ 대상 폴더를 <b>통째로 지우고 다시 쓴다</b>. 그래야 프레임 수가 줄었을 때 옛 파일이
  남지 않는다(멱등). 지우는 범위는 아래 `SPECIES` 에 적힌 폴더뿐이다.

사용법:  py -3 Tools/mirror_skin_assets_to_vault.py
"""

import os
import shutil
import sys

from vault_path import VAULT, PROJECT

SRC_ROOT = os.path.join(PROJECT, "Assets", "_Project", "Art", "Char_Asset")
DST_ROOT = os.path.join(VAULT, "리소스", "asset", "monster_neutral_asset")

#: 볼트로 복사할 종 (중립 몬스터 4종)
SPECIES = ["TumorSpider", "Tumorling", "TumorMole", "Carcinos"]

#: 종 폴더가 아닌 것들 — (유니티 쪽 폴더, 볼트 쪽 폴더 이름)
EXTRA = [
    (os.path.join(PROJECT, "Assets", "_Project", "Art", "OrganicTilemap", "CarcinosHabitat"),
     "CarcinosHabitat_tiles"),
    (os.path.join(PROJECT, "Assets", "_Project", "Art", "OrganicTilemap", "CarcinosHabitatEdge"),
     "CarcinosHabitat_edge"),
    (os.path.join(PROJECT, "Assets", "_Project", "Art", "OrganicTilemap", "CarcinosHabitatProps"),
     "CarcinosHabitat_props"),
]


def mirror(species):
    src = os.path.join(SRC_ROOT, "Char_Asset_" + species)
    if not os.path.isdir(src):
        print(f"  ⚠ {species}: 원본 폴더가 없습니다 — {src}")
        return 0

    dst = os.path.join(DST_ROOT, "Char_Asset_" + species)
    if os.path.isdir(dst):
        shutil.rmtree(dst)

    count = 0
    for root, dirs, files in os.walk(src):
        rel = os.path.relpath(root, src)
        out_dir = dst if rel == "." else os.path.join(dst, rel)
        pngs = [f for f in files if f.lower().endswith(".png")]
        if not pngs:
            continue
        os.makedirs(out_dir, exist_ok=True)
        for f in pngs:
            shutil.copy2(os.path.join(root, f), os.path.join(out_dir, f))
            count += 1

    motions = sorted(d for d in os.listdir(dst)) if os.path.isdir(dst) else []
    print(f"  {species:<12} {count:3d}장  ({' · '.join(motions) if motions else '없음'})")
    return count


def mirror_dir(src, out_name):
    """종 폴더가 아닌 낱장 묶음(서식지 타일 등)을 통째로 복사한다."""
    if not os.path.isdir(src):
        print(f"  ⚠ {out_name}: 원본 폴더가 없습니다 — {src}")
        return 0

    dst = os.path.join(DST_ROOT, out_name)
    if os.path.isdir(dst):
        shutil.rmtree(dst)
    os.makedirs(dst, exist_ok=True)

    count = 0
    for f in sorted(os.listdir(src)):
        if not f.lower().endswith(".png"):
            continue
        shutil.copy2(os.path.join(src, f), os.path.join(dst, f))
        count += 1
    print(f"  {out_name:<24} {count:3d}장")
    return count


def main():
    os.makedirs(DST_ROOT, exist_ok=True)
    total = sum(mirror(s) for s in SPECIES)
    total += sum(mirror_dir(src, name) for src, name in EXTRA)
    print(f"\n{total}장 복사 → {DST_ROOT}")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
