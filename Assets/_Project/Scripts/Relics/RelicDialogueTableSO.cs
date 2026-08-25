using System.Collections.Generic;
using UnityEngine;

namespace LastSanctuary.Relics
{
    /// <summary>
    /// 발굴 창이 쓰는 <b>상황</b> — 표 <c>Dialogue</c> 시트의 <c>situation</c> 과 1:1.
    ///
    /// ⚠ 여기 없는 값이 표에 들어오면 <see cref="RelicDialogueTableSO.ParseSituation"/> 가
    ///   <see cref="None"/> 을 돌려주고 그 줄은 <b>영영 안 뜬다</b>. 표에 상황을 더하면
    ///   반드시 이 enum 에도 더할 것(이 프로젝트가 세 번 밟은 함정이다 — 115-6·127절).
    /// </summary>
    public enum RelicDialogueSituation
    {
        None = 0,

        /// <summary>발굴 칸을 눌렀을 때 뜨는 <b>본문</b>. 여기에 선택지 두 개가 붙는다.</summary>
        Discover = 1,

        /// <summary>«파러 간다» 를 골랐을 때.</summary>
        Accept = 2,

        /// <summary>«그냥 둔다» 를 골랐을 때.</summary>
        Decline = 3,

        /// <summary>발굴이 <b>끝났을 때</b>. 결과 문구 앞에 붙는다.</summary>
        Result = 4,

        /// <summary>보스를 잡아 유물이 떨어졌을 때 (발굴과 무관 · 그룹 0).</summary>
        BossDrop = 5,
    }

    /// <summary>선택지 하나가 <b>무엇을 뜻하는가</b> — 표 <c>DigChoice.choice_kind</c>.</summary>
    public enum RelicChoiceKind
    {
        None = 0,

        /// <summary>파러 간다 — 가장 가까운 캐릭터를 보낸다.</summary>
        Accept = 1,

        /// <summary>그냥 둔다 — 창만 닫는다. 칸은 그대로 남는다.</summary>
        Decline = 2,
    }

    [System.Serializable]
    public class RelicDialogueRow
    {
        public int dialogueId;

        /// <summary>«한 벌». 0 은 그룹에 속하지 않는 독립 풀(보스 드랍)이다.</summary>
        public int groupId;

        public RelicDialogueSituation situation = RelicDialogueSituation.None;

        /// <summary><see cref="RelicDialogueSituation.Discover"/> 줄만 쓴다.</summary>
        public int choiceGroupId;

        [Min(0)] public int weight = 10;

        [TextArea(2, 5)] public string script;

        /// <summary>
        /// ★ 스트링 키 <c>relic_dialogue_&lt;dialogue_id&gt;</c> (2026-08-25).
        /// 위 <see cref="script"/> 는 <b>폴백</b>이다.
        /// </summary>
        public string scriptKey = "";

        /// <summary>화면에 나오는 대사.</summary>
        public string Script => Data.StringTable.Get(scriptKey, script);
    }

    [System.Serializable]
    public class RelicChoiceRow
    {
        public int choiceGroupId;
        public int choiceId;
        public int choiceOrder;
        public RelicChoiceKind kind = RelicChoiceKind.None;
        public string choiceText;

        /// <summary>
        /// ★ 스트링 키 <c>dig_choice_text_&lt;choice_id&gt;</c> (2026-08-25).
        /// 위 <see cref="choiceText"/> 는 <b>폴백</b>이다.
        /// </summary>
        public string choiceTextKey = "";

        /// <summary>버튼에 넣는 문구.</summary>
        public string ChoiceText => Data.StringTable.Get(choiceTextKey, choiceText);
    }

    /// <summary>
    /// ★★ <b>발굴·보스 드랍 대사표</b> (2026-08-24 신설 · 표 Ver02 의 <c>Dialogue</c>·
    /// <c>DigChoice</c> 시트).
    ///
    /// <b>왜 생겼나</b> — 유저 지시: *"발굴 ui가 나와서 발굴하기를 누르면 … 이벤트 ui처럼
    /// «위험이 도사리고 있을지도 모릅니다....» yes: 가까이 가서 살펴본다 / no: 방심은 금물이다
    /// … 여러가지 대사 스크립트 만들어서 <b>확률 동일로</b> 몇가지 대사 중 랜덤으로 뜨게"*.
    ///
    /// <b>흐름</b>
    /// <code>
    ///   칸을 처음 열 때   → 그룹 하나를 뽑아 <b>그 칸에 기억해 둔다</b>(DigSite.DialogueGroup)
    ///   창을 열 때        → 그 그룹의 discover 중 하나 · 그 줄이 가리키는 선택지 그룹의 버튼들
    ///   yes / no         → 같은 그룹의 accept / decline 중 하나
    ///   발굴이 끝나면      → 같은 그룹의 result 중 하나 + DigOutcome 의 결과 문구
    /// </code>
    ///
    /// ★ <b>그룹을 칸에 기억해 두는 이유</b> — 창을 다시 열 때마다 말투가 바뀌면
    ///   «다른 자리를 보고 있나» 싶어진다. 발견의 말투와 결과의 말투는 이어져야 한다.
    /// ★ <b>같은 상황 안에서는 균등 추첨</b>이다(유저 지시의 «확률 동일로») —
    ///   그래서 표의 <c>weight</c> 가 전부 10 이다. 특정 대사를 더/덜 나오게 하고 싶으면
    ///   <b>표의 그 칸만</b> 고치면 된다(코드는 안 고쳐도 된다).
    ///
    /// ⚠ <b>손으로 고치지 말 것</b> — <c>Tools/gen_relic_assets.py</c> 가 표에서 다시 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/유물 대사표", fileName = "RelicDialogueTable")]
    public class RelicDialogueTableSO : ScriptableObject
    {
        public List<RelicDialogueRow> lines = new List<RelicDialogueRow>();
        public List<RelicChoiceRow> choices = new List<RelicChoiceRow>();

        static readonly List<RelicDialogueRow> _pick = new List<RelicDialogueRow>();

        /// <summary>표의 문자열 → enum. 모르는 값은 <see cref="RelicDialogueSituation.None"/>.</summary>
        public static RelicDialogueSituation ParseSituation(string raw) =>
            (raw ?? "").Trim().ToLowerInvariant() switch
            {
                "discover"  => RelicDialogueSituation.Discover,
                "accept"    => RelicDialogueSituation.Accept,
                "decline"   => RelicDialogueSituation.Decline,
                "result"    => RelicDialogueSituation.Result,
                "boss_drop" => RelicDialogueSituation.BossDrop,
                _           => RelicDialogueSituation.None,
            };

        public static RelicChoiceKind ParseKind(string raw) =>
            (raw ?? "").Trim().ToLowerInvariant() switch
            {
                "accept"  => RelicChoiceKind.Accept,
                "decline" => RelicChoiceKind.Decline,
                _         => RelicChoiceKind.None,
            };

        /// <summary>대사가 들어 있는 <b>그룹 번호</b>들(0 은 뺀다). 칸을 만들 때 하나 뽑는다.</summary>
        public List<int> GroupIds()
        {
            var ids = new List<int>();
            for (int i = 0; i < lines.Count; i++)
            {
                int g = lines[i].groupId;
                if (g > 0 && !ids.Contains(g)) ids.Add(g);
            }
            return ids;
        }

        /// <summary>
        /// <paramref name="group"/> 의 <paramref name="situation"/> 대사 하나를 <b>가중치로</b>
        /// 뽑는다. 표의 weight 가 전부 같으면 그것이 곧 균등 추첨이다.
        /// 없으면 빈 문자열 — <b>창은 그래도 뜬다</b>(대사 하나 때문에 발굴이 막히면 안 된다).
        /// </summary>
        public string Roll(int group, RelicDialogueSituation situation)
        {
            RelicDialogueRow row = RollRow(group, situation);
            return row != null ? row.Script : "";
        }

        public RelicDialogueRow RollRow(int group, RelicDialogueSituation situation)
        {
            _pick.Clear();
            int total = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                RelicDialogueRow r = lines[i];
                if (r == null || r.situation != situation || r.groupId != group) continue;
                if (r.weight <= 0) continue;
                _pick.Add(r);
                total += r.weight;
            }
            if (_pick.Count == 0 || total <= 0) return null;

            int roll = Random.Range(0, total);
            for (int i = 0; i < _pick.Count; i++)
            {
                roll -= _pick[i].weight;
                if (roll < 0) return _pick[i];
            }
            return _pick[_pick.Count - 1];
        }

        /// <summary>이 그룹의 discover 줄이 가리키는 <b>선택지 그룹</b>. 못 찾으면 0.</summary>
        public int ChoiceGroupOf(int group)
        {
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].groupId == group &&
                    lines[i].situation == RelicDialogueSituation.Discover &&
                    lines[i].choiceGroupId > 0)
                    return lines[i].choiceGroupId;
            return 0;
        }

        /// <summary>선택지 그룹의 버튼들을 <c>choice_order</c> 순으로. 없으면 빈 목록.</summary>
        public List<RelicChoiceRow> ChoicesOf(int choiceGroupId)
        {
            var list = new List<RelicChoiceRow>();
            for (int i = 0; i < choices.Count; i++)
                if (choices[i] != null && choices[i].choiceGroupId == choiceGroupId)
                    list.Add(choices[i]);
            list.Sort((a, b) => a.choiceOrder.CompareTo(b.choiceOrder));
            return list;
        }

        /// <summary>
        /// 표가 없거나 비었을 때 화면이 <b>텅 비지 않게</b> 하는 최소 문구.
        /// ⚠ 여기 값을 늘리지 말 것 — 문구의 정본은 표다. 이것은 «표가 없다» 는 신호다.
        /// </summary>
        public static string Fallback(RelicDialogueSituation s) => s switch
        {
            RelicDialogueSituation.Discover => "무언가 묻혀 있는 것 같습니다.",
            RelicDialogueSituation.Accept   => "천사 하나가 그 자리로 향합니다.",
            RelicDialogueSituation.Decline  => "그 자리를 그대로 둡니다.",
            RelicDialogueSituation.Result   => "파낸 자리를 확인합니다.",
            RelicDialogueSituation.BossDrop => "쓰러진 것에게서 무언가를 얻었습니다.",
            _ => "",
        };
    }
}
