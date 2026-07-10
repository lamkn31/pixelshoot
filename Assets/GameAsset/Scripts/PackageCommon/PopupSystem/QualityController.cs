using System;
using UnityEngine;

namespace Wayfu.Lamkn
{
    public enum GameQualityLevel
    {
        Low = 0,    // Máy yếu: Tắt bóng, texture giảm, không khử răng cưa
        Medium = 1, // Máy trung: Bóng cứng (Hard Shadow), texture chuẩn
        High = 2    // Máy mạnh: Bóng mềm (Soft Shadow), khử răng cưa
    }

    /// <summary>
    /// Quản lý cấu hình đồ họa và hiệu năng.
    /// Thay thế: XQualitySetting
    /// </summary>
    public class QualityController : Singleton<QualityController>
    {
        private const string PREF_QUALITY = "Quality_Level";
        private const string PREF_FPS = "Target_FPS";

        // Cấu hình hiện tại
        public GameQualityLevel CurrentQuality { get; private set; }
        public int TargetFPS { get; private set; }

        // Event báo khi cấu hình thay đổi (để UI cập nhật hiển thị)
        public event Action<GameQualityLevel> OnQualityChanged;

        protected override void Awake()
        {
            base.Awake();
            LoadSettings();
        }

        private void Start()
        {
            // Đảm bảo màn hình không bao giờ tắt khi đang chơi
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private void LoadSettings()
        {
            // Mặc định là Medium nếu chưa chỉnh gì
            int qualityInt = PlayerPrefs.GetInt(PREF_QUALITY, (int)GameQualityLevel.Medium);
            CurrentQuality = (GameQualityLevel)Mathf.Clamp(qualityInt, 0, 2);

            // Mặc định là 60 FPS
            TargetFPS = PlayerPrefs.GetInt(PREF_FPS, 120);

            ApplyQuality(CurrentQuality);
            ApplyFrameRate(TargetFPS);
        }

        /// <summary>
        /// Thiết lập mức đồ họa tổng thể.
        /// </summary>
        public void SetQuality(GameQualityLevel level)
        {
            if (CurrentQuality == level) return;

            CurrentQuality = level;
            PlayerPrefs.SetInt(PREF_QUALITY, (int)level);
            PlayerPrefs.Save();

            ApplyQuality(level);
            OnQualityChanged?.Invoke(level);
        }

        /// <summary>
        /// Thiết lập giới hạn khung hình (30 hoặc 60).
        /// </summary>
        public void SetFrameRate(int fps)
        {
            TargetFPS = fps;
            PlayerPrefs.SetInt(PREF_FPS, fps);
            PlayerPrefs.Save();

            ApplyFrameRate(fps);
        }

        // --- INTERNAL APPLY LOGIC ---

        private void ApplyFrameRate(int fps)
        {
            Application.targetFrameRate = fps;
            // VSync phải tắt thì targetFrameRate mới có tác dụng trên một số nền tảng
            QualitySettings.vSyncCount = 0;
        }

        private void ApplyQuality(GameQualityLevel level)
        {
            // Sử dụng các mức Quality Level đã cài sẵn trong: Edit -> Project Settings -> Quality
            // Giả định bạn đã setup 3 mức trong Unity Editor tương ứng:
            // Index 0: Low (Fastest)
            // Index 1: Medium (Balanced)
            // Index 2: High (Fantastic)
            QualitySettings.SetQualityLevel((int)level, true);

            switch (level)
            {
                case GameQualityLevel.Low:
                    // Tối ưu cực đoan cho máy yếu
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.antiAliasing = 0;
                    QualitySettings.globalTextureMipmapLimit = 1; // Giảm độ nét texture (Half Res)
                                                                  // Giảm độ phân giải render xuống 85% để tăng FPS
                    SetResolutionScale(0.85f);
                    break;

                case GameQualityLevel.Medium:
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowResolution = ShadowResolution.Low;
                    QualitySettings.shadowDistance = 20f; // Chỉ đổ bóng gần
                    QualitySettings.antiAliasing = 0;
                    QualitySettings.globalTextureMipmapLimit = 0; // Full Res
                    SetResolutionScale(0.95f);
                    break;

                case GameQualityLevel.High:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.Medium;
                    QualitySettings.shadowDistance = 40f;
                    QualitySettings.antiAliasing = 2; // 2x MSAA
                    QualitySettings.globalTextureMipmapLimit = 0;
                    SetResolutionScale(1.0f);
                    break;
            }

            Debug.Log($"[QualityManager] Applied Quality: {level}");
        }

        /// <summary>
        /// Thay đổi độ phân giải Render (DPI Scaling).
        /// Giá trị < 1.0 giúp tăng FPS đáng kể trên điện thoại màn hình 2K/4K.
        /// </summary>
        private void SetResolutionScale(float scale)
        {
#if UNITY_ANDROID || UNITY_IOS
            // Chỉ áp dụng trên Mobile để tránh mờ hình trên PC
            ScalableBufferManager.ResizeBuffers(scale, scale);
#endif
        }

        /// <summary>
        /// Tự động phát hiện cấu hình máy để gợi ý mức đồ họa (Auto Detect).
        /// </summary>
        public void AutoDetectSettings()
        {
            // Dựa vào RAM hệ thống (MB)
            int sysMemory = SystemInfo.systemMemorySize;

            // Dựa vào Shader Level
            int shaderLevel = SystemInfo.graphicsShaderLevel;

            if (sysMemory > 4000 && shaderLevel >= 45) // > 4GB RAM
            {
                SetQuality(GameQualityLevel.High);
                SetFrameRate(60);
            }
            else if (sysMemory > 2000) // > 2GB RAM
            {
                SetQuality(GameQualityLevel.Medium);
                SetFrameRate(60); // Cố gắng chạy 60, nếu nóng máy người dùng tự giảm
            }
            else // Máy yếu
            {
                SetQuality(GameQualityLevel.Low);
                SetFrameRate(30);
            }
        }
    }
}