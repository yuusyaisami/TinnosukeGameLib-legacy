#nullable enable
namespace Game.Common
{
    /// <summary>
    /// stableKey(string) ⇔ varId(int) の解決を提供する。
    /// - Runtime での解決は、Save/Debug/移行用途に限定する（資産化されたデータは varId を直接持つ）。
    /// </summary>
    public interface IVarKeyRegistry
    {
        bool TryResolve(string stableKeyOrAlias, out int varId);
        bool TryGetStableKey(int varId, out string stableKey);
    }
}

