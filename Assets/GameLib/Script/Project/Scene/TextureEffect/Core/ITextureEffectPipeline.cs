#nullable enable

namespace Game.TextureEffect
{
    public interface ITextureEffectPipeline
    {
        void Process();
        int LayerCount { get; }
    }

    public interface ITextureEffectLayerRegistry
    {
        void RegisterLayer(in TextureEffectLayerDef layer);
        void UpdateLayer(string layerTag, in TextureEffectLayerDef layer);
        bool UnregisterLayer(string layerTag);
        bool TryGetLayer(string layerTag, out TextureEffectLayerDef layer);
    }

    public interface ITextureEffectMaskRegistry
    {
        int RegisterMask(in TextureEffectMaskEntry entry);
        void UpdateMask(int registrationId, in TextureEffectMaskEntry entry);
        bool UnregisterMask(int registrationId);
    }
}
