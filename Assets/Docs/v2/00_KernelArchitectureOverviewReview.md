# 00 Kernel Architecture Overview Review

## 状態

- 文書の役割: ルートアーキテクチャ草案に対するレビュー記録
- 範囲: 初版の Kernel Architecture Overview 草案に対するレビュー結果
- 目的: 現行プロジェクトとの不整合を洗い出し、下位仕様を書く前に 00 が修正すべき点を明確にすること
- レビュースタンス: 厳格、移行重視、実装根拠重視

---

## 要約

提案されている Kernel アーキテクチャの方向性は、概ね妥当である。
このプロジェクトには、検証済みの plan-first runtime、generated data に対するより厳密な trust boundary、legacy fallback 挙動の隔離が必要である。

ただし、現在の草案は同じ節の中で次の 3 種類の内容を混在させている。

1. 現行 runtime に関する観測結果
2. v2 に向けた設計目標
3. まだ正当化されていない最終形の API 形状

この混在が最大のリスクである。
00 がそのままだと、下位仕様は現行挙動に対する誤った前提と、置き換え先 runtime に対する過剰な固定前提を引き継いでしまう。

最も重要な修正は、00 を次の 2 層に分けることである。

- コードベースに根ざした現行アーキテクチャの観測
- 新しい kernel が保証すべき v2 の target policy

## テストケース

| テストケース | 目的 | 実行メモ |
|---|---|---|
| TC-RV-01 | 各高重大度指摘が、具体的なコードまたは設計根拠に紐づいていることを確認する。 | 各指摘の下にある anchor 一覧は、必ずソース根拠を維持すること。 |
| TC-RV-02 | 現行観測と v2 target policy が分離されていることを確認する。 | このメモは、事実レビューと target policy を 1 つの節に潰してはいけない。 |
| TC-RV-03 | 推奨する書き直し方針が、最終 API 形状を先送りにしていることを確認する。 | 最終 runtime の詳細は、下位仕様に残さなければならない。 |

---

## 高重大度の指摘

### 1. 草案は現行 runtime が実際に行っていることを過大に述べている

現行アーキテクチャが runtime discovery と authoring から runtime への広い結合に依存しているのは事実だが、草案には重要な差異を平坦化している記述がある。

コード上の観測からは、少なくとも次が確認できる。

- scope build は、単純な再帰走査ではなく、親の完了を前提とした調停付き処理である
- installer discovery は subtree ベースだが owner フィルタ付きであり、scope 配下の installer を無差別に集めるわけではない
- service resolution と runtime identity lookup は、すでに別メカニズムとして分かれている

具体的な根拠:

- `RuntimeLifetimeScopeBase.Build` と関連する build フロー: `Assets/GameLib/Script/Common/LTS/Runtime/RuntimeLifetimeScope.cs`
- installer ownership のフィルタリング: `Assets/GameLib/Script/Common/LTS/Core/ScopeFeatureInstallerUtility.cs`
- resolver と acquire/release dispatch の基盤: `Assets/GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs`

00 に必要な修正:

- 専用の「現行アーキテクチャの観測」節を追加する
- そこで述べる内容は、コードで直接裏付けられるものだけにする
- 一般化した anti-pattern の記述は、別の target policy 節へ移す

### 2. 草案は service resolution と runtime scope lookup を混同している

現行プロジェクトには、少なくとも 2 つの異なる runtime 機構がある。

- `RuntimeResolver` による型駆動の依存解決
- `BaseLifetimeScopeRegistry` 系 API による identity / filter 駆動の scope lookup

ルート草案は、新しい kernel が広い DI / runtime-discovery の塊を丸ごと置き換えるように読めてしまう。
これは移行仕様として曖昧すぎる。

この曖昧さは、command targeting、actor resolution、そして一部の scene-flow 挙動が、service resolution にそのままは対応しないためである。

具体的な根拠:

- `Assets/GameLib/Script/Common/LTS/Runtime/Core/RuntimeResolverHub.cs` の `RuntimeResolver`
- `Assets/GameLib/Script/Project/LTS/ProjectLifetimeScope.cs` から導入される registry 利用

00 に必要な修正:

- service graph の意味論と runtime lookup の意味論を明示的に分ける
- `ScopeGraph` が registry query を置き換えるのか、共存するのか、新しい query layer を要するのかを明言する
- 正確な query API は 06 と 07 に先送りする

### 3. command 節が、実際の移行課題を説明せずに target architecture へ飛んでいる

現行の command system は、単に「DI に多くの executor が登録されている」だけではない。
実態は次の複合構造である。

- `CommandRunnerMB` による大量 executor 登録
- `CommandExecutorRegistry` による registry ベースの ID lookup
- catalog services と key resolvers による key ベースの authoring 解決

つまり移行課題は、単なる performance の問題ではない。
意味論の統合も必要である。

具体的な根拠:

- `Assets/GameLib/Script/Common/Commands/MB/CommandRunnerMB.cs` の大量登録
- `Assets/GameLib/Script/Common/Commands/VNext/Core/CommandExecutorRegistry.cs` の executor registry

00 に必要な修正:

- 現行 command system の二重性を migration input として定義する
- `00` では authoring-key と runtime-ID の契約を最終確定しないと明記する
- executor のライフタイム方針と payload schema の詳細は 09 に残す

### 4. value 節が、現行の別責務を 1 つの target 文に潰している

現行プロジェクトには、次のように分かれつつも相互作用するシステムがある。

- Blackboard 階層と variant storage
- Var registry と stable key resolution
- DynamicValue と動的 source evaluation

「Blackboard を ValueStore に統合する」という方向性自体は妥当だが、00 の段階で書くには、まだ開いている migration decision を多く隠してしまう。

具体的な根拠:

- `Assets/GameLib/Script/Common/Variables/Blackboard/Service/BlackboardService.cs`
- `Assets/GameLib/Script/Common/Variables/VarStore/Registry/VarIdResolver.cs`
- `Assets/GameLib/Script/Common/Variables/VarStore/Registry/VarKeyRegistryLocator.cs`

00 に必要な修正:

- ValueStore の統合は target policy のみとして扱う
- 階層的 read/write 挙動、動的評価、保存意味論、authoring 参照は 10 に先送りすると明記する

### 5. boot の所有責務と scene integration が、移行に対して未定義すぎる

草案は `KernelBootManifest` と `KernelRuntime` を、現行 boot path を一気に置き換えられるもののように扱っている。
しかし現行 runtime boot は、`BeforeSceneLoad` 初期化と project / global scope のセットアップに密接に結びついている。

具体的な根拠:

- `Assets/GameLib/Script/Project/LTS/ProjectLifetimeScope.cs` の project bootstrap
- `Assets/GameLib/Script/Project/Global/LTS/GlobalLifetimeScope.cs` の global bootstrap
- `Assets/GameLib/Script/Project/System/SceneFlow/LoadingManager/Service/LoadingScreenService.cs` の scene-flow 挙動

00 に必要な修正:

- v2 の boot entry は、現行 bootstrap を段階的に置き換えるものだと明記する
- BootManifest は runtime input であり、boot migration 全体が解決済みであることの約束ではないと定義する
- 具体的な共存ルールは 05、12、13 に先送りする

---

## 中重大度の指摘

### 6. 00 が具体的な API 形状を早すぎる段階で固定しすぎている

現行草案には、説明用の C# 型やフィールドが多く含まれている。
説明としては有用だが、読者がそれを最終 runtime API と誤解すると危険である。

00 に必要な修正:

- 説明用の型は少数に絞る
- それらはすべて non-final の説明構造だと明示する
- 規範的な data layout ルールは 01、05、06、07、09、10、11 に移す

### 7. runtime 禁止の文言は方向として正しいが、移行ルートのルート仕様としては絶対的すぎる

ルート文書は target runtime contract を定義すべきである。
ただし、それが現行コードベースのすべてを、移行のあらゆる段階で即座に無効だと示唆してはいけない。

00 に必要な修正:

- target-kernel の禁止事項と、一時的な migration allowance を区別する
- 一時的な allowance はすべて 13 に向ける

### 8. hash と debug-map policy には、より明確な trust boundary の説明が必要である

現行草案は hash 検証と debug map を正しく重視しているが、ルート仕様では何を信頼してよいのかをもっと明確にすべきである。

00 に必要な修正:

- `KernelIR` を source of truth と定義する
- `VerifiedKernelPlan` を検証済み projection と定義する
- generated code と generated assets は transport / execution artifact であり、trust anchor ではないと定義する

---

## 軽重大度の指摘

### 9. 移行中の用語は、現行プロジェクトにより近づけるべきである

00 レベルでまったく新しい語彙だけを使うと、移行の曖昧さが増す。
現行コードベースには、`LifetimeScopeKind`、acquire / release handler、identity service、runtime registry など重要な概念がすでにある。

00 に必要な修正:

- migration input を説明するときは現行用語も併記する
- v2 用語は target language として維持する
- 旧名称が存在しなかった、あるいは概念的に無意味だったかのように書かない

### 10. 分割順は良いが、その理由を文書構造の中に埋め込むべきである

`01` と `04` を先に置き、その後に runtime specs を置く順序は正しい。
ただし、その優先順位は末尾の注記だけに現れていては弱い。

00 に必要な修正:

- 分割順をアーキテクチャの dependency structure に直接結びつける
- runtime specs が validated IR と graph rules に依存していることを明確にする

---

## 推奨する書き直し方針

00 は、次のトップレベル構成で書き直すべきである。

1. 目的と文書の役割
2. 現行アーキテクチャの観測
3. 解決すべき根本問題
4. v2 の target principles
5. 中核概念と trust boundary
6. runtime policy
7. Migration boundary
8. Specification split and dependency order
9. Success criteria

The rewrite should preserve the direction of the original draft while removing two kinds of ambiguity:

- ambiguity about the current codebase
- ambiguity about which details are intentionally deferred

---

## Review Outcome

The document should proceed.
It should not be discarded.

But it should proceed only after the root specification is rewritten so that:

- current-state observations are implementation-grounded
- v2 target rules are explicit and separated
- concrete runtime API shapes are treated as illustrative unless owned by a lower spec
- migration constraints are acknowledged rather than hidden

That is the threshold required before 01 and 04 can be written as real authority documents.
