#nullable enable
using System.Collections.Generic;
using Game;

namespace Game.Collision
{
    public interface IHitColliderScopeRegistry
    {
        void Register(DynamicColliderHandle handle, IScopeNode? scope);
        void Unregister(DynamicColliderHandle handle, IScopeNode? scope);
        bool TryResolve(DynamicColliderHandle handle, out IScopeNode? scope);
    }

    /// <summary>
    /// DynamicColliderHandle -> IScopeNode の解決用レジストリ。
    /// - Generation を保持し、再利用Idの取り違えを防ぐ
    /// - 例外は投げない（best-effort）
    /// </summary>
    public sealed class HitColliderScopeRegistry : IHitColliderScopeRegistry
    {
        struct Entry
        {
            public int Generation;
            public IScopeNode Scope;
        }

        readonly Dictionary<int, Entry> _byId = new();

        public void Register(DynamicColliderHandle handle, IScopeNode? scope)
        {
            if (!handle.IsValid || scope == null)
                return;

            _byId[handle.Id] = new Entry
            {
                Generation = handle.Generation,
                Scope = scope,
            };
        }

        public void Unregister(DynamicColliderHandle handle, IScopeNode? scope)
        {
            if (!handle.IsValid || scope == null)
                return;

            if (_byId.TryGetValue(handle.Id, out var e))
            {
                if (e.Generation != handle.Generation)
                    return;
                if (!ReferenceEquals(e.Scope, scope))
                    return;

                _byId.Remove(handle.Id);
            }
        }

        public bool TryResolve(DynamicColliderHandle handle, out IScopeNode? scope)
        {
            scope = null;

            if (!handle.IsValid)
                return false;

            if (!_byId.TryGetValue(handle.Id, out var e))
                return false;

            if (e.Generation != handle.Generation)
                return false;

            scope = e.Scope;
            return scope != null;
        }
    }
}
