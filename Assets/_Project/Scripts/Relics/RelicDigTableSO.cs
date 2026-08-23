using System.Collections.Generic;
using UnityEngine;

namespace LastSanctuary.Relics
{
    /// <summary>발굴 결과 한 줄 — 표 <c>DigOutcome</c> 시트의 한 행.</summary>
    [System.Serializable]
    public class DigOutcomeRow
    {
        [Tooltip("outcome_type — 표의 enum 문자열 그대로")]
        public string outcomeType;

        [Tooltip("weight — 가중치. 표에서 합이 100 이라 그대로 %로 읽힌다")]
        [Min(0)] public int weight;

        public int value01;
        public int value02;

        [TextArea(1, 3)] public string outcomeDesc;
        [TextArea(1, 3)] public string outcomeScript;
    }

    /// <summary>처치 드랍 한 줄 — 표 <c>Drop</c> 시트의 한 행.</summary>
    [System.Serializable]
    public class RelicDropRow
    {
        [Tooltip("kill_source — wave_normal / neutral_normal / neutral_epic / wave_boss")]
        public string killSource;

        [Tooltip("grade — common / rare / epic")]
        public RelicGrade grade = RelicGrade.Common;

        [Tooltip("percent — 처치 1회당 확률(%)")]
        [Min(0f)] public float percent;
    }

    /// <summary>
    /// 발굴 결과 추첨표와 처치 드랍표 — 표의 <c>DigOutcome</c>·<c>Drop</c> 두 시트.
    ///
    /// <b>왜 SO 하나로 두나</b> — 이 둘은 «유물 하나» 가 아니라 <b>규칙</b>이라
    /// <see cref="RelicDefinitionSO"/> 마다 나눠 담을 수 없다. 그리고 서비스에 상수로
    /// 박으면 <b>표와 코드가 갈린다</b> — 이 프로젝트가 반복해 피해 온 것이다
    /// (유저 지시: *"불가피한 경우 아니면 하드코딩 하지마"*).
    ///
    /// ⚠ <c>Tools/gen_relic_assets.py</c> 가 쓴다. 손으로 고치지 말 것.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/유물 발굴표", fileName = "RelicDigTable")]
    public class RelicDigTableSO : ScriptableObject
    {
        public List<DigOutcomeRow> outcomes = new List<DigOutcomeRow>();
        public List<RelicDropRow> drops = new List<RelicDropRow>();

        static RelicDigTableSO _instance;

        /// <summary><c>Resources/Relics/RelicDigTable</c> 를 한 번 읽어 둔다.</summary>
        public static RelicDigTableSO Load()
        {
            if (_instance == null)
            {
                _instance = Resources.Load<RelicDigTableSO>("Relics/RelicDigTable");
                if (_instance == null)
                    Debug.LogWarning("[유물] Resources/Relics/RelicDigTable 이 없습니다 — " +
                                     "py -3 Tools/gen_relic_assets.py 를 돌려주세요.");
            }
            return _instance;
        }

        /// <summary>가중치로 결과 하나를 뽑는다. 표가 비면 null.</summary>
        public DigOutcomeRow Roll()
        {
            if (outcomes == null || outcomes.Count == 0) return null;
            int total = 0;
            for (int i = 0; i < outcomes.Count; i++) total += Mathf.Max(0, outcomes[i].weight);
            if (total <= 0) return null;

            int pick = Random.Range(0, total);
            for (int i = 0; i < outcomes.Count; i++)
            {
                pick -= Mathf.Max(0, outcomes[i].weight);
                if (pick < 0) return outcomes[i];
            }
            return outcomes[outcomes.Count - 1];
        }

        /// <summary>이 처치 대상·등급의 드랍 확률(%). 표에 없으면 0.</summary>
        public float DropPercent(string killSource, RelicGrade grade)
        {
            if (drops == null) return 0f;
            for (int i = 0; i < drops.Count; i++)
                if (drops[i].grade == grade && drops[i].killSource == killSource)
                    return drops[i].percent;
            return 0f;
        }
    }
}
