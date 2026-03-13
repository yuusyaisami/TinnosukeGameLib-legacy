#nullable enable
using System;
using System.Collections.Generic;

namespace Game.AI
{
    /// <summary>
    /// AI State スナップショット（デバッグ用）
    /// </summary>
    public readonly struct AIStateSnapshot
    {
        public readonly int Version;
        public readonly string? ActiveClipKey;
        public readonly int StackDepth;
        public readonly bool IsBlocked;
        public readonly IReadOnlyList<AIClipStackEntry> Stack;
        public readonly IReadOnlyList<AITransitionEntry> RecentTransitions;

        public AIStateSnapshot(
            int version,
            string? activeClipKey,
            int stackDepth,
            bool isBlocked,
            IReadOnlyList<AIClipStackEntry> stack,
            IReadOnlyList<AITransitionEntry> recentTransitions)
        {
            Version = version;
            ActiveClipKey = activeClipKey;
            StackDepth = stackDepth;
            IsBlocked = isBlocked;
            Stack = stack;
            RecentTransitions = recentTransitions;
        }
    }

    /// <summary>
    /// Clip スタックエントリ
    /// </summary>
    public readonly struct AIClipStackEntry
    {
        public readonly int Index;
        public readonly string StableKey;
        public readonly int Priority;
        public readonly bool IsTop;
        public readonly bool HasPopRequest;

        public AIClipStackEntry(int index, string stableKey, int priority, bool isTop, bool hasPopRequest)
        {
            Index = index;
            StableKey = stableKey;
            Priority = priority;
            IsTop = isTop;
            HasPopRequest = hasPopRequest;
        }
    }

    /// <summary>
    /// 遷移履歴エントリ
    /// </summary>
    public readonly struct AITransitionEntry
    {
        public readonly int Frame;
        public readonly string Description;

        public AITransitionEntry(int frame, string description)
        {
            Frame = frame;
            Description = description;
        }
    }

    /// <summary>
    /// AI State テレメトリインターフェース
    /// </summary>
    public interface IAIStateTelemetry
    {
        /// <summary>テレメトリバージョン（変更検出用）</summary>
        int TelemetryVersion { get; }

        /// <summary>スナップショットを取得</summary>
        AIStateSnapshot GetSnapshot();
    }
}
