using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Danh sách level theo THỨ TỰ CHƠI. <see cref="LevelController"/> lấy level theo INDEX
    /// (UserProgressSO.currentLevelIndex) từ đây thay vì gán cứng 1 LevelData vào scene.
    /// Đặt asset trong thư mục Resources tên "LevelList" để runtime tự load (build mới chạy được);
    /// trong Editor có fallback tìm qua AssetDatabase.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelList", menuName = "Wayfu/Level List")]
    public class LevelList : ScriptableObject
    {
        [Tooltip("Thứ tự chơi: index 0 = level đầu tiên. Ô trống (null) tự bị bỏ qua khi lấy level.")]
        public List<LevelData> Levels = new List<LevelData>();

        /// <summary>Số level hợp lệ (đã bỏ ô trống).</summary>
        public int Count
        {
            get
            {
                int n = 0;
                if (Levels != null)
                    foreach (var l in Levels) if (l != null) n++;
                return n;
            }
        }

        /// <summary>
        /// Level theo index. Chơi hết danh sách (index >= <see cref="Count"/>) thì LẶP LẠI, nhưng chỉ
        /// lặp các level KHÔNG bật <see cref="LevelData.SkipLevelLoop"/> — level tutorial chỉ chơi 1 lần
        /// ở vòng đầu. Mọi level đều bật SkipLevelLoop thì lặp lại cả danh sách (không có gì để lặp thì
        /// thà lặp tất còn hơn kẹt game). Trả null nếu danh sách rỗng.
        /// </summary>
        public LevelData Get(int index)
        {
            var all = Compact();
            if (all.Count == 0) return null;
            if (index < 0) index = 0;
            if (index < all.Count) return all[index];

            var loop = all.FindAll(l => !l.SkipLevelLoop);
            if (loop.Count == 0) loop = all;
            return loop[(index - all.Count) % loop.Count];
        }

        /// <summary>Index trong danh sách (bỏ ô trống) của 1 level; -1 nếu không có.</summary>
        public int IndexOf(LevelData level)
        {
            if (level == null) return -1;
            return Compact().IndexOf(level);
        }

        private List<LevelData> Compact()
        {
            var list = new List<LevelData>();
            if (Levels != null)
                foreach (var l in Levels) if (l != null) list.Add(l);
            return list;
        }

        private static LevelList _instance;

        public static LevelList Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<LevelList>("LevelList");
#if UNITY_EDITOR
                if (_instance == null)
                {
                    var guids = UnityEditor.AssetDatabase.FindAssets("t:LevelList");
                    if (guids.Length > 0)
                        _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelList>(
                            UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
                }
#endif
                return _instance;
            }
        }
    }
}
