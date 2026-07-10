using System;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Canvas layout riêng cho tutorial (vd khung viền/nền focus xe). Là một <see cref="BasePopup"/>
    /// nên khi bật/tắt sẽ tự có hiệu ứng fade (và content scale) từ BasePopup —
    /// TutorialController gọi Show()/HideThen() thay cho SetActive.
    /// </summary>
    public class TutorialCanvasPopup : BasePopup
    {
        private Action _onHidden;

        /// <summary>Fade-out (Hide) rồi gọi <paramref name="onComplete"/> SAU khi fade xong. Nếu
        /// popup đang tắt sẵn thì gọi callback ngay.</summary>
        public void HideThen(Action onComplete)
        {
            if (!gameObject.activeSelf)
            {
                onComplete?.Invoke();
                return;
            }
            _onHidden = onComplete;
            Hide();
        }

        protected override void OnHideCompleted()
        {
            base.OnHideCompleted();
            var cb = _onHidden;
            _onHidden = null;
            cb?.Invoke();
        }
    }
}
