#nullable enable
using UnityEngine;
using VContainer;
using VContainer.Unity;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.UI
{
    // ================================================================
    // UIModalStackMB: UIModalStackServiceのFeatureInstaller
    // ================================================================

    /// <summary>
    /// UIModalStackServiceをDIコンテナに登録するFeatureInstaller。
    /// 
    /// ## 概要
    /// 
    /// UIModalStackServiceは、Modal Stack（モーダルUIの階層）を管理するサービス。
    /// 選択/ナビゲーション/ヒットテストの有効範囲を制限する役割を持つ。
    /// 
    /// ## Modal Stackとは
    /// 
    /// Modal Stackは、UIのヒエラルキーにおける「最低ライン」を意味する。
    /// これより上の階層（親方向）への選択移動は禁止される。
    /// 
    /// ## 主な機能
    /// 
    /// 1. **PushModal**: 新しいモーダルをStackに追加
    /// 2. **PopModal**: モーダルをStackから削除
    /// 3. **CurrentInputRoot**: 現在の入力対象ルートを提供
    /// 4. **選択クランプ**: 選択がModal外に出ないよう強制
    /// 
    /// ## 使用例
    /// 
    /// - メニュー画面を開く → PushModal
    /// - 確認ダイアログを開く → PushModal（スタック追加）
    /// - ダイアログを閉じる → PopModal
    /// </summary>
    public sealed class UIModalStackMB : MonoBehaviour, IFeatureInstaller
    {
        // ----------------------------------------------------------------
        // Inspector設定
        // ----------------------------------------------------------------

        [Header("Initial Root")]
        [Tooltip("起動時にModalStackの基準(root)として使用するUIElement。nullの場合は自身(Transform配下)から探索して設定する")]
        [SerializeField]
        UIElementLifetimeScope? _initialRoot;

        [Header("Debug")]
        [Tooltip("Modal Stack変更のログを出力するか")]
        [SerializeField]
        bool _enableModalStackLogging = false;

        [Header("Debug")]
        [SerializeField]
        UIModalStackDebugView _debugView = new UIModalStackDebugView();

        // ----------------------------------------------------------------
        // キャッシュ
        // ----------------------------------------------------------------

        IScopeNode? _ownerScope;

        // ----------------------------------------------------------------
        // IFeatureInstaller実装
        // ----------------------------------------------------------------

        /// <summary>
        /// UIModalStackServiceをDIコンテナに登録する。
        /// 
        /// 登録順序の注意:
        /// - UIModalStackServiceは他のUIサービスより先に登録される必要がある
        /// - UISelectionService, UINavigationServiceがこれに依存する
        /// </summary>
        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            _ownerScope = scope;

            // Modal Stack設定を登録
            builder.RegisterInstance(new UIModalStackOptions
            {
                EnableModalStackLogging = _enableModalStackLogging
            });

            // UIModalStackServiceを登録
            // - IUIModalStackService: 公開インターフェース
            builder.Register<UIModalStackService>(Lifetime.Singleton)
                .As<IUIModalStackService>()
                .As<IUIModalStackTelemetry>();

            // Register debug view instance and bind on build
            builder.RegisterInstance(_debugView);
            builder.RegisterBuildCallback(container =>
            {
                if (container.TryResolve<IUIModalStackTelemetry>(out var telemetry))
                {
                    _debugView.Bind(telemetry);
                }
            });

            // 初期ルートの設定（BuildCallback内で行う）
            builder.RegisterBuildCallback(SetupInitialRoot);
        }

        /// <summary>
        /// デフォルトルートを設定する。
        /// BuildCallback内で呼ばれる。
        /// </summary>
        void SetupInitialRoot(IObjectResolver container)
        {
            if (_ownerScope == null)
                return;

            // IUIModalStackServiceを取得
            if (container.TryResolve<IUIModalStackService>(out var modalStackService))
            {
                UniTask.Void(async () =>
                {
                    try
                    {
                        // 0) explicit inspector root
                        if (_initialRoot != null)
                        {
                            await ScopeFeatureInstallerUtility.WaitForResolverBuiltAsync(_initialRoot, CancellationToken.None);
                            var initResolver = _initialRoot.Resolver;
                            if (initResolver != null && initResolver.TryResolve<IUIModalRoot>(out var initRoot) && initRoot != null)
                            {
                                modalStackService.SetDefaultRoot(initRoot);
                                return;
                            }
                        }

                        // 1) Owner scope - wait if resolver isn't ready
                        await ScopeFeatureInstallerUtility.WaitForResolverBuiltAsync(_ownerScope, CancellationToken.None);
                        if (_ownerScope.Resolver != null && _ownerScope.Resolver.TryResolve<IUIModalRoot>(out var ownerRoot2) && ownerRoot2 != null)
                        {
                            modalStackService.SetDefaultRoot(ownerRoot2);
                            return;
                        }

                        // 2) transform subtree: search LifetimeScope components (includeInactive)
                        var t2 = transform != null ? transform : _ownerScope.Identity?.SelfTransform;
                        if (t2 != null)
                        {
                            var lifetimeScopes2 = t2.GetComponentsInChildren<VContainer.Unity.LifetimeScope>(includeInactive: true);

                            for (int i = 0; i < lifetimeScopes2.Length; i++)
                            {
                                var ls = lifetimeScopes2[i];

                                // Discover IScopeNode on the same GameObject by enumerating components.
                                IScopeNode? nodeFound2 = null;
                                if (ls is Component compRoot2)
                                {
                                    var comps = compRoot2.GetComponents<Component>();
                                    for (int ci = 0; ci < comps.Length; ci++)
                                    {
                                        var c = comps[ci];
                                        if (c is IScopeNode n)
                                        {
                                            nodeFound2 = n;
                                            break;
                                        }
                                    }
                                }

                                if (nodeFound2 != null)
                                {
                                    // Wait for the resolver to be built if necessary
                                    await ScopeFeatureInstallerUtility.WaitForResolverBuiltAsync(nodeFound2, CancellationToken.None);

                                    var resolver = nodeFound2.Resolver;
                                    if (resolver != null && resolver.TryResolve<IUIModalRoot>(out var root) && root != null)
                                    {
                                        modalStackService.SetDefaultRoot(root);
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex, this);
                    }
                });
            }
        }
    }

    // ================================================================
    // UIModalStackOptions: UIModalStackServiceのオプション設定
    // ================================================================

    /// <summary>
    /// UIModalStackServiceのオプション設定。
    /// MBのInspector設定をServiceに渡すために使用。
    /// </summary>
    public sealed class UIModalStackOptions
    {
        /// <summary>Modal Stack変更のログを出力するか</summary>
        public bool EnableModalStackLogging { get; set; }
    }
}
