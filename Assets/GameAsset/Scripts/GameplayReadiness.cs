using System;

namespace BusGame.Gameplay
{
    /// <summary>
    /// Cross-scene signal: GamePlay scene flips IsReady=true once <see cref="GameplayController.Build"/> finishes,
    /// StartGame's bootstrap polls it via the loading popup, and OnLoadingComplete fires once the popup
    /// finishes its fake-progress animation and hides — that's the moment to reveal the level + unload StartGame.
    /// </summary>
    public static class GameplayReadiness
    {
        public static bool IsReady { get; private set; }

        // Loading popup đã đóng (đây là lúc mở Gameplay) — giữ flag để bên đăng ký muộn vẫn biết.
        public static bool IsLoadingComplete { get; private set; }
        public static event Action OnLoadingComplete;

        public static void Reset()
        {
            IsReady = false;
            IsLoadingComplete = false;
        }

        public static void MarkReady()
        {
            IsReady = true;
        }

        public static void SignalLoadingComplete()
        {
            IsLoadingComplete = true;
            OnLoadingComplete?.Invoke();
        }
    }
}
