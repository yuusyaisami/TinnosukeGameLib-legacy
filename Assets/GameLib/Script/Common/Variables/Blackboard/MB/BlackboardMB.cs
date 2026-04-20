#nullable enable

// Game.Common.BlackboardInstallerMB.cs
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using System.Collections.Generic;

namespace Game.Common
{
    public class BlackboardMB : MonoBehaviour, IFeatureInstaller, IScopeAcquireHandler, IScopeReleaseHandler
    {
        [System.Serializable]
        sealed class LocalBlackboardInitEntry
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
            [SerializeField] int gridId;

            [LabelText("Rows")]
            [ListDrawerSettings(ShowPaging = false, DraggableItems = false, ShowFoldout = true, DefaultExpandedState = true)]
            [SerializeField] List<RowInit> rows = new();

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
                [SerializeField] List<ColumnInit> columns = new();

                public IReadOnlyList<ColumnInit> Columns => columns;
            }

            [System.Serializable]
            public sealed class ColumnInit
            {
                [LabelText("Vars")]
                [InlineProperty]
                [SerializeField] VarStorePayload vars = new();

                public VarStorePayload Vars => vars;
            }
        }

        [FoldoutGroup("Debug")]
        [LabelText("Enable Debug View")]
        public bool enableDebugView = false;

        [FoldoutGroup("Debug")]
        [SerializeField, InlineProperty, HideLabel, ShowIf(nameof(enableDebugView))]
        BlackboardDebugView _debugView = new BlackboardDebugView();

        [FoldoutGroup("Auto Write")]
        [LabelText("Auto Write Transform Vars")]
        [SerializeField] bool autoWriteTransformVars = false;

        [BoxGroup("Local Blackboard Init")]
        [LabelText("Initialize Local Blackboard")]
        [SerializeField] bool initializeLocalBlackboard = false;

        [BoxGroup("Local Blackboard Init")]
        [LabelText("Reinitialize On Acquire")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(initializeLocalBlackboard))]
        [SerializeField] bool reinitializeLocalBlackboardOnAcquire = true;

        [BoxGroup("Local Blackboard Init")]
        [LabelText("Entries")]
        [ShowIf(nameof(initializeLocalBlackboard))]
        [ListDrawerSettings(ShowPaging = false, DraggableItems = false, ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField] LocalBlackboardInitEntry[] localBlackboardInitEntries = System.Array.Empty<LocalBlackboardInitEntry>();

        [BoxGroup("Local Grid Blackboard Init")]
        [LabelText("Initialize Local Grid Blackboard")]
        [SerializeField] bool initializeLocalGridBlackboard = false;

        [BoxGroup("Local Grid Blackboard Init")]
        [LabelText("Reinitialize On Acquire")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(initializeLocalGridBlackboard))]
        [SerializeField] bool reinitializeLocalGridBlackboardOnAcquire = true;

        [BoxGroup("Local Grid Blackboard Init")]
        [LabelText("Grid Definition")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(initializeLocalGridBlackboard))]
        [InlineProperty]
        [SerializeField] LocalGridBlackboardInit localGridBlackboardInit = new();

        IScopeNode? _owner;
        bool _debugInitialized;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            _owner = scope;
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
                    Debug.LogWarning($"Unhandled LifetimeScopeKind: {kind} in BlackboardMB.");
                    break;
            }

            builder.Register<IGridBlackboardService, GridBlackboardService>(RuntimeLifetime.Singleton)
                .As<IGridBlackboardService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            // Register this component after the grid service so grid reset happens before local reinitialization.
            builder.RegisterComponent(this)
                .AsSelf()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            // Save registration is handled by ScopeBindingRegistry. BlackboardMB only owns local blackboard initialization.

            if (autoWriteTransformVars)
            {
                builder.Register<TransformVarAutoWriterService>(RuntimeLifetime.Singleton)
                    .WithParameter(transform)
                    .As<IScopeTickHandler>()
                    .As<IScopeAcquireHandler>()
                    .As<IScopeReleaseHandler>();
            }
        }

        [Inject]
        void Construct(IBlackboardService blackboard, IGridBlackboardService gridBlackboard)
        {
            TryInitializeLocalBlackboard(blackboard, overwrite: false);
            TryInitializeLocalGridBlackboard(blackboard, gridBlackboard, overwrite: false);
            TryInitializeDebugView(blackboard, gridBlackboard);
        }

        void OnDisable()
        {
            if (_debugInitialized)
            {
                _debugView.Dispose();
                _debugInitialized = false;
            }
        }

        void Start()
        {
            var resolver = _owner?.Resolver;
            if (resolver == null)
                return;

            if (resolver.TryResolve<IBlackboardService>(out var blackboard))
            {
                TryInitializeLocalBlackboard(blackboard, overwrite: false);

                IGridBlackboardService? gridBlackboard = null;
                if (resolver.TryResolve<IGridBlackboardService>(out var resolvedGridBlackboard))
                {
                    gridBlackboard = resolvedGridBlackboard;
                    TryInitializeLocalGridBlackboard(blackboard, resolvedGridBlackboard, overwrite: false);
                }

                TryInitializeDebugView(blackboard, gridBlackboard);
            }
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;

            var resolver = scope?.Resolver;
            if (resolver == null)
                return;

            if (!resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                return;

            TryInitializeLocalBlackboard(blackboard, overwrite: reinitializeLocalBlackboardOnAcquire);

            IGridBlackboardService? gridBlackboard = null;
            if (resolver.TryResolve<IGridBlackboardService>(out var resolvedGridBlackboard) && resolvedGridBlackboard != null)
            {
                gridBlackboard = resolvedGridBlackboard;
                TryInitializeLocalGridBlackboard(blackboard, resolvedGridBlackboard, overwrite: reinitializeLocalGridBlackboardOnAcquire);
            }

            TryInitializeDebugView(blackboard, gridBlackboard);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!isReset)
                return;

            var resolver = scope?.Resolver;
            if (resolver == null)
                return;

            if (!resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                return;

            blackboard.LocalVars.Clear();
        }

        void TryInitializeDebugView(IBlackboardService blackboard, IGridBlackboardService? gridBlackboard)
        {
            if (!enableDebugView || _debugInitialized || blackboard == null)
                return;

            _debugView.Initialize(blackboard, gridBlackboard, this);
            _debugInitialized = true;
        }

        void TryInitializeLocalGridBlackboard(IBlackboardService blackboard, IGridBlackboardService gridBlackboard, bool overwrite)
        {
            if (!initializeLocalGridBlackboard || blackboard == null || gridBlackboard == null)
                return;

            if (localGridBlackboardInit == null || !localGridBlackboardInit.HasTable)
                return;

            var vars = blackboard.LocalVars;
            if (vars == null)
                return;

            var ctx = new SimpleDynamicContext(vars, _owner);
            var rows = localGridBlackboardInit.Rows;
            if (rows == null || rows.Count == 0)
                return;

            var gridId = localGridBlackboardInit.GridId;
            var hasGridId = gridId != 0;

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
                    if (entries == null || entries.Count == 0)
                    {
                        if (hasGridId)
                        {
                            GridBlackboardWriteUtility.ApplyCellValues(gridBlackboard, row, column, null, ctx, overwrite, upsert: true, gridIdVarId: gridId);
                        }

                        continue;
                    }

                    var seenVarIds = new HashSet<int>();
                    for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                    {
                        var entry = entries[entryIndex];
                        var varId = entry.VarId;
                        if (varId == 0)
                            continue;

                        if (!seenVarIds.Add(varId))
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.LogWarning($"[BlackboardMB] LocalGridBlackboardInit has duplicate varId={varId} at row={row} column={column} index={entryIndex}. Later entries may override earlier values.", this);
#endif
                        }
                    }

                    GridBlackboardWriteUtility.ApplyCellValues(gridBlackboard, row, column, payload, ctx, overwrite, upsert: true, gridIdVarId: gridId);
                }
            }
        }

        void TryInitializeLocalBlackboard(IBlackboardService blackboard, bool overwrite)
        {
            if (!initializeLocalBlackboard || blackboard == null)
                return;

            if (localBlackboardInitEntries == null || localBlackboardInitEntries.Length == 0)
                return;

            var vars = blackboard.LocalVars;
            if (vars == null)
                return;

            var ctx = new SimpleDynamicContext(vars, _owner);
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

                if (!overwrite && vars.Contains(varId))
                {
                    //#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    //                    Debug.Log($"[BlackboardMB] LocalBlackboardInit skipped (already exists). VarId={varId}, Overwrite={overwrite}", this);
                    //#endif
                    continue;
                }

                if (overwrite && vars.Contains(varId))
                {
                    vars.TryUnset(varId);
                }

                var value = entry.Value.Evaluate(ctx);

                if (!TrySetLocalValue(vars, varId, value))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    var currentKind = vars.GetVarKind(varId);
                    Debug.LogWarning(
                        $"[BlackboardMB] LocalBlackboardInit failed. VarId={varId}, VariantKind={value.Kind}, Overwrite={overwrite}, CurrentKind={currentKind}",
                        this);
#endif
                }
            }
        }

        static bool TrySetLocalValue(IVarStore vars, int varId, in DynamicVariant value)
        {
            if (value.Kind == ValueKind.Null)
            {
                if (!vars.Contains(varId))
                    return true;
                return vars.TryUnset(varId);
            }

            if (value.Kind == ValueKind.ManagedRef)
                return value.AsManagedRef != null && vars.TrySetManagedRef(varId, value.AsManagedRef);

            return vars.TrySetVariant(varId, value);
        }

    }
}
