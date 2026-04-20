#nullable enable
using Game;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.SelectRuntime
{
    [DisallowMultipleComponent]
    public sealed class SelectRuntimeManagerMB :
        MonoBehaviour,
        IFeatureInstaller,
        IWorldPointerRuntimeOptions,
        ISelectRuntimeManagerOptions
    {
        [BoxGroup("Pointer")]
        [LabelText("Enabled")]
        [SerializeField]
        bool _enabled = true;

        [BoxGroup("Pointer")]
        [LabelText("Debug Mode")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _enableDebugLog;

        [BoxGroup("State")]
        [LabelText("Is Enabled")]
        [SerializeField]
        DynamicValue<bool> _isEnabled = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Pointer")]
        [LabelText("World Camera")]
        [SerializeField]
        Camera? _worldCamera;

        [BoxGroup("Pointer")]
        [LabelText("Hit Mask")]
        [SerializeField]
        LayerMask _hitMask = ~0;

        [BoxGroup("Pointer")]
        [LabelText("Query Trigger")]
        [SerializeField]
        QueryTriggerInteraction _queryTriggerInteraction = QueryTriggerInteraction.Collide;

        [BoxGroup("Pointer")]
        [LabelText("Long Press Seconds")]
        [MinValue(0.05f)]
        [SerializeField]
        float _longPressSeconds = 0.35f;

        [BoxGroup("Pointer")]
        [LabelText("Short Press Seconds")]
        [MinValue(0.05f)]
        [SerializeField]
        float _shortPressSeconds = 0.15f;

        public bool Enabled => _enabled;
        public bool EnableDebugLog => _enableDebugLog;
        public DynamicValue<bool> IsEnabled => _isEnabled;
        public Camera? WorldCamera => _worldCamera != null ? _worldCamera : Camera.main;
        public LayerMask HitMask => _hitMask;
        public QueryTriggerInteraction QueryTriggerInteraction => _queryTriggerInteraction;
        public float ShortPressSeconds => Mathf.Max(0.05f, _shortPressSeconds);
        public float LongPressSeconds => Mathf.Max(0.05f, _longPressSeconds);

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.RegisterInstance<IWorldPointerRuntimeOptions>(this);
            builder.RegisterInstance<ISelectRuntimeManagerOptions>(this);

            builder.Register<WorldPointerRuntimeService>(RuntimeLifetime.Singleton)
                .As<IWorldPointerRuntimeService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .AsSelf();

            builder.Register<SelectRuntimeManagerService>(RuntimeLifetime.Singleton)
                .As<ISelectRuntimeManagerService>()
                .As<ISelectRuntimeManagerStateProvider>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .WithParameter(scope)
                .AsSelf();

            builder.Register<UserMoveRotateRuntimeService>(RuntimeLifetime.Singleton)
                .As<IUserMoveRotateRuntimeService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .WithParameter(scope)
                .AsSelf();
        }
    }
}
