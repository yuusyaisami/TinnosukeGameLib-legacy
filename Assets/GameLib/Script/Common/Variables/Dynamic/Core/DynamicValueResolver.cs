// Game.Common.DynamicValueResolver.cs
//
// DynamicValueResolver - 動的値解決の共通ヘルパ
//
// 設計決定:
// - Scalar/Blackboard/VarStore への問い合わせを一元化
// - 新しい DynamicValue システムと互換

using System;
using System.Globalization;
using Game.Scalar;
using Game.Commands;
using VContainer;

namespace Game.Common
{
    /// <summary>
    /// Dynamic 値取得時の共通解決ヘルパ。
    /// Scalar / Blackboard / Vars(VarStore) への問い合わせを一元化。
    /// </summary>
    public static class DynamicValueResolver
    {
        static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        // ================================================================
        // Scalar 解決（既存 DynamicScalarResolver から移植・整理）
        // ================================================================

        /// <summary>
        /// 自スコープの IBaseScalarService から ScalarKey で float を取得。
        /// </summary>
        public static bool TryGetScalarFromSelf(
            IScopeNode scope,
            ScalarKey key,
            out float value)
        {
            value = 0f;
            if (!HasScalarKey(key))
                return false;

            var svc = ResolveSelfScalarService(scope);
            if (svc == null)
                return false;

            return svc.LocalTryGet(key, out value);
        }

        /// <summary>
        /// 別スコープの IBaseScalarService から ScalarKey で float を取得。
        /// </summary>
        public static bool TryGetScalarFromOther(
            IScopeNode scope,
            ScalarKey key,
            CommandTargetIdentityFilter filter,
            out float value)
        {
            value = 0f;
            if (!HasScalarKey(key))
                return false;

            var svc = ResolveScalarServiceInOtherScope(scope, filter);
            if (svc == null)
                return false;

            return svc.LocalTryGet(key, out value);
        }

        public static bool HasScalarKey(ScalarKey key)
        {
            return key.Id != 0 || !string.IsNullOrEmpty(key.Name);
        }

        static IBaseScalarService ResolveSelfScalarService(IScopeNode scope)
        {
            if (scope?.Resolver == null)
                return null;

            if (scope.Resolver.TryResolve<IBaseScalarService>(out var svc))
                return svc;

            return null;
        }

        static IBaseScalarService ResolveScalarServiceInOtherScope(
            IScopeNode origin,
            CommandTargetIdentityFilter filter)
        {
            if (origin?.Resolver == null)
                return null;

            if (!origin.Resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var registry))
                return null;

            var targetScope = registry.Resolve(filter, origin);
            if (targetScope?.Resolver == null)
                return null;

            if (targetScope.Resolver.TryResolve<IBaseScalarService>(out var svc))
                return svc;

            return null;
        }

        // ================================================================
        // Blackboard 解決（新規）
        // ================================================================

        /// <summary>
        /// 自スコープの Blackboard から値を取得。
        /// readScope により Local/Global を選択。
        /// </summary>
        public static bool TryGetFromSelfBlackboard<T>(
            IScopeNode scope,
            string key,
            out T value,
            BlackboardReadScope readScope = BlackboardReadScope.Local)
        {
            value = default;
            if (scope?.Resolver == null || string.IsNullOrEmpty(key))
                return false;

            if (!scope.Resolver.TryResolve<IBlackboardService>(out var bb))
                return false;

            if (!VarIdResolver.TryResolve(key, out var varId) || varId == 0)
                return false;

            if (TryGetVariant(bb, varId, readScope, out var v) && v.TryGet(out value))
                return true;

            if (TryGetManagedRef(scope, bb, varId, readScope, out var managed) && managed is T typed)
            {
                value = typed;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 別スコープの Blackboard から値を取得。
        /// readScope により Local/Global を選択。
        /// </summary>
        public static bool TryGetFromOtherBlackboard<T>(
            IScopeNode origin,
            string key,
            CommandTargetIdentityFilter filter,
            out T value,
            BlackboardReadScope readScope = BlackboardReadScope.Local)
        {
            value = default;
            if (origin?.Resolver == null || string.IsNullOrEmpty(key))
                return false;

            if (!origin.Resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var registry))
                return false;

            var targetScope = registry.Resolve(filter, origin);
            if (targetScope?.Resolver == null)
                return false;

            if (!targetScope.Resolver.TryResolve<IBlackboardService>(out var bb))
                return false;

            if (!VarIdResolver.TryResolve(key, out var varId) || varId == 0)
                return false;

            if (TryGetVariant(bb, varId, readScope, out var v) && v.TryGet(out value))
                return true;

            if (TryGetManagedRef(targetScope, bb, varId, readScope, out var managed) && managed is T typed)
            {
                value = typed;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 自スコープの Blackboard から文字列として取得（数値等は自動変換）。
        /// </summary>
        public static bool TryGetStringFromSelfBlackboard(
            IScopeNode scope,
            string key,
            out string value,
            BlackboardReadScope readScope = BlackboardReadScope.Local)
        {
            value = null;
            if (scope?.Resolver == null || string.IsNullOrEmpty(key))
                return false;

            if (!scope.Resolver.TryResolve<IBlackboardService>(out var bb))
                return false;

            if (!VarIdResolver.TryResolve(key, out var varId) || varId == 0)
                return false;

            if (TryGetVariant(bb, varId, readScope, out var v))
            {
                value = v.ToString();
                return true;
            }

            if (TryGetManagedRef(scope, bb, varId, readScope, out var managed) && managed != null)
            {
                value = managed.ToString();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 別スコープの Blackboard から文字列として取得。
        /// </summary>
        public static bool TryGetStringFromOtherBlackboard(
            IScopeNode origin,
            string key,
            CommandTargetIdentityFilter filter,
            out string value,
            BlackboardReadScope readScope = BlackboardReadScope.Local)
        {
            value = null;
            if (origin?.Resolver == null || string.IsNullOrEmpty(key))
                return false;

            if (!origin.Resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var registry))
                return false;

            var targetScope = registry.Resolve(filter, origin);
            if (targetScope?.Resolver == null)
                return false;

            if (!targetScope.Resolver.TryResolve<IBlackboardService>(out var bb))
                return false;

            if (!VarIdResolver.TryResolve(key, out var varId) || varId == 0)
                return false;

            if (TryGetVariant(bb, varId, readScope, out var v))
            {
                value = v.ToString();
                return true;
            }

            if (TryGetManagedRef(targetScope, bb, varId, readScope, out var managed) && managed != null)
            {
                value = managed.ToString();
                return true;
            }

            return false;
        }

        static bool TryGetVariant(IBlackboardService bb, int varId, BlackboardReadScope readScope, out DynamicVariant value)
        {
            value = default;
            if (bb == null || varId == 0)
                return false;

            if (readScope == BlackboardReadScope.Global && VerifiedValueRuntimeBridge.IsActive)
            {
                VerifiedValueAccessDiagnostics.ReportBlockedAccessOnce(
                    "DynamicValueResolver.TryGetVariant.Global",
                    "Wave D verified value authority blocked DynamicValueResolver global blackboard read. DynamicValue global traversal must migrate to verified value authority.");
                return false;
            }

            return readScope == BlackboardReadScope.Global
                ? bb.TryGlobalGetVariant(varId, out value)
                : bb.TryLocalGetVariant(varId, out value);
        }

        static bool TryGetManagedRef(IScopeNode scope, IBlackboardService bb, int varId, BlackboardReadScope readScope, out object value)
        {
            value = null;
            if (bb == null || varId == 0)
                return false;

            if (readScope == BlackboardReadScope.Global && VerifiedValueRuntimeBridge.IsActive)
            {
                VerifiedValueAccessDiagnostics.ReportBlockedAccessOnce(
                    "DynamicValueResolver.TryGetManagedRef.Global",
                    "Wave D verified value authority blocked DynamicValueResolver global managed-ref traversal. DynamicValue global traversal must migrate to verified value authority.");
                return false;
            }

            if (readScope == BlackboardReadScope.Local)
            {
                return bb.LocalVars != null && bb.LocalVars.TryGetManagedRef(varId, out value);
            }

            // Global: walk self -> parents and ask each LocalVars for managed ref.
            var node = scope;
            while (node != null)
            {
                var resolver = node.Resolver;
                if (resolver != null && resolver.TryResolve<IBlackboardService>(out var current) && current != null)
                {
                    if (current.LocalVars != null && current.LocalVars.TryGetManagedRef(varId, out value))
                        return true;
                }

                node = node.Parent;
            }

            value = null;
            return false;
        }

        // ================================================================
        // VarStore ヘルパ（vNext）
        // ================================================================

        public static bool TryGetFromVars<T>(
            IVarStore vars,
            int varId,
            out T value)
        {
            value = default;
            if (vars == null || varId == 0)
                return false;

            if (!vars.TryGetVariant(varId, out var variant))
                return false;

            return variant.TryGet(out value);
        }

        public static bool TryGetStringFromVars(
            IVarStore vars,
            int varId,
            out string value)
        {
            value = null;
            if (vars == null || varId == 0)
                return false;

            if (!vars.TryGetVariant(varId, out var variant))
                return false;

            if (variant.TryGet(out value))
                return true;

            value = variant.ToString();
            return true;
        }

        // ================================================================
        // 文字列変換ヘルパ（DynamicString 用）
        // ================================================================

        /// <summary>
        /// 任意の値を文字列に変換。
        /// </summary>
        public static string FormatAsString(object value)
        {
            if (value == null)
                return string.Empty;

            if (value is string s)
                return s;

            if (value is IFormattable formattable)
                return formattable.ToString(null, Culture);

            return value.ToString();
        }

        /// <summary>
        /// float を文字列に変換。
        /// </summary>
        public static string FormatFloat(float value, string format = null)
        {
            return string.IsNullOrEmpty(format)
                ? value.ToString(Culture)
                : value.ToString(format, Culture);
        }
    }
}
