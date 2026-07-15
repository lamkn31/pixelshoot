using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Đạn: bay từ gun tới cell mục tiêu, tới nơi thì phá 1 block của cell rồi tự trả về pool.
    /// Dùng Pooler qua <see cref="IItemPool{TItem}"/> để tự release.
    /// </summary>
    public class Bullet : MonoBehaviour, IItemPool<Bullet>
    {
        private const float HitDist = 0.25f;

        private Pooler<Bullet> _pool;
        private BlockCell _cell;
        private int _cellGen; // Generation của cell lúc bắn — lệch = cell pooled đã bị tái dùng
        private float _speed = 14f;
        private bool _active;
        private Renderer _renderer;

        public void OnInitializedInPool(Pooler<Bullet> pool) => _pool = pool;

        private void Awake()
        {
            // Bullet di chuyển bằng code, không cần collider.
            var col = GetComponent<Collider>();
            if (col != null) Destroy(col);
            _renderer = GetComponentInChildren<Renderer>();
        }

        public void Launch(Vector3 start, BlockCell target, float speed, TypeColor color)
        {
            transform.position = start;
            _cell = target;
            _cellGen = target != null ? target.Generation : 0;
            _speed = speed;
            _active = true;

            // Material lấy từ GlobalConfigManager theo TypeColor.
            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
            var mat = GlobalConfigManager.MaterialOf(color, TypeObject.Bullet);
            if (_renderer != null && mat != null) _renderer.sharedMaterial = mat;
        }

        private void Update()
        {
            if (!_active) return;
            // Cell đã bị phá — hoặc object pooled đã TÁI DÙNG thành cell khác (Generation lệch) → huỷ đạn,
            // không bay đuổi theo cell mới ở vị trí khác.
            if (_cell == null || _cell.Generation != _cellGen) { Despawn(); return; }

            Vector3 target = _cell.transform.position;
            transform.position = Vector3.MoveTowards(transform.position, target, _speed * Time.deltaTime);

            if ((transform.position - target).sqrMagnitude <= HitDist * HitDist)
            {
                _active = false;
                _cell.ApplyHit(); // trừ 1 block + huỷ pending
                GameController.Instance?.OnBoardChanged();
                Despawn();
            }
        }

        private void Despawn()
        {
            _active = false;
            _cell = null;
            if (_pool != null) _pool.Release(this);
            else Destroy(gameObject);
        }
    }
}
