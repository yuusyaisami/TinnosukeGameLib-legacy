#nullable enable
using System;
using Game.Commands;
using Game.Common;
namespace Game.AI
{
    /// <summary>
    /// AI の状態管理サービスインターフェース
    /// </summary>
    public interface IAIStateService : IDisposable
    {
        /// <summary>現在アクティブな Clip の StableKey</summary>
        string? ActiveClipKey { get; }

        /// <summary>スタック深度</summary>
        int StackDepth { get; }

        /// <summary>Agent 共有の Vars</summary>
        IVarStore Vars { get; }

        /// <summary>Agent 単位の MonitorChannelHub</summary>
        IMonitorChannelHub MonitorHub { get; }

        /// <summary>毎フレーム呼び出し</summary>
        void Tick(float deltaTime);

        /// <summary>外部から Clip を Push</summary>
        void PushClip(AIClipSO clip);

        /// <summary>外部から現在の Clip を Pop</summary>
        void PopClip();

        /// <summary>AI が ActionBlock でブロックされているか</summary>
        bool IsBlocked { get; }
    }
}
