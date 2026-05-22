using System.Collections.Generic;
using Game;
using Game.Common;
using UnityEngine;
using VContainer;
namespace Game.Common
{
    public interface IProjectBlackboardService : IBlackboardService { }
    public interface IPlatformBlackboardService : IBlackboardService { }
    public interface IGlobalBlackboardService : IBlackboardService { }
    public interface ISceneBlackboardService : IBlackboardService { }
    public interface IEntityBlackboardService : IBlackboardService { }
    public interface IFieldBlackboardService : IBlackboardService { }
    public interface IUIBlackboardService : IBlackboardService { }
    public interface IUIElementBlackboardService : IBlackboardService { }
    public interface IRuntimeBlackboardService : IBlackboardService { }

    public interface IBlackboardService
    {
        /// <summary>この LifetimeScope にローカルな VarStore。</summary>
        IVarStore LocalVars { get; }

        // ---- Local-only operations (do not touch parent scopes) ----
        bool TryLocalGetVariant(int varId, out DynamicVariant value);
        bool TryLocalSetVariant(int varId, in DynamicVariant value);

        // ---- Global (hierarchical) operations - will consult parent scopes when not found locally) ----
        bool TryGlobalGetVariant(int varId, out DynamicVariant value);
        bool TryGlobalSetVariant(int varId, in DynamicVariant value);
        bool TryGlobalSetVariant(int varId, in DynamicVariant value, GlobalBlackboardSetFallback fallback);

        DynamicVariant GlobalGetVariant(int varId, DynamicVariant defaultValue)
        {
            if (TryGlobalGetVariant(varId, out var value))
                return value;
            return defaultValue;
        }

        IScopeNode FindGlobalVariantScope(int varId);

        /// <summary>
        /// LocalVars の内容を dest にマージする。
        /// overwrite = true で、既存のキーを上書き。
        /// </summary>
        void MergeInto(IVarStore dest, bool overwrite = false);

        IScopeNode ScopeNode { get; }
    }

    public enum GlobalBlackboardSetFallback
    {
        Fail = 0,
        CreateLocal = 1,
        CreateGameLogicRoot = 2,
        CreateRoot = 3,
    }

    public sealed class BlackboardService : IBlackboardService,
        IProjectBlackboardService,
        IPlatformBlackboardService,
        IGlobalBlackboardService,
        ISceneBlackboardService,
        IEntityBlackboardService,
        IFieldBlackboardService,
        IUIBlackboardService,
        IUIElementBlackboardService,
        IRuntimeBlackboardService
    {
        readonly VarStore _localVars = new();

        public IVarStore LocalVars => _localVars;
        public IScopeNode ScopeNode => _scope;
        readonly IScopeNode _scope;

        public BlackboardService(IScopeNode scope)
        {
            _scope = scope;
        }

        // ---- Local-only operations -------------------------------------------------
        public bool TryLocalGetVariant(int varId, out DynamicVariant value)
        {
            return _localVars.TryGetVariant(varId, out value);
        }

        public bool TryLocalSetVariant(int varId, in DynamicVariant value)
        {
            return _localVars.TrySetVariant(varId, in value);
        }

        // ---- Global (hierarchical) operations -------------------------------------
        public bool TryGlobalGetVariant(int varId, out DynamicVariant value)
        {
            // 1) check local store
            if (_localVars.TryGetVariant(varId, out value))
                return true;

            if (VerifiedValueRuntimeBridge.IsActive)
            {
                VerifiedValueAccessDiagnostics.ReportBlockedAccessOnce(
                    "BlackboardService.TryGlobalGetVariant",
                    "Wave D verified value authority blocked BlackboardService.TryGlobalGetVariant hierarchical global read. Global blackboard traversal is no longer accepted runtime value truth.");
                value = default;
                return false;
            }

            // 2) ascend parent scopes and ask their local stores
            var node = _scope?.Parent;
            while (node != null)
            {
                var resolver = node.Resolver;
                if (resolver != null && resolver.TryResolve(typeof(IBlackboardService), out var parentObj) && parentObj is IBlackboardService parent)
                {
                    // Ask parent local store only to avoid re-walking this scope's hierarchy redundantly
                    if (parent.TryLocalGetVariant(varId, out value))
                        return true;
                }

                node = node.Parent;
            }

            value = default;
            return false;
        }

        public IScopeNode FindGlobalVariantScope(int varId)
        {
            // 1) check local store
            if (_localVars.Contains(varId))
                return _scope;

            if (VerifiedValueRuntimeBridge.IsActive)
            {
                VerifiedValueAccessDiagnostics.ReportBlockedAccessOnce(
                    "BlackboardService.FindGlobalVariantScope",
                    "Wave D verified value authority blocked BlackboardService.FindGlobalVariantScope hierarchical traversal. Global blackboard scope discovery is no longer accepted runtime value truth.");
                return null;
            }

            // 2) ascend parent scopes and ask their local stores
            var node = _scope?.Parent;
            while (node != null)
            {
                var resolver = node.Resolver;
                if (resolver != null && resolver.TryResolve(typeof(IBlackboardService), out var parentObj) && parentObj is IBlackboardService parent)
                {
                    // Ask parent local store only to avoid re-walking this scope's hierarchy redundantly
                    if (parent.TryLocalGetVariant(varId, out var _))
                        return node;
                }

                node = node.Parent;
            }

            return null;
        }

        public bool TryGlobalSetVariant(int varId, in DynamicVariant value)
        {
            return TryGlobalSetVariant(varId, value, GlobalBlackboardSetFallback.CreateGameLogicRoot);
        }

        public bool TryGlobalSetVariant(int varId, in DynamicVariant value, GlobalBlackboardSetFallback fallback)
        {

            // If var exists locally, set it here
            if (_localVars.Contains(varId))
            {
                return _localVars.TrySetVariant(varId, in value);
            }

            if (VerifiedValueRuntimeBridge.IsActive)
            {
                VerifiedValueAccessDiagnostics.ReportBlockedAccessOnce(
                    "BlackboardService.TryGlobalSetVariant",
                    "Wave D verified value authority blocked BlackboardService.TryGlobalSetVariant hierarchical global write. Global blackboard fallback writes are no longer accepted runtime value truth.");
                return false;
            }

            // Otherwise, walk upwards to find the nearest scope that contains the var
            var node = _scope?.Parent;
            while (node != null)
            {
                var resolver = node.Resolver;
                if (resolver != null && resolver.TryResolve(typeof(IBlackboardService), out var parentObj) && parentObj is IBlackboardService parent)
                {
                    if (parent.LocalVars.Contains(varId))
                    {
                        return parent.TryLocalSetVariant(varId, in value);
                    }
                }

                node = node.Parent;
            }

            if (fallback == GlobalBlackboardSetFallback.CreateLocal)
            {
                return _localVars.TrySetVariant(varId, in value);
            }

            if (fallback == GlobalBlackboardSetFallback.CreateGameLogicRoot)
            {
                var root = FindGameLogicRootBlackboard();

                return root.LocalVars.TrySetVariant(varId, in value);
            }

            if (fallback == GlobalBlackboardSetFallback.CreateRoot)
            {
                var root = FindRootBlackboard();
                if (VarIdResolver.TryGetStableKey(varId, out var stableKey))
                {
                    Debug.LogWarning($"[BlackboardService] varId={varId} (key='{stableKey}') not found; CreateRoot fallback writes to {FormatScopePath(root.ScopeNode)}.");
                }
                else
                {
                    Debug.LogWarning($"[BlackboardService] varId={varId} not found; CreateRoot fallback writes to {FormatScopePath(root.ScopeNode)}.");
                }
                return root.LocalVars.TrySetVariant(varId, in value);
            }

            Debug.LogWarning($"[BlackboardService] varId={varId} not found; fallback=Fail leaves it unset. origin={FormatScopePath(_scope)}");
            // Not found in any parent -> fail
            return false;
        }

        IBlackboardService FindRootBlackboard()
        {
            IBlackboardService root = this;
            var node = _scope?.Parent;
            while (node != null)
            {
                var resolver = node.Resolver;
                if (resolver != null && resolver.TryResolve(typeof(IBlackboardService), out var parentObj) && parentObj is IBlackboardService parent)
                {
                    root = parent;
                }

                node = node.Parent;
            }

            return root;
        }

        IBlackboardService FindGameLogicRootBlackboard()
        {
            var logicRoot = ScopeNodeHierarchy.FindNearestGameLogicRoot(_scope, includeSelf: true);
            if (logicRoot != null)
            {
                var resolver = logicRoot.Resolver;
                if (resolver != null && resolver.TryResolve(typeof(IBlackboardService), out var rootObj) && rootObj is IBlackboardService root)
                    return root;
            }

            return FindRootBlackboard();
        }

        public void MergeInto(IVarStore dest, bool overwrite = false)
        {
            if (dest == null)
                return;

            _localVars.MergeInto(dest, overwrite);
        }

        static string FormatScopePath(IScopeNode scope)
        {
            if (scope == null)
                return "<null>";

            var path = scope.GetPathFromRoot();
            if (path == null || path.Count == 0)
                return FormatScopeDescriptor(scope);

            var parts = new List<string>(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                var descriptor = path[i];
                if (descriptor == null)
                    continue;
                parts.Add(FormatScopeDescriptor(descriptor));
            }

            return parts.Count > 0 ? string.Join(" > ", parts) : FormatScopeDescriptor(scope);
        }

        static string FormatScopeDescriptor(IScopeNode node)
        {
            var identity = node.Identity;
            if (identity != null)
            {
                if (!string.IsNullOrEmpty(identity.Id))
                    return $"{identity.Kind}:{identity.Id}";
                return identity.Kind.ToString();
            }

            return node.Kind.ToString();
        }

    }
}
