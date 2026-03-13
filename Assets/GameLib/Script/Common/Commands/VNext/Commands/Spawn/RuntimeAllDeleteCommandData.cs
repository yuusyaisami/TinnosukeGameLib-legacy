#nullable enable

using System;
using Game.Project.Scene.Runtime;
using Game.Spawn;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class RuntimeAllDeleteCommandData : ICommandData
    {
        public int CommandId => CommandIds.RuntimeAllDelete;
        public string DebugData
        {
            get
            {
                var tag = string.IsNullOrEmpty(SpawnerTag) ? "<none>" : SpawnerTag;
                return $"Spawner={SpawnerKind} Tag={tag}";
            }
        }

        [Header("Spawner")]
        [SerializeField]
        [EnumToggleButtons]
        public SpawnerKind SpawnerKind = SpawnerKind.RuntimeEntity;

        [SerializeField]
        public string SpawnerTag = "";

        [Header("Filter")]
        [SerializeField]
        [InlineProperty]
        [HideLabel]
        public RuntimeLifetimeScopeDeleteFilter Filter = RuntimeLifetimeScopeDeleteFilter.Default;
    }
}
