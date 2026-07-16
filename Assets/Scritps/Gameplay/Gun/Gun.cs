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
        private enum GunState { InSlot, Queued, OnPath, Dead }

        public GunData Data { get; private set; }
        public TypeColor Color => Data.Color;
        public GunSlot Slot { get; private set; }

        [Header("Ngắm bắn")]
        [Tooltip("Nòng súng: xoay mặt về target khi có, không có thì trùng hướng path. Bỏ trống sẽ tự tìm " +
                 "child có tên chứa 'canon'. Thân 'machine' không cần gán — nó bám hướng path theo root.")]
        [SerializeField] private Transform canon;
        [Tooltip("Điểm đạn xuất phát. Nên đặt ở đầu nòng (child của canon). Bỏ trống → dùng canon, " +
                 "không có canon → gốc gun.")]
        [SerializeField] private Transform muzzle;
        [Tooltip("Tốc độ xoay nòng (độ/giây). Để 0 = quay tức thì sang target.")]
        [SerializeField] private float canonTurnSpeed = 540f;

        /// <summary>Arc-length hiện tại trên path — PathManager đọc để giữ khoảng cách giữa các gun.</summary>
        public float PathDistance => _follower != null ? _follower.CurrentDistance : 0f;
        public bool IsOnPath => _state == GunState.OnPath;

        private GunState _state = GunState.InSlot;
        private float _fireInterval = 0.25f;
        private float _fireRange = 3f;
        private float _fireAngle = 360f; // góc quạt phát hiện; 360 = quét tròn
        private float _bulletSpeed = 14f;
        private float _fireTimer;
        private BlockCell _currentTarget;
        private int _targetGen; // Generation của target lúc chốt — lệch = object pool đã thành cell khác
        private Renderer _renderer;
        private TextMesh _label;
        private Coroutine _moveRoutine;
        private Pooler<Gun> _pool;
        private RoundedPolylineFollower _follower;
        private Quaternion _canonRestLocalRot = Quaternion.identity; // pose gốc của model nòng
        private float _aimYaw;                                       // góc lệch nòng so với thân gun
        private int _lastLap;                                        // vòng path đã chạy, để reset target

        /// <summary>Target còn sống và đúng màu — object pooled có thể đã thành cell khác (Generation).</summary>
        private bool HasLiveTarget => _currentTarget != null && _currentTarget.Generation == _targetGen
                                      && !_currentTarget.IsEmpty && _currentTarget.Color == Data.Color;

        /// <summary>Điểm xuất phát của đạn: muzzle → canon → gốc gun.</summary>
        private Vector3 MuzzlePosition =>
            muzzle != null ? muzzle.position : (canon != null ? canon.position : transform.position);

        public void OnInitializedInPool(Pooler<Gun> pool) => _pool = pool;

        private void Awake()
        {
            // Tắt follower khi ở slot; PathManager bật lại (DeployOnPath) khi gun được click lên path.
            _follower = GetComponentInChildren<RoundedPolylineFollower>(true);
            if (_follower != null) _follower.enabled = false;

            if (canon == null) canon = FindChildByName("canon");
            if (canon != null) _canonRestLocalRot = canon.localRotation;

            // Collider của prefab thường nằm ở CHILD (vd "Model") → OnMouseDown gửi tới child, không tới
            // script Gun ở root. Gắn relay lên mọi collider để forward click về đây (yêu cầu click→deploy).
            foreach (var col in GetComponentsInChildren<Collider>(true))
            {
                var relay = col.GetComponent<GunClickRelay>();
                if (relay == null) relay = col.gameObject.AddComponent<GunClickRelay>();
                relay.Owner = this;
            }
        }

        private Transform FindChildByName(string keyword)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t != transform && t.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            return null;
        }

        public void Init(GunData data, float fireInterval, float fireRange, float fireAngle, float bulletSpeed)
        {
            Data = new GunData { Color = data.Color, CountBullet = data.CountBullet };
            _fireInterval = fireInterval;
            _fireRange = fireRange;
            _fireAngle = fireAngle;
            _bulletSpeed = bulletSpeed;

            // Reset trạng thái (item pooled có thể tái dùng).
            _fireTimer = 0f;
            _currentTarget = null;
            _lastLap = 0;
            if (_moveRoutine != null) { StopCoroutine(_moveRoutine); _moveRoutine = null; }

            // Material lấy từ GlobalConfigManager theo TypeColor (không tô material.color nữa).
            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
            var mat = GlobalConfigManager.MaterialOf(Data.Color, TypeObject.Gun);
            if (_renderer != null && mat != null) _renderer.sharedMaterial = mat;

            EnsureLabel();
            UpdateLabel();
            _state = GunState.InSlot;

            // Item pooled tái dùng: trả nòng về pose gốc, không thì nó giữ góc ngắm của lượt trước.
            _aimYaw = 0f;
            if (canon != null) canon.localRotation = _canonRestLocalRot;

            // Item pooled tái dùng: tắt follower để gun đứng yên trong slot (bật lại khi deploy).
            if (_follower != null) _follower.enabled = false;
        }

        public void SetSlot(GunSlot s) => Slot = s;

        // Được gọi từ GunClickRelay (collider ở child) hoặc trực tiếp nếu collider nằm cùng GO.
        public void HandleClick()
        {
            if (_state == GunState.InSlot) SlotManager.Instance?.OnGunClicked(this);
        }

        private void OnMouseDown() => HandleClick();

        /// <summary>Tách khỏi slot nhưng CHƯA lên path — gun đang xếp hàng chờ đủ khoảng cách.</summary>
        public void OnQueued()
        {
            _state = GunState.Queued;
            Slot = null;
            transform.SetParent(null);
            _fireTimer = 0f;
        }

        public void OnDeployed()
        {
            _state = GunState.OnPath;
            Slot = null;
            transform.SetParent(null);
            _fireTimer = 0f;
            // Gun chờ trong queue được MoveTo tới chỗ đứng; coroutine đó vẫn ghi transform.position mỗi
            // frame → phải dừng, không thì nó giành position với follower.
            if (_moveRoutine != null) { StopCoroutine(_moveRoutine); _moveRoutine = null; }
        }

        /// <summary>Bật RoundedPolylineFollower cho gun chạy vòng path liên tục từ startDistance (yêu cầu #3).</summary>
        public void DeployOnPath(RoundedPolylinePath path, float startDistance, float speed)
        {
            _lastLap = 0; // follower.Init đưa LapCount về 0 — mốc đếm vòng bắt đầu từ đây
            if (_follower != null) { _follower.Init(path, startDistance, speed); _follower.enabled = true; }
            else if (path != null) transform.position = path.GetPointAtDistance(startDistance); // gun ko có follower
        }

        private void Update()
        {
            if (_state != GunState.OnPath) return;

            // Chạy trọn 1 vòng về lại điểm vào → BỎ target đang bám, chọn lại từ đầu. Range/góc chỉ lọc
            // lúc CHỌN target nên không reset thì gun ôm mãi 1 cell ở tận đầu kia path, đi hết vòng này
            // qua vòng khác vẫn bắn nó thay vì ăn cell ngay trước mặt.
            int lap = _follower != null ? _follower.LapCount : 0;
            if (lap != _lastLap) { _lastLap = lap; _currentTarget = null; }

            // Bám target tới khi cell bị phá HẾT rồi mới chọn cell khác (yêu cầu: dứt điểm từng cell).
            // Cell là item POOLED: object có thể bị tái dùng cho cell mới ngay trong cùng frame (nhả ở ô
            // sâu) → check Generation, lệch = target cũ đã chết, không bám theo object ra ô mới.
            if (!HasLiveTarget)
            {
                _currentTarget = GridBlockManager.Instance?.FindTargetCell(
                    Data.Color, transform.position, transform.forward, _fireRange, _fireAngle);
                _targetGen = _currentTarget != null ? _currentTarget.Generation : 0;
            }

            _fireTimer -= Time.deltaTime;
            // Range chỉ lọc lúc CHỌN target (xem FindTargetCell); đã chốt được cell thì bắn dứt điểm kể cả
            // khi gun đã trôi ra xa. Chỉ bắn khi cell còn block CHƯA bị đạn đang bay đặt chỗ (tránh bắn dư).
            if (_fireTimer <= 0f && _currentTarget != null && _currentTarget.Available > 0)
            {
                Fire(_currentTarget);
                _fireTimer = _fireInterval;
            }
        }

        /// <summary>
        /// Xoay nòng về target (không có target thì trả nòng về hướng path). Chạy ở LateUpdate vì
        /// RoundedPolylineFollower set rotation của root trong Update — phải đợi nó xong mới tính đúng
        /// góc lệch giữa thân gun và target, không thì nòng giật theo thứ tự Update.
        /// </summary>
        private void LateUpdate()
        {
            if (canon == null || _state != GunState.OnPath) return;

            float desiredYaw = 0f; // 0 = nòng trùng hướng thân gun = hướng path
            if (HasLiveTarget)
            {
                Vector3 dir = _currentTarget.transform.position - canon.position;
                dir.y = 0f; // chỉ xoay trên sàn XZ, không chúc nòng lên/xuống
                if (dir.sqrMagnitude > 1e-6f)
                    desiredYaw = Vector3.SignedAngle(transform.forward, dir, Vector3.up);
            }

            _aimYaw = canonTurnSpeed > 0f
                ? Mathf.MoveTowardsAngle(_aimYaw, desiredYaw, canonTurnSpeed * Time.deltaTime)
                : desiredYaw;

            // Xoay quanh Y theo GÓC LỆCH rồi mới nhân pose gốc của model → không phụ thuộc model nòng
            // quay mặt về trục local nào (canon fbx có offset riêng, machine thì lệch -90 quanh Z).
            canon.rotation = Quaternion.AngleAxis(_aimYaw, Vector3.up) * transform.rotation * _canonRestLocalRot;
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
                bullet.Launch(MuzzlePosition, cell, _bulletSpeed, Data.Color);
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
        // Quạt vàng = tầm PHÁT HIỆN (bán kính _fireRange, mở _fireAngle độ quanh hướng thân gun). Chỉ dùng
        // để bắt cell hàng 0 sát path. Đường tới target: xanh = còn trong quạt, đỏ = đã ra ngoài — vẫn bắn
        // bình thường (không gate theo range sau khi đã chốt target).
        private void OnDrawGizmos()
        {
            if (_state == GunState.Dead) return;

            Handles.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            if (_fireAngle >= 360f)
                Handles.DrawWireDisc(transform.position, Vector3.up, _fireRange);
            else
            {
                Vector3 from = Quaternion.AngleAxis(-_fireAngle * 0.5f, Vector3.up) * transform.forward;
                Handles.DrawSolidArc(transform.position, Vector3.up, from, _fireAngle, _fireRange);
            }

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
