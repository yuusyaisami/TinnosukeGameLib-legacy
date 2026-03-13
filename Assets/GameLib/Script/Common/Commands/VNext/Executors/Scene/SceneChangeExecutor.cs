#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Flow;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class SceneChangeExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SceneChange;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SceneChangeCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SceneChangeCommandData is required.");

            if (ct.IsCancellationRequested)
                return;

            var scope = ctx.Scope;
            if (scope == null)
                return;

            if (!scope.TryResolveInAncestors<ISceneService>(out var sceneService) || sceneService == null)
                return;

            var targetSceneName = string.Empty;
            if (typed.TargetMode == SceneChangeTargetMode.SceneName)
            {
                targetSceneName = typed.SceneName.GetOrDefault(ctx, string.Empty);
                if (string.IsNullOrWhiteSpace(targetSceneName))
                    return;
            }

            switch (typed.Mode)
            {
                case SceneChangeMode.LoadSingle:
                    if (typed.TargetMode == SceneChangeTargetMode.SceneName)
                        await sceneService.LoadSingle(targetSceneName, typed.ForceReload);
                    else
                        await sceneService.LoadSingle(typed.Scene, typed.ForceReload);
                    break;
                case SceneChangeMode.LoadAdditive:
                    if (typed.TargetMode == SceneChangeTargetMode.SceneName)
                        await sceneService.LoadAdditive(targetSceneName);
                    else
                        await sceneService.LoadAdditive(typed.Scene);
                    break;
                case SceneChangeMode.Unload:
                    if (typed.TargetMode == SceneChangeTargetMode.SceneName)
                        await sceneService.Unload(targetSceneName);
                    else
                        await sceneService.Unload(typed.Scene);
                    break;
                default:
                    Debug.LogWarning($"[SceneChangeExecutor] Unsupported mode: {typed.Mode}");
                    break;
            }
        }
    }
}
