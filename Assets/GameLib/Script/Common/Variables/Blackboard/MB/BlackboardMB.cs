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
        [Tooltip("有効化（Acquire）時に Local Blackboard Init を再適用して既定値へ戻します")]
        [ShowIf(nameof(initializeLocalBlackboard))]
        [SerializeField] bool reinitializeLocalBlackboardOnAcquire = true;

        [BoxGroup("Local Blackboard Init")]
        [LabelText("Entries")]
        [ShowIf(nameof(initializeLocalBlackboard))]
        [ListDrawerSettings(ShowPaging = false, DraggableItems = false, ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField] LocalBlackboardInitEntry[] localBlackboardInitEntries = System.Array.Empty<LocalBlackboardInitEntry>();

        IScopeNode _owner;
        bool _debugInitialized;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            _owner = scope;
            builder.RegisterComponent(this)
                .AsSelf()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            LifetimeScopeKind kind = scope.Kind;
            RegistrationBuilder blackboard = builder.Register<IBlackboardService, BlackboardService>(Lifetime.Singleton).WithParameter(scope);
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

            // Save registration is handled by ScopeBindingRegistry. BlackboardMB only owns local blackboard initialization.

            if (autoWriteTransformVars)
            {
                builder.Register<TransformVarAutoWriterService>(Lifetime.Singleton)
                    .WithParameter(transform)
                    .As<ITickable>()
                    .As<IScopeAcquireHandler>()
                    .As<IScopeReleaseHandler>();
            }
        }

        [Inject]
        void Construct(IBlackboardService blackboard)
        {
            TryInitializeDebugView(blackboard);
            TryInitializeLocalBlackboard(blackboard, overwrite: false);
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
                TryInitializeDebugView(blackboard);
                TryInitializeLocalBlackboard(blackboard, overwrite: false);
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

            TryInitializeDebugView(blackboard);
            TryInitializeLocalBlackboard(blackboard, overwrite: reinitializeLocalBlackboardOnAcquire);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
        }

        void TryInitializeDebugView(IBlackboardService blackboard)
        {
            if (!enableDebugView || _debugInitialized || blackboard == null)
                return;

            _debugView.Initialize(blackboard, this);
            _debugInitialized = true;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[BlackboardMB] LocalBlackboardInit skipped (already exists). VarId={varId}, Overwrite={overwrite}", this);
#endif
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
