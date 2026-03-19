#nullable enable

using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class FunctionInitialVarEntry
    {
        [InlineProperty]
        [LabelText("Key")]
        public VarKeyRef Key;

        [HideLabel]
        [LabelText("Value")]
        public DynamicValue Value;

        public string ListLabel
        {
            get
            {
                var keyLabel = string.IsNullOrEmpty(Key.StableKey)
                    ? $"VarId={Key.VarId}"
                    : Key.StableKey;
                var valueLabel = string.IsNullOrEmpty(Value.SourceTypeName)
                    ? "None"
                    : Value.SourceTypeName;
                return $"{keyLabel} <- {valueLabel}";
            }
        }
    }

    public sealed class FunctionCommandData : ICommandData
    {
        readonly CommandFunctionPreset? _function;
        readonly IReadOnlyList<FunctionInitialVarEntry>? _initialVars;
        readonly string _debugName;

        public int CommandId => CommandIds.Function;
        public string DebugData => _debugName;
        public CommandFunctionPreset? Function => _function;
        public IReadOnlyList<FunctionInitialVarEntry>? InitialVars => _initialVars;

        public FunctionCommandData(
            CommandFunctionPreset? function,
            IReadOnlyList<FunctionInitialVarEntry>? initialVars,
            string debugName)
        {
            _function = function;
            _initialVars = initialVars;
            _debugName = debugName ?? string.Empty;
        }
    }
}
