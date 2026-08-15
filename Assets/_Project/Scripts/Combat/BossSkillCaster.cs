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
            var box = new Vector2(skill.LengthTiles, skill.WidthTiles);
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
            if (requireTarget && _scratch.Count == 0) return false;

            // 연출을 피해보다 먼저 띄운다 — 맞고 죽어 사라진 대상의 자리에도 범위가 보이게.
            PlayFx(slot, skill, center, dir, length, width, aim);
            return ApplyDamage(slot, skill, length, width, "직선");
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

            // ── 넉백 (죽음의 포효) ──────────────────────────────────────
            if (skill.KnockbackTiles > 0f) Knockback(target, skill.KnockbackTiles);
        }

        /// <summary>
        /// 대상을 <b>보스 반대 방향으로</b> 밀어낸다.
        ///
        /// ⚠ <b>벽을 뚫지 않는다.</b> 목표 지점이 막혀 있으면 한 타일씩 줄여가며 설 수 있는
        /// 가장 먼 자리를 찾는다 — 통째로 순간이동시키면 벽 안에 박혀 빠져나오지 못한다
        /// (13절이 이동 충돌을 타일 기준으로 판정하는 것과 같은 이유다).
        ///
        /// ⚠ <b>이동 목표(귀환 지점·집결지)는 건드리지 않는다.</b> 밀려난 뒤 스스로 걸어
        /// 돌아오는 것이 맞고, 목표까지 옮기면 "왜 자리를 이탈했지"가 된다.
        /// </summary>
        void Knockback(DamageableUnit target, float tiles)
        {
            Vector2 away = (Vector2)(target.transform.position - transform.position);
            if (away.sqrMagnitude < 0.0001f) away = Vector2.right;   // 정확히 겹쳤으면 아무 쪽으로
            away.Normalize();

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

            DamageableUnit best = null;
            float bestSqr = type == BossSkillType.VoidLaser ? -1f : float.MaxValue;

            for (int i = 0; i < _scratch.Count; i++)
            {
                DamageableUnit u = _scratch[i];
                if (u == null || !u.IsAlive) continue;

                float sqr = ((Vector2)(u.transform.position - transform.position)).sqrMagnitude;

                bool better = type == BossSkillType.VoidLaser ? sqr > bestSqr : sqr < bestSqr;
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
