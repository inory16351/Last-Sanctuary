using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.UI
{
    /// <summary>
    /// ★★ <b>성역 바로 위에 뜨는 월드 공간 체력 게이지</b> (2026-09-01 · 유저 지시:
    /// *"성역 바로 위에 체력 게이지 추가해주고"*).
    ///
    /// <b>왜 필요했나</b> — 성역의 체력을 볼 수 있는 곳이 <b>없었다</b>. 보스 체력바
    /// (<see cref="BossHealthPanel"/>)는 몬스터 전용이고, 로스터는 캐릭터만 보여준다.
    /// 성역이 파괴되면 <b>즉시 패배</b>(<c>DefeatReason.NexusDestroyed</c>)인데
    /// 플레이어는 «얼마나 남았는지» 를 화면 어디서도 알 수 없었다.
    ///
    /// ★ <b>스스로 붙는다</b> — <see cref="Ensure"/> 를 <c>Nexus.Start</c> 가 부른다.
    ///   씬·프리팹을 고치지 않는다(<see cref="ShieldOverlayFx"/> 와 같은 규칙 · 이 프로젝트의
    ///   «객체는 MCP 로, 붙는 연출은 코드로» 관행).
    ///
    /// ⚠⚠ <b>왜 성역의 «자식» 이 아닌가</b> — <c>NexusAnimator.FitToTiles</c> 가
    ///   <b>성역 트랜스폼의 localScale 을 매 프레임 덮어쓴다</b>(원화를 상자에 맞춰 넣는다).
    ///   자식으로 달면 그 배율을 그대로 물려받아, 스킨 원화가 바뀌거나 프레임마다 그림
    ///   크기가 달라질 때 <b>게이지가 같이 늘었다 줄었다 한다</b>. 그래서 <b>독립 루트
    ///   오브젝트</b>로 만들고 <see cref="LateUpdate"/> 에서 위치만 따라간다.
    ///   (<c>LateUpdate</c> 인 이유 — 애니메이터가 <c>Update</c> 에서 크기를 정하므로
    ///    그 뒤에 자리를 잡아야 한 프레임 늦게 따라가지 않는다.)
    ///
    /// ⚠ 스프라이트를 <b>코드에서 만든다</b>(1x1 흰 점). 막대 하나 때문에 에셋을 늘리지
    ///   않으려는 것이고, <c>pixelsPerUnit = 1</c> 로 두면 <b>스케일 1 = 1타일</b> 이라
    ///   크기를 타일로 바로 적을 수 있다(<c>FogOfWarService</c> 가 쓰는 것과 같은 수법).
    /// </summary>
    [DisallowMultipleComponent]
    public class NexusHealthBar : MonoBehaviour
    {
        // ── 모양 (타일 단위) ────────────────────────────────────────────
        [Tooltip("게이지 가로 길이(타일)")]
        [Min(0.1f)] [SerializeField] float widthTiles = 3.2f;

        [Tooltip("게이지 두께(타일)")]
        [Min(0.02f)] [SerializeField] float heightTiles = 0.28f;

        [Tooltip("성역 그림의 <b>위쪽 끝</b>에서 더 띄우는 거리(타일). " +
                 "게이지가 그림에 닿으면 올리고, 너무 멀면 내린다")]
        [SerializeField] float marginTiles = 0.45f;

        [Tooltip("테두리 두께(타일). 0 이면 테두리를 안 그린다")]
        [Min(0f)] [SerializeField] float borderTiles = 0.05f;

        [Tooltip("이 비율 아래에서 색이 빨강으로 간다. 로스터·전술 창과 같은 기준")]
        [Range(0.05f, 0.9f)] [SerializeField] float lowHpRatio = 0.35f;

        [Tooltip("줄어든 만큼을 흰 잔상으로 남겼다가 따라 줄어드는 속도(비율/초). " +
                 "0 이면 잔상 없이 즉시 줄어든다 — 로스터의 HpGhostBar 와 같은 결")]
        [Min(0f)] [SerializeField] float ghostFollowPerSecond = 0.6f;

        // ⚠⚠ <b>정렬 레이어를 반드시 정해야 한다</b> (2026-09-01 실측으로 발견).
        //   프로젝트 레이어는 뒤로 갈수록 위다:
        //       Default → Background → Floor → <b>Object</b> → Overhead → VFX → <b>WorldUI</b>
        //   성역 스프라이트는 <b>Object</b>, 안개는 <b>Overhead</b>(order 100),
        //   데미지 숫자도 <b>Overhead</b>(order 200)다.
        //   코드로 만든 SpriteRenderer 는 기본이 <b>Default</b> — 즉 <b>가장 아래</b>라
        //   성역에도 안개에도 가려 <b>아예 안 보인다</b>. 만들어졌는지만 확인하면
        //   «떴다» 고 착각하기 딱 좋은 종류의 버그다.
        //
        //   ★ 맨 위의 <b>WorldUI</b> 는 아직 아무도 안 쓰는 «월드 공간 UI» 자리다.
        //     체력바는 <b>무엇에도 가리면 안 되는</b> 정보이므로 여기가 맞다
        //     (데미지 숫자보다도 위다 — 얇은 막대라 숫자를 가리지 않는다).
        [Tooltip("정렬 레이어. 비우면 성역과 같은 레이어를 쓴다.\n" +
                 "기본 WorldUI — Object(성역)·Overhead(안개·데미지 숫자)·VFX 보다 위다")]
        [SerializeField] string sortingLayerName = "WorldUI";

        [Tooltip("같은 레이어 안에서의 순서")]
        [SerializeField] int sortingOrder = 0;

        DamageableUnit _unit;
        SpriteRenderer _body;

        /// <summary>
        /// 성역 트랜스폼에서 그림 <b>위쪽 끝</b>까지의 거리(타일). NaN 이면 아직 못 쟀다.
        ///
        /// ⚠⚠ <b>«절반 높이» 가 아니다</b>(2026-09-01 실측으로 고침) — 성역 원화는
        ///   <b>피벗이 바닥</b>이다. 플레이 모드에서 재보니 트랜스폼이 (0.5, 0.5) 인데
        ///   스프라이트 월드 <c>bounds</c> 가 <b>y 0.5 ~ 4.5</b> 였다. 중심 피벗을 가정하고
        ///   «절반 높이 + 여백» 을 더했더니 게이지가 <b>그림 한가운데(y 3.43)에 박혔다</b>.
        ///
        /// ★ <b>한 번만 재서 굳힌다</b> — 성역은 심장처럼 뛰는 애니메이션이라 프레임마다
        ///   <c>bounds</c> 가 미세하게 달라진다. 매 프레임 따라가면 게이지가 위아래로 떤다.
        /// </summary>
        float _topOffset = float.NaN;

        Transform _root;
        SpriteRenderer _border, _back, _fill, _ghost;
        float _ghostRatio = 1f;

        /// <summary>
        /// 이 성역에 게이지를 붙인다(이미 있으면 그대로 돌려준다).
        /// <c>Nexus.Start</c> 가 부른다 — 씬에 미리 넣어둘 필요가 없다.
        /// </summary>
        public static NexusHealthBar Ensure(DamageableUnit nexus)
        {
            if (nexus == null) return null;
            if (nexus.TryGetComponent(out NexusHealthBar existing)) return existing;
            return nexus.gameObject.AddComponent<NexusHealthBar>();
        }

        void Awake()
        {
            _unit = GetComponent<DamageableUnit>();
            _body = GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// 성역 그림 <b>위쪽 끝</b>까지의 거리를 잰다. 아직 못 재면 <c>false</c>.
        ///
        /// <b>실제 <c>bounds</c> 로 잰다</b> — 피벗이 바닥인지 가운데인지 <b>짐작하지 않으려는</b>
        /// 것이다(그 짐작이 정확히 이 버그였다). <see cref="LateUpdate"/> 에서 부르므로
        /// <c>NexusAnimator</c> 가 <c>Update</c> 에서 크기를 맞춘 <b>뒤</b>의 값이다.
        ///
        /// 폴백은 <see cref="Units.NexusAnimator.RenderSizeTiles"/> — 그림을 넣는 상자의
        /// 세로다. 피벗이 바닥이라는 실측과 맞물려 «위쪽 끝» 과 거의 같은 값이 된다.
        /// </summary>
        bool TryMeasureTop()
        {
            if (_body != null && _body.sprite != null)
            {
                _topOffset = _body.bounds.max.y - transform.position.y;
                return true;
            }

            if (TryGetComponent(out Units.NexusAnimator anim))
            {
                _topOffset = anim.RenderSizeTiles.y;
                return true;
            }

            return false;      // 다음 프레임에 다시 잰다
        }

        void OnEnable()
        {
            BuildIfNeeded();
            if (_root != null) _root.gameObject.SetActive(true);
            _ghostRatio = _unit != null ? _unit.HpRatio : 1f;
        }

        void OnDisable()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        // ------------------------------------------------------------------
        // 만들기
        // ------------------------------------------------------------------

        static Sprite _dot;

        /// <summary>
        /// 1x1 흰 점. <c>pixelsPerUnit = 1</c> 이라 <b>스케일 1 = 1타일</b> 이 된다.
        /// 한 장을 모두가 공유한다(성역은 하나뿐이지만, 다시 만들 이유도 없다).
        /// </summary>
        static Sprite Dot()
        {
            if (_dot != null) return _dot;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _dot = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _dot.hideFlags = HideFlags.HideAndDontSave;
            return _dot;
        }

        void BuildIfNeeded()
        {
            if (_root != null) return;

            var go = new GameObject("NexusHealthBar");
            _root = go.transform;

            // ⚠ 부모를 두지 않는다 — 클래스 주석의 ⚠⚠ 참조(성역의 localScale 을 물려받으면 안 된다).
            _border = MakePiece("Border", HudTheme.PanelBg, 0);
            _back = MakePiece("Back", HudTheme.BarBack, 1);
            _ghost = MakePiece("Ghost", new Color(1f, 1f, 1f, 0.55f), 2);
            _fill = MakePiece("Fill", HudTheme.BarHp, 3);

            if (borderTiles <= 0f && _border != null) _border.enabled = false;

            // 채움·잔상은 <b>왼쪽 끝을 축</b>으로 늘어나야 «왼쪽에서 오른쪽으로 줄어드는»
            // 보통의 체력바가 된다. 피벗을 바꿀 수 없으니 자리를 옮겨 같은 효과를 낸다
            // (아래 SetRatio 가 x 위치를 같이 옮긴다).
        }

        SpriteRenderer MakePiece(string name, Color color, int orderOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Dot();
            sr.color = color;

            // 레이어 이름이 비었거나 프로젝트에 없으면 <b>성역과 같은 레이어</b>로 떨어뜨린다 —
            // 최소한 «없는 레이어라 Default(맨 아래)로 가는» 사고는 막는다
            // (ShieldOverlayFx 가 _body.sortingLayerID 를 따라가는 것과 같은 안전망).
            if (!string.IsNullOrEmpty(sortingLayerName) &&
                SortingLayer.NameToID(sortingLayerName) != 0)
                sr.sortingLayerID = SortingLayer.NameToID(sortingLayerName);
            else if (_body != null)
                sr.sortingLayerID = _body.sortingLayerID;

            sr.sortingOrder = sortingOrder + orderOffset;
            return sr;
        }

        // ------------------------------------------------------------------
        // 갱신
        // ------------------------------------------------------------------

        void LateUpdate()
        {
            if (_unit == null || _root == null) return;

            // 성역이 죽으면 게이지도 사라진다 — «0 짜리 빈 막대» 가 남아 있으면
            // 패배 화면 뒤로 유령처럼 보인다.
            if (!_unit.IsAlive)
            {
                _root.gameObject.SetActive(false);
                return;
            }

            if (float.IsNaN(_topOffset) && !TryMeasureTop()) return;

            _root.position = transform.position + Vector3.up * (_topOffset + marginTiles);

            float ratio = Mathf.Clamp01(_unit.HpRatio);

            // 잔상은 «줄어들 때만» 따라온다. 회복은 즉시 반영해야 «찼다» 가 바로 보인다.
            if (ghostFollowPerSecond <= 0f || _ghostRatio < ratio) _ghostRatio = ratio;
            else _ghostRatio = Mathf.Max(ratio, _ghostRatio - ghostFollowPerSecond * Time.deltaTime);

            float w = widthTiles, h = heightTiles;

            if (_border != null && _border.enabled)
                Place(_border, w + borderTiles * 2f, h + borderTiles * 2f, 0f, 1f);
            Place(_back, w, h, 0f, 1f);
            Place(_ghost, w, h, -w * 0.5f, _ghostRatio);
            Place(_fill, w, h, -w * 0.5f, ratio);

            _fill.color = HpColor(ratio);
        }

        /// <summary>
        /// 조각 하나의 크기·자리를 정한다.
        /// <paramref name="anchorX"/> 가 0 이면 가운데 정렬(배경·테두리),
        /// <c>-w/2</c> 면 <b>왼쪽 끝 고정</b>(채움·잔상)이다.
        /// </summary>
        void Place(SpriteRenderer sr, float w, float h, float anchorX, float fill)
        {
            if (sr == null) return;
            fill = Mathf.Clamp01(fill);
            float len = w * fill;
            sr.transform.localScale = new Vector3(len, h, 1f);
            sr.transform.localPosition = new Vector3(anchorX + len * 0.5f, 0f, 0f);
            sr.enabled = len > 0.0001f;
        }

        /// <summary>
        /// 체력 비율 → 초록 · 노랑 · 빨강. 로스터(<see cref="CharacterRosterPanel"/>) ·
        /// 성장 창 · 전술 창이 쓰는 것과 <b>같은 3단 보간</b>이다 — 화면마다 다른 색이면
        /// 플레이어가 «얼마나 위험한지» 를 두 벌로 배워야 한다.
        /// </summary>
        Color HpColor(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            float mid = lowHpRatio;
            return ratio >= mid
                ? Color.Lerp(HudTheme.BarHpMid, HudTheme.BarHp, (ratio - mid) / (1f - mid))
                : Color.Lerp(HudTheme.BarHpLow, HudTheme.BarHpMid, ratio / mid);
        }
    }
}
