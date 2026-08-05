using UnityEngine;
using UnityEngine.Tilemaps;

namespace LastSanctuary.Map
{
    /// <summary>가중치를 가진 타일 하나. 가중 랜덤 추첨에 사용.</summary>
    [System.Serializable]
    public struct WeightedTile
    {
        public TileBase tile;
        [Min(0f)] public float weight;
    }

    /// <summary>
    /// 방향별 타일 묶음. 전이(경계 장식) 타일에 쓴다 — 바닥칸이 어느 쪽에서 벽과
    /// 닿는지에 따라 맞는 타일을 골라야 경계가 자연스럽게 이어진다.
    /// </summary>
    [System.Serializable]
    public class DirectionalTileSet
    {
        public WeightedTile[] north;
        public WeightedTile[] south;
        public WeightedTile[] west;
        public WeightedTile[] east;
        public WeightedTile[] cornerNW;
        public WeightedTile[] cornerNE;
        public WeightedTile[] cornerSW;
        public WeightedTile[] cornerSE;

        public bool HasAny =>
            Count(north) + Count(south) + Count(west) + Count(east) +
            Count(cornerNW) + Count(cornerNE) + Count(cornerSW) + Count(cornerSE) > 0;

        static int Count(WeightedTile[] a) => a != null ? a.Length : 0;
    }

    /// <summary>
    /// 3/4 뷰 벽 타일 묶음. <b>입체감은 여기서 나온다</b> — 벽 덩어리의 어느 면이
    /// 바닥에 노출됐는지에 따라 다른 타일을 깔아야 두께가 보인다.
    ///
    ///   · <see cref="innerFill"/> — 사방이 벽인 내부. 윗면만 보인다
    ///   · <c>exposed*</c>         — 그 방향이 바닥에 노출된 칸. 그쪽 측면이 보인다
    ///   · <c>corner*</c>          — 두 방향이 동시에 노출된 칸
    ///
    /// 타일 팩의 `Wall_Inner_20px` / `Wall_Outer_20px` 시트 구조를 그대로 따른다
    /// (카탈로그의 <c>wall_inner</c> / <c>wall_outer</c> + <c>exposed_*</c> rule).
    /// </summary>
    [System.Serializable]
    public class WallTileSet
    {
        [Tooltip("사방이 벽으로 둘러싸인 내부 칸")]
        public WeightedTile[] innerFill;

        public WeightedTile[] exposedNorth;
        public WeightedTile[] exposedSouth;
        public WeightedTile[] exposedWest;
        public WeightedTile[] exposedEast;
        public WeightedTile[] cornerNW;
        public WeightedTile[] cornerNE;
        public WeightedTile[] cornerSW;
        public WeightedTile[] cornerSE;
    }

    /// <summary>
    /// OrganicTilemap 스프라이트 시트에서 잘라낸 타일 전체를 용도별로 담아두는 카탈로그.
    ///
    /// <b>손으로 채우지 않는다</b> — 시트와 같이 들어온 <c>TileMapCatalog.json</c> 에
    /// 타일마다 category/rule/weight 가 적혀 있으므로, 그 파일을 읽어 자동으로 분류한다
    /// (메뉴: <c>LastSanctuary > 맵 > OrganicTilemap 타일셋 다시 읽기</c>).
    /// 타일이 128개라 수동 배치는 현실적이지 않고, 시트가 갱신될 때마다 다시 읽으면 된다.
    ///
    /// 청크마다 이 풀에서 매번 다른 조합을 뽑아 쓰므로(<see cref="MapGenerator"/>),
    /// 여기에 담긴 타일은 맵 한 장 안에서 골고루 등장한다.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/Map/Organic Tile Set", fileName = "OrganicTileSet")]
    public class OrganicTileSetSO : ScriptableObject
    {
        [Header("자동 채움 — 아래 폴더의 TileMapCatalog.json 을 읽는다")]
        [Tooltip("시트 PNG 와 TileMapCatalog.json 이 있는 폴더")]
        public string sourceFolder = "Assets/_Project/Art/OrganicTilemap";

        [Tooltip("잘라낸 Tile 에셋들이 있는 폴더")]
        public string tilesFolder = "Assets/_Project/Art/OrganicTilemap/Tiles";

        [Header("바닥 (Tilemap_Ground)")]
        public WeightedTile[] ground;

        [Tooltip("갈라진 바닥. 청크마다 일정 비율로 섞인다")]
        public WeightedTile[] groundCracked;

        [Header("장식 (Tilemap_Deco) — 바닥 위에 얹는 오브젝트")]
        public WeightedTile[] props;

        [Header("장애물 (Tilemap_Obstacle) — 노출 방향별. 입체감의 핵심")]
        public WallTileSet walls = new WallTileSet();

        [Header("전이 (Tilemap_Deco) — 바닥과 벽의 경계 장식")]
        [Tooltip("피 웅덩이 계열 경계")]
        public DirectionalTileSet bloodEdge = new DirectionalTileSet();

        [Tooltip("균열/구덩이 계열 경계")]
        public DirectionalTileSet chasmEdge = new DirectionalTileSet();

        /// <summary>등록된 타일 총 개수. 임포트가 제대로 됐는지 확인용.</summary>
        public int TotalTiles =>
            Len(ground) + Len(groundCracked) + Len(props) + WallCount +
            DirCount(bloodEdge) + DirCount(chasmEdge);

        public int WallCount =>
            walls == null ? 0
                : Len(walls.innerFill) +
                  Len(walls.exposedNorth) + Len(walls.exposedSouth) +
                  Len(walls.exposedWest) + Len(walls.exposedEast) +
                  Len(walls.cornerNW) + Len(walls.cornerNE) +
                  Len(walls.cornerSW) + Len(walls.cornerSE);

        /// <summary>맵을 생성할 수 있는 최소 조건 — 바닥과 벽이 하나라도 있어야 한다.</summary>
        public bool IsUsable =>
            Len(ground) + Len(groundCracked) > 0 && WallCount > 0;

        static int Len(WeightedTile[] a) => a != null ? a.Length : 0;

        static int DirCount(DirectionalTileSet d) =>
            d == null ? 0
                      : Len(d.north) + Len(d.south) + Len(d.west) + Len(d.east) +
                        Len(d.cornerNW) + Len(d.cornerNE) + Len(d.cornerSW) + Len(d.cornerSE);
    }
}
