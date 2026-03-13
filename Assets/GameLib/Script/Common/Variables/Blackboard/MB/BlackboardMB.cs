// Game.Common.BlackboardInstallerMB.cs
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Game.Vars.Generated;
using System.Collections.Generic;

namespace Game.Common
{
    public class BlackboardMB : MonoBehaviour, IFeatureInstaller, IScopeAcquireHandler, IScopeReleaseHandler
    {
        enum LocalVarKey
        {
            A = 0,
            B = 1,
            C = 2,
            D = 3,
            E = 4,
            F = 5,
            G = 6,
        }

        enum LocalVarValueType
        {
            Bool = 0,
            Int = 1,
            Float = 2,
            String = 3,
        }

        [System.Serializable]
        sealed class LocalVarInitEntry
        {
            [LabelText("Key")] public LocalVarKey Key = LocalVarKey.A;
            [LabelText("Value Type")] public LocalVarValueType ValueType = LocalVarValueType.Bool;

            [LabelText("Bool Value")]
            [ShowIf("@ValueType == LocalVarValueType.Bool")]
            public bool BoolValue = false;

            [LabelText("Int Value")]
            [ShowIf("@ValueType == LocalVarValueType.Int")]
            public int IntValue = 0;

            [LabelText("Float Value")]
            [ShowIf("@ValueType == LocalVarValueType.Float")]
            public float FloatValue = 0f;

            [LabelText("String Value")]
            [ShowIf("@ValueType == LocalVarValueType.String")]
            public string StringValue = string.Empty;
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

        [BoxGroup("Local Var Init")]
        [LabelText("Initialize Base LocalVars")]
        [SerializeField] bool initializeBaseLocalVars = false;

        [BoxGroup("Local Var Init")]
        [LabelText("Reinitialize On Acquire")]
        [Tooltip("有効化（Acquire）時に LocalVarInit を再適用して既定値へ戻します")]
        [ShowIf(nameof(initializeBaseLocalVars))]
        [SerializeField] bool reinitializeBaseLocalVarsOnAcquire = true;

        [BoxGroup("Local Var Init")]
        [LabelText("Entries")]
        [ShowIf(nameof(initializeBaseLocalVars))]
        [ListDrawerSettings(ShowPaging = false, DraggableItems = false, ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField] LocalVarInitEntry[] baseLocalVarEntries = System.Array.Empty<LocalVarInitEntry>();

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

            // Save/initialization pipeline is handled by ProfileRegistry only.

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
            TryInitializeBaseLocalVars(blackboard, overwrite: false);
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
                TryInitializeBaseLocalVars(blackboard, overwrite: false);
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
            TryInitializeBaseLocalVars(blackboard, overwrite: reinitializeBaseLocalVarsOnAcquire);
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

        void TryInitializeBaseLocalVars(IBlackboardService blackboard, bool overwrite)
        {
            if (!initializeBaseLocalVars || blackboard == null)
                return;

            if (baseLocalVarEntries == null || baseLocalVarEntries.Length == 0)
                return;

            var vars = blackboard.LocalVars;
            if (vars == null)
                return;

            var seenKeys = new HashSet<LocalVarKey>();

            for (int i = 0; i < baseLocalVarEntries.Length; i++)
            {
                var entry = baseLocalVarEntries[i];
                if (entry == null)
                    continue;

                if (!seenKeys.Add(entry.Key))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[BlackboardMB] LocalVarInit has duplicate key '{entry.Key}' at index={i}. Later entries may override earlier values.", this);
#endif
                }

                var varId = ResolveLocalVarId(entry.Key);
                if (varId == 0)
                    continue;

                if (!overwrite && vars.Contains(varId))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[BlackboardMB] LocalVarInit skipped (already exists). Key={entry.Key}, VarId={varId}, Overwrite={overwrite}", this);
#endif
                    continue;
                }

                if (overwrite && vars.Contains(varId))
                {
                    vars.TryUnset(varId);
                }

                DynamicVariant value = entry.ValueType switch
                {
                    LocalVarValueType.Int => DynamicVariant.FromInt(entry.IntValue),
                    LocalVarValueType.Float => DynamicVariant.FromFloat(entry.FloatValue),
                    LocalVarValueType.String => DynamicVariant.FromString(entry.StringValue),
                    _ => DynamicVariant.FromBool(entry.BoolValue),
                };

                if (!vars.TrySetVariant(varId, value))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    var currentKind = vars.GetVarKind(varId);
                    Debug.LogWarning(
                        $"[BlackboardMB] LocalVarInit failed. Key={entry.Key}, VarId={varId}, ValueType={entry.ValueType}, VariantKind={value.Kind}, Overwrite={overwrite}, CurrentKind={currentKind}",
                        this);
#endif
                }
            }
        }

        static int ResolveLocalVarId(LocalVarKey key)
        {
            return key switch
            {
                LocalVarKey.A => VarIds.GameLib.Base.LocalVar.A,
                LocalVarKey.B => VarIds.GameLib.Base.LocalVar.B,
                LocalVarKey.C => VarIds.GameLib.Base.LocalVar.C,
                LocalVarKey.D => VarIds.GameLib.Base.LocalVar.D,
                LocalVarKey.E => VarIds.GameLib.Base.LocalVar.E,
                LocalVarKey.F => VarIds.GameLib.Base.LocalVar.F,
                LocalVarKey.G => VarIds.GameLib.Base.LocalVar.G,
                _ => 0,
            };
        }
    }
}
