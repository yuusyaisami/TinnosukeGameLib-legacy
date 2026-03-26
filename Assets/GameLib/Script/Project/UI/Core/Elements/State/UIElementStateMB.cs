#nullable enable
using UnityEngine;
using VContainer;
using System.Collections.Generic;
using System;
using Game.Common;
using VNext = Game.Commands.VNext;

namespace Game.UI
{
    // ================================================================
    // UIElementStateMB - UIElementの状態設定用MonoBehaviour
    // ================================================================
    //
    // ## 概要
    //
    // UIElementStateMBは、UIElementの状態と設定を管理するMonoBehaviour。
    // IScopeNode（BaseLifetimeScope/RuntimeLifetimeScope）配下で動作し、以下の機能を提供する:
    //
    // 1. **Inspector設定**: Active/Visible、当たり判定、ナビゲーション設定
    // 2. **DIコンテナ登録**: UIElementStateServiceをスコープに登録
    // 3. **実行時設定変更**: Inspectorの変更をServiceに反映
    //
    // ## 当たり判定RectTransformについて
    //
    // ナビゲーションやポインター選択時に、このUIElementの物理的な領域を
    // 定義するために使用される。
    //
    // ### デフォルト動作
    //
    // このMBがアタッチされたGameObjectにRectTransformがある場合、
    // 自動的にそれが当たり判定として設定される。
    //
    // ### 複数設定
    //
    // 複雑な形状のUIElement（L字型など）では、複数のRectTransformを
    // 設定して当たり判定領域を構成できる。
    //
    // ## ナビゲーション設定について
    //
    // ### IsNavigationSelectable
    //
    // falseに設定すると、キーボード/ゲームパッドによるナビゲーションで
    /// このUIElementは選択候補から除外される。
    ///
    /// 用途: Page、Window、Panel等のコンテナ要素
    ///
    /// ### NavigationOverride
    ///
    /// 各方向（上下左右）の移動先を明示的に指定できる。
    /// 設定されていない方向は自動計算される。
    ///
    // ================================================================

    public interface IUIElementStateOptions
    {
        IReadOnlyList<RectTransform> HitTestRects { get; }
        int SelectionOrder { get; }
        int NavigationSelectionOrder { get; }
        Game.Common.DynamicValue<bool> IsSelectable { get; }
        Game.Common.DynamicValue<bool> IsNavigationSelectable { get; }
        NavigationOverride? NavigationOverride { get; }
        VNext.CommandListData OnSelectedCommands { get; }
        VNext.CommandListData OnDeselectedCommands { get; }
    }

    /// <summary>
    /// UIElementの状態設定用MonoBehaviour。
    /// 
    /// ## RequireComponent
    /// 
    /// UIElementLifetimeScopeにRequireComponentで強制されるが、
    /// RuntimeLifetimeScope配下でも同様に動作する。
    /// 
    /// ## IFeatureInstaller
    /// 
    /// このMBはIFeatureInstallerを実装し、
    /// UIElementStateServiceをDIコンテナに登録する。
    /// </summary>
    public sealed class UIElementStateMB : MonoBehaviour, IFeatureInstaller, IUIElementStateOptions, IScopeAcquireHandler, IScopeReleaseHandler
    {
        // ================================================================
        // Inspector設定 - 基本状態
        // ================================================================

        [Header("基本状態")]
        [Tooltip("UIシステムとしてのActive状態。falseの場合、選択不可・入力不可になる。\n" +
                 "注意: ScopeのActive（BaseLifetimeScopeならGameObjectのActive）と合算される。")]
        [SerializeField]
        bool _initialActive = true;

        [Tooltip("UIシステムとしてのVisible状態。falseの場合、絶対に描画されない。")]
        [SerializeField]
        bool _initialVisible = true;

        // ================================================================
        // Inspector設定 - 当たり判定
        // ================================================================

        [Header("当たり判定")]
        [Tooltip("ナビゲーション・ポインター選択の当たり判定に使用するRectTransform。\n" +
                 "空の場合、このGameObjectのRectTransformが自動で追加される。\n" +
                 "複数設定することで複雑な形状の当たり判定を構成できる。")]
        [SerializeField]
        List<RectTransform> _hitTestRects = new();

        [Tooltip("ポインター選択や汎用選択の優先度。\n" +
                 "数値が大きいほど優先される。\n" +
                 "同値なら見た目上前に描画されている要素が優先される。")]
        [SerializeField]
        int _selectionOrder = 0;

        [Tooltip("方向ナビゲーション専用の優先度。\n" +
                 "数値が大きいほど優先される。\n" +
                 "Selection Order とは別に、上下左右移動だけで使われる。")]
        [SerializeField]
        int _navigationSelectionOrder = 0;
        // ================================================================
        // Inspector設定 - ナビゲーション
        // ================================================================

        [Header("ナビゲーション設定")]
        [Tooltip("このUI自体を選択対象に含めるか。\n" +
                 "false ならポインター・直接選択・ナビゲーションの全部で選ばれない。")]
        [SerializeField]
        [DynamicValueDefaultLiteral(true)]
        Game.Common.DynamicValue<bool> _isSelectable = new Game.Common.DynamicValue<bool>();

        [Tooltip("方向ナビゲーションでだけ選択対象に含めるか。\n" +
                 "Is Selectable が true でも、ここが false ならクリックはできるがナビゲーションでは止まらない。")]
        [SerializeField]
        [DynamicValueDefaultLiteral(true)]
        Game.Common.DynamicValue<bool> _isNavigationSelectable = new Game.Common.DynamicValue<bool>();

        [Tooltip("方向ごとの移動先を明示指定する。\n" +
                 "設定した方向は自動計算より優先される。")]
        [SerializeField]
        NavigationOverride? _navigationOverride;


        // ================================================================
        // Inspector設定 - 選択イベントコマンド
        // ================================================================

        [Header("選択イベント")]
        [Tooltip("このUIElementが選択されたときに実行するコマンド。")]
        [SerializeField]
        [VNext.CommandListFunctionName("UIElementState.OnSelected")]
        VNext.CommandListData _onSelectedCommands = new();

        [Tooltip("このUIElementの選択が解除されたときに実行するコマンド。")]
        [SerializeField]
        [VNext.CommandListFunctionName("UIElementState.OnDeselected")]
        VNext.CommandListData _onDeselectedCommands = new();

        // ================================================================
        // キャッシュ
        // ================================================================

        /// <summary>登録されたService（ランタイム参照用）</summary>
        UIElementStateService? _service;

        /// <summary>所有者スコープ</summary>
        IScopeNode? _ownerScope;

        /// <summary>VarStore キャッシュ</summary>
        VarStore? _varStore;

        // ================================================================
        // プロパティ
        // ================================================================

        /// <summary>
        /// 当たり判定用RectTransformのリスト。
        /// </summary>
        public IReadOnlyList<RectTransform> HitTestRects => _hitTestRects;

        /// <summary>
        /// 選択順序。
        /// </summary>
        public int SelectionOrder => _selectionOrder;

        /// <summary>
        /// ナビゲーション専用の優先度。
        /// </summary>
        public int NavigationSelectionOrder => _navigationSelectionOrder;

        /// <summary>
        /// このUIElement自体が選択対象になれるかを決める条件。
        /// </summary>
        public Game.Common.DynamicValue<bool> IsSelectable => _isSelectable;

        /// <summary>
        /// ナビゲーションで選択可能かを決める条件（DynamicValue<bool>）。
        /// </summary>
        public Game.Common.DynamicValue<bool> IsNavigationSelectable => _isNavigationSelectable;

        /// <summary>
        /// ナビゲーション方向のオーバーライド設定。
        /// </summary>
        public NavigationOverride? NavigationOverride => _navigationOverride;

        /// <summary>
        /// 選択時コマンドリスト。
        /// </summary>
        public VNext.CommandListData OnSelectedCommands => _onSelectedCommands;

        /// <summary>
        /// 選択解除時コマンドリスト。
        /// </summary>
        public VNext.CommandListData OnDeselectedCommands => _onDeselectedCommands;

        // ================================================================
        // IFeatureInstaller実装
        // ================================================================

        /// <summary>
        /// UIElementStateServiceをDIコンテナに登録する。
        /// 
        /// ## 処理内容
        /// 
        /// 1. UIElementStateServiceをSingletonで登録
        /// 2. IUIElementStateとIUIElementStateControllerの両方で公開
        /// 3. BuildCallback内でInspector設定をServiceに反映
        /// 
        /// ## 呼び出しタイミング
        /// 
        /// UIElementLifetimeScopeのConfigure時に呼び出される。
        /// </summary>
        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            _ownerScope = scope;

            // 当たり判定用RectTransformのデフォルト設定
            // リストが空の場合、このGameObjectのRectTransformを自動追加
            EnsureDefaultHitTestRect();

            // UIElementStateServiceを登録
            builder.Register<UIElementStateService>(Lifetime.Singleton)
                .WithParameter<IScopeNode>(scope)
                .WithParameter<IUIElementStateOptions>(this)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IUIElementState>()
                .As<IUIElementStateController>()
                .As<IUIModalRoot>();

            // IUIInputConsumerHubを登録
            // 各FeatureInstaller（ボタン、スクロール等）はこのHubにConsumerを登録する
            builder.Register<UIInputConsumerHub>(Lifetime.Singleton)
                .As<IUIInputConsumerHub>();

            // Apply initial state and inspector settings to the created UIElementStateService
            builder.RegisterBuildCallback(container =>
            {
                if (container.TryResolve<UIElementStateService>(out var service))
                {
                    _service = service;  // _service をキャッシュ
                    service.SetHitTestRects(_hitTestRects);
                    service.SetSelectionOrder(_selectionOrder);
                    service.SetNavigationSelectionOrder(_navigationSelectionOrder);
                    service.SetSelectableCondition(_isSelectable);
                    service.SetNavigationSelectableCondition(_isNavigationSelectable);
                    service.SetNavigationOverride(_navigationOverride);
                    service.OnSelectedCommands.SetCommands(_onSelectedCommands);
                    service.OnDeselectedCommands.SetCommands(_onDeselectedCommands);
                    service.SetActive(_initialActive);
                    service.SetVisible(_initialVisible);
                }
            });
        }

        // ================================================================
        // MonoBehaviourライフサイクル
        // ================================================================
        void Awake()
        {
            BindDebugOwners();
        }

        /// <summary>
        /// Resetはエディタでコンポーネント追加時に呼ばれる。
        /// デフォルトで自身のRectTransformを当たり判定に追加する。
        /// </summary>
        void Reset()
        {
            EnsureDefaultHitTestRect();
        }

        /// <summary>
        /// OnValidateはInspector値変更時に呼ばれる（エディタのみ）。
        /// 実行時にServiceが存在すれば設定を反映する。
        /// </summary>
        void OnValidate()
        {
            BindDebugOwners();
            // 実行時のみ
            if (!Application.isPlaying) return;

            // Serviceが登録済みなら設定を反映
            if (_service != null)
            {
                // OnValidateでは選択監視の初期化は行わない（既に初期化済み）
                ApplyInspectorSettingsWithoutInitialize(_service);
            }
        }

        // ================================================================
        // IScopeAcquireHandler / IScopeReleaseHandler実装
        // ================================================================

        /// <summary>
        /// スコープ獲得時の初期化処理。
        /// VarStore から IsNavigationSelectable の変更を購読する。
        /// </summary>
        void IScopeAcquireHandler.OnAcquire(IScopeNode scope, bool isReset)
        {
            // VarStore を取得（ローカルから検索開始）
            if (_varStore == null && scope?.Resolver != null)
            {
                scope.Resolver.TryResolve<VarStore>(out _varStore);
            }
        }

        /// <summary>
        /// スコープ解放時のクリーンアップ処理。
        /// </summary>
        void IScopeReleaseHandler.OnRelease(IScopeNode scope, bool isReset)
        {
            // VarStore への購読は不要（VarStore 側で参照が解放される）
            _varStore = null;
        }

        // ================================================================
        // 内部メソッド
        // ================================================================

        /// <summary>
        /// デフォルトの当たり判定RectTransformを設定する。
        /// 
        /// ## 処理
        /// 
        /// _hitTestRectsが空の場合、自身のRectTransformを追加する。
        /// </summary>
        void EnsureDefaultHitTestRect()
        {
            if (_hitTestRects.Count > 0) return;

            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                _hitTestRects.Add(rt);
            }
        }

        void BindDebugOwners()
        {
            _onSelectedCommands?.BindDebugOwner(this, nameof(_onSelectedCommands));
            _onDeselectedCommands?.BindDebugOwner(this, nameof(_onDeselectedCommands));
        }


        /// <summary>
        /// Inspector設定をServiceに反映する（初期化なし）。
        /// OnValidateから呼ばれる。
        /// </summary>
        void ApplyInspectorSettingsWithoutInitialize(UIElementStateService service)
        {
            // 当たり判定
            service.SetHitTestRects(_hitTestRects);

            // ナビゲーション設定
            service.SetSelectionOrder(_selectionOrder);
            service.SetNavigationSelectionOrder(_navigationSelectionOrder);
            service.SetSelectableCondition(_isSelectable);
            service.SetNavigationSelectableCondition(_isNavigationSelectable);
            service.SetNavigationOverride(_navigationOverride);

            // 選択イベントコマンド
            service.OnSelectedCommands.SetCommands(_onSelectedCommands);
            service.OnDeselectedCommands.SetCommands(_onDeselectedCommands);
        }
    }
}
