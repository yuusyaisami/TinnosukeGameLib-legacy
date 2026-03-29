#nullable enable
using Game.Common;
using UnityEngine;

namespace Game.UI
{
    public sealed class TooltipSystemConfig
    {
        public RectTransform TooltipRoot = null!;
        public Transform? WorldRoot;
        public RectTransform? ClampArea;
        public TooltipChannelInputMode InputMode = TooltipChannelInputMode.AutoByInputService;
        public TooltipClampSettings ClampSettings = TooltipClampSettings.Default;
        public int SpawnWarmupFrames = 2;
        public TooltipSystemSharedDefaults SharedDefaults = new();
    }

    public sealed class TooltipSystemService :
        ITooltipSystemService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly TooltipSystemConfig _config;

        public TooltipSystemService(TooltipSystemConfig config)
        {
            _config = config;
        }

        public RectTransform TooltipRoot => _config.TooltipRoot;
        public Transform? WorldRoot => _config.WorldRoot;
        public RectTransform? ClampArea => _config.ClampArea;
        public TooltipChannelInputMode InputMode => _config.InputMode;
        public TooltipClampSettings ClampSettings => _config.ClampSettings;
        public int SpawnWarmupFrames => _config.SpawnWarmupFrames;
        public TooltipSystemSharedDefaults SharedDefaults => _config.SharedDefaults;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
        }
    }
}
