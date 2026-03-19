<!--
Spec Version: v2.0
Status: Draft
Updated: 2026-03-17
Change Summary:
- v2.0 で v1.0 を全面差し替え。
- enum ベースの Effect 指定、Effect ごとの C# Runtime クラス、旧 ProfileSO/Preset 群を前提にした設計を破棄。
- DynamicValue<T> + SerializeReference + 薄い SO wrapper を中心にした新設計へ変更。
- 旧仕様との互換レイヤーは作成しない。既存 StatusEffect asset / code / command data は破壊的に置換する。
- 本書はコード変更を伴う大規模改修の事前仕様であり、現行コードを読んだ上で更新している。

Primary References Read:
- Assets/GameLib/Script/Common/StatusEffect/Core/StatusEffectService.cs
- Assets/GameLib/Script/Common/StatusEffect/Core/IStatusEffectService.cs
- Assets/GameLib/Script/Common/StatusEffect/Core/BaseEffectRuntime.cs
- Assets/GameLib/Script/Common/StatusEffect/Core/EffectContext.cs
- Assets/GameLib/Script/Common/StatusEffect/Core/EffectConfig.cs
- Assets/GameLib/Script/Common/StatusEffect/Core/EffectRuntimeTypes.cs
- Assets/GameLib/Script/Common/StatusEffect/Core/EffectStackMode.cs
- Assets/GameLib/Script/Common/StatusEffect/Core/EffectState.cs
- Assets/GameLib/Script/Common/StatusEffect/Core/StatusEffectIdUtility.cs
- Assets/GameLib/Script/Common/StatusEffect/Profile/PoisonEffectProfileSO.cs
- Assets/GameLib/Script/Common/StatusEffect/Effects/PoisonEffect.cs
- Assets/GameLib/Script/Common/Commands/VNext/Commands/StatusEffect/StatusEffectCommandData.cs
- Assets/GameLib/Script/Common/Commands/VNext/Core/CommandListData.cs
- Assets/GameLib/Script/Common/Commands/VNext/Core/CommandListRuntimeMutationService.cs
- Assets/GameLib/Script/Common/Variables/Dynamic/Core/DynamicValue.cs
- Assets/GameLib/Script/Common/Variables/Dynamic/Core/IDynamicSource.cs
- Assets/GameLib/Script/Common/Variables/Dynamic/Core/IDynamicValueAsset.cs
- Assets/GameLib/Script/Common/Variables/Dynamic/Core/DynamicSources.cs
- Assets/GameLib/Script/Common/Variables/Dynamic/Sources/StatusEffectSources.cs
- Assets/GameLib/Script/Common/_Editor/Dynamic/TypedDynamicValueDrawer.cs
- Assets/GameLib/Script/Common/_Editor/Dynamic/DynamicManagedRefSourceCatalog.cs
- Assets/GameLib/Script/Common/Variables/Profile/Core/BaseProfileData.cs
- Assets/GameLib/Script/Common/Variables/Profile/Core/IScopeBindingRegistry.cs
- Assets/GameLib/Script/Common/Variables/Profile/Core/ScopeBindingRegistryService.cs
- Assets/GameLib/Script/Generated/ScalarKeys.g.cs
- Assets/GameLib/Script/Common/Health/Core/BaseVisualData.cs
-->

# StatusEffect System Redesign Spec v2

## 1. 結論

StatusEffect は、現在の `BaseEffectRuntime` 継承方式と `StatusEffectId` enum 方式を捨てる。

新システムでは以下を採用する。

1. Effect の定義は `DynamicValue<T>` で扱える polymorphic data に統一する。
2. Effect の中身は `SerializeReference` された interface 群で表現する。
3. `StatusEffectService` は `IHealthService` 等を constructor 必須依存にしない。
4. Duration / Count は「interface + 使用 bool」で明示的に有効化する。
5. `Use` は独立したライフサイクルとして扱い、Count と逆 interval を内包する。
6. Hook command は `OnApply / OnRemove / OnEnable / OnDisable / OnUse / OnStackIntensity / OnStackDuration` を持つ。
7. Hook command は Apply 時に差し込める。既存 effect がある場合は append / override / clear を選べる。
8. 旧 StatusEffect 実装、旧 enum、旧 effect class、旧 profile wrapper は残さない。

本改修は互換性維持ではなく、StatusEffect の再定義である。

---

## 2. 現行実装の問題

### 2.1 依存が重すぎる

現行 `StatusEffectService` は constructor で以下を必須にしている。

- `IHealthService`
- `IBaseScalarService`
- `IBlackboardService`
- `IEntityEventService`
- `IScopeBindingRegistry`

このため、Health 系 effect を 1 つも使わない scope でも `IHealthService` が無いだけで `StatusEffectService` 自体の生成に失敗する。

現行 `EffectContext` も同じ方向の設計で、effect 未使用の service まで重依存になっている。

この構造は今後 20 個以上の effect を追加する前提と相性が悪い。

### 2.2 拡張単位が悪い

現行は以下の組み合わせで effect を増やしている。

- `StatusEffectId` enum
- `StatusEffectIdUtility.TryCreateRuntime`
- `BaseEffectRuntime` 派生 C# class
- effect ごとの profile SO / preset

この方式では effect を増やすたびに enum、factory、runtime class、profile wrapper を増やす必要がある。
大量の effect を扱うには不向きで、Editor からの差し替えも弱い。

### 2.3 Hook と stack の柔軟性が足りない

現行 hook は以下しかない。

- `OnApply`
- `OnStack`
- `OnUse`
- `OnRemove`

不足しているもの:

- `OnEnable`
- `OnDisable`
- `OnStackIntensity`
- `OnStackDuration`
- Apply 時の hook command 差し替え
- append / override / clear の runtime mutation

### 2.4 Duration / Count が常時前提になっている

現行 `EffectConfig` は `MaxUseCount > 0` などの値で機能有無を暗黙判定している。
今後は「Duration を使わない effect」「Use はあるが Count は使わない effect」「Count は使うが OnUse command は不要な effect」が増えるため、bool で明示しないと運用不能になる。

### 2.5 effect 指定が enum 前提

現行 command と DynamicSource は `StatusEffectId` enum を前提にしている。
これは少数の固定 effect には便利だが、今後の追加量と asset 差し替え前提には合わない。

---

## 3. 破壊的変更ポリシー

今回の改修では以下を明示的に採用する。

1. 旧仕様との互換レイヤーは作らない。
2. 旧 StatusEffect asset が壊れても構わない。
3. 旧 enum ベース command / source も新仕様に合わせて作り直す。
4. 旧 effect class 群は削除対象とする。

削除・全面置換の対象:

- `Assets/GameLib/Script/Common/StatusEffect/Effects/*.cs`
- `Assets/GameLib/Script/Common/StatusEffect/Profile/*.cs`
- `Assets/GameLib/Script/Common/StatusEffect/Core/BaseEffectRuntime.cs`
- `Assets/GameLib/Script/Common/StatusEffect/Core/EffectContext.cs`
- `Assets/GameLib/Script/Common/StatusEffect/Core/StatusEffectIdUtility.cs`
- enum ベースの `StatusEffectCommandData`
- enum ベースの `ActiveStatusEffectDescriptionsSource` filter

残してよいのは、`EffectStackMode` など新設計でも意味が通る基礎 enum と、Var / Event / UI で再利用可能なデータ定義のみ。

---

## 4. 新アーキテクチャの中心思想

### 4.1 ScopeBinding と同じく「定義中心」にする

StatusEffect も ScopeBinding と同様に、定義本体を SerializeReference で持つ。

方針:

- ScriptableObject は薄い wrapper にする
- 実データは serializable class に置く
- `DynamicValue<T>` で asset / inline / var 差し替えができるようにする
- 具体 effect の差は polymorphic data 側に押し込む

### 4.2 Effect 1 個 = 「共通 envelope + polymorphic operation 群」

新システムの effect は、1 つの巨大 C# runtime class ではなく以下で構成する。

1. 共通 envelope
2. optional Duration policy
3. optional Count policy
4. polymorphic operation list
5. hook command set

これにより、effect 数が増えても code の増加量を抑えられる。

### 4.3 capability は apply 時に遅延解決する

Health を使う operation は apply 時に `IHealthService` を取りに行く。
Scalar を使う operation は apply 時に `IBaseScalarService` を取りに行く。

service 生成時に全 capability を必須 DI しない。

これにより、Health 非使用 scope でも `StatusEffectService` を常駐できる。

---

## 5. 新しいデータモデル

### 5.1 ルート定義

ルートは interface と abstract base の二層で持つ。

```csharp
public interface IStatusEffectDefinitionData
{
    string DefinitionId { get; }
    StatusEffectPresentationData Presentation { get; }
    EffectStackMode DefaultStackMode { get; }
    bool UseDuration { get; }
    bool UseCount { get; }
    IStatusEffectDurationDefinition DurationDefinition { get; }
    IStatusEffectCountDefinition CountDefinition { get; }
    IReadOnlyList<IStatusEffectOperationDefinition> Operations { get; }
    StatusEffectHookSet DefaultHooks { get; }
}

[Serializable]
public abstract class BaseStatusEffectDefinitionData :
    BaseProfileData,
    IStatusEffectDefinitionData,
    IDynamicManagedRefValue
{
}
```

`BaseStatusEffectDefinitionData` を使う理由:

- `BaseProfileData` ベースにすると既存の `DynamicManagedRefSourceCatalog` と親和性が高い
- `DynamicValue<BaseStatusEffectDefinitionData>` を asset / inline で扱いやすい
- 今後 binding 的な保存や定義列挙が必要になっても拡張しやすい

### 5.2 薄い SO wrapper

```csharp
[CreateAssetMenu(menuName = "Game/StatusEffect/Definition", fileName = "StatusEffectDefinition")]
public sealed class StatusEffectDefinitionSO :
    ScriptableObject,
    IDynamicValueAsset<BaseStatusEffectDefinitionData>
{
    [SerializeReference, InlineProperty, HideLabel]
    BaseStatusEffectDefinitionData preset;

    public BaseStatusEffectDefinitionData Preset => preset;
}
```

注記:

- 初期値生成は `new()` ではなく、Editor 初期化時に具体型を入れる selector で行う
- `DynamicManagedRefSourceCatalog` には `BaseStatusEffectDefinitionData` 用の登録を追加する

### 5.3 共通 envelope

全 effect に必須な共通情報:

- `DefinitionId`
- `Presentation`
- `DefaultStackMode`
- `DefaultIntensity`
- `DefaultTag`
- `UseDuration`
- `UseCount`
- `Operations`
- `DefaultHooks`

`DefinitionId` は string の stable id とし、enum に戻さない。

命名規約:

- `StatusEffect.GameLogic.BallProfile.MaxValue`
- `StatusEffect.GameLogic.NailProfile.Effect.Bounce`

### 5.4 Presentation

表示データは RichText 対応済みの `BaseVisualData` 系に寄せる。

```csharp
[Serializable]
public sealed class StatusEffectPresentationData : BaseVisualData
{
    [LabelText("Effect Type")]
    public EffectType EffectType = EffectType.Neutral;

    [LabelText("Sort Order")]
    public int SortOrder = 0;
}
```

この `EffectType` は UI 分類用の metadata であり、effect 実装の分岐キーには使わない。

### 5.5 polymorphic operation

effect の中身は operation list で持つ。

```csharp
public interface IStatusEffectOperationDefinition
{
    bool TryBuild(StatusEffectBuildContext context, out IStatusEffectOperationRuntime runtime);
}

public interface IStatusEffectOperationRuntime
{
    void Apply();
    void Remove();
    void Enable();
    void Disable();
    void Reset();
}
```

初期実装で想定する concrete operation:

1. `ScalarModifierOperationDefinition`
2. `BoolLayerFlagOperationDefinition`
3. `CommandOnlyOperationDefinition`
4. `CompositeStatusEffectDefinition` または複数 operation の並列保持

この構造により、effect ごとの UI 差分は operation 側の SerializeReference field で表現する。

### 5.6 Scalar operation

ゲーム固有 effect の大半は scalar modifier なので、最初にこれを主軸にする。

```csharp
[Serializable]
public sealed class ScalarModifierOperationDefinition : IStatusEffectOperationDefinition
{
    public ScalarKey TargetKey;
    public ScalarModifierApplyMode ApplyMode;
    public ScalarMulPhase MulPhase;
    public DynamicValue<float> Value;
    public StatusEffectScalarValueMode ValueMode;
    public ActorSource TargetActorSource;
    public string Layer;
}

public enum ScalarModifierApplyMode
{
    Add = 10,
    Mul = 20,
}

public enum StatusEffectScalarValueMode
{
    DynamicValue = 10,
    RuntimeIntensity = 20,
}
```

仕様:

- `TargetActorSource` の default は Self 相当
- 将来的に他 LTS の scalar service を触れるように `ActorSource` を使う
- `ValueMode = RuntimeIntensity` の場合、Apply request の intensity をそのまま modifier 値として使う
- 実際の tag は runtime instance ごとに自動採番し、operation 単位で suffix を付ける

### 5.7 Duration は interface + bool

```csharp
public interface IStatusEffectDurationDefinition
{
    bool TryCreateController(StatusEffectBuildContext context, out IStatusEffectDurationController controller);
}

public interface IStatusEffectDurationController
{
    float TotalDuration { get; }
    float RemainingDuration { get; }
    bool IsExpired { get; }
    void Tick(float deltaTime);
    void Reset();
    void ApplyStack(in StatusEffectApplyRequest request, EffectStackMode mode, out bool changed);
}
```

定義側は必ず以下で持つ。

- `bool UseDuration`
- `[SerializeReference] IStatusEffectDurationDefinition DurationDefinition`

ルール:

1. `UseDuration == false` の場合、`DurationDefinition` は見ない
2. `UseDuration == true` でも `DurationDefinition == null` は不正データ扱い
3. Duration の有無は値ではなく bool で判定する

### 5.8 Count は interface + bool

```csharp
public interface IStatusEffectCountDefinition
{
    bool TryCreateController(StatusEffectBuildContext context, out IStatusEffectCountController controller);
}

public interface IStatusEffectCountController
{
    int MaxCount { get; }
    int UsedCount { get; }
    int RemainingCount { get; }
    bool CanUse { get; }
    bool ConsumeUse();
    void Reset();
}
```

定義側は必ず以下で持つ。

- `bool UseCount`
- `[SerializeReference] IStatusEffectCountDefinition CountDefinition`

Count も bool で有効化を明示する。

### 5.9 Nail 用 count source

Nail 系 effect で Count を使う場合、`MaxCount` の既定 source は次を使う。

- `ScalarKeys.GameLogic.NailProfile.Effect.MaxHitCount`

実装上は `DynamicValue<int>` を使う。
既定は Self scalar 参照だが、必要なら `OtherScalarSource` に差し替えられる設計にする。

---

## 6. Apply request と hook mutation

### 6.1 Apply request

Apply 時に effect 定義そのものと runtime 差分を一緒に渡す。

```csharp
[Serializable]
public sealed class StatusEffectApplyRequest
{
    public DynamicValue<BaseStatusEffectDefinitionData> Definition;
    public EffectStackMode StackMode;
    public DynamicValue<float> Intensity;
    public bool OverrideDuration;
    public DynamicValue<float> DurationOverride;
    public string RuntimeTag;
    public bool OverrideSourceActor;
    public ActorSource SourceActor;
    public StatusEffectHookMutationSet HookMutations;
}
```

重要:

- effect 指定は enum ではなく `DynamicValue<BaseStatusEffectDefinitionData>`
- 同じ command data でも source 差し替えで effect を変えられる
- `RuntimeTag` は coexist / filter / debugging 用に保持する

### 6.2 hook set

effect runtime は以下の hook slot を持つ。

- `OnApply`
- `OnRemove`
- `OnEnable`
- `OnDisable`
- `OnUse`
- `OnStackIntensity`
- `OnStackDuration`

### 6.3 hook mutation

Apply 時に渡す hook の変更要求:

```csharp
[Serializable]
public sealed class StatusEffectHookMutationSet
{
    public CommandListMutationStep OnApply;
    public CommandListMutationStep OnRemove;
    public CommandListMutationStep OnEnable;
    public CommandListMutationStep OnDisable;
    public CommandListMutationStep OnUse;
    public CommandListMutationStep OnStackIntensity;
    public CommandListMutationStep OnStackDuration;
}
```

`CommandListRuntimeMutationPipeline.Apply` をそのまま使う。

### 6.4 既存 effect がある場合の順序

同一 slot に effect が既に存在する場合は以下の順序で処理する。

1. Apply request の hook mutation を既存 runtime に反映
2. StackMode に従って intensity / duration を反映
3. `OnStackIntensity` / `OnStackDuration` を必要なものだけ実行

この順序にする理由:

- 今回の Apply 呼び出しで差し込んだ command をその stack 処理から即時使用したい

---

## 7. Runtime モデル

### 7.1 旧 `BaseEffectRuntime` は廃止

新 runtime は継承型ではなく composition 型とする。

```csharp
public sealed class StatusEffectRuntime
{
    public string InstanceId { get; }
    public string DefinitionId { get; }
    public string RuntimeTag { get; }
    public bool IsRegistered { get; }
    public bool IsEnabled { get; }
    public bool IsApplied { get; }
    public bool IsUseBlocked { get; }
    public bool IsActive { get; }
}
```

内部保持:

- definition 参照
- operation runtime list
- duration controller
- count controller
- inverse interval timer
- hook runtime
- var store snapshot

### 7.2 状態の意味

- `IsRegistered`: service に存在している
- `IsEnabled`: 効果が有効で operation が scalar 等に影響を与えている
- `IsApplied`: apply 済み。runtime 初期化が完了している
- `IsUseBlocked`: count 切れまたは inverse interval 中で `Use` を受け付けない
- `IsActive`: policy に基づく外部向け active 判定

### 7.3 Enable / Disable / Remove

`Disable` と `Remove` は必ず別概念にする。

`Disable`

- runtime は残る
- scalar tag 等の実影響だけ外す
- `OnDisable` を実行

`Enable`

- runtime を再有効化する
- scalar tag 等を再付与する
- `OnEnable` を実行

`Remove`

- 実影響を外す
- `OnRemove` を実行
- rich text 登録を解除
- runtime vars を消す
- service から完全削除する

### 7.4 Use

`Use` は Apply とは独立した命令である。

`Use` の役割:

1. `OnUse` 実行
2. count 消費
3. inverse interval 開始
4. 必要なら Disable / Remove

`Use` で必ず scalar を再計算するわけではない。
effect によっては `OnApply` で外部インタラクトの準備だけ行い、実際にゲーム内で何かが起きたタイミングで `Use` を count 消費用に呼ぶ。

### 7.5 count 切れ時の分岐

count 切れ時の動作は以下を持つ。

```csharp
public enum StatusEffectCountExhaustedAction
{
    None = 0,
    Disable = 10,
    Remove = 20,
    BlockUseOnly = 30,
}
```

`BlockUseOnly` は以下の意味:

- `OnApply / OnRemove` の付け直しをしない
- scalar 影響も維持する
- `Use` だけ無効化する

### 7.6 inverse interval

1 回 `Use` した後、一時的に effect をオフにする時間を持てるようにする。

```csharp
public enum StatusEffectInverseIntervalAction
{
    None = 0,
    Disable = 10,
    BlockUseOnly = 20,
}
```

仕様:

- inverse interval は「効果継続時間」ではなく「Use 後の一時オフ時間」
- interval 終了時、`Disable` だった場合だけ再 `Enable`
- `BlockUseOnly` の場合は scalar 影響は外さない

### 7.7 Active 判定 policy

```csharp
public enum StatusEffectActivePolicy
{
    EnabledOnly = 10,
    RegisteredEvenIfDisabled = 20,
}
```

この policy は count 切れや inverse interval で一時 disable 中でも Active と見なすかを決める。

---

## 8. 遅延 capability 解決

### 8.1 service constructor で必須にしないもの

以下は `StatusEffectService` の constructor 必須依存から外す。

- `IHealthService`
- `IBaseScalarService`
- `IBlackboardService`
- `IEntityEventService`
- `IScopeBindingRegistry`

### 8.2 新しい build context

```csharp
public sealed class StatusEffectBuildContext : IDynamicContext
{
    public IScopeNode OwnerScope { get; }
    public IScopeNode CommandRootScope { get; }
    public IVarStore Vars { get; }
    public StatusEffectApplyRequest ApplyRequest { get; }

    public bool TryResolveLocal<T>(out T service) where T : class;
    public bool TryResolveFromActor<T>(ActorSource actorSource, out T service) where T : class;
}
```

operation はこの context 経由で必要 service を取りに行く。

### 8.3 capability 不足時の扱い

service 不足で例外を投げない。

基本方針:

1. service 生成失敗は起こさない
2. effect apply 時に validation する
3. 必須 capability が無ければその effect apply を失敗扱いにする
4. warning は出してよいが exception で止めない

例:

- Health 系 operation を持つ effect を Health 無し scope に Apply した
  - `StatusEffectService` 自体は正常
  - 当該 Apply のみ失敗

---

## 9. StatusEffectService API 再設計

```csharp
public interface IStatusEffectService
{
    bool TryApply(in StatusEffectApplyRequest request, out string instanceId);
    int Remove(in StatusEffectFilter filter);
    int SetEnabled(in StatusEffectFilter filter, bool enabled);
    int Use(in StatusEffectFilter filter, IScopeNode userScope = null);
    int Reset(in StatusEffectFilter filter);
    void ClearAll();
    void GetStates(List<StatusEffectState> output, in StatusEffectFilter filter);
}
```

### 9.1 filter

effect filter は enum で effect 本体を指定しない。
mode enum は持ってよいが、対象 effect は string id / tag ベースにする。

```csharp
public enum StatusEffectFilterMode
{
    All = 10,
    DefinitionId = 20,
    RuntimeTag = 30,
    InstanceId = 40,
}
```

ルール:

- `All` なら全 effect 対象
- id / tag フィールドが空なら対象 0 件
- `Use(All)` が 1 回の command で全 effect 使用を実現する

### 9.2 slot key

stack 対象判定は `DefinitionId` だけではなく slot key で行う。

既定:

- `SlotKey = DefinitionId`

タグ付き:

- `SlotKey = DefinitionId + ":" + RuntimeTag`

これにより、同一 definition をタグ違いで共存させられる。

---

## 10. Command と DynamicSource

### 10.1 Apply command

`StatusEffectCommandData` は enum ベースから以下へ変更する。

- `DynamicValue<BaseStatusEffectDefinitionData> Definition`
- `StatusEffectFilter Filter`
- `DynamicValue<float> Intensity`
- `bool OverrideDuration`
- `DynamicValue<float> DurationOverride`
- `string RuntimeTag`
- `StatusEffectHookMutationSet HookMutations`

### 10.2 operation 種別

command operation は維持してよい。

- `Apply`
- `Remove`
- `Enable`
- `Disable`
- `Use`
- `Reset`
- `ClearAll`

ただし effect 指定 enum は廃止し、`Apply` は definition source、非 Apply は filter を使う。

### 10.3 description source

`ActiveStatusEffectDescriptionsSource` も enum 依存を廃止する。

新仕様:

- 対象 actor は `ActorSource`
- 区切り文字は enum でよい
- 除外 filter は `List<string> ExcludedDefinitionIds`
- 除外 id が空なら全 effect 対象

必要なら将来 `IncludedDefinitionIds` も追加可能だが、初版は exclude only で十分。

### 10.4 Editor 配線

以下は実装時に必須。

1. `DynamicManagedRefSourceCatalog` に `BaseStatusEffectDefinitionData` を登録
2. `TypedDynamicValueDrawer` で `DynamicValue<BaseStatusEffectDefinitionData>` の source 選択を成立させる
3. 新しい DynamicSource を追加した場合は Editor 表示も対応する
4. 新 command executor は `CommandRunnerMB.cs` に必ず登録する

---

## 11. Runtime vars と公開 state

最低限保持する vars:

- `definitionId`
- `instanceId`
- `runtimeTag`
- `effectType`
- `isEnabled`
- `isApplied`
- `isActive`
- `isUseBlocked`
- `stackCount`
- `intensity`
- `usedCount`
- `remainingUseCount`
- `maxUseCount`
- `remainingDuration`
- `totalDuration`
- `remainingInverseInterval`
- `visualData`
- `nameKey`
- `descriptionKey`

既存の `VarIds.GameLib.Base.StatusEffect.Element.*` は再利用できるものは維持し、足りないものだけ追加する。

---

## 12. GameLogic 用の初期 effect カタログ

初期実装では、以下の scalar key ごとに 1 definition を用意する。

| DefinitionId | ScalarKey | Notes |
|---|---|---|
| `StatusEffect.GameLogic.BallProfile.MaxValue` | `ScalarKeys.GameLogic.BallProfile.MaxValue` | Ball 用。通常は count 無し |
| `StatusEffect.GameLogic.BallProfile.Value` | `ScalarKeys.GameLogic.BallProfile.Value` | Ball 用。通常は count 無し |
| `StatusEffect.GameLogic.NailProfile.Effect.Attract` | `ScalarKeys.GameLogic.NailProfile.Effect.Attract` | Nail 系 |
| `StatusEffect.GameLogic.NailProfile.Effect.Bounce` | `ScalarKeys.GameLogic.NailProfile.Effect.Bounce` | Nail 系 |
| `StatusEffect.GameLogic.NailProfile.Effect.Chain` | `ScalarKeys.GameLogic.NailProfile.Effect.Chain` | Nail 系 |
| `StatusEffect.GameLogic.NailProfile.Effect.ColiderSize` | `ScalarKeys.GameLogic.NailProfile.Effect.ColiderSize` | 生成 key の綴りをそのまま使う |
| `StatusEffect.GameLogic.NailProfile.Effect.Converter` | `ScalarKeys.GameLogic.NailProfile.Effect.Converter` | Nail 系 |
| `StatusEffect.GameLogic.NailProfile.Effect.Force` | `ScalarKeys.GameLogic.NailProfile.Effect.Force` | Nail 系 |
| `StatusEffect.GameLogic.NailProfile.Effect.Gamble` | `ScalarKeys.GameLogic.NailProfile.Effect.Gamble` | Nail 系 |
| `StatusEffect.GameLogic.NailProfile.Effect.Ghost` | `ScalarKeys.GameLogic.NailProfile.Effect.Ghost` | Nail 系 |
| `StatusEffect.GameLogic.NailProfile.Effect.LuckUp` | `ScalarKeys.GameLogic.NailProfile.Effect.LuckUp` | Nail 系 |
| `StatusEffect.GameLogic.NailProfile.Effect.MaxHitCount` | `ScalarKeys.GameLogic.NailProfile.Effect.MaxHitCount` | Count source としても使う |
| `StatusEffect.GameLogic.NailProfile.Effect.MaxValueUp` | `ScalarKeys.GameLogic.NailProfile.Effect.MaxValueUp` | Nail 系 |
| `StatusEffect.GameLogic.NailProfile.Effect.SpeedUp` | `ScalarKeys.GameLogic.NailProfile.Effect.SpeedUp` | Nail 系 |
| `StatusEffect.GameLogic.NailProfile.Effect.Split` | `ScalarKeys.GameLogic.NailProfile.Effect.Split` | Nail 系 |
| `StatusEffect.GameLogic.NailProfile.Effect.Teleport` | `ScalarKeys.GameLogic.NailProfile.Effect.Teleport` | Nail 系 |
| `StatusEffect.GameLogic.NailProfile.Effect.TimeExtend` | `ScalarKeys.GameLogic.NailProfile.Effect.TimeExtend` | Nail 系 |
| `StatusEffect.GameLogic.NailProfile.Effect.TimeValueUp` | `ScalarKeys.GameLogic.NailProfile.Effect.TimeValueUp` | Nail 系 |
| `StatusEffect.GameLogic.NailProfile.Effect.ValueMultiply` | `ScalarKeys.GameLogic.NailProfile.Effect.ValueMultiply` | Nail 系 |
| `StatusEffect.GameLogic.NailProfile.Effect.ValueUp` | `ScalarKeys.GameLogic.NailProfile.Effect.ValueUp` | Nail 系 |

共通ルール:

1. 1 scalar key = 1 effect definition
2. ただし code class は 1 effect 1 class にしない
3. 大半は `ScalarModifierOperationDefinition` の data 差し替えで表現する

Count に関するルール:

1. Nail 系 definition は count を使える設計にする
2. count を有効にした definition の既定 `MaxCount` source は `GameLogic.NailProfile.Effect.MaxHitCount`
3. Ball 系 definition は既定で count 無し

未確定事項:

- 各 key が `Add` か `Mul` か
- どの effect が Self 以外の scalar target を使うか

これは definition asset 側の authoring 項目で決める。core code にハードコードしない。

---

## 13. 実装フェーズ

### Phase 1: core の土台置換

1. `BaseEffectRuntime`, `EffectContext`, `StatusEffectIdUtility` を撤去
2. `BaseStatusEffectDefinitionData` と thin SO wrapper を追加
3. `StatusEffectRuntime` の composition 型を追加
4. `StatusEffectService` の constructor 依存を最小化

### Phase 2: effect operation / duration / count

1. `IStatusEffectOperationDefinition` / runtime を追加
2. `ScalarModifierOperationDefinition` を実装
3. duration / count interface と controller 実装を追加
4. inverse interval と active policy を実装

### Phase 3: command / source / vars

1. `StatusEffectCommandData` を definition source ベースへ変更
2. filter を string id / tag ベースへ変更
3. description source の enum filter を削除
4. runtime vars と rich text 登録を実装

### Phase 4: GameLogic effect authoring

1. Ball 2 件
2. Nail 18 件
3. `MaxHitCount` source を count 設定へ配線
4. hook command の動作確認

### Phase 5: legacy cleanup

1. 旧 effect class 削除
2. 旧 profile wrapper 削除
3. 旧 enum ベース command 削除
4. 旧 dynamic source filter 削除

---

## 14. 実装時の注意

1. 例外で流さず、apply validation は戻り値で扱う。
2. `IScopeAcquireHandler` で購読や初期化を行い、`IScopeReleaseHandler` で runtime mutation や cache を戻す。
3. `DynamicValue<T>` に新しい source を足したら Editor 配線も同時に入れる。
4. effect ごとの scalar tag は runtime instance 単位で一意にする。
5. effect 指定を enum に戻さない。
6. 新 command executor を作ったら `CommandRunnerMB.cs` に登録する。

---

## 15. まとめ

新しい StatusEffect は「効果の種類をコードで列挙する仕組み」ではなく、「polymorphic な定義データを runtime に流し込む仕組み」として作り直す。

これにより解決すること:

1. `IHealthService` 不在で service ごと死ぬ問題
2. effect 追加のたびに enum / class / factory を増やす問題
3. hook command の差し替え不足
4. count / duration の暗黙有効化
5. GameLogic 固有 scalar effect の大量追加への弱さ

v2 の実装では legacy 互換は捨て、StatusEffect を DynamicValue 中心の汎用能力システムとして再構築する。
