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
            if (scope == null)
                return DynamicVariant.FromString(string.Empty);

            if (!TryResolveStatusEffectService(scope, out var service) || service == null)
                return DynamicVariant.FromString(string.Empty);

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

        static bool TryResolveStatusEffectService(
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
}
