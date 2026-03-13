#nullable enable

using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Flow
{
    /// <summary>
    /// コンパイル済み Flow プログラムを保持する ScriptableObject。
    /// <para>ビルド／エディタツールが生成し、実行時には読み取り専用で使用します。</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Flow/FlowProgramAssetSO")]
    public sealed class FlowProgramAssetSO : ScriptableObject, IFlowProgramData
    {
        [SerializeField, ReadOnly] int version = 1;
        [SerializeField, ReadOnly] FlowInstruction[] code = System.Array.Empty<FlowInstruction>();
        [SerializeField, ReadOnly] FlowArg[] args = System.Array.Empty<FlowArg>();
        [SerializeField, ReadOnly] string[] stringTable = System.Array.Empty<string>();
        [SerializeField, ReadOnly] FlowFunctionInfo[] functions = System.Array.Empty<FlowFunctionInfo>();

        // Optional: linkage/debug
        [SerializeField, ReadOnly] ScriptableObject? sourceDefinition;
        [SerializeField, ReadOnly] string sourceHash = string.Empty;
        [SerializeField, ReadOnly] string buildTimestamp = string.Empty;
        [SerializeField, ReadOnly] FlowCompileReport? report;

        /// <summary>アセットのバージョン（コンパイラのインクリメント用）</summary>
        public int Version => version;
        /// <summary>バイトコード配列</summary>
        public FlowInstruction[] Code => code;
        /// <summary>引数テーブル</summary>
        public FlowArg[] Args => args;
        /// <summary>文字列テーブル</summary>
        public string[] StringTable => stringTable;
        /// <summary>関数情報テーブル</summary>
        public FlowFunctionInfo[] Functions => functions;

        /// <summary>オリジナルの定義（ソースアセットへのリンク、任意）</summary>
        public ScriptableObject? SourceDefinition => sourceDefinition;
        /// <summary>ソースのハッシュ（任意）</summary>
        public string SourceHash => sourceHash;
        /// <summary>ビルドタイムスタンプ（任意）</summary>
        public string BuildTimestamp => buildTimestamp;
        /// <summary>コンパイルレポート（警告/エラー）</summary>
        public FlowCompileReport? Report => report;

        /// <summary>
        /// コンパイル結果をこのアセットに設定します（エディタツール用）。
        /// </summary>
        public void SetCompiledData(
            int newVersion,
            FlowInstruction[] newCode,
            FlowArg[] newArgs,
            string[] newStringTable,
            FlowFunctionInfo[] newFunctions,
            ScriptableObject? newSourceDefinition,
            string newSourceHash,
            string newBuildTimestamp,
            FlowCompileReport? newReport)
        {
            version = newVersion;
            code = newCode ?? System.Array.Empty<FlowInstruction>();
            args = newArgs ?? System.Array.Empty<FlowArg>();
            stringTable = newStringTable ?? System.Array.Empty<string>();
            functions = newFunctions ?? System.Array.Empty<FlowFunctionInfo>();
            sourceDefinition = newSourceDefinition;
            sourceHash = newSourceHash ?? string.Empty;
            buildTimestamp = newBuildTimestamp ?? string.Empty;
            report = newReport;
        }
    }
}
