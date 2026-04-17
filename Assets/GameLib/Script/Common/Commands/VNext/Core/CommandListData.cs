#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CommandListFunctionNameAttribute : Attribute
    {
        public string Name { get; }

        public CommandListFunctionNameAttribute(string name)
        {
            Name = name ?? string.Empty;
        }
    }

    public enum CommandListMutationOperation
    {
        Append = 0,
        Override = 1,
        ClearAppended = 2,
        ClearOverride = 3,
        ClearAll = 4,
        Prepend = 5,
    }

    [Serializable]
    public sealed class CommandListMutationStep
    {
        [LabelText("Operation")]
        [Tooltip("Append: runtime の末尾に追加します。Prepend: runtime の先頭に追加します。Override: runtime 中の表示をこの Commands で置き換えます。ClearAppended: runtime 追加分だけ消します。ClearOverride: Override だけ消します。ClearAll: runtime mutation をすべて消します。Pool / Release 時は、この CommandListData を登録した scope の mutation service により自動で ClearAll されます。")]
        public CommandListMutationOperation Operation = CommandListMutationOperation.Append;

        [ShowIf(nameof(RequiresCommands))]
        [LabelText("Commands")]
        [CommandListFunctionName("CommandList.Mutation.Commands")]
        public CommandListData Commands = new();

        public bool RequiresCommands()
            => Operation == CommandListMutationOperation.Append
            || Operation == CommandListMutationOperation.Prepend
            || Operation == CommandListMutationOperation.Override;
    }

    [Serializable]
    public sealed class CommandListData
    {
        // Debug metadata: helps pinpoint which CommandListData resource triggered an error.
        // - _debugOwner: the Unity object that owns this list (MonoBehaviour/ScriptableObject/etc)
        // - _debugFieldPath: optional field name/path on the owner
        // NOTE: This is best-effort and safe to leave unset.
        [SerializeField, HideInInspector]
        UnityEngine.Object? _debugOwner;

        [SerializeField, HideInInspector]
        string _debugFieldPath = string.Empty;

        [SerializeField, HideInInspector]
        string _debugAssetPath = string.Empty;

        [SerializeField, LabelText("Function Name")]
        string _functionName = string.Empty;

        [SerializeReference]
        List<ICommandSource> _commands = new();

        [NonSerialized]
        List<ICommandSource>? _runtimeAppendedCommands;

        [NonSerialized]
        List<ICommandSource>? _runtimePrependedCommands;

        [NonSerialized]
        List<ICommandSource>? _runtimeOverrideCommands;

        [NonSerialized]
        bool _hasRuntimeOverride;

        public IReadOnlyList<ICommandSource> Commands => _commands;
        public int Count
        {
            get
            {
                if (_hasRuntimeOverride)
                    return _runtimeOverrideCommands?.Count ?? 0;

                var prependedCount = _runtimePrependedCommands?.Count ?? 0;
                var baseCount = _commands?.Count ?? 0;
                var appendedCount = _runtimeAppendedCommands?.Count ?? 0;
                return prependedCount + baseCount + appendedCount;
            }
        }
        public string FunctionName => _functionName;

        public ICommandSource? GetAt(int index)
        {
            if (index < 0)
                return null;

            if (_hasRuntimeOverride)
            {
                if (_runtimeOverrideCommands == null || index >= _runtimeOverrideCommands.Count)
                    return null;

                return _runtimeOverrideCommands[index];
            }

            var prepended = _runtimePrependedCommands;
            var prependedCount = prepended?.Count ?? 0;
            if (index < prependedCount && prepended != null)
                return prepended[index];

            index -= prependedCount;

            var baseCommands = _commands;
            var baseCount = baseCommands?.Count ?? 0;
            if (index < baseCount && baseCommands != null)
                return baseCommands[index];

            index -= baseCount;

            var appended = _runtimeAppendedCommands;
            if (appended == null || index < 0 || index >= appended.Count)
                return null;

            return appended[index];
        }

        public void Add(ICommandSource source)
        {
            _commands ??= new List<ICommandSource>();
            _commands.Add(source);
        }

        public void Add(ICommandData data)
        {
            Add(new InlineCommandSource(data));
        }

        public void SetCommands(List<ICommandSource> commands)
        {
            _commands = commands ?? new List<ICommandSource>();
        }

        public void SetCommands(CommandListData? other)
        {
            if (other == null)
            {
                _commands = new List<ICommandSource>();
            }
            else
            {
                _commands = other._commands ?? new List<ICommandSource>();
            }
        }

        public void EnsureIntegrity()
        {
            _commands ??= new List<ICommandSource>();
        }

        public void AddRuntimeCommand(ICommandSource? source)
        {
            if (source == null)
                return;

            _runtimeAppendedCommands ??= new List<ICommandSource>(4);
            _runtimeAppendedCommands.Add(source);
        }

        public void AddRuntimeCommands(CommandListData? other)
        {
            if (other == null)
                return;

            var count = other.Count;
            if (count <= 0)
                return;

            _runtimeAppendedCommands ??= new List<ICommandSource>(count);
            for (int i = 0; i < count; i++)
            {
                var source = other.GetAt(i);
                if (source == null)
                    continue;

                _runtimeAppendedCommands.Add(source);
            }
        }

        public void PrependRuntimeCommands(CommandListData? other)
        {
            if (other == null)
                return;

            var count = other.Count;
            if (count <= 0)
                return;

            var prependedCommands = new List<ICommandSource>(count);
            for (int i = 0; i < count; i++)
            {
                var source = other.GetAt(i);
                if (source == null)
                    continue;

                prependedCommands.Add(source);
            }

            if (prependedCommands.Count == 0)
                return;

            _runtimePrependedCommands ??= new List<ICommandSource>(prependedCommands.Count);
            _runtimePrependedCommands.InsertRange(0, prependedCommands);
        }

        public void SetRuntimeOverride(CommandListData? other)
        {
            _hasRuntimeOverride = true;

            if (other == null)
            {
                _runtimeOverrideCommands = new List<ICommandSource>(0);
                return;
            }

            var count = other.Count;
            if (count <= 0)
            {
                _runtimeOverrideCommands = new List<ICommandSource>(0);
                return;
            }

            _runtimeOverrideCommands = new List<ICommandSource>(count);
            for (int i = 0; i < count; i++)
            {
                var source = other.GetAt(i);
                if (source == null)
                    continue;

                _runtimeOverrideCommands.Add(source);
            }
        }

        public void ClearRuntimeOverride()
        {
            _hasRuntimeOverride = false;
            _runtimeOverrideCommands = null;
        }

        public void ClearRuntimeAppendedCommands()
        {
            _runtimeAppendedCommands = null;
        }

        public void ClearRuntimeMutations()
        {
            _hasRuntimeOverride = false;
            _runtimeOverrideCommands = null;
            _runtimePrependedCommands = null;
            _runtimeAppendedCommands = null;
        }

        public CommandListData CreateRuntimeCopy()
        {
            var copy = new CommandListData();
            copy.SetCommands(this);
            copy._debugOwner = _debugOwner;
            copy._debugFieldPath = _debugFieldPath;
            copy._debugAssetPath = _debugAssetPath;
            copy._functionName = _functionName;

            if (_runtimeAppendedCommands != null && _runtimeAppendedCommands.Count > 0)
                copy._runtimeAppendedCommands = new List<ICommandSource>(_runtimeAppendedCommands);

            if (_runtimePrependedCommands != null && _runtimePrependedCommands.Count > 0)
                copy._runtimePrependedCommands = new List<ICommandSource>(_runtimePrependedCommands);

            if (_runtimeOverrideCommands != null && _runtimeOverrideCommands.Count > 0)
                copy._runtimeOverrideCommands = new List<ICommandSource>(_runtimeOverrideCommands);

            copy._hasRuntimeOverride = _hasRuntimeOverride;
            return copy;
        }

        public bool ApplyRuntimeMutation(CommandListMutationStep? step, ICommandListRuntimeMutationService? mutationService = null)
        {
            if (step == null)
                return false;

            return ApplyRuntimeMutation(step.Operation, step.Commands, mutationService);
        }

        public bool ApplyRuntimeMutation(
            CommandListMutationOperation operation,
            CommandListData? commands,
            ICommandListRuntimeMutationService? mutationService = null)
        {
            mutationService?.Register(this);

            switch (operation)
            {
                case CommandListMutationOperation.Append:
                    AddRuntimeCommands(commands);
                    return true;
                case CommandListMutationOperation.Prepend:
                    PrependRuntimeCommands(commands);
                    return true;
                case CommandListMutationOperation.Override:
                    SetRuntimeOverride(commands);
                    return true;
                case CommandListMutationOperation.ClearAppended:
                    ClearRuntimeAppendedCommands();
                    return true;
                case CommandListMutationOperation.ClearOverride:
                    ClearRuntimeOverride();
                    return true;
                case CommandListMutationOperation.ClearAll:
                    ClearRuntimeMutations();
                    return true;
                default:
                    return false;
            }
        }

        public void BindDebugOwner(UnityEngine.Object owner, string? fieldPath = null)
        {
            _debugOwner = owner;
            _debugFieldPath = fieldPath ?? string.Empty;

#if UNITY_EDITOR
            try
            {
                _debugAssetPath = UnityEditor.AssetDatabase.GetAssetPath(owner) ?? string.Empty;
            }
            catch
            {
                _debugAssetPath = string.Empty;
            }
#endif
            TryApplyFunctionName(owner, _debugFieldPath);
        }

        public void SetFunctionName(string? name, bool overwrite = false)
        {
            if (string.IsNullOrEmpty(name))
                return;

            if (!overwrite && !string.IsNullOrEmpty(_functionName))
                return;

            _functionName = name;
        }

        public string GetDebugLabel()
        {
            if (_debugOwner == null)
                return string.Empty;

            if (_debugOwner is UnityEngine.Object unityObj && !unityObj)
                return "<destroyed>";

            var ownerType = _debugOwner.GetType().Name;
            var ownerName = _debugOwner.name;
            var field = string.IsNullOrEmpty(_debugFieldPath) ? string.Empty : $".{_debugFieldPath}";
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(_debugAssetPath))
                return $"{ownerType}('{ownerName}'){field} AssetPath='{_debugAssetPath}'";
#endif

            return $"{ownerType}('{ownerName}'){field}";
        }

        void TryApplyFunctionName(UnityEngine.Object owner, string fieldPath)
        {
            if (owner == null || string.IsNullOrEmpty(fieldPath))
                return;

            var field = FindFieldInfo(owner.GetType(), fieldPath);
            if (field == null)
                return;

            var attribute = field.GetCustomAttribute<CommandListFunctionNameAttribute>();
            if (attribute == null)
                return;

            SetFunctionName(attribute.Name, overwrite: false);
        }

        static FieldInfo? FindFieldInfo(Type ownerType, string fieldPath)
        {
            if (ownerType == null || string.IsNullOrEmpty(fieldPath))
                return null;

            var currentType = ownerType;
            FieldInfo? currentField = null;
            var parts = fieldPath.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                var raw = parts[i];
                var name = StripIndexer(raw);
                currentField = currentType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (currentField == null)
                    return null;

                currentType = currentField.FieldType;
                if (HasIndexer(raw))
                {
                    currentType = ResolveElementType(currentType) ?? currentType;
                }
            }

            return currentField;
        }

        static bool HasIndexer(string token) => token.IndexOf('[') >= 0;

        static string StripIndexer(string token)
        {
            var idx = token.IndexOf('[');
            return idx >= 0 ? token.Substring(0, idx) : token;
        }

        static Type? ResolveElementType(Type type)
        {
            if (type.IsArray)
                return type.GetElementType();

            if (type.IsGenericType)
            {
                var def = type.GetGenericTypeDefinition();
                if (def == typeof(List<>))
                    return type.GetGenericArguments()[0];
            }

            return null;
        }
    }
}
