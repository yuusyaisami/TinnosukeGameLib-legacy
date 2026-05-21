using System;
using Game.Kernel.Abstractions;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelProfilerMarkerTaxonomyTests
    {
        [Test]
        public void KernelProfilerMarkerTaxonomy_UsesStableExplicitFamilies()
        {
            IReadOnlyList<KernelProfilerMarkerFamily> families = KernelProfilerMarkerTaxonomy.Families;

            Assert.That(families, Has.Count.EqualTo(10));
            Assert.That(families[0].TaxonomyName, Is.EqualTo("Kernel.Boot"));
            Assert.That(families[1].TaxonomyName, Is.EqualTo("Kernel.ServiceGraph"));
            Assert.That(families[2].TaxonomyName, Is.EqualTo("Kernel.ScopeGraph"));
            Assert.That(families[3].TaxonomyName, Is.EqualTo("Kernel.Lifecycle"));
            Assert.That(families[4].TaxonomyName, Is.EqualTo("Kernel.CommandCatalog"));
            Assert.That(families[5].TaxonomyName, Is.EqualTo("Kernel.ValueStore"));
            Assert.That(families[6].TaxonomyName, Is.EqualTo("Kernel.DynamicEvaluation"));
            Assert.That(families[7].TaxonomyName, Is.EqualTo("Kernel.Diagnostics"));
            Assert.That(families[8].TaxonomyName, Is.EqualTo("Kernel.UnityBridge"));
            Assert.That(families[9].TaxonomyName, Is.EqualTo("Kernel.LegacyCompat"));
        }

        [Test]
        public void KernelProfilerMarkerTaxonomy_MapsToCanonicalMarkerPrefixes()
        {
            IReadOnlyList<KernelProfilerMarkerFamily> families = KernelProfilerMarkerTaxonomy.Families;

            Assert.That(families[0].MarkerPrefix, Is.EqualTo("KernelBoot"));
            Assert.That(families[1].MarkerPrefix, Is.EqualTo("ServiceGraph"));
            Assert.That(families[2].MarkerPrefix, Is.EqualTo("ScopeGraph"));
            Assert.That(families[3].MarkerPrefix, Is.EqualTo("Lifecycle"));
            Assert.That(families[4].MarkerPrefix, Is.EqualTo("CommandCatalog"));
            Assert.That(families[5].MarkerPrefix, Is.EqualTo("Command"));
            Assert.That(families[6].MarkerPrefix, Is.EqualTo("ValueStore"));
            Assert.That(families[7].MarkerPrefix, Is.EqualTo("RuntimeQuery"));
            Assert.That(families[8].MarkerPrefix, Is.EqualTo("DynamicEvaluation"));
            Assert.That(families[9].MarkerPrefix, Is.EqualTo("Diagnostics"));
            Assert.That(families[10].MarkerPrefix, Is.EqualTo("AuthoringBridge"));
            Assert.That(families[11].MarkerPrefix, Is.EqualTo("LegacyCompat"));
        }

        [Test]
        public void KernelProfilerMarkerFamily_BuildsCanonicalMarkerNames()
        {
            Assert.That(KernelProfilerMarkerTaxonomy.Boot.CreateMarkerName("LoadInputs"), Is.EqualTo("KernelBoot.LoadInputs"));
            Assert.That(KernelProfilerMarkerTaxonomy.ServiceGraph.CreateMarkerName("Resolve"), Is.EqualTo("ServiceGraph.Resolve"));
            Assert.That(KernelProfilerMarkerTaxonomy.Command.CreateMarkerName("Execute"), Is.EqualTo("Command.Execute"));
            Assert.That(KernelProfilerMarkerTaxonomy.RuntimeQuery.CreateMarkerName("Lookup"), Is.EqualTo("RuntimeQuery.Lookup"));
            Assert.That(KernelProfilerMarkerTaxonomy.UnityBridge.CreateMarkerName("Extract"), Is.EqualTo("AuthoringBridge.Extract"));
        }

        [Test]
        public void KernelProfilerMarkerTaxonomy_ExposesSupplementalFamiliesSeparately()
        {
            Assert.That(KernelProfilerMarkerTaxonomy.Command.TaxonomyName, Is.EqualTo("Kernel.Command"));
            Assert.That(KernelProfilerMarkerTaxonomy.RuntimeQuery.TaxonomyName, Is.EqualTo("Kernel.RuntimeQuery"));
        }
    }
}