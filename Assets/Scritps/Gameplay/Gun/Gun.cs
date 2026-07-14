using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Gun: nằm trong slot → click để ra path → chạy loop và tự bắn cell ngoài cùng gần nhất
    /// cùng màu TRONG TẦM BẮN; hết đạn thì biến mất (yêu cầu #3, #7). Di chuyển do PathManager điều khiển.
    /// </summary>
    public class Gun : MonoBehaviour, IItemPool<Gun>
    {
        private enum GunState { InSlot, OnPath, Dead }

        public GunData Data { get; private set; }
        public BlockColor Color => Data.Color;
        public GunSlot Slot { get; private set; }

        /// <summary>Khoảng cách tích luỹ trên path (PathManager ghi/đọc).</summary>
        public float Distance;
        public bool IsOnPath => _state == GunState.OnPath;

        /// <summary>Đang animate từ slot lên path — PathManager tạm không điều khiển vị trí.</summary>
        public bool IsEntering { get; private set; }

        private GunState _state = GunState.InSlot;
        private float _fireInterval = 0.25f;
        private float _fireRange = 3f;
        private float _bulletSpeed = 14f;
        private float _fireTimer;
        private BlockCell _currentTarget;
        private Renderer _renderer;
        private TextMesh _label;
        private Coroutine _moveRoutine;
        private Pooler<Gun> _pool;

        public void OnInitializedInPool(Pooler<Gun> pool) => _pool = pool;

        private void Awake()
        {
            // Gun tự di chuyển qua PathManager (station). Tắt follower cũ nếu prefab còn dính,
            // nếu không nó sẽ TỰ chạy path ngay khi init.
            var follower = GetComponentInChildren<RoundedPolylineFollower>(true);
            if (follower != null) follower.enabled = false;
        }

        public void Init(GunData data, float fireInterval, float fireRange, float bulletSpeed)
        {
            Data = new GunData { Color = data.Color, CountBullet = data.CountBullet };
            _fireInterval = fireInterval;
            _fireRange = fireRange;
            _bulletSpeed = bulletSpeed;

            // Reset trạng thái (item pooled có thể tái dùng).
            _fireTimer = 0f;
            _currentTarget = null;
            IsEntering = false;
            if (_moveRoutine != null) { StopCoroutine(_moveRoutine); _moveRoutine = null; }

            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null) _renderer.material.color = BlockColorPalette.ToColor(Data.Color);

            EnsureLabel();
            UpdateLabel();
            _state = GunState.InSlot;
        }

        public void SetSlot(GunSlot s) => Slot = s;

        private void OnMouseDown()
        {
            if (_state == GunState.InSlot) SlotManager.Instance?.OnGunClicked(this);
        }

        public void OnDeployed()
        {
            _state = GunState.OnPath;
            Slot = null;
            transform.SetParent(null);
            _fireTimer = 0f;
        }

        /// <summary>Bắt đầu animate gun từ vị trí slot lên điểm vào path, xong mới cho PathManager điều khiển.</summary>
        public void BeginEntry(Vector3 target, float duration)
        {
            IsEntering = true;
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            if (!gameObject.activeInHierarchy || duration <= 0f) { transform.position = target; IsEntering = false; return; }
            _moveRoutine = StartCoroutine(EntryRoutine(target, duration));
        }

        private IEnumerator EntryRoutine(Vector3 target, float dur)
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
            IsEntering = false;
            _moveRoutine = null;
        }

        private void Update()
        {
            if (_state != GunState.OnPath || IsEntering) return;

            // Luôn cập nhật target gần nhất cùng màu (để vẽ gizmo & quyết định bắn).
            _currentTarget = GridBlockManager.Instance?.FindTargetCell(Data.Color, transform.position);

            _fireTimer -= Time.deltaTime;
            // Chỉ bắn khi cell còn block CHƯA bị đạn đang bay đặt chỗ (tránh bắn dư đạn).
            if (_fireTimer <= 0f && _currentTarget != null && _currentTarget.Available > 0 && InRange(_currentTarget))
            {
                Fire(_currentTarget);
                _fireTimer = _fireInterval;
            }
        }

        private bool InRange(BlockCell cell)
        {
            return (cell.transform.position - transform.position).sqrMagnitude <= _fireRange * _fireRange;
        }

        private void Fire(BlockCell cell)
        {
            Data.CountBullet--;
            UpdateLabel();

            cell.ReserveHit();
            var bullet = PoolManager.Instance != null ? PoolManager.Instance.GetBullet() : null;
            if (bullet != null)
                bullet.Launch(transform.position, cell, _bulletSpeed, BlockColorPalette.ToColor(Data.Color));
            else
            {
                cell.ApplyHit(); // fallback không có pool
                GameController.Instance?.OnBoardChanged();
            }

            if (Data.CountBullet <= 0) Die();
        }

        private void Die()
        {
            _state = GunState.Dead;
            PathManager.Instance?.RemoveGun(this);
            GameController.Instance?.OnBoardChanged();
            Despawn();
        }

        private void Despawn()
        {
            if (_pool != null) _pool.Release(this);
            else Destroy(gameObject);
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

        private void EnsureLabel()
        {
            _label = GetComponentInChildren<TextMesh>();
            if (_label != null) return;

            var go = new GameObject("BulletLabel");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.6f);
            go.transform.localScale = Vector3.one * 0.15f;
            _label = go.AddComponent<TextMesh>();
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.fontSize = 64;
            _label.color = UnityEngine.Color.black;
        }

        private void UpdateLabel()
        {
            if (_label != null) _label.text = Data.CountBullet.ToString();
        }

#if UNITY_EDITOR
        // Vẽ tầm bắn (vòng vàng) + đường tới target (xanh = trong tầm, đỏ = ngoài tầm) để kiểm tra.
        private void OnDrawGizmos()
        {
            if (_state == GunState.Dead) return;

            Handles.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Handles.DrawWireDisc(transform.position, Vector3.up, _fireRange); // tầm bắn trên sàn XZ

            if (_state == GunState.OnPath && _currentTarget != null)
            {
                bool inRange = InRange(_currentTarget);
                Gizmos.color = inRange ? UnityEngine.Color.green : UnityEngine.Color.red;
                Gizmos.DrawLine(transform.position, _currentTarget.transform.position);
            }
        }
#endif
    }
}
