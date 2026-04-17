#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using UnityEngine;
using VContainer.Unity;

namespace Game.TransformSystem
{
    public readonly struct TransformManagerChannelApplyRequest
    {
        public TransformManagerChannelApplyRequest(bool hasChannelTagFilter, string? channelTagFilter)
        {
            HasChannelTagFilter = hasChannelTagFilter;
            ChannelTagFilter = hasChannelTagFilter
                ? TransformChannelTagUtility.Normalize(channelTagFilter)
                : string.Empty;
        }

        public static TransformManagerChannelApplyRequest All => new(false, string.Empty);

        public static TransformManagerChannelApplyRequest ForChannelTag(string? channelTag)
        {
            if (string.IsNullOrWhiteSpace(channelTag))
                return All;

            return new TransformManagerChannelApplyRequest(true, channelTag);
        }

        public bool HasChannelTagFilter { get; }
        public string ChannelTagFilter { get; }
    }

    public readonly struct TransformManagerEntrySettings
    {
        public TransformManagerEntrySettings(
            string entryId,
            bool applyAllChannels,
            string? channelTag,
            int priority,
            TransformChannelGlobalBlendMode blendMode,
            float weight,
            DynamicValue<bool> condition,
            int version,
            bool oneShot,
            float durationSeconds)
        {
            EntryId = entryId;
            ApplyAllChannels = applyAllChannels;
            ChannelTag = applyAllChannels ? string.Empty : TransformChannelTagUtility.Normalize(channelTag);
            Priority = priority;
            BlendMode = blendMode;
            Weight = Mathf.Max(0f, weight);
            Condition = condition;
            Version = version;
            OneShot = oneShot;
            DurationSeconds = Mathf.Max(0f, durationSeconds);
        }

        public string EntryId { get; }
        public bool ApplyAllChannels { get; }
        public string ChannelTag { get; }
        public int Priority { get; }
        public TransformChannelGlobalBlendMode BlendMode { get; }
        public float Weight { get; }
        public DynamicValue<bool> Condition { get; }
        public int Version { get; }
        public bool OneShot { get; }
        public float DurationSeconds { get; }
    }

    public readonly struct TransformManagerMovementEntry
    {
        public TransformManagerMovementEntry(TransformManagerEntrySettings settings, Vector2 velocity)
        {
            Settings = settings;
            Velocity = velocity;
        }

        public TransformManagerEntrySettings Settings { get; }
        public Vector2 Velocity { get; }

        public TransformManagerMovementEntry WithSettings(TransformManagerEntrySettings settings)
            => new(settings, Velocity);
    }

    public readonly struct TransformManagerRotateEntry
    {
        public TransformManagerRotateEntry(TransformManagerEntrySettings settings, float offsetDegrees, float angularVelocity)
        {
            Settings = settings;
            OffsetDegrees = offsetDegrees;
            AngularVelocity = angularVelocity;
        }

        public TransformManagerEntrySettings Settings { get; }
        public float OffsetDegrees { get; }
        public float AngularVelocity { get; }

        public TransformManagerRotateEntry WithSettings(TransformManagerEntrySettings settings)
            => new(settings, OffsetDegrees, AngularVelocity);
    }

    public readonly struct TransformManagerScaleEntry
    {
        public TransformManagerScaleEntry(TransformManagerEntrySettings settings, Vector3 localScale)
        {
            Settings = settings;
            LocalScale = localScale;
        }

        public TransformManagerEntrySettings Settings { get; }
        public Vector3 LocalScale { get; }

        public TransformManagerScaleEntry WithSettings(TransformManagerEntrySettings settings)
            => new(settings, LocalScale);
    }

    public interface ITransformManagerService
    {
        int RuntimeVersion { get; }

        bool UpsertMovement(TransformManagerMovementEntry entry);
        bool RemoveMovement(string entryId);
        void CollectMovementEntries(
            in TransformManagerChannelApplyRequest request,
            List<TransformManagerMovementEntry> continuousOutput,
            List<TransformManagerMovementEntry> oneShotOutput);

        bool UpsertRotate(TransformManagerRotateEntry entry);
        bool RemoveRotate(string entryId);
        void CollectRotateEntries(
            in TransformManagerChannelApplyRequest request,
            List<TransformManagerRotateEntry> continuousOutput,
            List<TransformManagerRotateEntry> oneShotOutput);

        bool UpsertScale(TransformManagerScaleEntry entry);
        bool RemoveScale(string entryId);
        void CollectScaleEntries(
            in TransformManagerChannelApplyRequest request,
            List<TransformManagerScaleEntry> continuousOutput,
            List<TransformManagerScaleEntry> oneShotOutput);
    }

    public sealed class TransformManagerService :
        ITransformManagerService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable
    {
        struct MovementEntryState
        {
            public TransformManagerMovementEntry Entry;
            public float RemainingSeconds;
        }
        struct RotateEntryState
        {
            public TransformManagerRotateEntry Entry;
            public float RemainingSeconds;
        }

        struct ScaleEntryState
        {
            public TransformManagerScaleEntry Entry;
            public float RemainingSeconds;
        }

        readonly TransformManagerMB? _source;

        readonly Dictionary<string, MovementEntryState> _movementEntries = new(StringComparer.Ordinal);
        readonly Dictionary<string, RotateEntryState> _rotateEntries = new(StringComparer.Ordinal);
        readonly Dictionary<string, ScaleEntryState> _scaleEntries = new(StringComparer.Ordinal);

        readonly List<string> _movementTimedEntryIds = new();
        readonly List<string> _rotateTimedEntryIds = new();
        readonly List<string> _scaleTimedEntryIds = new();

        readonly Dictionary<string, int> _movementTimedEntryIndices = new(StringComparer.Ordinal);
        readonly Dictionary<string, int> _rotateTimedEntryIndices = new(StringComparer.Ordinal);
        readonly Dictionary<string, int> _scaleTimedEntryIndices = new(StringComparer.Ordinal);

        bool _isActive;
        int _runtimeVersion;
        public TransformManagerService(TransformManagerMB? source = null)
        {
            _source = source;
        }

        public int RuntimeVersion => _runtimeVersion;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            _isActive = true;
            ClearAllInternal();
            _source?.ApplyInitialEntries(this);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            _isActive = false;
            ClearAllInternal();
        }

        public void Tick()
        {
            if (!_isActive)
                return;
            var deltaTime = Mathf.Max(0f, Time.deltaTime);
            if (deltaTime <= 0f)
                return;

            TickMovementTimedEntries(deltaTime);
            TickRotateTimedEntries(deltaTime);
            TickScaleTimedEntries(deltaTime);
        }

        public bool UpsertMovement(TransformManagerMovementEntry entry)
        {
            if (!TryNormalizeSettings(entry.Settings, out var normalizedSettings))
                return false;

            var nextVersion = ResolveNextVersion(_movementEntries, normalizedSettings.EntryId);
            var versionedSettings = CreateVersionedSettings(normalizedSettings, nextVersion);

            var normalizedEntry = entry.WithSettings(versionedSettings);
            _movementEntries[versionedSettings.EntryId] = new MovementEntryState
            {
                Entry = normalizedEntry,
                RemainingSeconds = versionedSettings.DurationSeconds,
            };

            _runtimeVersion++;
            if (versionedSettings.DurationSeconds > 0f)
                MarkTimedEntry(versionedSettings.EntryId, _movementTimedEntryIds, _movementTimedEntryIndices);
            else
                UnmarkTimedEntry(versionedSettings.EntryId, _movementTimedEntryIds, _movementTimedEntryIndices);

            return true;
        }

        public bool RemoveMovement(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                return false;

            if (!_movementEntries.Remove(entryId))
                return false;

            UnmarkTimedEntry(entryId, _movementTimedEntryIds, _movementTimedEntryIndices);
            _runtimeVersion++;
            return true;
        }

        public void CollectMovementEntries(
            in TransformManagerChannelApplyRequest request,
            List<TransformManagerMovementEntry> continuousOutput,
            List<TransformManagerMovementEntry> oneShotOutput)
        {
            PrepareOutputs(continuousOutput, oneShotOutput);

            foreach (var kv in _movementEntries)
            {
                var entry = kv.Value.Entry;
                if (!IsEntryMatched(entry.Settings, request))
                    continue;

                if (entry.Settings.OneShot)
                {
                    oneShotOutput.Add(entry);
                    continue;
                }

                continuousOutput.Add(entry);
            }

            continuousOutput.Sort(static (a, b) => CompareSettings(a.Settings, b.Settings));
            oneShotOutput.Sort(static (a, b) => CompareSettings(a.Settings, b.Settings));
        }

        public bool UpsertRotate(TransformManagerRotateEntry entry)
        {
            if (!TryNormalizeSettings(entry.Settings, out var normalizedSettings))
                return false;

            var nextVersion = ResolveNextVersion(_rotateEntries, normalizedSettings.EntryId);
            var versionedSettings = CreateVersionedSettings(normalizedSettings, nextVersion);

            var normalizedEntry = entry.WithSettings(versionedSettings);
            _rotateEntries[versionedSettings.EntryId] = new RotateEntryState
            {
                Entry = normalizedEntry,
                RemainingSeconds = versionedSettings.DurationSeconds,
            };

            _runtimeVersion++;
            if (versionedSettings.DurationSeconds > 0f)
                MarkTimedEntry(versionedSettings.EntryId, _rotateTimedEntryIds, _rotateTimedEntryIndices);
            else
                UnmarkTimedEntry(versionedSettings.EntryId, _rotateTimedEntryIds, _rotateTimedEntryIndices);

            return true;
        }

        public bool RemoveRotate(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                return false;

            if (!_rotateEntries.Remove(entryId))
                return false;

            UnmarkTimedEntry(entryId, _rotateTimedEntryIds, _rotateTimedEntryIndices);
            _runtimeVersion++;
            return true;
        }

        public void CollectRotateEntries(
            in TransformManagerChannelApplyRequest request,
            List<TransformManagerRotateEntry> continuousOutput,
            List<TransformManagerRotateEntry> oneShotOutput)
        {
            PrepareOutputs(continuousOutput, oneShotOutput);

            foreach (var kv in _rotateEntries)
            {
                var entry = kv.Value.Entry;
                if (!IsEntryMatched(entry.Settings, request))
                    continue;

                if (entry.Settings.OneShot)
                {
                    oneShotOutput.Add(entry);
                    continue;
                }

                continuousOutput.Add(entry);
            }

            continuousOutput.Sort(static (a, b) => CompareSettings(a.Settings, b.Settings));
            oneShotOutput.Sort(static (a, b) => CompareSettings(a.Settings, b.Settings));
        }

        public bool UpsertScale(TransformManagerScaleEntry entry)
        {
            if (!TryNormalizeSettings(entry.Settings, out var normalizedSettings))
                return false;

            var nextVersion = ResolveNextVersion(_scaleEntries, normalizedSettings.EntryId);
            var versionedSettings = CreateVersionedSettings(normalizedSettings, nextVersion);

            var normalizedEntry = entry.WithSettings(versionedSettings);
            _scaleEntries[versionedSettings.EntryId] = new ScaleEntryState
            {
                Entry = normalizedEntry,
                RemainingSeconds = versionedSettings.DurationSeconds,
            };

            _runtimeVersion++;
            if (versionedSettings.DurationSeconds > 0f)
                MarkTimedEntry(versionedSettings.EntryId, _scaleTimedEntryIds, _scaleTimedEntryIndices);
            else
                UnmarkTimedEntry(versionedSettings.EntryId, _scaleTimedEntryIds, _scaleTimedEntryIndices);

            return true;
        }

        public bool RemoveScale(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                return false;

            if (!_scaleEntries.Remove(entryId))
                return false;

            UnmarkTimedEntry(entryId, _scaleTimedEntryIds, _scaleTimedEntryIndices);
            _runtimeVersion++;
            return true;
        }

        public void CollectScaleEntries(
            in TransformManagerChannelApplyRequest request,
            List<TransformManagerScaleEntry> continuousOutput,
            List<TransformManagerScaleEntry> oneShotOutput)
        {
            PrepareOutputs(continuousOutput, oneShotOutput);

            foreach (var kv in _scaleEntries)
            {
                var entry = kv.Value.Entry;
                if (!IsEntryMatched(entry.Settings, request))
                    continue;

                if (entry.Settings.OneShot)
                {
                    oneShotOutput.Add(entry);
                    continue;
                }

                continuousOutput.Add(entry);
            }

            continuousOutput.Sort(static (a, b) => CompareSettings(a.Settings, b.Settings));
            oneShotOutput.Sort(static (a, b) => CompareSettings(a.Settings, b.Settings));
        }

        void TickMovementTimedEntries(float deltaTime)
        {
            for (var i = _movementTimedEntryIds.Count - 1; i >= 0; i--)
            {
                var entryId = _movementTimedEntryIds[i];
                if (!_movementEntries.TryGetValue(entryId, out var state))
                {
                    RemoveTimedEntryAt(i, _movementTimedEntryIds, _movementTimedEntryIndices);
                    continue;
                }

                state.RemainingSeconds -= deltaTime;
                if (state.RemainingSeconds <= 0f)
                {
                    _movementEntries.Remove(entryId);
                    RemoveTimedEntryAt(i, _movementTimedEntryIds, _movementTimedEntryIndices);
                    _runtimeVersion++;
                    continue;
                }

                _movementEntries[entryId] = state;
            }
        }

        void TickRotateTimedEntries(float deltaTime)
        {
            for (var i = _rotateTimedEntryIds.Count - 1; i >= 0; i--)
            {
                var entryId = _rotateTimedEntryIds[i];
                if (!_rotateEntries.TryGetValue(entryId, out var state))
                {
                    RemoveTimedEntryAt(i, _rotateTimedEntryIds, _rotateTimedEntryIndices);
                    continue;
                }

                state.RemainingSeconds -= deltaTime;
                if (state.RemainingSeconds <= 0f)
                {
                    _rotateEntries.Remove(entryId);
                    RemoveTimedEntryAt(i, _rotateTimedEntryIds, _rotateTimedEntryIndices);
                    _runtimeVersion++;
                    continue;
                }

                _rotateEntries[entryId] = state;
            }
        }

        void TickScaleTimedEntries(float deltaTime)
        {
            for (var i = _scaleTimedEntryIds.Count - 1; i >= 0; i--)
            {
                var entryId = _scaleTimedEntryIds[i];
                if (!_scaleEntries.TryGetValue(entryId, out var state))
                {
                    RemoveTimedEntryAt(i, _scaleTimedEntryIds, _scaleTimedEntryIndices);
                    continue;
                }

                state.RemainingSeconds -= deltaTime;
                if (state.RemainingSeconds <= 0f)
                {
                    _scaleEntries.Remove(entryId);
                    RemoveTimedEntryAt(i, _scaleTimedEntryIds, _scaleTimedEntryIndices);
                    _runtimeVersion++;
                    continue;
                }

                _scaleEntries[entryId] = state;
            }
        }

        void ClearAllInternal()
        {
            _movementEntries.Clear();
            _rotateEntries.Clear();
            _scaleEntries.Clear();

            _movementTimedEntryIds.Clear();
            _rotateTimedEntryIds.Clear();
            _scaleTimedEntryIds.Clear();

            _movementTimedEntryIndices.Clear();
            _rotateTimedEntryIndices.Clear();
            _scaleTimedEntryIndices.Clear();

            _runtimeVersion++;
        }

        static bool TryNormalizeSettings(TransformManagerEntrySettings settings, out TransformManagerEntrySettings normalized)
        {
            if (string.IsNullOrWhiteSpace(settings.EntryId))
            {
                normalized = default;
                return false;
            }

            normalized = new TransformManagerEntrySettings(
                settings.EntryId.Trim(),
                settings.ApplyAllChannels,
                settings.ChannelTag,
                settings.Priority,
                settings.BlendMode,
                settings.Weight,
                settings.Condition,
                0,
                settings.OneShot,
                settings.DurationSeconds);
            return true;
        }

        static int ResolveNextVersion<TState>(Dictionary<string, TState> entries, string entryId)
            where TState : struct
        {
            if (entries == null || string.IsNullOrWhiteSpace(entryId))
                return 1;

            if (!entries.TryGetValue(entryId, out var boxedState))
                return 1;

            return boxedState switch
            {
                MovementEntryState movement => movement.Entry.Settings.Version == int.MaxValue ? int.MaxValue : movement.Entry.Settings.Version + 1,
                RotateEntryState rotate => rotate.Entry.Settings.Version == int.MaxValue ? int.MaxValue : rotate.Entry.Settings.Version + 1,
                ScaleEntryState scale => scale.Entry.Settings.Version == int.MaxValue ? int.MaxValue : scale.Entry.Settings.Version + 1,
                _ => 1,
            };
        }

        static TransformManagerEntrySettings CreateVersionedSettings(in TransformManagerEntrySettings settings, int version)
        {
            return new TransformManagerEntrySettings(
                settings.EntryId,
                settings.ApplyAllChannels,
                settings.ChannelTag,
                settings.Priority,
                settings.BlendMode,
                settings.Weight,
                settings.Condition,
                Mathf.Max(0, version),
                settings.OneShot,
                settings.DurationSeconds);
        }

        static bool IsEntryMatched(in TransformManagerEntrySettings settings, in TransformManagerChannelApplyRequest request)
        {
            if (settings.ApplyAllChannels)
                return true;

            if (!request.HasChannelTagFilter)
                return true;

            return string.Equals(settings.ChannelTag, request.ChannelTagFilter, StringComparison.Ordinal);
        }

        static int CompareSettings(in TransformManagerEntrySettings a, in TransformManagerEntrySettings b)
        {
            var byPriority = a.Priority.CompareTo(b.Priority);
            if (byPriority != 0)
                return byPriority;

            return string.CompareOrdinal(a.EntryId, b.EntryId);
        }

        static void PrepareOutputs<T>(List<T> continuousOutput, List<T> oneShotOutput)
        {
            if (continuousOutput == null)
                throw new ArgumentNullException(nameof(continuousOutput));
            if (oneShotOutput == null)
                throw new ArgumentNullException(nameof(oneShotOutput));

            continuousOutput.Clear();
            oneShotOutput.Clear();
        }

        static void MarkTimedEntry(string entryId, List<string> timedEntryIds, Dictionary<string, int> timedEntryIndices)
        {
            if (timedEntryIndices.ContainsKey(entryId))
                return;

            timedEntryIndices.Add(entryId, timedEntryIds.Count);
            timedEntryIds.Add(entryId);
        }

        static void UnmarkTimedEntry(string entryId, List<string> timedEntryIds, Dictionary<string, int> timedEntryIndices)
        {
            if (!timedEntryIndices.TryGetValue(entryId, out var index))
                return;

            RemoveTimedEntryAt(index, timedEntryIds, timedEntryIndices);
        }

        static void RemoveTimedEntryAt(int index, List<string> timedEntryIds, Dictionary<string, int> timedEntryIndices)
        {
            if (index < 0 || index >= timedEntryIds.Count)
                return;

            var removedEntryId = timedEntryIds[index];
            var lastIndex = timedEntryIds.Count - 1;
            var lastEntryId = timedEntryIds[lastIndex];

            timedEntryIds[index] = lastEntryId;
            timedEntryIndices[lastEntryId] = index;

            timedEntryIds.RemoveAt(lastIndex);
            timedEntryIndices.Remove(removedEntryId);
        }
    }
}
