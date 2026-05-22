#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Game;
using Game.Common;
using Sirenix.OdinInspector;
using VNext = Game.Commands.VNext;
using Game.Commands.VNext;
namespace Game.Commands
{
    public abstract class CommandRunnerAuthoring : MonoBehaviour
    {
        [FoldoutGroup("Debug Viewer")]
        [LabelText("Monitor Channel Debug Viewer")]
        [SerializeField]
        protected MonitorChannelHubDebugViewer _monitorHubDebugViewer = new();

        [FoldoutGroup("Debug Viewer")]
        [LabelText("Shared LTS Channel Debug Viewer")]
        [SerializeField]
        protected SharedLTSChannelHubDebugViewer _sharedLtsChannelHubDebugViewer = new();

        [BoxGroup("Runner Vars")]
        [LabelText("Default Vars")]
        [Tooltip("Default variables inserted into command VarStore. Existing vars are not overwritten.")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        protected VarStorePayload _defaultVars = new();

        internal void ValidateOrThrow()
        {
            if (_defaultVars == null)
                throw new InvalidOperationException("CommandRunner authoring requires default vars when command runner configuration is present.");
        }

        internal RuntimeAuthoringState CaptureRuntimeAuthoringState()
        {
            ValidateOrThrow();
            return new RuntimeAuthoringState(_monitorHubDebugViewer, _sharedLtsChannelHubDebugViewer, _defaultVars);
        }

        internal sealed class RuntimeAuthoringState
        {
            public static RuntimeAuthoringState Empty { get; } = new RuntimeAuthoringState(null, null, new VarStorePayload());

            public RuntimeAuthoringState(
                MonitorChannelHubDebugViewer? monitorHubDebugViewer,
                SharedLTSChannelHubDebugViewer? sharedLtsChannelHubDebugViewer,
                VarStorePayload defaultVars)
            {
                MonitorHubDebugViewer = monitorHubDebugViewer;
                SharedLtsChannelHubDebugViewer = sharedLtsChannelHubDebugViewer;
                DefaultVars = defaultVars ?? throw new ArgumentNullException(nameof(defaultVars));
            }

            public MonitorChannelHubDebugViewer? MonitorHubDebugViewer { get; }

            public SharedLTSChannelHubDebugViewer? SharedLtsChannelHubDebugViewer { get; }

            public VarStorePayload DefaultVars { get; }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            try
            {
                ValidateOrThrow();
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message, this);
            }
        }
#endif
    }

    [DisallowMultipleComponent]
    public sealed class CommandRunnerMB : CommandRunnerAuthoring
    {

        public void InstallCommandRuntime(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            CommandRuntimeInstaller.Install(builder, owner, this);
        }
    }

    internal static class CommandRuntimeInstaller
    {
        public static void Install(IRuntimeContainerBuilder builder, IScopeNode owner, CommandRunnerAuthoring? authoring = null)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            CommandRunnerAuthoring.RuntimeAuthoringState authoringState = authoring?.CaptureRuntimeAuthoringState()
                ?? CommandRunnerAuthoring.RuntimeAuthoringState.Empty;

            VNext.VerifiedCommandRuntimeBridge.TryGetSession(out VNext.IVerifiedCommandRuntimeSession? verifiedCommandSession);
            if (verifiedCommandSession == null)
            {
                throw new InvalidOperationException(
                    "Wave C accepted path requires verified command runtime authority before command runtime can be configured. Legacy catalog loading and executor discovery are no longer allowed.");
            }

            builder.RegisterAsScopeMulti<IMonitorChannelHub, MonitorChannelHub>(RuntimeLifetime.Singleton)
                .WithParameter(owner)
                .As<IScopeTickHandler>()
                .As<IMonitorChannelHubTelemetry>();

            builder.RegisterBuildCallback(container =>
            {
                if (authoringState.MonitorHubDebugViewer != null && container.TryResolve<IMonitorChannelHubTelemetry>(out var telemetry))
                {
                    authoringState.MonitorHubDebugViewer.Bind(telemetry);
                }

                if (authoringState.SharedLtsChannelHubDebugViewer != null && container.TryResolve<VNext.ISharedLTSChannelHubTelemetry>(out var sharedTelemetry))
                {
                    authoringState.SharedLtsChannelHubDebugViewer.Bind(sharedTelemetry);
                }
            });

            builder.RegisterInstance<VNext.ICommandKeyResolver>(verifiedCommandSession.KeyResolver);
            builder.RegisterInstance<VNext.ICommandCatalog>(verifiedCommandSession.Catalog);

            builder.Register<VNext.UnityCommandResolveLogger>(RuntimeLifetime.Singleton)
                .As<VNext.ICommandResolveLogger>();

            List<VNext.ExplicitCommandExecutorBinding> explicitExecutorBindings = new(160);

            RegisterExplicitCommandExecutor<VNext.TransformAnimationChannelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.ParallaxChannelExecutor>(builder, explicitExecutorBindings);

            builder.Register<VNext.CommandListRuntimeMutationService>(RuntimeLifetime.Singleton)
                .As<VNext.ICommandListRuntimeMutationService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            if (owner.Kind == LifetimeScopeKind.Project)
            {
                builder.Register<VNext.SharedLTSChannelHub>(RuntimeLifetime.Singleton)
                    .As<VNext.ISharedLTSChannelHub>()
                    .As<VNext.ISharedLTSChannelHubTelemetry>()
                    .As<IScopeReleaseHandler>();

                RegisterExplicitProjectExecutors(builder, explicitExecutorBindings);
            }

            builder.Register<VNext.ICommandExecutorCatalog>(
                resolver => ResolveVerifiedExecutorCatalog(verifiedCommandSession, owner, resolver, explicitExecutorBindings),
                RuntimeLifetime.Singleton);

            builder.RegisterInstance<VNext.ICommandPayloadFieldReaderProvider>(new VNext.CommandPayloadFieldReaderProvider());
            builder.RegisterInstance<VNext.ICommandPayloadReferenceValidator>(verifiedCommandSession.PayloadReferenceValidator);

            switch (owner.Kind)
            {
                case LifetimeScopeKind.Project:
                    builder.Register<VNext.CommandRunner>(RuntimeLifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IProjectCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(authoringState.DefaultVars);
                    break;
                case LifetimeScopeKind.Platform:
                    builder.Register<VNext.CommandRunner>(RuntimeLifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IPlatformCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(authoringState.DefaultVars);
                    break;
                case LifetimeScopeKind.Global:
                    builder.Register<VNext.CommandRunner>(RuntimeLifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IGlobalCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(authoringState.DefaultVars);
                    break;
                case LifetimeScopeKind.Scene:
                    builder.Register<VNext.CommandRunner>(RuntimeLifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.ISceneCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(authoringState.DefaultVars);
                    break;
                case LifetimeScopeKind.Field:
                    builder.Register<VNext.CommandRunner>(RuntimeLifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IFieldCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(authoringState.DefaultVars);
                    break;
                case LifetimeScopeKind.Entity:
                    builder.Register<VNext.CommandRunner>(RuntimeLifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IEntityCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(authoringState.DefaultVars);
                    break;
                case LifetimeScopeKind.UI:
                    builder.Register<VNext.CommandRunner>(RuntimeLifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IUICommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(authoringState.DefaultVars);
                    break;
                case LifetimeScopeKind.UIElement:
                    builder.Register<VNext.UIElementCommandRunner>(RuntimeLifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IUIElementCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(authoringState.DefaultVars);
                    break;
            }
        }

        static void RegisterExplicitProjectExecutors(IRuntimeContainerBuilder builder, List<VNext.ExplicitCommandExecutorBinding> explicitExecutorBindings)
        {
            RegisterExplicitCommandExecutor<VNext.WithActorExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.WithActorDescendantRouterExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.WithPlayerExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.WithTargetChannelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.CommandChannelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.CommandChannelControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.CommandListChannelHubControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.CommandListChannelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.CommandListChannelPlayerControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SetContextSlotExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.FunctionExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.HostCallExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.CommandDebugExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.LifetimeScopeStateExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.ScopeLifecycleConditionExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.MonitorChannelRuleControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.StatusEffectExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.SharedLTSChannelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.WaitExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.BreakExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.CancelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.AdvanceWaitExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.IfExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.TriggerExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SwitchExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.ForExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SequenceExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.ActionBlockExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.ForgetExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.DelayExecutorExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.PlayAudioExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.StopAudioExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SetVelocityExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.AddForceExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SetChannelEnabledExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SetAllChannelsEnabledExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SetChannelInfluenceExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.ResetAllVelocitiesExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.CreateMovementChannelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.RemoveMovementChannelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SetMovementModuleExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SetInputMovementExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.MoveToPointsExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.TeleportExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.TransformChannelRigidbody2DExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.TransformManagerMovementExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.TransformManagerRotateExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.TransformManagerScaleExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.HealthApplyDamageExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.HealthApplyHealExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.HealthControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SelfDespawnExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SpawnParticleExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SpawnRuntimeTemplateExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SpawnRuntimeGridExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.RuntimeAllDeleteExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.StateMachineExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SetDirectionExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SetTimeScaleExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.TimerControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.UIControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.VisualBoundsReactiveHubControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.ButtonChannelHubControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.ButtonChannelPlayerControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SliderControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.DialogueChannelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.ConversationFlowExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.ConversationInFlowExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.WorldPointerTargetControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.UserMoveRotateRuntimeControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.TargetChannelControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.AreaChannelControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.AutoSpawnChannelControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.ShowTooltipExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.HideTooltipExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.TooltipChannelHubControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.TooltipChannelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.TooltipChannelPlayerControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.ShowToastExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.RunFlowExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.TextChannelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.VelocityDrivenRotationExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.AnimationSpriteChannelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.Light2DChannelHubControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.Light2DChannelPlayerControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.MeshChannelControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.MeshMaterialFxControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.PublishEventExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.WaitEventExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.VisualSetStateExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.VisualBroadcastExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.WriteDataExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.WriteGridDataExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.WriteTableDataExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.WithTableElementsExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.WriteStatusEffectDataExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.LotteryExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SetFootTransformOffsetZExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.CameraPostProcessExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.CameraShakeExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.CameraZoomExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.SetStateAnimationProfileExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.SceneChangeExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.BuildMapNodeExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.MoveMapNodeExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.RunMapNodeCommandsExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.RefreshMapNodeStateExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.WriteMapNodePlayerStateExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.BuildRoomMapExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.ClearRoomMapExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.RemoveRoomMapRectExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.ApplyRoomMapVisualExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.GetRoomMapCenterExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.BuildUITraitListExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.RefreshUITraitListExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.SetUITraitListRangeExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.ClearUITraitListExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.AddTraitToHolderExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.RemoveTraitFromHolderExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.UseTraitFromHolderExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.ClearTraitFromHolderExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.BindTraitListChannelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.RefreshTraitListChannelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.SetTraitListChannelRangeExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.ClearTraitListChannelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.BindGridObjectChannelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.RefreshGridObjectChannelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.ClearGridObjectChannelExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.ShowGridObjectChoiceAndWaitExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.EquipTraitExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.WriteTraitDataExecutor>(builder, explicitExecutorBindings);
            builder.Register<global::Game.Trait.TraitLotteryService>(RuntimeLifetime.Singleton)
                .As<global::Game.Trait.ITraitLotteryService>();
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.TraitLotteryExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.PlaceTraitRuntimeExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.RuntimeTraitPresentationCommandMutationExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.BackgroundLayerExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<ChangeGameStateExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.SetCollisionEnabledExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.SetUnityColliderExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.SetColliderSharedMaterialExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.SetColliderPhysicsMaterialValuesExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.SetGlobalPhysics2DExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.HitColliderRuleControlExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.WithHitColliderTargetsExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<global::Game.Commands.VNext.UnityRoomSendScoreExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.SaveProfileCommandExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.LoadProfileCommandExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.ClearProfileCommandExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.ProfileChangeCommandExecutor>(builder, explicitExecutorBindings);
            RegisterExplicitCommandExecutor<VNext.DeleteAllSaveDataCommandExecutor>(builder, explicitExecutorBindings);
        }

        static void RegisterExplicitCommandExecutor<TExecutor>(IRuntimeContainerBuilder builder, List<VNext.ExplicitCommandExecutorBinding> bindings)
            where TExecutor : class, ICommandExecutor
        {
            builder.Register<TExecutor>(RuntimeLifetime.Singleton)
                .AsSelf();
            bindings.Add(VNext.ExplicitCommandExecutorBinding.For<TExecutor>());
        }

        static VNext.ICommandExecutorCatalog ResolveVerifiedExecutorCatalog(
            VNext.IVerifiedCommandRuntimeSession verifiedCommandSession,
            IScopeNode owner,
            IRuntimeResolver resolver,
            IReadOnlyList<VNext.ExplicitCommandExecutorBinding> explicitExecutorBindings)
        {
            if (verifiedCommandSession == null)
                throw new ArgumentNullException(nameof(verifiedCommandSession));
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));
            if (explicitExecutorBindings == null)
                throw new ArgumentNullException(nameof(explicitExecutorBindings));

            if (owner.Kind == LifetimeScopeKind.Project)
                return verifiedCommandSession.CreateExecutorCatalog(resolver, explicitExecutorBindings);

            if (TryResolveVerifiedProjectExecutorCatalog(owner, out VNext.ICommandExecutorCatalog? catalog) && catalog != null)
                return catalog;

            throw new InvalidOperationException(
                $"Wave C accepted path requires {owner.Kind} runners to reuse the verified Project ICommandExecutorCatalog authority, but no verified Project catalog was available in the ancestor chain.");
        }

        static bool TryResolveVerifiedProjectExecutorCatalog(IScopeNode owner, out VNext.ICommandExecutorCatalog? catalog)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            IScopeNode? current = owner.Parent;
            while (current != null)
            {
                if (current.Kind == LifetimeScopeKind.Project)
                {
                    IRuntimeResolver? resolver = current.Resolver;
                    if (resolver != null &&
                        resolver.TryResolve<VNext.ICommandExecutorCatalog>(out VNext.ICommandExecutorCatalog resolved) &&
                        resolved != null)
                    {
                        catalog = resolved;
                        return true;
                    }
                }

                current = current.Parent;
            }

            catalog = null;
            return false;
        }
    }
}
