#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;

namespace Game.RoomMap
{
    public interface IRoomMapSystemService
    {
        bool HasMap { get; }

        UniTask BuildAsync(RoomMapProfileSO profile, IScopeNode? lifetimeScopeParent = null, float? visualDelayOverrideSeconds = null, CancellationToken ct = default);
        UniTask ClearAsync(CancellationToken ct = default);
        UniTask ApplyVisualAsync(RoomMapTileVisualSO visual, CancellationToken ct = default);

        // Edit operations (v0.1 commands need at least delete/remove)
        UniTask RemoveRectAsync(RectInt rect, CancellationToken ct = default);
    }
}
