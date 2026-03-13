using System;
using System.Collections.Generic;
using System.Globalization;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Common
{
    [Serializable]
    public sealed class BlackboardDebugView
    {
        [FoldoutGroup("Query Inputs")]
        [LabelText("Stable Key / Alias")]
        [Tooltip("Specify the stable key, alias, or numeric ID to inspect via TryGlobalGetVariant.")]
        public string queryKey = string.Empty;

        [FoldoutGroup("Query Inputs")]
        [LabelText("Var Id Override")]
        [Tooltip("Numerical varId that takes precedence over the key/alias when non-zero.")]
        [VarIdDropdown]
        public int queryVarId;

        [FoldoutGroup("Query Result")]
        [ShowInInspector, ReadOnly, LabelText("Input Used")]
        public string LastQueryInput { get; private set; } = "(none)";

        [FoldoutGroup("Query Result")]
        [ShowInInspector, ReadOnly, LabelText("Queried Id")]
        public int LastQueriedVarId { get; private set; }

        [FoldoutGroup("Query Result")]
        [ShowInInspector, ReadOnly, LabelText("Resolved Key")]
        public string LastResolvedKey { get; private set; } = "(none)";

        [FoldoutGroup("Query Result")]
        [ShowInInspector, ReadOnly, LabelText("Kind")]
        public string LastQueryKind { get; private set; } = "(none)";

        [FoldoutGroup("Query Result")]
        [ShowInInspector, ReadOnly, LabelText("Value")]
        public string LastQueryValue { get; private set; } = "(none)";

        [FoldoutGroup("Query Result")]
        [ShowInInspector, ReadOnly, LabelText("Status")]
        public string LastQueryStatus { get; private set; } = "(idle)";

        [FoldoutGroup("Local Store")]
        [ShowInInspector, ReadOnly, LabelText("Registered Vars")]
        public List<BlackboardVarRow> LocalVars => BuildLocalRows();

        [NonSerialized]
        IBlackboardService _blackboard;

#if UNITY_EDITOR
        [NonSerialized]
        UnityEngine.MonoBehaviour _ownerForEditor;
        static double _lastRepaintTime = 0.0;
        const double RepaintIntervalSeconds = 0.1; // throttle frequency
#endif

        public void Initialize(IBlackboardService blackboard)
        {
            _blackboard = blackboard;
            LastQueryStatus = "(idle)";
            LastQueryValue = "(none)";
            LastQueryKind = "(none)";
            LastResolvedKey = "(none)";
            LastQueriedVarId = 0;
            LastQueryInput = "(none)";
        }

        /// <summary>
        /// Editor-only initialize that accepts owner MonoBehaviour to allow forcing Inspector repaints.
        /// </summary>
        public void Initialize(IBlackboardService blackboard, UnityEngine.MonoBehaviour owner)
        {
            Initialize(blackboard);
#if UNITY_EDITOR
            _ownerForEditor = owner;
            UnityEditor.EditorApplication.update -= OnEditorUpdate;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
#endif
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= OnEditorUpdate;
            _ownerForEditor = null;
#endif
        }

#if UNITY_EDITOR
        void OnEditorUpdate()
        {
            // Throttle to avoid excessive editor CPU usage
            var now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now - _lastRepaintTime < RepaintIntervalSeconds)
                return;

            _lastRepaintTime = now;

            // Refresh query result and local rows so inspector shows fresh data
            if (_blackboard != null && _ownerForEditor != null)
            {
                // Force recompute of properties (they are computed on access)
                // and mark owner as dirty so inspector will repaint
                try
                {
                    UnityEditor.EditorUtility.SetDirty(_ownerForEditor);
                }
                catch { }

                // Repaint all views to ensure inspector updates even when mouse not over it
                try
                {
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                }
                catch { }
            }
        }
#endif

        [FoldoutGroup("Query Inputs")]
        [Button(ButtonSizes.Medium)]
        public void InspectGlobalVar()
        {
            if (_blackboard == null)
            {
                LastQueryStatus = "Blackboard not ready";
                return;
            }

            var trimmedKey = string.IsNullOrWhiteSpace(queryKey) ? string.Empty : queryKey.Trim();
            LastQueryInput = string.IsNullOrEmpty(trimmedKey) ? "(none)" : trimmedKey;

            var resolvedId = ResolveVarId(trimmedKey);
            LastQueriedVarId = resolvedId;

            if (resolvedId == 0)
            {
                LastResolvedKey = "(none)";
                LastQueryKind = "(none)";
                LastQueryValue = "(none)";
                LastQueryStatus = "Specify a valid varId or stable key.";
                return;
            }

            LastResolvedKey = VarIdResolver.TryGetIdToStable(resolvedId) ?? LastQueryInput;

            if (_blackboard.TryGlobalGetVariant(resolvedId, out var variant))
            {
                LastQueryKind = variant.Kind.ToString();
                LastQueryValue = variant.ToString();
                LastQueryStatus = "Found via TryGlobalGetVariant";
                return;
            }

            LastQueryKind = "(none)";
            LastQueryValue = "(missing)";
            LastQueryStatus = "Variable not set in this scope hierarchy.";
        }

        List<BlackboardVarRow> BuildLocalRows()
        {
            if (_blackboard?.LocalVars == null)
                return new List<BlackboardVarRow>();

            var rows = new List<BlackboardVarRow>();
            foreach (var varId in _blackboard.LocalVars.EnumerateVarIds())
            {
                var kind = _blackboard.LocalVars.GetVarKind(varId);
                var version = _blackboard.LocalVars.GetVarVersion(varId);
                var keyName = VarIdResolver.TryGetIdToStable(varId) ?? $"varId={varId}";
                string valueText;

                if (kind == ValueKind.ManagedRef)
                {
                    if (_blackboard.LocalVars.TryGetManagedRef(varId, out var managed))
                    {
                        valueText = managed?.ToString() ?? "(null)";
                    }
                    else
                    {
                        valueText = "(null)";
                    }
                }
                else if (_blackboard.LocalVars.TryGetVariant(varId, out var variant))
                {
                    valueText = variant.ToString();
                }
                else
                {
                    valueText = "(unavailable)";
                }

                rows.Add(new BlackboardVarRow(varId, keyName, kind.ToString(), version, valueText));
            }

            return rows;
        }

        // ------------------------------------------------------------
        // Write (test) inputs - allow inspector-driven test writes to this blackboard
        // ------------------------------------------------------------

        [FoldoutGroup("Query Inputs")]
        [LabelText("Write Entries")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false, DraggableItems = false)]
        [SerializeField]
        List<BlackboardWriteEntry> writeEntries = new();

        [FoldoutGroup("Query Inputs")]
        [Button(ButtonSizes.Medium)]
        public void ApplyAllWrites()
        {
            if (_blackboard == null)
            {
                LastQueryStatus = "Blackboard not ready";
                return;
            }

            for (int i = 0; i < writeEntries.Count; i++)
            {
                ApplyWrite(i);
            }
        }

        void ApplyWrite(int index)
        {
            if (index < 0 || index >= writeEntries.Count)
                return;

            var entry = writeEntries[index];
            var trimmedKey = string.IsNullOrWhiteSpace(entry.queryKey) ? string.Empty : entry.queryKey.Trim();
            var resolvedId = ResolveVarId(trimmedKey, entry.queryVarId);
            entry.LastStatus = "(idle)"; // reset

            if (resolvedId == 0)
            {
                entry.LastStatus = "Invalid varId / key";
                return;
            }

            var value = entry.value.Evaluate(null);
            var fallback = entry.fallback;

            try
            {
                var ok = _blackboard.TryGlobalSetVariant(resolvedId, in value, fallback);
                entry.LastStatus = ok ? "Written" : "Not written (fallback/exists?)";
            }
            catch (System.Exception ex)
            {
                entry.LastStatus = $"Error: {ex.Message}";
            }
        }

        int ResolveVarId(string trimmedKey, int overrideVarId = 0)
        {
            if (overrideVarId != 0)
                return overrideVarId;

            if (string.IsNullOrEmpty(trimmedKey))
                return 0;

            if (int.TryParse(trimmedKey, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed != 0)
                return parsed;

            if (VarIdResolver.TryResolve(trimmedKey, out var resolved) && resolved != 0)
                return resolved;

            return 0;
        }

        [Serializable]
        public sealed class BlackboardWriteEntry
        {
            [LabelText("Stable Key / Alias")]
            [Tooltip("Stable key, alias, or numeric ID to write to. VarId override takes precedence when non-zero.")]
            public string queryKey = string.Empty;

            [LabelText("Var Id Override")]
            [VarIdDropdown]
            public int queryVarId;

            [LabelText("Value")]
            [SerializeField]
            public LiteralSource value = new();

            [LabelText("Fallback")]
            public GlobalBlackboardSetFallback fallback = GlobalBlackboardSetFallback.CreateGameLogicRoot;

            [ShowInInspector, ReadOnly, LabelText("Status")]
            public string LastStatus { get; internal set; } = "(idle)";
        }

        int ResolveVarId(string trimmedKey)
        {
            if (queryVarId != 0)
                return queryVarId;

            if (string.IsNullOrEmpty(trimmedKey))
                return 0;

            if (int.TryParse(trimmedKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed != 0)
                return parsed;

            if (VarIdResolver.TryResolve(trimmedKey, out var resolved) && resolved != 0)
                return resolved;

            return 0;
        }

        [Serializable]
        public readonly struct BlackboardVarRow
        {
            [ShowInInspector, ReadOnly, LabelText("Id")]
            public readonly int VarId;

            [ShowInInspector, ReadOnly, LabelText("Key")]
            public readonly string KeyName;

            [ShowInInspector, ReadOnly, LabelText("Kind")]
            public readonly string KindName;

            [ShowInInspector, ReadOnly, LabelText("Version")]
            public readonly int Version;

            [ShowInInspector, ReadOnly, LabelText("Value")]
            public readonly string Value;

            public BlackboardVarRow(int varId, string keyName, string kindName, int version, string value)
            {
                VarId = varId;
                KeyName = keyName;
                KindName = kindName;
                Version = version;
                Value = value;
            }
        }
    }
}
