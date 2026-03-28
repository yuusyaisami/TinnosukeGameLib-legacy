#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;
using VContainer;
using VNext = Game.Commands.VNext;

namespace Game.UI
{
    public sealed class UISliderValueChangedCommandService : IScopeAcquireHandler, IScopeReleaseHandler
    {
        const string VarKeySliderRawValue = "SliderRawValue";
        const string VarKeySliderNormalizedValue = "SliderNormalizedValue";
        const string VarKeySliderIsEditing = "SliderIsEditing";

        readonly IScopeNode _owner;
        readonly IUISliderValueOptions _options;
        readonly VNext.CommandListData _onValueChangedCommands;

        IUISliderOutput? _output;
        VNext.ICommandRunner? _commandRunner;
        CancellationTokenSource? _commandCts;

        float _lastRawValue;
        float _lastNormalizedValue;
        bool _hasValue;

        public UISliderValueChangedCommandService(
            IScopeNode owner,
            IUISliderValueOptions options,
            VNext.CommandListData onValueChangedCommands)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _onValueChangedCommands = onValueChangedCommands ?? new VNext.CommandListData();
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return;

            if (_onValueChangedCommands.Count == 0)
                return;

            if (!resolver.TryResolve(out IUISliderOutput output) || output == null)
                return;

            if (!resolver.TryResolve(out VNext.ICommandRunner runner) || runner == null)
                return;

            _output = output;
            _commandRunner = runner;
            _output.OnUpdated += HandleOutputUpdated;

            ResetCommandState();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = isReset;
            if (_output != null)
            {
                _output.OnUpdated -= HandleOutputUpdated;
                _output = null;
            }

            _commandRunner = null;
            StopCommands();
            _hasValue = false;
        }

        void HandleOutputUpdated(UISliderOutputSnapshot snapshot)
        {
            if (_onValueChangedCommands.Count == 0)
                return;

            if (!ShouldExecute(snapshot))
                return;

            ExecuteValueChangedCommands(snapshot).Forget();
        }

        bool ShouldExecute(UISliderOutputSnapshot snapshot)
        {
            var epsilon = Mathf.Max(0f, _options.UpdateEpsilon);
            if (!_hasValue)
            {
                _hasValue = true;
                _lastRawValue = snapshot.RawValue;
                _lastNormalizedValue = snapshot.NormalizedValue;
                return false;
            }

            var rawChanged = Mathf.Abs(snapshot.RawValue - _lastRawValue) > epsilon;
            var normalizedChanged = Mathf.Abs(snapshot.NormalizedValue - _lastNormalizedValue) > epsilon;
            _lastRawValue = snapshot.RawValue;
            _lastNormalizedValue = snapshot.NormalizedValue;
            return rawChanged || normalizedChanged;
        }

        async UniTaskVoid ExecuteValueChangedCommands(UISliderOutputSnapshot snapshot)
        {
            if (_commandRunner == null || _commandCts == null)
                return;

            var variables = new VarStore();
            TrySetFloatVar(variables, VarKeySliderRawValue, snapshot.RawValue);
            TrySetFloatVar(variables, VarKeySliderNormalizedValue, snapshot.NormalizedValue);
            TrySetBoolVar(variables, VarKeySliderIsEditing, snapshot.IsEditing);

            var options = VNext.CommandRunOptions.Default;
            var ctx = new VNext.CommandContext(_owner, variables, _commandRunner, _owner, options);

            try
            {
                await _commandRunner.ExecuteListAsync(_onValueChangedCommands, ctx, _commandCts.Token, options);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        void ResetCommandState()
        {
            StopCommands();
            _commandCts = new CancellationTokenSource();
            _hasValue = false;
        }

        void StopCommands()
        {
            if (_commandCts == null)
                return;

            _commandCts.Cancel();
            _commandCts.Dispose();
            _commandCts = null;
        }

        static void TrySetFloatVar(IVarStore vars, string stableKey, float value)
        {
            if (vars == null || string.IsNullOrEmpty(stableKey))
                return;

            if (VarIdResolver.TryResolve(stableKey, out var varId) && varId != 0)
                vars.TrySetVariant(varId, DynamicVariant.FromFloat(value));
        }

        static void TrySetBoolVar(IVarStore vars, string stableKey, bool value)
        {
            if (vars == null || string.IsNullOrEmpty(stableKey))
                return;

            if (VarIdResolver.TryResolve(stableKey, out var varId) && varId != 0)
                vars.TrySetVariant(varId, DynamicVariant.FromBool(value));
        }
    }
}
