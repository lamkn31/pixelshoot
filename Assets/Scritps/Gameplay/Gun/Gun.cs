using System.Collections;
using System.Collections.Generic;
using TMPro;
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
        [Tooltip("Mất target bao lâu (giây) thì coi như quạt đã trôi qua grid → khoá bắn tới hết vòng. " +
                 "PHẢI lớn hơn GameSettings.BlockCollapseDuration: lúc cột đang dồn mọi cell đều " +
                 "PendingEntry nên target hụt trong chốc lát là bình thường, khoá ngay là gun chết oan giữa cột.")]
        [SerializeField] private float targetLostGrace = 0.4f;

        [Header("Hiển thị")]
        [Tooltip("Text đếm số đạn — gán sẵn trên prefab ('Text (TMP)'). Bỏ trống sẽ tự tìm TMP_Text " +
                 "trong children.")]
        [SerializeField] private TMP_Text bulletLabel;

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
        private Renderer[] _renderers;
        private Coroutine _moveRoutine;
        private Pooler<Gun> _pool;
        private RoundedPolylineFollower _follower;
        private Quaternion _canonRestLocalRot = Quaternion.identity; // pose gốc của model nòng
        private float _aimYaw;                                       // góc lệch nòng so với thân gun
        private int _lastLap;             // vòng path đã chạy, mốc để mở khoá bắn
        private bool _fireArmed = true;   // vòng này còn được bắn không (false = đang trên đường về pos 0)
        private bool _hadTarget;          // vòng này đã bắt được target chưa
        private float _noTargetTimer;     // mất target liên tục bao lâu rồi

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

            CollectRenderers();

            // Collider của prefab thường nằm ở CHILD (vd "Model") → OnMouseDown gửi tới child, không tới
            // script Gun ở root. Gắn relay lên mọi collider để forward click về đây (yêu cầu click→deploy).
            foreach (var col in GetComponentsInChildren<Collider>(true))
            {
                var relay = col.GetComponent<GunClickRelay>();
                if (relay == null) relay = col.gameObject.AddComponent<GunClickRelay>();
                relay.Owner = this;
            }
        }

        /// <summary>
        /// Gom MỌI renderer của model để tô màu theo TypeColor — trước đây chỉ lấy renderer ĐẦU TIÊN
        /// (thân 'machine') nên nòng canon không bao giờ được tô.
        /// Loại renderer của TMP_Text ra: nó dùng material font, đè material gun vào là mất chữ.
        /// </summary>
        private void CollectRenderers()
        {
            var list = new List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.GetComponent<TMP_Text>() == null) list.Add(r);
            _renderers = list.ToArray();
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
            _lastLap = 0;
            ArmForNewLap();
            if (_moveRoutine != null) { StopCoroutine(_moveRoutine); _moveRoutine = null; }

            // Material lấy từ GlobalConfigManager theo TypeColor (không tô material.color nữa).
            // sharedMaterial chỉ thay slot 0 — 'machine' có 2 slot, slot 1 (viền/chi tiết) giữ nguyên.
            if (_renderers == null) CollectRenderers();
            var mat = GlobalConfigManager.MaterialOf(Data.Color, TypeObject.Gun);
            if (mat != null)
                foreach (var r in _renderers) if (r != null) r.sharedMaterial = mat;

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
            _lastLap = 0;   // follower.Init đưa LapCount về 0 — mốc đếm vòng bắt đầu từ đây
            ArmForNewLap(); // vào path tại pos 0 = bắt đầu lượt bắn đầu tiên
            if (_follower != null) { _follower.Init(path, startDistance, speed); _follower.enabled = true; }
            else if (path != null) transform.position = path.GetPointAtDistance(startDistance); // gun ko có follower
        }

        private void Update()
        {
            if (_state != GunState.OnPath) return;

            // Mỗi vòng path gun chỉ được MỘT lượt bắn. Về tới pos 0 (xong 1 vòng) = mở khoá lượt mới.
            int lap = _follower != null ? _follower.LapCount : 0;
            if (lap != _lastLap) { _lastLap = lap; ArmForNewLap(); }

            // Đã bắn xong lượt của vòng này → im lặng trên cả quãng đường quay lại pos 0. Bỏ target luôn
            // để nòng tự quay về hướng path (xem LateUpdate).
            if (!_fireArmed) { _currentTarget = null; return; }

            // Bám target tới khi cell bị phá hết, HOẶC tới khi cell rơi ra ngoài quạt — quạt trôi qua grid
            // rồi thì buông grid đó, không bắn với theo nữa. Cell là item POOLED: object có thể bị tái dùng
            // cho cell mới ngay trong cùng frame (nhả ở ô sâu) → check Generation, lệch = target cũ đã chết.
            if (!HasLiveTarget || !InDetectZone(_currentTarget))
            {
                _currentTarget = GridBlockManager.Instance?.FindTargetCell(
                    Data.Color, transform.position, transform.forward, _fireRange, _fireAngle);
                _targetGen = _currentTarget != null ? _currentTarget.Generation : 0;
            }

            if (_currentTarget != null) { _hadTarget = true; _noTargetTimer = 0f; }
            else if (_hadTarget)
            {
                // Đã bắn được rồi mà giờ mất target → quạt đã trôi qua grid, hết lượt của vòng này.
                // Chờ targetLostGrace mới khoá: cột đang dồn thì cell nào cũng PendingEntry, target hụt
                // trong chốc lát là bình thường, khoá ngay là gun chết oan giữa cột.
                _noTargetTimer += Time.deltaTime;
                if (_noTargetTimer >= targetLostGrace) { _fireArmed = false; return; }
            }

            _fireTimer -= Time.deltaTime;
            // Target luôn nằm trong quạt (vừa lọc ở trên) nên không cần gate lại ở đây. Chỉ bắn khi cell
            // còn block CHƯA bị đạn đang bay đặt chỗ (tránh bắn dư).
            if (_fireTimer <= 0f && _currentTarget != null && _currentTarget.Available > 0)
            {
                Fire(_currentTarget);
                _fireTimer = _fireInterval;
            }
        }

        /// <summary>Mở khoá bắn cho 1 vòng path mới (gun vừa về tới pos 0, hoặc vừa được deploy).</summary>
        private void ArmForNewLap()
        {
            _fireArmed = true;
            _hadTarget = false;
            _noTargetTimer = 0f;
            _currentTarget = null;
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

        /// <summary>
        /// Cell có nằm trong vùng PHÁT HIỆN không: quạt _fireAngle độ quanh hướng thân gun, bán kính
        /// _fireRange, đo trên sàn XZ (bỏ qua chênh lệch Y). Phải khớp đúng bộ lọc của
        /// <see cref="GridBlockManager.FindTargetCell"/>, không thì target vừa chọn xong đã bị loại ngay.
        /// </summary>
        private bool InDetectZone(BlockCell cell)
        {
            if (cell == null) return false;
            Vector3 d = cell.transform.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > _fireRange * _fireRange) return false;
            if (_fireAngle >= 360f || d.sqrMagnitude < 1e-6f) return true;
            return Vector3.Angle(transform.forward, d) <= _fireAngle * 0.5f;
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

        // Text đã có sẵn trên prefab → chỉ tìm và bật, KHÔNG sinh thêm. includeInactive: prefab để
        // 'Text (TMP)' tắt sẵn nên GetComponentInChildren mặc định sẽ không thấy.
        private void EnsureLabel()
        {
            if (bulletLabel == null) bulletLabel = GetComponentInChildren<TMP_Text>(true);
            if (bulletLabel != null && !bulletLabel.gameObject.activeSelf)
                bulletLabel.gameObject.SetActive(true);
        }

        private void UpdateLabel()
        {
            if (bulletLabel != null) bulletLabel.text = Data.CountBullet.ToString();
        }

#if UNITY_EDITOR
        // Quạt = vùng PHÁT HIỆN (bán kính _fireRange, mở _fireAngle độ quanh hướng thân gun): gun chỉ bắt
        // VÀ chỉ bắn cell nằm trong đó. VÀNG = còn lượt bắn của vòng này; XÁM = đã bắn xong, đang trên
        // đường quay về pos 0 (im lặng tới hết vòng). Đường xanh = đang bám target.
        private void OnDrawGizmos()
        {
            if (_state == GunState.Dead) return;

            Handles.color = _state == GunState.OnPath && !_fireArmed
                ? new Color(0.5f, 0.5f, 0.5f, 0.5f)   // hết lượt: quạt xám
                : new Color(1f, 0.85f, 0.2f, 0.9f);
            if (_fireAngle >= 360f)
                Handles.DrawWireDisc(transform.position, Vector3.up, _fireRange);
            else
            {
                Vector3 from = Quaternion.AngleAxis(-_fireAngle * 0.5f, Vector3.up) * transform.forward;
                Handles.DrawSolidArc(transform.position, Vector3.up, from, _fireAngle, _fireRange);
            }

            if (_state == GunState.OnPath && _currentTarget != null)
            {
                Gizmos.color = InDetectZone(_currentTarget) ? UnityEngine.Color.green : UnityEngine.Color.red;
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
