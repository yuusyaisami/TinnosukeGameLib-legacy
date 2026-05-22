#nullable enable
using System.Reflection;
using Game.Commands.VNext;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class CommandRuntimeLegacyRemovalTests
    {
        [Test]
        public void CommandRunOptions_AndResolveContext_DoNotExposeRuntimeKeyFallbackSurface()
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;

            Assert.That(typeof(CommandRunOptions).GetField("AllowRuntimeKeyFallback", flags), Is.Null);
            Assert.That(typeof(CommandRunOptions).GetProperty("AllowRuntimeKeyFallback", flags), Is.Null);
            Assert.That(typeof(CommandResolveContext).GetField("AllowRuntimeKeyFallback", flags), Is.Null);
            Assert.That(typeof(CommandResolveContext).GetProperty("AllowRuntimeKeyFallback", flags), Is.Null);
        }
    }
}