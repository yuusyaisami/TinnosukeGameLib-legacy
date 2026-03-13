// Game.Save.SaveScalarBinderV2.cs
//
// v2.0 Binder: Scalar 向け。SaveEntry集合から動作。
//
// v2.0 仕様:
// - ScalarBaselineRegistry に依存しない（旧 "missing=0にクリア" は成立しない）
// - SaveEntry 集合（ProfileRegistry 由来）を受け取り、
//   その ScalarKeyId に該当する値のみを保存/復元対象にする
// - MissingPolicy に従って missing キーを処理（ProfileRegistry初期値との衝突を回避）

using System;
using System.Collections.Generic;
using Game.Profile;

namespace Game.Save
{
    /// <summary>
    /// SaveScalarBinderV2: ProfileRegistry の SaveEntry から動的に
    /// 保存対象を決定し、Scalar値を Save/Load する Binder。
    /// 
    /// 仕様 v2.0 準拠:
    /// - ScalarBaselineRegistry 依存・"missing=0クリア" 方式は廃止
    /// - SaveEntry集合（ProfileRegistry が収集したもの）が保存対象を定義
    /// - MissingPolicy に基づいて missing キーの処理を決定
    ///   * デフォルト: KeepCurrent（ProfileRegistry初期値が残る）
    ///   * Session等: Clear（セッション終了時にリセット）
    /// </summary>
    public interface ISaveScalarBinderV2
    {
        /// <summary>
        /// SaveEntry 集合から、対象の Scalar値を集める。
        /// 
        /// TODO v2.0 実装予定:
        /// - saveEntries を ScalarKeyId でフィルタ
        /// - IBaseScalarService から該当する値を取得
        /// - ScalarSave 形式で返す
        /// </summary>
        // ScalarSave CollectFromScalar(IBaseScalarService scalar, IEnumerable<BindingSaveEntry> saveEntries, SaveLayer layer);

        /// <summary>
        /// SaveEntry 集合と MissingPolicy に基づいて、
        /// 保存データを Scalar に適用（復元）する。
        /// 
        /// TODO v2.0 実装予定:
        /// - saveData の各キーを Scalar に書き込み
        /// - saveEntries にない ScalarKeyId に対しては、MissingPolicy を参照
        ///   * KeepCurrent: 何もしない（ProfileRegistry初期値が残る）
        ///   * Clear: 0 またはデフォルト値でクリア
        /// </summary>
        // void ApplyToScalar(IBaseScalarService scalar, ScalarSave saveData, IEnumerable<BindingSaveEntry> saveEntries, MissingPolicy missingPolicy);
    }

    /// <summary>
    /// SaveScalarBinderV2 の実装スタブ。
    /// 
    /// TODO v2.0 実装:
    /// 1. CollectFromScalar: SaveEntry(ScalarKeyId) リストから値を吸い上げ
    /// 2. ApplyToScalar: SaveEntry + MissingPolicy を考慮して復元
    ///    - MissingPolicy.KeepCurrent が デフォルト推奨
    ///      （ProfileRegistry の初期値を破壊しない）
    ///    - MissingPolicy.Clear は Session/一時データ用途のみ
    /// 3. Binder v1 互換性は不要（ScalarBaselineRegistry は削除済み）
    /// </summary>
    public class SaveScalarBinderV2Impl : ISaveScalarBinderV2
    {
        // TODO v2.0 実装予定（以下コメントのみ）

        // public ScalarSave CollectFromScalar(IBaseScalarService scalar, IEnumerable<BindingSaveEntry> saveEntries, SaveLayer layer)
        // {
        //     // SaveEntry.SaveLayer == layer && SaveEntry.Kind == Scalar でフィルタ
        //     // 各 ScalarKeyId に対応する値を scalar.GetValue() で取得
        //     // ScalarSave として返す
        //     throw new NotImplementedException("v2.0実装予定");
        // }

        // public void ApplyToScalar(IBaseScalarService scalar, ScalarSave saveData, IEnumerable<BindingSaveEntry> saveEntries, MissingPolicy missingPolicy)
        // {
        //     // saveData の各キーを scalar.SetValue() で書き込み
        //     // 
        //     // missing キーの処理:
        //     //   - saveEntries でカバーされている ScalarKeyId のみが対象
        //     //   - saveData にはないが saveEntries にある ScalarKeyId に対して:
        //     //     * missingPolicy == KeepCurrent: 何もしない（ProfileRegistry初期値残存）
        //     //     * missingPolicy == Clear: scalar.SetValue() で 0/デフォルト値を書き込み
        //     //
        //     // 重要: ProfileRegistry初期値と衝突しないよう、
        //     // MissingPolicy.KeepCurrent がデフォルト推奨。
        //     throw new NotImplementedException("v2.0実装予定");
        // }
    }
}
