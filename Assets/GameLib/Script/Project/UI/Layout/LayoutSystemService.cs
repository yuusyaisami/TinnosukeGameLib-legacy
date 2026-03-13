using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;
using Game.TransformSystem;

namespace Game.Layout
{
    public interface ILayoutSystemService
    {
        void MarkMembershipDirty();
        void MarkContentDirty();
        void RebuildNow();
        bool TryGetLastOutput(out LayoutOutput output);
    }

    public sealed class LayoutSystemService : ILayoutSystemService, ITickable, ILateTickable, ITickPhase, IScopeAcquireHandler, IScopeReleaseHandler
    {
        struct ContributorEntry
        {
            public RectTransform Target;
            public IRectTransformSizeAdapter SizeAdapter;
            public ITextLayoutAdapter TextAdapter;
            public LayoutFreezeBounds Freeze;
            public LayoutIgnore Ignore;
            public MaxWidthOverride MaxWidthOverride;
            public BoundsModeOverride BoundsModeOverride;
        }

        readonly LayoutSystemConfig _config;
        readonly List<ContributorEntry> _contributors = new(128);
        readonly List<RectTransform> _collectBuffer = new(128);
        readonly List<ITextLayoutAdapter> _subscribedTextAdapters = new(16);
        readonly Vector3[] _worldCorners = new Vector3[4];

        bool _membershipDirty = true;
        bool _contentDirty = true;
        bool _acquired;
        bool _disposed;
        bool _hasOutput;
        LayoutOutput _lastOutput;

        bool _warnedMissingRoot;
        bool _warnedMissingAdapter;
        bool _warnedGlyphMode;
        bool _warnedAnchorMismatch;

        public LayoutSystemService(LayoutSystemConfig config)
        {
            _config = config;
        }

        public TickPhase Phase => _config != null && _config.RunInLateUpdate ? TickPhase.Late : TickPhase.Default;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (_disposed)
                return;

            _acquired = true;
            MarkMembershipDirty();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (_disposed)
                return;

            _acquired = false;
            UnsubscribeTextAdapters();
        }

        public void MarkMembershipDirty()
        {
            _membershipDirty = true;
            _contentDirty = true;
        }

        public void MarkContentDirty()
        {
            _contentDirty = true;
        }

        public void RebuildNow()
        {
            Rebuild(force: true);
        }

        public bool TryGetLastOutput(out LayoutOutput output)
        {
            output = _lastOutput;
            return _hasOutput;
        }

        public void Tick()
        {
            if (!_acquired || _disposed)
                return;

            TickInternal();
        }

        public void LateTick()
        {
            if (!_acquired || _disposed)
                return;

            TickInternal();
        }

        void TickInternal()
        {
            if (!_membershipDirty && !_contentDirty)
                return;

            Rebuild(force: false);
        }

        void Rebuild(bool force)
        {
            if (_disposed)
                return;

            if (_config == null || _config.LayoutElementsRoot == null)
            {
                WarnMissingRoot();
                return;
            }

            if (_membershipDirty)
                CollectContributors();

            if (_config.ForceUnityLayoutRebuildOnRebuild)
                ForceUnityLayout();

            bool hasContributors = CalculateBounds(out var localRect);
            var adjusted = hasContributors
                ? ApplyBackgroundOptions(localRect, _config.BackgroundOptions)
                : Rect.zero;

            _lastOutput = new LayoutOutput(hasContributors ? localRect : Rect.zero);
            _hasOutput = true;

            ApplyBackground(adjusted, hasContributors);

            _membershipDirty = false;
            _contentDirty = false;
        }

        void CollectContributors()
        {
            _contributors.Clear();
            UnsubscribeTextAdapters();
            _collectBuffer.Clear();

            var root = _config.LayoutElementsRoot;
            if (root == null)
                return;

            bool includeInactive = !_config.ExcludeInactive;
            root.GetComponentsInChildren(includeInactive, _collectBuffer);

            var background = _config.BackgroundRect;

            for (int i = 0; i < _collectBuffer.Count; i++)
            {
                var rt = _collectBuffer[i];
                if (rt == null || rt == root)
                    continue;

                // Exclude the background rect itself and all its descendants
                if (background != null)
                {
                    if (rt == background || rt.IsChildOf(background))
                        continue;
                }

                if (_config.ExcludeInactive && !rt.gameObject.activeInHierarchy)
                    continue;

                var textAdapter = rt.GetComponent<ITextLayoutAdapter>();
                var sizeAdapter = textAdapter != null
                    ? (IRectTransformSizeAdapter)textAdapter
                    : rt.GetComponent<IRectTransformSizeAdapter>();

                var entry = new ContributorEntry
                {
                    Target = rt,
                    SizeAdapter = sizeAdapter,
                    TextAdapter = textAdapter,
                    Freeze = rt.GetComponent<LayoutFreezeBounds>(),
                    Ignore = rt.GetComponent<LayoutIgnore>(),
                    MaxWidthOverride = rt.GetComponent<MaxWidthOverride>(),
                    BoundsModeOverride = rt.GetComponent<BoundsModeOverride>(),
                };

                _contributors.Add(entry);

                if (textAdapter != null)
                    SubscribeTextAdapter(textAdapter);
            }

            _membershipDirty = false;
        }

        bool CalculateBounds(out Rect rect)
        {
            rect = Rect.zero;

            var root = _config.LayoutElementsRoot;
            if (root == null)
                return false;

            bool hasAny = false;
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < _contributors.Count; i++)
            {
                var entry = _contributors[i];
                if (entry.Target == null)
                {
                    _membershipDirty = true;
                    continue;
                }

                if (_config.ExcludeInactive && !entry.Target.gameObject.activeInHierarchy)
                    continue;

                if (entry.Ignore != null && entry.Ignore.isActiveAndEnabled)
                    continue;

                if (!TryGetEntryRect(entry, root, out var entryRect))
                    continue;

                if (!hasAny)
                {
                    min = entryRect.min;
                    max = entryRect.max;
                    hasAny = true;
                    continue;
                }

                min = Vector2.Min(min, entryRect.min);
                max = Vector2.Max(max, entryRect.max);
            }

            if (!hasAny)
                return false;

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        bool TryGetEntryRect(in ContributorEntry entry, RectTransform root, out Rect rect)
        {
            rect = Rect.zero;

            if (entry.Target == null)
                return false;

            if (entry.Freeze != null && entry.Freeze.isActiveAndEnabled && entry.Freeze.TryGetFrozenRect(out rect))
                return true;

            var mode = ResolveBoundsMode(entry);
            switch (mode)
            {
                case BoundsMode.PreferredLayout:
                    rect = ResolvePreferredRect(entry, root);
                    break;
                case BoundsMode.GlyphBounds:
                    WarnGlyphMode();
                    rect = ResolveRectTransformRect(entry.Target, root);
                    break;
                default:
                    rect = ResolveRectTransformRect(entry.Target, root);
                    break;
            }

            if (entry.Freeze != null && entry.Freeze.isActiveAndEnabled)
                entry.Freeze.SetFrozenRect(rect);

            return true;
        }

        Rect ResolvePreferredRect(in ContributorEntry entry, RectTransform root)
        {
            if (entry.SizeAdapter == null)
            {
                WarnMissingAdapter();
                return ResolveRectTransformRect(entry.Target, root);
            }

            float maxWidth = ResolveMaxWidth(entry);
            var size = entry.SizeAdapter.GetPreferredSize(maxWidth);
            var target = entry.Target;
            var pivot = target.pivot;

            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            var bl = new Vector3(-pivot.x * size.x, -pivot.y * size.y, 0f);
            var br = new Vector3(bl.x + size.x, bl.y, 0f);
            var tr = new Vector3(bl.x + size.x, bl.y + size.y, 0f);
            var tl = new Vector3(bl.x, bl.y + size.y, 0f);

            ExpandFromLocalCorner(target, root, bl, ref min, ref max);
            ExpandFromLocalCorner(target, root, br, ref min, ref max);
            ExpandFromLocalCorner(target, root, tr, ref min, ref max);
            ExpandFromLocalCorner(target, root, tl, ref min, ref max);

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        Rect ResolveRectTransformRect(RectTransform target, RectTransform root)
        {
            if (target == null || root == null)
                return Rect.zero;

            target.GetWorldCorners(_worldCorners);

            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < 4; i++)
            {
                var local = (Vector2)root.InverseTransformPoint(_worldCorners[i]);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static void ExpandFromLocalCorner(RectTransform target, RectTransform root, Vector3 localCorner, ref Vector2 min, ref Vector2 max)
        {
            var world = target.TransformPoint(localCorner);
            var rootLocal = (Vector2)root.InverseTransformPoint(world);
            min = Vector2.Min(min, rootLocal);
            max = Vector2.Max(max, rootLocal);
        }

        BoundsMode ResolveBoundsMode(in ContributorEntry entry)
        {
            if (entry.BoundsModeOverride != null && entry.BoundsModeOverride.isActiveAndEnabled)
                return entry.BoundsModeOverride.Mode;

            if (entry.TextAdapter != null || entry.SizeAdapter != null)
                return BoundsMode.PreferredLayout;

            return BoundsMode.RectTransform;
        }

        float ResolveMaxWidth(in ContributorEntry entry)
        {
            float maxWidth;
            if (entry.MaxWidthOverride != null && entry.MaxWidthOverride.isActiveAndEnabled)
            {
                maxWidth = entry.MaxWidthOverride.MaxWidth;
            }
            else
            {
                maxWidth = entry.Target != null ? entry.Target.rect.width : 0f;
            }

            if (maxWidth <= 0f)
                maxWidth = Mathf.Infinity;

            return maxWidth;
        }

        static Rect ApplyBackgroundOptions(Rect localRect, LayoutBackgroundOptions options)
        {
            var min = localRect.min;
            var max = localRect.max;

            min.x -= options.ExtendLeft;
            max.x += options.ExtendRight;
            min.y -= options.ExtendBottom;
            max.y += options.ExtendTop;

            var size = new Vector2(max.x - min.x, max.y - min.y);
            var center = (min + max) * 0.5f;

            if (options.MinSize.x > size.x) size.x = options.MinSize.x;
            if (options.MinSize.y > size.y) size.y = options.MinSize.y;

            center += options.Offset;
            return new Rect(center - size * 0.5f, size);
        }

        void ApplyBackground(Rect localRect, bool hasContributors)
        {
            var background = _config.BackgroundRect;
            if (background == null)
                return;

            if (!hasContributors && _config.HideBackgroundWhenEmpty)
            {
                if (background.gameObject.activeSelf)
                    background.gameObject.SetActive(false);
                return;
            }

            if (hasContributors && !background.gameObject.activeSelf)
                background.gameObject.SetActive(true);

            var bgParent = background.parent as RectTransform;
            if (bgParent == null)
                return;

            if (background.anchorMin != background.anchorMax)
            {
                WarnAnchorMismatch();
                return;
            }

            var worldCenter = _config.LayoutElementsRoot.TransformPoint(localRect.center);
            var bgLocalCenter = (Vector2)bgParent.InverseTransformPoint(worldCenter);

            var anchor = background.anchorMin;
            var parentSize = bgParent.rect.size;
            var anchorRefLocal = new Vector2(
                (anchor.x - bgParent.pivot.x) * parentSize.x,
                (anchor.y - bgParent.pivot.y) * parentSize.y);

            background.anchoredPosition = bgLocalCenter - anchorRefLocal;
            background.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, localRect.width);
            background.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, localRect.height);
            Debug.Log($"[LayoutSystem] Background applied at {background.anchoredPosition} with size {localRect.size}.");
        }

        void ForceUnityLayout()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_config.LayoutElementsRoot);
        }

        void SubscribeTextAdapter(ITextLayoutAdapter adapter)
        {
            if (adapter == null)
                return;

            for (int i = 0; i < _subscribedTextAdapters.Count; i++)
            {
                if (_subscribedTextAdapters[i] == adapter)
                    return;
            }

            _subscribedTextAdapters.Add(adapter);
            adapter.OnLayoutContentChanged += HandleLayoutContentChanged;
        }

        void UnsubscribeTextAdapters()
        {
            for (int i = 0; i < _subscribedTextAdapters.Count; i++)
            {
                var adapter = _subscribedTextAdapters[i];
                if (adapter != null)
                    adapter.OnLayoutContentChanged -= HandleLayoutContentChanged;
            }

            _subscribedTextAdapters.Clear();
        }

        void HandleLayoutContentChanged()
        {
            _contentDirty = true;
        }

        void WarnMissingRoot()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_warnedMissingRoot)
                return;
            _warnedMissingRoot = true;
            Debug.LogError("[LayoutSystem] LayoutElementsRoot is missing. Rebuild skipped.");
#endif
        }

        void WarnMissingAdapter()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_warnedMissingAdapter)
                return;
            _warnedMissingAdapter = true;
            Debug.LogWarning("[LayoutSystem] PreferredLayout requested but no size adapter found. Falling back to RectTransform.");
#endif
        }

        void WarnGlyphMode()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_warnedGlyphMode)
                return;
            _warnedGlyphMode = true;
            Debug.LogWarning("[LayoutSystem] GlyphBounds mode is not implemented. Falling back to RectTransform.");
#endif
        }

        void WarnAnchorMismatch()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_warnedAnchorMismatch)
                return;
            _warnedAnchorMismatch = true;
            Debug.LogError("[LayoutSystem] BackgroundRect anchorMin must match anchorMax. Background update skipped.");
#endif
        }
    }
}
