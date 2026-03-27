#nullable enable
using VContainer;
using VContainer.Unity;
using UnityEngine;
using System.Collections.Generic;
using Game.Common;

namespace Game.UI
{
    // ================================================================
    // UIElementLifetimeScope: UIElementの基盤となるLifetimeScope
    // ================================================================
    //
    // ## 概要
    //
    // UIElementLifetimeScopeは、UIにおける一つの選択単位を表す。
    // すべてのUIElement（ボタン、パネル、ダイアログ等）は
    // このクラスまたは派生クラスをアタッチする。
    //
    // ## UIElementの設計思想
    //
    // UIElementはFeatureInstallerにより柔軟に機能を追加できる:
    // - ボタン機能を追加 → ButtonChannelHubMB
    // - ページ機能を追加 → UIPageFeatureInstaller
    // - トグル機能を追加 → UIToggleFeatureInstaller
    //
    // ## 必須コンポーネント
    //
    // - **UIElementStateMB**: Active/Visible状態、当たり判定、ナビゲーション設定
    //   RequireComponentで強制的にアタッチされる
    //
    // - **CommandRunnerMB**: コマンド実行機能
    // - **BaseScalarMB**: スカラー値管理
    // - **EventMB**: イベントシステム
    //
    // ## Active状態について
    //
    // UIシステムにおいて、GameObjectのSetActive(false)は使用しない。
    // GameObject自体は常にactive=trueのままであり、UIシステム内部の
    // ロジックとしてActive状態を管理する（UIElementStateService）。
    //
    // Active=falseの場合:
    // - 選択対象から除外
    // - 入力イベントを受け取らない
    // - 親がActive=falseなら、子も実質的にActive=false
    //
    // ## IUIModalRootの実装
    //
    // このクラスはIUIModalRootを実装しており、
    // Modal Stackに登録可能なルート要素として機能する。
    //
    // Modal Stackに登録されると:
    // - このElement配下のみが選択可能となる（選択のクランプ）
    // - ナビゲーションの捜索範囲がこの配下に制限される
    //
    // ## IUIInputConsumerHub
    //
    // UIElementには複数のIUIInputConsumerが登録される可能性がある:
    // - ボタン押下処理
    // - スクロール処理
    // - ドラッグ処理
    //
    // これらはIUIInputConsumerHubを通じて集約管理される。
    // VContainerではIEnumerable<T>で複数解決もできるが、
    // Hubを使用することで優先度ソートや動的登録が可能になる。
    //
    // ================================================================

    /// <summary>
    /// UIElementの基盤となるLifetimeScope。
    /// 
    /// ## 設計方針
    /// 
    /// このクラス自体には最小限の機能のみを持たせ、
    /// 具体的な処理はServiceやFeatureInstallerに委譲する。
    /// </summary>
    [RequireComponent(typeof(Game.Commands.CommandRunnerMB))]
    [RequireComponent(typeof(Game.Scalar.BaseScalarMB))]
    [RequireComponent(typeof(Game.Common.EventMB))]
    [RequireComponent(typeof(UIElementStateMB))]
    [RequireComponent(typeof(BlackboardMB))]
    public class UIElementLifetimeScope : BaseLifetimeScope
    {
        // ----------------------------------------------------------------
        // BaseLifetimeScope設定
        // ----------------------------------------------------------------

        /// <summary>
        /// UI Window は親(UI)の下でビルドされるのでルートではない。
        /// </summary>
        protected override bool IsBuildRoot => false;

        /// <summary>
        /// 協調ビルドには参加させる。
        /// </summary>
        protected override bool UseBuildCoordinator => true;

        /// <summary>
        /// 自動 Build は不要（親からの協調ビルド or BaseLifetimeScopeSpawner が面倒を見る）。
        /// </summary>
        protected override bool AutoBuildOnAwake => false;

        // ----------------------------------------------------------------
        // ConfigureBase
        // ----------------------------------------------------------------

        /// <summary>
        /// Awake時の設定。
        /// ナビゲーションにおける初期設定等を記述可能。
        /// </summary>
        protected override void AwakeConfigure(IContainerBuilder builder)
        {
            // 将来的にUIElement固有の初期設定を追加可能
        }

        /// <summary>
        /// DIコンテナの基本構成。
        /// 
        /// ## 登録内容
        /// 
        /// - **IUIInputConsumerHub**: 複数のIUIInputConsumerを集約管理
        /// 
        /// ## 注意
        /// 
        /// UIElementStateServiceはUIElementStateMBで登録される。
        /// このメソッドでは登録しない。
        /// </summary>
        protected override void ConfigureBase(IContainerBuilder builder)
        {

        }

    }
}
