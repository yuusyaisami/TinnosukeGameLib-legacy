#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Search
{
    [Serializable]
    public struct DynamicObjectDebugEntry
    {
        public LifetimeScopeKind Kind;
        public string Id;
        public string Category;
        public UnityEngine.Object? Object;

        public DynamicObjectDebugEntry(LifetimeScopeKind kind, string id, string category, UnityEngine.Object? obj)
        {
            Kind = kind;
            Id = id ?? "";
            Category = category ?? "";
            Object = obj;
        }
    }

    public interface IDynamicObjectDebugSource
    {
        void CopyDebugEntries(List<DynamicObjectDebugEntry> destination);
    }
}
