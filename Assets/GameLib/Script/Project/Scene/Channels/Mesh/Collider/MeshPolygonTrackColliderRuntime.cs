#nullable enable

namespace Game.Channel
{
    sealed class MeshPolygonTrackColliderRuntime : IMeshTrackColliderRuntime
    {
        public MeshPolygonTrackColliderPreset? Preset { get; }

        public MeshPolygonTrackColliderRuntime(MeshPolygonTrackColliderPreset preset)
        {
            Preset = preset;
        }
    }
}
