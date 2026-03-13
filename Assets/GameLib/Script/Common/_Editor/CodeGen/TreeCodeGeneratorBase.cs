#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.CodeGen
{
    // ================================================================
    // TreeCodeGeneratorBase - 階層的なコード生成の基底クラス
    // ================================================================
    //
    // ## 概要
    //
    // Registry/Catalog などの階層データから静的クラスを生成する
    // コードジェネレーターの共通基盤。
    //
    // ## 使用方法
    //
    // 1. ICodeGenNode を実装したノードアダプターを作成
    // 2. ICodeGenSettings を実装した設定インターフェースを定義
    // 3. TreeCodeGeneratorBase<TNode, TSettings> を継承
    // 4. 抽象メソッドを実装
    //
    // ## 拡張ポイント
    //
    // - BuildTree: ソースデータからツリーを構築
    // - EmitFieldDeclaration: フィールド宣言のカスタマイズ
    // - EmitAllArrayElement: All配列要素のカスタマイズ
    // - GetAdditionalUsings: 追加のusing文
    // - GetAllArrayType: All配列の型名
    //
    // ================================================================

    // ================================================================
    // ICodeGenNode - コード生成用ノードインターフェース
    // ================================================================

    /// <summary>
    /// コード生成用ノードの共通インターフェース。
    /// 各 Registry/Catalog の Node をラップするアダプターが実装する。
    /// </summary>
    public interface ICodeGenNode
    {
        /// <summary>ノードの表示名/識別名</summary>
        string Name { get; }

        /// <summary>このノードがフォルダ（グループ）かどうか</summary>
        bool IsFolder { get; }

        /// <summary>説明文（XMLドキュメントコメント用）</summary>
        string Description { get; }

        /// <summary>キー値（文字列として出力される値）</summary>
        string KeyValue { get; }
    }

    // ================================================================
    // ICodeGenSettings - コード生成設定インターフェース
    // ================================================================

    /// <summary>
    /// コード生成設定の共通インターフェース。
    /// </summary>
    public interface ICodeGenSettings
    {
        /// <summary>出力ファイルの名前空間</summary>
        string NamespaceName { get; }

        /// <summary>ルートクラス名</summary>
        string RootClassName { get; }

        /// <summary>出力ファイルパス</summary>
        string OutputPath { get; }
    }

    // ================================================================
    // CodeGenTreeNode - 生成用ツリーノード
    // ================================================================

    /// <summary>
    /// コード生成時に使用する汎用ツリーノード。
    /// </summary>
    public sealed class CodeGenTreeNode
    {
        /// <summary>元データのノード（アダプター）</summary>
        public ICodeGenNode Node { get; set; }

        /// <summary>パス（"A/B/C" 形式）</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>子ノード</summary>
        public List<CodeGenTreeNode> Children { get; } = new();
    }

    // ================================================================
    // CodeGenContext - 生成コンテキスト
    // ================================================================

    /// <summary>
    /// コード生成時のコンテキスト情報。
    /// 生成中に共有されるデータを保持。
    /// </summary>
    public sealed class CodeGenContext
    {
        /// <summary>出力用 StringBuilder</summary>
        public StringBuilder Builder { get; } = new(4096);

        /// <summary>All配列に追加するアクセスパス一覧</summary>
        public List<string> AllAccessPaths { get; } = new();

        /// <summary>ルートクラス名</summary>
        public string RootClassName { get; set; } = string.Empty;

        /// <summary>名前空間</summary>
        public string NamespaceName { get; set; } = string.Empty;

        /// <summary>ジェネレーター名（ログ/コメント用）</summary>
        public string GeneratorName { get; set; } = string.Empty;
    }

    // ================================================================
    // TreeCodeGeneratorBase - 基底クラス
    // ================================================================

    /// <summary>
    /// 階層的なコード生成の基底クラス。
    /// 
    /// ## 型パラメータ
    /// 
    /// TSettings: 設定の具象型（ScriptableObject を想定）
    /// </summary>
    public abstract class TreeCodeGeneratorBase<TSettings>
        where TSettings : ScriptableObject, ICodeGenSettings
    {
        // ----------------------------------------------------------------
        // 抽象メソッド - 派生クラスで実装必須
        // ----------------------------------------------------------------

        /// <summary>
        /// ソースデータからツリーを構築する。
        /// </summary>
        /// <returns>ルートノード</returns>
        protected abstract CodeGenTreeNode BuildTree();

        /// <summary>
        /// フィールド宣言を出力する。
        /// 
        /// ## 例
        /// - `public const string FieldName = "value";`
        /// - `public static readonly ScalarKey FieldName = new ScalarKey("key");`
        /// </summary>
        /// <param name="sb">出力先</param>
        /// <param name="indent">インデント（スペース数）</param>
        /// <param name="fieldName">フィールド名</param>
        /// <param name="node">ノード</param>
        protected abstract void EmitFieldDeclaration(
            StringBuilder sb,
            int indent,
            string fieldName,
            ICodeGenNode node);

        /// <summary>
        /// All配列の要素を出力する。
        /// </summary>
        /// <param name="sb">出力先</param>
        /// <param name="indent">インデント</param>
        /// <param name="accessPath">フィールドへのアクセスパス</param>
        protected abstract void EmitAllArrayElement(
            StringBuilder sb,
            int indent,
            string accessPath);

        /// <summary>
        /// ジェネレーター名を取得する（ログ/コメント用）。
        /// </summary>
        protected abstract string GeneratorName { get; }

        /// <summary>
        /// デフォルトの名前空間を取得する。
        /// </summary>
        protected abstract string DefaultNamespace { get; }

        /// <summary>
        /// デフォルトのルートクラス名を取得する。
        /// </summary>
        protected abstract string DefaultRootClassName { get; }

        /// <summary>
        /// デフォルトの出力パスを取得する。
        /// </summary>
        protected abstract string DefaultOutputPath { get; }

        // ----------------------------------------------------------------
        // 仮想メソッド - 必要に応じてオーバーライド
        // ----------------------------------------------------------------

        /// <summary>
        /// 追加のusing文を取得する。
        /// デフォルトは空。
        /// </summary>
        protected virtual IEnumerable<string> GetAdditionalUsings()
        {
            yield break;
        }

        /// <summary>
        /// All配列の型名を取得する。
        /// デフォルトは "string[]"。
        /// </summary>
        protected virtual string GetAllArrayType() => "string[]";

        /// <summary>
        /// All配列の名前を取得する。
        /// デフォルトは "AllKeys"。
        /// </summary>
        protected virtual string GetAllArrayName() => "AllKeys";

        /// <summary>
        /// フィールド出力前にカスタム処理を行う。
        /// フラットフィールド出力などに使用。
        /// </summary>
        protected virtual void OnBeforeEmitFields(CodeGenContext context, CodeGenTreeNode root)
        {
        }

        /// <summary>
        /// クラス内の末尾にカスタム出力を行う。
        /// </summary>
        protected virtual void OnAfterEmitClasses(CodeGenContext context, CodeGenTreeNode root)
        {
        }

        /// <summary>
        /// ノードからフィールド名を決定する。
        /// </summary>
        protected virtual string GetFieldName(ICodeGenNode node, string parentClassName)
        {
            if (node == null) return "Value";

            var name = SanitizeIdentifier(node.Name);
            if (string.IsNullOrEmpty(name)) return "Value";

            // クラス名と同じ名前のフィールドは回避
            if (string.Equals(name, parentClassName, StringComparison.Ordinal))
                name = name + "_";

            return name;
        }

        /// <summary>
        /// XMLコメント用の説明を出力する。
        /// </summary>
        protected virtual void EmitDescription(StringBuilder sb, int indent, string description)
        {
            if (string.IsNullOrEmpty(description)) return;

            var tabs = new string(' ', indent);
            sb.AppendLine($"{tabs}/// <summary>{EscapeXml(description)}</summary>");
        }

        /// <summary>
        /// ノード用のコメント行を取得する（オーバーライドで拡張可能）。
        /// 戻り値の各要素は remarks タグ内に出力される。
        /// null または空の場合は出力しない。
        /// </summary>
        /// <param name="node">対象ノード</param>
        /// <returns>コメント行のリスト（または null/空）</returns>
        protected virtual IEnumerable<string> GetNodeCommentLines(ICodeGenNode node)
        {
            return null;
        }

        /// <summary>
        /// ノード用のコメントを出力する。
        /// summary（Description）と remarks（GetNodeCommentLines）を出力。
        /// </summary>
        protected virtual void EmitNodeComment(StringBuilder sb, int indent, ICodeGenNode node)
        {
            var tabs = new string(' ', indent);
            var description = node?.Description;
            var commentLines = GetNodeCommentLines(node);

            // コメントがあるかチェック
            bool hasDescription = !string.IsNullOrEmpty(description);
            bool hasCommentLines = false;
            if (commentLines != null)
            {
                foreach (var _ in commentLines)
                {
                    hasCommentLines = true;
                    break;
                }
            }

            if (!hasDescription && !hasCommentLines)
                return;

            // summary
            if (hasDescription)
            {
                sb.AppendLine($"{tabs}/// <summary>{EscapeXml(description)}</summary>");
            }

            // remarks
            if (hasCommentLines)
            {
                sb.AppendLine($"{tabs}/// <remarks>");
                foreach (var line in commentLines)
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        sb.AppendLine($"{tabs}/// {EscapeXml(line)}");
                    }
                }
                sb.AppendLine($"{tabs}/// </remarks>");
            }
        }

        // ----------------------------------------------------------------
        // 公開メソッド - 生成実行
        // ----------------------------------------------------------------

        /// <summary>
        /// コード生成を実行する。
        /// </summary>
        /// <param name="settings">設定</param>
        public void Generate(TSettings settings)
        {
            if (settings == null)
            {
                Debug.LogError($"[{GeneratorName}] Settings is null.");
                return;
            }

            var tree = BuildTree();
            if (tree == null)
            {
                Debug.LogError($"[{GeneratorName}] Failed to build tree.");
                return;
            }

            var context = new CodeGenContext
            {
                RootClassName = GetRootClassName(settings),
                NamespaceName = GetNamespaceName(settings),
                GeneratorName = GeneratorName
            };

            GenerateCode(context, tree, settings);
            WriteToFile(context.Builder.ToString(), GetOutputPath(settings));
        }

        // ----------------------------------------------------------------
        // コード生成ロジック
        // ----------------------------------------------------------------

        /// <summary>
        /// コード全体を生成する。
        /// </summary>
        protected virtual void GenerateCode(CodeGenContext context, CodeGenTreeNode root, TSettings settings)
        {
            var sb = context.Builder;

            // ヘッダー
            EmitHeader(sb, context.GeneratorName);

            // using
            EmitUsings(sb);

            // namespace開始
            sb.AppendLine($"namespace {context.NamespaceName}");
            sb.AppendLine("{");

            // クラス開始
            sb.AppendLine($"    public static partial class {context.RootClassName}");
            sb.AppendLine("    {");

            // カスタム前処理
            OnBeforeEmitFields(context, root);

            // ルートクラス名をusedNamesに追加
            var usedNamesInRoot = new HashSet<string>(StringComparer.Ordinal);

            // ルート直下のキー定数
            EmitRootLevelConstants(context, root, usedNamesInRoot);

            // ルート直下のフォルダ → ネストクラス
            foreach (var child in root.Children)
            {
                if (child.Node == null || !child.Node.IsFolder)
                    continue;

                sb.AppendLine();
                EmitNestedClass(context, child, 2, context.RootClassName, context.RootClassName, usedNamesInRoot);
            }

            // カスタム後処理
            OnAfterEmitClasses(context, root);

            // All配列
            EmitAllArray(context);

            // クラス終了
            sb.AppendLine("    }");

            // namespace終了
            sb.AppendLine("}");
        }

        /// <summary>
        /// ヘッダーコメントを出力する。
        /// </summary>
        protected virtual void EmitHeader(StringBuilder sb, string generatorName)
        {
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine($"// This file is generated by {generatorName}.");
            sb.AppendLine("// Do not edit manually.");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine();
        }

        /// <summary>
        /// using文を出力する。
        /// </summary>
        protected virtual void EmitUsings(StringBuilder sb)
        {
            foreach (var u in GetAdditionalUsings())
            {
                sb.AppendLine($"using {u};");
            }

            var hasUsings = false;
            foreach (var _ in GetAdditionalUsings())
            {
                hasUsings = true;
                break;
            }

            if (hasUsings)
            {
                sb.AppendLine();
            }
        }

        /// <summary>
        /// ルートレベルの定数を出力する。
        /// </summary>
        protected virtual void EmitRootLevelConstants(
            CodeGenContext context,
            CodeGenTreeNode root,
            HashSet<string> usedNames)
        {
            var sb = context.Builder;

            foreach (var child in root.Children)
            {
                if (child.Node == null || child.Node.IsFolder)
                    continue;

                var rawFieldName = GetFieldName(child.Node, context.RootClassName);
                var fieldName = MakeUnique(rawFieldName, usedNames);

                EmitNodeComment(sb, 8, child.Node);
                EmitFieldDeclaration(sb, 8, fieldName, child.Node);

                context.AllAccessPaths.Add($"{context.RootClassName}.{fieldName}");
            }
        }

        /// <summary>
        /// ネストクラスを出力する。
        /// </summary>
        protected virtual void EmitNestedClass(
            CodeGenContext context,
            CodeGenTreeNode node,
            int indent,
            string parentClassName,
            string parentAccessPath,
            HashSet<string> parentUsedNames)
        {
            if (node.Node == null || !node.Node.IsFolder)
                return;

            var sb = context.Builder;
            var tabs = new string(' ', indent * 4);

            var className = MakeUniqueClassName(node.Node.Name, parentClassName, parentUsedNames);
            var accessPath = $"{parentAccessPath}.{className}";

            sb.AppendLine($"{tabs}public static partial class {className}");
            sb.AppendLine($"{tabs}{{");

            var usedNamesInThis = new HashSet<string>(StringComparer.Ordinal);

            // キー定数
            foreach (var child in node.Children)
            {
                if (child.Node == null || child.Node.IsFolder)
                    continue;

                var rawFieldName = GetFieldName(child.Node, className);
                var fieldName = MakeUnique(rawFieldName, usedNamesInThis);

                EmitNodeComment(sb, (indent + 1) * 4, child.Node);
                EmitFieldDeclaration(sb, (indent + 1) * 4, fieldName, child.Node);

                context.AllAccessPaths.Add($"{accessPath}.{fieldName}");
            }

            // 子フォルダ → ネストクラス
            foreach (var child in node.Children)
            {
                if (child.Node == null || !child.Node.IsFolder)
                    continue;

                sb.AppendLine();
                EmitNestedClass(context, child, indent + 1, className, accessPath, usedNamesInThis);
            }

            sb.AppendLine($"{tabs}}}");
        }

        /// <summary>
        /// All配列を出力する。
        /// </summary>
        protected virtual void EmitAllArray(CodeGenContext context)
        {
            var sb = context.Builder;

            sb.AppendLine();
            sb.AppendLine($"        public static readonly {GetAllArrayType()} {GetAllArrayName()} = new {GetAllArrayType()}");
            sb.AppendLine("        {");

            foreach (var accessPath in context.AllAccessPaths)
            {
                EmitAllArrayElement(sb, 12, accessPath);
            }

            sb.AppendLine("        };");
        }

        // ----------------------------------------------------------------
        // ファイル書き込み
        // ----------------------------------------------------------------

        /// <summary>
        /// 生成したコードをファイルに書き込む。
        /// </summary>
        protected virtual void WriteToFile(string content, string outputPath)
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(outputPath, content, Encoding.UTF8);
            AssetDatabase.ImportAsset(outputPath);
            Debug.Log($"[{GeneratorName}] Generated: {outputPath}");
        }

        // ----------------------------------------------------------------
        // 設定取得ヘルパー
        // ----------------------------------------------------------------

        /// <summary>
        /// 名前空間を取得する（設定優先、なければデフォルト）。
        /// </summary>
        protected string GetNamespaceName(TSettings settings)
        {
            var ns = settings?.NamespaceName;
            return string.IsNullOrWhiteSpace(ns) ? DefaultNamespace : ns.Trim();
        }

        /// <summary>
        /// ルートクラス名を取得する（設定優先、なければデフォルト）。
        /// </summary>
        protected string GetRootClassName(TSettings settings)
        {
            var name = settings?.RootClassName;
            var sanitized = SanitizeIdentifier(name);
            return string.IsNullOrWhiteSpace(sanitized) ? DefaultRootClassName : sanitized;
        }

        /// <summary>
        /// 出力パスを取得する（設定優先、なければデフォルト）。
        /// </summary>
        protected string GetOutputPath(TSettings settings)
        {
            var path = settings?.OutputPath;
            return string.IsNullOrWhiteSpace(path) ? DefaultOutputPath : path.Trim();
        }

        // ----------------------------------------------------------------
        // ユーティリティ - 静的メソッド
        // ----------------------------------------------------------------

        /// <summary>
        /// 文字列を有効なC#識別子に変換する。
        /// </summary>
        public static string SanitizeIdentifier(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "_";

            var sb = new StringBuilder(raw.Length + 4);

            if (!(char.IsLetter(raw[0]) || raw[0] == '_'))
                sb.Append('_');

            for (int i = 0; i < raw.Length; i++)
            {
                var c = raw[i];
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            }

            return sb.ToString();
        }

        /// <summary>
        /// 名前の重複を回避する。
        /// </summary>
        public static string MakeUnique(string baseName, HashSet<string> used)
        {
            if (used == null)
                return baseName;

            var name = baseName;
            int counter = 2;
            while (!used.Add(name))
            {
                name = $"{baseName}_{counter}";
                counter++;
            }
            return name;
        }

        /// <summary>
        /// クラス名用のユニーク化。親クラスと同じ名前を回避。
        /// </summary>
        public static string MakeUniqueClassName(string segment, string parentClassName, HashSet<string> usedInParent)
        {
            var baseName = SanitizeIdentifier(segment);
            if (string.IsNullOrEmpty(baseName))
                baseName = "_";

            int counter = 1;
            while (true)
            {
                var candidate = counter == 1 ? baseName : $"{baseName}_{counter}";

                if (string.Equals(candidate, parentClassName, StringComparison.Ordinal))
                {
                    counter++;
                    continue;
                }

                if (usedInParent == null || usedInParent.Add(candidate))
                    return candidate;

                counter++;
            }
        }

        /// <summary>
        /// 文字列をC#文字列リテラル用にエスケープする。
        /// </summary>
        public static string EscapeString(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>
        /// 文字列をXMLコメント用にエスケープする。
        /// </summary>
        public static string EscapeXml(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        // ----------------------------------------------------------------
        // 設定のFindOrCreate - 静的ヘルパー
        // ----------------------------------------------------------------

        /// <summary>
        /// 設定を検索するか、なければ作成する。
        /// </summary>
        /// <typeparam name="T">設定の型</typeparam>
        /// <param name="defaultAssetPath">デフォルトの保存先パス</param>
        /// <param name="logPrefix">ログ用プレフィックス</param>
        /// <returns>設定インスタンス</returns>
        public static T FindOrCreateSettings<T>(string defaultAssetPath, string logPrefix)
            where T : ScriptableObject
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<T>(path);
            }

            var settings = ScriptableObject.CreateInstance<T>();
            var dir = Path.GetDirectoryName(defaultAssetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            AssetDatabase.CreateAsset(settings, defaultAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[{logPrefix}] Created settings: {defaultAssetPath}");
            return settings;
        }
    }

    // ================================================================
    // StringKeyCodeGeneratorBase - string キー生成の特化基底クラス
    // ================================================================

    /// <summary>
    /// `public const string FieldName = "value";` 形式の
    /// コード生成に特化した基底クラス。
    /// </summary>
    public abstract class StringKeyCodeGeneratorBase<TSettings> : TreeCodeGeneratorBase<TSettings>
        where TSettings : ScriptableObject, ICodeGenSettings
    {
        /// <inheritdoc/>
        protected override void EmitFieldDeclaration(StringBuilder sb, int indent, string fieldName, ICodeGenNode node)
        {
            var tabs = new string(' ', indent);
            var value = EscapeString(node?.KeyValue ?? string.Empty);
            sb.AppendLine($"{tabs}public const string {fieldName} = \"{value}\";");
        }

        /// <inheritdoc/>
        protected override void EmitAllArrayElement(StringBuilder sb, int indent, string accessPath)
        {
            var tabs = new string(' ', indent);
            sb.AppendLine($"{tabs}{accessPath},");
        }

        /// <inheritdoc/>
        protected override string GetAllArrayType() => "string[]";
    }
}
#endif
