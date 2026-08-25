# -*- coding: utf-8 -*-
"""엔딩 연출의 <b>시각표</b>를 계산한다 (2026-08-25 신설).

오프닝의 `import_opening_voice.py` 와 같은 일을 하지만 <b>에셋 복사는 하지 않는다</b> —
그건 `import_ending_assets.py` 가 이미 했다. 이 스크립트는 <b>시각만</b> 찍는다.

★★ 왜 시각을 손으로 재지 않는가 — 조각이 17개다. 하나를 0.2초 고치면 <b>뒤가 다 밀린다</b>.
   그리고 이 연출의 시계는 «브금의 절대 시각» 이므로(EndingDirector 의 _clock),
   표에 적히는 값은 «몇 초 보여줄지» 가 아니라 «브금의 몇 초에 시작할지» 다.

★★★ <b>박자 격자에 맞춘다</b> — 컷 전환과 조각 시작을 노래의 박 위에 올린다.
   격자는 이 스크립트가 <b>브금을 분석해 실측</b>한다(아래 measure_grid).

★ <b>오프닝과 다른 점 — 여유가 크다.</b>
   오프닝: 내레이션 93.07초 / 브금 119.65초 → 여유 26.6초 (촘촘하다)
   엔딩  : 내레이션 67.50초 / 브금 119.77초 → 여유 52.3초 (헐렁하다)
   그래서 오프닝의 «최소 텀»(문장 0.35 · 절 0.15)을 그대로 쓰면 <b>88초에 다 끝나고
   30초가 무음으로 남는다</b>. 엔딩은 텀을 <b>박 단위로 크게</b> 잡아 곡을 다 쓴다 —
   느린 낭독이 엔딩의 결에도 맞다.

사용법:  python Tools/gen_ending_schedule.py
결과를 EndingDirector.cs 의 slides 표에 그대로 옮긴다.
"""
import os
import shutil
import subprocess
import sys
import tempfile

import numpy as np
from scipy.io import wavfile

from vault_path import PROJECT

try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

FFMPEG = shutil.which('ffmpeg') or (
    r'C:\Users\user\AppData\Local\Microsoft\WinGet\Packages'
    r'\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe'
    r'\ffmpeg-8.1.1-full_build\bin\ffmpeg.exe')
# ⚠ 경로 전체에서 갈아치우면 안 된다 — 폴더 이름에도 «ffmpeg» 가 들어 있어
#   `ffprobe-8.1.1-full_build` 라는 없는 폴더가 된다. <b>파일 이름만</b> 바꾼다.
FFPROBE = os.path.join(os.path.dirname(FFMPEG),
                       os.path.basename(FFMPEG).replace('ffmpeg', 'ffprobe'))

RES = os.path.join(PROJECT, 'Assets', '_Project', 'Resources')
BGM = os.path.join(RES, 'Bgm', 'The Unspoken Oath.mp3')
VO_DIR = os.path.join(RES, 'Ending')

# ── 대본 ────────────────────────────────────────────────────────────────
# (컷, 조각, 끝 문장부호)  — 끝 부호가 «텀» 을 정한다:
#   '.' 문장이 끝났다 → 숨을 쉬는 자리 (긴 텀)
#   ',' '—' '…' 이어 읽는다 → 짧은 텀
SCRIPT = [
    (1, 1, '.'), (1, 2, '.'), (1, 3, '.'),
    (2, 1, '.'), (2, 2, ','), (2, 3, '.'), (2, 4, '.'), (2, 5, '…'),
    (3, 1, ','), (3, 2, '.'), (3, 3, '.'), (3, 4, '…'),
    (4, 1, '.'), (4, 2, '.'), (4, 3, '—'), (4, 4, '.'), (4, 5, '.'),
]

# ── 연출 규칙 (박 단위) ─────────────────────────────────────────────────
# ★ 초가 아니라 <b>박</b>으로 적는다 — 격자를 실측한 뒤 초로 환산한다.
#   그래야 텀이 «노래에 맞는» 길이가 되고, 곡이 바뀌어도 규칙이 살아 있다.
INTRO_BEATS = 2.0        # ★ 첫 박에서 이만큼 뒤에 컷 1 이 밝아지기 시작한다.
                         #   0 으로 두면 브금이 시작하는 <b>그 순간</b> 화면이 밝아져
                         #   «시작 버튼을 누르자마자 들이닥친다». 오프닝도 1.59초를 비워 뒀다.
FADE_IN_BEATS = 2.0      # 컷이 밝아지는 시간
LEAD_BEATS = 1.0         # 밝아진 뒤 첫 조각이 말을 시작하기까지
GAP_SENTENCE_BEATS = 2.0 # 문장이 끝난 뒤
GAP_CLAUSE_BEATS = 1.0   # 문장 안에서 끊길 때
HOLD_BEATS = 2.0         # 컷의 말이 끝난 뒤 머무는 시간
FADE_OUT_BEATS = 1.5     # 검게 지는 시간
HOLD_LAST_BEATS = 3.0    # 마지막 컷이 머무는 시간

# ★★ 컷마다 머묾을 따로 줄 수 있다 — <b>컷 2 에는 전사자 명단이 뜬다</b>.
#   기본 머묾(2박 = 1.6초)으로는 이름을 <b>읽을 시간이 안 난다</b>. 명단은 2-4
#   («그 이름은 성역에 새겨질 것이다») 뒤에 떠서 컷이 끝날 때까지 화면에 있으므로,
#   여기를 늘리면 그만큼 읽는 시간이 늘어난다.
HOLD_BEATS_BY_CUT = {2: 5.0}

# 조각 시작은 «박의 1/N» 격자에 올린다 (오프닝은 3분할을 썼다)
SUBDIV = 2


# ═══════════════════════════════════════════════════════════════════════
#  격자 실측
# ═══════════════════════════════════════════════════════════════════════
def to_wav(path):
    tmp = os.path.join(tempfile.gettempdir(), 'ending_bgm.wav')
    subprocess.run([FFMPEG, '-y', '-v', 'error', '-i', path,
                    '-ac', '1', '-ar', '22050', tmp], check=True)
    return tmp


def onset_envelope(samples, sr, hop=256, win=1024):
    """스펙트럴 플럭스 — 소리가 «새로 나기 시작한» 정도를 시간에 대해 뽑는다."""
    n = 1 + (len(samples) - win) // hop
    window = np.hanning(win)
    prev = None
    env = np.zeros(n, dtype=np.float64)
    for i in range(n):
        frame = samples[i * hop: i * hop + win] * window
        mag = np.abs(np.fft.rfft(frame))
        if prev is not None:
            diff = mag - prev
            env[i] = float(np.sum(diff[diff > 0]))     # 커진 성분만 센다
        prev = mag
    env -= env.mean()
    env[env < 0] = 0
    return env, sr / hop


def measure_grid(path):
    """BPM 과 첫 박의 위치를 실측한다.

    ⚠ «가장 센 주기» 를 그냥 고르면 <b>박의 배수·약수</b>가 뽑히는 일이 흔하다.
      그래서 50~90 BPM 으로 <b>범위를 좁혀</b> 놓고 그 안에서만 고른다 — 이 게임의
      브금들이 다 느린 시네마틱이고, 오프닝이 63.8 BPM 이었다.
    """
    sr, data = wavfile.read(to_wav(path))
    if data.ndim > 1:
        data = data.mean(axis=1)
    data = data.astype(np.float64) / (np.abs(data).max() or 1.0)

    env, fps = onset_envelope(data, sr)

    lo, hi = 50.0, 90.0
    best = None
    for bpm in np.arange(lo, hi + 0.01, 0.05):
        period = 60.0 / bpm * fps                      # 프레임 단위 박 주기
        lag = int(round(period))
        if lag < 2 or lag >= len(env) // 2:
            continue
        # 자기상관 — 박 주기만큼 밀었을 때 얼마나 겹치나
        a = env[:-lag]
        b = env[lag:]
        score = float(np.dot(a, b) / (np.linalg.norm(a) * np.linalg.norm(b) + 1e-9))
        if best is None or score > best[1]:
            best = (bpm, score, period)

    bpm, score, period = best

    # 위상 — 박 격자를 어디서 시작하면 온셋이 가장 많이 얹히나
    phase_best = None
    for off in np.arange(0.0, period, 0.25):
        idx = np.arange(off, len(env) - 1, period).astype(int)
        s = float(env[idx].sum())
        if phase_best is None or s > phase_best[1]:
            phase_best = (off, s)
    first_beat = phase_best[0] / fps

    beat = 60.0 / bpm
    # 격자 선명도 — 박 위의 평균 세기 / 전체 평균 (1.0 이면 «격자가 무의미»)
    idx = np.arange(phase_best[0], len(env) - 1, period).astype(int)
    clarity = float(env[idx].mean() / (env.mean() + 1e-9))
    return bpm, beat, first_beat, score, clarity


def duration(path):
    out = subprocess.run([FFPROBE, '-v', 'error', '-show_entries', 'format=duration',
                          '-of', 'csv=p=0', path],
                         capture_output=True, text=True).stdout.strip()
    return float(out) if out else 0.0


# ═══════════════════════════════════════════════════════════════════════
#  시각표
# ═══════════════════════════════════════════════════════════════════════
def snap_up(t, first_beat, beat, subdiv=1):
    """t 이상인 가장 가까운 격자점으로 <b>올려</b> 붙인다."""
    step = beat / subdiv
    k = np.ceil((t - first_beat) / step - 1e-9)
    return first_beat + max(0.0, k) * step


def main():
    bpm, beat, first_beat, score, clarity = measure_grid(BGM)
    bgm_len = duration(BGM)

    print('브금  %s' % os.path.basename(BGM))
    print('  길이 %.2f초 · %.2f BPM · 박 %.4f초 · 첫 박 %.3f초' % (bgm_len, bpm, beat, first_beat))
    print('  자기상관 %.3f · 격자 선명도 %.2f  (1.0 이면 격자가 무의미)' % (score, clarity))
    print()

    lens = {}
    for cut, frag, _ in SCRIPT:
        p = os.path.join(VO_DIR, 'VO_%02d_%d.mp3' % (cut, frag))
        lens[(cut, frag)] = duration(p)

    fade_in = FADE_IN_BEATS * beat
    fade_out = FADE_OUT_BEATS * beat
    lead = LEAD_BEATS * beat
    hold = HOLD_BEATS * beat
    hold_last = HOLD_LAST_BEATS * beat

    cuts = {}
    for cut, frag, _ in SCRIPT:
        cuts.setdefault(cut, []).append(frag)

    # 컷 1 은 첫 박에서 INTRO_BEATS 만큼 뒤에 시작한다
    t = first_beat + INTRO_BEATS * beat
    out = []
    for cut in sorted(cuts):
        cut_start = snap_up(t, first_beat, beat)          # 컷 전환은 <b>박</b> 위
        clock = cut_start + fade_in + lead
        rows = []
        frags = cuts[cut]
        for i, frag in enumerate(frags):
            start = snap_up(clock, first_beat, beat, SUBDIV)   # 조각은 박의 1/2 위
            dur = lens[(cut, frag)]
            end = start + dur
            rows.append((frag, start, dur))
            punct = dict(((c, f), p) for c, f, p in SCRIPT)[(cut, frag)]
            gap = (GAP_SENTENCE_BEATS if punct == '.' else GAP_CLAUSE_BEATS) * beat
            clock = end + gap
        last_end = rows[-1][1] + rows[-1][2]
        is_last = cut == max(cuts)
        cut_hold = (hold_last if is_last
                    else HOLD_BEATS_BY_CUT.get(cut, HOLD_BEATS) * beat)
        cut_end = last_end + cut_hold
        out.append((cut, cut_start, rows, cut_end))
        t = cut_end + fade_out

    total = out[-1][3] + fade_out
    print('연출 규칙 (박 단위 → 초)')
    print('  페이드인 %.2f · 리드 %.2f · 문장텀 %.2f · 절텀 %.2f · 머묾 %.2f · 페이드아웃 %.2f · 마지막머묾 %.2f'
          % (fade_in, lead, GAP_SENTENCE_BEATS * beat, GAP_CLAUSE_BEATS * beat,
             hold, fade_out, hold_last))
    print()

    for cut, cut_start, rows, cut_end in out:
        print('컷 %d   atMusicTime = %.2f      (끝 %.2f)' % (cut, cut_start, cut_end))
        for frag, start, dur in rows:
            print('    %d-%d   atMusicTime = %-7.2f  (%.2f초, 끝 %.2f)'
                  % (cut, frag, start, dur, start + dur))
        print()

    print('마지막 컷이 검게 지고 끝나는 시각 %.2f초 / 브금 %.2f초 — %s'
          % (total, bgm_len,
             ('여유 %.2f초' % (bgm_len - total)) if total <= bgm_len
             else ('★ 브금을 %.2f초 넘긴다' % (total - bgm_len))))

    print()
    print('─── EndingDirector.cs 에 옮길 값 ───')
    for cut, cut_start, rows, cut_end in out:
        print('컷%d %.2ff : %s' % (cut, cut_start,
                                   ' · '.join('%.2ff' % s for _, s, _ in rows)))


if __name__ == '__main__':
    main()
