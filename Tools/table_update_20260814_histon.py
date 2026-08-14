# -*- coding: utf-8 -*-
"""히스톤(9005) 표 정리 — 유저가 리터럴로 적어둔 값을 스트링 키로 옮기고 아이콘을 채운다 (2026-08-14).

유저 지시: *"테이블에 그냥 내가 그냥 적어놓은 값들도 스트링 키 테이블에 연동해서 넣고 정리.
아이콘은 임의로 너가 골라서 써."*

유저가 캐릭터 테이블에 직접 적어둔 상태:
  Skill      80013~80015 — skill_name / skill_explain 에 <b>한글 문장이 그대로</b>, skill_icon 은 <b>빈칸</b>
  Skill_Type Vanguard / Rage_on / Reaver — desc 에 <b>한글 문장이 그대로</b>

51절이 세운 규칙은 "표의 모든 문구는 스트링 키만 가리키고, 문장은 스트링 키 테이블에 둔다" 이므로
  · 한글 문장 → 스트링 키 테이블로 옮기고
  · 표에는 키(`skill_name_80013` 등)만 남긴다
  · 빈 skill_icon 은 아직 안 쓰는 아이콘 중에서 고른다

⚠ 편집은 <b>Excel COM</b> 으로 한다 — openpyxl 로 저장하면 51-11절이 넣은 <b>하이퍼링크</b>
   (키 칸을 눌러 스트링 키 테이블로 이동)가 날아간다. 64-2·69-10절과 같은 이유다.

사용법:  python Tools/table_update_20260814_histon.py
"""

import os
import shutil
import datetime

VAULT = r"C:\Project\Last-Sanctuary-Vault"
TABLES = os.path.join(VAULT, "데이터 테이블")
CHAR_XLSX = os.path.join(TABLES, "캐릭터 테이블.xlsx")
STRING_XLSX = os.path.join(TABLES, "스트링 키 테이블.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")

# ── 아이콘 배정 (유저: "임의로 골라서 써") ────────────────────────────────
# 이미 쓰이는 12개(spirit_sense·blessing·blood_dagger·holy_light·flame_burst·shield_aura·
# soul_devour·arcane_aura·demon_rage·poison_skull·barrier_dome·heal_cross)를 피해서 골랐다.
ICONS = {
    80013: "icon_charge_rush",   # 선봉장 — 앞장서 돌격하는 그림
    80014: "icon_lightning",     # 분노   — 부활 연출이 지면에서 솟는 빛기둥이다
    80015: "icon_whirlwind",     # 복수자 — 부활 지점을 중심으로 퍼지는 원형 범위
}

# ── 스트링 키 테이블에 새로 넣을 문구 ────────────────────────────────────
# (키, 한국어, 출처)  — 한국어는 유저가 표에 적어둔 문장을 <b>한 글자도 안 고치고</b> 옮긴다.
NEW_STRINGS = [
    ("skill_name_80013", "선봉장", "Skill.skill_name"),
    ("skill_name_80014", "분노", "Skill.skill_name"),
    ("skill_name_80015", "복수자", "Skill.skill_name"),
    ("skill_explain_80013",
     "전장의 앞을 지키는 것은 히스톤에게 쥐어진 운명입니다.", "Skill.skill_explain"),
    ("skill_explain_80014",
     "죽음조차 히스톤의 복수를 막을 수는 없습니다.", "Skill.skill_explain"),
    ("skill_explain_80015",
     "그의 부활은 적들에겐 공포이지만 아군에겐 기적과도 같습니다.", "Skill.skill_explain"),
    ("skill_type_desc_Vanguard",
     "히스톤의 포지션은 전방 / 공격 유형은 근거리로 고정된다. "
     "히스톤의 근거리 공격은 예외적으로 크리티컬 공격이 가능하다.", "Skill_Type.desc"),
    ("skill_type_desc_Rage_on",
     "히스톤에게 별개의 '분노' 수치 획득이 가능해진다.(0~100) "
     "히스톤이 공격 할때 마다 {value_01} 만큼의 분노를 획득한다. "
     "분노는 체력 재생 가능 상태 일때 초당 {value_02} 만큼 하락한다.(진군 중일때 제외) "
     "분노가 100일 때 히스톤이 죽음에 이를 시 {value_03}초 만큼의 경직 시간 이후 부활한다.",
     "Skill_Type.desc"),
    ("skill_type_desc_Reaver",
     "히스톤이 부활 할때마다 반경 {value_01} 타일 범위의 원형 공간의 적들에게 "
     "공격력의 {value_02}% 만큼의 피해를 주고, 반경 {value_01} 타일 범위의 원형 공간의 "
     "아군 캐릭터들에게 최대체력의 {value_03}% 만큼의 회복을 준다.", "Skill_Type.desc"),
]

# 표에서 리터럴을 키로 바꿀 자리
SKILL_KEYS = {
    80013: ("skill_name_80013", "skill_explain_80013"),
    80014: ("skill_name_80014", "skill_explain_80014"),
    80015: ("skill_name_80015", "skill_explain_80015"),
}
TYPE_KEYS = {
    "Vanguard": "skill_type_desc_Vanguard",
    "Rage_on": "skill_type_desc_Rage_on",
    "Reaver": "skill_type_desc_Reaver",
}


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    folder = os.path.join(BACKUP_ROOT, stamp + "_히스톤스트링키정리")
    os.makedirs(folder, exist_ok=True)
    for src in (CHAR_XLSX, STRING_XLSX):
        shutil.copy2(src, os.path.join(folder, os.path.basename(src)))
    print("백업 →", folder)


def main():
    import win32com.client as win32

    backup()
    excel = win32.gencache.EnsureDispatch("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False
    try:
        # ── ① 캐릭터 테이블 ────────────────────────────────────────────
        wb = excel.Workbooks.Open(CHAR_XLSX)
        ws = wb.Worksheets("Skill")
        changed = 0
        for r in range(4, ws.UsedRange.Rows.Count + 1):
            sid = ws.Cells(r, 1).Value
            if sid is None:
                continue
            sid = int(sid)
            if sid not in SKILL_KEYS:
                continue
            name_key, explain_key = SKILL_KEYS[sid]
            ws.Cells(r, 2).Value = name_key          # skill_name
            ws.Cells(r, 8).Value = ICONS[sid]        # skill_icon
            ws.Cells(r, 9).Value = explain_key       # skill_explain
            changed += 1
            print(f"  Skill {sid} → {name_key} · {ICONS[sid]} · {explain_key}")

        wt = wb.Worksheets("Skill_Type")
        for r in range(4, wt.UsedRange.Rows.Count + 1):
            t = wt.Cells(r, 1).Value
            if not t:
                continue
            t = str(t).strip()
            if t in TYPE_KEYS:
                wt.Cells(r, 2).Value = TYPE_KEYS[t]
                changed += 1
                print(f"  Skill_Type {t} → {TYPE_KEYS[t]}")
        wb.Save()
        wb.Close()
        print(f"캐릭터 테이블 {changed}칸 갱신")

        # ── ② 스트링 키 테이블 ──────────────────────────────────────────
        wb = excel.Workbooks.Open(STRING_XLSX)
        ws = wb.Worksheets("string")
        last = ws.UsedRange.Rows.Count
        existing = {str(ws.Cells(r, 1).Value).strip()
                    for r in range(4, last + 1) if ws.Cells(r, 1).Value}

        added = 0
        for key, kr, source in NEW_STRINGS:
            if key in existing:
                print(f"  (이미 있음) {key}")
                continue
            last += 1
            ws.Cells(last, 1).Value = key
            ws.Cells(last, 2).Value = kr
            ws.Cells(last, 4).Value = source
            added += 1
            print(f"  + {key}")
        wb.Save()
        wb.Close()
        print(f"스트링 키 {added}개 추가 (총 {last - 3}개)")
    finally:
        excel.Quit()


if __name__ == "__main__":
    main()
