using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace LastSanctuary.Map
{
    /// <summary>
    /// 고정 크기 아레나를 청크 단위로 절차적 생성한다.
    ///
    /// 파이프라인
    ///   1. 청크별 바이옴 추첨 (시드 결정적)
    ///   2. 바닥 채우기
    ///   3. 장애물 마스크 (펄린 노이즈 → 셀룰러 오토마타 스무딩)
    ///   4. 넥서스 반경 / 경계 벽 / 스폰 게이트 강제 반영
    ///   5. 스폰 게이트 → 넥서스 통로 굴착
    ///   6. 연결성 검사(BFS) — 도달 불가 지역은 벽으로 메움
    ///   7. 타일맵에 일괄 기록 (SetTilesBlock)
    ///
    /// 6번이 중요하다. 몬스터는 플로우 필드로 넥서스를 향해 이동하므로
    /// 고립된 구역이 남으면 스폰 지점이 도달 불가가 되어 웨이브가 진행되지 않는다.
    /// </summary>
    public class MapGenerator : MonoBehaviour
    {
        [Header("생성 설정")]
        [SerializeField] MapGenerationConfigSO config;

        [Header("대상 타일맵")]
        [SerializeField] Tilemap groundTilemap;
        [SerializeField] Tilemap decoTilemap;
        [SerializeField] Tilemap obstacleTilemap;

        [Header("경계")]
        [Tooltip("MapBounds 의 Collider2D. 카메라 Confiner2D 와 CameraRigController 가 이것을 경계로 쓴다")]
        [SerializeField] Collider2D boundsShape;

        [Tooltip("생성할 때마다 경계 콜라이더를 맵 크기에 자동으로 맞춘다. " +
                 "끄면 맵 크기를 바꿔도 카메라 경계는 그대로 남는다")]
        [SerializeField] bool autoResizeBounds = true;

        /// <summary>에디터에서 경계 동기화에 사용.</summary>
        public Collider2D BoundsShape => boundsShape;

        /// <summary>자동 리사이즈 설정. 에디터 인스펙터 표시용.</summary>
        public bool AutoResizeBounds => autoResizeBounds;

        [Header("런타임")]
        [Tooltip("체크하면 게임 시작 시 자동 생성. 끄면 에디터 버튼으로만 생성")]
        [SerializeField] bool generateOnAwake = false;

        // 생성 결과 — 다른 시스템(플로우 필드, 스폰 배치)이 참조한다.
        public MapGenerationConfigSO Config => config;
        public bool[] Walkable { get; private set; }
        public Vector2Int MapSize { get; private set; }
        public Vector2Int Origin { get; private set; }

        /// <summary>스폰 게이트의 로컬 좌표 4개 (하, 상, 좌, 우 순).</summary>
        public List<Vector2Int> SpawnGates { get; } = new List<Vector2Int>();

        void Awake()
        {
            if (generateOnAwake) Generate(config != null ? config.seed : 0);
        }

        // ------------------------------------------------------------------
        // 공개 API
        // ------------------------------------------------------------------

        /// <summary>지정 시드로 맵을 생성한다.</summary>
        public void Generate(int seed)
        {
            if (!Validate()) return;

            MapSize = config.MapSize;
            Origin  = config.Origin;
            int w = MapSize.x, h = MapSize.y;
            Vector2Int cc = config.ChunkCount;

            // 1. 청크별 바이옴
            ChunkBiomeSO[] chunkBiome = PickChunkBiomes(seed);

            // 셀 하나가 속한 바이옴을 빠르게 얻기 위한 헬퍼.
            // Tiles 모드에서 맵이 청크 배수보다 작으면(= 잘린 경우) 마지막 청크는
            // 일부만 사용되며, Clamp 가 인덱스 초과를 막는다.
            int csx = Mathf.Max(1, config.chunkSize.x);
            int csy = Mathf.Max(1, config.chunkSize.y);
            ChunkBiomeSO BiomeAt(int x, int y)
            {
                int cx = Mathf.Clamp(x / csx, 0, cc.x - 1);
                int cy = Mathf.Clamp(y / csy, 0, cc.y - 1);
                return chunkBiome[cx + cy * cc.x];
            }

            var rng = new System.Random(seed);

            // 2~6. 마스크 계산
            bool[] isWall = BuildObstacleMask(seed, w, h, BiomeAt);
            ApplyNexusClear(isWall, w, h);
            ApplyBorderAndGates(isWall, w, h);
            CarveCorridors(isWall, w, h, seed);
            SealUnreachable(isWall, w, h);

            // 7. 타일 배열 구성 후 일괄 기록
            var ground   = new TileBase[w * h];
            var deco     = new TileBase[w * h];
            var obstacle = new TileBase[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = x + y * w;
                    ChunkBiomeSO b = BiomeAt(x, y);
                    if (b == null) continue;

                    ground[i] = PickWeighted(b.floorTiles, rng);

                    if (isWall[i])
                    {
                        obstacle[i] = PickWeighted(b.obstacleTiles, rng);
                    }
                    else if (b.decoTiles != null && b.decoTiles.Length > 0
                             && rng.NextDouble() < b.decoChance)
                    {
                        deco[i] = PickWeighted(b.decoTiles, rng);
                    }
                }
            }

            var bounds = new BoundsInt(Origin.x, Origin.y, 0, w, h, 1);
            WriteBlock(groundTilemap,   bounds, ground);
            WriteBlock(decoTilemap,     bounds, deco);
            WriteBlock(obstacleTilemap, bounds, obstacle);

            if (autoResizeBounds) SyncBoundsToMap();

            // 다른 시스템이 쓸 통행 가능 맵 (벽 = false)
            Walkable = new bool[w * h];
            for (int i = 0; i < isWall.Length; i++) Walkable[i] = !isWall[i];

            Vector2Int cropped = config.CroppedAmount;
            string cropInfo = config.IsCropped
                ? $" · 청크 영역 {config.CoveredSize.x}x{config.CoveredSize.y} 에서 " +
                  $"{cropped.x}x{cropped.y} 잘라냄"
                : "";

            Debug.Log($"[MapGenerator] {w}x{h} 생성 완료 · seed={seed} · " +
                      $"청크 {cc.x}x{cc.y} (청크당 {config.chunkSize.x}x{config.chunkSize.y})" +
                      $"{cropInfo} · 통행 가능 {CountTrue(Walkable)}/{w * h}");
        }

        /// <summary>세 타일맵을 모두 비운다.</summary>
        public void ClearAll()
        {
            if (groundTilemap   != null) groundTilemap.ClearAllTiles();
            if (decoTilemap     != null) decoTilemap.ClearAllTiles();
            if (obstacleTilemap != null) obstacleTilemap.ClearAllTiles();
            Walkable = null;
            SpawnGates.Clear();
        }

        /// <summary>로컬 그리드 좌표 → 월드 타일 좌표.</summary>
        public Vector3Int LocalToCell(Vector2Int local) =>
            new Vector3Int(local.x + Origin.x, local.y + Origin.y, 0);

        // ------------------------------------------------------------------
        // 다른 시스템이 맵을 조회하는 API
        // Walkable[] 은 런타임 배열이라 씬을 다시 열면 사라지므로,
        // 위치 판정은 직렬화되어 남아 있는 장애물 타일맵을 기준으로 한다.
        // ------------------------------------------------------------------

        public Tilemap GroundTilemap => groundTilemap;
        public Tilemap ObstacleTilemap => obstacleTilemap;

        /// <summary>맵 중앙 셀. Origin = -MapSize/2 이므로 항상 (0,0) 이다.</summary>
        public Vector3Int CenterCell => Vector3Int.zero;

        /// <summary>셀 중심의 월드 좌표.</summary>
        public Vector3 CellCenterWorld(Vector3Int cell) =>
            groundTilemap != null
                ? groundTilemap.GetCellCenterWorld(cell)
                : new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

        /// <summary>해당 셀이 장애물인지.</summary>
        public bool IsCellBlocked(Vector3Int cell) =>
            obstacleTilemap != null && obstacleTilemap.HasTile(cell);

        /// <summary>월드 좌표 → 셀. 이동 충돌 판정처럼 위치 기반 조회가 필요한 곳에서 쓴다.</summary>
        public Vector3Int WorldToCell(Vector3 world) =>
            groundTilemap != null
                ? groundTilemap.WorldToCell(world)
                : new Vector3Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y), 0);

        /// <summary>해당 셀이 맵 범위 안인지. config 기준이라 생성 전에도 동작한다.</summary>
        public bool IsCellInsideMap(Vector3Int cell)
        {
            if (config == null) return false;
            Vector2Int size = config.MapSize;
            Vector2Int org = config.Origin;
            return cell.x >= org.x && cell.x < org.x + size.x
                && cell.y >= org.y && cell.y < org.y + size.y;
        }

        /// <summary>배치 가능한 셀인지 (맵 안 + 장애물 아님).</summary>
        public bool IsCellPlaceable(Vector3Int cell) =>
            IsCellInsideMap(cell) && !IsCellBlocked(cell);

        /// <summary>
        /// 지정 셀에서 바깥으로 링을 넓혀가며 배치 가능한 셀을 찾는다.
        /// exclude 로 이미 사용한 칸을 제외할 수 있다.
        /// </summary>
        public bool TryFindPlaceableNear(Vector3Int center, int maxRadius,
                                         System.Predicate<Vector3Int> exclude,
                                         out Vector3Int found)
        {
            for (int r = 0; r <= maxRadius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        // 링의 테두리만 검사 (r=0 은 중심 한 칸)
                        if (r > 0 && Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;

                        var c = new Vector3Int(center.x + dx, center.y + dy, 0);
                        if (!IsCellPlaceable(c)) continue;
                        if (exclude != null && exclude(c)) continue;

                        found = c;
                        return true;
                    }
                }
            }
            found = center;
            return false;
        }

        /// <summary>
        /// 경계 콜라이더를 현재 맵 크기에 맞춘다.
        ///
        /// 트랜스폼 스케일로 늘리는 대신 콜라이더의 점을 직접 지정한다.
        /// 스케일 방식은 콜라이더 원본 크기가 1x1 이라는 가정에 의존해서,
        /// 나중에 모양이 바뀌면 조용히 어긋난다.
        ///
        /// Confiner2D 는 경계 모양을 베이크해 캐시하므로, 모양을 바꾼 뒤
        /// InvalidateBoundingShapeCache() 를 부르지 않으면 낡은 경계를 계속 쓴다.
        /// </summary>
        public void SyncBoundsToMap()
        {
            if (boundsShape == null || config == null) return;

            Vector2Int size = config.MapSize;
            Transform t = boundsShape.transform;

            // 점을 직접 지정하므로 스케일은 1 로 되돌리고 원점에 맞춘다.
            t.localScale = Vector3.one;
            t.position = new Vector3(0f, 0f, t.position.z);

            float hw = size.x * 0.5f;
            float hh = size.y * 0.5f;

            switch (boundsShape)
            {
                case PolygonCollider2D poly:
                    poly.pathCount = 1;
                    poly.SetPath(0, new[]
                    {
                        new Vector2(-hw, -hh), new Vector2( hw, -hh),
                        new Vector2( hw,  hh), new Vector2(-hw,  hh),
                    });
                    break;

                case BoxCollider2D box:
                    box.offset = Vector2.zero;
                    box.size = new Vector2(size.x, size.y);
                    break;

                default:
                    // 지원하지 않는 콜라이더는 스케일로 근사한다.
                    Debug.LogWarning($"[MapGenerator] {boundsShape.GetType().Name} 는 점 지정을 " +
                                     "지원하지 않아 트랜스폼 스케일로 맞춥니다. " +
                                     "PolygonCollider2D 또는 BoxCollider2D 를 권장합니다.", boundsShape);
                    t.localScale = new Vector3(size.x, size.y, 1f);
                    break;
            }

            InvalidateConfinerCaches();
        }

        /// <summary>
        /// 이 경계 콜라이더를 쓰는 Cinemachine Confiner2D 의 베이크 캐시를 무효화한다.
        /// 참조를 직접 들고 있으면 맵 시스템이 카메라에 결합되므로, 생성 시점에만
        /// 한 번 훑어서 찾는다.
        /// </summary>
        void InvalidateConfinerCaches()
        {
            var confiners = FindObjectsByType<CinemachineConfiner2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int hit = 0;
            foreach (var c in confiners)
            {
                if (c.BoundingShape2D != boundsShape) continue;
                c.InvalidateBoundingShapeCache();
                hit++;
            }

            if (hit == 0 && confiners.Length > 0)
                Debug.LogWarning("[MapGenerator] Confiner2D 를 찾았지만 Bounding Shape 2D 가 " +
                                 $"{boundsShape.name} 를 가리키지 않습니다. 카메라 경계가 " +
                                 "맵과 다르게 동작할 수 있습니다.", this);
        }

        // ------------------------------------------------------------------
        // 단계별 구현
        // ------------------------------------------------------------------

        bool Validate()
        {
            if (config == null)
            {
                Debug.LogError("[MapGenerator] Config 가 비어 있습니다.", this);
                return false;
            }
            if (config.biomes == null || config.biomes.Length == 0)
            {
                Debug.LogError("[MapGenerator] Config 에 바이옴이 하나도 없습니다.", config);
                return false;
            }
            if (groundTilemap == null || decoTilemap == null || obstacleTilemap == null)
            {
                Debug.LogError("[MapGenerator] 타일맵 3개를 모두 연결해야 합니다.", this);
                return false;
            }
            return true;
        }

        /// <summary>청크마다 바이옴을 가중 랜덤으로 고른다. 시드가 같으면 결과가 같다.</summary>
        ChunkBiomeSO[] PickChunkBiomes(int seed)
        {
            Vector2Int cc = config.ChunkCount;
            int cw = cc.x, ch = cc.y;
            var result = new ChunkBiomeSO[cw * ch];

            for (int cy = 0; cy < ch; cy++)
            {
                for (int cx = 0; cx < cw; cx++)
                {
                    // 청크별 독립 RNG — 청크 순서를 바꿔도 결과가 흔들리지 않는다.
                    var r = new System.Random(Hash(seed, cx, cy));
                    result[cx + cy * cw] = PickWeightedBiome(config.biomes, r);
                }
            }
            return result;
        }

        /// <summary>
        /// 펄린 노이즈로 벽 후보를 만들고 셀룰러 오토마타로 덩어리화.
        ///
        /// 임계값은 고정값이 아니라 바이옴별 "백분위"로 구한다.
        /// Mathf.PerlinNoise 는 값이 0.5 근처에 몰려 있어(실질 0.2~0.8) 고정 임계값을
        /// 쓰면 density 를 0.18 로 줘도 벽이 거의 생기지 않는다. 해당 바이옴 셀들의
        /// 노이즈 값을 정렬해 density 위치의 값을 임계값으로 삼으면
        /// obstacleDensity 가 "그 바이옴 면적 중 벽이 될 비율" 이라는 뜻이 된다.
        /// </summary>
        bool[] BuildObstacleMask(int seed, int w, int h,
                                 System.Func<int, int, ChunkBiomeSO> biomeAt)
        {
            var mask = new bool[w * h];

            // 시드에 따라 노이즈 샘플 위치를 옮겨 서로 다른 지형이 나오게 한다.
            var offRng = new System.Random(seed);
            float offX = (float)offRng.NextDouble() * 10000f;
            float offY = (float)offRng.NextDouble() * 10000f;

            // 1) 셀별 노이즈 값 계산 + 바이옴별로 인덱스 모으기
            var noise = new float[w * h];
            var cellsByBiome = new Dictionary<ChunkBiomeSO, List<int>>();
            int maxSmooth = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    ChunkBiomeSO b = biomeAt(x, y);
                    if (b == null) continue;

                    int i = x + y * w;
                    // 노이즈는 월드 좌표로 샘플 — 같은 바이옴 안에서 청크 경계가 튀지 않는다.
                    noise[i] = Mathf.PerlinNoise((x + offX) * b.noiseScale,
                                                 (y + offY) * b.noiseScale);

                    if (!cellsByBiome.TryGetValue(b, out var list))
                        cellsByBiome[b] = list = new List<int>();
                    list.Add(i);

                    if (b.smoothPasses > maxSmooth) maxSmooth = b.smoothPasses;
                }
            }

            // 2) 바이옴별 백분위 임계값으로 벽 후보 결정
            foreach (var kv in cellsByBiome)
            {
                ChunkBiomeSO b = kv.Key;
                List<int> cells = kv.Value;
                if (b.obstacleDensity <= 0f || cells.Count == 0) continue;

                var values = new float[cells.Count];
                for (int k = 0; k < cells.Count; k++) values[k] = noise[cells[k]];
                System.Array.Sort(values);

                int cut = Mathf.Clamp(Mathf.RoundToInt(b.obstacleDensity * (cells.Count - 1)),
                                      0, cells.Count - 1);
                float threshold = values[cut];

                for (int k = 0; k < cells.Count; k++)
                    if (noise[cells[k]] <= threshold) mask[cells[k]] = true;
            }

            // 3) 스무딩은 맵 전체에 적용 — 청크별로 하면 경계에 직선 흔적이 남는다.
            for (int pass = 0; pass < maxSmooth; pass++)
                mask = SmoothOnce(mask, w, h);

            return mask;
        }

        /// <summary>주변 8칸 중 벽이 5개 이상이면 벽, 3개 이하면 빈칸. 고전 셀룰러 오토마타.</summary>
        static bool[] SmoothOnce(bool[] src, int w, int h)
        {
            var dst = new bool[src.Length];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int wallCount = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx, ny = y + dy;
                            // 맵 밖은 벽으로 취급 — 외곽이 자연스럽게 닫힌다.
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) { wallCount++; continue; }
                            if (src[nx + ny * w]) wallCount++;
                        }
                    }

                    int i = x + y * w;
                    if (wallCount >= 5)      dst[i] = true;
                    else if (wallCount <= 3) dst[i] = false;
                    else                     dst[i] = src[i];
                }
            }
            return dst;
        }

        /// <summary>맵 중앙(넥서스)을 원형으로 비운다.</summary>
        void ApplyNexusClear(bool[] mask, int w, int h)
        {
            int cx = w / 2, cy = h / 2;
            int r = config.nexusClearRadius;
            if (r <= 0) return;

            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= r2) mask[x + y * w] = false;
                }
            }
        }

        /// <summary>외곽을 벽으로 두르고, 각 변 중앙에 스폰 통로를 뚫는다.</summary>
        void ApplyBorderAndGates(bool[] mask, int w, int h)
        {
            int t = config.borderWallThickness;

            if (t > 0)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (x < t || y < t || x >= w - t || y >= h - t)
                            mask[x + y * w] = true;
                    }
                }
            }

            SpawnGates.Clear();
            int half = config.spawnGateWidth / 2;
            if (config.spawnGateWidth <= 0) return;

            int mx = w / 2, my = h / 2;

            // 하 / 상 — 가로 방향으로 열린 통로
            for (int x = mx - half; x <= mx + half; x++)
            {
                if (x < 0 || x >= w) continue;
                for (int y = 0; y < Mathf.Max(t, 1); y++)         mask[x + y * w] = false;
                for (int y = h - Mathf.Max(t, 1); y < h; y++)     mask[x + y * w] = false;
            }
            // 좌 / 우
            for (int y = my - half; y <= my + half; y++)
            {
                if (y < 0 || y >= h) continue;
                for (int x = 0; x < Mathf.Max(t, 1); x++)         mask[x + y * w] = false;
                for (int x = w - Mathf.Max(t, 1); x < w; x++)     mask[x + y * w] = false;
            }

            SpawnGates.Add(new Vector2Int(mx, 0));
            SpawnGates.Add(new Vector2Int(mx, h - 1));
            SpawnGates.Add(new Vector2Int(0, my));
            SpawnGates.Add(new Vector2Int(w - 1, my));
        }

        /// <summary>
        /// 각 스폰 게이트에서 넥서스까지 통로를 굴착한다.
        /// 웨이브 몬스터의 진입 경로를 물리적으로 보장하는 단계.
        /// </summary>
        void CarveCorridors(bool[] mask, int w, int h, int seed)
        {
            var target = new Vector2Int(w / 2, h / 2);
            int radius = Mathf.Max(1, config.corridorWidth / 2);

            for (int g = 0; g < SpawnGates.Count; g++)
            {
                var rng = new System.Random(Hash(seed, 777, g));
                Vector2 pos = SpawnGates[g];

                // 안전 상한 — 무한 루프 방지
                int maxSteps = (w + h) * 3;

                for (int step = 0; step < maxSteps; step++)
                {
                    CarveDisc(mask, w, h, Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y), radius);

                    Vector2 dir = ((Vector2)target - pos);
                    if (dir.sqrMagnitude <= 1f) break;
                    dir.Normalize();

                    // 좌우 흔들림을 섞어 통로가 자연스럽게 휘게 한다.
                    if (config.corridorWobble > 0f)
                    {
                        var perp = new Vector2(-dir.y, dir.x);
                        float wob = (float)(rng.NextDouble() * 2.0 - 1.0) * config.corridorWobble;
                        dir = (dir + perp * wob).normalized;
                    }

                    pos += dir;
                    pos.x = Mathf.Clamp(pos.x, 0, w - 1);
                    pos.y = Mathf.Clamp(pos.y, 0, h - 1);
                }
            }
        }

        static void CarveDisc(bool[] mask, int w, int h, int cx, int cy, int r)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= r2) mask[x + y * w] = false;
                }
            }
        }

        /// <summary>
        /// 넥서스에서 BFS 로 도달 가능한 구역을 찾고, 나머지 빈칸은 벽으로 메운다.
        /// 결과적으로 통행 가능 영역이 항상 하나로 연결된다 → 플로우 필드가 안전해진다.
        /// </summary>
        void SealUnreachable(bool[] mask, int w, int h)
        {
            var reached = new bool[w * h];
            var queue = new Queue<int>();

            int start = (w / 2) + (h / 2) * w;
            if (mask[start])
            {
                // 넥서스 자리가 막혔으면 강제로 뚫는다 (정상적으로는 발생하지 않음)
                mask[start] = false;
            }
            reached[start] = true;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % w, y = i / w;

                // 4방향 — 대각 이동을 허용하지 않으므로 벽 사이로 새지 않는다.
                TryVisit(x + 1, y); TryVisit(x - 1, y);
                TryVisit(x, y + 1); TryVisit(x, y - 1);

                void TryVisit(int nx, int ny)
                {
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) return;
                    int ni = nx + ny * w;
                    if (reached[ni] || mask[ni]) return;
                    reached[ni] = true;
                    queue.Enqueue(ni);
                }
            }

            int sealed_ = 0;
            for (int i = 0; i < mask.Length; i++)
            {
                if (!mask[i] && !reached[i]) { mask[i] = true; sealed_++; }
            }

            if (sealed_ > 0)
                Debug.Log($"[MapGenerator] 고립 구역 {sealed_}칸을 벽으로 메웠습니다.");
        }

        // ------------------------------------------------------------------
        // 유틸
        // ------------------------------------------------------------------

        static void WriteBlock(Tilemap map, BoundsInt bounds, TileBase[] tiles)
        {
            map.ClearAllTiles();
            // 셀 단위 SetTile 을 반복하면 매우 느리다. 블록 단위 한 번이 정답.
            map.SetTilesBlock(bounds, tiles);
        }

        static TileBase PickWeighted(WeightedTile[] pool, System.Random rng)
        {
            if (pool == null || pool.Length == 0) return null;

            float total = 0f;
            for (int i = 0; i < pool.Length; i++)
                if (pool[i].tile != null) total += Mathf.Max(0f, pool[i].weight);

            if (total <= 0f) return null;

            float roll = (float)rng.NextDouble() * total;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i].tile == null) continue;
                roll -= Mathf.Max(0f, pool[i].weight);
                if (roll <= 0f) return pool[i].tile;
            }
            return pool[pool.Length - 1].tile;
        }

        static ChunkBiomeSO PickWeightedBiome(ChunkBiomeSO[] pool, System.Random rng)
        {
            float total = 0f;
            for (int i = 0; i < pool.Length; i++)
                if (pool[i] != null) total += Mathf.Max(0f, pool[i].weight);

            if (total <= 0f)
            {
                // 전부 가중치 0이면 첫 유효 항목으로 폴백
                for (int i = 0; i < pool.Length; i++) if (pool[i] != null) return pool[i];
                return null;
            }

            float roll = (float)rng.NextDouble() * total;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] == null) continue;
                roll -= Mathf.Max(0f, pool[i].weight);
                if (roll <= 0f) return pool[i];
            }
            return pool[pool.Length - 1];
        }

        /// <summary>좌표와 시드를 섞어 결정적 해시를 만든다.</summary>
        static int Hash(int seed, int x, int y)
        {
            unchecked
            {
                int h = seed * 73856093;
                h ^= x * 19349663;
                h ^= y * 83492791;
                return h;
            }
        }

        static int CountTrue(bool[] a)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++) if (a[i]) n++;
            return n;
        }

        // ------------------------------------------------------------------
        // 시각화 — 씬 뷰에서 맵 경계와 스폰 게이트 확인
        // ------------------------------------------------------------------

        void OnDrawGizmosSelected()
        {
            if (config == null) return;

            Vector2Int size = config.MapSize;
            Vector2Int org = config.Origin;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0f));

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Vector3.zero, config.nexusClearRadius);

            Gizmos.color = Color.red;
            foreach (var g in SpawnGates)
                Gizmos.DrawWireSphere(new Vector3(g.x + org.x + 0.5f, g.y + org.y + 0.5f, 0f), 1.5f);
        }
    }
}
