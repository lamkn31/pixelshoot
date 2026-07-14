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
    /// 1 grid xếp block dạng VÒNG CUNG (fan) trên sàn XZ. Mỗi hàng (row) là 1 cung bán kính
    /// baseRadius + row*rowSpacing; SỐ BLOCK mỗi hàng TỰ TÍNH = chiều dài cung / (BlockWidth + Spacing),
    /// rồi dãn đều trong góc mở ArcAngle. Ra xa cung dài hơn → nhiều block hơn. Row 0 = ngoài cùng gần path.
    /// </summary>
    [Serializable]
    public class BlockGridData
    {
        [Tooltip("Tâm vòng cung (sàn XZ).")]
        public Vector3 Center;
        [Tooltip("Bán kính hàng đầu (row 0, gần path).")]
        public float BaseRadius = 3f;
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
        [Tooltip("Cell theo thứ tự (row, element). Chỉ dùng Color + BlockStackCt.")]
        public List<BlockCellData> Cells = new List<BlockCellData>();

        private const int SampleCount = 48;

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

        /// <summary>Số block hàng row = chiều dài đường cong / (BlockWidth + Spacing).</summary>
        public int ElementsInRow(int row)
        {
            float step = Mathf.Max(0.01f, BlockWidth + Spacing);
            return Mathf.Max(1, Mathf.FloorToInt(RowLength(row) / step));
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

        /// <summary>Vị trí phần tử e trong 'count' phần tử, dãn ĐỀU theo chiều dài đường cong của hàng.</summary>
        public Vector3 CellPosAt(int row, int e, int count)
        {
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
