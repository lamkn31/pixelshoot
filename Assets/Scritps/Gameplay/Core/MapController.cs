using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Spawn MAP theo LOẠI SỐ SLOT của level (2..5). Mỗi loại 1 prefab; prefab chứa sẵn các
    /// <see cref="GunSlot"/> con — VỊ TRÍ slot do chính prefab quyết định (SlotManager tìm các GunSlot này
    /// để nạp gun, khoảng cách gun vẫn theo GameSettings như cũ).
    /// <para>Đặt component này trên scene và gán 4 prefab. Nếu bị auto-tạo rỗng (LevelController.EnsureManagers)
    /// hoặc chưa gán prefab cho loại đó thì KHÔNG spawn gì — SlotManager tự fallback slot đặt sẵn trên scene.</para>
    /// </summary>
    public class MapController : Singleton<MapController>
    {
        public const int MinSlots = 2, MaxSlots = 5;

        [Tooltip("Map prefab theo số slot: [0]=2 slot, [1]=3, [2]=4, [3]=5. Mỗi prefab chứa sẵn GunSlot con " +
                 "(gán SlotIndex 0..N-1) đặt đúng chỗ slot sẽ sinh.")]
        [SerializeField] private GameObject[] mapPrefabs = new GameObject[MaxSlots - MinSlots + 1];

        private GameObject _current;
        private Map _currentMap;

        /// <summary>Map đang spawn (null nếu chưa gán prefab cho loại này).</summary>
        public GameObject CurrentMap => _current;

        /// <summary>Script <see cref="Map"/> trên map đang spawn — nguồn vị trí slot (null nếu prefab thiếu).</summary>
        public Map CurrentMapScript => _currentMap;

        /// <summary>Prefab map cho số slot (2..5, kẹp biên); null nếu chưa gán.</summary>
        public GameObject PrefabFor(int slotCount)
        {
            int idx = Mathf.Clamp(slotCount, MinSlots, MaxSlots) - MinSlots;
            return mapPrefabs != null && idx >= 0 && idx < mapPrefabs.Length ? mapPrefabs[idx] : null;
        }

        /// <summary>Dựng map cho level: instantiate prefab khớp SlotCount dưới node này (giữ transform local
        /// của prefab → slot ra đúng vị trí prefab định). Gọi TRƯỚC SlotManager.Build.</summary>
        public void Build(LevelData level)
        {
            Clear();
            if (level == null) return;
            var prefab = PrefabFor(level.SlotCount);
            if (prefab == null) return; // chưa gán → SlotManager dùng slot scene như cũ
            _current = Instantiate(prefab, transform);
            _current.name = prefab.name;
            _currentMap = _current.GetComponent<Map>();
        }

        public void Clear()
        {
            if (_current != null) Destroy(_current);
            _current = null;
            _currentMap = null;
        }
    }
}
