#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Spawn
{
    /// <summary>
    /// SpawnLine 定義の基底クラス。
    /// 派生クラスで各種ラインタイプを実装。
    /// </summary>
    [Serializable]
    public abstract class SpawnLineDefinition
    {
        public abstract SpawnLine Build(IDynamicContext ctx);
        public virtual Vector3[] GetPreviewPoints(int maxPoints = 100) => Array.Empty<Vector3>();
    }







}
