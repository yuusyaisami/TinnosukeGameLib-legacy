#nullable enable
using System;
using Game.Kernel.Boot;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class UnityObjectLinkTests
    {
        [Test]
        public void DefaultValue_IsEmpty_AndUsesUnknownKind()
        {
            UnityObjectLink link = default;

            Assert.That(link.IsEmpty, Is.True);
            Assert.That(link.Kind, Is.EqualTo(UnityObjectLinkKind.Unknown));
            Assert.That(link.SourceGuid, Is.EqualTo(string.Empty));
            Assert.That(link.LocalFileId, Is.EqualTo(0L));
            Assert.That(link.RuntimeInstanceId, Is.EqualTo(0));
            Assert.That(link.DebugName, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Bridge_CreatesSpecShapedLink_WithTraceMetadata()
        {
            UnityObjectLink link = UnityObjectLinkBridge.Create(
                UnityObjectLinkKind.Scene,
                "scene-guid-123",
                123L,
                77,
                "Scene/Root/Child");

            Assert.That(link.IsEmpty, Is.False);
            Assert.That(link.Kind, Is.EqualTo(UnityObjectLinkKind.Scene));
            Assert.That(link.SourceGuid, Is.EqualTo("scene-guid-123"));
            Assert.That(link.LocalFileId, Is.EqualTo(123L));
            Assert.That(link.RuntimeInstanceId, Is.EqualTo(77));
            Assert.That(link.DebugName, Is.EqualTo("Scene/Root/Child"));
            Assert.That(link.HasPersistentSource, Is.True);
        }

        [Test]
        public void Bridge_RejectsMissingDebugName_ForNonEmptyLinks()
        {
            Assert.That(
                () => UnityObjectLinkBridge.Create(UnityObjectLinkKind.Scene, "scene-guid-123", 123L, 77, "   "),
                Throws.ArgumentException);
        }
    }
}