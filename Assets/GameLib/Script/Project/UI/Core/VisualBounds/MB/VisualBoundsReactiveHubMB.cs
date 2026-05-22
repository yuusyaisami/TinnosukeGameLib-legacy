#nullable enable
using System;
using System.Collections.Generic;
using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class VisualBoundsReactiveHubMB : MonoBehaviour, IScopeInstaller
    {
        [Serializable]
        public sealed class Settings
        {
            [BoxGroup("Update")]
            [LabelText("Rebuild Before Evaluate")]
            [SerializeField]
            bool _rebuildBeforeEvaluate = true;

            [BoxGroup("Update")]
            [LabelText("Execute On Acquire")]
            [SerializeField]
            bool _executeOnAcquire = true;

            [BoxGroup("Update")]
            [LabelText("Position Epsilon")]
            [Min(0f)]
            [SerializeField]
            float _positionEpsilon = 0.1f;

            [BoxGroup("Update")]
            [LabelText("Size Epsilon")]
            [Min(0f)]
            [SerializeField]
            float _sizeEpsilon = 0.1f;

            [BoxGroup("Debug")]
            [LabelText("Enable Debug Log")]
            [SerializeField]
            bool _enableDebugLog;

            public bool RebuildBeforeEvaluate => _rebuildBeforeEvaluate;
            public bool ExecuteOnAcquire => _executeOnAcquire;
            public float PositionEpsilon => Mathf.Max(0f, _positionEpsilon);
            public float SizeEpsilon => Mathf.Max(0f, _sizeEpsilon);
            public bool EnableDebugLog => _enableDebugLog;
        }

        [BoxGroup("Channels")]
        [LabelText("Channels")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<VisualBoundsReactiveChannelDefinition> _channels = new()
        {
            new VisualBoundsReactiveChannelDefinition(),
        };

        [BoxGroup("Settings")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        Settings _settings = new();

        public IReadOnlyList<VisualBoundsReactiveChannelDefinition> Channels => _channels;
        public Settings HubSettings => _settings;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            builder.Register<VisualBoundsReactiveHubService>(RuntimeLifetime.Singleton)
                .WithParameter(owner)
                .WithParameter(this)
                .As<IVisualBoundsReactiveHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IScopeLateTickHandler>();
        }
    }
}
