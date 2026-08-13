using System.Collections.Generic;
using UnityEngine;

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

        /// <summary>
        /// 탄환이 <b>몸 중심이 아니라 앞쪽(입/총구)에서</b> 출발하도록 밀어내는 거리 —
        /// 스프라이트 가로 반지름에 이 비율을 곱한다. 0 이면 탄환이 몸 한가운데서 튀어나와
        /// "뱉는다"로 안 읽힌다. 목표까지의 거리가 짧으면 그 40%를 넘지 않게 잘라
        /// 붙어 있는 적을 지나쳐 생기지 않게 한다.
        /// </summary>
        const float MuzzleForwardRatio = 0.45f;

        /// <summary>시전 섬광이 머무는 시간(초).</summary>
        const float FlashSeconds = 0.12f;

        /// <summary>
        /// 착탄 효과가 머무는 시간(초). 섬광보다 길다 — <b>마법이면 이것이 피해 범위 표시</b>라
        /// 눈으로 읽을 시간이 필요하다. 뒤쪽 40% 구간에 옅어지며 사라진다.
        /// </summary>
        const float ImpactSeconds = 0.32f;

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
        }

        readonly List<Shot> _live = new List<Shot>();
        readonly Stack<Transform> _pool = new Stack<Transform>();

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

        void OnEnable() => DamageableUnit.OnAnyAttack += HandleAttack;
        void OnDisable() => DamageableUnit.OnAnyAttack -= HandleAttack;

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
            if (combat.AttackType != TacticalAttackType.Ranged &&
                combat.AttackType != TacticalAttackType.Magic) return;

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
        public static void PlayArea(Sprite[] frames, Vector3 center, Vector2 sizeTiles,
                                    float angleDeg, DamageableUnit anchor, float seconds)
        {
            if (_instance == null || !HasFrames(frames)) return;

            Sprite first = frames[0];
            if (first == null) return;

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

            _instance.Spawn(at, at, Mathf.Max(0.05f, seconds), anchor, first, scale,
                            frames: frames.Length > 1 ? frames : null,
                            stationary: true, rotation: rotation);
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
                    Impact = charAnim.Skin.impactFrames,
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

        /// <summary>유닛의 몸통 중심. 발밑 피벗이라 <c>transform.position</c> 은 바닥이다.</summary>
        static Vector3 CenterOf(DamageableUnit unit)
        {
            var sr = unit.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null) return sr.bounds.center;
            return unit.transform.position;
        }

        /// <summary>
        /// 발사 지점을 몸 중심에서 목표 쪽으로 밀어낼 양. 스프라이트 가로 반지름 기준이라
        /// 유닛 크기에 자동으로 맞는다. 목표가 코앞이면 그 40%까지만 밀어 탄환이 적을
        /// 지나쳐서 생기지 않게 한다.
        /// </summary>
        static Vector3 MuzzleOffset(DamageableUnit shooter, Vector3 dir, float dist)
        {
            var sr = shooter.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return Vector3.zero;

            float forward = Mathf.Min(sr.bounds.extents.x * MuzzleForwardRatio, dist * 0.4f);
            return dir * forward;
        }

        /// <summary>
        /// 연출 하나를 띄운다. <paramref name="anchor"/> 는 정렬 기준이 될 유닛이다 —
        /// 탄환·발사 섬광은 <b>쏘는 쪽</b>, 착탄 효과는 <b>맞는 쪽</b>을 넘긴다.
        /// 그래야 맞는 유닛 위에 범위 표시가 덮이고 유닛 몸에 가려지지 않는다.
        /// </summary>
        void Spawn(Vector3 from, Vector3 to, float duration, DamageableUnit anchor,
                   Sprite sprite, Vector2 scale, Sprite[] frames = null,
                   bool stationary = false, float delay = 0f, Quaternion? rotation = null)
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

            tr.position = from;
            tr.localScale = new Vector3(scale.x, scale.y, 1f);
            tr.rotation = rotation ?? Quaternion.identity;
            tr.gameObject.SetActive(true);

            _live.Add(new Shot
            {
                Tr = tr, Renderer = sr, From = from, To = to,
                Elapsed = 0f, Duration = Mathf.Max(0.01f, duration),
                Stationary = stationary, Delay = Mathf.Max(0f, delay),
                Frames = frames,
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
                    s.Tr.gameObject.SetActive(false);
                    s.Renderer.enabled = true;      // 풀에서 다시 꺼내 쓸 때를 위해 되돌린다
                    _pool.Push(s.Tr);
                    _live.RemoveAt(i);
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
    }
}
