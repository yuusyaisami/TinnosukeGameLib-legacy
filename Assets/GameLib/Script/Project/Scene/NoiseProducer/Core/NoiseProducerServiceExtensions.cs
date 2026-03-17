#nullable enable
using DG.Tweening;
using UnityEngine;

namespace Game.NoiseProducer
{
    public static class NoiseProducerServiceExtensions
    {
        public static bool SetFloat(
            this INoiseProducerService service,
            string channelId,
            string parameterKey,
            string layerTag,
            float value,
            float duration = 0f,
            Ease ease = Ease.Linear)
        {
            var request = new NoiseParameterWriteRequest(
                new NoiseParameterAddress(channelId, parameterKey, layerTag),
                NoiseParameterValue.Float(value),
                duration,
                ease);
            return service.TryWriteParameter(request);
        }

        public static bool SetVector2(
            this INoiseProducerService service,
            string channelId,
            string parameterKey,
            string layerTag,
            Vector2 value,
            float duration = 0f,
            Ease ease = Ease.Linear)
        {
            var request = new NoiseParameterWriteRequest(
                new NoiseParameterAddress(channelId, parameterKey, layerTag),
                NoiseParameterValue.Vec2(value),
                duration,
                ease);
            return service.TryWriteParameter(request);
        }

        public static bool SetColor(
            this INoiseProducerService service,
            string channelId,
            string parameterKey,
            string layerTag,
            Color value,
            float duration = 0f,
            Ease ease = Ease.Linear)
        {
            var request = new NoiseParameterWriteRequest(
                new NoiseParameterAddress(channelId, parameterKey, layerTag),
                NoiseParameterValue.Col(value),
                duration,
                ease);
            return service.TryWriteParameter(request);
        }

        public static bool SetBool(
            this INoiseProducerService service,
            string channelId,
            string parameterKey,
            string layerTag,
            bool value)
        {
            var request = new NoiseParameterWriteRequest(
                new NoiseParameterAddress(channelId, parameterKey, layerTag),
                NoiseParameterValue.Bool(value));
            return service.TryWriteParameter(request);
        }

        public static bool SetInt(
            this INoiseProducerService service,
            string channelId,
            string parameterKey,
            string layerTag,
            int value)
        {
            var request = new NoiseParameterWriteRequest(
                new NoiseParameterAddress(channelId, parameterKey, layerTag),
                NoiseParameterValue.Int(value));
            return service.TryWriteParameter(request);
        }
    }
}
