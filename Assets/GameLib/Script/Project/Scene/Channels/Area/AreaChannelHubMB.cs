#nullable enable
using System;
using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;
using VContainer.Unity;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Channel
{
    [DisallowMultipleComponent]
    public sealed class AreaChannelHubMB : MonoBehaviour, IScopeInstaller
    {
        [BoxGroup("Hub")]
        [LabelText("Channels")]
        [SerializeField] AreaChannelDefinition[] channels = Array.Empty<AreaChannelDefinition>();

        [BoxGroup("Debug")]
        [LabelText("Show Gizmos")]
        [SerializeField] bool showAreaGizmo = true;

        [BoxGroup("Debug")]
        [ShowIf(nameof(showAreaGizmo))]
        [LabelText("Only Selected")]
        [SerializeField] bool selectedOnly = true;

        IScopeNode? _ownerScope;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            _ownerScope = owner;

            if (channels == null)
                channels = Array.Empty<AreaChannelDefinition>();

            for (int i = 0; i < channels.Length; i++)
            {
                channels[i]?.EnsureIntegrity(this);
            }

            builder.Register<AreaChannelHubService>(RuntimeLifetime.Singleton)
                .As<IAreaChannelHubService>()
                .As<IChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .AsSelf()
                .WithParameter(owner)
                .WithParameter(channels);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (channels == null)
                channels = Array.Empty<AreaChannelDefinition>();

            for (int i = 0; i < channels.Length; i++)
            {
                channels[i]?.EnsureIntegrity(this);
            }
        }
#endif

        void OnDrawGizmos()
        {
            if (selectedOnly)
                return;

            DrawGizmosCore();
        }

        void OnDrawGizmosSelected()
        {
            if (!selectedOnly)
                return;

            DrawGizmosCore();
        }

        void DrawGizmosCore()
        {
            if (!showAreaGizmo || channels == null)
                return;

            var runtimeHub = Application.isPlaying ? TryResolveHub() : null;

            for (int i = 0; i < channels.Length; i++)
            {
                var def = channels[i];
                if (def == null || !def.Enabled || def.Shape == null)
                    continue;

                var anchor = def.Anchor != null ? def.Anchor : transform;
                var center = anchor.position + def.CenterOffset;

                if (runtimeHub != null)
                {
                    if (runtimeHub.TryGetContour(def.Tag, out var runtimeContour))
                        DrawContourGizmo(runtimeContour, center, ColorFromTag(def.Tag));

                    continue;
                }

                var previewPlayer = new AreaChannelRuntimePlayer(def);
                if (previewPlayer.TryGetContour(center, out var previewContour))
                {
                    DrawContourGizmo(previewContour, center, ColorFromTag(def.Tag));
                    continue;
                }

                Gizmos.color = MakeOutlineColor(ColorFromTag(def.Tag));
                def.Shape.DrawGizmo(center, def.Plane);
            }
        }

        IAreaChannelHubService? TryResolveHub()
        {
            var resolver = _ownerScope?.Resolver;
            if (resolver == null)
                return null;

            return resolver.TryResolve<IAreaChannelHubService>(out var hub) ? hub : null;
        }

        static void DrawContourGizmo(in AreaContourData contour, Vector3 basePosition, Color baseColor)
        {
#if UNITY_EDITOR
            var previousColor = Handles.color;
            var previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;

            try
            {
                var hasHole = false;
                for (int i = 0; i < contour.Paths.Count; i++)
                {
                    if (contour.Paths[i].IsHole)
                    {
                        hasHole = true;
                        break;
                    }
                }

                if (!hasHole)
                {
                    for (int i = 0; i < contour.Paths.Count; i++)
                    {
                        var path = contour.Paths[i];
                        if (path.IsHole)
                            continue;

                        var worldPoints = BuildWorldPoints(path.Points, basePosition, contour.Plane);
                        if (worldPoints.Length < 3)
                            continue;

                        Handles.color = MakeFillColor(baseColor);
                        Handles.DrawAAConvexPolygon(worldPoints);
                        break;
                    }
                }

                for (int i = 0; i < contour.Paths.Count; i++)
                {
                    var path = contour.Paths[i];
                    var worldPoints = BuildWorldPoints(path.Points, basePosition, contour.Plane);
                    if (worldPoints.Length == 0)
                        continue;

                    if (path.IsHole)
                    {
                        Handles.color = MakeHoleLineColor(baseColor);
                        DrawDottedClosedLoop(worldPoints);
                    }
                    else
                    {
                        Handles.color = MakeOutlineColor(baseColor);
                        DrawSolidClosedLoop(worldPoints, 2.4f);
                    }
                }
            }
            finally
            {
                Handles.color = previousColor;
                Handles.zTest = previousZTest;
            }
#else
            DrawContourFallback(contour, basePosition, baseColor);
#endif
        }

        static void DrawContourFallback(in AreaContourData contour, Vector3 basePosition, Color baseColor)
        {
            var outerColor = MakeOutlineColor(baseColor);
            var holeColor = MakeHoleLineColor(baseColor);

            for (int i = 0; i < contour.Paths.Count; i++)
            {
                var path = contour.Paths[i];
                var worldPoints = BuildWorldPoints(path.Points, basePosition, contour.Plane);
                if (worldPoints.Length == 0)
                    continue;

                Gizmos.color = path.IsHole ? holeColor : outerColor;
                for (int p = 0; p < worldPoints.Length; p++)
                {
                    var next = p + 1 < worldPoints.Length ? p + 1 : 0;
                    Gizmos.DrawLine(worldPoints[p], worldPoints[next]);
                }
            }
        }

        static Vector3[] BuildWorldPoints(System.Collections.Generic.IReadOnlyList<Vector2> points, Vector3 basePosition, AreaPlane plane)
        {
            if (points == null || points.Count == 0)
                return Array.Empty<Vector3>();

            var worldPoints = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++)
                worldPoints[i] = ToWorldPoint(points[i], basePosition, plane);

            return worldPoints;
        }

        static Vector3 ToWorldPoint(Vector2 point, Vector3 basePosition, AreaPlane plane)
        {
            return plane == AreaPlane.XZ
                ? new Vector3(point.x, basePosition.y, point.y)
                : new Vector3(point.x, point.y, basePosition.z);
        }

#if UNITY_EDITOR
        static void DrawSolidClosedLoop(Vector3[] points, float width)
        {
            if (points.Length == 0)
                return;

            var loop = new Vector3[points.Length + 1];
            Array.Copy(points, loop, points.Length);
            loop[loop.Length - 1] = points[0];
            Handles.DrawAAPolyLine(width, loop);
        }

        static void DrawDottedClosedLoop(Vector3[] points)
        {
            if (points.Length == 0)
                return;

            for (int i = 0; i < points.Length; i++)
            {
                var next = i + 1 < points.Length ? i + 1 : 0;
                Handles.DrawDottedLine(points[i], points[next], 4f);
            }
        }
#endif

        static Color MakeOutlineColor(Color baseColor)
        {
            baseColor.a = 0.98f;
            return baseColor;
        }

        static Color MakeFillColor(Color baseColor)
        {
            baseColor.a = 0.10f;
            return baseColor;
        }

        static Color MakeHoleLineColor(Color baseColor)
        {
            Color.RGBToHSV(baseColor, out var hue, out var saturation, out var value);
            var hole = Color.HSVToRGB(Mathf.Repeat(hue + 0.5f, 1f), Mathf.Clamp01(saturation * 0.55f), Mathf.Clamp01(value * 0.9f));
            hole.a = 0.95f;
            return hole;
        }

        static Color ColorFromTag(string? tag)
        {
            var normalized = string.IsNullOrWhiteSpace(tag) ? "default" : tag;
            var hue = Mathf.Abs(normalized.GetHashCode() % 1000) / 1000f;
            return Color.HSVToRGB(hue, 0.88f, 1f);
        }
    }
}

