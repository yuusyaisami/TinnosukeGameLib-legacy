#nullable enable
using System.Collections.Generic;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Chunk
{
    [CreateAssetMenu(
        fileName = "NewChunkProfile",
        menuName = "Game/Chunk/Profile",
        order = 200)]
    public sealed class ChunkProfileSO : ScriptableObject
    {
        [BoxGroup("Commands")]
        [SerializeField] CommandListData commonCommands = new();

        [BoxGroup("Commands")]
        [TableList(AlwaysExpanded = true)]
        [SerializeField] List<ConditionalCommand> conditionalCommands = new();

        public CommandListData CommonCommands => commonCommands;
        public List<ConditionalCommand> ConditionalCommands => conditionalCommands;

        void OnValidate()
        {
            commonCommands ??= new CommandListData();
            commonCommands.EnsureIntegrity();
            if (conditionalCommands == null)
                conditionalCommands = new List<ConditionalCommand>();
        }
    }
}
