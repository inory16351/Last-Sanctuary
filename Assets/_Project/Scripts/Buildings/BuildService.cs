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

        void Awake() => Instance = this;

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
            HudLog.Add($"{turretDefinition.displayName} 건설 예약 (에너지 {cost})", HudLogKind.Good);
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
        /// <b>적합도 순서</b>: ① 전술 우선 행동이 "건물 건설"인 캐릭터가 먼저, ② 그다음 거리순.
        /// 전담이 아닌 캐릭터는 자기 <c>AssistBuildRange</c> 안의 자리만 후보로 본다.
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

            // 남은 자리 × 남은 후보 중 가장 적합한 짝부터 차례로 붙인다.
            while (_freeWorkers.Count > 0)
            {
                BuildSite bestSite = null;
                Units.CharacterBehavior bestWorker = null;
                bool bestDedicated = false;
                float bestSqr = float.PositiveInfinity;

                for (int i = 0; i < _sites.Count; i++)
                {
                    BuildSite s = _sites[i];
                    if (s.Builder != null) continue;

                    for (int w = 0; w < _freeWorkers.Count; w++)
                    {
                        Units.CharacterBehavior worker = _freeWorkers[w];
                        bool dedicated = worker.BuildDedicated;
                        float range = worker.AssistBuildRange;
                        float sqr = ((Vector2)(s.Center - worker.transform.position)).sqrMagnitude;

                        // 전담이 아니면 눈앞(도와줄 만한 거리)의 자리만 맡는다.
                        if (!dedicated && (range <= 0f || sqr > range * range)) continue;

                        if (bestWorker != null)
                        {
                            if (bestDedicated && !dedicated) continue;
                            if (bestDedicated == dedicated && sqr >= bestSqr) continue;
                        }

                        bestSite = s;
                        bestWorker = worker;
                        bestDedicated = dedicated;
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
            tower.OnDestroyed += HandleTowerDestroyed;

            if (logChanges) Debug.Log($"[Build] {site.Definition.displayName} 완성 {site.MinCell}", this);
            HudLog.Add($"{site.Definition.displayName} 건설 완료", HudLogKind.Good);
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
            tower.name = $"{site.Definition.displayName}_{_builtCount + 1}";
            tower.gameObject.SetActive(true);
            tower.Initialize(site.Definition, site.MinCell, balance);
            return tower;
        }

        /// <summary>
        /// 포탑이 파괴되면 건설 회차를 하나 되돌린다 — 부서질 때마다 비용만 계속 올라가면
        /// 후반에 재건이 불가능해진다. (시트 Docs 의 "철거하면 회차도 되돌릴지 별도 결정 필요"
        /// 항목에 대한 이 프로젝트의 답이다.)
        /// </summary>
        void HandleTowerDestroyed(TowerUnit tower)
        {
            tower.OnDestroyed -= HandleTowerDestroyed;
            _builtCount = Mathf.Max(0, _builtCount - 1);
        }

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
