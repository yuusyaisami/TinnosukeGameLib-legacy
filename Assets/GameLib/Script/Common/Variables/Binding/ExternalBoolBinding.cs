#nullable enable
using Game.Commands.VNext;
using Game.Scalar;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Common
{
    public enum ExternalBoolBindingPriority
    {
        Scalar = 10,
        Blackboard = 20,
    }

    public interface IExternalBoolBindingOptions
    {
        bool UseScalarBinding { get; }
        ActorSource ScalarBindingSource { get; }
        ScalarKey ScalarKey { get; }
        bool UseBlackboardBinding { get; }
        ActorSource BlackboardBindingSource { get; }
        VarKeyRef BlackboardKey { get; }
        ExternalBoolBindingPriority BindingPriority { get; }
        bool WriteToBothBindings { get; }
    }

    [System.Serializable]
    public sealed class ExternalBoolBindingOptions : IExternalBoolBindingOptions
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
        ExternalBoolBindingPriority _bindingPriority = ExternalBoolBindingPriority.Scalar;

        [SerializeField]
        bool _writeToBothBindings = true;

        public bool UseScalarBinding => _useScalarBinding;
        public ActorSource ScalarBindingSource => _scalarBindingSource;
        public ScalarKey ScalarKey => _scalarKey;
        public bool UseBlackboardBinding => _useBlackboardBinding;
        public ActorSource BlackboardBindingSource => _blackboardBindingSource;
        public VarKeyRef BlackboardKey => _blackboardKey;
        public ExternalBoolBindingPriority BindingPriority => _bindingPriority;
        public bool WriteToBothBindings => _writeToBothBindings;
    }

    public sealed class ExternalBoolBindingRuntime
    {
        IExternalBoolBindingOptions? _options;

        IBaseScalarService? _scalarService;
        ActorSourceResolveCache _scalarBindingSourceCache;

        IVarStore? _blackboardVars;
        int _blackboardVarId;
        ActorSourceResolveCache _blackboardBindingSourceCache;

        public bool HasBinding =>
            _options != null &&
            ((_options.UseScalarBinding && _options.ScalarKey.Id != 0) ||
             (_options.UseBlackboardBinding && ResolveVarId(_options.BlackboardKey) != 0));

        public void Acquire(IScopeNode? scope, IExternalBoolBindingOptions? options)
        {
            Release();

            _options = options;
            if (scope == null || options == null)
                return;

            ResolveBindings(scope);
        }

        public void Release()
        {
            _options = null;
            _scalarService = null;
            _blackboardVars = null;
            _blackboardVarId = 0;
            _scalarBindingSourceCache = default;
            _blackboardBindingSourceCache = default;
        }

        public bool Write(bool value)
        {
            if (_options == null)
                return false;

            if (_options.WriteToBothBindings)
                return WriteScalar(value) | WriteBlackboard(value);

            return _options.BindingPriority == ExternalBoolBindingPriority.Scalar
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

        bool WriteScalar(bool value)
        {
            if (_options == null || _scalarService == null || !_options.UseScalarBinding || _options.ScalarKey.Id == 0)
                return false;

            _scalarService.SetGlobalBase(_options.ScalarKey, value ? 1f : 0f);
            return true;
        }

        bool WriteBlackboard(bool value)
        {
            if (_blackboardVars == null || _blackboardVarId == 0)
                return false;

            return _blackboardVars.TrySetVariant(_blackboardVarId, DynamicVariant.FromBool(value));
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
