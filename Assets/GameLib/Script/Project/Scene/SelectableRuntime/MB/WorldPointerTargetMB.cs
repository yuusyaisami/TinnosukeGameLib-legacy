#nullable enable
using System.Collections.Generic;
using Game;
using Game.Commands;
using Game.Common;
using Game.Commands.VNext;
using Game.Vars.Generated;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.SelectRuntime
{
    [DisallowMultipleComponent]
    public sealed class WorldPointerTargetMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Target")]
        [LabelText("Colliders")]
        [SerializeField]
        List<Collider2D> _colliders = new();

        [BoxGroup("Target")]
        [LabelText("Use Child Colliders")]
        [SerializeField]
        bool _useChildColliders = true;

        [BoxGroup("Pointer")]
        [LabelText("Short Press Seconds")]
        [MinValue(0.05f)]
        [SerializeField]
        float _shortPressSeconds = 0.15f;

        [BoxGroup("Pointer")]
        [LabelText("Long Press Seconds")]
        [MinValue(0.05f)]
        [SerializeField]
        float _longPressSeconds = 0.35f;

        [BoxGroup("Commands")]
        [LabelText("Left Click Commands")]
        [SerializeField]
        CommandListData _onLeftClickedCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Right Click Commands")]
        [SerializeField]
        CommandListData _onRightClickedCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Left Short Press Start Commands")]
        [SerializeField]
        CommandListData _onLeftShortPressStartedCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Left Short Press End Commands")]
        [SerializeField]
        CommandListData _onLeftShortPressEndedCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Right Short Press Start Commands")]
        [SerializeField]
        CommandListData _onRightShortPressStartedCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Right Short Press End Commands")]
        [SerializeField]
        CommandListData _onRightShortPressEndedCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Left Long Press Start Commands")]
        [SerializeField]
        CommandListData _onLeftLongPressStartedCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Left Long Press End Commands")]
        [SerializeField]
        CommandListData _onLeftLongPressEndedCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Right Long Press Start Commands")]
        [SerializeField]
        CommandListData _onRightLongPressStartedCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Right Long Press End Commands")]
        [SerializeField]
        CommandListData _onRightLongPressEndedCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Hover Enter Commands")]
        [SerializeField]
        CommandListData _onHoverEnteredCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Hover Exit Commands")]
        [SerializeField]
        CommandListData _onHoverExitedCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Only Execute When Self Clicked")]
        [SerializeField]
        bool _onlyExecuteWhenSelfClicked = true;

        [BoxGroup("Commands")]
        [LabelText("Command Overlap Behavior")]
        [Tooltip("Inspector setting.")]
        [EnumToggleButtons]
        [SerializeField]
        ExecutionBehavior _commandOverlapBehavior = ExecutionBehavior.AllowConcurrent;

        [BoxGroup("Variables")]
        [InlineProperty]
        [HideLabel]
        [LabelText("Self Click Key")]
        [SerializeField]
        VarKeyRef _pointerSelfResultKey = new(VarIds.GameLib.Base.PointerRelation.isSelf, "islSelf");

        [BoxGroup("Variables")]
        [InlineProperty]
        [HideLabel]
        [LabelText("Self Or Descendant Key")]
        [SerializeField]
        VarKeyRef _pointerSelfOrDescendantResultKey = new(VarIds.GameLib.Base.PointerRelation.isSelfOrDescendant, "GameLib.Base.PointerRelation.isSelfOrDescendant");

        [BoxGroup("Variables")]
        [InlineProperty]
        [HideLabel]
        [LabelText("Hover State Key")]
        [SerializeField]
        VarKeyRef _hoverStateKey = new(VarIds.GameLib.Base.PointerRelation.isHovered, "GameLib.Base.PointerRelation.isHovered");

        public CommandListData OnLeftClickedCommands => _onLeftClickedCommands;
        public CommandListData OnRightClickedCommands => _onRightClickedCommands;
        public CommandListData OnLeftShortPressStartedCommands => _onLeftShortPressStartedCommands;
        public CommandListData OnLeftShortPressEndedCommands => _onLeftShortPressEndedCommands;
        public CommandListData OnRightShortPressStartedCommands => _onRightShortPressStartedCommands;
        public CommandListData OnRightShortPressEndedCommands => _onRightShortPressEndedCommands;
        public CommandListData OnLeftLongPressStartedCommands => _onLeftLongPressStartedCommands;
        public CommandListData OnLeftLongPressEndedCommands => _onLeftLongPressEndedCommands;
        public CommandListData OnRightLongPressStartedCommands => _onRightLongPressStartedCommands;
        public CommandListData OnRightLongPressEndedCommands => _onRightLongPressEndedCommands;
        public CommandListData OnHoverEnteredCommands => _onHoverEnteredCommands;
        public CommandListData OnHoverExitedCommands => _onHoverExitedCommands;
        public bool OnlyExecuteWhenSelfClicked => _onlyExecuteWhenSelfClicked;
        public ExecutionBehavior CommandOverlapBehavior => _commandOverlapBehavior;
        public VarKeyRef PointerSelfResultKey => _pointerSelfResultKey;
        public VarKeyRef PointerSelfOrDescendantResultKey => _pointerSelfOrDescendantResultKey;
        public VarKeyRef HoverStateKey => _hoverStateKey;
        public float ShortPressSeconds => Mathf.Max(0.05f, _shortPressSeconds);
        public float LongPressSeconds => Mathf.Max(0.05f, _longPressSeconds);

        public IReadOnlyList<Collider2D> ResolveColliders()
        {
            if (_colliders != null && _colliders.Count > 0)
                return _colliders;

            if (!_useChildColliders)
                return System.Array.Empty<Collider2D>();

            return GetComponentsInChildren<Collider2D>(true);
        }

        void OnEnable()
        {
            BindDebugOwners();
            NotifyBridgeRefresh();
        }

        void OnDisable()
        {
            NotifyBridgeRelease();
        }

        void OnTransformParentChanged()
        {
            NotifyBridgeRefresh();
        }

        void OnValidate()
        {
            BindDebugOwners();
        }

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<WorldPointerTargetBridgeService>(RuntimeLifetime.Singleton)
                .AsSelf()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .WithParameter(this);

            builder.RegisterBuildCallback(resolver =>
            {
                if (resolver.TryResolve<WorldPointerTargetBridgeService>(out var bridge) && bridge != null)
                    bridge.RefreshBinding();
            });
        }

        void BindDebugOwners()
        {
            _onLeftClickedCommands?.BindDebugOwner(this, nameof(_onLeftClickedCommands));
            _onRightClickedCommands?.BindDebugOwner(this, nameof(_onRightClickedCommands));
            _onLeftShortPressStartedCommands?.BindDebugOwner(this, nameof(_onLeftShortPressStartedCommands));
            _onLeftShortPressEndedCommands?.BindDebugOwner(this, nameof(_onLeftShortPressEndedCommands));
            _onRightShortPressStartedCommands?.BindDebugOwner(this, nameof(_onRightShortPressStartedCommands));
            _onRightShortPressEndedCommands?.BindDebugOwner(this, nameof(_onRightShortPressEndedCommands));
            _onLeftLongPressStartedCommands?.BindDebugOwner(this, nameof(_onLeftLongPressStartedCommands));
            _onLeftLongPressEndedCommands?.BindDebugOwner(this, nameof(_onLeftLongPressEndedCommands));
            _onRightLongPressStartedCommands?.BindDebugOwner(this, nameof(_onRightLongPressStartedCommands));
            _onRightLongPressEndedCommands?.BindDebugOwner(this, nameof(_onRightLongPressEndedCommands));
            _onHoverEnteredCommands?.BindDebugOwner(this, nameof(_onHoverEnteredCommands));
            _onHoverExitedCommands?.BindDebugOwner(this, nameof(_onHoverExitedCommands));
        }

        void NotifyBridgeRefresh()
        {
            if (!ScopeFeatureInstallerUtility.TryGetNearestScopeNode(this, includeInactive: true, out var scope) ||
                scope?.Resolver == null)
                return;

            if (scope.Resolver.TryResolve<WorldPointerTargetBridgeService>(out var bridge) && bridge != null)
                bridge.RefreshBinding();
        }

        void NotifyBridgeRelease()
        {
            if (!ScopeFeatureInstallerUtility.TryGetNearestScopeNode(this, includeInactive: true, out var scope) ||
                scope?.Resolver == null)
                return;

            if (scope.Resolver.TryResolve<WorldPointerTargetBridgeService>(out var bridge) && bridge != null)
                bridge.ReleaseBinding();
        }
    }
}
