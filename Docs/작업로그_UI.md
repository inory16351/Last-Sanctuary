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
