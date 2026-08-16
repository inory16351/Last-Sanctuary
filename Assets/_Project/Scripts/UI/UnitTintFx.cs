using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// <b>유닛 스프라이트 색을 칠하는 단 하나의 주인</b> (2026-08-17 신설, 유저 지시:
    /// <i>"안 좋은 정신 이상 상태 걸린 캐릭터 빨간색 점멸 효과로 표기 —
    /// 현재 선택하면 초록색으로 보이는 것처럼"</i>).
    ///
    /// ══════════════════════════════════════════════════════════════════
    /// ★★ <b>왜 컴포넌트를 새로 만들었나 — 색을 두 곳에서 칠하면 반드시 어긋난다</b>
    /// ══════════════════════════════════════════════════════════════════
    /// 지금까지 유닛 스프라이트의 색을 건드리는 코드는 <c>UnitSelector</c> 하나뿐이었고,
    /// 그 방식은 <b>"칠하기 전 색을 기억해 뒀다가 해제할 때 되돌린다"</b> 였다:
    /// <code>
    ///   _originalColor = sr.color;      // 선택할 때 기억
    ///   sr.color = selectedTint;
    ///   ...
    ///   sr.color = _originalColor;      // 해제할 때 복구
    /// </code>
    /// 여기에 점멸을 <b>그냥 하나 더 얹으면</b> 이렇게 깨진다 —
    /// 점멸이 빨갛게 칠한 순간에 그 유닛을 클릭하면 <c>_originalColor</c> 에
    /// <b>「그 순간의 빨간색」</b>이 기억되고, 선택을 풀 때 그 빨간색으로 "복구"된다.
    /// <b>정신 이상이 풀려도 캐릭터가 영원히 빨간 채로 남는다.</b>
    /// (기억-복구 방식은 칠하는 주체가 <b>하나일 때만</b> 성립한다.)
    ///
    /// 그래서 <b>기억-복구를 없애고</b>, 이 컴포넌트가 매 프레임 <b>원래 색에서 다시 계산</b>한다.
    /// 선택도 점멸도 "상태"로만 들어오고, 실제로 <c>SpriteRenderer.color</c> 에 쓰는 것은
    /// 여기 한 줄뿐이다. 표시가 몇 가지로 늘어나도 서로 덮어쓸 수 없는 구조다.
    ///
    /// ══════════════════════════════════════════════════════════════════
    /// <b>겹칠 때의 우선순위</b>
    /// ══════════════════════════════════════════════════════════════════
    /// 선택(초록)과 정신 이상(빨강)은 <b>동시에 성립한다</b> — 이상해진 캐릭터를 눌러
    /// 전술을 고치는 것이 바로 유저가 할 일이므로, 그때 표시가 사라지면 안 된다.
    /// 그래서 <b>둘을 겹쳐서</b> 보여준다: 점멸의 바닥이 선택색(또는 원래색)이고
    /// 꼭대기가 빨강이다. 점멸이 가장 옅은 순간에 "선택돼 있다"가 그대로 보인다.
    ///
    /// ⚠ <b>좋은 정신 이상(진정·각성·고조)은 점멸하지 않는다</b> — 점멸은 경고 신호다.
    /// 좋은 효과는 <c>DamageNumberFx</c> 가 노란 문구로 한 번 알려주는 것으로 끝난다
    /// (유저 지시: 나쁜 효과만 빨간 점멸).
    ///
    /// 붙는 자리는 <c>Character_Template</c> — 캐릭터는 이 템플릿 복제로 생성되므로
    /// (5절) 새 캐릭터가 자동으로 물려받는다. <c>CharacterErosion</c> 과 같은 방식이고,
    /// 같은 이유로 <see cref="EnsureOn"/> 안전망도 같이 둔다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UnitTintFx : MonoBehaviour
    {
        [Header("정신 이상 점멸")]
        [Tooltip("나쁜 정신 이상 중 스프라이트가 물드는 색")]
        [SerializeField] Color mentalBadTint = new Color(1f, 0.22f, 0.22f, 1f);

        [Tooltip("초당 점멸 횟수. 너무 빠르면 시야가 피로해지고, 너무 느리면 '깜빡인다'로 안 읽힌다")]
        [Min(0.1f)] [SerializeField] float blinkPerSecond = 2.2f;

        [Tooltip("점멸이 가장 <b>옅을</b> 때 빨간색이 섞이는 비율(0~1). " +
                 "0 이면 원래 색까지 완전히 돌아온다 — 조금 남겨두면 '계속 이상하다'가 유지된다")]
        [Range(0f, 1f)] [SerializeField] float blinkMinStrength = 0.18f;

        [Tooltip("점멸이 가장 <b>진할</b> 때 빨간색이 섞이는 비율(0~1)")]
        [Range(0f, 1f)] [SerializeField] float blinkMaxStrength = 0.92f;

        [Tooltip("끄면 점멸하지 않는다(선택 표시는 그대로 동작한다)")]
        [SerializeField] bool enableMentalBlink = true;

        SpriteRenderer _renderer;
        CharacterUnit _character;
        CharacterErosion _erosion;

        /// <summary>
        /// 아무것도 안 칠했을 때의 색. <b>한 번만</b> 읽는다 —
        /// 매번 다시 읽으면 자기가 칠한 색을 원본으로 착각한다(클래스 주석의 그 사고).
        /// </summary>
        Color _baseColor = Color.white;
        bool _baseCaptured;

        bool _selected;
        Color _selectionTint = Color.white;

        /// <summary>마지막으로 쓴 색. 값이 안 바뀌면 쓰지 않는다(머티리얼 프로퍼티 변경 절약).</summary>
        Color _written;
        bool _writtenValid;

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _character = GetComponent<CharacterUnit>();
            CaptureBase();
        }

        void CaptureBase()
        {
            if (_baseCaptured || _renderer == null) return;
            _baseCaptured = true;
            _baseColor = _renderer.color;
        }

        /// <summary>
        /// <b>안전망</b> — 템플릿에서 이 컴포넌트가 빠져도 점멸이 조용히 사라지지 않게 한다.
        /// 브랜치 재동기화로 씬이 되돌아가는 사고가 이 저장소에서 두 번 있었다(28-3·28-4절).
        /// <c>CharacterErosion.EnsureOn</c> 과 같은 이유·같은 모양.
        /// </summary>
        public static UnitTintFx EnsureOn(GameObject go)
        {
            if (go == null) return null;
            var fx = go.GetComponent<UnitTintFx>();
            if (fx == null) fx = go.AddComponent<UnitTintFx>();
            return fx;
        }

        /// <summary>
        /// 선택 상태를 알린다. <c>UnitSelector</c> 만 부른다.
        /// <b>색을 직접 쓰지 않고 상태만 바꾼다</b> — 실제 칠하기는 <see cref="LateUpdate"/> 한 곳이다.
        /// </summary>
        public void SetSelected(bool selected, Color tint)
        {
            CaptureBase();
            _selected = selected;
            _selectionTint = tint;
        }

        /// <summary>
        /// ⚠ <b>LateUpdate 인 이유</b> — 스프라이트 교체(<c>CharacterAnimator</c>)가 <c>Update</c> 에서
        /// 돈다. 같은 <c>Update</c> 단계에서 칠하면 순서에 따라 한 프레임 어긋나 보인다.
        /// 모든 <c>Update</c> 가 끝난 뒤에 칠하면 순서와 무관하게 항상 맞는다.
        /// </summary>
        void LateUpdate()
        {
            if (_renderer == null) return;
            CaptureBase();

            Color target = _selected ? _selectionTint : _baseColor;

            if (enableMentalBlink && IsBadMentalState())
            {
                // 0..1 사인파. Time.time 을 쓰므로 여러 캐릭터가 같은 박자로 깜빡인다 —
                // 여기서는 그게 낫다. 전부 같이 깜빡여야 "몇 명이 이상하다"가 한눈에 세어진다.
                float wave = (Mathf.Sin(Time.time * blinkPerSecond * Mathf.PI * 2f) + 1f) * 0.5f;
                float strength = Mathf.Lerp(blinkMinStrength, blinkMaxStrength, wave);

                // 알파는 건드리지 않는다 — 안개·페이드가 알파로 유닛을 감추는 경우가 있어서,
                // 여기서 알파를 덮으면 그 표현을 지운다.
                float alpha = target.a;
                target = Color.Lerp(target, mentalBadTint, strength);
                target.a = alpha;
            }

            if (_writtenValid && target == _written) return;
            _renderer.color = target;
            _written = target;
            _writtenValid = true;
        }

        /// <summary>
        /// 지금 <b>나쁜</b> 정신 이상 상태인가. 좋고 나쁨의 기준은
        /// <see cref="MentalErrorTypes.IsGood"/> 한 곳이다(진정·각성·고조가 좋은 효과).
        /// </summary>
        bool IsBadMentalState()
        {
            if (_character == null) return false;

            // ⚠ 캐릭터는 템플릿 복제로 생기므로 Awake 시점에 CharacterErosion 이 아직
            //   없을 수 있다. 매번 찾지 않고, 잡힐 때까지만 다시 시도한다.
            if (_erosion == null) _erosion = CharacterErosion.Of(_character);
            if (_erosion == null || !_erosion.HasActive) return false;

            return !MentalErrorTypes.IsGood(_erosion.ActiveType);
        }
    }
}
