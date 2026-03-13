#nullable enable
using UnityEngine;
using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;
using Game.Spawn;
using Game.Common;
using VNext = Game.Commands.VNext;

namespace Game.UI
{
    // ================================================================
    // UIElementLifecycleService.cs - UIElement生成/削除の一元管理
    // ================================================================
    //
    // ## 概要
    //
    // UIElementLifecycleServiceは、UIElementの生成（Spawn）と削除（Despawn）を
    // 一元的に管理するサービス。
    //
    // ## なぜ「Lifecycle」と呼ぶか
    //
    // 単なるSpawnerではなく、以下の責務を持つため:
    //
    // 1. **生成管理**: IBaseLifetimeScopeSpawner経由でのUIElement生成
    // 2. **コンテキスト設定**: Blackboard/CommandRunner連携
    // 3. **削除管理**: 安全な削除処理（Active=false設定含む）
    // 4. **ライフサイクル連携**: IScopeLifecycleServiceとの連携
    //
    // ## 生成の仕組み
    //
    // UIElement PrefabはUIElementLifetimeScopeを必ず持つ。
    // 生成はIBaseLifetimeScopeSpawnerを経由して行い、
    // DIコンテナの協調ビルドを待機する。
    //
    // ## Blackboard/CommandRunner連携
    //
    // 生成時にBlackboard（IBlackboardService 経由の VarStore）に
    // データを設定し、ICommandRunnerでコマンドを実行できる。
    //
    // SpawnWithContextAsyncの引数:
    // - blackboard: LocalBag にマージされる VarStore
    // - commands: 生成直後に実行するコマンドリスト
    // - extraVariables: コマンド実行時にマージする VarStore
    //
    // ## 削除時の処理
    //
    // UIElementを削除する際は必ず以下の処理を行う:
    //
    // 1. UIElementStateServiceのActiveをfalseに設定
    // 2. BaseLifetimeScope.DespawnAsync()を呼び出し
    // 3. IScopeLifecycleServiceがあれば、そちらの処理を待つ
    //
    // ================================================================

    // ================================================================
    // IUIElementLifecycleService: 公開インターフェース
    // ================================================================

    /// <summary>
    /// UIElement生成/削除サービスの公開インターフェース。
    /// 
    /// ## 責務
    /// 
    /// - UIElement Prefabの生成（IBaseLifetimeScopeSpawner経由）
    /// - UIElementの安全な削除
    /// - 生成済みUIElementの追跡
    /// 
    /// ## 注意
    /// 
    /// 直接Destroy()を呼ばず、必ずDespawnAsyncを使用すること。
    /// </summary>
    public interface IUIElementLifecycleService
    {
        // ----------------------------------------------------------------
        // 生成API
        // ----------------------------------------------------------------

        /// <summary>
        /// UIElement Prefabを生成する。
        /// 
        /// ## 処理フロー
        /// 
        /// 1. IBaseLifetimeScopeSpawnerでPrefabを生成
        /// 2. 親Transform配下に配置
        /// 3. 協調ビルドを待機（IScopeLifecycleService.HandleSpawnAsyncも含む）
        /// 4. 生成済みリストに追加
        /// 5. UIElementLifetimeScopeを返す
        /// 
        /// ## パラメータ
        /// 
        /// - prefab: 生成するUIElement Prefab（UIElementLifetimeScopeを持つ）
        /// - parent: 配置先の親Transform（nullの場合はサービス所有者の下）
        /// - ct: キャンセルトークン
        /// </summary>
        UniTask<UIElementLifetimeScope?> SpawnAsync(
            GameObject prefab,
            Transform? parent = null,
            CancellationToken ct = default);

        /// <summary>
        /// UIElement Prefabを生成し、Blackboard/Commandを設定する。
        /// 
        /// ## Blackboardについて
        /// 
        /// UIElementに設定されたIBlackboardService.LocalVarsに
        /// 渡されたblackboard（IVarStore）をマージする。
        /// 
        /// ## Commandsについて
        /// 
        /// 生成直後にUIElementのICommandRunnerで実行するコマンドリスト。
        /// extraVariablesはこのコマンド実行時にマージされる。
        /// 
        /// ## 処理フロー
        /// 
        /// 1. SpawnAsyncで基本生成
        /// 2. IBlackboardServiceにblackboardをマージ
        /// 3. ICommandRunnerでcommandsを実行（extraVariablesをマージ）
        /// </summary>
        UniTask<UIElementLifetimeScope?> SpawnWithContextAsync(
            GameObject prefab,
            IVarStore? blackboard = null,
            VNext.CommandListData? commands = null,
            IVarStore? extraVariables = null,
            Transform? parent = null,
            CancellationToken ct = default);

        // ----------------------------------------------------------------
        // 削除API
        // ----------------------------------------------------------------

        /// <summary>
        /// UIElementを安全に削除する。
        /// 
        /// ## 処理フロー
        /// 
        /// 1. UIElementStateServiceのActiveをfalseに設定
        /// 2. 生成済みリストから削除
        /// 3. BaseLifetimeScope.DespawnAsync()を呼び出し
        /// 4. IScopeLifecycleServiceの処理完了を待機
        /// 5. GameObjectを破棄
        /// 
        /// ## 注意
        /// 
        /// このメソッドはGameObjectが実際に破棄されるまで待機する。
        /// 即座に戻りたい場合はDespawnFireAndForgetを使用。
        /// </summary>
        UniTask DespawnAsync(UIElementLifetimeScope element, CancellationToken ct = default);

        /// <summary>
        /// UIElementを非同期で削除開始する（完了を待たない）。
        /// 
        /// ## 用途
        /// 
        /// 削除完了を待つ必要がない場合に使用。
        /// 内部的にはDespawnAsyncを呼び出す。
        /// </summary>
        void DespawnFireAndForget(UIElementLifetimeScope element);

        // ----------------------------------------------------------------
        // 状態取得
        // ----------------------------------------------------------------

        /// <summary>
        /// このサービスで生成されたUIElementの一覧。
        /// </summary>
        IReadOnlyList<UIElementLifetimeScope> SpawnedElements { get; }

        /// <summary>
        /// 指定したUIElementがこのサービスで生成されたかどうか。
        /// </summary>
        bool IsSpawnedByThis(UIElementLifetimeScope element);
    }

    // ================================================================
    // UIElementLifecycleService: メイン実装
    // ================================================================

    /// <summary>
    /// UIElement生成/削除サービスの実装。
    /// 
    /// ## 依存
    /// 
    /// - IBaseLifetimeScopeSpawner: Prefab生成の実処理
    /// - Transform: デフォルトの親Transform
    /// 
    /// ## 登録
    /// 
    /// UILifetimeScopeのConfigureBaseで登録される。
    /// </summary>
    public sealed class UIElementLifecycleService : IUIElementLifecycleService
    {
        // ----------------------------------------------------------------
        // フィールド
        // ----------------------------------------------------------------

        /// <summary>
        /// BaseLifetimeScopeSpawner。
        /// Prefab生成の実処理を担当。
        /// </summary>
        readonly IScopeSpawner _spawner;

        /// <summary>
        /// デフォルトの親Transform。
        /// parent引数がnullの場合に使用。
        /// </summary>
        readonly Transform _defaultParent;

        /// <summary>
        /// 生成済みUIElementのリスト。
        /// Despawn時にここから削除される。
        /// </summary>
        readonly List<UIElementLifetimeScope> _spawnedElements = new();

        // ----------------------------------------------------------------
        // プロパティ
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public IReadOnlyList<UIElementLifetimeScope> SpawnedElements => _spawnedElements;

        // ----------------------------------------------------------------
        // コンストラクタ
        // ----------------------------------------------------------------

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="spawner">BaseLifetimeScopeSpawner</param>
        /// <param name="defaultParent">デフォルトの親Transform</param>
        public UIElementLifecycleService(
            IScopeSpawner spawner,
            Transform defaultParent)
        {
            _spawner = spawner;
            _defaultParent = defaultParent;
        }

        // ----------------------------------------------------------------
        // 生成API
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public async UniTask<UIElementLifetimeScope?> SpawnAsync(
            GameObject prefab,
            Transform? parent = null,
            CancellationToken ct = default)
        {
            if (prefab == null)
            {
                Debug.LogError("[UIElementLifecycleService] SpawnAsync: prefab is null.");
                return null;
            }

            // Prefabの検証
            var prefabScope = prefab.GetComponent<UIElementLifetimeScope>();
            if (prefabScope == null)
            {
                Debug.LogError($"[UIElementLifecycleService] SpawnAsync: " +
                             $"Prefab '{prefab.name}' does not have UIElementLifetimeScope.");
                return null;
            }

            // 親Transformを決定
            var actualParent = parent ?? _defaultParent;

            // IBaseLifetimeScopeSpawnerで生成
            // SpawnAsyncは協調ビルド + IScopeLifecycleService.HandleSpawnAsyncを自動実行
            var param = new ScopeSpawnParams
            {
                Parent = actualParent,
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                WorldSpace = false,
                BuildSynchronously = false
            };

            UIElementLifetimeScope elementScope;
            try
            {
                elementScope = await _spawner.SpawnAsync(prefabScope, param, ct);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"[UIElementLifecycleService] SpawnAsync cancelled: {prefab.name}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UIElementLifecycleService] SpawnAsync failed: {prefab.name}, {ex.Message}");
                return null;
            }

            if (elementScope == null)
            {
                Debug.LogError($"[UIElementLifecycleService] SpawnAsync: Spawner returned null.");
                return null;
            }

            // 生成済みリストに追加
            _spawnedElements.Add(elementScope);

            Debug.Log($"[UIElementLifecycleService] Spawned: {elementScope.name}");

            return elementScope;
        }

        /// <inheritdoc/>
        public async UniTask<UIElementLifetimeScope?> SpawnWithContextAsync(
            GameObject prefab,
            IVarStore? blackboard = null,
            VNext.CommandListData? commands = null,
            IVarStore? extraVariables = null,
            Transform? parent = null,
            CancellationToken ct = default)
        {
            // 基本の生成
            var element = await SpawnAsync(prefab, parent, ct);

            if (element == null)
            {
                return null;
            }

            // Blackboard設定
            if (blackboard != null)
            {
                if (element.Container != null &&
                    element.Container.TryResolve<IBlackboardService>(out var bbService))
                {
                    // UIElementのLocalVarsにマージ
                    blackboard.MergeInto(bbService.LocalVars, overwrite: true);
                    Debug.Log($"[UIElementLifecycleService] Merged blackboard to {element.name}");
                }
                else
                {
                    Debug.LogWarning($"[UIElementLifecycleService] Cannot find IBlackboardService in {element.name}");
                }
            }

            // Command実行
            if (commands != null && commands.Count > 0)
            {
                if (element.Container != null &&
                    element.Container.TryResolve<VNext.ICommandRunner>(out var runner))
                {
                    // コマンド実行用のVarsを準備
                    var vars = new VarStore();

                    // extraVariablesをマージ
                    if (extraVariables != null)
                    {
                        extraVariables.MergeInto(vars, overwrite: false);
                    }

                    // CommandContextを作成して実行
                    var options = VNext.CommandRunOptions.Default;
                    var ctx = new VNext.CommandContext(element, vars, runner, element, options);
                    try
                    {
                        var result = await runner.ExecuteListAsync(commands, ctx, ct, options);
                        if (result.Status == VNext.CommandRunStatus.Error)
                        {
                            Debug.LogError($"[UIElementLifecycleService] Command execution failed: {element.name}, {result.Message}");
                        }
                        else
                        {
                            Debug.Log($"[UIElementLifecycleService] Executed {commands.Count} commands on {element.name}");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.LogWarning($"[UIElementLifecycleService] Command execution cancelled: {element.name}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UIElementLifecycleService] Command execution failed: {element.name}, {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[UIElementLifecycleService] Cannot find ICommandRunner in {element.name}");
                }
            }

            return element;
        }

        // ----------------------------------------------------------------
        // 削除API
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public async UniTask DespawnAsync(UIElementLifetimeScope element, CancellationToken ct = default)
        {
            if (element == null)
            {
                Debug.LogWarning("[UIElementLifecycleService] DespawnAsync: element is null.");
                return;
            }

            // 生成済みリストから削除
            _spawnedElements.Remove(element);

            // 1. UIElementStateServiceのActiveをfalseに設定
            // これにより、削除処理中に選択されることを防ぐ
            var stateController = element.GetUIElementStateController();
            if (stateController != null)
            {
                stateController.SetActive(false);
                Debug.Log($"[UIElementLifecycleService] Set Active=false: {element.name}");
            }

            // 2. BaseLifetimeScope.DespawnAsync()を呼び出し
            // これはIScopeLifecycleServiceがあれば、そちらの処理完了を待つ
            try
            {
                await element.DespawnAsync(ct);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"[UIElementLifecycleService] DespawnAsync cancelled: {element.name}");
            }

            Debug.Log($"[UIElementLifecycleService] Despawned: {element.name}");
        }

        /// <inheritdoc/>
        public void DespawnFireAndForget(UIElementLifetimeScope element)
        {
            DespawnAsync(element).Forget();
        }

        // ----------------------------------------------------------------
        // 状態取得
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public bool IsSpawnedByThis(UIElementLifetimeScope element)
        {
            return element != null && _spawnedElements.Contains(element);
        }
    }
}
