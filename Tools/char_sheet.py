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
  ``("clusters",)`` (덩어리 + 뒷정리) · ``("span",)`` (폭 ÷ 장수) ·
  ``("feet",)`` (<b>발밑만 본다</b> — 이펙트가 몸통과 겹친 시트용 · 2026-08-20 신설) |
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
    cells_by_feet, erase_title_pills, drop_stray_parts, plant_feet,
    boxes_for, boxes_dominant, crop_rgba, body_anchor, base_anchor, compose,
    write_png, ensure_folder_meta, clear_frames, shadow_in_box, enclosed_background,
    resample_rgba, head_pixels, body_extent, feather_edges,
)


class Row(object):
    """한 줄의 좌표와 배선. 뜻은 모듈 최상단 표 참조."""

    def __init__(self, name, kind, y0, y1, x0, x1, cells, expect,
                 src="01", take=None, skip=0, keep=None, folder=None, side=None, start=0,
                 scale=True, dominant=True, feather=None):
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
        #: ★★ 자른 자리를 <b>서서히 사라지게</b> 할 변과 폭 — ``(위, 아래, 좌, 우)`` px.
        #:
        #:   맞닿은 두 줄을 가를 때 쓴다(:func:`skin_sheet.feather_edges` 의 긴 주석).
        #:   ⚠ 몸통 줄에는 쓰지 말 것 — 발이나 머리가 흐려진다. 이펙트 전용이다.
        self.feather = feather


class Spec(object):
    """시트 한 벌(= 캐릭터 하나)의 설정."""

    def __init__(self, title, sources, dst_root, skin_spec, rows,
                 erase=(), no_direction=(), original_side=None, default_side="Right",
                 scale_reference="Idle", scale_metric="head",
                 dominant_join=0.06, pocket=(60, 60), ppu=None, filter_mode=None,
                 supersample=1.0, sharpen=0.0, fx_target_height=None, pills=None,
                 grow_margin=None, drop_stray=True):
        self.title = title
        self.sources = sources          # {"01": path, …}
        self.dst_root = dst_root
        self.skin_spec = skin_spec
        self.rows = rows
        self.erase = list(erase)        # [(y0, y1, x0, x1), …]  ← 제목 글자
        #: ★★ <b>제목 딱지</b>(검은 알약 + 흰 글자)를 스스로 찾아 지운다 —
        #:   ``{}`` 면 기본값으로, 딕셔너리면 :func:`skin_sheet.erase_title_pills` 에
        #:   그대로 넘긴다(``min_run`` 등). ``None`` 이면 끄는 것이다.
        #:
        #:   <b>왜 :data:`erase` 로는 안 되나</b> — 딱지는 줄마다 x 가 다르고 개수도 다르다.
        #:   세라피엘 시트는 <b>일곱 줄에 딱지가 아홉 개</b>이고, 그 좌표를 손으로 적으면
        #:   원화가 한 번 바뀔 때 아홉 군데를 다시 재야 한다. 딱지는 «가로로 긴 어두운 줄 +
        #:   얇다» 로 <b>재서 찾을 수 있으므로</b> 재는 쪽이 맞다.
        self.no_direction = set(no_direction)
        self.original_side = original_side or {}
        self.default_side = default_side
        self.scale_reference = scale_reference
        self.scale_metric = scale_metric    # "head" | "height" | "min" | None
        self.dominant_join = dominant_join
        self.pocket = pocket                # (min_area, ring_lum)
        #: ★ 굽는 해상도 — :data:`supersample` 배로 키우고 ppu 도 같은 배로 올린다.
        #:   둘을 같이 올리므로 <b>게임 안의 크기는 그대로</b>이고 텍스처만 촘촘해진다.
        self.supersample = supersample
        self.sharpen = sharpen
        self.ppu = ppu
        self.filter_mode = filter_mode
        #: ★ 이펙트 줄의 <b>목표 높이(px)</b> — ``{줄 이름: 높이}``.
        #:
        #:   <b>왜 배율이 아니라 높이인가</b> — 몸통은 «대기 대비» 로 잴 수 있지만
        #:   이펙트는 견줄 기준이 없다. 그리고 <b>별지에서 오는 줄</b>은 원화가 아예 다른
        #:   크기로 그려져 있다(아루 회복 별지는 639px — 그대로 구우면 PPU 64 기준
        #:   <b>10타일</b> 짜리 그림이 아군 발밑에 깔린다).
        #:   그때 «몇 배» 는 사람이 알 수 없고 «몇 px» 는 알 수 있으므로 이쪽을 받는다.
        #:
        #:   ⚠ :data:`supersample` 과 곱해진다 — 굽는 해상도를 올려도 게임 안 크기는 그대로다.
        self.fx_target_height = dict(fx_target_height or {})
        self.pills = pills
        #: ★★ 프레임을 <b>위·아래로 되찾을</b> 최대 폭(px). ``None`` 이면 기본값
        #:   (:data:`GROW_MARGIN`), ``0`` 이면 끈다.
        #:   무엇을 고치는지는 :func:`skin_sheet.grow_box_vertical` 의 ★★ 참조.
        self.grow_margin = grow_margin
        #: ★★ 옆 프레임에서 들어온 <b>떠 있는 조각</b>을 지울지
        #:   (:func:`skin_sheet.drop_stray_parts` 의 ★★). 몸통 줄에만 쓴다.
        self.drop_stray = drop_stray


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


#: 몸통 크기가 대기와 이만큼 넘게 다르면 경고한다(비율).
BODY_TOLERANCE = 0.08


def report_body_size(spec, cut_rows):
    """
    ★★ <b>줄마다 «몸통» 크기를 재서 대기와 비교한다</b> (2026-08-20 신설).

    유저 지시: *"캐릭터의 크기는 유지하고 앞 공간에 이펙트가 나올 공간을 넣는 로직 …
    딱 캐릭터의 크기는 유지되게"*. 앞 공간(캔버스 폭)은 :func:`skin_sheet.compose` 가
    이미 <b>몸통 중심 기준 좌우 같은 폭</b>으로 잡아 준다 — 피벗이 몸통에 고정되므로
    이펙트가 아무리 앞으로 뻗어도 캐릭터는 제자리다.

    남는 문제는 «몸통이 줄마다 같은 크기로 그려졌는가» 하나뿐인데, <b>상자 크기로는
    그걸 못 본다</b>(이펙트가 상자를 늘린다). 그래서 :func:`skin_sheet.body_extent` 로
    <b>몸통만</b> 재서 대기와 견준다. 어긋나면 원화가 정말 다른 크기이거나 밴드가
    잘린 것이다 — 어느 쪽인지는 사람이 보고 정한다(그래서 경고만 한다).
    """
    sizes = {}
    for row, frames in cut_rows:
        if row.kind != "body":
            continue
        ext = [body_extent(f) for f in frames]
        ext = [e for e in ext if e[1] > 0]
        if ext:
            sizes[row.name] = (float(np.median([e[0] for e in ext])),
                               float(np.median([e[1] for e in ext])))

    ref = sizes.get(spec.scale_reference)
    print("  [몸통 크기] 이펙트를 뺀 «몸» 만 잰 값 — 대기와 같아야 한다")
    for name, (w, h) in sorted(sizes.items()):
        if ref and ref[1] > 0:
            d = h / ref[1] - 1.0
            # ⚠ 이 값은 <b>자세에도 움직인다</b> — 달리는 원화는 웅크려서 20~30% 짧게
            #   나온다(카이론 이동 −27%). 그것은 <b>연출이지 크기 오류가 아니다.</b>
            #   그래서 «확인» 으로만 적고, 판단은 사람이 같은 발 높이로 겹쳐 보고 한다.
            flag = "" if abs(d) <= BODY_TOLERANCE else "  ← 확인 (대기와 %+.0f%% · 자세일 수 있다)" % (d * 100)
        else:
            flag = ""
        print("    %-18s 몸통 %3.0f x %-3.0f px%s" % (name, w, h, flag))
    return sizes


def _median_head(frames):
    """이 줄의 머리 면적(중앙값). 잴 수 없으면 0.

    ⚠ **중앙값**을 쓴다. 평균을 쓰면 «안개로 흩어지는 마지막 두 장» 같은 프레임이
      줄 전체를 부풀린다(아루 회복 줄에서 실제로 x1.227 이 나왔다).
    """
    got = [head_pixels(f) for f in frames]
    got = [v for v in got if v > 60]
    return float(np.median(got)) if got else 0.0


def _median_height(frames):
    """이 줄의 상자 높이(중앙값)."""
    return float(np.median([f.shape[0] for f in frames]))


def measure_scale(spec, collected):
    """모션마다 «대기 대비 배율».

    ★ 기준이 <b>셋</b>이다 — 시트마다 맞는 쪽이 다르다:

    * ``head``  : 머리 <b>면적</b>. 지팡이·활·낫이 상자를 늘려 키를 못 믿는 캐릭터용
      (:func:`skin_sheet.head_pixels` 의 긴 주석).
    * ``height``: 상자 <b>높이</b>. 든 것이 없어 «상자가 곧 몸» 인 유닛용(골렘).
    * ``min``   : ★★ <b>위 둘 중 작은 쪽</b> (2026-08-21 신설 · 캐릭터의 기본값).

    ★★ <b>왜 «작은 쪽» 인가</b> (2026-08-21) — 두 기준은 <b>각각 한쪽으로만 틀린다</b>:

    * ``height`` 는 <b>웅크린 자세를 크기 오류로 읽는다</b> — 달리는 원화는 몸을 기울여
      키가 20~30% 줄어드는 것이 <b>연출</b>인데, 그것을 «작아졌다» 로 보고 <b>부풀린다</b>
      (아루 이동: 상자 x1.485 vs 머리 x1.279).
    * ``head`` 는 <b>밝고 저채도인 다른 부위</b>가 판정 창에 들어오면 부풀린다 —
      아르세니아는 대기에서 <b>흰 날개가 접혀</b> 상단 45% 에 들어오는데 마법 줄은
      날개를 <b>펼쳐</b> 창 밖으로 나간다. 그래서 기준(대기)만 커져 배율이 뛴다
      (마법: 머리 x1.371 vs 상자 x1.188).

    ★★ 그래서 ``min`` 은 <b>단순한 최솟값이 아니다</b>. 규칙은 한 줄이다:

        <b>상자 높이는 «키우는 것» 만 막는다 — «줄이는» 근거는 되지 않는다.</b>

            f = min(머리, 상자)   상자 배율이 1 이상일 때
            f = 머리              그렇지 않을 때

    <b>왜 그렇게 갈랐나</b> — 상자 높이는 <b>양쪽으로</b> 틀린다:

    * 웅크린 자세면 <b>위로</b> 틀린다(위 참조) → 그때는 머리를 넘지 못하게 <b>깎는 역할</b>이
      맞다.
    * ⚠ 그런데 <b>이펙트가 프레임을 키우면 아래로</b> 틀린다 — 카이론 「스킬 1」은 몸이
      <b>황금 구체 안</b>에 들어가 상자가 대기보다 크다. 그러면 상자 배율이 <b>x0.930</b> 이
      되어 «몸을 7% 줄여라» 는 뜻이 되는데, 머리로 재면 <b>x1.144</b>(키워야 한다)다.
      그대로 최솟값을 쓰면 <b>고치려던 것과 반대로</b> 스킬 중에 몸이 작아진다.

    그래서 «1 미만인 상자 배율» 은 <b>믿지 않는다</b>. 정말로 줄여야 하는 줄(변신·강림처럼
    원화가 일부러 큰 줄)은 <b>머리도 같이 1 미만</b>으로 나오므로 머리만으로 잡힌다
    (아루 「강림」 머리 x0.855) — 그런 줄은 대개 :data:`Row.scale` 을 ``False`` 로 빼는 것이
    더 낫다(아르세니아 「천사 강림」).

    ⚠ 값이 어긋나는 줄은 <b>둘 다 찍는다</b> — 사람이 보고 판단할 재료다.
    """
    if spec.scale_metric is None:
        return {}

    head, tall = {}, {}
    for name, frames in collected.items():
        head[name] = _median_head(frames)
        tall[name] = _median_height(frames)

    use_head = spec.scale_metric in ("head", "min")
    use_tall = spec.scale_metric in ("height", "min")

    ref_head = head.get(spec.scale_reference, 0.0)
    ref_tall = tall.get(spec.scale_reference, 0.0)
    if not (use_head and ref_head > 0.0) and not (use_tall and ref_tall > 0.0):
        print("  ⚠ 기준 모션(%s)을 못 재 크기 정규화를 건너뜁니다" % spec.scale_reference)
        return {}

    label = {"head": "머리 면적", "height": "상자 높이", "min": "머리·상자 중 작은 쪽"}
    print("  [크기 정규화] %s → 대기 기준 배율" % label[spec.scale_metric])

    factors = {}
    for name in sorted(collected):
        fh = ft = None
        if use_head and ref_head > 0.0 and head[name] > 0.0:
            fh = (ref_head / head[name]) ** 0.5      # 면적이므로 제곱근
        if use_tall and ref_tall > 0.0 and tall[name] > 0.0:
            ft = ref_tall / tall[name]

        if fh is None and ft is None:
            continue
        if spec.scale_metric == "head":
            f = fh if fh is not None else ft
        elif spec.scale_metric == "height":
            f = ft if ft is not None else fh
        elif fh is None:
            f = ft
        elif ft is None or ft < 1.0:
            # ⚠ 1 미만인 상자 배율은 «이펙트가 프레임을 키운 것» 이라 믿지 않는다(위 ★★).
            f = fh
        else:
            f = min(fh, ft)
        factors[name] = f
        note = ""
        if fh is not None and ft is not None and abs(fh - ft) > 0.05:
            note = "  (머리 x%.3f · 상자 x%.3f — 어긋난다)" % (fh, ft)
        flag = "" if SCALE_MIN <= f <= SCALE_MAX else "  ← ⚠ 범위 밖"
        print("    %-18s →  x%.3f%s%s" % (name, f, note, flag))

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


#: 프레임을 위·아래로 되찾을 기본 최대 폭(px). 밴드를 사람이 몇 px 좁게 잡는 정도.
GROW_MARGIN = 26


def grow_limits(spec, row, height):
    """
    ★★ 이 줄이 위·아래로 <b>어디까지</b> 넓혀도 되는가 (2026-08-22 신설).

    <b>이웃 줄의 밴드 사이 중간</b>까지다. «이웃» 은 <b>x 가 겹치는 줄</b>만 센다 —
    이 프로젝트의 시트는 <b>한 줄을 좌/우 단으로 갈라</b> 서로 다른 모션을 나란히 두므로
    (세라피엘 근거리|원거리), y 만 보면 «자기 짝» 을 이웃으로 잡아 못 넓힌다.

    ⚠ 중간까지 열어 주되 :data:`GROW_MARGIN` 으로 한 번 더 조인다 — 넓힐 근거는
      «잉크가 이어진다» 이고, 그 판정이 몇십 px 을 넘어 계속 이어질 일은 없다.
      상한이 있으면 판정이 틀렸을 때의 피해가 한 줄 안에 머문다.
    """
    margin = GROW_MARGIN if spec.grow_margin is None else spec.grow_margin
    if margin <= 0:
        return None
    lo, hi = 0, height - 1
    for other in spec.rows:
        if other is row or other.src != row.src:
            continue
        # x 가 겹치지 않으면 이웃이 아니다(같은 줄의 좌/우 단).
        if other.x1 < row.x0 or other.x0 > row.x1:
            continue
        if other.y1 < row.y0:
            lo = max(lo, (other.y1 + row.y0) // 2 + 1)
        elif other.y0 > row.y1:
            hi = min(hi, (row.y1 + other.y0) // 2 - 1)
    return (max(lo, row.y0 - margin), min(hi, row.y1 + margin))


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
    if kind == "feet":
        # ★★ «발밑만 본다». 이펙트가 몸통과 겹쳐 그려진 시트(2026-08-20 교체분)의 정답이다 —
        #   공중에 뜬 이펙트는 발밑 띠에 없으므로 몸통만 남는다(그 함수의 긴 주석).
        #
        # ★ <b>``expect`` 를 «목표 장수» 로 함께 넘긴다.</b> 맨다리 캐릭터는 <b>다리 하나가
        #   조각 하나</b>가 되어 조각이 프레임보다 많이 나온다(카이론 이동 줄: 9장 → 16조각).
        #   그때 «가장 좁은 틈부터» 합쳐 장수를 맞춘다(`skin_sheet.merge_to_count`).
        #   ⚠ 그러므로 ``feet`` 의 ``expect`` 는 <b>검산이 아니라 입력</b>이다(``span`` 과 같다) —
        #     장수는 시트를 눈으로 세어 적을 것. 조각이 장수보다 <b>적으면</b> 합칠 수 없고,
        #     그때는 아래 개수 검사가 죽어 준다.
        return cells_by_feet(sheet["mask"], row.y0, row.y1, row.x0, row.x1,
                             *row.cells[1:], count=row.expect)
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

        grow = grow_limits(spec, row, sheet["mask"].shape[0])
        if row.kind == "body" and row.dominant:
            raw = boxes_dominant(sheet["mask"], cells, row.y0, row.y1,
                                 min_ink_ratio=spec.dominant_join, name=row.name, grow=grow)
        else:
            raw = boxes_for(sheet["mask"], cells, row.y0, row.y1, name=row.name, grow=grow)

        boxes = [b for b in raw if b is not None]
        if len(boxes) != len(cells):
            raise SystemExit("⚠ %s: 빈 칸이 %d개 있습니다 — 밴드를 확인하세요."
                             % (row.name, len(cells) - len(boxes)))

        frames = [crop_rgba(sheet, b) for b in boxes]
        if spec.drop_stray and row.kind == "body":
            # ★ 옆 프레임에서 들어온 «떠 있는 조각» 을 뗀다(그 함수의 ★★).
            gone = 0
            cleaned = []
            for f in frames:
                f2, n = drop_stray_parts(f)
                gone += n
                cleaned.append(f2)
            if gone:
                print("    %-18s 옆 프레임 조각 %d px 떼어냄" % (row.name, gone))
            frames = cleaned
        out.append((row, frames))
        if row.kind == "body" and row.scale:
            collected[row.name] = frames
    return out, collected


def bake(spec, cut_rows, factors):
    # ★★ 굽기 전에 폴더를 비운다 — 장수가 줄어드는 수정에서 <b>옛 프레임이 남아</b>
    #   빼려던 그림이 계속 재생되는 사고가 실제로 있었다(:func:`skin_sheet.clear_frames`).
    #   ⚠ 한 폴더에 두 줄을 합쳐 굽는 경우(``Walk`` 의 좌/우)가 있으므로 <b>한 번만</b> 비운다.
    wiped = set()
    for row, _frames in cut_rows:
        folder = os.path.join(spec.dst_root, row.folder)
        if folder not in wiped:
            n = clear_frames(folder)
            if n:
                print("  옛 프레임 %d개 지움: %s" % (n, row.folder))
            wiped.add(folder)

    made = 0
    for row, frames in cut_rows:
        factor = factors.get(row.name, 1.0)
        if row.name in spec.fx_target_height:
            # ★ 이펙트는 «대기 대비» 로 못 재므로 목표 높이로 배율을 낸다
            #   (:data:`Spec.fx_target_height` 의 긴 주석).
            tall = max(f.shape[0] for f in frames)
            factor = spec.fx_target_height[row.name] / float(tall)
            print("    %-18s 원본 높이 %dpx → 목표 %dpx (x%.3f)"
                  % (row.name, tall, spec.fx_target_height[row.name], factor))
        factor *= spec.supersample
        if abs(factor - 1.0) > 0.002:
            frames = [resample_rgba(f, factor) for f in frames]
        if spec.sharpen > 0:
            from skin_sheet import sharpen_rgba
            frames = [sharpen_rgba(f, amount=spec.sharpen) for f in frames]
        if row.feather:
            # ★ 배율을 먹인 <b>뒤에</b> 흐린다 — 먼저 하면 확대가 흐림을 늘린다.
            t, b, l, r = (list(row.feather) + [0, 0, 0, 0])[:4]
            frames = [feather_edges(f, top=t, bottom=b, left=l, right=r) for f in frames]

        if row.kind == "body":
            # ★★ 묶음을 통째로 밀어 <b>발</b>을 피벗에 맞춘다(그 함수의 ★★).
            #   묶음 안의 움직임은 그대로 두고 묶음끼리만 맞춘다.
            anchors, shift = plant_feet(frames, [body_anchor(f) for f in frames])
        else:
            anchors, shift = [base_anchor(f) for f in frames], 0.0
        images, w, h = compose(frames, anchors)

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
        if abs(shift) >= 1.0:
            extra += "  피벗 %+.0fpx (발 맞춤)" % shift
        print("  %-18s %3d x %3d · %2d장 · %s%s" % (row.name, w, h, len(images), note, extra))
    return made


def run(spec, fx_target_height=None):
    """분해 한 번. 각 캐릭터 스크립트의 ``main()`` 은 이 한 줄이면 된다.

    ``fx_target_height`` 는 :data:`Spec.fx_target_height` 를 덮어쓴다 — 좌표표와 함께
    스크립트 아래쪽에 적어 두는 편이 읽기 좋은 경우가 있어 둘 다 받는다.
    """
    if fx_target_height:
        spec.fx_target_height = dict(fx_target_height)
    sys.stdout.reconfigure(encoding="utf-8")
    print("[%s 모션 시트 분해]" % spec.title)

    sheets = {k: load_sheet(v, box_borders=True) for k, v in spec.sources.items()}

    if spec.pills is not None:
        # ★ 딱지를 <b>가장 먼저</b> 지운다 — 밴드 검사·칸 가르기·상자 잡기가 모두
        #   «딱지가 없는 마스크» 를 봐야 한다(:data:`Spec.pills` 의 ★★).
        for sheet in sheets.values():
            erase_title_pills(sheet, **spec.pills)

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

    report_body_size(spec, cut_rows)
    factors = measure_scale(spec, collected)
    made = bake(spec, cut_rows, factors)

    # ⚠ «누가 만들었나» 에는 <b>스크립트 이름</b>을 적는다 — 예전에는 `spec.title`
    #   (캐릭터 이름)을 넘겨 파일 첫 줄이 «세라피엘 가 만든 파일» 로 나왔다.
    made_by = ("Tools/%s" % os.path.basename(sys.argv[0])
               if sys.argv and sys.argv[0] else "Tools/char_sheet.py")
    lines = write_skin_spec(spec.dst_root, spec.skin_spec, made_by)
    ensure_folder_meta(spec.dst_root)
    print("  스킨 설정 %s (%d줄)" % (SKIN_SPEC_NAME, lines))
    print("  → 프레임 %d장" % made)
    print("  다음: 유니티 메뉴 LastSanctuary/스킨/원화 폴더로 스킨 에셋 만들기")
