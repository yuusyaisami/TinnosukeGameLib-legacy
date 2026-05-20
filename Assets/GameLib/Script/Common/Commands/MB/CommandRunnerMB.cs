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
    [DisallowMultipleComponent]
    public sealed class CommandRunnerMB : MonoBehaviour, IFeatureInstaller
    {
        [FoldoutGroup("Debug Viewer")]
        [LabelText("Monitor Channel Debug Viewer")]
        [SerializeField]
        MonitorChannelHubDebugViewer _monitorHubDebugViewer = new();

        [FoldoutGroup("Debug Viewer")]
        [LabelText("Shared LTS Channel Debug Viewer")]
        [SerializeField]
        SharedLTSChannelHubDebugViewer _sharedLtsChannelHubDebugViewer = new();

        [BoxGroup("Runner Vars")]
        [LabelText("Default Vars")]
        [Tooltip("Default variables inserted into command VarStore. Existing vars are not overwritten.")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        VarStorePayload _defaultVars = new();

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            // MonitorChannelHub は吁E��コープで共有�Eシングルトンとして登録
            builder.RegisterAsScopeMulti<IMonitorChannelHub, MonitorChannelHub>(RuntimeLifetime.Singleton)
                .WithParameter(owner)
                .As<IScopeTickHandler>()
                .As<IMonitorChannelHubTelemetry>();

            // DebugViewer にチE��メトリをバインチE
            builder.RegisterBuildCallback(container =>
            {
                if (_monitorHubDebugViewer != null && container.TryResolve<IMonitorChannelHubTelemetry>(out var telemetry))
                {
                    _monitorHubDebugViewer.Bind(telemetry);
                }

                if (_sharedLtsChannelHubDebugViewer != null && container.TryResolve<VNext.ISharedLTSChannelHubTelemetry>(out var sharedTelemetry))
                {
                    _sharedLtsChannelHubDebugViewer.Bind(sharedTelemetry);
                }
            });

            // Shared executors (available in all scopes that host a CommandRunner)
            builder.Register<VNext.TransformAnimationChannelExecutor>(RuntimeLifetime.Singleton)
                .As<VNext.ICommandExecutor>();

            builder.Register<VNext.ParallaxChannelExecutor>(RuntimeLifetime.Singleton)
                .As<VNext.ICommandExecutor>();

            builder.Register<VNext.CommandListRuntimeMutationService>(RuntimeLifetime.Singleton)
                .As<VNext.ICommandListRuntimeMutationService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            // vNext core services (Project scope only)
            if (owner.Kind == LifetimeScopeKind.Project)
            {
                builder.Register<VNext.CommandKeyResolver>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandKeyResolver>()
                    .As<IScopeAcquireHandler>()
                    .As<IScopeReleaseHandler>();

                builder.Register<VNext.CommandCatalogService>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandCatalog>()
                    .As<IScopeAcquireHandler>()
                    .As<IScopeReleaseHandler>();

                builder.Register<VNext.UnityCommandResolveLogger>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandResolveLogger>();

                builder.Register<VNext.SharedLTSChannelHub>(RuntimeLifetime.Singleton)
                    .As<VNext.ISharedLTSChannelHub>()
                    .As<VNext.ISharedLTSChannelHubTelemetry>()
                    .As<IScopeReleaseHandler>();

                builder.Register<VNext.WithActorExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.WithActorDescendantRouterExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.WithPlayerExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.WithTargetChannelExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.CommandChannelExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.CommandChannelControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.CommandListChannelHubControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.CommandListChannelExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.CommandListChannelPlayerControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.SetContextSlotExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.FunctionExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.HostCallExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.CommandDebugExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.LifetimeScopeStateExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ScopeLifecycleConditionExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.MonitorChannelRuleControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.StatusEffectExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.SharedLTSChannelExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.WaitExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.BreakExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.CancelExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.AdvanceWaitExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.IfExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.TriggerExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SwitchExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ForExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SequenceExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.ActionBlockExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ForgetExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.DelayExecutorExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.PlayAudioExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.StopAudioExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.SetVelocityExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.AddForceExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SetChannelEnabledExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SetAllChannelsEnabledExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SetChannelInfluenceExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ResetAllVelocitiesExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.CreateMovementChannelExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.RemoveMovementChannelExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SetMovementModuleExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SetInputMovementExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.MoveToPointsExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.TeleportExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.TransformChannelRigidbody2DExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.TransformManagerMovementExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.TransformManagerRotateExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.TransformManagerScaleExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.HealthApplyDamageExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.HealthApplyHealExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.HealthControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.SelfDespawnExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SpawnParticleExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SpawnRuntimeTemplateExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SpawnRuntimeGridExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.RuntimeAllDeleteExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.StateMachineExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SetDirectionExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SetTimeScaleExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.TimerControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.UIControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.VisualBoundsReactiveHubControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ButtonChannelHubControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ButtonChannelPlayerControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SliderControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.DialogueChannelExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ConversationFlowExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ConversationInFlowExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.WorldPointerTargetControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.UserMoveRotateRuntimeControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.TargetChannelControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.AreaChannelControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.AutoSpawnChannelControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ShowTooltipExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.HideTooltipExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.TooltipChannelHubControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.TooltipChannelExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.TooltipChannelPlayerControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ShowToastExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.RunFlowExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.TextChannelExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.VelocityDrivenRotationExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.AnimationSpriteChannelExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.Light2DChannelHubControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.Light2DChannelPlayerControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.MeshChannelControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.MeshMaterialFxControlExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.PublishEventExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.WaitEventExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.VisualSetStateExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.VisualBroadcastExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<global::Game.Commands.VNext.WriteDataExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.WriteGridDataExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.WriteTableDataExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.WithTableElementsExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.WriteStatusEffectDataExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.LotteryExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();


                builder.Register<VNext.SetFootTransformOffsetZExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<global::Game.Commands.VNext.CameraPostProcessExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.CameraShakeExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.CameraZoomExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.SetStateAnimationProfileExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.SceneChangeExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();

                builder.Register<global::Game.Commands.VNext.BuildMapNodeExecutor>(RuntimeLifetime.Singleton)
                .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.MoveMapNodeExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.RunMapNodeCommandsExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.RefreshMapNodeStateExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.WriteMapNodePlayerStateExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.BuildRoomMapExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.ClearRoomMapExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.RemoveRoomMapRectExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.ApplyRoomMapVisualExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.GetRoomMapCenterExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.BuildUITraitListExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.RefreshUITraitListExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.SetUITraitListRangeExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.ClearUITraitListExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.AddTraitToHolderExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.RemoveTraitFromHolderExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.UseTraitFromHolderExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.ClearTraitFromHolderExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.BindTraitListChannelExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.RefreshTraitListChannelExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.SetTraitListChannelRangeExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.ClearTraitListChannelExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.BindGridObjectChannelExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.RefreshGridObjectChannelExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.ClearGridObjectChannelExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.ShowGridObjectChoiceAndWaitExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();

                builder.Register<global::Game.Commands.VNext.EquipTraitExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.WriteTraitDataExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Trait.TraitLotteryService>(RuntimeLifetime.Singleton)
                    .As<global::Game.Trait.ITraitLotteryService>();
                builder.Register<global::Game.Commands.VNext.TraitLotteryExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.PlaceTraitRuntimeExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.RuntimeTraitPresentationCommandMutationExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();

                builder.Register<VNext.BackgroundLayerExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<ChangeGameStateExecutor>(RuntimeLifetime.Singleton)
                    .As<ICommandExecutor>();



                // Collision commands: enable/disable collision on actor/targets
                builder.Register<global::Game.Commands.VNext.SetCollisionEnabledExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.SetUnityColliderExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.SetColliderSharedMaterialExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.SetColliderPhysicsMaterialValuesExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.SetGlobalPhysics2DExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.HitColliderRuleControlExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.WithHitColliderTargetsExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.UnityRoomSendScoreExecutor>(RuntimeLifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();

                // Save commands: SaveProfile, LoadProfile, ClearProfile, ProfileChange, DeleteAllSaveData
                builder.Register<VNext.SaveProfileCommandExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.LoadProfileCommandExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ClearProfileCommandExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ProfileChangeCommandExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.DeleteAllSaveDataCommandExecutor>(RuntimeLifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
            }

            builder.Register<VNext.CommandExecutorCatalog>(RuntimeLifetime.Singleton)
                .As<VNext.ICommandExecutorCatalog>();

            builder.RegisterInstance<VNext.ICommandPayloadFieldReaderProvider>(new VNext.CommandPayloadFieldReaderProvider());
            builder.RegisterInstance<VNext.ICommandPayloadReferenceValidator>(VNext.MissingCommandPayloadReferenceValidator.Instance);

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
                        .WithParameter(_defaultVars);
                    break;
                case LifetimeScopeKind.Platform:
                    builder.Register<VNext.CommandRunner>(RuntimeLifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IPlatformCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(_defaultVars);
                    break;
                case LifetimeScopeKind.Global:
                    builder.Register<VNext.CommandRunner>(RuntimeLifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IGlobalCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(_defaultVars);
                    break;
                case LifetimeScopeKind.Scene:
                    builder.Register<VNext.CommandRunner>(RuntimeLifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.ISceneCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(_defaultVars);
                    break;
                case LifetimeScopeKind.Field:
                    builder.Register<VNext.CommandRunner>(RuntimeLifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IFieldCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(_defaultVars);
                    break;
                case LifetimeScopeKind.Entity:
                    builder.Register<VNext.CommandRunner>(RuntimeLifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IEntityCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(_defaultVars);
                    break;
                case LifetimeScopeKind.UI:
                    builder.Register<VNext.CommandRunner>(RuntimeLifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IUICommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(_defaultVars);
                    break;
                case LifetimeScopeKind.UIElement:
                    builder.Register<VNext.UIElementCommandRunner>(RuntimeLifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IUIElementCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(_defaultVars);
                    break;
            }
        }
    }
}
