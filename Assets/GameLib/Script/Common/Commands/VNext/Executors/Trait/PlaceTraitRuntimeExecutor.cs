#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.SelectRuntime;
using Game.Trait;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class PlaceTraitRuntimeExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.PlaceTraitRuntime;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not PlaceTraitRuntimeCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "PlaceTraitRuntimeCommandData is required.");

            var (holderScope, holderError) = await ActorScopeResolver.ResolveAsync(typed.HolderActorSource, ctx, ct);
            if (holderScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, holderError ?? "Holder scope could not be resolved.");

            EnsureScopeBuiltIfNeeded(holderScope);
            if (holderScope.Resolver == null ||
                !holderScope.Resolver.TryResolve<ITraitPlacementService>(out var placementService) ||
                placementService == null)
            {
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TraitPlacementService was not found on holder scope.");
            }

            if (!holderScope.Resolver.TryResolve<ITraitHolderHubService>(out var holderHub) || holderHub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "TraitHolderHubService was not found on holder scope.");

            if (!holderHub.TryGetPlacementSettings(typed.HolderKey, out var placementSettings) || placementSettings == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Placement defaults were not found for holder '{typed.HolderKey}'.");

            var dynamicContext = new SimpleDynamicContext(ctx.Vars, ctx.Scope);
            if (!holderHub.TryGetHolder(typed.HolderKey, out var holder) || holder == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Trait holder '{typed.HolderKey}' was not found.");

            if (!typed.Selector.TryResolve(holder, dynamicContext, out var traitInstance, out var selectorError) || traitInstance == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, string.IsNullOrEmpty(selectorError) ? "Trait selector could not be resolved." : selectorError);

            if (!placementSettings.TryResolvePosition(dynamicContext, out var position))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Placement position could not be resolved for holder '{typed.HolderKey}'.");

            if (!placementSettings.TryResolveRotationEuler(dynamicContext, out var rotationEuler))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Placement rotation could not be resolved for holder '{typed.HolderKey}'.");

            if (!placementSettings.TryResolveScale(dynamicContext, out var scale))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Placement scale could not be resolved for holder '{typed.HolderKey}'.");

            if (typed.OverridePosition)
            {
                if (!typed.Position.TryGet(dynamicContext, out position))
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Override position could not be resolved.");
            }

            if (typed.OverrideRotation)
            {
                if (!typed.RotationEuler.TryGet(dynamicContext, out rotationEuler))
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Override rotation could not be resolved.");
            }

            if (typed.OverrideScale)
            {
                if (!typed.Scale.TryGet(dynamicContext, out scale))
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Override scale could not be resolved.");
            }

            var useParent = placementSettings.UseParent;
            var parentSource = placementSettings.ParentActorSource;
            if (typed.OverrideParent)
            {
                useParent = typed.UseParent;
                parentSource = typed.ParentActorSource;
            }

            Transform? transformParent = null;
            if (useParent)
            {
                transformParent = await ResolveTransformParentFromActorSourceAsync(parentSource, ctx, ct);
                if (transformParent == null)
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Placement parent could not be resolved.");
            }

            var runtime = await placementService.PlaceAsync(
                typed.HolderKey,
                typed.Selector,
                dynamicContext,
                position,
                Quaternion.Euler(rotationEuler),
                scale,
                transformParent,
                ct);
            if (runtime == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Trait runtime could not be placed.");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log(
            //    $"[PlaceTraitRuntime] Placed runtime. Holder='{typed.HolderKey}' TraitInstanceId='{traitInstance.InstanceId}' " +
            //    $"Runtime='{DescribeScope(runtime)}' SettingsRunOnPlaced={placementSettings.RunOnPlacedCommands} " +
            //    $"SettingsCount={(placementSettings.OnPlacedCommands != null ? placementSettings.OnPlacedCommands.Count : -1)} " +
            //    $"CommandRunOnPlaced={typed.RunOnPlacedCommands} " +
            //    $"CommandCount={(typed.OnPlacedCommands != null ? typed.OnPlacedCommands.Count : -1)}");
#endif

            if (placementSettings.RunOnPlacedCommands && placementSettings.OnPlacedCommands != null && placementSettings.OnPlacedCommands.Count > 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                //Debug.Log(
                //    $"[PlaceTraitRuntime] Running placement-settings OnPlaced commands. Holder='{typed.HolderKey}' " +
                //    $"TraitInstanceId='{traitInstance.InstanceId}' Count={placementSettings.OnPlacedCommands.Count} " +
                //    $"List={placementSettings.OnPlacedCommands.GetDebugLabel()}");
#endif
                await ExecuteOnPlacedCommandsAsync(runtime, traitInstance, ctx, placementSettings.OnPlacedCommands);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                //Debug.Log(
                //    $"[PlaceTraitRuntime] Skipped placement-settings OnPlaced commands. Holder='{typed.HolderKey}' " +
                //    $"RunFlag={placementSettings.RunOnPlacedCommands} " +
                //    $"Count={(placementSettings.OnPlacedCommands != null ? placementSettings.OnPlacedCommands.Count : -1)}");
            }
#endif

            if (typed.RunOnPlacedCommands && typed.OnPlacedCommands != null && typed.OnPlacedCommands.Count > 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                //Debug.Log(
                //    $"[PlaceTraitRuntime] Running command-local OnPlaced commands. Holder='{typed.HolderKey}' " +
                //    $"TraitInstanceId='{traitInstance.InstanceId}' Count={typed.OnPlacedCommands.Count} " +
                //    $"List={typed.OnPlacedCommands.GetDebugLabel()}");
#endif
                await ExecuteOnPlacedCommandsAsync(runtime, traitInstance, ctx, typed.OnPlacedCommands);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                //Debug.Log(
                //    $"[PlaceTraitRuntime] Skipped command-local OnPlaced commands. Holder='{typed.HolderKey}' " +
                //    $"RunFlag={typed.RunOnPlacedCommands} Count={(typed.OnPlacedCommands != null ? typed.OnPlacedCommands.Count : -1)}");
            }
#endif

            if (!TryApplyFinalPlacementCorrection(runtime))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Placed runtime pose could not be corrected to a valid state.");
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
                runtimeScope.EnsureScopeBuilt();
        }

        static async UniTask<Transform?> ResolveTransformParentFromActorSourceAsync(ActorSource source, CommandContext ctx, CancellationToken ct)
        {
            var (actorScope, error) = await ActorScopeResolver.ResolveAsync(source, ctx, ct);
            if (actorScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Placement parent actor resolve failed: {error}");

            var transform = actorScope.Identity?.SelfTransform;
            if (transform == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Resolved placement parent scope does not expose a Transform.");

            return transform;
        }

        static async UniTask ExecuteOnPlacedCommandsAsync(
            RuntimeLifetimeScope runtimeScope,
            ITraitInstance traitInstance,
            CommandContext sourceContext,
            CommandListData commands)
        {
            var runner = sourceContext.Runner;
            var resolver = runtimeScope.Resolver;
            if (resolver != null &&
                resolver.TryResolve<ICommandRunner>(out var resolvedRunner) &&
                resolvedRunner != null)
            {
                runner = resolvedRunner;
            }

            if (runner == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "Placed runtime scope has no ICommandRunner.");

            var vars = new VarStore();
            sourceContext.Vars?.MergeInto(vars, overwrite: true);
            traitInstance?.Context?.Vars?.MergeInto(vars, overwrite: true);
            if (resolver != null &&
                resolver.TryResolve<IBlackboardService>(out var blackboard) &&
                blackboard != null)
            {
                blackboard.MergeInto(vars, overwrite: true);
            }

            var detachedOptions = sourceContext.Options.WithSuppressCancelLog(true);
            var placedCtx = new CommandContext(
                runtimeScope,
                vars,
                runner,
                runtimeScope,
                detachedOptions,
                runtimeScope,
                runtimeScope,
                runtimeScope,
                sourceContext);
            var traitInstanceId = traitInstance?.InstanceId ?? string.Empty;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log(
            //    $"[PlaceTraitRuntime] ExecuteOnPlaced begin. TraitInstanceId='{traitInstanceId}' " +
            //    $"Scope='{DescribeScope(placedCtx.Scope)}' Actor='{DescribeScope(placedCtx.Actor)}' " +
            //    $"Caller='{DescribeScope(placedCtx.CallerActor)}' RootActor='{DescribeScope(placedCtx.RootActor)}' " +
            //    $"Count={commands.Count} List={commands.GetDebugLabel()} DetachedFromSourceCancellation=True");
#endif

            var result = await runner.ExecuteListAsync(commands, placedCtx, CancellationToken.None, detachedOptions);
            if (result.Status == CommandRunStatus.Canceled)
                throw new CommandExecutionException(CommandRunFailureKind.Canceled, "PlaceTraitRuntime OnPlaced command list was canceled on runtime scope.");

            if (result.Status == CommandRunStatus.Error || result.FailureCount > 0)
            {
                var label = commands.GetDebugLabel();
                if (string.IsNullOrEmpty(label))
                    label = "<inline>";

                throw new CommandExecutionException(
                    result.FailureKind,
                    $"PlaceTraitRuntime OnPlaced command list failed. List={label} FailureCount={result.FailureCount} ErrorIndex={result.ErrorIndex} Message={result.Message}");
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log(
            //    $"[PlaceTraitRuntime] ExecuteOnPlaced complete. TraitInstanceId='{traitInstanceId}' " +
            //    $"Status={result.Status} FailureCount={result.FailureCount} List={commands.GetDebugLabel()}");
#endif
        }

        static bool TryApplyFinalPlacementCorrection(RuntimeLifetimeScope runtimeScope)
        {
            if (runtimeScope == null)
                return true;

            var editor = runtimeScope.GetComponentInChildren<UserMoveRotateRuntimeMB>(true);
            if (editor == null)
                return true;

            var request = UserMoveRotateValidationRequest.Create(editor, runtimeScope);
            if (!request.IsValid)
                return true;

            ResolvePlacementTransforms(runtimeScope, editor, out var moveTransform, out var rotateTransform);
            var currentPosition = moveTransform.position;
            var currentRotation = rotateTransform.rotation;

            if (UserMoveRotateValidationUtility.IsValidPose(request, currentPosition, currentRotation))
                return true;

            if (!UserMoveRotateValidationUtility.TryFindNearestValidPose(
                    request,
                    currentPosition,
                    currentRotation,
                    out var correctedPosition,
                    out var correctedRotation))
            {
                return false;
            }

            ApplyPlacementPose(moveTransform, rotateTransform, correctedPosition, correctedRotation);
            return true;
        }

        static void ResolvePlacementTransforms(
            RuntimeLifetimeScope runtimeScope,
            UserMoveRotateRuntimeMB editor,
            out Transform moveTransform,
            out Transform rotateTransform)
        {
            var rootTransform = ResolveRootTransform(runtimeScope);
            moveTransform = editor.ApplyOverrideTargetTransform && editor.MoveTargetTransform != null
                ? editor.MoveTargetTransform
                : rootTransform;
            rotateTransform = editor.ApplyOverrideTargetTransform && editor.RotateTargetTransform != null
                ? editor.RotateTargetTransform
                : rootTransform;
        }

        static Transform ResolveRootTransform(RuntimeLifetimeScope runtimeScope)
        {
            return runtimeScope.Identity?.SelfTransform != null
                ? runtimeScope.Identity.SelfTransform
                : runtimeScope.transform;
        }

        static void ApplyPlacementPose(
            Transform moveTransform,
            Transform rotateTransform,
            Vector3 position,
            Quaternion rotation)
        {
            if (ReferenceEquals(moveTransform, rotateTransform))
            {
                moveTransform.SetPositionAndRotation(position, rotation);
                return;
            }

            moveTransform.position = position;
            rotateTransform.rotation = rotation;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "<null>";

            var name = scope.Identity?.SelfTransform != null
                ? scope.Identity.SelfTransform.name
                : scope.Identity?.Id;
            if (string.IsNullOrEmpty(name))
                name = scope.GetType().Name;

            return $"{name} ({scope.GetType().Name})";
        }
#endif
    }
}
