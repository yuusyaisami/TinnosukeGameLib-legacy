using System;
using System.Collections.Generic;

namespace Game.Input
{
    public interface IActionBlockService
    {
        /// <summary>
        /// Block the provided kinds. Returns a token that must be Dispose()'d to release the block.
        /// </summary>
        IDisposable Block(string kinds, string reason = null);

        /// <summary>
        /// BoolLayer に対する単純なフラグ設定。IDisposable を保持せずに Block 状態を投げる用途。
        /// blocked=true でセット、false で解除。
        /// </summary>
        void SetBlockFlag(string kinds, bool blocked, string reason = null);

        /// <summary>
        /// Whether a given kind is currently blocked.
        /// </summary>
        bool IsBlocked(string kind);

        /// <summary>
        /// Register a blockable service so tooling and introspection can list it.
        /// </summary>
        void RegisterBlockable(IActionBlockable blockable);

        /// <summary>
        /// Unregister previously registered blockable.
        /// </summary>
        void UnregisterBlockable(IActionBlockable blockable);

        /// <summary>
        /// Returns a read-only list of currently registered blockables.
        /// </summary>
        IReadOnlyList<IActionBlockable> RegisteredBlockables { get; }
    }
}
