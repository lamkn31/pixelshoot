using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Gun: nằm trong slot → click để ra path → chạy loop liên tục (RoundedPolylineFollower) và tự bắn
    /// cell ngoài cùng gần nhất cùng màu TRONG TẦM BẮN; hết đạn thì biến mất (yêu cầu #3, #7).
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

        private GunState _state = GunState.InSlot;
        private float _fireInterval = 0.25f;
        private float _fireRange = 3f;
        private float _bulletSpeed = 14f;
        private float _fireTimer;
        private BlockCell _currentTarget;
        private int _lockedCol = -1;   // cột đang cam kết ăn dứt (BlockCol); -1 = chưa cam kết
        private Renderer _renderer;
        private TextMesh _label;
        private Coroutine _moveRoutine;
        private Pooler<Gun> _pool;

        public void OnInitializedInPool(Pooler<Gun> pool) => _pool = pool;

        private void Awake()
        {
            // Tắt follower khi ở slot; PathManager bật lại (DeployOnPath) khi gun được click lên path.
            var follower = GetComponentInChildren<RoundedPolylineFollower>(true);
            if (follower != null) follower.enabled = false;

            // Collider của prefab thường nằm ở CHILD (vd "Model") → OnMouseDown gửi tới child, không tới
            // script Gun ở root. Gắn relay lên mọi collider để forward click về đây (yêu cầu click→deploy).
            foreach (var col in GetComponentsInChildren<Collider>(true))
            {
                var relay = col.GetComponent<GunClickRelay>();
                if (relay == null) relay = col.gameObject.AddComponent<GunClickRelay>();
                relay.Owner = this;
            }
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
            _lockedCol = -1;
            if (_moveRoutine != null) { StopCoroutine(_moveRoutine); _moveRoutine = null; }

            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null) _renderer.material.color = BlockColorPalette.ToColor(Data.Color);

            EnsureLabel();
            UpdateLabel();
            _state = GunState.InSlot;

            // Item pooled tái dùng: tắt follower để gun đứng yên trong slot (bật lại khi deploy).
            var follower = GetComponentInChildren<RoundedPolylineFollower>(true);
            if (follower != null) follower.enabled = false;
        }

        public void SetSlot(GunSlot s) => Slot = s;

        // Được gọi từ GunClickRelay (collider ở child) hoặc trực tiếp nếu collider nằm cùng GO.
        public void HandleClick()
        {
            if (_state == GunState.InSlot) SlotManager.Instance?.OnGunClicked(this);
        }

        private void OnMouseDown() => HandleClick();

        public void OnDeployed()
        {
            _state = GunState.OnPath;
            Slot = null;
            transform.SetParent(null);
            _fireTimer = 0f;
        }

        /// <summary>Bật RoundedPolylineFollower cho gun chạy vòng path liên tục từ startDistance (yêu cầu #3).</summary>
        public void DeployOnPath(RoundedPolylinePath path, float startDistance, float speed)
        {
            var follower = GetComponentInChildren<RoundedPolylineFollower>(true);
            if (follower != null) { follower.Init(path, startDistance, speed); follower.enabled = true; }
            else if (path != null) transform.position = path.GetPointAtDistance(startDistance); // gun ko có follower
        }

        private void Update()
        {
            if (_state != GunState.OnPath) return;

            // Bám target tới khi cell bị phá HẾT rồi mới chọn cell khác (yêu cầu: dứt điểm từng cell).
            // Cell bị clear sẽ Destroy → reference == null, nên chỉ cần check null/IsEmpty.
            // Cell Spawner đẩy cell kế ra TẠI CHỖ và có thể ĐỔI MÀU → phải bỏ khoá nếu màu hết khớp.
            if (_currentTarget == null || _currentTarget.IsEmpty || _currentTarget.Color != Data.Color)
            {
                _currentTarget = GridBlockManager.Instance?.FindTargetCell(
                    Data.Color, transform.position, _fireRange, _lockedCol);
                // Cột cạn (không còn cell cùng màu bắn được) → nhả cam kết; muốn ăn cột mới phải lọt
                // vòng phát hiện lần nữa, nên gun đã chạy qua grid sẽ không bắn cell vừa xuất hiện.
                _lockedCol = _currentTarget != null ? _currentTarget.BlockCol : -1;
            }

            _fireTimer -= Time.deltaTime;
            // KHÔNG gate theo range: range chỉ dùng để PHÁT HIỆN target (xem FindTargetCell). Đã chốt được
            // cell thì bắn dứt điểm rồi đi tiếp xuống cột, dù gun đã chạy ra xa.
            // Chỉ bắn khi cell còn block CHƯA bị đạn đang bay đặt chỗ (tránh bắn dư đạn).
            if (_fireTimer <= 0f && _currentTarget != null && _currentTarget.Available > 0)
            {
                Fire(_currentTarget);
                _fireTimer = _fireInterval;
            }
        }

        /// <summary>Cell có nằm trong vòng PHÁT HIỆN không (hình tròn trên sàn XZ, bỏ qua chênh lệch Y).</summary>
        private bool InRange(BlockCell cell)
        {
            Vector3 d = cell.transform.position - transform.position; d.y = 0f;
            return d.sqrMagnitude <= _fireRange * _fireRange;
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
        // Vòng vàng = tầm PHÁT HIỆN (chỉ dùng để bắt cell hàng 0 sát path). Đường tới target: xanh = target
        // còn trong vòng phát hiện, đỏ = đã ra ngoài — vẫn bắn bình thường (không gate theo range).
        private void OnDrawGizmos()
        {
            if (_state == GunState.Dead) return;

            Handles.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Handles.DrawWireDisc(transform.position, Vector3.up, _fireRange); // vòng phát hiện trên sàn XZ

            if (_state == GunState.OnPath && _currentTarget != null)
            {
                Gizmos.color = InRange(_currentTarget) ? UnityEngine.Color.green : UnityEngine.Color.red;
                Gizmos.DrawLine(transform.position, _currentTarget.transform.position);
            }
        }
#endif
    }

    /// <summary>
    /// Gắn lên GameObject có Collider (thường là child "Model" của gun) để forward OnMouseDown về
    /// <see cref="Gun"/> ở root — vì OnMouseDown chỉ gọi trên GO chứa collider, không lan lên parent.
    /// </summary>
    public class GunClickRelay : MonoBehaviour
    {
        public Gun Owner;
        private void OnMouseDown() { if (Owner != null) Owner.HandleClick(); }
    }
}
