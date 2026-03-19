#nullable enable
using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;

namespace Game.Commands.VNext
{
    public sealed class CommandDebugExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.DebugCommandContext;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not CommandDebugCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CommandDebugCommandData is required.");

            var output = BuildPayload(typed, ctx);
            Debug.Log(output);
            return UniTask.CompletedTask;
        }

        static string BuildPayload(CommandDebugCommandData data, CommandContext ctx)
        {
            var sb = new StringBuilder();
            var label = string.IsNullOrEmpty(data.Label) ? "CommandDebug" : data.Label;
            sb.AppendLine($"[CommandDebug] {label} (CmdId={data.CommandId})");

            if (!string.IsNullOrEmpty(data.Message))
                sb.AppendLine($"Message: {data.Message}");

            sb.AppendLine($"Scope: {DescribeScope(ctx.Scope)}");
            if (ctx.Actor != null)
                sb.AppendLine($"Actor: {DescribeScope(ctx.Actor)}");

            if (data.LogScopeInfo && ctx.Scope?.Identity != null)
                sb.AppendLine($"Scope Identity: Id={ctx.Scope.Identity.Id} Kind={ctx.Scope.Identity.Kind} Active={ctx.Scope.Identity.IsActive}");

            if (data.LogRunnerInfo)
            {
                sb.AppendLine($"Runner: {ctx.Runner.GetType().Name}");
                sb.AppendLine($"Runner Scope: {DescribeScope(ctx.Runner.Scope)}");
                sb.AppendLine($"Runner Vars Version: {ctx.Vars?.GlobalVersion ?? 0}");
            }

            if (data.LogContextSlots)
                AppendContextSlots(sb, ctx);

            if (data.LogOptions)
            {
                var options = ctx.Options;
                sb.AppendLine($"Options: FailurePolicy={options.FailurePolicy} TracePolicy={options.TracePolicy} MaxDepth={options.MaxTraceDepth} MaxFrames={options.MaxTraceFrames} AllowActorFallback={options.AllowActorFallback} AllowRuntimeKeyFallback={options.AllowRuntimeKeyFallback}");
            }

            if (data.LogVarStore)
            {
                var maxEntries = Math.Max(data.MaxVarEntries, 0);
                AppendVarStore(sb, ctx.Vars, maxEntries);
            }

            if (data.LogWatches)
            {
                var maxEntries = Math.Max(data.MaxWatchEntries, 0);
                AppendWatches(sb, data.Watches, ctx, maxEntries);
            }

            return sb.ToString();
        }

        static string DescribeScope(IScopeNode? node)
        {
            if (node == null)
                return "<null>";

            if (node is Component component && component.gameObject != null)
                return component.gameObject.name;

            if (node.Identity != null)
                return $"{node.Identity.Id}:{node.Identity.Kind}";

            return node.GetType().Name;
        }

        static void AppendContextSlots(StringBuilder sb, CommandContext ctx)
        {
            sb.AppendLine("Context Slots:");
            AppendContextSlot(sb, ctx, CommandLtsSlot.ContextA);
            AppendContextSlot(sb, ctx, CommandLtsSlot.ContextB);
            AppendContextSlot(sb, ctx, CommandLtsSlot.ContextC);
            AppendContextSlot(sb, ctx, CommandLtsSlot.ContextD);
        }

        static void AppendContextSlot(StringBuilder sb, CommandContext ctx, CommandLtsSlot slot)
        {
            sb.AppendLine($"  {slot}: {DescribeSlotScope(ctx.GetScope(slot))}");
        }

        static string DescribeSlotScope(IScopeNode? node)
        {
            if (node == null)
                return "<null>";

            if (node.Identity != null)
                return $"{node.Identity.Kind}:{node.Identity.Id}";

            return DescribeScope(node);
        }

        static void AppendVarStore(StringBuilder sb, IVarStore? vars, int maxEntries)
        {
            if (vars == null)
            {
                sb.AppendLine("VarStore: <null>");
                return;
            }

            sb.AppendLine($"VarStore Version={vars.GlobalVersion}");
            var count = 0;
            foreach (var varId in vars.EnumerateVarIds())
            {
                if (varId == 0)
                    continue;

                if (maxEntries > 0 && count >= maxEntries)
                {
                    sb.AppendLine($"  ...var entries truncated after {maxEntries} items.");
                    break;
                }

                var kind = vars.GetVarKind(varId);
                var key = VarIdResolver.TryGetStableKey(varId, out var stableKey) ? stableKey : "<runtime>";
                var value = GetVarValueDescription(vars, varId, kind);
                sb.AppendLine($"  varId={varId} key={key} kind={kind} value={value}");
                count++;
            }

            if (count == 0)
                sb.AppendLine("  (no vars)");
        }

        static void AppendWatches(StringBuilder sb, System.Collections.Generic.IReadOnlyList<CommandDebugWatchEntry>? watches, CommandContext ctx, int maxEntries)
        {
            if (watches == null || watches.Count == 0)
            {
                sb.AppendLine("Watches: (none)");
                return;
            }

            if (ctx.Scope == null)
            {
                sb.AppendLine("Watches: (scope is null)");
                return;
            }

            sb.AppendLine("Watches:");
            var count = 0;
            for (int i = 0; i < watches.Count; i++)
            {
                if (maxEntries > 0 && count >= maxEntries)
                {
                    sb.AppendLine($"  ...watch entries truncated after {maxEntries} items.");
                    break;
                }

                var w = watches[i];
                if (w == null)
                    continue;

                var label = string.IsNullOrWhiteSpace(w.Label) ? $"watch[{i}]" : w.Label.Trim();
                var v = w.Value.Evaluate(ctx);
                if (w.IncludeSourceInfo)
                    sb.AppendLine($"  {label}: {v} (Source={w.Value.SourceTypeName}, Data={w.Value.DebugData})");
                else
                    sb.AppendLine($"  {label}: {v}");

                count++;
            }
        }

        static string GetVarValueDescription(IVarStore vars, int varId, ValueKind kind)
        {
            if (kind == ValueKind.ManagedRef)
            {
                if (vars.TryGetManagedRef(varId, out var managedRef) && managedRef != null)
                    return managedRef.GetType().Name;
                return "null";
            }

            if (vars.TryGetVariant(varId, out var variant))
                return variant.ToString();

            return "<unknown>";
        }
    }
}
