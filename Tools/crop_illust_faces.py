# -*- coding: utf-8 -*-
"""캐릭터 일러스트를 얼굴 중심으로 잘라 초상화 프레임 비율에 맞춘다.

원본 3장이 비율이 제각각(1402x1122 / 298x453 / 1024x1536)이라, 그대로 넣으면
PreserveAspect 때문에 어떤 건 위아래가, 어떤 건 좌우가 남아 크기가 들쭉날쭉하다.
전부 같은 비율로 잘라 두면 초상화 칸을 정확히 꽉 채운다(유저 요청 2026-08-11).

전신샷이어도 **얼굴만** 잘라낸다 — 초상화 칸이 210x184 라 전신을 넣으면 얼굴이 몇 픽셀 안 된다.

⚠ 원본은 `리소스/illust/char/` 에 그대로 있다. 이 스크립트는 거기서 읽어
Unity 의 Resources 로 쓰므로, 몇 번을 돌려도 결과가 같고 원본이 상하지 않는다(멱등).
크롭 위치를 바꾸려면 FACES 의 숫자만 고쳐 다시 돌리면 된다.
"""
import hashlib
import os
from PIL import Image

from vault_path import VAULT, PROJECT

# ★★ 2026-08-16 — <b>하드코딩을 걷어냈다</b> (미결 193번).
#
#   이 상수는 <b>세 번 틀렸다.</b>
#     · 옛 프로젝트 경로 (67-4절에서 발견)
#     · 고친 값 `C:\Project\Last-Sanctuary\...` 도 틀렸다 — 하이픈이 붙은 것은 **볼트**이고
#       유니티 프로젝트는 공백(`Last Sanctuary`)이었다
#     · 그리고 그 둘 다 **이 PC 에는 아예 없는 경로**다(볼트가 `H:\c팀\...` 에 있다).
#       그대로 돌리면 `os.makedirs` 가 엉뚱한 새 폴더를 만들고 진짜 에셋은 하나도 안 바뀐 채
#       "완료" 만 찍힌다 — `vault_path.py` 가 생긴 이유가 정확히 이것이다.
#
#   ⚠ 앞으로 이 파일들에 경로를 <b>직접 적지 말 것</b>. `vault_path` 를 쓴다.
SRC = os.path.join(VAULT, '리소스', 'illust', 'char')
DST = os.path.join(PROJECT, 'Assets', '_Project', 'Resources', 'Illust')

# 초상화 Sprite 칸: Portrait(226x300) 에 -16 인셋 → 210x284
#
# ⚠ 2026-08-14 에 <b>가로형 → 세로형</b>으로 바뀌었다(유저 지시: "일러스트 공간이 너무 작아서
#   별로인듯. 공간 크기 좀 더 키워서 이미지가 좀더 넓은 범위로 들어가게").
#   예전 210x184(가로형 1.14)는 얼굴만 겨우 담겼다 — 원화가 전부 세로 전신샷이라
#   가로로 자르면 <b>머리 위아래만</b> 남는다. 세로형으로 바꾸고 크롭 높이를 1.7~1.8배로
#   키워 <b>머리 + 상반신</b>이 들어가게 했다.
#   Info 컬럼 폭이 250 이라 가로는 226 이 상한이다(옆 컬럼들이 꽉 차 있다 — 48절 미결 64번).
#   그래서 "키운다"는 곧 "세로로 키운다"였다.
TARGET_W, TARGET_H = 210, 284
ASPECT = TARGET_W / float(TARGET_H)      # 0.7394

# 출력 해상도 — 표시 크기의 2배. 원본이 작은 비기오르가 과하게 뭉개지지 않는 선.
OUT_W, OUT_H = 420, 568

# (원본 파일명, 출력 이름, 크롭 중심 x, 크롭 중심 y, 크롭 높이)
# 좌표는 원본 픽셀 기준. 격자를 씌워 눈으로 찍은 값이다.
#
# ⚠ 2026-08-14 세로형 전환에 맞춰 <b>네 줄을 전부 다시 찍었다.</b> 예전 값은 "얼굴 중심"이었지만
#   지금은 <b>머리가 위쪽 1/3 에 오고 상반신이 들어오는</b> 구도라, 중심 y 가 얼굴보다 아래에 있다.
#   비율(ASPECT)을 바꾸면 이 표도 같이 다시 찍어야 한다 — 높이만 바꾸면 가로가 따라 좁아진다.
#
# ★ 값을 고른 기준 (2026-08-14 2차 — 유저: "지금 비율로 캐릭터 일러스트 더 자연스럽게"):
#   ① <b>머리 위 여백</b> — 1차 값은 투구·모자가 프레임 위쪽에 딱 붙거나 잘렸다.
#      머리 꼭대기 위로 크롭 높이의 5~8% 를 남긴다. (엘린만 예외 — 원본 자체가 정수리가
#      잘린 클로즈업이라 위쪽 여백을 만들 수가 없다. 그래서 crop top 을 0 으로 붙였다.)
#   ② <b>가로 중심</b> — 크롭 정중앙에 세로선을 그어 얼굴이 그 선에 걸리도록 cx 를 맞췄다.
#      1차 값은 비기오르가 왼쪽, 피올로가 오른쪽으로 치우쳐 있었다.
#   ③ <b>눈높이</b> — 네 명 모두 얼굴이 위쪽 1/3 선 근처에 오게 맞췄다(인물 사진의 통상 구도).
FACES = [
    ('illust_Elin.png',   'illust_Elin',   780,  500, 1000),  # 1402x1122 — 가시관 + 어깨·가슴
    # ⚠ 비기오르는 2026-08-14 에 원본이 통째로 교체됐다: 298x453 → **1254x1254**.
    #   옛 좌표(147, 70, 115)를 그대로 두면 새 그림의 <b>왼쪽 위 구석</b>을 잘라낸다 —
    #   원본을 갈아끼우면 이 줄도 반드시 다시 찍어야 한다.
    ('illust_Bigior.png', 'illust_Bigior', 678,  395, 640),   # 1254x1254 — 후광 + 투구 + 흉갑
    ('illust_Preyja.png', 'illust_Preyja', 515,  290, 580),   # 1024x1536 — 가시 후광 전체 + 상반신
    ('illust_Piolo.png',  'illust_Piolo',  570,  588, 625),   # 1024x1535 — 실크햇 + 어깨·망토
    # 2026-08-14 신규 캐릭터. 원본이 세로 전신샷이라 다른 넷과 같은 규칙으로 잡았다.
    ('illust_Histon.png', 'illust_Histon', 610,  400, 780),   # 1109x1418 — 가시관 + 흉갑
    # 2026-08-20 신규 캐릭터 시그리드. 원본이 프레이야와 같은 1024x1536 전신샷이라
    # 그쪽과 같은 크롭 높이(580)를 썼다. cy 는 가시관 꼭대기(원본 y≈150) 위로
    # 크롭 높이의 6% 를 남기도록 잡은 값이다 — 얼굴(y≈280)이 크롭 위쪽 28% 에 온다.
    # ⚠⚠ 2026-08-20 — <b>볼트의 파일 이름이 바뀌었다</b>: `Sigrid_illust.png` →
    #   `illust_Sigrid.png`(다른 넷과 같은 규칙으로 유저가 맞췄다). 옛 이름을 그대로 두면
    #   이 스크립트가 <b>`MISSING` 한 줄만 찍고 조용히 건너뛴다</b> — 크롭이 갱신되지 않아
    #   볼트의 새 원화가 게임에 안 들어간다. 실제로 그 상태였다.
    #   ★ 그래서 아래 `resolve_src` 가 <b>두 이름을 다 받는다</b> — 이름 규칙이 또 흔들려도
    #     죽지 않고, 없으면 확실하게 죽는다(조용히 건너뛰지 않는다).
    ('illust_Sigrid.png', 'illust_Sigrid', 500,  405, 580),   # 1024x1536 — 가시관 + 지팡이 머리
    # 2026-08-20 신규 캐릭터 3인. 같은 세 기준(머리 위 여백 5~8% · 가로 중심 · 눈높이 1/3)으로 잡았다.
    # ⚠ 크롭 높이가 셋 다 다르다 — 원화의 <b>인물 크기</b>가 다르기 때문이다. 시카리아는 무릎까지
    #   오는 중경, 아루는 전신, 카이론은 <b>올려다보는 구도</b>라 얼굴이 화면 위쪽에 이미 있다.
    ('illust_Sicaria.png', 'illust_Sicaria', 512,  525, 760),  # 1024x1536 — 후드 + 활 잡은 두 손
    ('illust_Aru.png',     'illust_Aru',     530,  410, 590),  # 1023x1537 — 가시 후광 + 랜턴
    ('illust_Chiron.png',  'illust_Chiron',  585,  295, 500),  # 1122x1402 — 일식 + 상반신
    # 2026-08-20 신규 캐릭터 2인.
    ('illust_Arsenia.png', 'illust_Arsenia', 545,  383, 654),  # 1024x1536 — 후드 + 붉은 포션
    ('illust_Vulcan.png',  'illust_Vulcan',  545,  455, 540),  # 1024x1536 — 가시관 + 수염·날개
]



# ---------------------------------------------------------------------------
# ★★ .meta 를 여기서 같이 쓴다 (2026-08-20 · 미결 185번 해소)
#
# <b>왜</b> — 이 스크립트는 PNG 만 쓰고 `.meta` 는 안 만들었다. 그러면 유니티가 기본값으로
# 만들어 주는데 그 기본값이 `textureType: 0`(Default) 이고, 그 상태에서는
# `Resources.Load<Sprite>` 가 <b>null</b> 을 돌려준다. 그러면 초상화 코드의 폴백이
# <b>인게임 스프라이트</b>를 대신 얹는다 — 히스톤이 실제로 그랬다(84-8절 ②).
# 캐릭터를 넣을 때마다 같은 함정을 밟으므로 스크립트가 직접 쓴다.
#
# ⚠ <b>이미 있으면 건드리지 않는다</b> — 유저가 인스펙터에서 손본 설정을 덮으면 안 된다.
# ⚠ guid 는 경로에서 결정적으로 만든다(다른 Tools/*.py 와 같은 규칙) — 다시 돌려도
#   같은 값이라 참조가 안 끊긴다.
NL = chr(10)      # 줄바꿈 한 글자 — .meta 를 항상 LF 로 쓴다
REF_META = 'illust_Elin.png.meta'      # 본뜰 설정(textureType 8 · spriteMode 1 …)


def _guid_for(rel):
    return hashlib.md5(('LastSanctuary/' + rel).encode('utf-8')).hexdigest()


def write_meta(out_name):
    """`<이름>.png.meta` 가 없으면 기존 일러스트 것을 본떠 만든다."""
    dst = os.path.join(DST, out_name + '.png.meta')
    if os.path.exists(dst):
        return False

    ref = os.path.join(DST, REF_META)
    if not os.path.exists(ref):
        print('  ⚠ %s 가 없어 .meta 를 못 만들었습니다 — 유니티가 기본값(textureType 0)으로 '
              '만들면 초상화가 인게임 스프라이트로 뜹니다(84-8절).' % REF_META)
        return False

    guid = _guid_for('Resources/Illust/%s.png' % out_name)
    # spriteID 는 <b>고유하기만</b> 하면 된다 — 다른 에셋과 겹치면 두 스프라이트가 같은
    # id 를 갖는다(84-8절에서 실제로 밟은 함정). guid 앞 16자 + 0 으로 채운다.
    sprite_id = guid[:16] + '0' * 16

    lines = []
    with open(ref, encoding='utf-8') as f:
        for line in f:
            if line.startswith('guid: '):
                line = 'guid: %s' % guid + NL
            elif line.strip().startswith('spriteID: '):
                line = line[:line.index('spriteID:')] + 'spriteID: %s' % sprite_id + NL
            lines.append(line)
    with open(dst, 'w', encoding='utf-8', newline=NL) as f:
        f.writelines(lines)
    print('  + %s.png.meta (guid %s)' % (out_name, guid[:8]))
    return True


def crop_face(im, cx, cy, crop_h):
    """중심을 유지하며 목표 비율로 자른다. 경계를 넘으면 안쪽으로 민다."""
    crop_w = int(round(crop_h * ASPECT))
    W, H = im.size

    # 원본보다 크게 요구하면 원본에 맞춰 줄인다
    if crop_w > W:
        crop_w = W
        crop_h = int(round(crop_w / ASPECT))
    if crop_h > H:
        crop_h = H
        crop_w = int(round(crop_h * ASPECT))

    left = int(round(cx - crop_w / 2.0))
    top = int(round(cy - crop_h / 2.0))
    left = max(0, min(left, W - crop_w))     # 밖으로 나가면 안으로 민다
    top = max(0, min(top, H - crop_h))
    return im.crop((left, top, left + crop_w, top + crop_h)), (left, top, crop_w, crop_h)


def resolve_src(src_name, out_name):
    """
    원본 파일을 찾는다. **이름 규칙이 두 벌**이라 둘 다 받는다 —
    `illust_<이름>.png` (지금 규칙) 과 `<이름>_illust.png` (옛 규칙).
    오타가 섞인 파일(`ilust_Preyja.png`)이 실재하므로 표에 적힌 이름이 **먼저**다.

    ⚠ 못 찾으면 <b>None</b> 을 돌려주고, 부르는 쪽이 <b>죽인다</b>(2026-08-20).
      예전에는 `MISSING` 한 줄만 찍고 넘어갔는데, 그러면 볼트에서 원화를 갈아끼우고
      이 스크립트를 돌려도 **«완료» 만 찍히고 아무것도 안 바뀐다** — 시그리드가 정확히
      그 상태였다(볼트 파일이 프로젝트 크롭보다 새로운데 이름이 안 맞아 건너뛰었다).
    """
    stem = out_name[len('illust_'):] if out_name.startswith('illust_') else out_name
    for cand in (src_name, 'illust_%s.png' % stem, '%s_illust.png' % stem):
        path = os.path.join(SRC, cand)
        if os.path.exists(path):
            return path, cand
    return None, None


def main():
    os.makedirs(DST, exist_ok=True)
    missing = []
    for src_name, out_name, cx, cy, crop_h in FACES:
        path, found = resolve_src(src_name, out_name)
        if path is None:
            missing.append((out_name, src_name))
            continue
        if found != src_name:
            print('  ⚠ %s: 표의 이름 %r 이 없어 %r 로 찾았습니다 — 표를 고쳐 두세요.'
                  % (out_name, src_name, found))

        im = Image.open(path).convert('RGB')
        cropped, box = crop_face(im, cx, cy, crop_h)
        out = cropped.resize((OUT_W, OUT_H), Image.LANCZOS)
        out.save(os.path.join(DST, out_name + '.png'))
        write_meta(out_name)
        print('%-16s %s -> crop %s @%s  = %.4f' %
              (out_name, im.size, (box[2], box[3]), (box[0], box[1]),
               box[2] / float(box[3])))
    print('목표 비율 %.4f / 출력 %dx%d' % (ASPECT, OUT_W, OUT_H))

    # ⚠ 조용히 넘어가지 않는다 (resolve_src 주석) — 하나라도 못 찾으면 죽는다.
    if missing:
        lines = ['    %s  (표에 적힌 이름: %s)' % (o, s_) for o, s_ in missing]
        raise SystemExit('⚠ 원본을 못 찾은 캐릭터가 %d명 있습니다 — %s 아래를 확인하세요:\n%s'
                         % (len(missing), SRC, '\n'.join(lines)))


if __name__ == '__main__':
    main()
