#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Search;
using Game.Spawn;
using VContainer;
using Game.DI;

namespace Game.Fire
{
    public interface IFirePatternService
    {
        UniTask ExecuteAsync(
            BaseFirePattern[] patterns,
            UnitSpawnContext inputContext,
            System.Collections.Generic.IReadOnlyList<DynamicSearchHit> targetHits,
            CancellationToken ct = default);

        UniTask<IRuntimeResolver?> SpawnAndDeliverAsync(
            BaseFirePattern pattern,
            FireContext context,
            CancellationToken ct = default);

    }
}
