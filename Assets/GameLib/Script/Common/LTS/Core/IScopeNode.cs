#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Actions;
using UnityEngine;

namespace Game
{
    public interface IScopeNode
    {
        IScopeNode? Parent { get; }
        ILTSIdentityService? Identity { get; }
        LifetimeScopeKind Kind { get; }
        IRuntimeResolver? Resolver { get; }
        bool IsVisible { get; }
        bool IsActive { get; }
        bool TrySetVisible(bool visible, bool isReset = false);
        bool TrySetActive(bool active, bool isReset = false);
        UniTask SetActiveAsync(bool active, bool isReset = false, CancellationToken ct = default);
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

    public interface IScopeTickHandler
    {
        void Tick();
    }

    public interface IScopeLateTickHandler
    {
        void LateTick();
    }

    public interface IScopeFixedTickHandler
    {
        void FixedTick();
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

        public ScopeAcquireReleaseDispatcher(IRuntimeResolver resolver)
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            _acquireHandlers = ResolveHandlers<IScopeAcquireHandler>(resolver);
            _releaseHandlers = ResolveHandlers<IScopeReleaseHandler>(resolver);
        }

        public void Acquire(IScopeNode scope, bool isReset)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            for (int i = 0; i < _acquireHandlers.Length; i++)
            {
                var handler = _acquireHandlers[i];
                if (ScopeHandlerOwnershipUtility.ShouldInvokeHandler(scope, handler))
                    handler.OnAcquire(scope, isReset);
            }
        }

        public void Release(IScopeNode scope, bool isReset)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            for (int i = 0; i < _releaseHandlers.Length; i++)
            {
                var handler = _releaseHandlers[i];
                if (ScopeHandlerOwnershipUtility.ShouldInvokeHandler(scope, handler))
                    handler.OnRelease(scope, isReset);
            }
        }

        static THandler[] ResolveHandlers<THandler>(IRuntimeResolver resolver) where THandler : class
        {
            if (!resolver.TryResolve<IReadOnlyList<THandler>>(out var list) || list == null || list.Count == 0)
                return Array.Empty<THandler>();

            var result = new List<THandler>(list.Count);
            var seen = new HashSet<object>(ReferenceEqualityComparer<object>.Instance);
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item != null && seen.Add(item))
                    result.Add(item);
            }
            return result.ToArray();
        }
    }

    internal static class ScopeHandlerOwnershipUtility
    {
        static readonly bool StrictUnknownOwnerHandlers = false;
        static readonly bool EmitUnknownOwnerWarnings = false;
        static readonly HashSet<Type> MissingOwnerWarningTypes = new();
        static readonly object MissingOwnerWarningGate = new();

        public static bool ShouldInvokeHandler(IScopeNode scope, object? handler)
        {
            if (scope == null || handler == null)
                return false;

            if (handler is IScopeNode handlerScope)
                return ReferenceEquals(handlerScope, scope);

            if (handler is Component component)
            {
                if (!ScopeFeatureInstallerUtility.TryGetNearestScopeNode(component, includeInactive: true, out var owner) || owner == null)
                    return false;

                return ReferenceEquals(owner, scope);
            }

            if (TryGetOwnerScopeFromHandler(handler, out var ownerScope))
            {
                if (ownerScope != null)
                    return ReferenceEquals(ownerScope, scope);

                return HandleUnknownOwner(scope, handler);
            }

            return HandleUnknownOwner(scope, handler);
        }

        public static bool TryGetOwnerScopeFromHandler(object? handler, out IScopeNode? owner)
        {
            owner = null;
            if (handler == null)
                return false;

            var type = handler.GetType();
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;

            var prop = type.GetProperty("OwnerScope", flags)
                       ?? type.GetProperty("Scope", flags)
                       ?? type.GetProperty("Owner", flags);

            if (prop != null && typeof(IScopeNode).IsAssignableFrom(prop.PropertyType))
            {
                owner = prop.GetValue(handler) as IScopeNode;
                return true;
            }

            var field = type.GetField("_ownerScope", flags)
                        ?? type.GetField("_owner", flags)
                        ?? type.GetField("_scope", flags)
                        ?? type.GetField("ownerScope", flags)
                        ?? type.GetField("scope", flags);

            if (field != null && typeof(IScopeNode).IsAssignableFrom(field.FieldType))
            {
                owner = field.GetValue(handler) as IScopeNode;
                return true;
            }

            return false;
        }

        static bool HandleUnknownOwner(IScopeNode scope, object handler)
        {
            LogMissingOwnerOnce(scope, handler);
            return !StrictUnknownOwnerHandlers;
        }

        static void LogMissingOwnerOnce(IScopeNode scope, object handler)
        {
            if (!EmitUnknownOwnerWarnings)
                return;

            var type = handler.GetType();
            lock (MissingOwnerWarningGate)
            {
                if (!MissingOwnerWarningTypes.Add(type))
                    return;
            }

            Debug.LogWarning(
                $"[ScopeAcquireReleaseDispatcher] Handler '{type.FullName}' has no owner scope for '{scope.GetType().Name}'.");
        }
    }
}
