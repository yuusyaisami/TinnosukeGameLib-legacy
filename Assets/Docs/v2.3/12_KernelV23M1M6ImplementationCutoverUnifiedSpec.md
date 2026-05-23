# 12_KernelV23M1M6実装差し替え統合仕様書

最終更新日: 2026-05-24
適用範囲: M1 〜 M6
文書種別: 実装実行仕様（開始可能な最終マイルストーン定義）

## 1. 目的

本仕様の目的は、現行の巨大な実行基盤を段階的に完全差し替えし、最終的に次を達成することである。

- 旧システムの実行権限経路を許可実行経路から物理削除
- 新アーキテクチャ内部への差し替え完了
- 実行経路を検証済み計画駆動・明示依存・fail-closed に統一
- 互換面を直列化継続専用かつ非権限へ固定

## 2. 非目的

- 証跡文書だけ整えて完了扱いにすること
- 旧経路を残したまま「現状動作」を理由に許可すること
- 動的探索、暗黙補完、静かなフォールバックを残すこと

## 3. 用語

- 許可実行経路: 本仕様で唯一許可される実行権限経路
- 旧経路: 旧設計由来の権限経路、探索経路、互換経路
- 互換面: 移行都合で一時残置する直列化継続面
- 完全差し替え: 旧経路が許可実行経路から物理削除された状態

## 4. 現状アーキテクチャ理解（実コード基準）

本仕様は次の実コードを現状の事実として扱う。

### 4.1 現在の DI 登録・解決の中心

- Assets/GameLib/Script/Common/Scope/Runtime/Core/RuntimeResolverHub.cs
	- RuntimeContainerBuilder が Register 系 API を提供し、Build で RuntimeResolver を構築している。
- Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs
	- KernelScopeHost が scope 単位の build/acquire/release を実行し、互換シェル RuntimeLifetimeScope が残存している。
- Assets/GameLib/Script/Common/Scope/Core/ScopeFeatureInstallerUtility.cs
	- TryGetScopeNode が親 Transform たどりで scope を探索する経路を持つ。
- Assets/GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs
	- CommandRuntimeInstaller が builder 登録を行い、実行系を構成する。

### 4.2 現在の問題（この仕様で解消する対象）

- scope-local 登録と動的探索が混在し、許可実行経路の境界が曖昧。
- 実行時のサービス形態が統一されておらず、Service-Entity と AoS の切替規則が未固定。
- 旧互換面が実行権限に接触できる余地が残っている。

## 5. 目標アーキテクチャ（最終形）

### 5.1 Service-Entity 形態

- 単位: Scene または Scope に対応するサービスインスタンス単位
- 所有: Kernel 側レジストリが所有（scope 側は所有しない）
- 参照キー: ScopeHandle または同等の安定 ID
- 実行: Kernel の計画駆動コマンド経由のみ

### 5.2 AoS 形態（EntityRef キー）

- 単位: サービス内部のスロット配列
- 所有: Kernel 所有のサービス本体
- 参照キー: EntityRef
- 実行: EntityRef をキーにスロットを更新し、局所 DI を介在させない

### 5.3 共通制約

- MB は宣言専用。実行権限を持たない。
- 動的探索での権限取得は禁止。
- 失敗は fail-closed。暗黙継続は禁止。

## 6. 移行方式（DI 登録から Service-Entity/AoS への変換）

### 6.1 登録面の変換規則

- 現在の Register/As/Build に依存する scope-local 登録点を列挙。
- 各登録点を次のどちらかへ強制分類。
	- Service-Entity
	- AoS（EntityRef キー）
- 未分類登録点は次工程へ進めない。

### 6.2 Service-Entity への変換手順

1. 旧登録点で生成している実行インスタンスを特定。
2. 生成責務を Kernel 側のインスタンスレジストリへ移管。
3. Scope 側は宣言情報のみを渡す。
4. 旧 resolver 直結呼び出しを計画駆動呼び出しへ置換。

### 6.3 AoS への変換手順

1. 旧実装の可変状態を抽出。
2. 状態を EntityRef キーのスロットへ再配置。
3. 読み書き API を EntityRef 前提へ統一。
4. scope-local コンテナ経由アクセスを削除。

### 6.4 旧経路の遮断手順

- 親 Transform たどりなど探索由来の権限決定を削除。
- 互換シェル経由の build/acquire/release 権限経路を遮断。
- release 相当設定で旧経路利用を即拒否する。

## 7. M1〜M6 詳細マイルストーン（実装開始版）

### M1 実態棚卸し・凍結

#### M1-1 旧経路台帳確定

着手条件:
- M0 契約凍結済み

実装作業:
- RuntimeContainerBuilder 登録点を全列挙
- KernelScopeHost build/acquire/release 経路を全列挙
- ScopeFeatureInstallerUtility の探索経路を全列挙
- すべての経路にファイル/シンボル/呼び出し連鎖を付与

完了条件:
- 未分類経路ゼロ
- source anchor 欠落ゼロ

#### M1-2 置換先マッピング確定

実装作業:
- 旧経路ごとに次のいずれかへ1対1で確定
	- Service-Entity
	- AoS（EntityRef キー）
- 置換不能経路は即ブロッカー登録

完了条件:
- 置換先未定義ゼロ

#### M1-3 変更凍結ルール有効化

実装作業:
- 差し替え対象外の拡張実装を凍結
- 凍結違反時の拒否条件を明示

完了条件:
- 凍結ルール承認済み

### M2 中核 command 面切替

#### M2-1 実行入口統一

実装作業:
- command 実行入口を検証済み実行セッション必須へ統一
- CommandRunner 経路で非検証入口を削除

完了条件:
- 非検証セッションで実行不可

#### M2-2 旧入口削除

実装作業:
- 旧 catalog、旧 discovery、旧フォールバック入口を物理削除
- RuntimeResolver 直接依存の実行入口を計画駆動へ置換

完了条件:
- 旧入口参照ゼロ

#### M2-3 失敗明示化

実装作業:
- 拒否コードと診断項目を固定し、無音継続を禁止

完了条件:
- 失敗時に必ず診断を返却

### M3 葉スコープ降格・互換境界固定

#### M3-1 葉ドメイン旧権限除去

実装作業:
- entity/ui-element 系を優先して旧権限経路を除去
- サービスごとに Service-Entity または AoS へ移行実装

完了条件:
- 葉ドメイン許可経路で旧権限到達不可

#### M3-2 互換シェル非権限化

実装作業:
- 互換シェルを直列化継続専用へ固定
- RuntimeLifetimeScope 経由の権限処理を段階的に切離

完了条件:
- 互換シェル経由の実行権限取得不可

#### M3-3 参照継続保証

実装作業:
- 名前・参照の継続性を維持しつつ内部差し替え

完了条件:
- 参照切れゼロ

### M4 ルートシーン統合切替

#### M4-1 起動契約一本化

実装作業:
- ルート起動を計画先行契約へ統一
- root/scene 登録を Service-Entity/AoS 変換後の新契約へ接続

完了条件:
- 非計画起動経路ゼロ

#### M4-2 順序決定論化

実装作業:
- 登録・構築・活性化順序を固定

完了条件:
- 再実行で順序ドリフトなし

#### M4-3 回り込み削除

実装作業:
- root/scene の旧回り込み経路を削除

完了条件:
- 回り込み経路到達不可

### M5 旧系削除・ハードニング・性能確定

#### M5-1 旧経路物理削除

実装作業:
- 旧権限経路を disable ではなく削除で除去
- RuntimeResolverHub の旧経路補助 API と探索依存を削除

完了条件:
- 旧権限経路残存ゼロ

#### M5-2 失敗経路ハードニング

実装作業:
- 拒否分類と診断を実行時に確定

完了条件:
- 必須拒否コード欠落ゼロ
- フォールバック観測ゼロ

#### M5-3 予算確定

実装作業:
- 基準値と実測値比較で性能予算判定
- Service-Entity と AoS の両形態で予算検証を分離記録

完了条件:
- 未解決性能違反ゼロ

#### M5-4 互換面退役判定

実装作業:
- retain/remove-later を行単位で閉鎖判定

完了条件:
- 期限・退出条件未設定ゼロ

### M6 完全証明・公開判定

#### M6-1 完了証明統合

実装作業:
- M1〜M5 証明を統合し、トレース整合を確認
- 各サービスが Service-Entity または AoS のどちらで完了したかを全件証明

完了条件:
- 証明断絶ゼロ

#### M6-2 独立レビュー

実装作業:
- 独立レビューで主張整合を審査

完了条件:
- 重大指摘未解決ゼロ

#### M6-3 公開判定

実装作業:
- 公開可否を最終決定

完了条件:
- 判定記録が承認済み

## 8. 開始直後に実行する順序（実装開始セット）

1. M1-1 旧経路台帳確定
2. M1-2 置換先マッピング確定
3. M2-1 実行入口統一
4. M2-2 旧入口削除
5. M3-1 葉ドメイン旧権限除去

## 9. 各サブマイルストーン共通の必須出力

- 変更ファイル一覧（削除/置換理由付き）
- 変更シンボル一覧（追加/削除/移設）
- コンパイル結果
- fail-closed 検証結果
- 旧経路不達の実行観測
- サービス形態判定表（Service-Entity / AoS）
- EntityRef キー化済み一覧（AoS 対象のみ）

## 10. 共通ブロック条件

次が1つでも残る場合、次サブマイルストーンへ進まない。

- 旧権限経路到達性が残存
- 必須拒否コードまたは診断項目の欠落
- フォールバック観測あり
- 未解決性能違反あり
- 互換面の期限・退出条件未設定
- Service-Entity / AoS 未分類のサービスが残存
- EntityRef キー化未完了の AoS 対象が残存

## 11. 最終到達条件（M6 完了条件）

以下をすべて満たした場合のみ完了とする。

- 旧システムの実行権限経路が許可実行経路から物理削除済み
- 新アーキテクチャ内部差し替えが完了
- 必須互換面が直列化継続専用かつ非権限
- 実装証明、性能証明、独立レビューが承認済み

## 12. 初期移行キュー（実装開始時の固定対象）

このキューは M1-1/M1-2 の初期入力として扱う。最終確定は M1-2 で行う。

| QueueId | 現行コード位置 | 現行挙動 | 目標形態 | 実装アクション | 完了判定 |
| --- | --- | --- | --- | --- | --- |
| Q-001 | Assets/GameLib/Script/Common/Scope/Runtime/Core/RuntimeResolverHub.cs の RuntimeContainerBuilder.Register/Build 系 | scope-local 登録と resolver 構築 | Service-Entity / AoS 両方の登録入口 | 登録責務を Kernel 側登録経路へ移管し、直接 Build 依存を縮退 | 新規追加コードで局所 Build 呼び出しゼロ |
| Q-002 | Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScope.cs の KernelScopeHost build/acquire/release 経路 | 互換シェルを含む scope 主導の実行遷移 | Service-Entity 主導 | 実行権限を Kernel 側へ寄せ、互換シェルは非権限化 | 互換シェル経由で実行権限取得不可 |
| Q-003 | Assets/GameLib/Script/Common/Scope/Core/ScopeFeatureInstallerUtility.cs の TryGetScopeNode | 親 Transform 探索で scope 決定 | 共通（探索禁止） | 探索依存を削除し、明示 owner/ID 連結に限定 | 親たどりでの権限決定経路ゼロ |
| Q-004 | Assets/GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs の CommandRuntimeInstaller.Install | builder 登録で command 実行構成 | Service-Entity 主導（計画駆動） | 非検証入口を削除し、検証済み実行セッション必須化を徹底 | 非検証セッションで実行不可 |
| Q-005 | Assets/GameLib/Script/Common/Variables/Blackboard/MB/BlackboardMB.cs | 互換橋面が残存 | AoS（EntityRef キー）または直列化継続専用 | 可変状態を AoS スロットへ移し、MB は宣言専用へ固定 | EntityRef キー化完了かつ MB 非権限 |
| Q-006 | Assets/GameLib/Script/Common/Scope/Runtime/RuntimeLifetimeScopePool.cs | scope ベースの取得/再利用 | Service-Entity 主導 | プール利用時の実行権限経路を Kernel 側契約へ統合 | scope-local 権限経路が再流入しない |

### 12.1 Q-001〜Q-006 実行順

1. Q-003（探索経路遮断）
2. Q-001（登録入口移管）
3. Q-004（command 実行入口固定）
4. Q-002（互換シェル権限除去）
5. Q-005（Blackboard の AoS/宣言専用化）
6. Q-006（プール経路統合）

### 12.2 各キュー完了時の必須証拠

- 変更前後のシンボル差分
- 呼び出し経路差分
- 失敗系の fail-closed 検証結果
- 旧経路不達の実行ログ

