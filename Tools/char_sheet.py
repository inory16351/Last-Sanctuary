# -*- coding: utf-8 -*-
"""캐릭터 모션 시트 분해의 **공통 몸통** (2026-08-20 신설).

**왜 만들었나** — 시그리드·시카리아·아루·골렘 네 스크립트가 같은 200줄을 네 번 복사하고
있었다. 새 캐릭터가 셋 더 늘면(카이론·아르세니아·불칸) 일곱 벌이 된다. 그러면
`enclosed_background` 를 부르는 것을 세 스크립트가 빠뜨렸던 것과 <b>같은 사고</b>가
반복된다 — 고칠 곳이 일곱 군데라서다.

그래서 <b>«무엇을 자를 것인가»(각 스크립트의 :data:`ROWS`)와 «어떻게 자를 것인가»(여기)</b>
를 갈랐다. 각 캐릭터 스크립트는 이제 **좌표표와 배선표만** 들고 있다.

시트마다 다른 것은 전부 :class:`Row` 의 칸으로 뺐다:

| 칸 | 뜻 |
|---|---|
| ``cells`` | 칸 가르기 방법 — ``("labels", ly0, ly1)`` · ``("gaps",)`` · ``("bounds", [...])`` ·
  ``("clusters",)`` (덩어리 + 뒷정리) · ``("span",)`` (폭 ÷ 장수) |
| ``expect`` | 칸이 몇 개로 잡혀야 하는가. **어긋나면 죽는다**(조용히 틀린 프레임을 굽는 것보다 낫다) |
| ``take`` | 그중 앞에서 몇 칸만 구울 것인가 — «한 줄에 모션과 이펙트가 섞인» 시트용 |
| ``skip`` | 앞에서 몇 칸을 버릴 것인가 — 같은 줄의 <b>뒤쪽</b>만 쓸 때 |
| ``keep`` | 남길 칸 <b>번호 목록</b> — 몸통과 이펙트가 <b>번갈아</b> 든 줄용(카이론 근거리·회복) |
| ``folder`` | 출력 폴더(기본은 ``name``). 두 줄을 한 폴더로 합칠 때 쓴다 |
| ``side`` | 좌/우 원화가 <b>따로 그려진</b> 줄. 미러하지 않고 그 방향으로만 굽는다 |
| ``start`` | 합쳐 굽는 줄의 시작 번호 |
| ``scale`` | ``False`` 면 크기 정규화 기준에서 뺀다(소환·사망처럼 «자라나는» 그림) |

여기서 하는 일의 순서는 **엘린이 세운 순서 그대로**다 — 순서가 바뀌면 조용히 어긋난다:

1. 구획 테두리 지우기(:func:`skin_sheet.load_sheet` ``box_borders``)
2. 제목 글자 지우기(:data:`Spec.erase`) — 밴드로 못 가르는 시트용
3. **갇힌 배경** 되돌리기 — 흰 판때기가 남아 있으면 상자가 커져 4번이 어긋난다
4. 발밑 그림자 지우기
5. 상자 잡기(옆 칸 조각 끊기) → 프레임 자르기
6. 크기 정규화(머리 면적 또는 상자 높이) → 굽기
"""

import os
import sys

import numpy as np
from PIL import Image

from skin_sheet import (
    SKIN_SPEC_NAME, write_skin_spec,
    load_sheet, cells_by_labels, cells_by_gaps, cells_by_clusters, cells_by_span,
    boxes_for, boxes_dominant, crop_rgba, body_anchor, base_anchor, compose,
    write_png, ensure_folder_meta, shadow_in_box, enclosed_background,
    resample_rgba, head_pixels,
)


class Row(object):
    """한 줄의 좌표와 배선. 뜻은 모듈 최상단 표 참조."""

    def __init__(self, name, kind, y0, y1, x0, x1, cells, expect,
                 src="01", take=None, skip=0, keep=None, folder=None, side=None, start=0,
                 scale=True, dominant=True):
        self.name = name
        self.kind = kind            # "body" | "fx"
        self.y0, self.y1 = y0, y1
        self.x0, self.x1 = x0, x1
        self.cells = cells
        self.expect = expect
        self.src = src
        self.take = take
        self.skip = skip
        #: ★ 남길 칸의 <b>번호 목록</b>(0부터). ``take``/``skip`` 으로는 표현할 수 없는 줄용.
        #:
        #:   카이론의 근거리 줄이 그렇다 — 몸통과 이펙트가 <b>번갈아</b> 들어 있다:
        #:   ``1 2 4 5 6 [7=이펙트] 8 9 10 [12=이펙트]``. 앞에서 N칸을 자르는 방식으로는
        #:   가운데 칸을 못 버린다. 회복 줄도 같다(이펙트 뒤에 몸통이 한 장 더 있다).
        self.keep = keep
        self.folder = folder or name
        self.side = side
        self.start = start
        self.scale = scale
        self.dominant = dominant


class Spec(object):
    """시트 한 벌(= 캐릭터 하나)의 설정."""

    def __init__(self, title, sources, dst_root, skin_spec, rows,
                 erase=(), no_direction=(), original_side=None, default_side="Right",
                 scale_reference="Idle", scale_metric="head",
                 dominant_join=0.06, pocket=(60, 60), ppu=None, filter_mode=None,
                 supersample=1.0, sharpen=0.0):
        self.title = title
        self.sources = sources          # {"01": path, …}
        self.dst_root = dst_root
        self.skin_spec = skin_spec
        self.rows = rows
        self.erase = list(erase)        # [(y0, y1, x0, x1), …]  ← 제목 글자
        self.no_direction = set(no_direction)
        self.original_side = original_side or {}
        self.default_side = default_side
        self.scale_reference = scale_reference
        self.scale_metric = scale_metric    # "head" | "height" | None
        self.dominant_join = dominant_join
        self.pocket = pocket                # (min_area, ring_lum)
        #: ★ 굽는 해상도 — :data:`supersample` 배로 키우고 ppu 도 같은 배로 올린다.
        #:   둘을 같이 올리므로 <b>게임 안의 크기는 그대로</b>이고 텍스처만 촘촘해진다.
        self.supersample = supersample
        self.sharpen = sharpen
        self.ppu = ppu
        self.filter_mode = filter_mode


SCALE_MIN, SCALE_MAX = 0.60, 1.90


def report_tilt(name, frames):
    """머리와 발의 가로 차이. 양수면 머리가 오른쪽(= 오른쪽으로 간다).

    엘린이 좌/우 줄의 y 를 뒤바꿔 적어 «가는 방향과 보는 방향이 반대» 가 된 적이 있어
    (113-6절) 줄마다 찍어 둔다. **판단은 사람이 한다.**
    """
    vals = []
    for f in frames:
        a = np.asarray(f)[:, :, 3] > 8
        ys = np.where(a.any(axis=1))[0]
        if len(ys) < 8:
            continue
        h = ys[-1] - ys[0] + 1
        head = a[ys[0]:ys[0] + max(1, h // 4)]
        foot = a[ys[-1] - max(1, h // 5):ys[-1] + 1]
        if not head.any() or not foot.any():
            continue
        hx = np.average(np.arange(a.shape[1]), weights=head.sum(axis=0))
        fx = np.average(np.arange(a.shape[1]), weights=foot.sum(axis=0))
        vals.append(hx - fx)
    v = float(np.mean(vals)) if vals else 0.0
    print("    %-18s 머리−발 %+6.1f px  (양수 = 오른쪽)" % (name, v))
    return v


def measure_scale(spec, collected):
    """모션마다 «대기 대비 배율».

    ★ 기준이 <b>둘</b>이다 — 시트마다 맞는 쪽이 다르다:

    * ``head`` : 머리 <b>면적</b>. 지팡이·활·낫이 상자를 늘려 키를 못 믿는 캐릭터용
      (:func:`skin_sheet.head_pixels` 의 긴 주석).
    * ``height``: 상자 <b>높이</b>. 든 것이 없어 «상자가 곧 몸» 인 유닛용(골렘).
      옆을 보고 웅크린 이동 원화에서 머리 판정이 무너지는 경우가 여기 해당한다.

    ⚠ **중앙값**을 쓴다. 평균을 쓰면 «안개로 흩어지는 마지막 두 장» 같은 프레임이
      줄 전체를 부풀린다(아루 회복 줄에서 실제로 x1.227 이 나왔다).
    """
    if spec.scale_metric is None:
        return {}

    vals = {}
    for name, frames in collected.items():
        if spec.scale_metric == "height":
            vals[name] = float(np.median([f.shape[0] for f in frames]))
        else:
            got = [head_pixels(f) for f in frames]
            got = [v for v in got if v > 60]
            if got:
                vals[name] = float(np.median(got))

    ref = vals.get(spec.scale_reference)
    if not ref:
        print("  ⚠ 기준 모션(%s)을 못 재 크기 정규화를 건너뜁니다" % spec.scale_reference)
        return {}

    factors = {}
    label = "상자 높이" if spec.scale_metric == "height" else "머리 면적"
    print("  [크기 정규화] %s → 대기 기준 배율" % label)
    for name, v in sorted(vals.items()):
        f = (ref / v) if spec.scale_metric == "height" else (ref / v) ** 0.5
        factors[name] = f
        flag = "" if SCALE_MIN <= f <= SCALE_MAX else "  ← ⚠ 범위 밖"
        print("    %-18s %6.1f  →  x%.3f%s" % (name, v, f, flag))

    bad = {k: round(v, 3) for k, v in factors.items() if not (SCALE_MIN <= v <= SCALE_MAX)}
    if bad:
        raise SystemExit(
            "⚠ 크기 배율이 안전 범위(%.2f~%.2f)를 벗어났습니다: %s\n"
            "   판정이 틀렸거나 시트가 바뀌었습니다 — 그 줄을 scale=False 로 빼거나 "
            "좌표를 다시 재세요." % (SCALE_MIN, SCALE_MAX, bad))
    return factors


#: 밴드 위·아래를 몇 px 더 살펴볼 것인가 (:func:`warn_if_clipped`).
CLIP_MARGIN = 6


def warn_if_clipped(sheet, row):
    """
    ★★ <b>밴드가 그림을 자르고 있지 않은지</b> 검사한다 (2026-08-20 신설).

    <b>왜 필요했나</b> — 유저 질문: *"아르세니아 안 짤린거 맞음?"*. 실제로 잘려 있었다:
    이동 줄의 그림은 y 58~131 인데 밴드를 73~139 로 적어 두어 <b>머리 위 15px 가 잘리고
    프레임 번호 줄이 1px 들어와</b> 있었다. 개수 검사(``expect``)는 <b>이걸 못 잡는다</b> —
    칸 수는 맞기 때문이다.

    검사 방법은 단순하다: 밴드 <b>바로 위·아래 %d px</b> 를 들여다보고 그림이 있으면 알린다.
    제목·번호 줄도 걸리므로 <b>죽이지 않고 경고만</b> 한다 — 사람이 보고 판단한다.
    """ % CLIP_MARGIN
    m = sheet["mask"]
    h = m.shape[0]
    up0, up1 = max(0, row.y0 - CLIP_MARGIN), max(0, row.y0 - 1)
    dn0, dn1 = min(h - 1, row.y1 + 1), min(h - 1, row.y1 + CLIP_MARGIN)

    up = int(m[up0:up1 + 1, row.x0:row.x1 + 1].sum()) if up1 >= up0 else 0
    dn = int(m[dn0:dn1 + 1, row.x0:row.x1 + 1].sum()) if dn1 >= dn0 else 0
    if up or dn:
        print("    ⚠ %-18s 밴드 밖에 그림이 있다 — 위 %d px · 아래 %d px "
              "(y%d~%d). 제목·번호 줄이면 정상, 아니면 <b>잘리고 있다</b>."
              % (row.name, up, dn, row.y0, row.y1))


def cells_of(sheet, row):
    kind = row.cells[0]
    if kind == "labels":
        return cells_by_labels(sheet["gray"], row.x0, row.x1, row.cells[1], row.cells[2])
    if kind == "gaps":
        # ("gaps",) 또는 ("gaps", 최소 빈 열 폭)
        return cells_by_gaps(sheet["mask"], row.y0, row.y1, row.x0, row.x1, *row.cells[1:])
    if kind == "bounds":
        b = row.cells[1]
        return [(b[i], b[i + 1] - 1) for i in range(len(b) - 1)]
    if kind == "clusters":
        # ★ 덩어리를 그대로 쓰되 부스러기를 버리고 붙은 덩어리를 가른다(베일이 만든 함수).
        #   라벨이 미덥지 않고 빈 열도 고르지 않은 시트에서 가장 잘 듣는다.
        return cells_by_clusters(sheet["mask"], row.y0, row.y1, row.x0, row.x1,
                                 *row.cells[1:])
    if kind == "span":
        # ★ «그림이 놓인 폭 ÷ 장수». 라벨을 아예 안 본다 — 간격이 고른 줄에서 가장 튼튼하다.
        return cells_by_span(sheet["mask"], row.y0, row.y1, row.x0, row.x1, row.expect)
    raise SystemExit("알 수 없는 칸 가르기: %r" % (row.cells,))


def cut(spec, sheets):
    """자르기만 한다 — 배율은 기준(대기)을 다 재고 나서야 알 수 있다."""
    out, collected = [], {}

    for row in spec.rows:
        sheet = sheets[row.src]
        cells = cells_of(sheet, row)
        if len(cells) != row.expect:
            raise SystemExit(
                "⚠ %s(%s): 칸이 %d개인데 %d개를 기대했습니다 "
                "(그림 y%d~%d · x%d~%d · %s). 시트가 바뀌었으면 좌표를 다시 재세요."
                % (row.name, row.src, len(cells), row.expect,
                   row.y0, row.y1, row.x0, row.x1, row.cells[0]))

        if row.keep is not None:
            cells = [cells[i] for i in row.keep]
        else:
            if row.skip:
                cells = cells[row.skip:]
            if row.take:
                cells = cells[:row.take]

        if row.kind == "body":
            # ③ 갇힌 배경 → ④ 그림자. 순서가 중요하다(모듈 최상단).
            for cx0, cx1 in cells:
                pockets = enclosed_background(sheet, row.y0, row.y1, cx0, cx1,
                                              min_area=spec.pocket[0], ring_lum=spec.pocket[1])
                if pockets.any():
                    sheet["mask"] &= ~pockets

            rough = [b for b in boxes_for(sheet["mask"], cells, row.y0, row.y1) if b is not None]
            shadow = np.zeros(sheet["mask"].shape, dtype=bool)
            for b in rough:
                shadow |= shadow_in_box(sheet, b)
            sheet["mask"] &= ~shadow

        if row.kind == "body" and row.dominant:
            raw = boxes_dominant(sheet["mask"], cells, row.y0, row.y1,
                                 min_ink_ratio=spec.dominant_join)
        else:
            raw = boxes_for(sheet["mask"], cells, row.y0, row.y1)

        boxes = [b for b in raw if b is not None]
        if len(boxes) != len(cells):
            raise SystemExit("⚠ %s: 빈 칸이 %d개 있습니다 — 밴드를 확인하세요."
                             % (row.name, len(cells) - len(boxes)))

        frames = [crop_rgba(sheet, b) for b in boxes]
        out.append((row, frames))
        if row.kind == "body" and row.scale:
            collected[row.name] = frames
    return out, collected


def bake(spec, cut_rows, factors):
    made = 0
    for row, frames in cut_rows:
        factor = factors.get(row.name, 1.0) * spec.supersample
        if abs(factor - 1.0) > 0.002:
            frames = [resample_rgba(f, factor) for f in frames]
        if spec.sharpen > 0:
            from skin_sheet import sharpen_rgba
            frames = [sharpen_rgba(f, amount=spec.sharpen) for f in frames]

        anchor = body_anchor if row.kind == "body" else base_anchor
        images, w, h = compose(frames, [anchor(f) for f in frames])

        folder = os.path.join(spec.dst_root, row.folder)
        kw = {}
        if spec.ppu:
            kw["ppu"] = spec.ppu
        if spec.filter_mode is not None:
            kw["filter_mode"] = spec.filter_mode

        if row.side:
            # 좌/우 원화가 따로 그려진 줄 — 미러하지 않는다.
            for i, img in enumerate(images):
                write_png(img, folder, "Char_%s_%s_%02d" % (row.folder, row.side, row.start + i), **kw)
                made += 1
            note = "원화 그대로 (%s)" % row.side
        elif row.name in spec.no_direction or row.folder in spec.no_direction:
            for i, img in enumerate(images):
                write_png(img, folder, "Char_%s_%02d" % (row.folder, row.start + i), **kw)
                made += 1
            note = "방향 없음"
        else:
            orig = spec.original_side.get(row.name, spec.default_side)
            other = "Left" if orig == "Right" else "Right"
            for i, img in enumerate(images):
                write_png(img, folder, "Char_%s_%s_%02d" % (row.folder, orig, row.start + i), **kw)
                write_png(img.transpose(Image.FLIP_LEFT_RIGHT), folder,
                          "Char_%s_%s_%02d" % (row.folder, other, row.start + i), **kw)
                made += 2
            note = "%s 원본 + 미러" % ("오른쪽" if orig == "Right" else "★왼쪽")
        ensure_folder_meta(folder)

        extra = "" if abs(factor - 1.0) <= 0.002 else "  (x%.3f)" % factor
        print("  %-18s %3d x %3d · %2d장 · %s%s" % (row.name, w, h, len(images), note, extra))
    return made


def run(spec):
    """분해 한 번. 각 캐릭터 스크립트의 ``main()`` 은 이 한 줄이면 된다."""
    sys.stdout.reconfigure(encoding="utf-8")
    print("[%s 모션 시트 분해]" % spec.title)

    sheets = {k: load_sheet(v, box_borders=True) for k, v in spec.sources.items()}

    for y0, y1, x0, x1 in spec.erase:
        tgt = sheets["01"]
        n = int(tgt["mask"][y0:y1 + 1, x0:x1 + 1].sum())
        tgt["mask"][y0:y1 + 1, x0:x1 + 1] = False
        print("  제목 지움 y%d~%d x%d~%d · %d px" % (y0, y1, x0, x1, n))

    print("  [밴드 검사] 밴드 밖에 그림이 남아 있으면 알린다")
    for row in spec.rows:
        warn_if_clipped(sheets[row.src], row)

    cut_rows, collected = cut(spec, sheets)

    print("  [방향 실측]")
    for row, frames in cut_rows:
        if row.kind == "body":
            report_tilt(row.name, frames)

    factors = measure_scale(spec, collected)
    made = bake(spec, cut_rows, factors)

    lines = write_skin_spec(spec.dst_root, spec.skin_spec, spec.title)
    ensure_folder_meta(spec.dst_root)
    print("  스킨 설정 %s (%d줄)" % (SKIN_SPEC_NAME, lines))
    print("  → 프레임 %d장" % made)
    print("  다음: 유니티 메뉴 LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기")
