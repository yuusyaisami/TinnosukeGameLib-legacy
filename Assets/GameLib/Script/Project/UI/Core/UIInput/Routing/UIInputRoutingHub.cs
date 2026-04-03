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
        readonly struct PreviewEntry
        {
            public PreviewEntry(IScopeNode owner, IUIInputPreviewObserver observer)
            {
                Owner = owner;
                Observer = observer;
            }

            public IScopeNode Owner { get; }
            public IUIInputPreviewObserver Observer { get; }
        }

        readonly struct BubbleEntry
        {
            public BubbleEntry(IScopeNode owner, IUIInputBubbleConsumer consumer)
            {
                Owner = owner;
                Consumer = consumer;
            }

            public IScopeNode Owner { get; }
            public IUIInputBubbleConsumer Consumer { get; }
        }

        readonly List<PreviewEntry> _previewEntries = new();
        readonly List<BubbleEntry> _bubbleEntries = new();
        bool _previewSortDirty;
        bool _bubbleSortDirty;

        public void RegisterPreview(IScopeNode owner, IUIInputPreviewObserver observer)
        {
            if (owner == null || observer == null)
                return;

            for (var i = 0; i < _previewEntries.Count; i++)
            {
                var entry = _previewEntries[i];
                if (ReferenceEquals(entry.Owner, owner) && ReferenceEquals(entry.Observer, observer))
                    return;
            }

            _previewEntries.Add(new PreviewEntry(owner, observer));
            _previewSortDirty = true;
        }

        public void UnregisterPreview(IScopeNode owner, IUIInputPreviewObserver observer)
        {
            if (owner == null || observer == null)
                return;

            for (var i = _previewEntries.Count - 1; i >= 0; i--)
            {
                var entry = _previewEntries[i];
                if (ReferenceEquals(entry.Owner, owner) && ReferenceEquals(entry.Observer, observer))
                    _previewEntries.RemoveAt(i);
            }
        }

        public void RegisterBubble(IScopeNode owner, IUIInputBubbleConsumer consumer)
        {
            if (owner == null || consumer == null)
                return;

            for (var i = 0; i < _bubbleEntries.Count; i++)
            {
                var entry = _bubbleEntries[i];
                if (ReferenceEquals(entry.Owner, owner) && ReferenceEquals(entry.Consumer, consumer))
                    return;
            }

            _bubbleEntries.Add(new BubbleEntry(owner, consumer));
            _bubbleSortDirty = true;
        }

        public void UnregisterBubble(IScopeNode owner, IUIInputBubbleConsumer consumer)
        {
            if (owner == null || consumer == null)
                return;

            for (var i = _bubbleEntries.Count - 1; i >= 0; i--)
            {
                var entry = _bubbleEntries[i];
                if (ReferenceEquals(entry.Owner, owner) && ReferenceEquals(entry.Consumer, consumer))
                    _bubbleEntries.RemoveAt(i);
            }
        }

        public void NotifyPreview(IScopeNode? currentElement, IScopeNode? hoveredElement, in UIInputEvent inputEvent)
        {
            _ = currentElement;
            _ = hoveredElement;
            EnsurePreviewSorted();
            for (var i = 0; i < _previewEntries.Count; i++)
                _previewEntries[i].Observer.Observe(in inputEvent);
        }

        public bool DispatchBubble(IScopeNode? currentElement, IScopeNode? hoveredElement, in UIInputEvent inputEvent)
        {
            _ = currentElement;
            _ = hoveredElement;
            EnsureBubbleSorted();
            for (var i = 0; i < _bubbleEntries.Count; i++)
            {
                if (_bubbleEntries[i].Consumer.Consume(in inputEvent))
                    return true;
            }

            return false;
        }

        void EnsurePreviewSorted()
        {
            if (!_previewSortDirty)
                return;

            _previewEntries.Sort(static (x, y) =>
            {
                var depthCompare = GetScopeDepth(y.Owner).CompareTo(GetScopeDepth(x.Owner));
                if (depthCompare != 0)
                    return depthCompare;

                return y.Observer.Priority.CompareTo(x.Observer.Priority);
            });
            _previewSortDirty = false;
        }

        void EnsureBubbleSorted()
        {
            if (!_bubbleSortDirty)
                return;

            _bubbleEntries.Sort(static (x, y) =>
            {
                var depthCompare = GetScopeDepth(y.Owner).CompareTo(GetScopeDepth(x.Owner));
                if (depthCompare != 0)
                    return depthCompare;

                return y.Consumer.Priority.CompareTo(x.Consumer.Priority);
            });
            _bubbleSortDirty = false;
        }

        static int GetScopeDepth(IScopeNode scope)
        {
            var depth = 0;
            for (var current = scope; current != null; current = current.Parent)
                depth++;
            return depth;
        }
    }
}
