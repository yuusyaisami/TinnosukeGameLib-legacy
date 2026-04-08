#nullable enable
using System;
using Game.Commands.VNext;
using Game.Scalar;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace Game.Common
{
    public enum DataExistsStoreType
    {
        VarStore = 10,
        Blackboard = 20,
        Scalar = 30,
    }

    [Serializable]
    public sealed class DataExistsBoolSource : IDynamicSource
    {
        [SerializeField, LabelText("Store"), EnumToggleButtons]
        DataExistsStoreType storeType = DataExistsStoreType.VarStore;

        [SerializeField, ShowIf(nameof(UsesVarKey))]
        [LabelText("Var Key")]
        [InlineProperty]
        [HideLabel]
        VarKeyRef key;

        [SerializeField, ShowIf(nameof(UsesBlackboard))]
        [LabelText("Read Scope")]
        [EnumToggleButtons]
        BlackboardReadScope blackboardReadScope = BlackboardReadScope.Local;

        [SerializeField, ShowIf(nameof(UsesActorSource))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(targetActorSource)")]
        ActorSource targetActorSource = new() { Kind = ActorSourceKind.Current };

        [SerializeField, ShowIf(nameof(UsesScalar))]
        [LabelText("Scalar Key")]
        ScalarKey scalarKey;

        [SerializeField, ShowIf(nameof(UsesScalar))]
        [LabelText("Search Include Global")]
        bool searchIncludeGlobal;

        [SerializeField]
        [LabelText("Expected Kind")]
        [Tooltip("TraitDefinition の VarStore Kind と同じ基準です。Auto は値種別を問わず、何かしら値が存在すれば true です。")]
        VarStorePayload.EntryValueKind expectedKind = VarStorePayload.EntryValueKind.Auto;

        [NonSerialized]
        ActorSourceResolveCache _targetActorCache;

        public string SourceTypeName => "DataExists";

        public string GetDebugData
        {
            get
            {
                var keyLabel = storeType == DataExistsStoreType.Scalar
                    ? scalarKey.ToString()
                    : GetVarKeyDebugLabel();
                return $"Store={storeType} Key={keyLabel} Kind={expectedKind}";
            }
        }

        bool UsesVarKey => storeType == DataExistsStoreType.VarStore || storeType == DataExistsStoreType.Blackboard;
        bool UsesBlackboard => storeType == DataExistsStoreType.Blackboard;
        bool UsesActorSource => storeType == DataExistsStoreType.Blackboard || storeType == DataExistsStoreType.Scalar;
        bool UsesScalar => storeType == DataExistsStoreType.Scalar;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context == null)
                return DynamicVariant.FromBool(false);

            var exists = storeType switch
            {
                DataExistsStoreType.VarStore => EvaluateVarStore(context),
                DataExistsStoreType.Blackboard => EvaluateBlackboard(context),
                DataExistsStoreType.Scalar => EvaluateScalar(context),
                _ => false,
            };

            return DynamicVariant.FromBool(exists);
        }

        bool EvaluateVarStore(IDynamicContext context)
        {
            var vars = context.Vars;
            if (vars == null)
                return false;

            var varId = ResolveVarId();
            if (varId <= 0)
                return false;

            return TryMatchStore(vars, varId, expectedKind);
        }

        bool EvaluateBlackboard(IDynamicContext context)
        {
            var varId = ResolveVarId();
            if (varId <= 0)
                return false;

            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActorSource, ref _targetActorCache);
            if (targetScope == null)
                return false;

            if (blackboardReadScope == BlackboardReadScope.Global)
                return TryMatchBlackboardHierarchy(targetScope, varId, expectedKind);

            var resolver = targetScope.Resolver;
            if (resolver == null)
                return false;

            if (!resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                return false;

            return TryMatchStore(blackboard.LocalVars, varId, expectedKind);
        }

        bool EvaluateScalar(IDynamicContext context)
        {
            var targetScope = ActorSourceFastResolver.ResolveCached(context, targetActorSource, ref _targetActorCache);
            if (targetScope?.Resolver == null)
                return false;

            if (!targetScope.Resolver.TryResolve<IBaseScalarService>(out var scalarService) || scalarService == null)
                return false;

            var found = searchIncludeGlobal
                ? scalarService.GlobalTryGet(scalarKey, out var value)
                : scalarService.LocalTryGet(scalarKey, out value);

            if (!found)
                return false;

            return DoesVariantMatchExpectedKind(DynamicVariant.FromFloat(value), expectedKind);
        }

        static bool TryMatchBlackboardHierarchy(
            IScopeNode origin,
            int varId,
            VarStorePayload.EntryValueKind expectedKind)
        {
            for (IScopeNode? node = origin; node != null; node = node.Parent)
            {
                var resolver = node.Resolver;
                if (resolver == null)
                    continue;

                if (!resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                    continue;

                if (TryMatchStore(blackboard.LocalVars, varId, expectedKind))
                    return true;
            }

            return false;
        }

        static bool TryMatchStore(
            IVarStore store,
            int varId,
            VarStorePayload.EntryValueKind expectedKind)
        {
            if (!TryGetStoredVariant(store, varId, out var variant))
                return false;

            return DoesVariantMatchExpectedKind(variant, expectedKind);
        }

        static bool TryGetStoredVariant(IVarStore store, int varId, out DynamicVariant variant)
        {
            variant = DynamicVariant.Null;
            if (store == null || varId <= 0)
                return false;

            if (!store.Contains(varId))
                return false;

            var kind = store.GetVarKind(varId);
            if (kind == ValueKind.Null)
                return false;

            if (kind == ValueKind.ManagedRef)
            {
                if (!store.TryGetManagedRef(varId, out var managed) || managed == null)
                    return false;

                variant = DynamicVariant.FromManagedRef(managed);
                return variant.Kind != ValueKind.Null;
            }

            if (!store.TryGetVariant(varId, out variant))
                return false;

            return variant.Kind != ValueKind.Null;
        }

        static bool DoesVariantMatchExpectedKind(DynamicVariant variant, VarStorePayload.EntryValueKind expectedKind)
        {
            if (variant.Kind == ValueKind.Null)
                return false;

            switch (expectedKind)
            {
                case VarStorePayload.EntryValueKind.Auto:
                    return true;

                case VarStorePayload.EntryValueKind.Null:
                    return variant.Kind == ValueKind.Null;

                case VarStorePayload.EntryValueKind.Bool:
                    return variant.TryGet<bool>(out _);

                case VarStorePayload.EntryValueKind.Int:
                    return variant.TryGet<int>(out _);

                case VarStorePayload.EntryValueKind.Float:
                    return variant.TryGet<float>(out _);

                case VarStorePayload.EntryValueKind.String:
                    return variant.TryGet<string>(out _);

                case VarStorePayload.EntryValueKind.Vector2:
                    return variant.TryGet<Vector2>(out _);

                case VarStorePayload.EntryValueKind.Vector3:
                    return variant.TryGet<Vector3>(out _);

                case VarStorePayload.EntryValueKind.Vector4:
                    return variant.TryGet<Vector4>(out _);

                case VarStorePayload.EntryValueKind.Color:
                    return variant.TryGet<Color>(out _);

                case VarStorePayload.EntryValueKind.UnityObject:
                    return variant.TryGet<Object>(out _);

                case VarStorePayload.EntryValueKind.ManagedRef:
                    return variant.Kind == ValueKind.ManagedRef && variant.AsManagedRef != null;

                case VarStorePayload.EntryValueKind.CommandListData:
                    return variant.Kind == ValueKind.ManagedRef && variant.AsManagedRef is CommandListData;

                default:
                    return false;
            }
        }

        int ResolveVarId()
        {
            if (key.VarId > 0)
                return key.VarId;

            if (!string.IsNullOrWhiteSpace(key.StableKey) &&
                VarIdResolver.TryResolve(key.StableKey, out var resolved) &&
                resolved > 0)
            {
                return resolved;
            }

            return 0;
        }

        string GetVarKeyDebugLabel()
        {
            if (!string.IsNullOrWhiteSpace(key.StableKey))
                return $"{key.StableKey} (varId={key.VarId})";

            if (key.VarId > 0)
                return VarIdResolver.TryGetIdToStable(key.VarId) ?? $"varId={key.VarId}";

            return "(none)";
        }
    }
}