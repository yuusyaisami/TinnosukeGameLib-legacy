#nullable enable
using System.Collections.Generic;
using Game;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.SelectRuntime
{
    [DisallowMultipleComponent]
    public sealed class UserMoveRotateRuntimeMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Target")]
        [LabelText("Pointer Target")]
        [SerializeField]
        WorldPointerTargetMB? _target;

        [BoxGroup("Move")]
        [LabelText("Move Source Mode")]
        [EnumToggleButtons]
        [SerializeField]
        UserMoveSourceMode _moveSourceMode = UserMoveSourceMode.Hybrid;

        [BoxGroup("Move")]
        [LabelText("Input Move Speed")]
        [MinValue(0f)]
        [SerializeField]
        float _inputMoveSpeed = 5f;

        [BoxGroup("Move")]
        [LabelText("Fallback Plane")]
        [SerializeField]
        AreaPlane _fallbackPlane = AreaPlane.XY;

        [BoxGroup("Rotate")]
        [LabelText("Rotate Degrees Per Scroll")]
        [SerializeField]
        float _rotateDegreesPerScroll = 15f;

        [BoxGroup("Rotate/Binding")]
        [LabelText("Rotate Binding")]
        [InlineProperty]
        [SerializeField]
        ExternalFloatBindingOptions _rotateBinding = new();

        [BoxGroup("Validation")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(_areaActorSource)")]
        [SerializeField]
        ActorSource _areaActorSource;

        [BoxGroup("Validation")]
        [LabelText("Area Tags")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField]
        List<string> _areaTags = new();

        [BoxGroup("Validation")]
        [LabelText("Min Distance To Other")]
        [MinValue(0f)]
        [SerializeField]
        float _minDistanceToOtherSelectable;

        [BoxGroup("Validation")]
        [LabelText("Block Layer Mask")]
        [SerializeField]
        LayerMask _blockLayerMask;

        [BoxGroup("Validation")]
        [LabelText("Validation Colliders")]
        [SerializeField]
        List<Collider> _validationColliders = new();

        [BoxGroup("Validation")]
        [LabelText("Use Child Colliders")]
        [SerializeField]
        bool _useChildColliders = true;

        [BoxGroup("Commands")]
        [LabelText("On Editor Enter")]
        [SerializeField]
        CommandListData _onEditorEnterCommands = new();

        [BoxGroup("Commands")]
        [LabelText("On Editor Exit")]
        [SerializeField]
        CommandListData _onEditorExitCommands = new();

        readonly List<Collider> _resolvedColliders = new();

        public WorldPointerTargetMB? Target => ResolveTarget();
        public UserMoveSourceMode MoveSourceMode => _moveSourceMode;
        public float InputMoveSpeed => Mathf.Max(0f, _inputMoveSpeed);
        public AreaPlane FallbackPlane => _fallbackPlane;
        public float RotateDegreesPerScroll => _rotateDegreesPerScroll;
        public IExternalFloatBindingOptions RotateBinding => _rotateBinding;
        public ActorSource AreaActorSource => _areaActorSource;
        public IReadOnlyList<string> AreaTags => _areaTags;
        public float MinDistanceToOtherSelectable => Mathf.Max(0f, _minDistanceToOtherSelectable);
        public LayerMask BlockLayerMask => _blockLayerMask;
        public CommandListData OnEditorEnterCommands => _onEditorEnterCommands;
        public CommandListData OnEditorExitCommands => _onEditorExitCommands;

        public WorldPointerTargetMB? ResolveTarget()
        {
            if (_target != null)
                return _target;

            _target = GetComponent<WorldPointerTargetMB>();
            if (_target != null)
                return _target;

            _target = GetComponentInChildren<WorldPointerTargetMB>(true);
            return _target;
        }

        public IReadOnlyList<Collider> ResolveValidationColliders()
        {
            _resolvedColliders.Clear();
            if (_validationColliders != null)
            {
                for (int i = 0; i < _validationColliders.Count; i++)
                {
                    var collider = _validationColliders[i];
                    if (collider != null && !_resolvedColliders.Contains(collider))
                        _resolvedColliders.Add(collider);
                }
            }

            if (_resolvedColliders.Count == 0 && _useChildColliders)
            {
                var childColliders = GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < childColliders.Length; i++)
                {
                    var collider = childColliders[i];
                    if (collider != null && !_resolvedColliders.Contains(collider))
                        _resolvedColliders.Add(collider);
                }
            }

            return _resolvedColliders;
        }

        public bool TryResolveActorScope(out IScopeNode? scope)
        {
            return ScopeFeatureInstallerUtility.TryGetNearestScopeNode(this, includeInactive: true, out scope);
        }

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<UserMoveRotateRuntimeBridgeService>(Lifetime.Singleton)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>()
                .WithParameter(this);
        }
    }
}
