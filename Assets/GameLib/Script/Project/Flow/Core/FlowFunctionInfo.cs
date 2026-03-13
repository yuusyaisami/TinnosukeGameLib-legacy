#nullable enable

using System;

namespace Game.Flow
{
    /// <summary>
    /// 関数のメタ情報（文字列テーブル内の名前、エントリ位置、ローカル数など）
    /// </summary>
    [Serializable]
    public struct FlowFunctionInfo
    {
        /// <summary>関数名の stringTable インデックス</summary>
        public int NameStringId; // stringTable index
        /// <summary>関数のエントリ IP（コード内インデックス）</summary>
        public int EntryIp;      // code index
        /// <summary>この関数が持つローカル変数の数</summary>
        public int LocalCount;   // locals array size for this function
        /// <summary>スタック深度のヒント（オプション）</summary>
        public int MaxStackHint; // optional

        public FlowFunctionInfo(int nameStringId, int entryIp, int localCount, int maxStackHint = 0)
        {
            NameStringId = nameStringId;
            EntryIp = entryIp;
            LocalCount = localCount;
            MaxStackHint = maxStackHint;
        }
    }
}
