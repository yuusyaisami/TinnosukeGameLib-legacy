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
    /// UISelectionServiceをDIコンチE��に登録するFeatureInstaller、E
    /// 
    /// ## 概要E
    /// 
    /// UISelectionServiceは、UIの選択状態を管琁E��る中核サービス、E
    /// 多くのUIシスチE��がこのサービスにアクセスして現在の選択状態を取得する、E
    /// 
    /// ## 主な機�E
    /// 
    /// 1. **選択状態�E管琁E*: 現在選択中のUIElement�E�Eurrent�E�を保持
    /// 2. **ホバー状態�E管琁E*: マウス操作時のホバー対象を保持
    /// 3. **選択履歴**: 前回の選択！Erevious�E�を保持し、フォールバックに使用
    /// 4. **Modal Stackとの連携**: 選択篁E��をCurrentInputRoot冁E��制陁E
    /// 
    /// ## 重要な制紁E
    /// 
    /// - Selectedは常にCurrentInputRoot配下に存在する忁E��がある
    /// - Modal Stackが変更されたとき、E��択�E自動的にクランプされる
    /// 
    /// ## 設計上�E注愁E
    /// 
    /// 実際のSelect処琁E��物琁E��判定）�Eここでは行わなぁE��E
    /// WorldUI/ScreenUIで判定方法が異なるため、Interfaceで刁E��する、E
    /// </summary>
    public sealed class UISelectionMB : MonoBehaviour, IScopeInstaller
    {
        IUISelectionTelemetry? _telemetry;
        IUISelectionNavigation? _navigation;
        IModalStackChannelHubService? _modalStackHub;

        // ----------------------------------------------------------------
        // Inspector設宁E
        // ----------------------------------------------------------------

        [Header("Debug")]
        [Tooltip("選択変更のログを�E力するか")]
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

            if (_telemetry == null || _navigation == null || _modalStackHub == null)
            {
                Debug.LogWarning("[UISelectionMB] DumpSelectableUITree: telemetry or modal stack hub is not ready.");
                return;
            }

            var sb = new StringBuilder(16 * 1024);
            sb.AppendLine("=== UISelection Selectable UI Tree Dump ===");
            sb.Append("Current: ").AppendLine(DescribeNode(_telemetry.Current));
            sb.Append("Previous: ").AppendLine(DescribeNode(_telemetry.Previous));
            sb.Append("Hovered: ").AppendLine(DescribeNode(_telemetry.Hovered));
            sb.Append("SelectionSource: ").AppendLine(_telemetry.LastSelectionSource.ToString());
            sb.Append("CandidateProvider: ").AppendLine(_telemetry.CandidateProvider?.GetType().FullName ?? "(null)");
            sb.Append("CurrentInputRoot: ").AppendLine(ModalStackChannelDebugLabelUtility.DescribeRoot(_modalStackHub.CurrentInputRoot));
            sb.Append("LayerCount: ").AppendLine(_modalStackHub.LayerStates.Count.ToString());
            sb.Append("RootCount: ").AppendLine(_modalStackHub.RootStates.Count.ToString());

            for (int i = 0; i < _modalStackHub.LayerStates.Count; i++)
            {
                var layerState = _modalStackHub.LayerStates[i];
                if (!layerState.InputActive || layerState.ActiveRoot == null)
                    continue;

                sb.Append("  [").Append(i).Append("] LayerKey=").Append(layerState.LayerKey)
                    .Append(" Order=").Append(layerState.Order)
                    .Append(" Visible=").Append(layerState.Visible)
                    .Append(" InputActive=").Append(layerState.InputActive)
                    .Append(" TopOrder=").Append(layerState.IsTopOrderGroup)
                    .Append(" Primary=").Append(layerState.IsPrimaryInOrder)
                    .Append(" SuppressedBy=").Append(string.IsNullOrWhiteSpace(layerState.SuppressedByLayerKey) ? "(none)" : layerState.SuppressedByLayerKey)
                    .Append(" Root=").AppendLine(ModalStackChannelDebugLabelUtility.DescribeRoot(layerState.ActiveRoot));
            }

            var rootScopes = new List<IScopeNode>();
            TryGetActiveRootScopes(rootScopes);

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
        // IFeatureInstaller実裁E
        // ----------------------------------------------------------------

        /// <summary>
        /// UISelectionServiceをDIコンチE��に登録する、E
        /// 
        /// 登録頁E���E注愁E
        /// - UISelectionServiceはIModalStackChannelHubServiceに依存すめE
        /// - ModalStackChannelHubMBが�Eに登録されてぁE��忁E��がある
        /// </summary>
        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            // 選択設定を登録
            builder.RegisterInstance(new UISelectionOptions
            {
                EnableSelectionLogging = _enableSelectionLogging
            });

            // UISelectionServiceを登録
            // - IUISelectionService: 公開インターフェース
            builder.Register<UISelectionService>(RuntimeLifetime.Singleton)
                .As<IUISelectionService>()
                .As<IUISelectionState>()
                .As<IUISelectionNavigation>()
                .As<IUISelectionTelemetry>()
                .As<IUISelectionBlockService>();

            builder.Register<UIInputRoutingHub>(RuntimeLifetime.Singleton)
                .As<IUIInputRoutingHub>();

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

                if (container.TryResolve<IModalStackChannelHubService>(out var modalStackHub))
                    _modalStackHub = modalStackHub;
            });
        }

        bool TryGetActiveRootScopes(List<IScopeNode> results)
        {
            results.Clear();

            if (_modalStackHub == null)
                return false;

            var layerStates = _modalStackHub.LayerStates;
            for (var i = 0; i < layerStates.Count; i++)
            {
                var layerState = layerStates[i];
                if (!layerState.InputActive)
                    continue;

                var scope = layerState.ActiveRoot?.OwnerScope;
                if (scope != null && !results.Contains(scope))
                    results.Add(scope);
            }

            if (results.Count > 0)
                return true;

            var fallback = _modalStackHub.CurrentInputRoot?.OwnerScope;
            if (fallback != null)
            {
                results.Add(fallback);
                return true;
            }

            return false;
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
            var childIndent = indent + (isLast ? "   " : "━E ");

            TryResolveOwnedUIState(node, out var state);
            var canSelect = _navigation != null && _navigation.CanSelect(node);
            var inAnyInputRoot = _modalStackHub != null && _modalStackHub.IsInAnyInputRoot(node);
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

            if (ScopeFeatureInstallerUtility.TryGetScopeNode(transform, includeInactive: true, out var scopeNode) &&
                scopeNode != null &&
                TryResolveOwnedUIState(scopeNode, out _))
            {
                owner = scopeNode;
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
    // UISelectionOptions: UISelectionServiceのオプション設宁E
    // ================================================================

    /// <summary>
    /// UISelectionServiceのオプション設定、E
    /// MBのInspector設定をServiceに渡すために使用、E
    /// </summary>
    public sealed class UISelectionOptions
    {
        /// <summary>選択変更のログを�E力するか</summary>
        public bool EnableSelectionLogging { get; set; }
    }
}

