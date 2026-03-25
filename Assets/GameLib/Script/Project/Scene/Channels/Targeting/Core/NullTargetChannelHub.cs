#nullable enable
using System;
using Game.DI;

namespace Game.Targeting
{
    public sealed class NullTargetChannelHub : ITargetChannelHub, IDisposable, IResettableService, IEnabledService
    {
        bool _enabled = true;

        public int ChannelCount => 0;
        public bool IsEnabled => _enabled;

        public bool TryGetRuntime(string tag, out ITargetChannelRuntime runtime)
        {
            runtime = null!;
            return false;
        }

        public ITargetChannelRuntime RegisterOrReplace(TargetChannelPreset preset)
        {
            throw new InvalidOperationException("Targeting is disabled (IDynamicSearchService not available).");
        }

        public bool SwapPreset(string tag, TargetChannelPreset preset) => false;
        public bool MutateSettings(string tag, TargetChannelRuntimeMutation mutation) => false;
        public bool ResetRuntimeOverrides(string tag) => false;
        public bool SetDirectTargets(string tag, System.Collections.Generic.IReadOnlyList<Search.DynamicSearchHit> hits) => false;
        public bool ClearDirectTargets(string tag) => false;

        public bool Unregister(string tag) => false;
        public void Clear() { }

        public void Dispose()
        {
        }

        public void Reset()
        {
            _enabled = true;
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }
    }
}
