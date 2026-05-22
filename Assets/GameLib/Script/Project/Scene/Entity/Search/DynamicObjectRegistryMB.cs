#nullable enable
using System.Collections.Generic;
using Game.Search;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Entity.Search
{
    [DisallowMultipleComponent]
    public sealed class DynamicObjectRegistryMB : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] bool enableDebugView = true;

        [Min(0f)]
        [SerializeField] float debugRefreshIntervalSeconds = 0.25f;

        [SerializeField] List<DynamicObjectDebugEntry> debugEntries = new();

        public void InstallDynamicObjectRegistryRuntime(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            _ = owner ?? throw new System.ArgumentNullException(nameof(owner));

            builder.RegisterComponent(this);

            builder.Register<DynamicObjectRegistryService>(RuntimeLifetime.Singleton)
                .As<IDynamicObjectRegistryService>()
                .As<IDynamicSearchService>()
                .As<IDynamicObjectDebugSource>();

            if (enableDebugView)
            {
                builder.Register<DynamicObjectRegistryDebugUpdater>(RuntimeLifetime.Singleton)
                    .WithParameter(this)
                    .WithParameter(debugRefreshIntervalSeconds)
                    .As<IScopeTickHandler>();
            }
        }

        sealed class DynamicObjectRegistryDebugUpdater : IScopeTickHandler
        {
            readonly DynamicObjectRegistryMB _mb;
            readonly IDynamicObjectDebugSource _source;
            readonly float _intervalSeconds;
            float _nextTime;

            public DynamicObjectRegistryDebugUpdater(
                DynamicObjectRegistryMB mb,
                IDynamicObjectDebugSource source,
                float intervalSeconds)
            {
                _mb = mb;
                _source = source;
                _intervalSeconds = intervalSeconds;
            }

            public void Tick()
            {
                if (_mb == null || !_mb)
                    return;

                if (_intervalSeconds > 0f)
                {
                    float now = Time.unscaledTime;
                    if (now < _nextTime)
                        return;
                    _nextTime = now + _intervalSeconds;
                }

                _source.CopyDebugEntries(_mb.debugEntries);
            }
        }
    }
}
