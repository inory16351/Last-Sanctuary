using UnityEngine;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 맵에 꽂히는 집결지 깃발 하나. <see cref="RallyPointService"/> 가 모체
    /// (<c>RallyFlags/RallyFlagTemplate</c>)를 복제해서 집결지 개수만큼 만든다.
    ///
    /// <b>왜 UI 가 아니라 월드 오브젝트인가</b> — 깃발은 "맵 위에 꽂힌 물건"이라
    /// 카메라 줌에 따라 같이 커지고 작아져야 한다. 예전 집결지 표시(반투명 원)는
    /// 캔버스 UI 라 매 프레임 화면 크기를 다시 계산해야 했다. 스프라이트로 두면
    /// 그 계산이 통째로 필요 없고, <b>콜라이더로 클릭 판정</b>을 그림과 정확히
    /// 같은 모양으로 잡을 수 있다(유저 요청: "깃발 콜라이더 버튼은 이미지와 동일하게").
    ///
    /// <b>스프라이트를 코드에서 넣는 이유</b>: MCP 로는 씬 오브젝트에 Sprite 참조를
    /// 넣을 수 없다(진행상황 8절 1번·27-9절). 그래서 모체에는 빈 SpriteRenderer 만
    /// 두고 <see cref="Resources"/> 경로로 읽어 꽂는다 — <c>CharacterSkinSO</c>·
    /// <c>CombatProjectileFx</c> 와 같은 방식이다. 인스펙터에 그림이 이미 꽂혀 있으면
    /// <b>그쪽을 존중</b>한다(유저가 직접 아트를 넣었을 때 코드가 덮어쓰지 않게).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class RallyFlag : MonoBehaviour
    {
        [Tooltip("깃발 그림이 비어 있을 때 읽어올 Resources 경로 (확장자 없이)")]
        [SerializeField] string spriteResource = "UI/RallyFlag";

        [Tooltip("부대 색이 없을 때 쓰는 기본 색조")]
        [SerializeField] Color tint = Color.white;

        SpriteRenderer _renderer;
        BoxCollider2D _collider;

        /// <summary>이 깃발이 나타내는 집결지 id.</summary>
        public int PointId { get; private set; }

        /// <summary>깃발 꼭대기의 월드 좌표. 부대 이름 라벨을 그 위에 띄우는 데 쓴다.</summary>
        public Vector3 TopWorld
        {
            get
            {
                if (_renderer == null || _renderer.sprite == null) return transform.position;
                return new Vector3(transform.position.x,
                                   transform.position.y + _renderer.sprite.bounds.max.y,
                                   transform.position.z);
            }
        }

        void Awake() => Prepare();

        void Prepare()
        {
            if (_renderer != null) return;

            _renderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<BoxCollider2D>();

            if (_renderer.sprite == null && !string.IsNullOrEmpty(spriteResource))
            {
                Sprite loaded = Resources.Load<Sprite>(spriteResource);
                if (loaded != null) _renderer.sprite = loaded;
                else Debug.LogWarning($"[Rally] 깃발 그림 'Resources/{spriteResource}' 을 찾지 못했습니다.", this);
            }

            FitCollider();
        }

        /// <summary>
        /// 콜라이더를 <b>그림 크기 그대로</b> 맞춘다. 유저가 나중에 깃발 PNG 를 다른 크기로
        /// 갈아끼워도 클릭 판정이 저절로 따라오게 하려는 것 — 크기를 코드에 박아두지 않는다.
        /// </summary>
        void FitCollider()
        {
            if (_collider == null || _renderer == null || _renderer.sprite == null) return;

            Bounds b = _renderer.sprite.bounds;
            _collider.size = b.size;
            _collider.offset = b.center;
            _collider.isTrigger = true;      // 물리 충돌이 아니라 클릭 판정용이다
        }

        /// <summary>복제 직후 <see cref="RallyPointService"/> 가 부른다.</summary>
        public void Bind(int pointId)
        {
            Prepare();
            PointId = pointId;
        }

        /// <summary>
        /// 색조를 바꾼다. 상태(평소 / 범위 펼침 / 끌고 있는 중의 잔상 / 따라다니는 분신)마다
        /// 다른 색·투명도를 <see cref="RallyPointService"/> 가 정해서 넣는다 — 어떤 상태가
        /// 있는지는 서비스만 알면 되므로 깃발 쪽에 상태를 두지 않는다.
        /// </summary>
        public void SetTint(Color color)
        {
            Prepare();
            if (_renderer != null) _renderer.color = color;
        }

        /// <summary>인스펙터에 정해둔 기본 색조.</summary>
        public Color DefaultTint => tint;
    }
}
