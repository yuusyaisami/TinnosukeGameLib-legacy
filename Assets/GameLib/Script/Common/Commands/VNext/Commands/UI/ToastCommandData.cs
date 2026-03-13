#nullable enable
using System;
using Game.Common;
using Game.DI;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum ToastCommandAwaitMode
    {
        RunInBackground = 0,
        WaitForShown = 10,
        WaitForClosed = 20,
    }

    [Serializable]
    public sealed class ShowToastCommandData : ICommandData
    {
        public int CommandId => CommandIds.ShowToast;
        public string DebugData
        {
            get
            {
                var tag = string.IsNullOrWhiteSpace(SystemTag) ? "default" : SystemTag;
                var template = CommandDebugDataHelper.GetDynamicDebugData(OverrideRuntimeTemplatePreset, "default");
                return $"Tag={tag} Template={template} Await={AwaitMode}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("System Tag")]
        public string SystemTag = "default";

        [BoxGroup("Template")]
        [LabelText("Override Runtime Template")]
        public DynamicValue<BaseRuntimeTemplatePreset> OverrideRuntimeTemplatePreset;

        [BoxGroup("Lifetime")]
        [LabelText("Lifetime Seconds Override")]
        public DynamicValue<float> LifetimeSecondsOverride = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Lifetime")]
        [LabelText("Await Mode")]
        public ToastCommandAwaitMode AwaitMode = ToastCommandAwaitMode.RunInBackground;

        [FoldoutGroup("Commands")]
        [LabelText("On Spawn")]
        [CommandListFunctionName("Toast.OnSpawn")]
        public CommandListData OnSpawnCommands = new();

        [FoldoutGroup("Commands")]
        [LabelText("On Show")]
        [CommandListFunctionName("Toast.OnShow")]
        public CommandListData OnShowCommands = new();

        [FoldoutGroup("Commands")]
        [LabelText("On Close")]
        [CommandListFunctionName("Toast.OnClose")]
        public CommandListData OnCloseCommands = new();

        [FoldoutGroup("Commands")]
        [LabelText("On Stack Adjusted")]
        [CommandListFunctionName("Toast.OnStackAdjusted")]
        public CommandListData OnStackAdjustedCommands = new();
    }
}
