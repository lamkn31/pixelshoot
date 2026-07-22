using TMPro;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// 1 khối BĂNG (item pooled). Gắn trên ROOT prefab Ice; child TMP = countdown. IceController điều khiển:
    /// <see cref="Fit"/> đặt vị trí + scale vừa vùng cell, <see cref="SetCountdown"/> cập nhật số, <see cref="Despawn"/>
    /// trả về pool. Tự đo bounds (renderer) để fit nên không cần biết kích thước prefab trước.
    /// </summary>
    public class Ice : MonoBehaviour, IItemPool<Ice>, IResetComponent
    {
        private TMP_Text _countdown;
        private Pooler<Ice> _pool;
        private Vector3 _origScale, _countdownOrigScale;
        private Quaternion _origRot;
        private bool _cached;

        public void OnInitializedInPool(Pooler<Ice> pool) => _pool = pool;

        private void Awake() => CacheOriginals();

        // Nhớ scale/rotation gốc của prefab + TMP countdown (item pooled bị Fit ghi đè, phải reset trước khi dùng lại).
        private void CacheOriginals()
        {
            if (_cached) return;
            _origScale = transform.localScale;
            _origRot = transform.localRotation;
            _countdown = GetComponentInChildren<TMP_Text>(true);
            if (_countdown != null) _countdownOrigScale = _countdown.transform.localScale;
            _cached = true;
        }

        public void ResetComponent()
        {
            CacheOriginals();
            transform.localScale = _origScale;
            transform.localRotation = _origRot;
            if (_countdown != null) _countdown.transform.localScale = _countdownOrigScale;
        }

        // Băng nằm PHẲNG (X=90) úp xuống sàn, nhìn từ trên xuống; xoay thêm theo yaw của grid.
        private static readonly Quaternion FlatBase = Quaternion.Euler(90f, 0f, 0f);

        /// <summary>Phủ vùng cell: tâm world, bề rộng/sâu world (theo trục grid), xoay Y theo grid; hiện countdown = value.</summary>
        public void Fit(Vector3 center, float width, float depth, float yaw, bool showCountdown, int value)
        {
            CacheOriginals();
            // Đo bounds Ở TƯ THẾ PHẲNG (X=90, chưa yaw): local X → bề rộng sàn (world X), local Y → bề sâu (world Z).
            transform.localScale = _origScale;
            transform.rotation = FlatBase;
            transform.position = center;

            var b = CombinedBounds(gameObject);
            float fx = Mathf.Max(1e-3f, b.size.x), fz = Mathf.Max(1e-3f, b.size.z);
            float kx = width / fx, ky = depth / fz;
            transform.localScale = new Vector3(_origScale.x * kx, _origScale.y * ky, _origScale.z);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f) * FlatBase; // phẳng + xoay theo grid
            transform.position = center;

            if (_countdown != null)
            {
                _countdown.gameObject.SetActive(showCountdown);
                if (showCountdown)
                {
                    // Khối scale không đều → counter-scale số để không bị kéo méo.
                    _countdown.transform.localScale =
                        new Vector3(_countdownOrigScale.x / kx, _countdownOrigScale.y / ky, _countdownOrigScale.z);
                    _countdown.text = value.ToString();
                }
            }
        }

        /// <summary>Cập nhật số countdown (chỉ khối đang hiện countdown).</summary>
        public void SetCountdown(int remaining)
        {
            if (_countdown != null && _countdown.gameObject.activeSelf) _countdown.text = remaining.ToString();
        }

        public void Despawn()
        {
            if (_pool != null) _pool.Release(this);
            else Destroy(gameObject);
        }

        private static Bounds CombinedBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
