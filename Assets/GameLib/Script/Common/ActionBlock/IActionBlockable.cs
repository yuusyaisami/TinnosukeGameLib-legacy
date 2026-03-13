using Game.Common;

namespace Game.Input
{
    /// <summary>
    /// Implement this interface on a service that can be action-blocked.
    /// On creation the service should register itself with the central ActionBlockService,
    /// and unregister on destruction.
    /// </summary>
    public interface IActionBlockable
    {
        /// <summary>
        /// Kinds of actions this blockable participates in.
        /// The block service uses this information to query or reflect which services are blockable.
        /// </summary>
        string ActionBlockKind { get; }

        /// <summary>
        /// Optional displayable identifier.
        /// </summary>
        string BlockableId { get; }

        /// <summary>
        /// Block 状態を合成保持する BoolLayer（ActionBlockService から更新される）。
        /// </summary>
        BoolLayer BlockLayer { get; }
    }
}
