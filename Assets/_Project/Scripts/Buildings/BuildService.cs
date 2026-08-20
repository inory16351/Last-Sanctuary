using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using LastSanctuary.Combat;
using LastSanctuary.Map;
using LastSanctuary.Resource;
using LastSanctuary.UI;

namespace LastSanctuary.Buildings
{
    /// <summary>
    /// 아직 짓지 않은 건설 예정지 하나. 플레이어가 자리를 찍으면 생기고,
    /// 캐릭터가 와서 <see cref="progress"/> 를 채우면 실제 건물이 된다.
    /// </summary>
    public class BuildSite
    {
        /// <summary>발판의 좌하단 칸. 2x2 는 '중심 칸' 이 없으므로 좌하단을 기준으로 잡는다.</summary>
        public Vector3Int MinCell;

        /// <summary>발판의 월드 중심 — 캐릭터가 여기로 걸어온다.</summary>
        public Vector3 Center;

        public BuildingDefinitionSO Definition;

        /// <summary>지금까지 쌓인 건설 시간(초, 캐릭터-초 합계).</summary>
        public float Progress;

        /// <summary>
        /// 이 자리를 맡은 캐릭터. <b>한 자리에는 한 명만 붙는다</b>(유저 확정) —
        /// 배정은 <see cref="BuildService.AssignedSiteFor"/> 가 정한다.
        /// </summary>
        public Units.CharacterBehavior Builder;

        /// <summary>이 자리를 찍을 때 실제로 낸 비용. 취소 시 그대로 환불한다.</summary>
        public int PaidCost;

        public float Required => Definition != null ? Mathf.Max(0.01f, Definition.buildSeconds) : 1f;
        public float Ratio => Mathf.Clamp01(Progress / Required);
    }

    /// <summary>
    /// 건물 건설. "건물 건설" 버튼을 누르면 배치 모드로 들어가고, 맵을 클릭하면 그 자리에
    /// <b>2x2 건설 예정지</b>가 생긴다. 실제로 짓는 것은 캐릭터다 —
    /// <see cref="LastSanctuary.Units.CharacterBehavior"/> 가 <b>스스로 판단해</b> 현장으로
    /// 가서 시간을 채운다(유저 요청: "건설 타이밍은 캐릭터가 알아서 판단").
    ///
    /// <b>UI·상호작용은 <see cref="RallyPointService"/> 와 같은 구조</b>다 — 클릭 판정,
    /// 미리보기 오버레이, 카메라 드래그 무시 규칙까지 같은 규칙을 쓴다. 두 기능이 서로 다른
    /// 조작감을 갖지 않게 하려는 것.
    ///
    /// <b>비용은 자리를 찍는 순간</b> 낸다(취소하면 전액 환불). 완성 시점에 받으면 자원 없이
    /// 예정지만 잔뜩 찍어둘 수 있어서, 데이터 시트의 "회차마다 비용이 오른다" 규칙이
    /// 의미를 잃는다.
    ///
    /// <b>포탑 생성은 템플릿 복제</b>(진행상황 5절) — 씬의
    /// <c>Templates/Building_Templates/Tower_Template</c> 를 복제해 값을 넣는다.
    /// </summary>
    public class BuildService : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // ★★ 「타워·건설」 기능 스위치 (2026-08-20)
        //
        // 유저 지시: *"타워랑 건설 기능 삭제"*. 범위를 물었고 <b>「플레이에서만 제거」</b>로
        // 확정받았다(코드는 남긴다 — 되살릴 수 있게).
        //
        // <b>왜 스위치 하나인가</b> — 참조가 <b>9개 파일</b>에 걸쳐 있다(세이브·패배 조건·
        // 전술 지침·AI·HUD·맵·집결지). 그 아홉 군데를 파내면 ① 되살리기가 사실상 불가능해지고
        // ② 세이브 포맷이 바뀌어 <b>기존 세이브가 깨진다.</b>
        //
        // ★ 그런데 이 프로젝트는 <b>이미</b> 그 아홉 군데가 전부 «서비스가 없으면 아무것도
        //   안 한다» 로 쓰여 있다(실측: `BuildService.Instance` 를 쓰는 모든 곳이 null 검사를
        //   먼저 한다 — 세이브의 `CaptureTowers`/`RestoreTowers` 는 <c>return</c> 하고,
        //   `CharacterBehavior.TryBuild` 는 <c>false</c> 를 돌려주고, HUD 는 건너뛴다).
        //   그래서 <b>Instance 를 채우지 않는 것</b>만으로 기능이 통째로 사라진다.
        //   패배 조건도 저절로 맞는다 — <c>WaveManager.HasLivingTower()</c> 는 타워가
        //   하나도 안 생기므로 언제나 false 다(원래 «타워가 남아 있으면 안 진다» 였다).
        //
        // ⚠ <b>씬을 고치지 않는다.</b> 이 칸은 씬 YAML 에 <b>아직 없는</b> 새 필드이므로
        //   유니티가 C# 기본값(false)을 쓴다 — 씬 파일을 열지도 저장하지도 않는다
        //   (이 프로젝트의 씬 수정 규칙 · 진행상황 참조).
        // ⚠ 되살리려면 인스펙터에서 이 칸만 켜면 된다. 코드는 한 줄도 안 지웠다.
        // ------------------------------------------------------------------

        [Header("기능 스위치")]
        [Tooltip("타워·건설 기능을 쓸 것인가. ★ 2026-08-20 유저 지시로 <b>끈 상태</b>다 " +
                 "(«플레이에서만 제거» · 코드는 남겼다).\n" +
                 "끄면 이 서비스가 Instance 를 채우지 않으므로 건설 버튼·예정지·포탑 스폰·" +
                 "세이브 항목이 전부 사라진다. 되살리려면 이 칸만 켜면 된다")]
        [SerializeField] bool featureEnabled = false;

        /// <summary>
        /// 타워·건설을 쓰는가. <b>UI 가 버튼을 숨길 때 읽는다</b> —
        /// <see cref="Instance"/> 가 null 인 것과 «서비스를 아직 못 찾은 것» 을 구별하려면
        /// 이 값이 따로 있어야 한다(그 구별이 없으면 버튼이 «오류» 로 보인다).
        /// </summary>
        public static bool FeatureEnabled { get; private set; }

        [Header("데이터")]
        [SerializeField] BuildingDefinitionSO turretDefinition;
        [SerializeField] BalanceConfigSO balance;

        [Header("템플릿 (비워두면 이름으로 찾는다)")]
        [Tooltip("복제할 포탑 원본. 씬의 Templates 아래 비활성 오브젝트.\n" +
                 "MCP 로는 씬 오브젝트 참조를 인스펙터에 넣을 수 없어서(진행상황 8절 4번), " +
                 "비어 있으면 아래 경로로 직접 찾는다")]
        [SerializeField] TowerUnit towerTemplate;

        [SerializeField] string templatePath = "Templates/Building_Templates/Tower_Template";

        [Header("클릭 판정 (RallyPointService 와 같은 값으로 둘 것)")]
        [Min(0f)] [SerializeField] float clickThresholdPixels = 4f;

        [Header("오버레이 (모체 하나를 복제해서 쓴다 — 비활성으로 둘 것)")]
        [Tooltip("건설 예정지·미리보기를 그릴 사각형의 원본. UI_Root/BuildOverlay 아래")]
        [SerializeField] RectTransform siteTemplate;

        [SerializeField] RectTransform overlayParent;

        [SerializeField] Color validColor = new Color(0.35f, 0.95f, 0.75f, 0.35f);
        [SerializeField] Color invalidColor = new Color(0.95f, 0.35f, 0.35f, 0.35f);
        [SerializeField] Color pendingColor = new Color(0.98f, 0.82f, 0.35f, 0.30f);
        [SerializeField] Color progressColor = new Color(0.45f, 0.95f, 0.78f, 0.45f);

        [Header("디버그")]
        [SerializeField] bool logChanges = true;

        readonly List<BuildSite> _sites = new List<BuildSite>();

        /// <summary>이번 프레임의 배정에서 아직 자리를 안 맡은 캐릭터들 (매번 다시 채운다).</summary>
        readonly List<Units.CharacterBehavior> _freeWorkers = new List<Units.CharacterBehavior>();

        /// <summary>배정을 마지막으로 계산한 프레임. 프레임당 한 번만 돌게 한다.</summary>
        int _assignFrame = -1;

        readonly List<RectTransform> _overlays = new List<RectTransform>();

        Camera _camera;
        MapGenerator _map;
        Transform _towerRoot;

        Vector3Int _previewMinCell;
        bool _hasPreview;
        bool _previewValid;

        Vector2 _pressPosition;
        bool _pressActive;
        bool _pressStartedOverUI;

        /// <summary>지금까지 실제로 완성한 건물 수. 다음 건설 비용을 정하는 회차다.</summary>
        int _builtCount;

        public static BuildService Instance { get; private set; }

        /// <summary>지금 맵 클릭(자리 지정)을 기다리는 중인지.</summary>
        public bool IsPicking { get; private set; }

        public BuildingDefinitionSO TurretDefinition => turretDefinition;

        /// <summary>아직 안 지어진 예정지들. <c>CharacterBehavior</c> 가 읽어간다.</summary>
        public IReadOnlyList<BuildSite> PendingSites => _sites;

        /// <summary>지금 자리를 하나 찍으면 나가는 비용.</summary>
        public int CurrentCost =>
            turretDefinition != null ? turretDefinition.CostFor(_builtCount + _sites.Count) : 0;

        /// <summary>개수 제한에 걸렸는지 (시트의 <c>Max_count</c>. 포탑은 0 = 무제한).</summary>
        public bool AtLimit =>
            turretDefinition != null && turretDefinition.AtLimit(_builtCount + _sites.Count);

        /// <summary>지금 자리를 찍을 수 있는지 — 버튼 활성화 판단에 쓴다.</summary>
        public bool CanPlace
        {
            get
            {
                if (turretDefinition == null || AtLimit) return false;
                ResourceManager res = ResourceManager.Instance;
                return res != null && res.CanAfford(CurrentCost);
            }
        }

        void Awake()
        {
            FeatureEnabled = featureEnabled;
            if (!featureEnabled)
            {
                // ⚠ <b>Instance 를 채우지 않는다</b> — 그것이 이 기능을 끄는 방법이다(맨 위 ★★).
                //   컴포넌트도 끈다: Update 가 클릭을 먹지 않게 하려는 것이다(빈 맵을 눌렀을 때
                //   집결지 지정이 «건설 대기 중» 으로 오해받는 사고를 막는다).
                Debug.Log("[Build] 타워·건설 기능이 꺼져 있습니다 — 건설 버튼·예정지·포탑이 " +
                          "생기지 않습니다 (BuildService.featureEnabled).", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            _camera = Camera.main;
            _map = FindAnyObjectByType<MapGenerator>();

            if (towerTemplate == null) towerTemplate = FindTemplate();
            if (towerTemplate == null)
                Debug.LogError($"[Build] 포탑 템플릿을 찾지 못했습니다 ('{templatePath}'). " +
                               "건설이 완료돼도 포탑이 생기지 않습니다.", this);

            ResolveOverlay();
        }

        /// <summary>
        /// 경로로 템플릿을 찾는다.
        ///
        /// ⚠️ <b>루트까지 비활성이라 <c>GameObject.Find</c> 를 쓸 수 없다.</b> 템플릿들이 들어있는
        /// <c>Templates</c> 루트 자체가 꺼져 있는데(진행상황 1절), <c>GameObject.Find</c> 는
        /// 비활성 오브젝트를 아예 못 찾는다 — 실제로 이 함수를 그걸로 짰다가 플레이 모드에서
        /// "템플릿을 찾지 못했습니다" 가 떴다. 씬의 루트 목록에서 직접 이름을 맞춰야 한다
        /// (<c>GetRootGameObjects</c> 는 비활성도 돌려준다). 그 아래는 <c>Transform.Find</c> 로
        /// 내려가면 되고, 이쪽은 원래 비활성도 찾는다.
        /// </summary>
        TowerUnit FindTemplate()
        {
            if (string.IsNullOrEmpty(templatePath)) return null;

            string[] parts = templatePath.Split('/');

            Transform node = null;
            foreach (GameObject go in gameObject.scene.GetRootGameObjects())
            {
                if (go.name != parts[0]) continue;
                node = go.transform;
                break;
            }
            if (node == null) return null;

            for (int i = 1; i < parts.Length && node != null; i++) node = node.Find(parts[i]);
            return node != null ? node.GetComponent<TowerUnit>() : null;
        }

        void ResolveOverlay()
        {
            // 오버레이는 HUD 패널보다 뒤에 그려져야 한다 — 집결지 표시와 같은 규칙.
            GameObject canvas = GameObject.Find("UI_Root");
            Transform overlay = canvas != null ? canvas.transform.Find("BuildOverlay") : null;

            if (siteTemplate == null && overlay != null)
                siteTemplate = overlay.Find("BuildRangeTemplate") as RectTransform;

            if (overlayParent == null) overlayParent = overlay as RectTransform;
            if (overlayParent == null && siteTemplate != null)
                overlayParent = siteTemplate.parent as RectTransform;

            if (siteTemplate != null) siteTemplate.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // 배치 모드
        // ------------------------------------------------------------------

        public void TogglePicking()
        {
            if (IsPicking) CancelPicking();
            else BeginPicking();
        }

        public void BeginPicking()
        {
            if (IsPicking) return;

            if (!CanPlace)
            {
                HudLog.Add(AtLimit ? "더 이상 건설할 수 없습니다"
                                   : $"에너지가 부족합니다 (건설 비용 {CurrentCost})",
                           HudLogKind.Danger);
                return;
            }

            IsPicking = true;
            _pressActive = false;
            HudLog.Add($"건설 자리 지정 — {FootprintSize}x{FootprintSize} 범위를 클릭하세요 " +
                       $"(비용 {CurrentCost}, Esc 취소)", HudLogKind.Warn);
        }

        public void CancelPicking() => IsPicking = false;

        int FootprintSize => turretDefinition != null ? Mathf.Max(1, turretDefinition.footprintTiles) : 2;

        void Update()
        {
            UpdatePreview();
            if (IsPicking) HandlePicking();
            UpdateOverlay();
        }

        void UpdatePreview()
        {
            _hasPreview = false;
            if (!IsPicking || IsPointerOverUI()) return;

            if (_camera == null) _camera = Camera.main;
            Mouse mouse = Mouse.current;
            if (_camera == null || mouse == null || _map == null) return;

            Vector3 world = _camera.ScreenToWorldPoint(mouse.position.ReadValue());
            world.z = 0f;

            _previewMinCell = SnapToFootprint(world);
            _previewValid = IsAreaBuildable(_previewMinCell);
            _hasPreview = true;
        }

        /// <summary>
        /// 마우스가 가리키는 지점을 발판의 좌하단 칸으로 바꾼다.
        /// 2x2 는 마우스가 있는 칸이 <b>발판의 중앙에 가깝게</b> 오도록 반 칸 당긴다 —
        /// 커서가 항상 사각형의 좌하단 구석에 붙어 있으면 조준하기 어렵다.
        /// </summary>
        Vector3Int SnapToFootprint(Vector3 world)
        {
            Vector3Int cell = _map.WorldToCell(world);
            int back = (FootprintSize - 1) / 2;
            return new Vector3Int(cell.x - back, cell.y - back, 0);
        }

        void HandlePicking()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelPicking();
                HudLog.Add("건설 취소");
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // 우클릭 — 커서 아래의 예정지를 취소하고 비용을 돌려준다.
            if (mouse.rightButton.wasPressedThisFrame)
            {
                if (_hasPreview) CancelSiteAt(_previewMinCell);
                CancelPicking();
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                _pressActive = true;
                _pressPosition = mouse.position.ReadValue();
                _pressStartedOverUI = IsPointerOverUI();
                return;
            }

            if (!mouse.leftButton.wasReleasedThisFrame) return;

            bool wasPress = _pressActive;
            _pressActive = false;
            if (!wasPress || _pressStartedOverUI) return;

            // 카메라를 끌었던 것이면 배치가 아니다 (UnitSelector·RallyPointService 와 같은 규칙).
            Vector2 release = mouse.position.ReadValue();
            if ((release - _pressPosition).magnitude >= clickThresholdPixels) return;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null || _map == null) return;

            Vector3 world = _camera.ScreenToWorldPoint(release);
            world.z = 0f;

            if (TryPlaceSite(SnapToFootprint(world))) CancelPicking();
        }

        // ------------------------------------------------------------------
        // 예정지
        // ------------------------------------------------------------------

        /// <summary>발판 전체가 지을 수 있는 칸인지 (맵 안 + 벽·구조물 아님 + 다른 예정지와 안 겹침).</summary>
        public bool IsAreaBuildable(Vector3Int minCell)
        {
            if (_map == null || turretDefinition == null) return false;

            foreach (Vector3Int c in MapGenerator.FootprintCellsFrom(minCell, FootprintSize))
                if (!_map.IsCellPlaceable(c)) return false;

            int size = FootprintSize;
            for (int i = 0; i < _sites.Count; i++)
            {
                Vector3Int o = _sites[i].MinCell;
                if (minCell.x < o.x + size && o.x < minCell.x + size &&
                    minCell.y < o.y + size && o.y < minCell.y + size)
                    return false;
            }
            return true;
        }

        public bool TryPlaceSite(Vector3Int minCell)
        {
            if (turretDefinition == null) return false;

            if (!IsAreaBuildable(minCell))
            {
                HudLog.Add("그 자리에는 지을 수 없습니다", HudLogKind.Danger);
                return false;
            }
            if (AtLimit)
            {
                HudLog.Add("더 이상 건설할 수 없습니다", HudLogKind.Danger);
                return false;
            }

            int cost = CurrentCost;
            ResourceManager res = ResourceManager.Instance;
            if (res == null || !res.TrySpend(cost))
            {
                HudLog.Add($"에너지가 부족합니다 (건설 비용 {cost})", HudLogKind.Danger);
                return false;
            }

            _sites.Add(new BuildSite
            {
                MinCell = minCell,
                Center = _map.FootprintCenterWorld(minCell, FootprintSize),
                Definition = turretDefinition,
                PaidCost = cost,
            });

            if (logChanges) Debug.Log($"[Build] 건설 예정지 등록 {minCell} · 비용 {cost}", this);
            HudLog.Add($"{turretDefinition.DisplayName} 건설 예약 (에너지 {cost})", HudLogKind.Good);
            return true;
        }

        /// <summary>그 자리에 걸쳐 있는 예정지를 취소하고 비용을 전액 환불한다.</summary>
        public bool CancelSiteAt(Vector3Int minCell)
        {
            int size = FootprintSize;
            for (int i = 0; i < _sites.Count; i++)
            {
                Vector3Int o = _sites[i].MinCell;
                bool overlaps = minCell.x < o.x + size && o.x < minCell.x + size &&
                                minCell.y < o.y + size && o.y < minCell.y + size;
                if (!overlaps) continue;

                ResourceManager.Instance?.AddEnergy(_sites[i].PaidCost);
                HudLog.Add($"건설 예약 취소 (에너지 +{_sites[i].PaidCost})");
                _sites.RemoveAt(i);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // 캐릭터가 부르는 부분
        // ------------------------------------------------------------------

        /// <summary>
        /// 이 캐릭터가 맡은 예정지. 없으면 null.
        ///
        /// <b>배정은 캐릭터가 아니라 여기서 정한다</b>(유저 확정) — 캐릭터마다 스스로 가까운
        /// 자리를 고르면 같은 자리에 여럿이 붙거나(한 자리에는 한 명만 붙는 규칙 위반) 반대로
        /// 아무도 안 맡는 자리가 생긴다. 전체를 한 번에 보고 <b>예정지마다 지금 가장 적합한
        /// 캐릭터 한 명</b>을 붙인다.
        ///
        /// <b>적합도 기준은 거리 하나</b>다 — <b>예정지에서 가장 가까운 캐릭터</b>가 맡는다
        /// (유저 확정 2026-08-12: 전술 지침에서 '건물 건설' 항목이 사라지면서 "건설 전담
        /// 캐릭터" 개념도 같이 없어졌다). 인스펙터의 <c>BuildRange</c> 에 값을 넣으면 그
        /// 거리 안의 자리만 후보가 된다(0 = 무제한, 기본값).
        ///
        /// 계산은 <b>프레임당 한 번</b>만 돈다. 캐릭터들의 Update 순서를 알 수 없으므로
        /// 첫 호출이 계산을 끌고 나머지는 그 결과를 읽는다(실행 순서에 의존하지 않게).
        /// </summary>
        public BuildSite AssignedSiteFor(Units.CharacterBehavior worker)
        {
            if (worker == null || _sites.Count == 0) return null;

            if (_assignFrame != Time.frameCount)
            {
                _assignFrame = Time.frameCount;
                AssignBuilders();
            }

            for (int i = 0; i < _sites.Count; i++)
                if (_sites[i].Builder == worker) return _sites[i];
            return null;
        }

        /// <summary>
        /// 예정지마다 캐릭터 한 명씩 붙인다.
        ///
        /// <b>이미 맡고 있던 배정은 그대로 둔다</b> — 매 프레임 최적을 새로 뽑으면 거리가
        /// 비슷한 두 캐릭터가 서로 자리를 뺏어 둘 다 오가기만 하고 아무것도 안 지어진다.
        /// 맡고 있던 캐릭터가 죽거나 전투에 휘말리는 등 더 이상 일할 수 없게 됐을 때만
        /// 자리를 놓고, 그 자리는 남은 후보 중 최적자에게 넘어간다.
        /// </summary>
        void AssignBuilders()
        {
            _freeWorkers.Clear();
            var units = UnitRegistry.All;
            for (int i = 0; i < units.Count; i++)
            {
                DamageableUnit u = units[i];
                if (u == null || !u.IsAlive || u.Kind != UnitKind.Character) continue;

                var worker = u.GetComponent<Units.CharacterBehavior>();
                if (worker != null && worker.CanTakeBuildOrder) _freeWorkers.Add(worker);
            }

            // 유지되는 배정은 후보 목록에서 빼둔다(한 명이 두 자리를 맡지 않게).
            for (int i = 0; i < _sites.Count; i++)
            {
                BuildSite s = _sites[i];
                if (s.Builder == null) continue;
                if (!_freeWorkers.Remove(s.Builder)) s.Builder = null;
            }

            // 남은 자리 × 남은 후보 중 <b>가장 가까운 짝</b>부터 차례로 붙인다.
            //
            // ⚠️ 예전에는 "전술 우선 행동이 건물 건설인 전담 캐릭터 먼저 → 그다음 거리순"
            //    2단 기준이었다. 전술 지침에서 '건물 건설' 항목이 사라졌으므로(유저 확정
            //    2026-08-12: "건설은 그냥 제일 가까운 캐릭터가 우선 수행") 기준은 거리 하나다.
            while (_freeWorkers.Count > 0)
            {
                BuildSite bestSite = null;
                Units.CharacterBehavior bestWorker = null;
                float bestSqr = float.PositiveInfinity;

                for (int i = 0; i < _sites.Count; i++)
                {
                    BuildSite s = _sites[i];
                    if (s.Builder != null) continue;

                    for (int w = 0; w < _freeWorkers.Count; w++)
                    {
                        Units.CharacterBehavior worker = _freeWorkers[w];
                        float range = worker.BuildRange;
                        float sqr = ((Vector2)(s.Center - worker.transform.position)).sqrMagnitude;

                        // 인스펙터에서 거리 제한을 걸어둔 경우만 후보를 줄인다(0 = 무제한).
                        if (range > 0f && sqr > range * range) continue;
                        if (sqr >= bestSqr) continue;

                        bestSite = s;
                        bestWorker = worker;
                        bestSqr = sqr;
                    }
                }

                if (bestWorker == null) break;   // 더 붙일 짝이 없다

                bestSite.Builder = bestWorker;
                _freeWorkers.Remove(bestWorker);
            }
        }

        /// <summary>
        /// 현장에서 <paramref name="seconds"/> 만큼 일한다. 한 자리에는 한 명만 붙으므로
        /// 사람이 늘어도 빨라지지 않는다 — 대신 여러 자리를 동시에 지을 수 있다.
        /// 완성되면 즉시 건물을 세우고 예정지를 없앤다.
        /// </summary>
        public void Contribute(BuildSite site, float seconds)
        {
            if (site == null || seconds <= 0f) return;
            if (!_sites.Contains(site)) return;

            site.Progress += seconds;
            if (site.Progress < site.Required) return;

            Complete(site);
        }

        void Complete(BuildSite site)
        {
            _sites.Remove(site);

            TowerUnit tower = Spawn(site);
            if (tower == null) return;

            _builtCount++;

            if (logChanges) Debug.Log($"[Build] {site.Definition.DisplayName} 완성 {site.MinCell}", this);
            HudLog.Add($"{site.Definition.DisplayName} 건설 완료", HudLogKind.Good);
        }

        /// <summary>템플릿 복제 (진행상황 5절 — 이 프로젝트의 모든 유닛 생성 방식).</summary>
        TowerUnit Spawn(BuildSite site)
        {
            if (towerTemplate == null) towerTemplate = FindTemplate();
            if (towerTemplate == null)
            {
                Debug.LogError("[Build] 포탑 템플릿이 없어 건물을 세우지 못했습니다.", this);
                return null;
            }

            if (_towerRoot == null)
            {
                _towerRoot = new GameObject("Towers").transform;
                _towerRoot.SetParent(transform, false);
            }

            TowerUnit tower = Instantiate(towerTemplate, site.Center, Quaternion.identity, _towerRoot);
            tower.name = $"{site.Definition.DisplayName}_{_builtCount + 1}";
            tower.gameObject.SetActive(true);
            tower.Initialize(site.Definition, site.MinCell, balance);
            return tower;
        }

        // ==================================================================
        // 저장 복원 (2026-08-18 신설 — 98절)
        // ==================================================================

        /// <summary>
        /// 저장된 <b>완성된 포탑</b> 하나를 되살린다.
        ///
        /// ⚠ <b>건설 중이던 자리는 저장하지도 복원하지도 않는다</b> — 진행도가 캐릭터의 작업 배정
        /// (<see cref="AssignedSiteFor"/>)과 얽혀 있어 되살리면 배정이 어긋난 채로 남는다.
        /// 저장 시점에 짓고 있던 포탑은 <b>취소된 것</b>으로 다룬다(자원은 이미 나갔지만,
        /// 자동 저장이 웨이브 클리어·강화·사망 시점이라 건설 도중일 확률이 낮다).
        ///
        /// <b>건설 횟수(<see cref="_builtCount"/>)도 같이 올린다</b> — 이 값이 다음 건설 비용과
        /// 개수 상한을 정하므로(<see cref="CurrentCost"/>·<see cref="AtLimit"/>), 안 올리면
        /// 불러온 판에서 포탑을 <b>처음 값으로 다시</b> 지을 수 있다.
        /// </summary>
        public TowerUnit RestoreTower(Vector3Int minCell, int currentHp)
        {
            if (turretDefinition == null)
            {
                Debug.LogWarning("[Build] 포탑 정의가 없어 저장된 포탑을 복원하지 못했습니다.", this);
                return null;
            }

            if (towerTemplate == null) towerTemplate = FindTemplate();
            if (towerTemplate == null)
            {
                Debug.LogError("[Build] 포탑 템플릿이 없어 저장된 포탑을 복원하지 못했습니다.", this);
                return null;
            }

            if (_map == null) _map = FindAnyObjectByType<MapGenerator>();

            if (_towerRoot == null)
            {
                _towerRoot = new GameObject("Towers").transform;
                _towerRoot.SetParent(transform, false);
            }

            Vector3 center = _map != null
                ? _map.FootprintCenterWorld(minCell, FootprintSize)
                : new Vector3(minCell.x + FootprintSize * 0.5f, minCell.y + FootprintSize * 0.5f, 0f);

            TowerUnit tower = Instantiate(towerTemplate, center, Quaternion.identity, _towerRoot);
            tower.name = $"{turretDefinition.DisplayName}_{_builtCount + 1}";
            tower.gameObject.SetActive(true);
            tower.Initialize(turretDefinition, minCell, balance);

            _builtCount++;

            // 체력은 최대치로 세워진 뒤 저장된 값까지 깎는다 — 체력을 직접 넣는 통로가 없고,
            // 만들 이유도 없다(피해 파이프라인이 유일한 감소 경로여야 규칙이 한 곳에 남는다).
            int target = Mathf.Clamp(currentHp, 1, tower.MaxHp);
            if (target < tower.CurrentHp) tower.ApplyDamage(tower.CurrentHp - target);

            return tower;
        }

        /// <summary>지금 세워져 있는 포탑 전부. 저장할 때 훑는다.</summary>
        public TowerUnit[] AliveTowers() =>
            _towerRoot == null
                ? System.Array.Empty<TowerUnit>()
                : _towerRoot.GetComponentsInChildren<TowerUnit>(includeInactive: false);

        // ------------------------------------------------------------------
        // 오버레이 — 예정지 사각형 + 미리보기
        // ------------------------------------------------------------------

        void UpdateOverlay()
        {
            if (overlayParent == null || siteTemplate == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            int need = _sites.Count + (_hasPreview ? 1 : 0);
            while (_overlays.Count < need)
            {
                RectTransform clone = Instantiate(siteTemplate, overlayParent);
                clone.name = $"BuildSite_{_overlays.Count + 1}";
                _overlays.Add(clone);
            }

            float side = FootprintSize;

            for (int i = 0; i < _overlays.Count; i++)
            {
                RectTransform item = _overlays[i];
                bool used = i < need;
                if (item.gameObject.activeSelf != used) item.gameObject.SetActive(used);
                if (!used) continue;

                bool isPreview = _hasPreview && i == _sites.Count;
                Vector3 world = isPreview
                    ? _map.FootprintCenterWorld(_previewMinCell, FootprintSize)
                    : _sites[i].Center;

                Vector3 screen = _camera.WorldToScreenPoint(world);
                if (screen.z < 0f) { item.gameObject.SetActive(false); continue; }

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    overlayParent, screen, null, out Vector2 local);
                item.anchoredPosition = local;
                item.sizeDelta = WorldSizeToLocalSize(world, side);

                var graphic = item.GetComponent<UnityEngine.UI.Graphic>();
                if (graphic == null) continue;

                graphic.color = isPreview
                    ? (_previewValid ? validColor : invalidColor)
                    : Color.Lerp(pendingColor, progressColor, _sites[i].Ratio);
            }
        }

        /// <summary>
        /// 월드에서 <paramref name="worldSize"/> 타일인 정사각형이 지금 화면에서 몇 로컬 유닛인지.
        /// 카메라 줌·해상도·CanvasScaler 배율이 전부 자동으로 반영된다
        /// (<see cref="RallyPointService"/> 와 같은 계산).
        /// </summary>
        Vector2 WorldSizeToLocalSize(Vector3 centerWorld, float worldSize)
        {
            float half = worldSize * 0.5f;
            Vector3 c1 = centerWorld + new Vector3(half, half, 0f);
            Vector3 c2 = centerWorld + new Vector3(-half, -half, 0f);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                overlayParent, _camera.WorldToScreenPoint(c1), null, out Vector2 l1);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                overlayParent, _camera.WorldToScreenPoint(c2), null, out Vector2 l2);

            return new Vector2(Mathf.Abs(l1.x - l2.x), Mathf.Abs(l1.y - l2.y));
        }

        static bool IsPointerOverUI()
        {
            EventSystem es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }
    }
}
