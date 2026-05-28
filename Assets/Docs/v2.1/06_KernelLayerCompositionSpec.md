# Kernel v2.1 Kernel Layer Composition 仕様

## 文書ステータス

- 文書 ID: `06_KernelLayerCompositionSpec`
- 状態: Draft
- 役割: v2.1 で `ApplicationKernel` と `SceneKernel` の 2 層をどう定義し、V2 の既存 kernel 部品をどうまとめて再利用するかを定義する
- 範囲: DDOL / scene-local の kernel 分割、boot / scene handoff、App-wide service ownership、scene-local service ownership、V2 boot/runtime component mapping、legacy LTS 対応
- 非目標: 最終クラス名の固定、Scene 内部の全 service port、プロジェクト全体の scene transition policy の最終確定

### 改訂メモ

v2.1 では `SceneKernel` だけを考えると、application-wide な責務を別の legacy scope が吸収してしまい、二重実装の原因になる。

そのため、kernel は 2 層に分ける。

- `ApplicationKernel`: DDOL。ゲーム全体の root。旧 `PlatformLTS` / `GlobalLTS` / `ProjectLTS` に近い役割
- `SceneKernel`: scene-local。シーン内に 1 つだけ置かれる root。旧 `SceneLTS` に近い役割

この 2 層は、V2 の boot/runtime 実装を捨てるためのものではない。
むしろ、V2 の boot/runtime 部品をどこに置くかを整理するための composition layer である。

実装上の rule:

- `GameLib.Kernel.Layers.Core` は V2 `Boot` / `Diagnostics` に直接依存しない
- V2 部品を kernel pair に差し込む責務は `GameLib.Kernel.Layers.Composition` が持つ
- `ApplicationKernel` / `SceneKernel` は core-side contract を持ち、V2 concrete type は composition layer から attach される
- `SceneKernel` は entity registration intake に加えて spawn / despawn / pool mediation も所有する

---

## 所有範囲

この仕様が所有するもの:

- `ApplicationKernel` の責務
- `SceneKernel` の責務
- 2 層 kernel の所有境界
- DDOL と scene-local の分離
- V2 boot/runtime 部品の配置先
- kernel 間 handoff と ownership transfer
- spawn / despawn / pool mediation の所有境界
- legacy `PlatformLTS` / `GlobalLTS` / `ProjectLTS` / `SceneLTS` の置換位置

この仕様が所有しないもの:

- entity-scoped `ServiceGraph` の内部 algorithm
- command/value/scalar の個別意味論
- UI graph の内部 algorithm
- final bootstrap entrypoint の class 名

---

## 目的

この仕様の目的は次の通り。

```text
1. ApplicationKernel と SceneKernel を別の authority として明示する。
2. V2 の boot/runtime 部品を、二重実装せずにどちらの kernel に属するか決める。
3. scene-local と game-global を混ぜない。
4. 旧 LTS の役割を 2 層 kernel に吸収し、残骸を新アーキテクチャへ持ち込まない。
```

中心ルール:

```text
ApplicationKernel は scene-local details を所有しない。
SceneKernel は application-wide boot と persistent state selection を所有しない。
```

---

## kernel model

### 1. ApplicationKernel

`ApplicationKernel` は DDOL に存在する。

役割:

- game-wide boot authority
- boot manifest / profile selection
- app-wide diagnostics sink
- cross-scene shared service ownership
- persistent/global state coordination
- scene load / unload orchestration
- current SceneKernel の生成・破棄管理

旧システムで近いもの:

- `PlatformLTS`
- `GlobalLTS`
- `ProjectLTS`

ApplicationKernel がしてはならないこと:

- scene 内の entity registration を直接持つこと
- transform hierarchy を authority にすること
- scene-local UI graph を直接所有すること
- scene-local spawn / pool authority を ApplicationKernel 側へ戻さないこと
- `SceneKernel` の内部 entity registry を覗くこと

### 2. SceneKernel

`SceneKernel` は scene-local に 1 つだけ存在する。

役割:

- scene-local authority
- entity registration intake
- entity-scoped `ServiceGraph`
- spawn / despawn / pool mediation
- scene-local spawn mediation boundary / entity instance lease ownership
- scene-local lifecycle dispatch entry
- scene-local value / command / runtime query access boundary
- UI service-owned graph ownership

旧システムで近いもの:

- `SceneLTS`

SceneKernel がしてはならないこと:

- DDOL 的な application-wide persistence を持つこと
- boot manifest / profile を selection authority にすること
- cross-scene shared service の最終 authority になること

---

## V2 component mapping

V2 の既存 kernel 関連部品は、原則として新しい kernel 層の下敷きにする。

| V2 部品 | 役割 | v2.1 の配置先 |
|---|---|---|
| `KernelBootBoundary` | verified boot gate | ApplicationKernel |
| `KernelRuntime` / `KernelRuntimeShell` | runtime composition shell | ApplicationKernel が生成経路を所有し、SceneKernel には extracted boundary として渡す |
| `KernelBootRuntimeSurface` / Factory | runtime surface creation | ApplicationKernel |
| `KernelLifecycleDispatcher` | top-level lifecycle routing | ApplicationKernel が scene-local dispatcher を呼び出す形 |
| `KernelRuntimeServiceGraph` | verified service graph runtime | ApplicationKernel 直下の app/shared graph、または SceneKernel 直下の scene graph に分割投影 |
| `KernelRuntimeScopeGraph` / `KernelScopeGraphRuntime` | scope runtime | SceneKernel |
| `KernelBootManifest` / `KernelProfile` | boot input | ApplicationKernel |
| `KernelProjectionGenerator` | IR -> plan projection | boot 前の shared generation layer |
| `KernelDiagnosticService` | diagnostics | ApplicationKernel に置くが、SceneKernel からも sink できる |

重要なのは、V2 の部品を「もう 1 つ kernel を作るため」に複製しないことだ。
V2 の部品は、ApplicationKernel と SceneKernel のどちらに属するかを明示して再利用する。

`KernelRuntimeServiceGraph` は v2 core の verified runtime surface であり、SceneKernel の entity-scoped migration graph と同一ではない。
`ApplicationKernel` 直下の app/shared graph は cross-scene service のみを持ち、SceneKernel 直下の scene graph は scene-local service のみを持つ。
どちらも相手側への mutation 権限を持ってはならない。

特に `KernelRuntime` は raw object を SceneKernel の public truth にしない。
ApplicationKernel 側は `KernelBootBoundary` と `IKernelBootRuntimeSurfaceFactory` を所有し、
SceneKernel 側は verified runtime surface から extraction した次の boundary を使う。

- `KernelRuntimeServiceGraph`
- `KernelRuntimeScopeGraph`
- `KernelLifecycleDispatcher`
- `ILifecyclePlanResolver`

これにより、V2 runtime shell を再実装せずに使いながら、
scene-local service ownership と app-wide boot ownership を混線させない。

### Scene-local additions that are not legacy carry-over

v2.1 では、次の scene-local boundary を `SceneKernel` 側へ追加する。

- scene-local spawn / pool / release / delete mediation boundary
- entity instance lease table と bulk delete filter intake
- scene host / declaration bridge から受ける warmup / spawn settings input

これらは `RuntimeManagerMB` を別名で残す意味ではない。
旧 manager / old pool / old runtime species は deletion target であり、追加 boundary の owner にはならない。

---

## Boundary rules

### ApplicationKernel -> SceneKernel

ApplicationKernel は SceneKernel を生成し、破棄し、切り替える。

許可:

- scene load 時の SceneKernel creation
- scene unload 時の SceneKernel disposal
- app-wide diagnostics を scene-side から受けること

禁止:

- scene-local entity registry の直接操作
- scene-local UI graph の直接操作
- scene-local `ServiceGraph` の内部 slot mutation

### SceneKernel -> ApplicationKernel

SceneKernel は ApplicationKernel の提供する shared services を利用できる。

許可:

- boot 選択済み profile 参照
- app-wide diagnostics sink 利用
- shared/global service resolve

禁止:

- boot manifest を再選択すること
- persistent/global ownership を scene-local に戻すこと

---

## Legacy replacement mapping

| Legacy | 置換先 |
|---|---|
| `PlatformLTS` | `ApplicationKernel` |
| `GlobalLTS` | `ApplicationKernel` |
| `ProjectLTS` | `ApplicationKernel` |
| `SceneLTS` | `SceneKernel` |
| `RuntimeLifetimeScopeBase` | `ApplicationKernel` + `SceneKernel` の分割後に削除 |
| `RuntimeManagerMB` / `RuntimeLifetimeScopePool` / `RuntimeLifetimeScopeSpawnerService` | `SceneKernel` の spawn mediation boundary + host/declaration bridge へ置換後に削除 |
| `BaseLifetimeScopeRegistry` / `ScopeNodeHierarchy` | ApplicationKernel 側の残骸ではなく、target path から除去 |

このマッピングは「既存コードをそのまま名前変更する」意味ではない。
責務を 2 層へ分けたときに、どの残骸をどこへ移すかを示すだけである。

---

## Failure policy

次は failure にする。

- ApplicationKernel なしで SceneKernel を立てること
- SceneKernel なしで scene-local entity registration を始めること
- legacy LTS が 2 層 kernel の authority に残ること
- app-wide と scene-local の boundary が曖昧なこと

---

## Performance rule

2 層 kernel の追加で性能を悪化させてはならない。

必須:

- ApplicationKernel は DDOL でも lightweight に保つ
- SceneKernel は scene-local hot path に集中する
- kernel への hop は bounded である
- boot 時の composition を runtime hot path に流さない

---

## 受け入れ基準

- ApplicationKernel と SceneKernel の責務が分離されている
- ApplicationKernel が DDOL root である
- SceneKernel が scene-local root である
- V2 部品の再利用先が明示されている
- legacy LTS の役割が 2 層 kernel に吸収されている
- 二重実装を生む曖昧な authority がない

---

## テストケース

| テストケース | 目的 | 検証 |
|---|---|---|
| `TC-V21-06-01` | ApplicationKernel が DDOL root であることを確認する | boot / profile / diagnostics / cross-scene ownership が app-wide に定義されていなければならない |
| `TC-V21-06-02` | SceneKernel が scene-local root であることを確認する | entity registration / scene-local service ownership / UI graph ownership が scene-local に定義されていなければならない |
| `TC-V21-06-03` | V2 部品の配置先が明示されていることを確認する | `KernelBootBoundary`、`KernelRuntimeShell`、`KernelScopeGraphRuntime` などの mapping が書かれていなければならない |
| `TC-V21-06-04` | legacy LTS の置換位置が明示されていることを確認する | `PlatformLTS` / `GlobalLTS` / `ProjectLTS` / `SceneLTS` が 2 層 kernel に対応づけられていなければならない |
