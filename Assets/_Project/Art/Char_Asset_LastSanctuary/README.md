# Char 에셋 패키지 — Last Sanctuary 기사 캐릭터 🐾

원본 참고 시트(`54ba7997-ChatGPT_Image_...png`)에서 캐릭터 프레임 32장을 낱장으로
잘라내고, 배경을 투명하게 만들어서 `Char_` 네이밍 규칙으로 정리한 패키지다옹.

## 폴더 구조 & 네이밍 규칙

```
Char/
  Idle/
    Char_Idle_Right_00.png ~ 03.png   (대기, 오른쪽, 4프레임)
    Char_Idle_Left_00.png  ~ 03.png   (대기, 왼쪽, 4프레임)
  Walk/
    Char_Walk_Right_00.png ~ 05.png   (걷기, 오른쪽, 6프레임)
    Char_Walk_Left_00.png  ~ 05.png   (걷기, 왼쪽, 6프레임)
  Attack/
    Char_Attack_Right_00.png ~ 05.png (공격, 오른쪽, 6프레임)
    Char_Attack_Left_00.png  ~ 05.png (공격, 왼쪽, 6프레임)
```

규칙: `Char_<모션>_<방향>_<프레임번호(2자리)>.png`

- 모션: `Idle` / `Walk` / `Attack`
- 방향: `Right` / `Left`
- 각 파일은 같은 모션·방향 묶음 안에서 캔버스 크기를 통일해뒀어서(발밑 기준 정렬),
  애니메이션 재생할 때 캐릭터가 위아래로 떨리지 않아냥.
- 배경은 전부 투명(RGBA) 처리했어. 원본 시트의 진회색 배경(#1b1b1b 근처)을 색상
  거리로 걷어내고, 경계는 살짝 부드럽게(soft alpha) 남겨서 도트 느낌이 깨지지
  않게 했다옹.

## 왜 파일로만 준비했냐면

이번 세션에는 Unity 에디터에 연결된 MCP 도구가 없어서, 유니티 프로젝트를 직접
조작하는 건 못 했어. 대신 프로젝트에 그대로 끌어다 넣을 수 있는 상태로 다듬어
뒀으니, 아래 순서대로 넣어주면 된다옹. 나중에 로컬 Unity MCP 서버가 이 대화에
연결되면 임포트 설정이나 애니메이터 세팅까지 자동으로 도와줄 수 있을 거야.

## 유니티에 넣는 순서

1. `Char` 폴더 전체를 `Assets/Sprites/Char/` 같은 곳에 복사.
2. 각 PNG 선택 → Inspector에서:
   - **Texture Type**: Sprite (2D and UI)
   - **Filter Mode**: Point (no filter) — 도트가 뭉개지지 않게
   - **Compression**: None
   - **Pixels Per Unit**: 캐릭터를 씬에서 얼마나 크게 보여줄지에 맞춰 조절
     (예: 씬에서 세로 1.3유닛 정도로 보이길 원하면 PPU ≈ 프레임 높이(px) ÷ 1.3).
     "도트 하나 = 2px" 확대 비율 자체는 이미 이미지에 그대로 구워져 있어서, 이
     PPU 값만 원하는 표시 크기에 맞게 잡아주면 된다옹.
3. 각 모션×방향 폴더 안 프레임들을 순서대로 선택 → 씬에 드래그하면 유니티가
   자동으로 Animation Clip을 만들어줘 (`Char_Idle_Right.anim` 식으로 이름 바꿔주면
   깔끔).
   - Idle / Walk: Loop Time 체크
   - Attack: Loop Time 체크 해제 (한 번만 재생)
4. Animator Controller에 6개 클립을 넣고, 파라미터 예시:
   - `IsMoving` (bool): Idle ↔ Walk
   - `Attack` (Trigger): Any State → Attack → (Attack 종료 후 Idle/Walk로 복귀)
   - `FacingRight` (bool) 또는 방향별로 서브 상태머신을 나눠서 Right/Left 클립을
     전환 (이 캐릭터는 좌우가 단순 반전이 아니라 따로 그려져 있어서, SpriteRenderer
     flipX 대신 이 방식을 추천해).

## 참고

- 원본 참고 시트는 AI로 생성된 목업이라 그리드가 완벽히 규칙적이진 않아서, 프레임
  마다 폭이 1~2px씩 다를 수 있어. 실제 게임에 쓸 최종 원화라면, 이 자동 추출본은
  "가배치용 임시 스프라이트"로 쓰고, 최종본은 도트 아티스트가 정식 스프라이트
  시트로 다시 그려주는 걸 추천한다옹.
- 처음에 "완성 스프라이트를 2x2px로" 요청하셨던 부분은, 실제로 그렇게 하면 세밀한
  갑옷·검 형태가 색 4개짜리 얼룩으로만 남아서 원본 해상도 그대로 유지하는 쪽으로
  다시 확인받고 진행했어.
