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
    /// AI Agent 繧堤ｮ｡逅・☆繧・MonoBehaviour縲・
    /// Entity 縺ｫ驟咲ｽｮ縺励※菴ｿ逕ｨ縲・
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActionBlockMB))]
    public sealed class AIAgentMB : MonoBehaviour, IFeatureInstaller
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

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
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
        // 螟夜Κ API
        // ================================================================

        /// <summary>謖・ｮ壹＠縺・Clip 繧・Push</summary>
        public void PushClip(AIClipSO clip)
        {
            TryResolveStateService()?.PushClip(clip);
        }

        /// <summary>迴ｾ蝨ｨ縺ｮ Clip 繧・Pop</summary>
        public void PopClip()
        {
            TryResolveStateService()?.PopClip();
        }

        /// <summary>AI 縺後ヶ繝ｭ繝・け縺輔ｌ縺ｦ縺・ｋ縺・/summary>
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
