# itch.io 페이지 세팅 — Last Sanctuary

`itchio페이지 가이드.pptx` 의 세 장짜리 와이어프레임을 그대로 따랐다.
**프로젝트에 이미 있는 리소스와 기존 스크린샷만** 썼고, 새로 그린 그림은 없다.

```
1장  게임 배너 / 웹 버전 게임 플레이
2장  영상 · 이미지(넘기기) · 다운로드 버튼 · 개요 3~4줄 · 핵심 시스템 3 + 한 줄 설명
3장  개요 · 핵심 루프 3 + 한 줄 설명
```

| 와이어프레임 칸 | 지금 상태 |
|---|---|
| 게임 배너 | ✅ `images/banner_1920x480.png` |
| 웹 버전 게임 플레이 | ⬜ **비움** — WebGL 빌드 나오면 Uploads 에 올린다 |
| 영상 | ⬜ **비움** — 트레일러 나오면 본문 주석 한 줄 풀면 된다 |
| 이미지(넘기기 가능) | ✅ `images/screenshot_01~06` → itch 의 Screenshots 칸 |
| 다운로드 버튼 | ⬜ **비움** — 윈도우 빌드 zip 올리면 자동 생성 |
| 개요 3~4줄 | ✅ 본문 `<h2>개요</h2>` |
| 핵심 시스템 3 + 설명 | ✅ `images/system_01~03` + 한 줄씩 |
| 핵심 루프 3 + 설명 | ✅ `images/loop_01~03` + 한 줄씩 |

---

## 1. Details 탭에 넣을 값

| 칸 | 값 |
|---|---|
| Title | `Last Sanctuary` |
| Project URL | `last-sanctuary` |
| Short description | 조작하지 않고 지휘한다. 무너지는 성역에서, 동료가 버티는 것은 그 정신이 버티는 만큼이다. |
| Classification | Games |
| Kind of project | 지금은 **Downloadable** → 웹 빌드 올리면 **HTML** 로 바꾼다 |
| Release status | In development |
| Pricing | No payments (무료) |
| Genre | Strategy |
| Tags | `tower-defense` `real-time-strategy` `dark-fantasy` `roguelite` `pixel-art` `singleplayer` `atmospheric` `unity` `korean` |
| Custom noun | game |
| Community | Comments |
| Visibility | 다 채우기 전까지 **Draft** |

영어 페이지를 따로 쓸 거면 Short description 은
`You don't control them — you command them. In the falling sanctuary, they hold only as long as their minds do.`

## 2. 본문(Description)

- 한국어 → `description_ko.html`
- 영어 → `description_en.html`

Description 칸 오른쪽 위 `</>` 를 눌러 HTML 소스 모드로 바꾸고 통째로 붙여 넣는다.
`IMG_배너파일이름` 같은 자리표시자는 에디터 이미지 버튼으로 `images/` 안의 파일을 올린 뒤,
itch 가 만들어 준 `img.itch.zone/...` 주소로 바꿔 넣으면 된다.

> itch 는 `style` · `class` 속성을 지운다. 본문에 색이나 여백을 넣으려 하지 말고
> 색은 아래 3번의 Theme 에서 잡는다.

## 3. 이미지

| 파일 | 크기 | 올리는 곳 |
|---|---|---|
| `cover_630x500.png` | 630×500 | Cover image (필수) |
| `banner_1920x480.png` | 1920×480 | 본문 맨 위 |
| `screenshot_01_title.png` | 1920×1080 | Screenshots — 타이틀 |
| `screenshot_02_prep.png` | 1920×1080 | Screenshots — 웨이브 1 정비 |
| `screenshot_03_march.png` | 1920×1080 | Screenshots — 웨이브 7 진군 |
| `screenshot_04_tactics.png` | 1920×1080 | Screenshots — 전술 지침 |
| `screenshot_05_growth.png` | 1920×1080 | Screenshots — 캐릭터 성장 |
| `screenshot_06_battle.png` | 1920×1080 | Screenshots — 근접 전투 |
| `system_01_tactics.png` | 1200×675 | 본문 · 핵심 시스템 1 |
| `system_02_growth.png` | 1200×675 | 본문 · 핵심 시스템 2 |
| `system_03_erosion.png` | 1200×675 | 본문 · 핵심 시스템 3 |
| `loop_01_prep.png` | 1200×675 | 본문 · 핵심 루프 1 |
| `loop_02_battle.png` | 1200×675 | 본문 · 핵심 루프 2 |
| `loop_03_expand.png` | 1200×675 | 본문 · 핵심 루프 3 |
| `art_nexus.png` `art_mind.png` `art_fog.png` `art_opening_01.png` `art_opening_03.png` | 원본 그대로 | 남는 분위기 컷 — 필요할 때 |

전부 `build_images.py` 가 만든다. 원본을 고쳤으면 프로젝트 루트에서 다시 돌리면 된다.

```bash
python Docs/itchio/build_images.py
```

가져다 쓴 원본:
`Assets/_Project/Resources/UI/Lobby/LobbyBg.png` · `LobbyTitle.png`,
`Resources/EventBg/*`, `Resources/Opening/*`,
그리고 `C:\Users\user\Pictures\Screenshots` 의 인게임 캡처 6장.

## 4. Theme 탭 (게임 화면 색을 그대로 가져왔다)

| 칸 | 값 |
|---|---|
| Background | `#050508` |
| Base text | `#BEC8CD` |
| Link | `#73F2C7` |
| Border | `#3D5468` |
| Button background | `#212B38` |
| Button text | `#BEC8CD` |
| Theme | Dark |

체력 초록 `#66D985`, 침식 자홍 `#D053CE` 도 같은 화면에서 뽑은 값이다. 배지나 강조에 쓰면 된다.

## 5. 남은 일

1. **웹 빌드** — WebGL zip 을 Uploads 에 올리고 *This file will be played in the browser* 체크.
   Embed options 는 Viewport `1920 × 1080`, *Click to launch in fullscreen* 켬,
   *Mobile friendly* 끔, *Automatically start on page load* 끔.
   그다음 Kind of project 를 **HTML** 로 바꾼다.
2. **트레일러** — 유튜브에 올린 뒤 본문 위쪽 `<iframe>` 주석 한 줄을 풀고 VIDEO_ID 만 바꾼다.
3. **다운로드 빌드** — 윈도우 zip 을 올리면 다운로드 버튼은 알아서 생긴다.
4. **크레딧** — 두 본문 파일 맨 아래 `TODO` 자리에 팀 이름과 파트별 이름을 적는다.
5. 다 되면 Visibility 를 **Public** 으로.
