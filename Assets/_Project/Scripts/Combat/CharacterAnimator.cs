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

        [Tooltip("★ <b>이동 모션을 최소 이 시간(초)은 유지한다</b> (2026-08-13 신설).\n" +
                 "유저 리포트: \"보스 스킨 이동 모션이랑 대기 모션 자꾸 겹쳐서 나와서 어색하다\".\n" +
                 "원인은 <b>이동 판정이 한 프레임 이동량 하나로 결정</b>된다는 것이다 — 이동속도가 " +
                 "느린 유닛(단탈리온 1.4타일/초)은 프레임당 이동량이 임계값 근처라, 프레임률이 " +
                 "조금만 흔들려도 걷기↔대기가 <b>매 프레임 번갈아</b> 나온다. 두 모션이 섞여 " +
                 "보이는 것의 실체가 이것이다.\n" +
                 "한 번 '이동 중'이 되면 이 시간만큼은 걷기를 유지해 그 떨림을 없앤다. 0 이면 " +
                 "예전처럼 프레임 단위로 즉시 전환한다")]
        [Min(0f)] [SerializeField] float moveMotionHoldSeconds = 0.2f;

        [Tooltip("★ <b>바라보는 방향을 정할 때 진행 방향을 이 시간(초)만큼 평균낸다</b> " +
                 "(2026-08-19 신설).\n" +
                 "유저 리포트: \"이동할때 바라보는 방향 좀 더 정확하게\".\n" +
                 "원인은 <b>한 프레임 이동량 하나로 방향을 정했다</b>는 것이다 — 그 값에는 " +
                 "실제 진행 말고도 유닛끼리 밀어내는 힘(separation)·경로 재계산·발판 보정이 " +
                 "다 섞여 있어서, 특히 <b>위아래로 걸을 때</b> 좌우 부호가 프레임마다 뒤집혔다. " +
                 "최근 이동을 평균내면 그 잡음이 사라지고 <b>실제로 가고 있는 쪽</b>이 남는다.\n" +
                 "0 이면 예전처럼 한 프레임 값을 그대로 쓴다. 너무 키우면(0.5+) 방향을 바꾼 뒤 " +
                 "한참 뒤에 돌아본다")]
        [Min(0f)] [SerializeField] float facingSmoothSeconds = 0.15f;

        [Tooltip("★ <b>좌우 성분이 진행 방향의 이 비율은 돼야 방향을 바꾼다</b> (2026-08-19 신설).\n" +
                 "0.35 는 <b>수직에서 약 20도</b> 기울어야 돌아본다는 뜻이다. 거의 위아래로만 " +
                 "가는 동안에는 마지막 방향을 그대로 들고 있으므로 좌우가 덜덜 떨리지 않는다.\n" +
                 "0 이면 아주 작은 좌우 성분에도 즉시 돌아본다(예전 동작)")]
        [Range(0f, 0.9f)] [SerializeField] float facingTurnRatio = 0.35f;

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

        /// <summary>
        /// 다듬은 진행 방향(초당 이동량). <see cref="facingSmoothSeconds"/> 참조 —
        /// 한 프레임 이동량에 섞인 밀림·경로 재계산 잡음을 걸러낸 "실제로 가는 쪽"이다.
        /// </summary>
        Vector2 _heading;

        /// <summary>이 시각까지는 걷기 모션을 유지한다 — <see cref="moveMotionHoldSeconds"/> 참조.</summary>
        float _walkUntil;

        /// <summary>시전 중인 보스 스킬 슬롯. -1 이면 시전 중이 아니다.</summary>
        int _skillSlot = -1;
        float _skillUntil;

        /// <summary>지금 보스 스킬 시전 모션을 재생 중인지.</summary>
        bool InSkillMotion => _skillSlot >= 0 && Time.time < _skillUntil;

        /// <summary>부활 모션이 끝나는 시각. 「분노」(히스톤 80014)의 경직 구간이다.</summary>
        float _reviveUntil;

        /// <summary>지금 부활 모션을 재생 중인지.</summary>
        bool InReviveMotion => Time.time < _reviveUntil;

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

            // 켜질 때는 대기부터 — 이전 생애의 걷기 유지 시간이 남아 있으면 소환 직후
            // 제자리에서 걷는 것처럼 보인다.
            _walkUntil = 0f;
            _lastPosition = transform.position;
            // 이전 생애의 진행 방향이 남아 있으면 소환되자마자 엉뚱한 쪽을 본다.
            _heading = Vector2.zero;
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
        /// 지금 <b>오른쪽</b>을 보고 있는지 (2026-08-20 공개).
        ///
        /// <b>왜 공개하나</b> — 베일 「담뱃대 강타」의 밀쳐내기가 정의문에서
        /// *"캐릭터는 <b>자신이 바라보는 반대 방향</b>으로 밀려납니다"* 라고 못박고 있어서,
        /// 밀어내는 쪽이 «맞는 유닛이 어디를 보고 있는지» 를 알아야 한다. 다른 넉백
        /// (카르시노스 「죽음의 포효」)은 <b>시전자 반대쪽</b>이라 이 값이 필요 없었다.
        ///
        /// ⚠ 이 값은 <b>연출용 방향</b>이다 — 이동 방향과 <b>다를 수 있다</b>(멈춰 서서 적을
        ///   마주 볼 때 · 위아래로만 움직일 때는 마지막 좌우 방향을 유지한다).
        ///   정의문이 말하는 «바라보는 방향» 이 정확히 이것이다.
        /// </summary>
        public bool FacingRight => _facingRight;

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

            // ★ 방향 판정용 <b>진행 방향</b>을 여기서 다듬는다 (facingSmoothSeconds 참조).
            //   프레임률이 흔들려도 결과가 같도록 <b>이동량이 아니라 속도</b>(초당)를 평균낸다 —
            //   같은 속도로 걸어도 프레임률이 두 배면 프레임당 이동량은 절반이기 때문이다.
            float dt = Time.deltaTime;
            Vector2 velocity = dt > 0f ? delta / dt : Vector2.zero;
            _heading = facingSmoothSeconds > 0f
                ? Vector2.Lerp(_heading, velocity, Mathf.Clamp01(dt / facingSmoothSeconds))
                : velocity;

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
            // ★ 이동 판정을 <b>먼저</b> 갱신한다 — 공격·시전 중에도 위치는 계속 바뀌므로,
            //   여기서 안 재면 공격이 끝난 첫 프레임에 걷기 유지 시간이 0 인 채로 시작해
            //   "공격 → 대기 → 걷기" 로 한 프레임 튄다.
            if (moved > moveThreshold) _walkUntil = Time.time + moveMotionHoldSeconds;

            // ★ 부활이 <b>가장 앞</b>이다 — 이 구간에서 캐릭터는 체력 0(사망 상태)이라
            //   AI 가 멈춰 있고, 그대로 두면 대기 모션으로 <b>멀쩡히 서 있는 시체</b>가 된다.
            //   전용 원화가 없는 캐릭터는 폴백 없이 그냥 건너뛴다(부활은 히스톤 전용이다).
            if (InReviveMotion)
            {
                Sprite[] revive = _skin.Revive(_facingRight);
                if (revive != null && revive.Length > 0)
                {
                    fps = _skin.attackFramesPerSecond;
                    return revive;
                }
                _reviveUntil = 0f;
            }

            // 보스 스킬 시전이 가장 앞이다 — 시전 중에 평타·걷기 모션이 섞이면
            // "뭔가 큰 걸 쓰고 있다"는 신호가 사라진다.
            if (InSkillMotion)
            {
                Sprite[] skill = _skin.SkillMotion(_skillSlot, _facingRight);
                if (skill != null && skill.Length > 0)
                {
                    fps = _skin.attackFramesPerSecond;
                    return skill;
                }
                _skillSlot = -1;   // 방향을 바꾸는 사이 프레임이 사라졌다 — 평타로 떨어진다
            }

            if (Time.time < _attackUntil)
            {
                fps = _skin.attackFramesPerSecond;
                return _skin.Attack(_facingRight, CurrentAttackMotion());
            }

            fps = _skin.framesPerSecond;

            // 한 프레임 이동량을 그대로 보지 않고 <b>유지 시간</b>을 본다 —
            // 걷기와 대기가 매 프레임 번갈아 나오던 떨림이 이 한 줄로 사라진다.
            return Time.time < _walkUntil ? _skin.Walk(_facingRight) : _skin.Idle(_facingRight);
        }

        /// <summary>
        /// 지금 전술이 요구하는 공격 계열 모션.
        /// <b>회복·마법은 전용 모션을 먼저 찾는다</b> — 없으면
        /// <see cref="CharacterSkinSO.Attack"/> 안에서 원거리 → 근접으로 대체된다
        /// (유저 지시: 회복 원화가 없으면 공격 모션 사용).
        ///
        /// ★ <b>마법이 원거리와 갈라졌다</b> (2026-08-19) — 예전에는 여기서 마법도
        /// <c>Ranged</c> 로 접었다. 엘린 시트가 마법 시전을 <b>다른 동작으로</b> 그려 오면서
        /// 접을 수 없게 됐다(<see cref="SkinAttackMotion.Magic"/> 주석).
        /// 전용 원화가 없는 스킨은 <c>Attack()</c> 안에서 그대로 원거리로 떨어지므로
        /// <b>동작이 바뀌는 캐릭터는 엘린뿐</b>이다.
        /// </summary>
        SkinAttackMotion CurrentAttackMotion()
        {
            if (_combat == null) return SkinAttackMotion.Melee;
            switch (_combat.AttackType)
            {
                case TacticalAttackType.Heal: return SkinAttackMotion.Heal;
                case TacticalAttackType.Melee: return SkinAttackMotion.Melee;
                case TacticalAttackType.Magic: return SkinAttackMotion.Magic;
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
        ///
        /// ★ <b>2026-08-19 — 이동 방향을 「한 프레임 이동량」에서 「다듬은 진행 방향」으로</b>
        /// (유저 지시: *"이동할때 바라보는 방향 좀 더 정확하게 설정"*).
        ///
        /// 한 프레임 이동량에는 실제 진행 말고도 <b>유닛끼리 밀어내는 힘</b>·경로 재계산·
        /// 발판 보정이 전부 섞여 있다. 그래서 위아래로 걷는 동안 좌우 부호가 프레임마다
        /// 뒤집혀 <b>몸이 덜덜 떨렸고</b>, 대각선으로 갈 때는 잡음이 큰 쪽을 따라가 진행
        /// 방향과 다른 쪽을 보기도 했다. 이제 두 가지로 거른다:
        ///   ① <see cref="_heading"/> — 최근 <see cref="facingSmoothSeconds"/> 동안의 평균 속도
        ///   ② <see cref="facingTurnRatio"/> — 좌우 성분이 진행 방향의 이 비율은 돼야 돌아본다
        /// ②가 곧 <b>이력(hysteresis)</b>이기도 하다: 방향을 되돌릴 때 0 을 지나는 구간에서
        /// 좌우가 번갈아 뒤집히지 않고 마지막 방향을 들고 있는다.
        ///
        /// ⚠ <b>걷기 모션이 나오는 동안에는 이동 방향을 계속 본다</b>(<see cref="_walkUntil"/>).
        /// 한 프레임 이동량만 보면 걷는 그림이 나오는 중인데 판정은 "멈춤"이 되어, 옆에 적이
        /// 있을 때 <b>걸어가면서 뒤를 돌아보는</b> 그림이 됐다.
        /// </summary>
        void UpdateFacing(Vector2 delta)
        {
            // 시전 중에는 조준 방향을 고정한다 — 범위가 나가는 방향과 그림이 어긋나면
            // "엉뚱한 데를 보고 쏘는" 것처럼 보인다.
            if (InSkillMotion) return;

            // 쓰러진 동안에도 방향을 고정한다 — 죽은 캐릭터가 적을 따라 홱홱 도는 건 이상하다.
            if (InReviveMotion) return;

            DamageableUnit target = _combat != null ? _combat.Target : null;
            bool alive = target != null && target.IsAlive;

            // 때리는 순간만 타겟(= 투사체가 날아갈 방향)을 본다.
            if (Time.time < _attackUntil && alive)
            {
                FaceTowards(target.transform.position.x - transform.position.x);
                return;
            }

            // 이동 중 — 다듬은 진행 방향을 쓴다.
            if (Time.time < _walkUntil || delta.sqrMagnitude > moveThreshold * moveThreshold)
            {
                float speed = _heading.magnitude;
                if (speed > 0.0001f)
                {
                    if (Mathf.Abs(_heading.x) > speed * facingTurnRatio)
                        FaceTowards(_heading.x);
                    // 거의 위아래로만 가는 중이면 마지막 방향을 그대로 유지한다.
                    return;
                }
            }

            // 멈춰 서 있고 공격 모션도 아니면 적을 마주 본다 — 대치 중에 등을 보이면 어색하다.
            if (alive) FaceTowards(target.transform.position.x - transform.position.x);
        }

        /// <summary>좌우 성분이 뜻을 가질 만큼 클 때만 방향을 바꾼다.</summary>
        void FaceTowards(float dx)
        {
            if (Mathf.Abs(dx) < 0.001f) return;
            _facingRight = dx > 0f;
        }

        /// <summary>
        /// 보스 스킬 시전 모션을 재생한다 (<see cref="BossSkillCaster"/> 가 부른다).
        /// <paramref name="aimAt"/> 쪽을 바라보게 방향을 먼저 고정하고, 그 방향의
        /// 시전 프레임을 <paramref name="seconds"/> (또는 클립 한 바퀴 중 긴 쪽) 동안 재생한다.
        ///
        /// <b>전용 모션이 없으면 평타 모션으로 대체한다</b> — 스킨에 시전 원화를 안 넣은
        /// 보스(중간보스 2종은 전용 원화가 없다)도 "뭔가 했다"가 화면에 보여야 한다.
        /// 스킨의 모션 폴백 규칙(회복 → 원거리 → 근접)과 같은 취지다.
        /// </summary>
        public void PlaySkillMotion(int slot, float seconds, Vector3 aimAt)
        {
            if (_skin == null) return;

            float dx = aimAt.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.001f) _facingRight = dx > 0f;

            Sprite[] frames = _skin.SkillMotion(slot, _facingRight);
            if (frames != null && frames.Length > 0)
            {
                _skillSlot = slot;
                _skillUntil = Time.time + Mathf.Max(seconds, _skin.SkillClipSeconds(slot, _facingRight));
                _frameClock = 0f;
                return;
            }

            _attackUntil = Time.time + Mathf.Max(minAttackHoldSeconds, seconds);
        }

        /// <summary>
        /// 부활 모션을 <paramref name="seconds"/> 동안 재생한다 (「분노」의 경직 구간).
        /// <see cref="CharacterPassives"/> 가 사망 순간에 부른다.
        ///
        /// <b>전용 원화가 없으면 아무 일도 하지 않는다</b> — 보스 스킬(<see cref="PlaySkillMotion"/>)과
        /// 달리 평타로 대체하지 않는다. 부활은 지금 히스톤만 갖는 효과이고, 대체 모션으로
        /// 때리는 시늉을 하면 "죽었는데 공격한다" 로 보여 오히려 더 이상하다.
        /// </summary>
        public void PlayReviveMotion(float seconds)
        {
            if (_skin == null || seconds <= 0f) return;
            if (!HasReviveFrames) return;

            _reviveUntil = Time.time + seconds;
            _frameClock = 0f;

            // 남아 있던 다른 모션을 끊는다 — 죽는 순간의 공격 모션이 겹쳐 보이면 안 된다.
            _skillSlot = -1;
            _attackUntil = 0f;
            _walkUntil = 0f;
        }

        /// <summary>이 스킨에 부활 원화가 실제로 들어 있는지 (히스톤 외에는 비어 있다).</summary>
        public bool HasReviveFrames
        {
            get
            {
                if (_skin == null) return false;
                Sprite[] f = _skin.Revive(_facingRight);
                return f != null && f.Length > 0;
            }
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
