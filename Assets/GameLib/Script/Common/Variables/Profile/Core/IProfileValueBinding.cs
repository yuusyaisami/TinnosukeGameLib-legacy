// Game.Profile.IProfileValueBinding.cs
//
// ProfileValue の Blackboard/Scalar へのバインディングインターフェース

using System.Collections.Generic;
using Game.Common;
using Game.Save;
using Game.Scalar;

namespace Game.Profile
{
    /// <summary>
    /// Blackboard/Scalar へのバインディングを表現するインターフェース。
    /// BaseProfileSO のリフレクションベースフィールド列挙で使用。
    /// </summary>
    public interface IProfileValueBinding
    {
        /// <summary>
        /// Inspector の ListElementLabelName で使用する行ラベル
        /// </summary>
        string ProfileBindingListLabel { get; }

        /// <summary>
        /// Blackboard にバインドする VarId（0 の場合は Blackboard にバインドしない）
        /// </summary>
        int BlackboardKey { get; }

        /// <summary>
        /// Scalar にバインドするキー（default の場合は Scalar にバインドしない）
        /// </summary>
        ScalarKey ScalarKey { get; }

        /// <summary>
        /// Blackboard への書き込みポリシー
        /// </summary>
        BlackboardBindPolicy BlackboardPolicy { get; }

        /// <summary>
        /// Scalar への書き込みポリシー
        /// </summary>
        ScalarBindPolicy ScalarPolicy { get; }

        /// <summary>
        /// Blackboard に値を書き込む
        /// </summary>
        void WriteToBlackboard(IBlackboardService blackboard);

        /// <summary>
        /// Scalar に値を書き込む
        /// </summary>
        void WriteToScalar(IBaseScalarService scalar);

        /// <summary>
        /// バインディングが有効かどうか（少なくとも1つのキーが設定されている）
        /// </summary>
        bool HasAnyBinding { get; }

        // ================================================================
        // Save メタ情報
        // ================================================================

        /// <summary>
        /// Scalar の Save が有効か
        /// </summary>
        bool ScalarSaveEnabled { get; }

        /// <summary>
        /// Scalar の SaveLayer
        /// </summary>
        SaveLayer ScalarSaveLayer { get; }

        /// <summary>
        /// Blackboard の Save が有効か
        /// </summary>
        bool BlackboardSaveEnabled { get; }

        /// <summary>
        /// Blackboard の SaveLayer
        /// </summary>
        SaveLayer BlackboardSaveLayer { get; }

        /// <summary>
        /// この Binding から SaveEntry を収集する
        /// </summary>
        /// <param name="entries">出力先リスト</param>
        /// <param name="scopeIdentity">Scope の安定 ID</param>
        /// <param name="profileTypeName">Profile の型名（デバッグ用）</param>
        void CollectSaveEntries(List<BindingSaveEntry> entries, string scopeIdentity, string profileTypeName);
    }

    /// <summary>
    /// Blackboard への書き込みポリシー
    /// </summary>
    public enum BlackboardBindPolicy
    {
        /// <summary>常に上書き</summary>
        Overwrite,

        /// <summary>既存キーがあればスキップ</summary>
        SkipIfExists,

        /// <summary>既存キーの値を尊重（上書きしない）</summary>
        RespectExistingNoOverwrite
    }

    /// <summary>
    /// Scalar への書き込みポリシー
    /// </summary>
    public enum ScalarBindPolicy
    {
        /// <summary>Baseline を更新</summary>
        UpdateBaseline,

        /// <summary>RuntimeConfig ごと置き換え</summary>
        ReplaceRuntime,

        /// <summary>既に Runtime が存在すればスキップ</summary>
        SkipIfExists
    }
}
