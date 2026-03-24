#nullable enable
using System;
using System.Collections.Generic;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.SelectRuntime
{
    public enum UserMoveSourceMode
    {
        PointerFollow = 10,
        InputMove = 20,
        Hybrid = 30,
    }

    public interface IWorldPointerRuntimeOptions
    {
        bool Enabled { get; }
        Camera? WorldCamera { get; }
        LayerMask HitMask { get; }
        QueryTriggerInteraction QueryTriggerInteraction { get; }
        float ShortPressSeconds { get; }
        float LongPressSeconds { get; }
    }

    public interface ISelectRuntimeManagerOptions
    {
        DynamicValue<bool> IsEnabled { get; }
    }

    public interface ISelectRuntimeManagerStateProvider
    {
        bool EvaluateIsEnabled();
    }

    public readonly struct WorldPointerEventData
    {
        public readonly WorldPointerTargetMB? Target;
        public readonly Vector2 ScreenPosition;
        public readonly Vector3 WorldPosition;
        public readonly Vector3 HitNormal;
        public readonly Collider2D? HitCollider;

        public WorldPointerEventData(
            WorldPointerTargetMB? target,
            Vector2 screenPosition,
            Vector3 worldPosition,
            Vector3 hitNormal,
            Collider2D? hitCollider)
        {
            Target = target;
            ScreenPosition = screenPosition;
            WorldPosition = worldPosition;
            HitNormal = hitNormal;
            HitCollider = hitCollider;
        }
    }

    public readonly struct WorldPointerHoverChangedEventData
    {
        public readonly WorldPointerTargetMB? PreviousTarget;
        public readonly WorldPointerTargetMB? CurrentTarget;
        public readonly WorldPointerEventData EventData;

        public WorldPointerHoverChangedEventData(
            WorldPointerTargetMB? previousTarget,
            WorldPointerTargetMB? currentTarget,
            WorldPointerEventData eventData)
        {
            PreviousTarget = previousTarget;
            CurrentTarget = currentTarget;
            EventData = eventData;
        }
    }

    public readonly struct SelectRuntimeSelectionChangedEvent
    {
        public readonly SelectableRuntimeMB? Previous;
        public readonly SelectableRuntimeMB? Current;

        public SelectRuntimeSelectionChangedEvent(SelectableRuntimeMB? previous, SelectableRuntimeMB? current)
        {
            Previous = previous;
            Current = current;
        }
    }

    public readonly struct SelectRuntimeHoveredChangedEvent
    {
        public readonly SelectableRuntimeMB? Previous;
        public readonly SelectableRuntimeMB? Current;

        public SelectRuntimeHoveredChangedEvent(SelectableRuntimeMB? previous, SelectableRuntimeMB? current)
        {
            Previous = previous;
            Current = current;
        }
    }

    public interface IWorldPointerRuntimeService
    {
        event Action<WorldPointerHoverChangedEventData>? OnHoveredChanged;
        event Action<WorldPointerEventData>? OnLeftClicked;
        event Action<WorldPointerEventData>? OnRightClicked;
        event Action<WorldPointerEventData>? OnLeftShortPressStarted;
        event Action<WorldPointerEventData>? OnLeftShortPressEnded;
        event Action<WorldPointerEventData>? OnRightShortPressStarted;
        event Action<WorldPointerEventData>? OnRightShortPressEnded;
        event Action<WorldPointerEventData>? OnLeftLongPressStarted;
        event Action<WorldPointerEventData>? OnLeftLongPressEnded;
        event Action<WorldPointerEventData>? OnRightLongPressStarted;
        event Action<WorldPointerEventData>? OnRightLongPressEnded;
        event Action<Input.InputFrame>? OnFrameUpdated;

        void RegisterTarget(WorldPointerTargetMB target);
        void UnregisterTarget(WorldPointerTargetMB target);
    }

    public interface ISelectRuntimeManagerService : ISelectRuntimeManagerStateProvider
    {
        event Action<SelectRuntimeSelectionChangedEvent>? OnSelectionChanged;
        event Action<SelectRuntimeHoveredChangedEvent>? OnHoveredChanged;
        event Action<SelectableRuntimeMB>? OnLeftClickSelectable;
        event Action<SelectableRuntimeMB>? OnRightClickSelectable;
        event Action<SelectableRuntimeMB>? OnLeftLongPressSelectable;
        event Action<bool>? OnEnabledChanged;

        SelectableRuntimeMB? Current { get; }
        SelectableRuntimeMB? Hovered { get; }
        bool IsEnabled { get; }

        void RegisterSelectable(SelectableRuntimeMB selectable);
        void UnregisterSelectable(SelectableRuntimeMB selectable);
        void GetRegisteredSelectables(List<SelectableRuntimeMB> results);
    }

    public interface IUserMoveRotateRuntimeService
    {
        bool IsEditing(UserMoveRotateRuntimeMB editor);
        void RegisterEditor(UserMoveRotateRuntimeMB editor);
        void UnregisterEditor(UserMoveRotateRuntimeMB editor);
    }

    static class SelectRuntimeVarKeys
    {
        public const string Selected = "selectRuntime.selected";
        public const string Hovered = "selectRuntime.hovered";
        public const string Editing = "selectRuntime.editing";

        public static void WriteSelectionState(IVarStore vars, bool selected, bool hovered, bool editing)
        {
            if (vars == null)
                return;

            TrySetBool(vars, Selected, selected);
            TrySetBool(vars, Hovered, hovered);
            TrySetBool(vars, Editing, editing);
        }

        static void TrySetBool(IVarStore vars, string stableKey, bool value)
        {
            if (!VarIdResolver.TryResolve(stableKey, out var varId) || varId == 0)
                return;

            vars.TrySetVariant(varId, DynamicVariant.FromBool(value));
        }
    }

    public readonly struct UserMoveRotateValidationRequest
    {
        public readonly UserMoveRotateRuntimeMB Editor;
        public readonly RuntimeLifetimeScope RuntimeScope;
        public readonly Transform RootTransform;
        public readonly SelectRuntimeManagerMB? ManagerBridge;
        public readonly IReadOnlyList<Collider> ValidationColliders;

        public bool IsValid => Editor != null && RuntimeScope != null && RootTransform != null;

        UserMoveRotateValidationRequest(
            UserMoveRotateRuntimeMB editor,
            RuntimeLifetimeScope runtimeScope,
            Transform rootTransform,
            SelectRuntimeManagerMB? managerBridge,
            IReadOnlyList<Collider> validationColliders)
        {
            Editor = editor;
            RuntimeScope = runtimeScope;
            RootTransform = rootTransform;
            ManagerBridge = managerBridge;
            ValidationColliders = validationColliders;
        }

        public static UserMoveRotateValidationRequest Create(UserMoveRotateRuntimeMB editor, RuntimeLifetimeScope runtimeScope)
        {
            var rootTransform = runtimeScope.Identity?.SelfTransform != null
                ? runtimeScope.Identity.SelfTransform
                : runtimeScope.transform;
            var managerBridge = SelectRuntimeBridgeResolver.FindNearestManager(editor.transform);
            var colliders = editor.ResolveValidationColliders();
            return new UserMoveRotateValidationRequest(editor, runtimeScope, rootTransform, managerBridge, colliders);
        }
    }

    static class SelectRuntimeCommandUtility
    {
        public static void Execute(
            SelectableRuntimeMB selectable,
            IScopeNode ownerScope,
            bool selected,
            bool hovered,
            bool editing,
            CommandListData? commands)
        {
            if (selectable == null || ownerScope == null || commands == null || commands.Count == 0)
                return;

            if (!selectable.TryResolveActorScope(out var actorScope) || actorScope == null || actorScope.Resolver == null)
                return;

            ICommandRunner? runner = null;
            if (!actorScope.Resolver.TryResolve<ICommandRunner>(out runner) || runner == null)
            {
                var ownerResolver = ownerScope.Resolver;
                if (ownerResolver == null || !ownerResolver.TryResolve<ICommandRunner>(out runner) || runner == null)
                    return;
            }

            var vars = new VarStore();
            if (actorScope.Resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard != null)
                blackboard.MergeInto(vars, overwrite: true);

            SelectRuntimeVarKeys.WriteSelectionState(vars, selected, hovered, editing);
            var context = new ICommandContextFactory(ownerScope, actorScope, vars, runner).Create();
            Cysharp.Threading.Tasks.UniTask.Void(async () =>
            {
                try
                {
                    await runner.ExecuteListAsync(commands, context, System.Threading.CancellationToken.None, context.Options);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SelectRuntime] Command execution failed: {ex.Message}");
                }
            });
        }

        sealed class ICommandContextFactory
        {
            readonly IScopeNode _ownerScope;
            readonly IScopeNode _actorScope;
            readonly IVarStore _vars;
            readonly ICommandRunner _runner;

            public ICommandContextFactory(IScopeNode ownerScope, IScopeNode actorScope, IVarStore vars, ICommandRunner runner)
            {
                _ownerScope = ownerScope;
                _actorScope = actorScope;
                _vars = vars;
                _runner = runner;
            }

            public CommandContext Create()
            {
                return new CommandContext(
                    _actorScope,
                    _vars,
                    _runner,
                    _actorScope,
                    CommandRunOptions.Default,
                    _ownerScope,
                    _actorScope,
                    _actorScope);
            }
        }
    }

}
