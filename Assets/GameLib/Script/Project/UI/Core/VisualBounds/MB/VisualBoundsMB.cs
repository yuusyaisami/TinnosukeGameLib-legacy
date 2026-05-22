#nullable enable
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;
using VContainer;
using VContainer.Unity;
using Game;
using Game.Commands.VNext;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class VisualBoundsMB : MonoBehaviour, IScopeInstaller
    {
        const string RootGroup = "Root";
        const string SourcesGroup = "Sources";
        const string OptionsGroup = "Options";
        const string DebugGroup = "Debug";
        const string CommandGroup = "Command";

        [BoxGroup(RootGroup)]
        [LabelText("Root Transform")]
        [Required]
        [SerializeField] Transform root = null!;

        [BoxGroup(SourcesGroup)]
        [LabelText("RectTransforms")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField] List<RectTransform> rectTargets = new();

        [BoxGroup(SourcesGroup)]
        [LabelText("Images")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        [SerializeField] List<Image> imageTargets = new();

        [BoxGroup(SourcesGroup)]
        [LabelText("TextMeshProUGUI")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        [SerializeField] List<TMP_Text> textTargets = new();

        [BoxGroup(SourcesGroup)]
        [LabelText("SpriteRenderers")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        [SerializeField] List<SpriteRenderer> spriteTargets = new();

        [BoxGroup(SourcesGroup)]
        [LabelText("MeshRenderers")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        [SerializeField] List<MeshRenderer> meshTargets = new();

        [BoxGroup(SourcesGroup)]
        [LabelText("Collider2D")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        [SerializeField] List<Collider2D> collider2DTargets = new();

        [BoxGroup(SourcesGroup)]
        [LabelText("Collider (3D)")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        [SerializeField] List<Collider> colliderTargets = new();

        [BoxGroup(OptionsGroup)]
        [SerializeField] bool excludeInactive = true;

        [BoxGroup(OptionsGroup)]
        [SerializeField] bool autoRebuild = true;

        [BoxGroup(OptionsGroup)]
        [SerializeField] bool autoDetectChanges = true;

        [BoxGroup(OptionsGroup)]
        [Min(0f)]
        [SerializeField] float autoRebuildIntervalSeconds = 0f;

        [BoxGroup(OptionsGroup)]
        [SerializeField] bool runInLateUpdate = true;

        [BoxGroup(CommandGroup)]
        [LabelText("Execute On Bounds Changed")]
        [SerializeField] bool executeOnBoundsChanged;

        [BoxGroup(CommandGroup)]
        [LabelText("Rebuild Before Check")]
        [ShowIf(nameof(executeOnBoundsChanged))]
        [SerializeField] bool rebuildBeforeCommandCheck = true;

        [BoxGroup(CommandGroup)]
        [LabelText("Position Epsilon")]
        [ShowIf(nameof(executeOnBoundsChanged))]
        [Min(0f)]
        [SerializeField] float commandPositionEpsilon = 0.1f;

        [BoxGroup(CommandGroup)]
        [LabelText("Size Epsilon")]
        [ShowIf(nameof(executeOnBoundsChanged))]
        [Min(0f)]
        [SerializeField] float commandSizeEpsilon = 0.1f;

        [BoxGroup(CommandGroup)]
        [LabelText("On Bounds Changed")]
        [ShowIf(nameof(executeOnBoundsChanged))]
        [SerializeField] CommandListData onBoundsChangedCommands = new();

        [BoxGroup(CommandGroup)]
        [LabelText("Execute On Acquire")]
        [ShowIf(nameof(executeOnBoundsChanged))]
        [SerializeField] bool executeOnBoundsChangedOnAcquire = true;

        [BoxGroup(CommandGroup)]
        [LabelText("Command Debug Log")]
        [ShowIf(nameof(executeOnBoundsChanged))]
        [SerializeField] bool commandDebugLog;

        [BoxGroup(DebugGroup)]
        [LabelText("Enable Debug GUI")]
        [SerializeField] bool enableDebugGui;

        [BoxGroup(DebugGroup)]
        [LabelText("Debug Camera")]
        [ShowIf(nameof(enableDebugGui))]
        [SerializeField] Camera? debugCamera;

        [BoxGroup(DebugGroup)]
        [LabelText("Rebuild Every GUI")]
        [ShowIf(nameof(enableDebugGui))]
        [SerializeField] bool rebuildEveryGui;

        [BoxGroup(DebugGroup)]
        [LabelText("Line Color")]
        [ShowIf(nameof(enableDebugGui))]
        [SerializeField] Color debugLineColor = new(0.2f, 1f, 0.6f, 1f);

        [BoxGroup(DebugGroup)]
        [LabelText("Line Width")]
        [ShowIf(nameof(enableDebugGui))]
        [Min(1f)]
        [SerializeField] float debugLineWidth = 2f;

        [BoxGroup(DebugGroup)]
        [LabelText("Show Label")]
        [ShowIf(nameof(enableDebugGui))]
        [SerializeField] bool showDebugLabel = true;

        [BoxGroup(DebugGroup)]
        [LabelText("Label Color")]
        [ShowIf("@enableDebugGui && showDebugLabel")]
        [SerializeField] Color debugLabelColor = Color.white;

        [BoxGroup(DebugGroup)]
        [LabelText("Show In Scene")]
        [ShowIf(nameof(enableDebugGui))]
        [SerializeField] bool showInScene = true;

        [BoxGroup(DebugGroup), ShowInInspector, ReadOnly, LabelText("Has Bounds")]
        bool InspectorHasBounds => _output != null && _output.HasBounds;

        [BoxGroup(DebugGroup), ShowInInspector, ReadOnly, LabelText("Local Rect")]
        Rect InspectorLocalRect => _output != null ? _output.LocalRect : Rect.zero;

        [BoxGroup(DebugGroup), ShowInInspector, ReadOnly, LabelText("Local Center")]
        Vector2 InspectorLocalCenter => _output != null ? _output.LocalCenter : Vector2.zero;

        [BoxGroup(DebugGroup), ShowInInspector, ReadOnly, LabelText("Local Size")]
        Vector2 InspectorLocalSize => _output != null ? _output.LocalSize : Vector2.zero;

        [BoxGroup(DebugGroup), ShowInInspector, ReadOnly, LabelText("World Center")]
        Vector3 InspectorWorldCenter => _output != null ? _output.WorldCenter : Vector3.zero;

        [BoxGroup(DebugGroup), ShowInInspector, ReadOnly, LabelText("World Size")]
        Vector3 InspectorWorldSize => _output != null ? _output.WorldSize : Vector3.zero;

        IVisualBoundsService? _service;
        IVisualBoundsOutput? _output;
        static Texture2D? s_guiTexture;
        GUIStyle? _labelStyle;
        readonly Vector3[] _debugCorners = new Vector3[8];

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            var config = new VisualBoundsConfig
            {
                Root = root != null ? root : transform,
                RootRect = root as RectTransform,
                RectTargets = rectTargets ?? new List<RectTransform>(),
                ImageTargets = imageTargets ?? new List<Image>(),
                TextTargets = textTargets ?? new List<TMP_Text>(),
                SpriteTargets = spriteTargets ?? new List<SpriteRenderer>(),
                MeshTargets = meshTargets ?? new List<MeshRenderer>(),
                Collider2DTargets = collider2DTargets ?? new List<Collider2D>(),
                ColliderTargets = colliderTargets ?? new List<Collider>(),
                ExcludeInactive = excludeInactive,
                AutoRebuild = autoRebuild,
                AutoDetectChanges = autoDetectChanges,
                AutoRebuildIntervalSeconds = autoRebuildIntervalSeconds,
                RunInLateUpdate = runInLateUpdate,
            };

            builder.RegisterInstance(config);

            var registration = builder.Register<VisualBoundsService>(RuntimeLifetime.Singleton)
                .As<IVisualBoundsService>()
                .As<IVisualBoundsOutput>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            if (autoRebuild || autoDetectChanges)
            {
                if (runInLateUpdate)
                    registration.As<IScopeLateTickHandler>();
                else
                    registration.As<IScopeTickHandler>();
            }

            if (executeOnBoundsChanged)
            {
                var commandConfig = new VisualBoundsChangeCommandConfig
                {
                    Commands = onBoundsChangedCommands ?? new CommandListData(),
                    RebuildBeforeCheck = rebuildBeforeCommandCheck,
                    PositionEpsilon = Mathf.Max(0f, commandPositionEpsilon),
                    SizeEpsilon = Mathf.Max(0f, commandSizeEpsilon),
                    RunInLateUpdate = runInLateUpdate,
                    ExecuteOnAcquire = executeOnBoundsChangedOnAcquire,
                    EnableDebugLog = commandDebugLog,
                };

                builder.RegisterInstance(commandConfig);

                var commandRegistration = builder.Register<VisualBoundsChangeCommandService>(RuntimeLifetime.Singleton)
                    .As<IScopeAcquireHandler>()
                    .As<IScopeReleaseHandler>();

                if (runInLateUpdate)
                    commandRegistration.As<IScopeLateTickHandler>();
                else
                    commandRegistration.As<IScopeTickHandler>();
            }

            builder.RegisterBuildCallback(resolver =>
            {
                if (resolver.TryResolve<IVisualBoundsService>(out var service))
                {
                    _service = service;
                    _service.MarkDirty();
                }

                if (resolver.TryResolve<IVisualBoundsOutput>(out var output))
                    _output = output;
            });
        }

        void Reset()
        {
            if (root == null)
                root = transform;
        }

        void OnGUI()
        {
            if (!enableDebugGui || _output == null || !_output.HasBounds)
                return;

            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;

            if (rebuildEveryGui)
                _service?.RebuildNow();

            if (!TryBuildScreenRect(_output.WorldBounds, out var screenRect))
                return;

            DrawRectOutline(screenRect, debugLineColor, Mathf.Max(1f, debugLineWidth));

            if (!showDebugLabel)
                return;

            EnsureLabelStyle();
            _labelStyle!.normal.textColor = debugLabelColor;
            var text = $"VisualBounds pos=({screenRect.x:F1},{screenRect.y:F1}) size=({_output.LocalSize.x:F1},{_output.LocalSize.y:F1})";
            var labelRect = new Rect(screenRect.xMin, Mathf.Max(0f, screenRect.yMin - 22f), 800f, 20f);
            GUI.Label(labelRect, text, _labelStyle);
        }

        void OnDrawGizmos()
        {
            if (!enableDebugGui || !showInScene || _output == null || !_output.HasBounds)
                return;

            var world = _output.WorldBounds;
            Gizmos.color = debugLineColor;
            Gizmos.DrawWireCube(world.center, world.size);

#if UNITY_EDITOR
            if (showDebugLabel)
            {
                UnityEditor.Handles.color = debugLabelColor;
                UnityEditor.Handles.Label(
                    world.center,
                    $"VisualBounds\nCenter=({world.center.x:F2},{world.center.y:F2},{world.center.z:F2})\nSize=({world.size.x:F2},{world.size.y:F2},{world.size.z:F2})");
            }
#endif
        }

        bool TryBuildScreenRect(in Bounds worldBounds, out Rect rect)
        {
            rect = default;
            FillBoundsCorners(worldBounds, _debugCorners);

            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            ResolveProjectionContext(out var camera, out var useOverlayProjection);

            for (int i = 0; i < _debugCorners.Length; i++)
            {
                if (!TryProjectToGui(_debugCorners[i], camera, useOverlayProjection, out var point))
                    continue;

                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            if (!IsFinite(min.x) || !IsFinite(min.y) || !IsFinite(max.x) || !IsFinite(max.y))
                return false;

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return rect.width > 0f && rect.height > 0f;
        }

        void ResolveProjectionContext(out Camera? camera, out bool useOverlayProjection)
        {
            useOverlayProjection = false;
            camera = debugCamera;

            if (camera != null)
                return;

            var canvas = root != null ? root.GetComponentInParent<Canvas>() : null;
            if (canvas != null)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    useOverlayProjection = true;
                    camera = null;
                    return;
                }

                if (canvas.worldCamera != null)
                {
                    camera = canvas.worldCamera;
                    return;
                }
            }

            camera = Camera.main;
        }

        static bool TryProjectToGui(in Vector3 world, Camera? camera, bool useOverlayProjection, out Vector2 guiPoint)
        {
            if (!useOverlayProjection && camera != null)
            {
                var screen = camera.WorldToScreenPoint(world);
                if (!IsFinite(screen.x) || !IsFinite(screen.y) || screen.z < 0f)
                {
                    guiPoint = default;
                    return false;
                }

                guiPoint = new Vector2(screen.x, Screen.height - screen.y);
                return true;
            }

            var overlayScreen = RectTransformUtility.WorldToScreenPoint(null, world);
            if (!IsFinite(overlayScreen.x) || !IsFinite(overlayScreen.y))
            {
                guiPoint = default;
                return false;
            }

            guiPoint = new Vector2(overlayScreen.x, Screen.height - overlayScreen.y);
            return true;
        }

        static void FillBoundsCorners(in Bounds bounds, Vector3[] corners)
        {
            var min = bounds.min;
            var max = bounds.max;

            corners[0] = new Vector3(min.x, min.y, min.z);
            corners[1] = new Vector3(min.x, min.y, max.z);
            corners[2] = new Vector3(min.x, max.y, min.z);
            corners[3] = new Vector3(min.x, max.y, max.z);
            corners[4] = new Vector3(max.x, min.y, min.z);
            corners[5] = new Vector3(max.x, min.y, max.z);
            corners[6] = new Vector3(max.x, max.y, min.z);
            corners[7] = new Vector3(max.x, max.y, max.z);
        }

        static void DrawRectOutline(in Rect rect, in Color color, float thickness)
        {
            var tex = GetGuiTexture();
            var top = new Rect(rect.xMin, rect.yMin, rect.width, thickness);
            var bottom = new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness);
            var left = new Rect(rect.xMin, rect.yMin, thickness, rect.height);
            var right = new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height);

            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(top, tex);
            GUI.DrawTexture(bottom, tex);
            GUI.DrawTexture(left, tex);
            GUI.DrawTexture(right, tex);
            GUI.color = prev;
        }

        static Texture2D GetGuiTexture()
        {
            if (s_guiTexture != null)
                return s_guiTexture;

            s_guiTexture = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point
            };
            s_guiTexture.SetPixel(0, 0, Color.white);
            s_guiTexture.Apply(false, true);
            return s_guiTexture;
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        void EnsureLabelStyle()
        {
            if (_labelStyle != null)
                return;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                richText = false
            };
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (root == null)
                root = transform;

            if (autoRebuildIntervalSeconds < 0f)
                autoRebuildIntervalSeconds = 0f;
        }
#endif
    }
}

