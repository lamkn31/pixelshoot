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
        [Tooltip("Tầm PHÁT HIỆN (không phải điều kiện bắn): gun bắt cell cùng màu ở hàng ngoài cùng sát path " +
                 "trong bán kính này. Đã bắt được cột thì bắn dứt cả cột, kể cả khi gun đã chạy ra xa.")]
        public float GunFireRange = 3f;
        [Tooltip("Arc-length của station trước nhất trên path.")]
        public float FrontStationDistance = 0f;
        [Tooltip("Tốc độ bay của bullet.")]
        public float BulletSpeed = 14f;

        [Header("Block")]
        [Tooltip("Khoảng cách giữa các block trong 1 stack (theo trục Y).")]
        public float BlockStackSpacing = 0.5f;

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
