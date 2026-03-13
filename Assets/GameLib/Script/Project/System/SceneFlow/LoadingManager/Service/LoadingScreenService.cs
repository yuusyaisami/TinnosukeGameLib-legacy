// Assets/Game/Script/Loading/LoadingScreenService.cs
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands.VNext;
using Game.Common;
using Game.Scene;
using VContainer;
using UnityEngine;
using Game.Project;

namespace Game.Loading
{
    public interface ILoadingScreenService
    {
        bool IsShowing { get; }
        float CurrentProgress { get; }

        UniTask ShowAsync(string message = "Loading...");
        void SetProgress(float progress, string message = null);
        UniTask HideAsync();
    }

    /// <summary>
    /// ローディング画面の表示/非表示と進捗更新を担当するサービス。
    /// CommandRunner 経由で LoadingScreenMB の Command を実行する。
    /// </summary>
    public sealed class LoadingScreenService : ILoadingScreenService, IScopeAcquireHandler, IScopeReleaseHandler, IDisposable
    {
        const int MaxRunnerResolveFrames = 60;
        const float ProgressDispatchEpsilon = 0.01f;
        const float MinProgressDispatchIntervalSeconds = 0.05f;

        static SceneLifetimeScope s_loadingScene;
        static bool s_scannedExisting;
        static bool s_creating;

        readonly ILoadingScreenConfig _config;
        readonly VarStore _vars = new();

        BaseLifetimeScope _ownerScope;
        SceneLifetimeScope _loadingScene;
        ICommandRunner _runner;

        bool _isShowing;
        float _currentProgress;
        bool _transitioning;
        bool _isProgressDispatching;
        bool _hasPendingProgressUpdate;
        string _pendingProgressMessage = string.Empty;
        float _lastDispatchedProgress = -1f;
        float _lastProgressDispatchTime = float.NegativeInfinity;
        string _lastDispatchedMessage = string.Empty;

        public bool IsShowing => _isShowing;
        public float CurrentProgress => _currentProgress;

        public LoadingScreenService(ILoadingScreenConfig config)
        {
            _config = config;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_loadingScene = null;
            s_scannedExisting = false;
            s_creating = false;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ownerScope = scope as BaseLifetimeScope;
            EnsureLoadingSceneInstance();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            Dispose();
        }

        bool EnsureLoadingSceneInstance()
        {
            if (_loadingScene != null)
                return true;

            if (_config == null || _config.LoadingScenePrefab == null)
                return false;

            // If domain reload is disabled, a destroyed Unity object may still be held by statics.
            // Unity "fake null" support: if destroyed, treat as null.
            if (s_loadingScene != null && s_loadingScene.gameObject == null)
                s_loadingScene = null;

            if (!s_scannedExisting)
            {
                s_scannedExisting = true;
                // If previous buggy runs left duplicates, keep the first and destroy the rest.
                var existing = UnityEngine.Object.FindObjectsByType<SceneLifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                SceneLifetimeScope first = null;
                for (int i = 0; i < existing.Length; i++)
                {
                    var s = existing[i];
                    if (s == null)
                        continue;
                    if (s.gameObject == null)
                        continue;
                    if (s.gameObject.name != "[Singleton] LoadingScene")
                        continue;

                    if (first == null)
                    {
                        first = s;
                        continue;
                    }

                    UnityEngine.Object.Destroy(s.gameObject);
                }

                if (first != null)
                {
                    s_loadingScene = first;
                    EnsureLoadingSceneParent(first);
                }
            }

            if (s_loadingScene != null)
            {
                if (s_loadingScene.gameObject != null)
                {
                    EnsureLoadingSceneParent(s_loadingScene);
                    _loadingScene = s_loadingScene;
                    _runner = null;
                    return true;
                }
                s_loadingScene = null;
            }

            if (s_creating)
                return false;

            s_creating = true;

            SceneLifetimeScope inst;
            var parent = ResolvePersistentParentTransform();
            // IMPORTANT: instantiate under the persistent scope hierarchy (Global/Platform/Project)
            // so the build-parent is cached correctly during Awake.
            inst = parent != null
                ? UnityEngine.Object.Instantiate(_config.LoadingScenePrefab, parent)
                : UnityEngine.Object.Instantiate(_config.LoadingScenePrefab);

            if (inst == null)
            {
                s_creating = false;
                return false;
            }

            inst.gameObject.name = "[Singleton] LoadingScene";

            s_loadingScene = inst;
            _loadingScene = inst;
            _runner = null;
            s_creating = false;
            return true;
        }

        Transform ResolvePersistentParentTransform()
        {
            // Prefer Global -> Platform -> Project to match the intended hierarchy.
            var globals = UnityEngine.Object.FindObjectsByType<global::Game.GlobalLifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (globals != null && globals.Length > 0 && globals[0] != null)
                return globals[0].transform;

            var platforms = UnityEngine.Object.FindObjectsByType<global::Game.Platform.PlatformLifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (platforms != null && platforms.Length > 0 && platforms[0] != null)
                return platforms[0].transform;

            var projects = UnityEngine.Object.FindObjectsByType<global::Game.ProjectLifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (projects != null && projects.Length > 0 && projects[0] != null)
                return projects[0].transform;

            return _ownerScope != null ? _ownerScope.transform : null;
        }

        void EnsureLoadingSceneParent(SceneLifetimeScope scope)
        {
            if (scope == null)
                return;

            var desired = ResolvePersistentParentTransform();
            if (desired == null)
                return;

            var current = scope.transform.parent;
            if (current == desired)
                return;

            scope.transform.SetParent(desired, worldPositionStays: false);
        }

        async UniTask<bool> EnsureRunnerAsync(CancellationToken ct)
        {
            if (_runner != null)
                return true;

            if (!EnsureLoadingSceneInstance())
                return false;

            for (int i = 0; i < MaxRunnerResolveFrames; i++)
            {
                if (_loadingScene == null)
                    return false;

                var resolver = _loadingScene.Resolver;
                if (resolver != null && resolver.TryResolve<ICommandRunner>(out var runner) && runner != null)
                {
                    _runner = runner;
                    return true;
                }

                if (ct.IsCancellationRequested)
                    return false;

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            return false;
        }

        void UpdateContextVars(string message)
        {
            if (_config == null)
                return;

            if (_config.MessageVar.VarId > 0)
                _vars.TrySetVariant(_config.MessageVar.VarId, DynamicVariant.FromString(message ?? string.Empty));

            if (_config.ProgressVar.VarId > 0)
                _vars.TrySetVariant(_config.ProgressVar.VarId, DynamicVariant.FromFloat(_currentProgress));
        }

        async UniTask ExecuteCommandsAsync(CommandListData commands, string message, CancellationToken ct)
        {
            if (commands == null || commands.Count == 0)
                return;

            if (!await EnsureRunnerAsync(ct))
                return;

            if (_loadingScene == null || _loadingScene.Resolver == null || _runner == null)
                return;

            UpdateContextVars(message);
            var ctx = new CommandContext(_loadingScene, _vars, _runner, actor: _loadingScene, options: CommandRunOptions.Default);

            try
            {
                await _runner.ExecuteListAsync(commands, ctx, ct, ctx.Options);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public async UniTask ShowAsync(string message = "Loading...")
        {
            if (_config == null)
                return;

            if (_isShowing)
            {
                SetProgress(_currentProgress, message);
                return;
            }

            if (_transitioning)
                return;

            _transitioning = true;
            _currentProgress = 0f;
            _isShowing = true;
            _lastDispatchedProgress = -1f;
            _lastProgressDispatchTime = float.NegativeInfinity;
            _lastDispatchedMessage = string.Empty;

            try
            {
                await ExecuteCommandsAsync(_config.ShowCommands, message, CancellationToken.None);
            }
            finally
            {
                _transitioning = false;
            }
        }

        public void SetProgress(float progress, string message = null)
        {
            _currentProgress = Mathf.Clamp01(progress);
            _pendingProgressMessage = message ?? string.Empty;
            _hasPendingProgressUpdate = true;

            if (!_isShowing || _config == null)
                return;

            var commands = _config.ProgressCommands;
            if (commands == null || commands.Count == 0)
                return;

            if (_isProgressDispatching)
                return;

            _isProgressDispatching = true;
            UniTask.Void(async () => await DispatchProgressCommandsLoopAsync(commands));
        }

        public async UniTask HideAsync()
        {
            if (_transitioning)
            {
                return;
            }

            _transitioning = true;
            try
            {
                await ExecuteCommandsAsync(_config.HideCommands, string.Empty, CancellationToken.None);
            }
            finally
            {
                _transitioning = false;
                _isShowing = false;
                _currentProgress = 0f;
            }
        }

        public void Dispose()
        {
            ResetState();
        }

        void ResetState()
        {
            _runner = null;
            _loadingScene = null;
            _vars.Clear();
            _isShowing = false;
            _currentProgress = 0f;
            _transitioning = false;
            _isProgressDispatching = false;
            _hasPendingProgressUpdate = false;
            _pendingProgressMessage = string.Empty;
            _lastDispatchedProgress = -1f;
            _lastProgressDispatchTime = float.NegativeInfinity;
            _lastDispatchedMessage = string.Empty;
        }

        async UniTask DispatchProgressCommandsLoopAsync(CommandListData commands)
        {
            try
            {
                while (_isShowing && _config != null && commands != null && commands.Count > 0)
                {
                    if (!_hasPendingProgressUpdate)
                        break;

                    _hasPendingProgressUpdate = false;
                    var progress = _currentProgress;
                    var message = _pendingProgressMessage;
                    if (!ShouldDispatchProgressUpdate(progress, message))
                        continue;

                    await ExecuteCommandsAsync(commands, message, CancellationToken.None);
                    _lastDispatchedProgress = progress;
                    _lastDispatchedMessage = message;
                    _lastProgressDispatchTime = Time.unscaledTime;

                    if (_hasPendingProgressUpdate)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                _isProgressDispatching = false;
            }
        }

        bool ShouldDispatchProgressUpdate(float progress, string message)
        {
            var messageChanged = !string.Equals(_lastDispatchedMessage, message ?? string.Empty, StringComparison.Ordinal);
            var progressDiff = Mathf.Abs(progress - _lastDispatchedProgress);
            var progressChanged = _lastDispatchedProgress < 0f || progressDiff >= ProgressDispatchEpsilon;
            var isCompleted = progress >= 0.999f;
            var intervalElapsed = Time.unscaledTime - _lastProgressDispatchTime >= MinProgressDispatchIntervalSeconds;

            if (isCompleted)
                return true;

            if (messageChanged)
                return intervalElapsed || progressChanged;

            if (!progressChanged)
                return false;

            return intervalElapsed;
        }
    }
}
