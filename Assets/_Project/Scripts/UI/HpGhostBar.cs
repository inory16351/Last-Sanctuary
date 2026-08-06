using UnityEngine;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 격투 게임(철권)식 체력바의 "잔상" 계산기.
    ///
    /// <b>왜 필요한가</b> — 이전 방식은 <c>fillAmount</c> 자체를 목표치까지 서서히 줄였다.
    /// 그러면 <b>깎인 순간에는 아무 변화가 없고</b> 막대가 뒤늦게 스르륵 줄어들 뿐이라,
    /// "지금 이만큼 맞았다"가 눈에 안 들어온다는 피드백을 받았다(유저: "깎인 부분이 없어지는 게
    /// 시각적으로 보여야 한다").
    ///
    /// 그래서 <b>두 겹</b>으로 바꿨다:
    ///   · <b>본 막대</b>(HpFill) — 실제 체력을 <b>즉시</b> 반영한다. 맞는 순간 뚝 떨어진다.
    ///   · <b>잔상 막대</b>(HpGhost, 본 막대 <i>뒤</i>에 깔린다) — 맞기 직전 값을 잠시 그대로
    ///     들고 있다가(<see cref="HoldSeconds"/>), 그 뒤 서서히 줄어 사라진다.
    /// 결과적으로 맞은 직후 "방금 깎인 구간"이 밝은 띠로 남았다가 사라진다.
    ///
    /// 회복은 반대로 <b>잔상이 즉시 따라붙는다</b> — 회복은 잔상으로 강조할 것이 없고,
    /// 잔상이 뒤에 남아 있으면 체력이 늘었는데 막대가 안 늘어난 것처럼 보인다.
    ///
    /// 시간은 <c>unscaledTime</c> 기준이다 — UI 연출이라 게임 일시정지와 무관해야 한다.
    /// </summary>
    public class HpGhostBar
    {
        /// <summary>맞은 직후 잔상을 그대로 붙들고 있는 시간(초). "방금 이만큼 깎였다"를 보여주는 구간.</summary>
        public float HoldSeconds = 0.35f;

        /// <summary>붙들기가 끝난 뒤 잔상이 줄어드는 속도(비율/초). 1.0 = 가득 찬 막대가 1초에 다 빈다.</summary>
        public float DrainPerSecond = 0.7f;

        float _ghost;
        float _holdUntil;

        /// <summary>지금 잔상 막대가 그려야 할 비율. 항상 실제 체력 비율 이상이다.</summary>
        public float Value => _ghost;

        /// <summary>애니메이션 없이 즉시 맞춘다 (행에 새 캐릭터를 물릴 때 등).</summary>
        public void Snap(float ratio)
        {
            _ghost = ratio;
            _holdUntil = 0f;
        }

        /// <summary>실제 체력이 바뀐 것을 알린다. 줄어들었으면 붙들기 시간을 새로 잡는다.</summary>
        public void SetActual(float ratio)
        {
            if (ratio < _ghost) _holdUntil = Time.unscaledTime + HoldSeconds;
            else _ghost = ratio;   // 회복 — 잔상은 즉시 따라붙는다
        }

        /// <summary>매 프레임 호출. 잔상 값이 바뀌었으면 true (그때만 다시 그리면 된다).</summary>
        public bool Tick(float actualRatio, float deltaTime)
        {
            if (_ghost <= actualRatio)
            {
                if (Mathf.Approximately(_ghost, actualRatio)) return false;
                _ghost = actualRatio;
                return true;
            }

            if (Time.unscaledTime < _holdUntil) return false;

            float before = _ghost;
            _ghost = Mathf.MoveTowards(_ghost, actualRatio, DrainPerSecond * deltaTime);
            return !Mathf.Approximately(before, _ghost);
        }
    }
}
