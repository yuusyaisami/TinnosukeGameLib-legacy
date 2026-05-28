#nullable enable

// Game.Common.BlackboardInstallerMB.cs
using System;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using Game.Kernel.Abstractions;
using Game.Kernel.IR;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using System.Collections.Generic;

namespace Game.Common
{
    public class BlackboardMB : MonoBehaviour, IScopeAcquireHandler, IScopeReleaseHandler, ISceneKernelValueInitHost
    {
        const string LocalBlackboardValueStoreRef = "local:blackboard";

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
        readonly DynamicEvaluationRuntime _valueInitRuntime = new();
        BlackboardLocalValueInitPlan? _createLocalBlackboardPlan;
        BlackboardLocalValueInitPlan? _acquireLocalBlackboardPlan;
        BlackboardGridValueInitPlan? _createLocalGridBlackboardPlan;
        BlackboardGridValueInitPlan? _acquireLocalGridBlackboardPlan;
        bool _debugInitialized;
        bool _valueInitPlansBuilt;
        bool _createValueInitApplied;
        bool _acquireValueInitApplied;
        VarStoreBackedValueStore? _valueStore;
        EntityRef _valueStoreEntityRef;
        bool _valueStoreBound;
        bool _valueInitHostBound;
        int _sceneKernelValueInitHostId;

        public bool AutoWriteTransformVars => autoWriteTransformVars;

        internal void AttachOwnerScope(IScopeNode scope)
        {
            _owner = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        [Inject]
        void Construct(IBlackboardService blackboard, IGridBlackboardService gridBlackboard)
        {
            EnsureValueInitPlansBuilt();
            TryBindSceneKernelValueStore(blackboard.LocalVars);
            TryBindSceneKernelValueInitHost();
            if (!TryApplyValueInitPhase(LifecyclePhase.Create, blackboard, gridBlackboard, out string failureReason))
                Debug.LogError("[BlackboardMB] Failed to apply create-phase value init: " + failureReason, this);

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

        void OnDestroy()
        {
            UnbindSceneKernelValueInitHost();
            UnbindSceneKernelValueStore();
        }

        void Start()
        {
            var resolver = _owner?.Resolver;
            if (resolver == null)
                return;

            if (!resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                return;

            IGridBlackboardService? gridBlackboard = null;
            if (resolver.TryResolve<IGridBlackboardService>(out var resolvedGridBlackboard) && resolvedGridBlackboard != null)
                gridBlackboard = resolvedGridBlackboard;

            TryInitializeDebugView(blackboard, gridBlackboard);
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;

            var resolver = scope?.Resolver;
            if (resolver == null)
                return;

            if (!resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                return;

            EnsureValueInitPlansBuilt();
            IGridBlackboardService? gridBlackboard = null;
            if (resolver.TryResolve<IGridBlackboardService>(out var resolvedGridBlackboard) && resolvedGridBlackboard != null)
                gridBlackboard = resolvedGridBlackboard;

            if (!TryApplyValueInitPhase(LifecyclePhase.Acquire, blackboard, gridBlackboard, out string failureReason))
                Debug.LogError("[BlackboardMB] Failed to apply acquire-phase value init: " + failureReason, this);

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
            _acquireValueInitApplied = false;
        }

        void TryInitializeDebugView(IBlackboardService blackboard, IGridBlackboardService? gridBlackboard)
        {
            if (!enableDebugView || _debugInitialized || blackboard == null)
                return;

            _debugView.Initialize(blackboard, gridBlackboard, this);
            _debugInitialized = true;
        }

        void TryBindSceneKernelValueStore(IVarStore localVars)
        {
            if (_valueStoreBound || localVars == null || _owner == null || _owner.Kind != LifetimeScopeKind.Entity)
                return;

            if (!TryResolveValueStoreEntityRef(out EntityRef entityRef))
                return;

            _valueStore ??= new VarStoreBackedValueStore(localVars, Game.Kernel.Value.ValueStoreScopeKind.Entity);
            if (!SceneKernelValueStoreBindingHub.TryRegister(gameObject.scene, entityRef, _valueStore, out string failureReason))
            {
                Debug.LogError("[BlackboardMB] Failed to bind VarStore-backed ValueStore to SceneKernel: " + failureReason, this);
                return;
            }

            _valueStoreEntityRef = entityRef;
            _valueStoreBound = true;
        }

        void TryBindSceneKernelValueInitHost()
        {
            if (_valueInitHostBound || _owner == null || _owner.Kind != LifetimeScopeKind.Entity)
                return;

            int runtimeInstanceId = GetSceneKernelValueInitHostId();
            if (runtimeInstanceId <= 0)
                return;

            if (!SceneKernelValueStoreBindingHub.TryRegisterValueInitHost(gameObject.scene, runtimeInstanceId, this, out string failureReason))
            {
                Debug.LogError("[BlackboardMB] Failed to bind value-init host to SceneKernel: " + failureReason, this);
                return;
            }

            _valueInitHostBound = true;
        }

        void UnbindSceneKernelValueStore()
        {
            if (!_valueStoreBound || _valueStore == null || _valueStoreEntityRef.IsEmpty)
                return;

            SceneKernelValueStoreBindingHub.TryUnregister(gameObject.scene, _valueStoreEntityRef, _valueStore);
            _valueStoreEntityRef = default;
            _valueStoreBound = false;
        }

        void UnbindSceneKernelValueInitHost()
        {
            if (!_valueInitHostBound)
                return;

            SceneKernelValueStoreBindingHub.TryUnregisterValueInitHost(gameObject.scene, GetSceneKernelValueInitHostId(), this);
            _valueInitHostBound = false;
        }

        int GetSceneKernelValueInitHostId()
        {
            if (_sceneKernelValueInitHostId > 0)
                return _sceneKernelValueInitHostId;

            _sceneKernelValueInitHostId = RuntimeHelpers.GetHashCode(this) & int.MaxValue;
            if (_sceneKernelValueInitHostId == 0)
                _sceneKernelValueInitHostId = 1;

            return _sceneKernelValueInitHostId;
        }

        public bool TryDispatchValueInit(string targetStoreRef, LifecyclePhase phase, out string failureReason)
        {
            if (!string.Equals(targetStoreRef, LocalBlackboardValueStoreRef, System.StringComparison.Ordinal))
            {
                failureReason = "BlackboardMB does not own value-store target '" + targetStoreRef + "'.";
                return false;
            }

            var resolver = _owner?.Resolver;
            if (resolver == null)
            {
                failureReason = "BlackboardMB value-init dispatch requires a live scope resolver.";
                return false;
            }

            if (!resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
            {
                failureReason = "BlackboardMB value-init dispatch requires IBlackboardService.";
                return false;
            }

            IGridBlackboardService? gridBlackboard = null;
            if (NeedsGridBlackboard(phase))
            {
                if (!resolver.TryResolve<IGridBlackboardService>(out var resolvedGridBlackboard) || resolvedGridBlackboard == null)
                {
                    failureReason = "BlackboardMB value-init dispatch requires IGridBlackboardService for grid-backed init entries.";
                    return false;
                }

                gridBlackboard = resolvedGridBlackboard;
            }

            if (!TryApplyValueInitPhase(phase, blackboard, gridBlackboard, out failureReason))
                return false;

            TryInitializeDebugView(blackboard, gridBlackboard);
            return true;
        }

        bool TryResolveValueStoreEntityRef(out EntityRef entityRef)
        {
            if (_owner?.Kind != LifetimeScopeKind.Entity)
            {
                entityRef = default;
                return false;
            }

            if (EntityRef.TryParse(_owner.Identity?.Id, out entityRef))
                return true;

            EntityIdentityMB? identityMb = GetComponent<EntityIdentityMB>();
            if (identityMb != null && identityMb.TryGetEntityRef(out entityRef))
                return true;

            entityRef = default;
            return false;
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

        bool TryApplyValueInitPhase(LifecyclePhase phase, IBlackboardService blackboard, IGridBlackboardService? gridBlackboard, out string failureReason)
        {
            EnsureValueInitPlansBuilt();

            switch (phase)
            {
                case LifecyclePhase.Create:
                    if (_createValueInitApplied)
                    {
                        failureReason = string.Empty;
                        return true;
                    }

                    BlackboardValueInitRuntime.ApplyLocalPlan(blackboard, _owner, _createLocalBlackboardPlan, _valueInitRuntime);
                    if (_createLocalGridBlackboardPlan != null)
                    {
                        if (gridBlackboard == null)
                        {
                            failureReason = "Create-phase value init requires IGridBlackboardService for grid-backed entries.";
                            return false;
                        }

                        BlackboardValueInitRuntime.ApplyGridPlan(blackboard, gridBlackboard, _owner, _createLocalGridBlackboardPlan, _valueInitRuntime);
                    }

                    _createValueInitApplied = true;
                    failureReason = string.Empty;
                    return true;

                case LifecyclePhase.Acquire:
                    if (_acquireValueInitApplied)
                    {
                        failureReason = string.Empty;
                        return true;
                    }

                    BlackboardValueInitRuntime.ApplyLocalPlan(blackboard, _owner, _acquireLocalBlackboardPlan, _valueInitRuntime);
                    if (_acquireLocalGridBlackboardPlan != null)
                    {
                        if (gridBlackboard == null)
                        {
                            failureReason = "Acquire-phase value init requires IGridBlackboardService for grid-backed entries.";
                            return false;
                        }

                        BlackboardValueInitRuntime.ApplyGridPlan(blackboard, gridBlackboard, _owner, _acquireLocalGridBlackboardPlan, _valueInitRuntime);
                    }

                    _acquireValueInitApplied = true;
                    failureReason = string.Empty;
                    return true;

                case LifecyclePhase.Reset:
                    failureReason = string.Empty;
                    return true;

                default:
                    failureReason = "BlackboardMB does not support lifecycle phase '" + phase + "' for value init dispatch.";
                    return false;
            }
        }

        bool NeedsGridBlackboard(LifecyclePhase phase)
        {
            EnsureValueInitPlansBuilt();

            return phase switch
            {
                LifecyclePhase.Create => _createLocalGridBlackboardPlan != null,
                LifecyclePhase.Acquire => _acquireLocalGridBlackboardPlan != null,
                _ => false,
            };
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

    }
}
