using UnityEngine;
using Game.Registry;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.MaterialFx
{
    /// <summary>
    /// MaterialFx プロパティを階層管理する ScriptableObject。
    /// Editor で編集し、Runtime で参照する。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Registry/MaterialFx/MaterialFx Property Registry")]
    public sealed class MaterialFxPropertyRegistrySO : HierarchyRegistryBase<MaterialFxPropertyNode>
    {
#if UNITY_EDITOR
        [Header("CSV Import")]
        [SerializeField] TextAsset csvFile;
#endif

        /// <summary>
        /// ランタイム用の Registry インスタンスを作成して返す。
        /// </summary>
        public IMaterialFxPropertyRegistry GetRuntime() => new MaterialFxPropertyRegistryRuntime(this);

        /// <summary>
        /// 葉ノード用のキー文字列を取得（StableKey を返す）
        /// </summary>
        public override string GetKeyString(MaterialFxPropertyNode node)
        {
            if (node == null || node.IsFolder) return string.Empty;
            return node.StableKey;
        }

        /// <summary>
        /// 葉ノードの初期化
        /// </summary>
        protected override void InitializeLeafNode(MaterialFxPropertyNode node)
        {
            base.InitializeLeafNode(node);
            node.StableKey = string.Empty;
            node.Sender = MaterialFxSenderKind.BaseShader;
            node.ValueType = ValueKind.Float;
            node.ShaderPropertyName = string.Empty;
        }

        /// <summary>
        /// デフォルトの StableKey を計算（表示パスから生成）
        /// </summary>
        public string ComputeDefaultStableKey(MaterialFxPropertyNode node)
        {
            return GetDisplayPath(node);
        }

#if UNITY_EDITOR
        /// <summary>
        /// CSV インポート時のカスタム処理。
        /// CSV フォーマット: Type,Path,Sender,ValueType,ShaderPropertyName,StableKey
        /// </summary>
        protected override void OnCsvRowImport(MaterialFxPropertyNode node, CsvImportRowData rowData, bool isNew)
        {
            base.OnCsvRowImport(node, rowData, isNew);

            // フォルダの場合は追加設定不要
            if (node.IsFolder) return;

            var cols = rowData.RawColumns;

            // Column 2: Sender (BaseShader)
            if (cols.Length > 2 && !string.IsNullOrWhiteSpace(cols[2]))
            {
                var senderStr = cols[2].Trim();
                if (System.Enum.TryParse<MaterialFxSenderKind>(senderStr, true, out var sender))
                    node.Sender = sender;
            }

            // Column 3: ValueType (Float, Float2, Float3, Float4, Int, Bool, Color, etc.)
            if (cols.Length > 3 && !string.IsNullOrWhiteSpace(cols[3]))
            {
                var valueTypeStr = cols[3].Trim();
                if (System.Enum.TryParse<ValueKind>(valueTypeStr, true, out var valueType))
                    node.ValueType = valueType;
            }

            // Column 4: ShaderPropertyName
            if (cols.Length > 4 && !string.IsNullOrWhiteSpace(cols[4]))
            {
                node.ShaderPropertyName = cols[4].Trim();
            }

            // Column 5: StableKey (空の場合はパスから自動生成)
            if (cols.Length > 5 && !string.IsNullOrWhiteSpace(cols[5]))
            {
                node.StableKey = cols[5].Trim();
            }
            else if (isNew || string.IsNullOrEmpty(node.StableKey))
            {
                // StableKey が空なら Path から生成
                node.StableKey = rowData.Path;
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
                Debug.LogWarning("CSV file is not assigned.");
                return;
            }

            ImportFromCsv(csvFile.text, hasHeader: true);
            Debug.Log($"Imported CSV: {csvFile.name}");
        }
#endif
    }
}
