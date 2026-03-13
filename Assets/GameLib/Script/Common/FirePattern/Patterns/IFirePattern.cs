#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Search;
using Game.Spawn;

namespace Game.Fire
{
    public interface IFirePattern
    {
        string PatternId { get; }
        SpawnerKind SpawnerKind { get; }
        string SpawnerTag { get; }

        UniTask<FireContext[]> EvaluateAsync(
            IFirePatternService service,
            UnitSpawnContext inputContext,
            System.Collections.Generic.IReadOnlyList<DynamicSearchHit> targetHits,
            CancellationToken ct = default);
    }
}
