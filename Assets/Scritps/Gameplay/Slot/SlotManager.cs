using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Quản lý slot gun. Ưu tiên dùng các slot ĐẶT SẴN TRÊN SCENE (gán vào <see cref="sceneSlots"/>
    /// hoặc tự tìm trong scene, sắp theo SlotIndex). Level điền danh sách gun cho từng slot;
    /// số slot có gun = số slot được active, các slot dư bị tắt (yêu cầu #6, #4).
    /// Nếu scene không có slot nào → fallback tự tạo 1 hàng slot mặc định.
    /// </summary>
    public class SlotManager : Singleton<SlotManager>
    {
        [Tooltip("Các slot đặt sẵn trên scene (Slot0..4). Bỏ trống sẽ tự tìm GunSlot trong scene.")]
        [SerializeField] private List<GunSlot> sceneSlots = new List<GunSlot>();

        private readonly List<GunSlot> _activeSlots = new List<GunSlot>();
        private readonly List<GameObject> _fallbackCreated = new List<GameObject>();

        public void Build(LevelData level)
        {
            Clear();
            var gs = GameSettings.Instance;
            float spacing = gs != null ? gs.SlotGunSpacing : 1f;   // config chung
            float fireInterval = gs != null ? gs.FireInterval : 0.25f;
            float fireRange = gs != null ? gs.GunFireRange : 3f;
            float fireAngle = gs != null ? gs.GunFireAngle : 360f;
            float bulletSpeed = gs != null ? gs.BulletSpeed : 14f;
            var fireMode = gs != null ? gs.FireMode : GunFireMode.Single;

            var slots = ResolveSceneSlots();
            if (slots.Count > 0)
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    bool active = i < level.Slots.Count
                                  && level.Slots[i]?.Guns != null
                                  && level.Slots[i].Guns.Count > 0;
                    slots[i].gameObject.SetActive(active);
                    if (active)
                    {
                        slots[i].Fill(level.Slots[i].Guns, spacing, fireInterval, fireRange, fireAngle,
                                      bulletSpeed, fireMode);
                        _activeSlots.Add(slots[i]);
                    }
                }
            }
            else
            {
                // Fallback: chưa đặt slot trên scene → tạo 1 hàng slot mặc định.
                float gap = Mathf.Max(1.5f, spacing * 3f);
                for (int i = 0; i < level.Slots.Count; i++)
                {
                    var sd = level.Slots[i];
                    if (sd?.Guns == null || sd.Guns.Count == 0) continue;
                    var go = new GameObject("Slot" + i);
                    go.transform.SetParent(transform);
                    var slot = go.AddComponent<GunSlot>();
                    slot.SlotIndex = i;
                    slot.SetPosition(new Vector3(i * gap, 0f, 0f));
                    slot.Fill(sd.Guns, spacing, fireInterval, fireRange, fireAngle, bulletSpeed, fireMode);
                    _activeSlots.Add(slot);
                    _fallbackCreated.Add(go);
                }
            }
        }

        public void Clear()
        {
            foreach (var s in _activeSlots) if (s != null) s.Clear();
            _activeSlots.Clear();
            foreach (var go in _fallbackCreated) if (go != null) Destroy(go);
            _fallbackCreated.Clear();
        }

        public void OnGunClicked(Gun gun)
        {
            if (gun == null) return;
            var slot = gun.Slot;
            if (slot == null || slot.FrontGun != gun) return;              // chỉ gun đầu slot
            if (PathManager.Instance == null || !PathManager.Instance.CanAccept) return; // path đã đầy

            slot.RemoveFront();                     // gun sau dồn lên, click tiếp được ngay
            PathManager.Instance.RequestDeploy(gun); // vào path luôn, hoặc xếp hàng chờ đủ khoảng cách
            GameController.Instance?.OnBoardChanged();
        }

        private List<GunSlot> ResolveSceneSlots()
        {
            var list = new List<GunSlot>();
            if (sceneSlots != null)
                foreach (var s in sceneSlots) if (s != null) list.Add(s);

            if (list.Count == 0)
                list.AddRange(FindObjectsOfType<GunSlot>(true));

            list.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
            return list;
        }
    }
}
