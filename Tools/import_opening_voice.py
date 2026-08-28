# -*- coding: utf-8 -*-
"""유저가 <b>문장별로 나눠 준</b> 오프닝 내레이션을 프로젝트로 들여오고, 시각표를 계산한다
(2026-08-24 · 139절의 `split_opening_voice.py` 를 <b>대체</b>한다).

유저 지시: *"오프닝 목소리 수정한거 반영 좀 문장별로 다 분리해서 파일 만들어 놨으니까
이거 바탕으로 다시 배치해줘"*

★★ 왜 «자르는 스크립트» 를 버리고 «들여오는 스크립트» 로 바꿨나
----------------------------------------------------------------
139절에서는 통짜 음성 4개를 <b>내가 소리로 분석해</b> 11조각으로 잘랐다(경계를 찾느라 방법을
세 번 틀렸다). 이제 유저가 <b>직접 나눈 16개</b>를 주었으므로 그 추측은 필요 없다 —
경계는 «추정» 이 아니라 «주어진 것» 이다.

⚠ 그래서 `split_opening_voice.py` 는 <b>지웠다</b>. 남겨 두면 누군가 그것을 돌려
  유저가 준 파일을 <b>자동으로 자른 것으로 덮어쓴다</b>.

★ <b>다시 인코딩하지 않고 그대로 복사한다</b> — mp3 를 풀어 다시 굽으면 음질만 깎인다.
  앞뒤 묵음도 <b>다듬지 않는다</b>(139절에서는 다듬었다): 유저가 내보낸 파일이 정본이고,
  다듬기의 «말소리 문턱» 은 조용히 끝나는 낱말을 잘라먹을 수 있다(실제로 02-04 의 끝
  0.78초가 묵음인지 여린 말소리인지 소리로는 가릴 수 없었다).

★★ <b>텀을 두 단계로 둔다</b> — 유저의 16조각은 «문장» 이 아니라 «절(clause)» 단위다
-------------------------------------------------------------------------------------
16조각을 전부 같은 텀(0.7초)으로 이으면 <b>한 문장 안에서도 0.7초씩 끊긴다</b> — 낭독이
아니라 «단어 나열» 로 들리고, 오프닝이 브금(119.65초)보다 길어진다. 그래서 텀을 둘로 나눴다.

  GAP_SENTENCE  문장이 끝난 뒤    — 숨을 쉬는 자리
  GAP_CLAUSE    문장 안에서 끊길 때 — 이어 읽는 자리

<b>어느 쪽인지는 앞 자막의 «끝 글자» 가 정한다</b> — 마침표로 끝나면 문장 텀, 쉼표나
줄표(—)로 끝나면 절 텀이다. 대본을 눈으로 읽으면 어느 텀이 붙는지 바로 알 수 있고,
따로 관리하는 표가 없으니 어긋날 일도 없다.

사용법:  python Tools/import_opening_voice.py
⚠ 그 뒤 Unity 에서 Assets/Refresh 를 실행할 것.
"""

import hashlib
import math
import os
import re
import shutil
import subprocess
import sys
import tempfile

import numpy as np
from scipy.io import wavfile

from vault_path import VAULT, PROJECT

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

SRC = os.path.join(VAULT, "리소스", "voice")
DST = os.path.join(PROJECT, "Assets", "_Project", "Resources", "Opening")

FFMPEG = shutil.which("ffmpeg") or (
    r"C:\Users\user\AppData\Local\Microsoft\WinGet\Packages"
    r"\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe"
    r"\ffmpeg-8.1.1-full_build\bin\ffmpeg.exe")

# ── 연출 규칙 (초) ────────────────────────────────────────────────────────
FADE_IN = 1.0        # 컷이 밝아지는 시간 (OpeningDirector.fadeInSeconds 와 같아야 한다)
BEAT = 0.2           # 밝아진 뒤 첫 조각이 말을 시작하기까지의 한 박
GAP_SENTENCE = 0.35  # ★ 문장이 끝난 뒤의 <b>최소</b> 텀 (격자에 맞추며 늘어난다)
GAP_CLAUSE = 0.15    # ★ 문장 안에서 끊길 때의 <b>최소</b> 텀
HOLD = 0.6           # 컷의 말이 끝난 뒤 머무는 시간 (그 뒤 FADE_OUT 만큼 검게 진다)
FADE_OUT = 0.6       # OpeningDirector.fadeOutSeconds 와 같아야 한다
HOLD_LAST = 1.5      # 마지막 컷이 머무는 시간 (slides 의 holdAfterLastCaption)

BGM_SECONDS = 119.65 # 브금 The Fall of the Sanctuary 의 길이

# ── ★★ 노래의 박자 격자 (2026-08-24 실측 · scratchpad/bgm_beats.py · bgm_meter.py) ──
#
# 유저 지시: *"텀은 노래 타이밍에 맞춰서 전환 해주면 베스트 불가능하면 현재로 유지"*
#
# 실측 —  63.80 BPM · 박 주기 0.9404초 · 첫 박 0.650초
#         격자 선명도 1.94 (1.0 이면 «격자가 무의미» · 1.25 넘으면 «박이 뚜렷하다»)
#         4/4 (다운비트 대비 2.30 — 3박자는 1.16 으로 탈락) · 마디 3.7616초 · 첫 마디 1.590초
#         박의 3분할(191.4 BPM = 63.8 x 3)이 뚜렷하다 → 셋잇단 느낌. 그래서 잘게 맞출 때는
#         박을 <b>3으로</b> 나눈다(0.3135초) — 2분할보다 이 노래에 맞는다.
BEAT_SECONDS = 0.9404
BEAT_ZERO = 0.650
BEATS_PER_BAR = 4
BAR_SECONDS = BEAT_SECONDS * BEATS_PER_BAR      # 3.7616
BAR_ZERO = 1.590
SUBDIV = 3                                      # 박을 셋으로 — 0.3135초
SUB_SECONDS = BEAT_SECONDS / SUBDIV

#: 첫 컷이 시작하는 시각. 격자에 맞출 때는 첫 마디(1.590)에서 시작한다.
SLIDE_START_FREE = 2.0

#: 컷마다 «조각 파일 이름 → 그 조각에 띄울 한글 자막».
#:  ⚠ 자막의 <b>끝 글자</b>가 다음 텀을 정한다 (마침표=문장 텀 · 쉼표·줄표=절 텀).
#:  ★★ 2026-08-28 — 컷을 넷에서 여덟으로 늘렸다. 항목이 셋이 됐다 —
#:     (볼트의 조각 이름, <b>Resources 에 넣을 이름</b>, 자막).
#:     ⚠ 넣을 이름을 «컷_순번» 으로 지어 내면 <b>컷을 다시 묶을 때마다 파일이 전부 갈린다</b>.
#:        이름은 처음 녹음된 순서 그대로 박아 두고 컷 번호만 바꾼다.
#:     컷 번호 = 배경 그림 번호(BG_0N) = 이야기 순서.
SCRIPT = {
    1: ("Opening/BG_01", [
        ("01-01", "VO_01_1", "기억한다 — 이 성역이 순백으로 빛나던 시절을."),
        ("01-02", "VO_01_2", "천사들의 노래가 첨탑마다 울려 퍼졌고,"),
    ]),
    2: ("Opening/BG_02", [
        ("01-03", "VO_01_3", "그 어떤 어둠도 이 문턱을 넘지 못했다."),
    ]),
    3: ("Opening/BG_03", [
        ("02-01", "VO_02_1", "그 빛은 꺼졌다."),
        ("02-02", "VO_02_2", "하늘은 순식간에 핏빛으로 물들었고,"),
        ("02-03", "VO_02_3", "노도와 같은 어둠은 그들을 집어삼켰다."),
    ]),
    4: ("Opening/BG_04", [
        ("02-04", "VO_02_4", "터전을 지키는 데에,"),
        ("02-05", "VO_02_5", "그 이유는 중요치 않으리 — 그들은 영문도 모른 채,"),
        ("02-06", "VO_02_6", "갑주를 여미고, 짙어지는 어둠을 향해 나아갈 뿐이었다."),
    ]),
    5: ("Opening/BG_05", [
        ("03-01", "VO_03_1", "성문은 무너졌고, 하늘에서는 짐승이 울부짖는다."),
        ("03-02", "VO_03_2", "불길은 자비를 모르고, 어둠은 뿌리처럼 번져간다."),
    ]),
    6: ("Opening/BG_06", [
        ("03-03", "VO_03_3", "남은 것은 잿더미와, 지켜지지 못한 맹세뿐."),
    ]),
    7: ("Opening/BG_07", [
        ("04-01", "VO_04_1", "쓰러진 이들의 이름을 나는 다 기억하지 못한다."),
        ("04-02", "VO_04_2", "그러나 그들이 지키려 했던 것만은 잊지 않았다."),
    ]),
    8: ("Opening/BG_08", [
        ("04-03", "VO_04_3", "그대여, 마지막 성역이 완전히 저물기 전에 —"),
        ("04-04", "VO_04_4", "나서라."),
    ]),
}

META = """fileFormatVersion: 2
guid: {guid}
AudioImporterSettings:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 1
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 1
  preloadAudioData: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def guid_for(rel):
    return hashlib.md5(("LastSanctuary/" + rel).encode("utf-8")).hexdigest()


def duration_and_edges(path):
    """길이(초)와 앞뒤 묵음(초). 재기만 하고 파일은 건드리지 않는다."""
    with tempfile.TemporaryDirectory() as d:
        wav = os.path.join(d, "a.wav")
        subprocess.run([FFMPEG, "-v", "error", "-y", "-i", path, "-ac", "1",
                        "-ar", "44100", "-f", "wav", wav], check=True)
        sr, x = wavfile.read(wav)
    x = x.astype(np.float64)
    if x.ndim > 1:
        x = x.mean(axis=1)
    dur = len(x) / sr

    hop = int(0.010 * sr)
    frames = max(1, len(x) // hop)
    rms = np.array([np.sqrt(np.mean(x[i * hop:(i + 1) * hop] ** 2) + 1e-16)
                    for i in range(frames)])
    on = np.where(rms > rms.max() * (10 ** (-32 / 20)))[0]
    if len(on) == 0:
        return dur, 0.0, 0.0
    return dur, on[0] * 0.010, (frames - 1 - on[-1]) * 0.010


def gap_after(caption):
    """앞 자막의 끝 글자로 다음 텀을 정한다 — 위 «텀을 두 단계로 둔다» 참조."""
    tail = caption.rstrip()
    if tail.endswith((",", "—", "-", "…")):
        return GAP_CLAUSE, "절"
    return GAP_SENTENCE, "문장"


def snap_up(t, step, zero):
    """<paramref name="t"/> 이후(같으면 그대로)의 첫 격자점."""
    k = math.ceil((t - zero) / step - 1e-9)
    return zero + k * step


def plan(cut_grid_step, cut_grid_zero, sub_step, sub_zero, first_slide, lengths):
    """
    시각표를 짠다. 컷 시작은 <paramref name="cut_grid_step"/> 격자에, 조각 시작은
    <paramref name="sub_step"/> 격자에 <b>올려서</b> 맞춘다(내리면 텀이 최소보다 짧아진다).

    돌려주는 것: (컷별 계획, 마지막 말이 끝나는 시각, 막이 다 내려간 시각)
    """
    out = []
    slide = first_slide
    voice_end = slide
    for cut in sorted(lengths):
        items = lengths[cut]
        last_cut = cut == max(lengths)

        t = slide + FADE_IN + BEAT
        if sub_step:
            t = snap_up(t, sub_step, sub_zero)

        rows = []
        for i, (name, dur, caption) in enumerate(items):
            voice_end = t + dur
            rows.append((name, t, dur, caption))
            if i < len(items) - 1:
                g, kind = gap_after(caption)
                nxt = voice_end + g
                if sub_step:
                    nxt = snap_up(nxt, sub_step, sub_zero)
                t = nxt

        hold = HOLD_LAST if last_cut else HOLD
        out.append((cut, slide, rows, hold))

        if not last_cut:
            nxt = voice_end + hold + FADE_OUT
            slide = snap_up(nxt, cut_grid_step, cut_grid_zero) if cut_grid_step else nxt

    return out, voice_end, voice_end + HOLD_LAST + FADE_OUT


def main():
    #  ★ 볼트가 없으면 <b>복사를 건너뛰고 시각표만</b> 뽑는다 — 음성은 이미
    #    Resources 에 들어가 있고, 컷을 다시 묶을 때 필요한 것은 «길이» 뿐이다.
    schedule_only = not os.path.isdir(SRC)
    if schedule_only:
        print("⚠ 볼트의 음성 폴더가 없다:", SRC)
        print("  → 복사는 건너뛰고 <시각표만> 뽑는다 (길이는 Resources 의 파일에서 잰다)")
        print()
    else:
        missing = [n for _, items in SCRIPT.values() for n, _, _ in items
                   if not os.path.isfile(os.path.join(SRC, n + ".mp3"))]
        if missing:
            print("⚠ 볼트에 없는 조각:", missing)
            return 1

    # ② 예전 조각을 전부 치운다  (볼트가 있을 때만)
    if schedule_only:
        lengths = {}
        for cut in sorted(SCRIPT):
            _, items = SCRIPT[cut]
            lengths[cut] = []
            for _, name, caption in items:
                dur, _h, _t = duration_and_edges(os.path.join(DST, name + ".mp3"))
                lengths[cut].append((name, dur, caption))
            print("컷 %d  %s" % (cut, " · ".join("%s %.2f초" % (n, d) for n, d, _ in lengths[cut])))
        print()
        return report(lengths)

    #    예전 조각을 치운다 — 이름이 겹치지 않는 것이 남지 않도록
    removed = 0
    for f in sorted(os.listdir(DST)):
        if re.fullmatch(r"VO_\d\d(_\d)?\.mp3(\.meta)?", f):
            os.remove(os.path.join(DST, f))
            removed += 1
    if removed:
        print(f"예전 조각 {removed}개 치움\n")

    # ③ 복사 + 재기
    lengths = {}
    for cut in sorted(SCRIPT):
        _, items = SCRIPT[cut]
        lengths[cut] = []
        print(f"컷 {cut}")
        for src_name, name, caption in items:
            src = os.path.join(SRC, src_name + ".mp3")
            dst = os.path.join(DST, name + ".mp3")
            shutil.copy2(src, dst)

            rel = os.path.relpath(dst, os.path.join(PROJECT, "Assets", "_Project")).replace("\\", "/")
            with open(dst + ".meta", "w", encoding="utf-8", newline="\n") as f:
                f.write(META.format(guid=guid_for(rel)))

            dur, head, tail = duration_and_edges(src)
            lengths[cut].append((name, dur, caption))
            note = f"  ⚠ 끝 묵음 {tail:.2f}초" if tail > 0.4 else ""
            print(f"   {src_name} → {name}  {dur:5.2f}초  "
                  f"(앞 {head:.2f} · 뒤 {tail:.2f}){note}")
        print()

    return report(lengths)


def report(lengths):
    """시각표를 짜서 찍는다 (복사 여부와 무관하게 같은 규칙을 쓴다)."""
    # ④ ★★ 어느 격자에 맞출 수 있나 — 「센 것부터」 시험한다
    print("=" * 78)
    print("[격자 맞추기]  노래에 맞출 수 있는 가장 «센» 격자를 고른다")
    print(f"   63.80 BPM · 박 {BEAT_SECONDS}초 · 마디 {BAR_SECONDS:.4f}초 · "
          f"박의 1/{SUBDIV} = {SUB_SECONDS:.4f}초")
    print("=" * 78)

    ATTEMPTS = [
        ("① 컷=2마디 프레이즈 · 조각=박",
         BAR_SECONDS * 2, BAR_ZERO, BEAT_SECONDS, BEAT_ZERO, BAR_ZERO),
        ("② 컷=마디 · 조각=박",
         BAR_SECONDS, BAR_ZERO, BEAT_SECONDS, BEAT_ZERO, BAR_ZERO),
        ("③ 컷=마디 · 조각=박의 1/3",
         BAR_SECONDS, BAR_ZERO, SUB_SECONDS, BEAT_ZERO, BAR_ZERO),
        ("③b 컷=반마디(2박) · 조각=박의 1/3",
         BEAT_SECONDS * 2, BAR_ZERO, SUB_SECONDS, BEAT_ZERO, BAR_ZERO),
        ("④ 컷=박 · 조각=박의 1/3",
         BEAT_SECONDS, BEAT_ZERO, SUB_SECONDS, BEAT_ZERO, BAR_ZERO),
        ("④b 컷=박의 1/3 · 조각=박의 1/3",
         SUB_SECONDS, BEAT_ZERO, SUB_SECONDS, BEAT_ZERO, BAR_ZERO),
        ("⑤ 맞추지 않는다 (지금까지의 방식)",
         0, 0, 0, 0, SLIDE_START_FREE),
    ]

    chosen = None
    for label, cs, cz, ss, sz, first in ATTEMPTS:
        cuts, end, total = plan(cs, cz, ss, sz, first, lengths)
        ok = total <= BGM_SECONDS
        mark = "✓" if ok else "✗"
        print(f"   {mark} {label:32}  막 내려감 {total:7.2f}초  "
              f"({'브금 안 · %.2f초 남음' % (BGM_SECONDS - total) if ok else '브금보다 %.2f초 길다' % (total - BGM_SECONDS)})")
        if ok and chosen is None:
            chosen = (label, cuts, end, total)

    if chosen is None:
        print("\n⚠ 어느 격자로도 브금 안에 들어오지 않는다 — 맞추지 않은 배치를 쓴다")
        cuts, end, total = plan(0, 0, 0, 0, SLIDE_START_FREE, lengths)
        chosen = ("맞추지 않음", cuts, end, total)

    label, cuts, end, total = chosen
    print(f"\n[고른 것] {label}")

    # ⑤ 고른 계획을 찍는다
    print("=" * 78)
    for cut, slide, rows, hold in cuts:
        bg, _ = SCRIPT[cut]
        bar_off = (slide - BAR_ZERO) / BAR_SECONDS
        tag = f"마디 {bar_off:.0f}" if abs(bar_off - round(bar_off)) < 0.02 else \
              f"마디에서 {(bar_off - round(bar_off)) * BAR_SECONDS:+.2f}초"
        print(f"\n  컷 {cut}  slide.atMusicTime = {slide:6.2f}f   ({bg} · {tag})"
              + ("   holdAfterLastCaption = %.1ff" % hold if cut == max(SCRIPT) else ""))
        prev_end = None
        for name, t, dur, caption in rows:
            if prev_end is not None:
                _, kind = gap_after(prev_caption)
                print(f"              ↕ {kind} 텀 {t - prev_end:.2f}초")
            cps = min(max(len(caption) / max(0.25, dur - 0.8), 6.0), 90.0)
            print(f"     {name}  atMusicTime = {t:6.2f}f  음성 {dur:5.2f} → {t + dur:6.2f}   "
                  f"자막 {len(caption):2d}자 ({cps:4.1f}자/초)  \"{caption}\"")
            prev_end, prev_caption = t + dur, caption

    print("\n" + "=" * 78)
    print(f"마지막 말 끝  {end:6.2f}초 · 막이 다 내려간 시각 {total:6.2f}초 · 브금 {BGM_SECONDS}초")
    if total <= BGM_SECONDS:
        print(f"✓ 브금 안에 들어온다 ({BGM_SECONDS - total:.2f}초 남음)")
    else:
        print(f"⚠ 브금보다 {total - BGM_SECONDS:.2f}초 길다 "
              "(멈추지는 않는다 — _clock 은 브금이 끝나도 계속 흐른다)")

    # ⑥ ★ C# 대본 표를 <b>스크립트가 직접</b> 뽑는다 — 손으로 옮겨 적다 틀리지 않게.
    if "--cs" in sys.argv:
        out = [f'        [Header("대본 (배경 · 조각 = 자막 + 음성 + 시각)")]',
               f'        [SerializeField] Slide[] slides =',
               f'        {{']
        for cut, slide, rows, hold in cuts:
            bg, _ = SCRIPT[cut]
            out += [f'            new Slide',
                    f'            {{',
                    f'                background  = "{bg}",',
                    f'                atMusicTime = {slide:.2f}f,']
            if cut == max(SCRIPT):
                out.append(f'                holdAfterLastCaption = {hold:.1f}f,')
            out += [f'                captions = new[]', f'                {{']
            for name, t, dur, caption in rows:
                out += [f'                    new Caption',
                        f'                    {{',
                        f'                        voice       = "Opening/{name}",     // {dur:.2f}초',
                        f'                        atMusicTime = {t:.2f}f,',
                        f'                        text = "{caption}",',
                        f'                    }},']
            out += [f'                }},', f'            }},']
        out.append('        };')
        path = os.path.join(PROJECT, "Tools", "_opening_slides.cs.txt")
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(out) + "\n")
        print(f"\n★ C# 대본 표를 뽑았다 → {path}")
        print("  OpeningDirector.cs 의 slides 표를 이것으로 갈아끼울 것.")

    print("\n⚠ Unity 에서 Assets/Refresh 를 실행할 것.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
