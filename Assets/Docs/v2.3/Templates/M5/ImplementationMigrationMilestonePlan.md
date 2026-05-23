# 実装移行マイルストーン計画

参照文脈: M5 削除・ハードニング・切替ライン
実行種別: 実コード実装マイルストーン（証跡起票専用ではない）
担当: 割当待ち
最終更新日: 2026-05-23
状態: 実行開始可能

## 目的

M5 の目的は文書作成そのものではない。
M5 実装の目的は次のとおり。

- 旧権限経路を C# 実行コードから物理削除する
- 許可実行経路を検証済み計画駆動に統一する
- 互換面は明示的に限定し、直列化継続専用かつ非権限でのみ残す

## ここまでに確定した方針

- M5.1..M5.6 は運用方針・拒否分類・進入ゲート論理を定義済み
- 実行証跡と承認が閉じるまで M6 は開始不可
- 以降の主軸はテンプレート拡張ではなく、コード削除とコード移行

## 実装対象範囲（コード）

現行コードで優先対象となる移行・削除箇所は次のとおり。

- `Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs`
  - `RuntimeLifetimeScope` / `KernelScopeHost` / `SpawnedLifetimeHandle` 周辺の互換シェルと混在フォールバック経路
- `Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScopePool.cs`
  - 旧スコーププール結合と実行時互換処理
- `Assets/GameLib/Script/Common/Scope/Runtime/Core/RuntimeResolverHub.cs`
  - 境界ロックが必要な旧解決器登録経路
- `Assets/GameLib/Script/Common/Scope/Core/ScopeFeatureInstallerUtility.cs`
  - 親たどり・スコープ探索フォールバック経路
- `Assets/GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs`
  - 互換橋として残存する面（隔離/削除候補）
- `Assets/GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs`
  - 検証済み実行セッション専用化を維持し、旧カタログ・旧探索の再流入を禁止

## マイルストーントラック

### M5-IMP-01 実行スコープ互換シェル権限削除

狙い:
- 実行スコープ経路から互換シェル由来の権限挙動を除去する

実装作業:
- 許可実行経路から `RuntimeLifetimeScope` 利用を削除または隔離
- `SpawnedLifetimeHandle` 周辺の非検証オーナー解決フォールバックを削除
- build/acquire/release を検証済み計画権限のみに統一

完了条件:
- 許可実行経路で互換シェル権限経由の生成・経路分岐が不可能
- 実行経路がコンパイル可能で、起動ライフサイクル検証が通過

### M5-IMP-02 解決器フォールバック・探索経路遮断

狙い:
- 検証済み計画宣言を迂回できる動的探索・フォールバックを物理削除する

実装作業:
- スコープ補助で親 Transform たどりによる権限決定を削除
- 明示オーナー/明示識別子連結のみ許可
- 必須オーナー不足時は fail-closed で停止

完了条件:
- 未解決権限時に継続せず、明示拒否へ遷移
- 明示オーナー成功とフォールバック拒否を試験で担保

### M5-IMP-03 旧 Blackboard 互換面移行

狙い:
- 実行権限を新系契約へ移し、旧 MB 互換面の権限依存を除去

実装作業:
- `BlackboardAuthoring` / `BlackboardMB` を直列化継続専用へ隔離、または許可実行経路から除外
- 実行初期化権限を検証済み契約/セッション経路へ移設
- 許可実行経路から旧実行登録依存を削除

完了条件:
- 許可実行権限が旧 Blackboard 互換挙動に依存しない
- 明示的に必要な直列化継続は維持

### M5-IMP-04 Command 実行旧面封鎖

狙い:
- 残存する旧 Command 入口を閉じ、検証済み実行橋のみを残す

実装作業:
- すべての許可実行経路で `VerifiedCommandRuntimeBridge` 必須を固定
- 実装中に検出された旧 executor/catalog フォールバック入口を削除
- 継続必要な authoring 面は非権限に限定

完了条件:
- 検証済み実行セッションなしで command 実行不可
- セッション欠落時は明示失敗と診断情報で停止

### M5-IMP-05 旧互換境界の最終切断

狙い:
- 一時互換隔離を終了し、削除負債を閉じる

実装作業:
- 期限切れ一時アダプタを削除し、残存一時項目に削除期限を明記
- release プロファイルで新規旧アダプタ/フォールバック流入を試験で失敗化
- 削除方針メタデータの追跡課題をクローズ

完了条件:
- release プロファイルで旧互換境界検証が fail-closed
- 削除条件未設定の一時アダプタが残存しない

## 実行順序

1. M5-IMP-02（フォールバック遮断）
2. M5-IMP-01（実行スコープ互換シェル権限削除）
3. M5-IMP-04（Command 旧面封鎖）
4. M5-IMP-03（旧 Blackboard 互換面移行）
5. M5-IMP-05（旧互換境界の最終切断）

理由:
- 先にフォールバックを遮断しないと、後続改修で旧経路が再流入するため

## 強制ブロック規則

- M5-IMP が1つでも未完了なら M6 を開始しない
- コンパイル成功のみでは完了としない
- 新規の探索権限経路/フォールバック経路検出時は M5 ゲートを再オープン

## 実装検証パック

各 M5-IMP で必須添付:

- 変更ファイル一覧（削除シンボルと置換呼び出し箇所）
- 変更対象アセンブリのコンパイル検証
- fail-closed と旧フォールバック不在を示す対象試験
- M5.2/M5.3/M5.4/M5.5 証跡への実行観測リンク

推奨検証コマンド:

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build TinnosukeGameLib.slnx -v minimal`
- `rg "RuntimeLifetimeScope|BlackboardAuthoring|LegacyCompat|fallback" Assets/GameLib/Script -n`

## M5 実装完了条件

次のすべてを満たしたときのみ M5 実装完了とする。

- すべての M5-IMP が完了し、証跡が添付されている
- 許可実行経路から旧権限経路が物理削除されている
- 残存互換面が直列化継続専用かつ非権限である
- M5.2..M5.6 の実行証跡がクローズし、必須成果物が承認済みである
