using System.Collections.Generic;
using UnityEngine;
using Game.Registry;

namespace Game.Actions
{
    /// <summary>
    /// GameState を階層管理する Registry。
    /// 各リーフは Enum 生成用の安定IDを持つ。
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameStateRegistry",
        menuName = "Game/Registry/Flow/Game State Registry")]
    public sealed class GameStateRegistry : HierarchyRegistryBase<GameStateNode>
    {
        [SerializeField] int nextStateId = 1;

        /// <summary>
        /// 表示パスを取得する（フォルダ/リーフ共通）。
        /// </summary>
        public override string GetKeyString(GameStateNode node)
        {
            if (node == null) return string.Empty;
            return GetDisplayPath(node.Id);
        }

        protected override void InitializeLeafNode(GameStateNode node)
        {
            base.InitializeLeafNode(node);
            node.StateId = AllocateStateId();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            EnsureUniqueIds();
        }
#endif

        int AllocateStateId()
        {
            if (nextStateId < 1)
                nextStateId = 1;

            var id = nextStateId;
            nextStateId++;
            return id;
        }

        void EnsureUniqueIds()
        {
            var used = new HashSet<int>();
            var maxId = 0;
            var changed = false;

            foreach (var node in nodes)
            {
                if (node == null || node.IsFolder)
                    continue;

                var id = node.StateId;
                if (id < 0)
                {
                    id = 0;
                    node.StateId = 0;
                    changed = true;
                }

                if (!used.Add(id))
                {
                    id = FindNextAvailableId(used);
                    node.StateId = id;
                    changed = true;
                }

                if (id > maxId)
                    maxId = id;
            }

            var desiredNext = Mathf.Max(1, maxId + 1);
            if (nextStateId < desiredNext)
            {
                nextStateId = desiredNext;
                changed = true;
            }

            if (changed)
                MarkDirty();
        }

        int FindNextAvailableId(HashSet<int> used)
        {
            var id = Mathf.Max(1, nextStateId);
            while (!used.Add(id))
                id++;

            if (nextStateId <= id)
                nextStateId = id + 1;

            return id;
        }
    }
}
