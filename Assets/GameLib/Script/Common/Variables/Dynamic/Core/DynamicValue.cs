// Game.Common.DynamicValue.cs
//
// DynamicValue - 邨ｱ荳縺輔ｌ縺溷虚逧・､讒矩菴・
//
// 險ｭ險域ｱｺ螳・
// - 蜚ｯ荳縺ｮ蜈･蜿｣縺ｨ縺励※讖溯・
// - [SerializeReference] IDynamicSource 縺ｧ螟壽・蛹・
// - 隧穂ｾ｡邨先棡縺ｯ DynamicVariant 縺ｧ霑斐☆
// - DynamicValue.TryGet<T>() 縺ｧ蝙句､画鋤莉倥″蜿門ｾ・
// - DynamicValue<T> 縺ｧ繧ｸ繧ｧ繝阪Μ繝・け迚医ｂ謠蝉ｾ・

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
    /// 邨ｱ荳縺輔ｌ縺溷虚逧・､讒矩菴薙・
    /// 蜈ｨ縺ｦ縺ｮ蜍慕噪蛟､蜿門ｾ励・縺薙・蝙九ｒ騾壹§縺ｦ陦後≧縲・
    /// </summary>
    [Serializable]
    public struct DynamicValue
    {
        [SerializeReference]
        [HideLabel]
        IDynamicSource _source;

        /// <summary>
        /// 繧ｽ繝ｼ繧ｹ縺瑚ｨｭ螳壹＆繧後※縺・ｋ縺九・
        /// </summary>
        public bool HasSource => _source != null;

        /// <summary>
        /// 繧ｽ繝ｼ繧ｹ縺ｮ遞ｮ蛻･蜷阪・
        /// </summary>
        public string SourceTypeName => _source?.SourceTypeName ?? "None";

        /// <summary>
        /// 繧ｽ繝ｼ繧ｹ縺ｮ繝・ヰ繝・げ諠・ｱ縲・
        /// </summary>
        public string SourceDebugData => _source?.GetDebugData ?? string.Empty;

        /// <summary>
        /// Key繧Хalue縺ｪ縺ｩ縺ｮ縲√ョ繝ｼ繧ｿ(繝・ヰ繝・げ逕ｨ)
        /// </summary>
        public string DebugData => _source.GetDebugData ?? "No Source";

        /// <summary>
        /// 蛟､繧定ｩ穂ｾ｡縺励※ DynamicVariant 縺ｨ縺励※霑斐☆縲・
        /// </summary>
        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (_source == null)
                return DynamicVariant.Null;

            return _source.Evaluate(context);
        }

        /// <summary>
        /// 蜀・Κ繧ｽ繝ｼ繧ｹ繧貞梛謖・ｮ壹〒蜿門ｾ励☆繧・(source 縺後◎縺ｮ蝙・繧､繝ｳ繧ｿ繝ｼ繝輔ぉ繧､繧ｹ繧貞ｮ溯｣・＠縺ｦ縺・ｋ蝣ｴ蜷・縲・
        /// </summary>
        /// <summary>
        /// 蜀・Κ繧ｽ繝ｼ繧ｹ繧貞梛謖・ｮ壹〒蜿門ｾ励☆繧・(source 縺後◎縺ｮ蝙・繧､繝ｳ繧ｿ繝ｼ繝輔ぉ繧､繧ｹ繧貞ｮ溯｣・＠縺ｦ縺・ｋ蝣ｴ蜷・縲・
        /// </summary>
        public bool TryGetSource<TSource>(out TSource source) where TSource : class
        {
            source = _source as TSource;
            return source != null;
        }

        /// <summary>
        /// 蝙句ｮ牙・縺ｫ蛟､繧貞叙蠕励・
        /// </summary>
        public bool TryGet<T>(IDynamicContext context, out T value)
        {
            var variant = Evaluate(context);
            return variant.TryGet(out value);
        }

        /// <summary>
        /// 蛟､繧貞叙蠕励∝､ｱ謨玲凾縺ｯ繝・ヵ繧ｩ繝ｫ繝亥､繧定ｿ斐☆縲・
        /// </summary>
        public T GetOrDefault<T>(IDynamicContext context, T defaultValue = default)
        {
            return TryGet<T>(context, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 譁・ц荳崎ｦ√↑隧穂ｾ｡繧定｡後≧縲・
        /// 繝ｪ繝・Λ繝ｫ繧・null-safe 縺ｪ source 繧・EmptyDynamicContext 縺ｧ隧穂ｾ｡縺吶ｋ縲・
        /// </summary>
        public T GetOrDefaultWithoutContext<T>(T defaultValue = default)
        {
            return GetOrDefault(EmptyDynamicContext.Instance, defaultValue);
        }

        // ================================================================
        // 繝輔ぃ繧ｯ繝医Μ繝｡繧ｽ繝・ラ
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
    // 繧ｸ繧ｧ繝阪Μ繝・け迚・DynamicValue<T>
    // ================================================================

    /// <summary>
    /// 蝙区欠螳壹＆繧後◆蜍慕噪蛟､讒矩菴薙・
    /// DynamicValue&lt;Vector2&gt;, DynamicValue&lt;float&gt; 縺ｪ縺ｩ縺ｧ菴ｿ逕ｨ縲・
    /// </summary>
    /// <typeparam name="T">譛溷ｾ・☆繧句､縺ｮ蝙・/typeparam>
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
        /// 繧ｽ繝ｼ繧ｹ縺瑚ｨｭ螳壹＆繧後※縺・ｋ縺九・
        /// </summary>
        public bool HasSource => _source != null;

        /// <summary>
        /// 繧ｽ繝ｼ繧ｹ縺ｮ遞ｮ蛻･蜷阪・
        /// </summary>
        public string SourceTypeName => _source?.SourceTypeName ?? "None";

        /// <summary>
        /// 繧ｽ繝ｼ繧ｹ縺ｮ繝・ヰ繝・げ諠・ｱ縲・
        /// </summary>
        public string SourceDebugData => _source?.GetDebugData ?? string.Empty;

        /// <summary>
        /// 繧ｽ繝ｼ繧ｹ縺ｸ縺ｮ蜿ら・・亥ｼ上た繝ｼ繧ｹ縺ｮ萓晏ｭ倥く繝ｼ蜿門ｾ礼畑・・
        /// </summary>
        internal IDynamicSource Source => _source;

        /// <summary>
        /// 蛟､繧定ｩ穂ｾ｡縺励※ DynamicVariant 縺ｨ縺励※霑斐☆縲・
        /// </summary>
        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (_source == null)
                return DynamicVariant.FromObject(_defaultValue);

            return _source.Evaluate(context);
        }

        /// <summary>
        /// 蝙句ｮ牙・縺ｫ蛟､繧貞叙蠕励・
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
        /// 蛟､繧貞叙蠕励∝､ｱ謨玲凾縺ｯ繝・ヵ繧ｩ繝ｫ繝亥､繧定ｿ斐☆縲・
        /// </summary>
        public T GetOrDefault(IDynamicContext context, T defaultValue = default)
        {
            if (_source == null)
                return _defaultValue;

            return TryGet(context, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 譁・ц荳崎ｦ√↑隧穂ｾ｡繧定｡後≧縲・
        /// 繝ｪ繝・Λ繝ｫ繧・null-safe 縺ｪ source 繧・EmptyDynamicContext 縺ｧ隧穂ｾ｡縺吶ｋ縲・
        /// </summary>
        public T GetOrDefaultWithoutContext(T defaultValue = default)
        {
            return GetOrDefault(EmptyDynamicContext.Instance, defaultValue);
        }

        /// <summary>
        /// 繧ｳ繝ｳ繝・く繧ｹ繝医°繧牙､繧定ｧ｣豎ｺ・医す繝ｧ繝ｼ繝医き繝・ヨ・峨・
        /// </summary>
        public T Resolve(IDynamicContext context) => GetOrDefault(context);

        /// <summary>
        /// bool 蛟､繧定ｩ穂ｾ｡縺吶ｋ繝倥Ν繝代・・・ynamicValue&lt;bool&gt; 蟆ら畑・峨・
        /// 隧穂ｾ｡螟ｱ謨玲凾縺ｯ false 繧定ｿ斐☆縲・
        /// </summary>
        public bool EvaluateBool(IDynamicContext context)
        {
            var variant = Evaluate(context);
            if (variant.TryGet<bool>(out var b))
                return b;
            return false;
        }

        /// <summary>
        /// Scope + Vars 縺九ｉ蛟､繧貞叙蠕暦ｼ医す繝ｧ繝ｼ繝医き繝・ヨ・峨・
        /// </summary>
        public T Get(IScopeNode scope, IVarStore vars, T defaultValue = default)
        {
            var ctx = new SimpleDynamicContext(vars, scope);
            return GetOrDefault(ctx, defaultValue);
        }

        /// <summary>
        /// 蠑上た繝ｼ繧ｹ縺ｮ蝣ｴ蜷医∽ｾ晏ｭ倥く繝ｼ荳隕ｧ繧貞叙蠕励・
        /// MonitorChannelHub 縺ｮ EventDriven 繝｢繝ｼ繝峨〒菴ｿ逕ｨ縲・
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> GetDependentKeys()
        {
            if (_source is IExpressionSource exprSource)
                return exprSource.GetDependentKeys();
            return null;
        }

        bool ShouldShowDefaultValue() => _source == null;

        // ================================================================
        // 髱槭ず繧ｧ繝阪Μ繝・け迚医→縺ｮ逶ｸ莠貞､画鋤
        // ================================================================

        /// <summary>
        /// 髱槭ず繧ｧ繝阪Μ繝・け DynamicValue 縺ｸ縺ｮ證鈴ｻ吝､画鋤縲・
        /// </summary>
        public static implicit operator DynamicValue(DynamicValue<T> typed)
        {
            return DynamicValue.FromSource(typed._source);
        }

        /// <summary>
        /// 髱槭ず繧ｧ繝阪Μ繝・け DynamicValue 縺九ｉ縺ｮ譏守､ｺ螟画鋤縲・
        /// </summary>
        public static explicit operator DynamicValue<T>(DynamicValue untyped)
        {
            // 繝ｪ繝輔Ξ繧ｯ繧ｷ繝ｧ繝ｳ縺ｧ繧ｽ繝ｼ繧ｹ繧貞叙蠕暦ｼ・nternal 繝輔ぅ繝ｼ繝ｫ繝峨↑縺ｮ縺ｧ・・
            // 莉｣譖ｿ譯・ DynamicValue 縺ｫ GetSource() 繧定ｿｽ蜉
            return new DynamicValue<T>();
        }

        // ================================================================
        // 繝輔ぃ繧ｯ繝医Μ繝｡繧ｽ繝・ラ
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
    // DynamicValue<T> 諡｡蠑ｵ繝｡繧ｽ繝・ラ
    // ================================================================

    public static class DynamicValueExtensions
    {
        /// <summary>
        /// int 逕ｨ繝ｪ繝・Λ繝ｫ菴懈・縲・
        /// </summary>
        public static DynamicValue<int> FromLiteral(int value)
            => DynamicValue<int>.FromSource(LiteralSource.FromInt(value));

        /// <summary>
        /// float 逕ｨ繝ｪ繝・Λ繝ｫ菴懈・縲・
        /// </summary>
        public static DynamicValue<float> FromLiteral(float value)
            => DynamicValue<float>.FromSource(LiteralSource.FromFloat(value));

        /// <summary>
        /// bool 逕ｨ繝ｪ繝・Λ繝ｫ菴懈・縲・
        /// </summary>
        public static DynamicValue<bool> FromLiteral(bool value)
            => DynamicValue<bool>.FromSource(LiteralSource.FromBool(value));

        /// <summary>
        /// string 逕ｨ繝ｪ繝・Λ繝ｫ菴懈・縲・
        /// </summary>
        public static DynamicValue<string> FromLiteral(string value)
            => DynamicValue<string>.FromSource(LiteralSource.FromString(value));

        /// <summary>
        /// Vector2 逕ｨ繝ｪ繝・Λ繝ｫ菴懈・縲・
        /// </summary>
        public static DynamicValue<Vector2> FromLiteral(Vector2 value)
            => DynamicValue<Vector2>.FromSource(new LiteralVector2Source(value));

        /// <summary>
        /// Vector3 逕ｨ繝ｪ繝・Λ繝ｫ菴懈・縲・
        /// </summary>
        public static DynamicValue<Vector3> FromLiteral(Vector3 value)
            => DynamicValue<Vector3>.FromSource(new LiteralVector3Source(value));

        /// <summary>
        /// Table 逕ｨ繝ｪ繝・Λ繝ｫ菴懈・縲・
        /// </summary>
        public static DynamicValue<Table> FromLiteral(Table value)
            => DynamicValue<Table>.FromSource(new LiteralTableSource(value));

        /// <summary>
        /// VarStorePayload 逕ｨ縺ｮ莠呈鋤繝ｪ繝・Λ繝ｫ菴懈・縲・
        /// </summary>
        public static DynamicValue<VarStorePayload> FromLiteral(VarStorePayload value)
            => DynamicValue<VarStorePayload>.FromSource(new LiteralVarStorePayloadSource(value));

        /// <summary>
        /// CommandListData 逕ｨ繝ｪ繝・Λ繝ｫ菴懈・縲・
        /// </summary>
        public static DynamicValue<CommandListData> FromLiteral(CommandListData value)
            => DynamicValue<CommandListData>.FromSource(new LiteralCommandListDataSource(value));
    }

    // ================================================================
    // Simple Dynamic Context
    // ================================================================

    /// <summary>
    /// 繧ｷ繝ｳ繝励Ν縺ｪ IDynamicContext 螳溯｣・・
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
