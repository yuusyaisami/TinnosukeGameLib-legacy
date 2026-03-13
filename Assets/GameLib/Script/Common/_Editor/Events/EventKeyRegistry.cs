using System.Collections.Generic;
using UnityEngine;
using Game.Registry;
using Game.EventKey;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.EventKey
{
    /// <summary>
    /// イベントキーの階層構造レジストリ。
    /// CSV インポート機能を持ち、以下のフォーマットに対応:
    /// Type,Path,ExplicitKey,Description
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Registry/Events/Event Key Registry")]
    public sealed class EventKeyRegistry : HierarchyRegistryBase<EventKeyNode>
    {
#if UNITY_EDITOR
        [Header("CSV Import")]
        [Tooltip("CSV フォーマット: Type,Path,ExplicitKey,Description")]
        [SerializeField] TextAsset csvFile;
#endif

        /// <summary>
        /// 実際に使用されるキー文字列を取得。
        /// explicitKey があればそれ、なければ displayPath の '/' を '.' にしたもの。
        /// </summary>
        public override string GetKeyString(EventKeyNode node)
        {
            if (node == null || node.IsFolder)
                return string.Empty;

            if (!string.IsNullOrEmpty(node.ExplicitKey))
                return node.ExplicitKey;

            var path = GetDisplayPath(node.Id);
            return path.Replace('/', '.');
        }

        /// <summary>
        /// キーノードを作成する（explicitKey, description 指定可能）。
        /// </summary>
        public EventKeyNode CreateKey(string parentId, string name, string explicitKey = null, string description = null)
        {
            var node = CreateLeaf(parentId, name);
            node.ExplicitKey = explicitKey;
            node.Description = description;
            return node;
        }

#if UNITY_EDITOR
        /// <summary>
        /// CSV インポート時のカスタム処理。
        /// CSV フォーマット: Type,Path,ExplicitKey,Description
        /// </summary>
        protected override void OnCsvRowImport(EventKeyNode node, CsvImportRowData rowData, bool isNew)
        {
            base.OnCsvRowImport(node, rowData, isNew);

            // フォルダの場合は Description のみ設定可能
            var cols = rowData.RawColumns;

            // Column 2: ExplicitKey (リーフのみ有効)
            if (!node.IsFolder && cols.Length > 2 && !string.IsNullOrWhiteSpace(cols[2]))
            {
                node.ExplicitKey = cols[2].Trim();
            }
            else if (!node.IsFolder && (isNew || string.IsNullOrEmpty(node.ExplicitKey)))
            {
                // ExplicitKey が空ならクリア（パスベースのキー生成を使用）
                node.ExplicitKey = string.Empty;
            }

            // Column 3: Description (フォルダ・リーフ両方で有効)
            if (cols.Length > 3 && !string.IsNullOrWhiteSpace(cols[3]))
            {
                node.Description = cols[3].Trim();
            }
        }

        /// <summary>
        /// Inspector から CSV インポートを実行
        /// </summary>
        [ContextMenu("Import from CSV")]
        public void ImportFromCsvFile()
        {
            if (csvFile == null)
            {
                Debug.LogWarning("[EventKeyRegistry] CSV file is not assigned.");
                return;
            }

            ImportFromCsv(csvFile.text, hasHeader: true);
            Debug.Log($"[EventKeyRegistry] Imported CSV: {csvFile.name}");
        }
#endif
    }
}
