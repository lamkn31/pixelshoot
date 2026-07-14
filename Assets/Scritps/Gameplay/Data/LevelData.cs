using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Cấu hình 1 level. Các thông số gameplay chung (gun/path/bullet/block spacing) nằm ở
    /// <see cref="GameSettings"/>; ở đây chỉ giữ dữ liệu RIÊNG theo level: path shape, camera, slots,
    /// grids block, props/obstacles, prefab. Bám sát schema PixelShoot_2 (per-cell, holes, obstacles).
    /// </summary>
    [CreateAssetMenu(fileName = "LevelData", menuName = "Wayfu/Level Data")]
    public class LevelData : ScriptableObject
    {
        [Header("Meta")]
        public GameDifficulty CurGameDifficulty = GameDifficulty.Easy;
        public bool SkipLevelLoop;
        public int NumberOfColors;
        public string[] MechanicNames;

        [Header("Path shape (loop) — riêng theo level")]
        public List<Vector3> PathWaypoints = new List<Vector3>();
        public bool IsClosed = true;
        public float CornerRadius = 1f;

        [Header("Slots (số hàng gun + thứ tự gun ra)")]
        public List<SlotData> Slots = new List<SlotData>();

        [Header("Holes grid (~ HolesGridSize / HoleCapacity)")]
        public Vector2Int HolesGridSize;
        public int HoleCapacity;

        [Header("Grids block (mỗi grid định hình bằng 4 góc; nhiều grid / level)")]
        public List<BlockGridData> Grids = new List<BlockGridData>();

        [Header("Camera Frame (top-down)")]
        [Tooltip("Ortho Size = nửa chiều cao khung nhìn (world units) — dùng khi chưa gán Scene Camera.")]
        public float CameraOrthoSize = 9f;
        public Vector2 ScreenAspect = new Vector2(9f, 16f);
        public Vector3 CameraCenter;

        [Header("Props / Obstacles")]
        public List<GameBoardPropData> BoardProps = new List<GameBoardPropData>();
        public List<BlockObstacleData> Obstacles = new List<BlockObstacleData>();

        [Header("Prefabs (optional — null thì fallback primitive, đều dùng Pooler)")]
        public Gun GunPrefab;
        public Block BlockPrefab;
        public Bullet BulletPrefab;

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

        /// <summary>Tổng số block theo màu (gộp BlockStackCt của mọi cell trong mọi grid).</summary>
        public Dictionary<BlockColor, int> TotalBlocks()
        {
            var d = new Dictionary<BlockColor, int>();
            foreach (var grid in Grids)
            {
                if (grid?.Cells == null) continue;
                foreach (var c in grid.Cells)
                {
                    if (c == null || c.BlockStackCt <= 0) continue;
                    d.TryGetValue(c.Color, out var v);
                    d[c.Color] = v + c.BlockStackCt;
                }
            }
            return d;
        }

        /// <summary>Kiểm tra: mỗi màu, tổng bullet == tổng block (yêu cầu #10).</summary>
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
