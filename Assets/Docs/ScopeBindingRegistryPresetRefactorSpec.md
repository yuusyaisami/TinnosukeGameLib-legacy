<!--
Spec Version: v0.1
Status: Draft / Planning
Updated: 2026-03-13
Note:
- 本仕様書は新規作成です。
- 既存コードを読んだ上で、ProfileRegistry 系を Preset 中心へ移行し、将来的に SO 依存を減らす方針を整理しています。
- この版では設計方針と段階的移行案のみを定義し、コード変更は含みません。
- 主に参照したコード:
  - Assets/GameLib/Script/Common/Variables/Profile/MB/ProfileRegistryMB.cs
  - Assets/GameLib/Script/Common/Variables/Profile/Core/BaseProfileSO.cs
  - Assets/GameLib/Script/Common/Variables/Profile/Core/BaseProfileData.cs
  - Assets/GameLib/Script/Common/Variables/Profile/Core/IProfileRegistry.cs
  - Assets/GameLib/Script/Common/Variables/Profile/Core/IProfileRegistryConfigurator.cs
  - Assets/GameLib/Script/Common/Variables/Profile/Core/ProfileRegistryService.cs
  - Assets/GameLib/Script/Common/Variables/Profile/Core/ProfileRegistryInstallService.cs
  - Assets/GameLib/Script/Common/Variables/Profile/Core/IProfileDefinition.cs
  - Assets/GameLib/Script/Common/Variables/Profile/Core/IProfileValueBinding.cs
  - Assets/GameLib/Script/Common/Variables/Profile/Core/ProfileValue.cs
  - Assets/GameLib/Script/Common/Variables/Profile/Core/ProfileFloatValue.cs
  - Assets/GameLib/Script/Common/Variables/Profile/Core/ProfileSaveEntry.cs
  - Assets/GameLib/Script/Common/Variables/Profile/Core/CustomProfileDefinition.cs
  - Assets/GameLib/Script/Common/Variables/Save/Core/SaveScopeRegistrationService.cs
  - Assets/GameLib/Script/Common/Variables/Save/Core/ProfileRegistryPlanSource.cs
  - Assets/GameLib/Script/Common/Movement/SO/MovementProfileSO.cs
  - Assets/GameLib/Script/Common/Health/Profile/HealthModifierProfileSO.cs
  - Assets/GameLib/Script/Common/StatusEffect/Profile/SpeedModEffectProfileSO.cs
  - Assets/GameLib/Script/Common/StatusEffect/Profile/PoisonEffectProfileSO.cs
  - Assets/GameLib/Script/Common/Movement/Input/InputMovementService.cs
  - Assets/GameLib/Script/Project/Scene/Channels/Movement/Core/MovementChannelHubService.cs
-->

# Scope Binding Registry / Preset 移行仕様

## 1. 目的

現在の `ProfileRegistry` は名前に対して責務が広く、実際には次を行っています。

- 型ごとの設定定義を登録する
- その設定定義から Blackboard / Scalar へ初期値を反映する
- 各 Binding が Save 対象かどうかを収集し、Save plan を構成する
- Pool 再利用時に Reset / Clear / 再登録を行う

このため、`Profile` という語よりも、実態は「Scope に対する値バインディング定義の登録と適用」に近いです。

また、現状は `BaseProfileSO` と `BaseProfileData` が並立しており、

- `BaseProfileSO` は ScriptableObject ベース
- `BaseProfileData` は SerializeReference ベース

で、ほぼ同じリフレクション列挙責務を二重に持っています。

今後は以下を目標にします。

1. `BaseProfileSO` を廃止する
2. 各 Profile 系データを Preset 化する
3. 利用側は `DynamicValue<TPreset>` で指定できるようにする
4. `ProfileRegistry` 系を、実際の責務に合わせて改名する
5. プロジェクト全体で SO を「主たる設定入力」として使う場面を減らす


## 2. 現行コードを読んだ上での評価

### 2.1 良い点

- `IProfileValueBinding` により Blackboard / Scalar / Save の責務がかなり整理されている
- `BaseProfileData` が既に存在し、Preset 化の受け皿がある
- `ProfileRegistryService` は `IProfileDefinition` も扱えるため、完全な SO 専用設計ではない
- `SaveScopeRegistrationService` と `ProfileRegistryPlanSource` により Save plan 生成はある程度分離されている
- `HealthPreset` など、最近の Preset 化パターンを横展開できる土台がある

### 2.2 問題点

- `BaseProfileSO` と `BaseProfileData` が同じようなリフレクション列挙を持っており、抽象が二重化している
- `ProfileRegistryService` の generic API が `BaseProfileSO` に強く縛られている
- `ProfileType` の意味が「論理的な設定種別」ではなく「歴史的な SO 型」に引っ張られている
- `ProfileRegistryMB` は inspector 入力が `BaseProfileSO[]` と `IProfileDefinition[]` に分かれており、入力面が二系統になっている
- `TypedDynamicValueDrawer` は型ごとの source 許可リストを手書きしているため、Profile Preset を大量導入すると boilerplate が増えやすい
- `ProfileRegistry` という名称が、Var / Scalar / Save への初期反映システムであることを表していない


## 3. 結論

### 3.1 方向性そのものは妥当

`BaseProfileSO` を廃止し、Preset を中心に寄せる方向は妥当です。

理由:

- `BaseProfileData` が既に存在するため、技術的な受け皿がある
- `DynamicValue<T>` と相性がよい
- 最近導入した `HealthPreset` や `StateMachinePreset` と思想を揃えられる
- 利用側から見ると、SO 参照より `DynamicValue<TPreset>` の方が統一しやすい

### 3.2 ただし、一気に全部消すのは危険

特に危険なのは次の 3 点です。

1. 解決キー
   - 今は `TryResolve<MovementProfileSO>()` のように SO 型がそのままキーになっている
2. DynamicValue editor 対応
   - Preset 型ごとに `Literal / Asset` source と drawer 許可が必要になる
3. 名前変更との同時実施
   - `ProfileRegistry` 改名と `BaseProfileSO` 廃止を同時にやると差分が肥大化する

そのため、**Preset 化と命名変更は同じ方針で進めつつ、実装は段階的に分ける** のがよいです。


## 4. 推奨する最終イメージ

### 4.1 基本方針

- 登録単位は `BaseProfileSO` ではなく `Preset`
- registry のキーも SO 型ではなく `Preset 型`
- inspector の主入力も SO 配列ではなく `SerializeReference` の Preset 配列
- 利用側の直接参照は `DynamicValue<TPreset>`
- Asset が必要な場合だけ、薄い wrapper asset を許容する

### 4.2 薄い wrapper asset の位置づけ

SO を完全に禁止する必要はありません。  
ただし、**SO をメイン入力にしない** ことが重要です。

推奨:

- 基本運用: inline preset
- 再利用したい定義だけ: thin wrapper asset
- さらに進める場合: thin wrapper asset すら最小限にする

これは `HealthProfileSO -> HealthPreset` の方向と同じです。


## 5. 命名変更案

### 5.1 推奨名称

`ProfileRegistry` という語はやめて、`ScopeBindingRegistry` を推奨します。

理由:

- `Scope`:
  - 実際に Scope 単位で Blackboard / Scalar / Save と結びついている
- `Binding`:
  - 定義の本質が「値の保存」ではなく「Blackboard / Scalar へのバインド」
- `Registry`:
  - 型ベースの登録・解決ストアであること自体は維持している

### 5.2 推奨リネーム表

| 現在 | 推奨 | 備考 |
|---|---|---|
| `ProfileRegistryMB` | `ScopeBindingRegistryMB` | MB は installer + inspector 設定置き場 |
| `ProfileRegistryService` | `ScopeBindingRegistryService` | 実体 registry |
| `IProfileRegistry` | `IScopeBindingRegistry` | 解決 API |
| `IProfileRegistryConfigurator` | `IScopeBindingRegistryConfigurator` | 外部差し替え API |
| `ProfileRegistryPlanSource` | `ScopeBindingPlanSource` | Save plan source |
| `ProfileSaveEntry` | `BindingSaveEntry` | Save 対象定義 |
| `IProfileDefinition` | `IScopeBindingDefinition` | ただし phase 1 は現名維持でもよい |
| `BaseProfileData` | `BaseBindingPreset` | phase 2 以降の rename 推奨 |
| `ProfileData<TProfile>` | （廃止） | `BindingPreset<TSelf>` に統合。SO 型制約が不要になるため |
| `CustomProfileDefinition` | `CustomBindingPreset` | inline 用汎用定義 |



## 6. データモデル方針

### 6.1 Preset 基底

`BaseProfileSO` は削除し、Preset 基底に一本化します。

推奨イメージ:

```csharp
[Serializable]
public abstract class BaseBindingPreset : IProfileDefinition
{
    public virtual Type ProfileType => GetType();
    public IEnumerable<IProfileValueBinding> EnumerateBindings() { ... }
    public void CollectBindings(List<IProfileValueBinding> output) { ... }
    public int GetBindingCount() { ... }
}

[Serializable]
public abstract class BindingPreset<TSelf> : BaseBindingPreset
    where TSelf : BindingPreset<TSelf>
{
    public sealed override Type ProfileType => typeof(TSelf);
}
```

これにより、`ProfileType` を legacy SO 型ではなく **Preset 自身の型** にできます。

> **注意**: 現在 `HealthPreset` は `BaseProfileData` を継承し、`ProfileType` で `typeof(HealthProfileSO)` を返しています。
> つまり、既に Preset 化された型でも `ProfileType` が旧 SO 型を指している状態です。
> `BindingPreset<TSelf>` 導入時に、この参照先を `typeof(HealthPreset)` へ切り替える必要があります。
> これは Save の `ProfileTypeName` にも影響するため、移行時にデータ互換の対応が必須です。

### 6.2 wrapper asset

Asset 再利用が必要な型だけ、薄い wrapper を持ちます。

```csharp
public abstract class BindingPresetAssetSO<TPreset> : ScriptableObject, IProfileDefinition
    where TPreset : BaseBindingPreset, new()
{
    [SerializeReference] TPreset? preset = new();
    public TPreset? Preset => preset;
    public Type ProfileType => typeof(TPreset);
    public void CollectBindings(List<IProfileValueBinding> output) => Preset?.CollectBindings(output);
}
```

ただし Unity の都合で generic asset base をそのまま create menu に出さないなら、
各型ごとに薄い closed wrapper を置いてもよいです。

### 6.3 `CustomProfileDefinition` の扱い

これは別系統にせず、将来的には `BaseBindingPreset` 派生へ統一する方がよいです。

理由:

- `ProfileRegistryMB` の入力型を一本化できる
- `IProfileDefinition[]` と `BaseProfileSO[]` の二重入力が不要になる


## 7. Registry の責務再定義

### 7.1 Registry が持つべき責務

- Preset を型ベースで登録する
- 登録時に Blackboard / Scalar へ binding を適用する
- Save 対象 binding の entry を収集する
- Pool reset/release に対応する

### 7.2 Registry が持つべきでない責務

- SO 特化 API
- `BaseProfileSO` 前提の reflection setter cache
- SO 型をそのまま解決キーにする責務

### 7.3 推奨 API

```csharp
public interface IScopeBindingRegistry
{
    void SetDefinition(IProfileDefinition definition);
    void SetDefinition<TPreset>(TPreset preset) where TPreset : BaseBindingPreset;
    bool TryResolve<TPreset>(out TPreset preset) where TPreset : BaseBindingPreset;
    TPreset Resolve<TPreset>() where TPreset : BaseBindingPreset;
    bool HasDefinition<TPreset>() where TPreset : BaseBindingPreset;
    bool TryResolveDefinition(Type definitionType, out IProfileDefinition definition);
    int Version { get; }
}
```

`BaseProfileSO` 系 API は phase 1 で obsolete、phase 2 で削除がよいです。


## 8. `ProfileRegistryMB` の新しい入力形式

### 8.1 現状

現状の `ProfileRegistryMB` は、

- `BaseProfileSO[] _profilesFromInspector`
- `IProfileDefinition[] _profileDefinitionsFromInspector`

の二本立てです。

### 8.2 推奨

最終的には一本にします。

```csharp
[SerializeReference]
BaseBindingPreset[] _presetsFromInspector;
```

または phase 1 では、

```csharp
[SerializeReference]
IProfileDefinition[] _definitionsFromInspector;
```

だけに寄せてもよいです。

### 8.3 私見

**最終形は `BaseBindingPreset[]` 一本化がよい** です。

理由:

- inline preset 主体の思想と一致する
- editor の入力面が一つになる
- `CustomProfileDefinition` を特別扱いしなくてよい


## 9. 利用側を `DynamicValue<TPreset>` へ寄せる方針

### 9.1 方針

現在 `MovementProfileSO` や各種 `*ProfileSO` を直接持っているクラスは、
順次 `DynamicValue<TPreset>` へ置き換えます。

例:

- `MovementProfileSO` -> `DynamicValue<MovementPreset>`
- `HealthModifierProfileSO` -> `DynamicValue<HealthModifierPreset>`
- `SpeedModEffectProfileSO` -> `DynamicValue<SpeedModEffectPreset>`
- `PoisonEffectProfileSO` -> `DynamicValue<PoisonEffectPreset>`

### 9.2 重要な注意

Preset 型を増やすたびに、現在の `DynamicValue` 実装では次の追加が必要です。

1. `LiteralXxxPresetSource`
2. `AssetXxxPresetSource`
3. `TypedDynamicValueDrawer` の登録

このまま Profile 系を大量移行すると boilerplate が増えます。

### 9.3 強い推奨

Profile 系の全面移行に入る前に、**Preset 用 DynamicValue source 登録をもう少し汎用化する** ことを推奨します。

最低でも次のどちらかが欲しいです。

1. 共通 base を用意して、typed source 追加コストを最小化する
2. drawer 側の allow-list を手書きではなく、型規約ベースで自動構築する

これを先にやらないと、Profile 変換より editor 対応の方が重くなります。

### 9.4 具体的な実装方針

ここでやるべき汎用化は、**DynamicValue の仕組み自体を変えることではなく、Preset 向けの Literal/Asset source 追加方法を共通化すること** です。

重要なのは次の 2 点です。

1. `DynamicVariant.ManagedRef` はそのまま使う
2. 手書きで増えている `LiteralXxxPresetSource` / `AssetXxxPresetSource` / drawer allow-list を共通化する

つまり、Variant の拡張ではなく、**source 定義と editor 登録の共通化** が主目的です。

### 9.4.1 先に固定する前提

Preset 系の汎用化は、次の前提で進める。

- Preset 本体は基本的に `class`
- `DynamicValue<T>` で扱う Preset は `ManagedRef` で評価する
- Asset wrapper は「`Preset` を返すだけの薄い SO」に寄せる
- random / expression / special runtime source は今回の汎用化対象に含めない

今回汎用化するのは、あくまで次の 2 種だけです。

1. Literal managed-ref source
2. Asset -> preset managed-ref source

### 9.4.2 追加する基底 interface

まず、thin wrapper asset を共通の形で扱うための interface を追加する。

```csharp
public interface IDynamicValueAsset<out TValue>
{
    TValue? Preset { get; }
}
```

これを、Preset を返す各 asset wrapper に実装する。

例:

- `HealthProfileSO : IDynamicValueAsset<HealthPreset>`
- `StateMachineProfileSO : IDynamicValueAsset<StateMachinePreset>`
- `StateAnimationProfileSO : IDynamicValueAsset<StateAnimationPreset>`
- `BaseRuntimeTemplatePresetAssetSO : IDynamicValueAsset<BaseRuntimeTemplatePreset>`
- `ParticleRuntimeTemplatePresetAssetSO : IDynamicValueAsset<ParticleRuntimeTemplatePreset>`

この interface に揃えることで、asset source 側は `value.Preset` を固定の流儀で読めるようになる。

> **補足**: `StateMachineProfileSO` と `StateAnimationProfileSO` は現在 `IProfileDefinition` を実装していません（`ScriptableObject` 直接継承で Preset を保持するだけ）。
> 一方 `HealthProfileSO` は `IProfileDefinition` を実装し、registry に登録可能です。
> `IDynamicValueAsset<T>` の導入においては、`IProfileDefinition` 実装の有無に関わらず、
> 「Preset を返す asset wrapper」であれば実装対象になります。

### 9.4.3 追加する generic literal source

個別の `LiteralStateMachinePresetSource` などを増やす代わりに、managed-ref literal を generic 化する。

```csharp
[Serializable]
public sealed class ManagedRefLiteralSource<TValue> : IDynamicSource
    where TValue : class
{
    [SerializeReference, InlineProperty, HideLabel]
    TValue? value;

    public ManagedRefLiteralSource() { }
    public ManagedRefLiteralSource(TValue value) => this.value = value;

    public string SourceTypeName => "Literal";
    public string GetDebugData => value != null ? value.ToString() ?? value.GetType().Name : "null";

    public DynamicVariant Evaluate(IDynamicContext context)
        => value != null ? DynamicVariant.FromManagedRef(value) : DynamicVariant.Null;
}
```

これにより、Preset 型ごとに必要だった `LiteralXxxPresetSource` は、原則として

- `ManagedRefLiteralSource<StateMachinePreset>`
- `ManagedRefLiteralSource<HealthPreset>`
- `ManagedRefLiteralSource<MovementPreset>`

のような closed generic で代用できる。

> **トレードオフ**: 既存の個別 source は型固有の debug 情報を持っています
> （例: `LiteralStateMachinePresetSource` は `layers=..., states=...`、
> `LiteralHealthPresetSource` は `maxHp=...`）。
> generic 化すると `GetDebugData` は `ToString()` に依存するため、
> 各 Preset で `ToString()` を適切にオーバーライドすることを推奨します。

### 9.4.4 追加する generic asset source

Asset wrapper から Preset を返す source も generic 化する。

```csharp
[Serializable]
public sealed class ManagedRefAssetSource<TAsset, TValue> : IDynamicSource
    where TAsset : ScriptableObject, IDynamicValueAsset<TValue>
    where TValue : class
{
    [SerializeField, HideLabel]
    TAsset? value;

    public string SourceTypeName => "Asset";
    public string GetDebugData => value != null ? value.name : "null";

    public DynamicVariant Evaluate(IDynamicContext context)
    {
        var preset = value != null ? value.Preset : default;
        return preset != null ? DynamicVariant.FromManagedRef(preset) : DynamicVariant.Null;
    }
}
```

これにより、個別の `AssetStateMachinePresetSource` や `AssetHealthPresetSource` の大半を generic で置き換えられる。

### 9.4.5 汎用化しても残すべき個別 source

以下は generic 化せず、個別 source を残してよいです。

- primitive literal
- `UnityObjectRefSource<T>`
- expression source
- random source
- weighted random source
- runtime template のように追加制約や特殊 debug 表示を持つ source
- 評価時に単純な `Preset` 取得以外の処理を持つ source

つまり、「ただ value を返すだけ」の source だけを generic 化します。

### 9.4.6 drawer 側の設計変更

現在の `TypedDynamicValueDrawer<T>` は、

- `GetTypedLiteralSourceType`
- `if (targetType == typeof(...)) allowedList.Add(...)`

を手で列挙しています。

ここを次の 2 段に分けます。

1. 既存の primitive / expression / random / unity object は手書きのまま
2. managed-ref preset 系だけ catalog から自動追加

### 9.4.7 追加する editor catalog

editor 側に、managed-ref source の生成規則を持つ catalog を追加する。

推奨クラス名:

- `DynamicManagedRefSourceCatalog`

責務:

- target type に対して generic literal source を作れるか判定
- target type に対応する asset wrapper 型を列挙
- drawer が使う allowed source type を返す
- legacy source から generic source への移行規則を持つ

推奨 API:

```csharp
internal static class DynamicManagedRefSourceCatalog
{
    public static bool TryGetLiteralSourceType(Type targetType, out Type sourceType);
    public static void AppendAssetSourceTypes(Type targetType, List<Type> dest);
    public static bool TryConvertLegacySource(Type targetType, IDynamicSource current, out IDynamicSource converted);
}
```

### 9.4.8 literal source を自動許可する条件

全型に対して `ManagedRefLiteralSource<T>` を出すのは危険です。

そのため、次のどちらかで opt-in にするのを推奨します。

1. marker interface
2. attribute

推奨は marker interface です。

```csharp
public interface IDynamicManagedRefValue
{
}
```

許可条件の例:

- `targetType` が `BaseProfileData` 派生
- または `IDynamicManagedRefValue` を実装

これなら `MovementPreset` や `HealthPreset` を対象にしつつ、
任意の serializable class が勝手に DynamicValue 候補に出ることを防げます。

### 9.4.9 asset source を自動許可する条件

asset source は `TypeCache` で自動収集する。

判定ルール:

- `ScriptableObject`
- `IDynamicValueAsset<TTarget>` を実装

これに一致する型ごとに、

- `ManagedRefAssetSource<TAsset, TTarget>`

の closed generic を 1 つ作って allowed list に追加する。

この方式なら、新しい `MovementPresetAssetSO` を追加しても、
drawer 側に `if (targetType == typeof(MovementPreset))` を増やす必要がない。

### 9.4.10 legacy source 移行

既に project 内には、

- `LiteralStateMachinePresetSource`
- `AssetStateMachinePresetSource`
- `LiteralHealthPresetSource`
- `AssetHealthPresetSource`

などが存在します。

このため、generic source 導入時は **既存 source を即削除せず、drawer 上で自動変換する期間を設ける** のが安全です。

推奨手順:

1. generic source を追加
2. `TypedDynamicValueDrawer<T>` で `TryConvertLegacySource(...)` を呼ぶ
3. old source を generic source へ載せ替える
4. 既存 asset / prefab / scene の再保存を待つ
5. 十分移行できたら旧 source を削除する

この変換は、現在の `TryConvertLegacyLiteralSource` を一般化した位置づけになります。

### 9.4.11 `TypedDynamicValueDrawer<T>` の変更点

変更後の流れは次のイメージです。

```csharp
void CacheAllowedSourceTypes()
{
    var targetType = typeof(T);
    var allowed = new List<Type>();

    AppendPrimitiveAndSpecialSources(targetType, allowed);

    if (DynamicManagedRefSourceCatalog.TryGetLiteralSourceType(targetType, out var literalType))
        allowed.Add(literalType);

    DynamicManagedRefSourceCatalog.AppendAssetSourceTypes(targetType, allowed);

    AppendRuntimeCommonSources(targetType, allowed);
}
```

重要なのは、Preset 系だけを catalog に逃がし、
他の複雑な source は従来通り明示的に残すことです。

全部を自動化しようとすると逆に読みづらくなります。

### 9.4.12 button label / 展開 UI の扱い

generic source を入れても、drawer の UI は大きく変えなくてよいです。

理由:

- literal source は子プロパティ名が `value`
- asset source も子プロパティ名が `value`

に揃えば、既存の `BuildButtonLabel` / `TryGetChildValueAsString` をかなり流用できます。

したがって UI 側の基本方針は次です。

- source の見た目は今のまま維持
- source type の登録方法だけを catalog 化
- detail 表示は `value` フィールド規約で吸収

### 9.4.13 `MotionPreset` 特例の扱い

現在 `MotionPreset` は専用描画が入っています。

この特例はすぐには消さず、phase を分けます。

- phase 1:
  - preset source catalog を導入
  - `MotionPreset` 特例は維持
- phase 2:
  - generic source の描画が安定したら `MotionPreset` 特例を縮小

理由:

- 特例撤去まで同時にやると切り分けが難しい
- まずは source 登録の共通化だけ成功させた方が安全

### 9.4.14 実装順序

推奨する実装順序は次です。

1. `IDynamicValueAsset<TValue>` を追加
2. thin wrapper asset に interface 実装を追加
3. `ManagedRefLiteralSource<TValue>` を追加
4. `ManagedRefAssetSource<TAsset, TValue>` を追加
5. `DynamicManagedRefSourceCatalog` を editor に追加
6. `TypedDynamicValueDrawer<T>` を catalog 利用へ変更
7. `TryConvertLegacyLiteralSource` を `TryConvertLegacySource` へ一般化
8. 既存の preset literal/asset source を obsolete 化
9. 移行完了後に旧 source を削除

### 9.4.15 この方式の効果

この設計にすると、新しい Profile Preset を 1 つ追加するときの作業は、
理想的には次まで減ります。

1. `MovementPreset` を作る
2. 必要なら `MovementPresetAssetSO : IDynamicValueAsset<MovementPreset>` を作る

これだけで、

- `DynamicValue<MovementPreset>` で Literal が使える
- Asset wrapper があれば Asset source も使える
- drawer 側の手書き追加が不要

という状態にできます。

これが、Profile 系全面移行の前に 9.3 を先行してやるべき理由です。


## 10. 既存 Profile の変換方針

### 10.1 変換対象の第一候補

現時点で `BaseProfileSO` 派生として確認できた主な対象:

- `MovementProfileSO`
- `HealthModifierProfileSO`
- `SpeedModEffectProfileSO`
- `PoisonEffectProfileSO`

### 10.2 変換ルール

各型は次のルールで変換します。

1. 既存 SO の binding フィールド（`ProfileFloatValue` 等）を `Preset` へ移す
2. 非 binding フィールド（`AgentRadius` や `EffectVisualData` 等の純粋データ）も同じ Preset に含める
3. 既存 SO は薄い wrapper にする
4. 利用側の直接参照を `DynamicValue<TPreset>` に置き換える
5. registry への登録も `Preset` ベースにする
6. 最終的に wrapper SO を不要化できる箇所は消す

> **補足**: `MovementProfileSO` の `AgentRadius` のように `IProfileValueBinding` を実装していないフィールドも、
> Preset 側に移す必要があります。これらは binding ではないが、利用側が fallback として参照するデータです。

### 10.3 注意点

`SpeedModEffectProfileSO` や `PoisonEffectProfileSO` は、
単なる binding 集合ではなく、追加のデータや helper method も持っています。

具体的には:

- `SpeedModEffectProfileSO`: `EffectVisualData` × 2 + `CreateSpeedBoostConfig()` / `CreateSlowConfig()`
- `PoisonEffectProfileSO`: `EffectVisualData` × 1 + `CreateConfig()`

つまり Preset 化するときは、

- binding 情報（`ProfileFloatValue` 群）
- 純粋データ（`EffectVisualData` 等）
- 小さな helper API（`CreateConfig()` 等）

を一緒に移す必要があります。

これは `HealthPreset` と同じで、Preset は単なる DTO ではなく、
**「解決後にそのまま利用される設定オブジェクト」** として扱うべきです。


## 11. Save との関係

### 11.1 現行評価

`SaveScopeRegistrationService` と `ProfileRegistryPlanSource` により、
Save plan はすでに registry 外へ出せています。

これはよい構造です。

### 11.2 方針

rename 後も、基本方針は維持します。

- registry は save plan の元データを保持する
- save plan source がそれを `SaveEntry` に変換する
- save 有効/無効の判断は各 binding が持つ

### 11.3 変更すべき点

- `ProfileSaveEntry` の命名
- `ProfileRegistryPlanSource` の命名
- 「Profile が Save される」という説明ではなく、
  **「binding された Blackboard / Scalar 値が Save 対象になる」**
  という説明へ統一する


## 12. 段階的移行案

### Phase 1: Registry を Preset 受け入れ中心へ寄せる

目的:

- `BaseProfileSO` 依存 API を縮小する
- `IProfileDefinition` / `BaseProfileData` の一本化方向を作る

作業:

- `ProfileRegistryMB` から `BaseProfileSO[]` を削減
- `ProfileRegistryService` に preset-centric API を追加
- `BaseProfileSO` API を obsolete 化
- `CustomProfileDefinition` を Preset 系へ寄せる準備をする

### Phase 2: 各 `*ProfileSO` を `*Preset` + thin wrapper に変換

目的:

- 実データを Preset 側へ移す

作業:

- `MovementPreset`
- `HealthModifierPreset`
- `SpeedModEffectPreset`
- `PoisonEffectPreset`

を追加し、対応 SO を薄い wrapper 化する

> **注意**: `HealthPreset` は既に存在しますが、`ProfileType` が `typeof(HealthProfileSO)` を返しています。
> Phase 2 では、`HealthPreset` の `ProfileType` を `typeof(HealthPreset)` に変更する作業も含めます。
> この変更は save entry の `ProfileTypeName` に影響するため、
> 移行用の型名マッピング（旧名→新名）を `ProfileRegistryPlanSource` 等に追加することを推奨します。

### Phase 3: 利用側を `DynamicValue<TPreset>` へ移行

目的:

- 参照入力を preset-first に統一する

作業:

- `MovementProfileSO` 参照箇所を `DynamicValue<MovementPreset>` へ
- effect profile 参照箇所を `DynamicValue<...Preset>` へ
- registry 解決も `TryResolve<TPreset>` へ寄せる

> **注意**: `MovementChannelHubService` は DI コンストラクタ引数で `MovementProfileSO` を直接受け取り、
> registry にも `SetProfileSO(movementProfile)` で登録しています。
> この DI 注入パターンも Preset ベースに置き換える必要があります。
> `InputMovementService` も `profileRegistry.TryResolve<MovementProfileSO>()` で SO 型をキーに解決しています。
> Phase 3 では、これらの利用側を一括で Preset 型ベースに移行します。

### Phase 4: rename と legacy 削除

目的:

- 名前と実体を一致させる

作業:

- `ProfileRegistry*` -> `ScopeBindingRegistry*`
- `BaseProfileSO` 削除
- `SetProfileSO` 系 API 削除
- legacy `ProfileType == old SO type` を廃止


## 13. リスク

### 13.1 最大のリスク

`ProfileType` を旧 SO 型から Preset 型へ切り替える瞬間です。

> **現状の具体例**: `HealthPreset` は既に `BaseProfileData` 派生ですが、
> `ProfileType` は `typeof(HealthProfileSO)` を返しています。
> つまり、既に Preset 化済みの型であっても、registry キーと save entry は SO 型に依存しています。
> この参照先を `typeof(HealthPreset)` に変更するとき、
> **既存のセーブデータとの互換性が壊れる** 可能性があります。

ここで影響を受けるのは:

- registry の lookup
- save entry の `ProfileTypeName`
- `TryResolve<...>()` 呼び出し側
- wrapper SO から preset への解決処理

### 13.2 もう一つのリスク

`DynamicValue` editor 側の型追加コストです。

Profile 系は数が増えやすいため、ここを汎用化しないと運用負債になります。

### 13.3 Save のリスク

現在は ScopeIdentity が空だと save entry が収集されません。  
rename 後もこの仕様は維持すべきですが、
「Profile がないから保存されない」ではなく、
「Scope に binding plan がないから保存されない」という説明へ変える必要があります。


## 14. 推奨する初手

いきなり全派生 `ProfileSO` の削除には入らず、まずは以下を推奨します。

1. `ProfileRegistry` の rename 案を `ScopeBindingRegistry` で固定する
2. `BaseProfileData` を最終基底にする方針を固める
3. `MovementProfileSO` を最初の試験変換対象にする
4. `DynamicValue` の preset source 登録方式を整理する
5. その後に `HealthModifier` / `StatusEffect` 系へ広げる

`MovementProfileSO` は、

- binding を持つ
- fallback 値を持つ
- 利用側の参照箇所が比較的追いやすい

ため、最初の縦切り対象として適しています。


## 15. この仕様書時点での私見

結論としては次です。

- 方針は正しい
- `BaseProfileSO` は消してよい
- ただし「Preset 化」「DynamicValue 化」「rename」は同じ思想でも、実装は段階を分けるべき
- 特に `DynamicValue` 側の汎用化を先に少し入れた方が、以降の Profile 移行がかなり楽になる
- 名称は `ScopeBindingRegistry` が現行責務に最も近い

この仕様書は、まず `MovementProfile` を試験対象にして設計を固めることを推奨します。
