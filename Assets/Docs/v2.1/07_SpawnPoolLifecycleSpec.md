# Kernel v2.1 Spawn / Pool Lifecycle 仕様

## 文書ステータス

- 文書 ID: `07_SpawnPoolLifecycleSpec`
- 状態: Draft
- 役割: v2.1 において runtime-created entity の spawn / pool / despawn / delete mediation をどう所有し、外部 API をどう保ちながら内部 authority をどう置き換えるかを定義する
- 範囲: unified spawn core、compatibility registry、prefab-based pooling、warmup、release / despawn / delete mediation、reparent policy、diagnostics、performance、legacy replacement
- 非目標: 最終クラス名の固定、個別 gameplay ルールの再設計、UI レイアウト、scene transition policy の最終確定

### 改訂メモ

この文書は、既存の spawn / pool 系コードを「少し整理する」ためのものではない。
v2.1 の target path では、runtime spawn の authority を一度切り直し、SceneKernel 配下の単一 authority として再構成するための仕様である。

2026-05-27 変更メモ:

- `RuntimeLifetimeScope` / `RuntimeManagerMB` / `RuntimeLifetimeScopeSpawnerService` を改善対象ではなく削除対象として扱うことを明記した。
- new target path の public contract と public naming に `RuntimeLifetimeScope` 系の語を持ち込まないことを明記した。
- `SceneKernel` に scene-local spawn mediation boundary と host/declaration bridge が必要であることを明記した。
- scene / prefab が旧 MB / 旧 runtime species を参照している場合の asset migration を 07 の責務に含めた。

本文では、旧呼称ではなく runtime-created entity / runtime spawn / unified spawn core という語に統一する。

この改訂で固定する中心ルールは次の通り。

- 外部から見える spawn API は原則として維持する
- 内部では kind ごとの複数 authority をやめ、1 つの unified spawn core にまとめる
- pool の識別子は parent ではなく prefab family である
- same-parent-only reuse は廃止する
- reparenting は explicit policy として許可する
- delete / despawn / destroy は spawn system を必ず仲介させる
- direct `Destroy` を caller 側の通常経路に残さない

この仕様での hard rule:

- new target path の public truth に `RuntimeLifetimeScope` / `RuntimeManager` / `RuntimeLifetimeScopeSpawnerService` の語と責務を残さない
- runtime-created object は runtime species ではなく `Entity` として扱い、authority key は `EntityRef` と explicit lease / handle に置く
- compatibility adapter は許可するが、truth source を旧 manager / old pool / hierarchy authority に残さない

---

## 所有範囲

この仕様が所有するもの:

- unified spawn core の責務
- `SpawnerKind` / tag route の compatibility ルール
- `SpawnParams` の contract
- prefab-family pooling contract
- warmup contract
- release / despawn / destroy mediation contract
- bulk delete mediation contract
- pool lifetime boundary
- reparent / attach policy
- spawn / release / delete diagnostics
- spawn performance rule
- legacy runtime spawn authority からの replacement mapping

この仕様が所有しないもの:

- command system の意味論そのもの
- value / scalar / UI の個別意味論
- final prefab authoring UX
- final class / namespace 名
- scene flow や loading presentation の見た目

---

## 目的

v2.1 の spawn / pool 仕様の目的は次の通り。

```text
1. runtime spawn の authority を SceneKernel 配下に集約する。
2. spawn request の外形は保ちつつ、内部実装を単一 core に再構成する。
3. pool の truth source を parent から prefab family に移す。
4. release / despawn / delete を必ず mediator 経由にする。
5. hierarchy discovery と direct destroy を runtime 標準経路から外す。
```

中心ルール:

```text
spawn は route であって authority ではない。
pool は prefab family の再利用であって parent family の再利用ではない。
delete は mediator を通した release / destroy の結果であり、caller の直接操作ではない。
```

---

## v2 仕様との関係

この仕様は次の v2 文書を上位制約として扱う。

| v2 仕様 | v2.1 での意味 |
| --- | --- |
| [07_ScopeGraphRuntimeSpec.md](../v2/07_ScopeGraphRuntimeSpec.md) | scope の generation、attach / detach / reparent、pool invalidation を定義する上位制約 |
| [08_LifecyclePlanSpec.md](../v2/08_LifecyclePlanSpec.md) | spawn / despawn / release の実行順を plan 駆動にする制約 |
| [13_LegacyCompatBoundarySpec.md](../v2/13_LegacyCompatBoundarySpec.md) | legacy spawn authority を quarantine に閉じ込める制約 |
| [14_PerformanceBudgetAndRuntimeRulesSpec.md](../v2/14_PerformanceBudgetAndRuntimeRulesSpec.md) | hierarchy discovery、reflection、silent fallback を残さない制約 |
| [15_TestAndValidationSpec.md](../v2/15_TestAndValidationSpec.md) | spawn / pool / delete の test と validation gate を定義する制約 |

この仕様は、v2 の意味論を再定義しない。
v2 の scope / lifecycle / performance / legacy boundary を、現行ゲームの runtime spawn に適用するだけである。

---

## v2.1 仕様との関係

| v2.1 仕様 | 関係 |
| --- | --- |
| [01_LegacySystemReplacementSpec.md](01_LegacySystemReplacementSpec.md) | legacy runtime spawn authority の replacement mapping を補強する |
| [02_ConcreteMigrationArchitectureSpec.md](02_ConcreteMigrationArchitectureSpec.md) | `SceneKernel` の concrete responsibility に spawn mediation を加える |
| [03_LegacyRemovalExamplesSpec.md](03_LegacyRemovalExamplesSpec.md) | runtime manager / pool 削除の vertical example を補強する |
| [05_ImplementationMilestoneSpec.md](05_ImplementationMilestoneSpec.md) | spawn / pool authority の切替順を fixed order に落とす |
| [06_KernelLayerCompositionSpec.md](06_KernelLayerCompositionSpec.md) | spawn authority を `SceneKernel` の責務として固定する |

---

## 現行コードの観測と replacement anchor

この仕様は、次の現行コードを前提にする。

- [SceneKernel.cs](../../GameLib/Script/Kernel/Layers/Core/SceneKernel.cs)
- [SceneKernelComposition.cs](../../GameLib/Script/Kernel/Layers/Composition/SceneKernelComposition.cs)
- [SceneKernelHostMB.cs](../../GameLib/Script/Kernel/Layers/Unity/SceneKernelHostMB.cs)
- [RuntimeManagerMB.cs](../../GameLib/Script/Project/Scene/Runtime/RuntimeManager/RuntimeManagerMB.cs)
- [RuntimeLifetimeScopePool.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScopePool.cs)
- [RuntimeLifetimeScope.cs](../../GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs)
- [SpawnParams.cs](../../GameLib/Script/Common/Spawner/SpawnParams.cs)
- [SpawnerCore.cs](../../GameLib/Script/Common/Spawner/SpawnerCore.cs)
- [SceneSpawnerResolver.cs](../../GameLib/Script/Common/Spawner/SceneSpawnerResolver.cs)
- [RuntimeAllDeleteExecutor.cs](../../GameLib/Script/Common/Commands/VNext/Executors/Spawn/RuntimeAllDeleteExecutor.cs)
- [RuntimeAllDeleteCommandData.cs](../../GameLib/Script/Common/Commands/VNext/Commands/Spawn/RuntimeAllDeleteCommandData.cs)
- [RuntimeLifetimeScopeDeleteFilter.cs](../../GameLib/Script/Project/Scene/Runtime/RuntimeManager/RuntimeLifetimeScopeDeleteFilter.cs)
- [EmitterService.cs](../../GameLib/Script/Common/EmitterSpawn/Core/EmitterService.cs)
- [SelfDespawnExecutor.cs](../../GameLib/Script/Common/Commands/VNext/Executors/Core/LifecycleExecutors.cs)
- [EntityLifetimeScopeSpawnerMB.cs](../../GameLib/Script/Project/Scene/Field/Entity/Spawner/EntityLifetimeScopeSpawnerMB.cs)
- [UIElementSpawnerService.cs](../../GameLib/Script/Project/UI/Core/Spawner/UIElementSpawnerService.cs)

この一覧のうち、`SceneKernel` / `SceneKernelComposition` / `SceneKernelHostMB` は replacement owner 側の anchor であり、
`RuntimeManagerMB` / `RuntimeLifetimeScopePool` / `RuntimeLifetimeScope` / `RuntimeLifetimeScopeSpawnerService` は deletion anchor である。

### 重要な migration rule

07 の実装開始点は old pool key の微修正ではない。
最初に固定すべきものは次である。

- `SceneKernel` に scene-local spawn / pool / release / delete mediation boundary を追加すること
- host / declaration bridge で scene-local spawn settings と warmup input を渡せるようにすること
- compatibility adapter を `SpawnParams` / routing label / delete filter に限定すること
- scene / prefab の `RuntimeManagerMB` / `RuntimeLifetimeScope` 参照を自動移行できること
- その完了後にだけ旧 manager / pool / spawner class を削除すること

現行の圧力点は次の通り。

| 観測 | 問題 | 影響 |
| --- | --- | --- |
| runtime spawn の登録、warmup、telemetry、bulk delete が `RuntimeManagerMB` に集約されている | authority が肥大化している | SceneKernel の責務境界が曖昧になる |
| pool key が prefab だけでなく parent を含む | reuse が hierarchy に依存する | same-parent-only reuse が発生する |
| delete が hierarchy scan と registry route の両方を使っている | release / destroy の経路が散る | direct destroy との境界が崩れる |
| runtime spawner が kind ごとに分かれている | 内部 authority が複数に見える | unified core に見えない |
| spawn helper が direct destroy を持っている | delete mediation が漏れる | caller 側が authority になる |
| `SpawnParams` に prefab / template / parent / DI parent が同居している | contract が強力だが、その分 validator が必要 | 曖昧入力を許すと不具合が増える |

---

## Core Problem

現行 runtime の spawn / pool は、次の問題を同時に抱えている。

- spawn authority が registry、manager、pool、helper、command executor に分散している
- pool identity が prefab family ではなく parent hierarchy に引きずられている
- release / despawn / destroy の責務分離が曖昧である
- delete が hierarchy discovery に依存している
- kind/tag の routing と authority の所有者が分離されていない

v2.1 では、これを次の形に直す必要がある。

```text
SceneKernel が spawn authority を持つ。
kind/tag は routing label であり、別 authority ではない。
pool は prefab family に対する再利用であり、parent family ではない。
delete は mediator 経由でのみ成立する。
```

---

## Unified Spawn Core

### 1. SceneKernel ownership

spawn / pool authority は scene-local である。

そのため、spawn core は `SceneKernel` 配下に置く。

許可:

- scene-local spawn request の受理
- scene-local pool の管理
- scene-local release / despawn / destroy mediation
- scene-local warmup
- scene-local bulk delete mediation

禁止:

- `ApplicationKernel` に per-scene pool を持ち込むこと
- hierarchy discovery を authority とすること
- `Transform.parent` を pool key にすること
- scene external から direct destroy を standard path にすること

### 2. Compatibility routing

`SpawnerKind` と tag は、外部互換の routing label として残す。

ただし、routing label は authority ではない。
内部では 1 つの unified spawn core があり、kind/tag による分岐は policy input だけを切り替える。

compatibility fallback は次の条件のみ許可する。

- caller が明示的に fallback を許可していること
- fallback 使用時に structured diagnostics / telemetry を出すこと
- fallback が未定義の route を silently repair しないこと

許可しないもの:

- `RuntimeManagerMB` や `RuntimeLifetimeScopeSpawnerService` を compatibility manager として残すこと
- 旧 hierarchy authority を fallback route の実装母体に使うこと

### 3. `SpawnParams` contract

`SpawnParams` は caller-facing compatibility DTO として維持する。

この contract の意味は次の通り。

- `Prefab` または `Template` は spawn family を定義する
- `TransformParent` は Unity hierarchy の attachment target である
- `LifetimeScopeParent` は compatibility adapter が受け取る ownership parent 入力であり、SceneKernel 側では explicit owner handle / boundary input に正規化される
- `AllowPooling` は pooling eligibility を caller が明示するための flag である
- `Identity` は runtime identity metadata であり、pool key ではない
- `WorldSpace` / `Position` / `Rotation` / `Scale` は pose contract であり、ownership contract ではない

validation rule:

- `Prefab` と `Template` の両方が空なら invalid
- 両方が設定される入力は、compatibility adapter で正規化できない限り invalid
- `TransformParent` と `LifetimeScopeParent` を混同してはならない

### 4. Pooling model

pool identity は prefab family で決まる。

この仕様での pool family は次である。

- prefab asset identity
- template から解決された prefab family

禁止:

- parent ごとの pool 分岐
- same-parent-only reuse
- parent walk を pool lookup の truth にすること
- runtime search で pool family を invent すること

許可:

- 同一 prefab family への複数 parent からの再利用
- explicit reparent を伴う acquire
- explicit parking root への release
- template / preset による pool eligibility の制御

pooling rule:

- pooling eligibility は `AllowPooling` と template policy の両方で決まる
- template が pooling を禁止するなら caller が許可しても pool しない
- caller が pooling を禁止したなら template が許可していても pool しない

### 5. Reparent and attach policy

reparent は explicit policy として許可する。

ただし、reparent は ownership を意味しない。
pool family、generation、identity、lifecycle boundary は変えない。

許可:

- spawn 後の explicit attach
- scene-local の parent 再接続
- runtime graph の再編成

禁止:

- reparent を pool key にすること
- reparent を identity lookup に使うこと
- reparent を silent repair に使うこと

### 6. Warmup contract

warmup は pool family の preallocation である。

warmup に許可されること:

- explicit prefab family に対する事前生成
- scene-local pool root への parking
- deterministic な初期化

warmup に禁止されること:

- scene discovery
- parent hierarchy scan
- warmup での owner invent
- runtime fallback での pool family 生成

warmup は、spawn path を先に stable にし、その後に performance を上げるための補助手段である。

---

## Concrete implementation design

### 1. Responsibility split from current files

この設計は、少なくとも次の現行コードを分解対象として読むことを前提にする。

- `RuntimeManagerMB.cs`
- `RuntimeLifetimeScope.cs`
- `RuntimeLifetimeScopePool.cs`
- `SceneKernelHostMB.cs`
- `SceneLifetimeScope.cs`
- `UIElementRuntimeSpawnerMB.cs` / `UIElementRuntimeSpawnerService.cs`
- `RuntimeAllDeleteExecutor.cs`
- `LifecycleExecutors.cs`

責務分解は次の通りとする。

| Current file | 今の責務 | 新しい owner / class split | 備考 |
| --- | --- | --- | --- |
| `RuntimeManagerMB.cs` | runtime spawn 登録、warmup、telemetry、bulk delete、scene root 設定 | `SceneKernelSpawnDeclarationMB` + `SceneKernelSpawnHostMB` + `SceneKernelSpawnBoundary` + `SceneKernelSpawnDebugView` | 1 クラスに authority を集めない |
| `RuntimeLifetimeScope.cs` / `RuntimeLifetimeScopeBase.cs` | prefab root DI scope、identity、acquire/release、hierarchy 親推論 | `SceneKernelEntityInstanceMB` + `SceneKernelEntityLeaseTable` + migration-only release bridge | new target path の public truth から `IScopeNode` / resolver を外す |
| `RuntimeLifetimeScopePool.cs` | prefab instantiate / reuse / telemetry / on-reacquire command queue | `SceneKernelPrefabPool` + `SceneKernelSpawnTelemetry` | pool family は prefab family のみ |
| `SceneKernelHostMB.cs` | SceneKernel の scene-local host | `SceneKernelHostMB` のまま維持し、spawn 専用責務は `SceneKernelSpawnHostMB` に分離 | host を manager 化しない |
| `SceneLifetimeScope.cs` | `ISceneSpawnerRegistry` の登録、`RuntimeManagerMB` の依存 | `SceneSpawnerRegistryBridge` の登録と migration-only release gateway の登録だけに縮小 | `RuntimeManagerMB` の require を削除する |
| `UIElementRuntimeSpawnerMB.cs` / `UIElementRuntimeSpawnerService.cs` | runtime UI spawner の installer / wrapper | `SceneKernelSpawnDeclarationMB` の route entry へ移行し、class は削除する | `SpawnerKind.RuntimeUIElement` は route label としてのみ残す |
| `RuntimeAllDeleteExecutor.cs` | old runtime spawner interface への downcast | `IFilteredReleaseSpawnerService` への依存に置換 | old interface 名を target path に残さない |
| `LifecycleExecutors.cs` | `IRuntimeLifetimeScopePool` を直接 resolve して release | `ISceneKernelReleaseGateway` と current lease handle に置換 | self-despawn の truth を lease にする |

07 の first target は次である。

- `SpawnerKind.RuntimeEntity`
- `SpawnerKind.RuntimeUIElement`
- `RuntimeManagerMB` / `RuntimeLifetimeScopeSpawnerService` / `RuntimeLifetimeScopePool`

次は 07 の first target には含めない。

- `EntityLifetimeScopeSpawnerMB`
- `UIElementSpawnerMB`
- `SpawnerKind.Entity`
- `SpawnerKind.UIElement`

これらは legacy registry side に残ってよいが、new spawn core の内部依存にしてはならない。

### 2. New class set

new target path の principal class は次で固定する。

| Class | Layer | Responsibility |
| --- | --- | --- |
| `SceneKernelSpawnBoundary` | Core | `ISceneKernelSpawnBoundary` の concrete authority。spawn / warmup / release / bulk release / lease lookup を持つ |
| `SceneKernelEntityLeaseTable` | Core | `EntityRef` と `SceneKernelEntityLeaseHandle` の truth table。active / parked / released state と lease generation を所有する |
| `SceneKernelEntityRefFactory` | Core | runtime-created entity の canonical `EntityRef` string seed を決める。generation は持たない |
| `SceneKernelSpawnRouteTable` | Core | declaration から route 定義を compile した scene-local table |
| `SceneKernelPrefabPool` | Core | prefab-family pool。acquire / park / reuse / destroy と telemetry bucket を持つ |
| `SceneKernelSpawnTelemetry` | Core | spawn core の snapshot / recent event を持つ。debug viewer 以外の authority を持たない |
| `SceneKernelSpawnHostMB` | Unity | scene root で boundary を生成し、`SceneKernelComposition` に bind し、warmup を開始する |
| `SceneKernelSpawnDeclarationMB` | Authoring | route / warmup / parking root / debug policy を serialize する scene-local declaration |
| `SceneKernelEntityInstanceMB` | Unity | spawned prefab root の thin runtime anchor。current lease handle と scene-local release callback を保持する |
| `SceneSpawnerRegistryBridge` | Quarantine | legacy resolver から見える `ISceneSpawnerRegistry` bridge。runtime route adapter と未移行 spawner を束ねる |
| `SceneKernelSpawnRouteAdapter` | Quarantine | 1 route 分の `IAsyncSpawnerService` adapter。必要なら `IFilteredReleaseSpawnerService` も実装する |
| `SceneKernelReleaseGateway` | Quarantine | command / compatibility code が current instance を release するための migration-only surface |

`ISceneKernelSpawnBoundary` は current slice では lookup のみだが、最終形では少なくとも次を持つものとして扱う。

- `SpawnAsync(SceneKernelSpawnRequest request, CancellationToken ct)`
- `WarmupAsync(SceneKernelWarmupRequest request, CancellationToken ct)`
- `Release(SceneKernelEntityLeaseHandle lease, SceneKernelReleaseReason reason)`
- `ReleaseAll(SceneKernelBulkReleaseQuery query)`
- `TryGetLease(EntityRef entityRef, out SceneKernelEntityLeaseHandle lease)`
- `ValidateLease(SceneKernelEntityLeaseHandle lease)`

この拡張は `SceneKernelSpawnContracts.cs` に集約し、spawn contract を別ファイルへ分散させない。

### 3. Host / declaration bridge shape

scene ごとの persistent root `SceneKernel` GameObject に、少なくとも次の 3 component を置く。

- `SceneKernelHostMB`
- `SceneKernelSpawnHostMB`
- `SceneKernelSpawnDeclarationMB`

役割分担:

- `SceneKernelHostMB` は kernel lifecycle owner のままにする
- `SceneKernelSpawnHostMB` は spawn core の construction / binding / teardown のみを行う
- `SceneKernelSpawnDeclarationMB` は serialized authoring data だけを持つ

禁止:

- `SceneKernelHostMB` に warmup entry、tag list、telemetry state を直接抱えさせること
- `SceneKernelSpawnHostMB` を `IFeatureInstaller` にして old resolver build に従属させること
- declaration を scene 内の任意 object 探索で見つけること

route declaration の最小 shape は次とする。

| Field | Meaning |
| --- | --- |
| `SpawnerKind Kind` | `RuntimeEntity` または `RuntimeUIElement` |
| `string Tag` | compatibility routing label |
| `Transform Root` | spawn attach default root |
| `Transform ParkingRoot` | pooled inactive instance の parking root。未指定なら host default |
| `string RouteId` | diagnostics / migration report 用の stable id |

warmup declaration の最小 shape は次とする。

| Field | Meaning |
| --- | --- |
| `SpawnerKind Kind` | warmup 対象 route の kind |
| `string Tag` | warmup 対象 route の tag |
| `DynamicValue<BaseRuntimeTemplatePreset> Template` | warmup family の入力 |
| `int Count` | scene load 時の prewarm 数 |

`RuntimeManagerMB` 由来の `root` / `spawnerTag` / `warmupEntries` は、この declaration に統合する。

### 4. EntityRef and lease model

checked-in の `EntityRef` 型自体は string wrapper であり、generation は持たない。

したがって、pool 再利用を含む runtime equality を raw `EntityRef` だけで判定してはならない。
runtime reuse safety は `SceneKernelEntityLeaseHandle` の generation で担保する。

前提:

- prefab root の `EntityIdentityMB.EntityRef` は authoring seed である
- `RuntimeIdentityData.Id` は diagnostics / metadata seed としては使ってよい
- raw `EntityRef` は current active entity を引くための route key として使える
- stale reference の検出は generation を含む lease handle で行う

そのため、責務は次のように分ける。

- `SceneKernelEntityRefFactory` は canonical `EntityRef` string を決める
- `SceneKernelEntityLeaseTable` は lease slot と generation を所有する
- `SceneKernelEntityLeaseHandle` は `SceneKernelHandle` + `EntityRef` + `LeaseId` + `Generation` を equality の truth にする

`LeaseId` は acquire ごとの一回限り id ではなく、scene-local lease slot id として扱う。
同じ pooled instance slot を再利用した場合、`LeaseId` は維持されてよいが、`Generation` は必ず増加しなければならない。

これは既存の `ScopeHandle(index, generation)` と同じ方針に揃える。

rule:

- live `EntityRef` は scene-local で同時 active instance 間に一意でなければならない
- pooled reacquire で同じ logical slot を再利用する場合は `EntityRef` を再利用してよい
- その代わり reacquire ごとに `Generation` をインクリメントする
- cached handle の一致判定は `EntityRef` ではなく `SceneKernelEntityLeaseHandle` 全体で行う
- `TryGetLease(EntityRef, out lease)` はその `EntityRef` の current handle を返すだけであり、caller が保持する stale handle の有効性保証には使わない
- `ValidateLease(SceneKernelEntityLeaseHandle)` は stale 判定にのみ使い、current handle の lookup を兼ねない

推奨 canonical format:

```text
runtime:{scene-kernel-handle}:{entity-seed}:{lease-slot-id}
```

ここでの `entity-seed` は次の優先順で決める。

1. prefab root `EntityIdentityMB.EntityRef`
2. template `TemplateId`
3. route `RouteId`

`Generation` は string 化した `EntityRef` に埋め込まず、lease handle と lease table が所有する。

`RuntimeIdentityData.Id` は live `EntityRef` の authority に使わない。
必要なら diagnostics payload や display/debug metadata に残す。

### 5. Pool and instance model

pool unit は `SceneKernelPrefabPool` であり、key は prefab family のみである。

`SceneKernelPrefabPool` が持つ責務:

- inactive parked instance の管理
- acquire / release / destroy
- prefab-family telemetry bucket
- on-reacquire command queue の migration-only support
- reacquire 時の lease generation 更新要求

`SceneKernelEntityInstanceMB` が持つ責務:

- current lease handle の保持
- current scene kernel handle の保持
- unexpected destroy / disable を boundary へ通知する hook
- pooled rebind 時の lightweight reset point

`SceneKernelEntityInstanceMB` は次を持たない。

- `IScopeNode`
- runtime resolver
- parent discovery
- installer scan
- scene authority

`BaseRuntimeTemplateSO` は 07 の期間だけ compatibility input として残してよいが、template hook の authority は新しい context へ移す。

そのため、`BaseRuntimeTemplateSO.cs` は次の方向で変更する。

- new path 用に `SceneKernelSpawnContext` ベースの hook を追加する
- 既存の `OnAcquire(IScopeNode scope, RuntimeIdentityData identity)` / `OnRelease(IScopeNode scope)` は migration-only として quarantine へ寄せる
- new target path の public contract から `IScopeNode` を要求しない

### 6. Compatibility bridge policy

未移行 caller のために、compatibility surface は 2 本だけ残してよい。

- `ISceneSpawnerRegistry`
- `ISceneKernelReleaseGateway`

`ISceneSpawnerRegistry` の実体は `SceneSpawnerRegistryBridge` とする。

`SceneSpawnerRegistryBridge` は migration-only であり、new path の truth source ではない。
`Entity` / `UIElement` などの inner legacy registry への委譲は quarantine assembly に閉じた移行期間だけ許可し、M12 までに new-path resolution から除去する。
M12 以降に残るとしても、diagnostics-only で route authority を持ってはならない。

bridge の振る舞い:

- `RuntimeEntity` / `RuntimeUIElement` は `SceneKernelSpawnRouteAdapter` を synthesize して返す
- `Entity` / `UIElement` など未移行 kind は inner legacy registry に委譲する
- tag fallback は caller が許可した場合のみ使う
- fallback 使用時は diagnostics / telemetry を記録する

bulk delete capability は old spawner interface を残さず、neutral capability interface へ落とす。

```text
IFilteredReleaseSpawnerService
  int ReleaseAll(SceneKernelBulkReleaseQuery query)
```

`RuntimeAllDeleteExecutor.cs` は route adapter が `IFilteredReleaseSpawnerService` を実装しているかだけを見ればよく、
`IRuntimeLifetimeScopeSpawnerService` への downcast を持ってはならない。

`SelfDespawnExecutor` と同等の current-instance release caller は `ISceneKernelReleaseGateway` を先に試し、
old pool branch は migration window のみ許可する。

### 7. File structure

```text
Assets/GameLib/Script/Kernel/
  Authoring/
    SceneKernelSpawnDeclarationMB.cs
    SceneKernelSpawnRouteDeclaration.cs
    SceneKernelSpawnWarmupDeclaration.cs
  Layers/
    Core/
      SceneKernelSpawnContracts.cs
      SceneKernelSpawnBoundary.cs
      SceneKernelEntityLeaseTable.cs
      SceneKernelEntityRefFactory.cs
      SceneKernelSpawnRouteTable.cs
      SceneKernelPrefabPool.cs
      SceneKernelSpawnTelemetry.cs
      SceneKernelSpawnContext.cs
    Unity/
      SceneKernelHostMB.cs
      SceneKernelSpawnHostMB.cs
      SceneKernelEntityInstanceMB.cs
      SceneKernelSpawnDebugView.cs
    Quarantine/
      Spawn/
        SceneSpawnerRegistryBridge.cs
        SceneKernelSpawnRouteAdapter.cs
        SceneKernelReleaseGateway.cs

Assets/Editor/
  KernelBoot/
    Spawn/
      RuntimeManagerToSceneKernelSpawnMigration.cs
      RuntimeSpeciesToEntityInstanceMigration.cs
      SceneKernelSpawnMigrationReport.cs
  Tests/
    SceneKernelSpawnBoundaryTests.cs
    KernelBoot/
      SceneKernelSpawnMigrationTests.cs
```

この layout の意図は次である。

- Core は pure runtime authority を置く
- Unity は scene / prefab にぶら下がる thin MonoBehaviour だけを置く
- Quarantine は migration-only compatibility bridge を隔離する
- Editor は scene / prefab 自動移行とその検証だけを置く

### 8. Existing file change map

設計上、既存ファイルの変更先は次で固定する。

| Existing file | Required change |
| --- | --- |
| `SceneKernelSpawnContracts.cs` | lookup-only contract から spawn / warmup / release / bulk release を含む contract へ拡張する |
| `SceneKernel.cs` | `TryGetSpawnBoundary` を維持し、spawn authority 自体は持ち込まない |
| `SceneKernelComposition.cs` | boundary の bind / clear のみを持ち、spawn 実装は持たない |
| `SceneKernelHostMB.cs` | kernel host のまま維持し、spawn data field は増やさない |
| `SceneLifetimeScope.cs` | `RuntimeManagerMB` require を削除し、`SceneSpawnerRegistryBridge` を登録する |
| `UIElementSpawnerService.cs` | `UIElementRuntimeSpawnerService` とその interface を削除し、runtime UI は route declaration へ移す |
| `SpawnRuntimeTemplateExecutor.cs` | registry resolve の surface は維持し、返る route adapter が変わるだけにする |
| `RuntimeAllDeleteExecutor.cs` | `IFilteredReleaseSpawnerService` を使う実装へ置換する |
| `LifecycleExecutors.cs` | `ISceneKernelReleaseGateway` 優先の release path に置換する |
| `BaseRuntimeTemplateSO.cs` | `SceneKernelSpawnContext` ベースの hook を追加し、`IScopeNode` hook を migration-only に落とす |
| `RuntimeManagerMB.cs` | migration utility による asset rewrite 完了後に削除する |
| `RuntimeLifetimeScope.cs` / `RuntimeLifetimeScopeBase.cs` | prefab / scene migration 完了後に target path から削除する |

### 9. Asset migration sequence

asset migration は code edit の後回しにしない。

順序:

1. scene ごとに `SceneKernel` root を persistent object として保存し、`SceneKernelHostMB` / `SceneKernelSpawnHostMB` / `SceneKernelSpawnDeclarationMB` を配置する
2. `RuntimeManagerMB` / `UIElementRuntimeSpawnerMB` の serialized data を declaration route / warmup list へ copy する
3. scene 内参照を new host / declaration へ付け替える
4. runtime prefab root の `RuntimeLifetimeScope` を `SceneKernelEntityInstanceMB` へ置換する
5. `EntityIdentityMB` が欠けている prefab は migration error として止める
6. scene / prefab を reserialize した後で旧 component を削除する

migration error にする条件:

- 同一 scene で `SpawnerKind` + `Tag` が重複する
- route root が消失している
- runtime prefab root に `EntityIdentityMB` seed が無い
- old scene が `RuntimeManagerMB` を複数持つのに declaration merge policy が決まらない

editor migration は silent repair をしてはならない。
merge policy が曖昧なら diagnostics で止める。

---

## Release / Despawn / Delete Contract

spawn system は、生成と破壊の mediation を必ず仲介する。

### 1. Spawn

spawn の標準順序は次の通り。

1. routing label から compatibility route を解決する
2. request を validator で検証する
3. prefab family を確定する
4. pool から acquire するか、new instantiate するかを決める
5. explicit attach / reparent を適用する
6. identity と generation を確定する
7. lifecycle spawn hook を呼ぶ
8. SceneKernel-owned entity lease table と compatibility adapter へ公開する

### 2. Release / despawn

release / despawn の標準順序は次の通り。

1. lifecycle despawn hook を呼ぶ
2. active state と transient state を reset する
3. generation / lease を invalidate する
4. pooling eligible なら pool へ返す
5. pooling 不可、または shutdown 中なら destroy する

### 3. Delete mediation

delete は caller の直接 `Destroy` ではなく、spawn system の release / destroy mediation で成立する。

bulk delete も同じである。

許可:

- registry を使った filtered release
- identity filter による bulk release
- scene shutdown による一括 destroy

禁止:

- hierarchy scan だけで delete すること
- caller が direct destroy を standard path にすること
- delete のために scene-wide discovery を行うこと

### 4. Double release / stale release

stale release と double release は structured failure である。

許可される結果は次のいずれかだけである。

- diagnostics を伴う no-op
- diagnostics を伴う explicit failure

禁止:

- silent double destroy
- silent pool contamination
- stale instance を live instance と誤認すること

---

## Diagnostics and Failure Policy

次は failure にする。

- spawner route が存在しない
- prefab / template が invalid
- pool family が確定できない
- compatibility fallback が明示されていないのに fallback する
- stale release / double release が発生する
- pool への誤返却が起きる
- hierarchy scan が runtime delete path に入る
- direct destroy が caller 側の standard path に入る

diagnostics では少なくとも次を追跡できる必要がある。

- route kind / tag
- prefab family
- requested parent / DI parent
- pool hit / miss
- acquire / release / destroy reason
- warmup result
- fallback usage
- stale / invalid / duplicate state

---

## Performance Rule

spawn / pool の hot path で次を行ってはならない。

- `GetComponentsInChildren` による discovery
- `FindObjectsByType` による repair
- reflection based construction
- runtime string-key fallback の多段探索
- parent walk による owner 推定
- bulk delete のための scene-wide search

必須:

- lookup は bounded であること
- pool access は prefab family に対して O(1) であること
- release はできるだけ allocation-free であること
- warmup と delete は explicit table で処理すること

---

## Migration / Replacement Mapping

| Current / Legacy Surface | v2.1 での位置付け | 置換方針 |
| --- | --- | --- |
| `RuntimeManagerMB` | spawn / warmup / telemetry / delete authority の旧実装 | `SceneKernel` 配下の unified spawn core と host / declaration bridge に置換し、asset migration 後に削除する |
| `RuntimeLifetimeScopePool` | parent-scoped pool authority の旧実装 | prefab-family pool に置換し、old pool authority は削除する |
| `RuntimeLifetimeScopeSpawnerService` | kind 別 runtime spawner の旧実装 | `SceneKernel` compatibility adapter に置換し、旧 class は削除する |
| `RuntimeLifetimeScope` / `SceneLifetimeScope` / `EntityLifetimeScope` / `UIElementLifetimeScope` | legacy runtime species / resolver host | `Entity` + `EntityRef` + SceneKernel-owned entity instance lease に置換し、new target path の public truth から外す |
| `SceneSpawnerRegistry` / `SceneSpawnerResolver` | compatibility routing table | exact route / explicit fallback のみを担う |
| `SpawnParams` | caller-facing compatibility DTO | 形は保ち、validator と mediator で意味を再定義する |
| `RuntimeAllDeleteCommandData` / `RuntimeLifetimeScopeDeleteFilter` | bulk delete request surface | registry-backed filtered release の入力に使う |
| hierarchy scan delete | legacy delete mechanism | registry-backed release に置換する |
| direct destroy helper | unsafe release path | mediator 内部のみで限定的に使う |

### Asset migration rule

`RuntimeManagerMB`、`RuntimeLifetimeScope` 系 component、またはそれらの派生 species を参照する scene / prefab は、07 の implementation scope に含める。

必要条件:

- replacement host / declaration を先に用意すること
- scene / prefab の参照を editor migration で置換できること
- temporary bridge を置く場合は quarantine に閉じ込めること
- asset reserialize 後に旧 manager / old runtime species を target path から削除すること

---

## 受け入れ基準

- 外部の spawn request surface は維持されている
- 内部の spawn authority は 1 つに統一されている
- pool reuse は parent ではなく prefab family で決まる
- same-parent-only reuse が消えている
- explicit reparent が可能である
- delete / despawn / destroy が mediator 経由になっている
- runtime delete に hierarchy scan が使われていない
- scene shutdown で pool が scene-local に閉じる
- fallback が必要な場合は diagnostics で見える
- new target path の public naming / public contract に `RuntimeLifetimeScope` / `RuntimeManager` の語が残っていない
- asset migration 完了後に旧 manager / old runtime species が target path の authority として残っていない

---

## テストケース

| テストケース | 目的 | 検証 |
| --- | --- | --- |
| `TC-V21-07-01` | 外部 API が unified core に接続されることを確認する | kind/tag 経由の spawn が単一の spawn core に到達し、内部 authority が複数に分かれていない |
| `TC-V21-07-02` | prefab-family pool が parent に依存しないことを確認する | 同一 prefab を異なる parent で spawn しても同じ pool family として再利用できる |
| `TC-V21-07-03` | explicit reparent が可能であることを確認する | spawn 後の parent 再接続が pool identity を壊さず、再利用に影響しない |
| `TC-V21-07-04` | bulk delete が mediator 経由であることを確認する | delete が registry / identity filter を使い、hierarchy scan に依存しない |
| `TC-V21-07-05` | stale / double release が安全に失敗することを確認する | 二重 release や stale handle に対して structured diagnostics が出て、二重 destroy しない |
| `TC-V21-07-06` | scene shutdown で spawn authority が閉じることを確認する | scene unload / SceneKernel disposal で pool が scene-local に破棄され、cross-scene leak が起きない |
| `TC-V21-07-07` | warmup が explicit pool family に対してのみ動くことを確認する | warmup が hierarchy discovery を使わず、prefab family ベースで初期化される |
<!-- end -->
