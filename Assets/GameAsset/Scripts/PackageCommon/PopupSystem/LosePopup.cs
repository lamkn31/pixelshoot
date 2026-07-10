using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wayfu.Lamkn
{
    public class LosePopup : BasePopup
    {
        #region Inspector

        [Header("Lose Refs")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button homeButton;
        [SerializeField] GameObject btnHoldToView;

        [Header("Completion")]
        [Tooltip("Slider thể hiện % hoàn thành level = số xe đã rời map / tổng số xe ban đầu.")]
        [SerializeField] private Slider progressSlider;
        [Tooltip("Text % hoàn thành (tuỳ chọn). {0} = số nguyên %.")]
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private string progressFormat = "{0}%";
        [Tooltip("Thời gian chạy slider từ 0 → value (giây). 0 = set thẳng, không chạy.")]
        [SerializeField] private float progressAnimDuration = 0.6f;

        private Coroutine _progressRoutine;
        private Coroutine _viewMapFadeCoroutine;
        #endregion

        #region State

        private Action _onRetry;
        private Action _onHome;

        #endregion

        #region Unity

        protected override void Awake()
        {
            base.Awake();
            if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
            if (homeButton != null) homeButton.onClick.AddListener(OnHomeClicked);
            if (btnHoldToView != null) SetupHoldToView();
        }

        #endregion

        #region API

        public void Show(string title, Action onRetry = null, Action onHome = null, float progress01 = 0f)
        {
            if (titleText != null) titleText.text = title;
            _onRetry = onRetry;
            _onHome = onHome;
            SoundController.Instance?.PlayLoseSound();
            base.Show();
            ApplyProgress(progress01); // sau base.Show() để GO đã active → coroutine chạy slider được
        }

        // Chạy slider + text % hoàn thành TỪ 0 → value (số xe đã rời map / tổng số xe ban đầu).
        private void ApplyProgress(float progress01)
        {
            progress01 = Mathf.Clamp01(progress01);
            if (progressSlider != null)
            {
                progressSlider.minValue = 0f;
                progressSlider.maxValue = 1f;
            }
            if (_progressRoutine != null) { StopCoroutine(_progressRoutine); _progressRoutine = null; }
            if (isActiveAndEnabled && progressAnimDuration > 0f)
                _progressRoutine = StartCoroutine(ProgressRoutine(progress01));
            else
                SetProgressDisplay(progress01);
        }

        private IEnumerator ProgressRoutine(float target)
        {
            float dur = Mathf.Max(0.01f, progressAnimDuration);
            float t = 0f;
            SetProgressDisplay(0f);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                SetProgressDisplay(Mathf.Lerp(0f, target, t / dur));
                yield return null;
            }
            SetProgressDisplay(target);
            _progressRoutine = null;
        }

        // Set đồng thời slider value + text % theo cùng 1 giá trị (0..1).
        private void SetProgressDisplay(float v)
        {
            if (progressSlider != null) progressSlider.value = v;
            if (progressText != null)
                progressText.text = string.Format(progressFormat, Mathf.RoundToInt(v * 100f));
        }

        #endregion

        #region Callbacks


        private void OnRetryClicked()
        {
            SoundController.Instance?.PlayButtonClick();
            VibrationController.Instance?.Vibrate(VibrationStyle.Light);
            _onRetry?.Invoke();
            Hide();
            SoundController.Instance.PlayDefaultBGM();
        }

        private void OnHomeClicked()
        {
            SoundController.Instance?.PlayButtonClick();
            VibrationController.Instance?.Vibrate(VibrationStyle.Light);
            _onHome?.Invoke();
            Hide();
            SoundController.Instance.PlayDefaultBGM();
        }
        private void SetupHoldToView()
        {

            EventTrigger trigger = btnHoldToView.GetComponent<EventTrigger>();
            if (trigger == null) trigger = btnHoldToView.AddComponent<EventTrigger>();

            EventTrigger.Entry entryDown = new EventTrigger.Entry();
            entryDown.eventID = EventTriggerType.PointerDown;
            entryDown.callback.AddListener((data) => { OnHoldViewStart(); });
            trigger.triggers.Add(entryDown);

            EventTrigger.Entry entryUp = new EventTrigger.Entry();
            entryUp.eventID = EventTriggerType.PointerUp;
            entryUp.callback.AddListener((data) => { OnHoldViewEnd(); });
            trigger.triggers.Add(entryUp);
        }
        private void OnHoldViewStart()
        {
            if (_canvasGroup != null)
            {
                if (_viewMapFadeCoroutine != null) StopCoroutine(_viewMapFadeCoroutine);
                _viewMapFadeCoroutine = StartCoroutine(FadeAlphaRoutine(0f));
            }
        }
        private void OnHoldViewEnd()
        {
            if (_canvasGroup != null)
            {
                if (_viewMapFadeCoroutine != null) StopCoroutine(_viewMapFadeCoroutine);
                _viewMapFadeCoroutine = StartCoroutine(FadeAlphaRoutine(1f));
            }
        }
        private IEnumerator FadeAlphaRoutine(float targetAlpha)
        {
            float startAlpha = _canvasGroup.alpha;
            float duration = 0.2f;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / duration);
                yield return null;
            }
            _canvasGroup.alpha = targetAlpha;
            _viewMapFadeCoroutine = null;
        }
        #endregion
    }
}
