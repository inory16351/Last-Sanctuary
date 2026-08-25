using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Units
{
    /// <summary>
    /// ★★ <b>중립 몬스터 «종별 처치 수»</b> — 사냥할수록 그 종이 강해지는 규칙의 계수기
    /// (2026-08-21 · 유저 지시: *"중립 몬스터 같은 개체를 일정 마리 이상 사냥할 경우 배율이
    /// 적용 되는 로직 만들어줘 우선은 10마리당 0.1 배율 추가로 만들고(체력 말고는 상한값
    /// 웨이브 몬스터와 동일하게) 이거 에딧에서 조정가능하게 만든다음 어디에 넣었는지 알려줘"*).
    ///
    /// <b>조정하는 곳은 여기가 아니다</b> — 수치는 전부 <b>씬 컴포넌트</b>에 있다:
    /// <code>
    /// Hierarchy ▸ GameSystems ▸ Inspector ▸ Neutral Growth Service
    /// </code>
    /// (<see cref="NeutralGrowthService"/>). 이 클래스는 <b>세고 곱하는 일</b>만 한다.
    ///
    /// <list type="table">
    /// <item><term>세는 곳</term><description><see cref="NeutralMonsterUnit.OnDeath"/> →
    ///   <see cref="Record"/></description></item>
    /// <item><term>쓰는 곳</term><description><see cref="NeutralMonsterUnit.Initialize"/> →
    ///   <see cref="MultiplierFor"/> (소환 순간에 굳는다)</description></item>
    /// </list>
    ///
    /// ★ <b>왜 정적 클래스인가</b> — 씬에 컴포넌트를 하나 더 붙이면 «그 컴포넌트가 없는 씬»
    ///   에서 규칙이 조용히 사라진다. 이 값은 판 하나 동안만 살아 있으면 되고 저장 대상도
    ///   아니라, <see cref="CharacterDefinitionRegistry"/> 의 등장 기록과 <b>같은 방식</b>으로
    ///   두는 것이 이 프로젝트의 결에 맞는다.
    ///
    /// ⚠ <b>판이 바뀌면 비운다</b> — 도메인 리로드를 꺼도 정적 값은 남으므로
    ///   <see cref="RuntimeInitializeOnLoadMethod"/> 로 플레이 시작 때 한 번 지운다.
    /// </summary>
    public static class NeutralKillTally
    {
        static readonly Dictionary<int, int> _kills = new Dictionary<int, int>();

        /// <summary>한 마리 처치를 기록한다. <paramref name="monId"/> 가 0 이면 무시한다.</summary>
        public static void Record(int monId)
        {
            if (monId == 0) return;
            _kills[monId] = KillsOf(monId) + 1;
        }

        /// <summary>이 판에 이 종을 몇 마리 잡았는가.</summary>
        public static int KillsOf(int monId) =>
            monId != 0 && _kills.TryGetValue(monId, out int n) ? n : 0;

        /// <summary>
        /// 지금 이 종에게 걸리는 성장 배율. 성장이 꺼져 있거나 서비스가 없으면 <b>1</b>.
        ///
        /// <c>배율 = 1 + 한 단계값 × (처치 수 ÷ 단계당 마리 수)</c> — 정수 나눗셈이라
        /// «9마리까지는 그대로, 10마리째부터 +0.1» 이 된다(유저가 말한 «10마리당»).
        /// </summary>
        public static float MultiplierFor(int monId) => MultiplierFor(monId, 0f);

        /// <summary>
        /// ★★ <b>종별 성장 배율</b> (2026-08-24 · S6).
        ///
        /// <paramref name="perKill"/> 이 0 보다 크면 <b>그 값이 이긴다</b> —
        /// «한 마리당 이만큼» 이라는 뜻이고, 씬의
        /// <see cref="NeutralGrowthService.KillsPerStep"/>·<c>StepMultiplier</c> 를 건너뛴다.
        ///
        /// <b>왜 필요했나</b> — 밸런스 기획서가 에픽마다 다른 성장을 요구하는데
        /// (카르시노스 +1레벨/회 … 폴리르 +4~5레벨/회) 서비스는 씬에 하나뿐이라
        /// <b>종을 구분할 수 없었다</b>. 에픽은 <c>maxAlive</c> 1 이라 «10마리당» 이라는
        /// 단위 자체가 맞지 않는다 — 한 판에 열 마리 남짓만 나온다.
        ///
        /// ⚠ 0 이면 예전 그대로 전역 값으로 떨어진다(잡몹 중립은 표에 0.01 = 같은 값).
        /// ⚠ 상한(<see cref="NeutralGrowthService.MaxMultiplier"/>)은 <b>둘 다</b>에 걸린다 —
        ///   «무제한이 기본» 이라는 규약을 종별 값이 몰래 깨지 않게.
        /// </summary>
        public static float MultiplierFor(int monId, float perKill)
        {
            NeutralGrowthService cfg = NeutralGrowthService.Instance;
            if (cfg == null || !cfg.GrowthEnabled) return 1f;

            float mul;
            if (perKill > 0f)
            {
                int kills = KillsOf(monId);
                if (kills <= 0) return 1f;
                mul = 1f + perKill * kills;
            }
            else
            {
                int steps = KillsOf(monId) / cfg.KillsPerStep;
                if (steps <= 0) return 1f;
                mul = 1f + cfg.StepMultiplier * steps;
            }

            // 0 = 무제한 (몬스터 능력치 상한 칸들과 같은 규약)
            float cap = cfg.MaxMultiplier;
            return cap > 0f ? Mathf.Min(mul, cap) : mul;
        }

        /// <summary>
        /// ★★★ <b>처치 보상 에너지에 걸리는 배율</b> — <b>능력치 배율과 별개</b>다 (2026-08-25).
        ///
        /// 유저 리포트: *"몬스터들 잡을때마다 자원 성장이 너무 기하급수적으로 일어나서 밸런스가
        /// 무너짐. 몬스터의 스탯 성장과 자원 획득량 성장은 별개로 설정해야 할듯"*.
        ///
        /// <code>
        ///   자원 배율 = 1 + (능력치 배율 - 1) × EnergyGrowthRatio     ▸ 그다음 EnergyMaxMultiplier 로 자른다
        /// </code>
        ///
        /// ★ <b>«늘어난 몫» 에만 비율을 건다</b> — 배율 자체에 곱하면(<c>stat × ratio</c>) 성장이
        ///   0 일 때도 자원이 <b>줄어든다</b>. 기준선 1 은 건드리지 않는 것이 맞다.
        /// ★ 비율 1 · 상한 0 이면 <see cref="MultiplierFor(int,float)"/> 와 <b>완전히 같다</b>.
        /// ⚠ 상한은 <b>자원 쪽 상한</b>이다. 능력치 상한(<see cref="NeutralGrowthService.MaxMultiplier"/>)은
        ///   이미 <see cref="MultiplierFor(int,float)"/> 안에서 걸린 뒤라, 여기서 또 걸지 않는다.
        /// </summary>
        public static float EnergyMultiplierFor(int monId, float perKill)
        {
            NeutralGrowthService cfg = NeutralGrowthService.Instance;
            if (cfg == null || !cfg.GrowthEnabled || !cfg.ScaleEnergyReward) return 1f;

            float stat = MultiplierFor(monId, perKill);
            if (stat <= 1f) return 1f;

            float mul = 1f + (stat - 1f) * cfg.EnergyGrowthRatio;

            // 0 = 무제한 (능력치 상한 칸과 같은 규약)
            float cap = cfg.EnergyMaxMultiplier;
            return cap > 0f ? Mathf.Min(mul, cap) : mul;
        }

        /// <summary>표시·로그용 — 지금 몇 단계인가.</summary>
        public static int StepsOf(int monId)
        {
            NeutralGrowthService cfg = NeutralGrowthService.Instance;
            return cfg == null ? 0 : KillsOf(monId) / cfg.KillsPerStep;
        }

        /// <summary>새 판을 시작할 때 비운다.</summary>
        public static void ResetRun() => _kills.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _kills.Clear();
    }
}
