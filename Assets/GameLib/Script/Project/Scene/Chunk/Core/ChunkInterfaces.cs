#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Chunk.Biome;
using UnityEngine;

namespace Game.Chunk
{
    public interface IChunkStreamer
    {
        bool TryGetChunk(ChunkCoord coord, out ChunkHandle handle);
    }

    public interface IChunkFactory
    {
        UniTask<ChunkHandle?> SpawnAsync(ChunkContext context, ChunkPlan plan, CancellationToken ct);
        UniTask ReleaseAsync(ChunkHandle handle, CancellationToken ct);
    }

    public interface IChunkContentPlanner
    {
        ChunkPlan BuildPlan(ChunkContext context);
    }

    public interface IChunkAdapter
    {
        UniTask InitializeAsync(ChunkContext context, ChunkPlan plan, CancellationToken ct);
    }

    public interface IChunkBiomeService
    {
        ChunkBiomeResult Evaluate(ChunkContext context, int seed);
        void Forget(ChunkCoord coord);
    }

    public interface IChunkViewProvider
    {
        Rect GetViewRect();
        Vector2 GetTargetPosition();
    }
}
