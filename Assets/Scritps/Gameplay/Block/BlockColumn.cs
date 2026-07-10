using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// 1 cột gồm nhiều block cùng màu, cách đều. Mỗi phát bắn trừ 1 block; hết block
    /// → báo lane để dồn các cột phía sau lên (yêu cầu #5, #9).
    /// </summary>
    public class BlockColumn : MonoBehaviour
    {
        public BlockColor Color { get; private set; }

        private readonly List<Block> _blocks = new List<Block>();
        private BlockLane _lane;
        private Coroutine _moveRoutine;

        public int Count => _blocks.Count;
        public bool IsCleared => _blocks.Count == 0;

        public void Build(ColumnData data, BlockLane lane, Block prefab, Vector3 stackDir, float blockSpacing)
        {
            _lane = lane;
            Color = data.Color;

            Vector3 dir = stackDir.sqrMagnitude > 0.0001f ? stackDir.normalized : Vector3.right;
            for (int j = 0; j < data.BlockCount; j++)
            {
                var b = GameplayFactory.CreateBlock(prefab, transform);
                b.transform.position = transform.position + dir * blockSpacing * j;
                b.Init(this, j, Color);
                _blocks.Add(b);
            }
        }

        /// <summary>Trừ 1 block (từ đầu stack xa path nhất). Hết block → báo lane.</summary>
        public void HitOnce()
        {
            if (_blocks.Count == 0) return;

            int last = _blocks.Count - 1;
            var b = _blocks[last];
            _blocks.RemoveAt(last);
            if (b != null) b.Vanish();

            if (_blocks.Count == 0 && _lane != null) _lane.OnColumnCleared(this);
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
