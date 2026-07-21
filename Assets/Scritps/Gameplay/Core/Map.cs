using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Gắn trên ROOT của mỗi MAP prefab. Giữ danh sách MỐC vị trí slot: mỗi Transform = nơi sinh
    /// GUN 0 (gun đầu) của 1 slot; các gun sau xếp lùi theo spacing chung (GameSettings). Thứ tự phần tử
    /// trong list = SlotIndex (0..N-1). SlotManager đọc list này để đặt gun đúng chỗ map định.
    /// </summary>
    public class Map : MonoBehaviour
    {
        [Tooltip("Mỗi phần tử = mốc sinh GUN 0 của 1 slot (theo thứ tự slot 0..N-1). Đặt các Transform con " +
                 "vào đúng chỗ muốn slot mọc trên map.")]
        [SerializeField] private List<Transform> slotSpawns = new List<Transform>();

        /// <summary>Số slot mà map này định vị trí (= số mốc trong list).</summary>
        public int SlotCount => slotSpawns != null ? slotSpawns.Count : 0;

        public IReadOnlyList<Transform> SlotSpawns => slotSpawns;

        /// <summary>Vị trí world sinh GUN 0 của slot index; false nếu index ngoài list / mốc null.</summary>
        public bool TryGetSlotPosition(int index, out Vector3 pos)
        {
            if (slotSpawns != null && index >= 0 && index < slotSpawns.Count && slotSpawns[index] != null)
            { pos = slotSpawns[index].position; return true; }
            pos = default;
            return false;
        }
    }
}
