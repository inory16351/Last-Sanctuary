# -*- coding: utf-8 -*-
"""「능력치 낱낱」·「정신 이상 낱낱」을 <b>다른 도움말과 같은 문체</b>로 고친다 (2026-08-26).

유저 지시
---------
  *"정신이상 낱낱이랑 능력치 낱낱 AI티 너무 많이 나니까 다른 도움말 설명 보고 수정 좀 해줘"*

★★★ 무엇이 «AI티» 였나 — <b>다른 항목과 문장 모양이 달랐다</b>
──────────────────────────────────────────────────────────────────────
표의 다른 백과 본문(예: `help_mental_error_body`)은 이렇게 생겼다:

    여러 종류 중 하나가 무작위로 걸립니다.
    능력치를 깎는 것도 있고, 행동을 바꿔 놓는 것도 있습니다. …
    침식을 아예 안 쌓을 수는 없습니다. 누구에게 언제 쌓게 할지를 고르는 일입니다.

  → <b>완결된 평서문</b> · 한 줄에 한 뜻 · 꾸밈 없음.

반면 「낱낱」 둘은 이렇게였다:

    스스로를 갉는 것 —                     ← 소제목을 줄표로 세운다
    <b>자해</b> 그 자리에서 최대 체력의 4분의 1.   ← 용어 + <b>토막 문장</b>
    좋은 셋이 섞여 있지만 여덟은 나쁩니다. 노려서 될 일이 아닙니다.   ← 잠언조 맺음

  → 소제목 · 토막 나열 · 잠언조 맺음. <b>정보는 맞는데 목소리가 다르다.</b>
    이 셋이 «기계가 쓴 것 같다» 의 정체다.

★ 무엇을 고쳤나
  · 줄표 소제목을 없애고 <b>«…으로는 A(…), B(…), C(…)가 있습니다» 평서문</b>으로 묶었다.
  · 토막 나열을 <b>완결된 문장</b>으로 바꿨다. 수치·초·배수는 <b>한 개도 빼지 않았다</b>.
  · 잠언조 맺음을 다른 항목과 같은 <b>담담한 한 줄</b>로 바꿨다.
  · <b>태그는 용어에만</b> 남겼다(다른 항목의 쓰임과 같다).

⚠ 고치는 곳이 <b>둘</b>이다 — 도움말 표(원본)와 스트링 키 테이블(게임이 읽는 정본).
  스트링 키 테이블은 «사람이 고친 번역을 덮지 않는다» 는 규칙(`gen_string_table.py`)이라
  <b>거기에 직접 써야</b> 반영된다. 영어 칸도 같이 채운다.
⚠ <b>Excel COM 으로 쓴다</b>(136-4절).

사용법:  python Tools/table_update_20260826_help_detail_rewrite.py
다음:    python Tools/gen_string_table.py      (StringTable.txt 다시 내보내기)
"""

import datetime
import os
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import openpyxl
import win32com.client

from vault_path import TABLE_DIR

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

HELP_XLSX = os.path.join(TABLE_DIR, "Last_Sanctuary_도움말테이블_Ver01.xlsx")
STRING_XLSX = os.path.join(TABLE_DIR, "스트링 키 테이블.xlsx")
BACKUP_ROOT = os.path.join(TABLE_DIR, "_백업")

# ══════════════════════════════════════════════════════════════════════
#  새 문구 — {키: (한국어, 영어)}
# ══════════════════════════════════════════════════════════════════════
TEXT = {
    "help_stats_detail_summary": (
        "열두 칸이 각각 무엇을 정하는지 적어 두었습니다.\n"
        "어느 칸이 자랄지는 <b>성장 유형</b>이 정합니다.",
        "What each of the twelve stats decides.\n"
        "Which ones grow is decided by the <b>growth type</b>.",
    ),
    "help_stats_detail_body": (
        "능력치는 열두 칸입니다. 공격 계열은 넷이지만 전술 지침에서 고른 공격 유형에 "
        "해당하는 하나만 쓰입니다. 나머지 셋은 올려도 전투에 나오지 않습니다.\n"
        "<b>근거리 공격력</b>은 붙어서 때릴 때, <b>원거리 공격력</b>은 떨어져서 쏠 때, "
        "<b>마법</b>은 넓은 자리를 한꺼번에 칠 때, <b>회복력</b>은 동료를 되살릴 때 쓰입니다.\n"
        "<b>체력</b>이 0 이 되면 쓰러집니다. <b>방어력</b>은 한 대를 덜 아프게 하고, "
        "<b>체력 재생</b>은 싸움이 끝난 뒤 스스로 아무는 양입니다. "
        "<b>공격 속도</b>는 같은 시간에 몇 번 때리는지를, <b>이동 속도</b>는 다가갈 때와 "
        "물러설 때의 빠르기를 정합니다.\n"
        "<b>명중률</b>과 <b>크리티컬</b>은 원거리에만 붙습니다. 근거리와 마법, 회복은 "
        "언제나 맞고 급소도 나지 않습니다. 타고난 능력 몇 가지가 그 예외를 만듭니다. "
        "급소가 나면 피해가 1.5배가 됩니다.\n"
        "<b>저항력</b>은 침식이 차고 빠지는 속도를 정하며, 강화로는 오르지 않습니다. "
        "타고난 값이 그대로 갑니다.\n"
        "한 칸의 끝은 100 입니다. 그 위로 올리는 것은 영웅 각성과 유물뿐이고, "
        "성장 창의 숫자에는 그 둘이 이미 더해져 있습니다.",
        "There are twelve stats. Four of them are attack stats, but only the one that "
        "matches the attack type you chose in the tactical orders is used. The other "
        "three do nothing in battle, however high they are.\n"
        "<b>Melee attack</b> is used up close, <b>ranged attack</b> at a distance, "
        "<b>magic</b> when striking a wide area at once, and <b>healing</b> when mending "
        "an ally instead of striking.\n"
        "A character falls when <b>health</b> reaches 0. <b>Defense</b> makes each blow "
        "hurt less, and <b>health regeneration</b> is how much they mend on their own "
        "once the fighting stops. <b>Attack speed</b> decides how many times they strike "
        "in the same span, and <b>movement speed</b> how fast they close in and fall back.\n"
        "<b>Accuracy</b> and <b>critical</b> apply to ranged attacks only. Melee, magic "
        "and healing always land and never crit. A few innate abilities make exceptions "
        "to that. A critical hit deals 1.5 times the damage.\n"
        "<b>Resistance</b> decides how fast erosion rises and falls, and it does not go "
        "up with enhancement. The innate value is the one you keep.\n"
        "A stat stops at 100. Only hero awakening and relics go above it, and the numbers "
        "in the growth window already include both.",
    ),
    "help_mental_detail_summary": (
        "침식이 100 에 닿으면 열한 가지 중 하나가 무작위로 옵니다.\n"
        "무엇이 올지는 고를 수 없습니다.",
        "When erosion reaches 100, one of eleven conditions arrives at random.\n"
        "You cannot choose which one.",
    ),
    "help_mental_detail_body": (
        "정신 이상이 한 번 오면 침식은 절반쯤으로 내려앉습니다. 0 이 되지는 않으므로 "
        "두 번째는 더 빨리 옵니다.\n"
        "스스로를 해치는 것으로는 <b>자해</b>(그 자리에서 최대 체력의 4분의 1을 잃습니다), "
        "<b>피학</b>(45초 동안 체력이 저절로 줄어듭니다), <b>이기심</b>(90초 동안 치유를 "
        "받지 못하고 스스로 아무는 것만 남습니다)이 있습니다.\n"
        "지침을 따르지 않게 되는 것으로는 <b>혼란</b>(30초 동안 동료를 때립니다), "
        "<b>공포</b>(45초 동안 싸우기를 그만두고 성역 쪽으로 물러납니다), "
        "<b>광분</b>(90초 동안 전술 지침을 버리고 앞으로 뛰쳐나갑니다)이 있습니다.\n"
        "곁으로 번지는 것으로는 <b>우울</b>(가까운 동료에게 침식이 옮습니다)과 "
        "<b>역겨움</b>(40초 동안 곁의 체력을 갉습니다)이 있습니다.\n"
        "드물게 도움이 되는 것도 섞여 있습니다. <b>진정</b>은 곁의 침식을 덜어 주고, "
        "<b>각성</b>은 120초 동안 모든 능력치를 올리고, <b>고조</b>는 에너지를 쓰지 않고 "
        "한 번 강화합니다. 다음 강화 비용은 그만큼 오릅니다.\n"
        "열한 가지 가운데 여덟은 나쁜 것이라 노려서 얻을 일은 아닙니다. 침식을 아예 안 "
        "쌓을 수는 없고, 누구에게 쌓게 둘지를 고르는 일입니다. 전방에 오래 세워 둔 쪽이 "
        "먼저 찹니다.",
        "Once a condition arrives, erosion drops back to about half. It does not reach 0, "
        "so the second one comes sooner.\n"
        "Some of them turn on the character: <b>self-harm</b> (a quarter of maximum health "
        "is lost on the spot), <b>masochism</b> (health drains on its own for 45 seconds), "
        "and <b>selfishness</b> (no healing reaches them for 90 seconds, leaving only what "
        "they mend themselves).\n"
        "Some stop them from following orders: <b>confusion</b> (they strike allies for 30 "
        "seconds), <b>fear</b> (they stop fighting and fall back toward the sanctuary for "
        "45 seconds), and <b>frenzy</b> (they abandon the tactical orders and charge forward "
        "for 90 seconds).\n"
        "Some spread to those nearby: <b>depression</b> (erosion carries over to nearby "
        "allies) and <b>disgust</b> (it gnaws at the health of those beside them for 40 "
        "seconds).\n"
        "A few of them help. <b>Calm</b> eases the erosion of those nearby, <b>awakening</b> "
        "raises every stat for 120 seconds, and <b>elation</b> grants one enhancement "
        "without energy. The next enhancement costs that much more.\n"
        "Eight of the eleven are bad, so they are not worth aiming for. You cannot avoid "
        "erosion entirely; what you choose is who carries it. Whoever stands in front the "
        "longest fills up first.",
    ),
}


def backup():
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    folder = os.path.join(BACKUP_ROOT, stamp + "_도움말낱낱문체")
    os.makedirs(folder, exist_ok=True)
    for f in (HELP_XLSX, STRING_XLSX):
        shutil.copy2(f, os.path.join(folder, os.path.basename(f)))
    print("백업: " + folder)


def preview():
    """고치기 전 값을 찍어 둔다 — 무엇이 바뀌는지 눈으로 확인할 수 있게."""
    wb = openpyxl.load_workbook(HELP_XLSX, data_only=True)
    ws = wb["StringKeys"]
    for r in ws.iter_rows(min_row=2, values_only=True):
        if r[0] in TEXT:
            old = str(r[1] or "")
            print(f"  [{r[0]}] {len(old)}자 → {len(TEXT[r[0]][0])}자")


def write_sheet(ws, key_col, kr_col, en_col, label):
    """행을 훑어 키가 맞으면 kr·en 을 쓴다. 쓴 개수를 돌려준다."""
    touched = 0
    row = 1
    while True:
        v = ws.Cells(row, key_col).Value
        if v is None:
            # 표 끝 판정 — 헤더 위쪽의 빈 줄에서 멈추지 않게 20줄까지는 더 본다
            blank = all(ws.Cells(row + k, key_col).Value is None for k in range(1, 20))
            if blank:
                break
            row += 1
            continue
        key = str(v).strip()
        if key in TEXT:
            kr, en = TEXT[key]
            ws.Cells(row, kr_col).Value = kr
            if en_col:
                ws.Cells(row, en_col).Value = en
            print(f"  {label} {row}행 — {key}")
            touched += 1
        row += 1
    return touched


def main():
    print("고치기 전 길이")
    preview()
    backup()

    excel = win32com.client.Dispatch("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False
    try:
        # ① 도움말 표 — StringKeys 시트(A 키 · B 한국어 · C 영어 · D 비고)
        wb = excel.Workbooks.Open(os.path.abspath(HELP_XLSX))
        n1 = write_sheet(wb.Worksheets("StringKeys"), 1, 2, 3, "도움말표")
        wb.Save()
        wb.Close()

        # ② 스트링 키 테이블 — string 시트(A 키 · B 한국어 · C 영어)
        wb = excel.Workbooks.Open(os.path.abspath(STRING_XLSX))
        n2 = write_sheet(wb.Worksheets("string"), 1, 2, 3, "스트링키")
        wb.Save()
        wb.Close()
    finally:
        excel.Quit()

    print(f"\n도움말 표 {n1}칸 · 스트링 키 테이블 {n2}칸 갱신")
    if n1 != len(TEXT) or n2 != len(TEXT):
        print(f"⚠ {len(TEXT)}개를 기대했는데 다르다 — 키 이름을 확인할 것")


if __name__ == "__main__":
    main()
