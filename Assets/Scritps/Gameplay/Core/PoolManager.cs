using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Quản lý pool cho Gun / BlockCell / Block / Bullet (dùng <see cref="Pooler{TItem}"/>). Prefab lấy từ
    /// LevelData; null thì tạo template (primitive, hoặc GameObject rỗng với BlockCell) làm nguồn clone.
    /// Init() trả hết item cũ về pool trước khi rebuild level.
    /// </summary>
    public class PoolManager : Singleton<PoolManager>
    {
        [SerializeField] private Pooler<Gun> _gunPool = new Pooler<Gun>();
        [SerializeField] private Pooler<BlockCell> _cellPool = new Pooler<BlockCell>();
        [SerializeField] private Pooler<Block> _blockPool = new Pooler<Block>();
        [SerializeField] private Pooler<Bullet> _bulletPool = new Pooler<Bullet>();

        public void Init(LevelData level)
        {
            ReturnAllCells(); // PHẢI trước SetupPool — SetupPool nhả block pool

            SetupPool(_gunPool, level.GunPrefab, PrimitiveType.Sphere, 0.6f, "GunPool");
            SetupPool(_blockPool, level.BlockPrefab, PrimitiveType.Cube, 0.5f, "BlockPool");
            SetupPool(_bulletPool, level.BulletPrefab, PrimitiveType.Sphere, 0.25f, "BulletPool");
            // Cell chỉ là node chứa stack → fallback là GameObject RỖNG, không phải primitive.
            SetupPool(_cellPool, level.BlockCellPrefab, null, 1f, "CellPool");
        }

        /// <summary>
        /// Nhả cell qua chính <see cref="BlockCell.Despawn"/> để cell TỰ dọn stack rồi mới về pool, và
        /// phải chạy TRƯỚC khi block pool bị nhả hàng loạt.
        /// <para>Pooler.ReturnAll() nhả thẳng qua pool, không đi qua Despawn() → cell giữ nguyên list
        /// _blocks trỏ vào các block ĐÃ nằm sẵn trong pool. Lần tái dùng sau, BlockCell.Build() gọi
        /// ReleaseBlocks() nhả lần 2 → InvalidOperationException "already been released to the pool",
        /// và grid dựng dở dang. Chỉ lộ ra khi Build() level từ lần 2 trở đi (live sync / đổi level).</para>
        /// </summary>
        private void ReturnAllCells()
        {
            // Bản copy: Despawn() có sửa _externalPool của pool trong lúc duyệt.
            foreach (var cell in _cellPool.GetNewExternalPoolList())
                if (cell != null) cell.Despawn();
        }

        public void ReturnAll()
        {
            ReturnAllCells();
            _gunPool.ReturnAll();
            _cellPool.ReturnAll();
            _blockPool.ReturnAll();
            _bulletPool.ReturnAll();
        }

        public Gun GetGun() => _gunPool.Get();
        public BlockCell GetCell() => _cellPool.Get();
        public Block GetBlock() => _blockPool.Get();
        public Bullet GetBullet() => _bulletPool.Get();

        private void SetupPool<T>(Pooler<T> pool, T prefab, PrimitiveType? prim, float scale, string parentName)
            where T : MonoBehaviour
        {
            var parent = transform.Find(parentName);
            if (parent == null)
            {
                parent = new GameObject(parentName).transform;
                parent.SetParent(transform);
            }
            pool.SetParent(parent);
            pool.ReturnAll(); // trả hết item active của level trước

            T pf = prefab != null
                ? prefab
                : (pool.ItemPrefab != null ? pool.ItemPrefab : CreateTemplate<T>(prim, scale, parentName + "_Template"));
            pool.SetItemPrefab(pf);
        }

        private T CreateTemplate<T>(PrimitiveType? prim, float scale, string name) where T : MonoBehaviour
        {
            var go = prim.HasValue ? GameObject.CreatePrimitive(prim.Value) : new GameObject();
            go.name = name;
            go.transform.localScale = Vector3.one * scale;
            go.transform.SetParent(transform);
            go.SetActive(false); // template chỉ để clone
            return go.AddComponent<T>();
        }
    }
}
