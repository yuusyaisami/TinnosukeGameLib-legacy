// Game.StateMachine.StateMachineMB.cs

using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.StateMachine
{
    /// <summary>
    /// StateMachine の FeatureInstaller + Debug Viewer。
    /// LifetimeScope 配下に配置して使用する。
    /// </summary>
    public sealed class StateMachineMB : MonoBehaviour, IFeatureInstaller
    {
        // ════════════════════════════════════════════════════════════════
        //  Inspector Fields
        // ════════════════════════════════════════════════════════════════

        [Header("Profile")]
        [Tooltip("StateMachinePreset。Inline か Asset のどちらでも指定できます。")]
        [SerializeField, InlineProperty, HideLabel]
        DynamicValue<StateMachinePreset> _preset;

        [SerializeField, HideInInspector]
        StateMachineProfileSO _profile;

        // ════════════════════════════════════════════════════════════════
        //  Runtime References
        // ════════════════════════════════════════════════════════════════

        StateMachineService _serviceRef;
        uint _lastDebugRevision;

        // ════════════════════════════════════════════════════════════════
        //  Debug View - Current State
        // ════════════════════════════════════════════════════════════════

        [FoldoutGroup("Debug View")]
        [Title("Current State")]
        [ShowInInspector, ReadOnly]
        [LabelText("Current State")]
        [InfoBox("全 Layer を通じた最終選択 State", InfoMessageType.None)]
        string DebugCurrentState => _serviceRef?.CurrentState ?? "(none)";

        [FoldoutGroup("Debug View")]
        [ShowInInspector, ReadOnly]
        [LabelText("Current Layer")]
        string DebugCurrentLayer => _serviceRef?.CurrentLayer ?? "(none)";

        [FoldoutGroup("Debug View")]
        [ShowInInspector, ReadOnly]
        [LabelText("Machine Revision")]
        uint DebugMachineRevision => _serviceRef?.MachineRevision ?? 0;

        // ════════════════════════════════════════════════════════════════
        //  Debug View - Global Options
        // ════════════════════════════════════════════════════════════════

        [FoldoutGroup("Debug View/Global Options")]
        [ShowInInspector, ReadOnly]
        [TableList(AlwaysExpanded = true, IsReadOnly = true)]
        List<GlobalOptionDebugEntry> _globalOptionsDebug = new();

        [Serializable]
        public class GlobalOptionDebugEntry
        {
            [TableColumnWidth(200)]
            [LabelText("Option Key")]
            public string OptionKey = "";

            [TableColumnWidth(200)]
            [LabelText("Value")]
            public string Value = "";
        }

        // ════════════════════════════════════════════════════════════════
        //  Debug View - Layers & States
        // ════════════════════════════════════════════════════════════════

        [FoldoutGroup("Debug View/Layers & States")]
        [ShowInInspector, ReadOnly]
        [ListDrawerSettings(ShowFoldout = true, ShowPaging = false)]
        List<LayerDebugEntry> _layersDebug = new();

        [Serializable]
        public class LayerDebugEntry
        {
            [FoldoutGroup("$LayerKey")]
            [LabelText("Layer Key")]
            public string LayerKey = "";

            [FoldoutGroup("$LayerKey")]
            [LabelText("Priority")]
            public int Priority;

            [FoldoutGroup("$LayerKey")]
            [LabelText("Is Active")]
            public bool IsActive;

            [FoldoutGroup("$LayerKey")]
            [LabelText("Selected State")]
            public string SelectedState = "";

            [FoldoutGroup("$LayerKey/States")]
            [TableList(AlwaysExpanded = true, IsReadOnly = true)]
            public List<StateDebugEntry> States = new();

            [FoldoutGroup("$LayerKey/Local Options")]
            [TableList(AlwaysExpanded = true, IsReadOnly = true)]
            public List<LocalOptionDebugEntry> LocalOptions = new();
        }

        [Serializable]
        public class StateDebugEntry
        {
            [TableColumnWidth(200)]
            [LabelText("State Key")]
            public string StateKey = "";

            [TableColumnWidth(80)]
            [LabelText("Priority")]
            public int Priority;

            [TableColumnWidth(80)]
            [LabelText("Active")]
            public bool IsActive;

            [TableColumnWidth(80)]
            [LabelText("Tokens")]
            public int TokenCount;

            [TableColumnWidth(80)]
            [LabelText("Pulse")]
            public uint PulseCount;
        }

        [Serializable]
        public class LocalOptionDebugEntry
        {
            [TableColumnWidth(200)]
            [LabelText("Option Key")]
            public string OptionKey = "";

            [TableColumnWidth(200)]
            [LabelText("Value")]
            public string Value = "";
        }

        // ════════════════════════════════════════════════════════════════
        //  IFeatureInstaller
        // ════════════════════════════════════════════════════════════════

        public void InstallFeature(IContainerBuilder builder, IScopeNode baseLTS)
        {
            EnsurePresetMigrated();
            var dynamicContext = new SimpleDynamicContext(NullVarStore.Instance, baseLTS);
            var preset = _preset.GetOrDefault(dynamicContext, null);

            // StateMachineService 登録
            builder.Register<StateMachineService>(Lifetime.Singleton)
                .WithParameter("profile", preset)
                .AsSelf()
                .As<IStateMachine>()
                .As<IStateMachineReadOnly>();

            // Debug View 用に参照を取得
            builder.RegisterBuildCallback(container =>
            {
                _serviceRef = container.Resolve<StateMachineService>();
            });
        }

        // ════════════════════════════════════════════════════════════════
        //  Public API - Profile Hot-Swap
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Profile を動的に差し替える。
        /// </summary>
        /// <param name="profile">新しいプロファイル</param>
        /// <param name="applyGlobalDefaults">GlobalOptionDefaults を適用するか</param>
        /// <param name="overwriteExistingGlobals">既存の GlobalOption を上書きするか</param>
        public void SetProfile(StateMachineProfileSO profile, bool applyGlobalDefaults = true, bool overwriteExistingGlobals = false)
        {
            _profile = profile;
            EnsurePresetMigrated();
            SetProfile(profile != null ? profile.Preset : null, applyGlobalDefaults, overwriteExistingGlobals);
        }

        public void SetProfile(StateMachinePreset profile, bool applyGlobalDefaults = true, bool overwriteExistingGlobals = false)
        {
            _preset = profile != null
                ? DynamicValue<StateMachinePreset>.FromSource(new LiteralStateMachinePresetSource(profile))
                : default;

            if (_serviceRef != null)
            {
                _serviceRef.SetProfile(profile, applyGlobalDefaults, overwriteExistingGlobals);
            }
        }

        void OnValidate()
        {
            EnsurePresetMigrated();
        }

        void EnsurePresetMigrated()
        {
            if (_preset.HasSource || _profile == null)
                return;

            _preset = DynamicValue<StateMachinePreset>.FromSource(AssetStateMachinePresetSource.FromAsset(_profile));
        }

        // ════════════════════════════════════════════════════════════════
        //  Debug View Update
        // ════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
        void Update()
        {
            // Editor でのみ Debug View を更新
            if (_serviceRef == null)
                return;

            // Revision が変化した場合のみ更新（パフォーマンス考慮）
            var currentRevision = _serviceRef.MachineRevision;
            if (currentRevision == _lastDebugRevision)
                return;

            _lastDebugRevision = currentRevision;
            RefreshDebugView();
        }

        void RefreshDebugView()
        {
            if (_serviceRef == null)
                return;

            // Global Options 更新
            RefreshGlobalOptionsDebug();

            // Layers & States 更新
            RefreshLayersDebug();
        }

        void RefreshGlobalOptionsDebug()
        {
            _globalOptionsDebug.Clear();

            // Service の内部状態にアクセスするため、Reflection を使用
            // （パフォーマンスよりデバッグのしやすさを優先）
            var globalOptionsField = typeof(StateMachineService)
                .GetField("_globalOptions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (globalOptionsField?.GetValue(_serviceRef) is Dictionary<string, string> globalOptions)
            {
                foreach (var kvp in globalOptions)
                {
                    _globalOptionsDebug.Add(new GlobalOptionDebugEntry
                    {
                        OptionKey = kvp.Key,
                        Value = kvp.Value
                    });
                }
            }
        }

        void RefreshLayersDebug()
        {
            _layersDebug.Clear();

            // Service の内部状態にアクセス
            var layersField = typeof(StateMachineService)
                .GetField("_layers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (layersField?.GetValue(_serviceRef) is not System.Collections.IDictionary layers)
                return;

            foreach (System.Collections.DictionaryEntry layerEntry in layers)
            {
                var layerRuntime = layerEntry.Value;
                var layerType = layerRuntime.GetType();

                var layerKey = (string)layerType.GetField("LayerKey")?.GetValue(layerRuntime);
                var priority = (int)(layerType.GetField("Priority")?.GetValue(layerRuntime) ?? 0);

                var layerDebug = new LayerDebugEntry
                {
                    LayerKey = layerKey ?? "(unknown)",
                    Priority = priority
                };

                // HasActiveState
                var hasActiveStateMethod = layerType.GetMethod("HasActiveState");
                layerDebug.IsActive = hasActiveStateMethod != null && (bool)hasActiveStateMethod.Invoke(layerRuntime, null);

                // GetSelectedState
                var getSelectedStateMethod = layerType.GetMethod("GetSelectedState");
                var selectedState = getSelectedStateMethod?.Invoke(layerRuntime, null);
                if (selectedState != null)
                {
                    var stateType = selectedState.GetType();
                    layerDebug.SelectedState = (string)stateType.GetField("StateKey")?.GetValue(selectedState) ?? "";
                }

                // States
                var statesField = layerType.GetField("States");
                if (statesField?.GetValue(layerRuntime) is System.Collections.IDictionary states)
                {
                    foreach (System.Collections.DictionaryEntry stateEntry in states)
                    {
                        var stateRuntime = stateEntry.Value;
                        var stateType = stateRuntime.GetType();

                        var stateKey = (string)stateType.GetField("StateKey")?.GetValue(stateRuntime);
                        var statePriority = (int)(stateType.GetField("Priority")?.GetValue(stateRuntime) ?? 0);
                        var tokens = stateType.GetField("Tokens")?.GetValue(stateRuntime) as System.Collections.IList;
                        var pulseCount = (uint)(stateType.GetField("PulseCount")?.GetValue(stateRuntime) ?? 0u);

                        layerDebug.States.Add(new StateDebugEntry
                        {
                            StateKey = stateKey ?? "(unknown)",
                            Priority = statePriority,
                            IsActive = tokens != null && tokens.Count > 0,
                            TokenCount = tokens?.Count ?? 0,
                            PulseCount = pulseCount
                        });
                    }
                }

                // LocalOptions
                var localOptionsField = layerType.GetField("LocalOptions");
                if (localOptionsField?.GetValue(layerRuntime) is Dictionary<string, string> localOptions)
                {
                    foreach (var kvp in localOptions)
                    {
                        layerDebug.LocalOptions.Add(new LocalOptionDebugEntry
                        {
                            OptionKey = kvp.Key,
                            Value = kvp.Value
                        });
                    }
                }

                _layersDebug.Add(layerDebug);
            }
        }
#endif
    }
}
