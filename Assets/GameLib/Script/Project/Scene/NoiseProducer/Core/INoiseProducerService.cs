#nullable enable

namespace Game.NoiseProducer
{
    public interface INoiseProducerService
    {
        bool ContainsChannel(string channelId);
        bool TryGetChannelState(string channelId, out NoiseChannelState state);
        bool RegisterChannel(string channelId, NoiseChannelDefinition definition);
        bool UnregisterChannel(string channelId);
        bool TryWriteParameter(in NoiseParameterWriteRequest request);
        bool ClearParameterLayer(in NoiseParameterAddress address);
        bool RequestRefresh(string channelId, NoiseChannelRefreshFlags flags);
    }
}
