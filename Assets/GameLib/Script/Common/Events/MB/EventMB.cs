using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Sirenix.OdinInspector;
#nullable enable

namespace Game.Common
{
    /// <summary>LifetimeScopeKind ごとに EventService を登録する MB。</summary>
    public sealed class EventMB : MonoBehaviour, IFeatureInstaller
    {

        [Header("Event Logging")]
        [Tooltip("Capture published events for this scope and show the entries here at runtime.")]
        [SerializeField]
        bool _captureEventLog = true;

        [Tooltip("Maximum number of entries to keep in the log (circular buffer).")]
        [SerializeField, Min(1)]
        int _logCapacity = 64;

        EventLogStore? _eventLogStore;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            var logStore = new EventLogStore(_logCapacity);
            logStore.Enabled = _captureEventLog;
            _eventLogStore = logStore;
            builder.RegisterInstance(logStore)
                .As<IEventLogStore>()
                .As<EventLogStore>();

            LifetimeScopeKind kind = scope.Kind;
            switch (kind)
            {
                case LifetimeScopeKind.Project:
                    // Register legacy (async) EventService as IEventService for compatibility
                    builder.Register<IEventService, EventService>(Lifetime.Singleton)
                        .As<IProjectEventService>();

                    // Register the typed-only SyncEventBus for high-performance events (Collision, etc.)
                    // MaxEventIdExclusive should match generated event IDs range
                    builder.Register<ISyncEventBus>(c => new SyncEventBus(ProjectEventIds.MaxEventIdExclusive), Lifetime.Singleton)
                        .As<ISyncEventBus>();
                    break;
                case LifetimeScopeKind.Platform:
                    builder.Register<IEventService, EventService>(Lifetime.Singleton)
                           .As<IPlatformEventService>();
                    break;
                case LifetimeScopeKind.Global:
                    builder.Register<IEventService, EventService>(Lifetime.Singleton)
                           .As<IGlobalEventService>();
                    break;
                case LifetimeScopeKind.Scene:
                    builder.Register<IEventService, EventService>(Lifetime.Singleton)
                           .As<ISceneEventService>();
                    break;
                case LifetimeScopeKind.Field:
                    builder.Register<IEventService, EventService>(Lifetime.Singleton)
                           .As<IFieldEventService>();
                    break;
                case LifetimeScopeKind.Entity:
                    builder.Register<IEventService, EventService>(Lifetime.Singleton)
                           .As<IEntityEventService>();
                    break;
                case LifetimeScopeKind.UI:
                    builder.Register<IEventService, EventService>(Lifetime.Singleton)
                           .As<IUIEventService>();
                    break;
                case LifetimeScopeKind.UIElement:
                    builder.Register<IEventService, EventService>(Lifetime.Singleton)
                           .As<IUIElementEventService>();
                    break;
                case LifetimeScopeKind.Runtime:
                    builder.Register<IEventService, EventService>(Lifetime.Singleton);
                    break;
                default:
                    Debug.LogWarning($"[EventMB] Unhandled LifetimeScopeKind: {kind}");
                    break;
            }
        }

        bool IsPlayMode => Application.isPlaying;

        [ShowInInspector, ShowIf(nameof(IsPlayMode))]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, HideAddButton = true, HideRemoveButton = true, ShowPaging = false, DraggableItems = false)]
        public IReadOnlyList<EventLogEntryView> EventLogEntries
        {
            get
            {
                if (_eventLogStore == null)
                    return Array.Empty<EventLogEntryView>();

                var raw = _eventLogStore.Entries;
                var snapshot = new EventLogEntryView[raw.Count];
                for (int i = 0; i < raw.Count; i++)
                {
                    snapshot[i] = new EventLogEntryView(raw[i]);
                }
                return snapshot;
            }
        }

        [ShowInInspector, PropertyOrder(99)]
        public int EventLogCapacity => _eventLogStore?.Capacity ?? _logCapacity;

        [ShowInInspector, PropertyOrder(100)]
        public int EventLogCount => _eventLogStore?.Count ?? 0;

        [Button("Clear Event Log", ButtonSizes.Small)]
        [ShowIf(nameof(IsPlayMode))]
        void ClearEventLog()
        {
            _eventLogStore?.Clear();
        }

        [Serializable]
        public sealed class EventLogEntryView
        {
            public EventLogEntryView(EventLogEntry entry)
            {
                Timestamp = entry.Timestamp;
                Key = entry.Key;
                PayloadSummary = entry.PayloadSummary;
            }

            [ShowInInspector, PropertyOrder(0)]
            public string Timestamp { get; }

            [ShowInInspector, PropertyOrder(1)]
            public string Key { get; }

            [ShowInInspector, PropertyOrder(2)]
            [TextArea(2, 4)]
            public string PayloadSummary { get; }
        }
    }
}
