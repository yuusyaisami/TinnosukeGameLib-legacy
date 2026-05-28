#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Game.Input;
using Game.SelectRuntime;
using UnityEngine;
using VContainer.Unity;

namespace Game.UI
{
    public sealed class ButtonChannelHubService :
        IButtonChannelHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IScopeTickHandler
    {
        sealed class ChannelEntry
        {
            public string Tag = "default";
            public ButtonChannelOptions Options = null!;
            public ButtonChannelRuntime Runtime = null!;
        }

        readonly IScopeNode _owner;
        readonly ButtonChannelHubDeclarationMB _mb;
        readonly Dictionary<string, ChannelEntry> _channels = new(StringComparer.Ordinal);
        readonly List<ChannelEntry> _orderedChannels = new();
        readonly IUIElementState? _localElementState;
        readonly IUIInputConsumerHub? _localConsumerHub;
        readonly IUIHandleNode? _localHandleNode;
        readonly IUISelectionState? _localSelectionState;
        readonly IUISelectionBlockService? _localSelectionBlockService;
        readonly IInputRouter? _localInputRouter;
        readonly IVarStore? _localVarStore;
        readonly ICommandRunner? _localCommandRunner;
        readonly IScopeLifecycleService? _localLifecycleService;
        readonly IBlackboardService? _localBlackboard;

        IButtonChannelInteractionAdapter? _adapter;
        IScopeNode? _acquiredScope;
        UISelectionBlockMask _lastBlockMask;
        bool _isAcquired;

        public int ChannelCount => _orderedChannels.Count;

        public ButtonChannelHubService(
            IScopeNode owner,
            ButtonChannelHubDeclarationMB mb,
            IUIElementState? localElementState = null,
            IUIInputConsumerHub? localConsumerHub = null,
            IUIHandleNode? localHandleNode = null,
            IUISelectionState? localSelectionState = null,
            IUISelectionBlockService? localSelectionBlockService = null,
            IInputRouter? localInputRouter = null,
            IVarStore? localVarStore = null,
            ICommandRunner? localCommandRunner = null,
            IScopeLifecycleService? localLifecycleService = null,
            IBlackboardService? localBlackboard = null)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
            _localElementState = localElementState;
            _localConsumerHub = localConsumerHub;
            _localHandleNode = localHandleNode;
            _localSelectionState = localSelectionState;
            _localSelectionBlockService = localSelectionBlockService;
            _localInputRouter = localInputRouter;
            _localVarStore = localVarStore;
            _localCommandRunner = localCommandRunner;
            _localLifecycleService = localLifecycleService;
            _localBlackboard = localBlackboard;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _acquiredScope = scope;
            _isAcquired = true;
            _lastBlockMask = UISelectionBlockMask.None;
            RebuildAdapter(scope, isReset);
            RebuildChannels(scope, isReset);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            ReleaseChannels(scope, isReset);
            ReleaseAdapter(scope, isReset);
            _acquiredScope = null;
            _lastBlockMask = UISelectionBlockMask.None;
            _isAcquired = false;
        }

        public void Tick()
        {
            if (!_isAcquired || _acquiredScope == null)
                return;

            _adapter?.Tick();

            var blockMask = UISelectionBlockMask.None;
            for (var i = 0; i < _orderedChannels.Count; i++)
            {
                var runtime = _orderedChannels[i].Runtime;
                runtime.BindAdapter(_adapter);
                runtime.Tick();
                blockMask |= runtime.DesiredSelectionBlockMask;
            }

            if (_lastBlockMask == blockMask)
                return;

            _lastBlockMask = blockMask;
            _adapter?.SetBlockMask(blockMask);
        }

        public bool Contains(string tag)
        {
            return _channels.ContainsKey(NormalizeTag(tag));
        }

        public bool TryGetOutput(string tag, out IButtonChannelOutput? output)
        {
            output = null;
            if (!_channels.TryGetValue(NormalizeTag(tag), out var entry))
                return false;

            output = entry.Runtime;
            return true;
        }

        public bool TryGetControl(string tag, out IButtonChannelControlService? control)
        {
            control = null;
            if (!_channels.TryGetValue(NormalizeTag(tag), out var entry))
                return false;

            control = entry.Runtime;
            return true;
        }

        public bool RegisterOrReplace(string tag, ButtonChannelPreset preset)
        {
            if (preset == null)
                return false;

            var normalizedTag = NormalizeTag(tag);
            var options = new ButtonChannelOptions
            {
                OwnerTransform = _mb.transform,
                PresetValue = DynamicValue<ButtonChannelPreset>.FromSource(
                    new ManagedRefLiteralSource<ButtonChannelPreset>(preset.CreateRuntimeCopy())),
            };

            if (_channels.TryGetValue(normalizedTag, out var existing))
            {
                if (_isAcquired && _acquiredScope != null)
                    existing.Runtime.OnRelease(_acquiredScope, false);

                existing.Options = options;
                existing.Runtime = new ButtonChannelRuntime(_owner, normalizedTag, options);
                if (_isAcquired && _acquiredScope != null)
                    existing.Runtime.OnAcquire(_acquiredScope, false, _adapter, ResolveRuntimeServices(_acquiredScope));
                return true;
            }

            var entry = new ChannelEntry
            {
                Tag = normalizedTag,
                Options = options,
                Runtime = new ButtonChannelRuntime(_owner, normalizedTag, options),
            };

            _channels.Add(normalizedTag, entry);
            _orderedChannels.Add(entry);
            if (_isAcquired && _acquiredScope != null)
                entry.Runtime.OnAcquire(_acquiredScope, false, _adapter, ResolveRuntimeServices(_acquiredScope));
            return true;
        }

        public bool Unregister(string tag)
        {
            var normalizedTag = NormalizeTag(tag);
            if (!_channels.TryGetValue(normalizedTag, out var entry))
                return false;

            if (_isAcquired && _acquiredScope != null)
                entry.Runtime.OnRelease(_acquiredScope, false);

            _channels.Remove(normalizedTag);
            _orderedChannels.Remove(entry);
            return true;
        }

        public void GetTags(List<string> output)
        {
            if (output == null)
                return;

            output.Clear();
            for (var i = 0; i < _orderedChannels.Count; i++)
                output.Add(_orderedChannels[i].Tag);
        }

        void RebuildChannels(IScopeNode scope, bool isReset)
        {
            ReleaseChannels(scope, isReset);
            var runtimeServices = ResolveRuntimeServices(scope);

            var definitions = _mb.Channels;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                    continue;

                var tag = NormalizeTag(definition.ChannelTag);
                if (_channels.ContainsKey(tag))
                {
                    Debug.LogWarning($"[ButtonChannelHub] Duplicate channel tag '{tag}' was skipped.");
                    continue;
                }

                var options = definition.CreateOptions(_mb.transform);
                var runtime = new ButtonChannelRuntime(_owner, tag, options);
                runtime.OnAcquire(scope, isReset, _adapter, runtimeServices);

                var entry = new ChannelEntry
                {
                    Tag = tag,
                    Options = options,
                    Runtime = runtime,
                };

                _channels.Add(tag, entry);
                _orderedChannels.Add(entry);
            }
        }

        void ReleaseChannels(IScopeNode scope, bool isReset)
        {
            for (var i = _orderedChannels.Count - 1; i >= 0; i--)
                _orderedChannels[i].Runtime.OnRelease(scope, isReset);

            _channels.Clear();
            _orderedChannels.Clear();
        }

        void RebuildAdapter(IScopeNode scope, bool isReset)
        {
            ReleaseAdapter(scope, isReset);
            _adapter = CreateAdapter(scope);
            _adapter?.OnAcquire(scope, isReset);
        }

        void ReleaseAdapter(IScopeNode scope, bool isReset)
        {
            if (_adapter == null)
                return;

            _adapter.OnRelease(scope, isReset);
            _adapter.Dispose();
            _adapter = null;
        }

        IButtonChannelInteractionAdapter? CreateAdapter(IScopeNode scope)
        {
            return _mb.BindingMode switch
            {
                ButtonChannelBindingMode.UI when TryCreateUIAdapter(scope, out var uiAdapter) => uiAdapter,
                ButtonChannelBindingMode.World when TryCreateWorldAdapter(out var worldAdapter) => worldAdapter,
                ButtonChannelBindingMode.GameRoot when TryCreateGameRootAdapter(scope, out var gameRootAdapter) => gameRootAdapter,
                _ => null,
            };
        }

        bool TryCreateUIAdapter(IScopeNode scope, out IButtonChannelInteractionAdapter? adapter)
        {
            adapter = null;

            var consumerHub = _localConsumerHub;
            if (consumerHub == null && !ButtonChannelBindingResolver.TryResolveFromScope(scope, out consumerHub))
                return false;

            var elementState = _localElementState;
            if (elementState == null && !ButtonChannelBindingResolver.TryResolveFromScope(scope, out elementState))
                return false;

            var handleNode = _localHandleNode;
            if (handleNode == null && !ButtonChannelBindingResolver.TryResolveFromScope(scope, out handleNode))
                handleNode = elementState as IUIHandleNode;

            if (handleNode == null || !handleNode.NodeHandle.IsValid)
                return false;

            var selectionState = _localSelectionState;
            if (selectionState == null && !ButtonChannelBindingResolver.TryResolveFromAnchor(_mb.UISelectionSource, out selectionState))
                ButtonChannelBindingResolver.TryResolveFromScope(scope, out selectionState);

            if (selectionState == null)
                return false;

            var selectionBlockService = _localSelectionBlockService;
            if (selectionBlockService == null && !ButtonChannelBindingResolver.TryResolveFromAnchor(_mb.UISelectionSource, out selectionBlockService))
                ButtonChannelBindingResolver.TryResolveFromScope(scope, out selectionBlockService);

            adapter = new UIButtonChannelInteractionAdapter(
                consumerHub,
                handleNode.NodeHandle,
                elementState,
                selectionState,
                selectionBlockService,
                DispatchSignal);
            return true;
        }

        bool TryCreateWorldAdapter(out IButtonChannelInteractionAdapter? adapter)
        {
            adapter = null;

            var pointerTarget = _mb.WorldPointerTarget;
            var managerSource = _mb.WorldManagerSource;
            if (pointerTarget == null || managerSource == null)
                return false;

            if (!SelectRuntimeBridgeResolver.TryResolvePointerService(managerSource, out var pointerService) || pointerService == null)
                return false;

            SelectRuntimeBridgeResolver.TryResolveManagerService(managerSource, out var selectService);
            adapter = new WorldButtonChannelInteractionAdapter(pointerTarget, _mb.WorldSelectable, pointerService, selectService, DispatchSignal);
            return true;
        }

        bool TryCreateGameRootAdapter(IScopeNode scope, out IButtonChannelInteractionAdapter? adapter)
        {
            adapter = null;

            var inputRouter = _localInputRouter;
            if (inputRouter == null && !ButtonChannelBindingResolver.TryResolveFromAnchor(_mb.GameRootInputSource, out inputRouter))
                ButtonChannelBindingResolver.TryResolveFromScope(scope, out inputRouter);

            if (inputRouter == null)
                return false;

            adapter = new GameRootButtonChannelInteractionAdapter(inputRouter, DispatchSignal);
            return true;
        }

        ButtonChannelRuntimeServices ResolveRuntimeServices(IScopeNode scope)
        {
            var lifecycleService = _localLifecycleService;
            if (lifecycleService == null)
                ButtonChannelBindingResolver.TryResolveFromScope(scope, out lifecycleService);

            var vars = _localVarStore;
            if (vars == null && _localBlackboard != null)
                vars = _localBlackboard.LocalVars;
            if (vars == null && !ButtonChannelBindingResolver.TryResolveFromScope(scope, out vars))
            {
                if (ButtonChannelBindingResolver.TryResolveFromScope(scope, out IBlackboardService? blackboard) && blackboard != null)
                    vars = blackboard.LocalVars;
            }

            var commandRunner = _localCommandRunner;
            if (commandRunner == null)
                ButtonChannelBindingResolver.TryResolveFromScope(scope, out commandRunner);

            return new ButtonChannelRuntimeServices(lifecycleService, vars ?? NullVarStore.Instance, commandRunner);
        }

        bool DispatchSignal(ButtonChannelInteractionSignal signal)
        {
            var handled = false;
            for (var i = 0; i < _orderedChannels.Count; i++)
            {
                if (_orderedChannels[i].Runtime.HandleSignal(signal))
                    handled = true;
            }

            return handled;
        }

        static string NormalizeTag(string? tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
        }
    }
}
