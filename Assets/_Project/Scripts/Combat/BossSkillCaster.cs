using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Units;

namespace LastSanctuary.Combat
{
    /// <summary>
    /// 보스가 <b>스킬을 쓴다</b>. 웨이브 몬스터 테이블 <c>Skill</c> 시트의 두 줄
    /// (130001 타락한 무덤 · 130002 공허의 광선)을 실제로 발동시키는 곳이다 —
    /// 진행상황 66-7절이 "표·문구·아트는 준비돼 있는데 발동시키는 코드가 없다"로
    /// 남겨둔 미결 111번.
    ///
    /// <b>붙는 곳</b>: 씬의 <c>Templates/Monster_Templates/Monster_Dantalian_Template</c>.
    /// 몬스터는 이 템플릿을 <c>Instantiate</c> 해서 생성되므로(진행상황 5절) 스폰되는
    /// 모든 단탈리온이 자동으로 물려받는다. <b>스킬 목록은 이 컴포넌트가 정하지 않는다</b> —
    /// <see cref="MonsterDefinitionSO.bossSkillIds"/>(표의 boss_skill_1~3)를 읽어
    /// <c>Resources/BossSkills</c> 에서 그 번호의 에셋을 찾는다. 그래서 다른 보스에
    /// 이 컴포넌트를 붙이기만 하면 그 보스의 표대로 동작하고, 스킬을 바꾸는 것도 표에서 한다.
    ///
    /// <b>기존 전투를 건드리지 않는다</b> — <see cref="UnitCombat"/> 의 FSM·타겟팅·이동에
    /// 손대지 않고 <b>옆에서</b> 돈다. 보스는 평소처럼 평타를 때리다가 쿨타임이 차면
    /// 이 컴포넌트가 광역 피해를 한 번 넣는다. 25-5절 이후 이 프로젝트가 지켜온
    /// "전투 로직을 갈아엎지 않는다"는 제약 그대로다.
    ///
    /// <b>범위의 모양</b> (스트링 테이블 <c>skill_type_desc_*</c> 그대로):
    /// "…단탈리온이 존재하는 칸을 포함하여 {가로} x {세로} 범위".
    /// 자기 칸에서 <b>조준 방향으로</b> 가로 길이만큼 뻗고, 두께는 세로다.
    ///
    /// <b>★ 2026-08-13 개정 — 조준을 4방향에서 360도로 풀었다.</b>
    /// 예전에는 조준 방향을 상·하·좌·우로 스냅했다("표의 5 x 3 이 화면에서 몇 칸인지 셀 수
    /// 있게"). 그 결과 <b>대각선에만 적이 있으면 아무도 못 맞히는</b> 문제가 실제로 나왔다
    /// (유저 리포트: "4방향에 적이 없으면 대각선 방향 적을 못 때리니까 의도랑 안 맞음").
    /// 이제 상자를 조준 방향 그대로 돌리고
    /// (<see cref="UnitRegistry.CollectEnemiesInOrientedRect"/>), 지면 연출도 같은 각도로
    /// 돌려서 <b>연출과 판정이 어떤 각도에서도 일치</b>한다. 표에서
    /// <c>range_type = Circle</c> 로 두면 방향 자체가 없는 원형으로 돈다.
    ///
    /// <b>★ 범위는 연출 원화 비율에 맞춰 다시 잡힌다</b> — 표의 상자 안에 그림을 비율
    /// 유지로 최대한 넣고(contain), 그렇게 <b>실제로 그려진 크기</b>를 피해 범위로 쓴다
    /// (<see cref="ResolveArea"/>). 66절이 유닛 콜라이더에 쓴 로직과 같은 것이다.
    /// </summary>
    /// <remarks>
    /// ★ <b>2026-08-15 — 웨이브 보스 전용에서 「보스형 유닛」으로 넓혔다.</b>
    /// 카르시노스(에픽 중립 1004)가 스킬을 갖게 되면서다. 바뀐 것은 <b>두 군데뿐</b>이다:
    /// <code>
    ///   ① [RequireComponent(MonsterUnit)] 제거 → 자기 유닛을 DamageableUnit 으로 잡는다
    ///   ② 스킬 id 를 어디서 읽는지 → IBossSkillOwner 에게 물어본다
    /// </code>
    /// 조준·범위·피해·연출은 <b>한 줄도 안 바뀌었다</b> — 그 코드는 원래 유닛 종류를
    /// 몰라도 되게 짜여 있었다(Faction 과 위치만 본다).
    /// </remarks>
    [DisallowMultipleComponent]
    public class BossSkillCaster : MonoBehaviour
    {
        [Header("스킬 목록")]
        [Tooltip("보스 스킬 정의 에셋이 든 Resources 폴더 이름. 이 안에서 " +
                 "MonsterDefinitionSO.bossSkillIds 의 번호를 찾는다")]
        [SerializeField] string skillResourceFolder = "BossSkills";

        [Header("시전 규칙")]
        [Tooltip("첫 스킬을 쓰기까지의 대기(초). 0 이면 나오자마자 광역기가 나간다.\n" +
                 "기준 시점은 아래 delayFromFirstCombat 이 정한다")]
        [Min(0f)] [SerializeField] float initialDelaySeconds = 5f;

        [Tooltip("★ 대기 시간을 <b>소환 시점이 아니라 첫 교전 시점부터</b> 센다 " +
                 "(유저 확정 2026-08-16, 미결 195번).\n\n" +
                 "⚠ 이 값이 없으면 <b>맵에 상주하는 에픽 중립</b>이 소환 직후부터 시간을 세서, " +
                 "몇 분 뒤 캐릭터가 찾아오는 순간 <b>준비 시간 없이 즉시 광역기</b>를 맞는다.\n" +
                 "웨이브 보스에게도 이득이다 — 넥서스까지 진군하는 70여 초 동안 쿨타임이 " +
                 "헛돌지 않는다.\n" +
                 "교전 판정은 DamageableUnit.IsInCombat(때렸든 맞았든) 을 그대로 쓴다")]
        [SerializeField] bool delayFromFirstCombat = true;

        [Tooltip("스킬끼리 두는 최소 간격(초). 쿨타임이 동시에 차도 두 개가 같은 프레임에 " +
                 "나가지 않게 한다 — 겹쳐 맞으면 전열이 통째로 증발한다")]
        [Min(0f)] [SerializeField] float globalCooldownSeconds = 2f;

        [Tooltip("시전 모션·범위 연출이 화면에 머무는 시간(초) — <b>스킬 표에 값이 없을 때만</b> " +
                 "쓰는 기본값이다. 스킬마다 다르게 주려면 표(Skill 시트)의 cast_time 칸을 채울 것 " +
                 "(BossSkillSO.castSeconds). 피해는 시전과 동시에 들어간다")]
        [Min(0.05f)] [SerializeField] float castSeconds = 0.55f;

        [Tooltip("이 안에 맞을 적이 하나도 없으면 시전하지 않는다(쿨타임도 안 돈다). " +
                 "끄면 허공에도 쏜다 — 연출 확인용")]
        [SerializeField] bool requireTarget = true;

        [Tooltip("★ <b>범위를 연출 원화 비율에 맞춰 다시 잡는다</b> (유저 지시 2026-08-13: " +
                 "\"이미지에 맞춰서 타격 범위 재 조정\").\n" +
                 "표의 상자(가로 x 세로 타일) 안에 <b>비율을 유지한 채</b> 원화를 최대로 넣고 " +
                 "(contain), 그렇게 <b>실제로 그려진 크기</b>를 피해 범위로 쓴다 — 66절의 " +
                 "콜라이더 로직과 완전히 같은 방식이다. 그래서 '보이는 범위 = 맞는 범위' 가 된다.\n" +
                 "끄면 표 값을 그대로 쓰고 그림만 늘어난다(비율이 깨진다)")]
        [SerializeField] bool fitAreaToSkillArt = true;

        [Header("디버그")]
        [Tooltip("시전할 때마다 콘솔에 남긴다 (HUD 로그는 이 값과 무관하게 항상 남는다)")]
        [SerializeField] bool logCasts = true;

        [Tooltip("끄면 스킬을 전혀 쓰지 않는다 — 밸런스 테스트용 스위치 " +
                 "(ErosionService.enableErosion 과 같은 결)")]
        [SerializeField] bool enableSkills = true;

        DamageableUnit _self;
        CharacterAnimator _animator;

        /// <summary>이 보스가 실제로 쓸 스킬. 정의의 id 순서 = 슬롯 번호 = 스킨의 모션 슬롯.</summary>
        readonly List<BossSkillSO> _skills = new List<BossSkillSO>();

        /// <summary>슬롯별 다음 사용 가능 시각.</summary>
        readonly List<float> _nextUseTime = new List<float>();

        /// <summary>
        /// <b>시도 순서</b> — 슬롯 인덱스를 <see cref="BossSkillSO.coolTime"/> <b>내림차순</b>으로
        /// 정렬해둔 것(유저 지시 2026-08-13: "쿨이 동시에 돌면 쿨타임이 더 긴 스킬부터 쓰도록").
        ///
        /// <b>왜 매 프레임 정렬하지 않는가</b> — 스킬 목록·쿨타임은 <see cref="EnsureResolved"/>
        /// 이후 바뀌지 않으므로, 순서를 그때 한 번만 계산해두면 <see cref="Update"/> 는 매 프레임
        /// 정렬 없이 이 순서를 그대로 읽기만 하면 된다.
        /// </summary>
        readonly List<int> _priorityOrder = new List<int>();

        /// <summary>
        /// 슬롯별 <b>실제</b> 피해 범위(타일). 표의 상자에 연출 원화를 비율 유지로 넣어
        /// (<see cref="fitAreaToSkillArt"/>) 나온 크기다 — x = 뻗는 길이, y = 두께.
        ///
        /// 원화·표가 런타임에 바뀌지 않으므로 <see cref="EnsureResolved"/> 에서 한 번만 계산한다
        /// (스킨은 <c>OnEnable</c> 에서 정해지고, 그 뒤 <see cref="EnsureResolved"/> 가 돈다).
        /// </summary>
        readonly List<Vector2> _areaTiles = new List<Vector2>();

        float _nextAnyUseTime;
        bool _resolved;

        /// <summary>
        /// 폴더 조회는 비싸므로 한 번만 읽는다 (<see cref="CharacterAnimator"/> 의 스킨 캐시와 같은 이유).
        /// </summary>
        static readonly Dictionary<string, BossSkillSO[]> _skillCache =
            new Dictionary<string, BossSkillSO[]>();

        /// <summary>범위 안 유닛 임시 버퍼 — 프레임마다 리스트를 새로 만들지 않으려고 정적으로 둔다.</summary>
        static readonly List<DamageableUnit> _scratch = new List<DamageableUnit>(16);

        /// <summary>플레이 모드를 다시 시작할 때 캐시가 남지 않게 (도메인 리로드 off 대비).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => _skillCache.Clear();

        void Awake() => _self = GetComponent<DamageableUnit>();

        void OnEnable()
        {
            // 스포너가 템플릿을 복제해 활성화하는 순간이 곧 "소환"이다. 다만 이 시점에는
            // MonsterSpawner.Initialize 가 아직 안 돌아 Definition 이 비어 있을 수 있어서,
            // 실제 해석은 첫 Update 로 미룬다(아래 EnsureResolved).
            _resolved = false;
            _skills.Clear();
            _nextUseTime.Clear();
            _priorityOrder.Clear();
            _areaTiles.Clear();
            _combatStarted = false;
            _nextAnyUseTime = Time.time + initialDelaySeconds;
        }

        /// <summary>첫 교전이 시작됐는가. <see cref="delayFromFirstCombat"/> 가 켜져 있을 때만 본다.</summary>
        bool _combatStarted;

        /// <summary>
        /// 정의에서 스킬 목록을 푼다. <b>Update 에서 늦게 하는 이유</b> — 몬스터는 프레임
        /// 중간에 <c>Instantiate</c> 된 뒤 <c>Initialize</c>/<c>Configure</c> 로 정의를 받는다.
        /// <c>Awake</c>/<c>OnEnable</c> 에서 읽으면 아직 <c>definition</c> 이 null 이다 —
        /// 이 프로젝트에서 같은 순서 함정을 세 번 겪었다(24-6·27-9·29-2절).
        /// </summary>
        void EnsureResolved()
        {
            if (_resolved) return;
            if (_self == null) _self = GetComponent<DamageableUnit>();

            // ★ 스킬 목록을 <b>유닛에게 물어본다</b>(2026-08-15). 예전에는
            //   MonsterDefinitionSO.bossSkillIds 를 직접 읽어서 웨이브 몬스터만 쓸 수 있었다.
            //   중립 에픽(카르시노스)도 같은 컴포넌트를 쓰게 하려고 한 겹 뺐다.
            var owner = GetComponent<IBossSkillOwner>();
            int[] ids = owner != null ? owner.BossSkillIds : null;

            // 정의가 아직 안 들어왔으면(스폰 직후) null 이다 — 다음 프레임에 다시 본다.
            if (owner == null || !owner.SkillsReady) return;

            _resolved = true;
            _animator = GetComponent<CharacterAnimator>();

            if (ids == null || ids.Length == 0) return;

            BossSkillSO[] all = LoadSkills(skillResourceFolder);
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] <= 0) continue;           // 표의 빈 칸은 0 이다 (boss_skill_3)

                BossSkillSO found = null;
                for (int j = 0; j < all.Length; j++)
                    if (all[j] != null && all[j].skillId == ids[i]) { found = all[j]; break; }

                if (found == null)
                {
                    Debug.LogWarning($"[보스스킬] {_self.DisplayName} 의 스킬 id {ids[i]} 에 해당하는 " +
                                     $"에셋을 Resources/{skillResourceFolder} 에서 찾지 못했습니다.", this);
                    continue;
                }
                if (!found.IsUsable)
                {
                    Debug.LogWarning($"[보스스킬] {found.name} 의 종류('{found.skillType}')를 " +
                                     "알아보지 못했습니다 — 건너뜁니다.", this);
                    continue;
                }

                _skills.Add(found);
                // 쿨타임을 처음부터 다 채워두면 소환 직후 두 개가 연달아 나간다.
                // 슬롯마다 쿨타임의 절반씩 어긋나게 시작해 두 스킬이 번갈아 나오게 한다.
                _nextUseTime.Add(Time.time + initialDelaySeconds + found.coolTime * 0.5f * _skills.Count);
            }

            // 실제 피해 범위 — 표 상자에 연출 원화를 비율 유지로 맞춘 결과(아래 ResolveArea).
            for (int i = 0; i < _skills.Count; i++) _areaTiles.Add(ResolveArea(i, _skills[i]));

            // 시도 순서 = 쿨타임 내림차순. List.Sort 는 불안정 정렬이지만 슬롯 수가 2~3개뿐이라
            // 동률(같은 쿨타임)이 실제로 문제될 일이 없다.
            for (int i = 0; i < _skills.Count; i++) _priorityOrder.Add(i);
            _priorityOrder.Sort((a, b) => _skills[b].coolTime.CompareTo(_skills[a].coolTime));

            if (logCasts && _skills.Count > 0)
                Debug.Log($"[보스스킬] {_self.DisplayName} 스킬 {_skills.Count}종 준비 " +
                          $"(쿨타임 긴 순 {string.Join(" → ", _priorityOrder.ConvertAll(i => _skills[i].DisplayName))})",
                          this);
        }

        /// <summary>
        /// 이 슬롯의 <b>실제</b> 피해 범위(타일). x = 조준 방향으로 뻗는 길이, y = 두께.
        ///
        /// <b>표 상자 → 원화 비율로 contain → 그려진 크기 = 판정 크기</b> (유저 지시 2026-08-13).
        /// 66절이 유닛 콜라이더에 쓴 로직과 <b>같은 계산</b>이다:
        /// <code>배율 = min(상자가로 / 원화가로, 상자세로 / 원화세로)</code>
        /// 한 축은 표 값과 정확히 같고 다른 축은 비율 때문에 작아진다. 그래서 표에는 사람이
        /// 외우기 쉬운 값(5 x 3 · 15 x 3)만 적으면 되고, 화면에 보이는 범위와 실제로 맞는
        /// 범위가 <b>절대 어긋나지 않는다</b>.
        ///
        /// <b>왜 에셋에 안 적어두나</b> — 원화(스킨)를 바꾸면 결과도 같이 바뀌어야 하기 때문이다.
        /// 66-2절이 콜라이더에 대해 내린 결론과 같은 이유로 <b>런타임 계산이 정본</b>이다.
        ///
        /// 연출 원화가 없는 보스(중간보스는 전용 Fx 가 없다)는 표 값을 그대로 쓴다 —
        /// 맞출 그림이 없으면 맞출 것도 없다.
        /// </summary>
        Vector2 ResolveArea(int slot, BossSkillSO skill)
        {
            // ★★ <b>원형 스킬은 상자가 정사각이다</b> (2026-08-19).
            //
            // ⚠ <c>WidthTiles</c> 는 <c>value_02</c> 인데, 원형 스킬에서 그 칸은 <b>세로가
            //   아니다</b> — 「죽음의 포효」는 넉백 타일, 「거대한 위협 포효」는 <b>피해 %</b> 다.
            //   그대로 넣으면 상자가 <c>4 x 200</c> 이 되어(실제로 로그에 그렇게 찍혔다)
            //   원화 비율 맞추기가 <b>엉뚱한 값을 기준으로</b> 돌아간다.
            //
            // 지금까지 사고가 안 난 이유는 <b>아래 fit 이 그 값을 눌러 버리고</b>, 원형 갈래가
            // <c>.x</c> 만 읽기 때문이다. 하지만 이펙트 원화가 <b>세로로 긴</b> 스킬이 하나
            // 들어오면 반지름이 그 자리에서 틀어진다 — 그 전에 막는다.
            //
            // ⚠⚠ <b>고치는 것은 세로(.y) 하나뿐이다.</b> 가로(.x)를 건드리면 안 된다 —
            //   원형 갈래(<see cref="TryCast"/>)가 <c>area.x</c> 를 <b>value_01 그대로</b>로
            //   읽어 반지름을 정한다. 여기서 지름으로 바꿔 넣으면 <b>반지름이 두 배</b>가 된다.
            //   (실제로 그렇게 고쳤다가 바로 되돌렸다.)
            //
            // 세로를 가로와 같게 두면 지금 있는 두 원형 스킬의 결과는 <b>한 픽셀도 안 바뀐다</b>:
            // 원화가 가로로 길어(2.25:1 · 1.86:1) fit 의 최소값을 <b>가로가 정하기</b> 때문이다.
            // 바뀌는 것은 <b>세로로 긴 이펙트가 들어올 때</b>뿐이고, 그때가 사고가 날 자리였다.
            // ★ 2026-08-20 — <b>부채꼴도 정사각이다.</b> 위 ⚠ 가 «그 전에 막는다» 고 적어둔
            //   사고가 실제로 들어왔다: 베일 「담배 연기」의 <c>value_02</c> 는 세로가 아니라
            //   <b>연기가 남는 초</b>(=1)다. 그대로 두면 상자가 <c>5 x 1</c> 이 되고
            //   아래 fit 의 최소값을 <b>세로가 정해</b> <c>area.x</c> 가 5 보다 작아진다
            //   → <b>반지름이 표보다 줄어든다.</b> 원형과 같이 취급하면 그 경로가 막힌다.
            bool squareBox = skill.Shape == BossSkillShape.Circle
                          || skill.Shape == BossSkillShape.SemiCircle;

            var box = new Vector2(
                skill.LengthTiles,
                squareBox ? skill.LengthTiles : skill.WidthTiles);

            if (!fitAreaToSkillArt) return box;

            Sprite[] fx = _animator != null && _animator.Skin != null
                ? _animator.Skin.SkillFx(slot)
                : null;
            if (fx == null || fx.Length == 0 || fx[0] == null) return box;

            // 맵 한 칸 = 1 월드 유닛이므로 bounds.size 가 곧 "원화가 몇 타일인지"다.
            // 여기서 실제로 쓰는 것은 <b>비율</b> 뿐이라 PPU 가 몇이든 결과가 같다.
            Vector3 art = fx[0].bounds.size;
            if (art.x <= 0.0001f || art.y <= 0.0001f) return box;

            float s = Mathf.Min(box.x / art.x, box.y / art.y);
            var fitted = new Vector2(art.x * s, art.y * s);

            if (logCasts)
                Debug.Log($"[보스스킬] {skill.DisplayName} 범위 표 {box.x:0.#}x{box.y:0.#} → " +
                          $"원화 비율({art.x / art.y:0.##}:1)에 맞춰 {fitted.x:0.##}x{fitted.y:0.##}타일", this);

            return fitted;
        }

        static BossSkillSO[] LoadSkills(string folder)
        {
            if (_skillCache.TryGetValue(folder, out BossSkillSO[] cached)) return cached;

            BossSkillSO[] loaded = Resources.LoadAll<BossSkillSO>(folder);
            if (loaded.Length == 0)
                Debug.LogWarning($"[보스스킬] Resources/{folder} 에서 스킬 정의를 찾지 못했습니다. " +
                                 "Tools/sync_tables_to_assets.py 를 돌려주세요.");

            _skillCache[folder] = loaded;
            return loaded;
        }

        void Update()
        {
            if (!enableSkills) return;

            EnsureResolved();
            if (_skills.Count == 0) return;
            if (_self == null || !_self.IsAlive) return;

            // ★ 대기 시간의 기준을 <b>첫 교전</b>으로 (미결 195번).
            //   교전이 시작되는 그 프레임에 카운트다운을 다시 찍는다.
            if (delayFromFirstCombat && !_combatStarted)
            {
                if (!_self.IsInCombat) return;          // 아직 아무도 안 찾아왔다
                _combatStarted = true;
                _nextAnyUseTime = Time.time + initialDelaySeconds;

                // 슬롯별 쿨타임도 같이 다시 찍는다 — 안 그러면 소환 직후부터 돌던 값이
                // 이미 지나 있어 교전 시작과 동시에 두 스킬이 연달아 나간다.
                for (int i = 0; i < _nextUseTime.Count; i++)
                    _nextUseTime[i] = Time.time + initialDelaySeconds + _skills[i].coolTime * 0.5f * i;
            }

            if (Time.time < _nextAnyUseTime) return;

            // 동시 발동 방지 — <see cref="_priorityOrder"/>(쿨타임 내림차순)로 시도해
            // 쿨이 같이 돌면 <b>더 긴 쿨타임의 스킬을 먼저</b> 쓴다. 그 스킬이 대상이 없어
            // 못 나가면(requireTarget) 다음 순위로 넘어간다 — 큰 스킬만 계속 막혀 있다고
            // 작은 스킬까지 영원히 못 나가게 하지 않기 위해서다.
            for (int oi = 0; oi < _priorityOrder.Count; oi++)
            {
                int slot = _priorityOrder[oi];
                if (Time.time < _nextUseTime[slot]) continue;
                if (!TryCast(slot, _skills[slot])) continue;

                _nextUseTime[slot] = Time.time + _skills[slot].coolTime;
                _nextAnyUseTime = Time.time + globalCooldownSeconds;
                return;                              // 한 프레임에 하나만
            }
        }

        // ------------------------------------------------------------------
        // 시전
        // ------------------------------------------------------------------

        /// <summary>시전했으면 true. 대상이 없어 못 썼으면 false — 쿨타임을 돌리지 않는다.</summary>
        bool TryCast(int slot, BossSkillSO skill)
        {
            // 표 값이 아니라 <b>연출 원화에 맞춘 실제 범위</b>다 (ResolveArea 참조).
            Vector2 area = slot < _areaTiles.Count
                ? _areaTiles[slot]
                : new Vector2(skill.LengthTiles, skill.WidthTiles);
            float length = area.x;
            float width = area.y;

            // ── 특수 분기 — 「제자리에서 상자/원을 낸다」로 표현되지 않는 스킬들 ──
            //    아래 두 갈래(원형·직사각형)는 전부 <b>시전자가 안 움직이고, 범위가
            //    시전자 기준</b>이라는 전제 위에 있다. 그 전제를 깨는 것만 여기서 가른다.
            if (skill.Type == BossSkillType.BindingOrb) return TryCastBindingOrb(slot, skill);
            if (skill.Type == BossSkillType.LureBlood) return TryCastLureBlood(slot, skill);

            // ★ 2026-08-20 — 베일 「담뱃대 강타」. 표의 `range_type` 은 <c>Line</c> 인데
            //   정의문은 <b>«반지름 {v1} 원형 안 {v2}명»</b> 이라 <c>value_02</c> 가
            //   <b>세로가 아니라 「명」</b>이다. 기본 직사각형 갈래에 태우면 3x1 상자가 되어
            //   전혀 다른 기술이 되므로 전용 갈래를 탄다(구속탄과 같은 이유).
            if (skill.Type == BossSkillType.PipeStrike) return TryCastPipeStrike(slot, skill);

            // ── 부채꼴 범위 (2026-08-20 신설 · 베일 「담배 연기」) ────────
            //    ⚠ <b>표 값을 그대로 넘긴다</b>(<c>area</c> 가 아니다) — 아래 주석 참조.
            if (skill.Shape == BossSkillShape.SemiCircle) return TryCastSemiCircle(slot, skill);

            // ── 원형 범위 ───────────────────────────────────────────────
            // 방향이라는 개념이 없으므로 조준도 필요 없다 — 반지름 안이면 전부 맞는다.
            if (skill.Shape == BossSkillShape.Circle)
            {
                // ⚠ <b>value_01 이 지름인 스킬과 반지름인 스킬이 섞여 있다.</b>
                //   단탈리온 계열은 지름, 카르시노스 「죽음의 포효」는 정의문이
                //   *"카르시노스 + {value_01} 반지름 타일 범위"* 라 반지름이다.
                //   그대로 반으로 나누면 포효의 실제 범위가 표의 절반이 된다.
                //   판단은 BossSkillSO.CircleValueIsRadius 한 곳에 있다.
                float radius = skill.CircleValueIsRadius ? length : length * 0.5f;
                length = radius * 2f;                     // 아래 연출·로그는 지름을 쓴다

                UnitRegistry.CollectEnemiesInRadius(transform.position, radius, _self.Faction, _scratch);

                // ★ 2026-08-20 — 대상 수 상한(바리올라 「소름 끼치는 흉터」의 «가장 가까운
                //   {v2}명»). 상한이 0 인 <b>기존 스킬 전부는 아무 일도 안 일어난다</b>.
                TrimToMaxTargets(skill);
                if (requireTarget && _scratch.Count == 0) return false;

                // 그림은 지름 x 지름 정사각으로 깐다(회전 없음 — 원형이라 방향이 없다).
                PlayFx(slot, skill, transform.position, Vector2.right,
                       length, length, NearestOf(_scratch));
                return ApplyDamage(slot, skill, length, length, "원형");
            }

            // ── 직사각형 범위 (기본) ────────────────────────────────────
            // ★ 조준을 <b>4방향으로 자르지 않는다</b>(2026-08-13 개정) — 예전에는 상·하·좌·우로
            //   스냅해서 <b>대각선에만 적이 있으면 아무도 못 맞히는</b> 문제가 있었다.
            //   이제 상자를 조준 방향 그대로 돌린다(CollectEnemiesInOrientedRect).
            DamageableUnit aim = PickAim(skill.Type, length);
            if (aim == null && requireTarget) return false;

            Vector2 dir = aim != null
                ? AimDirection(aim.transform.position - transform.position)
                : Vector2.right;

            // ★ <b>대상이 어디에 있든 항상 최대 사거리까지</b> 뻗는다 (유저 지시 2026-08-13:
            //   "긴 사거리를 가진 보스 스킬은 타격 대상이 어디에 있든 최대 사거리까지 발사").
            //   조준은 <b>방향</b>만 정하고 길이는 언제나 표(→ 원화 보정) 값 그대로다.
            //
            // 자기 칸을 <b>포함해서</b> 앞으로 뻗는다:
            //   내 칸 중심에서 앞으로 (길이-1)/2 만큼 상자 중심을 밀고, 반길이는 길이/2.
            // 그래서 상자 뒤쪽 경계가 내 칸의 뒤쪽 경계와 정확히 맞는다.
            Vector3 center = transform.position + (Vector3)(dir * ((length - 1f) * 0.5f));
            var half = new Vector2(length * 0.5f, width * 0.5f);

            UnitRegistry.CollectEnemiesInOrientedRect(center, half, dir, _self.Faction, _scratch);
            TrimToMaxTargets(skill);          // 직사각형 스킬은 지금 전부 상한 0 이라 무동작
            if (requireTarget && _scratch.Count == 0) return false;

            // 연출을 피해보다 먼저 띄운다 — 맞고 죽어 사라진 대상의 자리에도 범위가 보이게.
            PlayFx(slot, skill, center, dir, length, width, aim);
            bool cast = ApplyDamage(slot, skill, length, width, "직선");

            // ★ <b>연타 스킬</b>(카시노마 「죽음의 노래」 6타) — 나머지 타수를 시전 시간에
            //   걸쳐 나눠 넣는다. 한 프레임에 다 몰면 회복·방어가 끼어들 틈이 없고,
            //   원화(6타 연격)와도 안 맞는다.
            if (cast && skill.HitCount > 1)
                StartCoroutine(RepeatHits(slot, skill, center, dir, length, width));

            return cast;
        }

        /// <summary>
        /// 연타의 <b>2타부터</b>를 시전 시간에 걸쳐 넣는다. 상자는 <b>처음 잡은 자리에 고정</b>이다 —
        /// 보스를 따라다니게 하면 도망친 적까지 6번 다 맞는다.
        /// 매 타마다 <b>대상을 다시 모은다</b>(그 사이 들어온 적도 맞고, 죽은 적은 빠진다).
        /// </summary>
        System.Collections.IEnumerator RepeatHits(int slot, BossSkillSO skill, Vector3 center,
                                                  Vector2 dir, float length, float width)
        {
            int remaining = skill.HitCount - 1;
            float step = skill.CastSecondsOr(castSeconds) / skill.HitCount;
            var half = new Vector2(length * 0.5f, width * 0.5f);

            for (int i = 0; i < remaining; i++)
            {
                yield return new WaitForSeconds(step);
                if (_self == null || !_self.IsAlive) yield break;

                UnitRegistry.CollectEnemiesInOrientedRect(center, half, dir, _self.Faction, _scratch);
                if (_scratch.Count == 0) continue;
                ApplyDamage(slot, skill, length, width, $"연타 {i + 2}/{skill.HitCount}");
            }
        }

        // ------------------------------------------------------------------
        // 베일 (2026-08-20) — <b>대상 수가 정해진 원형</b> · <b>부채꼴</b>
        // ------------------------------------------------------------------

        /// <summary>
        /// ★ <b>「담뱃대 강타」(130009)</b> — 자기 중심 <b>반지름 <c>value_01</c></b> 원형에서
        /// <b>가장 가까운 <c>value_02</c>명</b>만 때리고 <c>value_04</c> 타일 밀어낸다.
        ///
        /// 기존 <see cref="BossSkillShape.Circle"/> 갈래를 <b>못 쓰는 이유</b>는 하나다 —
        /// 그쪽은 «반지름 안이면 전부» 인데 이 스킬은 <b>대상 수 상한</b>이 있고, 그 상한이
        /// 하필 <c>value_02</c>(= 다른 스킬에서는 「세로」)에 적혀 있다.
        /// 상한 자체는 <see cref="TrimToMaxTargets"/> 가 처리하므로 여기서는
        /// <b>원형으로 모으고 넘기는 일</b>만 한다.
        /// </summary>
        bool TryCastPipeStrike(int slot, BossSkillSO skill)
        {
            // 정의문이 «반지름» 이라 CircleValueIsRadius 가 true 다 — 그대로 반지름이다.
            // ★★ 여기에 <b>자기 몸 반지름</b>을 더한다 — 안 더하면 원이 베일 몸 속에서
            //    끝나 <b>아무도 못 맞힌다</b>(SelfBodyRadiusTiles 주석의 계산).
            float radius = Mathf.Max(0.5f, skill.value01) + SelfBodyRadiusTiles();
            float diameter = radius * 2f;

            UnitRegistry.CollectEnemiesInRadius(transform.position, radius, _self.Faction, _scratch);
            TrimToMaxTargets(skill);
            if (requireTarget && _scratch.Count == 0) return false;

            // 원형이라 방향이 없다 — 연출은 지름 x 지름 정사각으로 깐다(Circle 갈래와 같다).
            PlayFx(slot, skill, transform.position, Vector2.right,
                   diameter, diameter, NearestOf(_scratch));
            return ApplyDamage(slot, skill, diameter, diameter, "원형(대상 제한)");
        }

        /// <summary>
        /// ★ <b>「담배 연기」(130010)</b> — <b>정면 부채꼴</b>(반지름 <c>value_01</c>) 안의 적을
        /// <b>중독</b>시킨다. 직접 피해는 표가 0 이다.
        ///
        /// ★★ <b>연기가 <c>value_02</c> 초 동안 남는다</b> — 정의문이 «연기를 {v2}초간
        ///   생성합니다» 이고 *"연기를 <b>맞은</b> 캐릭터는"* 이라, <b>나중에 걸어 들어온
        ///   캐릭터도 맞아야</b> 한다. 그래서 시전 순간 한 번으로 끝내지 않고
        ///   <see cref="LingerSmoke"/> 가 그 시간 동안 같은 자리를 계속 판정한다.
        ///
        /// ★ 부채꼴은 <b>방향이 있다</b>(원형과 다르다) — 조준은 직사각형 갈래와 같은
        ///   <see cref="PickAim"/> 로 정한다. 대상이 없으면 방향을 정할 수 없으므로
        ///   («정면» 이 어디인지 알 수 없다) 시전하지 않는다.
        /// </summary>
        bool TryCastSemiCircle(int slot, BossSkillSO skill)
        {
            // 정의문이 «부채꼴의 반지름» 이라 CircleValueIsRadius 가 true 다.
            //
            // ⚠⚠ <b>반지름을 <see cref="ResolveArea"/> 의 결과가 아니라 표 값에서 직접 읽는다.</b>
            //   원형 갈래는 <c>area.x</c> 를 쓰는데(원화 비율에 맞춰 줄어들 수 있다), 부채꼴은
            //   그러면 안 된다: 표의 <c>value_02</c> 가 <b>세로가 아니라 「연기 지속 초」</b>라
            //   «표가 말하는 가로:세로 비율» 이라는 것이 <b>애초에 존재하지 않는다</b>.
            //   맞출 비율이 없는데 맞추면 <b>반지름만 조용히 줄어든다</b>.
            //   (같은 종류의 사고를 이 날 「소름 끼치는 흉터」에서 한 번 잡았다 —
            //    칸의 뜻을 확인하지 않고 자리만 보면 이렇게 된다.)
            //
            // ★ 그래서 <b>판정과 연출이 같은 값</b>을 쓴다(아래 PlayFx 도 이 반지름이다) —
            //   «보이는 범위 = 맞는 범위» 라는 이 프로젝트의 규칙은 그대로 지킨다.
            float radius = Mathf.Max(0.5f, skill.CircleValueIsRadius
                ? skill.value01
                : skill.value01 * 0.5f) + SelfBodyRadiusTiles();

            DamageableUnit aim = PickAim(skill.Type, radius);
            if (aim == null && requireTarget) return false;

            Vector2 dir = aim != null
                ? AimDirection(aim.transform.position - transform.position)
                : Vector2.right;

            UnitRegistry.CollectEnemiesInSemiCircle(transform.position, radius, dir,
                                                   _self.Faction, _scratch);
            TrimToMaxTargets(skill);
            if (requireTarget && _scratch.Count == 0) return false;

            // ★★ 연출은 <b>「앞으로 뿜는 브레스」</b>다 (유저 지시 2026-08-20:
            //    *"Pipe_smoke 의 기획 의도가 베일이 바라보는 방향으로 연기 브레스를 쏘는건데
            //    지금 에셋에 있는 담배연기 패턴 반원형 이펙트를 빼고 만들어줘"*).
            //
            //    예전에는 원화의 «반원형 범위 이펙트» 한 장을 <b>지름 x 지름 상자</b>에 깔았다.
            //    그건 «범위 표시»(바닥에 반원을 그리는 것)이지 <b>브레스가 아니다</b> —
            //    입에서 뿜는 그림이 없으니 연기가 어디서 나오는지 안 보였다.
            //
            //    지금은 <b>연기 구체 원화</b>(스킨의 투사체 칸 · 담뱃대에서 뿜는 그 그림)를
            //    <b>앞쪽 절반</b>에 깔고 조준 각도로 돌린다:
            //      · 중심을 <b>앞으로 반지름의 절반</b> 밀어 상자가 «내 앞» 만 덮게 한다
            //        (부채꼴이 실제로 판정하는 영역과 같은 쪽이다).
            //      · 세로는 지름 그대로 — 부채꼴이 좌우로 반지름만큼 벌어지기 때문이다.
            //    ⚠ 「보이는 범위 = 맞는 범위」 규칙은 유지된다: 판정도 같은 반지름의
            //      <b>앞쪽 180도</b>이고, 상자도 앞쪽 절반이다.
            float diameter = radius * 2f;
            Vector3 breathAt = transform.position + (Vector3)(dir * (radius * 0.5f));
            PlayBreath(slot, skill, breathAt, dir, radius, diameter, aim);

            bool cast = ApplyDamage(slot, skill, diameter, diameter, "부채꼴");

            // 연기가 남는 동안 계속 판정한다(위 ★★).
            if (cast && skill.SmokeSeconds > 0f)
                StartCoroutine(LingerSmoke(skill, dir, radius));

            return cast;
        }

        /// <summary>
        /// 깔린 연기가 <b>남아 있는 동안</b> 같은 부채꼴을 다시 판정한다 — 그 사이 걸어
        /// 들어온 적도 중독된다.
        ///
        /// ★ <b>부채꼴은 처음 잡은 방향·자리에 고정</b>이다(「죽음의 노래」의 연타 상자와
        ///   같은 판단) — 연기는 공기 중에 남는 것이고 보스를 따라다니지 않는다.
        ///   보스를 따라다니게 하면 도망친 적까지 계속 중독된다.
        ///
        /// ★ <b>피해는 다시 넣지 않는다</b> — 중독만 다시 건다. 연기의 직접 피해는
        ///   표가 0 이지만(<c>value_03</c>), 표가 채워지더라도 «연기 안에 서 있으면 매
        ///   프레임 평타» 가 되면 안 된다. 초당 피해는 「중독」쪽이 담당한다.
        ///
        /// ⚠ 중독은 <b>중첩되지 않는다</b>(<see cref="UnitCombat.ApplyPoison"/>) —
        ///   그래서 매 프레임 다시 걸어도 «더 아픈 쪽 + 지속시간 갱신» 으로만 끝난다.
        /// </summary>
        System.Collections.IEnumerator LingerSmoke(BossSkillSO skill, Vector2 dir, float radius)
        {
            Vector3 center = transform.position;
            float until = Time.time + skill.SmokeSeconds;

            while (Time.time < until)
            {
                yield return null;
                if (_self == null || !_self.IsAlive) yield break;

                UnitRegistry.CollectEnemiesInSemiCircle(center, radius, dir, _self.Faction, _scratch);
                for (int i = 0; i < _scratch.Count; i++)
                {
                    DamageableUnit u = _scratch[i];
                    if (u == null || !u.IsAlive) continue;
                    ApplyPoison(skill, u);
                }
            }
        }

        /// <summary>
        /// ★★ <b>시전자 자기 몸의 반지름</b>(타일). 없으면 0 (2026-08-20 신설).
        ///
        /// <b>왜 필요한가 — 「담뱃대 강타」의 넉백이 한 번도 안 걸렸다</b>
        /// -----------------------------------------------------------
        /// 유저 리포트: *"담뱃대 휘두르기 스킬에 넉백이 구현이 안되어있어"*. 넉백 코드는
        /// 있었지만 <b>스킬이 아무도 못 맞히고 있었다</b>:
        /// <code>
        ///   베일 콜라이더 15 x 10  →  BodyRadiusTiles = min(15,10)/2 = <b>5.0타일</b>
        ///   근접 캐릭터는 그 <b>몸 표면</b>에 붙어 선다(UnitCombat.TargetRadius)
        ///     = 베일 <b>중심에서 5타일 밖</b>
        ///   그런데 표의 반지름은 <b>3</b> — 원이 <b>베일 몸 속에서 끝난다.</b>
        /// </code>
        /// 즉 «반지름 3» 을 <b>중심에서</b> 재면 이 스킬은 원리적으로 발동할 수 없다.
        ///
        /// ★ <b>표가 스스로 「보스 + N」이라고 적어놨다.</b> 다른 보스 스킬의 정의문을 보면
        ///   전부 그 형식이다: *"카르시노스 <b>+</b> {value_01} 반지름 타일 범위"* ·
        ///   *"아니사킬 <b>+</b> {value_01}"* · *"라린길이 <b>+</b> {value_01}"* ·
        ///   *"바리올라 <b>+</b> {value_01} 지름"*. 즉 <b>몸집에 더하는 값</b>이라는 뜻이다.
        ///   그래서 몸 반지름을 더한다 — 표를 그대로 읽는 것이다.
        ///
        /// ⚠ <b>지금은 베일의 두 스킬에만 적용한다.</b> 다른 넷(할퀴기·죽음의 포효·거대한
        ///   위협 포효·아우성)은 <b>이미 나가 있는 밸런스</b>라, 같은 규칙을 소급하면 범위가
        ///   커진다(카르시노스 몸 반지름 1.15 → 포효 5 가 6.15 로 +23%). 그건 밸런스 변경이라
        ///   유저가 정할 일이다 — 발견 사실만 남긴다.
        /// </summary>
        float SelfBodyRadiusTiles()
        {
            if (_self is Units.MonsterUnit m) return Mathf.Max(0f, m.BodyRadiusTiles);
            if (_self is Units.NeutralMonsterUnit n) return Mathf.Max(0f, n.BodyRadiusTiles);
            return 0f;
        }

        /// <summary>
        /// 대상 목록을 <see cref="BossSkillSO.MaxTargets"/> 명으로 <b>가까운 순</b>으로 자른다.
        /// 상한이 0(제한 없음)이거나 이미 그 아래면 아무 일도 하지 않는다 —
        /// <b>기존 스킬 전부가 그 경우</b>라 동작이 바뀌지 않는다.
        /// </summary>
        void TrimToMaxTargets(BossSkillSO skill)
        {
            int cap = skill.MaxTargets;
            if (cap <= 0 || _scratch.Count <= cap) return;

            Vector3 me = transform.position;
            _scratch.Sort((a, b) =>
            {
                float da = a == null ? float.MaxValue : ((Vector2)(a.transform.position - me)).sqrMagnitude;
                float db = b == null ? float.MaxValue : ((Vector2)(b.transform.position - me)).sqrMagnitude;
                return da.CompareTo(db);
            });
            _scratch.RemoveRange(cap, _scratch.Count - cap);
        }

        // ------------------------------------------------------------------
        // 말파스 「구속탄」 — <b>터지는 자리가 시전자가 아니다</b> (2026-08-18)
        // ------------------------------------------------------------------

        /// <summary>
        /// 가장 가까운 적에게 탄환을 날리고, <b>그 적의 자리</b>를 중심으로 원형 폭발을 낸다.
        ///
        /// ★ 기존 <see cref="BossSkillShape.Circle"/> 분기를 못 쓴다 — 그쪽은 원의 중심이
        ///   언제나 <b>시전자</b>다. 정의문은 *"맞은 적을 기준으로 {value_02} 반지름 타일
        ///   범위의 모든 적에게"* 라 중심이 대상 쪽이다.
        ///
        /// ★ <b>탐색 거리는 표에 없다</b> — <c>value_01</c> 은 탄환 크기지 사거리가 아니다.
        ///   그래서 이 유닛의 인식 범위(<see cref="UnitCombat.EffectiveDetectRange"/>)를 쓴다.
        ///   "보스가 인식한 적에게 쏜다" 가 표에 없는 값을 지어내는 것보다 낫다.
        /// </summary>
        bool TryCastBindingOrb(int slot, BossSkillSO skill)
        {
            DamageableUnit aim = NearestWithin(SeekRangeTiles());
            if (aim == null) return requireTarget ? false : true;

            Vector3 burst = aim.transform.position;
            float radius = skill.BlastRadiusTiles;
            float diameter = radius * 2f;

            UnitRegistry.CollectEnemiesInRadius(burst, radius, _self.Faction, _scratch);

            // 탄환 → 폭발 순서로 보여준다. 피해는 아래에서 <b>즉시</b> 들어간다
            // (이 프로젝트의 보스 스킬은 전부 시전과 동시에 판정한다 — PlayTravel 주석).
            PlayProjectile(slot, skill, transform.position, burst,
                           Mathf.Max(0.5f, skill.LengthTiles));
            PlayFx(slot, skill, burst, Vector2.right, diameter, diameter, aim);
            return ApplyDamage(slot, skill, diameter, diameter, "구속탄 폭발");
        }

        /// <summary>
        /// 이 슬롯의 <b>스킬 전용 탄환</b>을 <paramref name="from"/> → <paramref name="to"/> 로
        /// 흘려보낸다. 스킨에 그 칸이 비어 있으면 평타 탄환으로 떨어지고, 그것도 없으면
        /// 조용히 넘어간다(피해는 이미 들어갔다 — <see cref="CombatProjectileFx.PlayTravel"/> 주석).
        ///
        /// 쓰는 곳이 둘이고 <b>뜻이 다르다</b>: 구속탄은 <b>날아가는 탄환</b>,
        /// 이끌리는 혈취는 <b>돌진 잔상</b>. 그림만 다르고 「A 에서 B 로 흘러간다」는 같아서
        /// 한 함수로 둔다.
        /// </summary>
        void PlayProjectile(int slot, BossSkillSO skill, Vector3 from, Vector3 to, float sizeTiles)
        {
            if (_animator == null) _animator = GetComponent<CharacterAnimator>();
            CharacterSkinSO skin = _animator != null ? _animator.Skin : null;
            if (skin == null) return;

            Sprite[] frames = skin.SkillProjectile(slot);
            if (frames == null || frames.Length == 0) frames = skin.projectileFrames;
            if (frames == null || frames.Length == 0) return;

            CombatProjectileFx.PlayTravel(frames, from, to,
                                          skill.CastSecondsOr(castSeconds) * 0.5f, _self, sizeTiles);
        }

        // ------------------------------------------------------------------
        // 카시노마 「이끌리는 혈취」 — <b>시전자가 움직인다</b> (2026-08-18)
        // ------------------------------------------------------------------

        /// <summary>
        /// 지름 <c>value_01</c> 타일 안에서 가장 가까운 적에게 <b>직접 이동</b>해 붙은 뒤
        /// 한 번 때린다.
        ///
        /// ⚠ <b>벽을 뚫지 않는다.</b> 착지 지점이 막혀 있으면 대상 쪽으로 한 타일씩
        /// 당겨가며 설 수 있는 가장 가까운 자리를 고른다 — 넉백(<see cref="Knockback"/>)이
        /// 쓰는 규칙과 같다. 못 찾으면 <b>제자리에서 때린다</b>(시전은 성립시킨다 —
        /// 여기까지 왔는데 취소하면 쿨타임이 헛돌아 스킬이 영영 안 나간다).
        /// </summary>
        bool TryCastLureBlood(int slot, BossSkillSO skill)
        {
            DamageableUnit aim = NearestWithin(skill.DashSeekRadiusTiles);
            if (aim == null) return requireTarget ? false : true;

            Vector3 from = transform.position;
            DashTo(aim);

            // ★ 돌진 <b>잔상</b>을 출발점 → 도착점으로 흘려보낸다(스킨의 skill1Projectile).
            //   이동 자체는 한 프레임에 끝나므로(DashTo), 그 사이를 가려주는 것이 이 연출이다.
            PlayProjectile(slot, skill, from, transform.position, 2f);

            // 연출은 <b>출발점에서 도착점까지</b> 보여준다. 상자는 도착 자리에서 한 칸.
            PlayFx(slot, skill, transform.position, AimDirection(aim.transform.position - from),
                   1f, 1f, aim);

            _scratch.Clear();
            _scratch.Add(aim);                       // 정의문: "적 1명에게" — 광역이 아니다
            return ApplyDamage(slot, skill, 1f, 1f, "돌진");
        }

        /// <summary>대상 바로 앞의 설 수 있는 칸으로 옮긴다. 못 찾으면 제자리에 둔다.</summary>
        void DashTo(DamageableUnit target)
        {
            Vector3 to = target.transform.position;
            Vector2 delta = (Vector2)(to - transform.position);
            float dist = delta.magnitude;
            if (dist < 0.01f) return;

            Vector2 dir = delta / dist;
            if (_map == null) _map = FindAnyObjectByType<Map.MapGenerator>();

            // 대상에 겹치지 않게 한 칸 앞에서 멈춘다. 거기가 막혀 있으면 뒤로 물러나며 찾는다.
            for (float d = Mathf.Max(0f, dist - 1f); d >= 0f; d -= 1f)
            {
                Vector3 spot = transform.position + (Vector3)(dir * d);
                if (_map != null && !_map.IsCellPlaceable(_map.WorldToCell(spot))) continue;
                transform.position = spot;
                return;
            }
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// 이 유닛이 적을 찾는 거리(타일). 표에 사거리 칸이 없는 스킬이 쓴다 —
        /// 인식 범위가 있으면 그것, 없으면 12 타일(보스 표의 detect_range 기본값).
        /// </summary>
        float SeekRangeTiles()
        {
            if (_combat == null) _combat = GetComponent<UnitCombat>();
            return _combat != null ? _combat.EffectiveDetectRange : 12f;
        }

        UnitCombat _combat;

        /// <summary>반경 안에서 가장 가까운 적. 없으면 null.</summary>
        DamageableUnit NearestWithin(float radiusTiles)
        {
            UnitRegistry.CollectEnemiesInRadius(transform.position, radiusTiles, _self.Faction, _scratch);
            return NearestOf(_scratch);
        }

        /// <summary>
        /// <see cref="_scratch"/> 에 모인 대상 전부에 피해·침식을 넣고 로그를 남긴다.
        /// 두 범위 모양(원형·직사각형)이 <b>같은 규칙</b>을 쓰도록 한 곳에 모아둔 것이다.
        /// 항상 true 를 돌려준다 — 여기까지 왔다는 것은 이미 시전이 성립했다는 뜻이다.
        /// </summary>
        bool ApplyDamage(int slot, BossSkillSO skill, float length, float width, string shapeLabel)
        {
            // 공격한 쪽도 전투 상태로 기록해야 재생 대기 시간이 갱신된다(UnitCombat.TryAttack 과 같다).
            _self.MarkCombatAction();

            int hits = 0;
            for (int i = 0; i < _scratch.Count; i++)
            {
                DamageableUnit u = _scratch[i];
                if (u == null || !u.IsAlive) continue;
                u.TakeDamageFrom(_self, skill.DamagePercent);
                hits++;

                // 침식은 이 스킬 데이터의 값이다(BossSkillSO.value04, 유저 지시 2026-08-13) —
                // GameSystems 의 전역 규칙이 아니라 "이 스킬에 맞으면 얼마나 오르는지"를
                // 표에서 그대로 읽어 여기서 직접 적용한다. 캐릭터만 침식이 있다(구조물 제외).
                if (u.IsAlive && u is CharacterUnit character && skill.ErosionValue > 0f)
                    CharacterErosion.EnsureOn(character)?.AddErosion(skill.ErosionValue);

                if (u.IsAlive) ApplySideEffects(skill, u);
            }

            // 로그 형식은 캐릭터 스킬(CharacterPassives)과 <b>같은 규칙</b>이다 —
            // "누가 · 무슨 스킬" (유저 지시 2026-08-13: "로그에 스킬 쓰면 스킬 이름이랑
            // 같이 나오게 해줘 누가 썼는지랑"). 형식 문자열은 UI.HudLog.SkillLine 한 곳에 있다.
            string line = UI.HudLog.SkillLine(_self.DisplayName, skill.DisplayName,
                                              hits > 0 ? $"{hits}명 피격" : null);
            UI.HudLog.Add(line, UI.HudLogKind.Danger);

            if (logCasts)
                Debug.Log($"[보스스킬] {line} · {shapeLabel} 범위 {length:0.##}x{width:0.##}타일 · " +
                          $"공격력 {skill.DamagePercent}%", this);
            return true;
        }

        // ------------------------------------------------------------------
        // 피해 말고 <b>따라붙는 효과</b> (2026-08-15 — 카르시노스)
        //
        // 단탈리온의 두 스킬은 붙는 효과가 침식 하나뿐이라 위 루프에 한 줄로 들어 있었다.
        // 카르시노스는 스킬마다 다른 효과가 붙어서 한 곳에 모았다 —
        // <b>스킬이 늘 때 고칠 자리를 하나로</b> 두려는 것이다.
        // ------------------------------------------------------------------

        /// <summary>이 스킬이 대상에게 얹는 부가 효과. 해당 없는 스킬은 조용히 지나간다.</summary>
        void ApplySideEffects(BossSkillSO skill, DamageableUnit target)
        {
            // ── 방어력 감소 (할퀴기) ────────────────────────────────────
            // 표의 값은 <b>%</b> 다("방어력이 {value_04}% 만큼 감소"). 실제 장부는
            // 피올로의 「부식」이 쓰는 것과 <b>같은 것</b>을 쓴다 — 만료·중첩 규칙이
            // 이미 거기 있고, 방어력 보정 장부가 두 벌이 되면 하나가 새면 영구히 깎인다.
            if (skill.DefenseDownPercent > 0f && skill.DefenseDownSeconds > 0f)
            {
                int amount = Mathf.RoundToInt(target.DefenseStat * skill.DefenseDownPercent / 100f);
                if (amount > 0)
                    PassiveSkillService.ApplyCorrosion(target, amount, skill.DefenseDownSeconds);
            }

            // ── 넉백 (죽음의 포효 · 담뱃대 강타) ────────────────────────
            //    ⚠ 두 스킬이 <b>방향 기준이 다르다</b> — Knockback 안에서 가른다.
            if (skill.KnockbackTiles > 0f) Knockback(skill, target, skill.KnockbackTiles);

            // ── 중독 (담배 연기) ────────────────────────────────────────
            //    ★ 시전 순간과 «연기가 남아 있는 동안»(LingerSmoke) <b>둘 다</b> 이걸 부른다 —
            //      거는 규칙이 한 곳에만 있어야 두 경로가 어긋나지 않는다.
            ApplyPoison(skill, target);

            // ── 허약 → 구속 (구속탄) ────────────────────────────────────
            // 정의문: "…'허약' 상태로 만든다. … 허약 상태의 적이 <b>해당 공격에 다시 피격</b> 시
            //          즉시 허약 상태를 해제하고 {value_06}초 만큼 이동과 공격이 불가능한
            //          '구속' 상태로 만든다."
            // ⚠ <b>순서가 중요하다</b> — 먼저 "지금 허약인가"를 보고, 그 다음에 새로 건다.
            //   반대로 하면 첫 발에서 바로 구속에 걸린다.
            if (skill.WeakenSeconds > 0f)
            {
                // ⚠ 예전에는 여기서 `if (combat == null) return;` 로 <b>메서드를 빠져나갔다</b>.
                //   그때는 이게 마지막 블록이라 결과가 같았지만, 아래에 효과가 하나 붙은
                //   지금은 그 return 이 <b>그 효과를 삼킨다</b>. 조건 안으로 접었다.
                var combat = target.GetComponent<UnitCombat>();
                if (combat != null)
                {
                    if (combat.IsWeakened && skill.BindSeconds > 0f)
                    {
                        combat.ClearWeaken();
                        Bind(combat, target, skill.BindSeconds, skill.StatusName);
                    }
                    else
                    {
                        combat.ApplyWeaken(skill.WeakenAttackSpeedPercent, skill.WeakenSeconds);
                    }
                }
            }

            // ── 즉시 구속 (아니사킬 「거대한 위협 포효」) ────────────────
            // 정의문: "피격 당한 적은 '구속'상태에 걸리며, {value_03}초 만큼 이동과 공격이
            //          불가능해진다(기절상태)."
            //
            // ★ <b>구속탄과 같은 상태를 쓴다</b>(UnitCombat.ApplyBind) — 다른 것은 <b>거는
            //   조건</b>뿐이다. 구속탄은 「허약 중에 또 맞으면」이고 이쪽은 <b>맞으면 바로</b>다.
            //   상태를 새로 만들면 해제 규칙(정신 안정)이 두 벌이 되어 한쪽이 새게 된다.
            if (skill.Type == BossSkillType.HugeThreat && skill.BindSeconds > 0f)
            {
                var combat = target.GetComponent<UnitCombat>();
                if (combat != null) Bind(combat, target, skill.BindSeconds, skill.StatusName);
            }

            // ── 최대 체력 비례 추가 피해 (라린길 「타오르는 숨결」) ──────
            // 정의문: "…추가로 <b>적 전체 체력</b>의 {value_04}%의 데미지를 준다."
            //
            // ★ <b>방어력을 거치지 않는다</b> — 근거가 공격력이 아니라 <b>맞는 쪽의 체력</b>이라
            //   `BalanceConfigSO.Damage(공격력, 방어력)` 에 넣을 자리가 없다. 그래서 계산이
            //   끝난 값을 넣는 `ApplyDamage(int)` 를 직접 부른다.
            //
            // ⚠ <b>최대 체력</b>이지 남은 체력이 아니다. 남은 체력의 %로 하면 절대 안 죽는다.
            // ⚠ 반올림이 아니라 <b>올림</b>이다 — 최대 체력이 작은 유닛에서 0 이 되면
            //   "체력 비례"라는 말 자체가 무너진다(1은 반드시 들어간다).
            if (skill.MaxHpPercentDamage > 0f)
            {
                int extra = Mathf.CeilToInt(target.MaxHp * skill.MaxHpPercentDamage / 100f);
                if (extra > 0) target.ApplyDamage(extra);
            }
        }

        /// <summary>
        /// 구속을 걸고 로그를 남긴다 — 거는 자리가 둘이라 문구를 한 곳에 모았다.
        ///
        /// <paramref name="label"/> 은 이 스킬이 부르는 이름(<see cref="BossSkillSO.StatusName"/>,
        /// 2026-08-19) — 비어 있으면 <see cref="UnitCombat.ApplyBind"/> 안에서 "구속"으로
        /// 떨어진다. 로그도 상세 카드와 같은 이름을 쓰도록 여기서 한 번만 정한다.
        /// </summary>
        static void Bind(UnitCombat combat, DamageableUnit target, float seconds, string label)
        {
            combat.ApplyBind(seconds, label);
            string shown = string.IsNullOrEmpty(label) ? "구속" : label;
            UI.HudLog.Add($"{target.DisplayName} {shown}!", UI.HudLogKind.Danger);
        }

        /// <summary>
        /// 「중독」을 건다 (베일 「담배 연기」). 해당 없는 스킬은 조용히 지나간다.
        ///
        /// 실제 상태는 <see cref="UnitCombat.ApplyPoison"/> 가 들고 있다 — 「허약」·「구속」과
        /// <b>같은 자리</b>다. 상태를 여기(스킬 쪽)에 두면 <b>중독된 유닛을 매 프레임 훑는
        /// 장부</b>가 하나 더 생기고, 보스가 죽었을 때 그 장부를 정리하는 코드가 또 필요해진다.
        ///
        /// <paramref name="skill"/> 의 <c>status_name</c> 이 비어 있으면
        /// <see cref="UnitCombat.ApplyPoison"/> 안에서 <b>"중독"</b> 으로 떨어진다
        /// (「구속」의 <see cref="Bind"/> 와 같은 규칙).
        /// </summary>
        void ApplyPoison(BossSkillSO skill, DamageableUnit target)
        {
            if (skill.PoisonSeconds <= 0f || skill.PoisonMaxHpPercentPerSecond <= 0f) return;

            var combat = target.GetComponent<UnitCombat>();
            if (combat == null) return;

            bool wasClean = !combat.IsPoisoned;
            combat.ApplyPoison(skill.PoisonMaxHpPercentPerSecond, skill.PoisonSeconds,
                               skill.StatusName);

            // 로그는 <b>새로 걸릴 때만</b> 남긴다 — 연기 안에 서 있으면 매 프레임 다시
            // 거므로(LingerSmoke) 안 그러면 로그창이 한 종류로 가득 찬다.
            if (wasClean && combat.IsPoisoned)
                UI.HudLog.Add($"{target.DisplayName} {combat.PoisonLabel}!", UI.HudLogKind.Danger);
        }

        /// <summary>
        /// 대상을 밀어낸다. <b>방향 기준이 스킬마다 다르다</b>:
        /// <code>
        ///   죽음의 포효 (2002)   <b>시전자 반대쪽</b>  — "…뒤로 {value_02} 타일 만큼 밀려나며"
        ///   담뱃대 강타 (130009) <b>대상이 보는 반대쪽</b> — "캐릭터는 자신이 바라보는
        ///                        반대 방향으로 밀려납니다"
        /// </code>
        /// ★ <b>두 기준은 대개 같은 결과를 낸다</b>(교전 중이면 보스를 마주 보고 있다).
        ///   갈리는 것은 <b>등을 보이고 있을 때</b>다 — 도망치던 캐릭터는 강타에 맞으면
        ///   <b>보스 쪽으로 끌려온다</b>. 표가 그렇게 적혀 있고, 「담뱃대로 후려친다」는
        ///   그림과도 맞는다.
        ///
        /// ⚠ 「보는 방향」은 <see cref="CharacterAnimator.FacingRight"/> 다 —
        ///   <b>좌우뿐</b>이라 위아래 성분이 없다. 애니메이터가 없는 대상(구조물·포탑)은
        ///   방향이라는 개념이 없으므로 <b>시전자 반대쪽</b>으로 떨어뜨린다.
        ///
        /// ⚠ <b>벽을 뚫지 않는다.</b> 목표 지점이 막혀 있으면 한 타일씩 줄여가며 설 수 있는
        /// 가장 먼 자리를 찾는다 — 통째로 순간이동시키면 벽 안에 박혀 빠져나오지 못한다
        /// (13절이 이동 충돌을 타일 기준으로 판정하는 것과 같은 이유다).
        ///
        /// ⚠ <b>이동 목표(귀환 지점·집결지)는 건드리지 않는다.</b> 밀려난 뒤 스스로 걸어
        /// 돌아오는 것이 맞고, 목표까지 옮기면 "왜 자리를 이탈했지"가 된다.
        /// </summary>
        void Knockback(BossSkillSO skill, DamageableUnit target, float tiles)
        {
            Vector2 away;

            var anim = skill.Type == BossSkillType.PipeStrike
                ? target.GetComponent<CharacterAnimator>()
                : null;

            if (anim != null)
            {
                // 「바라보는 반대 방향」 — 오른쪽을 보고 있으면 왼쪽으로 밀린다.
                away = anim.FacingRight ? Vector2.left : Vector2.right;
            }
            else
            {
                away = (Vector2)(target.transform.position - transform.position);
                if (away.sqrMagnitude < 0.0001f) away = Vector2.right;   // 정확히 겹쳤으면 아무 쪽으로
                away.Normalize();
            }

            Vector3 from = target.transform.position;
            if (_map == null) _map = FindAnyObjectByType<Map.MapGenerator>();

            // 한 타일씩 줄여가며 실제로 설 수 있는 가장 먼 자리를 고른다.
            for (float d = tiles; d >= 1f; d -= 1f)
            {
                Vector3 to = from + (Vector3)(away * d);
                if (_map != null && !_map.IsCellPlaceable(_map.WorldToCell(to))) continue;
                target.transform.position = to;
                return;
            }
        }

        /// <summary>넉백이 벽을 뚫지 않게 하려고 본다. 없으면 그냥 밀어낸다(맵 없는 테스트 씬 대비).</summary>
        Map.MapGenerator _map;

        /// <summary>목록에서 <b>가장 가까운</b> 대상. 원형 범위에서 시전 모션이 볼 쪽을 정하는 데 쓴다.</summary>
        DamageableUnit NearestOf(List<DamageableUnit> units)
        {
            DamageableUnit best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < units.Count; i++)
            {
                DamageableUnit u = units[i];
                if (u == null || !u.IsAlive) continue;

                float sqr = ((Vector2)(u.transform.position - transform.position)).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                best = u;
                bestSqr = sqr;
            }
            return best;
        }

        /// <summary>
        /// 조준 대상. 타락한 무덤은 <b>가장 가까운</b> 적, 공허의 광선은 <b>가장 먼</b> 적이다
        /// (스트링 테이블의 설명 그대로).
        ///
        /// 후보는 <b>스킬이 닿는 거리 안</b>에서만 고른다 — 그래야 "가장 먼 적"이 15타일
        /// 밖에 있어서 빔이 아무에게도 안 닿는 헛시전이 안 생긴다.
        ///
        /// ⚠ 후보 수집을 <b>원형</b>으로 한다(예전에는 정사각 상자였다). 상자로 모으면 대각선
        /// 방향이 √2 배 더 멀리까지 후보로 잡혀, 조준은 됐는데 실제 범위 밖이라 헛시전이 되는
        /// 경우가 생긴다. 어차피 아래에서 거리로 다시 자르던 것을 수집 단계로 옮긴 것이다.
        ///
        /// 안개는 보지 않는다: 몬스터는 원래 지도 전체가 밝혀진 것처럼 싸운다(13절).
        /// </summary>
        DamageableUnit PickAim(BossSkillType type, float reachTiles)
        {
            UnitRegistry.CollectEnemiesInRadius(transform.position, reachTiles, _self.Faction, _scratch);

            // <b>가장 먼</b> 적을 노리는 스킬 — 뒤에서 안전하게 쏘던 원거리·치유를 잡는 기술이다.
            // 단탈리온 「공허의 광선」과 말파스 「저주광선」이 같은 규칙이다(정의문 그대로).
            bool wantFarthest = type == BossSkillType.VoidLaser || type == BossSkillType.CurseBeam;

            DamageableUnit best = null;
            float bestSqr = wantFarthest ? -1f : float.MaxValue;

            for (int i = 0; i < _scratch.Count; i++)
            {
                DamageableUnit u = _scratch[i];
                if (u == null || !u.IsAlive) continue;

                float sqr = ((Vector2)(u.transform.position - transform.position)).sqrMagnitude;

                bool better = wantFarthest ? sqr > bestSqr : sqr < bestSqr;
                if (!better) continue;

                best = u;
                bestSqr = sqr;
            }
            return best;
        }

        /// <summary>
        /// 조준 단위벡터 — <b>자르지 않은 실제 방향</b>이다(360도).
        /// 예전의 <c>AxisDirection</c>(4방향 스냅)을 대체한다: 상자를 통째로 돌릴 수 있게 되면서
        /// 방향을 축에 맞출 이유가 없어졌다. 0 벡터면 오른쪽으로 본다.
        /// </summary>
        static Vector2 AimDirection(Vector2 delta) =>
            delta.sqrMagnitude > 0.000001f ? delta.normalized : Vector2.right;

        /// <summary>시전 모션 + 지면 범위 연출. 둘 다 없으면 조용히 넘어간다(피해는 그대로 들어간다).</summary>
        /// <summary>
        /// <b>앞으로 뿜는 연기 브레스</b> (2026-08-20 · 베일 「담배 연기」).
        ///
        /// <see cref="PlayFx"/> 와 다른 점은 <b>어느 원화를 쓰는지</b> 하나다:
        /// 그쪽은 스킬 칸(<c>skill2Fx</c> = 바닥에 깔던 반원형 범위 표시)을 쓰고,
        /// 이쪽은 <b>연기 구체</b>(<c>skill2Projectile</c> → 없으면 평타 <c>projectileFrames</c>)를
        /// 쓴다. 시전 모션은 똑같이 재생한다 — 그건 «누가 뭘 한다» 를 보여주는 부분이다.
        ///
        /// ⚠ 원화가 <b>하나도 없으면</b> 시전 모션만 나가고 조용히 지나간다 — 연출이 없다고
        ///   피해까지 막으면 «스킬이 안 나간다» 가 된다(<see cref="PlayFx"/> 와 같은 규칙).
        /// </summary>
        void PlayBreath(int slot, BossSkillSO skill, Vector3 center, Vector2 dir,
                        float length, float width, DamageableUnit aim)
        {
            if (_animator == null) _animator = GetComponent<CharacterAnimator>();

            float seconds = skill.CastSecondsOr(castSeconds);
            Vector3 lookAt = aim != null ? aim.transform.position
                                         : transform.position + (Vector3)dir;
            _animator?.PlaySkillMotion(slot, seconds, lookAt);

            CharacterSkinSO skin = _animator != null ? _animator.Skin : null;
            if (skin == null) return;

            Sprite[] fx = skin.SkillProjectile(slot);
            if (fx == null || fx.Length == 0) fx = skin.projectileFrames;
            if (fx == null || fx.Length == 0) return;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            CombatProjectileFx.PlayArea(fx, center, new Vector2(length, width), angle,
                                        _self, seconds);
        }

        void PlayFx(int slot, BossSkillSO skill, Vector3 center, Vector2 dir,
                    float length, float width, DamageableUnit aim)
        {
            if (_animator == null) _animator = GetComponent<CharacterAnimator>();

            // 연출 길이는 <b>이 스킬</b>의 값이 먼저다 — 표(Skill 시트)의 cast_time 칸.
            // 비어 있으면 이 컴포넌트의 castSeconds(전역 기본값)로 떨어진다.
            float seconds = skill.CastSecondsOr(castSeconds);

            Vector3 lookAt = aim != null ? aim.transform.position
                                         : transform.position + (Vector3)dir;
            _animator?.PlaySkillMotion(slot, seconds, lookAt);

            Sprite[] fx = _animator != null && _animator.Skin != null
                ? _animator.Skin.SkillFx(slot)
                : null;
            if (fx == null) return;

            // 그림은 +X 를 향해 그려져 있다 — <b>조준 각도 그대로</b> 돌린다(360도).
            // 예전에는 0/90/180/-90 네 값만 썼는데, 판정 상자가 자유각으로 돌아가게 되면서
            // 그림만 축에 맞추면 <b>연출과 피해 범위가 최대 45도까지 어긋난다</b>.
            // 원형(Circle)은 방향이 없으므로 0도로 그대로 깐다.
            float angle = skill.Shape == BossSkillShape.Circle
                ? 0f
                : Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // 크기는 <b>회전 전(그림 기준)</b>으로 넘긴다: x = 뻗는 길이, y = 두께.
            CombatProjectileFx.PlayArea(fx, center, new Vector2(length, width), angle, _self, seconds);
        }
    }
}
