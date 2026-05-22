#nullable enable
using Game.DI;
using UnityEngine;

namespace Game.Spawn
{
    /// <summary>
    /// Generic runtime template for pooled/non-pooled emitters.
    /// Prefab must contain KernelScopeHost + EmitterMB (and related services).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Spawn/Runtime Template/Emitter", fileName = "EmitterRuntimeTemplate")]
    public sealed class EmitterRuntimeTemplateSO : BaseRuntimeObjectTemplate
    {
        // poolは強制して禁止
        protected override bool? FixedUsePooling => false;
    }
}


