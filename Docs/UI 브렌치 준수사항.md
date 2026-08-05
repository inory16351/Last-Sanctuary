# UI 브랜치 준수사항 — 전술 UI · 캐릭터 생성 · 집결지 설정 담당

> **대상**: 이 PC에서 `UI` 브랜치로 HUD/전술 UI 작업을 진행하는 Claude Code 및 그 서브에이전트
> **작성**: 2026-08-05
> **짝 문서**: [`프로토 브렌치 준수사항.md`](프로토%20브렌치%20준수사항.md) · [`머지 계획.md`](머지%20계획.md)
> **필독 선행 문서**: `C:\Project\라스트 생추어리\진행상황.md` (게임 전체 구조·함정 기록. **작업 시작 전 전체를 읽을 것**)
> **UI 목업**: `C:\Project\라스트 생추어리\UI\Last Sanctuary HUD.html`
> **기획서**: `C:\Project\라스트 생추어리\기획서\` (컨셉 / 핵심시스템 / 웨이브 / 캐릭터 성장 / 비주얼 가이드)

---

## 🔒 §0. 문서 수정 규약 — 이 문서를 여는 모든 사람·에이전트가 먼저 읽을 것

이 문서는 참고 메모가 아니라 **두 브랜치가 서로를 깨뜨리지 않기 위해 합의한 구속력 있는 규약**이다.
**임의 수정·삭제·완화 금지.** 갱신이 필요하면 아래 프롬프트를 그대로 읽고 따를 것.

> ### 갱신 프롬프트 (이 문서를 고치려는 에이전트는 그대로 따를 것)
>
> 너는 지금 `UI 브렌치 준수사항.md` 를 갱신하려 한다. 이 문서는 `main` 으로의 최종 머지를 **충돌 0으로** 끝내기 위한 제약 조건 명세다. 아래를 전부 지켜야 갱신한 것으로 인정된다.
>
> 1. **기존 제약을 지우거나 약화시키지 말 것.** 제약이 현실과 맞지 않으면 삭제가 아니라 "§N 은 이런 이유로 이렇게 바뀐다"를 **새 항목으로 추가**하고 원래 항목에 링크한다. (`진행상황.md` 0절 3번과 같은 원칙 — 완료·폐기된 기록도 남긴다)
> 2. **추가만 한다.** 본문의 § 번호와 순서를 재배치하지 말 것 — 짝 문서와 `머지 계획.md` 가 § 번호로 이 문서를 인용하고 있다.
> 3. **§2(파일 소유권) · §3(씬 소유권) · §4(계약 파일 동결)은 단독으로 바꿀 수 없다.** 이 셋의 변경은 **유저의 명시적 승인 + 양쪽 브랜치 문서 동시 갱신**이 있어야만 유효하다. 한쪽만 바꾸면 머지가 깨진다.
> 4. 갱신할 때마다 맨 아래 **§12 갱신 이력**에 (날짜 / 브랜치 / 무엇을 / 왜) 한 줄을 남긴다. 이력 없는 변경은 무효로 보고 되돌린다.
> 5. **작업 방향을 유지할 것.** 이 문서의 목적은 "빠르게 UI를 만들기"가 아니라 **"머지 시점에 충돌 없이 합쳐지기"** 다. 편의를 위해 상대 소유 파일(특히 `Proto_01.unity`)을 건드리는 판단은 어떤 경우에도 하지 말고, 필요하면 §7 의 요청서 경로를 쓴다.
> 6. **이 §0 규약 블록 자체는 수정 금지.**

---

## §1. 이 브랜치의 범위

### 맡는 것 (UI)
| 영역 | 내용 |
|---|---|
| **전술 UI** | 전투 성향 / 전투 포지션 / 행동 지침 — 목업의 "지정 캐릭터 명령" 패널. **값을 정하고 계약(§4)에 넣는 것까지가 UI의 일**, 그 값에 AI가 반응하는 것은 PROTO의 일 |
| **캐릭터 생성** | 캐릭터 관리 패널, 생성 버튼, 에너지 비용 규칙(`ResourceManager.TrySpend` 사용), 스탯 표시 |
| **집결지 설정** | 집결지 지정 입력(클릭), 집결지 그룹, 월드 마커 표시. 실제 이동 반영은 PROTO |
| **HUD 전반** | 미니맵, 자원/식량 표시, 웨이브 타이머, 전투 로그, 스테이터스 패널 |
| **기존 최소 UI 계승·정리** | `UnitSelector` / `EnergyLabel` / `UpgradeButtonUI` (이미 있음 — §9) |
| **`UI_HUD.unity` 씬 단독 소유** | §3 |

### 맡지 않는 것 (건드리면 머지가 깨진다)
| 영역 | 이유 |
|---|---|
| **`Assets/Scenes/Proto_01.unity`** | **PROTO 단독 소유. 절대 금지** — 38MB 파일이라 충돌 시 복구 불가 (§3) |
| **AI 로직** (`UnitCombat`, `CharacterBehavior`, `FlowFieldService`, `GridPathfinder`) | PROTO가 병렬 작업 중 |
| **건설(포탑) 시스템** | PROTO |
| **웨이브 구성표 / 밸런스 수치** (`BalanceConfig.asset`, `Monster_*.asset`) | PROTO |
| **맵 생성 / 타일 / 안개** | PROTO |
| **계약 파일 `Scripts/Orders/**`** | 동결 — 읽기 + 구현 등록만 (§4) |

---

## §2. 파일 소유권 — 이것만 지키면 머지 충돌은 사실상 0이다

원칙: **한 파일은 한 브랜치만 수정한다.** 상대 소유 파일은 **읽기 전용**이다.

### UI 가 자유롭게 수정·생성할 수 있는 경로
```
Assets/Scenes/UI_HUD.unity                    ← 단독 소유 (신규, §3)
Assets/_Project/Scripts/UI/**
Assets/_Project/Prefabs/UI/**                 ← 신규
Assets/_Project/Art/UI/**                     ← 신규
Assets/_Project/Art/Fonts/**
Assets/_Project/Data/UI/**                    ← 신규 (UI 전용 SO만)
ProjectSettings/EditorBuildSettings.asset     ← UI_HUD 등록용. UI만 건드린다
Docs/작업로그_UI.md                            ← 신규, 이 브랜치의 작업 기록
Docs/인계/씬반영요청_UI→PROTO.md                ← §7
Docs/인계/API요청_UI→PROTO.md                  ← §7
Docs/UI 브렌치 준수사항.md                       ← 이 문서 (§0 규약 준수)
```

### 절대 손대지 말 것 (PROTO 브랜치 소유)
```
Assets/Scenes/Proto_01.unity                  ← 최우선 금지 (§3)
Assets/_Project/Scripts/Combat/**
Assets/_Project/Scripts/Units/**
Assets/_Project/Scripts/Map/**
Assets/_Project/Scripts/Fog/**
Assets/_Project/Scripts/Wave/**
Assets/_Project/Scripts/Build/**
Assets/_Project/Scripts/Resource/**
Assets/_Project/Data/Combat/**, Data/Map/**, Data/Units/**
Assets/_Project/Art/Tiles/**, Art/OrganicTilemap/**, Art/Units/**
Assets/_Project/Prefabs/Build/**
Tools/**
Docs/프로토 브렌치 준수사항.md
Docs/작업로그_PROTO.md
```

### 동결 — 양쪽 모두 수정 금지 (§4)
```
Assets/_Project/Scripts/Orders/**             ← 두 브랜치 사이의 계약
.gitattributes
Docs/머지 계획.md                              ← 유저/머지 담당자만
Docs/진행상황_스냅샷_20260805.md                 ← PROTO 쪽 PC를 위한 읽기 전용 사본
```

> ⚠️ **`Scripts/Resource/ResourceManager.cs` 는 PROTO 소유다.** UI 는 `ResourceManager.Instance.TrySpend/CanAfford/OnEnergyChanged` 를 **호출만** 한다. 캐릭터 생성 비용 같은 새 규칙은 `ResourceManager` 에 넣지 말고 **`Scripts/UI/CharacterCreationService.cs`(신규, UI 소유)** 에 두고 `TrySpend` 를 쓴다 — `CharacterUpgradeService` 가 이미 그 패턴이므로 프로젝트 일관성도 유지된다.

---

## §3. 씬 규칙 — 이 프로젝트에서 가장 위험한 지점

### ⚠️ `Assets/Scenes/Proto_01.unity` 는 **38 MB 텍스트 YAML 파일**이다
320×320 타일맵 데이터가 씬에 직렬화되어 있다. 이 파일이 충돌하면 **손으로 해결할 수 없고, 한쪽 작업을 통째로 버리는 것 말고 방법이 없다.** 그래서 씬은 **PROTO 가 단독 소유**하고, UI는 **별도 씬에서 additive 로** 작업한다.

| 규칙 | 내용 |
|---|---|
| **U-S1** | **`Proto_01.unity` 를 열어 저장하지 말 것.** 실행해서 확인하는 것(Play)은 괜찮지만, **어떤 오브젝트도 추가·삭제·이동하지 말고 Ctrl+S 를 누르지 말 것.** 실수로 저장했으면 즉시 `git checkout -- Assets/Scenes/Proto_01.unity` 로 되돌린다. |
| **U-S2** | UI는 **신규 씬 `Assets/Scenes/UI_HUD.unity`** 에 만든다. 이 씬에는 `Canvas`(Screen Space-Overlay, CanvasScaler 1920×1080 Match 0.5 — 기존 `UI_Root` 설정과 동일하게), `EventSystem` **없이**(Proto_01 에 이미 있다), UI 루트 오브젝트들, 그리고 `TacticalOrdersService`(§4) 를 둔다. |
| **U-S3** | **씬 로딩은 코드로 한다 — Proto_01 을 건드리지 않기 위해.** UI 스크립트 하나에 아래 패턴을 둔다:<br>`[RuntimeInitializeOnLoadMethod]` → 활성 씬 이름이 `Proto_01` 이면 `SceneManager.LoadSceneAsync("UI_HUD", LoadSceneMode.Additive)`. 이미 로드돼 있으면 스킵. (`WaveManager` 가 이미 `RuntimeInitializeOnLoadMethod` 를 쓰고 있어 프로젝트 관례에 맞는다.) |
| **U-S4** | `UI_HUD.unity` 를 **`EditorBuildSettings.asset` 에 등록**해야 플레이 모드에서 로드된다. 이 파일은 UI 소유이므로 등록해도 된다. |
| **U-S5** | **씬을 넘는 참조(cross-scene reference)는 직렬화되지 않는다.** `Proto_01` 의 `WaveManager`/`UnitSpawner`/`MapGenerator` 를 인스펙터로 끌어다 놓을 수 없다. 반드시 `Start()` 에서 `FindAnyObjectByType<T>()` 또는 기존 싱글턴(`ResourceManager.Instance`, `UnitSelector.Instance`, `CharacterUpgradeService.Instance`)으로 찾는다. **찾지 못했을 때(null)도 예외 없이 동작해야 한다** — Proto_01 없이 UI_HUD 만 열어보는 경우가 있다. |
| **U-S6** | `Proto_01` 에 이미 있는 구식 UI 오브젝트(`GameSystems` / `Energy` / `Upgrade` / `UI_Root`)는 **지우지 말 것.** 새 HUD와 겹쳐 보이면 코드로 숨긴다(`SetActive(false)`). 실제 삭제는 머지 후 씬 소유자가 §7 의 요청서를 보고 처리한다. |
| **U-S7** | **UI를 프리팹으로 만들 것.** 패널마다 `Assets/_Project/Prefabs/UI/<패널명>.prefab` 로 두고 `UI_HUD.unity` 에는 인스턴스만 놓는다. 씬 diff가 작아지고, 머지 후 `Proto_01` 로 흡수하기도 쉽다. |
| **U-S8** | 새 씬을 더 만들지 말 것 (`UI_HUD.unity` 하나만). |

---

## §4. 계약 파일 (동결) — `Assets/_Project/Scripts/Orders/`

전술 UI와 집결지는 **"플레이어의 명령"** 이고, 그 명령에 실제로 반응하는 건 **AI 로직(=PROTO의 일)** 이다. 양쪽이 같은 자료구조를 각자 정의하면 머지에서 반드시 깨진다. 그래서 명령의 **자료형과 접근 경로만** 미리 확정해 `main` 의 시드 커밋에 넣고 **양쪽 모두 수정 금지**로 동결했다.

```
Assets/_Project/Scripts/Orders/
├── TacticalTypes.cs      enum CombatStance / BattlePosition / Doctrine,
│                         struct CharacterOrders (+ Default)
└── TacticalOrders.cs     static 접근점:
                            TacticalOrders.Source                       (UI가 등록)
                            TacticalOrders.For(CharacterUnit)           → CharacterOrders
                            TacticalOrders.TryGetRallyPoint(unit, out Vector3)
                          interface ITacticalOrderSource
```

### UI 가 이 계약을 다루는 방법
| 규칙 | 내용 |
|---|---|
| **U-C1** | **`Scripts/Orders/` 안의 파일을 수정·추가·삭제하지 말 것.** enum 값 하나 추가도 금지 — PROTO 쪽 `switch` 와 UI 쪽 드롭다운이 동시에 어긋난다. |
| **U-C2** | UI는 `ITacticalOrderSource` **구현체**를 `Scripts/UI/TacticalOrdersService.cs`(UI 소유)에 만들고, `Awake()` 에서 `TacticalOrders.Source = this`, `OnDestroy()` 에서 해제한다. 패널은 이 서비스만 읽고 쓴다. |
| **U-C3** | **UI만으로는 명령의 효과를 볼 수 없다.** AI가 반응하는 건 머지 후다. 그래서 UI 쪽 검증은 "서비스에 값이 제대로 들어갔는지 로그로 확인" 까지만 한다 — 효과가 안 보인다고 `CharacterBehavior` 를 고치러 가지 말 것. |
| **U-C4** | 계약을 바꿔야 하면(값이 부족하다 등) **직접 고치지 말고** `Docs/인계/API요청_UI→PROTO.md` 에 적고 유저에게 보고한다. 유저가 양쪽에 동시 반영한다. |
| **U-C5** | 각 명령값의 **의미 해석은 PROTO 가 정한다.** UI 툴팁 문구는 머지 시 `작업로그_PROTO.md` 의 해석에 맞춰 조정한다 — 지금은 잠정 문구로 두고 그 사실을 주석에 남길 것. |

---

## §5. 코딩 · 직렬화 제약 (이걸 어기면 PROTO 쪽 씬이 깨진다)

| 규칙 | 내용 |
|---|---|
| **U-D1** | **기존 UI 스크립트의 파일명·클래스명·네임스페이스를 바꾸지 말 것.** `UnitSelector` / `EnergyLabel` / `UpgradeButtonUI` 는 **`Proto_01.unity` 의 오브젝트가 GUID로 참조하고 있다.** 이름을 바꾸거나 파일을 옮기면 PROTO 쪽 씬에서 `Missing Script` 가 되고, 38MB 씬을 손으로 고쳐야 한다. |
| **U-D2** | 같은 이유로 **기존 UI 스크립트의 직렬화 필드를 삭제·개명하지 말 것.** 필드 추가는 안전하다(기본값으로 채워짐). 리팩터링이 하고 싶으면 **새 파일**을 만들고 기존 파일은 그대로 둔다. |
| **U-D3** | **`.meta` 파일을 삭제하지 말 것.** GUID 재발급 = 위와 같은 사고. |
| **U-D4** | PROTO 소유 클래스의 **공개 API 는 호출만** 한다. 없는 API가 필요하면 §7 요청서. 현재 쓸 수 있는 것들: <br>`WaveManager`: `Phase` `WaveNumber` `PhaseRemaining` `PhaseDuration` `OnPhaseChanged` `OnWaveSpawned` `OnWaveEnded` `OnDefeat` `SkipPhase()` <br>`ResourceManager`: `Instance` `Energy` `OnEnergyChanged` `CanAfford` `TrySpend` `AddEnergy` <br>`CharacterUnit`: `Stats` `UpgradeCount` `MaxHp` `Balance` `DebugSummary()` <br>`DamageableUnit`: `IsAlive` `CurrentHp` `MaxHp` `Faction` `Kind` `OnAnyDied` `OnAnyAttack` <br>`UnitCombat`: `State` `Target` `Home` `IsHunting` <br>`CharacterBehavior`: `Duty` `Destination` <br>`UnitSpawner`: `SpawnedNexus` `SpawnedCharacters` `SpawnOneCharacter()` `OnCharacterSpawned` (뒤 둘은 시드 커밋에서 추가) <br>`MapGenerator`: `Walkable` `MapSize` `Origin` `LocalToCell` `WorldToCell` `CellCenterWorld` `CenterCell` `IsCellBlocked` `IsCellPlaceable` `SpawnGates` ← **미니맵은 이것만으로 만들 수 있다** <br>`FogOfWarService`: `IsReady` `IsExplored` `IsVisible` `IsVisibleWorld` `ExploredPercent` ← **미니맵 안개** <br>`CharacterUpgradeService`: `Instance` `CostFor` `CanUpgrade` `TryUpgrade` `OnUpgraded` <br>`UnitRegistry`: 살아있는 유닛 목록 (전투 로그·미니맵 유닛 점) |
| **U-D5** | 네임스페이스는 `LastSanctuary.UI` 를 쓴다. 기존 UI 스크립트와 같은 규칙. |
| **U-D6** | 텍스트는 **TMP + `NeoDunggeunmo SDF`**(한글 픽셀 폰트, 이미 구움)를 쓴다. 새 폰트를 굽지 말 것 — `Scripts/Editor/NeoDunggeunmoFontBaker.cs` 참조. |
| **U-D7** | 입력은 **Input System** 을 쓴다 (`Mouse.current`, `InputSystemUIInputModule`). 구 Input Manager 혼용 금지 — `UnitSelector` 가 이미 Input System 방식이다. |
| **U-D8** | **좌클릭은 카메라 드래그(`CameraRigController`)·유닛 선택(`UnitSelector`)과 공유된다.** 집결지 지정처럼 새 클릭 동작을 넣을 때는 (a) 드래그 임계값 4px 규칙, (b) `EventSystem.current.IsPointerOverGameObject()` 로 UI 위 클릭 제외, 이 둘을 반드시 따를 것. `UnitSelector.HandleClick()` 이 참고 구현이다. 임계값을 다르게 두면 "클릭했는데 아무 일도 안 일어난다"가 재현된다. |
| **U-D9** | 주석은 기존 코드 스타일(한글 `///` 요약 + "왜 이렇게 했는지")을 유지할 것. |
| **U-D10** | UI가 매 프레임 `FindObjectsByType` 를 도는 구현은 피할 것 — 유닛이 수십~수백 마리다. `UnitRegistry` 나 이벤트 구독을 쓰고, 갱신은 `UpgradeButtonUI` 처럼 **값이 바뀔 때만** 한다. |

---

## §6. git 규율 — 22절 사고를 반복하지 않기 위한 것

> 2026-08-05, 커밋하지 않은 작업(스크립트 6개 + SO 3개 + 씬 변경)이 **브랜치 전환/discard 로 통째로 사라졌다** (진행상황 22절 마지막).

| 규칙 | 내용 |
|---|---|
| **U-G1** | **브랜치는 `UI` 하나만 쓴다.** `main` / `PROTO` 는 체크아웃하지 말 것. 원격 내용을 보려면 `git log origin/main` 처럼 참조만 읽는다. |
| **U-G2** | **작업 단위마다 즉시 커밋.** 특히 `UI_HUD.unity` 를 저장했으면 그 자리에서 커밋. |
| **U-G3** | **하루 1회 이상 `git push origin UI`.** |
| **U-G4** | 커밋 메시지: `[UI][ADD\|MOD\|FIX] 한 줄 요약`. |
| **U-G5** | `git reset --hard` / `git checkout -- .` / IDE의 "모든 변경사항 취소" **금지.** 단 하나의 예외: 실수로 `Proto_01.unity` 를 저장했을 때 그 **한 파일만** `git checkout -- Assets/Scenes/Proto_01.unity`. |
| **U-G6** | **`main` 에 직접 커밋·푸시 금지.** `main` 은 머지 시점에만 움직인다. |
| **U-G7** | `git rebase` / `push --force` 금지. |
| **U-G8** | 작업 종료 시 `git status` clean 상태로 남길 것. 특히 `Proto_01.unity` 가 modified 로 떠 있으면 안 된다 — **커밋 전에 항상 `git status` 를 눈으로 확인.** |
| **U-G9** | Unity 에디터가 켜져 있으면 씬/에셋을 자동 저장할 수 있다. 커밋 직전 `git status` 로 의도하지 않은 파일이 딸려오는지 확인할 것 (`Library/`·`Temp/`·`UserSettings/` 는 gitignore 되어 있어 안전). |

---

## §7. PROTO 브랜치에 요청하는 방법

상대 소유 파일이 필요하면 **직접 고치지 않고** 요청서에 적는다. 요청서를 커밋·푸시한 뒤 **유저에게 구두로도 알릴 것.**

### `Docs/인계/씬반영요청_UI→PROTO.md` — `Proto_01.unity` 에 넣어야 하는 것
```markdown
## 요청 N — <한 줄 제목>
- 날짜:
- 대상 씬 오브젝트:
- 요청 내용: (예: 루트 오브젝트 `Energy` / `Upgrade` 삭제 — 새 HUD가 대체함)
- 근거: (어느 패널이 대체하는지)
- 머지 전에 해야 하는가 / 머지 후에 해도 되는가:
- 상태: 요청 / 유저 확인 / 반영됨(커밋 해시)
```

### `Docs/인계/API요청_UI→PROTO.md` — PROTO 소유 클래스에 필요한 공개 API
```markdown
## 요청 N — <한 줄 제목>
- 날짜:
- 필요한 것: (예: `MapGenerator.SpawnGates` 를 미니맵에 표시하려면 월드 좌표 변환이 필요 → `LocalToWorld(Vector2Int)` 공개)
- 왜 필요한가:
- 임시 대응: (반영되기 전까지 UI가 어떻게 버티는지 — 대개 "해당 표시를 잠시 끈다")
- 상태: 요청 / 유저 확인 / 반영됨(커밋 해시)
```

---

## §8. 머지 전 제출물 체크리스트

`머지 계획.md` §4 의 UI 항목과 같다.

- [ ] Unity 콘솔 에러 0, 컴파일 에러 0
- [ ] `git status` clean, `git push origin UI` 완료
- [ ] **`git diff --name-only origin/main...UI` 에 `Assets/Scenes/Proto_01.unity` 가 없다** ← 가장 중요
- [ ] 같은 명령 결과에 PROTO 소유 경로(`Scripts/Combat|Units|Map|Fog|Wave|Build|Resource/`, `Data/Combat|Map|Units/`, `Art/Tiles|OrganicTilemap|Units/`, `Tools/`, `Scripts/Orders/`)가 **하나도 없다**
- [ ] `UI_HUD.unity` 가 `EditorBuildSettings.asset` 에 등록되어 있고, Proto_01 을 플레이하면 additive 로 실제 로드된다
- [ ] PROTO 관련 오브젝트가 없을 때(=UI_HUD 단독 실행) **NullReferenceException 이 나지 않는다**
- [ ] `Docs/작업로그_UI.md` 작성 — 아래 양식 (머지 후 `진행상황.md` 30절~ 로 편입)
- [ ] `Docs/인계/씬반영요청_UI→PROTO.md` 정리 완료

### `Docs/작업로그_UI.md` 양식 (진행상황.md 절 형식을 그대로 따를 것)
```markdown
## UI-N. <제목> (YYYY-MM-DD)

### 무엇을 / 왜
### 어떻게 (만든 파일 목록 + 왜 그 구조인지)
### 겪은 함정
### 확인된 것
### 아직 확인 못 한 것 (특히 "AI 반응은 머지 후에만 확인 가능" 항목)
### 계약(`Scripts/Orders/`)에 무엇을 어떻게 넣었는지  ← §4 U-C5
### 씬반영요청 목록
```
**절 번호는 `UI-1` 부터.** 머지 시 `진행상황.md` 의 **30절~** 을 이 브랜치가 쓰기로 예약해두었다 (PROTO는 24~29절). 번호를 섞지 말 것.

---

## §9. ⚠️ 진행상황.md 요약 표가 낡았다 — 이미 만들어져 있는 UI

`진행상황.md` 15절의 **문서 유실 사고(15~19절 결번)** 때문에 상단 요약 표가 실제 코드와 맞지 않는다. **"UI 전반 ❌ 미착수" 는 사실이 아니다.** 아래는 이미 있으므로 **새로 만들지 말고 계승·확장**할 것.

| 이미 있는 것 | 파일 | 씬 오브젝트 |
|---|---|---|
| 유닛 선택 (클릭 → 캐릭터 선택, 스프라이트 틴트, 드래그 구분 4px) | `Scripts/UI/UnitSelector.cs` | `GameSystems` |
| 에너지 표시 라벨 | `Scripts/UI/EnergyLabel.cs` | `Energy` (TMP) |
| 강화 버튼 (비용 표시·활성 조건) | `Scripts/UI/UpgradeButtonUI.cs` | `Upgrade` |
| 자원(에너지) 시스템 | `Scripts/Resource/ResourceManager.cs` (**PROTO 소유** — 호출만) | `ResourceManager` |
| 캐릭터 강화 규칙 (캐릭터별 누진 비용, 4스탯 랜덤 성장) | `Scripts/Units/CharacterUpgradeService.cs` (**PROTO 소유**) | — |
| 한글 픽셀 폰트 SDF | `Art/Fonts/NeoDunggeunmo SDF.asset` | — |

**즉 15~19절의 잃어버린 내용은 "자원 시스템 + 캐릭터 강화 + 유닛 선택 + 최소 UI + 유기적 타일셋 + 경로탐색 + 한글 폰트"였던 것으로 추정된다.** 코드는 남아있고 문서만 없다 → **작업 전에 위 파일들을 직접 읽을 것. 문서가 아니라 코드가 사실이다.**

### 아직 없는 것 (= 이 브랜치가 만들 것)
스탯 표시 패널, 미니맵, 웨이브/타이머 표시, 전투 로그, **전술 UI(전투 성향·포지션·행동 지침)**, **캐릭터 생성 UI**, **집결지 설정·집결지 그룹**, 식량/허기 표시(기획 미확정 — 진행상황 10절 5번), HP바.

### UI 목업에서 확인된 패널 목록 (`Last Sanctuary HUD.html`)
미니맵 · 스테이터스 · 자원 · 식량 · 전투 로그 · 전투 성향 · 전투 포지션 · 행동 지침 · 지정 캐릭터 · 지정 캐릭터 명령 · 집결지 그룹 · 캐릭터 관리 · "선택된 캐릭터에게만 적용" · 1기/2기

---

## §10. UI 작업 시 알아둘 프로젝트 제약

| # | 내용 |
|---|---|
| 1 | **카메라는 잡아서 이동(grab-pan) 방식**이다 — 원래 가장자리 자동 패닝이었는데 "UI가 화면 하단/좌상단/우측을 차지해서" 유저 요청으로 바뀌었다 (진행상황 7절). **HUD 레이아웃을 그 전제(하단·좌상단·우측이 UI)로 잡을 것.** |
| 2 | **캔버스 기준 해상도는 1920×1080, CanvasScaler Match 0.5** (기존 `UI_Root` 설정). `UI_HUD.unity` 도 같게 맞춰야 머지 후 레이아웃이 안 튄다. |
| 3 | **유닛에 Collider2D 가 없다.** 마우스 피킹을 물리 레이캐스트로 하면 안 된다 — `UnitSelector` 처럼 스프라이트 `bounds` 직접 검사 + 근접 보정 반경을 쓴다. 집결지 지정도 같은 방식으로 월드 좌표를 계산할 것. |
| 4 | **카메라 줌은 `PixelPerfectCamera` + 정수배 다운샘플**이다. 월드 공간 UI(집결지 마커, HP바)를 만들면 줌 배율에 따라 크기가 튈 수 있다 — 스크린 공간에 그리거나 줌에 맞춰 스케일을 보정할 것. |
| 5 | **안개(FogOfWar)는 `Overhead` 정렬 레이어 order 100** 에 그려진다. 월드 공간 UI는 그보다 위(더 큰 order)에 둘 것. 아니면 안개에 덮인다. |
| 6 | **유닛은 정렬 순서상 항상 벽보다 위에 그려진다** (진행상황 21절 마지막). 월드 UI도 같은 성질을 갖는다. |
| 7 | **`Run In Background` 가 꺼져 있으면 타이머가 멈춘다** — 에디터가 포커스를 잃는 순간 게임 루프가 정지한다. 타이머 UI를 검증하는데 시간이 안 흐르면 이걸 먼저 확인 (진행상황 11절). |
| 8 | 유저 확정 작업 방식: **MCP로 플레이 모드 진입/타이머 대기 같은 런타임 검증은 하지 않는다.** 오브젝트 생성/연결/씬 저장까지 하고, 돌려서 보는 건 유저가 한다. |
| 9 | MCP 함정은 `프로토 브렌치 준수사항.md` §10 과 동일하다 — 특히 **비활성 오브젝트는 `instanceId` 로만 접근**, **기존 필드 값 변경은 YAML 패치 대신 `update_component`**, **`.asset` YAML 에 빈 줄 금지**. |

---

## §11. 작업 시작 절차 (매 세션)

1. `C:\Project\라스트 생추어리\진행상황.md` **전체** 읽기 (0절 문서 관리 원칙 포함)
2. 이 문서(`UI 브렌치 준수사항.md`) 읽기
3. `Docs/작업로그_UI.md` 읽기 — 이전 세션이 어디까지 했는지
4. `git branch --show-current` → `UI` 확인, `git status` → clean 확인 (특히 `Proto_01.unity` 가 없어야 함)
5. `git pull origin UI`
6. 작업 → 단위마다 커밋 → 종료 전 `작업로그_UI.md` 갱신 + push
7. 요청서(`Docs/인계/`)에 쌓인 게 있으면 유저에게 보고

---

## §12. 갱신 이력

| 날짜 | 브랜치 | 무엇을 | 왜 |
|---|---|---|---|
| 2026-08-05 | UI | 최초 작성 | UI/PROTO 병렬 작업 시작에 앞서 소유권·씬·계약 제약을 확정 |
