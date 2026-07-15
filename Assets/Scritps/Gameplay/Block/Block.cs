using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>1 block đơn (1 phần tử trong stack của 1 cell). Dùng Pooler — bị bắn thì trả về pool.</summary>
    public class Block : MonoBehaviour, IItemPool<Block>
    {
        public TypeColor Color { get; private set; }
        public BlockData Data { get; private set; }

        private BlockCell _cell;
        private Renderer _renderer;
        private Pooler<Block> _pool;

        public void OnInitializedInPool(Pooler<Block> pool) => _pool = pool;

        public void Init(BlockCell cell, int indexInStack, TypeColor color)
        {
            _cell = cell;
            Color = color;
            Data = new BlockData { IndexInStack = indexInStack, LocalPos = transform.localPosition };

            // Material lấy từ GlobalConfigManager theo TypeColor (không tô material.color nữa).
            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
            var mat = GlobalConfigManager.MaterialOf(color, TypeObject.Block);
            if (_renderer != null && mat != null) _renderer.sharedMaterial = mat;
        }

        /// <summary>Trả block về pool (thay cho Destroy).</summary>
        public void Despawn()
        {
            if (_pool != null) _pool.Release(this);
            else Destroy(gameObject);
        }
    }
}
