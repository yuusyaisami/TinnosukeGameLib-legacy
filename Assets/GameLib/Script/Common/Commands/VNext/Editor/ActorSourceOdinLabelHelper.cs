#nullable enable

namespace Game.Commands.VNext.Editor
{
    /// <summary>
    /// Legacy editor-only shim.
    /// Prefer using <see cref="Game.Commands.VNext.ActorSourceOdinLabelHelper"/> from runtime.
    /// </summary>
    internal static class ActorSourceOdinLabelHelper
    {
        internal static string GetActorSourceLabel(ActorSource actorSource)
        {
            return Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource);
        }

        internal static string GetIdentityLabel(string baseLabel, Game.Commands.CommandTargetIdentityFilter filter)
        {
            return Game.Commands.VNext.ActorSourceOdinLabelHelper.GetIdentityLabel(baseLabel, filter);
        }
    }
}
