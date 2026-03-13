#nullable enable
using Game.Times;

namespace Game.CameraSystem
{
    public readonly struct CameraSystemOptions
    {
        public readonly string MoveChannelTag;
        public readonly bool RunInLateUpdate;
        public readonly TimeScaleBehavior TimeScaleBehavior;
        public readonly float ZoomMinSize;
        public readonly float ZoomMaxSize;

        public CameraSystemOptions(
            string moveChannelTag,
            bool runInLateUpdate,
            TimeScaleBehavior timeScaleBehavior,
            float zoomMinSize,
            float zoomMaxSize)
        {
            MoveChannelTag = string.IsNullOrEmpty(moveChannelTag) ? "camera" : moveChannelTag;
            RunInLateUpdate = runInLateUpdate;
            TimeScaleBehavior = timeScaleBehavior;
            ZoomMinSize = zoomMinSize;
            ZoomMaxSize = zoomMaxSize;
        }
    }
}
