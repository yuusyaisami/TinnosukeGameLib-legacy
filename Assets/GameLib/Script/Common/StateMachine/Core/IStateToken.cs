// Game.StateMachine.IStateToken.cs

using System;

namespace Game.StateMachine
{
    /// <summary>
    /// State の有効化トークン。
    /// <see cref="IDisposable"/> を実装し、Dispose で自動 Release される。
    /// </summary>
    /// <remarks>
    /// <para>推奨使用パターン:</para>
    /// <code>
    /// using var token = stateMachine.AcquireState("Movement.Walk", "PlayerController");
    /// // ... State を使用する処理 ...
    /// // using ブロック終了時に自動 Release
    /// </code>
    /// <para>手動 Release:</para>
    /// <code>
    /// var token = stateMachine.AcquireState("Combat.Attack", "Player");
    /// try {
    ///     // ...
    /// } finally {
    ///     token.Release();
    /// }
    /// </code>
    /// </remarks>
    public interface IStateToken : IDisposable
    {
        /// <summary>
        /// このトークンが管理する StateKey。
        /// </summary>
        string StateKey { get; }

        /// <summary>
        /// トークン所有者の識別子（デバッグ用）。
        /// </summary>
        string OwnerId { get; }

        /// <summary>
        /// Fire-and-forget 管理用の tag。通常の Acquire では空文字。
        /// </summary>
        string Tag { get; }

        /// <summary>
        /// トークンが有効かどうか。
        /// Release 後は false になる。
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// 明示的に State を Release する。
        /// 既に Release 済みの場合は何もしない。
        /// </summary>
        void Release();
    }
}
