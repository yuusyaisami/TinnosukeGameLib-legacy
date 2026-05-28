using Game.Common;
using Game.Kernel.IR;
using Game.Kernel.Value;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class VarStoreValueStoreBridgeTests
    {
        [Test]
        public void VarStoreBackedValueStore_RoundTripsSupportedScalarValues()
        {
            VarStore varStore = new VarStore();
            VarStoreBackedValueStore valueStore = new VarStoreBackedValueStore(varStore, ValueStoreScopeKind.Entity);

            ValueKeyId boolKey = new ValueKeyId(2_101);
            ValueKeyId intKey = new ValueKeyId(2_102);
            ValueKeyId floatKey = new ValueKeyId(2_103);
            ValueKeyId stringKey = new ValueKeyId(2_104);

            Assert.That(valueStore.TryWrite(boolKey, ValueVariant.FromBool(true)), Is.True);
            Assert.That(valueStore.TryWrite(intKey, ValueVariant.FromInt(42)), Is.True);
            Assert.That(valueStore.TryWrite(floatKey, ValueVariant.FromFloat(3.5f)), Is.True);
            Assert.That(valueStore.TryWrite(stringKey, ValueVariant.FromString("bridge")), Is.True);

            Assert.That(valueStore.TryRead(boolKey, out ValueVariant boolValue), Is.True);
            Assert.That(boolValue.TryGetBool(out bool boolResult), Is.True);
            Assert.That(boolResult, Is.True);

            Assert.That(valueStore.TryRead(intKey, out ValueVariant intValue), Is.True);
            Assert.That(intValue.TryGetInt(out int intResult), Is.True);
            Assert.That(intResult, Is.EqualTo(42));

            Assert.That(valueStore.TryRead(floatKey, out ValueVariant floatValue), Is.True);
            Assert.That(floatValue.TryGetFloat(out float floatResult), Is.True);
            Assert.That(floatResult, Is.EqualTo(3.5f));

            Assert.That(valueStore.TryRead(stringKey, out ValueVariant stringValue), Is.True);
            Assert.That(stringValue.TryGetString(out string? stringResult), Is.True);
            Assert.That(stringResult, Is.EqualTo("bridge"));

            Assert.That(valueStore.GetRevision(boolKey), Is.EqualTo(1u));
            Assert.That(valueStore.GetRevision(intKey), Is.EqualTo(1u));
            Assert.That(valueStore.GetRevision(floatKey), Is.EqualTo(1u));
            Assert.That(valueStore.GetRevision(stringKey), Is.EqualTo(1u));
        }

        [Test]
        public void VarStoreBackedValueStore_RejectsLongAndDoubleWritesUntilBackendSupportsThem()
        {
            VarStore varStore = new VarStore();
            VarStoreBackedValueStore valueStore = new VarStoreBackedValueStore(varStore, ValueStoreScopeKind.Entity);

            ValueKeyId longKey = new ValueKeyId(2_201);
            ValueKeyId doubleKey = new ValueKeyId(2_202);

            Assert.That(valueStore.TryWrite(longKey, ValueVariant.FromLong(99L)), Is.False);
            Assert.That(valueStore.TryWrite(doubleKey, ValueVariant.FromDouble(6.25d)), Is.False);
            Assert.That(valueStore.TryRead(longKey, out _), Is.False);
            Assert.That(valueStore.TryRead(doubleKey, out _), Is.False);
            Assert.That(valueStore.GetRevision(longKey), Is.EqualTo(0u));
            Assert.That(valueStore.GetRevision(doubleKey), Is.EqualTo(0u));
        }

        [Test]
        public void VarStoreBackedValueStore_NullWriteUnsetsExistingValue()
        {
            VarStore varStore = new VarStore();
            VarStoreBackedValueStore valueStore = new VarStoreBackedValueStore(varStore, ValueStoreScopeKind.Entity);
            ValueKeyId keyId = new ValueKeyId(2_301);

            Assert.That(valueStore.TryWrite(keyId, ValueVariant.FromInt(7)), Is.True);
            Assert.That(valueStore.TryRead(keyId, out _), Is.True);

            Assert.That(valueStore.TryWrite(keyId, ValueVariant.Null), Is.True);
            Assert.That(valueStore.TryRead(keyId, out _), Is.False);
        }
    }
}