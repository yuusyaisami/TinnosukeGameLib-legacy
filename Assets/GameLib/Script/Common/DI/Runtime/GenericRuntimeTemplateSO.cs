#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.DI
{
    [CreateAssetMenu(menuName = "Game/Runtime/Generic Runtime Template", fileName = "GenericRuntimeTemplate")]
    public sealed class GenericRuntimeTemplateSO : BaseRuntimeObjectTemplate
    {
        [Header("Commands")]
        [SerializeField]
        bool runOnAcquireCommands = false;

        [SerializeField, ShowIf(nameof(runOnAcquireCommands))]
        CommandListData onAcquireCommands = new();

        [SerializeField]
        bool runOnReleaseCommands = false;

        [SerializeField, ShowIf(nameof(runOnReleaseCommands))]
        CommandListData onReleaseCommands = new();

        public override void OnAcquire(IScopeNode scope, RuntimeIdentityData identity)
        {
            base.OnAcquire(scope, identity);

            if (!runOnAcquireCommands)
                return;

            TryRunCommands(scope, onAcquireCommands);
        }

        public override void OnRelease(IScopeNode scope)
        {
            base.OnRelease(scope);

            if (!runOnReleaseCommands)
                return;

            TryRunCommands(scope, onReleaseCommands);
        }

        static void TryRunCommands(IScopeNode scope, CommandListData list)
        {
            if (scope == null || list == null || list.Count == 0)
                return;

            var resolver = scope.Resolver;
            if (resolver == null)
                return;

            resolver.TryResolve(out ICommandRunner? runner);
            if (runner == null)
                return;

            var ctx = new CommandContext(scope, new VarStore(), runner);
            UniTask.Void(async () =>
            {
                try
                {
                    await runner.ExecuteListAsync(list, ctx, CancellationToken.None, ctx.Options);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            });
        }
    }
}
