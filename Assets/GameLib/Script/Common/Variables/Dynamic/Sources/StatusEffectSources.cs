#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Game.Commands.VNext;
using Game.StatusEffect;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Common
{
    public enum StatusEffectDescriptionSeparator
    {
        NewLine = 10,
        Space = 20,
        CommaSpace = 30,
    }

    static class StatusEffectSourceUtility
    {
        public static bool TryResolveStatusEffectService(
            IScopeNode origin,
            out IStatusEffectService? service)
        {
            service = null;

            var resolver = origin?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve<IStatusEffectService>(out service) && service != null;
        }
    }

    [Serializable]
    public sealed class ActiveStatusEffectDescriptionsSource : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource)")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        ActorSource actorSource;

        [LabelText("Separator")]
        [EnumToggleButtons]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        StatusEffectDescriptionSeparator separator = StatusEffectDescriptionSeparator.NewLine;

        [LabelText("Exclude Effect Ids")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = false, DefaultExpandedState = true)]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        List<string> excludedDefinitionIds = new();

        [NonSerialized]
        ActorSourceResolveCache _cache;

        static readonly List<EffectState> SharedStates = new();
        static readonly List<int> SharedIndices = new();
        static readonly EffectStateIndexComparer SharedIndexComparer = new();

        sealed class EffectStateIndexComparer : IComparer<int>
        {
            public int Compare(int left, int right)
            {
                var compare = SharedStates[left].SortOrder.CompareTo(SharedStates[right].SortOrder);
                if (compare != 0)
                    return compare;

                return left.CompareTo(right);
            }
        }

        public string SourceTypeName => "StatusEffectDescriptions";
        public string GetDebugData => $"{actorSource.Kind} exclude={excludedDefinitionIds?.Count ?? 0}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context == null)
                return DynamicVariant.FromString(string.Empty);

            var scope = ActorSourceFastResolver.ResolveCached(context, actorSource, ref _cache);
            if (scope == null)
                return DynamicVariant.FromString(string.Empty);

            if (!StatusEffectSourceUtility.TryResolveStatusEffectService(scope, out var service) || service == null)
                return DynamicVariant.FromString(string.Empty);

            SharedStates.Clear();
            service.GetActiveEffectStates(SharedStates);
            if (SharedStates.Count == 0)
                return DynamicVariant.FromString(string.Empty);

            SharedIndices.Clear();
            for (int i = 0; i < SharedStates.Count; i++)
                SharedIndices.Add(i);

            if (SharedIndices.Count > 1)
                SharedIndices.Sort(SharedIndexComparer);

            var builder = new StringBuilder();
            for (int i = 0; i < SharedIndices.Count; i++)
            {
                var state = SharedStates[SharedIndices[i]];
                if (ShouldExclude(state.EffectId))
                    continue;

                var text = ResolveDescription(state, context, scope);
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (builder.Length > 0)
                    builder.Append(GetSeparatorText());

                builder.Append(text);
            }

            return DynamicVariant.FromString(builder.ToString());
        }

        bool ShouldExclude(string effectId)
        {
            if (excludedDefinitionIds == null || excludedDefinitionIds.Count == 0 || string.IsNullOrEmpty(effectId))
                return false;

            for (int i = 0; i < excludedDefinitionIds.Count; i++)
            {
                if (string.Equals(effectId, excludedDefinitionIds[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        string ResolveDescription(EffectState state, IDynamicContext context, IScopeNode? originScope)
        {
            if (TryEvaluateDescriptionKey(state.DescriptionKey, context, originScope, out var text) &&
                !string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            return string.Empty;
        }

        string GetSeparatorText()
        {
            return separator switch
            {
                StatusEffectDescriptionSeparator.Space => " ",
                StatusEffectDescriptionSeparator.CommaSpace => ", ",
                _ => "\n",
            };
        }

        static bool TryEvaluateDescriptionKey(
            string key,
            IDynamicContext context,
            IScopeNode? origin,
            out string text)
        {
            text = string.Empty;
            if (string.IsNullOrWhiteSpace(key) || context == null)
                return false;

            var resolver = origin?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve<IRichTextRefService>(out var service) &&
                service != null &&
                service.TryEvaluate(key, context, out text);
        }

    }

    [Serializable]
    public sealed class StatusEffectOperationEnabledBoolSource : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActorSource)")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        ActorSource targetActorSource = new() { Kind = ActorSourceKind.Current };

        [LabelText("Definition")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        DynamicValue<BaseStatusEffectDefinitionData> definition;

        [LabelText("Operation Id")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        DynamicValue<string> operationId;

        [NonSerialized]
        ActorSourceResolveCache _targetActorCache;

        public string SourceTypeName => "StatusEffectOperationEnabled";
        public string GetDebugData => $"{targetActorSource.Kind} def={definition.SourceDebugData} op={operationId.SourceDebugData}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context == null)
                return DynamicVariant.FromBool(false);

            var resolvedOperationId = operationId.GetOrDefault(context, string.Empty);
            if (string.IsNullOrWhiteSpace(resolvedOperationId))
                return DynamicVariant.FromBool(false);

            var resolvedDefinition = definition.GetOrDefault(context, default!);
            if (resolvedDefinition == null || string.IsNullOrWhiteSpace(resolvedDefinition.DefinitionId))
                return DynamicVariant.FromBool(false);

            var scope = ActorSourceFastResolver.ResolveCached(context, targetActorSource, ref _targetActorCache);
            if (scope == null)
                return DynamicVariant.FromBool(false);

            if (!StatusEffectSourceUtility.TryResolveStatusEffectService(scope, out var service) || service == null)
                return DynamicVariant.FromBool(false);

            var filter = new StatusEffectRuntimeFilter(StatusEffectRuntimeFilterMode.DefinitionId, resolvedDefinition.DefinitionId);
            return DynamicVariant.FromBool(service.IsAnyOperationEnabled(filter, resolvedOperationId));
        }
    }

    [Serializable]
    public sealed class StatusEffectDefinitionExistsBoolSource : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActorSource)")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        ActorSource targetActorSource = new() { Kind = ActorSourceKind.Current };

        [LabelText("Definition")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        DynamicValue<BaseStatusEffectDefinitionData> definition;

        [NonSerialized]
        ActorSourceResolveCache _targetActorCache;

        public string SourceTypeName => "StatusEffectDefinitionExists";
        public string GetDebugData => $"{targetActorSource.Kind} def={definition.SourceDebugData}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context == null)
                return DynamicVariant.FromBool(false);

            var resolvedDefinition = definition.GetOrDefault(context, default!);
            if (resolvedDefinition == null || string.IsNullOrWhiteSpace(resolvedDefinition.DefinitionId))
                return DynamicVariant.FromBool(false);

            var scope = ActorSourceFastResolver.ResolveCached(context, targetActorSource, ref _targetActorCache);
            if (scope == null)
                return DynamicVariant.FromBool(false);

            if (!StatusEffectSourceUtility.TryResolveStatusEffectService(scope, out var service) || service == null)
                return DynamicVariant.FromBool(false);

            return DynamicVariant.FromBool(service.HasEffect(resolvedDefinition.DefinitionId));
        }
    }
}
