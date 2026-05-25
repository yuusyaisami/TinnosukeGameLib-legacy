#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Kernel.Layers.Composition;

namespace Game.Kernel.Layers.Unity
{
    [DefaultExecutionOrder(-32000)]
    public sealed class ApplicationKernelHostMB : MonoBehaviour
    {
        const string DefaultObjectName = "ApplicationKernel";

        static ApplicationKernelHostMB? s_instance;
        static bool s_isQuitting;

        readonly Dictionary<int, SceneKernelHostMB> sceneHostsBySceneHandle = new Dictionary<int, SceneKernelHostMB>(4);

        ApplicationKernel? runtimeKernel;
        ApplicationKernelComposition? applicationComposition;

        public static ApplicationKernelHostMB Instance
        {
            get
            {
                if (s_instance == null)
                    throw new InvalidOperationException("ApplicationKernelHostMB has not been created.");

                return s_instance;
            }
        }

        public ApplicationKernel RuntimeKernel => runtimeKernel ?? throw new InvalidOperationException("ApplicationKernelHostMB runtime kernel has not been initialized.");

        public SceneKernelHostMB? CurrentSceneKernelHost { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnsureExistsBeforeSceneLoad()
        {
            if (s_isQuitting || s_instance != null)
                return;

            GameObject root = new GameObject(DefaultObjectName);
            root.AddComponent<ApplicationKernelHostMB>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            s_instance = null;
            s_isQuitting = false;
        }

        public static ApplicationKernelHostMB EnsureExists()
        {
            if (s_instance != null)
                return s_instance;

            GameObject root = new GameObject(DefaultObjectName);
            return root.AddComponent<ApplicationKernelHostMB>();
        }

        public static bool TryGetInstance(out ApplicationKernelHostMB? instance)
        {
            instance = s_instance;
            return instance != null;
        }

        void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);

            runtimeKernel ??= new ApplicationKernel(DefaultObjectName);
            if (runtimeKernel.State == KernelLayerState.Created)
                runtimeKernel.Initialize();

            if (runtimeKernel.Composition == null)
            {
                applicationComposition ??= ApplicationKernelComposition.CreateDefault();
                runtimeKernel.AttachComposition(applicationComposition);
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        void OnApplicationQuit()
        {
            s_isQuitting = true;
        }

        void OnDestroy()
        {
            if (s_instance != this)
                return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

            if (CurrentSceneKernelHost != null)
                DetachCurrentSceneKernelHost(shutdownDetachedSceneKernel: true);

            if (runtimeKernel != null && applicationComposition != null && ReferenceEquals(runtimeKernel.Composition, applicationComposition))
                runtimeKernel.DetachComposition(applicationComposition);

            if (runtimeKernel != null && runtimeKernel.State != KernelLayerState.Shutdown)
                runtimeKernel.Shutdown();

            sceneHostsBySceneHandle.Clear();
            applicationComposition = null;
            s_instance = null;
        }

        internal void RegisterSceneKernelHost(SceneKernelHostMB sceneKernelHost)
        {
            if (sceneKernelHost == null)
                throw new ArgumentNullException(nameof(sceneKernelHost));

            int sceneHandle = sceneKernelHost.gameObject.scene.handle;
            if (sceneHandle == 0)
                throw new InvalidOperationException("SceneKernelHostMB must belong to a loaded scene.");

            if (sceneHostsBySceneHandle.TryGetValue(sceneHandle, out SceneKernelHostMB? existing) && existing != sceneKernelHost)
                throw new InvalidOperationException("Only one SceneKernelHostMB may exist per loaded scene.");

            sceneHostsBySceneHandle[sceneHandle] = sceneKernelHost;

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.handle == sceneHandle)
                AttachSceneKernelHost(sceneKernelHost);
        }

        internal void UnregisterSceneKernelHost(SceneKernelHostMB sceneKernelHost)
        {
            if (sceneKernelHost == null)
                throw new ArgumentNullException(nameof(sceneKernelHost));

            int sceneHandle = sceneKernelHost.gameObject.scene.handle;
            if (CurrentSceneKernelHost == sceneKernelHost)
                DetachCurrentSceneKernelHost();

            if (sceneHandle != 0)
                sceneHostsBySceneHandle.Remove(sceneHandle);
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneKernelHostMB sceneKernelHost = SceneKernelHostMB.EnsureForScene(scene);
            RegisterSceneKernelHost(sceneKernelHost);
        }

        void HandleSceneUnloaded(Scene scene)
        {
            if (CurrentSceneKernelHost != null && CurrentSceneKernelHost.SceneHandle == scene.handle)
                DetachCurrentSceneKernelHost();

            sceneHostsBySceneHandle.Remove(scene.handle);
        }

        void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            if (!nextScene.IsValid() || !nextScene.isLoaded)
                return;

            SceneKernelHostMB sceneKernelHost = SceneKernelHostMB.EnsureForScene(nextScene);
            RegisterSceneKernelHost(sceneKernelHost);
            AttachSceneKernelHost(sceneKernelHost);
        }

        void AttachSceneKernelHost(SceneKernelHostMB sceneKernelHost)
        {
            if (CurrentSceneKernelHost == sceneKernelHost)
                return;

            DetachCurrentSceneKernelHost();
            RuntimeKernel.AttachSceneKernel(sceneKernelHost.RuntimeKernel);
            CurrentSceneKernelHost = sceneKernelHost;
        }

        void DetachCurrentSceneKernelHost(bool shutdownDetachedSceneKernel = false)
        {
            if (CurrentSceneKernelHost == null)
                return;

            SceneKernelHostMB detachedHost = CurrentSceneKernelHost;
            RuntimeKernel.DetachSceneKernel(detachedHost.RuntimeKernel);
            CurrentSceneKernelHost = null;

            if (shutdownDetachedSceneKernel)
                detachedHost.ShutdownRuntimeKernelIfDetached();
        }
    }
}
