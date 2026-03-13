#nullable enable
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Sirenix.OdinInspector;
using Game.Common;
using Game.Scalar;
using VNext = Game.Commands.VNext;

namespace Game.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Slider))]
    public sealed class UISliderMB : MonoBehaviour, IFeatureInstaller, IUISliderValueOptions, IUISliderInputOptions
    {
        [Header("Unity Slider (Visual)")]
        [SerializeField] Slider? _unitySlider;

        [Tooltip("独自入力で当たり判定に使うRect。透明Image + RaycastTarget=ON 推奨。未指定ならSliderのContainerを使う。")]
        [SerializeField] RectTransform? _hitTestRect;

        [Tooltip("Unity標準のEventSystem入力を無効化したいならON（独自入力のみで動かす）。")]
        [SerializeField] bool _disableUnityNativeInput = true;

        [Header("Value")]
        [SerializeField] float _minValue = 0f;
        [SerializeField] float _maxValue = 1f;
        [SerializeField] float _initialValue = 0f;
        [SerializeField] float _step = 0.1f;
        [SerializeField] UISliderStepMode _stepMode = UISliderStepMode.Raw;
        [SerializeField] float _updateEpsilon = 0.0001f;
        [SerializeField] bool _isEditable = true;
        [SerializeField] UISliderCancelBehavior _cancelBehavior = UISliderCancelBehavior.KeepValue;

        [Header("Input")]
        [SerializeField] UISliderInputMode _inputMode = UISliderInputMode.PointerCapture;
        [SerializeField] float _paddingStart = 0f;
        [SerializeField] float _paddingEnd = 0f;
        [SerializeField] Camera? _uiCamera;
        [SerializeField] float _navigateRepeatDelay = 0.25f;
        [SerializeField] float _navigateRepeatInterval = 0.08f;
        [SerializeField] float _scrollRepeatDelay = 0.1f;
        [SerializeField] float _scrollRepeatInterval = 0.05f;

        [Header("Binding")]
        [SerializeField] bool _useScalarBinding;
        [ShowIf(nameof(_useScalarBinding))]
        [SerializeField] ScalarKey _scalarKey;

        [SerializeField] bool _useBlackboardBinding;
        [ShowIf(nameof(_useBlackboardBinding))]
        [SerializeField] VarKeyRef _blackboardKey;

        [SerializeField] UISliderExternalBindingPriority _bindingPriority = UISliderExternalBindingPriority.Scalar;
        [SerializeField] bool _writeToBothBindings = true;

        [Header("Commands")]
        [Tooltip("スライダーの値が変化したときに実行するコマンド。")]
        [SerializeField]
        [VNext.CommandListFunctionName("UISlider.OnValueChanged")]
        VNext.CommandListData _onValueChangedCommands = new();

        [FoldoutGroup("Telemetry")]
        [SerializeField, ShowInInspector, ReadOnly]
        InspectorTelemetryState _inspectorTelemetry = new InspectorTelemetryState();

        [System.Serializable]
        public sealed class InspectorTelemetryState
        {
            [ShowInInspector, ReadOnly]
            public UISliderInteractionEventKind LastEvent = UISliderInteractionEventKind.None;

            [ShowInInspector, ReadOnly]
            public Vector2 LastPointerPosition = default;

            [ShowInInspector, ReadOnly]
            public float NormalizedValue = 0f;

            [ShowInInspector, ReadOnly]
            public float RawValue = 0f;

            [ShowInInspector, ReadOnly]
            public bool IsEditing = false;

            [ShowInInspector, ReadOnly]
            public bool IsPointerDown = false;

            [ShowInInspector, ReadOnly]
            public bool IsLongPressed = false;

            [ShowInInspector, ReadOnly]
            public double TimestampUtc = 0.0;

            public void UpdateFrom(UISliderTelemetrySnapshot s)
            {
                LastEvent = s.LastEvent;
                LastPointerPosition = s.LastPointerPosition;
                NormalizedValue = s.NormalizedValue;
                RawValue = s.RawValue;
                IsEditing = s.IsEditing;
                IsPointerDown = s.IsPointerDown;
                IsLongPressed = s.IsLongPressed;
                TimestampUtc = s.TimestampUtc;
            }
        }

        public UISliderStepMode StepMode => _stepMode;
        public float UpdateEpsilon => _updateEpsilon;
        public bool IsEditable => _isEditable;
        public UISliderCancelBehavior CancelBehavior => _cancelBehavior;

        public bool UseScalarBinding => _useScalarBinding;
        public ScalarKey ScalarKey => _scalarKey;
        public bool UseBlackboardBinding => _useBlackboardBinding;
        public VarKeyRef BlackboardKey => _blackboardKey;
        public UISliderExternalBindingPriority BindingPriority => _bindingPriority;
        public bool WriteToBothBindings => _writeToBothBindings;
        public VNext.CommandListData OnValueChangedCommands => _onValueChangedCommands;

        public float MinValue => _minValue;
        public float MaxValue => _maxValue;
        public float InitialValue => _initialValue;
        public float Step => _step;

        public RectTransform TrackRect
        {
            get
            {
                var s = UnitySlider;
                return UISliderUnityGeometry.ResolveContainerRect(s);
            }
        }

        public RectTransform? HitTestRect
        {
            get
            {
                if (_hitTestRect != null) return _hitTestRect;
                return TrackRect;
            }
        }

        public UISliderAxis Axis
        {
            get
            {
                var dir = UnitySlider.direction;
                return UISliderUnityGeometry.IsHorizontal(dir) ? UISliderAxis.Horizontal : UISliderAxis.Vertical;
            }
        }

        public UISliderDirection Direction
        {
            get
            {
                return UnitySlider.direction switch
                {
                    Slider.Direction.LeftToRight => UISliderDirection.LeftToRight,
                    Slider.Direction.RightToLeft => UISliderDirection.RightToLeft,
                    Slider.Direction.BottomToTop => UISliderDirection.BottomToTop,
                    Slider.Direction.TopToBottom => UISliderDirection.TopToBottom,
                    _ => UISliderDirection.LeftToRight
                };
            }
        }

        public float PaddingStart => _paddingStart;
        public float PaddingEnd => _paddingEnd;
        public UISliderInputMode InputMode => _inputMode;
        public Camera? UICamera => _uiCamera;
        public float NavigateRepeatDelay => _navigateRepeatDelay;
        public float NavigateRepeatInterval => _navigateRepeatInterval;
        public float ScrollRepeatDelay => _scrollRepeatDelay;
        public float ScrollRepeatInterval => _scrollRepeatInterval;

        Slider UnitySlider
        {
            get
            {
                ResolveSlider();
                return _unitySlider!;
            }
        }

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            ResolveSlider();
            ApplyUnitySliderConfig();

            builder.Register<UISliderService>(Lifetime.Singleton)
                .WithParameter(scope)
                .WithParameter<IUISliderValueOptions>(this)
                .WithParameter<Slider>(UnitySlider)
                .As<IUISliderController>()
                .As<IUISliderOutput>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.Register<UISliderTelemetry>(Lifetime.Singleton)
                .WithParameter(scope)
                .As<IUISliderTelemetry>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.Register<global::Game.UI.UISliderTelemetryInspectorBridge>(Lifetime.Singleton)
                .WithParameter(this)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.Register<UISliderInput>(Lifetime.Singleton)
                .WithParameter(scope)
                .WithParameter<IUISliderInputOptions>(this)
                .WithParameter<IUISliderValueOptions>(this)
                .As<IUIInputConsumer>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.Register<UISliderValueChangedCommandService>(Lifetime.Singleton)
                .WithParameter(scope)
                .WithParameter<IUISliderValueOptions>(this)
                .WithParameter(_onValueChangedCommands)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }

        void Reset()
        {
            ResolveSlider();
            ApplyUnitySliderConfig();
        }

        void OnValidate()
        {
            if (_step < 0f) _step = 0f;
            if (_updateEpsilon < 0f) _updateEpsilon = 0f;
            if (_paddingStart < 0f) _paddingStart = 0f;
            if (_paddingEnd < 0f) _paddingEnd = 0f;
            if (_navigateRepeatDelay < 0f) _navigateRepeatDelay = 0f;
            if (_navigateRepeatInterval < 0f) _navigateRepeatInterval = 0f;
            if (_scrollRepeatDelay < 0f) _scrollRepeatDelay = 0f;
            if (_scrollRepeatInterval < 0f) _scrollRepeatInterval = 0f;

            ResolveSlider();
            ApplyUnitySliderConfig();
        }

        void ResolveSlider()
        {
            if (_unitySlider != null) return;
            _unitySlider = GetComponent<Slider>();
        }

        void ApplyUnitySliderConfig()
        {
            if (_unitySlider == null) return;

            _unitySlider.minValue = Mathf.Min(_minValue, _maxValue);
            _unitySlider.maxValue = Mathf.Max(_minValue, _maxValue);

            if (_disableUnityNativeInput)
            {
                _unitySlider.transition = Selectable.Transition.None;
                _unitySlider.interactable = false;
            }
            else
            {
                _unitySlider.interactable = _isEditable;
            }
        }

        public void SetInspectorTelemetry(UISliderTelemetrySnapshot s)
        {
            _inspectorTelemetry.UpdateFrom(s);
        }
    }
}
