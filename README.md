# TinnosukeGameLib

TinnosukeGameLib は、Unity 上で動くゲーム本体と、それを支える共有システム群 GameLib をまとめたリポジトリです。
ゲームロジックはコマンド駆動で組み立てられており、`DynamicValue` ベースのデータ表現、Expression / RichText による表示、独自の LifetimeScope による DI とライフサイクル管理を中心に構成されています。

この README は、現行実装の全体像をつかむための入口です。細かい挙動は各フォルダの個別ドキュメントを参照してください。

## レガシー扱いについて

このシステムは現在レガシー扱いです。
既存の構成は保守と互換性を優先して残しているため、新しい設計の理想形として読むのではなく、現行の実装を安全に運用するための土台として扱ってください。

特に次の点は、今後も前提として維持します。

- 既存の Scope 構造と生成済み VarId を壊さないこと
- 既存の命名や互換用のデータ表現を、必要以上に整理し直さないこと
- 大きな置き換えは、移行手順を含めて段階的に進めること

## ゲームの概要

本作は、盤面にヨーテムを置いて器の価値を上げ、下のスロットに流し込んでスコアを稼ぐ短時間プレイのゲームです。
ストーリーはありますが、毎回必ず濃く見せる構造ではなく、条件に応じて進行します。
そのため、1プレイのテンポとビルド構築の面白さを優先した設計になっています。

## 全体構成

このリポジトリは大きく次の層に分かれています。

- `Assets/GameLib` は共有システム層です。複数の機能から使われる基盤を置きます。
- `Assets/Game` はゲーム固有の層です。Flow、Player、UI など、この作品専用の接続部分が入ります。
- `Assets/Docs` は設計メモと参照資料です。Expression や RichText、StatusEffect の説明があります。
- `Assets/Plugins` は外部パッケージやエディタ拡張です。
- `Assets/Resources` はランタイムで参照されるレジストリや設定アセットを置く場所です。
- `Assets/Scenes` はシーン群です。
- `Assets/Game/Scripts/Generated` や各種 Generated フォルダには、生成コードが入ります。

### `GameLib` の主な責務

`Assets/GameLib/Script` には、汎用システムが責務ごとに分かれて入っています。

- `Base` は LifetimeScope と基盤的な DI / スコープ管理です。
- `Common` は Variables、Commands、StatusEffect、Trait、Movement、Audio、AI、Spawn、Time などの共通機能です。
- `Collision` は当たり判定、ヒットルーティング、Unity 連携、ジョブ実装です。
- `Shader` は MaterialFx、NoiseProducer、TextureEffect などの描画系です。
- `Platform` はプラットフォーム固有の差分吸収です。
- `Project` はこのゲームに特化した統合機能です。UI、Scene、Transform、SharedTexture などが入ります。

## システムの見方

このリポジトリの中心にあるのは、次の 4 つです。

### 1. Scope を軸にした DI とライフサイクル

このプロジェクトは VContainer を参考にした独自の LifetimeScope を使いますが、標準的な VContainer の使い方そのままではありません。
`[Inject]` は使わず、Installer 側で Resolver から明示的に解決します。
サービスの初期化と破棄は `IScopeAcquireHandler` と `IScopeReleaseHandler` に寄せており、`IStartable` / `IInitializable` は使いません。

この方針は、BaseLifetimeScope と RuntimeLifetimeScope を両立させつつ、スコープの所有関係を明示しやすくするためのものです。

### 2. DynamicValue 中心のデータ表現

多くの設定値やゲーム中の値は `DynamicValue<T>` を通して扱います。
固定値、式、参照、外部入力を同じ流れで扱えるため、Inspector での authoring と runtime の更新を分けすぎずに運用できます。

変数のキー管理は `VarKeyRegistry` と生成された `VarIds` に寄せています。
手書きの定数ファイルを増やすより、生成済みの識別子を使う前提です。

### 3. Expression と RichText の共有エンジン

Expression は数値、bool、Vector2、Vector3 などの計算に使われます。
RichText はその同じ式エンジンを使って、表示用の文言や数値整形、条件付き色分け、共有ラベルの再利用を行います。

関連ドキュメントは [Assets/Docs/ExpressionFunctions.md](Assets/Docs/ExpressionFunctions.md) と [Assets/Docs/RichText.md](Assets/Docs/RichText.md) です。

### 4. コマンド駆動のゲームロジック

状態遷移、UI 操作、エフェクト発火、スポーン、デバッグ操作の多くはコマンドとして実装されています。
コマンドは単なる関数ではなく、Scope と VarStore、Blackboard、対象オブジェクトの関係を前提に実行されます。

このため、ゲームの挙動は「どのコマンドが、どの Scope で、どの値を読んで、何を書き換えるか」という視点で追うと理解しやすいです。

## 主要システム

### Variables / Blackboard / VarStore

ゲーム全体で使う状態保持層です。
`VarStore`、`Blackboard`、`DynamicValue`、`VarKeyRegistry`、生成された `VarIds` が相互に連携します。
Transform や UI、Trait、StatusEffect のような別システムでも、最終的にはここに値が集まることが多いです。

### Commands / StateMachine / Flow

コマンド実行基盤、監視処理、ゲームフロー、状態遷移の中心です。
`Assets/Game/Scripts/Flow` にはゲーム固有の状態機械や開始処理があり、`Assets/GameLib/Script/Common/Commands` には共通のコマンド群があります。

### StatusEffect / Trait / Item / Health

効果や状態の蓄積、定義とランタイムの分離、説明文の生成、プレイヤー状態との連携を扱います。
StatusEffect は専用の説明書があり、定義側とランタイム側で使う var 群が分かれています。
詳細は [Assets/GameLib/Script/Common/StatusEffect/README_StatusEffect.md](Assets/GameLib/Script/Common/StatusEffect/README_StatusEffect.md) を参照してください。

### Collision

2D コライダー管理、ヒット判定、ルーティング、Unity 連携、ジョブ実装をまとめた領域です。
単純な当たり判定だけでなく、命中結果をコマンドや Trait、UI へ流すための橋渡しも行います。

### UI

Tooltip、Toast、TraitList、VisualBounds、Navigation、Input、Selection、Scroll、Spawner など、画面上の相互作用を広く扱います。
UI は見た目だけではなく、スコープ管理とコマンド連携まで含めて構成されています。

### Scene / Transform

TransformChannelHub、TransformAnimation、SharedTexture、TextureEffect など、シーン内オブジェクトの制御と表現を扱います。
単に移動や回転をするだけでなく、チャンネル化された制御やアニメーション出力を前提にしています。

### Shader / MaterialFx

見た目の変化を担当する層です。
MaterialFx、NoiseProducer、TextureEffect などがあり、描画パラメータをコード側から扱いやすくしています。

### Common utilities

Audio、Animation、Movement、Spawn、Emitter、Time、AI、Utility など、ゲーム全体の補助機能がまとまっています。

## 開発時の注意

- 新しいサービスは、初期化と後処理を `IScopeAcquireHandler` / `IScopeReleaseHandler` に寄せてください。
- Resolver を `TryResolve<T>` するファイルでは、必要に応じて `using VContainer;` を追加してください。
- コマンドを追加したら、`CommandRunnerMB` への登録を忘れないでください。
- `DynamicSource` 系の型を増やした場合は、エディタ側の配線も必ず確認してください。
- enum を追加するときは、数値を明示してください。
- 例外を握りつぶす実装は避けてください。失敗は失敗として見える形で扱ってください。
- 生成コードや生成アセットは、手で直すより生成元を更新する方を優先してください。
- 既存の legacy 名称や flat group が残っている場合は、互換のためのものとして扱ってください。

## 参照ドキュメント

- [Game Documents](Assets/Docs/GameDocuments.md)
- [GameLib Docs Root](Assets/Docs/README.md)
- [Expression Functions](Assets/Docs/ExpressionFunctions.md)
- [RichText](Assets/Docs/RichText.md)
- [StatusEffect README](Assets/GameLib/Script/Common/StatusEffect/README_StatusEffect.md)

## ビルド

このワークスペースでは、.NET のビルド確認に `TinnosukeGameLib.slnx` を使います。

```powershell
dotnet build TinnosukeGameLib.slnx -v minimal
```

もし `dotnet` が PATH にない場合は、Windows 側の `dotnet.exe` を直接使ってください。
Unity 側のスクリプト変更は、エディタの再コンパイル完了を待ってから確認してください。
