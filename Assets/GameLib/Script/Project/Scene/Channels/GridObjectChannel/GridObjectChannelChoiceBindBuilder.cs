#nullable enable
using Game.Common;

namespace Game.Channel
{
    internal static class GridObjectChannelChoiceBindBuilder
    {
        public static GridObjectChannelBindRequest Build(GridObjectChoiceRequest request)
        {
            var bindRequest = request.BindRequest?.Clone() ?? new GridObjectChannelBindRequest();
            var countPreset = GridObjectChannelStandalonePlayerPreset.CreateFixedCount(request.Entries?.Count ?? 0);
            bindRequest.OverridePlayerPreset = true;
            bindRequest.PlayerPresetValue = DynamicValue<GridObjectChannelPlayerPresetBase>.FromSource(
                new ManagedRefLiteralSource<GridObjectChannelPlayerPresetBase>(countPreset));
            return bindRequest;
        }
    }
}
