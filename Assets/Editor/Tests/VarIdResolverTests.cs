#nullable enable

using System.Reflection;
using Game.Common;
using Game.VarStoreKeys;
using NUnit.Framework;
using UnityEngine;

namespace Game.Editor.Tests
{
    public sealed class VarIdResolverTests
    {
        FieldInfo? positiveCacheField;
        VarKeyRegistry? previousRegistry;

        [SetUp]
        public void SetUp()
        {
            positiveCacheField = typeof(VarIdResolver).GetField("s_positiveCache", BindingFlags.Static | BindingFlags.NonPublic);
            VarKeyRegistryLocator.TryGetExplicitRegistry(out previousRegistry);

            VerifiedValueRuntimeBridge.Deactivate();
            VarKeyRegistryLocator.ClearExplicitRuntimeRegistry();

            if (positiveCacheField?.GetValue(null) is System.Collections.IDictionary cache)
                cache.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (positiveCacheField?.GetValue(null) is System.Collections.IDictionary cache)
                cache.Clear();

            VerifiedValueRuntimeBridge.Deactivate();

            if (previousRegistry != null)
                VarKeyRegistryLocator.SetExplicitRuntimeRegistry(previousRegistry);
            else
                VarKeyRegistryLocator.ClearExplicitRuntimeRegistry();
        }

        [Test]
        public void TryResolve_MissingStableKeyFailsClosedWithoutNegativeFallback()
        {
            VarKeyRegistry registry = CreateSeededRegistry();
            VarKeyRegistryLocator.SetExplicitRuntimeRegistry(registry);

            bool resolved = VarIdResolver.TryResolve("__missing_value_key_for_m12_6__", out int varId, out VarIdResolver.VarIdResolutionFailureReason failureReason);

            Assert.That(resolved, Is.False);
            Assert.That(varId, Is.EqualTo(0));
            Assert.That(failureReason, Is.EqualTo(VarIdResolver.VarIdResolutionFailureReason.StableKeyNotFound));
        }

        [Test]
        public void TryResolve_KnownStableKeyResolvesFromExplicitRuntimeRegistry()
        {
            VarKeyRegistry registry = CreateSeededRegistry();
            VarKeyRegistryLocator.SetExplicitRuntimeRegistry(registry);

            bool resolved = VarIdResolver.TryResolve("traitRuntime.presentationState", out int varId);

            Assert.That(resolved, Is.True);
            Assert.That(varId, Is.EqualTo(100125));
        }

        [Test]
        public void TryResolve_PrefersVerifiedRuntimeSessionBeforeRegistryLookup()
        {
            VarKeyRegistry registry = CreateSeededRegistry();
            VarKeyRegistryLocator.SetExplicitRuntimeRegistry(registry);
            VerifiedValueRuntimeBridge.Activate(new TestVerifiedValueSession(("verified.value", 500321)));

            bool resolved = VarIdResolver.TryResolve("verified.value", out int varId, out VarIdResolver.VarIdResolutionFailureReason failureReason);

            Assert.That(resolved, Is.True);
            Assert.That(varId, Is.EqualTo(500321));
            Assert.That(failureReason, Is.EqualTo(VarIdResolver.VarIdResolutionFailureReason.None));
        }

        [Test]
        public void TryResolve_PrefersVerifiedRuntimeSessionOverCachedRegistryResolution()
        {
            VarKeyRegistry registry = CreateSeededRegistry();
            VarKeyRegistryLocator.SetExplicitRuntimeRegistry(registry);

            Assert.That(VarIdResolver.TryResolve("traitRuntime.presentationState", out int cachedVarId), Is.True);
            Assert.That(cachedVarId, Is.EqualTo(100125));

            VerifiedValueRuntimeBridge.Activate(new TestVerifiedValueSession(("traitRuntime.presentationState", 500777)));

            bool resolved = VarIdResolver.TryResolve("traitRuntime.presentationState", out int varId, out VarIdResolver.VarIdResolutionFailureReason failureReason);

            Assert.That(resolved, Is.True);
            Assert.That(varId, Is.EqualTo(500777));
            Assert.That(failureReason, Is.EqualTo(VarIdResolver.VarIdResolutionFailureReason.None));
        }

        [Test]
        public void TryResolve_DoesNotFallbackToRegistryWhenVerifiedRuntimeIsActive()
        {
            VarKeyRegistry registry = CreateSeededRegistry();
            VarKeyRegistryLocator.SetExplicitRuntimeRegistry(registry);
            VerifiedValueRuntimeBridge.Activate(new TestVerifiedValueSession());

            bool resolved = VarIdResolver.TryResolve("traitRuntime.presentationState", out int varId, out VarIdResolver.VarIdResolutionFailureReason failureReason);

            Assert.That(resolved, Is.False);
            Assert.That(varId, Is.EqualTo(0));
            Assert.That(failureReason, Is.EqualTo(VarIdResolver.VarIdResolutionFailureReason.StableKeyNotFound));
        }

        [Test]
        public void TryResolve_DoesNotReturnStaleVerifiedCacheAfterVerifiedRuntimeDeactivates()
        {
            VerifiedValueRuntimeBridge.Activate(new TestVerifiedValueSession(("verified.value", 500321)));

            Assert.That(VarIdResolver.TryResolve("verified.value", out int activeVarId), Is.True);
            Assert.That(activeVarId, Is.EqualTo(500321));

            VerifiedValueRuntimeBridge.Deactivate();

            bool resolved = VarIdResolver.TryResolve("verified.value", out int varId, out VarIdResolver.VarIdResolutionFailureReason failureReason);

            Assert.That(resolved, Is.False);
            Assert.That(varId, Is.EqualTo(0));
            Assert.That(failureReason, Is.EqualTo(VarIdResolver.VarIdResolutionFailureReason.RegistryUnavailable));
        }

        [Test]
        public void TryGetExplicitRegistry_FailsClosedWhenExplicitRuntimeRegistryIsCleared()
        {
            VarKeyRegistryLocator.ClearExplicitRuntimeRegistry();

            bool found = VarKeyRegistryLocator.TryGetExplicitRegistry(out VarKeyRegistry? registry);

            Assert.That(found, Is.False);
            Assert.That(registry, Is.Null);
        }

        static VarKeyRegistry CreateSeededRegistry()
        {
            var registry = ScriptableObject.CreateInstance<VarKeyRegistry>();
            MethodInfo? seedMethod = typeof(VarKeyRegistryLocator).GetMethod("EnsureRuntimeTraitPresentationStateSeed", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(seedMethod, Is.Not.Null);
            Assert.That(seedMethod!.Invoke(null, new object[] { registry }), Is.True);

            return registry;
        }

        sealed class TestVerifiedValueSession : IVerifiedValueRuntimeSession
        {
            readonly System.Collections.Generic.Dictionary<string, int> values;

            public TestVerifiedValueSession(params (string StableKey, int ValueKeyId)[] entries)
            {
                values = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);
                for (int index = 0; index < entries.Length; index++)
                    values[entries[index].StableKey] = entries[index].ValueKeyId;
            }

            public bool TryResolveValueKey(string stableKey, out int valueKeyId)
            {
                return values.TryGetValue(stableKey, out valueKeyId);
            }

            public bool TryGetStableKey(int valueKeyId, out string stableKey)
            {
                foreach (System.Collections.Generic.KeyValuePair<string, int> entry in values)
                {
                    if (entry.Value == valueKeyId)
                    {
                        stableKey = entry.Key;
                        return true;
                    }
                }

                stableKey = string.Empty;
                return false;
            }

            public VerifiedValueInitApplyResult ApplyLocalBlackboardInit(IScopeNode scope, IBlackboardService blackboard, VerifiedValueInitPhase phase, DynamicEvaluationRuntime runtime)
            {
                _ = scope;
                _ = blackboard;
                _ = phase;
                _ = runtime;
                return VerifiedValueInitApplyResult.NotAvailable();
            }
        }
    }
}