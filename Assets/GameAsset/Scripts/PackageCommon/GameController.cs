using System;
using UnityEngine;

namespace Wayfu.Lamkn
{
    /// <summary>
    /// Máy trạng thái gameplay: theo dõi WIN (phá hết block) / LOSE (path đầy gun & không gun nào
    /// bắn được) — yêu cầu #3. Phát event OnWin/OnLose để UI (WinPopup/LosePopup) tự lắng nghe.
    /// </summary>
    public class GameController : Singleton<GameController>
    {
        public enum GameState { None, Playing, Win, Lose }

        public GameState State { get; private set; } = GameState.None;

        public event Action OnWin;
        public event Action OnLose;

        public void StartLevel() => State = GameState.Playing;

        /// <summary>Gọi sau mỗi thay đổi bàn chơi (deploy gun / bắn / cột bị phá).</summary>
        public void OnBoardChanged()
        {
            if (State != GameState.Playing) return;
            if (CheckWin()) return;
            CheckLose();
        }

        private bool CheckWin()
        {
            if (GridBlockManager.Instance != null && GridBlockManager.Instance.AllCleared)
            {
                Win();
                return true;
            }
            return false;
        }

        private void CheckLose()
        {
            var pm = PathManager.Instance;
            if (pm == null) return;
            if (!pm.IsFull) return;            // chỉ xét khi path đã đầy gun
            if (pm.AnyGunHasTarget()) return;  // còn gun bắn được → chưa thua
            Lose();
        }

        private void Win()
        {
            State = GameState.Win;
            Debug.Log("[GameController] WIN");
            OnWin?.Invoke();
        }

        private void Lose()
        {
            State = GameState.Lose;
            Debug.Log("[GameController] LOSE");
            OnLose?.Invoke();
        }
    }
}
