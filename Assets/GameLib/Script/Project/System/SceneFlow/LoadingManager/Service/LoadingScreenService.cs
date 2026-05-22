#nullable enable
// Assets/Game/Script/Loading/LoadingScreenService.cs
using System;
using Cysharp.Threading.Tasks;
using Game;
using Game.Project.Bootstrap;
using UnityEngine;
using Game.Project;

namespace Game.Loading
{
    public interface ILoadingScreenService
    {
        bool IsShowing { get; }
        float CurrentProgress { get; }

        UniTask ShowAsync(string message = "Loading...");
        void SetProgress(float progress, string? message = null);
        UniTask HideAsync();
    }

    /// <summary>
    /// ローディング画面の表示/非表示と進捗更新を担当するサービス。
    /// LoadingScreenMB を plain component として生成し、direct に更新する。
    /// </summary>
    public sealed class LoadingScreenService : ILoadingScreenService, IScopeAcquireHandler, IScopeReleaseHandler, IDisposable
    {
        const string DefaultLoadingMessage = "Loading...";

        static LoadingScreenMB s_loadingScene;
        static bool s_loadingSceneOwnedByVerifiedBoot;
        static bool s_creating;

        readonly ILoadingScreenConfig _config;

        LoadingScreenMB _loadingScene;

        bool _isShowing;
        float _currentProgress;
        bool _transitioning;
        string _currentMessage = string.Empty;

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
            s_loadingSceneOwnedByVerifiedBoot = false;
            s_creating = false;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            EnsureLoadingSceneInstance();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            Dispose();
        }

        bool TryEnsureLoadingScene()
        {
            try
            {
                return EnsureLoadingSceneInstance();
            }
            catch (InvalidOperationException ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        bool EnsureLoadingSceneInstance()
        {
            if (_loadingScene != null)
            {
                if (KernelLiveBootRuntime.IsVerifiedLiveBootReady && ReferenceEquals(_loadingScene, s_loadingScene) && s_loadingSceneOwnedByVerifiedBoot)
                {
                    ValidateExplicitLoadingSceneParent(_loadingScene, ResolveVerifiedLoadingParentTransform());
                }

                return true;
            }

            if (_config == null || _config.LoadingScenePrefab == null)
                return false;

            if (KernelLiveBootRuntime.IsVerifiedBootInProgress)
                return false;

            if (KernelLiveBootRuntime.IsVerifiedLiveBootReady)
            {
                if (!KernelLiveBootRuntime.IsSceneHandoffInProgress && !KernelLiveBootRuntime.IsSceneHandoffReady)
                {
                    throw new InvalidOperationException("LoadingScreenService requires verified scene handoff before it can create loading scene ownership.");
                }

                return EnsureVerifiedLoadingSceneInstance();
            }

            throw new InvalidOperationException("LoadingScreenService requires verified live boot authority before it can create loading scene ownership.");
        }

        bool EnsureVerifiedLoadingSceneInstance()
        {
            if (_loadingScene != null)
                return true;

            if (_config == null || _config.LoadingScenePrefab == null)
                return false;

            if (s_loadingScene != null && s_loadingScene.gameObject == null)
            {
                s_loadingScene = null;
                s_loadingSceneOwnedByVerifiedBoot = false;
            }

            if (s_loadingScene != null)
            {
                if (!s_loadingSceneOwnedByVerifiedBoot)
                    throw new InvalidOperationException("Verified live boot detected a pre-existing loading scene that was not created by the verified boot path.");

                ValidateExplicitLoadingSceneParent(s_loadingScene, ResolveVerifiedLoadingParentTransform());
                _loadingScene = s_loadingScene;
                return true;
            }

            if (s_creating)
                return false;

            Transform loadingParent = ResolveVerifiedLoadingParentTransform();

            s_creating = true;
            LoadingScreenMB? inst = null;
            try
            {
                inst = UnityEngine.Object.Instantiate(_config.LoadingScenePrefab, loadingParent);
                if (inst == null)
                    return false;

                inst.gameObject.name = "[Singleton] LoadingScene";
                inst.Hide();
                s_loadingScene = inst;
                s_loadingSceneOwnedByVerifiedBoot = true;
                _loadingScene = inst;
                return true;
            }
            finally
            {
                if (inst == null)
                {
                    s_loadingScene = null;
                    s_loadingSceneOwnedByVerifiedBoot = false;
                }

                s_creating = false;
            }
        }

        Transform ResolveVerifiedLoadingParentTransform()
        {
            if (KernelLiveBootRuntime.TryGetExplicitLoadingParent(out Transform? loadingParent) && loadingParent != null)
                return loadingParent;

            throw new InvalidOperationException("Verified live boot requires an explicit loading parent before LoadingScreenService can create the loading scene.");
        }

        static void ValidateExplicitLoadingSceneParent(LoadingScreenMB scope, Transform desired)
        {
            if (scope == null)
                return;

            if (desired == null)
                return;

            var current = scope.transform.parent;
            if (current == desired)
                return;

            throw new InvalidOperationException("Verified live boot detected loading scene parent drift. Repair is not accepted; recreate the loading scene from the explicit verified boot path.");
        }

        public UniTask ShowAsync(string message = DefaultLoadingMessage)
        {
            if (_config == null)
                return UniTask.CompletedTask;

            if (_isShowing)
            {
                SetProgress(_currentProgress, message);
                return UniTask.CompletedTask;
            }

            if (_transitioning)
                return UniTask.CompletedTask;

            _transitioning = true;

            try
            {
                if (!TryEnsureLoadingScene())
                {
                    Dispose();
                    return UniTask.CompletedTask;
                }

                _currentMessage = string.IsNullOrWhiteSpace(message) ? DefaultLoadingMessage : message;
                _currentProgress = 0f;
                _isShowing = true;
                _loadingScene?.Show(_currentMessage, _currentProgress);
            }
            finally
            {
                _transitioning = false;
            }

            return UniTask.CompletedTask;
        }

        public void SetProgress(float progress, string? message = null)
        {
            _currentProgress = Mathf.Clamp01(progress);
            if (message != null)
                _currentMessage = string.IsNullOrWhiteSpace(message) ? DefaultLoadingMessage : message;

            if (!_isShowing || _loadingScene == null)
                return;

            _loadingScene.SetProgress(_currentProgress, _currentMessage);
        }

        public UniTask HideAsync()
        {
            if (_config == null)
                return UniTask.CompletedTask;

            if (_transitioning)
            {
                return UniTask.CompletedTask;
            }

            _transitioning = true;
            try
            {
                _loadingScene?.Hide();
            }
            finally
            {
                _transitioning = false;
                _isShowing = false;
                _currentProgress = 0f;
                _currentMessage = string.Empty;
            }

            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            ReleaseLoadingSceneInstance();
            ResetState();
        }

        void ReleaseLoadingSceneInstance()
        {
            LoadingScreenMB loadingScene = _loadingScene;
            if (loadingScene == null)
                return;

            if (ReferenceEquals(loadingScene, s_loadingScene))
            {
                if (s_loadingSceneOwnedByVerifiedBoot && loadingScene.gameObject != null)
                {
                    DestroyLoadingSceneGameObject(loadingScene.gameObject);
                }

                s_loadingScene = null;
                s_loadingSceneOwnedByVerifiedBoot = false;
            }

            _loadingScene = null;
        }

        static void DestroyLoadingSceneGameObject(GameObject gameObject)
        {
            if (gameObject == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                return;
            }
#endif
            UnityEngine.Object.Destroy(gameObject);
        }

        void ResetState()
        {
            _loadingScene = null;
            _isShowing = false;
            _currentProgress = 0f;
            _currentMessage = string.Empty;
            _transitioning = false;
        }
    }
}
