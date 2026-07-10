using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// 1 hàng chứa các gun theo thứ tự ra. Chỉ gun đầu (FrontGun) mới deploy được;
    /// khi gun đầu rời đi, các gun phía sau dồn lên (yêu cầu #3, #6).
    /// </summary>
    public class GunSlot : MonoBehaviour
    {
        [SerializeField] private float shiftDuration = 0.15f;

        private readonly List<Gun> _guns = new List<Gun>();
        private Vector3 _pos, _dir;
        private float _spacing;

        public Gun FrontGun => _guns.Count > 0 ? _guns[0] : null;
        public int Count => _guns.Count;

        public void Build(SlotData data, LevelData level)
        {
            _pos = data.Position;
            _dir = data.Direction.sqrMagnitude > 0.0001f ? data.Direction.normalized : Vector3.up;
            _spacing = data.Spacing;

            for (int i = 0; i < data.Guns.Count; i++)
            {
                if (data.Guns[i] == null) continue;
                var g = GameplayFactory.CreateGun(level.GunPrefab, transform);
                g.transform.position = _pos + _dir * _spacing * i;
                g.Init(data.Guns[i], level.FireInterval);
                g.SetSlot(this);
                _guns.Add(g);
            }
        }

        public Gun RemoveFront()
        {
            if (_guns.Count == 0) return null;
            var front = _guns[0];
            _guns.RemoveAt(0);
            for (int i = 0; i < _guns.Count; i++)
                _guns[i].MoveTo(_pos + _dir * _spacing * i, shiftDuration);
            return front;
        }

        public void Clear()
        {
            foreach (var g in _guns) if (g != null) Destroy(g.gameObject);
            _guns.Clear();
        }
    }
}
