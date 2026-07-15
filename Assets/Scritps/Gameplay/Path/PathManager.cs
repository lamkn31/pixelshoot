using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Dựng đường path (RoundedPolylinePath + mặt đường LineRenderer) từ LevelData và quản lý các gun
    /// chạy trên đó. Gun chạy loop liên tục bằng RoundedPolylineFollower; mỗi lượt deploy được xếp cách
    /// nhau GunSpacing dọc path. Thua khi đủ gun mà không gun nào bắn được.
    /// </summary>
    public class PathManager : Singleton<PathManager>
    {
        [Header("Mặt đường")]
        [Tooltip("LineRenderer vẽ mặt đường — gán sẵn trên scene. Bỏ trống thì không vẽ mặt đường.")]
        [SerializeField] private LineRenderer pathLine;
        [Tooltip("Material mặt đường. Bỏ trống thì giữ material đang gán trên LineRenderer.")]
        [SerializeField] private Material pathMaterial;

        private RoundedPolylinePath _path;
        private readonly List<Gun> _guns = new List<Gun>();   // [0] = gun vào trước nhất
        private float _gunSpacing = 1.2f;
        private float _gunSpeed = 3f;
        private float _frontStationDistance;
        private int _maxGunOnPath = 5;
        private int _deployCount; // đếm lượt deploy để trải đều gun quanh loop (không tái dùng khi gun chết)

        public bool IsFull => _guns.Count >= _maxGunOnPath;
        public bool CanAccept => _guns.Count < _maxGunOnPath;
        public int GunCount => _guns.Count;
        public RoundedPolylinePath Path => _path;

        /// <summary>Dựng path từ level rồi nạp config gun. Gọi thay cho Init(path) cũ.</summary>
        public void Build(LevelData level)
        {
            Clear();

            var gs = GameSettings.Instance;
            _gunSpacing = gs != null ? gs.GunSpacing : 1.2f;
            _gunSpeed = gs != null ? gs.GunSpeed : 3f;
            _maxGunOnPath = gs != null ? gs.MaxGunOnPath : 5;
            _frontStationDistance = gs != null ? gs.FrontStationDistance : 0f;

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

        public void AddGun(Gun gun)
        {
            _guns.Add(gun);
            // index ĐẢO (yêu cầu #2): gun vào sau đứng TRƯỚC theo chiều path (+spacing thay vì -).
            float startDist = _frontStationDistance + _deployCount * _gunSpacing;
            _deployCount++;
            gun.Distance = startDist;
            gun.DeployOnPath(_path, startDist, _gunSpeed); // follower chạy vòng liên tục
        }

        public void RemoveGun(Gun gun)
        {
            _guns.Remove(gun); // gun khác vẫn chạy loop giữ nguyên khoảng cách — để lại 1 chỗ trống
        }

        public void Clear()
        {
            _guns.Clear(); // gun trả về pool qua PoolManager.ReturnAll khi rebuild
            _deployCount = 0;
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
