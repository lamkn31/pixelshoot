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

        [Header("Nòng bắn (2 bên)")]
        [Tooltip("Điểm đạn xuất phát của nòng BÊN PHẢI (+X local của gun). Bỏ trống → bắn từ gốc gun.")]
        [SerializeField] private Transform muzzleRight;
        [Tooltip("Điểm đạn xuất phát của nòng BÊN TRÁI (−X local của gun). Bỏ trống → bắn từ gốc gun.")]
        [SerializeField] private Transform muzzleLeft;
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
        private Renderer[] _renderers;
        private Coroutine _moveRoutine;
        private Pooler<Gun> _pool;
        private RoundedPolylineFollower _follower;
        private int _lastLap;             // vòng path đã chạy, mốc để mở khoá bắn

        /// <summary>
        /// Một bên nòng. Mỗi bên có target + nhịp bắn RIÊNG và quạt hướng ra sườn gun (±X local), nên
        /// gun chạy dọc path là quét được cả 2 phía cùng lúc mà không phải quay mặt.
        /// </summary>
        private class Barrel
        {
            public float Sign;        // +1 = phải (+X local), −1 = trái (−X local)
            public Transform Muzzle;
            public BlockCell Target;
            public int TargetGen;     // Generation lúc chốt — lệch = object pool đã thành cell khác
            public float FireTimer;
            public bool Armed;        // còn lượt bắn của vòng này không
            public bool HadTarget;    // vòng này đã bắt được cell nào chưa
            public float IdleTimer;   // quạt trống liên tục bao lâu rồi
        }

        private readonly Barrel _right = new Barrel { Sign = 1f };
        private readonly Barrel _left = new Barrel { Sign = -1f };

        /// <summary>Target của nòng còn sống và đúng màu — object pooled có thể đã thành cell khác.</summary>
        private bool HasLiveTarget(Barrel b) => b.Target != null && b.Target.Generation == b.TargetGen
                                                && !b.Target.IsEmpty && b.Target.Color == Data.Color;

        /// <summary>Góc toả tối đa của 1 nòng: quá 180° là đã kín nửa mặt phẳng của nó, không thêm được gì.</summary>
        private float Spread => Mathf.Clamp(_fireAngle, 0f, 180f);

        public void OnInitializedInPool(Pooler<Gun> pool) => _pool = pool;

        private void Awake()
        {
            // Tắt follower khi ở slot; PathManager bật lại (DeployOnPath) khi gun được click lên path.
            _follower = GetComponentInChildren<RoundedPolylineFollower>(true);
            if (_follower != null) _follower.enabled = false;

            _right.Muzzle = muzzleRight;
            _left.Muzzle = muzzleLeft;

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

        public void Init(GunData data, float fireInterval, float fireRange, float fireAngle, float bulletSpeed)
        {
            Data = new GunData { Color = data.Color, CountBullet = data.CountBullet };
            _fireInterval = fireInterval;
            _fireRange = fireRange;
            _fireAngle = fireAngle;
            _bulletSpeed = bulletSpeed;

            // Reset trạng thái (item pooled có thể tái dùng).
            _lastLap = 0;
            ResetBarrel(_right);
            ResetBarrel(_left);
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
            ResetBarrel(_right);
            ResetBarrel(_left);
        }

        public void OnDeployed()
        {
            _state = GunState.OnPath;
            Slot = null;
            transform.SetParent(null);
            ResetBarrel(_right);
            ResetBarrel(_left);
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

            // Mỗi vòng path, MỖI NÒNG chỉ được 1 lượt bắn. Về tới pos 0 (xong 1 vòng) = mở khoá lượt mới.
            int lap = _follower != null ? _follower.LapCount : 0;
            if (lap != _lastLap) { _lastLap = lap; ArmForNewLap(); }

            // Nòng phải chạy trước, rồi tới nòng trái — mỗi bên nhận target của bên kia làm danh sách
            // loại trừ nên 2 nòng không bao giờ nhắm trùng 1 cell.
            TickBarrel(_right, _left.Target);
            if (_state != GunState.OnPath) return; // hết đạn giữa chừng → Die() đã despawn gun
            TickBarrel(_left, _right.Target);
        }

        /// <summary>Chạy 1 bên nòng.</summary>
        private void TickBarrel(Barrel b, BlockCell otherTarget)
        {
            // Hết lượt của vòng này → nòng im lặng suốt quãng quay lại pos 0, KHÔNG nhặt cell mới dọc đường.
            if (!b.Armed) return;

            // DỨT ĐIỂM TỪNG CELL: chỉ chọn cell khác khi cell đang bám đã bị phá HẾT. Quạt KHÔNG được
            // dùng làm điều kiện đổi target — làm vậy là nòng bỏ dở cột này nhảy sang cột kia.
            if (!HasLiveTarget(b))
            {
                b.Target = GridBlockManager.Instance?.FindTargetCell(
                    Data.Color, transform.position, transform.forward, b.Sign, _fireRange, _fireAngle, otherTarget);
                b.TargetGen = b.Target != null ? b.Target.Generation : 0;
            }

            // Quạt là điều kiện BẮN: cell trôi ra ngoài quạt thì ngừng bắn, không đổi sang cell khác.
            bool inZone = InDetectZone(b.Target, b);

            if (inZone) { b.HadTarget = true; b.IdleTimer = 0f; }
            else if (b.HadTarget)
            {
                // Nòng này đã bắn được trong vòng rồi mà giờ quạt trống → nó đã đi qua grid của mình.
                // HẾT LƯỢT: khoá tới hết vòng. Khoá theo TỪNG NÒNG chứ không theo cả gun — gộp chung thì
                // chỉ cần 1 nòng còn thấy cell là gun không bao giờ khoá, và bắn suốt cả quãng quay lại.
                // Chờ targetLostGrace mới khoá: cột đang dồn thì cell nào cũng PendingEntry, quạt trống
                // trong chốc lát là bình thường, khoá ngay là nòng chết oan giữa cột.
                b.IdleTimer += Time.deltaTime;
                if (b.IdleTimer >= targetLostGrace) { b.Armed = false; return; }
            }

            b.FireTimer -= Time.deltaTime;
            // Chỉ bắn khi cell còn block CHƯA bị đạn đang bay đặt chỗ (tránh bắn dư).
            if (inZone && b.FireTimer <= 0f && b.Target.Available > 0)
            {
                Fire(b);
                b.FireTimer = _fireInterval;
            }
        }

        private static void ResetBarrel(Barrel b)
        {
            b.Target = null;
            b.TargetGen = 0;
            b.FireTimer = 0f;
            b.Armed = true;
            b.HadTarget = false;
            b.IdleTimer = 0f;
        }

        /// <summary>
        /// Mở khoá bắn cho 1 vòng path mới (gun vừa về tới pos 0, hoặc vừa được deploy). Bỏ luôn target
        /// cũ: vòng mới chọn lại từ đầu. Luật "dứt điểm từng cell" áp trong PHẠM VI 1 vòng — giữ target
        /// qua vòng thì nòng ôm mãi 1 cell nó không còn với tới, kẹt cứng không bao giờ bắn nữa.
        /// </summary>
        private void ArmForNewLap()
        {
            ResetBarrel(_right);
            ResetBarrel(_left);
        }

        /// <summary>
        /// Cell có nằm trong vùng bắn của nòng này không: bán kính _fireRange, quạt tính TỪ hướng trước
        /// mặt của gun (thân bám path) rồi toả sang sườn của nòng đúng <see cref="Spread"/> độ. Đo trên
        /// sàn XZ (bỏ qua chênh lệch Y).
        /// Phải khớp đúng bộ lọc của <see cref="GridBlockManager.FindTargetCell"/>, không thì target vừa
        /// chọn xong đã bị loại ngay frame sau và nòng đứng khựng chọn đi chọn lại.
        /// </summary>
        private bool InDetectZone(BlockCell cell, Barrel b)
        {
            if (cell == null) return false;
            Vector3 d = cell.transform.position - transform.position; d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr > _fireRange * _fireRange) return false;
            if (sqr < 1e-6f) return true;
            if (Vector3.Dot(transform.right, d) * b.Sign < 0f) return false; // sai sườn
            return Vector3.Dot(transform.forward, d) >= Mathf.Cos(Spread * Mathf.Deg2Rad) * Mathf.Sqrt(sqr);
        }

        private void Fire(Barrel b)
        {
            Data.CountBullet--;
            UpdateLabel();

            b.Target.ReserveHit();
            var bullet = PoolManager.Instance != null ? PoolManager.Instance.GetBullet() : null;
            Vector3 from = b.Muzzle != null ? b.Muzzle.position : transform.position;
            if (bullet != null)
                bullet.Launch(from, b.Target, _bulletSpeed, Data.Color);
            else
            {
                b.Target.ApplyHit(); // fallback không có pool
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
        // HAI quạt bắn: mỗi nòng quét TỪ hướng trước mặt (thân gun bám path) toả sang sườn của nó Spread độ.
        // VÀNG = còn lượt bắn của vòng này; XÁM = đã bắn xong, đang trên đường quay về pos 0.
        // Đường tới target: XANH = trong quạt, đang bắn. ĐỎ = cell còn sống nhưng đã ra ngoài quạt —
        // nòng vẫn GIỮ nó (dứt điểm từng cell) và chờ vào lại quạt để bắn tiếp.
        private void OnDrawGizmos()
        {
            //if (_state == GunState.Dead) return;

            //Handles.color = _state == GunState.OnPath && !_fireArmed
            //    ? new Color(0.5f, 0.5f, 0.5f, 0.5f)   // hết lượt: quạt xám
            //    : new Color(1f, 0.85f, 0.2f, 0.9f);

            //// Góc âm = quét ngược chiều → nòng trái toả sang trái, nòng phải sang phải, chung mép ở forward.
            //Handles.DrawSolidArc(transform.position, Vector3.up, transform.forward, Spread * _right.Sign, _fireRange);
            //Handles.DrawSolidArc(transform.position, Vector3.up, transform.forward, Spread * _left.Sign, _fireRange);

            //if (_state != GunState.OnPath) return;
            //DrawTargetLine(_right);
            //DrawTargetLine(_left);
        }

        private void DrawTargetLine(Barrel b)
        {
            if (b.Target == null) return;
            Gizmos.color = InDetectZone(b.Target, b) ? UnityEngine.Color.green : UnityEngine.Color.red;
            Gizmos.DrawLine(transform.position, b.Target.transform.position);
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
