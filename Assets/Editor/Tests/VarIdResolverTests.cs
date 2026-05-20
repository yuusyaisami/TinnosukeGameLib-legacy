#nullable enable

using System.Reflection;
using Game.VarStoreKeys;
using NUnit.Framework;
using UnityEngine;

namespace Game.Editor.Tests
{
    public sealed class VarIdResolverTests
    {
        FieldInfo? cachedRegistryField;
        FieldInfo? positiveCacheField;
        VarKeyRegistry? previousRegistry;

        [SetUp]
        public void SetUp()
        {
            cachedRegistryField = typeof(VarKeyRegistryLocator).GetField("_cachedRegistry", BindingFlags.Static | BindingFlags.NonPublic);
            positiveCacheField = typeof(VarIdResolver).GetField("s_positiveCache", BindingFlags.Static | BindingFlags.NonPublic);
            previousRegistry = cachedRegistryField != null ? cachedRegistryField.GetValue(null) as VarKeyRegistry : null;

            if (positiveCacheField?.GetValue(null) is System.Collections.IDictionary cache)
                cache.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (positiveCacheField?.GetValue(null) is System.Collections.IDictionary cache)
                cache.Clear();

            if (cachedRegistryField != null)
                cachedRegistryField.SetValue(null, previousRegistry);
        }

        [Test]
        public void TryResolve_MissingStableKeyFailsClosedWithoutNegativeFallback()
        {
            VarKeyRegistry registry = CreateSeededRegistry();
            cachedRegistryField!.SetValue(null, registry);

            bool resolved = VarIdResolver.TryResolve("__missing_value_key_for_m12_6__", out int varId, out VarIdResolver.VarIdResolutionFailureReason failureReason);

            Assert.That(resolved, Is.False);
            Assert.That(varId, Is.EqualTo(0));
            Assert.That(failureReason, Is.EqualTo(VarIdResolver.VarIdResolutionFailureReason.StableKeyNotFound));
        }

        [Test]
        public void TryResolve_KnownStableKeyResolvesFromTemporaryExplicitRegistry()
        {
            VarKeyRegistry registry = CreateSeededRegistry();

            Assert.That(cachedRegistryField, Is.Not.Null);
            cachedRegistryField!.SetValue(null, registry);

            bool resolved = VarIdResolver.TryResolve("traitRuntime.presentationState", out int varId);

            Assert.That(resolved, Is.True);
            Assert.That(varId, Is.EqualTo(100125));
        }

        static VarKeyRegistry CreateSeededRegistry()
        {
            var registry = ScriptableObject.CreateInstance<VarKeyRegistry>();
            MethodInfo? seedMethod = typeof(VarKeyRegistryLocator).GetMethod("EnsureRuntimeTraitPresentationStateSeed", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(seedMethod, Is.Not.Null);
            Assert.That(seedMethod!.Invoke(null, new object[] { registry }), Is.True);

            return registry;
        }
    }
}