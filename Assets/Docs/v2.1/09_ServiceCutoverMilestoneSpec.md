# Kernel v2.1 Service Cutover Milestone Specification

## 文書ステータス

- 文書 ID: `09_ServiceCutoverMilestoneSpec`
- 状態: Draft
- 役割: v2.1 における service 専用の cutover milestone を定義し、存在するすべての service surface を一覧化して置換状態を追跡する
- 範囲: Assets/GameLib と Assets/Game にある、識別子が `Service` で終わる class / interface surface
- 非目標: command surface、dynamic source surface、scene / prefab migration、spawn lifecycle、general full replacement

### 改訂メモ

service は件数が多く、代表例だけでは移行完了を宣言できない。
この文書は、service surface を 1 件も抜かさずに inventory 化し、`置換済み` / `進行中` / `隔離/削除対象` / `要差し替え` を明示するための milestone である。

canonical inventory は [Index/ServiceCutoverInventory.md](Index/ServiceCutoverInventory.md) である。

---

## 1. ミッション

service cutover milestone の目的は次の 4 つである。

1. current workspace に存在する service surface を全件列挙する
2. 各 service surface が new architecture に置換済みかどうかを記録する
3. legacy service surface を quarantine / deletion / replacement に振り分ける
4. shipped gameplay surface に残る service authority を 0 にする

この milestone では、1 件でも未把握の service surface が残っている限り完了とはみなさない。

---

## 2. Inventory Baseline

current baseline は次の通りである。

| 指標 | 値 | 意味 |
| --- | --- | --- |
| 定義レコード | 330 | class / interface の service surface 定義数 |
| unique service surface | 328 | partial を束ねた service surface 数 |
| class surfaces | 171 | service 実装または concrete service host |
| interface surfaces | 157 | service contract / capability surface |
| `置換済み` | 2 | new architecture に already anchored |
| `進行中` | 8 | explicit declaration companion を持つ |
| `隔離/削除対象` | 10 | legacy authority / quarantine / deletion-only |
| `要差し替え` | 308 | current scan で new-path evidence がない |

この baseline は完了宣言の根拠ではなく、移行の出発点である。

---

## 3. 状態定義

### 3.1 `置換済み`

- service surface が Kernel-native である
- もしくは、既に verified new architecture の truth source にある
- 代表例を増やすのではなく、実際に target runtime へ入っていることを証跡で示す

### 3.2 `進行中`

- service surface に explicit new-path declaration companion がある
- ただし legacy surface はまだ workspace に残っている
- current baseline では UI core 8 surface がこの状態にあり、dense handle metadata / ancestry-bounded lookup / declaration-backed binding を証跡として持つ
- 置換作業は完了しておらず、`置換済み` に昇格するまで milestone 完了には数えない

### 3.3 `隔離/削除対象`

- `Common/LTS` 系、`RuntimeLifetimeScope*` 系、`BlackboardService` 系、`DynamicObjectRegistryService` 系、`BaseScalarService` 系、またはそれに準ずる legacy authority surface
- runtime authority としての存続は認めない
- 残す場合でも quarantine または diagnostics-only に閉じる

### 3.4 `要差し替え`

- current scan で new-path evidence が見つからない service surface
- shipped gameplay surface に含まれる限り、いずれかの wave で replacement される必要がある
- 308 件の open surface があるため、この milestone はまだ未完了である

---

## 4. Service Cutover Rule

service replacement の判定は surface 単位で行う。

許可される再利用:

- service main logic
- pure data holder
- verified helper that does not own runtime authority

禁止される再利用:

- registration authority
- resolver authority
- installer mutation
- hierarchy fallback
- runtime discovery
- hidden adapter での延命
- service graph 外での暗黙 fallback

service surface は、class と interface を分けて inventory する。
partial class は 1 surface として扱うが、すべての定義ファイルが同じ replacement state に達するまで `置換済み` にはしない。

---

## 5. Milestone Phases

この service milestone は、M11 の service wave と M12 の verification の前提になる。

### M11-S1: Inventory Freeze

- service surface を全件固定する
- new service surface の追加があれば同じ変更セットで inventory を更新する
- 330 definition records の baseline を fixture 化する

出口条件:

- inventory companion が current workspace と一致している
- 未把握の service surface がない

### M11-S2: Kernel-Native Verification

- `置換済み` surface を検証する
- target-native service が runtime authority を持つことを確認する

出口条件:

- `IKernelDiagnosticService` と `KernelDiagnosticService` が new-path truth にある

### M11-S3: Declaration-Backed Cutover

- `進行中` surface を new declaration / authoring / plan に接続する
- legacy surface を残す場合は quarantine に限定する

出口条件:

- `ButtonChannelHubService`
- `IModalStackChannelHubService`
- `IUINavigationService`
- `IUISelectionService`
- `ModalStackChannelHubService`
- `UIElementStateService`
- `UINavigationService`
- `UISelectionService`

### M11-S4: Legacy Service Withdrawal

- `隔離/削除対象` surface を quarantine または削除へ寄せる
- shipped gameplay surface の legacy authority を消す

出口条件:

- `要差し替え` が減少している
- `隔離/削除対象` が runtime authority に参加していない

### M11-S5: Shipped Gameplay Service Cutover

- shipped gameplay surface の `要差し替え` を 0 にする
- scene / prefab の参照を new host / declaration / identity に付け替える
- service authority が new path だけで起動・進行・終了することを確認する

出口条件:

- `要差し替え` = 0
- shipped gameplay surface に legacy service authority が残っていない

### M11.2 Wave Execution Order

M11.2 の execution order は canonical inventory と shipped proof asset に従って固定する。

1. Wave 1: foundation services。`ApplicationShutdownService`、`TimeService`、`AudioService`、platform family、Event / Sync / RichTextRef を優先し、declaration / registration template を固める
2. Wave 2: input/control と camera family。TitleScene を narrow proof asset として boot/UI/button/command/service flow を確認する
3. Wave 3: GameScene core gameplay services。movement / rotation / direction / collision / channel hub family を representative gameplay proof に寄せる
4. Wave 4: gameplay orchestration services。Health / Status / Trait / Fire Pattern / Spawn / Emitter / Chunk を dependency order で切る
5. Wave 5: UI feature completion。M10.4 の UI core 8 surface を `置換済み` 相当まで閉じ、dialog / conversation / slider / tooltip / toast / scroll を追随させる
6. Wave 6: top orchestration。TitleScene -> GameScene transition を proof にし、game-specific top service authority を legacy host から外す

各 wave は同じ変更セットで canonical inventory を更新し、TitleScene または GameScene の proof を添えて state drift を防ぐ。

### M12-S1: Residual Audit

- residual bridge / adapter / legacy service dependency を再監査する
- 旧 surface が runtime authority に入っていないことを確認する

### M12-S2: Release Gate

- direct play
- regression
- diagnostics
- performance

の 4 gate を通す。

---

## 6. Completion Conditions

service cutover milestone は、次を同時に満たしたときのみ完了とする。

- `要差し替え` が shipped gameplay surface で 0 である
- `進行中` が残る場合は quarantine-only であり、runtime authority を持たない
- `隔離/削除対象` が source / asset / manifest / serialized reference から排除されるか quarantine-only へ退く
- scene / prefab が service replacement に合わせて reserialize 済みである
- direct play / regression / diagnostics / performance の gate が通る

---

## 7. Review Notes

current baseline では `進行中` が 8 件、`要差し替え` が 308 件ある。
つまり、この milestone は未完了であり、今後の wave で surface ごとに 0 へ収束させる必要がある。

この文書は service 専用であり、command surface と dynamic source surface は別 milestone で同じ構造を持つ。

---

## テストケース

| テストケース | 目的 | 検証 |
| --- | --- | --- |
| `TC-V21-09-01` | service inventory が全件列挙されていることを確認する | 330 definition records / 328 unique service surfaces が明記されていなければならない |
| `TC-V21-09-02` | 置換状態が service surface ごとに追跡されていることを確認する | `置換済み` / `進行中` / `隔離/削除対象` / `要差し替え` の 4 状態が定義されていなければならない |
| `TC-V21-09-03` | current baseline が shared truth として固定されていることを確認する | canonical inventory へのリンクがあり、baseline counts が記録されていなければならない |
| `TC-V21-09-04` | `進行中` service が限定的であることを確認する | UI core 8 surface (`ButtonChannelHubService` / `IModalStackChannelHubService` / `IUINavigationService` / `IUISelectionService` / `ModalStackChannelHubService` / `UIElementStateService` / `UINavigationService` / `UISelectionService`) が declaration-backed として記録されていなければならない |
| `TC-V21-09-05` | legacy service authority が quarantine-only であることを確認する | `Common/LTS` / `BlackboardService` / `DynamicObjectRegistryService` / `BaseScalarService` 系が隔離/削除対象とされていなければならない |
| `TC-V21-09-06` | 完了条件が open surface 0 を要求していることを確認する | `要差し替え` = 0 が service milestone completion 条件として書かれていなければならない |
