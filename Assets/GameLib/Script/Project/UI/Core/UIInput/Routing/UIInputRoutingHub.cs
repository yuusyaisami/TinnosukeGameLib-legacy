#nullable enable
using System;
using System.Collections.Generic;

namespace Game.UI
{
    public interface IUIInputPreviewObserver
    {
        int Priority { get; }
        void Observe(in UIInputEvent inputEvent);
    }

    public interface IUIInputBubbleConsumer
    {
        int Priority { get; }
        bool Consume(in UIInputEvent inputEvent);
    }

    public interface IUIInputRoutingHub
    {
        void RegisterPreview(IScopeNode owner, IUIInputPreviewObserver observer);
        void UnregisterPreview(IScopeNode owner, IUIInputPreviewObserver observer);
        void RegisterBubble(IScopeNode owner, IUIInputBubbleConsumer consumer);
        void UnregisterBubble(IScopeNode owner, IUIInputBubbleConsumer consumer);
        void NotifyPreview(IScopeNode? currentElement, IScopeNode? hoveredElement, in UIInputEvent inputEvent);
        bool DispatchBubble(IScopeNode? currentElement, IScopeNode? hoveredElement, in UIInputEvent inputEvent);
    }

    public sealed class UIInputRoutingHub : IUIInputRoutingHub
    {
        readonly Dictionary<IScopeNode, List<IUIInputPreviewObserver>> _previewEntriesByOwner =
            new(global::Game.ReferenceEqualityComparer<IScopeNode>.Instance);
        readonly Dictionary<IScopeNode, List<IUIInputBubbleConsumer>> _bubbleEntriesByOwner =
            new(global::Game.ReferenceEqualityComparer<IScopeNode>.Instance);
        readonly HashSet<IUIInputPreviewObserver> _previewDispatchDedup =
            new(global::Game.ReferenceEqualityComparer<IUIInputPreviewObserver>.Instance);
        readonly HashSet<IUIInputBubbleConsumer> _bubbleDispatchDedup =
            new(global::Game.ReferenceEqualityComparer<IUIInputBubbleConsumer>.Instance);

        public void RegisterPreview(IScopeNode owner, IUIInputPreviewObserver observer)
        {
            if (owner == null || observer == null)
                return;

            var entries = GetOrCreatePreviewEntries(owner);
            InsertPreview(entries, observer);
        }

        public void UnregisterPreview(IScopeNode owner, IUIInputPreviewObserver observer)
        {
            if (owner == null || observer == null)
                return;

            RemovePreview(owner, observer);
        }

        public void RegisterBubble(IScopeNode owner, IUIInputBubbleConsumer consumer)
        {
            if (owner == null || consumer == null)
                return;

            var entries = GetOrCreateBubbleEntries(owner);
            InsertBubble(entries, consumer);
        }

        public void UnregisterBubble(IScopeNode owner, IUIInputBubbleConsumer consumer)
        {
            if (owner == null || consumer == null)
                return;

            RemoveBubble(owner, consumer);
        }

        public void NotifyPreview(IScopeNode? currentElement, IScopeNode? hoveredElement, in UIInputEvent inputEvent)
        {
            _previewDispatchDedup.Clear();
            DispatchPreviewPath(currentElement, in inputEvent);
            DispatchPreviewPath(hoveredElement, in inputEvent);
        }

        public bool DispatchBubble(IScopeNode? currentElement, IScopeNode? hoveredElement, in UIInputEvent inputEvent)
        {
            _bubbleDispatchDedup.Clear();
            return DispatchBubblePath(currentElement, in inputEvent) || DispatchBubblePath(hoveredElement, in inputEvent);
        }

        List<IUIInputPreviewObserver> GetOrCreatePreviewEntries(IScopeNode owner)
        {
            if (_previewEntriesByOwner.TryGetValue(owner, out var entries))
                return entries;

            entries = new List<IUIInputPreviewObserver>();
            _previewEntriesByOwner.Add(owner, entries);
            return entries;
        }

        List<IUIInputBubbleConsumer> GetOrCreateBubbleEntries(IScopeNode owner)
        {
            if (_bubbleEntriesByOwner.TryGetValue(owner, out var entries))
                return entries;

            entries = new List<IUIInputBubbleConsumer>();
            _bubbleEntriesByOwner.Add(owner, entries);
            return entries;
        }

        static void InsertPreview(List<IUIInputPreviewObserver> entries, IUIInputPreviewObserver observer)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (ReferenceEquals(entries[i], observer))
                    return;
            }

            entries.Add(observer);
            entries.Sort(static (x, y) => y.Priority.CompareTo(x.Priority));
        }

        static void InsertBubble(List<IUIInputBubbleConsumer> entries, IUIInputBubbleConsumer consumer)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (ReferenceEquals(entries[i], consumer))
                    return;
            }

            entries.Add(consumer);
            entries.Sort(static (x, y) => y.Priority.CompareTo(x.Priority));
        }

        void RemovePreview(IScopeNode owner, IUIInputPreviewObserver observer)
        {
            if (!_previewEntriesByOwner.TryGetValue(owner, out var entries))
                return;

            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(entries[i], observer))
                    entries.RemoveAt(i);
            }

            if (entries.Count == 0)
                _previewEntriesByOwner.Remove(owner);
        }

        void RemoveBubble(IScopeNode owner, IUIInputBubbleConsumer consumer)
        {
            if (!_bubbleEntriesByOwner.TryGetValue(owner, out var entries))
                return;

            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(entries[i], consumer))
                    entries.RemoveAt(i);
            }

            if (entries.Count == 0)
                _bubbleEntriesByOwner.Remove(owner);
        }

        void DispatchPreviewPath(IScopeNode? start, in UIInputEvent inputEvent)
        {
            for (var current = start; current != null; current = current.Parent)
            {
                if (!_previewEntriesByOwner.TryGetValue(current, out var entries))
                    continue;

                for (var i = 0; i < entries.Count; i++)
                {
                    var observer = entries[i];
                    if (!_previewDispatchDedup.Add(observer))
                        continue;

                    observer.Observe(in inputEvent);
                }
            }
        }

        bool DispatchBubblePath(IScopeNode? start, in UIInputEvent inputEvent)
        {
            for (var current = start; current != null; current = current.Parent)
            {
                if (!_bubbleEntriesByOwner.TryGetValue(current, out var entries))
                    continue;

                for (var i = 0; i < entries.Count; i++)
                {
                    var consumer = entries[i];
                    if (!_bubbleDispatchDedup.Add(consumer))
                        continue;

                    if (consumer.Consume(in inputEvent))
                        return true;
                }
            }

            return false;
        }
    }
}
