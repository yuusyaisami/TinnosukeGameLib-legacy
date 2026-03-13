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
    /// ProjectLifetimeScope と同じ GameObject にアタッチして、
    /// シーン遷移サービスとローディングサービスを登録するインストーラ。
    /// </summary>
    public sealed class SceneFlowInstallerMB : MonoBehaviour, IFeatureInstaller, ILoadingScreenConfig
    {
        [Header("Loading Screen")]
        [Tooltip("ロード画面を使用するか。false ならプレハブが null になり、LoadingScreenService は no-op になる。")]
        [SerializeField] bool useLoadingScreen = true;


        [BoxGroup("Scene"), LabelText("Scene Prefab"), ShowIf(nameof(useLoadingScreen))]
        [SerializeField] SceneLifetimeScope loadingScenePrefab;

        [BoxGroup("Commands"), LabelText("Delay"), ShowIf(nameof(useLoadingScreen))]
        // // Showコマンドが実行されてからUnityのシーン変更が実行されるまでの遅延時間（秒）
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

        public void InstallFeature(IContainerBuilder builder, IScopeNode lts)
        {
            // プレハブは null でもよい。その場合ローディング自体は全て no-op。
            if (useLoadingScreen && loadingScenePrefab != null)
            {
                builder.RegisterInstance<ILoadingScreenConfig>(this);
            }
            else
            {
                // null の場合は NullLoadingScreenMB を登録（no-op実装）
                builder.Register<NullLoadingScreenMB>(Lifetime.Singleton)
                       .As<ILoadingScreenConfig>();
            }

            builder.Register<LoadingScreenService>(Lifetime.Singleton)
                   .As<ILoadingScreenService>()
                   .As<IScopeAcquireHandler>()
                   .As<IScopeReleaseHandler>();

            builder.Register<SceneService>(Lifetime.Singleton)
                .As<ISceneService>();

            builder.Register<SceneFlowBlackboardInitializer>(Lifetime.Singleton)
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
