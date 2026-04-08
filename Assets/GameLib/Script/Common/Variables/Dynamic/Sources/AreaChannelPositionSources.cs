#nullable enable

using System;
using Game.Channel;
using Game.Commands.VNext;
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

    public enum AreaChannelResolveFailureBehavior
    {
        ReturnNull = 10,
        ReturnFallback = 20,
        Fail = 30,
    }

    [Serializable]
    public sealed class AreaChannelPosition3Source : IDynamicSource
    {
        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Area Actor\", areaActorSource)")]
        ActorSource areaActorSource = new() { Kind = ActorSourceKind.Current };

        [SerializeField, LabelText("Channel Tag")]
        string channelTag = "default";

        [SerializeField, LabelText("Sample Mode")]
        AreaSampleMode sampleMode = AreaSampleMode.InteriorRandom;

        [SerializeField, LabelText("Layer Key")]
        string layerKey = string.Empty;

        [SerializeField, LabelText("On Resolve Failed")]
        [EnumToggleButtons]
        [Tooltip("Area の actor / hub / player / sample 解決に失敗したときの挙動です。ReturnNull は null、ReturnFallback は Fallback 値、Fail はコマンド中なら ResolveFailed を投げ、それ以外ではエラーログを出して null を返します。")]
        AreaChannelResolveFailureBehavior failureBehavior = AreaChannelResolveFailureBehavior.ReturnFallback;

        [SerializeField, LabelText("Fallback")]
        [ShowIf(nameof(UseFallbackValue))]
        Vector3 fallback = Vector3.zero;

        [NonSerialized]
        ActorSourceResolveCache _areaActorSourceCache;

        public string SourceTypeName => "AreaChannelPos";
        public string GetDebugData => $"{channelTag}:{sampleMode}:{layerKey} (Vector3)";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (TrySample(context, out var sampled, out var error))
                return DynamicVariant.FromVector3(sampled);

            return failureBehavior switch
            {
                AreaChannelResolveFailureBehavior.ReturnNull => DynamicVariant.Null,
                AreaChannelResolveFailureBehavior.Fail => BlackboardSourceUtility.FailOrNull(context, BuildResolveFailureMessage(error)),
                _ => DynamicVariant.FromVector3(fallback),
            };
        }

        bool TrySample(IDynamicContext context, out Vector3 sampled, out string error)
        {
            sampled = default;
            error = string.Empty;

            var scope = ActorSourceFastResolver.ResolveCached(context, areaActorSource, ref _areaActorSourceCache);
            if (scope?.Resolver == null)
            {
                error = $"AreaChannelPosition3 failed to resolve area actor. ActorSource={areaActorSource.Kind} Tag='{NormalizeTag(channelTag)}'";
                return false;
            }

            if (!scope.Resolver.TryResolve<IAreaChannelHubService>(out var hub) || hub == null)
            {
                error = $"AreaChannelPosition3 could not resolve IAreaChannelHubService. Scope='{scope.Identity?.Id}' Tag='{NormalizeTag(channelTag)}'";
                return false;
            }

            var normalizedTag = NormalizeTag(channelTag);
            if (!hub.TryGetPlayer(normalizedTag, out var player) || player == null)
            {
                error = $"AreaChannelPosition3 area channel was not found. Tag='{normalizedTag}'";
                return false;
            }

            if (!TryResolveBasePosition(scope, player.Definition, out var basePosition))
            {
                error = $"AreaChannelPosition3 could not resolve base position. Tag='{normalizedTag}'";
                return false;
            }

            var dynamicContext = AreaChannelDynamicContextUtility.CreateContext(scope);
            var request = new AreaSampleRequest(sampleMode, layerKey ?? string.Empty);
            if (!player.TrySamplePosition(dynamicContext, basePosition, in request, out sampled))
            {
                error = $"AreaChannelPosition3 sampling returned no position. Tag='{normalizedTag}' SampleMode={sampleMode} Layer='{layerKey ?? string.Empty}'";
                return false;
            }

            return true;
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

        string BuildResolveFailureMessage(string error)
        {
            return !string.IsNullOrWhiteSpace(error)
                ? error
                : $"AreaChannelPosition3 failed to resolve. Tag='{NormalizeTag(channelTag)}' ActorSource={areaActorSource.Kind}";
        }

        bool UseFallbackValue() => failureBehavior == AreaChannelResolveFailureBehavior.ReturnFallback;

        static string NormalizeTag(string value) => string.IsNullOrWhiteSpace(value) ? "default" : value.Trim();
    }

    [Serializable]
    public sealed class AreaChannelPosition2Source : IDynamicSource
    {
        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Area Actor\", areaActorSource)")]
        ActorSource areaActorSource = new() { Kind = ActorSourceKind.Current };

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

        [NonSerialized]
        ActorSourceResolveCache _areaActorSourceCache;

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

            var scope = ActorSourceFastResolver.ResolveCached(context, areaActorSource, ref _areaActorSourceCache);
            if (scope?.Resolver == null)
                return false;

            if (!scope.Resolver.TryResolve<IAreaChannelHubService>(out var hub) || hub == null)
                return false;

            var normalizedTag = string.IsNullOrWhiteSpace(channelTag) ? "default" : channelTag.Trim();
            if (!hub.TryGetPlayer(normalizedTag, out var player) || player == null)
                return false;

            if (!TryResolveBasePosition(scope, player.Definition, out var basePosition))
                return false;

            var dynamicContext = AreaChannelDynamicContextUtility.CreateContext(scope);
            var request = new AreaSampleRequest(sampleMode, layerKey ?? string.Empty);
            return player.TrySamplePosition(dynamicContext, basePosition, in request, out sampled);
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
