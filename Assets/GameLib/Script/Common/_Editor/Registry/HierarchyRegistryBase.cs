using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Registry
{
    // ================================================================
    // HierarchyRegistryBase<TNode> - 階層 Registry SO の基底クラス
    // ================================================================

    /// <summary>
    /// CSV インポート用の基本データ構造。
    /// 派生クラスで追加フィールドを読み取る場合はこれを継承する。
    /// </summary>
    public class CsvImportRowData
    {
        public bool IsFolder;
        public string Path;
        public string[] RawColumns; // 全カラムデータ（派生で使用）
    }

    /// <summary>
    /// 階層 Registry SO の基底クラス。
    /// 純粋なデータ SO であり、Editor/Runtime 両方で使用可能。
    /// </summary>
    public abstract class HierarchyRegistryBase<TNode> : ScriptableObject
        where TNode : HierarchyNodeBase, new()
    {
        [SerializeField] protected List<TNode> nodes = new();

        public IReadOnlyList<TNode> Nodes => nodes;
        public int Count => nodes.Count;

        // ------------------------------------------------------------
        // 仮想メソッド（派生でオーバーライド）
        // ------------------------------------------------------------

        /// <summary>フォルダノード作成後の追加初期化（派生で追加フィールド設定）</summary>
        protected virtual void InitializeFolderNode(TNode node) { }

        /// <summary>リーフノード作成後の追加初期化（派生で追加フィールド設定）</summary>
        protected virtual void InitializeLeafNode(TNode node) { }

        /// <summary>キー文字列を取得する。派生でオーバーライドしてカスタムキー生成を行う。</summary>
        public virtual string GetKeyString(TNode node)
        {
            if (node == null || node.IsFolder) return string.Empty;
            return GetDisplayPath(node.Id);
        }

        // ------------------------------------------------------------
        // フォルダ・リーフ作成
        // ------------------------------------------------------------

        public TNode CreateFolder(string parentId, string name)
        {
            name = SanitizeSegmentName(name);
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Folder name cannot be empty.", nameof(name));

            RecordUndo("Create Folder");

            var node = new TNode();
            node.Initialize(parentId ?? string.Empty, name, isFolder: true);
            InitializeFolderNode(node);
            nodes.Add(node);
            MarkDirty();
            return node;
        }

        public TNode CreateLeaf(string parentId, string name)
        {
            name = SanitizeSegmentName(name);
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Leaf name cannot be empty.", nameof(name));

            RecordUndo("Create Item");

            var node = new TNode();
            node.Initialize(parentId ?? string.Empty, name, isFolder: false);
            InitializeLeafNode(node);
            nodes.Add(node);
            MarkDirty();
            return node;
        }

        // ------------------------------------------------------------
        // ノード操作
        // ------------------------------------------------------------

        public void RenameNode(string nodeId, string newName)
        {
            var node = FindNode(nodeId);
            if (node == null) return;

            newName = SanitizeSegmentName(newName);
            if (string.IsNullOrEmpty(newName)) return;

            RecordUndo("Rename Node");
            node.Name = newName;
            MarkDirty();
        }

        public virtual void DeleteNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            RecordUndo("Delete Node");
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                var node = nodes[i];
                if (node == null || node.Id != nodeId)
                    continue;

                if (OnDeleteNode(node))
                    nodes.RemoveAt(i);
                break;
            }
            MarkDirty();
        }

        public virtual void DeleteNodeAndDescendants(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;

            RecordUndo("Delete Node");

            var toRemove = new HashSet<string> { nodeId };
            CollectDescendantIds(nodeId, toRemove);
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                var node = nodes[i];
                if (node == null || !toRemove.Contains(node.Id))
                    continue;

                if (OnDeleteNode(node))
                    nodes.RemoveAt(i);
            }
            MarkDirty();
        }

        /// <summary>
        /// DeleteNode/DeleteNodeAndDescendants の削除処理フック。
        /// true を返すと nodes から実際に削除される。
        /// </summary>
        protected virtual bool OnDeleteNode(TNode node) => true;

        public void MoveNode(string nodeId, string newParentId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;

            var node = FindNode(nodeId);
            if (node == null) return;

            if (nodeId == newParentId) return;
            if (!string.IsNullOrEmpty(newParentId) && IsAncestor(nodeId, newParentId)) return;

            RecordUndo("Move Node");
            node.ParentId = newParentId ?? string.Empty;
            MarkDirty();
        }

        public void ReorderSiblings(string parentId, List<string> orderedIds)
        {
            if (orderedIds == null || orderedIds.Count == 0) return;

            RecordUndo("Reorder Nodes");

            var pid = parentId ?? string.Empty;
            var siblings = new List<TNode>();
            var otherNodes = new List<TNode>();

            foreach (var node in nodes)
            {
                if ((node.ParentId ?? string.Empty) == pid)
                    siblings.Add(node);
                else
                    otherNodes.Add(node);
            }

            var ordered = new List<TNode>(siblings.Count);
            foreach (var id in orderedIds)
            {
                var found = siblings.Find(n => n.Id == id);
                if (found != null)
                {
                    ordered.Add(found);
                    siblings.Remove(found);
                }
            }
            ordered.AddRange(siblings);

            nodes.Clear();
            nodes.AddRange(otherNodes);
            nodes.AddRange(ordered);
            MarkDirty();
        }

        // ------------------------------------------------------------
        // 検索・列挙
        // ------------------------------------------------------------

        public TNode FindNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;
            return nodes.Find(n => n.Id == nodeId);
        }

        public int FindNodeIndex(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return -1;
            return nodes.FindIndex(n => n.Id == nodeId);
        }

        public TNode FindNodeByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            foreach (var node in nodes)
            {
                if (GetDisplayPath(node.Id) == path)
                    return node;
            }
            return null;
        }

        public IEnumerable<TNode> EnumerateChildren(string parentId)
        {
            var pid = parentId ?? string.Empty;
            foreach (var node in nodes)
            {
                if ((node.ParentId ?? string.Empty) == pid)
                    yield return node;
            }
        }

        public IEnumerable<TNode> EnumerateDescendants(string parentId)
        {
            var pid = parentId ?? string.Empty;
            foreach (var node in nodes)
            {
                if ((node.ParentId ?? string.Empty) == pid)
                {
                    yield return node;
                    foreach (var desc in EnumerateDescendants(node.Id))
                        yield return desc;
                }
            }
        }

        public bool IsAncestor(string ancestorId, string nodeId)
        {
            if (string.IsNullOrEmpty(ancestorId) || string.IsNullOrEmpty(nodeId))
                return false;

            var current = FindNode(nodeId);
            while (current != null && !string.IsNullOrEmpty(current.ParentId))
            {
                if (current.ParentId == ancestorId)
                    return true;
                current = FindNode(current.ParentId);
            }
            return false;
        }

        // ------------------------------------------------------------
        // パス計算
        // ------------------------------------------------------------

        public string GetDisplayPath(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return string.Empty;

            var node = FindNode(nodeId);
            if (node == null) return string.Empty;

            var segments = new List<string> { node.Name };
            var current = node;

            while (!string.IsNullOrEmpty(current.ParentId))
            {
                current = FindNode(current.ParentId);
                if (current == null) break;
                segments.Add(current.Name);
            }

            segments.Reverse();
            return string.Join("/", segments);
        }

        public string GetDisplayPath(TNode node)
        {
            if (node == null) return string.Empty;
            return GetDisplayPath(node.Id);
        }

        // ------------------------------------------------------------
        // ヘルパー
        // ------------------------------------------------------------

        void CollectDescendantIds(string parentId, HashSet<string> result)
        {
            foreach (var node in nodes)
            {
                if ((node.ParentId ?? string.Empty) == parentId)
                {
                    result.Add(node.Id);
                    CollectDescendantIds(node.Id, result);
                }
            }
        }

        protected static string SanitizeSegmentName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            name = name.Trim();
            name = name.Replace("/", "_").Replace("\\", "_");
            return name;
        }

        protected void MarkDirty()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// Undo 用に現在の状態を記録する。
        /// </summary>
        protected void RecordUndo(string actionName)
        {
#if UNITY_EDITOR
            Undo.RecordObject(this, actionName);
#endif
        }

        // ------------------------------------------------------------
        // CSV Import
        // ------------------------------------------------------------

#if UNITY_EDITOR
        /// <summary>
        /// CSV ファイルからデータをインポートする。
        /// 既存データは削除せず、パスが一致する場合は上書き、新規の場合は追加。
        /// </summary>
        /// <param name="csvText">CSV テキスト</param>
        /// <param name="hasHeader">最初の行がヘッダーかどうか</param>
        public virtual void ImportFromCsv(string csvText, bool hasHeader = true)
        {
            if (string.IsNullOrWhiteSpace(csvText)) return;

            RecordUndo("Import CSV");

            var lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int startLine = hasHeader ? 1 : 0;

            // パス → ノード ID のキャッシュ（フォルダ解決用）
            var pathToNodeId = new Dictionary<string, string>();
            foreach (var node in nodes)
            {
                var path = GetDisplayPath(node.Id);
                if (!string.IsNullOrEmpty(path))
                    pathToNodeId[path] = node.Id;
            }

            for (int i = startLine; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                var columns = ParseCsvLine(line);
                if (columns.Length < 2) continue;

                var rowData = new CsvImportRowData
                {
                    IsFolder = columns[0].Trim().ToLowerInvariant() == "folder",
                    Path = columns[1].Trim(),
                    RawColumns = columns
                };

                if (string.IsNullOrEmpty(rowData.Path)) continue;

                // パスからフォルダ構造を解析・作成
                var pathSegments = rowData.Path.Split('/');
                string currentParentId = string.Empty;
                string currentPath = string.Empty;

                // 最後のセグメントを除いた親フォルダを作成/取得
                for (int j = 0; j < pathSegments.Length - 1; j++)
                {
                    var segment = pathSegments[j].Trim();
                    if (string.IsNullOrEmpty(segment)) continue;

                    currentPath = string.IsNullOrEmpty(currentPath) ? segment : $"{currentPath}/{segment}";

                    if (pathToNodeId.TryGetValue(currentPath, out var existingId))
                    {
                        currentParentId = existingId;
                    }
                    else
                    {
                        // フォルダを作成
                        var folderNode = CreateFolderInternal(currentParentId, segment);
                        pathToNodeId[currentPath] = folderNode.Id;
                        currentParentId = folderNode.Id;
                    }
                }

                // 最後のセグメント（実際のノード）
                var nodeName = pathSegments[pathSegments.Length - 1].Trim();
                if (string.IsNullOrEmpty(nodeName)) continue;

                var fullPath = rowData.Path;

                // 既存ノードを検索
                if (pathToNodeId.TryGetValue(fullPath, out var existingNodeId))
                {
                    // 上書き
                    var existingNode = FindNode(existingNodeId);
                    if (existingNode != null)
                    {
                        OnCsvRowImport(existingNode, rowData, isNew: false);
                    }
                }
                else
                {
                    // 新規作成
                    TNode newNode;
                    if (rowData.IsFolder)
                    {
                        newNode = CreateFolderInternal(currentParentId, nodeName);
                    }
                    else
                    {
                        newNode = CreateLeafInternal(currentParentId, nodeName);
                    }
                    pathToNodeId[fullPath] = newNode.Id;
                    OnCsvRowImport(newNode, rowData, isNew: true);
                }
            }

            MarkDirty();
        }

        /// <summary>
        /// CSV の1行をパースする（カンマ区切り、ダブルクォート対応）。
        /// </summary>
        protected string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // ダブルクォート内でのエスケープチェック
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++; // skip next quote
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
            }

            result.Add(current.ToString());
            return result.ToArray();
        }

        /// <summary>
        /// CSV 行のインポート時に呼ばれる。派生クラスで追加フィールドを設定する。
        /// </summary>
        /// <param name="node">対象ノード</param>
        /// <param name="rowData">CSV 行データ</param>
        /// <param name="isNew">新規作成の場合 true、上書きの場合 false</param>
        protected virtual void OnCsvRowImport(TNode node, CsvImportRowData rowData, bool isNew)
        {
            // 基底クラスでは何もしない。派生でオーバーライドして追加フィールドを設定する。
        }

        /// <summary>
        /// フォルダ作成（内部用、Undo なし）
        /// </summary>
        private TNode CreateFolderInternal(string parentId, string name)
        {
            name = SanitizeSegmentName(name);
            var node = new TNode();
            node.Initialize(parentId ?? string.Empty, name, isFolder: true);
            InitializeFolderNode(node);
            nodes.Add(node);
            return node;
        }

        /// <summary>
        /// リーフ作成（内部用、Undo なし）
        /// </summary>
        private TNode CreateLeafInternal(string parentId, string name)
        {
            name = SanitizeSegmentName(name);
            var node = new TNode();
            node.Initialize(parentId ?? string.Empty, name, isFolder: false);
            InitializeLeafNode(node);
            nodes.Add(node);
            return node;
        }
#endif
    }
}
