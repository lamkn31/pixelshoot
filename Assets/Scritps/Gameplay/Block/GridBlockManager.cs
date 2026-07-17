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
        // Tốc độ dồn hàng lấy từ GameSettings (config chung), nạp lại mỗi lần Build.
        private float _collapseDuration = 0.25f;

        // Nguồn Spawner: 1 Ô CỐ ĐỊNH trên lưới. Cell ở đó dồn lên như cell thường; hễ ô trống là nhả mục kế
        // trong hàng đợi ẩn ra đúng ô đó — lặp tới khi cạn.
        private class SpawnerSource
        {
            public int Row, Col;
            public Queue<PendingBlockData> Queue;
            public float DirAngle;
        }

        private class GridRuntime
        {
            public BlockGridData Data;
            public readonly List<BlockCell[]> Rows = new List<BlockCell[]>(); // [row][index]; null = đã phá
            /// <summary>
            /// [row][index] — true = ô LỖ, do designer xoá trong level (BlockStackCt &lt;= 0).
            /// Phải tách khỏi "null vì bị bắn sạch": lỗ là VĨNH VIỄN, không cell nào được dồn hay refill
            /// vào. Chỉ nhìn Rows[r][e] == null thì 2 thứ đó giống hệt nhau và lỗ sẽ bị lấp mất.
            /// </summary>
            public readonly List<bool[]> Holes = new List<bool[]>();
            public readonly List<SpawnerSource> Sources = new List<SpawnerSource>();
            public Transform Root;                 // node cha để gắn cell (kể cả cell refill)
            public Queue<PendingBlockData> Pending; // hàng đợi refill mức GRID (lấp hàng sâu nhất)
            public float StackSpacing;
            public bool HasIndicators;             // còn mũi tên spawner nào cần dọn/gắn không
        }

        private readonly List<GridRuntime> _grids = new List<GridRuntime>();
        private bool _everHadBlocks;

        public void Build(LevelData level)
        {
            Clear();
            var gs = GameSettings.Instance;
            float globalSpacing = gs != null ? gs.BlockStackSpacing : 0.5f;
            _collapseDuration = gs != null ? gs.BlockCollapseDuration : 0.25f;

            foreach (var grid in level.Grids)
            {
                if (grid == null) continue;
                var gridGo = new GameObject("Grid");
                gridGo.transform.SetParent(transform);

                var gr = new GridRuntime
                {
                    Data = grid,
                    Root = gridGo.transform,
                    // StackSpacing riêng của grid; <= 0 thì rơi về config chung.
                    StackSpacing = grid.StackSpacing > 0f ? grid.StackSpacing : globalSpacing,
                    Pending = BuildPendingQueue(grid),
                };

                for (int r = 0; r < grid.Rows; r++)
                {
                    int count = grid.ElementsInRow(r);
                    var row = new BlockCell[count];
                    var holes = new bool[count];
                    for (int e = 0; e < count; e++)
                    {
                        var cellData = grid.GetCell(r, e);
                        // Designer xoá ô này (stack <= 0) → LỖ vĩnh viễn: không dựng cell, và về sau cũng
                        // không cho cell nào dồn/refill vào (xem AdvanceOnce, TryRefill).
                        if (cellData == null || cellData.BlockStackCt <= 0) { holes[e] = true; continue; }
                        row[e] = CreateCell(gr, $"Cell_r{r}_e{e}", grid.CellPos(r, e), cellData);

                        // Cell Spawner: ô này thành NGUỒN cố định. Bản thân cell là cell thường (vẫn dồn lên).
                        if (cellData.Type != BlockCellType.Spawner || cellData.Queue == null) continue;
                        var q = new Queue<PendingBlockData>();
                        foreach (var it in cellData.Queue)
                            if (it != null && it.BlockStackCt > 0) q.Enqueue(it);
                        if (q.Count > 0)
                            gr.Sources.Add(new SpawnerSource
                            {
                                Row = r, Col = e, Queue = q,
                                DirAngle = cellData.SpawnerDirectionAngleZ,
                            });
                    }
                    gr.Rows.Add(row);
                    gr.Holes.Add(holes);
                }
                gr.HasIndicators = gr.Sources.Count > 0; // mồi cờ trước, Refresh tự cập nhật lại sau
                RefreshSpawnerIndicators(gr);
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

        // Sinh 1 cell TỪ BlockCellPrefab (qua pool); chính cell sẽ sinh block từ BlockPrefab trong Build().
        private BlockCell CreateCell(GridRuntime gr, string cellName, Vector3 pos, BlockCellData data)
        {
            var cell = PoolManager.Instance.GetCell();
            cell.name = cellName;
            cell.transform.SetParent(gr.Root);
            cell.transform.position = pos;
            cell.transform.rotation = Quaternion.Euler(0f, CellAngle(gr, data), 0f);
            // Cell chỉ là node chứa → giữ scale 1; CellScale của grid áp thẳng lên BLOCK bên trong.
            cell.transform.localScale = Vector3.one;
            cell.Build(data, gr.StackSpacing, gr.Data.CellScale, this);
            return cell;
        }

        /// <summary>
        /// Hướng dồn/nhả của cell. Rect: tính thẳng từ grid (mọi cell chung 1 hướng) nên xoay grid là
        /// khớp ngay, data cũ chưa Generate lại cũng không lệch. Arc: lấy góc riêng đã lưu trong data —
        /// mỗi cell 1 góc, và người dùng kéo mũi tên chỉnh tay được.
        /// </summary>
        private static float CellAngle(GridRuntime gr, BlockCellData data) =>
            gr.Data.Shape == BlockGridShape.Rect
                ? gr.Data.DefaultCellAngle(0, 0)   // Rect: không phụ thuộc row/e
                : data.SpawnerDirectionAngleZ;

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
        /// Chọn cell cùng màu để bắn, trong số các cell KHÔNG BỊ CHẶN và nằm trong
        /// <paramref name="detectRange"/> (vòng phát hiện, tròn trên sàn XZ). Xét MỌI grid — gun chạy giữa
        /// 2 grid sẽ ăn được cả hai bên.
        /// <para>Thứ tự ưu tiên: cell SÂU hơn (row lớn) trước — cell sâu chỉ hở khi cell chặn nó đã sạch,
        /// nên gun ăn hàng 0 → xuống sâu dần trong cùng cột. Cùng độ sâu thì lấy cell GẦN NHẤT: vì cột trải
        /// dọc path nên gần nhất cũng chính là tuần tự theo index, không nhảy cóc.</para>
        /// <para>Bộ lọc range+góc ở đây phải khớp Gun.InDetectZone: Gun gọi lại mỗi frame để buông target
        /// khi quạt đã trôi qua, hai bên lệch nhau thì target vừa chọn xong đã bị loại ngay frame sau.</para>
        /// </summary>
        /// <summary>
        /// Cell cùng màu gần nhất ở hàng ngoài cùng còn bắn được, trong vùng bắn của MỘT nòng: bán kính
        /// <paramref name="detectRange"/>, quạt tính TỪ hướng trước mặt <paramref name="forward"/> rồi toả
        /// sang sườn của nòng đúng <paramref name="spreadAngle"/> độ.
        /// <paramref name="side"/>: +1 = nòng phải, −1 = nòng trái.
        /// <paramref name="exclude"/>: target của nòng bên kia — để 2 nòng không bắn trùng 1 cell.
        /// </summary>
        public BlockCell FindTargetCell(TypeColor color, Vector3 from, Vector3 forward, float side,
                                        float detectRange, float spreadAngle, BlockCell exclude = null)
        {
            BlockCell best = null;
            int bestRow = -1;
            float bestSqr = float.MaxValue;
            float detectSqr = detectRange * detectRange;

            // Quạt tính trên sàn XZ. Chuẩn hoá forward + dựng vector sườn 1 lần ở ngoài vòng lặp để so
            // bằng dot/cosin, khỏi gọi Vector3.Angle (Acos) cho từng cell.
            forward.y = 0f;
            bool hasDir = forward.sqrMagnitude > 1e-6f;
            if (hasDir) forward.Normalize();
            Vector3 sideVec = Vector3.Cross(Vector3.up, forward); // +X local của gun
            float sideSign = side >= 0f ? 1f : -1f;
            // Toả tối đa 180° là kín nửa mặt phẳng của nòng — quá số đó không còn ý nghĩa.
            float cosSpread = Mathf.Cos(Mathf.Clamp(spreadAngle, 0f, 180f) * Mathf.Deg2Rad);

            foreach (var gr in _grids)
                for (int r = 0; r < gr.Rows.Count; r++)
                {
                    var row = gr.Rows[r];
                    for (int e = 0; e < row.Length; e++)
                    {
                        var cell = row[e];
                        if (cell == null || cell.Color != color) continue;
                        if (cell == exclude) continue;  // nòng bên kia đang bắn cell này → không bắn trùng
                        if (cell.PendingEntry) continue; // đang TRƯỢT (nhả mới / dồn hàng) → chưa cho ngắm
                        if (!IsShootable(gr, r, e)) continue;
                        Vector3 d = cell.transform.position - from; d.y = 0f;
                        float sqr = d.sqrMagnitude;
                        if (sqr > detectSqr) continue;
                        // Trong quạt của nòng ⇔ ĐÚNG SƯỜN (dot với vector sườn cùng dấu) VÀ lệch khỏi
                        // hướng trước mặt không quá spreadAngle (dot(forward, d̂) >= cos spread).
                        // sqr>eps để cell trùng vị trí gun không chia cho 0 (luôn coi là trong quạt).
                        if (hasDir && sqr > 1e-6f)
                        {
                            if (Vector3.Dot(sideVec, d) * sideSign < 0f) continue;
                            if (Vector3.Dot(forward, d) < cosSpread * Mathf.Sqrt(sqr)) continue;
                        }
                        if (r > bestRow || (r == bestRow && sqr < bestSqr))
                        { bestRow = r; bestSqr = sqr; best = cell; }
                    }
                }
            return best;
        }

        public bool HasFrontCellOfColor(TypeColor color)
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
                        if (cell != null) cell.Despawn(); // trả cell về pool (thay cho Destroy)
                        found = true;
                        break;
                    }
                }
                if (found) { CollapseFront(gr); break; }
            }
            GameController.Instance?.OnBoardChanged();
        }

        // Dồn cell về phía path sau khi 1 cell bị phá, rồi spawner refill ở phía TRONG.
        // Cột thẳng (Rect / Arc-Uniform) → dồn theo CỘT: cell phía sau tiến lên lấp chỗ trống ngay,
        // không phải chờ cả hàng 0 sạch (gun ăn theo cột nên hàng 0 gần như không bao giờ sạch).
        // Arc-ArcLength → cột lệch (4/5/6) nên chỉ dồn được theo HÀNG như cũ.
        private void CollapseFront(GridRuntime gr)
        {
            // Lặp: dồn lên → nguồn Spawner nhả → refill mức grid → dồn tiếp… tới khi board ổn định.
            // Không lặp thì cell vừa nhả nằm lì ở ô gốc (tận trong sâu) thay vì trôi lên đầu.
            for (int guard = 0; guard < 64; guard++)
            {
                bool moved = AdvanceOnce(gr);
                bool fed = FeedSources(gr) | TryRefill(gr);
                if (!moved && !fed) break;
            }
            // Số hàng giữ NGUYÊN (không xoá hàng rỗng) để Row của SpawnerSource luôn trỏ đúng ô.
            RefreshSpawnerIndicators(gr);
        }

        // Cell nằm ở Ô GỐC của Spawner đổi liên tục (cell cũ dồn đi, cell mới nhả ra) → mỗi lần dồn xong
        // phải gắn lại dấu hiệu cho đúng cell đang đứng ở ô đó.
        private static void RefreshSpawnerIndicators(GridRuntime gr)
        {
            // Cờ thay cho "Sources.Count == 0 thì return": thoát sớm theo Sources là SAI — lúc nguồn cuối
            // bị gỡ chính là lúc phải dọn mũi tên còn sót, thoát ra thì nó bật vĩnh viễn. Cờ chỉ để grid
            // không (còn) spawner nào khỏi quét lại toàn bộ cell mỗi lần dồn hàng.
            if (!gr.HasIndicators) return;

            foreach (var row in gr.Rows)
                foreach (var c in row)
                    if (c != null) c.ShowSpawnerIndicator(false);

            bool any = false;
            foreach (var src in gr.Sources)
            {
                // Hàng đợi đã CẠN → cell đang đứng ở ô gốc là cell CUỐI, sau nó không còn gì nhả ra nữa
                // → thôi vẽ mũi tên spawner cho nó.
                if (src.Queue.Count == 0) continue;
                if (src.Row >= gr.Rows.Count) continue;
                var row = gr.Rows[src.Row];
                if (src.Col >= row.Length || row[src.Col] == null) continue;

                float ang = gr.Data.Shape == BlockGridShape.Rect
                    ? gr.Data.DefaultCellAngle(0, 0) : src.DirAngle;
                row[src.Col].ShowSpawnerIndicator(true, ang);
                any = true;
            }

            // Hàng đợi chỉ vơi đi chứ không đầy lại → hết mũi tên là hết hẳn, lần sau khỏi quét.
            gr.HasIndicators = any;
        }

        /// <summary>Ô (row, col) có nhận cell dồn/refill vào được không: phải trong mảng, đang trống, và
        /// KHÔNG phải lỗ designer xoá.</summary>
        private static bool CanEnter(GridRuntime gr, int row, int col, BlockCell[] rowCells)
        {
            if (col < 0 || col >= rowCells.Length || rowCells[col] != null) return false;
            return !IsHole(gr, row, col);
        }

        private static bool IsHole(GridRuntime gr, int row, int col) =>
            row >= 0 && row < gr.Holes.Count && col >= 0 && col < gr.Holes[row].Length && gr.Holes[row][col];

        // Dồn 1 bước: cell ở hàng r tiến lên ô CHẶN nó ở hàng r-1 nếu ô đó trống — dùng đúng map góc của
        // FrontIndices. Cột thẳng (Rect / Arc-Uniform) → 1:1. Cột lệch (Arc-ArcLength) → cell giữa có 2 ô
        // để dồn lên (vd index 1 của hàng 5 dồn được lên index 0 hoặc 1 của hàng 4); 2 ô ngoài cùng khớp 1:1.
        private bool AdvanceOnce(GridRuntime gr)
        {
            bool moved = false;
            for (int r = 1; r < gr.Rows.Count; r++)
            {
                var cur = gr.Rows[r];
                var prev = gr.Rows[r - 1];
                for (int e = 0; e < cur.Length; e++)
                {
                    var cell = cur[e];
                    if (cell == null) continue;

                    BlockGridData.FrontIndices(cur.Length, prev.Length, e, out int a, out int b);
                    // Lỗ do designer xoá KHÔNG phải chỗ trống để dồn vào — grid 3x3 xoá ô (0,0) thì cột 0
                    // dừng ở hàng 1, không bao giờ lấp kín hàng 0.
                    int slot = -1;
                    if (CanEnter(gr, r - 1, a, prev)) slot = a;
                    else if (CanEnter(gr, r - 1, b, prev)) slot = b;
                    if (slot < 0) continue; // ô trước còn cell / là lỗ → bị chặn, chưa dồn được

                    prev[slot] = cell;
                    cur[e] = null;
                    cell.SetColumn(slot);
                    // Không gỡ PendingEntry ở đây: transform còn đang trượt. MoveTo tự gỡ khi tới nơi.
                    cell.MoveTo(gr.Data.CellPosAt(r - 1, slot, prev.Length), _collapseDuration);
                    moved = true;
                }
            }
            return moved;
        }

        // Ô GỐC của Spawner: hễ trống (cell cũ vừa dồn lên) thì nhả mục kế trong hàng đợi ẩn ra ĐÚNG ô đó.
        // Cell nhả ra là cell thường → lần dồn sau lại trôi lên, ô gốc lại trống → nhả tiếp, cho tới khi cạn.
        private bool FeedSources(GridRuntime gr)
        {
            if (gr.Sources.Count == 0 || gr.Rows.Count == 0) return false;
            int cols = gr.Rows[0].Length;
            bool fed = false;

            for (int i = gr.Sources.Count - 1; i >= 0; i--)
            {
                var src = gr.Sources[i];
                if (src.Row >= gr.Rows.Count || src.Col >= cols) { gr.Sources.RemoveAt(i); continue; }
                if (gr.Rows[src.Row][src.Col] != null) continue;              // ô gốc chưa trống
                if (src.Queue.Count == 0) { gr.Sources.RemoveAt(i); continue; } // cạn → bỏ nguồn

                var p = src.Queue.Dequeue();
                if (p == null || p.BlockStackCt <= 0) continue;
                fed = true;

                var data = new BlockCellData
                {
                    Color = p.Color,
                    BlockStackCt = p.BlockStackCt,
                    BlockCol = src.Col,
                    SpawnerDepth = src.Row,
                    SpawnerDirectionAngleZ = src.DirAngle,
                };
                Vector3 pos = gr.Data.CellPosAt(src.Row, src.Col, cols);
                // Xuất phát từ ô sâu hơn 1 bậc rồi trượt vào → nhìn rõ là được đẩy ra.
                Vector3 spawnPos = gr.Data.CellPosAt(src.Row + 1, src.Col, cols);
                var cell = CreateCell(gr, $"Cell_spawn_r{src.Row}_e{src.Col}", spawnPos, data);
                cell.MoveTo(pos, _collapseDuration); // MoveTo tự khoá ngắm tới khi cell trượt xong
                gr.Rows[src.Row][src.Col] = cell;
            }
            return fed;
        }

        // Refill mức GRID (hàng đợi chung của grid): nhả vào các ô trống ở hàng SÂU NHẤT, rồi để AdvanceOnce
        // đẩy dần lên. Số hàng giữ nguyên nên không dựng thêm hàng như trước.
        private bool TryRefill(GridRuntime gr)
        {
            if (gr.Pending == null || gr.Pending.Count == 0 || gr.Rows.Count == 0) return false;

            int rowIndex = gr.Rows.Count - 1;
            var row = gr.Rows[rowIndex];
            int count = row.Length;
            bool fed = false;

            for (int e = 0; e < count && gr.Pending.Count > 0; e++)
            {
                if (row[e] != null || IsHole(gr, rowIndex, e)) continue; // lỗ designer xoá → không lấp
                var p = gr.Pending.Dequeue();
                if (p == null || p.BlockStackCt <= 0) continue;

                Vector3 pos = gr.Data.CellPosAt(rowIndex, e, count);
                // Xuất phát từ ĐÚNG vị trí hàng sâu hơn 1 bậc rồi trượt vào — hợp cả Arc lẫn Rect.
                Vector3 spawnPos = gr.Data.CellPosAt(rowIndex + 1, e, count);

                var data = new BlockCellData
                {
                    Color = p.Color,
                    BlockStackCt = p.BlockStackCt,
                    BlockCol = e,
                    SpawnerDepth = rowIndex,
                    SpawnerDirectionAngleZ = gr.Data.DefaultCellAngle(rowIndex, e),
                };

                row[e] = CreateCell(gr, $"Cell_refill_r{rowIndex}_e{e}", spawnPos, data);
                row[e].MoveTo(pos, _collapseDuration); // MoveTo tự khoá ngắm tới khi cell trượt xong
                fed = true;
            }
            return fed;
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
                    // Block chưa nhả ra (hàng đợi ẩn của Spawner + refill mức grid) vẫn là block "chưa clear".
                    foreach (var src in gr.Sources)
                        foreach (var p in src.Queue)
                            if (p != null && p.BlockStackCt > 0) s += p.BlockStackCt;
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
