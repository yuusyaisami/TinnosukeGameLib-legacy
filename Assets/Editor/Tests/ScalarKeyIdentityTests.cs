#nullable enable
using NUnit.Framework;
using Game.Scalar;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class ScalarKeyIdentityTests
    {
        [Test]
        public void Constructor_ResolvesVerifiedIdentityForKnownKey()
        {
            var key = new ScalarKey("GameLib.Movement.DefaultSpeed");

            Assert.That(key.Id, Is.GreaterThan(0));
            Assert.That(key.Name, Is.EqualTo("GameLib.Movement.DefaultSpeed"));
            Assert.That(key.IsVerified, Is.True);
        }

        [Test]
        public void Constructor_RejectsUnknownKey()
        {
            var key = new ScalarKey("Unknown.Scalar.Key");

            Assert.That(key.Id, Is.EqualTo(0));
            Assert.That(key.IsVerified, Is.False);
        }

        [Test]
        public void OnAfterDeserialize_UsesVerifiedIdentity()
        {
            var first = new ScalarKey("GameLib.Health.Current");
            var second = new ScalarKey("GameLib.Health.Current");

            Assert.That(first.Id, Is.EqualTo(second.Id));
            Assert.That(first.IsVerified, Is.True);
        }
    }
}
