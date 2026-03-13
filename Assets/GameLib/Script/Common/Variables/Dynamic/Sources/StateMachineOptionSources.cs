#nullable enable
using System;
using Game.Commands.VNext;
using Game.StateMachine;
using Game.StateMachine.Editor;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Common
{
    public enum StateMachineOptionLayerSourceMode
    {
        UseCurrentLayer = 0,
        UseSpecifiedLayer = 1,
    }

    /// <summary>
    /// StateMachine OptionKey が現在 set されているかを bool で返す。
    /// 解決順序: Local(layer) -> Global (fallback が有効な場合)。
    /// </summary>
    [Serializable]
    public sealed class StateMachineOptionIsSetBoolSource : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource)")]
        [SerializeField] ActorSource actorSource;

        [LabelText("Layer Source")]
        [EnumToggleButtons]
        [SerializeField] StateMachineOptionLayerSourceMode layerSourceMode = StateMachineOptionLayerSourceMode.UseCurrentLayer;

        [LabelText("Layer Key")]
        [ShowIf("@layerSourceMode == StateMachineOptionLayerSourceMode.UseSpecifiedLayer")]
        [SerializeField] string layerKey = string.Empty;

        [LabelText("Option Key")]
        [OptionKeyPicker]
        [SerializeField] string optionKey = string.Empty;

        [LabelText("Fallback To Global")]
        [SerializeField] bool fallbackToGlobal = true;

        public string SourceTypeName => "StateMachineOptionIsSet";
        public string GetDebugData
            => $"{actorSource.Kind}:{layerSourceMode}:{optionKey} fallbackGlobal={fallbackToGlobal}";

        [NonSerialized] ActorSourceResolveCache _cache;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (string.IsNullOrEmpty(optionKey))
                return DynamicVariant.FromBool(false);

            if (!StateMachineOptionSourceUtility.TryResolveStateMachine(context, actorSource, ref _cache, out var stateMachine) ||
                stateMachine == null)
                return DynamicVariant.FromBool(false);

            var resolvedLayer = ResolveLayerKey(stateMachine);
            if (!string.IsNullOrEmpty(resolvedLayer))
            {
                var localValue = stateMachine.GetLocalOption(resolvedLayer, optionKey);
                if (!string.IsNullOrEmpty(localValue))
                    return DynamicVariant.FromBool(true);
            }

            if (!fallbackToGlobal)
                return DynamicVariant.FromBool(false);

            var globalValue = stateMachine.GetGlobalOption(optionKey);
            return DynamicVariant.FromBool(!string.IsNullOrEmpty(globalValue));
        }

        string ResolveLayerKey(IStateMachineReadOnly stateMachine)
        {
            if (layerSourceMode == StateMachineOptionLayerSourceMode.UseSpecifiedLayer)
                return layerKey ?? string.Empty;

            return stateMachine.CurrentLayer ?? string.Empty;
        }
    }

    static class StateMachineOptionSourceUtility
    {
        public static bool TryResolveStateMachine(
            IDynamicContext? context,
            ActorSource actorSource,
            ref ActorSourceResolveCache cache,
            out IStateMachineReadOnly? stateMachine)
        {
            stateMachine = null;
            if (context == null)
                return false;

            var scope = ActorSourceFastResolver.ResolveCached(context.Scope, actorSource, ref cache, context.CommandRootScope);
            if (scope?.Resolver == null)
                return false;

            if (scope.Resolver.TryResolve<IStateMachineReadOnly>(out var readOnly) && readOnly != null)
            {
                stateMachine = readOnly;
                return true;
            }

            if (scope.Resolver.TryResolve<IStateMachine>(out var writable) && writable != null)
            {
                stateMachine = writable;
                return true;
            }

            return false;
        }
    }
}
