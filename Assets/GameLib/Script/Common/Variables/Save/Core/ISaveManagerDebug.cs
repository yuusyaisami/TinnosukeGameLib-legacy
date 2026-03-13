#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Save
{
    public enum SaveOperationKind : byte
    {
        Save = 0,
        Load = 1,
        Clear = 2,
    }

    public readonly struct SaveOperationRecord
    {
        public readonly long UtcTicks;
        public readonly SaveOperationKind Kind;
        public readonly SaveContext Context;
        public readonly SaveResult Result;
        public readonly string Key;
        public readonly int BytesLength;

        public SaveOperationRecord(
            long utcTicks,
            SaveOperationKind kind,
            SaveContext context,
            SaveResult result,
            string key,
            int bytesLength)
        {
            UtcTicks = utcTicks;
            Kind = kind;
            Context = context;
            Result = result;
            Key = key ?? string.Empty;
            BytesLength = bytesLength;
        }

        public DateTime UtcTime => new DateTime(UtcTicks, DateTimeKind.Utc);
    }

    public readonly struct SaveScopeRegistrationInfo
    {
        public readonly ScopeKey ScopeKey;
        public readonly int PlanSourceVersion;
        public readonly bool HasBlackboard;
        public readonly bool HasScalars;
        public readonly string PlanSourceType;

        public SaveScopeRegistrationInfo(
            ScopeKey scopeKey,
            int planSourceVersion,
            bool hasBlackboard,
            bool hasScalars,
            string planSourceType)
        {
            ScopeKey = scopeKey;
            PlanSourceVersion = planSourceVersion;
            HasBlackboard = hasBlackboard;
            HasScalars = hasScalars;
            PlanSourceType = planSourceType ?? string.Empty;
        }
    }

    /// <summary>
    /// Debug-only read API for SaveManager v2.
    /// - Does not mutate save state.
    /// - Intended for Inspector debug views.
    /// </summary>
    public interface ISaveManagerDebug
    {
        int PlanCacheCount { get; }

        void GetRegistrations(List<SaveScopeRegistrationInfo> results);
        void GetRecentOperations(List<SaveOperationRecord> results);
    }
}
