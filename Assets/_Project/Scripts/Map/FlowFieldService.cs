using System.Collections.Generic;
using UnityEngine;

namespace LastSanctuary.Map
{
    /// <summary>
    /// 성역까지의 거리장(flow field). 모든 몬스터가 같은 목적지를 향하므로
    /// 유닛마다 A* 를 돌리는 대신 맵 전체를 한 번 BFS 해두고 방향만 읽는다.
    /// 비용이 유닛 수와 무관해서 수백 마리도 부담이 없다.
    ///
    /// 통행 판정은 장애물 타일맵을 기준으로 한다. MapGenerator.Walkable 은
    /// 런타임 배열이라 씬을 다시 열면 사라지지만, 타일맵은 씬에 저장되어 남는다.
    /// </summary>
    public class FlowFieldService : MonoBehaviour
    {
        [SerializeField] MapGenerator mapGenerator;

        [Tooltip("게임 시작 시 맵 중앙을 목표로 자동 생성")]
        [SerializeField] bool buildOnStart = true;

        [Tooltip("대각 이동 허용. 끄면 4방향만 사용해 벽 사이로 새지 않는다")]
        [SerializeField] bool allowDiagonal = true;

        const int Unreachable = int.MaxValue;

        int[] _dist;
        Vector2Int _size, _origin;
        Vector3Int _goal;
        bool _ready;

        public bool IsReady => _ready;
        public Vector3Int Goal => _goal;

        void Start()
        {
            if (buildOnStart) BuildToMapCenter();
        }

        /// <summary>맵 중앙(성역 자리)을 목표로 생성한다.</summary>
        public void BuildToMapCenter()
        {
            if (mapGenerator == null)
            {
                Debug.LogError("[FlowField] Map Generator 가 연결되지 않았습니다.", this);
                return;
            }
            Build(mapGenerator.CenterCell);
        }

        /// <summary>지정 셀을 목표로 거리장을 만든다.</summary>
        public void Build(Vector3Int goalCell)
        {
            _ready = false;

            if (mapGenerator == null || mapGenerator.Config == null)
            {
                Debug.LogError("[FlowField] Map Generator / Config 가 없습니다.", this);
                return;
            }

            _size = mapGenerator.Config.MapSize;
            _origin = mapGenerator.Config.Origin;
            _goal = goalCell;

            int w = _size.x, h = _size.y;
            _dist = new int[w * h];
            for (int i = 0; i < _dist.Length; i++) _dist[i] = Unreachable;

            if (!TryToLocal(goalCell, out Vector2Int start))
            {
                Debug.LogError($"[FlowField] 목표 셀 {goalCell} 이 맵 밖입니다.", this);
                return;
            }

            var queue = new Queue<int>();
            int startIdx = start.x + start.y * w;
            _dist[startIdx] = 0;
            queue.Enqueue(startIdx);

            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                int cx = cur % w, cy = cur / w;
                int nd = _dist[cur] + 1;

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        if (!allowDiagonal && dx != 0 && dy != 0) continue;

                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;

                        int ni = nx + ny * w;
                        if (_dist[ni] <= nd) continue;
                        if (IsBlockedLocal(nx, ny)) continue;

                        // 대각 이동 시 코너를 뚫고 지나가지 않게 양쪽 직교 칸을 확인
                        if (dx != 0 && dy != 0 &&
                            (IsBlockedLocal(cx + dx, cy) || IsBlockedLocal(cx, cy + dy)))
                            continue;

                        _dist[ni] = nd;
                        queue.Enqueue(ni);
                    }
                }
            }

            _ready = true;

            int reached = 0;
            for (int i = 0; i < _dist.Length; i++) if (_dist[i] != Unreachable) reached++;
            Debug.Log($"[FlowField] {w}x{h} 생성 · 목표 {goalCell.x},{goalCell.y} · " +
                      $"도달 가능 {reached}/{_dist.Length}", this);
        }

        /// <summary>
        /// 해당 월드 위치에서 목표로 가는 방향. 이미 목표에 도달했거나
        /// 도달 불가 지역이면 false 를 반환한다.
        /// </summary>
        public bool TryGetDirection(Vector3 worldPos, out Vector2 direction)
        {
            direction = Vector2.zero;
            if (!_ready) return false;

            Vector3Int cell = WorldToCell(worldPos);
            if (!TryToLocal(cell, out Vector2Int local)) return false;

            int w = _size.x, h = _size.y;
            int here = _dist[local.x + local.y * w];
            if (here == 0) return false;                 // 목표 도착

            // 벽 안에 끼었으면(도달 불가) 주변에서 가장 가까운 유효 칸으로 빠져나간다.
            int bestDist = here == Unreachable ? Unreachable : here;
            Vector2Int bestCell = local;
            bool found = false;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = local.x + dx, ny = local.y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;

                    int nd = _dist[nx + ny * w];
                    if (nd == Unreachable || nd >= bestDist) continue;

                    bestDist = nd;
                    bestCell = new Vector2Int(nx, ny);
                    found = true;
                }
            }

            if (!found) return false;

            Vector3 target = mapGenerator.CellCenterWorld(LocalToCell(bestCell));
            direction = ((Vector2)(target - worldPos)).normalized;
            return direction.sqrMagnitude > 0.0001f;
        }

        /// <summary>해당 위치가 목표까지 도달 가능한지.</summary>
        public bool IsReachable(Vector3 worldPos) => IsCellReachable(WorldToCell(worldPos));

        /// <summary>
        /// 해당 <b>셀</b>이 목표(성역)까지 도달 가능한지.
        ///
        /// 탐험 목표를 고를 때 쓴다(유저 지시 2026-08-14: "물리적으로 갈 수 없는 곳 제외") —
        /// <see cref="MapGenerator.IsCellBlocked"/> 는 "그 칸이 벽인가"만 보므로 <b>벽으로 완전히
        /// 둘러싸인 빈 주머니</b>를 못 걸러낸다. 그런 칸을 정찰 목표로 잡으면 캐릭터가 벽에 붙어
        /// 재추첨 시간까지 멈춰 선다. 이 거리장은 성역에서 실제로 걸어서 퍼져 나간 결과라
        /// 그 판정이 공짜로 나온다.
        ///
        /// ⚠ 거리장이 아직 안 만들어졌으면 <b>전부 false</b> 다(<see cref="IsReady"/> 와 같은 규칙).
        /// 그래서 <b>호출부는 <see cref="IsReady"/> 일 때만 이 판정을 걸어야 한다</b> —
        /// 준비 전에 걸면 탐험 후보가 전부 사라져 캐릭터가 아무 데도 못 간다.
        /// </summary>
        public bool IsCellReachable(Vector3Int cell)
        {
            if (!_ready) return false;
            if (!TryToLocal(cell, out Vector2Int local)) return false;
            return _dist[local.x + local.y * _size.x] != Unreachable;
        }

        // ------------------------------------------------------------------

        Vector3Int WorldToCell(Vector3 world) =>
            mapGenerator.GroundTilemap != null
                ? mapGenerator.GroundTilemap.WorldToCell(world)
                : new Vector3Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y), 0);

        bool TryToLocal(Vector3Int cell, out Vector2Int local)
        {
            local = new Vector2Int(cell.x - _origin.x, cell.y - _origin.y);
            return local.x >= 0 && local.y >= 0 && local.x < _size.x && local.y < _size.y;
        }

        Vector3Int LocalToCell(Vector2Int local) =>
            new Vector3Int(local.x + _origin.x, local.y + _origin.y, 0);

        bool IsBlockedLocal(int lx, int ly) =>
            mapGenerator.IsCellBlocked(LocalToCell(new Vector2Int(lx, ly)));
    }
}
