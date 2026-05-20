// Game.Common.DynamicValue.cs
//
// DynamicValue - 統一された動皁E��構造佁E
//
// 設計決宁E
// - 唯一の入口として機�E
// - [SerializeReference] IDynamicSource で多�E匁E
// - 評価結果は DynamicVariant で返す
// - DynamicValue.TryGet<T>() で型変換付き取征E
// - DynamicValue<T> でジェネリチE��版も提侁E

using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Game;
using Game.Commands;
using Game.Commands.VNext;
using VContainer;

namespace Game.Common
{
    /// <summary>
    /// 統一された動皁E��構造体、E
    /// 全ての動的値取得�Eこ�E型を通じて行う、E
    /// </summary>
    [Serializable]
    public struct DynamicValue
    {
        [SerializeReference]
        [HideLabel]
        IDynamicSource _source;

        /// <summary>
        /// ソースが設定されてぁE��か、E
        /// </summary>
        public bool HasSource => _source != null;

        /// <summary>
        /// ソースの種別名、E
        /// </summary>
        public string SourceTypeName => _source?.SourceTypeName ?? "None";

        /// <summary>
        /// ソースのチE��チE��惁E��、E
        /// </summary>
        public string SourceDebugData => _source?.GetDebugData ?? string.Empty;

        /// <summary>
        /// KeyやValueなどの、データ(チE��チE��用)
        /// </summary>
        public string DebugData => _source.GetDebugData ?? "No Source";

        public int GetSourceConfigurationRevision()
        {
            if (_source is IDynamicSourceConfigurationRevisionProvider revisionProvider)
                return revisionProvider.GetSourceConfigurationRevision();

            return 0;
        }

        public int GetSourceDependencyRevision(IDynamicContext context)
        {
            if (_source is IDynamicSourceDependencyRevisionProvider revisionProvider)
                return revisionProvider.GetSourceDependencyRevision(context);

            return 0;
        }

        /// <summary>
        /// 値を評価して DynamicVariant として返す、E
        /// </summary>
        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (_source == null)
                return DynamicVariant.Null;

            if (context is IDynamicEvaluationContext evaluationContext)
            {
                if (evaluationContext.Runtime.TryEvaluate(_source, evaluationContext, out var tracked))
                    return tracked;

                return DynamicVariant.Null;
            }

            return _source.Evaluate(context);
        }

        /// <summary>
        /// 冁E��ソースを型持E��で取得すめE(source がその垁Eインターフェイスを実裁E��てぁE��場吁E、E
        /// </summary>
        /// <summary>
        /// 冁E��ソースを型持E��で取得すめE(source がその垁Eインターフェイスを実裁E��てぁE��場吁E、E
        /// </summary>
        public bool TryGetSource<TSource>(out TSource source) where TSource : class
        {
            source = _source as TSource;
            return source != null;
        }

        /// <summary>
        /// 型安�Eに値を取得、E
        /// </summary>
        public bool TryGet<T>(IDynamicContext context, out T value)
        {
            var variant = Evaluate(context);
            return variant.TryGet(out value);
        }

        /// <summary>
        /// 値を取得、失敗時はチE��ォルト値を返す、E
        /// </summary>
        public T GetOrDefault<T>(IDynamicContext context, T defaultValue = default)
        {
            return TryGet<T>(context, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 斁E��不要な評価を行う、E
        /// リチE��ルめEnull-safe な source めEEmptyDynamicContext で評価する、E
        /// </summary>
        public T GetOrDefaultWithoutContext<T>(T defaultValue = default)
        {
            return GetOrDefault(EmptyDynamicContext.Instance, defaultValue);
        }

        // ================================================================
        // ファクトリメソチE��
        // ================================================================

        public static DynamicValue FromSource(IDynamicSource source)
        {
            return new DynamicValue { _source = source };
        }

        public static DynamicValue FromLiteral(int value)
            => FromSource(LiteralSource.FromInt(value));

        public static DynamicValue FromLiteral(float value)
            => FromSource(LiteralSource.FromFloat(value));

        public static DynamicValue FromLiteral(bool value)
            => FromSource(LiteralSource.FromBool(value));

        public static DynamicValue FromLiteral(string value)
            => FromSource(LiteralSource.FromString(value));

        public static DynamicValue FromLiteral(Vector2 value)
            => FromSource(LiteralSource.FromVector2(value));

        public static DynamicValue FromLiteral(Vector3 value)
            => FromSource(LiteralSource.FromVector3(value));

        public static DynamicValue FromLiteral(Table value)
            => FromSource(new LiteralTableSource(value));

        public static DynamicValue FromLiteral(VarStorePayload value)
            => FromSource(new LiteralVarStorePayloadSource(value));

        public static DynamicValue FromVarId(int varId)
            => FromSource(VarStoreSource.FromVarId(varId));

        public static DynamicValue FromBlackboard(
            int key,
            BlackboardReadScope readScope = BlackboardReadScope.Local,
            BlackboardReadFallback fallback = BlackboardReadFallback.Default)
            => FromSource(SelfBlackboardSource.FromVarId(key, readScope, fallback));
    }

    // ================================================================
    // ジェネリチE��牁EDynamicValue<T>
    // ================================================================

    /// <summary>
    /// 型指定された動的値構造体、E
    /// DynamicValue&lt;Vector2&gt;, DynamicValue&lt;float&gt; などで使用、E
    /// </summary>
    /// <typeparam name="T">期征E��る値の垁E/typeparam>
    [Serializable]
    public struct DynamicValue<T>
    {
        [SerializeReference]
        [HideLabel, InlineProperty]
        IDynamicSource _source;

        [LabelText("Default Value")]
        [ShowIf(nameof(ShouldShowDefaultValue))]
        [SerializeField]
        T _defaultValue;

        /// <summary>
        /// ソースが設定されてぁE��か、E
        /// </summary>
        public bool HasSource => _source != null;

        /// <summary>
        /// ソースの種別名、E
        /// </summary>
        public string SourceTypeName => _source?.SourceTypeName ?? "None";

        /// <summary>
        /// ソースのチE��チE��惁E��、E
        /// </summary>
        public string SourceDebugData => _source?.GetDebugData ?? string.Empty;

        public int GetSourceConfigurationRevision()
        {
            if (_source is IDynamicSourceConfigurationRevisionProvider revisionProvider)
                return revisionProvider.GetSourceConfigurationRevision();

            return 0;
        }

        public int GetSourceDependencyRevision(IDynamicContext context)
        {
            if (_source is IDynamicSourceDependencyRevisionProvider revisionProvider)
                return revisionProvider.GetSourceDependencyRevision(context);

            return 0;
        }

        /// <summary>
        /// ソースへの参�E�E�式ソースの依存キー取得用�E�E
        /// </summary>
        internal IDynamicSource Source => _source;

        /// <summary>
        /// 値を評価して DynamicVariant として返す、E
        /// </summary>
        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (_source == null)
                return DynamicVariant.FromObject(_defaultValue);

            if (context is IDynamicEvaluationContext evaluationContext)
            {
                if (evaluationContext.Runtime.TryEvaluate(_source, evaluationContext, out var tracked))
                    return tracked;

                return DynamicVariant.Null;
            }

            return _source.Evaluate(context);
        }

        /// <summary>
        /// 型安�Eに値を取得、E
        /// </summary>
        public bool TryGetSource<TSource>(out TSource source) where TSource : class
        {
            source = _source as TSource;
            return source != null;
        }

        public bool TryGet(IDynamicContext context, out T value)
        {
            if (_source == null)
            {
                value = _defaultValue;
                return true;
            }

            DynamicVariant variant;
            if (context is IDynamicEvaluationContext evaluationContext)
            {
                if (!evaluationContext.Runtime.TryEvaluate(_source, evaluationContext, out variant))
                {
                    value = _defaultValue;
                    return false;
                }
            }
            else
            {
                variant = _source.Evaluate(context);
            }

            return variant.TryGet(out value);
        }

        /// <summary>
        /// 値を取得、失敗時はチE��ォルト値を返す、E
        /// </summary>
        public T GetOrDefault(IDynamicContext context, T defaultValue = default)
        {
            if (_source == null)
                return _defaultValue;

            return TryGet(context, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 斁E��不要な評価を行う、E
        /// リチE��ルめEnull-safe な source めEEmptyDynamicContext で評価する、E
        /// </summary>
        public T GetOrDefaultWithoutContext(T defaultValue = default)
        {
            return GetOrDefault(EmptyDynamicContext.Instance, defaultValue);
        }

        /// <summary>
        /// コンチE��ストから値を解決�E�ショートカチE���E�、E
        /// </summary>
        public T Resolve(IDynamicContext context) => GetOrDefault(context);

        /// <summary>
        /// bool 値を評価するヘルパ�E�E�EynamicValue&lt;bool&gt; 専用�E�、E
        /// 評価失敗時は false を返す、E
        /// </summary>
        public bool EvaluateBool(IDynamicContext context)
        {
            var variant = Evaluate(context);
            if (variant.TryGet<bool>(out var b))
                return b;
            return false;
        }

        /// <summary>
        /// Scope + Vars から値を取得（ショートカチE���E�、E
        /// </summary>
        public T Get(IScopeNode scope, IVarStore vars, T defaultValue = default)
        {
            var ctx = new SimpleDynamicContext(vars, scope);
            return GetOrDefault(ctx, defaultValue);
        }

        /// <summary>
        /// 式ソースの場合、依存キー一覧を取得、E
        /// MonitorChannelHub の EventDriven モードで使用、E
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> GetDependentKeys()
        {
            if (_source is IExpressionSource exprSource)
                return exprSource.GetDependentKeys();
            return null;
        }

        bool ShouldShowDefaultValue() => _source == null;

        // ================================================================
        // 非ジェネリチE��版との相互変換
        // ================================================================

        /// <summary>
        /// 非ジェネリチE�� DynamicValue への暗黙変換、E
        /// </summary>
        public static implicit operator DynamicValue(DynamicValue<T> typed)
        {
            return DynamicValue.FromSource(typed._source);
        }

        /// <summary>
        /// 非ジェネリチE�� DynamicValue からの明示変換、E
        /// </summary>
        public static explicit operator DynamicValue<T>(DynamicValue untyped)
        {
            // リフレクションでソースを取得！Enternal フィールドなので�E�E
            // 代替桁E DynamicValue に GetSource() を追加
            return new DynamicValue<T>();
        }

        // ================================================================
        // ファクトリメソチE��
        // ================================================================

        public static DynamicValue<T> FromSource(IDynamicSource source)
        {
            return new DynamicValue<T> { _source = source };
        }

        public static DynamicValue<T> FromDefault(T defaultValue)
        {
            return new DynamicValue<T> { _defaultValue = defaultValue };
        }
    }

    // ================================================================
    // DynamicValue<T> 拡張メソチE��
    // ================================================================

    public static class DynamicValueExtensions
    {
        /// <summary>
        /// int 用リチE��ル作�E、E
        /// </summary>
        public static DynamicValue<int> FromLiteral(int value)
            => DynamicValue<int>.FromSource(LiteralSource.FromInt(value));

        /// <summary>
        /// float 用リチE��ル作�E、E
        /// </summary>
        public static DynamicValue<float> FromLiteral(float value)
            => DynamicValue<float>.FromSource(LiteralSource.FromFloat(value));

        /// <summary>
        /// bool 用リチE��ル作�E、E
        /// </summary>
        public static DynamicValue<bool> FromLiteral(bool value)
            => DynamicValue<bool>.FromSource(LiteralSource.FromBool(value));

        /// <summary>
        /// string 用リチE��ル作�E、E
        /// </summary>
        public static DynamicValue<string> FromLiteral(string value)
            => DynamicValue<string>.FromSource(LiteralSource.FromString(value));

        /// <summary>
        /// Vector2 用リチE��ル作�E、E
        /// </summary>
        public static DynamicValue<Vector2> FromLiteral(Vector2 value)
            => DynamicValue<Vector2>.FromSource(new LiteralVector2Source(value));

        /// <summary>
        /// Vector3 用リチE��ル作�E、E
        /// </summary>
        public static DynamicValue<Vector3> FromLiteral(Vector3 value)
            => DynamicValue<Vector3>.FromSource(new LiteralVector3Source(value));

        /// <summary>
        /// Table 用リチE��ル作�E、E
        /// </summary>
        public static DynamicValue<Table> FromLiteral(Table value)
            => DynamicValue<Table>.FromSource(new LiteralTableSource(value));

        /// <summary>
        /// VarStorePayload 用の互換リチE��ル作�E、E
        /// </summary>
        public static DynamicValue<VarStorePayload> FromLiteral(VarStorePayload value)
            => DynamicValue<VarStorePayload>.FromSource(new LiteralVarStorePayloadSource(value));

        /// <summary>
        /// CommandListData 用リチE��ル作�E、E
        /// </summary>
        public static DynamicValue<CommandListData> FromLiteral(CommandListData value)
            => DynamicValue<CommandListData>.FromSource(new LiteralCommandListDataSource(value));
    }

    // ================================================================
    // Simple Dynamic Context
    // ================================================================

    /// <summary>
    /// シンプルな IDynamicContext 実裁E��E
    /// </summary>
    public sealed class SimpleDynamicContext : IDynamicContext, IDynamicDependencyTokenSource, IDynamicEvaluationOriginProvider
    {
        readonly DynamicDependencyTokenSet _dependencyTokens;
        readonly DynamicEvaluationOrigin _origin;

        public IVarStore Vars { get; }
        public IScopeNode Scope { get; }
        public IScopeNode CommandRootScope { get; }

        public SimpleDynamicContext(
            IVarStore vars,
            IScopeNode scope,
            IScopeNode commandRootScope = null,
            DynamicDependencyTokenSet dependencyTokens = default,
            DynamicEvaluationOrigin origin = default)
        {
            Vars = vars ?? NullVarStore.Instance;
            Scope = scope;
            CommandRootScope = commandRootScope;
            _dependencyTokens = dependencyTokens;
            _origin = origin.IsEmpty
                ? DynamicEvaluationOrigin.FromScopeNodes(scope, commandRootScope)
                : origin;
        }

        public IScopeNode ResolveOtherScope(CommandTargetIdentityFilter filter)
        {
            if (Scope?.Resolver == null)
                return null;

            if (!Scope.Resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var registry))
                return null;

            return registry.Resolve(filter, Scope);
        }

        public DynamicDependencyTokenSet GetDynamicDependencyTokens() => _dependencyTokens;

        public DynamicEvaluationOrigin GetDynamicEvaluationOrigin() => _origin;
    }

#nullable enable

    sealed class EmptyDynamicContext : IDynamicContext
    {
        public static readonly EmptyDynamicContext Instance = new();

        EmptyDynamicContext()
        {
        }

        public IVarStore Vars => NullVarStore.Instance;
        public IScopeNode Scope => EmptyScopeNode.Instance;
        public IScopeNode? CommandRootScope => null;

        public IScopeNode ResolveOtherScope(CommandTargetIdentityFilter filter)
        {
            _ = filter;
            return null!;
        }
    }

    sealed class EmptyScopeNode : IScopeNode
    {
        public static readonly EmptyScopeNode Instance = new();

        EmptyScopeNode()
        {
        }

        public IScopeNode? Parent => null;
        public ILTSIdentityService? Identity => null;
        public LifetimeScopeKind Kind => LifetimeScopeKind.None;
        public IRuntimeResolver? Resolver => null;
        public bool IsVisible => false;
        public bool IsActive => false;

        public bool TrySetVisible(bool visible, bool isReset = false)
        {
            _ = visible;
            _ = isReset;
            return false;
        }

        public bool TrySetActive(bool active, bool isReset = false)
        {
            _ = active;
            _ = isReset;
            return false;
        }

        public Cysharp.Threading.Tasks.UniTask SetActiveAsync(bool active, bool isReset = false, System.Threading.CancellationToken ct = default)
        {
            _ = active;
            _ = isReset;
            _ = ct;
            return Cysharp.Threading.Tasks.UniTask.CompletedTask;
        }

        public System.Collections.Generic.IReadOnlyList<IScopeNode>? GetPathFromRoot()
        {
            return System.Array.Empty<IScopeNode>();
        }
    }

#nullable restore
}
