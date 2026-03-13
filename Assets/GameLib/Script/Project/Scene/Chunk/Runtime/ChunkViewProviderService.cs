#nullable enable
using UnityEngine;

namespace Game.Chunk
{
    public sealed class ChunkViewProviderService : IChunkViewProvider
    {
        readonly ChunkStreamerConfig _config;

        public ChunkViewProviderService(ChunkStreamerConfig config)
        {
            _config = config;
        }

        public Rect GetViewRect()
        {
            if (_config.UseCameraView && _config.ViewCamera != null)
            {
                var cam = _config.ViewCamera;
                if (cam.orthographic)
                {
                    var height = cam.orthographicSize * 2f;
                    var width = height * cam.aspect;
                    var center = (Vector2)cam.transform.position;
                    return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
                }
            }

            var target = GetTargetPosition();
            var size = _config.ManualViewSize;
            return new Rect(target.x - size.x * 0.5f, target.y - size.y * 0.5f, size.x, size.y);
        }

        public Vector2 GetTargetPosition()
        {
            if (_config.TargetTransform != null)
                return _config.TargetTransform.position;
            return Vector2.zero;
        }
    }
}
