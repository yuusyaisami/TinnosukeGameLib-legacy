#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.Spawn;
using Game.Trait;
using Game.UI;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI.TraitList
{
    public interface IUITraitListVisualizerService
    {
        UniTask<UITraitListVisualInstance?> SpawnAsync(
            UITraitListSlot slot,
            UITraitListVisualizerProfileSO profile,
            Transform parent,
            IScopeNode scopeParent,
            ICommandRunner runner,
            CancellationToken ct);

        UniTask RelayoutAsync(
            UITraitListVisualInstance instance,
            UITraitListSlot slot,
            UITraitListLayoutProfileSO layoutProfile,
            CancellationToken ct);

        UniTask DespawnAsync(UITraitListVisualInstance instance, CancellationToken ct);
    }

    public sealed class UITraitListVisualizerService :
        IUITraitListVisualizerService,
        IUITransformListVisualizerService<UITraitListSlot, UITraitListVisualInstance, UITraitListLayoutProfileSO, UITraitListVisualizerProfileSO>,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly ISceneSpawnerRegistry _registry;
        readonly IUITraitListSystemOptions _options;

        public UITraitListVisualizerService(ISceneSpawnerRegistry registry, IUITraitListSystemOptions options)
        {
            _registry = registry;
            _options = options;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
        }

        public async UniTask<UITraitListVisualInstance?> SpawnAsync(
            UITraitListSlot slot,
            UITraitListVisualizerProfileSO profile,
            Transform parent,
            IScopeNode scopeParent,
            ICommandRunner runner,
            CancellationToken ct)
        {
            if (profile == null || parent == null || scopeParent == null)
                return null;

            var spawner = ResolveSpawner(profile.SpawnSource, _registry);
            if (spawner == null)
                return null;

            var spawnParent = profile.SpawnParentOverride != null ? profile.SpawnParentOverride : parent;
            var position = Vector3.zero;
            var rotation = Quaternion.identity;
            var scale = Vector3.one;
            var worldSpace = false;

            SpawnParams spawnParams;
            if (profile.SpawnSource == UITraitListSpawnSource.RuntimeTemplate)
            {
                var dynamicContext = new SimpleDynamicContext(NullVarStore.Instance, scopeParent);
                if (!profile.TryResolveRuntimeTemplate(dynamicContext, out var runtimeTemplate) || runtimeTemplate == null)
                {
                    Debug.LogError("[UITraitListVisualizer] RuntimeTemplate is null.");
                    return null;
                }

                spawnParams = SpawnParams.ForRuntime(
                    runtimeTemplate,
                    position,
                    rotation,
                    scale,
                    identity: null,
                    transformParent: spawnParent,
                    lifetimeScopeParent: scopeParent,
                    worldSpace: worldSpace,
                    allowPooling: profile.AllowPooling);
            }
            else
            {
                if (profile.Prefab == null)
                {
                    Debug.LogError("[UITraitListVisualizer] Prefab is null.");
                    return null;
                }

                spawnParams = SpawnParams.ForLTS(
                    profile.Prefab,
                    position,
                    rotation,
                    scale,
                    transformParent: spawnParent,
                    lifetimeScopeParent: scopeParent,
                    worldSpace: worldSpace,
                    allowPooling: profile.AllowPooling);
            }

            await UniTask.SwitchToMainThread();

            IObjectResolver? resolver = null;
            try
            {
                resolver = await spawner.SpawnAsync(spawnParams, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UITraitListVisualizer] Spawn failed: {ex.Message}");
                return null;
            }

            if (resolver == null)
                return null;

            ExtractSpawnedInfo(resolver, out var root, out var scope, out _, out _);
            if (root == null || scope == null)
            {
                Debug.LogError("[UITraitListVisualizer] Spawned instance missing root or scope.");
                return null;
            }

            var instance = new UITraitListVisualInstance(
                slot.Trait,
                slot.TraitIndex,
                slot.ListIndex,
                slot.Row,
                slot.Column,
                slot.AnchoredPosition,
                root,
                scope,
                resolver);

            var hub = await ResolveHolderHubAsync(scopeParent, resolver, runner, ct);

            SetPosition(instance, slot.AnchoredPosition);
            ApplySize(profile, instance);
            ApplyBlackboard(slot, instance, hub);

            try
            {
                slot.Trait?.OnLtsInstantiated(scope);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UITraitListVisualizer] OnLtsInstantiated failed: {ex.Message}");
            }

            if (TryResolveRunner(resolver, runner, out var runRunner))
                await ExecuteCommandsAsync(slot, profile, instance, runRunner, ct);

            return instance;
        }

        public async UniTask RelayoutAsync(
            UITraitListVisualInstance instance,
            UITraitListSlot slot,
            UITraitListLayoutProfileSO layoutProfile,
            CancellationToken ct)
        {
            if (layoutProfile == null)
                return;

            ct.ThrowIfCancellationRequested();
            await UniTask.SwitchToMainThread();

            ApplyBlackboard(slot, instance, hub: null);
            instance.UpdateSlot(slot);

            if (!layoutProfile.UseTransformAnimation || layoutProfile.MovePreset == null)
            {
                SetPosition(instance, slot.AnchoredPosition);
                return;
            }

            var rect = instance.RootRect;
            if (rect == null)
            {
                SetPosition(instance, slot.AnchoredPosition);
                return;
            }

            if (!instance.Resolver.TryResolve<ITransformAnimationHubService>(out var hub) ||
                hub == null)
            {
                SetPosition(instance, slot.AnchoredPosition);
                return;
            }

            if (!hub.TryGetPlayer(layoutProfile.ChannelTag, out var player) || player == null)
            {
                SetPosition(instance, slot.AnchoredPosition);
                return;
            }

            var step = FindAnchoredStep(layoutProfile.MovePreset);
            if (step == null)
            {
                SetPosition(instance, slot.AnchoredPosition);
                return;
            }

            var to = new Vector3(slot.AnchoredPosition.x, slot.AnchoredPosition.y, 0f);
            var task = player.PlayStepAsync(to, step);
            if (layoutProfile.WaitForCompletion)
                await task;
            else
                task.Forget();
        }

        public async UniTask DespawnAsync(UITraitListVisualInstance instance, CancellationToken ct)
        {
            await ReleaseSpawnedInstanceAsync(instance.Root, instance.Scope, instance.Resolver);
        }

        static void ApplyBlackboard(UITraitListSlot slot, UITraitListVisualInstance instance, ITraitHolderHubService? hub)
        {
            if (!instance.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                return;

            ApplyItemVars(blackboard.LocalVars, slot);
            ApplyRichTextKeys(blackboard.LocalVars, slot, hub);
        }

        static CommandContext CreateCommandContext(
            UITraitListSlot slot,
            UITraitListVisualInstance instance,
            ICommandRunner runner)
        {
            var vars = new VarStore(initialCapacity: 12);
            ApplyItemVars(vars, slot);
            return new CommandContext(instance.Scope, vars, runner, actor: instance.Scope, options: CommandRunOptions.Default);
        }

        static async UniTask ExecuteCommandsAsync(
            UITraitListSlot slot,
            UITraitListVisualizerProfileSO profile,
            UITraitListVisualInstance instance,
            ICommandRunner runner,
            CancellationToken ct)
        {
            if (runner == null || profile == null)
                return;

            var ctx = CreateCommandContext(slot, instance, runner);

            try
            {
                if (profile.SpawnCommands != null && profile.SpawnCommands.Count > 0)
                    await runner.ExecuteListAsync(profile.SpawnCommands, ctx, ct, CommandRunOptions.Default);

                var definition = slot.Trait?.Definition;
                if (definition != null &&
                    TryResolveByDefinition(profile.ByDefinition, definition, out var commands) &&
                    commands != null &&
                    commands.Count > 0)
                {
                    await runner.ExecuteListAsync(commands, ctx, ct, CommandRunOptions.Default);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UITraitListVisualizer] Command execution failed: {ex.Message}");
            }
        }

        static void ApplyItemVars(IVarStore vars, UITraitListSlot slot)
        {
            if (vars == null)
                return;

            TrySetVariant(vars, VarIds.GameLib.UI.TraitList.Item.listIndex, DynamicVariant.FromInt(slot.ListIndex));
            TrySetVariant(vars, VarIds.GameLib.UI.TraitList.Item.traitIndex, DynamicVariant.FromInt(slot.TraitIndex));
            TrySetVariant(vars, VarIds.GameLib.UI.TraitList.Item.row, DynamicVariant.FromInt(slot.Row));
            TrySetVariant(vars, VarIds.GameLib.UI.TraitList.Item.column, DynamicVariant.FromInt(slot.Column));
            TrySetVariant(vars, VarIds.GameLib.UI.TraitList.Item.holderKey, DynamicVariant.FromString(slot.HolderKey ?? string.Empty));
            TrySetVariant(vars, VarIds.GameLib.UI.TraitList.Item.rangeStart, DynamicVariant.FromInt(slot.RangeStart));
            TrySetVariant(vars, VarIds.GameLib.UI.TraitList.Item.rangeCount, DynamicVariant.FromInt(slot.RangeCount));

            var trait = slot.Trait;
            if (trait != null)
            {
                var definition = trait.Definition;
                if (definition != null)
                {
                    TrySetVariant(vars, VarIds.GameLib.UI.TraitList.Item.traitDefinitionId,
                        DynamicVariant.FromString(definition.DefinitionId ?? string.Empty));
                    if (VarIds.GameLib.UI.TraitList.Item.traitDefinitionRef != 0)
                        vars.TrySetManagedRef(VarIds.GameLib.UI.TraitList.Item.traitDefinitionRef, definition);

                    if (definition is TraitDefinitionSO traitDefinitionSO)
                    {
                        var visualSettings = traitDefinitionSO.VisualSettings;
                        if (visualSettings != null)
                        {
                            if (VarIds.GameLib.Base.VisualSetting.defaultAnim != 0)
                                vars.TrySetManagedRef(VarIds.GameLib.Base.VisualSetting.defaultAnim, visualSettings.DefaultAnim);
                            if (VarIds.GameLib.Base.VisualSetting.focusAnim != 0)
                                vars.TrySetManagedRef(VarIds.GameLib.Base.VisualSetting.focusAnim, visualSettings.FocusAnim);
                            if (VarIds.GameLib.Base.VisualSetting.InteractAnim != 0)
                                vars.TrySetManagedRef(VarIds.GameLib.Base.VisualSetting.InteractAnim, visualSettings.InteractAnim);
                            if (VarIds.GameLib.Base.VisualSetting.disableAnim != 0)
                                vars.TrySetManagedRef(VarIds.GameLib.Base.VisualSetting.disableAnim, visualSettings.DisableAnim);
                        }
                    }
                }

                TrySetVariant(vars, VarIds.GameLib.UI.TraitList.Item.traitInstanceId,
                    DynamicVariant.FromString(trait.InstanceId ?? string.Empty));
                if (VarIds.GameLib.UI.TraitList.Item.traitInstanceRef != 0)
                    vars.TrySetManagedRef(VarIds.GameLib.UI.TraitList.Item.traitInstanceRef, trait);
            }
        }

        static void TrySetVariant(IVarStore vars, int varId, DynamicVariant value)
        {
            if (vars == null || varId == 0)
                return;
            vars.TrySetVariant(varId, value);
        }

        static void ApplyRichTextKeys(IVarStore vars, UITraitListSlot slot, ITraitHolderHubService? hub)
        {
            if (vars == null)
                return;

            if (slot.Trait == null)
                return;

            if (hub == null)
                return;

            if (!hub.TryGetHolder(slot.HolderKey, out var holder) || holder == null)
            {
                Debug.LogWarning("[UITraitListVisualizer] Failed to get TraitHolderService from hub.");
                return;
            }

            if (holder is not TraitHolderService concreteHolder)
            {
                Debug.LogWarning("[UITraitListVisualizer] TraitHolderService is not concrete type.");
                return;
            }

            if (!concreteHolder.TryGetRichTextKeys(slot.Trait, out var descriptionKey, out var nameKey))
            {
                Debug.LogWarning("[UITraitListVisualizer] Failed to get rich text keys from TraitHolderService.");
                vars.TryUnset(VarIds.GameLib.Base.RichText.descriptionKey);
                vars.TryUnset(VarIds.GameLib.Base.RichText.nameKey);
                return;
            }

            if (!string.IsNullOrEmpty(descriptionKey))
                vars.TrySetVariant(VarIds.GameLib.Base.RichText.descriptionKey, DynamicVariant.FromString(descriptionKey));
            else
            {
                Debug.LogWarning("[UITraitListVisualizer] Description key is null or empty.");
                vars.TryUnset(VarIds.GameLib.Base.RichText.descriptionKey);
            }

            if (!string.IsNullOrEmpty(nameKey))
                vars.TrySetVariant(VarIds.GameLib.Base.RichText.nameKey, DynamicVariant.FromString(nameKey));
            else
            {
                Debug.LogWarning("[UITraitListVisualizer] Name key is null or empty.");
                vars.TryUnset(VarIds.GameLib.Base.RichText.nameKey);
            }
        }

        async UniTask<ITraitHolderHubService?> ResolveHolderHubAsync(
            IScopeNode scopeParent,
            IObjectResolver resolver,
            ICommandRunner fallback,
            CancellationToken ct)
        {
            if (_options == null)
                return null;

            if (!TryResolveRunner(resolver, fallback, out var runner) || runner == null)
                return null;

            var ctx = new CommandContext(scopeParent, new VarStore(), runner);
            var (hubScope, _) = await ActorScopeResolver.ResolveAsync(_options.HolderHubSource, ctx, ct);
            if (hubScope == null)
                return null;

            UITraitListCommandExecutorUtility.EnsureScopeBuiltIfNeeded(hubScope);

            if (hubScope.Resolver == null || !hubScope.Resolver.TryResolve<ITraitHolderHubService>(out var hub) || hub == null)
                return null;

            return hub;
        }

        static void SetPosition(UITraitListVisualInstance instance, Vector2 anchoredPosition)
        {
            if (instance.RootRect != null)
            {
                instance.RootRect.anchoredPosition = anchoredPosition;
                return;
            }

            if (instance.Root != null)
            {
                instance.Root.localPosition = new Vector3(anchoredPosition.x, anchoredPosition.y, 0f);
            }
        }

        static void ApplySize(UITraitListVisualizerProfileSO profile, UITraitListVisualInstance instance)
        {
            if (profile == null || instance.RootRect == null)
                return;

            if (!profile.OverrideSize)
                return;

            instance.RootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, profile.Width);
            instance.RootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, profile.Height);
        }

        static ITransformAnimationStep? FindAnchoredStep(TransformAnimationPreset? preset)
        {
            if (preset == null || preset.Steps == null)
                return null;

            var steps = preset.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step != null && step.operation == TransformAnimationOperation.AnchoredPosition)
                    return step;
            }

            return null;
        }

        static bool TryResolveRunner(IObjectResolver resolver, ICommandRunner fallback, out ICommandRunner runner)
        {
            runner = null!;
            if (resolver.TryResolve<ICommandRunner>(out var resolved) && resolved != null)
            {
                runner = resolved;
                return true;
            }

            if (fallback != null)
            {
                runner = fallback;
                return true;
            }

            return false;
        }

        static bool TryResolveByDefinition(
            List<UITraitDefinitionCommand>? list,
            ITraitDefinition definition,
            out CommandListData? commands)
        {
            commands = null;
            if (list == null || definition == null)
                return false;

            var defId = definition.DefinitionId;
            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry.Definition == null)
                    continue;

                if (ReferenceEquals(entry.Definition, definition) || entry.Definition.DefinitionId == defId)
                {
                    commands = entry.Commands;
                    return true;
                }
            }

            return false;
        }

        static IAsyncSpawnerService? ResolveSpawner(UITraitListSpawnSource source, ISceneSpawnerRegistry registry)
        {
            if (registry == null)
                return null;

            var kind = source == UITraitListSpawnSource.RuntimeTemplate
                ? SpawnerKind.RuntimeUIElement
                : SpawnerKind.UIElement;

            return registry.TryGet<IAsyncSpawnerService>(kind, "");
        }

        static async UniTask ReleaseSpawnedInstanceAsync(
            Transform? root,
            IScopeNode? scope,
            IObjectResolver? resolver)
        {
            if (resolver == null)
                return;

            await UniTask.SwitchToMainThread();

            try
            {
                if (resolver.TryResolve<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
                {
                    if (runtimeScope.Resolver != null &&
                        runtimeScope.Resolver.TryResolve<IRuntimeLifetimeScopePool>(out var pool) &&
                        pool != null)
                    {
                        pool.Release(runtimeScope);
                        return;
                    }

                    if (root != null)
                        UnityEngine.Object.Destroy(root.gameObject);
                    else
                        UnityEngine.Object.Destroy(runtimeScope.gameObject);
                    return;
                }

                if (scope is BaseLifetimeScope baseScope)
                {
                    await baseScope.DespawnAsync(CancellationToken.None);
                    return;
                }

                if (root != null)
                    UnityEngine.Object.Destroy(root.gameObject);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UITraitListVisualizer] Release failed: {ex.Message}");
            }
        }

        static void ExtractSpawnedInfo(
            IObjectResolver? resolver,
            out Transform? root,
            out IScopeNode? scopeNode,
            out RuntimeLifetimeScope? runtimeScope,
            out BaseLifetimeScope? baseScope)
        {
            root = null;
            scopeNode = null;
            runtimeScope = null;
            baseScope = null;

            if (resolver == null)
                return;

            resolver.TryResolve(out runtimeScope);

            if (runtimeScope != null)
                root = runtimeScope.transform;

            if (root == null)
            {
                if (resolver.TryResolve<Transform>(out var tr) && tr != null)
                    root = tr;
                else if (resolver.TryResolve<GameObject>(out var go) && go != null)
                    root = go.transform;
            }

            scopeNode = runtimeScope;
            if (scopeNode == null && resolver.TryResolve<IScopeNode>(out var resolved) && resolved != null)
                scopeNode = resolved;

            if (scopeNode == null && root != null)
            {
                var comps = root.GetComponents<Component>();
                for (int i = 0; i < comps.Length; i++)
                {
                    if (comps[i] is IScopeNode node)
                    {
                        scopeNode = node;
                        break;
                    }
                }
            }

            baseScope = scopeNode as BaseLifetimeScope;
        }
    }

}
