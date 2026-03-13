#nullable enable
using System;
using Game.Common;
using Game.Flow;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    public enum SceneChangeMode
    {
        LoadSingle = 0,
        LoadAdditive = 1,
        Unload = 2,
    }

    public enum SceneChangeTargetMode
    {
        GameScene = 0,
        SceneName = 1,
    }

    [Serializable]
    public sealed class SceneChangeCommandData : ICommandData
    {
        public int CommandId => CommandIds.SceneChange;
        public string DebugData
        {
            get
            {
                var reload = Mode == SceneChangeMode.LoadSingle ? $" ForceReload={ForceReload}" : string.Empty;
                var sceneLabel = TargetMode == SceneChangeTargetMode.GameScene
                    ? Scene.ToString()
                    : $"Name:{SceneName.SourceTypeName}";
                return $"Mode={Mode} Scene={sceneLabel}{reload}";
            }
        }

        [LabelText("Mode")]
        public SceneChangeMode Mode = SceneChangeMode.LoadSingle;

        [LabelText("Scene Target")]
        public SceneChangeTargetMode TargetMode = SceneChangeTargetMode.GameScene;

        [LabelText("Scene")]
        [ShowIf(nameof(ShowGameScene))]
        public GameScene Scene;

        [LabelText("Scene Name")]
        [ShowIf(nameof(ShowSceneName))]
        public DynamicValue<string> SceneName;

        [ShowIf(nameof(ShowForceReload))]
        [LabelText("Force Reload")]
        public bool ForceReload;

        public float DelayLoadingCommandToSceneChangeSec = 0f; // Showコマンドが実行されてからUnityのシーン変更が実行されるまでの遅延時間（秒）

        bool ShowGameScene => TargetMode == SceneChangeTargetMode.GameScene;
        bool ShowSceneName => TargetMode == SceneChangeTargetMode.SceneName;
        bool ShowForceReload => Mode == SceneChangeMode.LoadSingle;
    }
}
