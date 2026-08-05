# Tools

Unity 밖에서 도는 보조 스크립트. `Assets/` 밖이라 Unity 가 임포트하지 않는다.

## wall_depth_pass.py — 벽 타일 3/4 뷰 입체 음영

```bash
python Tools/wall_depth_pass.py
```

`_ArtBackup/OrganicTilemap_20260804/` 의 **원본**을 읽어
`Assets/_Project/Art/OrganicTilemap/OrganicTilemap/` 에 음영을 다시 입힌 결과를 쓴다.

**항상 원본을 입력으로 삼으므로 몇 번을 다시 돌려도 결과가 같다(멱등).**
파일 상단 상수(`TOP_TARGET` / `FACE_GRAD` / `LIP_GAIN` / `MUL_SHADOW` / `BAND`)를
고쳐 다시 실행하면 되고, 원본이 망가질 일이 없다.

원상복구: `_ArtBackup/OrganicTilemap_20260804/Wall_*.png` 를 Assets 쪽에 덮어쓰면 된다.

이 PNG 들은 git 에 없으므로(untracked) **`_ArtBackup/` 이 유일한 원본이다. 지우지 말 것.**

무엇을 고치는지는 `진행상황.md` 20절 참조.

## wall_extrude_pass.py — 벽을 2칸 높이(20x40)로 새로 그리기

```bash
python Tools/wall_extrude_pass.py
```

`_ArtBackup/OrganicTilemap_20260804/Wall_Outer_20px.png`(원본, 20x20)을 읽어
`Assets/.../Wall_Outer_20px.png` 를 **20x40(2칸 높이)** 로 다시 그리고, 같은 파일의
`.meta` 안 32개 스프라이트 rect/alignment 도 함께 갱신한다.

**핵심: `internalID`/`spriteID`/이름은 절대 바꾸지 않는다.** 그래야 이미 만들어진
32개 Tile 에셋(`Tiles/Wall_Outer_*.asset`)의 `m_Sprite` 참조가 안 끊긴다 — Tile
에셋을 다시 만들거나 타일셋을 재임포트할 필요가 없다.

**Wall_Inner(내부 채움)는 건드리지 않는다** — 사방이 벽인 칸은 정면이 안 보이므로
20x20 그대로 둔다.

이 스크립트도 `wall_depth_pass.py` 와 같은 규칙: 원본(백업)에서 읽어 Assets 로 쓰므로
몇 번을 다시 돌려도 결과가 같다(멱등). 상수(`FACE` 딕셔너리의 `lip`/`top`/`bot`,
`SHADOW_MUL` 등)를 고쳐 재실행하면 톤을 조정할 수 있다.

이 스크립트를 실행하면 **`Tilemap_Obstacle` 의 `TilemapRenderer.mode` 를 반드시
Individual 로 맞춰야 한다**(Chunk 로는 타일이 칸을 넘어 겹치는 걸 그릴 수 없다).
`sortOrder` 는 `TopLeft` 그대로 두면 된다(위→아래 순서로 그려져 남쪽이 자동으로
위에 덮인다 — 이미 그렇게 되어 있었다).

무엇을 왜 이렇게 했는지는 `진행상황.md` 21절 참조.
