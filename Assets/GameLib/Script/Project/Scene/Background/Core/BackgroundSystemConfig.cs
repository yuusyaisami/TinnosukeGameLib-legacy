#nullable enable
using System.Collections.Generic;
using Game.Commands.VNext;
using UnityEngine;

namespace Game.Background
{
    public sealed class BackgroundSystemConfig
    {
        public BackgroundSpace Space;
        public BackgroundMode Mode;

        public Transform? WorldRoot;
        public RectTransform? UiRoot;
        public Camera? WorldCamera;
        public Camera? UiCamera;
        public Transform? TargetTransform;

        public bool UseCameraView = true;
        public Vector2 ManualViewSize = new Vector2(32f, 32f);

        public float UpdateIntervalSeconds = 0f;
        public int MaxSpawnPerFrame = 4;
        public int MaxRemovePerFrame = 4;
        public Vector2Int ViewMarginTiles = Vector2Int.zero;
        public Vector2Int PreloadOutsideViewTiles = Vector2Int.zero;
        public bool RunInLateUpdate = true;

        public CommandListData UpdateCommands = new();
        public List<BackgroundConditionalCommand> ConditionalCommands = new();
        public float CommandIntervalSeconds = 0f;

        public IReadOnlyList<BackgroundLayerDefinition> Layers = System.Array.Empty<BackgroundLayerDefinition>();
    }
}
