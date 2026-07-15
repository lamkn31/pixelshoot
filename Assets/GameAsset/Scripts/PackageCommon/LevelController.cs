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
            PoolManager.Instance.Init(levelData); // set prefab + trả hết item cũ về pool
            ClearAll();

            PathManager.Instance.Build(levelData); // tự dựng RoundedPolylinePath + mặt đường
            GridBlockManager.Instance.Build(levelData);
            SlotManager.Instance.Build(levelData);
            SpawnBoardProps(levelData);
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
