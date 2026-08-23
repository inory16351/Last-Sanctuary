using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LastSanctuary.Combat;
using LastSanctuary.Fog;
using LastSanctuary.Map;
using LastSanctuary.UI;
using LastSanctuary.Units;

namespace LastSanctuary.Relics
{
    /// <summary>발굴 가능 칸 하나. <see cref="LastSanctuary.Buildings.BuildSite"/> 와 같은 모양이다.</summary>
    public class DigSite
    {
        public Vector3Int Cell;
        public Vector3 Center;

        /// <summary>한 번이라도 캐릭터 시야에 들어왔는가 — <b>기억</b>이다(표 Info 시트 «보이기»).</summary>
        public bool Revealed;

        /// <summary>유저가 «파라» 고 눌렀는가. 누르기 전에는 캐릭터가 가지 않는다.</summary>
        public bool Ordered;

        /// <summary>지금까지 쌓인 발굴 시간(초).</summary>
        public float Progress;

        /// <summary>이 자리를 맡은 캐릭터. <b>한 자리에 한 명</b>(건설과 같은 규칙).</summary>
        public CharacterBehavior Digger;

        public float Ratio(float required) => required > 0f ? Mathf.Clamp01(Progress / required) : 0f;
    }

    /// <summary>
    /// ★★★ <b>유물 발굴</b> (2026-08-23 신설 · 유저 지시).
    ///
    /// <code>
    ///   ② 맵이 생성(게임 재시작)될 때마다 맵 전체에 랜덤한 칸이 총 N개 발굴 가능 칸으로 선정
    ///   ③ 그 칸이 캐릭터의 시야에 보이면 느낌표가 뜨고 클릭 가능해진다.
    ///      클릭하면 «기존에 삭제된 건설처럼» 가장 가까운 캐릭터가 15초에 걸쳐 발굴하고,
    ///      일정 확률에 따라 에너지·체력 회복/감소·유물 획득 등이 일어난다
    /// </code>
    ///
    /// <b>왜 건설과 «같은 구조» 인가</b> — 유저 지시가 그렇게 못박았고("기존에 삭제된 건설처럼"),
    /// 실제로 필요한 것이 같다: «자리 목록 · 한 자리에 한 명 · 걸어가서 시간을 채운다 ·
    /// 진행도를 화면에 겹쳐 그린다». <see cref="LastSanctuary.Buildings.BuildService"/> 가
    /// 이미 그 네 가지를 다 갖고 있으므로 <b>그 구조를 그대로 옮겼다</b> — 두 기능이 서로 다른
    /// 조작감을 갖지 않게 하려는 것이다(그 클래스가 집결지에서 구조를 가져온 것과 같은 이유).
    ///
    /// ★ <b>다른 점 하나 — 클릭을 «월드» 가 아니라 «UI» 로 받는다.</b>
    ///   건설은 «배치 모드» 로 들어가 맵을 클릭하지만, 발굴은 모드가 없고 <b>칸에 뜬 느낌표</b>를
    ///   직접 누른다(지시 3번). 월드 클릭으로 받으면 <see cref="UnitSelector"/>·집결지와
    ///   <b>같은 클릭을 두고 다툰다</b>. 느낌표를 <b>UI 버튼</b>으로 두면 그쪽들이 이미
    ///   «포인터가 UI 위면 무시» 하므로 다툼이 아예 생기지 않는다.
    ///
    /// ★ <b>자리는 <see cref="Start"/> 에서 고른다 — 그것이 곧 «맵이 생성될 때마다» 다.</b>
    ///   이 프로젝트의 맵은 <see cref="MapGenerator.Awake"/> 가 <b>판마다 새 씨앗으로</b>
    ///   다시 만든다(그쪽 <c>ResolveStartupSeed</c>). 유니티는 «모든 Awake 가 모든 Start 보다
    ///   먼저» 를 보장하므로, <see cref="Start"/> 에 오면 <see cref="MapGenerator.MapSize"/> ·
    ///   걸을 수 있는 칸이 이미 확정돼 있다 — 안개·흐름장·스포너가 전부 같은 이유로
    ///   <c>Start</c> 에서 맵을 읽는다.
    /// ⚠ <b>이어하기는 새로 고르지 않는다</b> — 저장된 자리를 <see cref="Restore"/> 가 넣는다.
    ///   안 그러면 이어할 때마다 자리가 바뀌어 «가던 캐릭터가 허공을 판다».
    /// </summary>
    public class RelicDigService : MonoBehaviour
    {
        public static RelicDigService Instance { get; private set; }

        // ------------------------------------------------------------------
        // 인스펙터 — 유저 지시: *"발굴 가능 칸 갯수는 너가 정하고 에딧에서 수정 가능하게"*
        // ------------------------------------------------------------------

        [Header("발굴 가능 칸")]
        [Tooltip("맵 전체에 뿌릴 발굴 가능 칸 수.\n" +
                 "★ 기본 24 — 맵이 넓어 한 판에 다 밝히기 어렵고, 이 정도면 «탐험하다 가끔 만난다» " +
                 "가 된다. 너무 많으면 발굴이 주 수입원이 되어 웨이브를 미루는 것이 최적 전략이 된다")]
        [Min(0)] [SerializeField] int digSiteCount = 24;

        [Tooltip("넥서스에서 이만큼(타일) 떨어진 곳에만 둔다 — 시작하자마자 다 캐지 않게")]
        [Min(0f)] [SerializeField] float minDistanceFromNexus = 14f;

        [Tooltip("발굴 칸끼리 이만큼(타일)은 떨어뜨린다 — 한곳에 몰리면 «한 번 가서 다 캔다» 가 된다")]
        [Min(0f)] [SerializeField] float minSpacing = 10f;

        [Tooltip("자리를 고를 때 시도할 최대 횟수. 이만큼 굴려도 자리가 안 나오면 포기한다")]
        [Min(1)] [SerializeField] int placementAttempts = 4000;

        [Header("발굴")]
        [Tooltip("한 칸을 파는 데 걸리는 시간(초). 유저 지시: 15초")]
        [Min(1f)] [SerializeField] float digSeconds = 15f;

        [Tooltip("캐릭터가 이 거리(타일) 안에 들어와야 발굴이 진행된다")]
        [Min(0.5f)] [SerializeField] float digWorkRange = 1.6f;

        [Header("오버레이 (모체 하나를 복제해서 쓴다 — 비활성으로 둘 것)")]
        [Tooltip("느낌표 표식의 원본. UI_Root/DigOverlay 아래. 비어 있으면 이름으로 찾는다")]
        [SerializeField] RectTransform markerTemplate;

        [SerializeField] RectTransform overlayParent;

        [SerializeField] Color idleColor = new Color(0.98f, 0.85f, 0.35f, 0.95f);
        [SerializeField] Color orderedColor = new Color(0.45f, 0.95f, 0.78f, 0.95f);

        [Tooltip("표식의 화면 크기(픽셀). 카메라 줌과 무관하게 일정하게 둔다 — 작아지면 못 누른다")]
        [Min(8f)] [SerializeField] float markerPixels = 34f;

        [Header("디버그")]
        [SerializeField] bool logChanges = true;

        readonly List<DigSite> _sites = new List<DigSite>();
        readonly List<RectTransform> _markers = new List<RectTransform>();
        readonly List<CharacterBehavior> _freeWorkers = new List<CharacterBehavior>();

        int _assignFrame = -1;
        Camera _camera;
        MapGenerator _map;
        FogOfWarService _fog;
        RelicDigTableSO _table;

        /// <summary>지금 남아 있는 발굴 칸. <see cref="CharacterBehavior"/> 가 읽어간다.</summary>
        public IReadOnlyList<DigSite> Sites => _sites;

        /// <summary>한 칸을 파는 데 필요한 시간(초). UI 가 진행도를 그릴 때도 쓴다.</summary>
        public float DigSeconds => Mathf.Max(1f, digSeconds);

        /// <summary>지금 화면에 보이는(= 발견한) 칸 수. 액션 버튼의 뱃지가 읽는다.</summary>
        public int RevealedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _sites.Count; i++) if (_sites[i].Revealed) n++;
                return n;
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            // ★ 드랍 판정은 상태가 없는 정적 클래스다 — <b>이 서비스의 생애에 맞춰</b>
            //   붙였다 뗀다(그 클래스의 ⚠: 안 떼면 다음 판에 두 배로 걸린다).
            RelicDropService.Hook();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // ⚠ 정적 이벤트를 붙여 둔 것이 있으므로 판이 끝날 때 반드시 뗀다(그 클래스들의 ⚠).
            RelicDropService.Unhook();
            RelicEffectService.Unhook();
        }

        void Start()
        {
            _camera = Camera.main;
            _map = FindAnyObjectByType<MapGenerator>();
            _fog = FindAnyObjectByType<FogOfWarService>();
            _table = RelicDigTableSO.Load();

            ResolveOverlay();

            // ★ 이어하기가 예약돼 있으면 자리를 새로 고르지 않는다 — 저장된 자리를
            //   <see cref="Restore"/> 가 넣어 준다(그쪽이 늦게 도착해도 되도록 비워 둔다).
            if (Save.SaveService.PendingLoad != null) return;

            PickSites();
        }

        void ResolveOverlay()
        {
            GameObject canvas = GameObject.Find("UI_Root");
            Transform overlay = canvas != null ? canvas.transform.Find("DigOverlay") : null;

            if (markerTemplate == null && overlay != null)
                markerTemplate = overlay.Find("DigMarkerTemplate") as RectTransform;
            if (overlayParent == null) overlayParent = overlay as RectTransform;
            if (overlayParent == null && markerTemplate != null)
                overlayParent = markerTemplate.parent as RectTransform;

            if (markerTemplate != null) markerTemplate.gameObject.SetActive(false);
            else Debug.LogWarning("[유물] DigOverlay/DigMarkerTemplate 을 찾지 못했습니다 — " +
                                  "발굴 칸이 화면에 안 보입니다.", this);
        }

        // ==================================================================
        // 자리 고르기
        // ==================================================================

        /// <summary>
        /// 맵 전체에서 발굴 가능 칸을 <see cref="digSiteCount"/> 개 고른다.
        ///
        /// 조건 셋(표 Info 시트 «자리 고르기»):
        /// <code>
        ///   ① 걸을 수 있고 벽·구조물이 없는 칸       (IsCellPlaceable)
        ///   ② 넥서스에서 minDistanceFromNexus 밖
        ///   ③ 다른 발굴 칸과 minSpacing 이상 떨어짐
        /// </code>
        /// ⚠ 조건을 만족하는 자리가 모자라면 <b>있는 만큼만</b> 둔다 — 무한 루프를 돌지 않는다.
        /// </summary>
        public void PickSites()
        {
            _sites.Clear();
            if (_map == null || digSiteCount <= 0) return;

            Vector3 nexus = NexusPosition();
            float nexusSqr = minDistanceFromNexus * minDistanceFromNexus;
            float spaceSqr = minSpacing * minSpacing;

            Vector2Int size = _map.MapSize;
            Vector2Int origin = _map.Origin;
            if (size.x <= 0 || size.y <= 0) return;

            int tries = 0;
            while (_sites.Count < digSiteCount && tries < placementAttempts)
            {
                tries++;
                var local = new Vector2Int(Random.Range(0, size.x), Random.Range(0, size.y));
                Vector3Int cell = _map.LocalToCell(local);
                if (!_map.IsCellPlaceable(cell)) continue;

                Vector3 world = _map.CellCenterWorld(cell);
                if (((Vector2)(world - nexus)).sqrMagnitude < nexusSqr) continue;

                bool tooClose = false;
                for (int i = 0; i < _sites.Count; i++)
                    if (((Vector2)(world - _sites[i].Center)).sqrMagnitude < spaceSqr) { tooClose = true; break; }
                if (tooClose) continue;

                _sites.Add(new DigSite { Cell = cell, Center = world });
            }

            if (logChanges)
                Debug.Log($"[유물] 발굴 가능 칸 {_sites.Count}개 배치 (목표 {digSiteCount} · 시도 {tries})", this);
            if (_sites.Count < digSiteCount)
                Debug.LogWarning($"[유물] 조건에 맞는 자리가 모자라 {_sites.Count}개만 두었습니다 — " +
                                 "minSpacing / minDistanceFromNexus 를 줄여보세요.", this);
        }

        Vector3 NexusPosition()
        {
            var nexus = FindAnyObjectByType<Nexus>();
            return nexus != null ? nexus.transform.position : Vector3.zero;
        }

        // ==================================================================
        // 매 프레임
        // ==================================================================

        void Update()
        {
            UpdateReveal();
            UpdateMarkers();

            // ★ 문턱형 유물(「두꺼워진 가피」)을 여기서 함께 재운다 — 그 하나 때문에
            //   MonoBehaviour 를 새로 두지 않는다(RelicEffectService.Tick 의 주석).
            RelicEffectService.Tick();
        }

        /// <summary>
        /// 시야에 들어온 칸을 <b>발견</b>으로 표시한다.
        ///
        /// ★ <b>안개가 걷힌 것만으로는 안 된다</b> — 유저 지시가 «캐릭터의 시야에 보일 경우» 다.
        ///   그래서 <see cref="FogOfWarService.IsVisible"/>(지금 누군가의 시야 안인가)를 본다.
        ///   ⚠ 한 번 본 칸은 <b>계속 보인다</b>(기억) — 시야를 벗어날 때마다 느낌표가
        ///   사라지면 «분명 봤는데 없어졌다» 가 된다.
        /// </summary>
        void UpdateReveal()
        {
            if (_fog == null || !_fog.IsReady) return;
            for (int i = 0; i < _sites.Count; i++)
            {
                DigSite s = _sites[i];
                if (s.Revealed) continue;
                if (!_fog.IsVisible(s.Cell)) continue;

                s.Revealed = true;
                HudLog.Add("발굴할 수 있는 자리를 찾았습니다", HudLogKind.Good);
            }
        }

        // ==================================================================
        // 표식 (UI 버튼)
        // ==================================================================

        void UpdateMarkers()
        {
            if (overlayParent == null || markerTemplate == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            int need = 0;
            for (int i = 0; i < _sites.Count; i++) if (_sites[i].Revealed) need++;

            while (_markers.Count < need)
            {
                RectTransform clone = Instantiate(markerTemplate, overlayParent);
                clone.name = $"DigMarker_{_markers.Count + 1}";
                _markers.Add(clone);
            }

            int slot = 0;
            for (int i = 0; i < _sites.Count; i++)
            {
                DigSite s = _sites[i];
                if (!s.Revealed) continue;

                RectTransform item = _markers[slot++];
                Vector3 screen = _camera.WorldToScreenPoint(s.Center);
                if (screen.z < 0f)
                {
                    if (item.gameObject.activeSelf) item.gameObject.SetActive(false);
                    continue;
                }

                if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    overlayParent, screen, null, out Vector2 local);
                item.anchoredPosition = local;
                item.sizeDelta = new Vector2(markerPixels, markerPixels);

                var img = item.GetComponent<Image>();
                if (img != null)
                {
                    // 파는 중이면 진행도를 색으로 보여준다(건설 오버레이와 같은 규칙).
                    img.color = s.Ordered
                        ? Color.Lerp(orderedColor, Color.white, s.Ratio(DigSeconds))
                        : idleColor;
                }

                // ⚠ 버튼은 <b>매 프레임 다시 배선한다</b> — 표식은 자리마다 재사용되므로
                //   («다섯 번째 표식» 이 프레임마다 다른 자리를 가리킬 수 있다) 지난 프레임의
                //   람다가 남아 있으면 <b>엉뚱한 자리를 판다</b>.
                var button = item.GetComponent<Button>();
                if (button != null)
                {
                    DigSite captured = s;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => Order(captured));
                    button.interactable = !captured.Ordered;
                }
            }

            for (int i = slot; i < _markers.Count; i++)
                if (_markers[i].gameObject.activeSelf) _markers[i].gameObject.SetActive(false);
        }

        /// <summary>느낌표를 눌렀다 — «파라» 고 지시한다.</summary>
        public void Order(DigSite site)
        {
            if (site == null || site.Ordered) return;
            site.Ordered = true;
            HudLog.Add("발굴을 지시했습니다 — 가장 가까운 캐릭터가 갑니다", HudLogKind.Good);
        }

        // ==================================================================
        // 배정 · 진행 (건설과 같은 규칙)
        // ==================================================================

        /// <summary>
        /// 이 캐릭터가 맡은 발굴 자리. 없으면 null.
        /// <see cref="LastSanctuary.Buildings.BuildService.AssignedSiteFor"/> 와 같은 구조다 —
        /// 배정은 <b>프레임당 한 번</b> 전체를 보고 정한다(캐릭터마다 스스로 고르면 한 자리에 몰린다).
        /// </summary>
        public DigSite AssignedSiteFor(CharacterBehavior worker)
        {
            if (worker == null || _sites.Count == 0) return null;

            if (_assignFrame != Time.frameCount)
            {
                _assignFrame = Time.frameCount;
                AssignDiggers();
            }

            for (int i = 0; i < _sites.Count; i++)
                if (_sites[i].Digger == worker) return _sites[i];
            return null;
        }

        void AssignDiggers()
        {
            _freeWorkers.Clear();
            var units = UnitRegistry.All;
            for (int i = 0; i < units.Count; i++)
            {
                DamageableUnit u = units[i];
                if (u == null || !u.IsAlive || u.Kind != UnitKind.Character) continue;
                var worker = u.GetComponent<CharacterBehavior>();
                if (worker != null && worker.CanTakeBuildOrder) _freeWorkers.Add(worker);
            }

            // 유지되는 배정은 후보에서 뺀다(한 명이 두 자리를 맡지 않게).
            for (int i = 0; i < _sites.Count; i++)
            {
                DigSite s = _sites[i];
                if (s.Digger == null) continue;
                if (!s.Ordered || !_freeWorkers.Remove(s.Digger)) s.Digger = null;
            }

            // 남은 자리 × 남은 후보 중 <b>가장 가까운 짝</b>부터 (유저 지시: «가장 가까운 캐릭터»).
            while (_freeWorkers.Count > 0)
            {
                DigSite bestSite = null;
                CharacterBehavior bestWorker = null;
                float bestSqr = float.PositiveInfinity;

                for (int i = 0; i < _sites.Count; i++)
                {
                    DigSite s = _sites[i];
                    if (!s.Ordered || s.Digger != null) continue;

                    for (int w = 0; w < _freeWorkers.Count; w++)
                    {
                        CharacterBehavior worker = _freeWorkers[w];
                        float sqr = ((Vector2)(s.Center - worker.transform.position)).sqrMagnitude;
                        if (sqr >= bestSqr) continue;
                        bestSite = s;
                        bestWorker = worker;
                        bestSqr = sqr;
                    }
                }

                if (bestWorker == null) break;
                bestSite.Digger = bestWorker;
                _freeWorkers.Remove(bestWorker);
            }
        }

        /// <summary>캐릭터가 이 거리 안에 들어와야 삽질이 진행된다.</summary>
        public float WorkRange => digWorkRange;

        /// <summary>
        /// 현장에서 <paramref name="seconds"/> 만큼 판다. 한 자리에 한 명이므로 사람이 늘어도
        /// 빨라지지 않는다 — 대신 <see cref="RelicEffectType.DigSpeed"/> 유물이 빠르게 한다.
        /// </summary>
        public void Contribute(DigSite site, float seconds, CharacterUnit digger)
        {
            if (site == null || seconds <= 0f || !_sites.Contains(site)) return;

            site.Progress += seconds * RelicEffectService.DigSpeedMultiplier(digger);
            if (site.Progress < DigSeconds) return;

            Complete(site, digger);
        }

        // ==================================================================
        // 결과
        // ==================================================================

        void Complete(DigSite site, CharacterUnit digger)
        {
            _sites.Remove(site);

            DigOutcomeRow row = _table != null ? _table.Roll() : null;
            if (row == null)
            {
                HudLog.Add("발굴을 마쳤지만 아무것도 나오지 않았습니다");
                return;
            }

            string what = ApplyOutcome(row, digger);
            string who = digger != null ? digger.DisplayName : "누군가";
            HudLog.Add(string.IsNullOrEmpty(what)
                           ? $"{who} — 발굴: {row.outcomeScript}"
                           : $"{who} — 발굴: {what}",
                       what.StartsWith("−") || row.outcomeType == "dig_hurt" ||
                       row.outcomeType == "dig_erosion_up"
                           ? HudLogKind.Warn
                           : HudLogKind.Good);

            if (logChanges) Debug.Log($"[유물] 발굴 완료 {site.Cell} → {row.outcomeType}", this);
        }

        /// <summary>
        /// 발굴 결과 한 줄을 적용한다 — 표 <c>DigOutcome</c> 시트의 <c>outcome_type</c> 과 1:1.
        ///
        /// ⚠ <b>여기 없는 타입은 «아무 일도 안 일어난다»</b>. 표에 새 결과를 더하면
        ///   반드시 가지도 함께 더할 것(등록기가 유물 효과에 대해 하는 경고와 같은 규칙).
        /// </summary>
        string ApplyOutcome(DigOutcomeRow row, CharacterUnit digger)
        {
            switch (row.outcomeType)
            {
                case "dig_energy":
                case "dig_energy_big":
                    Resource.ResourceManager.Instance?.AddEnergy(row.value01);
                    return $"에너지 +{row.value01}";

                case "dig_heal":
                {
                    if (digger == null || !digger.IsAlive) return "";
                    int amount = Mathf.Max(1, Mathf.RoundToInt(digger.MaxHp * row.value01 * 0.01f));
                    digger.Heal(amount);
                    return $"체력 +{row.value01}%";
                }

                case "dig_hurt":
                {
                    if (digger == null || !digger.IsAlive) return "";
                    int amount = Mathf.Max(1, Mathf.RoundToInt(digger.MaxHp * row.value01 * 0.01f));
                    // ⚠ 표: *"이 피해로 죽지 않습니다"* — 체력 1 을 남긴다(이벤트 보상과 같은 규칙).
                    int safe = Mathf.Min(amount, Mathf.Max(0, digger.CurrentHp - 1));
                    if (safe > 0) digger.ApplyDamage(safe);
                    return $"체력 −{row.value01}%";
                }

                case "dig_erosion_up":
                case "dig_erosion_down":
                {
                    var er = digger != null ? CharacterErosion.Of(digger) : null;
                    if (er == null) return "";
                    bool up = row.outcomeType == "dig_erosion_up";
                    er.AddErosion(up ? row.value01 : -row.value01);
                    return up ? $"침식 +{row.value01}" : $"침식 −{row.value01}";
                }

                case "dig_nothing":
                    return "";

                case "dig_relic_common": return GrantRelic(RelicGrade.Common, false);
                case "dig_relic_rare":   return GrantRelic(RelicGrade.Rare, false);
                case "dig_relic_epic":   return GrantRelic(RelicGrade.Epic, true);
            }
            return "";
        }

        string GrantRelic(RelicGrade grade, bool digOnly)
        {
            RelicDefinitionSO relic = RelicRegistry.RollGrade(grade, digOnly);
            if (relic == null) return "";
            RelicInventory.Instance?.Grant(relic);
            return $"유물 「{relic.DisplayName}」 ({RelicDefinitionSO.NameOf(grade)})";
        }

        // ==================================================================
        // 세이브
        // ==================================================================

        /// <summary>세이브용 — (칸 x, 칸 y, 발견 여부, 진행도 ×100) 로 접는다.</summary>
        public List<Vector4> Capture()
        {
            var list = new List<Vector4>(_sites.Count);
            for (int i = 0; i < _sites.Count; i++)
            {
                DigSite s = _sites[i];
                list.Add(new Vector4(s.Cell.x, s.Cell.y,
                                     (s.Revealed ? 1 : 0) + (s.Ordered ? 2 : 0),
                                     s.Progress));
            }
            return list;
        }

        /// <summary>이어하기 — 저장된 자리를 되살린다. 없으면 새로 고른다.</summary>
        public void Restore(List<Vector4> saved)
        {
            if (saved == null || saved.Count == 0)
            {
                if (_sites.Count == 0) PickSites();
                return;
            }

            _sites.Clear();
            if (_map == null) _map = FindAnyObjectByType<MapGenerator>();
            for (int i = 0; i < saved.Count; i++)
            {
                Vector4 v = saved[i];
                var cell = new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), 0);
                int flags = Mathf.RoundToInt(v.z);
                _sites.Add(new DigSite
                {
                    Cell = cell,
                    Center = _map != null ? _map.CellCenterWorld(cell) : Vector3.zero,
                    Revealed = (flags & 1) != 0,
                    Ordered = (flags & 2) != 0,
                    Progress = v.w,
                });
            }
            if (logChanges) Debug.Log($"[유물] 발굴 칸 {_sites.Count}개 복원", this);
        }
    }
}
