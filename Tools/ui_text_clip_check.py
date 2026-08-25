# -*- coding: utf-8 -*-
"""
UI 프레임에 <b>글자가 먹히는 자리</b>를 전수로 찾는다 (2026-08-25 신설).

판·창에 9-슬라이스 그림을 깔면 <b>테두리 굵기만큼 안쪽이 좁아진다</b>. 예전 단색 판은
테두리가 1~2px 이었는데 지금은 창 23px · 판 10px · 액자 16px 이라, 예전 여백(14~18px)
으로 잡아 둔 글자들이 <b>테두리 밑으로 들어간다</b>.

씬 YAML 을 직접 읽어 ① 그림이 깔린 판의 «안전한 안쪽» 을 구하고 ② 그 안의 모든
TMP 글자 렉트와 견줘 ③ 삐져나온 것만 보고한다. 눈으로 찾지 않는다.

사용법:  python Tools/ui_text_clip_check.py
"""
import os, re, sys

from vault_path import PROJECT as PROJ

#: ★★ <b>훑는 캔버스가 둘이다</b> — 도움말 세 창은 `Help_Root` 에 있다(2026-08-26).
#:   `UiSkinApplier.Roots` / `UiTextInset` 와 <b>같은 목록</b>이어야 «에디터에서는
#:   맞췄는데 검사기는 모르는» 상태가 안 생긴다.
ROOTS = ("UI_Root", "Help_Root")

SCENE = os.path.join(PROJ, "Assets", "Scenes", "Proto_01.unity")

#: ⚠ 경계를 <b>`.png.meta` 에서 직접 읽는다</b>(2026-08-26). 예전에는
#:   `Temp/ui_sprite_cut.json` 을 읽었는데 <b>`Temp/` 는 유니티가 지우는 자리</b>라
#:   며칠만 지나도 «파일 없음» 으로 죽었다. 메타는 에셋과 함께 커밋되므로 안 사라진다.
CANVAS = (1920.0, 1080.0)

try: sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception: pass


def load_scene():
    txt = open(SCENE, encoding="utf-8", errors="replace").read()
    go, rt, img, tmp, layout = {}, {}, {}, {}, set()

    for m in re.finditer(r'--- !u!1 &(\d+)\n(GameObject:.*?)(?=\n--- |\Z)', txt, re.S):
        nm = re.search(r'm_Name: (.*)', m.group(2))
        go[m.group(1)] = nm.group(1).strip() if nm else "?"

    for m in re.finditer(r'--- !u!224 &(\d+)\n(RectTransform:.*?)(?=\n--- |\Z)', txt, re.S):
        b = m.group(2)
        ow = re.search(r'm_GameObject: \{fileID: (\d+)\}', b).group(1)
        def v(k, d=(0.0, 0.0)):
            mm = re.search(k + r': \{x: ([-\d.e]+), y: ([-\d.e]+)\}', b)
            return (float(mm.group(1)), float(mm.group(2))) if mm else d
        kids = re.findall(r'- \{fileID: (\d+)\}', b.split('m_Children:')[1].split('m_Father:')[0]) \
            if 'm_Children:' in b else []
        rt[m.group(1)] = dict(id=m.group(1), ow=ow, kids=kids,
                              father=re.search(r'm_Father: \{fileID: (\d+)\}', b).group(1),
                              amin=v('m_AnchorMin'), amax=v('m_AnchorMax'),
                              pos=v('m_AnchoredPosition'), sd=v('m_SizeDelta'), piv=v('m_Pivot'))

    for m in re.finditer(r'--- !u!114 &(\d+)\n(MonoBehaviour:.*?)(?=\n--- |\Z)', txt, re.S):
        b = m.group(2)
        ow = re.search(r'm_GameObject: \{fileID: (\d+)\}', b).group(1)
        if 'm_FillMethod' in b and 'm_Sprite' in b:
            g = re.search(r'm_Sprite: \{fileID: [-\d]+, guid: ([0-9a-f]{32})', b)
            img[ow] = g.group(1) if g else None
        if 'm_Spacing' in b or 'm_CellSize' in b or 'm_HorizontalFit' in b:
            layout.add(ow)
        if 'm_fontSize' in b:
            fs = re.search(r'm_fontSize: ([\d.]+)', b)
            t = re.search(r'm_text: (.*)', b)
            ha = re.search(r'm_HorizontalAlignment: (\d+)', b)
            wm = re.search(r'm_TextWrappingMode: (\d+)', b)
            raw = (t.group(1).strip() if t else "")
            if raw.startswith('"') and raw.endswith('"'): raw = raw[1:-1]
            esc = chr(92) + "u"
            if esc in raw:
                try: raw = raw.encode("latin-1", "backslashreplace").decode("unicode_escape")
                except Exception: pass
            tmp[ow] = dict(size=float(fs.group(1)) if fs else 0, text=raw,
                           halign=int(ha.group(1)) if ha else 2,
                           wrap=int(wm.group(1)) if wm else 1)
    return go, rt, img, tmp, layout


def sprite_names():
    """guid → 스프라이트 이름, 이름 → 경계(L,B,R,T). <b>둘 다 `.png.meta` 에서 읽는다.</b>"""
    name_of, border_of = {}, {}
    for sub in ("Buttons", "Frames"):
        d = os.path.join(PROJ, "Assets", "_Project", "Resources", "UI", sub)
        if not os.path.isdir(d): continue
        for f in os.listdir(d):
            if not f.endswith(".png.meta"): continue
            txt = open(os.path.join(d, f), encoding="utf-8").read()
            g = re.search(r'guid: ([0-9a-f]{32})', txt)
            if not g: continue
            nm = f[:-9]
            name_of[g.group(1)] = nm
            b = re.search(r'spriteBorder: \{x: ([-\d.]+), y: ([-\d.]+), '
                          r'z: ([-\d.]+), w: ([-\d.]+)\}', txt)
            # 메타는 (x=왼 · y=아래 · z=오른 · w=위) 순이다 — 이 파일의 (L,B,R,T) 와 같다.
            border_of[nm] = [float(b.group(i)) for i in (1, 2, 3, 4)] if b else [0, 0, 0, 0]
    return name_of, border_of


def resolve(rt, root_id):
    """
    UI_Root 아래 모든 RectTransform 의 화면 렉트(왼쪽위 원점)를 구한다.

    ⚠ <b>anchoredPosition 의 기준은 앵커 «중심»</b>이다 — 앵커가 벌어져 있으면
      (스트레치) 왼쪽/아래가 아니라 가운데를 기준으로 잰다. 왼쪽으로 잡으면
      스트레치된 자식이 통째로 <b>부모 폭의 절반만큼</b> 어긋난다(실측 오진의 원인).
    """
    out = {}
    def walk(rid, pw, ph, px, py, force=None):
        r = rt[rid]
        w = (r['amax'][0] - r['amin'][0]) * pw + r['sd'][0]
        h = (r['amax'][1] - r['amin'][1]) * ph + r['sd'][1]
        cx = px + (r['amin'][0] + r['amax'][0]) / 2.0 * pw
        cy = py + (1.0 - (r['amin'][1] + r['amax'][1]) / 2.0) * ph
        pivx = cx + r['pos'][0]
        pivy = cy - r['pos'][1]
        left = pivx - r['piv'][0] * w
        top = pivy - (1.0 - r['piv'][1]) * h
        if force:
            # ⚠ UI_Root(캔버스)의 렉트는 YAML 에 0 으로 적혀 있다 — CanvasScaler 가
            #   런타임에 정하기 때문이다. 기준 해상도로 못박아야 아래가 전부 맞는다.
            left, top, w, h = 0.0, 0.0, CANVAS[0], CANVAS[1]
        out[rid] = (left, top, left + w, top + h)
        for k in r['kids']:
            if k in rt: walk(k, w, h, left, top)
    walk(root_id, CANVAS[0], CANVAS[1], 0.0, 0.0, force=True)
    return out


def run():
    go, rt, img, tmp, layout = load_scene()
    name_of, border_of = sprite_names()

    # ★ 캔버스마다 따로 풀어서 <b>한 표에 합친다</b> — 좌표계(1920x1080)가 같으므로
    #   합쳐도 뒤섞이지 않고, 아래의 판·글자 훑기는 한 번만 돌면 된다.
    roots = {}
    rect = {}
    for name in ROOTS:
        rid = next((i for i, r in rt.items() if go[r['ow']] == name), None)
        if rid is None:
            print("  (건너뜀) %s 이 이 씬에 없다" % name)
            continue
        roots[rid] = name
        rect.update(resolve(rt, rid))
    if not roots:
        raise SystemExit("⚠ %s 을 하나도 못 찾았습니다." % " · ".join(ROOTS))

    # 그림이 깔린 판 = 경계가 있는 스프라이트를 쓰는 Image
    panels = []
    for rid, r in rt.items():
        if rid not in rect: continue
        guid = img.get(r['ow'])
        nm = name_of.get(guid) if guid else None
        if not nm: continue
        b = border_of.get(nm)
        if not b or b == [0, 0, 0, 0]: continue
        panels.append((rid, nm, b))

    def path(rid):
        parts = []
        cur = rid
        while cur in rt and cur not in roots:
            parts.append(go[rt[cur]['ow']]); cur = rt[cur]['father']
        return "/".join(reversed(parts))

    def descendants(rid):
        for k in rt[rid]['kids']:
            if k not in rt: continue
            yield k
            yield from descendants(k)

    from PIL import ImageFont, ImageDraw, Image as PILImage
    # ⚠ 폰트는 <b>프로젝트 안</b>의 것을 쓴다 — 볼트 경로를 박아 두면 볼트를 옮긴 PC 에서
    #   검사기가 통째로 죽는다(`vault_path.py` 가 생긴 이유와 같은 문제).
    FONT = os.path.join(PROJ, "Assets", "TextMesh Pro", "Fonts", "neodgm.ttf")
    _fc = {}
    def font(sz):
        sz = max(6, int(round(sz)))
        if sz not in _fc: _fc[sz] = ImageFont.truetype(FONT, sz)
        return _fc[sz]
    _draw = ImageDraw.Draw(PILImage.new("RGBA", (4, 4)))

    panel_by_id = {pid: (nm, b) for pid, nm, b in panels}

    def nearest_panel(cid):
        """가장 가까운 «그림 깔린 조상». 중첩된 판에서 중복 보고를 막는다."""
        cur = rt[cid]['father']
        while cur in rt:
            if cur in panel_by_id: return cur
            cur = rt[cur]['father']
        return None

    hits = []
    for cid, r in rt.items():
        if cid not in rect: continue
        ow = r['ow']
        if ow not in tmp: continue
        info = tmp[ow]
        pid = nearest_panel(cid)
        if pid is None: continue
        # ⚠ 레이아웃 그룹 <b>안</b>의 렉트는 씬 YAML 값이 «비어 있다» — 런타임에
        #   VerticalLayoutGroup / ContentSizeFitter 가 정하기 때문이다(액션 바 버튼이
        #   폭 0 으로 읽혔다). 정적으로는 못 재므로 건너뛴다.
        driven = False
        cur = rt[cid]['father']
        while cur in rt:
            if rt[cur]['ow'] in layout: driven = True; break
            cur = rt[cur]['father']
        if driven: continue
        nm, (L, B, R, T) = panel_by_id[pid]
        px0, py0, px1, py1 = rect[pid]
        safe = (px0 + L, py0 + T, px1 - R, py1 - B)

        cx0, cy0, cx1, cy1 = rect[cid]
        boxw = cx1 - cx0
        f = font(info['size'])
        # ★ <b>렉트가 아니라 «그려지는 글자» 로 잰다.</b> 버튼 라벨은 렉트가 장식까지
        #   덮지만 가운데 정렬이라 글자는 안 잘린다 — 렉트로 재면 전부 가짜 양성이다.
        #   ⚠ 다만 <b>글이 비어 있거나 줄바꿈이 켜진</b> 칸은 렉트 그대로 재야 한다 —
        #     본문·설명은 씬에 빈 문자열로 저장되고 런타임에 채워지며(스킬 설명이 그렇다),
        #     줄바꿈이 켜진 글은 <b>칸을 가득 채운다</b>. 이 둘을 «글자가 짧다» 고 넘기면
        #     정작 고쳐야 할 본문이 통째로 빠진다.
        line = info['text'].split('\n')[0].replace('<b>', '').replace('</b>', '')
        if not line or info['wrap']:
            tw = boxw
        else:
            tw = _draw.textlength(line, font=f)
        ha = info['halign']
        if ha == 1:   tx0 = cx0
        elif ha == 4: tx0 = cx1 - tw
        else:         tx0 = cx0 + (boxw - tw) / 2
        tx1 = tx0 + tw
        th = info['size'] * 1.25
        ty0 = cy0 + max(0.0, ((cy1 - cy0) - th) / 2)
        ty1 = ty0 + th

        over = []
        if tx0 < safe[0] - 0.5: over.append("왼 %.0f" % (safe[0] - tx0))
        if ty0 < safe[1] - 0.5: over.append("위 %.0f" % (safe[1] - ty0))
        if tx1 > safe[2] + 0.5: over.append("오 %.0f" % (tx1 - safe[2]))
        if ty1 > safe[3] + 0.5: over.append("아래 %.0f" % (ty1 - safe[3]))
        if over:
            hits.append((path(pid), nm, (L, B, R, T), path(cid), info, over, None, None))

    print(f"그림 깔린 판 {len(panels)}개 · 글자 삐져나옴 {len(hits)}건")
    print("")
    last = None
    for p, nm, b, c, info, over, cr, safe in sorted(hits):
        if p != last:
            print(f"■ {p}  [{nm} 경계 L{b[0]} B{b[1]} R{b[2]} T{b[3]}]")
            last = p
        print(f"    {c:44s} {', '.join(over):22s} {info['size']:.0f}pt  {info['text']}")


if __name__ == "__main__":
    run()
