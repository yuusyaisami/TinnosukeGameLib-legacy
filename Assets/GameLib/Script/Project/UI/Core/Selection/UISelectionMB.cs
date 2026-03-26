#nullable enable
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    // ================================================================
    // UISelectionMB: UISelectionServiceのFeatureInstaller
    // ================================================================

    /// <summary>
    /// UISelectionServiceをDIコンテナに登録するFeatureInstaller。
    /// 
    /// ## 概要
    /// 
    /// UISelectionServiceは、UIの選択状態を管理する中核サービス。
    /// 多くのUIシステムがこのサービスにアクセスして現在の選択状態を取得する。
    /// 
    /// ## 主な機能
    /// 
    /// 1. **選択状態の管理**: 現在選択中のUIElement（Current）を保持
    /// 2. **ホバー状態の管理**: マウス操作時のホバー対象を保持
    /// 3. **選択履歴**: 前回の選択（Previous）を保持し、フォールバックに使用
    /// 4. **Modal Stackとの連携**: 選択範囲をCurrentInputRoot内に制限
    /// 
    /// ## 重要な制約
    /// 
    /// - Selectedは常にCurrentInputRoot配下に存在する必要がある
    /// - Modal Stackが変更されたとき、選択は自動的にクランプされる
    /// 
    /// ## 設計上の注意
    /// 
    /// 実際のSelect処理（物理的判定）はここでは行わない。
    /// WorldUI/ScreenUIで判定方法が異なるため、Interfaceで分離する。
    /// </summary>
    public sealed class UISelectionMB : MonoBehaviour, IFeatureInstaller
    {
        IUISelectionTelemetry? _telemetry;
        IUISelectionNavigation? _navigation;
        IUIModalStackTelemetry? _modalTelemetry;
        IUIModalStackService? _modalStackService;

        // ----------------------------------------------------------------
        // Inspector設定
        // ----------------------------------------------------------------

        [Header("Debug")]
        [Tooltip("選択変更のログを出力するか")]
        [SerializeField]
        bool _enableSelectionLogging = false;

        [Header("Debug")]
        [SerializeField]
        UISelectionDebugView _debugView = new UISelectionDebugView();

        [Header("Debug")]
        [SerializeField]
        bool _includeGraphicsInDump = true;

        [Header("Debug")]
        [SerializeField]
        bool _includeNodesWithoutUIState = false;

        [ShowInInspector]
        [Button("Dump Selectable UI Tree", ButtonSizes.Large)]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        void DumpSelectableUITree()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[UISelectionMB] DumpSelectableUITree is available only in play mode.");
                return;
            }

            if (_telemetry == null || _navigation == null || _modalTelemetry == null || _modalStackService == null)
            {
                Debug.LogWarning("[UISelectionMB] DumpSelectableUITree: telemetry or modal stack service is not ready.");
                return;
            }

            var sb = new StringBuilder(16 * 1024);
            sb.AppendLine("=== UISelection Selectable UI Tree Dump ===");
            sb.Append("Current: ").AppendLine(DescribeNode(_telemetry.Current));
            sb.Append("Previous: ").AppendLine(DescribeNode(_telemetry.Previous));
            sb.Append("Hovered: ").AppendLine(DescribeNode(_telemetry.Hovered));
            sb.Append("SelectionSource: ").AppendLine(_telemetry.LastSelectionSource.ToString());
            sb.Append("CandidateProvider: ").AppendLine(_telemetry.CandidateProvider?.GetType().FullName ?? "(null)");
            sb.Append("CurrentInputRoot: ").AppendLine(_modalTelemetry.CurrentInputRoot?.ModalId ?? "(null)");
            sb.Append("ModalDepth: ").AppendLine(_modalTelemetry.Depth.ToString());
            sb.Append("ActiveRoots: ").AppendLine(_modalTelemetry.ActiveRoots.Count.ToString());

            for (int i = 0; i < _modalTelemetry.ActiveRoots.Count; i++)
            {
                var activeRoot = _modalTelemetry.ActiveRoots[i];
                var rootScope = activeRoot.Root.OwnerScope;
                sb.Append("  [").Append(i).Append("] StackKey=").Append(activeRoot.StackKey)
                    .Append(" Priority=").Append(activeRoot.Priority)
                    .Append(" Policy=").Append(activeRoot.Policy)
                    .Append(" Root=").Append(activeRoot.Root.ModalId)
                    .Append(" Scope=").AppendLine(DescribeNode(rootScope));
            }

            var rootScopes = new List<IScopeNode>();
            var rootDedup = new HashSet<IScopeNode>(Game.ReferenceEqualityComparer<IScopeNode>.Instance);
            for (int i = 0; i < _modalTelemetry.ActiveRoots.Count; i++)
            {
                var rootScope = _modalTelemetry.ActiveRoots[i].Root.OwnerScope;
                if (rootScope != null && rootDedup.Add(rootScope))
                    rootScopes.Add(rootScope);
            }

            if (rootScopes.Count == 0 && _telemetry.CurrentInputRoot?.OwnerScope != null)
            {
                rootScopes.Add(_telemetry.CurrentInputRoot.OwnerScope);
            }

            if (rootScopes.Count == 0)
            {
                sb.AppendLine("No active root scope found.");
                EmitDumpLog(sb.ToString());
                return;
            }

            var visited = new HashSet<IScopeNode>(Game.ReferenceEqualityComparer<IScopeNode>.Instance);
            var providerSelectable = new HashSet<IScopeNode>(Game.ReferenceEqualityComparer<IScopeNode>.Instance);
            var selectableBuffer = new List<IScopeNode>();

            for (int i = 0; i < rootScopes.Count; i++)
            {
                var root = rootScopes[i];
                providerSelectable.Clear();
                selectableBuffer.Clear();
                _telemetry.CandidateProvider?.GetAllSelectableCandidates(root, selectableBuffer);
                for (int j = 0; j < selectableBuffer.Count; j++)
                {
                    var candidate = selectableBuffer[j];
                    if (candidate != null)
                        providerSelectable.Add(candidate);
                }

                sb.AppendLine();
                sb.Append("ROOT[").Append(i).Append("] ").AppendLine(DescribeNode(root));
                AppendScopeNodeRecursive(sb, root, "", isLast: true, visited, providerSelectable);
            }

            EmitDumpLog(sb.ToString());
        }

        // ----------------------------------------------------------------
        // IFeatureInstaller実装
        // ----------------------------------------------------------------

        /// <summary>
        /// UISelectionServiceをDIコンテナに登録する。
        /// 
        /// 登録順序の注意:
        /// - UISelectionServiceはIUIModalStackServiceに依存する
        /// - UIModalStackMBが先に登録されている必要がある
        /// </summary>
        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            // 選択設定を登録
            builder.RegisterInstance(new UISelectionOptions
            {
                EnableSelectionLogging = _enableSelectionLogging
            });

            // UISelectionServiceを登録
            // - IUISelectionService: 公開インターフェース
            builder.Register<UISelectionService>(Lifetime.Singleton)
                .As<IUISelectionService>()
                .As<IUISelectionState>()
                .As<IUISelectionNavigation>()
                .As<IUISelectionTelemetry>()
                .As<IUISelectionBlockService>();

            // Register debug view instance
            builder.RegisterInstance(_debugView);

            // Bind debug view to telemetry after build
            builder.RegisterBuildCallback(container =>
            {
                if (container.TryResolve<IUISelectionTelemetry>(out var telemetry))
                {
                    _telemetry = telemetry;
                    _debugView.Bind(telemetry);
                }

                if (container.TryResolve<IUISelectionNavigation>(out var navigation))
                    _navigation = navigation;

                if (container.TryResolve<IUIModalStackTelemetry>(out var modalTelemetry))
                    _modalTelemetry = modalTelemetry;

                if (container.TryResolve<IUIModalStackService>(out var modalStackService))
                    _modalStackService = modalStackService;
            });
        }

        void AppendScopeNodeRecursive(
            StringBuilder sb,
            IScopeNode node,
            string indent,
            bool isLast,
            HashSet<IScopeNode> visited,
            HashSet<IScopeNode> providerSelectable)
        {
            if (!visited.Add(node))
            {
                sb.Append(indent).Append(isLast ? "└─ " : "├─ ").Append("(cycle) ").AppendLine(DescribeNode(node));
                return;
            }

            var branch = isLast ? "└─ " : "├─ ";
            var childIndent = indent + (isLast ? "   " : "│  ");

            TryResolveOwnedUIState(node, out var state);
            var canSelect = _navigation != null && _navigation.CanSelect(node);
            var inAnyInputRoot = _modalStackService != null && _modalStackService.IsInAnyInputRoot(node);
            var isCurrent = ReferenceEquals(_telemetry?.Current, node);
            var isHovered = ReferenceEquals(_telemetry?.Hovered, node);
            var isPrevious = ReferenceEquals(_telemetry?.Previous, node);

            if (state == null && !_includeNodesWithoutUIState)
            {
                var children = Game.ScopeNodeHierarchy.GetChildrenOrEmpty(node);
                for (int i = 0; i < children.Count; i++)
                {
                    AppendScopeNodeRecursive(sb, children[i], indent, i == children.Count - 1, visited, providerSelectable);
                }
                return;
            }

            sb.Append(indent).Append(branch).Append(DescribeNode(node));
            if (isCurrent) sb.Append(" [Current]");
            if (isHovered) sb.Append(" [Hovered]");
            if (isPrevious) sb.Append(" [Previous]");
            sb.AppendLine();

            sb.Append(childIndent).Append("type=").Append(node.GetType().Name)
                .Append(" kind=").Append(node.Kind)
                .Append(" active=").Append(node.IsActive)
                .Append(" visible=").Append(node.IsVisible)
                .Append(" inAnyInputRoot=").Append(inAnyInputRoot)
                .Append(" providerSelectable=").Append(providerSelectable.Contains(node))
                .Append(" canSelect=").Append(canSelect)
                .AppendLine();

            sb.Append(childIndent).Append("transform=").AppendLine(BuildTransformPath(node.Identity?.SelfTransform));

            if (state == null)
            {
                sb.Append(childIndent).AppendLine("uiState=(none)");
            }
            else
            {
                var isSelectable = state.EvaluateIsSelectable();
                var isNavigationSelectable = state.EvaluateIsNavigationSelectable();
                sb.Append(childIndent)
                    .Append("uiState owner=").Append(DescribeNode(state.Owner))
                    .Append(" isActive=").Append(state.IsActive)
                    .Append(" isVisible=").Append(state.IsVisible)
                    .Append(" effectivelyActive=").Append(state.IsEffectivelyActive)
                    .Append(" isSelectable=").Append(isSelectable)
                    .Append(" isNavigationSelectable=").Append(isNavigationSelectable)
                    .Append(" selectionOrder=").Append(state.SelectionOrder)
                    .Append(" navigationSelectionOrder=").Append(state.NavigationSelectionOrder)
                    .AppendLine();

                AppendHitRects(sb, childIndent, state);
            }

            if (_includeGraphicsInDump)
            {
                AppendOwnedGraphics(sb, childIndent, node);
            }

            var scopeChildren = Game.ScopeNodeHierarchy.GetChildrenOrEmpty(node);
            for (int i = 0; i < scopeChildren.Count; i++)
            {
                AppendScopeNodeRecursive(sb, scopeChildren[i], childIndent, i == scopeChildren.Count - 1, visited, providerSelectable);
            }
        }

        void AppendHitRects(StringBuilder sb, string indent, IUIElementState state)
        {
            sb.Append(indent).Append("hitRects=").AppendLine(state.HitTestRects.Count.ToString());
            for (int i = 0; i < state.HitTestRects.Count; i++)
            {
                var rect = state.HitTestRects[i];
                if (rect == null)
                {
                    sb.Append(indent).Append("  [").Append(i).AppendLine("] (null)");
                    continue;
                }

                sb.Append(indent).Append("  [").Append(i).Append("] ")
                    .Append(BuildTransformPath(rect))
                    .Append(" size=").Append(rect.rect.size)
                    .Append(" anchored=").Append(rect.anchoredPosition)
                    .Append(" siblingIndex=").Append(rect.GetSiblingIndex())
                    .AppendLine();
            }
        }

        void AppendOwnedGraphics(StringBuilder sb, string indent, IScopeNode node)
        {
            var graphics = CollectOwnedGraphics(node);
            sb.Append(indent).Append("ownedGraphics=").AppendLine(graphics.Count.ToString());
            for (int i = 0; i < graphics.Count; i++)
            {
                var graphic = graphics[i];
                if (graphic == null)
                    continue;

                var canvas = graphic.canvas;
                var rect = graphic.rectTransform;
                sb.Append(indent).Append("  [").Append(i).Append("] ")
                    .Append(graphic.GetType().Name)
                    .Append(" ").Append(BuildTransformPath(graphic.transform))
                    .Append(" raycastTarget=").Append(graphic.raycastTarget)
                    .Append(" active=").Append(graphic.isActiveAndEnabled)
                    .Append(" culled=").Append(graphic.canvasRenderer != null && graphic.canvasRenderer.cull)
                    .Append(" absDepth=").Append(graphic.canvasRenderer != null ? graphic.canvasRenderer.absoluteDepth : 0)
                    .Append(" size=").Append(rect != null ? rect.rect.size : Vector2.zero)
                    .Append(" canvas=").Append(canvas != null ? canvas.name : "(null)")
                    .Append(" sortingLayer=").Append(canvas != null ? canvas.sortingLayerID : 0)
                    .Append(" sortingOrder=").Append(canvas != null ? canvas.sortingOrder : 0)
                    .AppendLine();
            }
        }

        List<Graphic> CollectOwnedGraphics(IScopeNode node)
        {
            var results = new List<Graphic>();
            var root = node.Identity?.SelfTransform;
            if (root == null)
                return results;

            var stack = new Stack<Transform>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (!ReferenceEquals(current, root) && TryResolveOwnedScope(current, out var ownerScope) && !ReferenceEquals(ownerScope, node))
                    continue;

                var graphics = current.GetComponents<Graphic>();
                for (int i = 0; i < graphics.Length; i++)
                {
                    var graphic = graphics[i];
                    if (graphic != null)
                        results.Add(graphic);
                }

                for (int i = current.childCount - 1; i >= 0; i--)
                {
                    stack.Push(current.GetChild(i));
                }
            }

            return results;
        }

        static string DescribeNode(IScopeNode? node)
        {
            if (node == null)
                return "(null)";

            var transformName = node.Identity?.SelfTransform != null ? node.Identity.SelfTransform.name : "(no-transform)";
            var identityId = node.Identity?.Id ?? "";
            return string.IsNullOrEmpty(identityId)
                ? $"{transformName}<{node.GetType().Name}>"
                : $"{transformName}<{node.GetType().Name}> id={identityId}";
        }

        static string BuildTransformPath(Transform? transform)
        {
            if (transform == null)
                return "(null)";

            var names = new List<string>();
            var current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        static bool TryResolveOwnedUIState(IScopeNode node, out IUIElementState? state)
        {
            state = null;
            var resolver = node.Resolver;
            if (resolver == null || !resolver.TryResolve<IUIElementState>(out var resolved) || resolved == null)
                return false;

            if (!ReferenceEquals(resolved.Owner, node))
                return false;

            state = resolved;
            return true;
        }

        static bool TryResolveOwnedScope(Transform transform, out IScopeNode? owner)
        {
            owner = null;
            if (transform == null)
                return false;

            var runtimeScope = transform.GetComponent<RuntimeLifetimeScope>();
            if (runtimeScope != null && TryResolveOwnedUIState(runtimeScope, out _))
            {
                owner = runtimeScope;
                return true;
            }

            var baseScope = transform.GetComponent<BaseLifetimeScope>();
            if (baseScope != null && TryResolveOwnedUIState(baseScope, out _))
            {
                owner = baseScope;
                return true;
            }

            return false;
        }

        static void EmitDumpLog(string message)
        {
            const int chunkSize = 12000;
            if (string.IsNullOrEmpty(message))
            {
                Debug.Log("[UISelectionMB] DumpSelectableUITree produced an empty message.");
                return;
            }

            for (int offset = 0; offset < message.Length; offset += chunkSize)
            {
                var length = Mathf.Min(chunkSize, message.Length - offset);
                Debug.Log(message.Substring(offset, length));
            }
        }
    }

    // ================================================================
    // UISelectionOptions: UISelectionServiceのオプション設定
    // ================================================================

    /// <summary>
    /// UISelectionServiceのオプション設定。
    /// MBのInspector設定をServiceに渡すために使用。
    /// </summary>
    public sealed class UISelectionOptions
    {
        /// <summary>選択変更のログを出力するか</summary>
        public bool EnableSelectionLogging { get; set; }
    }
}
