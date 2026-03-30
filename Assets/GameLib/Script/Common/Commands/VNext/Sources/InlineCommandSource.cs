#nullable enable
using System;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class InlineCommandSource : ICommandSource, ICommandSourceExecutionControl
    {
        [SerializeField]
        bool enabled = true;

        [HideReferenceObjectPicker]
        [SerializeReference]
        ICommandData? data;

        public InlineCommandSource() { }

        public InlineCommandSource(ICommandData? commandData)
        {
            data = commandData;
        }

        public string DebugName => BuildDebugName();
        public bool IsExecutionEnabled => enabled && data != null;

        public void SetExecutionEnabled(bool value)
        {
            enabled = value;
        }

        public bool TryResolve(CommandResolveContext ctx, out ICommandData resolved)
        {
            resolved = data!;
            if (resolved == null)
            {
                var scopeKind = ctx.Scope.Kind;
                var scopeId = ctx.Scope.Identity?.Id ?? "(none)";
                ctx.Logger.LogResolveFailed(this, $"Inline command data is null. ScopeKind={scopeKind}, ScopeId={scopeId}, AllowRuntimeKeyFallback={ctx.AllowRuntimeKeyFallback}");
                return false;
            }

            return true;
        }

        string BuildDebugName()
        {
            var name = data?.GetType().Name ?? "None";
            const string suffix = "CommandData";
            if (name.EndsWith(suffix, StringComparison.Ordinal))
                name = name.Substring(0, name.Length - suffix.Length);
            return $"InlineCommandSource ({name})";
        }

        public override string ToString()
        {
            return DebugName;
        }
    }
}
