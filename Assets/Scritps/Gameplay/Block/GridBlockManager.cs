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
        // Config lấy từ GameSettings, nạp lại mỗi lần Build.
        private float _collapseDuration = 0.25f;
        private float _stackSpacing = 0.5f;   // khoảng cách block trong stack (dùng chung, không theo grid)
        private bool _frontRowFirst; // CoreType = FrontRowFirst → ưu tiên cell hàng 0 hơn cell sập xuống

        // Nguồn Spawner: 1 Ô CỐ ĐỊNH trên lưới. Cell ở đó dồn lên như cell thường; hễ ô trống là nhả mục kế
        // trong hàng đợi ẩn ra đúng ô đó — lặp tới khi cạn.
        private class SpawnerSource
        {
            public int Row, Col;
            public Queue<PendingBlockData> Queue;
            public float DirAngle;
            public bool EightWay; // Spawner8: nhả thêm vào ô trống ở 8 ô quanh ô gốc
        }

        // Thứ tự nhả của Spawner8 quanh ô gốc: 4 ô dọc/ngang trước, 4 ô chéo sau
        // (dr = lệch hàng: −1 tiến về path; dc = lệch cột).
        private static readonly int[,] EightNeighbors =
        {
            { -1, 0 }, { 1, 0 }, { 0, -1 }, { 0, 1 },     // dọc/ngang
            { -1, -1 }, { -1, 1 }, { 1, -1 }, { 1, 1 },   // chéo
        };

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
            /// <summary>
            /// [row][index] — true = ô GỐC của 1 Spawner8. Giữ CỐ ĐỊNH cả sau khi spawner đó nhả hết &amp;
            /// biến mất: 2 spawner KHÔNG được nhả cell vào ô của nhau, kể cả khi ô đó đã hết queue (yêu cầu).
            /// </summary>
            public readonly List<bool[]> SpawnerCells = new List<bool[]>();
            public readonly List<SpawnerSource> Sources = new List<SpawnerSource>();
            public Transform Root;                 // node cha để gắn cell (kể cả cell refill)
            public Queue<PendingBlockData> Pending; // hàng đợi refill mức GRID (lấp hàng sâu nhất)
            public bool HasIndicators;             // còn mũi tên spawner nào cần dọn/gắn không
        }

        private readonly List<GridRuntime> _grids = new List<GridRuntime>();
        private bool _everHadBlocks;

        public void Build(LevelData level)
        {
            Clear();
            var gs = GameSettings.Instance;
            _stackSpacing = gs != null ? gs.BlockStackSpacing : 0.5f;
            _collapseDuration = gs != null ? gs.BlockCollapseDuration : 0.25f;
            _frontRowFirst = gs != null && gs.CoreType == CoreGameType.FrontRowFirst;

            foreach (var grid in level.Grids)
            {
                if (grid == null) continue;
                var gridGo = new GameObject("Grid");
                gridGo.transform.SetParent(transform);

                var gr = new GridRuntime
                {
                    Data = grid,
                    Root = gridGo.transform,
                    Pending = BuildPendingQueue(grid),
                };

                for (int r = 0; r < grid.Rows; r++)
                {
                    int count = grid.ElementsInRow(r);
                    var row = new BlockCell[count];
                    var holes = new bool[count];
                    var spawners = new bool[count];
                    for (int e = 0; e < count; e++)
                    {
                        var cellData = grid.GetCell(r, e);
                        // Designer xoá ô này (stack <= 0) → LỖ vĩnh viễn: không dựng cell, và về sau cũng
                        // không cho cell nào dồn/refill vào (xem AdvanceOnce, TryRefill).
                        if (cellData == null || cellData.BlockStackCt <= 0) { holes[e] = true; continue; }
                        row[e] = CreateCell(gr, $"Cell_r{r}_e{e}", grid.CellPos(r, e), cellData);

                        if (!cellData.Type.IsSpawner()) continue;
                        bool eight = cellData.Type == BlockCellType.Spawner8;
                        var q = new Queue<PendingBlockData>();
                        // Spawner8 = conveyor: MÀU HIỆN TẠI (của chính ô gốc) là ĐẦU sequence → nhả ra
                        // trước, rồi mới tới các màu trong Queue (yêu cầu). Spawner thường giữ nguyên: ô gốc
                        // là cell thường, Queue chỉ là các mục refill phía sau.
                        if (eight)
                        {
                            q.Enqueue(new PendingBlockData { Color = cellData.Color, BlockStackCt = cellData.BlockStackCt });
                            spawners[e] = true; // ô gốc Spawner8: cấm spawner khác nhả vào (giữ cố định)
                        }
                        if (cellData.Queue != null)
                            foreach (var it in cellData.Queue)
                                if (it != null && it.BlockStackCt > 0) q.Enqueue(it);

                        if (q.Count > 0)
                            gr.Sources.Add(new SpawnerSource
                            {
                                Row = r, Col = e, Queue = q,
                                DirAngle = cellData.SpawnerDirectionAngleZ,
                                EightWay = eight,
                            });
                    }
                    gr.Rows.Add(row);
                    gr.Holes.Add(holes);
                    gr.SpawnerCells.Add(spawners);
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
            cell.Build(data, _stackSpacing, gr.Data.CellScale, this);
            cell.SetMultiSide(gr.Data.ShootableEdges != GridEdges.None);
            return cell;
        }

        /// <summary>
        /// Hướng dồn/nhả của cell. Rect: tính thẳng từ grid (mọi cell chung 1 hướng) nên xoay grid là
        /// khớp ngay, data cũ chưa Generate lại cũng không lệch. Arc: lấy góc riêng đã lưu trong data —
        /// mỗi cell 1 góc, và người dùng kéo mũi tên chỉnh tay được.
        /// </summary>
        private static float CellAngle(GridRuntime gr, BlockCellData data) =>
            gr.Data.CellAngleFromShape
                // Rect không phụ thuộc row/e, nhưng Spline thì MỖI cell 1 pháp tuyến → phải truyền đúng
                // ô của nó (SpawnerDepth = row, BlockCol = index trong hàng).
                ? gr.Data.DefaultCellAngle(data.SpawnerDepth, data.BlockCol)
                : data.SpawnerDirectionAngleZ;

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            _grids.Clear();
            _everHadBlocks = false;
        }

        // Mặc định cũ: cell (r,e) bắn được khi MỌI cell chặn nó ở hàng trước đã bị phá. Row 0 luôn bắn được.
        private static bool IsShootableLegacy(GridRuntime gr, int r, int e)
        {
            if (r <= 0) return true;
            var prev = gr.Rows[r - 1];
            BlockGridData.FrontIndices(gr.Rows[r].Length, prev.Length, e, out int a, out int b);
            if (a >= 0 && a < prev.Length && prev[a] != null) return false;
            if (b >= 0 && b < prev.Length && prev[b] != null) return false;
            return true;
        }

        // Cell (r,e) có bắn được KHÔNG XÉT vị trí gun — dùng để hỏi "còn cell màu này bắn được không".
        // Grid bật ShootableEdges: lộ ra ở BẤT KỲ cạnh nào đang bật. Cell Indestructible không bao giờ bị ngắm.
        private static bool IsShootable(GridRuntime gr, int r, int e)
        {
            var cell = gr.Rows[r][e];
            if (cell != null && cell.Indestructible) return false;

            var edges = gr.Data.ShootableEdges;
            if (edges == GridEdges.None) return IsShootableLegacy(gr, r, e);

            if ((edges & GridEdges.Front) != 0 && ExposedAlongRows(gr, r, e, -1)) return true;
            if ((edges & GridEdges.Back)  != 0 && ExposedAlongRows(gr, r, e, +1)) return true;
            if ((edges & GridEdges.Left)  != 0 && ExposedAlongRow(gr, r, e, -1)) return true;
            if ((edges & GridEdges.Right) != 0 && ExposedAlongRow(gr, r, e, +1)) return true;
            return false;
        }

        // Như IsShootable nhưng CÓ xét vị trí gun: với grid nhiều mặt, chỉ bắn được cell lộ ra ở cạnh mà
        // gun đang đứng về PHÍA NGOÀI cạnh đó — nhờ vậy gun đi 1 bên chỉ ăn mặt gần, KHÔNG xuyên qua grid
        // bắn luôn mặt bên kia. (Grid mặc định 1 chiều thì vị trí gun không đổi kết quả.)
        private static bool IsShootableFromGun(GridRuntime gr, int r, int e, Vector3 from)
        {
            var cell = gr.Rows[r][e];
            if (cell != null && cell.Indestructible) return false;

            var edges = gr.Data.ShootableEdges;
            if (edges == GridEdges.None) return IsShootableLegacy(gr, r, e);

            Vector3 cellPos = cell != null ? cell.transform.position : gr.Data.CellPos(r, e);
            Vector3 fwd = gr.Data.Forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 1e-6f ? Vector3.forward : fwd.normalized;
            Vector3 rgt = Vector3.Cross(Vector3.up, fwd);   // local +X trên sàn
            Vector3 toGun = from - cellPos; toGun.y = 0f;   // hướng từ cell ra gun

            // Lộ ra ở cạnh đang bật VÀ gun nằm về phía NGOÀI cạnh đó (dot với pháp tuyến ngoài > 0).
            if ((edges & GridEdges.Front) != 0 && ExposedAlongRows(gr, r, e, -1) && Vector3.Dot(toGun, -fwd) > 0f) return true;
            if ((edges & GridEdges.Back)  != 0 && ExposedAlongRows(gr, r, e, +1) && Vector3.Dot(toGun,  fwd) > 0f) return true;
            if ((edges & GridEdges.Left)  != 0 && ExposedAlongRow(gr, r, e, -1) && Vector3.Dot(toGun, -rgt) > 0f) return true;
            if ((edges & GridEdges.Right) != 0 && ExposedAlongRow(gr, r, e, +1) && Vector3.Dot(toGun,  rgt) > 0f) return true;
            return false;
        }

        // Lộ ra theo CỘT (Front/Back): mọi cell cùng index ở các hàng giữa (r, cạnh) đã trống chưa.
        // dir = -1 quét về hàng 0 (Front), +1 quét về hàng cuối (Back). Index kẹp theo độ dài hàng để
        // hợp cả grid cột lệch (Arc-ArcLength) lẫn cột thẳng (Rect/Uniform).
        private static bool ExposedAlongRows(GridRuntime gr, int r, int e, int dir)
        {
            for (int rr = r + dir; rr >= 0 && rr < gr.Rows.Count; rr += dir)
            {
                int idx = Mathf.Min(e, gr.Rows[rr].Length - 1);
                if (idx >= 0 && gr.Rows[rr][idx] != null) return false;
            }
            return true;
        }

        // Lộ ra theo HÀNG (Left/Right): mọi cell giữa (r,e) và mép hàng đã trống chưa.
        // dir = -1 quét về index 0 (Left), +1 quét về index cuối (Right).
        private static bool ExposedAlongRow(GridRuntime gr, int r, int e, int dir)
        {
            var row = gr.Rows[r];
            for (int i = e + dir; i >= 0 && i < row.Length; i += dir)
                if (row[i] != null) return false;
            return true;
        }

        /// <summary>
        /// Chọn cell cùng màu để bắn, trong số các cell KHÔNG BỊ CHẶN và nằm trong
        /// <paramref name="detectRange"/> (vòng phát hiện, tròn trên sàn XZ). Xét MỌI grid — gun chạy giữa
        /// 2 grid sẽ ăn được cả hai bên.
        /// <para>Thứ tự ưu tiên theo <see cref="GameSettings.CoreType"/>: NearestCell = cell GẦN gun nhất
        /// (khoảng cách XZ) — ăn từ gần ra xa, bền với sai số IsShootable (cell sâu lỡ hở vẫn ở xa nên
        /// không bị chọn). FrontRowFirst = cell có Depth GỐC nhỏ thắng trước (Depth 0 = sinh ở hàng 0, chưa
        /// bị bắn), cùng Depth mới xét gần nhất → cell front gốc luôn hơn cell sập xuống (Depth ≥1) dù cell
        /// sập đã dồn ra ngang hàng 0 và ở gần hơn.</para>
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
            int bestDepth = int.MaxValue;   // chỉ dùng khi _frontRowFirst; theo Depth GỐC, không phải row runtime
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
            {
                // Grid gán CỨNG 1 bên → chỉ nòng cùng bên bắn được; grid của nòng bên kia thì bỏ nguyên.
                // Khi đã khớp bên thì KHÔNG kiểm tra sườn hình học nữa (nó lật dấu khi path cong / grid chếch).
                var gside = gr.Data.Side;
                bool sideLocked = gside != GridSide.Any;
                if (sideLocked && (gside == GridSide.Right) != (sideSign > 0f)) continue;

                for (int r = 0; r < gr.Rows.Count; r++)
                {
                    var row = gr.Rows[r];
                    for (int e = 0; e < row.Length; e++)
                    {
                        var cell = row[e];
                        if (cell == null || cell.Color != color) continue;
                        if (cell.Indestructible) continue; // Spawner8 ở giữa: không bao giờ bị ngắm
                        if (cell == exclude) continue;  // nòng bên kia đang bắn cell này → không bắn trùng
                        if (cell.PendingEntry) continue; // đang TRƯỢT (nhả mới / dồn hàng) → chưa cho ngắm
                        if (!IsShootableFromGun(gr, r, e, from)) continue;
                        Vector3 d = cell.transform.position - from; d.y = 0f;
                        float sqr = d.sqrMagnitude;
                        if (sqr > detectSqr) continue;
                        // Trong quạt của nòng ⇔ ĐÚNG SƯỜN (dot với vector sườn cùng dấu) VÀ lệch khỏi
                        // hướng trước mặt không quá spreadAngle (dot(forward, d̂) >= cos spread).
                        // sqr>eps để cell trùng vị trí gun không chia cho 0 (luôn coi là trong quạt).
                        // Grid gán bên → bỏ kiểm tra SƯỜN (đã chốt theo Side), nhưng GIỮ quạt trước mặt để
                        // không bắn giật lùi vào grid đã đi qua.
                        if (hasDir && sqr > 1e-6f)
                        {
                            if (!sideLocked && Vector3.Dot(sideVec, d) * sideSign < 0f) continue;
                            if (Vector3.Dot(forward, d) < cosSpread * Mathf.Sqrt(sqr)) continue;
                        }
                        // NearestCell: gần nhất thắng. FrontRowFirst: cell có Depth GỐC nhỏ thắng trước
                        // (cell sinh ở hàng 0 = Depth 0, chưa bị bắn), cùng Depth mới xét gần nhất. Dùng
                        // Depth chứ KHÔNG dùng row runtime: cell sập xuống dồn ra hàng 0 vẫn giữ Depth cũ
                        // (≥1) nên luôn thua cell front gốc dù đã ngang hàng row 0 và ở gần hơn.
                        int depth = cell.Depth;
                        bool better = _frontRowFirst
                            ? (depth < bestDepth || (depth == bestDepth && sqr < bestSqr))
                            : (sqr < bestSqr);
                        if (better) { bestDepth = depth; bestSqr = sqr; best = cell; }
                    }
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
            // Grid bị path bao nhiều mặt (ShootableEdges bật): KHÔNG dồn trượt 1 chiều. Thay vào đó block
            // DỒN LAN TỎA hướng RA NGOÀI từ Spawner8 — ô trống ở xa được lấp bằng cell gần spawner hơn dịch
            // ra, cứ thế lan vào tới ô sát spawner, và spawner nhả bù ô trong cùng. Nhờ vậy bắn cell ở đâu
            // trong grid thì cả grid vẫn dồn kín, không chỉ 8 ô kề spawner. Grid mặc định (1 chiều) giữ nguyên.
            bool multiSide = gr.Data.ShootableEdges != GridEdges.None;

            // Lặp: dồn (lan tỏa hoặc 1 chiều) → spawner nhả → refill → dồn tiếp… tới khi board ổn định.
            for (int guard = 0; guard < 64; guard++)
            {
                bool moved = multiSide ? CascadeTowardSpawners(gr) : AdvanceOnce(gr);
                bool fed = FeedSources(gr) | (!multiSide && TryRefill(gr));
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

                float ang = gr.Data.CellAngleFromShape
                    ? gr.Data.DefaultCellAngle(src.Row, src.Col) : src.DirAngle;
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
                    if (cell.Indestructible) continue; // nguồn Spawner8 đứng yên, không dồn lên

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

        // Dồn LAN TỎA RA NGOÀI từ Spawner8: mỗi ô trống (không phải lỗ / ô spawner) được lấp bằng cell ở
        // 8-neighbor GẦN spawner hơn nó — cell đó dịch RA lấp chỗ, để lại chỗ trống gần spawner hơn, cứ thế
        // lan vào. Ô sát spawner (không còn neighbor nào gần hơn) do FeedSources nhả bù. Lặp qua guard-loop
        // của CollapseFront cho tới khi kín. Khoảng cách = Chebyshev (8 hướng) tới Spawner8 GẦN nhất.
        private bool CascadeTowardSpawners(GridRuntime gr)
        {
            bool anySpawner = false;
            foreach (var s in gr.Sources) if (s.EightWay) { anySpawner = true; break; }
            if (!anySpawner) return false;

            bool moved = false;
            for (int r = 0; r < gr.Rows.Count; r++)
            {
                var row = gr.Rows[r];
                for (int c = 0; c < row.Length; c++)
                {
                    if (row[c] != null) continue;                             // đã có cell
                    if (IsHole(gr, r, c) || IsSpawnerCell(gr, r, c)) continue; // lỗ / ô spawner: không lấp
                    int dE = DistToNearestSpawner(gr, r, c);
                    if (dE <= 0) continue;

                    // Chọn neighbor có cell di chuyển được và GẦN spawner hơn ô này (ưu tiên dọc/ngang trước
                    // chéo nhờ thứ tự EightNeighbors + so sánh chặt "<").
                    int bestD = dE, br = -1, bc = -1;
                    for (int k = 0; k < EightNeighbors.GetLength(0); k++)
                    {
                        int nr = r + EightNeighbors[k, 0];
                        if (nr < 0 || nr >= gr.Rows.Count) continue;
                        int nc = NeighborCol(gr, c + EightNeighbors[k, 1], gr.Rows[nr].Length);
                        if (nc < 0 || nc >= gr.Rows[nr].Length) continue;
                        var ncell = gr.Rows[nr][nc];
                        if (ncell == null || ncell.Indestructible) continue;   // trống / là spawner: không kéo
                        if (IsSpawnerCell(gr, nr, nc)) continue;
                        int dN = DistToNearestSpawner(gr, nr, nc);
                        if (dN >= 0 && dN < bestD) { bestD = dN; br = nr; bc = nc; }
                    }
                    if (br < 0) continue;

                    var cell = gr.Rows[br][bc];
                    gr.Rows[br][bc] = null;
                    row[c] = cell;
                    cell.SetColumn(c);
                    cell.MoveTo(gr.Data.CellPosAt(r, c, row.Length), _collapseDuration);
                    moved = true;
                }
            }
            return moved;
        }

        // Khoảng cách Chebyshev (8 hướng) tới ô gốc Spawner8 ĐANG hoạt động gần nhất; -1 nếu không còn cái nào.
        private static int DistToNearestSpawner(GridRuntime gr, int r, int c)
        {
            int best = int.MaxValue;
            foreach (var src in gr.Sources)
            {
                if (!src.EightWay) continue;
                int d = Mathf.Max(Mathf.Abs(src.Row - r), Mathf.Abs(src.Col - c));
                if (d < best) best = d;
            }
            return best == int.MaxValue ? -1 : best;
        }

        private bool FeedSources(GridRuntime gr)
        {
            if (gr.Sources.Count == 0 || gr.Rows.Count == 0) return false;
            bool fed = false;

            // Spawner THƯỜNG (1 hướng): ô gốc trống (cell cũ vừa dồn lên) thì nhả mục kế ra đúng ô đó.
            for (int i = gr.Sources.Count - 1; i >= 0; i--)
            {
                var src = gr.Sources[i];
                if (src.EightWay) continue;
                if (src.Row >= gr.Rows.Count || src.Col >= gr.Rows[src.Row].Length || src.Queue.Count == 0)
                { gr.Sources.RemoveAt(i); continue; }
                int len = gr.Rows[src.Row].Length;
                if (gr.Rows[src.Row][src.Col] == null)
                    fed |= SpawnFromQueue(gr, src.Queue, src.Row, src.Col,
                                          gr.Data.CellPosAt(src.Row + 1, src.Col, len), src.DirAngle);
            }

            // Spawner8: 3 LƯỢT ưu tiên — DỌC trước, rồi NGANG, rồi CHÉO. Nhờ vậy 1 ô trống giáp nhiều
            // spawner sẽ được spawner ở hướng ưu tiên cao hơn nhả trước; spawner hướng thấp hơn thấy ô đã
            // đầy thì thôi (yêu cầu). Mỗi phát dùng MÀU HIỆN TẠI (đầu Queue) rồi mới tới màu kế.
            fed |= FeedEightWay(gr, tier: 0); // dọc
            fed |= FeedEightWay(gr, tier: 1); // ngang
            fed |= FeedEightWay(gr, tier: 2); // chéo
            return fed;
        }

        // 1 lượt nhả của mọi Spawner8 theo tier hướng: 0 = dọc (dr≠0,dc=0), 1 = ngang (dr=0,dc≠0), 2 = chéo.
        private bool FeedEightWay(GridRuntime gr, int tier)
        {
            bool fed = false;
            for (int i = gr.Sources.Count - 1; i >= 0; i--)
            {
                var src = gr.Sources[i];
                if (!src.EightWay) continue;
                if (src.Row >= gr.Rows.Count || src.Col >= gr.Rows[src.Row].Length || src.Queue.Count == 0)
                { RemoveEightWaySource(gr, i); continue; }

                int len = gr.Rows[src.Row].Length;
                Vector3 origin = gr.Data.CellPosAt(src.Row, src.Col, len);
                for (int k = 0; k < EightNeighbors.GetLength(0) && src.Queue.Count > 0; k++)
                {
                    int dr = EightNeighbors[k, 0], dc = EightNeighbors[k, 1];
                    int t = dr != 0 && dc != 0 ? 2 : dc == 0 ? 0 : 1; // dọc / ngang / chéo
                    if (t != tier) continue;                          // lượt này chỉ 1 tier hướng

                    int r = src.Row + dr;
                    if (r < 0 || r >= gr.Rows.Count) continue;
                    int c = NeighborCol(gr, src.Col + dc, gr.Rows[r].Length);
                    if (IsSpawnerCell(gr, r, c)) continue;           // chừa ô của spawner khác (yêu cầu)
                    if (!CanEnter(gr, r, c, gr.Rows[r])) continue;

                    // Nhả MÀU HIỆN TẠI ra ô kề (cell mới trượt từ ô gốc ra), rồi ô gốc chuyển sang màu kế.
                    if (EmitHead(gr, src, r, c, origin)) { fed = true; UpdateEightWayDisplay(gr, src); }
                }
                if (src.Queue.Count == 0) RemoveEightWaySource(gr, i);
            }
            return fed;
        }

        // Nhả màu ĐẦU Queue (màu ô gốc đang hiển thị) ra ô (r,c): dựng cell ở ô gốc rồi trượt sang, và
        // DEQUEUE đầu. Bỏ qua mục rác (stack ≤ 0). false = Queue không còn mục hợp lệ.
        private bool EmitHead(GridRuntime gr, SpawnerSource src, int r, int c, Vector3 spawnPos)
        {
            PendingBlockData head = null;
            while (src.Queue.Count > 0 && head == null)
            {
                head = src.Queue.Peek();
                if (head == null || head.BlockStackCt <= 0) { src.Queue.Dequeue(); head = null; }
            }
            if (head == null) return false;

            var data = new BlockCellData
            {
                Color = head.Color,
                BlockStackCt = head.BlockStackCt,
                BlockCol = c,
                SpawnerDepth = r,
                SpawnerDirectionAngleZ = gr.Data.DefaultCellAngle(r, c),
            };
            var row = gr.Rows[r];
            var cell = CreateCell(gr, $"Cell_spawn_r{r}_e{c}", spawnPos, data);
            cell.MoveTo(gr.Data.CellPosAt(r, c, row.Length), _collapseDuration); // tự khoá ngắm khi trượt
            row[c] = cell;
            src.Queue.Dequeue(); // đã nhả đầu ra ngoài
            return true;
        }

        // Sau khi nhả đầu Queue: ô gốc chuyển sang hiển thị màu kế (đầu Queue mới), hoặc despawn nếu đã cạn.
        private void UpdateEightWayDisplay(GridRuntime gr, SpawnerSource src)
        {
            var cell = gr.Rows[src.Row][src.Col];
            if (src.Queue.Count == 0)
            {
                if (cell != null) { cell.Despawn(); gr.Rows[src.Row][src.Col] = null; }
                return;
            }
            if (cell == null) return;
            var head = src.Queue.Peek();
            var data = new BlockCellData
            {
                Color = head.Color,
                BlockStackCt = head.BlockStackCt,
                BlockCol = src.Col,
                SpawnerDepth = src.Row,
                SpawnerDirectionAngleZ = src.DirAngle,
                Type = BlockCellType.Spawner8, // vẫn là nguồn bất tử, đứng yên
            };
            cell.Build(data, _stackSpacing, gr.Data.CellScale, this);
            cell.SetMultiSide(gr.Data.ShootableEdges != GridEdges.None);
        }

        // Gỡ 1 nguồn Spawner8 đã cạn: despawn ô gốc bất tử nếu còn (thành ô trống — vẫn là SpawnerCell nên
        // spawner khác không lấp vào).
        private void RemoveEightWaySource(GridRuntime gr, int i)
        {
            var src = gr.Sources[i];
            if (src.Row < gr.Rows.Count && src.Col < gr.Rows[src.Row].Length)
            {
                var cell = gr.Rows[src.Row][src.Col];
                if (cell != null && cell.Indestructible) { cell.Despawn(); gr.Rows[src.Row][src.Col] = null; }
            }
            gr.Sources.RemoveAt(i);
        }

        // Cột kề của Spawner8: vòng KÍN thì nối vòng (cột cuối kề cột 0); vòng hở thì tràn mép = không có ô.
        private static int NeighborCol(GridRuntime gr, int col, int len) =>
            gr.Data.IsFullRing && len > 0 ? ((col % len) + len) % len : col;

        // Ô (row, col) có phải ô gốc của 1 Spawner8 không (giữ cố định kể cả khi spawner đó đã hết queue).
        private static bool IsSpawnerCell(GridRuntime gr, int row, int col) =>
            row >= 0 && row < gr.SpawnerCells.Count && col >= 0 && col < gr.SpawnerCells[row].Length
            && gr.SpawnerCells[row][col];

        /// <summary>
        /// Nhả mục hợp lệ kế tiếp trong <paramref name="queue"/> ra ô (row, col): dựng cell ở
        /// <paramref name="spawnPos"/> rồi trượt về đúng ô. false = hàng đợi không còn mục hợp lệ.
        /// Dùng cho Spawner THƯỜNG (1 hướng).
        /// </summary>
        private bool SpawnFromQueue(GridRuntime gr, Queue<PendingBlockData> queue, int row, int col,
                                    Vector3 spawnPos, float dirAngle)
        {
            PendingBlockData p = null;
            while (queue.Count > 0 && p == null)
            {
                p = queue.Dequeue();
                if (p == null || p.BlockStackCt <= 0) p = null; // mục rác trong data → bỏ, lấy mục kế
            }
            if (p == null) return false;

            var data = new BlockCellData
            {
                Color = p.Color,
                BlockStackCt = p.BlockStackCt,
                BlockCol = col,
                SpawnerDepth = row,
                SpawnerDirectionAngleZ = dirAngle,
            };
            var row_ = gr.Rows[row];
            var cell = CreateCell(gr, $"Cell_spawn_r{row}_e{col}", spawnPos, data);
            cell.MoveTo(gr.Data.CellPosAt(row, col, row_.Length), _collapseDuration); // tự khoá ngắm khi trượt
            row_[col] = cell;
            return true;
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
                    // Ô gốc Spawner8 (Indestructible) KHÔNG cộng ở đây: block nó đang hiển thị chính là ĐẦU
                    // Queue của nguồn, đã cộng ở vòng dưới → cộng cả 2 là đếm gấp đôi.
                    foreach (var row in gr.Rows)
                        foreach (var cell in row)
                            if (cell != null && !cell.Indestructible) s += cell.StackCount;
                    // Block chưa nhả ra: Spawner thường = các mục refill sau ô gốc; Spawner8 = cả sequence
                    // (gồm màu ô gốc đang hiển thị). Đều là block "chưa clear".
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
