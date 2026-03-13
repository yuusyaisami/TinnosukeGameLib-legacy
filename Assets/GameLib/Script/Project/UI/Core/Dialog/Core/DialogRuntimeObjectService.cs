#nullable enable

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    public readonly struct UIDialogRuntimeContext
    {
        public Game.IScopeNode DialogScope { get; }
        public Game.IScopeNode Owner { get; }
        public string ChannelKey { get; }
        public DialogRuntimeOptions Options { get; }

        public UIDialogRuntimeContext(
            Game.IScopeNode dialogScope,
            Game.IScopeNode owner,
            string channelKey,
            DialogRuntimeOptions options)
        {
            DialogScope = dialogScope;
            Owner = owner;
            ChannelKey = channelKey ?? string.Empty;
            Options = options ?? new DialogRuntimeOptions();
        }
    }

    public interface IUIDialogRuntimeService
    {
        void OnShow(in UIDialogRuntimeContext context);
        void OnHide(in UIDialogRuntimeContext context, DialogCloseReason reason);
    }

    public enum DialogFocusTargetMode
    {
        Self = 0,
        FirstSelectableInChildren = 1,
        Explicit = 2,
    }

    [Serializable]
    public sealed class UIDialogObjectOptions
    {
        [BoxGroup("Focus")]
        [LabelText("Enable")]
        public bool EnableFocus = true;

        [BoxGroup("Focus")]
        [ShowIf(nameof(EnableFocus))]
        [LabelText("Target")]
        public DialogFocusTargetMode FocusTargetMode = DialogFocusTargetMode.FirstSelectableInChildren;

        [BoxGroup("Focus")]
        [ShowIf("@EnableFocus && FocusTargetMode == DialogFocusTargetMode.Explicit")]
        [LabelText("Explicit Target")]
        public UIElementStateMB? ExplicitTarget;

        [BoxGroup("Focus")]
        [ShowIf("@EnableFocus && FocusTargetMode == DialogFocusTargetMode.FirstSelectableInChildren")]
        [LabelText("Include Inactive")]
        public bool IncludeInactive = true;
    }

    public sealed class DialogRuntimeObjectService : IUIDialogRuntimeService
    {
        readonly Game.IScopeNode _scope;
        readonly UIDialogObjectOptions _options;
        readonly IUISelectionService? _selection;

        public DialogRuntimeObjectService(
            Game.IScopeNode scope,
            UIDialogObjectOptions options,
            IUISelectionService? selection = null)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _options = options ?? new UIDialogObjectOptions();
            _selection = selection;
        }

        public void OnShow(in UIDialogRuntimeContext context)
        {
            if (!context.Options.FocusOnShow)
                return;

            if (!_options.EnableFocus)
                return;

            if (_selection == null)
                return;

            var target = ResolveFocusTarget();
            if (target == null)
                return;

            _selection.TrySelect(target);
        }

        public void OnHide(in UIDialogRuntimeContext context, DialogCloseReason reason)
        {
        }

        Game.IScopeNode? ResolveFocusTarget()
        {
            switch (_options.FocusTargetMode)
            {
                case DialogFocusTargetMode.Self:
                    return _scope;
                case DialogFocusTargetMode.Explicit:
                    return ResolveExplicitTarget();
                case DialogFocusTargetMode.FirstSelectableInChildren:
                    return ResolveFirstSelectableInChildren();
                default:
                    return null;
            }
        }

        Game.IScopeNode? ResolveExplicitTarget()
        {
            if (_options.ExplicitTarget == null)
                return null;

            return _options.ExplicitTarget.GetComponent<Game.IScopeNode>();
        }

        Game.IScopeNode? ResolveFirstSelectableInChildren()
        {
            var root = _scope.Identity?.SelfTransform;
            if (root == null)
                return null;

            var states = root.GetComponentsInChildren<UIElementStateMB>(includeInactive: _options.IncludeInactive);
            if (states == null || states.Length == 0)
                return null;

            Game.IScopeNode? best = null;
            var bestOrder = int.MaxValue;

            for (int i = 0; i < states.Length; i++)
            {
                var stMb = states[i];
                if (stMb == null)
                    continue;

                // UIElementStateService を取得
                var stService = stMb.GetComponent<UIElementStateService>();
                if (stService == null)
                    continue;

                if (!stService.EvaluateIsNavigationSelectable())
                    continue;

                var node = stMb.GetComponent<Game.IScopeNode>();
                if (node == null)
                    continue;

                if (_selection != null && !_selection.CanSelect(node))
                    continue;

                if (stService.SelectionOrder < bestOrder)
                {
                    bestOrder = stService.SelectionOrder;
                    best = node;
                }
            }

            return best;
        }
    }
}

