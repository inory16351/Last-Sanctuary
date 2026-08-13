using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 캐릭터 스프라이트 애니메이션. 대기 / 이동 / 공격(근접·원거리·회복) 모션을 방향별(좌·우)
    /// 프레임으로 재생한다. 프레임 목록은 <see cref="CharacterSkinSO"/> 가 들고 있고, 이 컴포넌트는
    /// "지금 어떤 모션·어느 방향인지"만 판단해서 <c>SpriteRenderer.sprite</c> 를 갈아끼운다.
    ///
    /// <b>왜 Animator/AnimatorController 를 안 쓰는가</b> — 컨트롤러·클립은 오브젝트 참조라
    /// MCP 로 씬에 넣을 수 없고(진행상황 8절 4번), 스킨마다 컨트롤러를 하나씩 손으로 만들어야
    /// 한다. 이 프로젝트는 이미 "코드가 상태를 알고 있는" 구조(<see cref="UnitCombat"/> FSM)라,
    /// 그 상태를 그대로 읽어 프레임을 넘기는 편이 훨씬 단순하고 연결할 참조가 하나도 없다.
    ///
    /// <b>방향</b>: 좌우 프레임이 따로 있으므로 <c>flipX</c> 를 쓰지 않는다 —
    /// <see cref="UnitCombat"/> 의 <c>flipSpriteToFaceMovement</c> 는 꺼두어야 한다
    /// (켜져 있으면 좌향 스프라이트를 한 번 더 뒤집어 오른쪽을 보게 만든다).
    ///
    /// <b>스킨 선택</b>: 시작할 때 <see cref="skinResourceFolder"/> 안의 스킨 중 하나를
    /// <b>무작위로</b> 고른다(유저 확정: 프로토타입 단계에서는 랜덤, 나중에 캐릭터별 테이블을
    /// 파싱하게 되면 <see cref="SetSkin"/> 으로 지정만 하면 된다).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CharacterAnimator : MonoBehaviour
    {
        [Header("스킨")]
        [Tooltip("Resources 아래의 스킨 폴더. 이 안의 CharacterSkinSO 전부가 후보가 된다 — " +
                 "새 외형을 추가하려면 에셋을 이 폴더에 넣기만 하면 된다")]
        [SerializeField] string skinResourceFolder = "Skins";

        [Header("판정")]
        [Tooltip("한 프레임에 이 거리(월드 유닛) 이상 움직였으면 '이동 중'으로 본다. " +
                 "너무 작으면 밀림(separation)만으로도 걷는 모션이 나온다")]
        [Min(0f)] [SerializeField] float moveThreshold = 0.004f;

        [Tooltip("공격 모션을 최소 이 시간(초)은 유지한다. 스킨의 프레임 수 ÷ 재생 속도가 " +
                 "이보다 길면 그 길이를 쓴다 — 공격 속도가 아주 빠를 때 모션이 첫 프레임에서 " +
                 "끊기는 것을 막는다")]
        [Min(0f)] [SerializeField] float minAttackHoldSeconds = 0.18f;

        [Header("디버그")]
        [Tooltip("어떤 스킨이 뽑혔는지 콘솔에 남긴다")]
        [SerializeField] bool logSkinChoice = false;

        /// <summary>
        /// Resources 조회는 비싸므로 폴더당 한 번만 읽는다.
        ///
        /// <b>폴더별로 따로 캐시한다</b> — 캐릭터(<c>Skins</c>)와 몬스터(<c>MonsterSkins</c>)가
        /// 서로 다른 폴더를 쓰는데, "마지막 폴더 하나"만 기억하면 캐릭터·몬스터가 번갈아
        /// 스폰될 때마다 <c>Resources.LoadAll</c> 이 다시 도는 캐시 스래싱이 생긴다.
        /// </summary>
        static readonly System.Collections.Generic.Dictionary<string, CharacterSkinSO[]> _skinCache =
            new System.Collections.Generic.Dictionary<string, CharacterSkinSO[]>();

        SpriteRenderer _sprite;
        UnitCombat _combat;
        DamageableUnit _self;

        CharacterSkinSO _skin;
        Sprite[] _frames;
        float _frameClock;
        float _fps;

        bool _facingRight = true;
        Vector3 _lastPosition;
        float _attackUntil;

        /// <summary>지금 쓰고 있는 스킨. 없으면 null(스프라이트를 건드리지 않는다).</summary>
        public CharacterSkinSO Skin => _skin;

        void Awake()
        {
            _sprite = GetComponent<SpriteRenderer>();
            _combat = GetComponent<UnitCombat>();
            _self = GetComponent<DamageableUnit>();
            _lastPosition = transform.position;
        }

        void OnEnable()
        {
            // 스포너가 템플릿을 복제해 활성화하는 순간이 곧 "생성"이다 — 그때 외형을 뽑는다.
            if (_skin == null) SetSkin(PickRandomSkin());
            if (_combat != null) _combat.OnAttackPerformed += HandleAttackPerformed;
        }

        void OnDisable()
        {
            if (_combat != null) _combat.OnAttackPerformed -= HandleAttackPerformed;
        }

        /// <summary>외형을 지정한다. 나중에 캐릭터 테이블을 파싱하게 되면 여기로 넣으면 된다.</summary>
        public void SetSkin(CharacterSkinSO skin)
        {
            _skin = skin;
            _frames = null;
            _frameClock = 0f;

            ApplyRenderSize();

            if (_skin != null && logSkinChoice)
                Debug.Log($"[Anim] {name} 외형 → {(_skin.displayName != "" ? _skin.displayName : _skin.name)}", this);
        }

        // ------------------------------------------------------------------
        // 크기 — <b>콜라이더 상자(타일)를 받아 그 안에 그림을 맞춘다</b> (유저 확정 2026-08-13)
        //
        // <b>흐름</b>: 표에 콜라이더 크기를 대충 적는다(2.5 x 1.9 처럼 한 자리 소수)
        //   → 그 상자 <b>안에 들어가는 최대 배율</b>을 비율 유지로 계산한다
        //   → 그림을 그 크기로 그린다
        //   → <b>콜라이더를 그 그림 크기로 다시 맞춘다</b>(<see cref="ColliderSizeTiles"/>)
        // 그래서 최종적으로 <b>보이는 몸집 = 판정 몸집</b>이 되고, 표에는 사람이 외우기 쉬운
        // 값만 적으면 된다. 한 축은 표 값과 정확히 같고, 다른 축은 비율 때문에 조금 작아진다.
        //
        // <b>왜 "안에 들어가게"(contain) 인가</b> — 상자를 넘치게(cover) 맞추면 그림이 판정
        // 밖으로 삐져나가 "허공을 때리는" 것처럼 보인다. 유저 지시도 "해당 콜라이더 범위
        // 내에서 최대한 유사한 크기"다.
        //
        // <b>왜 균등 배율인가</b> — 가로·세로를 따로 늘리면 원화가 찌그러진다. 60절에서 실제로
        // 그렇게 해서 보스가 납작해졌고, 61절이 그걸 고쳤다. 배율은 언제나 한 값이다.
        //
        // 실측값(<see cref="CharacterSkinSO.contentSizeTiles"/>)은 원화의 알파 경계를 잰 것이라
        // PPU·캔버스 여백과 무관하다 — 그래서 원화를 바꿔도 게임 안 크기가 안 흔들린다.
        // ------------------------------------------------------------------

        [Header("크기 (콜라이더 상자, 타일)")]
        [Tooltip("콜라이더 <b>가로</b>(타일). 세로와 함께 0 보다 크면 이 상자 안에 그림을 맞춘다.\n" +
                 "몬스터는 스폰할 때 정의 테이블(colliderWidthTiles)이 덮어쓴다")]
        [Min(0f)] [SerializeField] float colliderWidthTiles = 0f;

        [Tooltip("콜라이더 <b>세로</b>(타일). 가로와 함께 0 보다 크면 이 상자 안에 그림을 맞춘다")]
        [Min(0f)] [SerializeField] float colliderHeightTiles = 0f;

        [Tooltip("⚠ 콜라이더 상자를 안 쓸 때의 <b>세로 전용</b> 폴백(타일). 가로는 원화 비율대로 " +
                 "따라온다. 캐릭터가 이 경로를 쓴다(전원 같은 키라 상자가 필요 없다).\n" +
                 "0 이면 크기를 건드리지 않는다(원화 PPU 그대로)")]
        [Min(0f)] [SerializeField] float renderHeightTiles = 0f;

        /// <summary>지금 목표로 삼은 세로 크기(타일). 0 이면 크기 보정을 하지 않는다.</summary>
        public float RenderHeightTiles => renderHeightTiles;

        /// <summary>표에서 받은 콜라이더 <b>희망</b> 크기(타일). 실제 판정 크기는 <see cref="ColliderSizeTiles"/>.</summary>
        public Vector2 RequestedColliderTiles => new Vector2(colliderWidthTiles, colliderHeightTiles);

        /// <summary>
        /// 화면에 실제로 보이는 크기(타일). 근접 거리·선택 판정이 이 값을 읽는다 —
        /// 그래야 "보이는 몸집"과 "때릴 수 있는 거리"가 어긋나지 않는다.
        /// 크기 보정을 안 하는 경우에는 스킨 실측값을 그대로 돌려준다.
        /// </summary>
        public Vector2 RenderedSizeTiles
        {
            get
            {
                if (_skin == null) return Vector2.zero;
                float s = ResolveScale();
                return s > 0f ? _skin.contentSizeTiles * s : _skin.contentSizeTiles;
            }
        }

        /// <summary>
        /// <b>그림에 다시 맞춘 콜라이더 크기(타일).</b> 표에 적은 희망 크기가 아니라
        /// <b>실제로 그려진 크기</b>다 — 유저 지시의 마지막 단계("해당 값에 맞춰 콜라이더 재설정").
        /// 지금 구조에서 이 값을 읽는 곳이 곧 콜라이더 역할을 한다
        /// (<see cref="LastSanctuary.Units.MonsterUnit.BodyRadiusTiles"/> → <c>UnitCombat.TargetRadius</c>).
        /// </summary>
        public Vector2 ColliderSizeTiles => RenderedSizeTiles;

        /// <summary>
        /// 콜라이더 상자(타일)를 지정한다. 스포너가 정의 테이블 값을 넣는다 —
        /// 같은 템플릿·같은 스킨을 쓰는 중간보스가 잡몹보다 크게 나오는 것도 이 한 줄로 된다.
        /// </summary>
        public void SetColliderBoxTiles(float widthTiles, float heightTiles)
        {
            colliderWidthTiles = Mathf.Max(0f, widthTiles);
            colliderHeightTiles = Mathf.Max(0f, heightTiles);
            ApplyRenderSize();
        }

        /// <summary>세로 전용 폴백 경로(캐릭터용). 콜라이더 상자를 쓰면 이 값은 무시된다.</summary>
        public void SetRenderHeightTiles(float tiles)
        {
            renderHeightTiles = Mathf.Max(0f, tiles);
            ApplyRenderSize();
        }

        /// <summary>
        /// 지금 걸어야 할 <b>균등</b> 배율. 0 이면 "크기를 건드리지 않는다".
        /// 콜라이더 상자가 있으면 <b>그 안에 들어가는 최대 배율</b>(contain), 없으면 세로 폴백.
        /// </summary>
        float ResolveScale()
        {
            if (_skin == null) return 0f;

            Vector2 art = _skin.contentSizeTiles;
            if (art.x <= 0.0001f || art.y <= 0.0001f) return 0f;

            if (colliderWidthTiles > 0f && colliderHeightTiles > 0f)
                return Mathf.Min(colliderWidthTiles / art.x, colliderHeightTiles / art.y);

            return renderHeightTiles > 0f ? renderHeightTiles / art.y : 0f;
        }

        /// <summary>계산한 배율을 <b>균등</b>하게 건다(비율이 절대 안 깨진다).</summary>
        void ApplyRenderSize()
        {
            if (_skin == null) return;
            if (colliderWidthTiles <= 0f && colliderHeightTiles <= 0f && renderHeightTiles <= 0f) return;

            if (_skin.contentSizeTiles.x <= 0.0001f || _skin.contentSizeTiles.y <= 0.0001f)
            {
                // 실측값이 없는 스킨 — 배율을 계산할 수 없으므로 크기를 건드리지 않는다.
                // (스케일 0 이 되어 유닛이 사라지는 것보다 원래 크기로 두는 편이 안전하다)
                Debug.LogWarning($"[Anim] {_skin.name} 에 실측 크기(contentSizeTiles)가 없습니다 — " +
                                 "Tools/measure_skin_tiles.py 를 돌려주세요. 크기 보정을 건너뜁니다.", this);
                return;
            }

            float s = ResolveScale();
            if (s > 0f) transform.localScale = new Vector3(s, s, 1f);
        }

        void Update()
        {
            if (_skin == null || _sprite == null) return;

            // 이번 프레임에 얼마나 움직였는지는 방향 판정과 모션 판정이 <b>같은 값</b>을 써야 한다.
            // 예전에는 공격 중에 _lastPosition 이 갱신되지 않아, 공격이 끝난 첫 프레임의
            // 이동량이 공격 시간 전체의 이동량으로 잡혔다.
            Vector2 delta = transform.position - _lastPosition;
            _lastPosition = transform.position;

            UpdateFacing(delta);

            Sprite[] wanted = ResolveFrames(delta.magnitude, out float fps);
            if (wanted == null || wanted.Length == 0) return;

            // 모션이 바뀌면 첫 프레임부터 다시 시작한다 — 안 그러면 공격 모션이 걷기 프레임
            // 인덱스에서 이어져 어중간한 자세로 튄다.
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

        /// <summary>
        /// 지금 재생할 프레임 목록. 우선순위는 <b>공격 → 이동 → 대기</b> 다.
        /// 공격이 가장 앞인 이유: 공격 중에도 밀림(separation)으로 좌표가 조금씩 흔들려서,
        /// 이동 판정을 먼저 보면 때리는 동안 걷는 모션이 섞인다.
        /// </summary>
        Sprite[] ResolveFrames(float moved, out float fps)
        {
            if (Time.time < _attackUntil)
            {
                fps = _skin.attackFramesPerSecond;
                return _skin.Attack(_facingRight, CurrentAttackMotion());
            }

            fps = _skin.framesPerSecond;
            return moved > moveThreshold ? _skin.Walk(_facingRight) : _skin.Idle(_facingRight);
        }

        /// <summary>
        /// 지금 전술이 요구하는 공격 계열 모션.
        /// <b>회복은 전용 모션을 먼저 찾는다</b> — 없으면 <see cref="CharacterSkinSO.Attack"/> 안에서
        /// 원거리 → 근접으로 대체된다(유저 지시: 회복 원화가 없으면 공격 모션 사용).
        /// 마법은 예전처럼 원거리 모션을 같이 쓴다 — 둘 다 떨어져서 시전하는 동작이다.
        /// </summary>
        SkinAttackMotion CurrentAttackMotion()
        {
            if (_combat == null) return SkinAttackMotion.Melee;
            switch (_combat.AttackType)
            {
                case TacticalAttackType.Heal: return SkinAttackMotion.Heal;
                case TacticalAttackType.Melee: return SkinAttackMotion.Melee;
                default: return SkinAttackMotion.Ranged;
            }
        }

        /// <summary>
        /// 바라보는 방향.
        ///
        /// ★ <b>때리는 순간에만 타겟(=투사체가 날아갈 방향)을 보고, 그 외에는 진행 방향을 본다</b>
        /// (유저 지시 2026-08-11: "이동할때는 이동 방향을 바라보고 공격 할때는 투사체 방향을
        /// 바라보게 ... 지금처럼 뒷걸음질 치지 말고 롤에서 카이팅 할때처럼").
        ///
        /// <b>예전에는 타겟이 있으면 언제나 타겟을 봤다.</b> 그래서 물러나면서 싸울 때
        /// 적을 마주 본 채 뒤로 걸어가는 <b>뒷걸음질</b>이 됐다. 이제는 물러나는 동안 등을
        /// 보이고 걷다가, 공격이 나가는 순간(<see cref="_attackUntil"/> 유지 시간)만 홱 돌아
        /// 쏘고 다시 진행 방향으로 돌아온다.
        ///
        /// 좌우 성분이 거의 없을 때는 마지막 방향을 유지한다 — 위아래로만 움직일 때
        /// 좌우가 덜덜 떨리는 것을 막는다(<see cref="UnitCombat.FaceMovement"/> 와 같은 규칙).
        /// </summary>
        void UpdateFacing(Vector2 delta)
        {
            float dx;

            DamageableUnit target = _combat != null ? _combat.Target : null;
            bool attacking = Time.time < _attackUntil && target != null && target.IsAlive;

            if (attacking)
                dx = target.transform.position.x - transform.position.x;
            else if (delta.sqrMagnitude > moveThreshold * moveThreshold)
                dx = delta.x;
            else if (target != null && target.IsAlive)
                // 멈춰 서 있고 공격 모션도 아니면 적을 마주 본다 — 대치 중에 등을 보이면 어색하다.
                dx = target.transform.position.x - transform.position.x;
            else
                return;

            if (Mathf.Abs(dx) < 0.001f) return;
            _facingRight = dx > 0f;
        }

        void HandleAttackPerformed()
        {
            float clip = _skin != null ? _skin.AttackClipSeconds(_facingRight, CurrentAttackMotion()) : 0f;
            _attackUntil = Time.time + Mathf.Max(minAttackHoldSeconds, clip);
        }

        // ------------------------------------------------------------------

        CharacterSkinSO PickRandomSkin()
        {
            CharacterSkinSO[] skins = LoadSkins(skinResourceFolder);
            if (skins == null || skins.Length == 0) return null;

            // 후보가 하나뿐이면 굳이 굴리지 않는다.
            return skins.Length == 1 ? skins[0] : skins[Random.Range(0, skins.Length)];
        }

        static CharacterSkinSO[] LoadSkins(string folder)
        {
            if (_skinCache.TryGetValue(folder, out CharacterSkinSO[] cached)) return cached;

            var loaded = Resources.LoadAll<CharacterSkinSO>(folder);
            var usable = new System.Collections.Generic.List<CharacterSkinSO>(loaded.Length);
            for (int i = 0; i < loaded.Length; i++)
                if (loaded[i] != null && loaded[i].IsUsable) usable.Add(loaded[i]);

            if (usable.Count == 0)
                Debug.LogWarning($"[Anim] Resources/{folder} 에서 쓸 수 있는 CharacterSkinSO 를 " +
                                 "찾지 못했습니다. 유닛이 스프라이트 없이 보일 수 있습니다.");

            CharacterSkinSO[] result = usable.ToArray();
            _skinCache[folder] = result;
            return result;
        }

        /// <summary>플레이 모드를 다시 시작할 때 캐시가 남지 않게 (도메인 리로드 off 대비).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => _skinCache.Clear();
    }
}
