#nullable enable
using System.Threading;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Game;
using Game.Channel;
using Game.Common;
using Game.MaterialFx;
using VContainer;
using UnityEngine;

namespace Game.Commands.VNext
{
    public sealed class TextChannelExecutor : ICommandExecutor
    {
        static readonly Regex CountNumberPattern = new(@"-?\d+(?:\.\d+)?", RegexOptions.Compiled);
        static readonly TypewriterEventCommandHook[] TypewriterEventHooks =
        {
            TypewriterEventCommandHook.TypewriterStart,
            TypewriterEventCommandHook.TextShowed,
            TypewriterEventCommandHook.TextDisappeared,
            TypewriterEventCommandHook.CharacterVisible,
            TypewriterEventCommandHook.CharacterWaitStarted,
            TypewriterEventCommandHook.CharacterWaitFinished,
            TypewriterEventCommandHook.Message,
        };

        public int CommandId => CommandIds.TextChannel;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not TextChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TextChannelCommandData is required.");

            if (!TextChannelResolveUtility.TryResolvePlayerWithHub(ctx, typed.ChannelTag, out var player, out var hub) || player == null || hub == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                //Debug.Log($"[TextChannelExecutor] Player resolve failed. tag={typed.ChannelTag} chain={DescribeAncestorChain(ctx.Scope)} hubs={DescribeHubChain(ctx.Scope)}");
#endif
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"TextChannel '{typed.ChannelTag}' not found.");
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var hubScope = hub is TextChannelHubService textHub ? DescribeScope(textHub.OwnerScope) : hub.GetType().Name;
            //Debug.Log($"[TextChannelExecutor] Execute tag={typed.ChannelTag} mode={typed.Mode} action={typed.Action} scope={DescribeScope(ctx.Scope)} actor={DescribeScope(ctx.Actor)} hub={hubScope}");
#endif

            if (typed.ApplyMaterialFx)
            {
                if (typed.MaterialFxSource.TryGet(ctx, out var payload) && payload != null)
                    ApplyMaterialFxPayload(payload, ctx, player);
            }

            if (typed.Mode == TextChannelCommandMode.Single && typed.ApplyFontSize)
            {
                if (typed.FontSize.TryGet(ctx, out var size))
                    player.SetFontSize(size);
            }

            switch (typed.Mode)
            {
                case TextChannelCommandMode.Single:
                    await ExecuteSingleAsync(typed, player, hub, ctx, ct);
                    return;
                case TextChannelCommandMode.Preset:
                    await ExecutePresetAsync(typed, player, ctx, ct);
                    return;
                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unknown TextChannelCommandMode: {typed.Mode}");
            }
        }

        static async UniTask ExecuteSingleAsync(TextChannelCommandData typed, ITextChannelPlayer player, ITextChannelHubService hub, CommandContext ctx, CancellationToken ct)
        {
            if (typed.Action == TextChannelCommandAction.SetText ||
                typed.Action == TextChannelCommandAction.Append ||
                typed.Action == TextChannelCommandAction.Clear)
            {
                hub.UnregisterDynamicBinding(typed.ChannelTag);
            }

            var shouldWaitTypewriter = false;
            var typewriterEventConfig = BuildTypewriterEventRuntimeConfig(typed, hub, ctx);
            player.SetTypewriterEventCommands(typewriterEventConfig, new TypewriterEventCommandRuntimeContext(ctx));

            switch (typed.Action)
            {
                case TextChannelCommandAction.SetText:
                    if (typed.ApplyText)
                    {
                        player.ApplyStyleCommand(typed.StyleCommand);
                        var resolved = typed.Text.Resolve(ctx);
                        var resolvedText = resolved ?? string.Empty;
                        if (typed.PlayMode == TextPlayMode.Count)
                        {
                            resolvedText = TextChannelTextEvaluationUtility.EvaluateRichTextTemplate(ctx, resolvedText);
                            var settings = typed.TextSettings;
                            settings.UseCounter = true;

                            if (typed.OverrideCountStartValue && typed.CountStartValue.TryGet(ctx, out var startValue))
                            {
                                var startText = BuildCountStartText(resolvedText, startValue, settings);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                                //Debug.Log($"[TextChannelExecutor] Count start override tag={player.Tag} startValue={startValue} startText='{startText}' target='{resolved}'");
#endif
                                player.SetText(startText, TextPlayMode.Instant);
                            }

                            player.SetText(resolvedText, TextPlayMode.Count, settings);
                        }
                        else
                        {
                            player.SetText(resolvedText, typed.PlayMode, typed.TextSettings);
                        }

                        shouldWaitTypewriter = typed.PlayMode == TextPlayMode.Typewriter && typed.WaitForTypewriterComplete;

                        if (typed.EnableDynamicBind)
                            TryRegisterDynamicBinding(typed, hub, ctx);
                    }
                    break;
                case TextChannelCommandAction.Append:
                    if (typed.ApplyText)
                    {
                        player.ApplyStyleCommand(typed.StyleCommand);
                        var resolved = typed.Text.Resolve(ctx);
                        var resolvedText = resolved ?? string.Empty;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        //Debug.Log($"[TextChannelExecutor] Append tag={player.Tag} mode={typed.PlayMode} text={PreviewText(resolved)}");
#endif
                        player.Append(resolvedText, typed.PlayMode);
                        shouldWaitTypewriter = typed.PlayMode == TextPlayMode.Typewriter && typed.WaitForTypewriterComplete;
                    }
                    break;
                case TextChannelCommandAction.Clear:
                    if (typed.ApplyText)
                        player.Clear();
                    break;
                case TextChannelCommandAction.Skip:
                    if (typed.ApplyText)
                        player.Skip();
                    break;
                case TextChannelCommandAction.SetVisible:
                    player.SetVisible(typed.Visible);
                    break;
                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unknown TextChannelCommandAction: {typed.Action}");
            }

            if (shouldWaitTypewriter)
            {
                await player.WaitForTypewriterCompleteAsync(ct);
            }
        }

        static TypewriterEventCommandRuntimeConfig? BuildTypewriterEventRuntimeConfig(TextChannelCommandData typed, ITextChannelHubService hub, CommandContext ctx)
        {
            if (!ShouldUseTypewriterEventCommands(typed))
                return null;

            TypewriterEventCommandBindings? defaults = null;
            if (hub.TryGetChannelDef(typed.ChannelTag, out var channelDef) && channelDef is TextChannelDef textDef)
                defaults = textDef.TypewriterEventCommands;

            var applyCommandMutations = typed.ApplyTypewriterEventCommands;
            var hasDefaultCommands = defaults != null && defaults.Apply && defaults.HasAnyCommands();
            if (!hasDefaultCommands && !applyCommandMutations)
                return null;

            ICommandListRuntimeMutationService? mutationService = null;
            if (ctx.Resolver.TryResolve<ICommandListRuntimeMutationService>(out var resolvedMutationService) && resolvedMutationService != null)
                mutationService = resolvedMutationService;

            var config = new TypewriterEventCommandRuntimeConfig();
            var hasResolvedCommands = false;
            for (int i = 0; i < TypewriterEventHooks.Length; i++)
            {
                var hook = TypewriterEventHooks[i];
                var resolved = CloneCommandList(defaults?.Apply == true ? defaults.Resolve(hook) : null);

                if (applyCommandMutations)
                {
                    var mutation = typed.TypewriterEventCommands.Resolve(hook);
                    if (mutation != null && mutation.Apply)
                    {
                        resolved ??= new CommandListData();
                        var step = new CommandListMutationStep
                        {
                            Operation = mutation.Operation,
                            Commands = CloneCommandList(mutation.Commands) ?? new CommandListData(),
                        };
                        resolved.ApplyRuntimeMutation(step, mutationService);
                    }
                }

                if (resolved == null || resolved.Count <= 0)
                    continue;

                SetTypewriterEventCommands(config, hook, resolved);
                hasResolvedCommands = true;
            }

            return hasResolvedCommands ? config : null;
        }

        static bool ShouldUseTypewriterEventCommands(TextChannelCommandData typed)
        {
            return typed.Mode == TextChannelCommandMode.Single &&
                   typed.ApplyText &&
                   (typed.Action == TextChannelCommandAction.SetText || typed.Action == TextChannelCommandAction.Append) &&
                   typed.PlayMode == TextPlayMode.Typewriter;
        }

        static CommandListData? CloneCommandList(CommandListData? source)
        {
            if (source == null)
                return null;

            var clone = new CommandListData();
            clone.SetCommands(source);
            return clone;
        }

        static void SetTypewriterEventCommands(TypewriterEventCommandRuntimeConfig config, TypewriterEventCommandHook hook, CommandListData commands)
        {
            switch (hook)
            {
                case TypewriterEventCommandHook.TypewriterStart:
                    config.OnTypewriterStart = commands;
                    return;
                case TypewriterEventCommandHook.TextShowed:
                    config.OnTextShowed = commands;
                    return;
                case TypewriterEventCommandHook.TextDisappeared:
                    config.OnTextDisappeared = commands;
                    return;
                case TypewriterEventCommandHook.CharacterVisible:
                    config.OnCharacterVisible = commands;
                    return;
                case TypewriterEventCommandHook.CharacterWaitStarted:
                    config.OnCharacterWaitStarted = commands;
                    return;
                case TypewriterEventCommandHook.CharacterWaitFinished:
                    config.OnCharacterWaitFinished = commands;
                    return;
                case TypewriterEventCommandHook.Message:
                    config.OnMessage = commands;
                    return;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static string PreviewText(string text)
        {
            if (text == null)
                return "<null>";

            var normalized = text.Replace("\r", string.Empty).Replace("\n", "\\n");
            const int MaxLen = 80;
            if (normalized.Length <= MaxLen)
                return $"\"{normalized}\"";

            return $"\"{normalized.Substring(0, MaxLen)}...\"";
        }
#endif

        static async UniTask ExecutePresetAsync(TextChannelCommandData typed, ITextChannelPlayer player, CommandContext ctx, CancellationToken ct)
        {
            var preset = typed.AnimationPreset;
            if (preset == null || preset.Steps == null || preset.Steps.Count == 0)
                return;

            var isInfiniteLoopPreset = preset.Loop && preset.LoopCount < 0;
            if (isInfiniteLoopPreset && typed.AwaitMode == FlowRunAwaitMode.WaitForCompletion)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[TextChannelExecutor] Infinite preset loop cannot be awaited. Switching to RunInBackground. tag={player.Tag}");
#endif
                player.PlayPresetAsync(preset, ctx.Vars, ct).Forget();
                return;
            }

            if (typed.AwaitMode == FlowRunAwaitMode.RunInBackground)
            {
                player.PlayPresetAsync(preset, ctx.Vars, ct).Forget();
                return;
            }

            await player.PlayPresetAsync(preset, ctx.Vars, ct);
        }

        static void ApplyMaterialFxPayload(MaterialFxPayload payload, CommandContext ctx, ITextChannelPlayer player)
        {
            var fx = player.MaterialFx;
            if (fx == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "MaterialFx is not available on this channel.");

            if (!ctx.Resolver.TryResolve<IMaterialFxPropertyRegistry>(out var registry) || registry == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IMaterialFxPropertyRegistry is missing.");

            var context = payload.ContextTag ?? string.Empty;
            if (payload.ClearContextFirst)
                fx.ClearContext(context);

            var entries = payload.Entries;
            if (entries == null || entries.Count == 0)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var key = e.Key ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (!registry.TryGetValueType(key, out var kind))
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"MaterialFx key not found: '{key}'");

                var value = e.Value.ToTypedValue(kind, ctx);
                var lifetime = e.LifetimeSeconds;
                if (lifetime == 0f)
                    lifetime = -1f;

                if (e.ApplyWeightFade)
                {
                    fx.SetLayerFade(key, context, value, e.ResolveFadeDuration(ctx), e.FadeEase, e.BlendMode, payload.Priority, lifetime);
                    continue;
                }

                fx.SetLayer(key, context, value, e.BlendMode, payload.Priority, lifetime);
            }
        }

        static string BuildCountStartText(string targetText, float startValue, in SetTextSettings settings)
        {
            var formatter = new TextCounterProcessor();
            var formatted = formatter.FormatNumber(startValue, settings);

            if (string.IsNullOrEmpty(targetText))
                return formatted;

            if (!CountNumberPattern.IsMatch(targetText))
                return formatted;

            return CountNumberPattern.Replace(targetText, formatted);
        }

        static void TryRegisterDynamicBinding(TextChannelCommandData typed, ITextChannelHubService hub, CommandContext ctx)
        {
            if (!typed.Text.HasSource)
                return;

            var snapshotVars = new VarStore();
            ctx.Vars.MergeInto(snapshotVars, overwrite: true);

            var counterSettings = typed.DynamicBindCounterSettings;
            counterSettings.UseCounter = typed.DynamicBindPlayMode == TextDynamicBindingPlayMode.Counter;

            var request = new TextDynamicBindingRegisterRequest(
                typed.ChannelTag,
                typed.Text,
                typed.DynamicBindPlayMode,
                counterSettings,
                snapshotVars,
                ctx,
                ctx.Actor ?? ctx.Scope,
                TextDynamicBindingDefaults.PollIntervalFrames);

            if (!hub.RegisterDynamicBinding(in request))
            {
                Debug.LogWarning($"[TextChannelExecutor] Dynamic bind registration was rejected. tag={typed.ChannelTag} source={typed.Text.SourceTypeName}");
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static string DescribeAncestorChain(IScopeNode? scope)
        {
            if (scope == null)
                return "<null>";

            var parts = new System.Collections.Generic.List<string>();
            foreach (var node in scope.EnumerateAncestors(includeSelf: true))
            {
                parts.Add(DescribeScope(node));
            }
            return string.Join(" <- ", parts);
        }

        static string DescribeHubChain(IScopeNode? scope)
        {
            if (scope == null)
                return "<null>";

            var parts = new System.Collections.Generic.List<string>();
            foreach (var node in scope.EnumerateAncestors(includeSelf: true))
            {
                var resolver = node.Resolver;
                if (resolver == null)
                    continue;

                if (resolver.TryResolve<ITextChannelHubService>(out var hub) && hub != null)
                {
                    var hubScope = hub is TextChannelHubService textHub ? DescribeScope(textHub.OwnerScope) : hub.GetType().Name;
                    parts.Add(hubScope);
                }
            }

            if (parts.Count == 0)
                return "<none>";

            return string.Join(" <- ", parts);
        }

        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "<null>";
            if (scope is UnityEngine.Object unityObj && !unityObj)
                return "<destroyed>";
            var id = scope.Identity?.Id;
            if (!string.IsNullOrEmpty(id))
                return $"{id} ({scope.Kind})";
            return scope.GetType().Name;
        }

        static bool IsToastDebugContext(CommandContext ctx)
        {
            return ContainsToastMarker(ctx.Actor) || ContainsToastMarker(ctx.Scope);
        }

        static bool ContainsToastMarker(IScopeNode? scope)
        {
            foreach (var node in scope?.EnumerateAncestors(includeSelf: true) ?? System.Array.Empty<IScopeNode>())
            {
                var id = node?.Identity?.Id;
                if (string.IsNullOrEmpty(id))
                    continue;

                if (id.IndexOf("UIToast", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
#endif

    }
}
