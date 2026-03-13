#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Editor.Foundation;
using Game.Registry;
using UnityEngine;
using UnityEditor;

namespace Game.Editor.Registry
{
    // ================================================================
    // RegistryExplorerWindowBase - Registry Explorer Window の基底クラス
    // ================================================================
    //
    // ## 概要
    //
    // 階層 Registry の Explorer Window 基底クラス。
    // ツリー描画、D&D、コンテキストメニュー、詳細ペイン等を共通化する。
    //
    // ## 派生で実装するもの
    //
    // - GetRegistry(): Registry SO を返す
    // - GetSettings(): Settings SO を返す（任意、オーバーライドすれば使う）
    // - BuildTree(): PathTreeNode<NodeData> を構築
    // - GetKeyString(NodeData): リーフのキー文字列
    // - ApplyKeyToTarget(string): ピッカーモードでのキー適用
    // - DrawLeafDetail(NodeData): リーフの詳細表示
    // - DrawFolderDetail(NodeData): フォルダの詳細表示（任意）
    // - ConfigureTreeConfig(): TreeExplorerConfig のカスタマイズ（任意）
    //
    // ## ピッカーモード
    //
    // static _pickerTarget / _pickerCallback を設定してウィンドウを開くと
    // ピッカーモードになり、ダブルクリックでキーを適用してウィンドウを閉じる。
    //
    // ## コピー&ペースト
    //
    // Ctrl+C / Ctrl+V または右クリックメニューでノードをコピー&ペースト可能。
    // フォルダの場合は子孫ノードも含めてコピーされる。
    //
    // ================================================================

    /// <summary>
    /// Registry の NodeData。ツリー描画に必要な情報をまとめる。
    /// </summary>
    public struct RegistryNodeData<TNode> where TNode : HierarchyNodeBase, new()
    {
        public TNode Node;
        public string DisplayPath;
        public bool IsFolder;
        public bool HasKey;
    }

    // ================================================================
    // ClipboardData - コピー用データ構造
    // ================================================================

    /// <summary>
    /// コピーされたノードデータを保持する構造体。
    /// JSON シリアライズを使用してディープコピーを行う。
    /// </summary>
    [Serializable]
    public class ClipboardNodeData
    {
        public string NodeJson;       // JsonUtility でシリアライズしたノード
        public string NodeTypeName;   // ノードの型名
        public string OriginalId;     // 元の ID（重複チェック用）
        public string OriginalParentId; // 元の親 ID
        public bool IsFolder;
    }

    /// <summary>
    /// クリップボード全体のデータ。
    /// フォルダの場合は子孫ノードも含む。
    /// </summary>
    [Serializable]
    public class RegistryClipboard
    {
        public List<ClipboardNodeData> Nodes = new();
        public string RootNodeId;     // コピー元のルートノード ID
    }

    /// <summary>
    /// Registry Explorer Window の基底クラス。
    /// </summary>
    /// <typeparam name="TRegistry">Registry SO 型</typeparam>
    /// <typeparam name="TNode">Node 型</typeparam>
    /// <typeparam name="TSettings">Settings SO 型</typeparam>
    public abstract class RegistryExplorerWindowBase<TRegistry, TNode, TSettings> : EditorWindow
        where TRegistry : HierarchyRegistryBase<TNode>
        where TNode : HierarchyNodeBase, new()
        where TSettings : RegistrySettingsBase
    {
        // ------------------------------------------------------------
        // Static: Clipboard (全ウィンドウ共有)
        // ------------------------------------------------------------

        /// <summary>クリップボードデータ（全ウィンドウで共有）</summary>
        protected static RegistryClipboard _clipboard;

        // ------------------------------------------------------------
        // Static: Picker Mode
        // ------------------------------------------------------------

        protected static SerializedProperty _pickerTarget;
        protected static Action<string> _pickerCallback;

        /// <summary>ピッカーモードかどうか</summary>
        protected bool IsPickerMode => _pickerTarget != null || _pickerCallback != null;

        /// <summary>
        /// ピッカーモードの準備をする。派生クラスの Open でこれを呼ぶ。
        /// </summary>
        protected static void PreparePickerMode(SerializedProperty property, Action<string> callback)
        {
            _pickerTarget = property;
            _pickerCallback = callback;
        }

        /// <summary>
        /// ピッカーモードをクリアする。
        /// </summary>
        protected static void ClearPickerMode()
        {
            _pickerTarget = null;
            _pickerCallback = null;
        }

        // ------------------------------------------------------------
        // Instance Fields
        // ------------------------------------------------------------

        protected SerializedObject _serializedRegistry;
        protected TreeExplorerState _treeState = new();
        protected TreeExplorerConfig<RegistryNodeData<TNode>> _treeConfig;
        protected PathTreeNode<RegistryNodeData<TNode>> _treeRoot;

        protected string _selectedNodeId;
        protected float _treeWidth = 280f;
        protected bool _needsRebuild = true;

        // Cached icons
        protected Texture2D _cachedFolderIcon;
        protected Texture2D _cachedLeafIcon;

        // ------------------------------------------------------------
        // 抽象メソッド（派生で必ず実装）
        // ------------------------------------------------------------

        /// <summary>Registry SO を返す。</summary>
        protected abstract TRegistry GetRegistry();

        /// <summary>
        /// Registry からツリーを構築する。
        /// 戻り値は PathTreeNode&lt;RegistryNodeData&lt;TNode&gt;&gt; のルート。
        /// </summary>
        protected abstract PathTreeNode<RegistryNodeData<TNode>> BuildTree(TRegistry registry);

        /// <summary>リーフノードのキー文字列を取得する。</summary>
        protected abstract string GetKeyString(TNode node);

        /// <summary>リーフの詳細を描画する。</summary>
        protected abstract void DrawLeafDetail(TNode node);

        // ------------------------------------------------------------
        // オーバーライド可能なメソッド
        // ------------------------------------------------------------

        /// <summary>Settings SO を返す（任意）。</summary>
        protected virtual TSettings GetSettings() => null;

        /// <summary>フォルダの詳細を描画する（任意）。</summary>
        protected virtual void DrawFolderDetail(TNode node)
        {
            EditorGUILayout.LabelField("Folder", node?.Name ?? "(none)");
        }

        /// <summary>ピッカーでキー適用時の処理。</summary>
        protected virtual void ApplyKeyToTarget(string keyString)
        {
            if (_pickerTarget != null)
            {
                _pickerTarget.stringValue = keyString;
                _pickerTarget.serializedObject.ApplyModifiedProperties();
            }
            _pickerCallback?.Invoke(keyString);
            ClearPickerMode();
            Close();
        }

        /// <summary>TreeExplorerConfig の追加設定（任意）。</summary>
        protected virtual void ConfigureTreeConfig(TreeExplorerConfig<RegistryNodeData<TNode>> config) { }

        /// <summary>ウィンドウタイトルを返す。</summary>
        protected virtual string GetWindowTitle()
        {
            var settings = GetSettings();
            return settings != null ? settings.WindowTitle : "Registry Explorer";
        }

        /// <summary>フォルダアイコンを返す。</summary>
        protected virtual Texture2D GetFolderIcon()
        {
            var settings = GetSettings();
            if (settings != null)
                return settings.GetFolderIcon();

            return _cachedFolderIcon ??= EditorGUIUtility.IconContent("Folder Icon").image as Texture2D;
        }

        /// <summary>リーフアイコンを返す。</summary>
        protected virtual Texture2D GetLeafIcon()
        {
            var settings = GetSettings();
            if (settings != null)
                return settings.GetLeafIcon();

            return _cachedLeafIcon ??= EditorGUIUtility.IconContent("d_ScriptableObject Icon").image as Texture2D;
        }

        /// <summary>ウィンドウ最小サイズを返す。</summary>
        protected virtual Vector2 GetMinSize()
        {
            var settings = GetSettings();
            return settings != null ? settings.MinSize : new Vector2(600, 400);
        }

        /// <summary>コンテキストメニューに追加の項目を入れる（任意）。</summary>
        protected virtual void AddCustomContextMenuItems(GenericMenu menu, RegistryNodeData<TNode> nodeData) { }

        // ------------------------------------------------------------
        // Unity Callbacks
        // ------------------------------------------------------------

        protected virtual void OnEnable()
        {
            titleContent = new GUIContent(GetWindowTitle());
            minSize = GetMinSize();
            InitializeTreeConfig();
            _needsRebuild = true;

            // Undo/Redo 時にツリーを再構築
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        protected virtual void OnDisable()
        {
            ClearPickerMode();
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        /// <summary>Undo/Redo 実行時の処理。</summary>
        protected virtual void OnUndoRedoPerformed()
        {
            RequestRebuild();
        }

        protected virtual void OnGUI()
        {
            var registry = GetRegistry();
            if (registry == null)
            {
                EditorGUILayout.HelpBox("Registry not found.", MessageType.Warning);
                return;
            }

            // キーボードイベント処理
            HandleKeyboardEvents(registry);

            // SerializedObject 更新
            if (_serializedRegistry == null || _serializedRegistry.targetObject != registry)
                _serializedRegistry = new SerializedObject(registry);
            _serializedRegistry.Update();

            // ツリー再構築
            if (_needsRebuild)
            {
                _treeRoot = BuildTree(registry);
                _needsRebuild = false;
            }

            // レイアウト
            DrawHeader();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawTreePane();
                DrawDivider();
                DrawDetailPane(registry);
            }

            _serializedRegistry.ApplyModifiedProperties();
        }

        /// <summary>キーボードイベントを処理する。</summary>
        protected virtual void HandleKeyboardEvents(TRegistry registry)
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;

            // テキストフィールドなどのGUIコントロールにフォーカスがある場合はスキップ
            // （Detail ペインでの編集を妨げない）
            if (GUIUtility.keyboardControl != 0)
                return;

            // Ctrl+C: コピー
            if (e.control && e.keyCode == KeyCode.C)
            {
                if (!string.IsNullOrEmpty(_selectedNodeId))
                {
                    var node = registry.FindNode(_selectedNodeId);
                    if (node != null)
                    {
                        CopyNodeToClipboard(registry, node);
                        e.Use();
                    }
                }
            }
            // Ctrl+V: ペースト
            else if (e.control && e.keyCode == KeyCode.V)
            {
                if (HasClipboardData())
                {
                    var targetParentId = GetPasteTargetParentId(registry);
                    PasteFromClipboard(registry, targetParentId);
                    e.Use();
                }
            }
            // Delete キー
            else if (e.keyCode == KeyCode.Delete)
            {
                if (!string.IsNullOrEmpty(_selectedNodeId))
                {
                    var node = registry.FindNode(_selectedNodeId);
                    if (node != null)
                    {
                        if (EditorUtility.DisplayDialog("Delete",
                            $"Delete '{node.Name}' and all descendants?", "Delete", "Cancel"))
                        {
                            registry.DeleteNodeAndDescendants(_selectedNodeId);
                            _selectedNodeId = null;
                            RequestRebuild();
                        }
                        e.Use();
                    }
                }
            }
        }

        // ------------------------------------------------------------
        // UI Drawing
        // ------------------------------------------------------------

        /// <summary>ヘッダー（検索バー）を描画する。</summary>
        protected virtual void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _treeState.SearchText = EditorGUILayout.TextField(
                    _treeState.SearchText, EditorStyles.toolbarSearchField);

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    _needsRebuild = true;
            }
        }

        /// <summary>ツリーペインを描画する。</summary>
        protected virtual void DrawTreePane()
        {
            var treeRect = GUILayoutUtility.GetRect(
                _treeWidth, _treeWidth, 0, float.MaxValue, GUILayout.ExpandHeight(true));

            TreeExplorerGUI.DrawTree(treeRect, _treeRoot, _treeState, _treeConfig);
        }

        /// <summary>分割線を描画する。</summary>
        protected virtual void DrawDivider()
        {
            var dividerRect = GUILayoutUtility.GetRect(4, 4, 0, float.MaxValue, GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(dividerRect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDrag && dividerRect.Contains(Event.current.mousePosition))
            {
                _treeWidth += Event.current.delta.x;
                _treeWidth = Mathf.Clamp(_treeWidth, 150, position.width - 200);
                Repaint();
            }

            EditorGUI.DrawRect(dividerRect, new Color(0.2f, 0.2f, 0.2f, 1f));
        }

        /// <summary>詳細ペインを描画する。</summary>
        protected virtual void DrawDetailPane(TRegistry registry)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                if (string.IsNullOrEmpty(_selectedNodeId))
                {
                    EditorGUILayout.HelpBox("Select an item from the tree.", MessageType.Info);
                    return;
                }

                var node = registry.FindNode(_selectedNodeId);
                if (node == null)
                {
                    EditorGUILayout.HelpBox("Node not found.", MessageType.Warning);
                    return;
                }

                EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                if (node.IsFolder)
                    DrawFolderDetail(node);
                else
                    DrawLeafDetail(node);
            }
        }

        // ------------------------------------------------------------
        // Tree Config Initialization
        // ------------------------------------------------------------

        /// <summary>TreeExplorerConfig を初期化する。</summary>
        protected virtual void InitializeTreeConfig()
        {
            _treeConfig = new TreeExplorerConfig<RegistryNodeData<TNode>>
            {
                GetLabel = pathNode => pathNode.Entry.Node?.Name ?? pathNode.Entry.DisplayPath,
                GetTooltip = pathNode => pathNode.Entry.DisplayPath,
                // ルートノード（Node==null）または IsFolder==true の場合はコンテナ
                // また、子がある場合もコンテナとして扱う
                IsContainer = pathNode => pathNode.Entry.Node == null || pathNode.Entry.IsFolder || pathNode.Children.Count > 0,
                GetIcon = pathNode => pathNode.Entry.IsFolder ? GetFolderIcon() : GetLeafIcon(),
                MatchesSearch = MatchesSearch,
                OnSelected = OnNodeSelected,
                OnDoubleClick = OnNodeDoubleClick,
                OnContextClick = OnContextClick,
                CanDrag = pathNode => pathNode.Entry.Node != null,
                CanDrop = CanDrop,
                OnDrop = OnDrop
            };

            // Settings から TreeVisual を適用
            var settings = GetSettings();
            if (settings != null && settings.TreeVisual != null)
            {
                _treeConfig.Visual = settings.TreeVisual;
            }

            ConfigureTreeConfig(_treeConfig);
        }

        // ------------------------------------------------------------
        // Tree Callbacks
        // ------------------------------------------------------------

        protected virtual bool MatchesSearch(PathTreeNode<RegistryNodeData<TNode>> pathNode, string searchText)
        {
            if (string.IsNullOrEmpty(searchText)) return true;
            var data = pathNode.Entry;
            var label = data.Node?.Name ?? data.DisplayPath ?? string.Empty;
            return label.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        protected virtual void OnNodeSelected(PathTreeNode<RegistryNodeData<TNode>> pathNode)
        {
            var data = pathNode.Entry;
            _selectedNodeId = data.Node?.Id;
            Repaint();
        }

        protected virtual void OnNodeDoubleClick(PathTreeNode<RegistryNodeData<TNode>> pathNode)
        {
            if (!IsPickerMode) return;

            var data = pathNode.Entry;
            if (data.Node == null || data.Node.IsFolder) return;

            var keyString = GetKeyString(data.Node);
            if (!string.IsNullOrEmpty(keyString))
                ApplyKeyToTarget(keyString);
        }

        protected virtual void OnContextClick(PathTreeNode<RegistryNodeData<TNode>> pathNode, Vector2 position)
        {
            var menu = new GenericMenu();
            var data = pathNode?.Entry ?? default;
            var registry = GetRegistry();

            // Create Folder
            menu.AddItem(new GUIContent("Create Folder"), false, () =>
            {
                var parentId = data.Node?.IsFolder == true ? data.Node.Id : data.Node?.ParentId ?? string.Empty;
                TextPromptWindow.Open("Create Folder", "Folder Name:", "", name =>
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        registry.CreateFolder(parentId, name);
                        RequestRebuild();
                    }
                });
            });

            // Create Leaf
            menu.AddItem(new GUIContent("Create Item"), false, () =>
            {
                var parentId = data.Node?.IsFolder == true ? data.Node.Id : data.Node?.ParentId ?? string.Empty;
                TextPromptWindow.Open("Create Item", "Item Name:", "", name =>
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        registry.CreateLeaf(parentId, name);
                        RequestRebuild();
                    }
                });
            });

            menu.AddSeparator("");

            // Copy (Ctrl+C)
            if (data.Node != null)
            {
                menu.AddItem(new GUIContent("Copy %c"), false, () =>
                {
                    CopyNodeToClipboard(registry, data.Node);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Copy %c"));
            }

            // Paste (Ctrl+V)
            if (HasClipboardData())
            {
                menu.AddItem(new GUIContent("Paste %v"), false, () =>
                {
                    var targetParentId = data.Node?.IsFolder == true ? data.Node.Id : data.Node?.ParentId ?? string.Empty;
                    PasteFromClipboard(registry, targetParentId);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Paste %v"));
            }

            menu.AddSeparator("");

            // Rename
            if (data.Node != null)
            {
                menu.AddItem(new GUIContent("Rename"), false, () =>
                {
                    TextPromptWindow.Open("Rename", "New Name:", data.Node.Name, newName =>
                    {
                        if (!string.IsNullOrWhiteSpace(newName))
                        {
                            registry.RenameNode(data.Node.Id, newName);
                            RequestRebuild();
                        }
                    });
                });

                menu.AddItem(new GUIContent("Delete"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Delete",
                        $"Delete '{data.Node.Name}' and all descendants?", "Delete", "Cancel"))
                    {
                        registry.DeleteNodeAndDescendants(data.Node.Id);
                        _selectedNodeId = null;
                        RequestRebuild();
                    }
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Rename"));
                menu.AddDisabledItem(new GUIContent("Delete"));
            }

            // カスタム項目
            AddCustomContextMenuItems(menu, data);

            menu.ShowAsContext();
        }

        protected virtual bool CanDrop(
            PathTreeNode<RegistryNodeData<TNode>> dragging,
            PathTreeNode<RegistryNodeData<TNode>> target,
            TreeDropPosition position)
        {
            if (dragging?.Entry.Node == null) return false;
            if (target?.Entry.Node == null) return position != TreeDropPosition.Inside;

            var dragNode = dragging.Entry.Node;
            var targetNode = target.Entry.Node;

            // 自分自身にはドロップ不可
            if (dragNode.Id == targetNode.Id) return false;

            // 子孫にはドロップ不可
            var registry = GetRegistry();
            if (registry.IsAncestor(dragNode.Id, targetNode.Id)) return false;

            // Inside の場合、ターゲットはフォルダでなければならない
            if (position == TreeDropPosition.Inside && !targetNode.IsFolder)
                return false;

            return true;
        }

        protected virtual void OnDrop(
            PathTreeNode<RegistryNodeData<TNode>> dragging,
            PathTreeNode<RegistryNodeData<TNode>> target,
            TreeDropPosition position)
        {
            if (dragging?.Entry.Node == null) return;

            var registry = GetRegistry();
            var dragNode = dragging.Entry.Node;
            var targetNode = target?.Entry.Node;

            switch (position)
            {
                case TreeDropPosition.Inside:
                    if (targetNode != null && targetNode.IsFolder)
                    {
                        registry.MoveNode(dragNode.Id, targetNode.Id);
                    }
                    break;

                case TreeDropPosition.Before:
                case TreeDropPosition.After:
                    // 兄弟として挿入
                    var newParentId = targetNode?.ParentId ?? string.Empty;
                    registry.MoveNode(dragNode.Id, newParentId);

                    // 順序も並べ替え
                    ReorderAfterDrop(registry, dragNode.Id, targetNode?.Id, newParentId, position);
                    break;
            }

            _needsRebuild = true;
        }

        /// <summary>ドロップ後の順序並べ替え。</summary>
        protected virtual void ReorderAfterDrop(
            TRegistry registry,
            string dragNodeId,
            string targetNodeId,
            string parentId,
            TreeDropPosition position)
        {
            var siblings = new List<string>();
            foreach (var child in registry.EnumerateChildren(parentId))
            {
                if (child.Id != dragNodeId)
                    siblings.Add(child.Id);
            }

            if (string.IsNullOrEmpty(targetNodeId))
            {
                siblings.Add(dragNodeId);
            }
            else
            {
                var idx = siblings.IndexOf(targetNodeId);
                if (idx >= 0)
                {
                    if (position == TreeDropPosition.After)
                        idx++;
                    siblings.Insert(idx, dragNodeId);
                }
                else
                {
                    siblings.Add(dragNodeId);
                }
            }

            registry.ReorderSiblings(parentId, siblings);
        }

        // ------------------------------------------------------------
        // Utility: ツリー構築ヘルパー
        // ------------------------------------------------------------

        /// <summary>
        /// Registry のノードリストから PathTreeNode を構築するヘルパー。
        /// 派生クラスの BuildTree で使用可能。
        /// </summary>
        protected PathTreeNode<RegistryNodeData<TNode>> BuildTreeFromRegistry(
            TRegistry registry,
            Func<TNode, RegistryNodeData<TNode>> createNodeData)
        {
            var root = new PathTreeNode<RegistryNodeData<TNode>>
            {
                Segment = string.Empty,
                FullPath = string.Empty,
                HasEntry = false
            };

            // ID -> PathTreeNode のマッピング
            var nodeMap = new Dictionary<string, PathTreeNode<RegistryNodeData<TNode>>>(StringComparer.Ordinal)
            {
                [string.Empty] = root
            };

            // まずすべてのノードを PathTreeNode として作成
            foreach (var node in registry.Nodes)
            {
                var data = createNodeData(node);
                var pathNode = new PathTreeNode<RegistryNodeData<TNode>>
                {
                    Segment = node.Name,
                    FullPath = data.DisplayPath,
                    HasEntry = true,
                    Entry = data
                };
                nodeMap[node.Id] = pathNode;
            }

            // 親子関係を構築
            foreach (var node in registry.Nodes)
            {
                if (!nodeMap.TryGetValue(node.Id, out var pathNode))
                    continue;

                var parentId = string.IsNullOrEmpty(node.ParentId) ? string.Empty : node.ParentId;
                if (nodeMap.TryGetValue(parentId, out var parentPathNode))
                {
                    parentPathNode.Children.Add(pathNode);
                }
                else
                {
                    // 親が見つからない場合はルートに追加
                    root.Children.Add(pathNode);
                }
            }

            // ソート（Settings で有効な場合のみ）
            var settings = GetSettings();
            if (settings == null || settings.EnableTreeSort)
            {
                SortTreeRecursive(root);
            }

            return root;
        }

        /// <summary>ツリーを再帰的にソートする。</summary>
        protected virtual void SortTreeRecursive(PathTreeNode<RegistryNodeData<TNode>> node)
        {
            node.Children.Sort((a, b) =>
            {
                // フォルダ優先
                var aIsFolder = a.Entry.IsFolder ? 0 : 1;
                var bIsFolder = b.Entry.IsFolder ? 0 : 1;
                if (aIsFolder != bIsFolder)
                    return aIsFolder.CompareTo(bIsFolder);

                // 名前順
                return string.Compare(a.Segment, b.Segment, StringComparison.Ordinal);
            });

            foreach (var child in node.Children)
            {
                SortTreeRecursive(child);
            }
        }

        /// <summary>ツリーの再構築をリクエストする。</summary>
        protected void RequestRebuild()
        {
            _needsRebuild = true;
            Repaint();
        }

        // ------------------------------------------------------------
        // Copy & Paste
        // ------------------------------------------------------------

        /// <summary>クリップボードにデータがあるか</summary>
        protected bool HasClipboardData()
        {
            return _clipboard != null && _clipboard.Nodes.Count > 0;
        }

        /// <summary>ペースト先の親 ID を取得</summary>
        protected string GetPasteTargetParentId(TRegistry registry)
        {
            if (string.IsNullOrEmpty(_selectedNodeId))
                return string.Empty;

            var node = registry.FindNode(_selectedNodeId);
            if (node == null)
                return string.Empty;

            // フォルダが選択されていればその中へ、リーフなら同じ親へ
            return node.IsFolder ? node.Id : (node.ParentId ?? string.Empty);
        }

        /// <summary>ノードをクリップボードにコピー</summary>
        protected virtual void CopyNodeToClipboard(TRegistry registry, TNode node)
        {
            if (node == null) return;

            _clipboard = new RegistryClipboard
            {
                RootNodeId = node.Id
            };

            // ルートノードをコピー
            AddNodeToClipboard(node);

            // フォルダの場合は子孫もコピー
            if (node.IsFolder)
            {
                foreach (var descendant in registry.EnumerateDescendants(node.Id))
                {
                    AddNodeToClipboard(descendant);
                }
            }

            Debug.Log($"Copied: {node.Name} ({_clipboard.Nodes.Count} node(s))");
        }

        /// <summary>クリップボードにノードを追加</summary>
        protected void AddNodeToClipboard(TNode node)
        {
            var clipData = new ClipboardNodeData
            {
                NodeJson = JsonUtility.ToJson(node),
                NodeTypeName = node.GetType().AssemblyQualifiedName,
                OriginalId = node.Id,
                OriginalParentId = node.ParentId,
                IsFolder = node.IsFolder
            };
            _clipboard.Nodes.Add(clipData);
        }

        /// <summary>クリップボードからペースト</summary>
        protected virtual void PasteFromClipboard(TRegistry registry, string targetParentId)
        {
            if (_clipboard == null || _clipboard.Nodes.Count == 0) return;

            // 旧 ID → 新 ID のマッピング
            var idMapping = new Dictionary<string, string>();

            // まずすべてのノードの新 ID を生成
            foreach (var clipData in _clipboard.Nodes)
            {
                idMapping[clipData.OriginalId] = Guid.NewGuid().ToString("N");
            }

            // ルートノードの新しい親 ID
            var rootOriginalId = _clipboard.RootNodeId;

            // 各ノードを復元してレジストリに追加
            foreach (var clipData in _clipboard.Nodes)
            {
                var newNode = DeserializeNode(clipData);
                if (newNode == null) continue;

                // 新しい ID を設定
                var newId = idMapping[clipData.OriginalId];
                SetNodeId(newNode, newId);

                // 親 ID を設定（ルートノードは targetParentId、それ以外は新しいマッピング済み親 ID）
                if (clipData.OriginalId == rootOriginalId)
                {
                    newNode.ParentId = targetParentId ?? string.Empty;
                }
                else
                {
                    // 元の親 ID から新しい親 ID を取得
                    if (idMapping.TryGetValue(clipData.OriginalParentId, out var newParentId))
                    {
                        newNode.ParentId = newParentId;
                    }
                    else
                    {
                        // 親がマッピングにない場合はルートと同じ親に
                        newNode.ParentId = targetParentId ?? string.Empty;
                    }
                }

                // 名前の重複チェック（同じ親内で同名がある場合はサフィックス追加）
                newNode.Name = GetUniqueName(registry, newNode.ParentId, newNode.Name);

                // レジストリに追加
                AddNodeToRegistry(registry, newNode);
            }

            RequestRebuild();

            var rootNode = _clipboard.Nodes.Find(n => n.OriginalId == rootOriginalId);
            Debug.Log($"Pasted: {_clipboard.Nodes.Count} node(s)");
        }

        /// <summary>JSON からノードをデシリアライズ</summary>
        protected virtual TNode DeserializeNode(ClipboardNodeData clipData)
        {
            try
            {
                // TNode 型として直接デシリアライズ
                var node = JsonUtility.FromJson<TNode>(clipData.NodeJson);
                return node;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to deserialize node: {ex.Message}");
                return null;
            }
        }

        /// <summary>ノードの ID を設定（リフレクションを使用）</summary>
        protected void SetNodeId(TNode node, string newId)
        {
            // HierarchyNodeBase の protected フィールド "id" に書き込む
            var field = typeof(HierarchyNodeBase).GetField("id",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(node, newId);
        }

        /// <summary>レジストリにノードを追加</summary>
        protected virtual void AddNodeToRegistry(TRegistry registry, TNode node)
        {
            // リフレクションで nodes リストに直接追加
            var field = typeof(HierarchyRegistryBase<TNode>).GetField("nodes",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field?.GetValue(registry) is List<TNode> nodes)
            {
                // Undo 登録
                UnityEditor.Undo.RecordObject(registry, "Paste Node");
                nodes.Add(node);
                UnityEditor.EditorUtility.SetDirty(registry);
            }
        }

        /// <summary>同じ親内でユニークな名前を取得</summary>
        protected string GetUniqueName(TRegistry registry, string parentId, string baseName)
        {
            var existingNames = new HashSet<string>();
            foreach (var sibling in registry.EnumerateChildren(parentId))
            {
                existingNames.Add(sibling.Name);
            }

            if (!existingNames.Contains(baseName))
                return baseName;

            // サフィックスを追加
            int suffix = 1;
            string newName;
            do
            {
                newName = $"{baseName} ({suffix++})";
            } while (existingNames.Contains(newName));

            return newName;
        }
    }
}
#endif
