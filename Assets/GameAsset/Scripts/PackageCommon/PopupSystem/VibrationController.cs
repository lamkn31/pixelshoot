using UnityEngine;
using Solo.MOST_IN_ONE;
namespace Wayfu.Lamkn
{
    public enum VibrationStyle { Light, Medium, Heavy, Selection, Warning }

    public class VibrationController : Singleton<VibrationController>
    {
        #region Constants

        private const string PREF_VIBRATION = "Vibration_Enabled";

        #endregion

        #region State

        public bool IsEnabled { get; private set; }

        #endregion

        #region Unity

        protected override void OnAwake()
        {
            IsEnabled = PlayerPrefs.GetInt(PREF_VIBRATION, 1) == 1;
            MOST_HapticFeedback.HapticsEnabled = IsEnabled;
        }

        #endregion

        #region API

        public void Toggle()
        {
            IsEnabled = !IsEnabled;
            MOST_HapticFeedback.HapticsEnabled = IsEnabled;
            PlayerPrefs.SetInt(PREF_VIBRATION, IsEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetEnabled(bool enabled)
        {
            if (IsEnabled == enabled) return;
            Toggle();
        }

        public void Vibrate(VibrationStyle style = VibrationStyle.Light)
        {
            if (!IsEnabled) return;

            switch (style)
            {
                case VibrationStyle.Light: // Nhẹ
                    MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.LightImpact);
                    break;
                case VibrationStyle.Medium: // Vừa
                    MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.MediumImpact);
                    break;
                case VibrationStyle.Heavy: // Mạnh
                    MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.HeavyImpact);
                    break;
                case VibrationStyle.Selection:
                    MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.Selection);
                    break;
                case VibrationStyle.Warning:
                    MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.Warning);
                    break;
                default:
                    MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.SoftImpact);
                    break;
            }
        }

        #endregion
    }
}
