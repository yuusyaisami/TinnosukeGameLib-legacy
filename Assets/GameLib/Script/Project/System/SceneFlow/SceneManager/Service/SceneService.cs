// Assets/Game/Script/Flow/SceneService.cs
using System;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Loading;
using Game.Project;
using Game.Vars.Generated;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Game.Flow
{
    public enum GameScene
    {
        Title,
        Lobby,
        Game,
        Run,
        GameUI,
        HUD,
        Menu,
        // 必要に応じて追加
    }

    public static class GameSceneExtensions
    {
        public static string ToSceneName(this GameScene scene)
        {
            // 実際の Scene 名と合わせて書く
            return scene switch
            {
                GameScene.Title => "TitleScene",
                GameScene.Lobby => "Lobby",
                GameScene.Game => "GameScene",
                GameScene.Run => "Run",
                GameScene.GameUI => "GameUI",
                GameScene.HUD => "HUD",
                GameScene.Menu => "Menu",
                _ => scene.ToString()
            };
        }

        /// <summary>
        /// 「UI オーバーレイ系なのでローディング画面はいらない」みたいな判定。
        /// 必要なら適宜ここをいじる。
        /// </summary>
        public static bool IsUiOverlayScene(this GameScene scene)
        {
            return scene is GameScene.GameUI or GameScene.HUD or GameScene.Menu;
        }
    }
    public interface ISceneService
    {
        UniTask LoadSingle(GameScene scene, bool forceReload = false);
        UniTask LoadAdditive(GameScene scene);
        UniTask Unload(GameScene scene);
        bool IsLoaded(GameScene scene);
        UniTask LoadSingle(string sceneName, bool forceReload = false);
        UniTask LoadAdditive(string sceneName);
        UniTask Unload(string sceneName);
        bool IsLoaded(string sceneName);
    }
    public sealed class SceneService : ISceneService
    {
        readonly ILoadingScreenService _loading;
        readonly IProjectBlackboardService _blackboard;
        readonly float _commandLeadTimeBeforeSceneChangeSeconds;

        const float MinSingleLoadDuration = 1.0f;
        const float MinAdditiveLoadDuration = 0.5f;

        [Inject]
        public SceneService(ILoadingScreenService loading, IProjectBlackboardService blackboard, ILoadingScreenConfig loadingConfig)
        {
            _loading = loading;
            _blackboard = blackboard;
            _commandLeadTimeBeforeSceneChangeSeconds = loadingConfig?.CommandLeadTimeBeforeSceneChangeSeconds ?? 0f;
        }

        public bool IsLoaded(GameScene scene)
        {
            return IsLoaded(scene.ToSceneName());
        }

        bool ShouldShowLoading(GameScene scene, bool additive)
        {
            if (_loading == null)
                return false;

            // UI オーバーレイ系にはローディング画面を出さない
            if (scene.IsUiOverlayScene())
                return false;

            // 必要であれば additive 時は抑制、などのルールもここに追加
            // if (additive) { ... }

            return true;
        }

        public async UniTask LoadSingle(GameScene scene, bool forceReload = false)
        {
            await LoadSingle(scene.ToSceneName(), forceReload);
        }

        public async UniTask LoadAdditive(GameScene scene)
        {
            await LoadAdditive(scene.ToSceneName());
        }

        public async UniTask Unload(GameScene scene)
        {
            await Unload(scene.ToSceneName());
        }

        public bool IsLoaded(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return false;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).name == sceneName)
                    return true;
            }
            return false;
        }

        public async UniTask LoadSingle(string sceneName, bool forceReload = false)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return;

            if (IsLoaded(sceneName) &&
                SceneManager.GetActiveScene().name == sceneName &&
                !forceReload)
            {
                return;
            }

            SetIsLoading(true);
            try
            {
                var showLoading = ShouldShowLoadingName(sceneName, additive: false);

                if (showLoading)
                {
                    await ShowWithLeadTimeAsync($"Loading {sceneName}...");
                }

                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

                if (showLoading)
                {
                    await TrackProgressAsync(op, sceneName, MinSingleLoadDuration);
                    await _loading.HideAsync();
                }
                else
                {
                    await op.ToUniTask();
                }
            }
            finally
            {
                SetIsLoading(false);
            }
        }

        public async UniTask LoadAdditive(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return;

            if (IsLoaded(sceneName))
                return;

            SetIsLoading(true);
            try
            {
                var showLoading = ShouldShowLoadingName(sceneName, additive: true);
                var shouldStartNewLoading =
                    showLoading && _loading != null && !_loading.IsShowing;

                if (shouldStartNewLoading)
                {
                    await ShowWithLeadTimeAsync($"Loading {sceneName}...");
                }

                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

                if (showLoading && _loading != null)
                {
                    await TrackProgressAsync(op, sceneName, MinAdditiveLoadDuration);
                }
                else
                {
                    await op.ToUniTask();
                }

                var sc = SceneManager.GetSceneByName(sceneName);
                if (sc.IsValid())
                {
                    SceneManager.SetActiveScene(sc);
                }

                if (shouldStartNewLoading && _loading != null)
                {
                    await _loading.HideAsync();
                }
            }
            finally
            {
                SetIsLoading(false);
            }
        }

        public async UniTask Unload(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return;

            if (!IsLoaded(sceneName))
                return;

            var op = SceneManager.UnloadSceneAsync(sceneName);
            if (op != null)
                await op.ToUniTask();
        }

        async UniTask TrackProgressAsync(AsyncOperation op, string sceneName, float minDuration)
        {
            if (_loading == null)
            {
                await op.ToUniTask();
                return;
            }

            float startTime = Time.time;

            while (!op.isDone)
            {
                float elapsed = Time.time - startTime;
                float rawProgress = Mathf.Clamp01(op.progress / 0.9f);  // 0〜0.9 を 0〜1 に正規化
                float timeNorm = minDuration > 0f ? Mathf.Clamp01(elapsed / minDuration) : 1f;
                float visual = Mathf.Min(rawProgress, timeNorm);
                float smooth = Mathf.SmoothStep(0f, 1f, visual);

                _loading.SetProgress(smooth, $"Loading {sceneName}...");
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            _loading.SetProgress(1f, $"Loading {sceneName} Complete!");
        }

        void SetIsLoading(bool isLoading)
        {
            if (_blackboard == null)
                return;

            _blackboard.TryLocalSetVariant(VarIds.GameLib.SceneManager.IsLoading, DynamicVariant.FromBool(isLoading));
        }

        async UniTask ShowWithLeadTimeAsync(string message)
        {
            if (_loading == null)
                return;

            bool wasShowing = _loading.IsShowing;
            var showTask = _loading.ShowAsync(message);
            await DelayBeforeSceneChangeLeadAsync(wasShowing);
            await showTask;
        }

        async UniTask DelayBeforeSceneChangeLeadAsync(bool skip)
        {
            if (skip || _commandLeadTimeBeforeSceneChangeSeconds <= 0f)
                return;

            await UniTask.Delay(TimeSpan.FromSeconds(_commandLeadTimeBeforeSceneChangeSeconds));
        }

        bool ShouldShowLoadingName(string sceneName, bool additive)
        {
            if (_loading == null)
                return false;

            if (string.IsNullOrWhiteSpace(sceneName))
                return false;

            if (TryResolveGameScene(sceneName, out var gameScene))
                return ShouldShowLoading(gameScene, additive);

            return true;
        }

        static bool TryResolveGameScene(string sceneName, out GameScene scene)
        {
            var values = Enum.GetValues(typeof(GameScene));
            for (int i = 0; i < values.Length; i++)
            {
                var candidate = (GameScene)values.GetValue(i);
                if (string.Equals(candidate.ToSceneName(), sceneName, StringComparison.Ordinal))
                {
                    scene = candidate;
                    return true;
                }
            }

            scene = default;
            return false;
        }
    }
}
