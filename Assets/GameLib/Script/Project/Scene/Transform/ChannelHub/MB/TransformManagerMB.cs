#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.TransformSystem
{
    [Serializable]
    sealed class TransformManagerEntrySettingsDefinition
    {
        [BoxGroup("Entry")]
        [LabelText("Enabled")]
        [SerializeField]
        bool _enabled = true;

        [BoxGroup("Entry")]
        [LabelText("Entry Id")]
        [Tooltip("このグローバルエントリ専用のスロットキーです。同じエントリIDは、同じスロットを更新します。空の場合、カテゴリとリストのインデックスから安定したフォールバックIDが生成されます。")]
        [SerializeField]
        string _entryId = string.Empty;

        [BoxGroup("Entry")]
        [LabelText("Apply All Channels")]
        [SerializeField]
        bool _applyAllChannels = true;

        [BoxGroup("Entry")]
        [ShowIf(nameof(ShowChannelTag))]
        [LabelText("Channel Tag")]
        [SerializeField]
        string _channelTag = TransformChannelTagUtility.DefaultTag;

        [BoxGroup("Blend")]
        [LabelText("Priority")]
        [SerializeField]
        int _priority;

        [BoxGroup("Blend")]
        [LabelText("Blend Mode")]
        [SerializeField]
        TransformChannelGlobalBlendMode _blendMode = TransformChannelGlobalBlendMode.Additive;

        [BoxGroup("Blend")]
        [LabelText("Weight")]
        [MinValue(0f)]
        [SerializeField]
        float _weight = 1f;

        [BoxGroup("Lifetime")]
        [LabelText("One Shot")]
        [SerializeField]
        bool _oneShot;

        [BoxGroup("Lifetime")]
        [LabelText("Duration Seconds")]
        [MinValue(0f)]
        [SerializeField]
        float _durationSeconds;

        bool ShowChannelTag => !_applyAllChannels;

        public bool TryBuild(string fallbackEntryId, out TransformManagerEntrySettings settings)
        {
            settings = default;
            if (!_enabled)
                return false;

            var resolvedEntryId = string.IsNullOrWhiteSpace(_entryId)
                ? fallbackEntryId
                : _entryId.Trim();

            if (string.IsNullOrWhiteSpace(resolvedEntryId))
                return false;

            settings = new TransformManagerEntrySettings(
                resolvedEntryId,
                _applyAllChannels,
                _channelTag,
                _priority,
                _blendMode,
                _weight,
                0,
                _oneShot,
                _durationSeconds);
            return true;
        }
    }

    [Serializable]
    sealed class TransformManagerMovementGlobalValue
    {
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        TransformManagerEntrySettingsDefinition _settings = new();

        [BoxGroup("Movement")]
        [LabelText("Velocity")]
        [SerializeField]
        Vector2 _velocity = Vector2.zero;

        public bool TryBuild(int index, out TransformManagerMovementEntry entry)
        {
            entry = default;
            if (!_settings.TryBuild($"TransformManager.Movement.{index}", out var settings))
                return false;

            entry = new TransformManagerMovementEntry(settings, _velocity);
            return true;
        }
    }

    [Serializable]
    sealed class TransformManagerRotateGlobalValue
    {
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        TransformManagerEntrySettingsDefinition _settings = new();

        [BoxGroup("Rotate")]
        [LabelText("Offset Degrees")]
        [SerializeField]
        float _offsetDegrees;

        [BoxGroup("Rotate")]
        [LabelText("Angular Velocity")]
        [SerializeField]
        float _angularVelocity;

        public bool TryBuild(int index, out TransformManagerRotateEntry entry)
        {
            entry = default;
            if (!_settings.TryBuild($"TransformManager.Rotate.{index}", out var settings))
                return false;

            entry = new TransformManagerRotateEntry(settings, _offsetDegrees, _angularVelocity);
            return true;
        }
    }

    [Serializable]
    sealed class TransformManagerScaleGlobalValue
    {
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        TransformManagerEntrySettingsDefinition _settings = new();

        [BoxGroup("Scale")]
        [LabelText("Local Scale")]
        [SerializeField]
        Vector3 _localScale = Vector3.one;

        public bool TryBuild(int index, out TransformManagerScaleEntry entry)
        {
            entry = default;
            if (!_settings.TryBuild($"TransformManager.Scale.{index}", out var settings))
                return false;

            entry = new TransformManagerScaleEntry(settings, _localScale);
            return true;
        }
    }

    [DisallowMultipleComponent]
    public sealed class TransformManagerMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Movement Global")]
        [LabelText("Entries")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<TransformManagerMovementGlobalValue> _movementGlobals = new();

        [BoxGroup("Rotate Global")]
        [LabelText("Entries")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<TransformManagerRotateGlobalValue> _rotateGlobals = new();

        [BoxGroup("Scale Global")]
        [LabelText("Entries")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<TransformManagerScaleGlobalValue> _scaleGlobals = new();

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            _ = scope;

            builder.Register<TransformManagerService>(Lifetime.Singleton)
                .WithParameter(this)
                .As<ITransformManagerService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>();
        }

        internal void ApplyInitialEntries(ITransformManagerService manager)
        {
            if (manager == null)
                return;

            for (var i = 0; i < _movementGlobals.Count; i++)
            {
                if (_movementGlobals[i] == null)
                    continue;

                if (_movementGlobals[i].TryBuild(i, out var entry))
                    manager.UpsertMovement(entry);
            }

            for (var i = 0; i < _rotateGlobals.Count; i++)
            {
                if (_rotateGlobals[i] == null)
                    continue;

                if (_rotateGlobals[i].TryBuild(i, out var entry))
                    manager.UpsertRotate(entry);
            }

            for (var i = 0; i < _scaleGlobals.Count; i++)
            {
                if (_scaleGlobals[i] == null)
                    continue;

                if (_scaleGlobals[i].TryBuild(i, out var entry))
                    manager.UpsertScale(entry);
            }
        }
    }
}
