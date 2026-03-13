#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Background;
using Game.Common;
using Game.MaterialFx;
using Game.Visual;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class BackgroundLayerExecutor : ICommandExecutor
    {
        static readonly IReadOnlyList<MaterialFxPresetEntry> EmptyEntries = Array.Empty<MaterialFxPresetEntry>();

        public int CommandId => CommandIds.BackgroundLayer;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not BackgroundLayerCommandData cmd)
                return;

            if (!ctx.Resolver.TryResolve<IBackgroundSystem>(out var bg) || bg == null)
                return;

            if (cmd.Operation == BackgroundLayerOperation.MarkDirty)
            {
                bg.MarkDirty();
                return;
            }

            // ─── AllLayers の場合はすべてのレイヤーに適用 ──────────────
            if (cmd.AllLayers)
            {
                for (int i = 0; i < bg.LayerCount; i++)
                {
                    await ExecuteOperationAsync(cmd, bg, i, ctx, ct);
                }
                return;
            }

            // ─── 単一レイヤー解決 ─────────────────────────────────────
            int layerIndex;
            if (cmd.UseLayerName)
            {
                var name = cmd.LayerName.GetOrDefault(ctx, string.Empty);
                if (string.IsNullOrEmpty(name))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("[BackgroundLayerCmd] LayerName is empty.");
#endif
                    return;
                }
                if (!bg.TryGetLayerIndexByName(name, out layerIndex))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[BackgroundLayerCmd] Layer '{name}' not found.");
#endif
                    return;
                }
            }
            else
            {
                layerIndex = cmd.LayerIndex.GetOrDefault(ctx, 0);
            }

            await ExecuteOperationAsync(cmd, bg, layerIndex, ctx, ct);
        }

        async UniTask ExecuteOperationAsync(
            BackgroundLayerCommandData cmd,
            IBackgroundSystem bg,
            int layerIndex,
            CommandContext ctx,
            CancellationToken ct)
        {
            switch (cmd.Operation)
            {
                case BackgroundLayerOperation.SetScrollSpeed:
                    {
                        var speed = cmd.ScrollSpeed.GetOrDefault(ctx, Vector2.zero);
                        bg.SetLayerScrollSpeed(layerIndex, speed);
                        break;
                    }

                case BackgroundLayerOperation.SetOffset:
                    {
                        var offset = cmd.Offset.GetOrDefault(ctx, Vector2.zero);
                        bg.SetLayerOffset(layerIndex, offset);
                        break;
                    }

                case BackgroundLayerOperation.AddOffset:
                    {
                        var delta = cmd.Offset.GetOrDefault(ctx, Vector2.zero);
                        bg.AddLayerOffset(layerIndex, delta);
                        break;
                    }

                case BackgroundLayerOperation.SetPaused:
                    {
                        var paused = cmd.Paused.GetOrDefault(ctx, false);
                        bg.SetLayerPaused(layerIndex, paused);
                        break;
                    }

                case BackgroundLayerOperation.WriteSpritePreset:
                    {
                        if (!cmd.SpritePresetSource.TryGet(ctx, out var preset) || preset == null)
                            break;

                        var varId = cmd.SpritePresetVarId.GetOrDefault(ctx, 0);
                        if (varId <= 0)
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogWarning("[BackgroundLayerCmd] SpritePresetVarId is invalid.");
#endif
                            break;
                        }

                        // Blackboard の VarStore に AnimationSpritePreset を書き込む
                        ctx.Vars.TrySetVariant(varId, DynamicVariant.FromObject(preset));
                        break;
                    }

                case BackgroundLayerOperation.SetMaterialFx:
                    {
                        IReadOnlyList<MaterialFxPresetEntry> entries = EmptyEntries;

                        if (cmd.MaterialFxSource.TryGet(ctx, out var payload) && payload != null)
                            entries = payload.Entries ?? EmptyEntries;

                        var selector = cmd.VisualSelector.ToSelector();
                        bg.SetLayerMaterialFx(layerIndex, selector, entries, cmd.ClearMissingKeys, cmd.BasePriority);
                        break;
                    }

                case BackgroundLayerOperation.ExecuteOnElements:
                    {
                        if (cmd.ElementCommands == null || cmd.ElementCommands.Count == 0)
                            break;

                        await bg.ExecuteOnLayerElementsAsync(layerIndex, cmd.ElementCommands, ctx.Vars, ct);
                        break;
                    }

                case BackgroundLayerOperation.SetEnabled:
                    {
                        var enabled = cmd.Enabled.GetOrDefault(ctx, true);
                        bg.SetLayerEnabled(layerIndex, enabled);
                        break;
                    }

                case BackgroundLayerOperation.MarkDirty:
                    bg.MarkDirty();
                    break;
            }
        }
    }
}
