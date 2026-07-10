using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Wayfu.Lamkn
{
    public class SettingPopup : BasePopup
    {
        #region Inspector

        [Header("BGM")]
        [SerializeField] private Button bgmButton;
        [SerializeField] private Sprite bgmOnSprite;
        [SerializeField] private Sprite bgmOffSprite;

        [Header("SFX")]
        [SerializeField] private Button sfxButton;
        [SerializeField] private Sprite sfxOnSprite;
        [SerializeField] private Sprite sfxOffSprite;

        [Header("Vibration")]
        [SerializeField] private Button vibrationButton;
        [SerializeField] private Sprite vibrationOnSprite;
        [SerializeField] private Sprite vibrationOffSprite;

        [Header("Close")]
        [SerializeField] private Button closeButton;

        [Header("Retry (optional)")]
        [SerializeField] private bool hasRetryButton = false;
        [SerializeField] private Button retryButton;
        [Tooltip("Cover screen with a fade loading popup for this many seconds while the retry callback re-inits the level.")]
        [SerializeField] private float retryFadeDuration = 1f;

        #endregion

        #region State

        private Action _onRetry;

        #endregion

        #region Unity

        protected override void Awake()
        {
            base.Awake();
            if (bgmButton != null) bgmButton.onClick.AddListener(OnBgmClicked);
            if (sfxButton != null) sfxButton.onClick.AddListener(OnSfxClicked);
            if (vibrationButton != null) vibrationButton.onClick.AddListener(OnVibrationClicked);
            if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
            if (hasRetryButton && retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
        }

        #endregion

        #region API

        public override void Show()
        {
            Show(null);
        }

        public void Show(Action onRetry)
        {
            _onRetry = onRetry;
            if (retryButton != null) retryButton.gameObject.SetActive(hasRetryButton);
            RefreshIcons();
            Time.timeScale = 0f;
            base.Show();
        }

        protected override void OnHideCompleted()
        {
            base.OnHideCompleted();
            Time.timeScale = 1f;
        }

        #endregion

        #region Callbacks

        private void RefreshIcons()
        {
            if (SoundController.Instance != null)
            {
                ApplySprite(bgmButton, SoundController.Instance.IsBgmEnabled ? bgmOnSprite : bgmOffSprite);
                ApplySprite(sfxButton, SoundController.Instance.IsSfxEnabled ? sfxOnSprite : sfxOffSprite);
            }
            if (VibrationController.Instance != null)
            {
                ApplySprite(vibrationButton,
                    VibrationController.Instance.IsEnabled ? vibrationOnSprite : vibrationOffSprite);
            }
        }

        private static void ApplySprite(Button button, Sprite sprite)
        {
            if (button == null || button.image == null || sprite == null) return;
            button.image.sprite = sprite;
        }

        private void OnBgmClicked()
        {
            if (SoundController.Instance == null) return;
            SoundController.Instance.ToggleBgm();
            SoundController.Instance.PlayButtonClick();
            VibrationController.Instance?.Vibrate(VibrationStyle.Light);
            ApplySprite(bgmButton, SoundController.Instance.IsBgmEnabled ? bgmOnSprite : bgmOffSprite);
        }

        private void OnSfxClicked()
        {
            if (SoundController.Instance == null) return;
            SoundController.Instance.ToggleSfx();
            SoundController.Instance.PlayButtonClick();
            VibrationController.Instance?.Vibrate(VibrationStyle.Light);
            ApplySprite(sfxButton, SoundController.Instance.IsSfxEnabled ? sfxOnSprite : sfxOffSprite);
        }

        private void OnVibrationClicked()
        {
            if (VibrationController.Instance == null) return;
            VibrationController.Instance.Toggle();
            VibrationController.Instance.Vibrate(VibrationStyle.Light);
            SoundController.Instance?.PlayButtonClick();
            ApplySprite(vibrationButton,
                VibrationController.Instance.IsEnabled ? vibrationOnSprite : vibrationOffSprite);
        }

        private void OnRetryClicked()
        {
            SoundController.Instance?.PlayButtonClick();
            VibrationController.Instance?.Vibrate(VibrationStyle.Light);
            // CLIK: restart trong Setting = bỏ mission hiện tại → báo Failed trước khi rebuild.
            // Guard _missionInProgress trong tracking sẽ tự skip nếu không có mission đang chạy.
            MissionTracking.MissionFailed();
            var cb = _onRetry;
            _onRetry = null;
            Hide();
            // Run the fade+retry on PopupController (DontDestroyOnLoad) so it survives this popup's Hide.
            var pc = PopupController.Instance;
            if (pc != null) pc.StartCoroutine(RetryWithFadeRoutine(pc, cb));
            else cb?.Invoke();
        }

        private IEnumerator RetryWithFadeRoutine(PopupController pc, Action cb)
        {
            pc.ShowLoadingFade();
            // Wait one frame so fade-in begins covering the screen before re-init mutates the world.
            yield return null;
            cb?.Invoke();
            yield return new WaitForSecondsRealtime(retryFadeDuration);
            pc.HideLoading();
            SoundController.Instance?.PlayDefaultBGM();
        }

        private void OnCloseClicked()
        {
            SoundController.Instance?.PlayButtonClick();
            VibrationController.Instance?.Vibrate(VibrationStyle.Light);
            Hide();
        }

        #endregion
    }
}
