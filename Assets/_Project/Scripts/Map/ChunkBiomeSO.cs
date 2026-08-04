using UnityEngine;
using UnityEngine.Tilemaps;

namespace LastSanctuary.Map
{
    /// <summary>
    /// 가중치를 가진 타일 하나. 가중 랜덤 추첨에 사용.
    /// </summary>
    [System.Serializable]
    public struct WeightedTile
    {
        public TileBase tile;
        [Min(0f)] public float weight;
    }

    /// <summary>
    /// 청크 한 칸의 생성 규칙. 같은 맵 안에서도 청크마다 다른 바이옴이 뽑히므로
    /// "혈관 지대", "종양 지대"처럼 지형 성격을 나눌 수 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "LastSanctuary/Map/Chunk Biome", fileName = "Biome_")]
    public class ChunkBiomeSO : ScriptableObject
    {
        [Header("식별")]
        public string displayName = "혈관 지대";

        [Tooltip("청크 바이옴 추첨 가중치. 클수록 자주 등장")]
        [Min(0f)] public float weight = 1f;

        [Header("바닥 (Tilemap_Ground) — 모든 칸을 채움")]
        public WeightedTile[] floorTiles;

        [Header("장식 (Tilemap_Deco) — 확률적으로 얹음")]
        public WeightedTile[] decoTiles;
        [Range(0f, 1f)] public float decoChance = 0.06f;

        [Header("장애물 (Tilemap_Obstacle)")]
        public WeightedTile[] obstacleTiles;

        [Tooltip("장애물 비율. 0.18 = 약 18%가 벽 후보")]
        [Range(0f, 0.6f)] public float obstacleDensity = 0.18f;

        [Tooltip("펄린 노이즈 스케일. 작을수록 큰 덩어리, 클수록 잘게 흩어짐")]
        [Range(0.02f, 0.5f)] public float noiseScale = 0.12f;

        [Tooltip("셀룰러 오토마타 스무딩 횟수. 흩어진 점을 덩어리로 뭉쳐 자연스럽게 만든다")]
        [Range(0, 5)] public int smoothPasses = 3;

        /// <summary>에디터에서 값을 만졌을 때 최소한의 유효성만 보정.</summary>
        void OnValidate()
        {
            if (weight < 0f) weight = 0f;
        }
    }
}
