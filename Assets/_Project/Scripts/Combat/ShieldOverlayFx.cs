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
    /// ⚠ <b>크기는 몸에 맞춘다</b> — 원화의 구체는 캐릭터보다 조금 크게 그려져 있어
    ///   «몸을 감싸는» 크기가 저절로 나온다. 부모의 스케일을 그대로 받으므로
    ///   (자식이다) 캐릭터가 커지면 보호막도 같이 커진다.
    /// </summary>
    public class ShieldOverlayFx : MonoBehaviour
    {
        /// <summary>겹쳐 그리는 그림의 재생 속도(초당 장수). 숨 쉬는 느낌이 나는 정도.</summary>
        const float FramesPerSecond = 8f;

        /// <summary>몸통 스프라이트보다 몇 칸 앞에 그릴 것인가.</summary>
        const int SortingOffset = 1;

        /// <summary>자식 오브젝트 이름 — 하이라키에서 알아보기 쉽게.</summary>
        const string ChildName = "ShieldOverlay";

        CharacterAnimator _animator;
        SpriteRenderer _body;
        SpriteRenderer _renderer;
        float _clock;

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
            // ⚠ 부모 스케일을 그대로 받는다 — 캐릭터 크기 보정(ApplyRenderSize)이
            //   부모에 걸리므로 자식은 아무것도 안 해야 «같이» 커진다.
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
            bool show = frames != null && target != null && target.IsAlive && target.HasShield;
            if (!show)
            {
                if (_renderer.enabled)
                {
                    _renderer.enabled = false;
                    _clock = 0f;          // 다음에 켜질 때 첫 장부터 — «새로 걸렸다» 가 보인다
                }
                return;
            }

            _clock += Time.deltaTime;
            int i = frames.Length <= 1
                ? 0
                : Mathf.FloorToInt(_clock * FramesPerSecond) % frames.Length;

            _renderer.sprite = frames[i];
            _renderer.enabled = true;

            // ★ 몸통이 좌우로 뒤집혀도 <b>구체는 뒤집지 않는다</b> — 원형이라 뒤집을 것이
            //   없고, 뒤집으면 자식 스케일이 음수가 되어 정렬이 흔들린다.
            if (_body != null && _renderer.sortingLayerID != _body.sortingLayerID)
            {
                _renderer.sortingLayerID = _body.sortingLayerID;
                _renderer.sortingOrder = _body.sortingOrder + SortingOffset;
            }
        }
    }
}
