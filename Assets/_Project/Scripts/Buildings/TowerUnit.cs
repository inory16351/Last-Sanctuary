using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Combat;
using LastSanctuary.Fog;
using LastSanctuary.Map;

namespace LastSanctuary.Buildings
{
    /// <summary>
    /// 건설된 포탑 한 채. 성역·캐릭터·몬스터와 같은 <see cref="DamageableUnit"/> 을 상속해서
    /// 피해 계산이 한 공식으로 통일된다(진행상황 4절).
    ///
    /// <b>생성은 템플릿 복제</b>다(진행상황 5절 — 이 프로젝트의 모든 유닛이 쓰는 방식).
    /// 씬의 <c>Templates/Building_Templates/Tower_Template</c> 를 <see cref="BuildService"/> 가
    /// <c>Instantiate</c> 하고 <see cref="Initialize"/> 로 테이블 값을 넣는다. 그래서 템플릿에
    /// 컴포넌트나 자식(체력바 등)을 붙이면 지어지는 모든 포탑이 자동으로 물려받는다.
    ///
    /// <b>체력은 능력치가 아니라 절대값</b>이다 — 데이터 시트의 <c>Construction.HP</c> 가
    /// 그대로 최대 체력이다(포탑 100). 방어력·공격력만 1~100 능력치라
    /// <see cref="BalanceConfigSO"/> 로 치환된다.
    ///
    /// <b>발판(2x2)은 벽과 동일한 충돌 판정</b>을 받는다 — 성역이 쓰는
    /// <see cref="MapGenerator.RegisterStructureFootprint"/> 를 그대로 재사용한다(진행상황 14절).
    /// 유닛에는 Collider2D 가 없고 이동 충돌이 전부 타일 기준이라, 등록하지 않으면
    /// 캐릭터·몬스터가 포탑을 그냥 통과한다.
    /// </summary>
    public class TowerUnit : DamageableUnit
    {
        [Header("데이터")]
        [SerializeField] BuildingDefinitionSO definition;

        [Header("배치 (BuildService 가 채운다)")]
        [Tooltip("발판의 좌하단 칸. 짝수 발판(2x2)은 '중심 칸' 이 없어서 좌하단을 기준으로 잡는다")]
        [SerializeField] Vector3Int footprintMinCell;

        MapGenerator _map;
        Vector3Int[] _footprintCells;

        public BuildingDefinitionSO Definition => definition;

        /// <summary>발판의 좌하단 칸. 철거·중복 배치 판정에 쓴다.</summary>
        public Vector3Int FootprintMinCell => footprintMinCell;

        public override int MaxHp => definition != null ? definition.hp : 0;
        public override int DefenseStat => definition != null ? definition.defenseStat : 0;
        public override int AttackStat => definition != null ? definition.attackStat : 0;

        /// <summary>포탑은 스스로 회복하지 않는다(시트에 회복 항목이 없다).</summary>
        protected override int RegenStat => 0;

        public override Faction Faction => Faction.Angel;
        public override UnitKind Kind => UnitKind.Tower;

        /// <summary>포탑이 파괴되었을 때 — <see cref="BuildService"/> 가 건설 수를 되돌린다.</summary>
        public event System.Action<TowerUnit> OnDestroyed;

        /// <summary>
        /// <see cref="BuildService"/> 가 템플릿을 복제한 직후 호출한다.
        /// 위치는 호출 전에 이미 잡혀 있어야 한다(발판 중심).
        /// </summary>
        public void Initialize(BuildingDefinitionSO def, Vector3Int minCell, BalanceConfigSO balance)
        {
            definition = def;
            footprintMinCell = minCell;
            SetupHealth(balance);

            ApplySprite();
            ApplyRenderSize();
            ApplyCombat();
            ApplyVision();
        }

        /// <summary>
        /// 보이는 크기를 <b>타일</b>로 맞춘다(유저 확정 2026-08-13). 원화 픽셀·PPU 가 아니라
        /// 정의 테이블의 타일 값이 정본이라, 아트를 다시 임포트해도 게임 안 크기가 안 흔들린다.
        /// </summary>
        void ApplyRenderSize()
        {
            if (definition == null || definition.renderHeightTiles <= 0f) return;

            var anim = GetComponent<Combat.TowerAnimator>();
            if (anim != null) anim.SetRenderHeightTiles(definition.renderHeightTiles);
        }

        protected override void Start()
        {
            base.Start();
            RegisterFootprint();
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// 스프라이트는 <c>Resources</c> 에서 경로로 읽는다 — 스프라이트 참조는 오브젝트
        /// 참조라 MCP 로 템플릿에 넣을 수 없기 때문이다(진행상황 8절 1번·4번).
        /// 템플릿의 <see cref="SpriteRenderer"/> 에 이미 그림이 꽂혀 있으면 그쪽을 존중한다 —
        /// 유저가 에디터에서 직접 원하는 아트를 끌어다 놓을 수 있게 하려는 것.
        /// </summary>
        void ApplySprite()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null || definition == null) return;
            if (sr.sprite != null) return;
            if (string.IsNullOrEmpty(definition.spriteResourcePath)) return;

            Sprite sprite = Resources.Load<Sprite>(definition.spriteResourcePath);
            if (sprite == null)
            {
                Debug.LogWarning($"[Tower] Resources/{definition.spriteResourcePath} 를 찾지 못했습니다. " +
                                 "포탑이 그림 없이 보입니다.", this);
                return;
            }
            sr.sprite = sprite;
        }

        /// <summary>
        /// 전투 파라미터를 테이블에서 주입한다. 포탑은 <b>움직이지 않는다</b> —
        /// <see cref="UnitCombat.SetImmobile"/> 를 켜지 않으면 사거리 밖의 적을 쫓아
        /// 포탑이 걸어다닌다(이동속도 0 은 밸런스 기본값으로 폴백되기 때문).
        /// </summary>
        void ApplyCombat()
        {
            var ai = GetComponent<UnitCombat>();
            if (ai == null || definition == null) return;

            ai.SetImmobile(true);
            ai.Configure(definition.attackRange, definition.attackRange, speed: 0f,
                         aps: definition.attacksPerSecond, advance: false,
                         priority: new[] { UnitKind.Monster }, leash: 0f,
                         type: definition.AttackType);
            ai.ConfigureSplash(definition.splashAreaTiles);
            ai.SetHome(transform.position);
            ai.enabled = definition.attackStat > 0;   // 중앙건물처럼 공격 안 하는 건물은 아예 끈다
        }

        void ApplyVision()
        {
            var vision = GetComponent<VisionSource>();
            if (vision == null || definition == null) return;

            if (definition.visionTiles <= 0f) vision.enabled = false;
            else vision.SetVision(definition.visionTiles);
        }

        void RegisterFootprint()
        {
            if (definition == null) return;

            _map = FindAnyObjectByType<MapGenerator>();
            if (_map == null) return;

            _footprintCells = new List<Vector3Int>(
                MapGenerator.FootprintCellsFrom(footprintMinCell, definition.footprintTiles)).ToArray();
            _map.RegisterStructureFootprint(_footprintCells);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_map != null && _footprintCells != null)
                _map.UnregisterStructureFootprint(_footprintCells);
        }

        protected override void OnDeath()
        {
            Debug.Log($"[Tower] {(definition != null ? definition.DisplayName : name)} 파괴됨", this);
            OnDestroyed?.Invoke(this);

            // 파괴 연출이 있으면 그게 끝난 뒤에 지운다. 연출 도중에도 발판은 계속 막고 있는데,
            // 무너지는 잔해를 몬스터가 통과해 지나가면 어색하기 때문이다.
            // 발판 반납은 OnDisable 에서 일어난다 — Destroy 가 그걸 부른다.
            var anim = GetComponent<TowerAnimator>();
            float delay = anim != null ? anim.PlayDestroy() : 0f;
            if (delay <= 0f) { Destroy(gameObject); return; }

            // 시체가 계속 맞아 OnDeath 가 다시 불리거나, 죽은 포탑이 계속 쏘는 것을 막는다.
            var combat = GetComponent<UnitCombat>();
            if (combat != null) combat.enabled = false;
            Destroy(gameObject, delay);
        }
    }
}
