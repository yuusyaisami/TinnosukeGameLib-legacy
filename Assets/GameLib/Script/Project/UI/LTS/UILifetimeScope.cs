#nullable enable
using VContainer;
using VContainer.Unity;
using UnityEngine;
using Game.Scene;
using Game.Common;

namespace Game.UI
{
    // ================================================================
    // UILifetimeScope: UI階層のルートとなるLifetimeScope
    // ================================================================
    //
    // ## 概要
    //
    // UILifetimeScopeは、UI全体のルートコンテナとして機能し、
    // UI関連の共通サービスを提供する。
    //
    // ## サービス登録
    //
    // 以下のサービスがこのスコープで登録される:
    // - IUIElementLifecycleService: UIElement生成/削除の一元管理
    //
    // ## 配置
    //
    // 通常、シーンのLifetimeScopeの直下に配置される。
    // UIElementLifetimeScopeはこのスコープの子となる。
    //
    // ================================================================

    [RequireComponent(typeof(Game.Commands.CommandRunnerMB))]
    [RequireComponent(typeof(Game.Scalar.BaseScalarMB))]
    [RequireComponent(typeof(Game.Common.EventMB))]

    // UI専用RequireComponent属性
    [RequireComponent(typeof(UIInputMB))]
    [RequireComponent(typeof(UINavigationMB))]
    [RequireComponent(typeof(UIModalStackMB))]
    [RequireComponent(typeof(UISelectionMB))]
    [RequireComponent(typeof(UICanvasMB))]
    [RequireComponent(typeof(BlackboardMB))]
    public class UILifetimeScope : BaseLifetimeScope<SceneLifetimeScope>
    {
        // UI は親(Scene)の下でビルドされるのでルートではない
        protected override bool IsBuildRoot => false;
        // 協調ビルドには参加させる
        protected override bool UseBuildCoordinator => true;
        // 自動 Build は不要（親からの協調ビルド or BaseLifetimeScopeSpawner が面倒を見る）
        protected override bool AutoBuildOnAwake => false;

        protected override void ConfigureBase(IContainerBuilder builder)
        {
            // ----------------------------------------------------------------
            // UIElementLifecycleService
            // ----------------------------------------------------------------
            //
            // UI要素の生成・削除を一元的に管理するサービス。
            // 以下の機能を提供:
            // - BaseLifetimeScopeSpawner経由のUIElement生成
            // - IScopeLifecycleServiceを使った安全な削除
            // - Blackboard/Commandのコンテキスト設定
            //
            // このサービスはUILifetimeScopeでグローバルに登録され、
            // すべての子スコープから利用可能となる。
            // ----------------------------------------------------------------
            builder.Register<UIElementLifecycleService>(Lifetime.Singleton)
                .As<IUIElementLifecycleService>()
                .WithParameter<Transform>(transform);
        }
    }
}
