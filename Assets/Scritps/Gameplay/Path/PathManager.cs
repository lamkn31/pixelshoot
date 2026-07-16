using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Dựng đường path (RoundedPolylinePath + mặt đường LineRenderer) từ LevelData và quản lý các gun
    /// chạy trên đó. Mọi gun vào path tại ĐIỂM ĐẦU (FrontStationDistance, mặc định 0) rồi chạy loop liên
    /// tục bằng RoundedPolylineFollower. Gun chỉ được vào khi điểm đầu còn trống ít nhất
    /// GameSettings.GunSpacing; chưa đủ thì ĐỨNG CHỜ ngay tại điểm đầu (pos 0) cho tới lượt.
    /// Thua khi đủ MaxGunOnPath mà không gun nào bắn được.
    /// </summary>
    public class PathManager : Singleton<PathManager>
    {
        [Header("Mặt đường")]
        [Tooltip("LineRenderer vẽ mặt đường — gán sẵn trên scene. Bỏ trống thì không vẽ mặt đường.")]
        [SerializeField] private LineRenderer pathLine;
        [Tooltip("Material mặt đường. Bỏ trống thì giữ material đang gán trên LineRenderer.")]
        [SerializeField] private Material pathMaterial;

        [Header("Queue")]
        [Tooltip("Thời gian (giây) gun bay từ slot ra chỗ đứng chờ ở điểm vào path (pos 0).")]
        [SerializeField] private float queueMoveDuration = 0.15f;

        private RoundedPolylinePath _path;
        private readonly List<Gun> _guns = new List<Gun>();    // [0] = gun vào trước nhất
        private readonly List<Gun> _queue = new List<Gun>();   // [0] = gun sẽ vào path kế tiếp
        private float _gunSpeed = 3f;
        private float _minGunGap = 1.2f;     // khoảng cách arc-length tối thiểu giữa 2 gun
        private float _frontStationDistance; // điểm VÀO path của mọi gun (0 = đầu path)
        private int _maxGunOnPath = 5;

        /// <summary>Gun đang chờ cũng chiếm chỗ — không cho click quá sức chứa của path.</summary>
        private int Reserved => _guns.Count + _queue.Count;

        public bool IsFull => Reserved >= _maxGunOnPath;
        public bool CanAccept => Reserved < _maxGunOnPath;
        public int GunCount => _guns.Count;
        public int QueueCount => _queue.Count;
        public RoundedPolylinePath Path => _path;

        /// <summary>Dựng path từ level rồi nạp config gun. Gọi thay cho Init(path) cũ.</summary>
        public void Build(LevelData level)
        {
            Clear();

            var gs = GameSettings.Instance;
            _gunSpeed = gs != null ? gs.GunSpeed : 3f;
            _maxGunOnPath = gs != null ? gs.MaxGunOnPath : 5;
            _frontStationDistance = gs != null ? gs.FrontStationDistance : 0f;
            _minGunGap = gs != null ? Mathf.Max(0f, gs.GunSpacing) : 1.2f;

            _path = CreatePath(level);
            ApplyPathLine(_path);
            SetPathWidth(level.PathWidth);
        }

        private RoundedPolylinePath CreatePath(LevelData level)
        {
            var go = new GameObject("GunPath");
            go.transform.SetParent(transform);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var path = go.AddComponent<RoundedPolylinePath>();
            path.isClosed = level.IsClosed;
            path.cornerRadius = level.CornerRadius;
            path.waypoints = new List<Transform>();

            for (int i = 0; i < level.PathWaypoints.Count; i++)
            {
                var wp = new GameObject("WP_" + i).transform;
                wp.SetParent(go.transform);
                wp.position = level.PathWaypoints[i];
                path.waypoints.Add(wp);
            }
            path.GeneratePath();
            return path;
        }

        // Đổ đường bo góc vào LineRenderer đã gán sẵn trên scene.
        private void ApplyPathLine(RoundedPolylinePath path)
        {
            if (pathLine == null) return;

            // Trục Z của LineRenderer phải chỉ LÊN vì dùng LineAlignment.TransformZ — không thì mặt đường
            // dựng đứng. useWorldSpace nên transform chỉ ảnh hưởng hướng mặt, không ảnh hưởng toạ độ điểm.
            pathLine.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            pathLine.alignment = LineAlignment.TransformZ;
            pathLine.useWorldSpace = true;
            pathLine.loop = false; // samples đã tự khép kín khi IsClosed → bật loop sẽ nối thừa 1 đoạn
            pathLine.numCornerVertices = 6;
            pathLine.numCapVertices = 6;
            pathLine.textureMode = LineTextureMode.Tile;
            pathLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (pathMaterial != null) pathLine.sharedMaterial = pathMaterial;

            if (path != null && path.samples != null && path.samples.Length >= 2)
            {
                pathLine.positionCount = path.samples.Length;
                pathLine.SetPositions(path.samples);
            }
            else pathLine.positionCount = 0;
        }

        /// <summary>Chỉnh độ rộng mặt đường (world units). Gọi được lúc runtime để tinh chỉnh.</summary>
        public void SetPathWidth(float width)
        {
            if (pathLine == null) return;
            pathLine.enabled = width > 0f;
            pathLine.widthMultiplier = Mathf.Max(0f, width);
        }

        /// <summary>
        /// Gun vừa rời slot: vào path ngay nếu điểm đầu còn trống, không thì xếp hàng chờ.
        /// Queue là FIFO — hàng chờ còn người thì gun mới luôn phải đứng sau, kể cả lúc đầu path trống.
        /// </summary>
        public void RequestDeploy(Gun gun)
        {
            if (gun == null) return;
            gun.OnQueued();

            if (_queue.Count == 0 && IsEntryClear()) { Deploy(gun); return; }

            _queue.Add(gun);
            StageQueued(gun);
        }

        private void Update()
        {
            if (_queue.Count == 0 || !IsEntryClear()) return;

            // Mỗi frame chỉ thả 1 gun: gun vừa vào đứng ngay điểm đầu nên IsEntryClear() lập tức false.
            var gun = _queue[0];
            _queue.RemoveAt(0);
            if (gun != null) Deploy(gun);
        }

        private void Deploy(Gun gun)
        {
            _guns.Add(gun);
            gun.OnDeployed();
            // MỌI gun đều vào path từ ĐIỂM ĐẦU (distance = FrontStationDistance, mặc định 0 = pos 0 của
            // path) rồi chạy tới. Khoảng cách giữa các gun do IsEntryClear() bảo đảm, không cộng offset
            // theo lượt deploy nữa.
            gun.DeployOnPath(_path, _frontStationDistance, _gunSpeed); // follower chạy vòng liên tục
        }

        /// <summary>Điểm vào path có gun nào đứng gần hơn _minGunGap không.</summary>
        private bool IsEntryClear()
        {
            if (_guns.Count >= _maxGunOnPath) return false;
            if (_path == null || _minGunGap <= 0f) return true;

            foreach (var g in _guns)
            {
                if (g == null) continue;
                if (ArcGap(_frontStationDistance, g.PathDistance, _path.TotalLength) < _minGunGap) return false;
            }
            return true;
        }

        /// <summary>
        /// Khoảng cách NGẮN NHẤT giữa 2 vị trí trên path, đo cả 2 chiều. Path luôn chạy vòng
        /// (GetPointAtDistance tự Mathf.Repeat) nên gun sắp lượn hết vòng về tới điểm đầu cũng phải tính
        /// là "đang chắn cửa" — không thì thả gun mới đè lên nó.
        /// </summary>
        private static float ArcGap(float a, float b, float total)
        {
            if (total <= 1e-4f) return Mathf.Abs(a - b);
            float d = Mathf.Repeat(b - a, total);
            return Mathf.Min(d, total - d);
        }

        /// <summary>
        /// Gun chờ đứng NGAY TẠI điểm vào path (pos 0) — cả hàng chờ chồng lên nhau ở đúng chỗ đó, tới
        /// lượt ai thì người đó chạy đi. Không xếp lùi dọc path nữa: điểm vào là 0 nên lùi ra sau cho
        /// distance ÂM, GetPointAtDistance wrap nó về CUỐI path — mà đoạn cuối đó là track sống, gun đang
        /// chạy vòng lao thẳng qua hàng chờ.
        /// Vị trí chờ không phụ thuộc thứ tự nên chỉ cần gọi 1 lần lúc gun vào queue.
        /// </summary>
        private void StageQueued(Gun gun)
        {
            if (gun == null || _path == null) return;

            Vector3 pos = _path.GetPointAtDistance(_frontStationDistance);
            gun.MoveTo(pos, queueMoveDuration);

            // Follower đang tắt lúc chờ → tự quay mặt gun theo hướng path để lát nữa vào đường không giật.
            Vector3 dir = _path.GetPointAtDistance(_frontStationDistance + 0.05f) - pos;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-6f)
                gun.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        public void RemoveGun(Gun gun)
        {
            _guns.Remove(gun); // gun khác vẫn chạy loop giữ nguyên khoảng cách — để lại 1 chỗ trống
            _queue.Remove(gun);
        }

        public void Clear()
        {
            _guns.Clear(); // gun trả về pool qua PoolManager.ReturnAll khi rebuild
            _queue.Clear();
            // pathLine nằm trên scene (không bị destroy cùng GunPath) → phải xoá điểm của level cũ.
            if (pathLine != null) pathLine.positionCount = 0;
            if (_path != null) { Destroy(_path.gameObject); _path = null; }
        }

        /// <summary>Có gun nào trên path còn cell cùng màu để bắn không (check LOSE).</summary>
        public bool AnyGunHasTarget()
        {
            if (GridBlockManager.Instance == null) return false;
            foreach (var g in _guns)
                if (GridBlockManager.Instance.HasFrontCellOfColor(g.Color)) return true;
            return false;
        }
    }
}
