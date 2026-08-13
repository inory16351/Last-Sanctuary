using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LastSanctuary.CameraControl;
using LastSanctuary.Combat;
using LastSanctuary.Fog;
using LastSanctuary.Map;
using LastSanctuary.Units;
using LastSanctuary.Wave;

namespace LastSanctuary.UI
{
    /// <summary>
    /// 우하단 미니맵. 맵 한 칸 = 텍스처 한 픽셀로 그린다 —
    /// <see cref="FogOfWarService"/> 가 이미 쓰는 방식이라(진행상황 12절) 맵이 커져도 비용이 일정하다.
    ///
    /// 그리는 순서(뒤에 그린 것이 위에 온다):
    ///   1. 지형 — 통행 가능/벽. 맵이 바뀌지 않는 한 한 번만 굽고 캐시한다
    ///   2. 안개 — 미탐사는 거의 검정, 탐사됐지만 시야 밖이면 어둡게
    ///   3. 넥서스 / 캐릭터 — 안개와 무관하게 항상 보인다(아군이므로 위치를 안다)
    ///   4. 웨이브 소환 경보 — 아래 설명
    ///
    /// <b>소환 경보</b>: 웨이브 몬스터가 소환되면 <b>소환 지점만</b> 전장의 안개를 무시하고
    /// 빨간 원으로 점멸한다. "어디서 오는지"는 알려주되 몬스터의 실제 위치나 진군 경로는
    /// 노출하지 않는다 — 안개의 의미를 지키면서 대비할 시간을 주기 위한 절충이다.
    /// 점멸은 <b>웨이브 타이머가 돌기 시작하면</b>(= 첫 전투로 <see cref="WavePhase.Battle"/> 진입)
    /// 멈춘다. 그 시점부터는 이미 교전 중이라 경보가 의미 없다.
    ///
    /// <b>소환 지점은 <see cref="MonsterSpawner.CurrentPortals"/> 를 그대로 읽는다</b>(27-6절에서
    /// 추가된 실제 포탈 목록). 예전에는 이 API 가 없어서 "소환 직후 살아있는 몬스터 위치를
    /// 뭉쳐서" 역산했는데, <c>MonsterSpawner.SpawnWave()</c> → <c>StartCoroutine(SpawnRoutine)</c>
    /// 은 첫 <c>yield</c> 전까지(= 첫 몬스터 한 마리를 스폰할 때까지)만 동기 실행되므로, 이
    /// <see cref="HandleWaveSpawned"/> 콜백이 불리는 시점엔 <b>포탈이 1~4개여도 몬스터는 항상
    /// 딱 한 마리만 나와 있었다</b> — 그래서 포탈이 여러 개인 웨이브에서도 경보가 하나만 뜨는
    /// 버그가 있었다(유저 리포트로 발견). <c>BuildWavePortals()</c> 는 스폰 코루틴의 첫 줄이라
    /// 몬스터를 하나도 안 스폰한 시점에 이미 전체 포탈이 다 정해져 있으므로, 이 값을 직접 읽으면
    /// 개수·정확한 위치 둘 다 항상 맞는다(살아있는 몬스터가 이동해서 위치가 달라지는 문제도 없다).
    ///
    /// ⚠️ 지형은 <see cref="MapGenerator.Walkable"/> 이 아니라 <see cref="MapGenerator.IsCellBlocked"/>
    /// (장애물 타일맵 기준)로 굽는다 — 자세한 이유는 <see cref="EnsureTexture"/> 참조.
    /// 처음 구현에서 <c>Walkable</c> 을 썼다가 플레이 모드에서 미니맵이 계속 텅 비는 문제가 있었다.
    /// </summary>
    public class MinimapPanel : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [Header("하이라키 연결")]
        [Tooltip("미니맵 텍스처를 그릴 RawImage")]
        [SerializeField] RawImage view;

        [Header("갱신")]
        [Tooltip("안개·유닛을 다시 그리는 주기(초). 맵 전체를 훑으므로 너무 짧게 두지 말 것")]
        [Min(0.05f)] [SerializeField] float refreshInterval = 0.25f;

        [Header("표시 크기 (맵 타일 단위)")]
        [Tooltip("캐릭터 점의 반지름")]
        [Min(0)] [SerializeField] int characterDotRadius = 2;

        [Tooltip("넥서스 표식의 반지름")]
        [Min(0)] [SerializeField] int nexusDotRadius = 3;

        [Header("소환 경보")]
        [Tooltip("소환 지점에 그릴 빨간 원의 반지름(타일)")]
        [Min(1)] [SerializeField] int spawnAlertRadius = 7;

        [Tooltip("원 테두리 두께(타일)")]
        [Min(1)] [SerializeField] int spawnAlertThickness = 2;

        [Tooltip("점멸 주기(초). 이 시간의 절반은 켜지고 절반은 꺼진다")]
        [Min(0.1f)] [SerializeField] float spawnAlertBlinkPeriod = 0.8f;

        [Header("클릭 이동 (2026-08-13 신설)")]
        [Tooltip("지도를 클릭하면 그 지점으로 카메라가 간다. 누른 채로 끌면 계속 따라간다 — " +
                 "롤(LoL) 미니맵과 같은 조작이다")]
        [SerializeField] bool clickToMoveCamera = true;

        [Tooltip("클릭 이동 시 카메라를 <b>즉시</b> 옮긴다(SnapTo). 끄면 부드럽게 미끄러져 간다" +
                 "(FocusOn) — 먼 거리를 클릭했을 때 어디로 가는지 눈으로 따라갈 수 있다")]
        [SerializeField] bool snapCamera = true;

        MapGenerator _map;
        FogOfWarService _fog;
        WaveManager _wave;
        MonsterSpawner _spawner;

        Texture2D _texture;
        Color32[] _terrain;    // 지형만 구운 것 (캐시)
        Color32[] _pixels;     // 매 갱신마다 지형을 복사해 그 위에 덧그린다
        Vector2Int _size;
        Vector2Int _origin;

        readonly List<Vector3> _spawnPoints = new List<Vector3>();
        bool _alertActive;
        float _nextRefresh;

        CameraRigController _camera;
        Canvas _canvas;

        void Start()
        {
            _map = FindAnyObjectByType<MapGenerator>();
            _fog = FindAnyObjectByType<FogOfWarService>();
            _wave = FindAnyObjectByType<WaveManager>();
            _spawner = FindAnyObjectByType<MonsterSpawner>();

            // MCP 로는 씬 오브젝트 참조를 인스펙터에 넣을 수 없어서(진행상황 8절 4번),
            // 비어 있으면 이름으로 찾는다.
            if (view == null)
            {
                Transform child = transform.Find("View");
                if (child != null) view = child.GetComponent<RawImage>();
            }

            if (view == null)
            {
                Debug.LogError("[Minimap] View(RawImage)가 연결되지 않았습니다. " +
                               "HUD_Minimap/View 를 인스펙터에 넣어주세요.", this);
                enabled = false;
                return;
            }

            if (_wave != null)
            {
                _wave.OnWaveSpawned += HandleWaveSpawned;
                _wave.OnPhaseChanged += HandlePhaseChanged;
            }

            _camera = FindAnyObjectByType<CameraRigController>();
            _canvas = GetComponentInParent<Canvas>();

            // 클릭을 받으려면 지도 이미지가 레이캐스트를 먹어야 한다. 씬 값이 꺼져 있어도
            // (기본 RawImage 는 켜져 있지만 미니맵은 "클릭을 가로막지 않게" 꺼둘 수 있는 자리다)
            // 클릭 이동을 켠 이상 여기서 맞춰준다 — 값 보정이라 §10 H-1 위반이 아니다.
            if (clickToMoveCamera && view != null) view.raycastTarget = true;
        }

        void OnDestroy()
        {
            if (_wave == null) return;
            _wave.OnWaveSpawned -= HandleWaveSpawned;
            _wave.OnPhaseChanged -= HandlePhaseChanged;
        }

        void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;

            if (!EnsureTexture()) return;

            System.Array.Copy(_terrain, _pixels, _pixels.Length);
            DrawFog();
            DrawUnits();
            DrawSpawnAlerts();

            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }

        // ------------------------------------------------------------------
        // 지형 — 맵 크기가 바뀔 때만 다시 굽는다
        // ------------------------------------------------------------------

        /// <summary>
        /// ⚠️ <see cref="MapGenerator.Walkable"/> / <c>MapSize</c> / <c>Origin</c> 을 쓰지 않는다.
        /// 이 셋은 <c>Generate()</c> 안에서만 채워지는 <b>런타임 전용 캐시</b>라, 씬의
        /// <c>generateOnAwake</c> 가 꺼져 있으면(현재 씬 값 — 에디터에서 미리 생성해두고 저장하는
        /// 방식이라 꺼둔 것) 플레이 모드에서 <c>Generate()</c> 가 한 번도 안 불려 계속 <c>null</c>
        /// 이다. 그 결과 이 메서드가 항상 실패해 미니맵이 텅 빈 채로 남아있었다(실측 확인).
        ///
        /// 대신 <b>씬에 직렬화되어 항상 유효한 값</b>을 쓴다 — 크기/원점은
        /// <see cref="MapGenerator.Config"/>(SO 에셋, 항상 값이 있음)에서, 통행 가능 여부는
        /// <see cref="MapGenerator.IsCellBlocked"/>(장애물 타일맵 기준, 진행상황 8절이 이미
        /// 같은 이유로 권장하는 방식)에서 얻는다. 둘 다 <c>Generate()</c> 호출 여부와 무관하다.
        /// </summary>
        bool EnsureTexture()
        {
            if (_map == null) _map = FindAnyObjectByType<MapGenerator>();
            var config = _map != null ? _map.Config : null;
            if (config == null) return false;

            Vector2Int size = config.MapSize;
            if (size.x <= 0 || size.y <= 0) return false;

            if (_texture != null && _size == size) return true;

            _size = size;
            _origin = config.Origin;

            _texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false)
            {
                name = "MinimapTexture",
                filterMode = FilterMode.Point,     // 도트 느낌 유지 + 픽셀 경계가 뭉개지지 않게
                wrapMode = TextureWrapMode.Clamp,
            };

            _terrain = new Color32[size.x * size.y];
            _pixels = new Color32[size.x * size.y];

            for (int y = 0; y < size.y; y++)
            {
                int row = y * size.x;
                int cellY = y + _origin.y;
                for (int x = 0; x < size.x; x++)
                {
                    bool blocked = _map.IsCellBlocked(new Vector3Int(x + _origin.x, cellY, 0));
                    _terrain[row + x] = blocked ? HudTheme.MapWall : HudTheme.MapFloor;
                }
            }

            view.texture = _texture;
            return true;
        }

        // ------------------------------------------------------------------
        // 안개
        // ------------------------------------------------------------------

        void DrawFog()
        {
            if (_fog == null) _fog = FindAnyObjectByType<FogOfWarService>();
            if (_fog == null || !_fog.IsReady) return;

            int w = _size.x, h = _size.y;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                int cellY = y + _origin.y;

                for (int x = 0; x < w; x++)
                {
                    var cell = new Vector3Int(x + _origin.x, cellY, 0);

                    if (!_fog.IsExplored(cell))
                    {
                        _pixels[row + x] = HudTheme.MapUnexplored;
                        continue;
                    }
                    if (_fog.IsVisible(cell)) continue;      // 지금 시야 안 — 지형 원색 그대로(가장 밝음)

                    // 탐사는 됐지만 지금 시야 밖(캐릭터가 지나간 곳) — 미탐사 색과 지형 원색
                    // 사이를 보간한다. 곱연산으로 한 번 더 죽이면(예전 방식) 이미 어두운 지형
                    // 색이 거의 검정에 수렴해 미탐사와 구별이 안 됐다 — 보간이면 항상 미탐사보다
                    // 확실히 밝은 중간 밝기가 보장된다.
                    _pixels[row + x] = LerpColor32(HudTheme.MapUnexplored, _pixels[row + x],
                                                    HudTheme.MapExploredBrightness);
                }
            }
        }

        // ------------------------------------------------------------------
        // 유닛 — 넥서스와 캐릭터는 안개와 무관하게 항상 표시한다
        // ------------------------------------------------------------------

        void DrawUnits()
        {
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                DamageableUnit u = all[i];
                if (u == null || !u.IsAlive) continue;

                if (u.Kind == UnitKind.Nexus && u.Faction == Faction.Angel)
                {
                    DrawDisc(WorldToLocal(u.transform.position), nexusDotRadius, HudTheme.MapNexus);
                    continue;
                }

                if (u is CharacterUnit)
                    DrawDisc(WorldToLocal(u.transform.position), characterDotRadius, HudTheme.MapAlly);
            }
        }

        // ------------------------------------------------------------------
        // 소환 경보
        // ------------------------------------------------------------------

        /// <summary>이번 웨이브에 실제로 열린 포탈 전부를 그대로 경보 지점으로 삼는다.</summary>
        void HandleWaveSpawned(int wave)
        {
            _spawnPoints.Clear();
            _alertActive = true;

            if (_spawner == null) _spawner = FindAnyObjectByType<MonsterSpawner>();
            if (_spawner == null || _map == null) return;

            IReadOnlyList<Vector3Int> portals = _spawner.CurrentPortals;
            for (int i = 0; i < portals.Count; i++)
                _spawnPoints.Add(_map.CellCenterWorld(portals[i]));

            if (_spawnPoints.Count > 0)
                HudLog.Add($"웨이브 {wave} 소환 — 미니맵 {_spawnPoints.Count}곳 경보", HudLogKind.Danger);
        }

        /// <summary>웨이브 타이머가 돌기 시작하면(전투 진입) 경보를 끈다.</summary>
        void HandlePhaseChanged(WavePhase phase)
        {
            if (phase == WavePhase.Marching) return;   // 진군 중에는 계속 점멸

            _alertActive = false;
            _spawnPoints.Clear();
        }

        void DrawSpawnAlerts()
        {
            if (!_alertActive || _spawnPoints.Count == 0) return;

            // 주기의 앞 절반만 그린다 = 점멸.
            float phase = Mathf.Repeat(Time.unscaledTime, spawnAlertBlinkPeriod);
            if (phase > spawnAlertBlinkPeriod * 0.5f) return;

            for (int i = 0; i < _spawnPoints.Count; i++)
                DrawRing(WorldToLocal(_spawnPoints[i]), spawnAlertRadius, spawnAlertThickness,
                         HudTheme.MapEnemy);
        }

        // ------------------------------------------------------------------
        // 클릭 이동 — 롤(LoL) 미니맵 방식 (유저 지시 2026-08-13)
        //
        // 지도를 누르면 그 지점으로 카메라가 가고, 누른 채로 끌면 계속 따라간다.
        //
        // <b>왜 이 컴포넌트가 직접 받는가</b> — 클릭을 받는 것은 자식인 <c>View</c>(RawImage)
        // 인데, 유니티 이벤트 시스템은 핸들러를 <b>부모로 거슬러 올라가며</b> 찾는다
        // (<c>ExecuteEvents.ExecuteHierarchy</c>). 그래서 <c>View</c> 에 스크립트를 따로 붙이지
        // 않아도 여기서 받을 수 있다 — 씬에 오브젝트·컴포넌트를 하나도 더 만들지 않는다.
        //
        // <b>카메라 드래그와 충돌하지 않는다</b> — <c>CameraRigController</c> 는
        // <c>ignoreDragOverUI</c> 로 UI 위에서 시작한 드래그를 무시하고,
        // <c>UnitSelector</c> 도 <c>IsPointerOverGameObject()</c> 로 UI 클릭을 거른다
        // (준수사항 U-D8). 미니맵은 UI 라서 두 시스템 모두 자동으로 비켜난다.
        // ------------------------------------------------------------------

        public void OnPointerDown(PointerEventData eventData) => MoveCameraTo(eventData);

        public void OnDrag(PointerEventData eventData) => MoveCameraTo(eventData);

        void MoveCameraTo(PointerEventData eventData)
        {
            if (!clickToMoveCamera || _camera == null || view == null || _map == null) return;
            if (_texture == null) return;                     // 아직 지형을 안 구웠다
            if (eventData.button != PointerEventData.InputButton.Left) return;

            if (!TryScreenToWorld(eventData, out Vector3 world)) return;

            if (snapCamera) _camera.SnapTo(world);
            else _camera.FocusOn(world);
        }

        /// <summary>
        /// 화면 좌표 → 월드 좌표. 지도 밖을 눌렀으면 false.
        ///
        /// 지도는 <b>맵 한 칸 = 텍스처 한 픽셀</b>로 그려져 있으므로, 이미지 안에서의
        /// 정규화 좌표(0~1)가 곧 맵 안에서의 비율이다 — 텍스처 크기·화면 크기·줌과 무관하게
        /// 항상 맞는다.
        ///
        /// <paramref name="eventData"/> 의 <c>pressEventCamera</c> 를 쓴다: 캔버스가
        /// Screen Space - Overlay 면 null 이고, Camera 모드면 그 카메라다. 어느 쪽이든
        /// <see cref="RectTransformUtility"/> 가 알아서 처리한다.
        /// </summary>
        bool TryScreenToWorld(PointerEventData eventData, out Vector3 world)
        {
            world = default;

            RectTransform rt = view.rectTransform;
            Camera cam = eventData.pressEventCamera;
            if (cam == null && _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = _canvas.worldCamera;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rt, eventData.position, cam, out Vector2 local))
                return false;

            Rect r = rt.rect;
            float u = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
            float v = Mathf.InverseLerp(r.yMin, r.yMax, local.y);
            if (u < 0f || u > 1f || v < 0f || v > 1f) return false;

            // 정규화 → 셀. 마지막 칸을 누른 경우 반올림으로 맵 밖을 가리키지 않게 잘라둔다.
            var cell = new Vector3Int(
                _origin.x + Mathf.Clamp(Mathf.FloorToInt(u * _size.x), 0, _size.x - 1),
                _origin.y + Mathf.Clamp(Mathf.FloorToInt(v * _size.y), 0, _size.y - 1),
                0);

            world = _map.CellCenterWorld(cell);
            return true;
        }

        // ------------------------------------------------------------------
        // 픽셀 그리기
        // ------------------------------------------------------------------

        Vector2Int WorldToLocal(Vector3 world)
        {
            Vector3Int cell = _map.WorldToCell(world);
            return new Vector2Int(cell.x - _origin.x, cell.y - _origin.y);
        }

        /// <summary><c>Color32</c> 에는 내장 Lerp 가 없어서(Color 전용) 채널별로 직접 보간한다.</summary>
        static Color32 LerpColor32(Color32 a, Color32 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.r, b.r, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.g, b.g, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.b, b.b, t)),
                255);
        }

        void SetPixel(int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= _size.x || y >= _size.y) return;
            _pixels[y * _size.x + x] = color;
        }

        /// <summary>속이 찬 원.</summary>
        void DrawDisc(Vector2Int center, int radius, Color32 color)
        {
            if (radius <= 0) { SetPixel(center.x, center.y, color); return; }

            int rSqr = radius * radius;
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                    if (dx * dx + dy * dy <= rSqr)
                        SetPixel(center.x + dx, center.y + dy, color);
        }

        /// <summary>테두리만 있는 원. 소환 경보에 쓴다 — 속을 채우면 지형이 안 보인다.</summary>
        void DrawRing(Vector2Int center, int radius, int thickness, Color32 color)
        {
            int outer = radius * radius;
            int innerR = Mathf.Max(0, radius - thickness);
            int inner = innerR * innerR;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int d = dx * dx + dy * dy;
                    if (d > outer || d < inner) continue;
                    SetPixel(center.x + dx, center.y + dy, color);
                }
            }
        }
    }
}
