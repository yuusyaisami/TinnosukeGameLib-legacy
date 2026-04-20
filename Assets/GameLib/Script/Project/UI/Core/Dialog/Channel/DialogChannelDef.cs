#nullable enable

using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Animation;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using Game.Conversation;
using Game.DI;
using Game.UI;
using Sirenix.OdinInspector;
using UnityEngine;
using ChannelAnimationPlayMode = Game.Channel.AnimationPlayMode;

namespace Game.Dialogue
{
    public enum DialogueCharacterAnchor
    {
        None = 0,
        Left = 10,
        Center = 20,
        Right = 30,
    }

    public enum DialogueRootPosition
    {
        Top = 10,
        Center = 20,
        Bottom = 30,
    }

    [Serializable]
    public sealed class DialogueChannelDefinition
    {
        [BoxGroup("Channel")]
        [LabelText("Channel Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _channelTag = "default";

        [BoxGroup("Preset")]
        [LabelText("Preset")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<DialogueChannelPreset> _presetValue =
            DynamicValue<DialogueChannelPreset>.FromSource(
                new ManagedRefLiteralSource<DialogueChannelPreset>(new DialogueChannelPreset()));

        public string ChannelTag => DialogueTagUtility.Normalize(_channelTag);
        public DynamicValue<DialogueChannelPreset> PresetValue => _presetValue;

        public DialogueChannelDefinition CreateRuntimeCopy()
        {
            return new DialogueChannelDefinition
            {
                _channelTag = _channelTag,
                _presetValue = _presetValue,
            };
        }
    }

    [Serializable]
    public sealed class DialogueChannelPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Preset")]
        [LabelText("Input")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<DialogueInputPreset> _inputPresetValue =
            DynamicValue<DialogueInputPreset>.FromSource(
                new ManagedRefLiteralSource<DialogueInputPreset>(new DialogueInputPreset()));

        [BoxGroup("Preset")]
        [LabelText("Text")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<DialogueTextPreset> _textPresetValue =
            DynamicValue<DialogueTextPreset>.FromSource(
                new ManagedRefLiteralSource<DialogueTextPreset>(new DialogueTextPreset()));

        [BoxGroup("Preset")]
        [LabelText("Choice")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<DialogueChoicePreset> _choicePresetValue =
            DynamicValue<DialogueChoicePreset>.FromSource(
                new ManagedRefLiteralSource<DialogueChoicePreset>(new DialogueChoicePreset()));

        [BoxGroup("Preset")]
        [LabelText("Character")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<DialogueCharacterPresetBase> _characterPresetValue =
            DynamicValue<DialogueCharacterPresetBase>.FromSource(
                new ManagedRefLiteralSource<DialogueCharacterPresetBase>(new RuntimeSpawnCharacterPreset()));

        [BoxGroup("Preset")]
        [LabelText("Layout")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<DialogueLayoutPresetBase> _layoutPresetValue =
            DynamicValue<DialogueLayoutPresetBase>.FromSource(
                new ManagedRefLiteralSource<DialogueLayoutPresetBase>(new DialogueLayoutPreset()));

        [BoxGroup("Preset")]
        [LabelText("State")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<DialogueStatePreset> _statePresetValue =
            DynamicValue<DialogueStatePreset>.FromSource(
                new ManagedRefLiteralSource<DialogueStatePreset>(new DialogueStatePreset()));

        public DynamicValue<DialogueInputPreset> InputPresetValue => _inputPresetValue;
        public DynamicValue<DialogueTextPreset> TextPresetValue => _textPresetValue;
        public DynamicValue<DialogueChoicePreset> ChoicePresetValue => _choicePresetValue;
        public DynamicValue<DialogueCharacterPresetBase> CharacterPresetValue => _characterPresetValue;
        public DynamicValue<DialogueLayoutPresetBase> LayoutPresetValue => _layoutPresetValue;
        public DynamicValue<DialogueStatePreset> StatePresetValue => _statePresetValue;

        public DialogueChannelPreset CreateRuntimeCopy()
        {
            return new DialogueChannelPreset
            {
                _inputPresetValue = _inputPresetValue,
                _textPresetValue = _textPresetValue,
                _choicePresetValue = _choicePresetValue,
                _characterPresetValue = _characterPresetValue,
                _layoutPresetValue = _layoutPresetValue,
                _statePresetValue = _statePresetValue,
            };
        }
    }

    public sealed class DialoguePresetRuntimeSnapshot
    {
        public DialogueInputPreset Input = new();
        public DialogueTextPreset Text = new();
        public DialogueChoicePreset Choice = new();
        public DialogueCharacterPresetBase Character = new RuntimeSpawnCharacterPreset();
        public DialogueLayoutPresetBase Layout = new DialogueLayoutPreset();
        public DialogueStatePreset State = new();

        public static DialoguePresetRuntimeSnapshot Resolve(DynamicValue<DialogueChannelPreset> source, IDynamicContext context)
        {
            var root = source.GetOrDefault(context, new DialogueChannelPreset());
            var result = new DialoguePresetRuntimeSnapshot
            {
                Input = root.InputPresetValue.GetOrDefault(context, new DialogueInputPreset()).CreateRuntimeCopy(),
                Text = root.TextPresetValue.GetOrDefault(context, new DialogueTextPreset()).CreateRuntimeCopy(),
                Choice = root.ChoicePresetValue.GetOrDefault(context, new DialogueChoicePreset()).CreateRuntimeCopy(),
                Character = root.CharacterPresetValue.GetOrDefault(context, new RuntimeSpawnCharacterPreset()).CreateRuntimeCopy(),
                Layout = root.LayoutPresetValue.GetOrDefault(context, new DialogueLayoutPreset()).CreateRuntimeCopy(),
                State = root.StatePresetValue.GetOrDefault(context, new DialogueStatePreset()).CreateRuntimeCopy(),
            };

            return result;
        }
    }

    [Serializable]
    public sealed class DialogueInputPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Input")]
        [LabelText("Enable Input")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _enableInput = true;

        [BoxGroup("Input")]
        [ShowIf(nameof(UsesInputSettings))]
        [LabelText("Button Channel Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _buttonChannelTag = "default";

        [BoxGroup("Input")]
        [ShowIf(nameof(UsesInputSettings))]
        [LabelText("Advance Phase")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        ButtonChannelPhase _advancePhase = ButtonChannelPhase.Pressed;

        [BoxGroup("Input")]
        [ShowIf(nameof(UsesInputSettings))]
        [LabelText("Require Phase Transition")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _requirePhaseTransition = true;

        [BoxGroup("Modal")]
        [ShowIf(nameof(UsesInputSettings))]
        [LabelText("Auto Push Modal")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _autoPushModalLayer = true;

        [BoxGroup("Modal")]
        [ShowIf(nameof(UsesModalSettings))]
        [LabelText("Modal Layer Key")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _modalLayerKey = "default";

        [BoxGroup("Modal")]
        [ShowIf(nameof(UsesModalSettings))]
        [LabelText("Modal Options")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        ModalOptions _modalOptions = ModalOptions.Default;

        public bool EnableInput => _enableInput;
        public string ButtonChannelTag => DialogueTagUtility.Normalize(_buttonChannelTag);
        public ButtonChannelPhase AdvancePhase => _advancePhase;
        public bool RequirePhaseTransition => _requirePhaseTransition;
        public bool AutoPushModalLayer => _autoPushModalLayer;
        public string ModalLayerKey => DialogueTagUtility.Normalize(_modalLayerKey);
        public ModalOptions ModalOptions => _modalOptions;

        bool UsesInputSettings => _enableInput;
        bool UsesModalSettings => _enableInput && _autoPushModalLayer;

        public DialogueInputPreset CreateRuntimeCopy()
        {
            return new DialogueInputPreset
            {
                _enableInput = _enableInput,
                _buttonChannelTag = _buttonChannelTag,
                _advancePhase = _advancePhase,
                _requirePhaseTransition = _requirePhaseTransition,
                _autoPushModalLayer = _autoPushModalLayer,
                _modalLayerKey = _modalLayerKey,
                _modalOptions = _modalOptions,
            };
        }
    }

    [Serializable]
    public sealed class DialogueCharacterLayoutPreset
    {
        [BoxGroup("Layout")]
        [LabelText("On None")]
        [Tooltip("Inspector setting.")]
        [CommandListFunctionName("DialogueChannel.CharacterLayout.OnNone")]
        [SerializeField]
        CommandListData _onNone = new();

        [BoxGroup("Layout")]
        [LabelText("On Left")]
        [Tooltip("Inspector setting.")]
        [CommandListFunctionName("DialogueChannel.CharacterLayout.OnLeft")]
        [SerializeField]
        CommandListData _onLeft = new();

        [BoxGroup("Layout")]
        [LabelText("On Center")]
        [Tooltip("Inspector setting.")]
        [CommandListFunctionName("DialogueChannel.CharacterLayout.OnCenter")]
        [SerializeField]
        CommandListData _onCenter = new();

        [BoxGroup("Layout")]
        [LabelText("On Right")]
        [Tooltip("Inspector setting.")]
        [CommandListFunctionName("DialogueChannel.CharacterLayout.OnRight")]
        [SerializeField]
        CommandListData _onRight = new();

        public CommandListData ResolveCommands(DialogueCharacterAnchor anchor)
        {
            return anchor switch
            {
                DialogueCharacterAnchor.Left => _onLeft,
                DialogueCharacterAnchor.Center => _onCenter,
                DialogueCharacterAnchor.Right => _onRight,
                _ => _onNone,
            };
        }

        public DialogueCharacterLayoutPreset CreateRuntimeCopy()
        {
            return new DialogueCharacterLayoutPreset
            {
                _onNone = DialogueCloneUtility.CloneCommandList(_onNone),
                _onLeft = DialogueCloneUtility.CloneCommandList(_onLeft),
                _onCenter = DialogueCloneUtility.CloneCommandList(_onCenter),
                _onRight = DialogueCloneUtility.CloneCommandList(_onRight),
            };
        }
    }

    [Serializable]
    public abstract class DialogueCharacterPresetBase : IDynamicManagedRefValue
    {
        [BoxGroup("Character")]
        [LabelText("Character Layout")]
        [Tooltip("Inspector setting.")]
        [InlineProperty]
        [SerializeField]
        DialogueCharacterLayoutPreset _characterLayout = new();

        [BoxGroup("Character")]
        [LabelText("Default Name Channel Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _defaultNameChannelTag = "default";

        [BoxGroup("Character")]
        [LabelText("Default Sprite Channel Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _defaultSpriteChannelTag = "default";

        public DialogueCharacterLayoutPreset CharacterLayout => _characterLayout;
        public string DefaultNameChannelTag => DialogueTagUtility.Normalize(_defaultNameChannelTag);
        public string DefaultSpriteChannelTag => DialogueTagUtility.Normalize(_defaultSpriteChannelTag);

        public virtual bool EnableRuntimeSpawn => false;
        public virtual Transform? RuntimeParent => null;
        public virtual bool ReleaseSpawnedOnEnd => false;
        public virtual string RuntimeIdentityCategory => "DialogueCharacter";

        public abstract DialogueCharacterPresetBase CreateRuntimeCopy();

        protected void CopyCommonStateTo(DialogueCharacterPresetBase copy)
        {
            copy._characterLayout = _characterLayout?.CreateRuntimeCopy() ?? new DialogueCharacterLayoutPreset();
            copy._defaultNameChannelTag = _defaultNameChannelTag;
            copy._defaultSpriteChannelTag = _defaultSpriteChannelTag;
        }
    }

    [Serializable]
    public sealed class RuntimeSpawnCharacterPreset : DialogueCharacterPresetBase
    {
        [BoxGroup("Spawn")]
        [LabelText("Runtime Parent")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        Transform? _runtimeParent;

        [BoxGroup("Spawn")]
        [LabelText("Release Spawned On End")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _releaseSpawnedOnEnd = true;

        [BoxGroup("Spawn")]
        [LabelText("Runtime Identity Category")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _runtimeIdentityCategory = "DialogueCharacter";

        public override bool EnableRuntimeSpawn => true;
        public override Transform? RuntimeParent => _runtimeParent;
        public override bool ReleaseSpawnedOnEnd => _releaseSpawnedOnEnd;
        public override string RuntimeIdentityCategory => string.IsNullOrWhiteSpace(_runtimeIdentityCategory) ? "DialogueCharacter" : _runtimeIdentityCategory.Trim();

        public override DialogueCharacterPresetBase CreateRuntimeCopy()
        {
            var copy = new RuntimeSpawnCharacterPreset
            {
                _runtimeParent = _runtimeParent,
                _releaseSpawnedOnEnd = _releaseSpawnedOnEnd,
                _runtimeIdentityCategory = _runtimeIdentityCategory,
            };

            CopyCommonStateTo(copy);
            return copy;
        }
    }

    [Serializable]
    public sealed class DialogueCharacterPreset : DialogueCharacterPresetBase
    {
        [BoxGroup("Spawn")]
        [LabelText("Enable Runtime Spawn")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _enableRuntimeSpawn = true;

        [BoxGroup("Spawn")]
        [LabelText("Runtime Parent")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        Transform? _runtimeParent;

        [BoxGroup("Spawn")]
        [LabelText("Release Spawned On End")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _releaseSpawnedOnEnd = true;

        [BoxGroup("Spawn")]
        [LabelText("Runtime Identity Category")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _runtimeIdentityCategory = "DialogueCharacter";

        public override bool EnableRuntimeSpawn => _enableRuntimeSpawn;
        public override Transform? RuntimeParent => _runtimeParent;
        public override bool ReleaseSpawnedOnEnd => _releaseSpawnedOnEnd;
        public override string RuntimeIdentityCategory => string.IsNullOrWhiteSpace(_runtimeIdentityCategory) ? "DialogueCharacter" : _runtimeIdentityCategory.Trim();

        public override DialogueCharacterPresetBase CreateRuntimeCopy()
        {
            var copy = new DialogueCharacterPreset
            {
                _enableRuntimeSpawn = _enableRuntimeSpawn,
                _runtimeParent = _runtimeParent,
                _releaseSpawnedOnEnd = _releaseSpawnedOnEnd,
                _runtimeIdentityCategory = _runtimeIdentityCategory,
            };

            CopyCommonStateTo(copy);
            return copy;
        }
    }

    [Serializable]
    public sealed class SimpleCharacterPreset : DialogueCharacterPresetBase
    {
        public override DialogueCharacterPresetBase CreateRuntimeCopy()
        {
            var copy = new SimpleCharacterPreset();
            CopyCommonStateTo(copy);
            return copy;
        }
    }

    [Serializable]
    public abstract class DialogueLayoutPresetBase : IDynamicManagedRefValue
    {
        [BoxGroup("Layout")]
        [LabelText("Enable")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _enableLayout = true;

        [BoxGroup("Layout")]
        [ShowIf(nameof(UsesLayout))]
        [LabelText("Root Position")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DialogueRootPosition _rootPosition = DialogueRootPosition.Bottom;

        [BoxGroup("Layout")]
        [ShowIf(nameof(UsesLayout))]
        [LabelText("Refresh On Setup")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _refreshOnSetup = true;

        [BoxGroup("Layout")]
        [ShowIf(nameof(UsesLayout))]
        [LabelText("Refresh On Message")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _refreshOnMessage = false;

        [BoxGroup("Layout")]
        [ShowIf(nameof(UsesLayout))]
        [LabelText("Refresh On Character Update")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _refreshOnCharacterUpdate = true;

        public bool EnableLayout => _enableLayout;
        public DialogueRootPosition RootPosition => _rootPosition;
        public bool RefreshOnSetup => _refreshOnSetup;
        public bool RefreshOnMessage => _refreshOnMessage;
        public bool RefreshOnCharacterUpdate => _refreshOnCharacterUpdate;
        public virtual DynamicValue<float> RootMargin => DynamicValueExtensions.FromLiteral(0f);
        public virtual DynamicValue<float> MoveDurationSeconds => DynamicValueExtensions.FromLiteral(0f);
        public virtual Ease MoveEase => Ease.Linear;

        public bool UsesLayout => _enableLayout;

        public abstract DialogueLayoutPresetBase CreateRuntimeCopy();

        protected void CopyCommonStateTo(DialogueLayoutPresetBase copy)
        {
            copy._enableLayout = _enableLayout;
            copy._rootPosition = _rootPosition;
            copy._refreshOnSetup = _refreshOnSetup;
            copy._refreshOnMessage = _refreshOnMessage;
            copy._refreshOnCharacterUpdate = _refreshOnCharacterUpdate;
        }
    }

    [Serializable]
    public sealed class DialogueLayoutPreset : DialogueLayoutPresetBase
    {
        [BoxGroup("Layout")]
        [ShowIf(nameof(UsesLayout))]
        [LabelText("Root Transform Channel Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _rootTransformChannelTag = "default";

        [BoxGroup("Layout")]
        [ShowIf(nameof(UsesLayout))]
        [LabelText("Root Margin")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<float> _rootMargin = DynamicValueExtensions.FromLiteral(32f);

        [BoxGroup("Motion")]
        [ShowIf(nameof(UsesLayout))]
        [LabelText("Move Duration")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<float> _moveDurationSeconds = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Motion")]
        [ShowIf(nameof(UsesLayout))]
        [LabelText("Move Ease")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        Ease _moveEase = Ease.Linear;

        public string RootTransformChannelTag => DialogueTagUtility.Normalize(_rootTransformChannelTag);
        public override DynamicValue<float> RootMargin => _rootMargin;
        public override DynamicValue<float> MoveDurationSeconds => _moveDurationSeconds;
        public override Ease MoveEase => _moveEase;

        public override DialogueLayoutPresetBase CreateRuntimeCopy()
        {
            var copy = new DialogueLayoutPreset
            {
                _rootTransformChannelTag = _rootTransformChannelTag,
                _rootMargin = _rootMargin,
                _moveDurationSeconds = _moveDurationSeconds,
                _moveEase = _moveEase,
            };

            CopyCommonStateTo(copy);
            return copy;
        }
    }

    [Serializable]
    public sealed class DialogueLayoutCommandOnlyPreset : DialogueLayoutPresetBase
    {
        [BoxGroup("Hooks")]
        [LabelText("Commands")]
        [Tooltip("Inspector setting.")]
        [CommandListFunctionName("DialogueChannel.Layout.CommandOnly")]
        [SerializeField]
        CommandListData _commands = new();

        public CommandListData Commands => _commands;

        public override DialogueLayoutPresetBase CreateRuntimeCopy()
        {
            var copy = new DialogueLayoutCommandOnlyPreset
            {
                _commands = DialogueCloneUtility.CloneCommandList(_commands),
            };

            CopyCommonStateTo(copy);
            return copy;
        }
    }

    [Serializable]
    public sealed class DialogueTextChannelBinding : IDynamicManagedRefValue
    {
        [BoxGroup("Binding")]
        [LabelText("Dialogue Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _dialogueTag = "default";

        [BoxGroup("Binding")]
        [LabelText("Text Channel Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _textChannelTag = "default";

        [BoxGroup("Binding")]
        [LabelText("Transform Animation Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _transformAnimationTag = string.Empty;

        public string DialogueTag => DialogueTagUtility.Normalize(_dialogueTag);
        public string TextChannelTag => DialogueTagUtility.Normalize(_textChannelTag);
        public string TransformAnimationTag => DialogueTagUtility.Normalize(_transformAnimationTag);

        public DialogueTextChannelBinding CreateRuntimeCopy()
        {
            return new DialogueTextChannelBinding
            {
                _dialogueTag = _dialogueTag,
                _textChannelTag = _textChannelTag,
                _transformAnimationTag = _transformAnimationTag,
            };
        }
    }

    [Serializable]
    public sealed class DialogueTextPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Default")]
        [LabelText("Default Body Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _defaultBodyChannelTag = "default";

        [BoxGroup("Default")]
        [LabelText("Default Name Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _defaultNameChannelTag = "default";

        [BoxGroup("Default")]
        [LabelText("Default Play Mode")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        TextPlayMode _defaultPlayMode = TextPlayMode.Typewriter;

        [BoxGroup("Default")]
        [LabelText("Default Text Settings")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        SetTextSettings _defaultTextSettings = SetTextSettings.Default;

        [BoxGroup("Typewriter")]
        [LabelText("Wait Before Advance")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _waitForTypewriterBeforeAdvance = true;

        [BoxGroup("Typewriter")]
        [LabelText("Allow Skip By Input")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _allowSkipTypewriterByInput = true;

        [BoxGroup("Typewriter")]
        [LabelText("Auto Advance When Input Disabled")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _autoAdvanceWhenInputDisabled = true;

        [BoxGroup("Channels")]
        [LabelText("Body Channels")]
        [Tooltip("Inspector setting.")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true)]
        [SerializeField]
        List<DialogueTextChannelBinding> _bodyChannels = new() { new DialogueTextChannelBinding() };

        [BoxGroup("Channels")]
        [LabelText("Name Channels")]
        [Tooltip("Inspector setting.")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true)]
        [SerializeField]
        List<DialogueTextChannelBinding> _nameChannels = new() { new DialogueTextChannelBinding() };

        public string DefaultBodyChannelTag => DialogueTagUtility.Normalize(_defaultBodyChannelTag);
        public string DefaultNameChannelTag => DialogueTagUtility.Normalize(_defaultNameChannelTag);
        public TextPlayMode DefaultPlayMode => _defaultPlayMode;
        public SetTextSettings DefaultTextSettings => _defaultTextSettings;
        public bool WaitForTypewriterBeforeAdvance => _waitForTypewriterBeforeAdvance;
        public bool AllowSkipTypewriterByInput => _allowSkipTypewriterByInput;
        public bool AutoAdvanceWhenInputDisabled => _autoAdvanceWhenInputDisabled;
        public IReadOnlyList<DialogueTextChannelBinding> BodyChannels => _bodyChannels;
        public IReadOnlyList<DialogueTextChannelBinding> NameChannels => _nameChannels;

        public DialogueTextPreset CreateRuntimeCopy()
        {
            var copy = new DialogueTextPreset
            {
                _defaultBodyChannelTag = _defaultBodyChannelTag,
                _defaultNameChannelTag = _defaultNameChannelTag,
                _defaultPlayMode = _defaultPlayMode,
                _defaultTextSettings = _defaultTextSettings,
                _waitForTypewriterBeforeAdvance = _waitForTypewriterBeforeAdvance,
                _allowSkipTypewriterByInput = _allowSkipTypewriterByInput,
                _autoAdvanceWhenInputDisabled = _autoAdvanceWhenInputDisabled,
            };

            copy._bodyChannels.Clear();
            for (var i = 0; i < _bodyChannels.Count; i++)
            {
                if (_bodyChannels[i] == null)
                    continue;
                copy._bodyChannels.Add(_bodyChannels[i].CreateRuntimeCopy());
            }

            copy._nameChannels.Clear();
            for (var i = 0; i < _nameChannels.Count; i++)
            {
                if (_nameChannels[i] == null)
                    continue;
                copy._nameChannels.Add(_nameChannels[i].CreateRuntimeCopy());
            }

            return copy;
        }
    }

    [Serializable]
    public sealed class DialogueChoicePreset : IDynamicManagedRefValue
    {
        [BoxGroup("Choice")]
        [LabelText("Grid Choice Channel Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _choiceChannelTag = "default";

        [BoxGroup("Choice")]
        [LabelText("Keep Text Visible")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _keepTextVisibleDuringChoice = true;

        [BoxGroup("Choice")]
        [LabelText("Lock Dialogue Input")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _lockDialogueInputDuringChoice = true;

        [BoxGroup("Choice")]
        [LabelText("Cancel Active Choice On End")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _cancelChoiceOnEnd = true;

        [BoxGroup("Commands")]
        [LabelText("Spawn Commands")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        [CommandListFunctionName("DialogueChannel.Choice.OnSpawn")]
        CommandListData _spawnCommands = new();

        public string ChoiceChannelTag => DialogueTagUtility.Normalize(_choiceChannelTag);
        public bool KeepTextVisibleDuringChoice => _keepTextVisibleDuringChoice;
        public bool LockDialogueInputDuringChoice => _lockDialogueInputDuringChoice;
        public bool CancelChoiceOnEnd => _cancelChoiceOnEnd;
        public CommandListData SpawnCommands => _spawnCommands;

        public DialogueChoicePreset CreateRuntimeCopy()
        {
            return new DialogueChoicePreset
            {
                _choiceChannelTag = _choiceChannelTag,
                _keepTextVisibleDuringChoice = _keepTextVisibleDuringChoice,
                _lockDialogueInputDuringChoice = _lockDialogueInputDuringChoice,
                _cancelChoiceOnEnd = _cancelChoiceOnEnd,
                _spawnCommands = CloneCommandList(_spawnCommands),
            };
        }

        static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }
    }

    [Serializable]
    public sealed class DialogueStatePreset : IDynamicManagedRefValue
    {
        [BoxGroup("End")]
        [LabelText("Reset Visible On End")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _resetVisibleOnEnd = true;

        [BoxGroup("End")]
        [LabelText("Reset Active On End")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _resetActiveOnEnd = true;

        [BoxGroup("End")]
        [LabelText("Reset Dialogue Count On End")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _resetDialogueCountOnEnd = true;

        [BoxGroup("Hooks")]
        [LabelText("On Session Opened")]
        [Tooltip("Inspector setting.")]
        [CommandListFunctionName("DialogueChannel.State.OnSessionOpened")]
        [SerializeField]
        CommandListData _onSessionOpened = new();

        [BoxGroup("Hooks")]
        [LabelText("On Session Continued")]
        [Tooltip("Inspector setting.")]
        [CommandListFunctionName("DialogueChannel.State.OnSessionContinued")]
        [SerializeField]
        CommandListData _onSessionContinued = new();

        [BoxGroup("Hooks")]
        [LabelText("On Session Ended")]
        [Tooltip("Inspector setting.")]
        [CommandListFunctionName("DialogueChannel.State.OnSessionEnded")]
        [SerializeField]
        CommandListData _onSessionEnded = new();

        [BoxGroup("Hooks")]
        [LabelText("On Visible True")]
        [Tooltip("Inspector setting.")]
        [CommandListFunctionName("DialogueChannel.State.OnVisibleTrue")]
        [SerializeField]
        CommandListData _onVisibleTrue = new();

        [BoxGroup("Hooks")]
        [LabelText("On Visible False")]
        [Tooltip("Inspector setting.")]
        [CommandListFunctionName("DialogueChannel.State.OnVisibleFalse")]
        [SerializeField]
        CommandListData _onVisibleFalse = new();

        [BoxGroup("Hooks")]
        [LabelText("On Active True")]
        [Tooltip("Inspector setting.")]
        [CommandListFunctionName("DialogueChannel.State.OnActiveTrue")]
        [SerializeField]
        CommandListData _onActiveTrue = new();

        [BoxGroup("Hooks")]
        [LabelText("On Active False")]
        [Tooltip("Inspector setting.")]
        [CommandListFunctionName("DialogueChannel.State.OnActiveFalse")]
        [SerializeField]
        CommandListData _onActiveFalse = new();

        [BoxGroup("Hooks")]
        [LabelText("On Message Started")]
        [Tooltip("Inspector setting.")]
        [CommandListFunctionName("DialogueChannel.State.OnMessageStarted")]
        [SerializeField]
        CommandListData _onMessageStarted = new();

        [BoxGroup("Hooks")]
        [LabelText("On Message Completed")]
        [Tooltip("Inspector setting.")]
        [CommandListFunctionName("DialogueChannel.State.OnMessageCompleted")]
        [SerializeField]
        CommandListData _onMessageCompleted = new();

        [BoxGroup("Hooks")]
        [LabelText("On Typewriter Skipped")]
        [Tooltip("Inspector setting.")]
        [CommandListFunctionName("DialogueChannel.State.OnTypewriterSkipped")]
        [SerializeField]
        CommandListData _onTypewriterSkipped = new();

        [BoxGroup("Hooks")]
        [LabelText("On Choice Started")]
        [Tooltip("Inspector setting.")]
        [CommandListFunctionName("DialogueChannel.State.OnChoiceStarted")]
        [SerializeField]
        CommandListData _onChoiceStarted = new();

        [BoxGroup("Hooks")]
        [LabelText("On Choice Completed")]
        [Tooltip("Inspector setting.")]
        [CommandListFunctionName("DialogueChannel.State.OnChoiceCompleted")]
        [SerializeField]
        CommandListData _onChoiceCompleted = new();

        public bool ResetVisibleOnEnd => _resetVisibleOnEnd;
        public bool ResetActiveOnEnd => _resetActiveOnEnd;
        public bool ResetDialogueCountOnEnd => _resetDialogueCountOnEnd;
        public CommandListData OnSessionOpened => _onSessionOpened;
        public CommandListData OnSessionContinued => _onSessionContinued;
        public CommandListData OnSessionEnded => _onSessionEnded;
        public CommandListData OnVisibleTrue => _onVisibleTrue;
        public CommandListData OnVisibleFalse => _onVisibleFalse;
        public CommandListData OnActiveTrue => _onActiveTrue;
        public CommandListData OnActiveFalse => _onActiveFalse;
        public CommandListData OnMessageStarted => _onMessageStarted;
        public CommandListData OnMessageCompleted => _onMessageCompleted;
        public CommandListData OnTypewriterSkipped => _onTypewriterSkipped;
        public CommandListData OnChoiceStarted => _onChoiceStarted;
        public CommandListData OnChoiceCompleted => _onChoiceCompleted;

        public DialogueStatePreset CreateRuntimeCopy()
        {
            return new DialogueStatePreset
            {
                _resetVisibleOnEnd = _resetVisibleOnEnd,
                _resetActiveOnEnd = _resetActiveOnEnd,
                _resetDialogueCountOnEnd = _resetDialogueCountOnEnd,
                _onSessionOpened = DialogueCloneUtility.CloneCommandList(_onSessionOpened),
                _onSessionContinued = DialogueCloneUtility.CloneCommandList(_onSessionContinued),
                _onSessionEnded = DialogueCloneUtility.CloneCommandList(_onSessionEnded),
                _onVisibleTrue = DialogueCloneUtility.CloneCommandList(_onVisibleTrue),
                _onVisibleFalse = DialogueCloneUtility.CloneCommandList(_onVisibleFalse),
                _onActiveTrue = DialogueCloneUtility.CloneCommandList(_onActiveTrue),
                _onActiveFalse = DialogueCloneUtility.CloneCommandList(_onActiveFalse),
                _onMessageStarted = DialogueCloneUtility.CloneCommandList(_onMessageStarted),
                _onMessageCompleted = DialogueCloneUtility.CloneCommandList(_onMessageCompleted),
                _onTypewriterSkipped = DialogueCloneUtility.CloneCommandList(_onTypewriterSkipped),
                _onChoiceStarted = DialogueCloneUtility.CloneCommandList(_onChoiceStarted),
                _onChoiceCompleted = DialogueCloneUtility.CloneCommandList(_onChoiceCompleted),
            };
        }
    }

    [Serializable]
    public sealed class DialogueSetupRequest
    {
        [BoxGroup("Setup")]
        [LabelText("Set Visible")]
        [Tooltip("Inspector setting.")]
        public bool SetVisible = true;

        [BoxGroup("Setup")]
        [ShowIf(nameof(SetVisible))]
        [LabelText("Visible")]
        [Tooltip("Inspector setting.")]
        public bool Visible = true;

        [BoxGroup("Setup")]
        [LabelText("Set Active")]
        [Tooltip("Inspector setting.")]
        public bool SetActive = true;

        [BoxGroup("Setup")]
        [ShowIf(nameof(SetActive))]
        [LabelText("Active")]
        [Tooltip("Inspector setting.")]
        public bool Active = true;

        [BoxGroup("Setup")]
        [LabelText("Begin Dialogue Step")]
        [Tooltip("Inspector setting.")]
        public bool BeginDialogueStep = true;

        [BoxGroup("Setup")]
        [ShowIf(nameof(BeginDialogueStep))]
        [LabelText("Increment If Already Visible")]
        [Tooltip("Inspector setting.")]
        public bool IncrementIfAlreadyVisible = true;

        [BoxGroup("Setup")]
        [LabelText("Apply Layout")]
        [Tooltip("Inspector setting.")]
        public bool ApplyLayout = true;

        public DialogueSetupRequest CreateRuntimeCopy()
        {
            return new DialogueSetupRequest
            {
                SetVisible = SetVisible,
                Visible = Visible,
                SetActive = SetActive,
                Active = Active,
                BeginDialogueStep = BeginDialogueStep,
                IncrementIfAlreadyVisible = IncrementIfAlreadyVisible,
                ApplyLayout = ApplyLayout,
            };
        }
    }

    [Serializable]
    public sealed class DialogueEndRequest
    {
        [BoxGroup("End")]
        [LabelText("Reset Visible")]
        [Tooltip("Inspector setting.")]
        public bool ResetVisible = true;

        [BoxGroup("End")]
        [LabelText("Reset Active")]
        [Tooltip("Inspector setting.")]
        public bool ResetActive = true;

        [BoxGroup("End")]
        [LabelText("Reset Dialogue Count")]
        [Tooltip("Inspector setting.")]
        public bool ResetDialogueCount = true;

        [BoxGroup("End")]
        [LabelText("Release Spawned Characters")]
        [Tooltip("Inspector setting.")]
        public bool ReleaseSpawnedCharacters = true;

        public DialogueEndRequest CreateRuntimeCopy()
        {
            return new DialogueEndRequest
            {
                ResetVisible = ResetVisible,
                ResetActive = ResetActive,
                ResetDialogueCount = ResetDialogueCount,
                ReleaseSpawnedCharacters = ReleaseSpawnedCharacters,
            };
        }
    }

    [Serializable]
    public sealed class DialogueLayoutRefreshRequest
    {
        [BoxGroup("Layout")]
        [LabelText("Force")]
        [Tooltip("Inspector setting.")]
        public bool Force = true;

        [BoxGroup("Layout")]
        [LabelText("Override Root Position")]
        [Tooltip("Inspector setting.")]
        public bool OverrideRootPosition;

        [BoxGroup("Layout")]
        [ShowIf(nameof(OverrideRootPosition))]
        [LabelText("Root Position")]
        [Tooltip("Inspector setting.")]
        public DialogueRootPosition RootPosition = DialogueRootPosition.Bottom;

        public DialogueLayoutRefreshRequest CreateRuntimeCopy()
        {
            return new DialogueLayoutRefreshRequest
            {
                Force = Force,
                OverrideRootPosition = OverrideRootPosition,
                RootPosition = RootPosition,
            };
        }
    }

    [Serializable]
    public sealed class DialogueMessageLine
    {
        [BoxGroup("Line")]
        [LabelText("Dialogue Tag")]
        [Tooltip("Inspector setting.")]
        public string DialogueTag = "default";

        [BoxGroup("Line")]
        [LabelText("Channel Tag")]
        [Tooltip("Inspector setting.")]
        public string ChannelTag = "default";

        [BoxGroup("Line")]
        [LabelText("Text")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<string> Text = DynamicValueExtensions.FromLiteral(string.Empty);

        public DialogueMessageLine CreateRuntimeCopy()
        {
            return new DialogueMessageLine
            {
                DialogueTag = DialogueTag,
                ChannelTag = ChannelTag,
                Text = Text,
            };
        }
    }

    [Serializable]
    public sealed class DialogueMessageRequest
    {
        [BoxGroup("Message")]
        [LabelText("Body Lines")]
        [Tooltip("Inspector setting.")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true)]
        public List<DialogueMessageLine> BodyLines = new();

        [BoxGroup("Playback")]
        [LabelText("Use Preset Play Mode")]
        [Tooltip("Inspector setting.")]
        public bool UsePresetPlayMode = true;

        [BoxGroup("Playback")]
        [ShowIf(nameof(UsesExplicitPlayMode))]
        [LabelText("Play Mode")]
        [Tooltip("Inspector setting.")]
        public TextPlayMode PlayMode = TextPlayMode.Typewriter;

        [BoxGroup("Playback")]
        [LabelText("Text Settings")]
        [Tooltip("Inspector setting.")]
        public SetTextSettings TextSettings = SetTextSettings.Default;

        [BoxGroup("Playback")]
        [LabelText("Wait Typewriter Complete")]
        [ShowIf(nameof(UsesTypewriterOptions))]
        [Tooltip("Inspector setting.")]
        public bool WaitForTypewriterComplete = true;

        [BoxGroup("Advance")]
        [LabelText("Await Input")]
        [Tooltip("Inspector setting.")]
        public bool AwaitInput = true;

        [BoxGroup("Advance")]
        [LabelText("Allow Typewriter Skip By Input")]
        [ShowIf(nameof(UsesTypewriterOptions))]
        [Tooltip("Inspector setting.")]
        public bool AllowTypewriterSkipByInput = true;

        [BoxGroup("Advance")]
        [LabelText("Use Auto Advance Delay")]
        [Tooltip("Inspector setting.")]
        public bool UseAutoAdvanceDelay;

        [BoxGroup("Advance")]
        [ShowIf(nameof(UseAutoAdvanceDelay))]
        [LabelText("Auto Advance Delay")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<float> AutoAdvanceDelaySeconds = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Setup")]
        [LabelText("Auto Setup If Hidden")]
        [Tooltip("Inspector setting.")]
        public bool AutoSetupIfHidden = true;

        [BoxGroup("Setup")]
        [ShowIf(nameof(AutoSetupIfHidden))]
        [LabelText("Auto Setup")]
        [Tooltip("Inspector setting.")]
        [InlineProperty]
        public DialogueSetupRequest AutoSetup = new();

        bool UsesExplicitPlayMode => !UsePresetPlayMode;
        bool UsesTypewriterOptions => UsePresetPlayMode || PlayMode == TextPlayMode.Typewriter;

        public DialogueMessageRequest CreateRuntimeCopy()
        {
            var copy = new DialogueMessageRequest
            {
                UsePresetPlayMode = UsePresetPlayMode,
                PlayMode = PlayMode,
                TextSettings = TextSettings,
                WaitForTypewriterComplete = WaitForTypewriterComplete,
                AwaitInput = AwaitInput,
                AllowTypewriterSkipByInput = AllowTypewriterSkipByInput,
                UseAutoAdvanceDelay = UseAutoAdvanceDelay,
                AutoAdvanceDelaySeconds = AutoAdvanceDelaySeconds,
                AutoSetupIfHidden = AutoSetupIfHidden,
                AutoSetup = AutoSetup?.CreateRuntimeCopy() ?? new DialogueSetupRequest(),
            };

            for (var i = 0; i < BodyLines.Count; i++)
            {
                if (BodyLines[i] == null)
                    continue;
                copy.BodyLines.Add(BodyLines[i].CreateRuntimeCopy());
            }

            return copy;
        }
    }

    [Serializable]
    public sealed class DialogueChoiceRequest
    {
        [BoxGroup("Choice")]
        [LabelText("Use Preset Channel Tag")]
        [Tooltip("Inspector setting.")]
        public bool UsePresetChannelTag = true;

        [BoxGroup("Choice")]
        [ShowIf("@!UsePresetChannelTag")]
        [LabelText("Channel Tag")]
        [Tooltip("Inspector setting.")]
        public string ChannelTag = "default";

        [BoxGroup("Choice")]
        [LabelText("Use Preset Input Lock")]
        [Tooltip("Inspector setting.")]
        public bool UsePresetInputLock = true;

        [BoxGroup("Choice")]
        [ShowIf("@!UsePresetInputLock")]
        [LabelText("Lock Dialogue Input")]
        [Tooltip("Inspector setting.")]
        public bool LockDialogueInput = true;

        [BoxGroup("Choice")]
        [LabelText("Play Pre Message")]
        [Tooltip("Inspector setting.")]
        public bool PlayPreMessage;

        [BoxGroup("Choice")]
        [ShowIf(nameof(PlayPreMessage))]
        [LabelText("Pre Message")]
        [Tooltip("Inspector setting.")]
        public DialogueMessageRequest PreMessage = new();

        [BoxGroup("Choice")]
        [LabelText("Grid Choice Request")]
        [Tooltip("Inspector setting.")]
        [InlineProperty]
        public GridObjectChoiceRequest GridChoiceRequest = new();

        public DialogueChoiceRequest CreateRuntimeCopy()
        {
            return new DialogueChoiceRequest
            {
                UsePresetChannelTag = UsePresetChannelTag,
                ChannelTag = ChannelTag,
                UsePresetInputLock = UsePresetInputLock,
                LockDialogueInput = LockDialogueInput,
                PlayPreMessage = PlayPreMessage,
                PreMessage = PreMessage?.CreateRuntimeCopy() ?? new DialogueMessageRequest(),
                GridChoiceRequest = GridChoiceRequest?.CreateRuntimeCopy() ?? new GridObjectChoiceRequest(),
            };
        }
    }

    [Serializable]
    public sealed class DialogueCharacterEntryRequest
    {
        [BoxGroup("Character")]
        [LabelText("Character Id")]
        [Tooltip("Inspector setting.")]
        public string CharacterId = string.Empty;

        [BoxGroup("CharacterDataBase")]
        [LabelText("Character Data Id")]
        [Tooltip("Inspector setting.")]
        [MinValue(0)]
        public int CharacterDataId;

        [BoxGroup("CharacterDataBase")]
        [ShowIf(nameof(UsesStableKeyLookup))]
        [LabelText("Character Stable Key")]
        [Tooltip("Inspector setting.")]
        public string CharacterStableKey = string.Empty;

        [BoxGroup("Character")]
        [LabelText("Anchor")]
        [Tooltip("Inspector setting.")]
        public DialogueCharacterAnchor Anchor = DialogueCharacterAnchor.None;

        [BoxGroup("Character")]
        [LabelText("Display Name")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<string> DisplayName = DynamicValueExtensions.FromLiteral(string.Empty);

        [BoxGroup("Character")]
        [LabelText("Name Channel Tag")]
        [Tooltip("Inspector setting.")]
        public string NameChannelTag = string.Empty;

        [BoxGroup("Character")]
        [LabelText("Sprite Channel Tag")]
        [Tooltip("Inspector setting.")]
        public string SpriteChannelTag = string.Empty;

        [BoxGroup("Character")]
        [LabelText("Spawn If Needed")]
        [Tooltip("Inspector setting.")]
        public bool SpawnIfNeeded = true;

        [BoxGroup("Character")]
        [ShowIf(nameof(SpawnIfNeeded))]
        [LabelText("Runtime Template Preset")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<BaseRuntimeTemplatePreset> RuntimeTemplate =
            DynamicValue<BaseRuntimeTemplatePreset>.FromSource(
                new ManagedRefLiteralSource<BaseRuntimeTemplatePreset>(new BaseRuntimeTemplatePreset()));

        [BoxGroup("Character")]
        [LabelText("Expression Key")]
        [Tooltip("Inspector setting.")]
        public string ExpressionKey = string.Empty;

        [BoxGroup("Character")]
        [LabelText("Use Default Image Fallback")]
        [Tooltip("Inspector setting.")]
        public bool UseDefaultImageFallback = true;

        [BoxGroup("Character")]
        [ShowIf(nameof(SpawnIfNeeded))]
        [LabelText("Spawn Local Position")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<Vector3> SpawnLocalPosition = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [BoxGroup("Character")]
        [LabelText("Portrait Animation")]
        [Tooltip("Inspector setting.")]
        public AnimationDataSource PortraitAnimation = new();

        [BoxGroup("Character")]
        [LabelText("Portrait Play Mode")]
        [Tooltip("Inspector setting.")]
        public ChannelAnimationPlayMode PortraitPlayMode = ChannelAnimationPlayMode.Once;

        bool UsesStableKeyLookup => CharacterDataId <= 0;

        public DialogueCharacterEntryRequest CreateRuntimeCopy()
        {
            return new DialogueCharacterEntryRequest
            {
                CharacterId = CharacterId,
                CharacterDataId = CharacterDataId,
                CharacterStableKey = CharacterStableKey,
                Anchor = Anchor,
                DisplayName = DisplayName,
                NameChannelTag = NameChannelTag,
                SpriteChannelTag = SpriteChannelTag,
                SpawnIfNeeded = SpawnIfNeeded,
                RuntimeTemplate = RuntimeTemplate,
                ExpressionKey = ExpressionKey,
                UseDefaultImageFallback = UseDefaultImageFallback,
                SpawnLocalPosition = SpawnLocalPosition,
                PortraitAnimation = PortraitAnimation ?? new AnimationDataSource(),
                PortraitPlayMode = PortraitPlayMode,
            };
        }
    }

    [Serializable]
    public sealed class DialogueCharacterFrameRequest
    {
        [BoxGroup("Frame")]
        [LabelText("Entries")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true)]
        public List<DialogueCharacterEntryRequest> Entries = new();

        [BoxGroup("Frame")]
        [LabelText("Refresh Layout")]
        public bool RefreshLayout = true;

        public DialogueCharacterFrameRequest CreateRuntimeCopy()
        {
            var copy = new DialogueCharacterFrameRequest
            {
                RefreshLayout = RefreshLayout,
            };

            for (var i = 0; i < Entries.Count; i++)
            {
                if (Entries[i] == null)
                    continue;
                copy.Entries.Add(Entries[i].CreateRuntimeCopy());
            }

            return copy;
        }
    }

    public readonly struct DialogueChannelSnapshot
    {
        public string Tag { get; }
        public bool IsVisible { get; }
        public bool IsActive { get; }
        public bool IsInputEnabled { get; }
        public int DialogueCount { get; }
        public DialogueTypewriterState TypewriterState { get; }
        public DialogueChoiceState ChoiceState { get; }
        public DialogueCharacterAnchor ActiveCharacterAnchor { get; }

        public DialogueChannelSnapshot(
            string tag,
            bool isVisible,
            bool isActive,
            bool isInputEnabled,
            int dialogueCount,
            DialogueTypewriterState typewriterState,
            DialogueChoiceState choiceState,
            DialogueCharacterAnchor activeCharacterAnchor)
        {
            Tag = DialogueTagUtility.Normalize(tag);
            IsVisible = isVisible;
            IsActive = isActive;
            IsInputEnabled = isInputEnabled;
            DialogueCount = dialogueCount;
            TypewriterState = typewriterState;
            ChoiceState = choiceState;
            ActiveCharacterAnchor = activeCharacterAnchor;
        }
    }

    internal static class DialogueTagUtility
    {
        public static string Normalize(string? tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
        }
    }

    static class DialogueCloneUtility
    {
        public static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }
    }
}
