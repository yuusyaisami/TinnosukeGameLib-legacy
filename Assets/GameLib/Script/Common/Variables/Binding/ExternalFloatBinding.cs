#nullable enable
using System;
using Game.Commands.VNext;
using Game.Scalar;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Common
{
    public enum ExternalFloatBindingPriority
    {
        Scalar = 10,
        Blackboard = 20,
    }

    public interface IExternalFloatBindingOptions
    {
        bool UseScalarBinding { get; }
        ActorSource ScalarBindingSource { get; }
        ScalarKey ScalarKey { get; }
        bool UseBlackboardBinding { get; }
        ActorSource BlackboardBindingSource { get; }
        VarKeyRef BlackboardKey { get; }
        ExternalFloatBindingPriority BindingPriority { get; }
        bool WriteToBothBindings { get; }
    }

    [Serializable]
    public sealed class ExternalFloatBindingOptions : IExternalFloatBindingOptions
    {
        [SerializeField]
        bool _useScalarBinding;

        [ShowIf(nameof(_useScalarBinding))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Scalar Source\", _scalarBindingSource)")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        ActorSource _scalarBindingSource = new() { Kind = ActorSourceKind.Current };

        [ShowIf(nameof(_useScalarBinding))]
        [SerializeField]
        ScalarKey _scalarKey;

        [SerializeField]
        bool _useBlackboardBinding;

        [ShowIf(nameof(_useBlackboardBinding))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Blackboard Source\", _blackboardBindingSource)")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        ActorSource _blackboardBindingSource = new() { Kind = ActorSourceKind.Current };

        [ShowIf(nameof(_useBlackboardBinding))]
        [SerializeField]
        VarKeyRef _blackboardKey;

        [SerializeField]
        ExternalFloatBindingPriority _bindingPriority = ExternalFloatBindingPriority.Scalar;

        [SerializeField]
        bool _writeToBothBindings = true;

        public bool UseScalarBinding => _useScalarBinding;
        public ActorSource ScalarBindingSource => _scalarBindingSource;
        public ScalarKey ScalarKey => _scalarKey;
        public bool UseBlackboardBinding => _useBlackboardBinding;
        public ActorSource BlackboardBindingSource => _blackboardBindingSource;
        public VarKeyRef BlackboardKey => _blackboardKey;
        public ExternalFloatBindingPriority BindingPriority => _bindingPriority;
        public bool WriteToBothBindings => _writeToBothBindings;
    }

    public sealed class ExternalFloatBindingRuntime
    {
        IExternalFloatBindingOptions? _options;
        Action<float>? _onValueChanged;
        float _updateEpsilon;

        IBaseScalarService? _scalarService;
        IDisposable? _scalarSubscription;
        ActorSourceResolveCache _scalarBindingSourceCache;
        bool _suppressScalarEcho;
        float _lastScalarWrite;

        IVarStore? _blackboardVars;
        int _blackboardVarId;
        ActorSourceResolveCache _blackboardBindingSourceCache;
        bool _suppressVarEcho;
        int _lastVarWriteVersion;

        public bool HasBinding =>
            _options != null &&
            ((_options.UseScalarBinding && _options.ScalarKey.Id != 0) ||
             (_options.UseBlackboardBinding && ResolveVarId(_options.BlackboardKey) != 0));

        public void Acquire(
            IScopeNode? scope,
            IExternalFloatBindingOptions? options,
            Action<float>? onValueChanged,
            float updateEpsilon = 0.0001f)
        {
            Release();

            _options = options;
            _onValueChanged = onValueChanged;
            _updateEpsilon = Mathf.Max(0f, updateEpsilon);

            if (scope == null || options == null)
                return;

            ResolveBindings(scope);
            SubscribeExternal();
        }

        public void Release()
        {
            UnsubscribeExternal();
            _options = null;
            _onValueChanged = null;
            _updateEpsilon = 0f;
            _suppressScalarEcho = false;
            _lastScalarWrite = 0f;
            _suppressVarEcho = false;
            _lastVarWriteVersion = 0;
            _scalarBindingSourceCache = default;
            _blackboardBindingSourceCache = default;
        }

        public bool TryRead(out float value)
        {
            value = 0f;
            if (_options == null)
                return false;

            if (_options.BindingPriority == ExternalFloatBindingPriority.Scalar)
            {
                if (TryReadScalar(out value))
                    return true;

                if (TryReadBlackboard(out value))
                    return true;

                return false;
            }

            if (TryReadBlackboard(out value))
                return true;

            if (TryReadScalar(out value))
                return true;

            return false;
        }

        public bool Write(float value)
        {
            if (_options == null)
                return false;

            if (_options.WriteToBothBindings)
                return WriteScalar(value) | WriteBlackboard(value);

            return _options.BindingPriority == ExternalFloatBindingPriority.Scalar
                ? (WriteScalar(value) || WriteBlackboard(value))
                : (WriteBlackboard(value) || WriteScalar(value));
        }

        void ResolveBindings(IScopeNode scope)
        {
            _scalarService = null;
            _blackboardVars = null;
            _blackboardVarId = 0;

            if (_options == null || scope.Resolver == null)
                return;

            if (_options.UseScalarBinding && _options.ScalarKey.Id != 0 && TryResolveScalarService(scope, out var scalar))
                _scalarService = scalar;

            if (_options.UseBlackboardBinding)
            {
                _blackboardVarId = ResolveVarId(_options.BlackboardKey);
                if (_blackboardVarId != 0 && TryResolveBlackboardVars(scope, out var blackboardVars))
                    _blackboardVars = blackboardVars;
            }
        }

        void SubscribeExternal()
        {
            if (_options == null)
                return;

            if (_scalarService != null && _options.UseScalarBinding && _options.ScalarKey.Id != 0)
                _scalarSubscription = _scalarService.GlobalSubscribe(_options.ScalarKey, HandleScalarChanged);

            if (_blackboardVars != null && _blackboardVarId != 0)
                _blackboardVars.OnVarChanged += HandleVarChanged;
        }

        void UnsubscribeExternal()
        {
            _scalarSubscription?.Dispose();
            _scalarSubscription = null;

            if (_blackboardVars != null && _blackboardVarId != 0)
                _blackboardVars.OnVarChanged -= HandleVarChanged;

            _scalarService = null;
            _blackboardVars = null;
            _blackboardVarId = 0;
        }

        bool TryResolveScalarService(IScopeNode scope, out IBaseScalarService? scalarService)
        {
            scalarService = null;
            if (_options == null)
                return false;

            var targetScope = ActorSourceFastResolver.ResolveCached(
                scope,
                _options.ScalarBindingSource,
                ref _scalarBindingSourceCache);
            if (targetScope?.Resolver == null)
                return false;

            if (!targetScope.Resolver.TryResolve<IBaseScalarService>(out var resolved) || resolved == null)
                return false;

            scalarService = resolved;
            return true;
        }

        bool TryResolveBlackboardVars(IScopeNode scope, out IVarStore? blackboardVars)
        {
            blackboardVars = null;
            if (_options == null)
                return false;

            var targetScope = ActorSourceFastResolver.ResolveCached(
                scope,
                _options.BlackboardBindingSource,
                ref _blackboardBindingSourceCache);
            if (targetScope?.Resolver == null)
                return false;

            if (!targetScope.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                return false;

            blackboardVars = blackboard.LocalVars;
            return blackboardVars != null;
        }

        bool TryReadScalar(out float value)
        {
            value = 0f;
            if (_options == null || _scalarService == null || !_options.UseScalarBinding || _options.ScalarKey.Id == 0)
                return false;

            return _scalarService.GlobalTryGet(_options.ScalarKey, out value);
        }

        bool TryReadBlackboard(out float value)
        {
            value = 0f;
            if (_blackboardVars == null || _blackboardVarId == 0)
                return false;

            if (!_blackboardVars.TryGetVariant(_blackboardVarId, out var variant))
                return false;

            return variant.TryGet(out value);
        }

        bool WriteScalar(float value)
        {
            if (_options == null || _scalarService == null || !_options.UseScalarBinding || _options.ScalarKey.Id == 0)
                return false;

            _suppressScalarEcho = true;
            _lastScalarWrite = value;
            _scalarService.SetGlobalBase(_options.ScalarKey, value);
            return true;
        }

        bool WriteBlackboard(float value)
        {
            if (_blackboardVars == null || _blackboardVarId == 0)
                return false;

            var nextVersion = _blackboardVars.GlobalVersion + 1;
            if (!_blackboardVars.TrySetVariant(_blackboardVarId, DynamicVariant.FromFloat(value)))
            {
                _suppressVarEcho = false;
                return false;
            }

            _suppressVarEcho = true;
            _lastVarWriteVersion = nextVersion;
            return true;
        }

        void HandleScalarChanged(ScalarValueChangedArgs args)
        {
            if (_suppressScalarEcho)
            {
                if (Mathf.Abs(args.NewValue - _lastScalarWrite) <= _updateEpsilon)
                {
                    _suppressScalarEcho = false;
                    return;
                }

                _suppressScalarEcho = false;
            }

            _onValueChanged?.Invoke(args.NewValue);
        }

        void HandleVarChanged(int varId)
        {
            if (_blackboardVars == null || varId != _blackboardVarId)
                return;

            if (_suppressVarEcho)
            {
                if (_blackboardVars.GlobalVersion == _lastVarWriteVersion)
                {
                    _suppressVarEcho = false;
                    return;
                }

                _suppressVarEcho = false;
            }

            if (_blackboardVars.TryGetVariant(varId, out var variant) && variant.TryGet(out float value))
                _onValueChanged?.Invoke(value);
        }

        static int ResolveVarId(VarKeyRef key)
        {
            if (key.VarId != 0)
                return key.VarId;

            if (!string.IsNullOrEmpty(key.StableKey) && VarIdResolver.TryResolve(key.StableKey, out var varId))
                return varId;

            return 0;
        }
    }
}
