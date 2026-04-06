#nullable enable
using System;
using Game.Commands;
using Game.Common;
using Game.DI;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Trait
{
    public enum TraitElementSelectorKind
    {
        ByInstanceId = 10,
        ByDefinition = 20,
        ByDefinitionId = 30,
        ByIndex = 40,
        First = 50,
        Last = 60,
    }

    public enum TraitRuntimePlacementMode
    {
        Simple = 10,
    }

    public enum TraitRuntimePresentationState
    {
        None = 10,
        Visible = 20,
        Hidden = 30,
    }

    [Serializable]
    public sealed class PlaceableTraitSettings
    {
        [LabelText("Enabled")]
        [SerializeField]
        bool _enabled;

        [ShowIf(nameof(_enabled))]
        [LabelText("Runtime Template")]
        [SerializeField]
        DynamicValue<BaseRuntimeTemplatePreset> _runtimeTemplate = new();

        [ShowIf(nameof(_enabled))]
        [LabelText("Apply RuntimeTraitMB")]
        [SerializeField]
        bool _applyRuntimeTraitMb = true;

        [ShowIf(nameof(_enabled))]
        [LabelText("Default Placement Mode")]
        [EnumToggleButtons]
        [SerializeField]
        TraitRuntimePlacementMode _defaultPlacementMode = TraitRuntimePlacementMode.Simple;

        public bool Enabled => _enabled;
        public bool ApplyRuntimeTraitMB => _applyRuntimeTraitMb;
        public TraitRuntimePlacementMode DefaultPlacementMode => _defaultPlacementMode;

        public bool TryResolveRuntimeTemplate(IDynamicContext context, out BaseRuntimeTemplateSO? runtimeTemplate)
        {
            runtimeTemplate = null;
            if (!_enabled)
                return false;

            if (!_runtimeTemplate.TryGet(context, out var preset) || preset == null)
                return false;

            runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
            return runtimeTemplate != null;
        }
    }

    [Serializable]
    public struct TraitElementSelector
    {
        [EnumToggleButtons]
        public TraitElementSelectorKind Kind;

        [ShowIf("@Kind == Game.Trait.TraitElementSelectorKind.ByInstanceId")]
        [LabelText("Instance ID")]
        public DynamicValue<string> InstanceId;

        [ShowIf("@Kind == Game.Trait.TraitElementSelectorKind.ByDefinition")]
        [LabelText("Definition")]
        public DynamicValue<TraitDefinitionSO> Definition;

        [ShowIf("@Kind == Game.Trait.TraitElementSelectorKind.ByDefinitionId")]
        [LabelText("Definition ID")]
        public string DefinitionId;

        [ShowIf("@Kind == Game.Trait.TraitElementSelectorKind.ByIndex")]
        [LabelText("Index")]
        public DynamicValue<int> Index;

        public string DebugData
        {
            get
            {
                return Kind switch
                {
                    TraitElementSelectorKind.ByInstanceId => $"InstanceId={InstanceId.SourceDebugData}",
                    TraitElementSelectorKind.ByDefinition => $"Definition={Definition.SourceDebugData}",
                    TraitElementSelectorKind.ByDefinitionId => $"DefinitionId={DefinitionId}",
                    TraitElementSelectorKind.ByIndex => $"Index={Index.SourceDebugData}",
                    TraitElementSelectorKind.First => "First",
                    TraitElementSelectorKind.Last => "Last",
                    _ => Kind.ToString()
                };
            }
        }

        public bool TryResolve(
            ITraitHolderService? holder,
            IDynamicContext dynamicContext,
            out ITraitInstance? instance,
            out string error)
        {
            instance = null;
            error = string.Empty;

            if (holder == null)
            {
                error = "TraitHolderService is null.";
                return false;
            }

            var traits = holder.Traits;
            if (traits == null || traits.Count == 0)
            {
                error = "Trait holder is empty.";
                return false;
            }

            switch (Kind)
            {
                case TraitElementSelectorKind.ByInstanceId:
                    {
                        if (!InstanceId.TryGet(dynamicContext, out var instanceIdValue))
                        {
                            error = "InstanceId could not be resolved.";
                            return false;
                        }

                        var instanceId = string.IsNullOrWhiteSpace(instanceIdValue) ? string.Empty : instanceIdValue.Trim();
                        if (string.IsNullOrEmpty(instanceId))
                        {
                            error = "InstanceId is empty.";
                            return false;
                        }

                        for (int i = 0; i < traits.Count; i++)
                        {
                            var candidate = traits[i];
                            if (candidate == null)
                                continue;

                            if (!string.Equals(candidate.InstanceId, instanceId, StringComparison.Ordinal))
                                continue;

                            instance = candidate;
                            return true;
                        }

                        error = $"Trait instance '{instanceId}' was not found.";
                        return false;
                    }
                case TraitElementSelectorKind.ByDefinition:
                    {
                        if (!Definition.TryGet(dynamicContext, out var definition) || definition == null)
                        {
                            error = "Trait definition could not be resolved.";
                            return false;
                        }

                        for (int i = 0; i < traits.Count; i++)
                        {
                            var candidate = traits[i];
                            if (candidate == null)
                                continue;

                            if (!ReferenceEquals(candidate.Definition, definition))
                                continue;

                            instance = candidate;
                            return true;
                        }

                        error = $"Trait definition '{definition.name}' was not found.";
                        return false;
                    }
                case TraitElementSelectorKind.ByDefinitionId:
                    {
                        var definitionId = string.IsNullOrWhiteSpace(DefinitionId) ? string.Empty : DefinitionId.Trim();
                        if (string.IsNullOrEmpty(definitionId))
                        {
                            error = "DefinitionId is empty.";
                            return false;
                        }

                        for (int i = 0; i < traits.Count; i++)
                        {
                            var candidate = traits[i];
                            if (candidate == null)
                                continue;

                            if (!string.Equals(candidate.Definition.DefinitionId, definitionId, StringComparison.Ordinal))
                                continue;

                            instance = candidate;
                            return true;
                        }

                        error = $"Trait definition id '{definitionId}' was not found.";
                        return false;
                    }
                case TraitElementSelectorKind.ByIndex:
                    {
                        if (!Index.TryGet(dynamicContext, out var resolvedIndex))
                        {
                            error = "Trait index could not be resolved.";
                            return false;
                        }

                        if (resolvedIndex < 0 || resolvedIndex >= traits.Count)
                        {
                            error = $"Trait index is out of range. index={resolvedIndex} count={traits.Count}";
                            return false;
                        }

                        instance = traits[resolvedIndex];
                        if (instance == null)
                        {
                            error = $"Trait at index {resolvedIndex} is null.";
                            return false;
                        }

                        return true;
                    }
                case TraitElementSelectorKind.First:
                    for (int i = 0; i < traits.Count; i++)
                    {
                        if (traits[i] == null)
                            continue;

                        instance = traits[i];
                        return true;
                    }

                    error = "Trait holder is empty.";
                    return false;
                case TraitElementSelectorKind.Last:
                    for (int i = traits.Count - 1; i >= 0; i--)
                    {
                        if (traits[i] == null)
                            continue;

                        instance = traits[i];
                        return true;
                    }

                    error = "Trait holder is empty.";
                    return false;
                default:
                    error = $"Unsupported selector kind: {Kind}";
                    return false;
            }
        }
    }

    public readonly struct TraitRuntimeLinkKey : IEquatable<TraitRuntimeLinkKey>
    {
        public readonly LifetimeScopeKind SourceScopeKind;
        public readonly string SourceScopeId;
        public readonly string HolderKey;
        public readonly string TraitKey;

        public TraitRuntimeLinkKey(
            LifetimeScopeKind sourceScopeKind,
            string sourceScopeId,
            string holderKey,
            string traitKey)
        {
            SourceScopeKind = sourceScopeKind;
            SourceScopeId = sourceScopeId ?? string.Empty;
            HolderKey = holderKey ?? string.Empty;
            TraitKey = traitKey ?? string.Empty;
        }

        public bool Equals(TraitRuntimeLinkKey other)
        {
            return SourceScopeKind == other.SourceScopeKind
                && string.Equals(SourceScopeId, other.SourceScopeId, StringComparison.Ordinal)
                && string.Equals(HolderKey, other.HolderKey, StringComparison.Ordinal)
                && string.Equals(TraitKey, other.TraitKey, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is TraitRuntimeLinkKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)SourceScopeKind;
                hashCode = (hashCode * 397) ^ (SourceScopeId != null ? StringComparer.Ordinal.GetHashCode(SourceScopeId) : 0);
                hashCode = (hashCode * 397) ^ (HolderKey != null ? StringComparer.Ordinal.GetHashCode(HolderKey) : 0);
                hashCode = (hashCode * 397) ^ (TraitKey != null ? StringComparer.Ordinal.GetHashCode(TraitKey) : 0);
                return hashCode;
            }
        }
    }

    [Serializable]
    public sealed class TraitRuntimeLinkData
    {
        public LifetimeScopeKind SourceScopeKind;
        public string SourceScopeId = string.Empty;
        public string SourceScopeCategory = string.Empty;
        public string HolderKey = string.Empty;
        public string TraitKey = string.Empty;
        public string TraitDefinitionId = string.Empty;

        public TraitRuntimeLinkData Clone()
        {
            return new TraitRuntimeLinkData
            {
                SourceScopeKind = SourceScopeKind,
                SourceScopeId = SourceScopeId,
                SourceScopeCategory = SourceScopeCategory,
                HolderKey = HolderKey,
                TraitKey = TraitKey,
                TraitDefinitionId = TraitDefinitionId,
            };
        }

        public TraitRuntimeLinkKey ToLinkKey()
        {
            return new TraitRuntimeLinkKey(SourceScopeKind, SourceScopeId, HolderKey, TraitKey);
        }
    }

    public readonly struct TraitRuntimePresentationChange
    {
        public readonly string HolderKey;
        public readonly string TraitKey;
        public readonly TraitRuntimePresentationState PreviousState;
        public readonly TraitRuntimePresentationState CurrentState;

        public TraitRuntimePresentationChange(
            string holderKey,
            string traitKey,
            TraitRuntimePresentationState previousState,
            TraitRuntimePresentationState currentState)
        {
            HolderKey = holderKey ?? string.Empty;
            TraitKey = traitKey ?? string.Empty;
            PreviousState = previousState;
            CurrentState = currentState;
        }
    }

    static class TraitRuntimeLinkVarKeys
    {
        public const string SourceScopeKind = "traitRuntime.sourceScopeKind";
        public const string SourceScopeId = "traitRuntime.sourceScopeId";
        public const string SourceScopeCategory = "traitRuntime.sourceScopeCategory";
        public const string HolderKey = "traitRuntime.holderKey";
        public const string TraitKey = "traitRuntime.traitKey";
        public const string TraitDefinitionId = "traitRuntime.traitDefinitionId";
        public const string PresentationState = "traitRuntime.presentationState";

        public static void WriteLinkData(IVarStore vars, TraitRuntimeLinkData linkData)
        {
            if (vars == null || linkData == null)
                return;

            TrySetInt(vars, SourceScopeKind, (int)linkData.SourceScopeKind);
            TrySetString(vars, SourceScopeId, linkData.SourceScopeId);
            TrySetString(vars, SourceScopeCategory, linkData.SourceScopeCategory);
            TrySetString(vars, HolderKey, linkData.HolderKey);
            TrySetString(vars, TraitKey, linkData.TraitKey);
            TrySetString(vars, TraitDefinitionId, linkData.TraitDefinitionId);
        }

        // Hidden / Visible の現在状態は、RuntimeTraitMB で選んだキーに書く。
        // キー未設定時は legacy の stable key を使ってフォールバックする。
        public static void WritePresentationState(IVarStore vars, TraitRuntimePresentationState state, RuntimeTraitMB? runtimeBridge)
        {
            if (vars == null)
                return;

            if (runtimeBridge != null && runtimeBridge.TryResolvePresentationStateVarId(out var varId) && varId > 0)
            {
                vars.TrySetVariant(varId, DynamicVariant.FromInt((int)state));
                return;
            }

            if (VarIdResolver.TryResolve(PresentationState, out var fallbackVarId) && fallbackVarId > 0)
                vars.TrySetVariant(fallbackVarId, DynamicVariant.FromInt((int)state));
        }

        static void TrySetInt(IVarStore vars, string stableKey, int value)
        {
            if (!VarIdResolver.TryResolve(stableKey, out var varId) || varId == 0)
                return;

            vars.TrySetVariant(varId, DynamicVariant.FromInt(value));
        }

        static void TrySetString(IVarStore vars, string stableKey, string? value)
        {
            if (!VarIdResolver.TryResolve(stableKey, out var varId) || varId == 0)
                return;

            vars.TrySetVariant(varId, DynamicVariant.FromString(value ?? string.Empty));
        }
    }

    static class TraitPlacementScopeResolver
    {
        static IBaseLifetimeScopeRegistry? s_cachedRegistry;

        public static bool TryResolvePlacementService(
            TraitRuntimeLinkData? linkData,
            out ITraitPlacementService? placementService)
        {
            placementService = null;
            if (linkData == null)
                return false;

            if (!TryResolveSourceScope(linkData, out var scope) || scope?.Resolver == null)
                return false;

            if (scope.Resolver.TryResolve<ITraitPlacementService>(out var resolved) && resolved != null)
            {
                placementService = resolved;
                return true;
            }

            return false;
        }

        public static bool TryResolveSourceScope(TraitRuntimeLinkData? linkData, out IScopeNode? scope)
        {
            scope = null;
            if (linkData == null)
                return false;

            if (!TryResolveRegistry(out var registry) || registry == null)
                return false;

            var filter = new CommandTargetIdentityFilter
            {
                kind = linkData.SourceScopeKind,
                id = linkData.SourceScopeId,
                requireActive = false,
                searchScope = CommandTargetSearchScope.All,
            };

            scope = registry.Resolve(filter);
            return scope != null;
        }

        static bool TryResolveRegistry(out IBaseLifetimeScopeRegistry? registry)
        {
            if (s_cachedRegistry != null)
            {
                registry = s_cachedRegistry;
                return true;
            }

            var projects = UnityEngine.Object.FindObjectsByType<ProjectLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (projects != null)
            {
                for (int i = 0; i < projects.Length; i++)
                {
                    var project = projects[i];
                    if (project == null || project.Resolver == null)
                        continue;

                    if (!project.Resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var resolved) || resolved == null)
                        continue;

                    s_cachedRegistry = resolved;
                    registry = resolved;
                    return true;
                }
            }

            registry = null;
            return false;
        }
    }
}
