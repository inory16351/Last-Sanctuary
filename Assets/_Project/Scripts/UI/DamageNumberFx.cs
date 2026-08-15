using System.Collections.Generic;
using TMPro;
using UnityEngine;
using LastSanctuary.Combat;

namespace LastSanctuary.UI
{
    /// <summary>
    /// <b>맞은 자리에 피해량을 숫자로 띄운다</b> (2026-08-16, 유저 지시).
    ///
    /// <i>"캐릭터들 몬스터 공격할 때랑 맞을 때 피격 데미지랑 공격해서 들어가는 데미지 시각적으로
    /// 데미지 뜨게 만들어줘. 맞는 데미지랑 공격하는 데미지랑 숫자 다르게 해서 시각화 해주고
    /// 크리티컬도 색 다르게 하고"</i>
    ///
    /// ★ <b>세 가지를 색으로 가른다</b> — 플레이어 입장에서 뜻이 다른 숫자이기 때문이다:
    /// <code>
    ///   가한 피해   (맞은 쪽이 적)    흰색 · 보통 크기      "내가 얼마나 넣고 있나"
    ///   받은 피해   (맞은 쪽이 아군)  붉은색 · 크게 · 굵게  "지금 누가 위험한가"
    ///   치명타                        주황금색 · 더 크게    "운이 터졌다"
    /// </code>
    /// <b>받은 피해를 더 크게</b> 두는 것이 핵심이다. 난전에서 숫자가 쏟아질 때 <b>눈이 먼저
    /// 가야 하는 쪽</b>은 내가 맞고 있다는 사실이지, 내가 넣는 딜이 아니다.
    ///
    /// ★ <b>어디에 붙어 있나 — <c>GameSystems</c></b> (2026-08-16 이관)
    /// -------------------------------------------------------------
    /// 처음에는 <see cref="CombatProjectileFx"/> 처럼 <see cref="Bootstrap"/> 이 만드는
    /// <b>숨은 오브젝트</b>에만 붙였다. 그런데 그러면 <c>[SerializeField]</c> 를 달아놔도
    /// <b>인스펙터에서 고칠 자리가 없다</b> — 값이 코드 기본값으로 굳는다
    /// (유저 지적 2026-08-16: <i>"딜 타일이랑 테이큰 타일 어디서 고침"</i>).
    ///
    /// 크기·색·수명은 <b>화면에서 보고 맞추는 값</b>이라 반드시 에딧 모드에서 만질 수 있어야
    /// 한다(유저가 서식지 값에 대해 이미 확정한 원칙 — "타일 계산 값들은 에딧에서 수정할 수
    /// 있도록"). 그래서 <c>GameSystems</c> 오브젝트에 올렸다 —
    /// <c>ErosionService</c> · <c>CharacterUpgradeService</c> 와 같은 자리다.
    ///
    /// <see cref="Bootstrap"/> 은 <b>안전망으로 남겨둔다</b>: 씬에서 컴포넌트가 빠지는 사고가
    /// 이 저장소에서 실제로 두 번 있었다(28-3·28-4절 브랜치 재동기화). 씬에 있으면 그쪽
    /// <c>Awake</c> 가 먼저 돌아 <c>_instance</c> 를 채우므로 Bootstrap 은 조용히 물러난다.
    ///
    /// 폰트는 <c>Resources</c> 에서 읽는다(MCP 로는 씬에 오브젝트 참조를 넣을 수 없다 — 8절 4번).
    ///
    /// ★ <b>월드 공간 TMP</b>(UGUI 가 아니라 <see cref="TextMeshPro"/>)를 쓴다. 캔버스에 그리면
    /// 매 프레임 월드→스크린 변환을 해야 하고 유닛과의 앞뒤 관계가 깨진다. 월드에 두면
    /// 카메라가 움직여도 알아서 따라오고 정렬도 스프라이트와 같은 규칙으로 맞출 수 있다.
    ///
    /// ⚠ <b>같은 프레임에 여러 번 맞으면 숫자가 겹친다</b> — 그래서 좌우로 조금씩 흩고
    /// (<see cref="spreadTiles"/>), 같은 대상에게 연달아 뜨면 위로 조금 더 올려 시작한다.
    /// </summary>
    public class DamageNumberFx : MonoBehaviour
    {
        // ── 표시 규칙 (인스펙터가 없으므로 상수로 둔다 — Bootstrap 이 만드는 오브젝트다) ──

        [Header("색")]
        [Tooltip("적에게 <b>가한</b> 피해")]
        [SerializeField] Color dealtColor = new Color(0.96f, 0.96f, 0.92f, 1f);

        [Tooltip("아군이 <b>받은</b> 피해 — 가장 먼저 눈에 들어와야 한다")]
        [SerializeField] Color takenColor = new Color(1f, 0.34f, 0.34f, 1f);

        [Tooltip("치명타 — 가한 쪽에서 터졌을 때")]
        [SerializeField] Color criticalColor = new Color(1f, 0.78f, 0.24f, 1f);

        [Tooltip("치명타를 <b>맞았을</b> 때. 받은 피해보다 더 밝게 해서 '아팠다'가 보이게")]
        [SerializeField] Color criticalTakenColor = new Color(1f, 0.52f, 0.68f, 1f);

        // ──────────────────────────────────────────────────────────────────
        // 크기 — ★ <b>타일</b>로 적는다 (2026-08-16 수정)
        //
        // ⚠⚠ 처음에는 이 값을 TMP 의 <c>fontSize</c> 에 <b>그대로</b> 넣었다(1.5 / 2.1).
        //   그런데 <b>3D TextMeshPro 의 fontSize 는 월드 단위가 아니다</b> — 대략
        //   <c>fontSize / 10</c> 이 월드 높이가 된다. 그래서 실제로는 <b>0.15 타일</b>,
        //   캐릭터(2.1~2.3타일)의 <b>7%</b> 짜리 글자가 떴다. 화면에서 거의 안 보였다
        //   (유저 리포트: "데미지 숫자가 너무 작아서 안 보임 ... 진짜 너무 작음").
        //
        //   숫자를 손으로 키우면 다음에 또 같은 실수를 한다. <b>「몇 타일로 보이고 싶은지」만
        //   적고</b> 변환은 코드가 하게 했다 — 이 저장소가 유닛 크기·스킬 범위에서 이미
        //   쓰는 방식과 같다(61·66절 "크기 기준은 전부 타일").
        // ──────────────────────────────────────────────────────────────────

        [Header("크기 (타일 — 캐릭터가 약 2.2타일이다)")]
        [Tooltip("적에게 가한 피해의 글자 높이(타일). 캐릭터의 절반이 기준 " +
                 "(유저 확정 2026-08-16: \"캐릭터의 1/2 사이즈는 되어야\")")]
        [Min(0.1f)] [SerializeField] float dealtTiles = 1.1f;

        [Tooltip("아군이 받은 피해. 가한 것보다 크게 — 눈이 먼저 가야 하는 쪽이다")]
        [Min(0.1f)] [SerializeField] float takenTiles = 1.45f;

        [Tooltip("치명타일 때 곱하는 배수")]
        [Min(1f)] [SerializeField] float criticalScale = 1.4f;

        [Tooltip("검은 외곽선 두께(0~1). 어두운 지형 위에서도 숫자가 읽히게 한다. " +
                 "0 이면 외곽선 없음")]
        [Range(0f, 0.5f)] [SerializeField] float outlineWidth = 0.22f;

        /// <summary>
        /// 타일 → TMP <c>fontSize</c> 환산 계수. <b>실제 숫자 획의 높이</b>가 기준이다.
        ///
        /// ★ <b>어떻게 나온 값인가</b> (짐작이 아니라 폰트 에셋을 실측했다) —
        /// 3D TMP 는 글자를 이렇게 그린다:
        /// <code>
        ///   월드 높이 = 글리프 높이 / pointSize x fontSize x 0.1
        /// </code>
        /// <c>Resources/Fonts/NeoDunggeunmo SDF</c> 의 face 는
        /// <b>pointSize 32 · CapLine 20</b> 이므로 숫자(대문자 높이)는
        /// <code>
        ///   20 / 32 x fontSize x 0.1 = fontSize x 0.0625 타일
        /// </code>
        /// 즉 <b>1 타일 = fontSize 16</b>. 맵 한 칸이 1 월드 단위라 그대로 타일이 된다.
        ///
        /// ⚠ 처음에 이 계수를 <b>10</b> 으로 잡고 값도 1.5/2.1 을 <c>fontSize</c> 에 그대로
        ///   넣어서, 실제 글자가 <b>0.09 타일</b>(캐릭터의 4%)로 나왔다. 두 실수가 겹친 것이다.
        ///
        /// ⚠ <c>TMP_Text.isOrthographic</c> 을 켜면 위 식의 <c>0.1</c> 이 <b>1</b> 이 되어
        ///   글자가 <b>10배</b>가 된다. 이 프로젝트의 카메라는 직교지만 그 플래그는
        ///   <b>끈 채로 둔다</b>(기본값) — 켜면 이 계수도 같이 고쳐야 한다.
        /// </summary>
        const float FontSizePerTile = 16f;

        // ⚠ 아래 값들은 <b>글자 크기에 맞춰 같이 커져야 한다</b>. 2026-08-16 에 글자를
        //   0.15타일 → 1.1~2.0타일로 키우면서 전부 다시 잡았다 — 안 그러면 숫자끼리
        //   겹쳐 붙고 유닛 머리를 덮는다.
        [Header("움직임")]
        [Tooltip("떠 있는 시간(초). 글자가 커진 만큼 조금 길게 — 읽을 시간이 필요하다")]
        [Min(0.1f)] [SerializeField] float lifeSeconds = 0.95f;

        [Tooltip("초당 위로 올라가는 거리(타일)")]
        [SerializeField] float riseTilesPerSecond = 1.5f;

        [Tooltip("좌우로 흩는 폭(타일). 같은 프레임에 여러 숫자가 겹치는 것을 줄인다")]
        [Min(0f)] [SerializeField] float spreadTiles = 0.9f;

        [Tooltip("유닛 <b>머리 위</b> 어디에서 시작할지(타일). 유닛이 2.2타일쯤이라 " +
                 "그보다 위에서 시작해야 그림을 안 덮는다")]
        [SerializeField] float baseHeightTiles = 2.0f;

        [Tooltip("같은 대상에게 연달아 뜰 때 위로 더 올리는 간격(타일). " +
                 "글자 높이만큼 벌려야 겹쳐 읽히지 않는다")]
        [SerializeField] float stackTiles = 1.4f;

        [Header("동작")]
        [Tooltip("동시에 화면에 띄울 수 있는 최대 개수. 넘으면 가장 오래된 것부터 재활용한다 — " +
                 "난전에서 수백 개가 쌓여 프레임이 튀는 것을 막는다")]
        [Min(8)] [SerializeField] int maxLive = 64;

        [Tooltip("끄면 숫자를 아예 안 띄운다")]
        [SerializeField] bool enableNumbers = true;

        // ------------------------------------------------------------------

        class Number
        {
            public Transform Root;
            public TextMeshPro Text;
            public Vector3 From;
            public float Born;
            public float Life;
            public Color Base;
        }

        readonly List<Number> _live = new List<Number>();
        readonly Stack<Number> _pool = new Stack<Number>();

        /// <summary>대상별 "마지막으로 숫자를 띄운 시각 + 그때의 쌓임 단계".</summary>
        readonly Dictionary<DamageableUnit, (float time, int step)> _stack =
            new Dictionary<DamageableUnit, (float, int)>();

        TMP_FontAsset _font;
        System.Random _rng;

        static DamageNumberFx _instance;

        /// <summary>
        /// <b>안전망</b> — 씬의 <c>GameSystems</c> 에 이 컴포넌트가 없을 때만 스스로 만든다.
        ///
        /// ⚠ <c>AfterSceneLoad</c> 는 씬 오브젝트의 <c>Awake</c> <b>뒤에</b> 돈다. 그래서
        /// 씬에 붙어 있으면 그쪽이 이미 <see cref="_instance"/> 를 채워 여기서 조용히 물러난다 —
        /// <b>인스펙터에서 조정한 값이 이 폴백에 덮이지 않는다.</b>
        ///
        /// 여기서 만든 오브젝트의 값은 <b>코드 기본값</b>이다(고칠 자리가 없다). 그래도
        /// 남겨두는 이유는 컴포넌트가 씬에서 빠지는 사고가 실제로 두 번 있었기 때문이다
        /// (28-3·28-4절) — 그때 데미지 숫자가 통째로 사라지는 것보다는 기본값이 낫다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null) return;

            var go = new GameObject("~DamageNumberFx");
            go.hideFlags = HideFlags.DontSave;
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DamageNumberFx>();

            Debug.LogWarning("[데미지숫자] 씬(GameSystems)에 DamageNumberFx 가 없어 " +
                             "코드 기본값으로 임시 생성했습니다 — 인스펙터에서 크기를 조정하려면 " +
                             "GameSystems 에 컴포넌트를 붙여주세요.");
        }

        void Awake()
        {
            _instance = this;
            _font = HudTheme.Font;
            _rng = new System.Random(12345);

            DamageableUnit.OnAnyDamaged -= HandleDamaged;
            DamageableUnit.OnAnyDamaged += HandleDamaged;
        }

        void OnDestroy()
        {
            DamageableUnit.OnAnyDamaged -= HandleDamaged;
            if (_instance == this) _instance = null;
        }

        // ------------------------------------------------------------------

        void HandleDamaged(DamageableUnit attacker, DamageableUnit victim, int amount, bool critical)
        {
            if (!enableNumbers || victim == null || amount <= 0) return;

            // ★ "가한 / 받은" 은 <b>맞은 쪽이 누구인지</b>로 가른다 — 플레이어 시점에서
            //   아군이 맞았으면 받은 피해고, 그 외(웨이브·중립)가 맞았으면 가한 피해다.
            //   공격자를 보지 않는 이유: 지속 피해처럼 공격자가 없는 경로가 있다.
            bool taken = victim.Faction == Faction.Angel;

            Color color = critical
                ? (taken ? criticalTakenColor : criticalColor)
                : (taken ? takenColor : dealtColor);

            float tiles = (taken ? takenTiles : dealtTiles) * (critical ? criticalScale : 1f);

            Show(victim, amount, color, tiles * FontSizePerTile, critical);
        }

        void Show(DamageableUnit victim, int amount, Color color, float size, bool critical)
        {
            Number n = Rent();
            if (n == null) return;

            // ── 시작 위치 ────────────────────────────────────────────────
            // 같은 대상에게 짧은 간격으로 또 뜨면 한 단 위에서 시작한다(겹쳐 읽히지 않게).
            // ⚠ 단은 3 까지만 — 글자가 1.4타일씩 벌어지므로 4단이면 화면 위로 5타일 넘게 솟는다.
            int step = 0;
            if (_stack.TryGetValue(victim, out var last) && Time.time - last.time < lifeSeconds * 0.5f)
                step = (last.step + 1) % 3;
            _stack[victim] = (Time.time, step);

            float jitter = (float)(_rng.NextDouble() * 2.0 - 1.0) * spreadTiles;
            n.From = victim.transform.position +
                     new Vector3(jitter, baseHeightTiles + step * stackTiles, 0f);

            n.Born = Time.time;
            n.Life = lifeSeconds;
            n.Base = color;

            n.Text.text = critical ? $"{amount}!" : amount.ToString();
            n.Text.fontSize = size;
            n.Text.color = color;

            n.Root.position = n.From;
            n.Root.gameObject.SetActive(true);
            _live.Add(n);
        }

        void Update()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Number n = _live[i];
                float t = (Time.time - n.Born) / n.Life;

                if (t >= 1f)
                {
                    n.Root.gameObject.SetActive(false);
                    _pool.Push(n);
                    _live.RemoveAt(i);
                    continue;
                }

                n.Root.position = n.From + new Vector3(0f, riseTilesPerSecond * n.Life * t, 0f);

                // 뒤쪽 40% 구간에서만 사라진다 — 처음부터 흐려지면 읽기 전에 없어진다.
                float fade = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                Color c = n.Base;
                c.a = fade;
                n.Text.color = c;
            }
        }

        // ------------------------------------------------------------------

        Number Rent()
        {
            if (_pool.Count > 0) return _pool.Pop();

            if (_live.Count >= maxLive)
            {
                // 상한을 넘었다 — 가장 오래된 것을 뺏어 쓴다(새 숫자가 안 보이는 것보다 낫다).
                Number oldest = _live[0];
                _live.RemoveAt(0);
                return oldest;
            }

            var go = new GameObject("DamageNumber");
            go.transform.SetParent(transform, false);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = _font;
            tmp.alignment = TextAlignmentOptions.Center;
            // ⚠ enableWordWrapping 은 이 Unity 버전에서 obsolete 다 — textWrappingMode 를 쓴다.
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.autoSizeTextContainer = true;

            // ★ 가시성 (2026-08-16) — 크기만 키워서는 부족했다.
            //   · 굵게: 어두운 지형·유닛 위에서 획이 가늘면 뭉개진다
            //   · 검은 외곽선: 배경이 어두우면 붉은 글자가, 밝으면 흰 글자가 묻힌다.
            //     외곽선이 있으면 <b>배경이 무엇이든</b> 읽힌다.
            //   ⚠ outlineWidth 를 주면 TMP 가 <b>머티리얼 인스턴스</b>를 만든다. 풀에서
            //     재사용하는 64개뿐이고 만들어질 때 한 번만 설정하므로 비용이 늘지 않는다.
            tmp.fontStyle = FontStyles.Bold;
            if (outlineWidth > 0f)
            {
                tmp.outlineWidth = outlineWidth;
                tmp.outlineColor = new Color32(8, 6, 10, 255);
            }

            // 유닛보다 앞에 그린다 — 몬스터 그림에 숫자가 가리면 읽을 수가 없다.
            tmp.sortingLayerID = SortingLayer.NameToID("Overhead");
            tmp.sortingOrder = 200;

            // ⚠ 글자가 커졌으므로 담는 상자도 키운다 — 좁으면 TMP 가 줄을 접거나 잘라낸다.
            //   최대 크기(치명타로 받은 피해 = 1.45 x 1.4 ≈ 2타일)에 네 자리가 들어갈 만큼.
            var rect = go.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = new Vector2(16f, 5f);

            go.SetActive(false);
            return new Number { Root = go.transform, Text = tmp };
        }
    }
}
