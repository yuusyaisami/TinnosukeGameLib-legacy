// Game.Common.DynamicValue.cs
//
// DynamicValue - 統一された動的値構造体
//
// 設計決定:
// - 唯一の入口として機能
// - [SerializeReference] IDynamicSource で多態化
// - 評価結果は DynamicVariant で返す
// - DynamicValue.TryGet<T>() で型変換付き取得
// - DynamicValue<T> でジェネリック版も提供

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
    /// 統一された動的値構造体。
    /// 全ての動的値取得はこの型を通じて行う。
    /// </summary>
    [Serializable]
    public struct DynamicValue
    {
        [SerializeReference]
        [HideLabel]
        IDynamicSource _source;

        /// <summary>
        /// ソースが設定されているか。
        /// </summary>
        public bool HasSource => _source != null;

        /// <summary>
        /// ソースの種別名。
        /// </summary>
        public string SourceTypeName => _source?.SourceTypeName ?? "None";

        /// <summary>
        /// ソースのデバッグ情報。
        /// </summary>
        public string SourceDebugData => _source?.GetDebugData ?? string.Empty;

        /// <summary>
        /// KeyやValueなどの、データ(デバッグ用)
        /// </summary>
        public string DebugData => _source.GetDebugData ?? "No Source";

        /// <summary>
        /// 値を評価して DynamicVariant として返す。
        /// </summary>
        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (_source == null)
                return DynamicVariant.Null;

            return _source.Evaluate(context);
        }

        /// <summary>
        /// 内部ソースを型指定で取得する (source がその型/インターフェイスを実装している場合)。
        /// </summary>
        /// <summary>
        /// 内部ソースを型指定で取得する (source がその型/インターフェイスを実装している場合)。
        /// </summary>
        public bool TryGetSource<TSource>(out TSource source) where TSource : class
        {
            source = _source as TSource;
            return source != null;
        }

        /// <summary>
        /// 型安全に値を取得。
        /// </summary>
        public bool TryGet<T>(IDynamicContext context, out T value)
        {
            var variant = Evaluate(context);
            return variant.TryGet(out value);
        }

        /// <summary>
        /// 値を取得、失敗時はデフォルト値を返す。
        /// </summary>
        public T GetOrDefault<T>(IDynamicContext context, T defaultValue = default)
        {
            return TryGet<T>(context, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 文脈不要な評価を行う。
        /// リテラルや null-safe な source を EmptyDynamicContext で評価する。
        /// </summary>
        public T GetOrDefaultWithoutContext<T>(T defaultValue = default)
        {
            return GetOrDefault(EmptyDynamicContext.Instance, defaultValue);
        }

        // ================================================================
        // ファクトリメソッド
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

        public static DynamicValue FromVarId(int varId)
            => FromSource(VarStoreSource.FromVarId(varId));

        public static DynamicValue FromBlackboard(
            int key,
            BlackboardReadScope readScope = BlackboardReadScope.Local,
            BlackboardReadFallback fallback = BlackboardReadFallback.Default)
            => FromSource(SelfBlackboardSource.FromVarId(key, readScope, fallback));
    }

    // ================================================================
    // ジェネリック版 DynamicValue<T>
    // ================================================================

    /// <summary>
    /// 型指定された動的値構造体。
    /// DynamicValue&lt;Vector2&gt;, DynamicValue&lt;float&gt; などで使用。
    /// </summary>
    /// <typeparam name="T">期待する値の型</typeparam>
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
        /// ソースが設定されているか。
        /// </summary>
        public bool HasSource => _source != null;

        /// <summary>
        /// ソースの種別名。
        /// </summary>
        public string SourceTypeName => _source?.SourceTypeName ?? "None";

        /// <summary>
        /// ソースのデバッグ情報。
        /// </summary>
        public string SourceDebugData => _source?.GetDebugData ?? string.Empty;

        /// <summary>
        /// ソースへの参照（式ソースの依存キー取得用）
        /// </summary>
        internal IDynamicSource Source => _source;

        /// <summary>
        /// 値を評価して DynamicVariant として返す。
        /// </summary>
        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (_source == null)
                return DynamicVariant.FromObject(_defaultValue);

            return _source.Evaluate(context);
        }

        /// <summary>
        /// 型安全に値を取得。
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

            var variant = _source.Evaluate(context);
            return variant.TryGet(out value);
        }

        /// <summary>
        /// 値を取得、失敗時はデフォルト値を返す。
        /// </summary>
        public T GetOrDefault(IDynamicContext context, T defaultValue = default)
        {
            if (_source == null)
                return _defaultValue;

            return TryGet(context, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 文脈不要な評価を行う。
        /// リテラルや null-safe な source を EmptyDynamicContext で評価する。
        /// </summary>
        public T GetOrDefaultWithoutContext(T defaultValue = default)
        {
            return GetOrDefault(EmptyDynamicContext.Instance, defaultValue);
        }

        /// <summary>
        /// コンテキストから値を解決（ショートカット）。
        /// </summary>
        public T Resolve(IDynamicContext context) => GetOrDefault(context);

        /// <summary>
        /// bool 値を評価するヘルパー（DynamicValue&lt;bool&gt; 専用）。
        /// 評価失敗時は false を返す。
        /// </summary>
        public bool EvaluateBool(IDynamicContext context)
        {
            var variant = Evaluate(context);
            if (variant.TryGet<bool>(out var b))
                return b;
            return false;
        }

        /// <summary>
        /// Scope + Vars から値を取得（ショートカット）。
        /// </summary>
        public T Get(IScopeNode scope, IVarStore vars, T defaultValue = default)
        {
            var ctx = new SimpleDynamicContext(vars, scope);
            return GetOrDefault(ctx, defaultValue);
        }

        /// <summary>
        /// 式ソースの場合、依存キー一覧を取得。
        /// MonitorChannelHub の EventDriven モードで使用。
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> GetDependentKeys()
        {
            if (_source is IExpressionSource exprSource)
                return exprSource.GetDependentKeys();
            return null;
        }

        bool ShouldShowDefaultValue() => _source == null;

        // ================================================================
        // 非ジェネリック版との相互変換
        // ================================================================

        /// <summary>
        /// 非ジェネリック DynamicValue への暗黙変換。
        /// </summary>
        public static implicit operator DynamicValue(DynamicValue<T> typed)
        {
            return DynamicValue.FromSource(typed._source);
        }

        /// <summary>
        /// 非ジェネリック DynamicValue からの明示変換。
        /// </summary>
        public static explicit operator DynamicValue<T>(DynamicValue untyped)
        {
            // リフレクションでソースを取得（internal フィールドなので）
            // 代替案: DynamicValue に GetSource() を追加
            return new DynamicValue<T>();
        }

        // ================================================================
        // ファクトリメソッド
        // ================================================================

        public static DynamicValue<T> FromSource(IDynamicSource source)
        {
            return new DynamicValue<T> { _source = source };
        }
    }

    // ================================================================
    // DynamicValue<T> 拡張メソッド
    // ================================================================

    public static class DynamicValueExtensions
    {
        /// <summary>
        /// int 用リテラル作成。
        /// </summary>
        public static DynamicValue<int> FromLiteral(int value)
            => DynamicValue<int>.FromSource(LiteralSource.FromInt(value));

        /// <summary>
        /// float 用リテラル作成。
        /// </summary>
        public static DynamicValue<float> FromLiteral(float value)
            => DynamicValue<float>.FromSource(LiteralSource.FromFloat(value));

        /// <summary>
        /// bool 用リテラル作成。
        /// </summary>
        public static DynamicValue<bool> FromLiteral(bool value)
            => DynamicValue<bool>.FromSource(LiteralSource.FromBool(value));

        /// <summary>
        /// string 用リテラル作成。
        /// </summary>
        public static DynamicValue<string> FromLiteral(string value)
            => DynamicValue<string>.FromSource(LiteralSource.FromString(value));

        /// <summary>
        /// Vector2 用リテラル作成。
        /// </summary>
        public static DynamicValue<Vector2> FromLiteral(Vector2 value)
            => DynamicValue<Vector2>.FromSource(new LiteralVector2Source(value));

        /// <summary>
        /// Vector3 用リテラル作成。
        /// </summary>
        public static DynamicValue<Vector3> FromLiteral(Vector3 value)
            => DynamicValue<Vector3>.FromSource(new LiteralVector3Source(value));

        /// <summary>
        /// CommandListData 用リテラル作成。
        /// </summary>
        public static DynamicValue<CommandListData> FromLiteral(CommandListData value)
            => DynamicValue<CommandListData>.FromSource(new LiteralCommandListDataSource(value));
    }

    // ================================================================
    // Simple Dynamic Context
    // ================================================================

    /// <summary>
    /// シンプルな IDynamicContext 実装。
    /// </summary>
    public sealed class SimpleDynamicContext : IDynamicContext
    {
        public IVarStore Vars { get; }
        public IScopeNode Scope { get; }
        public IScopeNode CommandRootScope => null;

        public SimpleDynamicContext(IVarStore vars, IScopeNode scope)
        {
            Vars = vars ?? NullVarStore.Instance;
            Scope = scope;
        }

        public IScopeNode ResolveOtherScope(CommandTargetIdentityFilter filter)
        {
            if (Scope?.Resolver == null)
                return null;

            if (!Scope.Resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var registry))
                return null;

            return registry.Resolve(filter, Scope);
        }
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
        public IObjectResolver? Resolver => null;
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
