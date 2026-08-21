# -*- coding: utf-8 -*-
"""환경 설정 창에 「게임 재시작」 버튼을 하나 <b>씬에 실물로</b> 만든다 (2026-08-21 3차).

유저 지시: *"환경설정 UI에서 '저장하고 로비로 돌아가기' 아래에 게임 재시작 버튼 하나 추가로
만들어서 그 버튼 누르면 게임이 처음으로 초기화되는 기능 만들어줘"*.

★★ <b>왜 파이썬이 씬 YAML 을 건드리나</b> — 이 프로젝트의 규칙은 «오브젝트는 MCP 로 하이라키에
======================================================================
직접 만든다»(준수사항 §10 H-1) 이다. 그런데 이 세션에는 <b>유니티가 꺼져 있어</b>
MCP 브리지(포트 8090)가 없다. 두 갈래뿐이었다:

  ① 런타임에 코드가 <c>Instantiate</c> 로 만든다 — 하이라키에 <b>실물이 안 남는다</b>.
     인스펙터에서 위치·색·글자를 못 만진다(H-1 이 막으려는 바로 그 상태다).
  ② <b>씬 YAML 에 직접 적는다</b> — 실물이 남고 인스펙터에서 만질 수 있다.

②를 골랐다. 대신 <b>손으로 적지 않는다</b> — 이미 있는 「저장하고 로비로 돌아가기」 버튼
(<c>Body/LobbyButton</c>)의 <b>블록을 통째로 복제</b>하고 fileID 만 새로 딴다. 그래서
판 색·글꼴·크기·Button 전이색이 <b>옆 버튼과 한 톨도 다르지 않다</b>.

★ <b>글꼴을 다시 구울 필요가 없다</b>(유저 지시 2026-08-18: *"폰트는 네오 둥근모 베이크"*) —
  복제본의 <c>m_fontAsset</c> 이 원본과 같은 네오 둥근모 SDF 를 가리킨다. 새 TMP 를 «만드는»
  것이 아니라 «베낀» 것이라 이 프로젝트가 네 번 겪은 «새 글자만 Liberation Sans» 함정을
  아예 지나간다.

⚠ <b>유니티를 끄고 돌릴 것.</b> 에디터가 씬을 메모리에 들고 있으면 저장하는 순간 이 편집이
  통째로 덮인다(123-12절이 겪은 그 사고). 켜져 있었다면 씬을 <b>다시 열어야</b> 반영된다.

⚠ <b>여러 번 돌려도 안전하다</b> — 이미 <c>RestartButton</c> 이 있으면 아무것도 안 하고 나간다.

무엇을 하나
-----------
1. <c>UI_Root/HUD_Settings/Body/LobbyButton</c> 의 <b>하위 트리 전체</b>
   (GameObject·RectTransform·Image·Button·CanvasRenderer + 자식 <c>Label</c>)를 복제한다.
2. 복제본 안에서만 fileID 를 새로 딴다. 밖을 가리키는 참조(글꼴·스크립트 guid·부모)는 그대로.
3. 이름을 <c>RestartButton</c>, 글자를 «게임 재시작» 으로 바꾼다.
4. <c>Body</c> 의 자식 목록에서 <b>LobbyButton 바로 뒤</b>에 끼운다.
5. 세로 자리를 다시 잡는다 — 새 버튼 y=-112, 그 아래의 <c>Volume</c>·<c>Status</c> 를
   56px 씩 내린다. 창(520x430)은 <b>안 키운다</b>: Status 아래에 76px 이 비어 있고
   내린 뒤에도 <c>Copyright</c> 와 20px 이 남는다(실측).

사용법:  python Tools/scene_add_restart_button.py
"""

import io
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
SCENE = os.path.join(PROJECT, "Assets", "Scenes", "Proto_01.unity")

#: 베낄 원본과 새로 만들 이름.
SOURCE_PATH = ("HUD_Settings", "Body", "LobbyButton")
NEW_NAME = "RestartButton"

#: 새 버튼의 글자. TMP 의 <c>m_text</c> 에 <b>\\uXXXX 로 이스케이프</b>해서 넣는다 —
#: 씬의 다른 한글이 전부 그 모양이라 형식을 맞춘다(diff 가 지저분해지지 않는다).
NEW_LABEL = "게임 재시작"

#: 세로 자리 (Body 는 위에서부터 손으로 잡은 좌표를 쓴다 — 레이아웃 그룹이 없다).
NEW_Y = -112.0
SHIFT_BELOW = -56.0          #: 새 버튼 아래에 있던 것들을 이만큼 내린다
SHIFT_TARGETS = ("Volume", "Status")

BLOCK_RE = re.compile(r"^--- !u!(\d+) &(\d+)(.*)$")


# ---------------------------------------------------------------------------
#  YAML 을 «블록» 단위로만 다룬다 — 파서를 쓰지 않는다(38MB 이고 유니티 방언이다).
# ---------------------------------------------------------------------------

class Block(object):
    __slots__ = ("cls", "fid", "header", "body")

    def __init__(self, cls, fid, header, body):
        self.cls = cls          # 클래스 번호 (1 = GameObject · 224 = RectTransform …)
        self.fid = fid          # fileID
        self.header = header    # "--- !u!224 &2368638" (+ stripped 옵션)
        self.body = body        # 헤더 다음 줄부터의 본문 (줄 리스트)

    def text(self):
        return self.header + "\n" + "\n".join(self.body)


def read_scene():
    with io.open(SCENE, encoding="utf-8", newline="") as f:
        raw = f.read()
    crlf = "\r\n" in raw
    lines = raw.replace("\r\n", "\n").split("\n")
    return lines, crlf


def split_blocks(lines):
    """머리말(%YAML …)과 블록 목록으로 가른다."""
    preamble = []
    blocks = []
    current = None
    for line in lines:
        m = BLOCK_RE.match(line)
        if m:
            current = Block(int(m.group(1)), m.group(2), line, [])
            blocks.append(current)
            continue
        if current is None:
            preamble.append(line)
        else:
            current.body.append(line)
    return preamble, blocks


def field(block, name):
    """블록 본문에서 ``  name: 값`` 을 찾아 값 문자열로 돌려준다(들여쓰기 2칸짜리만)."""
    prefix = "  " + name + ":"
    for line in block.body:
        if line.startswith(prefix):
            return line[len(prefix):].strip()
    return None


def fid_of(value):
    """``{fileID: 123}`` 에서 123 을 뽑는다."""
    if not value:
        return None
    m = re.search(r"fileID:\s*(-?\d+)", value)
    return m.group(1) if m else None


def children_of(transform):
    """RectTransform 의 ``m_Children`` 에 적힌 fileID 목록."""
    out = []
    inside = False
    for line in transform.body:
        if line.startswith("  m_Children:"):
            inside = True
            if line.strip().endswith("[]"):
                return []
            continue
        if inside:
            m = re.match(r"\s+- \{fileID: (-?\d+)\}", line)
            if m:
                out.append(m.group(1))
                continue
            inside = False
    return out


# ---------------------------------------------------------------------------

def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    print("[환경설정 · 게임 재시작 버튼]")

    lines, crlf = read_scene()
    preamble, blocks = split_blocks(lines)
    by_fid = dict((b.fid, b) for b in blocks)

    go_of_transform = {}       # transform fileID → GameObject 블록
    transform_of_go = {}       # GameObject fileID → transform 블록
    for b in blocks:
        if b.cls in (4, 224):
            go = fid_of(field(b, "m_GameObject"))
            if go:
                go_of_transform[b.fid] = by_fid.get(go)
                transform_of_go[go] = b

    def name_of_transform(tfid):
        go = go_of_transform.get(tfid)
        return field(go, "m_Name") if go else None

    # ── ① 원본 찾기 — HUD_Settings ▸ Body ▸ LobbyButton ──────────────────
    root = None
    for b in blocks:
        if b.cls == 1 and field(b, "m_Name") == SOURCE_PATH[0]:
            root = transform_of_go.get(b.fid)
            break
    if root is None:
        raise SystemExit("⚠ 씬에서 %s 를 찾지 못했습니다." % SOURCE_PATH[0])

    node = root
    for want in SOURCE_PATH[1:]:
        found = None
        for c in children_of(node):
            if name_of_transform(c) == want:
                found = by_fid[c]
                break
        if found is None:
            raise SystemExit("⚠ %s 아래에서 %s 를 찾지 못했습니다." % (name_of_transform(node.fid), want))
        parent = node
        node = found
    source, body = node, parent

    # 이미 있으면 아무것도 안 한다(멱등).
    for c in children_of(body):
        if name_of_transform(c) == NEW_NAME:
            print("  이미 %s 가 있습니다 — 아무것도 하지 않았습니다." % NEW_NAME)
            return 0

    # ── ② 복제할 fileID 모으기 (하위 트리 전체) ──────────────────────────
    def collect(tfid, out):
        out.add(tfid)
        go = go_of_transform.get(tfid)
        if go is not None:
            out.add(go.fid)
            for line in go.body:
                m = re.match(r"\s+- component: \{fileID: (-?\d+)\}", line)
                if m:
                    out.add(m.group(1))
        for c in children_of(by_fid[tfid]):
            collect(c, out)

    subtree = set()
    collect(source.fid, subtree)
    print("  원본 %s — 블록 %d개 복제" % ("/".join(SOURCE_PATH), len(subtree)))

    # ── ③ 새 fileID 발급 — 쓰이지 않는 번호를 순서대로 ────────────────────
    used = set(by_fid.keys())
    remap = {}
    nxt = 7300000
    for old in sorted(subtree, key=lambda s: int(s)):
        while str(nxt) in used:
            nxt += 1
        remap[old] = str(nxt)
        used.add(str(nxt))
        nxt += 1

    def swap(text):
        """복제본 안의 참조만 바꾼다 — 밖(부모·글꼴·스크립트 guid)은 그대로 둔다."""
        def sub(m):
            old = m.group(1)
            return "{fileID: %s}" % remap.get(old, old)
        return re.sub(r"\{fileID: (-?\d+)\}", sub, text)

    # ── ④ 블록 복제 ──────────────────────────────────────────────────────
    new_blocks = []
    for old in sorted(subtree, key=lambda s: int(s)):
        b = by_fid[old]
        header = "--- !u!%d &%s" % (b.cls, remap[old])
        clone = Block(b.cls, remap[old], header, [swap(l) for l in b.body])
        new_blocks.append(clone)

    clone_by_fid = dict((b.fid, b) for b in new_blocks)
    new_root = clone_by_fid[remap[source.fid]]                      # RectTransform
    new_go = clone_by_fid[remap[go_of_transform[source.fid].fid]]   # GameObject

    # 이름
    for i, line in enumerate(new_go.body):
        if line.startswith("  m_Name:"):
            new_go.body[i] = "  m_Name: " + NEW_NAME
            break

    # 세로 자리
    for i, line in enumerate(new_root.body):
        if line.startswith("  m_AnchoredPosition:"):
            new_root.body[i] = re.sub(r"y: -?[\d.]+", "y: %g" % NEW_Y, line)
            break

    # 글자 — TMP 의 m_text 를 \uXXXX 로 적는다(씬의 다른 한글과 같은 형식).
    escaped = "".join("\\u%04X" % ord(ch) if ord(ch) > 127 else ch for ch in NEW_LABEL)
    hit = 0
    for b in new_blocks:
        for i, line in enumerate(b.body):
            if line.startswith("  m_text:"):
                b.body[i] = '  m_text: "%s"' % escaped
                hit += 1
    if hit != 1:
        raise SystemExit("⚠ m_text 를 %d개 찾았습니다 — 1개여야 합니다." % hit)

    # ── ⑤ Body 자식 목록에 끼우기 (LobbyButton 바로 뒤) ──────────────────
    inserted = False
    for i, line in enumerate(body.body):
        if line.strip() == "- {fileID: %s}" % source.fid:
            body.body.insert(i + 1, "  - {fileID: %s}" % new_root.fid)
            inserted = True
            break
    if not inserted:
        raise SystemExit("⚠ Body 의 m_Children 에서 원본 자리를 찾지 못했습니다.")

    # ── ⑥ 아래에 있던 것들을 내린다 ──────────────────────────────────────
    moved = []
    for c in children_of(body):
        nm = name_of_transform(c)
        if nm not in SHIFT_TARGETS:
            continue
        t = by_fid[c]
        for i, line in enumerate(t.body):
            if line.startswith("  m_AnchoredPosition:"):
                m = re.search(r"y: (-?[\d.]+)", line)
                y = float(m.group(1)) + SHIFT_BELOW
                t.body[i] = re.sub(r"y: -?[\d.]+", "y: %g" % y, line)
                moved.append("%s→%g" % (nm, y))
                break

    # ── ⑦ 쓰기 ───────────────────────────────────────────────────────────
    out = list(preamble)
    for b in blocks:
        out.append(b.text())
    for b in new_blocks:
        out.append(b.text())

    text = "\n".join(out)
    if not text.endswith("\n"):
        text += "\n"          # 원본은 개행으로 끝난다 — 블록을 뒤에 붙이면서 잃기 쉽다
    if crlf:
        text = text.replace("\n", "\r\n")
    with io.open(SCENE, "w", encoding="utf-8", newline="") as f:
        f.write(text)

    print("  %s 추가 — fileID %s · y=%g" % (NEW_NAME, new_root.fid, NEW_Y))
    print("  아래로 민 것: " + (" · ".join(moved) if moved else "없음"))
    print()
    print("  ⚠ 유니티가 켜져 있었다면 씬을 <b>다시 열어야</b> 보입니다(맨 위 ⚠).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
