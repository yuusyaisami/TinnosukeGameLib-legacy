# GameLib Kernel v2.2 Docs

このフォルダには、v2.1 の migration completion をさらに押し切り、Release accepted path を Kernel-only runtime authority に置き換えるための v2.2 実装仕様を置きます。

v2.2 の役割は明確です。

- v2: target kernel の意味論、trust boundary、runtime subsystem の正規仕様
- v2.1: 現行ゲームを旧アーキテクチャから新アーキテクチャへ段階移行する migration 仕様
- v2.2: Release accepted path から legacy runtime authority を除去し、Kernel を唯一の実行権限にする completion 仕様

v2.2 は second kernel ではない。
v2 の意味論を再定義せず、v2.1 の baseline debt を入力として、kernel-only live host、command/value host removal、service census、service-family cutover、representative gameplay/application cutover、legacy deletion、release hardening、final proof aggregation を定義する。

- [00 Kernel v2.2 Completion Overview Specification](00_KernelV22CompletionOverviewSpec.md)
- [01 Kernel v2.2 Authority and Service Census Specification](01_KernelV22AuthorityAndServiceCensusSpec.md)
- [02 Kernel v2.2 Kernel-Only Host Specification](02_KernelV22KernelOnlyHostSpec.md)
- [02-1 Kernel v2.2 Command and Value Host Removal Specification](02_1_KernelV22CommandAndValueHostRemovalSpec.md)
- [03 Kernel v2.2 Service Family Cutover Specification](03_KernelV22ServiceFamilyCutoverSpec.md)
- [03-1 Kernel v2.2 Representative Gameplay and Application Cutover Specification](03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md)
- [04 Kernel v2.2 Legacy Deletion and Hardening Specification](04_KernelV22LegacyDeletionAndHardeningSpec.md)
- [04-1 Kernel v2.2 Full Proof and Release Hardening Specification](04_1_KernelV22FullProofAndReleaseHardeningSpec.md)
- [05 Kernel v2.2 Milestone Order Specification](05_KernelV22MilestoneOrderSpec.md)
- [06 Kernel v2.2 Implementation Plan](06_KernelV22ImplementationPlan.md)
- [Index / V22-M0 Completion Package](Index/README.md)

## Principles

- continuity contract として残すのは existing command payload meaning、existing DynamicValue authoring surface、generated value-key identity だけ
- ProjectLifetimeScope、GlobalLifetimeScope、SceneLifetimeScope、RuntimeLifetimeScope、RuntimeResolverHub は accepted runtime authority から外す
- CommandRunnerAuthoring と BlackboardAuthoring は declaration surface に限定し、CommandRunnerMB と BlackboardMB は delete target とする
- service census は KernelCoreAuthority、KernelManagedFeatureService、HubOwnedRuntimeObject、AuthoringOnlyMonoBehaviour、DeleteTarget の 5 分類で固定する
- mixed-boundary service は split 完了まで ServiceGraph eligibility を主張できない
- direct-play reference の成功は live acceptance や release hardening の代用にしない
- release acceptance は runtime-capable legacy adapter 0 件と legacy runtime authority 0 件を要求する
- release completion claim は validation、generation、runtime behavior、diagnostics、performance、static、legacy、integration の gate bundle を reviewable に束ねなければならない
- 実装着手時の変更順、work package、validation cadence は [06_KernelV22ImplementationPlan.md](06_KernelV22ImplementationPlan.md) を execution companion として従う

## Test Cases

| Test Case | Purpose | Execution Note |
| --- | --- | --- |
| TC-V22-README-01 | Confirm v2.2 is explicitly separated from both v2 semantics and v2.1 migration staging. | This file must describe the role split between v2, v2.1, and v2.2. |
| TC-V22-README-02 | Confirm the overview spec is exposed as the first v2.2 root document. | This file must link to 00_KernelV22CompletionOverviewSpec.md. |
| TC-V22-README-03 | Confirm the service census spec is part of the base package. | This file must link to 01_KernelV22AuthorityAndServiceCensusSpec.md. |
| TC-V22-README-04 | Confirm the kernel-only host spec is exposed. | This file must link to 02_KernelV22KernelOnlyHostSpec.md. |
| TC-V22-README-05 | Confirm the command/value host-removal spec is exposed. | This file must link to 02_1_KernelV22CommandAndValueHostRemovalSpec.md. |
| TC-V22-README-06 | Confirm the service-family cutover spec is exposed. | This file must link to 03_KernelV22ServiceFamilyCutoverSpec.md. |
| TC-V22-README-07 | Confirm the representative gameplay/application cutover spec is exposed. | This file must link to 03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md. |
| TC-V22-README-08 | Confirm the legacy deletion and hardening spec is exposed. | This file must link to 04_KernelV22LegacyDeletionAndHardeningSpec.md. |
| TC-V22-README-09 | Confirm the milestone-order spec is exposed. | This file must link to 05_KernelV22MilestoneOrderSpec.md. |
| TC-V22-README-10 | Confirm the V22-M0 package is exposed. | This file must link to Index/README.md. |
| TC-V22-README-11 | Confirm the continuity contract is intentionally narrow. | This file must mention command payload meaning, DynamicValue authoring, and generated value-key identity only. |
| TC-V22-README-12 | Confirm release acceptance is defined as kernel-only authority rather than bounded quarantine. | This file must require zero runtime-capable legacy authority on the release accepted path. |
| TC-V22-README-13 | Confirm the final proof-aggregation spec is exposed. | This file must link to 04_1_KernelV22FullProofAndReleaseHardeningSpec.md. |
| TC-V22-README-14 | Confirm the execution companion plan is exposed. | This file must link to 06_KernelV22ImplementationPlan.md. |