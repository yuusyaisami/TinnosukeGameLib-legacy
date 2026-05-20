#nullable enable
using System;
using Game.Kernel.Boot;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using Game.Kernel.Diagnostics;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelRuntimeServiceGraphTests
    {
        [Test]
        public void Constructor_OrdersSlotsCanonicallyAndSupportsLookup()
        {
            KernelRuntimeServiceGraph graph = new KernelRuntimeServiceGraph(CreateServiceGraphPlan(new[]
            {
                CreateService(21, ServiceFactoryKind.GeneratedFactory, "Service21"),
                CreateService(11, ServiceFactoryKind.GeneratedFactory, "Service11"),
            }));

            Assert.That(graph.ServiceSlotCount, Is.EqualTo(2));
            Assert.That(graph.RootServiceCount, Is.EqualTo(2));
            Assert.That(graph.ServiceSlots[0].SlotIndex, Is.EqualTo(0));
            Assert.That(graph.ServiceSlots[0].ServiceIdentity, Is.EqualTo(ServiceIdentity(11)));
            Assert.That(graph.ServiceSlots[1].SlotIndex, Is.EqualTo(1));
            Assert.That(graph.ServiceSlots[1].ServiceIdentity, Is.EqualTo(ServiceIdentity(21)));
            Assert.That(graph.TryGetServiceSlot(ServiceIdentity(11), out KernelRuntimeServiceSlot slot11), Is.True);
            Assert.That(slot11.SlotIndex, Is.EqualTo(0));
            Assert.That(graph.TryGetServiceSlotIndex(ServiceIdentity(21), out int slot21Index), Is.True);
            Assert.That(slot21Index, Is.EqualTo(1));
            Assert.That(graph.TryGetServiceSlot(ServiceIdentity(99), out _), Is.False);
        }

        [Test]
        public void Constructor_FromServiceGraphPlan_PreservesApprovedFactoryKindsAndMetadata()
        {
            ServiceGraphPlan plan = CreateServiceGraphPlan(new[]
            {
                CreateService(21, ServiceFactoryKind.ProvidedInstance, "ProvidedService21"),
                CreateService(11, ServiceFactoryKind.GeneratedFactory, "GeneratedService11"),
            });

            KernelRuntimeServiceGraph graph = new KernelRuntimeServiceGraph(plan);

            Assert.That(graph.RootServiceCount, Is.EqualTo(2));
            Assert.That(graph.ServiceSlotCount, Is.EqualTo(2));
            Assert.That(graph.ServiceSlots[0].ServiceIdentity, Is.EqualTo(ServiceIdentity(11)));
            Assert.That(graph.ServiceSlots[0].EntryIndex, Is.EqualTo(0));
            Assert.That(graph.ServiceSlots[0].FactoryKind, Is.EqualTo(ServiceFactoryKind.GeneratedFactory));
            Assert.That(graph.ServiceSlots[0].ServiceName, Is.EqualTo("GeneratedService11"));
            Assert.That(graph.ServiceSlots[1].ServiceIdentity, Is.EqualTo(ServiceIdentity(21)));
            Assert.That(graph.ServiceSlots[1].FactoryKind, Is.EqualTo(ServiceFactoryKind.ProvidedInstance));
            Assert.That(graph.ServiceSlots[1].ServiceName, Is.EqualTo("ProvidedService21"));
        }

        [Test]
        public void Constructor_FromServiceGraphPlan_RejectsLegacyAdapterFactories()
        {
            ServiceGraphPlan plan = CreateServiceGraphPlan(new[]
            {
                CreateService(11, ServiceFactoryKind.LegacyAdapter, "LegacyService11"),
            });

            ArgumentException exception = Assert.Throws<ArgumentException>(() => new KernelRuntimeServiceGraph(plan));

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Does.Contain(KernelRuntimeServiceGraphCodes.ServiceFactoryUnsupported));
        }

        [Test]
        public void ResolveOptionalService_ReturnsResolved_WhenRequestedServiceExists()
        {
            KernelRuntimeServiceGraph graph = CreatePlanGraph(KernelProfileKind.Release);

            KernelRuntimeServiceResolutionResult result = graph.ResolveOptionalService(
                graph.ServiceSlots[0],
                ServiceIdentity(11),
                OptionalDependencyAbsenceBehavior.EmitWarning);

            Assert.That(result.Kind, Is.EqualTo(KernelRuntimeServiceResolutionKind.Resolved));
            Assert.That(result.IsResolved, Is.True);
            Assert.That(result.HasResolvedServiceSlot, Is.True);
            Assert.That(result.ResolvedServiceSlot!.Value.ServiceIdentity, Is.EqualTo(ServiceIdentity(11)));
            Assert.That(result.Diagnostic, Is.Null);
        }

        [Test]
        public void ResolveRequiredService_ReturnsMissingDiagnostic_WhenServiceIsAbsent()
        {
            KernelRuntimeServiceGraph graph = CreatePlanGraph(KernelProfileKind.Release);

            KernelRuntimeServiceResolutionResult result = graph.ResolveRequiredService(
                graph.ServiceSlots[0],
                ServiceIdentity(99));

            Assert.That(result.Kind, Is.EqualTo(KernelRuntimeServiceResolutionKind.MissingRequired));
            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.HasDiagnostic, Is.True);
            Assert.That(result.Diagnostic!.Code.Value, Is.EqualTo(KernelRuntimeServiceGraphCodes.ServiceRequiredMissing));
            Assert.That(result.Diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            AssertPayloadEntry(result.Diagnostic, "RequestedServiceIdentity", ServiceIdentity(99).ToString());
            AssertPayloadEntry(result.Diagnostic, "RequestingSlotIndex", "0");
        }

        [Test]
        public void ResolveOptionalService_ReturnsAbsentInfo_WhenDisableContributionApplies()
        {
            KernelRuntimeServiceGraph graph = CreatePlanGraph(KernelProfileKind.Development);

            KernelRuntimeServiceResolutionResult result = graph.ResolveOptionalService(
                graph.ServiceSlots[0],
                ServiceIdentity(99),
                OptionalDependencyAbsenceBehavior.DisableContribution);

            Assert.That(result.Kind, Is.EqualTo(KernelRuntimeServiceResolutionKind.OptionalAbsent));
            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.HasDiagnostic, Is.True);
            Assert.That(result.Diagnostic!.Code.Value, Is.EqualTo(KernelRuntimeServiceGraphCodes.ServiceOptionalAbsent));
            Assert.That(result.Diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Info));
            AssertPayloadEntry(result.Diagnostic, "AbsenceBehavior", OptionalDependencyAbsenceBehavior.DisableContribution.ToString());
        }

        [Test]
        public void ResolveOptionalService_UsesExplicitAlternative_WhenAlternativeIsCompatible()
        {
            KernelRuntimeServiceGraph graph = CreatePlanGraph(KernelProfileKind.Release);

            KernelRuntimeServiceResolutionResult result = graph.ResolveOptionalService(
                graph.ServiceSlots[0],
                ServiceIdentity(99),
                OptionalDependencyAbsenceBehavior.UseExplicitAlternative,
                alternativeServiceIdentity: ServiceIdentity(21));

            Assert.That(result.Kind, Is.EqualTo(KernelRuntimeServiceResolutionKind.OptionalAlternativeResolved));
            Assert.That(result.IsResolved, Is.True);
            Assert.That(result.HasResolvedServiceSlot, Is.True);
            Assert.That(result.ResolvedServiceSlot!.Value.ServiceIdentity, Is.EqualTo(ServiceIdentity(21)));
            Assert.That(result.AlternativeServiceIdentity, Is.EqualTo(ServiceIdentity(21)));
            Assert.That(result.Diagnostic, Is.Null);
        }

        [Test]
        public void ResolveOptionalService_RejectsExplicitAlternative_WhenAlternativeIsMissing()
        {
            KernelRuntimeServiceGraph graph = CreatePlanGraph(KernelProfileKind.Release);

            KernelRuntimeServiceResolutionResult result = graph.ResolveOptionalService(
                graph.ServiceSlots[0],
                ServiceIdentity(99),
                OptionalDependencyAbsenceBehavior.UseExplicitAlternative,
                alternativeServiceIdentity: ServiceIdentity(77));

            Assert.That(result.Kind, Is.EqualTo(KernelRuntimeServiceResolutionKind.Rejected));
            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.HasDiagnostic, Is.True);
            Assert.That(result.Diagnostic!.Code.Value, Is.EqualTo(KernelRuntimeServiceGraphCodes.ServiceOptionalAlternativeMissing));
            Assert.That(result.Diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            AssertPayloadEntry(result.Diagnostic, "AlternativeServiceIdentity", ServiceIdentity(77).ToString());
        }

        [Test]
        public void ResolveOptionalService_RejectsExplicitAlternative_WhenAlternativeIsIncompatible()
        {
            KernelRuntimeServiceGraph graph = new KernelRuntimeServiceGraph(CreateServiceGraphPlan(new[]
            {
                CreateService(11, ServiceFactoryKind.GeneratedFactory, "GeneratedService11", ServiceLifetimeKind.Singleton, ServiceCardinalityKind.SingletonGlobal),
                CreateService(21, ServiceFactoryKind.ProvidedInstance, "ProvidedService21", ServiceLifetimeKind.Project, ServiceCardinalityKind.OnePerScene),
            }), KernelProfileKind.Release);

            KernelRuntimeServiceResolutionResult result = graph.ResolveOptionalService(
                graph.ServiceSlots[0],
                ServiceIdentity(99),
                OptionalDependencyAbsenceBehavior.UseExplicitAlternative,
                alternativeServiceIdentity: ServiceIdentity(21));

            Assert.That(result.Kind, Is.EqualTo(KernelRuntimeServiceResolutionKind.Rejected));
            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.HasDiagnostic, Is.True);
            Assert.That(result.Diagnostic!.Code.Value, Is.EqualTo(KernelRuntimeServiceGraphCodes.ServiceOptionalAlternativeIncompatible));
            Assert.That(result.Diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            AssertPayloadEntry(result.Diagnostic, "AlternativeServiceIdentity", ServiceIdentity(21).ToString());
        }

        [Test]
        public void ResolveOptionalService_RejectsProfileSpecificError_WhenSelectedProfileMatchesBoundary()
        {
            KernelRuntimeServiceGraph graph = CreatePlanGraph(KernelProfileKind.Release);

            KernelRuntimeServiceResolutionResult result = graph.ResolveOptionalService(
                graph.ServiceSlots[0],
                ServiceIdentity(99),
                OptionalDependencyAbsenceBehavior.ProfileSpecificError,
                KernelProfileMask.Release);

            Assert.That(result.Kind, Is.EqualTo(KernelRuntimeServiceResolutionKind.Rejected));
            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.HasDiagnostic, Is.True);
            Assert.That(result.Diagnostic!.Code.Value, Is.EqualTo(KernelRuntimeServiceGraphCodes.ServiceOptionalProfileError));
            Assert.That(result.Diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            AssertPayloadEntry(result.Diagnostic, "ProfileSpecificErrorProfiles", KernelProfileMask.Release.ToString());
        }

        [Test]
        public void ResolveOptionalService_RejectsProfileSpecificError_WhenSelectedProfileIsMissing()
        {
            KernelRuntimeServiceGraph graph = CreatePlanGraph(null);

            KernelRuntimeServiceResolutionResult result = graph.ResolveOptionalService(
                graph.ServiceSlots[0],
                ServiceIdentity(99),
                OptionalDependencyAbsenceBehavior.ProfileSpecificError,
                KernelProfileMask.Release);

            Assert.That(result.Kind, Is.EqualTo(KernelRuntimeServiceResolutionKind.Rejected));
            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.HasDiagnostic, Is.True);
            Assert.That(result.Diagnostic!.Code.Value, Is.EqualTo(KernelRuntimeServiceGraphCodes.ServiceOptionalProfileMissing));
            Assert.That(result.Diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
        }

        [Test]
        public void ResolveOptionalService_RejectsProfileSpecificError_WhenProfileBoundaryIsMissing()
        {
            KernelRuntimeServiceGraph graph = CreatePlanGraph(KernelProfileKind.Release);

            KernelRuntimeServiceResolutionResult result = graph.ResolveOptionalService(
                graph.ServiceSlots[0],
                ServiceIdentity(99),
                OptionalDependencyAbsenceBehavior.ProfileSpecificError);

            Assert.That(result.Kind, Is.EqualTo(KernelRuntimeServiceResolutionKind.Rejected));
            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.HasDiagnostic, Is.True);
            Assert.That(result.Diagnostic!.Code.Value, Is.EqualTo(KernelRuntimeServiceGraphCodes.ServiceOptionalProfileBoundaryMissing));
            Assert.That(result.Diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
        }

        [Test]
        public void Constructor_RejectsDuplicateServiceIdentitiesRegardlessOfOrder()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => new KernelRuntimeServiceGraph(CreateServiceGraphPlan(new[]
            {
                CreateService(21, ServiceFactoryKind.GeneratedFactory, "Service21A"),
                CreateService(11, ServiceFactoryKind.GeneratedFactory, "Service11"),
                CreateService(21, ServiceFactoryKind.GeneratedFactory, "Service21B"),
            })));

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Does.Contain("duplicate service identities"));
        }

        [Test]
        public void MissingSlotDiagnostic_UsesServiceGraphDomainAndIdentityContext()
        {
            KernelRuntimeServiceGraph graph = new KernelRuntimeServiceGraph(CreateServiceGraphPlan(new[]
            {
                CreateService(11, ServiceFactoryKind.GeneratedFactory, "Service11"),
            }));

            KernelDiagnostic diagnostic = graph.CreateMissingServiceSlotDiagnostic(ServiceIdentity(99));

            Assert.That(diagnostic.Code.Value, Is.EqualTo(KernelRuntimeServiceGraphCodes.ServiceSlotMissing));
            Assert.That(diagnostic.Domain, Is.EqualTo(DiagnosticDomain.ServiceGraph));
            Assert.That(diagnostic.FailureBoundary, Is.EqualTo(DiagnosticFailureBoundary.Kernel));
            Assert.That(diagnostic.Context.RuntimeIdentities, Has.Count.EqualTo(1));
            Assert.That(diagnostic.Context.RuntimeIdentities[0], Is.EqualTo(ServiceIdentity(99)));
        }

        static RuntimeIdentityRef ServiceIdentity(int value)
        {
            return new RuntimeIdentityRef(RuntimeIdentityKind.Service, value);
        }

        static KernelRuntimeServiceGraph CreatePlanGraph(KernelProfileKind? selectedProfileKind)
        {
            return new KernelRuntimeServiceGraph(CreateServiceGraphPlan(new[]
            {
                CreateService(11, ServiceFactoryKind.GeneratedFactory, "GeneratedService11"),
                CreateService(21, ServiceFactoryKind.ProvidedInstance, "ProvidedService21"),
            }), selectedProfileKind);
        }

        static ServiceGraphPlan CreateServiceGraphPlan(ServiceIR[] services)
        {
            Hash128 generatedHash = KernelProjectionHashing.ComputeServiceGraphHash(services);

            VerifiedArtifactHeader header = new VerifiedArtifactHeader(
                new PlanId(31),
                new ArtifactSetId(11),
                new ArtifactId(1),
                ArtifactKind.ServiceGraph,
                11,
                new Hash128(1, 2, 3, 4),
                new Hash128(5, 6, 7, 8),
                new Hash128(9, 9, 9, 9),
                new Hash128(6, 6, 6, 6),
                generatedHash,
                "KernelRuntimeServiceGraphTests");

            return new ServiceGraphPlan(header, services);
        }

        static ServiceIR CreateService(
            int serviceId,
            ServiceFactoryKind factoryKind,
            string serviceName,
            ServiceLifetimeKind lifetime = ServiceLifetimeKind.Singleton,
            ServiceCardinalityKind cardinality = ServiceCardinalityKind.SingletonGlobal)
        {
            return new ServiceIR(
                new ServiceId(serviceId),
                serviceName,
                lifetime,
                new ModuleId(10),
                new[] { new ServiceContractIR("IService" + serviceId, new SourceLocationId(serviceId)) },
                Array.Empty<ServiceDependencyIR>(),
                factoryKind,
                new SourceLocationId(serviceId),
                cardinality);
        }

        static void AssertPayloadEntry(KernelDiagnostic diagnostic, string key, string expectedValue)
        {
            for (int index = 0; index < diagnostic.Payload.Entries.Count; index++)
            {
                if (diagnostic.Payload.Entries[index].Key != key)
                    continue;

                Assert.That(diagnostic.Payload.Entries[index].Value.ToString(), Is.EqualTo(expectedValue), key);
                return;
            }

            Assert.Fail("Missing payload entry: " + key);
        }
    }
}