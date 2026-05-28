using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands;
using Game.Commands.VNext;
using Game.DI;
using Game.Project.Scene.Runtime;
using Game.Spawn;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class RuntimeLifetimeScopeSpawnerServiceTests
    {
        [Test]
        public void AllDelete_DelegatesToPoolReleaseMatching()
        {
            GameObject rootObject = new GameObject("RuntimeRoot");
            RuntimeLifetimeScope detachedScope = CreateScope("Detached", id: "Detached", category: "Enemy", active: true);

            try
            {
                FakeRuntimeLifetimeScopePool pool = new FakeRuntimeLifetimeScopePool();
                pool.Add(detachedScope);
                SceneSpawnerRegistry registry = new SceneSpawnerRegistry();
                RuntimeLifetimeScopeSpawnerService service = new RuntimeLifetimeScopeSpawnerService(pool, rootObject.transform, string.Empty, registry);

                int deleted = service.AllDelete(RuntimeLifetimeScopeDeleteFilter.Default);

                Assert.That(deleted, Is.EqualTo(1));
                Assert.That(pool.ReleaseMatchingCallCount, Is.EqualTo(1));
            }
            finally
            {
                if (detachedScope != null)
                    Object.DestroyImmediate(detachedScope.gameObject);
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void AllDelete_AppliesIncludeAndInactiveFiltersThroughPoolPredicate()
        {
            GameObject rootObject = new GameObject("RuntimeRoot");
            RuntimeLifetimeScope activeEnemy = CreateScope("ActiveEnemy", id: "Enemy_01", category: "Enemy", active: true);
            RuntimeLifetimeScope inactiveEnemy = CreateScope("InactiveEnemy", id: "Enemy_02", category: "Enemy", active: false);
            RuntimeLifetimeScope activeNpc = CreateScope("ActiveNpc", id: "Npc_01", category: "Npc", active: true);

            try
            {
                FakeRuntimeLifetimeScopePool pool = new FakeRuntimeLifetimeScopePool();
                pool.Add(activeEnemy);
                pool.Add(inactiveEnemy);
                pool.Add(activeNpc);

                SceneSpawnerRegistry registry = new SceneSpawnerRegistry();
                RuntimeLifetimeScopeSpawnerService service = new RuntimeLifetimeScopeSpawnerService(pool, rootObject.transform, string.Empty, registry);
                RuntimeLifetimeScopeDeleteFilter filter = new RuntimeLifetimeScopeDeleteFilter
                {
                    UseInclude = true,
                    Include = new CommandTargetIdentityFilter
                    {
                        category = "Enemy",
                    },
                    UseExclude = false,
                    IncludeInactive = false,
                };

                int deleted = service.AllDelete(filter);

                Assert.That(deleted, Is.EqualTo(1));
                Assert.That(pool.ReleaseMatchingCallCount, Is.EqualTo(1));
            }
            finally
            {
                DestroyScope(activeEnemy);
                DestroyScope(inactiveEnemy);
                DestroyScope(activeNpc);
                Object.DestroyImmediate(rootObject);
            }
        }

        static RuntimeLifetimeScope CreateScope(string name, string id, string category, bool active)
        {
            GameObject gameObject = new GameObject(name);
            RuntimeLifetimeScope scope = gameObject.AddComponent<RuntimeLifetimeScope>();
            RuntimeIdentityData identity = RuntimeIdentityData.CreateDefault(scope.transform, id: id, category: category);
            identity.Kind = LifetimeScopeKind.Entity;
            scope.RuntimeIdentity.Apply(identity);
            gameObject.SetActive(active);
            return scope;
        }

        static void DestroyScope(RuntimeLifetimeScope scope)
        {
            if (scope == null)
                return;

            Object.DestroyImmediate(scope.gameObject);
        }

        sealed class FakeRuntimeLifetimeScopePool : IRuntimeLifetimeScopePool
        {
            public int ReleaseMatchingCallCount { get; private set; }

            readonly List<RuntimeLifetimeScope> _scopes = new();

            public void Add(RuntimeLifetimeScope scope)
            {
                if (scope != null)
                    _scopes.Add(scope);
            }

            public UniTask<RuntimeLifetimeScope> AcquireAsync(
                BaseRuntimeTemplateSO template,
                Transform parent,
                Vector3 position,
                Quaternion rotation,
                RuntimeIdentityData? identity = null,
                IScopeNode? lifetimeScopeParent = null,
                CancellationToken ct = default)
            {
                throw new NotSupportedException();
            }

            public UniTask WarmupAsync(BaseRuntimeTemplateSO template, int count, CancellationToken ct = default)
            {
                throw new NotSupportedException();
            }

            public void Release(RuntimeLifetimeScope scope)
            {
                throw new NotSupportedException();
            }

            public int ReleaseMatching(Predicate<RuntimeLifetimeScope> predicate)
            {
                ReleaseMatchingCallCount++;

                int deleted = 0;
                for (int index = _scopes.Count - 1; index >= 0; index--)
                {
                    RuntimeLifetimeScope scope = _scopes[index];
                    if (scope == null || !predicate(scope))
                        continue;

                    _scopes.RemoveAt(index);
                    deleted++;
                }

                return deleted;
            }

            public bool TryEnqueueOnNextAcquire(RuntimeLifetimeScope scope, CommandListData commands, CommandRunOptions options)
            {
                throw new NotSupportedException();
            }

            public void Dispose()
            {
            }
        }
    }
}