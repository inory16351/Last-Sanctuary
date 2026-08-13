using System.Collections.Generic;
using UnityEngine;
using LastSanctuary.Map;

namespace LastSanctuary.Fog
{
    /// <summary>
    /// 전장의 안개. 맵 한 칸이 텍스처 한 픽셀인 오버레이를 맵 위에 덮고,
    /// <see cref="VisionSource"/> 주변만 알파를 걷어낸다.
    ///
    /// 칸 상태는 3단계다.
    ///   미탐사 — 완전히 가려짐
    ///   탐사됨 — 한 번 봤던 곳. 지형은 기억하지만 지금은 안 보이므로 반투명하게 남긴다
    ///   시야 내 — 지금 누군가 보고 있는 곳. 완전히 트임
    ///
    /// 타일맵을 하나 더 쓰지 않고 텍스처 한 장으로 처리해서 320x320 맵에서도
    /// 갱신 비용이 일정하다. 픽셀아트에 맞춰 Point 필터를 쓴다.
    /// </summary>
    [RequireComponent(typeof(MapGenerator))]
    public class FogOfWarService : MonoBehaviour
    {
        const byte Unexplored = 0;
        const byte Explored = 1;
        const byte Visible = 2;

        [SerializeField] MapGenerator mapGenerator;

        [Header("색")]
        [Tooltip("한 번도 못 본 칸")]
        [SerializeField] Color unexploredColor = new Color(0.02f, 0.02f, 0.03f, 1f);

        [Tooltip("봤지만 지금은 시야 밖인 칸")]
        [SerializeField] Color exploredColor = new Color(0.02f, 0.02f, 0.03f, 0.6f);

        [Header("표시")]
        [SerializeField] string sortingLayer = "Overhead";
        [SerializeField] int sortingOrder = 100;

        [Header("성능")]
        [Tooltip("시야를 다시 계산하는 간격(초). 0 이면 매 프레임")]
        [Min(0f)] [SerializeField] float refreshInterval = 0.1f;

        [Header("실행")]
        [Tooltip("끄면 안개가 아예 생성되지 않는다 (디버깅용)")]
        [SerializeField] bool enableFog = true;

        Texture2D _texture;
        Color32[] _pixels;
        byte[] _state;
        Vector2Int _size, _origin;
        SpriteRenderer _renderer;
        bool _dirty;
        float _nextRefresh;

        // 지난 갱신에 "시야 내"였던 칸. 전체를 훑지 않고 이 목록만 되돌린다.
        readonly List<int> _visibleLastRefresh = new List<int>(4096);

        public bool IsReady => _state != null;

        /// <summary>밝혀진 칸 수. UI 가 없는 동안 정찰 진행도를 확인하는 용도.</summary>
        public int ExploredCount { get; private set; }

        public float ExploredPercent =>
            _state == null || _state.Length == 0 ? 0f : 100f * ExploredCount / _state.Length;

        void Reset() => mapGenerator = GetComponent<MapGenerator>();

        void Start()
        {
            if (!enableFog) return;
            Build();
        }

        // ------------------------------------------------------------------

        /// <summary>맵 크기에 맞춰 안개를 새로 만든다. 맵을 다시 생성했다면 호출할 것.</summary>
        public void Build()
        {
            if (mapGenerator == null) mapGenerator = GetComponent<MapGenerator>();
            if (mapGenerator == null || mapGenerator.Config == null)
            {
                Debug.LogError("[FogOfWar] Map Generator / Config 가 없습니다.", this);
                return;
            }

            _size = mapGenerator.Config.MapSize;
            _origin = mapGenerator.Config.Origin;

            int count = _size.x * _size.y;
            _state = new byte[count];
            _pixels = new Color32[count];

            Color32 hidden = unexploredColor;
            for (int i = 0; i < count; i++) _pixels[i] = hidden;

            _texture = new Texture2D(_size.x, _size.y, TextureFormat.RGBA32, false)
            {
                name = "FogOfWar",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
            ExploredCount = 0;

            SetupRenderer();
            _visibleLastRefresh.Clear();
            _nextRefresh = 0f;

            Debug.Log($"[FogOfWar] {_size.x}x{_size.y} 생성 · 원점 {_origin.x},{_origin.y}", this);
        }

        void SetupRenderer()
        {
            if (_renderer == null)
            {
                var go = new GameObject("FogOverlay");
                go.transform.SetParent(transform, false);
                _renderer = go.AddComponent<SpriteRenderer>();
            }

            // 픽셀 하나 = 타일 하나. 피벗을 좌하단에 두고 맵 좌하단 모서리에 맞춘다.
            var sprite = Sprite.Create(_texture,
                                       new Rect(0f, 0f, _size.x, _size.y),
                                       Vector2.zero,
                                       pixelsPerUnit: 1f);
            sprite.name = "FogOfWar";

            _renderer.sprite = sprite;
            _renderer.sortingLayerName = sortingLayer;
            _renderer.sortingOrder = sortingOrder;

            Vector3 originCenter = mapGenerator.CellCenterWorld(
                new Vector3Int(_origin.x, _origin.y, 0));
            _renderer.transform.position = originCenter - new Vector3(0.5f, 0.5f, 0f);
        }

        // ------------------------------------------------------------------

        void LateUpdate()
        {
            if (_state == null) return;
            if (Time.time < _nextRefresh) return;
            _nextRefresh = Time.time + refreshInterval;

            Refresh();
        }

        void Refresh()
        {
            // 지난번 시야를 "탐사됨"으로 되돌린다.
            Color32 dim = exploredColor;
            for (int i = 0; i < _visibleLastRefresh.Count; i++)
            {
                int idx = _visibleLastRefresh[i];
                if (_state[idx] != Visible) continue;
                _state[idx] = Explored;
                _pixels[idx] = dim;
                _dirty = true;
            }
            _visibleLastRefresh.Clear();

            var sources = VisionSource.All;
            for (int s = 0; s < sources.Count; s++)
            {
                VisionSource src = sources[s];
                if (src == null) continue;

                // 사각 시야를 쓰는 유닛은 상자만 밝힌다 — 「타고난 섬세함」의 엘린이 그렇다.
                // 원형으로 같은 그림을 덮으려면 모서리까지 닿는 반경이 필요해 넓이가 3~4배가 된다.
                if (src.UsesBox)
                    RevealRect(src.transform.position, src.VisionBoxTiles, src.VisionBoxOffsetTiles);
                else
                    RevealCircle(src.transform.position, src.VisionRadius);
            }

            if (!_dirty) return;
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
            _dirty = false;
        }

        /// <summary>
        /// ★ <b>사각형 영역</b>을 시야 안으로 만든다. <paramref name="sizeTiles"/> 가 상자의
        /// 가로·세로(타일), <paramref name="offsetTiles"/> 가 <paramref name="worldPos"/> 기준
        /// 중심 오프셋이다(캐릭터는 피벗이 발밑이라 y 에 높이의 절반을 넣는다).
        ///
        /// <b>왜 원형과 따로 두나</b> — "그림만 딱 덮는다"는 요구는 원으로 만족시킬 수 없다.
        /// 원이 그림의 네 모서리까지 닿아야 하므로 밝히는 넓이가 그림의 3~4배가 된다
        /// (엘린 실측: 그림 5.6타일² vs 반경 2.52 원 19.9타일²). 유저 지시
        /// "시야가 딱 캐릭터의 이미지 만큼의 공간만" 을 지키려면 사각형이어야 한다(2026-08-13).
        ///
        /// <b>경계 처리</b> — 상자가 <b>조금이라도 걸치는 칸은 전부</b> 밝힌다. 그래야 그림이
        /// 걸쳐 있는 칸에 안개가 남아 <b>몸의 일부만 어둡게 보이는</b> 일이 없다
        /// (73-7-1절이 고치려던 증상이 그것이다).
        /// </summary>
        public void RevealRect(Vector3 worldPos, Vector2 sizeTiles, Vector2 offsetTiles)
        {
            if (_state == null) return;
            if (sizeTiles.x <= 0f || sizeTiles.y <= 0f) return;

            Vector3 center = worldPos + (Vector3)offsetTiles;
            Vector3 half = new Vector3(sizeTiles.x * 0.5f, sizeTiles.y * 0.5f, 0f);

            // 걸치는 칸을 하나도 빠뜨리지 않으려면 두 모서리를 각각 칸으로 바꿔 그 사이를 전부 훑는다.
            Vector3Int min = WorldToCell(center - half);
            Vector3Int max = WorldToCell(center + half);

            for (int y = min.y; y <= max.y; y++)
            {
                for (int x = min.x; x <= max.x; x++)
                {
                    if (!TryIndex(x, y, out int idx)) continue;

                    _visibleLastRefresh.Add(idx);
                    if (_state[idx] == Visible) continue;

                    if (_state[idx] == Unexplored) ExploredCount++;
                    _state[idx] = Visible;
                    _pixels[idx] = new Color32(0, 0, 0, 0);
                    _dirty = true;
                }
            }
        }

        /// <summary>지정 위치 주변을 시야 안으로 만든다.</summary>
        public void RevealCircle(Vector3 worldPos, float radiusTiles)
        {
            if (_state == null) return;

            Vector3Int center = WorldToCell(worldPos);
            int r = Mathf.CeilToInt(radiusTiles);
            float sqrR = radiusTiles * radiusTiles;

            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy > sqrR) continue;
                    if (!TryIndex(center.x + dx, center.y + dy, out int idx)) continue;

                    _visibleLastRefresh.Add(idx);
                    if (_state[idx] == Visible) continue;

                    if (_state[idx] == Unexplored) ExploredCount++;
                    _state[idx] = Visible;
                    _pixels[idx] = new Color32(0, 0, 0, 0);
                    _dirty = true;
                }
            }
        }

        // ------------------------------------------------------------------
        // 조회
        // ------------------------------------------------------------------

        /// <summary>한 번이라도 밝혀진 칸인지.</summary>
        public bool IsExplored(Vector3Int cell) =>
            TryIndex(cell.x, cell.y, out int idx) && _state[idx] != Unexplored;

        /// <summary>지금 누군가의 시야 안인지.</summary>
        public bool IsVisible(Vector3Int cell) =>
            TryIndex(cell.x, cell.y, out int idx) && _state[idx] == Visible;

        /// <summary>월드 좌표 기준으로 지금 누군가의 시야 안인지. 전투 타겟팅에서 쓴다.</summary>
        public bool IsVisibleWorld(Vector3 worldPos) => IsVisible(WorldToCell(worldPos));

        /// <summary>
        /// 아직 못 밝힌 칸 중 갈 만한 곳을 하나 고른다. 정찰 목표 선정용.
        /// 링을 넓혀가며 찾고, 같은 링에서는 무작위로 골라 여러 캐릭터가
        /// 한 곳으로 몰리지 않게 한다.
        ///
        /// minRadiusTiles 를 두는 이유: 바로 옆 칸부터 찾으면 정찰병이 시야 경계를
        /// 한 칸씩 갉아먹으며 제자리에서 맴돈다. 최소 거리를 줘야 한 방향으로
        /// 쭉 나갔다 오는 정찰다운 움직임이 된다.
        /// </summary>
        public bool TryFindUnexploredTarget(Vector3 fromWorld, float minRadiusTiles,
                                            float maxRadiusTiles, System.Random rng,
                                            out Vector3 worldTarget)
        {
            worldTarget = default;
            if (_state == null) return false;

            Vector3Int from = WorldToCell(fromWorld);
            int minR = Mathf.Max(2, Mathf.RoundToInt(minRadiusTiles));
            int maxR = Mathf.CeilToInt(maxRadiusTiles);
            var ring = new List<Vector3Int>(64);

            // 먼저 최소 거리 바깥을 훑고, 거기서 못 찾으면 가까운 쪽으로 되돌아온다.
            for (int r = minR; r <= maxR; r++)
            {
                ring.Clear();
                CollectRing(from, r, ring);
                if (ring.Count == 0) continue;

                worldTarget = mapGenerator.CellCenterWorld(ring[NextIndex(rng, ring.Count)]);
                return true;
            }

            for (int r = 2; r < minR; r++)
            {
                ring.Clear();
                CollectRing(from, r, ring);
                if (ring.Count == 0) continue;

                worldTarget = mapGenerator.CellCenterWorld(ring[NextIndex(rng, ring.Count)]);
                return true;
            }
            return false;
        }

        static int NextIndex(System.Random rng, int count) =>
            rng != null ? rng.Next(count) : Random.Range(0, count);

        void CollectRing(Vector3Int center, int r, List<Vector3Int> into)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    // 링의 테두리만
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;

                    var cell = new Vector3Int(center.x + dx, center.y + dy, 0);
                    if (!TryIndex(cell.x, cell.y, out int idx)) continue;
                    if (_state[idx] != Unexplored) continue;
                    if (mapGenerator.IsCellBlocked(cell)) continue;

                    into.Add(cell);
                }
            }
        }

        // ------------------------------------------------------------------

        Vector3Int WorldToCell(Vector3 world) =>
            mapGenerator.GroundTilemap != null
                ? mapGenerator.GroundTilemap.WorldToCell(world)
                : new Vector3Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y), 0);

        bool TryIndex(int cellX, int cellY, out int index)
        {
            int lx = cellX - _origin.x;
            int ly = cellY - _origin.y;
            if (lx < 0 || ly < 0 || lx >= _size.x || ly >= _size.y)
            {
                index = -1;
                return false;
            }
            index = lx + ly * _size.x;
            return true;
        }
    }
}
