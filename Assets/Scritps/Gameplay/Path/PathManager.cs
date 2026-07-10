using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Quản lý các gun đang chạy trên path loop (yêu cầu #4): giữ tối đa MaxGunOnPath,
    /// đảm bảo khoảng cách đều, và DỒN gun phía sau lên khi 1 gun biến mất.
    /// Gun đầu (index 0) chạy tự do; các gun sau bám theo với hệ số catch-up để khép khoảng trống.
    /// </summary>
    public class PathManager : Singleton<PathManager>
    {
        [SerializeField] private float catchUpFactor = 1.4f;

        private RoundedPolylinePath _path;
        private readonly List<Gun> _guns = new List<Gun>();
        private float _gunSpeed = 3f;
        private float _gunSpacing = 1.2f;
        private int _maxGunOnPath = 5;

        public bool IsFull => _guns.Count >= _maxGunOnPath;
        public bool CanAccept => _guns.Count < _maxGunOnPath;
        public int GunCount => _guns.Count;

        public void Init(RoundedPolylinePath path, LevelData level)
        {
            _path = path;
            _gunSpeed = level.GunSpeed;
            _gunSpacing = level.GunSpacing;
            _maxGunOnPath = level.MaxGunOnPath;
            _guns.Clear();
        }

        public void AddGun(Gun gun)
        {
            gun.Distance = _guns.Count == 0 ? 0f : _guns[_guns.Count - 1].Distance - _gunSpacing;
            _guns.Add(gun);
        }

        public void RemoveGun(Gun gun) => _guns.Remove(gun);

        public void Clear()
        {
            foreach (var g in _guns) if (g != null) Destroy(g.gameObject);
            _guns.Clear();
            if (_path != null) { Destroy(_path.gameObject); _path = null; }
        }

        /// <summary>Có gun nào trên path còn cột cùng màu để bắn không (check LOSE).</summary>
        public bool AnyGunHasTarget()
        {
            if (GridBlockManager.Instance == null) return false;
            foreach (var g in _guns)
                if (GridBlockManager.Instance.HasFrontColumnOfColor(g.Color)) return true;
            return false;
        }

        private void Update()
        {
            if (_path == null || _guns.Count == 0) return;

            float step = _gunSpeed * Time.deltaTime;
            _guns[0].Distance += step; // gun đầu chạy tự do

            for (int i = 1; i < _guns.Count; i++)
            {
                float target = _guns[i - 1].Distance - _gunSpacing;
                _guns[i].Distance = Mathf.MoveTowards(_guns[i].Distance, target, step * catchUpFactor);
            }

            for (int i = 0; i < _guns.Count; i++) ApplyPose(_guns[i]);
        }

        private void ApplyPose(Gun g)
        {
            Vector3 pos = _path.GetPointAtDistance(g.Distance);
            g.transform.position = pos;

            Vector3 ahead = _path.GetPointAtDistance(g.Distance + 0.05f);
            Vector3 dir = (ahead - pos).normalized;
            if (dir != Vector3.zero)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                g.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
        }
    }
}
