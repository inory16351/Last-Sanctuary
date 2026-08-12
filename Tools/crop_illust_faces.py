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
import os
from PIL import Image

SRC = r'C:\Project\Last-Sanctuary-Vault\리소스\illust\char'
DST = r'C:\Project\Last Sanctuary\Assets\_Project\Resources\Illust'

# 초상화 Sprite 칸: Portrait(226x200) 에 -16 인셋 → 210x184
TARGET_W, TARGET_H = 210, 184
ASPECT = TARGET_W / float(TARGET_H)      # 1.1413

# 출력 해상도 — 표시 크기의 2배. 원본이 작은 비기오르가 과하게 뭉개지지 않는 선.
OUT_W, OUT_H = 420, 368

# (원본 파일명, 출력 이름, 얼굴 중심 x, 얼굴 중심 y, 크롭 높이)
# 좌표는 원본 픽셀 기준. 격자를 씌워 눈으로 찍은 값이다.
FACES = [
    ('illust_Elin.png',   'illust_Elin',   800,  415, 520),   # 1402x1122 — 가시관 쓴 얼굴
    ('illust_Bigior.png', 'illust_Bigior', 147,   70, 115),   # 298x453  — 투구 얼굴
    ('ilust_Preyja.png',  'illust_Preyja', 515,  210, 250),   # 1024x1536 — 후광 아래 얼굴
]


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


def main():
    os.makedirs(DST, exist_ok=True)
    for src_name, out_name, cx, cy, crop_h in FACES:
        path = os.path.join(SRC, src_name)
        if not os.path.exists(path):
            print('MISSING', path)
            continue

        im = Image.open(path).convert('RGB')
        cropped, box = crop_face(im, cx, cy, crop_h)
        out = cropped.resize((OUT_W, OUT_H), Image.LANCZOS)
        out.save(os.path.join(DST, out_name + '.png'))
        print('%-16s %s -> crop %s @%s  = %.4f' %
              (out_name, im.size, (box[2], box[3]), (box[0], box[1]),
               box[2] / float(box[3])))
    print('목표 비율 %.4f / 출력 %dx%d' % (ASPECT, OUT_W, OUT_H))


if __name__ == '__main__':
    main()
