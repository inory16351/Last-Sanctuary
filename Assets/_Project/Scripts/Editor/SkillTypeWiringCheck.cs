using System.Collections.Generic;
using System.Text;
using LastSanctuary.Combat;
using LastSanctuary.Units;
using UnityEditor;
using UnityEngine;

namespace LastSanctuary.EditorTools
{
    /// <summary>
    /// ★★ <b>표의 «스킬 종류» 가 코드의 enum 에 전부 닿는지 검사한다</b> (2026-08-21 신설).
    ///
    /// <b>왜 생겼나</b> — 이 프로젝트는 <b>같은 사고를 세 번</b> 겪었다:
    /// <list type="number">
    /// <item>115-6절 — 신규 캐릭터 패시브가 <c>Parse</c> 에 없어 <b>스킬을 하나도 안 싣고</b> 있었다.</item>
    /// <item>127절 — 레기미아의 <c>Tumor_explosion</c>·<c>Forced_supply</c> 가 없어 보스가
    ///       <b>패턴을 한 번도 안 썼다</b>(콘솔 경고는 떴지만 아무도 안 봤다).</item>
    /// <item>같은 날 — 「명사수」(80038)는 종류는 맞는데 <b>밸류가 전부 0</b> 이라 «+20» 이
    ///       <b>+0</b> 이 될 상태였다(정의문이 숫자를 문장에 박아 두었다).</item>
    /// </list>
    /// 셋 다 <b>파일만 봐서는 멀쩡해 보인다</b>. 그래서 «표 → 에셋 → enum» 의 마지막 한 칸을
    /// <b>기계가 확인</b>하게 만든다.
    ///
    /// <b>무엇을 보는가</b>
    /// <list type="bullet">
    /// <item><b>종류를 못 알아보는 에셋</b> — <c>Parse</c> 가 <c>None</c> 을 돌려주는 것.
    ///       이게 있으면 그 스킬은 <b>영영 발동하지 않는다</b>.</item>
    /// <item><b>밸류가 전부 0 인 에셋</b> — 종류는 맞는데 수치가 없다. «상시 0 이 뜻인» 스킬도
    ///       있으므로(예: 표가 비운 칸) <b>경고로만</b> 남긴다 — 사람이 정의문과 대조할 신호다.</item>
    /// </list>
    ///
    /// ⚠ <b>고치지는 않는다.</b> 표가 정본이므로 여기서 값을 지어내면 안 된다 —
    ///   무엇이 어긋났는지만 정확히 말한다.
    /// </summary>
    public static class SkillTypeWiringCheck
    {
        [MenuItem("LastSanctuary/검사/스킬 종류 배선 검사", priority = 90)]
        public static void Run()
        {
            var bad = new List<string>();
            var zero = new List<string>();
            int checkedCount = 0;

            // ── 캐릭터 패시브 ────────────────────────────────────────────
            foreach (PassiveSkillSO so in Resources.LoadAll<PassiveSkillSO>("PassiveSkills"))
            {
                if (so == null) continue;
                checkedCount++;
                if (PassiveSkillTypes.Parse(so.skillType) == PassiveSkillType.None)
                    bad.Add($"패시브 {so.name}: 종류 '{so.skillType}' 을 알아보지 못합니다");
                else if (AllValuesZero(so.value01, so.value02, so.value03,
                                       so.value04, so.value05, so.value06))
                    zero.Add($"패시브 {so.name} ('{so.skillType}'): 밸류가 전부 0 입니다");
            }

            // ── 보스 스킬 ────────────────────────────────────────────────
            foreach (BossSkillSO so in Resources.LoadAll<BossSkillSO>("BossSkills"))
            {
                if (so == null) continue;
                checkedCount++;
                if (so.Type == BossSkillType.None)
                    bad.Add($"보스스킬 {so.name}: 종류 '{so.skillType}' 을 알아보지 못합니다");
                else if (AllValuesZero(so.value01, so.value02, so.value03,
                                       so.value04, so.value05, so.value06))
                    zero.Add($"보스스킬 {so.name} ('{so.skillType}'): 밸류가 전부 0 입니다");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[스킬 배선 검사] 에셋 {checkedCount}개 확인");
            sb.AppendLine(bad.Count == 0
                ? "  · 종류를 못 알아보는 에셋 없음 — 전부 enum 에 닿습니다."
                : $"  ⚠⚠ 발동할 수 없는 스킬 {bad.Count}개:");
            foreach (string s in bad) sb.AppendLine("      " + s);

            if (zero.Count > 0)
            {
                sb.AppendLine($"  ⚠ 밸류가 전부 0 인 스킬 {zero.Count}개 " +
                              "— 정의문이 숫자를 문장에 박아둔 것이 아닌지 확인하세요:");
                foreach (string s in zero) sb.AppendLine("      " + s);
            }

            if (bad.Count > 0) Debug.LogError(sb.ToString());
            else if (zero.Count > 0) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb.ToString());
        }

        static bool AllValuesZero(params float[] v)
        {
            foreach (float f in v) if (Mathf.Abs(f) > 0.0001f) return false;
            return true;
        }
    }
}
