#nullable enable

// Game.Common.BlackboardAuthoring + BlackboardMB adapter.
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using System.Collections.Generic;

namespace Game.Common
{
    public abstract class BlackboardAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public sealed class LocalBlackboardInitEntry
        {
            [LabelText("VarId")]
            [VarIdDropdown]
            public int VarId = 0;

            [LabelText("Value")]
            [HideLabel]
            [InlineProperty]
            public DynamicValue Value;
        }

        [System.Serializable]
        public sealed class LocalGridBlackboardInit
        {
            [LabelText("Grid Id")]
            [Tooltip("Inspector setting.")]
            [VarIdDropdown]
            [SerializeField] protected int gridId;

            [LabelText("Rows")]
            [ListDrawerSettings(ShowPaging = false, DraggableItems = false, ShowFoldout = true, DefaultExpandedState = true)]
            [SerializeField] protected List<RowInit> rows = new();

            public int GridId => gridId;
            public bool HasGridId => gridId != 0;
            public IReadOnlyList<RowInit> Rows => rows;

            public bool HasTable
            {
                get
                {
                    if (rows == null || rows.Count == 0)
                        return false;

                    for (int row = 0; row < rows.Count; row++)
                    {
                        var columns = rows[row]?.Columns;
                        if (columns == null)
                            continue;

                        if (HasGridId && columns.Count > 0)
                            return true;

                        for (int column = 0; column < columns.Count; column++)
                        {
                            var vars = columns[column]?.Vars;
                            if (vars?.Entries != null && vars.Entries.Count > 0)
                                return true;
                        }
                    }

                    return false;
                }
            }

            [System.Serializable]
            public sealed class RowInit
            {
                [LabelText("Columns")]
                [ListDrawerSettings(ShowPaging = false, DraggableItems = false, ShowFoldout = true, DefaultExpandedState = true)]
                [SerializeField] protected List<ColumnInit> columns = new();

                public IReadOnlyList<ColumnInit> Columns => columns;
            }

            [System.Serializable]
            public sealed class ColumnInit
            {
                [LabelText("Vars")]
                [InlineProperty]
                [SerializeField] protected VarStorePayload vars = new();

                public VarStorePayload Vars => vars;
            }
        }

        [FoldoutGroup("Debug")]
        [LabelText("Enable Debug View")]
        [SerializeField] protected bool enableDebugView = false;

        [FoldoutGroup("Debug")]
        [SerializeField, InlineProperty, HideLabel, ShowIf(nameof(enableDebugView))]
        protected BlackboardDebugView _debugView = new BlackboardDebugView();

        [FoldoutGroup("Auto Write")]
        [LabelText("Auto Write Transform Vars")]
        [SerializeField] protected bool autoWriteTransformVars = false;

        [BoxGroup("Local Blackboard Init")]
        [LabelText("Initialize Local Blackboard")]
        [SerializeField] protected bool initializeLocalBlackboard = false;

        [BoxGroup("Local Blackboard Init")]
        [LabelText("Reinitialize On Acquire")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(initializeLocalBlackboard))]
        [SerializeField] protected bool reinitializeLocalBlackboardOnAcquire = true;

        [BoxGroup("Local Blackboard Init")]
        [LabelText("Entries")]
        [ShowIf(nameof(initializeLocalBlackboard))]
        [ListDrawerSettings(ShowPaging = false, DraggableItems = false, ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField] protected LocalBlackboardInitEntry[] localBlackboardInitEntries = System.Array.Empty<LocalBlackboardInitEntry>();

        [BoxGroup("Local Grid Blackboard Init")]
        [LabelText("Initialize Local Grid Blackboard")]
        [SerializeField] protected bool initializeLocalGridBlackboard = false;

        [BoxGroup("Local Grid Blackboard Init")]
        [LabelText("Reinitialize On Acquire")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(initializeLocalGridBlackboard))]
        [SerializeField] protected bool reinitializeLocalGridBlackboardOnAcquire = true;

        [BoxGroup("Local Grid Blackboard Init")]
        [LabelText("Grid Definition")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(initializeLocalGridBlackboard))]
        [InlineProperty]
        [SerializeField] protected LocalGridBlackboardInit localGridBlackboardInit = new();

        internal void ValidateOrThrow()
        {
            if (initializeLocalBlackboard && localBlackboardInitEntries == null)
                throw new InvalidOperationException("Blackboard authoring requires local blackboard entries when initialization is enabled.");

            if (initializeLocalGridBlackboard && localGridBlackboardInit == null)
                throw new InvalidOperationException("Blackboard authoring requires a grid definition when grid initialization is enabled.");

            if (localBlackboardInitEntries != null)
            {
                var seenVarIds = new HashSet<int>();
                for (int i = 0; i < localBlackboardInitEntries.Length; i++)
                {
                    var entry = localBlackboardInitEntries[i];
                    if (entry == null)
                        throw new InvalidOperationException($"Blackboard local init entry at index {i} is null.");

                    if (entry.VarId == 0)
                        throw new InvalidOperationException($"Blackboard local init entry at index {i} requires a non-zero VarId.");

                    if (!seenVarIds.Add(entry.VarId))
                        throw new InvalidOperationException($"Blackboard local init entry VarId '{entry.VarId}' is duplicated.");
                }
            }

            if (!initializeLocalGridBlackboard || localGridBlackboardInit == null)
                return;

            var rows = localGridBlackboardInit.Rows;
            if (rows == null)
                throw new InvalidOperationException("Blackboard grid init requires rows when grid initialization is enabled.");

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                if (row == null)
                    throw new InvalidOperationException($"Blackboard grid init row at index {rowIndex} is null.");

                var columns = row.Columns;
                if (columns == null)
                    throw new InvalidOperationException($"Blackboard grid init row at index {rowIndex} has null columns.");

                for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                {
                    var column = columns[columnIndex];
                    if (column == null)
                        throw new InvalidOperationException($"Blackboard grid init column at row {rowIndex}, column {columnIndex} is null.");

                    if (column.Vars?.Entries == null)
                        throw new InvalidOperationException($"Blackboard grid init column at row {rowIndex}, column {columnIndex} is missing vars.");
                }
            }
        }

        internal virtual RuntimeAuthoringState CaptureRuntimeAuthoringState()
        {
            ValidateOrThrow();
            return new RuntimeAuthoringState(
                createLocalBlackboardPlan: null,
                acquireLocalBlackboardPlan: null,
                createLocalGridBlackboardPlan: null,
                acquireLocalGridBlackboardPlan: null,
                enableDebugView,
                enableDebugView ? _debugView : null,
                autoWriteTransformVars);
        }

        internal sealed class RuntimeAuthoringState
        {
            public static RuntimeAuthoringState Empty { get; } = new RuntimeAuthoringState(
                createLocalBlackboardPlan: null,
                acquireLocalBlackboardPlan: null,
                createLocalGridBlackboardPlan: null,
                acquireLocalGridBlackboardPlan: null,
                enableDebugView: false,
                debugView: null,
                autoWriteTransformVars: false);

            public RuntimeAuthoringState(
                BlackboardLocalValueInitPlan? createLocalBlackboardPlan,
                BlackboardLocalValueInitPlan? acquireLocalBlackboardPlan,
                BlackboardGridValueInitPlan? createLocalGridBlackboardPlan,
                BlackboardGridValueInitPlan? acquireLocalGridBlackboardPlan,
                bool enableDebugView,
                BlackboardDebugView? debugView,
                bool autoWriteTransformVars)
            {
                CreateLocalBlackboardPlan = createLocalBlackboardPlan;
                AcquireLocalBlackboardPlan = acquireLocalBlackboardPlan;
                CreateLocalGridBlackboardPlan = createLocalGridBlackboardPlan;
                AcquireLocalGridBlackboardPlan = acquireLocalGridBlackboardPlan;
                EnableDebugView = enableDebugView;
                DebugView = debugView;
                AutoWriteTransformVars = autoWriteTransformVars;
            }

            public BlackboardLocalValueInitPlan? CreateLocalBlackboardPlan { get; }

            public BlackboardLocalValueInitPlan? AcquireLocalBlackboardPlan { get; }

            public BlackboardGridValueInitPlan? CreateLocalGridBlackboardPlan { get; }

            public BlackboardGridValueInitPlan? AcquireLocalGridBlackboardPlan { get; }

            public bool EnableDebugView { get; }

            public BlackboardDebugView? DebugView { get; }

            public bool AutoWriteTransformVars { get; }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            try
            {
                ValidateOrThrow();
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message, this);
            }
        }
#endif
    }

    public class BlackboardMB : BlackboardAuthoring
    {
        BlackboardLocalValueInitPlan? _createLocalBlackboardPlan;
        BlackboardLocalValueInitPlan? _acquireLocalBlackboardPlan;
        BlackboardGridValueInitPlan? _createLocalGridBlackboardPlan;
        BlackboardGridValueInitPlan? _acquireLocalGridBlackboardPlan;
        bool _valueInitPlansBuilt;

        public void InstallBlackboardRuntime(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            BlackboardRuntimeInstaller.Install(builder, scope, this);
        }

        void EnsureValueInitPlansBuilt()
        {
            if (_valueInitPlansBuilt)
                return;

            _createLocalBlackboardPlan = BuildLocalBlackboardPlan(BlackboardValueInitPhase.Create, overwriteExisting: false);
            _acquireLocalBlackboardPlan = BuildLocalBlackboardPlan(BlackboardValueInitPhase.Acquire, overwriteExisting: reinitializeLocalBlackboardOnAcquire);
            _createLocalGridBlackboardPlan = BuildLocalGridBlackboardPlan(BlackboardValueInitPhase.Create, overwriteExisting: false);
            _acquireLocalGridBlackboardPlan = BuildLocalGridBlackboardPlan(BlackboardValueInitPhase.Acquire, overwriteExisting: reinitializeLocalGridBlackboardOnAcquire);
            _valueInitPlansBuilt = true;
        }

        internal override RuntimeAuthoringState CaptureRuntimeAuthoringState()
        {
            ValidateOrThrow();
            EnsureValueInitPlansBuilt();
            return new RuntimeAuthoringState(
                _createLocalBlackboardPlan,
                _acquireLocalBlackboardPlan,
                _createLocalGridBlackboardPlan,
                _acquireLocalGridBlackboardPlan,
                enableDebugView,
                enableDebugView ? _debugView : null,
                autoWriteTransformVars);
        }

        BlackboardLocalValueInitPlan? BuildLocalBlackboardPlan(BlackboardValueInitPhase phase, bool overwriteExisting)
        {
            if (!initializeLocalBlackboard)
                return null;

            if (localBlackboardInitEntries == null || localBlackboardInitEntries.Length == 0)
                return null;

            var plans = new List<BlackboardLocalValueInitEntryPlan>(localBlackboardInitEntries.Length);
            var seenVarIds = new HashSet<int>();

            for (int i = 0; i < localBlackboardInitEntries.Length; i++)
            {
                var entry = localBlackboardInitEntries[i];
                if (entry == null)
                    continue;

                var varId = entry.VarId;
                if (varId == 0)
                    continue;

                if (!seenVarIds.Add(varId))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[BlackboardMB] LocalBlackboardInit has duplicate varId={varId} at index={i}. Later entries may override earlier values.", this);
#endif
                }

                plans.Add(new BlackboardLocalValueInitEntryPlan(varId, entry.Value, i));
            }

            return plans.Count == 0
                ? null
                : new BlackboardLocalValueInitPlan(phase, overwriteExisting, plans.ToArray());
        }

        BlackboardGridValueInitPlan? BuildLocalGridBlackboardPlan(BlackboardValueInitPhase phase, bool overwriteExisting)
        {
            if (!initializeLocalGridBlackboard)
                return null;

            if (localGridBlackboardInit == null || !localGridBlackboardInit.HasTable)
                return null;

            var rows = localGridBlackboardInit.Rows;
            if (rows == null || rows.Count == 0)
                return null;

            int gridId = localGridBlackboardInit.GridId;
            bool hasGridId = gridId != 0;
            var cellPlans = new List<BlackboardGridValueInitCellPlan>();
            int order = 0;

            for (int row = 0; row < rows.Count; row++)
            {
                var rowInit = rows[row];
                if (rowInit == null)
                    continue;

                var columns = rowInit.Columns;
                if (columns == null || columns.Count == 0)
                    continue;

                for (int column = 0; column < columns.Count; column++)
                {
                    var columnInit = columns[column];
                    if (columnInit == null)
                        continue;

                    var payload = columnInit.Vars;
                    var entries = payload?.Entries;
                    VarStorePayload.Entry[]? copiedEntries = null;

                    if (entries != null && entries.Count > 0)
                    {
                        copiedEntries = new VarStorePayload.Entry[entries.Count];
                        var seenVarIds = new HashSet<int>();
                        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                        {
                            var entry = entries[entryIndex];
                            copiedEntries[entryIndex] = entry;

                            int varId = entry.VarId;
                            if (varId == 0)
                                continue;

                            if (!seenVarIds.Add(varId))
                            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                                Debug.LogWarning($"[BlackboardMB] LocalGridBlackboardInit has duplicate varId={varId} at row={row} column={column} index={entryIndex}. Later entries may override earlier values.", this);
#endif
                            }
                        }
                    }

                    if (!hasGridId && (copiedEntries == null || copiedEntries.Length == 0))
                        continue;

                    cellPlans.Add(new BlackboardGridValueInitCellPlan(row, column, order++, copiedEntries));
                }
            }

            return cellPlans.Count == 0
                ? null
                : new BlackboardGridValueInitPlan(phase, overwriteExisting, gridId, cellPlans.ToArray());
        }

        internal sealed class RuntimeBindings : IScopeAcquireHandler, IScopeReleaseHandler, IDisposable
        {
            readonly IScopeNode _owner;
            readonly string _scopeName;
            readonly DynamicEvaluationRuntime _valueInitRuntime = new();
            readonly BlackboardLocalValueInitPlan? _createLocalBlackboardPlan;
            readonly BlackboardLocalValueInitPlan? _acquireLocalBlackboardPlan;
            readonly BlackboardGridValueInitPlan? _createLocalGridBlackboardPlan;
            readonly BlackboardGridValueInitPlan? _acquireLocalGridBlackboardPlan;
            readonly bool _enableDebugView;
            readonly BlackboardDebugView? _debugView;
            bool _debugInitialized;

            public RuntimeBindings(IScopeNode owner, BlackboardAuthoring.RuntimeAuthoringState authoringState)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _ = authoringState ?? throw new ArgumentNullException(nameof(authoringState));

                _scopeName = owner is MonoBehaviour behaviour
                    ? behaviour.gameObject.name
                    : owner.Kind.ToString();

                _createLocalBlackboardPlan = authoringState.CreateLocalBlackboardPlan;
                _acquireLocalBlackboardPlan = authoringState.AcquireLocalBlackboardPlan;
                _createLocalGridBlackboardPlan = authoringState.CreateLocalGridBlackboardPlan;
                _acquireLocalGridBlackboardPlan = authoringState.AcquireLocalGridBlackboardPlan;
                _enableDebugView = authoringState.EnableDebugView;
                _debugView = authoringState.DebugView;
            }

            [Inject]
            void Construct(IBlackboardService blackboard, IGridBlackboardService gridBlackboard)
            {
                if (!TryApplyVerifiedLocalValueInit(blackboard, VerifiedValueInitPhase.Create))
                    BlackboardValueInitRuntime.ApplyLocalPlan(blackboard, _owner, _createLocalBlackboardPlan, _valueInitRuntime);

                ThrowIfVerifiedGridInitWouldReenterLegacy(VerifiedValueInitPhase.Create);
                BlackboardValueInitRuntime.ApplyGridPlan(blackboard, gridBlackboard, _owner, _createLocalGridBlackboardPlan, _valueInitRuntime);
                TryInitializeDebugView(blackboard, gridBlackboard);
            }

            public void OnAcquire(IScopeNode scope, bool isReset)
            {
                _ = isReset;

                IRuntimeResolver? resolver = scope?.Resolver;
                if (resolver == null)
                    return;

                if (!resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                    return;

                if (!TryApplyVerifiedLocalValueInit(blackboard, VerifiedValueInitPhase.Acquire))
                    BlackboardValueInitRuntime.ApplyLocalPlan(blackboard, _owner, _acquireLocalBlackboardPlan, _valueInitRuntime);

                IGridBlackboardService? gridBlackboard = null;
                if (resolver.TryResolve<IGridBlackboardService>(out var resolvedGridBlackboard) && resolvedGridBlackboard != null)
                {
                    gridBlackboard = resolvedGridBlackboard;
                    ThrowIfVerifiedGridInitWouldReenterLegacy(VerifiedValueInitPhase.Acquire);
                    BlackboardValueInitRuntime.ApplyGridPlan(blackboard, resolvedGridBlackboard, _owner, _acquireLocalGridBlackboardPlan, _valueInitRuntime);
                }

                TryInitializeDebugView(blackboard, gridBlackboard);
            }

            public void OnRelease(IScopeNode scope, bool isReset)
            {
                if (!isReset)
                    return;

                IRuntimeResolver? resolver = scope?.Resolver;
                if (resolver == null)
                    return;

                if (!resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                    return;

                blackboard.LocalVars.Clear();
            }

            public void Dispose()
            {
                if (!_debugInitialized || _debugView == null)
                    return;

                _debugView.Dispose();
                _debugInitialized = false;
            }

            void TryInitializeDebugView(IBlackboardService blackboard, IGridBlackboardService? gridBlackboard)
            {
                if (!_enableDebugView || _debugInitialized || _debugView == null || blackboard == null)
                    return;

                _debugView.Initialize(blackboard, gridBlackboard, _owner as MonoBehaviour);
                _debugInitialized = true;
            }

            bool TryApplyVerifiedLocalValueInit(IBlackboardService blackboard, global::Game.Common.VerifiedValueInitPhase phase)
            {
                if (!VerifiedValueRuntimeBridge.TryGetSession(out IVerifiedValueRuntimeSession? verifiedSession) || verifiedSession == null)
                    return false;

                global::Game.Common.VerifiedValueInitApplyResult result = verifiedSession.ApplyLocalBlackboardInit(_owner, blackboard, phase, _valueInitRuntime);
                if (result.IsRejected)
                {
                    throw new InvalidOperationException(
                        result.FailureReason ?? ("Wave D verified value init rejected phase " + phase + " for scope " + _scopeName + "."));
                }

                if (!result.IsApplied && GetLocalBlackboardPlan(phase) != null)
                {
                    throw new InvalidOperationException(
                        "Wave D verified value authority is active, but BlackboardMB local init authoring for phase " + phase + " on scope " + _scopeName + " has no verified ValueInitPlan. Legacy local blackboard fallback is forbidden.");
                }

                return result.IsApplied;
            }

            void ThrowIfVerifiedGridInitWouldReenterLegacy(global::Game.Common.VerifiedValueInitPhase phase)
            {
                if (!VerifiedValueRuntimeBridge.IsActive)
                    return;

                if (GetGridBlackboardPlan(phase) == null)
                    return;

                throw new InvalidOperationException(
                    "Wave D verified value authority is active, but BlackboardMB grid init authoring for phase " + phase + " on scope " + _scopeName + " still depends on legacy grid initialization. Legacy grid fallback is forbidden until verified grid authority exists.");
            }

            BlackboardLocalValueInitPlan? GetLocalBlackboardPlan(global::Game.Common.VerifiedValueInitPhase phase)
            {
                return phase switch
                {
                    global::Game.Common.VerifiedValueInitPhase.Create => _createLocalBlackboardPlan,
                    global::Game.Common.VerifiedValueInitPhase.Acquire => _acquireLocalBlackboardPlan,
                    global::Game.Common.VerifiedValueInitPhase.Reset => null,
                    _ => null,
                };
            }

            BlackboardGridValueInitPlan? GetGridBlackboardPlan(global::Game.Common.VerifiedValueInitPhase phase)
            {
                return phase switch
                {
                    global::Game.Common.VerifiedValueInitPhase.Create => _createLocalGridBlackboardPlan,
                    global::Game.Common.VerifiedValueInitPhase.Acquire => _acquireLocalGridBlackboardPlan,
                    global::Game.Common.VerifiedValueInitPhase.Reset => null,
                    _ => null,
                };
            }
        }

    }

    internal static class BlackboardRuntimeInstaller
    {
        public static void Install(IRuntimeContainerBuilder builder, IScopeNode scope, BlackboardAuthoring? authoring = null)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            BlackboardAuthoring.RuntimeAuthoringState authoringState = authoring?.CaptureRuntimeAuthoringState()
                ?? BlackboardAuthoring.RuntimeAuthoringState.Empty;

            LifetimeScopeKind kind = scope.Kind;
            IRuntimeRegistrationBuilder blackboard = builder.Register<IBlackboardService, BlackboardService>(RuntimeLifetime.Singleton).WithParameter(scope);
            switch (kind)
            {
                case LifetimeScopeKind.Project:
                    blackboard.As<IProjectBlackboardService>();
                    break;
                case LifetimeScopeKind.Platform:
                    blackboard.As<IPlatformBlackboardService>();
                    break;
                case LifetimeScopeKind.Global:
                    blackboard.As<IGlobalBlackboardService>();
                    break;
                case LifetimeScopeKind.Scene:
                    blackboard.As<ISceneBlackboardService>();
                    break;
                case LifetimeScopeKind.Field:
                    blackboard.As<IFieldBlackboardService>();
                    break;
                case LifetimeScopeKind.Entity:
                    blackboard.As<IEntityBlackboardService>();
                    break;
                case LifetimeScopeKind.UI:
                    blackboard.As<IUIBlackboardService>();
                    break;
                case LifetimeScopeKind.UIElement:
                    blackboard.As<IUIElementBlackboardService>();
                    break;
                case LifetimeScopeKind.Runtime:
                    blackboard.As<IRuntimeBlackboardService>();
                    break;
                default:
                    Debug.LogWarning($"Unhandled LifetimeScopeKind: {kind} in BlackboardRuntimeInstaller.");
                    break;
            }

            builder.Register<IGridBlackboardService, GridBlackboardService>(RuntimeLifetime.Singleton)
                .As<IGridBlackboardService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.Register<BlackboardMB.RuntimeBindings>(RuntimeLifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(authoringState)
                .AsSelf()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IDisposable>();

            if (authoringState.AutoWriteTransformVars)
            {
                if (scope is not MonoBehaviour scopeBehaviour)
                {
                    throw new InvalidOperationException("Blackboard runtime auto-write requires a MonoBehaviour-backed scope.");
                }

                builder.Register<TransformVarAutoWriterService>(RuntimeLifetime.Singleton)
                    .WithParameter(scopeBehaviour.transform)
                    .As<IScopeTickHandler>()
                    .As<IScopeAcquireHandler>()
                    .As<IScopeReleaseHandler>();
            }
        }
    }
}
