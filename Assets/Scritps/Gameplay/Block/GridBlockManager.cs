using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Quản lý toàn bộ lane/cột. Cung cấp mục tiêu cho gun (cột ngoài cùng gần nhất cùng màu),
    /// theo dõi số block còn lại (WIN) và các front-column theo màu (LOSE) — yêu cầu #5.
    /// </summary>
    public class GridBlockManager : Singleton<GridBlockManager>
    {
        private readonly List<BlockLane> _lanes = new List<BlockLane>();
        private bool _everHadBlocks;

        public void Build(LevelData level)
        {
            Clear();
            foreach (var laneData in level.Lanes)
            {
                if (laneData == null) continue;
                var go = new GameObject("Lane");
                go.transform.SetParent(transform);
                var lane = go.AddComponent<BlockLane>();
                lane.Build(laneData, level.BlockPrefab);
                _lanes.Add(lane);
            }
            if (RemainingBlocks > 0) _everHadBlocks = true;
        }

        public void Clear()
        {
            foreach (var l in _lanes) if (l != null) Destroy(l.gameObject);
            _lanes.Clear();
            _everHadBlocks = false;
        }

        /// <summary>Cột ngoài cùng gần <paramref name="from"/> nhất có màu <paramref name="color"/>.</summary>
        public BlockColumn FindTargetColumn(BlockColor color, Vector3 from)
        {
            BlockColumn best = null;
            float bestSqr = float.MaxValue;
            foreach (var lane in _lanes)
            {
                var front = lane.FrontColumn;
                if (front == null || front.Color != color) continue;
                float d = (front.transform.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = front; }
            }
            return best;
        }

        /// <summary>Có tồn tại cột ngoài cùng màu này không (dùng cho check LOSE).</summary>
        public bool HasFrontColumnOfColor(BlockColor color)
        {
            foreach (var lane in _lanes)
            {
                var f = lane.FrontColumn;
                if (f != null && f.Color == color) return true;
            }
            return false;
        }

        public int RemainingBlocks
        {
            get { int s = 0; foreach (var l in _lanes) if (l != null) s += l.TotalBlocks; return s; }
        }

        public bool AllCleared => _everHadBlocks && RemainingBlocks == 0;

        public void OnLaneChanged() => GameController.Instance?.OnBoardChanged();
    }
}
