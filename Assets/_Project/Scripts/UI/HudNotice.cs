using UnityEngine;

namespace LastSanctuary.UI
{
    /// <summary>중대 사건 알림의 성격. 색과 잔류 시간만 결정한다.</summary>
    public enum HudNoticeKind
    {
        /// <summary>일반 알림.</summary>
        Info,

        /// <summary>유물을 얻었다.</summary>
        Relic,

        /// <summary>보스·에픽 중립을 쓰러뜨렸다.</summary>
        Triumph,

        /// <summary>나쁜 소식 (성역 피격 등). 지금은 쓰는 곳이 없고 색만 준비해 둔다.</summary>
        Danger,
    }

    /// <summary>
    /// ★★★ <b>중대 사건 알림의 전역 통로</b> (2026-08-26 신설 · 유저 지시:
    /// *"유물 획득 시 알려주는 기능 필요. 미니맵과 허드 액션 UI 사이에 중대한 이벤트는
    /// 페이드 인 텍스트로 알려주는 기능 추가(유물 획득 / 중립 보스 토벌 등)"*).
    ///
    /// <b>왜 <see cref="HudLog"/> 를 그대로 쓰지 않는가</b> — 로그는 <b>흘러가는 기록</b>이고
    /// 알림은 <b>한 번 크게 보이고 사라지는 것</b>이다. 같은 통로에 실으면 «유물을 얻었다» 가
    /// 잡몹 처치 스무 줄에 섞여 <b>지나간다</b> — 실제로 지금까지 그랬다(유물 획득은
    /// <c>RelicDropService</c>·<c>RelicDigService</c> 가 로그 한 줄로만 남기고 있었다).
    ///
    /// ★ <b>구조는 <see cref="HudLog"/> 를 그대로 베꼈다</b> — 남기는 쪽과 보여주는 쪽을
    ///   정적 이벤트로 끊는다. 그래서 알림을 하나 더 쓰고 싶은 곳이 생겨도 UI 참조를
    ///   들고 다닐 필요가 없고, 배너가 씬에 없어도 호출부가 깨지지 않는다.
    ///
    /// ⚠ 정적 이벤트는 도메인 리로드를 꺼두면 플레이 모드를 나가도 구독이 남는다 —
    ///   <see cref="ResetOnLoad"/> 에서 비운다(<see cref="HudLog"/> 와 같은 이유·같은 방법).
    /// </summary>
    public static class HudNotice
    {
        public static event System.Action<string, HudNoticeKind> OnNotice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad() => OnNotice = null;

        /// <summary>알림 한 줄. 배너가 없으면 아무 일도 일어나지 않는다.</summary>
        public static void Show(string message, HudNoticeKind kind = HudNoticeKind.Info) =>
            OnNotice?.Invoke(message, kind);

        /// <summary>
        /// 성격별 글자색. <see cref="HudTheme"/> 의 색을 그대로 쓴다 —
        /// 알림만 다른 팔레트를 쓰면 화면 안에서 «다른 게임» 처럼 보인다.
        /// </summary>
        public static Color ColorOf(HudNoticeKind kind) => kind switch
        {
            HudNoticeKind.Relic   => HudTheme.TextHero,
            HudNoticeKind.Triumph => HudTheme.TextAccent,
            HudNoticeKind.Danger  => HudTheme.TextDanger,
            _                     => HudTheme.TextMain,
        };
    }
}
