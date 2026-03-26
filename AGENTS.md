# AGENTS.md

# DIのシステム
今プロジェクトではVContainerを参考にしながら、自作のLifetimeScopeを使用して作成していく
また[Inject]は使用せず、Installer内でResolverから解決する形を取っています。
ワーニングは極力避ける方針です。
また作成を行ったときは、その作成したファイルでエラーが起こってる場合修正を行ってください、
タスクの終了前に、自身が作ったファイルをすべて査定し、エラーが起きていないか確認を行います。

あなたはC#/Unityの熟練エンジニアです。
つねに抽象性と具体性のバランスを考慮しながら、明確で簡潔なコードを書きます。
拡張性、保守性、パフォーマンスを考慮してください。
また大胆な破壊的変更も許容してください、
ただし既存の設計思想を尊重してください。

またファイル移動や削除等は行ってよいです、
ただしUnityのためファイルの作成や、フォルダ移動を行った後は
Metaデータやコンパイルのために、一定時間かかることがあります、
これらは開発の考慮に入れる必要はありませんが、長い時間コンパイルエラーが続く場合は、修正を行ってください

また今後Serviceを作成するときはIScopeAcquireHandlerで初期化（イベント購読など）を行い
IScopeReleaseHandlerでリセットを書きます、
この二つはBaseLifeitmeScopeとRuntimeLifetimeScopeを両立させるために作った独自のInterfaceで
これによりFeatureInstallerでRuntimeLTS BseLTS両方が対応できます。
IStartable, IInitializableは使わないようにしてください。


またVContainerのRegisterEntryは使わないようにしてください。

例外処理は極力使わないようにしてください、
つまり、失敗結果を握りつぶさないようにしてください。
UniTask等で必要な場合は、エラーキャッチを必ず行ってください
エラーキャッチなしの例外処理を行うことは禁止です。

もし変数のフィールド[SerializeField]などを作る際はodinInspectorなどで
見やすいフィールドづくりを心がけてください、
またShowIf(OdinInspector)などを活用し、開発者が迷わないフィールド設計を行います。

仕様書作成を行う場合はできる限り詳細に、修正を行います。
また、修正が仕様書のバージョンアップや、コードを含む場合は
必ずその旨をコメントで記載してください。
また関連するコードがある場合は必ず熟読してください、
コードを読まずに、仕様書を作成するのは禁止です。

またGitに勝手にコミットをしないでください、
コミットは命令がある前行ってはダメです。

必ずコードの変更を行うときは、それに関連するコードをすべて熟読し、
影響範囲を把握した上で、変更を行ってください。

コマンドを作成したときは、必ずExecutorをCommandRunnerMB.csに登録してください。」

Resolver関連のコードを作成したときはusing VContainerを追加してください
if (scope.Resolver.TryResolve<T>(out var service) && service != null)
    return service;
このようなTryResolverなどを使用したときです。

スコープのtransformが欲しい場合はLTSIdentityのSelfTransformを取得してください、
また, Transformをフィールドで指定する状況や、他のScopeが欲しい場合は必ずActorSourceを経由させて取得してください

enumを使用するときは必ず数値を入れてください
public enum SceneType{
    BattleScene = 10,
    RestScene = 20, 
    LoadingScene = 30
}
またシステムを組む際はパフォーマンスや最適化にも重要視してください。

DynamicValue系の変更をしたときもし、DynamicSourceを使用したクラス/Interfaceを増やした場合は、
必ず、Editor側の配線を行ってください。

またレガシー機能や、既存アセットのフィールドや参照などが切れるようなコード変更を行ってもよいとします。
もしコード変更にて、旧機能やレガシーが必要な場合でも、そういったもののための互換システムは作成する必要はありません
またもしSOを作成する際は、DynamicValueとの連携を考えているものは必ず、
薄いラッパとして作成して下さい、
基本はSerializeされたクラスを使用して、Soにはそのクラスだけがあるようにしたい、
これによりDynamicValue<T>という形をとる際に、シリアライズクラスをTの中に入れるだけで、その仕組みを使用できます。
ビルド確認はdotnet build TinnosukeGameLib.slnxで行うことができます。
$ /mnt/c/Program Files/dotnet/dotnet.exe" build TinnosukeGameLib.slnx -v minimal　出力が見えるコマンドです。
この環境の bash には dotnet が入っていません。Windows 側の dotnet.exe が見えるため、そちらを使用してください、