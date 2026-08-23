using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 원거리·마법 공격이 성사될 때 <b>날아가는 탄환을 그려주는 연출</b>.
    ///
    /// <b>순수 연출이다</b> — 피해는 <see cref="UnitCombat"/> 가 히트 스캔으로 <b>이미 즉시</b>
    /// 넣었고(진행상황 24절의 전투 규칙), 이 클래스는 그 사실을 눈에 보이게만 한다. 탄환이
    /// 목표에 닿는 순간에 피해가 들어가는 것이 <b>아니므로</b>, 여기서 피해를 다시 넣거나
    /// 명중 판정을 하면 이중 타격이 된다. 절대 그러지 말 것.
    ///
    /// <b>왜 씬에 오브젝트를 안 두는가</b> — 스프라이트·프리팹 참조는 오브젝트 참조라서 MCP 로
    /// 씬에 넣을 수 없다(진행상황 8절 4번). 그래서 <see cref="Bootstrap"/> 이 실행 시점에
    /// 스스로 오브젝트를 만들고, 탄환 그림은 <c>Resources</c> 에서 경로로 읽는다
    /// (<see cref="CharacterSkinSO"/> · HUD 폰트가 같은 이유로 <c>Resources</c> 를 쓴다).
    /// 씬에 배선할 것이 하나도 없으므로 유저가 손으로 붙일 것도 없다.
    ///
    /// <b>★ 탄환 그림은 이제 스킨이 들고 있다 (2026-08-11, 유저 지시)</b> —
    /// <see cref="CharacterSkinSO.projectileFrames"/> / <see cref="TowerSkinSO.projectileFrames"/>.
    /// 공격자의 스킨에 탄환 프레임이 있으면 <b>그것을 쓴다</b>. 그래서 같은 진영이라도
    /// 유닛마다 다른 탄환이 날아가고, 새 캐릭터를 추가할 때 이 파일을 건드릴 필요가 없다
    /// (스킨 에셋에 프레임을 넣기만 하면 된다).
    ///
    /// 아래의 <c>Resources/Fx</c> 상수들은 <b>스킨에 탄환이 없는 유닛용 폴백</b>으로만 남아있다 —
    /// 보스·중립 몬스터처럼 아직 전용 탄환을 안 넣은 유닛, 그리고 스킨 자체가 없는 유닛.
    ///
    /// <b>그림 출처</b> — 엘린(Angel) 원거리 공격 시트의 마지막 컷은 캐릭터가 없고 탄환만 그려진
    /// 프레임이었다(그대로 재생하면 시전 중에 캐릭터가 사라졌다). 그 컷에서 탄환과 섬광을
    /// 오려내 <c>Resources/Fx</c> 로 옮긴 것이 폴백으로 쓰는 스프라이트다.
    /// </summary>
    public class CombatProjectileFx : MonoBehaviour
    {
        const string BoltResourcePath = "Fx/Projectile_Bolt";
        const string FlashResourcePath = "Fx/Projectile_Flash";

        /// <summary>암세포(웨이브 몬스터) 진영 전용 탄환. 같은 그림을 색만 바꿔 만든 것이라
        /// 형태(+X 를 향하는 길쭉한 탄환)가 같고 <see cref="AimAt"/> 회전 로직이 그대로 통한다.</summary>
        const string BoltCancerResourcePath = "Fx/Projectile_Bolt_Cancer";
        const string FlashCancerResourcePath = "Fx/Projectile_Flash_Cancer";

        /// <summary>
        /// 포탑(<see cref="UnitKind.Tower"/>) 전용 탄환 — <c>Tower_Asset</c> 의 공격 프레임에
        /// 그려져 있던 붉은 레이저를 오려내 만든 것이다(진행상황 27-11절). 원화에는 빔이
        /// <b>아래-오른쪽으로 고정</b>돼 있었으므로 그대로 두면 포탑이 어느 방향을 쏘든 같은
        /// 방향으로만 빔이 보인다 — 오려서 여기로 넘기면 <see cref="AimAt"/> 이 실제 방향으로
        /// 돌려준다. 이것도 형태가 +X 를 향하므로 회전 로직은 그대로다.
        /// </summary>
        const string BoltTowerResourcePath = "Fx/Projectile_Bolt_Tower";
        const string FlashTowerResourcePath = "Fx/Projectile_Flash_Tower";

        /// <summary>
        /// 분비형 암세포(Spitter)가 뱉는 침 — <b>여러 장짜리</b> 탄환이다.
        ///
        /// 원본 아트팩이 공격 프레임과 <b>별도로</b> 투사체 9프레임을 갖고 있었는데(29-9절
        /// 미결 26번), 그동안 쓰지 않고 천사 탄환을 색만 바꿔 날렸다. 그래서 몬스터가
        /// 뱉는 그림과 날아가는 탄환이 서로 다른 물건으로 보였다. 이제 원본 투사체를
        /// 그대로 쓴다 — 공격 프레임에 구워져 있던 침 줄기는 스킨 쪽에서 지웠으므로
        /// 화면에 보이는 침은 <b>이 탄환 하나뿐</b>이다.
        ///
        /// 프레임은 비행 시간 전체에 고르게 펼친다(마지막 두 장이 흩어지는 그림이라
        /// <b>목표에 닿는 순간 저절로 부서져 사라지는</b> 연출이 된다 — 별도 착탄 효과가 필요 없다).
        /// </summary>
        const string SpitFramePathFormat = "Fx/Projectile_Spit_{0:00}";
        const int SpitFrameMax = 16;

        /// <summary>탄환 속도(월드 유닛/초). 사거리 5타일을 0.2초쯤에 지나가는 값.</summary>
        const float Speed = 26f;

        /// <summary>가장 오래 날아도 이 시간(초)이면 사라진다 — 목표가 죽어 사라져도 남지 않게.</summary>
        const float MaxLifetime = 0.6f;

        // ------------------------------------------------------------------
        // 폴백 탄환의 크기도 <b>타일 기준</b>이다 (유저 확정 2026-08-13).
        // 예전에는 "원화가 74px 이니까 0.55 배" 처럼 <b>픽셀을 보고 고른 배율</b>이었다 —
        // 원화를 바꾸면 화면 크기가 같이 흔들린다. 이제 <b>몇 타일로 그릴지</b>만 적고
        // 배율은 <see cref="ScaleForWidthTiles"/> 가 그 스프라이트 실제 크기로 계산한다.
        // ------------------------------------------------------------------

        /// <summary>기본 탄환 길이(타일). 한 타일이 채 안 되는 작은 화살.</summary>
        const float BoltWidthTiles = 0.8f;

        /// <summary>포탑 레이저는 유닛 탄환보다 굵고 길어야 "포대" 느낌이 난다.</summary>
        const float TowerBoltWidthTiles = 1.3f;

        /// <summary>분비형 암세포의 침 — 한 타일 조금 넘는 덩어리.</summary>
        const float SpitBoltWidthTiles = 1.3f;

        /// <summary>
        /// 이 스프라이트를 가로 <paramref name="widthTiles"/> 타일로 그리기 위한 배율.
        /// 맵 한 칸이 1 월드 유닛이므로 <c>bounds.size.x</c> 가 곧 "지금 몇 타일인지"다.
        /// </summary>
        static float ScaleForWidthTiles(Sprite sprite, float widthTiles)
        {
            if (sprite == null || widthTiles <= 0f) return 1f;
            float now = sprite.bounds.size.x;
            return now > 0.0001f ? widthTiles / now : 1f;
        }

        // ------------------------------------------------------------------
        // ★★★ 연출의 기준은 <b>「몸」 이지 「캔버스」 가 아니다</b> (2026-08-22)
        //
        // 유저 지시: *"캐릭터의 피벗 고정 후 캐릭터의 전방에 이펙트가 생성되도록 로직 구현.
        //   현재 캐릭터의 리소스 공간에 이펙트가 표현되는 버그가 너무 많이 발생함.
        //   단순한 일반 공격 또한 이펙트가 캐릭터의 공간과 분리되어 전방에 시각적으로 잘
        //   표현될 수 있도록 수정 요함"*.
        //
        // <b>무엇이 잘못돼 있었나</b> — 이 클래스는 두 자리에서 <b>SpriteRenderer 의
        // bounds</b>(= 지금 그려지는 <b>프레임 캔버스</b>)를 기준으로 삼고 있었다:
        //
        //     CenterOf()      → <c>sr.bounds.center</c>       «몸 중심» 이라고 적어 두었지만
        //                                                      실제로는 <b>캔버스 중심</b>이다
        //     MuzzleOffset()  → <c>sr.bounds.extents.x</c>    «몸 반지름» 이 아니라
        //                                                      <b>캔버스 반폭</b>이다
        //
        // 그런데 이 프로젝트의 프레임 캔버스는 <b>이펙트까지 담고 있다</b> — 총구 화염·궤적·
        // 바닥 고리가 들어간 프레임은 몸보다 훨씬 넓다(실측: 엘리시아 스킬1 캔버스 268px 에
        // 몸통 91px). 그래서
        //
        //   · 캔버스가 넓은 프레임에서는 <b>중심이 몸 밖으로</b> 밀린다 → 연출이 «몸 옆 허공»
        //     또는 <b>몸 안</b>에서 시작한다(유저가 «리소스 공간에 이펙트가 표현» 이라고 본 것)
        //   · <b>프레임이 바뀔 때마다</b> 기준이 흔들린다 → 같은 공격인데 연출 위치가 떨린다
        //   · 원화를 조금만 고쳐도 <b>게임 안 연출 위치가 따라 흔들린다</b>
        //
        // ★ 고칠 자리는 이미 프로젝트 안에 있었다 — <see cref="CharacterAnimator.RenderedSizeTiles"/>
        //   는 <b>대기 원화의 알파 경계</b>를 재서 배율을 먹인 값이다(`measure_skin_tiles.py`).
        //   즉 <b>캔버스 여백과 이펙트를 뺀 «몸» 의 크기</b>이고, 프레임이 바뀌어도 안 변한다.
        //   포탑도 같은 이름의 칸을 갖고 있다(<see cref="TowerAnimator.RenderedSizeTiles"/>).
        //   그래서 <see cref="BodyBox"/> 가 그 값을 읽고, 없을 때만 예전처럼 bounds 로 내려간다.
        // ------------------------------------------------------------------

        /// <summary>
        /// 연출이 출발할 자리를 몸 중심에서 앞으로 밀어낼 거리 — <b>몸 반폭</b>의 이 배수다.
        ///
        /// ⚠⚠ <b>0.45 → 1.0 으로 올렸다</b> (2026-08-22). 예전 값은 <b>캔버스</b> 반폭에
        ///   0.45 를 곱한 것이라 «대략 실루엣 가장자리» 였는데, 캔버스 폭이 원화마다 달라
        ///   어떤 유닛은 <b>몸 안</b>에서 연출이 시작됐다. 이제 <b>몸</b> 반폭 기준이므로
        ///   1.0 이 정확히 «실루엣 경계» 다 — 유저 지시의 «전방» 을 그대로 옮긴 값이다.
        /// </summary>
        const float MuzzleForwardRatio = 1.0f;

        /// <summary>
        /// 실루엣 경계에서 <b>더</b> 밀어낼 여유(타일). 경계에 딱 붙이면 연출의 왼쪽 절반이
        /// 몸에 겹쳐 «몸에서 새어 나오는» 것처럼 보인다 — 유저가 «캐릭터의 공간과 분리» 라고
        /// 한 것이 이 여유다. 0.2 타일이면 64px 기준 13px 쯤 앞이다.
        /// </summary>
        const float MuzzleClearTiles = 0.2f;

        /// <summary>
        /// 목표가 코앞일 때 밀어낼 수 있는 상한 — 목표까지 거리의 이 비율. 이걸 넘기면
        /// 탄환이 <b>적을 지나쳐서</b> 생겨 «맞추는 것처럼» 안 보인다.
        /// </summary>
        const float MuzzleMaxOfDistance = 0.45f;

        // ------------------------------------------------------------------
        //  기준 유닛이 없는 <b>지면 연출</b>의 정렬 (2026-08-21)
        //
        //  씬의 정렬 레이어는 순서가 있다(ProjectSettings/TagManager):
        //      Default → Background → <b>Floor</b> → <b>Object</b> → Overhead → VFX → WorldUI
        //  실측한 타일맵 셋: 바닥 = Floor(0) · 벽·데코 = Object(0) · 배경 = Background(0).
        //  유닛도 Object 레이어에 있다.
        //
        //  ★ 그래서 <b>Floor 레이어의 양수 순서</b>가 «바닥 타일 위 · 유닛 아래» 다.
        //    레이어 순서가 Floor < Object 이므로 순서 값과 무관하게 유닛보다 아래로 간다 —
        //    밟고 서는 마법진에는 그것이 맞다.
        //  ⚠⚠ <b>Default 를 쓰면 안 된다</b> — 목록의 <b>맨 앞</b>이라 바닥 타일보다도
        //    아래로 가서 <b>아예 안 보인다</b>.
        // ------------------------------------------------------------------

        /// <summary>지면 연출이 놓일 정렬 레이어 이름. 위 주석의 실측 근거를 따른다.</summary>
        const string GroundFxSortingLayer = "Floor";

        /// <summary>바닥 타일(Floor 0)보다 위로 올리는 순서. 같은 레이어 안에서만 의미가 있다.</summary>
        const int GroundFxSortingOrder = 10;

        /// <summary>시전 섬광이 머무는 시간(초).</summary>
        const float FlashSeconds = 0.12f;

        /// <summary>
        /// 착탄 효과가 머무는 시간(초). 섬광보다 길다 — <b>마법이면 이것이 피해 범위 표시</b>라
        /// 눈으로 읽을 시간이 필요하다. 뒤쪽 40% 구간에 옅어지며 사라진다.
        /// </summary>
        const float ImpactSeconds = 0.32f;

        /// <summary>
        /// 근접 평타 연출(<see cref="PlayMeleeTravel"/>)의 비행 시간 범위(초).
        ///
        /// 근접은 거리가 1~3타일이라 <see cref="Speed"/>(26타일/초)로 계산하면 0.04~0.12초 —
        /// 4프레임 그림이 <b>한두 장만 보이고 사라진다.</b> 그래서 하한을 둔다.
        /// 상한은 몸집이 큰 보스(라린길 11타일)가 멀리서 때릴 때 연출이 늘어지지 않게.
        /// </summary>
        const float MeleeTravelMinSeconds = 0.18f;
        const float MeleeTravelMaxSeconds = 0.30f;

        static CombatProjectileFx _instance;

        Sprite _bolt;
        Sprite _flash;
        Sprite _boltCancer;
        Sprite _flashCancer;
        Sprite _boltTower;
        Sprite _flashTower;
        Sprite[] _spitFrames;

        struct Shot
        {
            public Transform Tr;
            public SpriteRenderer Renderer;
            public Vector3 From;
            public Vector3 To;
            public float Elapsed;
            public float Duration;

            /// <summary>제자리에서 옅어지며 사라진다(발사 섬광·착탄 효과). 날아가지 않는다.</summary>
            public bool Stationary;

            /// <summary>
            /// 이 시간(초)이 지나야 보이기 시작한다. 착탄 효과가 <b>탄환이 도착한 뒤</b>
            /// 터지게 하는 수단 — 비행 시간을 그대로 넣는다.
            /// </summary>
            public float Delay;

            /// <summary>여러 장짜리면 살아있는 시간에 맞춰 넘길 프레임 목록. 아니면 null.</summary>
            public Sprite[] Frames;

            /// <summary>
            /// ★★ <b>취소용 손잡이</b> (2026-08-21). 0 이면 «취소할 수 없는 한 방» 이다
            /// (평타 탄환처럼 스스로 끝나는 것). <see cref="PlayArea"/> 가 돌려주는 번호와 같다.
            /// </summary>
            public int Handle;
        }

        readonly List<Shot> _live = new List<Shot>();
        readonly Stack<Transform> _pool = new Stack<Transform>();

        /// <summary>다음에 발급할 손잡이 번호. 0 은 «없음» 이라 1 부터 쓴다.</summary>
        int _nextHandle = 1;

        /// <summary>
        /// 씬에 아무것도 없어도 스스로 붙는다. 정적 이벤트를 쓰므로 도메인 리로드를 꺼도
        /// 구독이 남지 않게 <c>SubsystemRegistration</c> 단계에서 다시 만든다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null) return;

            var go = new GameObject("~CombatProjectileFx");
            go.hideFlags = HideFlags.DontSave;
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<CombatProjectileFx>();
        }

        void Awake()
        {
            _instance = this;
            _bolt = Resources.Load<Sprite>(BoltResourcePath);
            _flash = Resources.Load<Sprite>(FlashResourcePath);
            _boltCancer = Resources.Load<Sprite>(BoltCancerResourcePath);
            _flashCancer = Resources.Load<Sprite>(FlashCancerResourcePath);
            _boltTower = Resources.Load<Sprite>(BoltTowerResourcePath);
            _flashTower = Resources.Load<Sprite>(FlashTowerResourcePath);
            _spitFrames = LoadFrames(SpitFramePathFormat);

            if (_bolt == null)
                Debug.LogWarning($"[Fx] 폴백 탄환 Resources/{BoltResourcePath} 를 찾지 못했습니다. " +
                                 "스킨에 전용 탄환이 없는 유닛(보스·중립)은 탄환이 안 보입니다.");

            // 침 탄환은 프레임이 몇 장 잡혔는지 남긴다 — 파일만 넣으면 되는 구조라
            // "왜 예전 탄환이 그대로 날아가지?" 를 로그 한 줄로 바로 알 수 있게.
            Debug.Log(_spitFrames != null
                ? $"[Fx] 폴백 침 탄환 {_spitFrames.Length}프레임 로드"
                : "[Fx] 폴백 침 탄환 프레임 없음 — 암세포는 예전 탄환(Projectile_Bolt_Cancer)을 씁니다");
        }

        /// <summary>
        /// <c>Fx/이름_00, _01 …</c> 을 끊길 때까지 읽는다. 프레임을 늘리려면 파일만 더 넣으면 되고
        /// 코드를 고칠 필요가 없다 — 이 프로젝트가 스킨·BGM 에 쓰는 방식과 같다.
        /// </summary>
        static Sprite[] LoadFrames(string format)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < SpitFrameMax; i++)
            {
                var s = Resources.Load<Sprite>(string.Format(format, i));
                if (s == null) break;
                list.Add(s);
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        // ★ 씬 전환도 같이 구독한다 (2026-08-21) — 이 오브젝트는 DontDestroyOnLoad 라
        //   씬을 넘겨도 살아남으므로, 남아 있던 연출을 새 판으로 끌고 가지 않게 치운다
        //   (<see cref="HandleSceneLoaded"/> 의 ★★).
        void OnEnable()
        {
            DamageableUnit.OnAnyAttack += HandleAttack;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        void OnDisable()
        {
            DamageableUnit.OnAnyAttack -= HandleAttack;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        /// <summary>
        /// 한 유닛이 쏘는 탄환 한 벌. 스킨에서 읽거나, 스킨에 없으면 폴백에서 만든다.
        /// </summary>
        struct ProjectileArt
        {
            /// <summary>탄환 프레임(1장 이상). 여러 장이면 비행 중에 넘긴다.</summary>
            public Sprite[] Frames;

            /// <summary>발사 섬광 — <b>쏘는 쪽</b>에서 반짝인다. 없으면 띄우지 않는다.</summary>
            public Sprite[] Muzzle;

            /// <summary>착탄 효과 — <b>맞는 쪽</b>에서 터진다. 마법이면 피해 범위 표시.</summary>
            public Sprite[] Impact;

            public float Scale;

            /// <summary>착탄 효과 배율. 탑뷰에 눕히려고 y 를 따로 줄일 수 있다.</summary>
            public Vector2 ImpactScale;

            public bool IsValid => Frames != null && Frames.Length > 0;
        }

        /// <summary>근거리는 건너뛰고 원거리·마법만 탄환을 띄운다.</summary>
        void HandleAttack(DamageableUnit attacker, DamageableUnit target)
        {
            if (attacker == null || target == null) return;

            var combat = attacker.GetComponent<UnitCombat>();
            if (combat == null) return;

            // ★★ 근접 평타의 «날아가는» 연출 (2026-08-20 · 라린길 발톱 참격).
            //    근접은 아래 원거리 경로를 타지 않으므로 여기서 갈라진다.
            if (combat.AttackType == TacticalAttackType.Melee)
            {
                PlayMeleeTravel(attacker, target);
                return;
            }

            if (combat.AttackType != TacticalAttackType.Ranged &&
                combat.AttackType != TacticalAttackType.Magic) return;

            // ★★ 투사체를 쓰지 않는 유닛 (2026-08-19 · 엘린의 「쇠사슬 솟구침」).
            //    <b>ArtFor 보다 먼저</b> 갈라져야 한다 — 그쪽은 스킨에 탄환이 없으면
            //    진영 기본 탄환으로 떨어지므로, 뒤에서 걸러도 이미 늦다.
            if (TryPlayGroundImpact(attacker, target, combat)) return;

            ProjectileArt art = ArtFor(attacker, combat);
            if (!art.IsValid) return;

            Vector3 from = CenterOf(attacker);
            Vector3 to = CenterOf(target);
            float dist = ((Vector2)(to - from)).magnitude;
            if (dist < 0.01f) return;

            // 몸 중심이 아니라 앞쪽(입·총구)에서 나가게 민다.
            from += MuzzleOffset(attacker, (to - from) / dist, dist);
            dist = ((Vector2)(to - from)).magnitude;
            if (dist < 0.01f) return;

            float flight = Mathf.Min(MaxLifetime, dist / Speed);

            Spawn(from, to, flight, attacker, art.Frames[0], Vector2.one * art.Scale,
                  frames: art.Frames.Length > 1 ? art.Frames : null,
                  rotation: AimAt(to - from));

            // 발사 섬광 — 쏘는 쪽에서 즉시.
            if (HasFrames(art.Muzzle))
                Spawn(from, from, FlashSeconds, attacker, art.Muzzle[0], Vector2.one * art.Scale,
                      frames: art.Muzzle.Length > 1 ? art.Muzzle : null, stationary: true);

            // 착탄 효과 — 맞는 쪽에서, 탄환이 도착한 뒤에.
            // 회전은 주지 않는다: 바닥에 깔리는 범위 표시라 발사 방향으로 돌리면 기울어진다.
            if (HasFrames(art.Impact))
            {
                Vector2 s = art.ImpactScale == Vector2.zero ? Vector2.one : art.ImpactScale;
                Spawn(to, to, ImpactSeconds, target, art.Impact[0], s,
                      frames: art.Impact.Length > 1 ? art.Impact : null,
                      stationary: true, delay: flight);
            }
        }

        static bool HasFrames(Sprite[] frames) => frames != null && frames.Length > 0;

        /// <summary>
        /// ★★ <b>근접 평타의 날아가는 연출</b> (2026-08-20 · 라린길 발톱 참격).
        ///
        /// <b>왜 투사체 경로를 못 쓰는가</b> — <see cref="HandleAttack"/> 의 원거리 경로는
        /// <see cref="CharacterSkinSO.projectileFrames"/> 를 읽고, 그 칸이 비면
        /// <see cref="FallbackArt"/> 로 내려가 <b>진영 기본 탄환</b>(회색 화살)을 띄운다.
        /// 근접 유닛에 그게 붙으면 화살을 쏘는 그림이 된다. 그래서 <b>전용 칸</b>
        /// (<see cref="CharacterSkinSO.meleeTravelFrames"/>)을 읽고, 없으면 <b>아무것도
        /// 하지 않는다</b> — 폴백이 없는 것이 요점이다.
        ///
        /// <b>비행 시간</b>은 거리로 정하지만 <see cref="MeleeTravelMaxSeconds"/> 로 자른다 —
        /// 근접은 거리가 짧아 그대로 두면 한두 프레임에 끝나 보이지 않는다.
        ///
        /// ⚠ <b>순수 연출이다</b> — 근접 평타의 피해는 <see cref="UnitCombat"/> 이 이미
        /// 즉시 넣었다. 이 클래스의 대원칙 그대로다.
        /// </summary>
        void PlayMeleeTravel(DamageableUnit attacker, DamageableUnit target)
        {
            var anim = attacker.GetComponent<CharacterAnimator>();
            CharacterSkinSO skin = anim != null ? anim.Skin : null;
            if (skin == null || !skin.HasMeleeTravel) return;

            Sprite[] frames = skin.meleeTravelFrames;
            Sprite first = frames[0];
            if (first == null) return;

            Vector3 from = CenterOf(attacker);
            Vector3 to = CenterOf(target);
            float dist = ((Vector2)(to - from)).magnitude;
            if (dist < 0.01f) return;

            // 몸 중심이 아니라 앞쪽(발톱이 뻗는 자리)에서 나가게 민다 — 라린길은 몸집이
            // 11타일이라 중심에서 띄우면 연출이 자기 몸 안에서 시작한다.
            from += MuzzleOffset(attacker, (to - from) / dist, dist);
            dist = ((Vector2)(to - from)).magnitude;
            if (dist < 0.01f) return;

            Vector2 scale = Vector2.one;
            if (skin.meleeTravelWidthTiles > 0f)
                scale = Vector2.one * ScaleForWidthTiles(first, skin.meleeTravelWidthTiles);

            float flight = Mathf.Clamp(dist / Speed, MeleeTravelMinSeconds, MeleeTravelMaxSeconds);
            Spawn(from, to, flight, attacker, first, scale,
                  frames: frames.Length > 1 ? frames : null,
                  rotation: AimAt(to - from));
        }

        /// <summary>
        /// ★★ <b>투사체 없는 원거리·마법</b> — 대상 <b>발밑</b>에 착탄 연출만 즉시 깐다
        /// (유저 지시 2026-08-19: <i>"투사체 없이 적중대상 땅바닥에서 사슬이 올라오는 걸로"</i>).
        /// 이 스킨이 그런 유닛이 아니면 <c>false</c> 를 돌려주고 평소 경로로 보낸다.
        ///
        /// <b>왜 발밑인가</b> — 다른 착탄 연출은 몸통 중심(<see cref="CenterOf"/>)에 놓는다.
        /// 그건 「맞은 자리에 터지는」 그림이라 몸 위가 맞다. 땅에서 <b>솟아오르는</b> 그림은
        /// 밑동이 지면에 박혀 있어야 하고, 이 프로젝트의 원화 피벗은 전부 발밑(0.5, 0)이라
        /// <c>transform.position</c> 을 그대로 쓰면 된다.
        ///
        /// ⚠ <b>피해는 여기서 넣지 않는다</b> — 이 클래스의 대원칙 그대로 순수 연출이다.
        /// 피해는 <see cref="UnitCombat"/> 이 발사와 동시에 이미 넣었다(히트 스캔).
        /// </summary>
        bool TryPlayGroundImpact(DamageableUnit attacker, DamageableUnit target, UnitCombat combat)
        {
            var anim = attacker.GetComponent<CharacterAnimator>();
            CharacterSkinSO skin = anim != null ? anim.Skin : null;
            if (skin == null || !skin.groundImpactOnly) return false;

            Sprite[] impact = skin.ImpactFor(combat.AttackType);
            if (!HasFrames(impact)) return true;   // 의도는 「탄환 없음」이라 기본 탄환으로 안 내려간다

            Vector2 scale = skin.ImpactScaleFor(combat.MagicAreaTiles);
            if (scale == Vector2.zero) scale = Vector2.one;

            // 회전을 주지 않는다 — 바닥에서 솟는 그림이라 발사 방향으로 돌리면 눕는다.
            Spawn(target.transform.position, target.transform.position, ImpactSeconds,
                  target, impact[0], scale,
                  frames: impact.Length > 1 ? impact : null, stationary: true);
            return true;
        }

        /// <summary>
        /// ★ <b>회복 연출</b> — 회복이 실제로 들어간 순간 <b>회복받은 쪽 발밑</b>에 한 번 깐다
        /// (엘린 시트의 「회복 이펙트」 = 초록 십자가 7장 · <see cref="CharacterSkinSO.healFxFrames"/>).
        ///
        /// <b>왜 착탄 경로를 못 쓰나</b> — <see cref="HandleAttack"/> 는 원거리·마법에서만
        /// 돌고 회복은 투사체가 없어 그 경로를 아예 타지 않는다. 그래서 회복에는 지금까지
        /// <b>연출이 하나도 없었다.</b> 부르는 쪽은 <c>UnitCombat.PerformHeal</c> 하나다.
        ///
        /// ⚠ 순수 연출이다 — 회복량은 부르는 쪽이 이미 넣었다.
        /// </summary>
        public static void PlayHeal(DamageableUnit healer, DamageableUnit target)
        {
            if (_instance == null || healer == null || target == null) return;

            var anim = healer.GetComponent<CharacterAnimator>();
            Sprite[] frames = anim != null && anim.Skin != null ? anim.Skin.HealFx() : null;
            if (!HasFrames(frames)) return;

            Vector2 scale = anim.Skin.ImpactScaleFor(0f);
            if (scale == Vector2.zero) scale = Vector2.one;

            _instance.Spawn(target.transform.position, target.transform.position,
                            ImpactSeconds, target, frames[0], scale,
                            frames: frames.Length > 1 ? frames : null, stationary: true);
        }

        /// <summary>
        /// <b>범위 연출 한 번</b> — 정해진 직사각형을 그림 한 장으로 덮어 그린다
        /// (<see cref="BossSkillCaster"/> 의 보스 스킬 범위 표시).
        ///
        /// 여기에 둔 이유는 <b>풀·수명 관리가 이미 여기 있기 때문</b>이다. 스킬 쪽에서
        /// <c>new GameObject</c> 를 하면 매 시전마다 오브젝트가 생겼다 사라지고, 사라지는
        /// 시점을 또 관리해야 한다. 마법 착탄 연출이 쓰는 경로를 그대로 재사용한다.
        ///
        /// ⚠ <b>순수 연출이다</b> — 피해는 <see cref="BossSkillCaster"/> 가 이미 넣었다.
        /// 이 클래스의 대원칙(맨 위 주석)과 같다: 여기서 피해를 다시 넣으면 이중 타격이다.
        ///
        /// <paramref name="sizeTiles"/> 는 <b>피해 범위 그대로</b>를 받는다 — 그래야
        /// "보이는 범위 = 맞는 범위" 가 된다(61-5절의 마법 착탄과 같은 규칙).
        ///
        /// ⚠ <paramref name="sizeTiles"/> 는 <b>회전하기 전(그림 기준)</b> 크기다:
        /// x = 조준 방향으로 뻗는 길이, y = 그와 직각인 두께. 유니티는 스케일을 먼저,
        /// 회전을 나중에 적용하므로 세로로 쏠 때 x·y 를 바꿔 넣으면 안 된다
        /// (<paramref name="angleDeg"/> 만 90 으로 주면 된다).
        /// </summary>
        /// <returns>
        /// ★★ <b>취소용 손잡이</b> (2026-08-21). 0 이면 «못 만들었다» 이거나 취소가 필요 없는
        /// 경우다. 여러 초 동안 남는 연출은 이 번호를 들고 있다가
        /// <see cref="Cancel"/> 로 <b>주인이 사라질 때 같이 지워야 한다</b> —
        /// 안 그러면 «게임에 없는 캐릭터의 스킬 그림이 맵에 장식물처럼 남는다»
        /// (유저 리포트 2026-08-21 · 아르세니아 「성스러운 축복」).
        /// </returns>
        public static int PlayArea(Sprite[] frames, Vector3 center, Vector2 sizeTiles,
                                   float angleDeg, DamageableUnit anchor, float seconds)
        {
            if (_instance == null || !HasFrames(frames)) return 0;

            Sprite first = frames[0];
            if (first == null) return 0;

            // 원화의 세계 크기(타일)로 나눠 목표 크기에 맞춘다. 범위 표시는 직사각형이라
            // 가로·세로를 따로 늘려도 된다 — 유닛 그림과 달리 비율이 의미를 갖지 않는다.
            Vector3 art = first.bounds.size;
            var scale = new Vector2(
                art.x > 0.0001f ? sizeTiles.x / art.x : 1f,
                art.y > 0.0001f ? sizeTiles.y / art.y : 1f);

            // ⚠ <b>피벗 보정</b> — 이 프로젝트의 원화는 전부 <b>발밑 피벗</b>(0.5, 0)이다.
            // 그대로 놓으면 그림이 지정한 지점에서 <b>위로만</b> 뻗어 상자와 반 칸씩 어긋나고,
            // 세로로 쏠 때는 피벗을 축으로 통째로 돌아 엉뚱한 데 그려진다.
            // <c>Sprite.bounds.center</c> 가 "피벗에서 그림 중심까지"라 그만큼 되밀면
            // <b>피벗이 어디든</b> 상자 한가운데에 놓인다.
            var rotation = Quaternion.Euler(0f, 0f, angleDeg);
            Vector3 pivotFix = rotation * new Vector3(first.bounds.center.x * scale.x,
                                                      first.bounds.center.y * scale.y, 0f);
            Vector3 at = center - pivotFix;

            int handle = _instance._nextHandle++;
            _instance.Spawn(at, at, Mathf.Max(0.05f, seconds), anchor, first, scale,
                            frames: frames.Length > 1 ? frames : null,
                            stationary: true, rotation: rotation, handle: handle);
            return handle;
        }

        /// <summary>
        /// ★★ <b>띄워 둔 연출을 지운다</b> (2026-08-21 신설).
        ///
        /// <b>왜 필요한가</b> — 유저 리포트: *"아르세니아가 없는데 아르세니아의 2번째 스킬이
        /// 맵에 장식물처럼 구현되어있음"*. 원인은 이 클래스에 <b>취소 통로가 없었다</b>는 것이다:
        /// <see cref="PlayArea"/> 로 띄운 8초짜리 마법진은 <b>자기 타이머로만</b> 사라지고,
        /// 그 그림이 대신하는 실체(<c>SacredZone</c>)가 주인이 죽어 없어져도 <b>혼자 남았다</b>.
        /// 그림과 실체의 수명이 <b>두 벌</b>이었던 것이 버그의 정체다.
        ///
        /// ★ 이제 주인이 <b>같은 손잡이로</b> 지운다 — 수명이 한 벌이 된다.
        /// ⚠ 이미 사라진 손잡이를 넣어도 <b>안전하다</b>(아무 일도 안 한다) — 타이머가 먼저
        ///   끝난 경우와 주인이 먼저 죽은 경우가 <b>둘 다</b> 정상이다.
        /// </summary>
        public static void Cancel(int handle)
        {
            if (_instance == null || handle == 0) return;

            for (int i = _instance._live.Count - 1; i >= 0; i--)
            {
                if (_instance._live[i].Handle != handle) continue;
                _instance.Retire(i);
                return;
            }
        }

        /// <summary>
        /// <b>날아가는 탄환 한 발</b> — 보스 스킬이 자기 탄환을 따로 띄울 때 쓴다
        /// (말파스 「구속탄」 2026-08-18).
        ///
        /// <b>왜 <see cref="HandleAttack"/> 을 못 쓰는가</b> — 그쪽은 <b>평타 이벤트</b>에
        /// 붙어 있고 스킨의 <c>projectileFrames</c>(말파스의 검은 구체) 한 벌만 쓴다.
        /// 구속탄은 <b>다른 그림</b>(초록 바이러스 구체)이고 발사 시점도 평타가 아니다.
        ///
        /// ⚠ <b>순수 연출이다</b> — 피해는 <see cref="BossSkillCaster"/> 가 발사와 <b>동시에</b>
        /// 이미 넣었다(이 프로젝트의 보스 스킬은 전부 그렇다). 탄환이 도착해야 맞는 것이
        /// 아니므로 <paramref name="seconds"/> 는 <b>보이는 시간</b>일 뿐이다.
        ///
        /// <paramref name="sizeTiles"/> 는 그림을 몇 타일 크기로 그릴지다(0 이면 원화 크기 그대로).
        /// </summary>
        public static void PlayTravel(Sprite[] frames, Vector3 from, Vector3 to,
                                      float seconds, DamageableUnit anchor, float sizeTiles = 0f)
        {
            if (_instance == null || !HasFrames(frames)) return;

            Sprite first = frames[0];
            if (first == null) return;

            Vector2 scale = Vector2.one;
            if (sizeTiles > 0f)
            {
                Vector3 art = first.bounds.size;
                float longest = Mathf.Max(art.x, art.y);
                if (longest > 0.0001f) scale = Vector2.one * (sizeTiles / longest);
            }

            _instance.Spawn(from, to, Mathf.Max(0.05f, seconds), anchor, first, scale,
                            frames: frames.Length > 1 ? frames : null,
                            rotation: AimAt(to - from));
        }

        /// <summary>
        /// 이 공격자가 쓸 탄환. <b>스킨이 먼저다</b> — 스킨에 탄환 프레임이 들어있으면
        /// 그대로 쓰고, 없는 유닛만 아래의 진영·종류 폴백으로 넘어간다.
        ///
        /// <paramref name="combat"/> 는 <b>마법의 실제 피해 범위</b>를 읽으려고 받는다 —
        /// 착탄 연출이 곧 범위 표시이므로 "보이는 범위 = 맞는 범위" 여야 한다.
        /// </summary>
        ProjectileArt ArtFor(DamageableUnit attacker, UnitCombat combat)
        {
            // 마법이면 실제 착탄 범위(타일)를, 아니면 0(스킨의 표시 크기를 쓰라는 뜻).
            float areaTiles = combat != null ? combat.MagicAreaTiles : 0f;

            var charAnim = attacker.GetComponent<CharacterAnimator>();
            if (charAnim != null && charAnim.Skin != null && charAnim.Skin.HasProjectile)
                return new ProjectileArt
                {
                    Frames = charAnim.Skin.projectileFrames,
                    Muzzle = charAnim.Skin.muzzleFlashFrames,
                    // ★ 마법이면 마법 전용 착탄 그림이 먼저다 (2026-08-19).
                    Impact = charAnim.Skin.ImpactFor(combat != null ? combat.AttackType
                                                                   : TacticalAttackType.Ranged),
                    Scale = charAnim.Skin.ProjectileScale,
                    ImpactScale = charAnim.Skin.ImpactScaleFor(areaTiles),
                };

            var towerAnim = attacker.GetComponent<TowerAnimator>();
            if (towerAnim != null && towerAnim.Skin != null && towerAnim.Skin.HasProjectile)
                return new ProjectileArt
                {
                    Frames = towerAnim.Skin.projectileFrames,
                    Muzzle = towerAnim.Skin.muzzleFlashFrames,
                    Impact = towerAnim.Skin.impactFrames,
                    Scale = towerAnim.Skin.ProjectileScale,
                    ImpactScale = towerAnim.Skin.ImpactScaleFor(areaTiles),
                };

            return FallbackArt(attacker);
        }

        /// <summary>
        /// 스킨에 탄환이 없는 유닛용. 예전(27절·30절) 규칙 그대로 진영·종류로 고른다 —
        /// 보스·중립 몬스터가 여기로 온다. 새 유닛에 전용 탄환을 주고 싶으면
        /// <b>이 함수를 고치는 것이 아니라</b> 스킨 에셋에 프레임을 넣으면 된다.
        /// </summary>
        ProjectileArt FallbackArt(DamageableUnit attacker)
        {
            bool tower = attacker.Kind == UnitKind.Tower;
            bool cancer = attacker.Faction == Faction.Cancer;
            bool spit = cancer && !tower && _spitFrames != null;

            if (spit)
                return new ProjectileArt
                {
                    Frames = _spitFrames,
                    Muzzle = _flashCancer != null ? new[] { _flashCancer } : null,
                    Scale = ScaleForWidthTiles(_spitFrames[0], SpitBoltWidthTiles),
                    ImpactScale = Vector2.one,
                };

            Sprite bolt = tower && _boltTower != null ? _boltTower
                        : cancer && _boltCancer != null ? _boltCancer : _bolt;
            Sprite flash = tower && _flashTower != null ? _flashTower
                         : cancer && _flashCancer != null ? _flashCancer : _flash;
            if (bolt == null) return default;

            float widthTiles = tower && _boltTower != null ? TowerBoltWidthTiles : BoltWidthTiles;
            return new ProjectileArt
            {
                Frames = new[] { bolt },
                Muzzle = flash != null ? new[] { flash } : null,
                Scale = ScaleForWidthTiles(bolt, widthTiles),
                ImpactScale = Vector2.one,
            };
        }

        /// <summary>
        /// ★★ 이 유닛의 <b>「몸」 상자</b> — 가운데(월드)와 크기(타일=월드 유닛).
        /// 캔버스가 아니라 <b>대기 원화 실측값</b>을 쓴다(맨 위 ★★★).
        ///
        /// 못 구하면 <c>false</c> 를 돌려주고, 그때만 부르는 쪽이 예전처럼 bounds 로 내려간다
        /// (실측값이 없는 옛 프리팹·연출 전용 오브젝트).
        /// </summary>
        static bool BodyBox(DamageableUnit unit, out Vector3 center, out Vector2 size)
        {
            center = unit != null ? unit.transform.position : Vector3.zero;
            size = Vector2.zero;
            if (unit == null) return false;

            var charAnim = unit.GetComponent<CharacterAnimator>();
            if (charAnim != null) size = charAnim.RenderedSizeTiles;
            if (size.x <= 0.0001f || size.y <= 0.0001f)
            {
                var towerAnim = unit.GetComponent<TowerAnimator>();
                if (towerAnim != null) size = towerAnim.RenderedSizeTiles;
            }
            if (size.x <= 0.0001f || size.y <= 0.0001f) return false;

            // ★ 피벗이 <b>발밑</b>(0.5, 0)이라 <c>transform.position</c> 이 곧 바닥이다 —
            //   몸 가운데는 거기서 몸 높이의 절반만큼 위다. 캔버스와 무관한 값이다.
            center += Vector3.up * (size.y * 0.5f);
            return true;
        }

        /// <summary>유닛의 몸통 중심. 발밑 피벗이라 <c>transform.position</c> 은 바닥이다.</summary>
        static Vector3 CenterOf(DamageableUnit unit)
        {
            if (BodyBox(unit, out Vector3 center, out _)) return center;

            // ⚠ 폴백 — 실측값이 없을 때만. 캔버스 중심이라 이펙트가 든 프레임에서 밀린다.
            var sr = unit.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null) return sr.bounds.center;
            return unit.transform.position;
        }

        /// <summary>
        /// 발사 지점을 몸 중심에서 목표 쪽으로 밀어낼 양 — <b>몸</b> 가로 반지름 기준이라
        /// (캔버스가 아니다 · 맨 위 ★★★) 유닛 크기에 자동으로 맞고 프레임이 바뀌어도 안 흔들린다.
        /// 목표가 코앞이면 <see cref="MuzzleMaxOfDistance"/> 까지만 밀어 탄환이 적을 지나쳐서
        /// 생기지 않게 한다.
        /// </summary>
        static Vector3 MuzzleOffset(DamageableUnit shooter, Vector3 dir, float dist)
        {
            float half;
            if (BodyBox(shooter, out _, out Vector2 size))
            {
                half = size.x * 0.5f;
            }
            else
            {
                var sr = shooter.GetComponent<SpriteRenderer>();
                if (sr == null || sr.sprite == null) return Vector3.zero;
                // ⚠ 폴백은 캔버스 반폭이므로 옛 비율(0.45)을 그대로 쓴다 — 새 비율(1.0)을
                //   캔버스에 곱하면 넓은 원화에서 연출이 너무 멀리 나간다.
                half = sr.bounds.extents.x * 0.45f;
                return dir * Mathf.Min(half, dist * MuzzleMaxOfDistance);
            }

            float forward = half * MuzzleForwardRatio + MuzzleClearTiles;
            return dir * Mathf.Min(forward, dist * MuzzleMaxOfDistance);
        }

        /// <summary>
        /// 연출 하나를 띄운다. <paramref name="anchor"/> 는 정렬 기준이 될 유닛이다 —
        /// 탄환·발사 섬광은 <b>쏘는 쪽</b>, 착탄 효과는 <b>맞는 쪽</b>을 넘긴다.
        /// 그래야 맞는 유닛 위에 범위 표시가 덮이고 유닛 몸에 가려지지 않는다.
        /// </summary>
        void Spawn(Vector3 from, Vector3 to, float duration, DamageableUnit anchor,
                   Sprite sprite, Vector2 scale, Sprite[] frames = null,
                   bool stationary = false, float delay = 0f, Quaternion? rotation = null,
                   int handle = 0)
        {
            Transform tr = _pool.Count > 0 ? _pool.Pop() : NewProjectile();
            var sr = tr.GetComponent<SpriteRenderer>();

            sr.sprite = sprite;
            // 섬광·착탄은 알파를 깎으며 사라지므로, 풀에서 다시 꺼내 쓸 때 되돌려놔야 한다.
            sr.color = Color.white;
            // 지연 시작인 연출은 첫 프레임부터 숨겨둔다(Update 가 때가 되면 켠다).
            sr.enabled = delay <= 0f;

            // 유닛보다 위에 그려야 탄환이 몸에 가려지지 않는다.
            var anchorSr = anchor != null ? anchor.GetComponent<SpriteRenderer>() : null;
            if (anchorSr != null)
            {
                sr.sortingLayerID = anchorSr.sortingLayerID;
                sr.sortingOrder = anchorSr.sortingOrder + 20;
            }
            else
            {
                // ★★ <b>기준 유닛이 없으면 정렬을 못박는다</b> (2026-08-21).
                //
                //   ⚠ 예전에는 여기서 <b>아무것도 하지 않았다</b> — 그래서 풀에서 꺼낸
                //     오브젝트는 <b>지난번에 쓰던 레이어·순서를 그대로</b> 들고 나오고,
                //     새로 만든 것은 «기본 레이어 · 순서 0» 즉 <b>타일맵 깊이</b>였다.
                //     캐릭터 스킬의 범위 연출은 전부 <c>anchor: null</c> 로 부르므로
                //     («맞는 쪽» 이 여럿이라 하나를 고를 수 없다) 그 그림이 <b>바닥 장식과
                //     같은 층</b>에 깔렸다 — 유저가 «맵에 장식물처럼» 이라고 본 것의 절반이다.
                //   ★ 지면 연출이므로 <b>바닥 타일 위 · 유닛 아래</b>가 맞다(밟고 서는 그림이다).
                //     레이어·순서를 고른 근거는 위 GroundFxSortingLayer 주석에 실측으로 적었다.
                sr.sortingLayerID = SortingLayer.NameToID(GroundFxSortingLayer);
                sr.sortingOrder = GroundFxSortingOrder;
            }

            tr.position = from;
            tr.localScale = new Vector3(scale.x, scale.y, 1f);
            tr.rotation = rotation ?? Quaternion.identity;
            tr.gameObject.SetActive(true);

            _live.Add(new Shot
            {
                Tr = tr, Renderer = sr, From = from, To = to,
                Elapsed = 0f, Duration = Mathf.Max(0.01f, duration),
                Stationary = stationary, Delay = Mathf.Max(0f, delay),
                Frames = frames, Handle = handle,
            });
        }

        /// <summary>탄환 원화는 +X 를 향하고 있으므로 진행 방향으로 돌려주면 된다.</summary>
        static Quaternion AimAt(Vector3 dir) =>
            Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        Transform NewProjectile()
        {
            var go = new GameObject("Projectile");
            go.transform.SetParent(transform, false);
            go.AddComponent<SpriteRenderer>();
            return go.transform;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Shot s = _live[i];
                s.Elapsed += dt;

                // 착탄 효과는 탄환이 도착할 때까지 기다린다.
                if (s.Elapsed < s.Delay)
                {
                    _live[i] = s;
                    continue;
                }
                if (!s.Renderer.enabled) s.Renderer.enabled = true;

                float t = (s.Elapsed - s.Delay) / s.Duration;

                if (t >= 1f)
                {
                    Retire(i);
                    continue;
                }

                if (s.Stationary)
                {
                    // 제자리에서 옅어지며 사라진다. 착탄(범위 표시)은 뒤쪽 40% 에서만 사라지게
                    // 해서 처음 60% 동안은 또렷하게 읽히도록 한다.
                    Color c = s.Renderer.color;
                    c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                    s.Renderer.color = c;
                }
                else
                {
                    s.Tr.position = Vector3.Lerp(s.From, s.To, t);
                }

                // 여러 장짜리는 살아있는 시간 전체에 프레임을 고르게 펼친다.
                // 탄환이면 거리가 멀수록 천천히 부서지고, 어느 거리에서든 도착하는 순간이
                // 마지막(흩어지는) 프레임이 된다. 섬광이면 발사 순간에 한 바퀴 재생된다
                // (프레이야의 ProjectileBurst 5장이 이 경로를 탄다).
                if (s.Frames != null)
                {
                    int frame = Mathf.Clamp((int)(t * s.Frames.Length), 0, s.Frames.Length - 1);
                    if (s.Renderer.sprite != s.Frames[frame]) s.Renderer.sprite = s.Frames[frame];
                }

                _live[i] = s;
            }
        }

        /// <summary>
        /// 살아 있는 연출 하나를 <b>치우고 풀에 되돌린다</b>. 끝나는 길을 <b>한 곳</b>으로 모은
        /// 것이다 — 타이머 만료(<see cref="Update"/>)와 취소(<see cref="Cancel"/>)가 같은
        /// 정리를 지나야 «풀에 안 돌아간 오브젝트» 가 생기지 않는다.
        /// </summary>
        void Retire(int index)
        {
            Shot s = _live[index];
            if (s.Tr != null)
            {
                s.Tr.gameObject.SetActive(false);
                if (s.Renderer != null) s.Renderer.enabled = true;  // 다시 꺼내 쓸 때를 위해
                _pool.Push(s.Tr);
            }
            _live.RemoveAt(index);
        }

        /// <summary>
        /// ★★ <b>씬이 바뀌면 남아 있던 연출을 전부 치운다</b> (2026-08-21).
        ///
        /// <b>왜 필요한가</b> — 이 오브젝트는 <see cref="Object.DontDestroyOnLoad"/> 라
        /// <b>씬을 넘겨도 살아남는다</b>. 그래서 «패배 → 다시하기» 로 새 판을 열면
        /// <b>지난 판의 연출이 그대로 떠 있었다</b> — 아르세니아가 없는 새 판에
        /// 아르세니아의 마법진이 남아 있는 것이 정확히 이 경로다.
        ///
        /// ★ <c>DamageNumberFx</c>(99-6절)가 «폴백은 자기가 태어난 씬과 생애를 같이 해야
        ///   한다» 며 같은 문제를 고쳐 둔 것과 <b>같은 처리</b>다.
        /// </summary>
        void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => ClearAll();

        /// <summary>
        /// ★★ <b>지금 떠 있는 연출을 전부 치운다</b> (2026-08-21).
        ///
        /// <b>왜 필요한가</b> — 유저 리포트: *"아르세니아 이펙트가 중앙 건물 청크에 걸려서
        /// 장식물처럼 안없어져"*.
        ///
        /// <see cref="Update"/> 는 <c>Time.deltaTime</c> 으로 센다 — 배속을 걸면 연출도 같이
        /// 빨라져야 하므로 그것이 맞다. 그런데 <b>판이 끝나면 <c>timeScale</c> 이 0 으로
        /// 고정된다</b>(<c>DefeatPanel</c>·<c>VictoryPanel</c>). 그러면:
        ///
        ///   · 살아 있던 연출의 타이머가 <b>영원히 안 흐른다</b> → 화면에 <b>그대로 굳는다</b>
        ///   · 그것을 지워 줄 <c>SacredZone</c> 도 <c>Time.time</c> 으로 세므로 <b>같이 굳는다</b>
        ///     → <see cref="SacredZone.OnDestroy"/> 가 영영 안 돌고 취소도 안 된다
        ///
        /// 패배는 <b>넥서스가 부서질 때</b> 일어나므로, 굳은 그림은 정확히 «중앙 건물 자리» 에
        /// 남는다 — 유저가 본 그 «장식물» 이다.
        ///
        /// ★ <b>시간으로 우회하지 않았다</b>(<c>unscaledDeltaTime</c> 으로 바꾸는 것) —
        ///   그러면 <b>일시정지 중에도 연출만 계속 흐른다</b>. 멈춘 화면에서 탄환이 혼자
        ///   날아가는 것은 더 이상하다. 그래서 «판이 끝났다» 는 <b>사건</b>에 붙여
        ///   그때 치우는 쪽을 골랐다.
        /// </summary>
        public static void ClearAll()
        {
            if (_instance == null) return;
            for (int i = _instance._live.Count - 1; i >= 0; i--) _instance.Retire(i);
        }
    }
}
