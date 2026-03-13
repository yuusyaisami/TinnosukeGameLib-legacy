// Game.Profile.ProfileSaveEntry.cs
//
// Profile 主導の Save エントリ定義

using System;
using Game.Save;

namespace Game.Profile
{
    /// <summary>
    /// Save 対象の種類
    /// </summary>
    public enum SaveEntryKind
    {
        /// <summary>Scalar 値</summary>
        Scalar,

        /// <summary>Blackboard 値</summary>
        Blackboard
    }

    /// <summary>
    /// Profile から生成される Save エントリ。
    /// SaveManager に対してどの値を保存対象とするかを指定する。
    /// </summary>
    public readonly struct ProfileSaveEntry : IEquatable<ProfileSaveEntry>
    {
        /// <summary>Save 対象の種類（Scalar/Blackboard）</summary>
        public readonly SaveEntryKind Kind;

        /// <summary>Scalar の場合は ScalarKey 名（Name）、Blackboard の場合は null</summary>
        public readonly string ScalarKeyName;

        /// <summary>Blackboard の場合は varId、Scalar の場合は 0</summary>
        public readonly int BlackboardVarId;

        /// <summary>Save レイヤー</summary>
        public readonly SaveLayer SaveLayer;

        /// <summary>Scope の安定 ID（LTSIdentityMB.id）。空の場合は Save 対象外。</summary>
        public readonly string ScopeIdentity;

        /// <summary>元となった Profile の型名（デバッグ用）</summary>
        public readonly string ProfileTypeName;

        // ================================================================
        // Factory
        // ================================================================

        /// <summary>
        /// Scalar 用の SaveEntry を作成
        /// </summary>
        public static ProfileSaveEntry ForScalar(string scalarKeyName, SaveLayer layer, string scopeIdentity, string profileTypeName)
        {
            return new ProfileSaveEntry(SaveEntryKind.Scalar, scalarKeyName, 0, layer, scopeIdentity, profileTypeName);
        }

        /// <summary>
        /// Blackboard 用の SaveEntry を作成
        /// </summary>
        public static ProfileSaveEntry ForBlackboard(int bbVarId, SaveLayer layer, string scopeIdentity, string profileTypeName)
        {
            return new ProfileSaveEntry(SaveEntryKind.Blackboard, null, bbVarId, layer, scopeIdentity, profileTypeName);
        }

        ProfileSaveEntry(SaveEntryKind kind, string scalarKeyName, int bbVarId, SaveLayer layer, string scopeIdentity, string profileTypeName)
        {
            Kind = kind;
            ScalarKeyName = scalarKeyName;
            BlackboardVarId = bbVarId;
            SaveLayer = layer;
            ScopeIdentity = scopeIdentity;
            ProfileTypeName = profileTypeName;
        }

        // ================================================================
        // Validation
        // ================================================================

        /// <summary>
        /// この SaveEntry が有効かどうか（ScopeIdentity が設定されているか）
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(ScopeIdentity);

        /// <summary>
        /// キー部分の識別子を取得（Scalar: KeyName、Blackboard: キー名）
        /// </summary>
        public string KeyIdentifier => Kind == SaveEntryKind.Scalar
            ? ScalarKeyName ?? string.Empty
            : (BlackboardVarId != 0 ? BlackboardVarId.ToString() : string.Empty);

        // ================================================================
        // Equality
        // ================================================================

        public bool Equals(ProfileSaveEntry other)
        {
            return Kind == other.Kind
                && ScalarKeyName == other.ScalarKeyName
                && BlackboardVarId == other.BlackboardVarId
                && SaveLayer == other.SaveLayer
                && ScopeIdentity == other.ScopeIdentity;
        }

        public override bool Equals(object obj) => obj is ProfileSaveEntry other && Equals(other);

        public override int GetHashCode()
        {
            return HashCode.Combine(Kind, ScalarKeyName, BlackboardVarId, SaveLayer, ScopeIdentity);
        }

        public static bool operator ==(ProfileSaveEntry left, ProfileSaveEntry right) => left.Equals(right);
        public static bool operator !=(ProfileSaveEntry left, ProfileSaveEntry right) => !left.Equals(right);

        public override string ToString()
        {
            var key = Kind == SaveEntryKind.Scalar ? $"Scalar:{ScalarKeyName}" : $"BB:{BlackboardVarId}";
            return $"[{ProfileTypeName}] {key} @ {ScopeIdentity}/{SaveLayer}";
        }
    }
}
