using UnityEngine;

namespace LastSanctuary.Map
{
    /// <summary>맵 크기를 지정하는 방식.</summary>
    public enum MapSizeMode
    {
        /// <summary>청크 개수 × 청크 크기.</summary>
        Chunks,
        /// <summary>전체 타일 수로 직접 입력 (청크 크기의 배수로 보정).</summary>
        Tiles,
    }

    /// <summary>
    /// 맵 생성 전체 파라미터. 밸런싱 수치를 코드에 박지 않기 위해 전부 여기로 뺀다.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/Map/Generation Config", fileName = "MapGenConfig")]
    public class MapGenerationConfigSO : ScriptableObject
    {
        [Header("시드")]
        [Tooltip("같은 시드 = 항상 같은 맵. 버그 재현과 밸런싱 테스트에 필수")]
        public int seed = 12345;

        [Header("맵 크기")]
        [Tooltip("Chunks: 청크 개수로 지정 / Tiles: 전체 타일 수로 지정")]
        public MapSizeMode sizeMode = MapSizeMode.Chunks;

        [Tooltip("청크 개수 (가로, 세로) — sizeMode 가 Chunks 일 때 사용")]
        public Vector2Int chunkCount = new Vector2Int(3, 2);

        [Tooltip("전체 맵 타일 수 (가로, 세로) — sizeMode 가 Tiles 일 때 사용. " +
                 "청크 크기의 배수로 자동 보정된다")]
        public Vector2Int mapSizeTiles = new Vector2Int(60, 40);

        [Tooltip("청크 하나의 타일 수 (가로, 세로)")]
        public Vector2Int chunkSize = new Vector2Int(20, 20);

        [Header("넥서스")]
        [Tooltip("맵 중앙에서 장애물을 걷어내는 반경(타일). 중앙 건물과 초기 캐릭터 자리")]
        [Min(0)] public int nexusClearRadius = 6;

        [Header("경계 / 스폰")]
        [Tooltip("맵 최외곽을 벽으로 두르는 두께. 0이면 두르지 않음")]
        [Min(0)] public int borderWallThickness = 1;

        [Tooltip("상하좌우 각 변 중앙에 뚫을 스폰 통로 너비. 웨이브 몬스터 진입로")]
        [Min(0)] public int spawnGateWidth = 6;

        [Header("통로 보장")]
        [Tooltip("스폰 게이트에서 넥서스까지 강제로 뚫는 통로 너비")]
        [Range(1, 6)] public int corridorWidth = 3;

        [Tooltip("통로가 직선이 되지 않도록 주는 흔들림 강도. 0이면 직선")]
        [Range(0f, 1f)] public float corridorWobble = 0.35f;

        [Header("타일 세트")]
        [Tooltip("OrganicTilemap 카탈로그에서 자동 분류된 타일 묶음. " +
                 "메뉴 LastSanctuary > 맵 > OrganicTilemap 타일셋 다시 읽기 로 채운다")]
        public OrganicTileSetSO tileSet;

        // ------------------------------------------------------------------
        // 청크 랜덤화
        //
        // 예전에는 손으로 만든 바이옴 에셋 2개를 청크마다 추첨해서 썼다. 그러면
        // 에셋에 넣어둔 몇 개의 타일만 계속 나오고, 시트에 있는 나머지 타일은
        // 영원히 쓰이지 않는다. 지금은 청크마다 전체 풀에서 조합을 새로 뽑고
        // 가중치까지 다시 굴려서, 맵 한 장 안에 시트의 모든 타일이 골고루 섞이게 한다.
        // ------------------------------------------------------------------

        [Header("청크 랜덤화 — 생성할 때마다 청크마다 다시 뽑는다")]
        [Tooltip("한 청크가 쓸 바닥 타일 종류 수의 범위 (최소, 최대). " +
                 "작으면 청크마다 색이 뚜렷하게 갈리고, 크면 전체가 고르게 섞인다")]
        public Vector2Int groundTilesPerChunk = new Vector2Int(3, 7);

        [Tooltip("바닥 팔레트에서 '갈라진 바닥'이 차지하는 비율")]
        [Range(0f, 1f)] public float crackedGroundRatio = 0.3f;

        [Tooltip("한 청크가 쓸 프롭 종류 수의 범위 (최소, 최대)")]
        public Vector2Int propTilesPerChunk = new Vector2Int(2, 6);

        [Tooltip("프롭이 얹힐 확률 범위 (최소, 최대). 청크마다 이 사이에서 뽑는다")]
        public Vector2 propChanceRange = new Vector2(0.03f, 0.12f);

        [Tooltip("벽이 될 면적 비율의 범위 (최소, 최대). 청크마다 이 사이에서 뽑는다")]
        public Vector2 obstacleDensityRange = new Vector2(0.12f, 0.28f);

        [Tooltip("펄린 노이즈 스케일 범위. 작을수록 큰 덩어리, 클수록 잘게 흩어짐")]
        public Vector2 noiseScaleRange = new Vector2(0.08f, 0.16f);

        [Tooltip("셀룰러 오토마타 스무딩 횟수 범위 (최소, 최대)")]
        public Vector2Int smoothPassesRange = new Vector2Int(2, 4);

        [Header("전이 타일 (벽 경계 장식)")]
        [Tooltip("벽에 닿은 바닥칸에 경계 타일을 얹어 벽과 바닥 사이를 정리한다")]
        public bool useTransitionEdges = true;

        [Tooltip("경계 타일이 놓일 확률. 1 이면 벽에 닿은 모든 바닥칸에 놓인다")]
        [Range(0f, 1f)] public float transitionChance = 0.85f;

        [Tooltip("경계 타일의 방향이 반대로 보이면 켠다 (남/북과 동/서를 서로 바꾼다). " +
                 "아트의 방향 규약이 코드 가정과 다를 때를 위한 스위치")]
        public bool invertTransitionDirection = false;

        /// <summary>
        /// 생성에 사용할 청크 개수.
        /// Tiles 모드에서는 요청한 타일 수를 <b>덮을 수 있도록 올림</b>한다.
        /// 넘치는 영역은 MapSize 단계에서 잘려나간다.
        /// </summary>
        public Vector2Int ChunkCount
        {
            get
            {
                if (sizeMode == MapSizeMode.Chunks)
                    return new Vector2Int(Mathf.Max(1, chunkCount.x), Mathf.Max(1, chunkCount.y));

                return new Vector2Int(
                    Mathf.Max(1, Mathf.CeilToInt(mapSizeTiles.x / (float)Mathf.Max(4, chunkSize.x))),
                    Mathf.Max(1, Mathf.CeilToInt(mapSizeTiles.y / (float)Mathf.Max(4, chunkSize.y))));
            }
        }

        /// <summary>
        /// 청크가 덮는 전체 영역. 항상 청크 크기의 배수.
        /// Tiles 모드에서 MapSize 보다 클 수 있고, 그 차이가 잘려나가는 부분이다.
        /// </summary>
        public Vector2Int CoveredSize
        {
            get
            {
                Vector2Int cc = ChunkCount;
                return new Vector2Int(cc.x * Mathf.Max(4, chunkSize.x),
                                      cc.y * Mathf.Max(4, chunkSize.y));
            }
        }

        /// <summary>
        /// 최종 맵 타일 크기. Tiles 모드에서는 입력값이 그대로 나온다
        /// (청크 배수로 올려서 생성한 뒤 경계에 걸친 부분을 잘라냄).
        /// </summary>
        public Vector2Int MapSize =>
            sizeMode == MapSizeMode.Chunks
                ? CoveredSize
                : new Vector2Int(Mathf.Max(4, mapSizeTiles.x), Mathf.Max(4, mapSizeTiles.y));

        /// <summary>잘려나가는 타일 수 (가로, 세로). 0 이면 청크에 딱 맞음.</summary>
        public Vector2Int CroppedAmount => CoveredSize - MapSize;

        /// <summary>경계에 걸친 청크가 잘리는지.</summary>
        public bool IsCropped
        {
            get
            {
                Vector2Int c = CroppedAmount;
                return c.x > 0 || c.y > 0;
            }
        }

        /// <summary>맵이 원점을 중심으로 놓이도록 하는 좌하단 타일 좌표.</summary>
        public Vector2Int Origin
        {
            get
            {
                Vector2Int s = MapSize;
                return new Vector2Int(-s.x / 2, -s.y / 2);
            }
        }

        void OnValidate()
        {
            chunkCount   = new Vector2Int(Mathf.Max(1, chunkCount.x), Mathf.Max(1, chunkCount.y));
            chunkSize    = new Vector2Int(Mathf.Max(4, chunkSize.x),  Mathf.Max(4, chunkSize.y));
            mapSizeTiles = new Vector2Int(Mathf.Max(4, mapSizeTiles.x), Mathf.Max(4, mapSizeTiles.y));

            // 범위 필드는 (최소, 최대) 순서가 뒤집히면 추첨이 깨지므로 여기서 바로잡는다.
            groundTilesPerChunk = SortedInt(groundTilesPerChunk, 1);
            propTilesPerChunk   = SortedInt(propTilesPerChunk, 0);
            smoothPassesRange   = SortedInt(smoothPassesRange, 0);
            propChanceRange     = Sorted(propChanceRange, 0f, 1f);
            obstacleDensityRange = Sorted(obstacleDensityRange, 0f, 0.6f);
            noiseScaleRange     = Sorted(noiseScaleRange, 0.02f, 0.5f);
        }

        static Vector2Int SortedInt(Vector2Int v, int min)
        {
            int a = Mathf.Max(min, v.x);
            int b = Mathf.Max(min, v.y);
            return new Vector2Int(Mathf.Min(a, b), Mathf.Max(a, b));
        }

        static Vector2 Sorted(Vector2 v, float min, float max)
        {
            float a = Mathf.Clamp(v.x, min, max);
            float b = Mathf.Clamp(v.y, min, max);
            return new Vector2(Mathf.Min(a, b), Mathf.Max(a, b));
        }
    }
}
