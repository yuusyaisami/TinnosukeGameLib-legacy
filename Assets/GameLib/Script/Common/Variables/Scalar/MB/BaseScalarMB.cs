using UnityEngine;
using VContainer;
using VContainer.Unity;
using Sirenix.OdinInspector;
using Game;
using System.Collections.Generic;
using System;

namespace Game.Scalar
{
    [DisallowMultipleComponent]
    public class BaseScalarMB : MonoBehaviour, IFeatureInstaller
    {
        [FoldoutGroup("Debug")]
        [LabelText("Enable Debug View")]
        public bool enableDebugView = false;

        [FoldoutGroup("Debug")]
        [SerializeField, InlineProperty, HideLabel, ShowIf(nameof(enableDebugView))]
        private ScalarDebugView _debugView = new ScalarDebugView();

        IScopeNode _owner;

        // Whether the debug view has been initialized via DI injection or Start fallback
        bool _debugInitialized = false;

        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            _owner = owner;

            // No baseline/init pipeline here. If you need initial values, use ProfileRegistry.
            builder.Register<NullScalarRuntimeConfigProvider>(Lifetime.Singleton)
                .As<IScalarRuntimeConfigProvider>()
                .As<IScalarBaseline>();

            builder.RegisterComponent(this);
            switch (owner.Kind)
            {
                case LifetimeScopeKind.Project:
                    builder.Register<BaseScalarService>(Lifetime.Singleton)
                           .WithParameter(_owner)
                           .As<IBaseScalarService>()
                           .As<IProjectScalarService>()
                           .As<IScalarTelemetry>()
                           .As<ITickable>();
                    break;
                case LifetimeScopeKind.Platform:
                    builder.Register<BaseScalarService>(Lifetime.Singleton)
                           .WithParameter(_owner)
                           .As<IBaseScalarService>()
                           .As<IPlatformScalarService>()
                           .As<IScalarTelemetry>()
                           .As<ITickable>();
                    break;
                case LifetimeScopeKind.Global:
                    builder.Register<BaseScalarService>(Lifetime.Singleton)
                           .WithParameter(_owner)
                           .As<IBaseScalarService>()
                           .As<IGlobalScalarService>()
                           .As<IScalarTelemetry>()
                           .As<ITickable>();
                    break;
                case LifetimeScopeKind.Scene:
                    builder.Register<BaseScalarService>(Lifetime.Singleton)
                           .WithParameter(_owner)
                           .As<IBaseScalarService>()
                           .As<ISceneScalarService>()
                           .As<IScalarTelemetry>()
                           .As<ITickable>();
                    break;
                case LifetimeScopeKind.Field:
                    builder.Register<BaseScalarService>(Lifetime.Singleton)
                           .WithParameter(_owner)
                           .As<IBaseScalarService>()
                           .As<IFieldScalarService>()
                           .As<IScalarTelemetry>()
                           .As<ITickable>();
                    break;
                case LifetimeScopeKind.Entity:
                    builder.Register<BaseScalarService>(Lifetime.Singleton)
                           .WithParameter(_owner)
                           .As<IBaseScalarService>()
                           .As<IEntityScalarService>()
                           .As<IScalarTelemetry>()
                           .As<ITickable>();
                    break;
                case LifetimeScopeKind.UI:
                    builder.Register<BaseScalarService>(Lifetime.Singleton)
                           .WithParameter(_owner)
                           .As<IBaseScalarService>()
                           .As<IUIScalarService>()
                           .As<IScalarTelemetry>()
                           .As<ITickable>();
                    break;
                case LifetimeScopeKind.UIElement:
                    builder.Register<BaseScalarService>(Lifetime.Singleton)
                           .WithParameter(_owner)
                           .As<IBaseScalarService>()
                           .As<IUIElementScalarService>()
                           .As<IScalarTelemetry>()
                           .As<ITickable>();
                    break;
                case LifetimeScopeKind.Runtime:
                    builder.Register<BaseScalarService>(Lifetime.Singleton)
                           .WithParameter(_owner)
                           .As<IBaseScalarService>()
                           .As<IRuntimeScalarService>()
                           .As<IScalarTelemetry>()
                           .As<ITickable>();
                    break;
            }
            ExtraInstallFeature(builder, owner);
        }
        protected virtual void ExtraInstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            // 継承先で追加の登録を行う場合にオーバーライドする
        }

        [Inject]
        protected void Construct(
            IBaseScalarService scalar,
            IScalarTelemetry telemetry)
        {
            if (enableDebugView && scalar != null && telemetry != null)
            {
                _debugView.Initialize(scalar, telemetry);
                _debugInitialized = true;
            }
        }

        void Start()
        {
            if (!enableDebugView || _debugInitialized)
                return;

            var resolver = _owner?.Resolver;
            if (resolver == null)
                return;

            if (resolver.TryResolve<IBaseScalarService>(out var scalar)
                && resolver.TryResolve<IScalarTelemetry>(out var telemetry))
            {
                _debugView.Initialize(scalar, telemetry);
                _debugInitialized = true;
            }
        }
    }
}
