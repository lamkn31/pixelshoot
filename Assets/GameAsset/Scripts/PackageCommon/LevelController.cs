using System.Collections.Generic;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Đọc LevelData → dựng path, grid block, slot và khởi động GameController.
    /// Tự tạo các manager nếu scene chưa có, nên chỉ cần đặt 1 LevelController + gán LevelData là chạy.
    /// </summary>
    public class LevelController : Singleton<LevelController>
    {
        [SerializeField] private LevelData levelData;

        public LevelData Level => levelData;

        private void Start()
        {
            if (levelData != null) Build();
        }

        public void SetLevel(LevelData data) => levelData = data;

        public void Build()
        {
            if (levelData == null)
            {
                Debug.LogError("[LevelController] Chưa gán LevelData");
                return;
            }

            EnsureManagers();
            ClearAll();

            var path = BuildPath(levelData);
            PathManager.Instance.Init(path, levelData);
            GridBlockManager.Instance.Build(levelData);
            SlotManager.Instance.Build(levelData);
            GameController.Instance.StartLevel();
        }

        public void Retry() => Build();

        private void ClearAll()
        {
            PathManager.Instance?.Clear();
            GridBlockManager.Instance?.Clear();
            SlotManager.Instance?.Clear();
        }

        private void EnsureManagers()
        {
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

        private RoundedPolylinePath BuildPath(LevelData level)
        {
            var go = new GameObject("GunPath");
            var path = go.AddComponent<RoundedPolylinePath>();
            path.isClosed = true;
            path.cornerRadius = level.CornerRadius;
            path.waypoints = new List<Transform>();

            for (int i = 0; i < level.PathWaypoints.Count; i++)
            {
                var wp = new GameObject("WP_" + i).transform;
                wp.SetParent(go.transform);
                wp.position = level.PathWaypoints[i];
                path.waypoints.Add(wp);
            }

            path.GeneratePath();
            return path;
        }
    }
}
