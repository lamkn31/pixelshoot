using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Cấu hình 1 level: path (loop), giới hạn gun, danh sách slot và danh sách lane/cột.
    /// Là nguồn dữ liệu cho Level Tool (yêu cầu #4,#6,#7,#8,#9,#10).
    /// </summary>
    [CreateAssetMenu(fileName = "LevelData", menuName = "Wayfu/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Header("Path (luôn là loop)")]
        public List<Vector3> PathWaypoints = new List<Vector3>();
        public float CornerRadius = 1f;

        [Header("Guns trên path")]
        [Min(1)] public int MaxGunOnPath = 5;
        public float GunSpeed = 3f;
        public float GunSpacing = 1.2f;
        [Tooltip("Khoảng thời gian giữa 2 phát bắn của 1 gun.")]
        public float FireInterval = 0.25f;

        [Header("Slots (số hàng gun + thứ tự gun ra)")]
        public List<SlotData> Slots = new List<SlotData>();

        [Header("Grid Block (lanes / columns)")]
        public List<LaneData> Lanes = new List<LaneData>();

        [Header("Prefabs (optional — null thì fallback primitive)")]
        public Gun GunPrefab;
        public Block BlockPrefab;

        /// <summary>Tổng số bullet theo màu (gộp toàn bộ gun trong mọi slot).</summary>
        public Dictionary<BlockColor, int> TotalBullets()
        {
            var d = new Dictionary<BlockColor, int>();
            foreach (var s in Slots)
            {
                if (s?.Guns == null) continue;
                foreach (var g in s.Guns)
                {
                    if (g == null) continue;
                    d.TryGetValue(g.Color, out var v);
                    d[g.Color] = v + g.CountBullet;
                }
            }
            return d;
        }

        /// <summary>Tổng số block theo màu (gộp toàn bộ cột trong mọi lane).</summary>
        public Dictionary<BlockColor, int> TotalBlocks()
        {
            var d = new Dictionary<BlockColor, int>();
            foreach (var l in Lanes)
            {
                if (l?.Columns == null) continue;
                foreach (var c in l.Columns)
                {
                    if (c == null) continue;
                    d.TryGetValue(c.Color, out var v);
                    d[c.Color] = v + c.BlockCount;
                }
            }
            return d;
        }

        /// <summary>
        /// Kiểm tra: với MỖI màu, tổng bullet == tổng block (yêu cầu #10).
        /// Trả về true nếu khớp toàn bộ; report mô tả chi tiết.
        /// </summary>
        public bool ValidateColorBalance(out string report)
        {
            var bullets = TotalBullets();
            var blocks = TotalBlocks();

            var colors = new HashSet<BlockColor>();
            foreach (var k in bullets.Keys) colors.Add(k);
            foreach (var k in blocks.Keys) colors.Add(k);

            var sb = new StringBuilder();
            bool ok = true;
            foreach (var c in colors)
            {
                bullets.TryGetValue(c, out var b);
                blocks.TryGetValue(c, out var bl);
                bool match = b == bl;
                if (!match) ok = false;
                sb.AppendLine($"{c}: bullets={b} | blocks={bl} → {(match ? "OK" : "MISMATCH")}");
            }
            if (colors.Count == 0) sb.AppendLine("(chưa có gun/block nào)");
            report = sb.ToString();
            return ok;
        }
    }
}
