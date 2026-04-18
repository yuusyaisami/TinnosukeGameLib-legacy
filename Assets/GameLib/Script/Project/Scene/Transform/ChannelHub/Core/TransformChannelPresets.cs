#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Movement;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.TransformSystem
{
    [Serializable]
    public abstract class TransformChannelOutputPreset : IDynamicManagedRefValue
    {
        public abstract TransformChannelOutputTarget OutputTarget { get; }

        public abstract TransformChannelOutputPreset CreateRuntimeCopy();

        public abstract void ApplyToConfig(TransformControllerConfig config, Transform ownerTransform);

        protected static void ResetOutputTargets(TransformControllerConfig config)
        {
            config.TargetTransform = null;
            config.TargetRectTransform = null;
            config.TargetRigidbody2D = null;
            config.TargetCharacterController = null;
            config.Rigidbody2DVelocityMode = Rigidbody2DVelocityApplyMode.Override;
            config.Rigidbody2DAdditiveControl = Rigidbody2DAdditiveControlSettings.Default;
            config.Rigidbody2DGravityClamp = Rigidbody2DGravityClampSettings.Default;
        }
    }

    [Serializable]
    public sealed class TransformChannelTransformOutputPreset : TransformChannelOutputPreset
    {
        [BoxGroup("Output")]
        [LabelText("Target Transform")]
        [SerializeField]
        Transform? _targetTransform;

        public override TransformChannelOutputTarget OutputTarget => TransformChannelOutputTarget.Transform;

        public override TransformChannelOutputPreset CreateRuntimeCopy()
        {
            return new TransformChannelTransformOutputPreset
            {
                _targetTransform = _targetTransform,
            };
        }

        public override void ApplyToConfig(TransformControllerConfig config, Transform ownerTransform)
        {
            ResetOutputTargets(config);
            config.OutputTarget = TransformOutputTarget.Transform;
            config.TargetTransform = _targetTransform != null ? _targetTransform : ownerTransform;
        }
    }

    [Serializable]
    public sealed class TransformChannelBulkTransformOutputPreset : TransformChannelOutputPreset
    {
        [BoxGroup("Output")]
        [LabelText("Target Transform")]
        [SerializeField]
        Transform? _targetTransform;

        public override TransformChannelOutputTarget OutputTarget => TransformChannelOutputTarget.BulkTransform;

        public override TransformChannelOutputPreset CreateRuntimeCopy()
        {
            return new TransformChannelBulkTransformOutputPreset
            {
                _targetTransform = _targetTransform,
            };
        }

        public override void ApplyToConfig(TransformControllerConfig config, Transform ownerTransform)
        {
            ResetOutputTargets(config);
            config.OutputTarget = TransformOutputTarget.BulkTransform;
            config.TargetTransform = _targetTransform != null ? _targetTransform : ownerTransform;
        }
    }

    [Serializable]
    public sealed class TransformChannelRectTransformOutputPreset : TransformChannelOutputPreset
    {
        [BoxGroup("Output")]
        [LabelText("Target RectTransform")]
        [SerializeField]
        RectTransform? _targetRectTransform;

        public override TransformChannelOutputTarget OutputTarget => TransformChannelOutputTarget.RectTransform;

        public override TransformChannelOutputPreset CreateRuntimeCopy()
        {
            return new TransformChannelRectTransformOutputPreset
            {
                _targetRectTransform = _targetRectTransform,
            };
        }

        public override void ApplyToConfig(TransformControllerConfig config, Transform ownerTransform)
        {
            ResetOutputTargets(config);
            config.OutputTarget = TransformOutputTarget.RectTransform;
            var rectTarget = _targetRectTransform != null ? _targetRectTransform : ownerTransform as RectTransform;
            config.TargetRectTransform = rectTarget;
            config.TargetTransform = rectTarget != null ? rectTarget : ownerTransform;
        }
    }

    [Serializable]
    public sealed class TransformChannelRigidbody2DOutputPreset : TransformChannelOutputPreset
    {
        [BoxGroup("Output")]
        [LabelText("Target Rigidbody2D")]
        [SerializeField]
        Rigidbody2D? _targetRigidbody2D;

        [BoxGroup("Rigidbody2D")]
        [LabelText("Velocity Apply Mode")]
        [SerializeField]
        Rigidbody2DVelocityApplyMode _rigidbody2DVelocityMode = Rigidbody2DVelocityApplyMode.Override;

        [BoxGroup("Rigidbody2D")]
        [ShowIf(nameof(UseRigidbody2DOverlayControl))]
        [LabelText("Velocity Overlay Control")]
        [SerializeField, InlineProperty]
        Rigidbody2DAdditiveControlSettings _rigidbody2DAdditiveControl = Rigidbody2DAdditiveControlSettings.Default;

        [BoxGroup("Rigidbody2D")]
        [SerializeField, InlineProperty]
        Rigidbody2DGravityClampSettings _rigidbody2DGravityClamp = Rigidbody2DGravityClampSettings.Default;

        public override TransformChannelOutputTarget OutputTarget => TransformChannelOutputTarget.Rigidbody2D;

        bool UseRigidbody2DOverlayControl()
        {
            return _rigidbody2DVelocityMode == Rigidbody2DVelocityApplyMode.Overlay;
        }

        public override TransformChannelOutputPreset CreateRuntimeCopy()
        {
            return new TransformChannelRigidbody2DOutputPreset
            {
                _targetRigidbody2D = _targetRigidbody2D,
                _rigidbody2DVelocityMode = _rigidbody2DVelocityMode,
                _rigidbody2DAdditiveControl = _rigidbody2DAdditiveControl,
                _rigidbody2DGravityClamp = _rigidbody2DGravityClamp,
            };
        }

        public override void ApplyToConfig(TransformControllerConfig config, Transform ownerTransform)
        {
            ResetOutputTargets(config);
            config.OutputTarget = TransformOutputTarget.Rigidbody2D;
            var rbTarget = _targetRigidbody2D != null ? _targetRigidbody2D : ownerTransform.GetComponent<Rigidbody2D>();
            config.TargetRigidbody2D = rbTarget;
            config.TargetTransform = rbTarget != null ? rbTarget.transform : ownerTransform;
            config.Rigidbody2DVelocityMode = _rigidbody2DVelocityMode;
            config.Rigidbody2DAdditiveControl = _rigidbody2DAdditiveControl;
            config.Rigidbody2DGravityClamp = _rigidbody2DGravityClamp;
        }
    }

    [Serializable]
    public sealed class TransformChannelCharacterControllerOutputPreset : TransformChannelOutputPreset
    {
        [BoxGroup("Output")]
        [LabelText("Target CharacterController")]
        [SerializeField]
        CharacterController? _targetCharacterController;

        public override TransformChannelOutputTarget OutputTarget => TransformChannelOutputTarget.CharacterController;

        public override TransformChannelOutputPreset CreateRuntimeCopy()
        {
            return new TransformChannelCharacterControllerOutputPreset
            {
                _targetCharacterController = _targetCharacterController,
            };
        }

        public override void ApplyToConfig(TransformControllerConfig config, Transform ownerTransform)
        {
            ResetOutputTargets(config);
            config.OutputTarget = TransformOutputTarget.CharacterController;
            var ccTarget = _targetCharacterController != null ? _targetCharacterController : ownerTransform.GetComponent<CharacterController>();
            config.TargetCharacterController = ccTarget;
            config.TargetTransform = ccTarget != null ? ccTarget.transform : ownerTransform;
        }
    }

    [Serializable]
    public sealed class TransformChannelFeaturePreset : IDynamicManagedRefValue
    {
        [BoxGroup("Features")]
        [LabelText("Enable Movement")]
        [SerializeField]
        bool _enableMovement = true;

        [BoxGroup("Features")]
        [LabelText("Enable Rotation")]
        [SerializeField]
        bool _enableRotation;

        [BoxGroup("Features")]
        [LabelText("Enable Scale")]
        [SerializeField]
        bool _enableScale = true;

        public bool EnableMovement => _enableMovement;
        public bool EnableRotation => _enableRotation;
        public bool EnableScale => _enableScale;

        public TransformChannelFeaturePreset CreateRuntimeCopy()
        {
            return new TransformChannelFeaturePreset
            {
                _enableMovement = _enableMovement,
                _enableRotation = _enableRotation,
                _enableScale = _enableScale,
            };
        }
    }

    [Serializable]
    public abstract class TransformChannelEffectElement : IDynamicManagedRefValue
    {
        public abstract TransformChannelEffectElement CreateRuntimeCopy();
    }

    [Serializable]
    public sealed class TransformChannelGlobalEffectElement : TransformChannelEffectElement
    {
        [BoxGroup("Global")]
        [LabelText("Enabled")]
        [SerializeField]
        bool _enabled = true;

        [BoxGroup("Global")]
        [LabelText("Channel Tag Override")]
        [SerializeField]
        string _channelTagOverride = string.Empty;

        public override TransformChannelEffectElement CreateRuntimeCopy()
        {
            return new TransformChannelGlobalEffectElement
            {
                _enabled = _enabled,
                _channelTagOverride = _channelTagOverride,
            };
        }

        public bool TryBuildApplyRequest(out TransformManagerChannelApplyRequest request)
        {
            request = default;
            if (!_enabled)
                return false;

            if (string.IsNullOrWhiteSpace(_channelTagOverride))
            {
                request = TransformManagerChannelApplyRequest.All;
                return true;
            }

            request = TransformManagerChannelApplyRequest.ForChannelTag(_channelTagOverride);
            return true;
        }
    }

    [Serializable]
    public sealed class TransformChannelEffectPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Effect")]
        [LabelText("Elements")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeReference]
        List<TransformChannelEffectElement> _elements = new();

        public IReadOnlyList<TransformChannelEffectElement> Elements => _elements;

        public TransformChannelEffectPreset CreateRuntimeCopy()
        {
            var copy = new TransformChannelEffectPreset();
            for (var i = 0; i < _elements.Count; i++)
            {
                var element = _elements[i];
                if (element == null)
                    continue;

                copy._elements.Add(element.CreateRuntimeCopy());
            }

            return copy;
        }

        public void BuildGlobalApplyRequests(List<TransformManagerChannelApplyRequest> output)
        {
            if (output == null)
                return;

            output.Clear();
            for (var i = 0; i < _elements.Count; i++)
            {
                if (_elements[i] is not TransformChannelGlobalEffectElement globalElement)
                    continue;

                if (!globalElement.TryBuildApplyRequest(out var request))
                    continue;

                output.Add(request);
            }
        }
    }

    [CreateAssetMenu(menuName = "Game/Transform/Channel/Output Preset", fileName = "TransformChannelOutputPreset")]
    public sealed class TransformChannelOutputPresetSO : ScriptableObject, IDynamicValueAsset<TransformChannelOutputPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        TransformChannelOutputPreset? _preset = new TransformChannelTransformOutputPreset();

        public TransformChannelOutputPreset? Preset
        {
            get
            {
                EnsurePreset();
                return _preset;
            }
        }

        void OnEnable()
        {
            EnsurePreset();
        }

        void OnValidate()
        {
            EnsurePreset();
        }

        void EnsurePreset()
        {
            _preset ??= new TransformChannelTransformOutputPreset();
        }
    }

    [CreateAssetMenu(menuName = "Game/Transform/Channel/Feature Preset", fileName = "TransformChannelFeaturePreset")]
    public sealed class TransformChannelFeaturePresetSO : ScriptableObject, IDynamicValueAsset<TransformChannelFeaturePreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        TransformChannelFeaturePreset? _preset = new();

        public TransformChannelFeaturePreset? Preset
        {
            get
            {
                EnsurePreset();
                return _preset;
            }
        }

        void OnEnable()
        {
            EnsurePreset();
        }

        void OnValidate()
        {
            EnsurePreset();
        }

        void EnsurePreset()
        {
            _preset ??= new TransformChannelFeaturePreset();
        }
    }

    [CreateAssetMenu(menuName = "Game/Transform/Channel/Effect Preset", fileName = "TransformChannelEffectPreset")]
    public sealed class TransformChannelEffectPresetSO : ScriptableObject, IDynamicValueAsset<TransformChannelEffectPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        TransformChannelEffectPreset? _preset = new();

        public TransformChannelEffectPreset? Preset
        {
            get
            {
                EnsurePreset();
                return _preset;
            }
        }

        void OnEnable()
        {
            EnsurePreset();
        }

        void OnValidate()
        {
            EnsurePreset();
        }

        void EnsurePreset()
        {
            _preset ??= new TransformChannelEffectPreset();
        }
    }
}
