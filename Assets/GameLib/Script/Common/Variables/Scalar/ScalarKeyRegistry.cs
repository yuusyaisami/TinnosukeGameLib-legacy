using UnityEngine;
using Game.Registry;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Scalar
{
    /// <summary>
    /// ScalarKey を階層管理する Registry。
    /// CSV インポート機能を持ち、以下のフォーマットに対応:
    /// Type,Path,ExplicitKey,Description,Obsolete,Tags
    /// </summary>
    [CreateAssetMenu(
        fileName = "ScalarKeyRegistry",
        menuName = "Game/Registry/Scalar/Scalar Key Registry")]
    public sealed class ScalarKeyRegistry : HierarchyRegistryBase<ScalarKeyNode>
    {
#if UNITY_EDITOR
        [Header("CSV Import")]
        [Tooltip("CSV フォーマット: Type,Path,ExplicitKey,Description,Obsolete,Tags")]
        [SerializeField] TextAsset csvFile;
#endif

        /// <summary>
        /// 葉ノード用のキー文字列を取得。
        /// ExplicitKey が設定されていればそれを使用、なければドット区切りのパスを自動生成。
        /// </summary>
        public override string GetKeyString(ScalarKeyNode node)
        {
            if (node == null || node.IsFolder) return string.Empty;

            if (!string.IsNullOrEmpty(node.ExplicitKey))
                return node.ExplicitKey;

            // パスからドット区切りキーを生成
            var path = GetDisplayPath(node);
            if (string.IsNullOrEmpty(path)) return string.Empty;

            // スラッシュをドットに変換
            return path.Replace("/", ".");
        }

        /// <summary>
        /// 葉ノードの初期化。
        /// </summary>
        protected override void InitializeLeafNode(ScalarKeyNode node)
        {
            base.InitializeLeafNode(node);
            node.ExplicitKey = string.Empty;
            node.Obsolete = false;
            node.Tags = null;
        }

        /// <summary>
        /// 便利メソッド：キーを作成する。
        /// </summary>
        public ScalarKey CreateKey(string path)
        {
            if (string.IsNullOrEmpty(path)) return default;
            return new ScalarKey(path);
        }

#if UNITY_EDITOR
        /// <summary>
        /// CSV インポート時のカスタム処理。
        /// CSV フォーマット: Type,Path,ExplicitKey,Description,Obsolete,Tags
        /// </summary>
        protected override void OnCsvRowImport(ScalarKeyNode node, CsvImportRowData rowData, bool isNew)
        {
            base.OnCsvRowImport(node, rowData, isNew);

            // フォルダの場合は追加設定不要
            if (node.IsFolder) return;

            var cols = rowData.RawColumns;

            // Column 2: ExplicitKey (空の場合はパスから自動生成)
            if (cols.Length > 2 && !string.IsNullOrWhiteSpace(cols[2]))
            {
                node.ExplicitKey = cols[2].Trim();
            }
            else if (isNew || string.IsNullOrEmpty(node.ExplicitKey))
            {
                // ExplicitKey が空ならクリア（パスベースのキー生成を使用）
                node.ExplicitKey = string.Empty;
            }

            // Column 3: Description
            if (cols.Length > 3 && !string.IsNullOrWhiteSpace(cols[3]))
            {
                node.Description = cols[3].Trim();
            }

            // Column 4: Obsolete (true/false/1/0)
            if (cols.Length > 4 && !string.IsNullOrWhiteSpace(cols[4]))
            {
                var obsoleteStr = cols[4].Trim().ToLowerInvariant();
                node.Obsolete = obsoleteStr == "true" || obsoleteStr == "1";
            }

            // Column 5: Tags (カンマまたはセミコロン区切り)
            if (cols.Length > 5 && !string.IsNullOrWhiteSpace(cols[5]))
            {
                var tagsStr = cols[5].Trim();
                if (!string.IsNullOrEmpty(tagsStr))
                {
                    node.Tags = tagsStr.Split(new[] { ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < node.Tags.Length; i++)
                    {
                        node.Tags[i] = node.Tags[i].Trim();
                    }
                }
                else
                {
                    node.Tags = null;
                }
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
                Debug.LogWarning("[ScalarKeyRegistry] CSV file is not assigned.");
                return;
            }

            ImportFromCsv(csvFile.text, hasHeader: true);
            Debug.Log($"[ScalarKeyRegistry] Imported CSV: {csvFile.name}");
        }
#endif
    }
}
