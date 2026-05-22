#nullable enable
using Game.Commands.VNext;
using Game.Common;
using Game.SelectRuntime;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class PointerTiltSwipeMB : MonoBehaviour, IScopeInstaller
    {
        [BoxGroup("General")]
        [LabelText("Enabled")]
        [SerializeField]
        bool _enabled = true;

        [BoxGroup("General")]
        [LabelText("Environment")]
        [SerializeField]
        PointerTiltEnvironmentMode _environmentMode = PointerTiltEnvironmentMode.Auto;

        [BoxGroup("General")]
        [LabelText("Target Transform")]
        [Tooltip("Empty uses owner scope identity transform.")]
        [SerializeField]
        Transform? _targetTransform;

        [BoxGroup("TransformAnimation")]
        [LabelText("Rotation Channel Tag")]
        [SerializeField]
        string _rotationChannelTag = "default";

        [BoxGroup("TransformAnimation")]
        [LabelText("Position Channel Tag")]
        [SerializeField]
        string _positionChannelTag = "default.pointerOffset";

        [BoxGroup("Tilt")]
        [LabelText("Enable Tilt")]
        [SerializeField]
        bool _enableTilt = true;

        [BoxGroup("Tilt")]
        [LabelText("Max Tilt Angle X")]
        [MinValue(0f)]
        [SerializeField]
        float _maxTiltAngleX = 6f;

        [BoxGroup("Tilt")]
        [LabelText("Max Tilt Angle Y")]
        [MinValue(0f)]
        [SerializeField]
        float _maxTiltAngleY = 6f;

        [BoxGroup("Tilt")]
        [LabelText("Local Range X")]
        [MinValue(0.001f)]
        [SerializeField]
        float _tiltLocalRangeX = 80f;

        [BoxGroup("Tilt")]
        [LabelText("Local Range Y")]
        [MinValue(0.001f)]
        [SerializeField]
        float _tiltLocalRangeY = 80f;

        [BoxGroup("Tilt")]
        [LabelText("Invert Tilt X")]
        [SerializeField]
        bool _invertTiltX = true;

        [BoxGroup("Tilt")]
        [LabelText("Invert Tilt Y")]
        [SerializeField]
        bool _invertTiltY = false;

        [BoxGroup("Tilt")]
        [LabelText("Tilt Apply Smooth")]
        [MinValue(0f)]
        [SerializeField]
        float _tiltApplySmoothTime = 0f;

        [BoxGroup("Tilt")]
        [LabelText("Tilt Return Duration")]
        [MinValue(0f)]
        [SerializeField]
        float _tiltReturnDuration = 0.12f;

        [BoxGroup("Swipe")]
        [LabelText("Enable Swipe")]
        [SerializeField]
        bool _enableSwipe = true;

        [BoxGroup("Swipe")]
        [LabelText("Threshold Local Distance")]
        [MinValue(0f)]
        [ShowIf(nameof(_enableSwipe))]
        [SerializeField]
        float _swipeThresholdLocalDistance = 24f;

        [BoxGroup("Swipe")]
        [LabelText("Offset Scale X")]
        [MinValue(0f)]
        [ShowIf(nameof(_enableSwipe))]
        [SerializeField]
        float _preThresholdOffsetScaleX = 0.25f;

        [BoxGroup("Swipe")]
        [LabelText("Offset Scale Y")]
        [MinValue(0f)]
        [ShowIf(nameof(_enableSwipe))]
        [SerializeField]
        float _preThresholdOffsetScaleY = 0.25f;

        [BoxGroup("Swipe")]
        [LabelText("Max Offset X")]
        [MinValue(0f)]
        [ShowIf(nameof(_enableSwipe))]
        [SerializeField]
        float _preThresholdOffsetMaxX = 12f;

        [BoxGroup("Swipe")]
        [LabelText("Max Offset Y")]
        [MinValue(0f)]
        [ShowIf(nameof(_enableSwipe))]
        [SerializeField]
        float _preThresholdOffsetMaxY = 12f;

        [BoxGroup("Swipe")]
        [LabelText("Position Return Duration")]
        [MinValue(0f)]
        [ShowIf(nameof(_enableSwipe))]
        [SerializeField]
        float _positionReturnDuration = 0.12f;

        [BoxGroup("World")]
        [LabelText("World Pointer Target")]
        [Tooltip("Optional explicit target. If empty, auto-resolve from SelectableRuntimeMB / children.")]
        [SerializeField]
        WorldPointerTargetMB? _worldPointerTarget;

        [BoxGroup("Commands")]
        [LabelText("On Swipe Candidate Started")]
        [SerializeField]
        CommandListData _onSwipeCandidateStartedCommands = new();

        [BoxGroup("Commands")]
        [LabelText("On Swipe Threshold Reached")]
        [SerializeField]
        CommandListData _onSwipeThresholdReachedCommands = new();

        public bool IsEnabled => _enabled;
        public PointerTiltEnvironmentMode EnvironmentMode => _environmentMode;
        public Transform? TargetTransform => _targetTransform;
        public string RotationChannelTag => string.IsNullOrWhiteSpace(_rotationChannelTag) ? "default" : _rotationChannelTag.Trim();
        public string PositionChannelTag => string.IsNullOrWhiteSpace(_positionChannelTag) ? "default.pointerOffset" : _positionChannelTag.Trim();

        public bool EnableTilt => _enableTilt;
        public float MaxTiltAngleX => Mathf.Max(0f, _maxTiltAngleX);
        public float MaxTiltAngleY => Mathf.Max(0f, _maxTiltAngleY);
        public float TiltLocalRangeX => Mathf.Max(0.001f, _tiltLocalRangeX);
        public float TiltLocalRangeY => Mathf.Max(0.001f, _tiltLocalRangeY);
        public bool InvertTiltX => _invertTiltX;
        public bool InvertTiltY => _invertTiltY;
        public float TiltApplySmoothTime => Mathf.Max(0f, _tiltApplySmoothTime);
        public float TiltReturnDuration => Mathf.Max(0f, _tiltReturnDuration);

        public bool EnableSwipe => _enableSwipe;
        public float SwipeThresholdLocalDistance => Mathf.Max(0f, _swipeThresholdLocalDistance);
        public float PreThresholdOffsetScaleX => Mathf.Max(0f, _preThresholdOffsetScaleX);
        public float PreThresholdOffsetScaleY => Mathf.Max(0f, _preThresholdOffsetScaleY);
        public float PreThresholdOffsetMaxX => Mathf.Max(0f, _preThresholdOffsetMaxX);
        public float PreThresholdOffsetMaxY => Mathf.Max(0f, _preThresholdOffsetMaxY);
        public float PositionReturnDuration => Mathf.Max(0f, _positionReturnDuration);

        public CommandListData OnSwipeCandidateStartedCommands => _onSwipeCandidateStartedCommands;
        public CommandListData OnSwipeThresholdReachedCommands => _onSwipeThresholdReachedCommands;

        public WorldPointerTargetMB? ResolveWorldPointerTarget()
        {
            if (_worldPointerTarget != null)
                return _worldPointerTarget;

            var selectable = GetComponent<SelectableRuntimeMB>();
            if (selectable == null)
                selectable = GetComponentInChildren<SelectableRuntimeMB>(true);

            if (selectable != null)
            {
                var resolved = selectable.ResolveTarget();
                if (resolved != null)
                    return resolved;
            }

            var direct = GetComponent<WorldPointerTargetMB>();
            if (direct != null)
                return direct;

            return GetComponentInChildren<WorldPointerTargetMB>(true);
        }

        void OnEnable()
        {
            BindDebugOwners();
        }

        void OnValidate()
        {
            BindDebugOwners();
        }

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<PointerTiltSwipeService>(RuntimeLifetime.Singleton)
                .AsSelf()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IScopeTickHandler>()
                .WithParameter(scope)
                .WithParameter(this);
        }

        void BindDebugOwners()
        {
            _onSwipeCandidateStartedCommands.BindDebugOwner(this, nameof(_onSwipeCandidateStartedCommands));
            _onSwipeThresholdReachedCommands.BindDebugOwner(this, nameof(_onSwipeThresholdReachedCommands));
        }
    }
}

