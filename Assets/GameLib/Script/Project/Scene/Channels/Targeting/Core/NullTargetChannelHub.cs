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

        public ITargetChannelRuntime GetOrRegister(TargetChannelDef def, bool replaceIfExists = false)
        {
            throw new InvalidOperationException("Targeting is disabled (IDynamicSearchService not available).");
        }

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
