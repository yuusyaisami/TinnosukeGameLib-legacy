#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game.Common;
using Game.UI;
using Game.Vars.Generated;
using UnityEngine;
using UnityEngine.UI;
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

            if (state.EnableVerboseLayoutLog)
            {
                Debug.Log(
                    $"[GridObjectChannel] Preview spawn position. Tag='{_tag}' Channel={state.ChannelTag} Item={DescribeItem(item)} " +
                    $"Env={state.EnvironmentKind} ListRoot={DescribeTransform(state.ListRoot)} LayoutRef={DescribeTransform(state.LayoutReferenceTransform)} " +
                    $"LayoutRect={DescribeRectTransform(state.LayoutRectTransform)} StartAnchor={DescribeVector3(startAnchor)} PreviewLocal={DescribeVector3(previewLocal)} " +
                    $"RootBefore={DescribeLocalPosition(instance.Root, instance.RootRect)}",
                    state.ListRoot);
            }

            TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, previewLocal, state.EnvironmentKind);
        }

        public async UniTask InitializeSpawnedInstanceAsync(
            GridObjectChannelRuntimeState state,
            GridObjectChannelResolvedItem item,
            GridObjectChannelVisualInstance instance,
            CancellationToken ct)
        {
            instance.UpdateFromItem(item);
            var payload = _payloadBuilder.BuildPayload(state, item);
            var commandVars = _payloadBuilder.ApplyPayloadToBlackboard(instance, payload);

            if (state.EnableVerboseLayoutLog)
            {
                Debug.Log(
                    $"[GridObjectChannel] Spawn initialize begin. Tag='{_tag}' Channel={state.ChannelTag} Item={DescribeItem(item)} " +
                    $"Env={state.EnvironmentKind} Root={DescribeLocalPosition(instance.Root, instance.RootRect)} " +
                    $"LayoutRect={DescribeRectTransform(state.LayoutRectTransform)} RuntimeTemplate={state.ResolvedRuntimeTemplate?.name ?? "null"}",
                    state.ListRoot);
            }

            await ExecuteSpawnCommandsAsync(state, item, instance, commandVars, ct);
            TransformGridSharedUtility.RefreshLayoutAndBounds(instance.Resolver);

            if (state.EnableVerboseLayoutLog)
            {
                Debug.Log(
                    $"[GridObjectChannel] Spawn layout refreshed. Tag='{_tag}' Channel={state.ChannelTag} Item={DescribeItem(item)} " +
                    $"RootAfterRefresh={DescribeLocalPosition(instance.Root, instance.RootRect)} LayoutRect={DescribeRectTransform(state.LayoutRectTransform)}",
                    state.ListRoot);

                Debug.Log(
                    $"[GridObjectChannel] Spawn visual diagnostics. Tag='{_tag}' Channel={state.ChannelTag} Item={DescribeItem(item)} " +
                    $"Bounds={DescribeVisualBounds(instance)} LayoutComponents={DescribeLayoutComponents(instance)}",
                    instance.Root);
            }

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

            if (state.EnableVerboseLayoutLog)
            {
                Debug.Log(
                    $"[GridObjectChannel] Spawn placement resolved. Tag='{_tag}' Channel={state.ChannelTag} Item={DescribeItem(item)} " +
                    $"StartAnchor={DescribeVector3(startAnchor)} StartLocal={DescribeVector3(startLocal)} TargetLocal={DescribeVector3(targetLocal)} " +
                    $"RootBeforeMove={DescribeLocalPosition(instance.Root, instance.RootRect)}",
                    state.ListRoot);
            }

            TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, startLocal, state.EnvironmentKind);

            if (state.EnableVerboseLayoutLog)
            {
                Debug.Log(
                    $"[GridObjectChannel] Spawn start position applied. Tag='{_tag}' Channel={state.ChannelTag} Item={DescribeItem(item)} RootAfterStart={DescribeLocalPosition(instance.Root, instance.RootRect)}",
                    state.ListRoot);
            }

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
            var choiceEntry = _choiceEntryResolver(item.ListIndex);
            if (!TryResolveCommandRunner(state, instance, out var runner) || runner == null)
            {
                if ((state.ResolvedVisualizerPreset.SpawnCommands != null && state.ResolvedVisualizerPreset.SpawnCommands.Count > 0) ||
                    (choiceEntry?.SpawnCommands != null && choiceEntry.SpawnCommands.Count > 0))
                {
                    Debug.LogError($"[GridObjectChannel] Spawn commands were skipped because ICommandRunner could not be resolved. Tag='{_tag}'");
                }

                return;
            }

            var counterVarId = GridObjectChannelRuntimeUtility.ResolveVarId(state.ResolvedVisualizerPreset.CounterVar, VarIds.GameLib.Base.CommandVar.i);
            if (counterVarId > 0)
                commandVars.TrySetVariant(counterVarId, DynamicVariant.FromInt(item.ListIndex));

            if (state.EnableVerboseLayoutLog)
            {
                Debug.Log(
                    $"[GridObjectChannel] Spawn command runner resolved. Tag='{_tag}' Channel={state.ChannelTag} Item={DescribeItem(item)} " +
                    $"Runner={runner.GetType().Name} VisualizerCommands={state.ResolvedVisualizerPreset.SpawnCommands?.Count ?? 0} " +
                    $"ChoiceCommands={choiceEntry?.SpawnCommands?.Count ?? 0}",
                    instance.Root);
            }

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

        static string DescribeItem(GridObjectChannelResolvedItem item)
        {
            return $"Key={item.Key.Kind}:{item.Key.ValueA},{item.Key.ValueB} ListIndex={item.ListIndex} Row={item.Row} Column={item.Column} SourceRow={item.SourceRow} SourceColumn={item.SourceColumn}";
        }

        static string DescribeTransform(Transform? transform)
        {
            if (transform == null)
                return "null";

            return $"{transform.name} local={transform.localPosition} world={transform.position}";
        }

        static string DescribeRectTransform(RectTransform? rectTransform)
        {
            if (rectTransform == null)
                return "null";

            var rect = rectTransform.rect;
            return $"{rectTransform.name} rect={rect} anchored={rectTransform.anchoredPosition3D} anchorMin={rectTransform.anchorMin} anchorMax={rectTransform.anchorMax} pivot={rectTransform.pivot}";
        }

        static string DescribeVector3(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }

        static string DescribeLocalPosition(Transform root, RectTransform? rootRect)
        {
            return rootRect != null
                ? $"anchored={rootRect.anchoredPosition3D} local={root.localPosition}"
                : $"local={root.localPosition}";
        }

        static string DescribeVisualBounds(GridObjectChannelVisualInstance instance)
        {
            if (instance.Resolver == null ||
                !instance.Resolver.TryResolve<IVisualBoundsService>(out var boundsService) ||
                boundsService == null ||
                !boundsService.TryGetLastOutput(out var output) ||
                !output.HasBounds)
            {
                return "none";
            }

            return $"LocalRect={output.LocalRect} LocalCenter={output.LocalCenter} LocalSize={output.LocalSize} " +
                   $"WorldCenter={output.WorldCenter} WorldSize={output.WorldSize} ClampHasValue={output.LastClamp.HasValue} ClampMaxRate={output.LastClamp.MaxRate:0.###}";
        }

        static string DescribeLayoutComponents(GridObjectChannelVisualInstance instance)
        {
            var root = instance.Root;
            if (root == null)
                return "root=null";

            var layoutGroups = root.GetComponentsInChildren<LayoutGroup>(true);
            var contentSizeFitters = root.GetComponentsInChildren<ContentSizeFitter>(true);

            if (layoutGroups.Length == 0 && contentSizeFitters.Length == 0)
                return "none";

            var result = string.Empty;
            if (layoutGroups.Length > 0)
            {
                result += "LayoutGroups=[";
                for (var i = 0; i < layoutGroups.Length; i++)
                {
                    if (i > 0)
                        result += ", ";

                    var component = layoutGroups[i];
                    result += component == null ? "null" : $"{component.GetType().Name}:{component.name}";
                }

                result += "]";
            }

            if (contentSizeFitters.Length > 0)
            {
                if (result.Length > 0)
                    result += " ";

                result += "ContentSizeFitters=[";
                for (var i = 0; i < contentSizeFitters.Length; i++)
                {
                    if (i > 0)
                        result += ", ";

                    var component = contentSizeFitters[i];
                    result += component == null ? "null" : $"{component.GetType().Name}:{component.name}";
                }

                result += "]";
            }

            return result;
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
