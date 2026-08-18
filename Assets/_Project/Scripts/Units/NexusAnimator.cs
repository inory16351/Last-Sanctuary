using UnityEngine;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 넥서스를 <b>심장처럼 뛰게</b> 한다 (2026-08-18, 유저 지시:
    /// <i>"스프라이트 이미지 찾아보고 스킨 만들어서 심장 뛰는 거 처럼 만들어줘 모션 끼워 맞춰서"</i>).
    ///
    /// <b>모션이 세 벌인 이유는 원화가 그렇게 그려져 있기 때문이다</b> — 시트가
    /// 「체력 50% 이상 / 10~50% / 10% 이하」로 나뉘어 있고 각각 *"박동 강함 / 박동 약화 ·
    /// 균열 / 박동 불규칙 · 붕괴"* 라고 적혀 있다. 그래서 <b>체력이 곧 애니메이션</b>이다 —
    /// 따로 상태를 만들지 않았다(<see cref="NexusSkinSO.IdleFor"/>).
    ///
    /// ★ <b>「불규칙」을 코드로</b> — 10% 이하 구간에서는 프레임 간격을 매번 흔든다
    /// (<see cref="lowHpJitter"/>). 원화는 프레임만 다르지 <b>속도</b>는 못 그리는데,
    /// 시트가 요구한 것은 "박동이 불규칙해진다" 라 간격 쪽에서 만들어야 한다.
    ///
    /// ⚠ <b>스킨을 문자열로 찾는다</b>(<see cref="skinResourcePath"/>). MCP 로는 씬에
    /// 오브젝트 참조를 못 넣기 때문이다(진행상황 8절 4번) — 이 프로젝트의
    /// <c>CharacterAnimator</c> 가 쓰는 방식과 같다.
    ///
    /// ⚠ <b>크기는 여기서 정한다</b>(<see cref="renderSizeTiles"/>). 넥서스는 표가 없는
    /// 유일한 유닛이라(정의 에셋에 콜라이더 칸이 없다) 몬스터처럼 표에서 받을 수 없다.
    /// 발판(<c>NexusDefinitionSO.footprintTiles</c>)과 <b>따로</b>인 것은 의도다 —
    /// 발판은 이동 판정이고 이 값은 보이는 크기다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public class NexusAnimator : MonoBehaviour
    {
        [Header("스킨")]
        [Tooltip("Resources 아래의 스킨 에셋 경로. 예: BuildingSkins/Nexus/Skin_Nexus")]
        [SerializeField] string skinResourcePath = "BuildingSkins/Nexus/Skin_Nexus";

        [Header("크기")]
        [Tooltip("화면에 그릴 크기(타일). 그림을 이 상자 안에 <b>비율을 유지한 채</b> 넣는다 — " +
                 "몬스터 콜라이더 상자와 같은 규칙(66절).\n" +
                 "0 이면 원화 크기 그대로 둔다")]
        [SerializeField] Vector2 renderSizeTiles = new Vector2(4f, 4f);

        [Header("박동")]
        [Tooltip("체력 10% 이하일 때 프레임 간격을 흔드는 폭(비율). 0.4 면 간격이 60~140% 사이에서 " +
                 "매 프레임 달라진다 — 원화 시트의 \"심장 박동 불규칙\" 을 만드는 값이다.\n" +
                 "0 이면 다른 구간과 같은 일정한 속도로 뛴다")]
        [Range(0f, 0.9f)] [SerializeField] float lowHpJitter = 0.4f;

        [Tooltip("체력이 낮을수록 느리게 뛴다 — 체력 100% 일 때 1.0 배, 0% 일 때 이 값. " +
                 "1 이면 체력과 무관하게 같은 속도")]
        [Range(0.2f, 1f)] [SerializeField] float slowestBeatScale = 0.55f;

        [Header("디버그")]
        [SerializeField] bool logSkinChoice;

        SpriteRenderer _renderer;
        Combat.DamageableUnit _unit;
        NexusSkinSO _skin;

        Sprite[] _frames;
        int _index;
        float _nextFrameTime;

        /// <summary>파괴 모션이 시작됐는지. 시작하면 대기 모션으로 돌아가지 않는다.</summary>
        bool _dying;

        /// <summary>지금 쓰는 스킨 (디버그·검증용).</summary>
        public NexusSkinSO Skin => _skin;

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _unit = GetComponent<Combat.DamageableUnit>();

            _skin = Resources.Load<NexusSkinSO>(skinResourcePath);
            if (_skin == null || !_skin.IsUsable)
            {
                Debug.LogWarning($"[넥서스] 스킨 'Resources/{skinResourcePath}' 을 찾지 못했습니다 — " +
                                 "Tools/gen_nexus_skin.py 를 돌려주세요. 기존 스프라이트를 그대로 둡니다.",
                                 this);
                _skin = null;
                enabled = false;
                return;
            }

            if (logSkinChoice)
                Debug.Log($"[넥서스] 스킨 {_skin.name} · 대기 {_skin.IdleFor(1f).Length}프레임", this);
        }

        void OnEnable()
        {
            if (_unit != null) _unit.OnDied += HandleDied;
            _nextFrameTime = 0f;
        }

        void OnDisable()
        {
            if (_unit != null) _unit.OnDied -= HandleDied;
        }

        void HandleDied(Combat.DamageableUnit _)
        {
            if (_skin == null || !_skin.HasDestroy) return;
            _dying = true;
            _frames = _skin.destroy;
            _index = 0;
            _nextFrameTime = 0f;
        }

        void Update()
        {
            if (_skin == null) return;

            // ★ 체력 구간이 바뀌면 <b>모션을 통째로 갈아끼운다</b>. 프레임 번호는 유지하지
            //   않는다 — 세 벌이 같은 박동의 다른 단계라 이어 붙이면 오히려 튄다.
            if (!_dying)
            {
                float ratio = _unit != null ? _unit.HpRatio : 1f;
                Sprite[] want = _skin.IdleFor(ratio);
                if (!ReferenceEquals(want, _frames))
                {
                    _frames = want;
                    _index = 0;
                    _nextFrameTime = 0f;
                }
            }

            if (_frames == null || _frames.Length == 0) return;
            if (Time.time < _nextFrameTime) return;

            Sprite sprite = _frames[_index];
            if (sprite != null && _renderer.sprite != sprite)
            {
                _renderer.sprite = sprite;
                FitToTiles(sprite);
            }

            _nextFrameTime = Time.time + FrameInterval();

            if (_index + 1 < _frames.Length) _index++;
            // 파괴는 <b>마지막에서 멈춘다</b>(시트의 "완전 파괴 후 정지"). 대기는 순환한다.
            else if (!_dying) _index = 0;
        }

        /// <summary>
        /// 다음 프레임까지의 간격(초). 체력이 낮을수록 느려지고, 10% 아래에서는 흔들린다.
        /// </summary>
        float FrameInterval()
        {
            float fps = Mathf.Max(1f, _skin.framesPerSecond);
            float step = 1f / fps;
            if (_dying) return step;

            float ratio = _unit != null ? Mathf.Clamp01(_unit.HpRatio) : 1f;

            // 체력이 낮을수록 느리게 — 1.0 배(만피) → slowestBeatScale(빈사).
            step /= Mathf.Lerp(slowestBeatScale, 1f, ratio);

            if (ratio <= 0.10f && lowHpJitter > 0f)
                step *= 1f + Random.Range(-lowHpJitter, lowHpJitter);

            return Mathf.Max(0.02f, step);
        }

        /// <summary>
        /// 그림을 <see cref="renderSizeTiles"/> 상자 안에 <b>비율 유지로</b> 넣는다
        /// (맵 한 칸 = 1 월드 유닛이므로 <c>bounds.size</c> 가 곧 타일 수다).
        /// 66절이 몬스터 콜라이더에 쓴 계산과 같다.
        /// </summary>
        void FitToTiles(Sprite sprite)
        {
            if (renderSizeTiles.x <= 0f || renderSizeTiles.y <= 0f) return;

            Vector3 art = sprite.bounds.size;
            if (art.x <= 0.0001f || art.y <= 0.0001f) return;

            float s = Mathf.Min(renderSizeTiles.x / art.x, renderSizeTiles.y / art.y);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
