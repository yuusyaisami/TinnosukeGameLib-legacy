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
        [SerializeField]
        MonitorChannelHubDebugViewer _monitorHubDebugViewer = new();

        [BoxGroup("Runner Vars")]
        [LabelText("Default Vars")]
        [Tooltip("Default variables inserted into command VarStore. Existing vars are not overwritten.")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        VarStorePayload _defaultVars = new();

        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            // MonitorChannelHub は各スコープで共有のシングルトンとして登録
            builder.RegisterAsScopeMulti<IMonitorChannelHub, MonitorChannelHub>(Lifetime.Singleton)
                .WithParameter(owner)
                .As<ITickable>()
                .As<IMonitorChannelHubTelemetry>();

            // DebugViewer にテレメトリをバインド
            builder.RegisterBuildCallback(container =>
            {
                if (_monitorHubDebugViewer != null && container.TryResolve<IMonitorChannelHubTelemetry>(out var telemetry))
                {
                    _monitorHubDebugViewer.Bind(telemetry);
                }
            });

            // Shared executors (available in all scopes that host a CommandRunner)
            builder.Register<VNext.TransformAnimationChannelExecutor>(Lifetime.Singleton)
                .As<VNext.ICommandExecutor>();

            builder.Register<VNext.ParallaxChannelExecutor>(Lifetime.Singleton)
                .As<VNext.ICommandExecutor>();

            builder.Register<VNext.CommandListRuntimeMutationService>(Lifetime.Singleton)
                .As<VNext.ICommandListRuntimeMutationService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            // vNext core services (Project scope only)
            if (owner.Kind == LifetimeScopeKind.Project)
            {
                builder.Register<VNext.CommandKeyResolver>(Lifetime.Singleton)
                    .As<VNext.ICommandKeyResolver>()
                    .As<IScopeAcquireHandler>()
                    .As<IScopeReleaseHandler>();

                builder.Register<VNext.CommandCatalogService>(Lifetime.Singleton)
                    .As<VNext.ICommandCatalog>()
                    .As<IScopeAcquireHandler>()
                    .As<IScopeReleaseHandler>();

                builder.Register<VNext.UnityCommandResolveLogger>(Lifetime.Singleton)
                    .As<VNext.ICommandResolveLogger>();

                builder.Register<VNext.SharedLTSChannelHub>(Lifetime.Singleton)
                    .As<VNext.ISharedLTSChannelHub>()
                    .As<IScopeReleaseHandler>();

                builder.Register<VNext.WithActorExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.WithActorDescendantRouterExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.WithPlayerExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.CommandChannelExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.CommandChannelControlExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.SetContextSlotExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.FunctionExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.HostCallExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.CommandDebugExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.LifetimeScopeStateExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ScopeLifecycleConditionExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.MonitorChannelRuleControlExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.StatusEffectExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.SharedLTSChannelExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.WaitExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.AdvanceWaitExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.IfExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SwitchExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ForExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SequenceExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.ActionBlockExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ForgetExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.DelayExecutorExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.PlayAudioExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.StopAudioExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.SetVelocityExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.AddForceExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SetChannelEnabledExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SetAllChannelsEnabledExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SetChannelInfluenceExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ResetAllVelocitiesExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.CreateMovementChannelExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.RemoveMovementChannelExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SetMovementModuleExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SetInputMovementExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.MoveToPointsExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.TeleportExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.TransformControllerRigidbody2DExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.HealthApplyDamageExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.HealthApplyHealExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.HealthControlExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.SelfDespawnExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SpawnParticleExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SpawnRuntimeTemplateExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SpawnRuntimeGridExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.RuntimeAllDeleteExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.StateMachineExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SetDirectionExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.SetTimeScaleExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.TimerControlExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.UIControlExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.UIButtonControlExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.WorldSliderControlExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.WorldPointerTargetControlExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.TargetChannelControlExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ShowTooltipExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.HideTooltipExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ShowToastExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.RunFlowExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.TextChannelExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.VelocityDrivenRotationExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.AnimationSpriteChannelExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.MeshChannelControlExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.MeshMaterialFxControlExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.PublishEventExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.WaitEventExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.UIDialogChannelExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<VNext.VisualSetStateExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.VisualBroadcastExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<global::Game.Commands.VNext.WriteDataExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.WriteStatusEffectDataExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.LotteryExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();


                builder.Register<VNext.SetFootTransformOffsetZExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<global::Game.Commands.VNext.CameraPostProcessExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.CameraShakeExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.CameraZoomExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.SetStateAnimationProfileExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.SceneChangeExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();

                builder.Register<global::Game.Commands.VNext.BuildMapNodeExecutor>(Lifetime.Singleton)
                .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.MoveMapNodeExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.RunMapNodeCommandsExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.RefreshMapNodeStateExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.WriteMapNodePlayerStateExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.BuildRoomMapExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.ClearRoomMapExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.RemoveRoomMapRectExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.ApplyRoomMapVisualExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.GetRoomMapCenterExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.BuildUITraitListExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.RefreshUITraitListExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.SetUITraitListRangeExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.ClearUITraitListExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.AddTraitToHolderExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.RemoveTraitFromHolderExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.UseTraitFromHolderExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();

                builder.Register<global::Game.Commands.VNext.EquipTraitExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.WriteTraitDataExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Trait.TraitLotteryService>(Lifetime.Singleton)
                    .As<global::Game.Trait.ITraitLotteryService>();
                builder.Register<global::Game.Commands.VNext.TraitLotteryExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.PlaceTraitRuntimeExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.RuntimeTraitPresentationCommandMutationExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();

                builder.Register<VNext.BackgroundLayerExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();

                builder.Register<ChangeGameStateExecutor>(Lifetime.Singleton)
                    .As<ICommandExecutor>();



                // Collision commands: enable/disable collision on actor/targets
                builder.Register<global::Game.Commands.VNext.SetCollisionEnabledExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.SetUnityColliderExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.SetColliderSharedMaterialExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.SetColliderPhysicsMaterialValuesExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.SetGlobalPhysics2DExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.HitColliderRuleControlExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.WithHitColliderTargetsExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();
                builder.Register<global::Game.Commands.VNext.UnityRoomSendScoreExecutor>(Lifetime.Singleton)
                    .As<global::Game.Commands.VNext.ICommandExecutor>();

                // Save commands: SaveProfile, LoadProfile, ClearProfile, ProfileChange, DeleteAllSaveData
                builder.Register<VNext.SaveProfileCommandExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.LoadProfileCommandExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ClearProfileCommandExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.ProfileChangeCommandExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
                builder.Register<VNext.DeleteAllSaveDataCommandExecutor>(Lifetime.Singleton)
                    .As<VNext.ICommandExecutor>();
            }

            builder.Register<VNext.CommandExecutorRegistry>(Lifetime.Singleton);

            switch (owner.Kind)
            {
                case LifetimeScopeKind.Project:
                    builder.Register<VNext.CommandRunner>(Lifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IProjectCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(_defaultVars);
                    break;
                case LifetimeScopeKind.Platform:
                    builder.Register<VNext.CommandRunner>(Lifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IPlatformCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(_defaultVars);
                    break;
                case LifetimeScopeKind.Global:
                    builder.Register<VNext.CommandRunner>(Lifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IGlobalCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(_defaultVars);
                    break;
                case LifetimeScopeKind.Scene:
                    builder.Register<VNext.CommandRunner>(Lifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.ISceneCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(_defaultVars);
                    break;
                case LifetimeScopeKind.Field:
                    builder.Register<VNext.CommandRunner>(Lifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IFieldCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(_defaultVars);
                    break;
                case LifetimeScopeKind.Entity:
                    builder.Register<VNext.CommandRunner>(Lifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IEntityCommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(_defaultVars);
                    break;
                case LifetimeScopeKind.UI:
                    builder.Register<VNext.CommandRunner>(Lifetime.Singleton)
                        .As<VNext.ICommandRunner>()
                        .As<VNext.ICommandRunnerActivity>()
                        .As<VNext.IUICommandRunner>()
                        .As<IScopeAcquireHandler>()
                        .As<IScopeReleaseHandler>()
                        .WithParameter(owner)
                        .WithParameter(_defaultVars);
                    break;
                case LifetimeScopeKind.UIElement:
                    builder.Register<VNext.UIElementCommandRunner>(Lifetime.Singleton)
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
