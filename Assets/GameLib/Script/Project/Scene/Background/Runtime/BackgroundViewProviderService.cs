#nullable enable
using UnityEngine;

namespace Game.Background
{
    public readonly struct BackgroundViewState
    {
        public Rect ViewRect { get; }
        public Vector2 ViewCenter { get; }
        public Vector2 ViewSize { get; }
        public float Zoom { get; }
        public Camera? Camera { get; }

        public BackgroundViewState(Rect viewRect, Vector2 viewCenter, Vector2 viewSize, float zoom, Camera? camera)
        {
            ViewRect = viewRect;
            ViewCenter = viewCenter;
            ViewSize = viewSize;
            Zoom = zoom;
            Camera = camera;
        }
    }

    public sealed class BackgroundViewProviderService
    {
        readonly BackgroundSystemConfig _config;

        public BackgroundViewProviderService(BackgroundSystemConfig config)
        {
            _config = config;
        }

        public BackgroundViewState GetViewState()
        {
            return _config.Space == BackgroundSpace.UI
                ? GetUiViewState()
                : GetWorldViewState();
        }

        BackgroundViewState GetWorldViewState()
        {
            var cam = _config.WorldCamera;
            if (_config.UseCameraView && cam != null && cam.orthographic)
            {
                var height = cam.orthographicSize * 2f;
                var width = height * cam.aspect;
                var center = (Vector2)cam.transform.position;
                var size = new Vector2(width, height);
                var rect = new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
                return new BackgroundViewState(rect, center, size, cam.orthographicSize, cam);
            }

            var targetPos = ResolveTargetPosition();
            var viewSize = _config.ManualViewSize;
            var viewRect = new Rect(targetPos.x - viewSize.x * 0.5f, targetPos.y - viewSize.y * 0.5f, viewSize.x, viewSize.y);
            return new BackgroundViewState(viewRect, targetPos, viewSize, 1f, cam);
        }

        BackgroundViewState GetUiViewState()
        {
            var root = _config.UiRoot;
            if (root == null)
                return default;

            var viewSize = root.rect.size;
            var center = root.rect.center;
            var target = ResolveUiTargetLocal(root);
            if (target.HasValue)
                center = target.Value;

            var viewRect = new Rect(center.x - viewSize.x * 0.5f, center.y - viewSize.y * 0.5f, viewSize.x, viewSize.y);
            return new BackgroundViewState(viewRect, center, viewSize, 1f, _config.UiCamera);
        }

        Vector2 ResolveTargetPosition()
        {
            if (_config.TargetTransform != null)
                return _config.TargetTransform.position;
            if (_config.WorldRoot != null)
                return _config.WorldRoot.position;
            return Vector2.zero;
        }

        Vector2? ResolveUiTargetLocal(RectTransform root)
        {
            if (_config.TargetTransform == null)
                return null;

            var world = _config.TargetTransform.position;
            var local = root.InverseTransformPoint(world);
            return new Vector2(local.x, local.y);
        }
    }
}
