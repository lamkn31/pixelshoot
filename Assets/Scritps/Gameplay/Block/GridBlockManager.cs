using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Quản lý các grid block dạng vòng cung. Mỗi grid = nhiều RING (hàng cung); gun chỉ bắn được cell
    /// ở ring NGOÀI CÙNG (front). Khi ring front bị phá hết → các ring phía sau DỒN vào 1 bậc (giảm
    /// bán kính), tái phân bố vị trí (yêu cầu #5).
    /// </summary>
    public class GridBlockManager : Singleton<GridBlockManager>
    {
        [SerializeField] private float collapseDuration = 0.25f;

        private class Ring { public readonly List<BlockCell> Cells = new List<BlockCell>(); }
        private class GridRuntime
        {
            public BlockGridData Data;
            public readonly List<Ring> Rings = new List<Ring>();
            public Transform Root;                 // node cha để gắn cell (kể cả cell refill)
            public Queue<PendingBlockData> Pending; // hàng đợi spawner nhả thêm
            public float StackSpacing;
            public int TargetRows;                 // số ring mong muốn — refill giữ ~ mức này
        }

        private readonly List<GridRuntime> _grids = new List<GridRuntime>();
        private bool _everHadBlocks;

        public void Build(LevelData level)
        {
            Clear();
            float stackSpacing = GameSettings.Instance != null ? GameSettings.Instance.BlockStackSpacing : 0.5f;

            foreach (var grid in level.Grids)
            {
                if (grid == null) continue;
                var gridGo = new GameObject("Grid");
                gridGo.transform.SetParent(transform);

                var gr = new GridRuntime
                {
                    Data = grid,
                    Root = gridGo.transform,
                    StackSpacing = stackSpacing,
                    TargetRows = Mathf.Max(1, grid.Rows),
                    Pending = BuildPendingQueue(grid),
                };

                for (int r = 0; r < grid.Rows; r++)
                {
                    var ring = new Ring();
                    int count = grid.ElementsInRow(r);
                    for (int e = 0; e < count; e++)
                    {
                        var cellData = grid.GetCell(r, e);
                        if (cellData == null || cellData.BlockStackCt <= 0) continue; // ô trống

                        var cell = CreateCell(gr, $"Cell_r{r}_e{e}", grid.CellPos(r, e), cellData);
                        ring.Cells.Add(cell);
                    }
                    gr.Rings.Add(ring);
                }
                _grids.Add(gr);
            }

            _everHadBlocks = RemainingBlocks > 0;
        }

        private static Queue<PendingBlockData> BuildPendingQueue(BlockGridData grid)
        {
            var q = new Queue<PendingBlockData>();
            if (grid.PendingRefill != null)
                foreach (var p in grid.PendingRefill)
                    if (p != null && p.BlockStackCt > 0) q.Enqueue(p);
            return q;
        }

        private BlockCell CreateCell(GridRuntime gr, string cellName, Vector3 pos, BlockCellData data)
        {
            var go = new GameObject(cellName);
            go.transform.SetParent(gr.Root);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, data.SpawnerDirectionAngleZ, 0f); // hướng dồn của cell
            var cell = go.AddComponent<BlockCell>();
            cell.Build(data, gr.StackSpacing, this);
            return cell;
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            _grids.Clear();
            _everHadBlocks = false;
        }

        private static Ring FrontRing(GridRuntime gr)
        {
            foreach (var ring in gr.Rings) if (ring.Cells.Count > 0) return ring;
            return null;
        }

        /// <summary>Cell cùng màu gần nhất ở ring ngoài cùng của mọi grid.</summary>
        public BlockCell FindTargetCell(BlockColor color, Vector3 from)
        {
            BlockCell best = null;
            float bestSqr = float.MaxValue;
            foreach (var gr in _grids)
            {
                var ring = FrontRing(gr);
                if (ring == null) continue;
                foreach (var cell in ring.Cells)
                {
                    if (cell == null || cell.Color != color) continue;
                    float d = (cell.transform.position - from).sqrMagnitude;
                    if (d < bestSqr) { bestSqr = d; best = cell; }
                }
            }
            return best;
        }

        public bool HasFrontCellOfColor(BlockColor color)
        {
            foreach (var gr in _grids)
            {
                var ring = FrontRing(gr);
                if (ring == null) continue;
                foreach (var cell in ring.Cells) if (cell != null && cell.Color == color) return true;
            }
            return false;
        }

        public void OnCellCleared(BlockCell cell)
        {
            foreach (var gr in _grids)
            {
                bool found = false;
                foreach (var ring in gr.Rings)
                {
                    int idx = ring.Cells.IndexOf(cell);
                    if (idx < 0) continue;
                    ring.Cells.RemoveAt(idx);
                    if (cell != null) Destroy(cell.gameObject);
                    found = true;
                    break;
                }
                if (found) { CollapseFront(gr); break; }
            }
            GameController.Instance?.OnBoardChanged();
        }

        // Bỏ các ring rỗng ở đầu và dồn các ring còn lại vào trong (giảm 1 bậc bán kính).
        private void CollapseFront(GridRuntime gr)
        {
            bool changed = false;
            while (gr.Rings.Count > 0 && gr.Rings[0].Cells.Count == 0)
            {
                gr.Rings.RemoveAt(0);
                changed = true;
            }
            if (!changed) return;

            for (int ri = 0; ri < gr.Rings.Count; ri++)
            {
                var ring = gr.Rings[ri];
                int count = ring.Cells.Count;
                for (int e = 0; e < count; e++)
                    ring.Cells[e].MoveTo(gr.Data.CellPosAt(ri, e, count), collapseDuration);
            }

            TryRefill(gr);
        }

        // Spawner nhả bù (~ TrySpawnBlocks/PendingBlockDataArr của PixelShoot_2): sau khi front dồn vào,
        // dựng thêm ring ở NGOÀI CÙNG từ hàng đợi, giữ số ring ~ TargetRows để tạo cảm giác băng chuyền.
        private void TryRefill(GridRuntime gr)
        {
            if (gr.Pending == null) return;

            while (gr.Pending.Count > 0 && gr.Rings.Count < gr.TargetRows)
            {
                int rowIndex = gr.Rings.Count;
                int full = Mathf.Max(1, gr.Data.ElementsInRow(rowIndex));
                int take = Mathf.Min(full, gr.Pending.Count);

                var ring = new Ring();
                for (int e = 0; e < take; e++)
                {
                    var p = gr.Pending.Dequeue();
                    if (p == null || p.BlockStackCt <= 0) continue;

                    var data = new BlockCellData
                    {
                        Color = p.Color,
                        BlockStackCt = p.BlockStackCt,
                        BlockCol = 0,
                        SpawnerDepth = rowIndex,
                    };

                    Vector3 pos = gr.Data.CellPosAt(rowIndex, e, take);
                    // Spawn hơi lệch ra ngoài theo phương bán kính rồi trượt vào cho mượt (feed).
                    Vector3 radial = pos - gr.Data.Center;
                    Vector3 dir = radial.sqrMagnitude > 1e-6f ? radial.normalized : Vector3.forward;
                    Vector3 spawnPos = gr.Data.Center + dir * (radial.magnitude + gr.Data.RowSpacing);
                    // Hướng cell refill: dồn về TÂM grid (đồng nhất với cell Generate).
                    data.SpawnerDirectionAngleZ = radial.sqrMagnitude > 1e-6f
                        ? Mathf.Repeat(Mathf.Atan2(-radial.x, -radial.z) * Mathf.Rad2Deg, 360f) : 0f;

                    var cell = CreateCell(gr, $"Cell_refill_r{rowIndex}_e{e}", spawnPos, data);
                    cell.MoveTo(pos, collapseDuration);
                    ring.Cells.Add(cell);
                }

                if (ring.Cells.Count == 0) break;
                gr.Rings.Add(ring);
            }
        }

        public int RemainingBlocks
        {
            get
            {
                int s = 0;
                foreach (var gr in _grids)
                {
                    foreach (var ring in gr.Rings)
                        foreach (var cell in ring.Cells) s += cell.StackCount;
                    // Block còn trong hàng đợi refill cũng là block "chưa clear" → tính để AllCleared đúng.
                    if (gr.Pending != null)
                        foreach (var p in gr.Pending)
                            if (p != null && p.BlockStackCt > 0) s += p.BlockStackCt;
                }
                return s;
            }
        }

        public bool AllCleared => _everHadBlocks && RemainingBlocks == 0;
    }
}
