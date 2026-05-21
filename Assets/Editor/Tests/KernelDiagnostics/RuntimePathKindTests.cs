using System;
using Game.Kernel.Abstractions;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class RuntimePathKindTests
    {
        [Test]
        public void RuntimePathKind_UsesStableExplicitValues()
        {
            Assert.That((int)RuntimePathKind.HotPath, Is.EqualTo(10));
            Assert.That((int)RuntimePathKind.WarmPath, Is.EqualTo(20));
            Assert.That((int)RuntimePathKind.ColdPath, Is.EqualTo(30));
            Assert.That((int)RuntimePathKind.BootPath, Is.EqualTo(40));
            Assert.That((int)RuntimePathKind.EditorGenerationPath, Is.EqualTo(50));
            Assert.That((int)RuntimePathKind.ValidationPath, Is.EqualTo(60));
            Assert.That((int)RuntimePathKind.TestOnlyPath, Is.EqualTo(70));
            Assert.That((int)RuntimePathKind.LegacyMigrationPath, Is.EqualTo(90));
        }

        [Test]
        public void RuntimePathKind_ContainsOnlyTheRequiredMembers()
        {
            string[] names = Enum.GetNames(typeof(RuntimePathKind));

            Assert.That(names, Is.EqualTo(new[]
            {
                nameof(RuntimePathKind.HotPath),
                nameof(RuntimePathKind.WarmPath),
                nameof(RuntimePathKind.ColdPath),
                nameof(RuntimePathKind.BootPath),
                nameof(RuntimePathKind.EditorGenerationPath),
                nameof(RuntimePathKind.ValidationPath),
                nameof(RuntimePathKind.TestOnlyPath),
                nameof(RuntimePathKind.LegacyMigrationPath),
            }));
        }
    }
}