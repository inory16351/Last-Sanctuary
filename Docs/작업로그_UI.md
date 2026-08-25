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

---

## UI-21. 보스 피격 침식을 스킬 테이블 컬럼으로 이전 · 보스/캐릭터 스킬 동시발동 방지 (2026-08-13)

> 상세는 `진행상황.md` **68절**.

### 무엇을 했나

유저가 UI-20의 침식 구현을 정정: "시스템 로직 변경 게임 시스템에 넣지말고 보스 스킬
테이블에 침식 수치 칼럼 추가" + "쿨이 동시에 돌면 쿨타임이 더 긴 스킬부터 쓰도록
로직 만들어 보스/캐릭터 둘 다 동일 사항".

1. **침식 값을 시스템(`GameSystems/ErosionService`)에서 스킬 데이터로 이전.**
   웨이브 몬스터 테이블.xlsx / `Skill` 시트에 `value_04`(밸류타입_04) 컬럼을 기존
   9칸 형식 그대로 뒤에 붙였다(타락한 무덤 5 · 공허의 광선 10). 스트링 키 테이블의
   두 `skill_type_desc_*` 설명문에도 "맞은 적은 침식이 {value_04} 만큼 오른다"를
   추가했다. `BossSkillSO.value04`/`ErosionValue` 신설, `BossSkillCaster.TryCast`
   가 피해를 넣은 직후 대상이 `CharacterUnit`이면 `CharacterErosion.AddErosion`을
   직접 부른다. `ErosionService`의 `erosionPerBossHit`/`midBossCountsAsBoss`/
   `bossHitErosionCooldown`과 `DamageableUnit.OnAnyHit`(이 용도로만 썼다)를 제거해
   29-2절 원래 형태로 되돌렸다.
2. **동시발동 방지 — 쿨타임 긴 스킬 우선.** `BossSkillCaster`에 `_priorityOrder`
   (슬롯을 `coolTime` 내림차순으로 정렬한 목록, `EnsureResolved`에서 한 번만 계산)를
   추가해 `Update()`가 인덱스 순서 대신 이 순서로 시도하게 바꿨다. 캐릭터 쪽도 같은
   패턴 — `CharacterPassives`에 `_cooldownPriority`를 추가하고, 기존
   `TickSacrifice`/`TickCalmDown`/`TickPurifyingTouch`를 `bool`을 돌려주는
   `TrySacrifice`/`TryCalmDown`/`TryPurifyingTouch`로 바꿔 `TickCooldownSkills()`가
   우선순위 순으로 시도하다 하나가 발동하면 그 프레임엔 멈춘다. 지금 이 셋을 동시에
   가진 캐릭터는 피올로(정신 안정 180초 · 정화의 손길 120초)뿐이지만 구조는 임의의
   조합에 대해 일반적으로 동작한다.
3. **보스 스킬 시전 모션(`SpecialShockwave`/`SpecialBeam`)은 UI-20에서 이미 스킨에
   배선돼 있었다** — 이번에 다시 확인만 했고 코드·에셋 변경은 없었다.

### 겪은 함정

- 스트링 키 테이블의 문장을 마침표 없이 그대로 이어붙였다가("...공격한다 맞은 적은...")
  다른 `skill_type_desc_*` 항목들이 마침표로 문장을 가르는 관례라는 걸 늦게
  확인했다. 백업에서 되돌리고 마침표를 넣어 다시 적용했다.
- 웨이브 몬스터 테이블.xlsx의 `Skill` 시트에는 `skill_name`·`skill_explain` 칸에
  스트링 키 테이블로 넘어가는 하이퍼링크가 걸려 있다(UI-17이 이미 겪은 함정) —
  openpyxl 로 저장하면 날아가므로 새 스크립트(`add_boss_skill_erosion_column.py`)도
  Excel COM 으로만 셀 값을 바꿨다. 편집 후 하이퍼링크 4개(B/I열 두 행)가 그대로
  남아있는 것을 다시 열어 확인했다.

### 소유권 (§2)

**UI 소유** — `Scripts/Combat/BossSkillCaster.cs · BossSkillSO.cs · CharacterPassives.cs ·
DamageableUnit.cs · ErosionService.cs`, `Resources/BossSkills/**`,
`Resources/Data/StringTable.txt`, `Assets/Scenes/Proto_01.unity`.

**⚠ PROTO 소유 파일을 건드렸다** — `Tools/sync_tables_to_assets.py`(컬럼 읽기 한 줄) ·
신규 `Tools/add_boss_skill_erosion_column.py · add_boss_skill_erosion_string.py`.
UI-15·17·18·19·20 과 같은 종류의 크로싱.

### 씬 변경 여부 — **있음** (MCP 불필요, recompile 후 저장만)

`GameSystems/ErosionService`에서 필드 3개가 빠진 것 외에 하이라키 변경 없음.

### 검증

`recompile_scripts` 에러 0·경고 0 · 콘솔 에러 0 · `sync_tables_to_assets.py` 재실행
멱등(두 스킬 에셋 내용 불변) · `BossSkill_130001/130002.asset`에 `value04: 5`/`10`
반영 확인 · 하이퍼링크 4개 보존 확인.
**플레이 모드 검증은 안 했다** — 유저가 직접 볼 것(우선순위 로직이 실제로 큰 스킬을
먼저 내보내는지, 침식이 스킬마다 다르게 오르는지).

### 씬반영요청 목록

없음.

---

## UI-22. 미니맵 클릭 이동 · 보스 스킬 360도 조준 · 무리 소환 · 보스 칭호 외 11건 (2026-08-13)

유저 지시 11건을 한 번에 처리했다. 전제는 이번에도 **"모든 객체 생성 및 수정은
템플릿/슬롯 복제를 제외하고 MCP 로 직접"**.

| # | 지시 | 결과 |
|---:|---|---|
| 1 | 미니맵 클릭 시 화면 전환 (롤 미니맵처럼) | `MinimapPanel` 이 클릭·드래그를 받아 카메라 이동 |
| 2 | 보스 이동 속도 증가 (너무 느림) | 표 `movement_speed` 1 → 4 · 이동속도가 이제 표에서 온다 (1.4 → 2.389) |
| 3 | 포탈에서 여러 마리 나오게 (각개 격파 방지) | 웨이브표에 `spawn_group_size` 신설 + 무리 단위 소환 |
| 4 | 스킬 범위 360도 · 이미지에 맞춰 타격 범위 재조정 | 조준 4방향 → 자유각, 범위를 연출 원화 비율로 재계산 |
| 5 | Cast Seconds 를 스킬마다 | 표 `cast_time` 신설 (타락한 무덤 1.2 · 공허의 광선 2.5) |
| 6 | 긴 사거리 스킬은 항상 최대 사거리까지 | 조준은 방향만 정하고 길이는 언제나 표 값 (명시화) |
| 7 | 보스 이동/대기 모션 겹침 | `moveMotionHoldSeconds` 유지 시간으로 떨림 제거 |
| 8 | 프레이야 머리 위 검은 선 | 오른쪽 프레임 19장의 유령 띠 14개 제거 (빌드 스크립트에 내장) |
| 9 | 로그에 누가·무슨 스킬 | `HudLog.SkillLine` 한 형식으로 보스·패시브 통일 |
| 10 | 몬스터 로그 이름의 복제 번호 | `unit.name = DisplayName` · 로그는 `DisplayName` 을 읽는다 |
| 11 | 보스 체력바에 칭호 | 표 `boss_title` → `titleKey` → 체력바 첫 줄 |

### 1. 미니맵 클릭 이동 (롤 방식)

`MinimapPanel` 이 `IPointerDownHandler`·`IDragHandler` 를 구현한다. 누르면 그 지점으로,
누른 채 끌면 계속 따라간다(`CameraRigController.SnapTo`, 인스펙터의 `snapCamera` 를
끄면 부드럽게 미끄러지는 `FocusOn`).

- **자식 `View` 에 스크립트를 따로 붙이지 않았다** — 유니티 이벤트 시스템은 핸들러를
  부모로 거슬러 올라가며 찾으므로(`ExecuteEvents.ExecuteHierarchy`) 패널에서 그대로
  받는다. 씬에 오브젝트가 하나도 안 늘어난다.
- 클릭을 받으려면 `View`(RawImage)의 `raycastTarget` 이 켜져야 한다 — **MCP 로 켰다**
  (`false → true`). 코드에도 `Start()` 안전망을 뒀다(값 보정이라 §10 H-1 위반 아님).
- **U-D8 충돌 없음** — `CameraRigController.ignoreDragOverUI` 와 `UnitSelector` 의
  `IsPointerOverGameObject()` 가 UI 위 클릭을 이미 거른다. 미니맵은 UI 라 자동으로 비켜난다.

### 2. 보스 이동 속도 — ⚠ 원인은 "표를 안 읽고 있었다"

보스 에셋에 `moveSpeedTiles: 1.4` 가 **손으로 적혀** 있었다(잡몹 2.2 · 중간보스 2.25).
`sync_tables_to_assets.py` 는 **중간보스에만** 이동속도를 옮기고 있었기 때문이다 —
그래서 표를 아무리 고쳐도 잡몹·최종보스는 안 바뀌는 상태였다.

`sync_monsters()` 의 `patch_fields` 에 `moveSpeedTiles` 를 추가하고(공식은 38-1절
`mspd_from_stat` 하나뿐), 표의 `movement_speed` 를 **1 → 4** 로 올렸다.
결과: 보스 **1.4 → 2.389**(1.7배), 잡몹 2.2 → 2.25(공식값으로 정렬, 체감 차이 없음).

### 3. 무리 소환 — 웨이브표 `spawn_group_size` 신설

예전에는 한 마리씩, 그것도 **포탈을 돌아가며**(`PortalAt(i)`) 내보냈다. 그래서 어느
순간에도 화면에는 서로 다른 방향에서 온 한 마리씩만 있었다 — "각개 격파"의 실체가 이것이다.

- 웨이브테이블.xlsx / Sheet2 **H열 `spawn_group_size`** 신설 (웨이브 1~20 에 2~7).
- `WaveMonsterComposition.spawnGroupSize` 추가 · `MonsterSpawner.SpawnRoutine` 이
  **무리 단위로** 같은 포탈에서 한꺼번에 내보낸다.
- **소환 주기는 무리 개수로 역산**한다(`ResolveSpawnInterval(groupCount)`) — 마리 수로
  계산하면 무리 하나당 한 번만 기다리므로 전체 소환이 `groupSize` 배 빨리 끝나버린다.
- 무리가 커지면 흩어질 반경도 넓힌다(`GroupSpread` = `ceil(√개수)`) — 3x3 포탈 구역에
  7마리를 넣으면 한 덩어리로 겹쳐 나온 뒤 밀어내기가 한꺼번에 걸려 사방으로 튄다.
- **증원도 같은 문제였다** — `SpawnReinforcementBatch` 가 마리마다 포탈을 돌려서 증원
  4마리가 네 방향에 한 마리씩 흩어졌다. 무리 하나 = 포탈 하나로 바꿨다.
- 총 마리 수·능력치는 그대로다. 나오는 **방식**만 바뀐다. 값이 0·1 이면 예전 동작 그대로.

### 4. ★ 스킬 범위 — 4방향 스냅 제거 + 원화 비율로 범위 재계산

유저: *"단탈리온 스킬이 4방향에 적이 없으면 대각선 방향 적을 못 때리니까 의도랑 안 맞음"*
· *"보스 스킬 이펙트랑 적용 범위도 기존 스킨 이미지 역계산 방식 이용해서 ... 이미지에
맞춰서 타격 범위 재조정"*.

**① 조준 자유각(360도)** — `UnitRegistry.CollectEnemiesInOrientedRect(중심, 반크기,
방향, 진영, 목록)` 신설. 상자를 돌리는 대신 **대상을 스킬 좌표계로 옮겨** 내적 두 번으로
검사한다(회전 행렬 불필요). `BossSkillCaster` 의 `AxisDirection`(4방향 스냅)을
`AimDirection`(자르지 않음)으로 교체했고, **지면 연출도 같은 각도로** 돌린다 —
그림만 축에 맞추면 연출과 판정이 최대 45도 어긋난다.

**② 범위를 연출 원화에 맞춘다** (`BossSkillCaster.ResolveArea`) — 66절이 유닛 콜라이더에
쓴 로직과 **같은 계산**이다:

```
배율 = min(표가로 / 원화가로, 표세로 / 원화세로)     ← contain
실제 범위 = 원화크기 × 배율
```

| 스킬 | 표 | 원화 비율 | 실제 범위 |
|---|---|---:|---|
| 타락한 무덤 | 5 x 3 | ShockwaveFx 3.06:1 | 5.00 x 1.63 |
| 공허의 광선 | 15 x 3 | BeamFx 9.42:1 | 15.00 x 1.59 |

⚠ **두께가 표 값의 절반쯤으로 줄어든다** — 원화가 표보다 훨씬 납작해서다. 이것이
유저가 요구한 "이미지에 맞춰서 재조정"의 결과다. 더 두껍게 하려면 **표의 세로를 키우는
것으로는 안 되고**(비율이 상한이다) 가로(`value_01`)를 줄이거나 원화를 다시 그려야 한다.
비율 보정 자체를 끄려면 `BossSkillCaster.fitAreaToSkillArt` 를 끈다.
**계산 결과를 에셋에 안 적는다** — 원화가 바뀌면 결과도 같이 바뀌어야 하므로 런타임 계산이
정본이다(66-2절과 같은 이유).

**③ 원형 범위는 표에서 고른다** — `Skill` 시트 **L열 `range_type`** 신설
(`Line` 기본 / `Circle`). 유저가 "원형으로" 라고 했지만 단탈리온 두 원화는 **가로로 긴
파동·광선**이라 원형으로 만들면 그림과 판정이 어긋난다. 그래서 **모양을 표에서 고르게**
하고 두 스킬은 `Line` 으로 뒀다 — 실제로 지적된 문제(대각선을 못 때린다)는 ①이 해소한다.
진짜 원형이 필요해지면 표의 이 칸만 `Circle` 로 바꾸면 코드 수정 없이 돈다
(`CollectEnemiesInRadius` 경로).

### 5·6. 스킬별 시전 시간 · 최대 사거리 발사

- `Skill` 시트 **K열 `cast_time`** 신설 → `BossSkillSO.castSeconds`.
  0 이면 `BossSkillCaster.castSeconds`(전역 기본값 0.55)로 떨어진다.
  값: **타락한 무덤 1.2 · 공허의 광선 2.5**(유저 지시 "가시성 증가").
  ⚠ 피해는 시전과 **동시에** 들어간다 — 이 값은 연출 길이일 뿐 판정 시점이 아니다.
- 최대 사거리 발사는 **이미 그렇게 동작하고 있었다**(상자 길이는 언제나 표 값이고 조준은
  방향만 정한다). 리팩터링 중에 잃지 않도록 코드에 명시적으로 못 박아 주석으로 남겼다.
  ⚠ 다만 `PickAim` 의 후보 수집을 **정사각 → 원형**으로 바꿨다 — 상자로 모으면 대각선이
  √2배 멀리까지 후보로 잡혀 "조준은 됐는데 범위 밖" 인 헛시전이 생긴다.

### 7. 이동/대기 모션 겹침 — 원인은 프레임 단위 판정

`CharacterAnimator` 는 **한 프레임 이동량**(`moveThreshold` 0.004)으로 걷기/대기를 갈랐다.
이동속도가 느린 유닛은 프레임당 이동량이 임계값 근처라 **프레임률이 조금만 흔들려도
걷기↔대기가 매 프레임 번갈아** 나온다 — "두 모션이 겹쳐 보인다"의 실체가 이것이다.
`moveMotionHoldSeconds`(기본 0.2초) 유지 시간을 두어 한 번 이동으로 판정되면 그동안은
걷기를 유지한다. 판정 갱신은 **`ResolveFrames` 맨 앞**에서 한다 — 공격·시전 중에도 위치는
바뀌므로, 거기서 안 재면 공격이 끝난 첫 프레임에 "공격 → 대기 → 걷기"로 한 번 튄다.

### 8. 프레이야 머리 위 검은 선 — ⚠ 오른쪽 프레임 전부에 있었다

이미지 분석 결과(연결 요소 분해): **Right 프레임 19장 전부**에 캐릭터 몸통과 **완전히
떨어진** 가로 띠가 머리 위에 떠 있었다. Left 프레임에는 없다.

```
Idle_Right_00  y 24~26 (3줄) · x 48~170 (123px) · 310픽셀 · 밝기 37~42
Melee_Right_02 y 12~17 (6줄) · x 42~184 (143px)
```

가로 100~165px · 세로 2~6줄 · 몸통 최상단보다 위 — 캐릭터 그림일 수 없는 모양이고,
**원본 시트를 자를 때 위쪽 행의 밑동이 딸려 들어온 것**이다(40절이 고친 "한 컷에
캐릭터가 1.5명" 과 같은 뿌리의 슬라이스 사고).

**PNG 를 손으로 고치지 않고 `char_asset_preyja_build.py` 에 넣었다** — Assets 쪽 PNG 는
이 스크립트가 매번 다시 쓰므로 손으로 지우면 다음 실행에 되살아난다(64-4절의 그 사고).
`strip_detached_bars()` 는 **① 몸통과 떨어져 있고 ② 세로 8줄 이하로 납작하고 ③ 가로
40px 이상·가로/세로 8배 이상이고 ④ 그림 위쪽 40% 안**인 덩어리만 지운다.
실측 확인: 지워지는 것은 그 띠 14개(합성 Walk 는 정리된 Idle 에서 만들어지므로 자동)뿐이고,
세로로 긴 창 자국(y0~146 x189~191)·발밑 조각(y126~146)은 조건 ②④에서 걸러진다.

⚠ **부수 효과 — PPU 가 60 → 56 으로 바뀐다(정상).** 띠가 몸 높이에 섞여 들어가 있어서
빌드 스크립트가 잘못된 높이(131px)로 PPU 를 잡고 있었다. 진짜 높이는 121px 이고 목표
크기(2.18 유닛)를 맞추려면 PPU 56 이 맞다. `Skin_Preyja.contentSizeTiles` 도
`2.05 x 2.167 → 2.089 x 2.179` 로 재측정했다(`measure_skin_tiles.py`).
캐릭터는 씬 템플릿의 `renderHeightTiles 2.15` 로 최종 크기가 정해지므로 **화면 크기는
안 바뀐다**. **파일 이름·guid 는 그대로다**(8절 1번 방식 — 스프라이트 참조 46개 무손상).

### 9·10. 로그 — 형식 통일 + 복제 번호 제거

- `HudLog.SkillLine(시전자, 스킬, 덧붙일말)` 신설. 그전에는 보스가
  `"단탈리온 — 공허의 광선!"`, 캐릭터가 `"엘린의 희생 — …"` 으로 **호출부마다 형식이
  달랐고 스킬 이름이 코드에 한글로 박혀** 있었다(표에서 이름을 바꿔도 로그는 안 바뀐다).
  이제 셋 다 `"{시전자} · {스킬} — {덧붙일말}"` 이고 스킬 이름은 언제나 `DisplayName`(표)이다.
- `MonsterSpawner` 가 붙이던 일련번호(`unit.name = "{이름}_{n}"`)를 제거해 캐릭터
  (`CharacterUnit.ApplyDefinition`)와 같은 규칙으로 맞췄다. 추가로 `MonsterUnit.DisplayName`
  을 신설하고 `BattleLogPanel`·`CharacterPassives` 가 그것을 읽게 해서 **하이라키 이름이
  어떻든 로그가 흔들리지 않게** 이중으로 막았다.

### 11. 보스 체력바 칭호

- `wave_mid_boss` 시트에 **H열 `boss_title`** 신설(최종보스에만 있던 칸이다 — UI-17 의
  영어 이름과 똑같은 상황). 값은 위임 범위로 정했다: 혈인 **"피에 새겨진 낙인"** ·
  공허의 속삭임 **"허공을 삼킨 목소리"**. 표가 정본이니 마음에 안 들면 표만 고치면 된다.
- `gen_string_table.py` 규칙에 한 줄 추가 → `boss_title_110001/110002` 키 생성.
- `MonsterDefinitionSO.titleKey`/`Title` 신설. `sync_tables_to_assets.py` 가
  **표에 칭호가 실제로 적힌 몬스터에만** `titleKey` 를 넣는다(없는 키를 넣어두면 조회가
  매번 실패해 "칭호가 있는데 왜 안 뜨지"가 된다).
- `BossHealthPanel` 이 이름 줄 앞에 **rich text 로** 붙인다
  (`<size=72%><color=…>칭호</color></size>  이름`).
  ⚠ **라벨을 새로 만들지 않았다** — 줄을 하나 더 만들면 `Name`·`HpBack`·`Body` 세
  RectTransform 을 전부 다시 잡아야 하고, MCP 로 앵커 필드를 넣으면 조용히 무시되는
  경우가 있다(§10). 38MB 씬의 레이아웃을 건드리지 않는 쪽을 골랐다.
  **별도 줄로 원하면 말할 것** — 그때는 세 rect 를 MCP 로 다시 잡는다.

### 표 변경 (전부 Excel COM — 하이퍼링크 보존)

| 파일 / 시트 | 컬럼 | 값 |
|---|---|---|
| 웨이브 몬스터 테이블 / `Skill` | K `cast_time`(float) · L `range_type`(enum) | 1.2·2.5 / Line·Line |
| 웨이브 몬스터 테이블 / `first_Stat` | (기존) `movement_speed` | 120001: 1 → **4** |
| 웨이브 몬스터 테이블 / `wave_mid_boss` | H `boss_title`(string) | 칭호 2개 |
| 웨이브테이블 / `Sheet2` | H `spawn_group_size`(int) | 웨이브별 2~7 |

신규 스크립트 [Tools/table_update_20260813_boss_and_wave.py](../Tools/table_update_20260813_boss_and_wave.py)
가 네 가지를 한 번에 처리한다(백업 후 진행 · 재실행 안전).
**컬럼은 항상 맨 뒤에 붙인다** — 중간에 끼우면 위치로 읽는 기존 코드(`r[10]` = 방어력 등)가
통째로 깨진다(65-2절의 규약).

### 겪은 함정

- `patch_fields` 는 **없는 키를 조용히 건너뛴다.** C# 에 필드를 새로 추가한 첫 실행에는
  에셋 YAML 에 그 줄이 아예 없어서 `titleKey` 가 영원히 안 들어간다. `add_missing` 인자를
  만들어 없으면 파일 끝에 새로 만들게 했다(⚠ 빈 줄 금지 규칙은 그대로 지킨다 — 8절 3번).
- 하이퍼링크 검사에 `openpyxl` 의 `ws.hyperlinks` 를 쓰면 **원본에서도 0으로 나온다.**
  xlsx 를 zip 으로 열어 `xl/worksheets/_rels` 와 시트 XML 의 `<hyperlink ` 를 직접 세야
  한다 — 편집 전후 모두 **12개**로 확인했다.
- 보스 스킬의 `_scratch` 는 정적 공용 버퍼다. `PickAim` 이 후보를 담고 그 뒤
  `CollectEnemiesInOrientedRect` 가 덮어쓴다 — 조준 대상(`aim`)만 바깥에 들고 있으면
  안전하지만, 원형 분기에서는 `NearestOf(_scratch)` 를 **피해 적용 전에** 불러야 한다.

### 소유권 (§2)

**UI 소유** — `Scripts/Combat/**`(BossSkillCaster·BossSkillSO·BossSkillType·
CharacterAnimator·CharacterPassives·UnitRegistry) · `Scripts/UI/**`(MinimapPanel·
BossHealthPanel·BattleLogPanel·HudLog) · `Resources/BossSkills/**` ·
`Resources/Data/StringTable.txt` · `Assets/Scenes/Proto_01.unity`.

**⚠ PROTO 소유 파일을 건드렸다** — `Scripts/Units/MonsterSpawner.cs · MonsterUnit.cs ·
MonsterDefinitionSO.cs`, `Scripts/Wave/WaveDefinitionSO.cs`, `Data/Units/**`,
`Data/Wave/WaveDefinitions.asset`, `Tools/**`(sync_tables_to_assets · gen_string_table ·
char_asset_preyja_build + 신규 1개), `Art/Char_Asset/Char_Asset_Preyja/**`.
UI-15·17·18·19·20·21 과 같은 종류의 크로싱이다.

### 씬 변경 여부 — **있음** (전부 MCP · 저장 1회)

| 오브젝트 | 무엇 |
|---|---|
| `UI_Root/HUD_Minimap/View` | `RawImage.raycastTarget` **false → true** |
| `UI_Root/HUD_Minimap` | `MinimapPanel.clickToMoveCamera`/`snapCamera` = true |
| `UI_Root/HUD_Boss` | `BossHealthPanel.showTitle` = true · `titleSizePercent` = 72 |
| `Templates/.../Monster_Dantalian_Template` | `BossSkillCaster.fitAreaToSkillArt` = true |
| 템플릿 6개(캐릭터 + 몬스터 5) | `CharacterAnimator.moveMotionHoldSeconds` = 0.2 |

비활성 템플릿은 경로 조회가 안 되므로 `get_scenes_hierarchy` 의 `instanceId` 를 **같은 턴에**
썼다(12절의 그 함정). **GameObject 357 → 357**(증가 0).

### 검증

- `recompile_scripts` **에러 0 · 경고 0**, `Assets/Refresh` 후 콘솔 **에러 0**.
- 표 편집 후 하이퍼링크 **12개 전부 유지**(zip 직접 검사).
- `sync_tables_to_assets.py` 반영 확인 — 보스 `moveSpeedTiles: 2.389` ·
  `titleKey: boss_title_120001` · `BossSkill_130002` 의 `castSeconds: 2.5` ·
  `rangeType: Line` · 웨이브 20행 전부 `spawnGroupSize`.
- `StringTable.txt` diff **2줄만 추가**(나머지 82개 키 불변).
- 프레이야 프레임 재검사 — 머리 위 가로 띠 **19장 전부 0개**, 발밑·창 조각은 그대로 보존.
  스프라이트 **guid 변경 0건**.
- 씬 저장 1회 · `GameObject:` 블록 357 → 357.

### 아직 확인 못 한 것 (유저가 볼 것)

1. 미니맵을 눌러 화면이 그 지점으로 가는지 · 끌었을 때 따라오는지
2. 보스가 눈에 띄게 빨라졌는지 (1.4 → 2.39, 잡몹 2.25 와 비슷한 속도)
3. 무리 소환이 "디펜스 느낌"을 주는지 — 무리가 너무 크거나 작으면 **웨이브테이블의
   `spawn_group_size` 만** 고치면 된다
4. 보스 스킬이 **대각선 적에게도** 나가는지 · 연출과 맞는 범위가 일치하는지
5. 공허의 광선 2.5초가 충분히 보이는지 (짧으면 표의 `cast_time` 만 키운다)
6. **범위 두께가 절반으로 준 것**(5x1.63 · 15x1.59)이 받아들일 만한지 — 아니면
   `fitAreaToSkillArt` 를 끄거나 원화를 다시 그려야 한다
7. 보스 이동/대기 모션이 더 이상 섞이지 않는지
8. 프레이야 머리 위 선이 사라졌는지 · 크기가 예전과 같은지
9. 로그가 "엘린 · 희생 — 비기오르 회복" 형식으로 · 몬스터 이름에 번호가 없는지
10. 보스 체력바에 칭호가 뜨는지(단탈리온 "끝없는 형상의 군주" / 중간보스 2종)

### 씬반영요청 목록

없음.

---

## UI-23. 배속 버튼 · 중립 몬스터 표 확장 · 선공 판정 단일화 · HUD 창 배타 (2026-08-13)

유저 지시 6건. 전제는 이번에도 **"모든 객체 생성 및 수정은 템플릿/슬롯 복제 제외하고 MCP 로 직접"**.
상세는 `진행상황.md` **71절**에 있고, 여기에는 UI 브랜치가 알아야 할 것만 적는다.

| # | 지시 | 결과 |
|---:|---|---|
| 1 | 칭호 텍스트 크기 키우기 | `BossHealthPanel.titleSizePercent` 72 → **92** (상한 150) |
| 2 | 깃발 클릭 판정이 너무 작다 (2x1 타일) | 축별 최대값으로 **2 x 2 타일** |
| 3 | 중립 몬스터 `first_Stat` · 배회 범위 · 개체량/주기 | 표 2곳 + 파이프라인 신설 |
| 4 | 선공/비선공 체크가 여러 개 | 표 `atk_take` 한 칸으로 단일화 |
| 5 | 웨이브 타이머 옆 배속 버튼 | `GameSpeedPanel` 신규 + 씬 `HUD_Speed` |
| + | HUD 액션 버튼이 동시에 눌린다 | `HudExclusive` 신규 |
| + | 표의 영어 이름 컬럼 제거 | 5칸 삭제 + 파이프라인 3개 수정 |

### 1·2. 칭호 크기 · 깃발 클릭 판정

깃발 원화는 `32x64px @ PPU 32` = **1 x 2 타일**이라 가로 한 칸짜리 막대였다. 유저가 준
값(2 x 1)을 그대로 쓰면 세로가 오히려 줄어들어서, **축마다 큰 쪽**을 쓴다 → 실제 판정
**2 x 2 타일**. "이미지보다 더 크게"와 "2x1"을 둘 다 만족한다.
⚠ `BoxCollider2D.size` 는 로컬 단위라 `lossyScale` 로 나눈다(지금 스케일은 1이지만
나중에 스케일을 주면 어긋난다).

### 3. 중립 몬스터 — ⚠ 이 표는 게임에 반영되는 경로가 아예 없었다

22절에서 값을 손으로 옮겨 적은 뒤로 파이프라인이 없었다. `sync_neutral_monsters()` 를
신설해 다른 표와 같은 흐름으로 맞췄다.

- `임시용 중립 몬스터.xlsx` 에 **`first_Stat` 시트 신설**(웨이브 몬스터와 같은 형식).
- `neutrality_mon` 에 **`max_alive`·`respawn_seconds`** 컬럼 신설 → 개체수·주기가 표로.
- 개체 분포를 **뒤집었다**: 가까운 종 15→**8**(45초) · 중간 8→**14**(22초) ·
  먼 종 4→**18**(16초). 에너지 격차도 벌렸다(먼 종 30~50 → **55~90**).
- ★ **"중앙으로 모인다"의 실체** — 스폰·배회가 *각도 + 유클리드 반지름*으로 점을 뽑고
  등장 고리는 **체비셰프**로 검사했다. 두 거리가 대각선에서 √2 배 차이 나므로 결과가
  안쪽으로 쏠렸고, 배회는 상한을 아예 안 봤다. **체비셰프 고리에서 직접 뽑도록** 고쳤다
  (`SampleRingCell`). 스포너가 배회에 넘기던 상한도 무한대였던 것을 실제 스폰 상한과
  같은 값으로 맞췄다 — **배회 범위 = 스폰 범위**.

### 4. 선공/비선공 — 체크가 세 곳에 흩어져 있었다

표의 `atk_take` · 정의의 `aggressive` · **템플릿의 `UnitCombat.canAcquireTargets`/
`canRetaliate`**. 스포너가 `canAcquireTargets` 만 넣고 `canRetaliate` 는 템플릿 값을 그대로
뒀기 때문에 셋이 어긋날 수 있었다.

이제 스폰할 때 **둘 다 표 값으로 덮어쓴다**(`UnitCombat.SetCanRetaliate` 신설).
비선공 = 맞기 전엔 공격 안 함(맞으면 반격) · 선공 = 보이면 감. 공격 방식은 **근거리 고정**.
1001 에 공격력 2 를 넣었다(예전엔 0 이라 반격해도 무의미했다).

### 5. 배속 버튼 (`GameSpeedPanel` 신규)

`HUD_Wave` 오른쪽에 `HUD_Speed` 를 만들고 **x1 · x2 · x4 · x8**. (유저는 x2/x4/x8 을
요청했는데 되돌아올 x1 이 없으면 배속을 끌 수 없어 하나 더 뒀다.)

- `Time.timeScale` 한 줄이면 된다 — 게임 로직은 `Time.deltaTime`, HUD·카메라는 이미
  `Time.unscaledTime` 이라 그대로 통한다(패배 화면이 `timeScale = 0` 으로 게임만 멈추는 구조).
- **`Time.fixedDeltaTime` 도 같이 곱한다** — 안 그러면 8배속에서 FixedUpdate 가 8배 돈다.
- ⚠ 패배·승리 화면이 `timeScale` 의 주인일 때(0)는 배속을 걸지 않는다.
- ⚠ 에디터의 `timeScale` 은 플레이 모드를 나가도 유지된다 → `OnDisable` 에서 1 로 되돌린다.
- 키보드 1~4 (Input System, U-D7).

### + HUD 창 배타 — 창끼리 서로를 닫는 구조가 문제였다

```
TacticalOrderPanel.SetOpen(true)   → CharacterGrowthPanel.Close()    (부대 설정은 안 닫음)
CharacterGrowthPanel.SetOpen(true) → TacticalOrderPanel.Close()      (부대 설정은 안 닫음)
SquadPanel.SetOpen(true)           → 전술·성장 둘 다 Close()
```

조합 3개 중 2개가 빠져 있었다. 창이 하나 늘 때마다 N² 로 늘어나 반드시 빠뜨리는 구조라
**`HudExclusive.OpenOnly(this)` 한 줄로 통일**했다(`IExclusiveHudPanel` 구현만 하면 된다).
창이 열릴 때 **맵 클릭 모드(집결지 지정·건설 자리 지정)도 끊는다.**
⚠ 비활성 창을 찾아야 하므로 `FindObjectsInactive.Include` 필수(59-6절 함정) — 한 번만 조회하고 캐시.
⚠ 반대 방향(지정 모드가 창을 닫는다)은 일부러 안 넣었다 — `SquadPanel._pickingHandoff`
흐름과 충돌해 방금 켠 지정 모드를 스스로 꺼버린다.

### + 영어 이름 컬럼 제거

`wave_nom` · `wave_mid_boss` · `wave_top_boss` · `Character` 의 `character_name_EG` 와
`wave_top_boss.boss_title_EG` — 5칸. 스트링 키 테이블의 `en` 이 정본이 됐는데 원본 표에
같은 값이 남아 있었다.

⚠ **컬럼 삭제는 "맨 뒤에만 붙인다"(UI-18) 규약으로도 못 막는 사고다** — 뒤 컬럼이 밀려
위치로 읽던 코드가 조용히 엉뚱한 값을 읽는다. 영향받는 세 곳을 **필드명 기반**으로 바꿨다
(`read_rows()` 신설 · `char_cell()` · `gen_string_table` 규칙 삭제).
`gen_character_assets.py` 는 에셋 파일 이름에 쓰던 영어 이름을 이제 **스트링 키 테이블의
`en` 칸**에서 읽는다(비면 guid 가 바뀌어 참조가 끊기므로 비면 에러를 내고 멈춘다).

### ⚠ 이번에 발견한 사고 — 폰트 굽기 메뉴가 씬 폰트를 통째로 갈아끼웠다

`HUD_Speed` 라벨에 폰트를 붙이려고 `LastSanctuary/폰트/…` 메뉴를 눌렀더니 씬의 TMP 폰트
참조 **234개**가 새 에셋으로 바뀌었다. `NeoDunggeunmoFontBaker.OutputFolder` 가
`Assets/_Project/Art/Fonts` 였는데 그 자리에 에셋이 없어서(정본은 `Resources/Fonts`)
**새로 굽고 씬 전체에 적용**해버린 것이다 — §10 H-4 정면 위반.

경로 상수를 정본으로 고치고 중복 에셋을 지운 뒤 메뉴를 다시 실행해 되돌렸다.
검증: 중복 참조 **0개** · 정본 참조 **165개**(기존 161 + 새 라벨 4).
⚠ 부수 효과로 `m_sharedMaterial` 이 비어 있던 텍스트 92개에 폰트 기본 머티리얼이
명시적으로 들어갔다 — 런타임에 쓰던 것과 같은 머티리얼이라 화면은 안 바뀐다.

### 소유권 (§2)

**UI 소유** — `Scripts/UI/**`(BossHealthPanel · RallyFlag · GameSpeedPanel(신규) ·
HudExclusive(신규) · TacticalOrderPanel · SquadPanel · CharacterGrowthPanel) ·
`Scripts/Combat/UnitCombat.cs` · `Scripts/Editor/NeoDunggeunmoFontBaker.cs` ·
`Assets/Scenes/Proto_01.unity`.

**⚠ PROTO 소유 파일을 건드렸다** — `Scripts/Units/NeutralMonsterDefinitionSO.cs ·
NeutralMonsterSpawner.cs · NeutralMonsterWander.cs`, `Data/Units/NeutralMonster_*.asset`,
`Tools/**`(sync_tables_to_assets · gen_string_table · gen_character_assets + 신규 1개).
UI-15·17·18·19·20·21·22 와 같은 종류의 크로싱이다.

### 씬 변경 여부 — **있음** (전부 MCP · 저장 1회 · GameObject 357 → 366)

| 오브젝트 | 무엇 |
|---|---|
| **신설** `UI_Root/HUD_Speed` + 버튼 4개 + 라벨 4개 | 배속 패널 (+9 오브젝트) |
| `UI_Root/HUD_Boss` | `titleSizePercent` 92 |
| `RallyFlags/RallyFlagTemplate` | `minClickSizeTiles` (2, 1) |
| `Neutral_Templates` 3개 | `canAcquireTargets`/`canRetaliate` 를 표에 맞춤 |

⚠ MCP 함정 둘: **새 컴포넌트는 `recompile_scripts` 만으로는 안 붙는다** — `Assets/Refresh`
로 임포트까지 돼야 `update_component` 가 타입을 찾는다. TMP 정렬은 `m_VerticalAlignment` 에
숫자를 넣으면 범위 오류가 나고 프로퍼티 `alignment: "Center"` 로 넣어야 통한다.

### 검증

`recompile_scripts` 에러 0·경고 0 · 콘솔 에러 0 · 하이퍼링크 유지(웨이브 12 · 캐릭터 40 ·
중립 3) · `StringTable.txt` 바이트 단위 불변 · 캐릭터 에셋 변화 0(멱등) ·
씬 폰트 참조 정본 165 / 중복 0 · GameObject 357 → 366.
**플레이 모드 검증은 안 했다** — 진행상황 71-11절의 8가지를 유저가 볼 것.

### 씬반영요청 목록

없음.

---

## UI-24. 시야 밖 적 공격 불가 · 전방 캐릭터 확인 이동 · 엘린 시야 사각형화 (2026-08-13)

> 상세는 `진행상황.md` **75·76절**. (⚠️ 73절은 이 로그에 항목이 없다 — 그 세션이 빠뜨렸다.
> 이 항목은 **75절**에 대응한다.)

### 무엇을 했나

| # | 변경 | 파일 |
|---|---|---|
| 1 | **시야 밖 적 공격 불가** — `respectFogOfWar` 가 타겟 선정 필터에만 걸려 있어 **반격(`FindRetaliationTarget`) · 동료 구원(`FindAllyAttacker`) · `TryAttack`** 세 경로가 안개를 우회했다. 판정을 신규 `IsFogVisible` 한 곳으로 모았다(치유는 아군 대상이라 예외) | `Combat/UnitCombat.cs` |
| 2 | **전방 캐릭터가 확인하러 간다** — 안 보이는 적에게 맞으면 경보를 남기고 전방 중 가장 가까운 한 명만 이동. 누가 그 자리를 보면 경보 자동 해제 | `Combat/SightAlertService.cs` **(신규)** · `Units/CharacterBehavior.cs` |
| 3 | **엘린 시야 원형 → 그림 크기 사각형** — 원은 모서리까지 닿아야 해 넓이가 그림의 3.54배(19.9 vs 5.62타일²)였다 | `Fog/VisionSource.cs` · `Fog/FogOfWarService.cs` · `Combat/CharacterPassives.cs` |

### 씬 변경 여부 — **없음**

사각 시야는 스킬이 **런타임에** 넣고(`SetVisionBox`), 신규 `[SerializeField]` 4개
(`investigateUnseenAttacks` / `investigateRange` / `investigateTtlSeconds` /
`investigateMergeTiles`)는 씬에 저장돼 있지 않아 코드 기본값이 적용된다 — UI-9 이후 계속 쓰는 방식.
캐릭터 템플릿의 `visionTiles` 7 · `respectFogOfWar` 1 도 그대로다.

### ⚠️ 검증 — Unity 가 닫혀 있어 `dotnet build` 로 했다

유저 지시는 "mcp 연결해서 수정" 이었으나 작업 시점에 **Unity 에디터가 실행 중이 아니었다**
(`Get-Process Unity` → 없음, MCP 는 전부 60초 큐 타임아웃). 이번 변경은 씬 수정이 필요 없어
작업 자체는 완결됐다.

```
dotnet build Assembly-CSharp.csproj   →  오류 0개 · 경고 0개
```

★ **앞으로 쓸 수 있는 수단** — Unity 가 닫혀 있어도 이 명령으로 컴파일 검증이 된다.
지금까지는 `recompile_scripts` 가 유일한 수단인 줄 알았다.

⚠️ 단, **신규 `.cs` 파일은 `Assembly-CSharp.csproj` 의 `<Compile Include>` 목록에 없다** —
이 csproj 는 Unity 가 생성하고 `.gitignore` 대상이라 에디터를 안 켜면 갱신되지 않는다.
한 줄 추가해 빌드했고, Unity 를 켜면 다시 생성되므로 저장소에는 영향이 없다.

⚠️ `dotnet build` 는 Unity 의 임포트·직렬화 단계를 거치지 않는다 — **에디터를 켠 뒤
`recompile_scripts` 로 한 번 더 확인하는 편이 안전하다**(미결 151번).

### 소유권 (§2)

UI 소유 — `Scripts/Combat/**`(`UnitCombat` · `CharacterPassives` · `SightAlertService` 신규).
`Scripts/Units/CharacterBehavior.cs` 는 UI-13·UI-14 와 같은 전제(전투 AI 이관 이후 이 브랜치 소유).
⚠️ **`Scripts/Fog/**` 는 §2 상 PROTO 소유**다(`VisionSource` · `FogOfWarService`).
이번엔 **기존 원형 동작을 건드리지 않고 사각 모드를 덧붙이는 방식**으로만 고쳤다
(원형 경로는 한 줄도 안 바뀌었다) — 그래도 경계를 넘었으므로 §2 갱신 여부는 유저 판단이 필요하다.

### 씬반영요청 목록

없음.

---

## UI-25. 캐릭터가 몬스터에게 끌려가던 원인 2가지 + 확인 담당 폴백 (2026-08-13)

> 상세는 `진행상황.md` **77·78절**.

### 무엇을 했나

유저 피드백: **"빌드 후에 테스트 해보는데 종종 캐릭터가 몬스터에게 끌려가는 상황이 나온다"**
→ 조사해보니 **원인이 둘**이었고 둘 다 고쳤다.

| # | 원인 | 조치 | 파일 |
|---|---|---|---|
| 1 | **목줄이 정상 탐색 경로에만** 걸려 있었다. 반격·동료 구원은 목줄을 안 보고 거리 기준이 **자기 위치**라, 걸어갈수록 판정 범위가 따라와 **래칫처럼 끌려갔다** | 목줄 관문을 **하나로 모으고 기준점을 언제나 귀환 지점**으로. 경로별 허용 거리만 다르다 — 전부 `max(leashRange, …)` 라 **최소 보장** 의미다. 씬 템플릿(`leashRange` 7) 기준 실효값 **탐색 7 · 반격 8 · 동료 구원 12타일** | `Combat/UnitCombat.cs` |
| 2 | **밀림(separation)에 크기 상한이 없었다.** 유닛마다 힘을 더하므로 몬스터 5마리면 1.4×5=7 이 되어 길이 1 인 방향 벡터가 묻히고 **진군 무리에 그대로 휩쓸렸다** | `separationMaxInfluence`(신규, 0.7) 로 `ClampMagnitude` — 가려던 방향이 항상 주도권을 갖는다 | `Combat/UnitCombat.cs` |
| 3 | 전방 포지션이 없으면 아무도 확인하러 안 갔다(UI-24 의 미결) | `PickInvestigator()` 신설 — 전방이 있으면 그중 경보에 가장 가까운 한 명, **없으면 넥서스에서 가장 먼**(제일 앞선) 캐릭터 | `Units/CharacterBehavior.cs` |

⚠️ **1번은 이 저장소가 같은 버그를 두 번째로 밟은 것이다** — 진행상황 73-12절이 중립 몬스터에서
똑같이 겪고 *"판정 기준을 움직이지 않는 지점으로 옮겨야 한다"* 고 기록했는데,
**캐릭터 쪽에는 그 교훈이 적용돼 있지 않았다.**

### 검증

`dotnet build Assembly-CSharp.csproj` → **오류 0 · 경고 0** (UI-24 의 방법. Unity 는 여전히 미실행).
사냥 추격 한계(`huntPursuitTiles`)의 기준점이 `_huntOrigin`(고정)인 것을 확인 — 래칫 아님.

**씬 변경 없음** — 신규 `[SerializeField]` 1개(`separationMaxInfluence`)는 씬에 없어 코드 기본값 0.7 적용.

### 소유권 (§2)

⚠️ **UI-24 가 넘은 `Scripts/Fog/**` 경계는 유저가 승인**했다("경계는 넘어도 되고").
§2 의 소유권 표를 실제에 맞게 갱신하는 것이 남았다(미결 154번).
이번 변경 자체는 `Scripts/Combat/**`(UI 소유) + `Units/CharacterBehavior.cs`(UI-13 이후 전제)뿐이다.

### 씬반영요청 목록

없음.

## UI-26. 중립 몬스터 전면 정비 — 이름·스킨·클릭 초상화·카르시노스 스킬·서식지·토벌 지시 (2026-08-15)

> 상세는 `진행상황.md` **86·87절**. 여기에는 UI 브랜치가 알아야 할 것만 적는다.

유저 지시 9건 + 추가 3건. 전제는 이번에도 **"모든 객체 생성 및 수정은 MCP 로 직접,
단 템플릿/슬롯 복제는 예외"**.

### ⚠⚠ 먼저 — MCP 가 이 세션에 안 붙어 있었다 (다음 세션이 반드시 볼 것)

`.mcp.json` 이 서버를 `"command": "node"` 로 띄우는데 **이 PC 의 PATH 에 node 가 없었다**
(설치는 돼 있다). 서버 프로세스가 아예 안 떠서 대화 세션에 MCP 도구가 **하나도** 붙지 않았다.
Unity 브리지(포트 8090)는 멀쩡했다.

**`Tools/mcp_unity_cli.js` 를 만들어** 같은 브리지에 직접 붙었다 — 같은 규약, 같은 도구.
씬 YAML 을 직접 건드린 곳은 없다. `.mcp.json` 의 `command` 를 절대경로로 고쳤으니
**다음 세션부터는 네이티브 MCP 도구가 붙는다.**

★ 이 과정에서 알아낸 **MCP 함정 3가지** (UI-23 의 함정 목록에 추가할 것):

1. ★★ **컴포넌트를 붙이는 호출과 값을 넣는 호출을 나눠야 한다.** 한 번에 하면
   **"성공" 응답이 오는데도 값이 하나도 안 들어간다**(실측: TMP 의 text 가 빈 문자열,
   fontSize 가 36 기본값). 두 번 나눠 부르면 전부 들어간다.
2. **`instanceId` 는 도메인 리로드에서 통째로 무효**가 된다(합성 번호다).
   `Assets/Refresh`·`recompile_scripts` 가 끼면 못 쓴다 — 계층 조회와 수정을 한 흐름에서.
3. **`get_scenes_hierarchy` 는 깊이 제한**이 있다. 버튼 같은 잎 노드는 안 보이므로
   `get_gameobject` 에 `maxDepth` 를 줘서 그 가지만 다시 파야 한다.

### 신규 UI (`Scripts/UI/`)

| 파일 | 역할 |
|---|---|
| `UnitPortraitPanel.cs` | 클릭한 유닛의 일러스트·이름·칭호. **로그 창 바로 아래**(`HUD_Portrait`) |
| `SubjugationPanel.cs` | 부대별 에픽 몬스터 토벌 지시 창(`HUD_Subjugate`). `IExclusiveHudPanel` |

### 고친 UI

- **`UnitSelector`** — 캐릭터 전용에서 **유닛 전반**으로. ★ 기존 이름의 뜻은 한 글자도 안 바꿨다:
  `Selected`(CharacterUnit) = **조작 대상** · `SelectedUnit`(DamageableUnit) = **표시 대상**.
  몬스터를 클릭하면 `Selected` 는 **null 이 된다** — 안 그러면 강화창이 몬스터를 강화하려 든다.
  겹치면 **캐릭터가 이긴다**. 안개에 가려진 적은 안 잡힌다(아군은 예외).
- **`BossHealthPanel`** — 대상 타입을 `MonsterUnit` → `DamageableUnit`.
  에픽 중립은 **교전 중일 때만** 뜬다(`IsInCombat`). ⚠ "살아있으면 계속"으로 두면
  에픽은 맵 상주라 **게임 내내 체력바가 떠 있다.**
- **`ActionPanel`** — `SubjugateButton` 추가(발견 수를 라벨에 표시). 패널 높이 252 → 300.
- **`BattleLogPanel` · `CharacterPassives`** — 이름 고르는 `is` 갈래를 지우고
  `DamageableUnit.DisplayName` 에게 물어본다. **두 곳 다 중립을 빠뜨리고 있었다.**
- **`CharacterRosterPanel`** — 이름 칸에 `Lv.N` 을 리치 텍스트로. 칸을 새로 만들지 않았다 —
  행 폭이 이미 꽉 차 있다(48절 미결 64번).
- **`CharacterGrowthPanel`** — "강화 N회" → `Lv.N` · 해금 문구 "강화 {0}회에 해금" → "Lv.{0} 에 해금".
- **`TacticalOrderPanel`** — 탐험 유형에서 **정찰 배선 제거**(사냥/탐색 2종).

### 소유권 (§2)

**UI 소유** — `Scripts/UI/**`(UnitSelector · BossHealthPanel · ActionPanel · BattleLogPanel ·
CharacterRosterPanel · CharacterGrowthPanel · TacticalOrderPanel · UnitPortraitPanel(신규) ·
SubjugationPanel(신규)) · `Scripts/Combat/**`(DamageableUnit · BossSkillCaster · BossSkillSO ·
BossSkillType · TacticalOrder · CharacterPassives · IBossSkillOwner(신규)) ·
`Assets/Scenes/Proto_01.unity`.

**⚠ PROTO 소유 파일을 건드렸다** — `Scripts/Units/**`(NeutralMonsterUnit · NeutralMonsterDefinitionSO ·
NeutralMonsterSpawner · MonsterUnit · CharacterUnit · CharacterBehavior ·
NeutralHabitat(신규) · EpicSubjugationService(신규)), `Scripts/Map/MapGenerator.cs`(한 줄 —
`DecoTilemap` 접근자), `Tools/**`. UI-15·17·18·19·20·21·22·23 과 같은 종류의 크로싱이다.
(⚠ §2 소유권 표 갱신은 여전히 미결 154번.)

### 씬 변경 여부 — **있음** (전부 MCP · 저장 2회)

| 오브젝트 | 무엇 |
|---|---|
| **신설** `UI_Root/HUD_Portrait` + 자식 4 | 클릭 초상화 (비활성 시작) |
| **신설** `UI_Root/HUD_Subjugate` + 자식 12 | 토벌 지시 창 (비활성 시작 · 행 템플릿 2개도 비활성) |
| **신설** `HUD_Actions/Buttons/SubjugateButton` + Label | 창 여는 버튼 |
| `HUD_Actions` | 높이 252 → 300 (버튼 6개 x 40 + 간격 5 x 8 + 여백 20) |
| `Templates/Neutral_Templates/*` 4개 | 이름을 종 이름으로 + `CharacterAnimator` 부착 |
| `GameSystems` | `EpicSubjugationService` 추가 |
| `HUD_Tactics/Col3/Non/Patrol` | 비활성 (**지우지 않았다** — 지우면 세로 배치가 밀린다) |

### 검증

`recompile_scripts` **에러 0 · 경고 0** · 콘솔 에러 **0** · 스트링 테이블 재생성 멱등 ·
잘라낸 프레임 8묶음을 스트립으로 이어 붙여 눈으로 확인 · 서식지 모양 6시드 시뮬레이션.
**플레이 모드 검증은 안 했다** — 진행상황 86절의 "아직 확인 못 한 것" 8가지를 유저가 볼 것.

### 씬반영요청 목록

없음.

## UI-27. 창이 안 열리던 버그 · 레이아웃 · 데미지 숫자 · 사각 스폰 · 밸런스 (2026-08-16)

> 상세는 `진행상황.md` **88·89절**. 여기에는 UI 브랜치가 알아야 할 것만 적는다.

### ⚠⚠ 다음 세션이 반드시 볼 것 — MCP 로 UI 를 만들 때의 함정 둘

UI-26 에서 만든 창 둘이 **둘 다 화면에서 안 됐다.** 원인이 서로 다른 종류였고,
둘 다 **앞으로 반복될 실수**다.

**① 비활성으로 시작하는 창은 `Awake` 에서 자기를 끄면 안 된다**

```csharp
void Awake() { …; gameObject.SetActive(false); }   // ← 절대 금지
```

비활성으로 저장된 오브젝트의 `Awake` 는 씬 로드 때 안 돌고, `SetActive(true)` 로
켜지는 **그 호출 안에서 동기적으로** 처음 돈다. 그래서 이 한 줄이 **열리는 순간
자기를 다시 끈다** — 창이 영영 안 열린다.

⚠ 이 코드가 `UnitPortraitPanel`·`SubjugationPanel` 뿐 아니라 **`SkillDetailPanel`
에도** 있었다. 그쪽은 예전에 "Instance 가 null 이라 안 열린다"를 고쳤는데
(그 절의 긴 주석) **바로 뒤에 이 두 번째 원인이 그대로 남아** 있었다 — 증상이
"눌러도 아무 일도 안 일어난다"로 같아서 첫 원인을 고친 뒤에도 못 열렸다.

→ "닫힌 채로 시작"은 **씬에 그렇게 저장해서** 지키고, 혹시 켜져 있으면
**항상 살아 있는 쪽**이 닫아준다(`UnitSelector.Start`·`ActionPanel.Start`). **미결 196번.**

**② 늘린 앵커에 pivot 0 을 쓰면 위치가 부모 중앙 기준이 된다**

`anchorMin.x=0` · `anchorMax.x=1` 인데 `pivot.x=0` 이면 `anchoredPosition.x` 는
**앵커 사각형의 중심**에서 잰다. 900폭 창에 여백 20 을 주려고 `pos=(20,…)` 을 넣으면
**글자가 x=470(한가운데)에서 시작한다.**

```
규칙: 늘린 축의 피벗은 0.5.
      왼쪽 여백 L · 오른쪽 여백 R → sizeDelta.x = -(L+R) · anchoredPosition.x = (L-R)/2
```
`HUD_Subjugate` 8개 + `HUD_Portrait` 2개를 다시 잡았다. **미결 197번.**

### 신규 UI (`Scripts/UI/`)

| 파일 | 역할 |
|---|---|
| `DamageNumberFx.cs` | 맞은 자리에 피해량을 숫자로. **가한(흰) / 받은(붉고 크게) / 치명타(금색+`!`)** |

★ **받은 피해를 더 크게** 둔 것이 설계의 핵심이다 — 난전에서 눈이 먼저 가야 하는 쪽은
"내가 맞고 있다"이지 "내가 넣는 딜"이 아니다.
★ **가한/받은은 맞은 쪽의 진영으로 가른다** — 공격자로 가르면 지속 피해(공격자 없음)에서 깨진다.
★ 씬에 배선이 없다(`CombatProjectileFx` 와 같은 `Bootstrap` 구조) · **월드 공간 TMP** · 풀링 + 상한 64.

### 고친 UI

- **`BossHealthPanel`** — (UI-26) 에픽 중립도 교전 중이면 뜬다.
- **`ActionPanel`** — `Start` 에서 토벌 창을 한 번 닫는다(위 ①).
- **`UnitSelector`** — `Bind` 에서 초상화 창을 한 번 닫는다(위 ①).
- **`DamageableUnit`**(Combat) — 신규 정적 이벤트 `OnAnyDamaged(공격자, 대상, 피해량, 치명타)`.
  ⚠ `ApplyDamage(int)` 의 **시그니처는 안 건드렸다**(PROTO 가 쓰는 공개 API — 준수사항 U-D4).
  치명타 여부는 `_pendingCritical` 한 칸으로 넘긴다.

### 소유권 (§2)

**UI 소유** — `Scripts/UI/**`(DamageNumberFx 신규 · UnitPortraitPanel · SubjugationPanel ·
SkillDetailPanel · ActionPanel · UnitSelector) · `Scripts/Combat/**`(DamageableUnit ·
BossSkillCaster · CharacterPassives) · `Assets/Scenes/Proto_01.unity`.

**⚠ PROTO 소유 파일을 건드렸다** — `Scripts/Units/**`(NeutralMonsterSpawner ·
NeutralMonsterDefinitionSO · NeutralMonsterWander · NeutralHabitat · CharacterBehavior),
`Tools/**`. UI-15 이후 계속되는 같은 종류의 크로싱이다(§2 갱신은 여전히 미결 154번).

### 씬 변경 여부 — **있음** (전부 MCP · 저장 1회)

| 오브젝트 | 무엇 |
|---|---|
| `HUD_Portrait/Name` · `Title` | 피벗·위치 보정 |
| `HUD_Subjugate` 하위 8개 | 피벗·위치 보정 |
| `GameSystems > ErosionService` | 침식 3값 강화 |

### 검증

`recompile_scripts` **에러 0 · 경고 0** · 콘솔 **에러 0 · 경고 0** ·
사각 범위 커버리지 셀 단위 계산 · 위협도 웨이브별 출력 · 서식지를 실제로 깔아보고 두 번 조정.
**플레이 모드 검증은 안 했다** — 진행상황 88절의 "아직 확인 못 한 것" 7가지를 유저가 볼 것.

### 씬반영요청 목록

없음.

---

## UI-28. 회복·빗나감 숫자 · 정신 이상 연출 · 전술 충돌 · 초상화 cover (2026-08-17)

> 진행상황 **90·91절**에 전문(全文)이 있다. 여기는 브랜치 기준의 요약이다.

### 무엇을 / 왜

유저 지시 8건 (88절 데미지 숫자 · 29절 침식 · 86절 클릭 초상화의 후속).

1. 힐 숫자를 초록으로 · 2. 원거리 빗나감에 "빗나감" · 3. 네오둥근모 폰트 · 4. 숫자 크기 축소
5. 정신 이상 문구(나쁨 빨강·흔들림 / 좋음 노랑·페이드인) · 6. 나쁜 정신 이상 = 빨간 점멸
7. 정신 이상 > 전술 우선순위 검토 · 8. 일러스트 빈 공간 제거(스타크래프트 느낌)

### 핵심 3가지

**① 두 이벤트가 이미 있었고 구독자가 0명이었다.** `DamageableUnit.OnAnyMissed`(88절)와
`ErosionService.OnMentalErrorTriggered`(29절)는 선언·발생·리셋까지 다 돼 있는데 듣는 코드가
없었다. 그래서 33-11절의 *"MISS 표시가 없다"* 가 그대로 살아 있었다. **새 판정을 만들지 않고
구독만 걸었다.** 회복만 이벤트가 아예 없어서 `OnAnyHealed` 를 신설했고, ⚠ 체력 재생을
`HealSilently` 로 걸러야 한다 — 안 걸면 평시 화면이 초록 숫자로 뒤덮인다.

**② 빨간 점멸을 넣으려면 색칠 주인을 하나로 합쳐야 했다.** `UnitSelector` 의 「기억했다
복구」 방식은 **칠하는 주체가 하나일 때만** 성립한다. 점멸을 그냥 얹으면, 빨갛게 칠해진
순간에 클릭할 때 **그 빨간색이 원본으로 기억돼 선택을 풀어도 영영 빨갛게 남는다.**
신규 `UnitTintFx` 가 매 프레임 원래 색에서 다시 계산하고, 선택·점멸은 상태로만 들어온다.

**③ 정신 이상이 전술 변경에 실제로 지워지고 있었다.** 11종 중 「혼란」 하나 —
아군 공격이 `SetHuntTarget` 하나로 구현돼 있는데 `CharacterBehavior.ApplyTactics` 와
`UnitCombat.SetNeutralHostilitySuppressed` **두 곳이 무조건 지웠다.** `SetForcedAttackType` 과
**똑같은 모양**의 `SetForcedHuntTarget`/`ClearForcedHuntTarget` 잠금을 넣고, 직접
`_huntOverrideTarget = null` 로 밀던 세 곳을 전부 `ClearHuntTarget()` 경유로 바꿨다.

**④ 초상화 빈 공간의 정체는 `preserveAspect`** — 그건 채우기(cover)가 아니라 맞춰
넣기(contain)다. 가로 액자(424x262)에 세로 인물화(420x568)를 넣으면 가로의 46%만 쓴다.
몬스터는 반대로 가로형(1.5)이라 **액자 비율을 어느 한쪽에 맞춰도 다른 쪽이 깨진다.**
신규 `PortraitFit.Cover` + `RectMask2D` 로 바꾸고 `HUD_Portrait` 를 480x322 세로 액자 +
오른쪽 이름칸으로 재구성했다. **이미지는 한 장도 다시 만들지 않았다.**

### 겪은 함정

1. ⚠⚠ **비활성 루트 아래를 경로로 지목하면 MCP 가 같은 이름을 새로 만든다.**
   `update_gameobject` 는 "경로에 없으면 만든다"가 규약인데 `Templates` 루트가 비활성이라
   조회가 실패했다 → 같은 이름 루트가 2개가 되고 이후 경로 조회가 **빈 쪽**을 잡는다.
   유저 확인을 받고 지웠다. **비활성은 반드시 `instanceId` 로만 지목할 것.**
2. **"바꿔달라"를 받으면 먼저 지금 값을 확인할 것** — 폰트는 이미 네오둥근모였다.
   확인 없이 고쳤으면 같은 값을 다시 써넣고 "고쳤다"고 보고할 뻔했다.
3. **씬 값이 코드 기본값을 이긴다** — `DamageNumberFx` 는 `GameSystems` 에 붙어 있어
   코드만 고치면 화면이 안 바뀐다. MCP 로 씬 컴포넌트도 같이 고쳤다.

### 확인된 것

`Assets/Refresh` → `recompile_scripts` **에러 0 · 경고 0**, 콘솔 에러 0.
씬 값 `get_gameobject` 확인 (`DamageNumberFx` 15칸 · `Character_Template` 에 `UnitTintFx` ·
`Art` 236x302 + `RectMask2D` · `Sprite` 의 `hasRectClipping: true`).
저장된 씬 YAML 재파싱으로 `Templates` 루트가 1개뿐임을 확인. **저장 1회.**

### 아직 확인 못 한 것

**플레이 모드 검증은 안 했다** — 진행상황 90절의 "아직 확인 못 한 것" 8가지를 유저가 볼 것.

### 씬 변경 여부

**있음.** `GameSystems > DamageNumberFx` 값 15칸 · `Character_Template` 에 `UnitTintFx` 신설 ·
`HUD_Portrait` 480x322 재구성(+ `Art/Sprite` 신설, `Art` 에 `RectMask2D`) ·
`HUD_Tactics`/`HUD_Growth` 의 `Info/Portrait` 에 `RectMask2D`. 저장 1회.

### 씬반영요청 목록

없음.

---

## UI-29. 성역 축소(지름 10) · 성역 색 재설계 · 보스/침식 배선 점검 (2026-08-18)

> 진행상황 **104·105절**에 전문(全文)이 있다. 여기는 브랜치 기준의 요약이다.

### 무엇을 / 왜

유저 지시 5건 — 전부 102절(같은 날 앞 세션)의 후속이다. 앞 세션이 「아직 확인 못 한 것」으로
남긴 두 항목을 유저가 켜 보고 되돌려준 리포트 + 확인 지시 셋.

1. 성역이 너무 크다 → **지름 10**, 인스펙터에서 조절 가능하게, 생성마다 불규칙하게
2. 성역이 일반 블록과 구분이 안 된다 → **이미지적 변형**
3. 보스 스킬이 실제로 적용되는지 확인
4. 침식 효과 텍스트가 실제로 적용되는지 확인
5. 카시노마 15웨이브 등장 확인

### 핵심 3가지

**① 성역은 반지름이 아니라 「지름」 칸이 됐다.** 지시가 「지름 10」인데 반지름 칸에 10 을
넣으면 **지시의 두 배**가 된다. 칸 이름이 값의 뜻을 말하게 해서 그 착각이 생길 자리를 없앴다
(`radiusTiles` 20 → `diameterTiles` 10, 면적 16분의 1). 크기 편차는 새 칸
`diameterJitter`(0.2)가 만든다 — ⚠ **씨앗에서 뽑는다.** `UnityEngine.Random` 을 쓰면
`fixedSeed` 를 고정해도 크기만 매번 달라져 모양 비교가 불가능해진다. 그리고 ⚠ 씨앗을 한 번
비튼다(`^ 0x5F3759DF`) — 마스크 생성이 **같은 씨앗의 첫 두 난수**를 노이즈 위치로 쓰기 때문에,
안 비틀면 크기와 찌그러지는 방향이 한 값에 묶인다.

**② 색은 채도·명도가 아니라 「색조」를 옮겨야 했다.** 실측: 맵 바닥 H 359° · 성역 1차 H 333° —
**차이가 26°뿐**이다. 그 정도는 맵 생성기가 이미 만드는 밝기 편차(V 0.28~0.43)에 묻힌다.
붉은 계열 → **보라-청 265°** 로 옮겨 **94°** 벌렸다. ⚠ **명도는 억제했다**(V 0.45) —
88-5절에서 형광 분홍 서식지 위의 유닛이 안 보였다. `punch()` → `restyle()`(색조 이동 ·
채도 하한 · 대비 · 발광) + 가장자리 `rim_glow()`.

**③ ⚠⚠ 성역 타일에 세로 격자선이 있었다.** 열 평균 밝기를 재 보니 **왼쪽 한 열만** 59(내부
82) — 참조 시트가 칸 왼쪽에 구분선을 그려서 균등 분할의 반올림 오차가 그쪽으로 쏠린다.
`INSET` 을 키우는 것은 네 방향 모두에서 그림을 버리는 방법이라, **어두운 줄을 찾아서만** 깎는
`trim_dark_border` 를 넣었다(내부 평균의 0.88배보다 어두우면 한 줄 버리고 다시 본다).

**④ ★★ 침식 11종 중 「역겨움」이 아무 피해도 안 주고 있었다.** 초당 0.5% · 묶음 0.25초 ·
캐릭터 MaxHp 100 → 한 묶음의 피해 **0.125** → `RoundToInt` = **0**. 40초 내내 체력이 1 도
안 깎인다. 로스터에 「역겨움」이 뜨고 로그도 남으니 **텍스트만 맞고 실제로는 아무 일도 안
일어나는** 상태였다 — 유저 지시가 정확히 이것을 겨눴다. 같은 성질의 「피학」이 쓰는
**내림 + 나머지 보관** 을 그대로 쓰되, 대상이 여러 명이고 최대 체력이 아군마다 달라
나머지를 **대상마다** 들고 있게 했다(`_allyDamageCarryById`).

### 확인만 하고 안 고친 것

- **보스 스킬** — 표 → 에셋 → 정의 → 씬 템플릿까지 전부 이어져 있다. 세 보스 템플릿 모두
  `BossSkillCaster` 부착 · `enableSkills: 1`, `TryCast` 가 8종 전부 처리, 스킨에 스킬 FX·
  탄환 프레임 존재. 플레이 모드에서 **같은 컴포넌트를 쓰는 카르시노스**가 「스킬 2종 준비」
  로그를 찍는 것까지 봤다.
- **침식 표시** — `ErosionGaugeView`(「침식 42 · 혼란」) · `HudLog` · `DamageNumberFx`
  (나쁨 빨강 흔들림 / 좋음 노랑) · `UnitTintFx`(빨간 점멸) 넷 다 씬에 붙어 있다.
- **카시노마 15웨이브** — `WaveDefinitions` 15행 `bossMonsterId: 120003` →
  `Monster_Kasinoma.monsterId 120003` → `bossSlots[2]` 의 `Monster_Kasinoma_Template`
  (폴백이 아니다).

### 겪은 함정

1. **"확실하게 구분되게" 를 채도·명도로 두 번 시도할 뻔했다** — 1차와 같은 축을 더 올리는
   것으로는 안 된다는 것을 **재 보고 나서** 알았다. 고치기 전에 **실측부터** 할 것.
2. **작은 반지름이 셀룰러 오토마타에 먹히는지** 먼저 파이썬으로 시뮬레이션했다(반지름 5 ·
   8회 → 60~106칸). 안 먹혔으므로 `NeutralHabitat` 값은 **하나도 안 바꿨다**.
3. ⚠ `Nexus_Template` 은 비활성 루트 아래라 **`instanceId` 로만** 지목했다(90절 함정 1번).

### 씬 변경 여부 — **있음** (전부 MCP · 저장 1회)

`Nexus_Template > NexusSanctuary` 값 2칸(`diameterTiles: 10` · `diameterJitter: 0.2`) ·
`Nexus_Template` 에 **`NeutralHabitat` 신설 부착**(모양 값을 인스펙터에 드러내기 위해 —
런타임 `AddComponent` 로는 에디터에서 보이지 않는다). 컴포넌트 6 → 7 · `Templates` 루트 1개 유지.

### 검증

`recompile_scripts` 에러 0 · 경고 0 · 콘솔 에러 0. 플레이 모드에서
`[성역] 넥서스 둘레 88칸 · 데코 8개 (지름 11.39타일 — 기준 10 ±20%)`. 새 타일을 맵 바닥
위에 실제로 깔아 본 미리보기 이미지로 눈 확인(구분 확실 · 네 개가 서로 다른 모양 · 격자선 없음).
볼트 미러 719장.

### 아직 확인 못 한 것

진행상황 104절의 「아직 확인 못 한 것」 5가지를 유저가 볼 것 — 특히 ① 지름 10 이 화면에서
적당한지 ② 보라색이 유닛을 묻지 않는지 ③ 웨이브 보스 스킬의 실전 발동.

### 씬반영요청 목록

없음.

---

## UI-30. 성역 지름 15 · 색을 넥서스 건물 색에 맞춰 재설계 (2026-08-18)

> 진행상황 **106·107절**에 전문(全文)이 있다. 여기는 브랜치 기준의 요약이다.

### 무엇을 / 왜

유저 지시: *"15로 바꾸고 이거는 색 너무 다르자나 건물 색 확인해보고 해당 색과 컨셉이랑 맞춰서
다시 해봐 저렇게 푸르게 하면 안돼 건물 색이랑 맞춰야지"*

UI-29 의 즉시 후속. **UI-29 의 색 방향이 틀렸다.**

### 핵심 — "구분"만 보고 "무엇의 일부인가"를 놓쳤다

UI-29 가 푼 문제는 *"맵 바닥과 구분이 안 된다"* 였고 색조를 **보라-청(265°)** 으로 옮겨 그
문제만 정확히 풀었다. 그런데 성역은 **넥서스가 뻗어 나온 조직**이다 — 넥서스와 다른 색이면
「구분되는 다른 구역」은 되지만 **「넥서스의 구역」이라는 뜻이 사라진다.**

**색을 정할 때 기준이 둘이다**: ① 무엇과 **갈려야** 하나(맵 바닥) ② 무엇에 **속해야** 하나
(넥서스). UI-29 는 ①만 봤다. **②를 먼저 재고 ①을 다른 축으로 풀어야 한다.**

### 넥서스 원화 실측 → 컨셉 셋

`Char_Asset_Nexus/IdleHigh` 6프레임 · 불투명 픽셀 142,819개 → 전체 **rgb(62,26,24)
H 2.8° S 0.61 V 0.24**. 픽셀의 65%가 거의 검은 크림슨(V 0.10)이고, 밝은 픽셀은 4%도 안 되는데
그 소수가 **심장의 발광**을 만든다. 그리고 **밝아질수록 색조가 3° → 16° 로 열린다.**

### 맵과는 색조가 아니라 「깊이」로 갈린다

| | H | S | V | 읽히는 것 |
|---|---:|---:|---:|---|
| 맵 바닥 | 359° | 0.49 | 0.37 | 탁한 벽돌빛 **갈색** |
| 성역 (3차) | **3°** | **0.70** | **0.24** | 짙은 **핏빛 살** |
| 넥서스 건물 | 2.9° | 0.61 | 0.24 | (맞춘 대상) |

색조는 맵과 4°밖에 안 떨어져 있고 **그게 의도다.** 대신 **맵과 반대 방향으로** 벌린다 —
채도는 맵보다 크게 올리고(0.70), 밝기는 맵보다 **어둡게 내린다**(0.24 = 건물과 동일).
바닥이 아니라 **파인 구덩이**로 보이게 하는 것이다. 밝은 무늬만 강하게 발광시키고 색조를
오렌지-핑크로 열어 「불이 붙은 살」을 만든다.

⚠ UI-29 가 *"채도·명도만으로는 안 된다"* 고 적은 것은 **그때 올린 폭이 작았기 때문**이다
(채도 +0.10 · 명도 +0.13 은 맵 자체의 밝기 편차 V 0.28~0.43 **안에** 묻힌다). 이번에는
밝기를 **반대 방향으로** 내리므로 겹치지 않는다.

⚠ 어둡게 만드는 것이 **유닛 가시성에도 더 안전하다** — 88-5절에서 유닛이 묻힌 이유는 서식지가
**밝아서**였다. 미리보기에 캐릭터 5종을 실제로 얹어 확인했다.

★ 가장자리 **테두리 발광을 0.34 → 0.55** 로 올렸다. 색조로 경계를 알릴 수 없으므로 테두리
띠의 밝기가 **유일한 경계 신호**다.

### 지름 10 → 15

코드 기본값과 씬 값 **둘 다** 바꿨다(28절 함정 3번 — 씬 값이 코드 기본값을 이긴다).
`diameterJitter` 0.2 그대로라 매 게임 12~18타일. 플레이 로그 `지름 15.79타일 · 173칸`.

### 겪은 함정

⚠ `update_component` 가 한 번 *"instance ID 94 not found"* 로 실패했다(에셋 재임포트 중으로
보인다). 하이라키를 다시 조회해 **같은 id 94** 임을 확인한 뒤 재시도하니 성공. **id 가 바뀐
것이 아니므로 재시도가 정답이다** — 경로로 갈아타면 90절 함정 1번(같은 이름 증식)을 밟는다.

### 씬 변경 여부 — **있음** (MCP · 저장 1회)

`Nexus_Template > NexusSanctuary.diameterTiles: 10 → 15`.

### 검증

`recompile_scripts` 에러 0 · 경고 0 · 콘솔 에러 0. 팔레트 실측으로 건물과 색조 0.3° 차이 ·
밝기 동일 확인. 미리보기에 **넥서스 원화와 캐릭터 5종을 얹어** 눈으로 확인. 볼트 미러 719장.

### 아직 확인 못 한 것

실제 게임 화면(`Global Light 2D` · 안개 포함)에서의 대비. 묻힌다고 느끼면 `VALUE_LIFT` 를
−0.18 → −0.22 로 더 내릴 것 — **색조를 건드리는 것보다 낫다**(미결 274번).

### 씬반영요청 목록

없음.

---

## UI-31. ★★ 성역 가운데의 「건물 발판 모양 구멍」 (2026-08-18)

> 진행상황 **108·109절**에 전문(全文)이 있다.

### 무엇을 / 왜

유저 리포트: *"이제 느낌은 괜찮은데 중앙 건물 바로 아래 타일은 왜 기본 타일 그대로지"*

### 원인 — `IsCellBlocked` 는 「못 지나가는가」이고, 칠하는 쪽이 알아야 할 것은 「벽 그림이 있는가」다

`NeutralHabitat.Paint` 는 2026-08-15(카르시노스 서식지)부터 `IsCellBlocked` 로 걸러 왔다.
의도는 옳다 — 벽칸을 칠하면 벽 그림이 지워진다. 그런데 그 판정은 성질이 다른 셋을 묶는다:

| | 무엇 | 그림이 어디 있나 | 칠하면? |
|---|---|---|---|
| ① | 이 칸의 벽 타일 | 이 칸 | ⚠ 벽 그림이 지워진다 |
| ② | 북쪽 벽의 앞면(치마) | 북쪽 칸 스프라이트가 덮는다 | 안 보인다(무해) |
| ③ | **구조물 발판**(넥서스 3x3 · 타워) | 유닛 스프라이트 | **문제 없다 — 바닥은 평범한 지형** |

성역을 넥서스에 붙이자 ③이 문제가 됐다. `Nexus.OnEnable` 이 발판 3x3 을 등록하고, 그 뒤에 도는
`NexusSanctuary.Start` 가 **그 9칸을 건너뛴다.** 넥서스 스프라이트(4x4)가 위를 덮으니 화면에는
**스프라이트 아래로 삐져나온 한 줄**만 원래 바닥으로 보였다.

⚠ **카르시노스 서식지에서는 여태 안 드러났다** — 그쪽은 맵 바깥 고리(200~320타일)에 생기고
그 안에 구조물이 없다. **같은 코드의 같은 버그가 붙이는 대상이 바뀌자 드러난 것이다.**

### 고친 방법 — 판정을 뜻으로 갈랐다

`MapGenerator.HasWallTile(cell)` 신설(=이 칸에 벽 타일 자체가 있는가, ①만) → `Paint` 가 이것을
쓴다. ②는 벽 앞면 스프라이트가 알파 100%로 덮어 안 보이고(102-5절 실측), ③은 칠하는 것이
오히려 옳다. ★ **`IsCellBlocked` 는 안 건드렸다** — 이동·배치 판정에서는 셋을 묶는 것이 맞다.
판정을 하나 더 만든 것이 아니라, **서로 다른 질문 두 개를 각자의 이름으로 갈라 준 것**이다.

### 씬 변경 여부 — **없음**

### 검증

컴파일 에러 0 · 경고 0 · 콘솔 에러 0. 플레이 모드 `[성역] 179칸 (지름 15.02타일)` —
직전 실행이 **지름 15.79 에 173칸**이었으니 **지름이 작아졌는데 칸이 늘었다**(발판 9칸이 이제
포함된다는 방향과 맞다 · 씨앗이 달라 정확한 대조는 아니다). 카르시노스도 488 → 511칸.

### 겪은 함정 (다음 세션이 반드시 볼 것)

1. ⚠⚠ **`load_scene` 뒤에는 MCP `instanceId` 가 무효가 된다.** `get_scenes_hierarchy` 가
   같은 번호(`Nexus_Template id=94`)를 보고해도 `update_component`/`get_gameobject` 가
   *"instance ID not found"* 를 낸다(활성 오브젝트의 `id=1` 도 같이 실패). **비활성
   오브젝트를 고쳐야 하면 씬을 다시 열지 말 것.**
2. ⚠ **플레이는 `Lobby`(빌드 인덱스 0)에서 시작한다.** 로비는 유저 입력을 기다리므로 맵·성역
   로그가 안 나온다. 성역을 확인하려면 `Proto_01` 을 활성 씬으로 만든 직후 플레이할 것 —
   그런데 그 `load_scene` 이 1번 함정을 부른다. **씬 수정 → 저장 → 그 다음에 검증 플레이**
   순서로 갈 것.

### 씬반영요청 목록

없음.

---

## UI-32. ★★ 아니사킬(1005) · 고르도네(1006) 구현 (2026-08-19)

> 진행상황 **110·111절**에 전문(全文)이 있다. 여기는 브랜치 기준의 요약이다.

### 무엇을 / 왜

유저가 볼트에 표·원화·청크를 새로 넣고 두 종을 구현하라고 지시했다. 작업 중에 지시가 네 번
더 붙었다(청크 에셋 도착 · *"투명해지는거 조심해라"* · *"표 기준이 맞으니까 …"* ·
*"구속 해제 로직 체크"*).

### 핵심 6가지

**① 이름이 네 갈래였고, 유저가 「표가 정본」으로 정리했다.** 처음엔 다수결로 갈라 1006 의
**표를** `Gordonae` → `Gordone` 로 고쳤는데, 유저 지시로 **되돌리고 프로젝트 에셋 이름을
`Gordonae` 로** 맞췄다(스킨 폴더 · 일러스트 · 씬 템플릿). ★ 앞으로 이름이 어긋나면 **표를
고치지 말고 에셋을 표에 맞춘다.**

**② 표에 없던 것을 채웠다.** 1006 능력치 행(아예 없었다) · 1005 체력 20 → **40**(카르시노스와
보상·재생성·범위·콜라이더가 전부 같은데 체력만 절반이라 오타로 판단) · `habitat_design` 1005 행 ·
중립 `Skill` 시트의 `mentalerror_damage` 컬럼(값 0 — 29절 규칙대로 **0 이 맞는 값**이고, 이제
그 0 이 표에 적혀 있다) · 스트링 키 8줄 · 오타 2건.

**③ 신규 스킬 2종.** `Tail_strike` 는 「타락한 무덤」과 같은 모양이라 전용 분기가 없다.
`Huge_threat` 은 원형 + **즉시 구속** — ★ 말파스 구속탄과 **같은 상태**(`ApplyBind`)를 쓴다.
⚠ `ApplySideEffects` 의 `if (combat == null) return;` 이 **다음 효과를 삼키는** 함정이었다
(그때는 마지막 블록이라 무해했다). 조건 안으로 접었다.

**④ ★★ 「해로운 정신 이상 해제」가 구속을 못 풀고 있었다** (유저 지시로 확인).
해제 경로는 **하나뿐**(피올로 「정신 안정」 → `ClearActiveExternally`)이고 `ero.HasActive` 만
봐서 구속은 대상이 아니었다. `UnitCombat.ClearBind()` 를 신설하고 `TryCalmDown` 이
**나쁜 정신 이상 또는 구속**을 대상으로 보게 고쳤다 — 말파스 구속탄에도 같이 걸린다.

**⑤ ⚠⚠ 투명해지는 사고를 두 번 냈다** (유저 경고와 정확히 일치).
아니사킬은 배경을 「채도·밝기」로 판정했다가 **검은 비늘이 배경으로 잡혀 몸통이 통째로 지워졌다**.
고르도네는 거리 키잉이 **돔에 벌집 구멍**을 뚫었다(내부 픽셀의 25%가 배경과 거리 14 이하).
★ 둘 다 **테두리에서 흘려 채워 이어진 것만** 투명하게 + **구멍 메우기**로 해결.
★★ **어두운 개체에 「알파 = 배경과의 거리」를 쓰면 안 된다.** 같은 사고를 네 번째 냈다.

**⑥ ★★ 좌/우 라벨이 두 시트 모두 뒤집혀 있었다.** 입 위치·투사체 방향을 프레임마다 재서
확인했다. **앞으로 시트마다 라벨을 믿지 말 것.**

### 서식지 — 색조를 <b>건드리지 않았다</b>

유저가 지목한 「아까 그 실수」(성역 1차의 색조 이동)를 피하려고 **먼저 실측**했다. 원본이
H 342° 로 맵(359°)과 17° · 카르시노스(309°)와 33° 떨어져 **이미 알맞은 자리**였다 — 옮기면
어느 한쪽과 붙는다. 그래서 106-3절이 찾은 **다른 두 축**만 썼다: 맵보다 **어둡고 진하게**
(V 0.26 · S 0.62) + **호박색 불씨**(성역 3차의 절반 이하 폭).

★ 원본 16종의 평균 밝기가 30~62 로 벌어져 **격자무늬**가 보였다 — 타일 안쪽 무늬는 두고
**평균만** 0.75 당겨 45~53 으로 좁혔다. ⚠ 가장자리 16종은 원화가 없어 **바닥에서 파생**했다.

### 겪은 함정

1. ⚠ **`gen_string_table.py` 를 빼먹어** 화면에 키가 그대로 떴다(`NeutralMonster_5`).
   그리고 **`Assets/Refresh` 를 한 번 더** 해야 한다 — `recompile_scripts` 는 텍스트 에셋을
   다시 임포트하지 않는다. ★ 표를 고쳤으면 파이프라인이 **셋**이다.
2. ⚠ 원형 스킬의 상자가 `value_02` 를 세로로 읽어 `4 x 200` 이 됐다(그 칸은 **피해 %** 다).
   정사각으로 고쳤다 — ⚠⚠ **가로는 건드리면 안 된다**(반지름이 두 배가 된다 · 바로 되돌렸다).
3. ⚠ 템플릿을 복제하면 원본의 `definition` 참조가 따라온다(아니사킬이 카르시노스를 가리켰다).
4. ⚠⚠ **볼트 `진행상황.md` 의 104~109절이 사라져 있었다** — 되돌려진 것으로 보인다.
   스크래치패드에 원문을 남겨 뒀어서 순서대로 복원했다. **긴 문서를 append 로만 쌓지 말 것.**

### 씬 변경 여부 — **있음** (전부 MCP · 저장 2회)

`Anisakil_Template`(Carcinos 복제) · `Gordonae_Template`(Tumorling 복제) 신규 —
스킨 폴더 배선 + 아니사킬 `definition` 을 `NeutralMonster_5` 로 교정.

### 검증

컴파일 에러 0 · 경고 0 · 콘솔 에러 0. 플레이 모드에서 두 종 **자동 등록** ·
**아니사킬 서식지 465칸** · `[보스스킬] 아니사킬 스킬 2종 준비 (거대한 위협 포효 → 치명적 꼬리 타격)`.
뽑아낸 프레임 전부를 **초록 배경에 얹어 눈으로** 확인(투명 사고를 그렇게 찾았다).
서식지를 **카르시노스와 나란히 깔아** 눈으로 확인. 볼트 미러 897장.

### 아직 확인 못 한 것

진행상황 110절의 5가지 — 특히 ① 두 종이 실제로 맵에 나오는 것 ② 구속이 캐릭터를 멈추고
정신 안정이 그것을 푸는지 ③ **「치명적 꼬리 타격」의 실제 범위가 3 x 1.3 타일**이라는 점
(표는 3 x 5 인데 이펙트 원화 비율이 세로를 눌렀다).

### 씬반영요청 목록

없음.

---

## UI-33. 캐릭터/보스 상세 카드(`HUD_Portrait`) 목업 반영 (2026-08-19)

> 상세는 `진행상황.md` **112절**.

### 무엇을 했나

유저가 준 PPT 목업(`캐릭터 상세 ui 예시.pptx` 2장)대로 왼쪽 아래 상세 카드를 재구성했다.
**기능은 건드리지 않았다** — 선택 연동·닫힘·강화 갱신은 86·94절 그대로다.

```
칭호            ← 맨 위 (없으면 빈칸)
이름 · Lv.N · 상태
캐릭터 → 스킬 3줄 (아이콘 + 이름)
보스   → 체력바 + HP: N%      ← 스킬과 같은 자리를 배타적으로 쓴다
```

- **확인용 UI** — 스킬 줄에 `Button` 을 안 달았고 글자·아이콘의 `raycastTarget` 을 껐다.
- **잠긴 스킬은 흐리게** — 아이콘 알파 0.25 · 회색 글자 · 이름 뒤 `(Lv.5)`(해금 필요 강화 횟수).
- **칭호가 없으면 빈칸** — 예전엔 소속("아군")을 대신 적었는데 칭호를 얻은 것처럼 보였다.
  갈 곳 잃은 소속은 **상태 칸**이 받는다(구속 → 정신 이상 → 임무 → 소속 순).

### 겪은 함정

- `HUD_Portrait` 는 **비활성으로 저장**돼 경로 조회가 안 된다 → `instanceId` 로 잠깐 켜고
  작업한 뒤 다시 껐다(33-10절 방식). `instanceId` 는 조회한 **같은 턴**에만 유효하다.
- MCP 로 만든 TMP 에는 폰트가 안 붙는다 → **기존 `Title` 을 복제**해서 만들었다(§10 H-2).
- `Name` 의 `m_fontSize` 만 MCP 로 안 먹었다(23 → 26 유지) → 자동 크기(13~24)로 해결.
  긴 이름이 칸을 넘치는 문제도 같이 잡혔다.

### 소유권 (§2)

**UI 소유** — `Scripts/UI/UnitPortraitPanel.cs`, `Assets/Scenes/Proto_01.unity`. 그 외 없음.

### 디자인 다듬기 (유저 추가 지시 "깔끔하게")

- 세로 배치를 카드 전체에 폈다 — 스킬 행 32→**44** 높이, 간격 36→**52**.
- **스킬 행 배경판** — 새 오브젝트 없이 `Slot` 자신에게 `Image` 를 붙였다(부모가 자식보다
  먼저 그려지므로 형제 순서 재정렬이 필요 없다).
- **구분선 · 초상화 액자 · 카드 외곽 테두리**를 청록 낮은 알파로. 미니맵 테두리와 같은
  4줄 스트립 방식(스프라이트 참조 불필요). 카드 배경 알파 0.82 → 0.92.
- 아이콘 28→**32**, 글자 여백 36→**48**.

### 씬 변경 여부 — **있음** (전부 MCP, 저장 1회)

`Title`·`Name` 재배치 · `Level`·`State`·`Skills(Slot0~2/Icon,Label)`·`Hp(HpBack/HpFill,HpText)`·
`Divider`·`ArtFrame`·`Border` 신설. 카드 하위 총 33개. 저장 후 `HUD_Portrait` 가 다시
비활성인지 확인했다.

⚠ `NeoDunggeunmo SDF.asset` 이 같이 바뀐다 — Dynamic 아틀라스가 글리프 6자를 추가로 구운
것이다(655→661). 정상 동작이고 되돌려도 런타임에 다시 구워진다.

### 검증

`recompile_scripts` 에러 0·경고 0 · 콘솔 에러 0 · 저장된 씬 YAML 재파싱으로 위치/크기/피벗 ·
글자칸 6개 폰트 guid 동일 · `HpFill` `m_Type: 3`(Filled) · `m_IsActive: 0` 확인.
**플레이 모드 검증은 안 했다** — 유저가 직접 볼 것(112-6절).

### 씬반영요청 목록

없음.

---

## UI-34. 캐릭터 칭호 컬럼 신설 — 5명 전원 (2026-08-19)

> 상세는 `진행상황.md` **112-7절**.

UI-33 이 "캐릭터에는 칭호 데이터가 없어 항상 빈칸"으로 남긴 것을 채웠다.

| id | 이름 | 칭호 | 영어 칭호 |
|---:|---|---|---|
| 9001 | 엘린 | 눈먼 파수꾼 | The Blind Sentinel |
| 9002 | 비기오르 | 무너지지 않는 방벽 | The Unbreaking Bulwark |
| 9003 | 프레이야 | 굶주린 사냥꾼 | The Starving Hunter |
| 9004 | 피올로 | 역병을 걷는 의사 | The Plague Walking Doctor |
| 9005 | 히스톤 | 죽음을 딛는 복수자 | The Avenger Beyond Death |

- 표에 `character_title` / `character_title_EG` 2컬럼을 **기존 7칸 뒤에** 붙였다
  (`wave_top_boss` 의 칭호 2컬럼과 같은 짜임).
- `CharacterDefinitionSO.titleKey`/`title`/`Title` 신설 + `CharacterUnit.Title` 재정의.
  표가 비면 키도 비어 **칭호 칸이 그대로 빈칸**으로 남는다(유저 규칙 유지).
- 문구를 바꾸려면 `Tools/add_character_title_column.py` 의 `TITLES` 한 곳만 고치면 된다.

### ⚠ 겪은 함정 2개 (COM 스크립트 공통)

1. `convert_tables_to_string_keys.py` 가 **경고 출력에서 죽으면 변환이 통째로 안 된다** —
   경고 출력이 저장보다 앞이다. cp949 콘솔에서 `—` 한 글자에 죽었고, "변환 계획"만 찍혀
   성공한 것처럼 보였다. `sys.stdout.reconfigure(utf-8)` 추가.
2. **`EnsureDispatch` 는 유저가 엑셀을 켜 두면 실패**하고, `Dispatch` 로 바꾸면 **유저 창에
   붙어 `app.Quit()` 이 그 창을 닫아 버린다**. `DispatchEx`(항상 새 인스턴스)로 세 스크립트를
   고쳤다. ⚠ `link_string_keys.py` 는 아직 `EnsureDispatch` 다.

### 소유권 (§2)

**UI 소유** — `Scripts/Units/CharacterDefinitionSO.cs · CharacterUnit.cs`,
`Resources/Characters/**` · `Resources/Data/StringTable.txt`.
**⚠ PROTO 소유** — `Tools/gen_character_assets.py · gen_string_table.py ·
convert_tables_to_string_keys.py · add_boss_skill_erosion_column.py`, 신규
`Tools/add_character_title_column.py`. 기존 크로싱과 같은 종류.

### 씬 변경 여부 — 없음

### 검증

재실행 멱등 · `.meta` 20개 guid 불변(내용 diff 0) · 패시브 에셋 15개 내용 무변경 ·
캐릭터 에셋 5개는 `titleKey`/`title` 2줄만 추가 · recompile 에러 0·경고 0 ·
작업 후 유저 Excel 창 생존 확인.

---

## UI-35. 상세 카드 상태 칸 — 평상시엔 빈칸으로 (2026-08-19)

> 상세는 `진행상황.md` **112-8절**.

유저 피드백: "레벨 옆 방어 / 보스 이름 옆 웨이브몬스터 없애줘. 테이블 컬럼도 있으면 제거."

- **표 컬럼부터 확인** — 캐릭터/웨이브 몬스터 테이블 전체 시트를 훑었지만
  `state`/`상태`/`duty` 컬럼은 없었다. `StateTextOf` 는 표를 읽은 적이 없다
  (`CharacterBehavior.Duty` 는 런타임 값, `Faction` 은 코드 고정값) — 지울 컬럼이 없다.
- `UnitPortraitPanel.StateTextOf` 에서 `DutyLabel`(캐릭터 임무)·`FactionLabel`(몬스터 소속)
  폴백을 제거. **구속·정신 이상만** 채우고 나머지는 빈칸.

### 소유권 (§2)

**UI 소유** — `Scripts/UI/UnitPortraitPanel.cs`. 씬 변경 없음.

### 검증

recompile 에러 0·경고 0 · 렌더 비교로 히스톤/카시노마 평상시 빈칸 확인,
구속 상태 렌더로 기능 유지 확인 · 두 테이블에 관련 컬럼 없음을 재확인.

---

## UI-36. 구속(기절) 상태의 화면 표시 이름을 스킬 데이터로 (2026-08-19)

> 상세는 `진행상황.md` **112-9절**.

112-8절이 지운 것은 평상시 필러(임무·소속)였다. 이번엔 「구속」의 이름 자체가
하드코딩("구속" 리터럴)이던 것을 스킬 데이터 컬럼으로 내렸다 — 아니사킬의 「거대한
위협 포효」는 정의문이 스스로 "기절"이라 부르는데 화면에는 항상 "구속"만 뜨고 있었다.

- 표 2곳에 `status_name` 컬럼 신설(기존 컬럼 뒤에 그냥 붙임): 웨이브 몬스터 테이블
  (130003 구속탄 — 비움, 기본값 유지) · 임시용 중립 몬스터(2004 거대한 위협 포효 — "기절").
- `BossSkillSO.StatusName` → `BossSkillCaster.Bind()` → `UnitCombat.ApplyBind(seconds, label)`
  → `BoundLabel` → `UnitPortraitPanel.StateTextOf`. 0.2초 주기 갱신이 이미 있어서
  "바로바로 바뀌게"는 별도 이벤트 없이 자동으로 성립한다.
- 레이아웃: `Level` 374→366(폭 40), `State` 422→408(폭 58) — **기존 오브젝트 재사용**,
  새로 만든 것 없음.

### 소유권 (§2)

**UI 소유** — `Scripts/Combat/BossSkillSO.cs · BossSkillCaster.cs · UnitCombat.cs`,
`Scripts/UI/UnitPortraitPanel.cs`, `Resources/BossSkills/**`, `Resources/Data/StringTable.txt`,
`Assets/Scenes/Proto_01.unity`.
**⚠ PROTO 소유** — `Tools/gen_string_table.py · convert_tables_to_string_keys.py ·
sync_tables_to_assets.py`, 신규 `Tools/add_boss_skill_status_name_column.py`.

⚠ 파이프라인 재실행으로 다른 세션이 표에만 넣어둔 고르도네 스킬(2005·2006) 등도 같이
에셋으로 반영됐다 — 표가 정본이라는 원칙의 자연스러운 결과.

### 검증

recompile 에러 0·경고 0 · 재실행 멱등 · `BossSkill_130003`(빈칸)/`BossSkill_2004`
(`status_name_2004`) 확인 · 렌더로 기절 표시/평상시 빈칸 비교 확인.

---

## UI-37. 말파스도 "기절"로 통일 + 로스터 패널 구속 표기 (2026-08-19)

> 상세는 `진행상황.md` **112-10절**.

- 표: 말파스 구속탄(130003) `status_name`을 빈칸→**"기절"**로 변경. 발동 조건
  차이(15초 내 2회 피격 vs 즉발)는 기존 스킬 데이터·전이 로직이 이미 처리하므로
  코드 변경 없음.
- `CharacterRosterPanel.RefreshValues()` — 구속(`UnitCombat.IsBound`/`BoundLabel`)을
  정신 이상보다 먼저 확인해 로스터 "현재 상태" 칸에도 표시. 구속은
  `HudTheme.TextDanger`, 정신 이상은 기존 `TextErosion`으로 색 구분.

### 소유권 (§2)

**UI 소유** — `Scripts/UI/CharacterRosterPanel.cs`, `Resources/BossSkills/BossSkill_130003.asset`,
`Resources/Data/StringTable.txt`.
**⚠ PROTO 소유** — 신규 `Tools/set_malphas_status_name.py`.

### 검증

recompile 에러 0·경고 0 · 파이프라인 재실행 후 diff가 의도한 파일로만 한정됨 확인.

---

## UI-38. ★★ 고르도네 두 마리가 벽에 끼고 겹친 채 멈추던 버그 (2026-08-19)

> 상세는 `진행상황.md` **116절**.

유저 리포트: *"Gordonae 몬스터 두 마리가 벽에 끼는 버그 및 두 몬스터가 겹쳐져서 움직이지 않는
버그 — 해당 AI 로직 확인 후 수정"*.

**두 증상은 한 원인의 두 얼굴이었다.** `NeutralMonsterWander.PickReturnDestination` 이
`ClampToRing` 이 낸 고리 경계 한 점을 **벽 판정 없이 그대로** 목적지로 쓰고 있었다 — 배회 추첨
(`PickDestination`)에는 `IsWalkable` 검사가 있는데 **복귀 경로에만 없었다.** 그 점이 벽이면
A* 는 근처까지만 데려다주므로 `arriveDistance`(1) 안에 못 들어가고, 아직 고리 밖이라
`_returning` 도 안 풀려 **후퇴 사격 상태로 벽에 붙어 영구 정지**한다. 같은 각도로 끌려나온 두
마리는 **같은 경계점**을 받으므로 같은 벽 자리에 겹쳐 선다 — 유저가 본 "두 마리" 는 우연이 아니다.
(비선공이라도 `retaliateChaseRange` 8 로 캐릭터를 쫓아가므로 고리 밖으로 끌려나갈 수 있다.)

- `Scripts/Units/NeutralMonsterWander.cs` — 복귀 목적지를 `NearestOpenSpot`(벽이면 근처 빈 칸)으로
  옮기고, 두 번 헛돌면 **각도를 흔든다**(`ReturnStallLimit`). 고리에 실제로 가까워지면 헛돎
  카운터를 되돌린다. 배회 추첨 앞쪽 2/3 은 **몸집 여유**(`HasBodyClearance`)까지 요구한다 —
  뒤쪽 1/3 에서 그 조건을 버리는 것이 중요하다(끝까지 요구하면 좁은 지형에서 **제자리에 굳는다**).
  ⚠ 서식지 모드(에픽)는 여유를 요구하지 않는 `NearestWalkable` 을 쓴다 — 몸집 반경 3.7 이라
  여유를 요구하면 대기 지점이 서식지 중앙에서 최대 8칸 밀려난다.
- `Scripts/Combat/UnitCombat.cs` — 겹침이 풀리지 않은 이유 셋을 같이 고쳤다.
  ① `separationRadius` 가 인스펙터 고정 0.55 로 **몸집을 안 봤다**(고르도네 실측
  `BodyRadiusTiles` = **0.854** → 그림 68% 겹침). 이제 `max(인스펙터 값, 내 몸집 + 상대 몸집)`.
  `TargetRadius` 와 **같은 값**을 읽어 근접 사거리와 안 싸운다(공격 위치에서는 밀림이 0).
  ② 밀림이 `Step()` 안에만 있어 **`Idle` 에서는 아예 안 돌았다** → `UnstackWhileIdle` 신설.
  ★ **귀환 지점을 같이 옮기는 것**이 핵심 — 안 옮기면 0.3타일 귀환 판정이 되돌려 보내 제자리에서
  덜덜 떤다. ③ **정확히 겹친 쌍을 `continue` 로 건너뛰고 있었다** → `instanceId` 비교로 서로
  반대쪽을 정해준다. 추가로 `WallClearance` — 몸집 큰 유닛을 벽에서 **막지 않고 민다**(하드
  판정은 몸집보다 좁은 통로를 통째로 막아 중심 기준 A* 와 어긋난다). ⚠ 거리는 칸 중심이 아니라
  **테두리**까지 잰다(`DistanceToCell`) — 중심으로 재면 옆칸 벽이 1.0 으로 잡혀 판정이 무효가 된다.
- `Scripts/Units/MonsterUnit.cs` · `NeutralMonsterUnit.cs` — `_animatorResolved` 로 **「없다」도
  캐시**한다. `Separation()` 이 주변 유닛 전체의 몸집을 매 프레임 읽게 되면서, 스킨 없는 종
  (1001~1003)에서 `GetComponent` 가 **유닛 수의 제곱**만큼 돌 상태였다.

### 씬 변경 여부

**없음** — 원인이 전부 AI 로직이다. 유저 지시(하드코딩 금지 · 객체는 MCP 로)에 따라 코드로 씬
오브젝트를 만들지 않았고, 새 값은 기존 인스펙터 필드(`separationRadius`)를 **덮어쓰지 않고
하한으로만** 쓰므로 MCP 로 고칠 것도 없었다. `save_scene` 호출 안 함(세션 시작 시점에 이미
`isDirty: true` — 그 dirty 는 이 작업의 것이 아니다).

### 소유권 (§2)

`Scripts/Combat/**` · `Scripts/Units/**` 는 §2 의 PROTO 제한 목록
(`Scripts/Wave|Map|Fog|Build`)에 **없다**. 다만 110·113·115절이 같은 폴더를 고치면서도 소유권을
적지 않았으므로 단정하지 않는다 — 이번 변경은 전부 기존 함수의 내부 로직이고 공개 API·직렬화
필드를 하나도 늘리지 않았다.

### 검증

`recompile_scripts` **에러 0 · 경고 0**(4회) · 플레이 모드 **콘솔 에러 0**(기존 경고
`NeutralMonster_7_Template` 미존재 1건만 — 114-3절 미결) · 고르도네가 실제로 스폰돼
`State: Chase` 로 이동 중(위치 체비셰프 58.3 → 고리 50~99.5 안 · `IsRetreatFiring: false`).
116-2 의 근거 수치는 **런타임 실측**이다.

⚠ **재현 조건 자체는 재현하지 못했다** — 116-1 의 굳는 경로는 캐릭터가 고르도네를 고리 밖까지
유인해야 성립한다. 유저가 직접 때려서 끌고 나갔다 놓아주는 상황을 몇 번 만들어 확인해야 한다.

---

## UI-39. 아니사킬 서식지 버그 + 맵 랜덤 생성 (2026-08-19)

> 상세는 `진행상황.md` **118절**.

**① 아니사킬 서식지 — 타일은 다 있었고, 빠진 건 표의 한 줄이었다.**
`AnisakilHabitat`(16) · `Edge`(16) · `Props`(32) 가 110-6절 그대로 살아 있고 색조·알파도
정상이라 **다시 만들지 않았다**. `habitat_design` 시트에 아니사킬 줄이 **백업 15개 전부에
걸쳐 한 번도 없었다**(110-2절은 넣었다고 적어놨지만 실제로 저장되지 않았다).
★★ 그 상태가 **콘솔에 경고 한 줄도 안 남긴다** — `habitatTileAsset` 이 비면
`LoadHabitatTiles` 가 첫 줄에서 null 로 빠지고 경고 코드는 그 아래라 도달하지 못한다.
그 구멍도 같이 막았다(`NeutralMonsterSpawner.PaintHabitat` — **에픽인데** 비면 경고.
빈 칸은 일반 종에게는 정상이므로 조건을 에픽으로 좁혔다).
⚠ 파이썬이 `.asset` 을 고쳐도 **Unity `Assets/Refresh`** 를 해야 반영된다 — 안 해서 처음엔
그대로 실패했고, 새 경고가 정확히 그 상태를 잡아줬다.
범위 값은 카르시노스와 같게 **14/8/1** (110-2절이 두 에픽을 같은 보상·재생성·콜라이더의
동급으로 맞춰놨다). 결과: `아니사킬 서식지 542칸 · 데코 57개`.

**② 맵 랜덤 생성 — 런타임 생성 자체가 꺼져 있었다.**
`generateOnAwake = false` 라 게임은 **씬에 구운 320x320 타일맵**을 쓰고 있었고(그래서 씬이
38MB), 켜기만 해도 `config.seed` 고정이라 같은 맵이 나온다. 씨앗을
**이어하기 > 무작위 > 고정** 3갈래로 정하게 했다(`MapGenerator.ResolveStartupSeed`).
★ 저장에 `mapSeed` 를 신설하고 판을 **1 → 2** 로 올렸다 — 이 칸이 없으면 이어하기가 다른
지형을 만들고 그 위에 저장된 좌표로 유닛을 되살려 캐릭터가 벽에 박힌다. 옛 세이브는 씨앗이
없어 올바른 이주 경로가 없으므로 **거부가 맞다**.
★ 씨앗 결정을 **Awake 에서 당겨 읽는** 이유 — 맵을 읽는 쪽이 전부 `Start` 인데 오브젝트 간
Awake 순서는 보장되지 않아 "밀어 넣기" 구조를 만들 수 없다. 복원 시점 재생성도 못 쓴다
(그때는 안개·초기 개체·서식지가 이미 있다).

### 씬 변경 여부

**있음** — `MapGenerator.generateOnAwake` · `randomizeSeedOnAwake` (전부 **MCP** · `save_scene` 1회).
⚠ 저장 뒤 씬 파일이 **+29바이트**만 커진 것을 확인 — 플레이 모드에서 생성된 타일맵이 씬에
구워지지 않았다는 증거다(구웠다면 MB 단위로 변한다).

### 소유권 (§2)

⚠ **`Scripts/Map/**` 는 §2 상 PROTO 소유**다(5397·2321절이 그렇게 적었다). 맵 랜덤 생성이
지시 내용이라 불가피하게 손댔고, 변경은 `Awake` + 씨앗 결정 함수 하나로 국한했다.
`Scripts/Save/**` · `Scripts/Units/**` 는 그 목록에 없다. `Tools/*.py` 는 PROTO 소유
(신규 `table_update_20260819_anisakil_habitat.py`).

### 검증

`recompile_scripts` 에러 0·경고 0(5회) · 콘솔 에러 0 · 서식지 542칸 실제 출력 ·
새 경고가 Refresh 전후로 켜졌다 꺼지는 것 확인 · 연속 두 판 `ActiveSeed`
**944187279 → 1353762651** 이고 두 판 모두 `Walkable` 102,400칸 · `SpawnGates` 4 ·
FlowField/Fog `IsReady = true` · 표 시트 5개 보존(`DispatchEx`).

⚠ **기존 세이브(v1 · 웨이브 20)는 이제 "이어하기"에서 거부된다.** 씬의 `startWave` 가 20 이라
웨이브 20 테스트는 세이브 없이도 되므로 영향은 작다고 판단했다.

### UI-39 이어서 — 서식지 범위 값을 표 컬럼으로 (2026-08-19)

유저가 118-1절 미결 항목을 지적: *"테이블에 청크값 넣어줘"* → *"아니사킬 청크값"*.

`habitat_design` 시트에 `habitat_radius_tiles`·`habitat_chase_tiles`·
`habitat_idle_slack_tiles` 컬럼을 신설하고 카르시노스(1101)·아니사킬(1102) 둘 다 지금
에셋 값(14/8/1)을 그대로 옮겨 적었다. `Tools/sync_tables_to_assets.py` 의
`EPIC_HABITAT_SEED` 하드코딩 딕셔너리는 표에서 읽도록 바꿨지만 **seed-only 규칙**(에셋에
필드가 이미 있으면 절대 덮어쓰지 않음)은 그대로다 — 매직 넘버가 표 컬럼으로 옮겨간 것뿐,
"타일 계산 값들은 에딧에서 수정할 수 있도록" 이라는 예전 유저 지시는 안 뒤집었다.

검증: sync 재실행 → `git status` 로 `Assets/` 아래 아무 파일도 안 바뀜 확인(완전 멱등).

상세는 `진행상황.md` **118-3절**.

---

## UI-40. 엘린 스킨 재제작 · 40웨이브 · 몬스터 스탯 상한 · 시그리드 · 스킬 아이콘 66종 (2026-08-20)

> 상세는 `진행상황.md` **119절**.

### ① ★★ 엘린 스킨 — 시트 두 장 재분해 (`Elin_asset_02.png` 정본)

두 판본을 줄 단위로 다 재봤고 **모든 줄에서 `_02` 가 이기거나 같아** 정본이 하나다
(대기 7→8칸 · 이동이 **좌/우 전용 원화 두 줄** · 프레임 번호 있음). 근거 표는
`Tools/elin_skin_build.py` 맨 위.

이 시트가 낸 함정 셋 — 전부 **공통 부품**(`Tools/skin_sheet.py` 신설)으로 옮겼다:

1. ★★ **두건·눈가리개 흰색이 배경(254)과 같은 색**이다. 「흰색과의 거리」로 알파를 만들면
   얼굴 반쪽이 사라진다 → 배경을 **테두리와 이어진 덩어리**로만 정의(`background_mask`).
2. ★★ **은발이 발밑 그림자와 채도·광도가 겹친다.** 카시노마·라린길이 쓰던 「채도 낮고
   밝으면 그림자」 흐름을 그대로 쓰면 머리카락을 타고 번져 **시트 전체 38,917px** 이
   지워진다 → 프레임 아래 **14px 띠 안으로 가둔다**(`shadow_in_box`).
3. ★ 근거리 5번 칸의 **원호 안쪽 3,323px 이 흰 원판**으로 남는다(갇힌 배경) →
   「면적 크고 **먹선에 둘러싸인**」 것만 되돌린다. 회복 이펙트의 밝은 속은 테두리가 밝아
   안 걸린다(`enclosed_background`).

★ **크기 정규화를 하지 않았다** — 원거리·회복·마법이 **무릎 꿇는** 동작이라 세로가 원래
짧다. 말파스식으로 대기 키에 맞추면 꿇은 자세가 서 있는 키까지 늘어난다. 한 배율로
그려졌다는 근거는 **머리 폭이 일곱 줄 전부 32~35px** 이라는 것(스크립트가 매번 출력).

### ② 마법/원거리를 **투사체 없이** — 스킨 칸 넷 신설

유저 지시: *"엘린의 마법/원거리 공격은 투사체 없이 적중대상 땅바닥에서 사슬이 올라오는 걸로"*.

⚠ `projectileFrames` 를 비우는 것만으로는 안 된다 — 비면 `ArtFor` 가 `FallbackArt` 로
내려가 **진영 기본 탄환(회색 화살)** 을 띄운다. 「그림이 없다」와 「탄환이 없어야 한다」는
다른 뜻이라 **의도를 적는 칸**이 필요했다.

| 신설 칸 | 왜 |
|---|---|
| `groundImpactOnly` | 켜면 탄환·섬광을 건너뛰고 **대상 발밑**에 착탄만 깐다 |
| `magicRight/Left` + `SkinAttackMotion.Magic` | 시트가 마법과 원거리를 **다른 동작**으로 그렸다(5번 프레임 65x62 vs 55x74) |
| `magicImpactFrames` | 적중 이펙트가 **갈색 사슬 / 보라 사슬** 두 벌이다 |
| `healFxFrames` | 회복에는 **연출이 하나도 없었다**(`PerformHeal` 이 체력만 올렸다) |

전부 **비어 있으면 예전 동작 그대로**라 기존 스킨은 아무것도 안 바뀐다.

### ③ ★★ 스킨 에셋을 **유니티가 직접** 만든다 (`Editor/CharacterSkinBuilder.cs` 신설)

유저 지시: *"하드코딩 하지 말고 스킨 에셋 만들어서 mcp 로 직접 넣어줘"*.

예전에는 `gen_*_skin.py` 가 **프레임 .meta 에서 guid 를 읽어 YAML 을 엮었다** — 캐릭터마다
거의 같은 스크립트가 여섯 벌이었고, .asset 에 **빈 줄 하나만 들어가도 뒤 필드가 전부
무시**된다(8절 3번). 이제 **폴더 이름이 곧 스킨 칸**이고 유니티가 `AssetDatabase` 로 만든다.
MCP `execute_menu_item` 으로 부른다.

원화만 봐서는 알 수 없는 값(재생 속도·투사체 유무)은 분해 스크립트가 원화 폴더 옆에
`_skin_spec.txt` 로 남기고 빌더가 **리플렉션으로** 같은 이름 필드에 넣는다 — 캐릭터가
늘어도 C# 은 안 바뀐다.
⚠ `measure_skin_tiles.py` 주석의 *"MCP 에는 SO 에셋을 다루는 도구가 없다"*(59-2절)는
이제 옛말이다 — 이 파일이 그 도구다.
⚠ MCP `execute_menu_item` 은 **이름이 정확히** 맞아야 찾는다. 괄호·밑줄이 섞인 긴 메뉴
이름은 실제로 못 찾았다 → 메뉴 이름을 짧게 두고 설명은 주석에.

### ④ 웨이브 40 확장 + **마리 수 단조 증가**

유저 정정: *"웨이브 처음부터 계속 동일한 숫자가 나오지 말고 점진적으로 나오는 몬스터 수를
늘리는 방식"*.

★★ 실제로 바뀐 것은 **보스 웨이브의 「움푹 파임」**이다 — 9웨이브 18 → **10웨이브 9** →
11웨이브 17 처럼 보스 웨이브마다 잡몹이 절반으로 떨어졌다. 이제 한 번도 안 줄어든다:
**8마리(1) … 62(20) … 102(40)**. 비보스 웨이브 값은 기존과 ±2 안쪽이라 **바뀌는 것은
보스 웨이브뿐**이고, 그만큼 어려워진다(10웨이브 위협도 1.89배 · 20웨이브 1.72배).

배율은 21웨이브부터 **눕혔다** — 웨이브당 ×1.12 를 그대로 이으면 40웨이브가 17,800% 가
되어 캐릭터 상한 100 으로는 원리적으로 못 막는다. 1850% → **3000%** 로 완만하게.

### ⑤ ★★ 몬스터 능력치 상한 — **공격만 자르고 체력은 그대로**

유저: *"후반에 그냥 캐릭터가 녹아버려서 기존처럼 체력배율로만 플러스를 주고 스탯 상한은
어느 정도 조절해야 할듯"* / *"잡몹도 체력 배율 줘도 됨"*.

원인은 **비대칭**이다 — 몬스터 능력치는 배율로 무한히 오르는데 **캐릭터는 statMax 100 에서
멈춘다**. 방어력이 비율 감소라 무적이 안 되므로 공격력이 계속 오르면 언젠가 두 대를 못 버틴다.

`Assets/_Project/Data/Combat/BalanceConfig.asset` (인스펙터에서 수정):

| 칸 | 기본 | 뜻 |
|---|---|---|
| `monsterHpStatMax` | **0 = 무제한** | 체력은 계속 오른다(유저 확정) |
| `monsterAttackStatMax` | **60** | 잡몹 공격 상한 — 대략 13~15웨이브에서 닿는다 |
| `bossAttackStatMax` | **120** | 보스만 두 배. 안 나누면 후반에 보스와 잡몹 한 대가 같아진다 |

⚠ 방어·재생·명중·치명·저항은 **애초에 웨이브 배율을 안 받는다**(96-1절) — 칸을 안 만들었다.
⚠ 96절이 뗀 것은 `statMax`(캐릭터 강화 상한)이고 이건 **몬스터 전용**이라 다른 값이다.

### ⑥ 시그리드(9006) 구현

스킨은 **두 장이 서로 없는 줄을 갖고 있어** 라린길처럼 줄 단위로 골랐다 — `_02` 가 대부분,
**원거리 공격(7)·원거리 투사체(8)는 `_01` 에만** 있다. 시그리드는 엘린과 반대로 **투사체를
쓴다**(점→별→폭발로 자라 목표에서 저절로 터진다 · 29-9절 침 탄환과 같은 성질).

표의 리터럴을 **스트링 키로** 옮겼다(기존 파이프라인 `gen_string_table` →
`convert_tables_to_string_keys` 그대로): 이름·스킬 이름 3·설명 3·정의문 3 = 10칸.
⚠ 유저 확정 *"스킬 이름은 테이블 우선"* — 시트의 「섭취/고통 선사/사디즘」이 아니라
표의 「가학증/고통의 기쁨/통제할 수 없는 쾌락」이 정본이다.
칭호는 비어 있어서 유저 지시로 **환희에 젖은 순교자 / The Ecstatic Martyr** 를 넣었다.

패시브 3종을 **실제로 구현**했다(그동안 enum 에도 없었다):

| 스킬 | 어떻게 |
|---|---|
| 가학증 | 때린 적이 value01 초 안에 죽으면 value02% 확률로 **지름** value03 안 아군을 자기 현재 체력의 value04% 만큼 회복시키고 자신은 그만큼 잃는다 |
| 고통의 기쁨 | 가학증이 터질 때마다 공속 +value01%, value02초. **중첩 없이 지속시간만 초기화** — 「허약」과 같은 짜임(`UnitCombat.ApplyHaste`) |
| 통제할 수 없는 쾌락 | 체력이 최대의 value01% 아래로 내려가면 value02초 **무적**. 회복은 되고 체력 변화로 안 풀린다 |

⚠ 무적은 `DamageableUnit.ApplyDamage` **한 곳**에만 걸었다 — 체력이 깎이는 자리가 거기
하나라 위쪽 경로마다 막으면 새 경로가 조용히 뚫는다.
⚠ 「가학증」의 자기 대가는 `LoseHpToSelfCost`(신설)로 깎는다 — `ApplyDamage` 로 깎으면
**무적이 자기 대가까지 막아** 스킬이 공짜가 된다. 이걸로는 죽지 않는다(체력 1에서 멈춤).

### ⑦ ★★ `value_04` 컬럼이 **버려지고 있었다** (발견·수정)

`PassiveSkillSO` 에 `value04` 칸이 없어서 표의 네 번째 값이 통째로 버려지고 있었다.
그리고 `gen_character_assets.py` 가 Skill 시트를 **컬럼 번호로** 읽고 있어서, 그 컬럼이
생긴 뒤로는 **쿨타임이 value_04 를, 아이콘이 쿨타임(숫자)을, 플레이버가 아이콘 이름을**
읽을 상태였다. 다행히 그동안 한 번도 안 돌려서 에셋은 멀쩡했다 — **다음 실행이 전부
망가뜨릴 상태였다.** 필드명으로 읽게 고쳤다(sync 쪽 read_rows 와 같은 규칙).

### ⑧ 스킬 아이콘 66종 — 34개 스킬 전면 재배정

새 시트 두 장(6x5 · 6x6)을 잘라 `Resources/SkillIcons/` 에 넣었다. 기존 24개와는
**스타일이 다른 세트**(테두리 없는 발광 ↔ 장식 테두리 픽셀아트)라 섞으면 어긋나 보이므로
**전부 새 세트로** 옮겼다. ★ 그 김에 **아이콘이 없던 16개**를 처음 채웠다 — 보스 스킬
10개·중립 6개는 컬럼만 있고 **한 번도 채워진 적이 없었다**.
⚠ 이름이 기존과 겹치면 옛 그림을 덮어쓴다(`poison_skull` 로 한 번 겪었다) — 스크립트가
스스로 검사한다.

### ⑨ 라린길 「돌진 이펙트」를 날아가는 투사체로

유저 지시: *"라린길 돌진 이펙트를 투사체의 형태로 만들어서 날아가는 모습 연출"*.
시트의 「3. 근거리 공격 이펙트」(초승달 참격 4장)는 **뽑아만 두고 버려져 있었다**
(115절: "「평타 이펙트」 칸이 없다"). `meleeTravelFrames` 칸을 만들어 근접 평타에 실었다.
⚠ `projectileFrames` 에 넣으면 안 된다 — 그 칸은 **원거리·마법에서만** 돌고 라린길은
`atk_type = Melee` 다. 공격 유형을 바꾸면 사거리·명중·치명 판정이 전부 달라진다.
라린길 스킨도 새 빌더로 옮겼다(옛 `gen_laryngeal_skin.py` 는 **실행을 막아** 뒀다).

### ⑩ 베일(120005) — 표·스트링·일러스트만. **스킨은 못 만들었다**

★ 표에서 **버그 하나를 찾았다**: `skill_explain` 이 130009·130010 둘 다 **라린길의 키**
(130007·130008)를 가리키고 있었다. 윗줄 복사 흔적이고, `convert_tables_to_string_keys` 의
「이미 다른 키가 있다」 경고가 아니었으면 못 찾았다(게임은 조용히 엉뚱한 설명을 띄운다).

⚠⚠ **스킨은 만들지 못했다.** 원화 시트의 **프레임 수가 확정되지 않는다**:
헤더는 「대기 (16프레임)」인데 실제로 그려진 것은 **14장**이고(빈 열로 세면 정확히 14덩어리 ·
간격 94.6px), 라벨 번호도 **건너뛰거나 중복**된다(대기에 12·15 없음 · 원거리에 14 가 두 번).
줄마다 값이 달라 자동으로 정할 수 없다. 그래서 **보스 순환에서도 뺐다** — 스킨 없는 보스를
넣으면 `ResolveBossSlot` 이 경고를 내고 **기본 보스로 대신 내보낸다**(표와 화면이 달라진다).

### 씬 변경 여부

**있음** — `WaveManager.victoryWave` 20 → **40** (MCP · `save_scene` 1회).

### 검증

`recompile_scripts` 오류 0 · 경고 0 (5회) · 콘솔 오류 0 ·
엘린 프레임 109장/시그리드 149장/라린길 112장 **눈으로 확인**(체크무늬 위 합성) ·
스킨 배열 실측(엘린 15칸 · 시그리드 9칸 · 라린길 8칸) ·
`skin_sheet.py` 분리 뒤 엘린 출력 **md5 동일**(bc7dcfe2…) ·
패시브 에셋 15개 diff가 `value04` 추가 + 아이콘 교체 **둘뿐**임을 확인 ·
캐릭터 6명 `skinAssetName` 전원 배선 · 웨이브 40행 · 마리 수 단조 증가 확인.

### 아직 확인 못 한 것 (유저가 볼 것)

1. ⚠⚠ **베일 스킨** — 위 ⑩. 줄마다 실제 프레임 수를 알려주시면 바로 뽑는다.
2. **바리올라(1103)** — 표·정의·일러스트는 이미 있고 **스킨과 서식지 청크만** 남았다.
3. ⚠ **보스 웨이브가 어려워졌다**(10웨이브 1.89배 · 20웨이브 1.72배) — 실플레이 확인 필요.
4. **스탯 상한 60/120 은 계산으로만 잡았다** — 실제로 싸워보고 조정할 것.
5. **증원(`reinforce*`)이 1~40 전부 0** 이다(시스템이 사실상 꺼져 있다). 켤지 확인 필요.
6. 시그리드 `narrative`(플레이버 문장)가 비어 있다 — 다른 다섯은 채워져 있다.

### 씬반영요청 목록

- 없음

---

## UI-41. 바리올라 서식지 타일 · 베일 스킨 (두 시트 조합) (2026-08-20)

> 상세는 `진행상황.md` **120절**.

### ① 바리올라(1103) 서식지 — 타일 77종 + 표 한 줄

원본: `<볼트>/리소스/sprites/Variola_chunk.png` (1443x1090). 아니사킬 것과 달리 **라벨이
붙은 참조 시트**라 구획마다 좌표를 재야 했다. 시트가 스스로 적어놓은 구성 네 개 중
③ 벽의 「16종」은 **틀렸다** — 실제로 그려진 것은 12칸이다(격자 실측).
⚠ 베일 시트에서도 헤더 숫자가 틀렸다 — **이 작가의 시트는 헤더를 믿으면 안 된다**.

| 구획 | 결과 | 배선 |
|---|---|---|
| ① 바닥 2x8 | **16종** → `VariolaHabitat` | 서식지 바닥 |
| ④ 전이/경계 2x4 | **8종** → `VariolaHabitatEdge` | ★ 아니사킬과 달리 **원화가 있다**(파생 아님) |
| ② 데코 3줄 | **41종** → `VariolaHabitatProps` | 서식지 데코 |
| ③ 벽 2x6 | 12종 → `VariolaHabitatWall` | ⚠ 미배선 — `NeutralHabitat` 에 「벽」 칸이 없다 |

★★ **데코는 격자가 아니다** — 폭이 제각각인 프롭이 띠 위에 늘어서 있고 칸 구분선도 없다.
기준을 **시트 바탕(19)이 아니라 띠 배경(33~45)** 으로 잡아야 갈린다 — 시트 바탕 기준으로
재면 띠 전체가 한 덩어리가 된다(실제로 그랬다). 폭이 중앙값의 1.6배를 넘는 덩어리는
둘로 갈랐다(1번 칸의 결정+뾰족탑만 붙어 있었다).

★ **색은 손대지 않았다** (110절의 *"너무 이질적으로 만들진 마"*). 실측 H 290° · S 0.16 ·
V 0.19 — 카르시노스(309°)와 19° 로 가장 가깝지만 **바리올라만 형광 초록 균열**이 있어
화면에서 안 헷갈리고, 맵(V 0.37)보다 이미 훨씬 어둡다(88-5절의 «밝아서 유닛이 묻힘» 반대편).

⚠⚠ 표 `habitat_design` 에 **1103 줄이 없었다** — 118절 아니사킬과 **똑같은 구멍**이다.
`VariolaHabitat · 14 · 8 · 1` 로 넣었다(앞의 두 에픽과 같은 값 — `neutrality_mon` 에서
완전히 같은 등급이라 다르게 할 근거가 없다).

⚠ 「생성될 때마다 랜덤 서식지 · 저장 시 위치 보존」은 **이미 되어 있었다** — 새로 만든 것이
없다. 빠져 있던 것은 타일과 표 한 줄뿐이다(`NeutralMonsterSpawner.PaintHabitat` + 99-9절).

### ② ★★ 베일 스킨 — **판본을 줄별로 갈라 썼다**

유저 지시: *"베일 스킨 모션 동작 값은 너가 스킨 이미지 분석해서 적절한 값 지정해서 잘라서 써
내가 넣은 두가지 이미지 조합해서"*.

UI-40 에서 «장수가 많은 ``_02`` 가 정본» 이라고 판단했는데 **그게 틀렸다.**
``_02`` 는 같은 폭에 두 배를 그려 넣느라 **프레임이 서로 붙어 있어 칸을 가를 수가 없다** —
네 가지 방법을 다 재봤다:

| 방법 | ``_02`` 결과 |
|---|---|
| 빈 열 | 24칸이 **4~11칸**으로 붙는다 |
| 라벨 개수 | 두 자리 숫자가 붙어 16칸 줄에서 **13~14개** |
| 맨 앞·뒤 라벨 간격 | **맨 앞 라벨이 「1」이 아니다**(원거리 줄) — 한 칸씩 밀린다 |
| 폭 ÷ 장수 | 간격이 완전히 일정하지 않아 **프레임마다 옆 칸 망토가 딸려 온다** |

★ 반면 ``_01`` 은 **깨끗하게 떨어져 있다**(대기 8덩어리 · 폭 91~96px). 그림도 **1.4배 크게**
그려져 있다. 장수(8~13장)는 다른 유닛과 같은 대역이다(엘린·라린길·시그리드 대기 각 8).
→ **몸통 여섯 줄은 ``_01``**. 장수가 절반이어도 <b>자를 수 있는 쪽</b>이 정본이다.

★ **투사체만 ``_02``** — 이쪽은 반대다. 연기 구체는 서로 겹칠 일이 없어 ``_02`` 에서도
깨끗이 갈리고(폭 32~42px 로 고르다) 장수가 많을수록 부드럽다. ``_01`` 은 8장인데 폭이
31~72px 로 들쭉날쭉하다. 몸통과 투사체는 따로 재생되므로 판본이 갈려도 어긋날 곳이 없다
(라린길이 화염만 다른 판본에서 가져온 것과 같은 이유 · 115절).

결과 — **135장**: 대기 8 · 이동 9 · 근거리 8 · 원거리 10 · 담뱃대 13 · 담배연기 12 ·
투사체 14 · 반원형 범위 이펙트 1.

⚠ 패턴 두 줄은 **구획 제목·프레임 번호가 그림과 같은 y 에 걸친다**(캐릭터가 그만큼 높다).
잉크 밴드로 못 가르므로 y0 를 글자 아래로 직접 내려 잡았다 — 담뱃대 끝이 몇 px 잘리지만
글자가 프레임에 섞이는 것보다 낫다.

★ 새 공통 부품 :func:`skin_sheet.cells_by_clusters` — 덩어리를 그대로 쓰되
① 중앙값의 40% 미만인 **부스러기를 버리고** ② 1.55배를 넘는 **붙은 덩어리를 가른다**
(경계는 그 근처에서 잉크가 가장 적은 열로 옮겨 팔·망토를 안 자른다).

### ③ 베일 인게임 배선

- `Monster_Bale.asset` — `sync_tables_to_assets.py` 가 표에서 만들었다
  (hp 174 · 근접 18 · 원거리 15 · 방어 35 · 콜라이더 15x10 · 스킬 130009·130010)
- 보스 순환에 복귀 — **25웨이브**가 베일이다(표 순서대로: 5·10·15·20·25 → 120001~120005)
- 씬: `Monster_Bale_Template`(라린길 템플릿 복제 · **MCP**) + `skinResourceFolder` 를
  `MonsterSkins/Bale` 로(**MCP**) + `MonsterSpawner.bossSlots` 5번째 항목

⚠⚠ **`bossSlots` 항목만은 MCP 로 못 넣었다** — `update_component` 가
*"Expected object value for 'bossSlots'"* 로 거부한다(구조체 배열 미지원).
진행상황 5절이 적어둔 «SO 참조는 MCP 로 씬 배열 항목에 넣을 수 없다» 가 그대로 재현됐다.
그 **한 항목만** 씬 파일에 직접 넣었다 — 값(정의 guid · 템플릿 fileID)은 둘 다 이미
씬·에셋에 있던 것이라 새로 만든 것이 없고, 넣은 뒤 씬을 다시 열어 유니티가 5개로 읽는 것을
확인했다.

### ④ ★★ `MONSTER_ASSET_BY_ID` 하드코딩 제거 (버그의 원인이었다)

`sync_tables_to_assets.py` 가 «표 id → 정의 에셋 이름» 을 손으로 적은 표로 갖고 있었다.
여섯 줄이 전부 **같은 규칙**(표의 `ingame_asset` 인 `Char_Asset_X` → `Monster_X`)이라
표의 중복이었고, 그래서 베일을 추가했을 때 그 줄을 안 고쳐 **정의 에셋이 아예 안 만들어졌다.**
규칙(`monster_asset_name`)으로 바꿨다 — 이제 표에 몬스터를 추가하면 코드를 안 고쳐도 된다.
(UI-40 의 `SKIN_OVERRIDE`·`gen_character_assets.py` 와 같은 종류의 정리다.)

### 씬 변경 여부

**있음** — `Monster_Bale_Template` 신규(MCP 복제) · 그 `skinResourceFolder`(MCP) ·
`MonsterSpawner.bossSlots` 5번째 항목(⚠ 파일 직접 — 위 ⚠⚠).

### 검증

콘솔 오류 0 · 유니티가 `bossSlots` 를 **5개**로 읽는 것 확인(`get_gameobject`) ·
바리올라 타일 16/8/41/12 · `NeutralMonster_7.habitatTileAsset = VariolaHabitat` ·
`Skin_Bale` 배열 실측(대기 8·이동 9·근접 8·원거리 10·스킬1 13·스킬2 12·투사체 14·스킬2Fx 1) ·
베일·바리올라 프레임과 타일을 **눈으로 확인**(체크무늬/어두운 바탕 합성) ·
웨이브 40행 · 보스 5·10·15·20·**25(베일)**·30·35·40.

### 아직 확인 못 한 것 (유저가 볼 것)

1. **실플레이로 25웨이브 베일** — 스킨·정의·씬 배선까지만 확인했다. 담뱃대 강타(130009)와
   담배연기(130010)는 **범위 모양이 표에 `Line`·`Semi_Circle`** 인데 코드의
   `BossSkillShape` 에는 `Line`·`Circle` 뿐이다 — `Semi_Circle` 은 `Line` 으로 떨어진다.
   밀쳐내기·중독도 아직 없다(⚠ **미구현** — 아래 2번).
2. ⚠⚠ **베일 스킬 두 개의 «효과»는 아직 없다** — 시전 모션과 범위 연출은 나가지만
   ① 담뱃대 강타의 **밀쳐내기**(value_04 타일) ② 담배연기의 **부채꼴 범위**와 **중독 도트**가
   구현돼 있지 않다. `BossSkillShape.SemiCircle` 추가 + 두 효과가 필요하다.
3. **바리올라 스킨** — 서식지는 끝났지만 `Variola_asset.png` 의 모션은 아직 안 뽑았다.
4. 바리올라 **벽 타일 12종이 미배선** — 받을 칸이 없다.

### 씬반영요청 목록

- 없음

---

## UI-42. 캐릭터 스킬 `value_05` · 시그리드 「가학증」 후퇴기준 고정 (2026-08-20)

> 상세는 `진행상황.md` **121절**.

유저 지시: *"캐릭터 스킬에 밸류 5 추가하고 시그리드 스킬 변경점 있으니까 확인해보고 적용"*

### ① `value_05` 칸 신설

표(`캐릭터 테이블.xlsx` / `Skill`)에 `밸류타입_05` 컬럼이 생겼다. `PassiveSkillSO.value05` 를
만들고 `gen_character_assets.py` 가 그 칸을 옮긴다.

★ **컬럼이 또 늘었는데 아무 일도 안 났다** — UI-40 에서 그 시트를 «번호» 대신
**«이름»으로 읽게 고쳐 뒀기 때문이다**. 그때 안 고쳤다면 이번에도 쿨타임·아이콘·설명이
한 칸씩 밀렸을 것이다(그 사고가 실제로 대기 중이었다). 회귀 확인: 정화의 손길이
`value 4/20/50/0/0 · cool 120 · icon_heal_plus` 로 그대로다.

### ② ★★ 시그리드 「가학증」의 변경점 — **후퇴기준 고정**

정의문(`skill_type_desc_Sadism`)에 한 문장이 붙었다:

> 시그리드의 후퇴기준이 **{Value_05}%로 고정**됩니다.  ( value_05 = **5** )

★ **왜 이 문장이 그 스킬에 붙었는지** — 시그리드의 셋은 「체력이 바닥일 때 강해지는」
구성이다. 「통제할 수 없는 쾌락」이 최대 체력 **10%** 아래에서 무적을 주는데, 유저가
후퇴 기준을 30% 로 두면 **그 무적이 한 번도 안 켜진다**(그 전에 물러난다). 그래서
스킬이 후퇴 기준을 5% 로 못박아 «무적 구간까지 버티게» 만든다 — 세 스킬이 한 줄기다.

구현은 「선봉장」(히스톤 80013)의 역할 잠금과 **완전히 같은 짜임**이다 — 잠금이 두 겹이고
거는 순간 값을 스냅한다:

| 겹 | 어디 |
|---|---|
| 값을 거부 | `CharacterTactics.SetRetreatHpPercent` — `RetreatHpLocked` 면 무시 |
| UI 를 끔 | `TacticalOrderPanel` 의 +/− 버튼과 슬라이더 `interactable` |
| 걸고 스냅 | `CharacterPassives.ApplyAlwaysOn` → `SetRetreatHpLock(value05)` |

⚠ 잠금은 해금 목록이 바뀔 때마다 다시 걸린다 — 강화로 슬롯이 늦게 열려도 그 순간부터
적용되고, 스킬이 없으면 **푼다**(「걸었으면 되돌린다」 는 이 파일의 규칙).

### ③ ★ 곁에서 찾은 구멍 — 「초기화」가 잠금을 뚫었다

`CharacterTactics.ResetToDefault` 가 <b>잠긴 칸을 다시 걸지 않았다</b>. 그래서 「선봉장」이
걸린 히스톤도 **전술 창의 「초기화」 한 번으로 중위·원거리가 됐다** — 정의문의 「고정」이
깨진다. 후퇴 기준도 같은 구멍을 탔을 것이므로, 지침을 **통째로 갈아끼우는 두 경로**
(`SetOrder` · `ResetToDefault`)가 `ForceAllLocks()` 하나를 부르게 묶었다.

### 씬 변경 여부 — **없음**

### 검증

`recompile_scripts` 오류 0 · 경고 0 · 콘솔 오류 0 ·
가학증 에셋 `value 2/20/5/10/5` · 다른 스킬 17개는 `value05: 0` 만 늘고 나머지 동일(diff 확인).

### 아직 확인 못 한 것

1. 실플레이로 시그리드의 후퇴 기준이 5% 로 잠기는지, 전술 창의 슬라이더·버튼이 꺼지는지.
2. UI-41 의 미완(베일 스킬 효과 · 바리올라 모션 스킨)은 그대로다.

### 씬반영요청 목록

- 없음

---

## UI-43. ★★ 시그리드 일러스트 · 베일/바리올라 스킬 4종 · 엘린 이동 방향 · 바리올라 인게임 (2026-08-20)

> 상세는 `진행상황.md` **122절**.

유저 지시 7건: ① 시그리드 일러스트 미적용 ② 베일 스킬 두 개의 효과(밀쳐내기·부채꼴·중독)
③ 바리올라 모션 스킨 + 인게임 추가 ④ 엘린이 바라보는 방향과 반대로 걷는다
⑤ 시그리드 스킬 구현 확인 ⑥ 시그리드 초기 전술 포지션을 근거리로 ⑦ 전체적인 버그 수정.

### ⓞ MCP 경로 — `Tools/mcp_unity_cli.js` 로 돌았다

`~/.claude.json` 의 mcp-unity 서버가 이 세션에 **붙지 않았다**(서버 스크립트 경로가
`C:/Project/WeedHoldings/…` 의 PackageCache 를 가리킨다). Unity 쪽 브리지는 **멀쩡히
8090 에서 듣고 있었다** — 그래서 2026-08-15 에 만들어 둔 `Tools/mcp_unity_cli.js`
(같은 WebSocket 규약)로 전부 처리했다. **씬 YAML 을 직접 건드린 곳은 없다.**
⚠ 브리지 서비스 경로는 `ws://localhost:8090/`**`McpUnity`** 다(루트로 붙으면 501).

### ① ★★ 시그리드 일러스트 — **임포트 설정이 Default 였다**

`illust_Sigrid.png` 가 **`textureType: 0`(Default)** 로 들어와 있었다. 파일도 있고 표의
`illust` 칸도 맞는데 `Resources.Load<Sprite>` 가 **null** 을 돌려준다 — Default 텍스처에는
Sprite 서브에셋이 없기 때문이다.

★★ **이 실패가 거의 안 보인다** — `CharacterDefinitionSO.Illust` 는 경고를 한 줄 남기지만
**초상화를 처음 여는 순간에만** 뜨고, 그 뒤엔 `_illustLoaded` 캐시에 걸려 두 번 다시 안 뜬다.
콘솔을 나중에 보면 흔적이 없고 «그냥 안 나온다» 로만 보인다.

`Editor/SpriteImportFixer.cs` 신설 — **한 파일을 고치는 도구가 아니라 규칙**이다.
`Resources/` 아래 PNG 는 전부 코드가 `Resources.Load` 로 읽는 그림이므로 Sprite 가 아닐
이유가 없다. 그래서 대상을 이름으로 나열하지 않고 **폴더로** 정했고, 유니티의
`TextureImporter` 를 그대로 쓴다(`import_monster_illust.py` 처럼 .meta YAML 을 손으로
엮으면 유니티 버전이 `serializedVersion` 을 올릴 때 조용히 어긋난다).
MCP `execute_menu_item` 으로 부른다 — 실측 **133장 확인 · 1장 수정**.

⚠ 그 파이썬 스크립트는 **넣을 때만** 돌아서 이미 들어와 있던 파일은 안 고쳤다 —
시그리드가 정확히 그 구멍으로 빠졌다.

### ② ★★ 보스 스킬 **4종**이 enum 에 없었다 (2종은 새로 발견)

콘솔 경고가 둘 있었다: `BossSkill_130010('Pipe_smoke')` · `BossSkill_130009('Pipe_strike')`
**«종류를 알아보지 못했습니다 — 건너뜁니다»**. 즉 베일의 두 스킬은 «효과만 없는» 것이
아니라 **아예 시전되지 않았다**(UI-41 은 «시전 모션과 범위 연출은 나간다» 고 적었지만
그건 표만 보고 쓴 것이다).

★ 그 김에 **바리올라의 두 스킬도 같은 상태**임을 찾았다 — `Creepy_scar`(2005) ·
`Deadly_venom`(2006). 경고가 안 뜬 이유는 **바리올라가 스폰 자체를 못 하고 있었기**
때문이다(아래 ④) — 캐스터가 돌지 않으면 경고도 안 난다.

★ `BossSkillShape.SemiCircle` 신설. 표는 `Semi_Circle` 로 적어놨는데 enum 에 없어서
`Parse` 가 조용히 `Line` 으로 떨어뜨렸다 — «정면 반지름 5 부채꼴» 이 «5x1 상자» 가 되는데
**폴백이 정상 동작이라 경고 한 줄도 안 남는다**. 각도는 **180도**다: 표에 각도 칸이 **없어서**
코드에 숫자를 지어내는 대신 이름이 말하는 값을 쓴다(범위 값을 상수에서 표 컬럼으로 옮겨 온
118-3절의 규칙 — 각도를 조절하고 싶어지면 그때 표에 칸을 만드는 것이 맞는 순서다).

★★ **칸의 뜻이 또 달랐다** — 「정의문이 정본」이라는 규칙의 실전 예가 셋 나왔다:

| 스킬 | 함정 |
|---|---|
| 담뱃대 강타 130009 | `value_02` 가 **세로가 아니라 「명」**(대상 수)이다. 기본 직사각형 갈래에 태우면 «반지름 3 원형에 1명» 이 **3x1 상자**가 된다 → 구속탄처럼 **전용 갈래**를 태웠다 |
| 담배 연기 130010 | `value_02` 가 **「연기 지속 초」**다. `ResolveArea` 가 그걸 세로로 읽어 원화 비율에 맞추면 **반지름만 조용히 줄어든다** — 그 함수의 ⚠ 주석이 «세로로 긴 이펙트가 들어오면 사고» 라고 예고해 둔 자리다. 부채꼴도 **정사각 상자**로 바꾸고, 판정 반지름은 **표 값에서 직접** 읽는다 |
| 소름 끼치는 흉터 2005 | `value_03` 이 **침식량**이다. ⚠ 처음에 「폴백 없음」 목록에만 넣었다가 **침식 20 이 「공격력 20%」로 읽히는** 것을 리뷰에서 잡았다 — 있어서는 안 되는 피해가 생긴다. `DamagePercent` 를 **0 으로 못박았다** |

신설 프로퍼티: `MaxTargets`(0 = 제한 없음 — **기존 스킬 전부가 그 경우라 동작이 안 바뀐다**) ·
`SmokeSeconds` · `PoisonSeconds` · `PoisonMaxHpPercentPerSecond`.
`KnockbackTiles`·`MaxHpPercentDamage`·`ErosionValue`·`CircleValueIsRadius` 는 종류별로 갈라 읽게 확장.

**★★ 밀쳐내는 방향이 「죽음의 포효」와 다르다** — 정의문이 *"캐릭터는 **자신이 바라보는
반대 방향**으로 밀려납니다"* 라고 못박고 있다. 포효는 «시전자 반대쪽» 이다. 대개 같은
결과지만 **등을 보이고 도망치던 중에 맞으면 보스 쪽으로 끌려온다** — 그게 표가 말하는
것이고 「담뱃대로 후려친다」는 그림과도 맞는다. 그래서 `CharacterAnimator.FacingRight` 를
공개했다(애니메이터가 없는 구조물·포탑은 «시전자 반대쪽» 으로 떨어진다).

**★★ 「중독」 신설** — `UnitCombat.ApplyPoison`. 「허약」·「구속」과 **같은 자리**다
(되돌릴 보정이 없는 «시각 하나로 표현되는 상태»). 다만 **하나 다르다 — 스스로 피해를
낸다**: 프레임마다 조금씩 깎지 않고 **1초마다 한 번** 넣는다(정의문이 «매 초» 이고,
프레임 분할하면 ① 최대 체력이 작은 유닛에서 **반올림이 0 이 되어 아예 안 아프고**
② 전투 숫자가 초당 60개 뜬다). 피해는 **최대 체력** 비례이고 **올림**이다(남은 체력의
%로 하면 절대 안 죽고, 반올림하면 0 이 된다 — 「타오르는 숨결」과 같은 판단).
⚠ **중첩되지 않는다**: 연기 안에 서 있으면 매 프레임 다시 걸리는데, 중첩시키면 **몇
프레임 만에 즉사**한다. «더 아픈 쪽 + 지속시간 갱신» 으로만 끝낸다.
⚠ `TickPoison` 은 `Update` **맨 앞**이다 — 아래 두 갈래(벽 탈출·구속)가 **둘 다 return**
하므로 그 뒤에 두면 **벽에 끼거나 구속된 동안 독이 멈춘다**. 중독은 «행동» 이 아니라
«몸» 에 걸린 상태다. 화면 표기는 구속과 같은 자리에 넣었고 **우선순위를 두 창에서
같게** 맞췄다(구속 → 중독 → 정신 이상 → 임무 · `UnitPortraitPanel`·`CharacterRosterPanel`).

★ **연기가 `value_02` 초 동안 남는다**(`LingerSmoke`) — 정의문이 «연기를 {v2}초간
생성합니다» 이고 *"연기를 **맞은** 캐릭터는"* 이라 **나중에 걸어 들어온 캐릭터도 맞아야**
한다. 시전 순간만 판정하면 그 문장이 아무 뜻도 없어진다. 부채꼴은 **처음 잡은 자리·방향에
고정**이다(연기는 공기 중에 남는 것이고 보스를 따라다니지 않는다 — 「죽음의 노래」 연타
상자와 같은 판단). **피해는 다시 넣지 않는다** — 초당 피해는 「중독」이 담당한다.

### ③ 바리올라 스킨 — `Tools/variola_skin_build.py` 신설 (112장)

시트는 깨끗했지만 함정이 셋 있었다:

1. ★★ **라벨 번호가 건너뛴다** — 이동 「06」 없음(실제 7장) · 근거리 「03」 없음(9장) ·
   스킬2 「02」 없음(7장). 이 작가의 시트는 **헤더 숫자도 틀린 적이 있다**(120절).
   그래서 **라벨 개수(= 그려진 칸 수)가 정본**이고 번호는 무시한다.
2. ★★ **소의 뿔이 프레임 번호 줄까지 솟는다** — 스킬2 줄 라벨이 7개가 아니라 **9개**로
   잡혔다(폭 3~4px 짜리 가짜 덩어리 둘). `skin_sheet.LABEL_MIN_W` 신설 —
   `LABEL_MAX_W` 의 **반대 방향** 함정이다.
   ⚠⚠ **기본값을 8 로 뒀다가 엘린 분해가 «프레임 번호 0개» 로 죽었다** — 엘린 라벨은
   3~7px 다(바리올라는 9~12px). 라벨 글자 크기가 시트마다 달라 **전역 기본값으로 정할
   근거가 없다** → 기본값 **1(무동작)**, 필요한 스크립트가 `min_w=` 로 넘긴다.
3. ★★ **스킬2 줄만 라벨이 그림 띠 안에 걸친다**(독기 구름이 위로 퍼진다). 베일은 같은
   상황에서 y0 를 글자 아래로 내렸지만(120절) 여기서는 잘리는 것이 **스킬의 정체인 독기
   구름**이라 손실이 눈에 띈다 → `erase_label_patches` 로 **글자 자리만**(19x12px 창 7개) 지운다.

★ **방향 — 원본이 왼쪽을 본다**(베일·라린길과 **반대**). 아니사킬·고르도네 시트가 좌/우
라벨을 뒤집어 놓은 적이 있어(118절) 라벨을 안 믿고 실측했다: 대기 8프레임에서 위쪽
띠(뿔·머리) 무게중심 **0.367** vs 아래쪽 띠 0.557 — 뿔과 머리가 왼쪽에 몰려 있다.

★★ **새 공통 부품 `skin_sheet.reflood_background`** — 이 개체는 **네발**이라 발밑 그림자가
**다리 사이를 막아** 배 아래 배경이 바깥과 끊긴다. 끊긴 배경은 배경으로 안 잡혀 **불투명한
흰 웅덩이**로 남았다(배 밑에 흰 천이 붙은 것처럼 보였다 · 대기 한 줄에서 5,346px).
그림자를 지운 **다음에** 배경을 다시 흘려 해결. ⚠ `enclosed_background` 로는 안 된다 —
그쪽은 테두리가 **먹선일 때만** 되돌리는데 이 웅덩이의 벽 절반은 **연회색 그림자**다.
⚠ 지금까지 캐릭터·보스가 전부 **두 발**이라 이 함정이 없었다.

칸 가르기는 **줄마다 다르다** — 스킬2 만 `labels`(독기 구름이 이어져 덩어리가 7개가 아니라
**1개**로 붙는다 · 폭 809px), 나머지는 `clusters`.

**안 넣은 원화 넷** (`Unused_`): 방향 전환 4 · 스킬1/2 종료 후 경직 각 4 · 대시 돌진 잔상 4.
★ 경직을 **스킬 프레임 뒤에 붙이지 않았다** — 스킬 모션은 시전 시간 동안 **반복 재생**되므로
경직 자세가 중간에 계속 끼어들어 어색해진다. 잔상은 `skill1Projectile` 에 넣어도
`BossSkillCaster` 가 그 칸을 구속탄·이끌리는 혈취에서만 써 **영영 안 나온다** —
안 나오는 자리에 배선해 두면 「배선했다」는 기록만 남는다.

### ④ ★★ 바리올라 인게임 — **씬 템플릿이 없었다**

콘솔 경고: *«NeutralMonster_7 의 템플릿을 씬에서 찾지 못했습니다»*. 서식지·표·정의·일러스트는
UI-41 에서 다 끝나 있었고 **빠진 것은 하이라키의 템플릿 하나**였다.
`Anisakil_Template`(같은 에픽·서식지 있음)을 **MCP `duplicate_gameobject`** 로 복제해
`Variola_Template` 로 만들고, `definition`→`NeutralMonster_7` ·
`skinResourceFolder`→`MonsterSkins/Variola` 를 **MCP `update_component`** 로 넣었다.
⚠ 템플릿은 **비활성**이라 `GameObject.Find` 로 못 찾는다 — `newParentId`/`instanceId` 로
지정해야 한다(`objectPath` 는 실패한다).

★ `CharacterSkinBuilder` 가 **출력 폴더가 없으면 죽고 있었다**(«출력 폴더가 없습니다»).
새 유닛을 넣을 때마다 **반드시** 걸리는 단계다 — 분해 스크립트는 원화 폴더만 만들고
`Resources/MonsterSkins/<종>` 은 안 만든다. 실제로 스킨 4개는 만들어졌다고 찍히고
5번째만 에러 한 줄이었다. `EnsureFolder` 로 **유니티가 만들게** 했다.

### ⑤ ★★ 엘린 이동 방향 — **시트 제목을 믿은 것이 원인**

`elin_skin_build.py` 가 시트 제목("이동 (Move / Walk) - **좌 / 우**")대로 첫 줄을 `left`,
둘째 줄을 `right` 로 뒀는데 **그림은 반대**였다. 눈으로는 판단이 갈려서 **지표 두 개를
따로** 재서 확인했다 — 기준자는 **미러로 만든 줄**(근거리·대기: 원본이 오른쪽을 보는 것이
확실하다. 사슬이 오른쪽으로 뻗는다):

| 지표 | 기준자(오른쪽) | 첫 줄 | 둘째 줄 |
|---|---|---|---|
| 두건 **금색 십자** 가로 위치(두건 폭 비율) | 근거리 0.60 · 대기 0.52 | **0.61** ✔ | 0.42 |
| **은발** 중심 − 실루엣 중심 (머리는 뒤로 흐른다) | 근거리 오른쪽 −5.6px | **−8.4px** ✔ | +8.0px |

두 지표가 같은 답 — **첫 줄 = 오른쪽 · 둘째 줄 = 왼쪽**. `side` 값만 서로 바꿨다
(y 좌표는 시트 실측값이라 그대로). 수정 뒤 재측정: `Walk_Right` 십자 **0.606** · 머리결
**−8.44px** (기준자 0.597 / −5.61px 과 같은 쪽).
⚠ **다른 캐릭터는 이 함정에 안 걸렸다** — 이동 줄이 **좌/우 전용 두 줄인 캐릭터가
엘린뿐**이고 나머지는 한 줄을 미러하므로 방향이 어긋날 수가 없다.

### ⑥ 시그리드 스킬 — **이미 구현돼 있었다** (확인만)

표·에셋·코드를 전부 다시 읽었다. 세 패시브는 UI-40 ⑥ 과 UI-42 에서 **실제로 구현돼 있고
배선도 맞다**:

- 에셋 `Skill_80016~80018` 의 `skillType` 문자열이 `PassiveSkillTypes.Parse` 와 1:1 로 맞는다
- `Character_9006_Sigrid.passives` 세 칸이 그 에셋들을 가리킨다(guid 확인)
- `PassiveSkillService`(씬 `GameSystems`, 활성)가 매 프레임 `CharacterPassives.Tick` 을 돌리고,
  `HandleDied` → `OnRecentTargetKilled` → `TrySadism` 경로도 이어져 있다
- 후퇴 기준 잠금 두 겹(`SetRetreatHpPercent` 거부 · `ForceAllLocks`)도 그대로다

★★ **«구현이 안 된 것처럼 보인 이유는 둘»** 이다:
① 위 ① 의 일러스트 — 상세 카드/스킬 창의 그림이 **빈칸**이라 «아무것도 안 붙었다» 로 보인다.
② **생성 시점에는 「가학증」 하나만 켜져 있다** — 슬롯 2·3 은 강화 **5회·10회**에 열린다
(`PassiveUnlockConfig`, 캐릭터 전원 공통). 「고통의 기쁨」·「통제할 수 없는 쾌락」은
강화를 그만큼 해야 뜬다.
→ **새로 구현한 것은 없다.** 잘못 짚은 것이 있으면 어느 스킬이 어떻게 안 되는지 알려주시면
그 경로만 다시 본다.

### ⑦ 시그리드 초기 전술 포지션 → 근거리

`gen_character_assets.py` 의 `ROLE_OVERRIDE` 에 `9006: (1, 0)` 추가 — 히스톤(선봉장)이
쓰는 **바로 그 자리**다. 능력치 역산은 **원거리**를 고른다(원거리 8 > 근접 6). 그런데
스킬 셋이 **전부 붙어서 싸우는 전제**다: 「가학증」은 «때린 적이 2초 안에 죽으면» 이라
자기가 마무리를 쳐야 터지고, 「통제할 수 없는 쾌락」의 무적은 맞는 자리에 서 있어야 뜻이
있다(「가학증」이 후퇴 기준을 5% 로 못박는 것도 같은 방향 · UI-42).

⚠ **위치는 `Auto` 로 뒀다** — 유저가 말한 것은 「원거리 → 근거리」(공격 유형)뿐이다.
근거리 + 맷집(체력 6 + 방어 2 = 8)이라 역산이 **중위**가 되고, 그게 «무른 근접» 인
시그리드에게 맞는다(프레이야와 같은 자리). 전방으로 못박으면 가장 먼저 맞아 **무적 구간을
쓰기도 전에 죽는다**.
검산: 재생성 결과 **바뀐 줄이 `attackPreset: 0 → 1` 하나**이고 나머지 5명은 무변경.

### 씬 변경 여부

**있음** — `Variola_Template` 신규(MCP 복제) + 그 `definition`·`skinResourceFolder`(MCP) ·
`save_scene` 1회. 씬 파일이 **+5,831바이트**만 커진 것으로 타일맵이 구워지지 않았음을 확인.

### 검증

`recompile_scripts` 오류 0 · 경고 0 (5회) · **플레이 모드 진입 후 런타임 오류 0 · 경고 0**
(예전 경고 3개가 전부 사라졌다) ·
로그로 확인: `[보스스킬] 바리올라 스킬 2종 준비 (BossSkill_2006 → BossSkill_2005)` ·
`[NeutralMonsterSpawner] NeutralMonster_7 자동 등록 (템플릿 Variola_Template)` ·
`바리올라 서식지 513칸 · 데코 75개 (바닥 16 · 가장자리 8 · 데코 41)` ·
`[토벌] 에픽 몬스터 발견 — 바리올라 (얼금뱅이 소)` ·
`illust_Sigrid` 임포트 `textureType 8 · spriteMode 1 · alphaIsTransparency 1` ·
바리올라 프레임 112장을 **눈으로 확인**(체크무늬 합성 · 다리 사이 흰 웅덩이 제거 전/후 비교) ·
`Skin_Variola` 배열 실측(대기 8 · 이동 7 · 근접 9 · 스킬1 5 · 스킬2 7 · 스킬1Fx 6 · 스킬2Fx 6) ·
몸집 실측 2.141 x 1.844 타일 · 엘린 방향 지표 2종 재측정 ·
`git status` 로 **바뀐 PNG 가 엘린 이동 16장뿐**임을 확인(다른 캐릭터 무영향) ·
스킨 프레임 .meta **guid 무변경**(줄바꿈만 바뀜).

### 아직 확인 못 한 것 (유저가 볼 것)

1. ⚠⚠ **베일(25웨이브)·바리올라 스킬을 실전투로는 못 봤다.** 플레이 모드가 시작 단계까지만
   돌고 멈췄고(브리지로 재진입이 안 됐다) 보스가 스폰되기 전이었다. 확인한 것은
   «표 → 에셋 → enum 파싱 → 갈래 선택» 까지다. **직접 25웨이브를 돌려봐 주세요** —
   특히 ① 담뱃대 강타의 밀쳐내기 방향(도망치던 캐릭터가 **보스 쪽으로** 끌려오는 것이 정상)
   ② 담배연기 부채꼴이 **정면만** 덮는지 ③ 중독 초당 피해가 최대체력 2% 인지.
2. ⚠ **표의 `range_type` 과 정의문이 어긋난 줄이 하나 있다** — 담뱃대 강타(130009)는
   `Line` 인데 정의문은 *"반지름 {value_01}의 **원형** 범위"* 다. `value_02` 가 「명」이라
   직사각형으로 읽을 수가 없어 **정의문을 따랐고**(전용 갈래) 그 종류는 `range_type` 을
   보지 않는다. 표를 `Circle` 로 고쳐 두면 두 문서가 일치한다 — **표는 안 건드렸다.**
3. **바리올라 벽 타일 12종이 여전히 미배선** — `NeutralHabitat` 에 「벽」 칸이 없다(UI-41 4번).
4. 「방향 전환」·「스킬 종료 후 경직」 원화가 **받을 칸이 없어** 남아 있다(위 ③). 칸을 만들면
   `Unused_` 접두사만 떼면 된다.
5. 시그리드 `narrative`(플레이버 문장)가 여전히 비어 있다(UI-40 6번).
6. 증원(`reinforce*`)이 1~40 전부 0 인 것도 그대로다(UI-40 5번).

### 씬반영요청 목록

- 없음

---

## UI-44. ★★ 시그리드 방향·크기 · 베일 브레스/넉백/선명도 · 에픽 토벌 리스폰 (2026-08-20)

> 상세는 `진행상황.md` **123절**.

유저 지시 7건: ① 시그리드 일러스트 재확인 ② 전술지침 창의 «(추후 연동)» 문구 제거
③ 시그리드도 바라보는 방향과 이동 방향이 반대 ④ Pipe_smoke 를 «바라보는 방향의 연기
브레스» 로 재설계 + 반원형 이펙트 제거 ⑤ 베일·베일 스킬 이미지 선명하게
⑥ 담뱃대 강타의 넉백 미작동 ⑦ 시그리드가 인게임에서 커졌다 작아졌다
⑧ 에픽 몬스터를 한 번 잡고 리스폰되면 토벌 지시 UI 에 안 뜬다.

### ① 시그리드 일러스트 — **이미 고쳐져 있었다** + 크롭 파이프라인의 구멍 하나

UI-43 ① 의 임포트 수정으로 **`Resources.Load<Sprite>` 가 정상 동작한다.** 새로 만든
메뉴 `LastSanctuary/스킨/일러스트 로드 점검` 으로 **13개 전부 Sprite 로 읽히는 것을 확인**했다
(정의 에셋의 `illustName` 을 리플렉션으로 훑어 실제로 `Resources.Load` 해 본다 —
«임포트 설정이 Sprite 인가» 와 «실제로 읽히는가» 는 다른 질문이라 점검을 따로 뒀다).

★ 그런데 **볼트 파일이 프로젝트 크롭보다 새로웠다**(09:18 vs 09:15). 원인:
유저가 볼트에서 `Sigrid_illust.png` → **`illust_Sigrid.png`** 로 이름을 바꿨는데
(다른 넷과 같은 규칙으로 맞춘 것) `crop_illust_faces.py` 의 표는 옛 이름을 들고 있었다.
그 스크립트는 못 찾으면 **`MISSING` 한 줄만 찍고 넘어간다** — 즉 볼트에서 원화를 갈아끼우고
스크립트를 돌려도 **«완료» 만 찍히고 아무것도 안 바뀐다.**

고친 것 둘:
- `resolve_src` 신설 — `illust_<이름>` · `<이름>_illust` **두 규칙을 다 받는다**
  (오타 파일 `ilust_Preyja.png` 가 실재하므로 표에 적힌 이름이 먼저다).
- **못 찾으면 죽는다** — 조용히 건너뛰지 않는다. 이 구멍이 이번 혼란의 원인이었다.

재실행 결과 **6명 전원 결과가 바이트 단위로 동일**했다(볼트 파일은 내용이 같은 재저장이었다).

### ② «(추후 연동)» 제거 — MCP 로 씬 텍스트 두 곳

`캐릭터 일러스트\n(추후 연동)` → `캐릭터 일러스트`.
`UI_Root/HUD_Tactics/Info/Portrait/Hint` · `UI_Root/HUD_Growth/Info/Portrait/Hint`
(**MCP `update_component`**).

⚠ 이 오브젝트들은 **창이 닫혀 있어 비활성**이라 `objectPath` 로는 못 찾는다
(`GameObject.Find` 가 비활성을 건너뛴다). `get_gameobject` 에 **`maxDepth`** 를 주어
`instanceId` 를 얻어야 한다 — 하이라키 덤프는 기본 깊이에서 잘린다.

### ③ ★★ 시그리드 이동 방향 — **엘린과 원인이 다르다**

엘린은 «좌/우 전용 두 줄»의 y 좌표를 뒤바꿔 적은 것이었지만, 시그리드는 **이동이 한 줄이고
그것을 미러**한다. 그래서 «원본이 어느 쪽을 보는가» 하나만 틀렸다.

지표는 **머리−발 가로 차이(기울기)** 다 — 달릴 때는 머리가 앞선다:

| 대상 | 기울기 | 뜻 |
|---|---|---|
| 바리올라 이동 (오른쪽 확정) | **+27.4px** | 양수 = 오른쪽 |
| 시그리드 근거리 (오른쪽 확정 · 지팡이가 오른쪽으로 뻗는다) | **+8.8px** | 오른쪽 |
| 시그리드 이동 (당시 `Right`) | **−3.2px** | ★ **부호가 반대** |

`ORIGINAL_SIDE = {"Walk": "Left"}` 를 신설해 **줄마다 원본 방향을 지정**할 수 있게 했다.
수정 뒤 재측정 **+3.4px**(부호가 맞았다).

### ④ ★★ 시그리드 «커졌다 작아졌다» — 줄마다 그린 크기가 다르다

말파스와 **같은 버그**다(113절 ②): `contentSizeTiles` 는 **대기 원화 하나만** 재고 그 배율이
모든 모션에 곱해지는데, 원화가 줄마다 다른 크기로 그려져 있었다. 실측(머리 면적 기준):

| 줄 | 필요 배율 | 비고 |
|---|---|---|
| 대기 | 1.000 | 기준 |
| 이동 | 1.045 | |
| 근거리 | **1.154** | |
| 원거리 | **1.391** | ★ 이 줄만 다른 판본(`_01`)이고 그 시트가 전체적으로 작다 |

★★ **말파스식 「세로 중앙값」을 그대로 쓸 수 없었다.** 시그리드는 ① **지팡이**가 경계
상자를 늘리고(대기는 곧게 세워 상자가 크고, 이동은 비스듬해 작다) ② **이동은 몸을 기울여
키가 줄어드는 것이 연출**이다. 키로 재면 이동에 **1.154배**가 필요하다고 나오는데 —
그대로 늘리면 **걸을 때 10% 커진다**(고치려던 증상이 반대로 생긴다).

그래서 `skin_sheet.head_pixels` 신설 — **얇은 줄기(지팡이)를 버린 뒤 상단 45% 의
밝고 저채도인 덩어리(은발+두건)** 면적을 잰다. 머리는 **기울여도 크기가 안 바뀌는** 부위다.
줄 내부 변동계수 4~8% 로 안정적이고, 1:1 로 겹쳐 본 것과도 일치한다.

★ 리샘플은 `skin_sheet.resample_rgba` 신설 — **알파를 곱해 두고 늘렸다가 다시 나눈다**
(premultiply). 그냥 RGBA 를 늘리면 투명 픽셀의 **흰 배경색이 경계로 번져** 캐릭터 둘레에
흰 테가 생긴다. ⚠ 말파스는 «RGB 를 먼저 늘리고 알파를 다시 계산» 으로 피했지만, 그 방법은
`background_mask` 가 살려낸 **흰 두건**을 잃는다.

검증: 머리 면적이 **1022 / 1022 / 1028 / 1031** 로 1% 안에 들어왔다(전 1022/937/768/528).
⚠ 대기가 기준이라 `contentSizeTiles` 는 **안 바뀐다** — 캐릭터 사이 크기는 그대로다.

### ⑤ ★★ 담뱃대 강타의 넉백 — **코드는 있었고 스킬이 아무도 못 맞히고 있었다**

```
베일 콜라이더 15 x 10  →  BodyRadiusTiles = min(15,10)/2 = 5.0타일
근접 캐릭터는 그 몸 표면에 붙어 선다        → 베일 중심에서 5타일 밖
그런데 표의 반지름은 3                     → 원이 베일 몸 속에서 끝난다
```
즉 «반지름 3» 을 **중심에서** 재면 이 스킬은 **원리적으로 발동할 수 없다.** 넉백 코드가
한 번도 실행되지 않았던 것이고, 그래서 «구현이 안 되어있다» 로 보였다.

★ **표가 스스로 「보스 + N」이라고 적어놨다** — 다른 보스 스킬 정의문이 전부 그 형식이다:
*"카르시노스 **+** {value_01} 반지름"* · *"아니사킬 **+** …"* · *"라린길이 **+** …"* ·
*"바리올라 **+** {value_01} 지름"*. 몸집에 **더하는** 값이라는 뜻이다.
`SelfBodyRadiusTiles()` 신설 — 베일의 두 스킬 반지름에 자기 몸 반지름을 더한다
(강타 3 → **8** · 연기 5 → **10**).

⚠⚠ **다른 넷(할퀴기·죽음의 포효·거대한 위협 포효·아우성)에는 적용하지 않았다** —
이미 나가 있는 밸런스라 소급하면 범위가 커진다(카르시노스 포효 5 → 6.15, **+23%**).
그건 밸런스 변경이라 유저가 정할 일이다. **발견 사실만 남긴다.**

### ⑥ Pipe_smoke — «바닥에 깔던 반원» 을 «앞으로 뿜는 브레스» 로

유저 지시: *"기획 의도가 베일이 바라보는 방향으로 연기 브레스를 쏘는건데 지금 에셋에 있는
담배연기 패턴 반원형 이펙트를 빼고 만들어줘"*.

- 원화의 «반원형 범위 이펙트» 한 장을 스킬 칸에서 **뺐다**(`Skill2Fx` → `Unused_Skill2Fx`).
  그건 **범위를 알려주는 그림**이지 «입에서 뿜는 연기» 가 아니다 — 연기가 어디서 나오는지
  안 보였다. ⚠ 원화는 **지우지 않았다**; 범위 표시가 다시 필요하면 접두사만 떼면 된다.
- `BossSkillCaster.PlayBreath` 신설 — **연기 구체 원화**(투사체 칸)를 **앞쪽 절반**에 깔고
  조준 각도로 돌린다(중심을 앞으로 반지름의 절반 밀어 «내 앞» 만 덮게 한다).
  「보이는 범위 = 맞는 범위」 규칙은 유지된다 — 판정도 같은 반지름의 **앞쪽 180도**다.

⚠⚠ **부채꼴 각도는 여전히 180도**다(표의 `Semi_Circle`). 「브레스」라면 더 좁아야 자연스럽지만
**표에 각도 칸이 없어** 코드에 숫자를 지어내지 않았다(118-3절의 규칙). 원하는 각도를
알려주시면 표에 컬럼을 만들어 배선한다.

★ 옛 폴더 정리 — 이름만 바꾸면 유니티 빌더가 **옛 폴더를 여전히 스킬 칸으로 읽어**
빼려던 원화가 계속 배선된다(실제로 `Skill2Fx(1)` 이 남았다). `drop_stale_folders` 로 지운다.

### ⑦ 베일 선명도 — 원화가 무른 것이 원인

측정으로 원인을 갈랐다:
- `filterMode` 는 **이미 Point** 였다(번짐 아님).
- **타일당 원화 픽셀**: 일반 중립 70~96px vs **에픽 보스 13~23px** — 5~6배 부족.
- ★★ **베일은 표에서 콜라이더가 혼자 크다**: 나머지 보스 전부 `11 x 7.5` 인데
  베일만 **`15 x 10`**. 원화는 85px 로 같은데 확대율이 **x7.4** 까지 올라간다.
- 게다가 베일 원화는 **부드러운 에어브러시 음영**이다(라린길·바리올라는 먹선이 또렷하다).
  나란히 렌더해 보면 확연히 흐리다.

확대로 잃은 **해상도는 되돌릴 수 없으므로**(볼트에 더 큰 원화가 없다 — `_01` 85px ·
`_02` 80px 이 전부) **국부 대비**를 올렸다: `skin_sheet.sharpen_rgba` 신설(언샵 마스크,
**RGB 만** · 알파는 그대로 · premultiply).

값은 **네 단계를 눈으로 비교**해 골랐다 — 대비가 오르는 만큼 털에 **흰 잡티**도 늘어난다:

| 설정 | 인접 대비 | 흰 점(원본 34) |
|---|---|---|
| 원본 | 20.8 | 34 |
| **0.40 / r1.2 / th6** | **30.3 (+46%)** | **176** ← 채택 |
| 0.55 / r1.2 / th5 | 32.0 | 229 |
| 0.90 / r1.0 / th2 | 34.7 | 282 ← 털에 흰 점이 눈에 띈다 |

⚠ `threshold` 를 6 으로 올린 것이 핵심이다 — 평탄한 면(검은 옷)은 안 건드리고 **경계만** 조인다.
스킬 프레임(Skill1 13장 · Skill2 12장 · 투사체 14장)도 같이 적용했다.

⚠⚠ **콜라이더 15x10 은 안 건드렸다** — 표 값이고, `11x7.5` 로 맞추면 다른 보스와 같은
크기가 되어 **그 자체로 36% 선명해진다.** 다만 보스가 작아지는 **디자인 변경**이라 유저 확인이 필요하다.

### ⑧ ★★ 에픽 토벌 — 발견을 «개체» 가 아니라 «종» 으로 기억한다

유저 리포트: *"한 번 잡고 재생성 됐을 때 전장의 안개는 밝혀진 상황이지만 … 시야가 없으면
토벌 지시에 에픽 몬스터 UI가 뜨지않아"*.

원인은 `_discovered` 가 **유닛 인스턴스**를 들고 있었다는 것:
```
① 에픽을 잡는다              → PruneDead 가 목록에서 지운다
② 재생성(respawnSeconds 600)  → 완전히 새 GameObject 다
③ 그 자리에 캐릭터가 없다      → 안개 판정 false → 다시는 목록에 안 올라온다
```
안개는 이미 걷혀 있는데도 «시야» 가 없어서 영구히 못 찾는 상태였다.

`_knownSpecies`(종 번호 `mon_id`) 신설 — **개체가 죽어도 지우지 않는다.** 아는 종은
재생성되면 **보자마자가 아니라 태어나자마자** 목록에 오른다. 서식지가 고정이고(99-9절)
그 자리를 이미 아는 것이 이 게임의 전제이므로, 같은 종이 같은 자리에 다시 나오는 것을
플레이어가 «모른다» 고 볼 이유가 없다.

⚠ **세이브에 남긴다**(`SaveData.subjugationKnownSpecies`) — 안 남기면 불러오기 한 번에
«처음 보는 종» 으로 되돌아가 같은 버그가 재현된다(개체 번호만 저장하던 것이 정확히 그랬다).
★ **옛 세이브도 그대로 열린다** — 그 칸이 비어 있으면 개체 목록에서 종을 역산한다
(`RestoreState` 가 `_knownSpecies` 를 **비우지 않고 보태기만** 한다).
⚠ 재발견 로그·HUD 알림은 **처음 보는 종만** 낸다 — 안 그러면 600초마다 같은 줄이 쌓인다.

### 씬 변경 여부

**있음** — 초상화 힌트 문구 2곳(**MCP**) · `save_scene` 1회.

### 검증

`recompile_scripts` 오류 0 · 경고 0 (5회) · **플레이 모드 런타임 오류 0 · 경고 0** ·
`일러스트 로드 점검` → **13개 전부 Sprite** ·
시그리드 머리 면적 4줄 **1% 안** · 이동 기울기 부호 정상(+3.4px) ·
베일 인접 대비 20.8 → 30.3 · `Skin_Bale` 에서 `skill2Fx` 칸이 **비었음** 확인 ·
크롭 재실행 결과 6명 전원 **바이트 동일** ·
씬에서 «추후 연동» **0건**(문구는 「캐릭터 일러스트」로 남음) ·
`git status` 로 바뀐 프레임이 **베일 135장 · 시그리드 46장**(=이동 16 + 근거리 16 + 원거리 14)
뿐이고 다른 캐릭터 무영향인 것 확인.

### 아직 확인 못 한 것 (유저가 볼 것)

1. ⚠⚠ **25웨이브 실전투** — 담뱃대 강타의 넉백은 «반지름이 몸 밖까지 닿게» 고쳤지만
   **실제로 밀려나는 것은 못 봤다**(플레이 모드가 시작 단계까지만 돌았다). 확인 포인트:
   ① 강타가 실제로 발동하는지 ② 도망치던 캐릭터가 **보스 쪽으로** 끌려오는지(정의문대로다)
   ③ 연기 브레스가 **앞쪽에만** 깔리는지 ④ 중독 초당 최대체력 2%.
2. ⚠ **부채꼴 각도** — 지금 180도다. «브레스» 답게 좁히려면 **표에 각도 칸**이 필요하다.
   원하는 각도를 알려주시면 컬럼을 만들어 배선한다.
3. ⚠ **베일 콜라이더 15x10** — 다른 보스는 전부 11x7.5 다. `11x7.5` 로 맞추면 확대율이
   x7.4 → x5.4 로 떨어져 **선명도가 36% 개선**된다(원화를 안 고치고). 보스가 작아지는
   디자인 변경이라 **결정을 안 했다.**
4. ⚠ **다른 보스 스킬 넷의 «보스 + N»** — 표는 몸집에 더하라고 적혀 있는데 코드는 중심에서
   잰다(⑤). 소급하면 범위가 최대 +23% 커진다 — 밸런스 판단이 필요하다.
5. **에픽 토벌** — 리스폰 후 목록에 뜨는 것은 코드로만 확인했다. 실제로 잡고 600초 뒤에
   토벌 지시 창을 열어봐 주세요.
6. UI-43 의 미완(바리올라 벽 타일 12종 미배선 · 시그리드 `narrative` 빈칸 · 증원 전부 0)은 그대로다.

### 씬반영요청 목록

- 없음

---

## UI-45. ★★ 모션별 크기 정규화 · 잘린 프레임/이펙트 복구 · 이벤트 테이블(임시 UI) · 골렘 3건 (2026-08-21)

유저 지시가 세션 중에 계속 늘어난 회차다. 처리 순서대로 적는다.

### ① ★★ 「아루 스킨만 작아진다」 — 대기 줄만 크게 그려져 있었다

유저 리포트: *"아루 스킨만 크기가 작아지는 상황 이미지 스프라이트 하나하나 비교 분석 해서 해결"*.

말파스(113절 ②)·시그리드(123절 ④)와 **같은 버그**다. 몸집 배율은
`CharacterSkinSO.contentSizeTiles` = **대기 원화 하나**로 정해지고(`measure_skin_tiles.py`),
캐릭터는 `renderHeightTiles` 2.15 로 그려진다. 그런데 아루 시트는 대기만 크다:

| 줄 | 상자 높이 | 대기 대비 | 인게임 크기 |
|---|---:|---:|---|
| 대기   | **122 px** | 기준 | 2.15 타일 |
| 이동   | 82 px  | −33% | **1.45 타일** ← 걷는 순간 32% 줄어든다 |
| 근거리 | 90 px  | −26% | |
| 원거리 | 84 px  | −31% | |
| 회복   | 82 px  | −33% | |

⚠ `char_sheet.report_body_size` 가 **이미 경고를 찍고 있었다** — 「대기와 −34%」. 그것을
«자세일 수 있다» 로 넘긴 것이 원인이다. 스크립트 맨 위 주석도 «시트가 모든 줄을 같은 크기로
그렸다» 고 단정하고 있었는데, **상자를 안 재고 내린 추측**이었다.

### ② ★★ 크기 정규화 기준 `"min"` 신설 — 두 기준의 오차가 서로를 막는다

유저 지시: *"어떤 캐릭터든 간에 이동할때 작아지는 문제 수정해"*. 그래서 아르세니아·카이론·
불칸까지 켰는데, 기존 기준 둘이 **각각 한쪽으로만 틀린다**는 것이 드러났다:

* `height`(상자 높이)는 **웅크린 자세를 크기 오류로 읽는다** — 달리는 원화는 몸을 기울여
  키가 20~30% 줄어드는 것이 *연출*인데 그것을 부풀린다(아루 이동 x1.485 vs 머리 x1.279).
* `head`(머리 면적)는 **밝고 저채도인 다른 부위**가 판정 창에 들어오면 부풀린다 —
  아르세니아는 대기에서 **흰 날개가 접혀** 상단 45% 에 들어오는데 마법 줄은 펼쳐 나간다.
  그래서 기준(대기)만 커져 배율이 뛴다(마법 머리 x1.371 vs 상자 x1.188).

→ `scale_metric="min"` : **상자 높이는 «키우는 것» 만 막는다.**

```
f = min(머리, 상자)   상자 배율이 1 이상일 때
f = 머리              그렇지 않을 때
```

⚠⚠ 뒷줄이 중요하다. 카이론 「스킬 1」은 몸이 **황금 구체 안**에 들어가 상자가 대기보다 크고,
그래서 상자 배율이 **x0.930**(«몸을 7% 줄여라») 이 된다. 머리로 재면 **x1.144**(키워야 한다)다.
단순 최솟값을 쓰면 **고치려던 것과 반대로** 스킬 중에 몸이 작아진다.

★ 정규화 뒤 머리 배율이 네 캐릭터 전부 **1.00 ±0.01** 로 수렴했다(검증).
⚠ 기준은 **대기**다 — 그래서 `contentSizeTiles` 가 안 바뀌고 **캐릭터 사이 크기(2.15타일)도
  그대로**다. 바뀐 것은 «모션이 바뀔 때 커졌다 작아졌다» 하는 것뿐이다.
⚠ 변신·강림처럼 원화가 일부러 큰 줄은 `Row.scale=False` 로 뺀다
  (아르세니아 「천사 강림」 x0.737 · 아루 「강림」 좌우가 12% 어긋났다).
⚠ **보스·중립은 안 건드렸다** — 몬스터 스킨 14개 전부 `measure_skin_tiles` 에서 «변경 없음».

### ③ ★★ 아르세니아 이동 9장 중 3장이 「날개 조각」이었다

유저 리포트: *"아르세니아 스킨 망가진거 … 이동 시마다 모션이 다 잘려서 나옴"*.

원인이 셋이고 **셋 다 개수 검사(`expect`)를 통과하는** 종류다:

1. **이동 줄의 칸 가르기가 `feet`** 였다. 망토 자락이 바닥을 따라 이어져 발밑 띠에서 조각이
   붙고, `merge_to_count` 가 장수를 맞추려고 엉뚱한 곳을 합쳤다 — 실측 왼쪽 줄
   `(546,687)` 한 칸에 **몸통 둘** + 다음 칸 `(688,730)` 은 **43px 날개 조각**.
   오른쪽 줄은 `(334,541)` 이 **208px**(몸통 셋)이었다. → 손으로 잰 `bounds`.
2. **「스킬 3」은 6장인데 `expect` 가 7** 이었다(문서 표에는 6 이라고 적혀 있었다).
   `clusters` 가 **마침 7** 을 냈고(3번 프레임의 후광이 끊겨 두 덩어리) 검사를 통과했다.
3. **스킬 2 의 3번 프레임 모자 꼭대기**(붉은 구슬 장식 y588~607)가 밴드 `y608` 에 잘려
   **평평하게** 나왔다. 유저가 *"중간에 머리 잘린 거 하나"* 로 잡아낸 그 장이다.

★ 이 일 때문에 도구를 **셋** 만들었다:

| 도구 | 무엇을 |
|---|---|
| `Tools/sheet_split.py` | «빈 열 + 국소 최소» 로 **경계 자체**를 찍는다. `sheet_rows.py` 는 «개수가 맞는가» 만 보므로 2번 같은 오류를 구조적으로 못 잡는다 |
| `Tools/sheet_clip.py` | **칸마다** 위·아래로 그림이 이어지는지 본다. `warn_if_clipped` 은 줄 단위라 20줄 중 19줄에 경고가 떠서 진짜 잘림이 묻힌다 |
| `Tools/sheet_fxband.py` | 이펙트 밴드의 위·아래끝을 «딱지 판과 이웃 줄 사이» 에서 실측한다 |

### ④ ★★ 이펙트 밴드 — 아르세니아 11줄 **전부** 위가 잘려 있었다

유저 리포트: *"이펙트가 안 잘리게 해 윗부분 살짝 잘렸자나"*. 심한 것은 **33줄**
(「투척 폭발」 472 → 439). 원인은 «둥근 판 테두리를 피하려고 밴드를 안쪽으로 넉넉히 넣은» 것 —
테두리는 이미 `erase` 로 지우므로 물러설 필요가 없었다.

⚠⚠ 그런데 «잉크가 이어지는 끝» 까지 그냥 늘리면 **제목 딱지 글자가 프레임에 박힌다**
(네 줄에서 실제로 그랬다). 딱지는 **가로로 200~250px 이어지는 한 덩어리**이고 이펙트는
둥근 덩어리 40~120px 이라 **«줄마다 최장 연속 런»** 으로 명확히 갈린다:

```
폭발  딱지 y216~237 → 밴드 238   (원래 값이 맞았다)
회복  딱지 y323~345 → 밴드 346   (348 이었다 · 2줄 잘림)
투척  딱지 y439~461 → 밴드 462   (472 이었다 · 10줄 잘림)
천사  딱지 y635~654 → 밴드 655   (665 이었다 · 10줄 잘림)
```

⚠ 옛 「하늘 레이저」 밴드는 아래끝이 **845** 여서 **다음 줄(폭발/섬멸)의 윗부분 2줄**을
물고 있었다 — 위아래를 동시에 재야 이런 것이 보인다.

### ⑤ ★★ 카이론 — 폭발이 한 장에 두 개 · 실드 6장

유저 리포트: *"카이론이랑 아르세니아 스킨 다시 분석해봐 이펙트 짤린듯"* ·
*"쉴드 이미지 또 잘렸네 이펙트 확실히 구분해서 잘라"*.

* **스킬 2 이펙트**: 이 구획은 「좌」·「우」 **두 줄**로 같은 암석 폭발을 두 번 그려 놓았는데
  밴드가 `654~875` 로 **두 줄을 통째로** 덮고 있었다 → 구운 그림이 «폭발이 위아래로 쌓인
  한 장» 이었다. 위 줄만 쓴다(지면 연출이라 방향이 없다). 경계는 두 줄 사이에서 가장 옅은
  **y775**(잉크 31px · 위아래는 100px 이상).
  칸 경계도 18px 앞이었고 오른쪽 끝이 「좌」 딱지를 물었다 → `[670, 773, 869]`.
* **실드 구체 6장**: ① 위·아래 줄 경계가 `960/961`(1px 겹침)이었는데 실측 가장 옅은 줄은
  **y958** 이고 위 줄 구체는 y963 까지 내려온다 → 위는 아래가 잘리고 아래는 위 줄 밑동을
  물었다. ② 오른쪽 끝 `790` 이 **옆 구획 조각**(x770~830)을 물어 3·6번에 흰 조각이 박혔다.
  ③ **위·아래 줄이 같은 격자가 아니었다**(4~14px 어긋난다 — 옛 주석은 같다고 적어 두었다).

★★ ①은 «어디를 잘라도 잘린다» — 한 열의 잉크가 `y892~1024` 로 **끊기지 않는다**(구체 두 개가
맞닿아 있다). 허리에서 갈라도 옅은 후광이 변에 **28px** 남아 직선으로 보였다.
→ 자리를 더 찾는 대신 **그 자리를 흐린다**: `skin_sheet.feather_edges` 신설
(알파만 · 지정한 변에서 서서히 0 으로). 검증: 변에 남은 알파 28 → **0**.

### ⑥ 불칸 · 아루 이펙트

* 불칸 **총구 섬광**: `clusters` 가 개수(5)는 맞췄지만 4번 섬광의 오른쪽을 잘라 반토막이
  났다 → 손으로 잰 `bounds`.
* 아루: 스킬1Fx +5줄 · 스킬2Fx +2줄 · 회복 별지 +17줄.
* ⚠ **옛 7명(엘린·시카리아·시그리드·피올로·히스톤·프레이야·비기오르)은 안 봤다** — 유저 확정
  («일단 냅둬도 되고»). 그들은 `char_sheet` 를 안 쓰는 개별 스크립트라 새 도구가 그대로 안 붙는다.

### ⑦ ★★ 베일 「담배 연기」 — 평타 탄환이 깔리고 있었다

유저 리포트: *"보스 베일 이펙트 이상한거 있었으니까"*.

UI-44 에서 «반원형 범위 표시» 를 스킬 칸에서 빼고 `PlayBreath` 가 **투사체 원화**(연기 구체
14장)를 앞쪽에 깔게 했다. 그 원화는 «날아가다 흩어지는 **둥근 구체**» 라 앞쪽 반원에 깔면
**공깃돌이 굴러다니는** 것처럼 보인다 — 「입에서 뿜는 연기」가 아니다.

★ 정작 **진짜 연기 브레스 원화가 이 시트에 있었다** — 스킬 2 줄 가운데의 «입에서 앞으로 길게
뿜어지는 연기». 그런데 그것은 **프레임 다섯 장이 아니라 한 장의 이어진 그림**이다
(실측: 잉크 `x435~916` 한 덩어리 481px). 덩어리 판정이 그 긴 구름을 다섯 조각으로 잘라
`Unused_Skill2Smoke` 에 넣어 두었던 것이다.
→ `SINGLE_FX` 에서 **한 장으로** 잘라 `Skill2Fx` 로 굽고, `PlayBreath` 의 원화 선택 순서를
`skill2Fx` → `skill2Projectile` → `projectileFrames` 로 바꿨다(옛 순서는 폴백으로 남겼다).

### ⑧ ★★ 아루의 골렘 — 정의문 네 줄이 지켜지지 않고 있었다

유저 리포트: *"아루의 골렘이 강화 가능하고 전술 수정이 가능한 버그 수정 아루가 사망하면
골렘도 사망함. 골렘 크기 테이블 값대로 적용 안됐음 테이블 스킬 타입 스트링 키 다시 읽어보고
맞는 방향으로 수정해줘"*.

`skill_type_desc_Dawn` 을 다시 읽으면 네 문장이 나온다 —
*"골렘은 강화할 수 없습니다"* · *"전술을 수정할 수도 없습니다"* ·
*"골렘의 크기는 타일 기준 {value_02}(가로) * {value_03}(세로) 입니다"* ·
*"아루가 사망할 경우 골렘도 함께 사망합니다"*.

★★ 넷 다 «코드는 있는데 안 닿는» 상태였다:

| 항목 | 코드에 있던 것 | 왜 안 먹었나 |
|---|---|---|
| 강화 | `CharacterUpgradeService.CanUpgrade` 가 소환수를 **거부** | 성장 창의 «성장 유형 고르기» 단계(`pickingStage`)가 **그 검사를 안 거쳐** 버튼이 눌렸고, 안내문도 «에너지가 부족합니다» 로 나왔다 |
| 전술 | `CharacterTactics` 가 잠긴 유닛의 값 변경을 **전부 거부**(주석 11곳) | 전술 창은 **역할 두 줄만** 잠금을 반영했다(`roleFree`) — 나머지 줄은 버튼이 멀쩡히 눌렸다 |
| 주인 사망 | `AruGolem.Dismiss` 가 **실제 피해로 죽인다** | 부르는 곳이 `OnDisable` **하나뿐**이었다. 캐릭터는 죽어도 오브젝트가 남아 `OnDisable` 이 안 오고, `PassiveSkillService` 는 죽은 캐릭터의 `Tick` 을 **건너뛴다** → 골렘만 남아 계속 싸웠다 |
| 크기 | `OrientBoxToArt` 가 «원화가 서 있으면 상자를 세운다» | 정의문이 **어느 쪽이 가로인지 못박고 있다** — 상자를 돌리는 것은 «표를 코드가 뒤집는» 일이었다 |

고친 것: `pickingStage` 에 `!summoned` 추가 + 안내문 「소환수는 강화할 수 없습니다」 신설 ·
전술 창 `RefreshAll` 에 `locked` 게이트 한 줄(옵션마다 적으면 새 줄이 늘 때 빠뜨린다) ·
`CharacterPassives.OnOwnerDied()` 신설 + `PassiveSkillService.HandleDied` 에서 호출 ·
`SetColliderBoxTiles(so.value02, so.value03)` 로 되돌림.

⚠ 골렘 일러스트(`illust_AruDawn`)는 **이미 배선되어 있었다** — `AruGolem.IllustName` 상수 ·
  `Resources/Illust/illust_AruDawn.png`(420x568 · textureType 8 = Sprite). 확인만 했다.
⚠ 크기를 표대로 되돌리면 골렘이 **아루보다 작아진다**(선 그림을 눕힌 3x2 상자에 contain 하므로
  1.41 x 2.00 타일 vs 아루 2.07 x 2.15). 그것이 문제라면 **표의 3x2 를 기획이 고치는** 것이
  맞는 방향이다 — 코드가 뒤집지 않는다.

### ⑨ ★★ 이벤트 테이블 적용 — 임시 UI + 발동 확률 인스펙터

유저 지시: *"이벤트 테이블 적용 일단 임시 ui 로 구현 / 빈 텍스트는 임시 텍스트로 채우기
(폰트 네오둥근모 사용)"* · *"그 이벤트 발동확률 에딧에서 조정할 수 있게 만들어줘 자연적
발생확률 그리고 하이라키 어디에 해당 기능 넣었는지 말해줘"*.

원본: `<볼트>/데이터 테이블/Last_Sanctuary_이벤트테이블_Ver012_파송송계란탁.xlsx`
(시트 7개 · 이벤트 42행 · 대사 168행 · 보상 타입 43종)

**만든 것**

| 파일 | 역할 |
|---|---|
| `Tools/gen_event_assets.py` | 표 → `EventDefinitionSO` **42개**. 대사는 **이벤트 에셋 안에** 넣는다 |
| `Scripts/Events/EventDefinitionSO.cs` | 이벤트 한 행 + `EventLine`(대사 한 행) |
| `Scripts/Events/EventService.cs` | 언제 무엇이 뜨는가. **인스펙터에 확률** |
| `Scripts/Events/EventRewardService.cs` | 보상 적용 + 웨이브 끝에 되돌리기 |
| `Scripts/UI/EventPanel.cs` | 임시 UI (제목 · 대사 · 선택지 2 · 닫기) |

**하이라키 (MCP 로 직접 생성)**
```
GameSystems
└─ EventService  ★★ ← 자연 발생 확률이 여기 있다
     자연 발생 확률 (%)   웨이브 80 · 비공개 80          ← 표 EventType.event_cond_value_01
     비공개 타이머        주기 180초 · 첫 발동 60초       ← 표 event_value_01
     스위치              이벤트 사용 / 항상 발생(테스트) / 주사위 로그
     (인스펙터 우클릭 → 「웨이브 이벤트 하나 발생시키기」)

UI_Root/HUD_Event   (EventPanel · 760x420 · 평소 비활성)
├─ Title            이벤트 이름
├─ Body             지금 줄의 대사
├─ Choice0 > Label  선택지 1 (수락)
├─ Choice1 > Label  선택지 2 (거절)
└─ CloseButton > Label
```

★ 확률을 **에셋이 아니라 컴포넌트**에 둔 이유 — 밸런싱 중에 가장 자주 만지는 값이고 표에는
타입별로 하나씩(둘)뿐이다. 42개 에셋에 흩어 두면 «한 번에 바꾸기» 가 안 되고, 표를 다시
생성하면 손으로 고친 값이 지워진다.
⚠ 그래서 **표의 80 과 인스펙터 기본값이 같아야 한다.**

**표를 어떻게 읽었나**

* 「두 단계」다 — ① 확률(80%)로 한 번 굴리고 ② 통과하면 `event_value_02`(가중치 10/5)로 뽑는다.
* 발생 시점은 **전투 단계 진입**이다. 표의 `wave_start` 는 «웨이브 타이머 시작 시» 이고,
  이 게임의 웨이브 타이머는 **첫 전투가 벌어질 때** 흐른다(진군 중에는 멈춰 있다).
* 대사 흐름: `active` 로 시작 → `choice_proceed` 줄에서 선택지 둘(`next_dialogue_id_01/02`,
  각자 `reward_value_01/02` · `03/04`) → 결과 줄에서 `end_switch`.
  `random_proceed` 는 유저 입력을 안 보고 `reward_proceed_value_01` % 로 갈린다.
* 종료 스위치 **500002**(재수락 불가)면 그 판에서 다시 뽑지 않는다. **500005** 는 재수락 가능.
* 시트 다섯 벌(`EventType`·`Switch`·`Condition`·`RewardType`·`Info`)은 **에셋으로 안 옮겼다** —
  enum 의 «뜻» 을 적은 사전이고 판정은 코드가 한다.

**보상** — 표는 전부 «{value_01}% 만큼 …» 인데 이 프로젝트의 통로는
`AddStatPercentBonus`(모든 능력치 한꺼번에) / `AddFlatStatBonus`(한 능력치 고정치) 둘뿐이라
어느 쪽도 그대로 맞지 않는다. 그래서 **걸 때 한 번 환산**한다:
`델타 = round(지금 능력치 × 퍼센트 ÷ 100)` — 그리고 **걸어둔 델타를 기억**했다가 웨이브가
끝날 때 정확히 같은 값을 뺀다(「걸었으면 반드시 되돌린다」 규칙).
⚠ 능력치 20종 + 에너지 + 성역 회복/손상까지 배선했다. **나머지는 조용히 넘어가지 않고
  경고를 남긴다** — 지어내서 «비슷한 것» 을 걸면 기획이 표를 고쳐도 알 수 없게 된다.

### 씬 변경 여부

**있음** — `UI_Root/HUD_Event`(신규 8개 오브젝트) · `GameSystems > EventService` 추가 ·
초상화 빈칸 문구 2곳(「캐릭터 일러스트」 → 「캐릭터 선택」). 전부 **MCP** · `save_scene` 2회.

### 검증

`recompile_scripts` 오류 0 · 경고 0 · **플레이 모드 실제 발동 확인**:
`[이벤트] 비공개 주사위 7 < 80% → 발생` → 「대서사시」 진행 → `[이벤트] 웨이브 — 이미
진행 중이라 건너뜀` → `[이벤트] 대서사시 종료 — 웨이브 종료` (런타임 오류·경고 0) ·
씬의 TMP 213개 **전부** NeoDunggeunmo SDF 참조 확인 ·
정규화 뒤 네 캐릭터 머리 배율 1.00 ±0.01 · 카이론 실드 변에 남은 알파 28 → 0 ·
아르세니아 「윗변 연속 36px」(잘린 모자) → 0 ·
`git diff` 에 몬스터 스킨·보스 아트 **0건**.

### 아직 확인 못 한 것 (유저가 볼 것)

1. ⚠⚠ **카이론 「타락한 육체」 쿨타임 — 재현하지 못했다.** 표 60초 · 코드도
   `_fallenBodyReadyAt = Time.time + 60` 을 **보상 지급 전에** 세우고 있고, 씬에
   `CharacterPassives` 는 **하나뿐**이며(중복 컴포넌트 아님) 스킬 에셋에 빈 줄도 없다
   (`coolTime: 60` 정상 파싱). 관찰한 두 판에 카이론이 뽑히지 않아 로그를 못 봤다.
   → **확인 방법**: 전투 로그에 「카이론 · 타락한 육체 · 보호막 N · 10초」 가 **60초에 한 번**
     찍히는지 보면 된다. 여러 번 연속으로 찍히면 쿨타임 문제, 한 번만 찍히는데 구체가 계속
     보이면 표시 문제다 — 어느 쪽인지 알려주시면 그 갈래만 파겠다.
   ⚠ 참고: 보호막은 «양» 이다(최대체력 20%). 한 대 맞으면 사라지므로 **원래도 오래 안 보인다**.
   ★ 실드 **원화**는 이번에 고쳤다(위 ⑤) — 옛 구운 그림이 남아 있었다면 그것이 원인일 수 있다.
2. **이벤트 UI 모양** — 목업에 이벤트 패널이 없어 «글자와 버튼만» 있는 창이다. 배경 이미지
   (`event_bg = bg_test`)·연출·타이핑 효과는 배선하지 않았다.
3. **토벌 이벤트(ev_raid 207001 「드래곤 슬레이어」)** — 조건이 «{몬스터 id}와 인접한 위치에
   도달할 시» 인데 그 판정을 아직 안 붙였다. 지금은 뽑히지 않는다.
4. ⚠ **비공개 이벤트가 웨이브 이벤트를 자주 가로막는다** — 둘 다 80% 인데 «한 번에 하나» 규칙이
   있고 비공개 첫 발동이 60초라, 첫 전투 때는 대개 비공개가 먼저 떠 있다(실제 로그에서 그랬다).
   확률·주기를 인스펙터에서 조정해 보고 원하는 감이 나오면 알려주세요.
5. **보상 미구현 목록** — 정신 이상(`char_mental_error_*`)·캐릭터 합류/사망(`char_join`·
   `char_die`)·`char_upgrade`·`summon_enemy`·`summon_m_boss`·포탑 회복/손상. 발동하면
   콘솔에 경고가 뜬다.
6. **골렘 크기** — 표대로(3 가로 x 2 세로) 되돌렸으므로 **아루보다 작다**(위 ⑧ 마지막 ⚠).

### 씬반영요청 목록

- 없음

---

## UI-46. ★★ 잃어버린 122절 작업본 복구 · 카이론/골렘 4건 · 로스터 표기·순서 · 중복 생성 금지 · 중립 체력배율/사냥성장 · 미니맵 시야상자 · 이벤트 창이 안 열리던 구조 버그 (2026-08-21)

유저 지시가 세션 중에 계속 늘어난 회차다. 처리 순서대로 적는다.

### ⓿ ★★★ 먼저 — **122절 작업본이 `git stash` 에 갇혀 있었다**

작업 도중 진행상황 122절을 읽다가 <b>그 절에 적힌 코드가 이 브랜치에 하나도 없다</b>는 것을
발견했다(실드 원화 6장 그대로 · 「도움의 손길」 쿨타임 에셋 0 · 골렘 크기 contain).

    stash@{0}  2026-08-21 10:33:17  "On UI: !!GitHub_Desktop<UI>"   ← 43개 파일
    진행상황.md 갱신                 2026-08-21 10:34:05
    Unity 리포지토리 마지막 커밋      2026-08-21 06:51 (UI-45)

즉 <b>GitHub Desktop 이 pull 직전에 만든 스태시</b>였다 — 볼트에서 2026-08-20 에 겪은 것과
<b>같은 사고</b>다(진행상황 «볼트 작업본이 사라졌을 때» 참조). 유저 확인을 받고 복원했다.

⚠⚠ <b>단순 복원이 아니었다.</b> 스태시의 <b>기준 커밋이 `4790de3`(어제 20:19)</b> 이고 HEAD 는
그 뒤로 552개 파일이 바뀐 상태였다(UI-44·UI-45 가 들어왔다). 그래서 두 세션이 <b>같은 문제를
서로 다르게 고친</b> 파일들이 충돌했다. 판단은 이렇게 갈랐다:

| 충돌 | 무엇과 무엇 | 어느 쪽을 택했나 |
|---|---|---|
| `Tools/chiron_skin_build.py` | 스태시 «실드 한 장만 굽기» ↔ HEAD «줄 경계 958/959 재실측 + feather» | <b>둘 다</b> — 한 장만 굽되 <b>새 실측값</b>으로. 낡은 경계로 되돌리면 «위 줄 2px 잘림» 버그가 같이 돌아온다 |
| `AruGolem.cs` | 스태시 «상자를 꽉 채운다(cover · FillBoxHeight)» ↔ HEAD «표 값 그대로(contain)» | <b>스태시</b> — 유저가 122절에서 확정한 해석이다 |
| 불칸 이동 PNG 18장 · 카이론 실드 PNG | 바이너리라 합칠 수 없다 | <b>다시 구웠다</b> — 합친 `.py` 로 `chiron/vulcan_skin_build.py` 재실행. 어느 쪽 바이너리를 고르는 판단을 아예 없앴다 |
| `NeoDunggeunmo SDF.asset`(-10,536줄) · LiberationSans Fallback | 폰트 아틀라스 «글리프 캐시» 차이 | <b>HEAD 유지</b> — 122절 요약에 폰트 얘기가 없다. TMP 가 런타임에 굽는 값이고 HEAD 쪽이 글리프가 더 많다 |

★ 복원 결과 확인: 실드 원화 <b>6장 → 1장</b> · 「도움의 손길」(80022) <b>쿨타임 30</b> ·
`ShieldOverlayFx` 136줄 수정 · 골렘 cover 크기 · `Tools` 두 스크립트.
⚠ <b>스태시는 지우지 않았다</b>(`stash@{1}`, `stash@{2}` 로 남아 있다) — 안전망.

### ① 카이론 2번 스킬 「천상의 방패」 — 전투 중일 때만

유저 지시: *"카이론 2번 스킬 전투 중일 때만 발동하게 만들어줘"*.

시작 조건에 «표적이 있을 것» 만 있었다(`TryStartChannel`). 그런데 <b>표적은 «쫓는 중» 에도
잡힌다</b> — 멀리 있는 적에게 걸어가는 동안 정신집중 2초가 돌고, 도발 반경이 <b>1.5타일</b>
뿐이라 아무도 도발하지 못한 채 <b>쿨타임 30초</b>를 날렸다.

→ `TryCelestialShield` 에 `DamageableUnit.IsInCombat` 게이트. 같은 캐릭터의 「타락한 육체」가
쓰는 <b>같은 판정</b>이다(새 규칙을 만들지 않았다).
⚠ 3번 「천벌」에는 걸지 않았다 — 지시가 2번이고, 천벌은 직사각형 원거리라 «붙기 전에 쏘는»
것이 낭비가 아니다. 그래서 공통 함수가 아니라 <b>2번 진입점에만</b> 넣었다.

### ② ★★ 아루의 골렘이 비전투 중 중립 몬스터를 **한 대도** 못 때리고 있었다

유저 리포트: *"아루의 골렘이 비전투 상황일때 중립몹은 공격 안하는 문제"*.

`AruGolem.Follow` 는 «아루가 때리는 적을 골렘도 때린다» 를 위해 <b>매 프레임</b> 불린다.
그런데 아루에게 표적이 없을 때 <b>무조건 `ClearHuntTarget()`</b> 을 했다:

```
골렘 CharacterBehavior : TryFindHuntPrey → SetHuntTarget(중립)   ← 물었다
다음 프레임 Follow      : 아루 표적 없음 → ClearHuntTarget()      ← 뱉었다
```

`ClearHuntTarget` 은 <b>타겟까지 비운다</b>(그 함수). 그래서 문 프레임에 바로 놓기를 반복하며
<b>영원히 한 대도 못 때렸다.</b> 골렘의 탐험 유형은 템플릿 기본값 «사냥»(`expeditionType: 0`)
이라 지침 문제가 아니었다.

→ <b>주인에게서 물려받은 표적만</b> 놓는다. 구분은 <b>진영</b>으로 — 골렘이 스스로 무는
사냥감은 언제나 중립이고(`TryFindHuntPrey` 는 `Faction.Neutral` 만 고른다), 그 밖의 사냥감은
`Follow` 가 넣은 것뿐이다(골렘은 침식을 끄므로 「혼란」이 잡는 사냥감도 없다).
읽을 통로로 `UnitCombat.HuntTarget` 을 열었다 — `IsHunting` 과 <b>같은 판정</b>을 쓴다.

★ <b>같이 고친 두 번째 경로</b> — 골렘이 <b>배회 범위를 주인에게서 물려받는다</b>.
골렘의 지침은 템플릿 기본값이라 배회 범위가 «근방» 인데, 골렘은 <b>아루 옆</b>에 나타난다.
아루가 «전역» 으로 멀리 나가 있으면 골렘은 태어난 순간부터 자기 한계 밖이고, 그 상태에서는
`IsBeyondRoamLimit` 이 <b>새 사냥감을 막는다</b>. 전술을 <b>잠그기 전에</b> 넣는다.

### ③ 아루가 성장하면 골렘도 실시간으로 성장

유저 지시: *"아루가 성장할 때 마다 골렘도 실시간으로 성장(지금은 골렘이 죽고 아루가 성장한
상태에서 다시 골렘이 리스폰되면 골렘도 성장한 채로 나옴)"*.

골렘의 능력치는 `Summon` 시점에 <b>한 번</b> 계산해 정의 에셋에 구워 넣는 값이었다.
→ `AruGolem.SyncStats` 를 매 프레임 부른다(`TickGolemLifetime`). <b>같은 식</b>을 다시 계산해
값이 달라졌을 때만 `CharacterUnit.ApplyStats`(체력 비율 유지) 로 덮어쓴다. 강화뿐 아니라
능력치 보정·패시브·정신 이상으로 아루의 값이 변할 때도 저절로 따라간다 — 정의문의
«아루가 사용하는 능력치의 value01%» 를 <b>시점이 아니라 상태</b>로 읽은 것이다.
⚠ 값이 같으면 아무것도 하지 않는다(매 프레임 `ApplyStats` 는 반올림으로 체력이 1씩 흔들린다).
★ 능력치 규칙은 `StatValueFor`·`StatsOf` 로 한 곳에 모았다 — 소환과 동기화가 두 벌이면 어긋난다.

### ④ ★★ 골렘에게 침식이 쌓이고 있었다 — `enabled = false` 가 아무것도 막지 못했다

유저 지시: *"아루의 골렘에게 침식이 적용 안되도록 (ui에도 아루의 골렘에겐 침식 수치가 보이지
않거나 항상 0으로 고정되게)"*.

`AruGolem.ApplyRules` 는 정의문(«침식이 일어나지 않습니다») 대로 `CharacterErosion` 을
<b>껐다</b>. 그런데 `ErosionService.Update` 는 유닛마다 `EnsureOn` + `Tick` 을 <b>직접</b>
부른다 — 컴포넌트 `Update` 를 쓰지 않는 구조라 <b>`enabled` 가 판정에 들어가지 않는다.</b>
그래서 골렘에게 침식이 그대로 쌓였다.

→ 판정을 <b>도는 쪽</b>에 뒀다: `ErosionService.Update` 에서 `character.IsSummoned` 를 건너뛴다.
켜고 끄는 곳(스킬)과 도는 곳(서비스)이 다르면 또 새므로 «누가 침식하는가» 는 도는 쪽이 정한다.
`CharacterErosion.Tick` 에도 같은 판정을 한 겹 더 뒀다(정의문은 한 곳만 지키게 두지 않는다).
UI 는 `ErosionGaugeView` 가 소환수 칸을 <b>«침식 -»</b> 로 그린다 — «0» 은 «아직 안 쌓였다» 로
읽혀 «곧 쌓이겠구나» 라는 잘못된 기대를 준다.

### ⑤ 크리티컬 숫자 옆 `!` 제거

유저 지시: *"크리티컬 데미지 이펙트옆에 ! 빼기"*. 치명타는 <b>색과 크기</b>로만 구분한다
(`criticalColor` · `criticalScale`). ⚠ <b>회복 치명타의 `!` 도 같이 뺐다</b> — 한쪽만 남기면
«회복 치명타만 기호가 붙는» 어긋난 표시가 된다.

### ⑥ 캐릭터 카드 — 인원 표기 `8/12` · 순서를 **생성순으로 고정**

유저 지시: *"캐릭터그리드에 캐릭터 뽑은 개수에 비례하여 8/12 이런식으로 표기 · 생성순대로
캐릭터 그리드 위치 고정"*.

* <b>표기</b> — `HUD_Roster/Title` 의 첫 문구(«캐릭터»)를 기억해 두고 뒤에 «인원/상한» 만 붙인다
  (문구를 코드에 적으면 씬에서 이름을 바꿀 수 없다). 세는 값은
  `CharacterCreationService.AliveCount` — <b>상한을 막는 값과 같은 값</b>이어야 «12/12 인데 왜
  못 만드나» 가 안 생긴다(로스터 행 수는 죽은 카드까지 남아 있어 쓰지 않았다).
  분모는 새로 노출한 `MaxCharacters`(=12). 서비스가 없으면 <b>분모를 그리지 않는다</b>.
* <b>순서</b> — 체력순 정렬을 없앴다. 예전에는 «체력 % 낮은 순, 사망은 맨 아래» 였는데,
  <b>맞을 때마다 카드가 자리를 바꿔</b> 누르려던 카드가 손 밑에서 사라졌다. 순서의 근거는
  `_characters` 목록의 순번이다 — `AppendNewCharacters` 가 <b>뒤에만</b> 붙이므로 그 자체가
  생성순이고, 죽어도 빠지지 않는다. 즉 <b>죽은 카드도 자기 자리에 남는다</b>(«위치 고정»).

### ⑦ 같은 캐릭터 중복 생성 금지

유저 지시: *"같은 캐릭터 중복 생성 안되게 설정"*. 처음부터 만들어 둔 스위치
(`CharacterDefinitionRegistry.preventReappearance`)를 <b>켰다</b> — 캐릭터가 2명뿐이던 때는
3번째부터 막혀서 꺼 뒀고, 지금은 <b>11명</b>이라 켤 수 있다.

⚠ <b>켜기만 하면 끝이 아니었다.</b> 인물을 다 쓰면 `Pick` 이 null 을 주고, `UnitSpawner` 는 그
null 을 «정의 에셋이 없다» 로 읽어 <b>능력치 무작위인 «무명 캐릭터»</b>를 만들었다 — 중복을
막으려고 켠 규칙이 «무명 양산» 으로 새는 셈이다. 그래서 두 뜻을 갈랐다:

* `CharacterDefinitionRegistry.Exhausted` / `RemainingCount` 신설
* `UnitSpawner` — 소진이면 <b>만들지 않고 되돌린다</b>(칸도 되돌려 놓는다)
* `CharacterCreationService.OutOfCandidates` — 비용을 깎기 <b>전에</b> 막고 «더 등장할 인물이
  없습니다» 를 띄운다(환불 경로를 타면 로그가 두 줄 나와 헷갈린다)

★ 그래서 <b>한 판의 인물 상한은 정의 수(11)</b> 이고, 인원 상한 12보다 이쪽이 먼저 걸린다.

### ⑧ 중립 몬스터 — **체력 배율 칼럼** 신설 (표 + 게임)

유저 지시: *"중립 몬스터에게도 체력 배율 추가 해야될듯 특히 보스 몬스터 칼럼 추가하고
테이블에도 추가해줘"*.

* 표: `임시용 중립 몬스터.xlsx` → `first_Stat` 시트에 <b>`hp_percent`(체력 배율(%))</b> 신설
  (`Tools/table_update_20260821_neutral_hp_percent.py` · Excel COM · 백업 자동).
  <b>씨앗값은 전부 100 = 배율 없음</b> — 균형 수치는 기획이 정하는 값이라 지어내지 않았다.
  보스 체력을 4배로 하려면 1101~1104 행에 <b>400</b> 을 적으면 된다.
* 게임: `NeutralMonsterDefinitionSO.hpPercent` · `sync_tables_to_assets.py` 가 옮긴다
  (`NEUTRAL_NEW_FIELDS` 에 넣어야 기존 에셋 YAML 에 줄이 <b>생긴다</b>).
* <b>왜 필요한가</b> — 중립은 웨이브 배율을 받지 않는다(설계). 그래서 에픽 보스 체력을 키우려면
  표의 `hp` 칸을 계속 키워야 하는데, 그 칸은 다른 종과 <b>같은 척도</b>라 «40 vs 4000» 처럼
  벌어지면 표를 읽기 어려워진다. ⚠ 웨이브 쪽 같은 이름 칸(`MonsterDefinitionSO.hpPercent`)은
  <b>상한 우회</b>용이라 폐기됐지만, 이 칸은 <b>척도</b> 문제를 푸는 것이라 성격이 다르다.

### ⑨ ★★ 중립 몬스터 **사냥 성장** — 같은 종을 잡을수록 다음 개체가 강해진다

유저 지시: *"중립 몬스터 같은 개체를 일정 마리 이상 사냥할 경우 배율이 적용 되는 로직 만들어줘
우선은 10마리당 0.1 배율 추가로 만들고(체력 말고는 상한값 웨이브 몬스터와 동일하게) 이거
에딧에서 조정가능하게 만든다음 어디에 넣었는지 알려줘"* · *"중립 몬스터 성장 배율에 자원값도
배율 적용 되어야 해"*.

<b>어디서 조정하나 — 하이라키다</b> (유저 지시: *"에딧모드에서 변경 가능하게 해달라고"*):

```
Hierarchy ▸ GameSystems ▸ Inspector ▸ Neutral Growth Service
```

| 칸 | 기본값 | 뜻 |
|---|---|---|
| `growthEnabled` | 켜짐 | 끄면 처치 수만 세고 배율을 걸지 않는다 |
| `killsPerStep` | 10 | 몇 마리마다 한 단계 |
| `stepMultiplier` | 0.1 | 한 단계당 +10% (10마리 x1.1 · 100마리 x2.0) |
| `maxMultiplier` | 0 | 배율 상한 (0 = 무제한) |
| `scaleEnergyReward` | 켜짐 | 보상 에너지에도 같은 배율 |
| `logGrowth` | 꺼짐 | 소환 때 «종·처치수·단계·배율·체력» 을 콘솔에 찍는다 |

⚠⚠ <b>처음에 `BalanceConfigSO`(에셋)에 만들었다가 옮겼다.</b> 유저가 원한 것은
<b>하이라키에서 바로 만지는 것</b>이었다 — 침식 수치가 `ErosionService` 로 `GameSystems` 에
있는 것과 같은 자리다. <b>수치의 정본은 이제 이 컴포넌트 하나뿐</b>(에셋 쪽 칸은 지웠다).
★ 다만 <b>능력치 상한</b>은 에셋에 남는다 — 웨이브 몬스터와 <b>공유</b>하는 값이다.

코드는 셋뿐이다 — <b>수치</b> `NeutralGrowthService`(신규 · 씬 컴포넌트) ·
<b>세는 곳</b> `NeutralKillTally`(신규 · 정적 · 종별 처치 수) ·
<b>쓰는 곳</b> `NeutralMonsterUnit.Initialize`(소환 순간에 배율이 굳는다) ·
`NeutralMonsterDefinitionSO.BuildStats(growth, balance)`.

<b>어디에 걸고 어디에 안 거는가</b> — 웨이브 몬스터와 <b>같은 규칙</b>으로 맞췄다
(«체력 말고는 상한값 웨이브 몬스터와 동일하게»):

* <b>체력</b> — 배율을 걸고 <b>상한 없이</b> 오른다(`monsterHpStatMax` 기본 0 = 무제한)
* <b>공격 계열 4칸</b> — 배율을 걸고 <b>웨이브와 같은 상한</b>으로 자른다
  (`AttackStatMaxFor(epic)` — 에픽은 보스 상한 150)
* <b>방어·재생·명중·치명·저항</b> — 배율을 걸지 않는다. 웨이브 배율도 이 칸들은 건드리지 않는다
  (두 곳의 규칙이 갈리면 «어느 쪽이 맞나» 를 매번 다시 물어야 한다)
* <b>보상 에너지</b> — `RollEnergyReward()` 에 곱한다. ⚠ 배율은 <b>그 개체가 태어난 시점</b>의
  값이다 — 방금 잡은 한 마리가 자기 보상을 올려주지 않는다(그러면 «마지막 한 마리만 이득»)

⚠ 배율은 <b>소환 순간에 굳는다</b> — 이미 서 있는 개체가 갑자기 세지지 않는다.

### ⑩ 미니맵 — 클릭하면 **시야 사각형**

유저 지시: *"미니맵 클릭시 사각형범위생성해야함"*. 미니맵을 눌러 카메라를 옮겨도 <b>화면이
지도의 어디를 보고 있는지</b> 알 수 없었다. RTS 미니맵의 기본 장치인 «시야 상자» 를 그린다.

* 크기를 <b>지어내지 않는다</b> — 출력 카메라의 `orthographicSize`(세로 절반)와 `aspect` 로
  계산한다. 줌을 바꾸면 상자도 같이 커지고 작아진다.
* <b>맨 위에</b> 그린다(경보·유닛 점에 가리면 쓸모가 없다).
* 누른 직후 `cameraViewFlashSeconds`(0.7초) 동안 <b>강조색</b>.
* `cameraViewAlwaysVisible` 을 끄면 <b>클릭 직후에만</b> 보인다 — 지시의 «클릭시» 를 문자
  그대로 읽고 싶을 때의 손잡이다(기본은 항상 표시).

### ⑪ ★★★ 이벤트 창이 **한 번도 열릴 수 없는 구조**였다

유저 지시: *"이벤트 지금 적용 되어도 시각적으로 확인이 불가하니까 이벤트가 등장할 시 유저에게
보이는 ui 조성 그리고 선택지 버튼도 연결해줘"*.

<b>플레이 모드에서 재현했다</b>(`alwaysTrigger` 켜고 3초 뒤) — 콘솔에는
`[이벤트] 비공개 주사위 82 < 80% → 발생` 이 찍히는데 `HUD_Event` 는 <b>비활성 그대로</b>였다.

원인: `HUD_Event` 는 씬에 <b>비활성</b>으로 저장돼 있고, 유니티는 비활성 오브젝트의
`Awake`·`OnEnable`·`Update` 를 <b>한 번도 부르지 않는다</b>. 그런데 `EventPanel` 은
<b>매 프레임 `Update` 에서 `OnEventChanged` 를 구독</b>하는 구조였다 — 그 코드는 <b>영원히
실행되지 않았다.</b> 표·확률·대사·보상은 전부 정상으로 돌고 있었고 <b>보여주는 통로 하나만</b>
죽어 있었다. (그래서 «적용은 됐는데 확인이 불가» 라는 리포트가 정확했다.)

→ 방향을 <b>뒤집었다</b>: `EventService` 가 창을 <b>직접 부른다</b>(`EventPanel.Present`).
비활성 오브젝트도 <b>참조로는</b> 부를 수 있다.
⚠ 찾을 때 `FindObjectsInactive.Include` 를 반드시 켜야 한다 — 기본값으로는 꺼진 창을
<b>못 찾는다</b>(이 버그의 두 번째 함정). 창은 캐시한다.
⚠ `Awake` 에서 `SetActive(false)` 를 <b>없앴다</b> — 처음 활성화되는 순간이 곧 «열릴 때» 라
그때 닫으면 열자마자 닫힌다. 씬에 켜진 채로 저장된 경우만 `Start` 가 정리한다.
⚠ `Bind()` 를 <b>한 번만</b> 돌게 했다 — `onClick` 을 두 번 붙이면 선택지가 두 번 눌린다.
★ `OnEventChanged` 는 그대로 남겼다(다른 구독자가 붙을 자리다).

<b>검증</b> — 다시 플레이해서 `HUD_Event.activeSelf == true` 확인 · 선택지 구성도 확인했다
(`Choice0` 활성 · `Choice1` 비활성 = 그 줄에 둘째 분기가 없음 · `CloseButton` 활성).

★ <b>씬에서 `HUD_Event` 를 «활성» 으로 저장했다</b> — 런타임 동작은 같고(`Start` 가 닫는다),
<b>MCP 로 편집할 수 있게</b> 하려는 것이다: 이 세션에서 확인한 대로 <b>MCP 는 비활성
오브젝트를 이름·경로로 찾지 못한다</b>(그래서 «꺼진 창을 고치려면 켜야 하는데 켜려면 찾아야
한다» 는 순환에 걸린다).

### ⑫ 아르세니아 「성스러운 축복」(80029) — 표 개정 반영

유저: *"아르세니아 테이블에 스킬 조금 수정 했는데 이거 반영해주고"*.
`gen_character_assets.py` → `gen_string_table.py` 로 다시 읽어 대조했다. 바뀐 것은
<b>80029 한 줄</b>이고 그 안에서 두 군데다:

1. <b>«자신을 중심으로 반지름 {value_05} 타일 범위 내»</b> 의 아군만 고른다(value_05 = 5 신설).
   예전 문장에는 거리 제한이 없어 <b>맵 반대편에서 싸우는 아군</b>에게도 물약이 날아갔다.
2. <b>회복 증폭이 {value_03} → {value_04}%</b> 로 갈라졌다 — 예전에는 «몬스터 초당 피해» 와
   «아군 회복 증폭» 이 같은 칸이어서 코드도 같은 값을 두 번 넘겼다(`SacredZone.Spawn`).

→ `TrySacredBlessing` 에 `pickRadius`·`healPercent` 분리. `FindFrontMostFightingAlly(withinTiles)`.
⚠ value_05 가 0 이면 <b>거리 제한 없음</b>(0 = 무제한 규약).
⚠ 후보를 <b>거르는 기준점</b>은 «내 위치», 그중 <b>고르는 기준</b>은 «적과의 거리» 다.

★ <b>같이 따라온 표 변경 셋</b>(전부 «표가 정본» 대로 에셋 재생성): 80024 「강림」 문구에
*"아루가 사망할 경우 골렘도 함께 사망합니다"* 추가 · 80027 「천벌」 쿨 67 → <b>60</b> ·
80022 「도움의 손길」 쿨 30 → <b>0</b>.

⚠⚠ <b>80022 는 122절과 반대 방향이다</b> — 그 절은 «표 30 · 에셋 0» 이라 보고 쿨타임 스킬로
옮겼는데 지금 표는 <b>0</b> 이다. 표를 정본으로 두어 0 으로 맞췄다(쿨 0 이면 «상시» 로 돌아가고,
그래도 `HelpingHandInterval` 0.5초가 있어 매 프레임 훑지는 않는다).
<b>30초로 되돌리려면 표의 `cool_time` 칸에 30 을 적으면 된다</b> — 코드는 이미 읽는다.

### ⑬ ★★ 「혼란」이 풀려도 아군끼리 계속 싸우던 버그

유저 리포트: *"혼란 상태에서 캐릭터 끼리 전투가 일어나면 혼란이 풀리더라도 몬스터가 오기
전까진 계속해서 둘이서 전투를 하는 버그"*.

<b>혼란에 걸린 쪽은 제대로 멈춘다</b>(`ClearForcedHuntTarget` 이 잠금과 타겟을 놓는다).
문제는 <b>맞은 쪽</b>이었다 — `UnitCombat.FindRetaliationTarget` 이 «나를 때린 상대» 를
<b>진영을 보지 않고</b> 돌려줬다:

```
A(혼란) → B 를 때린다
B : 반격 대상 = A          ← 진영 검사가 없었다
A : 반격 대상 = B (B 가 때렸으니)
→ 서로가 서로의 «방금 나를 때린 상대» 가 되어 8초 기억이 영원히 갱신된다
```

반격은 <b>적을 못 찾았을 때만</b> 보므로, 진짜 적이 오면 그쪽이 이긴다 — 리포트의
«몬스터가 오기 전까지» 가 그 구조를 정확히 가리킨다.

→ <b>같은 진영이면 반격하지 않는다</b> 한 줄. ★ 같은 판정이 <b>동료 구원 쪽에는 이미
있었다</b>(`AttackerOf` 의 «혼란으로 아군이 때린 경우») — 두 경로 중 한 곳에만 있었던 것이다.
⚠ 혼란에 걸린 쪽의 공격은 이 함수를 지나지 않는다(강제 사냥 타겟) — 이 한 줄이
<b>혼란의 효과를 약화시키지 않는다.</b>

### ⑭ ★★ 표의 「스킬 3 id」 필드명 중복 — 에픽 중립 셋의 두 번째 스킬이 사라지고 있었다

폴리르(1104)를 추가할 때 `neutrality_mon` 시트에 「스킬 3 id」 칼럼이 생겼는데,
<b>필드명(2행)이 `mon_skill_2` 로 중복</b>돼 있었다(복사 흔적):

```
한글 헤더 :  스킬 1 id   |  스킬 2 id   |  스킬 3 id
필드명    :  mon_skill_1 |  mon_skill_2 |  mon_skill_2   ← 중복
```

`read_rows` 는 <b>필드명으로</b> 행을 딕셔너리로 만든다 — 그래서 `mon_skill_2` 가
<b>뒤 칼럼(1101~1103 에서는 빈 칸)으로 덮여</b> 버렸고, sync 를 돌리자
<b>카르시노스·아니사킬·바리올라의 두 번째 스킬이 조용히 지워졌다</b>
(플레이 콘솔 «스킬 2종 준비» → «1종 준비» 로 발견했다).

→ 표의 필드명을 `mon_skill_3` 으로 고치고(`Tools/table_update_20260821_neutral_skill3_field.py`)
sync 가 <b>세 칸</b>을 읽게 했다. 재확인: `[2001, 2002]` · `[2003, 2004]` · `[2005, 2006]` 복구 ·
플레이 콘솔 «2종 준비» 복구 · 에러 0.

⚠ <b>교훈</b> — 표 칼럼을 복사해서 늘릴 때 <b>2행(필드명)까지</b> 고쳐야 한다. 필드명이 겹치면
  게임이 «조용히» 값을 잃는다(에러도 경고도 안 난다).

### 씬 변경 여부

**있음** — `UI_Root/HUD_Event` 를 <b>활성</b>으로 저장(위 ⑪ ★). 그 밖에 오브젝트 추가·삭제는
없다. `save_scene` 1회.

⚠⚠ <b>이 세션에서 겪은 씬 함정 두 개를 적어 둔다</b>:
1. <b>에디터가 디스크보다 오래된 씬을 들고 있었다</b> — `HUD_Event` 가 하이라키에 아예 없었다
   (파일에는 있다). 그 상태에서 `save_scene` 을 했으면 UI-45 의 씬 작업이 통째로 날아간다.
   → <b>작업 시작 전에 `load_scene` 으로 다시 읽었다.</b> 씬을 만질 세션은 이걸 먼저 할 것.
2. <b>`load_scene` 은 «저장할까요?» 를 자동 수락한다</b> — 씬이 dirty 한 상태에서 다른 씬을
   열면 <b>지금 메모리 상태가 그대로 저장된다</b>. 디버그로 바꿔 둔 값이 커밋될 수 있다
   (실제로 `EventService` 의 `alwaysTrigger` 가 한 번 저장됐고 되돌렸다 — HEAD 값과 대조 확인).

### 검증

`recompile_scripts` 오류 0 · 경고 0 (매 단계) · `sync_tables_to_assets.py` 전체 통과 ·
카이론/불칸 재분해 147장·130장 · `measure_skin_tiles.py` <b>0개 갱신</b>(재분해가 크기를 한 톨도
안 바꿨다는 검산) · 실드 프레임 <b>1장</b> 확인 · 표 `hp_percent` 8행 100 확인 ·
플레이 모드 2회(이벤트 발생 → 창 활성 확인).

### 아직 확인 못 한 것 (유저가 볼 것)

1. ⚠⚠ **폴리르(1104) 미구현** — 표에는 이미 있다(`neutrality_mon` · `first_Stat` · 스킬
   2007·2008·2009). 이번에 <b>표 → 스킬 에셋</b>까지는 자동으로 들어갔지만
   (`BossSkill_2007~2009` 생성됨), 게임에 세우려면 바리올라(120절)급 작업이 남았다:
   원화 분해(`Char_Asset_Polyir.png` 2304x1536 · 라벨 14개 · <b>시트는 깨끗하다</b>) ·
   스킨 에셋 · 서식지 타일(`Polyir_chunk/deco.png`) · `habitat_design` 1104 행 ·
   템플릿 오브젝트 · `NEUTRAL_ASSET_BY_ID` 에 1104 → `NeutralMonster_8` ·
   <b>스킬 3종 동작 구현</b>(`Flame_emission` 부채꼴 화염 · `Rapid_Playback` 급속 재생 ·
   `Dread` 낙뢰). 지금은 `sync` 가 «중립 id 1104 에 해당하는 에셋 이름을 모릅니다» 로 건너뛴다.
2. **이벤트 창 모양** — 열리는 것과 선택지 동작은 고쳤지만, 배경 이미지(`event_bg`)·연출·
   타이핑 효과는 여전히 없다(글자와 버튼만 있는 임시 창이다).
3. **중립 사냥 성장 체감** — 10마리당 +10% 가 적당한지는 플레이로 봐야 한다.
   `BalanceConfig` 에셋에서 바로 조정 가능(위 ⑨ 표).
4. **캐릭터 11명 vs 인원 상한 12** — 재등장 금지를 켠 이상 12번째는 만들 수 없다.
   상한을 11로 내리거나 캐릭터를 한 명 더 추가하는 것은 기획 판단이다.

### 씬반영요청 목록

- 없음

---

## UI-47. 폴리르 데코 시트 교체(투명 배경) · 이벤트 지속시간 인스펙터 · ★★ 이어하기 유령 카드 3장 · 게임 재시작 버튼 (2026-08-21, 3차)

유저 지시(들어온 순서대로):

1. *"플뢰르 데코 파일 변경한거 적용(투명 배경)"*
2. *"이벤트 타이머 지속시간 에딧에서 수정가능하게 게임 시스템 하이라키에서"*
3. *"현재 아웃게임 로비화면에서 저장하고 이어하기를 눌렀을 때 캐릭터 그리드 (왼쪽위 체력바
   있는거)에 아무런 상호작용이 되지 않는 캐릭터 UI 3개가 나와"*
4. *"환경설정 UI에서 '저장하고 로비로 돌아가기' 아래에 게임 재시작 버튼 하나 추가로 만들어서
   그 버튼 누르면 게임이 처음으로 초기화되는 기능 만들어줘"*

⚠⚠ **이 세션은 유니티가 꺼져 있었다** — MCP 브리지(포트 8090)가 없어서 씬 오브젝트를
MCP 로 만들 수 없었다. 4번은 그래서 **씬 YAML 을 파이썬으로 편집**했다(아래 47-4 ★★).

---

### 47-1. 폴리르(=유저가 말하는 「플뢰르」) 데코 시트가 **통째로 바뀌었다**

볼트의 `리소스/sprites/Polyir_deco.png` 가 갈렸다 — **규격까지** 달라졌다:

| | 옛 시트 | 새 시트 |
|---|---|---|
| 크기 | 1586x992 | **1536x1024** |
| 채널 | RGB (배경 = 칸 바탕색) | **RGBA (배경 투명 · 62%)** |
| 겹쳐 그린 것 | 「20」 숫자 · 캡션 · 칸 테두리 | **없다** |

그래서 `gen_polyir_habitat_tiles.py` 가 데코를 위해 갖고 있던 **세 겹의 장치가 전부 필요
없어졌고, 그대로 두면 해롭다** — 옛 격자(X0/X1/Y0/Y1/CONTENT)는 1586x992 기준이라 새 시트에서는
**엉뚱한 자리를 자른다**. 지웠다: `prop_box` · `is_grey` · `keyed` ·
`PROP_ART_DIFF`·`GREY_SAT_MAX`·`GREY_LUM_MIN`·`PROP_BG_TOL`.

★ **바닥 시트(`Polyir_chunk.png`)는 안 바뀌었다** — 격자 상수는 남아 있고 이제 **바닥 전용**이다.
  (주석에 그렇게 적어 뒀다. 안 적으면 다음 사람이 «데코는 왜 격자를 안 쓰지» 를 다시 판다.)

**새 데코 경로 — 알파를 그대로 믿는다**(`prop_boxes` · `square` · `shrink`):

* **«칸» 을 세지 않고 «덩어리» 를 센다.** 프롭이 **칸 경계를 넘나든다** — 실측하면 32칸 중
  절반이 칸의 좌우 끝(x=0 · x=191)에 닿아 있다. 균등 격자로 자르면 가시가 잘리고 옆 칸 것이
  물려 들어온다. 그래서 **그림이 스스로 알려주는 경계**를 쓴다.
* 덩어리 찾기는 **가로 런(run) + 유니온 파인드**다. 화소 단위 BFS 는 150만 화소에서 느리고
  이 프로젝트에는 `scipy.ndimage` 가 없다. 런은 몇 천 개뿐이라 즉시 끝난다. 8방향으로 잇는다
  (대각선으로만 붙은 가시 끝이 따로 떨어지지 않게).
* ⚠ **마스크로 이웃을 지운다** — 상자만 잘라 쓰면 «옆 것이 물려 들어오는» 옛 문제가 되살아난다.
* ⚠ **알파를 곱해 두고 줄인다**(premultiply) — 그냥 줄이면 투명 화소의 RGB(검정)가 가장자리에
  섞여 **검은 테두리**가 생긴다(113-2절과 같은 함정).
* ★ **32 를 못 박지 않았다** — 시트가 알려주는 개수만큼 굽고, 8x4 와 다르면 **경고를 찍는다**.
  조용히 32개를 만들면 «어느 프롭이 어느 타일인가» 가 어긋난 채로 넘어간다.
* 알파 1~3px 짜리 **먼지가 여섯 군데** 있었다(원화 저장 흔적) — `PROP_MIN_PIXELS = 2000` 이
  거른다. 실제 프롭은 가장 작은 것이 10,702px 이라 여유가 아주 크다.

**검증** — 32개 인식 · 구운 타일 32장을 자홍 배경에 합성해 눈검사(순서·투명·잘림·검은 테두리
전부 확인) · 타일 이름과 guid 는 그대로라 `PolyirHabitatProps` 참조가 안 끊겼다.

### 47-2. 이벤트 **지속시간** — `GameSystems ▸ Event Service` 에서 조정

⚠⚠ **«있지만 안 도는» 값이었다.** 표의 `event_value_01`(웨이브 120 · 비공개 180)을
`EventDefinitionSO.DurationSeconds` 가 읽어 두기는 했는데 **아무도 쓰지 않았다** — 이벤트는
오직 «웨이브 단계가 바뀔 때»(`HandlePhase`)만 끝났다. 즉 비공개 이벤트는 **표가 적은 180초와
아무 상관 없이** 웨이브가 넘어갈 때까지 남아 있었다.

**어디서 조정하나** — `Hierarchy ▸ GameSystems ▸ Inspector ▸ Event Service ▸ 이벤트 지속시간`.
침식(`ErosionService`)·중립 사냥 성장(`NeutralGrowthService`)이 있는 **같은 자리**다.

| 칸 | 기본값 | 뜻 |
|---|---|---|
| `useTableDuration` | 켜짐 | 표의 `event_value_01` 을 쓴다. 끄면 아래 두 칸이 **전부를 덮는다** |
| `waveEventDurationSeconds` | 120 | 웨이브 이벤트. **0 = 시간으로는 안 끝난다** |
| `privateEventDurationSeconds` | 180 | 비공개·토벌 이벤트. 0 = 무제한 |
| `pauseWhilePanelOpen` | 켜짐 | **창이 떠 있는 동안은 안 깎인다** |

★ `pauseWhilePanelOpen` 을 켜 둔 이유 — 시간을 세는 목적은 «지속 보정이 영원히 남는 것» 을
막는 것이지 유저를 재촉하는 것이 아니다. 선택지를 읽는 시간이 제한시간에서 깎이면
«읽다가 사라졌다» 가 된다. 제한시간 연출을 원하면 끄면 된다.

새 코드는 `TickDuration` 하나 · `DurationFor` 하나다. 끝낼 때 `EndCurrent` 를 그대로 타므로
`EventRewardService.ClearAll()` 이 같이 돌아 **시간과 효과가 갈리지 않는다**.
`RemainingSeconds` 를 public 으로 열어 뒀다 — 나중에 «남은 시간» 을 그릴 자리다(지금은 아무도 안 본다).

### 47-3. ★★ 이어하기 직후 **아무 반응 없는 캐릭터 카드 3장**

**왜 3장인가** — 게임 씬은 열리자마자 **시작 캐릭터 3명**을 자동으로 세운다(80절).
`CharacterRosterPanel.Start` 가 그 셋을 `_characters` 에 담고 행을 만든다. 그 **다음 프레임**에
`GameSnapshot.RestoreNextFrame` 이 돌면서 `UnitSpawner.DestroySpawnedCharactersForRestore()` 가
셋을 **통째로 파괴**하고 저장된 인원을 새로 세운다.

그런데 이 목록은 **죽어야만** 줄어든다(`HandleWaveEnded` — «죽은 카드를 웨이브가 끝날 때까지
회색으로 남긴다» 는 유저 요청 때문이다). 파괴된 셋은 `OnDied` 를 **부르지 않고** 사라지므로
`_dead` 에도 안 들어간다. 그래서 셋이 목록에 영원히 남고, `RefreshValues` 는
`row.Unit == null` 이라 **건너뛰기만** 한다 — 마지막으로 그려진 이름·체력바가 **그대로 굳은**
행 세 개. 눌러도 아무 일이 없는 이유는 `Row.Unit` 이 파괴된 오브젝트라서다.

→ `PurgeVanishedCharacters()` 한 개. 판정은 **«파괴됐는데 `_dead` 에 없으면 갈아엎힌 것»**
이다 — 죽은 캐릭터는 파괴되기 **전에** `_dead` 에 들어가므로 «회색으로 남겨둘 카드» 와
확실히 갈린다.

⚠ **불러오기 전용으로 만들지 않았다** — «판을 갈아엎는» 경로가 또 생겨도(47-4 의 재시작 등)
이 한 곳이 알아서 정리한다.

★ 120절이 스포너 쪽에서 고친 것(«비활성화를 먼저 해서 그 프레임에 등록을 끊는다»)과 **다른
버그**다. 그쪽은 «새 유령 행이 *추가로* 생기는 것» 을 막았고, 이것은 «이미 만들어져 있던 행이
안 지워지는 것» 이다.

### 47-4. 환경 설정에 「게임 재시작」 버튼

**어디에** — `HUD_Settings ▸ Body ▸ RestartButton` (「저장하고 로비로 돌아가기」 **바로 아래**,
y = -112). 아래에 있던 `Volume`·`Status` 를 56px 씩 내렸다. **창(520x430)은 안 키웠다** —
`Status` 아래에 76px 이 비어 있어서 내린 뒤에도 `Copyright` 와 20px 이 남는다(실측).

★★ **왜 파이썬이 씬 YAML 을 건드렸나** — 이 프로젝트의 규칙은 «오브젝트는 MCP 로 하이라키에
직접 만든다»(§10 H-1)인데 **이 세션에는 유니티가 꺼져 있었다**. 두 갈래뿐이었다:

| | 하이라키에 실물 | 인스펙터 조정 | 글꼴 |
|---|---|---|---|
| 런타임 `Instantiate` | ✗ | ✗ (H-1 이 막으려는 상태) | — |
| **씬 YAML 직접** | ✓ | ✓ | ✓ |

후자를 골랐다. 대신 **손으로 적지 않는다** — `Tools/scene_add_restart_button.py` 가 옆 버튼
(`LobbyButton`)의 **하위 트리 블록을 통째로 복제**하고 fileID 만 새로 딴다(9개 블록).
그래서 판 색·크기·Button 전이색이 **옆 버튼과 한 톨도 다르지 않다**.

★ **글꼴을 다시 구울 필요가 없다** — 복제본의 `m_fontAsset` 이 원본과 같은 네오 둥근모 SDF 를
  가리킨다. TMP 를 «만드는» 것이 아니라 «베낀» 것이라, 이 프로젝트가 네 번 겪은
  «새 글자만 Liberation Sans» 함정을 아예 지나간다.
⚠ 스크립트는 **멱등**이다 — 이미 `RestartButton` 이 있으면 아무것도 안 한다.
⚠ **유니티를 끄고 돌릴 것.** 켜져 있으면 저장하는 순간 편집이 통째로 덮인다(123-12절의 사고).

**"처음으로 초기화" 를 무엇으로 읽었나** — 로비의 «새로하기»와 **같은 도착 지점**이다.
로비를 거치지 않을 뿐 결과가 같아야 «둘이 다르다» 가 안 생긴다. `SettingsPanel.RestartRun()`:

1. `EventRewardService.ClearAll()` + `EventService.EndCurrent` — 지속 보정을 **유닛이 살아
   있을 때** 거둔다.
2. ⚠⚠ `CharacterDefinitionRegistry.ResetRun()` · `NeutralKillTally.ResetRun()` —
   **둘 다 `static` 이라 씬을 다시 열어도 살아남는다.** 두 클래스 모두 «새 판을 시작할 때
   비운다» 는 `ResetRun()` 을 이미 갖고 있는데 **아무도 부르고 있지 않았다**(에디터 플레이
   진입 때 도메인 리로드가 우연히 비워 주고 있었을 뿐이다). 여기가 그 자리다.
   ★ 로비의 «새로하기» 도 같은 구멍을 갖고 있다 — 씬만 갈아치울 뿐 static 은 그대로다.
3. `SaveService.Delete()` + `PendingLoad = null` — 안 지우면 첫 자동 저장 전에 게임을 껐다 켤 때
   **버린 판으로 되돌아간다**(로비 «새로하기» 와 같은 이유).
4. `Time.timeScale = 1f` 뒤 **지금 씬을 다시 연다**(`GetActiveScene().name`).
   ⚠ 씬 이름을 적어 두지 않았다 — 두 곳(여기·로비)에 적으면 한쪽만 고쳐질 수 있다.

⚠ **두 번 눌러야 실행된다**(`restartConfirmSeconds` 5초). 되돌릴 수 없고 저장까지 지우는
동작이 「저장하기」 바로 아래에 있어서, 한 번의 오조작으로 판이 통째로 날아가면 안 된다.
첫 누름은 `Status` 칸에 경고만 띄우고, 5초가 지나거나 창을 다시 열면 풀린다.

### 씬 변경 여부

**있음** — `UI_Root/HUD_Settings/Body` 에 `RestartButton`(+ 자식 `Label`) 추가,
`Volume`·`Status` 를 56px 아래로. 블록 9개 추가(fileID 7300000~7300008).
파이썬으로 편집했으므로 **유니티에서 씬을 열면 그대로 보인다**(다시 저장할 필요 없음).

### 검증

`dotnet build Assembly-CSharp.csproj` **오류 0 · 경고 0** ·
씬 fileID **2180개 전부 유일**(중복 없음) · 씬 말미 개행 복구 ·
데코 타일 32장 눈검사 · `.meta`/`.asset` 은 **줄바꿈만 달라진 것을 되돌려** 실제 변경분만 남겼다
(내용 대조로 확인 — 실제 변경 0건).

### 아직 확인 못 한 것 (유저가 볼 것)

1. **데코가 서식지 위에서 어울리는지** — 타일 자체는 확인했지만 바닥과 같이 깔린 그림은
   플레이로 봐야 한다.
2. **재시작 버튼 자리** — 씬을 열어 창 안에서 겹치지 않는지(계산상 20px 여유). 좁으면
   `Tools/scene_add_restart_button.py` 의 `NEW_Y`·`SHIFT_BELOW` 가 아니라 **인스펙터에서**
   바로 옮기면 된다(그게 실물을 씬에 넣은 이유다).
3. **이벤트 지속시간 체감** — 표의 120/180초가 적당한지. 위 표의 칸에서 바로 조정 가능.
4. 123-16 의 남은 일(폴리르 이름 · 이벤트 창 모양 · 캐릭터 11명 vs 상한 12)은 **그대로다**.

### 씬반영요청 목록

- 없음 (씬 편집을 이 세션에서 직접 마쳤다)

---

## UI-48. 이벤트 테이블 Ver013 이식 · 아르세니아 유령 마법진 · 캐릭터 생성 사망 후 잠김 · 폴리르 토벌 목록 · 시카리아 흰 판때기 (2026-08-21, 4차)

유저 지시(들어온 순서대로):

1. *"이벤트 테이블 수정된거 읽어보고 다시 인게임에 구현"*
2. *"아르세니아가 없는데 아르세니아의 2번째 스킬이 맵에 장식물처럼 구현되어있음"*
3. *"캐릭터가 죽으면 캐릭터 생성이 안되는 버그있는데 이거 수정해"*
4. *"다른 중립 에픽 몬스터들은 … 토벌 지시 목록에 뜨는데 폴리르만 안뜨고 있어"*
5. *"시카리아가 공격할 때 활 안에 흰색 이미지 … 이동할 때 다리 뒤에 흰색 이미지"*

★ 이번 세션은 **유니티가 켜져 있었다** — 씬 수정은 MCP 로 직접 했다(§10 H-1).

---

### 48-0. 행동 UI 패널 검은 여백 — **다시** 적용했다

UI-47 에서 고쳤다고 적었는데 **원복돼 있었다**(조회하니 `sizeDelta.y` 가 366 그대로).
에디터가 씬을 메모리에 들고 있어 저장이 덮인 것이다(123-12절과 같은 사고).

→ MCP 로 다시 고치고 이번엔 **씬 파일에 기록된 것까지 확인**했다
(`Proto_01.unity:167758` = `m_SizeDelta: {x: 260, y: 300}`).

원인 자체는 `BuildButtonUI.Start` 가 「건물 건설」을 **플레이 중에만** 숨기는데
(`BuildService.FeatureEnabled` 가 꺼져 있다) 배경 판은 버튼 7개 크기로 남아 있던 것.
366 → 300 으로 줄여 버튼 6개(40×6 + 간격 8×5 = 280 + 여백 20)에 맞췄다.
⚠ 「건물 건설」 버튼 **자체는 남겼다** — 코드 주석이 «기능이 다시 켜지면 쓴다» 고 못박고 있다.

### 48-1. ★★ 이벤트 테이블 **Ver013** — 구조가 통째로 바뀌었다

볼트의 표가 갈렸다: `…Ver012_파송송계란탁.xlsx` → **`…Ver013.xlsx`**.
`Info` 시트가 «무엇을 고쳐야 하는가» 를 직접 적어 두었고(r69·r70), 그 목록을 그대로 따랐다.

| Ver012 | Ver013 |
|---|---|
| `Dialogue` 시트 (대사 사슬 168행 · `next_dialogue_id_01/02` 그래프) | **삭제** → `Event.event_script` 한 칸 |
| `EventType` 시트 (5001/5002/5003) | **삭제** → `Event.trigger_cond` enum 3종 |
| `Switch` 시트 (start/finish/restart) | **삭제** → `Event.repeatable` 불리언 하나 |
| — | **`ChoiceGroup` 시트 신설** (선택지 86행) |
| 보상 = 타입 + 수치 | 보상 = 타입 + 수치 + **지속시간(초)** |

**코드 변화**

| 파일 | 무엇을 |
|---|---|
| `EventDefinitionSO.cs` | `EventKind`→`EventTrigger`(이름으로 판다) · `EventLine`→`EventChoice` · `eventScript` 한 칸 |
| `EventService.cs` | `wave_end` 판정 이동 · `habitat_contact` 신설 · 쿨타임 2회 · `repeatable` |
| `EventRewardService.cs` | **효과마다 남은 초**(`Tick`) · 체력% · 침식 4종 · 처치기록 부여 |
| `EventPanel.cs` | 한 창이 «본문+선택지» / «결과» **두 모습**을 낸다 |
| `Tools/gen_event_assets.py` | Ver013 두 시트를 읽는다 |

⚠⚠ **`wave_end` 의 뜻이 뒤집혔다.** Ver012 는 «웨이브 타이머 시작 시» 라 구현이
**전투 단계 진입**(`WavePhase.Battle`)에 발동했다. Ver013 표는 *"광폭화까지 모두 종료되어
정비 시간으로 넘어가는 프레임"* 이다 → `Battle|Enrage → Preparation` **전이**로 옮겼다.
★ **어디서 왔는지를 본다** — 판이 처음 시작될 때도 Preparation 이라, «들어왔다» 만 보면
0웨이브에 이벤트가 뜬다.

⚠⚠ **지속시간이 «초» 가 된 것이 가장 큰 변화다.** 옛 표는 «이벤트가 끝날 때까지» 라는
상대값이었고 `EndCurrent` 가 `ClearAll()` 로 한꺼번에 걷었다. 이제 효과마다 초가 적혀 있고
(`reward_duration_01/02`) **이벤트가 끝난 뒤에도 계속 흐른다**(웨이브 이벤트의 240초 =
«두 웨이브 분량»). → `EventRewardService.Tick()` 이 각자의 시각에 하나씩 되돌린다.
**창을 닫을 때 `ClearAll` 을 부르지 않는다** — 그러면 표가 적은 초가 전부 무의미해진다.
★ 대신 «판을 갈아엎을 때» 를 위해 `ClearRun()` 을 따로 뒀다.

★ `habitat_contact` 는 서식지 **중심에서 반지름+1타일** 로 «인접» 을 잰다 — 서식지가
(중심·반지름)으로 완전히 결정되므로(`NeutralHabitat`), 저장 코드가 «칸을 담지 않는» 것과
같은 이유다.
⚠ 소환수(골렘)는 세지 않는다 — «캐릭터가» 라는 표의 문장이고, 골렘이 먼저 닿으면
«내가 안 갔는데» 가 된다.

**검증** — 에셋 **43개**(wave_end 28 · private_timer 11 · habitat_contact 4) ·
선택지 **86행** · 경고 0건. `Info` 시트의 자동 집계(43/86)와 **정확히 일치**.

### 48-2. ★★ 아르세니아의 마법진이 «맵 장식» 으로 남았다

**씬에 있는 오브젝트가 아니었다.** 원화 guid 209개를 씬의 참조 294개와 대조해 확인했다 —
**0건**. 런타임에 만들어진 것이고, 원인이 **셋** 겹쳐 있었다:

① **그림과 실체의 수명이 두 벌이었다.** 「성스러운 축복」(80029)은
   `SacredZone.Spawn`(주인이 죽으면 사라진다) 과 `CombatProjectileFx.PlayArea(…, 8초)`
   (**자기 타이머로만** 사라진다) 를 **따로** 불렀다. 주인이 죽으면 공간만 없어지고
   **마법진은 남았다** — 실체 없는 그림, 즉 «장식물» 이다.
   → `PlayArea` 가 **취소용 손잡이**를 돌려주게 하고, `SacredZone` 이 그림의 주인이 되어
   `OnDestroy` 에서 지운다. 이 클래스가 이미 지키던 «걸었으면 되돌린다» 규칙에
   **그림**을 포함시킨 것이다. 수명이 한 벌이 된다.

② **정렬을 아무도 정하지 않았다.** `Spawn` 은 `anchor` 가 있을 때만 sorting 을 건다.
   캐릭터 스킬의 범위 연출은 **전부 `anchor: null`** 이라(맞는 쪽이 여럿이라 하나를 못 고른다)
   풀에서 꺼낸 오브젝트가 **지난번 레이어를 그대로** 들고 나오고, 새 것은 «기본 레이어·순서 0»
   즉 **타일맵 깊이**였다. → 기준이 없으면 **`Floor` 레이어 · 순서 10** 으로 못박는다.

   ⚠ 처음에 `Default` 레이어를 골랐다가 **되돌렸다** — 씬의 정렬 레이어를 실측해 보니
   순서가 `Default → Background → Floor → Object → Overhead → VFX → WorldUI` 이고
   타일맵 셋은 **바닥 = Floor(0) · 벽·데코 = Object(0) · 배경 = Background(0)** 이었다.
   `Default` 는 **맨 앞**이라 바닥 타일보다도 아래로 가서 **아예 안 보인다**.
   `Floor` 의 양수 순서가 «바닥 위 · 유닛 아래» 이고, 밟고 서는 마법진에는 그것이 맞다.

③ **씬을 넘겨도 살아남았다.** `~CombatProjectileFx` 는 `DontDestroyOnLoad` 다.
   «패배 → 다시하기» 로 새 판을 열면 **지난 판의 연출이 그대로 떠 있었다** —
   아르세니아가 없는 판에 아르세니아의 마법진이 남은 정확한 경로다.
   → `sceneLoaded` 에서 전부 치운다. `DamageNumberFx`(99-6절)가 *"폴백은 자기가 태어난
   씬과 생애를 같이 해야 한다"* 며 같은 문제를 고쳐 둔 것과 **같은 처리**다.

④ ★★ **판이 끝나면 `timeScale` 이 0 으로 고정돼 «굳는다».** 유저 추가 리포트:
   *"아르세니아 이펙트가 중앙 건물 청크에 걸려서 장식물처럼 안없어져"*.

   `Update` 는 `Time.deltaTime` 으로 센다 — 배속을 걸면 연출도 빨라져야 하므로 그것이 맞다.
   그런데 패배·승리는 `Time.timeScale = 0f` 를 **영구히** 걸므로:
   * 살아 있던 연출의 타이머가 **영원히 안 흐른다** → 화면에 **그대로 굳는다**
   * 그것을 지워 줄 `SacredZone` 도 `Time.time` 으로 세므로 **같이 굳는다** →
     `OnDestroy` 가 영영 안 돌고 ①의 취소도 **안 걸린다**

   패배는 **넥서스가 부서질 때** 일어나므로 굳은 그림은 정확히 «중앙 건물 자리» 에 남는다 —
   유저가 «중앙 건물 청크에 걸려서» 라고 본 그것이다.

   → `CombatProjectileFx.ClearAll()` 을 열고, `DefeatPanel`·`VictoryPanel` 이
   **`timeScale = 0` 을 걸기 전에** 부른다(순서가 중요하다 — 0 이 된 뒤에는 못 치운다).

   ★ **시간으로 우회하지 않았다**(`unscaledDeltaTime` 으로 바꾸는 것) — 그러면 **일시정지
   중에도 연출만 계속 흐른다**. 멈춘 화면에서 탄환이 혼자 날아가는 것은 더 이상하다.
   그래서 «판이 끝났다» 는 **사건**에 붙였다.

★ 끝나는 길을 `Retire()` **한 곳**으로 모았다 — 타이머 만료·취소·판 종료·씬 전환이 전부
같은 정리를 지나야 «풀에 안 돌아간 오브젝트» 가 안 생긴다.

### 48-3. ★★ 캐릭터가 죽으면 생성이 잠긴다 — **네 경로 중 하나만** 고쳐져 있었다

**원인** — «이미 등장한 인물» 집합(`CharacterDefinitionRegistry._spawned`)은 `static` 이라
**씬을 다시 열어도 살아남는다**. 비우는 `ResetStatics` 는 `RuntimeInitializeOnLoadMethod` 라
**프로세스마다 한 번**만 돈다. 인물 정의는 **11개**뿐이라 한 판에서 다 나오고 죽으면
집합이 꽉 차고 → `Pick` 이 null → `UnitSpawner` 가 **시작 캐릭터 3명까지** 생성을 취소 →
`OutOfCandidates` 가 **영구히** 참 → 「캐릭터 생성」이 그 프로세스가 끝날 때까지 죽는다.

⚠⚠ 에디터에서는 플레이 진입 때 **도메인 리로드가 우연히 비워 주고 있었다** — 그래서
빌드에서만, 또는 인게임 재시작 버튼으로만 재현되는 «안 죽는» 버그였다.

**고칠 자리가 넷이었고 하나만 돼 있었다:**

| 새 판을 시작하는 경로 | 예전 |
|---|---|
| 환경설정 ▸ 게임 재시작 | ✅ (UI-47 에서 고쳐 둔 것) |
| 패배 ▸ 다시하기 | ❌ 씬만 다시 열었다 |
| 승리 ▸ 다시하기 | ❌ 씬만 다시 열었다 |
| 로비 ▸ 새로하기 | ❌ 저장만 지웠다 |

→ 나머지 셋에 `ResetRun()` 을 **복사해 넣지 않았다**. 그러면 규칙이 네 벌이 되고 다음에
«새 판» 경로가 하나 더 생길 때 **또** 빠진다(이번이 정확히 그 일이다).
**`Save/RunResetService.cs` 신설** — «새 판을 시작한다» 의 도착 지점을 하나로 모았다.
★ **이어하기는 이 문을 지나지 않는다** — 불러온 판은 `MarkAppeared` 로 «등장했음» 을
되살리므로, 여기서 비우면 **같은 인물이 두 번 나오는** 판이 된다.
★ `NeutralKillTally.ResetRun()` 도 같은 구멍이었고 같은 문으로 함께 고쳐졌다.

**곁들여 고친 둘**

* `CharacterCreationService.AliveCount` 에 **소환수 제외**를 넣었다 — 골렘도 `CharacterUnit`
  이라 **정원 한 칸을 먹고 있었다**. 이 프로젝트의 다른 인원 집계 셋(`WaveManager`·
  `DefeatPanel`·`VictoryPanel`)은 이미 `!IsSummoned` 를 쓰고 있었고 **여기만** 빠져 있었다.
* `ActionPanel` 에 「등장할 인물 없음」 문구를 넣었다. ⚠ 예전에는 이 상태에서도
  «캐릭터 생성 170» 이 그대로 떠 있고 버튼만 회색이 됐다. 그리고 `interactable = false` 라
  클릭이 안 되므로 `TryCreate` 안의 설명 로그에 **도달할 방법이 없었다** — 즉 이 상태를
  유저에게 알리는 통로가 **하나도** 없었다.

### 48-4. 폴리르만 토벌 목록에 안 뜬다 — 판정이 «지금 보이는가» 였다

`EpicSubjugationService` 가 스스로 적어 둔 규칙은 «안개가 **걷힌** 자리에 있는 에픽» 인데,
코드는 `IsVisibleWorld`(«**지금** 누군가의 시야 안인가»)를 보고 있었다 — **다른 질문**이다.

**그런데 왜 폴리르만 문제였나** — UI-44 의 «종 기억»(`_knownSpecies`)이 그 어긋남을
**가려 주고 있었다**. 카르시노스·아니사킬·바리올라는 맵을 밝히던 시절에 한 번 시야에
들어와 기억에 남았고, 그 뒤로는 시야 검사를 **건너뛴다**. 폴리르는 **나중에 추가된 종**이라
기억이 없고, 태어나는 자리(넥서스에서 100~160칸)는 이미 안개가 걷혀 «아무도 보고 있지 않는»
곳이다 → 영원히 그 줄에서 걸렸다.

→ `FogOfWarService.IsExploredWorld()` 신설 + 판정 한 줄 교체.
★ 이제 **종을 가리지 않고** 규칙이 성립하므로 에픽이 또 늘어도 같은 구멍이 안 생긴다.
★ «종 기억» 은 **남겼다** — «아직 안 가 본 곳에서 다시 태어난» 개체를 여전히 잡아 주고
  세이브 호환(`subjugationKnownSpecies`)도 지킨다.
⚠ 에셋은 **한 칸도 안 고쳤다** — 폴리르 정의는 다른 에픽과 필드가 전부 같았다(대조 확인).

### 48-5. 시카리아 — 활 안쪽·다리 뒤 «흰 판때기»

`enclosed_background`(갇힌 배경 되돌리기)를 **이미 부르고 있었는데** 기본값
`(면적 300, 테두리 광도 120)` 이라 **면적에서 걸러졌다**. 실측:

| | 면적 | 테두리 광도 | |
|---|---:|---:|---|
| 활 안쪽 | 39~197px | 39~58 | **배경이다** |
| 다리 사이 | 53~250px | 25~40 | **배경이다** |
| 후드 정수리 | 44~81px | 104~156 | 그림이다 · 남아야 한다 |
| 날개-몸통 띠 | — | 67~88 | 그림이다 · 남아야 한다 |

⚠⚠ **면적만 낮추면 안 된다** — `(40, 120)` 으로 구워 보니 이동 9장 전부
**후드 정수리에 구멍이 뚫렸다**. → **테두리를 60 으로 조인 별도 통과**를 더해 **합집합**을 쓴다.
⚠ **기존 통과를 지우면 안 된다** — 근거리 6번(1220px·테두리 60.2)·8번(701px·103.6)은
테두리가 60 보다 밝아 새 통과로는 안 잡힌다. **둘 다** 필요하다.
★ 60/60 은 지어낸 값이 아니라 이 프로젝트의 표준이다 — `char_sheet.py` 의 기본값
`pocket=(60,60)` · `aru_golem_skin_build.py` 와 같다. 시카리아는 «옛 7명» 의 개별
스크립트라 그 값이 안 내려와 있었다.
⚠ **여유가 넓지 않다** — 가장 빡빡한 것(원거리 4번 아래 테두리 58.4)과 남겨야 할 것(62.0)이
60 을 사이에 두고 ±1.6 이다. 원화가 바뀌면 이 표를 다시 재야 한다.

**검증** — 프레임 수 동일(10/7/9/9/12/12/9) · 배율 전부 안전 범위(1.00~1.12) ·
자홍 합성 눈검사에서 활 안쪽·다리 뒤가 뚫리고 **후드 정수리와 근거리4 흰 초승달은 남았다** ·
`measure_skin_tiles.py` **변경 없음**(제거된 것이 전부 «안쪽» 이라 경계 상자가 안 바뀌었다) —
즉 **인게임 크기가 안 흔들린다**.

### 씬 변경 여부

**있음** — `UI_Root/HUD_Actions` 의 `sizeDelta` 366 → 300 (MCP 로 수정 · `save_scene` 1회).
⚠ 그 외에는 씬을 안 건드렸다. 아르세니아 마법진은 **씬 오브젝트가 아니었다**(48-2).

### 검증

`dotnet build` **오류 0 · 경고 0** · 유니티 `recompile_scripts` **경고 0** · 콘솔 에러 **0건** ·
이벤트 에셋 43개/선택지 86행(표의 자동 집계와 일치) · 시카리아 프레임 눈검사 ·
`gen_event_assets.py`·`set_body_scale_adjust.py`·`sicaria_skin_build.py` 전부 **멱등** 확인.

### 아직 확인 못 한 것 (유저가 볼 것)

1. ★ **이벤트 흐름을 플레이로** — 웨이브가 «완전히» 끝나는 순간에 뜨는지, 선택지를 누르면
   결과창으로 넘어가는지, 지속 효과가 표에 적힌 초만큼 남는지.
2. ★ **서식지 접촉 이벤트 4종** — 카르시노스/아니사킬/바리올라/폴리르 서식지에 처음 닿을 때.
3. **배경 이미지 14종이 아직 없다**(`bg_aftermath` 등). 표에 이름만 있고 그림이 없어
   지금은 글자만 뜬다 — 임시 UI 그대로다.
4. **캐릭터 11명 vs 상한 12** — 표의 인물이 11명뿐이라 `maxCharacters` 가 영원히 안 걸린다.
   48-3 으로 «판을 새로 시작하면 비워진다» 는 고쳤지만, 한 판 안에서 11명을 다 쓰면
   여전히 「등장할 인물 없음」이 맞다(이제 화면에 그렇게 뜬다).
5. 폴리르의 `PolyirHabitatEdge` 타일이 **없다** — 다른 에픽 셋은 있다. 서식지 테두리 고리가
   안 그려지고 바닥 타일로 폴백한다(연출 문제 · 이번 버그와 무관).
6. `Polyir_Template` 의 직렬화된 참조가 **바리올라를 가리킨다**(런타임에 표가 덮으므로
   지금은 무해하지만 씬을 읽는 사람을 오해시킨다).

### 씬반영요청 목록

- 없음 (씬 편집을 이 세션에서 직접 마쳤다)

---

## UI-49. ★★★ 스킨 전수 재측정 — 「짤림」 4건 · 옆 프레임 침범 · 발 기준 피벗 · 이펙트 기준을 «몸» 으로 (2026-08-22)

### 무엇을 / 왜

유저 리포트 넷 + 진행 중 추가 지시 셋:

1. 세라피엘 **이동 중 모션 짤림**
2. 엘리시아 **상하 모션 짤림**
3. **뱀 모양 보스**(= 아니사킬) 위아래 짤림
4. 시안 **이펙트 짤림**
5. *"신규 몬스터·캐릭터 위주로 스킨 균일성 검토 · 캐릭터 피벗 고정 후 **전방에 이펙트가 생성**
   되도록 로직 구현. 지금은 **캐릭터의 리소스 공간에 이펙트가 표현**되는 버그가 많다"*
6. *"이동 모션 사이 사이에 **전 동작 모션과 함께 짤려 들어가서** 어색해지는 부분들"*
7. *"**위 아래도 조금씩 짤리는** 이미지들이 발견된다"*
8. *"캐릭터가 **커졌다 작아졌다** 도 안하게 확실하게 분석해서 **피벗 맞추고 비율**이랑"*

### ★★ 가장 큰 것 — 「짤렸는지」를 <b>아무도 재고 있지 않았다</b>

`char_sheet.warn_if_clipped` 가 그 일을 하는데, 그것은 **`char_sheet` 를 쓰는 다섯 스크립트에만**
있었다. 세라피엘·엘리시아·시안·레기미아는 자기 좌표를 직접 들고 있어(bespoke) **검사가 하나도
없었다** — 유저 리포트 넷이 정확히 그 넷이다. 규칙이 두 벌이라 생긴 사고다.

그래서 검사를 **모든 스크립트가 반드시 지나는 자리**에 옮겨 심었다.

| 어디 | 무엇 |
|---|---|
| `skin_sheet.audit_boxes` | `boxes_for`·`boxes_dominant` <b>안에서</b> 잘림을 잰다 — 밴드 좌표와 마스크가 만나는 단 두 곳이다 |
| 판정 | «같은 열에서 잉크가 경계를 **넘어 이어지는가**» — 제목·번호 줄에는 안 걸리고 진짜 잘림은 반드시 걸린다 |
| `Tools/check_frame_edges.py` | **구워진 PNG 쪽**에서 «테두리 한 줄이 얼마나 불투명한가» 로 본다(잘린 단면은 꽉 차고 자연스러운 끝은 안티에일리어싱뿐) |

⚠ 몸통 줄의 «아래» 는 <b>세지 않는다</b> — 피벗이 발밑이라 서 있는 캐릭터는 바닥 한 줄이
꽉 찬 것이 정상이다. 그걸 세면 진짜 잘림이 묻힌다(실측: 76건 중 50건이 그것이었다).

### 시트별로 무엇이 잘못돼 있었나 (전부 실측)

**세라피엘** — 셋이 겹쳐 있었다.
* 이동 줄은 **10장**인데 `x0=480` 이라 **첫 장(x345~466)을 통째로 버리고** 아홉 장을
  «폭÷9» 로 갈랐다. 간격이 고르지 않아(틈 4~18px) 칸이 밀리고 **날개를 잘랐다**.
* 그 밀림을 `CELL_INSET={"Move":4}`(칸을 양쪽에서 깎기)로 덮고 있었다 — 검사가
  아홉 장 중 <b>일곱 장</b>에서 «오른쪽 18~45px 이 이어진다» 고 잡았다. **실제로 자르고 있었다.**
* 제목 딱지를 «밴드를 내려서» 피하느라 **머리 위 11~47px** 을 잃고 있었다.

→ **딱지를 지운다**(아래 신설) · 밴드를 그림 그대로 · 칸은 **발밑**으로 가른다
  (`cells_by_feet` — 근거리+원거리 한 줄이 정확히 13칸(7+6)으로 갈린다).
  이펙트 상자 **x 를 넷 다 다시 쟀다**(총구 이펙트는 옛 좌표 295~566, 실측 461~653 —
  총알 상자의 오른쪽 끝과 총구의 왼쪽 절반이 섞여 구워지고 있었다).

**엘리시아** — 라벨도 구획선도 딱지도 없는 시트라 옛 좌표가 «격자 이미지를 눈으로 읽은» 값이었고
**열네 줄 중 열세 줄**이 어긋나 있었다. 잉크 프로파일의 **국소 최소**로 다시 쟀다.
회복은 **아래 20px(발밑 빛 고리)**, 스킬1은 **아래 43px(네 날개 아랫깃)** 을 잃고 있었고,
몸통 단 `x1` 이 860 이라 **오른쪽 끝 프레임의 방패·날개**(실측 x878~914)가 잘렸다.
이펙트 단은 **일곱 줄이 전부** 밀려 있었다.

**시안** — 이펙트 구획이 **반투명 흰 판** 위에 그려져 있다(실측 RGB 245,245,247 · **알파 181~205**).
`ALPHA_INK_MIN` 이 8 이라 그 판이 전부 «그림» 으로 잡혀 **불투명한 흰 사각형**이 프레임마다
함께 구워졌다 — 회복 이펙트는 거의 흰 판이고 이펙트가 그 안에 묻혀 «짤린» 것처럼 보였다.
그리고 마법 좌 줄은 **위 19px**, 근거리 우는 **아래 14px**, 회복 좌는 **위 40px** 을 잃고 있었다.

**아니사킬(뱀 보스)** — 프레임 상자를 **채도 마스크**로 잡는데 이 개체는 **등만 보라색으로 빛나고
배는 거의 검다**. 검은 배가 문턱을 못 넘어 상자에서 빠져 **배가 평평하게 잘린 벌레**가 구워졌다
(실측: 몸 y422~510 · 채도 상자 y423~495 = **아래 15px 손실**).
→ «패널색이 아닌 픽셀» 로 **밴드에서 위·아래로 이어서 넓힌다**(`grow_to_body`).
규칙 하나다: <b>한 줄이 칸 폭의 6% 이상 불투명하면 몸이다</b>(번호 글자는 1.8~5.3%라 안 걸린다).

### 신설한 공통 부품 (`skin_sheet.py`)

| 함수 | 무엇을 / 왜 |
|---|---|
| `audit_boxes` · `clipped_edges` | 위 ★★ |
| `pill_mask` · `erase_title_pills` | **제목 딱지**를 좌표 없이 **재서** 지운다. 근거 둘: 가로로 긴 어두운 줄(실측 138~318px · 그림은 최대 79px) + **얇다**(27~33px) |
| `panel_mask` · `erase_panels` · `sweep_panel_residue` | **반투명 판때기**를 색·알파 키로 찾아 지운다. 판이 여러 겹인 시트가 있어 `passes` 로 돈다 |
| `grow_box_vertical` · `_grow_all` | 밴드가 «조금 자른 것» 을 프레임마다 되찾는다(잉크가 이어지는 동안만) |
| `components` · `drop_stray_parts` | **옆 프레임에서 들어온 떠 있는 조각**을 뗀다 |
| `foot_center` · `plant_feet` | **발을 피벗에 맞춘다** |

⚠ 딱지 찾기는 <b>두 번 고쳤다</b>. ① «위·아래 테두리를 세로로 닫기» 는 아래 테두리가 글자
내림꼴에 끊겨 짧으면 실패한다(시안 「대기 모션」). ② «어두운 곳으로 번지기» 는 **인물이 검은**
시트에서 그림으로 새어 전부 버려진다(시안). → **«그 알약 폭에서 어두운 비율» 로 넓힌다**
(알약은 언제나 절반 이상 어둡고, 그림은 그 폭에서 그렇게까지 채워지지 않는다).

### ★★ 옆 프레임 침범 (유저 지시 6)

`boxes_dominant` 은 침범을 **열(가로)로만** 걸러낸다. 남의 날개가 이 칸 몸통과 **x 가 겹치는
높이**(머리 위·발 아래)에 있으면 열로는 못 가르고 **떠 있는 조각**으로 함께 구워진다.
재생하면 그 조각만 나타났다 사라져 «전 동작이 끼어드는» 것으로 보인다.

→ `drop_stray_parts` 가 **2차원 이어짐**으로 한 번 더 본다: 가장 큰 덩어리(=몸)에 **붙어 있지
않고** 멀면 남의 것이다. ⚠ 거리를 «가로 간격» 으로 재면 안 된다 — 아니사킬 이동 4·5번은 옆
개체의 머리가 몸통 **아래·옆**에 들어와 x 가 겹쳐 «간격 0» 이었다. **몸을 부풀려 닿는지** 본다.

⚠ 크기만으로 자르지 않는다 — 엘리시아 근접의 금색 궤적·세라피엘의 총구 화염은 몸에서
떨어져 있지만 **이 프레임의 그림**이다. 그래서 «가깝다» 를 먼저 본다.

**시안은 칸 가르기도 바꿨다** — 이 시트는 프레임 간격이 고르지 않아(대기 왼쪽 줄 몸통 중심
간격 62~110px) «폭÷장수» 가 최대 45px 어긋났다. **발밑으로 먼저 갈라 보고**, 장수가 안 맞는
줄만 «폭÷장수» 로 되돌린다. 그리고 **대기는 좌 8장 · 우 7장**이다(발 덩어리 폭이 52~56px 로
전부 같아 «합쳐진 것 없음» 이 확인된다) — 옛 코드는 양쪽 다 8로 두고 있었다.

### ★★ 피벗 — 발을 기준으로 (유저 지시 8)

`body_anchor`(세로로 두꺼운 열의 가운데)는 **한 묶음 안에서는** 훌륭하지만 **묶음끼리 어긋난다**:
총을 앞으로 뻗는 원거리 줄은 총이 «두꺼운 열» 에 들어가 기준이 통째로 밀린다. 실측(발 중심 − 피벗):

    시안   대기 −13.5 px vs 회복 +5.5 px → 모션이 바뀌면 19px 미끄러진다
    엘리시아 이동 +9.5 vs 원거리 −9.0    → 18px
    아루   원거리 +10.0 vs 이동 −9.2     → 19px

→ `plant_feet` 가 **묶음마다 한 값**만 민다(프레임마다 맞추면 걷는 다리 놀림이 지워진다).
분해 스크립트가 있는 **23벌**에 배선했다.

**분해 스크립트가 없는 옛 팩 여덟 벌**(단타리안 42px · 히스톤 23 · 피올로 23 · 종양거미 20 ·
카시노마 15 · 비기오르 9 · 헬팽 9 · 영혼궁수 6)은 원화가 없어 다시 구울 수 없다 →
`Tools/align_skin_pivots.py` 신설: **구워진 PNG 를 다시 얹는다**. 방향마다 따로 재고, `.meta` 는
건드리지 않는다(이 프로젝트의 프레임 메타에는 rect 가 없다). **멱등**이다.
⚠ 밀 양은 «여백을 걷어낸 뒤» 다시 재야 한다 — 원본 캔버스에서 잰 값을 쓰면 한 번 맞춘 뒤
다시 재면 더 어긋나 있었다(영혼궁수 +6.5 → −9.0). 멱등이 깨진 것을 보고 잡았다.

**결과 — 전 캐릭터 31벌의 «모션 간 발 어긋남» 이 최대 42px 에서 2px 이하로.**

### ★★ 이펙트의 기준을 「몸」 으로 (유저 지시 5) — `CombatProjectileFx.cs`

두 자리가 **SpriteRenderer 의 bounds**(= 지금 그려지는 **프레임 캔버스**)를 기준으로 삼고 있었다:

    CenterOf()      → sr.bounds.center      «몸 중심» 이라 적혀 있지만 실제로는 캔버스 중심
    MuzzleOffset()  → sr.bounds.extents.x   «몸 반지름» 이 아니라 캔버스 반폭

그런데 이 프로젝트의 프레임 캔버스는 **이펙트까지 담고 있다**(실측: 엘리시아 스킬1 캔버스 268px
안에 몸통 91px). 그래서 ① 캔버스가 넓은 프레임에서 중심이 **몸 밖으로** 밀리거나 연출이 **몸 안**
에서 시작하고 ② **프레임이 바뀔 때마다 기준이 흔들리고** ③ 원화를 고치면 연출 위치가 따라 흔들렸다.
유저가 «캐릭터의 리소스 공간에 이펙트가 표현» 이라고 본 것이 이것이다.

→ `BodyBox()` 신설 — `CharacterAnimator.RenderedSizeTiles`(= **대기 원화의 알파 경계**를 재서
배율을 먹인 값 · 캔버스 여백과 이펙트를 뺀 «몸») 를 읽는다. 포탑도 같은 이름의 칸이 있다.
실측값이 없는 옛 프리팹만 예전처럼 bounds 로 내려간다.

* `MuzzleForwardRatio` **0.45 → 1.0** — 이제 **몸** 반폭 기준이라 1.0 이 정확히 «실루엣 경계» 다
* `MuzzleClearTiles = 0.2` 신설 — 경계에서 **더** 밀어 «캐릭터의 공간과 분리» 시킨다
* 신규 셋의 `_skin_spec.txt` 에 `meleeTravelWidthTiles`·`impactWidthTiles` 를 **타일로 못박았다**
  (비워 두면 구워진 픽셀 크기가 그대로 화면 크기가 되어 원화를 다시 자를 때마다 연출이 커졌다 작아졌다)

### 곁들여 고친 것

* **볼트 원화 경로** — 유저가 낱장 원화를 `리소스/asset/` → `리소스/sprites/` 로 옮겨서
  아니사킬·카르시노스·고르도네·말파스 **넷이 «원본이 없습니다» 로 죽어 있었다**(다시 구울 수
  없는 상태). `vault_path.find_art()` 신설 — 찾는 규칙을 한 곳에 둔다.
* `sheet_probe.py --cells` — 칸을 세는 창과 «잘렸는지» 재는 창을 따로 두고 `bounds=` 를 찍어 준다.
* `check_frame_edges.py --size` — 모션마다 «몸» 이 대기와 같은 크기로 그려졌는지 재서 알린다.
* 폴리르 제목 딱지 상자 12개가 **전부 2~3px 씩 작아** 가로 선 한 줄이 남아 있었다(투사체 1·2번의
  «위 99% 불투명» 이 그것) → 사방으로 넓혔다.
* `char_sheet` 가 `_skin_spec.txt` 첫 줄에 캐릭터 이름을 적어 «세라피엘 가 만든 파일» 이 되던 것.

### 검증

* `dotnet build` **오류 0 · 경고 0**
* **테두리 검사** — 몸통 줄의 잘림은 다시 구운 23벌에서 사실상 사라졌다(남은 43건은 대부분
  «이펙트 팔레트에서 부품끼리 붙어 있어 어디를 잘라도 걸리는» 자리다 · 목록은 도구가 찍는다)
* **피벗 검사** — 31벌 전부 2px 이하. `align_skin_pivots.py` 재실행 시 «고칠 것 0장»(멱등)
* 눈검사 — 세라피엘 이동 10장 · 엘리시아 이동/근접 · 시안 이동/대기 · 아니사킬 이동 6장을
  자홍 배경에 얹어 프레임 경계선과 함께 확인

### 씬 변경 여부

**없음** — 이번 작업은 원화 분해 스크립트 · 구워진 PNG · `CombatProjectileFx.cs` 뿐이다.

### 아직 확인 못 한 것 (유저가 볼 것)

1. ★★ **유니티에서 스킨 에셋을 다시 만들어야 한다** — 메뉴 `LastSanctuary/스킨/원화 폴더로
   스킨 에셋 만들기` → 그 다음 `python Tools/measure_skin_tiles.py`.
   프레임 장수가 바뀐 곳이 있다(세라피엘 이동 9→**10** · 시안 대기 우 8→**7** · 투사체 8→**6**).
2. ★ **인게임에서 «전방 이펙트»** — 평타의 총구 섬광·탄환이 몸 실루엣 **밖**에서 시작하는지.
   `MuzzleClearTiles`(0.2타일)가 모자라거나 과하면 그 상수 하나만 고치면 된다.
3. 이펙트 팔레트에서 부품끼리 붙어 있는 43건 — 원화를 고치지 않는 한 «어디를 잘라도 조금 걸리는»
   자리다. 눈에 거슬리는 것이 있으면 그 줄만 짚어 줄 것.
4. 카이론 스킬1 은 보호막 구체가 **줄 경계까지 내려와** 아래가 평평하다(원화가 그렇다).

### 씬반영요청 목록

- 없음

---

## UI-50. ★★★ 유물 시스템 신설 — 표 Ver01 · 발굴 · 드랍 · 장착 · 유물 관리 창 (2026-08-23)

### 무엇을 / 왜

유저 지시 11항목. 요약하면 **«몸이 스스로 만들어낸 면역 반응의 흔적»을 모아 캐릭터에게
하나씩 끼우는 시스템**이다. 얻는 길은 셋 — 발굴 · 일반 몹 · 보스.

### ★★ 순서를 지켰다 — «표 먼저, 데이터 적용은 그다음»

유저 지시 8번: *"일단 테이블 반드시 제작 후에 인포에 상세한 설명 넣어놓고 데이터 적용"*.

`<볼트>/데이터 테이블/**Last_Sanctuary_유물테이블_Ver01.xlsx**` — 시트 여섯 장.

| 시트 | 무엇 |
|---|---|
| `Info` | 구조·규약·ID 규칙·밸런스 근거 (78줄) |
| `Grade` | 등급 3종 — 색·세기 대역·성격 |
| `EffectType` | **효과 enum 21종** 사전. 적용은 코드가 한다 |
| `Relic` | **유물 31종** — 일반 11 · 레어 8 · 에픽 12 |
| `DigOutcome` | 발굴 결과 10종 (가중치 합 **100** → 그대로 %) |
| `Drop` | 처치 드랍 확률 6줄 |

★ **표를 스크립트가 쓴다**(`Tools/gen_relic_table.py`) — 이벤트 테이블 Ver013 과 같은 방식이다.
손으로 만든 xlsx 는 다시 만들 수 없어서, 컬럼 하나를 더하거나 밸런스를 한 번에 조정할 때
사람이 30행을 다시 두드려야 한다.

⚠ **스트링 키를 쓰지 않았다** — 유저 지시 6번대로 **한국어 원문**을 그대로 담았다.
검토가 끝나면 `스트링 키 테이블.xlsx` 로 옮기면 되고, 그때 고칠 곳은 생성 스크립트 한 곳이다.

### 이름 — 면역 반응 (유저 지시)

이 프로젝트의 은유 축은 이미 정해져 있다(이벤트 테이블 Info): **성역 = 신체 · 넥서스 = 심장 ·
천사 = 백혈구 · 웨이브 = 감염 · 중립 = 종양/기생체**. 유물은 그 축 위에 **몸이 스스로 만든
흔적**으로 놓았다.

* **일반** = 비특이 면역 — 굳은 딱지 · 오른 열 · 부어오른 자리 · 붉은 실 · 마른 각질 ·
  삼킨 티끌 · 곤두선 솜털 · 저린 손끝 · 맑은 진물 · 첫 재채기 · 들뜬 손톱
* **레어** = 특이 면역 — 항체의 낙인 · 보체의 사슬 · 인터페론 결정 · 비만세포 주머니 ·
  굶주린 대식세포 · 기억 세포 · 두꺼워진 가피 · 서늘한 해열
* **에픽** = **침입자에게서 빼앗은 것** — 보스 10종 고유(형상을 잊은 핵 · 구속의 인장 ·
  유혹하는 피주머니 · 비명을 삼킨 성대 · 잿빛 담뱃대 · 증식하는 촉수 · 검은 숲의 홀씨 ·
  삼킨 것의 이빨 · 얼금뱅이 뿔 · 영원한 숙적의 눈) + 발굴 전용 둘(태초의 골수 · 흉선의 씨앗)

### 효과 — 「타입 + 밸류」 (유저 지시 7번)

이벤트 보상 타입(`RewardType` 시트)과 **같은 규약**이다. 다른 점은 하나뿐:

```
이벤트 보상 : 전원에게 · 몇 초 동안        → duration 칸이 있다
유물        : 장착한 한 명에게 · 장착한 동안 → duration 칸이 없다
```

`RelicEffectService` 가 **두 갈래**로 처리한다:

* **능력치 계열 12종** — 장착하는 «순간» 고정 수치를 더하고 벗을 때 같은 수치를 뺀다
  (`CharacterUnit.AddFlatStatBonus`). ⚠ 비율을 그때그때 다시 계산하면 그 사이에 강화가
  끼어들었을 때 뺀 값이 더한 값과 달라져 **능력치가 영구히 어긋난다** — 이벤트 보상의
  `ApplyStat` 이 같은 이유로 같은 방식이다.
* **반응 계열 9종** — 흡혈·반사·처치 보상·부활은 «상태» 가 아니라 «사건» 이라 걸어둘 곳이 없다.
  `DamageableUnit.OnAnyDamaged` / `OnAnyDied` 에 붙는다.
  ⚠ 도메인 리로드를 끄고 쓰는 프로젝트라 **반드시 뗀다**(`Unhook`) — 안 떼면 다음 판에 두 번 걸린다.

⚠ **「두꺼워진 가피」(체력 문턱형)만 주기 갱신이 필요하다.** 그 하나 때문에 `MonoBehaviour` 를
새로 두지 않고, 이미 매 프레임 도는 `RelicDigService.Update` 에 `RelicEffectService.Tick()` 을 얹었다.

### 발굴 — 건설과 **같은 구조** (유저 지시 3번)

유저가 *"기존에 삭제된 건설처럼"* 이라고 못박았고, 실제로 필요한 것이 같다:
«자리 목록 · 한 자리에 한 명 · 걸어가서 시간을 채운다 · 진행도를 화면에 겹쳐 그린다».
그래서 `BuildService` 의 구조를 그대로 옮겼다(`AssignedSiteFor` → `CharacterBehavior.TryExcavate`
→ `Contribute`). `CharacterDuty` 에 `Dig` 를 더했다.

| 값 | 기본 | 근거 |
|---|---|---|
| 발굴 가능 칸 | **24** | 맵이 넓어 한 판에 다 밝히기 어렵고, 이 정도면 «탐험하다 가끔 만난다» 가 된다. 너무 많으면 발굴이 주 수입원이 되어 **웨이브를 미루는 것이 최적 전략**이 된다 |
| 발굴 시간 | **15초** | 유저 지시 |
| 넥서스에서 최소 거리 | 14타일 | 시작하자마자 다 캐지 않게 |
| 칸끼리 최소 간격 | 10타일 | 몰리면 «한 번 가서 다 캔다» 가 된다 |

전부 인스펙터에서 고친다(유저 지시 2번: *"에딧에서 수정 가능하게"*).

★ **다른 점 하나 — 클릭을 «월드» 가 아니라 «UI» 로 받는다.** 건설은 «배치 모드» 로 들어가
맵을 클릭하지만 발굴은 모드가 없고 **칸에 뜬 느낌표**를 직접 누른다. 월드 클릭으로 받으면
`UnitSelector`·집결지와 **같은 클릭을 두고 다툰다** — 느낌표를 UI 버튼으로 두면 그쪽들이 이미
«포인터가 UI 위면 무시» 하므로 다툼이 아예 생기지 않는다.

★ **«보인다» 의 판정** — 안개가 걷힌 것만으로는 안 된다. 유저 지시가 «캐릭터의 **시야**에
보일 경우» 라 `FogOfWarService.IsVisible` 을 본다. ⚠ 한 번 본 칸은 계속 보인다(기억) —
시야를 벗어날 때마다 느낌표가 사라지면 «분명 봤는데 없어졌다» 가 된다.

★ 자리는 `Start` 에서 고른다 — 맵을 `MapGenerator.Awake` 가 **판마다 새 씨앗으로** 다시
만들고, 유니티가 «모든 Awake → 모든 Start» 를 보장하므로 그때는 지형이 확정돼 있다
(안개·흐름장·스포너가 전부 같은 이유로 `Start` 에서 맵을 읽는다).

### 드랍 (유저 지시 4·5번)

| 처치 대상 | 등급 | 확률 |
|---|---|---|
| 웨이브 일반 · 중립 일반 | 일반 → (실패 시) 레어 | 1.2% → 0.3% |
| 에픽 중립 보스 1101~1104 | **그 보스의 고유 에픽** | 20% |
| 웨이브 보스 120001~120006 | **그 보스의 고유 에픽** | 35% |

⚠ 일반·레어를 **차례로** 굴린다 — 순서를 뒤바꾸면 «레어가 일반보다 흔해» 진다.
⚠ **캐릭터가 죽인 것만** 센다(`LastAttacker`) — 중립끼리 싸우다 죽은 것은 제외.
⚠ 에픽은 **발굴 전용 둘을 빼면 보스로만** 나온다 — 등록기가 풀 자체를 갈라 둔다.

### 장착 · UI (유저 지시 9·10번)

* `RelicInventory` — 보유(ID→개수)와 장착(**캐릭터 정의 ID**→유물 ID)을 든다.
  ⚠ 열쇠가 인스턴스가 아니라 **정의 ID** 인 이유: 캐릭터는 죽고 다시 나고, 세이브를 거치면
  인스턴스가 통째로 바뀐다 — 정의 ID 라야 «같은 인물» 이 이어진다.
* 규약 — 한 명당 하나 · 같은 유물을 두 명이 나눠 낄 수 없다 · 소환수 불가 ·
  **캐릭터가 죽어도 유물은 보관함으로 돌아온다**.
* `HUD_Relics`(유물 관리 창) — 왼쪽 목록(아이콘·이름·개수), 오른쪽 상세(효과·서사·출처·착용자)
  와 장착/해제 버튼. 토벌 창과 같은 API·같은 배타 처리(`HudExclusive`).
* `HUD_Actions/Buttons/RelicButton` — «유물 관리 (발굴 N)». 토벌 버튼이 «발견한 에픽 수» 를
  보여주는 것과 같은 규칙이다: 창을 열지 않아도 «지금 할 일이 있는가» 를 알 수 있어야 한다.
* `HUD_Growth/Stats/RelicSlot` — 낀 유물을 보여주고, 누르면 유물 관리 창을 연다.
  ⚠ **고르는 일은 그쪽 한 곳에서만** 한다 — 두 곳에서 구현하면 규칙이 두 벌이 된다.

### 씬 오브젝트는 전부 MCP 로 (유저 지시)

`Tools/mcp_build_relic_ui.py` — **요청 108건을 한 배치**로 보낸다.
손으로 하나씩 부르면 ① 중간에 끊겼을 때 어디까지 했는지 모르고 ② 다시 만들 수 없다.
`update_gameobject`/`update_component` 가 «없으면 만들고 있으면 고친다» 라 **멱등**하다.

⚠⚠ **TMP 의 «있을 것 같은» 칸을 넣으면 요청 전체가 거절된다.** 이 버전에는
`m_enableWordWrapping`(→ `m_TextWrappingMode`) 도 `m_raycastTarget` 도 없다. 하나라도 섞으면
브리지가 그 요청을 통째로 버려서 **글자·크기·색까지 조용히 안 들어간다**(실측 16건이 그랬다).
⚠ 정렬은 **이름**으로 준다(`"Left"`) — 브리지가 enum 을 **인덱스**로 해석해서 TMP 의 실제
값(513)을 넘기면 «Enum index out of range» 로 죽는다.

### 임시 아이콘 (유저 지시 11번)

`gen_relic_assets.py` 가 **등급 색 바탕 + 유물마다 다른 문양**을 그린다(31장).
문양은 유물 ID 로 정하므로 다시 돌려도 같은 그림이다. 원화가 오면 **같은 파일 이름으로
덮으면** 된다 — 표의 `icon` 칸이 그 이름이다.

### 세이브

`SaveData.CurrentVersion` **2 → 3**. 보유·장착·발굴 칸 셋을 담는다.
⚠ 판을 올려 옛 세이브를 거부하는 것이 맞다 — 그대로 읽으면 **유물을 다 잃은 채** 이어진다.
⚠ 발굴 칸을 저장하지 않으면 이어할 때마다 자리가 바뀌어 «가던 캐릭터가 허공을 판다».
`RunResetService` 에도 «새 판이면 유물을 비운다» 를 넣었다(이어하기는 그 문을 지나지 않는다).

### 검증

* `dotnet build` **오류 0 · 경고 0** · 유니티 `recompile_scripts` **경고 0**
* MCP 요청 **108건 전부 성공** · 씬 저장 1회
* 에셋 — 유물 31개 · 임시 아이콘 31장 · 발굴표 1개 생성 확인
* 하이라키 확인 — `DigOverlay/DigMarkerTemplate/Label` · `HUD_Relics`(목록·상세 17개 자식) ·
  `HUD_Actions/Buttons/RelicButton` · `HUD_Growth/Stats/RelicSlot`

### 씬 변경 여부

**있음** — 위 네 갈래를 MCP 로 새로 만들고 `GameSystems` 에 `RelicInventory`·`RelicDigService`
를 붙였다. `save_scene` 1회.

### 아직 확인 못 한 것 (유저가 볼 것)

1. ★★ **플레이로 한 바퀴** — 탐험하다 느낌표가 뜨는지 · 누르면 가장 가까운 캐릭터가 가는지 ·
   15초 뒤 결과가 로그에 뜨는지 · 유물 창에 쌓이는지 · 장착하면 능력치가 오르는지.
2. ★ **레이아웃** — 창 크기(760x480)와 칸 위치는 값으로 잡은 것이라 실제 화면에서 넘치거나
   좁을 수 있다. 어긋나면 `Tools/mcp_build_relic_ui.py` 의 좌표만 고쳐 다시 돌리면 된다(멱등).
3. **밸런스** — 발굴 24칸 · 결과 확률 · 드랍 1.2/0.3/20/35% 는 전부 표의 값이다.
   플레이해 보고 조정할 것(표를 고치고 `gen_relic_table.py` → `gen_relic_assets.py`).
4. **스트링 키 이관** — 유물 이름·설명·서사가 아직 표의 원문이다(유저 검토 대기).
5. 발굴 표식은 **노란 사각형 + 「!」** 다(임시). 원화가 오면 `DigMarkerTemplate` 의 Image 에
   스프라이트만 넣으면 된다.

### 씬반영요청 목록

- 없음 (씬 편집을 이 세션에서 MCP 로 마쳤다)

---

## UI-51. 유물 아이콘 연동 · 유물 UI 폰트 · 허드 액션 확장 · 레기미아 30웨이브 · 로비 클릭/스킵 · 이벤트 등장 연출 · ★★ 신규 3인이 표에서 사라진 것 복구 (2026-08-24)

### 무엇을 / 왜

유저 지시 다섯 + 중간 추가 둘:

1. *"유물 아이콘들 뽑아 놨으니까 연동 / 유물 ui 네오둥근모로 변경 / 허드 액션 확장"*
2. *"30웨이브에 등장하는 레기미아 스킨이 연동되어 있지 않음 단탈리온으로 나옴"*
3. *"시작 로비 화면 버튼 등장시 지금 버튼 있는 부분 누르면 바로 들어가게 하지 말고 직접적으로
   버튼 클릭할 시에 들어갈 수 있게 … 연출 나올때 화면 클릭하면 연출 스킵"*
4. *"이벤트 등장 시 ui 등장에 페이드 인 / 떠오르기 효과 추가"*
5. *"캐릭터 시트에 마지막 캐릭터 2인이 삭제되었는데 테이블에 추가하고 캐릭터 테이블 스트링 키 연동"*
   (+ 중간 추가) *"세라피엘이랑 시안 스킬 아이콘도 추가해줘 안 나오네"* ·
   *"캐릭터 시트 복구할때는 진행상황.md 확인해보고 복구 / 시안 스킬 적용 되는건지 확인"*

6번 지시(듀토리얼)는 **계획만** 세웠다 — 아래 「UI-51-8」.

**신규 스크립트** — [Tools/relic_icon_build.py](Tools/relic_icon_build.py) ·
[Tools/table_restore_20260824_new_characters.py](Tools/table_restore_20260824_new_characters.py)
**고친 C#** — `UI/LobbyPanel.cs` · `UI/EventPanel.cs`
**씬 변경** — 있음(HUD_Actions 크기 · TMP 폰트 16개) · `save_scene` 1회

---

### UI-51-1. 유물 아이콘 31장 — 원화에서 잘라 <b>덮어썼다</b>

볼트에 `리소스/sprites/Lelic_icon_01~03.png` (각 1254x1254 · **6x6 = 36칸** · 합 108칸)이
들어와 있었다. 그중 **31칸**을 골라 `Resources/RelicIcons/*.png` 를 갈아끼웠다.

★★ **`.meta` 를 손대지 않았다.** 유물 에셋(`Resources/Relics/Relic_*.asset`)이 아이콘을
  `icon: {fileID: 21300000, guid: …}` 로 **직접 참조**한다(UI-50 의 `gen_relic_assets.py` 가
  그렇게 썼다). PNG **픽셀만** 바꾸면 참조가 그대로 산다 — `.meta` 를 다시 쓰면 guid 가 바뀌어
  **31개 참조가 전부 끊긴다**. 실측 확인: `git status` 에 **PNG 31개만** 뜨고 `.meta` 는 0개.

★ **격자를 픽셀로 세지 않았다** — 시트마다 간격이 다르다(01/02 는 약 203px · 03 은 약 205px,
  시작 여백도 3~16px 로 제각각). 행·열 평균 밝기가 2.0 미만인 **새까만 고랑**을 찾아 그
  가운데를 자른다(`--probe` 로 언제든 다시 잰다). 칸이 정사각이 아니라(199~211 x 199~205)
  **긴 쪽에 맞춰 검은 여백을 채워** 비율을 지킨 채 128x128 로 굽는다 — 늘리면 액자가 찌그러진다.

★ **액자(테두리)를 떼지 않았다** — 원화가 액자를 포함해 그려져 있어서 떼면 아이콘마다 여백이
  제각각이 된다. 액자가 있어야 목록에서 줄이 고르다.

배정 근거는 `PICK` 표의 주석에 한 줄씩 적어 두었다(이름·서사에 맞췄다). 예 —
「태초의 골수」 ← 피가 밴 뼈 · 「구속의 인장」 ← 피 묻은 족쇄 · 「삼킨 것의 이빨」 ← 이빨 줄 ·
「영원한 숙적의 눈」 ← 감기지 않은 눈. 바꾸려면 그 표의 (시트, 행, 열)만 고치고 다시 돌린다.

### UI-51-2. ⚠ 유물 UI 가 <b>TMP 기본 폰트</b>였다 — 16개

씬의 `m_fontAsset` 을 세어 보니 **16개가 기본 폰트**(LiberationSans)였다 — UI-50 이 MCP 로
만든 유물 UI 의 글자 전부다. `m_fontAsset` 은 에셋 **참조**라 MCP 로 넣을 수 없고
(진행상황 8절 4번), 그래서 태어날 때부터 기본 폰트였다.

→ 유니티 메뉴 **`LastSanctuary/폰트/네오 둥근모 TMP 에셋 굽고 씬에 적용`** 한 번.
  그 메뉴는 «에셋이 이미 있으면 재사용» 하고 «폰트가 다른 것만» 갈아끼우므로 안전하다
  (진행상황 8절이 적어둔 2026-08-13 사고의 재발 방지 장치가 이미 들어 있다).
  결과: **232개 전부** `NeoDunggeunmo SDF`(guid `c9323a04…`) 하나로 통일.

⚠ `Tools/mcp_build_relic_ui.py` 에 **«돌린 뒤 이 메뉴를 실행할 것»** 주석을 박아 두었다 —
  그 스크립트는 멱등이라 다시 돌리기 쉽고, 다시 돌리면 새 글자가 또 기본 폰트로 태어난다.

### UI-51-3. 허드 액션 — 버튼 8개가 <b>칸 밖으로 넘치고 있었다</b>

`HUD_Actions` 는 260x300 인데 안쪽 `Buttons`(VerticalLayoutGroup · spacing 8)에 버튼이
**8개**다(생성·부대·전술·강화·건설·토벌·환경설정 + UI-50 이 더한 **유물 관리**).

```
필요:    8 x 40 + 7 x 8 = 376px
있던 것: 300 − 20(안쪽 여백) = 280px      → 96px 초과
```

→ **높이 300 → 396**(=376+20). 아래에 있는 `HUD_Minimap` 은 바닥 기준 y16 · 높이 322 라
  위쪽 경계가 −742 다. 확장 후 바닥이 −476 이므로 **266px 여유**가 남는다(겹치지 않는다).

### UI-51-4. ★★ 30웨이브 보스가 <b>표에서</b> 단탈리온으로 되돌아가 있었다

유저는 «스킨이 연동 안 됐다» 고 봤지만 **스킨 문제가 아니었다.**

* 씬의 `MonsterSpawner.bossSlots[5]` = 정의 `Monster_Legimia` + 템플릿
  `fileID 297047034`(= `Monster_Legimia_Template` 자신) — **배선은 멀쩡했다**(126-1절이 고친 그대로).
* `Monster_Legimia_Template` 의 `skinResourceFolder` = `MonsterSkins/Legimia` — **멀쩡했다**.
* ★★ **`웨이브테이블.xlsx` Sheet2 의 30웨이브 `boss_monster_id` 가 120001(단탈리온)** 이었다.
  `WaveDefinitions.asset` 에도 그대로 120001 이 굳어 있었다.

즉 «레기미아가 단탈리온 <b>모습으로</b> 나온» 것이 아니라 **처음부터 단탈리온이 나오고 있었다.**
e16ce8d(2026-08-21)가 이 칸을 120006 으로 고쳤는데, 그 뒤 볼트가 되돌아가면서
(120절의 병합 사고와 같은 경로) **표가 옛 값으로 돌아왔고**, 그 상태로 sync 를 돌린 누군가가
에셋에도 옛 값을 다시 밀어 넣었다.

→ `table_update_20260821_legimia_wave30.py` **재실행** → `sync_tables_to_assets.py`.
  결과: `5 단탈리온 · 10 말파스 · 15 카시노마 · 20 라린길 · 25 베일 · **30 레기미아** ·
  35 말파스 · 40 카시노마`. 바뀐 칸은 `WaveDefinitions.asset` **1줄뿐**(나머지 diff 는 줄바꿈).

⚠ **교훈** — 표가 되돌아간 것을 «게임 버그» 로 읽으면 씬·스킨·템플릿을 몇 시간 뒤진다.
  «표 → 에셋 → 씬» 순서로 **표부터** 확인할 것. 이 프로젝트는 코드에 보스 id 가 한 군데도 없다.

### UI-51-5. 로비 — 안 보이는 버튼이 눌리던 것 · 연출 건너뛰기

★★ **원인** — `EnsureGroup` 이 `alpha = 0` 만 했다. `CanvasGroup.alpha` 는 **그리기만** 끄고
  **레이캐스트는 살아 있다.** 그래서 버튼이 뜨기 전에 그 자리를 누르면 **투명한 버튼**이
  그대로 눌려 게임이 시작됐다.

→ `EnsureGroup` 에서 `blocksRaycasts = false`, 그 버튼의 페이드가 **끝난 뒤에만** `true`
  (`FadeInButton`).
⚠ `interactable` 이 아니라 `blocksRaycasts` 를 쓴다 — `interactable = false` 로 막으면 버튼이
  **비활성 색**으로 떠올라 페이드 인 하는 내내 회색이다(«이어하기» 의 진짜 비활성과 헷갈린다).

**건너뛰기** — `Update` 에서 `_introPlaying` 인 동안만 «누르면 끝까지 감기»(`SkipToEnd`).
⚠ 이 프로젝트는 **Input System 패키지 전용**이다(`activeInputHandler: 1`) — `UnityEngine.Input`
  을 쓰면 실행 시점에 예외가 난다. `Mouse.current` / `Keyboard.current` 를 쓴다.
★ **누르는 순간**(`wasPressedThisFrame`)에 건너뛴다. 그 순간 버튼은 아직
  `blocksRaycasts = false` 라 **누름이 버튼에 등록되지 않았고**, 유니티 버튼은 «같은 대상에서
  누르고 뗐을 때» 만 클릭이 되므로 **손을 떼도 그 버튼이 눌리지 않는다** —
  «건너뛰기 클릭이 그대로 이어하기로 이어지는» 사고가 구조적으로 막힌다.
★ 타이틀의 «제자리» 를 `RiseIn` 안이 아니라 **필드(`_titleHome`)로 끌어냈다** — 건너뛸 때도
  그 자리로 되돌려야 하는데, 지역 변수로 두면 연출을 끊었을 때 타이틀이 아래에 눌러앉는다.

### UI-51-6. 이벤트 창 — 페이드 인 + 떠오르기

`EventPanel` 이 열릴 때 `CanvasGroup` 알파 0 → 1, 아래 36px 에서 제자리로. 끝에서 감속하는
곡선(`1 − (1−t)²`)은 로비 `RiseIn` 과 같다.

★ **«처음 열릴 때» 만** 태운다 — `HandleChanged` 에서 `!gameObject.activeSelf` 를 먼저 읽는다.
  본문 → 결과 단계는 **같은 창의 내용이 바뀌는 것**이라 거기서 또 태우면 선택지를 누를 때마다
  창이 아래로 툭 떨어졌다 올라온다.
★ `CanvasGroup` 은 **코드가 붙인다**(씬에 없어도 동작해야 한다 — 로비와 같은 규칙).
⚠ `Time.unscaledDeltaTime` — 일시정지 중에 창이 뜨면 `deltaTime` 이 0 이라 연출이 영영 멈춘다.
⚠ `Close()` 에서 **제자리로 세워 둔다** — 연출 중에 닫히면 알파 0.3 · 자리가 아래인 채로 굳고,
  다음에 연출 없이 열리는 경로가 그 상태를 그대로 물려받는다.

인스펙터 값: `appearSeconds 0.28` · `appearRisePixels 36`.

### UI-51-7. ★★ 신규 3인이 표에서 <b>사라져 있었다</b> — 복구

유저 지시대로 **진행상황.md 를 먼저 확인**했다(125절이 이 값들의 출처를 그대로 적어 두었다).

**실측 — 백업 `_백업/20260821_194541_명사수_밸류/` 와 한 칸씩 비교**했더니
<b>덧붙임만 있고 값이 충돌하는 칸은 0개</b>였다. «누가 고쳤다» 가 아니라 **되돌아갔다**:

| 파일 / 시트 | 사라진 것 |
|---|---|
| `캐릭터 테이블` / `Character` | **9013·9014 행 삭제** · 9012 는 스트링 키 → 한글 리터럴로 되돌아감 |
| `캐릭터 테이블` / `first_Stat` | **9013·9014 행 삭제** |
| `캐릭터 테이블` / `Skill` | **80037~80042 행 삭제** · 80034~80036 리터럴로 되돌아감 |
| `캐릭터 테이블` / `Skill_Type` | **6줄 삭제** · 3줄 리터럴로 되돌아감 |
| `스트링 키 테이블` | **42행 삭제** (300행 → 258행) |

★ 128절이 인게임 구현을 끝냈으므로 **유니티 에셋에는 세 인물이 다 살아 있었다** — 그래서
  게임은 지금도 돌고 **표만** 어긋나 있었다. ⚠ 그 상태로 `gen_character_assets.py` 를 돌렸다면
  두 인물이 «표에 없다» 며 **에셋에서도 사라진다.** 되돌리는 것이 먼저였다.

`table_restore_20260824_new_characters.py` 가 백업 값을 그대로 되살리고, 백업 이후에 결정된
둘만 **덧입힌다**:

1. **「명사수」(80038) 밸류** — 그 백업은 `table_update_20260821_sharpshooter_value.py` 가
   돌기 **직전**에 뜬 것이라 그대로 되살리면 `value_01 = 0` 이라는 구멍(125-4절 1번)이
   되살아난다. → `value_01 = 20` · 정의문 «20의» → «{value_01}의».
2. **스킬 아이콘 9칸** — 129-5절이 «착수 못 했다» 고 남긴 일이자 이번 유저 추가 지시다.

⚠ `캐릭터 테이블.xlsx` 는 **Excel COM + DispatchEx** 로 썼다 — 하이퍼링크 154칸(51-11절)이
  openpyxl 저장에서 전부 날아간다. **저장 후 154칸 그대로**인 것을 확인했다.

이어서 파이프라인: `gen_string_table.py`(297키 · `StringTable.txt` 내보내기) →
`link_string_keys.py`(하이퍼링크 280칸 재생성) → `gen_character_assets.py`(**정의 14 · 패시브 42**).

#### 스킬 아이콘 배정 — 이미 있는 90장에서 골랐다

새로 자를 것이 없다(UI-40 의 `skill_icon_build.py` 가 시트 두 장을 이미 다 갈라 뒀다 —
90장 중 **33장만** 쓰이고 있었다). 쓰이는 33장과 **겹치지 않게** 골랐다:

| 스킬 | 아이콘 | 왜 |
|---|---|---|
| 80034 강인한 정신 | `icon_heal_cross` | 초록 회복 십자 — 스스로 되찾는다 |
| 80035 군단의 방패 | `icon_barrier_dome` | 사람을 감싸는 방패 돔 |
| 80036 네 날개의 가호 | `icon_meteor_circle` | 원형 범위로 내리꽂히는 빛 |
| 80037 회피 기동 | `icon_sprint_dash` | 달려 빠지는 형상 |
| 80038 명사수 | `icon_snipe_mark` | 조준선과 화살 |
| 80039 종말의 선언 | `icon_cannon_battery` | 집중 포격 |
| 80040 영혼 흡수 | `icon_ghost_wail` | 거둬들이는 망령 |
| 80041 사신의 낫 | `icon_hooded_reaper` | 두건 쓴 사신 |
| 80042 한계 돌파 | `icon_red_slash` | 붉은 참격(근거리 공격력) |

⚠ 되도록 **새 세트**(픽셀아트 타일 66장)에서 골랐다 — 옛 24장은 테두리 없는 납작한 그림이라
  한 화면에 섞이면 «빠진 것처럼» 보인다(UI-40 의 결론). 다만 «방패 돔»·«회복 십자» 는 새
  세트에 대체할 그림이 없어 옛 것을 썼다.

#### ★ 시안 스킬이 실제로 도는지 확인했다 (유저 지시)

| 확인 | 결과 |
|---|---|
| `PassiveSkillTypes.Parse` | `soul_absorption` · `the_reapers_scythe` · `breaking_through_limits` **셋 다 있다** |
| 표의 둥근 따옴표 `The_Reaper’s_Scythe` | `Normalize` 가 U+2019 를 지우므로 **맞는다** |
| 구현 | `CharacterPassives.Trio.cs` — 영혼 획득(`OnAnyDiedTrio`) · 낫(`OnAttackPerformedTrio`) · 한계 돌파(`ApplyAlwaysOnTrio`) |
| 호출 경로 | `CharacterPassives.cs:368` → `TickTrio` → `HookTrioEvents`/`ApplyAlwaysOnTrio` · `:121` → `ClearTrioEffects` |
| 에디터 검사 | `LastSanctuary/검사/스킬 종류 배선 검사` — **에셋 63개 · 종류를 못 알아보는 것 0** |

⚠ 그 검사가 남긴 «밸류 전부 0» 경고 3건(`Vanguard` 80013 · `Salvation` 80023 ·
  `Instability` 80028)은 **이번 3인과 무관한 기존 항목**이다. 손대지 않았다.

### UI-51-8. 듀토리얼 — <b>계획만</b> (유저 지시: 만들지 말 것)

방향만 세워 유저에게 설명했다. 요지 — **별도 씬·별도 진행도를 만들지 않고**, 웨이브 1~3 위에
「강조 + 말풍선 + 한 가지만 눌리게」를 얹는 **오버레이 한 겹**으로 만든다. 표
(`듀토리얼 테이블.xlsx`)가 단계를 들고, `TutorialService` 가 «조건 → 강조 → 완료 조건» 만 돌린다.
상세 설계는 착수할 때 이 절 아래에 이어 쓴다.

### 검증

* `recompile_scripts` **에러 0 · 경고 0** · 콘솔 에러 0
* 유물 아이콘 **31/31** 교체 · `.meta` **0개** 변경(guid 유지) · 대조표 눈검사 완료
* 씬 TMP 폰트 **232/232** 네오둥근모 · `HUD_Actions` 396 확인 · `save_scene` 1회
* `WaveDefinitions.asset` 30웨이브 `bossMonsterId 120006`
* 캐릭터 테이블 — 백업과 비교해 **차이가 «아이콘 9칸 + 명사수 밸류 1칸» 뿐**임을 확인
* 하이퍼링크 154칸 유지 · 스트링 297키 · 캐릭터 정의 14 · 패시브 42

### 아직 확인 못 한 것 (유저가 볼 것)

1. ★★ **플레이 실측을 못 했다** — 로비 클릭/스킵 · 이벤트 등장 연출 · 유물 아이콘이 창에
   실제로 뜨는지 · 성장 창의 새 3인 스킬 아이콘. 전부 눈으로 볼 것.
2. **30웨이브 실전** — 레기미아가 실제로 나오는지(배속으로 30웨이브까지 가야 한다).
3. 유물 아이콘 배정이 마음에 안 들면 `relic_icon_build.py` 의 `PICK` 표 좌표만 고치면 된다.
4. **`진행상황.md` 가 세 절 밀려 있다** — UI-49·UI-50·UI-51 이 아직 병합되지 않았다.

### 씬반영요청 목록

- 없음 (씬 편집을 이 세션에서 MCP 로 마쳤다)

---

## UI-52. ★★ 유물 UI 좌표가 통째로 어긋나 있던 이유 · 허드 액션 자동 높이 · 창 드래그 · 성장 창 유물 띠 · 로스터 부대 묶음 · 세라피엘 포격 (2026-08-24, 2차)

### 무엇을 / 왜

유저 지시 일곱:

1. *"지금 허드 액션이 하드 코딩 되어 있음 건설 버튼 비활성화 유물관리 넣고 허드 액션 크기 맞춰 mcp 로 직접"*
2. *"유물 관리 버튼 닫기가 안되고"*
3. *"ui 들 스크롤 해서 창 옮길 수 있게 해줘 이벤트나 유물관리 ui도 다"*
4. *"캐릭터 성장 ui에 유물 장착 칸 있어야 하는데 없음 … 적절한 위치에 직접 mcp로 생성"*
5. *"캐릭터 로스터 배열 정렬을 같은 부대 기준으로 하고 같은 부대인 캐릭터를 각기 다른 색의 아웃라인으로 묶어서"*
6. *"시안 / 세라피엘 스킨 모션 확인"*
7. *"세라피엘 세번째 스킬 포격 느낌이 안 나니까 좀 여러개 섞어서 화려하게"*

**신규 스크립트** — [Tools/mcp_hud_20260824.py](Tools/mcp_hud_20260824.py)
**고친 스크립트** — `mcp_build_relic_ui.py` · `seraphiel_skin_build.py`
**고친 C#** — `UI/ActionPanel.cs` · `UI/CharacterGrowthPanel.cs` · `UI/CharacterRosterPanel.cs` ·
`UI/HudTheme.cs` · `Combat/CharacterPassives.Trio.cs`
**씬 변경** — 있음(유물 창 재배치 · 성장 창 확장 + 유물 띠 · 액션 패널 · 드래그 3건) · `save_scene`

---

### UI-52-1. ★★★ 원인 하나가 둘을 만들었다 — `label()` 의 <b>인자 밀림</b>

지시 2번(«유물 관리 닫기가 안 된다»)과 «유물 창 글자가 이상한 데 있다» 는 <b>같은 버그</b>였다.

`Tools/mcp_build_relic_ui.py` 의 도우미 서명이 이랬다:

```python
def label(path, value, size, color, align, wrap, amin, amax, omin, omax)
                                          ^^^^ 쓰지도 않는 칸
```

`wrap` 은 `text()` 가 <b>받기만 하고 한 번도 안 쓰는</b> 죽은 인자였다. 그런데 호출은
좌표 네 개를 <b>위치 인자</b>로 넘긴다:

```python
label(p + "/Header", "유물 관리", 22, TEXT, "Left", (0,1), (1,1), (18,-46), (-56,-10))
#                                                    └ wrap  └ amin └ amax   └ omin
```

→ 네 좌표가 <b>한 칸씩 밀려</b> `amin=(1,1) · amax=(18,-46) · omin=(-56,-10) · omax=(-6,0)` 이 됐다.
<b>앵커에 −46, 18 같은 값이 들어간다.</b> 앵커는 0~1 이 정상이므로 18 은 «부모 폭의 18배»,
−46 은 «부모 높이의 −46배» 다.

실측(고치기 전 씬):

```
Header  min{1, 1} max{18, -46}      ← 부모 폭의 18배까지 뻗는다
Effect  min{1, 1} max{14, -170}
Flavor  min{1, 1} max{14, -250}
```

★★ <b>그래서 닫기가 안 됐다.</b> 이 글자 칸들은 `raycastTarget` 이 켜진 채로 <b>화면 전체를
  덮을 만큼</b> 커져 있었고, 만들어진 순서상 `CloseButton` 보다 <b>뒤 형제</b>(= 위에 그려짐)라
  X 버튼도, 그 아래 깔린 <b>허드 액션의 「유물 관리 닫기」 버튼</b>도 클릭이 닿지 않았다.
  «닫기 로직» 은 멀쩡했다 — <b>클릭이 도달하지 못한 것</b>이다.

→ `wrap` 을 <b>두 함수에서 없앴다</b>(죽은 칸이므로 지우는 것이 맞다). `False` 를 위치로
  넘기던 호출 네 개도 같이 정리했다. 그 뒤 유물 창 전체를 다시 구웠다(멱등).

고친 뒤 실측:

```
Header  min{0, 1} max{1, 1}  pos{-19, -28} size{-74, 36}
Effect  min{0, 1} max{1, 1}  pos{0, -131}  size{-28, 78}
```

⚠ <b>교훈</b> — 도우미에 «쓰지 않는 인자» 를 남겨두지 말 것. 위치 인자로 부르는 코드가
  하나라도 있으면 그 칸이 <b>조용한 함정</b>이 된다. 파이썬은 타입을 안 보므로 튜플이
  불리언 자리에 들어가도 죽지 않는다 — 결과만 이상해진다.

### UI-52-2. 허드 액션 — 건설 끄기 + <b>높이를 세어서</b> 정한다

* `BuildButton` 을 **껐다**(MCP). 건설은 이미 걷어낸 기능인데 버튼만 남아 있었다.
  ⚠ <b>지우지 않았다</b> — `BuildButtonUI`·`BuildService` 는 그대로라 되살릴 때 한 줄이면 된다.
* 씬 높이 **396 → 348**(7 x 40 + 6 x 8 + 20).
* ★★ 그리고 <b>`ActionPanel.FitHeight()` 를 새로 만들었다</b> — 지시 1번의 «하드 코딩» 이 그것이다.
  `Buttons` 의 <b>켜져 있는 자식</b>을 세고 `LayoutElement.preferredHeight` 와
  `VerticalLayoutGroup.spacing` 으로 필요한 높이를 계산해 `Start` 에서 스스로 맞춘다.
  ⚠ `Buttons` 는 <b>늘어나는 자식</b>이라 자기 높이가 «부모 + sizeDelta.y» 다 —
    그래서 부모에게 필요한 값은 <b>내용 높이 − sizeDelta.y</b> 다(−20 이면 20 을 더하는 셈).
  ★ 이제 버튼을 더하거나 빼도 <b>칸이 따라온다</b>. 씬의 값은 «에디터에서도 맞아 보이게» 하는 것뿐이다.

### UI-52-3. 창 드래그 — 남은 셋

2026-08-18 에 여섯(설정·전술·성장·유물·토벌·부대)에 `UiWindowDrag` 가 붙었고,
`HUD_Event` · `HUD_SkillDetail` · `HUD_Portrait` 가 빠져 있었다. 셋 다 붙였다.

⚠ `HUD_Portrait` 는 배경 `Image` 의 `raycastTarget` 이 <b>꺼져 있었다</b> — 포인터를 아예
  못 받으므로 컴포넌트만 붙여서는 드래그가 시작되지 않는다. 같이 켰다.

### UI-52-4. ★★ 성장 창의 유물 칸 — <b>«없었다» 가 아니라 «깔려 있었다»</b>

UI-50 이 만든 `HUD_Growth/Stats/RelicSlot` 은 <b>존재했다</b>. 그런데 `Stats`(906x640)는
위에서 아래까지 이미 꽉 차 있다:

```
Head -16 · GrowthLabel -50 · GrowthTypes -76 · Grid -120~-414
PassiveHead -424 · PassiveGrid -456~-632          (칸 바닥이 -640)
```

바닥에 놓은 유물 칸(y37 · 높이 58)은 <b>패시브 카드 밑에 깔려</b> 보이지 않았다.
게다가 그 자식들의 앵커는 UI-52-1 의 인자 밀림에 함께 당해 있었다
(`Head min{10, -26}` · `Name min{1,1} max{56,4}`).

→ <b>창을 84px 늘리고</b>(830 → 924) <b>두 열 아래에 가로로 긴 띠</b>를 새로 깔았다.

```
HUD_Growth (1220 x 924)
├ Info  250x640   (그대로)
├ Stats 906x640   (그대로 · 옛 RelicSlot 은 꺼둠)
├ RelicBar 1172x84  ← 신설, y -762
│   Icon 64x64 · Head「장착한 유물」· Name(등급색) · Effect(효과 한 줄) · 「유물 관리 열기」
└ Footer 1220x70  (바닥 고정이라 자동으로 따라 내려감)
```

★ 두 열은 <b>손대지 않았다</b> — 그 안을 비집으면 다른 칸이 전부 밀린다(그 배치는 82-14절에서
  유저가 직접 잡은 것이다).
★ 창 높이 924 는 화면 1080 에서 위 110 을 뺀 970 안에 들어간다(바닥 여백 46px).
★ 「유물 관리 열기」는 <b>창을 열 뿐</b>이다 — 고르는 일은 유물 창 한 곳에서만 한다(UI-50 의 규칙).
⚠ `CharacterGrowthPanel` 은 <b>옛 경로도 폴백으로 남긴다</b> — 씬을 되돌린 사람이 있어도 안 깨진다.

### UI-52-5. 로스터 — 부대별 정렬 + 부대색 아웃라인

* 정렬: `OrderBy(부대 순번).ThenBy(생성순)`. 부대에 안 든 캐릭터는 <b>맨 아래</b>로 모인다.
  ★ <b>안정 정렬</b>이라 같은 부대 안의 순서는 생성순 그대로다 — 카드가 손 밑에서 튀지 않는다
    («생성순 고정» 을 정한 2026-08-21 지시를 부대 안에서 그대로 지킨다).
* 색: `HudTheme.SquadColor(순번)` — 청록·주황·보라·연두·하늘·분홍 여섯(부대 상한이 6).
  ★ <b>부대 id 가 아니라 «목록에서의 순번»</b> 으로 고른다 — id 는 지웠다 만들면 1,2,5 처럼
    띄엄띄엄해져 색이 건너뛴다. 순번이면 부대 창의 카드 순서와 색이 언제나 같이 간다.
  ★ 체력바(초록·노랑·빨강) · 침식(보라·자홍) · 각성(금색)과 <b>겹치지 않게</b> 골랐다.
* 그리는 방법: 행 배경에 `UnityEngine.UI.Outline` 을 <b>코드가 붙인다</b>(보호막 막대와 같은 방식 —
  행 모체를 손대지 않아도 모든 행에 생긴다). 부대가 없으면 `effectColor = Color.clear` 다.
  ⚠ 컴포넌트를 붙였다 뗐다 하지 않는다 — 레이아웃이 흔들린다.
  ⚠ 죽은 행은 테두리를 지운다(`SquadService` 가 사망 시 부대에서 빼므로 표시도 맞춰야 한다).

### UI-52-6. 스킨 확인 — 세라피엘은 고쳤고 시안은 <b>남았다</b>

`check_frame_edges.py` 로 구운 PNG 를 재고, 문제가 난 묶음은 눈으로 확인했다.

#### ★★ 세라피엘 — 이펙트 아래에 <b>다음 줄이 반쯤 구워져</b> 있었다 (고침)

`MuzzleFlash` 00·01 «아래 43/35%» · `ImpactMagic` 00·02 «아래 61/41%». 구운 PNG 를 보니
<b>아랫줄 부품의 윗부분</b>이 프레임마다 붙어 있었다(총구 섬광 87x71 · 착탄 73x84).

원인은 `char_sheet.grow_limits`(UI-49 신설)다. 그 함수는 «x 가 겹치는 <b>이웃 줄</b>» 의 밴드
중간까지 상자를 넓혀 준다. 그런데 이 팔레트는 한 상자에 <b>같은 부품이 세기별로 2~3줄</b>
쌓여 있고, <b>그 아랫줄들이 `ROWS` 에 선언돼 있지 않았다.</b> 이웃이 «없으니» 상한이
`GROW_MARGIN`(26px)까지 열렸고 글로우가 이어진 아랫줄을 그만큼 끌고 왔다.

실측(알파>60):

```
x346~653 :  라벨 681~707 · 1줄 710~753 · 2줄 758~801 · 3줄 804~831
x1045~1290: 라벨 683~709 · 1줄 711~760 · 2줄 768~816
```

→ 아랫줄을 <b>«울타리» 줄로 선언만 했다</b>(`Unused_MuzzleFlashB/C` · `Unused_ImpactMagicB`).
  그러면 상한이 두 밴드의 중간으로 내려와 침범이 끝난다. `ImpactMagic` 밴드는 라벨을
  물고 있어 `704 → 711` 로 조였다.
★ <b>좌표를 손으로 조이는 대신 «선언» 으로 고쳤다</b> — 손으로 조이면 원화가 바뀔 때 또 어긋난다.
★ 울타리 줄은 <b>배선하지 않는다</b>(`Unused_` 규약) — 총구 섬광은 평타마다 도는 애니메이션이라
  장수를 늘리면 «한 발 쏘는데 12프레임» 이 된다.

고친 뒤: `MuzzleFlash` 93x47 · `ImpactMagic` 63x59 · <b>테두리 검사 경고 0</b>(배선 안 한
`Unused_Skill2Smoke` 하나만 남는다) · `measure_skin_tiles` <b>몸집 «변경 없음»</b>.

#### 시안 — 확인만 했다 (⚠ 남은 일)

| 항목 | 판정 |
|---|---|
| `idleRight 7` vs `idleLeft 8` | ★ <b>정상</b> — 원화가 그렇게 그려져 있다(UI-49 가 발밑 덩어리로 실측해 `(8, 7)` 로 못박았다). 예전에 양쪽 8로 두었다가 낫이 반쪽으로 잘렸던 그 자리다 |
| `skill2/skill3` 칸이 빔 | ★ <b>정상</b> — 시안의 스킬 셋 중 모션이 있는 것은 「사신의 낫」 하나뿐이고 그것이 slot 0 이다 |
| `HealFx` 5장 전부 좌우 47~78% 단면 | ⚠ <b>실제 결함</b> — 옆 부품의 조각이 슬리버로 남는다 |
| `Heal` 프레임 3·4·8 | ⚠ <b>실제 결함</b> — 글로우가 <b>직각으로 잘린 사각 덩어리</b>로 남는다(구운 PNG 확인) |

★ 원인은 세라피엘과 다르다 — 시안의 회복 상자는 <b>부품끼리 글로우가 가로로 완전히 겹친다</b>
  (열 프로파일이 x803~1379 <b>한 덩어리</b>다 · 알파 문턱을 120 까지 올려도 안 갈린다).
  즉 «칸을 더 잘 재면» 되는 문제가 아니다 — 자르는 자리를 <b>흐리게 해서</b>(`skin_sheet.feather_edges`)
  단면을 없애야 한다. 시안 빌더는 `char_sheet.Row` 가 아니라 <b>독자 형식</b>이라
  `feather` 를 받을 자리가 없다.
⚠ <b>손대지 않았다</b> — 회복 이펙트는 이번 지시(포격)와 무관하고, 잘못 건드리면 UI-49 가
  고친 것을 되돌릴 위험이 있다. 유저 판단을 받고 하는 것이 맞다.

### UI-52-7. 세라피엘 「종말의 선언」 — 포격답게

**예전에는 왜 밋밋했나** — 1초에 한 번, `skill2Fx` <b>한 장</b>을 `PlayArea` 로 깔았다.
그것도 크기를 `max(length, width)` <b>정사각</b>으로 줘서 6x2 상자와 모양이 안 맞았고,
무엇보다 «한 번 번쩍» 이라 <b>연사</b>로 읽히지 않았다.

→ 연출을 <b>피해와 갈라</b> 코루틴 하나를 새로 뒀다(`BarrageFx`). 세 겹이다:

| 겹 | 무엇 |
|---|---|
| 상자 | 어디가 맞는지 <b>한 번만</b> 깐다. 이제 <b>가로 x 세로를 따로</b> 준다 |
| 포탄 | 상자 안 아무 자리에 <b>초당 7발</b>. 크기(0.9~1.7타일)·각도(0~360°)·프레임 묶음을 매번 흔든다 |
| 총구 섬광 | 매 초 시전자 앞 0.8타일. «쏘는 쪽» 이 보여야 포격이 된다 |

★ 프레임은 스킨의 <b>여러 칸을 섞어 쓴다</b>(유저 지시의 «여러개 섞어서») —
  `skill2Fx`(화염 3) · `magicImpactFrames`(고리·별 4) · `impactFrames` · `muzzleFlashFrames`(섬광 4)
  중 <b>있는 것만</b> 모아 무작위로 고른다. 없으면 조용히 빠지므로 다른 인물이 이 스킬을
  갖게 돼도 안 깨진다.
⚠ <b>순수 연출이다</b> — 피해는 `Barrage` 가 넣는다. 여기서 또 넣으면 이중 타격이다
  (`CombatProjectileFx` 의 대원칙).
★ 이 스킬이 쓰는 두 이펙트가 마침 UI-52-6 에서 고친 그 둘이다 — 아랫줄이 붙어 있던
  그림으로 포탄을 뿌렸으면 «네모난 얼룩» 이 흩뿌려졌을 것이다.

### 검증

* `recompile_scripts` **에러 0 · 경고 0**
* MCP **126건**(유물 UI 119 + 허드 7) **전부 성공** · `save_scene`
* 씬 실측 — `HUD_Relics` 앵커 전부 0~1 로 정상 · `HUD_Growth` 924 + `RelicBar` 1172x84 ·
  `HUD_Actions` 348 · `BuildButton` 꺼짐
* 폰트 메뉴 재실행 — 새로 만든 글자까지 네오둥근모
* 세라피엘 재구움 **139장** · `check_frame_edges` 경고 **0**(배선 안 한 것 하나 제외) ·
  `measure_skin_tiles` 몸집 **변경 없음**
* `진행상황.md` **130~132절 편입** — 옛 16,844줄 중 <b>바뀐 줄은 머리말 1줄뿐</b>이고
  597줄이 덧붙었다(무손실 확인 후 백업 삭제)

### 아직 확인 못 한 것 (유저가 볼 것)

1. ★★ **플레이 실측을 못 했다** — 유물 창 닫기 · 창 드래그 · 성장 창 유물 띠 ·
   로스터 부대 테두리 · 세라피엘 포격. 전부 눈으로 볼 것.
2. ⚠ **시안 회복 이펙트/모션의 단면**(UI-52-6) — 고칠지 유저 판단 대기.
3. `HUD_Portrait` 의 `raycastTarget` 을 켰다 — 초상화 카드 <b>아래의 월드 클릭</b>이
   막힌다(카드가 유닛을 가리고 있을 때). 의도한 동작이지만 어색하면 되돌릴 것.
4. 부대 색 여섯은 값으로 잡은 것이다 — 화면에서 구분이 약하면 `HudTheme.SquadColors` 만 고치면 된다.

### 씬반영요청 목록

- 없음 (씬 편집을 이 세션에서 MCP 로 마쳤다)

---

## UI-53. ★★★ 유물 테이블 Ver02 — 정수·상한 초월 보상 · 효과 슬롯 둘 · 45종 · 발굴을 「묻고 답하는」 창으로 · 대사 표 (2026-08-24, 3차)

### 무엇을 / 왜

유저 지시 여섯:

1. *"유물 좀 더 추가하고 효과 조금 더 다양화 해주고 유물로 얻은 능력치 보상은 정수로 만들어서
   최댓값 초월할 수 있게 해줘 해당 설명을 밸류 타입에 넣어주고, 두가지 이상의 능력치가
   한번에 오르는 유물도 있으면 좋을 듯"*
2. *"유물 자동 발굴 되게 하지말고 유물이 발견된 칸에 별도의 ui로 버튼을 생성하고 해당 버튼은
   캐릭터/몬스터 등 개체 클릭보다 상위로 두고 해당 칸을 누를 경우 발굴 ui가 나와서 발굴하기를
   누르면 가장 가까운 캐릭터가 가서 발굴하게 해줘. 이벤트 ui처럼 위험이 도사리고 있을지도
   모릅니다.... yes: 가까이 가서 살펴본다. no: 방심은 금물이다. 그냥 두자."*
3. *"여러가지 대사 스크립트 만들어서 확률 동일로 몇가지 대사 중 랜덤으로 뜨게 … 대사 그룹은
   따로 유물 테이블에 다이얼로그 시트로 따로 빼서 … 발견/yes/no/result 4가지 상황에 대해
   초이스 그룹과 연동해서 분기 처리 … 이벤트 테이블 참고"*
4. *"보스 유물 획득 다이얼로그도 따로 넣어주고"*
5. *"너무 오버 밸런스 안 나오게 잘 생각해줘"*
6. *"발굴 가능 칸이 발견되면 생성되는 발굴 ui 버튼은 느낌표로 처리"* · *"mcp 사용해서 직접 생성해"*

(+ 곁가지) *"부대 설정에서 부대 선택했을때 표시되는 색이 캐릭터 로스터에서 같은 부대로 묶이는
색으로 해줘"*

**신규** — [Tools/mcp_build_dig_ui.py](Tools/mcp_build_dig_ui.py) ·
`Scripts/Relics/RelicDialogueTableSO.cs` · `Scripts/UI/RelicDigPanel.cs`
**고친 것** — `gen_relic_table.py`(전면) · `gen_relic_assets.py` · `relic_icon_build.py` ·
`RelicDefinitionSO.cs` · `RelicEffectService.cs` · `RelicDigService.cs` · `RelicDropService.cs` ·
`SquadPanel.cs`
**씬 변경** — 있음(`UI_Root/HUD_Dig` 신설 · MCP 46건) · `save_scene`

---

### UI-53-1. ★★ 능력치 보상을 «정수 · 상한 초월» 로 — <b>공식을 재고 나서</b> 정했다

유저 지시 5번(«오버 밸런스 안 나오게»)이 중간에 들어와서, **처음 잡은 값을 버리고 다시 잡았다.**
근거는 `BalanceConfigSO` 의 실제 공식이다:

| 능력치 1 포인트 | 실제 효과 | 캐릭터 기본값 |
|---|---|---|
| 체력 | **+10 HP** (`40 + stat x 10`) | 2~12 → 60~160 HP |
| 근거리/원거리/마법 | **+2 피해** (`2 + stat x 2`) | 1~10 → 4~22 |
| 방어 | 약 **+1.3%p 경감** (`def/(def+50)`) | 1~11 |
| 명중 / 크리 | **+1%p** (`80 + stat` / `stat`) | 2~9 / 1~10 |
| 저항 | 침식 속도 1%p | **13~96** ← 대역이 다르다 |

★★ **가장 중요한 기준 — 「강화 1회」의 크기.** `CharacterUpgradeService` 의
`growthWeights = {8,17,25,25,17,8}` · `growthFocusBonus = 1` 이므로
**한 번 강화하면 총 +2~3 포인트가 12개 능력치에 흩어진다.**

처음에 잡았던 «에픽 +7» 은 **강화 3회분을 한 능력치에 몰아주는** 값이었다 — 확실히 과했다.
(참고로 캐릭터의 <b>시그니처 패시브</b>가 「광란」 공격 +10 · 「로 아이아스」 방어 +8 ·
「명사수」 크리 +20 이다. 유물은 그보다 <b>확실히 약해야</b> 한다 — 모두가 하나씩 끼고,
판이 갈수록 쌓인다.)

**최종 대역** (표 `Grade` 시트에 적어 두었다):

| 등급 | 일반 능력치 | 방어 | 크리·명중 | 저항 | 슬롯 |
|---|---|---|---|---|---|
| 일반 | **+1** | +2 | +2 | +4 | 대개 하나 |
| 레어 | **+2** | +3~5 | +4 | +8 | 셋이 두 칸 |
| 에픽 | **+2 x 2칸** | +3 | +4 | +8 | 대부분 두 칸 |

→ 에픽 하나 ≈ **강화 1.6회분**을 원하는 곳에 집중. 단일 슬롯이 깊이, 두 칸이 넓이를 준다.

★ **상한 초월은 새로 만들지 않았다** — `CharacterUnit.AddFlatStatBonus` 가 이미 그 일을 한다
  (`EffectiveStat` 이 상한을 적용한 <b>뒤에</b> 고정 보정을 더한다). 패시브들이 쓰는 그 통로다.
⚠ **비율이 뜻을 갖는 효과는 % 그대로다** — 흡혈·반사·처치 회복·부활·침식 감속·시야·발굴 속도.
  이것들은 «내 능력치» 가 아니라 «벌어진 일» 에 대한 비율이라 정수로 바꿀 대상이 아니다.

⚠ 「삼킨 것의 이빨」 처치 회복은 12% → **6%** 로 낮췄다(처치가 잦다).
  「심장에 박힌 가시」 누적은 10회 → **5회**(최대 +5 = 「광란」의 절반).
  「마르지 않는 샘」은 20%+60 → **12%+40**.

### UI-53-2. 효과 슬롯이 <b>둘</b>이 됐다

표에 `effect_type_02` · `value_03` · `value_04` 가 생겼다. 규약은 이벤트 보상의
`reward_type_01/02` 와 **같다**. 코드는 `RelicDefinitionSO.Effects()` 하나로 두 칸을 돌린다 —
«첫 칸만 보는» 코드가 조용히 틀리는 것을 막으려고 <b>조회를 한 곳으로 모았다</b>
(`RelicEffectService.ValueOf` / `TryValueOf`).

⚠ 그 과정에서 <b>기존 버그 하나</b>를 같이 고쳤다 — 「두꺼워진 가피」가 문턱을 넘나들 때
  `RemoveAllFor(unit)` 로 **그 캐릭터의 보정을 통째로** 뗐다. 슬롯이 하나일 때는 우연히
  맞았지만, 둘이 되면 **같은 유물의 상시 보너스까지 사라진다**.
  → `Applied.conditional` 표시를 두고 <b>문턱형이 붙인 것만</b> 뗀다.

### UI-53-3. 효과 타입 6종 추가 (21 → 27)

| 타입 | 무엇 | 어디에 붙나 |
|---|---|---|
| `relic_kill_erosion_down` | 처치할 때마다 침식 −v1 | `HandleKillCredit` |
| `relic_kill_growth` | 처치마다 근거리 +v1, **최대 v2회** | `HandleKillCredit` + 누적 장부 |
| `relic_low_hp_lifesteal` | 체력 v2% 이하일 때만 흡혈 v1% | `HandleDamaged` |
| `relic_wave_shield` | 웨이브 시작 때 최대체력 v1% 보호막 v2초 | `OnWaveSpawned` |
| `relic_wave_energy` | 웨이브가 끝날 때마다 에너지 +v1 | `OnWaveEnded` |
| `relic_wave_heal` | 웨이브가 끝날 때마다 최대체력 v1% 회복 | `OnWaveEnded` |

★★ **웨이브 계열의 통로가 까다로웠다.** `WaveManager` 의 이벤트는 <b>정적이 아니라 인스턴스</b>
  것이라 정적 클래스인 `RelicEffectService` 가 붙을 수 없고, 매니저가 유물을 알게 만드는 것도
  방향이 거꾸로다(유물은 나중에 생긴 기능이다).
  → **이미 매 프레임 도는 `RelicDigService.Update` 가 웨이브 번호가 바뀌는 것을 보고 알린다**
    (`WatchWave`). 「두꺼워진 가피」를 그 `Update` 에 얹은 것과 <b>같은 판단</b>이다.
  ⚠ 첫 프레임은 «바뀌었다» 로 보지 않는다 — 판을 켜자마자 보호막이 공짜로 걸리면 안 된다.

### UI-53-4. 유물 31 → 45종

일반 11→16 · 레어 8→13 · 에픽 12→16. 이름 축은 그대로다(일반 = 비특이 면역 ·
레어 = 특이 면역 · 에픽 = 침입자에게서 빼앗은 것).

신규 아이콘 14장은 **이미 있는 원화에서 잘랐다** — 108칸 중 45칸을 쓰게 됐다
(`relic_icon_build.py` 의 `PICK` 에 14줄 추가).
⚠⚠ **순서를 지켜야 한다** — `gen_relic_assets.py` 가 아이콘을 <b>임시 그림으로 덮어쓴다</b>.
  그래서 반드시 `gen_relic_table.py` → `gen_relic_assets.py` → **`relic_icon_build.py`** 순이다.
  (이번에 실제로 한 번 덮였다가 되살렸다.)

### UI-53-5. ★★ 발굴이 「묻고 답한다」 — 자동 발굴 폐지

**예전** — 느낌표를 누르면 `Order(site)` 가 <b>곧바로</b> 지시를 내렸다.
잘못 눌러도 되돌릴 수 없었고, 무엇보다 **발굴이 도박이라는 것**이 화면 어디에도 없었다
(표 `DigOutcome` 에 `dig_hurt` 가 10% 있다).

**지금** — 느낌표 → `Open(site)` → **`HUD_Dig` 창**:

```
① 발견   discover 대사 + 선택지 둘        「가까이 가서 살펴본다.」/「방심은 금물이다. 그냥 두자.」
② 답변   accept · decline 대사 + 「확인」   ← accept 를 고른 순간에야 Confirm(site) 이 나간다
③ 결과   result 대사 + 발굴 결과 + 아이콘
   보스   boss_drop 대사 + 얻은 유물        ← ③ 과 같은 모습
```

★ **표식은 느낌표 그대로다**(유저 지시 6번) — `DigMarkerTemplate` 을 손대지 않았다.
★ **«개체 클릭보다 상위» 는 이미 성립하고 있었다** — 확인해 보니 두 겹이다:
  ① `DigOverlay` 가 <b>자기 Canvas + GraphicRaycaster</b> 를 갖고 `sortingOrder = 5`
     (`UI_Root` 0 · 건설/집결지 오버레이 −1)
  ② `UnitSelector` 가 <b>포인터가 UI 위면 월드 클릭을 버린다</b>
  → 새로 할 일은 없었다. 대신 <b>창에 자기 Canvas(order 6)를 줬다</b> —
    안 그러면 **표식(5)이 창 위에 그려져** 느낌표가 글자를 뚫고 보인다.
  ⚠ Canvas 를 겹치면 `GraphicRaycaster` 도 함께 붙여야 한다(없으면 그 아래 버튼이 안 눌린다).
★ **창은 게임을 멈추지 않는다** — 발굴은 전투 중에도 일어난다. 대신 `HudExclusive` 로 배타다.
★ 창이 없으면(씬을 아직 안 만든 상태) `Open` 이 **예전처럼 곧바로 지시한다** —
  UI 하나 때문에 기능이 통째로 죽으면 안 된다.

### UI-53-6. 대사 표 — `Dialogue` · `DigChoice` 시트

이벤트 테이블 Ver013 의 `Event` + `ChoiceGroup` 과 **같은 규약**이다.

```
Dialogue    dialogue_id · dialogue_group_id · situation · choice_group_id · weight · script
DigChoice   choice_group_id · choice_id · choice_order · choice_kind · choice_text
```

* **상황 다섯** — `discover` / `accept` / `decline` / `result` (유저 지시 3번) +
  `boss_drop` (유저 지시 4번 · 그룹 0 의 독립 풀).
* **그룹이 «한 벌»** — 칸을 만들 때 그룹 하나를 뽑아 **그 칸에 기억해 둔다**(`DigSite.DialogueGroup`).
  그 뒤 accept/decline/result 를 <b>같은 그룹에서</b> 고른다.
  ★ 창을 열 때마다 말투가 바뀌면 «다른 자리를 보고 있나» 싶어진다 — 발견의 말투와 결과의
    말투는 이어져야 한다.
* **같은 상황 안에서는 균등 추첨**(유저 지시의 «확률 동일로») — 그래서 `weight` 가 전부 10 이다.
  특정 대사를 더/덜 나오게 하려면 **표의 그 칸만** 고치면 된다.
* `choice_kind`(accept/decline)가 **코드의 분기**다 — 버튼 문구를 바꿔도 동작은 안 바뀐다.
* 대사 **41줄**(그룹 6개 x 6줄 + 보스 드랍 5줄) · 선택지 **8줄**(그룹 4개).

⚠ **대사 묶음은 세이브에 넣지 않았다** — 세이브 형식(`Vector4`)에 칸이 없고, 말투가 이어하기
  뒤에 달라지는 것은 «틀린» 것이 아니다. 자리·진행도처럼 «맞아야 하는» 값과 구분했다.
⚠ 보스 드랍 창은 **에픽에만** 띄운다 — 일반 몹 드랍(1.2%)에도 띄우면 한 웨이브에 수십 번
  튀어나온다.

### UI-53-7. 부대 색 통일 (곁가지 지시)

부대 설정 창의 카드 색을 **로스터의 부대 테두리 색**과 맞췄다(`HudTheme.SquadColor`).
고른 카드는 그 색을 섞어 칠하고, 안 고른 카드도 **같은 색 테두리**를 둘러 «몇 번 부대인지»
항상 보이게 했다.
★ **부대 id 가 아니라 «목록 순번»** 으로 고른다 — 로스터가 쓰는 기준과 같아야
  «저 색이 저 부대» 가 성립한다.
⚠ 부대 색을 그대로 칠하지 않고 <b>선택색과 섞는다</b>(0.45) — 부대 색은 밝은 파스텔이고
  카드 글자는 흰색이라 그대로 칠하면 글자가 안 보인다.

### 검증

* `gen_relic_table.py` 가 **스스로 검산한다**(`check()`) — 모르는 효과 타입 · 겹치는 ID/아이콘 ·
  발굴 가중치 합 ≠ 100 · 그룹에 빠진 상황 · accept/decline 짝이 안 맞는 선택지 그룹.
  **하나라도 걸리면 표를 쓰지 않는다**(틀린 표를 만드는 것이 안 만드는 것보다 나쁘다).
* 표 — 유물 45 · 효과 27 · 2중 슬롯 17 · 발굴 결과 가중치 합 100 · 대사 41 · 선택지 8
* 에셋 — 유물 45 · 아이콘 45(원화) · `RelicDigTable` · `RelicDialogueTable`(대사 41 · 선택지 8)
* `recompile_scripts` **에러 0 · 경고 0** · 콘솔 에러 0
* MCP **46건 전부 성공** · `HUD_Dig` 하이라키·좌표 실측 확인 · 폰트 **242/242** 네오둥근모
* 아이콘 신규 14장 눈검사 완료

### 아직 확인 못 한 것 (유저가 볼 것)

1. ★★ **플레이 실측을 못 했다** — 느낌표 클릭 → 창 → yes/no → 발굴 → 결과 창의 한 바퀴.
2. ★ **밸런스는 계산으로만 잡았다.** 실제로 돌려 보고 «에픽이 시시하다»/«일반이 너무 세다» 가
   있으면 표의 `value_01`·`value_03` 만 고치고 `gen_relic_table.py` → `gen_relic_assets.py` →
   `relic_icon_build.py` 를 돌리면 된다.
3. **결과 창이 잦은지** — 발굴은 유저가 눌러서 시작하므로 창이 떠도 자연스럽다고 봤지만,
   전투 한복판에서 뜨면 성가실 수 있다. 성가시면 결과만 로그로 돌리면 된다(한 줄 삭제).
4. **표 Ver01 은 남겨 두었다** — 볼트에 두 파일이 공존한다. Ver02 가 정본이고 코드도 Ver02 를
   읽는다. Ver01 을 지울지는 유저 판단.

### 씬반영요청 목록

- 없음 (씬 편집을 이 세션에서 MCP 로 마쳤다)

---

## UI-54. 이벤트·유물 대사 2차 문체 정리 — 되풀이되는 「낱말」 걷어내기 (2026-08-24, 4차)

### 무엇을 / 왜

유저 지시: *"지금 이벤트랑 유물 스크립트 중복된 표현 제외하고 문어체 살려서 수정해줘
테이블 부터 수정하고 이후에 게임에 적용"*.

★★ **1차(직전 세션)에서 이미 한 번 다듬은 글이다.** 그래서 먼저 «남아 있는 되풀이가
무엇인지» 를 <b>세어서</b> 확인했다 — 고칠 데를 눈으로 찾으면 결국 «내가 어색하다고 느낀
문장» 만 고치게 되고, 그것은 취향이지 정리가 아니다.

표 전체(이벤트 대사 43 + 결과 대사 86 + 유물 대사 41 + 서사 45 + 결과 문구·선택지 =
**319 토막**)를 어절 n-gram 으로 셌더니 **문장 되풀이는 이미 없었다** —
3어절 이상 겹치는 것이 **하나도 없다**(1차가 제 일을 했다).

**남아 있던 것은 «낱말» 이었다:**

| 낱말 | 전 | 후 | 판단 |
|---|---|---|---|
| **자리** | **45** | **10** | ★★ 파낸 데·곪은 데·갈라진 틈·대열의 빈 데·흉터를 <b>전부</b> 이 한 낱말로 불렀다 |
| 대열 | 15 | 7 | 진형을 뜻할 때만 남겼다 |
| 함께·같이 | 12 | 4 | |
| 언제나 | 10 | 3 | 격언투의 표시였다 |
| 무언가 | 9 | 2 | |
| 박동 | 8 | 5 | ← 넥서스의 심장을 가리키는 <b>고유한</b> 말이라 남겼다 |
| 만큼 | 7 | 2 | |
| 곳간 | 14 | 13 | ← 양분 저장고의 이름이라 <b>거의 그대로</b> 두었다 |

**고친 것** — `gen_relic_table.py`(유물 대사·서사·결과 문구 23군데) ·
**신규** [Tools/table_update_20260824_dialogue_polish2.py](Tools/table_update_20260824_dialogue_polish2.py)(이벤트 표 50군데)
**씬 변경** — 없음 · **C# 변경** — 없음

---

### UI-54-1. ★★ 「자리」 45번이 가장 큰 문제였다

45번 중 어느 것도 제 모양을 갖지 못했다. 같은 말로 다섯 가지를 가리키면
독자는 그 말을 <b>읽지 않고 넘긴다</b>. 그래서 **가리키는 것에 맞게 갈랐다**:

| 원래 | 무엇을 가리키던 것인가 | 바꾼 말 |
|---|---|---|
| 붕대가 곪은 **자리**를 덮었습니다 | 몸의 살 | 곪은 **살** |
| 갈라진 **자리**를 눌러 덮었습니다 | 성역의 균열 | 갈라진 **틈** |
| 패인 **자리**가 하나씩 메워졌습니다 | 얼금뱅이 소의 구덩이 | 패인 **구덩이** |
| 대열에서 한 **자리**가 비었고 | 사람 하나 | **앞줄**에서 하나가 |
| 같은 **자리**를 다시 여는 손 | 오래된 흉터 | 같은 **흉** |
| 적이 없는 **자리**를 향해 | 허공 | **허공** |
| 파낸 **자리**에서 흙이 흘러내립니다 | 발굴 구멍 | 파낸 **구멍** |

★ **남긴 열 곳은 「자리」여야 하는 곳이다** — 「곪은 자리」(이벤트 205010 의 <b>제목</b>이
  그것이다) · 「영웅의 자리」 · 「떼어낸 자리에서 김이 피어오릅니다」.
⚠ **다 지우지 않았다.** 낱말 수만 보고 기계적으로 걷어내면 문체가 아니라
  <b>어휘력 과시</b>가 된다. 세는 것은 <b>후보를 찾는 데까지</b>이고, 남길지는 문장마다
  따로 판단했다.

### UI-54-2. 되풀이를 걷어내자 <b>가려져 있던 되풀이</b>가 드러났다

★ 이것이 이번에 가장 조심한 대목이다. 「언제나」를 빼자 **그 자리에 있던 다른 반복이
보였다**:

* 300037 「먼저 치는 쪽은 **언제나** 제 살을 **먼저** 내놓습니다」 → 「언제나」를 빼니
  한 칸에 **「먼저」가 셋**이 됐다(앞 문장에도 있었다) → **「선수를 치는 쪽은 제 살을
  앞서 내놓습니다」**.
* 300006 「곳간에는 여유가」 → 「남긴 **몫**이」로 바꿨더니 앞 문장의 「제 **몫**을」과
  겹쳤다 → **「덜어낸 것이 곳간에 쌓였고」**.
* 발굴 대사에서도 같은 일이 났다 — 「자리」를 「흙」·「땅」으로 바꾸자
  **한 그룹 안에서** 「흙」이 두 번 나왔다(그룹 5 는 발견 대사가 이미 「흙 위에 낯선
  자국」이다) → 「닿았던 **곳**을 다시 헤칩니다」.

→ **고치고 나서 n-gram 을 다시 돌렸다.** 3어절 이상 겹침 **0** · 2어절은 넷만 남았고
  그 넷은 전부 **뜻이 되풀이인 것**이다:
  「다음 파도」(웨이브) · 「이 몸」(성역=몸이라는 이 세계의 중심 비유) ·
  「한 사람을」(제물 선택지 그 자체).

### UI-54-3. 문어체 — 격언투를 <b>줄이는</b> 방향이었다

유저의 «문어체 느낌이 살게» 는 <b>문장을 더 꾸미라는 뜻이 아니었다</b>(1차 지시에
«작위적임» 이 있었다). 이미 전부 「…습니다」체다. 실제로 손댄 것은
**둘째 줄이 매번 격언으로 끝나던 것**이다:

* 「…은 **언제나** …합니다」 5 → 0(이 표에서) · 「**반드시**」 2 → 1
* 「**대신**」·「**만큼**」으로 대구를 만드는 문장 11 → 4
  (「화살이 짧아진 **만큼** 방패는 두꺼워졌습니다」 → 「화살은 짧아지고 방패는
  두꺼워졌습니다」 — 대구를 풀면 문장이 오히려 담백해진다)

★ 대신 <b>문어체 어휘</b>로 갈아 끼운 것은 남겨 두었다 —
  「함께 태웠습니다」→「**아울러** 태웠습니다」 · 「자리를 잡았습니다」→「**진을 쳤습니다**」 ·
  「정확함을 대신합니다」→「정확함을 **메웁니다**」.

### UI-54-4. 표를 먼저 고쳤다 — <b>두 곳의 정본이 다르다</b>

유저 지시(*"테이블 부터 수정하고"*)대로, 그리고 1차와 같은 이유로:

| 무엇 | 정본 | 어떻게 고쳤나 |
|---|---|---|
| **이벤트** 대사·결과 대사 | **xlsx** (Ver013 은 생성 스크립트가 없다 — 손으로 만든 표다) | `table_update_20260824_dialogue_polish2.py` (Excel COM) |
| **유물** 대사·서사·결과 문구 | **`gen_relic_table.py`** (표를 통째로 다시 쓴다) | 스크립트를 직접 고쳤다 |

⚠ **유물 쪽을 xlsx 로 고치면 다음 굽기에 되돌아간다** — `gen_relic_table.py` 가 표를
  통째로 다시 쓰기 때문이다. 반대로 **이벤트 쪽은 스크립트가 없어서** 표가 정본이다.
  같은 «표를 고친다» 인데 손이 반대로 가야 한다.
⚠ 편집은 **Excel COM · DispatchEx** — openpyxl 로 저장하면 서식이 상한다(UI-17절).
⚠⚠ **굽는 순서를 지켰다** — `gen_relic_table.py` → `gen_relic_assets.py` →
  **`relic_icon_build.py`**. 가운데 것이 아이콘을 임시 그림으로 덮으므로 마지막이
  반드시 필요하다(UI-53-4 에서 실제로 한 번 덮였던 그 문제다).

### 검증

* n-gram — **3어절 이상 겹침 0** · 2어절 3회 이상은 4건(전부 «뜻이 되풀이인 것»)
* 낱말 — 자리 45→10 · 대열 15→7 · 함께·같이 12→4 · 언제나 10→3 · 무언가 9→2 · 만큼 7→2
* 표 — 이벤트 `event_script` 7 · `result_script` 43 / 유물 대사 14 · 서사 6 ·
  결과 문구 2 · 선택지 1 = **73줄**
* `gen_relic_table.py` **자체 검산 통과**(효과 타입 · 중복 ID/아이콘 ·
  발굴 가중치 합 100 · 그룹별 상황 · accept/decline 짝) — 유물 45 · 대사 41 · 선택지 8
* 에셋 — 이벤트 43개 · 선택지 86행 · 유물 45 · 아이콘 **45/45 원화 복구** ·
  `RelicDialogueTable`(41+8) · `RelicDigTable`
* ★ **바뀐 필드를 세어 확인했다** — `eventScript` 7 · `resultScript` 43 · `script` 14 ·
  `relicFlavor` 6 · `outcomeScript` 2 · `choiceText` 1 = **73**.
  <b>수치·보상·`result_effect` 는 한 줄도 바뀌지 않았다</b>(밸런스 무변경).
* ★ **에셋 guid 무변경** 확인 — `gen_event_assets.py` 가 에셋을 지우고 다시 만들지만
  `.meta` 를 건드리지 않아 씬·프리팹 참조가 그대로다.
* **C# 변경 없음** — 대사가 코드에 박힌 곳은 `RelicDigPanel` 의
  `fallbackAccept/Decline`(표가 없을 때만 쓰는 대체 문구)뿐이고, 그 두 줄은
  `DigChoice 640001` 과 같은 문장이라 손댈 것이 없었다.

### 아직 확인 못 한 것 (유저가 볼 것)

1. ★ **유니티에서 `Assets/Refresh` 가 필요하다** — 이 세션에는 유니티 MCP 가 붙어 있지
   않아 에셋 새로고침·컴파일을 하지 못했다. C# 을 안 건드렸으므로 컴파일 위험은 없다.
2. ★ **창에서 눈으로 볼 것** — 줄 수와 길이를 유지했으므로 `Body`(560x340)에서 잘릴
   일은 없다고 봤지만, 실제로 이벤트 창·발굴 창을 한 번씩 띄워 확인이 필요하다.
3. **남긴 되풀이가 거슬리는지** — 「곳간」 13 · 「박동」 5 · 「자리」 10 은 <b>일부러</b>
   남긴 것이다. 거슬리면 그 낱말만 다시 세어 갈라내면 된다(방법은 위와 같다).

### 씬반영요청 목록

- 없음 (씬·프리팹 변경이 없다. 유니티에서 `Assets/Refresh` 만 하면 된다)

## UI-55. ★★★ 도움말(튜토리얼) 전면 구현 — 조언 카드 · 백과 · <b>빨간 테두리로 화면 짚어 주기</b> · 사건 보상 유물 9종 (2026-08-24, 13차)

### 무엇을 / 왜

유저 지시 일곱(들어온 순서대로):

1. *"지금 듀토리얼 테이블에 들어가 있는 거 듀토리얼 용 UI 캔버스 만들어서 도움말 방식으로 듀토리얼 구성해줘"*
2. *"최초로 해당 기능을 눌렀을때 나타나게 그리고 지금 발굴 가능 칸에 느낌표 뜨는거 맞음? 확인좀"*
3. *"보면 계속 남는게 아니라 발굴을 완료하면 사라져야지"*
4. *"이벤트 보상용 전용 에픽 유물 3개만 추가해 … 이벤트 내용이랑 관련있는 유물이었으면 좋겠음 예상치못한 획득의 재미"*
5. *"등급별로 3개씩 이벤트 보상에 유물획득도 넣어"* · *"MCP 이용 해서 직접 생성, 수정 하고 추가한 데이터들 반드시 테이블에도 반영해"*
6. *"도움말 뜨면 게임 일시정지 되야함"*
7. *"말 좀 더 다듬어줘 지금 너무 ai 티 나니까 … 게임을 아예 처음 하는 사람도 이해할 수 있도록. 자세히 보기에서 실제 ui로 연결하고 <b>빨간 테두리 선으로 하나하나 설명</b>해주는 기능"*

진행상황 **140-6절이 «아직 안 한 것» 으로 남긴 네 가지를 전부 마쳤다.**

**신규 C#** — `Help/HelpTableSO.cs` · `Help/HelpService.cs` · `UI/HelpPanel.cs` ·
`UI/HelpCardPanel.cs` · `UI/HelpTourPanel.cs` · `UI/ReadingPause.cs` · `Editor/HelpMenu.cs`
**고친 C#** — `UI/HudExclusive.cs`(`AnyOpen`) · `UI/HudHotkeys.cs`(F1 · Esc 양보) ·
`UI/ActionPanel.cs`(도움말 버튼) · `Relics/RelicDefinitionSO.cs`(`RelicSource.Event`) ·
`Relics/RelicRegistry.cs` · `UI/RelicPanel.cs` · `Events/EventRewardService.cs`(`relic_gain`)
**신규 스크립트** — `help_string_merge.py` · `gen_help_assets.py` · `mcp_build_help_ui.py` ·
`table_update_20260824_help_rewrite.py` · `table_update_20260824_event_relic_reward.py`
**고친 스크립트** — `gen_relic_table.py` · `gen_relic_assets.py` · `relic_icon_build.py`
**씬 변경** — 있음(`Help_Root` 캔버스 신설 · 액션 버튼 · `GameSystems`) · `save_scene`

---

### UI-55-1. 확인 요청 둘에 대한 실측 답 (지시 2·3번)

| 물음 | 실측 결과 |
|---|---|
| 발굴 칸에 느낌표가 뜨는가 | **맞다.** `UI_Root/DigOverlay/DigMarkerTemplate` 의 TMP 가 느낌표 한 글자(22pt · 글자색 0.1,0.09,0.06) |
| 모양 | ⚠ Image 색은 호박색(0.98,0.85,0.35)인데 **스프라이트가 비어 있다** — 동그라미가 아니라 **네모난 판**이다 |
| 발굴을 완료하면 사라지는가 | **이미 사라진다.** `Complete()` 가 자리를 목록에서 지우고 `UpdateMarkers()` 가 남는 표식을 끈다 |

★ 「한 번 보면 계속 남는다」는 주석은 **«시야를 벗어나도 남는다»** 라는 뜻이고
  «다 파낸 뒤에도 남는다» 가 아니다. 코드 변경 없음.

### UI-55-2. ★★★ 계기를 «버튼» 이 아니라 «결과» 에 걸었다 (지시 1번의 핵심)

유저 지시는 «최초로 해당 기능을 눌렀을 때» 다. 그런데 한 기능을 누르는 통로가 여럿이다 —
액션 버튼 · 창 안의 버튼 · 단축키 · 로스터 우클릭. **버튼마다 세면 반드시 하나를 빠뜨리고**,
통로가 늘 때 조용히 어긋난다. 그래서 **기능이 실제로 일어난 자리**를 듣는다.

★★ **그 덕에 도움말이 다른 시스템을 한 줄도 고치지 않았다.** 계기 23개가 전부
**이미 있는 public 이벤트**거나 **이미 있는 public 상태**였다:

| 통로 | 계기 |
|---|---|
| `WaveManager.OnPhaseChanged` | 첫 정비 · 전투 시작 · 광폭화 |
| `DamageableUnit.OnAnyDied` / `OnAnyMissed` | 아군 사망 · 중립 처치 · 첫 빗나감 |
| `CharacterCreationService.OnCreated` · `CharacterUpgradeService.OnUpgraded` | 생성 · 강화 |
| `SquadService.OnSquadsChanged` · `RallyPointService.OnPointsChanged` · `CharacterTactics.OnAnyOrderChanged` | 부대 · 집결지 · 전술 |
| `RelicInventory.OnChanged` · `EventService.OnEventChanged` · `EpicSubjugationService.OnChanged` | 유물 · 사건 · 에픽 |
| `ErosionService.OnMentalErrorTriggered` · `HeroAwakeningService.OnAwakened` | 정신 이상 · 각성 |
| **주기 검사(6개만)** | 보스 등장 · 발굴 표식 · 침식 50 · 건설 모드 · 자동 저장 · 배속 |

★ 주기 검사는 **뜨고 나면 목록에서 빠진다** — 여섯이 다 뜨면 검사가 스스로 멈춘다.

### UI-55-3. ★★★ 플레이 실측으로 찾은 구멍 둘 (이번 세션의 가장 큰 소득)

**둘 다 «코드는 맞는데 안 뜨거나 엉뚱하게 뜨는» 종류라 눈으로 봐야만 찾을 수 있었다.**

#### ㉠ 첫 정비 단계 이벤트를 **영영 놓치고 있었다**

`WaveManager.Start()` 가 곧바로 `StartGame()` → `BeginPreparation()` 을 부른다. 즉
**첫 정비 이벤트는 Start 단계에서 이미 터진다.** 그런데 `HelpService` 는 `Update` 에서
구독하므로 그 한 번을 못 듣는다 → **가장 먼저 떠야 하는 두 장(「성역과 넥서스」·「웨이브와
정비 시간」)이 아무 소리 없이 안 뜬다.** 이벤트를 «듣는» 방식의 유일한 약점이 이것이다.

→ 붙는 즉시 **지금 단계를 한 번 평가한다**(`CatchUpWave`). 계기는 «처음 한 번» 이라
  따라잡아도 중복이 나지 않는다.

#### ㉡ 판이 시작되자마자 **엉뚱한 카드**가 떴다 — 「전술 지침」

아무도 전술을 만지지 않았는데 그 카드가 떴다. 원인은 이 프로젝트의 시작 절차다 —
`UnitSpawner.SpawnAll` 이 캐릭터를 만들고 `AutoSquadInitialCharacters` 로 부대를 묶고,
캐릭터마다 `CharacterTactics.Apply` 가 한 번 돌면서 `OnAnyOrderChanged` 를 쏜다.
**«판이 스스로 자기를 차리는 첫 프레임» 이 «유저가 누른 것» 으로 잡혔다.**

→ **시작 절차를 고치지 않았다.** 그쪽은 도움말과 아무 상관이 없고, 거기에 «도움말은 빼고» 를
  심으면 이 서비스가 다른 시스템을 알게 된다(UI-55-2 의 요지가 무너진다).
  **받는 쪽에서 걸러 낸다** — `startupGraceSeconds 1.5` 동안의 계기는 버린다.
  ⚠ **첫 정비만 예외다** — 그것은 «유저가 누른 것» 이 아니라 **판의 상태**이고 t=0 에 맞다.

### UI-55-4. ★★ 「자세히 보기」 → <b>빨간 테두리로 화면을 짚는다</b> (지시 7번)

글로만 설명하면 「강화」가 무엇인지는 알아도 **어디를 눌러야 하는지**는 모른다.
그래서 표에 **`HelpStep` 시트**를 새로 만들었다 — `help_id · step_order · target_path · step_text`.

★★ **테두리를 «막대 넷» 으로 그렸다.** 9-slice 테두리 스프라이트가 없어서 Image 하나로
  그리면 **속이 꽉 찬 네모**가 되어 짚으려는 것을 덮어 버린다. 위·아래·왼·오른 얇은 막대 넷이면
  그림 없이도 가운데가 비어 **대상이 그대로 보인다**. 천천히 깜빡이게 해서(`unscaledTime`)
  «UI 의 일부» 가 아니라 «지금 이것을 보라» 로 읽히게 했다.
★ **말풍선은 대상을 가리지 않는 쪽으로 붙는다** — 위에 자리가 있으면 위, 없으면 아래.
  가운데 고정으로 두면 «가리키는 것을 말풍선이 덮는» 일이 반드시 생긴다.
★ **어두운 막을 깔지 않았다** — 가리켜야 할 것이 화면의 UI 인데 화면을 어둡게 덮으면
  대상이 같이 어두워진다. 대신 **거의 투명한 막**(알파 0.02)으로 **클릭만** 막는다.
⚠ **창 안의 칸을 가리키지 않았다** — 창은 평소 닫혀 있어 짚어도 안 보인다. 대신
  **그 창을 여는 버튼**을 가리킨다. 그것이 유저가 실제로 눌러야 하는 곳이다.
⚠ **월드 오브젝트도 못 짚는다**(넥서스·몬스터) — UI 좌표로 계산하므로 RectTransform 이 아닌
  것은 대상이 될 수 없다. 그런 단계는 `target_path` 를 비워 **글만** 보여준다.
★ 단계가 없는 항목(「명중과 크리티컬」)은 **백과를 여는 예전 동작으로 되돌아간다** —
  «눌러도 아무 일이 없는 버튼» 을 만들지 않는다.

### UI-55-5. ★★ 문구 54칸을 다시 썼다 — <b>«AI 티» 가 무엇인지 세어서 찾았다</b>

고칠 데를 느낌으로 찾으면 결국 취향이 된다. 그래서 앞 초안에서 **무엇이 되풀이되는지 셌다**:

| 버릇 | 앞 초안 | 왜 «AI 티» 로 읽히는가 |
|---|---|---|
| 문장 끝의 격언 | 27개 중 **25개**가 ★ 한 줄로 끝났다 | 설명이 아니라 **논평**이다 |
| «…» 기호 | **31군데** | 사람이 쓰는 글에 이렇게 자주 나오지 않는다 |
| 추상 요약 | 「모든 판단의 기준은 …입니다」 | 처음 하는 사람에게 **아무것도 알려주지 않는다** |
| 화면 이름 없음 | 「액션 패널의」 | 초보자는 «액션 패널» 이 어디인지 모른다 |

**규칙 다섯** — ① 한 문장에 한 가지 ② ★·⚠·«» 를 **전부 걷어내고** 강조만 남긴다
③ 「오른쪽 버튼 묶음의 강화」처럼 **자리를 함께** 적는다 ④ 논평하지 않고 «그래서 무엇을 하면
되는가» 를 적는다 ⑤ 말투는 문어체 ~입니다 유지.

⚠ **뜻은 하나도 바꾸지 않았다** — 규칙·수치·조건은 앞 초안 그대로다. «다시 쓴다» 가
  «다시 기획한다» 가 되면 도움말이 게임과 어긋난다.
★ **제목 27개는 그대로 뒀다** — 이름표라 다듬을 것이 없었다(그래서 81칸 중 54칸만 바뀌었다).
★ 스크립트가 **스스로 검산한다** — 반말로 끝나는 줄 0건 · 걷어내기로 한 기호 0건.
  ⚠ 말투 검사는 «다.» 가 아니라 **«니다»** 로 한다(140-7절이 오탐 55건으로 배운 규칙).

### UI-55-6. ★★ 일시정지의 주인이 <b>넷</b>이 될 뻔했다 (지시 6번)

`GameSpeedPanel` 주석이 못박아 둔 문장 — *"timeScale = 0 의 주인이 둘이 되는 것이 이 기능의
유일한 위험이다."* 지금 주인은 둘(일시정지 버튼/P · 패배·승리 화면)인데 이번에 **읽는 판이
셋** 늘었다(조언 카드 · 백과 · 짚어 주기). 셋이 각자 0 을 쓰면 「닫았는데 안 흐른다」가 난다.

→ **`ReadingPause` 한 클래스로 모았다.** 규칙은 하나다 — **내가 멈춘 것만 내가 푼다.**
  ㉠ 유저가 P 로 멈춰 둔 채 카드가 뜨면 닫아도 **계속 멈춰 있다**
  ㉡ 패배·승리 화면에서는 `SetPaused` 의 가드에 걸려 아무 일도 안 한다
  ㉢ 일시정지 버튼 문구가 「재개」로 바뀌어 **«왜 멈췄는지» 가 화면에 설명된다**
★ 창이 하나 더 늘어도 **필드 하나 + 두 줄**이면 붙는다(`HudExclusive` 가 생긴 것과 같은 판단).

### UI-55-7. ★★ 사건 보상에 유물을 넣었다 — 등급별 3개씩 (지시 4·5번)

**신규 «사건 전용» 에픽 3종.** 위 16종은 «침입자에게서 빼앗은 것»(보스의 장기)인데
이 셋은 **어디서 왔는지 알 수 없는 것**이다 — 그것이 «예상치 못한 획득» 의 결이다.

| 유물 | 효과 | 어느 사건이 주는가 | 왜 그 사건인가 |
|---|---|---|---|
| 720017 값이 붙지 않은 은혜 | 저체력 방어 +8 · 재생 +5 | 205020 「대가 없는 은혜」 / «사양한다» | ★ **사양했는데 남아 있었다** |
| 720018 스스로 메운 살 | 웨이브 보호막 22%/20초 · 체력 +5 | 206005 「메워지는 살」 / «심장을 채운다» | 아무도 꿰매지 않았는데 아물었다 |
| 720019 돌아갈 곳의 기억 | 처치 시 침식 −4 · 침식 지연 30% | 206002 「꺾인 무릎」 / «어깨를 붙들어준다» | 돌아갈 수 없음을 알고도 기억한다 |

★ 셋이 **에픽에 비어 있던 효과 칸**을 메운다 — 저체력 방어 · 웨이브 보호막 · 침식 계열은
  지금까지 레어까지만 있었다(같은 효과에 «더 큰 수치» 가 등급의 규약이다).

**일반 3 · 레어 3** 은 이미 있는 유물에서 사건 내용에 맞춰 골랐다 —
「식후 정리 / 모조리 불태워라」→**오른 열**(태운 열이 몸에 남는다) ·
「올라온 양분 / 전부 먹여라」→**부어오른 자리** · 「심장의 잡음 / 손발을 먹여라」→**붉은 실** ·
「노련해진 손」→**각성한 수지상세포** · 「낮은 노래」→**서늘한 해열** ·
「메워지는 살 / 손발을 채운다」→**부푼 림프절**.

#### ★★ `RelicRegistry` 의 <b>`default:` 가 함정이었다</b>

`RelicSource.Event` 를 더하기만 하면 **발굴·처치 뽑기에서 그대로 튀어나온다** —
그 `switch` 의 `default` 가 «일반 풀» 이기 때문이다. 「전용」이 곧 거짓이 된다.
→ `case RelicSource.Event: break;` 를 **명시적으로** 뒀다. 표 쪽에서도
`gen_relic_table.py` 의 검산에 «사건 전용인데 가중치가 0 이 아니다» 를 더했다.

#### ⚠ 아홉을 고른 이유는 <b>빈 칸이 열 개뿐</b>이었기 때문이다

표의 보상 칸은 **두 개뿐**(`reward_type_01/02`)이고 86개 선택지 중 **76개가 둘 다 차 있다.**
이미 있는 보상을 밀어내면 그것은 **밸런스 변경**이지 «보상 추가» 가 아니다.
⚠ 그래서 「봉화의 흔적」·「영웅의 자리」처럼 **유물과 더 잘 맞는 사건** 몇은 쓰지 못했다.
  그쪽에 붙이려면 표와 코드에 **셋째 보상 칸**이 필요하다 — 요청 범위 밖이라 손대지 않았다.

### UI-55-8. 표를 정본으로 다뤘다 (지시 5번의 *"반드시 테이블에도 반영해"*)

| 무엇 | 정본 | 어떻게 |
|---|---|---|
| 도움말 문구·단계 | **도움말 표 xlsx** (생성 스크립트가 «초안만» 굽는다) | `table_update_20260824_help_rewrite.py` (openpyxl) |
| 도움말 → 스트링 | **도움말 표가 `help_*` 키의 정본** | `help_string_merge.py` — 매번 덮어쓴다 |
| 이벤트 보상 | **이벤트 표 xlsx** (생성 스크립트가 없다) | `table_update_20260824_event_relic_reward.py` (**Excel COM**) |
| 유물 | **`gen_relic_table.py`** (표를 통째로 다시 쓴다) | 스크립트를 직접 고쳤다 |

⚠ **`help_*` 문구는 도움말 표에서 고칠 것** — 스트링 키 테이블에서 고치면 다음 병합에
  되돌아간다(규칙을 한 곳으로 못박기 위해 일부러 그렇게 했다. `--keep` 으로 뒤집을 수 있다).
⚠⚠ **유물 굽는 순서를 지켰다** — `gen_relic_table.py` → `gen_relic_assets.py` →
  **`relic_icon_build.py`**. 가운데 것이 아이콘을 임시 그림으로 덮으므로 마지막이 반드시 필요하다.

### 검증

* `recompile_scripts` **에러 0 · 경고 0** · 플레이 중 콘솔 **에러 0 · 경고 0**
* ★★ **플레이 실측** — 판을 시작하자 「성역과 넥서스」 카드가 떴고(분류 «기본» · 새 문구),
  **일시정지 버튼 문구가 「재개」** 로 바뀐 것을 확인했다(= 카드가 실제로 멈췄다).
  ⚠ 그 실측이 UI-55-3 의 구멍 둘을 찾아냈다 — 고친 뒤 다시 확인했다.
* ★★ **짚어 줄 자리 61단계 전수 검사**(에디터 메뉴) — 화면을 가리키는 **47개 전부 찾음** ·
  못 찾은 자리 **0개** · 글만 보여주는 단계 14개
* 도움말 에셋 — 항목 **27개** · 계기가 붙은 항목 24 · 백과 전용 3 ·
  단계가 붙은 항목 **26/27**(「명중과 크리티컬」만 없다 — 화면의 칸이 아닌 규칙이다)
* 문구 검산 — **반말로 끝나는 줄 0건** · 걷어내기로 한 기호 **0건** · 54칸 덮어씀
* 스트링 — `help_*` 키 **81개** 내보냄 · 스트링 총 **378키** · 하이퍼링크 **280칸 재생성** ·
  정의된 이름 **378/378 확인**
* 유물 — 총 **48종**(일반 16 · 레어 13 · 에픽 19) · 사건 전용 3 · 아이콘 **48/48** ·
  `.meta` **0개 변경**(guid 유지) · `gen_relic_table.py` 자체 검산 통과
* 이벤트 — `relic_gain` 보상 타입 1줄 추가 · 선택지 **9줄** 갱신 · 에셋 43개·선택지 86행 재생성 ·
  `Event_205020` 에서 `rewardType02: relic_gain` / `rewardValue02: 720017` 확인
* 씬 — `Help_Root`(sortingOrder 20) · `HUD_Help`·`HUD_HelpCard`·`HUD_HelpTour` **셋 다 꺼진 채 저장** ·
  TMP 폰트 **267/267 네오둥근모** · `HUD_Actions` 높이 396(버튼 8개) · `save_scene`
* MCP 요청 **190건 실패 0건**

### 아직 확인 못 한 것 (유저가 볼 것)

1. ★★ **「자세히 보기」를 눌러 안내가 도는 것은 눈으로 봐야 한다.** 경로 47개가 다 있고
   코드는 컴파일되지만, **빨간 테두리의 굵기·말풍선 자리**는 실제로 띄워 봐야 판단이 된다.
   거슬리면 인스펙터의 `borderThickness`·`bubbleGap`·`pulseSeconds` 만 고치면 된다.
2. ★ **문구를 읽어 볼 것** — 54칸을 다시 썼다. 아직 «AI 티» 가 남았다고 느껴지면
   `table_update_20260824_help_rewrite.py` 의 `TEXTS` 만 고쳐 다시 돌리면 된다(파이프라인 4줄).
3. **발굴 표식을 동그라미로 바꿀지** — 지금은 호박색 **네모**다(스프라이트가 비어 있다 · UI-55-1).
4. **에픽 유물 3종의 수치** — 에픽 대역(방어 +8 · 저항 +20 · 그 외 +5)에 맞췄고 % 는
   레어의 1.5배쯤으로 잡았다. 세 보이면 `gen_relic_table.py` 의 그 세 줄만 고치면 된다.
5. **셋째 보상 칸을 만들지** — 만들면 「봉화의 흔적」·「영웅의 자리」에도 유물을 붙일 수 있다
   (UI-55-7 의 ⚠).
6. ⚠ **`gen_string_table.py` 가 남긴 경고 하나** — `웨이브 몬스터 테이블.xlsx` 에
   **`wave_mid_boss` 시트가 없다**(지금 시트는 `wave_nom`·`wave_top_boss`·`Skill`·
   `Skill_Type`·`first_Stat`·`work`). **이번 작업과 무관한 기존 문제**이고 손대지 않았다 —
   중간보스 이름·칭호 스트링이 수집되지 않는 상태다.

### 씬반영요청 목록

- 없음 (씬 편집을 이 세션에서 MCP 로 마쳤다)

---

## UI-56. ★★★ 도움말 배선을 다시 짰다 — 「자세히 보기」가 <b>그 창을 직접 띄우고 안을 짚는다</b> (2026-08-24, 14차)

### 무엇을 / 왜

유저 지시: *"듀토리얼 이벤트의 배선이 어수선해 해당 버튼을 누르면 해당 기능에 대한 설명이
먼저 나온 후 자세히 보기를 누르면 <b>다음 기능을 설명하는 것이 아니라 해당 ui를 직접 띄워서
설명하는 방식</b>으로 만들어야 하고, 단순히 넥서스가 파괴되면 게임이 종료된다는 간단한 규칙
같은거 (다른 ui와 연결되지 않아도 되는 기능)은 <b>그냥 자세히 보기 없어도 됨</b>. 예를 들어
전술 지침을 누르면 전술 지침에 대한 간략한 설명을 해주는 ui가 나와야 하고 거기서 자세히
보기를 누르면 <b>실제 전술 지침 ui를 띄워놓고 각 영역에 대해</b> 빨간색 테두리로 설명해
주어야 함"*.

**고친 C#** — `UI/HudExclusive.cs`(`TryOpen`) · `UI/HelpTourPanel.cs`(창 열기·되돌리기·매 프레임
재배치) · `UI/HelpCardPanel.cs`(버튼 숨김·가운데 정렬) · `UI/HelpPanel.cs`(판단 통일) ·
`Help/HelpTableSO.cs`(`openPanelPath`) · `Editor/HelpMenu.cs`(안내 시험 메뉴 3개)
**고친 스크립트** — `table_update_20260824_help_rewrite.py`(`OPEN_PANEL` · `STEPS` 전면 교체 ·
«한 UI 안» 검산) · `gen_help_assets.py`(`open_panel` 읽기 · 같은 검산)
**표 변경** — `Help` 시트에 **`open_panel` 열 신설** · `HelpStep` 시트 **61줄 → 44줄로 전면 교체**
**씬 변경** — 없음(구조는 그대로. 저장만)

---

### UI-56-1. ★★★ «어수선하다» 의 정체를 먼저 찾았다

앞 초안(UI-55)의 단계는 <b>단계마다 다른 HUD</b>를 짚고 있었다. 「전술 지침」 항목이 이랬다:

```
1단계  UI_Root/HUD_Actions/Buttons/TacticsButton   «이 버튼을 누르면 창이 열립니다»
2단계  UI_Root/HUD_Roster                          «먼저 대상을 고르십시오»
3단계  (글만)                                       «전방은 앞에서 받아내고…»
```

**눈이 화면을 세 번 건너뛰는데, 정작 «전술 지침 창 안에 무엇이 있는지» 는 하나도 알려주지
않았다.** 창을 여는 버튼만 가리키고 정작 그 창은 열지 않았으니, 「자세히 보기」가 사실상
**«다음 기능 소개»** 가 되어 있었다. 유저가 «어수선하다» 고 한 것이 그것이다.

⚠ 그렇게 만든 근거는 앞 세션의 이 판단이었다 — *"창 안의 칸을 가리키지 말 것. 창은 평소
닫혀 있어 짚어도 안 보인다."* **전제가 틀렸다.** 「짚어도 안 보인다」의 답은 «창 안을 가리키지
않는 것» 이 아니라 <b>«창을 열고 나서 가리키는 것»</b> 이었다.

### UI-56-2. ★★ 창을 «켜는 것» 과 «여는 것» 은 다르다

`gameObject.SetActive(true)` 로 켜면 <b>내용이 빈 창</b>이 뜬다. 각 창의 `SetOpen` 안에는
그것 말고도 세 가지가 더 있다 — `HudExclusive.OpenOnly`(다른 창 닫기 · 맨 앞으로) ·
`Rebuild()`(목록 다시 그리기) · 맵 클릭 모드 취소.

→ `HudExclusive.TryOpen(Transform, bool)` 을 만들었다. **규칙을 창들의 집이 있는 곳에 둔다.**

★ <b>인터페이스에 `SetOpen` 을 올리지 않았다.</b> `IExclusiveHudPanel` 구현체는 아홉인데
  그중 `EventPanel`·`RelicDigPanel` 둘은 **«상황이 만들어» 뜨는 창**이라 바깥에서 열 수 있는
  것이 아니다. 인터페이스에 올리면 그 둘에 <b>가짜 구현</b>을 넣어야 한다. 그래서 «열 수 있는
  창» 일곱만 `TryOpen` 에 <b>명시적으로</b> 적었다 — 컴파일러가 검사해 주고, 창이 늘 때 한 줄만
  더하면 된다.
⚠ 목록에 없는 창을 넘기면 `false` 를 돌려주고 **경고를 찍는다.** 조용히 `SetActive` 로 때우면
  «빈 창이 뜨는데 왜인지 모르는» 상태가 된다.

### UI-56-3. ★★ 창을 켠 <b>그 프레임</b>에는 좌표를 읽을 수 없다

창을 켠 즉시 자식의 `GetWorldCorners` 를 읽으면 <b>레이아웃이 아직 돌지 않아</b> 엉뚱한 값이
나온다(테두리가 화면 구석에 찍힌다). → `_waitLayout` 으로 **한 프레임 미루고** 다시 찾는다.

★ 그리고 `Reposition()` 을 <b>매 프레임</b> 부른다. 짚는 대상이 가만히 있지 않기 때문이다:
  ㉠ 창을 켠 첫 프레임 ㉡ 유저가 창을 <b>끌어 옮길</b> 때(`UiWindowDrag`)
  ㉢ 목록이 다시 그려지며 칸 높이가 바뀔 때. 한 번만 잡아 두면 그 셋에서 테두리가 남는다.
  비용은 막대 넷과 말풍선 하나의 좌표 계산뿐이다.

### UI-56-4. ★★ 「자세히 보기」가 <b>없어야 하는</b> 항목 열한 개

유저 지시: *"간단한 규칙 같은거 (다른 ui와 연결되지 않아도 되는 기능)은 그냥 자세히 보기
없어도 됨"*. 표에서 그 열한 항목의 단계를 <b>아예 지웠다</b>:

| 없앤 항목 | 왜 |
|---|---|
| 넥서스 · 광폭화 · 쓰러진 캐릭터 · 명중과 크리티컬 · 보스 · 중립 몬스터 | <b>규칙</b>이다. 짚을 칸이 없다 |
| 스스로 벌어지는 전투 · 화면 보기와 선택 | <b>개념·조작</b>이다 |
| 사건 · 유물 발굴 | 창이 <b>상황이 만들어</b> 뜬다 — 바깥에서 열 수 없다 |
| 포탑 건설 | 지금 <b>꺼진 기능</b>이다 |

★ 판단은 <b>`HelpTourPanel.HasTour` 한 곳</b>에서 한다 — 조언 카드와 백과가 같은 함수를 쓴다.
  두 곳에서 각자 따지면 한쪽에만 버튼이 남아 **«눌러도 아무 일이 없는 버튼»** 이 된다
  (이 프로젝트가 건설 버튼에서 이미 겪은 일이고, 그때는 알릴 통로가 하나도 없었다).
★ 버튼이 사라지면 「알겠습니다」가 <b>가운데로</b> 온다 — 버튼 하나가 한쪽에 치우쳐 있으면
  «옆 버튼이 사라졌다» 로 보인다. ⚠ 제자리 좌표는 <b>씬에서 읽어 기억</b>했다가 되돌린다
  (여기서 좌표를 지어내면 씬을 다시 구울 때 두 곳의 값이 갈린다).

### UI-56-5. ★★ 검산으로 못박은 규칙 — <b>한 항목의 단계는 «한 UI 안» 에서만 머문다</b>

«어수선함» 은 사람이 표를 늘릴 때 **반드시 다시 생긴다.** 그래서 규칙을 글로 적지 않고
<b>검산으로</b> 만들었다 — 표를 쓰는 스크립트와 에셋을 굽는 스크립트 <b>양쪽에서</b> 본다:

* 창을 여는 항목(`open_panel` 이 있는 것)은 <b>그 창 경로로 시작하는</b> 곳만 짚어야 한다
* 창이 없는 항목은 `UI_Root/HUD_xxx` <b>두 토막이 전부 같아야</b> 한다
* `open_panel` 만 있고 단계가 없으면 <b>빈 창만 뜨므로</b> 실패로 잡는다

### UI-56-6. 새 구조 — 16개에 안내, 그중 12개가 창을 연다

| 여는 창 | 항목 | 짚는 영역 |
|---|---|---|
| `HUD_Growth` | 강화 · 능력치 · 각성 · 침식 · 정신 이상 | `Info/EnhanceButton` · `Info/CostValue` · `Info/Level` · `Info/Name` · `Info/Note` · `Info/ErosionBack` · `Stats/Grid` · `Stats/GrowthTypes` · `Stats/PassiveGrid` · `RelicBar` |
| `HUD_Tactics` | 전술 지침 · 후퇴 기준 | `Col1/Pos` · `Col1/Type` · `Col2/Target` · `Col2/RetreatSlider` · `Col2/RetreatValue` · `Col2/RetreatAction` · `Col3/Wave` · `Col3/Summary` |
| `HUD_Squad` | 부대 · 집결지 | `Header/AddButton` · `Body/Grid` |
| `HUD_Relics` | 유물 장착 | `List` · `Detail/Effect` · `Detail/EquipButton` · `Detail/Wearer` |
| `HUD_Subjugate` | 에픽 토벌 | `Targets/List` · `Squads/List` · `Hint` |
| `HUD_Settings` | 저장 | `Body/SaveButton` · `Body/LobbyButton` · `Body/Status` |
| (창 없음) | 웨이브 · 에너지 · 캐릭터 생성 · 배속 | 늘 보이는 HUD 하나 안에서만 |

⚠ <b>내가 연 창만 내가 닫는다</b> — 유저가 이미 열어 두었으면 안내가 끝나도 열린 채 둔다
  (`ReadingPause` 와 같은 소유권 규칙이다).
⚠ 안내 중에 다른 안내를 시작하면 <b>앞 창을 먼저 닫는다</b> — 그러지 않으면 소유권 표시가
  사라져 내가 열어 둔 창이 영영 안 닫힌다. 다음 창의 배타 처리가 닫아 주는 것에 기대면
  «배타가 아닌 창» 이 하나 생기는 날 조용히 새어 나간다.

### UI-56-7. 검수용 메뉴를 더했다 — 「안내 시험」

「자세히 보기」는 <b>그 상황이 처음 왔을 때</b>만 뜨는 카드에서 눌러야 한다. 「전술 지침을
처음 바꿨을 때」를 만들려면 판을 처음부터 굴려야 하고, 카드를 닫으면 <b>다시 볼 방법이 없다</b>
(`show_once`). 검수 비용이 너무 커서 <b>항목을 골라 곧바로 띄우는</b> 메뉴 셋을 만들었다 —
`LastSanctuary/도움말/안내 시험 — 전술 지침 · 강화(성장 창) · 유물 장착`.
⚠ 플레이 중이 아니면 아무 일도 하지 않는다(런타임 UI 좌표를 읽어야 한다).

### 검증

* `recompile_scripts` **에러 0 · 경고 0** · 플레이 중 콘솔 **에러 0 · 경고 0**
* ★★ **플레이 실측 — 전술 지침 안내** : `UI_Root/HUD_Tactics` **활성**(창이 실제로 열렸다) ·
  테두리가 `Col1/Pos` 자리에 **274x48** 로 잡힘 · 말풍선이 그 **위로 피함**(−125, 223) ·
  「1 / 5」 · 본문 「전방, 중위, 후방 중 어디에 설지 고릅니다…」 ·
  일시정지 버튼 문구 **「재개」**(멈춤 확인)
* ★★ **창 갈아타기** : 이어서 강화 안내를 띄우니 `HUD_Tactics` **꺼지고** `HUD_Growth` 열림 ·
  테두리가 `Info/EnhanceButton` 자리에 **234x58** 로 잡힘
* ★★ **버튼 숨김** : 판을 시작해 뜬 「성역과 넥서스」 카드에서
  `MoreButton` **active=False** · `OkButton` 이 **anchorMin (0.5, 0) · pos (0, 42)** 로 가운데 이동
* **짚어 줄 자리 44단계 전수 검사** — 찾은 자리 **43/43** · 못 찾은 자리 **0** ·
  그중 비활성 36개(창 안의 영역이라 <b>정상</b>이다)
* 표 검산 — «한 UI 안» 규칙 통과 · 창을 여는 항목 **12개** · 안내가 뜨는 항목 **16개** ·
  안 뜨는 항목 **11개**
* 에셋 — `openPanelPath` **27줄**(빈 것 15 · 채운 것 12) · `HelpStep` **44단계**

### 아직 확인 못 한 것 (유저가 볼 것)

1. ★ **말풍선이 창을 얼마나 가리는지** — 성장 창은 1220x924 로 커서 말풍선이 창 위에 얹힌다.
   짚는 <b>대상</b>은 피하도록 되어 있지만, 다른 영역을 덮는 것이 거슬릴 수 있다.
   거슬리면 `bubbleGap`·말풍선 크기(560x210)를 줄이면 된다.
2. ★ **성장 창·전술 지침 창은 «고른 캐릭터» 가 없으면 내용이 빈다.** 조언 카드가 뜨는 시점에는
   대개 골라져 있지만(강화·전술을 방금 썼으므로), 백과에서 직접 눌렀을 때는 빌 수 있다.
   그때도 <b>칸의 자리</b>는 짚으므로 설명은 성립한다.
3. **단계 수가 적당한지** — 전술 지침 5단계 · 강화 4단계 · 능력치 4단계다. 길면 줄이고
   짧으면 늘리는 것은 `table_update_20260824_help_rewrite.py` 의 `STEPS` 한 곳이다.
4. **사건 창·발굴 창에도 안내를 붙일지** — 지금은 «상황이 만들어» 뜨는 창이라 뺐다.
   붙이려면 그 둘에 «바깥에서 열 수 있는» 통로를 만들어야 한다(권장하지 않는다).

### 씬반영요청 목록

- 없음 (씬 구조 변경 없음 · 저장만 했다)

---

## UI-57. ★★★ 「자세히 보기」 위로 <b>다음 카드가 튀어나오던</b> 것 — 가짜 트리거 셋과 대기줄 구멍 (2026-08-25, 15차)

### 무엇을 / 왜

유저 리포트: *"지금 자세히 보기를 누르면 <b>다음 기능 도움말 ui 기능이 떠서</b> 정작 자세히
보기를 누른 기능에 대한 ui 뒤에 뜨고 다음 기능 설명에 대한 ui가 먼저 뜸. <b>이러면 정상적인
듀토 진행 불가</b> 확인해보고 고쳐줘"*.

**고친 C#** — `Help/HelpService.cs`(대기줄 가드 · 가짜 트리거 셋) ·
`UI/ReadingPause.cs`(`AnyHeld`) · `UI/HelpTourPanel.cs`(안내 시작 시 카드 닫기)
**표·씬 변경** — 없음

---

### UI-57-1. ★★★ 짐작하지 않고 <b>트리거 로그를 켜서 세었다</b>

「자세히 보기」를 눌렀을 때 대기줄에 무엇이 남아 있었는지가 문제의 전부다. 그래서
`HelpService.logTriggers` 를 MCP 로 켜고 판을 처음부터 돌려 <b>대기줄에 들어간 것을 그대로
찍었다</b>:

```
[도움말] TacticsChanged — 판이 차려지는 중이라 버렸습니다   (x4)
[도움말] TacticsChanged — 1개 대기줄에 넣었습니다          ← ⚠ 다섯째가 통과했다
[도움말] NewRunFirstPreparation — 2개 대기줄에 넣었습니다
[도움말] AutoSaved — 1개 대기줄에 넣었습니다              ← ⚠ 저장한 적이 없는데
[도움말] GameSpeedChanged — 1개 대기줄에 넣었습니다        ← ⚠ 배속을 만진 적이 없는데
```

**판을 켜자마자 카드 다섯 장이 예약돼 있었다.** 그래서 「자세히 보기」로 카드를 닫는 순간
다음 장이 곧바로 튀어나왔고, 카드는 뜰 때 `SetAsLastSibling` 을 부르므로 <b>안내보다 앞</b>에
올라왔다 — 유저가 본 «누른 기능의 안내는 뒤에, 다음 기능 카드가 앞에» 가 그것이다.

⚠ 원인이 <b>넷</b>이었다. 하나만 고치면 증상이 «가끔» 남는다.

### UI-57-2. ★★★ 원인 ① — 대기줄이 <b>안내를 기다리지 않았다</b> (직접 원인)

`PumpQueue` 는 «카드가 떠 있는가» 와 «배타 창이 열려 있는가» 만 봤다. 안내
(<see cref="HelpTourPanel"/>)는 <b>배타 창이 아니다</b> — 창이 아니라 덮는 한 겹이다.

★ 그래서 <b>창을 여는 항목은 우연히 막혔고</b>(그 창이 배타라서) <b>창을 열지 않는 항목만
  새어 나왔다</b>(웨이브·에너지·캐릭터 생성·배속). 증상이 «어떤 것은 되고 어떤 것은 안 되는»
  모양으로 보인 이유가 이것이다.

→ `PumpQueue` 에 «안내가 돌고 있으면 기다린다» 를 더했다.

### UI-57-3. ★★★ 원인 ② — <b>도움말이 도움말을 불러냈다</b>

`GameSpeedChanged` 는 «배속을 처음 만졌을 때» 를 알려고 `GameSpeedPanel.IsPaused` 를 본다.
그런데 <b>조언 카드가 뜨면서 스스로 그 값을 true 로 만든다</b>(`ReadingPause`).
즉 <b>카드가 뜨는 것만으로 다음 카드가 예약된다.</b> 자기 자신을 트리거하고 있었다.

→ `ReadingPause.AnyHeld` 를 만들어 «지금 읽는 중인가» 를 <b>멈춤의 주인이</b> 알려 주게 했다.
  판단하는 쪽이 `GameSpeedPanel` 속을 들여다보며 «이 멈춤이 누구 것인가» 를 따지게 하면
  그 지식이 두 곳으로 갈린다.
★ <b>유저가 P 로 먼저 멈춰 둔 경우는 세지 않는다</b> — 그때 `ReadingPause` 는 소유권을 갖지
  않으므로 `AnyHeld` 가 거짓이고, 그 멈춤은 정말 유저가 한 것이라 조언이 떠야 맞다.

### UI-57-4. ★★ 원인 ③ — 「저장 파일이 있다」를 「방금 저장됐다」로 읽었다

`AutoSaved` 의 검사가 `SaveService.HasSave` 였다. 그것은 <b>지난 판의 저장 파일이 남아 있다</b>
는 뜻이라, <b>이어하기를 한 번이라도 쓴 유저에게는 언제나</b> 판을 켜는 순간 조언이 예약됐다.

→ `Awake` 에서 저장 라벨을 적어 두고 <b>바뀔 때</b>만 참이다(`SaveChanged`).

### UI-57-5. ★★★ 원인 ④ — <b>시간으로 «누가 했는가» 를 가를 수 없다</b>

`TacticsChanged` 는 시작 유예(1.5초)로 스포너의 일을 걸렀는데 <b>다섯째가 통과했다</b>.
스포너의 일이 1.5초 안에 끝난다는 보장이 없기 때문이다 — 시간으로 가르는 것은 <b>언제나 경합</b>이다.

→ <b>그 창이 열려 있는가</b>로 가른다. 전술 지침 창·부대 설정 창은 유저가 직접 열어야 하는
  것이므로 «열려 있다» 가 곧 «유저가 지금 이것을 만지고 있다» 다. <b>경합이 없다.</b>
★ 시작 유예는 <b>남겨 두었다</b> — 실측에서 넷을 제대로 걸렀고, 다른 계기의 백스톱이 된다.

### UI-57-6. 곁들여 막은 것 — 안내가 시작될 때 카드를 닫는다

「자세히 보기」 경로는 이미 카드를 닫고 오지만, <b>순서에 기대지 않는다</b>. 검수 메뉴나
다른 UI 가 `Begin` 을 직접 부를 수 있고, 그때 카드가 떠 있으면 <b>안내가 카드 뒤에 깔린다</b>
(실제로 검수 중에 그 모양을 봤다). `Begin` 이 스스로 카드를 닫는다.

### 검증

* `recompile_scripts` **에러 0 · 경고 0** · 플레이 중 콘솔 **에러 0**
* ★★ **고치기 전/후를 같은 방법으로 재봤다** — 콘솔에 표식을 찍고 그 뒤만 읽었다
  (로그 경계를 줄 번호로 잡으려다 앞 세션의 줄을 섞어 읽어 한 번 헛짚었다):

  | | 고치기 전 | 고친 뒤 |
  |---|---|---|
  | 대기줄에 들어간 계기 | TacticsChanged · NewRunFirstPreparation · **AutoSaved** · **GameSpeedChanged** | **NewRunFirstPreparation 하나뿐** |

* ★★ **실제 흐름 실측** — 카드(「웨이브와 정비 시간」)가 뜬 상태에서 안내를 시작하니
  `HUD_HelpCard` **꺼짐** · `HUD_HelpTour` 활성 · `UI_Root/HUD_Tactics` 활성 ·
  **10초를 더 기다려도 새 카드가 뜨지 않았다** · 테두리 `Col1/Pos` 자리에 274x48 유지
* 검수 뒤 `logTriggers` 를 <b>다시 끄고</b> 「읽은 조언 잊기」로 되돌린 뒤 저장했다

### 아직 확인 못 한 것 (유저가 볼 것)

1. ★ **안내를 끝낸 뒤 다음 카드가 이어지는지** — 대기줄은 안내가 닫히면 다시 도는 구조다.
   「다 봤습니다」를 눌러 다음 조언이 이어지는 것을 한 번 봐 주십시오(클릭이 필요해 MCP 로는 못 했다).
2. **부대 조언의 조건이 좁아졌다** — 이제 <b>부대 설정 창이 열려 있을 때만</b> 뜬다.
   창을 닫아 둔 채 로스터에서 배정하는 흐름이 있다면 그때는 안 뜬다.
3. **저장 조언** — 이제 «판을 켠 뒤 처음 저장이 일어날 때» 뜬다(자동 저장이 그 통로다).

### 씬반영요청 목록

- 없음 (`GameSystems` 의 `logTriggers` 를 되돌려 저장한 것뿐이다)

---

## UI-58. 발굴 표식을 <b>글자에서 주황 느낌표 원화로</b> · 통통 튀게 (2026-08-25, 16차)

### 무엇을 / 왜

유저 지시 둘: *"느낌표 스프라이트 볼트에 넣어놨으니까 <b>텍스트 대신 주황색 느낌표 짤라서 써</b>
발굴칸에"* · *"<b>통통 튀게</b> 해줘 주의를 끌 수 있도록"*.

UI-55-1 에서 실측해 «호박색 <b>네모</b> + 글자 느낌표(스프라이트가 비어 있다)» 라고 적어 둔
그 자리를 채운 것이다.

**신규 스크립트** — [Tools/dig_marker_build.py](Tools/dig_marker_build.py)
**신규 에셋** — `Assets/_Project/Resources/DigMarker/dig_marker.png` (220x510 · RGBA)
**고친 C#** — `Relics/RelicDigService.cs`
**씬 변경** — `markerPixels` 34 → **46** (MCP) · `save_scene`

---

### UI-58-1. 원화에 느낌표가 <b>다섯 벌</b>이었다

볼트 `리소스/sprites/!.png`(1721x914 · 알파 없는 RGB)에 노랑 · 빨강 · 파랑 · 흰색 ·
<b>주황</b>이 나란히 들어 있었다. 유저가 «주황색» 이라고 했고, 다섯째만 <b>반짝임 조각</b>이
둘러 있어 «주의를 끄는 것» 이라는 이번 지시의 성격과도 맞는다 — 그래서 다섯째를 골랐다.

### UI-58-2. ★★ 배경을 «흰색 지우기» 로 없애면 <b>하이라이트에 구멍이 뚫린다</b>

원화는 알파가 없고 배경이 흰색이다. 그런데 <b>느낌표 안에도 흰 하이라이트가 있다</b>(볼록한
느낌을 내는 빛). 「흰 픽셀을 전부 투명하게」로 처리하면 그 빛이 <b>구멍</b>이 된다.

→ <b>테두리에서 흘려보내(flood fill)</b> «바깥과 이어진 흰색» 만 지운다. 안쪽 하이라이트는
  검은 테두리에 둘러싸여 바깥과 이어지지 않으므로 <b>그대로 남는다</b>.

### UI-58-3. ⚠ 잘라내는 자리를 세어 찾다가 <b>파란색을 집었다</b>

좌표를 박지 않고 «열마다 배경 아닌 픽셀을 세어 빈 열로 가른다» 로 찾았는데,
덩이가 <b>다섯이 아니라 아홉</b>으로 나왔다. 느낌표 사이에 <b>1~2px 짜리 잡티</b>가 끼어 있어
그것까지 덩이로 세었고, 그래서 «다섯째» 가 주황이 아니라 <b>파랑</b>이 됐다.

→ `MIN_WIDTH = 24` 로 부스러기를 버린다. 느낌표는 135px 이 넘으므로 안전한 문턱이다.
★ 그래도 <b>좌표를 박지 않은 것이 맞다</b> — 원화가 바뀌어도 다시 돌리면 따라간다.
  세는 방식이 틀렸으면 <b>세는 규칙</b>을 고치는 것이 옳다.

### UI-58-4. ★ 스프라이트 «참조» 는 MCP 로 못 넣는다 — 코드가 꽂는다

`update_component` 로는 오브젝트 참조를 넣을 수 없다(진행상황 8절 4번). 그래서 씬이 아니라
<b>코드가 `Resources` 에서 읽어</b> 템플릿에 꽂는다(`HudTheme.Font` 와 같은 방식).

★ <b>원화를 못 찾으면 글자 느낌표로 돌아간다</b> — 표식이 아예 안 보이는 것보다 «옛 모양» 이
  낫고, 무엇을 굽지 않았는지 경고로 알린다. 그래서 씬의 `Label` 은 <b>켠 채로 두고</b>
  원화를 찾았을 때만 코드가 끈다.
★ <b>원본(템플릿)에서 한 번 끈다</b> — 복제는 템플릿을 그대로 베끼므로 표식마다 끌 필요가 없다.
⚠ <b>`preserveAspect` 를 켠다</b> — 느낌표는 220x510 으로 세로가 길다. 안 켜면 정사각 칸에
  <b>납작하게 눌린다</b>. 칸은 정사각으로 둬서 누르는 넓이를 지킨다(그림은 46x20 으로 보인다).
⚠ <b>평소 색을 흰색으로</b> 바꿨다 — 예전 `idleColor`(호박색)를 그대로 두면 주황 원화가
  곱연산으로 한 번 더 물든다. 파는 중일 때만 옅은 색으로 물들여 «누가 파고 있다» 를 알린다.

### UI-58-5. ★★ «통통» 은 <b>바닥에서 쉬는 시간</b>이 만든다

`Sin` 으로 흔들면 위아래로 <b>고르게 흐물거린다</b> — 튀는 것으로 안 보인다.
실제로 튀는 공은 <b>잠깐 솟았다가 내려와 머문다</b>. 그래서 «공중에 있는 동안» 만
포물선(`4x(1-x)`)을 그리고 나머지 시간은 0으로 둔다.

```
|    ▁▄█▄▁          ▁▄█▄▁          ▁▄█▄▁
|____      __________     __________     ____   ← 이 «쉼» 이 통통의 정체다
     공중         바닥
```

인스펙터 값: 높이 **9px** · 주기 **0.95초** · 공중 비율 **0.55** · 표식마다 어긋남 **0.19초**.

★ <b>표식마다 때를 어긋나게</b> 했다 — 여럿이 한꺼번에 튀면 기계처럼 보인다.
★ <b>파는 중에는 튀지 않는다</b> — 이미 사람이 가고 있으니 주의를 끌 일이 끝났다.
⚠ `Time.unscaledTime` 을 쓴다 — 조언 카드나 일시정지로 시간이 멈춰도 표식은 계속 튀어야
  «누를 수 있는 것» 으로 읽힌다(멈춘 동안에도 누를 수 있다).

### 검증

* `recompile_scripts` **에러 0 · 경고 0** · 플레이 중 콘솔 **에러 0**
* 잘라낸 결과를 <b>눈으로 확인</b>했다 — 220x510 RGBA · 배경 투명 · <b>안쪽 하이라이트 살아 있음</b> ·
  반짝임 조각 여덟 개 모두 포함
* ★ 플레이 중 **«느낌표 원화를 찾지 못했습니다» 경고 0건** — `Resources.Load` 가 성공했다는 뜻이다
  (실패하면 반드시 경고가 찍히게 해 뒀다)
* 덩이 가르기 — 부스러기를 버린 뒤 **정확히 다섯 덩이** · 다섯째가 x1353~1568(216px)로
  <b>가장 넓다</b>(반짝임 때문) — 주황이 맞다

### 아직 확인 못 한 것 (유저가 볼 것)

1. ★★ <b>실제로 튀는 모습은 눈으로 봐야 한다.</b> 플레이 중에 <b>발굴 칸이 시야에 들어오지
   않아</b>(캐릭터가 아직 못 찾았다) 살아 있는 표식을 못 봤다. 스프라이트 적재와 컴파일은
   확인했지만 <b>크기·튀는 높이·주기는 실물로 판단해야</b> 한다.
   거슬리면 `GameSystems > RelicDigService` 의 `bounceHeight`(9) · `bouncePeriod`(0.95) ·
   `bounceAirRatio`(0.55) · `markerPixels`(46) 만 고치면 된다.
2. **다른 색을 쓰고 싶으면** `Tools/dig_marker_build.py` 의 `PICK` 만 1~5 로 바꿔 다시 돌린다
   (1 노랑 · 2 빨강 · 3 파랑 · 4 흰색 · 5 주황).
   ⚠ 그때 `.meta` 는 <b>그대로 둔다</b>(guid 유지) — 스크립트가 이미 그렇게 되어 있다.
3. **칸 크기와 그림 크기** — 칸은 46x46(누르는 넓이), 그림은 46x20 으로 보인다.
   그림을 더 크게 하려면 `markerPixels` 를 올리면 되지만 누르는 칸도 같이 커진다.

### 씬반영요청 목록

- 없음 (`markerPixels` 를 MCP 로 고쳐 저장했다)

---

## UI-59. ★★★ 게임을 다시 시작하면 <b>창 배타가 통째로 죽던</b> 것 · <b>자원 성장 분리</b> · 로그의 스킬 «번호» · 불칸 칭호 (2026-08-25, 17차)

### 무엇을 / 왜

유저 리포트 넷을 한 절에 묶는다 — 넷 중 셋이 «<b>한 번만 하고 캐시한다</b>» 또는
«<b>표에서 한 칸이 비어 있다</b>» 라는 같은 종류의 구멍이었다.

| # | 유저의 말 | 고친 곳 |
|---|---|---|
| ① | *"게임 재시작을 누르거나 게임을 껐다 키면 UI창들이 여러개 켜지고 esc로도 종료가 되지 않아"* | `UI/HudExclusive.cs` · `UI/ActionPanel.cs` |
| ② | *"몬스터들 잡을때마다 자원 성장이 너무 기하급수적… 스탯 성장과 자원 획득량 성장은 별개로"* | `Units/NeutralGrowthService.cs` · `NeutralKillTally.cs` · `NeutralMonsterUnit.cs` · 씬 |
| ③ | *"유물 중에 숨긴 티끌… 자원 획득량 12 늘어나는거 너무 사기니까 좀 조정해"* → *"%가 더 사기일듯 걍 자원 획득량을 좀 줄이거나 확률을 넣어"* | `Relics/*` · 유물 표 · 유물 에셋 |
| ④ | *"보스 스킬 쓸때 스킬 이름이 아니라 번호 나오는거 고쳐줘 로그에"* | `Tools/gen_string_table.py` · 스트링 키 표 · `StringTable.txt` |
| ⑤ | *"데이터 테이블이랑 폴더 확인해서 불칸 칭호를 '화염의 마수' -> '대마법사' 로"* | 캐릭터 표 · 스트링 키 표 · `StringTable.txt` · 캐릭터 에셋 |

**씬 변경** — `GameSystems ▸ NeutralGrowthService` 에 칸 둘 신설(MCP) · `save_scene` 1회

---

### UI-59-1. ★★★ ① 원인 — <b>창 목록이 시체로 가득 차 있었다</b>

`HudExclusive` 는 «창은 씬에 고정이라 늘거나 줄지 않는다» 는 전제로 비활성 포함 조회를
<b>한 번만</b> 하고 캐시한다(`EnsureScanned`). 그 전제는 <b>씬 하나 안에서만</b> 참이었다.

```
로비 ▸ 게임 시작            → Proto_01 을 연다 → 여기서 스캔        (정상)
환경설정 ▸ 게임 재시작      → Proto_01 을 다시 연다 → 옛 창 전부 파괴 (여기서 죽는다)
환경설정 ▸ 로비 ▸ 이어하기  → 같은 일
패배/승리 ▸ 다시하기        → 같은 일
```

캐시를 비우는 `Reset` 이 `RuntimeInitializeOnLoadMethod` 라 <b>프로세스마다 한 번</b>만 돈다.
씬을 다시 여는 것으로는 돌지 않는다 — `RunResetService` 가 생긴 이유와 <b>똑같은 뿌리</b>다
(그 클래스의 ⚠⚠: *"«씬을 다시 여는 것» 으로는 안 비워진다"*).

목록에 남은 것은 <b>파괴된 컴포넌트</b>뿐이고, 세 함수가 전부 그것을 <b>조용히 건너뛴다</b>
(`p is Object o && o == null`). 그래서 새 씬의 창은 <b>목록에 없다</b>:

| 함수 | 죽은 뒤 | 화면에 보이는 것 |
|---|---|---|
| `OpenOnly` | 아무것도 닫지 못한다 | 전술 지침·캐릭터 성장이 <b>동시에</b> 열린다 |
| `CloseOpenPanel` | `false` 를 돌려준다 | Esc 가 «닫을 창이 없다» 로 보고 <b>환경 설정을 하나 더 연다</b> |
| `AnyOpen` | `false` 를 돌려준다 | 조언 카드가 열린 창 위로 덮인다 |

★ <b>Esc 가 누를수록 나빠졌다</b> — ②에서 못 닫고 ③으로 떨어져 창을 <b>더</b> 여는 구조다.
  유저가 «esc로도 종료가 되지 않아» 라고 적은 것이 정확히 이것이다.

### UI-59-2. ① 고친 방법 — <b>«언제 다시 훑는가»</b> 를 고쳤다

★ <b>등록 방식(`OnEnable` 에서 Register)으로는 못 바꾼다.</b> 창은 평소 비활성이고 유니티는
  비활성 오브젝트의 `Awake`·`OnEnable` 을 <b>아예 부르지 않는다</b>. «한 번도 열린 적 없는 창» 이
  스스로 등록할 방법이 없어서 훑는 것이다. 그러니 고칠 곳은 «훑는 방법» 이 아니라
  <b>«언제 다시 훑는가»</b> 다.

**① 정본 — `SceneManager.sceneLoaded` 를 구독해 목록을 버린다** (`Invalidate`).
씬이 바뀌는 바로 그 순간을 짚으므로 «언제 낡았는가» 를 추측할 필요가 없다.

⚠ 구독은 `-=` 를 먼저 하고 `+=` 한다 — 도메인 리로드를 꺼 두면 지난 플레이의 구독이
  <b>살아남아</b> 두 번 걸린다. 그래서 람다가 아니라 <b>정적 메서드</b>로 뺐다(델리게이트 비교가 된다).

**② 안전망 — 캐시에 파괴된 컴포넌트가 하나라도 있으면 다시 훑는다** (`IsCacheAlive`).
목록 전체가 옛 씬의 것이라는 확실한 신호다.

★ <b>빈 목록도 «못 믿는다» 로 본다</b> — 창이 하나도 없는 씬(로비·오프닝)에서 한 번 훑고 나면
  그 빈 목록이 게임 씬까지 따라가 <b>같은 증상</b>을 낳는다. ①이 있으면 안 일어나지만,
  그물은 두 겹이 낫다.

⚠ 값이 아홉 개 남짓이라 매 호출 검사해도 싸다 — 프레임마다 부르는 곳은 `HelpService` 의
  `AnyOpen` 하나뿐이고 거기서도 참조 비교 아홉 번이다.

### UI-59-3. ① 곁가지 — 「닫힌 채로 시작」 목록에 <b>둘이 빠져 있었다</b>

`ActionPanel.Start` 의 «창들은 닫힌 채로 시작이 규칙이다» 정리에 토벌·환경설정·유물·도움말
<b>넷</b>만 있고 <b>전술 지침·부대 설정</b>이 없었다. 씬 값이 마침 비활성이라 드러나지 않았을
뿐이다(실측: `HUD_Tactics`·`HUD_Squad` 둘 다 `m_IsActive: 0`).

창을 켠 채 씬을 저장하는 사고는 <b>실제로 일어난다</b> — `EventPanel.Start` 가 그 사고를
막으려고 존재한다. 여섯 창이 다 있어야 규칙이 규칙이다.

---

### UI-59-4. ★★★ ② 자원 수입이 «기하급수» 였던 진짜 셈

`NeutralGrowthService.scaleEnergyReward` 는 켜면 <b>능력치와 똑같은 배율</b>을 보상 에너지에
곱했다(2026-08-21 유저 지시로 그렇게 만들었다). 그런데 <b>능력치 배율은 상한이 0(무제한)</b>
으로 운영 중이었다(씬 실측 `maxMultiplier: 0`). 그러면 이렇게 된다:

```
N 마리째 한 마리 값 = 기본 × (1 + 한마리당 × N)        ← 1차
N 마리까지의 누적    = 기본 × (N + 한마리당 × N²/2)     ← 2차 다
```

실측(id 1003 · 평균 265 · `growth_per_kill` 0.01 · 상한 없음):

| 처치 수 | 누적 에너지 | 배율 없을 때 대비 |
|---:|---:|---:|
| 100 | 39,750 | 1.5배 |
| 300 | 198,750 | 2.5배 |
| 500 | 463,750 | 3.5배 |

★ <b>멈출 곳이 없다</b>. 게다가 잡몹 중립은 `maxAlive` 19~43 으로 계속 되살아나므로
  처치 수는 한 판에 수백 단위로 쌓인다.

### UI-59-5. ② 고친 방법 — 칸을 <b>둘로 나눴다</b>

`GameSystems ▸ NeutralGrowthService` 에 <b>자원 전용 칸 둘</b>을 신설했다.

| 칸 | 기본값 | 뜻 |
|---|---:|---|
| `scaleEnergyReward` | 켬 | 자원도 사냥 성장을 따라 오르는가 (기존) |
| `energyGrowthRatio` | **0.25** | 능력치 배율의 <b>늘어난 몫</b> 중 자원에 반영할 비율 |
| `energyMaxMultiplier` | **2** | <b>자원 배율 상한</b>(0 = 무제한) — 능력치 상한과 별개 |

```
자원 배율 = 1 + (능력치 배율 - 1) × energyGrowthRatio   ▸ 그다음 energyMaxMultiplier 로 자른다
```

같은 실측이 이렇게 바뀐다 — 400마리에서 상한 x2 에 닿고 그 뒤로는 <b>선형</b>이다:

| 처치 수 | 전 | 후 |
|---:|---:|---:|
| 100 | 39,750 | 33,125 |
| 300 | 198,750 | 109,224 |
| 500 | 463,750 | 약 165,000 (상한 뒤 선형) |

★ <b>«늘어난 몫» 에만 비율을 건다</b> — 배율 자체에 곱하면(`stat × ratio`) 성장이 0 일 때도
  자원이 <b>줄어든다</b>. 기준선 1 은 건드리지 않는 것이 맞다.
★ <b>`ratio 1 · cap 0` 이면 예전과 완전히 같다</b> — 되돌리는 길을 남겼다.
★ <b>능력치는 손대지 않았다.</b> 유저가 말한 «별개» 가 그 뜻이다 — 몬스터는 계속 세지고
  자원만 완만해진다.
★ <b>«비율» 로 이었고 자원 곡선을 따로 재지 않았다</b> — 종별 성장(`growth_per_kill`)이 표에
  있어서 자원 쪽에도 같은 컬럼을 만들면 <b>표를 두 벌</b> 관리하게 된다. 비율이면 종이 늘어도
  손댈 곳이 없고, `0` 으로 두면 «자원은 전혀 안 오른다» 는 완전한 독립도 그대로 성립한다.
⚠ 자원 배율도 능력치와 마찬가지로 <b>소환 순간에 굳는다</b>(`NeutralMonsterUnit.EnergyMultiplier`) —
  방금 잡은 한 마리가 자기 보상을 올려주지 않는다.

### UI-59-6. ③ 「삼킨 티끌」 — <b>%가 아니라 «확률»</b> 로

유저가 처음엔 *"%로 하든가"* 라고 했다가 곧 <b>스스로 뒤집었다</b>: *"%가 더 사기일듯
걍 자원 획득량을 좀 줄이거나 확률을 넣어"*. 그 판단이 맞다 —

★ 이 게임의 처치 보상은 <b>대역이 60배 넘게 벌어진다</b>(웨이브 잡몹 10 · 중립 200~1200).
  %로 주면 <b>중립을 사냥할수록 유물 값이 같이 불어나</b> 방금 고친 «기하급수» 문제가
  유물로 옮겨올 뿐이다. <b>절대값 + 확률</b>이면 어떤 적을 잡아도 기댓값이 같다.

| | 전 | 후 |
|---|---|---|
| `value_01` (에너지) | 12 | **5** |
| `value_02` (확률 %) | — (칸이 없었다) | **40** |
| 기댓값 / 처치 | 12 | **2.0** |

★ <b>일반 등급인데 혼자 다른 칸이었다.</b> 같은 등급의 다른 유물은 «능력치 +1~4» 인데
  이것만 웨이브 잡몹 보상(10)을 <b>두 배</b>로 만들고 있었다. 이제 잡몹 보상의 20% 다.
★ `value_02` 를 <b>확률 칸</b>으로 쓴다 — 「두꺼워진 가피」(체력 문턱) ·「심장에 박힌 가시」
  (누적 상한) ·「최후의 발버둥」이 이미 v2 를 부차 인자로 쓰는 그 규약 그대로다.
⚠ <b>`value_02` 가 0 이면 «항상»</b> 이다 — 확률 칸이 없던 시절의 표(Ver01·Ver02)와 호환된다.
  다른 `KillEnergy` 유물이 나중에 v2 없이 들어와도 조용히 안 터지는 일이 없다.

### UI-59-7. ★★ ④ 로그에 스킬 «번호» — <b>수집 규칙에 두 줄이 없었다</b>

`BossSkillSO.DisplayName` 은 «스트링 테이블 ▸ 없으면 리터럴 ▸ 그것도 없으면 <b>에셋 이름</b>»
순으로 떨어진다. 에셋 이름이 `BossSkill_2003` 이라 그것이 화면의 «번호» 였다.

거슬러 올라가니 구멍은 <b>표를 걷는 규칙</b>에 있었다:

```python
# gen_string_table.py — 있던 것
('웨이브 몬스터 테이블.xlsx', 'Skill', …, [('skill_name', …), ('skill_explain', …), ('status_name', …)])
('캐릭터 테이블.xlsx',      'Skill', …, [('skill_name', …), ('skill_explain', …), ('skill_detail', …)])
('임시용 중립 몬스터.xlsx',  'Skill', …, [                                        ('status_name', …)])   ← ★
```

<b>중립 표만 `skill_name` 이 안 걷히고 있었다.</b> 그래서 `skill_name_2003~2009` 가
`StringTable.txt` 에 <b>아예 없었다</b>(실측: 없는 키 7개).

⚠ <b>2001·2002 만 멀쩡했던 이유</b> — 그 둘이 스트링 키 표에 <b>손으로</b> 들어가 있었다
  (출처 칸이 비어 있다). 손으로 넣은 두 줄이 <b>구멍을 가리고 있었던 것</b>이다.
  카르시노스(2001·2002)만 로그가 멀쩡하고 아니사킬·바리올라·폴리르가 번호로 나온 이유다.

→ 규칙에 두 줄을 더하고, 이름 일곱을 지어 스트링 키 표와 TSV 양쪽에 넣었다.
  이름은 <b>정의문(`skill_type_desc`)을 읽고</b> 붙였다 — 지어낸 것이 아니다.

| id | 타입 | 이름 | 누가 쓰나 · 무엇을 하나 |
|---|---|---|---|
| 2003 | `Tail_strike` | **꼬리치기** | 아니사킬 · 전방 3x5 상자 근접 150% |
| 2004 | `Huge_threat` | **거대한 위협** | 아니사킬 · 반지름 4 원형 200% + 3초 기절 |
| 2005 | `Creepy_scar` | **소름 끼치는 흉터** | 바리올라 · 가장 가까운 1명에게 침식 +20 |
| 2006 | `Deadly_venom` | **치명적인 맹독** | 바리올라 · 지름 5 안의 적 현재 체력 −최대의 10% |
| 2007 | `Flame_emission` | **화염 방출** | 폴리르 · 정면 반지름 5 부채꼴 200% |
| 2008 | `Rapid_Playback` | **급속 재생** | 폴리르 · 체력 50% 에서 최대의 50% 회복 |
| 2009 | `Dread` | **공포** | 폴리르 · 최대 3명에게 낙뢰 200% + 1초 기절 |

### UI-59-8. ⑤ 불칸 칭호 — 「화염의 마수」 → <b>「대마법사」</b>

칭호가 <b>네 곳</b>에 있다. 유저가 *"데이터 테이블이랑 폴더 확인해서"* 라고 한 것이
정확했다 — 유니티 쪽만 고치면 표를 다시 내보내는 순간 <b>되돌아간다</b>.

| 자리 | 후 |
|---|---|
| `Resources/Characters/Character_9011_Vulcan.asset` 의 `title` | 대마법사 |
| `Resources/Data/StringTable.txt` 의 `character_title_9011` | 대마법사 / The Archmage |
| 볼트 `스트링 키 테이블.xlsx` (정본) | 대마법사 / The Archmage |
| 볼트 `캐릭터 테이블.xlsx` 의 `character_title_EG` | The Archmage |

★ 영어도 같이 고쳤다 — 한글만 바꾸면 언어를 바꿨을 때 <b>옛 칭호가 되살아난다</b>.
⚠ `titleKey`(`character_title_9011`)는 <b>건드리지 않았다</b> — 키가 바뀌면 참조가 전부 깨진다.
  바뀐 것은 «그 키가 가리키는 글자» 뿐이다.

### UI-59-9. ★ 「표를 안 고치면 되돌아간다」 — 이번에 세 번 걸릴 뻔했다

②③⑤ 가 전부 <b>볼트 표가 정본</b>인 값이다. `gen_string_table.py` 는 «이미 있는 키의 kr/en 은
그대로 둔다» 지만 `StringTable.txt` <b>자체는 표에서 다시 만들어진다</b>. 그래서 유니티 쪽만
고치면 다음 재생성에서 조용히 옛 값으로 돌아간다(그 스크립트의 ⚠ 그대로).

이번에 <b>양쪽을 다 맞췄다</b>:

| 바뀐 값 | 유니티 | 볼트 표 | 생성 스크립트 |
|---|---|---|---|
| 불칸 칭호 | 에셋 · TSV | 캐릭터 표 · 스트링 키 표 | — |
| 삼킨 티끌 | 에셋 · TSV | 유물 표(Relic·EffectType) | `gen_relic_table.py` |
| 스킬 이름 7 | TSV | 스트링 키 표 | `gen_string_table.py`(규칙 자체를 고쳤다) |

### 검증

* `recompile_scripts` **에러 0 · 경고 0**
* `grep -rn "화염의 마수" Assets/` → **0건**
* `grep "^skill_name_200[3-9]" StringTable.txt` → **7건** (전부 있음)
* 씬 실측 — `GameSystems ▸ NeutralGrowthService` 에 `energyGrowthRatio: 0.25` ·
  `energyMaxMultiplier: 2` 가 저장됐다. 능력치 쪽 `maxMultiplier: 0`(무제한)은 <b>그대로</b>다.
* 씬 실측 — 배타 창 일곱 전부 `m_IsActive: 0` · `HudHotkeys` 는 씬에 실물로 붙어 있다
  (guid `cabe47b8…` 1건). 즉 씬을 다시 열어도 Esc 를 <b>읽는</b> 쪽은 살아 있었다.
  <b>죽어 있던 것은 «무엇을 닫을지 아는» 쪽뿐이었다.</b>

### 아직 확인 못 한 것 (유저가 볼 것)

1. ★★ <b>① 은 플레이로 재현·확인해야 한다.</b> ① 전술 지침을 연다 → ② 환경설정 ▸ 게임 재시작
   → ③ 다시 전술 지침을 열고 그 위에 캐릭터 성장을 눌러 본다(<b>전술 지침이 닫혀야 한다</b>)
   → ④ Esc 로 닫히는지 본다. 로비 왕복(환경설정 ▸ 로비 ▸ 이어하기)으로도 같은 것을.
2. ★★ <b>② 의 0.25 / x2 는 «첫 값» 이다.</b> 위 표는 셈이고, 체감은 한 판을 돌려 봐야 한다.
   `GameSystems ▸ NeutralGrowthService` 에서 바로 만진다 — 더 조이려면 `energyGrowthRatio` 를
   0.15 로, 더 풀려면 `energyMaxMultiplier` 를 3 으로.
   ⚠ <b>웨이브 몬스터 보상(`ResourceManager.energyPerMonsterKill` = 10)은 성장이 없다.</b>
     불어나던 것은 중립 사냥 쪽뿐이라 그쪽만 손댔다.
3. ③ **40% 가 심심하면** `Relic_700006.asset` 의 `value02` 만 올리면 된다(코드 수정 불필요).
   ⚠ 그때 `Tools/gen_relic_table.py` 와 볼트 유물 표에도 <b>같은 값</b>을 넣을 것.
4. ④ **`skill_explain_2001~2009` 는 여전히 비어 있다** — 표의 `skill_explain` 칸에 키만 있고
   한국어가 어디에도 없다. 이제 수집 규칙에는 들어갔으니 스트링 키 표에 문구를 적으면
   바로 붙는다. 지금은 툴팁이 빈칸일 뿐 <b>로그에는 영향이 없다</b>(이름만 쓴다).
5. **표를 다시 내보낼 때** — `gen_string_table.py` 를 돌리면 위 값들이 <b>유지된다</b>(병합 규칙).
   다만 `--rebuild` 는 <b>쓰지 말 것</b> — 손으로 다듬은 문구가 전부 원본 리터럴로 덮인다.

### 씬반영요청 목록

- 없음 (`energyGrowthRatio`·`energyMaxMultiplier` 를 MCP 로 넣고 `save_scene` 했다)

---

## UI-60. HUD 에 <b>판·버튼·게이지 그림</b>을 입혔다 — 단색 사각형에서 무쇠 UI 로 (2026-08-25, 18차)

### 무엇을 / 왜

유저가 생성 AI 로 뽑은 UI 원화 14장(볼트 `리소스/sprites/BUTTON_01~06` · `UI_01~08`)을
받아 게임에 적용했다. 지금까지 HUD 는 <b>전부 단색 `Image`</b> 였다 — 창 테두리는
`Border/Top·Bottom·Left·Right` 라는 <b>1~2px 짜리 단색 이미지 네 장</b>, 게이지는 8×8
`BarFill.png` 한 장. 로비만 그림이 있어서 전장에 들어가는 순간 톤이 갈렸다.

### 어떻게 — 세 토막으로 나뉜다

| 토막 | 누가 | 왜 거기서 |
|---|---|---|
| ① 시트 자르기 | `Tools/ui_sprite_cut.py` | 자홍 키잉·낱장 분리·9-슬라이스 경계 산출 |
| ② 임포트 설정 | `Editor/UiSpriteImporter.cs` (`LastSanctuary/UI/임포트`) | 경계는 <b>임포터에만</b> 있다 |
| ③ 씬 배선 | `Editor/UiSkinApplier.cs` (`LastSanctuary/UI/배선`) | MCP 가 스프라이트 참조를 못 넣는다 |

**원화 14장이 실제로는 여섯 종 + 여덟 종이다.** 씬의 버튼은 마흔 개가 넘지만 폭만 다르고
모양은 여섯 가지뿐이라, 한 장을 9-슬라이스로 늘려 쓴다. 잘라 낸 낱장은 36장
(`Resources/UI/Buttons/` 24 · `Resources/UI/Frames/` 12).

```
BUTTON_02 → Btn_Action_{Normal,Hover,On,Off}   액션 바 8개 (240×40)
BUTTON_01 → Btn_Panel_*                        창 안 보통 버튼 (116~340 폭)
BUTTON_03 → Btn_Chip_*                         좁은 토글 칩
BUTTON_04 → Btn_Close_*                        정사각 닫기 + 배속 칩
BUTTON_05 → Btn_Choice_*                       사건·발굴 선택지
BUTTON_06 → Btn_Wide_*                         (예비 — 아직 안 씀)
UI_01 → Win_Frame · UI_02 → Hud_Plate · UI_03 → Bar_Track · UI_04 → Bar_Fill
UI_05 → Portrait_Frame · UI_06 → Minimap_Bezel · UI_07 → Slot_{Empty,Filled,Locked}
UI_08 → Divider_{Plain,Diamond,Header}
```

### 설계 판단 — 왜 이렇게 했는지

- **자홍(`#FF00FF`) 키잉은 «비율» 로 푼다.** 생성 AI 는 알파를 못 만들어 자홍 바탕에 뽑는다.
  섞인 픽셀은 `P = a·F + (1-a)·K` 인데, `min(R,B) - G` 가 순수 자홍에서 255·회색에서 0 이고
  <b>붉은 균열(R 만 크고 B 는 낮다)에서도 0 에 가깝다</b>. 그래서 이 값을 «자홍 비율» 로 쓰면
  원화의 진홍 장식을 갉아먹지 않는다. 실측 잔기 평균 0~2(255 만점).
  ⚠ 옅은 알파(1~24)는 <b>지운다</b> — 눈엔 안 보이는데 `getbbox` 가 «내용» 으로 쳐서
  여백이 안 잘리고(슬롯이 104px 대신 207px 로 나왔다), 어두운 배경에서 후광으로 보인다.
- **전부 2배로 뽑고 PPU 를 200 으로 준다.** 1배(PPU 100)로 두면 액션 버튼(240×40)에 장식이
  134+134=268px 로 붙어 <b>버튼보다 장식이 커진다</b>. PPU 를 캔버스 기준(100)의 두 배로 주면
  경계가 화면에서 절반으로 그려져 240 폭 안에 134 만 쓰고 106 이 늘어난다.
- **9-슬라이스 경계는 손으로 준다.** 자동 측정(가운데 열과 같은 열이 이어지는 구간)을 짜 봤는데
  <b>금속 질감의 얼룩 때문에 «똑같은 열» 이 두 개도 없어서</b> 0 아니면 판 전체가 나왔다.
  그림을 보고 비율로 주는 것이 정확했다(`ui_sprite_cut.py` 의 `JOBS`).
- **닫기·슬롯·구분선은 경계를 0 으로 둔다** — 32×32 짜리에 9-슬라이스를 걸면 L+R 이 표시 폭보다
  커져서 오히려 깨진다. 정사각을 정사각으로 늘리는 것이라 그냥 늘려도 된다.
- **배선은 경로가 아니라 «이름·비율» 로 판단한다.** 로스터 행·부대 카드·유물 행은 템플릿을
  복제해 런타임에 생기므로(UI-1절 ★ 템플릿 예외) 경로 목록으로 잡으면 복제본이 빠진다.
  버튼 종류도 렉트 비율로 고른다 — 버튼이 하나 늘 때마다 스크립트를 고치지 않으려고.
- **목록의 «행» 은 일부러 안 칠했다**(로스터·도움말·유물·부대 카드·토벌 행). 그것들은
  `HudTheme` 의 색으로 선택 상태를 표시하는데, 그림을 깔면 그 색이 곱해져 선택이 안 보인다.
  행은 단색이 맞다.
- **얇은 선 네 장 → 액자 한 장.** 초상화(`ArtFrame`)와 미니맵(`ViewBorder`)의 테두리는 1~2px
  이미지 네 장이었다. 그 <b>빈 컨테이너에 `Image` 를 붙이고</b> 네 장을 껐다 — 컨테이너가 이미
  정확히 액자가 놓일 렉트라서 오브젝트를 새로 만들 필요가 없었다. 겹쳐 보이던 바깥
  `Border/*` 네 장도 껐다(이제 `Hud_Plate` 가 테두리를 가지고 있다).

### 겪은 함정

1. ★★ **MCP 로는 스프라이트를 넣을 수 없다.** `update_component` 에 `m_Sprite` 를
   <b>경로로도 이름으로도</b> 줘 봤지만 `m_Type`(단순 enum)만 반영되고 `m_Sprite` 는 계속
   `null` 이었다. 진행상황 8절 4번의 «MCP 는 참조를 못 넣는다» 가 <b>에셋 참조에도</b> 걸린다.
   TMP 폰트만 예외였던 것(UI-1절)은 그 브리지가 `m_fontAsset` 을 따로 처리하기 때문이다.
   → <b>참조를 넣는 일만</b> 에디터 코드로 옮겼다. 크기·위치·계층은 여전히 MCP 가 맡는다.
2. **새 파일은 `Assets/Refresh` 를 눌러야 유니티가 안다.** `recompile_scripts` 는 «성공» 을
   돌려주지만 <b>임포트되지 않은 새 `.cs` 는 보지 않는다</b> — 메뉴가 등록 안 돼
   `execute_menu_item` 이 "no menu named" 로 실패했다. 새 파일을 만들었으면 Refresh 먼저.
3. ★★ **색을 칠하던 코드를 같이 안 고치면 버튼이 새까매진다.** 패널들이
   `background.color = buttonOn` 으로 상태를 표시하고 있었는데, 그림 위에 그 어두운 색이
   <b>곱해진다</b>. → `HudTheme.PaintButton(img, ButtonState, fallback)` 을 새로 만들고
   호출부 17곳을 바꿨다(`ActionPanel` 8 · `SquadPanel` 3 · `BuildButtonUI` 2 ·
   `GameSpeedPanel` 2 · `UpgradeButtonUI` 1 · `CharacterGrowthPanel` 1 · `TacticalOrderPanel` 1).
   ★ 그 함수는 <b>지금 붙은 스프라이트 이름에서 계열을 읽는다</b>(`Btn_Action_Normal` →
   `Btn_Action`) — 부르는 쪽이 «이 버튼이 무슨 그림인지» 를 몰라도 된다.
   ⚠ 그림이 없으면 예전처럼 색을 칠한다(`fallback`) — 그래서 목록 행과 그림 없는 버튼이
   그대로 산다.
4. **채워지는 막대는 `type` 을 건드리면 안 된다.** `Filled` 여야 `fillAmount` 가 먹는다
   (`UiFillBar` 의 설명 그대로). 배선 스크립트가 막대만 `keepType: true` 로 지나간다.
   `UiFillBar.Prepare` 는 «스프라이트가 있으면 안 건드린다» 라 새 `Bar_Fill` 을 덮지 않는다.
5. **게이지 채움은 반드시 흰 회색조여야 한다** — 체력(초록→노랑→빨강)·침식(보라→자홍)을
   코드가 `Image.color` 로 곱한다. `UI_04` 실측 채도 평균 2(255 만점)로 통과.

### 확인된 것

- `recompile_scripts` 에러·경고 0, 콘솔 에러 0.
- 배선 결과: **창 9 · 판 25 · 버튼 72 · 막대 16 · 칸 4 · 액자 2**.
- 저장된 씬 YAML 에서 새 스프라이트 GUID **415회 참조** 확인(파이썬으로 직접 세었다).
- 9-슬라이스를 <b>유니티와 같은 방식으로 파이썬에 다시 그려</b> 실제 표시 크기 14가지에서
  경계 넘침 0건 확인(240×40 · 116×40 · 96×32 · 32×32 · 208×18 · 520×430 · 760×420 …).

### 아직 확인 못 한 것 (유저가 볼 것)

- **플레이 모드 검증 전혀 안 함.** 에디터에서 직접 볼 것:
  - 액션 바 8개 버튼의 글자가 장식에 가리지 않는지 (240 폭 중 가운데 106px 만 평평하다)
  - **`HUD_Growth/Footer/CloseButton`·`HUD_Tactics/Footer/*`(120~130 폭)** 가 「칩」으로,
    **`HUD_Settings/Body/*`(폭 480 스트레치)** 가 「액션 바」로 분류됐다 — 비율로 고른
    결과라 의도와 다르면 `UiSkinApplier.ButtonKind` 의 문턱을 고치면 된다
  - 배속 칩 x1~x8(57×40)이 「닫기」 그림을 쓴다 — 원화에 배속용 칩(1.4:1)이 없어서 정사각을
    늘려 쓰는 중이다. 어색하면 그 비율로 한 장 더 뽑는 것이 낫다
  - 전술 창 옵션 버튼 다수가 「액션 바」 그림이라 장식이 많이 반복된다
- **안 쓰고 남은 그림 8장**: `Btn_Wide_*`(4) · `Divider_Diamond` · `Divider_Header` ·
  `Slot_Filled` · `Slot_Locked`. 앞의 다섯은 예비(웨이브 배너 후보), 뒤의 둘은
  «칸이 찼다/잠겼다» 를 코드가 갈아끼워야 쓴다 — 아직 안 붙였다.
- **웨이브 배너(440×92)** 는 원화가 없어 `Hud_Plate` 를 쓰고 있다.

### 씬 변경 여부

**있음.** `save_scene` 2회(배선 1차 → 액자 추가 후 2차). 스프라이트 참조 415건,
`ArtFrame`·`ViewBorder` 에 `Image` 신설 2건, 얇은 테두리 16장 비활성.

### 씬반영요청 목록

없음 (이 브랜치가 씬 소유자).

---

## UI-61. UI 를 <b>픽셀 아트로 갈아엎었다</b> · 유물 창 이름 가림 수정 (2026-08-25, 19차)

### 무엇을 / 왜

UI-60 에서 넣은 painted UI 를 유저가 물렸다 — *"너무 중국산 게임 같아서"*. 원인이 셋이었다:

1. **장식 크기가 픽셀 규격이 아니었다.** 액션 바가 240×40 인데 끝 장식이 화면에서 67px 씩
   먹었다. 40px 높이면 장식 예산은 <b>양끝 8px, 테두리 1~2px</b> 이다.
2. **폰트와 해상도가 안 맞았다.** 네오둥근모는 비트맵 픽셀 폰트라 <b>글자 1픽셀 = 화면 1픽셀</b>
   인데, UI 만 2배로 뽑아 PPU 200 으로 깔았다. 각진 글자 옆에 보간된 금속이 놓였다.
3. **팔레트가 두 벌이었다.** 로비(무쇠+진홍)에 맞췄는데 `HudTheme` 의 실제 색은
   <b>차가운 슬레이트 + 청록</b>이고 사건 배경도 이미 16비트 픽셀이다. 게임 안은 원래
   픽셀이었고 거기에 painted UI 를 얹은 것이었다.

원화 15장을 다시 받아(볼트 `리소스/sprites/` · `BUTTON_01~06` · `UI_01~09`) 픽셀로 교체했다.

### 어떻게 — 파이프라인을 픽셀용으로 다시 짰다

`Tools/ui_sprite_cut.py` 를 통째로 갈아엎었다. 여섯 단계다.

| 단계 | 무엇 | 왜 |
|---|---|---|
| ① | **배경 자동 판별** | 파일마다 배경이 다르다 — 자홍이거나 <b>이미 알파</b>거나 |
| ② | 칸 분리 | |
| ③ | **네이티브로 되줄임 (BOX)** | NEAREST 는 1픽셀 테두리를 통째로 날린다 |
| ④ | **16색 팔레트 스냅** | 생성기가 몰래 넣은 그라데이션 제거 |
| ⑤ | **알파 이진화 (0/255)** | ★ «투명 배경을 확실히» 의 답 |
| ⑥ | 9-슬라이스 경계 산출 | 이번엔 <b>자동이 먹힌다</b> |

★ **팔레트는 `HudTheme` 에서 뽑았다.** 그림과 코드가 같은 색표를 써야 글자와 판이 한 벌로
  보인다. 결과: 낱장마다 색이 **4~12개**(painted 세트는 수천 개였다).

★ **9-슬라이스 경계 자동 측정이 이번엔 성공했다.** 지난 painted 세트는 금속 얼룩 때문에
  «가운데와 똑같은 열» 이 두 개도 없어서 0 아니면 판 전체가 나왔는데, 팔레트 스냅을 거치니
  평평한 면이 진짜로 평평해져서 그대로 잡힌다.

임포터(`UiSpriteImporter.cs`)는 두 값만 바뀌었다:
`Bilinear → **Point**`(1픽셀 테두리를 뭉개지 않게) · `PPU 200 → **100**`(1배 네이티브).

### 겪은 함정

1. ★★ **묶음 단위로 크기·경계를 통일해야 한다.** 상태 4장을 따로 자르니 «켜짐» 의 발광과
   «올림» 의 호박색 선 때문에 bbox 가 갈려 <b>`Btn_Choice` 가 44/50/52/50</b> 으로 나왔다.
   그대로 두면 마우스를 올릴 때마다 버튼이 씰룩거린다. → 합집합 렉트로 한 번에 자르고,
   경계도 네 장 중 가장 넉넉한 값 하나로 못박는다.
2. ★★ **자르기 전에 옅은 알파를 먼저 끊어야 한다.** 키잉이 남긴 알파 1~127 은 눈에 안
   보이는데 `getbbox` 가 «내용» 으로 쳐서 여백이 안 잘린다. <b>32×32 짜리 닫기 버튼이
   79×32 로</b> 나왔고(높이 기준으로 줄이니 가로가 두 배 넘게 늘어난 채 확정됐다).
3. **슬롯·구분선은 크기를 맞추면 안 된다.** 상태 묶음이 아니라 <b>서로 다른 그림</b>이라,
   맞추면 7px 짜리 구분선이 26px 빈 칸을 이고 다닌다. 상태 4장일 때만 맞춘다.

### 유물 창 — 착용 캐릭터 이름이 가려지던 것

`HUD_Relics/Detail` 에서 **`Wearer`(y 34~58)와 `EquipButton`(y 8~44)이 10px 겹쳤다.**
게다가 `Wearer` 는 <b>왼쪽 정렬</b>이고 버튼도 왼쪽(x 14~150)에 있어서 «누가 착용 중인지»가
버튼에 통째로 가렸다. 두 줄을 버튼 위로 올렸다 — **Wearer 46~70 · Source 74~100**
(버튼 위로 2px 여유). MCP `update_component` 로 씬을 고치고,
**`Tools/mcp_build_relic_ui.py` 의 원본 값도 같이 고쳤다** — 그 스크립트를 다시 돌리면
재발하기 때문이다.

### 확인된 것

- 콘솔 에러·경고 0. `임포트` 37장 / `배선` 성공.
- **알파가 0 아니면 255만** — `Portrait_Frame` 실측 `[0 255]`, <b>안쪽 60% 불투명 픽셀 0개</b>.
  `Slot_Filled`·`Minimap_Bezel` 도 같다. 반투명 가장자리가 한 픽셀도 없다.
- 낱장 크기가 네이티브와 정확히 일치: `Portrait_Frame` 236×302(= 표시 크기 그대로) ·
  `Hud_Plate` 128×128 · `Btn_Close` 32×32.
- 임포트 설정 전수 검사: Point · PPU 100 · FullRect **37/37**.
- 씬 저장 후 새 스프라이트 GUID **417회 참조**.
- 9-슬라이스를 유니티와 같은 방식으로 파이썬에 다시 그려 표시 크기 17가지에서 <b>경계 넘침 0건</b>.
- 유물 창 배치 재확인: EquipButton 8~44 · Wearer 46~70 · Source 74~100 (겹침 없음).

### 웨이브 배너 — 글자를 판 안으로 (같은 날 추가)

배너를 넣고 보니 <b>글자가 그림 밖에 떠 있었다</b>. 그림을 재 보니 이유가 분명했다:

```
Wave_Banner 338x92 —  y  0~41 : 사슬과 허공 (양끝에 26px 짜리 사슬만)
                      y 42~91 : 판(슬래브) 50px   ← 글자가 들어갈 자리는 여기뿐
```

그런데 씬의 값은 `Phase` 가 y 8~34, `Timer` 가 y 38~78 이었다 — <b>`Phase` 는 통째로
허공에 떠 있고</b> `Timer` 는 판 윗테두리에 걸쳐 있었다. 예전 단색 판(440×92 전체가 판)
기준으로 잡아 둔 값이라 그림이 바뀌자 어긋난 것이다.

★ <b>두 줄로는 안 들어간다.</b> 판이 50px 인데 `Phase` 20pt + `Timer` 34pt 는 54px 이다.
  폰트를 줄여 억지로 쌓으면(15/20pt) 타이머가 읽히지 않는다. 그래서 <b>가로로 나란히</b>
  놓았다 — 판의 안쪽 폭이 360px 이라 오히려 남는다.

| | 자리 (440×92 기준) | 크기 |
|---|---|---|
| `Phase` | x 42~250 · y 50~86 | 20 → **16pt** |
| `Timer` | x 256~398 · y 50~86 | 34 → **28pt** |

MCP `update_component` 로 `RectTransform` 과 `m_fontSize` 를 넣었다 —
<b>둘 다 단순 값이라 MCP 로 들어간다</b>(스프라이트 참조와 달리).
`WaveStatusPanel` 은 <see cref="HudTheme.FitText"/> 도 자동 크기도 안 쓰므로
씬 값이 그대로 정본이다.

#### 그리고 판을 늘렸다 — <b>세로 9-슬라이스</b> (유저 요청: *"그렇게 하고 폰트도 좀 키워줘"*)

50px 판에 글자를 우겨넣는 대신 <b>판 자체를 키웠다</b>. 그냥 높이를 올리면 사슬까지
같이 늘어나므로, 스프라이트에 <b>세로 경계</b>를 줘서 «판의 평평한 띠» 만 늘어나게 했다.

★ <b>어디가 늘어나도 되는지는 실측해야 한다.</b> 행끼리 비교해 보니 <b>y 56~70 만</b>
  서로 같았다 — 그 아래는 판 모서리가 <b>대각선으로 깎여</b> 행마다 폭이 다르다.
  그래서 위 56 · 아래 21 을 고정하고 <b>가운데 15px 만</b> 늘린다
  (`spriteBorder: {x:33, y:21, z:33, w:56}`).

⚠ 이 값은 자동 측정이 못 잡는다(«가운데 행과 똑같은 행» 이 없다). `ui_sprite_cut.py` 에
  `MANUAL_VERT` 를 새로 두고 거기 적었다 — 그림을 다시 뽑아도 값이 따라간다.

| | 전 | 후 |
|---|---|---|
| `HUD_Wave` 높이 | 92 | **128** (판 안쪽이 50 → 51px 로) |
| `Phase` | 16pt · x 42~250 | **22pt** · x 40~246 |
| `Timer` | 28pt · x 256~398 | **34pt** · x 252~400 |
| 둘 다 세로 | y 50~86 | y 60~102 |

⚠ 실제 폰트(네오둥근모)로 폭을 <b>재서</b> 칸을 정했다 — 타이머는 시간뿐 아니라
  <b>한글 네 글자</b>(`진군 중` · `광폭화!`)가 오는데 34pt 에서 119px 이라 148px 칸에 들어간다.
  `Phase` 는 최장 `웨이브 20 · 광폭화` 가 22pt 에서 198px, 칸이 206px 이다.

#### 배너 <b>아래 꼬리를 잘라내고</b> 구분선으로 막았다 (유저 요청)

유저 요청: *"웨이브 타이머 아래에 구분선 넣어서 폰트 아래쪽에서 짤리게 만들어줘
지금 체력바로 가려지는 것처럼"* → 확인: *"텍스트를 가리지 말고 구분선을 이용해서
<b>이미지를 자르라고</b>"*.

⚠ <b>처음에 잘못 알아들었다.</b> 구분선을 글자 위에 얹어 «글자» 를 자르는 것으로 읽고
  `TimerCut` 을 y 86 에 놓았다. 자르라는 것은 <b>그림</b>이었다.

★ <b>«체력바로 가려지는» 은 내가 만든 겹침이었다.</b> 배너를 92 → 128 로 키우면서
  `HUD_Wave`(화면 y 16~144)가 `HUD_Boss`(y 116~190)를 <b>28px 파고들었다</b>(가로도
  740~1180 안에 680~1240 으로 완전히 겹친다). 보스전에서 체력바가 배너 아랫부분을
  덮고 있었고, 유저는 <b>그 «짧아진 배너»</b> 가 나으니 그렇게 만들어 달라고 한 것이다.

**그림을 실제로 잘랐다.** 배너는 판 아래가 <b>대각선으로 40px 넘게 늘어져</b> 있었는데
글자는 판 위쪽에만 들어간다. 그 꼬리를 잘라 평평한 단면으로 끝내고 구분선이 막는다.

⚠ <b>자를 행은 실측해서 정했다</b> — y 0~64 까지가 전폭(338)이고 <b>65부터 대각선이
  파고든다</b>. 65 이후에서 자르면 단면이 삐뚤어진다. `ui_sprite_cut.py` 에
  `CROP_BOTTOM` 을 새로 두고 적었다(그림을 다시 뽑아도 따라간다).

| | 전 | 후 |
|---|---|---|
| `Wave_Banner` 스프라이트 | 338×92 · 경계 T56 **B21** | **338×65** · 경계 T56 **B2** |
| `HUD_Wave` | 440×128 (화면 y 16~144) | **440×112** (y 16~128) |
| `TimerCut` | y 86~94 · <b>글자를 덮음</b> | **y 104~112 · 폭 0~440** (단면을 막음) |
| `HUD_Boss` | y 116 (28px 겹침) | **y 136** (배너 아래 8px 여유) |

`Phase`·`Timer` 는 y 60~102 그대로 — <b>이제 아무것도 안 덮는다</b>.

#### 프레임에 먹힌 글자 전수 수정 (유저 요청)

유저 요청: *"그 이미지들에 가려서 텍스트 짤리는 것들 수정 좀. 스킬 설명이나 그런거"*.

★ <b>원인은 하나다.</b> 예전 판은 테두리가 <b>1~2px 단색 선</b>이라 글자를 가장자리
  14~18px 에 놓아도 됐다. 픽셀 UI 를 깔면서 창 <b>23px</b> · 판 <b>10px</b> ·
  게이지 <b>7px</b> 짜리 테두리가 생겼고, 그 안에 있던 제목·힌트·본문이 밑으로 들어갔다.

**눈으로 찾지 않았다** — 도구를 둘 만들었다.

| 도구 | 하는 일 |
|---|---|
| `Tools/ui_text_clip_check.py` | 씬 YAML 을 읽어 <b>먹힌 글자를 전수 보고</b>(고치지는 않는다) |
| `Editor/UiTextInset.cs` (`LastSanctuary/UI/글자 여백`) | 스프라이트 경계를 읽어 <b>안쪽으로 민다</b> |
| `UiSkinApplier.InsetLabel` | 버튼 라벨은 배선할 때 같이 민다 |

★ 셋 다 여백을 <see cref="Sprite.border"/> <b>에서 읽는다</b> — 그림을 다시 뽑아
  테두리가 바뀌어도 다시 돌리면 다시 맞는다.

**43건 → 7건.** 남은 7건은 전부 가짜다: 가운데 정렬 짧은 글의 «렉트만» 넘친 것
(`HUD_Event/Title` · `HUD_Roster/Title` · 침식 라벨 2개), 레이아웃이 자리를 정하는 것
(`RelicSlot` 2개 · `LineTemplate`).

### 검사 도구를 만들며 걸린 함정 넷

1. ★★ <b>렉트가 아니라 «그려지는 글자» 로 재야 한다.</b> 버튼 라벨은 렉트가 장식까지
   덮지만 <b>가운데 정렬</b>이라 글자는 안 잘린다. 렉트로 재면 198건이 나오는데 대부분
   가짜다. 실제 폰트(네오둥근모)로 폭을 재고 정렬을 적용해 판정한다.
   ⚠ 단 <b>글이 비어 있거나</b>(런타임에 채워진다 — 스킬 설명이 그렇다) <b>줄바꿈이
   켜진</b> 칸은 렉트로 재야 맞다. 그 둘을 «짧다» 고 넘기면 정작 본문이 빠진다.
2. ★★ <b>anchoredPosition 의 기준은 앵커 «중심»</b>이다. 왼쪽으로 잡았더니 스트레치된
   자식이 <b>부모 폭의 절반</b>만큼 통째로 어긋나 오진이 쏟아졌다.
3. ★★ <b>`GetComponentInParent` 는 꺼진 부모를 건너뛴다.</b> HUD 창은 대부분 꺼진 채로
   저장되므로 <b>인자에 true</b> 를 줘야 한다. 안 주면 버튼 라벨이 «버튼 밑이 아닌 것»
   으로 읽혀 두 번 밀린다.
4. ★★ <b>안전 영역보다 «넓은» 칸은 밀면 안 된다.</b> 밀면 반대쪽이 그만큼 나가서 돌릴
   때마다 좌우로 <b>핑퐁</b>한다(로그 줄 템플릿이 +6 / -6 을 반복했다). 그런 칸은 위치가
   아니라 크기가 문제다. 그리고 <b>말이 안 되는 값(200px 초과)은 통째로 건너뛴다</b> —
   꺼진 창의 월드 모서리가 엉뚱하게 나와 «가로 -48504» 가 실제로 찍혔다.

⚠ 3·4 번을 넣기 전에 돌린 판이 <b>저장돼서</b> 렉트 두 개가 수천 픽셀 밖으로 나갔다
  (`RelicSlot/Head` 27→-7905 · `Name` -9→-48513). 커밋본과 대조해 <b>그 둘만</b>
  어긋난 것을 확인하고 MCP 로 되돌렸다. 지금은 커밋본 대비 100px 넘게 움직인 렉트가 0 이다.

### 곁들여 고친 것

- **창 모서리 닫기 버튼이 프레임 모서리 장식에 물렸다** — 창 가장자리 12px 에 있어서
  23px 짜리 레일 밑으로 들어갔다. `HUD_Dig`·`HUD_Relics` 를 27px, `HUD_Subjugate` 를
  27px 로 밀었다.
- **배속 칩을 `Btn_Speed_*` → `Btn_Chip_*` 으로 바꿨다.** 전용 그림은 원화가 3:1 로 나와
  좌우 장식이 13px 씩이라, 57px 폭에 넣으면 글자 자리가 <b>23px</b> 밖에 안 남아
  «정지»(20pt · 40px)가 삐져나왔다. 칩은 장식이 6px 라 45px 이 남는다.
  ⚠ `Btn_Speed_*` 4장은 <b>이제 안 쓴다</b>(파일은 남겨 뒀다).
- ★★ **배속을 고르면 글자가 사라지던 것** (유저 지시: *"텍스트가 사라지게 하지 말고
  다른 것들처럼 초록색으로"*). `activeTextColor` 가 <b>거의 검정</b>(0.05, 0.08, 0.10)
  이었다 — 그때는 선택된 칩 배경이 <b>밝은 청록</b>이라 어두운 글자가 맞았는데, 픽셀 UI 의
  «켜짐» 그림이 <b>어두운 청록 판</b>이 되자 그대로 묻혔다. <see cref="HudTheme.TextAccent"/>
  와 같은 청록(0.45, 0.95, 0.78)으로 바꿨다 — 코드 기본값과 씬 값 <b>둘 다</b>.

#### ★★ 눌러도 색이 «바로» 안 바뀌던 것 (유저 보고)

유저 보고: *"버튼 클릭 시 선택했을때 바로 청록색으로 바뀌는게 아니라 클릭하고 다른 곳
클릭해야 색 바뀐다"*.

<b>원인 — 유니티가 쓰는 손잡이와 우리가 쓰는 손잡이가 다르다.</b>
`SpriteSwap` 은 <see cref="Image.sprite"/> 가 아니라 <see cref="Image.overrideSprite"/> 를
갈아끼우고, 그 값이 <b>언제나 이긴다</b>. 버튼을 누르면 이벤트 시스템이 그 버튼을
«선택됨» 으로 붙들고 있으므로 `overrideSprite = selectedSprite`(= 평시 그림)가 걸려,
`HudTheme.PaintButton` 이 `sprite` 에 넣은 «켜짐» 그림을 <b>덮어버린다</b>.
다른 곳을 누르면 선택이 풀려 `overrideSprite` 가 사라지고 <b>그제야</b> 보였다.

⚠ UI-60 절에서 «둘은 서로 안 싸운다» 고 적었는데 <b>틀렸다</b>. `overrideSprite` 가
  `sprite` 를 가린다 — 그래서 코드가 정하는 상태는 `overrideSprite` 까지 손봐야 한다.

**고친 방법 — `HudTheme.ClaimSelectable`** 이 두 가지를 한다:
  ① `overrideSprite` 를 <b>지운다</b> — 지금 당장 바뀌어 보이게.
  ② 버튼의 `SpriteState` 를 <b>지금 상태에 맞게 다시 쓴다</b> — «켜짐» 이면
     올림·눌림·선택이 <b>전부 켜짐 그림</b>이 된다.

★ <b>②가 없으면 마우스를 얹는 순간 다시 풀린다</b> — 올림 그림이 평시 계열이라 켜진
  배속 위에 커서만 둬도 안 켜진 것처럼 보인다. ①만으로는 모자라다.
⚠ 그림이 <b>실제로 바뀔 때만</b> 부른다 — 이 경로는 창이 열릴 때마다 지나가는데
  매번 `SpriteState` 를 다시 쓰면 헛일이다.

이 하나로 <b>코드가 «켜짐» 을 칠하는 모든 버튼</b>이 같이 고쳐진다 — 배속·정지 ·
액션 바(창 열림) · 전술 옵션 · 성장 유형 · 협동 탐험 · 집결지.

#### 로스터 행 배치 · 성장 창 «칸» 에 판 그림 (유저 요청)

유저 요청: *"캐릭터 로스터에 텍스트 짤리는거 수정하고 캐릭터 성장 스탯이랑 스킬 칸에도
UI 이미지로 깔끔하게 만들어줘"* (+ *"텍스트 안 짤리게 조심"*).

**① 로스터 행 — 겹쳐 있었다.** 340×78 행을 재 보니 두 군데가 포개져 있었다:
`Name`(x 14~238)이 `Duty`(x 232~332)와 <b>6px 겹치고</b>, `HpBack`(y 26~52)이
`Name`(y 14~34)을 <b>8px 파고들었다</b>. 이름이 오른쪽으로 길어지면 «맡은 일» 글자
밑으로 들어가고 아래는 체력바가 덮었다. 겹치지 않게 다시 쌓았다:

| | 전 | 후 |
|---|---|---|
| `Name` | x 14~238 · y 14~34 | **x 14~226 · y 8~30** |
| `Duty` | x 232~332 · y 14~34 | x 232~332 · **y 8~30** |
| `HpBack` | y 26~52 | **y 34~54** |
| `ErosionBack` | y 56~72 | **y 58~72** |

그 위에 안전망으로 `HudTheme.FitText` 를 이름·맡은일에 걸었다(줄바꿈 끔) — 이름은
«이름 Lv.N» 이라 <b>이름이 길면 여전히 넘칠 수 있다</b>. 자르지 않고 작게 맞춘다.

**② 성장 창 스탯 12칸 · 스킬 3칸에 `Hud_Plate` 를 깔았다.**

⚠ 이 칸들은 <b>버튼이다</b>. 그래서 비율만 보면 스킬 칸(280×176 = 1.59:1)이 「닫기」
  그림으로 떨어져 모서리가 뭉갠다 — 이름으로 먼저 걸러 판 그림을 준다.

⚠ ★★ <b>글자 여백 도구가 이 칸들을 건너뛴다</b> — 버튼 밑의 글자는 배선의
  `InsetLabel` 이 맡는 것으로 갈라 놨는데, `InsetLabel` 은 <b>늘어난 라벨</b>만 다룬다.
  이 칸의 이름·값·증감은 좌상단에 고정된 넷~다섯이라 <b>둘 다 안 건드린다</b>.
  → `UiSkinApplier.LayoutCell` 을 새로 만들어 <b>값으로 못박았다</b>.

★ <b>자동 «밀기» 를 쓰면 안 되는 자리</b>다 — 위아래로 붙어 있어서 각자 안쪽으로 밀면
  서로 겹친다. 판 테두리가 위 10 · 아래 8 이라 안쪽이 <b>y 10~58 뿐</b>이고, 거기에
  이름(12~30)과 값(32~56)을 손으로 나눠 넣었다.

**③ 칸의 색을 «색» 에서 «명암» 으로 바꿨다.** 예전 값(잠김 0.07,0.08,0.10 등)은
<b>칸 자체의 색</b>이었는데, 이제 어두운 판 그림 위에 <b>곱해진다</b> — 그대로 두면
판이 새까매진다. 여섯 개를 전부 흰 계열로 바꿨다(코드 기본값과 씬 값 둘 다).
«고른 것»만 청록 기운(0.55, 1, 0.90)을 섞어 판이 청록으로 물들게 했다.

**확인** — 15칸(스탯 12 · 스킬 3)의 자식 전부를 다시 재서 <b>테두리를 넘는 자식 0</b>,
칸 안에서 서로 겹치는 것도 없음(`Hint`/`RageBack` 은 <b>같은 자리를 번갈아 쓰는</b>
사이라 의도된 겹침). 로스터 행도 겹침 0.

#### 스킬 아이콘을 «칸 테두리» 가 덮던 것 (유저 보고)

유저 보고: *"캐릭터 선택 시 나오는 UI에 미해금 된 스킬 아이콘이 배너 UI 에 미세하게
가려진다"*.

★★ <b>정사각 소켓을 가로로 늘리면 테두리도 같이 늘어난다.</b>
`Slot_Empty` 는 43×44 정사각이고 경계가 <b>0</b>(늘리지 않는 그림)이다. 그런데
초상화의 스킬 줄은 <b>208×44</b>(비 4.73)라 `Simple` 로 4.8배 늘어나면서 6px 짜리
왼쪽 테두리가 <b>29px</b> 이 됐다 — x 8~40 에 있는 `Icon` 을 그만큼 덮었다.
"미세하게" 로 보인 이유는 아이콘 <b>왼쪽 일부만</b> 물렸기 때문이다.

**모양으로 갈라 줬다.**

| 칸 | 크기 | 비 | 그림 |
|---|---|---|---|
| 부대 카드 초상화 `Slot_00~03` | 82×72 | 1.14 | `Slot_Empty` (정사각 소켓 그대로) |
| 초상화 스킬 줄 `Slot0~2` | 208×44 | 4.73 | **`Bar_Track`** (Sliced) |
| 성장 창 유물 칸 `RelicSlot` | 882×58 | 15.2 | **`Bar_Track`** (Sliced) |

★ `Bar_Track` 은 경계가 <b>가로에만</b> 있다(L7 R7 · 위아래 0). 늘려도 위아래 테두리가
  안 생기고, 좌우 마개는 7px 이라 아이콘(8px 부터)을 <b>건드리지 않는다</b>.

⚠ <b>초상화의 `Hp` 와 `Skills` 가 같은 자리(y 86)를 쓰는 것은 버그가 아니다</b> —
  `UnitPortraitPanel` 이 `_hpRoot.SetActive(character == null)` ·
  `_skillsRoot.SetActive(character != null)` 로 <b>서로 배타</b>로 켠다(몬스터는 체력,
  캐릭터는 스킬). 커밋본과도 같아서 손대지 않았다.

#### 곁들여 — 앵커가 깨져 있던 렉트 셋

찾다가 발견했다. `HUD_Growth/Stats/RelicSlot` 의 자식 셋이 <b>앵커가 0~1 밖</b>이었다:

```
Head  m_AnchorMax: {x: 10, y: -26}      ← 앵커는 0~1 이어야 한다
Name  m_AnchorMax: {x: 56, y:   4}
```

그래서 폭이 `(10-0) × 882 - 66 = 8754px` 로 계산돼, 이 절의 검사 도구가 «가로 -7932» ·
«아래 1526» 같은 값을 뱉었다. <b>커밋본에도 있는 기존 버그</b>다(내가 만든 것이 아니다).
유물 칸을 사람이 읽을 수 있게 다시 잡았다 — `유물`(x 12~60) · 아이콘(70~108) ·
이름(118~870), 전부 세로 가운데.

**확인** — 씬 전체에서 <b>앵커가 0~1 밖인 렉트 0개</b>. 글자 삐져나옴은 8 → 6 건
(남은 6 은 전부 가짜: 가운데 정렬 짧은 글의 렉트만 넘친 것과 꺼진 템플릿).

### 새로 생긴 것 / 사라진 것

- **신규**: `Btn_Speed_*`(배속 칩 전용 — 예전엔 정사각 「닫기」를 늘려 쓰고 있었다) ·
  `Wave_Banner`(웨이브 표시 — 예전엔 `Hud_Plate` 를 쓰고 있었다).
- **삭제**: `Btn_Wide_*` 4장 — painted 시절 예비본. 픽셀 세트와 톤이 안 맞고 씬에서 안 쓰였다.
- **아직 안 씀**: `Divider_Diamond` · `Divider_Header` · `Slot_Filled` · `Slot_Locked`.
  뒤의 둘은 «칸이 찼다/잠겼다» 를 코드가 갈아끼워야 붙는다.

### 아직 확인 못 한 것 (유저가 볼 것)

- **플레이 모드 검증 전혀 안 함.**
- **로비는 그대로 painted 다**(`LobbyButton.png`). 픽셀 게임이 타이틀만 일러스트로 가는 건
  흔한 관례라 두었는데, 통일하려면 로비도 다시 뽑아야 한다 — <b>유저 판단</b>.
- 액션 바(240×40)의 평평한 가운데가 **192px** 이다. 「캐릭터 생성 170」 이 들어가는지 볼 것.
- `Btn_Panel` 을 116px 짜리 버튼에 쓰면 좌우 장식(22+22)이 거의 붙는다.

### 씬 변경 여부

**있음.** `save_scene` 1회. 스프라이트 참조 417건 교체(+ `Btn_Speed`·`Wave_Banner` 신규 배선),
유물 창 `Wearer`·`Source` 위치 수정.

### 씬반영요청 목록

없음 (이 브랜치가 씬 소유자).

---

## UI-62. ★★★ <b>발굴이 한 판도 돈 적이 없었다</b> · 유물 중복 금지 · 토벌 지시 확장(적정 레벨 · 두 부대) (2026-08-25, 20차)

### 무엇을 / 왜

유저 지시 여섯:

| # | 유저의 말 | 고친 곳 |
|---|---|---|
| ① | *"발굴 기능이 구현이 안된거같은데 한 번 확인해줘"* | `Map/MapGenerator.cs` · `Relics/RelicDigService.cs` |
| ② | *"유물 중복 획득 안되게 수정해줘"* | `Relics/RelicRegistry·RelicInventory·RelicDropService` |
| ③ | *"중립 에픽 몬스터의 적정 레벨을 … 에픽 몬스터 오른쪽 끝에다가 표시해"* | 표 컬럼 신설 · `SubjugationPanel` · 씬 |
| ④ | *"해당 부대 오른쪽 끝에는 지금 토벌을 보낸 몬스터의 이름이 뜨게"* | `SubjugationPanel` · 씬 |
| ⑤ | *"한 몬스터 토벌은 최대 두개의 부대까지 설정 가능하게"* | `Units/EpicSubjugationService.cs` |
| ⑥ | *"중립 에픽 몬스터 리젠시간 테이블 참고해서 2배로"* · 도움말 한 줄 추가 | 에셋 4개 · 볼트 표 둘 |

**씬 변경** — `HUD_Subjugate` 의 두 모체에 글자 칸 하나씩 **MCP 로 신설** · 폰트 굽기 · `save_scene`

---

### UI-62-1. ★★★ ① <b>발굴은 «구현이 안 된» 것이 아니라 «한 칸도 안 놓이고» 있었다</b>

코드는 전부 있었다 — 자리 고르기 · 표식 · 배정 · 삽질 · 결과 · 저장까지.
그런데 <b>플레이 로그에 `[유물]` 로 시작하는 줄이 한 개도 없었다</b>(`logChanges` 는 켜져 있다).
로그가 없다는 것은 <b>로그보다 앞에서 빠져나갔다</b>는 뜻이다.

```
RelicDigService.PickSites()
  ├ if (_map == null || digSiteCount <= 0) return;    ← 로그보다 앞
  ├ Vector2Int size = _map.MapSize;
  ├ if (size.x <= 0 || size.y <= 0) return;           ← ★ 여기서 빠져나갔다 (로그보다 앞)
  └ Debug.Log("발굴 가능 칸 N개 배치")                 ← 한 번도 도달하지 못했다
```

<b>왜 <c>MapSize</c> 가 0 이었나</b> — 씬의 `MapGenerator` 는 <b><c>generateOnAwake: 0</c></b> 이다.
이 판의 지형은 <b>에디터에서 미리 구워 타일맵에 직렬화</b>돼 있고 런타임에는 만들지 않는다.
그런데 `MapSize`·`Origin` 은 <c>{ get; private set; }</c> 라 <b><c>Generate()</c> 안에서만</b>
채워졌다 → 게임 중에는 <b>영원히 (0,0)</b>.

★★ <b>왜 이것 하나만 걸렸나</b> — 맵 크기를 읽는 곳이 <b>여섯</b>인데 다섯은 전부
<c>mapGenerator.<b>Config</b>.MapSize</c>(에셋)를 읽는다. `RelicDigService` <b>혼자만</b>
런타임 속성을 읽고 있었다:

```
FogOfWarService · FlowFieldService · GridPathfinder · MonsterSpawner · NeutralMonsterSpawner
   → Config.MapSize   (에셋 — 언제나 옳다)
RelicDigService
   → _map.MapSize     (런타임 — 만든 적이 없으면 0)   ★ 이 한 줄
```

→ <b>속성이 스스로 맞게</b> 고쳤다. 만든 적이 없으면 <c>config</c> 를 그대로 돌려준다.
  <c>Generate()</c> 도 <c>config.MapSize</c> 를 옮겨 담을 뿐이라 <b>두 값은 언제나 같다</b> —
  폴백이 «다른 값» 이 아니라 «같은 값» 이다.
★ 이렇게 두면 <b>앞으로 이 값을 읽는 코드가 늘어도</b> 같은 함정에 안 빠진다.
  «Config 를 읽어라» 를 <b>사람이 기억하는 대신 코드가 지킨다.</b>

### UI-62-2. ⚠⚠ ① 진짜 교훈 — <b>조용한 실패는 «기능이 없는 것» 과 구별되지 않는다</b>

이 버그가 오래 산 이유는 <b>아무 말도 하지 않았기</b> 때문이다. 발굴 칸이 0개면
표식(느낌표)이 0개고, 누를 것이 없으니 화면상 «그런 기능이 아직 없다» 와 <b>똑같이 보인다</b>.
147절이 이 표식을 «글자에서 주황 느낌표 원화로» 바꾸며 «크기·튀는 높이는 실물로 판단해야
한다» 고 적었는데 — 그때도 <b>실물은 한 번도 화면에 없었다</b>.

→ `PickSites` 의 두 early return 에 <b>경고를 달았다</b>. 못 하면 반드시 이유를 말한다.

### UI-62-3. ★★ ② 유물 중복 — <b>«들어오는 문» 한 곳에서 막는다</b>

주는 통로가 <b>넷</b>이다: 발굴 · 일반 처치 드랍 · <b>보스 고유 드랍</b> · <b>사건 보상</b>.
뒤의 둘은 <b>추첨을 거치지 않고</b> 정해진 유물을 곧바로 준다. 통로마다 검사를 흩으면
다섯 번째 통로가 생기는 날 반드시 빠뜨린다 — `HudExclusive` 가 창 배타에서 내린 결론과 같다.

**두 겹으로 막았다.**

| 겹 | 어디 | 무엇 |
|---|---|---|
| ① 추첨 | `RelicRegistry.RollGrade` | 이미 가진 것을 <b>후보에서 먼저 빼고</b> 가중치를 다시 합한다 |
| ② 지급 | `RelicInventory.Grant` | 그래도 중복이면 <b>거절하고 `false`</b> — 마지막 방어선 |

★ <b>«뽑고 나서 버리는» 방식이 아니다.</b> 그러면 다 모을수록 «아무것도 안 나오는» 판정이 늘어
  <b>체감 확률이 조용히 떨어진다</b>. 후보에서 먼저 빼면 남은 것들 사이의 비율이 표 그대로다.
★ <b>보스 고유 드랍은 굴리지도 않는다</b> — 굴린 뒤 버리면 «떴는데 안 준» 로그가 남아 헷갈린다.
★ 한 등급을 <b>다 모으면</b> 발굴 결과가 «<b>일반 유물은 이미 다 모았습니다</b>» 로 말한다.
  조용히 «아무것도 안 나옴» 이면 유저가 «또 고장났나» 로 읽는다(160-2 의 교훈 그대로).
⚠ `Grant` 의 반환형이 <c>void → bool</c> 로 바뀌었다. `Give` 는 <b>거절당하면 로그도 창도
  띄우지 않는다</b> — 「남겼습니다」 가 뜨는데 보관함에는 안 늘어나는 것이 가장 나쁘다.

### UI-62-4. ③ 적정 레벨 — <b>계산이 아니라 표의 값</b>

유저가 준 기준: <b>부대 하나(4명)의 레벨</b>이 이 값에 닿았을 때가 «갈 만하다».

| 몬스터 | 적정 | 리젠(전 → 후) |
|---|---:|---:|
| 카르시노스 (1101) | **Lv.10** | 600 → **1200** |
| 아니사킬 (1102) | **Lv.15** | 600 → **1200** |
| 바리올라 (1103) | **Lv.20** | 600 → **1200** |
| 폴리르 (1104) | **Lv.25** | 800 → **1600** |

★ <b>능력치에서 «계산해» 뽑지 않았다</b> — 그러면 밸런스를 만질 때마다 권장치가 제멋대로
  흔들린다. 이것은 계산값이 아니라 <b>기획이 정한 문턱</b>이므로 표에 <b>새 컬럼</b>
  (`recommend_level`)을 만들어 적힌 그대로 보여 준다(몬스터 크기를 상수에서 표 컬럼으로
  옮긴 118-3절과 같은 판단).
⚠ 잡몹 중립은 <b>0</b> 으로 채웠다 — 빈칸이면 «아직 안 정한 것» 인지 «없는 것» 인지 모른다.
★ `sync_tables_to_assets.py` 에 매핑 한 줄을 더했다 — 표를 다시 밀어도 값이 살아남는다.

<b>색으로 «갈 만한가» 를 말한다</b> — 고른 부대의 <b>평균 레벨</b>(살아 있는 사람만)과 견준다.
부대를 아직 안 골랐으면 <b>회색으로 숫자만</b> 보여 준다(거짓 안심·거짓 경고를 만들지 않는다).
⚠ 레벨은 <c>UpgradeCount</c> 다 — 로스터·초상화가 «Lv.N» 으로 부르는 그 값이다.

### UI-62-5. ③④ 씬 — 모체에 글자 칸 <b>하나씩</b>

```
HUD_Subjugate
├─ Squads/RowTemplate                    ★모체
│   ├─ Name · Order
│   └─ Target  ← 신설 · 오른쪽 정렬 · 지금 토벌 보낸 몬스터 이름
└─ Targets/RowTemplate                   ★모체
    ├─ Art · Name · Hp
    └─ Level   ← 신설 · 오른쪽 정렬 · 「적정 Lv.N」 + 「부대 n/2」
```

★ <b>모체 하나만 MCP 로 만들었다</b> — 줄 개수는 런타임에 정해지므로 복제는 스크립트가 한다
  (템플릿 복제 예외, §10 H-2). 유저 지시 *"템플릿 슬롯 복제 하는 경우를 제외하고는
  하드 코딩을 하지말고 mcp 연결해서 직접 생성 및 수정"* 그대로다.
⚠ <b>비활성 창은 MCP 가 경로로 못 찾는다</b> — `HUD_Subjugate` 를 잠깐 켜고 만든 뒤 다시 껐다.
  (`get_gameobject`/`update_component` 가 전부 활성 오브젝트만 훑는다.)
⚠ <b>글자를 새로 만들면 폰트를 다시 굽는다</b> — 안 하면 새 칸만 TMP 기본 폰트로 남아
  한글이 안 보인다. `LastSanctuary/폰트/네오 둥근모 TMP 에셋 굽고 씬에 적용` 을 MCP 로 실행했다.
★ <b>왼쪽 둘째 줄의 뜻을 바꿨다</b> — 예전에는 거기 몬스터 이름이 있었다. 이름이 오른쪽으로
  갔으므로 왼쪽은 «지금 무엇을 하는 중인가»(<b>명령 없음 / 토벌 중</b>)만 말한다.
  <b>같은 글자를 한 줄에 두 번 적으면 어느 쪽이 정본인지 알 수 없어진다.</b>

### UI-62-6. ⑤ 한 대상에 <b>두 부대까지</b>

명령의 정본은 <c>_orders</c>(부대 id → 대상) 하나뿐이라 <b>거기서 세면 언제나 맞는다</b>.
UI 가 자기 화면을 세면 «스크롤 밖·갱신 전» 부대를 빠뜨린다.

```
SquadCountOn(target)   지금 이 대상에 붙은 부대 수
CanOrder(squad, t)     이미 그 대상을 맡고 있으면 참 (다시 눌러 해제하는 길을 막지 않는다)
SetOrder(...) → bool   정원이 차면 거절하고 false
```

★ 정원은 <b>인스펙터 값</b>(`GameSystems ▸ EpicSubjugationService ▸ maxSquadsPerTarget = 2`).
  1 로 두면 예전 동작이다.
★ <b>거절을 조용히 하지 않는다</b> — 창의 안내줄에 «이 대상에는 이미 2개 부대가 가 있습니다» 가
  2.5초 뜨고 로그에도 남는다. 조용히 덮어쓰면 «눌렀는데 아무 일도 안 일어난다» 가 된다.

### UI-62-7. ⑥ 도움말 — 표에서 고치고 다시 구웠다

`help_epic` 2단계 문구 아래에 유저가 준 문장을 <b>그대로</b> 한 줄 붙였다:

```
어느 부대를 보낼지 여기서 고릅니다. 지시를 내린 부대만 잡으러 갑니다.
중립 에픽 몬스터는 최대 두 개의 부대까지 토벌을 보낼 수 있습니다
```

★ <b>에셋을 직접 고치지 않았다</b> — 볼트 `Last_Sanctuary_도움말테이블_Ver01.xlsx` 의
  `HelpStep` 시트를 고치고 `gen_help_assets.py` 로 다시 구웠다. 에셋만 고치면 다음 굽기에
  <b>조용히 되돌아간다</b>(159-9절의 그 규칙).
✓ 굽기의 검산(«한 UI 안» 규칙 · 스트링 키 · 계기)이 전부 통과했다.

### 검증

* `recompile_scripts` **에러 0 · 경고 0** · 플레이 중 콘솔 **에러 0**
* ★★★ <b>발굴 — 실측으로 확인했다.</b> 고치기 <b>전</b> 플레이: `[유물]` 로 시작하는 줄
  <b>0개</b>. 고친 <b>뒤</b> 플레이: <b>`[유물] 발굴 가능 칸 24개 배치 (목표 24 · 시도 29)`</b>.
  시도 29회로 24칸을 채웠다 — 조건이 넉넉하다는 뜻이다.
* 씬 실측 — `Squads/RowTemplate/Target` · `Targets/RowTemplate/Level` 둘 다
  폰트 `c9323a04…`(네오 둥근모) · 가로 정렬 4(오른쪽) · 세로 512(가운데) · 15pt
* 에셋 실측 — 에픽 4종 `respawnSeconds` 1200/1200/1200/1600 · `recommendLevel` 10/15/20/25
* 도움말 에셋 실측 — `stepText` 에 두 줄이 들어갔다(`\n` 이스케이프)

### 아직 확인 못 한 것 (유저가 볼 것)

1. ★★ <b>토벌 지시 창의 새 두 칸은 눈으로 봐야 한다.</b> 에픽은 게임 시작 <b>300초</b> 뒤에
   처음 나오고 그 전에는 목록이 비어 있어, 이번 확인에서는 창을 채운 모습을 못 봤다.
   글자가 넘치거나 이름과 겹치면 `HUD_Subjugate` 의 두 모체에서 `Target`·`Level` 의
   폭(`sizeDelta.x`)만 조절하면 된다.
2. ★ <b>적정 레벨 값 자체는 «첫 값» 이다</b>(10/15/20/25). 실제로 그 레벨의 4명이 감당하는지는
   플레이로 봐야 한다. 볼트 표 `임시용 중립 몬스터.xlsx` 의 `recommend_level` 에서 고치고
   `sync_tables_to_assets.py` 를 돌리면 된다.
3. ★ <b>리젠 2배는 «덜 나온다» 를 뜻한다</b> — 에픽 처치가 유물·에너지의 큰 몫이라
   한 판의 총수입이 함께 줄어든다. 159절의 자원 성장 조정과 <b>같은 방향</b>이라
   둘이 겹쳐 너무 마를 수 있다. 마르면 `respawnSeconds` 를 먼저 되돌리는 편이 낫다
   (자원 성장 쪽은 상한이 있어 되돌리기가 더 거칠다).
4. **유물을 다 모으면** 발굴·드랍이 «이미 다 모았습니다» 로 뜬다. 일반 16종 · 레어 13종이라
   한 판에 다 모으기는 어렵지만, 도달하면 그 등급의 드랍이 <b>완전히 마른다</b>.
   중복을 금지한 이상 이것이 옳은 동작이지만, 대신 «에너지» 같은 다른 보상으로 갈음하고
   싶으면 말씀해 주세요.

### 씬반영요청 목록

- 없음 (MCP 로 만들고 폰트 굽고 `save_scene` 했다)

---
