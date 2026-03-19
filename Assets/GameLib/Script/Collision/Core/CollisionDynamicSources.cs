#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Collision
{
    [Serializable]
    public sealed class DynamicColliderSetIdIntSource : IDynamicSource
    {
        [LabelText("Set Id")]
        [SerializeField]
        DynamicColliderSetRef setId = new(DynamicColliderSetId.PlayerBullet);

        public string SourceTypeName => "CollisionSetId";
        public string GetDebugData => setId.ToString();

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            return DynamicVariant.FromInt(setId.RawValue);
        }
    }
}
