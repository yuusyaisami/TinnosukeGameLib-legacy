using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using VNext = Game.Commands.VNext;
using Game.Common;
using UnityEngine;
using System;
using VContainer;

namespace Game.Channel
{
    internal sealed class HookRunner
    {
        readonly IScopeNode _scope;
        readonly VNext.ICommandRunner _commandRunner;

        readonly object _gate = new();
        readonly Stack<VarStore> _varsPool = new(8);

        public HookRunner(IScopeNode scope, VNext.ICommandRunner commandRunner)
        {
            _scope = scope;
            _commandRunner = commandRunner;
        }

        public UniTask RunAsync(VNext.CommandListData commands, CancellationToken ct)
        {
            return RunCoreAsync(commands, ct, suppressCancelLog: false);
        }

        public UniTask RunAsync(VNext.CommandListData commands, CancellationToken ct, bool suppressCancelLog)
        {
            return RunCoreAsync(commands, ct, suppressCancelLog);
        }

        public void RunFireAndForget(VNext.CommandListData commands, CancellationToken ct)
        {
            RunCoreAsync(commands, ct, suppressCancelLog: false).Forget(ex =>
            {
                if (ex is OperationCanceledException)
                    return;
                Debug.LogException(ex);
            });
        }

        public void RunFireAndForget(VNext.CommandListData commands, CancellationToken ct, bool suppressCancelLog)
        {
            RunCoreAsync(commands, ct, suppressCancelLog).Forget(ex =>
            {
                if (ex is OperationCanceledException)
                    return;
                Debug.LogException(ex);
            });
        }

        async UniTask RunCoreAsync(VNext.CommandListData commands, CancellationToken ct, bool suppressCancelLog)
        {
            if (_commandRunner == null || commands == null || commands.Count == 0)
                return;

            var vars = RentVars();
            try
            {
                var options = VNext.CommandRunOptions.Default;
                if (suppressCancelLog)
                    options = options.WithSuppressCancelLog(true);

                var ctx = new VNext.CommandContext(_scope, vars, _commandRunner, _scope, options);
                await _commandRunner.ExecuteListAsync(commands, ctx, ct, options);
            }
            finally
            {
                ReturnVars(vars);
            }
        }

        VarStore RentVars()
        {
            lock (_gate)
            {
                if (_varsPool.Count > 0)
                {
                    var vars = _varsPool.Pop();
                    vars.Clear();
                    return vars;
                }
            }

            return new VarStore();
        }

        void ReturnVars(VarStore vars)
        {
            if (vars == null)
                return;

            vars.Clear();
            lock (_gate)
            {
                if (_varsPool.Count < 32)
                    _varsPool.Push(vars);
            }
        }
    }
}
