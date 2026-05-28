#nullable enable
using System;
using System.Linq;
using Game.Kernel.IR;
using Game.Kernel.Value;
using NUnit.Framework;

namespace Game.Editor.Tests
{
    public sealed class ValueStoreContractTests
    {
        [Test]
        public void ValueStoreScopeKind_MatchesSpecValues()
        {
            Assert.That((int)ValueStoreScopeKind.Kernel, Is.EqualTo(10));
            Assert.That((int)ValueStoreScopeKind.Project, Is.EqualTo(20));
            Assert.That((int)ValueStoreScopeKind.Scene, Is.EqualTo(30));
            Assert.That((int)ValueStoreScopeKind.Scope, Is.EqualTo(40));
            Assert.That((int)ValueStoreScopeKind.Entity, Is.EqualTo(50));
            Assert.That((int)ValueStoreScopeKind.CommandLocal, Is.EqualTo(60));
            Assert.That((int)ValueStoreScopeKind.Test, Is.EqualTo(90));
        }

        [Test]
        public void ValueVariant_ScalarFactoriesRoundTripWithoutLegacyPayloadTypes()
        {
            ValueVariant boolValue = ValueVariant.FromBool(true);
            ValueVariant intValue = ValueVariant.FromInt(5);
            ValueVariant longValue = ValueVariant.FromLong(99L);
            ValueVariant floatValue = ValueVariant.FromFloat(1.5f);
            ValueVariant doubleValue = ValueVariant.FromDouble(2.5d);
            ValueVariant stringValue = ValueVariant.FromString("hp.current");

            Assert.That(boolValue.Kind, Is.EqualTo(ValueKind.Bool));
            Assert.That(boolValue.TryGetBool(out bool resolvedBool), Is.True);
            Assert.That(resolvedBool, Is.True);

            Assert.That(intValue.Kind, Is.EqualTo(ValueKind.Int));
            Assert.That(intValue.TryGetInt(out int resolvedInt), Is.True);
            Assert.That(resolvedInt, Is.EqualTo(5));
            Assert.That(intValue.TryGetLong(out _), Is.False);

            Assert.That(longValue.Kind, Is.EqualTo(ValueKind.Long));
            Assert.That(longValue.TryGetLong(out long resolvedLong), Is.True);
            Assert.That(resolvedLong, Is.EqualTo(99L));

            Assert.That(floatValue.Kind, Is.EqualTo(ValueKind.Float));
            Assert.That(floatValue.TryGetFloat(out float resolvedFloat), Is.True);
            Assert.That(resolvedFloat, Is.EqualTo(1.5f));
            Assert.That(floatValue.TryGetDouble(out _), Is.False);

            Assert.That(doubleValue.Kind, Is.EqualTo(ValueKind.Double));
            Assert.That(doubleValue.TryGetDouble(out double resolvedDouble), Is.True);
            Assert.That(resolvedDouble, Is.EqualTo(2.5d));

            Assert.That(stringValue.Kind, Is.EqualTo(ValueKind.String));
            Assert.That(stringValue.TryGetString(out string? resolvedString), Is.True);
            Assert.That(resolvedString, Is.EqualTo("hp.current"));
        }

        [Test]
        public void ValueKeyMetadata_FlattensRuntimeFacingFieldsWithoutStableKey()
        {
            ValueKeyMetadata metadata = new ValueKeyMetadata(
                new ValueKeyId(301),
                new ValueSchemaId(401),
                ValueKind.Int,
                "HP Current",
                persists: true,
                saveAcrossProfiles: false,
                saveChannel: "profile");

            Assert.That(metadata.KeyId, Is.EqualTo(new ValueKeyId(301)));
            Assert.That(metadata.SchemaId, Is.EqualTo(new ValueSchemaId(401)));
            Assert.That(metadata.Kind, Is.EqualTo(ValueKind.Int));
            Assert.That(metadata.DisplayName, Is.EqualTo("HP Current"));
            Assert.That(metadata.Persists, Is.True);
            Assert.That(metadata.SaveAcrossProfiles, Is.False);
            Assert.That(metadata.SaveChannel, Is.EqualTo("profile"));
            Assert.That(typeof(ValueKeyMetadata).GetProperty("StableKey"), Is.Null);
        }

        [Test]
        public void ValueStoreSurface_IsValueKeyIdBasedAndExcludesStableKeyOverloads()
        {
            Type storeType = typeof(IValueStore);
            var methods = storeType.GetMethods();

            Assert.That(methods.Any(method => method.Name == nameof(IReadOnlyValueStore.TryRead)), Is.True);
            Assert.That(methods.Any(method => method.Name == nameof(IValueStore.TryWrite)), Is.True);
            Assert.That(methods.Any(method => method.Name == nameof(IReadOnlyValueStore.GetRevision)), Is.True);
            Assert.That(methods.Any(method => method.Name == nameof(IReadOnlyValueStore.TryGetMetadata)), Is.True);
            Assert.That(methods.Any(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(string))), Is.False);

            var readMethod = methods.Single(method => method.Name == nameof(IReadOnlyValueStore.TryRead));
            var writeMethod = methods.Single(method => method.Name == nameof(IValueStore.TryWrite));
            Assert.That(readMethod.GetParameters()[0].ParameterType, Is.EqualTo(typeof(ValueKeyId)));
            Assert.That(writeMethod.GetParameters()[0].ParameterType, Is.EqualTo(typeof(ValueKeyId)));
            Assert.That(writeMethod.GetParameters()[1].ParameterType, Is.EqualTo(typeof(ValueVariant).MakeByRefType()));
        }

        [Test]
        public void ValueAssembly_DoesNotReferenceUnityOrLegacyGameplayAssembly()
        {
            string[] references = typeof(IValueStore).Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name ?? string.Empty)
                .ToArray();

            Assert.That(references, Does.Not.Contain("UnityEngine"));
            Assert.That(references, Does.Not.Contain("Assembly-CSharp"));
            Assert.That(references, Does.Not.Contain("Assembly-CSharp-firstpass"));
        }
    }
}