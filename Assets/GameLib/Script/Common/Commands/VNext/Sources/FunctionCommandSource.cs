#nullable enable

using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class FunctionCommandSource : ICommandSource, ICommandSourceExecutionControl
    {
        [SerializeField]
        bool enabled = true;

        [SerializeField]
        [LabelText("Function")]
        DynamicValue<CommandFunctionPreset> function;

        [SerializeField]
        [LabelText("Initial Vars")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, ListElementLabelName = nameof(FunctionInitialVarEntry.ListLabel))]
        List<FunctionInitialVarEntry> initialVars = new();

        public string DebugName => BuildDebugName();
        public bool IsExecutionEnabled => enabled;

        public void SetExecutionEnabled(bool value)
        {
            enabled = value;
        }

        public bool TryResolve(CommandResolveContext ctx, out ICommandData data)
        {
            data = null!;

            if (!function.TryGet(ctx, out CommandFunctionPreset preset) || preset == null)
            {
                ctx.Logger.LogResolveFailed(this, "CommandFunctionPreset failed to resolve.");
                return false;
            }

            var commands = preset.Commands;
            if (commands == null || commands.Count == 0)
            {
                ctx.Logger.LogResolveFailed(this, "CommandFunctionPreset.Commands is empty.");
                return false;
            }

            data = new FunctionCommandData(
                preset,
                initialVars,
                $"Function={BuildFunctionLabel(preset)} Vars={initialVars?.Count ?? 0} Body={commands.Count}");
            return true;
        }

        string BuildDebugName()
        {
            var sourceType = function.SourceTypeName;
            if (string.IsNullOrEmpty(sourceType) || string.Equals(sourceType, "None", StringComparison.Ordinal))
                return "FunctionCommandSource (<unset>)";

            var debugData = function.SourceDebugData;
            if (string.IsNullOrEmpty(debugData))
                return $"FunctionCommandSource ({sourceType})";

            return $"FunctionCommandSource ({sourceType}: {debugData})";
        }

        static string BuildFunctionLabel(CommandFunctionPreset preset)
        {
            if (preset?.Commands == null)
                return "<null>";

            var debugLabel = preset.Commands.GetDebugLabel();
            if (!string.IsNullOrEmpty(debugLabel))
                return debugLabel;

            var functionName = preset.Commands.FunctionName;
            if (!string.IsNullOrEmpty(functionName))
                return functionName;

            return "InlineFunction";
        }

        public override string ToString()
        {
            return DebugName;
        }
    }
}
