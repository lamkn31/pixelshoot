using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Vẽ gizmo preview của 1 LevelData ngay trong Scene (path loop, các cột theo màu, các gun trong slot).
    /// Hỗ trợ Level Tool trực quan hoá bố cục (yêu cầu #10). Chỉ chạy trong Editor qua OnDrawGizmos.
    /// </summary>
    public class LevelPreview : MonoBehaviour
    {
        public LevelData level;
        public bool drawPath = true;
        public bool drawLanes = true;
        public bool drawSlots = true;

        private void OnDrawGizmos()
        {
            if (level == null) return;

            if (drawPath && level.PathWaypoints != null && level.PathWaypoints.Count >= 2)
            {
                Gizmos.color = Color.cyan;
                int n = level.PathWaypoints.Count;
                for (int i = 0; i < n; i++)
                {
                    Vector3 a = level.PathWaypoints[i];
                    Vector3 b = level.PathWaypoints[(i + 1) % n];
                    Gizmos.DrawLine(a, b);
                    Gizmos.DrawWireSphere(a, 0.15f);
                }
            }

            if (drawLanes && level.Lanes != null)
            {
                foreach (var lane in level.Lanes)
                {
                    if (lane?.Columns == null) continue;
                    Vector3 dir = lane.ColumnDirection.sqrMagnitude > 0.0001f ? lane.ColumnDirection.normalized : Vector3.up;
                    Vector3 sdir = lane.BlockStackDir.sqrMagnitude > 0.0001f ? lane.BlockStackDir.normalized : Vector3.right;
                    for (int i = 0; i < lane.Columns.Count; i++)
                    {
                        var col = lane.Columns[i];
                        if (col == null) continue;
                        Vector3 cpos = lane.FrontPos + dir * lane.ColumnSpacing * i;
                        Gizmos.color = BlockColorPalette.ToColor(col.Color);
                        for (int j = 0; j < col.BlockCount; j++)
                            Gizmos.DrawWireCube(cpos + sdir * lane.BlockSpacing * j, Vector3.one * 0.45f);
                    }
                }
            }

            if (drawSlots && level.Slots != null)
            {
                foreach (var s in level.Slots)
                {
                    if (s?.Guns == null) continue;
                    Vector3 sdir = s.Direction.sqrMagnitude > 0.0001f ? s.Direction.normalized : Vector3.up;
                    for (int i = 0; i < s.Guns.Count; i++)
                    {
                        var g = s.Guns[i];
                        if (g == null) continue;
                        Gizmos.color = BlockColorPalette.ToColor(g.Color);
                        Gizmos.DrawWireSphere(s.Position + sdir * s.Spacing * i, 0.3f);
                    }
                }
            }
        }
    }
}
