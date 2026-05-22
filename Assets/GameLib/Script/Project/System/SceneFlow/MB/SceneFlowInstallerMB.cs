// Assets/Game/Script/Flow/SceneFlowInstallerMB.cs
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using Game.Flow;
using Game.Loading;
using Game.Scene;
using Game.Common;
using Game.Commands.VNext;
using Game.Vars.Generated;

namespace Game.Project
{
    public interface ILoadingScreenConfig
    {

        float CommandLeadTimeBeforeSceneChangeSeconds { get; }
        LoadingScreenMB LoadingScenePrefab { get; }
        CommandListData ShowCommands { get; }
        CommandListData HideCommands { get; }
        CommandListData ProgressCommands { get; }
        VarKeyRef MessageVar { get; }
        VarKeyRef ProgressVar { get; }
    }

    public abstract class SceneFlowAuthoring : MonoBehaviour
    {
        [Header("Loading Screen")]
        [Tooltip("Inspector setting.")]
        [SerializeField] protected bool useLoadingScreen = true;

        [BoxGroup("Scene"), LabelText("Scene Prefab"), ShowIf(nameof(useLoadingScreen))]
        [SerializeField] protected LoadingScreenMB loadingScenePrefab;

        [BoxGroup("Commands"), LabelText("Delay"), ShowIf(nameof(useLoadingScreen))]
        [SerializeField, Min(0f)] protected float commandLeadTimeBeforeSceneChangeSeconds = 0f;

        [BoxGroup("Commands"), LabelText("Show"), ShowIf(nameof(useLoadingScreen))]
        [SerializeField] protected CommandListData showCommands = new();

        [BoxGroup("Commands"), LabelText("Progress"), ShowIf(nameof(useLoadingScreen))]
        [SerializeField] protected CommandListData progressCommands = new();

        [BoxGroup("Commands"), LabelText("Hide"), ShowIf(nameof(useLoadingScreen))]
        [SerializeField] protected CommandListData hideCommands = new();

        [BoxGroup("Context Vars"), LabelText("Message Var"), ShowIf(nameof(useLoadingScreen))]
        [SerializeField] protected VarKeyRef messageVar;

        [BoxGroup("Context Vars"), LabelText("Progress Var"), ShowIf(nameof(useLoadingScreen))]
        [SerializeField] protected VarKeyRef progressVar;

        internal void ValidateOrThrow()
        {
            if (!useLoadingScreen)
                return;

            if (loadingScenePrefab == null)
                throw new InvalidOperationException("Scene flow authoring requires a loading scene prefab when loading screen support is enabled.");
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

        public float CommandLeadTimeBeforeSceneChangeSeconds => commandLeadTimeBeforeSceneChangeSeconds;
        public LoadingScreenMB LoadingScenePrefab => loadingScenePrefab;
        public CommandListData ShowCommands => showCommands ?? new CommandListData();
        public CommandListData HideCommands => hideCommands ?? new CommandListData();
        public CommandListData ProgressCommands => progressCommands ?? new CommandListData();
        public VarKeyRef MessageVar => messageVar;
        public VarKeyRef ProgressVar => progressVar;
    }
    /// <summary>
    /// ProjectLifetimeScope 縺ｨ蜷後§ GameObject 縺ｫ繧｢繧ｿ繝・メ縺励※縲・
    /// 繧ｷ繝ｼ繝ｳ驕ｷ遘ｻ繧ｵ繝ｼ繝薙せ縺ｨ繝ｭ繝ｼ繝・ぅ繝ｳ繧ｰ繧ｵ繝ｼ繝薙せ繧堤匳骭ｲ縺吶ｋ繧､繝ｳ繧ｹ繝医・繝ｩ縲・
    /// </summary>
    public sealed class SceneFlowInstallerMB : SceneFlowAuthoring, ILoadingScreenConfig
    {
        public void InstallSceneFlowRuntime(IRuntimeContainerBuilder builder, IScopeNode lts)
        {
            ValidateOrThrow();

            if (lts == null || lts.Kind != LifetimeScopeKind.Project)
                return;

            RegisterSceneFlowServices(builder, this);
        }

        public static void RegisterSceneFlowServices(IRuntimeContainerBuilder builder, ILoadingScreenConfig config)
        {
            if (builder == null)
                throw new System.ArgumentNullException(nameof(builder));

            if (config != null && config.LoadingScenePrefab != null)
            {
                builder.RegisterInstance(config).As<ILoadingScreenConfig>();
            }
            else
            {
                builder.Register<NullLoadingScreenMB>(RuntimeLifetime.Singleton)
                       .As<ILoadingScreenConfig>();
            }

            builder.Register<LoadingScreenService>(RuntimeLifetime.Singleton)
                   .As<ILoadingScreenService>()
                   .As<IScopeAcquireHandler>()
                   .As<IScopeReleaseHandler>();

            builder.Register<SceneService>(RuntimeLifetime.Singleton)
                .As<ISceneService>();

            builder.Register<SceneFlowBlackboardInitializer>(RuntimeLifetime.Singleton)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
        public sealed class NullLoadingScreenMB : ILoadingScreenConfig
        {
            static readonly CommandListData EmptyCommands = new();
            public float CommandLeadTimeBeforeSceneChangeSeconds => 0f;
            public LoadingScreenMB LoadingScenePrefab => null;
            public CommandListData ShowCommands => EmptyCommands;
            public CommandListData HideCommands => EmptyCommands;
            public CommandListData ProgressCommands => EmptyCommands;
            public VarKeyRef MessageVar => default;
            public VarKeyRef ProgressVar => default;
        }

        sealed class SceneFlowBlackboardInitializer : IScopeAcquireHandler, IScopeReleaseHandler
        {
            readonly IProjectBlackboardService _blackboard;

            public SceneFlowBlackboardInitializer(IProjectBlackboardService blackboard)
            {
                _blackboard = blackboard;
            }

            public void OnAcquire(IScopeNode scope, bool isReset)
            {
                _ = scope;
                _ = isReset;
                EnsureDefaultVars();
            }

            public void OnRelease(IScopeNode scope, bool isReset)
            {
                _ = scope;
                _ = isReset;
                EnsureDefaultVars();
            }

            void EnsureDefaultVars()
            {
                _blackboard?.TryLocalSetVariant(
                    VarIds.GameLib.SceneManager.IsLoading,
                    DynamicVariant.FromBool(false));
            }
        }
    }
}
