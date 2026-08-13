# UI 브랜치 작업로그

> 이 브랜치에서 한 작업의 기록. 머지 시 `진행상황.md` 의 **30절~** 로 편입된다
> (형식 유지, 요약 금지 — `머지 계획.md` §7).
> 절 번호는 `UI-1` 부터 순서대로. PROTO 는 `PROTO-1~` / 진행상황 24~29절을 쓴다.

---

## UI-1. HUD 6개 패널 + 집결지 + 캐릭터 생성 (2026-08-05)

### 무엇을 / 왜

유저 확정 스코프: **좌상단 캐릭터 로스터(상태 + 선택 기능) · 그 아래 로그라인 · 우 최상단 에너지 ·
그 아래 캐릭터 생성/집결지 설정/캐릭터 강화 · 중앙 상단 웨이브 단계와 남은 타이머 · 우하단 미니맵.**
목업(`Last Sanctuary HUD.html`)의 나머지 패널(전술 명령·식량·스테이터스 상세)은 이번 범위 밖.

지금까지 게임 상태를 확인할 수단이 콘솔 로그뿐이었다(진행상황 9절 6번). 웨이브 타이머·자원·
캐릭터 스탯이 전부 로그로만 보여서 밸런싱 판단이 어려웠다.

### 어떻게

**오브젝트는 MCP 로 하이라키에 직접 생성했다** (준수사항 §10 H-1, 유저 확정).
처음엔 38MB 씬 재작성을 피하려고 런타임 코드 생성으로 만들려 했으나, 유저가 "하이라키에 실물이
있어야 인스펙터에서 조정할 수 있다"는 이유로 MCP 직접 생성으로 확정했다. 씬 저장 비용은
**계층을 다 만든 뒤 `save_scene` 1회**(H-8)로 관리했다.

계층 (전부 `UI_Root` 아래, 기존 캔버스 재사용 — 1920×1080 Match 0.5):
```
UI_Root
├─ HUD_Roster    (좌상단 360x320)   CharacterRosterPanel
│   ├─ Title · List(VerticalLayoutGroup)
│   └─ RowTemplate ★모체(비활성) — Name / Duty / HpBack>HpFill / Stats / RowUpgrade>Label
├─ HUD_Log       (좌측 360x240)     BattleLogPanel
│   ├─ Title · Lines(VerticalLayoutGroup)
│   └─ LineTemplate ★모체(비활성)
├─ HUD_Wave      (중앙상단 440x92)  WaveStatusPanel — Phase / Timer
├─ HUD_Energy    (우최상단 260x56)  ← 기존 Energy(EnergyLabel) 를 이 안으로 이동
├─ HUD_Actions   (우측 260x164)     ActionPanel
│   └─ Buttons — CreateButton / RallyButton / Upgrade(기존 UpgradeButtonUI 이동)
├─ HUD_Minimap   (우하단 300x322)   MinimapPanel — Title / View(RawImage)
└─ RallyMarkerTemplate ★모체(비활성)
GameSystems  ← RallyPointService · CharacterCreationService 추가 (기존 UnitSelector·CharacterUpgradeService 옆)
```
★ 표시가 **템플릿 복제 예외**(§10 H-2) — 모체 하나만 MCP 로 만들고 개수가 런타임에 정해지는
반복 요소는 스크립트가 `Instantiate` 로 복제한다. 유닛 템플릿 복제 패턴(진행상황 5절)과 같은 모양.

**기존 UI 를 재사용했다**: `EnergyLabel`(에너지 표시)과 `UpgradeButtonUI`(강화 버튼)는 이미
있던 것이라 새로 만들지 않고 새 레이아웃 안으로 옮겼다(§10 H-3). 같은 기능이 두 벌이 되면
어느 쪽이 정본인지 알 수 없어진다.

신규 스크립트 (`Assets/_Project/Scripts/UI/`):
| 파일 | 역할 |
|---|---|
| `CharacterRosterPanel.cs` | 캐릭터별 행. 이름/HP바/4능력치/현재 행동, 행 클릭 = 선택 + **카메라 이동**, 행 안 강화 버튼 |
| `BattleLogPanel.cs` | 처치·에너지·생성·강화 이벤트를 줄로 표시. 줄 재활용 |
| `WaveStatusPanel.cs` | 웨이브 번호·단계·타이머 |
| `MinimapPanel.cs` | 지형 + 안개 + 캐릭터/넥서스 + **소환 경보 점멸** |
| `ActionPanel.cs` | 캐릭터 생성 / 집결지 설정 버튼 |
| `RallyPointService.cs` | 집결지 지정 모드 · 저장 · 마커 |
| `CharacterCreationService.cs` | 생성 비용 규칙 + `UnitSpawner.SpawnOneCharacter()` 호출 |
| `HudLog.cs` | 로그 한 줄을 남기는 정적 통로 (남기는 쪽 ↔ 보여주는 쪽 분리) |
| `HudTheme.cs` | 스크립트가 런타임에 칠하는 색 상수 (HP 바, 선택 하이라이트, 미니맵 픽셀) |

수정한 기존 파일:
- `Units/CharacterBehavior.cs` — `CharacterDuty.Rally` 추가 + 집결지 우선 로직 + `rallyLeash` 필드
- `Units/UnitSpawner.cs` — (시드 커밋에서) `SpawnOneCharacter()` / `OnCharacterSpawned`

### 설계 판단 — 왜 이렇게 했는지

- **집결지는 이동 코드를 새로 짜지 않았다.** `UnitCombat` 은 타겟이 없으면 귀환 지점으로 걸어가므로,
  `CharacterBehavior` 가 귀환 지점을 집결지로 갈아끼우는 것으로 끝난다 — 진행상황 12절이 정찰/방어에
  쓰던 방식 그대로다. 이동 로직이 두 벌로 갈라지지 않는다.
- **집결지 우선순위**: 교전 중 > 집결 > 정찰/방어. 플레이어의 명시적 명령이므로 자율 이동보다 위,
  다만 이미 붙어 싸우는 중이면 건드리지 않는다(기존 가드 재사용).
- **대상 규칙**: 선택된 캐릭터가 있으면 그 캐릭터만, 없으면 전체. 전체 지정은 개별 지정을 지운다 —
  규칙이 두 벌로 갈리면 "왜 얘만 안 가지"가 된다.
- **캐릭터 생성 비용은 `ResourceManager` 가 아니라 `CharacterCreationService` 에 뒀다.**
  `CharacterUpgradeService` ↔ `UpgradeButtonUI` 와 같은 "규칙과 입력 분리" 구조를 맞춘 것.
  **비용을 먼저 차감하고 성공했을 때만 생성**하며, 생성이 실패하면 에너지를 돌려준다.
- **로스터 행 클릭 = 카메라 이동**: `CameraRigController.FocusOn()` 이 이미 공개돼 있어 추가 API 가
  필요 없었다. 카메라가 아니라 `CameraAnchor` 리그를 움직이는 구조(진행상황 1·7절)를 그대로 따랐고,
  맵 경계 클램프도 리그가 처리한다. 인스펙터에서 즉시이동/부드러운이동을 고를 수 있다.
- **미니맵 소환 경보**: 웨이브 몬스터가 소환되면 **소환 지점만** 안개를 무시하고 빨간 원으로 점멸한다.
  "어디서 오는지"는 알려주되 몬스터의 실제 위치·진군 경로는 노출하지 않는다 — 안개의 의미를 지키면서
  대비 시간을 주는 절충. **웨이브 타이머가 돌기 시작하면(= 첫 전투로 `Battle` 진입) 멈춘다.**
  소환 지점은 `MonsterSpawner.Alive` 의 위치를 8타일 반경으로 뭉쳐서 구한다 — 스포너가 어느 게이트를
  썼는지 공개하지 않기 때문인데, 덕분에 **PROTO 소유인 스포너 코드를 전혀 건드리지 않았다**(준수사항 §2).
- **넥서스·캐릭터는 미니맵에서 안개와 무관하게 항상 표시**한다(아군이므로 위치를 안다). 몬스터는 표시하지
  않는다 — 표시하면 안개가 무의미해진다.
- **갱신 비용**: 로스터는 0.2초, 미니맵은 0.25초 주기. 웨이브/에너지는 값이 바뀔 때만 문자열을 다시 만든다
  (`EnergyLabel` 과 같은 이유 — 매 프레임 string 을 만들면 TMP 가 메시를 다시 굽는다).

### 겪은 함정

1. **`.gitattributes` 에 `*.unity binary` 를 쓰면 안 된다.** `binary` = `-text -diff -merge` 인데
   이 저장소는 `core.autocrlf=true` 이고 작업 트리 씬은 CRLF, blob 은 LF 다. `-text` 를 거는 순간
   38MB 파일 전체가 "수정됨"으로 뜨고, 무심코 `add` 하면 줄바꿈만 바뀐 38MB 커밋이 박힌다.
   → **`-merge -diff` 만** 주고, 적용 후 `git status` 로 씬이 깨끗한지 확인했다.
2. **MCP 로는 씬 오브젝트 참조를 인스펙터에 넣을 수 없다**(진행상황 8절 4번). 그래서 모든 패널이
   **필드가 비어 있으면 자식을 이름으로 찾도록** 했다 (`UpgradeButtonUI` 의 "비워두면 자동으로 찾는다"
   패턴 재사용). 인스펙터에서 직접 넣으면 그 값이 우선이다.
3. **비활성 오브젝트는 경로로 조회되지 않는다.** 템플릿 3개(RowTemplate/LineTemplate/RallyMarkerTemplate)는
   **전부 활성 상태로 만들어 자식까지 다 구성한 뒤, 맨 마지막에 비활성으로 껐다.** 순서를 바꾸면
   자식을 못 만든다.
4. **`GameObject.Find` 는 비활성을 못 찾지만 `Transform.Find` 는 찾는다.** `RallyPointService` 가
   비활성 마커 모체를 찾을 때 이 차이가 중요했다 — `GameObject.Find("UI_Root")` 로 활성 부모를 잡고
   거기서 `Transform.Find` 로 내려간다.
5. **폰트를 `Resources/` 로 옮겼다.** 원래는 런타임 생성 UI 가 인스펙터 참조를 못 가져서였는데,
   MCP 직접 생성으로 방침이 바뀐 뒤 **`update_component` 로 폰트 에셋 경로를 넣으면 정상 반영되는 것을
   확인**했다(`m_fontAsset: NeoDunggeunmo SDF`). Resources 경로는 그대로 두었다 — `HudTheme.Font` 가
   런타임에 폰트를 쓸 때 필요하고, 이동 시 `.meta` 를 같이 옮겨 GUID 가 유지되므로 씬의 기존 참조도
   그대로 살아있다. **폰트는 네오둥근모 고정**(유저 확정).

### 확인된 것
- `recompile_scripts` 에러·경고 0, 콘솔 에러 0.
- 계층 33개 오브젝트 생성 확인, 템플릿 3종 `activeSelf: false` 확인.
- TMP 폰트 참조가 실제로 `NeoDunggeunmo SDF` 로 붙은 것을 `get_gameobject` 로 확인.
- Edit mode 확인 후 `save_scene` **1회**. 씬 36.8MB.

### 아직 확인 못 한 것 (유저가 볼 것)
- **플레이 모드 검증 전혀 안 함** — 프로젝트 작업 방식대로 MCP 로 플레이 모드에 들어가지 않았다.
  다음을 에디터에서 직접 볼 것:
  - 6개 패널의 실제 위치·크기·글자 잘림 (특히 로스터 행 안의 요소 겹침)
  - 로스터 행 클릭 → 선택 + 카메라 이동
  - 캐릭터 생성 버튼 → 에너지 차감 + 실제 생성 + 로그
  - 집결지 설정 → 맵 클릭 → 캐릭터가 실제로 그 지점으로 가는지, 우클릭/Esc 해제
  - 미니맵 지형·안개·유닛 점, 그리고 **웨이브 소환 시 빨간 원 점멸 → 전투 시작 시 사라짐**
- **`Run In Background` 가 꺼져 있으면 타이머가 안 흐른다** — 웨이브 패널이 멈춰 보이면 이것부터 확인
  (진행상황 11절).
- 미니맵 갱신 비용(320×320 = 102,400 픽셀을 0.25초마다) 실측 안 함. 프레임이 튀면 `refreshInterval`
  을 늘리거나 안개 검사를 최적화할 것.
- 캐릭터 생성 비용(기본 30, 증가 15, 상한 12명)과 집결 목줄(6타일)은 **임의값** — 밸런싱 필요.

### 씬 변경 여부
**있음.** `UI_Root` 아래 HUD 계층 신설 + 기존 `Energy`/`Upgrade` 이동, `GameSystems` 에 서비스 2개 추가.
저장 1회. 커밋을 스크립트/문서와 분리했다(준수사항 U-S3).

### 씬반영요청 목록
없음 (이 브랜치가 씬 소유자).

---

## UI-2. 미니맵 빈 화면 수정 + 집결지를 "구역 경계"로 재설계 (2026-08-05)

### 무엇을 / 왜

유저 플레이테스트 피드백 2건:
1. **미니맵에 아무것도 안 보임** (지형·유닛·경보 전부 표시 안 됨).
2. **집결지를 찍으면 캐릭터가 그 자리에서 아무것도 안 하고 가만히 서 있음.**
   요청한 해결 방향:
   - 집결지 범위를 **10×10 구역**으로 (임시값)
   - 그 구역에서 **경계하며 수비**(방어와 같은 개념) — 한 점에 서 있지 않기
   - 집결지는 **미리 찍어둘 수 있지만**, 캐릭터가 실제로 이동하는 시점은 **웨이브 몬스터가
     소환된 직후**부터

### 어떻게

**① 미니맵 — 근본 원인은 존재하지 않는 캐시를 읽고 있었던 것**

`MapGenerator.Walkable` / `MapSize` / `Origin` 은 `Generate()` 안에서만 채워지는 **런타임 전용
캐시**다(직렬화 안 됨). 씬의 `MapGenerator.generateOnAwake` 는 **0(꺼짐)** — 맵을 에디터에서
미리 생성해 씬에 저장해두고, 플레이 모드에서는 다시 생성하지 않는 이 프로젝트의 작업 방식(상단
"작업 방식" 참조) 때문이다. 그래서 플레이 모드에 들어가면 `Generate()` 가 한 번도 안 불려
`Walkable` 이 계속 `null`이고, `MinimapPanel.EnsureTexture()` 가 매 프레임 실패해 **텍스처
자체가 만들어지지 않았다** — RawImage 가 빈 채로 남는다.

- [MinimapPanel.cs](Assets/_Project/Scripts/UI/MinimapPanel.cs) `EnsureTexture()` —
  `Walkable`/`MapSize`/`Origin` 대신 **씬에 직렬화되어 항상 유효한 값**을 쓰도록 교체:
  - 크기/원점: `MapGenerator.Config`(SO 에셋, `Generate()` 호출 여부와 무관하게 항상 있음)의
    `MapSize`/`Origin` 계산 프로퍼티
  - 통행 가능 여부: `MapGenerator.IsCellBlocked(cell)` (장애물 타일맵 기준 — 진행상황 8절이
    "이동 판정은 이 방식이 씬 재실행 후에도 유효하다"고 이미 적어둔 것과 같은 이유)
  - `FlowFieldService` 는 원래부터 이 방식을 쓰고 있었다 — `Walkable` 을 쓴 건 미니맵 쪽의
    실수였다.

**② 집결지 — "이동 명령"에서 "구역 방어 임무"로 재정의**

기존 문제의 정확한 원인: `UnitCombat` 은 목적지(귀환 지점)에 도착하면 **Idle 로 멈추고
스스로는 돌아다니지 않는다.** 방어(`PickGuardSpot`)가 "가만히 서 있지 않고 순찰하는 것처럼
보이는" 이유는 `CharacterBehavior` 가 도착·타임아웃마다 **새 순찰 지점을 계속 다시 뽑아주기**
때문인데, 기존 집결지 구현은 지정한 좌표를 **딱 한 번**만 목적지로 넣고 그 뒤로 재추첨을 안
했다 — 그래서 도착 후 그대로 멈춰 있었다.

- [CharacterBehavior.cs](Assets/_Project/Scripts/Units/CharacterBehavior.cs):
  - `CharacterDuty.Rally` 의 의미를 **"집결지로 이동"에서 "집결지 구역을 경계 순찰"**로 바꿨다.
    방어(`PickGuardSpot`)와 로직을 공유하도록 `PickSpotAround(center, halfExtent, extraLeash, square)`
    로 추출 — 넥서스 방어는 **원형**(기존 그대로, `square: false`), 집결은 요청한 "n×n 구역"을
    그대로 반영해 **정사각 균등 표본**(`square: true`)으로 나눴다. 폴백(길 막힘 시 근처 배치
    가능 칸 탐색)·재추첨 타이머·목줄 부여는 완전히 같은 코드를 공유한다.
  - 신규 필드 `rallyAreaSize`(기본 **10** — 유저 지시대로 10×10, 임시값). 반지름이 아니라
    "한 변 길이"로 노출해 유저가 의도한 단위 그대로 인스펙터에 보인다.
  - **반영 시점**: `Update()` 에서 `baseline`(정찰/방어 판정, 기존 `CurrentDuty()`)이
    **Guard 일 때만** 집결지를 확인해 `Rally` 로 바꾼다. `WaveManager` 는 대기시간이 끝나며
    몬스터를 소환하는 순간 바로 `Marching` 으로 넘어가므로(11절), "baseline 이 Guard 로
    바뀌는 시점 = 웨이브 몬스터 소환 직후"가 그대로 성립한다 — **소환 이벤트를 따로 구독하지
    않고도 요청한 트리거 시점이 정확히 맞아떨어진다.** 대기시간(정찰) 중에는 집결지가 있어도
    반영되지 않는다 — 미리 찍어두는 것과 실제로 가는 것을 분리해달라는 요청 그대로다.
  - 클래스 상단 문서에 이 트리거 시점의 근거를 남겨서, 다음에 이 코드를 보는 사람이 "왜 소환
    이벤트를 안 구독했지"라고 의심하지 않게 했다.

### 겪은 함정

1. **`out` 변수와 조건식을 분리된 문장에서 같이 쓰면 정의되지 않은 변수 오류가 난다.**
   `bool hasRally = baseline == Guard && TryGetRallyPoint(_character, out Vector3 rallyCenter);`
   처럼 써놓고 그 아래 다른 `if` 블록에서 `rallyCenter` 를 쓰면, 컴파일러는 "duty==Rally 면
   hasRally==true 였다"를 증명하지 못해 `CS0165` 를 낸다. `Vector3 rallyCenter = default;` 로
   먼저 선언해 초기화해두고 `out rallyCenter`(타입 재선언 없이)로 채우는 방식으로 고쳤다.
2. **미니맵처럼 "값이 있어야 동작한다"는 전제를 코드로 넣을 때, 그 값이 실제로 언제 채워지는지
   반드시 확인할 것.** `Walkable != null` 이라는 가드 자체는 방어적으로 보였지만, 정작 그 값을
   채워주는 `Generate()` 가 이 씬 설정에서는 플레이 모드 중 아예 안 불린다는 사실을 처음엔
   놓쳤다 — "null 체크가 있으니 안전하다"와 "그 값이 실제로 세팅된다"는 다른 문제다.

### 확인된 것
- `recompile_scripts` 에러·경고 0.
- `Walkable`/`MapSize`/`Origin` 을 참조하는 다른 코드가 없는지 전체 검색 — `FlowFieldService`
  의 설명 주석과 이번에 고친 `MinimapPanel` 두 곳(주석)뿐, 실제로 그 값을 읽어 쓰는 코드는
  이제 없다.

### 아직 확인 못 한 것 (유저가 볼 것)
- **여전히 플레이 모드 검증 안 함** — 미니맵이 실제로 지형을 그리는지, 집결지 구역에서 캐릭터가
  진짜로 돌아다니는지(멈춰서기 재발 여부), 웨이브 소환 직후에만 이동을 시작하는지 직접 확인 필요.
- `rallyAreaSize`(10) / `rallyLeash`(6) 는 여전히 **임의값** — 기획 확정 전까지 임시.
- 집결지를 **정찰(대기시간) 중에 지정**했을 때 마커가 미니맵/월드에 잘 보이는지는 확인했지만,
  실제 이동은 방어 임무로 넘어가기 전까지 안 일어나는 게 **의도**다 — "안 움직인다"고 오해하지
  않도록 유저에게 이 문서를 먼저 보여줄 것.

### 씬 변경 여부
없음 (스크립트만 수정).

### 씬반영요청 목록
없음.

---

## UI-3. 집결지 범위 표시 · HP 바 이벤트 반영 · 로스터 스크롤 (2026-08-05)

### 무엇을 / 왜

유저 요청 3건.
1. 집결지 마커 주변에 실제 범위(구역)를 눈으로 보이게 — 지정 모드로 들어가 마우스를 움직일 때도,
   실제로 찍은 뒤에도 둘 다.
2. HUD_Roster 의 HP 바가 몬스터에게 맞아도 안 줄어드는 것을 "줄어드는 게이지"로 확실히 만들 것.
3. HUD_Roster 를 세로로 더 길게(캐릭터가 늘어날 걸 감안), 넘치면 오른쪽 스크롤바로 드래그.
   그 결과 로그 창은 아래로 밀리는데, **로그의 새 위치는 미니맵의 Y값을 기준으로 그 바로 위**에
   두고, X 는 그대로 좌측 고정.

### 어떻게

**① 집결지 범위 표시 — `RallyPointService.cs`**
- `RallyAreaSize` 를 이 서비스의 **정본**으로 옮겼다. `CharacterBehavior` 는 더 이상 자기 필드를
  들고 있지 않고 `RallyPointService.Instance.RallyAreaSize` 를 그대로 읽는다(서비스가 없을 때만
  `rallyAreaSizeFallback` 폴백). **화면에 보이는 범위와 실제 순찰 범위가 항상 같아야** 값을 두
  곳에 따로 둘 수 없었다 — 하나가 정본, 나머지는 그걸 읽기만 하는 구조로 정리했다.
- 신규 템플릿 `RallyRangeTemplate`(반투명 정사각, 노란색 alpha 0.16, MCP 로 직접 생성)을
  `RallyMarkerTemplate` 과 같은 방식으로 풀링한다. `UpdateMarkers()` 를 `UpdateOverlay()` 로
  확장해 **마커(점)와 범위(사각형)를 같은 위치 목록으로 동시에 동기화**한다.
- **미리보기**: 지정 모드(`IsPicking`) 중에는 매 프레임 마우스가 가리키는 월드 위치를 `Snap()`
  까지 거쳐 계산해두고(`UpdatePreview()`), 그 위치를 활성 목록의 **인덱스 0**으로 얹는다. 확정된
  집결지와 구분되도록 `previewAlphaScale`(기본 0.55)만큼 옅게 그린다 — "아직 안 찍었다"는 걸
  알아볼 수 있게.
- **범위 사각형의 화면 크기 계산**: 카메라 줌이 바뀌면 같은 10타일이 차지하는 화면 픽셀 크기도
  바뀐다(진행상황 §11, "월드 공간 UI는 줌에 따라 크기가 튄다"). 그래서 매 프레임 `worldSize`
  만큼 떨어진 두 모서리를 각각 `WorldToScreenPoint` → `ScreenPointToLocalPointInRectangle` 로
  변환해 그 차이를 재는 방식(`WorldSizeToLocalSize`)을 썼다 — 줌·해상도·CanvasScaler 배율이
  전부 이 변환 두 번 사이에서 자동으로 상쇄되므로, "픽셀당 타일 수"를 직접 계산할 필요가 없다.

**② HP 바를 이벤트로 반영 — `CharacterRosterPanel.cs`**
- HP 표시가 폴링(0.2초 간격)에만 의존하고 있었다 — 로직 자체는 맞았지만(측정된 `Image` 컴포넌트
  값도 `Type.Filled/Horizontal/Left/1` 로 정확히 붙어 있었다), "정의해 줄어들게 표시"라는 요청에
  맞춰 **`DamageableUnit.OnHpChanged` 를 행마다 직접 구독**하는 방식으로 바꿨다. 이 이벤트는
  `ApplyDamage`/`Heal`/`SetupHealth`(강화 포함) 전부에서 발생하므로 HP 에 영향을 주는 모든
  경로를 하나도 놓치지 않는다.
- 행은 재활용되므로(캐릭터가 죽고 다른 캐릭터가 그 자리를 대신 씀), `Row.SubscribedUnit` 을 따로
  들고 있다가 `Rebuild()` 에서 캐릭터가 바뀌는 순간 **이전 구독을 정확히 끊고 새로 구독**한다.
  구독 즉시 `ApplyHp` 를 한 번 호출해 재구성 직후에도 최신 HP 가 바로 보이게 했다. `RefreshValues()`
  의 폴링 쪽에서는 HP 필드를 더 이상 건드리지 않는다 — 이벤트와 폴링이 같은 값을 이중으로 쓰면
  순서에 따라 잠깐 어긋나 보일 수 있어서다.
- `OnDestroy()` 에서 모든 행의 구독을 해제해 패널이 없어져도 죽은 델리게이트가 이벤트에 남지
  않게 했다.

**③ 로스터 스크롤 + 로그 재배치 — 씬(MCP) + 코드**
- `HUD_Roster` 높이 320 → **460**(위치는 그대로, 아래로만 늘림).
- 표준 uGUI 스크롤 구조를 그대로 따랐다: `ScrollView`(`ScrollRect`) → `Viewport`(`RectMask2D` +
  투명 `Image`, `raycastTarget=true` — 마스크 자체는 Graphic 이 아니라 드래그를 받으려면 필요) →
  `List`(기존 것을 **reparent**, `VerticalLayoutGroup` 은 그대로 두고 `ContentSizeFitter`
  Vertical=PreferredSize 추가 — 내용만큼 자동으로 커진다). `Scrollbar`(오른쪽 14px, `Handle` 자식)
  는 새로 만들었다. **`ScrollRect.content/viewport/verticalScrollbar`, `Scrollbar.handleRect/
  targetGraphic` 같은 오브젝트 참조 필드는 MCP 로 못 넣는다**(진행상황 8절 4번) — 그래서
  `CharacterRosterPanel.BindScrollRect()` 를 새로 만들어 `Start()` 에서 이름으로 찾아 코드로
  연결했다. `listRoot` 의 자동 탐색 경로도 `"List"` → `"ScrollView/Viewport/List"` 로 한 단계
  깊어진 걸 반영했다.
- 로그 위치는 **미니맵을 기준으로 재계산**했다: 미니맵은 우하단 고정(`anchoredPosition.y=16`,
  높이 322) → 미니맵 윗변 = 화면 아래로부터 `16+322=338`. 로그를 **아래-왼쪽 앵커**로 바꾸고
  (기존엔 위-왼쪽 앵커였다) `anchoredPosition = (16, 338+8[기존 여백과 같은 간격])`. X 는 그대로
  16(좌측 고정, 애초에 앵커가 왼쪽이라 값 자체는 안 바뀜) — 요청한 "미니맵의 y값만 가져오고 x는
  좌측 고정"을 그대로 구현했다. 로스터(위 16~476)와 로그(아래 494~734, 화면 위쪽 기준)가
  18px 여유를 두고 겹치지 않는 것도 계산으로 확인했다.

### 겪은 함정

1. **`RectTransform` 을 `anchoredPosition`+`sizeDelta` 와 `offsetMin`+`offsetMax` 로 동시에
   지정하면 안 된다.** 둘은 같은 내부 값을 표현하는 다른 방식일 뿐이라 서로 덮어쓸 수 있다
   (실제로 스크롤바 RT 를 처음에 `anchoredPosition`/`sizeDelta` 조합으로 대충 계산했다가, 다시
   `get_gameobject` 로 확인해보니 계산이 꼬여 있었다 — `offsetMin`/`offsetMax` 로 정리해서
   고쳤다). **한 axis 라도 스트레치 앵커(anchorMin≠anchorMax)면 그 axis 는 `offsetMin`/`offsetMax`
   로만 지정하는 편이 안전하다** — 포인트 앵커인 축도 offset 표현이 항상 유효하므로(앵커 지점
   기준 오프셋), 아예 RectTransform 은 전부 offsetMin/offsetMax 로 통일해서 쓰는 게 사고를 줄인다.
2. **object 참조 필드(ScrollRect/Scrollbar)는 MCP 로 절대 못 넣는다**는 걸 다시 확인했다(이미
   알던 제약이지만 컴포넌트를 추가하고 나서야 `get_gameobject` 로 `handleRect: null` 인 걸 보고
   실제로 체감했다). **object 참조가 있는 컴포넌트를 씬에 추가할 땐, 처음부터 "이건 코드에서
   이름으로 찾아 연결해야 한다"를 전제하고 시작할 것** — 나중에 알아채면 이미 만든 계층을 다시
   훑어야 한다.
3. **집결지 범위 크기를 두 컴포넌트에 각각 갖고 있으면 반드시 벌어진다.** `CharacterBehavior` 가
   `rallyAreaSize` 를 자기 필드로 갖고 있었는데, 화면에 표시하는 범위(`RallyPointService`)와
   값이 분리되어 있어서 인스펙터에서 한쪽만 고치면 "보이는 범위"와 "실제 순찰 범위"가 어긋난다.
   값을 하나로 합치고 한쪽이 다른 쪽을 읽게 고쳤다.

### 확인된 것
- `recompile_scripts` 에러·경고 0 (총 3회 — 코드 변경 단계마다).
- 씬 계층 확인(`get_gameobject`): `ScrollView/Viewport/List` 경로, `List` 가
  `drivenByObject: "List"`(ContentSizeFitter 가 실제로 높이를 제어 중)로 표시됨,
  `Scrollbar` rect 너비 14 · 높이 414(패널 body 영역과 정확히 일치), `HUD_Log` rect
  (16,346)-(376,586) 계산값과 일치.
- `HpFill` 의 `Image` 값(`m_Type: Filled, m_FillMethod: Horizontal, m_FillOrigin: 0,
  m_FillAmount: 1`)이 애초에 정확히 붙어 있었다는 것도 재확인 — 원래 버그는 UI 쪽이 아니라
  (아마) 폴링 타이밍/관찰 시점 문제였을 가능성이 높지만, 이벤트 기반으로 바꿔 어느 쪽이든
  해소되게 했다.
- Edit mode 확인 후 `save_scene` **1회**.

### 아직 확인 못 한 것 (유저가 볼 것)
- **여전히 플레이 모드 검증 안 함.** 이번엔 특히:
  - 집결지 지정 모드에서 마우스를 움직일 때 옅은 사각형(미리보기)이 따라오는지, 클릭하면 진하게
    고정되는지, 우클릭/Esc 로 없어지는지
  - HP 바가 실제로 전투 중 즉시 줄어드는지 (이번 수정으로 원래 뭐가 문제였는지도 자연히 드러날 것)
  - 로스터에 캐릭터가 6명 이상일 때 스크롤바가 실제로 드래그되고 목록이 스크롤되는지
  - 로그 창이 미니맵 바로 위에 딱 붙어 보이는지, 로스터와 로그 사이 간격이 어색하지 않은지
- `rallyAreaSize`(10), `previewAlphaScale`(0.55) 는 여전히 **임의값**.
- 스크롤 영역이 생기면서 행 너비가 340→322 로 18px 좁아졌다 — 텍스트 잘림이 생기면 알려줄 것.

### 씬 변경 여부
**있음.** `RallyRangeTemplate` 신설, `HUD_Roster` 높이 변경 + `ScrollView/Viewport/List/Scrollbar/
Handle` 신설(+ 기존 `List` reparent), `HUD_Log` 앵커·위치 변경. 저장 1회.

### 씬반영요청 목록
없음.

---

## UI-4. 집결지 마커 원형화 · 사망 표시 재설계 · 미니맵 명암 재조정 (2026-08-05)

### 무엇을 / 왜

유저 피드백 3건.
1. 집결지 마커(점) 디자인 변경 — 노란 네모 → **반투명 회색 원**.
2. HUD_Roster 사망 처리 재설계: 지금은 죽으면 행이 통째로 사라진다(HP 게이지가 "줄어드는"
   느낌이 아니라 "빨갱졌다 사라지는" 것처럼 보임) — **① 살아있는 동안은 초록 게이지가
   실제로 줄어드는 것으로 확실히 보이게, ② 죽으면 사라지지 않고 회색 처리로 "죽음"을
   명시, ③ 웨이브가 끝나야 그 행을 실제로 지울 것.**
3. 미니맵 명암 대비가 부족해 "캐릭터가 지나간 곳(안개가 걷힌 곳)"이 잘 안 보임 — 더
   직관적으로 밝아지게.

### 어떻게

**① 마커 원형화 — 이 프로젝트의 "MCP 로 Sprite 참조 불가" 제약을 처음으로 우회했다**
- 진행상황 8절 1번은 "`update_component` 로 Sprite 서브에셋 참조가 안 된다"고 적어뒀지만,
  그건 **Sprite Mode = Multiple**(텍스처가 메인 에셋, 스프라이트는 서브에셋)인 경우다.
  **Sprite Mode = Single** 이면 Unity 가 그 스프라이트 자체를 그 경로의 "메인 오브젝트"로
  취급하므로 `LoadAssetAtPath(path, typeof(Object))` 가 스프라이트를 직접 돌려준다 — 이번에
  실제로 시도해서 확인했다(`update_component` 로 `Image.sprite` 에 PNG 경로를 그대로 넣었더니
  `get_gameobject` 로 `m_Sprite: "RallyRange"` 가 정상적으로 붙어 있는 걸 확인).
  **→ 앞으로 새 UI 스프라이트가 필요하면, 새 텍스처를 Single 모드로 import 해서 MCP 로 바로
  참조를 넣는 방법을 먼저 시도할 것** (기존 텍스처의 내용만 바꿔치기하는 8절 1번의 우회법은
  "이미 참조가 걸려 있는 기존 스프라이트"에만 필요하다).
- Python(Pillow)으로 원(alpha 배경, 4배 크기로 그려 다운샘플해 안티에일리어싱) PNG 를 생성해
  `Assets/_Project/Art/UI/RallyRange.png` 에 저장. `Assets/Refresh` 로 1차 임포트(기본값은
  `textureType: 0`=Default, Sprite 아님) → `.meta` 를 기존 유닛 스프라이트(`monster_melee.png.meta`)
  구조를 참고해 손으로 `textureType: 8`(Sprite) · `spriteMode: 1`(Single) · `alphaIsTransparency: 1`
  로 고친 뒤 재-Refresh(진행상황 8절의 손 편집 패턴 재사용).
- `RallyMarkerTemplate` 의 `Image` 에 이 스프라이트를 연결하고 `color`를 반투명 회색
  `(0.62,0.62,0.62,0.55)`으로, 45° 회전(예전엔 정사각형을 돌려 다이아몬드처럼 보이게 한
  트릭)은 원에는 의미가 없으므로 0으로 되돌렸다. `RallyRangeTemplate`(10×10 구역 사각형)은
  이번 요청 대상이 아니라 그대로 뒀다.

**② HUD_Roster 사망 표시 재설계 — `CharacterRosterPanel.cs`**
- 원인 진단: HP 필드 자체(`Image.Type.Filled` 등)는 UI-3 에서 이미 정확한 값으로 확인됐었다.
  진짜 문제는 **`CharacterUnit.OnDeath()` 가 `Destroy(gameObject)` 를 부르고, 그 결과
  `UnitRegistry` 에서 빠지면서 다음 폴링에서 `CharacterSetChanged()` 가 true 가 되어 `Rebuild()`
  가 그 행을 통째로 비활성화(`SetActive(false)`)했던 것**이었다. HP 는 실제로 0까지 정확히
  줄어들고 있었지만, 그 직후 행 자체가 사라지니 "게이지가 줄어드는" 경험 자체가 안 만들어졌다.
- 캐릭터 목록(`_characters`)의 성격을 바꿨다: **더 이상 "지금 살아있는 캐릭터" 를 매 폴링
  재계산하지 않고, "로스터에 한 번이라도 오른 캐릭터" 를 계속 보관한다.** 새 캐릭터는
  `AppendNewCharacters()` 가 등장하는 즉시 뒤에 추가하고, 죽어도 이 목록에서 빼지 않는다.
- **사망 확정은 폴링이 아니라 `DamageableUnit.OnDied` 구독**(행마다 하나, `OnHpChanged` 와
  같은 패턴)으로 처리한다. `CharacterUnit.OnDeath()` 가 `Destroy()` 를 부른 바로 다음, 같은
  프레임 안에서 `OnDied` 가 호출되는데 — Unity 의 `Destroy` 는 프레임 끝에 처리되므로
  **이 콜백 안에서는 아직 `Stats`/`name` 을 안전하게 읽을 수 있는 마지막 순간**이다. 그 값을
  `CachedName`/`CachedStats` 로 스냅샷해두고, 그 뒤로는 `row.Unit` 의 멤버를 다시 읽지 않는다
  (`RefreshValues()` 에서 `row.IsDead` 인 행은 통째로 스킵).
- 죽은 행의 표시(`ApplyDeadAppearance`): 배경을 어둡게, 이름·능력치 글자를 회색으로,
  Duty 칸에 "사망" 표시, **그리고 HP 바는 비우는(투명) 게 아니라 반대로 100% 꽉 채운 회색
  막대로 바꿨다** — 빈 막대는 안 보여서 "사망"이 눈에 안 띄지만, 꽉 찬 회색 막대는 "여기 죽은
  사람이 있다"가 한눈에 들어온다. 선택·강화 버튼도 `interactable=false` 로 잠근다.
- **웨이브 종료 시 정리**: 사망한 캐릭터를 `HashSet<CharacterUnit> _dead` 에 모아두고,
  `WaveManager.OnWaveEnded` (요청한 "웨이브가 끝나면" 그대로)가 fire 하면 `_characters` 에서
  한꺼번에 제거한다. 목록이 줄어들며 인덱스가 밀리므로, 살아있는 캐릭터가 자리를 옮겨야 하는
  경우까지 포함해 행 전체를 다시 배정하는 `ReassignAllRows()` 를 새로 만들었다(기존 캐릭터가
  그대로 남은 자리는 참조 비교로 건너뛰어 불필요한 재구독을 피한다).
- 행이 재활용될 때(죽은 캐릭터 자리에 다른 살아있는 캐릭터가 들어오는 경우) `ApplyAliveAppearance()`
  로 회색 잔상을 지우고 정상 색으로 되돌린다.

**③ 미니맵 명암 재조정 — `HudTheme.cs` / `MinimapPanel.cs`**
- 원인: 지형 원색 자체가 어두웠다(바닥 38,44,52 — 최대 밝기가 이 정도). "탐사됐지만 지금 시야
  밖"은 이 색에 다시 140/255 배를 곱해 어둡게 눌렀는데, **이미 어두운 색을 한 번 더 곱하면
  거의 검정에 수렴**해서 미탐사(6,6,8)와 거의 구별이 안 됐다 — "캐릭터가 지나간 곳이 안 밝아
  보인다"는 지적과 정확히 일치.
- 지형 원색을 훨�써 밝게 올렸다(바닥 38→145, 벽 16→55, `HudTheme.MapFloor`/`MapWall`).
  "탐사됐지만 시야 밖" 처리 방식을 **곱연산 → 보간(미탐사 색과 지형 원색 사이 50%)** 으로
  바꿨다(`MinimapPanel.LerpColor32`) — 보간은 시작점(미탐사)이 정해져 있어서 결과가 절대
  그보다 어두워질 수 없다. 이제 미탐사(10,10,13) → 탐사·시야밖(대략 78,85,92) → 지금 시야 안
  (145,160,170) 세 단계가 뚜렷하게 구분된다.

### 겪은 함정

1. **"MCP 로 Sprite 참조 불가"는 절대 규칙이 아니라 "Multiple 모드일 때"의 제약이었다.**
   Single 모드에서는 스프라이트 자체가 메인 에셋이라 그대로 통했다. 기존 문서(진행상황 8절
   1번)를 너무 넓게 해석해서 "스프라이트는 무조건 씬 YAML 패치나 텍스처 바꿔치기로만 가능하다"
   고 여기고 있었는데, 이번에 실제로 시도해보고서야 조건이 더 좁다는 걸 알았다. **앞으로는
   "일단 Single 모드로 새로 만들어서 MCP 로 시도해본다"를 먼저 해볼 것.**
2. **Unity 의 `Destroy()` 가 프레임 끝까지 지연된다는 사실이, "죽는 순간에 값을 스냅샷할 수
   있는 마지막 기회가 있다"는 뜻이기도 하다.** `DamageableUnit.ApplyDamage` 의 기존 주석
   ("Destroy 는 프레임 끝에 처리되므로 아래 이벤트는 안전하다")이 정확히 이 사실을 가리키고
   있었는데, 처음 로스터를 만들 때는 이걸 활용하지 못하고 그냥 "죽으면 사라진다"로 설계했다.
3. **`Color32` 는 `Lerp` 가 없다**(`Color` 전용). 채널별로 직접 `Mathf.Lerp` 후 `RoundToInt` 로
   반올림하는 짧은 헬퍼가 필요했다 — 캐스팅만 하면(반올림 없이) 다운샘플 시 밝기가 계통적으로
   살짝 어두워진다.

### 확인된 것
- `recompile_scripts` 에러·경고 0.
- `RallyMarkerTemplate` 의 `Image.m_Sprite` 가 실제로 `"RallyRange"` 로 붙은 것을 `get_gameobject`
  (instanceId 경유 — 비활성 오브젝트라 경로 조회 불가, 진행상황 12절)로 확인.
- `HudTheme.MapDimMul` 삭제 후 참조 잔존 여부 전체 검색 — 없음.
- Edit mode 확인 후 `save_scene` 1회.

### 아직 확인 못 한 것 (유저가 볼 것)
- **여전히 플레이 모드 검증 안 함.** 이번엔 특히:
  - 마커가 실제로 반투명 회색 원으로 보이는지 (Single 스프라이트 임포트가 화면에서도 의도대로
    나오는지 — 인스펙터로는 확인했지만 렌더링 결과 자체는 못 봤다)
  - 캐릭터가 맞을 때 초록 게이지가 실제로 줄어들고, 죽으면 회색으로 바뀐 채 행이 남아있는지
  - 웨이브가 끝나는 순간 죽은 행들이 실제로 사라지고, 그 자리에 다른 살아있는 캐릭터 행이
    올바르게 밀려 올라오는지(스크롤 위치가 이상하게 튀지 않는지도 같이 봐줄 것)
  - 미니맵에서 세 밝기 단계(미탐사/탐사·시야밖/지금 시야 안)가 실제로 뚜렷하게 구분되는지
- 미니맵 새 팔레트(바닥 145,160,170 등)가 유닛 점(캐릭터 파랑·넥서스 청록 등)과 충돌해 보이지
  않는지는 육안 확인 필요 — 채도 높은 색이라 괜찮을 것으로 예상하지만 확정은 아니다.
- `deadBarColor`/`rowDead`/`deadTextColor`/`MapExploredBrightness`(0.5) 는 전부 **임의값**.

### 씬 변경 여부
**있음.** `RallyMarkerTemplate` 의 `Image`(sprite·color·rotation) 변경. 신규 에셋
`Assets/_Project/Art/UI/RallyRange.png`(+`.meta`). 저장 1회.

### 씬반영요청 목록
없음.

---

## UI-5. 캐릭터 생성 비용 공식 확정 (2026-08-05)

### 무엇을 / 왜

유저 확정: 캐릭터 성장 기획서(5장 "캐릭터 생성 방식" — "생성한 캐릭터 수에 비례하여 자원
소모량 점진적 상승", 구체적 수치는 기획서에 없음)를 근거로, **비용 = 150 + 100n** (n = 몇
번째로 만드는 캐릭터인지, 1부터 — ex: 1 → 2 → 3 …) 공식을 그대로 적용하라는 지시.

### 어떻게

[CharacterCreationService.cs](Assets/_Project/Scripts/UI/CharacterCreationService.cs) —
기존엔 `baseCost(30) + costIncreasePerCharacter(15) × extra`(extra = 0부터 시작하는 증가분)
였던 걸, `NextCreationNumber`(n, 1부터) 프로퍼티를 새로 뽑아내고 `CurrentCost = baseCost(150)
+ costPerCreation(100) × n` 으로 교체했다. n 계산 로직(시작 인원 3명을 빼는 것)은 그대로
재사용 — 시작 캐릭터는 기획서가 말하는 "생성한 캐릭터"가 아니라 게임이 처음부터 쥐여주는
인원이라 공식에서 빼는 게 맞다.

씬의 `GameSystems/CharacterCreationService` 필드 이름이 `costIncreasePerCharacter` →
`costPerCreation` 으로 바뀌면서, 기존 씬 YAML 의 `baseCost: 30` 값은 필드명이 그대로라
남아있었고(30에서 150으로 안 바뀜) `costPerCreation` 은 새 필드라 코드 기본값(100)을 그대로
받았다 — `baseCost` 만 `update_component` 로 150으로 명시적으로 다시 넣어 맞췄다
(`get_gameobject` 로 `NextCreationNumber: 1, CurrentCost: 250` 확인, 150+100×1과 일치).

### 겪은 함정
- **필드 이름을 바꾸면 기존 씬의 값이 새 기본값으로 조용히 리셋된다(필드별로 다르게).**
  이름이 그대로인 필드(`baseCost`)는 옛 값(30)이 남고, 이름이 바뀐 필드(옛
  `costIncreasePerCharacter` 15 → 새 `costPerCreation`)는 코드의 새 기본값(100)을 받는다 —
  둘 다 다시 확인 안 하면 어중간하게 섞인 값(30+100×1=130)이 나온다. 값을 바꾸는 리팩터링을
  할 땐 **씬에 이미 값이 박혀있는 필드는 이름을 유지하거나, 바꿨으면 반드시 씬 값도
  다시 확인할 것.**

### 확인된 것
- `recompile_scripts` 에러·경고 0.
- `get_gameobject` 로 `CharacterCreationService.CurrentCost: 250`(=150+100×1, 첫 생성) 확인.
- Edit mode 확인 후 `save_scene` 1회.

### 아직 확인 못 한 것
- 플레이 모드에서 실제로 2번째·3번째 생성 시 350/450 으로 오르는지 미확인.
- `baseCost`/`costPerCreation` 값 자체(150/100)는 유저가 명시적으로 지정한 최종값이라
  추가 확인 불필요.

### 씬 변경 여부
있음. `CharacterCreationService.baseCost` 값 변경(30→150). 저장 1회.

### 씬반영요청 목록
없음.

---

## UI-6. 전술 지침 UI + 전술에 따른 전투 AI 재구성 (2026-08-06)

### 무엇을 / 왜

유저 요청: 목업 `라스트 생추어리/UI/Last Sanctuary 전술 지침 UI.html` 을 참고해 전술 지침
팝업을 만들고, **설정한 전술이 실제 인게임 AI 로직에 반영**되게 할 것. 목업의 캐릭터 정보
칸(체력/마나/스태미나/능력치)은 요구대로 줄여서 **일러스트 자리(비워둠) · 이름 · 강화 횟수(LV)
· 현재 체력 %** 만 남겼다.

**핵심 제약 두 가지 (유저가 명시)**
1. **전술 지침 창은 캐릭터를 선택하지 않는다.** 선택은 `UnitSelector`(월드 클릭)와 로스터만
   한다. 창은 `UnitSelector.OnSelectionChanged` 를 구독해 따라가기만 한다.
2. **창이 `HUD_Roster` 를 가리면 안 된다.** 그래서 (a) 창을 x 400~1620 에 배치해 로스터
   (x 16~376)와 우측 패널(x 1644~)을 피하고, (b) **전체 화면 모달 배경을 만들지 않았다** —
   모달 `Image` 는 레이캐스트를 먹어 로스터 클릭을 막는다.

이 둘 덕분에 **누르는 순서가 병렬**이 된다: 로스터 → 전술 지침이든, 전술 지침 → 로스터든
언제나 "지금 선택된 캐릭터"가 편집 대상이고, 창이 열린 채로 로스터를 눌러도 실시간 전환된다.

### 어떻게 — 데이터

- [TacticalOrder.cs](Assets/_Project/Scripts/Combat/TacticalOrder.cs) — enum 6종 + 직렬화 클래스
  + 표시 라벨. UI 문구와 요약문이 같은 문자열을 쓰도록 라벨을 여기 한 곳에 모았다.
- [CharacterTactics.cs](Assets/_Project/Scripts/Combat/CharacterTactics.cs) — **캐릭터가 들고
  다니는** 지침. 중앙 서비스(딕셔너리)로 안 만든 이유: 이 프로젝트는 캐릭터를
  `Character_Template` 복제로 만들기 때문에(진행상황 5절) 컴포넌트로 두면 **템플릿 인스펙터
  값이 곧 모든 신규 캐릭터의 기본 지침**이 되고, 죽거나 새로 생겨도 항목 관리가 필요 없다.

### 어떻게 — AI (요청받은 4가지 공격 유형)

전부 `UnitCombat` 의 인스펙터 값이다(= `Character_Template` 에서 고치면 전원 적용).

| 유형 | 동작 | 인스펙터 값(기본) |
|---|---|---|
| 근거리 | 기존 공격 그대로 | `attackRange` |
| 원거리 | 히트 스캔 단일 타격(투사체 없음) | `rangedRangeTiles` 5 |
| 마법 | 사거리 밴드 안의 대상에 정사각 범위 피해 | `magicMinRangeTiles` 2 / `magicMaxRangeTiles` 6 / `magicAreaTiles` 2(=2x2) / `magicSafeRadiusTiles` 1 |
| 치유 | **적을 안 노리고** 다친 아군을 회복 | `healRangeTiles` 3 / `healPercentOfAttack` 100(=공격력 수치만큼) |

- 마법의 "1의 범위 안에 있는 적은 공격 불가"는 **두 겹**으로 구현했다 — 타겟 후보 필터에서
  안전 반경 안의 적을 빼고(`BuildTargetFilter`), 범위 피해를 넣을 때도 한 번 더 제외한다
  (`PerformMagicSplash`). 그리고 적이 안전 반경 안까지 붙으면 `_backOff` 로 **거리를 벌린다** —
  안 그러면 영영 못 때리고 그 자리에 서 있는다.
- 원거리·마법은 `requireLineOfSight` 로 벽 너머 사격을 막는다(`GridPathfinder.HasLineOfSight`).
- `EffectiveDetectRange = max(detectRange, 실제 사거리)` — 사거리가 인식 거리보다 길면
  "때릴 수 있는데 못 보는" 모순이 생긴다.

### 어떻게 — AI (나머지 항목, 맥락상 구성)

- **포지션(전방/중위/후방)**: 유저 정의대로 **집결지 구역 기준 넥서스에서 먼 쪽 = 전방**.
  `CharacterBehavior.PickSpotAround` 가 넥서스→구역중심 축을 잡고 구역을 축 방향으로 3등분해
  해당 구간에서만 순찰 지점을 뽑는다. 집결지가 없을 땐 넥서스 방어 **원**의 반지름을 3등분한다
  (원에서는 반지름이 곧 넥서스로부터의 거리라 같은 정의가 그대로 성립).
- **공격 우선 대상**: `UnitRegistry.FindTargetBy`(신규). 기존 `FindTarget`(몬스터의 `UnitKind`
  우선순위, 웨이브 기획서 p13)은 **손대지 않았다** — 판정 축이 달라서 한 함수에 섞으면 몬스터
  타겟팅까지 흔들린다(진행상황 6절의 과거 버그와 같은 종류의 위험).
- **공격 반응**: "대기"는 타겟을 잡되 쫓지 않고 자기 자리로 돌아간다(`_holdingGround`).
- **후퇴 판단 기준**: 체력 % 이하 → `UnitCombat.SetCombatSuppressed(true)` + 넥서스 근처로
  물러남. 복귀는 기준 + `retreatRecoverMargin`(15%) 이상 — 여유가 없으면 기준선에서
  후퇴/복귀가 매 프레임 뒤집힌다. 로스터 `Duty` 에 "후퇴" 표시 추가.
- **비전투 우선 행동**: 사냥 = 기존 동작, 탐색 = 사냥 안 함(안개 해제만), **건설 = 건설
  시스템이 없어 실질적으로 "자리 지키며 대기"**. 치유 유형은 사냥하지 않는다.
- **웨이브 반응**: "즉시 방어" = 기존 동작(웨이브 타임에 사냥 타겟을 놓는다),
  "우선 행동 중시" = 진군 구간까지 정찰/사냥을 유지하다 목적지에 닿으면 합류. 전투(Battle)가
  시작되면 어느 쪽이든 합류한다 — 그때까지 안 오면 넥서스가 빈다.

### 어떻게 — 하이라키 (§10 H-1: MCP 로 직접 생성)

`UI_Root/HUD_Tactics` 이하 전부 MCP 로 만들었다(스크립트 런타임 생성 없음). 구조:
`Header`(제목/부제/닫기) · `Info`(일러스트 자리·이름·LV·체력%) · `Col1`(교전 설정) ·
`Col2`(전투 행동) · `Col3`(비전투·웨이브) · `Footer`(초기화/닫기). 기본 **비활성**.
`HUD_Actions/Buttons` 의 집결지 버튼 **아래**에 `TacticsButton` 을 넣어 토글한다.

- **버튼·텍스트는 기존 오브젝트를 `duplicate_gameobject` 로 복제해서 만들었다** — 새로 만든
  TMP 에는 네오둥근모 폰트 참조가 안 붙기 때문(§10 H-4 가 말하는 `HudFontApplier` 안전망은
  **실제로는 존재하지 않는다**). 버튼은 `HUD_Actions/Buttons/RallyButton`, 텍스트는
  `HUD_Roster/Title` 을 원본으로 썼다. 24-3절이 쓴 방법과 같다.
- 반복 배치는 `GridLayoutGroup` 에 맡겨 자식의 `RectTransform` 을 일일이 안 넣게 했다.

### 겪은 함정

1. **새 `.cs` 파일은 `recompile_scripts` 만으로는 안 잡힌다** — 먼저
   `execute_menu_item("Assets/Refresh")` 로 임포트해야 한다. 안 하면 새 타입이 전부
   `CS0246 could not be found` 로 뜬다(실제로 14개 에러를 봤다).
2. **`update_component` 의 enum 필드는 "인덱스"로 해석된다.** TMP 의
   `m_VerticalAlignment: 512`(실제 enum 값)를 넣으면 `Enum index 512 is out of range` 로
   실패한다. **문자열 이름**(`"Middle"`, `"Left"`, `"Right"`, `"Top"`)을 넣어야 한다.
3. **`duplicate_gameobject` 는 항상 부모의 맨 뒤에 붙는다** — 형제 순서를 지정하는 인자가 없다.
   집결지 버튼 아래에 넣으려고, 뒤에 와야 할 `Upgrade` 를 `reparent_gameobject` 로 밖에
   뺐다가 다시 넣어 맨 뒤로 보내는 식으로 순서를 맞췄다.
4. **`Slider` 를 쓰지 않고 +/- 버튼으로 갔다** — `Slider.fillRect`/`handleRect` 는 오브젝트
   참조라 MCP 로 넣을 수 없다(진행상황 8절 4번). 참조가 필요 없는 구성으로 우회.
5. 비활성 오브젝트(`Character_Template`)는 여전히 경로/이름으로 조회가 안 된다 —
   `get_scenes_hierarchy` 의 `instanceId` 를 같은 턴에 써서 `CharacterTactics` 를 붙였다.

### 확인된 것
- `Assets/Refresh` → `recompile_scripts` 에러·경고 0, 콘솔 에러 0.
- 씬 저장 후 YAML 재검증: `Character_Template` 의 `UnitCombat` 에 전술 사거리 8개 필드가
  전부 들어갔고(`rangedRangeTiles: 5` / `magicMinRangeTiles: 2` / `magicMaxRangeTiles: 6` /
  `magicSafeRadiusTiles: 1` / `magicAreaTiles: 2` / `healRangeTiles: 3` /
  `healPercentOfAttack: 100` / `requireLineOfSight: 1`), `CharacterTactics.order` 블록도
  기본 지침(중위·근거리·가장 가까운 적·추격·사냥·즉시 방어·후퇴 35%)으로 직렬화됨.
- 버튼 순서: 캐릭터 생성 → 집결지 설정 → **전술 지침** → 강화.

### 아직 확인 못 한 것 (유저가 볼 것)
- **플레이 모드 미검증** (§11-5 원칙). 특히: 창이 실제로 로스터를 안 가리는지, 옵션 버튼
  하이라이트가 맞게 도는지, 마법의 거리 벌리기가 자연스러운지, 치유 캐릭터가 아군을 제대로
  따라다니는지.
- **레이아웃 수치는 눈으로 못 봤다** — 1920x1080 기준 계산으로만 배치했다. 글자가 넘치거나
  칸이 비면 인스펙터에서 `RectTransform` 을 직접 조정할 것(그래서 하이라키에 실물로 만들었다).
- **치유 유형은 밸런스 미검증** — 회복량 = 공격력 수치라 성장한 캐릭터가 매우 강한 힐러가 된다.
  `healPercentOfAttack` 로 조절 가능.
- **"건물 건설"은 실동작이 없다**(건설 시스템 미구현). 지금은 대기로 동작하며 버튼 라벨에도
  "(미구현·대기)"라고 표기해뒀다.

### 씬 변경 여부
있음. `UI_Root/HUD_Tactics` 신규 생성, `HUD_Actions` 에 버튼 추가 + 높이 164→212,
`Character_Template` 에 `CharacterTactics` 추가 + `UnitCombat` 전술 필드 기본값. 저장 1회.
커밋 분리: 스크립트(`643ff31`) / 씬(`09d797e`) / 폰트 아틀라스(`cbd678d`).

### 씬반영요청 목록
없음.

---

## UI-7. 전방 포지션 적극 방어 · 비선공 반격 · 철권식 체력바 · 캐릭터 애니메이션 (2026-08-06)

UI-6 에 대한 유저 피드백 7건을 한 번에 처리했다.

### 1. 전방 포지션이 적극적으로 방어

⚠️ **처음 "근거리 공격 유형"으로 잘못 이해했다가 유저가 정정했다** — "근거리가 아니라 **전방
포지션**이 적극 방어해야 하고, **공격 유형은 기존 방식 유지**". 전열을 정하는 축은 포지션이고
공격 유형은 때리는 방식일 뿐이므로, 그쪽이 옳다.

[CharacterBehavior.cs](Assets/_Project/Scripts/Units/CharacterBehavior.cs) —
`PickZoneSpot()` 을 새로 만들어 목적지 선택을 한 곳으로 모으고, **전방 포지션에 한해**
`TryPickInterceptSpot()` 을 순찰보다 먼저 시도한다. 구역 중심에서 `frontInterceptRange`(14타일)
안에 웨이브 몬스터가 있으면, 구역 안을 어슬렁거리는 대신 **구역 중심 → 그 적 방향으로 구역
경계 + `frontInterceptOvershoot`(1.5타일)** 지점으로 나가 선다. 목줄도 그만큼 늘려줘야 그
자리에서 실제로 교전한다(`frontInterceptLeashBonus` 4).

- **위협 선정은 "나에게 가까운 적"이 아니라 "구역에 가까운 적"**이다 — 지키는 대상이 구역이므로.
- **중립 몬스터는 대상이 아니다.** 넣으면 전방 캐릭터가 사냥감을 쫓아 구역을 비운다
  (24-5절에서 고친 문제와 같은 종류).
- 적이 이미 구역 안까지 들어왔으면 `outward` 를 적까지의 거리로 clamp 해서 뒤로 물러나지 않게 했다.

### 2. 집결지 표시가 다른 UI 를 가리지 않게

같은 캔버스 안이라 **형제 순서**로 그려지는데, 마커·범위가 `UI_Root` 의 마지막 자식이라
HUD 위에 덮여 그려졌다. `duplicate_gameobject`/`Instantiate` 는 항상 맨 뒤에 붙으므로
순서로 해결하면 계속 깨진다 → **`UI_Root/RallyOverlay` 컨테이너에 `Canvas`(overrideSorting,
sortingOrder **-1**)를 붙여** 형제 순서와 무관하게 항상 HUD 뒤에 그려지게 했다.

- `GraphicRaycaster` 를 **일부러 안 붙였다** — 집결지 표시가 클릭을 먹으면 안 된다.
- [RallyPointService.cs](Assets/_Project/Scripts/UI/RallyPointService.cs) 는 템플릿을 이
  컨테이너에서 찾고, 복제본도 그 안에 만든다. 예전 위치(UI_Root 직속)도 폴백으로 남겼다.

### 3. 비선공 중립 몬스터도 맞으면 반격

유저 정의: **"비선공은 먼저 공격하지 않는다는 뜻"** — 맞고도 가만히 있는 게 아니다.

⚠️ `NeutralMonster*` 3개 파일은 **PROTO 소유(§2)라 손댈 수 없다.** 그래서 UI 소유 파일에서만 처리했다:
- [DamageableUnit.cs](Assets/_Project/Scripts/Combat/DamageableUnit.cs) — `LastAttacker` /
  `LastAttackedTime` 추가. `TakeDamageFrom(attacker)` 가 이미 공격자를 받고 있어서 기록만 하면 됐다.
- [UnitCombat.cs](Assets/_Project/Scripts/Combat/UnitCombat.cs) — `canAcquireTargets` 가 꺼져
  있어도 `FindRetaliationTarget()` 으로 때린 상대를 타겟으로 잡는다. 그만두는 조건 셋:
  **공격력 0**(때려봐야 의미 없음 — 유저가 말한 "공격력이 존재할 경우") ·
  `retaliateMemorySeconds`(8초) 경과 · `retaliateChaseRange`(8타일) 밖으로 도망.
  마지막 두 개가 없으면 배회하던 중립이 캐릭터를 맵 끝까지 쫓아간다.
- 부수 효과 없음 확인: 중립은 `Faction.Neutral` 이고 `WaveManager.IsMonsterVersusAngel` 은
  `Faction.Cancer` 를 확인하므로(24-6절 3번 수정), 반격이 웨이브 전투 타이머를 잘못 켜지 않는다.

### 4. 후퇴 판단 기준 — 막대 드래그(1%) + ±5% 버튼 유지

[UiDragBar.cs](Assets/_Project/Scripts/UI/UiDragBar.cs)(신규) — `IPointerDownHandler` +
`IDragHandler` 만 구현한 최소 컴포넌트. 자기 `RectTransform` 안에서의 마우스 x 만 보고 0~1 을
낸다. **유니티 `Slider` 를 안 쓴 이유**: `fillRect`/`handleRect`/`targetGraphic` 이 전부
오브젝트 참조라 MCP 로 넣을 수 없다(진행상황 8절 4번). 이 방식은 참조가 0개다.

`Col2/RetreatBar` 에 붙이고 `raycastTarget` 을 켰다(`Fill` 은 껐다 — 자식이 이벤트를 가로채면
막대 오른쪽 끝을 잡을 수 없다). ± 버튼은 그대로 남겼다: 막대는 대충, 버튼은 정확히.

### 5. 체력바 — 철권식 잔상으로 재작업

**UI-6 의 방식이 틀렸다.** `fillAmount` 자체를 목표치까지 서서히 줄이면 **맞는 순간에는 아무
변화가 없고** 막대가 뒤늦게 스르륵 줄어들 뿐이다 — "실제로 깎이는 게 보인다"의 반대다.

[HpGhostBar.cs](Assets/_Project/Scripts/UI/HpGhostBar.cs)(신규, 로스터·전술 지침 창 공용) —
두 겹으로 갈랐다:
- **본 막대**(`HpFill`) — 실제 체력을 **즉시** 반영. 맞는 순간 뚝 떨어진다.
- **잔상 막대**(`HpGhost`, 본 막대 **뒤**) — 맞기 직전 값을 `HoldSeconds`(0.35초) 붙들고
  있다가 `DrainPerSecond`(0.7/초)로 줄어 사라진다. 그래서 "방금 깎인 구간"이 밝은 띠로 남는다.
- **회복은 잔상이 즉시 따라붙는다** — 안 그러면 체력이 늘었는데 막대가 안 늘어난 것처럼 보인다.
- 행 재활용·캐릭터 전환 시엔 `Snap()` 으로 애니메이션 없이 맞춘다.
- 사망 표시(회색 꽉 찬 막대)에는 잔상을 0으로 죽였다 — 방해만 된다.

`hpDrainSpeed` 필드는 없어졌고 `ghostHoldSeconds` / `ghostDrainSpeed` / `ghostColor` 로 대체됐다.

### 6·7. 캐릭터 애니메이션 + 외형 2종 랜덤

에셋 구성 확인: `Art/Char_Asset/Char_Asset_Angel`(Idle 4 / Walk 5 / MeleeAttack 5 /
RangedAttack 5, 방향별) · `Art/Char_Asset/Char_Asset_LastSanctuary`(Idle 4 / Walk 6 /
Attack 6, 방향별). 둘 다 `spriteMode: 1`(Single) · 피벗 (0.5, 0) 발밑 · PPU 50 · Point 필터.

- [CharacterSkinSO.cs](Assets/_Project/Scripts/Combat/CharacterSkinSO.cs) — 모션×방향 프레임 목록.
  원거리 프레임이 없는 스킨은 근접 모션으로 자동 대체한다(LastSanctuary 가 그렇다).
- [CharacterAnimator.cs](Assets/_Project/Scripts/Combat/CharacterAnimator.cs) — `UnitCombat` 의
  상태와 새 `OnAttackPerformed` 이벤트를 읽어 프레임을 넘긴다. 우선순위는 **공격 → 이동 → 대기**
  (공격을 먼저 보는 이유: 때리는 동안에도 밀림(separation)으로 좌표가 흔들려서 이동 판정을
  먼저 보면 걷는 모션이 섞인다).
- **Animator/AnimatorController 를 쓰지 않았다** — 컨트롤러·클립이 전부 오브젝트 참조라 MCP 로
  넣을 수 없고 스킨마다 컨트롤러를 손으로 만들어야 한다. 이 프로젝트는 이미 코드가 FSM 상태를
  들고 있어서 그걸 그대로 읽는 편이 단순하다. (`Char_Asset_Angel/Anim/*.anim` 과
  `Char_Angel_Controller.controller` 는 에셋에 들어있지만 **쓰지 않는다** — 남겨는 뒀다.)
- **스킨 목록은 `Resources/Skins` 에서 `LoadAll` 한다** — Sprite 배열은 오브젝트 참조라 MCP 로
  못 넣기 때문. 덕분에 **새 외형은 에셋을 폴더에 넣기만 하면 후보에 추가**된다.
- 스킨 에셋 2개는 **PNG `.meta` 의 guid 를 읽어 스크립트로 생성**했다(멱등). Single 모드
  스프라이트 참조 형식 `{fileID: 21300000, guid: ..., type: 3}` 그대로.
- 생성 시 `OnEnable` 에서 무작위로 하나를 고른다(유저 확정: 프로토타입은 랜덤). 추후 캐릭터별
  테이블을 파싱하면 `SetSkin()` 으로 지정만 하면 되고 이 구조는 안 바뀐다.
- `Character_Template` 의 `UnitCombat.flipSpriteToFaceMovement` 를 **껐다** — 좌우 프레임이
  따로 있으므로 `flipX` 로 한 번 더 뒤집으면 왼쪽 스프라이트가 오른쪽을 보게 된다.

### 겪은 함정

1. **`update_component` 의 enum 은 문자열 이름으로 넣어야 한다**(UI-6 에서 발견한 것과 동일).
2. **비활성 오브젝트 안쪽은 경로로 접근이 안 된다.** `HUD_Tactics/Info/HpBack` 작업은
   **패널을 잠깐 활성화 → 작업 → 다시 비활성화**로 처리했다(Edit mode 에선 스크립트가 안 돌아
   부작용 없음). `Character_Template` 처럼 루트가 비활성인 것은 `get_scenes_hierarchy` 의
   `instanceId` 를 같은 턴에 쓰는 방법밖에 없다.
3. **형제 순서를 앞으로 보내는 MCP 도구가 없다.** `HpGhost` 를 `HpFill` 앞에 두려고,
   `HpGhost` 를 만든 뒤 **`HpFill`(과 `HpPercentLabel`)을 밖으로 뺐다가 다시 넣어** 맨 뒤로
   보냈다. UI-6 의 `TacticsButton` 때와 같은 수법.

### 확인된 것
- `Assets/Refresh` → `recompile_scripts` 에러·경고 0, 콘솔 에러 0.
- 씬 저장 1회. YAML 재검증: `frontInterceptRange: 14` / `canRetaliate: 1` /
  `retaliateChaseRange: 8` / `skinResourceFolder: Skins` / `flipSpriteToFaceMovement: 0` /
  `RallyOverlay` Canvas `m_OverrideSorting: 1`, `m_SortingOrder: -1` / `HpGhost` 2개
  (로스터 행 템플릿 + 전술 지침 창) / `UiDragBar` 1개.
- 스킨 에셋 임포트 에러 0. 프레임 수: Angel idle 4·walk 5·attack 5·ranged 5 (방향별),
  LastSanctuary idle 4·walk 6·attack 6 (방향별).
- 자식 순서: `HpBack/HpGhost → HpFill → HpPercentLabel`.

### 아직 확인 못 한 것 (유저가 볼 것)
- **플레이 모드 미검증** (§11-5).
- **캐릭터 크기**: PPU 50 · 프레임 높이 99~140px 이라 캐릭터가 화면에서 **2.0~2.8 타일** 높이가
  된다(몬스터는 1타일, 14절의 기존 캐릭터는 2타일). 유저가 정한 임포트 설정이라 **일부러 안
  건드렸다** — 크기를 바꾸려면 각 PNG `.meta` 의 `spritePixelsToUnits` 만 고치면 된다
  (2타일로 맞추려면 Angel 69 / LastSanctuary 66 근처).
- **모션별 캔버스 높이가 다르다** — Angel 은 공격 프레임(103~105px)이 대기(125~138px)보다
  낮아서 때릴 때 캐릭터가 살짝 작아 보인다. 원화 자체의 자세 차이로 보이지만 어색하면 알려줄 것.
- **전방 인터셉트 수치**(14 / 1.5 / 4 타일, 0.5초 갱신)는 임의값 — 실제로 전방이 너무 멀리
  나가거나 반응이 느리면 인스펙터에서 조정할 것.
- **잔상 색·시간**(0.35초 붙들기, 0.7/초 감소, 연한 분홍)도 임의값.

### 씬 변경 여부
있음. `RallyOverlay` 신규 + 마커 2개 이동, `HpGhost` 2개 신규, `RetreatBar` 에 `UiDragBar`,
`Character_Template` 에 `CharacterAnimator` + `flipSpriteToFaceMovement` 끔. 저장 1회.
커밋: 스크립트 `bb59829` / 스킨·애니메이션·아트 `8e681b8` / 씬 `a8cf443` / 폰트 `fc2965d`.

### 씬반영요청 목록
없음.

---

## UI-8. 원거리 몬스터(Spitter) 스킨 좌우 재정렬 · 짤린 프레임 복구 · 전용 투사체 (2026-08-11)

> 상세는 `진행상황.md` **30절**. 이 로그는 소유권·씬 변경 관점만 남긴다.
> (참고: UI-7 이후 27~29절 작업은 이 로그에 기록되지 않고 `진행상황.md` 에만 남아 있다.)

### 무엇을 했나
1. **전투 로직은 멀쩡했다** — 플레이 모드로 실제 스폰된 개체를 조회해 `attackType: Ranged` /
   `EffectiveAttackRange: 5` 를 확인했다(30-0절). "근거리로 변했다"는 증상의 원인은 전부 그림 쪽.
2. **원본 팩의 `Left`/`Right` 이름이 모션마다 규칙이 달랐다**(Idle 둘 다 좌향 / Move 반전 /
   RangedAttack 정상) → 걷기가 반대로 재생되던 "문워크". 좌향 하나를 기준으로 삼고 우향은
   반전 생성하도록 스킨 전체를 다시 만들었다.
3. **짤린 원본 프레임 처리** — `Move_Right_00` 은 이웃 프레임에서 결손부만 복구, 나머지 손상
   프레임은 애초에 안 쓰는 쪽으로 소스를 골랐다.
4. **공격 프레임에 구워진 침 줄기 제거** → 캔버스 236x104 → 150x106.
5. **투사체를 원본 팩의 `Char/Projectile` 9프레임으로 교체**, `CombatProjectileFx` 에
   다중 프레임 탄환 + 총구 오프셋 추가.

### 소유권 (§2)
- 수정: `Scripts/Combat/CombatProjectileFx.cs` (UI 소유),
  `Resources/Fx/**` · `Resources/MonsterSkins/Ranged/Skin_Spitter.asset` (UI 소유),
  `Art/Char_Asset/Char_Asset_Spitter/**` (PROTO 의 `Art/Units`·`Art/Tiles`·`Art/OrganicTilemap`
  어디에도 해당하지 않는 경로 — 27-1·29-9절이 이미 이 브랜치에서 쓰던 자리).
- **PROTO 소유는 한 파일도 안 건드렸다** — 특히 `Data/Units/Monster_Ranged.asset` 은 사거리가
  이미 5라 그대로 뒀다. `Scripts/Units/Monster*`, `Scripts/Wave|Map|Fog|Build`, `Tools/` 무접촉.

### 씬 변경 여부
있음(저장 1회 + 검증용 원복 저장 1회, 커밋은 1개). `Monster_Ranged_Template` 의
`UnitCombat.attackType` 을 Melee → **Ranged**. 그 외 변경 없음.
검증용으로 `MonsterSpawner.spawnOnStart` 를 잠시 켰다가 **원복 후 저장까지 확인**했다.
커밋: `b6edd99` (스크립트 · 아트 · 씬 한 커밋).

### 씬반영요청 목록
없음.

---

## UI-9. 마법 유형 사냥 무피해 수정 · 전술 포지션별 교전 거리 (2026-08-11)

> 상세는 `진행상황.md` **31절**.

### 무엇을 했나
1. `PerformMagicSplash` 가 `Opposite` 진영만 모아서 **마법 캐릭터가 중립 몬스터를 사냥하면
   피해가 0**이던 버그(모션만 재생). 타겟 자신을 따로 확인해 때리도록 고쳤다.
2. "못 때리는 최소 거리"를 `UnitCombat.MinAttackDistance` 한 곳으로 모아, 타겟 필터·상태
   결정·실제 타격이 같은 선을 쓰게 했다. `TryAttack` 에도 가드 추가(헛 모션 방지).
3. `UnitCombat.SetStandoff(tiles)` 신설 — 교전 중 유지 거리. `ChaseDestination` 의 후퇴
   목적지를 타겟 기준 고정점으로 바꿔 끝없이 물러나던 것도 같이 고쳤다.
4. `CharacterBehavior` — 포지션별 교전 거리(전방 0 / 중위 전방아군+1.5 / 후방 최대사거리),
   중위·후방의 구역 내 교전 지원(`TryPickSupportSpot`).

### 소유권 (§2)
- 수정: `Scripts/Combat/UnitCombat.cs`, `Scripts/Units/CharacterBehavior.cs` — **둘 다 UI 소유**.
- PROTO 소유 무접촉.

### 씬 변경 여부
**없음.** 신규 `[SerializeField]` 3개(`supportRange`/`supportRepick`/`midBehindGap`)는 씬에
저장돼 있지 않아 코드 기본값이 그대로 적용된다(플레이 모드에서 14/0.5/1.5 로 들어오는 것 확인).
커밋: `c43ede7`.

### 씬반영요청 목록
없음.

---

## UI-10. 포탑 파괴 시 건설 비용 되돌아가던 버그 수정 (2026-08-11)

> 상세는 `진행상황.md` **32절**.

### 무엇을 했나
유저 확정: 캐릭터 생성 비용·건물 건설 비용은 사망·파괴와 무관하게 "지금까지 수행한
횟수"에만 비례해야 한다. `BuildService.HandleTowerDestroyed` 가 포탑 파괴마다
`_builtCount` 를 되돌리던 로직(27-5절이 의도적으로 넣었던 것)을 제거했다.
캐릭터 생성 비용은 원래부터 `UnitSpawner.SpawnedCharacters`(죽어도 안 지워지는 리스트)
기준이라 문제없음을 확인만 했다(코드 변경 없음). 강화 비용은 이 요청에 포함되지 않는다
(개인별 이력이 캐릭터와 함께 사라지는 게 맞는 동작).

### 소유권 (§2)
- 수정: `Scripts/Buildings/BuildService.cs`.
  ⚠️ 이 문서 §2 는 건설을 `Scripts/Build/**` 로 PROTO 소유라 적어뒀지만, 실제 폴더명은
  `Buildings`(다름)이고 27-5절부터 이 브랜치가 계속 만들고 고쳐왔다(git log 확인,
  `7ce2c47`·`f02420d`) — §2 항목이 실제 경로와 안 맞는 문서 오류로 보인다. PROTO 소유
  경로(`Scripts/Wave|Map|Fog`, `Units/Monster*`, `Data/Units|Map`, `Tools/`)는 무접촉.

### 씬 변경 여부
없음. 커밋: `813f57d`.

### 씬반영요청 목록
없음.

---

## UI-11. 공속·이속 공식 개정 · 스킨 이름 정리 · 프레이야 임포트 · 객체별 투사체/착탄 · 회복 모션 (2026-08-11)

> 상세는 `진행상황.md` **38·39절**.

### 무엇을 했나

유저 요청 5 + 대화 중 추가 2:

| # | 요청 | 결과 |
|---|---|---|
| 1 | 엑셀에 기입 안 된 값 채우기 | `계수` 시트에 없던 값 4개(생성 랜덤 범위·폴백 공속/이속), `공식` 시트 빈 예시 1칸, `#NAME?` 로 깨져 있던 1칸, `능력치` 시트 빈 비고 3칸 |
| 2 | **공속·이속이 능력치 100 이상에서도 적용되게** | 하드 상한 → **점근 곡선**. 예전 식은 공속 40·이속 36 에서 상한에 닿아 그 위가 전부 죽었다 |
| 3 | 프레이야 스킨 제작 | `Skin_Preyja` + `Character_9003_Preyja` 생성. **원본 Idle 이 깨져 있어 복원**했다 |
| 4 | 모든 스킨에 투사체 — 객체별 관리 | `CharacterSkinSO`/`TowerSkinSO` 에 투사체 필드. 연출 코드의 진영·종류 분기는 폴백으로만 |
| 5 | 스킨 회복 모션 (없으면 공격 모션) | `healRight/healLeft` + `SkinAttackMotion` 3분기. 회복 → 원거리 → 근접 폴백 |
| 6 | 스킨 이름을 캐릭터/몬스터에 맞게 | `Skin_Angel`→`Skin_Elin` 외 3건 + 아트 폴더 4개 |
| 7 | `ProjectileBurst` 는 섬광이 아니라 **맞았을 때 범위 표시** | 발사 섬광과 **착탄**을 다른 개념으로 분리. 착탄은 맞는 쪽에서, 비행 시간만큼 지연 후 재생 |

### 소유권 (§2)

**UI 소유 — 문제 없음**
- `Scripts/Combat/BalanceConfigSO.cs` · `CharacterSkinSO.cs` · `TowerSkinSO.cs` ·
  `CharacterAnimator.cs` · `CombatProjectileFx.cs`
- `Data/Combat/BalanceConfig.asset`
- `Resources/Skins/**` · `Resources/MonsterSkins/**` · `Resources/BuildingSkins/**` ·
  `Resources/Characters/**` (§2 의 `Assets/_Project/Resources/**`)

**⚠️ §2 와 어긋나는 부분 2가지 — 유저 판단 필요**

1. **`Tools/**` 는 §2 상 PROTO 소유**인데 이번에 3개를 추가/수정했다:
   `stats_sheet_revise.py`(신규) · `char_asset_preyja_build.py`(신규) ·
   `gen_skin_assets.py`(신규) · `gen_character_assets.py`(수정).
   **선례가 있다** — 진행상황 35절이 캐릭터 에셋 생성기를 "스크래치패드는 다음 세션에 없다"는
   이유로 `Tools/` 로 옮겼고, 36절도 `Tools/crop_illust_faces.py` 를 넣었다. 즉 이 브랜치가
   이미 `Tools/` 를 쓰고 있다. **§2 항목을 갱신하거나(§2 는 단독 변경 금지 — 유저 승인 필요)
   생성기 전용 폴더를 UI 소유로 새로 정하는 편이 낫다.**
   PROTO 가 실제로 쓰는 `Tools/wall_depth_pass.py` · `wall_extrude_pass.py` 는 **무접촉**이다.
2. **`Art/Char_Asset/**` 는 §2 어느 쪽에도 안 적혀 있다.** PROTO 소유로 적힌 것은
   `Art/Tiles` · `Art/OrganicTilemap` · `Art/Units` 뿐이다. `Char_Asset` 은 25·27·30절부터
   이 브랜치가 계속 만들어온 폴더라 사실상 UI 소유로 다뤘다(폴더 4개 개명 + 프레이야 44장 추가).

그 외 PROTO 소유 경로(`Scripts/Wave|Map|Fog`, `Units/Monster*`, `Data/Units|Map`,
`Art/Tiles|OrganicTilemap|Units`)는 **무접촉**.

### 씬 변경 여부

**없음.** 스킨·캐릭터 정의는 전부 `Resources` 경로 로딩이라 씬에 배선할 참조가 하나도 없다
(§10 의 이유와 같다). `Character_Template` 의 `CharacterAnimator.skinResourceFolder` 도
`Skins` 그대로다. 스킨 에셋 이름을 바꿨지만 **`.meta` 를 같이 옮겨 guid 가 유지**되므로
씬·에셋 어느 쪽도 참조가 끊기지 않았다.

⚠️ **에셋(.asset)은 MCP 로 만들 수 없다** — SO 를 만드는 도구도, 스프라이트 참조를 넣는
도구도 없다(진행상황 8절 1·4번). 그래서 스킨·캐릭터 정의만 생성 스크립트로 쓰고
**씬 오브젝트·컴포넌트는 MCP** 라는 원칙은 유지된다. 이번엔 씬 변경 자체가 없었다.

### 검증

`recompile_scripts` **에러 0 / 경고 1** — 경고는 `SquadPanel.emptySlotText` 미사용으로
**UI-9(36절)에서 생긴 것이고 이번 작업과 무관**하다. `Assets/Refresh` 후 콘솔 에러·경고 0
(새 PNG 44장 `.meta` + 스킨 에셋 6개 정상 임포트). 상세 목록은 진행상황 38-8절.

### 씬반영요청 목록

없음.

---

## UI-12. 프레이야 걷기 재제작(Idle 재해석) · 보스 크기 2x3 타일 (2026-08-11)

> 상세는 `진행상황.md` **40·41절**. MCP 함정은 **8절 10번**.

### 무엇을 했나

1. **프레이야 걷기를 Idle 에서 합성했다.** 원본 `Move` 8장이 웅크린 돌진 포즈라 직립 Idle 과
   실루엣이 완전히 달랐다(엘린·비기오르는 Walk 가 Idle 과 같은 직립에 다리만 움직인다).
   Idle 3프레임에 **기울임 3px · 자락 흔들림 ±3px · 바운스 2px** 를 겹쳐 6프레임 주기를 만들었다.
   전부 **행 단위 정수 이동**이라 픽셀이 뭉개지지 않는다. 캔버스가 316x147 → 218x147 로 줄었다.
2. **보스 크기를 6x6 → 가로 2 · 세로 3 타일로** 바꿨다. 씬 템플릿 스케일 (1, 1.5, 1) +
   `Monster_Boss.asset` 의 `footprintTiles` 3 → 1 (1보다 크면 스포너가 균일 스케일로 덮어쓴다).

### 소유권 (§2)

**UI 소유** — `Tools/char_asset_preyja_build.py`(§2 이슈는 UI-11 에 적은 그대로),
`Resources/Skins/Skin_Preyja.asset`, `Art/Char_Asset/Char_Asset_Preyja/**`,
`Assets/Scenes/Proto_01.unity`.

**⚠️ PROTO 소유 파일 1건을 건드렸다** — `Data/Units/Monster_Boss.asset` 의
`footprintTiles: 3 → 1` **한 값**. 유저가 직접 지시한 보스 크기 변경이고, 이 값을 안 내리면
`MonsterSpawner`(PROTO 소유 코드)가 스폰 때 균일 스케일로 덮어써서 **씬에서 아무리 고쳐도
반영되지 않는다.** 코드를 고치지 않는 대신 데이터 한 값으로 끝낸 선택이다.
`MonsterSpawner.cs` 자체는 **무접촉**.

### 씬 변경 여부

**있음.** `Templates/Monster_Templates/Monster_Boss_Template` 의 `m_LocalScale` → `(1, 1.5, 1)`.
커밋된 씬과 비교해 **줄 수 1,404,919 동일 · GameObject 326개 동일 · Transform 33개 동일** —
그 한 줄 말고는 바뀐 것이 없다(U-S1 의 "변경 최소화" 준수).

### ⚠️ 이번에 씬을 오염시켰다가 되돌린 일 (기록)

`update_gameobject` 로 보스 템플릿을 건드리려 했는데, **비활성 오브젝트라 MCP 경로 조회가 못 찾고
"없으면 만든다" 동작으로 `Templates` → `Monster_Templates` → `Monster_Boss_Template` 빈 껍데기
3개가 새로 생겼다.** 이어 부른 `set_transform` 이 진짜 템플릿이 아니라 껍데기에 적용됐고
**응답은 둘 다 "성공"이었다.** `grep "m_Name: ..."` 개수를 커밋본과 비교해서 발견했다.

`delete_gameobject` 는 권한 거부로 막혀서, 씬 YAML 에서 블록 4개 + 부모 `m_Children` 항목 +
`SceneRoots` 루트 등록을 지우고 `load_scene` 으로 리로드해 되돌렸다.
**앞으로 비활성 오브젝트는 `get_gameobject` 로 찾아지는지 먼저 확인하고, 안 찾아지면
`update_gameobject` 를 그 경로에 쓰지 말 것** (진행상황 8절 10번).

### 검증

`Assets/Refresh` · `load_scene` 후 **콘솔 에러·경고 0**. 걷기 6프레임이 방향별로 전부 다른
파일임을 md5 로 확인. `Skin_Preyja` 재생성(walk 6/6), 빈 줄 0. 씬 구조 커밋본과 동일.

### 씬반영요청 목록

없음.

---

## UI-13. 교전 고정 · 동료 구원 · 후퇴 사격(카이팅) (2026-08-11)

> 상세는 `진행상황.md` **42·43절**.

### 무엇을 했나

유저 리포트: **"중위나 후방으로 뒀을때 최대 사거리를 유지하려다가 전투 지역에서 벗어날 정도로
무빙을 해버리는게 어색하다"**. 31-3절의 전열 유지가 매 프레임 "타겟에서 N타일" 지점으로
이동하는데, 적이 다가오는 만큼 그 점도 밀려서 끝없이 뒷걸음질을 쳤다.

| # | 변경 | 파일 |
|---|---|---|
| 1 | **교전 고정** — 타겟이 한 번이라도 사거리 안에 들어오면 유지 거리로 물러나는 분기를 건너뛴다. 쫓기·최소 사거리 회피는 그대로 | `Combat/UnitCombat.cs` |
| 2 | **동료 구원** — 동료를 때리는 적은 사거리 밖이라도 잡으러 간다. 교전 고정과 "대기" 반응을 푸는 유일한 조건 | `Combat/UnitCombat.cs` |
| 3 | **후퇴 사격** — `SetRetreatFiring()` 신설. 이동은 후퇴 지점 고정, 사거리 안의 적만 사격. 체력 후퇴(본인/전방 아군)에서만 켜진다. 공포는 여전히 전투를 끈다 | `Combat/UnitCombat.cs` · `Units/CharacterBehavior.cs` |
| 4 | **카이팅 방향** — 이동 중엔 진행 방향, 공격 순간엔 타겟(투사체) 방향. 뒷걸음질 제거 | `Combat/CharacterAnimator.cs` |

### 소유권 (§2)

- **UI 소유** — `Scripts/Combat/UnitCombat.cs` · `CharacterAnimator.cs` (§2 의 `Scripts/Combat/**`).
- ⚠️ **`Scripts/Units/CharacterBehavior.cs`** — §2 의 PROTO 목록은 `Units/Monster*` 만
  지정하고 있고, `CharacterBehavior` 는 전투 AI 이관(v2) 이후 이 브랜치가 계속 만들고
  고쳐온 파일이다(28·31·36절). 이번에도 같은 전제로 수정했다.
- PROTO 소유 경로(`Scripts/Wave|Map|Fog`, `Units/Monster*`, `Data/Units|Map`,
  `Art/Tiles|OrganicTilemap|Units`, `Tools/wall_*`)는 **무접촉**.

### 씬 변경 여부

**없음.** 신규 `[SerializeField]` 4개(`answerAllyCalls` / `allyCallMemorySeconds` /
`allyCallRange` / `frontRetreatCheckInterval`)는 씬에 저장돼 있지 않아 코드 기본값
(✔ / 2초 / 12타일 / 0.25초)이 그대로 적용된다 — UI-9 와 같은 방식이다.
씬 저장이 38MB 재작성이라 값을 넣으려고 저장하지는 않았다(U-S1).
조정이 필요하면 인스펙터에서 만지면 그때 씬에 남는다.

### 만들면서 잡은 함정 2개 (같은 실수 방지)

1. **동료 구원 판정을 타겟 선정 시점에 캐시했더니** 재탐색 간격(0.2초) 동안·억제·사냥 경로에서
   옛 값이 남아 엉뚱한 순간에 자리를 떴다 → `DecideState` 에서 매 프레임 다시 계산하도록 고쳤다.
2. **"전방 아군"을 적과의 거리로 판정했더니** 물러나는 도중 적이 사거리 밖으로 나가
   `Target` 이 null 이 되는 순간 판정이 뒤집혀 **후퇴/복귀를 반복하며 떨었다** →
   **넥서스로부터의 거리**(이 프로젝트가 전열을 정의하는 방식, 36절)로 바꾸고,
   한 번 따라 물러나면 그 상대가 끝낼 때까지 붙잡도록(`_followingRetreatOf`) 했다.

### 검증

재컴파일 **에러 0 / 경고 1**(기존 `SquadPanel.emptySlotText`, 무관).
`Character_Template` 의 `flipSpriteToFaceMovement: 0` 확인 — 방향 결정의 주인이
`CharacterAnimator` 하나뿐이라 `UnitCombat.FaceMovement` 와 충돌하지 않는다.
`CombatState` 를 외부에서 읽는 곳이 없음을 `grep` 으로 확인.
**플레이 검증은 유저 몫**(진행상황 42-6절에 확인 항목 4가지).

### 씬반영요청 목록

없음.

---

## UI-14. 전술 지침 "후퇴 시 행동" + 교전 개시 위치 한계 (2026-08-11)

> 상세는 `진행상황.md` **44·45절**.

### 무엇을 했나

1. **전술 지침에 "후퇴 시 행동" 추가** — `공격 유지`(기본) / `동료와 함께 후퇴`.
   42-3절에서 무조건 켜뒀던 "전방을 따라 물러난다"가 이제 캐릭터별 선택이 됐다.
   **전방 포지션은 '공격 유지'로 고정**(따라 물러날 대상이 없다) — 강제 지점을
   `TacticalOrder.Normalize()` 한 곳에 모아 UI·복사·인스펙터 모든 경로가 지나게 했다.
2. **동반 후퇴는 최대 사거리를 유지한다** — 넥서스까지 도망가지 않고
   `적 위치 + 넥서스 방향 × 내 사거리` 지점을 계속 잡는다.
3. **교전 개시 위치 잡기에 한계 거리**(`openingRepositionMaxTiles`, 4타일).
   원거리·마법이 최대 사거리로 물러나 시작하는 동작은 **이미 있었고**(DecideState 의
   유지-거리 분기가 공격 분기보다 앞), 문제는 그 구간에 한계가 없어 적이 더 빠르면
   전투 지역을 벗어난다는 것이었다.

### 씬 변경 여부 — **있음** (전부 MCP)

유저 지시: "모든 객체 생성과 수정은 하드 코딩 하지말고 mcp 연결해서 직접 시도
단 템플릿 복제는 예외적으로 허용".

- `Col1/React`(세로 2단 버튼 그룹)를 **`duplicate_gameobject`** 로 복제 →
  `Col2/RetreatAction`, 자식 `Chase`→`Keep` · `Hold`→`WithAlly` 개명.
- `Col2/RetreatLabel` · `Col2/RetreatHint` 복제 → `RetreatActionLabel` · `RetreatActionHint`.
- 문구·위치·크기는 `update_component`(`m_text` / `anchoredPosition` / `sizeDelta`).
- **패널·컬럼 크기는 안 늘렸다** — Col2 가 −442 에서 끝나 200px 가 비어 있었다.
  다른 요소가 하나도 안 움직이는 것이 U-S1(씬 변경 최소화)에도 맞다.

### ⚠️ 비활성 패널을 MCP 로 만질 때의 절차 (UI-12 사고 이후)

UI-12 에서 `update_gameobject` 가 오브젝트를 새로 만들어버린 사고가 있었다. 이번엔
**만지기 전에 `UI_Root` · `HUD_Tactics` 이름이 각각 1개뿐임을 grep 으로 확인**하고 시작했고,
작업 뒤 이름별 개수와 GameObject 총계(326 → 333, 정확히 만든 것 7개)를 다시 확인했다.
UI-12 의 원인은 **같은 이름의 루트가 3개**여서 경로 첫 조각이 엉뚱한 것에 걸린 것으로 보인다 —
이름이 유일하면 비활성이어도 제대로 찾아간다.

### 소유권 (§2)

UI 소유 — `Scripts/Combat/TacticalOrder.cs` · `CharacterTactics.cs` · `UnitCombat.cs`,
`Scripts/UI/TacticalOrderPanel.cs`, `Assets/Scenes/Proto_01.unity`.
`Scripts/Units/CharacterBehavior.cs` 는 UI-13 과 같은 전제(전투 AI 이관 이후 이 브랜치 소유).
PROTO 소유 경로 무접촉.

### 검증

재컴파일 **에러 0 · 경고 0**(`SquadPanel` 경고는 이번 재컴파일 로그에 안 잡혔다 —
증분 컴파일이라 해당 파일이 안 돌았을 뿐, 코드는 그대로다).
`HUD_Tactics` 는 작업 후 다시 `m_IsActive: 0` 으로 되돌렸다.
**화면 확인은 유저 몫**(진행상황 44-6절에 확인 항목 3가지).

### 씬반영요청 목록

없음.

---

## UI-15. 크기·범위 기준을 전부 「타일」로 (보스가 잡몹보다 작던 문제) (2026-08-13)

> 상세는 `진행상황.md` **61절**.

### 무엇을 했나

1. **원인** — 크기를 **배율**로 적고 있었다(`spriteScale` 0.75 · `projectileScale` 0.55 …).
   그 숫자는 "원화가 몇 픽셀인지 · PPU 가 얼마인지"를 보고 손으로 고른 값이라 **원화가 바뀌면
   게임 안 크기가 같이 흔들린다.** 실측 결과 단탈리온이 **2.00 x 1.40 타일** 로,
   잡몹(2.62 x 1.92)보다도 작고 중간보스(5.24 x 3.84)의 1/3 이었다.
2. **기준을 타일로 바꿨다.** 정의·스킨에는 "몇 타일로 보일지"만 적고, 배율은 코드가
   `목표 세로(타일) ÷ 스킨 실측 세로(타일)` 로 계산한다. **균등 배율이라 비율이 안 깨진다.**
3. **실측은 `Tools/measure_skin_tiles.py`(신규)가 알파 경계로 잰다.** 유니티의 `Sprite.bounds` 는
   캔버스(여백 포함) 기준이라 못 쓴다 — 엘린은 캔버스 189px 에 그림 130px, 피올로는 64px 에 52px 다.
4. **발판(근접 거리 판정)도 보이는 크기를 따라간다**(유저 확정) — `MonsterUnit.BodyRadiusTiles`.
5. **착탄 연출은 마법의 실제 피해 범위(`UnitCombat.MagicAreaTiles`)로 그린다** — 보이는 범위와
   맞는 범위가 같아진다.
6. **넥서스 근접 반경이 `transform.localScale.x`(픽셀 배율)를 읽던 것을 `footprintTiles`(타일)로** 바꿨다.

### 값 (유저 확정 2026-08-13)

| 대상 | 렌더 세로(타일) | 결과 가로 |
|---|---:|---:|
| 단탈리온(최종보스) | **5** | 7.13 |
| 중간보스 2종 | **3** | 4.09 / 4.32 |
| 지옥 송곳니 | 1.9 | 2.59 |
| 영혼 사수 | 1.7 | 2.45 |
| 캐릭터 4명 | 2.15 (씬 템플릿) | 원화 비율 |
| 포탑 | 4.6 | 2.15 |

### 소유권 (§2)

**UI 소유** — `Scripts/Combat/CharacterSkinSO.cs · TowerSkinSO.cs · CharacterAnimator.cs ·
TowerAnimator.cs · CombatProjectileFx.cs · UnitCombat.cs`, `Resources/**`(스킨 에셋),
`Assets/Scenes/Proto_01.unity`.

**⚠️ PROTO 소유 파일을 건드렸다** — `Scripts/Units/MonsterDefinitionSO.cs`(필드 추가만) ·
`MonsterSpawner.cs`(스케일 한 줄) · `MonsterUnit.cs`(몸집 반경) ·
`Scripts/Buildings/BuildingDefinitionSO.cs · TowerUnit.cs`(필드 추가·호출 한 줄) ·
`Data/Units/**`(`renderHeightTiles` 한 줄씩) · `Data/Buildings/Building_Turret.asset` ·
`Tools/measure_skin_tiles.py`(신규). **기존 필드는 하나도 지우거나 개명하지 않았다** —
구식 배율(`spriteScale`/`projectileScale`/`impactScale`)은 폴백으로 그대로 남겼다(U-D3·U-D4).
UI-12 와 같은 종류의 크로싱이고, PROTO 브랜치는 `7047af4` 이후 움직이지 않았다.

---

## UI-16. 중간보스 전용 템플릿 2개 생성 + 스포너 슬롯 배선 (2026-08-13)

> 상세는 `진행상황.md` **63절**.

### 무엇을 했나

중간보스는 지금까지 **템플릿이 없어** `ResolveMidBossTemplate` 의 폴백(같은 공격 타입의 잡몹
템플릿)으로 스폰되고 있었다(59-4절). 전용 템플릿을 만들어 `midBossSlots` 에 직접 연결했다.

- `Monster_MidBoss_HellFang_Template`(혈인 110001) · `Monster_MidBoss_SoulArcher_Template`
  (공허의 속삭임 110002) — 잡몹 템플릿을 **MCP `duplicate_gameobject`** 로 복제. 비활성(U-S5).
- 스킨은 잡몹 것 그대로(전용 원화 없음). 크기는 정의 테이블이 정한다(세로 3타일, 61절).
- `midBossSlots[].template` 배선은 **MCP 가 구조체 배열을 거부**해서(미결 116번) 씬 YAML 패치 →
  `load_scene` → **Unity 가 스스로 저장한 뒤에도 값이 남아있는지**로 반영 확인.

### 소유권 (§2)

**UI 소유** — `Assets/Scenes/Proto_01.unity`. 씬 외 변경 없음.

### 검증

GameObject **347 → 349**(만든 2개뿐, 껍데기 증식 없음) · 새 이름 각 1개 · `m_IsActive: 0` ·
슬롯 2개가 새 템플릿을 가리킴 · 콘솔 에러 0.

---

## UI-17. 중간보스 영어 이름 (BloodMark · VoidWhisper) + 빈 Templates 루트 정리 (2026-08-13)

> 상세는 `진행상황.md` **64절**.

### 무엇을 했나

1. **중간보스만 영어 이름이 없었다** — `wave_mid_boss` 시트에 `character_name_EG` 컬럼 자체가
   없어서 에셋·템플릿이 물려받는 잡몹 이름(`Monster_MidBoss_HellFang`)을 쓰고 있었다.
   표에 컬럼을 만들고 **BloodMark(혈인) · VoidWhisper(공허의 속삭임)** 를 넣었다.
   ⚠ 표 편집은 **Excel COM** — openpyxl 저장은 이 파일의 하이퍼링크 12칸을 날린다(51-3절).
2. `gen_string_table.py` 에 en 규칙 추가 → 스트링 키 테이블·`StringTable.txt` 재생성.
3. 에셋 2개 개명(`.meta` 동반 → guid 유지) · 씬 템플릿 2개 개명(MCP).
4. `sync_tables_to_assets.py` 가 중간보스 에셋을 **전체 재작성**하면서 61절의
   `renderHeightTiles: 3` 을 날려서, 그 값을 **생성 코드에 넣었다**(`render_h=3`).
5. **빈 `Templates` 루트 2개 삭제** — Transform 하나뿐·자식 0·참조 0 인 껍데기.
   `main` 에는 1개뿐이었고 `86a45c0` 에서 3개가 됐다(8절 10번의 그 함정).

### 소유권 (§2)

**UI 소유** — `Assets/Scenes/Proto_01.unity`, `Resources/Data/StringTable.txt`.
**⚠ PROTO 소유** — `Data/Units/**`(개명 2건) · `Tools/gen_string_table.py` ·
`Tools/sync_tables_to_assets.py`. UI-15 와 같은 종류의 크로싱이고 PROTO 는 `7047af4` 이후 정지 상태.

### 검증

하이퍼링크 12칸 유지 · guid 커밋본과 동일 · 파이프라인 재실행 후 크기 값 유지 ·
씬 새 이름 각 1개/옛 이름 0개 · `midBossSlots` 배선 유지 · GameObject **349 → 347** · 콘솔 에러 0.

---

## UI-18. 몬스터 렌더 크기 하드코딩 제거 - 표에 컬럼 2개 신설 (2026-08-13)

> 상세는 `진행상황.md` **65절**.

### 무엇을 했나

UI-17 에서 파이프라인이 크기 값을 날려서 스크립트 안(`MID_BOSS` 딕셔너리)에
`render_h=3` 을 리터럴로 박아넣었는데, 유저가 이건 하드코딩이라고 정확히 짚었다.

- `웨이브 몬스터 테이블.xlsx` / `first_Stat` 에 `render_height_tiles` · `render_width_tiles`
  컬럼 신설(기존 14컬럼 순서 유지, 끝에 추가 — 위치 인덱스로 읽는 기존 코드 안 깨짐).
  값은 61절에서 이미 확정한 것 그대로(1.9/1.7/3/3/5, 가로는 계산값).
- 스크립트가 잡몹·최종보스·중간보스 **5종 전부** 표에서 `renderHeightTiles` 를 읽도록 통일.
  `render_h` 리터럴 삭제.
- **가로 칸은 게임에 적용하지 않는다** — `CharacterAnimator` 는 세로 하나로 균등 배율을
  계산하고 가로는 원화 비율대로 저절로 따라온다(61절, 찌그러짐 방지). 가로 칸은 참고용
  기록이고, `check_render_width()` 로 표의 가로가 실측과 3% 넘게 다르면 경고만 한다.

### 소유권 (§2)

**⚠ PROTO 소유** — `Tools/sync_tables_to_assets.py`. UI-15·UI-17 과 같은 크로싱.

### 검증

다른 시트 하이퍼링크 12칸 유지 · 재실행 후 5개 에셋 값 불변(멱등) · 경고 0 · 콘솔 에러 0.

---

## UI-19. 콜라이더 기준 크기 로직 재설계 (2026-08-13)

> 상세는 `진행상황.md` **66절**.

### 무엇을 했나

65절의 "세로 하나 → 배율" 방식을 유저가 정정: **"표에 콜라이더 값 → 비율 안 깨지는 이미지
크기 계산 → 이미지 삽입 → 그 이미지에 콜라이더 재설정"** 3단계 로직으로 바꿨다.

- 이 프로젝트 유닛엔 `Collider2D`가 없다(U-D9) — "콜라이더"의 실체는
  `UnitCombat.TargetRadius`가 읽는 `MonsterUnit.BodyRadiusTiles`.
- `CharacterAnimator`에 `colliderWidthTiles/HeightTiles` + `SetColliderBoxTiles()` 신설.
  상자 안에 들어가는 최대 배율(contain, 균등)을 계산 → `ColliderSizeTiles`(재설정된 콜라이더,
  표 희망값이 아니라 실측 결과)를 공개.
- `MonsterUnit.BodyRadiusTiles`가 이 재설정된 콜라이더를 읽도록 변경.
- 표 컬럼 `render_*` → `collider_*` 개명, 값 소수점 한 자리로 정리(float 유지).
- `sync_tables_to_assets.py`가 계산 결과를 콘솔에만 찍는다(에셋엔 저장 안 함 — 원화가
  바뀌면 결과도 바뀌어야 하므로 런타임 계산이 정본).

### 부수 조사 (유저 요청)

캐릭터 패시브 12종 전부 코드 참조·씬 배선 확인(정상). 보스 스킬 2종은 여전히 미구현
(코드에 흔적 없음, 미결 111번).

### 소유권 (§2)

**UI 소유** — `CharacterAnimator.cs`. **⚠ PROTO 소유** — `MonsterDefinitionSO.cs` ·
`MonsterSpawner.cs` · `MonsterUnit.cs` · `Tools/sync_tables_to_assets.py`(UI-15·17·18과 동일 크로싱).

### 검증

recompile 에러 0·경고 0, 콘솔 에러 0, 재실행 멱등 확인.

---

## UI-20. 보스 스킬 2종 구현 · 보스 피격 침식 · 피올로 스킨/일러스트 · 미니맵 테두리 (2026-08-13)

> 상세는 `진행상황.md` **67절**.

### 무엇을 했나

1. **보스 스킬 2종 (미결 111번 해소)** — 표(`Skill` 시트)의 130001 타락한 무덤 ·
   130002 공허의 광선을 실제로 발동시킨다. 신규 `BossSkillType` · `BossSkillSO` ·
   `BossSkillCaster`. 범위는 **보스 자기 칸에서 조준 방향으로 뻗는 직사각형**
   (5x3 / 15x3 타일, 4방향 정렬), 피해는 `TakeDamageFrom(공격자, 퍼센트)` 새 오버로드.
   조준은 타락한 무덤 = 가장 가까운 적, 공허의 광선 = 가장 먼 적(스트링 테이블 그대로).
   `SpecialShockwave`/`SpecialBeam` 시전 원화와 `Fx` 지면 연출을 스킨에 배선했다 —
   59-3절이 임포트만 해두고 놀리던 프레임이다.
2. **보스에게 맞으면 침식 +10** (유저 확정) — `ErosionService` 인스펙터 3칸 신설
   (`erosionPerBossHit` 10 · `midBossCountsAsBoss` ✔ · `bossHitErosionCooldown` 0).
   **붙어 있는 오브젝트는 씬의 `GameSystems`** 다(29-2절과 같은 자리).
   판정은 신규 `DamageableUnit.OnAnyHit` — `OnAnyAttack` 은 명중 판정 **전에** 나서
   빗나간 공격까지 세므로 "피격"에 쓸 수 없다.
3. **피올로 스킨 4배 재구성** — 혼자만 원화가 64x64(다른 캐릭터의 1/3 밀도)였다.
   `Tools/piolo_skin_rebuild.py`(신규)가 볼트 원본을 읽어 66프레임을 256x256 으로
   재구성하고 `.meta` PPU 를 21 → 84 로 같이 올린다 → **게임 안 크기 불변**.
4. **피올로 일러스트 연동** — 볼트에 있던 `illust_Piolo.png` 가 임포트되지 않아
   `Character_9004_Piolo.illustName` 이 빈 참조였다. `crop_illust_faces.py` 에 항목을
   추가해 다른 3명과 같은 규칙(얼굴 확대 · 420x368 · 비율 1.1413)으로 잘랐다.
   ⚠ 그 스크립트의 출력 경로가 옛 프로젝트 경로(`Last Sanctuary`)로 남아 있어
   **돌려도 아무 데도 안 써지고 있었다** — 같이 고쳤다.
5. **미니맵 테두리** — `HUD_Minimap/Border`(패널 외곽) · `ViewBorder`(지도 영역)에
   2px 띠 4개씩, 전부 MCP 로 생성. 패널 배경 알파 0.82 → 0.94.

### 겪은 함정

- **`gen_new_skins.py` 가 `Skin_Dantalian` 을 `Resources/Skins/` 에 쓰고 있었다.**
  그 폴더는 **캐릭터**가 무작위로 뽑는 후보 폴더라, 다시 돌리면 캐릭터가 최종보스
  외형으로 튀어나온다. 누군가 손으로 `MonsterSkins/Dantalian/` 로 옮겨서 가려져
  있던 것 — `folder` 인자를 만들어 고치고 유령 사본을 지웠다.
  같은 스크립트가 `.meta` 를 매번 덮어써 guid 를 갈아치우던 것도 "없을 때만"으로 고쳤다.
- `gen_new_skins.py` 는 스킨을 **통째로 다시 쓴다** → 실측 크기(`contentSizeTiles`)가
  날아간다. 64절에서 하드코딩으로 때웠던 사고와 같은 것이라, 스크립트 끝에
  "이어서 `measure_skin_tiles.py` 를 돌릴 것"을 출력하게 했다.
- 업스케일(Lanczos)이 원화 **바깥**으로 알파 1~2/255 짜리 링잉을 흘려 몸집 실측값이
  2.8% 커졌다. `ALPHA_CUT`(2/255)로 잘라 원본과 같은 크기로 맞췄다.
- FX 원화 피벗이 전부 발밑(0.5, 0)이라 범위 연출을 그대로 놓으면 상자와 어긋나고
  세로로 쏠 때 피벗을 축으로 돌아버린다 → `Sprite.bounds.center` 로 피벗 보정.

### 소유권 (§2)

**UI 소유** — `Scripts/Combat/BossSkillType.cs · BossSkillSO.cs · BossSkillCaster.cs`(신규) ·
`DamageableUnit.cs · UnitRegistry.cs · ErosionService.cs · CharacterSkinSO.cs ·
CharacterAnimator.cs · CombatProjectileFx.cs`, `Resources/BossSkills/**`(신규) ·
`Resources/Skins/**` · `Resources/Illust/**`, `Assets/Scenes/Proto_01.unity`.

**⚠ PROTO 소유 파일을 건드렸다** — `Scripts/Units/MonsterDefinitionSO.cs`(`bossSkillIds` **추가만**) ·
`Data/Units/Monster_Dantalian.asset`(스킬 id 2줄) · `Resources/MonsterSkins/Dantalian/Skin_Dantalian.asset` ·
`Art/Char_Asset/Char_Asset_Piolo/**`(재구성 66프레임) ·
`Tools/sync_tables_to_assets.py · gen_new_skins.py · crop_illust_faces.py · piolo_skin_rebuild.py`(신규).
**기존 필드·시그니처는 하나도 지우거나 바꾸지 않았다**(U-D3·U-D4 — `TakeDamageFrom` 은 오버로드로 얹었다).
UI-15·17·18·19 와 같은 종류의 크로싱이고 PROTO 는 `7047af4` 이후 정지 상태다.

### 씬 변경 여부 — **있음** (전부 MCP, 저장 1회)

`HUD_Minimap` 하위 10개 신설(GameObject 347 → 357) · `Monster_Dantalian_Template` 에
`BossSkillCaster` 추가 · `GameSystems/ErosionService` 새 필드 3개.

### 검증

`recompile_scripts` 에러 0·경고 0 · 콘솔 에러 0 · 파이프라인 재실행 멱등 ·
`Skin_Dantalian` 은 +17줄(스킬 칸)만 늘고 실측값 유지 · 씬 GameObject 정확히 +10.
**플레이 모드 검증은 안 했다**(§11-5) — 유저가 직접 볼 것.

### 씬반영요청 목록

없음.
