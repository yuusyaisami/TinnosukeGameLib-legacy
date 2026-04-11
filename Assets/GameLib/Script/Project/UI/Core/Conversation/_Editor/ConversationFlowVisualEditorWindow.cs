#if UNITY_EDITOR
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Conversation;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Game.Conversation.Editor
{
    public sealed class ConversationFlowVisualEditorWindow : EditorWindow
    {
        const float NodeDragThreshold = 4f;
        const float PortSize = 12f;
        const float NodeSnapStep = 8f;
        const float MinGraphZoom = 0.1f;
        const float MaxGraphZoom = 2.0f;
        const float GraphZoomStep = 0.1f;

        sealed class JointVisualContext
        {
            public ConversationNodeJointPreset Joint = null!;
            public VisualElement Root = null!;
            public VisualElement OutputPort = null!;
        }

        sealed class NodeVisualContext
        {
            public ConversationNodePresetBase Node = null!;
            public ConversationNodeGraphViewPreset NodeView = null!;
            public VisualElement Root = null!;
            public VisualElement Header = null!;
            public Label HeaderLabel = null!;
            public VisualElement Body = null!;
            public VisualElement? InputPort;
            public Dictionary<ConversationNodeJointPreset, JointVisualContext> JointVisuals = new();
        }

        sealed class NodeClipboardData
        {
            public ConversationNodePresetBase Node = null!;
            public Vector2 Position;
            public Vector2 Size;
            public bool IsExpanded;
        }

        readonly struct CharacterDefinitionOption
        {
            public int CharacterId { get; }
            public string Label { get; }

            public CharacterDefinitionOption(int characterId, string label)
            {
                CharacterId = characterId;
                Label = label;
            }
        }

        ConversationFlowPreset? _preset;
        UnityEngine.Object? _owner;
        string _ownerPropertyPath = string.Empty;

        SerializedObject? _ownerSerializedObject;
        PropertyTree? _inspectorPropertyTree;

        TwoPaneSplitView? _splitRoot;
        VisualElement? _graphRoot;
        VisualElement? _graphContentRoot;
        VisualElement? _nodeLayer;
        IMGUIContainer? _wireLayer;

        VisualElement? _inspectorRoot;
        IMGUIContainer? _inspectorGui;
        Vector2 _inspectorScroll;

        Label? _zoomLabel;
        float _graphZoom = 1f;
        Vector2 _graphPan;

        bool _isGraphPanning;
        int _graphPanPointerId = -1;
        Vector2 _graphPanStartMouse;
        Vector2 _graphPanStartOffset;

        bool _loggedFlowPathMismatch;
        bool _needsGraphRebuildFromSerializedSync;
        bool _isGraphRebuildQueued;
        int _lastUnresolvedSelectedNodeId = -1;
        NodeClipboardData? _nodeClipboard;

        readonly Dictionary<int, NodeVisualContext> _nodeVisuals = new();

        bool _isLinkDragging;
        int _linkSourceNodeId;
        ConversationNodeJointPreset? _linkSourceJoint;
        VisualElement? _linkSourcePort;
        Vector2 _lastGraphMouse;

        [MenuItem("Tools/Conversation/Flow Visual Editor")]
        public static void OpenEmpty()
        {
            var window = GetWindow<ConversationFlowVisualEditorWindow>("Conversation Flow Graph");
            window.minSize = new Vector2(1100f, 640f);
            window.Focus();
        }

        public static void Open(ConversationFlowPreset preset, UnityEngine.Object? owner, string ownerPropertyPath = "")
        {
            var window = GetWindow<ConversationFlowVisualEditorWindow>("Conversation Flow Graph");
            window.minSize = new Vector2(1100f, 640f);
            window.Initialize(preset, owner, ownerPropertyPath);
            window.Focus();
        }

        void Initialize(ConversationFlowPreset preset, UnityEngine.Object? owner, string ownerPropertyPath)
        {
            _preset = preset;
            _owner = owner;
            _ownerPropertyPath = ownerPropertyPath ?? string.Empty;
            _ownerSerializedObject = null;
            DisposeInspectorPropertyTree();

            titleContent = new GUIContent(owner != null ? $"Conversation Flow - {owner.name}" : "Conversation Flow");
            if (_graphRoot != null)
                RebuildGraph();

            _inspectorGui?.MarkDirtyRepaint();
        }

        void OnDisable()
        {
            EditorApplication.delayCall -= FlushQueuedGraphRebuild;
            _isGraphRebuildQueued = false;
            DisposeInspectorPropertyTree();
        }

        void OnDestroy()
        {
            EditorApplication.delayCall -= FlushQueuedGraphRebuild;
            _isGraphRebuildQueued = false;
            DisposeInspectorPropertyTree();
        }

        void CreateGUI()
        {
            rootVisualElement.focusable = true;
            rootVisualElement.tabIndex = 0;
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);

            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.style.backgroundColor = new Color(0.09f, 0.10f, 0.12f, 1f);

            rootVisualElement.Add(BuildToolbar());

            _splitRoot = new TwoPaneSplitView(0, 760f, TwoPaneSplitViewOrientation.Horizontal);
            _splitRoot.style.flexGrow = 1f;

            BuildGraphPane();
            BuildInspectorPane();

            if (_graphRoot != null)
                _splitRoot.Add(_graphRoot);
            if (_inspectorRoot != null)
                _splitRoot.Add(_inspectorRoot);

            rootVisualElement.Add(_splitRoot);
            RebuildGraph();
        }

        Toolbar BuildToolbar()
        {
            var toolbar = new Toolbar();

            var addMenu = new ToolbarMenu { text = "Add Node" };
            AppendAddNodeMenu(addMenu.menu, Vector2.zero);
            toolbar.Add(addMenu);

            var rebuildLinksButton = new ToolbarButton(() =>
            {
                if (_preset == null)
                    return;

                RecordOwnerUndo("Rebuild Previous Links");
                _preset.RebuildPreviousLinks();
                MarkOwnerDirty();
                RebuildGraph();
            })
            {
                text = "Rebuild Links"
            };
            toolbar.Add(rebuildLinksButton);

            var refreshButton = new ToolbarButton(RebuildGraph)
            {
                text = "Refresh"
            };
            toolbar.Add(refreshButton);

            var saveButton = new ToolbarButton(() =>
            {
                MarkOwnerDirty();
                AssetDatabase.SaveAssets();
            })
            {
                text = "Save"
            };
            toolbar.Add(saveButton);

            var separator = new VisualElement();
            separator.style.width = 1f;
            separator.style.height = 18f;
            separator.style.marginLeft = 4f;
            separator.style.marginRight = 4f;
            separator.style.backgroundColor = new Color(0.30f, 0.34f, 0.40f, 0.8f);
            toolbar.Add(separator);

            var zoomOutButton = new ToolbarButton(() => SetGraphZoom(_graphZoom - GraphZoomStep))
            {
                text = "-"
            };
            toolbar.Add(zoomOutButton);

            _zoomLabel = new Label();
            _zoomLabel.style.minWidth = 52f;
            _zoomLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            toolbar.Add(_zoomLabel);

            var zoomInButton = new ToolbarButton(() => SetGraphZoom(_graphZoom + GraphZoomStep))
            {
                text = "+"
            };
            toolbar.Add(zoomInButton);

            var resetZoomButton = new ToolbarButton(() => SetGraphZoom(1f))
            {
                text = "100%"
            };
            toolbar.Add(resetZoomButton);

            toolbar.Add(new ToolbarSpacer());
            var hint = new Label("Right click: add/delete/change | Drag node: move | Drag output port: connect | Empty drag / Middle drag: pan | Click node: edit in right inspector");
            hint.style.unityFontStyleAndWeight = FontStyle.Italic;
            hint.style.color = new Color(0.72f, 0.76f, 0.82f, 1f);
            toolbar.Add(hint);

            UpdateZoomLabel();

            return toolbar;
        }

        void BuildGraphPane()
        {
            _graphRoot = new VisualElement();
            _graphRoot.focusable = true;
            _graphRoot.tabIndex = 0;
            _graphRoot.style.flexGrow = 1f;
            _graphRoot.style.position = Position.Relative;
            _graphRoot.style.backgroundColor = new Color(0.11f, 0.12f, 0.14f, 1f);
            _graphRoot.RegisterCallback<PointerMoveEvent>(OnGraphPointerMove);
            _graphRoot.RegisterCallback<PointerUpEvent>(OnGraphPointerUp);
            _graphRoot.RegisterCallback<PointerDownEvent>(OnGraphPointerDown);
            _graphRoot.RegisterCallback<WheelEvent>(OnGraphWheel);
            _graphRoot.AddManipulator(new ContextualMenuManipulator(BuildGraphContextMenu));

            _graphContentRoot = new VisualElement();
            _graphContentRoot.style.position = Position.Absolute;
            _graphContentRoot.style.left = 0f;
            _graphContentRoot.style.top = 0f;
            _graphContentRoot.style.width = Length.Percent(100f);
            _graphContentRoot.style.height = Length.Percent(100f);
            _graphRoot.Add(_graphContentRoot);

            _nodeLayer = new VisualElement();
            _nodeLayer.style.position = Position.Absolute;
            _nodeLayer.style.left = 0f;
            _nodeLayer.style.top = 0f;
            _nodeLayer.style.right = 0f;
            _nodeLayer.style.bottom = 0f;
            _graphContentRoot.Add(_nodeLayer);

            _wireLayer = new IMGUIContainer(DrawWires);
            _wireLayer.style.position = Position.Absolute;
            _wireLayer.style.left = 0f;
            _wireLayer.style.top = 0f;
            _wireLayer.style.right = 0f;
            _wireLayer.style.bottom = 0f;
            _wireLayer.pickingMode = PickingMode.Ignore;
            _graphContentRoot.Add(_wireLayer);

            ApplyGraphZoom();
        }

        void BuildInspectorPane()
        {
            _inspectorRoot = new VisualElement();
            _inspectorRoot.style.backgroundColor = new Color(0.15f, 0.16f, 0.18f, 1f);
            _inspectorRoot.style.borderLeftWidth = 1f;
            _inspectorRoot.style.borderLeftColor = new Color(0.24f, 0.26f, 0.30f, 1f);
            _inspectorRoot.style.flexGrow = 1f;

            var title = new Label("Node Settings");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14;
            title.style.color = new Color(0.95f, 0.98f, 1f, 1f);
            title.style.marginLeft = 8f;
            title.style.marginTop = 8f;
            _inspectorRoot.Add(title);

            _inspectorGui = new IMGUIContainer(DrawInspectorPanel);
            _inspectorGui.style.flexGrow = 1f;
            _inspectorGui.style.marginLeft = 4f;
            _inspectorGui.style.marginRight = 4f;
            _inspectorRoot.Add(_inspectorGui);
        }

        void RebuildGraph()
        {
            if (_preset == null || _nodeLayer == null)
                return;

            _preset.EnsureStartNode();
            _preset.RebuildPreviousLinks();

            _nodeVisuals.Clear();
            _nodeLayer.Clear();
            CancelLinkDrag();

            var nodes = _preset.EditorNodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                var nodeView = _preset.GetOrCreateNodeView(node.NodeId);
                if (nodeView.Position == Vector2.zero)
                    nodeView.SetPosition(new Vector2(52f + (i * 40f), 56f + (i * 26f)));

                var context = BuildNodeVisual(node, nodeView);
                _nodeVisuals[node.NodeId] = context;
                _nodeLayer.Add(context.Root);
            }

            _wireLayer?.MarkDirtyRepaint();
            _inspectorGui?.MarkDirtyRepaint();
        }

        NodeVisualContext BuildNodeVisual(ConversationNodePresetBase node, ConversationNodeGraphViewPreset nodeView)
        {
            var context = new NodeVisualContext
            {
                Node = node,
                NodeView = nodeView,
            };

            var root = new VisualElement();
            root.style.position = Position.Absolute;
            root.style.left = nodeView.Position.x;
            root.style.top = nodeView.Position.y;
            root.style.width = nodeView.Size.x;
            root.style.backgroundColor = node.IsStartNode
                ? new Color(0.18f, 0.21f, 0.18f, 1f)
                : new Color(0.16f, 0.18f, 0.22f, 1f);
            root.style.borderTopLeftRadius = 8f;
            root.style.borderTopRightRadius = 8f;
            root.style.borderBottomLeftRadius = 8f;
            root.style.borderBottomRightRadius = 8f;
            root.style.borderLeftWidth = 2f;
            root.style.borderRightWidth = 2f;
            root.style.borderTopWidth = 2f;
            root.style.borderBottomWidth = 2f;
            root.style.borderLeftColor = new Color(0.20f, 0.24f, 0.30f, 1f);
            root.style.borderRightColor = new Color(0.20f, 0.24f, 0.30f, 1f);
            root.style.borderTopColor = new Color(0.20f, 0.24f, 0.30f, 1f);
            root.style.borderBottomColor = new Color(0.20f, 0.24f, 0.30f, 1f);
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 6f;
            root.style.paddingBottom = 8f;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 6f;

            VisualElement? inputPort = null;
            if (!node.IsStartNode)
            {
                inputPort = CreatePortElement(isInput: true);
                inputPort.style.marginRight = 6f;
                inputPort.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (!_isLinkDragging || _linkSourceJoint == null || context.Node.NodeId == _linkSourceNodeId)
                        return;

                    CommitJointConnection(context.Node.NodeId);
                    evt.StopPropagation();
                });
                inputPort.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                headerRow.Add(inputPort);
            }
            else
            {
                var spacer = new VisualElement();
                spacer.style.width = PortSize;
                spacer.style.height = PortSize;
                spacer.style.marginRight = 6f;
                headerRow.Add(spacer);
            }

            var header = new Label($"{node.NodeId}: {node.DebugViewText}");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new Color(0.93f, 0.96f, 1f, 1f);
            header.style.flexGrow = 1f;
            headerRow.Add(header);

            if (node.IsStartNode)
            {
                var startBadge = new Label("START");
                startBadge.style.color = new Color(0.70f, 1f, 0.70f, 1f);
                startBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
                startBadge.style.fontSize = 10;
                headerRow.Add(startBadge);
            }

            root.Add(headerRow);

            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Column;

            var preview = new Label(BuildNodePreview(node));
            preview.style.color = new Color(0.76f, 0.82f, 0.90f, 1f);
            preview.style.marginBottom = 6f;
            body.Add(preview);

            var jointContainer = new VisualElement();
            jointContainer.style.flexDirection = FlexDirection.Column;
            jointContainer.style.marginTop = 2f;
            body.Add(jointContainer);

            var joints = node.NextNodeJoints;
            for (var i = 0; i < joints.Count; i++)
            {
                var joint = joints[i];
                if (joint == null)
                    continue;

                var jointVisual = BuildJointChip(context, joint);
                context.JointVisuals[joint] = jointVisual;
                jointContainer.Add(jointVisual.Root);
            }

            root.Add(body);

            context.Root = root;
            context.Header = headerRow;
            context.HeaderLabel = header;
            context.Body = body;
            context.InputPort = inputPort;

            ApplyNodeSelectionStyle(context, _preset != null && node.NodeId == _preset.GraphState.SelectedNodeId);
            ApplyNodeExpandedState(context, nodeView.IsExpanded);
            RegisterNodePointerHandlers(context);
            root.AddManipulator(new ContextualMenuManipulator(evt => BuildNodeContextMenu(evt, context)));
            return context;
        }

        JointVisualContext BuildJointChip(NodeVisualContext context, ConversationNodeJointPreset joint)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.paddingLeft = 8f;
            row.style.paddingRight = 8f;
            row.style.paddingTop = 3f;
            row.style.paddingBottom = 3f;
            row.style.marginBottom = 3f;
            row.style.backgroundColor = new Color(0.20f, 0.24f, 0.30f, 0.95f);
            row.style.borderTopLeftRadius = 10f;
            row.style.borderTopRightRadius = 10f;
            row.style.borderBottomLeftRadius = 10f;
            row.style.borderBottomRightRadius = 10f;
            row.style.borderLeftWidth = 1f;
            row.style.borderRightWidth = 1f;
            row.style.borderTopWidth = 1f;
            row.style.borderBottomWidth = 1f;
            row.style.borderLeftColor = new Color(0.33f, 0.40f, 0.50f, 1f);
            row.style.borderRightColor = new Color(0.33f, 0.40f, 0.50f, 1f);
            row.style.borderTopColor = new Color(0.33f, 0.40f, 0.50f, 1f);
            row.style.borderBottomColor = new Color(0.33f, 0.40f, 0.50f, 1f);

            var title = string.IsNullOrWhiteSpace(joint.DebugText)
                ? joint.JointName
                : $"{joint.JointName} ({joint.DebugText})";

            var label = new Label(title);
            label.style.color = new Color(0.83f, 0.88f, 0.95f, 1f);
            label.style.flexGrow = 1f;
            row.Add(label);

            var targetLabel = new Label(joint.SelectedNextNodeId > 0 ? $"-> {joint.SelectedNextNodeId}" : "-> End");
            targetLabel.style.color = new Color(0.60f, 0.90f, 0.74f, 1f);
            targetLabel.style.marginRight = 8f;
            row.Add(targetLabel);

            var outputPort = CreatePortElement(isInput: false);
            outputPort.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                StartLinkDrag(context.Node.NodeId, joint, outputPort);
                evt.StopPropagation();
            });
            row.Add(outputPort);

            row.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Disconnect", _ =>
                {
                    RecordOwnerUndo("Disconnect Joint");
                    joint.SetSelectedNextNodeId(0);
                    _preset?.RebuildPreviousLinks();
                    MarkOwnerDirty();
                    RebuildGraph();
                });

                if (context.Node.SupportsDynamicJoints)
                {
                    evt.menu.AppendAction("Remove Joint", _ =>
                    {
                        RecordOwnerUndo("Remove Joint");
                        if (context.Node.RemoveJoint(joint))
                        {
                            _preset?.RebuildPreviousLinks();
                            MarkOwnerDirty();
                            RebuildGraph();
                        }
                    });
                }
            }));

            return new JointVisualContext
            {
                Joint = joint,
                Root = row,
                OutputPort = outputPort,
            };
        }

        static VisualElement CreatePortElement(bool isInput)
        {
            var port = new VisualElement();
            port.style.width = PortSize;
            port.style.height = PortSize;
            port.style.borderTopLeftRadius = PortSize * 0.5f;
            port.style.borderTopRightRadius = PortSize * 0.5f;
            port.style.borderBottomLeftRadius = PortSize * 0.5f;
            port.style.borderBottomRightRadius = PortSize * 0.5f;
            port.style.borderLeftWidth = 1f;
            port.style.borderRightWidth = 1f;
            port.style.borderTopWidth = 1f;
            port.style.borderBottomWidth = 1f;
            port.style.borderLeftColor = new Color(0.78f, 0.84f, 0.95f, 1f);
            port.style.borderRightColor = new Color(0.78f, 0.84f, 0.95f, 1f);
            port.style.borderTopColor = new Color(0.78f, 0.84f, 0.95f, 1f);
            port.style.borderBottomColor = new Color(0.78f, 0.84f, 0.95f, 1f);
            port.style.backgroundColor = isInput
                ? new Color(0.34f, 0.76f, 0.96f, 1f)
                : new Color(0.52f, 0.89f, 0.66f, 1f);
            return port;
        }

        static string BuildNodePreview(ConversationNodePresetBase node)
        {
            switch (node)
            {
                case ConversationStartNodePreset:
                    return "Flow entry point";

                case ConversationMessageNodePreset messageNode:
                    {
                        var speaker = messageNode.CharacterDataId > 0
                            ? $"CharacterId: {messageNode.CharacterDataId}"
                            : "Speaker: (none)";
                        return speaker;
                    }

                case ConversationChoiceNodePreset choiceNode:
                    return $"Choices: {choiceNode.ChoiceRequest?.GridChoiceRequest?.Entries?.Count ?? 0}";

                case ConversationIfNodePreset:
                    return "Conditional branch";

                case ConversationSwitchNodePreset switchNode:
                    return $"Switch cases: {switchNode.Cases.Count}";

                case ConversationJumpNodePreset:
                    return "Jump to connected node";

                case ConversationCommandOnlyNodePreset:
                    return "Command-only step";

                default:
                    return node.GetType().Name;
            }
        }

        void RegisterNodePointerHandlers(NodeVisualContext context)
        {
            var root = context.Root;
            Vector2 dragStartMouse = Vector2.zero;
            Vector2 dragStartNode = Vector2.zero;
            var dragging = false;
            var pointerId = -1;

            root.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                FocusGraph();
                SelectNode(context.Node.NodeId);
                dragStartMouse = (Vector2)evt.position;
                dragStartNode = context.NodeView.Position;
                dragging = false;
                pointerId = evt.pointerId;
                root.CapturePointer(pointerId);
                evt.StopPropagation();
            });

            root.RegisterCallback<PointerMoveEvent>(evt =>
            {
                UpdateLastGraphMouse(evt.position);

                if (pointerId < 0 || !root.HasPointerCapture(pointerId))
                    return;

                var currentMouse = (Vector2)evt.position;
                var delta = currentMouse - dragStartMouse;
                if (!dragging && delta.magnitude >= NodeDragThreshold)
                    dragging = true;

                if (!dragging)
                    return;

                var zoom = Mathf.Max(MinGraphZoom, _graphZoom);
                var newPos = dragStartNode + (delta / zoom);
                newPos = SnapPosition(newPos);
                context.NodeView.SetPosition(newPos);
                root.style.left = newPos.x;
                root.style.top = newPos.y;
                MarkOwnerDirty();
                _wireLayer?.MarkDirtyRepaint();
                evt.StopPropagation();
            });

            root.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_isLinkDragging && _linkSourceJoint != null && context.Node.NodeId != _linkSourceNodeId)
                {
                    CommitJointConnection(context.Node.NodeId);
                    evt.StopPropagation();
                    return;
                }

                if (pointerId < 0 || !root.HasPointerCapture(pointerId))
                    return;

                root.ReleasePointer(pointerId);
                pointerId = -1;
                dragging = false;
                _wireLayer?.MarkDirtyRepaint();
                evt.StopPropagation();
            });
        }

        void BuildGraphContextMenu(ContextualMenuPopulateEvent evt)
        {
            var position = ToGraphSpace(evt.localMousePosition);
            AppendAddNodeMenu(evt.menu, position);

            if (_nodeClipboard != null && _nodeClipboard.Node != null)
            {
                evt.menu.AppendAction("Node/Paste", _ => PasteClipboardNode(position), _ => DropdownMenuAction.Status.Normal);
            }
        }

        void AppendAddNodeMenu(DropdownMenu menu, Vector2 graphPosition)
        {
            AppendNodeAddAction(menu, "Add/Start", typeof(ConversationStartNodePreset), graphPosition, !HasStartNode());
            AppendNodeAddAction(menu, "Add/Message", typeof(ConversationMessageNodePreset), graphPosition, true);
            AppendNodeAddAction(menu, "Add/Choice", typeof(ConversationChoiceNodePreset), graphPosition, true);
            AppendNodeAddAction(menu, "Add/If", typeof(ConversationIfNodePreset), graphPosition, true);
            AppendNodeAddAction(menu, "Add/Jump", typeof(ConversationJumpNodePreset), graphPosition, true);
            AppendNodeAddAction(menu, "Add/Switch", typeof(ConversationSwitchNodePreset), graphPosition, true);
            AppendNodeAddAction(menu, "Add/CommandOnly", typeof(ConversationCommandOnlyNodePreset), graphPosition, true);
        }

        void AppendNodeAddAction(DropdownMenu menu, string label, Type nodeType, Vector2 graphPosition, bool enabled)
        {
            menu.AppendAction(label, _ => AddNode(nodeType, graphPosition),
                _ => enabled ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        bool HasStartNode()
        {
            if (_preset == null)
                return false;

            var nodes = _preset.EditorNodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is ConversationStartNodePreset)
                    return true;
            }

            return false;
        }

        void BuildNodeContextMenu(ContextualMenuPopulateEvent evt, NodeVisualContext context)
        {
            evt.menu.AppendAction("Node/Delete", _ => DeleteNode(context.Node.NodeId),
                _ => context.Node.IsStartNode ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);

            evt.menu.AppendAction("Node/Copy", _ => CopySelectedNodeToClipboard(context.Node.NodeId),
                _ => context.Node.IsStartNode ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);

            evt.menu.AppendAction("Node/Duplicate", _ => DuplicateNode(context.Node.NodeId),
                _ => context.Node.IsStartNode ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);

            evt.menu.AppendAction("Node/Paste", _ => PasteClipboardNode(context.NodeView.Position + new Vector2(40f, 24f)),
                _ => _nodeClipboard != null && _nodeClipboard.Node != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            evt.menu.AppendAction("Node/Toggle Expanded", _ =>
            {
                RecordOwnerUndo("Toggle Node Expanded");
                context.NodeView.SetExpanded(!context.NodeView.IsExpanded);
                ApplyNodeExpandedState(context, context.NodeView.IsExpanded);
                MarkOwnerDirty();
                _wireLayer?.MarkDirtyRepaint();
            });

            evt.menu.AppendAction("Node/Add Dynamic Joint", _ =>
            {
                if (!context.Node.SupportsDynamicJoints)
                    return;

                RecordOwnerUndo("Add Dynamic Joint");
                context.Node.AddDynamicJoint();
                _preset?.RebuildPreviousLinks();
                MarkOwnerDirty();
                RebuildGraph();
            },
            context.Node.SupportsDynamicJoints ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);

            AppendNodeChangeTypeAction(evt.menu, "Node/Change Type/Message", context, typeof(ConversationMessageNodePreset));
            AppendNodeChangeTypeAction(evt.menu, "Node/Change Type/Choice", context, typeof(ConversationChoiceNodePreset));
            AppendNodeChangeTypeAction(evt.menu, "Node/Change Type/If", context, typeof(ConversationIfNodePreset));
            AppendNodeChangeTypeAction(evt.menu, "Node/Change Type/Jump", context, typeof(ConversationJumpNodePreset));
            AppendNodeChangeTypeAction(evt.menu, "Node/Change Type/Switch", context, typeof(ConversationSwitchNodePreset));
            AppendNodeChangeTypeAction(evt.menu, "Node/Change Type/CommandOnly", context, typeof(ConversationCommandOnlyNodePreset));
        }

        void AppendNodeChangeTypeAction(DropdownMenu menu, string label, NodeVisualContext context, Type nodeType)
        {
            menu.AppendAction(label, _ => ChangeNodeType(context.Node, nodeType), _ =>
            {
                if (context.Node.IsStartNode)
                    return DropdownMenuAction.Status.Disabled;
                return DropdownMenuAction.Status.Normal;
            });
        }

        void AddNode(Type nodeType, Vector2 graphPosition)
        {
            if (_preset == null)
                return;

            if (nodeType == typeof(ConversationStartNodePreset) && HasStartNode())
                return;

            if (Activator.CreateInstance(nodeType) is not ConversationNodePresetBase node)
                return;

            RecordOwnerUndo("Add Conversation Node");

            var nodeId = _preset.GenerateNextNodeId();
            node.SetNodeId(nodeId);
            _preset.AddNode(node);
            _preset.EnsureStartNode();

            var nodeView = _preset.GetOrCreateNodeView(nodeId);
            nodeView.SetPosition(SnapPosition(graphPosition));

            _preset.RebuildPreviousLinks();
            MarkOwnerDirty();
            RebuildGraph();
            SelectNode(nodeId);
        }

        void DeleteNode(int nodeId)
        {
            if (_preset == null)
                return;

            if (_preset.TryGetNode(nodeId, out var currentNode) && currentNode != null && currentNode.IsStartNode)
                return;

            RecordOwnerUndo("Delete Conversation Node");

            var nodes = _preset.EditorNodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                var joints = node.NextNodeJoints;
                for (var j = 0; j < joints.Count; j++)
                {
                    var joint = joints[j];
                    if (joint == null)
                        continue;

                    joint.RemoveNextCandidate(nodeId);
                }
            }

            var removed = _preset.RemoveNode(nodeId);
            if (!removed)
                return;

            _preset.EnsureStartNode();
            _preset.RebuildPreviousLinks();
            MarkOwnerDirty();
            RebuildGraph();
            SelectNode(_preset.EntryNodeId);
        }

        void ChangeNodeType(ConversationNodePresetBase source, Type targetType)
        {
            if (_preset == null || source == null)
                return;

            if (source.IsStartNode)
                return;

            if (source.GetType() == targetType)
                return;

            if (Activator.CreateInstance(targetType) is not ConversationNodePresetBase replacement)
                return;

            RecordOwnerUndo("Change Conversation Node Type");

            replacement.SetNodeId(source.NodeId);
            replacement.CopySharedFieldsFrom(source);
            CopyJointLinks(source, replacement);
            _preset.ReplaceNode(source.NodeId, replacement);
            _preset.EnsureStartNode();
            _preset.RebuildPreviousLinks();
            MarkOwnerDirty();
            RebuildGraph();
            SelectNode(replacement.NodeId);
        }

        static void CopyJointLinks(ConversationNodePresetBase source, ConversationNodePresetBase target)
        {
            var sourceJoints = source.NextNodeJoints;
            if (sourceJoints == null || sourceJoints.Count == 0)
                return;

            if (target.SupportsDynamicJoints)
            {
                while (target.NextNodeJoints.Count < sourceJoints.Count)
                {
                    if (target.AddDynamicJoint() == null)
                        break;
                }
            }

            var targetJoints = target.NextNodeJoints;
            var copyCount = Mathf.Min(sourceJoints.Count, targetJoints.Count);
            for (var i = 0; i < copyCount; i++)
            {
                var sourceJoint = sourceJoints[i];
                var targetJoint = targetJoints[i];
                if (sourceJoint == null || targetJoint == null)
                    continue;

                var candidates = sourceJoint.NextNodeCandidates;
                for (var c = 0; c < candidates.Count; c++)
                    targetJoint.AddNextCandidate(candidates[c]);

                targetJoint.SetSelectedNextNodeId(sourceJoint.SelectedNextNodeId);
                targetJoint.SetJointName(sourceJoint.JointName);
                targetJoint.SetDebugText(sourceJoint.DebugText);
            }
        }

        void StartLinkDrag(int sourceNodeId, ConversationNodeJointPreset joint, VisualElement sourcePort)
        {
            _isLinkDragging = true;
            _linkSourceNodeId = sourceNodeId;
            _linkSourceJoint = joint;
            _linkSourcePort = sourcePort;
            _wireLayer?.MarkDirtyRepaint();
        }

        void CommitJointConnection(int targetNodeId)
        {
            if (!_isLinkDragging || _linkSourceJoint == null || _preset == null)
                return;

            if (_preset.TryGetNode(targetNodeId, out var targetNode) && targetNode != null && targetNode.IsStartNode)
            {
                Debug.LogWarning($"[ConversationFlow] Start node ({targetNodeId}) cannot have incoming links. Connection was ignored.");
                CancelLinkDrag();
                return;
            }

            RecordOwnerUndo("Connect Conversation Joint");
            _linkSourceJoint.AddNextCandidate(targetNodeId);
            _linkSourceJoint.SetSelectedNextNodeId(targetNodeId);
            _preset.RebuildPreviousLinks();
            MarkOwnerDirty();
            CancelLinkDrag();
            RebuildGraph();
        }

        void CancelLinkDrag()
        {
            _isLinkDragging = false;
            _linkSourceNodeId = 0;
            _linkSourceJoint = null;
            _linkSourcePort = null;
            _wireLayer?.MarkDirtyRepaint();
        }

        void SelectNode(int nodeId)
        {
            if (_preset == null)
                return;

            _preset.GraphState.SetSelectedNodeId(nodeId);
            foreach (var pair in _nodeVisuals)
                ApplyNodeSelectionStyle(pair.Value, pair.Key == nodeId);

            MarkOwnerDirty();
            _inspectorGui?.MarkDirtyRepaint();
        }

        static void ApplyNodeSelectionStyle(NodeVisualContext context, bool selected)
        {
            var color = selected
                ? new Color(0.30f, 0.56f, 0.98f, 1f)
                : new Color(0.20f, 0.24f, 0.30f, 1f);

            context.Root.style.borderLeftColor = color;
            context.Root.style.borderRightColor = color;
            context.Root.style.borderTopColor = color;
            context.Root.style.borderBottomColor = color;
        }

        static void ApplyNodeExpandedState(NodeVisualContext context, bool expanded)
        {
            context.Body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void OnGraphPointerMove(PointerMoveEvent evt)
        {
            UpdateLastGraphMouse(evt.position);

            if (_isGraphPanning && _graphRoot != null && _graphPanPointerId >= 0 && _graphRoot.HasPointerCapture(_graphPanPointerId))
            {
                var current = (Vector2)evt.position;
                var delta = current - _graphPanStartMouse;
                _graphPan = _graphPanStartOffset + delta;
                ApplyGraphZoom();
                _wireLayer?.MarkDirtyRepaint();
                evt.StopPropagation();
                return;
            }

            if (_isLinkDragging)
                _wireLayer?.MarkDirtyRepaint();
        }

        void OnGraphPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 2)
            {
                FocusGraph();
                BeginGraphPan(evt);
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
                return;

            if (!IsBackgroundPointerDown(evt))
                return;

            FocusGraph();
            ClearNodeSelection();
            BeginGraphPan(evt);
            evt.StopPropagation();
        }

        void OnGraphPointerUp(PointerUpEvent evt)
        {
            UpdateLastGraphMouse(evt.position);

            if (_isGraphPanning && evt.pointerId == _graphPanPointerId)
            {
                EndGraphPan();
                evt.StopPropagation();
            }

            if (_isLinkDragging && evt.button == 0)
                CancelLinkDrag();
        }

        void OnGraphWheel(WheelEvent evt)
        {
            var direction = evt.delta.y < 0f ? 1f : -1f;
            SetGraphZoom(_graphZoom + (direction * GraphZoomStep), evt.localMousePosition);
            evt.StopPropagation();
        }

        void OnRootKeyDown(KeyDownEvent evt)
        {
            if (!IsGraphShortcutContext())
                return;

            if (EditorGUIUtility.editingTextField)
                return;

            var handled = false;
            if (evt.actionKey && evt.keyCode == KeyCode.C)
                handled = CopySelectedNodeToClipboard();
            else if (evt.actionKey && evt.keyCode == KeyCode.V)
                handled = PasteClipboardNode();
            else if (evt.actionKey && evt.keyCode == KeyCode.D)
                handled = DuplicateSelectedNode();
            else if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
                handled = DeleteSelectedNode();

            if (!handled)
                return;

            evt.StopPropagation();
        }

        void FocusGraph()
        {
            _graphRoot?.Focus();
        }

        bool IsGraphShortcutContext()
        {
            if (_graphRoot == null || rootVisualElement.panel == null)
                return false;

            var focusedElement = rootVisualElement.panel.focusController.focusedElement as VisualElement;
            if (focusedElement == null)
                return false;

            return focusedElement == _graphRoot || _graphRoot.Contains(focusedElement);
        }

        bool DeleteSelectedNode()
        {
            if (_preset == null)
                return false;

            var selectedNodeId = _preset.GraphState.SelectedNodeId;
            if (selectedNodeId <= 0)
                return false;

            if (!_preset.TryGetNode(selectedNodeId, out var selectedNode) || selectedNode == null || selectedNode.IsStartNode)
                return false;

            DeleteNode(selectedNodeId);
            return true;
        }

        bool CopySelectedNodeToClipboard(int nodeId = 0)
        {
            if (_preset == null)
                return false;

            if (nodeId <= 0)
                nodeId = _preset.GraphState.SelectedNodeId;

            if (nodeId <= 0)
                return false;

            if (!_preset.TryGetNode(nodeId, out var node) || node == null || node.IsStartNode)
                return false;

            var nodeView = _preset.GetOrCreateNodeView(nodeId);
            _nodeClipboard = new NodeClipboardData
            {
                Node = node.CreateRuntimeCopy(),
                Position = nodeView.Position,
                Size = nodeView.Size,
                IsExpanded = nodeView.IsExpanded,
            };

            return true;
        }

        bool DuplicateNode(int nodeId)
        {
            if (_preset == null)
                return false;

            if (nodeId <= 0)
                return false;

            if (!_preset.TryGetNode(nodeId, out var selectedNode) || selectedNode == null || selectedNode.IsStartNode)
                return false;

            if (!CopySelectedNodeToClipboard(nodeId))
                return false;

            var selectedNodeView = _preset.GetOrCreateNodeView(nodeId);
            return PasteClipboardNode(selectedNodeView.Position + new Vector2(40f, 24f), "Duplicate Conversation Node");
        }

        bool DuplicateSelectedNode()
        {
            if (_preset == null)
                return false;

            return DuplicateNode(_preset.GraphState.SelectedNodeId);
        }

        bool PasteClipboardNode()
        {
            return PasteClipboardNode(null, "Paste Conversation Node");
        }

        bool PasteClipboardNode(Vector2? graphPosition)
        {
            return PasteClipboardNode(graphPosition, "Paste Conversation Node");
        }

        bool PasteClipboardNode(Vector2? graphPosition, string undoLabel)
        {
            if (_preset == null || _nodeClipboard == null || _nodeClipboard.Node == null)
                return false;

            var node = _nodeClipboard.Node.CreateRuntimeCopy();
            var nodeId = _preset.GenerateNextNodeId();
            node.SetNodeId(nodeId);

            var position = graphPosition ?? _lastGraphMouse;

            RecordOwnerUndo(undoLabel);
            _preset.AddNode(node);
            _preset.EnsureStartNode();

            var nodeView = _preset.GetOrCreateNodeView(nodeId);
            nodeView.SetPosition(SnapPosition(position));
            nodeView.SetSize(_nodeClipboard.Size);
            nodeView.SetExpanded(_nodeClipboard.IsExpanded);

            _preset.RebuildPreviousLinks();
            MarkOwnerDirty();
            RebuildGraph();
            SelectNode(nodeId);
            return true;
        }

        void SetGraphZoom(float zoom, Vector2? mouseLocalPosition = null)
        {
            var clamped = Mathf.Clamp(zoom, MinGraphZoom, MaxGraphZoom);
            if (Mathf.Approximately(_graphZoom, clamped))
                return;

            var previousZoom = Mathf.Max(MinGraphZoom, _graphZoom);
            if (_graphRoot != null && mouseLocalPosition.HasValue)
            {
                var pivotOffset = ResolveZoomPivot(mouseLocalPosition.Value);
                var graphPivot = (pivotOffset - _graphPan) / previousZoom;

                _graphZoom = clamped;
                _graphPan = pivotOffset - (graphPivot * _graphZoom);
            }
            else
            {
                _graphZoom = clamped;
            }

            ApplyGraphZoom();
            _wireLayer?.MarkDirtyRepaint();
        }

        Vector2 ResolveZoomPivot(Vector2 mouseLocalPosition)
        {
            if (_graphRoot == null)
                return mouseLocalPosition;

            var rect = _graphRoot.contentRect;
            return mouseLocalPosition - rect.center;
        }

        void ApplyGraphZoom()
        {
            if (_graphContentRoot != null)
            {
                _graphContentRoot.style.left = _graphPan.x;
                _graphContentRoot.style.top = _graphPan.y;
                _graphContentRoot.style.scale = new StyleScale(new Scale(new Vector3(_graphZoom, _graphZoom, 1f)));
            }

            UpdateZoomLabel();
        }

        void UpdateZoomLabel()
        {
            if (_zoomLabel == null)
                return;

            _zoomLabel.text = $"{Mathf.RoundToInt(_graphZoom * 100f)}%";
        }

        void ClearNodeSelection()
        {
            if (_preset == null)
                return;

            if (_preset.GraphState.SelectedNodeId == 0)
                return;

            FocusGraph();
            _preset.GraphState.SetSelectedNodeId(0);
            foreach (var pair in _nodeVisuals)
                ApplyNodeSelectionStyle(pair.Value, false);

            MarkOwnerDirty();
            _inspectorGui?.MarkDirtyRepaint();
        }

        Vector2 ToGraphSpace(Vector2 rootLocalPosition)
        {
            var zoom = Mathf.Max(MinGraphZoom, _graphZoom);
            return (rootLocalPosition - _graphPan) / zoom;
        }

        static Vector2 SnapPosition(Vector2 position)
        {
            var snappedX = Mathf.Round(position.x / NodeSnapStep) * NodeSnapStep;
            var snappedY = Mathf.Round(position.y / NodeSnapStep) * NodeSnapStep;
            return new Vector2(snappedX, snappedY);
        }

        void UpdateLastGraphMouse(Vector2 worldPosition)
        {
            if (_wireLayer == null)
            {
                _lastGraphMouse = worldPosition;
                return;
            }

            _lastGraphMouse = _wireLayer.WorldToLocal(worldPosition);
        }

        void BeginGraphPan(PointerDownEvent evt)
        {
            if (_graphRoot == null)
                return;

            EndGraphPan();
            _isGraphPanning = true;
            _graphPanPointerId = evt.pointerId;
            _graphPanStartMouse = (Vector2)evt.position;
            _graphPanStartOffset = _graphPan;
            _graphRoot.CapturePointer(_graphPanPointerId);
        }

        void EndGraphPan()
        {
            if (!_isGraphPanning)
                return;

            if (_graphRoot != null && _graphPanPointerId >= 0 && _graphRoot.HasPointerCapture(_graphPanPointerId))
                _graphRoot.ReleasePointer(_graphPanPointerId);

            _isGraphPanning = false;
            _graphPanPointerId = -1;
        }

        bool IsBackgroundPointerDown(PointerDownEvent evt)
        {
            if (_graphRoot == null)
                return false;

            if (evt.target is not VisualElement targetElement)
                return false;

            if (targetElement == _graphRoot || targetElement == _graphContentRoot || targetElement == _nodeLayer)
                return true;

            foreach (var pair in _nodeVisuals)
            {
                var nodeRoot = pair.Value.Root;
                if (nodeRoot == targetElement || nodeRoot.Contains(targetElement))
                    return false;
            }

            return true;
        }

        void DrawWires()
        {
            if (_wireLayer == null)
                return;

            Handles.BeginGUI();
            var wireColor = new Color(0.40f, 0.66f, 1f, 0.9f);
            var ghostColor = new Color(0.62f, 0.78f, 1f, 0.6f);

            foreach (var pair in _nodeVisuals)
            {
                var sourceContext = pair.Value;
                if (sourceContext == null)
                    continue;

                foreach (var jointPair in sourceContext.JointVisuals)
                {
                    var joint = jointPair.Key;
                    var jointVisual = jointPair.Value;
                    if (joint == null || jointVisual?.OutputPort == null)
                        continue;

                    if (joint.SelectedNextNodeId <= 0)
                        continue;

                    if (!_nodeVisuals.TryGetValue(joint.SelectedNextNodeId, out var targetContext) || targetContext == null)
                        continue;

                    var fromWorld = jointVisual.OutputPort.worldBound.center;
                    var toWorld = targetContext.InputPort != null
                        ? targetContext.InputPort.worldBound.center
                        : targetContext.Root.worldBound.center;

                    var from = _wireLayer.WorldToLocal(fromWorld);
                    var to = _wireLayer.WorldToLocal(toWorld);
                    DrawBezier(from, to, wireColor, 3f);
                }
            }

            if (_isLinkDragging && _linkSourcePort != null)
            {
                var fromWorld = _linkSourcePort.worldBound.center;
                var from = _wireLayer.WorldToLocal(fromWorld);
                DrawBezier(from, _lastGraphMouse, ghostColor, 2f);
            }

            Handles.EndGUI();
        }

        static void DrawBezier(Vector2 from, Vector2 to, Color color, float width)
        {
            var tangent = Mathf.Max(42f, Mathf.Abs(to.x - from.x) * 0.35f);
            var fromTangent = from + new Vector2(tangent, 0f);
            var toTangent = to + new Vector2(-tangent, 0f);
            Handles.DrawBezier(from, to, fromTangent, toTangent, color, null, width);
        }

        void DrawInspectorPanel()
        {
            EditorGUILayout.Space(4f);

            if (_preset == null)
            {
                EditorGUILayout.HelpBox("ConversationFlowPreset is not assigned.", MessageType.Info);
                return;
            }

            var hasSerializedFlow = TryGetSerializedFlowProperty(out var flowProperty);
            var beganScroll = false;
            try
            {
                _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);
                beganScroll = true;
                if (!hasSerializedFlow || flowProperty == null)
                {
                    EditorGUILayout.HelpBox("Serialized property binding was not resolved. Open this window from the Flow field Open button.", MessageType.Info);
                    DrawFallbackInspector();
                    return;
                }

                _ownerSerializedObject?.UpdateIfRequiredOrScript();
                EditorGUI.BeginChangeCheck();

                if (_needsGraphRebuildFromSerializedSync)
                {
                    _needsGraphRebuildFromSerializedSync = false;
                    QueueGraphRebuild();
                }

                var selectedNodeId = _preset.GraphState.SelectedNodeId;
                if (selectedNodeId > 0)
                {
                    if (TryResolveSelectedNodeProperty(flowProperty, selectedNodeId, out var nodeProperty) && nodeProperty != null)
                    {
                        EditorGUILayout.LabelField($"Selected Node: {selectedNodeId}", EditorStyles.boldLabel);
                        _lastUnresolvedSelectedNodeId = -1;

                        var selectedNode = nodeProperty.managedReferenceValue as ConversationNodePresetBase;
                        var drawn = TryDrawOdinPropertyAtUnityPath(nodeProperty.propertyPath);

                        if (!drawn)
                        {
                            EditorGUILayout.HelpBox("Odin property drawing failed for selected node; fallback Unity property drawing was used.", MessageType.Warning);
                            EditorGUILayout.PropertyField(nodeProperty, true);
                        }

                        if (selectedNode is ConversationMessageNodePreset selectedMessageNode)
                            DrawMessageModuleHints(selectedMessageNode);
                        else if (nodeProperty.managedReferenceValue is ConversationMessageNodePreset messageNode)
                            DrawMessageModuleHints(messageNode);
                    }
                    else if (_preset.TryGetNode(selectedNodeId, out var selectedNode) && selectedNode != null)
                    {
                        EditorGUILayout.LabelField($"Selected Node: {selectedNodeId}", EditorStyles.boldLabel);
                        _lastUnresolvedSelectedNodeId = -1;

                        if (TryFindNodePropertyByReferenceAcrossSerializedObject(selectedNodeId, out var reboundNodeProperty) && reboundNodeProperty != null)
                        {
                            if (!TryDrawOdinPropertyAtUnityPath(reboundNodeProperty.propertyPath))
                                EditorGUILayout.PropertyField(reboundNodeProperty, true);
                        }
                        else
                        {
                            EditorGUILayout.HelpBox("Selected node could not be resolved from serialized object. Fallback inspector is shown.", MessageType.Warning);
                            DrawFallbackInspector();
                        }

                        if (selectedNode is ConversationMessageNodePreset messageNode)
                            DrawMessageModuleHints(messageNode);
                    }
                    else
                    {
                        if (_lastUnresolvedSelectedNodeId != selectedNodeId)
                        {
                            Debug.LogWarning($"[ConversationFlow] Selected node ({selectedNodeId}) could not be resolved from serialized flow. Keeping selection and drawing fallback inspector.");
                            _lastUnresolvedSelectedNodeId = selectedNodeId;
                        }

                        EditorGUILayout.HelpBox($"Selected node ({selectedNodeId}) could not be resolved from serialized flow. Fallback inspector is shown.", MessageType.Warning);
                        DrawFallbackInspector();
                    }
                }
                else
                {
                    _lastUnresolvedSelectedNodeId = -1;

                    EditorGUILayout.LabelField("Conversation Settings", EditorStyles.boldLabel);

                    var settingsProperty = flowProperty.FindPropertyRelative("_settings");
                    if (settingsProperty != null)
                    {
                        if (!TryDrawOdinPropertyAtUnityPath(settingsProperty.propertyPath))
                        {
                            EditorGUILayout.HelpBox("Odin property drawing failed for flow settings; fallback Unity property drawing was used.", MessageType.Warning);
                            EditorGUILayout.PropertyField(settingsProperty, true);
                        }
                    }

                    EditorGUILayout.Space(8f);
                    EditorGUILayout.HelpBox("Nodeをクリックするとそのノード設定をここで編集できます。", MessageType.Info);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    _ownerSerializedObject?.ApplyModifiedProperties();

                    _preset.ValidateAndFixDuplicateNodeIds(true);
                    _preset.EnsureStartNode();
                    _preset.RebuildPreviousLinks();

                    MarkOwnerDirty();
                    QueueGraphRebuild();
                }
            }
            finally
            {
                if (beganScroll)
                    EditorGUILayout.EndScrollView();
            }
        }

        void QueueGraphRebuild()
        {
            if (_isGraphRebuildQueued)
                return;

            _isGraphRebuildQueued = true;
            EditorApplication.delayCall += FlushQueuedGraphRebuild;
        }

        void FlushQueuedGraphRebuild()
        {
            EditorApplication.delayCall -= FlushQueuedGraphRebuild;

            if (!_isGraphRebuildQueued)
                return;

            _isGraphRebuildQueued = false;
            if (this == null)
                return;

            RebuildGraph();
        }

        bool TryDrawNodeInspectorDirect(ConversationNodePresetBase node)
        {
            if (node == null)
                return false;

            PropertyTree? tree = null;
            var beganDraw = false;
            try
            {
                tree = PropertyTree.Create(node);
                if (tree == null)
                    return false;

                tree.UpdateTree();
                tree.BeginDraw(false);
                beganDraw = true;

                var rootProperty = tree.RootProperty;
                if (rootProperty == null)
                    return false;

                var children = rootProperty.Children;
                for (var i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    if (child == null)
                        continue;

                    DrawInspectorPropertySafely(child);
                }

                return true;
            }
            catch (Exception ex)
            {
                if (ex is ExitGUIException)
                    throw;

                Debug.LogWarning($"[ConversationFlow] Direct node inspector draw failed. nodeType={node.GetType().Name} message={ex.Message}");
                return false;
            }
            finally
            {
                if (tree != null)
                {
                    if (beganDraw)
                        tree.EndDraw();

                    tree.Dispose();
                }
            }
        }

        void DrawMessageNodeInspector(SerializedProperty nodeProperty, ConversationMessageNodePreset messageNode)
        {
            DrawProperty(nodeProperty, "_nodeId");
            DrawProperty(nodeProperty, "_slot");
            DrawProperty(nodeProperty, "_dialogueTagOverride");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Message", EditorStyles.boldLabel);

            var characterDataIdProp = DrawCharacterDataIdDropdown(nodeProperty);
            DrawMessageBodyTextProperty(nodeProperty);

            var selectedCharacterId = characterDataIdProp != null
                ? Mathf.Max(0, characterDataIdProp.intValue)
                : Mathf.Max(0, messageNode.CharacterDataId);

            var hasDefinition = TryResolveCharacterDefinition(selectedCharacterId, out var definition);
            CharacterExpressionModulePreset? expressionModule = null;
            var hasExpressionModule = hasDefinition
                && definition != null
                && definition.TryGetModule<CharacterExpressionModulePreset>(out expressionModule)
                && expressionModule != null;
            var hasDefaultImageModule = hasDefinition && definition != null && definition.TryGetModule<CharacterDefaultImageModulePreset>(out _);

            if (hasExpressionModule && expressionModule != null)
                DrawExpressionKeyDropdown(nodeProperty, expressionModule);

            if (hasDefaultImageModule)
                DrawProperty(nodeProperty, "_useDefaultImageFallback");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Detail", EditorStyles.boldLabel);

            var detailOverrideProp = DrawProperty(nodeProperty, "_overrideDetailSettings");
            if (detailOverrideProp != null && detailOverrideProp.boolValue)
                DrawProperty(nodeProperty, "_detailSettingsOverride");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Message Hooks", EditorStyles.boldLabel);

            var hookOverrideProp = DrawProperty(nodeProperty, "_overrideMessageHooks");
            if (hookOverrideProp != null && hookOverrideProp.boolValue)
            {
                DrawProperty(nodeProperty, "_messageHookMergeMode");
                DrawProperty(nodeProperty, "_messageHooksOverride");
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Node Commands", EditorStyles.boldLabel);
            DrawProperty(nodeProperty, "_onEnterCommands");
            DrawProperty(nodeProperty, "_onExitCommands");
        }

        SerializedProperty? DrawCharacterDataIdDropdown(SerializedProperty nodeProperty)
        {
            var property = nodeProperty.FindPropertyRelative("_characterDataId");
            if (property == null)
                return null;

            var options = CollectCharacterDefinitionOptions();
            if (options.Count <= 0)
            {
                EditorGUILayout.PropertyField(property, true);
                return property;
            }

            var currentId = Mathf.Max(0, property.intValue);
            var currentIndex = 0;
            for (var i = 0; i < options.Count; i++)
            {
                if (options[i].CharacterId != currentId)
                    continue;

                currentIndex = i;
                break;
            }

            if (currentId > 0 && options[currentIndex].CharacterId != currentId)
            {
                options.Add(new CharacterDefinitionOption(currentId, $"{currentId}: (Missing Definition)"));
                currentIndex = options.Count - 1;
            }

            var labels = new string[options.Count];
            for (var i = 0; i < options.Count; i++)
                labels[i] = options[i].Label;

            var newIndex = EditorGUILayout.Popup("Character Data Id", currentIndex, labels);
            if (newIndex >= 0 && newIndex < options.Count && newIndex != currentIndex)
                property.intValue = options[newIndex].CharacterId;

            return property;
        }

        void DrawExpressionKeyDropdown(SerializedProperty nodeProperty, CharacterExpressionModulePreset expressionModule)
        {
            var property = nodeProperty.FindPropertyRelative("_expressionKey");
            if (property == null)
                return;

            var labels = new List<string> { "<None>" };
            var values = new List<string> { string.Empty };

            var entries = expressionModule.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;

                var key = string.IsNullOrWhiteSpace(entry.Key) ? string.Empty : entry.Key.Trim();
                if (string.IsNullOrEmpty(key))
                    continue;

                var debugText = string.IsNullOrWhiteSpace(entry.DebugText) ? string.Empty : entry.DebugText.Trim();
                var label = string.IsNullOrEmpty(debugText)
                    ? key
                    : $"{debugText} ({key})";

                labels.Add(label);
                values.Add(key);
            }

            var currentValue = property.stringValue ?? string.Empty;
            var currentIndex = 0;
            for (var i = 1; i < values.Count; i++)
            {
                if (!string.Equals(values[i], currentValue, StringComparison.Ordinal))
                    continue;

                currentIndex = i;
                break;
            }

            var newIndex = EditorGUILayout.Popup("Expression Key", currentIndex, labels.ToArray());
            if (newIndex >= 0 && newIndex < values.Count && newIndex != currentIndex)
                property.stringValue = values[newIndex];
        }

        void DrawMessageBodyTextProperty(SerializedProperty nodeProperty)
        {
            var property = nodeProperty.FindPropertyRelative("_bodyText");
            if (property == null)
                return;

            if (TryDrawOdinPropertyAtUnityPath(property.propertyPath))
                return;

            EditorGUILayout.PropertyField(property, true);
        }

        SerializedProperty? DrawProperty(SerializedProperty root, string relativeName)
        {
            var property = root.FindPropertyRelative(relativeName);
            if (property == null)
                return null;

            if (!TryDrawOdinPropertyAtUnityPath(property.propertyPath))
                EditorGUILayout.PropertyField(property, true);
            return property;
        }

        static List<CharacterDefinitionOption> CollectCharacterDefinitionOptions()
        {
            var labelsById = new Dictionary<int, string>();
            var databases = Resources.FindObjectsOfTypeAll<CharacterDataBaseMB>();
            if (databases != null)
            {
                for (var i = 0; i < databases.Length; i++)
                {
                    var db = databases[i];
                    if (db == null)
                        continue;

                    var definitions = db.Definitions;
                    for (var d = 0; d < definitions.Count; d++)
                    {
                        var candidate = definitions[d];
                        if (candidate == null || candidate.CharacterId <= 0)
                            continue;

                        if (labelsById.ContainsKey(candidate.CharacterId))
                            continue;

                        labelsById.Add(candidate.CharacterId, BuildCharacterDefinitionLabel(candidate));
                    }
                }
            }

            var ids = new List<int>(labelsById.Keys);
            ids.Sort();

            var options = new List<CharacterDefinitionOption>
            {
                new CharacterDefinitionOption(0, "<None>")
            };

            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (!labelsById.TryGetValue(id, out var label))
                    continue;

                options.Add(new CharacterDefinitionOption(id, label));
            }

            return options;
        }

        static string BuildCharacterDefinitionLabel(CharacterDataBaseDefinition definition)
        {
            var stableKey = string.IsNullOrWhiteSpace(definition.StableKey)
                ? string.Empty
                : definition.StableKey.Trim();
            var displayName = string.IsNullOrWhiteSpace(definition.DisplayName)
                ? string.Empty
                : definition.DisplayName.Trim();

            if (!string.IsNullOrEmpty(stableKey) && !string.IsNullOrEmpty(displayName))
                return $"{definition.CharacterId}: {stableKey} ({displayName})";

            if (!string.IsNullOrEmpty(stableKey))
                return $"{definition.CharacterId}: {stableKey}";

            if (!string.IsNullOrEmpty(displayName))
                return $"{definition.CharacterId}: {displayName}";

            return $"{definition.CharacterId}: Character";
        }

        bool TryDrawOdinPropertyAtUnityPath(string unityPropertyPath)
        {
            if (string.IsNullOrWhiteSpace(unityPropertyPath))
                return false;

            if (!TryGetInspectorPropertyTree(out var tree) || tree == null)
                return false;

            tree.UpdateTree();
            tree.BeginDraw(true);
            try
            {
                var property = tree.GetPropertyAtUnityPath(unityPropertyPath);
                if (property == null)
                    return false;

                DrawInspectorPropertySafely(property);
                return true;
            }
            finally
            {
                tree.EndDraw();
            }
        }

        bool TryGetInspectorPropertyTree(out PropertyTree? tree)
        {
            tree = null;
            if (_ownerSerializedObject == null)
                return false;

            _inspectorPropertyTree ??= PropertyTree.Create(_ownerSerializedObject);
            tree = _inspectorPropertyTree;
            return tree != null;
        }

        static void DrawInspectorPropertySafely(InspectorProperty property)
        {
            if (property == null)
                return;

            var oldLabelWidth = EditorGUIUtility.labelWidth;
            var oldIndent = EditorGUI.indentLevel;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            var oldContentColor = GUI.contentColor;
            var oldBackgroundColor = GUI.backgroundColor;
            try
            {
                property.Draw();
            }
            finally
            {
                EditorGUIUtility.labelWidth = oldLabelWidth;
                EditorGUI.indentLevel = oldIndent;
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.contentColor = oldContentColor;
                GUI.backgroundColor = oldBackgroundColor;
            }
        }

        void DisposeInspectorPropertyTree()
        {
            if (_inspectorPropertyTree == null)
                return;

            _inspectorPropertyTree.Dispose();
            _inspectorPropertyTree = null;
        }

        void DrawFallbackInspector()
        {
            var selectedNodeId = _preset?.GraphState.SelectedNodeId ?? 0;
            if (selectedNodeId > 0 && _preset != null && _preset.TryGetNode(selectedNodeId, out var node) && node != null)
            {
                EditorGUILayout.LabelField($"Selected Node: {selectedNodeId}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Type", node.GetType().Name);
                EditorGUILayout.LabelField("Preview", node.DebugViewText);
                return;
            }

            EditorGUILayout.LabelField("Flow", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Nodes", (_preset?.EditorNodes.Count ?? 0).ToString());
        }

        bool TryGetSerializedFlowProperty(out SerializedProperty? flowProperty)
        {
            flowProperty = null;
            if (_owner == null)
                return false;

            if (string.IsNullOrWhiteSpace(_ownerPropertyPath))
                return false;

            if (_ownerSerializedObject == null || _ownerSerializedObject.targetObject != _owner)
            {
                _ownerSerializedObject = new SerializedObject(_owner);
                DisposeInspectorPropertyTree();
            }

            _ownerSerializedObject.UpdateIfRequiredOrScript();

            var direct = _ownerSerializedObject.FindProperty(_ownerPropertyPath);
            if (direct != null && IsFlowPropertyBoundToCurrentPreset(direct))
            {
                _loggedFlowPathMismatch = false;
                flowProperty = direct;
                TrySyncPresetReferenceFromSerializedFlow(flowProperty);
                return true;
            }

            if (TryFindSerializedFlowPropertyByPresetReference(out var rebound) && rebound != null)
            {
                if (!string.Equals(_ownerPropertyPath, rebound.propertyPath, StringComparison.Ordinal))
                {
                    _ownerPropertyPath = rebound.propertyPath;
                    DisposeInspectorPropertyTree();
                }

                _loggedFlowPathMismatch = false;
                flowProperty = rebound;
                TrySyncPresetReferenceFromSerializedFlow(flowProperty);
                return true;
            }

            if (TryFindAnySerializedFlowProperty(out var inferred) && inferred != null)
            {
                if (!string.Equals(_ownerPropertyPath, inferred.propertyPath, StringComparison.Ordinal))
                {
                    _ownerPropertyPath = inferred.propertyPath;
                    DisposeInspectorPropertyTree();
                }

                _loggedFlowPathMismatch = false;
                flowProperty = inferred;
                TrySyncPresetReferenceFromSerializedFlow(flowProperty);
                return true;
            }

            if (!_loggedFlowPathMismatch && _preset != null)
            {
                Debug.LogWarning("[ConversationFlow] Serialized flow property path mismatch detected and automatic rebind failed. Open the window again from the Flow field if this persists.");
                _loggedFlowPathMismatch = true;
            }

            flowProperty = direct;
            if (flowProperty != null)
                TrySyncPresetReferenceFromSerializedFlow(flowProperty);
            return flowProperty != null;
        }

        bool TryResolveSelectedNodeProperty(SerializedProperty flowProperty, int nodeId, out SerializedProperty? nodeProperty)
        {
            nodeProperty = null;
            if (flowProperty == null || nodeId <= 0)
                return false;

            if (TryFindNodePropertyById(flowProperty, nodeId, out nodeProperty) && nodeProperty != null)
                return true;

            if (TryFindNodePropertyByReferenceAcrossSerializedObject(nodeId, out nodeProperty) && nodeProperty != null)
                return true;

            if (!TrySyncPresetReferenceFromSerializedFlow(flowProperty))
                return false;

            if (TryFindNodePropertyById(flowProperty, nodeId, out nodeProperty) && nodeProperty != null)
                return true;

            if (TryFindNodePropertyByReferenceAcrossSerializedObject(nodeId, out nodeProperty) && nodeProperty != null)
                return true;

            return false;
        }

        bool TryFindNodePropertyById(SerializedProperty flowProperty, int nodeId, out SerializedProperty? nodeProperty)
        {
            nodeProperty = null;
            if (flowProperty == null)
                return false;

            var nodesProperty = flowProperty.FindPropertyRelative("_nodes");
            if (nodesProperty == null || !nodesProperty.isArray)
                return false;

            for (var i = 0; i < nodesProperty.arraySize; i++)
            {
                var candidate = nodesProperty.GetArrayElementAtIndex(i);
                if (candidate == null)
                    continue;

                if (candidate.managedReferenceValue is ConversationNodePresetBase managedNode && managedNode.NodeId == nodeId)
                {
                    nodeProperty = candidate;
                    return true;
                }

                if (!TryReadNodeId(candidate, out var candidateNodeId))
                    continue;

                if (candidateNodeId != nodeId)
                    continue;

                nodeProperty = candidate;
                return true;
            }

            if (_preset == null)
                return false;

            var editorNodes = _preset.EditorNodes;
            for (var i = 0; i < editorNodes.Count; i++)
            {
                var candidateNode = editorNodes[i];
                if (candidateNode == null || candidateNode.NodeId != nodeId)
                    continue;

                if (i < nodesProperty.arraySize)
                {
                    var byIndex = nodesProperty.GetArrayElementAtIndex(i);
                    if (byIndex != null)
                    {
                        nodeProperty = byIndex;
                        return true;
                    }
                }

                break;
            }

            if (!_preset.TryGetNode(nodeId, out var selectedNode) || selectedNode == null)
                return false;

            for (var i = 0; i < nodesProperty.arraySize; i++)
            {
                var candidate = nodesProperty.GetArrayElementAtIndex(i);
                if (candidate == null)
                    continue;

                if (!ReferenceEquals(candidate.managedReferenceValue, selectedNode))
                    continue;

                nodeProperty = candidate;
                return true;
            }

            return false;
        }

        bool TryFindNodePropertyByReferenceAcrossSerializedObject(int nodeId, out SerializedProperty? nodeProperty)
        {
            nodeProperty = null;
            if (_ownerSerializedObject == null || _preset == null)
                return false;

            if (!_preset.TryGetNode(nodeId, out var selectedNode) || selectedNode == null)
                return false;

            var iterator = _ownerSerializedObject.GetIterator();
            if (!iterator.Next(true))
                return false;

            do
            {
                if (iterator.propertyType != SerializedPropertyType.ManagedReference)
                    continue;

                if (!ReferenceEquals(iterator.managedReferenceValue, selectedNode))
                    continue;

                var resolved = _ownerSerializedObject.FindProperty(iterator.propertyPath);
                if (resolved == null)
                    continue;

                nodeProperty = resolved;
                return true;
            }
            while (iterator.Next(true));

            return false;
        }

        bool TrySyncPresetReferenceFromSerializedFlow(SerializedProperty flowProperty)
        {
            if (flowProperty == null)
                return false;

            if (flowProperty.managedReferenceValue is not ConversationFlowPreset serializedFlow || serializedFlow == null)
                return false;

            if (ReferenceEquals(_preset, serializedFlow))
                return false;

            _preset = serializedFlow;
            _needsGraphRebuildFromSerializedSync = true;
            return true;
        }

        bool TryFindAnySerializedFlowProperty(out SerializedProperty? flowProperty)
        {
            flowProperty = null;
            if (_ownerSerializedObject == null)
                return false;

            var iterator = _ownerSerializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (iterator.propertyType != SerializedPropertyType.Generic
                    && iterator.propertyType != SerializedPropertyType.ManagedReference)
                {
                    continue;
                }

                var resolved = GetTargetObjectOfProperty(_ownerSerializedObject, iterator.propertyPath);
                if (resolved is not ConversationFlowPreset)
                    continue;

                flowProperty = _ownerSerializedObject.FindProperty(iterator.propertyPath);
                return flowProperty != null;
            }

            return false;
        }

        bool TryFindSerializedFlowPropertyByPresetReference(out SerializedProperty? flowProperty)
        {
            flowProperty = null;
            if (_ownerSerializedObject == null || _preset == null)
                return false;

            var iterator = _ownerSerializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (iterator.propertyType != SerializedPropertyType.Generic
                    && iterator.propertyType != SerializedPropertyType.ManagedReference)
                {
                    continue;
                }

                var resolved = GetTargetObjectOfProperty(_ownerSerializedObject, iterator.propertyPath);
                if (!ReferenceEquals(resolved, _preset))
                    continue;

                flowProperty = _ownerSerializedObject.FindProperty(iterator.propertyPath);
                return flowProperty != null;
            }

            return false;
        }

        bool IsFlowPropertyBoundToCurrentPreset(SerializedProperty property)
        {
            if (_ownerSerializedObject == null || _preset == null || property == null)
                return false;

            var resolved = GetTargetObjectOfProperty(_ownerSerializedObject, property.propertyPath);
            return ReferenceEquals(resolved, _preset);
        }

        static bool TryReadNodeId(SerializedProperty nodeProperty, out int nodeId)
        {
            nodeId = 0;
            if (nodeProperty == null)
                return false;

            var directId = nodeProperty.FindPropertyRelative("_nodeId");
            if (directId != null && directId.propertyType == SerializedPropertyType.Integer)
            {
                nodeId = directId.intValue;
                return true;
            }

            var iterator = nodeProperty.Copy();
            var end = iterator.GetEndProperty();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (!string.Equals(iterator.name, "_nodeId", StringComparison.Ordinal))
                    continue;

                if (iterator.propertyType != SerializedPropertyType.Integer)
                    continue;

                nodeId = iterator.intValue;
                return true;
            }

            return false;
        }

        static object? GetTargetObjectOfProperty(SerializedObject serializedObject, string propertyPath)
        {
            if (serializedObject == null || serializedObject.targetObject == null || string.IsNullOrWhiteSpace(propertyPath))
                return null;

            var path = propertyPath.Replace(".Array.data[", "[");
            object? current = serializedObject.targetObject;
            var elements = path.Split('.');
            for (var i = 0; i < elements.Length; i++)
            {
                if (current == null)
                    return null;

                var element = elements[i];
                var bracketIndex = element.IndexOf("[", StringComparison.Ordinal);
                if (bracketIndex >= 0)
                {
                    var memberName = element.Substring(0, bracketIndex);
                    var indexText = element.Substring(bracketIndex).Replace("[", string.Empty).Replace("]", string.Empty);
                    if (!int.TryParse(indexText, out var index))
                        return null;

                    current = GetIndexedMemberValue(current, memberName, index);
                    continue;
                }

                current = GetMemberValue(current, element);
            }

            return current;
        }

        static object? GetIndexedMemberValue(object source, string memberName, int index)
        {
            var enumerable = GetMemberValue(source, memberName) as IEnumerable;
            if (enumerable == null)
                return null;

            var currentIndex = 0;
            foreach (var value in enumerable)
            {
                if (currentIndex == index)
                    return value;

                currentIndex++;
            }

            return null;
        }

        static object? GetMemberValue(object source, string memberName)
        {
            var type = source.GetType();
            while (type != null)
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                var field = type.GetField(memberName, flags);
                if (field != null)
                    return field.GetValue(source);

                var property = type.GetProperty(memberName, flags);
                if (property != null)
                    return property.GetValue(source, null);

                type = type.BaseType;
            }

            return null;
        }

        void DrawMessageModuleHints(ConversationMessageNodePreset messageNode)
        {
            if (messageNode.CharacterDataId <= 0)
            {
                EditorGUILayout.HelpBox("CharacterDataId が未指定のため Character module は適用されません。", MessageType.None);
                return;
            }

            if (!TryResolveCharacterDefinition(messageNode.CharacterDataId, out var definition) || definition == null)
            {
                EditorGUILayout.HelpBox("CharacterDataBase定義が未解決です。CharacterDataIdを確認してください。", MessageType.Warning);
                return;
            }

            var hasExpression = definition.TryGetModule<CharacterExpressionModulePreset>(out var expressionModule) && expressionModule != null;
            var hasDefaultImage = definition.TryGetModule<CharacterDefaultImageModulePreset>(out _);

            if (hasExpression)
            {
                EditorGUILayout.HelpBox("Expression moduleがあるため ExpressionKey を使えます。", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Expression moduleが無いため ExpressionKey は実行時に使われません。", MessageType.None);
            }

            if (hasDefaultImage)
                EditorGUILayout.HelpBox("DefaultImage moduleにより表情未解決時のフォールバックが有効です。", MessageType.None);
        }

        static bool TryResolveCharacterDefinition(int characterDataId, out CharacterDataBaseDefinition? definition)
        {
            definition = null;
            if (characterDataId <= 0)
                return false;

            var databases = Resources.FindObjectsOfTypeAll<CharacterDataBaseMB>();
            if (databases == null || databases.Length == 0)
                return false;

            for (var i = 0; i < databases.Length; i++)
            {
                var db = databases[i];
                if (db == null)
                    continue;

                var definitions = db.Definitions;
                for (var d = 0; d < definitions.Count; d++)
                {
                    var candidate = definitions[d];
                    if (candidate == null)
                        continue;

                    if (candidate.CharacterId == characterDataId)
                    {
                        definition = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        void RecordOwnerUndo(string label)
        {
            if (_owner == null)
                return;

            Undo.RecordObject(_owner, label);
        }

        void MarkOwnerDirty()
        {
            if (_owner == null)
                return;

            EditorUtility.SetDirty(_owner);
        }
    }
}
#endif
