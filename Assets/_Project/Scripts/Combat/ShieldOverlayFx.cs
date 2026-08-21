using UnityEngine;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// ★★ <b>보호막을 몸 위에 «상시» 겹쳐 그린다</b> (2026-08-20).
    ///
    /// 유저 지시: *"카이론 실드 상시 표시(실드 효과 받고 있을때) … 분리된 이미지 사용해서
    /// 쉴드 있는 동안 <b>상시 적용</b> 되게 하고 <b>발동 이펙트로만</b> 스킬 이펙트 쓰고"*.
    ///
    /// <b>무엇이 문제였나</b> — 「타락한 육체」(80025)는 보호막을 <b>{value_01}초 동안</b>
    /// 건다. 그런데 연출은 <see cref="CharacterSkinSO.skill1Fx"/> 로 <b>발동 순간 한 번</b>만
    /// 떴다. 그래서 플레이어는 «보호막이 지금 있는지» 를 <b>화면에서 알 수 없었다</b> —
    /// 체력바의 흰 막대(120-12절 ①)로만 짐작해야 했다.
    ///
    /// ★ <b>왜 별도 컴포넌트인가</b> — <see cref="CharacterAnimator"/> 는
    ///   «지금 어느 모션을 재생하나» 하나를 판단하는 곳이고, 그 판단은 <b>배타적</b>이다
    ///   (사망 → 탈진 → 소환 → 부활 → 스킬 → 평타 → 걷기/대기 중 <b>하나</b>).
    ///   보호막은 그 줄에 끼어드는 것이 아니라 <b>어느 모션과도 동시에</b> 있어야 한다.
    ///   애니메이터에 넣으면 그 배타 규칙을 깨야 하고, 그러면 «모션이 하나» 라는
    ///   이 프로젝트의 뼈대가 흔들린다. 그래서 <b>겹쳐 그리는 것을 따로</b> 뒀다.
    ///
    /// ★ <b>스스로 붙는다</b> — <see cref="DamageableUnit.GrantShield"/> 가 걸릴 때
    ///   누가 붙여 주지 않아도, 이 컴포넌트는 캐릭터 템플릿에 없어도 된다.
    ///   <see cref="CharacterAnimator"/> 가 외형을 정할 때
    ///   <see cref="Ensure"/> 로 <b>필요한 유닛에만</b> 붙인다(원화가 있는 유닛뿐).
    ///   씬을 고치지 않으려는 것이다 — 이 프로젝트의 규칙이다.
    ///
    /// ⚠ <b>정렬은 몸보다 한 칸 앞</b>이다. 뒤에 두면 날개·몸통에 가려 안 보이고,
    ///   두 칸 앞에 두면 데미지 숫자·체력바를 덮는다.
    ///
    /// ------------------------------------------------------------------
    /// ★★★ <b>2026-08-21 — «깜빡거리고 정신 없다» 를 고쳤다</b>
    /// ------------------------------------------------------------------
    /// 유저 리포트: *"카이론 쉴드 사용 시 사용 이펙트만 행동하고 캐릭터에게 들어가게.
    /// 현재 <b>앞을 보고 있는 프레임이 섞이고</b> 사용되는 쉴드의 <b>다른 이미지들이 섞여
    /// 들어가서 깜빡거리는것처럼</b> 보이고 정신 없음. <b>하나의</b> 쉴드 이펙트를
    /// <b>행동하는</b> 캐릭터에게 덧씌우는 로직으로 구현할것"*.
    ///
    /// 원인이 <b>셋</b>이었고 세 곳을 고쳤다:
    ///
    /// <list type="number">
    /// <item><b>원화 여섯 장이 애니메이션이 아니었다.</b> 서로 다른 «디자인 후보» 여서
    ///   8fps 로 돌리면 매 프레임 다른 구체가 나온다 — 그것이 «깜빡임» 이다.
    ///   → 분해기가 <b>한 장만</b> 굽는다(<c>Tools/chiron_skin_build.py</c> 의 ★★★).
    ///   여기서는 <b>첫 장을 고정</b>으로 쓴다 — 두 장 이상이 와도 돌리지 않는다.</item>
    /// <item><b>「타락한 육체」가 보호막이 도는 내내 스킬 1 모션을 재생했다.</b> 그 원화는
    ///   <b>정면 자세</b>이고 3·4번 칸에는 구체가 <b>몸과 한 그림으로</b> 그려져 있다 —
    ///   그래서 정면 프레임이 섞이고 구체가 두 겹으로 겹쳐 번쩍였다.
    ///   → <c>CharacterPassives.Newcomers.TryFallenBody</c> 가 그 호출을 <b>지웠다</b>.
    ///   캐릭터는 하던 행동을 그대로 하고, 보호막은 이 컴포넌트만 그린다.</item>
    /// <item><b>구체가 발밑에 작게 깔렸다.</b> 두 원화의 피벗이 «가로 0.5 · 세로 0»(발밑)
    ///   이라 구체의 <b>아래쪽이 발에 붙고</b>, 크기도 몸통의 3/4 뿐이라 다리만 감쌌다
    ///   (실측: 몸통 83x95 · 구체 71x65). → 아래 <see cref="Fit"/> 가 <b>몸을 감싸도록</b>
    ///   키우고 <b>몸의 가운데</b>에 놓는다.</item>
    /// </list>
    ///
    /// ★ <b>기준은 «지금 프레임» 이 아니라 «대기 원화 한 장»</b>이다 — 모션마다 캔버스
    ///   크기가 달라서(대기 83x95 · 스킬 1 이 더 크다) 매 프레임 재면 <b>보호막이
    ///   출렁인다.</b> 애니메이터가 탈진 구간에서 «지금 프레임이 아니라 첫 장» 을 기준으로
    ///   삼는 것과 같은 이유다(<see cref="CharacterAnimator"/> 의 <c>ResolveScale</c>).
    ///
    /// ★ <b>발동 순간에만 한 번 «펑» 한다</b> — 유저가 말한 «사용 이펙트» 다.
    ///   같은 그림 한 장을 <see cref="PopSeconds"/> 동안 <b>조금 크게 · 투명하게</b>
    ///   시작해 제 크기로 죄는 것이 전부다. 새 원화가 필요 없고, 끝나면 <b>완전히
    ///   멈춘다</b> — 그래야 «정신 없다» 로 돌아가지 않는다.
    /// </summary>
    public class ShieldOverlayFx : MonoBehaviour
    {
        /// <summary>몸통 스프라이트보다 몇 칸 앞에 그릴 것인가.</summary>
        const int SortingOffset = 1;

        /// <summary>자식 오브젝트 이름 — 하이라키에서 알아보기 쉽게.</summary>
        const string ChildName = "ShieldOverlay";

        /// <summary>
        /// 구체가 몸통 상자를 <b>얼마나 넉넉히</b> 감쌀 것인가. 1.0 이면 딱 붙어서
        /// 어깨·날개 끝이 구체 선에 닿는다 — 조금 띄우는 편이 «감싼» 것으로 보인다.
        /// </summary>
        const float FitMargin = 1.04f;

        /// <summary>발동 «펑» 이 걸리는 시간(초). 이보다 길면 다시 산만해진다.</summary>
        const float PopSeconds = 0.22f;

        /// <summary>발동 순간 몇 배에서 시작해 제 크기로 죄는가.</summary>
        const float PopScale = 1.35f;

        CharacterAnimator _animator;
        SpriteRenderer _body;
        SpriteRenderer _renderer;

        /// <summary>발동 «펑» 이 시작된 시각. 0 이면 지금 보호막이 없다.</summary>
        float _shownAt;

        /// <summary>지금 걸린 값을 계산해 둔 스킨 — 바뀔 때만 다시 잰다.</summary>
        CharacterSkinSO _fitted;
        float _fitScale = 1f;
        Vector3 _fitOffset;

        /// <summary>
        /// 이 유닛에 겹쳐 그리기를 <b>필요하면</b> 붙인다. 원화가 없으면 아무것도 하지 않는다.
        ///
        /// ★ 부르는 쪽은 <see cref="CharacterAnimator"/> 의 외형 결정 지점이다 —
        ///   그때가 «이 유닛의 스킨이 확정되는» 유일한 순간이다.
        /// </summary>
        public static void Ensure(GameObject host, CharacterSkinSO skin)
        {
            if (host == null || skin == null || !skin.HasShieldArt) return;
            if (host.GetComponent<ShieldOverlayFx>() != null) return;
            host.AddComponent<ShieldOverlayFx>();
        }

        void Awake()
        {
            _animator = GetComponent<CharacterAnimator>();
            _body = GetComponent<SpriteRenderer>();
            Build();
        }

        void Build()
        {
            Transform found = transform.Find(ChildName);
            if (found != null)
            {
                _renderer = found.GetComponent<SpriteRenderer>();
                return;
            }

            var go = new GameObject(ChildName);
            go.transform.SetParent(transform, false);
            // ⚠ <b>위치·크기는 Fit 이 정한다</b> — 부모 스케일은 그대로 물려받으므로
            //   (자식이다) 캐릭터가 커지면 보호막도 같이 커진다. 여기서 넣는 값은
            //   Fit 이 아직 안 돌았을 때의 안전한 초기값이다.
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;

            _renderer = go.AddComponent<SpriteRenderer>();
            _renderer.enabled = false;
            if (_body != null)
            {
                _renderer.sortingLayerID = _body.sortingLayerID;
                _renderer.sortingOrder = _body.sortingOrder + SortingOffset;
            }
        }

        void LateUpdate()
        {
            if (_renderer == null) return;

            CharacterSkinSO skin = _animator != null ? _animator.Skin : null;
            Sprite[] frames = skin != null ? skin.Shield() : null;
            var target = GetComponent<DamageableUnit>();

            // ⚠ <b>죽은 유닛에는 그리지 않는다</b> — 사망 모션이 도는 동안 보호막이
            //   남아 있는 경우가 있다(시간이 남았을 뿐 의미가 없다).
            bool show = frames != null && frames[0] != null
                        && target != null && target.IsAlive && target.HasShield;
            if (!show)
            {
                if (_renderer.enabled)
                {
                    _renderer.enabled = false;
                    _shownAt = 0f;      // 다음에 걸릴 때 «펑» 이 다시 보이게
                }
                return;
            }

            // ★ <b>한 장으로 고정</b> — 돌리지 않는다(맨 위 ★★★ ①).
            Sprite sprite = frames[0];
            if (_renderer.sprite != sprite) _renderer.sprite = sprite;

            Fit(skin, sprite);

            if (_shownAt <= 0f) _shownAt = Time.time;
            float t = PopSeconds > 0f ? Mathf.Clamp01((Time.time - _shownAt) / PopSeconds) : 1f;

            // 발동 «펑» : 크게·투명하게 시작해 제 크기·제 색으로 죈다. t=1 이면 완전히 멈춘다.
            float scale = _fitScale * Mathf.Lerp(PopScale, 1f, t);
            _renderer.transform.localScale = new Vector3(scale, scale, 1f);
            _renderer.transform.localPosition = FitOffsetFor(sprite, scale);

            Color c = _renderer.color;
            c.a = t;
            _renderer.color = c;

            _renderer.enabled = true;

            // ★ 몸통이 좌우로 뒤집혀도 <b>구체는 뒤집지 않는다</b> — 원형이라 뒤집을 것이
            //   없고, 뒤집으면 자식 스케일이 음수가 되어 정렬이 흔들린다.
            if (_body != null && _renderer.sortingLayerID != _body.sortingLayerID)
            {
                _renderer.sortingLayerID = _body.sortingLayerID;
                _renderer.sortingOrder = _body.sortingOrder + SortingOffset;
            }
        }

        /// <summary>
        /// 구체가 <b>몸통 상자를 감싸는</b> 배율을 잰다. 스킨이 바뀔 때만 다시 잰다.
        ///
        /// ★ 기준은 <b>대기 원화 첫 장</b>이다 — 모션마다 캔버스가 달라서 지금 프레임으로
        ///   재면 보호막이 모션마다 출렁인다(맨 위 ★ 참조).
        /// ⚠ 배율은 <b>가로·세로 중 더 모자란 쪽</b>으로 정한다(max) — 안에 넣는(contain)
        ///   것이 아니라 <b>덮는(cover)</b> 것이어야 몸이 구체 밖으로 안 삐져나온다.
        ///   캐릭터 크기 보정이 contain 인 것과 <b>반대</b>이고, 그것이 맞다: 저쪽은
        ///   «판정 상자 안에 그림을 넣는» 일이고 이쪽은 «그림을 그림으로 감싸는» 일이다.
        /// </summary>
        void Fit(CharacterSkinSO skin, Sprite sprite)
        {
            if (skin == _fitted) return;

            _fitted = skin;
            _fitScale = 1f;
            _fitOffset = Vector3.zero;

            Sprite[] idle = skin.Idle(true);
            if (idle == null || idle.Length == 0 || idle[0] == null || sprite == null) return;

            Bounds body = idle[0].bounds;
            Bounds ball = sprite.bounds;
            if (ball.size.x <= 0.0001f || ball.size.y <= 0.0001f) return;

            _fitScale = Mathf.Max(body.size.x / ball.size.x, body.size.y / ball.size.y) * FitMargin;
            _fitOffset = body.center;
        }

        /// <summary>
        /// 배율 <paramref name="scale"/> 로 그릴 때 구체의 <b>가운데가 몸의 가운데</b>에
        /// 오게 하는 자리. 두 원화의 피벗이 발밑(세로 0)이라 그냥 두면 구체가
        /// <b>발에 붙는다</b> — 그래서 매번 다시 낸다(«펑» 중에는 배율이 변한다).
        /// </summary>
        Vector3 FitOffsetFor(Sprite sprite, float scale)
        {
            if (sprite == null) return _fitOffset;
            Vector3 ballCenter = sprite.bounds.center * scale;
            return new Vector3(_fitOffset.x - ballCenter.x, _fitOffset.y - ballCenter.y, 0f);
        }
    }
}
