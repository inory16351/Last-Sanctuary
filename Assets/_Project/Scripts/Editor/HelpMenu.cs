using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LastSanctuary.Help;
using LastSanctuary.UI;

namespace LastSanctuary.EditorTools
{
    /// <summary>
    /// ★ <b>도움말 검수 메뉴</b> (2026-08-24 신설).
    ///
    /// 조언 카드는 <b>한 번 보면 다시 안 뜬다</b>(<c>show_once</c>) — 그것이 이 기능의 요점이지만
    /// <b>기획을 검수할 때는 정반대로 걸림돌</b>이다. 「그 카드가 어떻게 떴는지」를 다시 보려면
    /// 기억을 지워야 하고, 그 기억은 <see cref="HelpService.PrefsKey"/>(PlayerPrefs)에 있어서
    /// 플레이 모드를 껐다 켜도 남는다.
    ///
    /// ⚠ <b>플레이 중이 아니어도 지워야 한다</b> — 그래서 <see cref="HelpService"/> 인스턴스를
    ///   찾지 못하면 PlayerPrefs 를 직접 지운다(에디터에서는 씬이 안 돌고 있을 때가 잦다).
    /// </summary>
    public static class HelpMenu
    {
        [MenuItem("LastSanctuary/도움말/읽은 조언 잊기 (처음부터 다시 보기)", priority = 200)]
        static void ForgetSeen()
        {
            HelpService service = Object.FindAnyObjectByType<HelpService>(FindObjectsInactive.Include);
            if (service != null)
            {
                service.ForgetAll();     // 서비스가 자기 캐시까지 비운다(그것이 중요하다)
                Debug.Log("[도움말] 읽은 조언을 잊었습니다 — 다음 판부터 처음처럼 뜹니다.");
                return;
            }

            PlayerPrefs.DeleteKey(HelpService.PrefsKey);
            PlayerPrefs.Save();
            Debug.Log("[도움말] 읽은 조언을 잊었습니다 (PlayerPrefs 직접 삭제 — 씬에 " +
                      "HelpService 가 없어서). 플레이하면 처음처럼 뜹니다.");
        }

        [MenuItem("LastSanctuary/도움말/표 확인 (항목·계기·스트링 키)", priority = 201)]
        static void DumpTable()
        {
            HelpTableSO table = HelpTableSO.Load();
            if (table == null)
            {
                Debug.LogError("[도움말] Resources/" + HelpTableSO.ResourcePath + " 를 찾지 못했습니다. " +
                               "py -3 Tools/gen_help_assets.py 를 돌리세요.");
                return;
            }

            int withTrigger = 0, missingString = 0;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[도움말] 항목 {table.entries.Count}개 · 분류 " +
                          string.Join(" · ", table.Categories()));

            for (int i = 0; i < table.entries.Count; i++)
            {
                HelpEntry e = table.entries[i];
                if (e == null) continue;
                if (e.trigger != HelpTrigger.None) withTrigger++;

                // ★ 키가 표에 없으면 StringTable 이 <b>키 이름 자체</b>를 돌려준다 —
                //   그것이 화면에 그대로 뜨므로 여기서 미리 세어 알린다.
                bool bad = !Data.StringTable.Has(e.titleKey) ||
                           !Data.StringTable.Has(e.summaryKey) ||
                           !Data.StringTable.Has(e.bodyKey);
                if (bad) missingString++;

                sb.AppendLine($"  {e.category,-4} {e.order,4}  {e.helpId,-18} " +
                              $"p{e.priority} {e.trigger}{(bad ? "   ⚠ 스트링 키 없음" : "")}");
            }

            sb.AppendLine($"  계기가 붙은 항목 {withTrigger}개 · 백과 전용 " +
                          $"{table.entries.Count - withTrigger}개 · 스트링 키가 빈 항목 {missingString}개");
            if (missingString > 0) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb.ToString());
        }

        /// <summary>
        /// ★★ <b>짚어 줄 자리가 실제로 씬에 있는지 확인한다</b> (2026-08-24).
        ///
        /// <b>왜 필요한가</b> — 표의 <c>target_path</c> 는 <b>글자열</b>이다. 오타가 나거나 씬에서
        /// 오브젝트 이름이 바뀌면 그 단계는 «이 칸은 지금 화면에 없습니다» 로 조용히 뜬다.
        /// 실행해서 하나하나 눌러 보지 않으면 알 수 없으므로 <b>에디터에서 한 번에</b> 센다.
        ///
        /// ⚠ <b>지금 «비활성» 인 것과 «없는» 것을 갈라서 보고한다</b> — 창 안의 칸은 비활성이
        ///   정상이지만(그 창을 열면 보인다), 아예 없는 것은 표를 고쳐야 하는 오류다.
        /// </summary>
        [MenuItem("LastSanctuary/도움말/짚어 줄 자리 확인 (씬 경로 검사)", priority = 202)]
        static void CheckStepTargets()
        {
            HelpTableSO table = HelpTableSO.Load();
            if (table == null)
            {
                Debug.LogError("[도움말] 표를 찾지 못했습니다. py -3 Tools/gen_help_assets.py 를 돌리세요.");
                return;
            }

            int ok = 0, inactive = 0, textOnly = 0;
            var broken = new List<string>();

            for (int i = 0; i < table.steps.Count; i++)
            {
                HelpStepRow st = table.steps[i];
                if (st == null) continue;

                if (string.IsNullOrWhiteSpace(st.targetPath)) { textOnly++; continue; }

                Transform t = Resolve(st.targetPath);
                if (t == null) broken.Add($"{st.helpId} 단계 {st.stepOrder} → {st.targetPath}");
                else if (!(t is RectTransform))
                    broken.Add($"{st.helpId} 단계 {st.stepOrder} → {st.targetPath} " +
                               "(RectTransform 이 아닙니다 — UI 가 아닌 오브젝트는 짚을 수 없습니다)");
                else if (!t.gameObject.activeInHierarchy) { inactive++; ok++; }
                else ok++;
            }

            string head = $"[도움말] 짚어 주기 {table.steps.Count}단계 — 찾은 자리 {ok}개" +
                          $"(그중 지금 비활성 {inactive}개) · 글만 보여주는 단계 {textOnly}개 · " +
                          $"못 찾은 자리 {broken.Count}개";

            if (broken.Count == 0) { Debug.Log(head + "\n  ✓ 전부 찾았습니다."); return; }

            Debug.LogWarning(head + "\n  " + string.Join("\n  ", broken));
        }

        /// <summary>
        /// ★★ <b>안내를 지금 띄워 본다</b> — 플레이 중에만 쓸 수 있다 (2026-08-24).
        ///
        /// <b>왜 필요한가</b> — 「자세히 보기」는 <b>그 상황이 처음 왔을 때</b>만 뜨는 카드에서
        /// 눌러야 한다. 「전술 지침을 처음 바꿨을 때」를 만들려면 판을 처음부터 굴려야 하고,
        /// 그러고 나서 카드를 닫아 버리면 <b>다시 볼 방법이 없다</b>(show_once).
        /// 검수는 «그 창 안을 제대로 짚는가» 를 봐야 하는데 거기 도달하는 비용이 너무 크다.
        ///
        /// ★ 그래서 <b>항목을 골라 곧바로 띄운다.</b> 창을 여는 항목 열두 개 중 가장 손이 많이
        ///   가는 셋(전술 지침 · 성장 · 유물)을 메뉴로 뽑았다.
        /// ⚠ 플레이 중이 아니면 아무 일도 하지 않는다 — 안내는 <c>ReadingPause</c> 로 시간을
        ///   멈추고 런타임 UI 좌표를 읽으므로 에디터 정지 상태에서는 뜻이 없다.
        /// </summary>
        static void TryTour(string helpId)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[도움말] 안내 시험은 <b>플레이 중</b>에만 됩니다 " +
                                 "(창 좌표를 실제로 읽어야 합니다).");
                return;
            }

            HelpTableSO table = HelpTableSO.Load();
            HelpEntry e = table != null ? table.ById(helpId) : null;
            if (e == null)
            {
                Debug.LogError($"[도움말] 항목 {helpId} 를 찾지 못했습니다.");
                return;
            }

            HelpTourPanel tour = HelpTourPanel.Instance;
            if (tour == null)
            {
                Debug.LogError("[도움말] Help_Root/HUD_HelpTour 를 찾지 못했습니다. " +
                               "py -3 Tools/mcp_build_help_ui.py 를 돌리세요.");
                return;
            }

            if (!tour.Begin(e))
                Debug.LogWarning($"[도움말] {helpId} 에는 짚어 줄 단계가 없습니다 " +
                                 "(「자세히 보기」 버튼도 뜨지 않는 항목입니다).");
            else
                Debug.Log($"[도움말] {helpId} 안내를 띄웠습니다 — 여는 창: " +
                          (string.IsNullOrEmpty(e.openPanelPath) ? "(없음)" : e.openPanelPath));
        }

        [MenuItem("LastSanctuary/도움말/안내 시험 — 전술 지침", priority = 210)]
        static void TourTactics() => TryTour("help_tactics");

        [MenuItem("LastSanctuary/도움말/안내 시험 — 강화(성장 창)", priority = 211)]
        static void TourUpgrade() => TryTour("help_upgrade");

        [MenuItem("LastSanctuary/도움말/안내 시험 — 유물 장착", priority = 212)]
        static void TourRelic() => TryTour("help_relic_equip");

        /// <summary>
        /// <see cref="HelpTourPanel"/> 과 <b>같은 규칙</b>으로 찾는다 — 비활성도 찾아야 하므로
        /// 뿌리부터 손으로 걸어간다(<c>GameObject.Find</c> 는 비활성을 못 찾는다).
        /// </summary>
        static Transform Resolve(string path)
        {
            string[] parts = path.Split('/');
            Transform node = null;

            GameObject[] roots = UnityEngine.SceneManagement.SceneManager
                                            .GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i].name == parts[0]) { node = roots[i].transform; break; }

            for (int i = 1; node != null && i < parts.Length; i++)
                node = node.Find(parts[i]);

            return node;
        }
    }
}
