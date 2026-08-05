# PROTO 브랜치 작업로그

> 이 브랜치에서 한 작업의 기록. 머지 시 `진행상황.md` 의 **24~29절** 로 편입된다
> (형식 유지, 요약 금지 — `머지 계획.md` §7).
> 절 번호는 `PROTO-1` 부터 순서대로. UI 는 `UI-1~` / 진행상황 30절~ 을 쓴다.
>
> ⚠️ **이 파일은 원래 PROTO 브랜치(다른 PC) 소유다.** 아래 PROTO-1 은 예외적으로
> **UI 브랜치 세션이 유저의 명시적 승인을 받아 PROTO 소유 파일에 작업한 기록**이다 —
> 자세한 사유는 항목 본문 참조. PROTO 쪽 세션이 시작되면 이 파일의 존재를 인지하고
> `PROTO-2` 부터 이어서 쓸 것 (겹쳐 쓰지 말 것).

---

## PROTO-1. 웨이브 테이블 반영 (2026-08-05, UI 브랜치 세션이 예외적으로 작업)

### ⚠️ 브랜치 소유권 예외 — 왜 UI 세션이 PROTO 파일을 건드렸는가

`Scripts/Wave/**`, `Scripts/Units/MonsterSpawner.cs`, `Data/Wave/**` 는 `프로토 브렌치
준수사항.md` §2 가 **PROTO 단독 소유**로 지정한 영역이다. 유저가 "웨이브 테이블 확인해서
값 반영"을 요청했을 때 이 경계를 짚어 다시 확인했고, **유저가 "지금 여기서 바로 진행"으로
명시적으로 확정**했다. 진행 전 `git fetch` 로 `origin/PROTO` 상태를 확인해 **아직 아무도
이 파일들을 건드리지 않은 것**(origin/PROTO 가 여전히 시드 커밋 이전의 오래된 커밋
`bb39e53`)을 확인하고서야 실제로 작업했다 — 충돌 위험이 없는 상태였다.

**PROTO 쪽 세션이 나중에 이 파일들을 열어볼 때 당황하지 않도록**: 이 절이 그 이유를
기록해두는 것이다. `Scripts/Wave/WaveDefinitionSO.cs`(신규) / `Data/Wave/WaveDefinitions.asset`
(신규) / `Scripts/Units/MonsterSpawner.cs`(수정) 세 곳이 이번에 바뀌었다.

### 무엇을 / 왜

`C:\Project\라스트 생추어리\테이블\웨이브테이블.xlsx` (Sheet2, `neutrality_mon` 과 같은
"데이터 테이블" 구조)를 열어 확인 — `wave_id`/`wave_num`/`melee_mon_num`/`ranged_mon_num`/
`boss_mon_num`/`wave_mon_abil_per` 6개 열, 1~20웨이브까지 20행. 이게 바로 진행상황 6절·9절이
"미착수"로 남겨둔 `WaveDefinitionSO`(웨이브 번호별 몬스터 구성)의 원본 데이터였다.

지금까지는(진행상황 6절) 웨이브 번호를 올려도 몬스터 **구성**은 고정(스폰 테이블 3줄:
근거리10 원거리10 보스1)이고 **배율**만 `100 + 12%×(w-1)`(체력) / `100 + 10%×(w-1)`(공격)
선형 공식으로 커졌다. 표는 이거보다 훨씬 구체적이다 — 웨이브마다 **마리 수 자체가 다르고**,
능력치 배율도 선형이 아니라 표에 박힌 값(60% → 70% → 80% → 90% → 110% → … → 263%)을
그대로 쓴다.

### 어떻게

- [WaveDefinitionSO.cs](Assets/_Project/Scripts/Wave/WaveDefinitionSO.cs) (신규) —
  `WaveMonsterComposition`(waveNumber/meleeCount/rangedCount/bossCount/statPercent) 배열을
  담는 SO. `TryGetWave(waveNumber)` / `GetWaveOrExtrapolate(waveNumber)`(표 밖이면 마지막
  행을 그대로 반복) 두 조회 API.
- [WaveDefinitions.asset](Assets/_Project/Data/Wave/WaveDefinitions.asset) (신규) — 표
  20행을 그대로 옮겼다. `wave_mon_abil_per`(0.6~2.63, 소수)를 `statPercent`(60~263, 정수)로
  ×100 — 전부 나누어떨어져서 반올림 없이 정확한 정수가 나왔다(4절의 "정수화" 원칙과 맞음).
  SO 에셋 생성 MCP 도구가 없어서 손으로 YAML 작성(8절·20절의 기존 방식 재사용) →
  `Assets/Refresh` 로 임포트.
- [MonsterSpawner.cs](Assets/_Project/Scripts/Units/MonsterSpawner.cs) —
  `waveDefinitions`(WaveDefinitionSO) + `meleeSlot`/`rangedSlot`/`bossSlot`(각각
  `MonsterSpawnEntry`, count 는 무시됨) 3개 필드 추가. `SpawnRoutine()` 에서 **표에 그
  웨이브 행이 있으면** 그 마리 수 + `statPercent` 를 hp/atk 배율 양쪽에 동일하게 써서 대기열을
  만들고, **없으면(표 미지정 등) 기존 `spawnTable` + 선형 공식으로 그대로 되돌아간다** —
  기존 동작을 하나도 안 깨는 분기.

### 설계 판단 — 왜 이렇게 했는지

- **`spawnTable`(기존 3줄 고정 테이블)을 지우지 않고 그대로 뒀다.** 새 표 없이 스포너를
  쓰는 경우(예: 스포너 단독 테스트, `spawnOnStart`)가 여전히 살아있어야 해서 폴백으로
  남겨뒀다.
- **melee/ranged/boss 를 구분하는 필드가 `MonsterDefinitionSO` 에 없었다** (`tier` 는
  Normal/MidBoss/MainBoss 뿐, 근거리·원거리를 안 가른다). 정의 쪽에 새 분류 필드를 추가하는
  대신 스포너 쪽에 `meleeSlot`/`rangedSlot`/`bossSlot` 3개의 **명시적** 참조 슬롯을 뒀다 —
  `spawnTable` 배열 순서에 암묵적으로 의존하는 것보다 이게 더 명확하고, 정의 스키마를 안
  건드려서 변경 범위가 작다.
- **표의 능력치 배율 하나(`wave_mon_abil_per`)를 hp/atk 양쪽에 그대로 쓴다.** 표에 hp/atk
  를 나눈 열이 없었고 "웨이브 몬스터 능력치 배율"이라는 이름 자체가 단일값이라, 기존의
  "hp/atk 를 각각 다른 계수로 선형 증가시키던" 방식과 다르게 **표가 있는 웨이브에서는 둘을
  묶어서** 쓴다.
- **10·20웨이브에만 보스가 있다** (`boss_mon_num`: 나머지는 0, 10·20 은 1). 진행상황 9절이
  "5=중간보스, 10=메인보스" 로 적어둔 건 **확정 아님으로 표시된 추정**이었는데, 실제 표는
  5웨이브에 보스가 없다 — 표가 정본이므로 그대로 반영했다. `MonsterTier.MidBoss` 를 실제로
  쓰는 규칙은 이 표에 없어서 이번 작업 범위에 안 넣었다(여전히 미착수).
- **20웨이브를 넘으면?** 표에 없다. `GetWaveOrExtrapolate` 는 일단 20웨이브 행을 그대로
  반복하는 걸로 임시 처리했다 — 진행상황 10절의 "총 스테이지 수 미확정"과 이어지는 열린
  질문이라, 사이클/반복 규칙이 기획 확정되면 이 메서드만 고치면 된다.

### 겪은 함정

1. **새 SO 에셋을 손으로 만들면 Unity 가 자동 생성한 `.meta` 가 `mainObjectFileID: 0`
   으로 비어 있는 채로 남을 수 있다.** `NativeFormatImporter` 의 캐시 필드라 재-Refresh 로도
   안 채워졌지만, `update_component`/`load_scene` 로 실제 참조를 걸어보니 정상적으로
   `"waveDefinitions": "WaveDefinitions"` 로 이름이 뜨는 것까지 확인됐다 — **`mainObjectFileID`
   가 0 이어도 참조 자체는 유효할 수 있다**(에셋 안의 `--- !u!114 &11400000` 헤더가 실제
   fileID 를 갖고 있어서, 참조 해석은 그걸 기준으로 하는 것으로 보인다). 겉보기 값만으로
   "임포트 실패"로 오판하지 말고 실제로 참조를 걸어서 확인할 것.
2. **새로 추가한 직렬화 필드(`waveDefinitions`/`meleeSlot` 등)는 씬을 한 번 저장해야
   기본값(0/null)으로 YAML 에 나타난다.** 그 전에는 컴포넌트에 아예 안 보여서 YAML 패치할
   자리가 없다 — "스크립트 컴파일 → 씬 저장(필드 골격 생성) → YAML 패치(실제 값) → load_scene
   → 확인 → 저장" 순서를 지켰다(8절 4번 "씬 오브젝트 참조는 MCP 로 설정 불가" 이슈의 표준
   우회 절차, 이번엔 씬 오브젝트가 아니라 **에셋+씬오브젝트가 섞인 구조체 필드**라 같은
   우회가 필요했다).

### 확인된 것
- `recompile_scripts` 에러·경고 0.
- `load_scene` 후 `get_gameobject` 로 `waveDefinitions: "WaveDefinitions"` 참조 해석 확인.
- 최종 저장 후 씬 YAML 을 직접 grep 해서 `waveDefinitions`/`meleeSlot`/`rangedSlot`/
  `bossSlot` 의 guid·fileID 가 `spawnTable` 의 기존 3줄과 정확히 일치(같은 정의·템플릿을
  가리킴)하는 것 확인.
- 콘솔 에러 0. Edit mode 확인 후 저장(필드 골격 생성 1회 + 값 패치 후 1회, 총 2회 — 불가피).

### 아직 확인 못 한 것
- **플레이 모드 검증 전혀 안 함** — 웨이브 1~20 을 실제로 돌려서 마리 수·능력치 배율이
  표대로 나오는지, `SkipPhase()` 로 여러 웨이브를 빨리 넘겨보며 확인이 필요하다.
- 표 밖(21웨이브~) 처리 방식은 임시(20웨이브 반복) — 기획 확정 필요.
- `MonsterTier.MidBoss` 는 여전히 실제로 쓰이는 곳이 없다(표에 근거가 없어서 그대로 둠).

### 씬 변경 여부
있음. `MonsterSpawner` 에 신규 필드 3+1개 추가 및 값 연결. 저장 2회(필드 골격 생성 → 패치).

### 씬반영요청 목록
없음 — 이 항목 자체가 이미 씬을 직접 반영한 기록이다.
