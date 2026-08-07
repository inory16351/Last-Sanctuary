using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 건물(포탑) 스프라이트 애니메이션. 대기 / 공격 / 파괴 세 모션을 재생한다.
    /// 프레임 목록은 <see cref="TowerSkinSO"/> 가 들고 있고, 이 컴포넌트는 "지금 어떤 모션인지"만
    /// 판단해 <c>SpriteRenderer.sprite</c> 를 갈아끼운다 — <see cref="CharacterAnimator"/> 와 같은 구조다.
    ///
    /// <b>캐릭터 쪽과 다른 점 세 가지</b>
    /// <list type="number">
    /// <item>방향이 없다 — 고정 구조물이라 늘 같은 면을 보인다. <c>flipX</c> 도 쓰지 않는다.</item>
    /// <item>이동 모션이 없다 — 걷기 판정(좌표 변화 감지)이 아예 필요 없다.</item>
    /// <item><b>파괴 모션은 한 번만 재생하고 마지막 프레임에서 멈춘다.</b> 반복하면 폭발이
    ///       무한히 되풀이된다. 이 구간에서는 다른 모션이 끼어들지 못하게 잠근다.</item>
    /// </list>
    ///
    /// <b>Animator/AnimatorController 를 쓰지 않는 이유</b>는 <see cref="CharacterAnimator"/> 와 같다 —
    /// 컨트롤러·클립은 오브젝트 참조라 MCP 로 씬에 넣을 수 없고(진행상황 8절 4번), 이 프로젝트는
    /// 이미 코드가 상태를 알고 있는 구조라 그 상태를 읽어 프레임을 넘기는 편이 훨씬 단순하다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class TowerAnimator : MonoBehaviour
    {
        [Header("스킨")]
        [Tooltip("Resources 아래의 건물 스킨 폴더. 이 안의 TowerSkinSO 중 하나를 쓴다 — " +
                 "새 외형을 추가하려면 에셋을 이 폴더에 넣기만 하면 된다")]
        [SerializeField] string skinResourceFolder = "BuildingSkins";

        [Tooltip("비워두면 폴더의 첫 스킨(여러 개면 무작위)을 쓴다. 건물 종류가 늘어나면 " +
                 "이 이름으로 골라 쓸 수 있다 (에셋 이름 또는 displayName)")]
        [SerializeField] string skinName = "";

        [Header("판정")]
        [Tooltip("공격 모션을 최소 이 시간(초)은 유지한다. 공격 속도가 아주 빠를 때 모션이 " +
                 "첫 프레임에서 끊기는 것을 막는다 (CharacterAnimator 와 같은 규칙)")]
        [Min(0f)] [SerializeField] float minAttackHoldSeconds = 0.2f;

        [Header("디버그")]
        [SerializeField] bool logSkinChoice = false;

        /// <summary>Resources 조회는 비싸므로 폴더당 한 번만 읽는다(<see cref="CharacterAnimator"/> 와 동일).</summary>
        static readonly System.Collections.Generic.Dictionary<string, TowerSkinSO[]> _skinCache =
            new System.Collections.Generic.Dictionary<string, TowerSkinSO[]>();

        SpriteRenderer _sprite;
        UnitCombat _combat;

        TowerSkinSO _skin;
        Sprite[] _frames;
        float _frameClock;
        float _fps;
        float _attackUntil;

        bool _destroying;
        float _destroyClock;

        /// <summary>지금 쓰고 있는 스킨. 없으면 null(스프라이트를 건드리지 않는다).</summary>
        public TowerSkinSO Skin => _skin;

        /// <summary>파괴 연출 중인지. 이 동안에는 대기·공격 모션이 끼어들지 않는다.</summary>
        public bool IsDestroying => _destroying;

        void Awake()
        {
            _sprite = GetComponent<SpriteRenderer>();
            _combat = GetComponent<UnitCombat>();
        }

        void OnEnable()
        {
            // 템플릿을 복제해 활성화하는 순간이 곧 "건설 완료"다 — 그때 외형을 정한다.
            if (_skin == null) SetSkin(PickSkin());

            // 첫 프레임을 즉시 얹는다. Update 를 기다리면 한 프레임 동안 폴백 스프라이트
            // (BuildingDefinitionSO.spriteResourcePath) 가 보였다가 바뀐다.
            if (_skin != null && _skin.HasIdle && _sprite != null) _sprite.sprite = _skin.idle[0];

            if (_combat != null) _combat.OnAttackPerformed += HandleAttackPerformed;
        }

        void OnDisable()
        {
            if (_combat != null) _combat.OnAttackPerformed -= HandleAttackPerformed;
        }

        public void SetSkin(TowerSkinSO skin)
        {
            _skin = skin;
            _frames = null;
            _frameClock = 0f;

            if (_skin != null && logSkinChoice)
                Debug.Log($"[TowerAnim] {name} 외형 → " +
                          $"{(_skin.displayName != "" ? _skin.displayName : _skin.name)}", this);
        }

        /// <summary>
        /// 파괴 연출을 시작한다. 돌려주는 값은 <b>연출 길이(초)</b> 이므로 호출한 쪽이
        /// 그만큼 뒤에 오브젝트를 지우면 된다(<see cref="LastSanctuary.Buildings.TowerUnit"/>).
        /// 프레임이 없는 스킨이면 0 을 돌려주므로 즉시 지워도 안전하다.
        /// </summary>
        public float PlayDestroy()
        {
            if (_destroying) return Mathf.Max(0f, DestroySeconds() - _destroyClock);
            if (_skin == null || !_skin.HasDestroy) return 0f;

            _destroying = true;
            _destroyClock = 0f;
            _frames = _skin.destroy;
            _frameClock = 0f;
            _fps = _skin.destroyFramesPerSecond;
            if (_sprite != null) _sprite.sprite = _frames[0];
            return DestroySeconds();
        }

        float DestroySeconds() => _skin != null ? _skin.DestroyClipSeconds : 0f;

        void Update()
        {
            if (_skin == null || _sprite == null) return;

            if (_destroying)
            {
                // 파괴는 **한 번만** 재생하고 마지막 프레임에서 멈춘다 — 반복하면 폭발이 되풀이된다.
                _destroyClock += Time.deltaTime;
                int last = _frames.Length - 1;
                int idx = _fps > 0f ? Mathf.Min(last, (int)(_destroyClock * _fps)) : last;
                if (_frames[idx] != null) _sprite.sprite = _frames[idx];
                return;
            }

            Sprite[] wanted = Time.time < _attackUntil && _skin.HasAttack ? _skin.attack : _skin.idle;
            float fps = ReferenceEquals(wanted, _skin.attack)
                ? _skin.attackFramesPerSecond : _skin.framesPerSecond;
            if (wanted == null || wanted.Length == 0) return;

            // 모션이 바뀌면 첫 프레임부터 다시 시작한다 — 안 그러면 어중간한 프레임에서 이어진다.
            if (!ReferenceEquals(wanted, _frames))
            {
                _frames = wanted;
                _frameClock = 0f;
            }
            _fps = fps;

            _frameClock += Time.deltaTime;
            int index = _fps > 0f ? (int)(_frameClock * _fps) % _frames.Length : 0;

            Sprite next = _frames[index];
            if (next != null && _sprite.sprite != next) _sprite.sprite = next;
        }

        void HandleAttackPerformed()
        {
            if (_destroying) return;
            float clip = _skin != null ? _skin.AttackClipSeconds : 0f;
            _attackUntil = Time.time + Mathf.Max(minAttackHoldSeconds, clip);
        }

        // ------------------------------------------------------------------

        TowerSkinSO PickSkin()
        {
            TowerSkinSO[] skins = LoadSkins(skinResourceFolder);
            if (skins == null || skins.Length == 0) return null;

            if (!string.IsNullOrEmpty(skinName))
            {
                for (int i = 0; i < skins.Length; i++)
                    if (skins[i].name == skinName || skins[i].displayName == skinName)
                        return skins[i];

                Debug.LogWarning($"[TowerAnim] Resources/{skinResourceFolder} 에 '{skinName}' 스킨이 " +
                                 "없습니다. 폴더의 다른 스킨으로 대체합니다.", this);
            }

            return skins.Length == 1 ? skins[0] : skins[Random.Range(0, skins.Length)];
        }

        static TowerSkinSO[] LoadSkins(string folder)
        {
            if (_skinCache.TryGetValue(folder, out TowerSkinSO[] cached)) return cached;

            var loaded = Resources.LoadAll<TowerSkinSO>(folder);
            var usable = new System.Collections.Generic.List<TowerSkinSO>(loaded.Length);
            for (int i = 0; i < loaded.Length; i++)
                if (loaded[i] != null && loaded[i].IsUsable) usable.Add(loaded[i]);

            if (usable.Count == 0)
                Debug.LogWarning($"[TowerAnim] Resources/{folder} 에서 쓸 수 있는 TowerSkinSO 를 " +
                                 "찾지 못했습니다. 건물이 정지 스프라이트로 보입니다.");

            TowerSkinSO[] result = usable.ToArray();
            _skinCache[folder] = result;
            return result;
        }

        /// <summary>플레이 모드를 다시 시작할 때 캐시가 남지 않게 (도메인 리로드 off 대비).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => _skinCache.Clear();
    }
}
