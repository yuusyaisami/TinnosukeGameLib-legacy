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

    [Serializable]
    public sealed class ActiveStatusEffectDescriptionsSource : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource)")]
        [SerializeField]
        [Tooltip("説明文を取得する対象 Actor を指定します。")]
        ActorSource actorSource;

        [LabelText("Separator")]
        [EnumToggleButtons]
        [SerializeField]
        [Tooltip("複数の説明文を連結するときの区切り文字です。")]
        StatusEffectDescriptionSeparator separator = StatusEffectDescriptionSeparator.NewLine;

        [LabelText("Exclude Effect Ids")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = false, DefaultExpandedState = true)]
        [SerializeField]
        [Tooltip("ここに入れた definitionId は連結対象から除外します。空ならすべて対象です。")]
        List<string> excludedDefinitionIds = new();

        [NonSerialized]
        ActorSourceResolveCache _cache;

        static readonly List<EffectState> SharedStates = new();

        public string SourceTypeName => "StatusEffectDescriptions";
        public string GetDebugData => $"{actorSource.Kind} exclude={excludedDefinitionIds?.Count ?? 0}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context == null)
                return DynamicVariant.FromString(string.Empty);

            var scope = ActorSourceFastResolver.ResolveCached(context, actorSource, ref _cache);
            if (scope?.Resolver == null)
                return DynamicVariant.FromString(string.Empty);

            if (!scope.Resolver.TryResolve<IStatusEffectService>(out var service) || service == null)
                return DynamicVariant.FromString(string.Empty);

            IRichTextRefService? richTextRef = null;
            if (!scope.Resolver.TryResolve<IRichTextRefService>(out richTextRef) || richTextRef == null)
                richTextRef = ResolveRichTextRefServiceFromAncestors(scope);

            SharedStates.Clear();
            service.GetActiveEffectStates(SharedStates);
            if (SharedStates.Count == 0)
                return DynamicVariant.FromString(string.Empty);

            var builder = new StringBuilder();
            for (int i = 0; i < SharedStates.Count; i++)
            {
                var state = SharedStates[i];
                if (ShouldExclude(state.EffectId))
                    continue;

                var text = ResolveDescription(state, context, richTextRef);
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

        string ResolveDescription(EffectState state, IDynamicContext context, IRichTextRefService? richTextRef)
        {
            if (!string.IsNullOrEmpty(state.DescriptionKey) &&
                richTextRef != null &&
                richTextRef.TryEvaluate(state.DescriptionKey, context, out var text) &&
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

        static IRichTextRefService? ResolveRichTextRefServiceFromAncestors(IScopeNode? origin)
        {
            var current = origin?.Parent;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<IRichTextRefService>(out var service) && service != null)
                    return service;

                current = current.Parent;
            }

            return null;
        }
    }
}
