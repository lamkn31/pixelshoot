using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Singleton quản lý dữ liệu người chơi. Nhiệm vụ chính: nạp dữ liệu đã lưu (PlayerPrefs qua
    /// <see cref="UserProgressSO.Load"/>) vào <see cref="UserProgressSO"/> đúng MỘT lần ở lần chạy
    /// đầu tiên, trước khi gameplay đọc currentLevelIndex.
    ///
    /// Vì <see cref="UserProgressSO"/> là asset dùng chung, mọi nơi tham chiếu tới cùng instance đó
    /// sẽ thấy giá trị đã nạp. Singleton + DontDestroyOnLoad bảo đảm Load chỉ chạy 1 lần dù scene
    /// reload (retry / next level).
    /// </summary>
    [DefaultExecutionOrder(-1000)] // Awake chạy SỚM, trước BusLevelController để nạp data trước khi build level.
    public sealed class UserDataController : Singleton<UserDataController>
    {
        [SerializeField] [Tooltip("Tiến trình người chơi cần nạp dữ liệu đã lưu vào.")]
        private UserProgressSO _userProgress;

        private static bool _loaded;

        /// <summary>Truy cập tiến trình người chơi đã nạp dữ liệu lưu.</summary>
        public UserProgressSO UserProgress => _userProgress;

        protected override void OnAwake()
        {
            EnsureLoaded();
        }

        /// <summary>Nạp dữ liệu đã lưu vào <see cref="UserProgressSO"/> đúng một lần cho phiên chạy.
        /// Gọi lại nhiều lần là no-op.</summary>
        public void EnsureLoaded()
        {
            if (_loaded) return;
            if (_userProgress != null) _userProgress.Load();
            _loaded = true;
        }
    }
}
