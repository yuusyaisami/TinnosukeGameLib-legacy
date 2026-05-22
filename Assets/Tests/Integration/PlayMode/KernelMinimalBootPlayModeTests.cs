#nullable enable
using Game.Kernel.Abstractions;
using Game.Kernel.Boot;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.PlayMode
{
    [TestFixture]
    public sealed class KernelMinimalBootPlayModeTests
    {
        [Test]
        public void MinimalPublishedBundle_ProducesReadyBootBoundaryResult()
        {
            KernelBootPublishedArtifactBundle bundle = KernelBootPublishedArtifactBundleFactory.CreateMinimal(
                new KernelProfile(new KernelProfileId(22001), KernelProfileKind.Development),
                new ManifestId(22001),
                new BootPolicyId(22001),
                new PlanId(22001),
                new ArtifactSetId(22001),
                formatVersion: 1,
                generatorVersion: "M15.4-PlayMode");

            BootValidationInput input = bundle.CreateValidationInput(new BootFallbackValidationState(false, false, false, false, false, false));

            BootValidationReport report = BootValidator.Validate(input);
            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input);

            Assert.That(report.Status, Is.EqualTo(ValidationResultStatus.Passed));
            Assert.That(result.Status, Is.EqualTo(KernelBootBoundaryStatus.Ready));
            Assert.That(result.IsReady, Is.True);
            Assert.That(result, Is.InstanceOf<KernelBootBoundaryResult.Success>());
        }
    }
}