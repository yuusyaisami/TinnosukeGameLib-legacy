#nullable enable

using System;
using System.Collections.Generic;
using Game.Kernel.Authoring;
using Game.Kernel.Contributions;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using NUnit.Framework;
using TinnosukeGameLib.Editor.KernelBoot;

using KernelHash128 = Game.Kernel.IR.Hash128;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class ShippedGameplayVerificationTests
    {
        const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        const string GameScenePath = "Assets/Scenes/GameScene.unity";

        [Test]
        public void BuildReport_BlocksWhenInventoryGateIsOpen()
        {
            SceneAssetMigrationReport migrationReport = CreateCleanMigrationReport();
            ShippedGameplayInventoryGateSnapshot gate = new ShippedGameplayInventoryGateSnapshot(
                serviceTodoCount: 1,
                commandTodoCount: 2,
                valueBoundaryDebtCount: 0,
                dynamicTodoCount: 0,
                hasSummaryParseFailure: false);

            ShippedGameplayVerificationReport report = ShippedGameplayVerificationService.BuildReport(migrationReport, gate);
            AuthoringValidationReport validation = ShippedGameplayVerificationService.Validate(report);

            Assert.That(report.EntryGate.IsClosed, Is.False);
            Assert.That(report.IsExecutable, Is.False);
            Assert.That(report.UnresolvedItemCount, Is.EqualTo(2));
            Assert.That(report.Targets, Has.Count.EqualTo(2));
            Assert.That(report.Targets[0].Status, Is.EqualTo(ShippedGameplayProofStatus.Blocked));
            Assert.That(report.Targets[1].Status, Is.EqualTo(ShippedGameplayProofStatus.Blocked));
            Assert.That(ContainsIssue(validation, ShippedGameplayVerificationCodes.InventoryGateBlocked), Is.True);
            Assert.That(ContainsIssue(validation, ShippedGameplayVerificationCodes.InventoryGateServiceTodo), Is.True);
            Assert.That(ContainsIssue(validation, ShippedGameplayVerificationCodes.InventoryGateCommandTodo), Is.True);
        }

        [Test]
        public void BuildReport_BlocksSceneWhenMigrationContainsLegacyAnchors()
        {
            SceneAssetMigrationReport migrationReport = new SceneAssetMigrationReport(
                new[]
                {
                    CreateSceneRecord(TitleScenePath, missingRequiredAnchorCount: 0, legacyAnchorCount: 1, hasRoots: true),
                    CreateSceneRecord(GameScenePath, missingRequiredAnchorCount: 0, legacyAnchorCount: 0, hasRoots: true),
                },
                System.Array.Empty<string>());

            ShippedGameplayInventoryGateSnapshot gate = new ShippedGameplayInventoryGateSnapshot(0, 0, 0, 0, false);
            ShippedGameplayVerificationReport report = ShippedGameplayVerificationService.BuildReport(migrationReport, gate);
            AuthoringValidationReport validation = ShippedGameplayVerificationService.Validate(report);

            Assert.That(report.EntryGate.IsClosed, Is.True);
            Assert.That(report.IsExecutable, Is.False);
            Assert.That(report.UnresolvedItemCount, Is.EqualTo(1));
            Assert.That(report.Targets[0].AssetPath, Is.EqualTo(GameScenePath));
            Assert.That(report.Targets[0].Status, Is.EqualTo(ShippedGameplayProofStatus.Ready));
            Assert.That(report.Targets[1].AssetPath, Is.EqualTo(TitleScenePath));
            Assert.That(report.Targets[1].Status, Is.EqualTo(ShippedGameplayProofStatus.Blocked));
            Assert.That(report.Targets[1].LegacyAnchorCount, Is.EqualTo(1));
            Assert.That(ContainsIssue(validation, ShippedGameplayVerificationCodes.SceneLegacyAnchorPresent), Is.True);
        }

        [Test]
        public void BuildReport_IsExecutableWhenEntryGateAndMigrationAreClean()
        {
            SceneAssetMigrationReport migrationReport = CreateCleanMigrationReport();
            ShippedGameplayInventoryGateSnapshot gate = new ShippedGameplayInventoryGateSnapshot(0, 0, 0, 0, false);

            ShippedGameplayVerificationReport report = ShippedGameplayVerificationService.BuildReport(migrationReport, gate);
            AuthoringValidationReport validation = ShippedGameplayVerificationService.Validate(report);

            Assert.That(report.EntryGate.IsClosed, Is.True);
            Assert.That(report.UnresolvedItemCount, Is.EqualTo(0));
            Assert.That(report.IsExecutable, Is.True);
            Assert.That(report.IsVerified, Is.False);
            Assert.That(report.Targets, Has.Count.EqualTo(2));
            Assert.That(report.Targets[0].Status, Is.EqualTo(ShippedGameplayProofStatus.Ready));
            Assert.That(report.Targets[1].Status, Is.EqualTo(ShippedGameplayProofStatus.Ready));
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void BuildReport_IsVerifiedWhenSuccessfulDirectPlayProofsCoverAllScenes()
        {
            SceneAssetMigrationReport migrationReport = CreateCleanMigrationReport();
            ShippedGameplayInventoryGateSnapshot gate = new ShippedGameplayInventoryGateSnapshot(0, 0, 0, 0, false);
            ShippedGameplayDirectPlayProofRecord[] proofs =
            {
                CreateSuccessfulDirectPlayProof(TitleScenePath),
                CreateSuccessfulDirectPlayProof(GameScenePath),
            };

            ShippedGameplayVerificationReport report = ShippedGameplayVerificationService.BuildReport(migrationReport, gate, proofs);
            AuthoringValidationReport validation = ShippedGameplayVerificationService.Validate(report);

            Assert.That(report.IsExecutable, Is.True);
            Assert.That(report.IsVerified, Is.True);
            Assert.That(report.DirectPlayProofs, Has.Count.EqualTo(2));
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void SummarizeDirectPlayProof_ReportsFailedNormalizationAndInvalidatesVerification()
        {
            AuthoringDirectPlayResult failedResult = CreateFailedNormalizationDirectPlayResult();
            ShippedGameplayDirectPlayProofRecord failedProof = ShippedGameplayVerificationService.SummarizeDirectPlayProof(TitleScenePath, failedResult);
            SceneAssetMigrationReport migrationReport = CreateCleanMigrationReport();
            ShippedGameplayInventoryGateSnapshot gate = new ShippedGameplayInventoryGateSnapshot(0, 0, 0, 0, false);
            ShippedGameplayVerificationReport report = ShippedGameplayVerificationService.BuildReport(
                migrationReport,
                gate,
                new[]
                {
                    failedProof,
                    CreateSuccessfulDirectPlayProof(GameScenePath),
                });
            AuthoringValidationReport validation = ShippedGameplayVerificationService.Validate(report);

            Assert.That(failedProof.Status, Is.EqualTo(ShippedGameplayDirectPlayProofStatus.Failed));
            Assert.That(failedProof.FailedStage, Is.EqualTo(AuthoringDirectPlayStage.Normalization));
            Assert.That(failedProof.DiagnosticCount, Is.EqualTo(1));
            Assert.That(failedProof.ErrorCount, Is.EqualTo(1));
            Assert.That(failedProof.BlockingCodes, Has.Member(ShippedGameplayVerificationCodes.DirectPlayProofFailed));
            Assert.That(report.IsExecutable, Is.True);
            Assert.That(report.IsVerified, Is.False);
            Assert.That(report.UnresolvedItemCount, Is.EqualTo(1));
            Assert.That(ContainsIssue(validation, ShippedGameplayVerificationCodes.DirectPlayProofFailed), Is.True);
        }

        static SceneAssetMigrationReport CreateCleanMigrationReport()
        {
            return new SceneAssetMigrationReport(
                new[]
                {
                    CreateSceneRecord(TitleScenePath, missingRequiredAnchorCount: 0, legacyAnchorCount: 0, hasRoots: true),
                    CreateSceneRecord(GameScenePath, missingRequiredAnchorCount: 0, legacyAnchorCount: 0, hasRoots: true),
                },
                System.Array.Empty<string>());
        }

        static SceneAssetMigrationAssetRecord CreateSceneRecord(string assetPath, int missingRequiredAnchorCount, int legacyAnchorCount, bool hasRoots)
        {
            SceneAssetMigrationTarget target = new SceneAssetMigrationTarget(
                SceneAssetMigrationAssetKind.Scene,
                assetPath,
                "test-guid",
                new[] { typeof(EntityIdentityMB).FullName! },
                new[] { "Game.Commands.CommandRunnerMB" });

            List<string> missingRequired = new List<string>(missingRequiredAnchorCount);
            for (int index = 0; index < missingRequiredAnchorCount; index++)
                missingRequired.Add("Missing.Anchor." + index);

            List<SceneAssetMigrationAnchorRecord> requiredAnchors = new List<SceneAssetMigrationAnchorRecord>();
            if (hasRoots)
            {
                requiredAnchors.Add(new SceneAssetMigrationAnchorRecord(
                    typeof(EntityIdentityMB).FullName!,
                    "Root",
                    CreateSceneSourceLocation(assetPath, 1000)));
            }

            List<SceneAssetMigrationAnchorRecord> legacyAnchors = new List<SceneAssetMigrationAnchorRecord>(legacyAnchorCount);
            for (int index = 0; index < legacyAnchorCount; index++)
            {
                legacyAnchors.Add(new SceneAssetMigrationAnchorRecord(
                    "Game.Commands.CommandRunnerMB",
                    "Root/Legacy" + index,
                    CreateSceneSourceLocation(assetPath, 2000 + index)));
            }

            return new SceneAssetMigrationAssetRecord(target, requiredAnchors, legacyAnchors, missingRequired, hasRoots);
        }

        static ShippedGameplayDirectPlayProofRecord CreateSuccessfulDirectPlayProof(string assetPath)
        {
            return new ShippedGameplayDirectPlayProofRecord(
                assetPath,
                AuthoringDirectPlayStage.None,
                diagnosticCount: 0,
                warningCount: 0,
                errorCount: 0,
                fatalCount: 0,
                wasTruncated: false,
                ShippedGameplayDirectPlayProofStatus.Succeeded,
                Array.Empty<string>());
        }

        static AuthoringDirectPlayResult CreateFailedNormalizationDirectPlayResult()
        {
            KernelIR kernelIR = CreateKernelIR();
            AuthoringDirectPlayInput input = new AuthoringDirectPlayInput(
                Array.Empty<ScopeAuthoringRoot>(),
                kernelIR,
                new KernelProfile(new KernelProfileId(7), KernelProfileKind.Development),
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                new ManifestId(303),
                new BootPolicyId(404));

            ScopeAuthoringExtractionReport extractionReport = new ScopeAuthoringExtractionReport(
                Array.Empty<ModuleContributionData>(),
                Array.Empty<EntityAuthoringInput>(),
                Array.Empty<EntityDeclarationPlanInput>(),
                Array.Empty<EntityServiceDeclarationInput>(),
                Array.Empty<CommandDeclarationInput>(),
                Array.Empty<AuthoringValidationIssue>());

            KernelIRNormalizationReport normalizationReport = new KernelIRNormalizationReport(
                new KernelHash128(1, 2, 3, 4),
                new KernelHash128(4, 3, 2, 1),
                new KernelHash128(5, 6, 7, 8),
                new KernelHash128(8, 7, 6, 5));

            DependencyValidationReport dependencyValidationReport = new DependencyValidationReport(input.Profile.Kind.ToString(), Array.Empty<DependencyValidationIssue>());

            return new AuthoringDirectPlayResult(
                input,
                kernelIR,
                AuthoringDirectPlayStage.Normalization,
                extractionReport,
                normalizationReport,
                dependencyValidationReport,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        static KernelIR CreateKernelIR()
        {
            SourceLocationTable sources = new SourceLocationTable(new[]
            {
                new SourceLocationIR(new GeneratedSourceLocation("PlanHeaderGenerator", "MinimalKernel", "Build")),
            });

            ModuleIR module = new ModuleIR(
                new ModuleId(1),
                "MinimalKernel",
                ModuleKind.Feature,
                new ModuleVersion(1),
                new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new SourceLocationId(1));

            return new KernelIR(
                new KernelIRHeader("KernelIR-Minimal", 1, "TinnosukeGameLib", "Release", "1.0.0", new KernelHash128(1, 2, 3, 4), new KernelHash128(5, 6, 7, 8)),
                new KernelProfileIR("Release", KernelProfileMask.Release, new AvailabilityIR(KernelProfileMask.Release, true, null)),
                new[] { module },
                Array.Empty<ScopeIR>(),
                Array.Empty<ServiceIR>(),
                Array.Empty<CommandIR>(),
                Array.Empty<ValueKeyIR>(),
                Array.Empty<LifecycleIR>(),
                Array.Empty<RuntimeQueryIR>(),
                Array.Empty<DependencyEdgeIR>(),
                sources);
        }

        static UnitySourceLocation CreateSceneSourceLocation(string assetPath, long localFileId)
        {
            return new UnitySourceLocation(
                UnityAuthoringSourceKind.SceneObject,
                "scene-guid",
                assetPath,
                localFileId,
                assetPath,
                "Root",
                "TestComponent",
                "TestComponent");
        }

        static bool ContainsIssue(AuthoringValidationReport report, string code)
        {
            for (int index = 0; index < report.Issues.Count; index++)
            {
                if (report.Issues[index].Code == code)
                    return true;
            }

            return false;
        }
    }
}