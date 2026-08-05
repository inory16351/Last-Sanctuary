# UI 브랜치 준수사항 — HUD · 전투 AI · 캐릭터 생성 · 집결지 담당

> **대상**: 이 PC에서 `UI` 브랜치로 작업하는 Claude Code 및 그 서브에이전트
> **작성**: 2026-08-05 · **개정 v2**: 2026-08-05 (전투 AI 를 이 브랜치로 이관, 씬 소유권 이전 — §13 갱신 이력)
> **짝 문서**: [`프로토 브렌치 준수사항.md`](프로토%20브렌치%20준수사항.md) · [`머지 계획.md`](머지%20계획.md)
> **필독 선행 문서**: `C:\Project\라스트 생추어리\진행상황.md` (게임 전체 구조·함정 기록. **작업 시작 전 전체를 읽을 것**)
> **UI 목업**: `C:\Project\라스트 생추어리\UI\Last Sanctuary HUD.html`
> **기획서**: `C:\Project\라스트 생추어리\기획서\`

---

## 🔒 §0. 문서 수정 규약 — 이 문서를 여는 모든 사람·에이전트가 먼저 읽을 것

이 문서는 참고 메모가 아니라 **두 브랜치가 서로를 깨뜨리지 않기 위해 합의한 구속력 있는 규약**이다.
**임의 수정·삭제·완화 금지.** 갱신이 필요하면 아래 프롬프트를 그대로 읽고 따를 것.

> ### 갱신 프롬프트 (이 문서를 고치려는 에이전트는 그대로 따를 것)
>
> 너는 지금 `UI 브렌치 준수사항.md` 를 갱신하려 한다. 이 문서는 `main` 으로의 최종 머지를 **충돌 0으로** 끝내기 위한 제약 조건 명세다. 아래를 전부 지켜야 갱신한 것으로 인정된다.
>
> 1. **기존 제약을 지우거나 약화시키지 말 것.** 제약이 현실과 맞지 않으면 삭제가 아니라 "§N 은 이런 이유로 이렇게 바뀐다"를 **새 항목으로 추가**하고 원래 항목에 링크한다. (`진행상황.md` 0절 3번과 같은 원칙)
> 2. **추가만 한다.** 본문의 § 번호와 순서를 재배치하지 말 것 — 짝 문서와 `머지 계획.md` 가 § 번호로 이 문서를 인용하고 있다.
> 3. **§2(파일 소유권) · §3(씬 소유권)은 단독으로 바꿀 수 없다.** 변경은 **유저의 명시적 승인 + 양쪽 브랜치 문서 동시 갱신**이 있어야만 유효하다. 한쪽만 바꾸면 머지가 깨진다.
> 4. 갱신할 때마다 맨 아래 **§13 갱신 이력**에 (날짜 / 브랜치 / 무엇을 / 왜) 한 줄을 남긴다. 이력 없는 변경은 무효로 보고 되돌린다.
> 5. **작업 방향을 유지할 것.** 이 문서의 목적은 "빠르게 만들기"가 아니라 **"머지 시점에 충돌 없이 합쳐지기"** 이고, 그 수단은 **① 파일 단위 단독 소유 ② 씬 변경 최소화 ③ 상대 소유 파일 무접촉**이다. 편의를 위해 이 셋을 우회하는 판단은 어떤 경우에도 하지 말고, 필요하면 §7 의 요청서 경로를 쓴다.
> 6. **이 §0 규약 블록 자체는 수정 금지.**

---

## §1. 이 브랜치의 범위 (v2 — 전투 AI 포함)

> **v2 개정 이유**: 전술 명령·집결지 같은 UI 기능은 결국 **AI 거동을 바꾸는 것**이라, UI와 AI가 다른 PC에 갈라져 있으면 어느 쪽도 결과를 확인할 수 없다. 유저 결정으로 **전투 AI를 이 브랜치로 가져와 UI와 붙여서 개발**한다. 그 결과 v1 에 있던 계약 파일(`Scripts/Orders/`)과 별도 UI 씬(`UI_HUD.unity`)은 **불필요해져 폐기**했다 (§4).

### 맡는 것 (UI 브랜치)
| 영역 | 내용 |
|---|---|
| **HUD 전반** | §9 의 이번 스코프 6개 패널 |
| **전투 AI** | `UnitCombat`(FSM·타겟팅·이동·우회), `CharacterBehavior`(정찰·방어·사냥), `UnitRegistry`, `DamageableUnit` |
| **캐릭터** | `CharacterUnit`, `UnitSpawner`, `CharacterUpgradeService`, `Nexus` |
| **자원** | `ResourceManager` (에너지 획득·소비) |
| **집결지** | 지정 입력 → 마커 표시 → `CharacterBehavior` 반영까지 **한 브랜치 안에서 끝낸다** |
| **전투 밸런스 공식·수치** | `BalanceConfigSO.cs` + `BalanceConfig.asset` |
| **씬 `Proto_01.unity` 단독 소유** | §3 — **단, 변경은 최대한 피한다** |

### 맡지 않는 것 (건드리면 머지가 깨진다)
| 영역 | 이유 |
|---|---|
| **건설(포탑)** `Scripts/Build/**`, `Prefabs/Build/**` | PROTO가 병렬 작업 중 |
| **웨이브 규칙·구성표** `Scripts/Wave/**`, `WaveDefinitionSO` | PROTO |
| **몬스터 정의·스폰** `MonsterUnit`, `MonsterDefinitionSO`, `MonsterSpawner`, `NeutralMonster*` | PROTO |
| **맵 생성 / 타일 / 안개** `Scripts/Map/**`, `Scripts/Fog/**` | PROTO |
| **몬스터·타일 아트, `Tools/**`** | PROTO |

> ⚠️ **경계 주의**: 몬스터의 *전투 거동*은 `UnitCombat`(UI 소유)이 담당하고, 몬스터의 *정의·스폰·구성*은 PROTO 소유다. 몬스터가 이상하게 싸우면 UI 쪽 문제, 이상한 조합으로 나오면 PROTO 쪽 문제다.

---

## §2. 파일 소유권 (v2)

원칙: **한 파일은 한 브랜치만 수정한다.** 상대 소유 파일은 **읽기 전용**이다.

### UI 가 자유롭게 수정·생성할 수 있는 경로
```
Assets/Scenes/Proto_01.unity                  ← 단독 소유. 단 §3 의 변경 최소화 규칙 준수
Assets/_Project/Scripts/UI/**
Assets/_Project/Scripts/Combat/**             (UnitCombat, UnitRegistry, DamageableUnit,
                                               StatBlock, Faction, BalanceConfigSO)
Assets/_Project/Scripts/Resource/**
Assets/_Project/Scripts/Units/CharacterUnit.cs
Assets/_Project/Scripts/Units/CharacterBehavior.cs
Assets/_Project/Scripts/Units/CharacterUpgradeService.cs
Assets/_Project/Scripts/Units/UnitSpawner.cs
Assets/_Project/Scripts/Units/Nexus.cs · NexusDefinitionSO.cs
Assets/_Project/Data/Combat/**                (BalanceConfig.asset, NexusDefinition.asset)
Assets/_Project/Data/UI/**                    ← 신규
Assets/_Project/Prefabs/UI/**                 ← 신규
Assets/_Project/Art/UI/**                     ← 신규
Assets/_Project/Resources/**                  ← 신규 (런타임 로드용, §10)
Docs/작업로그_UI.md
Docs/인계/씬반영요청_UI→PROTO.md · API요청_UI→PROTO.md   (구식 — v2에선 거의 쓸 일 없음)
Docs/UI 브렌치 준수사항.md                       ← 이 문서 (§0 규약 준수)
```

### 절대 손대지 말 것 (PROTO 소유)
```
Assets/_Project/Scripts/Build/**
Assets/_Project/Scripts/Wave/**
Assets/_Project/Scripts/Map/**
Assets/_Project/Scripts/Fog/**
Assets/_Project/Scripts/Units/MonsterUnit.cs · MonsterDefinitionSO.cs · MonsterSpawner.cs
Assets/_Project/Scripts/Units/NeutralMonsterUnit.cs · NeutralMonsterDefinitionSO.cs
                                 · NeutralMonsterSpawner.cs
Assets/_Project/Data/Units/**  ·  Data/Map/**
Assets/_Project/Art/Tiles/** · Art/OrganicTilemap/** · Art/Units/**
Assets/_Project/Prefabs/Build/**
Tools/**
Docs/프로토 브렌치 준수사항.md · Docs/작업로그_PROTO.md
```

### 동결 — 양쪽 모두 수정 금지
```
.gitattributes
Docs/머지 계획.md                              ← 유저/머지 담당자만
Docs/진행상황_스냅샷_20260805.md                 ← PROTO 쪽 PC를 위한 읽기 전용 사본
```

### `ProjectSettings/`
원칙 금지. 필요하면 유저에게 먼저 알리고 양쪽에 동시 반영한다.
`Run In Background` 는 **1로 켜져 있어야** 타이머가 흐른다 (진행상황 11절).

---

## §3. 씬 규칙 — 이 프로젝트에서 가장 위험한 지점

### ⚠️ `Assets/Scenes/Proto_01.unity` 는 **38 MB 텍스트 YAML 파일**이다
320×320 타일맵이 씬에 직렬화되어 있다. 충돌하면 손으로 병합할 방법이 없고 한쪽 작업을 통째로 버려야 한다.

**v2 에서 씬 소유권이 PROTO → UI 로 넘어왔다.** 이유: 전투 AI가 이 브랜치로 오면서 `Character_Template` 의 인스펙터 값을 반복해서 조정하게 됐고, PROTO의 남은 작업(건설·웨이브·맵)은 프리팹과 SO 에셋으로 처리할 수 있기 때문이다. **PROTO는 이제 씬을 전혀 열지 않는다** → 이 파일에 충돌이 날 수 없다.

| 규칙 | 내용 |
|---|---|
| **U-S1** | **씬을 소유하지만, 변경은 최대한 피한다.** 저장 한 번이 38MB 파일을 전면 재작성하고 커밋마다 새 blob 이 히스토리에 영구히 쌓인다. |
| **U-S2** | ~~HUD 는 코드로 런타임 생성한다~~ → **폐기(2026-08-05, 유저 확정).** 씬 오브젝트는 **MCP 로 하이라키에 직접 만든다** — §10 참조. 하이라키에 실물이 있어야 유저가 인스펙터에서 직접 조정할 수 있기 때문이다. 씬 저장 비용은 U-S3 로 관리한다. |
| **U-S3** | 씬 저장 **횟수를 최소화**한다. 계층을 다 만든 뒤 **한 번만** 저장하고, **커밋을 따로 분리**해 메시지에 `(씬)` 을 붙인다. 무엇을 왜 바꿨는지 `작업로그_UI.md` 에 남길 것. |
| **U-S4** | **맵 재생성 금지.** 재생성은 씬 전체 재작성이다. 필요하면 유저에게 먼저 확인하고, 커밋 메시지에 `[MAP]` 을 붙여 1회만. |
| **U-S5** | `Templates/` 하위 오브젝트는 전부 **비활성**이어야 한다. 활성으로 두면 게임 시작부터 템플릿이 살아 움직인다 (진행상황 12절의 실제 사고). |
| **U-S6** | **플레이 모드에서 씬 저장은 실패한다.** 항상 Edit mode 확인 후 저장 (진행상황 8절 5번). |
| **U-S7** | 새 씬을 만들지 말 것. `Proto_01.unity` 하나로 간다. (v1 의 `UI_HUD.unity` additive 계획은 §4 에서 폐기됨) |
| **U-S8** | 구식 UI 오브젝트 `UI_Root/Energy`(`EnergyLabel`), `UI_Root/Upgrade`(`UpgradeButtonUI`) 는 **지우지 말고 새 레이아웃 안으로 옮겨 재사용**한다(§10 H-3). 같은 기능을 새로 만들어 두 벌이 되게 하지 말 것. |

---

## §4. 폐기된 v1 설계 (기록용 — 다시 도입하지 말 것)

| 폐기 항목 | v1 에서 왜 있었나 | v2 에서 왜 없앴나 |
|---|---|---|
| `Assets/_Project/Scripts/Orders/` **계약 파일**(동결 enum + `TacticalOrders` static 접근점) | 전술 명령을 UI가 쓰고 AI(PROTO)가 읽어야 해서, 자료형을 미리 확정해 동결할 필요가 있었다 | **UI와 AI가 같은 브랜치가 되어 계약 자체가 불필요.** 집결지·명령은 `Scripts/UI/` 와 `CharacterBehavior` 사이에서 직접 연결한다. 시드 커밋 2/2 는 취소 |
| `Assets/Scenes/UI_HUD.unity` **additive 씬** | UI가 `Proto_01.unity` 를 못 건드리니 별도 씬이 필요했다 | **씬 소유권이 UI로 왔고, HUD는 아예 코드 생성이라 씬이 필요 없다.** `EditorBuildSettings` 도 안 건드린다 |
| `씬반영요청_UI→PROTO.md` | 씬 소유자가 PROTO였다 | 방향이 **역전**됐다 → `씬반영요청_PROTO→UI.md` 를 PROTO가 쓴다 |

⚠️ 이 표를 지우지 말 것 — "왜 없앴는지"를 모르면 다음 세션이 같은 구조를 다시 만든다.

---

## §5. 코딩 · 직렬화 제약

| 규칙 | 내용 |
|---|---|
| **U-D1** | **기존 스크립트의 파일명·클래스명·네임스페이스를 바꾸지 말 것.** 씬/프리팹이 `.meta` GUID로 참조한다. 개명·이동하면 38MB 씬에서 `Missing Script` 가 되고 손으로 고쳐야 한다. 리팩터링은 **새 파일**로. |
| **U-D2** | **`.meta` 파일을 삭제하지 말 것.** 파일을 옮겨야 하면 `.meta` 를 반드시 같이 옮긴다(GUID 유지 → 참조 살아있음). |
| **U-D3** | **직렬화 필드를 삭제·개명하지 말 것.** 템플릿 인스펙터에 값이 직렬화돼 있다. 추가는 안전(기본값으로 채워짐). |
| **U-D4** | **PROTO 가 쓰는 공개 API 를 제거·개명하지 말 것.** 추가는 자유. PROTO(건설·웨이브)가 의존하는 것: <br>`DamageableUnit`: `IsAlive` `CurrentHp` `MaxHp` `Faction` `Kind` `TakeDamageFrom` `OnAnyDied` `OnAnyAttack` `MarkCombatAction` <br>`UnitCombat`: `Configure()` `SetHome()` `Target` `State` <br>`UnitRegistry`: `All` `FindTarget` `FindFirst` `NearestOfKinds` <br>`ResourceManager`: `Instance` `Energy` `CanAfford` `TrySpend` `AddEnergy` `OnEnergyChanged` <br>`BalanceConfigSO`: `MaxHp()` `Attack()` `Damage()` `DivRound` `statMax` <br>`Faction` / `UnitKind` / `StatBlock` 의 기존 값 |
| **U-D5** | 네임스페이스는 기존 규칙 `LastSanctuary.<영역>` 을 따른다. HUD 는 `LastSanctuary.UI`. |
| **U-D6** | **폰트는 네오둥근모(`NeoDunggeunmo SDF`)로 고정한다.** §10 참조. 다른 폰트를 쓰거나 새로 굽지 말 것. |
| **U-D7** | 입력은 **Input System** 을 쓴다 (`Mouse.current`, `InputSystemUIInputModule`). 구 Input Manager 혼용 금지. |
| **U-D8** | **좌클릭은 카메라 드래그(`CameraRigController`)·유닛 선택(`UnitSelector`)·집결지 지정이 공유한다.** 새 클릭 동작은 반드시 (a) 드래그 임계값 4px, (b) `EventSystem.current.IsPointerOverGameObject()` 로 UI 위 클릭 제외 를 따를 것. `UnitSelector.HandleClick()` 이 참고 구현이다. |
| **U-D9** | 유닛에 **Collider2D 가 없다.** 마우스 피킹을 물리 레이캐스트로 하면 안 된다 — 스프라이트 `bounds` 직접 검사 + 근접 보정 반경(`UnitSelector` 방식). |
| **U-D10** | UI가 매 프레임 `FindObjectsByType` 을 도는 구현은 피할 것. `UnitRegistry` 나 이벤트 구독을 쓰고, 갱신은 **값이 바뀔 때만**. 미니맵처럼 무거운 것은 갱신 주기를 둔다. |
| **U-D11** | 주석은 기존 코드 스타일(한글 `///` 요약 + "왜 이렇게 했는지")을 유지할 것. 이 프로젝트는 주석이 사실상 설계 문서다. |

---

## §6. git 규율

> 2026-08-05, 커밋 안 한 작업이 **브랜치 전환/discard 로 통째로 사라진 사고**가 있었다 (진행상황 22절).

| 규칙 | 내용 |
|---|---|
| **U-G1** | **브랜치는 `UI` 하나만 쓴다.** `main` / `PROTO` 체크아웃 금지. 원격을 보려면 `git log origin/main` 처럼 참조만 읽는다. |
| **U-G2** | **작업 단위마다 즉시 커밋.** 씬을 저장했으면 그 자리에서 별도 커밋. |
| **U-G3** | **하루 1회 이상 `git push origin UI`.** ⚠️ 이 환경의 셸에서는 GitHub 인증이 대화형이라 **에이전트가 push 할 수 없다** — 커밋까지 하고 **유저에게 push 를 요청**할 것. |
| **U-G4** | 커밋 메시지: `[UI][ADD\|MOD\|FIX] 한 줄 요약`. 씬 포함 시 `(씬)` 표기. |
| **U-G5** | `git reset --hard` / `git checkout -- .` / IDE의 "모든 변경사항 취소" **금지.** |
| **U-G6** | **`main` 에 직접 커밋·푸시 금지.** `main` 은 머지 시점에만 움직인다. |
| **U-G7** | `git rebase` / `push --force` 금지. |
| **U-G8** | 작업 종료 시 `git status` clean. 커밋 직전 **`git status` 로 `Proto_01.unity` 가 의도치 않게 딸려오는지 반드시 눈으로 확인**(Unity가 자동 저장할 수 있다). |

---

## §7. PROTO 브랜치와 주고받기

- **PROTO → UI (씬 반영 요청)**: `Docs/인계/씬반영요청_PROTO→UI.md`. PROTO가 씬에 필요한 것(포탑 템플릿 배치, `WaveManager` 필드 연결 등)을 적으면 **UI 가 처리**한다. 처리 후 `상태` 칸을 갱신하고 커밋 해시를 남긴다.
- **PROTO → UI (API 요청)**: `Docs/인계/API요청_PROTO→UI.md`. UI 소유 클래스에 필요한 공개 API. **추가만** 하고 기존 시그니처는 바꾸지 않는다.
- **UI → PROTO**: 몬스터 정의·웨이브 구성·맵에 필요한 게 있으면 `Docs/인계/API요청_UI→PROTO.md`.

요청서를 커밋한 뒤 **유저에게 구두로도 알릴 것** — 쌓아두면 아무도 안 본다.

---

## §8. 머지 전 제출물 체크리스트

- [ ] 컴파일 에러 0, Unity 콘솔 에러 0 (`recompile_scripts`)
- [ ] `git status` clean · 유저가 `git push origin UI` 완료
- [ ] **소유권 위반 검사** — 아래 결과가 비어 있어야 한다:
```bash
git diff --name-only origin/main...UI | grep -E "Scripts/(Build|Wave|Map|Fog)/|Units/(Monster|NeutralMonster)|Data/(Units|Map)/|Art/(Tiles|OrganicTilemap|Units)/|Tools/|Docs/(프로토|작업로그_PROTO)"
```
- [ ] 씬 커밋 개수 확인 (적을수록 좋다): `git log --oneline origin/main...UI -- Assets/Scenes/Proto_01.unity`
- [ ] `Docs/작업로그_UI.md` 작성 (UI-1, UI-2 … / 머지 후 `진행상황.md` **30절~** 로 편입)
- [ ] `Docs/인계/씬반영요청_PROTO→UI.md` 의 요청을 전부 처리했거나 사유를 남겼다

---

## §9. 이번 스코프 — HUD 6개 패널 (유저 확정 2026-08-05)

**이 범위 밖의 UI 는 지금 만들지 않는다.** 목업(`Last Sanctuary HUD.html`)에는 전술 명령(전투 성향·포지션·행동 지침), 식량, 스테이터스 상세 패널 등이 더 있지만 **이번 스코프에서 제외**한다.

| # | 위치 | 패널 | 내용 |
|---|---|---|---|
| 1 | **좌측 상단** | 캐릭터 로스터 | 캐릭터 **개별**로 한 줄씩 — 이름 / HP 바 / 4능력치 / 현재 행동(정찰·방어·교전). 줄을 클릭하면 그 캐릭터가 **선택**된다(`UnitSelector` 연동). 줄마다 선택 가능한 기능 버튼 |
| 2 | **좌측, 로스터 아래** | 로그라인 | 전투·자원·생성 이벤트를 최근 것부터 몇 줄 표시 |
| 3 | **우측 최상단** | 에너지 | 현재 보유 에너지 (`ResourceManager`) |
| 4 | **우측, 에너지 아래** | 액션 버튼 3개 | **캐릭터 생성** / **집결지 설정** / **캐릭터 강화** |
| 5 | **중앙 상단** | 웨이브 상태 | 웨이브 번호 · 단계(대기/진군/전투/패배) · 남은 타이머 (`WaveManager`) |
| 6 | **우 하단** | 미니맵 | 맵 지형 + 안개 + 유닛 점. `MapGenerator.Walkable` / `FogOfWarService` / `UnitRegistry` 읽기 전용 |

### 동작 규칙
- **캐릭터 생성**: 에너지 비용을 `ResourceManager.TrySpend` 로 먼저 차감하고 성공했을 때만 `UnitSpawner.SpawnOneCharacter()` 를 호출한다. 비용 규칙은 `Scripts/UI/CharacterCreationService.cs` 에 둔다 (`CharacterUpgradeService` 와 같은 패턴 — 규칙과 입력을 분리).
- **집결지 설정**: 버튼을 누르면 **지정 모드**로 들어가고, 맵을 클릭하면 그 지점이 집결지가 된다. 선택된 캐릭터가 있으면 그 캐릭터만, 없으면 전체. 집결지가 있는 캐릭터는 정찰·순찰 대신 그 지점으로 간다. 다시 누르거나 우클릭하면 해제.
- **캐릭터 강화**: 기존 `CharacterUpgradeService.TryUpgrade` 를 그대로 쓴다. 선택된 캐릭터가 없거나 에너지가 부족하면 비활성.

---

## §10. 오브젝트 생성 방침 — **MCP 로 하이라키에 직접 생성** (유저 확정 2026-08-05)

> ⚠️ **이 절은 §3(씬 변경 최소화)보다 우선한다.** 처음에는 38MB 씬 재작성을 피하려고 HUD 를
> 런타임 코드 생성으로 만들려 했으나(H-1 구안), **유저가 "객체 생성은 스크립트로 하지 말고
> MCP 로 하이라키에 직접 생성"으로 확정**했다. 씬 저장 비용은 §3 U-S3(커밋 분리·저장 횟수 최소화)로
> 관리하고, 생성 방식 자체는 아래를 따른다.

| 방침 | 내용 |
|---|---|
| **H-1 MCP 직접 생성** | 씬에 존재해야 하는 GameObject 는 **MCP(`update_gameobject` / `update_component` / `batch_execute`)로 하이라키에 직접 만든다.** 스크립트에서 `new GameObject(...)` / `Instantiate` 로 UI·시스템 오브젝트를 만들어내지 말 것. 하이라키에 실물이 보여야 유저가 인스펙터에서 위치·값을 직접 조정할 수 있다. |
| **H-2 템플릿 복제만 예외** | 개수가 런타임에 정해지는 반복 요소(캐릭터 로스터의 행, 로그 한 줄 등)는 **모체가 되는 템플릿 오브젝트 딱 하나만 MCP 로 만들고**, 나머지는 스크립트가 그것을 `Instantiate` 로 복제한다. 이 프로젝트가 이미 유닛에 쓰는 패턴 그대로다(진행상황 5절). 템플릿은 **비활성**으로 두고 복제본만 활성화한다. |
| **H-3 기존 UI 재사용** | `UI_Root/Energy`(`EnergyLabel`) 와 `UI_Root/Upgrade`(`UpgradeButtonUI`) 는 **지우지 말고 새 레이아웃 안으로 옮겨 재사용**한다. 같은 기능을 새로 만들어 두 벌이 되게 하지 말 것. |
| **H-4 폰트 — 네오둥근모 고정** | `Assets/_Project/Resources/Fonts/NeoDunggeunmo SDF.asset`. MCP 로 만든 TMP 컴포넌트에 폰트가 안 붙는 경우가 있어(진행상황 8절 2번: `update_component` 로 에셋 참조가 조용히 누락될 수 있음) **`HudFontApplier` 가 시작 시 HUD 하위 TMP 를 훑어 `Resources.Load` 한 폰트를 적용하는 안전망**을 둔다. 오브젝트 생성이 아니라 **값 보정**이므로 H-1 위반이 아니다. 다른 폰트로 바꾸거나 새로 굽지 말 것. |
| **H-5 스타일** | 색·크기 상수는 `HudTheme` 에 모아 **스크립트가 런타임에 칠하는 부분**(선택 하이라이트, HP 바 색, 미니맵 픽셀)에서 쓴다. 씬에 고정된 값은 인스펙터가 정본이다. |
| **H-6 해상도** | 기존 `UI_Root` 의 CanvasScaler(1920×1080, Match 0.5)를 그대로 쓴다. 새 캔버스를 만들지 말 것. |
| **H-7 갱신 비용** | 매 프레임 전수 조회 금지(U-D10). 로스터·미니맵은 갱신 주기를 두고, 에너지·선택은 이벤트 구독으로. |
| **H-8 저장은 한 번에** | MCP 로 계층을 다 만든 **뒤에 `save_scene` 을 한 번만** 부른다. 중간중간 저장하면 38MB 파일이 그만큼 여러 번 재작성된다. 저장 전 `get_play_mode_status` 로 Edit mode 확인(U-S6). |

### MCP 로 씬 UI 를 만들 때의 실전 주의
- `update_gameobject` 는 **`objectPath` 가 없으면 새로 만든다** — 부모부터 순서대로 만들 것.
- `update_component` 는 **컴포넌트가 없으면 추가**한다. `RectTransform` 의 앵커·피벗·오프셋은 필드명이 `m_AnchorMin` 계열이라 **넣은 뒤 `get_gameobject` 로 반드시 재확인**할 것 (진행상황 8절 3번: 조용히 무시되는 필드가 있다).
- 비활성 오브젝트(템플릿)는 **경로로 조회되지 않는다** — `get_scenes_hierarchy` 로 받은 `instanceId` 를 **같은 턴에** 쓸 것 (진행상황 12절).
- 스프라이트 참조는 MCP 로 넣을 수 없다(8절 1번). 배경은 스프라이트 없는 `Image`(색만)로 처리한다.

---

## §11. UI 작업 시 알아둘 프로젝트 제약

| # | 내용 |
|---|---|
| 1 | **카메라는 잡아서 이동(grab-pan) 방식**이다 — 가장자리 자동 패닝에서 유저 요청으로 바뀌었다. 이유가 "UI가 화면 하단/좌상단/우측을 차지해서"였으므로 **HUD 배치가 그 전제와 일치해야 한다** (진행상황 7절). |
| 2 | **카메라 줌은 `PixelPerfectCamera` + 정수배 다운샘플.** 월드 공간 UI(집결지 마커 등)는 줌에 따라 크기가 튄다 — 스크린 공간에 그리거나 스케일을 보정할 것. |
| 3 | **안개는 `Overhead` 정렬 레이어 order 100.** 월드 공간 UI는 그보다 큰 order 여야 안 덮인다. |
| 4 | **`Run In Background` 가 꺼져 있으면 타이머가 멈춘다** — 웨이브 타이머 UI 검증 시 이걸 먼저 확인 (진행상황 11절). |
| 5 | 유저 확정 작업 방식: **MCP로 플레이 모드 진입/타이머 대기 같은 런타임 검증은 하지 않는다.** 만들어서 연결까지 하고, 돌려서 보는 건 유저가 한다. |
| 6 | MCP 함정: **비활성 오브젝트는 `instanceId` 로만 접근**(경로 조회 불가, instanceId 는 그 응답 안에서만 유효) · **기존 필드 값 변경은 YAML 패치 대신 `update_component`** · **`.asset` YAML 에 빈 줄 금지** · **`execute_menu_item` 이 "성공"만 내고 실제로 안 도는 경우 있음** (진행상황 8절). |
| 7 | 넥서스는 항상 셀 `(0,0)`(`MapGenerator.CenterCell`). 미니맵 중심 계산에 쓸 수 있다. |

---

## §12. 작업 시작 절차 (매 세션)

1. `C:\Project\라스트 생추어리\진행상황.md` **전체** 읽기 (0절 문서 관리 원칙 포함)
2. 이 문서 읽기 — 특히 §2 소유권, §4 폐기 설계
3. `Docs/작업로그_UI.md` 읽기
4. `git branch --show-current` → `UI` 확인, `git status` → clean 확인
5. `Docs/인계/씬반영요청_PROTO→UI.md` 에 새 요청이 있는지 확인
6. 작업 → 단위마다 커밋 → 종료 전 `작업로그_UI.md` 갱신 → **유저에게 push 요청**

---

## §13. 갱신 이력

| 날짜 | 브랜치 | 무엇을 | 왜 |
|---|---|---|---|
| 2026-08-05 | UI | 최초 작성(v1) | UI/PROTO 병렬 작업 개시. 씬은 PROTO 소유, UI는 additive 씬 + 동결 계약 파일 구조 |
| 2026-08-05 | UI | **v2.1** — §10 을 "MCP 로 하이라키 직접 생성"으로 교체(런타임 코드 생성 방침 폐기), U-S2/U-S8 연동 수정, MCP 실전 주의 추가 | 유저 확정: **객체 생성은 스크립트로 하지 말고 MCP 로 하이라키에 직접 생성. 단 하나의 템플릿을 복제하는 구조는 모체 1개만 MCP 로 만들고 나머지는 스크립트 복제 허용.** 하이라키에 실물이 있어야 인스펙터에서 직접 조정할 수 있다. 씬 저장 비용은 "다 만든 뒤 1회 저장"(H-8)으로 관리한다 |
| 2026-08-05 | UI | **v2 전면 개정** — 전투 AI를 UI 브랜치로 이관, **씬 소유권 PROTO→UI 이전**, `Scripts/Orders/` 계약과 `UI_HUD.unity` 폐기(§4에 사유 보존), §9 이번 스코프 6개 패널 확정, §10 HUD 코드 생성 방침·네오둥근모 고정 추가 | 유저 결정: 전술 명령·집결지는 AI 거동을 바꾸는 기능이라 UI와 AI가 다른 PC에 갈라져 있으면 어느 쪽도 결과를 확인할 수 없다. 두 축이 한 브랜치로 합쳐지면서 계약·별도 씬이 불필요해졌고, 대신 템플릿 조정 빈도가 올라가 씬 소유권을 가져왔다. **v1 은 아직 원격에 push 되지 않아 PROTO 쪽에 배포된 적이 없다 — 그래서 v1 조항을 새 항목으로 덧붙이지 않고 본문을 재작성했다.** 폐기 항목의 사유는 §4 에 남겼다 |
