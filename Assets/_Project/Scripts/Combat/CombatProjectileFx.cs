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
    /// <b>그림 출처</b> — Angel 원거리 공격 시트의 마지막 컷은 캐릭터가 없고 탄환만 그려진
    /// 프레임이었다(그대로 재생하면 시전 중에 캐릭터가 사라졌다). 그 컷에서 탄환과 섬광을
    /// 오려내 <c>Resources/Fx</c> 로 옮긴 것이 여기서 쓰는 스프라이트다.
    /// </summary>
    public class CombatProjectileFx : MonoBehaviour
    {
        const string BoltResourcePath = "Fx/Projectile_Bolt";
        const string FlashResourcePath = "Fx/Projectile_Flash";

        /// <summary>암세포(웨이브 몬스터) 진영 전용 탄환. 같은 그림을 색만 바꿔 만든 것이라
        /// 형태(+X 를 향하는 길쭉한 탄환)가 같고 <see cref="AimAt"/> 회전 로직이 그대로 통한다.</summary>
        const string BoltCancerResourcePath = "Fx/Projectile_Bolt_Cancer";
        const string FlashCancerResourcePath = "Fx/Projectile_Flash_Cancer";

        /// <summary>탄환 속도(월드 유닛/초). 사거리 5타일을 0.2초쯤에 지나가는 값.</summary>
        const float Speed = 26f;

        /// <summary>가장 오래 날아도 이 시간(초)이면 사라진다 — 목표가 죽어 사라져도 남지 않게.</summary>
        const float MaxLifetime = 0.6f;

        /// <summary>원화가 큰 편이라(74px ≈ 1.5유닛) 줄여서 쓴다.</summary>
        const float BoltScale = 0.55f;

        /// <summary>시전 섬광이 머무는 시간(초).</summary>
        const float FlashSeconds = 0.12f;

        static CombatProjectileFx _instance;

        Sprite _bolt;
        Sprite _flash;
        Sprite _boltCancer;
        Sprite _flashCancer;

        struct Shot
        {
            public Transform Tr;
            public SpriteRenderer Renderer;
            public Vector3 From;
            public Vector3 To;
            public float Elapsed;
            public float Duration;
            public bool IsFlash;
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

            if (_bolt == null)
                Debug.LogWarning($"[Fx] Resources/{BoltResourcePath} 를 찾지 못했습니다. " +
                                 "원거리 공격에 탄환이 보이지 않습니다.");
        }

        void OnEnable() => DamageableUnit.OnAnyAttack += HandleAttack;
        void OnDisable() => DamageableUnit.OnAnyAttack -= HandleAttack;

        /// <summary>근거리는 건너뛰고 원거리·마법만 탄환을 띄운다.</summary>
        void HandleAttack(DamageableUnit attacker, DamageableUnit target)
        {
            if (_bolt == null || attacker == null || target == null) return;

            var combat = attacker.GetComponent<UnitCombat>();
            if (combat == null) return;
            if (combat.AttackType != TacticalAttackType.Ranged &&
                combat.AttackType != TacticalAttackType.Magic) return;

            Vector3 from = CenterOf(attacker);
            Vector3 to = CenterOf(target);
            float dist = ((Vector2)(to - from)).magnitude;
            if (dist < 0.01f) return;

            // 진영별로 다른 색의 탄환을 쓴다 — 난전 중에 누가 쏜 것인지 구분되게.
            bool cancer = attacker.Faction == Faction.Cancer;
            Sprite bolt = cancer && _boltCancer != null ? _boltCancer : _bolt;
            Sprite flash = cancer && _flashCancer != null ? _flashCancer : _flash;

            Spawn(from, to, Mathf.Min(MaxLifetime, dist / Speed), attacker, bolt);
            if (flash != null) Spawn(from, from, FlashSeconds, attacker, flash, isFlash: true);
        }

        /// <summary>유닛의 몸통 중심. 발밑 피벗이라 <c>transform.position</c> 은 바닥이다.</summary>
        static Vector3 CenterOf(DamageableUnit unit)
        {
            var sr = unit.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null) return sr.bounds.center;
            return unit.transform.position;
        }

        void Spawn(Vector3 from, Vector3 to, float duration, DamageableUnit shooter,
                   Sprite sprite, bool isFlash = false)
        {
            Transform tr = _pool.Count > 0 ? _pool.Pop() : NewProjectile();
            var sr = tr.GetComponent<SpriteRenderer>();

            sr.sprite = sprite;
            // 섬광은 알파를 깎으며 사라지므로, 풀에서 다시 꺼내 쓸 때 되돌려놔야 한다.
            sr.color = Color.white;

            // 유닛보다 위에 그려야 탄환이 몸에 가려지지 않는다.
            var shooterSr = shooter.GetComponent<SpriteRenderer>();
            if (shooterSr != null)
            {
                sr.sortingLayerID = shooterSr.sortingLayerID;
                sr.sortingOrder = shooterSr.sortingOrder + 20;
            }

            tr.position = from;
            tr.localScale = Vector3.one * BoltScale;
            tr.rotation = isFlash ? Quaternion.identity : AimAt(to - from);
            tr.gameObject.SetActive(true);

            _live.Add(new Shot
            {
                Tr = tr, Renderer = sr, From = from, To = to,
                Elapsed = 0f, Duration = Mathf.Max(0.01f, duration), IsFlash = isFlash,
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
                float t = s.Elapsed / s.Duration;

                if (t >= 1f)
                {
                    s.Tr.gameObject.SetActive(false);
                    _pool.Push(s.Tr);
                    _live.RemoveAt(i);
                    continue;
                }

                if (s.IsFlash)
                {
                    // 섬광은 제자리에서 옅어지며 사라진다.
                    Color c = s.Renderer.color;
                    c.a = 1f - t;
                    s.Renderer.color = c;
                }
                else
                {
                    s.Tr.position = Vector3.Lerp(s.From, s.To, t);
                }

                _live[i] = s;
            }
        }
    }
}
