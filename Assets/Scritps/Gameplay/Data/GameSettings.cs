using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Cấu hình DÙNG CHUNG cho toàn game (không theo level). Đặt asset trong folder Resources tên
    /// "GameSettings" để runtime tự load; Level Tool tự tìm qua AssetDatabase.
    /// </summary>
    [CreateAssetMenu(fileName = "GameSettings", menuName = "Wayfu/Game Settings")]
    public class GameSettings : ScriptableObject
    {
        [Header("Slot")]
        [Tooltip("Spacing DÙNG CHUNG giữa các gun trong slot (theo trục Z).")]
        public float SlotGunSpacing = 1f;

        [Header("Guns / Path (config chung, không theo level)")]
        [Min(1)] public int MaxGunOnPath = 5;
        public float GunSpeed = 3f;
        [Tooltip("Khoảng cách (arc-length) giữa các station gun trên path.")]
        public float GunSpacing = 1.2f;
        public float FireInterval = 0.25f;
        [Tooltip("Bán kính vùng PHÁT HIỆN của mỗi nòng. Cũng là ĐIỀU KIỆN BẮN — cell trôi ra ngoài là nòng " +
                 "ngừng bắn (nhưng vẫn giữ target để bắn dứt điểm khi nó vào lại quạt).")]
        public float GunFireRange = 3f;
        [Tooltip("Góc TOẢ của mỗi nòng (độ), tính TỪ hướng trước mặt của gun (thân luôn bám path, không " +
                 "quay mặt về target) rồi mở sang sườn của nòng đó: nòng phải toả sang phải, nòng trái " +
                 "sang trái — 2 quạt chung mép ở trục trước mặt.\n" +
                 "Ví dụ 60 = mỗi nòng quét từ 0° tới 60° về phía mình. 180 = 2 quạt phủ kín vòng tròn " +
                 "(mỗi nòng gác trọn 1 nửa mặt phẳng). Trên 180 không thêm được gì, sẽ bị kẹp về 180.")]
        [Range(1f, 180f)] public float GunFireAngle = 60f;
        [Tooltip("Arc-length của station trước nhất trên path.")]
        public float FrontStationDistance = 0f;
        [Tooltip("Tốc độ bay của bullet.")]
        public float BulletSpeed = 14f;

        [Header("Block")]
        [Tooltip("Khoảng cách giữa các block trong 1 stack (theo trục Y).")]
        public float BlockStackSpacing = 0.5f;
        [Tooltip("Tốc độ dồn hàng: thời gian (giây) cell trượt từ ô cũ sang ô mới. NHỎ = dồn NHANH.")]
        [Min(0f)] public float BlockCollapseDuration = 0.25f;

        private static GameSettings _instance;

        public static GameSettings Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<GameSettings>("GameSettings");
#if UNITY_EDITOR
                if (_instance == null)
                {
                    var guids = UnityEditor.AssetDatabase.FindAssets("t:GameSettings");
                    if (guids.Length > 0)
                        _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<GameSettings>(
                            UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
                }
#endif
                return _instance;
            }
        }

        /// <summary>Spacing gun-trong-slot dùng chung (fallback 1f nếu chưa có asset).</summary>
        public static float SlotSpacing => Instance != null ? Instance.SlotGunSpacing : 1f;
    }
}
