using UnityEngine;

namespace Game.Layout
{
    public sealed class LayoutSystemConfig
    {
        public RectTransform LayoutElementsRoot;
        public RectTransform BackgroundRect;
        public LayoutBackgroundOptions BackgroundOptions = LayoutBackgroundOptions.Default;
        public bool ForceUnityLayoutRebuildOnRebuild;
        public bool ExcludeInactive = true;
        public bool HideBackgroundWhenEmpty = true;
        public bool RunInLateUpdate = true;
    }
}
