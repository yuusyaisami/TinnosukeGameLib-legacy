#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Profile;
using Game.Save;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    /// <summary>
    /// Save Profile - finds the nearest parent ProfileRegistry scope and saves it.
    /// </summary>
    public sealed class SaveProfileCommandExecutor : ICommandExecutor
    {
        static readonly List<ScopeKey> RegisteredScopeKeys = new(32);

        public int CommandId => CommandIds.SaveProfile;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SaveProfileCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SaveProfileCommandData is required.");

            if (!ctx.Resolver.TryResolve<ISaveManager>(out var saveManager) || saveManager == null)
            {
                Debug.LogWarning("[SaveProfileCommandExecutor] SaveManager not resolved.");
                return;
            }

            switch (typed.Mode)
            {
                case SaveCommandMode.Manual:
                    await ExecuteManualAsync(typed, ctx, saveManager, ct);
                    return;
                case SaveCommandMode.SaveAll:
                    ExecuteSaveAll(typed, saveManager);
                    return;
                default:
                    Debug.LogWarning($"[SaveProfileCommandExecutor] Unsupported save mode: {typed.Mode}");
                    return;
            }
        }

        static async UniTask ExecuteManualAsync(
            SaveProfileCommandData typed,
            CommandContext ctx,
            ISaveManager saveManager,
            CancellationToken ct)
        {
            var (targetScope, resolveError) = await ActorScopeResolver.ResolveAsync(typed.TargetScope, ctx, ct);
            if (targetScope == null)
            {
                Debug.LogWarning($"[SaveProfileCommandExecutor] Target scope resolve failed: {resolveError}");
                return;
            }

            var profileScope = FindProfileRegistryScope(targetScope);
            if (profileScope == null || profileScope.Identity == null)
            {
                Debug.LogWarning($"[SaveProfileCommandExecutor] No ProfileRegistry scope found from target {DescribeScope(targetScope)}.");
                return;
            }

            var scopeKey = new ScopeKey(profileScope.Identity.Kind, profileScope.Identity.Id);
            var saveCtx = new SaveContext(saveManager.ActiveProfileId, typed.LayerOverride, scopeKey);
            var result = saveManager.Save(in saveCtx, typed.UpdateBackup);

            switch (result.Status)
            {
                case SaveStatus.Success:
                    Debug.Log(
                        $"[SaveProfileCommandExecutor] Save manual success | Scope={profileScope.Identity.Kind}/{profileScope.Identity.Id}" +
                        $" | ProfileId={saveManager.ActiveProfileId} | Layer={typed.LayerOverride} | UpdateBackup={typed.UpdateBackup}");
                    return;
                case SaveStatus.NoData:
                    Debug.Log(
                        $"[SaveProfileCommandExecutor] Save manual skipped | Scope={profileScope.Identity.Kind}/{profileScope.Identity.Id}" +
                        $" | ProfileId={saveManager.ActiveProfileId} | Layer={typed.LayerOverride} | Reason=NoEntries");
                    return;
                default:
                    Debug.LogWarning(
                        $"[SaveProfileCommandExecutor] Save manual failed | Scope={profileScope.Identity.Kind}/{profileScope.Identity.Id}" +
                        $" | ProfileId={saveManager.ActiveProfileId} | Layer={typed.LayerOverride} | Error={result.Error} | Message={result.Message}");
                    return;
            }
        }

        static void ExecuteSaveAll(SaveProfileCommandData typed, ISaveManager saveManager)
        {
            lock (RegisteredScopeKeys)
            {
                RegisteredScopeKeys.Clear();
                saveManager.GetRegisteredScopeKeys(RegisteredScopeKeys);

                var successCount = 0;
                var skippedCount = 0;
                var failedCount = 0;

                for (int i = 0; i < RegisteredScopeKeys.Count; i++)
                {
                    var scopeKey = RegisteredScopeKeys[i];
                    var saveCtx = new SaveContext(saveManager.ActiveProfileId, typed.LayerOverride, scopeKey);
                    var result = saveManager.Save(in saveCtx, typed.UpdateBackup);

                    switch (result.Status)
                    {
                        case SaveStatus.Success:
                            successCount++;
                            break;
                        case SaveStatus.NoData:
                            skippedCount++;
                            break;
                        default:
                            failedCount++;
                            Debug.LogWarning(
                                $"[SaveProfileCommandExecutor] SaveAll failed | Scope={scopeKey} | ProfileId={saveManager.ActiveProfileId}" +
                                $" | Layer={typed.LayerOverride} | Error={result.Error} | Message={result.Message}");
                            break;
                    }
                }

                var summary =
                    $"[SaveProfileCommandExecutor] SaveAll completed | ProfileId={saveManager.ActiveProfileId}" +
                    $" | Layer={typed.LayerOverride} | UpdateBackup={typed.UpdateBackup}" +
                    $" | Saved={successCount} | Skipped={skippedCount} | Failed={failedCount}";

                if (failedCount > 0)
                    Debug.LogWarning(summary);
                else
                    Debug.Log(summary);
            }
        }

        static IScopeNode? FindProfileRegistryScope(IScopeNode? start)
        {
            for (var cur = start; cur != null; cur = cur.Parent)
            {
                if (cur.Resolver != null && cur.Resolver.TryResolve<IProfileRegistry>(out _))
                    return cur;
            }
            return null;
        }

        static string DescribeScope(IScopeNode scope)
        {
            if (scope == null)
                return "(null)";

            var id = scope.Identity?.Id;
            return string.IsNullOrEmpty(id) ? scope.Kind.ToString() : $"{scope.Kind}/{id}";
        }
    }

    /// <summary>
    /// Load Profile - finds the nearest parent ProfileRegistry scope and loads it.
    /// </summary>
    public sealed class LoadProfileCommandExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.LoadProfile;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not LoadProfileCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "LoadProfileCommandData is required.");

            if (!ctx.Resolver.TryResolve<ISaveManager>(out var saveManager) || saveManager == null)
            {
                Debug.LogWarning("[LoadProfileCommandExecutor] SaveManager not resolved.");
                return UniTask.CompletedTask;
            }

            var profileScope = FindProfileRegistryScope(ctx.Scope);
            if (profileScope == null || profileScope.Identity == null)
            {
                Debug.LogWarning("[LoadProfileCommandExecutor] No ProfileRegistry parent scope found.");
                return UniTask.CompletedTask;
            }

            var scopeKey = new ScopeKey(profileScope.Identity.Kind, profileScope.Identity.Id);
            var ctx_load = new SaveContext(saveManager.ActiveProfileId, typed.LayerOverride, scopeKey);
            var result = saveManager.Load(in ctx_load);

            if (result.Status == SaveStatus.Success)
            {
                Debug.Log($"[LoadProfileCommandExecutor] Loaded ProfileRegistry State" +
                    $" | Scope: {profileScope.Identity.Kind}/{profileScope.Identity.Id}" +
                    $" | ProfileId: {saveManager.ActiveProfileId}" +
                    $" | Layer: {typed.LayerOverride}");
                return UniTask.CompletedTask;
            }

            if (result.Status == SaveStatus.NoData)
            {
                Debug.Log($"[LoadProfileCommandExecutor] No SaveData found for {scopeKey} - using initial Profile values.");
                return UniTask.CompletedTask;
            }

            Debug.LogWarning($"[LoadProfileCommandExecutor] Load failed: {result.Error} - {result.Message}");
            return UniTask.CompletedTask;
        }

        static IScopeNode? FindProfileRegistryScope(IScopeNode? start)
        {
            for (var cur = start; cur != null; cur = cur.Parent)
            {
                if (cur.Resolver != null && cur.Resolver.TryResolve<IProfileRegistry>(out _))
                    return cur;
            }
            return null;
        }
    }

    /// <summary>
    /// Clear Profile - finds the nearest parent ProfileRegistry scope and clears the specified layer.
    /// </summary>
    public sealed class ClearProfileCommandExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ClearProfile;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ClearProfileCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ClearProfileCommandData is required.");

            if (!ctx.Resolver.TryResolve<ISaveManager>(out var saveManager) || saveManager == null)
            {
                Debug.LogWarning("[ClearProfileCommandExecutor] SaveManager not resolved.");
                return UniTask.CompletedTask;
            }

            var profileScope = FindProfileRegistryScope(ctx.Scope);
            if (profileScope == null || profileScope.Identity == null)
            {
                Debug.LogWarning("[ClearProfileCommandExecutor] No ProfileRegistry parent scope found.");
                return UniTask.CompletedTask;
            }

            var scopeKey = new ScopeKey(profileScope.Identity.Kind, profileScope.Identity.Id);
            var ctx_clear = new SaveContext(saveManager.ActiveProfileId, typed.LayerOverride, scopeKey);
            var result = saveManager.Clear(in ctx_clear);

            if (result.Status == SaveStatus.Success)
            {
                Debug.Log($"[ClearProfileCommandExecutor] Cleared ProfileRegistry Layer" +
                    $" | Scope: {profileScope.Identity.Kind}/{profileScope.Identity.Id}" +
                    $" | ProfileId: {saveManager.ActiveProfileId}" +
                    $" | Layer: {typed.LayerOverride}");
                return UniTask.CompletedTask;
            }

            Debug.LogWarning($"[ClearProfileCommandExecutor] Clear failed: {result.Error} - {result.Message}");
            return UniTask.CompletedTask;
        }

        static IScopeNode? FindProfileRegistryScope(IScopeNode? start)
        {
            for (var cur = start; cur != null; cur = cur.Parent)
            {
                if (cur.Resolver != null && cur.Resolver.TryResolve<IProfileRegistry>(out _))
                    return cur;
            }
            return null;
        }
    }

    public sealed class ProfileChangeCommandExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ProfileChange;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ProfileChangeCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ProfileChangeCommandData is required.");

            if (!ctx.Resolver.TryResolve<ISaveManager>(out var saveManager) || saveManager == null)
            {
                Debug.LogWarning("[ProfileChangeCommandExecutor] SaveManager not resolved.");
                return UniTask.CompletedTask;
            }

            if (typed.ProfileId < 0)
            {
                Debug.LogWarning($"[ProfileChangeCommandExecutor] Invalid ProfileId: {typed.ProfileId}");
                return UniTask.CompletedTask;
            }

            var result = saveManager.ChangeActiveProfile(typed.ProfileId);
            if (!result.IsSuccess)
            {
                Debug.LogWarning($"[ProfileChangeCommandExecutor] Profile change failed: {result.Error} - {result.Message}");
                return UniTask.CompletedTask;
            }

            Debug.Log($"[ProfileChangeCommandExecutor] Changed active ProfileId to {typed.ProfileId}");
            return UniTask.CompletedTask;
        }
    }

    public sealed class DeleteAllSaveDataCommandExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.DeleteAllSaveData;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not DeleteAllSaveDataCommandData)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "DeleteAllSaveDataCommandData is required.");

            if (!ctx.Resolver.TryResolve<ISaveManager>(out var saveManager) || saveManager == null)
            {
                Debug.LogWarning("[DeleteAllSaveDataCommandExecutor] SaveManager not resolved.");
                return UniTask.CompletedTask;
            }

            var result = saveManager.DeleteAllPersistedData();
            if (result.IsSuccess)
            {
                Debug.Log("[DeleteAllSaveDataCommandExecutor] Deleted all persisted save data.");
                return UniTask.CompletedTask;
            }

            Debug.LogWarning($"[DeleteAllSaveDataCommandExecutor] Delete failed: {result.Error} - {result.Message}");
            return UniTask.CompletedTask;
        }
    }
}

