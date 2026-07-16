using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Đọc LevelData → dựng path, grid block, slot và khởi động GameController.
    /// Level lấy từ <see cref="LevelList"/> theo INDEX (UserProgressSO.currentLevelIndex); gán
    /// <see cref="levelOverride"/> để ghim 1 level cố định khi test.
    /// Tự tạo các manager nếu scene chưa có, nên chỉ cần đặt 1 LevelController là chạy.
    /// </summary>
    public class LevelController : Singleton<LevelController>
    {
        [Header("Nguồn level")]
        [Tooltip("Danh sách level theo thứ tự chơi. Bỏ trống → tự load asset 'LevelList' trong Resources.")]
        [SerializeField] private LevelList levelList;
        [Tooltip("GHI ĐÈ để test: gán 1 level thì bỏ qua Level List + index. Bỏ trống = chơi theo index " +
                 "của UserProgressSO.currentLevelIndex.")]
        [FormerlySerializedAs("levelData")] // giữ tham chiếu đã gán sẵn trên scene khi đổi tên field
        [SerializeField] private LevelData levelOverride;

        private LevelData _level;

        /// <summary>Level ĐANG dựng (đã giải ra từ Level List hoặc override).</summary>
        public LevelData Level => _level;

        /// <summary>Index của level đang chơi trong Level List.</summary>
        public int CurrentIndex { get; private set; }

        private void Start() => LoadCurrent();

        /// <summary>Nạp level theo tiến trình đã lưu (UserProgressSO.currentLevelIndex).</summary>
        public void LoadCurrent() => LoadLevel(ProgressIndex);

        /// <summary>
        /// Nạp + dựng level theo index trong Level List. Index vượt quá cuối danh sách sẽ LẶP
        /// (xem <see cref="LevelList.Get"/>), nên không cần tự chặn biên.
        /// </summary>
        public void LoadLevel(int index)
        {
            CurrentIndex = Mathf.Max(0, index);
            _level = Resolve(CurrentIndex);
            if (_level == null)
            {
                Debug.LogError($"[LevelController] Không lấy được level index {CurrentIndex} — chưa gán " +
                               "Level List (hoặc chưa có asset 'LevelList' trong Resources) và cũng chưa gán Level Override.");
                return;
            }
            Build();
        }

        /// <summary>Qua level kế: tăng tiến trình đã lưu rồi nạp level mới. Chưa có ai gọi — nối vào nút Next của WinPopup.</summary>
        public void Next()
        {
            Progress?.AdvanceLevel(); // tự Save()
            LoadCurrent();
        }

        /// <summary>Ghim 1 level cụ thể mà không đụng tiến trình (tool/debug). Gọi Build() để dựng.</summary>
        public void SetLevel(LevelData data) => _level = data;

        private LevelData Resolve(int index)
        {
            if (levelOverride != null) return levelOverride; // override thắng: giữ đúng hành vi scene cũ
            var list = levelList != null ? levelList : LevelList.Instance;
            return list != null ? list.Get(index) : null;
        }

        // Không dùng UserDataController.Instance: Singleton.Instance log error khi scene chưa có nó.
        // Thiếu UserDataController → coi như index 0, không phải lỗi.
        private static UserProgressSO Progress =>
            UserDataController.IsActive ? UserDataController.Instance.UserProgress : null;

        private static int ProgressIndex
        {
            get { var p = Progress; return p != null ? Mathf.Max(0, p.currentLevelIndex) : 0; }
        }

        public void Build()
        {
            if (_level == null)
            {
                Debug.LogError("[LevelController] Chưa có LevelData để dựng");
                return;
            }

            EnsureManagers();
            PoolManager.Instance.Init(_level); // set prefab + trả hết item cũ về pool
            ClearAll();

            PathManager.Instance.Build(_level); // tự dựng RoundedPolylinePath + mặt đường
            GridBlockManager.Instance.Build(_level);
            SlotManager.Instance.Build(_level);
            SpawnBoardProps(_level);
            GameController.Instance.StartLevel();
        }

        public void Retry() => Build();

        private Transform _propsRoot;

        private void ClearAll()
        {
            PathManager.Instance?.Clear();
            GridBlockManager.Instance?.Clear();
            SlotManager.Instance?.Clear();
            if (_propsRoot != null) Destroy(_propsRoot.gameObject);
        }

        private void SpawnBoardProps(LevelData level)
        {
            if (level.BoardProps == null || level.BoardProps.Count == 0) return;
            _propsRoot = new GameObject("BoardProps").transform;
            foreach (var p in level.BoardProps)
            {
                if (p?.PropPrefab == null) continue;
                var rot = p.PropRot.Equals(default(Quaternion)) ? Quaternion.identity : p.PropRot;
                var scale = p.PropScale == Vector3.zero ? Vector3.one : p.PropScale;
                var go = Instantiate(p.PropPrefab, p.PropPos, rot, _propsRoot);
                go.transform.localScale = scale;
            }
        }

        private void EnsureManagers()
        {
            Ensure<PoolManager>();
            Ensure<GameController>();
            Ensure<GridBlockManager>();
            Ensure<SlotManager>();
            Ensure<PathManager>();
        }

        private void Ensure<T>() where T : MonoBehaviour
        {
            if (FindObjectOfType<T>() == null)
                new GameObject(typeof(T).Name).AddComponent<T>();
        }

    }
}
