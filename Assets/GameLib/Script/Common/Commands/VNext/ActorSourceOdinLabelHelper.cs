#nullable enable
using System.Collections.Generic;
using Game.Commands;
using UnityEngine;

namespace Game.Commands.VNext
{
    /// <summary>
    /// Odin inspector label helper for <see cref="ActorSource"/>.
    /// Kept in runtime assembly (not under an Editor folder) so CommandData can safely reference it.
    /// </summary>
    public static class ActorSourceOdinLabelHelper
    {
        public static string GetActorSourceLabel(ActorSource actorSource)
        {
            return GetLabel("Actor Source", actorSource);
        }

        public static string GetLabel(string baseLabel, ActorSource actorSource)
        {
            var summary = actorSource.Kind switch
            {
                ActorSourceKind.ByIdentity => FormatIdentitySummary(actorSource.Identity),
                ActorSourceKind.FromUnityObject => FormatUnityObjectSummary(actorSource.UnityObject),
                ActorSourceKind.Shared => FormatSharedSummary(actorSource.SharedTag),
                ActorSourceKind.ContextSlot => FormatContextSlotSummary(actorSource.ContextSlot),
                var kind => kind.ToString(),
            };

            return $"{baseLabel} ({summary})";
        }

        public static string GetIdentityLabel(string baseLabel, CommandTargetIdentityFilter filter)
        {
            var summary = FormatIdentitySummary(filter);
            return $"{baseLabel} ({summary})";
        }

        static string FormatIdentitySummary(CommandTargetIdentityFilter filter)
        {
            var segments = new List<string>();
            if (!string.IsNullOrWhiteSpace(filter.id))
            {
                segments.Add($"Id={filter.id}");
            }

            if (!string.IsNullOrWhiteSpace(filter.category))
            {
                segments.Add($"Catalog={filter.category}");
            }

            var details = segments.Count > 0 ? string.Join(", ", segments) : "None";
            return $"{filter.kind} ({details})";
        }

        static string FormatUnityObjectSummary(Object? unityObject)
        {
            if (unityObject == null)
            {
                return "UnityObject (None)";
            }

            var name = string.IsNullOrWhiteSpace(unityObject.name) ? "Unnamed" : unityObject.name;
            return $"{unityObject.GetType().Name}: {name}";
        }

        static string FormatSharedSummary(string? sharedTag)
        {
            return string.IsNullOrWhiteSpace(sharedTag)
                ? "Shared (None)"
                : $"Shared: {sharedTag}";
        }

        static string FormatContextSlotSummary(CommandLtsSlot slot)
        {
            return slot == CommandLtsSlot.None
                ? "ContextSlot (None)"
                : $"ContextSlot: {slot}";
        }
    }
}
