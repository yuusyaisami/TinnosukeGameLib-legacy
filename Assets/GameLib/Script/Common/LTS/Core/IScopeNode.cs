#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Actions;
using VContainer;

namespace Game
{
    /// <summary>
    /// Common hierarchical scope interface shared by BaseLifetimeScope and RuntimeLifetimeScope.
    /// Phase0 introduces this as the single scope type exposed to installers and services.
    /// </summary>
    public interface IScopeNode
    {
        /// <summary>Nearest parent scope in the hierarchy, if any.</summary>
        IScopeNode? Parent { get; }

        /// <summary>Identity service bound to this scope.</summary>
        ILTSIdentityService? Identity { get; }

        /// <summary>Kind of this scope (usually from Identity.Kind).</summary>
        LifetimeScopeKind Kind { get; }

        /// <summary>Resolver for this scope (VContainer container for LTS, RuntimeResolver for Runtime).</summary>
        IObjectResolver? Resolver { get; }

        /// <summary>
        /// Scope-level visibility flag.
        /// Note: Phase0 only defines the flag; systems may ignore it.
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// Scope-level active flag.
        /// When toggled, IScopeAcquireHandler / IScopeReleaseHandler should be invoked.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Set scope visibility. Returns false if unsupported.
        /// </summary>
        bool TrySetVisible(bool visible, bool isReset = false);

        /// <summary>
        /// Set scope active state. Returns false if unsupported.
        /// </summary>
        bool TrySetActive(bool active, bool isReset = false);

        /// <summary>
        /// Set scope active state asynchronously.
        /// Pool/spawn systems should prefer this API.
        /// </summary>
        UniTask SetActiveAsync(bool active, bool isReset = false, CancellationToken ct = default);

        /// <summary>Path from root scope to this node, inclusive.</summary>
        IReadOnlyList<IScopeNode>? GetPathFromRoot();
    }

    public interface IScopeAcquireHandler
    {
        void OnAcquire(IScopeNode scope, bool isReset);
    }

    public interface IScopeReleaseHandler
    {
        void OnRelease(IScopeNode scope, bool isReset);
    }

    public interface IScopeAcquireReleaseDispatcher
    {
        void Acquire(IScopeNode scope, bool isReset);
        void Release(IScopeNode scope, bool isReset);
    }

    public sealed class ScopeAcquireReleaseDispatcher : IScopeAcquireReleaseDispatcher
    {
        readonly IScopeAcquireHandler[] _acquireHandlers;
        readonly IScopeReleaseHandler[] _releaseHandlers;

        public ScopeAcquireReleaseDispatcher(IObjectResolver resolver)
        {
            _acquireHandlers = ResolveHandlers<IScopeAcquireHandler>(resolver);
            _releaseHandlers = ResolveHandlers<IScopeReleaseHandler>(resolver);

            // デバッグ: 収集されたハンドラの数と型を出力

            //UnityEngine.Debug.Log($"[ScopeAcquireReleaseDispatcher] AcquireHandlers count={_acquireHandlers.Length}");
            for (int i = 0; i < _acquireHandlers.Length; i++)
            {
                var h = _acquireHandlers[i];
                //if (h?.GetType() == typeof(GameStateMachineService))
                //UnityEngine.Debug.Log($"  [{i}] {h?.GetType().Name ?? "null"} hash={h?.GetHashCode()}");
            }
        }

        public void Acquire(IScopeNode scope, bool isReset)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            for (int i = 0; i < _acquireHandlers.Length; i++)
            {
                var handler = _acquireHandlers[i];
                if (!ShouldInvokeHandler(scope, handler))
                    continue;
                handler?.OnAcquire(scope, isReset);
            }
        }

        public void Release(IScopeNode scope, bool isReset)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            for (int i = 0; i < _releaseHandlers.Length; i++)
            {
                var handler = _releaseHandlers[i];
                if (!ShouldInvokeHandler(scope, handler))
                    continue;
                handler?.OnRelease(scope, isReset);
            }
        }

        // NOTE(原因/修正):
        // VContainerのIReadOnlyList<T>解決で同一サービスが複数回返り、
        // さらにサービス系(IScopeNode/Component以外)はスコープ帰属判定が
        // trueになりやすく、OnAcquireが複数回呼ばれていた。
        // 対策として、所有スコープが取得できる場合は一致時のみ実行。
        static bool ShouldInvokeHandler(IScopeNode scope, object handler)
        {
            if (handler == null)
                return false;

            if (handler is IScopeNode handlerScope)
                return ReferenceEquals(handlerScope, scope);

            if (handler is UnityEngine.Component component)
            {
                if (!ScopeFeatureInstallerUtility.TryGetNearestScopeNode(component, includeInactive: true, out var owner) ||
                    owner == null)
                    return false;
                return ReferenceEquals(owner, scope);
            }

            if (TryGetOwnerScopeFromHandler(handler, out var ownerScope) && ownerScope != null)
            {
                return ReferenceEquals(ownerScope, scope);
            }

            return true;
        }

        static bool TryGetOwnerScopeFromHandler(object handler, out IScopeNode? owner)
        {
            owner = null;
            if (handler == null)
                return false;

            var type = handler.GetType();

            // Property check (OwnerScope / Scope / Owner)
            var prop = type.GetProperty("OwnerScope", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                       ?? type.GetProperty("Scope", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                       ?? type.GetProperty("Owner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

            if (prop != null && typeof(IScopeNode).IsAssignableFrom(prop.PropertyType))
            {
                owner = prop.GetValue(handler) as IScopeNode;
                if (owner != null)
                    return true;
            }

            // Field check (_ownerScope / _scope / ownerScope / scope)
            var field = type.GetField("_ownerScope", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                        ?? type.GetField("_scope", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                        ?? type.GetField("ownerScope", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                        ?? type.GetField("scope", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

            if (field != null && typeof(IScopeNode).IsAssignableFrom(field.FieldType))
            {
                owner = field.GetValue(handler) as IScopeNode;
                if (owner != null)
                    return true;
            }

            return false;
        }

        // NOTE(原因/修正):
        // VContainerのIReadOnlyList<THandler>に同一型サービスが重複登録され、
        // ScopeAcquireReleaseDispatcherのAcquireが複数回発火していた。
        // 修正として、Component/IScopeNode以外は型単位で重複を排除する。
        static THandler[] ResolveHandlers<THandler>(IObjectResolver resolver) where THandler : class
        {
            if (resolver != null &&
                resolver.TryResolve<IReadOnlyList<THandler>>(out var list) &&
                list != null &&
                list.Count > 0)
            {

                //UnityEngine.Debug.Log($"[ResolveHandlers<{typeof(THandler).Name}>] Input list count={list.Count}");

                var result = new List<THandler>(list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    var item = list[i];
                    var added = false;
                    if (item != null)
                    {
                        added = true;
                        var itemType = item.GetType();
                        for (int j = 0; j < result.Count; j++)
                        {
                            var existing = result[j];
                            if (ReferenceEquals(existing, item))
                            {
                                added = false;
                                break;
                            }

                            // サービス系（Component でも IScopeNode でもない）重複は型単位で排除
                            if (existing != null &&
                                item is not UnityEngine.Component &&
                                item is not IScopeNode &&
                                existing.GetType() == itemType)
                            {
                                added = false;
                                break;
                            }
                        }
                    }

                    //UnityEngine.Debug.Log($"  [{i}] {item?.GetType().Name ?? "null"} hash={item?.GetHashCode()} added={added}");
                    if (added)
                        result.Add(item!);
                }

                //  UnityEngine.Debug.Log($"[ResolveHandlers<{typeof(THandler).Name}>] Output count={result.Count}");
                return result.ToArray();
            }

            return Array.Empty<THandler>();
        }
    }
}
