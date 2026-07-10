using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Tạo Block/Gun từ prefab; nếu prefab null thì fallback primitive (Cube/Sphere)
    /// để game chạy được ngay khi chưa có art. Primitive kèm sẵn Collider để click gun.
    /// </summary>
    public static class GameplayFactory
    {
        public static Block CreateBlock(Block prefab, Transform parent)
        {
            if (prefab != null) return Object.Instantiate(prefab, parent);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent);
            go.transform.localScale = Vector3.one * 0.5f;
            return go.AddComponent<Block>();
        }

        public static Gun CreateGun(Gun prefab, Transform parent)
        {
            if (prefab != null) return Object.Instantiate(prefab, parent);

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(parent);
            go.transform.localScale = Vector3.one * 0.6f;
            return go.AddComponent<Gun>();
        }
    }
}
