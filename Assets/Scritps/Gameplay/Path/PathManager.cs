using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Quản lý các gun trên path loop (yêu cầu #4). Gun KHÔNG chạy loop liên tục: mỗi gun tiến tới 1
    /// "station" cố định trên path rồi DỪNG để bắn. Tối đa MaxGunOnPath station. Khi 1 gun rời đi
    /// (hết đạn), các gun phía sau DỒN lên 1 station. Thua khi đủ gun mà không gun nào bắn được.
    /// </summary>
    public class PathManager : Singleton<PathManager>
    {
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

        public void Init(RoundedPolylinePath path)
        {
            _path = path;
            var gs = GameSettings.Instance;
            _gunSpacing = gs != null ? gs.GunSpacing : 1.2f;
            _gunSpeed = gs != null ? gs.GunSpeed : 3f;
            _maxGunOnPath = gs != null ? gs.MaxGunOnPath : 5;
            _frontStationDistance = gs != null ? gs.FrontStationDistance : 0f;
            _guns.Clear();
            _deployCount = 0;
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
