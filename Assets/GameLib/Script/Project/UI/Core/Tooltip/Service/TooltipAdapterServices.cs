#nullable enable
using System.Collections.Generic;
using Game.Common;
using Game.Commands.VNext;
using Game.DI;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    public abstract class TooltipAdapterServiceBase : ITooltipAdapter, IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly IScopeNode _owner;
        readonly ITooltipAdapterOptions _options;
        ITooltipSystemService? _system;

        protected TooltipAdapterServiceBase(IScopeNode owner, ITooltipAdapterOptions options)
        {
            _owner = owner;
            _options = options;
        }

        public IScopeNode Owner => _owner;
        public abstract TooltipAdapterKind Kind { get; }
        public bool EnablePointerHover => _options.EnablePointerHover;
        public bool EnableSelectionHover => _options.EnableSelectionHover;
        public float HoverDelaySeconds => _options.HoverDelaySeconds;
        public float SelectionDelaySeconds => _options.SelectionDelaySeconds;
        public float PointerMoveThreshold => _options.PointerMoveThreshold;
        public TooltipSpawnMode SpawnMode => _options.SpawnMode;
        public Vector2 FixedOffset => _options.FixedOffset;
        public TooltipAnchorX AnchorX => _options.AnchorX;
        public TooltipAnchorY AnchorY => _options.AnchorY;
        public CommandListData ShowCommands => _options.ShowCommands;
        public CommandListData HideCommands => _options.HideCommands;
        public SelfDespawnCommandData SelfDespawn => _options.SelfDespawn;
        public Camera? UiCamera => _options.UiCamera;
        public Camera? WorldCamera => _options.WorldCamera;
        public IReadOnlyList<RectTransform> HitRects => _options.HitRects;
        public IReadOnlyList<SpriteRenderer> HitSprites => _options.HitSprites;
        public Transform? AnchorTransform => _options.AnchorTransform ?? _owner.Identity?.SelfTransform;
        public int Priority => _options.Priority;
        public string SpawnerTag => _options.SpawnerTag;

        public bool TryResolveRuntimeTemplate(IDynamicContext context, out BaseRuntimeTemplateSO? runtimeTemplate)
        {
            return _options.TryResolveRuntimeTemplate(context, out runtimeTemplate);
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            var resolver = scope?.Resolver;
            if (resolver != null && resolver.TryResolve<ITooltipSystemService>(out var system) && system != null)
            {
                _system = system;
                _system.RegisterAdapter(this);
            }
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (_system != null)
            {
                _system.UnregisterAdapter(this);
                _system = null;
            }
        }
    }

    public sealed class TooltipUIScreenAdapterService : TooltipAdapterServiceBase
    {
        public TooltipUIScreenAdapterService(IScopeNode owner, ITooltipAdapterOptions options)
            : base(owner, options)
        {
        }

        public override TooltipAdapterKind Kind => TooltipAdapterKind.UIScreen;
    }

    public sealed class TooltipWorldAdapterService : TooltipAdapterServiceBase
    {
        public TooltipWorldAdapterService(IScopeNode owner, ITooltipAdapterOptions options)
            : base(owner, options)
        {
        }

        public override TooltipAdapterKind Kind => TooltipAdapterKind.World;
    }
}
