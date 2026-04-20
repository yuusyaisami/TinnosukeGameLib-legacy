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
    // UISelectionMB: UISelectionService縺ｮFeatureInstaller
    // ================================================================

    /// <summary>
    /// UISelectionService繧奪I繧ｳ繝ｳ繝・リ縺ｫ逋ｻ骭ｲ縺吶ｋFeatureInstaller縲・
    /// 
    /// ## 讎りｦ・
    /// 
    /// UISelectionService縺ｯ縲ゞI縺ｮ驕ｸ謚樒憾諷九ｒ邂｡逅・☆繧倶ｸｭ譬ｸ繧ｵ繝ｼ繝薙せ縲・
    /// 螟壹￥縺ｮUI繧ｷ繧ｹ繝・Β縺後％縺ｮ繧ｵ繝ｼ繝薙せ縺ｫ繧｢繧ｯ繧ｻ繧ｹ縺励※迴ｾ蝨ｨ縺ｮ驕ｸ謚樒憾諷九ｒ蜿門ｾ励☆繧九・
    /// 
    /// ## 荳ｻ縺ｪ讖溯・
    /// 
    /// 1. **驕ｸ謚樒憾諷九・邂｡逅・*: 迴ｾ蝨ｨ驕ｸ謚樔ｸｭ縺ｮUIElement・・urrent・峨ｒ菫晄戟
    /// 2. **繝帙ヰ繝ｼ迥ｶ諷九・邂｡逅・*: 繝槭え繧ｹ謫堺ｽ懈凾縺ｮ繝帙ヰ繝ｼ蟇ｾ雎｡繧剃ｿ晄戟
    /// 3. **驕ｸ謚槫ｱ･豁ｴ**: 蜑榊屓縺ｮ驕ｸ謚橸ｼ・revious・峨ｒ菫晄戟縺励√ヵ繧ｩ繝ｼ繝ｫ繝舌ャ繧ｯ縺ｫ菴ｿ逕ｨ
    /// 4. **Modal Stack縺ｨ縺ｮ騾｣謳ｺ**: 驕ｸ謚樒ｯ・峇繧辰urrentInputRoot蜀・↓蛻ｶ髯・
    /// 
    /// ## 驥崎ｦ√↑蛻ｶ邏・
    /// 
    /// - Selected縺ｯ蟶ｸ縺ｫCurrentInputRoot驟堺ｸ九↓蟄伜惠縺吶ｋ蠢・ｦ√′縺ゅｋ
    /// - Modal Stack縺悟､画峩縺輔ｌ縺溘→縺阪・∈謚槭・閾ｪ蜍慕噪縺ｫ繧ｯ繝ｩ繝ｳ繝励＆繧後ｋ
    /// 
    /// ## 險ｭ險井ｸ翫・豕ｨ諢・
    /// 
    /// 螳滄圀縺ｮSelect蜃ｦ逅・ｼ育黄逅・噪蛻､螳夲ｼ峨・縺薙％縺ｧ縺ｯ陦後ｏ縺ｪ縺・・
    /// WorldUI/ScreenUI縺ｧ蛻､螳壽婿豕輔′逡ｰ縺ｪ繧九◆繧√！nterface縺ｧ蛻・屬縺吶ｋ縲・
    /// </summary>
    public sealed class UISelectionMB : MonoBehaviour, IFeatureInstaller
    {
        IUISelectionTelemetry? _telemetry;
        IUISelectionNavigation? _navigation;
        IModalStackChannelHubService? _modalStackHub;

        // ----------------------------------------------------------------
        // Inspector險ｭ螳・
        // ----------------------------------------------------------------

        [Header("Debug")]
        [Tooltip("驕ｸ謚槫､画峩縺ｮ繝ｭ繧ｰ繧貞・蜉帙☆繧九°")]
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
        // IFeatureInstaller螳溯｣・
        // ----------------------------------------------------------------

        /// <summary>
        /// UISelectionService繧奪I繧ｳ繝ｳ繝・リ縺ｫ逋ｻ骭ｲ縺吶ｋ縲・
        /// 
        /// 逋ｻ骭ｲ鬆・ｺ上・豕ｨ諢・
        /// - UISelectionService縺ｯIModalStackChannelHubService縺ｫ萓晏ｭ倥☆繧・
        /// - ModalStackChannelHubMB縺悟・縺ｫ逋ｻ骭ｲ縺輔ｌ縺ｦ縺・ｋ蠢・ｦ√′縺ゅｋ
        /// </summary>
        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            // 驕ｸ謚櫁ｨｭ螳壹ｒ逋ｻ骭ｲ
            builder.RegisterInstance(new UISelectionOptions
            {
                EnableSelectionLogging = _enableSelectionLogging
            });

            // UISelectionService繧堤匳骭ｲ
            // - IUISelectionService: 蜈ｬ髢九う繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ
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
                sb.Append(indent).Append(isLast ? "笏披楳 " : "笏懌楳 ").Append("(cycle) ").AppendLine(DescribeNode(node));
                return;
            }

            var branch = isLast ? "笏披楳 " : "笏懌楳 ";
            var childIndent = indent + (isLast ? "   " : "笏・ ");

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
    // UISelectionOptions: UISelectionService縺ｮ繧ｪ繝励す繝ｧ繝ｳ險ｭ螳・
    // ================================================================

    /// <summary>
    /// UISelectionService縺ｮ繧ｪ繝励す繝ｧ繝ｳ險ｭ螳壹・
    /// MB縺ｮInspector險ｭ螳壹ｒService縺ｫ貂｡縺吶◆繧√↓菴ｿ逕ｨ縲・
    /// </summary>
    public sealed class UISelectionOptions
    {
        /// <summary>驕ｸ謚槫､画峩縺ｮ繝ｭ繧ｰ繧貞・蜉帙☆繧九°</summary>
        public bool EnableSelectionLogging { get; set; }
    }
}
