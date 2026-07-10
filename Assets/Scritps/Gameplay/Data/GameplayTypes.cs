using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>Màu dùng chung cho Gun và Block. Gun chỉ bắn được block cùng màu.</summary>
    public enum BlockColor { Red, Green, Blue, Yellow, Purple, Orange }

    /// <summary>Data 1 gun: màu + số đạn (yêu cầu #7).</summary>
    [Serializable]
    public class GunData
    {
        public BlockColor Color;
        [Min(1)] public int CountBullet = 5;
    }

    /// <summary>Data 1 block: index trong cột + vị trí local (yêu cầu #8).</summary>
    [Serializable]
    public class BlockData
    {
        public int IndexInColumn;
        public Vector3 LocalPos;
    }

    /// <summary>Data 1 cột: nhiều block cùng màu, cách đều (yêu cầu #9).</summary>
    [Serializable]
    public class ColumnData
    {
        public BlockColor Color;
        [Min(1)] public int BlockCount = 3;
    }

    /// <summary>
    /// Data 1 lane (làn) chứa nhiều cột xếp theo hàng. Cột index 0 = ngoài cùng (gần path nhất).
    /// Khi cột ngoài cùng bị phá hết → các cột phía sau dồn lên theo <see cref="ColumnDirection"/>.
    /// </summary>
    [Serializable]
    public class LaneData
    {
        [Tooltip("Vị trí world của cột ngoài cùng (gần path).")]
        public Vector3 FrontPos;
        [Tooltip("Hướng các cột xếp về phía sau (cũng là hướng dồn lên khi cột trước bị phá).")]
        public Vector3 ColumnDirection = Vector3.up;
        public float ColumnSpacing = 1.2f;
        [Tooltip("Hướng stack các block trong 1 cột.")]
        public Vector3 BlockStackDir = Vector3.right;
        public float BlockSpacing = 0.55f;
        public List<ColumnData> Columns = new List<ColumnData>();
    }

    /// <summary>Data 1 slot (hàng gun): vị trí + hàng đợi gun theo thứ tự ra (yêu cầu #6).</summary>
    [Serializable]
    public class SlotData
    {
        public Vector3 Position;
        [Tooltip("Hướng các gun xếp hàng trong slot.")]
        public Vector3 Direction = Vector3.up;
        public float Spacing = 1f;
        public List<GunData> Guns = new List<GunData>();
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
