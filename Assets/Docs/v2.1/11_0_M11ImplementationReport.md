# Kernel v2.1 M11 実装レポート

## 文書ステータス

- 文書 ID: `11_0_M11ImplementationReport`
- 状態: Draft
- 役割: M11.1 から M11.5 までの実装状況、移行方針、現在の完成度、使い方、拡張方法、残存 legacy を具体ファイル付きで説明する
- 対象読者: 現在の v2.1 切り替え作業を引き継ぐ開発者、scene 移行担当、service / command / dynamic / value 側の実装担当
- 前提文書:
  - [05_ImplementationMilestoneSpec.md](05_ImplementationMilestoneSpec.md)
  - [08_FullReplacementCompletionSpec.md](08_FullReplacementCompletionSpec.md)
  - [09_ServiceCutoverMilestoneSpec.md](09_ServiceCutoverMilestoneSpec.md)
  - [10_CommandCutoverMilestoneSpec.md](10_CommandCutoverMilestoneSpec.md)
  - [11_DynamicSourceCutoverMilestoneSpec.md](11_DynamicSourceCutoverMilestoneSpec.md)
  - [11_4_SceneAssetMigrationSpec.md](11_4_SceneAssetMigrationSpec.md)
  - [Index/ServiceCutoverInventory.md](Index/ServiceCutoverInventory.md)
  - [Index/CommandCutoverInventory.md](Index/CommandCutoverInventory.md)
  - [Index/DynamicSourceCutoverInventory.md](Index/DynamicSourceCutoverInventory.md)
  - [Index/ValueScalarQueryInventory.md](Index/ValueScalarQueryInventory.md)
  - [Index/ScenePrefabInventory.md](Index/ScenePrefabInventory.md)

---

## 1. この文書が説明すること

この文書は「M11 で何を作ったか」を、コードと asset と検証の実体に沿って説明する。

M11 の本来の目的は、M6 から M10 までで作った新経路を代表例で終わらせず、実ゲームで使っている scene / command / value / dynamic / service 全体に広げることにある。

つまり M11 は、単に新クラスを追加するフェーズではない。次の 5 つを同時に進めるフェーズである。

1. 何がまだ legacy なのかを inventory で固定する
2. legacy host を target path から外し始める
3. scene / asset を fail-closed で移行できるようにする
4. shipped gameplay の検証経路を report 化する
5. 最終的に M12 で legacy を物理削除できる状態まで情報を揃える

---

## 2. 先に結論

現時点の M11 は、基盤と gate はかなり揃っているが、全体完了ではない。

実装状況を短く言うと次の通り。

- M11.1 は完了している。inventory と shipped asset baseline は固定済み。
- M11.2 は一部着手済み。`CommandRunnerMB` と `RuntimeManagerMB` は explicit install 化が進んだが、scene から完全には消えていない。
- M11.3 は未完了。`BlackboardMB`、`ActorSourceFastResolver`、scalar fallback、query helper、service 側の legacy authority がまだ残っている。
- M11.4 は report / validator / spec / tests が揃っているが、実際に scene を書き換える migration utility 本体はまだない。
- M11.5 は shipped gameplay verification report の基盤があり、direct-play proof 集約もある。ただし real TitleScene / GameScene harness はまだ薄い。

要するに、M11 は「実行基盤の準備」と「切替対象の可視化」はかなり進んでいるが、「すべての gameplay surface を new path のみへ切り替える」ところまでは到達していない。

---

## 3. M11 全体像

### 3.1 M11 の節構成

M11 は [05_ImplementationMilestoneSpec.md](05_ImplementationMilestoneSpec.md) で次の 5 つに分割されている。

1. M11.1 残存 inventory freeze
2. M11.2 service wave 全件消化
3. M11.3 command / value / scalar / query / dynamic source 残存接続切替
4. M11.4 existing scene / prefab migration
5. M11.5 shipped gameplay verification

### 3.2 実装の方針

今回の M11 実装で特に守っている方針は次の通り。

- 旧システムをいきなり消さず、先に authority を外す
- だが legacy host を新経路の truth に残さない
- scene migration は ad-hoc 手修正にせず、report と validator を先に作る
- verification は「たぶん動く」ではなく report と proof record にする
- inventory を先に固定し、どこまで終わっているかを数で見えるようにする

---

## 4. M11.1 で作ったもの

M11.1 の目的は「何を移行しなければならないかを、あとからごまかせない形で固定すること」だった。

### 4.1 作成済み inventory 文書

M11.1 の中心は次の 5 文書である。

- [Index/ServiceCutoverInventory.md](Index/ServiceCutoverInventory.md)
- [Index/CommandCutoverInventory.md](Index/CommandCutoverInventory.md)
- [Index/DynamicSourceCutoverInventory.md](Index/DynamicSourceCutoverInventory.md)
- [Index/ValueScalarQueryInventory.md](Index/ValueScalarQueryInventory.md)
- [Index/ScenePrefabInventory.md](Index/ScenePrefabInventory.md)

### 4.2 何を固定したか

#### service inventory

[ServiceCutoverInventory.md](Index/ServiceCutoverInventory.md) では 328 unique service surfaces を固定している。

現在の summary は次の通り。

- `置換済み`: 2
- `進行中`: 8
- `隔離/削除対象`: 10
- `要差し替え`: 308

つまり service wave は、まだ大半が残っている。M11.2 の main workload はここにある。

#### command inventory

[CommandCutoverInventory.md](Index/CommandCutoverInventory.md) では 558 unique command surfaces を固定している。

現在の summary は次の通り。

- `置換済み`: 23
- `進行中`: 1
- `隔離/削除対象`: 27
- `要差し替え`: 507

command 系は kernel-side IR と artifact はかなりあるが、gameplay 実体の多くはまだ旧経路のままである。

#### dynamic source inventory

[DynamicSourceCutoverInventory.md](Index/DynamicSourceCutoverInventory.md) では 236 unique dynamic surfaces を固定している。

現在の summary は次の通り。

- `置換済み`: 146
- `進行中`: 0
- `隔離/削除対象`: 12
- `要差し替え`: 78

dynamic は runtime substrate 自体はかなり new path 寄りだが、Blackboard 起源の source や editor support の一部、gameplay source 群がまだ残っている。

#### value / scalar / query inventory

[ValueScalarQueryInventory.md](Index/ValueScalarQueryInventory.md) は 5 boundary records を固定している。

現在の summary は次の通り。

- `進行中`: 3
- `隔離/削除対象`: 2

ここで重要なのは、`ValueStore` public contract と `RuntimeQuery` artifact は存在しているが、runtime truth から `Blackboard` や scalar fallback をまだ完全には抜けていないことを明文化した点である。

#### scene / prefab inventory

[ScenePrefabInventory.md](Index/ScenePrefabInventory.md) は shipped asset baseline を固定している。

現在の baseline は次の通り。

- scene: 2
  - `Assets/Scenes/TitleScene.unity`
  - `Assets/Scenes/GameScene.unity`
- prefab: 0

この baseline を先に固定したことで、M11.4 の対象 asset がぶれなくなった。

### 4.3 M11.1 の完成度

M11.1 は実質完了でよい。なぜなら、M11 の後続フェーズが参照する canonical baseline が既に存在し、M11.4 / M11.5 の report もこの baseline を前提に動いているからである。

---

## 5. M11.2 で何をやったか

M11.2 の目的は、service や host の legacy authority を 1 つずつ explicit path に落としていくことだった。

### 5.1 代表的に進んだもの: CommandRunnerMB

command host demotion の代表ファイルは次である。

- [Assets/GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs)
- [Assets/GameLib/Script/Project/LTS/ProjectLifetimeScope.cs](../GameLib/Script/Project/LTS/ProjectLifetimeScope.cs)
- [Assets/GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs](../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs)
- [Assets/Editor/Tests/CommandExecutorCatalogTests.cs](../Editor/Tests/CommandExecutorCatalogTests.cs)

#### CommandRunnerMB で何を変えたか

`CommandRunnerMB` は、以前の `IFeatureInstaller` による自動検出前提ではなく、`InstallRuntime(IRuntimeContainerBuilder builder, IScopeNode owner)` を明示的に呼ぶ形へ寄せた。

それに合わせて、`ProjectLifetimeScope` や `SceneLifetimeScope` 側が `GetComponent<CommandRunnerMB>()` して explicit install する形に寄せている。

これは「host component はまだ残るが、authority を feature scan から剥がす」という中間段階である。

#### どこまで終わっているか

- `CommandRunnerMB` の explicit install 化は進んでいる
- test も [CommandExecutorCatalogTests.cs](../Editor/Tests/CommandExecutorCatalogTests.cs) で新 API に揃えた
- ただし [Index/CommandCutoverInventory.md](Index/CommandCutoverInventory.md) では `CommandRunnerMB` はまだ `隔離/削除対象` のまま
- shipped scene から `CommandRunnerMB` はまだ消えていない

つまり、bridge は整理され始めたが、asset cutover はまだである。

### 5.2 代表的に進んだもの: RuntimeManagerMB

runtime manager 側で最近進んだ代表ファイルは次である。

- [Assets/GameLib/Script/Project/Scene/Runtime/RuntimeManager/RuntimeManagerMB.cs](../GameLib/Script/Project/Scene/Runtime/RuntimeManager/RuntimeManagerMB.cs)
- [Assets/GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs](../GameLib/Script/Project/Scene/LTS/SceneLifetimeScope.cs)

#### RuntimeManagerMB で何を変えたか

`RuntimeManagerMB` も `IFeatureInstaller` 経由ではなく、`InstallRuntime(IRuntimeContainerBuilder builder, IScopeNode owner)` を明示的に呼ぶ形へ変更した。

`SceneLifetimeScope` は現在、`CommandRunnerMB` に加えて `RuntimeManagerMB` も明示的に install する。

#### 重要な意味

これは `RuntimeManagerMB` を「まだ残っている legacy runtime host」から「明示 install される migration bridge」へ一段落としたことを意味する。

ただし中で登録しているものはまだ旧寄りである。実際に [RuntimeManagerMB.cs](../GameLib/Script/Project/Scene/Runtime/RuntimeManager/RuntimeManagerMB.cs) では次を引き続き登録している。

- `RuntimeLifetimeScopePool`
- `RuntimeLifetimeScopeSpawnerService`
- `IRuntimeLifetimeScopePoolTelemetry`
- `IFilteredReleaseSpawnerService`
- `IRuntimeLifetimeScopeSpawnerService`
- warmup callback

つまり authority の呼び出し方は少し改善されたが、authority の中身はまだ `SceneKernel` 側へ移っていない。

### 5.3 M11.2 の完成度

M11.2 は完了していない。

理由は明確で、[Index/ServiceCutoverInventory.md](Index/ServiceCutoverInventory.md) の 308 surface がまだ `要差し替え` だからである。

特に次はまだ強い migration debt である。

- [Assets/GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)
- [Assets/GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs](../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs)
- [Assets/GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs](../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs)
- [Assets/GameLib/Script/Common/Events/Core/EventService.cs](../GameLib/Script/Common/Events/Core/EventService.cs)
- [Assets/GameLib/Script/Common/Audio/AudioService.cs](../GameLib/Script/Common/Audio/AudioService.cs)
- [Assets/GameLib/Script/Common/Variables/Dynamic/RichText/RichTextRefService.cs](../GameLib/Script/Common/Variables/Dynamic/RichText/RichTextRefService.cs)

---

## 6. M11.3 で何をやったか、何が残っているか

M11.3 の本題は、command / value / scalar / query / dynamic source の残存接続を切り替えることだった。

ここは現時点で最も unfinished である。

### 6.1 command 側の現状

command 側で new path へ寄っている主なファイルは次である。

- [Assets/GameLib/Script/Common/Commands/VNext/Core/CommandRunnerService.cs](../GameLib/Script/Common/Commands/VNext/Core/CommandRunnerService.cs)
- [Assets/GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs](../GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs)
- [Assets/GameLib/Script/Common/Commands/VNext/Core/ProvisionalRunnerBridge.cs](../GameLib/Script/Common/Commands/VNext/Core/ProvisionalRunnerBridge.cs)
- [Assets/GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogService.cs](../GameLib/Script/Common/Commands/VNext/Catalog/CommandCatalogService.cs)
- [Assets/GameLib/Script/Common/Commands/VNext/Core/CommandExecutorRegistry.cs](../GameLib/Script/Common/Commands/VNext/Core/CommandExecutorRegistry.cs)

#### command 側で進んだ点

- `CommandRunnerService` は inventory 上 `進行中`
- command host の explicit install 化が進んだ
- test project build も整えた

#### command 側で残っている点

[CommandCutoverInventory.md](Index/CommandCutoverInventory.md) の summary 通り、507 surface が `要差し替え` である。

特に次はまだ legacy 側で残っている。

- `CommandCatalogService`
- `CommandExecutorRegistry`
- `CommandKeyResolver`
- `CommandChannelHubMB`
- `CommandListChannelHubMB`
- `CommandRunnerMB`

つまり command IR と runner shell はあるが、catalog / executor discovery / authoring / scene host 全体の切替はまだ終わっていない。

### 6.2 value 側の現状

value 側の核は次のファイルにある。

- [Assets/GameLib/Script/Kernel/Value/ValueStoreContracts.cs](../GameLib/Script/Kernel/Value/ValueStoreContracts.cs)
- [Assets/GameLib/Script/Common/Variables/VarStore/Core/VarStoreValueStoreBridge.cs](../GameLib/Script/Common/Variables/VarStore/Core/VarStoreValueStoreBridge.cs)
- [Assets/GameLib/Script/Common/Variables/VarStore/Core/SceneKernelValueStoreBoundary.cs](../GameLib/Script/Common/Variables/VarStore/Core/SceneKernelValueStoreBoundary.cs)
- [Assets/GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)
- [Assets/GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs](../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs)

#### value 側で進んだ点

- `ValueStore` public contract はある
- `VarStore` backend を new path に bridge する境界もある
- `SceneKernel` value boundary という考え方自体はコード上にある

#### value 側で残っている点

`BlackboardMB` がまだ `IFeatureInstaller` として残っている。さらに `BlackboardService` も runtime truth 側に深く残っている。

[BlackboardMB.cs](../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs) を見ると、いまも `InstallFeature` で `IBlackboardService` や `IGridBlackboardService` を scope kind ごとに登録している。

これは M11.3 的には未完了である。なぜなら、仕様上 `VarStore` は backend として残してよいが、`Blackboard` architecture は truth に残してはいけないからである。

### 6.3 scalar / query 側の現状

scalar / query migration debt を象徴しているファイルは次である。

- [Assets/GameLib/Script/Common/Variables/Scalar/Core/BaseScalarService.cs](../GameLib/Script/Common/Variables/Scalar/Core/BaseScalarService.cs)
- [Assets/GameLib/Script/Common/Variables/Scalar/MB/BaseScalarMB.cs](../GameLib/Script/Common/Variables/Scalar/MB/BaseScalarMB.cs)
- [Assets/GameLib/Script/Common/Commands/VNext/Core/ActorSourceFastResolver.cs](../GameLib/Script/Common/Commands/VNext/Core/ActorSourceFastResolver.cs)

`ActorSourceFastResolver` は、query / actor resolve がまだ old helper に依存している典型である。

[ActorSourceFastResolver.cs](../GameLib/Script/Common/Commands/VNext/Core/ActorSourceFastResolver.cs) では、いまも次のような legacy dependency が見える。

- `IBaseLifetimeScopeRegistry` fallback
- `current.Parent` のような親辿り
- `IScopeNode` ベースの resolve
- shared hub を親 scope に向かって探索する経路

つまり query / actor targeting は、まだ verified declaration / handle / explicit graph のみには閉じていない。

### 6.4 dynamic source 側の現状

dynamic は M11 の中では比較的進んでいる。しかし完了ではない。

核になるファイルは次である。

- [Assets/GameLib/Script/Common/Variables/Dynamic/Core/IDynamicSource.cs](../GameLib/Script/Common/Variables/Dynamic/Core/IDynamicSource.cs)
- [Assets/GameLib/Script/Common/Variables/Dynamic/Core/DynamicSources.cs](../GameLib/Script/Common/Variables/Dynamic/Core/DynamicSources.cs)
- [Assets/GameLib/Script/Common/Variables/Dynamic/Core/DynamicEvaluationRuntime.cs](../GameLib/Script/Common/Variables/Dynamic/Core/DynamicEvaluationRuntime.cs)
- [Assets/GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs](../GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs)
- [Assets/GameLib/Script/Common/Variables/Dynamic/Core/DynamicVariant.cs](../GameLib/Script/Common/Variables/Dynamic/Core/DynamicVariant.cs)
- [Assets/GameLib/Script/Common/Variables/Dynamic/RichText/RichTextSource.cs](../GameLib/Script/Common/Variables/Dynamic/RichText/RichTextSource.cs)
- [Assets/GameLib/Script/Common/_Editor/Dynamic/DynamicValueCompactDrawer.cs](../GameLib/Script/Common/_Editor/Dynamic/DynamicValueCompactDrawer.cs)
- [Assets/GameLib/Script/Common/_Editor/Dynamic/TypedDynamicValueDrawer.cs](../GameLib/Script/Common/_Editor/Dynamic/TypedDynamicValueDrawer.cs)

#### dynamic 側で進んだ点

[DynamicSourceCutoverInventory.md](Index/DynamicSourceCutoverInventory.md) では 146 surface が `置換済み` である。

つまり以下はかなり新経路に寄っている。

- `IDynamicSource`
- `DynamicEvaluationRuntime`
- `DynamicValue`
- `DynamicVariant`
- `RichTextSource`
- `DynamicCounterController`
- expression / rich-text AST 群

#### dynamic 側で残っている点

次はまだ quarantine か replacement debt である。

- `BlackboardSourceUtility`
- `DeferredDynamicVarValue`
- `DynamicValueResolver`
- `GridBlackboardSourceUtility`
- Blackboard 起源 source 群

つまり dynamic runtime 自体はかなりよいが、「dynamic がどこから値を取るか」の origin がまだ Blackboard 系に戻れる余地を残している。

### 6.5 M11.3 の完成度

M11.3 は未完了である。

理由は次の通り。

- `BlackboardMB` がまだ runtime truth 側にいる
- `BaseScalarService` fallback がまだ残っている
- `ActorSourceFastResolver` が explicit query contract に閉じていない
- command catalog / executor / host がまだ大きく legacy 依存
- dynamic source は substrate は強いが origin 側 debt が残る

---

## 7. M11.4 で作ったもの

M11.4 の方針は非常に重要だった。

scene migration を、手作業ベースの雰囲気修正ではなく、report と validator で fail-closed にする方針を先に固定した。

### 7.1 M11.4 の主要ファイル

M11.4 基盤の中心は次である。

- [11_4_SceneAssetMigrationSpec.md](11_4_SceneAssetMigrationSpec.md)
- [Assets/Editor/KernelBoot/SceneAssetMigrationModel.cs](../Editor/KernelBoot/SceneAssetMigrationModel.cs)
- [Assets/Editor/KernelBoot/SceneAssetMigrationReportService.cs](../Editor/KernelBoot/SceneAssetMigrationReportService.cs)
- [Assets/Editor/KernelBoot/SceneAssetMigrationValidationService.cs](../Editor/KernelBoot/SceneAssetMigrationValidationService.cs)
- [Assets/Editor/Tests/KernelBoot/SceneAssetMigrationValidationTests.cs](../Editor/Tests/KernelBoot/SceneAssetMigrationValidationTests.cs)
- [Index/ScenePrefabInventory.md](Index/ScenePrefabInventory.md)

### 7.2 何を作ったか

#### report model

[SceneAssetMigrationModel.cs](../Editor/KernelBoot/SceneAssetMigrationModel.cs) では、report を機械可読にするための型が定義されている。

主な役割は次の通り。

- validation code の固定
- asset kind 定義
- target 定義
- anchor record 定義
- asset record 定義
- aggregate report 定義

#### report builder

[SceneAssetMigrationReportService.cs](../Editor/KernelBoot/SceneAssetMigrationReportService.cs) は M11.4 の実際の入口である。

主要 API は次の通り。

- `BuildWorkspaceBaselineReport()`
- `BuildReport(...)`
- `ScanSceneRoots(...)`
- `CreateDefaultTargets()`

また Unity menu からも叩ける。

- `Tools/Kernel/M11.4/Print Asset Migration Report`

#### validator

[SceneAssetMigrationValidationService.cs](../Editor/KernelBoot/SceneAssetMigrationValidationService.cs) は report を `AuthoringValidationReport` に落とす。

これにより、scene migration が単なるログ出力で終わらず、build-blocking な validation code を持てるようになった。

### 7.3 対象 scene と required anchor

現在の default target は [SceneAssetMigrationReportService.cs](../Editor/KernelBoot/SceneAssetMigrationReportService.cs) で固定している。

#### TitleScene

対象:

- `Assets/Scenes/TitleScene.unity`

required anchor:

- `EntityIdentityMB`
- `SceneKernelHostMB`

legacy anchor:

- `CommandRunnerMB`
- `BlackboardMB`

#### GameScene

対象:

- `Assets/Scenes/GameScene.unity`

required anchor:

- `EntityIdentityMB`
- `SceneKernelHostMB`
- `SceneKernelSpawnDeclarationMB`
- `SceneKernelSpawnHostMB`

legacy anchor:

- `RuntimeLifetimeScope`
- `CommandRunnerMB`
- `BlackboardMB`

### 7.4 何がまだないか

M11.4 でまだないものは、実際の destructive rewrite 実装である。

つまり、次のような utility はまだ存在しない。

- `SceneAssetMigrationUtility.MigrateScene(...)`
- `PrefabAssetMigrationUtility.MigratePrefab(...)`
- legacy field を successor authoring にコピーする scene rewrite 実装

これは仕様違反ではなく、意図的な順序である。まず report と validator を先に作り、そのあとで rewrite を載せる構成にしている。

### 7.5 M11.4 の完成度

M11.4 は「migration foundation はかなり完成しているが、rewrite 本体は未着手」と言うのが正確である。

---

## 8. M11.5 で作ったもの

M11.5 は「実ゲーム surface がどれだけ新経路で通せるか」を report にするフェーズである。

### 8.1 M11.5 の主要ファイル

- [Assets/Editor/KernelBoot/ShippedGameplayVerificationModel.cs](../Editor/KernelBoot/ShippedGameplayVerificationModel.cs)
- [Assets/Editor/KernelBoot/ShippedGameplayVerificationService.cs](../Editor/KernelBoot/ShippedGameplayVerificationService.cs)
- [Assets/Editor/Tests/KernelBoot/ShippedGameplayVerificationTests.cs](../Editor/Tests/KernelBoot/ShippedGameplayVerificationTests.cs)
- [Assets/Editor/KernelBoot/AuthoringBridge.cs](../Editor/KernelBoot/AuthoringBridge.cs)
- [Assets/Editor/KernelBoot/AuthoringDirectPlayDiagnostics.cs](../Editor/KernelBoot/AuthoringDirectPlayDiagnostics.cs)
- [Assets/GameLib/Script/Kernel/Diagnostics/Core/Sinks/InMemoryDiagnosticSink.cs](../GameLib/Script/Kernel/Diagnostics/Core/Sinks/InMemoryDiagnosticSink.cs)

### 8.2 何を作ったか

#### verification report

[ShippedGameplayVerificationService.cs](../Editor/KernelBoot/ShippedGameplayVerificationService.cs) は M11.5 report の中心である。

主要 API は次の通り。

- `BuildWorkspaceReport()`
- `BuildReport(...)`
- `Validate(...)`
- `SummarizeDirectPlayProof(...)`

Unity menu からも叩ける。

- `Tools/Kernel/M11.5/Print Shipped Gameplay Verification Report`

#### inventory gate

この service は次の inventory 文書の summary を読んで entry gate を閉じる。

- [Index/ServiceCutoverInventory.md](Index/ServiceCutoverInventory.md)
- [Index/CommandCutoverInventory.md](Index/CommandCutoverInventory.md)
- [Index/ValueScalarQueryInventory.md](Index/ValueScalarQueryInventory.md)
- [Index/DynamicSourceCutoverInventory.md](Index/DynamicSourceCutoverInventory.md)

つまり M11.5 は「scene だけ綺麗なら OK」ではない。service / command / value / dynamic 側の残債も gate に入る。

#### direct-play proof 集約

`SummarizeDirectPlayProof(...)` は `AuthoringDirectPlayResult` を `ShippedGameplayDirectPlayProofRecord` に落とし込む。

このとき次を集計する。

- failed stage
- diagnostic count
- warning count
- error count
- fatal count
- truncation
- blocking codes

これにより、shipped gameplay の検証が単なるテスト pass/fail ではなく、proof record の集合として扱えるようになった。

### 8.3 M11.5 が検証対象にしている scene

[ShippedGameplayVerificationService.cs](../Editor/KernelBoot/ShippedGameplayVerificationService.cs) で required scene target は固定されている。

- `Assets/Scenes/TitleScene.unity`
- `Assets/Scenes/GameScene.unity`

### 8.4 M11.5 の完成度

M11.5 は verification foundation としては成立している。

ただし、real harness の厚みはまだ十分ではない。特に次は未完了である。

- TitleScene harness の実体強化
- GameScene proof harness の実体強化
- scene migration 後の direct play 実証

つまり、report の器はあるが、scene rewrite が終わっていない以上、proof もまだ最終状態ではない。

---

## 9. 旧システムとの分離はどこまで進んだか

ここが最も重要である。

「分離が進んでいる」と「legacy が消えた」は違う。

### 9.1 かなり分離できている部分

#### inventory / gate / report

次はかなり新アーキテクチャの考え方で揃っている。

- inventory 文書群
- M11.4 scene asset migration report / validation
- M11.5 shipped gameplay verification report
- dynamic runtime substrate
- kernel-side IR / generation artifact の command 面

#### explicit install 化が始まった host

次は `IFeatureInstaller` 自動探索依存を少しずつ落としている。

- `CommandRunnerMB`
- `RuntimeManagerMB`

これは「legacy component はまだ残るが、legacy scan authority ではなくする」ための重要な一歩である。

### 9.2 まだ深く残っている部分

#### Blackboard 系

- [Assets/GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs](../GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs)
- [Assets/GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs](../GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs)
- [Assets/GameLib/Script/Common/Variables/Blackboard/Service/GridBlackboardService.cs](../GameLib/Script/Common/Variables/Blackboard/Service/GridBlackboardService.cs)

value init、grid blackboard、scope-kind dispatch がまだ残るため、value runtime の truth はまだ完全に切れていない。

#### Actor / query fallback 系

- [Assets/GameLib/Script/Common/Commands/VNext/Core/ActorSourceFastResolver.cs](../GameLib/Script/Common/Commands/VNext/Core/ActorSourceFastResolver.cs)

`IScopeNode`、親 traversal、fallback registry に依存しているので、新しい verified query contract に閉じていない。

#### scene discovery / hierarchy repair 系

- [Assets/GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs](../GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs)

この file では、いまも `FindObjectsByType`、`BaseLifetimeScope`、親 reparent が見える。これは明確に legacy residue である。

#### runtime spawn / pool authority

- [Assets/GameLib/Script/Project/Scene/Runtime/RuntimeManager/RuntimeManagerMB.cs](../GameLib/Script/Project/Scene/Runtime/RuntimeManager/RuntimeManagerMB.cs)
- [Assets/GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs](../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs)

spawn / pool authority はまだ `SceneKernel` へ完全移行していない。

### 9.3 分離の評価

率直に言うと、分離は中盤である。

- inventory / validation / verification はかなり新アーキテクチャ準拠
- runtime authority はまだ複数箇所で legacy が残る
- shipped scene にはまだ legacy component が serialize されている

したがって、M11 は「構造と gate はできたが、runtime truth の切替はまだ完了していない」という評価になる。

---

## 10. 現在の完成度

M11 各節の完成度を、現実的にまとめると次の通り。

| 節 | 状態 | 評価 |
| --- | --- | --- |
| M11.1 | 完了 | inventory freeze は機能している |
| M11.2 | 部分完了 | explicit install 化は始まったが service wave は大半未消化 |
| M11.3 | 未完了 | command / value / scalar / query / dynamic source の残存接続切替はまだ重い |
| M11.4 | 部分完了 | report / validator / spec はあるが rewrite 実装がない |
| M11.5 | 部分完了 | verification report と proof summary はあるが final harness は未完了 |

全体としては、M11 の 100% 中、実装・文書・gate を含めて 45% から 60% 程度と見るのが妥当である。

理由は次の通り。

- M11.1 が完了
- M11.4 / M11.5 は foundation が強い
- しかし M11.2 / M11.3 の runtime truth 切替がまだ大きく残る
- scene asset 自体の rewrite がまだ終わっていない

---

## 11. 今の仕組みをどう使うか

### 11.1 inventory を見る

M11 作業の最初の入口は inventory 文書である。

- service を見たいときは [Index/ServiceCutoverInventory.md](Index/ServiceCutoverInventory.md)
- command を見たいときは [Index/CommandCutoverInventory.md](Index/CommandCutoverInventory.md)
- dynamic を見たいときは [Index/DynamicSourceCutoverInventory.md](Index/DynamicSourceCutoverInventory.md)
- value / scalar / query を見たいときは [Index/ValueScalarQueryInventory.md](Index/ValueScalarQueryInventory.md)
- asset を見たいときは [Index/ScenePrefabInventory.md](Index/ScenePrefabInventory.md)

### 11.2 M11.4 report を使う

M11.4 の report は Unity Editor から次で実行できる。

- `Tools/Kernel/M11.4/Print Asset Migration Report`

コードから使う場合は [SceneAssetMigrationReportService.cs](../Editor/KernelBoot/SceneAssetMigrationReportService.cs) の次を使う。

- `BuildWorkspaceBaselineReport()`
- `BuildReport(...)`
- `ScanSceneRoots(...)`

使い方の流れは次の通り。

1. shipped scene baseline を report 化する
2. required anchor が足りているか見る
3. legacy anchor が残っていないか見る
4. unresolved item count が 0 でない限り rewrite しない

### 11.3 M11.5 verification を使う

M11.5 は Unity Editor から次で実行できる。

- `Tools/Kernel/M11.5/Print Shipped Gameplay Verification Report`

コードから使う場合は [ShippedGameplayVerificationService.cs](../Editor/KernelBoot/ShippedGameplayVerificationService.cs) の次を使う。

- `BuildWorkspaceReport()`
- `BuildReport(...)`
- `Validate(...)`
- `SummarizeDirectPlayProof(...)`

使い方の流れは次の通り。

1. inventory gate を閉じる
2. scene migration report を読む
3. shipped target ごとの blocking code を見る
4. direct-play result を proof summary に落とす
5. unresolved が 0 になるまで閉じる

---

## 12. 新しい機能をどう足すか

M11 以降で新機能を足すとき、最も重要なのは「旧経路に乗せないこと」である。

### 12.1 新しい service を足すとき

原則は次の順で行う。

1. service surface を既存 inventory に追加または更新する
2. `IFeatureInstaller` ではなく declaration / explicit host / verified path を使う
3. scene に置く必要があるなら `EntityIdentityMB` や declaration MB に乗せる
4. M11.5 で proof target に必要なら verification flow も更新する
5. legacy host に仮置きしたら、その change set で inventory を `進行中` に更新する

見本になる file:

- [Assets/GameLib/Script/Project/UI/Core/Elements/ButtonChannel/ButtonChannelHubDeclarationMB.cs](../GameLib/Script/Project/UI/Core/Elements/ButtonChannel/ButtonChannelHubDeclarationMB.cs)
- [Assets/GameLib/Script/Kernel/Authoring/EntityDeclarationMB.cs](../GameLib/Script/Kernel/Authoring/EntityDeclarationMB.cs)
- [Assets/Editor/Tests/KernelBoot/ScopeAuthoringExtractionTests.cs](../Editor/Tests/KernelBoot/ScopeAuthoringExtractionTests.cs)

### 12.2 新しい command を足すとき

原則は次の通り。

1. command data と executor を追加する
2. command inventory で surface を追跡対象に入れる
3. `CommandRunnerMB` 前提ではなく `CommandRunnerService` 側で使う前提で考える
4. actor resolve に新 fallback を足さない
5. scene host や MB を増やすなら declaration-backed に寄せる

見本・関連 file:

- [Assets/GameLib/Script/Common/Commands/VNext/Core/CommandRunnerService.cs](../GameLib/Script/Common/Commands/VNext/Core/CommandRunnerService.cs)
- [Assets/GameLib/Script/Common/Commands/VNext/Core/CommandContext.cs](../GameLib/Script/Common/Commands/VNext/Core/CommandContext.cs)
- [Assets/GameLib/Script/Common/Commands/VNext/Core/ActorSourceFastResolver.cs](../GameLib/Script/Common/Commands/VNext/Core/ActorSourceFastResolver.cs)

### 12.3 新しい dynamic source を足すとき

原則は次の通り。

1. `IDynamicSource` 系の純粋な runtime surface として追加する
2. Blackboard を参照しない source にする
3. editor drawer や preview を足す場合も inventory を更新する
4. `DynamicValueResolver` や registry fallback を再導入しない

見本 file:

- [Assets/GameLib/Script/Common/Variables/Dynamic/Core/IDynamicSource.cs](../GameLib/Script/Common/Variables/Dynamic/Core/IDynamicSource.cs)
- [Assets/GameLib/Script/Common/Variables/Dynamic/Core/DynamicSources.cs](../GameLib/Script/Common/Variables/Dynamic/Core/DynamicSources.cs)
- [Assets/GameLib/Script/Common/Variables/Dynamic/Core/DynamicEvaluationRuntime.cs](../GameLib/Script/Common/Variables/Dynamic/Core/DynamicEvaluationRuntime.cs)

### 12.4 scene に新しい host / declaration を足すとき

原則は次の通り。

1. [Index/ScenePrefabInventory.md](Index/ScenePrefabInventory.md) の baseline を確認する
2. successor anchor を増やすなら [11_4_SceneAssetMigrationSpec.md](11_4_SceneAssetMigrationSpec.md) の target contract も同時更新する
3. report service の default target に required anchor を追加する
4. verification flow も M11.5 側で更新する

見本 file:

- [Assets/Editor/KernelBoot/SceneAssetMigrationReportService.cs](../Editor/KernelBoot/SceneAssetMigrationReportService.cs)
- [Assets/Editor/KernelBoot/ShippedGameplayVerificationService.cs](../Editor/KernelBoot/ShippedGameplayVerificationService.cs)

---

## 13. service / DynamicSource / command などの接続は修正されたのか

この問いに対する正確な答えは「一部は yes、全体は no」である。

### 13.1 service 接続

- inventory 化は済んだ
- UI core 8 surface には `進行中` がある
- explicit install 化も一部始まった
- だが service 全体では 308 surface が `要差し替え`

したがって、service 接続は全体としてはまだ大きく未完了である。

### 13.2 command 接続

- `CommandRunnerService` は前進した
- `CommandRunnerMB` の explicit install 化も進んだ
- だが 507 surface が `要差し替え`
- catalog / executor / host / authoring まで含めると未完了

つまり command は「runner shell は進んだが、command 面全体はまだ途中」である。

### 13.3 dynamic source 接続

- substrate と evaluation runtime はかなり整っている
- 146 surface は `置換済み`
- しかし Blackboard 起源 source や一部 gameplay source は残る

つまり dynamic は、M11 の中では最も progress が良い側の 1 つだが、完了ではない。

### 13.4 value / scalar / query 接続

- `ValueStore` contract はある
- `VarStore` backend bridge もある
- しかし `BlackboardMB` と `BlackboardService` が残る
- scalar fallback と actor query helper も残る

ここはまだかなり未完了である。

---

## 14. いま最優先で残っている実装課題

M11 を本当に閉じるために、次の順が自然である。

1. `BlackboardMB` を explicit / declaration / value-boundary 側へ落とし、`Blackboard` truth を切る
2. `ActorSourceFastResolver` の parent / registry / helper fallback を消す
3. `LoadingScreenService` の scene discovery と parent repair を消し、明示 host に寄せる
4. M11.4 の scene rewrite utility を作る
5. TitleScene を先に migration する
6. GameScene を次に migration する
7. M11.5 で direct-play proof を scene rewrite 後の実 asset に対して固める

---

## 15. 最後の評価

M11 までの実装は、土台作りとしては強い。

特に良い点は次である。

- inventory があり、進捗を数で見える
- scene migration を fail-closed で扱っている
- shipped gameplay verification を report と proof にしている
- explicit install 化が始まり、legacy scan authority を少しずつ剥がしている
- dynamic runtime substrate はかなり整っている

一方で、まだ厳しく見るべき点は次である。

- shipped scene に legacy component がまだ残る
- `BlackboardMB` がまだ強い
- `ActorSourceFastResolver` がまだ legacy query helper
- `LoadingScreenService` がまだ scene discovery / parent repair に依存
- `RuntimeManagerMB` の中身はまだ旧 spawn authority
- service wave は数の上でもまだ大半が未切替

結論として、M11 は「最終切替のための実務的な地図と gate はできた」が、「旧システムからの完全脱却」はまだ未達成である。

この文書の役割は、その未達成部分を曖昧にしないことにある。
