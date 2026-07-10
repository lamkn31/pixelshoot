using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// 1 làn chứa nhiều cột xếp theo hàng. Cột index 0 = ngoài cùng (gần path).
    /// Khi 1 cột bị phá hết → xoá khỏi danh sách và dồn các cột phía sau lên theo hướng lane.
    /// </summary>
    public class BlockLane : MonoBehaviour
    {
        [SerializeField] private float collapseDuration = 0.25f;

        private readonly List<BlockColumn> _columns = new List<BlockColumn>();
        private Vector3 _frontPos, _colDir, _stackDir;
        private float _colSpacing, _blockSpacing;

        /// <summary>Cột ngoài cùng (gần path) — cột duy nhất mà gun có thể bắn tới trong lane này.</summary>
        public BlockColumn FrontColumn => _columns.Count > 0 ? _columns[0] : null;

        public int TotalBlocks
        {
            get { int s = 0; foreach (var c in _columns) s += c.Count; return s; }
        }

        public void Build(LaneData data, Block blockPrefab)
        {
            _frontPos = data.FrontPos;
            _colDir = data.ColumnDirection.sqrMagnitude > 0.0001f ? data.ColumnDirection.normalized : Vector3.up;
            _colSpacing = data.ColumnSpacing;
            _stackDir = data.BlockStackDir;
            _blockSpacing = data.BlockSpacing;

            for (int i = 0; i < data.Columns.Count; i++)
            {
                if (data.Columns[i] == null) continue;
                var go = new GameObject("Column_" + i);
                go.transform.SetParent(transform);
                go.transform.position = _frontPos + _colDir * _colSpacing * i;

                var col = go.AddComponent<BlockColumn>();
                col.Build(data.Columns[i], this, blockPrefab, _stackDir, _blockSpacing);
                _columns.Add(col);
            }
        }

        public void OnColumnCleared(BlockColumn col)
        {
            int idx = _columns.IndexOf(col);
            if (idx >= 0) _columns.RemoveAt(idx);
            if (col != null) Destroy(col.gameObject);

            // Dồn các cột còn lại về đúng slot (cột 0 gần path nhất).
            for (int i = 0; i < _columns.Count; i++)
                _columns[i].MoveTo(_frontPos + _colDir * _colSpacing * i, collapseDuration);

            GridBlockManager.Instance?.OnLaneChanged();
        }
    }
}
