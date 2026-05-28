# Kernel v2.1 Command Cutover Milestone Specification

## 文書ステータス

- 文書 ID: `10_CommandCutoverMilestoneSpec`
- 状態: Draft
- 役割: v2.1 における command 専用の cutover milestone を定義し、存在するすべての command surface を一覧化して置換状態を追跡する
- 範囲: Assets/GameLib と Assets/Game にある、識別子に `Command` を含む class / interface / enum / struct / record / delegate surface と、`ICommandExecutor` 実装 surface
- 非目標: service surface、dynamic source surface、scene / prefab migration、spawn lifecycle、scalar / value migration、一般的な full replacement 定義

### 改訂メモ

command は件数が多く、代表例だけでは移行完了を宣言できない。
この文書は、command surface を 1 件も抜かさずに inventory 化し、`置換済み` / `進行中` / `隔離/削除対象` / `要差し替え` を明示するための milestone である。

canonical inventory は [Index/CommandCutoverInventory.md](Index/CommandCutoverInventory.md) である。

---

## 1. ミッション

command cutover milestone の目的は次の 4 つである。

1. current workspace に存在する command surface を全件列挙する
2. 各 command surface が new architecture に置換済みかどうかを記録する
3. legacy command surface を quarantine / deletion / replacement に振り分ける
4. shipped gameplay surface に残る command authority を 0 にする

この milestone では、1 件でも未把握の command surface が残っている限り完了とはみなさない。

---

## 2. Inventory Baseline

current baseline は次の通りである。

| 指標 | 値 | 意味 |
| --- | --- | --- |
| 定義レコード | 574 | class / interface / enum / struct / record / delegate の command surface 定義数 |
| unique command surface | 558 | partial や dual-role surface を束ねた command surface 数 |
| class surfaces | 434 | command 実装または command host |
| interface surfaces | 39 | command contract / capability surface |
| enum surfaces | 53 | command IR / policy / mode surface |
| struct surfaces | 32 | command identity / payload / reference surface |
| `置換済み` | 23 | kernel-native command IR / plan / schema surface |
| `進行中` | 1 | explicit declaration companion を持つ surface は current scan で 1 件だけ確認済み |
| `隔離/削除対象` | 27 | legacy bootstrap / catalog / registry / fallback surface |
| `要差し替え` | 507 | current scan で new-path evidence がない |

この baseline は完了宣言の根拠ではなく、移行の出発点である。

---

## 3. 状態定義

### 3.1 `置換済み`

- command surface が Kernel-native である
- もしくは、既に verified new architecture の truth source にある
- 代表例を増やすのではなく、実際に target runtime へ入っていることを証跡で示す

### 3.2 `進行中`

- command surface に explicit new-path declaration companion がある
- ただし legacy surface はまだ workspace に残っている
- 置換作業は完了しておらず、`置換済み` に昇格するまで milestone 完了には数えない

### 3.3 `隔離/削除対象`

- `CommandRunnerMB` 系、`CommandExecutorRegistry` 系、`CommandCatalogService` 系、`CommandCatalogLocator` 系、`CommandCatalogSO` 系、`CommandKeyResolver` 系、`CatalogCommandSource` 系、`FunctionCommandSource` 系、`InlineCommandSource` 系、`NullCommandCatalog` 系、`NullCommandKeyResolver` 系、`VerifiedCommandPayloadSchemaCatalog` 系、またはそれに準ずる legacy authority surface
- runtime authority としての存続は認めない
- 残す場合でも quarantine または diagnostics-only に閉じる

### 3.4 `要差し替え`

- current scan で new-path evidence が見つからない command surface
- shipped gameplay surface に含まれる限り、いずれかの wave で replacement される必要がある
- 508 件の open surface があるため、この milestone はまだ未完了である

---

## 4. Command Cutover Rule

command replacement の判定は surface 単位で行う。

許可される再利用:

- command execution semantics
- pure data holder
- verified helper that does not own runtime authority

禁止される再利用:

- registration authority
- resolver authority
- catalog authority
- discovery authority
- `IReadOnlyList<ICommandExecutor>` を走査する discovery path
- `Resources.Load` fallback
- `CommandRunnerMB` bootstrap authority
- hidden adapter での延命
- command graph 外での暗黙 fallback

command surface は、class と interface だけでなく enum / struct / record / delegate も inventory する。
partial class は 1 surface として扱うが、すべての定義ファイルが同じ replacement state に達するまで `置換済み` にはしない。

---

## 5. Milestone Phases

この command milestone は、M8 の command transition と M11 / M12 の full replacement に対する前提になる。

### M8-C1: Inventory Freeze

- command surface を全件固定する
- new command surface の追加があれば同じ変更セットで inventory を更新する
- 574 definition records の baseline を fixture 化する

出口条件:

- inventory companion が current workspace と一致している
- 未把握の command surface がない

### M8-C2: Kernel-Native Verification

- `置換済み` surface を検証する
- target-native command IR / plan / schema surface が runtime authority を持つことを確認する

出口条件:

- `CommandIR`
- `CommandEntryPlan`
- `CommandCatalogPlan`
- `CommandPayloadSchemaPlan`
- `CommandExecutorRef`
- `CommandTypeId`

が new-path truth にある

### M8-C3: Legacy Bootstrap Withdrawal

- `隔離/削除対象` surface を quarantine または削除へ寄せる
- legacy catalog / registry / source / MB bootstrap を runtime authority から外す

出口条件:

- `CommandRunnerMB`
- `CommandExecutorRegistry`
- `CommandCatalogService`
- `CommandCatalogLocator`
- `CommandCatalogSO`

が quarantine-only である

### M8-C4: Runtime Command Cutover

- `要差し替え` surface を new command runtime path に載せる
- command authority が new path だけで起動・進行・終了することを確認する

出口条件:

- `要差し替え` が減少している
- shipped gameplay command surface に legacy authority が参加していない

### M11-C1: Asset / Scene Cutover

- shipped gameplay surface の command 参照を new host / catalog / executor / identity に付け替える
- scene / prefab の command authoring を field-preserving reserialize し、successor Authoring に差し替える

出口条件:

- 対象 asset が少修正で successor Authoring へ移行される
- old command bootstrap / registry / fallback / host MB の参照が target path から消える

### M12-C1: Residual Audit

- residual bridge / adapter / legacy command dependency を再監査する
- 旧 surface が runtime authority に入っていないことを確認する

### M12-C2: Release Gate

- direct play
- regression
- diagnostics
- performance

の 4 gate を通す。

---

## 6. Completion Conditions

command cutover milestone は、次を同時に満たしたときのみ完了とする。

- `要差し替え` が shipped gameplay surface で 0 である
- `進行中` が残る場合は quarantine-only であり、runtime authority を持たない
- `隔離/削除対象` が source / asset / manifest / serialized reference から排除されるか quarantine-only へ退く
- scene / prefab が command replacement に合わせて reserialize 済みである
- direct play / regression / diagnostics / performance の gate が通る

---

## 7. Review Notes

current baseline では `置換済み` が 23 件、`進行中` が 1 件ある。
つまり、command cutover は greenfield ではなく、kernel-native plan / schema / identity surface を既に持った状態から legacy bootstrap を剥がしていく作業である。

`進行中` は現時点で 1 件であり、CommandRunnerService が service-owned shell と provisional bridge を分離した状態にある。

この文書は command 専用であり、service surface と dynamic source surface は別 milestone で同じ構造を持つ。

---

## テストケース

| テストケース | 目的 | 検証 |
| --- | --- | --- |
| `TC-V21-10-01` | command inventory が全件列挙されていることを確認する | 574 definition records / 558 unique command surfaces が明記されていなければならない |
| `TC-V21-10-02` | 置換状態が command surface ごとに追跡されていることを確認する | `置換済み` / `進行中` / `隔離/削除対象` / `要差し替え` の 4 状態が定義されていなければならない |
| `TC-V21-10-03` | current baseline が shared truth として固定されていることを確認する | canonical inventory へのリンクがあり、baseline counts が記録されていなければならない |
| `TC-V21-10-04` | kernel-native command surfaces が明示されていることを確認する | `CommandIR` / `CommandEntryPlan` / `CommandCatalogPlan` / `CommandPayloadSchemaPlan` / `CommandExecutorRef` / `CommandTypeId` が `置換済み` として記録されていなければならない |
| `TC-V21-10-05` | legacy command authority が quarantine-only であることを確認する | `CommandRunnerMB` / `CommandExecutorRegistry` / `CommandCatalogService` / `CommandCatalogLocator` / `CommandCatalogSO` 系が隔離/削除対象とされ、`CommandRunner` は Entity 固定で MB なしに動くと書かれていなければならない |
| `TC-V21-10-06` | 完了条件が open surface 0 を要求していることを確認する | `要差し替え` = 0 が command milestone completion 条件として書かれていなければならない |
