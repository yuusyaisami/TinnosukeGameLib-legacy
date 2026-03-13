#nullable enable
using UnityEngine;

namespace Game.SharedTexture
{
    public interface ISharedTextureChannelHub
    {
        bool Publish(string tag, Texture texture, in SharedTextureDescriptor descriptor, in SharedTexturePublishOptions options);
        bool TryGet(string tag, out SharedTextureFrame frame);
        bool Contains(string tag);
        bool Remove(string tag, string producerTag);
        void ClearByProducer(string producerTag);
        void ClearAll();
        int ChannelCount { get; }
    }
}
