# -*- coding: utf-8 -*-
"""이벤트 표 Ver013 — <b>보상에 「유물 획득」을 넣는다</b> (2026-08-24).

유저 지시:
  ① *"이벤트 보상용 전용 에픽 유물 3개만 추가해 그리고 해당 유물 보상으로 그냥 이벤트에
     끼워넣어줘 이벤트 내용이랑 관련있는 유물이었으면 좋겠음 <b>예상치못한 획득의 재미</b>를
     느낄 수 있게"*
  ② *"등급별로 3개씩 이벤트 보상에 유물획득도 넣어"*
  ③ *"추가한 데이터들 반드시 테이블에도 반영해"*

무엇을 하나
-----------
`RewardType` 시트에 <b>`relic_gain` 한 줄</b>을 더하고, `ChoiceGroup` 의 <b>아홉 선택지</b>에
그 보상을 <b>둘째 칸</b>으로 붙인다 — 일반 3 · 레어 3 · 에픽 3(신규 사건 전용).

★★ <b>`value_01` 이 «수치» 가 아니라 «유물 ID» 다</b>
-----------------------------------------------
다른 보상 타입은 전부 «얼마나» 를 담는데 이것만 «무엇을» 을 담는다. 등급만 적어 굴리는
방식을 <b>안 쓴 이유</b>는 유저가 «이벤트 내용과 관련 있는 유물» 을 원했기 때문이다 —
등급으로 굴리면 「곪은 자리」에서 「젖은 활시위」가 나오고, 그러면 <b>사건과 유물이 서로
남이 된다</b>. 그래서 표가 <b>어느 유물인지 지목</b>한다.

★★ <b>왜 이 아홉 선택지인가</b> — <b>둘째 보상 칸이 비어 있는 것이 정확히 열 개</b>다
------------------------------------------------------------------------------
표의 보상 칸은 <b>두 개뿐</b>이고(`reward_type_01/02`), 86개 선택지 중 <b>76개가 둘 다
차 있다</b>. 이미 있는 보상을 밀어내면 그것은 <b>밸런스 변경</b>이지 «보상 추가» 가 아니다
(유저가 요청한 것이 아니다). 그래서 <b>빈 칸이 있는 열 개 안에서</b> 사건 내용과 가장
잘 맞는 아홉을 골랐다.

  ⚠ 그 대신 「봉화의 흔적」·「영웅의 자리」·「남겨진 파편」처럼 <b>유물과 더 잘 맞는 사건</b>
    몇은 두 칸이 이미 차 있어 쓰지 못했다. 그쪽에 붙이려면 표와 코드에 <b>셋째 보상 칸</b>이
    필요하다(`EventChoice.rewardType03` · `gen_event_assets.py` · `EventService.ApplyRewards`).
    지금은 <b>손대지 않았다</b> — 유저가 요청한 범위 밖이다.

★ <b>206005 「메워지는 살」만 두 선택지에 붙었다</b> — 남은 빈 칸이 여덟 사건에 열 개라
  하나는 겹쳐야 했다. 그 사건이 «몸이 아무는 이야기» 라 <b>심장을 채우면 에픽 · 손발을
  채우면 레어</b> 로 갈리는 것이 오히려 읽히므로 거기를 골랐다.

⚠ 편집은 <b>Excel COM · DispatchEx</b> — openpyxl 로 저장하면 서식이 상한다(UI-17절).
⚠ 이 표는 <b>생성 스크립트가 없다</b>(손으로 만든 표다) — 그래서 <b>표가 정본</b>이고
  이 스크립트가 그 표를 고친다. 유물 쪽은 반대다(`gen_relic_table.py` 가 정본).

사용법:  py -3 Tools/table_update_20260824_event_relic_reward.py
다음:    py -3 Tools/gen_event_assets.py   (표 → EventDefinitionSO 43개)
"""

import datetime
import os
import shutil
import sys

from vault_path import TABLE_DIR as TABLES

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

EVENT_XLSX = os.path.join(TABLES, "Last_Sanctuary_이벤트테이블_Ver013.xlsx")
BACKUP_ROOT = os.path.join(TABLES, "_백업")

# ──────────────────────────────────────────────────────────────────────────
# RewardType 시트에 더할 줄 — (reward_type, reward_type_desc, use_duration)
#
# ⚠ 설명문의 «{value_01}» 규약을 <b>일부러 쓰지 않았다</b> — 이 칸은 수치가 아니라 ID 라
#   «{value_01} 만큼» 이라고 쓰면 표를 읽는 사람이 수치로 오해한다.
# ──────────────────────────────────────────────────────────────────────────
REWARD_ROWS = [
    ("— 유물 —", "", ""),
    ("relic_gain",
     "value_01 에 적힌 <b>유물 ID</b>(유물 테이블의 relic_id) 하나를 즉시 얻습니다. "
     "★ 다른 보상과 달리 value_01 은 «수치» 가 아니라 «무엇을» 입니다 — 사건 내용과 맞는 "
     "유물을 표가 지목합니다. ⚠ 없는 ID 면 아무 일도 일어나지 않고 콘솔에 경고가 남습니다.",
     0),
]

# ──────────────────────────────────────────────────────────────────────────
# ChoiceGroup — choice_id → (유물 ID, 유물 이름, 등급, 왜 그 사건에 그 유물인가)
#
# ★ 등급별 3개씩. 에픽 셋은 이번에 새로 만든 <b>사건 전용</b> 유물이다
#   (gen_relic_table.py 의 720017~720019 — 발굴·처치·보스에서는 절대 나오지 않는다).
# ──────────────────────────────────────────────────────────────────────────
RELIC_REWARDS = {
    # ── 일반 3 ────────────────────────────────────────────────────────
    300001: (700002, "오른 열", "일반",
             "205001 「식후 정리」 / «모조리 불태워라!!» — 유해를 태운 열이 몸에 남는다. "
             "그 선택지의 기존 보상도 공격 속도라 결이 같다"),
    300027: (700003, "부어오른 자리", "일반",
             "205014 「올라온 양분」 / «전부 먹여라» — 먹은 만큼 살이 부푼다"),
    300036: (700004, "붉은 실", "일반",
             "205018 「심장의 잡음」 / «심장은 버틴다. 손발을 먹여라» — "
             "피가 닿는 곳까지가 아직 살아 있는 곳이다"),

    # ── 레어 3 ────────────────────────────────────────────────────────
    300031: (710011, "각성한 수지상세포", "레어",
             "205016 「노련해진 손」 / «저 하나를 끌어올려라» — 같은 동작을 수천 번 반복한 "
             "손은 결국 다른 손이 된다(명중·크리티컬)"),
    300061: (710008, "서늘한 해열", "레어",
             "206003 「낮은 노래」 / «같이 부르게 한다» — 옆에 선 천사의 떨림이 잦아든다"
             "(침식이 쌓이는 속도가 느려진다)"),
    300066: (710010, "부푼 림프절", "레어",
             "206005 「메워지는 살」 / «손발을 채운다» — 메워진 살(체력·방어)"),

    # ── 에픽 3 · 사건 전용 신규 ────────────────────────────────────────
    300040: (720017, "값이 붙지 않은 은혜", "에픽",
             "★ 205020 「대가 없는 은혜」 / «사양한다» — <b>사양했는데 남아 있었다</b>. "
             "«값이 뒤에 붙는» 호의를 거절한 자리에서 값이 붙지 않은 것 하나가 남는 것이 "
             "이번 지시의 «예상치 못한 획득» 그 자체다"),
    300065: (720018, "스스로 메운 살", "에픽",
             "206005 「메워지는 살」 / «심장을 채운다» — 아무도 꿰매지 않았는데 아물었다"
             "(파도가 올 때마다 새 살이 덮는다 = 웨이브 보호막)"),
    300059: (720019, "돌아갈 곳의 기억", "에픽",
             "206002 「꺾인 무릎」 / «어깨를 붙들어준다» — 집으로 돌아갈 수 없다는 것을 "
             "알고도 그곳을 기억한다(침식 계열)"),
}

#: 결과창 «효과 요약» 에 덧붙일 문구. ⚠ 원문을 지우지 않고 <b>뒤에 잇는다</b>.
EFFECT_SUFFIX = "  ·  유물 「{name}」 획득 ({grade})"


def backup(paths, tag):
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    dst = os.path.join(BACKUP_ROOT, stamp + "_" + tag)
    os.makedirs(dst, exist_ok=True)
    for p in paths:
        shutil.copy2(p, os.path.join(dst, os.path.basename(p)))
    print("backup:", dst)


def find_col(ws, field, max_col=24):
    """2행이 필드명 줄이다(1행 한글 · 2행 필드 · 3행 타입 · 4행부터 값)."""
    for c in range(1, max_col + 1):
        v = ws.Cells(2, c).Value
        if v is not None and str(v).strip() == field:
            return c
    return 0


def txt(v):
    return "" if v is None else str(v).replace("\r\n", "\n").replace("\r", "\n").strip()


def update_reward_type(ws):
    """`RewardType` 시트 맨 아래에 줄을 붙인다. 이미 있으면 아무것도 하지 않는다."""
    c_type = find_col(ws, "reward_type") or 1
    c_desc = find_col(ws, "reward_type_desc") or 2
    c_dur = find_col(ws, "use_duration") or 3

    last = ws.UsedRange.Rows.Count
    have = set()
    for r in range(4, last + 1):
        have.add(txt(ws.Cells(r, c_type).Value))

    row = last + 1
    added = 0
    for name, desc, dur in REWARD_ROWS:
        if name in have:
            print("    이미 있음:", name)
            continue
        ws.Cells(row, c_type).Value = name
        if desc:
            ws.Cells(row, c_desc).Value = desc
            ws.Cells(row, c_dur).Value = dur
        row += 1
        added += 1
        print("    + %s" % name)
    return added


def update_choices(ws):
    """아홉 선택지의 <b>둘째 보상 칸</b>에 유물을 붙이고 효과 요약을 잇는다."""
    c_cid = find_col(ws, "choice_id") or 2
    c_eff = find_col(ws, "result_effect")
    c_t2 = find_col(ws, "reward_type_02")
    c_v2 = find_col(ws, "reward_value_02")
    c_d2 = find_col(ws, "reward_duration_02")
    for name, col in (("result_effect", c_eff), ("reward_type_02", c_t2),
                      ("reward_value_02", c_v2), ("reward_duration_02", c_d2)):
        if col == 0:
            print("  ! %s 열을 찾지 못했습니다" % name)
            return -1

    last = ws.UsedRange.Rows.Count
    seen, changed = set(), 0

    for r in range(4, last + 1):
        raw = ws.Cells(r, c_cid).Value
        if raw is None:
            continue
        try:
            cid = int(raw)
        except (TypeError, ValueError):
            continue
        if cid not in RELIC_REWARDS:
            continue

        seen.add(cid)
        relic_id, relic_name, grade, why = RELIC_REWARDS[cid]

        cur_type = txt(ws.Cells(r, c_t2).Value)
        if cur_type and cur_type != "relic_gain":
            # ⚠⚠ <b>절대 밀어내지 않는다.</b> 여기 걸리면 표가 이 스크립트를 쓴 뒤에
            #   바뀐 것이다 — 조용히 덮으면 그 보상이 사라진 것을 아무도 모른다.
            print("    ⚠ %d: 둘째 칸이 이미 '%s' 로 차 있어 <b>건너뜁니다</b>" % (cid, cur_type))
            continue

        ws.Cells(r, c_t2).Value = "relic_gain"
        ws.Cells(r, c_v2).Value = relic_id
        ws.Cells(r, c_d2).Value = 0

        suffix = EFFECT_SUFFIX.format(name=relic_name, grade=grade)
        eff = txt(ws.Cells(r, c_eff).Value)
        if "유물 「" not in eff:
            ws.Cells(r, c_eff).Value = eff + suffix

        changed += 1
        print("    %d  ->  %d 「%s」 (%s)" % (cid, relic_id, relic_name, grade))

    missing = sorted(set(RELIC_REWARDS) - seen)
    if missing:
        print("    ⚠ 표에서 못 찾은 choice_id: %s" % missing)
    return changed


def main():
    if not os.path.isfile(EVENT_XLSX):
        print("파일이 없습니다:", EVENT_XLSX)
        return 1

    import win32com.client as win32

    backup((EVENT_XLSX,), "이벤트_유물보상")

    excel = win32.DispatchEx("Excel.Application")
    excel.Visible = False
    excel.DisplayAlerts = False
    n = 0
    try:
        wb = excel.Workbooks.Open(EVENT_XLSX)

        print("[RewardType 시트]")
        n += max(0, update_reward_type(wb.Worksheets("RewardType")))

        print("[ChoiceGroup 시트]")
        c = update_choices(wb.Worksheets("ChoiceGroup"))
        if c < 0:
            wb.Close(SaveChanges=False)
            return 1
        n += c

        if n:
            wb.Save()
        wb.Close(SaveChanges=False)
    finally:
        excel.Quit()

    print("총 %d칸을 고쳤습니다 (보상 타입 1 + 선택지 9 를 기대합니다)." % n)
    print("다음: py -3 Tools/gen_event_assets.py")
    return 0


if __name__ == "__main__":
    sys.exit(main())
