#nullable enable
using Game.Common;
using Game.DI;
using Game.Spawn;
using UnityEngine;

namespace Game.Chunk
{
    public sealed class ChunkStreamerConfig
    {
        public Transform? TargetTransform;
        public Camera? ViewCamera;
        public Vector2 ManualViewSize = new Vector2(32f, 32f);
        public bool UseCameraView = true;

        public ChunkSettings Settings;
        public ChunkOriginSettings OriginSettings;

        public DynamicValue<BaseRuntimeTemplatePreset> ChunkRuntimeTemplatePreset;
        public SpawnerKind SpawnerKind = SpawnerKind.RuntimeEntity;
        public string SpawnerTag = string.Empty;
        public ChunkSpawnPivot SpawnPivot = ChunkSpawnPivot.ChunkCenter;
        public Transform? ChunkParent;

        public bool TryResolveChunkRuntimeTemplate(IDynamicContext context, out BaseRuntimeTemplateSO? chunkRuntimeTemplate)
        {
            chunkRuntimeTemplate = null;
            if (!ChunkRuntimeTemplatePreset.TryGet(context, out var preset) || preset == null)
                return false;

            chunkRuntimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
            return chunkRuntimeTemplate != null;
        }
    }

    public sealed class ChunkPlannerConfig
    {
        public int Seed = 0;
        public ChunkProfileSO? DefaultProfile;
        public ChunkRuleSetSO? RuleSet;
    }
}
