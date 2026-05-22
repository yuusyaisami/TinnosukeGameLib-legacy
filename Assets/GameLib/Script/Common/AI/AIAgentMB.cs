#nullable enable
using System;
using Game.Commands;
using Game.Common;
using Game.Input;
using Game.Movement;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.AI
{
    /// <summary>
    /// AI Agent を管琁E��めEMonoBehaviour、E
    /// Entity に配置して使用、E
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActionBlockMB))]
    public sealed class AIAgentMB : MonoBehaviour, IScopeInstaller
    {
        [Header("Configuration")]
        [Required]
        [SerializeField]
        AIClipProfileSO? _profile;

        [Header("Debug")]
        [SerializeField]
        [ReadOnly]
        string? _currentClipKey;

        [SerializeField]
        [ReadOnly]
        int _stackDepth;

        [FoldoutGroup("State Debug Viewer")]
        [HideLabel]
        [SerializeField]
        AIStateDebugViewer _stateDebugViewer = new();

        IScopeNode? _ownerScope;

        public AIClipProfileSO? Profile => _profile;

        internal void SetDebugState(string? activeClipKey, int stackDepth)
        {
            _currentClipKey = activeClipKey;
            _stackDepth = stackDepth;
        }

        IAIStateService? TryResolveStateService()
        {
            var resolver = _ownerScope?.Resolver;
            if (resolver == null)
                return null;

            return resolver.TryResolve<IAIStateService>(out var service) ? service : null;
        }

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            _ownerScope = scope;

            if (_profile == null)
            {
                Debug.LogError($"[AIAgentMB] Profile is not set on {gameObject.name}");
                return;
            }

            builder.Register<AIStateService>(RuntimeLifetime.Singleton)
                .WithParameter(_profile)
                .WithParameter(scope)
                .As<IAIStateService>()
                .As<IAIStateTelemetry>()
                .As<IScopeTickHandler>()
                .As<IDisposable>();

            builder.Register<AIAgentDebugService>(RuntimeLifetime.Singleton)
                .WithParameter(this)
                .WithParameter(_stateDebugViewer)
                .As<IScopeTickHandler>()
                .As<IDisposable>();
        }

        // ================================================================
        // 外部 API
        // ================================================================

        /// <summary>持E��しぁEClip めEPush</summary>
        public void PushClip(AIClipSO clip)
        {
            TryResolveStateService()?.PushClip(clip);
        }

        /// <summary>現在の Clip めEPop</summary>
        public void PopClip()
        {
            TryResolveStateService()?.PopClip();
        }

        /// <summary>AI がブロチE��されてぁE��ぁE/summary>
        public bool IsBlocked => TryResolveStateService()?.IsBlocked ?? false;

#if UNITY_EDITOR
        [Button("Dump Stack")]
        void DumpStack()
        {
            if (TryResolveStateService() is AIStateService impl)
            {
                Debug.Log(impl.GetStackDump());
            }
        }
#endif
    }
}

