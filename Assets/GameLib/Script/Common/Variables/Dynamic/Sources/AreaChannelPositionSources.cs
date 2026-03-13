#nullable enable

using System;
using Game.Channel;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Common
{
    public enum AreaChannelVector2OutputPlane
    {
        XY = 0,
        XZ = 1,
    }

    [Serializable]
    public sealed class AreaChannelPosition3Source : IDynamicSource
    {
        [SerializeField, LabelText("Channel Tag")]
        string channelTag = "default";

        [SerializeField, LabelText("Sample Mode")]
        AreaSampleMode sampleMode = AreaSampleMode.InteriorRandom;

        [SerializeField, LabelText("Layer Key")]
        string layerKey = string.Empty;

        [SerializeField, LabelText("Fallback")]
        Vector3 fallback = Vector3.zero;

        public string SourceTypeName => "AreaChannelPos";
        public string GetDebugData => $"{channelTag}:{sampleMode}:{layerKey} (Vector3)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!TrySample(context, out var sampled))
                sampled = fallback;

            return DynamicVariant.FromVector3(sampled);
        }

        bool TrySample(IDynamicContext context, out Vector3 sampled)
        {
            sampled = default;

            var scope = context?.Scope;
            if (scope?.Resolver == null)
                return false;

            if (!scope.Resolver.TryResolve<IAreaChannelHubService>(out var hub) || hub == null)
                return false;

            var normalizedTag = string.IsNullOrWhiteSpace(channelTag) ? "default" : channelTag.Trim();
            if (!hub.TryGetPlayer(normalizedTag, out var player) || player == null)
                return false;

            if (!TryResolveBasePosition(scope, player.Definition, out var basePosition))
                return false;

            var request = new AreaSampleRequest(sampleMode, layerKey ?? string.Empty);
            return player.TrySamplePosition(basePosition, in request, out sampled);
        }

        static bool TryResolveBasePosition(IScopeNode scope, AreaChannelDefinition definition, out Vector3 basePosition)
        {
            var anchor = definition.Anchor;
            if (anchor == null)
            {
                var owner = scope.Identity?.SelfTransform;
                if (owner == null)
                {
                    basePosition = default;
                    return false;
                }

                anchor = owner;
            }

            basePosition = anchor.position + definition.CenterOffset;
            return true;
        }
    }

    [Serializable]
    public sealed class AreaChannelPosition2Source : IDynamicSource
    {
        [SerializeField, LabelText("Channel Tag")]
        string channelTag = "default";

        [SerializeField, LabelText("Sample Mode")]
        AreaSampleMode sampleMode = AreaSampleMode.InteriorRandom;

        [SerializeField, LabelText("Layer Key")]
        string layerKey = string.Empty;

        [SerializeField, LabelText("Output Plane")]
        AreaChannelVector2OutputPlane outputPlane = AreaChannelVector2OutputPlane.XY;

        [SerializeField, LabelText("Fallback")]
        Vector2 fallback = Vector2.zero;

        public string SourceTypeName => "AreaChannelPos";
        public string GetDebugData => $"{channelTag}:{sampleMode}:{layerKey}:{outputPlane} (Vector2)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!TrySample(context, out var sampled3))
                return DynamicVariant.FromVector2(fallback);

            var sampled2 = outputPlane == AreaChannelVector2OutputPlane.XZ
                ? new Vector2(sampled3.x, sampled3.z)
                : new Vector2(sampled3.x, sampled3.y);

            return DynamicVariant.FromVector2(sampled2);
        }

        bool TrySample(IDynamicContext context, out Vector3 sampled)
        {
            sampled = default;

            var scope = context?.Scope;
            if (scope?.Resolver == null)
                return false;

            if (!scope.Resolver.TryResolve<IAreaChannelHubService>(out var hub) || hub == null)
                return false;

            var normalizedTag = string.IsNullOrWhiteSpace(channelTag) ? "default" : channelTag.Trim();
            if (!hub.TryGetPlayer(normalizedTag, out var player) || player == null)
                return false;

            if (!TryResolveBasePosition(scope, player.Definition, out var basePosition))
                return false;

            var request = new AreaSampleRequest(sampleMode, layerKey ?? string.Empty);
            return player.TrySamplePosition(basePosition, in request, out sampled);
        }

        static bool TryResolveBasePosition(IScopeNode scope, AreaChannelDefinition definition, out Vector3 basePosition)
        {
            var anchor = definition.Anchor;
            if (anchor == null)
            {
                var owner = scope.Identity?.SelfTransform;
                if (owner == null)
                {
                    basePosition = default;
                    return false;
                }

                anchor = owner;
            }

            basePosition = anchor.position + definition.CenterOffset;
            return true;
        }
    }
}
