#nullable enable
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Kernel.V21.Composition;

namespace Game.Kernel.V21.Unity
{
    [DefaultExecutionOrder(-31990)]
    public sealed class SceneKernelHostMB : MonoBehaviour
    {
        const string DefaultObjectName = "SceneKernel";

        static int s_nextSceneKernelHandle;

        [SerializeField] string sceneNameOverride = string.Empty;

        SceneKernel? runtimeKernel;
        SceneKernelV2Composition? sceneComposition;

        public SceneKernel RuntimeKernel => runtimeKernel ?? throw new InvalidOperationException("SceneKernelHostMB runtime kernel has not been initialized.");

        public int SceneHandle => gameObject.scene.handle;

        public string EffectiveSceneName => string.IsNullOrWhiteSpace(sceneNameOverride) ? gameObject.scene.name : sceneNameOverride;

        public static SceneKernelHostMB EnsureForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new ArgumentException("SceneKernelHostMB can only be ensured for a valid loaded scene.", nameof(scene));

            SceneKernelHostMB? existing = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                SceneKernelHostMB? candidate = roots[index].GetComponent<SceneKernelHostMB>();
                if (candidate == null)
                    continue;

                if (existing != null && existing != candidate)
                    throw new InvalidOperationException("Only one root SceneKernelHostMB may exist per loaded scene.");

                existing = candidate;
            }

            if (existing != null)
                return existing;

            GameObject root = new GameObject(DefaultObjectName);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.AddComponent<SceneKernelHostMB>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            s_nextSceneKernelHandle = 0;
        }

        void Awake()
        {
            if (transform.parent != null)
                throw new InvalidOperationException("SceneKernelHostMB must be placed at the scene root.");

            runtimeKernel ??= new SceneKernel(new SceneKernelHandle(Interlocked.Increment(ref s_nextSceneKernelHandle)), EffectiveSceneName);
            if (runtimeKernel.State == KernelLayerState.Created)
                runtimeKernel.Initialize();

            if (runtimeKernel.Composition == null)
            {
                sceneComposition ??= SceneKernelV2Composition.CreatePending();
                runtimeKernel.AttachComposition(sceneComposition);
            }

            ApplicationKernelHostMB.EnsureExists().RegisterSceneKernelHost(this);
        }

        void OnDestroy()
        {
            if (ApplicationKernelHostMB.TryGetInstance(out ApplicationKernelHostMB? applicationKernelHost) && applicationKernelHost != null)
                applicationKernelHost.UnregisterSceneKernelHost(this);

            ShutdownRuntimeKernelIfDetached();
        }

        internal void ShutdownRuntimeKernelIfDetached()
        {
            if (runtimeKernel != null && runtimeKernel.OwnerApplicationKernel == null && runtimeKernel.State != KernelLayerState.Shutdown)
                runtimeKernel.Shutdown();
        }
    }
}
