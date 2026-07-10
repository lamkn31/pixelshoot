using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Quản lý các slot (số hàng gun mỗi level + thứ tự gun ra). Nhận click từ gun và
    /// đẩy gun đầu slot sang PathManager nếu path chưa đầy (yêu cầu #6).
    /// </summary>
    public class SlotManager : Singleton<SlotManager>
    {
        private readonly List<GunSlot> _slots = new List<GunSlot>();

        public void Build(LevelData level)
        {
            Clear();
            foreach (var slotData in level.Slots)
            {
                if (slotData == null) continue;
                var go = new GameObject("Slot");
                go.transform.SetParent(transform);
                var slot = go.AddComponent<GunSlot>();
                slot.Build(slotData, level);
                _slots.Add(slot);
            }
        }

        public void Clear()
        {
            foreach (var s in _slots) if (s != null) { s.Clear(); Destroy(s.gameObject); }
            _slots.Clear();
        }

        public void OnGunClicked(Gun gun)
        {
            if (gun == null) return;

            var slot = gun.Slot;
            if (slot == null || slot.FrontGun != gun) return;              // chỉ gun đầu slot
            if (PathManager.Instance == null || !PathManager.Instance.CanAccept) return; // path đã đầy

            slot.RemoveFront();
            PathManager.Instance.AddGun(gun);
            gun.OnDeployed();
            GameController.Instance?.OnBoardChanged();
        }
    }
}
