# -*- coding: utf-8 -*-
"""스킬 아이콘을 **새 세트로 전면 재배정** 한다 (2026-08-20).

유저 지시
---------
*"추가로 스킬 아이콘 더 만들어서 넣었으니까 검토 해보고 더 적절한 아이콘이라고 생각할 경우에
현재 적용된 스킬 아이콘들 포함해서 바꿔서 적용하고 잘라서 에셋에 맞는 이름으로 저장한 후
테이블에도 적어줘"*

무엇을 왜 바꾸나 — 검토 결과
----------------------------
``Tools/skill_icon_build.py`` 가 새 시트 두 장에서 **66개**를 뽑았다. 기존 24개와는
**스타일이 다른 세트**다(기존은 테두리 없는 납작한 발광 그림 · 새 것은 장식 테두리 픽셀아트).
섞으면 한 화면에서 어긋나 보이므로 **전부 새 세트로 옮긴다** — 지시의 *"현재 적용된
스킬 아이콘들 포함해서 바꿔서"* 가 그 뜻이다.

★ 그리고 **아이콘이 없던 스킬 16개**를 여기서 처음 채운다:
  · 시그리드 패시브 3개 (80016~80018) — 표에 아이콘 칸이 비어 있었다
  · 보스 스킬 10개 (130001~130010) — 칸은 있었는데 **한 번도 채워진 적이 없다**
  · 중립 몬스터 스킬 6개 (2001~2006) — 같음
  즉 지금까지 **보스·중립 스킬은 아이콘이 통째로 비어 있었다.**

배정 규칙 — **그림이 스킬을 설명해야 한다**
-------------------------------------------
스킬 이름(enum)과 표의 설명을 읽고 골랐다. 34개 **전부 서로 다른 아이콘**이다 —
같은 그림이 두 스킬에 붙으면 상세 카드에서 구분이 안 된다.

몇 개는 그림이 이름을 그대로 담고 있어 고민할 것이 없었다:
  · ``Binding_orb``(말파스 구속탄) → ``chain_shackle`` (사슬과 족쇄)
  · ``Rage_on``(죽으면 부활) → ``revive_angel`` (천사가 시체를 일으킨다)
  · ``Purifying_touch``(정화의 손길) → ``heal_plus`` (초록 십자)
  · ``Pipe_strike``(베일의 관 타격) → ``cannon_battery`` (포열)

독 계열이 셋뿐인데 후보 스킬이 넷이라 한 번 갈랐다:
  ``Corrosion``→``plague_cloud`` · ``Deadly_venom``→``plague_skull`` ·
  ``Gluttony``→``skull_pyre`` · ``Pipe_smoke``→``void_spiral``(창백한 연기)

⚠ 세 워크북을 **모두** 고친다 — 스킬이 세 표에 나뉘어 있다:
    캐릭터 테이블.xlsx / Skill            (캐릭터 패시브 80001~80018)
    웨이브 몬스터 테이블.xlsx / Skill      (보스 스킬 130001~130010)
    임시용 중립 몬스터.xlsx / Skill        (중립 몬스터 스킬 2001~2006)

⚠ 편집은 **Excel COM** — openpyxl 로 저장하면 하이퍼링크가 날아간다(UI-17절 실사고).
⚠ 파일이 엑셀에서 **열려 있으면 안 된다** (``~$`` 잠금 파일이 있으면 멈춘다).

사용법:  py -3 Tools/table_update_20260820_skill_icons.py
다음:    py -3 Tools/sync_tables_to_assets.py   (에셋에 반영)
        py -3 Tools/gen_character_assets.py    (캐릭터 패시브 에셋)
"""

import datetime
import os
import shutil
import sys

from vault_path import TABLE_DIR as TABLES

BACKUP_ROOT = os.path.join(TABLES, "_백업")

#: 스킬 id → 아이콘 이름(``icon_`` 접두사 없이). 근거는 맨 위 「배정 규칙」.
ICONS = {
    # ── 캐릭터 패시브 ────────────────────────────────────────────────────
    80001: "holy_target",    # Innate_delicacy 타고난 섬세함 — 보지 않고 알아채는 조준 고리
    80002: "ascension",      # Sacrifice 희생 — 금빛으로 올라가는 형상
    80003: "blood_drop",     # Blood_attack 피의 공격
    80004: "guard_shield",   # Will_of_iron 강철 의지 — 푸른 방패
    80005: "feather_burst",  # Blazing_wings 불타는 날개 — 빛나는 날개
    80006: "holy_banner",    # Rho_aias 로 아이아스 — 방어 진형의 깃발
    80007: "skull_pyre",     # Gluttony 탐식 — 먹어치운 잔해
    80008: "heart_guard",    # Ecstasy 황홀 — 붉은 심장
    80009: "dark_effigy",    # Rampage 광란 — 붉은 어둠의 형상
    80010: "plague_cloud",   # Corrosion 부식 — 초록 독 구름
    80011: "light_pillar",   # Calm_down 정신 안정 — 정화의 광주
    80012: "heal_plus",      # Purifying_touch 정화의 손길 — 초록 십자
    80013: "charge_slash",   # Vanguard 선봉 — 붉은 돌격
    80014: "revive_angel",   # Rage_on 분노(부활) — 천사가 시체를 일으킨다
    80015: "gold_tornado",   # Reaver 복수자 — 광역 회오리
    80016: "blood_spikes",   # Sadism 가학증 — 붉은 가시
    80017: "blade_buff",     # Joy_of_pain 고통의 기쁨 — 공속 증가(상승 화살표)
    80018: "void_orb",       # Uncontrollable_pleasure 통제 불능 — 붉은 공허 구슬

    # ── 보스 스킬 ────────────────────────────────────────────────────────
    130001: "dark_shrine",     # Fallen_tomb 타락한 무덤 — 어두운 제단
    130002: "red_slash",       # Void_laser 공허의 광선 — 직선 참격
    130003: "chain_shackle",   # Binding_orb 구속탄 — 사슬과 족쇄
    130004: "evil_eye",        # Curse_beam 저주 광선 — 붉은 사안
    130005: "blood_moon",      # Lure_blood 피의 유혹 — 붉은 달
    130006: "ghost_wail",      # Death_song 죽음의 노래 — 청록 망령
    130007: "undead_rise",     # Screaming 아우성 — 팔을 든 해골
    130008: "fire_burst",      # Burning_breath 타오르는 숨결 — 화염 폭발
    130009: "cannon_battery",  # Pipe_strike 관 타격 — 포열
    130010: "void_spiral",     # Pipe_smoke 관 연기 — 창백한 소용돌이

    # ── 중립 몬스터 스킬 ─────────────────────────────────────────────────
    2001: "claw_marks",     # Scratch 긁기 — 발톱 자국
    2002: "frost_wolf",     # Roar_death 죽음의 포효 — 벌린 아가리
    2003: "thorn_vines",    # Tail_strike 꼬리치기 — 가시 덩굴
    2004: "hooded_reaper",  # Huge_threat 거대한 위협 — 두건 쓴 사신
    2005: "bone_spikes",    # Creepy_scar 소름끼치는 상흔 — 뼈 가시
    2006: "plague_skull",   # Deadly_venom 치명적인 독 — 초록 독 해골
}

#: (파일, 시트) — 스킬 표 셋.
TARGETS = [
    ("캐릭터 테이블.xlsx", "Skill"),
    ("웨이브 몬스터 테이블.xlsx", "Skill"),
    ("임시용 중립 몬스터.xlsx", "Skill"),
]

FIRST_DATA_ROW = 4


def check_locks():
    """엑셀에서 열려 있으면 ``~$`` 잠금 파일이 생긴다 — 그 상태로 저장하면 충돌한다."""
    locked = [f for f, _ in TARGETS
              if os.path.isfile(os.path.join(TABLES, "~$" + f))]
    if locked:
        raise SystemExit("⚠ 엑셀에서 열려 있는 파일이 있습니다 — 닫고 다시 실행하세요:\n   "
                         + "\n   ".join(locked))


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_스킬아이콘재배정")
    os.makedirs(dst, exist_ok=True)
    for f, _ in TARGETS:
        src = os.path.join(TABLES, f)
        if os.path.isfile(src):
            shutil.copy2(src, os.path.join(dst, f))
    print("백업:", dst)


def find_col(ws, field, max_col=32):
    for c in range(1, max_col + 1):
        v = ws.Cells(2, c).Value
        if v is not None and str(v).strip() == field:
            return c
    return 0


def main():
    # 아이콘 파일이 실제로 있는지 먼저 본다 — 표에만 적고 파일이 없으면 게임이
    # 경고만 남기고 조용히 빈 아이콘을 띄운다(PassiveSkillSO.Icon).
    project = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    icon_dir = os.path.join(project, "Assets", "_Project", "Resources", "SkillIcons")
    missing = [n for n in sorted(set(ICONS.values()))
               if not os.path.isfile(os.path.join(icon_dir, "icon_%s.png" % n))]
    if missing:
        raise SystemExit("⚠ 아이콘 파일이 없습니다 (Tools/skill_icon_build.py 를 먼저 "
                         "돌리세요): " + ", ".join(missing))

    check_locks()

    import win32com.client as win32
    backup()
    excel = win32.gencache.EnsureDispatch("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False

    seen = set()
    try:
        for fname, sheet in TARGETS:
            path = os.path.join(TABLES, fname)
            if not os.path.isfile(path):
                print("⚠ 파일 없음:", path)
                continue

            wb = excel.Workbooks.Open(path)
            ws = wb.Worksheets(sheet)
            c_id = find_col(ws, "skill_id")
            c_icon = find_col(ws, "skill_icon")
            c_type = find_col(ws, "skill_type")
            if not c_id or not c_icon:
                print("⚠ %s/%s: skill_id · skill_icon 컬럼을 못 찾음" % (fname, sheet))
                wb.Close(False)
                continue

            print()
            print("  [%s / %s]" % (fname, sheet))
            last = ws.UsedRange.Rows.Count
            for r in range(FIRST_DATA_ROW, last + 1):
                v = ws.Cells(r, c_id).Value
                if v is None:
                    continue
                sid = int(v)
                if sid not in ICONS:
                    print("    ⚠ %d: 배정표에 없습니다 — 건드리지 않습니다" % sid)
                    continue

                old = ws.Cells(r, c_icon).Value
                new = "icon_" + ICONS[sid]
                ws.Cells(r, c_icon).Value = new
                seen.add(sid)
                stype = ws.Cells(r, c_type).Value if c_type else ""
                print("    %-7d %-26s %-22s → %s"
                      % (sid, str(stype), str(old) if old else "(비어 있었다)", new))

            wb.Save()
            wb.Close()

        left = sorted(set(ICONS) - seen)
        print()
        print("  아이콘을 적은 스킬 %d개" % len(seen))
        if left:
            print("  ⚠ 표에서 못 찾은 스킬 id: %s" % left)
    finally:
        excel.Quit()

    print()
    print("다음: py -3 Tools/sync_tables_to_assets.py")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
