using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// 1 cell block (~ BlockCell của PixelShoot_2): chứa 1 stack block cùng màu (lấy từ Pooler).
    /// Mỗi ĐẠN tới trừ 1 block; hết block → báo GridBlockManager dồn các cell phía sau (yêu cầu #5, #9).
    /// Có <see cref="_pendingHits"/> để không bắn dư đạn khi nhiều gun cùng nhắm 1 cell.
    /// </summary>
    public class BlockCell : MonoBehaviour
    {
        public BlockColor Color { get; private set; }
        public int BlockCol { get; private set; }
        public int Depth { get; private set; }

        /// <summary>Cập nhật index cột khi cell dồn lên ô khác (Arc cột lệch: index có thể đổi).</summary>
        public void SetColumn(int col) => BlockCol = col;

        private readonly List<Block> _blocks = new List<Block>();
        private GridBlockManager _manager;
        private Coroutine _moveRoutine;
        private int _pendingHits;
        private float _stackSpacing;

        public int StackCount => _blocks.Count;
        /// <summary>Số block chưa bị đạn "đặt chỗ" (đạn đang bay) — gun chỉ bắn khi còn &gt; 0.</summary>
        public int Available => _blocks.Count - _pendingHits;
        public bool IsEmpty => _blocks.Count == 0;

        public void Build(BlockCellData data, float stackSpacing, GridBlockManager manager)
        {
            _manager = manager;
            _stackSpacing = stackSpacing;
            BlockCol = data.BlockCol;
            Depth = data.SpawnerDepth;
            _pendingHits = 0;

            Fill(data.Color, Mathf.Max(1, data.BlockStackCt));
        }

        // Dựng stack block cùng màu tại chỗ (dùng cho cả lần đầu lẫn khi Spawner đẩy cell kế ra).
        private void Fill(BlockColor color, int n)
        {
            Color = color;
            for (int j = 0; j < n; j++)
            {
                var b = PoolManager.Instance.GetBlock();
                b.transform.SetParent(transform);
                b.transform.position = transform.position + Vector3.up * _stackSpacing * j; // stack theo Y
                b.Init(this, j, Color);
                _blocks.Add(b);
            }
        }

        /// <summary>Đặt chỗ 1 đạn đang bay tới cell này.</summary>
        public void ReserveHit() => _pendingHits++;

        /// <summary>Đạn tới nơi: trừ 1 pending + phá 1 block.</summary>
        public void ApplyHit()
        {
            if (_pendingHits > 0) _pendingHits--;
            HitOnce();
        }

        private void HitOnce()
        {
            if (_blocks.Count == 0) return;

            int last = _blocks.Count - 1;
            var b = _blocks[last];
            _blocks.RemoveAt(last);
            if (b != null) b.Despawn(); // trả block về pool

            if (_blocks.Count > 0) return;
            if (_manager != null) _manager.OnCellCleared(this);
        }

        public void MoveTo(Vector3 target, float duration)
        {
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            if (!gameObject.activeInHierarchy || duration <= 0f) { transform.position = target; return; }
            _moveRoutine = StartCoroutine(MoveRoutine(target, duration));
        }

        private IEnumerator MoveRoutine(Vector3 target, float dur)
        {
            Vector3 start = transform.position;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(start, target, t / dur);
                yield return null;
            }
            transform.position = target;
            _moveRoutine = null;
        }
    }
}
