#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Common
{
    /// <summary>
    /// VarStore の最小インターフェース。
    /// - 実行時は varId(int) でアクセスする（string key API を外に出さない）。
    /// - Pure(DynamicVariant) と RuntimeRef(object) を明確に分離する。
    /// - 例外を投げず、TrySet/TryUnset で拒否を表現する。
    /// </summary>
    public interface IVarStore
    {
        int GlobalVersion { get; }

        /// <summary>
        /// 変数単位の変更通知。
        /// - 成功した書き込み/Unset のみ発火する。
        /// - 同一値比較は行わない（成功した Set は毎回通知）。
        /// </summary>
        event Action<int /*varId*/>? OnVarChanged;

        /// <summary>
        /// Merge/Serialize/Debug 用の列挙。
        /// ホットパスでは使用しない（列挙順は未規定）。
        /// </summary>
        IEnumerable<int> EnumerateVarIds();

        bool Contains(int varId);
        int GetVarVersion(int varId);
        ValueKind GetVarKind(int varId);

        // ---- Pure (DynamicVariant が表現できる範囲) ----
        bool TrySetVariant(int varId, in DynamicVariant value);
        bool TryGetVariant(int varId, out DynamicVariant value);

        // ---- RuntimeRef (式対象外) ----
        bool TrySetManagedRef(int varId, object value);
        bool TryGetManagedRef(int varId, out object value);

        // ---- Table (2D / Cell VarStore) ----
        IEnumerable<int> EnumerateTableVarIds();
        bool ContainsTable(int tableVarId);
        int GetTableVersion(int tableVarId);
        bool TryGetTableRowCount(int tableVarId, out int rowCount);
        bool TryGetTableColumnCount(int tableVarId, int rowIndex, out int columnCount);
        bool TryHasTableCell(int tableVarId, int rowIndex, int columnIndex);
        bool TryEnsureTableRow(int tableVarId, int rowIndex);
        bool TryInsertTableRow(int tableVarId, int rowIndex);
        bool TryRemoveTableRow(int tableVarId, int rowIndex);
        bool TryAppendTableCell(int tableVarId, int rowIndex, out int columnIndex);
        bool TryInsertTableCell(int tableVarId, int rowIndex, int columnIndex);
        bool TryRemoveTableCell(int tableVarId, int rowIndex, int columnIndex);
        bool TryClearTable(int tableVarId);
        bool TryGetTableCellStore(int tableVarId, int rowIndex, int columnIndex, out IVarStore cellStore);
        bool TryGetOrEnsureTableCellStore(int tableVarId, int rowIndex, int columnIndex, bool autoCreateRow, out IVarStore cellStore);

        /// <summary>
        /// 値の未設定化。
        /// - 既に未設定なら false
        /// - 成功した場合は Version/OnVarChanged を更新する
        /// </summary>
        bool TryUnset(int varId);

        /// <summary>
        /// ストア全体を空にする。
        /// release/reset 時の一括破棄に使う。
        /// </summary>
        void Clear();
    }
}

