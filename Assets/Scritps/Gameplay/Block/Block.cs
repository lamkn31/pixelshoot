using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>1 block đơn (1 phần tử trong stack của 1 cell). Dùng Pooler — bị bắn thì trả về pool.</summary>
    public class Block : MonoBehaviour, IItemPool<Block>
    {
        public BlockColor Color { get; private set; }
        public BlockData Data { get; private set; }

        private BlockCell _cell;
        private Renderer _renderer;
        private Pooler<Block> _pool;

        public void OnInitializedInPool(Pooler<Block> pool) => _pool = pool;

        public void Init(BlockCell cell, int indexInStack, BlockColor color)
        {
            _cell = cell;
            Color = color;
            Data = new BlockData { IndexInStack = indexInStack, LocalPos = transform.localPosition };

            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null) _renderer.material.color = BlockColorPalette.ToColor(color);
        }

        /// <summary>Trả block về pool (thay cho Destroy).</summary>
        public void Despawn()
        {
            if (_pool != null) _pool.Release(this);
            else Destroy(gameObject);
        }
    }
}
