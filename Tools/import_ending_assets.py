# -*- coding: utf-8 -*-
"""엔딩 에셋을 볼트에서 프로젝트로 들여온다 (2026-08-25 신설).

유저 지시: *"아 그리고 엔딩도 만들어줘 필요한건 다 넣었음"* · *"패배 이미지도 추가"*

들여오는 것 넷 —
    볼트 리소스/Ending_bg.png            → Resources/Ending/BG_01 ~ BG_04   (2x2 로 자른다)
    볼트 리소스/Ending/01-01.mp3 …        → Resources/Ending/VO_01_1 …       (17개)
    볼트 리소스/Ending/The Unspoken Oath  → Resources/Bgm/                   (엔딩 브금)
    볼트 리소스/Defeat_bg.png             → Resources/UI/Result/DefeatBg.png (패배 창 배경)

★ <b>오프닝과 같은 규격이다</b> — Ending_bg.png 가 1672x941(정확히 16:9)이고, 이것을
  2x2 로 자르면 각 칸이 836x470 으로 <b>또 16:9</b> 다. opening_BG.png 와 완전히 같다.

★★ <b>자른 순서 = 이야기 순서</b> (오프닝의 교훈을 뒤집은 것).
  오프닝은 2x2 를 «왼위·오른위·왼아래·오른아래» 로 자른 순서가 <b>이야기 순서가 아니어서</b>
  나중에 배경을 대사에 맞춰 다시 짝지어야 했다(142절). 이번에는 그림을 <b>읽는 순서로
  주문했으므로</b> 자른 결과가 곧 컷 1~4 다. 다시 짝지을 일이 없다.

★ <b>mp3 는 다시 굽지 않고 그대로 복사한다</b> — 풀어서 다시 인코딩하면 음질만 깎인다.
  앞뒤 묵음도 <b>다듬지 않는다</b>: 유저가 내보낸 파일이 정본이고, 다듬기의 «말소리 문턱» 은
  조용히 끝나는 낱말을 잘라먹을 수 있다(오프닝에서 실제로 겪었다).

사용법:  python Tools/import_ending_assets.py
⚠ 그 뒤 Unity 에서 Assets/Refresh 를 실행할 것.
"""
import os
import shutil
import subprocess
import sys

from PIL import Image

from vault_path import VAULT, PROJECT

try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

SRC_DIR = os.path.join(VAULT, '리소스')
SRC_ENDING = os.path.join(SRC_DIR, 'Ending')

DST_ENDING = os.path.join(PROJECT, 'Assets', '_Project', 'Resources', 'Ending')
DST_BGM = os.path.join(PROJECT, 'Assets', '_Project', 'Resources', 'Bgm')
DST_RESULT = os.path.join(PROJECT, 'Assets', '_Project', 'Resources', 'UI', 'Result')

BGM_NAME = 'The Unspoken Oath.mp3'

FFPROBE = shutil.which('ffprobe') or (
    r'C:\Users\user\AppData\Local\Microsoft\WinGet\Packages'
    r'\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe'
    r'\ffmpeg-8.1.1-full_build\bin\ffprobe.exe')

# 컷마다 조각이 몇 개인지 — 대본과 같아야 한다 (3 · 5 · 4 · 5 = 17)
CUT_SIZES = [3, 5, 4, 5]


def duration(path):
    out = subprocess.run([FFPROBE, '-v', 'error', '-show_entries', 'format=duration',
                          '-of', 'csv=p=0', path],
                         capture_output=True, text=True).stdout.strip()
    return float(out) if out else 0.0


def crop_sheet():
    """1672x941 시트를 2x2 로 잘라 BG_01~04 로 저장한다 (읽는 순서)."""
    src = os.path.join(SRC_DIR, 'Ending_bg.png')
    im = Image.open(src)
    w, h = im.size

    # ⚠ 시트 높이가 <b>홀수</b>(941)면 아래 두 칸이 1픽셀 커진다 — 넉 장의 크기가
    #   갈리면 AspectRatioFitter 가 컷마다 미세하게 다른 비율을 잡는다. 짝수로 깎는다.
    w -= w % 2
    h -= h % 2
    hw, hh = w // 2, h // 2
    print('시트 %dx%d → 칸 %dx%d (비율 %.3f)' % (w, h, hw, hh, hw / hh))

    boxes = [(0, 0, hw, hh),        # ① 좌상 = 컷 1
             (hw, 0, w, hh),        # ② 우상 = 컷 2
             (0, hh, hw, h),        # ③ 좌하 = 컷 3
             (hw, hh, w, h)]        # ④ 우하 = 컷 4

    os.makedirs(DST_ENDING, exist_ok=True)
    for i, box in enumerate(boxes, 1):
        out = os.path.join(DST_ENDING, 'BG_%02d.png' % i)
        im.crop(box).save(out)
        print('  BG_%02d.png  %s' % (i, im.crop(box).size))


def copy_voices():
    """01-01.mp3 → VO_01_1.mp3 로 이름만 바꿔 그대로 복사한다."""
    os.makedirs(DST_ENDING, exist_ok=True)
    rows = []
    for cut, count in enumerate(CUT_SIZES, 1):
        for frag in range(1, count + 1):
            src = os.path.join(SRC_ENDING, '%02d-%02d.mp3' % (cut, frag))
            if not os.path.exists(src):
                print('  ⚠ 없음: %s' % os.path.basename(src))
                continue
            dst = os.path.join(DST_ENDING, 'VO_%02d_%d.mp3' % (cut, frag))
            shutil.copyfile(src, dst)
            rows.append((cut, frag, duration(dst)))
    total = sum(r[2] for r in rows)
    print('음성 %d개 복사 · 합계 %.2f초' % (len(rows), total))
    return rows


def copy_bgm():
    os.makedirs(DST_BGM, exist_ok=True)
    src = os.path.join(SRC_ENDING, BGM_NAME)
    dst = os.path.join(DST_BGM, BGM_NAME)
    shutil.copyfile(src, dst)
    d = duration(dst)
    print('브금 %s · %.2f초' % (BGM_NAME, d))
    return d


def copy_defeat_bg():
    os.makedirs(DST_RESULT, exist_ok=True)
    src = os.path.join(SRC_DIR, 'Defeat_bg.png')
    dst = os.path.join(DST_RESULT, 'DefeatBg.png')
    shutil.copyfile(src, dst)
    print('패배 배경 %dx%d' % Image.open(dst).size)


def main():
    print('== 배경 시트 자르기 ==')
    crop_sheet()
    print()
    print('== 음성 ==')
    rows = copy_voices()
    print()
    print('== 브금 ==')
    bgm = copy_bgm()
    print()
    print('== 패배 배경 ==')
    copy_defeat_bg()
    print()

    print('조각 길이 (컷-조각: 초)')
    for cut, frag, d in rows:
        print('  %02d-%d  %6.3f' % (cut, frag, d))
    speech = sum(r[2] for r in rows)
    print('내레이션 합계 %.2f초 / 브금 %.2f초 — 여유 %.2f초' % (speech, bgm, bgm - speech))
    print()
    print('⚠ Unity 에서 Assets/Refresh 를 실행할 것.')


if __name__ == '__main__':
    main()
