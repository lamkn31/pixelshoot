using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>1 block đơn: bị gun bắn thì biến mất, báo lại cột.</summary>
    public class Block : MonoBehaviour
    {
        public BlockColor Color { get; private set; }
        public BlockData Data { get; private set; }

        private BlockColumn _column;
        private Renderer _renderer;

        public void Init(BlockColumn column, int index, BlockColor color)
        {
            _column = column;
            Color = color;
            Data = new BlockData { IndexInColumn = index, LocalPos = transform.localPosition };

            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null) _renderer.material.color = BlockColorPalette.ToColor(color);
        }

        public void Vanish()
        {
            // TODO: hook FX/pooling ở đây nếu cần.
            Destroy(gameObject);
        }
    }
}
