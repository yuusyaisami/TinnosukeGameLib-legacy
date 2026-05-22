# Kernel v2.1 Proof Anchor Catalog

## Document Status

- Document ID: KernelV21ProofAnchorCatalog
- Status: Draft
- Role: proof-family catalog that separates live-boot proof, direct-play reference proof, representative gameplay proof, and residue-hardening proof for V21-M0
- Depends on:
  - [../00_KernelV21MigrationOverviewSpec.md](../00_KernelV21MigrationOverviewSpec.md)
  - [../01_WaveABootAndSceneEntryCutoverSpec.md](../01_WaveABootAndSceneEntryCutoverSpec.md)
  - [../05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../05_WaveERepresentativeGameplaySystemsCutoverSpec.md)
  - [../06_WaveFLegacyRemovalAndHardeningSpec.md](../06_WaveFLegacyRemovalAndHardeningSpec.md)
  - [../07_KernelV21MigrationMilestoneOrderSpec.md](../07_KernelV21MigrationMilestoneOrderSpec.md)
  - [../../v2/11_DebugMapAndDiagnosticsSpec.md](../../v2/11_DebugMapAndDiagnosticsSpec.md)
  - [../../v2/12_UnityAuthoringBridgeSpec.md](../../v2/12_UnityAuthoringBridgeSpec.md)
  - [../../v2/13_LegacyCompatBoundarySpec.md](../../v2/13_LegacyCompatBoundarySpec.md)
  - [../../v2/15_TestAndValidationSpec.md](../../v2/15_TestAndValidationSpec.md)
  - [../../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md](../../v2/17_AssemblyDefinitionAndCompileBoundarySpec.md)
- Provides foundation for:
  - [../01_WaveABootAndSceneEntryCutoverSpec.md](../01_WaveABootAndSceneEntryCutoverSpec.md)
  - [../05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../05_WaveERepresentativeGameplaySystemsCutoverSpec.md)
  - [../06_WaveFLegacyRemovalAndHardeningSpec.md](../06_WaveFLegacyRemovalAndHardeningSpec.md)

### Revision Note

This document records the proof-anchor set required by V21-M0.

Its primary job is not to claim that every proof family already has full executable coverage.
Its job is to stop the project from treating distinct proof families as interchangeable.

---

## Purpose

The purpose of this catalog is to separate proof families before later milestones are claimed.

The minimum separation required by V21-M0 is:

- live boot
- direct-play reference
- representative gameplay
- residue hardening

If one family is cited as proof for another family, the milestone claim is invalid.

---

## Scope

This catalog records:

- current proof anchors that already exist in docs, sources, scenes, tests, or asmdef gates
- which milestone consumes each anchor first
- what each anchor proves
- what each anchor explicitly does not prove

This catalog does not redefine the acceptance criteria owned by the wave specs.
It exists to keep later claim review falsifiable.

---

## Proof Family Rules

1. `Live boot` evidence proves the accepted playable live path and scene-entry authority, not merely verified-input tooling.
2. `Direct-play reference` evidence proves a verified-input reference path only.
3. `Representative gameplay` evidence proves GameScene-facing migrated consumption only.
4. `Residue hardening` evidence proves bounded legacy residue, compile-boundary enforcement, and forbidden-pattern gates only.
5. A proof anchor may support more than one discussion, but it may have only one primary proof-family classification in this catalog.

---

## Proof Anchor Catalog

| Anchor ID | Proof family | Evidence form | Current anchor | Primary consumer | Proves | Does not prove |
| --- | --- | --- | --- | --- | --- | --- |
| V21-PA-LIVE-001 | Live boot | Contract document | [01_WaveABootAndSceneEntryCutoverSpec.md](../01_WaveABootAndSceneEntryCutoverSpec.md); [00_KernelV21MigrationOverviewSpec.md](../00_KernelV21MigrationOverviewSpec.md) | V21-M1 | the accepted live-entry contract, mixed-authority failures, and non-completion rules already exist as reviewable targets | it is not executable proof that the live path has already migrated |
| V21-PA-LIVE-002 | Live boot | Representative scene anchor | [TitleScene.unity](../../../Scenes/TitleScene.unity); [GameScene.unity](../../../Scenes/GameScene.unity) | V21-M1 | the concrete scenes that live-entry proof must exercise are fixed before later claims begin | a scene anchor by itself does not prove that boot or transition authority has migrated |
| V21-PA-DIRECT-001 | Direct-play reference | Source anchor | [AuthoringBridge.cs](../../../Editor/KernelBoot/AuthoringBridge.cs) | V21-M0 and V21-M1 | the repository already contains a verified-input direct-play preparation path | it does not prove that the playable live game uses that path |
| V21-PA-DIRECT-002 | Direct-play reference | Executable reference | [AuthoringBridgeDirectPlayTests.cs](../../../Editor/Tests/KernelBoot/AuthoringBridgeDirectPlayTests.cs) | V21-M0 and V21-M1 | direct-play preparation, verified generation, boot boundary handoff, and central diagnostics fan-out are executable today | it does not prove live boot, representative gameplay, or residue removal |
| V21-PA-GAME-001 | Representative gameplay | Contract document and scene anchor | [05_WaveERepresentativeGameplaySystemsCutoverSpec.md](../05_WaveERepresentativeGameplaySystemsCutoverSpec.md); [GameScene.unity](../../../Scenes/GameScene.unity); [Scenes/GameScene/GameScene.unity](../../../Scenes/GameScene/GameScene.unity) | V21-M5 | the real GameScene bundle that later gameplay proof must use is fixed before gameplay migration is claimed | it does not prove that representative gameplay already consumes migrated authority |
| V21-PA-GAME-002 | Representative gameplay | Trace substrate | [CommandExecutionTrace.cs](../../../GameLib/Script/Common/Commands/VNext/Core/CommandExecutionTrace.cs); [DynamicRuntimeLogUtility.cs](../../../GameLib/Script/Common/Variables/Dynamic/Core/DynamicRuntimeLogUtility.cs) | V21-M5 | the repository already has trace-capable substrates that can attribute gameplay flows to runtime context | substrate existence alone is not proof that any representative gameplay slice is migrated |
| V21-PA-RES-001 | Residue hardening | Executable gate | [LegacyCompatBoundaryTests.cs](../../../Editor/Tests/LegacyCompatBoundaryTests.cs) | V21-M0 and V21-M6 | runtime-capable adapters, profile bounds, removal policy metadata, and release-forbidden residue already have executable checks | it does not prove representative gameplay or live-boot migration |
| V21-PA-RES-002 | Residue hardening | Runtime report model | [LegacyMigrationModel.cs](../../../GameLib/Script/Kernel/Validation/LegacyMigrationModel.cs) | V21-M6 | residue can already be described with adapter kind, target replacement, removal condition, and blocking metadata | a report model alone does not fail the build or prove residue has been removed |
| V21-PA-RES-003 | Residue hardening | Static gate | [KernelForbiddenPatternScanner.cs](../../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternScanner.cs); [KernelForbiddenPatternTests.cs](../../../Editor/Tests/KernelDiagnostics/KernelForbiddenPatternTests.cs); [KernelDebugGateTests.cs](../../../Editor/Tests/KernelDiagnostics/KernelDebugGateTests.cs) | V21-M0 and V21-M6 | forbidden discovery, fallback, and direct-logging patterns already have scanner infrastructure and tests | static pattern coverage alone does not prove a migrated live path |
| V21-PA-RES-004 | Residue hardening | Upstream documentation gate | [KernelArchitectureDocTests.cs](../../../Editor/Tests/KernelArchitectureDocTests.cs) | V21-M0 | the upstream v2 M0 and related doc artifacts are already fixed by automated checks | it does not validate any v2.1-specific baseline-freeze document |
| V21-PA-RES-005 | Residue hardening | Compile-boundary executable gate | [KernelDiagnosticsAsmdefBoundaryTests.cs](../../../Editor/Tests/KernelDiagnostics/KernelDiagnosticsAsmdefBoundaryTests.cs); [GameLib.Kernel.Diagnostics.asmdef](../../../GameLib/Script/Kernel/Diagnostics/Core/GameLib.Kernel.Diagnostics.asmdef); [GameLib.Kernel.Validation.asmdef](../../../GameLib/Script/Kernel/Validation/GameLib.Kernel.Validation.asmdef); [GameLib.Kernel.Boot.asmdef](../../../GameLib/Script/Kernel/Boot/GameLib.Kernel.Boot.asmdef) | V21-M0 and V21-M6 | the current partial kernel or test asmdef split is auditable and executable already | it does not prove that all common legacy or gameplay residue is already quarantined |

---

## Execution Harness Note

[Tools/Run-UnityTests.ps1](../../../Tools/Run-UnityTests.ps1) is the current batch wrapper for focused Unity EditMode or PlayMode verification.
It is execution infrastructure, not one of the proof families above.

---

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V21-PA-01 | Confirm the four proof families are separated explicitly. | Purpose and Proof Family Rules must distinguish live boot, direct-play reference, representative gameplay, and residue hardening. |
| TC-V21-PA-02 | Confirm direct play is cataloged as a reference path rather than live migration proof. | The catalog must contain AuthoringBridgeDirectPlayTests.cs under Direct-play reference and state what it does not prove. |
| TC-V21-PA-03 | Confirm representative gameplay proof is anchored to the real GameScene bundle. | The catalog must contain Wave E and the GameScene anchors under Representative gameplay. |
| TC-V21-PA-04 | Confirm residue hardening already has executable anchors. | The catalog must contain LegacyCompatBoundaryTests.cs, KernelForbiddenPatternScanner.cs, and KernelDiagnosticsAsmdefBoundaryTests.cs under Residue hardening. |
| TC-V21-PA-05 | Confirm compile-boundary evidence is treated as residue hardening rather than gameplay proof. | The asmdef boundary row must be classified under Residue hardening and must state what it does not prove. |
| TC-V21-PA-06 | Confirm the catalog distinguishes proof anchors from execution harness infrastructure. | The catalog must mention Tools/Run-UnityTests.ps1 separately from the proof-family table. |
