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
        [SerializeField] private float transitionDuration = 0.3f;

        private RoundedPolylinePath _path;
        private readonly List<Gun> _guns = new List<Gun>();   // [0] = station trước nhất
        private float _gunSpacing = 1.2f;
        private float _frontStationDistance;
        private int _maxGunOnPath = 5;

        public bool IsFull => _guns.Count >= _maxGunOnPath;
        public bool CanAccept => _guns.Count < _maxGunOnPath;
        public int GunCount => _guns.Count;

        public void Init(RoundedPolylinePath path)
        {
            _path = path;
            var gs = GameSettings.Instance;
            _gunSpacing = gs != null ? gs.GunSpacing : 1.2f;
            _maxGunOnPath = gs != null ? gs.MaxGunOnPath : 5;
            _frontStationDistance = gs != null ? gs.FrontStationDistance : 0f;
            _guns.Clear();
        }

        /// <summary>Arc-length của station thứ index (0 = trước nhất).</summary>
        private float StationDistance(int index) => _frontStationDistance - index * _gunSpacing;

        public void AddGun(Gun gun)
        {
            int index = _guns.Count;
            _guns.Add(gun);
            SendToStation(gun, index); // animate từ slot lên station, rồi dừng
        }

        public void RemoveGun(Gun gun)
        {
            int idx = _guns.IndexOf(gun);
            if (idx < 0) return;
            _guns.RemoveAt(idx);
            // Dồn các gun phía sau tiến lên 1 station.
            for (int i = idx; i < _guns.Count; i++) SendToStation(_guns[i], i);
        }

        public void Clear()
        {
            _guns.Clear(); // gun trả về pool qua PoolManager.ReturnAll khi rebuild
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

        private void SendToStation(Gun gun, int index)
        {
            if (_path == null) return;
            gun.Distance = StationDistance(index);
            Vector3 pos = _path.GetPointAtDistance(gun.Distance);
            gun.transform.rotation = FacingAt(gun.Distance);
            gun.BeginEntry(pos, transitionDuration); // trượt lên station rồi dừng
        }

        private Quaternion FacingAt(float distance)
        {
            Vector3 pos = _path.GetPointAtDistance(distance);
            Vector3 ahead = _path.GetPointAtDistance(distance + 0.05f);
            Vector3 dir = (ahead - pos).normalized;
            if (dir == Vector3.zero) return Quaternion.identity;
            return Quaternion.LookRotation(dir, Vector3.up); // hướng path trên sàn ngang
        }
    }
}
