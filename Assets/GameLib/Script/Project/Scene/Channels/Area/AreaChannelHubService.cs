#nullable enable
using System;
using System.Collections.Generic;
using Game;
using Game.Common;
using UnityEngine;

namespace Game.Channel
{
    public sealed class AreaChannelHubService : IAreaChannelHubService, IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly IScopeNode _owner;
        readonly Dictionary<string, AreaChannelDefinition> _defsByTag = new(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, AreaChannelRuntimePlayer> _runtimeByTag = new(StringComparer.OrdinalIgnoreCase);
        readonly List<ChannelDefBase> _defsSnapshot = new();
        readonly HashSet<string> _missingBasePositionLoggedTags = new(StringComparer.OrdinalIgnoreCase);
        bool _defsDirty = true;

        IScopeNode? _ownerScope;
        IDynamicContext _dynamicContext = EmptyDynamicContext.Instance;

        public IReadOnlyList<ChannelDefBase> ChannelDefs
        {
            get
            {
                if (_defsDirty)
                {
                    _defsSnapshot.Clear();
                    foreach (var item in _defsByTag.Values)
                        _defsSnapshot.Add(item);
                    _defsDirty = false;
                }

                return _defsSnapshot;
            }
        }

        public AreaChannelHubService(IScopeNode owner, AreaChannelDefinition[] definitions)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));

            if (definitions == null)
                return;

            for (int i = 0; i < definitions.Length; i++)
                RegisterChannelInternal(definitions[i], overwrite: false);
        }

        public bool TrySamplePosition(string tag, in AreaSampleRequest request, out Vector3 position)
        {
            position = default;

            if (!TryGetPlayer(tag, out var player) || player == null)
                return false;

            if (!TryResolveBasePosition(tag, player.Definition, out var basePosition))
                return false;

            return player.TrySamplePosition(_dynamicContext, basePosition, in request, out position);
        }

        public bool TrySamplePosition(IReadOnlyList<string> tags, AreaTagSelectionMode selectionMode, in AreaSampleRequest request, out Vector3 position, out string selectedTag)
        {
            position = default;
            selectedTag = string.Empty;

            if (tags == null || tags.Count == 0)
                return false;

            if (selectionMode != AreaTagSelectionMode.RandomOne)
                selectionMode = AreaTagSelectionMode.RandomOne;

            var count = tags.Count;
            var start = UnityEngine.Random.Range(0, count);
            for (int i = 0; i < count; i++)
            {
                var idx = (start + i) % count;
                var tag = tags[idx];
                if (!TryGetPlayer(tag, out var player) || player == null)
                    continue;

                if (!TryResolveBasePosition(tag, player.Definition, out var basePosition))
                    continue;

                if (!player.TrySamplePosition(_dynamicContext, basePosition, in request, out position))
                    continue;

                selectedTag = NormalizeTag(tag);
                return true;
            }

            return false;
        }

        public bool TryGetPlayer(string tag, out IAreaChannelPlayer player)
        {
            if (!TryGetRuntime(tag, out var runtime))
            {
                player = null!;
                return false;
            }

            player = runtime;
            return true;
        }

        public bool ContainsPosition(string tag, Vector3 worldPosition)
        {
            if (!TryGetPlayer(tag, out var player) || player == null)
                return false;

            if (!TryResolveBasePosition(tag, player.Definition, out var basePosition))
                return false;

            return player.ContainsPosition(_dynamicContext, basePosition, worldPosition);
        }

        public bool TryGetContour(string tag, out AreaContourData contour)
        {
            contour = default;

            if (!TryGetPlayer(tag, out var player) || player == null)
                return false;

            if (!TryResolveBasePosition(tag, player.Definition, out var basePosition))
                return false;

            return player.TryGetContour(_dynamicContext, basePosition, out contour);
        }

        public bool TryGetRectSnapshot(string tag, out AreaRectSnapshot snapshot)
        {
            snapshot = default;

            if (!TryGetPlayer(tag, out var player) || player == null)
                return false;

            if (!TryResolveBasePosition(tag, player.Definition, out var basePosition))
                return false;

            return player.TryGetRectSnapshot(_dynamicContext, basePosition, out snapshot);
        }

        public bool TryGetCanvasRectSnapshot(string tag, Canvas canvas, out AreaCanvasRectSnapshot snapshot)
        {
            snapshot = default;

            if (canvas == null)
                return false;

            if (!TryGetPlayer(tag, out var player) || player == null)
                return false;

            if (!TryResolveBasePosition(tag, player.Definition, out var basePosition))
                return false;

            return player.TryGetCanvasRectSnapshot(_dynamicContext, basePosition, canvas, out snapshot);
        }

        public bool TryGetChannelDef(string tag, out ChannelDefBase def)
        {
            tag = NormalizeTag(tag);

            if (_defsByTag.TryGetValue(tag, out var hit) && hit != null)
            {
                def = hit;
                return true;
            }

            def = null!;
            return false;
        }

        public bool RegisterChannel(ChannelDefBase def, bool overwrite = false)
        {
            if (def is not AreaChannelDefinition typed)
                return false;

            return RegisterChannelInternal(typed, overwrite);
        }

        public bool UnregisterChannel(string tag)
        {
            tag = NormalizeTag(tag);

            if (!_defsByTag.Remove(tag))
                return false;

            _runtimeByTag.Remove(tag);
            _defsDirty = true;
            return true;
        }

        public bool MutateChannel(string tag, AreaChannelRuntimeMutation mutation)
        {
            if (mutation == null || !mutation.HasAnyMutation())
                return false;

            tag = NormalizeTag(tag);

            if (!_defsByTag.TryGetValue(tag, out var def) || def == null)
                return false;

            if (!ApplyMutation(def, mutation))
                return false;

            if (_ownerScope is Component ownerComponent)
                def.EnsureIntegrity(ownerComponent);
            else
                def.Sample?.EnsureIntegrity();

            if (def.Shape == null)
                def.Shape = new CircleAreaShape();

            _runtimeByTag[tag] = new AreaChannelRuntimePlayer(def);
            _defsDirty = true;
            return true;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _ownerScope = scope;
            _dynamicContext = AreaChannelDynamicContextUtility.CreateContext(scope);
            _missingBasePositionLoggedTags.Clear();
            foreach (var runtime in _runtimeByTag.Values)
                runtime.ResetRuntime();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _ownerScope = null;
            _dynamicContext = EmptyDynamicContext.Instance;
            _missingBasePositionLoggedTags.Clear();
        }

        bool RegisterChannelInternal(AreaChannelDefinition? def, bool overwrite)
        {
            if (def == null)
                return false;

            var normalizedTag = NormalizeTag(def.Tag);
            if (string.IsNullOrWhiteSpace(normalizedTag))
                return false;

            if (_defsByTag.ContainsKey(normalizedTag))
            {
                if (!overwrite)
                    return false;

                _defsByTag.Remove(normalizedTag);
                _runtimeByTag.Remove(normalizedTag);
            }

            _defsByTag[normalizedTag] = def;
            _runtimeByTag[normalizedTag] = new AreaChannelRuntimePlayer(def);
            _defsDirty = true;
            return true;
        }

        bool TryGetRuntime(string tag, out AreaChannelRuntimePlayer runtime)
        {
            tag = NormalizeTag(tag);

            if (_runtimeByTag.TryGetValue(tag, out runtime) && runtime != null && runtime.Definition.Enabled)
                return true;

            runtime = null!;
            return false;
        }

        static string NormalizeTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return "default";

            return tag.Trim();
        }

        bool TryResolveBasePosition(string tag, AreaChannelDefinition def, out Vector3 basePosition)
        {
            var anchor = def.Anchor;
            if (anchor == null)
            {
                basePosition = default;
                var normalizedTag = NormalizeTag(tag);
                if (_missingBasePositionLoggedTags.Add(normalizedTag))
                    Debug.LogError($"[AreaChannelHubService] Area channel anchor is required for range resolution. Tag='{normalizedTag}'.");
                return false;
            }

            basePosition = anchor.position + def.CenterOffset;
            return true;
        }

        static bool ApplyMutation(AreaChannelDefinition def, AreaChannelRuntimeMutation mutation)
        {
            var changed = false;

            if (mutation.ApplyEnabled && def.Enabled != mutation.Enabled)
            {
                def.Enabled = mutation.Enabled;
                changed = true;
            }

            if (mutation.ApplyCenterOffset && def.CenterOffset != mutation.CenterOffset)
            {
                def.CenterOffset = mutation.CenterOffset;
                changed = true;
            }

            if (mutation.ApplyPlane && def.Plane != mutation.Plane)
            {
                def.Plane = mutation.Plane;
                changed = true;
            }

            if (mutation.ApplySample)
            {
                def.Sample = mutation.Sample?.CreateRuntimeCopy() ?? new AreaSampleSettings();
                changed = true;
            }

            if (mutation.ApplyShape)
            {
                def.Shape = AreaChannelMutationCloneUtility.CloneShape(mutation.Shape);
                changed = true;
            }

            return changed;
        }
    }
}
