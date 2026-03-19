#nullable enable
using System;
using Game.Actions;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Common
{
    public enum GameStateCompareOperator
    {
        Equal = 0,
        NotEqual = 1,
    }

    /// <summary>
    /// Compares the current GameState against a target state and returns bool.
    /// Intended for DynamicValue&lt;bool&gt;.
    /// </summary>
    [Serializable]
    public sealed class GameStateMachineCompareBoolSource : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource)")]
        [SerializeField] ActorSource actorSource;

        [LabelText("Operator")]
        [EnumToggleButtons]
        [SerializeField] GameStateCompareOperator op = GameStateCompareOperator.Equal;

        [LabelText("Target State")]
        [SerializeField] GameState targetState = GameState.Default;

        public string SourceTypeName => "GameStateCompare";
        public string GetDebugData => $"{actorSource.Kind}:{op} {targetState}";

        [NonSerialized] ActorSourceResolveCache _cache;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!GameStateMachineSourceUtility.TryResolveService(context, actorSource, ref _cache, out var svc) || svc == null)
                return DynamicVariant.FromBool(false);

            var current = svc.GetCurrentState();
            var result = op == GameStateCompareOperator.Equal
                ? current == targetState
                : current != targetState;
            return DynamicVariant.FromBool(result);
        }
    }

    /// <summary>
    /// Returns the current GameState enum value as int.
    /// Intended for DynamicValue&lt;int&gt;.
    /// </summary>
    [Serializable]
    public sealed class GameStateMachineStateIdIntSource : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource)")]
        [SerializeField] ActorSource actorSource;

        public string SourceTypeName => "GameStateId";
        public string GetDebugData => actorSource.Kind.ToString();

        [NonSerialized] ActorSourceResolveCache _cache;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!GameStateMachineSourceUtility.TryResolveService(context, actorSource, ref _cache, out var svc) || svc == null)
                return DynamicVariant.Null;

            var current = svc.GetCurrentState();
            return DynamicVariant.FromInt((int)current);
        }
    }

    static class GameStateMachineSourceUtility
    {
        public static bool TryResolveService(
            IDynamicContext? context,
            ActorSource actorSource,
            ref ActorSourceResolveCache cache,
            out IGameStateMachineService? service)
        {
            service = null;
            if (context == null)
                return false;

            var scope = ActorSourceFastResolver.ResolveCached(context, actorSource, ref cache);
            if (scope?.Resolver == null)
                return false;

            return scope.Resolver.TryResolve<IGameStateMachineService>(out service) && service != null;
        }

    }
}
