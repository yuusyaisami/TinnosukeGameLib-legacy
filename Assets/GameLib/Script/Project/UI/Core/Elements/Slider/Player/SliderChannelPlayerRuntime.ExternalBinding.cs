#nullable enable
using Game.Commands.VNext;
using Game.Common;
using Game.Scalar;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    internal sealed partial class SliderChannelPlayerRuntime
    {
        void ResolveBindings(IScopeNode scope)
        {
            _scalarService = null;
            _blackboardVars = null;
            _blackboardVarId = 0;

            if (_activeBindingEntry != null &&
                _activeBindingEntry.UseScalarBinding &&
                _activeBindingEntry.ScalarKey.Id != 0 &&
                TryResolveScalarService(scope, out var scalarService))
            {
                _scalarService = scalarService;
            }

            if (_activeBindingEntry != null && _activeBindingEntry.UseBlackboardBinding)
            {
                _blackboardVarId = SliderRuntimeHelpers.ResolveVarId(_activeBindingEntry.BlackboardKey);
                if (_blackboardVarId != 0 &&
                    TryResolveBlackboardVars(scope, out var blackboardVars))
                {
                    _blackboardVars = blackboardVars;
                }
            }
        }

        void ResolveCommandRunner(IScopeNode scope)
        {
            _commandRunner = null;
            if (!scope.TryResolveInAncestors(out _commandRunner))
                scope.Resolver?.TryResolve(out _commandRunner);
        }

        void SubscribeExternal()
        {
            if (_scalarService != null && _activeBindingEntry != null && _activeBindingEntry.UseScalarBinding && _activeBindingEntry.ScalarKey.Id != 0)
                _scalarSubscription = _scalarService.GlobalSubscribe(_activeBindingEntry.ScalarKey, HandleScalarChanged);

            if (_blackboardVars != null && _blackboardVarId != 0)
                _blackboardVars.OnVarChanged += HandleBlackboardVarChanged;
        }

        void UnsubscribeExternal()
        {
            _scalarSubscription?.Dispose();
            _scalarSubscription = null;

            if (_blackboardVars != null && _blackboardVarId != 0)
                _blackboardVars.OnVarChanged -= HandleBlackboardVarChanged;

            _scalarService = null;
            _blackboardVars = null;
            _blackboardVarId = 0;
        }

        bool TryResolveScalarService(IScopeNode scope, out IBaseScalarService? scalarService)
        {
            scalarService = null;
            if (_activeBindingEntry == null)
                return false;

            var targetScope = ActorSourceFastResolver.ResolveCached(
                scope,
                _activeBindingEntry.ScalarBindingSource,
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
            if (_activeBindingEntry == null)
                return false;

            var targetScope = ActorSourceFastResolver.ResolveCached(
                scope,
                _activeBindingEntry.BlackboardBindingSource,
                ref _blackboardBindingSourceCache);
            if (targetScope?.Resolver == null)
                return false;

            if (!targetScope.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                return false;

            blackboardVars = blackboard.LocalVars;
            return blackboardVars != null;
        }

        void HandleScalarChanged(ScalarValueChangedArgs args)
        {
            if (_suppressScalarEcho)
            {
                if (Mathf.Abs(args.NewValue - _lastScalarWrite) <= 0.0001f)
                {
                    _suppressScalarEcho = false;
                    return;
                }

                _suppressScalarEcho = false;
            }

            if (_isInteracting)
            {
                _pendingExternalResync = true;
                return;
            }

            RefreshTargetFromBinding();
        }

        void HandleBlackboardVarChanged(int varId)
        {
            if (_blackboardVarId == 0 || varId != _blackboardVarId || _blackboardVars == null)
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

            if (_isInteracting)
            {
                _pendingExternalResync = true;
                return;
            }

            RefreshTargetFromBinding();
        }

        void RefreshTargetFromBinding()
        {
            if (!_isVisible || _activeBindingEntry == null)
                return;

            ResolveRange();
            if (!TryReadBoundValue(out var rawValue))
                return;

            _suppressRuntimeCommands = false;
            SetTargetRawValue(rawValue, allowCommands: true);
        }

        void SyncFromExternal(bool allowCommands)
        {
            if (!TryReadBoundValue(out var rawValue))
                return;

            _suppressRuntimeCommands = !allowCommands;
            SetTargetRawValue(rawValue, allowCommands);
            if (!_transitionActive)
                _suppressRuntimeCommands = false;
        }

        void ApplyInteractionRevert()
        {
            _suppressRuntimeCommands = true;
            SetTargetRawValue(_interactionStartRawValue, allowCommands: false);
            WriteExternal(_interactionStartRawValue);
            if (!_transitionActive)
                _suppressRuntimeCommands = false;
        }

        float ResolveInitialRawValue()
        {
            if (_activeBindingEntry == null)
                return ResolveFloat(_playerPreset.InitialValue, _minValue);

            ResolveRange();
            if (TryReadBoundValue(out var boundValue))
                return boundValue;

            return ResolveFloat(_playerPreset.InitialValue, _minValue);
        }

        bool TryReadBoundValue(out float rawValue)
        {
            rawValue = 0f;
            if (_activeBindingEntry == null)
                return false;

            if (_activeBindingEntry.BindingPriority == SliderBindingPriority.Scalar)
            {
                if (TryReadScalar(out rawValue))
                    return true;
                if (TryReadBlackboard(out rawValue))
                    return true;
                return false;
            }

            if (TryReadBlackboard(out rawValue))
                return true;

            return TryReadScalar(out rawValue);
        }

        bool TryReadScalar(out float rawValue)
        {
            rawValue = 0f;
            if (_activeBindingEntry == null)
                return false;

            return _scalarService != null &&
                   _activeBindingEntry.UseScalarBinding &&
                   _activeBindingEntry.ScalarKey.Id != 0 &&
                   _scalarService.GlobalTryGet(_activeBindingEntry.ScalarKey, out rawValue);
        }

        bool TryReadBlackboard(out float rawValue)
        {
            rawValue = 0f;
            if (_blackboardVars == null || _blackboardVarId == 0)
                return false;

            return _blackboardVars.TryGetVariant(_blackboardVarId, out var variant) &&
                   variant.TryGet(out rawValue);
        }

        void WriteExternal(float rawValue)
        {
            if (_activeBindingEntry == null)
                return;

            if (_activeBindingEntry.BindingPriority == SliderBindingPriority.Scalar)
            {
                if (!WriteScalar(rawValue))
                    WriteBlackboard(rawValue);
                return;
            }

            if (!WriteBlackboard(rawValue))
                WriteScalar(rawValue);
        }

        bool WriteScalar(float rawValue)
        {
            if (_scalarService == null || _activeBindingEntry == null || !_activeBindingEntry.UseScalarBinding || _activeBindingEntry.ScalarKey.Id == 0)
                return false;

            _suppressScalarEcho = true;
            _lastScalarWrite = rawValue;
            _scalarService.SetGlobalBase(_activeBindingEntry.ScalarKey, rawValue);
            return true;
        }

        bool WriteBlackboard(float rawValue)
        {
            if (_blackboardVars == null || _blackboardVarId == 0)
                return false;

            var nextVersion = _blackboardVars.GlobalVersion + 1;
            if (_blackboardVars.TrySetVariant(_blackboardVarId, DynamicVariant.FromFloat(rawValue)))
            {
                _suppressVarEcho = true;
                _lastVarWriteVersion = nextVersion;
                return true;
            }

            _suppressVarEcho = false;
            return false;
        }
    }
}
