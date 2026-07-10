using System.Collections;
using BusGame.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wayfu.Lamkn;

namespace BusSort.Gameplay
{
    /// <summary>
    /// Lives in the StartGame scene. Shows the loading popup, async-loads the GamePlay scene additively,
    /// and unloads StartGame once GameplayController reports built. PopupController persists across scenes
    /// (DontDestroyOnLoad) so the loading bar stays on screen during the swap.
    ///
    /// Auto-bootstraps via RuntimeInitializeOnLoadMethod when the active scene is StartGame, so the scene
    /// doesn't need any wiring — just exists.
    /// </summary>
    public sealed class StartGameBootstrap : MonoBehaviour
    {
        private const string StartScene   = "StartGame";
        private const string GameplayScene = "GamePlay";

        [Tooltip("Thời gian (giây) StartScene hiển thị trước khi bắt đầu load GamePlay.")]
        [SerializeField] private float startSceneDuration = 0.1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            var active = SceneManager.GetActiveScene();
            if (active.name != StartScene) return;
            // Nếu designer đã đặt component trong scene thì dùng luôn, không tạo thêm.
            if (FindObjectOfType<StartGameBootstrap>() != null) return;
            var go = new GameObject("[StartGameBootstrap]");
            SceneManager.MoveGameObjectToScene(go, active);
            go.AddComponent<StartGameBootstrap>();
        }

        private void Start()
        {
            GameplayReadiness.Reset();
            StartCoroutine(StartRoutine());
        }

        private IEnumerator StartRoutine()
        {
            // Hiện loading ngay, không animation.
            PopupController.Instance.ShowLoadingInstant();

            // Chờ StartScene hiển thị đủ thời gian rồi mới bắt đầu load GamePlay.
            if (startSceneDuration > 0f)
                yield return new WaitForSeconds(startSceneDuration);

            var op = SceneManager.LoadSceneAsync(GameplayScene, LoadSceneMode.Additive);
            // Fallback: nếu GamePlay scene chưa có GameplayController gọi MarkReady (vd. dự án mới),
            // tự signal ready ngay khi scene load xong để loading không kẹt vô tận.
            op.completed += _ => { if (!GameplayReadiness.IsReady) GameplayReadiness.MarkReady(); };

            PopupController.Instance.ShowLoadingUntilReady(
                isReady: () => GameplayReadiness.IsReady,
                onComplete: OnLoadingDone);
        }

        private static void OnLoadingDone()
        {
            GameplayReadiness.SignalLoadingComplete();
            SceneManager.UnloadSceneAsync(StartScene);
            SoundController.Instance?.PlayDefaultBGM();
        }
    }
}
