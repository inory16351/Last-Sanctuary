using UnityEngine;

namespace LastSanctuary.UI
{
    /// <summary>로그 한 줄의 성격. 색만 결정한다.</summary>
    public enum HudLogKind
    {
        Info,      // 일반
        Good,      // 이득 (에너지 획득, 처치, 생성)
        Warn,      // 주의 (웨이브 시작, 에너지 부족)
        Danger,    // 손실 (아군 사망, 넥서스 피격)
    }

    /// <summary>
    /// HUD 로그라인에 한 줄 남기는 전역 통로.
    ///
    /// 로그를 남기는 쪽(자원·전투·생성)과 보여주는 쪽(<see cref="BattleLogPanel"/>)을
    /// 정적 이벤트로 끊어둔다. 이렇게 하면 로그를 남기려고 UI 참조를 들고 다닐 필요가 없고,
    /// HUD 가 아직 안 만들어졌거나 꺼져 있어도 호출부가 깨지지 않는다.
    /// (프로젝트가 이미 <c>DamageableUnit.OnAnyDied</c> 같은 정적 이벤트를 쓰고 있어 결이 같다.)
    ///
    /// ⚠️ 정적 이벤트는 도메인 리로드를 꺼두면 플레이 모드를 나가도 구독이 남는다.
    /// <see cref="ResetOnLoad"/> 에서 초기화한다.
    /// </summary>
    public static class HudLog
    {
        public static event System.Action<string, HudLogKind> OnLine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad() => OnLine = null;

        public static void Add(string message, HudLogKind kind = HudLogKind.Info) =>
            OnLine?.Invoke(message, kind);

        /// <summary>
        /// <b>스킬 발동 한 줄의 형식</b> — "누가 · 무슨 스킬 (덧붙일 말)".
        ///
        /// 유저 지시(2026-08-13): "로그에 스킬 쓰면 스킬 이름이랑 같이 나오게 해줘
        /// 누가 썼는지랑". 그전에는 보스는 <c>"단탈리온 — 공허의 광선!"</c>, 캐릭터는
        /// <c>"엘린의 희생 — …"</c> 처럼 <b>호출부마다 형식이 달랐고 스킬 이름도 코드에
        /// 한글로 박혀 있었다</b>(표의 스킬 이름이 바뀌어도 로그는 안 바뀐다).
        /// 형식을 여기 한 곳에 모아 두면 세 군데(보스·패시브·앞으로 생길 것)를 따로 고칠
        /// 일이 없고, 스킬 이름은 언제나 표(<c>DisplayName</c>)에서 온다.
        /// </summary>
        /// <param name="caster">시전자 표시 이름 (<c>DisplayName</c>).</param>
        /// <param name="skill">스킬 표시 이름 (<c>PassiveSkillSO/BossSkillSO.DisplayName</c>).</param>
        /// <param name="detail">"3명 피격" · "엘린 회복" 처럼 덧붙일 말. 없으면 생략된다.</param>
        public static string SkillLine(string caster, string skill, string detail = null)
        {
            string head = string.IsNullOrWhiteSpace(caster) ? skill : $"{caster} · {skill}";
            return string.IsNullOrWhiteSpace(detail) ? head : $"{head} — {detail}";
        }

        public static Color ColorOf(HudLogKind kind) => kind switch
        {
            HudLogKind.Good   => HudTheme.TextAccent,
            HudLogKind.Warn   => HudTheme.TextWarn,
            HudLogKind.Danger => HudTheme.TextDanger,
            _                 => HudTheme.TextDim,
        };
    }
}
