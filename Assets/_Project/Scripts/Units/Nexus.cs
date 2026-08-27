using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Map;

namespace LastSanctuary.Units
{
    /// <summary>
    /// 중앙 건물. 파괴되면 즉시 패배(핵심시스템 기획서 p9).
    /// 공격은 하지 않는다 — 공격하는 건물은 포탑이 따로 담당한다.
    /// </summary>
    public class Nexus : DamageableUnit
    {
        [Header("데이터")]
        [SerializeField] NexusDefinitionSO definition;

        /// <summary>성역이 파괴되었을 때. 패배 처리가 여기에 붙는다.</summary>
        public event System.Action<Nexus> OnNexusDestroyed;

        MapGenerator _mapGenerator;
        Vector3Int[] _footprintCells;

        public NexusDefinitionSO Definition => definition;

        public override int MaxHp =>
            definition != null && Balance != null ? definition.MaxHp(Balance) : 0;

        public override int DefenseStat => definition != null ? definition.defenseStat : 0;

        /// <summary>성역은 공격하지 않는다.</summary>
        public override int AttackStat => 0;

        protected override int RegenStat => definition != null ? definition.regenStat : 0;

        public override Faction Faction => Faction.Angel;
        public override UnitKind Kind => UnitKind.Nexus;

        // ------------------------------------------------------------------
        // 클릭 초상화 (2026-08-18, 유저 지시: "성역 클릭 가능하게 만들고 일러스트 넣어서
        // ILLUST UI 에 적용")
        //
        // ⚠ <b>클릭 자체는 원래 됐다.</b> UnitSelector.PickAt 은 DamageableUnit 을 전수
        //   검사하고 성역은 아군이라 안개 검사도 건너뛴다. 눌러도 아무 일이 없어 보였던
        //   이유는 <see cref="Portrait"/> 가 베이스의 null 이라 UnitPortraitPanel 이
        //   「일러스트 없음」만 띄웠기 때문이다 — <b>그림이 없었던 것이지 클릭이 안 된 게 아니다.</b>
        // ------------------------------------------------------------------

        /// <summary>초상화. 정의 에셋의 <c>illustName</c> 을 Resources/Illust 에서 읽는다.</summary>
        public override Sprite Portrait => definition != null ? definition.Illust : null;

        /// <summary>
        /// 화면에 뜨는 이름 — 정의 에셋이 정한다.
        /// ★ 2026-08-27(184절) — <b>스트링 표를 거친다</b>(<see cref="NexusDefinitionSO.DisplayName"/>).
        ///   예전에는 <c>definition.displayName</c> 리터럴을 그대로 써서 초상화의 이 한 줄만
        ///   <b>영어로 안 바뀌었다</b>(유저 리포트).
        /// </summary>
        public override string DisplayName =>
            definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.DisplayName
                : base.DisplayName;

        /// <summary>칭호 — 비어 있으면 초상화에 칭호 줄이 안 뜬다.</summary>
        public override string Title =>
            definition != null && definition.Title != null ? definition.Title : string.Empty;

        /// <summary>스포너가 복제 직후 호출한다.</summary>
        public void Initialize(NexusDefinitionSO def, BalanceConfigSO balance)
        {
            definition = def;
            SetupHealth(balance);
        }

        /// <summary>
        /// 성역이 차지한 칸을 벽과 동일하게 막는다. 유닛에는 Collider2D 가 없고
        /// 이동 충돌이 전부 타일 기준 판정(<see cref="MapGenerator.IsCellBlocked"/>)이므로,
        /// 성역도 그 판정에 자기 칸을 등록해야 캐릭터·몬스터가 뚫고 지나가지 않는다.
        /// </summary>
        protected override void Start()
        {
            base.Start();
            RegisterFootprint();
        }

        void RegisterFootprint()
        {
            if (definition == null) return;

            _mapGenerator = FindAnyObjectByType<MapGenerator>();
            if (_mapGenerator == null) return;

            Vector3Int center = _mapGenerator.WorldToCell(transform.position);
            _footprintCells = new List<Vector3Int>(
                MapGenerator.FootprintCells(center, definition.footprintTiles)).ToArray();
            _mapGenerator.RegisterStructureFootprint(_footprintCells);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_mapGenerator != null && _footprintCells != null)
                _mapGenerator.UnregisterStructureFootprint(_footprintCells);
        }

        protected override void OnDeath()
        {
            Debug.Log("[Nexus] 중앙 건물이 파괴되었습니다 → 패배", this);
            OnNexusDestroyed?.Invoke(this);
        }

        public string DebugSummary()
        {
            if (definition == null || Balance == null) return "(데이터 없음)";
            return $"{definition.footprintTiles}x{definition.footprintTiles}타일 · " +
                   $"체력 {MaxHp} (능력치 {definition.hpStat}) · " +
                   $"피해감소 {definition.DefenseReductionPercent(Balance)}% " +
                   $"(능력치 {definition.defenseStat}) · " +
                   $"재생 {definition.RegenPerTick(Balance)}/{Balance.regenTickSeconds:0.#}초 " +
                   $"(능력치 {definition.regenStat}, 전투 후 {Balance.outOfCombatRegenDelay:0.#}초 대기)";
        }
    }
}
