#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game
{
    public interface ICoordinatedBuildScope : IScopeNode
    {
        bool UseBuildCoordinator { get; }
        bool IsBuildCompleted { get; }
        UniTask WaitForParentForBuildAsync();
        void ExecuteBuildForCoordinator();
    }

    public static class ScopeBuildCoordinator
    {
        sealed class BuildHandle
        {
            readonly ICoordinatedBuildScope _scope;
            readonly UniTaskCompletionSource _tcs = new();
            bool _scheduled;

            public bool IsScheduled => _scheduled;

            public BuildHandle(ICoordinatedBuildScope scope)
            {
                _scope = scope;
            }

            public void Register(bool autoBuild)
            {
                if (autoBuild)
                {
                    ScheduleBuild();
                }
            }

            public void ScheduleBuild()
            {
                if (_scheduled)
                    return;

                if (!IsUnityAlive(_scope))
                {
                    _tcs.TrySetCanceled();
                    return;
                }

                if (_scope.Resolver != null || _scope.IsBuildCompleted)
                {
                    _tcs.TrySetResult();
                    return;
                }

                _scheduled = true;
                UniTask.Void(async () =>
                {
                    try
                    {
                        await _scope.WaitForParentForBuildAsync();

                        if (IsUnityAlive(_scope) && _scope.Resolver == null)
                        {
                            _scope.ExecuteBuildForCoordinator();
                        }

                        if (IsUnityAlive(_scope) && (_scope.Resolver != null || _scope.IsBuildCompleted))
                        {
                            _tcs.TrySetResult();
                        }
                    }
                    catch (Exception ex)
                    {
                        _tcs.TrySetException(ex);
                    }
                });
            }

            public void Complete()
            {
                _tcs.TrySetResult();
            }

            public void Cancel()
            {
                _tcs.TrySetCanceled();
            }

            public UniTask WaitAsync(CancellationToken token)
            {
                if (token.CanBeCanceled)
                {
                    return _tcs.Task.AttachExternalCancellation(token);
                }
                return _tcs.Task;
            }
        }

        static readonly Dictionary<ICoordinatedBuildScope, BuildHandle> Handles =
            new(ReferenceEqualityComparer<ICoordinatedBuildScope>.Instance);

        public static void Reset()
        {
            Handles.Clear();
        }

        public static void Register(ICoordinatedBuildScope scope, bool autoBuild)
        {
            if (!IsUnityAlive(scope))
                return;

            if (!Handles.TryGetValue(scope, out var handle))
            {
                handle = new BuildHandle(scope);
                Handles.Add(scope, handle);
            }

            handle.Register(autoBuild);
        }

        public static void Unregister(ICoordinatedBuildScope scope)
        {
            if (!IsUnityAlive(scope))
                return;

            if (Handles.Remove(scope, out var handle))
            {
                handle.Cancel();
            }
        }

        public static void NotifyBuilt(ICoordinatedBuildScope scope)
        {
            if (!IsUnityAlive(scope))
                return;

            if (Handles.TryGetValue(scope, out var handle))
            {
                handle.Complete();
            }

            var children = ScopeNodeHierarchy.GetChildrenOrEmpty(scope);
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is not ICoordinatedBuildScope child)
                    continue;
                if (!child.UseBuildCoordinator)
                    continue;
                if (child.Resolver != null || child.IsBuildCompleted)
                    continue;

                Register(child, autoBuild: false);
                Handles[child].ScheduleBuild();
            }
        }

        public static UniTask WaitUntilBuiltAsync(ICoordinatedBuildScope scope, CancellationToken token)
        {
            if (!IsUnityAlive(scope))
                return UniTask.CompletedTask;

            if (scope.Resolver != null || scope.IsBuildCompleted)
                return UniTask.CompletedTask;

            if (!Handles.TryGetValue(scope, out var handle))
            {
                handle = new BuildHandle(scope);
                Handles.Add(scope, handle);
                handle.ScheduleBuild();
            }
            else if (!handle.IsScheduled)
            {
                handle.ScheduleBuild();
            }

            return handle.WaitAsync(token);
        }

        static bool IsUnityAlive(object? o)
        {
            if (o is UnityEngine.Object uo)
                return uo;
            return o != null;
        }
    }
}
