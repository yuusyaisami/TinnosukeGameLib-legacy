#nullable enable
using Game;
using UnityEngine;

namespace Game.Background
{
    public sealed class BackgroundElementAdapterService :
        IBackgroundElementAdapter,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IBackgroundElementAdapterOptions _options;
        RectTransform? _rectTransform;
        SpriteRenderer? _spriteRenderer;

        public BackgroundElementAdapterService(IBackgroundElementAdapterOptions options)
        {
            _options = options;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _rectTransform = _options.RectTransform;
            _spriteRenderer = _options.SpriteRenderer;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _rectTransform = null;
            _spriteRenderer = null;
        }

        public void Initialize(in BackgroundElementContext context)
        {
            Apply(context);
        }

        public void Apply(in BackgroundElementContext context)
        {
            var size = context.TileSize + context.TilePadding;

            if (_options.ApplyRectTransformSize && _rectTransform != null)
            {
                _rectTransform.sizeDelta = size;
            }

            if (_options.ApplySpriteRendererSize && _spriteRenderer != null)
            {
                _spriteRenderer.size = size;
            }

            if (_options.ApplySortingOrder && _spriteRenderer != null)
            {
                _spriteRenderer.sortingOrder = context.SortingOrder + _options.SortingOrderOffset;
            }
        }
    }
}
