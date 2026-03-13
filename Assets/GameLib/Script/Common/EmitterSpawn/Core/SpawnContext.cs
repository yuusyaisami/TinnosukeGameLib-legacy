#nullable enable
using System;
using Game.DI;
using UnityEngine;
using VContainer;

namespace Game.Spawn
{
    /// <summary>
    /// Unit 生成に必要な全情報を集約したコンテキスト。
    /// SpawnPattern の最終出力。
    /// </summary>
    public readonly struct SpawnContext
    {
        // Layer 1
        public readonly int Index;
        public readonly Vector3 Position;
        public readonly Vector3 EmitterPosition;
        public readonly Vector3 DirectionFromEmitter;
        public readonly float DistanceFromEmitter;
        public readonly Vector3 TangentDirection;
        public readonly int SpawnCount;

        // Layer 2
        public readonly SpawnData Data;

        // Spawn Target
        public readonly SpawnParams SpawnParams;



        // Emitter repeat (new)
        public readonly int EmitIndex;
        public readonly int EmitCount;

        public SpawnContext(
            int index,
            Vector3 position,
            Vector3 emitterPosition,
            Vector3 directionFromEmitter,
            float distanceFromEmitter,
            Vector3 tangentDirection,
            int spawnCount,
            SpawnData data,
            SpawnParams spawnParams,
            int emitIndex,
            int emitCount)
        {
            Index = index;
            Position = position;
            EmitterPosition = emitterPosition;
            DirectionFromEmitter = directionFromEmitter;
            DistanceFromEmitter = distanceFromEmitter;
            TangentDirection = tangentDirection;
            SpawnCount = spawnCount;
            Data = data;
            SpawnParams = spawnParams;
            EmitIndex = emitIndex;
            EmitCount = emitCount;
        }
    }

    /// <summary>
    /// SpawnContext の拡張版。Unit に渡される最終形態。
    /// ISpawnContextConsumer が受け取る。
    /// </summary>
    public readonly struct UnitSpawnContext
    {
        public readonly SpawnContext Base;
        public readonly IObjectResolver UnitResolver;
        public readonly float SpawnTime;
        public readonly int WaveIndex;

        public UnitSpawnContext(SpawnContext @base, IObjectResolver unitResolver, float spawnTime, int waveIndex)
        {
            Base = @base;
            UnitResolver = unitResolver;
            SpawnTime = spawnTime;
            WaveIndex = waveIndex;
        }
    }
}
