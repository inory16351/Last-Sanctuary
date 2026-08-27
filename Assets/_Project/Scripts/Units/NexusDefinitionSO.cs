using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 중앙 건물(성역) 능력치. 에디터에서 직접 편집한다.
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

        // ══════════════════════════════════════════════════════════════════
        //  ★★★ 이름과 칭호도 <b>스트링 표</b>를 거친다 (2026-08-27 · 184절)
        // ══════════════════════════════════════════════════════════════════
        //  유저 리포트: *"중앙건물도 번역 안됨 — 초상화 UI"*.
        //
        //  ⚠ 예전 주석은 «성역은 <b>표가 없는 유일한 유닛</b>이라 스트링 키가 아니라 여기
        //    직접 적는다» 였다. 그 말은 <b>능력치 표</b>가 없다는 뜻이었는데, 그것이
        //    «스트링 표도 못 쓴다» 로 굳어져 이 두 칸만 <b>영영 한국어</b>로 남았다 —
        //    능력치가 어디서 오는지와 <b>글자가 어디서 오는지</b>는 별개다.
        //
        //  ★ 이름은 <b>새 키를 만들지 않고</b> 「건설」 표의 <c>const_name_10001</c> 을
        //    그대로 쓴다 — 같은 건물의 같은 이름이다(179-1절 «같은 문구는 키 하나로»).
        //    ⚠ 그 시트의 <c>Const_name</c> 칸에는 <b>글자가 아니라 이 키</b>가 들어 있다.
        //      즉 <b>글자의 정본은 스트링 키 테이블</b>이고 건물 시트는 가리킬 뿐이라,
        //      이름을 바꿀 때 고칠 곳은 <b>표 한 칸과 아래 폴백</b> 둘뿐이다.
        //
        //  ★ 2026-08-27(185절) — 유저 지시로 「중앙 건물」 → <b>「성역의 심장부」</b>
        //    (영어 <b>Heart Of Sanctuary</b>). ⚠ 아래 폴백은 표의 kr 과 <b>한 글자도
        //    다르면 안 된다</b> — 어긋나면 표가 없을 때만 다른 이름이 뜬다.
        [Tooltip("이름의 스트링 키. 기본값은 「건설」 표의 중앙 건물(Const_id 10001) 칸이다.\n" +
                 "비우면 아래 displayName 리터럴을 쓴다")]
        public string nameKey = "const_name_10001";

        [Tooltip("화면에 뜨는 이름. ⚠ 이제 <b>nameKey 의 폴백</b>이다 — 표시에는 DisplayName 을 쓴다")]
        public string displayName = "성역의 심장부";

        [Tooltip("칭호의 스트링 키. 비우면 아래 title 리터럴을 쓴다")]
        public string titleKey = "ui_nexus_title";

        [Tooltip("칭호. 비우면 초상화에 칭호 줄이 안 뜬다. ⚠ 이제 <b>titleKey 의 폴백</b>이다")]
        public string title = "마지막 성역";

        /// <summary>화면에 보여줄 이름 — 스트링 표가 먼저, 없으면 리터럴
        /// (<see cref="Buildings.BuildingDefinitionSO.DisplayName"/> 과 같은 규칙).</summary>
        public string DisplayName => Data.StringTable.Get(nameKey, displayName);

        /// <summary>초상화의 칭호 줄. ⚠ <b>빈 문자열이 «칭호 없음»</b> 이라 폴백을 그대로 넘긴다.</summary>
        public string Title => Data.StringTable.Get(titleKey, title);

        [Tooltip("일러스트 이름. Resources/Illust 아래를 찾는다 (예: Nexus_illust).\n" +
                 "★ 2026-08-18 신설 — 유저 지시 \"성역 클릭 가능하게 만들고 일러스트 넣어서 " +
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
                    Debug.LogWarning($"[성역] 일러스트 'Resources/Illust/{n}' 을 찾지 못했습니다 — " +
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
