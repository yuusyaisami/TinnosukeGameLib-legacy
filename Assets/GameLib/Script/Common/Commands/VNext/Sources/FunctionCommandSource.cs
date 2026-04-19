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

            var resolved = function.Evaluate(ctx);
            if (!TryResolveFunction(resolved, out var preset))
            {
                ctx.Logger.LogResolveFailed(this, $"CommandFunctionPreset failed to resolve. Resolved={DescribeResolvedValue(resolved)}");
                return false;
            }

            var functionPreset = preset;
            if (functionPreset == null)
            {
                ctx.Logger.LogResolveFailed(this, $"CommandFunctionPreset failed to resolve. Resolved={DescribeResolvedValue(resolved)}");
                return false;
            }

            var commands = functionPreset.Commands;
            if (commands == null || commands.Count == 0)
            {
                ctx.Logger.LogResolveFailed(this, "CommandFunctionPreset.Commands is empty.");
                return false;
            }

            data = new FunctionCommandData(
                functionPreset,
                initialVars,
                $"Function={BuildFunctionLabel(functionPreset)} Vars={initialVars?.Count ?? 0} Body={commands.Count}");
            return true;
        }

        static bool TryResolveFunction(DynamicVariant resolved, out CommandFunctionPreset? preset)
        {
            preset = null;

            if (resolved.TryGet(out CommandFunctionPreset functionPreset) && functionPreset != null)
            {
                preset = functionPreset;
                return true;
            }

            if (resolved.TryGet(out CommandListData commandList) && commandList != null && commandList.Count > 0)
            {
                preset = new CommandFunctionPreset
                {
                    Commands = commandList
                };
                return true;
            }

            return false;
        }

        static string DescribeResolvedValue(DynamicVariant resolved)
        {
            return resolved.Kind switch
            {
                ValueKind.Null => "Null",
                ValueKind.ManagedRef => resolved.AsManagedRef == null
                    ? "ManagedRef(null)"
                    : $"ManagedRef({DescribeManagedRef(resolved.AsManagedRef)})",
                ValueKind.UnityObject => resolved.AsUnityObject == null
                    ? "UnityObject(null)"
                    : $"UnityObject({resolved.AsUnityObject.GetType().Name}: {resolved.AsUnityObject.name})",
                _ => $"{resolved.Kind}({resolved})",
            };
        }

        static string DescribeManagedRef(object managedRef)
        {
            if (managedRef == null)
                return "null";

            if (managedRef is CommandListData commandList)
                return $"CommandListData(count={commandList.Count})";

            if (managedRef is CommandFunctionPreset functionPreset)
                return $"CommandFunctionPreset(commands={functionPreset.Commands?.Count ?? 0})";

            return managedRef.GetType().Name;
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
