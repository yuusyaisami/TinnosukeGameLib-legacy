#nullable enable
using System;
using System.Collections.Generic;
using Game.VarStoreKeys;
using Game;
using UnityEngine;

namespace Game.Common
{
    public enum VerifiedValueInitPhase
    {
        Create = 10,
        Acquire = 20,
        Reset = 30,
    }

    public enum VerifiedValueInitApplyResultKind
    {
        NotAvailable = 10,
        Applied = 20,
        Rejected = 30,
    }

    public readonly struct VerifiedValueInitApplyResult
    {
        public VerifiedValueInitApplyResult(VerifiedValueInitApplyResultKind kind, int appliedEntryCount = 0, string? failureReason = null)
        {
            if (kind == default)
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Verified value init results must provide a defined result kind.");

            if (appliedEntryCount < 0)
                throw new ArgumentOutOfRangeException(nameof(appliedEntryCount), appliedEntryCount, "Verified value init results must not report a negative applied entry count.");

            if (failureReason != null && string.IsNullOrWhiteSpace(failureReason))
                throw new ArgumentException("Verified value init result failure reasons must be null or non-empty.", nameof(failureReason));

            Kind = kind;
            AppliedEntryCount = appliedEntryCount;
            FailureReason = failureReason;
        }

        public VerifiedValueInitApplyResultKind Kind { get; }

        public int AppliedEntryCount { get; }

        public string? FailureReason { get; }

        public bool IsApplied => Kind == VerifiedValueInitApplyResultKind.Applied;

        public bool IsRejected => Kind == VerifiedValueInitApplyResultKind.Rejected;

        public static VerifiedValueInitApplyResult NotAvailable()
        {
            return new VerifiedValueInitApplyResult(VerifiedValueInitApplyResultKind.NotAvailable);
        }

        public static VerifiedValueInitApplyResult Applied(int appliedEntryCount)
        {
            return new VerifiedValueInitApplyResult(VerifiedValueInitApplyResultKind.Applied, appliedEntryCount);
        }

        public static VerifiedValueInitApplyResult Rejected(string failureReason)
        {
            return new VerifiedValueInitApplyResult(VerifiedValueInitApplyResultKind.Rejected, 0, failureReason);
        }
    }

    public interface IVerifiedValueRuntimeSession
    {
        bool TryResolveValueKey(string stableKey, out int valueKeyId);

        bool TryGetStableKey(int valueKeyId, out string stableKey);

        VerifiedValueInitApplyResult ApplyLocalBlackboardInit(IScopeNode scope, IBlackboardService blackboard, VerifiedValueInitPhase phase, DynamicEvaluationRuntime runtime);
    }

    public static class VerifiedValueRuntimeBridge
    {
        static IVerifiedValueRuntimeSession? s_session;

        public static bool IsActive => s_session != null;

        public static void Activate(IVerifiedValueRuntimeSession session)
        {
            s_session = session ?? throw new ArgumentNullException(nameof(session));
            VarIdResolver.ClearCachedResolutions();
            VerifiedValueAccessDiagnostics.ResetBlockedAccessReports();
        }

        public static void Deactivate()
        {
            s_session = null;
            VarIdResolver.ClearCachedResolutions();
            VerifiedValueAccessDiagnostics.ResetBlockedAccessReports();
        }

        public static bool TryGetSession(out IVerifiedValueRuntimeSession? session)
        {
            session = s_session;
            return session != null;
        }
    }

    static class VerifiedValueAccessDiagnostics
    {
        static readonly object Gate = new();
        static readonly HashSet<string> s_reportedKeys = new(StringComparer.Ordinal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ResetBlockedAccessReports();
        }

        internal static void ResetBlockedAccessReports()
        {
            lock (Gate)
            {
                s_reportedKeys.Clear();
            }
        }

        internal static void ReportBlockedAccessOnce(string key, string message)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Blocked access diagnostics require a stable key.", nameof(key));

            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Blocked access diagnostics require a message.", nameof(message));

            lock (Gate)
            {
                if (!s_reportedKeys.Add(key))
                    return;
            }

            Debug.LogError(message);
        }
    }
}