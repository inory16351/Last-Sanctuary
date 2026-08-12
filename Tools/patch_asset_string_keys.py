# -*- coding: utf-8 -*-
"""손으로/MCP 로 만들어 둔 ScriptableObject 에셋에 스트링 키(`nameKey`)를 넣는다.

`gen_character_assets.py` 가 매번 새로 쓰는 캐릭터·스킬 에셋과 달리, 아래 에셋들은
생성 스크립트가 없어서 **YAML 을 직접 패치**한다(진행상황 5절·8절에서 쓰던 방식 —
SO 에셋을 다루는 MCP 도구가 없다).

멱등하다 — 이미 `nameKey` 가 있으면 값만 맞추고, 없으면 기준 필드 바로 앞에 끼워 넣는다.
⚠️ .asset YAML 에 **빈 줄을 넣으면 Unity 파서가 그 뒤 필드를 전부 무시한다**(8절 3번).
   그래서 줄 삽입만 하고 다른 줄은 건드리지 않는다.
"""
import os
import re

_PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def patch(rel_path, key, anchor_field):
    """`anchor_field:` 줄 바로 앞에 `nameKey: <key>` 를 넣는다."""
    path = os.path.join(_PROJECT, rel_path.replace('/', os.sep))
    if not os.path.exists(path):
        print('  ! 파일 없음:', rel_path)
        return False

    with open(path, encoding='utf-8') as f:
        lines = f.read().split('\n')

    # 이미 있으면 값만 맞춘다.
    for i, line in enumerate(lines):
        if re.match(r'^  nameKey:', line):
            if line.strip() == f'nameKey: {key}':
                print('  = 그대로:', rel_path)
                return False
            lines[i] = f'  nameKey: {key}'
            write(path, lines)
            print('  ~ 값 갱신:', rel_path, '->', key)
            return True

    for i, line in enumerate(lines):
        if line.startswith(f'  {anchor_field}:'):
            lines.insert(i, f'  nameKey: {key}')
            write(path, lines)
            print('  + 추가:', rel_path, '->', key)
            return True

    print(f'  ! 기준 필드 없음: {rel_path} ({anchor_field})')
    return False


def write(path, lines):
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines))


# (에셋 경로, 스트링 키, 키를 끼워 넣을 기준 필드)
TARGETS = [
    # 정신 이상 11종 — 테이블 mental_error_id 40001~40011 순서 그대로다.
    ('Assets/_Project/Resources/MentalErrors/MentalError_01_Confusion.asset',   'mental_error_name_40001', 'koreanName'),
    ('Assets/_Project/Resources/MentalErrors/MentalError_02_SettleDown.asset',  'mental_error_name_40002', 'koreanName'),
    ('Assets/_Project/Resources/MentalErrors/MentalError_03_Arousal.asset',     'mental_error_name_40003', 'koreanName'),
    ('Assets/_Project/Resources/MentalErrors/MentalError_04_Terrified.asset',   'mental_error_name_40004', 'koreanName'),
    ('Assets/_Project/Resources/MentalErrors/MentalError_05_Depression.asset',  'mental_error_name_40005', 'koreanName'),
    ('Assets/_Project/Resources/MentalErrors/MentalError_06_Madness.asset',     'mental_error_name_40006', 'koreanName'),
    ('Assets/_Project/Resources/MentalErrors/MentalError_07_Upsurge.asset',     'mental_error_name_40007', 'koreanName'),
    ('Assets/_Project/Resources/MentalErrors/MentalError_08_SelfHarm.asset',    'mental_error_name_40008', 'koreanName'),
    ('Assets/_Project/Resources/MentalErrors/MentalError_09_Masochism.asset',   'mental_error_name_40009', 'koreanName'),
    ('Assets/_Project/Resources/MentalErrors/MentalError_10_Selfishness.asset', 'mental_error_name_40010', 'koreanName'),
    ('Assets/_Project/Resources/MentalErrors/MentalError_11_Disgusting.asset',  'mental_error_name_40011', 'koreanName'),

    # 중립 몬스터 — 테이블 mon_id 와 그대로 맞는다.
    ('Assets/_Project/Data/Units/NeutralMonster_1.asset', 'mon_name_1001', 'displayName'),
    ('Assets/_Project/Data/Units/NeutralMonster_2.asset', 'mon_name_1002', 'displayName'),
    ('Assets/_Project/Data/Units/NeutralMonster_3.asset', 'mon_name_1003', 'displayName'),

    # 건물 — Const_id 10002 포탑. (중앙건물 10001 은 NexusDefinitionSO 라 이 타입이 아니다)
    ('Assets/_Project/Data/Buildings/Building_Turret.asset', 'const_name_10002', 'displayName'),

    # ★ 웨이브 몬스터 3종 — <b>웨이브 몬스터 테이블이 최신 정본</b>이므로 그 이름으로 맞춘다
    #   (유저 지시 2026-08-12: "웨이브 몬스터 테이블이 최신 버전이니 파일들 다 거기에 맞춰서 수정").
    #   ⚠️ <b>게임에 보이는 이름이 실제로 바뀐다</b>:
    #      근거리 암세포 → 지옥 송곳니 · 원거리 암세포 → 영혼 사수 · 암세포 군주 → 단탈리온
    #   리터럴(displayName)은 폴백으로 남아 있지만 키가 먼저 잡히므로 화면에는 표의 이름이 뜬다.
    ('Assets/_Project/Data/Units/Monster_HellFang.asset',  'monster_name_100001', 'displayName'),
    ('Assets/_Project/Data/Units/Monster_SoulArcher.asset', 'monster_name_100002', 'displayName'),
    ('Assets/_Project/Data/Units/Monster_Dantalian.asset',   'monster_name_120001', 'displayName'),
]


def main():
    changed = 0
    for rel, key, anchor in TARGETS:
        if patch(rel, key, anchor):
            changed += 1
    print(f'\n패치된 에셋 {changed} / 대상 {len(TARGETS)}')


if __name__ == '__main__':
    main()
