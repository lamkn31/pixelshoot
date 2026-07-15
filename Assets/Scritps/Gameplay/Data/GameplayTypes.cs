using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>Màu dùng chung cho Gun và Block. Gun chỉ bắn được block cùng màu.</summary>
    public enum BlockColor { Red, Green, Blue, Yellow, Purple, Orange }

    /// <summary>Độ khó của level (~ GameDifficulty của PixelShoot_2).</summary>
    public enum GameDifficulty { Easy, Normal, Hard, VeryHard, Expert }

    /// <summary>Loại obstacle gắn lên block/cell (~ BlockObstacleType).</summary>
    public enum BlockObstacleType { None, Crate, Lock, Ice, Mystery, Barricade }

    /// <summary>
    /// Cách chia số cell mỗi hàng của grid.
    /// <para><b>ArcLength</b>: số cell = chiều dài cung / (BlockWidth+Spacing) → hàng ra xa có NHIỀU cell hơn
    /// (4/5/6...). Cột không thẳng: 1 cell hàng sau bị chặn bởi TỐI ĐA 2 cell hàng trước (theo góc).</para>
    /// <para><b>Uniform</b>: mọi hàng CÙNG số cell (lấy theo hàng chật nhất = row 0 để không chồng cell)
    /// → cùng index = cùng góc = 1 cột xuyên tâm; chặn 1:1.</para>
    /// </summary>
    public enum BlockGridLayout { ArcLength, Uniform }

    /// <summary>
    /// Hình dạng grid block.
    /// <para><b>Arc</b>: vòng cung (fan) quanh Center — hàng = cung bán kính BaseRadius + row*RowSpacing.</para>
    /// <para><b>Rect</b>: lưới CHỮ NHẬT thông thường — hàng thẳng dọc trục X, sâu dần theo +Z;
    /// mọi hàng có đúng <see cref="BlockGridData.Columns"/> cell nên cột luôn thẳng (chặn 1:1).</para>
    /// </summary>
    public enum BlockGridShape { Arc, Rect }

    /// <summary>
    /// 1 grid xếp block trên sàn XZ. Row 0 = ngoài cùng, gần path (gun ăn từ row 0 vào trong).
    /// <para><b>Shape = Arc</b>: vòng cung (fan). Mỗi hàng là 1 cung bán kính BaseRadius + row*RowSpacing,
    /// dãn đều trong góc mở ArcAngle. Số cell mỗi hàng theo <see cref="Layout"/>: ArcLength = chiều dài cung
    /// / (BlockWidth+Spacing) → ra xa nhiều cell hơn (cột lệch); Uniform = mọi hàng bằng row 0 (cột thẳng).</para>
    /// <para><b>Shape = Rect</b>: lưới CHỮ NHẬT thông thường — mỗi hàng có đúng <see cref="Columns"/> cell dãn
    /// theo trục X quanh Center, hàng sâu dần theo +Z. Cột luôn thẳng (chặn 1:1); Layout không dùng.</para>
    /// </summary>
    [Serializable]
    public class BlockGridData
    {
        [Tooltip("Arc = vòng cung quanh Center. Rect = lưới chữ nhật thông thường (hàng thẳng theo X).")]
        public BlockGridShape Shape = BlockGridShape.Arc;
        [Tooltip("Tâm grid (sàn XZ).")]
        public Vector3 Center;
        [Tooltip("Arc: bán kính hàng đầu. Rect: khoảng cách từ Center tới hàng đầu (row 0, gần path).")]
        public float BaseRadius = 3f;
        [Tooltip("CHỈ dùng cho Rect: số cell mỗi hàng (mọi hàng bằng nhau → cột thẳng).")]
        [Min(1)] public int Columns = 5;
        [Tooltip("Bán kính tăng thêm mỗi hàng ra xa.")]
        public float RowSpacing = 1.2f;
        [Min(1)] public int Rows = 3;
        [Tooltip("Tổng góc mở/quét của cung (độ). >360 = cuộn nhiều vòng (xoắn ốc).")]
        public float ArcAngle = 90f;
        [Tooltip("Bán kính tăng thêm DỌC theo sweep: 0 = vòng cung phẳng; >0 = xoắn ốc.")]
        public float SpiralGrowth = 0f;
        [Tooltip("Độ rộng 1 block (world units).")]
        public float BlockWidth = 0.8f;
        [Tooltip("Khoảng cách giữa 2 block trên cùng hàng.")]
        public float Spacing = 0.2f;
        [Tooltip("ArcLength = hàng ra xa nhiều cell hơn (cột lệch). Uniform = mọi hàng bằng nhau (cột thẳng).")]
        public BlockGridLayout Layout = BlockGridLayout.ArcLength;
        [Tooltip("Cell theo thứ tự (row, element). Chỉ dùng Color + BlockStackCt.")]
        public List<BlockCellData> Cells = new List<BlockCellData>();

        [Tooltip("Hàng đợi SPAWNER nhả thêm (~ PendingBlockDataArr của PixelShoot_2). Mỗi lần ring front " +
                 "bị thu hết → collapse, spawner dựng 1 ring mới ở NGOÀI CÙNG lấy lần lượt các mục này.")]
        public List<PendingBlockData> PendingRefill = new List<PendingBlockData>();

        private const int SampleCount = 48;

        /// <summary>Tổng số block đang chờ trong hàng đợi refill (∑BlockStackCt).</summary>
        public int PendingBlockTotal()
        {
            int t = 0;
            if (PendingRefill != null)
                foreach (var p in PendingRefill)
                    if (p != null && p.BlockStackCt > 0) t += p.BlockStackCt;
            return t;
        }

        // Vị trí tâm-hàng theo s (0..1) dọc sweep — có xoắn ốc.
        private Vector3 PosAlong(int row, float s)
        {
            float angleRad = Mathf.Lerp(-ArcAngle * 0.5f, ArcAngle * 0.5f, s) * Mathf.Deg2Rad;
            float radius = BaseRadius + row * RowSpacing + s * SpiralGrowth;
            return Center + new Vector3(Mathf.Sin(angleRad) * radius, 0f, Mathf.Cos(angleRad) * radius);
        }

        private float RowLength(int row)
        {
            float len = 0f;
            Vector3 prev = PosAlong(row, 0f);
            for (int i = 1; i <= SampleCount; i++)
            {
                Vector3 p = PosAlong(row, i / (float)SampleCount);
                len += Vector3.Distance(prev, p);
                prev = p;
            }
            return len;
        }

        /// <summary>
        /// Số cell hàng row. ArcLength: theo chiều dài cung (ra xa nhiều hơn).
        /// Uniform: mọi hàng lấy theo row 0 (hàng CHẬT nhất) để không hàng nào bị chồng cell.
        /// </summary>
        public int ElementsInRow(int row)
        {
            if (Shape == BlockGridShape.Rect) return Mathf.Max(1, Columns); // lưới chữ nhật: mọi hàng bằng nhau
            if (Layout == BlockGridLayout.Uniform) row = 0;
            float step = Mathf.Max(0.01f, BlockWidth + Spacing);
            return Mathf.Max(1, Mathf.FloorToInt(RowLength(row) / step));
        }

        /// <summary>
        /// Các index ở hàng TRƯỚC (row-1) chặn cell (row, e), map theo vị trí góc chuẩn hoá dọc cung.
        /// Uniform → luôn 1:1 (chỉ <paramref name="a"/>). ArcLength → cell giữa có 2 index chặn
        /// (<paramref name="a"/> và <paramref name="b"/>), cell đầu/cuối chỉ 1. b = -1 nếu không có.
        /// </summary>
        public static void FrontIndices(int curCount, int prevCount, int e, out int a, out int b)
        {
            a = -1; b = -1;
            if (prevCount <= 0) return;
            if (curCount <= 1 || prevCount <= 1) { a = 0; return; }

            float s = Mathf.Clamp01(e / (float)(curCount - 1)); // vị trí chuẩn hoá dọc cung
            float f = s * (prevCount - 1);
            a = Mathf.Clamp(Mathf.FloorToInt(f), 0, prevCount - 1);
            int c = Mathf.Clamp(Mathf.CeilToInt(f), 0, prevCount - 1);
            if (c != a) b = c;
        }

        public int TotalCells()
        {
            int t = 0;
            for (int r = 0; r < Rows; r++) t += ElementsInRow(r);
            return t;
        }

        /// <summary>Index phẳng của cell (row, element) trong <see cref="Cells"/>.</summary>
        public int CellIndex(int row, int e)
        {
            int idx = 0;
            for (int r = 0; r < row; r++) idx += ElementsInRow(r);
            return idx + e;
        }

        /// <summary>
        /// Vị trí phần tử e trong 'count' phần tử của hàng row.
        /// Arc: dãn ĐỀU theo chiều dài đường cong. Rect: lưới thẳng — cell dãn theo X quanh Center,
        /// hàng sâu dần theo +Z (row 0 gần path nhất).
        /// </summary>
        public Vector3 CellPosAt(int row, int e, int count)
        {
            if (Shape == BlockGridShape.Rect)
            {
                float step = Mathf.Max(0.01f, BlockWidth + Spacing);
                float lateral = (e - (count - 1) * 0.5f) * step; // canh giữa quanh Center
                return Center + new Vector3(lateral, 0f, BaseRadius + row * RowSpacing);
            }
            float total = RowLength(row);
            float target = count > 1 ? e * (total / (count - 1)) : total * 0.5f;
            return PosByLength(row, target, total);
        }

        public Vector3 CellPos(int row, int e) => CellPosAt(row, e, ElementsInRow(row));

        private Vector3 PosByLength(int row, float targetLen, float totalLen)
        {
            if (targetLen <= 0f) return PosAlong(row, 0f);
            if (targetLen >= totalLen) return PosAlong(row, 1f);
            float acc = 0f;
            Vector3 prev = PosAlong(row, 0f);
            for (int i = 1; i <= SampleCount; i++)
            {
                Vector3 p = PosAlong(row, i / (float)SampleCount);
                float d = Vector3.Distance(prev, p);
                if (acc + d >= targetLen)
                    return Vector3.Lerp(prev, p, d > 1e-4f ? (targetLen - acc) / d : 0f);
                acc += d; prev = p;
            }
            return PosAlong(row, 1f);
        }

        public BlockCellData GetCell(int row, int e)
        {
            int idx = CellIndex(row, e);
            return (Cells != null && idx >= 0 && idx < Cells.Count) ? Cells[idx] : null;
        }
    }

    /// <summary>
    /// 1 mục trong hàng đợi refill của spawner (~ PendingBlockData của PixelShoot_2): màu + số block xếp chồng.
    /// Khi ring front bị thu hết, spawner lấy lần lượt các mục này để dựng cell mới ở ring ngoài cùng.
    /// </summary>
    [Serializable]
    public class PendingBlockData
    {
        public BlockColor Color;
        [Min(1)] public int BlockStackCt = 3;
    }

    /// <summary>Data 1 gun: màu + số đạn (yêu cầu #7).</summary>
    [Serializable]
    public class GunData
    {
        public BlockColor Color;
        [Min(1)] public int CountBullet = 5;
    }

    /// <summary>Data 1 block đơn trong cell: index + vị trí local (yêu cầu #8).</summary>
    [Serializable]
    public class BlockData
    {
        public int IndexInStack;
        public Vector3 LocalPos;
    }

    /// <summary>
    /// Data 1 cell block — bám sát BlockCellData của PixelShoot_2 (per-cell).
    /// Cell cùng <see cref="BlockCol"/> tạo thành 1 cột; <see cref="SpawnerDepth"/> = vị trí trong cột
    /// (0 = ngoài cùng, gần collector). Khi cell depth 0 bị phá hết → các cell phía sau dồn lên.
    /// </summary>
    [Serializable]
    public class BlockCellData
    {
        public BlockColor Color;
        public Vector3 CellPos;
        public Vector3 CellScale = Vector3.one;

        [Tooltip("Nhóm cột — các cell cùng BlockCol dồn về nhau.")]
        public int BlockCol;
        [Tooltip("Vị trí trong cột: 0 = ngoài cùng (gần collector).")]
        public int SpawnerDepth;
        [Tooltip("Số block xếp chồng trong cell (~ BlockStackCt).")]
        [Min(1)] public int BlockStackCt = 3;
        [Tooltip("Hướng dồn/spawn của cell trên sàn ngang XZ, tính bằng độ quanh trục Y (0° = +Z).")]
        public float SpawnerDirectionAngleZ;

        /// <summary>Hướng dồn (vector NGANG trên sàn XZ): 0°=+Z, 90°=+X, 180°=−Z, 270°=−X.</summary>
        public Vector3 DirectionVector => Quaternion.Euler(0f, SpawnerDirectionAngleZ, 0f) * Vector3.forward;
    }

    /// <summary>
    /// Data 1 slot: chỉ chứa danh sách gun theo thứ tự ra (yêu cầu #6). Vị trí/hướng slot lấy từ
    /// GunSlot đặt sẵn trên SCENE; spacing dùng chung từ <see cref="GameSettings"/>.
    /// </summary>
    [Serializable]
    public class SlotData
    {
        public System.Collections.Generic.List<GunData> Guns = new System.Collections.Generic.List<GunData>();
    }

    /// <summary>Prop trang trí trên board (~ GameBoardPropData).</summary>
    [Serializable]
    public class GameBoardPropData
    {
        public GameObject PropPrefab;
        public Vector3 PropPos;
        public Quaternion PropRot;
        public Vector3 PropScale = Vector3.one;
    }

    /// <summary>Obstacle gắn lên cell (~ BlockObstacleData, bản rút gọn — chưa mô phỏng runtime).</summary>
    [Serializable]
    public class BlockObstacleData
    {
        public BlockObstacleType Type;
        public Vector3 Pos;
        [Tooltip("Index cell mà obstacle gắn vào (-1 = độc lập).")]
        public int TargetCellIndex = -1;
        [Min(1)] public int Strength = 1;
    }

    /// <summary>Bảng màu dùng chung để tô Block/Gun và vẽ gizmo.</summary>
    public static class BlockColorPalette
    {
        public static Color ToColor(BlockColor c)
        {
            switch (c)
            {
                case BlockColor.Red:    return new Color(0.90f, 0.22f, 0.22f);
                case BlockColor.Green:  return new Color(0.24f, 0.78f, 0.32f);
                case BlockColor.Blue:   return new Color(0.22f, 0.45f, 0.95f);
                case BlockColor.Yellow: return new Color(0.96f, 0.83f, 0.22f);
                case BlockColor.Purple: return new Color(0.62f, 0.32f, 0.85f);
                case BlockColor.Orange: return new Color(0.96f, 0.55f, 0.16f);
                default:                return Color.white;
            }
        }
    }
}
