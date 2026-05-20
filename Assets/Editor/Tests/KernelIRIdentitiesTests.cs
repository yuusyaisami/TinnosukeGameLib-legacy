using System;
using System.Reflection;
using Game.Kernel.IR;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelIRIdentitiesTests
    {
        [Test]
        public void TypedIdentityPrimitives_PreserveValueEqualityAndHashCode()
        {
            foreach (IdentityCase identityCase in GetIdentityCases())
            {
                object first = identityCase.Create(17);
                object same = identityCase.Create(17);
                object different = identityCase.Create(21);

                Assert.That(identityCase.ReadValue(first), Is.EqualTo(17), identityCase.Name + " should preserve the assigned value.");
                Assert.That(first.Equals(same), Is.True, identityCase.Name + " should compare equal for the same value.");
                Assert.That(first.Equals(different), Is.False, identityCase.Name + " should compare unequal for different values.");
                Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()), identityCase.Name + " should keep equal hash codes for equal values.");
                Assert.That(first.ToString(), Does.Contain("17"), identityCase.Name + " should render a stable debug representation.");
            }
        }

        [Test]
        public void TypedIdentityPrimitives_DefaultValues_AreZeroAndDoNotThrow()
        {
            foreach (IdentityCase identityCase in GetIdentityCases())
            {
                object defaultValue = identityCase.CreateDefault();

                Assert.That(identityCase.ReadValue(defaultValue), Is.Zero, identityCase.Name + " default value should be zero/unset.");
                Assert.That(defaultValue.ToString(), Is.Not.Null, identityCase.Name + " default value should still have a debug representation.");
            }
        }

        [Test]
        public void TypedIdentityPrimitives_DoNotExposeImplicitOrExplicitNumericConversions()
        {
            foreach (IdentityCase identityCase in GetIdentityCases())
            {
                MethodInfo[] publicStaticMethods = identityCase.Type.GetMethods(BindingFlags.Public | BindingFlags.Static);
                for (int i = 0; i < publicStaticMethods.Length; i++)
                {
                    Assert.That(publicStaticMethods[i].Name, Is.Not.EqualTo("op_Implicit"), identityCase.Name + " must not expose implicit numeric conversions.");
                    Assert.That(publicStaticMethods[i].Name, Is.Not.EqualTo("op_Explicit"), identityCase.Name + " must not expose explicit numeric conversions.");
                }
            }
        }

        static IdentityCase[] GetIdentityCases()
        {
            return new[]
            {
                new IdentityCase("ModuleId", typeof(ModuleId), value => new ModuleId(value), instance => ((ModuleId)instance).Value, () => default(ModuleId)),
                new IdentityCase("ServiceId", typeof(ServiceId), value => new ServiceId(value), instance => ((ServiceId)instance).Value, () => default(ServiceId)),
                new IdentityCase("ScopeAuthoringId", typeof(ScopeAuthoringId), value => new ScopeAuthoringId(value), instance => ((ScopeAuthoringId)instance).Value, () => default(ScopeAuthoringId)),
                new IdentityCase("ScopePlanId", typeof(ScopePlanId), value => new ScopePlanId(value), instance => ((ScopePlanId)instance).Value, () => default(ScopePlanId)),
                new IdentityCase("CommandTypeId", typeof(CommandTypeId), value => new CommandTypeId(value), instance => ((CommandTypeId)instance).Value, () => default(CommandTypeId)),
                new IdentityCase("CommandExecutorId", typeof(CommandExecutorId), value => new CommandExecutorId(value), instance => ((CommandExecutorId)instance).Value, () => default(CommandExecutorId)),
                new IdentityCase("CommandPayloadSchemaId", typeof(CommandPayloadSchemaId), value => new CommandPayloadSchemaId(value), instance => ((CommandPayloadSchemaId)instance).Value, () => default(CommandPayloadSchemaId)),
                new IdentityCase("CommandAuthoringKeyId", typeof(CommandAuthoringKeyId), value => new CommandAuthoringKeyId(value), instance => ((CommandAuthoringKeyId)instance).Value, () => default(CommandAuthoringKeyId)),
                new IdentityCase("ValueKeyId", typeof(ValueKeyId), value => new ValueKeyId(value), instance => ((ValueKeyId)instance).Value, () => default(ValueKeyId)),
                new IdentityCase("ValueSchemaId", typeof(ValueSchemaId), value => new ValueSchemaId(value), instance => ((ValueSchemaId)instance).Value, () => default(ValueSchemaId)),
                new IdentityCase("LifecycleStepId", typeof(LifecycleStepId), value => new LifecycleStepId(value), instance => ((LifecycleStepId)instance).Value, () => default(LifecycleStepId)),
                new IdentityCase("RuntimeQueryId", typeof(RuntimeQueryId), value => new RuntimeQueryId(value), instance => ((RuntimeQueryId)instance).Value, () => default(RuntimeQueryId)),
                new IdentityCase("SourceLocationId", typeof(SourceLocationId), value => new SourceLocationId(value), instance => ((SourceLocationId)instance).Value, () => default(SourceLocationId)),
            };
        }

        sealed class IdentityCase
        {
            public IdentityCase(string name, Type type, Func<int, object> create, Func<object, int> readValue, Func<object> createDefault)
            {
                Name = name;
                Type = type;
                Create = create;
                ReadValue = readValue;
                CreateDefault = createDefault;
            }

            public string Name { get; }

            public Type Type { get; }

            public Func<int, object> Create { get; }

            public Func<object, int> ReadValue { get; }

            public Func<object> CreateDefault { get; }
        }
    }
}