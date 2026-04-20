// Assets/Game/Script/Flow/SceneFlowInstallerMB.cs
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
        SceneLifetimeScope LoadingScenePrefab { get; }
        CommandListData ShowCommands { get; }
        CommandListData HideCommands { get; }
        CommandListData ProgressCommands { get; }
        VarKeyRef MessageVar { get; }
        VarKeyRef ProgressVar { get; }
    }
    /// <summary>
    /// ProjectLifetimeScope 縺ｨ蜷後§ GameObject 縺ｫ繧｢繧ｿ繝・メ縺励※縲・
    /// 繧ｷ繝ｼ繝ｳ驕ｷ遘ｻ繧ｵ繝ｼ繝薙せ縺ｨ繝ｭ繝ｼ繝・ぅ繝ｳ繧ｰ繧ｵ繝ｼ繝薙せ繧堤匳骭ｲ縺吶ｋ繧､繝ｳ繧ｹ繝医・繝ｩ縲・
    /// </summary>
    public sealed class SceneFlowInstallerMB : MonoBehaviour, IFeatureInstaller, ILoadingScreenConfig
    {
        [Header("Loading Screen")]
        [Tooltip("Inspector setting.")]
        [SerializeField] bool useLoadingScreen = true;


        [BoxGroup("Scene"), LabelText("Scene Prefab"), ShowIf(nameof(useLoadingScreen))]
        [SerializeField] SceneLifetimeScope loadingScenePrefab;

        [BoxGroup("Commands"), LabelText("Delay"), ShowIf(nameof(useLoadingScreen))]
        // // Show繧ｳ繝槭Φ繝峨′螳溯｡後＆繧後※縺九ｉUnity縺ｮ繧ｷ繝ｼ繝ｳ螟画峩縺悟ｮ溯｡後＆繧後ｋ縺ｾ縺ｧ縺ｮ驕・ｻｶ譎る俣・育ｧ抵ｼ・
        [SerializeField, Min(0f)] float commandLeadTimeBeforeSceneChangeSeconds = 0f;

        [BoxGroup("Commands"), LabelText("Show"), ShowIf(nameof(useLoadingScreen))]
        [SerializeField] CommandListData showCommands = new();
        [BoxGroup("Commands"), LabelText("Progress"), ShowIf(nameof(useLoadingScreen))]
        [SerializeField] CommandListData progressCommands = new();
        [BoxGroup("Commands"), LabelText("Hide"), ShowIf(nameof(useLoadingScreen))]
        [SerializeField] CommandListData hideCommands = new();

        [BoxGroup("Context Vars"), LabelText("Message Var"), ShowIf(nameof(useLoadingScreen))]
        [SerializeField] VarKeyRef messageVar;
        [BoxGroup("Context Vars"), LabelText("Progress Var"), ShowIf(nameof(useLoadingScreen))]
        [SerializeField] VarKeyRef progressVar;
        public float CommandLeadTimeBeforeSceneChangeSeconds => commandLeadTimeBeforeSceneChangeSeconds;
        public SceneLifetimeScope LoadingScenePrefab => loadingScenePrefab;
        public CommandListData ShowCommands => showCommands ?? new CommandListData();
        public CommandListData HideCommands => hideCommands ?? new CommandListData();
        public CommandListData ProgressCommands => progressCommands ?? new CommandListData();
        public VarKeyRef MessageVar => messageVar;
        public VarKeyRef ProgressVar => progressVar;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode lts)
        {
            // 繝励Ξ繝上ヶ縺ｯ null 縺ｧ繧ゅｈ縺・ゅ◎縺ｮ蝣ｴ蜷医Ο繝ｼ繝・ぅ繝ｳ繧ｰ閾ｪ菴薙・蜈ｨ縺ｦ no-op縲・
            if (useLoadingScreen && loadingScenePrefab != null)
            {
                builder.RegisterInstance<ILoadingScreenConfig>(this);
            }
            else
            {
                // null 縺ｮ蝣ｴ蜷医・ NullLoadingScreenMB 繧堤匳骭ｲ・・o-op螳溯｣・ｼ・
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
            public SceneLifetimeScope LoadingScenePrefab => null;
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
