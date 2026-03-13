#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Common
{
    /// <summary>
    /// 何も保持しない IVarStore。
    /// vNext 対応のために Vars を非 null で返したいケースで使う。
    /// </summary>
    public sealed class NullVarStore : IVarStore
    {
        public static readonly NullVarStore Instance = new();

        NullVarStore() { }

        public int GlobalVersion => 0;
        public event Action<int>? OnVarChanged { add { } remove { } }

        public IEnumerable<int> EnumerateVarIds() => Array.Empty<int>();
        public bool Contains(int varId) => false;
        public int GetVarVersion(int varId) => 0;
        public ValueKind GetVarKind(int varId) => ValueKind.Null;
        public bool TrySetVariant(int varId, in DynamicVariant value) => false;
        public bool TryGetVariant(int varId, out DynamicVariant value) { value = default; return false; }
        public bool TrySetManagedRef(int varId, object value) => false;
        public bool TryGetManagedRef(int varId, out object value) { value = null!; return false; }
        public bool TryUnset(int varId) => false;
    }
}

