using UnityEngine;
using UnityEngine.SceneManagement;
using LastSanctuary.Events;
using LastSanctuary.Units;

namespace LastSanctuary.Save
{
    /// <summary>
    /// ★★ <b>«새 판을 시작한다» 를 정의하는 단 하나의 자리</b> (2026-08-21 신설).
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  왜 이 클래스가 생겼나 — «캐릭터가 죽으면 캐릭터 생성이 안 되는» 버그
    /// ══════════════════════════════════════════════════════════════════
    /// 유저 리포트: *"캐릭터가 죽으면 캐릭터 생성이 안되는 버그있는데 이거 수정해"*.
    ///
    /// 원인은 <b>판 전역 <c>static</c> 기록이 씬을 다시 열어도 살아남는다</b>는 것이었다:
    ///
    /// * <see cref="CharacterDefinitionRegistry"/> 의 «이미 등장한 인물» 집합은
    ///   <b>죽어도 지우지 않는다</b>(재등장 금지가 그 목적이다).
    /// * 그런데 인물 정의는 <b>11개</b>뿐이다. 한 판에서 11명이 다 나오고 죽으면
    ///   그 집합이 꽉 차고, <c>Pick</c> 이 <c>null</c> 을 돌려준다.
    /// * 그러면 <c>UnitSpawner</c> 가 <b>시작 캐릭터 3명까지</b> 생성을 취소하고,
    ///   <c>CharacterCreationService.OutOfCandidates</c> 가 <b>영구히</b> 참이 된다.
    ///   → 「캐릭터 생성」 버튼이 <b>그 프로세스가 끝날 때까지</b> 죽는다.
    ///
    /// ⚠⚠ <b>«씬을 다시 여는 것» 으로는 안 비워진다.</b> 비우는 코드
    /// (<c>ResetStatics</c>)가 <see cref="RuntimeInitializeOnLoadMethod"/> 라
    /// <b>프로세스마다 한 번</b> 돌기 때문이다. 에디터에서는 플레이 진입 때 도메인
    /// 리로드가 <b>우연히</b> 비워 주고 있었다 — 그래서 빌드에서만, 또는 인게임
    /// 재시작 버튼으로만 재현되는 «안 죽는» 버그였다.
    ///
    /// ══════════════════════════════════════════════════════════════════
    ///  왜 «세 곳에 한 줄씩» 이 아니라 이 클래스인가
    /// ══════════════════════════════════════════════════════════════════
    /// 고칠 자리가 <b>넷</b>이었다. 그중 <b>하나만</b> 제대로 돼 있었다:
    ///
    /// | 새 판을 시작하는 경로 | 예전 상태 |
    /// |---|---|
    /// | 환경설정 ▸ 게임 재시작 | ✅ (2026-08-21 3차에 고쳐 둔 것) |
    /// | 패배 ▸ 다시하기        | ❌ 씬만 다시 열었다 |
    /// | 승리 ▸ 다시하기        | ❌ 씬만 다시 열었다 |
    /// | 로비 ▸ 새로하기        | ❌ 저장만 지웠다 |
    ///
    /// 나머지 셋에 <c>ResetRun()</c> 을 <b>복사해 넣지 않았다</b> — 그러면 규칙이 네 벌이
    /// 되고, 다음에 «새 판» 경로가 하나 더 생길 때 <b>또</b> 빠진다(이번이 정확히 그 일이다).
    /// 이 프로젝트가 반복해 택한 «규칙은 한 곳에» 원칙을 따라 <b>도착 지점을 하나로</b> 모았다.
    ///
    /// ★★ <b>2026-08-21 — 이 클래스의 원인 절반이 없어졌다.</b> 인물 중복 금지가
    ///   «지금 살아 있는가» 기준으로 바뀌면서 <c>CharacterDefinitionRegistry</c> 의 판 전역
    ///   <c>static</c> 기록이 사라졌다(그 클래스의 ★★★). 남은 것은 중립 사냥 수와
    ///   이벤트 지속 효과 둘이고, 이 문은 그 둘을 위해 그대로 있다.
    /// </summary>
    public static class RunResetService
    {
        /// <summary>
        /// <b>판 전역 기록만</b> 비운다 — 씬은 건드리지 않는다.
        ///
        /// 씬을 다시 열지 <b>않는</b> 경로(로비에서 게임 씬으로 «새로하기»)가 이것만 쓴다.
        /// 순서에 뜻이 있다:
        ///   ① <b>지속 보정을 먼저 걷는다</b> — 유닛에 건 보정을 «되돌릴 대상» 으로 들고
        ///      있으므로, 씬을 넘긴 뒤에는 그 유닛이 이미 파괴돼 조용히 건너뛴다.
        ///      살아 있을 때 거두는 편이 «누가 무엇을 되돌렸는가» 가 분명하다.
        ///   ② <b>판 전역 <c>static</c> 을 비운다</b> — 이 클래스의 존재 이유(맨 위 ⚠⚠).
        /// </summary>
        public static void ClearRunState()
        {
            // ① 이벤트 지속 효과 — Ver013 부터 «초» 로 남아 있어서 창을 닫는 것으로는 안 걷힌다.
            if (EventService.Instance != null) EventService.Instance.ClearRun();
            else EventRewardService.ClearAll();

            // ② 씬을 다시 열어도 살아남는 판 전역 기록.
            //   ★ 2026-08-21 — <c>CharacterDefinitionRegistry</c> 는 <b>여기서 빠졌다</b>.
            //     중복 금지의 기준이 «지금 살아 있는가» 로 바뀌면서 그 클래스의 판 전역
            //     <c>static</c> 기록이 아예 없어졌다 — 비울 것이 없다(그 클래스의 ★★).
            //     이 클래스가 생긴 원인(«등장 기록이 씬을 넘어 살아남는다»)의 뿌리가 사라진 것이다.
            NeutralKillTally.ResetRun();
        }

        /// <summary>
        /// <b>지금 판을 버리고 새 판을 시작한다</b> — 기록을 비우고, 저장을 지우고, 씬을 다시 연다.
        ///
        /// <paramref name="sceneName"/> 이 비어 있으면 <b>지금 씬</b>을 다시 연다(게임 씬 안에서
        /// 부르는 경우). 로비에서 부를 때는 게임 씬 이름을 준다.
        ///
        /// ⚠ <b><c>timeScale</c> 을 반드시 되돌린다</b> — 배속·일시정지로 0 이나 8 일 수 있고
        ///   씬을 넘겨도 유지된다. 0 인 채로 새 씬이 시작되면 «멈춘 게임» 이 된다.
        /// ⚠ <b>저장을 지운다</b> — 안 지우면 첫 자동 저장 전에 게임을 껐다 켤 때
        ///   <b>버린 판으로 되돌아간다</b>. <c>PendingLoad</c> 도 같이 비운다(남아 있으면
        ///   새 판이 그것으로 덮인다).
        /// </summary>
        public static void BeginNewRun(string sceneName = null)
        {
            ClearRunState();

            SaveService.Delete();
            SaveService.PendingLoad = null;

            Time.timeScale = 1f;
            SceneManager.LoadScene(string.IsNullOrWhiteSpace(sceneName)
                ? SceneManager.GetActiveScene().name
                : sceneName);
        }
    }
}
