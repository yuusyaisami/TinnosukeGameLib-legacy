#nullable enable
using System;
using Game.Commands.VNext;
using Game.Trait;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Common
{
    /// <summary>
    /// Resolve a TraitDefinitionSO from a specific holder by using TraitElementSelector.
    /// Intended for DynamicValue&lt;TraitDefinitionSO&gt;.
    /// </summary>
    [Serializable]
    public sealed class HolderTraitDefinitionSource : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(hubActorSource)")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        ActorSource hubActorSource = new() { Kind = ActorSourceKind.Current };

        [LabelText("Holder Key")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string holderKey = string.Empty;

        [LabelText("Selector")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        TraitElementSelector selector;

        [NonSerialized]
        ActorSourceResolveCache _hubActorCache;

        public string SourceTypeName => "HolderTraitDefinition";
        public string GetDebugData => $"{holderKey} @ {hubActorSource.Kind}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context?.Scope == null)
                return DynamicVariant.Null;

            if (string.IsNullOrWhiteSpace(holderKey))
                return DynamicVariant.Null;

            var hubScope = ActorSourceFastResolver.ResolveCached(context, hubActorSource, ref _hubActorCache);
            EnsureScopeBuiltIfNeeded(hubScope);
            if (hubScope?.Resolver == null)
                return DynamicVariant.Null;

            if (!hubScope.Resolver.TryResolve<ITraitHolderHubService>(out var holderHub) || holderHub == null)
                return DynamicVariant.Null;

            if (!holderHub.TryGetHolder(holderKey, out var holder) || holder == null)
                return DynamicVariant.Null;

            if (!selector.TryResolve(holder, context, out var traitInstance, out _) || traitInstance == null)
                return DynamicVariant.Null;

            return traitInstance.Definition is TraitDefinitionSO definition
                ? DynamicVariant.FromUnityObject(definition)
                : DynamicVariant.Null;
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode? scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
                runtimeScope.EnsureScopeBuilt();
        }
    }
}
