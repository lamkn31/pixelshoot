using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Quản lý pool cho Gun / Block / Bullet (dùng <see cref="Pooler{TItem}"/>). Prefab lấy từ LevelData;
    /// null thì tạo template primitive (inactive) làm nguồn clone. Init() trả hết item cũ về pool trước
    /// khi rebuild level.
    /// </summary>
    public class PoolManager : Singleton<PoolManager>
    {
        [SerializeField] private Pooler<Gun> _gunPool = new Pooler<Gun>();
        [SerializeField] private Pooler<Block> _blockPool = new Pooler<Block>();
        [SerializeField] private Pooler<Bullet> _bulletPool = new Pooler<Bullet>();

        public void Init(LevelData level)
        {
            SetupPool(_gunPool, level.GunPrefab, PrimitiveType.Sphere, 0.6f, "GunPool");
            SetupPool(_blockPool, level.BlockPrefab, PrimitiveType.Cube, 0.5f, "BlockPool");
            SetupPool(_bulletPool, level.BulletPrefab, PrimitiveType.Sphere, 0.25f, "BulletPool");
        }

        public Gun GetGun() => _gunPool.Get();
        public Block GetBlock() => _blockPool.Get();
        public Bullet GetBullet() => _bulletPool.Get();

        public void ReturnAll()
        {
            _gunPool.ReturnAll();
            _blockPool.ReturnAll();
            _bulletPool.ReturnAll();
        }

        private void SetupPool<T>(Pooler<T> pool, T prefab, PrimitiveType prim, float scale, string parentName)
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

        private T CreateTemplate<T>(PrimitiveType prim, float scale, string name) where T : MonoBehaviour
        {
            var go = GameObject.CreatePrimitive(prim);
            go.name = name;
            go.transform.localScale = Vector3.one * scale;
            go.transform.SetParent(transform);
            go.SetActive(false); // template chỉ để clone
            return go.AddComponent<T>();
        }
    }
}
