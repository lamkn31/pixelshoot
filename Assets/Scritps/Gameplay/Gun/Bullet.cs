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

        public void Launch(Vector3 start, BlockCell target, float speed, Color color)
        {
            transform.position = start;
            _cell = target;
            _speed = speed;
            _active = true;
            if (_renderer != null) _renderer.material.color = color;
        }

        private void Update()
        {
            if (!_active) return;
            if (_cell == null) { Despawn(); return; } // cell đã bị phá bởi đạn khác

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
