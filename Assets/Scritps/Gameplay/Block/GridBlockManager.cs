using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Quản lý các grid block dạng vòng cung. Mỗi grid = nhiều HÀNG cung; row 0 = ngoài cùng, SÁT PATH.
    /// Gun ăn từ ngoài vào trong: cell (r,e) chỉ bắn được khi các cell CHẶN nó ở hàng r-1 đã bị phá hết
    /// (Uniform → chặn 1:1; ArcLength → cell giữa bị 2 cell chặn theo góc). Row 0 luôn bắn được.
    /// Khi row 0 sạch → các hàng sau DỒN ra sát path, spawner refill thêm hàng ở phía trong.
    /// </summary>
    public class GridBlockManager : Singleton<GridBlockManager>
    {
        [SerializeField] private float collapseDuration = 0.25f;

        private class GridRuntime
        {
            public BlockGridData Data;
            public readonly List<BlockCell[]> Rows = new List<BlockCell[]>(); // [row][index]; null = đã phá
            public Transform Root;                 // node cha để gắn cell (kể cả cell refill)
            public Queue<PendingBlockData> Pending; // hàng đợi spawner nhả thêm
            public float StackSpacing;
            public int TargetRows;                 // số hàng mong muốn — refill giữ ~ mức này
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
                    int count = grid.ElementsInRow(r);
                    var row = new BlockCell[count];
                    for (int e = 0; e < count; e++)
                    {
                        var cellData = grid.GetCell(r, e);
                        if (cellData == null || cellData.BlockStackCt <= 0) continue; // ô trống
                        row[e] = CreateCell(gr, $"Cell_r{r}_e{e}", grid.CellPos(r, e), cellData);
                    }
                    gr.Rows.Add(row);
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

        // Cell (r,e) bắn được khi MỌI cell chặn nó ở hàng trước đã bị phá. Row 0 (sát path) luôn bắn được.
        private static bool IsShootable(GridRuntime gr, int r, int e)
        {
            if (r <= 0) return true;
            var prev = gr.Rows[r - 1];
            BlockGridData.FrontIndices(gr.Rows[r].Length, prev.Length, e, out int a, out int b);
            if (a >= 0 && a < prev.Length && prev[a] != null) return false;
            if (b >= 0 && b < prev.Length && prev[b] != null) return false;
            return true;
        }

        /// <summary>
        /// Chọn cell cùng màu để bắn, trong số các cell KHÔNG BỊ CHẶN.
        /// <para><paramref name="detectRange"/> = vòng PHÁT HIỆN (tròn trên sàn XZ): chỉ áp cho cell HÀNG 0
        /// — tức lúc gun "bắt" được một cột mới ở hàng ngoài cùng sát path. Cell sâu hơn là phần TIẾP THEO
        /// của cột đã mở nên KHÔNG lọc theo tầm: đã mở cột thì ăn dứt cột đó dù gun đã chạy ra xa.</para>
        /// <para>Ưu tiên cell SÂU hơn (row lớn) để ăn dứt cột đang mở rồi mới sang cột khác; cùng độ sâu
        /// thì lấy cell gần nhất.</para>
        /// </summary>
        public BlockCell FindTargetCell(BlockColor color, Vector3 from, float detectRange)
        {
            BlockCell best = null;
            int bestRow = -1;
            float bestSqr = float.MaxValue;
            float detectSqr = detectRange * detectRange;
            foreach (var gr in _grids)
                for (int r = 0; r < gr.Rows.Count; r++)
                {
                    var row = gr.Rows[r];
                    for (int e = 0; e < row.Length; e++)
                    {
                        var cell = row[e];
                        if (cell == null || cell.Color != color) continue;
                        if (!IsShootable(gr, r, e)) continue;
                        Vector3 d = cell.transform.position - from; d.y = 0f;
                        float sqr = d.sqrMagnitude;
                        if (r == 0 && sqr > detectSqr) continue; // hàng 0: chỉ bắt cột khi lọt vòng phát hiện
                        if (r > bestRow || (r == bestRow && sqr < bestSqr))
                        { bestRow = r; bestSqr = sqr; best = cell; }
                    }
                }
            return best;
        }

        public bool HasFrontCellOfColor(BlockColor color)
        {
            foreach (var gr in _grids)
                for (int r = 0; r < gr.Rows.Count; r++)
                {
                    var row = gr.Rows[r];
                    for (int e = 0; e < row.Length; e++)
                        if (row[e] != null && row[e].Color == color && IsShootable(gr, r, e)) return true;
                }
            return false;
        }

        public void OnCellCleared(BlockCell cell)
        {
            foreach (var gr in _grids)
            {
                bool found = false;
                for (int r = 0; r < gr.Rows.Count && !found; r++)
                {
                    var row = gr.Rows[r];
                    for (int e = 0; e < row.Length; e++)
                    {
                        if (row[e] != cell) continue;
                        row[e] = null;
                        if (cell != null) Destroy(cell.gameObject);
                        found = true;
                        break;
                    }
                }
                if (found) { CollapseFront(gr); break; }
            }
            GameController.Instance?.OnBoardChanged();
        }

        // Row 0 (sát path) sạch → bỏ hàng đó và dồn các hàng sau RA sát path (giảm 1 bậc bán kính),
        // rồi spawner refill thêm hàng ở phía TRONG để giữ ~ TargetRows.
        private void CollapseFront(GridRuntime gr)
        {
            bool changed = false;
            while (gr.Rows.Count > 0 && IsRowEmpty(gr.Rows[0]))
            {
                gr.Rows.RemoveAt(0);
                changed = true;
            }
            if (!changed) return;

            for (int r = 0; r < gr.Rows.Count; r++)
            {
                var row = gr.Rows[r];
                int count = row.Length;
                for (int e = 0; e < count; e++)
                    if (row[e] != null) row[e].MoveTo(gr.Data.CellPosAt(r, e, count), collapseDuration);
            }

            TryRefill(gr);
        }

        private static bool IsRowEmpty(BlockCell[] row)
        {
            foreach (var c in row) if (c != null) return false;
            return true;
        }

        // Spawner nhả bù (~ TrySpawnBlocks/PendingBlockDataArr của PixelShoot_2): dựng thêm hàng ở phía
        // TRONG từ hàng đợi, giữ số hàng ~ TargetRows để tạo cảm giác băng chuyền đẩy ra phía path.
        private void TryRefill(GridRuntime gr)
        {
            if (gr.Pending == null) return;

            while (gr.Pending.Count > 0 && gr.Rows.Count < gr.TargetRows)
            {
                int rowIndex = gr.Rows.Count;
                int count = Mathf.Max(1, gr.Data.ElementsInRow(rowIndex));
                int take = Mathf.Min(count, gr.Pending.Count);

                var row = new BlockCell[count];
                bool any = false;
                for (int e = 0; e < take; e++)
                {
                    var p = gr.Pending.Dequeue();
                    if (p == null || p.BlockStackCt <= 0) continue;

                    Vector3 pos = gr.Data.CellPosAt(rowIndex, e, count);
                    Vector3 radial = pos - gr.Data.Center;
                    Vector3 dir = radial.sqrMagnitude > 1e-6f ? radial.normalized : Vector3.forward;

                    var data = new BlockCellData
                    {
                        Color = p.Color,
                        BlockStackCt = p.BlockStackCt,
                        BlockCol = e,
                        SpawnerDepth = rowIndex,
                        // Hướng cell refill: dồn về TÂM grid (đồng nhất với cell Generate).
                        SpawnerDirectionAngleZ = radial.sqrMagnitude > 1e-6f
                            ? Mathf.Repeat(Mathf.Atan2(-radial.x, -radial.z) * Mathf.Rad2Deg, 360f) : 0f,
                    };

                    // Spawn hơi lệch ra ngoài theo phương bán kính rồi trượt vào cho mượt (feed).
                    Vector3 spawnPos = gr.Data.Center + dir * (radial.magnitude + gr.Data.RowSpacing);
                    row[e] = CreateCell(gr, $"Cell_refill_r{rowIndex}_e{e}", spawnPos, data);
                    row[e].MoveTo(pos, collapseDuration);
                    any = true;
                }

                if (!any) break;
                gr.Rows.Add(row);
            }
        }

        public int RemainingBlocks
        {
            get
            {
                int s = 0;
                foreach (var gr in _grids)
                {
                    foreach (var row in gr.Rows)
                        foreach (var cell in row)
                            if (cell != null) s += cell.StackCount;
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
