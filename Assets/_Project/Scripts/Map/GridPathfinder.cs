using System.Collections.Generic;
using UnityEngine;

namespace LastSanctuary.Map
{
    /// <summary>
    /// 타일 그리드 위의 A* 길찾기. <see cref="MapGenerator"/> 와 같은 오브젝트에
    /// 붙는다(<see cref="FlowFieldService"/> 와 동일한 배치).
    ///
    /// 왜 필요한가: 몬스터의 성역 진군은 <see cref="FlowFieldService"/> 가 목표
    /// 하나를 향한 방향장을 미리 깔아 해결하지만, 캐릭터는 유닛마다 목적지가 다르므로
    /// 공용 방향장을 쓸 수 없다. 그동안 캐릭터 이동은 "직선으로 가다 막히면 축을
    /// 미끄러뜨리는" 국소 회피였는데, 이 방식은 벽이 조금만 크거나 오목하면
    /// 원리적으로 빠져나올 수 없어 캐릭터가 벽 앞에 붙어 멈추는 일이 잦았다.
    ///
    /// 탐색 버퍼는 이 컴포넌트가 한 벌만 들고 재사용한다 — 맵이 320x320이면
    /// 유닛마다 배열을 들 수 없기 때문이다. 탐색은 동기 실행이라 공유해도 안전하다.
    /// </summary>
    [RequireComponent(typeof(MapGenerator))]
    public class GridPathfinder : MonoBehaviour
    {
        // 대각 이동을 √2 로 두기 위한 정수 비용 (10 : 14 ≈ 1 : 1.4)
        const int CostStraight = 10;
        const int CostDiagonal = 14;

        [Header("탐색 한도")]
        [Tooltip("한 번의 탐색에서 펼칠 수 있는 최대 칸 수. 초과하면 그때까지 목표에 " +
                 "가장 가까웠던 칸까지의 경로를 돌려준다 — 완전 실패보다 조금이라도 " +
                 "가까워지는 편이 낫다")]
        [Min(256)] [SerializeField] int maxExpandedNodes = 6000;

        [Tooltip("목표 칸이 벽일 때 주변 몇 칸까지 대체 목표를 찾을지")]
        [Min(0)] [SerializeField] int goalFallbackRadius = 4;

        [Header("가시선")]
        [Tooltip("두 점 사이가 직선으로 통하는지 검사할 때의 샘플 간격(타일). " +
                 "작을수록 정확하지만 비용이 늘어난다")]
        [Min(0.05f)] [SerializeField] float lineOfSightStepTiles = 0.25f;

        MapGenerator _map;

        // 맵 크기에 맞춰 한 번만 할당하고 계속 재사용하는 탐색 버퍼.
        int _width, _height, _originX, _originY;
        int[] _gScore;
        int[] _fScore;
        int[] _cameFrom;
        int[] _visitStamp;    // 이 탐색에서 값이 유효한지 (배열 초기화 비용 제거용)
        int[] _closedStamp;
        int _stamp;

        readonly List<int> _heap = new List<int>(256);
        readonly List<Vector3> _reconstructScratch = new List<Vector3>(64);
        readonly List<Vector3> _smoothScratch = new List<Vector3>(64);

        void Awake() => _map = GetComponent<MapGenerator>();

        /// <summary>
        /// <paramref name="fromWorld"/> 에서 <paramref name="toWorld"/> 까지의 경로를
        /// 월드 좌표 웨이포인트 목록으로 채운다. 출발 칸은 포함하지 않는다.
        /// 목표가 벽이면 근처의 갈 수 있는 칸으로 대체하고, 탐색 한도를 넘으면
        /// 목표에 가장 가까웠던 칸까지의 부분 경로를 돌려준다.
        /// </summary>
        public bool TryFindPath(Vector3 fromWorld, Vector3 toWorld, List<Vector3> pathOut)
        {
            if (pathOut == null) return false;
            pathOut.Clear();
            if (!EnsureBuffers()) return false;

            Vector3Int start = _map.WorldToCell(fromWorld);
            Vector3Int goal = _map.WorldToCell(toWorld);

            // 목표가 벽 안이면(순찰 지점이 벽에 걸린 경우 등) 근처 빈 칸으로 옮긴다.
            if (!_map.IsCellPlaceable(goal) &&
                !_map.TryFindPlaceableNear(goal, goalFallbackRadius, null, out goal))
                return false;

            if (!TryIndex(start, out int startIdx)) return false;
            if (!TryIndex(goal, out int goalIdx)) return false;

            if (startIdx == goalIdx)
            {
                pathOut.Add(_map.CellCenterWorld(goal));
                return true;
            }

            int reachedIdx = Search(startIdx, goalIdx, goal);
            if (reachedIdx < 0) return false;

            Reconstruct(startIdx, reachedIdx, pathOut);
            Smooth(fromWorld, pathOut);
            return pathOut.Count > 0;
        }

        /// <summary>
        /// 두 점을 잇는 직선이 벽을 통과하지 않는지. 경로 없이 바로 갈 수 있는지
        /// 판단하거나(대부분의 경우 A* 자체가 불필요하다), 경로를 매끄럽게 다듬을 때 쓴다.
        /// </summary>
        public bool HasLineOfSight(Vector3 fromWorld, Vector3 toWorld)
        {
            if (_map == null) return false;

            Vector2 d = toWorld - fromWorld;
            float dist = d.magnitude;
            if (dist < 1e-4f) return _map.IsCellPlaceable(_map.WorldToCell(fromWorld));

            int steps = Mathf.CeilToInt(dist / Mathf.Max(0.05f, lineOfSightStepTiles));
            for (int i = 0; i <= steps; i++)
            {
                Vector3 p = fromWorld + (Vector3)(d * (i / (float)steps));
                if (!_map.IsCellPlaceable(_map.WorldToCell(p))) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------
        // A* 본체
        // ------------------------------------------------------------------

        /// <summary>도달한 칸의 인덱스. 목표에 닿았으면 goalIdx, 한도 초과면 최선 근접 칸.</summary>
        int Search(int startIdx, int goalIdx, Vector3Int goalCell)
        {
            _stamp++;
            _heap.Clear();

            Touch(startIdx);
            _gScore[startIdx] = 0;
            _fScore[startIdx] = Heuristic(startIdx, goalCell);
            _cameFrom[startIdx] = -1;
            HeapPush(startIdx);

            int bestIdx = startIdx;
            int bestH = _fScore[startIdx];
            int expanded = 0;

            while (_heap.Count > 0)
            {
                int current = HeapPop();
                if (_closedStamp[current] == _stamp) continue;   // 지연 삭제된 중복 항목
                _closedStamp[current] = _stamp;

                if (current == goalIdx) return goalIdx;

                if (++expanded > maxExpandedNodes) break;

                int cx = current % _width;
                int cy = current / _width;

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || nx >= _width || ny < 0 || ny >= _height) continue;
                        if (!IsLocalWalkable(nx, ny)) continue;

                        // 대각선으로 벽 모서리를 관통하지 못하게 한다 — 양옆이 모두
                        // 열려 있을 때만 대각 이동을 허용한다.
                        bool diagonal = dx != 0 && dy != 0;
                        if (diagonal &&
                            (!IsLocalWalkable(cx + dx, cy) || !IsLocalWalkable(cx, cy + dy)))
                            continue;

                        int neighbor = nx + ny * _width;
                        if (_closedStamp[neighbor] == _stamp) continue;

                        int tentative = _gScore[current] + (diagonal ? CostDiagonal : CostStraight);
                        Touch(neighbor);
                        if (tentative >= _gScore[neighbor]) continue;

                        _gScore[neighbor] = tentative;
                        int h = Heuristic(neighbor, goalCell);
                        _fScore[neighbor] = tentative + h;
                        _cameFrom[neighbor] = current;
                        HeapPush(neighbor);

                        if (h < bestH) { bestH = h; bestIdx = neighbor; }
                    }
                }
            }

            // 목표까지 못 갔다 — 가장 가까이 갔던 칸까지라도 돌려준다.
            return bestIdx != startIdx ? bestIdx : -1;
        }

        /// <summary>이 탐색에서 아직 손대지 않은 칸이면 초기값을 넣는다.</summary>
        void Touch(int idx)
        {
            if (_visitStamp[idx] == _stamp) return;
            _visitStamp[idx] = _stamp;
            _gScore[idx] = int.MaxValue;
            _fScore[idx] = int.MaxValue;
            _cameFrom[idx] = -1;
        }

        int Heuristic(int idx, Vector3Int goalCell)
        {
            int x = idx % _width + _originX;
            int y = idx / _width + _originY;
            int dx = Mathf.Abs(x - goalCell.x);
            int dy = Mathf.Abs(y - goalCell.y);

            // 옥타일 거리 — 대각 이동을 허용하는 그리드의 정확한 하한값.
            int min = Mathf.Min(dx, dy);
            return CostDiagonal * min + CostStraight * (dx + dy - 2 * min);
        }

        void Reconstruct(int startIdx, int endIdx, List<Vector3> pathOut)
        {
            _reconstructScratch.Clear();

            int cursor = endIdx;
            int guard = _width * _height;
            while (cursor >= 0 && cursor != startIdx && guard-- > 0)
            {
                _reconstructScratch.Add(_map.CellCenterWorld(LocalToCell(cursor)));
                cursor = _cameFrom[cursor];
            }

            // 목표 → 출발 순으로 쌓였으므로 뒤집는다. 출발 칸은 넣지 않았다.
            for (int i = _reconstructScratch.Count - 1; i >= 0; i--)
                pathOut.Add(_reconstructScratch[i]);
        }

        /// <summary>
        /// 격자 경로를 가시선으로 당겨 불필요한 웨이포인트를 지운다(string pulling).
        /// 없으면 캐릭터가 칸 중심을 하나하나 밟으며 지그재그로 걷는다.
        /// </summary>
        void Smooth(Vector3 fromWorld, List<Vector3> path)
        {
            if (path.Count < 2) return;

            _smoothScratch.Clear();
            Vector3 anchor = fromWorld;
            int i = 0;

            while (i < path.Count)
            {
                // anchor 에서 직선으로 갈 수 있는 가장 먼 웨이포인트를 찾는다.
                int farthest = i;
                for (int j = path.Count - 1; j > i; j--)
                {
                    if (!HasLineOfSight(anchor, path[j])) continue;
                    farthest = j;
                    break;
                }

                _smoothScratch.Add(path[farthest]);
                anchor = path[farthest];
                i = farthest + 1;
            }

            path.Clear();
            path.AddRange(_smoothScratch);
        }

        // ------------------------------------------------------------------
        // 버퍼 / 좌표 변환
        // ------------------------------------------------------------------

        bool EnsureBuffers()
        {
            if (_map == null) _map = GetComponent<MapGenerator>();
            MapGenerationConfigSO cfg = _map != null ? _map.Config : null;
            if (cfg == null) return false;

            Vector2Int size = cfg.MapSize;
            if (size.x <= 0 || size.y <= 0) return false;

            _width = size.x;
            _height = size.y;
            Vector2Int org = cfg.Origin;
            _originX = org.x;
            _originY = org.y;

            int need = _width * _height;
            if (_gScore == null || _gScore.Length != need)
            {
                _gScore = new int[need];
                _fScore = new int[need];
                _cameFrom = new int[need];
                _visitStamp = new int[need];
                _closedStamp = new int[need];
                _stamp = 0;
            }
            return true;
        }

        bool TryIndex(Vector3Int cell, out int index)
        {
            int lx = cell.x - _originX;
            int ly = cell.y - _originY;
            if (lx < 0 || lx >= _width || ly < 0 || ly >= _height)
            {
                index = -1;
                return false;
            }
            index = lx + ly * _width;
            return true;
        }

        Vector3Int LocalToCell(int idx) =>
            new Vector3Int(idx % _width + _originX, idx / _width + _originY, 0);

        bool IsLocalWalkable(int lx, int ly)
        {
            if (lx < 0 || lx >= _width || ly < 0 || ly >= _height) return false;
            return !_map.IsCellBlocked(new Vector3Int(lx + _originX, ly + _originY, 0));
        }

        // ------------------------------------------------------------------
        // 최소 힙 (f 값 기준). 중복 항목은 지연 삭제로 처리한다.
        // ------------------------------------------------------------------

        void HeapPush(int idx)
        {
            _heap.Add(idx);
            int child = _heap.Count - 1;
            while (child > 0)
            {
                int parent = (child - 1) / 2;
                if (_fScore[_heap[parent]] <= _fScore[_heap[child]]) break;
                (_heap[parent], _heap[child]) = (_heap[child], _heap[parent]);
                child = parent;
            }
        }

        int HeapPop()
        {
            int top = _heap[0];
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);

            int parent = 0;
            int count = _heap.Count;
            while (true)
            {
                int left = parent * 2 + 1;
                if (left >= count) break;
                int right = left + 1;
                int smaller = right < count && _fScore[_heap[right]] < _fScore[_heap[left]]
                    ? right : left;
                if (_fScore[_heap[parent]] <= _fScore[_heap[smaller]]) break;
                (_heap[parent], _heap[smaller]) = (_heap[smaller], _heap[parent]);
                parent = smaller;
            }
            return top;
        }
    }
}
