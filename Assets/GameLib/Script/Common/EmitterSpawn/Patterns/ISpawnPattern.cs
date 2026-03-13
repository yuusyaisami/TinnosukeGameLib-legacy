#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Spawn
{
    /// <summary>
    /// 生成パターンの基底インターフェース。
    /// </summary>
    public interface ISpawnPattern
    {
        string PatternId { get; }
        SpawnerKind SpawnerKind { get; }
        string SpawnerTag { get; }
        bool AutoDespawnSpawnedUnitsAfterComplete { get; }

        UniTask<SpawnContext[]> EvaluateAsync(IEmitterService emitter, CancellationToken ct = default);
    }
}
