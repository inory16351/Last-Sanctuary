# -*- coding: utf-8 -*-
"""로비 버튼 판을 <b>가로만 짧게 잘라</b> 오프닝의 «건너뛰기» 버튼 판을 만든다 (2026-08-24).

유저 지시: *"스킵 버튼 디자인은 로비 버튼 디자인이랑 똑같게 가로 길이만 이미지 짤라서
줄여주고"*.

★ 왜 «자르는» 것이고 «줄이는» 것이 아닌가
------------------------------------------
`LobbyButton.png` 를 좁은 칸에 그대로 넣으면 <b>가로로 눌린다</b> — 양끝의 창끝 장식과
붉은 마름모가 찌그러져 로비 버튼과 «같은 디자인» 으로 보이지 않는다. 9-slice(테두리)를
주는 방법도 있지만 이 그림은 장식이 끝에서 안쪽으로 길게 뻗어 있어 자를 자리를 잡기가
까다롭고, 원본 `.meta` 의 `spriteBorder` 를 건드리면 <b>로비 버튼까지</b> 달라진다.

그래서 <b>가운데의 평평한 띠만 도려내고</b> 양끝 장식을 그대로 붙인다. 장식은 원본
픽셀 그대로이므로 화면에서 로비 버튼과 <b>같은 크기·같은 모양</b>으로 보인다.

★ 화면에서 몇 px 인가 — 픽셀 축척을 로비와 <b>같게</b> 맞춘다
--------------------------------------------------------------
로비 버튼은 768px 그림을 360px 칸에 넣는다(축척 0.469). 오프닝 버튼도 같은 축척이어야
장식의 두께·그림자가 같아 보인다. 그래서 그림 폭을 «원하는 칸 폭 ÷ 0.469» 로 잡는다.

  · 칸 200x70  ←  그림 427x149   (LobbyButton 은 칸 360x70 · 그림 768x149)

⚠ <b>세로는 건드리지 않는다</b> — 높이를 줄이면 구워 넣은 그림자가 잘린다
  (`import_lobby_art.py` 의 BUTTON_FX 주석 그대로).

⚠ 자른 자리는 <b>가운데의 가장 평평한 곳</b>이어야 한다. 장식 쪽을 물면 이음매가 보인다.
  아래 KEEP_LEFT / KEEP_RIGHT 가 «끝에서 이만큼은 반드시 남긴다» 를 못 박는다.

⚠ `textureType` 이 8(Sprite) 이어야 `Resources.Load<Sprite>` 가 찾는다 — META 는
  `import_lobby_art.py` 것을 그대로 쓴다(규칙이 두 벌로 갈리지 않게).

⚠ 원본 `LobbyButton.png` 에서 읽어 새 파일로 쓰므로 몇 번을 돌려도 결과가 같다(멱등).

사용법:  py -3 Tools/make_skip_button_art.py
"""

import os
import sys

from PIL import Image

from import_lobby_art import DST, META, guid_for
from vault_path import PROJECT

#: 원본 — 로비 버튼 판 (그림자·글로우가 이미 구워져 있다)
SRC = os.path.join(DST, "LobbyButton.png")

#: 만드는 것
OUT_NAME = "LobbyButtonSkip"

#: 로비의 픽셀 축척 = 칸 폭 360 ÷ 그림 폭 768. 이 값을 유지해야 장식이 같은 크기로 보인다.
LOBBY_FRAME_W = 360
LOBBY_SPRITE_W = 768

#: 오프닝 «건너뛰기» 버튼의 칸 폭(px, 1920x1080 기준). 세로는 로비와 같은 70px.
SKIP_FRAME_W = 200

#: 끝에서 이만큼(px)은 장식이므로 반드시 남긴다 — 이 안쪽에서만 도려낸다.
KEEP_LEFT = 150
KEEP_RIGHT = 150


def main():
    if not os.path.isfile(SRC):
        print("  ⚠ 원본 없음:", SRC)
        print("    먼저 py -3 Tools/import_lobby_art.py 를 돌릴 것.")
        return 1

    plate = Image.open(SRC).convert("RGBA")
    w, h = plate.size
    print(f"  원본 {w}x{h}  (로비 칸 {LOBBY_FRAME_W}x{LOBBY_FRAME_W * h // w})")

    scale = LOBBY_FRAME_W / float(LOBBY_SPRITE_W)
    want = int(round(SKIP_FRAME_W / scale))          # 그림에서 남길 폭
    if want >= w:
        print(f"  ⚠ 자를 것이 없다 — 원하는 그림 폭 {want}px 이 원본 {w}px 보다 넓다.")
        return 1

    cut = w - want                                    # 도려낼 가운데 띠의 폭
    if KEEP_LEFT + KEEP_RIGHT > want:
        print(f"  ⚠ 양끝 장식({KEEP_LEFT}+{KEEP_RIGHT}px)이 남길 폭 {want}px 보다 넓다.")
        return 1

    # 도려낼 띠를 <b>가운데</b>에 둔다 — 양끝에서 같은 만큼 떨어져 이음매가 대칭이 된다.
    hole_x = (w - cut) // 2
    left = plate.crop((0, 0, hole_x, h))
    right = plate.crop((hole_x + cut, 0, w, h))

    out = Image.new("RGBA", (want, h), (0, 0, 0, 0))
    out.paste(left, (0, 0))
    out.paste(right, (left.size[0], 0))

    path = os.path.join(DST, OUT_NAME + ".png")
    out.save(path)

    # ⚠ guid 는 <b>`Assets/_Project` 기준 상대 경로</b>에서 뽑는다 —
    #   import_lobby_art.write() 와 같은 규칙이어야 한다.
    rel = os.path.relpath(path, os.path.join(PROJECT, "Assets", "_Project")).replace("\\", "/")
    g = guid_for(rel)
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(META.format(guid=g, sprite_id=g[:32]))

    frame_h = SKIP_FRAME_W * h / float(want)
    print(f"  {OUT_NAME:<16} {want}x{h}  "
          f"(가운데 {cut}px 도려냄 · {os.path.getsize(path) / 1024:.0f}KB)")
    print(f"       └ 씬 버튼 칸: {SKIP_FRAME_W}x{frame_h:.0f} — 로비(360x70)와 같은 축척")
    print("\nUnity 에서 Assets/Refresh 를 실행할 것.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
