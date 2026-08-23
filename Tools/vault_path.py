# -*- coding: utf-8 -*-
"""
볼트(데이터 테이블·원화가 있는 폴더) 위치를 찾는 <b>단 하나의 자리</b>.

★ 왜 이 파일이 생겼나 (2026-08-15)
---------------------------------
`Tools/` 의 거의 모든 스크립트가 볼트 경로를 ``C:\\Project\\Last-Sanctuary-Vault`` 로
**하드코딩**하고 있었다. 그런데 그 경로가 없는 PC 가 실재한다:

  · 2026-08-15 A : 볼트가 ``H:\\c팀\\Last-Sanctuary-Vault``
  · 2026-08-15 B : 볼트가 ``H:\\Last sanctuary\\Last-Sanctuary-Vault``

그 PC 에서는 표 파싱 파이프라인이 **전부 "파일 없음" 으로 죽는다.** 실제로 그래서
표를 고쳐도 게임에 반영되지 않는 상태가 이어졌다(중립 몬스터 에셋이 표보다 뒤처져
있던 것이 그 결과다).

`histon_skin_build.py` 가 같은 문제를 만나 ``find_vault()`` 를 자기 안에 만들어 뒀는데,
**한 파일에만 있어서** 나머지는 여전히 깨져 있었다. 그 함수를 여기로 옮기고 전부
이 모듈을 쓰게 한다 — 경로 규칙이 두 벌로 갈리지 않게.

⚠ `gen_character_assets.py` 가 남긴 교훈과 같은 종류의 문제다:
  *"예전에는 `C:\\Project\\Last Sanctuary` 로 박아뒀는데 실제 폴더는 하이픈이 들어간
  `Last-Sanctuary` 다. 그대로 돌리면 엉뚱한 폴더를 새로 만들고 진짜 에셋은 하나도
  안 바뀐 채 '생성 완료' 만 찍혔다."*
그 교훈이 **프로젝트 경로에는** 적용됐지만 **볼트 경로에는** 적용되지 않고 있었다.

쓰는 법
-------
    from vault_path import VAULT, TABLE_DIR
"""

import os

# 이 파일은 <프로젝트>/Tools/ 에 있다 → 두 번 올라가면 프로젝트 루트.
PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def find_vault():
    """
    볼트 폴더를 찾는다. 순서:

      ① 환경변수 ``LAST_SANCTUARY_VAULT``            ← 어디에도 안 맞으면 이걸로 지정
      ② 유니티 프로젝트의 **형제 폴더** ``Last-Sanctuary-Vault``  ← 보통 여기다
      ③ 옛 고정 경로 (기존 PC 호환)

    전부 없으면 ③ 을 그대로 돌려준다 — 없는 경로로 실패하더라도 메시지에 찾던 곳이
    찍혀야 원인을 알 수 있다(조용히 빈 문자열을 돌려주면 더 헷갈린다).
    """
    cands = []

    env = os.environ.get("LAST_SANCTUARY_VAULT")
    if env:
        cands.append(env)

    cands.append(os.path.join(os.path.dirname(PROJECT), "Last-Sanctuary-Vault"))
    cands.append(r"C:\Project\Last-Sanctuary-Vault")

    for c in cands:
        if os.path.isdir(c):
            return c
    return cands[-1]


VAULT = find_vault()

#: 원화가 들어 있을 수 있는 폴더들 — <b>볼트 안에서의 상대 경로</b>. 앞에 있는 것부터 본다.
ART_DIRS = (
    ("리소스", "sprites"),
    ("리소스", "asset"),
    ("리소스", "asset", "char_asset"),
    ("리소스", "chunk"),
    ("리소스", "illust"),
)


def find_art(*names):
    """
    ★★ <b>원화 파일을 «있는 곳에서» 찾는다</b> (2026-08-22 신설).

    <b>왜 생겼나</b> — 유저가 볼트를 정리하면서 낱장 원화를 ``리소스/asset/`` 에서
    ``리소스/sprites/`` 로 <b>옮겼다</b>. 그런데 분해 스크립트들은 옛 경로를 박아 두고 있어서
    <b>돌리면 «원본이 없습니다» 로 죽는다</b> — 실제로 아니사킬·카르시노스·고르도네 셋이
    그 상태였다(그래서 그 셋은 <b>다시 구울 수 없는</b> 상태로 남아 있었다).

    이 모듈이 만들어진 이유(맨 위 ★)와 <b>같은 종류의 문제</b>다: 경로를 여러 파일에
    박아 두면 폴더가 한 번 움직일 때 전부 깨진다. 그래서 «찾는 규칙» 을 여기 한 곳에 둔다.

    ``names`` 는 마지막이 파일 이름이고 앞은 그 안의 하위 폴더다
    (예: ``find_art("Char_Asset_Histon", "histon_motion.png")``).

    ⚠ 못 찾으면 <b>첫 후보 경로를 그대로 돌려준다</b> — 없는 경로로 실패하더라도
      메시지에 «찾던 곳» 이 찍혀야 원인을 알 수 있다(:func:`find_vault` 와 같은 규칙).
    """
    for parts in ART_DIRS:
        cand = os.path.join(VAULT, *(parts + tuple(names)))
        if os.path.isfile(cand):
            return cand
    return os.path.join(VAULT, *(ART_DIRS[0] + tuple(names)))

#: 표(xlsx)들이 있는 폴더. 예전 스크립트들의 ``VAULT`` / ``TABLE_DIR`` 상수가 가리키던 값이다.
TABLE_DIR = os.path.join(VAULT, "데이터 테이블")


if __name__ == "__main__":
    import sys
    sys.stdout.reconfigure(encoding="utf-8")
    print("PROJECT   :", PROJECT)
    print("VAULT     :", VAULT, "(있음)" if os.path.isdir(VAULT) else "(⚠ 없음)")
    print("TABLE_DIR :", TABLE_DIR, "(있음)" if os.path.isdir(TABLE_DIR) else "(⚠ 없음)")
