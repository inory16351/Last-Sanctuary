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
    /// 자기 칸에서 <b>조준 방향으로</b> 가로 길이만큼 뻗고, 두께는 세로다. 조준은
    /// 4방향으로 자른다 — 맵이 타일 격자이고 표의 값도 칸 수라, 비스듬한 상자를 만들면
    /// 표의 "5 x 3" 이 화면에서 몇 칸인지 셀 수 없어진다.
    /// </summary>
    [RequireComponent(typeof(MonsterUnit))]
    [DisallowMultipleComponent]
    public class BossSkillCaster : MonoBehaviour
    {
        [Header("스킬 목록")]
        [Tooltip("보스 스킬 정의 에셋이 든 Resources 폴더 이름. 이 안에서 " +
                 "MonsterDefinitionSO.bossSkillIds 의 번호를 찾는다")]
        [SerializeField] string skillResourceFolder = "BossSkills";

        [Header("시전 규칙")]
        [Tooltip("소환된 뒤 첫 스킬을 쓰기까지의 대기(초). 0 이면 나오자마자 광역기가 나간다")]
        [Min(0f)] [SerializeField] float initialDelaySeconds = 5f;

        [Tooltip("스킬끼리 두는 최소 간격(초). 쿨타임이 동시에 차도 두 개가 같은 프레임에 " +
                 "나가지 않게 한다 — 겹쳐 맞으면 전열이 통째로 증발한다")]
        [Min(0f)] [SerializeField] float globalCooldownSeconds = 2f;

        [Tooltip("시전 모션·범위 연출이 화면에 머무는 시간(초). 피해는 시전과 동시에 들어간다")]
        [Min(0.05f)] [SerializeField] float castSeconds = 0.55f;

        [Tooltip("이 안에 맞을 적이 하나도 없으면 시전하지 않는다(쿨타임도 안 돈다). " +
                 "끄면 허공에도 쏜다 — 연출 확인용")]
        [SerializeField] bool requireTarget = true;

        [Header("디버그")]
        [Tooltip("시전할 때마다 콘솔에 남긴다 (HUD 로그는 이 값과 무관하게 항상 남는다)")]
        [SerializeField] bool logCasts = true;

        [Tooltip("끄면 스킬을 전혀 쓰지 않는다 — 밸런스 테스트용 스위치 " +
                 "(ErosionService.enableErosion 과 같은 결)")]
        [SerializeField] bool enableSkills = true;

        MonsterUnit _self;
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

        void Awake() => _self = GetComponent<MonsterUnit>();

        void OnEnable()
        {
            // 스포너가 템플릿을 복제해 활성화하는 순간이 곧 "소환"이다. 다만 이 시점에는
            // MonsterSpawner.Initialize 가 아직 안 돌아 Definition 이 비어 있을 수 있어서,
            // 실제 해석은 첫 Update 로 미룬다(아래 EnsureResolved).
            _resolved = false;
            _skills.Clear();
            _nextUseTime.Clear();
            _priorityOrder.Clear();
            _nextAnyUseTime = Time.time + initialDelaySeconds;
        }

        /// <summary>
        /// 정의에서 스킬 목록을 푼다. <b>Update 에서 늦게 하는 이유</b> — 몬스터는 프레임
        /// 중간에 <c>Instantiate</c> 된 뒤 <c>Initialize</c>/<c>Configure</c> 로 정의를 받는다.
        /// <c>Awake</c>/<c>OnEnable</c> 에서 읽으면 아직 <c>definition</c> 이 null 이다 —
        /// 이 프로젝트에서 같은 순서 함정을 세 번 겪었다(24-6·27-9·29-2절).
        /// </summary>
        void EnsureResolved()
        {
            if (_resolved) return;
            if (_self == null) _self = GetComponent<MonsterUnit>();

            MonsterDefinitionSO def = _self != null ? _self.Definition : null;
            if (def == null) return;                 // 아직 초기화 전 — 다음 프레임에 다시 본다

            _resolved = true;
            _animator = GetComponent<CharacterAnimator>();

            int[] ids = def.bossSkillIds;
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
                    Debug.LogWarning($"[보스스킬] {def.DisplayName} 의 스킬 id {ids[i]} 에 해당하는 " +
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

            // 시도 순서 = 쿨타임 내림차순. List.Sort 는 불안정 정렬이지만 슬롯 수가 2~3개뿐이라
            // 동률(같은 쿨타임)이 실제로 문제될 일이 없다.
            for (int i = 0; i < _skills.Count; i++) _priorityOrder.Add(i);
            _priorityOrder.Sort((a, b) => _skills[b].coolTime.CompareTo(_skills[a].coolTime));

            if (logCasts && _skills.Count > 0)
                Debug.Log($"[보스스킬] {def.DisplayName} 스킬 {_skills.Count}종 준비 " +
                          $"(쿨타임 긴 순 {string.Join(" → ", _priorityOrder.ConvertAll(i => _skills[i].DisplayName))})",
                          this);
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
            float length = skill.LengthTiles;
            float width = skill.WidthTiles;

            DamageableUnit aim = PickAim(skill.Type, length);
            if (aim == null && requireTarget) return false;

            // 조준 방향을 4방향으로 자른다 (위 클래스 주석 참조).
            Vector2 dir = aim != null
                ? AxisDirection(aim.transform.position - transform.position)
                : Vector2.right;

            // 자기 칸을 <b>포함해서</b> 앞으로 뻗는다:
            //   내 칸 중심에서 앞으로 (길이-1)/2 만큼 상자 중심을 밀고, 반길이는 길이/2.
            // 그래서 상자 뒤쪽 경계가 내 칸의 뒤쪽 경계와 정확히 맞는다.
            Vector3 center = transform.position + (Vector3)(dir * ((length - 1f) * 0.5f));

            bool horizontal = Mathf.Abs(dir.x) > Mathf.Abs(dir.y);
            var half = horizontal
                ? new Vector2(length * 0.5f, width * 0.5f)
                : new Vector2(width * 0.5f, length * 0.5f);

            UnitRegistry.CollectEnemiesInRect(center, half, _self.Faction, _scratch);
            if (requireTarget && _scratch.Count == 0) return false;

            // 연출을 피해보다 먼저 띄운다 — 맞고 죽어 사라진 대상의 자리에도 범위가 보이게.
            PlayFx(slot, skill, center, dir, length, width, aim);

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
            }

            string bossName = _self.Definition != null ? _self.Definition.DisplayName : _self.name;
            string line = $"{bossName} — {skill.DisplayName}!";
            UI.HudLog.Add(line, UI.HudLogKind.Danger);
            if (logCasts)
                Debug.Log($"[보스스킬] {line} 범위 {length:0.#}x{width:0.#}타일 · " +
                          $"공격력 {skill.DamagePercent}% · {hits}명 피격", this);
            return true;
        }

        /// <summary>
        /// 조준 대상. 타락한 무덤은 <b>가장 가까운</b> 적, 공허의 광선은 <b>가장 먼</b> 적이다
        /// (스트링 테이블의 설명 그대로).
        ///
        /// 후보는 <b>스킬이 닿는 거리 안</b>에서만 고른다 — 그래야 "가장 먼 적"이 15타일
        /// 밖에 있어서 빔이 아무에게도 안 닿는 헛시전이 안 생긴다.
        /// 안개는 보지 않는다: 몬스터는 원래 지도 전체가 밝혀진 것처럼 싸운다(13절).
        /// </summary>
        DamageableUnit PickAim(BossSkillType type, float reachTiles)
        {
            UnitRegistry.CollectEnemiesInRect(transform.position,
                                              new Vector2(reachTiles, reachTiles),
                                              _self.Faction, _scratch);

            DamageableUnit best = null;
            float bestSqr = type == BossSkillType.VoidLaser ? -1f : float.MaxValue;
            float reachSqr = reachTiles * reachTiles;

            for (int i = 0; i < _scratch.Count; i++)
            {
                DamageableUnit u = _scratch[i];
                if (u == null || !u.IsAlive) continue;

                float sqr = ((Vector2)(u.transform.position - transform.position)).sqrMagnitude;
                if (sqr > reachSqr) continue;

                bool better = type == BossSkillType.VoidLaser ? sqr > bestSqr : sqr < bestSqr;
                if (!better) continue;

                best = u;
                bestSqr = sqr;
            }
            return best;
        }

        /// <summary>가장 큰 성분만 남긴 4방향 단위벡터. 0 벡터면 오른쪽으로 본다.</summary>
        static Vector2 AxisDirection(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) < 0.0001f && Mathf.Abs(delta.y) < 0.0001f) return Vector2.right;
            return Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new Vector2(Mathf.Sign(delta.x), 0f)
                : new Vector2(0f, Mathf.Sign(delta.y));
        }

        /// <summary>시전 모션 + 지면 범위 연출. 둘 다 없으면 조용히 넘어간다(피해는 그대로 들어간다).</summary>
        void PlayFx(int slot, BossSkillSO skill, Vector3 center, Vector2 dir,
                    float length, float width, DamageableUnit aim)
        {
            if (_animator == null) _animator = GetComponent<CharacterAnimator>();

            Vector3 lookAt = aim != null ? aim.transform.position
                                         : transform.position + (Vector3)dir;
            _animator?.PlaySkillMotion(slot, castSeconds, lookAt);

            Sprite[] fx = _animator != null && _animator.Skin != null
                ? _animator.Skin.SkillFx(slot)
                : null;
            if (fx == null) return;

            // 그림은 +X 를 향해 그려져 있다 — 세로로 쏠 때만 90도 돌린다.
            // 크기는 <b>회전 전(그림 기준)</b>으로 넘긴다: x = 뻗는 길이, y = 두께.
            float angle = Mathf.Abs(dir.x) > Mathf.Abs(dir.y)
                ? (dir.x >= 0f ? 0f : 180f)
                : (dir.y >= 0f ? 90f : -90f);

            CombatProjectileFx.PlayArea(fx, center, new Vector2(length, width), angle, _self, castSeconds);
        }
    }
}
