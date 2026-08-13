# -*- coding: utf-8 -*-
"""데이터 테이블(xlsx) → Unity ScriptableObject 에셋 동기화.

`gen_character_assets.py` 가 캐릭터·패시브 스킬을 맡는다면, 이 스크립트는 **나머지 전부**를 맡는다:
정신 이상 11종 · 웨이브 몬스터(잡몹·중간보스·최종보스) · 포탑/중앙건물 · 웨이브 구성표.

진행상황 54절(다른 PC 의 테이블 재밸런싱)이 "게임 미반영" 으로 남긴 항목들을 실제로 반영하는
파이프라인이다. 54-10절이 손으로 옮겨야 한다고 적어둔 것을 스크립트로 만든 것 -
표가 정본이고 이 스크립트가 그대로 옮긴다. **값을 손으로 옮겨 적지 않는다.**

⚠ 왜 스크립트인가 (MCP 가 아니라) - MCP 에는 **ScriptableObject 에셋(.asset)을 다루는 도구가
  없다.** `update_component` 는 씬의 GameObject 컴포넌트만 만진다. 그래서 씬 오브젝트는 MCP 로,
  에셋은 이 스크립트로 갈라 놓는다(진행상황 5절·8절부터 이 프로젝트가 쓰는 방식이고,
  33-5절의 `gen_character_assets.py` 도 같은 이유로 스크립트다).

⚠ .asset YAML 에 **빈 줄을 넣으면 Unity 파서가 그 뒤 필드를 전부 무시한다**(8절 3번).
  이 스크립트는 기존 파일의 필드 값만 **한 줄씩 치환**하고 구조는 건드리지 않는다 -
  전체를 다시 쓰면 내가 모르는 필드(나중에 추가된 것)를 날릴 수 있기 때문이다.
"""
import os
import re
import sys

# 콘솔이 cp949 라 한글 출력에서 죽는다 - 출력만 UTF-8 로 바꾼다(파일 내용과 무관).
try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass
import hashlib
import openpyxl

VAULT = r'C:\Project\Last-Sanctuary-Vault\데이터 테이블'
_PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(_PROJECT, 'Assets', '_Project')

XLSX_MENTAL = os.path.join(VAULT, '정신 이상 테이블.xlsx')
XLSX_WAVE_MON = os.path.join(VAULT, '웨이브 몬스터 테이블.xlsx')
XLSX_WAVE = os.path.join(VAULT, '웨이브테이블.xlsx')
XLSX_BUILDING = os.path.join(VAULT, 'Last_Sanctuary_건물데이터시트_Ver05.xlsx')

SCRIPT_GUID_MONSTER = '5dbe527860d1cbe42a3efae9fd5cb4b2'   # MonsterDefinitionSO.cs

ASSET_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""

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


def guid_for(key):
    """gen_character_assets.py 와 같은 규칙 - 경로에서 결정적으로 만든다(다시 돌려도 같은 guid)."""
    return hashlib.md5(('LastSanctuary/' + key).encode('utf-8')).hexdigest()


def num(v, default=0):
    if v is None:
        return default
    try:
        f = float(v)
    except (TypeError, ValueError):
        return default
    return int(f) if f == int(f) else f


def read_sheet(path, sheet, first_row=4):
    wb = openpyxl.load_workbook(path, data_only=True)
    ws = wb[sheet]
    rows = []
    for r in range(first_row, ws.max_row + 1):
        vals = [ws.cell(r, c).value for c in range(1, ws.max_column + 1)]
        if vals and vals[0] is not None:
            rows.append(vals)
    return rows


def patch_fields(path, changes, label):
    """에셋 파일의 `  key: value` 줄만 치환한다. 없는 키는 조용히 건너뛴다(보고만 한다)."""
    if not os.path.exists(path):
        print('  ! 없는 파일:', path)
        return 0
    with open(path, encoding='utf-8') as f:
        text = f.read()

    hit = 0
    for key, value in changes.items():
        pattern = re.compile(r'^(\s*%s:).*$' % re.escape(key), re.M)
        if not pattern.search(text):
            print('  ! %s: 필드 %r 가 없습니다 (건너뜀)' % (label, key))
            continue
        text, n = pattern.subn(lambda m: '%s %s' % (m.group(1), value), text, count=1)
        hit += n

    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    print('  %s: %d개 필드 갱신' % (label, hit))
    return hit


# ---------------------------------------------------------------------------
# 1) 정신 이상 11종 - 54-6절 (after_erosion 45~55 로 낮춤 · 가중치 재배분)
# ---------------------------------------------------------------------------
def sync_mental_errors():
    print('[정신 이상]')
    folder = os.path.join(ASSETS, 'Resources', 'MentalErrors')
    by_id = {}
    for name in os.listdir(folder):
        if not name.endswith('.asset'):
            continue
        with open(os.path.join(folder, name), encoding='utf-8') as f:
            m = re.search(r'mentalErrorId:\s*(\d+)', f.read())
        if m:
            by_id[int(m.group(1))] = os.path.join(folder, name)

    total = 0
    weight_sum = 0.0
    for row in read_sheet(XLSX_MENTAL, 'mental_error'):
        mid = int(row[0])
        if mid not in by_id:
            print('  ! id %d 에 해당하는 에셋이 없습니다' % mid)
            continue
        weight_sum += float(num(row[6]))
        total += patch_fields(by_id[mid], {
            'value01': num(row[2]),
            'value02': num(row[3]),
            'durationSeconds': num(row[4]),
            'afterErosion': int(num(row[5])),
            'activationProbability': num(row[6]),
        }, 'MentalError %d' % mid)

    # 29-3절: 이 값은 "판정 확률"이 아니라 추첨 가중치이고 합이 1.00 이어야 한다.
    print('  가중치 합 = %.4f %s' % (weight_sum, '(OK)' if abs(weight_sum - 1.0) < 1e-6 else '(⚠ 1.00 아님!)'))
    return total


# ---------------------------------------------------------------------------
# 2) 웨이브 몬스터 - 잡몹 2종 + 최종보스 갱신, 중간보스 2종 신규 생성
# ---------------------------------------------------------------------------
# 표의 monster_id → 기존 에셋 파일명. 중간보스는 아직 에셋이 없어서 새로 만든다(아래).
MONSTER_ASSET_BY_ID = {
    100001: 'Monster_HellFang',
    100002: 'Monster_SoulArcher',
    120001: 'Monster_Dantalian',
}

# 중간보스 - 54-4절. tier=MidBoss(1).
#
# ⚠ **전투 파라미터를 지어내지 않는다** - 같은 공격 타입의 잡몹 에셋에서 그대로 물려받는다
#   (`inherit` 이 그 에셋 이름이다). 유저 지시 4번이 "중간 보스 인게임 모션은 임시로 일반
#   몬스터 스킨을 그대로 쓰는 것이니 신경 쓰지 말고 냅둬" 였고, 표에도 사거리·인식범위 칸이
#   없다. 표에 있는 것(능력치·체력보정)만 표에서 가져오고 나머지는 물려받는 것이 정확하다.
#
# ⚠ **외형(template)은 이 에셋에 넣을 수 없다** - ScriptableObject 는 씬 오브젝트를 참조할 수
#   없다(진행상황 5절). 스포너의 슬롯이 템플릿을 지정하는 구조이므로 씬에서 MCP 로 연결한다.
# ⚠ 에셋 이름은 **표의 `character_name_EG`** 를 따른다(2026-08-13). 예전에는 물려받는
#   잡몹 이름(`Monster_MidBoss_HellFang`)을 그대로 썼는데, 그러면 하이라키·에셋 목록에서
#   중간보스가 잡몹으로 보인다. 표에 영어 이름 컬럼이 아예 없어서 그랬던 것이라
#   컬럼을 만들고(BloodMark · VoidWhisper) 그 값으로 개명했다.
MID_BOSS = {
    110001: dict(asset='Monster_MidBoss_BloodMark', name='혈인', inherit='Monster_HellFang', render_h=3),
    110002: dict(asset='Monster_MidBoss_VoidWhisper', name='공허의 속삭임', inherit='Monster_SoulArcher', render_h=3),
}

# 능력치 → 공속/이속 치환은 게임이 인스펙터 값을 그대로 쓰므로(몬스터는 StatMoveSpeedTiles 0)
# 표의 공속·이속 스탯을 38-1절 공식으로 미리 풀어서 넣는다.
def aspd_from_stat(s):
    return round(0.6 + 3.0 * s / (s + 50.0), 3)


def mspd_from_stat(s):
    return round(2.1 + 3.9 * s / (s + 50.0), 3)


def sync_monsters():
    print('[웨이브 몬스터]')
    stats = {int(r[0]): r for r in read_sheet(XLSX_WAVE_MON, 'first_Stat')}
    folder = os.path.join(ASSETS, 'Data', 'Units')
    total = 0

    # --- 기존 3종 갱신 ---
    for mid, asset in MONSTER_ASSET_BY_ID.items():
        r = stats.get(mid)
        if r is None:
            print('  ! 표에 id %d 가 없습니다' % mid)
            continue
        melee, ranged = int(num(r[2])), int(num(r[3]))
        total += patch_fields(os.path.join(folder, asset + '.asset'), {
            'hpStat': int(num(r[1])),
            'attackStat': max(melee, ranged),      # atk_type 에 해당하는 칸만 채워져 있다
            'defenseStat': int(num(r[10])),
            'regenStat': int(num(r[11])),
            'hpPercent': int(num(r[13], 100)),
            'attacksPerSecond': aspd_from_stat(num(r[8])),
        }, asset)

    # --- 중간보스 2종 신규 ---
    for mid, spec in MID_BOSS.items():
        r = stats.get(mid)
        if r is None:
            print('  ! 표에 중간보스 id %d 가 없습니다' % mid)
            continue
        melee, ranged = int(num(r[2])), int(num(r[3]))
        rel = 'Data/Units/%s.asset' % spec['asset']
        path = os.path.join(folder, spec['asset'] + '.asset')

        # 전투 파라미터는 같은 타입의 잡몹에서 물려받는다 (표에 없는 값을 지어내지 않는다).
        with open(os.path.join(folder, spec['inherit'] + '.asset'), encoding='utf-8') as f:
            base = f.read()

        def inherited(key, default):
            m = re.search(r'^\s*%s:\s*(\S+)\s*$' % re.escape(key), base, re.M)
            return m.group(1) if m else default

        body = HEADER.format(script_guid=SCRIPT_GUID_MONSTER, name=spec['asset'])
        body += "  nameKey: monster_name_%d\n" % mid
        body += "  displayName: %s\n" % spec['name']
        body += "  tier: 1\n"                       # MonsterTier.MidBoss
        body += "  template: {fileID: 0}\n"         # 스포너 슬롯이 지정한다 (위 ⚠ 참조)
        body += "  hpStat: %d\n" % int(num(r[1]))
        body += "  attackStat: %d\n" % max(melee, ranged)
        body += "  defenseStat: %d\n" % int(num(r[10]))
        body += "  regenStat: %d\n" % int(num(r[11]))
        body += "  hpPercent: %d\n" % int(num(r[13], 100))
        body += "  attackType: %s\n" % inherited('attackType', '0')
        body += "  detectRange: %s\n" % inherited('detectRange', '7')
        body += "  attackRange: %s\n" % inherited('attackRange', '1.2')
        body += "  attacksPerSecond: %s\n" % aspd_from_stat(num(r[8]))
        body += "  moveSpeedTiles: %s\n" % mspd_from_stat(num(r[9]))
        body += "  footprintTiles: %s\n" % inherited('footprintTiles', '1')
        # 크기 - <b>기준은 타일</b>이다(유저 확정 2026-08-13, 진행상황 61절).
        # 예전 값(spriteScale 2 = "잡몹 스킨의 2배")은 원화 픽셀에 매인 배율이라
        # 스킨이 바뀌면 크기가 같이 흔들렸다. 이제 "몇 타일로 보일지"만 적는다.
        # bodyWidth/Height·spriteScale 은 스킨이 없는 경우의 폴백으로만 남는다.
        # ⚠ 이 블록이 없으면 이 스크립트를 다시 돌릴 때마다 크기가 초기화된다
        #   (중간보스 에셋은 갱신이 아니라 전체 재작성이다).
        body += "  bodyWidthTiles: 2\n"
        body += "  bodyHeightTiles: 2\n"
        body += "  spriteScale: 2\n"
        body += "  renderHeightTiles: %s\n" % spec.get('render_h', 3)

        with open(path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(body)
        mp = path + '.meta'
        if not os.path.exists(mp):
            with open(mp, 'w', encoding='utf-8', newline='\n') as f:
                f.write(ASSET_META.format(guid=guid_for(rel)))
        print('  %s 생성 (id %d, HP스탯 %s, 보정 %s%%)'
              % (spec['asset'], mid, int(num(r[1])), int(num(r[13], 100))))
        total += 1
    return total


# ---------------------------------------------------------------------------
# 3) 건물 - 54-7절 (포탑 상향 · 중앙건물은 이미 게임 값과 같다)
# ---------------------------------------------------------------------------
def sync_buildings():
    print('[건물]')
    const = {int(r[0]): r for r in read_sheet(XLSX_BUILDING, 'Construction')}
    atk = {int(r[0]): r for r in read_sheet(XLSX_BUILDING, 'ATK')}

    turret = const.get(10002)
    if turret is None:
        print('  ! Construction 시트에 10002(포탑)가 없습니다')
        return 0

    a = atk.get(int(num(turret[9])))    # ATK_ID
    changes = {
        'hp': int(num(turret[4])),
        'defenseStat': int(num(turret[7])),
        'attackStat': int(num(turret[8])),
        'buildSeconds': int(num(turret[11])),
        'maxCount': int(num(turret[3])),
    }
    if a is not None:
        changes['attacksPerSecond'] = num(a[2])
        changes['attackRange'] = num(a[3])

    n = patch_fields(os.path.join(ASSETS, 'Data', 'Buildings', 'Building_Turret.asset'),
                     changes, 'Building_Turret')

    # 중앙건물(10001) - 게임은 능력치 치환(체력스탯 100 × 보정 250% = 2,600)으로 같은 값을
    # 이미 만들고 있다(54-7절이 "시트를 게임에 맞췄다"). 표의 HP/DEF 와 실제 값이 같은지만 검사한다.
    core = const.get(10001)
    if core is not None:
        want_hp, want_def = int(num(core[4])), int(num(core[7]))
        nexus = os.path.join(ASSETS, 'Data', 'Combat', 'NexusDefinition.asset')
        with open(nexus, encoding='utf-8') as f:
            t = f.read()
        hp_stat = int(re.search(r'hpStat:\s*(\d+)', t).group(1))
        hp_pct = int(re.search(r'hpPercent:\s*(\d+)', t).group(1))
        d = int(re.search(r'defenseStat:\s*(\d+)', t).group(1))
        actual = round((40 + hp_stat * 10) * hp_pct / 100)
        ok = (actual == want_hp and d == want_def)
        print('  중앙건물: 표 HP %d/DEF %d ↔ 게임 %d/DEF %d %s'
              % (want_hp, want_def, actual, d, '(일치)' if ok else '(⚠ 불일치)'))
    return n


# ---------------------------------------------------------------------------
# 4) 웨이브 구성표 - 54-5절 (중간보스 수량 컬럼 신설 + 5·15웨이브 잡몹 감소)
# ---------------------------------------------------------------------------
def sync_waves():
    print('[웨이브 구성표]')
    path = os.path.join(ASSETS, 'Data', 'Wave', 'WaveDefinitions.asset')
    with open(path, encoding='utf-8') as f:
        text = f.read()

    # 증원(reinforce*)은 표에 없는 값이다 - 27절이 코드/에셋 쪽에서 정한 것이므로
    # 표를 반영할 때 **덮어쓰지 않고 기존 값을 그대로 유지**한다(54-8절과 같은 취지).
    keep = {}
    for m in re.finditer(r'- waveNumber: (\d+)(.*?)(?=\n  - waveNumber:|\Z)', text, re.S):
        blk = m.group(2)
        keep[int(m.group(1))] = (
            re.search(r'reinforceIntervalSeconds: ([\d.]+)', blk).group(1),
            re.search(r'reinforceCount: (\d+)', blk).group(1),
        )

    rows = read_sheet(XLSX_WAVE, 'Sheet2')
    out = ['  waves:']
    for r in rows:
        wn = int(num(r[1]))
        ri, rc = keep.get(wn, ('0', '0'))
        out.append('  - waveNumber: %d' % wn)
        out.append('    meleeCount: %d' % int(num(r[2])))
        out.append('    rangedCount: %d' % int(num(r[3])))
        out.append('    bossCount: %d' % int(num(r[4])))
        out.append('    midBossCount: %d' % int(num(r[6])))
        out.append('    statPercent: %d' % round(float(num(r[5])) * 100))
        out.append('    reinforceIntervalSeconds: %s' % ri)
        out.append('    reinforceCount: %s' % rc)

    head = text[:text.index('  waves:')]
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(head + '\n'.join(out) + '\n')
    mids = [int(num(r[6])) for r in rows]
    print('  웨이브 %d개 기록 · 중간보스가 나오는 웨이브: %s'
          % (len(rows), [int(num(r[1])) for r in rows if int(num(r[6])) > 0] or '없음'))
    return len(rows)


if __name__ == '__main__':
    sync_mental_errors()
    sync_monsters()
    sync_buildings()
    sync_waves()
    print('\n완료 - Unity 에서 Assets/Refresh 를 실행할 것.')
