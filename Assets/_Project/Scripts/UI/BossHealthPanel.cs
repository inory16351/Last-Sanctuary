using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Combat;
using LastSanctuary.Units;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 보스 체력바 — 웨이브 타이머(<see cref="WaveStatusPanel"/>) <b>바로 위</b>에 뜬다.
    /// 보스가 살아있는 동안에만 보이고, 죽거나 아직 등장하지 않았으면 스스로 숨는다.
    /// (그래서 씬의 <c>HUD_Wave</c> 는 이 패널 높이만큼 아래로 내려 배치돼 있다.)
    ///
    /// <b>대상 선정</b> — <see cref="UnitRegistry"/> 에서 살아있는 <see cref="MonsterUnit"/> 중
    /// 등급이 <see cref="MonsterTier.Normal"/> 이 아닌 것을 찾는다. 여럿이면 최대 체력이
    /// 가장 큰 쪽(= 그 웨이브의 주인공)을 잡는다. 스포너·웨이브 매니저에 새 참조를 만들지
    /// 않으려는 것 — 레지스트리는 이미 모든 유닛이 자동 등록되는 곳이다(진행상황 6절).
    ///
    /// <b>왜 스스로 찾는가</b> — MCP 로는 씬 오브젝트 참조를 인스펙터에 넣을 수 없다
    /// (진행상황 8절 4번). 그래서 이 프로젝트의 다른 HUD 패널과 같이 자식은 이름으로,
    /// 대상은 레지스트리로 직접 찾는다.
    /// </summary>
    public class BossHealthPanel : MonoBehaviour
    {
        [Header("하이라키 연결 (비워두면 이름으로 찾는다)")]
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] Image hpFill;
        [SerializeField] TMP_Text hpLabel;

        [Header("색")]
        [SerializeField] Color barHigh = new Color(0.92f, 0.38f, 0.38f, 1f);
        [SerializeField] Color barLow = new Color(0.62f, 0.16f, 0.20f, 1f);

        [Header("표시")]
        [Tooltip("게이지가 목표치까지 따라가는 속도(초당 비율). 0 이면 즉시")]
        [Min(0f)] [SerializeField] float fillLerpPerSecond = 2.5f;

        [Tooltip("보스가 없을 때 패널 자체를 숨긴다. 끄면 빈 바가 계속 보인다(레이아웃 확인용)")]
        [SerializeField] bool hideWhenNoBoss = true;

        [Header("칭호 (2026-08-13 신설)")]
        [Tooltip("보스 <b>칭호</b>를 이름 앞에 같이 띄운다 — 단탈리온이면 \"끝없는 형상의 군주\".\n" +
                 "문구는 표(wave_top_boss.boss_title → MonsterDefinitionSO.titleKey)에서 오고, " +
                 "칭호가 비어 있는 몬스터는 이름만 나온다.\n" +
                 "★ 라벨을 새로 만들지 않고 <b>같은 줄에 rich text 로</b> 붙인다 — 줄을 하나 더 " +
                 "만들면 Name·HpBack·Body 세 RectTransform 을 전부 다시 잡아야 하고, MCP 로 " +
                 "앵커 필드를 넣으면 조용히 무시되는 경우가 있다(준수사항 §10). 38MB 씬의 " +
                 "레이아웃을 건드리지 않는 쪽이 안전하다")]
        [SerializeField] bool showTitle = true;

        [Tooltip("칭호 글자 크기(이름 대비 %). rich text <size> 태그로 들어간다.\n" +
                 "2026-08-13 유저 요청으로 72 → 92 로 키웠다. 이름과 거의 같은 크기지만 색이 " +
                 "흐려서 이름이 먼저 읽힌다. 100 을 넘겨도 되게 상한을 150 까지 열어뒀다")]
        [Range(40, 150)] [SerializeField] int titleSizePercent = 92;

        [Tooltip("칭호 색. 이름보다 흐리게 두어 이름이 먼저 읽히도록 한다")]
        [SerializeField] Color titleColor = new Color(0.85f, 0.72f, 0.55f, 1f);

        MonsterUnit _boss;
        float _shownRatio = 1f;
        int _shownHp = -1;
        int _shownMax = -1;
        string _shownName;

        /// <summary>지금 표시 중인 보스. 없으면 null.</summary>
        public MonsterUnit Boss => _boss;

        /// <summary>
        /// 실제로 보이는 부분(<c>Body</c>). <b>이 스크립트가 붙은 오브젝트 자체를 끄면 안 된다</b> —
        /// 껐다가는 <see cref="Update"/> 가 안 돌아 다시 켤 방법이 없어진다. 그래서 배경·라벨·
        /// 게이지를 전부 자식 <c>Body</c> 안에 넣고 그 자식만 켜고 끈다.
        /// </summary>
        Transform _content;

        void Start()
        {
            _content = transform.Find("Body");
            if (_content == null)
            {
                Debug.LogError("[Boss HUD] 자식 'Body' 를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            if (nameLabel == null) nameLabel = Find<TMP_Text>("Body/Name");
            if (hpFill == null) hpFill = Find<Image>("Body/HpBack/HpFill");
            if (hpLabel == null) hpLabel = Find<TMP_Text>("Body/HpBack/HpLabel");

            if (nameLabel == null || hpFill == null)
            {
                Debug.LogError("[Boss HUD] Body/Name · Body/HpBack/HpFill 을 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            // 스프라이트가 비어 있으면 fillAmount 가 무시되어 보스 체력바가 항상 꽉 찬 것처럼
            // 보인다 — UiFillBar 문서 참조.
            UiFillBar.Prepare(hpFill);

            SetVisible(false);
        }

        void Update()
        {
            _boss = FindBoss();

            if (_boss == null)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            Refresh(_boss);
        }

        /// <summary>
        /// 살아있는 보스 중 최대 체력이 가장 큰 하나. 여러 마리가 겹치는 웨이브는
        /// 아직 없지만(표에 <c>bossCount</c> 는 1), 나중에 늘어나도 조용히 잘못 잡지 않게 해둔다.
        /// </summary>
        static MonsterUnit FindBoss()
        {
            MonsterUnit best = null;
            var all = UnitRegistry.All;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null || !all[i].IsAlive) continue;
                if (all[i] is not MonsterUnit m) continue;
                if (m.Tier == MonsterTier.Normal) continue;

                if (best == null || m.MaxHp > best.MaxHp) best = m;
            }
            return best;
        }

        void Refresh(MonsterUnit boss)
        {
            // 이름은 바뀔 일이 거의 없으니 바뀔 때만 쓴다 (TMP 는 대입할 때마다 메시를 다시 굽는다).
            string bossName = NameLine(boss);
            if (bossName != _shownName)
            {
                _shownName = bossName;
                if (nameLabel != null) nameLabel.text = bossName;
            }

            int hp = Mathf.Max(0, boss.CurrentHp);
            int max = Mathf.Max(1, boss.MaxHp);
            float target = Mathf.Clamp01((float)hp / max);

            _shownRatio = fillLerpPerSecond > 0f
                ? Mathf.MoveTowards(_shownRatio, target, fillLerpPerSecond * Time.deltaTime)
                : target;

            hpFill.fillAmount = _shownRatio;
            hpFill.color = Color.Lerp(barLow, barHigh, target);

            if (hpLabel != null && (hp != _shownHp || max != _shownMax))
            {
                _shownHp = hp;
                _shownMax = max;
                hpLabel.text = $"{hp} / {max}  ({Mathf.RoundToInt(target * 100f)}%)";
            }
        }

        /// <summary>
        /// 체력바 맨 윗줄 — <b>칭호 + 이름</b>. 유저 지시 2026-08-13:
        /// "단탈리온처럼 보스 몬스터는 소환되면 체력바에 타이틀을 붙여서 표기".
        ///
        /// 칭호가 없는 보스(중간보스 2종은 표에 칭호 칸이 채워지기 전까지 비어 있다)는
        /// <b>이름만</b> 나온다 — 빈 칭호 자리가 생기지 않게 문자열 단계에서 걸러낸다.
        /// </summary>
        string NameLine(MonsterUnit boss)
        {
            string bossName = boss.DisplayName;
            if (!showTitle) return bossName;

            string title = boss.Title;
            if (string.IsNullOrWhiteSpace(title)) return bossName;

            // TMP rich text. 색은 인스펙터 값을 그대로 16진수로 넘긴다.
            return $"<size={titleSizePercent}%><color=#{ColorUtility.ToHtmlStringRGB(titleColor)}>" +
                   $"{title}</color></size>  {bossName}";
        }

        void SetVisible(bool visible)
        {
            if (!hideWhenNoBoss) visible = true;
            if (_content == null) return;
            if (_content.gameObject.activeSelf == visible) return;

            // 새로 등장할 때는 게이지를 만피에서 시작시킨다 — 이전 보스의 잔상이 남지 않게.
            if (visible)
            {
                _shownRatio = 1f;
                _shownHp = -1;
                _shownMax = -1;
                _shownName = null;
            }

            _content.gameObject.SetActive(visible);
        }

        T Find<T>(string path) where T : Component
        {
            Transform node = transform.Find(path);
            return node != null ? node.GetComponent<T>() : null;
        }
    }
}
