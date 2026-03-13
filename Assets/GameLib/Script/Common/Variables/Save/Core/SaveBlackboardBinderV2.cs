// Game.Save.SaveBlackboardBinderV2.cs
//
// v2.0 Binder: Blackboard 向け。SaveEntry集合から動作。
//
// v2.0 仕様:
// - BlackboardDefinitionSO に依存しない（旧 PreClear + Whitelist は成立しない）
// - SaveEntry 集合（ProfileRegistry 由来）を受け取り、
//   その VarId に該当する値のみを保存/復元対象にする
// - MissingPolicy に従って missing キーを処理

using System;
using System.Collections.Generic;
using Game.Common;
using Game.Profile;

namespace Game.Save
{
    /// <summary>
    /// SaveBlackboardBinderV2: ProfileRegistry の SaveEntry から動的に
    /// 保存対象を決定し、Blackboard値を Save/Load する Binder。
    /// 
    /// 仕様 v2.0 準拠:
    /// - PreClear + Whitelist(DefinitionSO) 方式は廃止
    /// - SaveEntry集合（ProfileRegistry が収集したもの）が保存対象を定義
    /// - MissingPolicy に基づいて missing キーの処理を決定
    /// </summary>
    public interface ISaveBlackboardBinderV2
    {
        /// <summary>
        /// SaveEntry 集合から、対象の Blackboard値を集める。
        /// 
        /// TODO v2.0 実装予定:
        /// - saveEntries を VarId でフィルタ
        /// - IBlackboardService から該当する値を取得
        /// - BlackboardSave 形式で返す
        /// </summary>
        // BlackboardSave CollectFromBlackboard(IBlackboardService bb, IEnumerable<ProfileSaveEntry> saveEntries, SaveLayer layer);

        /// <summary>
        /// SaveEntry 集合と MissingPolicy に基づいて、
        /// 保存データを Blackboard に適用（復元）する。
        /// 
        /// TODO v2.0 実装予定:
        /// - saveData の各レコードを VarId で Blackboard に書き込み
        /// - saveEntries にない VarId に対しては、MissingPolicy を参照
        ///   * KeepCurrent: 何もしない
        ///   * Clear: Unset でクリア
        /// </summary>
        // void ApplyToBlackboard(IBlackboardService bb, BlackboardSave saveData, IEnumerable<ProfileSaveEntry> saveEntries, MissingPolicy missingPolicy);
    }

    /// <summary>
    /// SaveBlackboardBinderV2 の実装スタブ。
    /// 
    /// TODO v2.0 実装:
    /// 1. CollectFromBlackboard: SaveEntry(VarId) リストから値を吸い上げ
    /// 2. ApplyToBlackboard: SaveEntry + MissingPolicy を考慮して復元
    /// 3. Binder v1（BlackboardDefinitionSO 依存）との併存期間を検討
    /// </summary>
    public class SaveBlackboardBinderV2Impl : ISaveBlackboardBinderV2
    {
        // TODO v2.0 実装予定（以下コメントのみ）

        // public BlackboardSave CollectFromBlackboard(IBlackboardService bb, IEnumerable<ProfileSaveEntry> saveEntries, SaveLayer layer)
        // {
        //     // SaveEntry.SaveLayer == layer && SaveEntry.Kind == Blackboard でフィルタ
        //     // 各 VarId に対応する値を bb.GetValue() で取得
        //     // BlackboardSave として返す
        //     throw new NotImplementedException("v2.0実装予定");
        // }

        // public void ApplyToBlackboard(IBlackboardService bb, BlackboardSave saveData, IEnumerable<ProfileSaveEntry> saveEntries, MissingPolicy missingPolicy)
        // {
        //     // saveData の各レコードを bb.SetValue() で書き込み
        //     // 
        //     // missing キーの処理:
        //     //   - saveEntries でカバーされている VarId のみが対象
        //     //   - saveData にはないが saveEntries にある VarId に対して:
        //     //     * missingPolicy == KeepCurrent: 何もしない
        //     //     * missingPolicy == Clear: bb.Unset() またはデフォルト値で初期化
        //     throw new NotImplementedException("v2.0実装予定");
        // }
    }
}
