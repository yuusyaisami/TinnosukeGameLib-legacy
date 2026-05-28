#nullable enable
using System;
using System.Reflection;
using Game.Kernel.Boot;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class UnityObjectLinkTests
    {
        const string RuntimeLinkTypeName = "Game.Kernel.ScopeGraph.UnityObjectLink";
        const string RuntimeLinkKindTypeName = "Game.Kernel.ScopeGraph.UnityObjectLinkKind";

        static readonly Type RuntimeLinkType = ResolveRuntimeType(RuntimeLinkTypeName);
        static readonly Type RuntimeLinkKindType = ResolveRuntimeType(RuntimeLinkKindTypeName);

        [Test]
        public void DefaultValue_IsEmpty_AndUsesUnknownKind()
        {
            object link = Activator.CreateInstance(RuntimeLinkType)!;
            object unknownKind = Enum.Parse(RuntimeLinkKindType, "Unknown");

            Assert.That(ReadProperty<bool>(link, "IsEmpty"), Is.True);
            Assert.That(ReadProperty<object>(link, "Kind"), Is.EqualTo(unknownKind));
            Assert.That(ReadProperty<string>(link, "SourceGuid"), Is.EqualTo(string.Empty));
            Assert.That(ReadProperty<long>(link, "LocalFileId"), Is.EqualTo(0L));
            Assert.That(ReadProperty<int>(link, "RuntimeInstanceId"), Is.EqualTo(0));
            Assert.That(ReadProperty<string>(link, "DebugName"), Is.EqualTo(string.Empty));
        }

        [Test]
        public void Bridge_CreatesSpecShapedLink_WithTraceMetadata()
        {
            object sceneKind = Enum.Parse(RuntimeLinkKindType, "Scene");
            object link = InvokeCreate(sceneKind, "scene-guid-123", 123L, 77, "Scene/Root/Child");

            Assert.That(ReadProperty<bool>(link, "IsEmpty"), Is.False);
            Assert.That(ReadProperty<object>(link, "Kind"), Is.EqualTo(sceneKind));
            Assert.That(ReadProperty<string>(link, "SourceGuid"), Is.EqualTo("scene-guid-123"));
            Assert.That(ReadProperty<long>(link, "LocalFileId"), Is.EqualTo(123L));
            Assert.That(ReadProperty<int>(link, "RuntimeInstanceId"), Is.EqualTo(77));
            Assert.That(ReadProperty<string>(link, "DebugName"), Is.EqualTo("Scene/Root/Child"));
            Assert.That(ReadProperty<bool>(link, "HasPersistentSource"), Is.True);
        }

        [Test]
        public void Bridge_RejectsMissingDebugName_ForNonEmptyLinks()
        {
            object sceneKind = Enum.Parse(RuntimeLinkKindType, "Scene");

            Assert.That(
                () => InvokeCreate(sceneKind, "scene-guid-123", 123L, 77, "   "),
                Throws.TypeOf<TargetInvocationException>()
                    .With.InnerException.TypeOf<ArgumentException>());
        }

        static object InvokeCreate(object kind, string sourceGuid, long localFileId, int runtimeInstanceId, string debugName)
        {
            MethodInfo? createMethod = typeof(UnityObjectLinkBridge).GetMethod(
                "Create",
                BindingFlags.Public | BindingFlags.Static);

            if (createMethod == null)
                throw new AssertionException("UnityObjectLinkBridge.Create was not found.");

            object? result = createMethod.Invoke(null, new object?[] { kind, sourceGuid, localFileId, runtimeInstanceId, debugName });
            if (result == null)
                throw new AssertionException("UnityObjectLinkBridge.Create returned null.");

            return result;
        }

        static T ReadProperty<T>(object instance, string propertyName)
        {
            PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
                throw new AssertionException($"Property '{propertyName}' was not found on '{instance.GetType().FullName}'.");

            object? value = property.GetValue(instance);
            if (value is T typed)
                return typed;

            throw new AssertionException($"Property '{propertyName}' returned an unexpected type.");
        }

        static Type ResolveRuntimeType(string fullTypeName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type? resolved = assemblies[index].GetType(fullTypeName, throwOnError: false);
                if (resolved != null)
                    return resolved;
            }

            throw new AssertionException($"Missing runtime type: {fullTypeName}");
        }
    }
}