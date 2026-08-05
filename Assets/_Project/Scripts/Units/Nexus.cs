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

        /// <summary>넥서스가 파괴되었을 때. 패배 처리가 여기에 붙는다.</summary>
        public event System.Action<Nexus> OnNexusDestroyed;

        MapGenerator _mapGenerator;
        Vector3Int[] _footprintCells;

        public NexusDefinitionSO Definition => definition;

        public override int MaxHp =>
            definition != null && Balance != null ? definition.MaxHp(Balance) : 0;

        public override int DefenseStat => definition != null ? definition.defenseStat : 0;

        /// <summary>넥서스는 공격하지 않는다.</summary>
        public override int AttackStat => 0;

        protected override int RegenStat => definition != null ? definition.regenStat : 0;

        public override Faction Faction => Faction.Angel;
        public override UnitKind Kind => UnitKind.Nexus;

        /// <summary>스포너가 복제 직후 호출한다.</summary>
        public void Initialize(NexusDefinitionSO def, BalanceConfigSO balance)
        {
            definition = def;
            SetupHealth(balance);
        }

        /// <summary>
        /// 넥서스가 차지한 칸을 벽과 동일하게 막는다. 유닛에는 Collider2D 가 없고
        /// 이동 충돌이 전부 타일 기준 판정(<see cref="MapGenerator.IsCellBlocked"/>)이므로,
        /// 넥서스도 그 판정에 자기 칸을 등록해야 캐릭터·몬스터가 뚫고 지나가지 않는다.
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
