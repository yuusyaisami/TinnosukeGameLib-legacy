#nullable enable

using System.Collections.Generic;
using Game.MaterialFx;

namespace Game.Visual
{
    /// <summary>
    /// VisualSystem へ登録される Hub の最小インターフェース。
    /// Player へ直接触らず、HubState/Broadcast API だけを公開する。
    /// </summary>
    public interface IVisualHub
    {
        VisualHubKind Kind { get; }
        string HubTag { get; }
        int HubInstanceId { get; }

        void SetHubState(IReadOnlyList<MaterialFxPresetEntry> entries, bool clearMissingKeys = true, int basePriority = 0);
        void BroadcastMaterialFx(IReadOnlyList<MaterialFxPresetEntry> entries, int basePriority = 0);
    }
}
