using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;
using Game;
using Game.Save;
using Game.Common;
using VNext = Game.Commands.VNext;
using VContainer;
using Sirenix.OdinInspector;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class FPSDisplay : MonoBehaviour
{
    [Serializable]
    sealed class CustomDebugButtonEntry
    {
        [HorizontalGroup("Row", 0.24f)]
        [LabelText("Key")]
        [SerializeField]
        KeyCode binding = KeyCode.None;

        [HorizontalGroup("Row", 0.38f)]
        [LabelText("Name")]
        [SerializeField]
        string displayName = string.Empty;

        [HorizontalGroup("Row", 0.16f)]
        [LabelText("Execute")]
        [SerializeField]
        bool executeCommandOnPress = true;

        [LabelText("Commands")]
        [SerializeField]
        VNext.CommandListData commands = new();

        [NonSerialized]
        public string LastStatus = "Idle";

        public KeyCode Binding => binding;
        public bool ExecuteCommandOnPress => executeCommandOnPress;
        public VNext.CommandListData Commands => commands;

        public string GetDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName;

            if (!string.IsNullOrWhiteSpace(commands.FunctionName))
                return commands.FunctionName;

            return "<unnamed>";
        }

        public string GetCommandName()
        {
            if (!string.IsNullOrWhiteSpace(commands.FunctionName))
                return commands.FunctionName;

            return commands.Count > 0 ? "<anonymous commands>" : "<empty>";
        }
    }

    [SerializeField] float updateInterval = 0.25f;
    [SerializeField] bool visibleOnStart = false;
    [SerializeField] float deleteSaveHoldSeconds = 2f;
    [BoxGroup("Performance")]
    [LabelText("Apply Frame Pacing On Awake")]
    [SerializeField] bool applyFramePacingOnAwake = true;
    [BoxGroup("Performance")]
    [LabelText("Disable VSync")]
    [SerializeField] bool disableVSync = true;
    [BoxGroup("Performance")]
    [LabelText("Target FPS (-1: Unlimited)")]
    [SerializeField] int targetFps = -1;
    [BoxGroup("Performance")]
    [LabelText("Force Exclusive Fullscreen (Windows)")]
    [SerializeField] bool forceExclusiveFullscreenOnWindows;
    [BoxGroup("Performance/Adaptive RenderScale")]
    [LabelText("Enable Adaptive RenderScale")]
    [SerializeField] bool enableAdaptiveRenderScale;
    [BoxGroup("Performance/Adaptive RenderScale")]
    [ShowIf(nameof(enableAdaptiveRenderScale))]
    [LabelText("Min RenderScale")]
    [MinValue(0.4f)]
    [MaxValue(1.0f)]
    [SerializeField] float adaptiveMinRenderScale = 0.55f;
    [BoxGroup("Performance/Adaptive RenderScale")]
    [ShowIf(nameof(enableAdaptiveRenderScale))]
    [LabelText("Max RenderScale")]
    [MinValue(0.4f)]
    [MaxValue(1.0f)]
    [SerializeField] float adaptiveMaxRenderScale = 0.85f;
    [BoxGroup("Performance/Adaptive RenderScale")]
    [ShowIf(nameof(enableAdaptiveRenderScale))]
    [LabelText("GPU High Threshold (ms)")]
    [MinValue(1f)]
    [SerializeField] float adaptiveGpuHighMs = 16.7f;
    [BoxGroup("Performance/Adaptive RenderScale")]
    [ShowIf(nameof(enableAdaptiveRenderScale))]
    [LabelText("GPU Low Threshold (ms)")]
    [MinValue(1f)]
    [SerializeField] float adaptiveGpuLowMs = 13.5f;
    [BoxGroup("Performance/Adaptive RenderScale")]
    [ShowIf(nameof(enableAdaptiveRenderScale))]
    [LabelText("Scale Step")]
    [MinValue(0.01f)]
    [MaxValue(0.2f)]
    [SerializeField] float adaptiveScaleStep = 0.05f;
    [BoxGroup("Performance/Adaptive RenderScale")]
    [ShowIf(nameof(enableAdaptiveRenderScale))]
    [LabelText("Change Cooldown Seconds")]
    [MinValue(0.05f)]
    [SerializeField] float adaptiveChangeCooldownSeconds = 0.4f;
    [BoxGroup("Custom Debug Buttons")]
    [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
    [SerializeField] CustomDebugButtonEntry[] customDebugButtons = Array.Empty<CustomDebugButtonEntry>();
    FrameTiming[] timings = new FrameTiming[1];
    float cpuMs, gpuMs;
    bool hasCpuTiming;
    bool hasGpuTiming;
    bool isVisible;
    string cpuName = "";
    string gpuName = "";

    float accum;
    int frames;
    float timeleft;
    float fps;
    float intervalWorstFrameMs;
    float intervalBestFrameMs;

    int gc0Count;
    int gc1Count;
    int gc2Count;
    float deleteSaveHoldTime;
    bool deleteSaveTriggeredThisPress;
    string saveDeleteStatus = "Ready";
    ISaveManager cachedSaveManager;
    VNext.ICommandRunner cachedCommandRunner;
    UniversalRenderPipelineAsset cachedUrpAsset;
    float adaptiveGpuEmaMs;
    float adaptiveCooldownTimer;
    float adaptiveCurrentRenderScale;
    bool hasAdaptiveGpuEma;

    void Awake()
    {
        if (!applyFramePacingOnAwake)
            return;

        QualitySettings.vSyncCount = disableVSync ? 0 : Mathf.Max(0, QualitySettings.vSyncCount);
        Application.targetFrameRate = targetFps > 0 ? targetFps : (disableVSync ? 60 : Application.targetFrameRate);
    }
    void Start()
    {
#if UNITY_STANDALONE_WIN
        if (forceExclusiveFullscreenOnWindows)
        {
            Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
            Screen.fullScreen = true;
        }
#endif
        isVisible = visibleOnStart;
        cpuName = string.IsNullOrWhiteSpace(SystemInfo.processorType) ? "Unknown" : SystemInfo.processorType;
        gpuName = string.IsNullOrWhiteSpace(SystemInfo.graphicsDeviceName) ? "Unknown" : SystemInfo.graphicsDeviceName;
        cachedUrpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (cachedUrpAsset == null)
            cachedUrpAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;

        if (cachedUrpAsset != null)
            adaptiveCurrentRenderScale = cachedUrpAsset.renderScale;
    }

    void OnEnable()
    {
        timeleft = updateInterval;
        accum = 0f;
        frames = 0;
        intervalWorstFrameMs = 0f;
        intervalBestFrameMs = float.MaxValue;
        gc0Count = System.GC.CollectionCount(0);
        gc1Count = System.GC.CollectionCount(1);
        gc2Count = System.GC.CollectionCount(2);
    }

    void Update()
    {
        if (IsTogglePressed())
        {
            isVisible = !isVisible;
            if (!isVisible)
                ResetDeleteHoldState();
        }

        UpdateDeleteAllSaveInput();
        UpdateCustomDebugButtons();

        var dt = Time.unscaledDeltaTime;
        timeleft -= dt;
        accum += dt;
        frames++;
        var frameMsNow = dt * 1000f;
        if (frameMsNow > intervalWorstFrameMs)
            intervalWorstFrameMs = frameMsNow;
        if (frameMsNow < intervalBestFrameMs)
            intervalBestFrameMs = frameMsNow;

        if (timeleft <= 0f)
        {
            fps = frames / accum;
            timeleft = updateInterval;
            accum = 0f;
            frames = 0;
            intervalWorstFrameMs = frameMsNow;
            intervalBestFrameMs = frameMsNow;
        }

        if (isVisible || enableAdaptiveRenderScale)
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
            {
                // 単位は ms
                var cpu = (float)timings[0].cpuFrameTime;
                var gpu = (float)timings[0].gpuFrameTime;

                hasCpuTiming = cpu > 0.0001f;
                hasGpuTiming = gpu > 0.0001f;

                if (hasCpuTiming)
                    cpuMs = cpu;
                if (hasGpuTiming)
                    gpuMs = gpu;
            }
        }

        UpdateAdaptiveRenderScale(dt);
    }

    void OnGUI()
    {
        if (!isVisible)
            return;

        if (Event.current == null || Event.current.type != EventType.Repaint)
            return;

        GUI.color = Color.white;
        var x = 10f;
        var y = 10f;
        const float w = 1200f;
        const float h = 20f;

        GUI.Label(new Rect(x, y, w, h), "[F1] Toggle FPS Display");
        y += 22f;
        GUI.Label(new Rect(x, y, w, h), $"[F4 Hold] Delete all save data | Hold: {deleteSaveHoldTime:F1}/{Mathf.Max(0.1f, deleteSaveHoldSeconds):F1}s | Status: {saveDeleteStatus}");
        y += 20f;
        if (customDebugButtons != null && customDebugButtons.Length > 0)
        {
            for (int i = 0; i < customDebugButtons.Length; i++)
            {
                var entry = customDebugButtons[i];
                if (entry == null)
                    continue;

                GUI.Label(
                    new Rect(x, y, w, h),
                    $"[{FormatBindingLabel(entry.Binding)}] Custom Debug Button: {entry.GetDisplayName()} | Command: {entry.GetCommandName()} | Execute: {(entry.ExecuteCommandOnPress ? "On" : "Off")} | Status: {entry.LastStatus}");
                y += 20f;
            }
        }

        GUI.Label(new Rect(x, y, w, h), $"Platform: {GetPlatformLabel()} | Build: {GetBuildLabel()} | Unity: {Application.unityVersion}");
        y += 20f;
        GUI.Label(new Rect(x, y, w, h), $"OS: {SystemInfo.operatingSystem}");
        y += 20f;
        GUI.Label(new Rect(x, y, w, h), $"Resolution: {Screen.width}x{Screen.height} @ {GetRefreshRateText()} | FullScreen: {Screen.fullScreen} ({Screen.fullScreenMode})");
        y += 20f;
        GUI.Label(new Rect(x, y, w, h), $"Quality: {QualitySettings.names[QualitySettings.GetQualityLevel()]} | vSync: {QualitySettings.vSyncCount} | targetFPS: {Application.targetFrameRate}");
        y += 20f;
        if (cachedUrpAsset != null)
        {
            GUI.Label(new Rect(x, y, w, h), $"URP RenderScale: {cachedUrpAsset.renderScale:F2} | Adaptive: {(enableAdaptiveRenderScale ? "On" : "Off")} | GPU EMA: {(hasAdaptiveGpuEma ? adaptiveGpuEmaMs.ToString("F2") : "N/A")} ms");
            y += 20f;
        }

        GUI.Label(new Rect(x, y, w, h), $"FPS: {fps:F1}");
        y += 20f;
        var frameMs = 1000f / Mathf.Max(0.01f, fps);
        GUI.Label(new Rect(x, y, w, h), $"Frame ms(avg): {frameMs:F2} | Frame ms(best): {intervalBestFrameMs:F2} | Frame ms(worst): {intervalWorstFrameMs:F2}");
        y += 20f;

        var cpuDisplayMs = hasCpuTiming ? cpuMs : frameMs;
        var cpuFps = cpuDisplayMs > 0.0001f ? 1000f / cpuDisplayMs : 0f;
        var gpuFps = hasGpuTiming && gpuMs > 0.0001f ? 1000f / gpuMs : 0f;

        GUI.Label(new Rect(x, y, w, h), $"CPU Name: {cpuName} | Cores: {SystemInfo.processorCount} | Freq: {SystemInfo.processorFrequency} MHz");
        y += 20f;
        GUI.Label(new Rect(x, y, w, h), hasCpuTiming
            ? $"CPU: {cpuMs:F2} ms  (theory {cpuFps:F0} fps)"
            : $"CPU: {cpuDisplayMs:F2} ms (fallback by frame time)");
        y += 20f;
        GUI.Label(new Rect(x, y, w, h), $"GPU Name: {gpuName} | API: {SystemInfo.graphicsDeviceType} | VRAM: {SystemInfo.graphicsMemorySize} MB");
        y += 20f;
        GUI.Label(new Rect(x, y, w, h), hasGpuTiming
            ? $"GPU: {gpuMs:F2} ms  (theory {gpuFps:F0} fps)"
            : "GPU: N/A (FrameTiming unsupported/disabled on this build/platform)");
        y += 20f;
        GUI.Label(new Rect(x, y, w, h), hasGpuTiming
            ? $"Bottleneck theory fps: {Mathf.Min(cpuFps, gpuFps):F0}"
            : $"Bottleneck theory fps: {cpuFps:F0} (CPU only)");
        y += 20f;

        var monoUsedMb = Profiler.GetMonoUsedSizeLong() / (1024f * 1024f);
        var totalAllocMb = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
        var gc0Now = System.GC.CollectionCount(0);
        var gc1Now = System.GC.CollectionCount(1);
        var gc2Now = System.GC.CollectionCount(2);
        GUI.Label(new Rect(x, y, w, h),
            $"Memory: MonoUsed {monoUsedMb:F1} MB | TotalAllocated {totalAllocMb:F1} MB | SystemRAM {SystemInfo.systemMemorySize} MB");
        y += 20f;
        GUI.Label(new Rect(x, y, w, h),
            $"GC Count(Delta): Gen0 {gc0Now - gc0Count} | Gen1 {gc1Now - gc1Count} | Gen2 {gc2Now - gc2Count}");
    }

    void UpdateAdaptiveRenderScale(float dt)
    {
        if (!enableAdaptiveRenderScale)
            return;
#if UNITY_EDITOR
        if (Application.isEditor)
            return;
#endif
        if (cachedUrpAsset == null)
            return;
        if (!hasGpuTiming)
            return;

        var minScale = Mathf.Clamp(adaptiveMinRenderScale, 0.4f, 1.0f);
        var maxScale = Mathf.Clamp(adaptiveMaxRenderScale, minScale, 1.0f);
        var step = Mathf.Clamp(adaptiveScaleStep, 0.01f, 0.2f);
        var highMs = Mathf.Max(1f, adaptiveGpuHighMs);
        var lowMs = Mathf.Clamp(adaptiveGpuLowMs, 1f, highMs - 0.1f);

        if (!hasAdaptiveGpuEma)
        {
            adaptiveGpuEmaMs = gpuMs;
            hasAdaptiveGpuEma = true;
        }
        else
        {
            var lerpT = 1f - Mathf.Exp(-Mathf.Max(0.001f, dt) * 4f);
            adaptiveGpuEmaMs = Mathf.Lerp(adaptiveGpuEmaMs, gpuMs, lerpT);
        }

        if (adaptiveCooldownTimer > 0f)
        {
            adaptiveCooldownTimer -= dt;
            return;
        }

        adaptiveCurrentRenderScale = Mathf.Clamp(cachedUrpAsset.renderScale, minScale, maxScale);
        var nextScale = adaptiveCurrentRenderScale;

        if (adaptiveGpuEmaMs > highMs)
            nextScale -= step;
        else if (adaptiveGpuEmaMs < lowMs)
            nextScale += step;
        else
            return;

        nextScale = Mathf.Clamp(nextScale, minScale, maxScale);
        if (Mathf.Abs(nextScale - adaptiveCurrentRenderScale) < 0.001f)
            return;

        cachedUrpAsset.renderScale = nextScale;
        adaptiveCurrentRenderScale = nextScale;
        adaptiveCooldownTimer = Mathf.Max(0.05f, adaptiveChangeCooldownSeconds);
    }

    void UpdateDeleteAllSaveInput()
    {
        if (!isVisible)
        {
            ResetDeleteHoldState();
            return;
        }

        if (!IsDeleteHoldPressed())
        {
            if (!deleteSaveTriggeredThisPress)
                saveDeleteStatus = "Ready";
            deleteSaveTriggeredThisPress = false;
            deleteSaveHoldTime = 0f;
            return;
        }

        if (deleteSaveTriggeredThisPress)
            return;

        deleteSaveHoldTime += Time.unscaledDeltaTime;
        saveDeleteStatus = $"Holding {Mathf.Clamp01(deleteSaveHoldTime / Mathf.Max(0.1f, deleteSaveHoldSeconds)) * 100f:F0}%";

        if (deleteSaveHoldTime < deleteSaveHoldSeconds)
            return;

        deleteSaveTriggeredThisPress = true;
        deleteSaveHoldTime = 0f;

        var saveManager = ResolveSaveManager();
        if (saveManager == null)
        {
            saveDeleteStatus = "SaveManager not found";
            return;
        }

        var result = saveManager.DeleteAllPersistedData();
        saveDeleteStatus = result.IsSuccess
            ? "Deleted all save data"
            : $"Delete failed: {result.Error}";
    }

    void ResetDeleteHoldState()
    {
        deleteSaveHoldTime = 0f;
        deleteSaveTriggeredThisPress = false;
        if (string.IsNullOrEmpty(saveDeleteStatus))
            saveDeleteStatus = "Ready";
    }

    void UpdateCustomDebugButtons()
    {
        if (!isVisible || customDebugButtons == null || customDebugButtons.Length == 0)
            return;

        for (int i = 0; i < customDebugButtons.Length; i++)
        {
            var entry = customDebugButtons[i];
            if (entry == null)
                continue;

            if (!IsCustomBindingPressed(entry.Binding))
                continue;

            if (!entry.ExecuteCommandOnPress)
            {
                entry.LastStatus = "Input received (execution disabled)";
                continue;
            }

            if (entry.Commands == null || entry.Commands.Count == 0)
            {
                entry.LastStatus = "Input received (no commands)";
                continue;
            }

            RunCustomDebugButtonAsync(entry).Forget();
        }
    }

    async UniTaskVoid RunCustomDebugButtonAsync(CustomDebugButtonEntry entry)
    {
        entry.LastStatus = "Running";

        try
        {
            var runner = ResolveCommandRunner();
            if (runner == null)
            {
                entry.LastStatus = "ICommandRunner not found";
                return;
            }

            var context = new VNext.CommandContext(
                runner.Scope,
                NullVarStore.Instance,
                runner,
                runner.Scope,
                VNext.CommandRunOptions.Default);

            var result = await runner.ExecuteListAsync(
                entry.Commands,
                context,
                System.Threading.CancellationToken.None,
                VNext.CommandRunOptions.Default);

            entry.LastStatus = result.Status == VNext.CommandRunStatus.Completed
                ? "Completed"
                : $"{result.Status}: {result.Message}";
        }
        catch (Exception ex)
        {
            entry.LastStatus = $"Error: {ex.Message}";
        }
    }

    ISaveManager ResolveSaveManager()
    {
        if (cachedSaveManager != null)
            return cachedSaveManager;

        var scopes = UnityEngine.Object.FindObjectsByType<ProjectLifetimeScope>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < scopes.Length; i++)
        {
            var scope = scopes[i];
            var container = scope != null ? scope.Container : null;
            if (container == null)
                continue;

            if (container.TryResolve<ISaveManager>(out var saveManager) && saveManager != null)
            {
                cachedSaveManager = saveManager;
                return cachedSaveManager;
            }
        }

        return null;
    }

    VNext.ICommandRunner ResolveCommandRunner()
    {
        if (cachedCommandRunner != null)
            return cachedCommandRunner;

        var scopes = UnityEngine.Object.FindObjectsByType<ProjectLifetimeScope>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < scopes.Length; i++)
        {
            var scope = scopes[i];
            var container = scope != null ? scope.Container : null;
            if (container == null)
                continue;

            if (container.TryResolve<VNext.ICommandRunner>(out var runner) && runner != null)
            {
                cachedCommandRunner = runner;
                return cachedCommandRunner;
            }
        }

        return null;
    }

    static bool IsTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F1))
            return true;
#endif
        return false;
    }

    static bool IsDeleteHoldPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.f4Key.isPressed)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.F4))
            return true;
#endif
        return false;
    }

    static bool IsCustomBindingPressed(KeyCode binding)
    {
        if (binding == KeyCode.None)
            return false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && TryGetInputSystemKeyControl(Keyboard.current, binding, out var control) && control != null && control.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(binding))
            return true;
#endif
        return false;
    }

    static string FormatBindingLabel(KeyCode binding)
    {
        return binding == KeyCode.None ? "Unbound" : binding.ToString();
    }

#if ENABLE_INPUT_SYSTEM
    static bool TryGetInputSystemKeyControl(Keyboard keyboard, KeyCode binding, out KeyControl control)
    {
        switch (binding)
        {
            case KeyCode.A: control = keyboard.aKey; return true;
            case KeyCode.B: control = keyboard.bKey; return true;
            case KeyCode.C: control = keyboard.cKey; return true;
            case KeyCode.D: control = keyboard.dKey; return true;
            case KeyCode.E: control = keyboard.eKey; return true;
            case KeyCode.F: control = keyboard.fKey; return true;
            case KeyCode.G: control = keyboard.gKey; return true;
            case KeyCode.H: control = keyboard.hKey; return true;
            case KeyCode.I: control = keyboard.iKey; return true;
            case KeyCode.J: control = keyboard.jKey; return true;
            case KeyCode.K: control = keyboard.kKey; return true;
            case KeyCode.L: control = keyboard.lKey; return true;
            case KeyCode.M: control = keyboard.mKey; return true;
            case KeyCode.N: control = keyboard.nKey; return true;
            case KeyCode.O: control = keyboard.oKey; return true;
            case KeyCode.P: control = keyboard.pKey; return true;
            case KeyCode.Q: control = keyboard.qKey; return true;
            case KeyCode.R: control = keyboard.rKey; return true;
            case KeyCode.S: control = keyboard.sKey; return true;
            case KeyCode.T: control = keyboard.tKey; return true;
            case KeyCode.U: control = keyboard.uKey; return true;
            case KeyCode.V: control = keyboard.vKey; return true;
            case KeyCode.W: control = keyboard.wKey; return true;
            case KeyCode.X: control = keyboard.xKey; return true;
            case KeyCode.Y: control = keyboard.yKey; return true;
            case KeyCode.Z: control = keyboard.zKey; return true;
            case KeyCode.Alpha0: control = keyboard.digit0Key; return true;
            case KeyCode.Alpha1: control = keyboard.digit1Key; return true;
            case KeyCode.Alpha2: control = keyboard.digit2Key; return true;
            case KeyCode.Alpha3: control = keyboard.digit3Key; return true;
            case KeyCode.Alpha4: control = keyboard.digit4Key; return true;
            case KeyCode.Alpha5: control = keyboard.digit5Key; return true;
            case KeyCode.Alpha6: control = keyboard.digit6Key; return true;
            case KeyCode.Alpha7: control = keyboard.digit7Key; return true;
            case KeyCode.Alpha8: control = keyboard.digit8Key; return true;
            case KeyCode.Alpha9: control = keyboard.digit9Key; return true;
            case KeyCode.Keypad0: control = keyboard.numpad0Key; return true;
            case KeyCode.Keypad1: control = keyboard.numpad1Key; return true;
            case KeyCode.Keypad2: control = keyboard.numpad2Key; return true;
            case KeyCode.Keypad3: control = keyboard.numpad3Key; return true;
            case KeyCode.Keypad4: control = keyboard.numpad4Key; return true;
            case KeyCode.Keypad5: control = keyboard.numpad5Key; return true;
            case KeyCode.Keypad6: control = keyboard.numpad6Key; return true;
            case KeyCode.Keypad7: control = keyboard.numpad7Key; return true;
            case KeyCode.Keypad8: control = keyboard.numpad8Key; return true;
            case KeyCode.Keypad9: control = keyboard.numpad9Key; return true;
            case KeyCode.F1: control = keyboard.f1Key; return true;
            case KeyCode.F2: control = keyboard.f2Key; return true;
            case KeyCode.F3: control = keyboard.f3Key; return true;
            case KeyCode.F4: control = keyboard.f4Key; return true;
            case KeyCode.F5: control = keyboard.f5Key; return true;
            case KeyCode.F6: control = keyboard.f6Key; return true;
            case KeyCode.F7: control = keyboard.f7Key; return true;
            case KeyCode.F8: control = keyboard.f8Key; return true;
            case KeyCode.F9: control = keyboard.f9Key; return true;
            case KeyCode.F10: control = keyboard.f10Key; return true;
            case KeyCode.F11: control = keyboard.f11Key; return true;
            case KeyCode.F12: control = keyboard.f12Key; return true;
            case KeyCode.UpArrow: control = keyboard.upArrowKey; return true;
            case KeyCode.DownArrow: control = keyboard.downArrowKey; return true;
            case KeyCode.LeftArrow: control = keyboard.leftArrowKey; return true;
            case KeyCode.RightArrow: control = keyboard.rightArrowKey; return true;
            case KeyCode.Space: control = keyboard.spaceKey; return true;
            case KeyCode.Return:
            case KeyCode.KeypadEnter: control = keyboard.enterKey; return true;
            case KeyCode.Escape: control = keyboard.escapeKey; return true;
            case KeyCode.Tab: control = keyboard.tabKey; return true;
            case KeyCode.Backspace: control = keyboard.backspaceKey; return true;
            case KeyCode.Delete: control = keyboard.deleteKey; return true;
            case KeyCode.Insert: control = keyboard.insertKey; return true;
            case KeyCode.Home: control = keyboard.homeKey; return true;
            case KeyCode.End: control = keyboard.endKey; return true;
            case KeyCode.PageUp: control = keyboard.pageUpKey; return true;
            case KeyCode.PageDown: control = keyboard.pageDownKey; return true;
            case KeyCode.CapsLock: control = keyboard.capsLockKey; return true;
            case KeyCode.Numlock: control = keyboard.numLockKey; return true;
            case KeyCode.ScrollLock: control = keyboard.scrollLockKey; return true;
            case KeyCode.Pause: control = keyboard.pauseKey; return true;
            case KeyCode.Print: control = keyboard.printScreenKey; return true;
            case KeyCode.LeftShift: control = keyboard.leftShiftKey; return true;
            case KeyCode.RightShift: control = keyboard.rightShiftKey; return true;
            case KeyCode.LeftControl: control = keyboard.leftCtrlKey; return true;
            case KeyCode.RightControl: control = keyboard.rightCtrlKey; return true;
            case KeyCode.LeftAlt: control = keyboard.leftAltKey; return true;
            case KeyCode.RightAlt: control = keyboard.rightAltKey; return true;
            case KeyCode.Minus: control = keyboard.minusKey; return true;
            case KeyCode.Equals: control = keyboard.equalsKey; return true;
            case KeyCode.LeftBracket: control = keyboard.leftBracketKey; return true;
            case KeyCode.RightBracket: control = keyboard.rightBracketKey; return true;
            case KeyCode.Backslash: control = keyboard.backslashKey; return true;
            case KeyCode.Semicolon: control = keyboard.semicolonKey; return true;
            case KeyCode.Quote: control = keyboard.quoteKey; return true;
            case KeyCode.BackQuote: control = keyboard.backquoteKey; return true;
            case KeyCode.Comma: control = keyboard.commaKey; return true;
            case KeyCode.Period: control = keyboard.periodKey; return true;
            case KeyCode.Slash: control = keyboard.slashKey; return true;
            default:
                control = null;
                return false;
        }
    }
#endif

    static string GetPlatformLabel()
    {
        return Application.platform.ToString();
    }

    static string GetBuildLabel()
    {
        if (Application.isEditor)
            return "Editor";
        return Debug.isDebugBuild ? "Development Build" : "Release Build";
    }

    static string GetRefreshRateText()
    {
#if UNITY_2022_2_OR_NEWER
        return $"{Screen.currentResolution.refreshRateRatio.value:F0}Hz";
#else
        return $"{Screen.currentResolution.refreshRate}Hz";
#endif
    }
}
