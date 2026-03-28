#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands
{
    public enum CommandListPlaybackMode
    {
        OneShot = 10,
        Loop = 20,
        PingPong = 30,
    }

    public enum CommandListPlayerState
    {
        Stopped = 0,
        Playing = 10,
        Paused = 20,
    }

    public enum CommandListStepDirection
    {
        Forward = 10,
        Backward = 20,
    }

    public enum CommandListChannelHubControlOperation
    {
        RegisterOrReplace = 10,
        Unregister = 20,
        ClearAll = 30,
    }

    public enum CommandListChannelPlayerControlOperation
    {
        SwapCommandListPreset = 10,
        SwapPlayerPreset = 20,
        MutateCommands = 30,
        SetRuntimeVars = 40,
        ClearRuntimeVars = 50,
        ResetRuntimeOverrides = 60,
    }

    public enum CommandListChannelOperation
    {
        Play = 10,
        Pause = 20,
        Resume = 30,
        Stop = 40,
        ExecuteNow = 50,
    }

    public interface ICommandListChannelPlayer
    {
        string Tag { get; }
        CommandListPlayerState State { get; }
        int CurrentStepIndex { get; }
        int CurrentCommandCount { get; }
        bool IsExecuting { get; }
        float RemainingIntervalSeconds { get; }
        CommandListStepDirection StepDirection { get; }
    }

    public interface ICommandListChannelCommandService
    {
        bool Play(IVarStore? callerVars);
        bool Pause();
        bool Resume();
        bool Stop();
        bool ExecuteNow(IVarStore? callerVars);
        UniTask WaitForCurrentExecutionAsync(CancellationToken ct);
    }

    public interface ICommandListChannelControlService
    {
        bool SwapCommandListPreset(CommandListPreset? preset);
        bool SwapPlayerPreset(CommandListPlayerPreset? preset);
        bool MutateCommands(CommandListMutationStep? mutation, ICommandListRuntimeMutationService? mutationService);
        bool SetRuntimeVars(VarStorePayload? payload, IVarStore? callerVars, bool overwriteExistingVars);
        bool ClearRuntimeVars();
        bool ResetRuntimeOverrides(bool resetCommands, bool resetPlayer, bool resetRuntimeVars, bool resetPlaybackState);
    }

    public interface ICommandListChannelHubService
    {
        int ChannelCount { get; }
        bool Contains(string tag);
        bool TryGetPlayer(string tag, out ICommandListChannelPlayer? player);
        bool TryGetCommand(string tag, out ICommandListChannelCommandService? command);
        bool TryGetControl(string tag, out ICommandListChannelControlService? control);
        bool RegisterOrReplace(string tag, CommandListChannelPreset preset);
        bool Unregister(string tag);
        void Clear();
        void GetTags(List<string> output);
    }

    public interface ICommandListChannelOptions
    {
        DynamicValue<CommandListChannelPreset> PresetValue { get; }
    }

    [Serializable]
    public sealed class CommandListPreset : IDynamicManagedRefValue
    {
        [LabelText("Commands")]
        [HideLabel]
        [Tooltip("この preset が保持するコマンド列です。CommandListChannel の 1 step 実行や ExecuteNow の元になります。")]
        [CommandListFunctionName("CommandListChannel.Commands")]
        public CommandListData Commands = new();

        public CommandListPreset CreateRuntimeCopy()
        {
            return new CommandListPreset
            {
                Commands = Commands?.CreateRuntimeCopy() ?? new CommandListData(),
            };
        }

        public void BindDebugOwner(UnityEngine.Object owner, string fieldPath)
        {
            Commands?.BindDebugOwner(owner, fieldPath);
        }

        public override string ToString()
        {
            return $"CommandList Count={Commands?.Count ?? 0}";
        }
    }

    [Serializable]
    public sealed class CommandListPlayerPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Playback")]
        [LabelText("Playback Mode")]
        [Tooltip("再生方式です。OneShot は末尾で停止、Loop は先頭へ戻り、PingPong は端で折り返します。")]
        [SerializeField]
        CommandListPlaybackMode _playbackMode = CommandListPlaybackMode.Loop;

        [BoxGroup("Playback")]
        [LabelText("Interval Seconds")]
        [MinValue(0d)]
        [Tooltip("各 step 実行の間隔秒です。Play と AutoPlay の最初の 1 回目は即時実行され、その後この間隔で進みます。")]
        [SerializeField]
        float _intervalSeconds = 1f;

        [BoxGroup("Playback")]
        [LabelText("Auto Play")]
        [Tooltip("scope acquire 後に自動で再生を開始します。false の場合は外部 command から Play を呼ぶまで待機します。")]
        [SerializeField]
        bool _autoPlay;

        [BoxGroup("Vars")]
        [LabelText("Variables")]
        [InlineProperty]
        [HideLabel]
        [Tooltip("再生時に毎回マージされる base variables です。優先順位は caller vars -> preset vars -> runtime vars です。")]
        [SerializeField]
        VarStorePayload _variables = new();

        public CommandListPlaybackMode PlaybackMode => _playbackMode;
        public float IntervalSeconds => _intervalSeconds;
        public bool AutoPlay => _autoPlay;
        public VarStorePayload Variables => _variables;

        internal CommandListPlayerPreset CreateRuntimeCopy()
        {
            return new CommandListPlayerPreset
            {
                _playbackMode = _playbackMode,
                _intervalSeconds = _intervalSeconds,
                _autoPlay = _autoPlay,
                _variables = _variables ?? new VarStorePayload(),
            };
        }
    }

    [Serializable]
    public sealed class CommandListChannelPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Preset")]
        [LabelText("Command List Preset")]
        [Tooltip("再生対象の CommandListPreset source です。runtime command による swap/reset の基準にもなります。")]
        [SerializeField]
        DynamicValue<CommandListPreset> _commandListPreset =
            DynamicValue<CommandListPreset>.FromSource(
                new ManagedRefLiteralSource<CommandListPreset>(new CommandListPreset()));

        [BoxGroup("Preset")]
        [LabelText("Player Preset")]
        [Tooltip("再生方法と base variables を定義する CommandListPlayerPreset source です。")]
        [SerializeField]
        DynamicValue<CommandListPlayerPreset> _playerPreset =
            DynamicValue<CommandListPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<CommandListPlayerPreset>(new CommandListPlayerPreset()));

        public DynamicValue<CommandListPreset> CommandListPresetValue => _commandListPreset;
        public DynamicValue<CommandListPlayerPreset> PlayerPresetValue => _playerPreset;

        public CommandListChannelPreset CreateRuntimeCopy()
        {
            return new CommandListChannelPreset
            {
                _commandListPreset = _commandListPreset,
                _playerPreset = _playerPreset,
            };
        }
    }

    [CreateAssetMenu(menuName = "Game/Commands/Command List Preset", fileName = "CommandListPreset")]
    public sealed class CommandListPresetSO : ScriptableObject, IDynamicValueAsset<CommandListPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        [Tooltip("CommandListChannel 用のコマンド列 preset 本体です。")]
        CommandListPreset? _preset = new();

        public CommandListPreset? Preset
        {
            get
            {
                EnsurePreset();
                return _preset;
            }
        }

        void OnEnable()
        {
            EnsurePreset();
            BindDebugOwner();
        }

        void OnValidate()
        {
            EnsurePreset();
            BindDebugOwner();
        }

        void EnsurePreset()
        {
            if (_preset == null)
                _preset = new CommandListPreset();
        }

        void BindDebugOwner()
        {
            _preset?.BindDebugOwner(this, nameof(_preset) + "." + nameof(CommandListPreset.Commands));
        }
    }

    [CreateAssetMenu(menuName = "Game/Commands/Command List Player Preset", fileName = "CommandListPlayerPreset")]
    public sealed class CommandListPlayerPresetSO : ScriptableObject, IDynamicValueAsset<CommandListPlayerPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        [Tooltip("CommandListChannel の再生挙動と base variables を定義する preset 本体です。")]
        CommandListPlayerPreset? _preset = new();

        public CommandListPlayerPreset? Preset
        {
            get
            {
                EnsurePreset();
                return _preset;
            }
        }

        void OnEnable()
        {
            EnsurePreset();
        }

        void OnValidate()
        {
            EnsurePreset();
        }

        void EnsurePreset()
        {
            if (_preset == null)
                _preset = new CommandListPlayerPreset();
        }
    }

    [CreateAssetMenu(menuName = "Game/Commands/Command List Channel Preset", fileName = "CommandListChannelPreset")]
    public sealed class CommandListChannelPresetSO : ScriptableObject, IDynamicValueAsset<CommandListChannelPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        [Tooltip("CommandListPreset と CommandListPlayerPreset を束ねる top-level preset です。Hub には通常これを渡します。")]
        CommandListChannelPreset? _preset = new();

        public CommandListChannelPreset? Preset
        {
            get
            {
                EnsurePreset();
                return _preset;
            }
        }

        void OnEnable()
        {
            EnsurePreset();
        }

        void OnValidate()
        {
            EnsurePreset();
        }

        void EnsurePreset()
        {
            if (_preset == null)
                _preset = new CommandListChannelPreset();
        }
    }
}
