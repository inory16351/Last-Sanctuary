using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 중앙 건물(넥서스) 능력치. 에디터에서 직접 편집한다.
    ///
    /// 캐릭터와 같은 1~100 척도와 같은 치환 공식을 쓰되, 건물이라 체력 규모가
    /// 훨씬 커야 하므로 체력에만 별도 배율을 둔다. 배율도 퍼센트(정수)라 결과는 정수다.
    /// (능력치 100 → 1040, × 250% → 2600)
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/Units/Nexus Definition", fileName = "NexusDefinition")]
    public class NexusDefinitionSO : ScriptableObject
    {
        [Header("능력치 (1 ~ 100)")]
        [Range(1, 100)] public int hpStat = 100;
        [Range(1, 100)] public int defenseStat = 10;
        [Range(1, 100)] public int regenStat = 5;

        [Header("건물 보정")]
        [Tooltip("건물은 캐릭터보다 체력이 커야 하므로 최대 체력에만 곱하는 배율(%). " +
                 "250 이면 2.5배")]
        [Min(1)] public int hpPercent = 250;

        [Header("외형")]
        [Tooltip("한 변이 몇 타일인지. 3 이면 3x3 타일을 차지한다")]
        [Min(1)] public int footprintTiles = 3;

        [Tooltip("화면에 뜨는 이름. 클릭 초상화(UnitPortraitPanel)와 로그가 쓴다.\n" +
                 "⚠ 넥서스는 <b>표가 없는 유일한 유닛</b>이라 스트링 키가 아니라 여기 직접 적는다")]
        public string displayName = "중앙 건물";

        [Tooltip("칭호. 비우면 초상화에 칭호 줄이 안 뜬다")]
        public string title = "마지막 성역";

        [Tooltip("일러스트 이름. Resources/Illust 아래를 찾는다 (예: Nexus_illust).\n" +
                 "★ 2026-08-18 신설 — 유저 지시 \"넥서스 클릭 가능하게 만들고 일러스트 넣어서 " +
                 "ILLUST UI 에 적용\". 몬스터·중립과 <b>같은 폴더·같은 규칙</b>이다")]
        public string illustName = "Nexus_illust";

        Sprite _illust;
        bool _illustLoaded;

        /// <summary>
        /// 초상화 일러스트. <c>Resources/Illust/</c> 에서 이름으로 읽어 캐시한다 —
        /// <see cref="MonsterDefinitionSO.Illust"/> 와 같은 규칙이다.
        /// </summary>
        public Sprite Illust
        {
            get
            {
                if (_illustLoaded) return _illust;
                _illustLoaded = true;

                string n = illustName != null ? illustName.Trim() : "";
                if (n.Length == 0) return null;

                _illust = Resources.Load<Sprite>("Illust/" + n);
                if (_illust == null)
                    Debug.LogWarning($"[넥서스] 일러스트 'Resources/Illust/{n}' 을 찾지 못했습니다 — " +
                                     "파일 이름과 .meta 의 textureType(8=Sprite) 을 확인해주세요.", this);
                return _illust;
            }
        }

        /// <summary>치환된 최대 체력(정수).</summary>
        public int MaxHp(BalanceConfigSO balance) =>
            balance == null ? 0 : BalanceConfigSO.ScaleByPercent(balance.MaxHp(hpStat), hpPercent);

        /// <summary>치환된 회복 틱당 회복량(정수).</summary>
        public int RegenPerTick(BalanceConfigSO balance) =>
            balance == null ? 0 : balance.RegenPerTick(regenStat);

        /// <summary>표시용 피해 감소율(%). 정수.</summary>
        public int DefenseReductionPercent(BalanceConfigSO balance) =>
            balance == null ? 0 : balance.DefenseReductionPercent(defenseStat);
    }
}
