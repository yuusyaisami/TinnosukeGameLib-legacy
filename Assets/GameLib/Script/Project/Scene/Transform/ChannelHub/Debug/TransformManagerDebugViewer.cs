#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.TransformSystem
{
    [Serializable]
    public sealed class TransformManagerDebugViewer
    {
        [FoldoutGroup("Summary")]
        [ShowInInspector, ReadOnly, LabelText("Bound")]
        public bool IsBound => _manager != null;

        [FoldoutGroup("Summary")]
        [ShowInInspector, ReadOnly, LabelText("Runtime Version")]
        public int RuntimeVersion => _manager?.RuntimeVersion ?? -1;

        [FoldoutGroup("Summary")]
        [ShowInInspector, ReadOnly, LabelText("Manager Instance Id")]
        public int ManagerInstanceId => _manager != null ? RuntimeHelpers.GetHashCode(_manager) : -1;

        [FoldoutGroup("Summary")]
        [ShowInInspector, ReadOnly, LabelText("Movement Count")]
        public int MovementCount
        {
            get
            {
                EnsureSnapshot();
                return _movementRows.Count;
            }
        }

        [FoldoutGroup("Summary")]
        [ShowInInspector, ReadOnly, LabelText("Rotate Count")]
        public int RotateCount
        {
            get
            {
                EnsureSnapshot();
                return _rotateRows.Count;
            }
        }

        [FoldoutGroup("Summary")]
        [ShowInInspector, ReadOnly, LabelText("Scale Count")]
        public int ScaleCount
        {
            get
            {
                EnsureSnapshot();
                return _scaleRows.Count;
            }
        }

        [FoldoutGroup("Actions")]
        [Button(ButtonSizes.Small)]
        public void RefreshNow()
        {
            RebuildSnapshot(force: true);
        }

        [FoldoutGroup("Movement")]
        [ShowInInspector]
        [TableList(IsReadOnly = true, AlwaysExpanded = true)]
        [LabelText("Movement Entries")]
        public List<MovementRow> MovementEntries
        {
            get
            {
                EnsureSnapshot();
                return _movementRows;
            }
        }

        [FoldoutGroup("Rotate")]
        [ShowInInspector]
        [TableList(IsReadOnly = true, AlwaysExpanded = true)]
        [LabelText("Rotate Entries")]
        public List<RotateRow> RotateEntries
        {
            get
            {
                EnsureSnapshot();
                return _rotateRows;
            }
        }

        [FoldoutGroup("Scale")]
        [ShowInInspector]
        [TableList(IsReadOnly = true, AlwaysExpanded = true)]
        [LabelText("Scale Entries")]
        public List<ScaleRow> ScaleEntries
        {
            get
            {
                EnsureSnapshot();
                return _scaleRows;
            }
        }

        ITransformManagerService? _manager;
        int _lastRuntimeVersion = -1;

        readonly List<TransformManagerMovementEntry> _movementContinuousEntries = new();
        readonly List<TransformManagerMovementEntry> _movementOneShotEntries = new();
        readonly List<TransformManagerRotateEntry> _rotateContinuousEntries = new();
        readonly List<TransformManagerRotateEntry> _rotateOneShotEntries = new();
        readonly List<TransformManagerScaleEntry> _scaleContinuousEntries = new();
        readonly List<TransformManagerScaleEntry> _scaleOneShotEntries = new();

        readonly List<MovementRow> _movementRows = new();
        readonly List<RotateRow> _rotateRows = new();
        readonly List<ScaleRow> _scaleRows = new();

        public void Bind(ITransformManagerService manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _lastRuntimeVersion = -1;
            RebuildSnapshot(force: true);
        }

        void EnsureSnapshot()
        {
            if (_manager == null)
            {
                ClearRows();
                return;
            }

            if (_lastRuntimeVersion == _manager.RuntimeVersion)
                return;

            RebuildSnapshot(force: false);
        }

        void RebuildSnapshot(bool force)
        {
            if (_manager == null)
            {
                ClearRows();
                return;
            }

            var runtimeVersion = _manager.RuntimeVersion;
            if (!force && runtimeVersion == _lastRuntimeVersion)
                return;

            _movementContinuousEntries.Clear();
            _movementOneShotEntries.Clear();
            _rotateContinuousEntries.Clear();
            _rotateOneShotEntries.Clear();
            _scaleContinuousEntries.Clear();
            _scaleOneShotEntries.Clear();

            _movementRows.Clear();
            _rotateRows.Clear();
            _scaleRows.Clear();

            _manager.CollectMovementEntries(TransformManagerChannelApplyRequest.All, _movementContinuousEntries, _movementOneShotEntries);
            AddMovementRows(_movementContinuousEntries);
            AddMovementRows(_movementOneShotEntries);
            _movementRows.Sort(CompareMovementRows);

            _manager.CollectRotateEntries(TransformManagerChannelApplyRequest.All, _rotateContinuousEntries, _rotateOneShotEntries);
            AddRotateRows(_rotateContinuousEntries);
            AddRotateRows(_rotateOneShotEntries);
            _rotateRows.Sort(CompareRotateRows);

            _manager.CollectScaleEntries(TransformManagerChannelApplyRequest.All, _scaleContinuousEntries, _scaleOneShotEntries);
            AddScaleRows(_scaleContinuousEntries);
            AddScaleRows(_scaleOneShotEntries);
            _scaleRows.Sort(CompareScaleRows);

            _lastRuntimeVersion = runtimeVersion;
        }

        void ClearRows()
        {
            _movementContinuousEntries.Clear();
            _movementOneShotEntries.Clear();
            _rotateContinuousEntries.Clear();
            _rotateOneShotEntries.Clear();
            _scaleContinuousEntries.Clear();
            _scaleOneShotEntries.Clear();

            _movementRows.Clear();
            _rotateRows.Clear();
            _scaleRows.Clear();

            _lastRuntimeVersion = -1;
        }

        void AddMovementRows(List<TransformManagerMovementEntry> entries)
        {
            for (var i = 0; i < entries.Count; i++)
                _movementRows.Add(CreateMovementRow(entries[i]));
        }

        void AddRotateRows(List<TransformManagerRotateEntry> entries)
        {
            for (var i = 0; i < entries.Count; i++)
                _rotateRows.Add(CreateRotateRow(entries[i]));
        }

        void AddScaleRows(List<TransformManagerScaleEntry> entries)
        {
            for (var i = 0; i < entries.Count; i++)
                _scaleRows.Add(CreateScaleRow(entries[i]));
        }

        static MovementRow CreateMovementRow(TransformManagerMovementEntry entry)
        {
            var settings = entry.Settings;
            return new MovementRow
            {
                EntryId = settings.EntryId,
                Version = settings.Version,
                Priority = settings.Priority,
                ApplyAllChannels = settings.ApplyAllChannels,
                ChannelTag = settings.ChannelTag,
                BlendMode = settings.BlendMode,
                Weight = settings.Weight,
                Condition = DescribeCondition(settings.Condition),
                OneShot = settings.OneShot,
                DurationSeconds = settings.DurationSeconds,
                Velocity = entry.Velocity,
            };
        }

        static RotateRow CreateRotateRow(TransformManagerRotateEntry entry)
        {
            var settings = entry.Settings;
            return new RotateRow
            {
                EntryId = settings.EntryId,
                Version = settings.Version,
                Priority = settings.Priority,
                ApplyAllChannels = settings.ApplyAllChannels,
                ChannelTag = settings.ChannelTag,
                BlendMode = settings.BlendMode,
                Weight = settings.Weight,
                Condition = DescribeCondition(settings.Condition),
                OneShot = settings.OneShot,
                DurationSeconds = settings.DurationSeconds,
                OffsetDegrees = entry.OffsetDegrees,
                AngularVelocity = entry.AngularVelocity,
            };
        }

        static ScaleRow CreateScaleRow(TransformManagerScaleEntry entry)
        {
            var settings = entry.Settings;
            return new ScaleRow
            {
                EntryId = settings.EntryId,
                Version = settings.Version,
                Priority = settings.Priority,
                ApplyAllChannels = settings.ApplyAllChannels,
                ChannelTag = settings.ChannelTag,
                BlendMode = settings.BlendMode,
                Weight = settings.Weight,
                Condition = DescribeCondition(settings.Condition),
                OneShot = settings.OneShot,
                DurationSeconds = settings.DurationSeconds,
                LocalScale = entry.LocalScale,
            };
        }

        static string DescribeCondition(DynamicValue<bool> condition)
        {
            if (!condition.HasSource)
                return "(none)";

            var sourceData = condition.SourceDebugData;
            if (string.IsNullOrWhiteSpace(sourceData))
                return condition.SourceTypeName;

            return $"{condition.SourceTypeName}: {sourceData}";
        }

        static int CompareMovementRows(MovementRow a, MovementRow b)
        {
            var byPriority = a.Priority.CompareTo(b.Priority);
            if (byPriority != 0)
                return byPriority;

            return string.CompareOrdinal(a.EntryId, b.EntryId);
        }

        static int CompareRotateRows(RotateRow a, RotateRow b)
        {
            var byPriority = a.Priority.CompareTo(b.Priority);
            if (byPriority != 0)
                return byPriority;

            return string.CompareOrdinal(a.EntryId, b.EntryId);
        }

        static int CompareScaleRows(ScaleRow a, ScaleRow b)
        {
            var byPriority = a.Priority.CompareTo(b.Priority);
            if (byPriority != 0)
                return byPriority;

            return string.CompareOrdinal(a.EntryId, b.EntryId);
        }

        [Serializable]
        public sealed class MovementRow
        {
            [TableColumnWidth(160)] public string EntryId = string.Empty;
            [TableColumnWidth(60)] public int Version;
            [TableColumnWidth(60)] public int Priority;
            [TableColumnWidth(70)] public bool ApplyAllChannels;
            [TableColumnWidth(140)] public string ChannelTag = string.Empty;
            [TableColumnWidth(120)] public TransformChannelGlobalBlendMode BlendMode;
            [TableColumnWidth(70)] public float Weight;
            [TableColumnWidth(260)] public string Condition = string.Empty;
            [TableColumnWidth(60)] public bool OneShot;
            [TableColumnWidth(90)] public float DurationSeconds;
            [TableColumnWidth(120)] public Vector2 Velocity;
        }

        [Serializable]
        public sealed class RotateRow
        {
            [TableColumnWidth(160)] public string EntryId = string.Empty;
            [TableColumnWidth(60)] public int Version;
            [TableColumnWidth(60)] public int Priority;
            [TableColumnWidth(70)] public bool ApplyAllChannels;
            [TableColumnWidth(140)] public string ChannelTag = string.Empty;
            [TableColumnWidth(120)] public TransformChannelGlobalBlendMode BlendMode;
            [TableColumnWidth(70)] public float Weight;
            [TableColumnWidth(260)] public string Condition = string.Empty;
            [TableColumnWidth(60)] public bool OneShot;
            [TableColumnWidth(90)] public float DurationSeconds;
            [TableColumnWidth(90)] public float OffsetDegrees;
            [TableColumnWidth(100)] public float AngularVelocity;
        }

        [Serializable]
        public sealed class ScaleRow
        {
            [TableColumnWidth(160)] public string EntryId = string.Empty;
            [TableColumnWidth(60)] public int Version;
            [TableColumnWidth(60)] public int Priority;
            [TableColumnWidth(70)] public bool ApplyAllChannels;
            [TableColumnWidth(140)] public string ChannelTag = string.Empty;
            [TableColumnWidth(120)] public TransformChannelGlobalBlendMode BlendMode;
            [TableColumnWidth(70)] public float Weight;
            [TableColumnWidth(260)] public string Condition = string.Empty;
            [TableColumnWidth(60)] public bool OneShot;
            [TableColumnWidth(90)] public float DurationSeconds;
            [TableColumnWidth(140)] public Vector3 LocalScale;
        }
    }
}