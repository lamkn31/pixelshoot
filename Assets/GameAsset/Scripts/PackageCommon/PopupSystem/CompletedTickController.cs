using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Pools UI tick (checkmark) Images under a Canvas. Lives under the GamePlayPopup so its
    /// lifetime is tied to the gameplay HUD — GamePlayPopup exposes this controller via its
    /// <c>TickController</c> property so gameplay code (e.g. GameplayController in BusSort)
    /// can call <see cref="ShowTick"/> when a car completes and <see cref="HideAll"/> on
    /// init / reset / win / lose.
    /// </summary>
    public sealed class CompletedTickController : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("RectTransform under the Canvas where tick Images are spawned. Usually a dedicated empty GO under GamePlayPopup with full-screen rect.")]
        [SerializeField] private RectTransform tickParent;
        [Tooltip("Prefab containing an Image with the checkmark sprite + RectTransform. Pivot at 0.5/0.5.")]
        [SerializeField] private Image tickPrefab;
        [Tooltip("Camera used for World→Screen projection. Leave empty to use Camera.main.")]
        [SerializeField] private Camera worldCamera;

        [Header("Offset")]
        [Tooltip("Vertical offset (canvas units, +Y = up) applied AFTER projecting the car's CENTER to the canvas. Lifts the tick above the car body. Horizontal is centered (no X offset) so it stays on the car center at any rotation.")]
        [SerializeField] private float yOffset = 50f;

        [Header("Timing")]
        [Tooltip("Delay (seconds) AFTER a car completes before its tick appears — lets the car's complete animation + effect finish first. 0 = show immediately.")]
        [SerializeField, Range(0f, 3f)] private float showDelay = 0.9f;

        [Header("Animation")]
        [SerializeField, Range(0.05f, 2f)] private float fromScale = 0.4f;
        [SerializeField, Range(0.05f, 2f)] private float toScale   = 0.2f;
        [SerializeField, Range(0.05f, 2f)] private float duration  = 0.5f;
        [Tooltip("Ease curve for the 0.4 → 0.2 shrink. Default ease-in-out.")]
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private readonly List<Image> _pool = new List<Image>();

        // Số tick đang "in flight" (đang chờ delay HOẶC đang chạy anim scale). >0 = chưa xong.
        private int _inFlight;
        /// <summary>True khi còn tick đang chờ hiện hoặc đang chạy animation. Win popup đợi
        /// flag này về false rồi mới show (xem GameplayController.InvokeResultAfter).</summary>
        public bool IsBusy => _inFlight > 0;

        private Camera Cam => worldCamera != null ? worldCamera : Camera.main;

        /// <summary>Spawn (or reuse from pool) a tick Image at the screen projection of
        /// <paramref name="worldPos"/> and play the scale animation.</summary>
        public void ShowTick(Vector3 worldPos)
        {
            if (tickPrefab == null || tickParent == null) return;
            _inFlight++;
            // Delay so the tick only pops AFTER the car's complete animation + effect finish.
            // HideAll()'s StopAllCoroutines cancels any pending show (reset / win / lose).
            StartCoroutine(ShowRoutine(worldPos));
        }

        private IEnumerator ShowRoutine(Vector3 worldPos)
        {
            if (showDelay > 0f) yield return new WaitForSeconds(showDelay);
            var img = GetFreeTick();
            if (img != null)
            {
                img.gameObject.SetActive(true);
                PositionAtWorld(img.rectTransform, worldPos);
                yield return AnimScale(img.rectTransform);
            }
            _inFlight = Mathf.Max(0, _inFlight - 1);
        }

        /// <summary>Hide every tick in the pool. Called by gameplay on Build (init),
        /// after Win popup fires, and on Lose.</summary>
        public void HideAll()
        {
            StopAllCoroutines();
            _inFlight = 0;
            for (int i = 0; i < _pool.Count; i++)
                if (_pool[i] != null) _pool[i].gameObject.SetActive(false);
        }

        private Image GetFreeTick()
        {
            // First reuse any inactive pooled instance.
            for (int i = 0; i < _pool.Count; i++)
                if (_pool[i] != null && !_pool[i].gameObject.activeSelf) return _pool[i];
            // Pool exhausted → instantiate a new one.
            var inst = Instantiate(tickPrefab, tickParent);
            inst.gameObject.SetActive(false);
            _pool.Add(inst);
            return inst;
        }

        /// <summary>Project a WORLD position onto the tickParent canvas as an anchoredPosition.
        /// Handles both Screen-Space Overlay (uiCam = null) and Screen-Space Camera/World canvases.</summary>
        private void PositionAtWorld(RectTransform rect, Vector3 worldPos)
        {
            var cam = Cam;
            if (cam == null || tickParent == null) return;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
            Canvas canvas = tickParent.GetComponentInParent<Canvas>();
            Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(tickParent, screen, uiCam, out var local))
                rect.anchoredPosition = new Vector2(local.x, local.y + yOffset);
        }

        private IEnumerator AnimScale(RectTransform rect)
        {
            rect.localScale = new Vector3(fromScale, fromScale, 1f);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                float s = Mathf.LerpUnclamped(fromScale, toScale, scaleCurve.Evaluate(u));
                rect.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rect.localScale = new Vector3(toScale, toScale, 1f);
        }
    }
}
