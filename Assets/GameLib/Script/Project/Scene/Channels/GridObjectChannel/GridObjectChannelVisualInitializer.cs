#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game.Common;
using Game.UI;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;

namespace Game.Channel
{
    internal sealed class GridObjectChannelVisualInitializer
    {
        readonly string _tag;
        readonly IScopeNode _owner;
        readonly GridObjectChannelPayloadBuilder _payloadBuilder;
        readonly GridObjectChannelVisualRelayoutService _relayoutService;
        readonly Func<int, GridObjectChoiceEntry?> _choiceEntryResolver;

        public GridObjectChannelVisualInitializer(
            string tag,
            IScopeNode owner,
            GridObjectChannelPayloadBuilder payloadBuilder,
            GridObjectChannelVisualRelayoutService relayoutService,
            Func<int, GridObjectChoiceEntry?> choiceEntryResolver)
        {
            _tag = tag;
            _owner = owner;
            _payloadBuilder = payloadBuilder;
            _relayoutService = relayoutService;
            _choiceEntryResolver = choiceEntryResolver;
        }

        public void ApplyPreviewSpawnPosition(
            GridObjectChannelRuntimeState state,
            GridObjectChannelVisualInstance instance,
            GridObjectChannelResolvedItem item)
        {
            TransformGridSharedUtility.RefreshLayoutAndBounds(instance.Resolver);
            var startAnchor = ResolveSpawnAnchorLocalPosition(state, item);
            var previewLocal = TransformGridSharedUtility.ResolvePlacementLocalPosition(
                instance.Resolver,
                instance.Root,
                instance.RootRect,
                startAnchor,
                (int)state.ResolvedLayoutPreset.ItemHorizontalAlignment,
                (int)state.ResolvedLayoutPreset.ItemVerticalAlignment);
            TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, previewLocal, state.EnvironmentKind);
        }

        public async UniTask InitializeSpawnedInstanceAsync(
            GridObjectChannelRuntimeState state,
            GridObjectChannelResolvedItem item,
            GridObjectChannelVisualInstance instance,
            CancellationToken ct)
        {
            instance.UpdateFromItem(item);
            var payload = _payloadBuilder.BuildPayload(item);
            var commandVars = _payloadBuilder.ApplyPayloadToBlackboard(instance, payload);

            await ExecuteSpawnCommandsAsync(state, item, instance, commandVars, ct);
            TransformGridSharedUtility.RefreshLayoutAndBounds(instance.Resolver);

            var startAnchor = ResolveSpawnAnchorLocalPosition(state, item);
            var startLocal = TransformGridSharedUtility.ResolvePlacementLocalPosition(
                instance.Resolver,
                instance.Root,
                instance.RootRect,
                startAnchor,
                (int)state.ResolvedLayoutPreset.ItemHorizontalAlignment,
                (int)state.ResolvedLayoutPreset.ItemVerticalAlignment);
            var targetLocal = TransformGridSharedUtility.ResolvePlacementLocalPosition(
                instance.Resolver,
                instance.Root,
                instance.RootRect,
                item.TargetLocalPosition,
                (int)state.ResolvedLayoutPreset.ItemHorizontalAlignment,
                (int)state.ResolvedLayoutPreset.ItemVerticalAlignment);

            TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, startLocal, state.EnvironmentKind);
            GridObjectChannelVisualSpawner.SetInstancePresentationVisible(instance, true);
            await _relayoutService.AnimateInstanceAsync(state, instance, targetLocal, state.ResolvedLayoutPreset.SpawnMotion, ct);
        }

        public async UniTask DelayBetweenNewSpawnsIfNeededAsync(
            GridObjectChannelRuntimeState state,
            int initializedCount,
            int totalSpawnCount,
            CancellationToken ct)
        {
            if (initializedCount >= totalSpawnCount || !state.ResolvedVisualizerPreset.DelayBetweenSpawns.HasSource || state.ActiveScope == null)
                return;

            var delay = state.ResolvedVisualizerPreset.DelayBetweenSpawns.GetOrDefault(
                new SimpleDynamicContext(GridObjectChannelRuntimeUtility.ResolveVars(state.ActiveScope), state.ActiveScope),
                0f);
            if (delay <= 0f)
                return;

            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
        }

        async UniTask ExecuteSpawnCommandsAsync(
            GridObjectChannelRuntimeState state,
            GridObjectChannelResolvedItem item,
            GridObjectChannelVisualInstance instance,
            IVarStore commandVars,
            CancellationToken ct)
        {
            if (!TryResolveCommandRunner(state, instance, out var runner) || runner == null)
                return;

            var counterVarId = GridObjectChannelRuntimeUtility.ResolveVarId(state.ResolvedVisualizerPreset.CounterVar, VarIds.GameLib.Base.CommandVar.i);
            if (counterVarId > 0)
                commandVars.TrySetVariant(counterVarId, DynamicVariant.FromInt(item.ListIndex));

            var ctx = new CommandContext(instance.Scope, commandVars, runner, instance.Scope, CommandRunOptions.Default);
            if (state.ResolvedVisualizerPreset.WriteSpawnerToContext)
            {
                var targetScope = state.ActiveScope ?? _owner;
                ctx.SetScope(GridObjectChannelRuntimeUtility.ResolveContextSlotOrDefault(state.ResolvedVisualizerPreset.SpawnerContextSlot), targetScope);
            }

            try
            {
                if (state.ResolvedVisualizerPreset.SpawnCommands != null && state.ResolvedVisualizerPreset.SpawnCommands.Count > 0)
                    await runner.ExecuteListAsync(state.ResolvedVisualizerPreset.SpawnCommands, ctx, ct, CommandRunOptions.Default);

                var choiceEntry = _choiceEntryResolver(item.ListIndex);
                if (choiceEntry != null &&
                    choiceEntry.SpawnCommands != null &&
                    choiceEntry.SpawnCommands.Count > 0)
                {
                    await runner.ExecuteListAsync(choiceEntry.SpawnCommands, ctx, ct, CommandRunOptions.Default);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GridObjectChannel] Spawn commands failed. Tag='{_tag}' Message={ex.Message}");
            }
        }

        Vector3 ResolveSpawnAnchorLocalPosition(GridObjectChannelRuntimeState state, GridObjectChannelResolvedItem item)
        {
            if (state.ResolvedLayoutPreset.SpawnAnchorMode == GridObjectChannelSpawnAnchorMode.LayoutTarget)
                return item.TargetLocalPosition + state.ResolvedLayoutPreset.SpawnOffset;

            var anchorLocal = Vector3.zero;
            if (state.ResolvedLayoutPreset.FixedAnchorTransform != null)
            {
                anchorLocal = TransformGridSharedUtility.ResolveLocalPointFromTransform(
                    state.ListRoot,
                    state.LayoutReferenceTransform,
                    state.LayoutRectTransform,
                    state.Canvas,
                    state.ResolvedLayoutPreset.FixedAnchorTransform,
                    state.EnvironmentKind);
            }
            else if (state.ResolvedLayoutPreset.UseFixedAnchorActorSource && state.ActiveScope != null)
            {
                var scope = ActorSourceFastResolver.ResolveCached(
                    state.ActiveScope,
                    state.ResolvedLayoutPreset.FixedAnchorActorSource,
                    ref state.FixedAnchorSourceCache,
                    state.ActiveScope);
                var transform = scope?.Identity?.SelfTransform;
                if (transform != null)
                {
                    anchorLocal = TransformGridSharedUtility.ResolveLocalPointFromTransform(
                        state.ListRoot,
                        state.LayoutReferenceTransform,
                        state.LayoutRectTransform,
                        state.Canvas,
                        transform,
                        state.EnvironmentKind);
                }
            }

            return anchorLocal + state.ResolvedLayoutPreset.SpawnOffset;
        }

        static bool TryResolveCommandRunner(
            GridObjectChannelRuntimeState state,
            GridObjectChannelVisualInstance instance,
            out ICommandRunner? runner)
        {
            runner = null;
            if (instance.Resolver != null &&
                instance.Resolver.TryResolve<ICommandRunner>(out var localRunner) &&
                localRunner != null)
            {
                runner = localRunner;
                return true;
            }

            return GridObjectChannelRuntimeUtility.TryResolveFromScopeOrAncestors(state.ActiveScope, out runner) && runner != null;
        }
    }
}
