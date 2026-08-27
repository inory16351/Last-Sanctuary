using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.UI;
using LastSanctuary.Units;

namespace LastSanctuary.Relics
{
    /// <summary>
    /// ★★ <b>처치 드랍</b> (2026-08-23 신설 · 유저 지시 4·5번).
    ///
    /// <code>
    ///   ④ 보스 몹은 일정확률로 고유의 에픽 유물을 드랍. 그 유물은 보스 처치로만 획득 가능
    ///   ⑤ 일반 몹 사냥 시 낮은 확률로 일반~레어 등급 유물 획득 가능
    /// </code>
    ///
    /// <b>왜 <see cref="MonoBehaviour"/> 가 아닌가</b> — 씬에 오브젝트를 하나 더 두면
    /// «누가 먼저 깨어나는가» 를 또 관리해야 한다. 이 클래스가 하는 일은 «죽었다는 소식을
    /// 듣고 한 번 굴리는 것» 뿐이라 상태가 없다. <see cref="DamageableUnit.OnAnyDied"/> 에
    /// 붙었다 떼기만 하면 된다.
    ///
    /// ⚠ <b>정적 이벤트는 반드시 떼어야 한다</b> — 이 프로젝트는 도메인 리로드를 끄고 쓰므로
    ///   붙인 채로 두면 다음 판에 <b>두 번 걸린다</b>(드랍이 두 배가 된다).
    ///   <see cref="RelicDigService"/> 가 자기 생애에 맞춰 <see cref="Hook"/>/<see cref="Unhook"/>
    ///   를 부른다.
    ///
    /// ★ <b>누가 죽였는가</b> 는 <see cref="DamageableUnit.LastAttacker"/> 로 본다 —
    ///   이 프로젝트가 이미 그 값을 «마지막으로 때린 유닛» 으로 관리하고 있다.
    ///   캐릭터가 죽인 것이 아니면(중립끼리 싸우다 죽는 등) 드랍하지 않는다.
    /// </summary>
    public static class RelicDropService
    {
        static bool _hooked;

        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            DamageableUnit.OnAnyDied += HandleDied;
        }

        public static void Unhook()
        {
            if (!_hooked) return;
            _hooked = false;
            DamageableUnit.OnAnyDied -= HandleDied;
        }

        static void HandleDied(DamageableUnit dead)
        {
            if (dead == null) return;

            // ★ 캐릭터가 죽인 것만 센다. 중립끼리 싸우다 죽거나 이벤트로 죽은 것은 제외한다.
            var killer = dead.LastAttacker as CharacterUnit;
            if (killer == null) return;

            // 처치 보상형 유물(에너지·회복)은 <b>무엇을 죽였든</b> 발동한다.
            RelicEffectService.HandleKillCredit(killer);

            RelicDigTableSO table = RelicDigTableSO.Load();
            if (table == null) return;

            switch (dead)
            {
                case MonsterUnit m:
                    HandleMonster(m, table);
                    break;
                case NeutralMonsterUnit n:
                    HandleNeutral(n, table);
                    break;
            }
        }

        static void HandleMonster(MonsterUnit m, RelicDigTableSO table)
        {
            if (m.Definition == null) return;
            int id = m.Definition.monsterId;

            // ── 웨이브 보스 : 고유 에픽 ──
            RelicDefinitionSO unique = RelicRegistry.ForBoss(id);
            if (unique != null)
            {
                TryDrop(unique, table.DropPercent("wave_boss", RelicGrade.Epic), m.DisplayName);
                return;
            }

            // ── 웨이브 일반 : 일반 → (실패하면) 레어 ──
            RollNormal(table, "wave_normal", m.DisplayName);
        }

        static void HandleNeutral(NeutralMonsterUnit n, RelicDigTableSO table)
        {
            if (n.Definition == null) return;
            int id = n.Definition.monId;

            RelicDefinitionSO unique = RelicRegistry.ForBoss(id);
            if (unique != null)
            {
                TryDrop(unique, table.DropPercent("neutral_epic", RelicGrade.Epic), n.DisplayName);
                return;
            }

            RollNormal(table, "neutral_normal", n.DisplayName);
        }

        /// <summary>
        /// 일반 몹 — <b>일반을 먼저 굴리고, 안 나오면 레어를 굴린다</b>.
        ///
        /// ⚠ 순서가 뒤바뀌면 «레어가 일반보다 흔해» 진다. 표의 확률은 «각자 독립» 이 아니라
        ///   «같은 처치 판정에서 차례로» 를 전제로 적혀 있다(Drop 시트의 설명 그대로).
        /// </summary>
        static void RollNormal(RelicDigTableSO table, string source, string victimName)
        {
            if (Roll(table.DropPercent(source, RelicGrade.Common)))
            {
                Give(RelicRegistry.RollGrade(RelicGrade.Common), victimName);
                return;
            }
            if (Roll(table.DropPercent(source, RelicGrade.Rare)))
                Give(RelicRegistry.RollGrade(RelicGrade.Rare), victimName);
        }

        /// <summary>
        /// 보스 고유 유물 — <b>이미 가지고 있으면 굴리지도 않는다</b> (2026-08-25 · 중복 금지).
        /// ★ 굴린 뒤에 버리지 않는다 — 그러면 «떴는데 안 준» 로그가 남아 헷갈린다.
        /// </summary>
        static void TryDrop(RelicDefinitionSO relic, float percent, string victimName)
        {
            if (relic == null) return;
            if (RelicInventory.Instance != null && RelicInventory.Instance.Owns(relic.relicId)) return;
            if (Roll(percent)) Give(relic, victimName);
        }

        static bool Roll(float percent) =>
            percent > 0f && Random.value * 100f < percent;

        static void Give(RelicDefinitionSO relic, string victimName)
        {
            if (relic == null || RelicInventory.Instance == null) return;

            // ⚠ 중복이면 <b>아무 말도 하지 않는다</b> — 「남겼습니다」 로그와 보스 창이
            //   뜨는데 보관함에는 안 늘어나는 것이 가장 나쁘다(2026-08-25).
            if (!RelicInventory.Instance.Grant(relic)) return;

            // ★★ <b>유물 이름과 등급을 등급 색으로</b> (2026-08-26 · 유저 지시:
            //   일반 하양 · 레어 파랑 · 에픽 보라). 로그 한 줄 안에서 색이 갈려야 하므로
            //   <b>리치 텍스트 태그</b>를 쓴다 — TMP 는 태그 색이 라벨 색을 이긴다.
            // ⚠ 색 태그까지 포함해 «형식 하나» 로 둔다 — 조각으로 나누면 영어 어순
            //   (누가 · 무엇을 · 남겼다)을 못 맞춘다.
            HudLog.Add(string.Format(
                           HudTheme.T("log_relic_dropped",
                                      "{0} 이(가) <color=#{1}>「{2}」 ({3})</color> 을(를) 남겼습니다"),
                           victimName, relic.GradeHex, relic.DisplayName,
                           RelicDefinitionSO.NameOf(relic.grade)),
                       relic.grade == RelicGrade.Epic ? HudLogKind.Good : HudLogKind.Info);

            // ★★ <b>보스가 남긴 것만 창을 띄운다</b> (2026-08-24 · 유저 지시:
            //   *"보스 유물 획득 다이얼로그도 따로 넣어주고"*).
            //
            // ⚠ <b>일반 몹 드랍에는 띄우지 않는다</b> — 한 웨이브에 수십 마리가 죽고
            //   확률이 1.2% 라 창이 계속 튀어나온다. 보스는 한 판에 몇 번뿐이다.
            if (relic.grade != RelicGrade.Epic) return;
            ShowBossDialogue(relic);
        }

        /// <summary>
        /// 보스 드랍 대사 — 표 <c>Dialogue</c> 시트의 <c>boss_drop</c> 풀에서 균등 추첨한다
        /// (그룹 0 · 발굴 흐름과 이어지지 않는 독립 풀).
        /// 창이 없으면 <b>조용히 넘어간다</b> — 로그에는 이미 남았다.
        /// </summary>
        static void ShowBossDialogue(RelicDefinitionSO relic)
        {
            var panel = UI.RelicDigPanel.Instance;
            if (panel == null) return;

            var table = Resources.Load<RelicDialogueTableSO>("Relics/RelicDialogueTable");
            string flavor = table != null
                ? table.Roll(0, RelicDialogueSituation.BossDrop) : "";
            if (string.IsNullOrWhiteSpace(flavor))
                flavor = RelicDialogueTableSO.Fallback(RelicDialogueSituation.BossDrop);

            panel.PresentBossDrop(
                flavor,
                // 제목 줄만 표를 거친다 — 뒤에 붙는 설명은 유물 데이터 자체다(문구가 아니다).
                string.Format(HudTheme.T("ui_relic_boss_drop_title", "유물 「{0}」 ({1})"),
                              relic.DisplayName, RelicDefinitionSO.NameOf(relic.grade))
                + "\n" + relic.Desc,
                relic.icon);
        }
    }
}
