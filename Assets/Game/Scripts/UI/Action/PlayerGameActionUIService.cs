using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Spawn;
using Game.UI;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;
using VNext = Game.Commands.VNext;

namespace Game.Actions
{
    public interface IPlayerGameActionUIService
    {
        UniTask BuildActionBarUI(CancellationToken cancellationToken);
        UniTask ShowActionBarAsync(CancellationToken cancellationToken);
        UniTask HideActionBarAsync(CancellationToken cancellationToken);
    }

    public sealed class PlayerGameActionUIService :
        IPlayerGameActionUIService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        // Partitioning [0,1] by three ranges yields at most 7 color segments.
        const int MaxSegmentBars = 7;
        const float MinSegmentLength = 0.0001f;

        readonly IScopeNode _scope;
        readonly IPlayerGameActionUISettings _settings;
        readonly IPlayerLocationService _locationService;
        readonly IUIElementRuntimeSpawnerService _runtimeSpawner;
        readonly VNext.ICommandRunner _commandRunner;

        IPlayerGameActionService _actionService;
        bool _isBuilt;
        bool _isShowing;
        CancellationTokenSource _updateCts;

        readonly List<SegmentBar> _segmentBars = new();

        sealed class SegmentBar
        {
            public RuntimeLifetimeScope Scope;
            public RectTransform Rect;
            public GageColor LastColor;
            public bool HasSegment;
            public bool IsShown;
        }

        readonly struct Interval
        {
            public readonly float Start;
            public readonly float End;

            public Interval(float start, float end)
            {
                Start = start;
                End = end;
            }

            public bool IsValid => End > Start;
            public bool Contains(float t) => t >= Start && t <= End;
        }

        readonly struct ColorSegment
        {
            public readonly float Start;
            public readonly float End;
            public readonly GageColor Color;

            public ColorSegment(float start, float end, GageColor color)
            {
                Start = start;
                End = end;
                Color = color;
            }
        }

        public PlayerGameActionUIService(
            IScopeNode scope,
            IPlayerGameActionUISettings settings,
            IPlayerLocationService locationService,
            IUIElementRuntimeSpawnerService runtimeSpawner,
            VNext.ICommandRunner commandRunner)
        {
            _scope = scope;
            _settings = settings;
            _locationService = locationService;
            _runtimeSpawner = runtimeSpawner;
            _commandRunner = commandRunner;
        }

        public async UniTask BuildActionBarUI(CancellationToken cancellationToken)
        {
            if (_isBuilt)
            {
                UpdateActionBarLayout();
                return;
            }

            if (_settings.ActionBarPrefab == null || _settings.BarParentTransform == null)
                return;

            if (!await EnsureActionServiceAsync(cancellationToken))
                return;

            // Spawn the maximum number of bars once, and reuse them for segments.
            await EnsureActionBarsAsync(cancellationToken);
            UpdateActionBarLayout();
            _isBuilt = true;
        }

        public async UniTask ShowActionBarAsync(CancellationToken cancellationToken)
        {
            if (!_isBuilt)
                await BuildActionBarUI(cancellationToken);

            if (!_isBuilt || cancellationToken.IsCancellationRequested)
                return;

            if (!_isShowing)
            {
                _isShowing = true;
                StartUpdateLoop();
                SubscribeStop();
            }

            await ExecuteCommandsAsync(_settings.ShowActionBarCommands, cancellationToken, "ShowActionBar");
        }

        public async UniTask HideActionBarAsync(CancellationToken cancellationToken)
        {
            if (_isShowing)
            {
                HideSegmentBars();
                _isShowing = false;
                StopUpdateLoop();
                UnsubscribeStop();
            }

            await ExecuteCommandsAsync(_settings.HideActionBarCommands, cancellationToken, "HideActionBar");
        }

        void IScopeAcquireHandler.OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
        }

        void IScopeReleaseHandler.OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            StopUpdateLoop();
            UnsubscribeStop();
            _isShowing = false;
            _actionService = null;
            _segmentBars.Clear();
            _isBuilt = false;
        }

        async UniTask<bool> EnsureActionServiceAsync(CancellationToken cancellationToken)
        {
            if (_actionService != null)
                return true;

            var playerScope = await _locationService.GetPlayerScopeAsync(cancellationToken);
            if (playerScope == null || playerScope.Resolver == null)
                return false;

            if (playerScope.Resolver.TryResolve<IPlayerGameActionService>(out var actionService) &&
                actionService != null)
            {
                _actionService = actionService;
                return true;
            }

            return false;
        }

        async UniTask EnsureActionBarsAsync(CancellationToken cancellationToken)
        {
            if (_settings.ActionBarPrefab == null || _settings.BarParentTransform == null)
                return;

            var basePosition = _settings.ActionBarLocalPosition;
            while (_segmentBars.Count < MaxSegmentBars)
            {
                var runtimeScope = await SpawnActionBarAsync(basePosition, cancellationToken);
                if (runtimeScope == null)
                    break;

                if (!TryResolveActionBarRect(runtimeScope, out var rect))
                    continue;

                _segmentBars.Add(new SegmentBar
                {
                    Scope = runtimeScope,
                    Rect = rect,
                    LastColor = GageColor.Gray,
                    HasSegment = false,
                    IsShown = false
                });
            }
        }

        async UniTask<RuntimeLifetimeScope> SpawnActionBarAsync(
            Vector3 localPosition,
            CancellationToken cancellationToken)
        {
            if (_runtimeSpawner == null || _settings.ActionBarPrefab == null)
                return null;

            var spawnParams = SpawnParams.ForRuntime(
                _settings.ActionBarPrefab,
                localPosition,
                Quaternion.identity,
                Vector3.one,
                identity: null,
                transformParent: _settings.BarParentTransform,
                lifetimeScopeParent: _scope,
                worldSpace: false,
                allowPooling: true);

            var resolver = await _runtimeSpawner.SpawnAsync(spawnParams, cancellationToken);
            if (resolver == null)
                return null;

            return ResolveRuntimeScope(resolver);
        }

        void UpdateActionBarLayout()
        {
            if (_actionService == null || _settings.BarParentTransform == null)
                return;

            var areaSize = _settings.BarParentTransform.rect.size;
            var basePosition = _settings.ActionBarLocalPosition;

            if (!TryGetSnapshots(out var red, out var blue, out var green))
            {
                ApplySegmentsToBars(Array.Empty<ColorSegment>(), areaSize, basePosition);
                UpdateSelectorPosition(areaSize, basePosition);
                return;
            }

            // Detect color segments across [0,1] by sampling between range boundaries.
            var redInterval = BuildInterval(in red);
            var blueInterval = BuildInterval(in blue);
            var greenInterval = BuildInterval(in green);
            var segments = BuildColorSegments(in redInterval, in blueInterval, in greenInterval);

            ApplySegmentsToBars(segments, areaSize, basePosition);
            UpdateSelectorPosition(areaSize, basePosition);
        }

        bool TryGetSnapshots(
            out ActionGageSnapshot red,
            out ActionGageSnapshot blue,
            out ActionGageSnapshot green)
        {
            red = default;
            blue = default;
            green = default;

            return _actionService.TryGetActionGageSnapshot(GageColor.Red, out red) &&
                   _actionService.TryGetActionGageSnapshot(GageColor.Blue, out blue) &&
                   _actionService.TryGetActionGageSnapshot(GageColor.Green, out green);
        }

        static Interval BuildInterval(in ActionGageSnapshot snapshot)
        {
            var half = snapshot.Range * 0.5f;
            var start = Mathf.Clamp01(snapshot.Position - half);
            var end = Mathf.Clamp01(snapshot.Position + half);
            return new Interval(start, end);
        }

        static List<ColorSegment> BuildColorSegments(
            in Interval red,
            in Interval blue,
            in Interval green)
        {
            var boundaries = new List<float>(8) { 0f, 1f };
            if (red.IsValid)
            {
                boundaries.Add(red.Start);
                boundaries.Add(red.End);
            }
            if (blue.IsValid)
            {
                boundaries.Add(blue.Start);
                boundaries.Add(blue.End);
            }
            if (green.IsValid)
            {
                boundaries.Add(green.Start);
                boundaries.Add(green.End);
            }

            boundaries.Sort();
            var segments = new List<ColorSegment>(7);
            for (int i = 0; i < boundaries.Count - 1; i++)
            {
                var start = boundaries[i];
                var end = boundaries[i + 1];
                if (end - start <= MinSegmentLength)
                    continue;

                var sample = (start + end) * 0.5f;
                var color = ResolveSegmentColor(sample, in red, in blue, in green);
                segments.Add(new ColorSegment(start, end, color));
            }

            return segments;
        }

        static GageColor ResolveSegmentColor(float t, in Interval red, in Interval blue, in Interval green)
        {
            var isRed = red.IsValid && red.Contains(t);
            var isBlue = blue.IsValid && blue.Contains(t);
            var isGreen = green.IsValid && green.Contains(t);

            if (isRed && isBlue && isGreen)
                return GageColor.White;
            if (isRed && isBlue)
                return GageColor.Purple;
            if (isRed && isGreen)
                return GageColor.Yellow;
            if (isBlue && isGreen)
                return GageColor.Cyan;
            if (isRed)
                return GageColor.Red;
            if (isBlue)
                return GageColor.Blue;
            if (isGreen)
                return GageColor.Green;

            return GageColor.Gray;
        }

        void ApplySegmentsToBars(IReadOnlyList<ColorSegment> segments, Vector2 areaSize, Vector3 basePosition)
        {
            var executeCommands = _isShowing;
            var segmentCount = segments != null ? segments.Count : 0;
            for (int i = 0; i < _segmentBars.Count; i++)
            {
                var bar = _segmentBars[i];
                if (bar == null || bar.Rect == null)
                    continue;

                if (i >= segmentCount)
                {
                    if (bar.HasSegment)
                    {
                        if (executeCommands && bar.IsShown)
                            RunActionBarCommands(bar, bar.LastColor, isShow: false);

                        bar.HasSegment = false;
                        bar.IsShown = false;
                    }

                    SetBarActive(bar, false);
                    continue;
                }

                var segment = segments[i];
                ApplySegmentTransform(bar, in segment, areaSize, basePosition);
                SetBarActive(bar, true);

                var colorChanged = bar.LastColor != segment.Color;
                if (!bar.HasSegment || colorChanged)
                {
                    if (executeCommands && bar.IsShown)
                        RunActionBarCommands(bar, bar.LastColor, isShow: false);

                    ApplySegmentColor(bar, segment.Color, force: !bar.HasSegment);
                    bar.HasSegment = true;

                    if (executeCommands)
                    {
                        RunActionBarCommands(bar, segment.Color, isShow: true);
                        bar.IsShown = true;
                    }
                    else
                    {
                        bar.IsShown = false;
                    }

                    continue;
                }

                bar.HasSegment = true;
                if (executeCommands && !bar.IsShown)
                {
                    RunActionBarCommands(bar, segment.Color, isShow: true);
                    bar.IsShown = true;
                }
            }
        }

        static void ApplySegmentTransform(
            SegmentBar bar,
            in ColorSegment segment,
            Vector2 areaSize,
            Vector3 basePosition)
        {
            var center = (segment.Start + segment.End) * 0.5f;
            var width = Mathf.Max(0f, segment.End - segment.Start);
            var offsetX = (center - 0.5f) * areaSize.x;

            bar.Rect.localPosition = basePosition + new Vector3(offsetX, 0f, 0f);
            bar.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, areaSize.x * width);
            bar.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, areaSize.y);
        }

        static void SetBarActive(SegmentBar bar, bool isActive)
        {
            if (bar?.Rect == null)
                return;

            var go = bar.Rect.gameObject;
            if (go != null && go.activeSelf != isActive)
                go.SetActive(isActive);
        }

        static void ApplySegmentColor(SegmentBar bar, GageColor color, bool force)
        {
            if (bar == null || bar.Scope == null)
                return;

            if (!force && bar.LastColor == color)
                return;

            bar.LastColor = color;
            var resolver = bar.Scope.Resolver;
            if (resolver == null)
                return;

            if (resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard != null)
            {
                blackboard.TryLocalSetVariant(
                    VarIds.GameLogic.GameProfiles.PlayerGameAction.currentGageColor,
                    DynamicVariant.FromInt((int)color));
            }
        }

        void HideSegmentBars()
        {
            for (int i = 0; i < _segmentBars.Count; i++)
            {
                var bar = _segmentBars[i];
                if (bar == null || bar.Rect == null)
                    continue;

                if (bar.IsShown)
                    RunActionBarCommands(bar, bar.LastColor, isShow: false);

                bar.IsShown = false;
            }
        }

        void RunActionBarCommands(SegmentBar bar, GageColor color, bool isShow)
        {
            if (!TryGetActionBarSetting(color, out var setting))
                return;

            var commands = isShow ? setting.showCommands : setting.hideCommands;
            if (commands == null || commands.Count == 0)
                return;

            var vars = CreateGageColorVarStore(color);
            ExecuteRuntimeCommandsAsync(bar, commands, vars, isShow ? "ShowActionBarColor" : "HideActionBarColor").Forget();
        }

        bool TryGetActionBarSetting(GageColor color, out ActionBarUISetting setting)
        {
            setting = null;
            if (_settings == null)
                return false;

            var settings = _settings.ActionBarSettings;
            if (settings == null || settings.Length == 0)
                return false;

            for (int i = 0; i < settings.Length; i++)
            {
                var entry = settings[i];
                if (entry != null && entry.key == color)
                {
                    setting = entry;
                    return true;
                }
            }

            return false;
        }

        UniTask ExecuteRuntimeCommandsAsync(
            SegmentBar bar,
            VNext.CommandListData commands,
            IVarStore vars,
            string label)
        {
            if (bar?.Scope == null)
                return UniTask.CompletedTask;

            if (commands == null || commands.Count == 0)
                return UniTask.CompletedTask;

            var resolver = bar.Scope.Resolver;
            if (resolver == null)
                return UniTask.CompletedTask;

            if (!resolver.TryResolve<VNext.ICommandRunner>(out var runner) || runner == null)
                return UniTask.CompletedTask;

            return ExecuteRuntimeCommandsCoreAsync(bar.Scope, runner, commands, vars, label);
        }

        async UniTask ExecuteRuntimeCommandsCoreAsync(
            IScopeNode scope,
            VNext.ICommandRunner runner,
            VNext.CommandListData commands,
            IVarStore vars,
            string label)
        {
            var options = VNext.CommandRunOptions.Default;
            var ctx = new VNext.CommandContext(scope, vars ?? NullVarStore.Instance, runner, scope, options);

            try
            {
                var result = await runner.ExecuteListAsync(commands, ctx, CancellationToken.None, options);
                if (result.Status == VNext.CommandRunStatus.Error)
                    Debug.LogError($"[PlayerGameActionUIService] {label} commands failed: {result.Message}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        void UpdateSelectorPosition(Vector2 areaSize, Vector3 basePosition)
        {
            var selector = _settings.SelectorParentTransform;
            if (selector == null || _actionService == null)
                return;

            var pos = selector.localPosition;
            var offsetX = (_actionService.CurrentSelectorPosition - 0.5f) * areaSize.x;
            pos.x = basePosition.x + offsetX;
            selector.localPosition = pos;
        }

        void StartUpdateLoop()
        {
            StopUpdateLoop();
            _updateCts = new CancellationTokenSource();
            UpdateLoopAsync(_updateCts.Token).Forget();
        }

        void StopUpdateLoop()
        {
            if (_updateCts == null)
                return;

            try
            {
                _updateCts.Cancel();
            }
            catch
            {
            }
            finally
            {
                _updateCts.Dispose();
                _updateCts = null;
            }
        }

        async UniTask UpdateLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    UpdateActionBarLayout();
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        void SubscribeStop()
        {
            if (_actionService == null)
                return;

            _actionService.OnGageStopped -= HandleGageStopped;
            _actionService.OnGageStopped += HandleGageStopped;
        }

        void UnsubscribeStop()
        {
            if (_actionService == null)
                return;

            _actionService.OnGageStopped -= HandleGageStopped;
        }

        void HandleGageStopped(GageColor color)
        {
            if (!_isShowing)
                return;

            var vars = CreateGageColorVarStore(color);
            ExecuteCommandsAsync(_settings.StopSelectorCommands, CancellationToken.None, "StopSelector", vars).Forget();
        }

        UniTask ExecuteCommandsAsync(
            VNext.CommandListData commands,
            CancellationToken cancellationToken,
            string label,
            IVarStore vars = null)
        {
            if (cancellationToken.IsCancellationRequested)
                return UniTask.CompletedTask;

            if (commands == null || commands.Count == 0)
                return UniTask.CompletedTask;

            if (_commandRunner == null)
                return UniTask.CompletedTask;

            if (_scope == null || _scope.Resolver == null)
                return UniTask.CompletedTask;

            return ExecuteCommandsCoreAsync(
                commands,
                _scope,
                vars ?? NullVarStore.Instance,
                cancellationToken,
                label);
        }

        async UniTask ExecuteCommandsCoreAsync(
            VNext.CommandListData commands,
            IScopeNode scope,
            IVarStore vars,
            CancellationToken cancellationToken,
            string label)
        {
            var options = VNext.CommandRunOptions.Default;
            var ctx = new VNext.CommandContext(scope, vars ?? NullVarStore.Instance, _commandRunner, scope, options);

            try
            {
                var result = await _commandRunner.ExecuteListAsync(commands, ctx, cancellationToken, options);
                if (result.Status == VNext.CommandRunStatus.Error)
                    Debug.LogError($"[PlayerGameActionUIService] {label} commands failed: {result.Message}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        static bool TryResolveActionBarRect(RuntimeLifetimeScope scope, out RectTransform rect)
        {
            rect = scope != null ? scope.GetComponent<RectTransform>() : null;
            return rect != null;
        }

        static RuntimeLifetimeScope ResolveRuntimeScope(IObjectResolver resolver)
        {
            if (resolver == null)
                return null;

            if (resolver.TryResolve<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
                return runtimeScope;

            if (resolver.TryResolve<IScopeNode>(out var scope) && scope is RuntimeLifetimeScope resolved)
                return resolved;

            if (resolver.TryResolve<Transform>(out var transform) && transform != null)
                return transform.GetComponent<RuntimeLifetimeScope>();

            if (resolver.TryResolve<GameObject>(out var go) && go != null)
                return go.GetComponent<RuntimeLifetimeScope>();

            return null;
        }

        static VarStore CreateGageColorVarStore(GageColor color)
        {
            var vars = new VarStore(1);
            vars.TrySetVariant(
                VarIds.GameLogic.GameProfiles.PlayerGameAction.currentGageColor,
                DynamicVariant.FromInt((int)color));
            return vars;
        }
    }
}
