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

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            _owner = owner;

            // No baseline/init pipeline here. If you need initial values, use ProfileRegistry.
            builder.Register<NullScalarRuntimeConfigProvider>(RuntimeLifetime.Singleton)
                .As<IScalarRuntimeConfigProvider>()
                .As<IScalarBaseline>();

            if (owner.Kind == LifetimeScopeKind.Project)
            {
                builder.Register<ScalarInheritedRuntimeTopology>(RuntimeLifetime.Singleton)
                    .As<IScalarInheritedRuntimeTopology>();
            }

            builder.RegisterComponent(this);
            switch (owner.Kind)
            {
                case LifetimeScopeKind.Project:
                    builder.Register<ScalarRuntimeService>(resolver =>
                    {
                        var configProvider = resolver.TryResolve<IScalarRuntimeConfigProvider>(out var resolvedConfigProvider) ? resolvedConfigProvider : null;
                        var inheritedRuntimeTopology = resolver.TryResolve<IScalarInheritedRuntimeTopology>(out var resolvedTopology) ? resolvedTopology : null;
                        return new ScalarRuntimeService(_owner, configProvider, inheritedRuntimeTopology);
                    }, RuntimeLifetime.Singleton)
                           .As<IBaseScalarService>()
                          .As<IScalarRuntimeShell>()
                           .As<IProjectScalarService>()
                           .As<IScalarTelemetry>()
                           .As<IScopeAcquireHandler>()
                           .As<IScopeReleaseHandler>()
                           .As<IScopeTickHandler>();
                    break;
                case LifetimeScopeKind.Platform:
                    builder.Register<ScalarRuntimeService>(resolver =>
                    {
                        var configProvider = resolver.TryResolve<IScalarRuntimeConfigProvider>(out var resolvedConfigProvider) ? resolvedConfigProvider : null;
                        var inheritedRuntimeTopology = resolver.TryResolve<IScalarInheritedRuntimeTopology>(out var resolvedTopology) ? resolvedTopology : null;
                        return new ScalarRuntimeService(_owner, configProvider, inheritedRuntimeTopology);
                    }, RuntimeLifetime.Singleton)
                           .As<IBaseScalarService>()
                          .As<IScalarRuntimeShell>()
                           .As<IPlatformScalarService>()
                           .As<IScalarTelemetry>()
                           .As<IScopeAcquireHandler>()
                           .As<IScopeReleaseHandler>()
                           .As<IScopeTickHandler>();
                    break;
                case LifetimeScopeKind.Global:
                    builder.Register<ScalarRuntimeService>(resolver =>
                    {
                        var configProvider = resolver.TryResolve<IScalarRuntimeConfigProvider>(out var resolvedConfigProvider) ? resolvedConfigProvider : null;
                        var inheritedRuntimeTopology = resolver.TryResolve<IScalarInheritedRuntimeTopology>(out var resolvedTopology) ? resolvedTopology : null;
                        return new ScalarRuntimeService(_owner, configProvider, inheritedRuntimeTopology);
                    }, RuntimeLifetime.Singleton)
                           .As<IBaseScalarService>()
                          .As<IScalarRuntimeShell>()
                           .As<IGlobalScalarService>()
                           .As<IScalarTelemetry>()
                           .As<IScopeAcquireHandler>()
                           .As<IScopeReleaseHandler>()
                           .As<IScopeTickHandler>();
                    break;
                case LifetimeScopeKind.Scene:
                    builder.Register<ScalarRuntimeService>(resolver =>
                    {
                        var configProvider = resolver.TryResolve<IScalarRuntimeConfigProvider>(out var resolvedConfigProvider) ? resolvedConfigProvider : null;
                        var inheritedRuntimeTopology = resolver.TryResolve<IScalarInheritedRuntimeTopology>(out var resolvedTopology) ? resolvedTopology : null;
                        return new ScalarRuntimeService(_owner, configProvider, inheritedRuntimeTopology);
                    }, RuntimeLifetime.Singleton)
                           .As<IBaseScalarService>()
                          .As<IScalarRuntimeShell>()
                           .As<ISceneScalarService>()
                           .As<IScalarTelemetry>()
                           .As<IScopeAcquireHandler>()
                           .As<IScopeReleaseHandler>()
                           .As<IScopeTickHandler>();
                    break;
                case LifetimeScopeKind.Field:
                    builder.Register<ScalarRuntimeService>(resolver =>
                    {
                        var configProvider = resolver.TryResolve<IScalarRuntimeConfigProvider>(out var resolvedConfigProvider) ? resolvedConfigProvider : null;
                        var inheritedRuntimeTopology = resolver.TryResolve<IScalarInheritedRuntimeTopology>(out var resolvedTopology) ? resolvedTopology : null;
                        return new ScalarRuntimeService(_owner, configProvider, inheritedRuntimeTopology);
                    }, RuntimeLifetime.Singleton)
                           .As<IBaseScalarService>()
                          .As<IScalarRuntimeShell>()
                           .As<IFieldScalarService>()
                           .As<IScalarTelemetry>()
                           .As<IScopeAcquireHandler>()
                           .As<IScopeReleaseHandler>()
                           .As<IScopeTickHandler>();
                    break;
                case LifetimeScopeKind.Entity:
                    builder.Register<ScalarRuntimeService>(resolver =>
                    {
                        var configProvider = resolver.TryResolve<IScalarRuntimeConfigProvider>(out var resolvedConfigProvider) ? resolvedConfigProvider : null;
                        var inheritedRuntimeTopology = resolver.TryResolve<IScalarInheritedRuntimeTopology>(out var resolvedTopology) ? resolvedTopology : null;
                        return new ScalarRuntimeService(_owner, configProvider, inheritedRuntimeTopology);
                    }, RuntimeLifetime.Singleton)
                           .As<IBaseScalarService>()
                          .As<IScalarRuntimeShell>()
                           .As<IEntityScalarService>()
                           .As<IScalarTelemetry>()
                           .As<IScopeAcquireHandler>()
                           .As<IScopeReleaseHandler>()
                           .As<IScopeTickHandler>();
                    break;
                case LifetimeScopeKind.UI:
                    builder.Register<ScalarRuntimeService>(resolver =>
                    {
                        var configProvider = resolver.TryResolve<IScalarRuntimeConfigProvider>(out var resolvedConfigProvider) ? resolvedConfigProvider : null;
                        var inheritedRuntimeTopology = resolver.TryResolve<IScalarInheritedRuntimeTopology>(out var resolvedTopology) ? resolvedTopology : null;
                        return new ScalarRuntimeService(_owner, configProvider, inheritedRuntimeTopology);
                    }, RuntimeLifetime.Singleton)
                           .As<IBaseScalarService>()
                          .As<IScalarRuntimeShell>()
                           .As<IUIScalarService>()
                           .As<IScalarTelemetry>()
                           .As<IScopeAcquireHandler>()
                           .As<IScopeReleaseHandler>()
                           .As<IScopeTickHandler>();
                    break;
                case LifetimeScopeKind.UIElement:
                    builder.Register<ScalarRuntimeService>(resolver =>
                    {
                        var configProvider = resolver.TryResolve<IScalarRuntimeConfigProvider>(out var resolvedConfigProvider) ? resolvedConfigProvider : null;
                        var inheritedRuntimeTopology = resolver.TryResolve<IScalarInheritedRuntimeTopology>(out var resolvedTopology) ? resolvedTopology : null;
                        return new ScalarRuntimeService(_owner, configProvider, inheritedRuntimeTopology);
                    }, RuntimeLifetime.Singleton)
                           .As<IBaseScalarService>()
                          .As<IScalarRuntimeShell>()
                           .As<IUIElementScalarService>()
                           .As<IScalarTelemetry>()
                           .As<IScopeAcquireHandler>()
                           .As<IScopeReleaseHandler>()
                           .As<IScopeTickHandler>();
                    break;
                case LifetimeScopeKind.Runtime:
                    builder.Register<ScalarRuntimeService>(resolver =>
                    {
                        var configProvider = resolver.TryResolve<IScalarRuntimeConfigProvider>(out var resolvedConfigProvider) ? resolvedConfigProvider : null;
                        var inheritedRuntimeTopology = resolver.TryResolve<IScalarInheritedRuntimeTopology>(out var resolvedTopology) ? resolvedTopology : null;
                        return new ScalarRuntimeService(_owner, configProvider, inheritedRuntimeTopology);
                    }, RuntimeLifetime.Singleton)
                           .As<IBaseScalarService>()
                          .As<IScalarRuntimeShell>()
                           .As<IRuntimeScalarService>()
                           .As<IScalarTelemetry>()
                           .As<IScopeAcquireHandler>()
                           .As<IScopeReleaseHandler>()
                           .As<IScopeTickHandler>();
                    break;
            }
            ExtraInstallFeature(builder, owner);
        }
        protected virtual void ExtraInstallFeature(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            // 継承先で追加の登録を行う場合にオーバ�EライドすめE
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
