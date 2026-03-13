#nullable enable

using System.Collections.Generic;
using Game.MaterialFx;

namespace Game.Visual
{
    public interface IVisualSystem
    {
        void RegisterHub(IVisualHub hub);
        void UnregisterHub(IVisualHub hub);

        void SetState(
            VisualTargetSelector selector,
            IReadOnlyList<MaterialFxPresetEntry> entries,
            bool clearMissingKeys = true,
            int basePriority = 0);

        void Broadcast(
            VisualTargetSelector selector,
            IReadOnlyList<MaterialFxPresetEntry> entries,
            int basePriority = 0);
    }
}
